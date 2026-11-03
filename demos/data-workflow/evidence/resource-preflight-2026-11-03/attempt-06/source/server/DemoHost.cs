// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using NPS.Core.Alpha20;
using NPS.NIP.Invocation;
using NPS.NIP.Storage;
using NPS.NWP.ActionNode;
using NPS.NWP.Extensions;
using NPS.NWP.Frames;
using NPS.NWP.Nwm;
using NPS.NWP.Results;
using NSec.Cryptography;

namespace NPS.Demo.DataWorkflow;

/// <summary>Opt-in loopback reference host using installed public SDKs, with independent public and receiver admission.</summary>
internal sealed class DemoHost(DemoAuthority authority, OrderSource source)
{
    private static readonly FeatureSelection[] Features = [new("nps.data_results", 1), new("nps.invocation_context", 1)];
    private static readonly JsonSerializerOptions Json = new()
        { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private ResultActionProvider _receiver = null!, _ordinary = null!;
    private ActionNodeManifestSource _manifest = null!;
    private Uri _endpoint = null!;
    private static ResultPrincipal Principal => new("demo-tenant", DemoAuthority.Owner, "v1");
    private sealed record PolicyDenial(ActionNodeException Error);
    private readonly object _policyDenialKey = new();
    private readonly object _originalContextKey = new();

    internal static async Task<WebApplication> StartAsync(DemoAuthority authority, OrderSource source, string directory)
    {
        var host = new DemoHost(authority, source);
        await host.ConfigureProviders(directory);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Logging.ClearProviders(); // No request bodies, proof carriers, keys or source rows in logs.
        if (Environment.GetEnvironmentVariable("NPS_DEMO_DIAGNOSTICS") == "1")
            builder.Logging.AddProvider(new ExceptionTypeLogger());
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(https =>
        {
            https.ServerCertificate = authority.Server;
            https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
            https.ClientCertificateValidation = (peer, _, _) => authority.AcceptTls(peer);
        })));
        builder.Services.AddSingleton(host._receiver);
        builder.Services.AddSingleton<IActionTaskStore, InMemoryActionTaskStore>();
        builder.Services.AddSingleton<IActionTaskCancellationRegistry, InMemoryActionTaskCancellationRegistry>();
        builder.Services.AddSingleton<IIdempotencyCache, InMemoryIdempotencyCache>();
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            try
            {
                if (context.Request.Path.StartsWithSegments("/nps")) { await host.PublicGateway(context); return; }
                if (context.Request.Path.StartsWithSegments("/ordinary")) { await host.Ordinary(context); return; }
                if (context.Request.Path.StartsWithSegments("/_demo")) { await host.Operator(context); return; }
                await next();
            }
            catch (NipInvocationException error)
            {
                var map = Alpha20ErrorStatus.Mappings[error.Code];
                await Error(context, map.HttpStatus, map.NpsStatus, error.Code);
            }
            catch (ActionNodeException error) { await Error(context, error.HttpStatus, error.NpsStatus, error.ErrorCode); }
            catch (ResultHttpException error)
            { await Error(context, error.HttpStatus ?? 503, error.NpsStatus ?? "NPS-SERVER-UNAVAILABLE", error.ErrorCode ?? "NWP-RESULT-UNAVAILABLE"); }
            catch (Exception error) when (error is JsonException or DecoderFallbackException or InvalidOperationException or ArgumentException)
            { await Error(context, 400, "NPS-CLIENT-BAD-FRAME", "DEMO-INVALID-REQUEST"); }
        });
        app.UseActionNode<ResultActionProvider>(options =>
        {
            options.NodeId = DemoAuthority.Node; options.PathPrefix = "/receiver";
            host._receiver.ConfigureNode(options);
            options.ManifestSource = new(() => new NeuralWebManifest
            {
                Nwp = "0.4", NodeId = DemoAuthority.Node, NodeType = "action", ManifestVersion = 1,
                ManifestUpdatedAt = "2026-11-02T00:00:00Z", Stability = "experimental", Actions = options.Actions,
                WireFormats = ["json", "ncp-capsule"], PreferredFormat = "json", Capabilities = new(), DataSources = ["orders"],
                Auth = new() { Required = true, IdentityType = "nip-cert", TrustedIssuers = [DemoAuthority.Ca],
                    RequiredCapabilities = ["nwp:action"], ScopeCheck = "exact" },
                Endpoints = new() { Invoke = "/receiver/invoke" }, Profiles = new() { FeatureNegotiation = FeatureOffer(Features) },
            });
            host._manifest = options.ManifestSource;
        });
        await app.StartAsync();
        host._endpoint = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        var configuration = new
        {
            kind = "synthetic loopback D20-05 reference; ephemeral one-hour credentials; not production PKI",
            base_url = new Uri(host._endpoint, "/nps").AbsoluteUri,
            ordinary_url = new Uri(host._endpoint, "/ordinary").AbsoluteUri,
            operator_url = new Uri(host._endpoint, "/_demo").AbsoluteUri,
            issuer_nid = DemoAuthority.Node, rows = source.Scale,
            server_processor_count = Environment.ProcessorCount,
            server_gc_available_memory_bytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            server_gc_is_server_gc = System.Runtime.GCSettings.IsServerGC,
            ca_file = Path.Combine(directory, "ca.pem"), certificate_file = Path.Combine(directory, "owner.pem"),
            key_file = Path.Combine(directory, "owner-key.pem"),
            operator_certificate_file = Path.Combine(directory, "operator.pem"), operator_key_file = Path.Combine(directory, "operator-key.pem"),
        };
        File.WriteAllText(Path.Combine(directory, "connection.json"), JsonSerializer.Serialize(configuration, Json));
        Console.WriteLine(Path.Combine(directory, "connection.json"));
        return app;
    }

    private async Task ConfigureProviders(string directory)
    {
        var policy = new ResultResourcePolicy(DemoAuthority.Node);
        policy.SetSourceAuthority(new(Principal.Tenant, Principal.SubjectNid, "orders", Principal.PolicyVersion,
            OrderSource.Fields, DemoAuthority.Operations, AllowModelEgress: true));
        var store = new ResultResourceStore(new ResultStoreOptions { IssuerNid = DemoAuthority.Node }, policy.Authorize);
        var sources = new Dictionary<string, ResultActionSource> { ["orders"] = new(OrderSource.Validate, source.Capture) };
        var replay = await SqliteNipProjectionReplayStore.OpenAsync(Path.Combine(directory, "replay.sqlite"),
            DemoAuthority.Node, initializeNewFile: true);
        var binding = new NipResultHttpBinding((context, _) => ValueTask.FromResult(new NipInvocationVerificationOptions
        {
            Enabled = () => true, Audience = DemoAuthority.Node,
            ChannelIssuers = () => context.Request.IsHttps && DemoAuthority.Matches(context.Connection.ClientCertificate, authority.GatewayTls)
                ? new HashSet<string> { DemoAuthority.Gateway } : new HashSet<string>(),
            SelectedFeatures = Features, AllowedCapabilities = new HashSet<string> { "nwp:action" },
            AllowedScopes = new HashSet<string> { "results:access" }, MaximumAssurance = "verified", ReplayStore = replay,
            ResolveKey = (issuer, key, _) => ValueTask.FromResult<PublicKey?>(issuer == DemoAuthority.Gateway
                && key == "demo-gateway-key" ? authority.ProjectionKey.PublicKey : null),
            ValidateOperation = (request, _) => ValueTask.FromResult(request.GetProperty("protocol").GetString() == "nwp"
                && DemoAuthority.Operations.Contains(request.GetProperty("operation").GetString())),
            Authorize = async (claim, request, ct) =>
            {
                if (claim.Subject != DemoAuthority.Owner || !claim.Capabilities.Contains("nwp:action")
                    || !claim.Scope.Contains("results:access") || !await authority.CurrentOwnerAsync(ct)) return false;
                var logicalKey = request.GetProperty("idempotency_key");
                var frame = new ActionFrame { ActionId = request.GetProperty("operation").GetString()!,
                    Params = request.GetProperty("body").GetProperty("value").GetProperty("params"),
                    IdempotencyKey = logicalKey.ValueKind == JsonValueKind.String ? logicalKey.GetString() : null };
                try { _receiver.ValidateResourceAccess(frame, new(Principal, claim.RequestDigest, ToModel: true), ct); return true; }
                catch (ActionNodeException error) { context.Items[_policyDenialKey] = new PolicyDenial(error); return false; }
            },
        }));
        _receiver = new(store, sources, async (_, context, ct) =>
        {
            try
            {
                var admission = await binding.VerifyAsync(context.HttpContext!, context.HttpRequestBody!.Value, ct);
                return new(Principal, admission.Claims.RequestDigest, ToModel: true);
            }
            catch (NipInvocationException error)
            {
                if (error.Code == "NIP-CONTEXT-BINDING-MISMATCH" && context.HttpContext!.Items.TryGetValue(_policyDenialKey, out var denial)
                    && denial is PolicyDenial policyDenial) throw policyDenial.Error;
                var map = Alpha20ErrorStatus.Mappings[error.Code];
                throw new ActionNodeException(map.HttpStatus, map.NpsStatus, error.Code, "Invocation admission denied.");
            }
        }, qualifiedHttpRouteEnabled: () => true, projectedHttp: true);
        // Ordinary HTTP uses the same public SDK provider as an internal operator engine.
        // Its external wire is named JSON endpoints; no protocol discovery/projection is claimed on this leg.
        _ordinary = new(store, sources, async (_, context, _) =>
        {
            var original = (HttpContext)context.HttpContext!.Items[_originalContextKey]!;
            if (!await authority.AdmitOwnerAsync(original)) return null;
            var canonical = NipInvocationJson.Canonicalize(context.HttpRequestBody!.Value);
            return new(Principal, "sha256:" + Convert.ToHexStringLower(SHA256.HashData(canonical)), ToModel: true);
        }, qualifiedHttpRouteEnabled: () => true);
    }

    private NipResultHttpProjector Projector() => new(new NipResultHttpProjectorOptions
    {
        Enabled = () => true, Issuer = DemoAuthority.Gateway, Audience = DemoAuthority.Node, KeyId = "demo-gateway-key",
        AllowedCapabilities = new HashSet<string> { "nwp:action" }, AllowedScopes = new HashSet<string> { "results:access" },
        ResolveSigningKey = _ => ValueTask.FromResult<Key?>(authority.ProjectionKey),
        Authenticate = async (context, request, _) => await authority.AdmitOwnerAsync(context)
            && DemoAuthority.Operations.Contains(request.GetProperty("operation").GetString())
            ? new NipGatewayAuthority(DemoAuthority.Owner, "anonymous", ["nwp:action"], ["results:access"],
                DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()) : null,
    });

    private async Task PublicGateway(HttpContext context)
    {
        context.Request.Headers.Remove("X-NPS-Invocation-Context");
        if (context.Request.Method == "GET" && context.Request.Path == "/nps/.nwm")
        {
            var manifest = _manifest.Read(DemoAuthority.Node).Manifest with
                { Endpoints = new() { Invoke = "/nps/invoke" }, Profiles = new() { FeatureNegotiation = FeatureOffer(Features[..1]) } };
            context.Response.Headers["X-NWM-Version"] = "1";
            await Write(context, manifest, "application/nwp-manifest+json"); return;
        }
        if (context.Request.Method != "POST" || context.Request.Path != "/nps/invoke")
        { context.Response.StatusCode = 404; return; }
        if (context.Request.Headers["X-NPS-Required-Features"].Count != 1
            || context.Request.Headers["X-NPS-Required-Features"][0] != "nps.data_results=1")
            throw new NipInvocationException("NIP-CONTEXT-BINDING-MISMATCH");
        if (context.Request.ContentType?.Split(';')[0].Trim() != "application/nwp-frame")
            throw ActionNodeException.ParamsInvalid("Expected frame content type.");
        var raw = await ReadBody(context);
        _ = NipInvocationJson.ResultActionRequest(raw, DemoAuthority.Node, Features);
        var projector = Projector();
        using var outgoing = new ResultHttpClient(new Uri(_endpoint, "/receiver"), DemoAuthority.Node,
            (frame, target, features, ct) => projector.ProjectAsync(context, frame, target, features, ct), authority.ConfigureGatewayTransport);
        var payload = await outgoing.SendFrameAsync(raw, context.RequestAborted);
        context.Response.Headers["X-NPS-Selected-Features"] = "nps.data_results=1";
        await Write(context, new { count = 1, data = new[] { payload } }, "application/nwp-capsule");
    }

    private async Task Ordinary(HttpContext context)
    {
        context.Request.Headers.Remove("X-NPS-Invocation-Context");
        var suffix = context.Request.Path.Value is { } path && path.StartsWith("/ordinary/", StringComparison.Ordinal)
            ? path["/ordinary/".Length..] : null;
        if (context.Request.Method != "POST" || suffix is not ("create" or "read" or "aggregate" or "release"))
        { context.Response.StatusCode = 404; return; }
        if (context.Request.ContentType?.Split(';')[0].Trim() != "application/json")
            throw ActionNodeException.ParamsInvalid("Expected JSON content type.");
        using var body = JsonDocument.Parse(await ReadBody(context));
        var request = body.RootElement;
        if (request.ValueKind != JsonValueKind.Object || request.EnumerateObject().Any(p => p.Name is not ("params" or "idempotency_key")))
            throw ActionNodeException.ParamsInvalid("Invalid ordinary request.");
        var raw = JsonSerializer.SerializeToElement(new { frame = "0x11", action_id = "result." + suffix,
            @params = request.GetProperty("params"),
            idempotency_key = request.TryGetProperty("idempotency_key", out var key) ? key.GetString() : null }, Json);
        var frame = raw.Deserialize<ActionFrame>(Json)!;
        var internalContext = new DefaultHttpContext();
        internalContext.Request.ContentType = "application/nwp-frame";
        internalContext.Request.Headers["X-NPS-Required-Features"] = "nps.data_results=1";
        internalContext.Items[_originalContextKey] = context;
        var result = await _ordinary.ExecuteAsync(frame, new ActionContext
        {
            HttpContext = internalContext, HttpRequestBody = raw, AgentNid = null, RequestId = null,
            Spec = new ActionSpec { Description = "Bounded demo operator", Async = false }, TimeoutMs = 30000, Priority = "normal",
        }, context.RequestAborted);
        await Write(context, result.Result!.Value, "application/json");
    }

    private async Task Operator(HttpContext context)
    {
        if (!DemoAuthority.Matches(context.Connection.ClientCertificate, authority.AdminTls))
        { await Error(context, 403, "NPS-AUTH-FORBIDDEN", "DEMO-OPERATOR-REQUIRED"); return; }
        if (context.Request.Method != "POST" || context.Request.Path != "/_demo/advance-source")
        { context.Response.StatusCode = 404; return; }
        // No caller-selected mutation, SQL or code. This operator action is never a model tool.
        var body = await ReadBody(context);
        if (body != "{}") throw ActionNodeException.ParamsInvalid("Empty operator command required.");
        await Write(context, new { source_revision = source.Advance() }, "application/json");
    }

    private static JsonElement FeatureOffer(IEnumerable<FeatureSelection> features) => JsonSerializer.SerializeToElement(new
        { version = 1, feature_offer = features.Select(pair => new { id = pair.Id,
            revisions = new[] { new { revision = 1, lifecycle = "experimental" } } }) });

    private static async Task<string> ReadBody(HttpContext context)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) != 0)
        {
            if (buffer.Length + count > 65536) throw new NipInvocationException("NIP-CONTEXT-INVALID");
            buffer.Write(chunk, 0, count);
        }
        var raw = new UTF8Encoding(false, true).GetString(buffer.ToArray());
        // Public canonicalizer rejects duplicates, floats, unsafe integers, invalid Unicode and excessive depth.
        using var document = JsonDocument.Parse(raw);
        _ = NipInvocationJson.Canonicalize(document.RootElement);
        return raw;
    }

    private static Task Write(HttpContext context, object payload, string mediaType)
    {
        context.Response.ContentType = mediaType;
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, Json), context.RequestAborted);
    }
    private static Task Error(HttpContext context, int status, string npsStatus, string code)
    {
        context.Response.StatusCode = status;
        return Write(context, new { status = npsStatus, error = code }, "application/nwp-error+json");
    }
}


// Opt-in local diagnosis records CLR exception types only, never formatter/state/body/claims.
internal sealed class ExceptionTypeLogger : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new TypeOnlyLogger();
    public void Dispose() { }
    private sealed class TypeOnlyLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (exception is not null) Console.Error.WriteLine("demo_exception_type:" + exception.GetType().FullName);
        }
    }
}
