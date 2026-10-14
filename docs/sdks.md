# SDKs

> English | [中文版](sdks.cn.md)

Six official SDKs — each implementing all five protocols (NCP + NWP + NIP + NDP + NOP) — aligned to suite release **1.0.0-alpha.18** (published 2026-08-15). Package-manager availability is tracked per ecosystem.

> alpha.18 release note: all six SDKs implement the NPS-CR-0011 / NWP 0.21 stateful LLM context contract — owner-bound context ids, `create` / `append` / `fork` / `reset` / `status` / `release`, compare-and-swap versions, atomic cancellation, NWM 0.2 discovery, and NIP 0.14 `llm:context` authorization — plus official NWP LLM usage telemetry with unary `request_id` correlation and the new `NPS-LIMIT-RESOURCE` code.

> npm note: install `@labacacia/nps-sdk@1.0.0-alpha.18`, or use the `alpha` dist-tag — both resolve to the same release. The unqualified `latest` dist-tag is deliberately still `1.0.0-alpha.7`, so an untagged `npm install @labacacia/nps-sdk` will **not** give you the current release.
>
> Python note: PyPI normalizes the pre-release suffix — install `nps-lib==1.0.0a18`.

---

## Language matrix

| Language | Package | Min version | Repo | Wiki deep-dive |
|----------|---------|-------------|------|----------------|
| .NET       | `LabAcacia.NPS.Core` (+ `.NWP` / `.NIP` / `.NDP` / `.NOP`) | .NET 10     | [NPS-sdk-dotnet](https://github.com/labacacia/NPS-sdk-dotnet) | [Wiki: SDK-dotnet](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-dotnet.md) |
| Python     | `nps-lib`                                                    | 3.11        | [NPS-sdk-py](https://github.com/labacacia/NPS-SDK-Python)         | [Wiki: SDK-Python](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-Python.md) |
| TypeScript | `@labacacia/nps-sdk`                                         | Node 22     | [NPS-sdk-ts](https://github.com/labacacia/NPS-SDK-TypeScript)         | [Wiki: SDK-TypeScript](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-TypeScript.md) |
| Java       | `com.labacacia.nps:nps-java`                                 | Java 21     | [NPS-sdk-java](https://github.com/labacacia/NPS-sdk-java)     | [Wiki: SDK-Java](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-Java.md) |
| Rust       | `nps-sdk`                                                    | Rust stable | [NPS-sdk-rust](https://github.com/labacacia/NPS-sdk-rust)     | [Wiki: SDK-Rust](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-Rust.md) |
| Go         | `github.com/labacacia/NPS-sdk-go`                            | Go 1.23     | [NPS-sdk-go](https://github.com/labacacia/NPS-sdk-go)         | [Wiki: SDK-Go](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-Go.md) |

For install commands, minimal examples, and per-feature coverage tables, see the per-language Wiki pages above or [SDK-Quickstart](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-Quickstart.md) for a language-agnostic walkthrough.

---

## NIP CA Server

Standalone deployable Certificate Authority for the Neural Identity Protocol (NPS-3 §8). Independently versioned from the SDKs since `v1.0.0-alpha.11`; currently published at `v1.0.0-alpha.18` alongside the suite. The signed-CRL and remote-client boundary landed in alpha.16.

| Repo | Stack | Quickstart |
|------|-------|------------|
| [labacacia/nip-ca-server](https://github.com/labacacia/nip-ca-server) | C# / ASP.NET Core 10, PostgreSQL or SQLite, single-Docker | `docker compose up -d` |

For operator guides and embedding options (SQLite vs PostgreSQL) see [Wiki: NIP-CA-Server-Ops](https://github.com/labacacia/NPS/blob/main/docs/handbook/NIP-CA-Server-Ops.md).

---

## NPS Daemons

Reference deployment binaries for the standard three-layer NPS topology, currently published at `v1.0.0-alpha.18` alongside the suite. Native NCP TLS/mTLS and NWP serving landed in alpha.16.

| Repo | Daemons | Quickstart |
|------|---------|------------|
| [labacacia/nps-daemons](https://github.com/labacacia/nps-daemons) | `npsd` (L1, :17433) · `nps-runner` (L1 FaaS) · `nps-ingress` (L2, :8080) · `nps-registry` (L2 NDP, :17436) | `docker compose up -d` |

`nps-ingress` is a process-level Internet ingress daemon name, not the retired
NWP **Gateway Node** logical role. CR-0001 replaced that logical role with
**Anchor Node** and **Bridge Node**.

The Layer-3 trust-anchor daemons (`nps-cloud-ca` and `nps-ledger`) are private under the `innolotus` org and ship publicly with NPS Cloud GA (2027 Q1+). For operator and architecture detail see [Wiki: Operators-QuickStart](https://github.com/labacacia/NPS/blob/main/docs/handbook/Operators-QuickStart.md).

---

## Which SDK should you pick?

| You are… | Suggested SDK |
|----------|---------------|
| Writing an agent that calls NPS nodes | **Python** or **TypeScript** — fastest iteration |
| Building a Memory Node for an existing service | match the service language (.NET / Java / Go for enterprise; Python / TS for startups) |
| Writing a high-throughput orchestrator (NOP) | **Rust** or **Go** |
| Bundling with a React frontend | **TypeScript** (dual ESM + CJS) |
| Shipping to a JVM-heavy environment | **Java 21** |

All SDKs produce wire-identical frames — mix languages freely (e.g. a Python agent calling a Rust Memory Node, orchestrated by a Go NOP server).

---

## Next

- [Overview](overview.md) — what NPS is, why it exists
- [Protocols](protocols.md) — the five layers
- [Roadmap](roadmap.md) — what's shipped and what's next
- [Get Started](get-started.md) — choose your path by audience

---

📖 For tutorials, references, and operator guides, see the [NPS handbook](https://github.com/labacacia/NPS/blob/main/docs/handbook/Home.md).
