English | [中文版](./NPS-0-Overview.cn.md)

# NPS-0: Neural Protocol Suite — Overview

**Spec Number**: NPS-0  
**Status**: Proposed  
**Version**: 0.4
**Date**: 2026-06-27
**Authors**: Ori Lynn / INNO LOTUS PTY LTD  
**License**: Apache 2.0  

---

## Abstract

NPS (Neural Protocol Suite) comprises five protocols covering AI communication, Web access, identity, node discovery, and multi-agent orchestration. Schema anchoring supports reusable structure at the transport/runtime boundary. Wire bytes, model-visible tokens and provider cost are separate metrics; no universal token-saving percentage follows from anchoring alone.

---

## Table of Contents

1. [Background and Motivation](#1-background-and-motivation)
2. [Design Principles](#2-design-principles)
3. [Protocol Suite Overview](#3-protocol-suite-overview)
4. [Core Concepts](#4-core-concepts)
5. [Addressing](#5-addressing)
6. [Frame Namespace](#6-frame-namespace)
7. [Encoding Tiers](#7-encoding-tiers)
8. [Security Overview](#8-security-overview)
9. [Versioning Policy](#9-versioning-policy)
10. [Relationship to Existing Protocols](#10-relationship-to-existing-protocols)
11. [Roadmap](#11-roadmap)
12. [Appendix A — Quick Reference](#appendix-a--quick-reference)
13. [Appendix B — Glossary](#appendix-b--glossary)
14. [Appendix C — Change Log](#appendix-c--change-log)

---

## 1. Background and Motivation

### 1.1 Problem Statement

Existing Web protocols (HTTP, REST, GraphQL) were designed for human browsers. When AI agents access them, three fundamental problems emerge:

1. **Semantic parsing overhead**: HTML/CSS/JS contains large amounts of presentation-layer content that is meaningless to AI, consuming tokens without carrying information.
2. **Redundant schema transmission**: Structural definitions are re-transmitted in every response, which is especially wasteful under high-frequency AI access.
3. **No concept of agent identity**: Existing HTTP Auth cannot distinguish "AI agent access" from "human access", and offers no way to declare agent capabilities and scope.

### 1.2 Goals

- When an AI accesses a Neural Node, no semantic-parsing layer is required; the response is directly consumable by the model.
- AnchorFrame schema reuse can remove repeated structure on the wire. Model-token effects depend on the final input representation and which data the runtime sends to the model.
- A unified Agent NID (Neural Identity) makes "AI access" vs. "human access" an explicit distinction at the protocol layer.
- Layered and optional: the minimal deployment needs only NCP + NWP; NIP / NDP / NOP are opt-in.

### 1.3 Non-Goals

- NPS does not replace MCP (the Tool-invocation semantic layer); it fills the AI-native node request/response network layer MCP does not cover.
- NPS is not designed as a human-readable API format (except in Overlay mode).
- NPS does not handle model inference itself, only data transport and access control.

### 1.4 Protocol Analogies

| Human Internet | NPS Counterpart | Responsibility |
|---------------|-----------------|----------------|
| Wire Format / Framing | **NCP** | AI-to-AI frame format, encoding tiers, semantic compression |
| HTTP | **NWP** | AI-native node request/response semantics |
| TLS / PKI | **NIP** | Agent identity certificates and trust chain |
| DNS | **NDP** | Global discovery of nodes and agents |
| SMTP / Message Bus | **NOP** | Multi-agent task orchestration |

---

## 2. Design Principles

### P1 — AI-Native by Design
Not a retrofit of existing protocols. Response structure directly matches LLM attention mechanisms; fields carry semantic type annotations so that models can reason directly.

### P2 — Token Economy First
Schema anchoring (AnchorFrame), incremental transmission (DiffFrame), and Cognon estimates help control data exchange. Model tokens, network bytes and provider cost require separate validation.

### P3 — Layered and Optional
Each protocol layer is independently deployable. The minimum configuration is NCP + NWP; NIP / NDP / NOP are introduced only when needed.

### P4 — Web-Standards-Oriented
Frame format, addressing, and manifest protocol are designed toward W3C / IETF standardization, with formal RFC as the long-term goal.

### P5 — Dual-Track Compatibility
- For AI: expose NWP endpoints returning `application/nwp-*` formats.
- For human browsers: Overlay mode still returns HTML, coexisting seamlessly with today's Web.

### P6 — Open Source First, Sustainable Commercially
The core specification and reference implementations are fully open source (LabAcacia). Commercialization is via the NPS Cloud managed service without closing the protocol.

---

## 3. Protocol Suite Overview

```
┌─────────────────────────────────────────────────────────┐
│                     Human Web Layer                      │
│              HTTP · HTML · CSS · JavaScript              │
├─────────────────────────────────────────────────────────┤
│  NOP  Neural Orchestration Protocol                      │
│       Multi-agent orchestration · AlignStream · DAG      │
├─────────────────────────────────────────────────────────┤
│  NDP  Neural Discovery Protocol                          │
│       Global node/agent discovery · resolution · graph   │
├─────────────────────────────────────────────────────────┤
│  NIP  Neural Identity Protocol                           │
│       Agent identity · NID certificates · trust · revoke │
├─────────────────────────────────────────────────────────┤
│  NWP  Neural Web Protocol                                │
│       Memory · Action · Complex · Anchor · Bridge        │
├─────────────────────────────────────────────────────────┤
│  NCP  Neural Communication Protocol                      │
│       Framing · Schema anchoring · diff coding · stream  │
├─────────────────────────────────────────────────────────┤
│                   Transport Layer                        │
│  HTTP mode:   HTTP/1.1 · HTTP/2 · HTTPS                 │
│  Native mode: TCP · QUIC · WebSocket                     │
│                 Unified port :17433                      │
└─────────────────────────────────────────────────────────┘
```

### Dependency Graph

```
NOP  depends on  NCP + NWP + NIP
NDP  depends on  NCP + NIP
NIP  depends on  NCP
NWP  depends on  NCP
NCP  no deps
```

### Transport Modes

NPS supports two transport modes (see [NPS-1-NCP.md §2.2](NPS-1-NCP.md#22-传输模式)):

| Mode | Carrier | Use cases |
|------|---------|-----------|
| **HTTP mode** | NCP frames in HTTP body paired with NWP headers | Overlay deployment, firewall-friendly, recommended for Phase 1 |
| **Native mode** | Direct TCP/QUIC, NCP frame is the on-the-wire format | High performance, low latency, Phase 2+ |

### Unified Port

The whole protocol suite shares **port 17433** by default, with routing based on the frame-type byte. Implementations MAY assign independent ports to individual protocols for isolated deployments.

| Protocol | Frame range | Default port | Optional dedicated port |
|----------|-------------|--------------|-------------------------|
| NCP | 0x01–0x0F | 17433 | — |
| NWP | 0x10–0x1F | 17433 | 17434 |
| NIP | 0x20–0x2F | 17433 | 17435 |
| NDP | 0x30–0x3F | 17433 | 17436 |
| NOP | 0x40–0x4F | 17433 | 17437 |

---

## 4. Core Concepts

### 4.1 Frame

All NPS protocol communication is carried in **frames**. The default frame has a 4-byte fixed header (1 B frame type + 1 B flags + 2 B payload length) plus a variable-length payload. Extended mode (EXT flag) uses an 8-byte header and supports up to 4 GB payloads. See [NPS-1-NCP.md §3](NPS-1-NCP.md#3-帧格式).

### 4.2 AnchorFrame and Schema Deduplication

A Node publishes an AnchorFrame containing the full Schema definition and a SHA-256 `anchor_id`. An Agent can cache it and reference the schema in later exchanges. This reduces repeated schema transmission where that structure would otherwise be resent. If the runtime expands the same data into the same model messages, transport compression alone does not reduce model-input tokens. Compact model-visible representations and runtime-side filtering require separate correctness and provider-usage measurements.

### 4.3 Cognon (CGN) and Token Budget

NPS introduces the **Cognon (CGN)** as a standardized cross-model token accounting unit. An Agent declares its maximum CGN consumption per request via the `X-NWP-Budget` header. The Node determines the calculation method through the tokenizer resolution chain and truncates the response or rejects the request accordingly. See [token-budget.md](./token-budget.md).

### 4.4 Node Types

| Type | Responsibility | Core frames |
|------|----------------|-------------|
| **Memory Node** | Data storage and retrieval | QueryFrame |
| **Action Node** | Operations and side effects | ActionFrame |
| **Complex Node** | Mix of data and operations, may contain sub-node references | QueryFrame + ActionFrame |

### 4.5 Agent Identity (NID)

Each AI agent holds an NID (Neural Identity Descriptor) in the form `urn:nps:agent:{issuer}:{id}`, issued by a NIP CA. The NID carries capability claims and an access scope, which the node enforces at the protocol layer.

---

## 5. Addressing

### 5.1 Node Address

```
nwp://<host>[:<port>]/<node-path>

# Examples (default port 17433)
nwp://api.myapp.com/products          Memory Node
nwp://api.myapp.com/orders/actions    Action Node
nwp://api.myapp.com/ecommerce         Complex Node
```

### 5.2 NID Format

```
urn:nps:<entity-type>:<issuer-domain>:<identifier>

entity-type:  agent | node | org

# Examples
urn:nps:agent:ca.innolotus.com:550e8400-e29b-41d4
urn:nps:node:api.myapp.com:products
urn:nps:org:mycompany.com
```

### 5.3 Protocol Version Identifiers

```
NPS/0.2     Suite version
NCP/0.7     Sub-protocol version
```

---

## 6. Frame Namespace

All NPS frames share a single byte namespace, partitioned by protocol. See [frame-registry.yaml](./frame-registry.yaml) for the full definition.

| Range | Protocol | Frames |
|-------|----------|--------|
| 0x01–0x0F | NCP | AnchorFrame(0x01), DiffFrame(0x02), StreamFrame(0x03), CapsFrame(0x04), AlignFrame(0x05)† |
| 0x10–0x1F | NWP | QueryFrame(0x10), ActionFrame(0x11) |
| 0x20–0x2F | NIP | IdentFrame(0x20), TrustFrame(0x21), RevokeFrame(0x22) |
| 0x30–0x3F | NDP | AnnounceFrame(0x30), ResolveFrame(0x31), GraphFrame(0x32) |
| 0x40–0x4F | NOP | TaskFrame(0x40), DelegateFrame(0x41), SyncFrame(0x42), AlignStream(0x43) |
| 0xF0–0xFF | System | ErrorFrame(0xFE); others reserved |

† AlignFrame(0x05) was marked Deprecated in NCP v0.7 and is replaced by NOP AlignStream(0x43).

---

## 7. Encoding Tiers

Every frame type supports the following encoding tiers, selected via the Flags field of the frame header:

| Tier | Format | Flag | Use cases |
|------|--------|------|-----------|
| Tier-1 | JSON | 0x00 | Development, debugging, compatibility mode |
| Tier-2 | MsgPack (binary) | 0x01 | Production, ~60% size reduction |
| Tier-3 | BinaryVector v1 | 0x02 | Optional vector-heavy payload encoding; `binary_vector.v1` negotiation required; metadata MessagePack + little-endian float32 segments |
| — | Reserved | 0x03 | Reserved |

Defaults: Tier-2 for production, Tier-1 for development.

---

## 8. Security Overview

All NPS communication requires TLS 1.3 by default. The Agent authentication flow:

```
1. Agent → NDP ResolveFrame: resolve target node's physical address
2. Agent → GET /.nwm: read the node manifest's trusted_issuers
3. Agent → Node: send IdentFrame as the connection handshake
4. Node → NIP CA: verify the IdentFrame signature (OCSP or CRL)
5. Node: validate that the Agent's scope covers this node's path
6. Once validated: the Agent issues formal QueryFrame / ActionFrame
```

The detailed security model appears in each sub-protocol's Security Considerations chapter.

---

## 9. Versioning Policy

| Version | Meaning |
|---------|---------|
| `v0.x-draft` | Internal draft; breaking changes allowed |
| `v0.x-alpha` | Public preview; APIs not yet stable |
| `v0.x-beta` | Feature-complete; external testing welcome |
| `v1.0` | Spec frozen; production-ready |

Breaking changes are defined as: frame-format changes, field-semantics changes, or addressing changes. Such changes MUST ship a new Minor or Major version and include a migration guide in `CHANGELOG.md`.

---

## 10. Relationship to Existing Protocols

| Protocol | Position | Relationship to NPS |
|----------|----------|---------------------|
| REST / GraphQL | Human APIs | NWP is the AI-native node request/response replacement |
| MCP | Tool-invocation layer | NPS fills the AI-native node request/response network layer MCP does not cover; an MCP adapter is provided |
| A2A | Agent collaboration | NOP covers similar scenarios; an A2A adapter is provided |

NPS does not replace MCP — it adds an efficiency layer on top. MCP answers "how does an AI invoke a tool"; NPS answers "how does an AI plug into the Internet".

---

## 11. Roadmap

| Phase | Timeline | Goals |
|-------|----------|-------|
| Phase 0 | 2026 Q2 | Unified spec skeleton; public repo release |
| Phase 1 | 2026 Q3 | Core NCP + NWP + NIP implementations; NIP CA Server OSS v0.1 |
| Phase 2 | 2026 Q4 | Complete NDP + NOP; MCP/A2A adapters; TypeScript SDK |
| Phase 3 | 2027 Q1–Q2 | Ecosystem validation; NPS Cloud CA v1.0; industry PoCs |
| Phase 4 | 2027 Q3+ | W3C / IETF standardization; NPS 1.0 freeze |

Full roadmap: [NPS-Roadmap.md](./NPS-Roadmap.md).

---

## Appendix A — Quick Reference

### Port Assignments

| Purpose | Port |
|---------|------|
| NPS unified port (whole suite) | 17433 |
| NIP CA Server API | 17440 |
| NIP CA Admin UI | 8080 |

Optional dedicated per-protocol ports: NWP 17434, NIP 17435, NDP 17436, NOP 17437.

### Content-Type

| Scenario | Content-Type |
|----------|-------------|
| NWP standard response | `application/nwp-capsule` |
| NWP frame data | `application/nwp-frame` |
| NWM manifest | `application/nwp-manifest+json` |

### Key HTTP Headers (HTTP mode)

| Header | Direction | Description |
|--------|-----------|-------------|
| `X-NWP-Agent` | Request | Agent NID |
| `X-NWP-Budget` | Request | CGN budget ceiling |
| `X-NWP-Tokenizer` | Request | Tokenizer identifier the Agent uses |
| `X-NWP-Depth` | Request | Node-graph traversal depth (default 1, max 5) |
| `X-NWP-Schema` | Response | anchor_id of the Schema used |
| `X-NWP-Tokens` | Response | Actual CGN consumed |
| `X-NWP-Tokens-Native` | Response | Native-token consumption (if tokenizer is known) |
| `X-NWP-Tokenizer-Used` | Response | Tokenizer actually applied |
| `X-NWP-Cached` | Response | Whether the cache was hit |

### Reference Documents

| Document | Description |
|----------|-------------|
| [status-codes.md](./status-codes.md) | NPS native status codes and HTTP mapping |
| [token-budget.md](./token-budget.md) | Cognon Budget specification |
| [error-codes.md](./error-codes.md) | Unified error-code namespace |
| [frame-registry.yaml](./frame-registry.yaml) | Machine-readable frame registry |

---

## Appendix B — Glossary

| Term | Definition |
|------|------------|
| **NPS** | Neural Protocol Suite; umbrella name for this protocol family |
| **NCP** | Neural Communication Protocol; framing and communication base layer |
| **NWP** | Neural Web Protocol; AI-native node request/response semantic layer |
| **NIP** | Neural Identity Protocol; Agent identity layer |
| **NDP** | Neural Discovery Protocol; node discovery layer |
| **NOP** | Neural Orchestration Protocol; multi-agent orchestration layer |
| **NID** | Neural Identity Descriptor; unique identity for agents/nodes |
| **NWM** | Neural Web Manifest; machine-readable node manifest |
| **CGN** | Cognon; standardized cross-model token unit |
| **AnchorFrame** | NCP frame type published by a Node for Schema anchoring |
| **ErrorFrame** | The unified NPS error frame (0xFE), shared by all protocol layers |
| **AlignStream** | NOP sub-specification; a directed task stream; replaces NCP AlignFrame |
| **Token Budget** | The maximum CGN consumption the Agent declares for a single request |
| **Trust Federation** | Mechanism for establishing trust chains across organizational CAs |
| **Memory Node** | An NWP node type for data storage and retrieval |
| **Action Node** | An NWP node type for operations and services |
| **Complex Node** | An NWP node combining data and operations; may contain sub-node references |

---

## Appendix C — Change Log

| Version | Date | Changes |
|---------|------|---------|
| 0.4 | 2026-06-27 | NCP v0.9 activates Tier-3 BinaryVector v1 (`binary_vector.v1`) as the optional vector-heavy payload encoding; `0x02` is no longer a reserved/invalid tier when negotiated. |
| 0.2 | 2026-04-12 | Unified port (17433); dual transport modes (HTTP / native); AnchorFrame ownership clarified as Node-published; Cognon (CGN) accounting; NPS status-code system; ErrorFrame (0xFE); encoding Tier-3 marked Reserved; configurable frame size |
| 0.1-draft | 2026-04-10 | Initial draft, integrating the NCP v0.7 design document |

---

*Copyright: LabAcacia / INNO LOTUS PTY LTD · Apache 2.0*
