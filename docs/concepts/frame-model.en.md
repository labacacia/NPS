English | [中文版](./frame-model.md)

# Frame Model

Every communication unit across all NPS protocol layers is called a **Frame**. Each frame consists of a fixed-format **header** and a **payload** serialized according to the selected encoding tier.

---

## Frame Namespace

All frame types are identified by a single-byte type code (`FrameType`), managed centrally in `spec/frame-registry.yaml`:

| Range | Protocol | Type Code | Frame Name |
|-------|----------|-----------|------------|
| 0x01–0x0F | NCP | `0x01` | AnchorFrame — schema anchor |
| | | `0x02` | DiffFrame — incremental patch |
| | | `0x03` | StreamFrame — streaming chunk |
| | | `0x04` | CapsFrame — full response capsule |
| | | `0x06` | HelloFrame — native-mode handshake |
| 0x10–0x1F | NWP | `0x10` | QueryFrame — structured query |
| | | `0x11` | ActionFrame — operation invocation |
| 0x20–0x2F | NIP | `0x20` | IdentFrame — identity declaration |
| | | `0x21` | TrustFrame — cross-CA trust chain |
| | | `0x22` | RevokeFrame — certificate revocation |
| 0x30–0x3F | NDP | `0x30` | AnnounceFrame — node presence broadcast |
| | | `0x31` | ResolveFrame — address resolution |
| | | `0x32` | GraphFrame — node graph synchronization |
| 0x40–0x4F | NOP | `0x40` | TaskFrame — task definition |
| | | `0x41` | DelegateFrame — sub-task delegation |
| | | `0x42` | SyncFrame — synchronization barrier |
| | | `0x43` | AlignStreamFrame — directed task stream |
| 0xF0–0xFF | System | `0xFE` | ErrorFrame — unified error frame |

---

## Encoding Tiers

The encoding format is specified in the lower 2 bits of the Flags byte (`EncodingTier`):

| Tier | Identifier | Flags bits 1–0 | Description |
|------|------------|----------------|-------------|
| Tier-1 | `Json` | `0x00` | UTF-8 JSON — human-readable; for development, debugging, and compatibility |
| Tier-2 | `MsgPack` | `0x01` | MessagePack binary — ~40% of JSON size; default for production |
| Tier-3 | `BinaryVector` v1 | `0x02` | Optional vector-heavy payload encoding; requires `binary_vector.v1` negotiation; metadata MessagePack + little-endian float32 segments |

Each frame type declares a preferred tier (`IFrame.PreferredTier`). A codec override is a policy hook for negotiated sessions, tests, and diagnostics; application code should not switch tiers arbitrarily inside one workflow.

---

## Frame Header Format

### Default Mode (4 bytes)

```
 0       1       2       3
 ┌───────┬───────┬───────────────┐
 │ Type  │ Flags │ PayloadLength │
 │ 1 B   │ 1 B   │ 2 B (BE)      │
 └───────┴───────┴───────────────┘
```

- **Type**: `FrameType` enum value (1 byte)
- **Flags**: flags byte (1 byte); see below
- **PayloadLength**: payload size in big-endian order (max 65 535 bytes / 64 KiB)

### Extended Mode (8 bytes, EXT=1)

When payload exceeds 64 KiB (and `EnableExtendedFrameHeader = true`), the header is automatically promoted to 8 bytes:

```
 0       1       2       3       4       5       6       7
 ┌───────┬───────┬───────────────────────────────────────┐
 │ Type  │ Flags │ PayloadLength (4 B, BE)                │
 │ 1 B   │ 1 B   │ max 4 294 967 295 bytes / 4 GiB        │
 └───────┴───────┴───────────────────────────────────────┘
```

Bit 7 of the Flags byte (`EXT`) is set to 1 to signal extended mode.

---

## Frame Flags

| Bit | Name | Value | Description |
|-----|------|-------|-------------|
| 1–0 | Encoding Tier | `0x00`/`0x01` | Lower 2 bits; encoding tier |
| 2 | FINAL | `0x04` | Set on the last frame of a stream; all non-stream frames MUST set this bit |
| 3 | ENC | `0x08` | Set when the payload is encrypted; MUST be set in production |
| 7 | EXT | `0x80` | Set when using the 8-byte extended frame header |

---

## Schema Anchoring

One of NPS's core optimizations is avoiding repeated schema transmission. The flow is:

1. **First request**: the server sends an `AnchorFrame` carrying the full `FrameSchema` and its `anchor_id` (the SHA-256 digest of the schema).
2. **Subsequent requests**: response frames (e.g. `CapsFrame`) carry only the `anchor_id` reference — not the schema itself.
3. **Client-side cache**: `AnchorFrameCache` stores schemas in memory, TTL defaulting to 3 600 seconds.
4. **Conflict detection**: if the same `anchor_id` maps to a different schema, `NpsAnchorPoisonException` is thrown.

### anchor_id Computation

```
anchor_id = "sha256:" + SHA256( RFC8785_JCS_Canonical_JSON(schema) )
```

Schema fields are sorted by name before serialization to ensure order-independence (NPS-1 §4.1).

### FrameSchema Field Types

| Type String | Description |
|-------------|-------------|
| `uint64` | Unsigned 64-bit integer |
| `int32` | Signed 32-bit integer |
| `string` | UTF-8 string |
| `decimal` | Exact decimal number |
| `bool` | Boolean |
| `timestamp` | ISO 8601 UTC timestamp |
| `bytes` | Byte sequence (Base64-encoded in transit) |

The optional `Semantic` annotation uses dot notation to convey meaning to AI models, e.g. `entity.id`, `commerce.price.usd`.

---

## Error Frame

All protocol layers share a single error frame type `ErrorFrame (0xFE)`:

```json
{
  "status": "NPS-CLIENT-NOT-FOUND",
  "error":  "NCP-ANCHOR-NOT-FOUND",
  "message": "anchor_id sha256:a3f9... not found in cache"
}
```

- `status`: NPS status code — see `spec/status-codes.md`
- `error`: protocol-level error code — see `spec/error-codes.md`

---

## Related Documentation

- [NCP Specification](../../spec/NPS-1-NCP.md)
- [Frame Registry](../../spec/frame-registry.yaml)
- [Error Code Namespace](../../spec/error-codes.md)
- [NPS.Core SDK Reference](../sdk/dotnet/nps-core.en.md)
