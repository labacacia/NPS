[English](README.md) | 中文

# NPS — Neural Protocol Suite

面向 Agent 通信、数据访问、身份、发现与编排的协议族和 SDK。

[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)
[![Release](https://img.shields.io/badge/release-v1.0.0--alpha.20-orange.svg)](https://github.com/labacacia/NPS/releases/tag/v1.0.0-alpha.20)

**最新版本为 [1.0.0-alpha.20](https://github.com/labacacia/NPS/releases/tag/v1.0.0-alpha.20)，发布于 2026-11-10。** 六个语言 SDK 均已发布。alpha.20 新增有明确边界的数据工作流实验，覆盖结果资源、细粒度授权、schema 批次与本地结果交接。

**[SDK 与安装](#sdk) · [项目进度](#项目进度) · [协议规范](#协议族) · [文档导航](#文档导航) · [参考服务部署](#参考服务)**

## 协议族

| 协议 | 规范版本 | 职责 |
| --- | --- | --- |
| **NCP** — Neural Communication Protocol | [0.13](spec/NPS-1-NCP.cn.md) | 帧、编码、会话与 schema 绑定的批次传输 |
| **NWP** — Neural Web Protocol | [0.23](spec/NPS-2-NWP.cn.md) | 节点请求、操作、流与结果资源访问 |
| **NIP** — Neural Identity Protocol | [0.16](spec/NPS-3-NIP.cn.md) | Agent 身份、签名请求、权限与受限委托 |
| **NDP** — Neural Discovery Protocol | [0.14](spec/NPS-4-NDP.cn.md) | 节点发现、注册表查询与状态归属 |
| **NOP** — Neural Orchestration Protocol | [0.11](spec/NPS-5-NOP.cn.md) | 任务图、执行协调与授权结果交接 |

从[协议总览](spec/NPS-0-Overview.cn.md)和[帧注册表](spec/frame-registry.yaml)开始阅读。各协议文档版本与套件包版本分别维护。

## SDK

根据应用语言选择 SDK，各仓库均有自己的环境说明、API 示例和测试。

| 语言 | 仓库 | 已发布包 |
| --- | --- | --- |
| **C# / .NET** | [NPS-SDK-DotNet](https://github.com/labacacia/NPS-SDK-DotNet) | [LabAcacia.NPS.Core](https://www.nuget.org/packages/LabAcacia.NPS.Core/1.0.0-alpha.20) 及 NWP/NIP/NDP/NOP 包族 |
| **Python** | [NPS-SDK-Python](https://github.com/labacacia/NPS-SDK-Python) | [nps-lib 1.0.0a20](https://pypi.org/project/nps-lib/1.0.0a20/)，导入名为 `nps_sdk` |
| **TypeScript** | [NPS-SDK-TypeScript](https://github.com/labacacia/NPS-SDK-TypeScript) | [@labacacia/nps-sdk](https://www.npmjs.com/package/@labacacia/nps-sdk/v/1.0.0-alpha.20) |
| **Java** | [NPS-SDK-Java](https://github.com/labacacia/NPS-SDK-Java) | [com.labacacia.nps:nps-java](https://central.sonatype.com/artifact/com.labacacia.nps/nps-java/1.0.0-alpha.20) |
| **Rust** | [NPS-SDK-Rust](https://github.com/labacacia/NPS-SDK-Rust) | [nps-sdk](https://crates.io/crates/nps-sdk/1.0.0-alpha.20) |
| **Go** | [NPS-SDK-Go](https://github.com/labacacia/NPS-SDK-Go) | [github.com/labacacia/NPS-sdk-go](https://pkg.go.dev/github.com/labacacia/NPS-sdk-go@v1.0.0-alpha.20) |

在项目中安装已发布版本。

```sh
# .NET
dotnet add package LabAcacia.NPS.NWP --version 1.0.0-alpha.20

# Python
python -m pip install 'nps-lib==1.0.0a20'

# TypeScript / Node.js
npm install @labacacia/nps-sdk@1.0.0-alpha.20

# Rust
cargo add nps-sdk@=1.0.0-alpha.20

# Go，模块路径需保留大小写
go get github.com/labacacia/NPS-sdk-go@v1.0.0-alpha.20
```

Java 项目使用 Maven 或 Gradle 依赖 `com.labacacia.nps:nps-java:1.0.0-alpha.20`。[SDK 指南](docs/sdks.cn.md)列出环境要求、依赖配置及各语言文档。

[支持台账](docs/qualification/alpha20-support-2026-11-03.cn.md)逐路径列出 alpha.20 的验证范围。新增结果 HTTP 工作流已验证 .NET、Python 结果客户端到 .NET 参考服务的路径，其余语言包按各自文档提供协议与辅助能力。

## 项目进度

| 里程碑 | 状态 | 当前范围 |
| --- | --- | --- |
| 五协议规范与六个 SDK 仓库 | 已发布 | 版本化规范、语言包与对应示例 |
| alpha.20 数据工作流实验 | 已交付 | 结果资源与授权、.NET schema 批次、本地 NOP 交接和易失 NDP 转移 |
| alpha.20 套件发布 | 已发布 | 六 SDK 渠道、四个 daemon 源码及 CA 服务，提供 GitHub 和 Gitee Release |
| 通用 NWP HTTP 客户端互通 | 后续工作 | Python MIME/解码、.NET 公共路径和协议错误详情 |
| 完整生产运行时与采用方验证 | 后续规划 | 跨 SDK 运行时一致性、远程编排、持久化发现和集成资格 |
| beta 准备 | 待评估 | 逐项复核门槛并作出下一版本决定 |

本次实验包含 100、1,000、100,000 行的真实 mTLS 结果工作流、JSON/MessagePack schema 批次、本地四 worker NOP 参考实现及两种易失 NDP 转移配置。[验收证据](docs/plans/2026-11-03-alpha20-experimental-ac-evidence.cn.md)记录了各项支持配置。

[路线图](docs/roadmap.cn.md)列出已交付里程碑和后续工作。alpha.20 保留了与等价 HTTP gzip 及普通工具的对照测量，适用范围和测量结果见[已接受的范围决定](docs/adr/2026-11-03-alpha20-experimental-closeout.cn.md)。

## 参考服务

参考部署由四个 daemon 进程和单独部署的身份授权机构组成。

| 仓库 | 服务 |
| --- | --- |
| [NPS-Daemons](https://github.com/labacacia/NPS-Daemons) | `npsd`、`nps-runner`、`nps-ingress`、`nps-registry` |
| [NIP-CA-Server](https://github.com/labacacia/NIP-CA-Server) | NIP 证书授权机构和管理接口 |

从发布标签构建并启动四个 daemon 的开发栈。

```sh
git clone --branch v1.0.0-alpha.20 https://github.com/labacacia/NPS-Daemons.git
cd NPS-Daemons
docker compose up --build -d
```

CA 的配置和密钥设置见 [CA 仓库说明](https://github.com/labacacia/NIP-CA-Server#readme)，运维资料见[daemon 架构](docs/daemons/architecture.md)。

## 文档导航

| 入口 | 内容 |
| --- | --- |
| [Start Here](START-HERE.md) | 架构、仓库地图和审阅入口 |
| [SDK 指南](docs/sdks.cn.md) | 六语言仓库、包坐标和安装方式 |
| [路线图](docs/roadmap.cn.md) | 已交付里程碑、现有缺口和待定事项 |
| [.NET API 参考](docs/sdk/dotnet/index.md) | 包依赖、配置和 API 指南 |
| [帧模型](docs/concepts/frame-model.md) | 帧结构、编码与 Anchor |
| [NID 身份](docs/concepts/nid-identity.md) | Agent 标识、证书与验证 |
| [DAG 编排](docs/concepts/dag-orchestration.md) | 任务执行与结果聚合 |
| [alpha.20 支持台账](docs/qualification/alpha20-support-2026-11-03.cn.md) | 已验证操作、传输绑定和限制 |
| [变更日志](CHANGELOG.cn.md) | 发布历史 |
| [参与贡献](CONTRIBUTING.cn.md) | 贡献和审阅流程 |

## 开发与相关项目

NPS 的主开发仓库为 `NPS-Dev`。本公开仓库提供套件规范、文档和[版本清单](version.yaml)，六个 SDK 与服务仓库通过发布归属工具接收对应源码。

[NPS-Model-Runtime](https://github.com/labacacia/NPS-Model-Runtime) 和 [NPS-Paper](https://github.com/labacacia/NPS-Paper) 是独立生命周期的相关项目，NPS-Console 是独立的操作控制台项目。

## 许可与安全

NPS 采用 [Apache License 2.0](LICENSE)，署名信息见 [NOTICE](NOTICE)。漏洞反馈请按 [SECURITY.md](SECURITY.md) 中的流程提交。
