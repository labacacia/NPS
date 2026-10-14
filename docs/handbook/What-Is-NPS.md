# What Is NPS?

> **Audience:** Newcomers — no prior knowledge of NPS required
> **Status:** ✅ Reviewed for v1.0.0-alpha.19
> **Source-of-truth precedence:** `spec/` documents in [`labacacia/NPS`](https://github.com/labacacia/NPS/tree/main/spec) win over this page if they disagree.

---

## The Problem: HTTP Was Built for Humans

Every HTTP/REST/GraphQL API on the planet was designed around the assumption that a human browser reads the response. When an AI agent replaces that browser, three structural inefficiencies kick in immediately.

**Schema repetition.** A REST endpoint re-transmits its field names, type hints, and nesting structure on every single response. An agent querying a product catalog a hundred times receives the same structural skeleton a hundred times. There is no protocol-level mechanism to say "you already know this shape — here is only the data."

**No agent identity.** HTTP authentication is a patchwork of cookies, API keys, OAuth, and mTLS, none of which have any concept of "this is an AI agent acting on behalf of a user, with these declared capabilities and this bounded scope." When Agent A delegates a sub-task to Agent B, HTTP offers no standard answer to the question: *does A actually speak for the user?*

**Single-request, no orchestration.** REST is request-response. Streaming was bolted on in 2009 via SSE and chunked transfer. Multi-agent coordination is left entirely to application frameworks like LangGraph, Temporal, or Airflow — not the wire protocol.

---

## The Solution: Five Coordinated Protocols

**NPS (Neural Protocol Suite)** is a purpose-built protocol family for AI-native workloads. It can run in an HTTP overlay during adoption or use native NCP transport; it does not require every deployment to discard HTTP on day one. Its design question is: *what would a network protocol look like if AI agents were the primary client?*

The suite has five layers, each with a well-defined analogue in the human internet stack:

```
┌──────────────────────────────────────────────────────────────────┐
│  L3   NOP — Neural Orchestration Protocol                        │
│       Multi-agent DAGs · delegation chains · AlignStream         │
│       Analogue: SMTP + message queues + workflow engines         │
├──────────────────────────────────────────────────────────────────┤
│       NIP — Neural Identity Protocol   NDP — Neural Discovery    │
│  L2   Agent NIDs · certs · trust       Node resolution · graph   │
│       Analogue: TLS / PKI              Analogue: DNS             │
│       ╔══════════════════════════════════════════════════╗       │
│       ║  NWP — Neural Web Protocol                       ║       │
│       ║  Query · Action · Anchor · Bridge · Agent nodes  ║       │
│       ║  Analogue: HTTP semantics                        ║       │
│       ╚══════════════════════════════════════════════════╝       │
├──────────────────────────────────────────────────────────────────┤
│  L1   NCP — Neural Communication Protocol                        │
│       Binary frames · MsgPack/JSON codec · AnchorFrame cache     │
│       Analogue: HTTP/2 frames / wire format                      │
├──────────────────────────────────────────────────────────────────┤
│  Transport                                                        │
│  HTTP mode:   HTTP/1.1 · HTTP/2 · HTTPS  (firewall-friendly)    │
│  Native mode: TCP · QUIC · WebSocket     (low-latency, Phase 2+) │
│               Unified default port  :17433                       │
└──────────────────────────────────────────────────────────────────┘
```

Every protocol shares a single default port (17433) and routes on a 1-byte frame type. A single TCP connection can carry frames from all five layers simultaneously. The minimum viable deployment is just NCP + NWP; NIP, NDP, and NOP are each individually opt-in.

The headline efficiency feature is **AnchorFrame schema deduplication**: a node publishes its data schema once, content-addressed by a SHA-256 `anchor_id`. All subsequent requests and responses carry only that identifier. Measured savings within a single agent session are 30–60% of token consumption.

---

## Who Should Use NPS?

NPS is the right choice if you are building or operating any of the following:

- **A data API consumed by AI agents** — publish an AnchorFrame once and eliminate repeated schema transmission across thousands of agent queries.
- **A multi-agent orchestration platform** — use NOP `TaskFrame` DAGs with cryptographically signed delegation chains instead of building orchestration in application code.
- **An agent marketplace or registry** — NID identity + NDP discovery give agents a globally unique, cryptographically verifiable address without running your own DNS infrastructure.
- **A compliance-bound service** — the AaaS Profile defines L1 / L2 / L3 compliance tiers with concrete conformance tests.
- **A legacy REST/gRPC/MCP service** that needs to become accessible to AI agents — deploy a Bridge Node adapter in front of it.

NPS is **not** a replacement for LLM APIs (Anthropic, OpenAI, etc.) and is **not** a framework like LangChain or AutoGen. It is the **wire protocol** those frameworks speak when their agents communicate with data sources, services, and each other.

---

## Node Types

### Anchor Node

An **Anchor Node** is the stateless cluster entry point for an NPS deployment. It accepts inbound NWP `QueryFrame` and `ActionFrame` requests addressed to the cluster as a whole, authenticates the caller's NID, and dispatches the work into the cluster's NOP orchestration layer. Anchor Nodes optionally maintain a registry of cluster member nodes and, at AaaS Profile L2 and above, must expose that registry via the `topology.snapshot` and `topology.stream` query types. A single physical process can combine the Anchor Node role with Memory Node or other roles.

> Anchor Node was introduced in v1.0-alpha.3 by [NPS-CR-0001](https://github.com/labacacia/NPS/blob/main/spec/cr/NPS-CR-0001-anchor-bridge-split.md), replacing the former `Gateway Node` type.

### Bridge Node

A **Bridge Node** translates in either or both directions between NPS frames and non-NPS external protocols — HTTP/REST, gRPC, MCP (Model Context Protocol), and A2A. Outbound calls use `bridge_target`; inbound adapters expose foreign protocol surfaces and dispatch into NPS. Bridge Nodes are stateless per request and declare direction explicitly in NDP: `bridge_protocols` for NPS → external and `bridge_inbound_protocols` for external → NPS.

The old standalone MCP/A2A/gRPC compatibility ingress packages were last published at v1.0.0-alpha.16 (2026-07-23) — an alpha.17 deprecation release was prepared but never published — and they left the synchronized train at alpha.18. Their maintained replacement is the inbound surface of `NPS.NWP.Bridge`. A hosting library that does not announce `node_roles: ["bridge"]` is a Bridge adapter, not a discoverable Bridge Node.

### Agent Node

An **Agent Node** is an NWP-speaking AI executor — typically an LLM-driven process that both consumes and produces NPS frames. Agent Nodes carry a NIP-issued NID certificate, declare their capabilities in an `IdentFrame` handshake, and participate in NOP DAG executions as Worker Agents. The NID format is `urn:nps:agent:{issuer-domain}:{identifier}`.

### Memory Node

A **Memory Node** owns a data anchor and serves agent queries against it. It is the most common NWP node type — a product catalog, a knowledge base, a vector store, or any structured dataset. It publishes an `AnchorFrame` that agents cache, then returns schema-deduped `CapsFrame` responses to `QueryFrame` requests. An agent that has cached the anchor receives only raw field values, with no repeated structural overhead.

---

## Compared to MCP, A2A, and gRPC

| Dimension | MCP | A2A | gRPC | NPS |
|-----------|-----|-----|------|-----|
| **Primary concern** | Tool invocation by a single LLM | Agent-to-agent collaboration | Strongly-typed RPC | Network protocol for AI agents accessing the internet |
| **Schema transmission** | Per-call or per-session (no content-addressed cache) | Per-call | Proto compiled in; no runtime dedup | One-time `AnchorFrame`; 30–60% token savings per session |
| **Agent identity** | Not defined at wire level | Defined via DID | mTLS / token | First-class `IdentFrame` + NIP CA + Ed25519 NID |
| **Orchestration** | Out-of-band (application layer) | Limited peer messaging | Out-of-band | Wire-level `TaskFrame` DAGs with delegation + sync barriers |
| **Discovery** | Not defined | Not defined | Not defined | NDP — DNS-analogous resolution with signed records |
| **NPS compatibility** | `NPS.NWP.Bridge` inbound adapter | `NPS.NWP.Bridge` inbound adapter | `NPS.NWP.Bridge` inbound adapter | Native |

NPS does not replace MCP — it adds a network layer *beneath* it. MCP answers "how does an LLM invoke a tool"; NPS answers "how does an AI agent access the internet and talk to other agents."

---

## Go Next

- [SDK Quickstart](./SDK-Quickstart.md) — get a working NWP client in your language in under 15 minutes
- [Operator Quickstart Bundle](./Operator-Quickstart-Bundle.md) — deploy the full daemon stack with Docker Compose
- [Specs Index](./Specs-Index.md) — browse the protocol specifications and reference documents

---

*Last reviewed at suite version: v1.0.0-alpha.19*
