[English Version](./README.md) | 中文版

# NPS — Neural Protocol Suite

> **AI 时代的完整互联网基础协议族**

[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)
[![Status](https://img.shields.io/badge/status-Phase%201-green.svg)]()
[![Release](https://img.shields.io/badge/release-v1.0.0--alpha.18-orange.svg)](CHANGELOG.cn.md)
[![NCP](https://img.shields.io/badge/NCP-v0.12-5b8cff.svg)]()
[![NWP](https://img.shields.io/badge/NWP-v0.22-4af0b0.svg)]()
[![NIP](https://img.shields.io/badge/NIP-v0.15-7b61ff.svg)]()
[![NDP](https://img.shields.io/badge/NDP-v0.13-f0a050.svg)]()
[![NOP](https://img.shields.io/badge/NOP-v0.10-ff8c42.svg)]()

NPS 是面向 AI Agent 和模型的完整 Web 基础协议族，由五个子协议组成，覆盖 AI 通信、Web 访问、身份认证、节点发现与多 Agent 编排。

> 最新已发布版本为 `v1.0.0-alpha.18`，包含 NWP 0.21 有状态 LLM context、
> 六 SDK 生命周期对齐与 strict-native 节省门禁。
>
> 当前分支包含经审查、尚未发布的 alpha.19 规范候选：NCP 0.12 / NWP 0.22 /
> NIP 0.15 / NDP 0.13 / NOP 0.10。安装命令与 package 表仍固定在已发布的 alpha.18 线。

---

## 协议族概览

```
┌──────────────────────────────────────────────────────┐
│  NOP  Neural Orchestration Protocol   多 Agent 编排  │
├──────────────────────────────────────────────────────┤
│  NDP  Neural Discovery Protocol       节点发现        │
├──────────────────────────────────────────────────────┤
│  NIP  Neural Identity Protocol        Agent 身份      │
├──────────────────────────────────────────────────────┤
│  NWP  Neural Web Protocol             节点请求/响应   │
├──────────────────────────────────────────────────────┤
│  NCP  Neural Communication Protocol   AI 间通信       │
└──────────────────────────────────────────────────────┘
```

| 协议 | 类比 | 规范版本 | 实现状态 | 端口 (默认/独立) |
|------|------|----------|----------|-----------------|
| **NCP** Neural Communication Protocol | Wire Format | v0.12 | alpha.19 候选：确定性 keepalive/close 时钟、防重放 QUIC 策略、保持身份的迁移与两层背压 | 17433 / — |
| **NWP** Neural Web Protocol | 节点请求/响应 | v0.22 | alpha.19 候选：可续期订阅租约与可移植 stability/SLA/billing 归一化 | 17433 / 17434 |
| **NIP** Neural Identity Protocol | TLS / PKI | v0.15 | alpha.19 候选：确定性续期、吊销新鲜度/未知响应处理与 Phase-3 advisory 真实性 | 17433 / 17435 |
| **NDP** Neural Discovery Protocol | DNS | v0.13 | alpha.19 候选：持久化 sequence/epoch 栅栏与 fail-closed 重启/分区恢复 | 17433 / 17436 |
| **NOP** Neural Orchestration Protocol | SMTP / MQ | v0.10 | alpha.19 候选：有界重放 ledger、单调结果 TTL、安全驱逐与确定性聚合 | 17433 / 17437 |

### 本地 Dev Stack

从独立分发仓库启动四个活跃的开源 daemon：

```bash
git clone https://github.com/labacacia/NPS-Daemons.git
cd NPS-Daemons
docker compose up --build -d
```

自托管身份颁发机构需要单独部署
[`labacacia/NIP-CA-Server`](https://github.com/labacacia/NIP-CA-Server)。
调整网络暴露前，请先阅读两个仓库各自的安全说明。

### 参考 daemon 部署

当前参考部署由 **四个 daemon 进程和独立部署的 NIP CA** 组成。四个 daemon
由 [`NPS-Daemons`](https://github.com/labacacia/NPS-Daemons) 分发，CA 由
[`NIP-CA-Server`](https://github.com/labacacia/NIP-CA-Server) 分发。

| 层 | Daemon | 端口 | 状态（已发布 alpha.18 基线）|
|----|--------|------|-----------------|
| 1（本机基础设施）| [`npsd`](https://github.com/labacacia/NPS-Daemons/tree/main/npsd) | 17433 | 主机身份、本地协议状态和 inbox |
| 1（本机基础设施）| [`nps-runner`](https://github.com/labacacia/NPS-Daemons/tree/main/nps-runner) | — | 有界任务执行和持久租约 |
| 2（接入网关）| [`nps-ingress`](https://github.com/labacacia/NPS-Daemons/tree/main/nps-ingress) | 8080 / 17443 | TLS/mTLS 准入和本地 NCP 代理 |
| 2（发现）| [`nps-registry`](https://github.com/labacacia/NPS-Daemons/tree/main/nps-registry) | 17436 | NDP 注册与图解析 |
| 3（信任锚点）| [`NIP-CA-Server`](https://github.com/labacacia/NIP-CA-Server) | 17435 / 17436 | 证书签发、吊销与管理健康检查 |

原 NPS Cloud CA 与 Ledger 原型仓库已经归档，只保留为设计历史，不再属于活跃发布列车。

---

## 为什么需要 NPS？

现有 Web 协议为人类浏览器设计，AI Agent 访问时面临三个根本问题：

- **语义解析开销**：HTML/CSS/JS 表现层对 AI 无意义，浪费大量 token
- **Schema 重复传输**：每次响应携带完整结构定义，高频访问场景浪费显著
- **无 Agent 身份概念**：无法在协议层区分 AI 访问与人类访问，无法声明 Agent 能力和权限范围

NPS 从零开始，通过 **AnchorFrame Schema 锚定**、**Cognon (CGN) 标准化计量** 和 **NID 身份体系** 重新设计 AI 互联网基础设施。

---

## 核心特性

**Token Economy 第一**
- **Schema 锚定**：首次请求后后续调用仅传 SHA-256 引用，典型场景节约 30–60% token 消耗。
- **Cognon (CGN)**：跨模型的标准化 token 计量单位，支持按预算裁剪响应（Token Budget）。

**五类神经节点角色**
- `Memory Node`：数据存储与检索（RDS / NoSQL / 文件 / 向量数据库）
- `Action Node`：操作与服务调用
- `Complex Node`：综合数据与处理，支持节点图谱遍历（Depth 控制）
- `Anchor Node`：集群入口与 NOP 路由控制面
- `Bridge Node`：NPS 到外部协议的翻译（HTTP/gRPC/MCP/A2A）

**AI-Native 身份**
每个 Agent 持有 NID（Neural Identity Descriptor），格式为 `urn:nps:agent:{issuer}:{id}`，由 NIP CA 颁发，携带能力声明和访问 scope，节点在协议层强制校验。

**统一端口 & 传输双模**
- **统一端口**：全协议族共用 **17433**，通过帧类型码天然路由。
- **双模承载**：支持 **HTTP 模式**（Overlay 部署，防火墙友好）与 **原生模式**（高性能 TCP/QUIC）。

---

## 仓库结构

```
nps/
├── spec/                    # 语言无关规范文档（SSoT）
│   ├── NPS-0-Overview.md    # 套件总览 v0.4
│   ├── NPS-1-NCP.md         # NCP 规范 v0.12
│   ├── NPS-2-NWP.md         # NWP 规范 v0.22
│   ├── NPS-3-NIP.md         # NIP 规范 v0.15
│   ├── NPS-4-NDP.md         # NDP 规范 v0.13
│   ├── NPS-5-NOP.md         # NOP 规范 v0.10
│   ├── frame-registry.yaml  # 机器可读帧注册表 v0.15
│   ├── error-codes.md       # 统一错误码命名空间
│   ├── status-codes.md      # NPS 原生状态码 + HTTP 映射
│   ├── token-budget.md      # CGN 计量规范
│   ├── conformance/         # 共享 wire vectors 与 conformance fixtures
│   ├── services/
│   │   └── NPS-AaaS-Profile.md  # AaaS 合规性规范 v0.2
│   └── rfcs/                # RFC 流程 + 4 份草案（NCP 前导 / X.509+ACME NID / 身份保证等级 / 声誉日志）
├── version.yaml             # 已发布 suite 版本 oracle；最后升版
├── impl/
│   ├── dotnet/              # C# / .NET 10 参考实现（含 samples/ + benchmarks/）
│   ├── python/              # Python SDK v1.0.0-alpha.18 候选
│   ├── typescript/          # TypeScript SDK v1.0.0-alpha.18 候选
│   ├── java/                # Java SDK v1.0.0-alpha.18 候选
│   ├── rust/                # Rust SDK v1.0.0-alpha.18 候选
│   └── go/                  # Go SDK v1.0.0-alpha.18 候选
├── tools/
│   ├── daemons/                # 六个常驻服务。4 个 OSS 打 bundle 发到 labacacia/nps-daemons（npsd / nps-runner / nps-ingress / nps-registry）；2 个 cloud daemon 私有发到 labacacia/NPS-Cloud-CA + labacacia/NPS-Ledger
│   ├── nip-ca-server/          # NIP CA Server — C# / ASP.NET Core；独立发布到 labacacia/nip-ca-server（example/ 收录 5 个冻结的参考移植）
│   ├── release/                # 发布同步脚本（dev → 各独立发布仓）
│   └── mirror-to-gitee/        # Gitee 镜像同步脚本（GitHub → Gitee，labacacia URL 改写）
├── compat/
│   ├── mcp-ingress/          # MCP Ingress v1.0.0-alpha.16（LabAcacia.McpIngress）
│   ├── a2a-ingress/          # A2A Ingress v1.0.0-alpha.16（LabAcacia.A2aIngress）
│   └── grpc-ingress/         # gRPC Ingress v1.0.0-alpha.16（LabAcacia.GrpcIngress）
└── demos/                  # 同时单独发布在 github.com/labacacia/NPS-examples
    ├── nps-demo/            # 端到端业务 demo —— NIP 身份 → AnchorFrame → NOP → DiffFrame
    ├── nwp-graph-walk/      # NWP Complex Node §11 —— depth 扇出 + X-NWP-Trace 环路检测
    ├── ingress-playground/   # 一个 NWP Action Node 被 MCP + A2A + gRPC 同时前置
    └── cross-sdk-interop/   # 四语言客户端（dotnet/python/node/go）对同一个 Memory Node 做 diff
```

> **单独展示仓库。** Tier-1 三个演示（`nwp-graph-walk`、`ingress-playground`、
> `cross-sdk-interop`）同时作为一个精选仓库发布在
> [`labacacia/NPS-examples`](https://github.com/labacacia/NPS-examples)
> （[Gitee 镜像](https://gitee.com/labacacia/NPS-examples)）。代码的唯一
> 源仍然在这里；单独仓库的意义是"可发现性"，并且每个演示都有按
> 原理 / 作用 / 演示了什么 / 运行结果 组织的 README。

---

## 实现状态

下表描述已发布的 `1.0.0-alpha.18` 源码树。最新已发布 package line
为 `1.0.0-alpha.18`。

### C# / .NET（`impl/dotnet/`）

| 组件 | 版本 | 状态 | 内容 |
|------|------|------|------|
| `NPS.Core` | 1.0.0-alpha.18 | 已发布 | 帧编解码（MsgPack/JSON）、双头模式（4B/8B）、帧注册表、Anchor 缓存、NativeAOT-safe codec |
| `NPS.NWP` | 1.0.0-alpha.18 | 已发布 | Memory / Action / Complex / Anchor / Bridge Node 中间件；通过 NCP session 的 native-mode NWP serving；`/.nwm`·`/.schema`·`/actions`·`/invoke`·`/query`·`/system.task.*`；图谱遍历 + X-NWP-Depth + 环路检测；SSRF + 幂等 + priority + 异步任务生命周期 |
| `NPS.NIP` | 1.0.0-alpha.18 | 已发布 | CA 库（密钥生成、证书签发/吊销、类型化远程 CA client、OCSP、CRL）、`NipIdentVerifier` 6 步身份验证 |
| `NPS.NDP` | 1.0.0-alpha.18 | 已发布 | NDP 帧类型（Announce/Resolve/Graph）、内存注册表（TTL 淘汰）、公告签名验证器 |
| `NPS.NOP` | 1.0.0-alpha.18 | 已发布 | DAG 编排引擎（条件求值、输入映射、K-of-N 同步、重试/退避）+ §8.2 委托链深度限制 + §8.4 callback SSRF 防护及指数退避重试 |
| `NPS.Conformance` | 1.0.0-alpha.18 | 已发布 | Node L1/L2 conformance case catalog、run manifest model 与 CI validation helper |
| `tools/nip-ca-server` | 1.0.0-alpha.18 | 已发布 | NIP CA Server —— C# / ASP.NET Core 10、PostgreSQL、Docker。独立发布到 [`labacacia/nip-ca-server`](https://github.com/labacacia/nip-ca-server)（唯一打 release 的实现）；5 个其它语言参考移植（Python / TypeScript / Java / Rust / Go）冻结在 `1.0.0-alpha.11`，放在 `tools/nip-ca-server/example/` 下。|
| Compat 接入 | 1.0.0-alpha.16 | 最后一次发布（2026-07-23） | MCP Ingress（JSON-RPC 2.0，MCP 2024-11-05）、A2A Ingress（Google A2A v0.2）、gRPC Ingress（HTTP/2，4 个 unary RPC）；alpha.17 曾准备过弃用版但从未发布，自 alpha.18 起退出同步发布列车，迁移到 `NPS.NWP.Bridge` |
| Daemons | 1.0.0-alpha.18 | 已发布 | 六个常驻服务：`npsd`（L1 最小集）、`nps-runner`、`nps-ingress`、`nps-registry`、`nps-cloud-ca`、`nps-ledger`（RFC-0004 内存日志）；详见 [`docs/daemons/architecture.cn.md`](docs/daemons/architecture.cn.md) |
| Samples | — | ✅ 可用 | `samples/NPS.Samples.NopDag` —— 真 HTTP 的 3 节点 NOP DAG 端到端；`demos/nps-demo` —— 4 幕业务 demo（NIP → AnchorFrame → NOP → DiffFrame）|
| Benchmarks | — | ✅ 可用 | `benchmarks/NPS.Benchmarks.TokenSavings` → **相对 REST 节省 45.0% CGN**（超过 Phase 1 ≥30% 出口）；`benchmarks/NPS.Benchmarks.WireSize` → **MsgPack 相对 JSON 减少 63.6%**（超过 Phase 2 ≤50% 出口）|

.NET SDK test gate：**964 tests**（NPS.Core / NWP / NIP（含 AssuranceLevel、Reputation、X.509/ACME、吊销、存储、remote CA client）/ NDP / NOP / Anchor / Bridge / native NCP / native NWP / conformance / samples / benchmarks），加上 **48 个冻结 compat-ingress tests**（15 mcp + 18 a2a + 15 grpc）。

### Python（`impl/python/`）

| 组件 | 版本 | 状态 | 内容 |
|------|------|------|------|
| `nps-lib` | 1.0.0-alpha.18 | 已发布 | 完整 client/server 协议能力，asyncio + httpx，Ed25519 签名，1372 测试，92.07% 覆盖率。Python 导入模块仍为 `nps_sdk`。 |

### TypeScript（`impl/typescript/`）

| 组件 | 版本 | 状态 | 内容 |
|------|------|------|------|
| `@labacacia/nps-sdk` | 1.0.0-alpha.18 | 已发布 | 完整 client/server 协议能力，Node.js 22+，MsgPack + JSON 双编码，Ed25519 签名，1171 测试。最新已发布的 `1.0.0-alpha.18` package 包含 `dist/`。 |

### Java（`impl/java/`）

| 组件 | 版本 | 状态 | 内容 |
|------|------|------|------|
| `nps-java` | 1.0.0-alpha.18 | 已发布 | 完整 client/server 协议能力，Java 21，MsgPack + JSON 双编码，Ed25519 内置签名，AES-256-GCM 密钥加密，693 测试 |

### Rust（`impl/rust/`）

| 组件 | 版本 | 状态 | 内容 |
|------|------|------|------|
| `nps-rs` | 1.0.0-alpha.18 | 已发布 | 完整 client/server 协议能力，Rust stable，MsgPack + JSON 双编码，Ed25519 签名，AES-256-GCM 密钥加密，Tokio 异步，755 测试 |

### Go（`impl/go/`）

| 组件 | 版本 | 状态 | 内容 |
|------|------|------|------|
| `github.com/labacacia/NPS-sdk-go` | 1.0.0-alpha.18 | 已发布 | 完整 client/server 协议能力，Go 1.23+，MsgPack + JSON 双编码，Ed25519 内置签名，AES-256-GCM 密钥加密，683 测试 |

---

## 快速开始（C#）

### 1. 注册 NCP 核心与 NWP 服务
```csharp
services.AddNpsCore(opt => {
    opt.DefaultTier = EncodingTier.MsgPack;
});

services.AddNwp(opt => {
    opt.DefaultTokenBudget = 1000;
});
```

### 2. 构建并编码 NWP 查询帧
```csharp
var query = new QueryFrame {
    AnchorRef = "sha256:a3f9b2c1...",
    Filter = JsonSerializer.SerializeToElement(new { status = "active" }),
    Limit = 50
};

byte[] wire = codec.Encode(query); // 自动处理 4-byte/8-byte 帧头
```

---

## 路线图

**Phase 0 — 规范统一（已完成）**
- [x] 全部 5 份子协议规范文档 v0.2+ draft
- [x] `frame-registry.yaml` 机器可读帧注册表 v0.2
- [x] `error-codes.md` / `status-codes.md` 错误码与 HTTP 映射
- [x] `token-budget.md` CGN 计量规范
- [x] `services/NPS-AaaS-Profile.md` AaaS 合规性规范 v0.1

**Phase 1 — 核心构建（进行中，目标 2026 Q3）**
- [x] `NPS.Core` C# 参考实现 — 帧编解码库
- [x] `NPS.NWP` C# 参考实现 — Memory / Action / Complex / Anchor / Bridge Node 中间件
- [x] `NPS.NIP` C# 参考实现 — CA 库 + 身份验证器（OCSP / CRL）
- [x] `NPS.NDP` C# 参考实现 — 帧类型 + 注册表 + 公告验证器
- [x] `NPS.NOP` C# 参考实现 — 编排引擎全量实现 + 安全加固
- [x] NIP CA Server OSS v0.1 —— **6 个语言变体**（C# / Python / TypeScript / Java / Rust / Go）
- [x] Python SDK `nps-lib` v1.0.0-alpha.16 (Phase 1)（NCP + NWP + NIP + NDP + NOP，162 测试；import 模块 `nps_sdk`）
- [x] Token-savings 基准 → 相对 REST 聚合节省 45.0%（达成 Phase 1 ≥30% 出口标准）
- [x] NuGet registry 发布（`NPS.Core`、`NPS.NWP` 等）—— 14 个包已发布到 Nexus；14 个符号包已生成并保留在 GitHub Release bundle
- [ ] PyPI 正式发布（`nps-lib`）

**Phase 2 — 生态扩展（进行中）**
- [x] TypeScript SDK v1.0.0-alpha.16（Phase 2；取代此前缺 `dist/` 已 deprecated 的 npm `1.0.0-alpha.11`；`alpha` dist-tag 现已指向 `1.0.0-alpha.16`）
- [x] Java SDK v1.0.0-alpha.16（Phase 2；nps-java：NCP + NWP + NIP + NDP + NOP，Java 21，Ed25519，87 tests）
- [x] Rust SDK v1.0.0-alpha.16（Phase 2；nps-rs：NCP + NWP + NIP + NDP + NOP，Rust stable，Ed25519，88 tests）
- [x] Go SDK v1.0.0-alpha.16（Phase 2；github.com/labacacia/NPS-sdk-go：NCP + NWP + NIP + NDP + NOP，Go 1.25+，Ed25519，75 tests）
- [x] **MCP Ingress** v1.0.0-alpha.16（`LabAcacia.McpIngress`，MCP 2024-11-05，JSON-RPC 2.0；NPS-CR-0001 重命名）
- [x] **A2A Ingress** v1.0.0-alpha.16（`LabAcacia.A2aIngress`，Google A2A v0.2；NPS-CR-0001 重命名）
- [x] **gRPC Ingress** v1.0.0-alpha.16（`LabAcacia.GrpcIngress`，4 个 unary RPC；NPS-CR-0001 重命名）
- [x] [`NPS-Daemons`](https://github.com/labacacia/NPS-Daemons) 中的 **4 个活跃 daemon 二进制**，加独立 [`NIP-CA-Server`](https://github.com/labacacia/NIP-CA-Server)；Cloud CA 与 Ledger 原型已归档
- [x] **NPS-RFC-0001** Accepted（NCP 连接前导）—— Phase 1 .NET helper
- [x] **NPS-RFC-0003** Accepted（Agent 身份保证等级）—— Phase 1 .NET 参考类型
- [x] **NPS-RFC-0004** Accepted（NID 声誉日志）—— Phase 1 .NET 条目类型 + 签名/验签
- [x] **NPS-CR-0001** Implemented（Anchor + Bridge Node 拆分；`compat/*-bridge` → `compat/*-ingress` 重命名）
- [x] **NPS-CR-0002** Implemented（Anchor Node 上的 `topology.snapshot` / `topology.stream` 保留查询类型；.NET 参考实现 + L2 合规套件 —— daemon 端整合推迟）
- [x] 3 节点 NOP DAG 样例（`samples/NPS.Samples.NopDag`）—— 达成 Phase 2 DAG 出口标准
- [x] Wire-size 基准 → MsgPack 相对 JSON 减少 63.6%（达成 Phase 2 ≤50% 出口标准）
- [x] RFC-0001..0004 草案回应 2026-04-20 评审（NCP 前导 / X.509+ACME NID / 身份保证等级 / 声誉日志）
- [ ] NPS Cloud 托管服务
- [ ] 各协议规范升至 Proposed / Stable 状态

---

## 文档索引

### 概念指南

| 主题 | 说明 |
|------|------|
| [帧模型](docs/concepts/frame-model.md) | NCP 帧结构、编码 Tier、Flags 位图、Schema 锚定机制 |
| [NID 身份体系](docs/concepts/nid-identity.md) | NID 格式、证书结构、六步验证、OCSP、Scope 通配符 |
| [DAG 编排](docs/concepts/dag-orchestration.md) | 执行流程、输入映射、K-of-N 同步、回调与聚合策略 |
| [节点类型](docs/concepts/node-types.md) | Memory / Action / Complex / Anchor / Bridge 节点对比 |

### .NET SDK 参考

| 包 | 说明 |
|----|------|
| [.NET SDK 索引](docs/sdk/dotnet/index.md) | 环境要求、包依赖关系、DI 注册总览 |
| [NPS.Core](docs/sdk/dotnet/nps-core.md) | 帧类型、编解码器、AnchorCache、异常体系 |
| [NPS.NWP](docs/sdk/dotnet/nps-nwp.md) | QueryFrame、Filter DSL、MemoryNodeMiddleware、NWM 清单 |
| [NPS.NIP](docs/sdk/dotnet/nps-nip.md) | NipIdentVerifier 六步验证、NipCaService、NipSigner、CA HTTP 路由 |
| [NPS.NDP](docs/sdk/dotnet/nps-ndp.md) | AnnounceFrame、INdpRegistry、NdpAnnounceValidator |
| [NPS.NOP](docs/sdk/dotnet/nps-nop.md) | TaskFrame/DAG 模型、NopOrchestrator、回调校验器、条件/输入映射 |

### 兼容性桥

| 主题 | 说明 |
|------|------|
| [桥层总览](docs/compat/index.md) | MCP / A2A / gRPC 何时选哪个；共同设计与非目标 |
| [MCP Ingress 详解](docs/compat/mcp-ingress.md) | 1:N 上游模型、tool 名编码、异步生命周期、header 语义 |
| [A2A Ingress 详解](docs/compat/a2a-ingress.md) | 1:1 AgentCard 映射、skill 查找、任务状态转换、内存绑定 |
| [gRPC Ingress 详解](docs/compat/grpc-ingress.md) | bytes 透传原理、双错误映射、多语言 client |

### 基准报告

| 报告 | 结果 |
|------|------|
| [REST vs NWP token 节省](docs/benchmarks/token-savings.md) | 聚合 **45.0%** CGN 节省（S1 43.1% / S2 44.0% / S3 54.2%）—— 超过 Phase 1 ≥30% 出口标准 |
| [Tier-1 JSON vs Tier-2 MsgPack wire size](docs/benchmarks/wire-size.md) | 稳态帧聚合字节减少 **63.6%** —— 超过 Phase 2 ≤50% 出口标准 |
| [有状态 LLM context 节省](docs/benchmarks/llm-context-savings.md) | Strict-native 第二轮 decoder 字节减少 **22.5%**、evaluated token 减少 **62.4%**，保持 role/tool 语义一致并禁用 fallback |

### 协议规范

| 文档 | 说明 |
|------|------|
| [NPS 总览](spec/NPS-0-Overview.md) | 套件入口，帧命名空间一览 |
| [NCP 规范](spec/NPS-1-NCP.md) | 帧线格式、编码 Tier、双头模式 |
| [NWP 规范](spec/NPS-2-NWP.md) | 节点请求/响应语义、Filter DSL、流式响应 |
| [NIP 规范](spec/NPS-3-NIP.md) | 身份协议、Ed25519 签名、CRL/OCSP |
| [NDP 规范](spec/NPS-4-NDP.md) | 节点发现、TTL 广播、图谱同步 |
| [NOP 规范](spec/NPS-5-NOP.md) | DAG 编排、委托链、K-of-N |
| [Cognon (CGN) 计量](spec/token-budget.md) | 跨模型标准化 Token 计量单位 |
| [错误码命名空间](spec/error-codes.md) | 全协议统一错误码 |
| [状态码映射表](spec/status-codes.md) | NPS 原生状态码与 HTTP 映射 |
| [AaaS 合规性规范](spec/services/NPS-AaaS-Profile.md) | Anchor Node + Bridge Node、Vector Proxy、L1/L2/L3 合规级别 |
| [Daemon 架构](docs/daemons/architecture.cn.md) | 六-daemon、三层参考部署拓扑 |
| [RFC 流程 + 草案](spec/rfcs/README.md) | RFC-0001 NCP 前导 · 0002 X.509+ACME NID · 0003 身份保证等级 · 0004 声誉日志 |

---

## 归属

| 产出 | 归属 |
|------|------|
| NPS 规范文档 | LabAcacia / INNO LOTUS PTY LTD |
| 参考实现 (OSS) | LabAcacia |
| NPS Cloud 服务 | INNO LOTUS PTY LTD |

**LabAcacia** 是 INNO LOTUS PTY LTD 旗下的开源实验室。Apache 2.0 授权。

---

## License

[Apache 2.0](LICENSE) © 2026 INNO LOTUS PTY LTD
