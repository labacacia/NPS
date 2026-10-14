# 快速开始

> [English](get-started.md) | 中文版

NPS 是一套多层协议族，从哪里开始取决于你在构建什么。

最新 suite release 是 `1.0.0-alpha.18`，于 2026-08-15 发布。安装命令与 SDK 说明
仍对齐该 release。规范树包含经审查、尚未发布的 alpha.19 候选；各生态包管理器可用性单独跟踪。

---

## 我在编写 AI Agent 或模型客户端

你需要一个 **SDK**，选择你的语言，跟着 Wiki 上的快速入门走。

| 语言 | 安装 | Wiki 快速入门 |
|------|------|--------------|
| Python     | `pip install nps-lib==1.0.0a18`                          | [SDK-Python](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-Python.md) |
| TypeScript | `npm install @labacacia/nps-sdk@1.0.0-alpha.18`          | [SDK-TypeScript](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-TypeScript.md) |
| Rust       | `cargo add nps-sdk@=1.0.0-alpha.18`                       | [SDK-Rust](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-Rust.md) |
| Go         | `go get github.com/labacacia/NPS-sdk-go@v1.0.0-alpha.18`  | [SDK-Go](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-Go.md) |
| Java       | `implementation("com.labacacia.nps:nps-java:1.0.0-alpha.18")` | [SDK-Java](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-Java.md) |
| .NET       | `dotnet add package LabAcacia.NPS.Core --version 1.0.0-alpha.18` | [SDK-dotnet](https://github.com/labacacia/NPS/blob/main/docs/handbook/SDK-dotnet.md) |

> Python 说明：PyPI 会归一化预发布后缀，套件的 `1.0.0-alpha.18` 在 PyPI 上发布为 `1.0.0a18`。
>
> npm 说明：请显式指定版本号，或使用 `alpha` dist-tag —— 两者都解析到 `1.0.0-alpha.18`。不带修饰的 `latest` dist-tag 仍刻意停留在 `1.0.0-alpha.7`，因此不加版本号或 tag 直接 `npm install @labacacia/nps-sdk` **不会**装到当前发布版。

---

## 我在部署 NPS 节点或基础设施

你需要 **NPS Daemons** bundle（`npsd` + `nps-runner` + `nps-ingress` + `nps-registry`）。

`nps-ingress` 是进程级 Internet ingress daemon 名，不是 NWP 中已退役的
**Gateway Node** 逻辑角色。CR-0001 已将该逻辑角色替换为 **Anchor Node**
和 **Bridge Node**。

→ [Wiki: Operators-QuickStart](https://github.com/labacacia/NPS/blob/main/docs/handbook/Operators-QuickStart.md)

---

## 我在开发协议集成或协议桥接器

阅读 [协议族](protocols.cn.md) 页面中链接的协议规范，然后选择对应的兼容适配器：

- `compat/mcp-ingress` — MCP → NPS 翻译
- `compat/a2a-ingress` — A2A → NPS 翻译
- `compat/grpc-ingress` — gRPC → NPS 翻译

→ [Wiki: Protocol-Designer-Guide](https://github.com/labacacia/NPS/blob/main/docs/handbook/Protocol-Designer-Guide.md)

---

## 我想在写代码之前先理解 NPS

从 [总览](overview.cn.md)（5 分钟阅读）开始，然后阅读 [协议族](protocols.cn.md) 了解五层架构。

---

## 我想为 NPS 做贡献

见 [CONTRIBUTING](https://github.com/labacacia/NPS/blob/main/CONTRIBUTING.md) 了解流程，以及 [Wiki: Contributors-Guide](https://github.com/labacacia/NPS/blob/main/docs/handbook/Contributors-Guide.md) 了解惯例。

---

📖 教程、参考资料和运维指南，请访问 [NPS handbook](https://github.com/labacacia/NPS/blob/main/docs/handbook/Home.md)。
