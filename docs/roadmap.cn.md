# 路线图

> [English](roadmap.md) | 中文版

NPS 的路径分为四个 Phase — 从草案规范走向正式标准。最新 suite release 为 **v1.0.0-alpha.18**，于 2026-08-15 发布，在六个 SDK 中交付 NPS-CR-0011 / NWP 0.21 有状态 LLM context 契约 —— owner 绑定的 context id、`create` / `append` / `fork` / `reset` / `status` / `release`、compare-and-swap 版本、原子取消、NWM 0.2 发现能力，以及 NIP 0.14 `llm:context` 授权 —— 另含官方 NWP LLM usage 遥测与 unary `request_id` 关联，以及新增的 `NPS-LIMIT-RESOURCE` 错误码。规范树现已包含经审查、尚未发布的 alpha.19 债务收口候选；可安装产物仍为 alpha.18。

---

## 版本约定

| 版本号 | 含义 |
|--------|------|
| `v0.x-draft`  | 内部草稿，可破坏性变更 |
| `v0.x-alpha`  | 公开预览，API 不稳定 |
| `v0.x-beta`   | 功能完整，欢迎外部测试 |
| `v1.0`        | 规范冻结，生产可用 |

---

## Phase 0 — 规范统一（2026 Q2）— ✅ 完成

建立全套 NPS 规范骨架（NCP / NWP / NIP / NDP / NOP），统一帧命名空间，发布 AaaS 与节点合规性规范，开放公共仓库。

---

## Phase 1 — 核心实现（2026 Q3）— ✅ 已交付

六种参考语言（.NET / Python / TypeScript / Java / Rust / Go）全五协议生产可用，NIP CA Server 六语言参考部署。关键里程碑：

- NPS-RFC-0001（NCP 连接前导码）、NPS-RFC-0003（Agent 身份保证级别）、NPS-CR-0001（Anchor/Bridge Node 拆分）、NPS-CR-0002（Anchor 拓扑查询）——全部交付
- NPS-RFC-0002（X.509 NID + ACME）原型覆盖全六 SDK；类型化远程 CA client 已在 alpha.16 交付
- NPS-RFC-0004（CT 风格 NID 声誉日志）Phase 1+2+3：SQLite + Merkle 树 + STH + 包含证明 + STH gossip 联邦
- `npsd` L1 daemon：子 NID 签发 + 每 NID 收件箱队列
- Token 节约：**45.0 %**（对比 REST）· 线路体积：**63.6 %** 压缩（对比 JSON）

---

## Phase 2 — 生态扩展（2026 Q4）— 🚧 进行中

MCP、A2A、gRPC 生态适配器，Tier-2 MsgPack 生产硬化，参考工具链。

- ✅ MCP、A2A、gRPC ingress 适配器已打包并发布到 Nexus v1.0.0-alpha.16
- ✅ Tier-2 MsgPack 与 Token 节约基准测试已发布
- ✅ NOP Orchestrator 执行多节点 DAG；Claude Desktop 通过 `mcp-ingress` 集成验证
- ✅ `NDP.ResolveFrame` DNS TXT 解析（`nwp://` → 物理端点）—— 全六 SDK 交付
- ✅ native-mode NWP serving helper 与 TC-N1/TC-N2 conformance helper 已在 SDK alpha.16 交付
- ⬜ **NPS Studio**（帧流可视化调试器）+ **NPS Probe**（合规检查 CLI）

---

## Phase 3 — 生态验证（2027 Q1–Q2）

真实场景部署、NPS Cloud CA v1.0 上线，建立事实标准基础。

- ⬜ NPS Cloud CA v1.0 — 多区域 HA，实时 OCSP
- ⬜ LangChain / AutoGen / CrewAI 集成适配包
- ⬜ FinTech 与车联网 PoC
- ⬜ NIP CA Server OSS v1.0（PostgreSQL + Web Admin UI）
- ⬜ Token 节约基准报告（公开发布）
- ⬜ GitHub Stars ≥ 500

---

## Phase 4 — 标准化（2027 Q3 起）

W3C / IETF 正式标准化与 NPS 1.0 规范冻结。

- ⬜ W3C WebAI Community Group 提案
- ⬜ IETF Internet-Draft（NCP + NWP 核心规范）
- ⬜ NPS 1.0 规范冻结
- ⬜ ISO/IEC JTC 1 评估

---

## 下一步

- [总览](overview.cn.md) — NPS 是什么
- [协议族](protocols.cn.md) — 五层协议
- [SDK](sdks.cn.md) — 选一门语言

---

📖 教程、参考资料和运维指南，请访问 [NPS handbook](https://github.com/labacacia/NPS/blob/main/docs/handbook/Home.md)。
