# NPS — 面向 AI Agent 的 Wire Protocol

> **Neural Protocol Suite** — 为 AI Agent 和神经模型量身设计的完整互联网协议栈。
>
> 最新 suite release 1.0.0-alpha.18（2026-08-15 发布）· Apache 2.0 · [English](index.md)
>
> 规范树包含经审查、尚未发布的 alpha.19 候选；下方所有安装命令仍固定到已发布的 alpha.18 package。

---

## 问题

今天的 AI Agent 通过 HTTP、REST、HTML 访问 Web — 这些协议是为**人类浏览器**设计的，而不是为按 token 推理的模型设计的。

| 问题 | 代价 |
|------|------|
| 每次响应重复 Schema | Token 浪费，延迟升高 |
| 没有原生的 Agent 身份 | 外挂的认证，缺失信任链 |
| 语义解析留给 Agent | Prompt 臃肿，幻觉增多 |
| 单次请求模型 | 没有原生的流式 / 任务编排 |

## 答案

NPS 在**协议层面**同时解决这四个问题：

- **一次性 Schema Anchor** — 服务端发布一次 `AnchorFrame`，Agent 用内容寻址 id 引用
- **每一跳都有 Ed25519 身份** — `NipIdentity` 是一等公民，不是附加品
- **帧内语义注解** — 语义在帧里，不在散文里
- **统一的 DAG 任务帧** — 多 Agent 工作流，不必重造 MQ 或 Temporal

---

## 体验一下

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

> Python 说明：PyPI 会归一化预发布后缀，套件的 `1.0.0-alpha.18` 在 PyPI 上发布为 `1.0.0a18`。
>
> npm 说明：请显式指定版本号，或使用 `alpha` dist-tag —— 两者都解析到 `1.0.0-alpha.18`。不带修饰的 `latest` dist-tag 仍刻意停留在 `1.0.0-alpha.7`，因此不加版本号或 tag 直接 `npm install @labacacia/nps-sdk` **不会**装到当前发布版。

---

## 探索

- [**总览**](overview.cn.md) — NPS 是什么、为何存在、面向谁
- [**协议族**](protocols.cn.md) — 五层协议（NCP / NWP / NIP / NDP / NOP）一览
- [**SDK**](sdks.cn.md) — 六种语言的安装与快速开始
- [**手册**](handbook/Home.md) — 教程、运维、示例与贡献者指南
- [**路线图**](roadmap.cn.md) — Phase 0 → Phase 4

---

## 状态

**v1.0.0-alpha.18** — 于 2026-08-15 发布。已发布的 SDK/spec line 跟踪 NCP v0.11、NWP v0.21、NIP v0.14、NDP v0.12、NOP v0.9，在六个 SDK 中交付 NPS-CR-0011 有状态 LLM context —— owner 绑定的 context id、`create` / `append` / `fork` / `reset` / `status` / `release`、compare-and-swap 版本、原子取消、NWM 0.2 发现能力，以及 NIP 0.14 `llm:context` 授权 —— 另外还包含官方 NWP LLM usage 遥测与 unary `request_id` 关联、新增的 `NPS-LIMIT-RESOURCE` 错误码，以及 .NET NativeAOT nullable `UInt64` 修复。**NIP CA Server** 与公开 **NPS Daemons** bundle 保持独立发布，并已随套件同步发布 alpha.18。

---

## 链接

- [GitHub — NPS](https://github.com/labacacia/NPS) · 权威协议规范
- [CONTRIBUTING](https://github.com/labacacia/NPS/blob/main/CONTRIBUTING.md) · 如何提案
- [LICENSE](https://github.com/labacacia/NPS/blob/main/LICENSE) · Apache 2.0

版权所有 © 2026 INNO LOTUS PTY LTD — LabAcacia 开源实验室。

---

📖 教程、参考资料和运维指南，请访问 [NPS 手册](handbook/Home.md)。
