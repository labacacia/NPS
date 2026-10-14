# Glossary

> **Audience:** Anyone (reference)
> **Status:** ✅ Reviewed for v1.0.0-alpha.19
> **Source-of-truth precedence:** `spec/` documents in [`labacacia/NPS`](https://github.com/labacacia/NPS/tree/main/spec) win over this page if they disagree.
>
> Each entry: one-sentence definition followed by the primary spec reference in parentheses.
> See also: [What Is NPS](./What-Is-NPS.md) for a prose introduction, [Protocol Stack Architecture](./Protocol-Stack-Architecture.md) for layer relationships.

---

## A

**Agent** — An AI entity (typically an LLM-driven process) that holds a NIP-issued NID certificate, declares capabilities in an `IdentFrame`, and communicates with NWP nodes and other agents over the NPS suite. ([NPS-0 §4.5](https://github.com/labacacia/NPS/blob/main/spec/NPS-0-Overview.md))

**Agent Node** — An NWP node type that is itself an AI executor; speaks NWP + NOP, carries an NID, and participates in NOP DAG executions as a Worker Agent. ([NPS-2 §2.1](https://github.com/labacacia/NPS/blob/main/spec/NPS-2-NWP.md))

**AlignFrame** — A deprecated NCP frame type (0x05) for multi-agent state alignment; superseded by `AlignStream` (0x43) and MUST NOT be used in new implementations. ([NPS-1 §4.5](https://github.com/labacacia/NPS/blob/main/spec/NPS-1-NCP.md))

**AlignStream** — The NOP frame type (0x43) that replaces `AlignFrame`; a directed task stream carrying DAG context (`task_id` + `subtask_id`), NID binding, and token-level backpressure control in CGN units. ([NPS-5 §3.4](https://github.com/labacacia/NPS/blob/main/spec/NPS-5-NOP.md))

**Anchor Node** — An NWP node type serving as the stateless cluster entry point; authenticates callers via NIP, routes inbound NWP `Action`/`Query` frames to member nodes via NOP, and optionally maintains a member-node topology registry exposed through `topology.snapshot` and `topology.stream`. ([NPS-2 §2.1](https://github.com/labacacia/NPS/blob/main/spec/NPS-2-NWP.md), [NPS-CR-0001](https://github.com/labacacia/NPS/blob/main/spec/cr/NPS-CR-0001-anchor-bridge-split.md))

**AnchorFrame** — The NCP frame type (0x01) published by a node to broadcast its full data schema; content-addressed by a SHA-256 `anchor_id` so that agents can cache it once and eliminate schema retransmission for 30–60% token savings per session. ([NPS-1 §4.1](https://github.com/labacacia/NPS/blob/main/spec/NPS-1-NCP.md))

**AssuranceLevel** — A field on `IdentFrame` (and corresponding X.509 certificate extension `id-nid-assurance-level`) indicating the identity-verification strength of a NID; one of `"anonymous"`, `"attested"`, or `"verified"`; absent field MUST be treated as `"anonymous"` for backward compatibility. ([NPS-3 §5.1](https://github.com/labacacia/NPS/blob/main/spec/NPS-3-NIP.md), [NPS-RFC-0003](https://github.com/labacacia/NPS/blob/main/spec/rfcs/NPS-RFC-0003-agent-identity-assurance-levels.md))

---

## B

**BinaryVector (`binary_vector.v1`)** — The optional NCP Tier-3 encoding tier (Flags `T1T0 = 10`), activated in NCP v0.9, for compact dense-vector (embedding) payloads — primarily the NWP `QueryFrame.vector_search.vector` binding; a payload carries a 16-byte `NPBV` prefix (magic + version + `vector_count` + `metadata_len`), a MessagePack metadata map, and appended per-vector `dim` (uint32 BE) + little-endian float32 segments. It is negotiated via `supported_encodings` / `enabled_encodings` and is not a new frame type; malformed payloads return `NCP-BINARY-VECTOR-*` client errors and the reserved tier `0b11` returns `NCP-FRAME-FLAGS-INVALID`. ([NPS-1 §8.1](https://github.com/labacacia/NPS/blob/main/spec/NPS-1-NCP.md), [NPS-CR-0008](https://github.com/labacacia/NPS-Dev/blob/main/spec/cr/NPS-CR-0008-tier3-binary-vector.md))

**Bridge Node** — An NWP node type that translates outbound NPS frames into non-NPS external protocols (HTTP/REST, gRPC, MCP, A2A) and maps responses back into NWP frames; stateless per request and does not participate in cluster topology. ([NPS-2 §2.1](https://github.com/labacacia/NPS/blob/main/spec/NPS-2-NWP.md), [NPS-CR-0001](https://github.com/labacacia/NPS/blob/main/spec/cr/NPS-CR-0001-anchor-bridge-split.md))

**Bridge server (inbound)** — The *inverse* of the outbound Bridge Node: an SDK adapter (`McpServerBridge` / `A2aServerBridge`, wired via ASP.NET `AddBridgeServer` / `UseBridgeServer`) that lets external MCP or A2A clients invoke **local NPS actions**. Secure-by-default — it requires a valid `X-NWP-Agent` NID plus a configured verifier hook, enforces an action allowlist, bounds the request body (`MaxRequestBodyBytes`, default 1 MB → 413), bounds dispatch (`DispatchTimeoutMs`, default 30 s → 504), and sanitizes client-facing errors. ([NPS-2 §2.1](https://github.com/labacacia/NPS/blob/main/spec/NPS-2-NWP.md))

---

## C

**Capability** — A string token (e.g., `nwp:query`, `nop:delegate`, `topology:read`) declared in an agent's `IdentFrame` or an `AnnounceFrame`; nodes enforce capability gates at the protocol layer before processing frames. ([NPS-3 §5.1](https://github.com/labacacia/NPS/blob/main/spec/NPS-3-NIP.md))

**CapsFrame** — The NCP frame type (0x04) used as the standard response envelope; wraps a response body with an anchor reference and pagination cursor, and is also used by the server during native-mode capability negotiation. ([NPS-1 §4.4](https://github.com/labacacia/NPS/blob/main/spec/NPS-1-NCP.md))

**CGN (Cognon)** — The standardized cross-model token accounting unit in NPS; agents declare a maximum CGN budget per request via `X-NWP-Budget` and nodes use the tokenizer resolution chain to enforce truncation or rejection. Since token-budget v0.5, CGN is split into two non-overlapping profiles, CGN-Estimate and CGN-Billing. ([spec/token-budget.md](https://github.com/labacacia/NPS/blob/main/spec/token-budget.md))

**CGN-Billing** — The settlement-grade CGN profile (token-budget §2.1) used for commercial billing and dispute/chargeback handling; requires the `verified_tokenizer` tier (NIP §5.1), NID-signed and audit-logged metering records, exact (non-sampled) per-record counts, a pinned exchange-rate-table version, and the `X-NWP-Tokens-Profile: billing` / `X-NWP-Billing-Record` / `X-NWP-Billing-Tokenizer-Tier` response headers. ([spec/token-budget.md §2.1, §4.2](https://github.com/labacacia/NPS/blob/main/spec/token-budget.md))

**CGN-Estimate** — The estimation-grade CGN profile (token-budget §2.1) used for `X-NWP-Budget` enforcement, telemetry, and push-stream `cgn_est` reporting; permits sampling, the `ceil(UTF-8_bytes / 4)` byte-size fallback, and ±5 % exchange-rate drift, requires no signing, and is the default for any CGN value carried without an explicit profile marker. ([spec/token-budget.md §2.1](https://github.com/labacacia/NPS/blob/main/spec/token-budget.md))

**Complex Node** — An NWP node type that combines data storage and callable operations; may include sub-node references and serves both `QueryFrame` and `ActionFrame` requests. ([NPS-2 §2.1](https://github.com/labacacia/NPS/blob/main/spec/NPS-2-NWP.md))

**Compensation (Saga)** — The NOP rollback mechanism (NOP v0.6) for partially-executed DAGs; a `CompensationPolicy` plus per-node `DagNode.compensate_action` define how to undo completed subtasks, with `TaskState` values `COMPENSATING` / `COMPENSATED` tracking the rollback. ([NPS-5 NOP](https://github.com/labacacia/NPS/blob/main/spec/NPS-5-NOP.md))

---

## D

**DAG** — A Directed Acyclic Graph; the execution model for NOP `TaskFrame` workflows, with up to 32 nodes (agent sub-tasks) connected by directed data-flow edges; cycles are rejected with `NOP-TASK-DAG-CYCLE`. ([NPS-5 §3.1](https://github.com/labacacia/NPS/blob/main/spec/NPS-5-NOP.md))

**DiffFrame** — The NCP frame type (0x02) for incremental schema evolution and streaming change notifications; supports `json_patch` (RFC 6902) and `binary_bitset` patch formats; extended by NPS-CR-0002 with topology-stream event types (`member_joined`, `member_left`, `member_updated`). ([NPS-1 §4.2](https://github.com/labacacia/NPS/blob/main/spec/NPS-1-NCP.md))

---

## E

**ErrorFrame** — The unified NPS error frame (0xFE) used by all protocol layers; carries an NPS status code, a structured protocol error code in `DOMAIN-CATEGORY-DETAIL` format, and optional detail fields. ([NPS-1 §4.7](https://github.com/labacacia/NPS/blob/main/spec/NPS-1-NCP.md), [spec/error-codes.md](https://github.com/labacacia/NPS/blob/main/spec/error-codes.md))

---

## F

**Federation** — The NDP v0.8 §9 mechanism by which `public-federated` registries forward AnnounceFrames to one another so discovery spans organizations; loop detection uses the `ndp-forwarded-by` chain (max 3 hops, `NDP-FEDERATION-LOOP`) and is governed by the `SecurityProfile` values `LOCAL_DEV` / `ORG_PRIVATE` / `PUBLIC_FEDERATED`. ([NPS-4 §9](https://github.com/labacacia/NPS/blob/main/spec/NPS-4-NDP.md))

**Frame** — The fundamental unit of NPS communication; standard frames have a 4-byte header (1 B frame type + 1 B flags + 2 B payload length) supporting up to 64 KB, while EXT-flag frames use an 8-byte header supporting up to 4 GB. ([NPS-1 §3](https://github.com/labacacia/NPS/blob/main/spec/NPS-1-NCP.md))

---

## M

**Memory Node** — An NWP node type that owns a data anchor and serves read queries from agents; the most common node type, analogous to a REST data endpoint, accessed via `QueryFrame`. ([NPS-2 §2.1](https://github.com/labacacia/NPS/blob/main/spec/NPS-2-NWP.md))

---

## N

**NCP (Neural Communication Protocol)** — The framing and base-layer protocol of NPS; defines the frame format, three encoding tiers (Tier-1 JSON, Tier-2 MsgPack, and the optional Tier-3 BinaryVector `binary_vector.v1` activated in NCP v0.9), and the core frame types; all higher-level protocols are carried as NCP frames. ([NPS-1 NCP](https://github.com/labacacia/NPS/blob/main/spec/NPS-1-NCP.md))

**NDP (Neural Discovery Protocol)** — The NPS discovery layer analogous to DNS; agents and nodes use it to resolve `nwp://` addresses to physical (host, port, protocol) tuples, broadcast capabilities via signed `AnnounceFrame`, and subscribe to topology graph changes. ([NPS-4 NDP](https://github.com/labacacia/NPS/blob/main/spec/NPS-4-NDP.md))

**NID (Neural Identity Descriptor)** — The globally unique identity URN for any agent, node, or organization in NPS; takes the form `urn:nps:{entity-type}:{issuer-domain}:{identifier}` and is backed by an Ed25519 keypair and a NIP CA-issued certificate. ([NPS-3 §3](https://github.com/labacacia/NPS/blob/main/spec/NPS-3-NIP.md))

**NIP (Neural Identity Protocol)** — The NPS identity layer analogous to TLS/PKI; issues verifiable NID certificates to agents and nodes, defines a three-tier CA hierarchy with OCSP/CRL revocation, and specifies `IdentFrame`, `TrustFrame`, and `RevokeFrame`. ([NPS-3 NIP](https://github.com/labacacia/NPS/blob/main/spec/NPS-3-NIP.md))

**NOP (Neural Orchestration Protocol)** — The NPS orchestration layer analogous to SMTP + message queues + workflow engines; provides wire-level DAG task dispatch (`TaskFrame`), agent delegation (`DelegateFrame`), K-of-N sync barriers (`SyncFrame`), and streaming progress (`AlignStream`). ([NPS-5 NOP](https://github.com/labacacia/NPS/blob/main/spec/NPS-5-NOP.md))

**NopFrame** — The NCP keepalive/heartbeat frame (0x07, added NCP v0.8); a zero-payload frame (the single byte `0x07`) that either peer MAY send after the handshake to keep an idle connection alive; cadence is driven by `HelloFrame.ping_interval_ms`, with a dead-peer threshold of 3 × interval (`NCP-KEEPALIVE-TIMEOUT` → `NPS-SERVER-TIMEOUT`). ([NPS-1 §4.8](https://github.com/labacacia/NPS/blob/main/spec/NPS-1-NCP.md))

**NwpNativeNodeServer** — An SDK server component that lets Memory and Action Nodes serve `QueryFrame` / `ActionFrame` requests directly over a native `NcpSession` / NCP stream (RFC-0006 length-prefix framing), rather than through a hand-rolled per-frame read/write loop or the HTTP overlay. ([NPS-2 NWP](https://github.com/labacacia/NPS/blob/main/spec/NPS-2-NWP.md), [spec/transport-profile.md](https://github.com/labacacia/NPS/blob/main/spec/transport-profile.md))

**NPT (Neural Processing Token)** — **Deprecated.** Legacy alias for CGN (Cognon); the associated wire field `estimated_npt` was renamed to `cgn_est` in v1.0-alpha.5.2 — do not use in new code or new spec text. ([spec/token-budget.md](https://github.com/labacacia/NPS/blob/main/spec/token-budget.md))

**NWM (Neural Web Manifest)** — A machine-readable JSON manifest served at `GET /.nwm` on every NWP node; declares node type, capabilities, available actions, AnchorFrame reference, trusted CA issuers, and (at AaaS Profile L2+) topology query endpoint availability. ([NPS-2 §4](https://github.com/labacacia/NPS/blob/main/spec/NPS-2-NWP.md))

**NWP (Neural Web Protocol)** — The NPS request/response layer analogous to HTTP; defines how agents access data and invoke operations on Memory, Action, Anchor, Bridge, and Complex nodes using `QueryFrame` (0x10) and `ActionFrame` (0x11). ([NPS-2 NWP](https://github.com/labacacia/NPS/blob/main/spec/NPS-2-NWP.md))

**node_roles** — A self-declared array of node-role tags (`memory` / `action` / `complex` / `anchor` / `bridge`) carried on `AnnounceFrame` (NDP, NPS-CR-0001) and `IdentFrame` (NIP v0.10); it replaced the singular `node_kind` (accepted as an alias through alpha.5 only), and a NIP cert/`IdentFrame` mismatch surfaces as `NIP-CERT-NODE-ROLES-MISMATCH` → `NPS-CLIENT-BAD-FRAME`. ([NPS-4 §3.1](https://github.com/labacacia/NPS/blob/main/spec/NPS-4-NDP.md), [NPS-3 §5.1](https://github.com/labacacia/NPS/blob/main/spec/NPS-3-NIP.md))

---

## O

**OCSP Staple** — The base64url-encoded DER OCSP response carried in `IdentFrame.ocsp_staple` (NIP v0.9), letting a presenter prove its certificate is currently unrevoked without the verifier making a live OCSP query; the Phase 3 flag day at v1.0.0-beta.1 introduces `NIP-OCSP-STAPLE-EXPIRED`. ([NPS-3 §5.1](https://github.com/labacacia/NPS/blob/main/spec/NPS-3-NIP.md))

---

## R

**Reputation Log** — An append-only, Merkle-tree-backed log of NID behavioral incidents (rate-limit violations, revocations, ToS violations, positive attestations, etc.) defined by NPS-RFC-0004; analogous to Certificate Transparency logs, allowing any node to assess both the identity and the historical track record of an agent. ([NPS-RFC-0004](https://github.com/labacacia/NPS/blob/main/spec/rfcs/NPS-RFC-0004-nid-reputation-log.md))

---

## S

**Signed canonical form (AnnounceFrame)** — The normative, cross-SDK-consistent byte scope that the NDP `AnnounceFrame.signature` covers (NDP v0.9 §7.4 / §"Signed scope"): all emitted wire fields *except* `signature`, `health`, `last_seen`, and the `frame` discriminant. Absent or `null` optional fields MUST be omitted (not serialized as JSON `null`); `heartbeat_interval_ms` is signed and is canonicalized to the default `60000` only when absent, while an explicit `0` (disabled) is signed literally. Signature verification MUST precede deduplication, conflict detection, and storage. Aligned identically across all six SDKs, so old per-SDK-divergent signed announcements may fail cross-SDK verification. ([NPS-4 §7.4](https://github.com/labacacia/NPS/blob/main/spec/NPS-4-NDP.md))

**STH (Signed Tree Head)** — A signed commitment to the current state of a Reputation Log's Merkle tree (current tree size + root hash + timestamp), published by a log operator and gossiped between peer logs at a default 30-second interval so that any fork or tampering is detectable via STH divergence. ([NPS-RFC-0004 §4.4–4.5](https://github.com/labacacia/NPS/blob/main/spec/rfcs/NPS-RFC-0004-nid-reputation-log.md))

**SubscribeFrame** — The NWP change-subscription frame (0x12); its formal wire shape was standardized in CR-0006 (NWP v0.13, §13) with `subscription_id` (UUID v4), a QueryFrame-compatible `filter`, `heartbeat_interval_ms`, `max_events`, and an opaque `cursor` for lossless resume, and an optional `type` field that selects reserved namespaces such as `topology.stream`; topology subscriptions require both `topology:read` and `topology:subscribe` capabilities (NWP §12.4). ([NPS-2 §13](https://github.com/labacacia/NPS/blob/main/spec/NPS-2-NWP.md), [NPS-CR-0006](https://github.com/labacacia/NPS-Dev/blob/main/spec/cr/NPS-CR-0006-subscribe-frame.md))

**Suite Version** — The top-level version identifier for an NPS release as a whole (e.g., `v1.0.0-alpha.19`); distinct from individual sub-protocol versions (NCP v0.12, NWP v0.22, NIP v0.15, NDP v0.13, NOP v0.10) which are tracked per spec document. ([NPS-0 §9](https://github.com/labacacia/NPS/blob/main/spec/NPS-0-Overview.md))

---

## T

**Topology Stream** — A real-time subscription feed of cluster membership change events (`member_joined`, `member_left`, `member_updated`) pushed from an Anchor Node as `DiffFrame` messages; subscribed to via a `SubscribeFrame` with `type = "topology.stream"`, mandatory at AaaS Profile L2 and above. ([NPS-2 §12.2](https://github.com/labacacia/NPS/blob/main/spec/NPS-2-NWP.md), [NPS-CR-0002](https://github.com/labacacia/NPS/blob/main/spec/cr/NPS-CR-0002-anchor-topology-queries.md))

---

## Renamed in alpha.5 / alpha.5.2

The table below records field and term renames that affect wire compatibility. During the alpha transition window, parsers encountering old names SHOULD accept them as aliases; new publishers MUST use the new names.

| Old name | New name | Changed in | Scope |
|----------|----------|------------|-------|
| `node_kind` | `node_roles` | v1.0-alpha.5 (NDP v0.6, NPS-CR-0001) | NDP `AnnounceFrame` field; parsers MUST accept `node_kind` as alias through the alpha.5 window |
| `estimated_npt` / NPT | `cgn_est` / CGN (Cognon) | v1.0-alpha.5.2 | Token-budget field on `TopologyEventEnvelope` and `AlignStream`; `NPT` and `estimated_npt` are fully deprecated |
| `Gateway Node` (`"gateway"`) | `Anchor Node` (`"anchor"`) + `Bridge Node` (`"bridge"`) | v1.0-alpha.3 (NPS-CR-0001) | NWP node type — single Gateway role split into stateless cluster entry (Anchor) and protocol translator (Bridge); wire value `"gateway"` is removed; parsers MUST reject it with `NWP-MANIFEST-NODE-TYPE-REMOVED` |

---

*Last reviewed at suite version: v1.0.0-alpha.19*
