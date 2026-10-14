# Get Started

> English | [中文版](get-started.cn.md)

NPS is a multi-layer protocol suite. Where you start depends on what you're building.

The latest suite release is `1.0.0-alpha.18`, published 2026-08-15. Install
commands and SDK notes remain aligned with that release. The specification tree
contains the reviewed, unreleased alpha.19 candidate; package-manager
availability is tracked per ecosystem.

---

## I'm writing an AI agent or model client

You need an **SDK**. Pick your language and follow the quickstart on the Wiki.

| Language | Install | Wiki quickstart |
|----------|---------|-----------------|
| Python     | `pip install nps-lib==1.0.0a18`                          | [SDK-Python](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-Python.md) |
| TypeScript | `npm install @labacacia/nps-sdk@1.0.0-alpha.18`          | [SDK-TypeScript](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-TypeScript.md) |
| Rust       | `cargo add nps-sdk@=1.0.0-alpha.18`                       | [SDK-Rust](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-Rust.md) |
| Go         | `go get github.com/labacacia/NPS-sdk-go@v1.0.0-alpha.18`  | [SDK-Go](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-Go.md) |
| Java       | `implementation("com.labacacia.nps:nps-java:1.0.0-alpha.18")` | [SDK-Java](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-Java.md) |
| .NET       | `dotnet add package LabAcacia.NPS.Core --version 1.0.0-alpha.18` | [SDK-dotnet](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-dotnet.md) |

> Python note: PyPI normalizes the pre-release suffix, so the suite's `1.0.0-alpha.18` is published as `1.0.0a18`.
>
> npm note: pin the explicit version, or use the `alpha` dist-tag — both resolve to `1.0.0-alpha.18`. The unqualified `latest` dist-tag is deliberately still `1.0.0-alpha.7`, so `npm install @labacacia/nps-sdk` with no version or tag will **not** give you the current release.

---

## I'm deploying an NPS node or infrastructure

You need the **NPS Daemons** bundle (`npsd` + `nps-runner` + `nps-ingress` + `nps-registry`).

`nps-ingress` is a process-level Internet ingress daemon name, not the retired
NWP **Gateway Node** logical role. CR-0001 replaced that logical role with
**Anchor Node** and **Bridge Node**.

→ [Wiki: Operators-QuickStart](https://github.com/labacacia/NPS/blob/main/docs/handbook/Operators-QuickStart.md)

---

## I'm building a protocol integration or bridge

Read the protocol specs (linked from [Protocols](protocols.md)), then pick a compat adapter:

- `compat/mcp-ingress` — MCP → NPS translation
- `compat/a2a-ingress` — A2A → NPS translation
- `compat/grpc-ingress` — gRPC → NPS translation

→ [Wiki: Protocol-Designer-Guide](https://github.com/labacacia/NPS/blob/main/docs/handbook/Protocol-Designer-Guide.md)

---

## I want to understand NPS before writing any code

Start with [Overview](overview.md) (5 min read), then [Protocols](protocols.md) for the five-layer architecture.

---

## I want to contribute to NPS

See [CONTRIBUTING](https://github.com/labacacia/NPS/blob/main/CONTRIBUTING.md) for the process, and [Wiki: Contributors-Guide](https://github.com/labacacia/NPS/blob/main/docs/handbook/Contributors-Guide.md) for conventions.

---

📖 For tutorials, references, and operator guides, see the [NPS handbook](https://github.com/labacacia/NPS/blob/main/docs/handbook/Home.md).
