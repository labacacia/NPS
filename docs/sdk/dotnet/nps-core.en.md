English | [中文版](./nps-core.md)

# NPS.Core API Reference

`NPS.Core` is the foundation package for the entire NPS protocol stack. It provides the frame interface, the full frame namespace, codecs, schema anchor caching, and the exception hierarchy.

---

## DI Registration

```csharp
builder.Services.AddNpsCore(opt =>
{
    opt.DefaultTier               = EncodingTier.MsgPack;
    opt.AnchorTtlSeconds          = 3600;
    opt.MaxFramePayload           = 65535;
    opt.AllowPlaintext            = false;
    opt.EnableExtendedFrameHeader = false;
});
```

After registration, the DI container provides: `FrameRegistry` (singleton), `Tier1JsonCodec` (singleton), `Tier2MsgPackCodec` (singleton), `NpsFrameCodec` (singleton), `AnchorFrameCache` (scoped).

---

## NpsCoreOptions

Namespace: `NPS.Core.Extensions`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultTier` | `EncodingTier` | `MsgPack` | Default encoding tier |
| `AnchorTtlSeconds` | `int` | `3600` | AnchorFrame cache TTL in seconds |
| `AllowPlaintext` | `bool` | `false` | Allow plaintext connections (must be `false` in production) |
| `MaxFramePayload` | `uint` | `65535` | Maximum payload size per frame (64 KiB) |
| `EnableExtendedFrameHeader` | `bool` | `false` | Allow payloads exceeding 64 KiB using 8-byte extended headers |

---

## IFrame

Namespace: `NPS.Core.Frames`

Interface implemented by all frame types:

```csharp
public interface IFrame
{
    FrameType    FrameType     { get; }
    EncodingTier PreferredTier { get; }
}
```

---

## FrameType (enum)

Namespace: `NPS.Core.Frames`

Frame type codes, underlying type `byte`:

```csharp
// NCP (0x01–0x0F)
Anchor      = 0x01   // Schema anchor
Diff        = 0x02   // Incremental patch
Stream      = 0x03   // Streaming chunk
Caps        = 0x04   // Full response capsule
Hello       = 0x06   // Native-mode handshake

// NWP (0x10–0x1F)
Query       = 0x10   // Structured query
Action      = 0x11   // Operation invocation

// NIP (0x20–0x2F)
Ident       = 0x20   // Identity declaration
Trust       = 0x21   // Cross-CA trust chain
Revoke      = 0x22   // Certificate revocation

// NDP (0x30–0x3F)
Announce    = 0x30   // Node presence broadcast
Resolve     = 0x31   // Address resolution
Graph       = 0x32   // Node graph synchronization

// NOP (0x40–0x4F)
Task        = 0x40   // Task definition
Delegate    = 0x41   // Sub-task delegation
Sync        = 0x42   // Synchronization barrier
AlignStream = 0x43   // Directed task stream

// System (0xF0–0xFF)
Error       = 0xFE   // Unified error frame
```

---

## EncodingTier / FrameFlags (enums)

Namespace: `NPS.Core.Frames`

```csharp
public enum EncodingTier : byte
{
    Json    = 0x00,   // Tier-1: UTF-8 JSON
    MsgPack = 0x01,   // Tier-2: MessagePack binary
}

[Flags]
public enum FrameFlags : byte
{
    None         = 0x00,
    Tier2MsgPack = 0x01,  // bit 0
    Final        = 0x04,  // bit 2: last frame of a stream; all non-stream frames must set this
    Encrypted    = 0x08,  // bit 3: payload is encrypted
    Ext          = 0x80,  // bit 7: 8-byte extended header
}
```

---

## FrameHeader

Namespace: `NPS.Core.Frames`

```csharp
public record struct FrameHeader(FrameType FrameType, FrameFlags Flags, uint PayloadLength)
```

### Constants

| Constant | Value | Description |
|----------|-------|-------------|
| `DefaultSize` | `4` | Default header size in bytes |
| `ExtendedSize` | `8` | Extended header size in bytes |
| `DefaultMaxPayload` | `65535` | Max payload in default mode (64 KiB - 1) |
| `ExtendedMaxPayload` | `4294967295` | Max payload in extended mode (4 GiB - 1) |

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsExtended` | `bool` | Whether the EXT flag (bit 7) is set |
| `HeaderSize` | `int` | Header size for this instance (4 or 8) |
| `EncodingTier` | `EncodingTier` | Encoding tier extracted from lower 2 bits of Flags |
| `IsFinal` | `bool` | Whether the FINAL flag (bit 2) is set |
| `IsEncrypted` | `bool` | Whether the ENC flag (bit 3) is set |

### Methods

```csharp
// Parse a frame header from the start of a buffer (auto-detects 4/8-byte mode)
static FrameHeader Parse(ReadOnlySpan<byte> buffer)

// Write this header into a buffer
void WriteTo(Span<byte> buffer)
```

---

## NCP Frame Types

Namespace: `NPS.Core.Frames.Ncp`

### AnchorFrame

Establishes a schema reference, allowing subsequent frames to reference it by `anchor_id` only.

```csharp
public sealed record AnchorFrame : IFrame
{
    public required string      AnchorId { get; init; } // sha256:{64 hex chars}
    public required FrameSchema Schema   { get; init; }
    public          uint        Ttl      { get; init; } = 3600; // seconds; 0 = no cache
}
```

**Example:**

```csharp
var schema = new FrameSchema
{
    Fields =
    [
        new SchemaField("id",    "uint64",  Semantic: "entity.id"),
        new SchemaField("name",  "string",  Semantic: "entity.label"),
        new SchemaField("price", "decimal", Semantic: "commerce.price.usd"),
    ]
};

var frame = new AnchorFrame
{
    AnchorId = AnchorIdComputer.Compute(schema),
    Schema   = schema,
    Ttl      = 3600,
};

byte[] wire = codec.Encode(frame);
```

---

### DiffFrame

Transmits only changed fields. Dramatically reduces token consumption in high-frequency update scenarios.

```csharp
public sealed record DiffFrame : IFrame
{
    public required string                          AnchorRef    { get; init; }
    public required ulong                           BaseSeq      { get; init; }
    public          string                          PatchFormat  { get; init; } = "json_patch";
    public          IReadOnlyList<JsonPatchOperation>? Patch     { get; init; }
    public          byte[]?                         BinaryBitset { get; init; }
    public          string?                         EntityId     { get; init; }
}
```

`JsonPatchOperation` follows RFC 6902. `Op` values: `add`, `remove`, `replace`, `move`, `copy`, `test`.

`PatchFormat` values (see `NcpPatchFormat`):

| Value | Description |
|-------|-------------|
| `"json_patch"` | RFC 6902 JSON Patch; compatible with all tiers |
| `"binary_bitset"` | Compact binary format; **only** allowed with Tier-2 (MsgPack) |

**Example:**

```csharp
var diff = new DiffFrame
{
    AnchorRef = anchorId,
    BaseSeq   = 42,
    Patch     =
    [
        new JsonPatchOperation("replace", "/price", Value: JsonSerializer.SerializeToElement(99.99m)),
    ],
};
```

---

### StreamFrame

Streaming chunk frame. Enables paginated streaming of large result sets with back-pressure support.

```csharp
public sealed record StreamFrame : IFrame
{
    public required string                     StreamId   { get; init; }
    public required uint                        Seq        { get; init; }
    public required bool                        IsLast     { get; init; }
    public          string?                     AnchorRef  { get; init; }
    public required IReadOnlyList<JsonElement>  Data       { get; init; }
    public          uint?                       WindowSize { get; init; }
    public          string?                     ErrorCode  { get; init; }
}
```

**Streaming protocol:**
- `Seq` is strictly monotonically increasing from 0
- `IsLast = true` signals end of stream; if `ErrorCode != null` the stream terminated abnormally
- `WindowSize` enables back-pressure: the consumer notifies the sender of the new window after processing a batch

---

### CapsFrame

Full response capsule. Contains complete data rows described by a schema, with cursor-based pagination.

```csharp
public sealed record CapsFrame : IFrame
{
    public required string                     AnchorRef     { get; init; }
    public required uint                       Count         { get; init; }
    public required IReadOnlyList<JsonElement> Data          { get; init; }
    public          string?                    NextCursor    { get; init; }
    public          uint?                      TokenEst      { get; init; }
    public          bool?                      Cached        { get; init; }
    public          string?                    TokenizerUsed { get; init; }
    public          AnchorFrame?               InlineAnchor  { get; init; }
}
```

`InlineAnchor`: when the schema has been updated, the node should inline the latest `AnchorFrame` to avoid a round-trip.

---

### HelloFrame

Native-mode handshake frame. Sent by clients to declare supported NPS versions, encodings, and protocols.

```csharp
public sealed record HelloFrame : IFrame
{
    public required string                NpsVersion           { get; init; } // "0.4"
    public          string?               MinVersion           { get; init; }
    public required IReadOnlyList<string> SupportedEncodings   { get; init; }
    public required IReadOnlyList<string> SupportedProtocols   { get; init; }
    public          string?               AgentId              { get; init; }
    public          uint                  MaxFramePayload      { get; init; } = 65535;
    public          bool                  ExtSupport           { get; init; } = false;
    public          uint                  MaxConcurrentStreams  { get; init; } = 32;
    public          IReadOnlyList<string>? E2EEncAlgorithms    { get; init; }
}
```

---

### ErrorFrame

Unified error frame shared across all protocol layers.

```csharp
public sealed record ErrorFrame : IFrame
{
    public required string       Status  { get; init; } // NPS status code
    public required string       Error   { get; init; } // Protocol-level error code
    public          string?      Message { get; init; }
    public          JsonElement? Details { get; init; }
}
```

---

## FrameSchema / SchemaField

Namespace: `NPS.Core.Frames`

```csharp
public record FrameSchema
{
    public required IReadOnlyList<SchemaField> Fields { get; init; }
}

// Positional record
public record SchemaField(
    string  Name,
    string  Type,
    string? Semantic = null,
    bool    Nullable = false
);
```

---

## NpsFrameCodec

Namespace: `NPS.Core.Codecs`

The main codec — responsible for header read/write and tier routing:

```csharp
public class NpsFrameCodec
{
    // Constructor (typically injected by DI)
    public NpsFrameCodec(
        Tier1JsonCodec          json,
        Tier2MsgPackCodec       msgpack,
        Tier3BinaryVectorCodec  binaryVector,
        FrameRegistry           registry,
        uint                    maxPayload = 65535)

    // Encode: serialize frame and write header; overrideTier is for negotiated policy,
    // diagnostics, and tests, not arbitrary workflow-level switching.
    public byte[] Encode(IFrame frame, EncodingTier? overrideTier = null)

    // Decode: parse a complete wire message (header + payload) into a strongly-typed frame
    public IFrame Decode(ReadOnlySpan<byte> wire)

    // Parse header only, without deserializing the payload
    public static FrameHeader PeekHeader(ReadOnlySpan<byte> wire)
}
```

**Example:**

```csharp
public class MyService(NpsFrameCodec codec)
{
    public byte[] EncodeQuery(QueryFrame q) => codec.Encode(q);

    public IFrame ParseIncoming(byte[] wire) => codec.Decode(wire);

    // Force JSON encoding (debugging)
    public byte[] DebugEncode(IFrame f) => codec.Encode(f, EncodingTier.Json);
}
```

---

## Tier1JsonCodec / Tier2MsgPackCodec

Namespace: `NPS.Core.Codecs`

Low-level tier codecs, implementing `IFrameCodec`:

```csharp
public interface IFrameCodec
{
    byte[] Encode(IFrame frame);
    IFrame Decode(FrameType type, ReadOnlySpan<byte> payload, FrameRegistry registry);
}
```

Generally accessed through `NpsFrameCodec` rather than directly.

---

## FrameRegistry / FrameRegistryBuilder

Namespace: `NPS.Core.Registry`

Maps frame type codes to CLR types:

```csharp
// Build (fluent API)
var registry = new FrameRegistryBuilder()
    .AddNcp()   // register all NCP core frames
    .AddNwp()   // register NWP frames (QueryFrame, ActionFrame)
    .AddNip()   // register NIP frames (IdentFrame, TrustFrame, RevokeFrame)
    .AddNdp()   // register NDP frames (AnnounceFrame, ResolveFrame, GraphFrame)
    .Build();

// Query
Type clrType = registry.Resolve(FrameType.Anchor); // → typeof(AnchorFrame)
```

---

## AnchorFrameCache

Namespace: `NPS.Core.Caching`

Thread-safe in-memory schema cache (DI lifetime: Scoped):

```csharp
// Store and return anchor_id (throws NpsAnchorPoisonException on schema conflict)
string anchorId = cache.Set(anchorFrame);

// Try-get
if (cache.TryGet(anchorId, out AnchorFrame? found))
{
    // use found.Schema
}

// Required get (throws NpsAnchorNotFoundException if missing)
AnchorFrame frame = cache.GetRequired(anchorId);
```

---

## AnchorIdComputer

Namespace: `NPS.Core.Anchoring`

```csharp
// Compute anchor_id (sha256:{hex})
string id = AnchorIdComputer.Compute(schema);

// Debug: return the canonical JSON string used for hashing
string json = AnchorIdComputer.CanonicalJson(schema);
```

---

## Exception Hierarchy

Namespace: `NPS.Core.Exceptions`

```
NpsException
├── NpsFrameException              — frame is unparseable or structurally invalid
├── NpsCodecException              — encoding or decoding failed at the codec layer
├── NpsAnchorNotFoundException     — anchor_id not found in cache
├── NpsAnchorPoisonException       — same anchor_id maps to a conflicting schema
├── NpsAnchorStaleException        — anchor_id is known but the schema has been updated
├── NpsStreamException             — stream sequence gap or abnormal termination
└── NpsVersionIncompatibleException — client min_version is higher than server supports
```

All exceptions expose `NpsStatusCode` (NPS status code) and `ProtocolErrorCode` (protocol-level error code).

**Example:**

```csharp
try
{
    var frame = codec.Decode(wire);
}
catch (NpsAnchorNotFoundException ex)
{
    // ex.AnchorId          — the missing anchor_id
    // ex.NpsStatusCode     — "NPS-CLIENT-NOT-FOUND"
    // ex.ProtocolErrorCode — "NCP-ANCHOR-NOT-FOUND"
    logger.LogWarning("Schema anchor missing: {Id}", ex.AnchorId);
}
catch (NpsFrameException ex)
{
    logger.LogError("Malformed frame: {Msg}", ex.Message);
}
```

---

## Status Code and Error Code Constants

### NpsStatusCodes

Namespace: `NPS.Core`

```csharp
NpsStatusCodes.Ok                  // "NPS-OK"
NpsStatusCodes.OkAccepted          // "NPS-OK-ACCEPTED"
NpsStatusCodes.ClientBadFrame      // "NPS-CLIENT-BAD-FRAME"
NpsStatusCodes.ClientNotFound      // "NPS-CLIENT-NOT-FOUND"
NpsStatusCodes.AuthUnauthenticated // "NPS-AUTH-UNAUTHENTICATED"
NpsStatusCodes.AuthForbidden       // "NPS-AUTH-FORBIDDEN"
NpsStatusCodes.LimitBudget         // "NPS-LIMIT-BUDGET"
NpsStatusCodes.ServerInternal      // "NPS-SERVER-INTERNAL"
// Full list: spec/status-codes.md
```

### NcpErrorCodes

Namespace: `NPS.Core`

```csharp
NcpErrorCodes.AnchorNotFound      // "NCP-ANCHOR-NOT-FOUND"
NcpErrorCodes.AnchorSchemaInvalid // "NCP-ANCHOR-SCHEMA-INVALID"
NcpErrorCodes.AnchorStale         // "NCP-ANCHOR-STALE"
NcpErrorCodes.StreamSeqGap        // "NCP-STREAM-SEQ-GAP"
NcpErrorCodes.EncodingUnsupported // "NCP-ENCODING-UNSUPPORTED"
NcpErrorCodes.VersionIncompatible // "NCP-VERSION-INCOMPATIBLE"
// Full list: spec/error-codes.md
```

---

## Related Documentation

- [Frame Model Concepts](../../concepts/frame-model.en.md)
- [NPS.NWP API Reference](./nps-nwp.en.md)
- [NCP Specification](../../../spec/NPS-1-NCP.md)
