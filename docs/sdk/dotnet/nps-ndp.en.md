English | [中文版](./nps-ndp.md)

# NPS.NDP API Reference

`NPS.NDP` provides node discovery: AnnounceFrame broadcast, `nwp://` address resolution, GraphFrame graph synchronization, and announcement signature validation.

---

## DI Registration

```csharp
builder.Services.AddNdp();
// Registers: INdpRegistry (singleton InMemoryNdpRegistry), NdpAnnounceValidator (singleton)
```

---

## NDP Frame Types

Namespace: `NPS.NDP.Frames`

### AnnounceFrame

Node/Agent presence broadcast frame:

```csharp
public sealed record AnnounceFrame : IFrame
{
    public required string                    Nid          { get; init; }
    public          string?                   NodeType     { get; init; } // "memory"|"action"|"complex"
    public required IReadOnlyList<NdpAddress> Addresses    { get; init; }
    public required IReadOnlyList<string>     Capabilities { get; init; }
    public required uint                      Ttl          { get; init; } // seconds; 0 = immediate expiry
    public required string                    Timestamp    { get; init; } // ISO 8601 UTC
    public required string                    Signature    { get; init; } // Ed25519 over canonical JSON
}
```

### NdpAddress

Physical address entry:

```csharp
public sealed record NdpAddress
{
    public required string Host     { get; init; } // hostname or IP
    public required int    Port     { get; init; }
    public required string Protocol { get; init; } // "https" | "nps-native"
}
```

**Example:**

```csharp
var announce = new AnnounceFrame
{
    Nid      = "urn:nps:node:api.example.com:products",
    NodeType = "memory",
    Addresses =
    [
        new NdpAddress { Host = "api.example.com", Port = 443,   Protocol = "https" },
        new NdpAddress { Host = "api.example.com", Port = 17433, Protocol = "nps-native" },
    ],
    Capabilities = ["nwp:query", "nwp:stream"],
    Ttl          = 300,
    Timestamp    = DateTime.UtcNow.ToString("O"),
    Signature    = NipSigner.Sign(caPrivateKey, announcePayload),
};
```

---

### ResolveFrame

Resolves a `nwp://` address to a physical endpoint (request and response share the same frame type):

```csharp
public sealed record ResolveFrame : IFrame
{
    public required string              Target        { get; init; } // nwp://host/path
    public          string?             RequesterNid  { get; init; }
    public          NdpResolveResult?   Resolved      { get; init; } // populated in response
}

public sealed record NdpResolveResult
{
    public required string  Host             { get; init; }
    public required int     Port             { get; init; }
    public          string? CertFingerprint  { get; init; }
    public required uint    Ttl              { get; init; }
}
```

**Example:**

```csharp
// Request
var req = new ResolveFrame
{
    Target       = "nwp://api.example.com/products",
    RequesterNid = "urn:nps:agent:ca.example.com:my-agent",
};

// Resolve using the registry
NdpResolveResult? result = registry.Resolve("nwp://api.example.com/products");
```

---

### GraphFrame

Topology snapshot frame:

```csharp
public sealed record GraphFrame : IFrame
{
    public required string                       GraphId  { get; init; }
    public required IReadOnlyList<NdpGraphNode>  Nodes    { get; init; } // max 256
    public required IReadOnlyList<NdpGraphEdge>  Edges    { get; init; } // max 1024
    public required uint                         Ttl      { get; init; }
    public          JsonElement?                 Metadata { get; init; }
}

public sealed record NdpGraphNode
{
    public required string                Nid           { get; init; }
    public          string?               ClusterAnchor { get; init; }
    public          IReadOnlyList<string>? NodeRoles     { get; init; }
}

public sealed record NdpGraphEdge
{
    public required string FromNid   { get; init; }
    public required string ToNid     { get; init; }
    public          uint?  LatencyMs { get; init; }
    public          string? Protocol { get; init; }
}
```

---

## INdpRegistry

Namespace: `NPS.NDP.Registry`

```csharp
public interface INdpRegistry
{
    // Register or refresh a node announcement (TTL=0 causes immediate eviction)
    void Announce(AnnounceFrame frame)

    // Resolve a nwp:// target URL to a physical endpoint
    NdpResolveResult? Resolve(string target)

    // Return all currently live announcements
    IReadOnlyList<AnnounceFrame> GetAll()

    // Return the announcement for a given NID (null if expired)
    AnnounceFrame? GetByNid(string nid)
}
```

---

## InMemoryNdpRegistry

Namespace: `NPS.NDP.Registry`

Thread-safe in-memory registry using `Lock` for synchronization:

```csharp
public sealed class InMemoryNdpRegistry : INdpRegistry
{
    // Replaceable clock (for unit testing)
    public Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;

    void Announce(AnnounceFrame frame)        // TTL=0 → evict immediately; otherwise store or refresh
    NdpResolveResult? Resolve(string target)  // scan all live NIDs for the first one covering target
    IReadOnlyList<AnnounceFrame> GetAll()     // return a snapshot (expired entries lazily evicted)
    AnnounceFrame? GetByNid(string nid)

    // Check whether a NID covers the given target URL (authority segment + path-prefix match)
    static bool NwpTargetMatchesNid(string nid, string target)
}
```

### NwpTargetMatchesNid Explained

NID format: `urn:nps:node:{authority}:{path-segments}`

Matching rule: extract the authority segment from the NID (after the 4th colon) and perform a prefix match against the `nwp://` URL's authority + path:

```csharp
// nid = "urn:nps:node:api.example.com:products"
// covers nwp://api.example.com/products and all sub-paths
InMemoryNdpRegistry.NwpTargetMatchesNid(
    "urn:nps:node:api.example.com:products",
    "nwp://api.example.com/products/123") // → true

InMemoryNdpRegistry.NwpTargetMatchesNid(
    "urn:nps:node:api.example.com:products",
    "nwp://api.example.com/orders")       // → false
```

---

## NdpAnnounceValidator

Namespace: `NPS.NDP.Validation`

Validates the Ed25519 signature and NID consistency of an AnnounceFrame:

```csharp
public sealed class NdpAnnounceValidator
{
    // Register the public key for a NID (ed25519:{base64url} format)
    void RegisterPublicKey(string nid, string encodedPubKey)

    // Remove a registered public key
    void RemovePublicKey(string nid)

    // Read-only view of all registered public keys
    IReadOnlyDictionary<string, string> KnownPublicKeys { get; }

    // Validate an AnnounceFrame (signature + NID consistency)
    NdpAnnounceResult Validate(AnnounceFrame frame)
}
```

### NdpAnnounceResult

```csharp
public sealed record NdpAnnounceResult
{
    public bool    IsValid   { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message   { get; init; }

    public static NdpAnnounceResult Ok()
    public static NdpAnnounceResult Fail(string errorCode, string message)
}
```

**Example:**

```csharp
// Register the node's public key at startup
validator.RegisterPublicKey(
    "urn:nps:node:api.example.com:products",
    NipSigner.EncodePublicKey(nodePubKey));

// Validate on receiving an announcement
var result = validator.Validate(announceFrame);
if (!result.IsValid)
{
    logger.LogWarning("Invalid announce: {Code} {Msg}", result.ErrorCode, result.Message);
    return;
}

// Write to registry after successful validation
registry.Announce(announceFrame);
```

---

## FrameRegistry Extension

Namespace: `NPS.NDP.Registry`

```csharp
// Register NDP frame types with FrameRegistryBuilder
builder.AddNdp()
// Registers: AnnounceFrame(0x30), ResolveFrame(0x31), GraphFrame(0x32)
```

---

## NDP Error Codes

Namespace: `NPS.NDP`

```csharp
NdpErrorCodes.ResolveNotFound          // "NDP-RESOLVE-NOT-FOUND"
NdpErrorCodes.ResolveAmbiguous         // "NDP-RESOLVE-AMBIGUOUS"
NdpErrorCodes.ResolveTimeout           // "NDP-RESOLVE-TIMEOUT"
NdpErrorCodes.ResolveStale             // "NDP-RESOLVE-STALE"
NdpErrorCodes.AnnounceSignatureInvalid // "NDP-ANNOUNCE-SIGNATURE-INVALID"
NdpErrorCodes.AnnounceNidMismatch      // "NDP-ANNOUNCE-NID-MISMATCH"
NdpErrorCodes.AnnounceProfileViolation // "NDP-ANNOUNCE-PROFILE-VIOLATION"
NdpErrorCodes.GraphSeqGap              // "NDP-GRAPH-SEQ-GAP"
NdpErrorCodes.GraphTooLarge            // "NDP-GRAPH-TOO-LARGE"
NdpErrorCodes.GraphInvalid             // "NDP-GRAPH-INVALID"
NdpErrorCodes.FederationLoop           // "NDP-FEDERATION-LOOP"
NdpErrorCodes.RegistryUnavailable      // "NDP-REGISTRY-UNAVAILABLE"
```

---

## Complete Usage Example

```csharp
// 1. Node startup — broadcast presence
var registry  = serviceProvider.GetRequiredService<INdpRegistry>();
var validator = serviceProvider.GetRequiredService<NdpAnnounceValidator>();

// Register the node's own public key
validator.RegisterPublicKey(myNid, NipSigner.EncodePublicKey(myPubKey));

// Build and publish the announcement
var announce = new AnnounceFrame
{
    Nid          = myNid,
    NodeType     = "memory",
    Addresses    = [new NdpAddress { Host = "api.example.com", Port = 443, Protocol = "https" }],
    Capabilities = ["nwp:query"],
    Ttl          = 300,
    Timestamp    = DateTime.UtcNow.ToString("O"),
    Signature    = NipSigner.Sign(myPrivateKey, payload),
};

var r = validator.Validate(announce);
if (r.IsValid) registry.Announce(announce);

// 2. Agent — resolve the target node
var resolved = registry.Resolve("nwp://api.example.com/products");
if (resolved is null) throw new Exception("Node not found");

var endpoint = $"{resolved.Host}:{resolved.Port}";
```

---

## Related Documentation

- [Node Types Concepts](../../concepts/node-types.en.md)
- [NPS.NOP API Reference](./nps-nop.en.md)
- [NDP Specification](../../../spec/NPS-4-NDP.md)
