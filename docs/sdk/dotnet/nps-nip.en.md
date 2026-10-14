English | [中文版](./nps-nip.md)

# NPS.NIP API Reference

`NPS.NIP` provides two independent subsystems:
- **CA Service (`NipCaService`)**: Certificate Authority — handles NID registration, certificate issuance, renewal, and revocation.
- **Node-side Verifier (`NipIdentVerifier`)**: Six-step IdentFrame verification on the node side.

---

## DI Registration

### Verifier Only (no CA)

For ordinary nodes that only need to verify incoming IdentFrames:

```csharp
builder.Services.AddNipVerifier(opt =>
{
    opt.TrustedIssuers = new Dictionary<string, string>
    {
        ["urn:nps:org:ca.example.com"] = "https://ca.example.com"
    };
    opt.OcspUrl             = "https://ca.example.com/v1/agents/{nid}/verify";
    opt.LocalRevokedSerials = null; // or provide a local revocation set
});
```

### Full CA Service

For running a NIP CA Server:

```csharp
builder.Services.AddNipCa(opt =>
{
    opt.CaNid                = "urn:nps:org:ca.example.com";
    opt.DisplayName          = "Example NPS CA";
    opt.KeyFilePath          = "/secrets/ca.key";
    opt.KeyPassphrase        = Environment.GetEnvironmentVariable("CA_PASSPHRASE")!;
    opt.BaseUrl              = "https://ca.example.com";
    opt.ConnectionString     = Environment.GetEnvironmentVariable("DB_CONN")!;
    opt.AgentCertValidityDays = 30;
    opt.NodeCertValidityDays  = 90;
    opt.RenewalWindowDays     = 7;
}, generateKeyIfMissing: true);

// Map CA HTTP routes
app.MapNipCa();
```

---

## NIP Frame Types

Namespace: `NPS.NIP.Frames`

### IdentFrame

Agent identity declaration frame, carrying certificate information issued by a CA:

```csharp
public sealed record IdentFrame : IFrame
{
    public required string                  Nid          { get; init; } // urn:nps:agent:{ca}:{id}
    public required string                  PubKey       { get; init; } // ed25519:{base64url}
    public required IReadOnlyList<string>   Capabilities { get; init; }
    public required JsonElement             Scope        { get; init; }
    public required string                  IssuedBy     { get; init; }
    public required string                  IssuedAt     { get; init; } // ISO 8601 UTC
    public required string                  ExpiresAt    { get; init; }
    public required string                  Serial       { get; init; }
    public required string                  Signature    { get; init; } // Ed25519 over canonical JSON
    public          IdentMetadata?          Metadata     { get; init; }
}

public sealed record IdentMetadata
{
    public string? ModelFamily { get; init; } // e.g. "claude"
    public string? Tokenizer   { get; init; } // e.g. "cl100k_base"
    public string? Runtime     { get; init; } // e.g. "dotnet-10"
}
```

---

### TrustFrame

Cross-CA trust chain delegation frame:

```csharp
public sealed record TrustFrame : IFrame
{
    public required string                  GrantorNid  { get; init; }
    public required string                  GranteeCa   { get; init; }
    public required IReadOnlyList<string>   TrustScope  { get; init; }
    public required IReadOnlyList<string>   Nodes       { get; init; }
    public required string                  IssuedAt    { get; init; }
    public required string                  ExpiresAt   { get; init; }
    public required string                  Serial      { get; init; }
    public required string                  SignerNid   { get; init; }
    public required string                  Signature   { get; init; }
}
```

---

### RevokeFrame

Certificate revocation frame:

```csharp
public sealed record RevokeFrame : IFrame
{
    public required string TargetNid { get; init; }
    public          string? Serial    { get; init; }
    public required string Reason    { get; init; }
    public required string RevokedAt { get; init; }
    public          string? ParentNid { get; init; }
    public required string SignerNid { get; init; }
    public required string Signature { get; init; }
}
```

---

## NipIdentVerifier (Node-side Verifier)

Namespace: `NPS.NIP.Verification`

### Six-Step Verification

| Step | Check | Failure Error Code |
|------|-------|--------------------|
| 1 | Certificate validity: `ExpiresAt > now` | `NIP-CERT-EXPIRED` |
| 2 | Issuer trust: `IssuedBy` is in `TrustedIssuers` | `NIP-CERT-UNTRUSTED-ISSUER` |
| 3 | Signature verification: Ed25519 verify | `NIP-CERT-SIGNATURE-INVALID` |
| 4 | Local CRL + OCSP lookup | `NIP-CERT-REVOKED` / `NIP-OCSP-UNAVAILABLE` |
| 5 | Capability check: NID must declare all `RequiredCapabilities` | `NIP-CERT-CAPABILITY-MISSING` |
| 6 | Scope check: `TargetNodePath` is covered by the NID scope | `NIP-CERT-SCOPE-VIOLATION` |

```csharp
public sealed class NipIdentVerifier
{
    public NipIdentVerifier(
        NipVerifierOptions         opts,
        IHttpClientFactory?        httpFactory = null,
        ILogger<NipIdentVerifier>? logger      = null)

    // Verify an IdentFrame, returning the verification result
    public Task<NipIdentVerifyResult> VerifyAsync(
        IdentFrame          frame,
        NipVerifyContext?   context = null,
        CancellationToken   ct      = default)

    // Utility: check whether a NWP path matches a scope pattern (supports * wildcards)
    public static bool NwpPathMatches(string pattern, string path)
}
```

### NipVerifierOptions

```csharp
public sealed class NipVerifierOptions
{
    // CA NID -> CA base URL mapping (used for online verify / OCSP-style queries)
    public required Dictionary<string, string> TrustedIssuers     { get; set; }
    public          string?                    OcspUrl            { get; set; }
    public          IReadOnlySet<string>?      LocalRevokedSerials { get; set; }
}
```

### NipVerifyContext

Per-request context (required for steps 5–6):

```csharp
public sealed record NipVerifyContext
{
    public IReadOnlyList<string>? RequiredCapabilities { get; init; }
    public string?                TargetNodePath       { get; init; } // target node path
    public DateTime?              AsOf                 { get; init; } // verification time (default: now)
}
```

### NipIdentVerifyResult

```csharp
public sealed record NipIdentVerifyResult
{
    public bool    IsValid     { get; init; }
    public string? ErrorCode   { get; init; }
    public string? Message     { get; init; }
    public int     FailedStep  { get; init; } // 0 = success, 1-6 = failed step

    public static NipIdentVerifyResult Ok()
    public static NipIdentVerifyResult Fail(int step, string errorCode, string message)
}
```

**Example:**

```csharp
// Verify incoming requests in an ASP.NET Core middleware
public class NipAuthMiddleware(RequestDelegate next, NipIdentVerifier verifier)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var agentHeader = ctx.Request.Headers[NwpHttpHeaders.Agent].FirstOrDefault();
        if (agentHeader is null) { ctx.Response.StatusCode = 401; return; }

        // Parse IdentFrame from header or request body
        var frame = ParseIdentFrame(agentHeader);

        var result = await verifier.VerifyAsync(frame, new NipVerifyContext
        {
            RequiredCapabilities = ["nwp:query"],
            TargetNodePath       = ctx.Request.Path,
        });

        if (!result.IsValid)
        {
            ctx.Response.StatusCode = 403;
            await ctx.Response.WriteAsJsonAsync(new { error = result.ErrorCode });
            return;
        }

        await next(ctx);
    }
}
```

**`NwpPathMatches` usage:**

```csharp
// Supports * (single-segment) and ** (multi-segment) wildcards
NipIdentVerifier.NwpPathMatches("/products/*",  "/products/123")   // true
NipIdentVerifier.NwpPathMatches("/admin/**",    "/admin/a/b/c")    // true
NipIdentVerifier.NwpPathMatches("/products",    "/orders")         // false
```

---

## NipCaService (CA Business Logic)

Namespace: `NPS.NIP.Ca`

```csharp
public sealed class NipCaService
{
    public NipCaService(NipCaOptions opts, INipCaStore store, NipKeyManager keys)

    // Register a new Agent/Node and issue an IdentFrame
    public Task<IdentFrame> RegisterAsync(
        string                  entityType,   // "agent" | "node"
        string                  identifier,
        string                  pubKey,       // ed25519:{base64url}
        IReadOnlyList<string>   capabilities,
        string                  scopeJson,
        string?                 metadataJson = null,
        CancellationToken       ct = default)

    // Renew a certificate (only valid within the renewal window)
    public Task<IdentFrame> RenewAsync(string nid, CancellationToken ct = default)

    // Immediately revoke a certificate
    public Task<RevokeFrame> RevokeAsync(string nid, string reason, CancellationToken ct = default)

    // Check whether a NID is valid (returns NipVerifyResult)
    public Task<NipVerifyResult> VerifyAsync(string nid, CancellationToken ct = default)

    // Return the current CRL (all revoked certificate records)
    public Task<IReadOnlyList<NipCertRecord>> GetCrlAsync(CancellationToken ct = default)

    // Return the CA public key in ed25519:{base64url} format
    public string GetCaPublicKey()

    // Build a NID string
    public string BuildNid(string entityType, string identifier)
}
```

### NipVerifyResult

```csharp
public sealed class NipVerifyResult
{
    public bool           Valid     { get; init; }
    public string?        ErrorCode { get; init; }
    public string?        Message   { get; init; }
    public NipCertRecord? Record    { get; init; }

    public static NipVerifyResult Ok(NipCertRecord record)
    public static NipVerifyResult Fail(string errorCode, string message)
}
```

**Example:**

```csharp
// Issue an agent certificate
var identFrame = await caService.RegisterAsync(
    entityType:   "agent",
    identifier:   "gpt-agent-01",
    pubKey:       NipSigner.EncodePublicKey(agentPubKey),
    capabilities: ["nwp:query", "nwp:action"],
    scopeJson:    """{"paths": ["/products/*", "/orders/*"]}""");

// Revoke
var revokeFrame = await caService.RevokeAsync(
    "urn:nps:agent:ca.example.com:gpt-agent-01",
    reason: "key_compromise");
```

---

## NipCaOptions

Namespace: `NPS.NIP.Ca`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CaNid` | `string` | required | CA NID identifier |
| `DisplayName` | `string?` | `null` | Human-readable name |
| `KeyFilePath` | `string` | required | CA private key file path (AES-256-GCM encrypted) |
| `KeyPassphrase` | `string` | required | Key file decryption passphrase |
| `AgentCertValidityDays` | `int` | `30` | Agent certificate validity in days |
| `NodeCertValidityDays` | `int` | `90` | Node certificate validity in days |
| `RenewalWindowDays` | `int` | `7` | Renewal window: N days before expiry |
| `BaseUrl` | `string` | required | CA server base URL |
| `RoutePrefix` | `string` | `""` | HTTP route prefix |
| `ConnectionString` | `string` | required | PostgreSQL connection string |
| `NormalizeOcspResponseTime` | `bool` | `true` | Enforce minimum 200 ms OCSP response (timing attack mitigation) |
| `Algorithms` | `IReadOnlyList<string>` | `["ed25519"]` | Supported signature algorithms |

---

## NipCertRecord

Namespace: `NPS.NIP.Ca`

Certificate record stored in the CA database:

```csharp
public sealed class NipCertRecord
{
    public required string    Nid          { get; init; }
    public required string    EntityType   { get; init; }
    public required string    Serial       { get; init; }
    public required string    PubKey       { get; init; }
    public required string[]  Capabilities { get; init; }
    public required string    ScopeJson    { get; init; }
    public required string    IssuedBy     { get; init; }
    public required DateTime  IssuedAt     { get; init; }
    public required DateTime  ExpiresAt    { get; init; }
    public          DateTime? RevokedAt    { get; init; }
    public          string?   RevokeReason { get; init; }
    public          string?   MetadataJson { get; init; }
}
```

---

## INipCaStore

Namespace: `NPS.NIP.Ca`

Persistence abstraction; the built-in implementation is `PostgreSqlNipCaStore`:

```csharp
public interface INipCaStore
{
    Task SaveAsync(NipCertRecord record, CancellationToken ct = default)
    Task<NipCertRecord?> GetByNidAsync(string nid, CancellationToken ct = default)
    Task<NipCertRecord?> GetBySerialAsync(string serial, CancellationToken ct = default)
    Task<bool> RevokeAsync(string nid, string reason, DateTime revokedAt, CancellationToken ct = default)
    Task<string> NextSerialAsync(CancellationToken ct = default)
    Task<IReadOnlyList<NipCertRecord>> GetRevokedAsync(CancellationToken ct = default)
}
```

---

## Cryptographic Utilities

### NipSigner

Namespace: `NPS.NIP.Crypto`

Ed25519 signing and verification:

```csharp
public static class NipSigner
{
    // Sign a payload's canonical JSON with Ed25519, returning a base64url-encoded signature
    static string Sign(Key caPrivateKey, object payload)

    // Verify a signature
    static bool Verify(PublicKey caPubKey, object payload, string signature)

    // Public key encode/decode (ed25519:{base64url})
    static string     EncodePublicKey(PublicKey pubKey)
    static PublicKey? DecodePublicKey(string encoded)

    // Canonical JSON (fields sorted alphabetically, no whitespace)
    static string CanonicalJson(object obj)

    // Base64URL encode/decode
    static string Base64Url(byte[] data)
    static byte[] FromBase64Url(string s)
}
```

### NipKeyManager

Namespace: `NPS.NIP.Crypto`

CA key pair management (PBKDF2 + AES-256-GCM encrypted storage):

```csharp
public sealed class NipKeyManager : IDisposable
{
    // Constants
    const int NonceSize    = 12;       // AES-GCM nonce
    const int TagSize      = 16;       // AES-GCM tag
    const int Pbkdf2Iters  = 600_000;  // PBKDF2-SHA256 iterations
    const int RawKeyBytes  = 32;       // Ed25519 raw key length

    // Properties
    PublicKey PublicKey  { get; }
    Key       PrivateKey { get; }
    bool      IsLoaded   { get; }

    // Generate a new key pair and save it encrypted to a file
    void Generate(string keyFilePath, string passphrase)

    // Load a key pair from an encrypted file
    void Load(string keyFilePath, string passphrase)

    void Dispose()
}
```

---

## NIP Error Codes

Namespace: `NPS.NIP.Ca`

```csharp
NipErrorCodes.CertExpired      // "NIP-CERT-EXPIRED"
NipErrorCodes.CertRevoked      // "NIP-CERT-REVOKED"
NipErrorCodes.CertSigInvalid   // "NIP-CERT-SIGNATURE-INVALID"
NipErrorCodes.CertUntrusted    // "NIP-CERT-UNTRUSTED-ISSUER"
NipErrorCodes.CertCapMissing   // "NIP-CERT-CAPABILITY-MISSING"
NipErrorCodes.CertScope        // "NIP-CERT-SCOPE-VIOLATION"
NipErrorCodes.NidNotFound      // "NIP-CA-NID-NOT-FOUND"
NipErrorCodes.NidAlreadyExists // "NIP-CA-NID-ALREADY-EXISTS"
NipErrorCodes.SerialDuplicate  // "NIP-CA-SERIAL-DUPLICATE"
NipErrorCodes.RenewalTooEarly  // "NIP-CA-RENEWAL-TOO-EARLY"
NipErrorCodes.ScopeExpansion   // "NIP-CA-SCOPE-EXPANSION-DENIED"
NipErrorCodes.OcspUnavailable  // "NIP-OCSP-UNAVAILABLE"
NipErrorCodes.TrustInvalid     // "NIP-TRUST-FRAME-INVALID"
```

---

## CA HTTP Routes

`MapNipCa()` automatically mounts the following endpoints:

| Path | Method | Function |
|------|--------|----------|
| `/.well-known/nps-ca` | `GET` | CA discovery document |
| `/v1/ca/cert` | `GET` | Return the CA public key |
| `/v1/crl` | `GET` | Retrieve the signed revocation list |
| `/v1/agents/register` | `POST` | Register an agent and issue an IdentFrame |
| `/v1/nodes/register` | `POST` | Register a node and issue an IdentFrame |
| `/v1/agents/{nid}/renew` | `POST` | Renew an agent certificate |
| `/v1/nodes/{nid}/renew` | `POST` | Renew a node certificate |
| `/v1/agents/{nid}/revoke` | `POST` | Revoke an agent certificate |
| `/v1/nodes/{nid}/revoke` | `POST` | Revoke a node certificate |
| `/v1/agents/{nid}/verify` | `GET` | OCSP-style online status check for an agent |
| `/v1/nodes/{nid}/verify` | `GET` | OCSP-style online status check for a node |
| `/v1/agents/register-x509` | `POST` | Register an agent and issue an X.509-backed IdentFrame |
| `/v1/nodes/register-x509` | `POST` | Register a node and issue an X.509-backed IdentFrame |
| `/v1/orchestrators/groups/register` | `POST` | Register an orchestrator group NID |
| `/v1/orchestrators/groups/{group_nid}/sessions/issue` | `POST` | Issue a short-lived session NID under a group |
| `/v1/orchestrators/groups/{group_nid}/revoke` | `POST` | Revoke a group and cascade-revoke live sessions |
| `/v1/orchestrators/groups/{group_nid}/sessions` | `GET` | List sessions under a group |

---

## Related Documentation

- [NID Identity System Concepts](../../concepts/nid-identity.en.md)
- [NPS.NDP API Reference](./nps-ndp.en.md)
- [NIP Specification](../../../spec/NPS-3-NIP.md)
