# SDK

> [English](sdks.md) | 中文版

六种官方 SDK，每种均完整实现五层协议（NCP + NWP + NIP + NDP + NOP）。当前已对齐 suite release **1.0.0-alpha.18**（2026-08-15 发布）；各生态包管理器可用性单独跟踪。

> alpha.18 release 说明：六个 SDK 均已实现 NPS-CR-0011 / NWP 0.21 有状态 LLM context 契约 —— owner 绑定的 context id、`create` / `append` / `fork` / `reset` / `status` / `release`、compare-and-swap 版本、原子取消、NWM 0.2 发现能力，以及 NIP 0.14 `llm:context` 授权 —— 另含官方 NWP LLM usage 遥测与 unary `request_id` 关联，以及新增的 `NPS-LIMIT-RESOURCE` 错误码。

> npm 说明：请安装 `@labacacia/nps-sdk@1.0.0-alpha.18`，或使用 `alpha` dist-tag —— 两者解析到同一个版本。不带修饰的 `latest` dist-tag 仍刻意停留在 `1.0.0-alpha.7`，因此不加 tag 直接 `npm install @labacacia/nps-sdk` **不会**装到当前发布版。
>
> Python 说明：PyPI 会归一化预发布后缀 —— 请安装 `nps-lib==1.0.0a18`。

---

## 语言矩阵

| 语言 | 包名 | 最低版本 | 仓库 | Wiki 深度文档 |
|------|------|----------|------|--------------|
| .NET       | `LabAcacia.NPS.Core` (+ `.NWP` / `.NIP` / `.NDP` / `.NOP`) | .NET 10     | [NPS-sdk-dotnet](https://github.com/labacacia/NPS-sdk-dotnet) | [Wiki: SDK-dotnet](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-dotnet.md) |
| Python     | `nps-lib`                                                    | 3.11        | [NPS-sdk-py](https://github.com/labacacia/NPS-SDK-Python)         | [Wiki: SDK-Python](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-Python.md) |
| TypeScript | `@labacacia/nps-sdk`                                         | Node 22     | [NPS-sdk-ts](https://github.com/labacacia/NPS-SDK-TypeScript)         | [Wiki: SDK-TypeScript](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-TypeScript.md) |
| Java       | `com.labacacia.nps:nps-java`                                 | Java 21     | [NPS-sdk-java](https://github.com/labacacia/NPS-sdk-java)     | [Wiki: SDK-Java](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-Java.md) |
| Rust       | `nps-sdk`                                                    | Rust stable | [NPS-sdk-rust](https://github.com/labacacia/NPS-sdk-rust)     | [Wiki: SDK-Rust](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-Rust.md) |
| Go         | `github.com/labacacia/NPS-sdk-go`                            | Go 1.23     | [NPS-sdk-go](https://github.com/labacacia/NPS-sdk-go)         | [Wiki: SDK-Go](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-Go.md) |

安装命令、极简示例、功能覆盖对照表等深度内容，请访问上方各语言 Wiki 页面，或查阅 [SDK-Quickstart](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-Quickstart.md) 获取语言无关的入门教程。

---

## NIP CA Server

可独立部署的 Neural Identity Protocol（NPS-3 §8）证书颁发机构，自 `v1.0.0-alpha.11` 起从 SDK 拆出独立发布；当前已随套件发布到 `v1.0.0-alpha.18`。签名 CRL 与 remote-client 边界在 alpha.16 交付。

| 仓库 | 技术栈 | 快速开始 |
|------|--------|----------|
| [labacacia/nip-ca-server](https://github.com/labacacia/nip-ca-server) | C# / ASP.NET Core 10，PostgreSQL 或 SQLite，单 Docker | `docker compose up -d` |

运维指南及嵌入选项（SQLite vs PostgreSQL）请参阅 [Wiki: NIP-CA-Server-Ops](https://github.com/labacacia/NPS/blob/main/docs/handbook/NIP-CA-Server-Ops.md)。

---

## NPS Daemons

标准三层 NPS 拓扑的参考部署二进制，最新已发布版本为 `v1.0.0-alpha.18`，与套件同步发布。native NCP TLS/mTLS 与 NWP serving 在 alpha.16 交付。

| 仓库 | Daemon | 快速开始 |
|------|--------|----------|
| [labacacia/nps-daemons](https://github.com/labacacia/nps-daemons) | `npsd`（L1，:17433）· `nps-runner`（L1 FaaS）· `nps-ingress`（L2，:8080）· `nps-registry`（L2 NDP，:17436）| `docker compose up -d` |

`nps-ingress` 是进程级 Internet ingress daemon 名，不是 NWP 中已退役的
**Gateway Node** 逻辑角色。CR-0001 已将该逻辑角色替换为 **Anchor Node**
和 **Bridge Node**。

Layer-3 信任锚 daemon（`nps-cloud-ca` 和 `nps-ledger`）在 `innolotus` 组织私有仓库，随 NPS Cloud GA 公开发布（2027 Q1+）。运维和架构详情请参阅 [Wiki: Operators-QuickStart](https://github.com/labacacia/NPS/blob/main/docs/handbook/Operators-QuickStart.md)。

---

## 如何选择 SDK？

| 你在做… | 建议使用 |
|---------|---------|
| 编写调用 NPS 节点的 Agent | **Python** 或 **TypeScript** — 迭代最快 |
| 为已有服务构建 Memory Node | 跟服务语言匹配（企业级用 .NET / Java / Go；初创用 Python / TS） |
| 编写高吞吐编排器（NOP） | **Rust** 或 **Go** |
| 与 React 前端打包 | **TypeScript**（双 ESM + CJS） |
| JVM 环境 | **Java 21** |

所有 SDK 产出线缆上完全一致的帧，可自由混搭语言（如 Python Agent 调用 Rust Memory Node，由 Go NOP 服务编排）。

---

## 下一步

- [总览](overview.cn.md) — NPS 是什么，为何存在
- [协议族](protocols.cn.md) — 五层协议
- [路线图](roadmap.cn.md) — 已发布与待发布
- [快速开始](get-started.cn.md) — 按受众选择路径

---

📖 教程、参考资料和运维指南，请访问 [NPS handbook](https://github.com/labacacia/NPS/blob/main/docs/handbook/Home.md)。
