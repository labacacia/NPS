English | [中文版](./README.cn.md)

# NPS — Neural Protocol Suite

> First time here? Start with [START-HERE.md](START-HERE.md) for architecture, security, and review guidance.

> **A complete internet infrastructure protocol suite for the AI era**

[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)
[![Status](https://img.shields.io/badge/status-Phase%201-green.svg)]()
[![Release](https://img.shields.io/badge/release-v1.0.0--alpha.18-orange.svg)](CHANGELOG.md)
[![NCP](https://img.shields.io/badge/NCP-v0.12-5b8cff.svg)]()
[![NWP](https://img.shields.io/badge/NWP-v0.22-4af0b0.svg)]()
[![NIP](https://img.shields.io/badge/NIP-v0.15-7b61ff.svg)]()
[![NDP](https://img.shields.io/badge/NDP-v0.13-f0a050.svg)]()
[![NOP](https://img.shields.io/badge/NOP-v0.10-ff8c42.svg)]()

NPS is a complete web infrastructure protocol suite designed for AI Agents and models. It consists of five sub-protocols covering AI communication, web access, identity, node discovery, and multi-agent orchestration.

> Latest published release: `v1.0.0-alpha.18`, including NWP 0.21 stateful
> LLM context, six-SDK lifecycle parity, and strict-native savings gates.
>
> This branch contains the reviewed, unreleased alpha.19 specification
> candidate: NCP 0.12 / NWP 0.22 / NIP 0.15 / NDP 0.13 / NOP 0.10. Install
> commands and package tables remain pinned to the published alpha.18 line.

---

## Protocol Suite Overview

```
┌──────────────────────────────────────────────────────────────┐
│  NOP  Neural Orchestration Protocol   Multi-Agent Orchestration │
├──────────────────────────────────────────────────────────────┤
│  NDP  Neural Discovery Protocol       Node Discovery          │
├──────────────────────────────────────────────────────────────┤
│  NIP  Neural Identity Protocol        Agent Identity          │
├──────────────────────────────────────────────────────────────┤
│  NWP  Neural Web Protocol             Node Request/Response   │
├──────────────────────────────────────────────────────────────┤
│  NCP  Neural Communication Protocol   AI-to-AI Communication  │
└──────────────────────────────────────────────────────────────┘
```

| Protocol | Analogy | Spec Version | Implementation Status | Port (default / standalone) |
|----------|---------|--------------|-----------------------|-----------------------------|
| **NCP** Neural Communication Protocol | Wire Format | v0.12 | Alpha.19 candidate: deterministic keepalive/close clocks, replay-safe QUIC policy, identity-preserving migration, and two-layer backpressure | 17433 / — |
| **NWP** Neural Web Protocol | Node request/response | v0.22 | Alpha.19 candidate: renewable subscription leases and portable stability/SLA/billing normalization | 17433 / 17434 |
| **NIP** Neural Identity Protocol | TLS / PKI | v0.15 | Alpha.19 candidate: deterministic renewal, revocation freshness/unknown handling, and Phase-3 advisory truth | 17433 / 17435 |
| **NDP** Neural Discovery Protocol | DNS | v0.13 | Alpha.19 candidate: durable sequence/epoch fences and fail-closed restart/partition recovery | 17433 / 17436 |
| **NOP** Neural Orchestration Protocol | SMTP / MQ | v0.10 | Alpha.19 candidate: bounded replay ledger, monotonic result TTL, safe eviction, and deterministic aggregation | 17433 / 17437 |

### Local dev stack

Run the four active open-source daemons from their distribution repository:

```bash
git clone https://github.com/labacacia/NPS-Daemons.git
cd NPS-Daemons
docker compose up --build -d
```

For a self-hosted identity authority, deploy
[`labacacia/NIP-CA-Server`](https://github.com/labacacia/NIP-CA-Server)
separately. Both repositories bind development services conservatively by
default; read their security guidance before changing network exposure.

### Reference daemon deployment

The active reference deployment consists of **four daemon processes plus the
separately deployed NIP CA**. The four daemon binaries are distributed by
[`NPS-Daemons`](https://github.com/labacacia/NPS-Daemons); the CA is
distributed by [`NIP-CA-Server`](https://github.com/labacacia/NIP-CA-Server).

| Layer | Daemon | Port | Status (published alpha.18 baseline) |
|-------|--------|------|------------------|
| 1 (host-local) | [`npsd`](https://github.com/labacacia/NPS-Daemons/tree/main/npsd) | 17433 | Host identity, local protocol state, and inbox |
| 1 (host-local) | [`nps-runner`](https://github.com/labacacia/NPS-Daemons/tree/main/nps-runner) | — | Bounded task execution and durable leases |
| 2 (network entry) | [`nps-ingress`](https://github.com/labacacia/NPS-Daemons/tree/main/nps-ingress) | 8080 / 17443 | TLS/mTLS admission and local NCP proxy |
| 2 (discovery) | [`nps-registry`](https://github.com/labacacia/NPS-Daemons/tree/main/nps-registry) | 17436 | NDP registry and graph resolution |
| 3 (trust anchor) | [`NIP-CA-Server`](https://github.com/labacacia/NIP-CA-Server) | 17435 / 17436 | Certificate issuance, revocation, and management health |

The former NPS Cloud CA and Ledger prototype repositories are archived; they
remain design history, not deployable members of the active release train.

---

## Why NPS?

Existing web protocols were designed for human browsers. When AI Agents access them, three fundamental problems arise:

- **Semantic parsing overhead**: HTML/CSS/JS presentation layers are meaningless to AI and waste large numbers of tokens.
- **Repeated schema transmission**: Every response carries a full structure definition — a significant waste in high-frequency access scenarios.
- **No Agent identity concept**: The protocol layer cannot distinguish AI access from human access, nor declare Agent capabilities or permission scopes.

NPS is designed from the ground up to address these problems through **AnchorFrame schema anchoring**, **Cognon (CGN) standardized metering**, and the **NID identity system** — rebuilding the AI internet infrastructure layer.

---

## Key Features

**Token Economy First**
- **Schema Anchoring**: After the first request, subsequent calls pass only a SHA-256 reference. Typical scenarios reduce token consumption by 30–60%.
- **Cognon (CGN)**: A cross-model standardized token metering unit with budget-aware response trimming (Token Budget).

**Five Neural Node Roles**
- `Memory Node`: Data storage and retrieval (RDS / NoSQL / files / vector databases)
- `Action Node`: Operation and service invocation
- `Complex Node`: Combined data and processing with node-graph traversal (Depth control)
- `Anchor Node`: Cluster entry point and NOP routing control plane
- `Bridge Node`: NPS-to-external dispatch and external-to-NPS server ingress (HTTP/gRPC/MCP/A2A; MCP/A2A inbound first)

**AI-Native Identity**
Every Agent holds a **NID** (Neural Identity Descriptor) in the form `urn:nps:agent:{issuer}:{id}`, issued by a NIP CA. The NID carries capability declarations and access scopes enforced at the protocol layer.

**Unified Port & Dual Transport Mode**
- **Unified Port**: All protocols share port **17433**, routed naturally by frame type code.
- **Dual Transport**: **HTTP mode** (overlay-friendly, firewall-compatible) and **Native mode** (high-performance TCP/QUIC).

---

## Repository Structure

```
nps/
├── spec/                    # Language-agnostic specification (SSoT)
│   ├── NPS-0-Overview.md    # Suite overview v0.4
│   ├── NPS-1-NCP.md         # NCP spec v0.12
│   ├── NPS-2-NWP.md         # NWP spec v0.22
│   ├── NPS-3-NIP.md         # NIP spec v0.15
│   ├── NPS-4-NDP.md         # NDP spec v0.13
│   ├── NPS-5-NOP.md         # NOP spec v0.10
│   ├── frame-registry.yaml  # Machine-readable frame registry v0.15
│   ├── error-codes.md       # Unified error code namespace
│   ├── status-codes.md      # NPS native status codes + HTTP mapping
│   ├── token-budget.md      # CGN metering spec
│   ├── conformance/         # Shared wire vectors and conformance fixtures
│   ├── services/
│   │   └── NPS-AaaS-Profile.md  # AaaS compliance profile v0.2
│   └── rfcs/                # RFC process + 4 drafts (NCP preamble, X.509/ACME NID, assurance levels, reputation log)
├── version.yaml             # Published suite-version oracle; bumped last
├── impl/
│   ├── dotnet/              # C# / .NET 10 reference implementation (includes samples/ + benchmarks/)
│   ├── python/              # Python SDK v1.0.0-alpha.18 candidate
│   ├── typescript/          # TypeScript SDK v1.0.0-alpha.18 candidate
│   ├── java/                # Java SDK v1.0.0-alpha.18 candidate
│   ├── rust/                # Rust SDK v1.0.0-alpha.18 candidate
│   └── go/                  # Go SDK v1.0.0-alpha.18 candidate
├── tools/
│   ├── daemons/                # Six resident services. 4 OSS published as labacacia/nps-daemons bundle (npsd / nps-runner / nps-ingress / nps-registry); 2 cloud daemons published as private labacacia/NPS-Cloud-CA + labacacia/NPS-Ledger
│   ├── nip-ca-server/          # NIP CA Server — C# / ASP.NET Core; published standalone at labacacia/nip-ca-server (subdir example/ holds 5 frozen reference ports)
│   ├── release/                # Release sync scripts (dev → standalone publish repos)
│   └── mirror-to-gitee/        # Gitee mirror sync script (GitHub → Gitee with labacacia URL rewrite)
├── compat/
│   ├── mcp-ingress/          # MCP Ingress v1.0.0-alpha.16 (LabAcacia.McpIngress)
│   ├── a2a-ingress/          # A2A Ingress v1.0.0-alpha.16 (LabAcacia.A2aIngress)
│   └── grpc-ingress/         # gRPC Ingress v1.0.0-alpha.16 (LabAcacia.GrpcIngress)
└── demos/                  # Also published standalone at github.com/labacacia/NPS-examples
    ├── nps-demo/            # End-to-end business demo — NIP identity → AnchorFrame → NOP → DiffFrame
    ├── nwp-graph-walk/      # NWP Complex Node §11 — depth-scoped fanout + X-NWP-Trace cycle detection
    ├── ingress-playground/   # One NWP Action Node fronted simultaneously by MCP + A2A + gRPC
    └── cross-sdk-interop/   # Four language clients (dotnet/python/node/go) diffed against one Memory Node
```

> **Standalone showcase.** The three Tier-1 demos (`nwp-graph-walk`,
> `ingress-playground`, `cross-sdk-interop`) are also published as a
> curated repo at [`labacacia/NPS-examples`](https://github.com/labacacia/NPS-examples)
> ([Gitee mirror](https://gitee.com/labacacia/NPS-examples)). The
> source of truth stays here; the standalone repo exists for
> discoverability and has per-demo READMEs with principle / purpose /
> what-it-demonstrates / captured results structure.

---

## Implementation Status

The table below describes the released `1.0.0-alpha.18` source tree.
The latest published package line is `1.0.0-alpha.18`.

### C# / .NET (`impl/dotnet/`)

| Component | Version | Status | Contents |
|-----------|---------|--------|----------|
| `NPS.Core` | 1.0.0-alpha.18 | Released | Frame codec (MsgPack/JSON), dual-header mode (4B/8B), frame registry, Anchor cache, NativeAOT-safe codecs |
| `NPS.NWP` | 1.0.0-alpha.18 | Released | Memory / Action / Complex / Anchor / Bridge Node middleware; native-mode NWP serving over NCP sessions; `/.nwm`·`/.schema`·`/actions`·`/invoke`·`/query`·`/system.task.*`; graph traversal + X-NWP-Depth + cycle detection; SSRF + idempotency + priority + async task lifecycle |
| `NPS.NIP` | 1.0.0-alpha.18 | Released | CA library (key generation, certificate issuance/revocation, typed remote CA client, OCSP, CRL), `NipIdentVerifier` 6-step identity verification |
| `NPS.NDP` | 1.0.0-alpha.18 | Released | NDP frame types (Announce/Resolve/Graph), in-memory registry (TTL eviction), announce signature validator |
| `NPS.NOP` | 1.0.0-alpha.18 | Released | DAG orchestration engine (condition eval, input mapping, K-of-N sync, retry/backoff) + §8.2 delegation chain depth limit + §8.4 callback SSRF guard and exponential backoff retry |
| `NPS.Conformance` | 1.0.0-alpha.18 | Released | Node L1/L2 conformance case catalog, run manifest model, and CI validation helpers |
| `tools/nip-ca-server` | 1.0.0-alpha.18 | Released | NIP CA Server — C# / ASP.NET Core 10, PostgreSQL, Docker. Published standalone at [`labacacia/nip-ca-server`](https://github.com/labacacia/nip-ca-server) (the only release-tracked impl); 5 reference ports (Python / TypeScript / Java / Rust / Go) frozen at `1.0.0-alpha.11` under `tools/nip-ca-server/example/`. |
| Compat ingresses | 1.0.0-alpha.16 | Last published release (2026-07-23) | MCP Ingress (JSON-RPC 2.0, MCP 2024-11-05), A2A Ingress (Google A2A v0.2), gRPC Ingress (HTTP/2, 4 unary RPCs); an alpha.17 deprecation build was prepared but never published, and the packages left the synchronized train from alpha.18 in favor of `NPS.NWP.Bridge` |
| Daemons | 1.0.0-alpha.18 | Released | Six resident services: `npsd` (L1 minimum), `nps-runner`, `nps-ingress`, `nps-registry`, `nps-cloud-ca`, `nps-ledger` (RFC-0004 in-memory log); see [`docs/daemons/architecture.md`](docs/daemons/architecture.md) |
| Samples | — | ✅ Available | `samples/NPS.Samples.NopDag` — 3-node NOP DAG end-to-end over real HTTP; `demos/nps-demo` — 4-scene business demo (NIP → AnchorFrame → NOP → DiffFrame) |
| Benchmarks | — | ✅ Available | `benchmarks/NPS.Benchmarks.TokenSavings` → **45.0% token saving vs REST** (exceeds Phase 1 ≥30% exit criterion); `benchmarks/NPS.Benchmarks.WireSize` → **63.6% MsgPack vs JSON** (exceeds Phase 2 ≤50% exit criterion) |

.NET SDK test gate: **964 tests** across NPS.Core / NWP / NIP (incl. AssuranceLevel, Reputation, X.509/ACME, revocation, storage, remote CA client) / NDP / NOP / Anchor / Bridge / native NCP / native NWP / conformance / samples / benchmarks; plus **48 frozen compat-ingress tests** (15 mcp + 18 a2a + 15 grpc).

### Python (`impl/python/`)

| Component | Version | Status | Contents |
|-----------|---------|--------|----------|
| `nps-lib` | 1.0.0-alpha.18 | Released | Full client/server protocol surface, asyncio + httpx, Ed25519 signing, 1372 tests, 92.07% coverage. Python import module remains `nps_sdk`. |

### TypeScript (`impl/typescript/`)

| Component | Version | Status | Contents |
|-----------|---------|--------|----------|
| `@labacacia/nps-sdk` | 1.0.0-alpha.18 | Released | Full client/server protocol surface, Node.js 22+, MsgPack + JSON dual encoding, Ed25519 signing, 1171 tests. The latest published `1.0.0-alpha.18` package includes `dist/`. |

### Java (`impl/java/`)

| Component | Version | Status | Contents |
|-----------|---------|--------|----------|
| `nps-java` | 1.0.0-alpha.18 | Released | Full client/server protocol surface, Java 21, MsgPack + JSON dual encoding, Ed25519 built-in signing, AES-256-GCM key encryption, 693 tests |

### Rust (`impl/rust/`)

| Component | Version | Status | Contents |
|-----------|---------|--------|----------|
| `nps-rs` | 1.0.0-alpha.18 | Released | Full client/server protocol surface, Rust stable, MsgPack + JSON dual encoding, Ed25519 signing, AES-256-GCM key encryption, Tokio async, 755 tests |

### Go (`impl/go/`)

| Component | Version | Status | Contents |
|-----------|---------|--------|----------|
| `github.com/labacacia/NPS-sdk-go` | 1.0.0-alpha.18 | Released | Full client/server protocol surface, Go 1.23+, MsgPack + JSON dual encoding, Ed25519 built-in signing, AES-256-GCM key encryption, 683 tests |

---

## Quick Start (C#)

### 1. Register NCP core and NWP services

```csharp
services.AddNpsCore(opt => {
    opt.DefaultTier = EncodingTier.MsgPack;
});

services.AddNwp(opt => {
    opt.DefaultTokenBudget = 1000;
});
```

### 2. Build and encode an NWP query frame

```csharp
var query = new QueryFrame {
    AnchorRef = "sha256:a3f9b2c1...",
    Filter = JsonSerializer.SerializeToElement(new { status = "active" }),
    Limit = 50
};

byte[] wire = codec.Encode(query); // auto-handles 4-byte / 8-byte frame headers
```

---

## Roadmap

**Phase 0 — Spec Unification (Complete)**
- [x] All 5 sub-protocol spec documents at v0.2+ draft
- [x] `frame-registry.yaml` machine-readable frame registry v0.2
- [x] `error-codes.md` / `status-codes.md` error codes with HTTP mapping
- [x] `token-budget.md` CGN metering spec
- [x] `services/NPS-AaaS-Profile.md` AaaS compliance profile v0.1

**Phase 1 — Core Build (In progress, target 2026 Q3)**
- [x] `NPS.Core` C# reference implementation — frame codec library
- [x] `NPS.NWP` C# reference implementation — Memory / Action / Complex / Anchor / Bridge Node middleware
- [x] `NPS.NIP` C# reference implementation — CA library + identity verifier (OCSP / CRL)
- [x] `NPS.NDP` C# reference implementation — frame types + registry + announce validator
- [x] `NPS.NOP` C# reference implementation — full orchestration engine + security hardening
- [x] NIP CA Server OSS v0.1 — **6 language variants** (C# / Python / TypeScript / Java / Rust / Go)
- [x] Python SDK `nps-lib` v1.0.0-alpha.16 (Phase 1) (NCP + NWP + NIP + NDP + NOP, 162 tests; import module `nps_sdk`)
- [x] Token-savings benchmark → 45.0% aggregate saving vs REST (Phase 1 ≥30% exit criterion met)
- [x] NuGet registry publish (`NPS.Core`, `NPS.NWP`, etc.) — 14 packages published to Nexus; 14 symbol packages generated and retained in the GitHub Release bundle
- [ ] PyPI release (`nps-lib`)

**Phase 2 — Ecosystem Expansion (In progress)**
- [x] TypeScript SDK v1.0.0-alpha.16 (Phase 2; supersedes the deprecated npm `1.0.0-alpha.11` which omitted `dist/`; `alpha` dist-tag now resolves to `1.0.0-alpha.16`)
- [x] Java SDK v1.0.0-alpha.16 (Phase 2; nps-java: NCP + NWP + NIP + NDP + NOP, Java 21, Ed25519, 87 tests)
- [x] Rust SDK v1.0.0-alpha.16 (Phase 2; nps-rs: NCP + NWP + NIP + NDP + NOP, Rust stable, Ed25519, 88 tests)
- [x] Go SDK v1.0.0-alpha.16 (Phase 2; github.com/labacacia/NPS-sdk-go: NCP + NWP + NIP + NDP + NOP, Go 1.25+, Ed25519, 75 tests)
- [x] **MCP Ingress** v1.0.0-alpha.16 (`LabAcacia.McpIngress`, MCP 2024-11-05, JSON-RPC 2.0; renamed by NPS-CR-0001)
- [x] **A2A Ingress** v1.0.0-alpha.16 (`LabAcacia.A2aIngress`, Google A2A v0.2; renamed by NPS-CR-0001)
- [x] **gRPC Ingress** v1.0.0-alpha.16 (`LabAcacia.GrpcIngress`, 4 unary RPCs; renamed by NPS-CR-0001)
- [x] **Four active daemon binaries** in [`NPS-Daemons`](https://github.com/labacacia/NPS-Daemons), plus the standalone [`NIP-CA-Server`](https://github.com/labacacia/NIP-CA-Server); retired Cloud CA and Ledger prototypes are archived
- [x] **NPS-RFC-0001** Accepted (NCP connection preamble) — Phase 1 .NET helper
- [x] **NPS-RFC-0003** Accepted (Agent identity assurance levels) — Phase 1 .NET reference types
- [x] **NPS-RFC-0004** Accepted (NID reputation log) — Phase 1 .NET entry types + sign/verify
- [x] **NPS-CR-0001** Implemented (Anchor + Bridge Node split; `compat/*-bridge` → `compat/*-ingress` rename)
- [x] **NPS-CR-0002** Implemented (`topology.snapshot` / `topology.stream` reserved query types on Anchor Node; .NET reference impl + L2 conformance suite — daemon-side adoption deferred)
- [x] 3-node NOP DAG sample (`samples/NPS.Samples.NopDag`) — Phase 2 DAG exit criterion met
- [x] Wire-size benchmark → 63.6% MsgPack-vs-JSON reduction (Phase 2 ≤50% exit criterion met)
- [x] RFC-0001..0004 drafts responding to 2026-04-20 review (NCP preamble / X.509+ACME NID / assurance levels / reputation log)
- [ ] NPS Cloud hosted service
- [ ] Protocol specs promoted to Proposed / Stable status

---

## Documentation Index

### Concept Guides

| Topic | Description |
|-------|-------------|
| [Frame Model](docs/concepts/frame-model.en.md) | NCP frame structure, encoding tiers, flags bitmap, schema anchoring |
| [NID Identity System](docs/concepts/nid-identity.en.md) | NID format, certificate structure, 6-step verification, OCSP, scope wildcards |
| [DAG Orchestration](docs/concepts/dag-orchestration.en.md) | Execution flow, input mapping, K-of-N sync, callbacks and aggregation strategies |
| [Node Types](docs/concepts/node-types.en.md) | Memory / Action / Complex / Anchor / Bridge node comparison |

### .NET SDK Reference

| Package | Description |
|---------|-------------|
| [.NET SDK Index](docs/sdk/dotnet/index.en.md) | Environment requirements, package dependencies, DI registration overview |
| [NPS.Core](docs/sdk/dotnet/nps-core.en.md) | Frame types, codecs, AnchorCache, exception hierarchy |
| [NPS.NWP](docs/sdk/dotnet/nps-nwp.en.md) | QueryFrame, Filter DSL, MemoryNodeMiddleware, NWM manifest |
| [NPS.NIP](docs/sdk/dotnet/nps-nip.en.md) | NipIdentVerifier 6-step verification, NipCaService, NipSigner, CA HTTP routes |
| [NPS.NDP](docs/sdk/dotnet/nps-ndp.en.md) | AnnounceFrame, INdpRegistry, NdpAnnounceValidator |
| [NPS.NOP](docs/sdk/dotnet/nps-nop.en.md) | TaskFrame/DAG model, NopOrchestrator, callback validator, condition/input mapping |

### Compatibility Bridges

| Topic | Description |
|-------|-------------|
| [Compat overview](docs/compat/index.en.md) | When to pick MCP / A2A / gRPC; shared design and non-goals |
| [MCP Ingress deep dive](docs/compat/mcp-ingress.en.md) | 1:N upstream model, tool-name encoding, async lifecycle, header semantics |
| [A2A Ingress deep dive](docs/compat/a2a-ingress.en.md) | 1:1 AgentCard mapping, skill lookup, task state translation, in-memory binding |
| [gRPC Ingress deep dive](docs/compat/grpc-ingress.en.md) | Bytes-passthrough rationale, dual error mapping, multi-language clients |

### Benchmarks

| Report | Result |
|--------|--------|
| [REST vs NWP token savings](docs/benchmarks/token-savings.md) | Aggregate **45.0%** CGN reduction (S1 43.1% / S2 44.0% / S3 54.2%) — exceeds Phase 1 ≥30% exit criterion |
| [Tier-1 JSON vs Tier-2 MsgPack wire size](docs/benchmarks/wire-size.md) | Aggregate **63.6%** byte reduction on steady-state frames — exceeds Phase 2 ≤50% exit criterion |
| [Stateful LLM context savings](docs/benchmarks/llm-context-savings.md) | Strict-native second turn: **22.5%** fewer decoder bytes and **62.4%** fewer evaluated tokens, with role/tool semantic parity and fallback disabled |

### Protocol Specifications

| Document | Description |
|----------|-------------|
| [NPS Overview](spec/NPS-0-Overview.md) | Suite entry point, frame namespace at a glance |
| [NCP Spec](spec/NPS-1-NCP.md) | Frame wire format, encoding tiers, dual-header mode |
| [NWP Spec](spec/NPS-2-NWP.md) | Node request/response semantics, Filter DSL, streaming responses |
| [NIP Spec](spec/NPS-3-NIP.md) | Identity protocol, Ed25519 signing, CRL/OCSP |
| [NDP Spec](spec/NPS-4-NDP.md) | Node discovery, TTL broadcast, graph sync |
| [NOP Spec](spec/NPS-5-NOP.md) | DAG orchestration, delegation chains, K-of-N |
| [Cognon (CGN) Metering](spec/token-budget.md) | Cross-model standardized token metering unit |
| [Error Code Namespace](spec/error-codes.md) | Unified error codes across all protocols |
| [Status Code Mapping](spec/status-codes.md) | NPS native status codes with HTTP mapping |
| [AaaS Compliance Profile](spec/services/NPS-AaaS-Profile.md) | Anchor Node + Bridge Node, Vector Proxy, L1/L2/L3 compliance levels |
| [Daemon architecture](docs/daemons/architecture.md) | Six-daemon, three-layer reference deployment topology |
| [RFC process + drafts](spec/rfcs/README.md) | RFC-0001 NCP preamble · 0002 X.509+ACME NID · 0003 assurance levels · 0004 reputation log |

---

## Attribution

| Output | Owner |
|--------|-------|
| NPS Specification | LabAcacia / INNO LOTUS PTY LTD |
| Reference Implementations (OSS) | LabAcacia |
| NPS Cloud Service | INNO LOTUS PTY LTD |

**LabAcacia** is the open-source lab under INNO LOTUS PTY LTD. Licensed under Apache 2.0.

---

## License

[Apache 2.0](LICENSE) © 2026 INNO LOTUS PTY LTD
