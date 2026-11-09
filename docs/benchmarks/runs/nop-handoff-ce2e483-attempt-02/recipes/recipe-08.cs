// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NPS.Core.Alpha20;
using NPS.NIP;
using NPS.NIP.Ca;
using NPS.NIP.Crypto;
using NPS.NIP.Frames;
using NPS.NIP.Invocation;
using NPS.NIP.Storage;
using NPS.NIP.Verification;
using NPS.NWP.ActionNode;
using NPS.NWP.Nwm;
using NPS.NWP.Results;
using NSec.Cryptography;

namespace NPS.Tests.Nwp;

// The same Kestrel host exposes two distinct authenticated paths. Public clients
// possess Alice/Bob transport pins; only the gateway possesses the receiver's pin.
// These are explicit test provisioning, not a claim of automatic public PKI issuance.
public sealed partial class ResultInvocationTlsTests
{
    private const string IdentityCa = "urn:nps:org:fixture.ca";
    private readonly Key _identityCaKey = Key.Create(SignatureAlgorithm.Ed25519);
    private readonly Key _ownerIdentityKey = Key.Create(SignatureAlgorithm.Ed25519);
    private readonly Key _recipientIdentityKey = Key.Create(SignatureAlgorithm.Ed25519);
    private readonly X509Certificate2 _ownerCertificate = Certificate(server: false);
    private readonly X509Certificate2 _recipientCertificate = Certificate(server: false);
    private readonly ConcurrentQueue<string> _mintedProofs = new();
    private IdentFrame _aliceIdentity = null!, _bobIdentity = null!;
    private SqliteNipCaStore _identityCaStore = null!;
    private ActionNodeManifestSource _receiverManifest = null!;
    private bool _identityCaUnavailable, _gatewayEnabled = true, _withdrawKeyDuringAdmission;
    private int _gatewayRequests, _gatewayAdmissions;

    private async Task InitializeGatewayIdentities()
    {
        _identityCaStore = await SqliteNipCaStore.OpenAsync("Data Source=" + Path.Combine(_directory, "identity-ca.sqlite"));
        _aliceIdentity = await Identity(Alice, "0x01", _ownerIdentityKey);
        _bobIdentity = await Identity(Bob, "0x02", _recipientIdentityKey);
    }

    private async Task<IdentFrame> Identity(string subject, string serial, Key subjectKey)
    {
        var now = DateTime.UtcNow;
        var frame = new IdentFrame
        {
            Nid = subject, PubKey = NipSigner.EncodePublicKey(subjectKey.PublicKey),
            Capabilities = ["nwp:action"], Scope = JsonSerializer.SerializeToElement(new
            {
                nodes = new[] { "nwp://fixture/results" },
                actions = new[] { "result.create", "result.read", "result.aggregate", "result.release" },
            }),
            IssuedBy = IdentityCa, IssuedAt = now.ToString("O"), ExpiresAt = now.AddHours(1).ToString("O"),
            Serial = serial, Signature = "",
        };
        frame = frame with { Signature = NipSigner.Sign(_identityCaKey, frame) };
        await _identityCaStore.SaveAsync(new NipCertRecord
        {
            Nid = subject, EntityType = "agent", Serial = serial, PubKey = frame.PubKey,
            Capabilities = frame.Capabilities.ToArray(), ScopeJson = frame.Scope.GetRawText(),
            IssuedBy = IdentityCa, IssuedAt = now, ExpiresAt = now.AddHours(1),
        });
        return frame;
    }

    private async Task<bool> CurrentNipIdentityValid(IdentFrame frame, CancellationToken ct)
    {
        if (_identityCaUnavailable) throw new NipInvocationException("NIP-CONTEXT-VERIFIER-UNAVAILABLE");
        // Require a known live serial, in addition to the verifier's CA signature/revocation.
        var record = await _identityCaStore.GetBySerialAsync(frame.Serial, ct);
        if (record is null || record.Nid != frame.Nid || record.PubKey != frame.PubKey) return false;
        var verifier = new NipIdentVerifier(new NipVerifierOptions
        {
            TrustedIssuers = new() { [IdentityCa] = NipSigner.EncodePublicKey(_identityCaKey.PublicKey) },
            RevocationMode = NipRevocationMode.Required, OcspFailOpen = false, RevocationStore = _identityCaStore,
        });
        var result = await verifier.VerifyAsync(frame, new NipVerifyContext
        {
            RequiredCapabilities = ["nwp:action"], TargetNodePath = "nwp://fixture/results",
        }, ct);
        if (result.ErrorCode == NipErrorCodes.OcspUnavailable)
            throw new NipInvocationException("NIP-CONTEXT-VERIFIER-UNAVAILABLE");
        return result.IsValid;
    }

    private NipResultHttpProjector Projector() => new(new NipResultHttpProjectorOptions
    {
        Enabled = () => _gatewayEnabled, Issuer = Issuer, Audience = Node, KeyId = "gateway-key",
        AllowedCapabilities = new HashSet<string> { "nwp:action" }, AllowedScopes = new HashSet<string> { "results:access" },
        ResolveSigningKey = _ => ValueTask.FromResult(_keyPresent ? _projectionKey : null),
        Authenticate = async (connection, request, ct) =>
        {
            Assert.False(connection.Request.Headers.ContainsKey("X-NPS-Invocation-Context"));
            Interlocked.Increment(ref _gatewayAdmissions);
            var certificate = connection.Connection.ClientCertificate;
            if (!connection.Request.IsHttps || certificate is null || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow
                || certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow) return null;
            var pin = certificate.GetCertHashString(HashAlgorithmName.SHA256);
            var identity = pin == _ownerCertificate.GetCertHashString(HashAlgorithmName.SHA256) ? _aliceIdentity
                : pin == _recipientCertificate.GetCertHashString(HashAlgorithmName.SHA256) ? _bobIdentity : null;
            if (identity is null || !await CurrentNipIdentityValid(identity, ct)) return null;
            var operation = request.GetProperty("operation").GetString();
            if (!identity.Scope.GetProperty("actions").EnumerateArray().Any(item => item.GetString() == operation)) return null;
            if (_withdrawKeyDuringAdmission) _keyPresent = false;
            return new NipGatewayAuthority(identity.Nid, "anonymous", ["nwp:action"], ["results:access"],
                DateTimeOffset.Parse(identity.ExpiresAt).ToUnixTimeSeconds());
        },
    });

    private async Task HandlePublicGateway(HttpContext context)
    {
        // Untrusted carrier removal belongs at public ingress, before body admission.
        context.Request.Headers.Remove("X-NPS-Invocation-Context");
        if (context.Request.Method == "GET" && context.Request.Path == "/gateway/.nwm")
        {
            var manifest = _receiverManifest.Read(Node).Manifest with
            {
                Endpoints = new() { Invoke = "/gateway/invoke" },
                Profiles = new() { FeatureNegotiation = JsonSerializer.SerializeToElement(new { version = 1,
                    feature_offer = _gatewayEnabled ? new[] { new { id = "nps.data_results",
                        revisions = new[] { new { revision = 1, lifecycle = "experimental" } } } } : [] }) },
            };
            context.Response.ContentType = "application/nwp-manifest+json";
            context.Response.Headers["X-NWM-Version"] = _manifestVersion.ToString();
            await context.Response.WriteAsync(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }));
            return;
        }
        if (context.Request.Method != "POST" || context.Request.Path != "/gateway/invoke")
        { context.Response.StatusCode = 405; return; }
        Interlocked.Increment(ref _gatewayRequests);
        try
        {
            if (!_gatewayEnabled || context.Request.Headers["X-NPS-Required-Features"].Count != 1
                || context.Request.Headers["X-NPS-Required-Features"][0] != "nps.data_results=1")
                throw new NipInvocationException("NIP-CONTEXT-BINDING-MISMATCH");
            using var buffer = new MemoryStream();
            var bytes = new byte[8192];
            int count;
            while ((count = await context.Request.Body.ReadAsync(bytes, context.RequestAborted)) != 0)
            {
                if (buffer.Length + count > 65536) throw new NipInvocationException("NIP-CONTEXT-INVALID");
                buffer.Write(bytes, 0, count);
            }
            var raw = new UTF8Encoding(false, true).GetString(buffer.ToArray());
            _ = NipInvocationJson.ResultActionRequest(raw, Node, Features);
            var projector = Projector();
            using var outgoing = new ResultHttpClient(new Uri(_endpoint, "./"), Node, async (frame, target, requirements, ct) =>
            {
                var carrier = await projector.ProjectAsync(context, frame, target, requirements, ct);
                _mintedProofs.Enqueue(carrier);
                return carrier;
            }, handler => ConfigureTls(handler, _gatewayCertificate, _downstreamTraffic));
            var payload = await outgoing.SendFrameAsync(raw, context.RequestAborted);
            outgoing.Dispose(); // Include downstream connection shutdown before the public response completes.
            context.Response.ContentType = "application/nwp-capsule";
            context.Response.Headers["X-NPS-Selected-Features"] = "nps.data_results=1";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { count = 1, data = new[] { payload } }));
        }
        catch (NipInvocationException error)
        {
            var mapping = Alpha20ErrorStatus.Mappings[error.Code];
            await GatewayError(context, mapping.HttpStatus, mapping.NpsStatus, error.Code);
        }
        catch (ResultHttpException error)
        {
            await GatewayError(context, error.HttpStatus ?? 503, error.NpsStatus ?? "NPS-SERVER-UNAVAILABLE",
                error.ErrorCode ?? "NWP-RESULT-UNAVAILABLE");
        }
    }

    private static Task GatewayError(HttpContext context, int status, string npsStatus, string code)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/nwp-error+json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new { status = npsStatus, error = code }));
    }

    private void ConfigureTls(SocketsHttpHandler handler, X509Certificate2 identity, TlsTraffic? traffic = null)
    {
        handler.UseProxy = false;
        handler.AutomaticDecompression = _gzipResponses ? DecompressionMethods.GZip : DecompressionMethods.None;
        handler.SslOptions.AllowTlsResume = false;
        if (traffic is not null) traffic.Configure(handler);
        handler.SslOptions.ClientCertificates = new X509CertificateCollection { identity };
        handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, _, _) => certificate is not null
            && certificate.GetCertHashString(HashAlgorithmName.SHA256) == _serverCertificate.GetCertHashString(HashAlgorithmName.SHA256);
    }
    private ResultHttpClient PublicClient(X509Certificate2? identity = null, TlsTraffic? traffic = null) => new(new Uri(_endpoint, "/gateway"), Node,
        configureTransport: handler => ConfigureTls(handler, identity ?? _ownerCertificate, traffic));

    [Fact]
    public async Task RealGatewayUsesTwoAuthenticatedLegsAndFreshProjectionForEveryOperation()
    {
        using var client = PublicClient();
        var key = Guid.NewGuid();
        var descriptor = await client.CreateAsync("orders", key);
        Assert.Equal(descriptor, await client.CreateAsync("orders", key));
        Assert.Equal(2, (await client.ReadAsync(descriptor, 10)).Rows.Count);
        Assert.Equal(1500, (await client.AggregateAsync(descriptor, ["reason"], "amount_minor")).Groups[0].GetProperty("sum").GetInt32());
        Assert.True((await client.ReleaseAsync(descriptor)).Released);
        Assert.Equal(1, _captures);
        Assert.Equal(5, _gatewayRequests);
        Assert.Equal(10, _gatewayAdmissions);
        Assert.Equal(5, _mintedProofs.Distinct().Count());
        Assert.All(_resourceAudit, audit => Assert.Equal(Alice, audit.Subject));
    }

    [Fact]
    public async Task PublicCopiedProjectionAndAgentHintCannotSelectSubjectOrBeForwarded()
    {
        var frame = Create();
        var copied = Carrier(frame, Bob);
        using var publicHttp = Client(_ownerCertificate);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_endpoint, "/gateway/invoke"))
        { Content = new StringContent(frame, Encoding.UTF8, "application/nwp-frame") };
        request.Headers.Add("X-NPS-Required-Features", "nps.data_results=1");
        request.Headers.Add("X-NPS-Invocation-Context", copied);
        request.Headers.Add("X-NWP-Agent", Bob);
        using var response = await publicHttp.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(copied, Assert.Single(_mintedProofs));
        Assert.Equal(Alice, Assert.Single(_resourceAudit).Subject);
    }

    [Theory]
    [InlineData("unmapped")]
    [InlineData("revoked")]
    [InlineData("unavailable")]
    [InlineData("key")]
    public async Task PublicGatewayRejectsUntrustedOrWithdrawnAuthorityBeforeReceiverPost(string failure)
    {
        if (failure == "revoked") await _identityCaStore.RevokeAsync(Alice, "test revocation", DateTime.UtcNow);
        if (failure == "unavailable") _identityCaUnavailable = true;
        if (failure == "key") _withdrawKeyDuringAdmission = true;
        using var client = PublicClient(failure == "unmapped" ? _outsiderCertificate : null);
        var error = await Assert.ThrowsAsync<ResultHttpException>(() => client.CreateAsync("orders", Guid.NewGuid()));
        Assert.Equal(failure == "unavailable" ? 503 : 403, error.HttpStatus);
        Assert.Empty(_mintedProofs);
        Assert.Equal(0, _posts);
        Assert.Equal(0, _handlers);
    }

    [Fact]
    public async Task OwnerCaRevocationDeniesRetainedRecipientGrantAtActualReceiver()
    {
        using var owner = PublicClient();
        var descriptor = await owner.CreateAsync("orders", Guid.NewGuid());
        var principal = new ResultPrincipal("fixture-tenant", Alice, "v1");
        _policy.IssueGrant(principal, _store.GetOwnerGrantScope(principal, descriptor.ResultRef), Bob,
            ["result.read", "result.aggregate"], ["id", "amount_minor"], descriptor.ExpiresAt);
        using var recipient = PublicClient(_recipientCertificate);
        Assert.Single((await recipient.ReadAsync(descriptor, 1, ["id"])).Rows);
        await _identityCaStore.RevokeAsync(Alice, "test owner revocation", DateTime.UtcNow);
        var error = await Assert.ThrowsAsync<ResultHttpException>(() => recipient.ReadAsync(descriptor, 1, ["id"]));
        Assert.Equal(404, error.HttpStatus);
        Assert.Equal("NWP-RESULT-NOT-FOUND", error.ErrorCode);
        Assert.Equal(1, _captures);
    }

    [Fact]
    public async Task RecipientCaRevocationRejectsPreviouslySignedProofBeforeDispatch()
    {
        using var owner = PublicClient();
        var descriptor = await owner.CreateAsync("orders", Guid.NewGuid());
        var principal = new ResultPrincipal("fixture-tenant", Alice, "v1");
        _policy.IssueGrant(principal, _store.GetOwnerGrantScope(principal, descriptor.ResultRef), Bob,
            ["result.read"], ["id"], descriptor.ExpiresAt);
        var frame = Frame("result.read", new { result_ref = descriptor.ResultRef, snapshot_id = descriptor.SnapshotId,
            schema_ref = descriptor.SchemaRef, limit = 1, fields = new[] { "id" } });
        var freshButRevoked = Carrier(frame, Bob);
        await _identityCaStore.RevokeAsync(Bob, "test recipient revocation", DateTime.UtcNow);
        var prior = _handlers;
        await Error(await Send(frame, freshButRevoked), 403, "NIP-CONTEXT-BINDING-MISMATCH");
        Assert.Equal(prior, _handlers);
    }

    private void DisposeGatewayIdentities()
    {
        _ownerCertificate.Dispose();
        _recipientCertificate.Dispose();
        _identityCaKey.Dispose();
        _ownerIdentityKey.Dispose();
        _recipientIdentityKey.Dispose();
    }

    [Fact]
    public async Task UnavailableOwnerCaFailsBeforeReplayOrHandlerAndDoesNotDisableHealthySource()
    {
        var descriptor = await Success(await Send(Create()));
        var frame = ReadFrame(descriptor);
        var carrier = Carrier(frame);
        var prior = _handlers;
        _identityCaUnavailable = true;
        await Error(await Send(frame, carrier), 503, "NIP-CONTEXT-VERIFIER-UNAVAILABLE");
        Assert.Equal(prior, _handlers);
        _identityCaUnavailable = false;
        await Success(await Send(frame, carrier));
        Assert.Equal(prior + 1, _handlers);
    }
}
