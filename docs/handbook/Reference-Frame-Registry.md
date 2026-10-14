# Reference: Frame Registry

**Status:** ✅ Reviewed for v1.0.0-alpha.19 release

Every NPS frame type is identified by a single byte. Frames from all five protocols share one unified byte space, routed by type code. The machine-readable source of truth is `spec/frame-registry.yaml` in the repository — that file is CI-validated on every commit.

This page provides the human-readable reference for the same data.

## How the Byte Space Works

All NPS connections — native mode and HTTP mode — carry frames prefixed by a 1-byte type identifier. Because all protocols share port 17433 (the unified NPS port), the type byte is the primary routing signal: a node reads the first byte and dispatches the frame to the correct protocol handler.

Each protocol owns a contiguous range:

| Range | Owner |
|-------|-------|
| `0x01–0x0F` | NCP — Neural Communication Protocol |
| `0x10–0x1F` | NWP — Neural Web Protocol |
| `0x20–0x2F` | NIP — Neural Identity Protocol |
| `0x30–0x3F` | NDP — Neural Discovery Protocol |
| `0x40–0x4F` | NOP — Neural Orchestration Protocol |
| `0xF0–0xFF` | System / Reserved |

Bytes not listed in the registry as assigned are **reserved**. Implementations MUST NOT define or handle frames with unassigned bytes unless an RFC explicitly claims the range.

---

## NCP Frames (0x01–0x0F)

Neural Communication Protocol — wire format, framing, streaming, and handshake.

| Byte | Frame Name | Status | Spec Reference | Description |
|------|------------|--------|----------------|-------------|
| `0x01` | AnchorFrame | Stable | NPS-1-NCP §4.1 | Schema anchor published by a Node. `anchor_id` is computed via RFC 8785 JCS + SHA-256. Establishes a global schema reference to eliminate repeated schema transmission. Agents read and cache; they do not author AnchorFrames. |
| `0x02` | DiffFrame | Stable | NPS-1-NCP §4.2 | Incremental data frame. Supports `json_patch` (RFC 6902, Tier-1 default) and `binary_bitset` (Tier-2, ~15–20% smaller) via the `patch_format` field. NPS-CR-0002 / CR-0006 (NWP §13.2) extends `event_type` with the full topology-stream set: `member_joined`, `member_left`, `member_updated`, `anchor_state`, `resync_required`. NWP v0.13 (CR-0002 Phase 2) adds the optional per-event `cgn_est` field (uint32, estimated CGN cost of this push event's payload) for agent-side cumulative-budget accounting. |
| `0x03` | StreamFrame | Stable | NPS-1-NCP §4.3 | Streaming data chunk with formal flow control (`window_size` backpressure protocol). Used for large datasets, real-time push, or payload fragmentation. |
| `0x04` | CapsFrame | Stable | NPS-1-NCP §4.4 | Capsule response frame. Wraps a complete response body with anchor reference and pagination cursor. Also used for server-side connection capability negotiation. Supports `inline_anchor` for zero-RTT schema refresh. |
| `0x05` | ~~AlignFrame~~ | **Deprecated** | NPS-1-NCP §4.5 | Multi-AI state alignment. **Superseded by NOP AlignStream (0x43).** New implementations MUST NOT emit this frame type. |
| `0x06` | HelloFrame | Stable | NPS-1-NCP §4.6 | Native-mode client handshake frame. Sent by the Agent immediately after TCP/QUIC connect to declare protocol version, encoding capabilities, `max_frame_payload`, and (NCP v0.8) `ping_interval_ms` (uint32; 0 = disabled). Server responds with CapsFrame. Not used in HTTP mode. |
| `0x07` | NopFrame | Stable | NPS-1-NCP §4.8 | Keepalive / heartbeat frame (added NCP v0.8). Null payload (the single byte `0x07`). Either peer MAY send at any time after the handshake; the receiver MUST accept and SHOULD reply with another NopFrame. When `HelloFrame.ping_interval_ms` > 0, both peers SHOULD send at that interval; the dead-peer detection threshold is 3 × `ping_interval_ms` (NCP §7.6). Timeout surfaces as `NCP-KEEPALIVE-TIMEOUT` → `NPS-SERVER-TIMEOUT`. |

**Reserved in NCP range:** `0x08–0x0F`.

> **Special reservation — `0x4E`**: The byte `0x4E` (ASCII `N`) is reserved by NPS-RFC-0001 as the first byte of the native-mode connection preamble `b"NPS/1.0\n"`. Servers MUST NOT interpret `0x4E` as a frame type when it is read as the very first byte after the transport handshake.

### Encoding Tiers

The frame *type byte* identifies the logical frame; the *encoding tier* is a separate, orthogonal choice carried in the `T0`/`T1` bits of the NCP frame Flags field (NPS-1-NCP §8). Every frame type can be serialized at any negotiated tier — the tier does not change which frame it is.

| Tier | Flag (`T1 T0`) | Format | Use case |
|------|---------------|--------|----------|
| Tier-1 | `00` | JSON | Development, debugging, compatibility mode |
| Tier-2 | `01` | MsgPack (binary) | Production default; ~60% size reduction |
| Tier-3 | `10` | **BinaryVector v1** (`binary_vector.v1`) | Vector-heavy frames; MessagePack metadata plus raw little-endian float32 vector segments (NCP v0.9) |
| — | `11` | Reserved | MUST be rejected with `NCP-FRAME-FLAGS-INVALID` |

**Tier-3 BinaryVector v1 (`binary_vector.v1`, NCP v0.9).** Activated in NCP v0.9, Tier-3 is an optional, negotiated frame-*payload* encoding for compact dense-vector (embedding) payloads — primarily the NWP `QueryFrame.vector_search.vector` binding. It is **not** a new frame type and does **not** consume a registry byte. Senders MUST NOT emit `Flags.T1T0 = 10` unless both peers advertised `binary_vector.v1` in their `supported_encodings` / `enabled_encodings`; a receiver that did not negotiate Tier-3 MUST reject the frame with `NCP-ENCODING-UNSUPPORTED`.

The Tier-3 payload is a 16-byte fixed prefix followed by MessagePack metadata and one or more appended vector segments:

| Offset | Size | Field | Encoding |
|--------|------|-------|----------|
| 0 | 4 | Magic | ASCII `NPBV` |
| 4 | 1 | Version | `0x01` |
| 5 | 1 | Flags | `0x00` in v1; receivers MUST reject non-zero |
| 6 | 2 | `vector_count` | uint16, big-endian |
| 8 | 4 | `metadata_len` | uint32, big-endian |
| 12 | 4 | Reserved | MUST be zero |
| 16 | `metadata_len` | Metadata | MessagePack map (same field names as Tier-2) |
| … | variable | Vector segments | repeated `dim:uint32_be` + `dim` little-endian float32 values |

Vector fields moved into binary segments are replaced in the metadata map by a zero-based marker object, e.g. `{"$nps_binary_vector": 0, "dtype": "float32", "dim": 1536}`. `dtype` MUST be `float32` (IEEE-754 binary32, little-endian). Malformed Tier-3 payloads surface documented client errors rather than server-internal faults: `NCP-BINARY-VECTOR-MALFORMED`, `-DIM-MISMATCH`, `-INDEX-INVALID`, `-DTYPE-UNSUPPORTED`, and `-TRUNCATED`, all mapping to `NPS-CLIENT-BAD-FRAME`. MatrixTensor, float16, quantized int8, and multi-vector bindings are reserved for future CRs.

---

## NWP Frames (0x10–0x1F)

Neural Web Protocol — query, action, and subscription.

| Byte | Frame Name | Status | Spec Reference | Description |
|------|------------|--------|----------------|-------------|
| `0x10` | QueryFrame | Draft | NPS-2-NWP §6 | Structured data query for Memory Nodes. Supports filtering, field selection, cursor pagination, ordering, vector search, streaming mode, aggregation, and (NPS-CR-0002) optional `type` field for reserved namespaces (`topology.snapshot`, `topology.stream`). Unrecognized `type` values MUST be rejected with `NWP-RESERVED-TYPE-UNSUPPORTED`. |
| `0x11` | ActionFrame | Draft | NPS-2-NWP §7 | Operation invocation for Action and Complex Nodes. Supports sync/async execution, idempotency, `callback_url`, priority, and `request_id`. Standard actions include task status/cancel, typed `llm.complete`, and CR-0011 `llm.context.status` / `llm.context.release`; LLM semantics add no new frame type. |
| `0x12` | SubscribeFrame | Stable | NPS-2-NWP §13 | Change subscription management for Memory and Anchor Nodes. The CR-0006 formal wire shape (NWP v0.13) uses `subscription_id` (UUID v4), a QueryFrame-compatible `filter`, `heartbeat_interval_ms`, `max_events`, and an opaque `cursor` for lossless resume; the server pushes DiffFrame (0x02) events until cancellation or closure. HTTP mode uses Server-Sent Events. NPS-CR-0002 adds an optional `type` field — set `type="topology.stream"` for the §12.2 cluster change feed. Unrecognized `type` values MUST be rejected with `NWP-RESERVED-TYPE-UNSUPPORTED`. `topology.stream` subscriptions MUST carry both `topology:read` and `topology:subscribe` (NWP §12.4). |

**Reserved in NWP range:** `0x13–0x1F`.

---

## NIP Frames (0x20–0x2F)

Neural Identity Protocol — identity declarations, trust chains, and revocation.

| Byte | Frame Name | Status | Spec Reference | Description |
|------|------------|--------|----------------|-------------|
| `0x20` | IdentFrame | Proposed | NPS-3-NIP §5.1 | Agent identity declaration and certificate. Carries NID, public key, capabilities, scope, optional metadata (`model_family`, `tokenizer`), and (NPS-RFC-0003) optional `assurance_level` (`anonymous` / `attested` / `verified`). NIP v0.7–0.10 add `cert_chain` + `cert_format` (DER chain + encoding), `ocsp_staple` (base64url DER OCSP, NIP v0.9), and `node_roles` (string[] self-declared role tags, NIP v0.10) — all excluded from the Ed25519-signed payload. Used as the connection handshake. |
| `0x21` | TrustFrame | Proposed | NPS-3-NIP §5.2 | Cross-CA trust chain delegation. One CA (`grantor_nid`) authorizes another CA (`grantee_ca`) to issue IdentFrames whose `trust_scope` and `nodes` patterns are honored by nodes that trust the grantor; carries `issued_at` / `expires_at` / serial / `signer_nid`, signed Ed25519 or ECDSA P-256 over canonical JSON. |
| `0x22` | RevokeFrame | Proposed | NPS-3-NIP §5.3 | NID or capability revocation. Takes immediate effect upon CA acknowledgment. |

**Reserved in NIP range:** `0x23–0x2F`.

---

## NDP Frames (0x30–0x3F)

Neural Discovery Protocol — presence announcement, address resolution, and graph synchronization.

| Byte | Frame Name | Status | Spec Reference | Description |
|------|------------|--------|----------------|-------------|
| `0x30` | AnnounceFrame | Draft | NPS-4-NDP §3.1 | Signed Node/Agent presence and capability broadcast. In addition to addresses, TTL, roles, activation and SpawnSpec, NDP v0.10-v0.12 add HA `cluster_epoch`, directional `bridge_inbound_protocols`, monotonic `graph_seq`, and portable-registry validation. Legacy `gateway` is rejected and `node_kind` is no longer emitted. |
| `0x31` | ResolveFrame | Draft | NPS-4-NDP §3.2 | Resolves a `nwp://` URL to a physical endpoint (host:port + certificate fingerprint). |
| `0x32` | GraphFrame | Draft | NPS-4-NDP §5 | Node graph synchronization. Rewritten in NDP v0.8 to the §5 topology-snapshot format: `graph_id`, `nodes` (`nid` / `cluster_anchor` / `node_roles`), `edges` (`from_nid` / `to_nid` / `latency_ms` / `protocol`), `ttl`, and `metadata`. Limits: max 256 nodes / 1024 edges (`NDP-GRAPH-INVALID`, `NDP-GRAPH-TOO-LARGE`). Supports full and incremental sync modes. |

**Reserved in NDP range:** `0x33–0x3F`.

---

## NOP Frames (0x40–0x4F)

Neural Orchestration Protocol — DAG task dispatch, delegation, synchronization, and directed streaming.

| Byte | Frame Name | Status | Spec Reference | Description |
|------|------------|--------|----------------|-------------|
| `0x40` | TaskFrame | Draft | NPS-5-NOP §3.1 | Task definition and dispatch. Defines a DAG of subtasks with per-node `timeout`, `retry_policy`, `condition`, and `input_mapping`. Supports preflight resource check, OpenTelemetry context propagation, `callback_url`, and `priority`. Maximum DAG node count: 32. |
| `0x41` | DelegateFrame | Draft | NPS-5-NOP §3.2 | Subtask delegation to a Worker Agent. Carries a carved-down scope (MUST NOT exceed parent scope). Also used for preflight probe (`action="preflight"`) and cancellation (`action="cancel"`). Maximum delegation chain depth: 3 levels. |
| `0x42` | SyncFrame | Draft | NPS-5-NOP §3.3 | Multi-Agent synchronization barrier. Supports K-of-N semantics (`min_required`), aggregation strategies (`merge` / `first` / `all` / `fastest_k`), and per-barrier timeout. |
| `0x43` | AlignStream | Draft | NPS-5-NOP §3.4 | Directed task stream. Supersedes deprecated AlignFrame (0x05). Extends NCP streaming with task DAG context (`task_id` + `subtask_id`), NID binding, CGN-unit backpressure (`window_size`), and error propagation. |

**Reserved in NOP range:** `0x44–0x4F`.

---

## System / Reserved (0xF0–0xFF)

| Byte | Frame Name | Status | Spec Reference | Description |
|------|------------|--------|----------------|-------------|
| `0xFE` | ErrorFrame | Stable | NPS-1-NCP §4.7 | Unified error frame for all protocol layers. Carries NPS status code, protocol error code, and optional diagnostic details. Used in native transport mode. Allocated from the Reserved range and shared across all five protocols. |

**Reserved:** `0xF0–0xFD` and `0xFF`. Implementations MUST NOT define frames in this range without a spec update.

---

## Reserved Ranges Summary

| Range | Status |
|-------|--------|
| `0x05` | Deprecated (AlignFrame, superseded by 0x43) |
| `0x07` | Assigned — NopFrame (keepalive/heartbeat, NCP v0.8) |
| `0x08–0x0F` | Reserved — NCP expansion |
| `0x13–0x1F` | Reserved — NWP expansion |
| `0x23–0x2F` | Reserved — NIP expansion |
| `0x33–0x3F` | Reserved — NDP expansion |
| `0x44–0x4F` | Reserved — NOP expansion |
| `0x4E` | Hard-reserved — native-mode preamble byte (NPS-RFC-0001) |
| `0x50–0xEF` | Unassigned — available for future protocol layers via RFC |
| `0xF0–0xFD`, `0xFF` | System reserved |

---

## How to Claim a New Frame Type

New frame types require a formal RFC. The process is:

1. **Draft an RFC** using the template at `spec/rfcs/template.md`. The RFC must justify the new frame, specify its byte assignment from an appropriate reserved range, and define the full field schema.
2. **Open a PR** against the `dev` branch following the RFC process described in `spec/rfcs/README.md`. Breaking changes to existing frames require shepherd approval and a minimum discussion window.
3. **After the RFC is accepted**, add the frame entry to `spec/frame-registry.yaml`. CI validates that the YAML is well-formed and that no byte is assigned twice.
4. **Update the owning protocol spec** (`spec/NPS-{n}-*.md`) with the complete field definition table.
5. **Update `spec/NPS-0-Overview.md`** frame namespace table.

Do not implement or ship a new frame type before the RFC is accepted. The `frame-registry.yaml` is the authoritative registry — any byte not listed there as `assigned` is treated as unknown by all conformant implementations.

---

## See Also

- [Protocol NCP](./Protocol-NCP.md) — NCP frame semantics in full

---

*Last reviewed at suite version: v1.0.0-alpha.19 release*
