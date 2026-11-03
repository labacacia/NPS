// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.ResponseCompression;
using NPS.Core.Alpha20;
using NPS.NIP.Invocation;
using NPS.NIP.Storage;
using NPS.NWP.ActionNode;
using NPS.NWP.Extensions;
using NPS.NWP.Results;
using NPS.NWP.Nwm;
using NSec.Cryptography;
using PublicKey = NSec.Cryptography.PublicKey;

namespace NPS.Tests.Nwp;

/// <summary>
/// Real loopback Kestrel HTTPS/mTLS + Ed25519 gateway projection + file replay + result store.
/// Transport test certificates are explicitly pinned; no forwarded-header identity is accepted.
/// This qualifies this receiver hop, not upstream public gateway admission/native/six-SDK parity.
/// </summary>
public sealed partial class ResultInvocationTlsTests : IAsyncLifetime
{
    private const string Issuer = "urn:nps:node:fixture:gateway", OtherIssuer = "urn:nps:node:fixture:outsider";
    private const string Alice = "urn:nps:agent:fixture:alice", Bob = "urn:nps:agent:fixture:bob";
    private const string Node = "urn:nps:node:fixture:results";
    private const string Schema = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string SecretRowValue = "protected-source-row-do-not-log";
    private static readonly FeatureSelection[] Features = [new("nps.data_results", 1), new("nps.invocation_context", 1)];
    private readonly Key _projectionKey = Key.Create(SignatureAlgorithm.Ed25519);
    private readonly X509Certificate2 _serverCertificate = Certificate(server: true);
    private readonly X509Certificate2 _gatewayCertificate = Certificate(server: false);
    private readonly X509Certificate2 _outsiderCertificate = Certificate(server: false);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "nps-result-tls-" + Guid.NewGuid().ToString("N"));
    private readonly ConcurrentQueue<NipInvocationAudit> _identityAudit = new();
    private readonly ConcurrentQueue<ResultOperationAudit> _resourceAudit = new();
    private WebApplication _app = null!;
    private HttpClient _client = null!, _outsider = null!;
    private Uri _endpoint = null!;
    private ResultResourceStore _store = null!;
    private ResultResourcePolicy _policy = null!;
    private ResultActionProvider _resultProvider = null!;
    private readonly object _resourceRejectionKey = new();
    private bool _keyPresent = true, _enabled = true, _toModel, _identityAuditUnavailable, _resourceAuditUnavailable;
    private int _captures, _handlers;
    private uint _manifestVersion = 1;
    private int _posts, _signatures;
    private bool _redirectManifest, _stripEcho, _redirectInvoke;
    private Func<NeuralWebManifest, NeuralWebManifest> _manifestChange = manifest => manifest;
    private string? _manifestBody, _operationBody;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        await InitializeGatewayIdentities();
        var replay = await SqliteNipProjectionReplayStore.OpenAsync(Path.Combine(_directory, "replay.sqlite"), Node,
            initializeNewFile: true);
        var binding = new NipResultHttpBinding((context, _) =>
        {
            // Actual TLS certificate possession is independent of the signed projection.
            // Ignore X-NWP-Agent, X-Forwarded-Client-Cert and any caller-provided issuer hint.
            IReadOnlySet<string> ChannelIssuers()
            {
                var peer = context.Connection.ClientCertificate;
                if (!context.Request.IsHttps || peer is null) return new HashSet<string>();
                var fingerprint = peer.GetCertHashString(HashAlgorithmName.SHA256);
                if (fingerprint == _gatewayCertificate.GetCertHashString(HashAlgorithmName.SHA256)) return new HashSet<string> { Issuer };
                if (fingerprint == _outsiderCertificate.GetCertHashString(HashAlgorithmName.SHA256)) return new HashSet<string> { OtherIssuer };
                return new HashSet<string>();
            }
            return ValueTask.FromResult(new NipInvocationVerificationOptions
            {
                Enabled = () => _enabled, Audience = Node, ChannelIssuers = ChannelIssuers,
                SelectedFeatures = Features, AllowedCapabilities = new HashSet<string> { "nwp:action" },
                AllowedScopes = new HashSet<string> { "results:access" }, MaximumAssurance = "verified",
                ReplayStore = replay,
                ResolveKey = (issuer, keyId, _) => ValueTask.FromResult<PublicKey?>(
                    _keyPresent && issuer == Issuer && keyId == "gateway-key" ? _projectionKey.PublicKey : null),
                ValidateOperation = (request, _) => ValueTask.FromResult(request.GetProperty("protocol").GetString() == "nwp"
                    && request.GetProperty("operation").GetString() is "result.create" or "result.read" or "result.aggregate" or "result.release"),
                Authorize = async (claim, request, ct) =>
                {
                    if (claim.Subject is not (Alice or Bob) || !claim.Capabilities.Contains("nwp:action")
                        || !claim.Scope.Contains("results:access")
                        || !await CurrentNipIdentityValid(claim.Subject == Alice ? _aliceIdentity : _bobIdentity, ct)) return false;
                    // This callback runs only AFTER signature/channel/request verification.
                    // Check actual resource policy before the durable nonce is consumed.
                    var logicalKey = request.GetProperty("idempotency_key");
                    var frame = new NPS.NWP.Frames.ActionFrame
                    {
                        ActionId = request.GetProperty("operation").GetString()!,
                        Params = request.GetProperty("body").GetProperty("value").GetProperty("params"),
                        IdempotencyKey = logicalKey.ValueKind == JsonValueKind.String ? logicalKey.GetString() : null,
                    };
                    var admission = new ResultActionAdmission(new(ResultTenant(claim.Subject), claim.Subject, "v1"), claim.RequestDigest, _toModel);
                    try { _resultProvider.ValidateResourceAccess(frame, admission, ct); return true; }
                    catch (ActionNodeException error)
                    {
                        context.Items[_resourceRejectionKey] = new ResourceRejection(error, admission, frame.ActionId);
                        return false;
                    }
                },
            });
        }, audit: audit =>
        {
            if (_identityAuditUnavailable) throw new IOException("private identity audit detail");
            _identityAudit.Enqueue(audit);
        });
        _policy = new(Node);
        _policy.SetSourceAuthority(Authority());
        _store = new(new ResultStoreOptions { IssuerNid = Node }, _policy.Authorize);
        var provider = new ResultActionProvider(_store, new Dictionary<string, ResultActionSource>
        {
            ["orders"] = new(ValidateFixtureQuery, (_, parameters, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                Interlocked.Increment(ref _captures);
                if (_oracleScale != 0) return ValueTask.FromResult(OracleCapture(parameters));
                return ValueTask.FromResult(new ResultSnapshot("source-1", Schema, JsonSerializer.SerializeToElement(new[]
                {
                    new { id = "order-a", reason = "delivery", amount_minor = 1200, note = SecretRowValue },
                    new { id = "order-b", reason = "delivery", amount_minor = 300, note = SecretRowValue },
                })));
            }),
        }, async (_, context, ct) =>
        {
            try
            {
                // This fixture source is owned by Alice. Refresh its live CA authority on
                // EVERY operation, including Bob's retained grant; caller validity is separate.
                var ownerValid = await CurrentNipIdentityValid(_aliceIdentity, ct);
                if (!ownerValid) _policy.SetSourceAuthority(Authority() with { Enabled = false });
                var admitted = await binding.VerifyAsync(context.HttpContext!, context.HttpRequestBody!.Value, ct);
                // Tenant/policy revision/egress are local service policy, never model-supplied.
                return new(new(ResultTenant(admitted.Claims.Subject), admitted.Claims.Subject, "v1"), admitted.Claims.RequestDigest, _toModel);
            }
            catch (NipInvocationException error)
            {
                // Only a locally recorded policy denial reached through verified claims may
                // retain its privacy-preserving profile code. Never mask audit/infra failures.
                if (error.Code == "NIP-CONTEXT-BINDING-MISMATCH"
                    && context.HttpContext!.Items.TryGetValue(_resourceRejectionKey, out var value)
                    && value is ResourceRejection rejection)
                {
                    if (_resourceAuditUnavailable) throw new ActionNodeException(503, "NPS-SERVER-UNAVAILABLE",
                        "NWP-RESULT-POLICY-UNAVAILABLE", "Resource audit unavailable.");
                    _resourceAudit.Enqueue(new(rejection.Admission.Principal.Tenant, rejection.Admission.Principal.SubjectNid,
                        rejection.Operation, rejection.Error.ErrorCode, null));
                    throw rejection.Error;
                }
                var mapping = Alpha20ErrorStatus.Mappings[error.Code];
                throw new ActionNodeException(mapping.HttpStatus, mapping.NpsStatus, error.Code, "Invocation admission denied.");
            }
        }, qualifiedHttpRouteEnabled: () => _enabled, projectedHttp: true, audit: audit =>
        {
            if (_resourceAuditUnavailable) throw new IOException("private resource audit detail");
            _resourceAudit.Enqueue(audit);
            _beforeResultReply?.Invoke(audit);
        });
        _resultProvider = provider;

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new RuntimeExceptionTypeLogger(_runtimeExceptionTypes));
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true; // Synthetic, explicitly pinned benchmark fixture only.
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ["application/json", "application/nwp-manifest+json", "application/nwp-capsule", "application/nwp-error+json"];
        });
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(https =>
        {
            https.ServerCertificate = _serverCertificate;
            https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
            https.ClientCertificateValidation = (certificate, _, _) =>
            {
                var hash = certificate.GetCertHashString(HashAlgorithmName.SHA256);
                return certificate.NotBefore.ToUniversalTime() <= DateTime.UtcNow && certificate.NotAfter.ToUniversalTime() > DateTime.UtcNow
                    && (hash == _ownerCertificate.GetCertHashString(HashAlgorithmName.SHA256)
                        || hash == _recipientCertificate.GetCertHashString(HashAlgorithmName.SHA256)
                        || hash == _gatewayCertificate.GetCertHashString(HashAlgorithmName.SHA256)
                        || hash == _outsiderCertificate.GetCertHashString(HashAlgorithmName.SHA256));
            };
        })));
        builder.Services.AddSingleton(new CountedProvider(provider, () => Interlocked.Increment(ref _handlers)));
        builder.Services.AddSingleton<IActionTaskStore, InMemoryActionTaskStore>();
        builder.Services.AddSingleton<IActionTaskCancellationRegistry, InMemoryActionTaskCancellationRegistry>();
        builder.Services.AddSingleton<IIdempotencyCache, InMemoryIdempotencyCache>();
        _app = builder.Build();
        _app.UseResponseCompression();
        _app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                if (_downstreamTraffic is not null && context.Response.Headers.ContentEncoding.ToString().Contains("gzip", StringComparison.Ordinal))
                    Interlocked.Increment(ref _gzipResponseCount);
                return Task.CompletedTask;
            });
            if (context.Request.Path.StartsWithSegments("/ordinary"))
            {
                await HandleOrdinaryRuntime(context);
                return;
            }
            if (context.Request.Path.StartsWithSegments("/gateway"))
            {
                await HandlePublicGateway(context);
                return;
            }
            if (_manifestBody is not null && context.Request.Path == "/results/.nwm")
            {
                context.Response.ContentType = "application/nwp-manifest+json";
                context.Response.Headers["X-NWM-Version"] = "1";
                await context.Response.WriteAsync(_manifestBody);
                return;
            }
            if (_redirectManifest && context.Request.Path == "/results/.nwm")
            {
                context.Response.Redirect("https://invalid.example/.nwm");
                return;
            }
            if (context.Request.Method == "POST")
            {
                Interlocked.Increment(ref _posts);
                if (_operationBody is not null)
                {
                    context.Response.ContentType = "application/nwp-capsule";
                    context.Response.Headers["X-NPS-Selected-Features"] = NipResultHttpBinding.RequiredFeatures;
                    await context.Response.WriteAsync(_operationBody);
                    return;
                }
                if (_redirectInvoke)
                {
                    context.Response.Redirect("https://invalid.example/invoke");
                    return;
                }
                if (_stripEcho) context.Response.OnStarting(() =>
                {
                    context.Response.Headers.Remove("X-NPS-Selected-Features");
                    return Task.CompletedTask;
                });
            }
            await next();
        });
        _app.UseActionNode<CountedProvider>(options =>
        {
            options.NodeId = Node;
            options.PathPrefix = "/results";
            provider.ConfigureNode(options);
            // Explicit fixture-only offer: receiver implementation alone never enables this.
            options.ManifestSource = new(() => _manifestChange(new NeuralWebManifest
            {
                Nwp = "0.4", NodeId = Node, NodeType = "action", ManifestVersion = _manifestVersion,
                ManifestUpdatedAt = "2026-11-02T00:00:00Z", Stability = "experimental",
                Actions = options.Actions, WireFormats = ["json", "ncp-capsule"], PreferredFormat = "json",
                Capabilities = new(), DataSources = ["orders"],
                Auth = new() { Required = true, IdentityType = "nip-cert", TrustedIssuers = ["https://fixture.invalid/ca"],
                    RequiredCapabilities = ["nwp:action"], ScopeCheck = "exact" },
                Endpoints = new() { Invoke = "/results/invoke" },
                Profiles = new() { FeatureNegotiation = JsonSerializer.SerializeToElement(new { version = 1,
                    feature_offer = _enabled ? Features.Select(pair => new { id = pair.Id,
                        revisions = new[] { new { revision = 1, lifecycle = "experimental" } } }).ToArray() : [] }) },
            }));
            _receiverManifest = options.ManifestSource;
        });
        await _app.StartAsync();
        var addresses = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses;
        _endpoint = new Uri(addresses.Single().TrimEnd('/') + "/results/invoke");
        _client = Client(_gatewayCertificate);
        _outsider = Client(_outsiderCertificate);
    }

    [Fact]
    public async Task RealTlsProjectionLifecycleAndSameKeyRecoveryUseBoundedCapsules()
    {
        var frame = Create();
        var created = await Success(await Send(frame));
        Assert.Equal(2, created.GetProperty("row_count").GetInt32());
        Assert.False(created.TryGetProperty("rows", out _));
        var replay = await Success(await Send(frame)); // Fresh projection, same logical create key.
        Assert.Equal(created.GetRawText(), replay.GetRawText());
        Assert.Equal(1, _captures);
        var page = await Success(await Send(ReadFrame(created)));
        Assert.Equal("order-a", page.GetProperty("rows")[0].GetProperty("id").GetString());
        var aggregate = Frame("result.aggregate", new
        {
            result_ref = created.GetProperty("result_ref").GetString(), snapshot_id = created.GetProperty("snapshot_id").GetString(), schema_ref = Schema,
            aggregate = new { group_by = new[] { "reason" }, operations = new[] {
                JsonSerializer.SerializeToElement(new { func = "COUNT", alias = "count" }),
                JsonSerializer.SerializeToElement(new { func = "SUM", alias = "sum", field = "amount_minor" }),
            } },
        });
        var sum = await Success(await Send(aggregate));
        Assert.Equal(1500, sum.GetProperty("groups")[0].GetProperty("sum").GetInt64());
        var release = Frame("result.release", new { result_ref = created.GetProperty("result_ref").GetString() });
        Assert.True((await Success(await Send(release))).GetProperty("released").GetBoolean());
        await Error(await Send(ReadFrame(created)), 404, "NWP-RESULT-NOT-FOUND");
        Assert.Equal(5, _handlers); // Released-result denial never dispatches the handler.
    }

    [Fact]
    public async Task CopiedValidSignatureOnAnotherAuthenticatedChannelHasZeroDispatch()
    {
        var frame = Create();
        var carrier = Carrier(frame);
        await Error(await Send(frame, carrier, _outsider), 403, "NIP-CONTEXT-UNTRUSTED");
        Assert.Equal(0, _handlers);
        Assert.Equal(0, _captures);
        await Success(await Send(frame, carrier)); // Invalid channel did not consume its projection ID.
        Assert.Equal(1, _captures);
    }

    [Fact]
    public async Task ReplayedProjectionAndTamperedBodyHaveZeroAdditionalDispatch()
    {
        var frame = Create();
        var carrier = Carrier(frame);
        await Success(await Send(frame, carrier));
        var handlers = _handlers;
        await Error(await Send(frame, carrier), 409, "NIP-CONTEXT-REPLAY");
        Assert.Equal(handlers, _handlers);
        var original = Create();
        var signed = Carrier(original);
        var changed = original.Replace("orders", "unknown-source");
        await Error(await Send(changed, signed), 422, "NWP-ACTION-PARAMS-INVALID");
        Assert.Equal(handlers, _handlers);
        await Success(await Send(original, signed));
    }

    [Fact]
    public async Task ReceiverTargetCapabilityAndLiveKeyPolicyRejectBeforeHandler()
    {
        var frame = Create();
        await Error(await Send(frame, Carrier(frame, mutate: payload => payload["audience"] = "service:other")),
            403, "NIP-CONTEXT-BINDING-MISMATCH");
        await Error(await Send(frame, Carrier(frame, mutate: payload => payload["capabilities"] = Array.Empty<string>())),
            403, "NIP-CONTEXT-BINDING-MISMATCH");
        _keyPresent = false;
        await Error(await Send(frame), 403, "NIP-CONTEXT-UNTRUSTED");
        Assert.Equal(0, _handlers);
        Assert.Equal(0, _captures);
    }

    [Theory]
    [InlineData("\"frame\":\"0x10\",")]
    [InlineData("\"frame\":true,")]
    public async Task WrongTransportDiscriminantCannotHideBehindAnActionDigest(string marker)
    {
        var frame = Create();
        var carrier = Carrier(frame);
        await Error(await Send("{" + marker + frame[1..], carrier), 400, "NWP-HTTP-FRAME-BODY-MALFORMED");
        Assert.Equal(0, _handlers);
        Assert.Equal(0, _captures);
        await Success(await Send(frame, carrier));
    }

    [Fact]
    public async Task RecipientGrantAndSourceRevocationApplyAcrossTheVerifiedHttpHop()
    {
        var created = await Success(await Send(Create()));
        var reference = created.GetProperty("result_ref").GetString()!;
        var owner = new ResultPrincipal("fixture-tenant", Alice, "v1");
        await Error(await Send(ReadFrame(created), subject: Bob), 404, "NWP-RESULT-NOT-FOUND");
        _policy.IssueGrant(owner, _store.GetOwnerGrantScope(owner, reference), Bob,
            ["result.read"], ["id"], created.GetProperty("expires_at").GetInt64());
        var narrow = ReadFrame(created, ["id"]);
        var page = await Success(await Send(narrow, subject: Bob));
        Assert.False(page.GetProperty("rows")[0].TryGetProperty("note", out _));
        var second = await Success(await Send(Create()));
        await Error(await Send(ReadFrame(second, ["id"]), subject: Bob), 404, "NWP-RESULT-NOT-FOUND");
        _policy.SetSourceAuthority(Authority() with { Enabled = false });
        await Error(await Send(narrow, subject: Bob), 404, "NWP-RESULT-NOT-FOUND");
        await Error(await Send(ReadFrame(created)), 404, "NWP-RESULT-NOT-FOUND");
    }

    [Fact]
    public async Task ResourceDenialAndSnapshotMismatchHaveZeroResultHandlerDispatch()
    {
        var created = await Success(await Send(Create()));
        var before = _handlers;
        await Error(await Send(ReadFrame(created), subject: Bob), 404, "NWP-RESULT-NOT-FOUND");
        Assert.Equal(before, _handlers);
        var mismatch = Frame("result.read", new
        {
            result_ref = created.GetProperty("result_ref").GetString(), snapshot_id = "wrong-snapshot",
            schema_ref = created.GetProperty("schema_ref").GetString(), limit = 1,
        });
        await Error(await Send(mismatch), 409, "NWP-RESULT-SNAPSHOT-MISMATCH");
        Assert.Equal(before, _handlers);
        _toModel = true;
        await Error(await Send(ReadFrame(created)), 404, "NWP-RESULT-NOT-FOUND");
        Assert.Equal(before, _handlers);
    }

    [Fact]
    public async Task ResourceDeniedProjectionDoesNotConsumeReplayBeforeAValidRecipientGrant()
    {
        var created = await Success(await Send(Create()));
        var frame = ReadFrame(created, ["id"]);
        var proof = Carrier(frame, Bob);
        var before = _handlers;
        await Error(await Send(frame, proof), 404, "NWP-RESULT-NOT-FOUND");
        Assert.Equal(before, _handlers);
        var owner = new ResultPrincipal("fixture-tenant", Alice, "v1");
        var reference = created.GetProperty("result_ref").GetString()!;
        _policy.IssueGrant(owner, _store.GetOwnerGrantScope(owner, reference), Bob, ["result.read"], ["id"],
            created.GetProperty("expires_at").GetInt64());
        await Success(await Send(frame, proof));
        Assert.Equal(before + 1, _handlers);
    }

    [Fact]
    public async Task EgressIsServerOwnedAndAuditsContainOnlyVerifiedMinimizedFacts()
    {
        var created = await Success(await Send(Create()));
        _toModel = true;
        await Error(await Send(ReadFrame(created)), 404, "NWP-RESULT-NOT-FOUND");
        Assert.Contains(_identityAudit, audit => audit.Subject == Alice && audit.Outcome == "admitted");
        Assert.Contains(_resourceAudit, audit => audit.Subject == Alice && audit.Outcome == "success");
        Assert.Contains(_resourceAudit, audit => audit.Subject == Alice && audit.Outcome == "NWP-RESULT-NOT-FOUND");
        var auditJson = JsonSerializer.Serialize(new { identity = _identityAudit.ToArray(), resource = _resourceAudit.ToArray() });
        Assert.DoesNotContain(SecretRowValue, auditJson);
        Assert.DoesNotContain("amount_minor", auditJson);
        Assert.DoesNotContain(created.GetProperty("result_ref").GetString()!, auditJson);
        Assert.DoesNotContain("forged-agent-header", auditJson);
        Assert.DoesNotContain("signature", auditJson);
    }

    [Fact]
    public async Task ResourceAuditFailureDoesNotFalselyClaimAllocationWasRolledBack()
    {
        var frame = Create();
        _resourceAuditUnavailable = true;
        await Error(await Send(frame), 503, "NWP-RESULT-POLICY-UNAVAILABLE");
        Assert.Equal(1, _captures);
        _resourceAuditUnavailable = false;
        var recovered = await Success(await Send(frame)); // Fresh projection, original logical create key.
        Assert.Equal(2, recovered.GetProperty("row_count").GetInt32());
        Assert.Equal(1, _captures);
    }

    [Fact]
    public async Task AdmissionAuditFailureClosesAccessAndRequiresFreshProjection()
    {
        var frame = Create();
        var carrier = Carrier(frame);
        _identityAuditUnavailable = true;
        await Error(await Send(frame, carrier), 503, "NIP-CONTEXT-VERIFIER-UNAVAILABLE");
        Assert.Equal(0, _handlers);
        Assert.Equal(0, _captures);
        _identityAuditUnavailable = false;
        await Error(await Send(frame, carrier), 409, "NIP-CONTEXT-REPLAY");
        await Success(await Send(frame));
        Assert.Equal(1, _captures);
    }

    [Fact]
    public async Task LostReplayFileFailsClosedOnTheActualHttpRoute()
    {
        var file = Path.Combine(_directory, "replay.sqlite");
        File.Delete(file);
        await Error(await Send(Create()), 503, "NIP-CONTEXT-VERIFIER-UNAVAILABLE");
        Assert.False(File.Exists(file));
        Assert.Equal(0, _handlers);
        Assert.Equal(0, _captures);
    }

    [Fact]
    public async Task WithdrawnRouteHasZeroDispatchAndDoesNotConsumeProjection()
    {
        var frame = Create();
        var carrier = Carrier(frame);
        _enabled = false;
        await Error(await Send(frame, carrier), 409, "NWP-FEATURE-REQUIRED-UNSUPPORTED");
        Assert.Equal(0, _handlers);
        _enabled = true;
        await Success(await Send(frame, carrier));
    }

    [Fact]
    public async Task NativeBodyCarrierCannotBeSmuggledIntoProjectedHttp()
    {
        var frame = Create();
        var carrier = Carrier(frame);
        await Error(await Send("{\"invocation_context\":\"forged\"," + frame[1..], carrier), 400, "NIP-CONTEXT-INVALID");
        Assert.Equal(0, _handlers);
        Assert.Equal(0, _captures);
        await Success(await Send(frame, carrier));
    }

    [Fact]
    public async Task SupportedClientExecutesRealTlsLifecycleAndStructuredErrors()
    {
        using var client = SupportedClient();
        var key = Guid.NewGuid();
        var descriptor = await client.CreateAsync("orders", key);
        Assert.Equal(2, descriptor.RowCount);
        Assert.Equal(descriptor, await client.CreateAsync("orders", key));
        var page = await client.ReadAsync(descriptor, 1, ["id", "amount_minor"]);
        Assert.Single(page.Rows);
        Assert.NotNull(page.NextCursor);
        var next = await client.ReadAsync(descriptor, 1, ["id", "amount_minor"], page.NextCursor);
        Assert.NotEqual(page.Rows[0].GetProperty("id").GetString(), next.Rows[0].GetProperty("id").GetString());
        var aggregate = await client.AggregateAsync(descriptor, ["reason"], "amount_minor");
        Assert.Equal(2, aggregate.Groups[0].GetProperty("count").GetInt32());
        Assert.Equal(1500, aggregate.Groups[0].GetProperty("sum").GetInt64());
        Assert.True((await client.ReleaseAsync(descriptor)).Released);
        var error = await Assert.ThrowsAsync<ResultHttpException>(() => client.ReadAsync(descriptor, 1));
        Assert.False(error.OutcomeUnknown);
        Assert.Equal(404, error.HttpStatus);
        Assert.Equal("NWP-RESULT-NOT-FOUND", error.ErrorCode);
        Assert.Equal(1, _captures);
        Assert.Equal(_posts, _signatures);
    }

    [Fact]
    public async Task ClientDiscoversWithdrawalBeforeSigningOrPosting()
    {
        using var client = SupportedClient();
        await client.CreateAsync("orders", Guid.NewGuid());
        _enabled = false;
        _manifestVersion++;
        var error = await Assert.ThrowsAsync<ResultHttpException>(() => client.CreateAsync("orders", Guid.NewGuid()));
        Assert.False(error.OutcomeUnknown);
        Assert.Equal(1, _posts);
        Assert.Equal(1, _signatures);
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("node")]
    [InlineData("route")]
    [InlineData("auth")]
    [InlineData("profile")]
    public async Task ClientRejectsNonAffirmativeDiscoveryBeforeSigning(string mutation)
    {
        _manifestChange = manifest => mutation switch
        {
            "legacy" => manifest with { Profiles = null },
            "node" => manifest with { NodeId = "urn:nps:node:fixture:wrong" },
            "route" => manifest with { Endpoints = new() { Invoke = "https://invalid.example/invoke" } },
            "auth" => manifest with { Auth = new() { Required = false } },
            _ => manifest with { Profiles = new() { FeatureNegotiation = JsonSerializer.SerializeToElement(new { version = 2, feature_offer = new object[0] }) } },
        };
        using var client = SupportedClient();
        var error = await Assert.ThrowsAsync<ResultHttpException>(() => client.CreateAsync("orders", Guid.NewGuid()));
        Assert.False(error.OutcomeUnknown);
        Assert.Equal(0, _signatures);
        Assert.Equal(0, _posts);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ClientNeverFollowsManifestOrOperationRedirects(bool manifest)
    {
        _redirectManifest = manifest;
        _redirectInvoke = !manifest;
        using var client = SupportedClient();
        var error = await Assert.ThrowsAsync<ResultHttpException>(() => client.CreateAsync("orders", Guid.NewGuid()));
        Assert.Equal(!manifest, error.OutcomeUnknown);
        Assert.Equal(manifest ? 0 : 1, _posts);
        Assert.Equal(0, _handlers);
    }

    [Fact]
    public async Task ClientDoesNotRetryMissingSuccessEchoAndRecoversOriginalKey()
    {
        _stripEcho = true;
        using var client = SupportedClient();
        var key = Guid.NewGuid();
        var error = await Assert.ThrowsAsync<ResultHttpException>(() => client.CreateAsync("orders", key));
        Assert.True(error.OutcomeUnknown);
        Assert.Equal(1, _captures);
        Assert.Equal(1, _posts);
        _stripEcho = false;
        var recovered = await client.CreateAsync("orders", key);
        Assert.Equal(2, recovered.RowCount);
        Assert.Equal(1, _captures);
        Assert.Equal(2, _posts);
    }

    [Fact]
    public async Task VersionedManifestSupportsConditionalReadAndRejectsUnversionedChange()
    {
        using var first = await _client.GetAsync(new Uri(_endpoint, ".nwm"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("1", first.Headers.GetValues("X-NWM-Version").Single());
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_endpoint, ".nwm"));
        request.Headers.TryAddWithoutValidation("If-None-Match", "1");
        using var unchanged = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);
        _enabled = false; // Same revision with changed offer is a host error, not a new valid snapshot.
        using var client = SupportedClient();
        Assert.False((await Assert.ThrowsAsync<ResultHttpException>(() => client.CreateAsync("orders", Guid.NewGuid()))).OutcomeUnknown);
        Assert.Equal(0, _posts);
        _manifestVersion++;
        using var revised = await _client.GetAsync(new Uri(_endpoint, ".nwm"));
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
        _manifestVersion = 1;
        using var rollback = await _client.GetAsync(new Uri(_endpoint, ".nwm"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, rollback.StatusCode);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"count\":\"1\",\"data\":[]}")]
    [InlineData("{\"count\":1,\"data\":[null]}")]
    [InlineData("{\"count\":1,\"count\":1,\"data\":[{}]}")]
    [InlineData("oversize")]
    public async Task ClientBoundsAndRejectsMalformedOperationBodiesWithoutRetry(string body)
    {
        _operationBody = body == "oversize" ? new string(' ', 131073) : body;
        using var client = SupportedClient();
        var error = await Assert.ThrowsAsync<ResultHttpException>(() => client.CreateAsync("orders", Guid.NewGuid()));
        Assert.True(error.OutcomeUnknown);
        Assert.Equal(1, _posts);
        Assert.Equal(0, _handlers);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"node_id\":[]}")]
    [InlineData("{\"node_id\":null,\"node_id\":null}")]
    [InlineData("oversize")]
    public async Task ClientBoundsAndRejectsMalformedDiscoveryWithoutDispatch(string body)
    {
        _manifestBody = body == "oversize" ? new string(' ', 65537) : body;
        using var client = SupportedClient();
        var error = await Assert.ThrowsAsync<ResultHttpException>(() => client.CreateAsync("orders", Guid.NewGuid()));
        Assert.False(error.OutcomeUnknown);
        Assert.Equal(0, _posts);
        Assert.Equal(0, _signatures);
    }

    [Fact]
    public async Task ClientRejectsManifestRollbackAcrossServerRestarts()
    {
        _manifestVersion = 2;
        using var client = SupportedClient();
        await client.CreateAsync("orders", Guid.NewGuid());
        using var response = await _client.GetAsync(new Uri(_endpoint, ".nwm"));
        _manifestBody = (await response.Content.ReadAsStringAsync()).Replace("\"manifest_version\":2", "\"manifest_version\":1", StringComparison.Ordinal);
        var error = await Assert.ThrowsAsync<ResultHttpException>(() => client.CreateAsync("orders", Guid.NewGuid()));
        Assert.False(error.OutcomeUnknown);
        Assert.Equal(1, _posts);
    }

    [Fact]
    public async Task ClientConvertsUntrustedFeatureFormatErrorsToDiscoveryFailure()
    {
        using var response = await _client.GetAsync(new Uri(_endpoint, ".nwm"));
        _manifestBody = (await response.Content.ReadAsStringAsync()).Replace("\"revision\":1", "\"revision\":true", StringComparison.Ordinal);
        using var client = SupportedClient();
        var error = await Assert.ThrowsAsync<ResultHttpException>(() => client.CreateAsync("orders", Guid.NewGuid()));
        Assert.False(error.OutcomeUnknown);
        Assert.Equal(0, _signatures);
        Assert.Equal(0, _posts);
    }

    private ResultHttpClient SupportedClient() => new(new Uri(_endpoint, "./"), Node,
        (raw, target, requirements, _) =>
        {
            Assert.Equal(Node, target);
            Assert.Equal(Features, requirements);
            Interlocked.Increment(ref _signatures);
            return ValueTask.FromResult(Carrier(raw));
        }, handler =>
        {
            handler.AllowAutoRedirect = true; // Client must override even a caller-enabled redirect policy.
            handler.UseProxy = false;
            handler.SslOptions.ClientCertificates = new X509CertificateCollection { _gatewayCertificate };
            handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, _, _) => certificate is not null
                && certificate.GetCertHashString(HashAlgorithmName.SHA256) == _serverCertificate.GetCertHashString(HashAlgorithmName.SHA256);
        });

    private async Task<HttpResponseMessage> Send(string frame, string? carrier = null, HttpClient? client = null, string subject = Alice)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(frame, Encoding.UTF8, "application/nwp-frame"),
        };
        request.Headers.Add("X-NPS-Required-Features", NipResultHttpBinding.RequiredFeatures);
        request.Headers.Add("X-NPS-Invocation-Context", carrier ?? Carrier(frame, subject));
        request.Headers.Add("X-NWP-Agent", "forged-agent-header");
        request.Headers.Add("X-Forwarded-Client-Cert", "forged-certificate-header");
        return await (client ?? _client).SendAsync(request);
    }

    private string Carrier(string frame, string subject = Alice, Action<Dictionary<string, object?>>? mutate = null)
    {
        var request = NipInvocationJson.ResultActionRequest(frame, Node, Features);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = new Dictionary<string, object?>
        {
            ["profile"] = "nps.invocation_context", ["revision"] = 1, ["issuer"] = Issuer, ["key_id"] = "gateway-key",
            ["audience"] = Node, ["subject"] = subject, ["assurance"] = "verified",
            ["capabilities"] = new[] { "nwp:action" }, ["scope"] = new[] { "results:access" },
            ["issued_at"] = now, ["expires_at"] = now + 60,
            ["projection_id"] = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)),
            ["request_digest"] = NipInvocationJson.RequestDigest(request),
        };
        mutate?.Invoke(payload);
        var bytes = "NPS-CONTEXT-PROJECTION/1\0"u8.ToArray().Concat(NipInvocationJson.Canonicalize(JsonSerializer.SerializeToElement(payload))).ToArray();
        var signature = SignatureAlgorithm.Ed25519.Sign(_projectionKey, bytes);
        return Encode(JsonSerializer.SerializeToUtf8Bytes(new { payload, signature = Encode(signature) }));
    }

    private HttpClient Client(X509Certificate2 identity)
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false };
        handler.ClientCertificates.Add(identity);
        handler.ServerCertificateCustomValidationCallback = (_, certificate, _, _) => certificate is not null
            && certificate.GetCertHashString(HashAlgorithmName.SHA256) == _serverCertificate.GetCertHashString(HashAlgorithmName.SHA256);
        return new(handler) { Timeout = TimeSpan.FromSeconds(10) };
    }

    private static string Create() => JsonSerializer.Serialize(new { action_id = "result.create", idempotency_key = Guid.NewGuid().ToString(),
        @params = new { source_id = "orders" } });
    private static string Frame(string operation, object parameters) => JsonSerializer.Serialize(new { action_id = operation, @params = parameters });
    private static string ReadFrame(JsonElement descriptor, string[]? fields = null) => Frame("result.read", new
    {
        result_ref = descriptor.GetProperty("result_ref").GetString(), snapshot_id = descriptor.GetProperty("snapshot_id").GetString(),
        schema_ref = descriptor.GetProperty("schema_ref").GetString(), limit = 1, fields = fields ?? ["id", "reason", "amount_minor", "note"],
    });
    private static ResultSourceAuthority Authority() => new("fixture-tenant", Alice, "orders", "v1",
        ["id", "reason", "amount_minor", "note"], ["result.create", "result.read", "result.aggregate", "result.release"], AllowDelegation: true);

    private static async Task<JsonElement> Success(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/nwp-capsule", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(NipResultHttpBinding.RequiredFeatures, response.Headers.GetValues("X-NPS-Selected-Features").Single());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data")[0].Clone();
    }
    private static async Task Error(HttpResponseMessage response, int status, string error)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/nwp-error+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("X-NPS-Selected-Features"));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(error, document.RootElement.GetProperty("error").GetString());
    }
    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static X509Certificate2 Certificate(bool server)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(server ? "CN=localhost" : "CN=test-transport-peer", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection
            { new(server ? "1.3.6.1.5.5.7.3.1" : "1.3.6.1.5.5.7.3.2") }, true));
        if (server)
        {
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName("localhost");
            names.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(names.Build());
        }
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _outsider?.Dispose();
        if (_app is not null) await _app.DisposeAsync();
        _serverCertificate.Dispose();
        _gatewayCertificate.Dispose();
        _outsiderCertificate.Dispose();
        DisposeGatewayIdentities();
        _projectionKey.Dispose();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
    private sealed record ResourceRejection(ActionNodeException Error, ResultActionAdmission Admission, string Operation);

    private sealed class CountedProvider(ResultActionProvider inner, Action entered) : IActionNodeProvider
    {
        public bool ManagesIdempotency => inner.ManagesIdempotency;
        public int? MaxRequestBytes => inner.MaxRequestBytes;
        public string ErrorMediaType => inner.ErrorMediaType;
        public ActionNodeException? GetUnknownActionError(NPS.NWP.Frames.ActionFrame frame, HttpContext context) => inner.GetUnknownActionError(frame, context);
        public Task AuthorizeAsync(NPS.NWP.Frames.ActionFrame frame, ActionContext context, CancellationToken ct = default) => inner.AuthorizeAsync(frame, context, ct);
        public Task<ActionExecutionResult> ExecuteAsync(NPS.NWP.Frames.ActionFrame frame, ActionContext context, CancellationToken ct = default)
        { entered(); return inner.ExecuteAsync(frame, context, ct); }
    }
}
