English | [中文版](./NPS-1-NCP.cn.md)

# NPS-1: Neural Communication Protocol (NCP)

**Spec Number**: NPS-1  
**Status**: Proposed  
**Version**: 0.13
**Date**: 2026-09-09
**Port**: 17433 (default, shared across the protocol suite)  
**Authors**: Ori Lynn / INNO LOTUS PTY LTD  

> This document is the NCP detailed specification. For the suite overview, see [NPS-0-Overview.md](NPS-0-Overview.md).

---

## 1. Terminology

The key words "MUST", "MUST NOT", "REQUIRED", "SHOULD", "SHOULD NOT", and "MAY" in this document are to be interpreted as described in RFC 2119.

---

## 2. Protocol Overview

NCP is the framing and communication base layer of NPS. It defines the frame structure, encoding tiers, and semantic-compression mechanism for AI-to-AI communication. All higher-layer protocols (NWP / NIP / NDP / NOP) are carried as NCP frames.

### 2.1 Roles

- **Sender**: the party that originates a frame (Agent or node).
- **Receiver**: the party that accepts and processes a frame.
- **Relay**: transparently forwards frames without modifying the payload (optional, for proxy scenarios).

### 2.2 Transport Modes

NCP supports two transport modes. The frame format is identical in both; only the carrier differs.

| Mode | Carrier | Use case | Recommended phase |
|------|---------|---------|-------------------|
| **HTTP mode** | NCP frames serialized in the HTTP body, paired with `X-NWP-*` request headers | Overlay deployment, compatible with existing Web infrastructure, firewall-friendly | Recommended for Phase 1 |
| **Native mode** | Direct TCP/QUIC connection; NCP frame is the on-the-wire format with no HTTP overhead | High-performance Agent-to-Agent, low-latency scenarios | Phase 2+ |

**HTTP mode**

```
POST /nwp/products/query HTTP/1.1
Host: api.example.com:17433
Content-Type: application/nwp-frame
X-NWP-Agent: urn:nps:agent:ca.innolotus.com:550e8400

[NCP Frame bytes / JSON]
```

**Native mode**

```
TCP connect → api.example.com:17433
→ HelloFrame (handshake, 0x06)
← CapsFrame (capability negotiation, 0x04)
→ QueryFrame (0x10)
← CapsFrame (response, 0x04)
```

The frame payload is identical in both modes. In native mode, errors are returned via ErrorFrame (0xFE); in HTTP mode, the HTTP status code is returned in parallel (mapping: [status-codes.md](./status-codes.md)).

### 2.3 Unified Port

The whole NPS suite shares **port 17433** by default. Frames from different protocols are routed by the Frame Type byte:

| Frame type range | Protocol |
|------------------|----------|
| 0x01–0x0F | NCP |
| 0x10–0x1F | NWP |
| 0x20–0x2F | NIP |
| 0x30–0x3F | NDP |
| 0x40–0x4F | NOP |
| 0xF0–0xFF | Reserved (including ErrorFrame 0xFE) |

Implementations MAY assign dedicated ports per protocol for isolated deployment, but default behavior MUST support single-port multiplexing.

### 2.4 Frame-Exchange Patterns

| Pattern | Description | Typical frame sequence |
|---------|-------------|------------------------|
| Request-response | Single query or operation | QueryFrame → CapsFrame |
| Streaming push | Large datasets or real-time data | QueryFrame → StreamFrame × N → StreamFrame(is_last=true) |
| Incremental subscription | Change notifications | subscribe → DiffFrame × N |
| Multi-agent sync | Task-state alignment | AlignStream × N (see NPS-5) |

### 2.5 Connection State Machine

```
┌──────────┐  connect()  ┌──────────────────┐  anchor_ok  ┌─────────────┐
│  CLOSED  │ ──────────→ │ ANCHOR_NEGOTIATE  │ ──────────→ │ ESTABLISHED │
└──────────┘             └──────────────────┘             └─────────────┘
                                │ timeout/error                  │ close()
                                ↓                                ↓
                          ┌──────────┐                   ┌──────────────┐
                          │  FAILED  │                   │   CLOSING    │
                          └──────────┘                   └──────────────┘
                                                                 │
                                                                 ↓
                                                           ┌──────────┐
                                                           │  CLOSED  │
                                                           └──────────┘
```

### 2.6 Connection Handshake Sequence (Native Mode)

In native mode, connection setup MUST follow the handshake below:

```
Client (Agent)                        Server (Node)
      │                                     │
      │── TCP/QUIC connect ──────────────→  │
      │── 8 bytes "NPS/1.0\n"  (preamble) → │  validate; close on mismatch (no ErrorFrame)
      │── HelloFrame (0x06) ─────────────→  │  declare client version & capabilities
      │                                     │  version negotiation & capability intersection
      │  ←──────────────── CapsFrame (0x04) │  return negotiated server capabilities
      │                                     │  (on incompatibility: ErrorFrame + disconnect)
      │  [optional] preload AnchorFrame     │
      │── GET /.schema ──────────────────→  │
      │  ←──────────────── AnchorFrame(s)   │
      │                                     │
      │  ── ESTABLISHED ───────────────── ESTABLISHED
```

**Version negotiation rules**

- Server MUST pick `min(client.nps_version, server.nps_version)` as the session version.
- If `client.min_version > server.nps_version`, Server MUST return `NCP-VERSION-INCOMPATIBLE` and close the connection.
- The negotiated encoding policy is selected from `client.supported_encodings` in priority order, constrained by the server's supported encodings. Servers SHOULD prefer the client's order when multiple encodings are supported.
- A session SHOULD use one stable default encoding policy: Tier-2 MsgPack for production, Tier-1 JSON for development/compatibility, or Tier-3 BinaryVector only as a negotiated specialization for vector-heavy frames. Per-frame tier variation is permitted only when it follows the negotiated policy (for example, HelloFrame in JSON before negotiation, ordinary frames in MsgPack, and NWP vector-search QueryFrame in BinaryVector). Implementations MUST NOT treat the header tier bits as permission for arbitrary application-level switching.
- `binary_vector.v1` MAY be enabled only when both peers support Tier-3 BinaryVector v1; enabling it does not make Tier-3 the session default.
- The negotiated `max_frame_payload` = `min(client.max_frame_payload, server.max_frame_payload)`.

**Identity and authorization binding**

- `HelloFrame` is a transport/session capability negotiation frame only. It MUST NOT carry bearer tokens, authorization scopes, or other credentials beyond the optional `agent_id` routing hint.
- In native mode, when the selected higher-layer protocol requires authentication, the client MUST present a NIP `IdentFrame` once per connection after the server's handshake `CapsFrame` and before the first authenticated NWP/NOP frame. The server binds the verified NID, capabilities, scope, and assurance level from that `IdentFrame` to the NCP session. When that Anchor subsequently transfers cluster ownership (multi-Anchor HA, [NPS-CR-0009](cr/NPS-CR-0009-multi-anchor-ha.md)), the server MAY close the native connection; on connection loss or an `NCP-NID-MISMATCH` after a resumption attempt, the client MUST re-resolve the cluster (NDP §9, highest `cluster_epoch`) or use the `successor_nid` from the NWP `anchor_failover` event (NPS-2 §12.2) and re-establish the session against the new active Anchor. Native-mode transport is normative per NPS-RFC-0006 (Accepted).
- NWP `QueryFrame`, `ActionFrame`, and `SubscribeFrame` do not contain per-message auth-token fields. Authorization for native-mode messages is evaluated against the session-bound identity plus any per-message target/scope data.
- HTTP/Overlay mode does not use `HelloFrame`; deployments that use bearer credentials present them in the HTTP transport envelope (for example `Authorization: Bearer ...`) alongside the NWP request headers. Bearer credentials are not embedded inside NWP frame payloads.

**Error handling**

If the handshake fails (incompatible version, empty capability set, etc.), Server MUST return:

```json
{
  "frame": "0xFE",
  "status": "NPS-PROTO-VERSION-INCOMPATIBLE",
  "error": "NCP-VERSION-INCOMPATIBLE",
  "message": "No compatible NPS version",
  "details": { "server_version": "0.4", "client_min_version": "0.5" }
}
```

### 2.6.1 Connection Preamble (Native Mode)

> **Status**: Mandatory (added in NCP v0.7 by [NPS-RFC-0001](rfcs/NPS-RFC-0001-ncp-connection-preamble.md)).

In native mode, every NCP connection MUST begin with an **8-byte constant preamble** sent by the client immediately after the transport (TCP / QUIC) handshake completes and before the first `HelloFrame`. HTTP mode is unaffected (the `Content-Type: application/nwp-frame` header serves the same identification role).

**Wire format**

```
Offset  0   1   2   3   4   5   6   7
       ┌───┬───┬───┬───┬───┬───┬───┬───┐
       │ N │ P │ S │ / │ 1 │ . │ 0 │\n │
       └───┴───┴───┴───┴───┴───┴───┴───┘
       0x4E 50  53  2F  31  2E  30  0A
```

Hex: `4E 50 53 2F 31 2E 30 0A`. The preamble is **not** a frame — it has no header, no Flags, no length field. Frame-type byte `0x4E` is reserved (see [frame-registry.yaml](frame-registry.yaml) `reservations:` block) and MUST NOT be assigned to any NCP frame.

**Client behavior**

- Client MUST write the 8 bytes in a single buffer (no interleaving with application logic; never split across writes — at 8 bytes, transport MTU constraints are never reached in practice).
- Client MAY pipeline `HelloFrame` back-to-back with the preamble (no round-trip wait), since server validation is byte-by-byte against a constant.

**Server behavior**

1. On accepting a native-mode connection, server reads exactly 8 bytes.
2. If the bytes match `b"NPS/1.0\n"`, server proceeds to frame parsing.
3. If they do not match:
   - Server MUST close the connection within 500 ms.
   - Server MUST NOT send an `ErrorFrame` — the peer is not known to speak NCP, and emitting framed bytes would leak protocol details to scanners.
   - Server MAY log the first 32 received bytes for operational diagnosis.
4. If fewer than 8 bytes arrive within 10 seconds, server treats it as a timeout and closes silently.

**Major-version semantics**

- `b"NPS/2.0\n"` and other future major-version preambles are reserved.
- A v1.x server reading `NPS/2.0\n` MUST close the connection (unknown major). It MAY write a single 32-byte diagnostic line `NPS-PREAMBLE-UNSUPPORTED-VERSION\n` before closing — permitted because the peer self-identified as an NPS speaker.
- The minor-version byte (the `0` in `1.0`) is reserved; v1 clients MUST send `0`, v1 servers MUST accept `0` only. A future RFC may define minor-version negotiation.

**Errors**

| Code | When |
|------|------|
| `NCP-FEATURE-NEGOTIATION-INVALID` | `NPS-CLIENT-BAD-FRAME` | Invalid feature negotiation metadata or selection |
| `NCP-FEATURE-REQUIRED-UNSUPPORTED` | `NPS-CLIENT-CONFLICT` | Required native feature has no eligible common revision |
| `NCP-PREAMBLE-INVALID` (`NPS-PROTO-PREAMBLE-INVALID`) | First 8 bytes do not match the constant; close silent within 500 ms. |
| (timeout) | Fewer than 8 bytes within 10 s; close silent. No code emitted on the wire. |

**Server state machine (native mode)**

```
[LISTEN] ──accept──→ [PREAMBLE-WAIT] ──8-byte match──→ [FRAMING] ──HelloFrame──→ [HANDSHAKE] ──CapsFrame──→ [ESTABLISHED]
                          │
                          ├──mismatch──→ [CLOSING] (≤ 500 ms)
                          └──10 s timeout──→ [CLOSING] (silent)
```

See [NPS-RFC-0001](rfcs/NPS-RFC-0001-ncp-connection-preamble.md) for full design rationale, alternatives considered, and SDK rollout phases.

### 2.6.2 Native Server Interoperability Profile (NCP v0.11)

This profile defines the minimum observable behavior of a portable native-mode
NCP server. It applies equally to TCP and QUIC stream implementations. The
language-neutral cases in
[`conformance/ncp/native_server_handshake_vectors.json`](conformance/ncp/native_server_handshake_vectors.json)
are normative.

**Transport and read boundaries**

1. When transport authentication is configured, the Server MUST complete the
   authentication or stream-wrapping hook before reading the NCP preamble.
   A deployment that requires an authenticated stream MUST fail locally when no
   hook is configured or the hook returns the original unauthenticated stream.
2. The Server MUST bound the preamble read. The default limit is 10 seconds.
   An invalid or incomplete preamble is closed silently as defined in §2.6.1.
3. After a valid preamble, the Server MUST bound the complete Hello read
   (header plus payload) separately. The default limit is 5 seconds.
4. The Server MUST inspect the frame header before allocating the Hello payload.
   `max_hello_payload` defaults to 65,535 bytes and MUST be configurable to a
   lower deployment limit.
5. The first frame MUST be an unencrypted Tier-1 JSON `HelloFrame` with
   `EXT=0`. A wrong frame type, reserved encoding tier, `ENC=1`, `EXT=1`,
   malformed header/payload, oversized payload, or Hello timeout causes a silent
   close. These pre-admission failures MUST NOT emit an `ErrorFrame`.
6. Once a valid Hello has been decoded, the Server MUST either send one
   handshake `CapsFrame` and establish the session, or send one `ErrorFrame` and
   close. It MUST NOT continue reading application frames while the connection
   remains in the handshake state.

Servers MUST expose bounded preamble and Hello timeouts. Deployments MAY lower
the defaults, but MUST NOT disable both limits on an untrusted listener.

**Deterministic negotiation**

The Server evaluates a decoded Hello against its configured profile:

- Both peers declare an inclusive `min_version`-`nps_version` range.
  An omitted `min_version` equals that peer's `nps_version`. Versions use the
  strict `major.minor` form. The session version is the highest version in the
  overlap. No overlap returns `NCP-VERSION-INCOMPATIBLE`.
- The stable default encoding is the first token in the client's ordered
  `supported_encodings` list that is also enabled by the Server and is either
  `msgpack` or `json`. No stable intersection returns
  `NCP-ENCODING-UNSUPPORTED`.
- `binary_vector.v1` is enabled only when both peers declare it. It is appended
  after the stable default in `enabled_encodings` and never becomes the default.
- `supported_protocols` is the intersection of both lists in client preference
  order. `ncp` MUST be present; otherwise the Server returns
  `NCP-VERSION-INCOMPATIBLE`.
- `max_frame_payload` and `max_concurrent_streams` are the minimum of the two
  peers' advertised limits. A zero limit is invalid.
- `ext_support` is enabled only when both peers advertise `true`.

The handshake `CapsFrame` payload is distinguished by handshake state and uses
the following portable fields. Existing fields remain additive and
backward-compatible:

| Field | Type | Required | Description |
|---|---|---:|---|
| `node_id` | string | yes | Server NID |
| `caps` | array[string] | yes | Server application capabilities |
| `session_version` | string | yes | Negotiated protocol version |
| `negotiated_encoding` | string | yes | Stable default: `msgpack` or `json` |
| `enabled_encodings` | array[string] | yes | Stable default followed by negotiated extensions |
| `supported_protocols` | array[string] | yes | Negotiated higher-layer protocol intersection |
| `max_frame_payload` | uint32 | yes | Negotiated payload ceiling |
| `ext_support` | bool | yes | Negotiated extended-header support |
| `max_concurrent_streams` | uint32 | yes | Negotiated concurrent-stream ceiling |
| `anchor_ref` | string | no | First offered schema reference |
| `payload` | object | no | Implementation-defined metadata |

This profile adds no frame type or error code.

---

### 2.6.3 Feature revision negotiation (NCP v0.13)

This optional alpha.20 profile reuses Hello and handshake Caps; it assigns no frame type.
Unchanged base behavior and existing compatibility floors remain available when unused.
The design record is [CR-0012](cr/NPS-CR-0012-feature-negotiation.md).

**NCP-FTR-01 — bounded types.** Hello MAY carry `feature_negotiation: 1`,
`feature_offer` and `required_features`. Offers without version 1 are invalid.
An offer is at most 32 objects of shape
`{"id":"example.echo","revisions":[{"revision":1,"lifecycle":"stable"}]}`.
IDs MUST match ASCII `^[a-z][a-z0-9]*([.][a-z][a-z0-9_-]*)+$` and be at most
96 bytes; `nps.` is reserved for published NPS profiles. Each ID has 1..16 ascending,
unique uint16 revisions (1..65535). Lifecycle is exactly experimental, stable or
deprecated, per revision. Required IDs are a unique array of at most 32 offered IDs.
Missing optional arrays mean empty. Unknown members in feature structures, duplicate
JSON keys/IDs/revisions, boolean/fractional revisions and unsupported versions MUST
be rejected with `NCP-FEATURE-NEGOTIATION-INVALID`.

JSON integer fields MUST use decimal-digit syntax without a fraction or exponent.

The [feature registry](feature-registry.json) owns reserved IDs/revisions and their
dependencies. Required dependency revisions must be offered and selected explicitly;
no implementation may add an unoffered revision. A selected dependency conflict is
NCP-FEATURE-NEGOTIATION-INVALID. Registry presence is not proof of SDK support.

**NCP-FTR-02 — payload bound.** A feature-bearing Hello or handshake Caps MUST have
a complete encoded payload of at most 16384 bytes, also subject to the existing
frame/admission limit (the smaller wins). This includes base fields, not just feature
values, so receivers never need a reserialization-dependent byte count. Existing
frame-size errors may take precedence before feature parsing. Base-only handshakes
retain existing limits. The standalone JSON block corpus tests the parser boundary;
full frame fixtures must additionally account for base fields.

**NCP-FTR-03 — selection.** A version-1 server MUST validate before selecting the
highest common locally eligible revision per shared ID. Unknown optional IDs are
omitted. Experimental revisions require explicit opt-in on both peers; deprecated
revisions require local policy permission. A remote lifecycle label MUST NOT relax
local policy. Required IDs absent from the offer are invalid; required IDs without
an eligible common revision fail with `NCP-FEATURE-REQUIRED-UNSUPPORTED`.
Cross-feature constraints MUST be checked on the selected combination; reject an
invalid combination instead of silently changing deterministic selection.

**NCP-FTR-04 — acknowledgment.** Handshake Caps MUST return
`feature_negotiation: 1` and `selected_features`, an explicit array (possibly empty)
of at most 32 `{"id":"example.echo","revision":1}` objects. Clients MUST reject
duplicate, unoffered, policy-ineligible or invalid selections before application
writes. Missing required IDs fail with `NCP-FEATURE-REQUIRED-UNSUPPORTED`.
An old Caps without acknowledgment means an empty selection: close without dispatch
if anything is required, otherwise base-only behavior MAY continue.

**NCP-FTR-05 — lifetime and authority.** Selection belongs only to the admitted
session. Reconnect/failover MUST repeat selection and identity admission. Application
Caps MUST NOT renegotiate an active session. Withdrawal requires stop-admission,
drain/close and fresh negotiation. Each operation MUST verify its exact required
revisions before writing. Feature support grants no subject, capabilities, scope or
assurance. A rejected extension handshake MAY fall back only on a fresh connection,
when local policy permits and no required feature or application request is carried
forward. A dispatched operation MUST NOT be replayed as a handshake fallback.

---

## 3. Frame Format

### 3.1 Fixed Header (4 bytes, default) / Extended Header (8 bytes, large-frame mode)

**Default frame header (4 bytes)**

```
 Byte 0          Byte 1          Byte 2–3
┌───────────────┬───────────────┬───────────────────────────┐
│  Frame Type   │     Flags     │      Payload Length       │
│   (1 byte)    │   (1 byte)    │        (2 bytes, BE)      │
└───────────────┴───────────────┴───────────────────────────┘
```

**Extended frame header (8 bytes, EXT=1)**

```
 Byte 0          Byte 1          Byte 2–5               Byte 6–7
┌───────────────┬───────────────┬───────────────────────┬──────────────┐
│  Frame Type   │     Flags     │   Payload Length      │   Reserved   │
│   (1 byte)    │   (1 byte)    │   (4 bytes, BE)       │  (2 bytes)   │
└───────────────┴───────────────┴───────────────────────┴──────────────┘
```

- **Frame Type**: frame-type byte, see the frame namespace (§2.3).
- **Flags**: control bits, see §3.2.
- **Payload Length**: 2 bytes by default (max 65,535); 4 bytes in extended mode (max 4,294,967,295).
- **Reserved**: 2 bytes (Byte 6–7, extended header only); senders MUST set to 0, receivers MUST ignore. The extended header is therefore 8 bytes: 1 (type) + 1 (flags) + 4 (length) + 2 (reserved).

### 3.2 Flags Field (Bit-level Definition)

```
Bit 7   Bit 6   Bit 5   Bit 4   Bit 3   Bit 2   Bit 1   Bit 0
┌───────┬───────┬───────┬───────┬───────┬───────┬───────┬───────┐
│  EXT  │  RSV  │  RSV  │  RSV  │  ENC  │ FINAL │  T1   │  T0  │
└───────┴───────┴───────┴───────┴───────┴───────┴───────┴───────┘
```

| Bit | Name | Description |
|-----|------|-------------|
| 0–1 | T0, T1 | Encoding tier: `00`=Tier-1 JSON, `01`=Tier-2 MsgPack, `10`=Tier-3 BinaryVector v1, `11`=Reserved |
| 2 | FINAL | Final-chunk flag for streaming frames (StreamFrame only; fixed to 1 elsewhere) |
| 3 | ENC | Payload uses application-layer E2E encryption (see §7.4); TLS is independent, MAY be 0 in dev mode |
| 4–6 | RSV | Reserved; senders MUST set to 0, receivers MUST ignore |
| 7 | EXT | Extended-header flag: `0`=default 4-byte header (payload ≤ 64 KB), `1`=extended 8-byte header (payload ≤ 4 GB) |

### 3.3 Frame-Size Configuration

| Parameter | Default | Max | Description |
|-----------|---------|-----|-------------|
| `max_frame_payload` | 65,535 (64 KB) | 4,294,967,295 (4 GB) | Maximum payload bytes per frame |

- Default header (EXT=0): payload up to **65,535 bytes**, sufficient for most scenarios.
- Extended header (EXT=1): payload up to **4,294,967,295 bytes**, for large embeddings or bulk document transfer.
- Implementations MUST support the default mode; SHOULD support extended mode.
- When the payload exceeds the negotiated `max_frame_payload`, the sender MUST chunk via StreamFrame.
- `max_frame_payload` is negotiated via CapsFrame at connection setup.

**Chunking rules**

- StreamFrame chunking: `seq` starts at 0 and increments; last chunk has FINAL=1.
- Each chunk's frame MUST respect the connection's negotiated `max_frame_payload`.

---

## 4. Frame Types

### 4.1 AnchorFrame (0x01)

Schema-anchor frame, **published by a Node**, used to establish a global Schema reference and eliminate redundant transmission.

**Fields**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `frame` | uint8 | required | Fixed value `0x01` |
| `anchor_id` | string | required | SHA-256 digest of the Schema, formatted `sha256:{64 hex chars}` |
| `schema` | object | required | Schema definition object |
| `schema.fields` | array | required | Field descriptors (see below) |
| `ttl` | uint32 | optional | Cache validity in seconds; default 3600; `0` = do not cache |
| `supersedes` | string | optional | `anchor_id` of the prior AnchorFrame this one replaces. Producers MAY set; consumers SHOULD prefer the newer frame when both are cached (see §5.5) |
| `valid_from` | string | optional | ISO-8601 UTC timestamp marking the start of the temporal validity window (e.g. `2026-05-10T00:00:00Z`). Anchor MUST be treated as invalid before this instant (see §5.5) |
| `valid_until` | string | optional | ISO-8601 UTC timestamp marking the end of the temporal validity window. Anchor MUST be treated as invalid after this instant (see §5.5) |

**schema.fields element**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | required | Field name |
| `type` | string | required | Data type: `string` / `uint64` / `int64` / `decimal` / `bool` / `timestamp` / `bytes` / `object` / `array` |
| `semantic` | string | optional | Semantic annotation, format `{domain}.{concept}` (see semantic type system) |
| `nullable` | bool | optional | Whether null is permitted; default false |

**anchor_id computation rules**

1. Serialize `schema` to canonical JSON per **RFC 8785 (JSON Canonicalization Scheme, JCS)**.
   - JCS preserves Unicode string data without normalization, sorts object keys by UTF-16, and emits no extra whitespace ([RFC 8785 §3.1](https://www.rfc-editor.org/rfc/rfc8785.html#section-3.1)).
   - Implementations MUST use a standard JCS library (do not implement your own) to keep .NET / Python / TS SDKs consistent.
2. Compute the SHA-256 digest of the JCS output as UTF-8 bytes.
3. Format as `sha256:{lowercase hex}`.

> **Note**: For JCS reference implementations, see https://www.rfc-editor.org/rfc/rfc8785#appendix-A

**Example**

```json
{
  "frame": "0x01",
  "anchor_id": "sha256:a3f9b2c1d4e5f6789012345678901234567890abcdef1234567890abcdef12",
  "schema": {
    "fields": [
      { "name": "id",    "type": "uint64",  "semantic": "entity.id" },
      { "name": "name",  "type": "string",  "semantic": "entity.label" },
      { "name": "price", "type": "decimal", "semantic": "commerce.price.usd" },
      { "name": "stock", "type": "uint64",  "semantic": "commerce.inventory.count" }
    ]
  },
  "ttl": 3600
}
```

**Semantic type system (partial)**

| Semantic | Meaning |
|----------|---------|
| `entity.id` | Unique entity identifier |
| `entity.label` | Human-readable entity name |
| `entity.description` | Entity description text |
| `entity.timestamp.created` | Creation time |
| `entity.timestamp.updated` | Last-updated time |
| `commerce.price.usd` | USD price |
| `commerce.price.cny` | CNY price |
| `commerce.inventory.count` | Inventory count |
| `geo.latitude` | Latitude |
| `geo.longitude` | Longitude |

---

### 4.2 DiffFrame (0x02)

Incremental-data frame carrying only a patch of changed fields, for subscription or polling scenarios.

**Fields**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `frame` | uint8 | required | Fixed value `0x02` |
| `anchor_ref` | string | required | anchor_id of the base Schema |
| `base_seq` | uint64 | required | Version sequence this diff is based on |
| `patch_format` | string | optional | Patch format: `json_patch` (default) or `binary_bitset` (recommended for Tier-2) |
| `patch` | array \| bytes | required | Patch data (format decided by `patch_format`, see below) |
| `entity_id` | string | optional | ID of the entity being updated |

**patch_format: json_patch** (default, for Tier-1 JSON)

`patch` is an array of JSON Patch operations (RFC 6902); each element has `op`, `path`, and `value`.

**patch_format: binary_bitset** (Tier-2 MsgPack, ~15–20% smaller)

`patch` is a binary byte stream (MsgPack `bin` type) structured as:

```
┌──────────────────────────────────────────────────────┐
│  Changed Fields Bitset (ceil(N/8) bytes, N=field count)│
│  Bit i=1 indicates the i-th field changed (schema.fields order)│
├──────────────────────────────────────────────────────┤
│  Values (new values for changed fields only, in field order, MsgPack-encoded)│
└──────────────────────────────────────────────────────┘
```

Rules:
- Senders MUST only use `binary_bitset` inside Tier-2 (MsgPack) frames.
- Receivers that do not support `binary_bitset` MUST return `NCP-DIFF-FORMAT-UNSUPPORTED`.
- When `patch_format` is omitted, default is `json_patch`.

**Example (json_patch)**

```json
{
  "frame": "0x02",
  "anchor_ref": "sha256:a3f9b2c1...",
  "base_seq": 42,
  "patch_format": "json_patch",
  "entity_id": "product:1001",
  "patch": [
    { "op": "replace", "path": "/price", "value": 299.00 },
    { "op": "replace", "path": "/stock", "value": 48 }
  ]
}
```

---

### 4.3 StreamFrame (0x03)

Streaming data-chunk frame, used for large datasets, real-time push, or fragmentation of oversized payloads.

**Fields**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `frame` | uint8 | required | Fixed value `0x03` |
| `stream_id` | string | required | Unique stream identifier (UUID v4) |
| `seq` | uint32 | required | Chunk sequence, starts at 0 and increments; receivers reassemble by `seq` |
| `is_last` | bool | required | `true` marks the final chunk |
| `anchor_ref` | string | optional | Schema anchor_id (carried on the first chunk; may be omitted later) |
| `data` | array | required | Chunk data, conforming to the referenced Schema |
| `window_size` | uint32 | optional | Remaining frames the receiver can buffer (see flow-control semantics) |
| `error_code` | string | optional | Present when the stream aborts abnormally; `is_last` is forced to true |

**Flow-control semantics**

NCP provides application-layer semantic back-pressure, complementing TCP/QUIC transport-layer flow control:

- **Initial window**: the sender carries `window_size` in the first frame (seq=0), declaring how many frames the receiver can still accept.
- **Window consumption**: the receiver's logical window decrements by 1 per received frame.
- **Window update**: when the receiver is ready for more data, it sends a reverse StreamFrame (`data=[]`, `is_last=false`) carrying a new `window_size` to refresh the window.
- **Back-pressure pause**: if the receiver sends `window_size=0`, the sender MUST pause until it receives a non-zero `window_size`.
- **Omission semantics**: absence of `window_size` means application-layer flow control is not enabled (rely on the transport).
- If the sender continues after the window is exhausted, the receiver SHOULD return `NCP-STREAM-WINDOW-OVERFLOW` and MAY terminate the stream.

---

### 4.4 CapsFrame (0x04)

Encapsulates a full response body; the most common response frame. Also used for capability negotiation at connection setup.

**Fields**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `frame` | uint8 | required | Fixed value `0x04` |
| `anchor_ref` | string | required | Schema anchor_id |
| `count` | uint32 | required | Length of `data`; MUST equal `len(data)` |
| `data` | array | required | Data records; each conforms to the anchor_ref Schema |
| `next_cursor` | string | optional | Next-page cursor, Base64-URL encoded; null = last page |
| `token_est` | uint32 | optional | Estimated CGN consumption for this response (see [token-budget.md](./token-budget.md)) |
| `tokenizer_used` | string | optional | Identifier of the tokenizer actually applied |
| `cached` | bool | optional | `true` = response came from server-side cache |
| `request_id` | string | optional | Correlates a unary response with the originating request. For synchronous NWP operations, the producer SHOULD copy the request frame's `request_id` |
| `inline_anchor` | object | optional | Latest AnchorFrame included inline when the Schema has been updated, avoiding an extra RTT (see §5.4) |

`cached` describes caching of the complete NWP response. It MUST NOT be interpreted
as a model prefix/KV-cache hit. Protocol-specific payloads MAY expose a distinct
cache-usage object when the underlying runtime can report one.

**Connection-negotiation CapsFrame**

Native handshake uses the dedicated top-level payload of §2.6.2, identified by
binary-header type 0x04, not the application anchor_ref/count/data schema below.
Feature negotiation fields MUST NOT be nested under data. This example negotiates
the extension; base-only handshakes omit the feature fields.

```json
{
  "node_id": "urn:nps:agent:example.test:server",
  "caps": [
    "ncp"
  ],
  "session_version": "0.13",
  "negotiated_encoding": "json",
  "enabled_encodings": [
    "json"
  ],
  "supported_protocols": [
    "ncp"
  ],
  "max_frame_payload": 65535,
  "ext_support": false,
  "max_concurrent_streams": 32,
  "feature_negotiation": 1,
  "selected_features": []
}
```

**Data-response example**

```json
{
  "frame": "0x04",
  "anchor_ref": "sha256:a3f9b2c1...",
  "count": 2,
  "data": [
    { "id": 1001, "name": "iPhone 15 Pro", "price": 999.00, "stock": 42 },
    { "id": 1002, "name": "MacBook Air M3", "price": 1299.00, "stock": 15 }
  ],
  "next_cursor": "eyJpZCI6MTAwM30",
  "token_est": 180
}
```

---

### 4.5 AlignFrame (0x05) — Deprecated

> ⚠️ AlignFrame was marked **Deprecated** in NCP v0.7.
> Use NOP AlignStream (0x43) instead, see [NPS-5-NOP.md](NPS-5-NOP.md).
> AlignFrame will be removed in NPS v1.0.

---

### 4.6 HelloFrame (0x06)

Client-side handshake frame in native mode, **sent by the Agent (client) first after the connection is established**, declaring the client's protocol version and capabilities. The Server responds with a CapsFrame (see §2.6).

> **Naming note**: The NIP layer defines IdentFrame (0x20) for identity-certificate exchange. NCP HelloFrame is only for version and capability negotiation and does not carry identity; the responsibilities are cleanly separated.

**Fields**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `frame` | uint8 | required | Fixed value `0x06` |
| `nps_version` | string | required | Highest NPS version the client supports; format `"major.minor"` |
| `min_version` | string | optional | Lowest NPS version the client supports; defaults to `nps_version` |
| `supported_encodings` | array[string] | required | Supported encodings, e.g. `["binary_vector.v1", "msgpack", "json"]`, in descending priority |
| `supported_protocols` | array[string] | required | Supported higher-layer protocols, e.g. `["ncp", "nwp", "nip"]` |
| `agent_id` | string | optional | Agent NID (NIP identity identifier), format `urn:nps:agent:{domain}:{id}` |
| `max_frame_payload` | uint32 | optional | Maximum payload bytes the client can accept; default 65535 |
| `ext_support` | bool | optional | Whether extended header (EXT=1) is supported; default false |
| `max_concurrent_streams` | uint32 | optional | Maximum concurrent streams the client can handle; default 32 |
| `e2e_enc_algorithms` | array[string] | optional | E2E encryption algorithms the client supports (see §7.4), e.g. `["aes-256-gcm"]` |
| `ping_interval_ms` | uint32 | optional | Preferred keepalive interval in milliseconds; 0 = disabled (default). When non-zero, both peers SHOULD send a `NopFrame` at this interval and MUST send one within `3 × ping_interval_ms` (NCP v0.8 §7.6). |

**Rules**

- HelloFrame MUST be the first frame the client sends in native mode; nothing else may precede it.
- HTTP mode does not use HelloFrame (capabilities flow via `X-NWP-*` headers).
- HelloFrame is unencrypted (ENC=0) and SHOULD use Tier-1 JSON (the encoding has not yet been negotiated).
- The Server MUST reply with a CapsFrame or ErrorFrame within 5 seconds of receiving HelloFrame; otherwise the client SHOULD disconnect.

**HelloFrame example**

```json
{
  "frame": "0x06",
  "nps_version": "0.4",
  "min_version": "0.3",
  "supported_encodings": ["binary_vector.v1", "msgpack", "json"],
  "supported_protocols": ["ncp", "nwp"],
  "agent_id": "urn:nps:agent:ca.innolotus.com:550e8400",
  "max_frame_payload": 65535,
  "ext_support": false,
  "max_concurrent_streams": 16,
  "e2e_enc_algorithms": ["aes-256-gcm", "chacha20-poly1305"]
}
```

---

### 4.7 ErrorFrame (0xFE)

The unified NPS error frame, shared across all protocol layers. Carries error responses in native mode.

**Fields**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `frame` | uint8 | required | Fixed value `0xFE` |
| `status` | string | required | NPS status code (see [status-codes.md](./status-codes.md)) |
| `error` | string | required | Protocol-level error code (e.g. `NCP-ANCHOR-NOT-FOUND`) |
| `message` | string | optional | Human-readable error description |
| `details` | object | optional | Structured error context (e.g. `anchor_ref`, `stream_id`) |

**Example**

```json
{
  "frame": "0xFE",
  "status": "NPS-CLIENT-NOT-FOUND",
  "error": "NCP-ANCHOR-NOT-FOUND",
  "message": "Schema anchor not found in cache, please resend AnchorFrame",
  "details": { "anchor_ref": "sha256:a3f9b2c1..." }
}
```

---

### 4.8 NopFrame (0x07) — Keepalive / Heartbeat

Null frame with no payload. Used as a keepalive probe on idle connections to prevent NAT/firewall timeouts and to detect dead peers (NCP v0.8).

**Fields**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `frame` | uint8 | required | Fixed value `0x07` |

**Rules**

- NopFrame MAY be sent at any time on an established native-mode connection (never during handshake).
- Receivers MUST accept NopFrame and MUST NOT return an ErrorFrame in response; they SHOULD reply with another NopFrame.
- When `ping_interval_ms > 0` is declared in HelloFrame, both peers SHOULD send a NopFrame at the declared interval. If no frame (of any type) has been received within `3 × ping_interval_ms`, the peer SHOULD send `NCP-KEEPALIVE-TIMEOUT` and close the connection.
- NopFrame MUST NOT carry any payload; a NopFrame with non-zero payload length MUST be rejected with `NCP-FRAME-PAYLOAD-TOO-LARGE` (treat as 0-byte limit violation).

---

## 5. Schema Anchoring Mechanism

### 5.1 Schema Ownership

AnchorFrame is published by the **Node (the data-model owner)**. The Agent fetches the AnchorFrame via the node manifest or a Schema endpoint, caches it locally, and references it in subsequent requests.

### 5.2 Standard Flow

```
Agent                              Node
  │                                  │
  │── GET /.nwm ─────────────────→│  read manifest, fetch schema_anchors
  │←── NWM(schema_anchors) ───────│
  │                                  │
  │── GET /.schema ──────────────→│  fetch full AnchorFrame (optional, on-demand)
  │←── AnchorFrame(schema) ───────│  Agent caches anchor_id → schema locally
  │                                  │
  │── QueryFrame(anchor_ref) ────→│  request carries only anchor_ref
  │←── CapsFrame(anchor_ref) ─────│  response also only references anchor_ref
  │                                  │
  │── QueryFrame(anchor_ref) ────→│  no Schema retransmission on later requests
  │←── CapsFrame(anchor_ref) ─────│
```

### 5.3 Cache Semantics

- The Agent MUST cache AnchorFrames fetched from a Node, keyed by `anchor_id`.
- The cache validity equals the AnchorFrame's `ttl` in seconds.
- The Agent SHOULD preload all AnchorFrames likely to be used after the connection is established.

### 5.4 Cache-Miss Handling

#### 5.4.1 Schema Updated (Stale anchor_ref)

If the Agent references an outdated `anchor_ref` and the Node has a newer version, behavior depends on the request's `auto_anchor` flag (defined in NWP QueryFrame):

- **auto_anchor=true (default)**: the Node SHOULD attach an `inline_anchor` field in the CapsFrame response containing the latest AnchorFrame, avoiding an extra RTT:

```json
{
  "frame": "0x04",
  "anchor_ref": "sha256:new_anchor...",
  "count": 2,
  "data": [ ... ],
  "inline_anchor": {
    "frame": "0x01",
    "anchor_id": "sha256:new_anchor...",
    "schema": { "fields": [ ... ] },
    "ttl": 3600
  }
}
```

  Upon receiving a response with `inline_anchor`, the Agent MUST update its local cache (replacing the Schema for the old `anchor_id`).

- **auto_anchor=false**: the Node SHOULD return `NCP-ANCHOR-STALE` with the old anchor_id, letting the Agent proactively fetch the new AnchorFrame.

#### 5.4.2 Unknown anchor_ref

If the Agent references an `anchor_ref` the Node does not recognize (hand-crafted, cross-node misreference, etc.), the Node MUST return:

```json
{
  "frame": "0xFE",
  "status": "NPS-CLIENT-NOT-FOUND",
  "error": "NCP-ANCHOR-NOT-FOUND",
  "details": { "anchor_ref": "sha256:..." }
}
```

> **Note**: `NCP-ANCHOR-STALE` means the anchor exists but has been updated; `NCP-ANCHOR-NOT-FOUND` means the anchor was never registered at this Node.

### 5.5 Schema Evolution

Schemas evolve over time. NCP defines a compatibility-class taxonomy and the producer/consumer obligations that govern lifecycle of an AnchorFrame across versions. The optional `supersedes`, `valid_from`, and `valid_until` fields (see §4.1) carry the lineage and temporal-validity metadata used by this section.

#### 5.5.1 Compatibility Classes

Every Schema change MUST be classified into one of the following compatibility classes. Producers SHOULD record the class in their release notes; consumers MAY use the class to decide whether re-fetching the anchor is mandatory.

| Class | Description | Backward-compatible |
|-------|-------------|---------------------|
| `additive_optional` | Adds an optional field (i.e. `nullable=true` or with a documented default) to `schema.fields` | yes |
| `additive_required` | Adds a required field (no default) to `schema.fields` | no |
| `type_widening` | Broadens an existing field's type without losing previously valid values (e.g. `uint32` → `uint64`, `string` enum → open `string`) | yes |
| `type_narrowing` | Restricts an existing field's type or value domain (e.g. `string` → enum, `int64` → `uint32`) | no |
| `semantic_change` | Identical wire shape but the meaning of an existing field changes (e.g. unit, currency, or referent) | case-by-case |
| `field_remove` | Removes an existing field from `schema.fields` | no |

Any change classified as `additive_required`, `type_narrowing`, or `field_remove` is a **breaking change** and MUST result in a new `anchor_id`. Producers MUST set `supersedes` on the new AnchorFrame to the old `anchor_id` so consumers can trace the lineage.

For `semantic_change`, producers MUST treat the change as breaking unless they can demonstrate via out-of-band documentation that no consumer relies on the prior meaning.

#### 5.5.2 Temporal Validity

When `valid_from` and/or `valid_until` are set, an AnchorFrame is only valid within the half-open window `[valid_from, valid_until)`. Outside this window:

- Consumers MUST NOT treat data referencing this anchor as fresh.
- Consumers SHOULD evict the anchor from cache once `valid_until` has elapsed, regardless of remaining `ttl`.
- Producers MUST return `NCP-ANCHOR-NOT-FOUND` for any `anchor_ref` whose anchor has expired and been evicted.

If `valid_until` is set, producers **MUST** include an `inline_anchor` (carrying the successor AnchorFrame) on every CapsFrame whose response time falls within 60 seconds of expiry (`now ≥ valid_until − 60s`). This bounds the worst-case window in which a consumer is still using a soon-to-expire anchor and guarantees the successor reaches the consumer before the current anchor becomes invalid.

#### 5.5.3 Producer / Consumer Obligations

- **Producers MUST NOT** introduce a breaking change (`additive_required`, `type_narrowing`, `field_remove`, or unmitigated `semantic_change`) within the `ttl` window of the prior AnchorFrame. The successor's `valid_from` MUST be ≥ the prior anchor's `valid_until` (if set), or ≥ `prior_publish_time + prior.ttl` otherwise. This guarantees consumers caching the prior anchor cannot observe a breaking transition mid-window.
- **Producers MUST** set `supersedes` on every AnchorFrame that replaces a prior version, regardless of compatibility class.
- **Consumers MUST** tolerate `additive_optional` changes — i.e. unknown extra fields in payloads referencing a known anchor MUST NOT cause the consumer to fail, even if the consumer has not yet refreshed the anchor.
- **Consumers SHOULD** prefer the newer AnchorFrame when both an anchor and its successor (linked via `supersedes`) are present in cache, and SHOULD eagerly evict the superseded entry.
- **Consumers MUST** validate the temporal window on every reference: if `valid_until` has elapsed, the consumer MUST refresh before issuing a new request that references the anchor.

---

## 6. Status Codes and Error Codes

NPS uses a two-tier error model:

1. **NPS status codes**: transport-level status categories, see [status-codes.md](./status-codes.md).
2. **Protocol error codes**: specific error identifiers, prefixed by the owning protocol.

### NCP Error Codes

| Error code | NPS status | Description |
|------------|------------|-------------|
| `NCP-ANCHOR-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | Referenced Schema not found |
| `NCP-ANCHOR-SCHEMA-INVALID` | `NPS-CLIENT-BAD-FRAME` | Schema in AnchorFrame is malformed |
| `NCP-ANCHOR-ID-MISMATCH` | `NPS-CLIENT-CONFLICT` | Same anchor_id received with different Schema (anchor-poisoning defense) |
| `NCP-FRAME-UNKNOWN-TYPE` | `NPS-CLIENT-BAD-FRAME` | Unknown frame-type byte |
| `NCP-FRAME-PAYLOAD-TOO-LARGE` | `NPS-LIMIT-PAYLOAD` | Payload exceeds negotiated max_frame_payload |
| `NCP-FRAME-FLAGS-INVALID` | `NPS-CLIENT-BAD-FRAME` | Reserved bits in Flags are non-zero |
| `NCP-BINARY-VECTOR-MALFORMED` | `NPS-CLIENT-BAD-FRAME` | Tier-3 BinaryVector payload is malformed |
| `NCP-BINARY-VECTOR-DIM-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | BinaryVector marker dimension does not match the vector segment |
| `NCP-BINARY-VECTOR-INDEX-INVALID` | `NPS-CLIENT-BAD-FRAME` | BinaryVector marker references a missing vector segment |
| `NCP-BINARY-VECTOR-DTYPE-UNSUPPORTED` | `NPS-CLIENT-BAD-FRAME` | BinaryVector marker uses an unsupported dtype |
| `NCP-BINARY-VECTOR-TRUNCATED` | `NPS-CLIENT-BAD-FRAME` | BinaryVector vector segment is truncated |
| `NCP-STREAM-SEQ-GAP` | `NPS-STREAM-SEQ-GAP` | Non-contiguous StreamFrame sequence |
| `NCP-STREAM-NOT-FOUND` | `NPS-STREAM-NOT-FOUND` | stream_id does not refer to an existing stream |
| `NCP-STREAM-LIMIT-EXCEEDED` | `NPS-STREAM-LIMIT` | Max concurrent streams per connection exceeded |
| `NCP-ENCODING-UNSUPPORTED` | `NPS-SERVER-ENCODING-UNSUPPORTED` | Requested encoding tier is not supported |
| `NCP-ANCHOR-STALE` | `NPS-CLIENT-CONFLICT` | anchor_ref exists but the Schema has been updated (paired with inline_anchor) |
| `NCP-DIFF-FORMAT-UNSUPPORTED` | `NPS-CLIENT-BAD-FRAME` | DiffFrame used a patch_format the receiver does not support |
| `NCP-VERSION-INCOMPATIBLE` | `NPS-PROTO-VERSION-INCOMPATIBLE` | Client min_version exceeds server's supported version |
| `NCP-STREAM-WINDOW-OVERFLOW` | `NPS-STREAM-LIMIT` | Sender continued after the flow-control window was exhausted |
| `NCP-PREAMBLE-INVALID` | `NPS-PROTO-PREAMBLE-INVALID` | Native-mode connection opened with bytes other than `b"NPS/1.0\n"`; server closes silently within 500 ms (no ErrorFrame) — see §2.6.1 |
| `NCP-REKEY-REQUIRED` | `NPS-PROTO-VERSION-INCOMPATIBLE` | E2E-encrypted channel has reached the rekey threshold (2^32 frames or 24 h); peer MUST initiate key rotation before sending more encrypted frames |
| `NCP-KEEPALIVE-TIMEOUT` | `NPS-SERVER-TIMEOUT` | No frame (including NopFrame) received within 3 × `ping_interval_ms`; connection will be closed |
| `NCP-EARLY-DATA-REJECTED` | `NPS-PROTO-VERSION-INCOMPATIBLE` | An NPS frame was carried in QUIC/TLS 0-RTT early data; NPS application data starts only after handshake confirmation |

HTTP-mode status mapping: see [status-codes.md](./status-codes.md).

---

## 7. Security Considerations

### 7.1 Replay-Attack Defense
CapsFrame and QueryFrame should be protected at the TLS layer. In non-TLS scenarios (dev mode), the Agent SHOULD include a `nonce` field.

### 7.2 Anchor Poisoning
Schemas are published by the Node and referenced read-only by the Agent. Nodes MUST enforce idempotency on anchor_id — the same anchor_id MUST always correspond to the same Schema. If the Agent observes a differing Schema under the same anchor_id, it SHOULD disconnect and report a security event.

### 7.3 Stream Flooding
Nodes SHOULD limit max concurrent streams per connection (recommended default: 32, negotiated via CapsFrame `max_concurrent_streams`). On overflow, return `NCP-STREAM-LIMIT-EXCEEDED`.

### 7.4 E2E Encryption (Formal Definition of the ENC Flag)

> **Rekeying** (NCP v0.7): When ENC=1 is in use, the sender MUST trigger rekeying before any of these thresholds are reached: (a) 2^32 frames sent on the current key, (b) 24 hours elapsed since the key was established. The triggering peer sends an ErrorFrame with `error: "NCP-REKEY-REQUIRED"` as a graceful signal; the peer MUST acknowledge by completing the key rotation before sending further encrypted frames. Key rotation follows the same ECDH + HKDF algorithm as the initial key derivation; implementations SHOULD use X25519.

When frame-header flag **ENC=1**, the frame payload uses application-layer end-to-end encryption. E2E encryption is independent of TLS and applies where a Relay across multiple hops is untrusted.

**Algorithm selection**

Negotiated via the `e2e_enc_algorithms` field in the handshake CapsFrame, priority descending:

| Algorithm | Identifier | Key length | Nonce | Tag |
|-----------|-----------|------------|-------|-----|
| AES-256-GCM | `aes-256-gcm` | 256-bit | 12 bytes | 16 bytes |
| ChaCha20-Poly1305 | `chacha20-poly1305` | 256-bit | 12 bytes | 16 bytes |

**Payload layout when ENC=1**

```
┌────────────────┬──────────────────────────────────────┬─────────────┐
│   Nonce        │   Encrypted Payload                  │  Auth Tag   │
│  (12 bytes)    │   (ciphertext of original payload)   │  (16 bytes) │
└────────────────┴──────────────────────────────────────┴─────────────┘
```

- `Payload Length` (in the header) = 12 + len(encrypted) + 16
- The nonce MUST be unique per frame; random generation via a CSPRNG is recommended.
- AAD (Additional Authenticated Data) = the frame header (4 or 8 bytes).
- Key management: E2E keys are distributed through NIP (Neural Identity Protocol) key-exchange and are not defined at the NCP layer.

**Implementation requirements**

- If HelloFrame's `e2e_enc_algorithms` is empty, E2E encryption is not supported and neither side MUST set ENC=1.
- If ENC=1 but no E2E algorithm has been negotiated for the session, the receiver MUST return `NCP-ENC-NOT-NEGOTIATED` and drop the frame.
- In dev/debug mode (non-production only), ENC MAY be 0, but implementations SHOULD log a warning.

**New error codes**

| Error code | NPS status | Description |
|------------|------------|-------------|
| `NCP-ENC-NOT-NEGOTIATED` | `NPS-CLIENT-BAD-FRAME` | ENC=1 but no E2E algorithm negotiated for this session |
| `NCP-ENC-AUTH-FAILED` | `NPS-CLIENT-BAD-FRAME` | E2E Auth Tag verification failed (possible tampering) |

### 7.5 Native-Mode TLS Binding & Mutual Authentication

The transport-security layer for native mode is normatively specified by **[NPS-RFC-0006](rfcs/NPS-RFC-0006-ncp-native-transport.md) §6** (Proposed). In summary, non-`local-dev` native-mode connections MUST:

- Negotiate the suite-wide ALPN token **`nps/1.0`** over a **TLS-wrapped** transport (TCP) or QUIC's built-in TLS 1.3; STARTTLS-style in-band upgrade is prohibited.
- Use **mutual TLS** with NIP-issued certificates (RFC-0002 X.509 NID profile). The server binds the client-certificate NID to the NCP session and MUST reject a certificate/`IdentFrame` NID mismatch with `NCP-NID-MISMATCH`.
- Optionally offer TLS 1.3 **session-resumption tickets** for repeat short-lived connections, bounded to the certificate validity and a ticket lifetime (≤ 24 h); resumption shortcuts only the TLS handshake, never the RFC-0001 preamble or HelloFrame.

This binding is the transport-layer admission gate terminated by the `nps-ingress` (L2) daemon.

### 7.6 Keepalive and Dead-Peer Detection (NCP v0.8)

Long-lived native-mode connections (TCP/QUIC) can be silently killed by NAT appliances or firewalls. NopFrame (§4.8) provides an application-layer keepalive:

- If both sides declare `ping_interval_ms > 0` in their HelloFrame/CapsFrame, both SHOULD send a NopFrame at approximately that interval when no other traffic is flowing.
- The minimum acceptable interval is **1000 ms** (1 second); smaller values MUST be treated as 1000 ms.
- A peer that receives no frame of any kind within `3 × ping_interval_ms` MUST send `NCP-KEEPALIVE-TIMEOUT` and close the connection within 500 ms.
- When only one side declares `ping_interval_ms`, the other side SHOULD honour the declared interval as the shared interval.
- TCP keepalive (`SO_KEEPALIVE`) and QUIC idle timeout are complementary mechanisms; implementations SHOULD configure both at the transport layer in addition to the application-layer NopFrame protocol.

### 7.7 Alpha.19 connection hardening profile (NCP v0.12)

The following requirement IDs freeze the portable runtime behavior used by
[`runtime_hardening_vectors.json`](conformance/ncp/runtime_hardening_vectors.json):

1. **NCP-P19-01 — effective interval.** Let `C` and `S` be the non-zero
   `ping_interval_ms` values offered by the client and server. If both are zero,
   application keepalive is disabled. Otherwise the effective interval is
   `max(1000, min(C, S))` after omitting zero values.
2. **NCP-P19-02 — receive clock.** Admission of any complete, valid inbound NCP
   frame resets the dead-peer clock. Local sends, partial headers, malformed
   frames, and transport-only activity do not reset it.
3. **NCP-P19-03 — idle probe.** When no application frame has been sent for one
   effective interval, a peer MUST enqueue exactly one payload-free NopFrame.
   Ordinary outbound traffic resets the idle-send clock and replaces that probe.
4. **NCP-P19-04 — deterministic close.** At `3 × effective_interval` without a
   valid inbound frame, the connection atomically enters `closing`, emits at
   most one `NCP-KEEPALIVE-TIMEOUT`, cancels all streams, and closes the carrier
   within 500 ms. No application frame may be emitted after the timeout error.
5. **NCP-P19-05 — Nop shape.** A NopFrame payload length other than zero is
   rejected with `NCP-FRAME-PAYLOAD-TOO-LARGE` and never resets either clock.
6. **NCP-P19-06 — no early application data.** NPS preambles, HelloFrames, and
   all other NPS frames MUST NOT be sent or accepted as QUIC/TLS 0-RTT data.
   A server that detects such data rejects it with
   `NCP-EARLY-DATA-REJECTED`; handshake retry after confirmation is allowed.
7. **NCP-P19-07 — QUIC migration identity.** QUIC path migration is allowed only
   after handshake confirmation. It preserves the negotiated NCP session and
   MUST NOT change its certificate/IdentFrame NID binding. A binding change is
   rejected with `NCP-NID-MISMATCH` and closes the connection.
8. **NCP-P19-08 — two-layer backpressure.** Both QUIC/TCP carrier credit and the
   NCP StreamFrame application window apply. Carrier credit never authorizes a
   sender to exceed a zero/exhausted NCP window. NopFrame is not charged to a
   stream window, but it remains subject to carrier writability.

---

### 7.8 Production runtime profile (NCP v0.13)

When experimental `nps.production_runtime` revision 1 is selected, the following
requirements apply. Unselected base behavior is unchanged; no new frame or heartbeat
mechanism is allocated.

#### 7.8.1 Public responsibilities

Six SDKs expose equivalent capabilities with language-idiomatic types:

| Component | Contract |
|---|---|
| SessionFactory | Connect, negotiate, admit identity; return immutable peer/feature/security binding |
| SessionPool | Acquire by full security key with bounded pending queue; release only healthy sessions |
| InvocationDispatcher | Register correlation before write, send typed frames, dispatch exactly one local terminal result |
| RetryDecision | Pure decision from dispatch certainty, operation contract, deadline and remaining budget |
| DrainController | Stop admission, bounded wait/cancel, close with unresolved-operation evidence |

No caller receives a generic setter for admitted authority. Low-level stream APIs may remain,
but bypassing the managed dispatcher forfeits this profile's guarantees and must be explicit.

#### 7.8.2 States and outcomes

Session states: connecting -> negotiating -> admitting -> active -> draining -> closed.
Any establishment failure closes its resources; closed sessions cannot return to a pool.
Reconnect produces a new session and fresh NIP admission, not authorization resumption.

Invocation states: queued -> writing -> awaiting -> completed, with rejected-before-dispatch,
cancelled-before-dispatch, expired-before-dispatch or indeterminate terminal alternatives.
A transport failure during/after the first write attempt is indeterminate unless positive server
evidence proves no dispatch. Bytes written locally or received by TCP do not prove execution.
A valid correlated remote terminal result establishes a known remote outcome. Repeated identical
terminal results are ignored; conflicting terminal results close the session and report a protocol
fault without invoking the application completion callback again.

Maintain a bounded terminal-correlation tombstone set for the remaining session lifetime. When
its configured capacity is reached, drain/reconnect rather than evicting and risking ID reuse.
No correlation ID may be reused within a session. Unknown correlation IDs close the session.
Draining preserves tombstones until close. A late terminal after local cancellation may update
diagnostic evidence but cannot reverse the already-delivered local terminal outcome.

#### 7.8.3 Resource limits

Default local operational limits (applications may configure other finite positive values):

- connect 5 s; handshake 5 s; admission 5 s;
- total request deadline 30 s including queue/write/wait and retries;
- drain 30 s; pool acquisition uses the request's remaining deadline;
- at most 8 pooled sessions per complete pool key, 64 total sessions and 256 pending acquisitions;
- at most 32 queued frames and 1 MiB queued payload bytes per session; whichever limit is hit first;
- terminal tombstone limit 4096 per session.

Negotiated payload/concurrency ceilings always bound local settings. Keepalive/idle rules remain
NCP §7.6/7.7; this profile adds no conflicting heartbeat negotiation. All waits/callbacks honor
remaining deadlines. Queue saturation rejects before dispatch, without hidden unbounded buffers.
For streaming, stop accepting new frames before local buffers overflow and use the protocol's
existing flow control. A peer exceeding the negotiated window fails the session.

The pool key includes endpoint identity, admitted subject, credential generation, assurance/scope
policy fingerprint, selected features and encoding/security profile. Different tenants or delegated
scopes cannot share a session merely because host/port matches. Credential revocation or rotation
invalidates affected pool entries. Bound the total number of keys by the global session/queue limits.

#### 7.8.4 Retry decision

| Evidence | Automatic retry |
|---|---|
| Before any write attempt, no dispatch | Allowed within total deadline and attempt budget |
| Explicit authenticated remote rejection proving no dispatch | Allowed only if operation/profile allows retry and rejection is transient |
| Unknown outcome for read-only operation declared replay-safe | Allowed only under that operation's freshness/cost semantics |
| Unknown outcome for mutation without durable deduplication proof | Never |
| Unknown outcome with server-declared durable idempotency contract | Only same principal/operation key/digest within server retention and permitted target scope |
| Known remote terminal result | Do not replay automatically |

Default attempt budget is 2 total attempts, not 2 retries. Default delay is uniformly sampled
between zero and min(1000 ms, 100 ms * 2^(retry_index-1)); delay consumes the original deadline.
An injected random/clock interface makes tests deterministic. A retry requiring sleep beyond the
remaining deadline terminates. Server retry hints may lengthen delay only within remaining budget.
Reconnect success never proves an earlier operation unexecuted. Failover retries require the same
durable deduplication scope at the new owner; otherwise return indeterminate.

#### 7.8.5 Cancellation and drain

Cancellation before first write prevents dispatch. During/after writing it stops local waiting and
propagates only the cancellation mechanism defined by the negotiated operation/NOP profile.
No invented cancellation frame is allocated here. Without remote rollback/terminal evidence the
outcome remains indeterminate; cancellation does not claim that side effects were undone.

Drain rejects new acquisitions/operations, waits for admitted work within the drain deadline,
requests supported cancellation, then closes and reports unresolved mutations as indeterminate.
A receiver must not advertise draining as proof that previously written operations were unexecuted.
One local completion per invocation applies to timeout/cancel/close races.

#### 7.8.6 Failover and observability

On connection loss, re-resolve active Anchor, honor monotonic cluster_epoch fencing and then
negotiate/admit anew. Clear route/capability caches that belong to the old peer. Do not use a stale
owner or transport resumption ticket as authorization. Never replay an uncertain mutation across
owners without shared durable operation evidence.

Emit connection/negotiation/queue/attempt/retry/reconnect/cancel/drain/failover counters and durations,
outcome certainty and bounded request/correlation identifiers. Do not use subject/scope/operation
keys as unbounded metric labels or log credentials, projections or request bodies.

#### 7.8.7 Error proposals

Runtime API errors are typed outcomes, not automatically new wire error assignments:
queue-full, deadline-before-dispatch, cancelled-before-dispatch, indeterminate,
correlation-conflict and session-closed. Map existing protocol errors without erasing their
original code. Wire additions, if necessary for a specific operation, require its own registry entry.

Operation cancellation bindings are NWP §7.3 and NOP §3.6/§5.2. No operation may
be marked safe for automatic replay without an explicit durable retention period,
principal/key/digest binding and cross-owner scope guarantee. Product state names or
a successful connection do not establish these guarantees.

---

### 7.9 Experimental schema-batch candidate

The optional [schema-batch revision 1](profiles/schema-batches.md), recorded by
[CR-0017](cr/NPS-CR-0017-schema-batches.md), defines explicit ordered rows, complete
stream baselines and epoch/schema/base-bound diffs over existing frames. Exact
`nps.schema_batches=1` selection and local experimental opt-in are required; a suite
version, anchor or feature registry entry is not admission or source authority.
Unselected operations retain their base behavior. The current .NET reference is
limited to its explicitly qualified mutual TLS subset. Candidate registration does
not establish six-SDK, NWP live-result-stream or efficiency/billing qualification.

---

## 8. Encoding Tiers

All frame types support the following encoding tiers, selected via the T0/T1 bits of Flags:

| Tier | Format | Flag | Use case |
|------|--------|------|----------|
| Tier-1 | JSON | `00` | Development, debugging, compatibility mode |
| Tier-2 | MsgPack (binary) | `01` | Production, ~60% size reduction |
| Tier-3 | BinaryVector v1 | `10` | Vector-heavy frames; MessagePack metadata plus raw little-endian float32 vector segments |
| — | Reserved | `11` | Reserved |

Defaults are policy-level defaults: Tier-2 for production and Tier-1 for development/compatibility. Implementations SHOULD choose a stable session encoding policy during handshake or configuration, then apply it consistently. A frame MAY use a different tier only when the selected policy explicitly allows that frame class or phase.

Tier-3 is optional. Senders MUST NOT emit `Flags.T1T0 = 10` unless the session has negotiated `binary_vector.v1`; receivers that do not support or did not negotiate Tier-3 MUST reject the frame with `NCP-ENCODING-UNSUPPORTED`. `Flags.T1T0 = 11` remains reserved and MUST be rejected with `NCP-FRAME-FLAGS-INVALID`.

### 8.1 Tier-3 BinaryVector v1 Payload

Tier-3 BinaryVector v1 is a frame-payload encoding, not a replacement for NWP semantics. The frame type byte still determines the logical frame, and the Tier-3 payload encodes that frame's metadata plus dense vector segments.

Negotiation token: `binary_vector.v1`.

Binary layout:

| Offset | Size | Field | Encoding |
|--------|------|-------|----------|
| 0 | 4 | Magic | ASCII `NPBV` |
| 4 | 1 | Version | `0x01` |
| 5 | 1 | Flags | `0x00` in v1; receivers MUST reject non-zero values |
| 6 | 2 | `vector_count` | uint16, big-endian |
| 8 | 4 | `metadata_len` | uint32, big-endian |
| 12 | 4 | Reserved | MUST be zero |
| 16 | `metadata_len` | Metadata | MessagePack map using the same field names as Tier-2 |
| ... | variable | Vector segments | repeated `dim:uint32_be` + `dim` little-endian float32 values |

Metadata MUST preserve the logical frame object. Vector fields moved to binary segments are replaced by a marker object:

```json
{
  "$nps_binary_vector": 0,
  "dtype": "float32",
  "dim": 1536
}
```

For NCP v0.9, the standard binding is NWP `QueryFrame.vector_search.vector`. The marker index is zero-based and MUST reference one of the appended vector segments. `dtype` MUST be `float32`; vector values are IEEE-754 binary32 little-endian. MatrixTensor, float16, quantized int8, and multi-vector schema bindings are reserved for future CRs.

---

## 9. Implementation Notes

- Recommended AnchorFrame cache limit: 1000 entries per connection, LRU-evicted on overflow.
- MsgPack (Tier-2) codec: use the official MessagePack library; do not implement your own.
- When StreamFrame `seq` overflows (reaches `0xFFFFFFFF` as uint32), the new stream MUST use a new `stream_id`.
- Support for extended header (EXT=1) SHOULD be declared in the CapsFrame capability negotiation.
- **anchor_id computation**: use an RFC 8785 JCS standard library; do not implement canonicalization yourself (cross-language SDK consistency).
- **HelloFrame timeout**: if the Server does not receive a HelloFrame within 5 seconds, it SHOULD send an ErrorFrame and close the connection.
- **DiffFrame binary_bitset**: only after both sides have negotiated support via CapsFrame; forbidden in Tier-1 JSON mode.
- **E2E encryption nonce**: a per-frame random nonce (12 bytes, CSPRNG); never reuse; key rotation SHOULD be at least once per 2^32 frames.
- **inline_anchor handling**: after receiving `inline_anchor`, the Agent MUST first verify the anchor_id (JCS + SHA-256), then update the cache.
- **Tier-2 performance optimization (.NET)**: `Tier2MsgPackCodec` is recommended to use MessagePack AOT code generation to avoid reflection overhead in high-frequency frame parsing (implementation guidance; does not affect protocol interoperability).

---

## 10. Change Log

| Version | Date | Changes |
|---------|------|---------|
| 0.13 | 2026-09-09 | Alpha.20 optional feature revision negotiation: bounded types and complete handshake payloads, deterministic selection, required-feature pre-dispatch rejection and session scope; no new frames or base compatibility-floor change. Also defines bounded production runtime, retry/drain certainty, and corrects JCS Unicode and handshake Caps examples. |
| 0.12 | 2026-08-31 | Alpha.19 connection hardening profile: deterministic effective keepalive interval and receive/idle clocks, single timeout-close transition, payload-free Nop enforcement, replay-safe rejection of QUIC/TLS 0-RTT NPS frames, identity-preserving QUIC migration, and carrier/application backpressure composition. Adds shared runtime hardening vectors and `NCP-EARLY-DATA-REJECTED`. |
| 0.11 | 2026-07-29 | Added §2.6.2 Native Server Interoperability Profile: authentication-before-preamble ordering, separate bounded preamble/Hello reads, allocation-safe Hello limits, silent pre-admission failure behavior, deterministic version/encoding/protocol/limit negotiation, portable handshake Caps fields, and shared `native_server_handshake_vectors.json`. Additive; no new frame type or error code. |
| 0.10 | 2026-07-05 | Native-mode transport adopted as normative (NPS-RFC-0006 **Accepted**). New native-mode **session continuity across Anchor failover** (NPS-CR-0009): on connection loss / `NCP-NID-MISMATCH` after a multi-Anchor ownership transfer, the client re-resolves via NDP §9 (highest `cluster_epoch`) or the NWP `anchor_failover` `successor_nid` and re-establishes the session. No new frames or error codes (`NCP-NID-MISMATCH` reused). |
| 0.9 | 2026-06-27 | Activated Tier-3 BinaryVector v1 (`Flags.T1T0 = 10`) with negotiation token `binary_vector.v1`; defined the `NPBV` payload layout, MessagePack metadata marker, float32 little-endian vector segments, and NWP `QueryFrame.vector_search.vector` binding; `0b11` remains reserved. |
| 0.8 | 2026-06-12 | New §7.5 Native-Mode TLS Binding & Mutual Authentication, summarising **NPS-RFC-0006 §6** (promoted Draft → Proposed): suite-wide ALPN `nps/1.0` (supersedes provisional `ncp/1`), TLS-wrapped framing mandated for native-mode-over-TCP, mTLS with NIP certificates + session-NID binding, TLS 1.3 session-resumption tickets; added error code `NCP-NID-MISMATCH`. Gates the `nps-ingress` (L2) daemon. |
| 0.8 | 2026-06-03 | **NopFrame (0x07)** keepalive/heartbeat: null payload, bidirectional; `HelloFrame.ping_interval_ms` declares preferred interval (0 = disabled, default); `NCP-KEEPALIVE-TIMEOUT` error code when no frame received within 3 × interval; §7.6 dead-peer detection rules. |
| 0.7 | 2026-05-31 | `max_concurrent_streams` negotiation via HelloFrame/CapsFrame (uint32, default 32); `NCP-STREAM-LIMIT-EXCEEDED` on overflow; mid-stream ErrorFrame MUST be sent when rejecting in-progress streams (previously MAY); E2E rekeying protocol (§7.4): trigger at 2^32 frames or 24 h, `NCP-REKEY-REQUIRED` signal; QUIC stream mapping: one bidirectional stream per NCP channel, HelloFrame on stream 0. See [NPS-RFC-0006](rfcs/NPS-RFC-0006-ncp-native-transport.md). |
| 0.6 | 2026-04-25 | Added §2.6.1 native-mode connection preamble (8-byte constant `b"NPS/1.0\n"`); reserved frame-type byte 0x4E in `frame-registry.yaml`; added error code `NCP-PREAMBLE-INVALID` and status code `NPS-PROTO-PREAMBLE-INVALID`. See [NPS-RFC-0001](rfcs/NPS-RFC-0001-ncp-connection-preamble.md). |
| 0.4 | 2026-04-14 | Added HelloFrame (0x06); new §2.6 handshake sequence & version-negotiation rules; anchor_id computation now explicitly references RFC 8785 JCS; DiffFrame gains `patch_format` (json_patch / binary_bitset); CapsFrame gains `inline_anchor`; StreamFrame flow control formalized (window_size protocol); §7.4 E2E encryption section (ENC flag, AES-256-GCM / ChaCha20-Poly1305, payload layout); §5.4 auto-anchor protocol (NCP-ANCHOR-STALE + inline_anchor); added error codes NCP-ANCHOR-STALE, NCP-DIFF-FORMAT-UNSUPPORTED, NCP-VERSION-INCOMPATIBLE, NCP-STREAM-WINDOW-OVERFLOW, NCP-ENC-NOT-NEGOTIATED, NCP-ENC-AUTH-FAILED |
| 0.3 | 2026-04-12 | Dual transport (HTTP / native); unified port 17433; configurable frame size (EXT bit); ErrorFrame (0xFE); NPS status-code system; Tier-3 marked Reserved; AnchorFrame ownership clarified as Node-published; token estimation switched to CGN |
| 0.2 | 2026-04-10 | AnchorFrame / DiffFrame / StreamFrame / CapsFrame / AlignFrame definitions; bit-level Flags definition; AlignFrame marked Deprecated |
| 0.1 | 2026-03-01 | Initial frame format and encoding tiers |

---

*Copyright: LabAcacia / INNO LOTUS PTY LTD · Apache 2.0*
