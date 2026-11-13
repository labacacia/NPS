English | [中文](README.cn.md)

# NPS — Neural Protocol Suite

Protocols and SDKs for Agent communication, data access, identity, discovery and orchestration.

[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)
[![Release](https://img.shields.io/badge/release-v1.0.0--alpha.20-orange.svg)](https://github.com/labacacia/NPS/releases/tag/v1.0.0-alpha.20)

**Latest release — [1.0.0-alpha.20](https://github.com/labacacia/NPS/releases/tag/v1.0.0-alpha.20), published 2026-11-10.** All six language SDKs are published. Alpha.20 adds a bounded experimental data workflow with result resources, scoped authorization, schema batches and local result handoff.

**[SDKs and installation](#sdks) · [Project progress](#project-progress) · [Specifications](#protocol-suite) · [Documentation](#documentation) · [Reference services](#reference-services)**

## Protocol suite

| Protocol | Specification | Responsibility |
| --- | --- | --- |
| **NCP** — Neural Communication Protocol | [0.13](spec/NPS-1-NCP.md) | Frames, encoding, sessions and schema-bound batch transport |
| **NWP** — Neural Web Protocol | [0.23](spec/NPS-2-NWP.md) | Node requests, actions, streaming and result-resource access |
| **NIP** — Neural Identity Protocol | [0.16](spec/NPS-3-NIP.md) | Agent identity, signed requests, permissions and constrained delegation |
| **NDP** — Neural Discovery Protocol | [0.14](spec/NPS-4-NDP.md) | Node discovery, registry queries and state ownership |
| **NOP** — Neural Orchestration Protocol | [0.11](spec/NPS-5-NOP.md) | Task graphs, execution coordination and authorized result handoff |

Start with the [suite overview](spec/NPS-0-Overview.md) and [frame registry](spec/frame-registry.yaml). Protocol document versions are independent of the suite package version.

## SDKs

Choose the language that fits your application. Each repository includes setup instructions, API examples and tests.

| Language | Repository | Published package |
| --- | --- | --- |
| **C# / .NET** | [NPS-SDK-DotNet](https://github.com/labacacia/NPS-SDK-DotNet) | [LabAcacia.NPS.Core](https://www.nuget.org/packages/LabAcacia.NPS.Core/1.0.0-alpha.20) and the NWP/NIP/NDP/NOP package family |
| **Python** | [NPS-SDK-Python](https://github.com/labacacia/NPS-SDK-Python) | [nps-lib 1.0.0a20](https://pypi.org/project/nps-lib/1.0.0a20/), imported as `nps_sdk` |
| **TypeScript** | [NPS-SDK-TypeScript](https://github.com/labacacia/NPS-SDK-TypeScript) | [@labacacia/nps-sdk](https://www.npmjs.com/package/@labacacia/nps-sdk/v/1.0.0-alpha.20) |
| **Java** | [NPS-SDK-Java](https://github.com/labacacia/NPS-SDK-Java) | [com.labacacia.nps:nps-java](https://central.sonatype.com/artifact/com.labacacia.nps/nps-java/1.0.0-alpha.20) |
| **Rust** | [NPS-SDK-Rust](https://github.com/labacacia/NPS-SDK-Rust) | [nps-sdk](https://crates.io/crates/nps-sdk/1.0.0-alpha.20) |
| **Go** | [NPS-SDK-Go](https://github.com/labacacia/NPS-SDK-Go) | [github.com/labacacia/NPS-sdk-go](https://pkg.go.dev/github.com/labacacia/NPS-sdk-go@v1.0.0-alpha.20) |

Install the released version in your project.

```sh
# .NET
dotnet add package LabAcacia.NPS.NWP --version 1.0.0-alpha.20

# Python
python -m pip install 'nps-lib==1.0.0a20'

# TypeScript / Node.js
npm install @labacacia/nps-sdk@1.0.0-alpha.20

# Rust
cargo add nps-sdk@=1.0.0-alpha.20

# Go — preserve the module path's case
go get github.com/labacacia/NPS-sdk-go@v1.0.0-alpha.20
```

For Java, add `com.labacacia.nps:nps-java:1.0.0-alpha.20` to your Maven or Gradle dependencies. The [SDK guide](docs/sdks.md) lists requirements, dependency examples and per-language documentation.

The [support ledger](docs/qualification/alpha20-support-2026-11-03.md) lists the qualified alpha.20 paths. The new result HTTP workflow is qualified for the .NET and Python result clients against the .NET reference server. Other language packages retain their documented protocol/helper coverage.

## Project progress

| Milestone | Status | Current scope |
| --- | --- | --- |
| Five protocol specifications and six SDK repositories | Published | Versioned specifications, packages and language-specific examples |
| Alpha.20 data-workflow experiment | Delivered | Result resources and authorization, .NET schema batches, local NOP handoff and volatile NDP transfer |
| Alpha.20 suite release | Published | Six SDK channels, four daemon sources and the CA server, with GitHub and Gitee releases |
| General NWP HTTP client compatibility | Follow-up | Python client MIME/decoding, the .NET public path and protocol error details |
| Full production runtime and adopter qualification | Planned follow-up | Cross-SDK runtime parity, remote orchestration, durable discovery and integration qualification |
| Beta readiness | Pending evaluation | Gate review and an explicit next-release decision |

The experiment includes real mTLS result workflows at 100, 1,000 and 100,000 rows, JSON/MessagePack schema-batch checks, a four-worker local NOP reference and two volatile NDP transfer configurations. The [acceptance evidence](docs/plans/2026-11-03-alpha20-experimental-ac-evidence.md) records each supported configuration.

The [roadmap](docs/roadmap.md) separates released milestones from follow-up work. Alpha.20 measurements include comparisons with equivalent HTTP gzip and ordinary tools. See the [accepted scope and measured limits](docs/adr/2026-11-03-alpha20-experimental-closeout.md) for the results and their applicability.

## Reference services

The reference deployment has four daemon processes and a separately deployed identity authority.

| Repository | Services |
| --- | --- |
| [NPS-Daemons](https://github.com/labacacia/NPS-Daemons) | `npsd`, `nps-runner`, `nps-ingress`, `nps-registry` |
| [NIP-CA-Server](https://github.com/labacacia/NIP-CA-Server) | NIP certificate authority and management endpoints |

Build and start the four-daemon development stack from the release tag.

```sh
git clone --branch v1.0.0-alpha.20 https://github.com/labacacia/NPS-Daemons.git
cd NPS-Daemons
docker compose up --build -d
```

Follow the [CA repository setup](https://github.com/labacacia/NIP-CA-Server#readme) for the authority's configuration and secrets. Operator guidance is in the [daemon architecture](docs/daemons/architecture.md).

## Documentation

| Start here | What you will find |
| --- | --- |
| [Start Here](START-HERE.md) | Architecture, repository map and review entry points |
| [SDK guide](docs/sdks.md) | Six language repositories, package coordinates and installation |
| [Roadmap](docs/roadmap.md) | Released milestones, current gaps and upcoming decisions |
| [.NET API reference](docs/sdk/dotnet/index.en.md) | Package dependencies, configuration and API guides |
| [Frame model](docs/concepts/frame-model.en.md) | Frame structure, encoding and anchors |
| [NID identity](docs/concepts/nid-identity.en.md) | Agent identifiers, certificates and verification |
| [DAG orchestration](docs/concepts/dag-orchestration.en.md) | Task execution and result aggregation |
| [Alpha.20 support ledger](docs/qualification/alpha20-support-2026-11-03.md) | Qualified operations, bindings and limits |
| [Changelog](CHANGELOG.md) | Release history |
| [Contributing](CONTRIBUTING.md) | Contribution and review workflow |

## Development and related projects

NPS is authored in `NPS-Dev`. This public repository contains the suite specifications, documentation and [version manifest](version.yaml). The six SDKs and service repositories receive their corresponding source through the release ownership tooling.

[NPS-Model-Runtime](https://github.com/labacacia/NPS-Model-Runtime) and [NPS-Paper](https://github.com/labacacia/NPS-Paper) are related projects with independent lifecycles. NPS-Console is an independent operator-console project.

## License and security

NPS is released under [Apache License 2.0](LICENSE). Attribution is recorded in [NOTICE](NOTICE). Report vulnerabilities through the process in [SECURITY.md](SECURITY.md).
