English | [中文](sdks.cn.md)

# SDKs

The six official SDKs are published at suite version **1.0.0-alpha.20**, released 2026-11-10. Each SDK repository provides its API examples, tests and platform-specific setup.

| Language | Runtime | Repository | Package |
| --- | --- | --- | --- |
| C# / .NET | .NET 10 | [NPS-SDK-DotNet](https://github.com/labacacia/NPS-SDK-DotNet) | [LabAcacia.NPS.NWP](https://www.nuget.org/packages/LabAcacia.NPS.NWP/1.0.0-alpha.20) and the NPS package family |
| Python | Python 3.11+ | [NPS-SDK-Python](https://github.com/labacacia/NPS-SDK-Python) | [nps-lib](https://pypi.org/project/nps-lib/1.0.0a20/), import `nps_sdk` |
| TypeScript | Node.js 22+ | [NPS-SDK-TypeScript](https://github.com/labacacia/NPS-SDK-TypeScript) | [@labacacia/nps-sdk](https://www.npmjs.com/package/@labacacia/nps-sdk/v/1.0.0-alpha.20) |
| Java | Java 21+ | [NPS-SDK-Java](https://github.com/labacacia/NPS-SDK-Java) | [com.labacacia.nps:nps-java](https://central.sonatype.com/artifact/com.labacacia.nps/nps-java/1.0.0-alpha.20) |
| Rust | Rust toolchain from the SDK manifest | [NPS-SDK-Rust](https://github.com/labacacia/NPS-SDK-Rust) | [nps-sdk](https://crates.io/crates/nps-sdk/1.0.0-alpha.20) |
| Go | Go 1.23+ | [NPS-SDK-Go](https://github.com/labacacia/NPS-SDK-Go) | [github.com/labacacia/NPS-sdk-go](https://pkg.go.dev/github.com/labacacia/NPS-sdk-go@v1.0.0-alpha.20) |

## Install in an existing project

```sh
dotnet add package LabAcacia.NPS.NWP --version 1.0.0-alpha.20
python -m pip install 'nps-lib==1.0.0a20'
npm install @labacacia/nps-sdk@1.0.0-alpha.20
cargo add nps-sdk@=1.0.0-alpha.20
go get github.com/labacacia/NPS-sdk-go@v1.0.0-alpha.20
```

Run the command for your language. The Python distribution name is `nps-lib` and its import name is `nps_sdk`. PyPI normalizes the version to `1.0.0a20`. The Go module uses the case-sensitive path `github.com/labacacia/NPS-sdk-go`.

Use the explicit version above or npm's `alpha` tag for this prerelease. npm's `latest` tag follows its own release channel.

For Maven, add this dependency.

```xml
<dependency>
  <groupId>com.labacacia.nps</groupId>
  <artifactId>nps-java</artifactId>
  <version>1.0.0-alpha.20</version>
</dependency>
```

For Gradle Kotlin DSL, use `implementation("com.labacacia.nps:nps-java:1.0.0-alpha.20")`.

## Alpha.20 feature support

Package publication and per-operation runtime qualification are tracked separately. The result HTTP workflow is qualified for .NET and Python result clients against the .NET reference. The schema-batch native reference is .NET. NOP handoff is a local four-worker reference and NDP transfer uses two volatile-node configurations.

The [support ledger](qualification/alpha20-support-2026-11-03.md) records exact operations and bindings. The [acceptance report](plans/2026-11-03-alpha20-experimental-ac-evidence.md) links their evidence. General HTTP client compatibility and broader cross-SDK runtime work are listed on the [roadmap](roadmap.md).

## Deployable services

[NPS-Daemons](https://github.com/labacacia/NPS-Daemons) publishes `npsd`, `nps-runner`, `nps-ingress` and `nps-registry`. [NIP-CA-Server](https://github.com/labacacia/NIP-CA-Server) publishes the reference identity authority. Both have an alpha.20 release tag and local build instructions.

The internal Nexus feed supplies the 11 .NET primary packages. Debug symbols are available from the public nuget.org symbol server. The installed Nexus version predates symbol-server support.

[Project home](../README.md) · [Roadmap](roadmap.md) · [Protocol overview](../spec/NPS-0-Overview.md)
