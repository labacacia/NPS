# Overview

> English | [中文版](overview.cn.md)

## What is NPS?

The **Neural Protocol Suite** is a family of five coordinated protocols that together replace the HTTP/REST stack for AI-native workloads:

| Layer | Name | Analogue |
|-------|------|----------|
| NCP   | Neural Communication Protocol | Wire format / framing (think HTTP/2 frames) |
| NWP   | Neural Web Protocol           | Request / response (think HTTP semantics) |
| NIP   | Neural Identity Protocol      | TLS / PKI for agents |
| NDP   | Neural Discovery Protocol     | DNS for agents |
| NOP   | Neural Orchestration Protocol | SMTP + MQ — multi-agent DAGs |

All five share a single default port — **17433** — and route on a 1-byte frame type. A single connection can carry frames from any layer.

---

## Why not just keep using HTTP?

HTTP and REST assume a **human reads the response**. For an AI agent, the assumptions are wrong in four specific ways:

### 1. Schema repetition

A REST endpoint re-describes its schema implicitly in every response (field names, type hints, nesting). For an agent that queries 100 products, the same field names travel 100 times.

NPS's `AnchorFrame` publishes the schema **once**. Agents reference it by a SHA-256 content-addressed id — `sha256:abc…`. The server can invalidate it or evolve it with a `DiffFrame`.

### 2. Identity as an afterthought

HTTP identity is a patchwork: cookies, OAuth, API keys, mTLS. For a multi-agent workflow where Agent A delegates to Agent B, there is no standard answer for *"does A actually speak for the user?"*

NPS's `IdentFrame` and `TrustFrame` make Ed25519 identity first-class. Every frame can be signed; the `NipCa` issues and revokes identities with a spec'd certificate lifecycle.

### 3. Prose semantics

REST APIs ship OpenAPI schemas that describe **types**, not **meaning**. An agent has to infer that `price` means "the retail price in USD" from context.

NPS schema fields carry `semantic` annotations — e.g. `commerce.price.usd` — drawn from a shared ontology. Agents can resolve intent without prose analysis.

### 4. Single-shot requests

REST is request-response. Streaming is a 2009 bolt-on (SSE / chunked transfer). Multi-agent orchestration is left entirely to frameworks (Temporal, Airflow, LangGraph).

NPS makes streams (`StreamFrame`, `AlignStreamFrame`) and orchestration (`TaskFrame`, `DelegateFrame`, `SyncFrame`) **wire-level primitives**.

---

## Who is NPS for?

| You are building… | NPS helps you by… |
|-------------------|-------------------|
| A knowledge base / API for AI shoppers | Publishing an anchor once → 80% token reduction per query |
| A multi-agent orchestrator | Using `TaskFrame` DAGs with signed delegation chains |
| An agent marketplace | Cryptographic identity + discovery without running your own DNS |
| A compliance-bound service | AaaS Profile L1 / L2 / L3 levels define what "production-ready" means |
| A tool that consumes agent-produced data | Schema-anchored responses instead of LLM-parsed JSON |

NPS is **not** a replacement for LLM APIs (OpenAI, Anthropic). It is the **network** those LLM-driven agents speak when they talk to each other, to data sources, and to each other's tools.

---

## What NPS is not

- Not a new foundation-model architecture
- Not a LangChain-style SDK (NPS is the **wire**, SDKs encode it)
- Not MCP (MCP is a tool protocol for a single LLM; NPS is a network for many agents)
- Not A2A-compatible by default — an `a2a-bridge` adapter is on the roadmap

---

## Next

- [Protocols](protocols.md) — the five layers explained
- [SDKs](sdks.md) — pick your language
- [Roadmap](roadmap.md) — what's shipped and what's next

---

📖 For tutorials, references, and operator guides, see the [NPS handbook](https://github.com/labacacia/NPS/blob/main/docs/handbook/Home.md).
