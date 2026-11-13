[English](sdks.md) | 中文

# SDK

六个官方 SDK 均已发布套件版本 **1.0.0-alpha.20**，发布于 2026-11-10。各语言仓库提供 API 示例、测试和对应平台的环境说明。

| 语言 | 环境 | 仓库 | 包 |
| --- | --- | --- | --- |
| C# / .NET | .NET 10 | [NPS-SDK-DotNet](https://github.com/labacacia/NPS-SDK-DotNet) | [LabAcacia.NPS.NWP](https://www.nuget.org/packages/LabAcacia.NPS.NWP/1.0.0-alpha.20) 及 NPS 包族 |
| Python | Python 3.11+ | [NPS-SDK-Python](https://github.com/labacacia/NPS-SDK-Python) | [nps-lib](https://pypi.org/project/nps-lib/1.0.0a20/)，导入名 `nps_sdk` |
| TypeScript | Node.js 22+ | [NPS-SDK-TypeScript](https://github.com/labacacia/NPS-SDK-TypeScript) | [@labacacia/nps-sdk](https://www.npmjs.com/package/@labacacia/nps-sdk/v/1.0.0-alpha.20) |
| Java | Java 21+ | [NPS-SDK-Java](https://github.com/labacacia/NPS-SDK-Java) | [com.labacacia.nps:nps-java](https://central.sonatype.com/artifact/com.labacacia.nps/nps-java/1.0.0-alpha.20) |
| Rust | 按 SDK 清单选择 Rust 工具链 | [NPS-SDK-Rust](https://github.com/labacacia/NPS-SDK-Rust) | [nps-sdk](https://crates.io/crates/nps-sdk/1.0.0-alpha.20) |
| Go | Go 1.23+ | [NPS-SDK-Go](https://github.com/labacacia/NPS-SDK-Go) | [github.com/labacacia/NPS-sdk-go](https://pkg.go.dev/github.com/labacacia/NPS-sdk-go@v1.0.0-alpha.20) |

## 在已有项目中安装

```sh
dotnet add package LabAcacia.NPS.NWP --version 1.0.0-alpha.20
python -m pip install 'nps-lib==1.0.0a20'
npm install @labacacia/nps-sdk@1.0.0-alpha.20
cargo add nps-sdk@=1.0.0-alpha.20
go get github.com/labacacia/NPS-sdk-go@v1.0.0-alpha.20
```

选择对应语言的命令执行。Python 的发行包名为 `nps-lib`，导入名为 `nps_sdk`，PyPI 将版本规范化为 `1.0.0a20`。Go 模块路径 `github.com/labacacia/NPS-sdk-go` 的大小写需保持一致。

npm 安装本预发布版时使用上述显式版本或 `alpha` 标签，`latest` 标签按其独立发布通道维护。

Maven 项目添加以下依赖。

```xml
<dependency>
  <groupId>com.labacacia.nps</groupId>
  <artifactId>nps-java</artifactId>
  <version>1.0.0-alpha.20</version>
</dependency>
```

Gradle Kotlin DSL 使用 `implementation("com.labacacia.nps:nps-java:1.0.0-alpha.20")`。

## alpha.20 功能支持

包的发布状态和逐操作的运行时验证分别记录。结果 HTTP 工作流已验证 .NET、Python 结果客户端到 .NET 参考服务的路径。schema 批次原生参考实现使用 .NET，NOP 交接使用本地四 worker 参考配置，NDP 转移使用两种易失节点配置。

[支持台账](qualification/alpha20-support-2026-11-03.cn.md)列出精确操作与传输绑定，[验收报告](plans/2026-11-03-alpha20-experimental-ac-evidence.cn.md)链接对应证据。通用 HTTP 客户端互通及更广泛的跨 SDK 运行时工作列在[路线图](roadmap.cn.md)中。

## 可部署服务

[NPS-Daemons](https://github.com/labacacia/NPS-Daemons) 发布 `npsd`、`nps-runner`、`nps-ingress`、`nps-registry`。[NIP-CA-Server](https://github.com/labacacia/NIP-CA-Server) 发布参考身份授权机构。两个仓库均有 alpha.20 标签和本地构建说明。

内部 Nexus 提供 11 个 .NET 主包，调试符号可从公开的 nuget.org 符号服务器获取。当前 Nexus 安装版本早于符号服务支持版本。

[项目首页](../README.cn.md) · [路线图](roadmap.cn.md) · [协议总览](../spec/NPS-0-Overview.cn.md)
