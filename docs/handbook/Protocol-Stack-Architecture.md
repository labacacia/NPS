# Protocol Stack Architecture

> **Audience:** Newcomers and protocol designers
> **Status:** ✅ Released alpha.19 architecture
> **Source-of-truth precedence:** `spec/` documents in [`labacacia/NPS`](https://github.com/labacacia/NPS/tree/main/spec) win over this page if they disagree.

This page explains *how* the five NPS layers relate to each other and *why* the boundaries are drawn where they are. For per-protocol reference, see the individual [Protocol-NCP](./Protocol-NCP.md), [Protocol-NWP](./Protocol-NWP.md), [Protocol-NIP](./Protocol-NIP.md), [Protocol-NDP](./Protocol-NDP.md), and [Protocol-NOP](./Protocol-NOP.md) pages.

**Released alpha.19 versions:** NCP 0.12 · NWP 0.22 · NIP 0.15 · NDP 0.13 · NOP 0.10. The release adds the current hardening and daemon debt-closure boundary without changing the five-layer ownership model.

> The five-layer ownership model is unchanged; alpha.19 hardening deltas and
> six-SDK/daemon verification are summarized in
> [Alpha.19 Current Status](./Alpha19-Current-Status.md).

---

## The Five-Layer Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           Human Web Layer                               │
│                  HTTP · HTML · CSS · JavaScript                         │
│              (NPS Overlay mode: coexists here transparently)            │
├─────────────────────────────────────────────────────────────────────────┤
│  L3   NOP — Neural Orchestration Protocol                (0x40–0x4F)   │
│       Multi-agent DAGs · DelegateFrame · SyncFrame · AlignStream        │
│       Depends on: NCP + NWP + NIP                                       │
├───────────────────────────┬─────────────────────────────────────────────┤
│  L2a  NIP                 │  L2b  NDP                                   │
│  Neural Identity Protocol │  Neural Discovery Protocol   (0x30–0x3F)   │
│  (0x20–0x2F)              │  Node/Agent resolution                      │
│  Ed25519 NIDs · CA · CRL  │  AnnounceFrame · ResolveFrame · GraphFrame  │
│  Depends on: NCP          │  Depends on: NCP + NIP                      │
├───────────────────────────┴─────────────────────────────────────────────┤
│  L2   NWP — Neural Web Protocol                          (0x10–0x1F)   │
│       QueryFrame · ActionFrame · SubscribeFrame                         │
│       Memory / Action / Complex / Anchor / Bridge / Agent Nodes         │
│       Depends on: NCP (+ NIP + NDP for full operation)                  │
├─────────────────────────────────────────────────────────────────────────┤
│  L1   NCP — Neural Communication Protocol                (0x01–0x0F)   │
│       AnchorFrame · DiffFrame · StreamFrame · CapsFrame · HelloFrame    │
│       · NopFrame (keepalive)                                            │
│  Framing · encoding tiers (JSON / MsgPack / BinaryVector) · schema dedup│
│       No dependencies                                                   │
├─────────────────────────────────────────────────────────────────────────┤
│  Transport                                                               │
│  ┌──────────────────────────────┐  ┌─────────────────────────────────┐  │
│  │  HTTP mode                   │  │  Native mode                    │  │
│  │  NCP frames in HTTP body     │  │  Raw TCP / QUIC / WebSocket     │  │
│  │  X-NWP-* headers             │  │  NCP frame is on-the-wire unit  │  │
│  │  Overlay / firewall-friendly │  │  Low-latency, Phase 2+          │  │
│  └──────────────────────────────┘  └─────────────────────────────────┘  │
│                    Unified default port  :17433                          │
└─────────────────────────────────────────────────────────────────────────┘
```

**System frame (all layers):** `ErrorFrame (0xFE)` — shared across every protocol layer.

**Dependency chain in spec notation:**
```
NCP  ←  NWP  ←  NIP  ←  NDP
         ↑
NCP + NWP + NIP  ←  NOP
```

The minimum viable deployment is **NCP + NWP** only. NIP, NDP, and NOP are each independently opt-in.

---

## Why NCP Has Two Transport Modes

NCP defines two ways to carry its frames over a network connection. The **frame payload is byte-identical in both modes** — only the carrier differs.

### HTTP mode

In HTTP mode, NCP frames are serialized into the HTTP request/response body (`Content-Type: application/nwp-frame`), and NWP metadata travels in `X-NWP-*` headers. The server distinguishes agent clients from human browsers by checking for the `X-NWP-Agent` header or a `HelloFrame` in the body, then returns `application/nwp-*` content types to agents while continuing to return `text/html` to browsers.

This is called **Overlay mode** and is the recommended deployment path for Phase 1. It requires zero firewall changes, works behind existing HTTP/2 reverse proxies, and allows gradual NPS adoption alongside an existing REST API.

### Native mode

In native mode, the client opens a raw TCP or QUIC connection to port 17433, sends an 8-byte preamble (`NPS/1.0\n`), and immediately begins exchanging NCP frames with no HTTP overhead. The connection lifecycle follows a defined state machine: `CLOSED → ANCHOR_NEGOTIATE → ESTABLISHED → CLOSING → CLOSED`.

Native mode is lower-latency and more efficient for high-frequency agent-to-agent communication, but requires that the connecting agent can reach port 17433 directly. It is the target for Phase 2 deployments.

In native mode, errors are returned via `ErrorFrame (0xFE)`; in HTTP mode, the HTTP status code is set in parallel according to the mapping in `spec/status-codes.md`.

---

## Why NIP and NDP Are L2 Siblings of NWP

A common question is why NIP (identity) and NDP (discovery) are drawn at the same level as NWP rather than as sub-layers beneath or above it.

**NIP is not inside NWP because identity must be cross-protocol.** An `IdentFrame` is relevant to NWP requests, NDP announcements, NOP task delegations, and any future protocol layers. If NIP were a sub-protocol of NWP, NDP would have no standard way to verify signed `AnnounceFrame` records without importing NWP. Keeping NIP as a peer of NWP means the dependency graph stays acyclic: both NWP and NDP depend on NIP independently.

**NDP is not inside NWP because discovery precedes access.** Before an agent can send a `QueryFrame` (NWP), it must know the physical (host, port) for the target `nwp://` URL. That resolution is NDP's job. Making NDP a peer rather than a sub-layer of NWP keeps the resolution concern separate from the access concern and allows independent deployment of NDP resolvers.

**NOP sits above all of L2** because it orchestrates *across* NWP calls (dispatching `ActionFrame` sequences) and *requires* NIP identity verification at every delegation step. It cannot operate without an identity substrate (NIP) and an access protocol (NWP). As of NOP v0.7, L3 runtime execution is integrated via the `nps-runner` daemon lease ([NPS-CR-0007](https://github.com/labacacia/NPS/blob/main/spec/cr/NPS-CR-0007-nop-l3-runtime-integration.md)).

---

## Node Type Catalog

NPS defines six node types at the NWP layer. A single physical process may declare multiple roles via the `node_roles` array in its NDP `AnnounceFrame`.

| Node Type | Role | Primary Frames | Typical Deployment |
|-----------|------|----------------|--------------------|
| **Memory Node** | Data storage and retrieval | `QueryFrame (0x10)` | Product catalogs, knowledge bases, vector stores |
| **Action Node** | Operations with side effects | `ActionFrame (0x11)` | Function endpoints, external API wrappers, queues |
| **Complex Node** | Mixed data + operations; may reference sub-nodes | `QueryFrame + ActionFrame` | Full-service nodes combining read and write |
| **Anchor Node** | Stateless cluster entry point; routes to NOP DAGs | `ActionFrame (0x11)` → `TaskFrame (0x40)` | AaaS gateways, service cluster front doors |
| **Bridge Node** | NPS ↔ external protocol translation (HTTP, gRPC, MCP, A2A) | `ActionFrame (0x11)` (inbound) | Protocol adapters, legacy API bridges |
| **Agent Node** | AI executor; participates in NOP as Worker Agent | `ActionFrame`, `AlignStream (0x43)` | LLM-driven worker processes |

> **Naming note:** `Anchor Node` and `Bridge Node` were introduced by [NPS-CR-0001](https://github.com/labacacia/NPS/blob/main/spec/cr/NPS-CR-0001-anchor-bridge-split.md) in v1.0-alpha.3, replacing the former `Gateway Node` type. The wire value `"gateway"` is removed; parsers MUST reject it with `NWP-MANIFEST-NODE-TYPE-REMOVED`.

---

## Frame-Type Byte Range Table

Every NPS frame is identified by a single leading byte. The byte namespace is partitioned by protocol, allowing a single demultiplexer on port 17433 to route frames without parsing the payload.

| Byte range | Protocol | Assigned frames |
|------------|----------|-----------------|
| `0x01–0x0F` | **NCP** | `AnchorFrame (0x01)`, `DiffFrame (0x02)`, `StreamFrame (0x03)`, `CapsFrame (0x04)`, `AlignFrame (0x05, deprecated)`, `HelloFrame (0x06)`, `NopFrame (0x07)` |
| `0x10–0x1F` | **NWP** | `QueryFrame (0x10)`, `ActionFrame (0x11)`, `SubscribeFrame (0x12)` |
| `0x20–0x2F` | **NIP** | `IdentFrame (0x20)`, `TrustFrame (0x21)`, `RevokeFrame (0x22)` |
| `0x30–0x3F` | **NDP** | `AnnounceFrame (0x30)`, `ResolveFrame (0x31)`, `GraphFrame (0x32)` (§5 topology-snapshot format since NDP v0.8) |
| `0x40–0x4F` | **NOP** | `TaskFrame (0x40)`, `DelegateFrame (0x41)`, `SyncFrame (0x42)`, `AlignStream (0x43)` |
| `0xF0–0xFD`, `0xFF` | Reserved | Reserved for future extension — MUST NOT be assigned without a spec update |
| `0xFE` | **System** | `ErrorFrame` — unified error frame for all protocol layers |

`NopFrame (0x07)` is a zero-payload keepalive/heartbeat frame added in NCP v0.8: either peer MAY send it after the handshake. The ping cadence is negotiated via `HelloFrame.ping_interval_ms` (uint32, `0` = disabled), and a peer is considered dead after 3 × the interval (`NCP-KEEPALIVE-TIMEOUT`, mapped to `NPS-SERVER-TIMEOUT`).

The special byte `0x4E` (ASCII `N`, the first byte of the native-mode preamble `NPS/1.0\n`) is reserved by [NPS-RFC-0001](https://github.com/labacacia/NPS/blob/main/spec/rfcs/NPS-RFC-0001-ncp-connection-preamble.md) and MUST NOT be used as a frame type when encountered as the very first byte of a native-mode connection.

The complete machine-readable registry is at [`spec/frame-registry.yaml`](https://github.com/labacacia/NPS/blob/main/spec/frame-registry.yaml).

---

## Cross-Cutting Concerns

### Error Code Namespace

Every NPS error code follows the pattern `DOMAIN-CATEGORY-DETAIL` (all uppercase, hyphen-separated). The domain prefix identifies which protocol layer issued the error:

| Prefix | Layer |
|--------|-------|
| `NCP-*` | NCP framing errors |
| `NWP-*` | NWP request/response errors |
| `NIP-*` | NIP identity and certificate errors |
| `NDP-*` | NDP discovery and resolution errors |
| `NOP-*` | NOP orchestration and DAG errors |
| `NPS-*` | Suite-level errors (e.g., `NPS-SERVER-UNSUPPORTED`) |

Every protocol error code MUST map to an NPS status code. See [`spec/error-codes.md`](https://github.com/labacacia/NPS/blob/main/spec/error-codes.md) and [`spec/status-codes.md`](https://github.com/labacacia/NPS/blob/main/spec/status-codes.md).

### Unified Port 17433

Port 17433 is the single default port for the entire suite. Frame-type-based routing means a node only needs one open port to serve all five protocols simultaneously. Per-protocol dedicated ports (17434 for NWP, 17435 for NIP, 17436 for NDP, 17437 for NOP) are available for isolated deployments but are not required.

### Encoding Tiers

Every frame type supports the following encoding tiers, selected via the flags byte:

| Tier | Encoding | Flag | Notes |
|------|----------|------|-------|
| Tier-1 | JSON | `0x00` | Development, debugging, overlay interop |
| Tier-2 | MsgPack (binary) | `0x01` | Production default; ~60% size reduction vs JSON |
| Tier-3 | BinaryVector v1 | `0x02` | Optional, negotiated (`binary_vector.v1`); vector-heavy frames — MessagePack metadata + little-endian float32 segments. Activated in NCP v0.9; bound to NWP `QueryFrame` vector search. See [Protocol-NCP](./Protocol-NCP.md#encoding-tiers) |
| — | Reserved | `0x03` | Reserved; no format assigned |

### Security Baseline

All NPS communication MUST use TLS 1.3 by default. The primary signature algorithm is Ed25519, chosen for performance: agents verify signatures at high frequency, and Ed25519 verification is orders of magnitude faster than RSA-2048 at equivalent security levels.

---

## Comparison to HTTP/REST/gRPC Layering

| Concern | HTTP/REST | gRPC | NPS |
|---------|-----------|------|-----|
| **Wire format** | HTTP/1.1 or HTTP/2 frames | HTTP/2 + Protobuf | NCP frames (JSON or MsgPack) |
| **Request/response semantics** | HTTP verbs + URL + body | Generated stub methods | NWP `QueryFrame` + `ActionFrame` |
| **Schema definition** | OpenAPI (external, repeated per response) | `.proto` file (compiled in, not runtime) | `AnchorFrame` (runtime, content-addressed, cached by client) |
| **Identity** | Varies: API key, OAuth, mTLS | mTLS or token | NIP `IdentFrame` + Ed25519 NID; CA-issued certificates |
| **Discovery** | None (hard-coded URLs or service mesh) | None native | NDP (DNS-analogous, signed records, graph traversal) |
| **Streaming** | SSE / chunked transfer (bolt-on) | Bidirectional streaming (first-class) | `StreamFrame` (NCP) + `AlignStream` (NOP) |
| **Multi-agent orchestration** | None (application layer only) | None | NOP `TaskFrame` DAGs (wire-level primitive) |
| **Token metering** | Not applicable | Not applicable | CGN budget headers + backpressure built in |

The key distinction is that HTTP and gRPC are general-purpose RPC mechanisms that happen to be used by AI agents, whereas NPS is designed from the start for the specific access patterns of AI agents: high-frequency queries, schema reuse across requests, first-class cryptographic identity, and coordinated multi-agent execution.

---

## Related Pages

- [What Is NPS](./What-Is-NPS.md) — five-minute orientation for newcomers
- [Glossary](./Glossary.md) — definitions for every term on this page
- [Reference: Frame Registry](./Reference-Frame-Registry.md) — full machine-readable frame table
- [Reference: Error Codes](./Reference-Error-Codes.md) — complete error code namespace
- [Reference: Status Codes](./Reference-Status-Codes.md) — NPS status codes and HTTP mapping
- [Protocol-NCP](./Protocol-NCP.md), [Protocol-NWP](./Protocol-NWP.md), [Protocol-NIP](./Protocol-NIP.md), [Protocol-NDP](./Protocol-NDP.md), [Protocol-NOP](./Protocol-NOP.md) — per-protocol deep dives

---

> Last reviewed at suite version: v1.0.0-alpha.19
>
> Alpha.19 source status reconciled through NPS-Dev PR #115 on 2026-09-05.
