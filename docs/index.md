# NPS — A Wire Protocol for AI Agents

> **Neural Protocol Suite** — a complete internet protocol stack purpose-built for AI agents and neural models.
>
> Latest suite release 1.0.0-alpha.18 (2026-08-15) · Apache 2.0 · [中文版](index.cn.md)
>
> The specification tree contains the reviewed, unreleased alpha.19 candidate;
> all install commands below remain pinned to the published alpha.18 packages.

---

## The problem

Today's AI agents consume the web through HTTP, REST, and HTML — protocols designed for **human browsers**, not for models reasoning at token scale.

| Problem | Cost |
|---------|------|
| Schema repeats on every response | Token waste, latency |
| No native agent identity | Bolted-on auth, no trust chain |
| Semantic interpretation left to the agent | Prompt bloat, hallucinations |
| Single-request model | No native streaming or task orchestration |

## The answer

NPS solves all four **at the wire level**:

- **One-time schema anchors** — servers publish an `AnchorFrame` once; agents reference it by content-addressed id
- **Ed25519 identity on every hop** — `NipIdentity` is first-class, not an afterthought
- **Semantic annotations in the frame itself** — not in prose
- **Unified DAG task frame** — orchestrate multi-agent workflows without re-inventing MQ or Temporal

---

## Try it

```bash
# Python
pip install nps-lib==1.0.0a18

# TypeScript
npm install @labacacia/nps-sdk@1.0.0-alpha.18

# Rust
cargo add nps-sdk@=1.0.0-alpha.18

# Go
go get github.com/labacacia/NPS-sdk-go@v1.0.0-alpha.18

# Java (Gradle)
implementation("com.labacacia.nps:nps-java:1.0.0-alpha.18")

# .NET
dotnet add package LabAcacia.NPS.Core --version 1.0.0-alpha.18
```

> Python note: PyPI normalizes the pre-release suffix, so the suite's `1.0.0-alpha.18` is published as `1.0.0a18`.
>
> npm note: pin the explicit version, or use the `alpha` dist-tag — both resolve to `1.0.0-alpha.18`. The unqualified `latest` dist-tag is deliberately still `1.0.0-alpha.7`, so `npm install @labacacia/nps-sdk` with no version or tag will **not** give you the current release.

---

## Explore

- [**Overview**](overview.md) — what NPS is, why it exists, who it's for
- [**Protocols**](protocols.md) — the five layers (NCP / NWP / NIP / NDP / NOP) at a glance
- [**SDKs**](sdks.md) — install and quick-start for all six languages
- [**Handbook**](handbook/Home.md) — tutorials, operations, examples, and contributor guides
- [**Roadmap**](roadmap.md) — Phase 0 → Phase 4

---

## Status

**v1.0.0-alpha.18** — released 2026-08-15. The published SDK/spec line tracks NCP v0.11, NWP v0.21, NIP v0.14, NDP v0.12, and NOP v0.9. It delivers NPS-CR-0011 stateful LLM context across all six SDKs — owner-bound context ids, `create` / `append` / `fork` / `reset` / `status` / `release`, compare-and-swap versions, atomic cancellation, NWM 0.2 discovery, and NIP 0.14 `llm:context` authorization — plus official NWP LLM usage telemetry with unary `request_id` correlation, the new `NPS-LIMIT-RESOURCE` code, and a .NET NativeAOT nullable-`UInt64` fix. **NIP CA Server** and the public **NPS Daemons** bundle are independently published, and ship alpha.18 alongside the suite.

---

## Links

- [GitHub — NPS](https://github.com/labacacia/NPS) · the protocol specifications (authoritative)
- [CONTRIBUTING](https://github.com/labacacia/NPS/blob/main/CONTRIBUTING.md) · how to propose changes
- [LICENSE](https://github.com/labacacia/NPS/blob/main/LICENSE) · Apache 2.0

Copyright 2026 INNO LOTUS PTY LTD — LabAcacia Open Source Lab.

---

📖 For tutorials, references, and operator guides, see the [NPS handbook](handbook/Home.md).
