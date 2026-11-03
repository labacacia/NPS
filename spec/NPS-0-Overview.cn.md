[English Version](./NPS-0-Overview.md) | 中文版

# NPS-0: Neural Protocol Suite — Overview

**Spec Number**: NPS-0  
**Status**: Proposed  
**Version**: 0.4
**Date**: 2026-06-27
**Authors**: Ori Lynn / INNO LOTUS PTY LTD  
**License**: Apache 2.0  

---

## Abstract

NPS（Neural Protocol Suite）由五个协议组成，覆盖 AI 通信、Web 访问、身份认证、节点发现与多 Agent 编排。Schema 锚定支持传输/运行时边界上的结构复用。网络字节、模型可见 token 和供应商费用是不同指标，不能仅凭锚定承诺固定 token 节省百分比。

---

## 目录

1. [背景与动机](#1-背景与动机)
2. [设计原则](#2-设计原则)
3. [协议族总览](#3-协议族总览)
4. [核心概念](#4-核心概念)
5. [地址规范](#5-地址规范)
6. [帧命名空间](#6-帧命名空间)
7. [编码层级](#7-编码层级)
8. [安全概述](#8-安全概述)
9. [版本策略](#9-版本策略)
10. [与现有协议的关系](#10-与现有协议的关系)
11. [路线图](#11-路线图)
12. [附录 A — 快速参考](#附录-a--快速参考)
13. [附录 B — 术语表](#附录-b--术语表)
14. [附录 C — 变更历史](#附录-c--变更历史)

---

## 1. 背景与动机

### 1.1 问题陈述

现有 Web 协议（HTTP、REST、GraphQL）为人类浏览器设计，AI Agent 访问时存在三个根本性问题：

1. **语义解析开销**：HTML/CSS/JS 中大量表现层内容对 AI 无意义，消耗 token 却不携带信息。
2. **Schema 重复传输**：每次响应重复传输结构定义，在高频 AI 访问场景中浪费显著。
3. **无 Agent 身份概念**：现有 HTTP Auth 无法区分"AI Agent 访问"与"人类访问"，无法声明 Agent 能力和权限范围。

### 1.2 目标

- AI 访问神经节点时，无需语义解析层，响应内容直接可被模型理解
- AnchorFrame 复用可减少传输中的重复结构；模型 token 收益取决于最终输入表达及运行时实际送入模型的数据
- 统一的 Agent NID（神经身份），将"AI 访问"与"人类访问"在协议层明确区分
- 分层可选：最小部署仅需 NCP + NWP，NIP / NDP / NOP 按需引入

### 1.3 非目标

- NPS 不替代 MCP（Tool 调用语义层），而是填补 MCP 没有覆盖的 AI-native 节点请求/响应网络层
- NPS 不设计为人类可读的 API 格式（Overlay 模式除外）
- NPS 不处理模型推理本身，只处理数据传输与访问控制

### 1.4 协议类比

| 人类互联网 | NPS 对应 | 职责 |
|-----------|---------|------|
| Wire Format / Framing | **NCP** | AI 间帧格式、编码层级与语义压缩 |
| HTTP | **NWP** | AI-native 节点请求/响应语义 |
| TLS / PKI | **NIP** | Agent 身份证书与信任链 |
| DNS | **NDP** | 节点与 Agent 的全网发现 |
| SMTP / 消息总线 | **NOP** | 多 Agent 任务编排 |

---

## 2. 设计原则

### P1 — AI-Native 从零设计
非改造现有协议，响应结构直接对应 LLM 注意力机制，字段携带语义类型标注，模型可直接推理。

### P2 — Token Economy 第一
帧级 Schema 锚定（AnchorFrame）、增量传输（DiffFrame）和 Cognon 估算帮助控制数据交换；模型 token、网络字节和费用分别验证。

### P3 — 分层可选
各协议层独立可部署。最小配置为 NCP + NWP；NIP / NDP / NOP 视场景需求引入。

### P4 — 面向 Web 标准
帧格式、地址规范、清单协议均面向 W3C / IETF 标准化设计，长期目标为正式 RFC。

### P5 — 双轨兼容
- 对 AI：暴露 NWP 接口，返回 `application/nwp-*` 格式
- 对人类浏览器：Overlay 模式下正常返回 HTML，与现有 Web 无缝共存

### P6 — 开源优先，商业可持续
核心规范和参考实现完全开源（LabAcacia），商业化通过 NPS Cloud 托管服务实现，不封闭协议。

---

## 3. 协议族总览

```
┌─────────────────────────────────────────────────────────┐
│                     Human Web Layer                      │
│              HTTP · HTML · CSS · JavaScript              │
├─────────────────────────────────────────────────────────┤
│  NOP  Neural Orchestration Protocol                      │
│       多 Agent 任务编排 · AlignStream · DAG 任务图       │
├─────────────────────────────────────────────────────────┤
│  NDP  Neural Discovery Protocol                          │
│       节点与 Agent 全网发现 · 地址解析 · 图谱订阅        │
├─────────────────────────────────────────────────────────┤
│  NIP  Neural Identity Protocol                           │
│       Agent 身份 · NID 证书 · 信任链 · 吊销              │
├─────────────────────────────────────────────────────────┤
│  NWP  Neural Web Protocol                                │
│       Memory · Action · Complex · Anchor · Bridge        │
├─────────────────────────────────────────────────────────┤
│  NCP  Neural Communication Protocol                      │
│       帧格式 · Schema 锚定 · 增量编码 · 流式             │
├─────────────────────────────────────────────────────────┤
│                   Transport Layer                        │
│  HTTP 模式: HTTP/1.1 · HTTP/2 · HTTPS                   │
│  原生模式: TCP · QUIC · WebSocket                        │
│                 统一端口 :17433                           │
└─────────────────────────────────────────────────────────┘
```

### 协议依赖关系

```
NOP  依赖  NCP + NWP + NIP
NDP  依赖  NCP + NIP
NIP  依赖  NCP
NWP  依赖  NCP
NCP  无依赖
```

### 传输模式

NPS 支持两种传输模式（详见 [NPS-1-NCP.cn.md §2.2](NPS-1-NCP.cn.md#22-传输模式)）：

| 模式 | 承载 | 适用场景 |
|------|------|---------|
| **HTTP 模式** | NCP 帧在 HTTP Body 中，配合 NWP 头 | Overlay 部署、防火墙友好、Phase 1 推荐 |
| **原生模式** | TCP/QUIC 直连，NCP 帧为线上格式 | 高性能、低延迟、Phase 2+ |

### 统一端口

全协议族默认共用 **端口 17433**，通过帧类型码路由。实现 MAY 为各协议分配独立端口用于隔离部署。

| 协议 | 帧类型范围 | 默认端口 | 独立端口（可选） |
|------|-----------|---------|----------------|
| NCP | 0x01–0x0F | 17433 | — |
| NWP | 0x10–0x1F | 17433 | 17434 |
| NIP | 0x20–0x2F | 17433 | 17435 |
| NDP | 0x30–0x3F | 17433 | 17436 |
| NOP | 0x40–0x4F | 17433 | 17437 |

---

## 4. 核心概念

### 4.1 帧（Frame）

NPS 所有协议通信以**帧**为基本单元。默认帧含 4 字节固定头（帧类型 1B + Flags 1B + Payload 长度 2B）和可变长度 Payload。扩展模式（EXT 标志位）支持 8 字节帧头，Payload 最大 4GB。详见 [NPS-1-NCP.cn.md §3](NPS-1-NCP.cn.md#3-帧格式)。

### 4.2 AnchorFrame 与 Schema 去重

Node 发布 AnchorFrame，包含完整 Schema 定义和 SHA-256 `anchor_id`。Agent 缓存后可在后续交换中引用，减少原本会重复发送的结构。若运行时仍把相同数据展开成相同模型消息，仅压缩传输不会减少模型输入 token。紧凑的模型可见表达和运行时筛选需要分别验证正确率及供应商 usage。

### 4.3 Cognon（CGN）与 Token Budget

NPS 引入 **Cognon（CGN）** 作为跨模型的标准化 token 计量单位。Agent 通过 `X-NWP-Budget` 头声明本次请求最大 CGN 消耗上限。Node 按 tokenizer 解析链确定计算方式，据此裁剪响应或拒绝超预算请求。详见 [token-budget.cn.md](token-budget.cn.md)。

### 4.4 节点类型（Node Types）

| 类型 | 职责 | 核心帧 |
|------|------|--------|
| **Memory Node** | 数据存储与检索 | QueryFrame |
| **Action Node** | 操作执行与副作用 | ActionFrame |
| **Complex Node** | 混合数据与操作，含子节点引用 | QueryFrame + ActionFrame |

### 4.5 Agent 身份（NID）

每个 AI Agent 持有 NID（Neural Identity Descriptor），格式为 `urn:nps:agent:{issuer}:{id}`，由 NIP CA 颁发，携带能力声明和访问 scope，节点在协议层强制校验。

---

## 5. 地址规范

### 5.1 节点地址

```
nwp://<host>[:<port>]/<node-path>

# 示例（默认端口 17433）
nwp://api.myapp.com/products          Memory Node
nwp://api.myapp.com/orders/actions    Action Node
nwp://api.myapp.com/ecommerce         Complex Node
```

### 5.2 NID 格式

```
urn:nps:<entity-type>:<issuer-domain>:<identifier>

entity-type:  agent | node | org

# 示例
urn:nps:agent:ca.innolotus.com:550e8400-e29b-41d4
urn:nps:node:api.myapp.com:products
urn:nps:org:mycompany.com
```

### 5.3 协议版本标识

```
NPS/0.2     套件版本
NCP/0.7     子协议版本
```

---

## 6. 帧命名空间

所有 NPS 帧共用统一字节命名空间，按协议分段分配。完整定义见 [frame-registry.yaml](frame-registry.yaml)。

| 范围 | 协议 | 帧列表 |
|------|------|--------|
| 0x01–0x0F | NCP | AnchorFrame(0x01), DiffFrame(0x02), StreamFrame(0x03), CapsFrame(0x04), AlignFrame(0x05)† |
| 0x10–0x1F | NWP | QueryFrame(0x10), ActionFrame(0x11) |
| 0x20–0x2F | NIP | IdentFrame(0x20), TrustFrame(0x21), RevokeFrame(0x22) |
| 0x30–0x3F | NDP | AnnounceFrame(0x30), ResolveFrame(0x31), GraphFrame(0x32) |
| 0x40–0x4F | NOP | TaskFrame(0x40), DelegateFrame(0x41), SyncFrame(0x42), AlignStream(0x43) |
| 0xF0–0xFF | System | ErrorFrame(0xFE)，其余保留 |

† AlignFrame(0x05) 已在 NCP v0.7 标记为 Deprecated，由 NOP AlignStream(0x43) 替代。

---

## 7. 编码层级

所有帧类型支持以下编码层级，通过帧头 Flags 字段标识：

| Tier | 格式 | Flag | 适用场景 |
|------|------|------|---------|
| Tier-1 | JSON | 0x00 | 开发调试、兼容模式 |
| Tier-2 | MsgPack（Binary） | 0x01 | 生产环境，~60% 体积压缩 |
| Tier-3 | BinaryVector v1 | 0x02 | 可选的向量密集型 Payload 编码；需协商 `binary_vector.v1`；MessagePack 元数据 + little-endian float32 向量段 |
| — | Reserved | 0x03 | 保留 |

默认：生产环境使用 Tier-2，开发环境使用 Tier-1。

---

## 8. 安全概述

所有 NPS 通信默认要求 TLS 1.3。Agent 认证流程：

```
1. Agent → NDP ResolveFrame：解析目标节点物理地址
2. Agent → GET /.nwm：读取节点清单 trusted_issuers
3. Agent → Node：发送 IdentFrame 作为连接握手
4. Node → NIP CA：验证 IdentFrame 签名（OCSP 或 CRL）
5. Node：校验 Agent scope 是否覆盖本节点路径
6. 通过后：Agent 发起正式 QueryFrame / ActionFrame
```

详细安全模型见各子协议规范的 Security Considerations 章节。

---

## 9. 版本策略

| 版本号 | 含义 |
|--------|------|
| `v0.x-draft` | 内部草稿，可破坏性变更 |
| `v0.x-alpha` | 公开预览，API 不稳定 |
| `v0.x-beta` | 功能完整，欢迎外部测试 |
| `v1.0` | 规范冻结，生产可用 |

Breaking Change 定义：帧格式变更、字段语义变更、地址规范变更。以上变更须发布新 Minor 或 Major 版本，并在 `CHANGELOG.md` 中记录迁移指南。

---

## 10. 与现有协议的关系

| 协议 | 定位 | 与 NPS 的关系 |
|------|------|--------------|
| REST / GraphQL | 人类 API | NWP 是其 AI-native 节点请求/响应替代 |
| MCP | Tool 调用层 | NPS 填补 MCP 未覆盖的 AI-native 节点请求/响应网络层；提供 MCP 适配器 |
| A2A | Agent 协作 | NOP 覆盖类似场景；提供 A2A 适配器 |

NPS 不替代 MCP，而是在其之上提供效率层。MCP 解决"AI 如何调用工具"，NPS 解决"AI 如何接入互联网"。

---

## 11. 路线图

| Phase | 时间 | 目标 |
|-------|------|------|
| Phase 0 | 2026 Q2 | 规范骨架统一，仓库公开发布 |
| Phase 1 | 2026 Q3 | NCP + NWP + NIP 核心实现，NIP CA Server OSS v0.1 |
| Phase 2 | 2026 Q4 | NDP + NOP 完整协议族，MCP/A2A 适配器，TypeScript SDK |
| Phase 3 | 2027 Q1–Q2 | 生态验证，NPS Cloud CA v1.0，行业 PoC |
| Phase 4 | 2027 Q3+ | W3C / IETF 标准化，NPS 1.0 冻结 |

完整路线图见 [NPS-Roadmap.cn.md](NPS-Roadmap.cn.md)。

---

## 附录 A — 快速参考

### 端口分配

| 用途 | 端口 |
|------|------|
| NPS 统一端口（全协议族） | 17433 |
| NIP CA Server API | 17440 |
| NIP CA Admin UI | 8080 |

各协议可选独立端口：NWP 17434、NIP 17435、NDP 17436、NOP 17437。

### Content-Type

| 场景 | Content-Type |
|------|-------------|
| NWP 标准响应 | `application/nwp-capsule` |
| NWP 帧数据 | `application/nwp-frame` |
| NWM 清单 | `application/nwp-manifest+json` |

### 关键请求头（HTTP 模式）

| 头 | 方向 | 描述 |
|----|------|------|
| `X-NWP-Agent` | 请求 | Agent NID |
| `X-NWP-Budget` | 请求 | CGN 预算上限 |
| `X-NWP-Tokenizer` | 请求 | Agent 使用的 tokenizer 标识 |
| `X-NWP-Depth` | 请求 | 节点图谱遍历深度（默认 1，最大 5）|
| `X-NWP-Schema` | 响应 | 使用的 Schema anchor_id |
| `X-NWP-Tokens` | 响应 | 实际 CGN 消耗 |
| `X-NWP-Tokens-Native` | 响应 | 原生 token 消耗（若已知 tokenizer）|
| `X-NWP-Tokenizer-Used` | 响应 | 实际使用的 tokenizer |
| `X-NWP-Cached` | 响应 | 是否命中缓存 |

### 参考文档

| 文档 | 描述 |
|------|------|
| [status-codes.cn.md](status-codes.cn.md) | NPS 原生状态码与 HTTP 映射 |
| [token-budget.cn.md](token-budget.cn.md) | Cognon Budget 规范 |
| [error-codes.cn.md](error-codes.cn.md) | 统一错误码命名空间 |
| [frame-registry.yaml](frame-registry.yaml) | 机器可读帧注册表 |

---

## 附录 B — 术语表

| 术语 | 定义 |
|------|------|
| **NPS** | Neural Protocol Suite，本协议族总称 |
| **NCP** | Neural Communication Protocol，帧格式与通信基础层 |
| **NWP** | Neural Web Protocol，AI-native 节点请求/响应语义层 |
| **NIP** | Neural Identity Protocol，Agent 身份层 |
| **NDP** | Neural Discovery Protocol，节点发现层 |
| **NOP** | Neural Orchestration Protocol，多 Agent 编排层 |
| **NID** | Neural Identity Descriptor，Agent/节点的唯一身份标识 |
| **NWM** | Neural Web Manifest，节点机器可读清单 |
| **CGN** | Cognon，跨模型标准化 token 计量单位 |
| **AnchorFrame** | NCP 帧类型，由 Node 发布，用于 Schema 锚定 |
| **ErrorFrame** | NPS 统一错误帧 (0xFE)，所有协议层共用 |
| **AlignStream** | NOP 子规范，有向任务流，替代 NCP AlignFrame |
| **Token Budget** | Agent 在单次请求中声明的最大 CGN 消耗上限 |
| **Trust Federation** | 跨组织 CA 的信任链建立机制 |
| **Memory Node** | 数据存储与检索类型的 NWP 节点 |
| **Action Node** | 操作与服务类型的 NWP 节点 |
| **Complex Node** | 综合数据与操作的 NWP 节点，可含子节点引用 |

---

## 附录 C — 变更历史

| 版本 | 日期 | 变更 |
|------|------|------|
| 0.4 | 2026-06-27 | NCP v0.9 激活 Tier-3 BinaryVector v1（`binary_vector.v1`）作为可选向量密集型 Payload 编码；协商后 `0x02` 不再是保留/无效 tier。 |
| 0.2 | 2026-04-12 | 统一端口（17433）；传输双模（HTTP/原生）；AnchorFrame 所有权明确为 Node 发布；Cognon (CGN) 计量体系；NPS 状态码体系；ErrorFrame (0xFE)；编码 Tier-3 标记 Reserved；帧大小可配置 |
| 0.1-draft | 2026-04-10 | 初始草稿，整合 NCP v0.7 设计文档 |

---

*归属：LabAcacia / INNO LOTUS PTY LTD · Apache 2.0*
