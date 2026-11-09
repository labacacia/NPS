// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using NPS.NIP;
using NPS.NIP.Ca;
using NPS.NIP.Crypto;
using NPS.NIP.Frames;
using NPS.NIP.Storage;
using NPS.NIP.Verification;
using NSec.Cryptography;

namespace NPS.Demo.DataWorkflow;

/// <summary>One-run synthetic provisioning. TLS possession and current NIP identity are checked separately.</summary>
internal sealed class DemoAuthority : IDisposable
{
    internal const string Node = "urn:nps:node:demo:orders", Owner = "urn:nps:agent:demo:owner";
    internal const string Gateway = "urn:nps:node:demo:gateway", Ca = "urn:nps:org:demo.ca";
    internal static readonly string[] Operations = ["result.create", "result.read", "result.aggregate", "result.release"];
    private readonly Key _caKey = Key.Create(SignatureAlgorithm.Ed25519);
    private readonly Key _ownerKey = Key.Create(SignatureAlgorithm.Ed25519);
    internal Key ProjectionKey { get; } = Key.Create(SignatureAlgorithm.Ed25519);
    private readonly X509Certificate2 _tlsCa;
    internal X509Certificate2 Server { get; }
    internal X509Certificate2 OwnerTls { get; }
    internal X509Certificate2 GatewayTls { get; }
    internal X509Certificate2 AdminTls { get; }
    private SqliteNipCaStore _store = null!;
    private IdentFrame _identity = null!;

    internal DemoAuthority()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=NPS local demo CA", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        _tlsCa = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(2));
        Server = Issue("localhost", server: true);
        OwnerTls = Issue("demo-owner");
        GatewayTls = Issue("demo-gateway");
        AdminTls = Issue("demo-operator");
    }

    private X509Certificate2 Issue(string name, bool server = false)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=" + name, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(_tlsCa, includeKeyIdentifier: true, includeIssuerAndSerial: false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection
            { new(server ? "1.3.6.1.5.5.7.3.1" : "1.3.6.1.5.5.7.3.2") }, true));
        if (server)
        {
            var san = new SubjectAlternativeNameBuilder();
            san.AddDnsName("localhost"); san.AddIpAddress(System.Net.IPAddress.Loopback);
            request.CertificateExtensions.Add(san.Build());
        }
        using var certificate = request.Create(_tlsCa, DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddHours(1), RandomNumberGenerator.GetBytes(16));
        return certificate.CopyWithPrivateKey(key);
    }

    internal async Task InitializeAsync(string directory)
    {
        _store = await SqliteNipCaStore.OpenAsync("Data Source=" + Path.Combine(directory, "identity.sqlite"));
        var now = DateTime.UtcNow;
        _identity = new IdentFrame
        {
            Nid = Owner, PubKey = NipSigner.EncodePublicKey(_ownerKey.PublicKey), Capabilities = ["nwp:action"],
            Scope = JsonSerializer.SerializeToElement(new { nodes = new[] { "nwp://demo/orders" }, actions = Operations }),
            IssuedBy = Ca, IssuedAt = now.ToString("O"), ExpiresAt = now.AddHours(1).ToString("O"), Serial = "0x01", Signature = "",
        };
        _identity = _identity with { Signature = NipSigner.Sign(_caKey, _identity) };
        await _store.SaveAsync(new NipCertRecord
        {
            Nid = Owner, EntityType = "agent", Serial = _identity.Serial, PubKey = _identity.PubKey,
            Capabilities = ["nwp:action"], ScopeJson = _identity.Scope.GetRawText(), IssuedBy = Ca,
            IssuedAt = now, ExpiresAt = now.AddHours(1),
        });
        File.WriteAllText(Path.Combine(directory, "ca.pem"), _tlsCa.ExportCertificatePem());
        Export(directory, "owner", OwnerTls);
        Export(directory, "operator", AdminTls);
    }

    private static void Export(string directory, string name, X509Certificate2 certificate)
    {
        File.WriteAllText(Path.Combine(directory, name + ".pem"), certificate.ExportCertificatePem());
        var path = Path.Combine(directory, name + "-key.pem");
        using var privateKey = certificate.GetRSAPrivateKey()!;
        File.WriteAllText(path, privateKey.ExportPkcs8PrivateKeyPem());
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    internal static bool Matches(X509Certificate2? actual, X509Certificate2 expected) => actual is not null
        && actual.NotBefore.ToUniversalTime() <= DateTime.UtcNow && actual.NotAfter.ToUniversalTime() > DateTime.UtcNow
        && actual.GetCertHashString(HashAlgorithmName.SHA256) == expected.GetCertHashString(HashAlgorithmName.SHA256);

    internal bool AcceptTls(X509Certificate2 peer) => Matches(peer, OwnerTls) || Matches(peer, GatewayTls) || Matches(peer, AdminTls);

    internal async Task<bool> CurrentOwnerAsync(CancellationToken ct)
    {
        var known = await _store.GetBySerialAsync(_identity.Serial, ct);
        if (known is null || known.Nid != Owner || known.PubKey != _identity.PubKey) return false;
        var verifier = new NipIdentVerifier(new NipVerifierOptions
        {
            TrustedIssuers = new() { [Ca] = NipSigner.EncodePublicKey(_caKey.PublicKey) },
            RevocationMode = NipRevocationMode.Required, OcspFailOpen = false, RevocationStore = _store,
        });
        return (await verifier.VerifyAsync(_identity, new NipVerifyContext
            { RequiredCapabilities = ["nwp:action"], TargetNodePath = "nwp://demo/orders" }, ct)).IsValid;
    }

    internal async Task<bool> AdmitOwnerAsync(HttpContext context) => context.Request.IsHttps
        && Matches(context.Connection.ClientCertificate, OwnerTls) && await CurrentOwnerAsync(context.RequestAborted);

    internal void ConfigureGatewayTransport(SocketsHttpHandler handler)
    {
        handler.UseProxy = false;
        handler.SslOptions.AllowTlsResume = false;
        handler.SslOptions.ClientCertificates = new X509CertificateCollection { GatewayTls };
        handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, _, _) => certificate is not null
            && certificate.GetCertHashString(HashAlgorithmName.SHA256) == Server.GetCertHashString(HashAlgorithmName.SHA256);
    }

    public void Dispose()
    {
        ProjectionKey.Dispose(); _ownerKey.Dispose(); _caKey.Dispose();
        OwnerTls.Dispose(); GatewayTls.Dispose(); AdminTls.Dispose(); Server.Dispose(); _tlsCa.Dispose();
    }
}
