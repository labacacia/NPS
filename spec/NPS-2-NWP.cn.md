[English Version](./NPS-2-NWP.md) | 中文版

# NPS-2: Neural Web Protocol (NWP)

**Spec Number**: NPS-2
**Status**: Proposed
**Version**: 0.23
**Date**: 2026-09-09
**Port**: 17433（默认，共用）/ 17434（可选独立）
**Authors**: Ori Lynn / INNO LOTUS PTY LTD
**Depends-On**: NPS-1 (NCP v0.13)、NPS-3 (NIP v0.16)、NPS-4 (NDP v0.14)

> 本文档为 NWP 详细规范。套件总览见 [NPS-0-Overview.cn.md](NPS-0-Overview.cn.md)。

---

## 1. Terminology

本文档中的关键字 "MUST"、"MUST NOT"、"REQUIRED"、"SHOULD"、"MAY" 按照 RFC 2119 解释。

---

## 2. 协议概述

NWP 定义 AI Agent 与神经节点交互时使用的 AI-native 请求/响应语义。Agent 通过 `nwp://` 地址查询数据、调用操作、订阅变化、经由集群 Anchor 路由，并通过 Bridge 连接外部协议；覆盖 Memory / Action / Complex / Anchor / Bridge 五类角色。节点响应直接可被模型理解，无需任何语义解析层。

NWP 是语义协议层，不是只面向 Memory Node 的 REST API。它可以承载在原生 NCP session 上，也可以使用 §2.2 定义的 HTTP Overlay 模式；REST 只是一种便于理解请求/响应语义的类比，或 Bridge Node 的外部协议目标之一。

LLM 服务通过普通 Action Node 或 Complex Node 上的 NWM **LLM/Thinking Profile**（§4.2a）表达，而不是新增第六种基础 `node_type`。这样 `node_type` 继续描述协议职责，同时让模型服务节点拥有标准发现形态。

### 2.1 节点类型

| 类型 | 职责 | 典型数据源 |
|------|------|-----------|
| **Memory Node** | 数据存储与检索，不含计算逻辑 | RDS、NoSQL、文件系统、向量数据库 |
| **Action Node** | 执行操作，返回结果或副作用 | 函数、外部 API、消息队列、Webhook |
| **Complex Node** | 混合数据与操作，含子节点引用 | 以上所有类型 + 子节点引用 |
| **Anchor Node** | 集群控制平面与对外入口 —— 把入站 NWP `Action`/`Query` 帧通过 NOP 路由给成员节点，可选维护成员节点拓扑 | AaaS 平台、多 Agent 服务网关、子集群路由器 |
| **Bridge Node** | 在 NPS 帧与非-NPS 协议（HTTP/HTTPS、gRPC、MCP、A2A）之间翻译 | 调用遗留 REST API、gRPC 服务、Model Context Protocol 服务端、Agent-to-Agent 端点 |

一个节点 MAY 同时承担多个角色（例如同一进程既是 Memory Node 又是 Anchor Node，无须分离时）。多角色声明放在 NDP `Announce` 帧的 `node_roles` 字段（NPS-4 §3.1）。

> **Thinking Node** 是产品层别名，不是 wire-level 节点类型。服务 LLM completion 的节点如果只执行模型 action，SHOULD 声明 `node_type: "action"`；如果还拥有记忆、工具编排、路由、图遍历或会话状态，SHOULD 声明 `node_type: "complex"`。它通过标准 NWM `profiles.llm` 块（§4.2a）与对应 NIP capabilities（`llm:*`，NPS-3 §5.1）暴露能力。

> **Anchor Node** 与 **Bridge Node** 由 [NPS-CR-0001](cr/NPS-CR-0001-anchor-bridge-split.md) 一同引入，替换原 `Gateway Node` 类型：
> - **Anchor Node** 继承了 Gateway Node 原本承载的"集群入口 + NOP 路由"角色。它在每次请求中无状态，但 MAY 维持一份成员节点的长期注册表。
> - **Bridge Node** 是新类型，职责是 **NPS ↔ 非-NPS 协议翻译，双向**。每次请求无状态，不参与集群拓扑。方向按协议在 NDP `Announce` 中声明（`bridge_protocols` 出向、`bridge_inbound_protocols` 入向）。（[NPS-CR-0010](cr/NPS-CR-0010-bridge-bidirectional.md) 就此定案：alpha.3–alpha.15 期间规范中存在一条"仅出向"的收窄，其唯一存在理由是让 `Bridge` 这个名字与当时独立存在的 `compat/*-ingress` 包区分开；那些包现已并入 Bridge 包，该限制随之解除。）
> - 原 `Gateway Node` 术语已弃用；`"gateway"` wire 值被移除，解析器 MUST 拒绝并明确报告 CR-0001。

#### 已移除类型

> **Gateway Node**（v1.0-alpha.3 移除）—— 拆为 **Anchor Node**（集群入口 / NOP 路由）与 **Bridge Node**（NPS↔非-NPS 协议翻译）。完整背景与迁移说明见 [NPS-CR-0001](cr/NPS-CR-0001-anchor-bridge-split.md)。实现 MUST 以 `NWP-MANIFEST-NODE-TYPE-REMOVED` 拒绝遗留 `node_type: "gateway"`，以 `NDP-ANNOUNCE-ROLE-REMOVED` 拒绝遗留 `node_roles: ["gateway"]`（或 `node_kind: "gateway"`）；响应 SHOULD 携带指向 NPS-CR-0001 的 `hint` 字段。

#### 节点角色解析

两个字段在不同协议层参与节点角色声明，语义有意设计为不同，命名也刻意区分：

| 字段 | 协议 | 层次 | 基数 | 权威含义 |
|------|------|------|------|---------|
| `node_roles` | NDP `Announce`（NPS-4 §3.1）| 发现层 | 数组——该节点承担的全部角色 | 完整角色集；用于发现过滤和集群成员管理 |
| `node_type` | NWP NWM（§4.1）| 服务层 | 字符串——单个运营角色 | 该 `/.nwm` 端点当前提供的角色 |

**约束**：`node_type` MUST 是该节点 NDP `Announce.node_roles` 中声明的值之一。有缓存 NDP 数据时，验证方 SHOULD 对照校验。

多角色节点（如 `node_roles: ["anchor", "memory"]`）可在不同路径或端口上暴露多个 `/.nwm` 端点，每个端点声明不同的 `node_type`；也可在同一端点选择主导角色填入 `node_type`。无论哪种方式，约束 `node_type ∈ node_roles` 均成立。

#### Anchor Node —— 详细语义

Anchor Node MUST：

1. 接受寻址到集群（即 Anchor 自身的 NID，而非具体成员 NID）的入站 NWP `Action` / `Query` 帧。
2. 根据成员节点声明的能力与当前负载，把帧分派给合适的成员。参考分派路径把 `ActionFrame` 转为 NOP `TaskFrame`，委托给本机 NOP orchestrator（见 [NPS-AaaS-Profile §2](services/NPS-AaaS-Profile.cn.md)）。
3. 把成员节点的出站响应聚合成单条响应流回送给原始调用方。
4. 可选地维护集群内成员节点的注册表（NID、声明能力、`activation_mode`，见 [NPS-Node Profile §6](services/NPS-Node-Profile.cn.md)）。成员节点入集群时通过 NDP `Announce` 帧携带 `cluster_anchor` 引用 Anchor Node 的 NID 完成注册；下线遵循标准 NDP 离线语义。

集群 MUST 至少有一个 Anchor Node。HA 部署 MAY 为同一集群运行多个 Anchor Node；Anchor Node 之间的共识协议是实现自定的，推迟到 NPS-AaaS Profile L3。

维护成员注册表的 Anchor Node MUST 通过保留查询类型 `topology.snapshot` 与 `topology.stream`（§12）暴露注册表内容，详见 [NPS-CR-0002](cr/NPS-CR-0002-anchor-topology-queries.md)。两者在 NPS-AaaS Profile L2 及以上等级强制要求。

#### Bridge Node —— 详细语义

Bridge Node 在 NPS 帧与非-NPS 协议之间做**双向**翻译。它 MUST 至少实现一个方向，并且 MUST 通过 NDP `Announce` 声明哪些协议走哪个方向（NPS-4 §3.1）：`bridge_protocols` 表示出向，`bridge_inbound_protocols` 表示入向。**不得**假设一个 Bridge Node 提供它未声明的方向。（NPS-CR-0010）

**出向 —— NPS → 外部协议**（与既有规范一致，未变更）。提供出向服务的 Bridge Node MUST：

1. 接受携带 `bridge_target` 参数（标识外部协议与端点）的入站 NWP 帧。规范化 `bridge_target` wire 形状为 `{ "protocol", "endpoint", "extras"? }`：`protocol`（string，必填，取值为 `"http"`、`"grpc"`、`"mcp"`、`"a2a"` 之一）；`endpoint`（string URL，必填）；`extras`（object，可选，承载按协议变化的参数，如 HTTP `method`、`headers`，MCP `tool`，或 gRPC call metadata）。HTTP header MUST 放在 `bridge_target.extras.headers` 内，不能作为顶层 `bridge_target.headers` 字段。第三方 adapter MAY 在 `extras` 中扩展字段；consumer MUST 忽略未知顶层字段与未知 `extras` 成员。
2. 用目标协议的格式产出对外请求。
3. 把目标协议的响应翻译回 NWP 帧（通常是 `CapsFrame`）。

**入向 —— 外部协议 → NPS**（新增）。提供入向服务的 Bridge Node MUST：

4. 为 `bridge_inbound_protocols` 中列出的每个协议暴露一个服务端端点，使用该协议的原生线格式。
5. 把外部协议请求翻译成发往其所代理的 NPS 节点的 NWP 帧 —— 读操作走 Memory Node `Query`，调用走 Action / Complex Node `Invoke` —— 并把 NWP 响应翻译回该外部协议的响应格式。
6. **不得**要求外部客户端具备任何 NPS 寻址、NID 或帧的知识。外部客户端 MUST 能够把这个 Bridge 当作它自己协议的原生服务端来使用。
7. 按该协议的规范化映射表（§16.3）把 NWP / NPS 错误码映射到外部协议的错误空间。

对 **MCP 入向**而言，合规的 Bridge Node MUST 提供 `initialize`、`ping`、`tools/list`、`tools/call`、`resources/list`、`resources/read`。Memory Node 投射为 MCP resource；Action / Complex Node 投射为 MCP tool。

一个部署 MAY 把入向翻译当作纯宿主库跑在 NPS 节点前面，不持有 NID、不发 `Announce`。这样的部署**不是** Bridge Node —— 它是 Bridge 库，上述 MUST 不约束它。只有发出 `node_roles: ["bridge"]` 公告的部署才是 Bridge Node。（NPS-CR-0010 §3.3）

Bridge Node 在**两个方向上**都是每次请求无状态、不参与集群拓扑。一个 Bridge Node MAY 同时服务多个协议和两个方向；部署 MAY 为隔离起见按协议或按方向跑独立的 Bridge Node。

参考 Bridge Node 实现期望支持的标准外部协议：

- HTTP / HTTPS（REST 与 streaming）
- gRPC（unary 与 streaming）
- MCP（Model Context Protocol）
- A2A（Agent-to-Agent 协议）

更多协议适配器 MAY 通过未来 CR 注册。支持的协议集合在 NDP `Announce.bridge_protocols` / `Announce.bridge_inbound_protocols` 中声明（NPS-4 §3.1）。

### 2.2 Overlay 模式

在现有 HTTP 服务上附加 NWP 接口，服务器根据请求头区分访问者：

```
请求含 X-NWP-Agent 或 HelloFrame  →  返回 application/nwp-*
普通浏览器请求（无以上标识）        →  返回 text/html（正常网站）
```

Overlay 模式下 NWP 使用 HTTP 传输，帧序列化在 HTTP Body 中。详见 [NPS-1-NCP.cn.md §2.2](NPS-1-NCP.cn.md#22-传输模式)。

---

## 3. 节点地址规范

### 3.1 nwp:// URL 语法（ABNF）

```abnf
nwp-url     = "nwp://" host [":" port] "/" node-path ["/" sub-path]
host        = <RFC 3986 host>
port        = 1*DIGIT               ; 默认 17433
node-path   = segment *("/" segment)
sub-path    = "query" / "stream" / "invoke" / "subscribe" / "actions"
            / ".schema" / ".nwm"
segment     = 1*(ALPHA / DIGIT / "-" / "_")
```

### 3.2 子路径约定

| 子路径 | 方法 | 适用节点 | 描述 |
|--------|------|---------|------|
| `/query` | POST | Memory | 单次结构化查询（返回 CapsFrame）|
| `/stream` | POST | Memory | 流式查询（返回 StreamFrame 序列）|
| `/invoke` | POST | Action / Complex | 操作调用入口 |
| `/subscribe` | POST | Memory | 变更订阅入口（HTTP 模式，SSE）|
| `/actions` | GET | Action / Complex | 列举节点可调用操作（返回 NWM actions 子集 JSON）|
| `/.schema` | GET | 所有 | Schema 定义（返回 AnchorFrame JSON）|
| `/.nwm` | GET | 所有 | 完整节点清单（返回 NWM JSON）|

---

## 4. Neural Web Manifest (NWM)

每个节点 MUST 在 `/.nwm` 路径暴露机器可读清单，MIME 类型：`application/nwp-manifest+json`。

### 4.1 完整字段定义

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `nwp` | string | 必填 | NWP 版本，当前为 `"0.4"` |
| `node_id` | string | 必填 | 节点 NID，格式：`urn:nps:node:{host}:{path}` |
| `node_type` | string | 必填 | `"memory"` / `"action"` / `"complex"` / `"anchor"` / `"bridge"`。遗留值 `"gateway"` 在 v1.0-alpha.3 移除（见 §2.1 *已移除类型* 与 [NPS-CR-0001](cr/NPS-CR-0001-anchor-bridge-split.md)），解析器 MUST 以 `NWP-MANIFEST-NODE-TYPE-REMOVED` 拒绝；其他无法识别的值 MUST 以 `NWP-MANIFEST-NODE-TYPE-UNKNOWN` 拒绝。此字段声明该 NWP 服务端点**当前运营的角色**，MUST 是节点 NDP `Announce.node_roles`（NPS-4 §3.1）中的一个值。多角色节点在此选填生效角色；完整约束见 §2.1 *节点角色解析*。 |
| `display_name` | string | 可选 | 人类可读节点名称 |
| `manifest_version` | uint32 | 可选 | 单调递增的清单版本计数器（从 1 开始，每次结构性变更递增）。服务端 MUST 在每次 `GET /.nwm` 响应中携带 `X-NWM-Version: <manifest_version>`。Agent SHOULD 在后续请求中发送 `If-None-Match: <manifest_version>`；清单未变更时返回 `304 Not Modified`。（NWP v0.14）|
| `manifest_updated_at` | string | 可选 | 最近一次清单变更的 ISO 8601 时间戳，例如 `"2026-06-03T12:00:00Z"`。SHOULD 在 `manifest_version` 递增时一并设置。（NWP v0.14）|
| `min_agent_version` | string | 可选 | Agent 支持的最低 NPS 版本，格式 `"major.minor"`；低于此版本的 Agent MUST 被拒绝并返回 `NWP-MANIFEST-VERSION-UNSUPPORTED` |
| `min_assurance_level` | string | 可选 | 取值 `"anonymous"`（默认）/ `"attested"` / `"verified"` 之一（见 [NIP §5.1.1](NPS-3-NIP.cn.md#511-保证等级nps-rfc-0003)）。请求等级低于此值时 MUST 返回 `NWP-AUTH-ASSURANCE-TOO-LOW`（`NPS-AUTH-FORBIDDEN`），响应 SHOULD 在 `hint` 字段附 CA 注册 URL。默认 `"anonymous"` 与 v1.0-alpha.2 节点向后兼容。可在 §4.6 单个 ActionSpec 上以 `min_assurance_level` 字段覆盖。（NPS-RFC-0003）|
| `wire_formats` | array | 必填 | 支持的编码格式列表：`["ncp-capsule", "msgpack", "json"]` |
| `preferred_format` | string | 必填 | 首选格式 |
| `schema_anchors` | object | 可选 | 预声明的 Schema 锚点，`{name: anchor_id}` |
| `capabilities` | object | 必填 | 节点能力声明，见 §4.2 |
| `data_sources` | array | 可选 | 底层数据源标识列表 |
| `auth` | object | 必填 | 认证要求，见 §4.3 |
| `rate_limits` | object | 可选 | 频率限制声明，见 §4.4 |
| `actions` | object | 条件必填 | Action/Complex/Anchor 节点 MUST 填写；操作注册表，见 §4.6 |
| `endpoints` | object | 必填 | 各功能端点 URL |
| `graph` | object | 可选 | 子节点引用（Complex Node 专用），见 §11 |
| `tokenizer_support` | array | 可选 | 节点支持的 tokenizer 列表（见 [token-budget.cn.md](token-budget.cn.md)）|
| `stability` | string | 可选 | 生命周期阶段：`"experimental"` / `"stable"` / `"deprecated"`。Marketplace / NeuronHub 发现客户端据此过滤或对非 stable 服务给出告警。默认 `"stable"`（向后兼容——0.11 之前的清单一律视为 stable）。可在 ActionSpec.stability（§4.6）上做 per-action 覆盖。|
| `sla` | object | 可选 | 节点的 SLO 承诺，见 §4.7。仅供参考，协议层不做强制。可在 ActionSpec.sla（§4.6）上做 per-action 覆盖。|
| `billing` | object | 可选 | 节点的商业元数据（计量 profile + 价格提示），见 §4.8。仅供参考，协议层不收取也不结算费用。可在 ActionSpec.billing（§4.6）上做 per-action 覆盖。|
| `subscription_policy` | object | 可选 | 可续订订阅限制：`default_lease_seconds`、`max_lease_seconds` 与 `renew_before_seconds`，见 §13.4。缺省表示 v0.21 无 lease 的兼容行为。|
| `trust_anchors` | array of strings | 可选 | Anchor 接受作为 IdentFrame 签发者的 CA 节点 NID 列表（例如 `["urn:nps:agent:ca.example.com:root"]`）。消费方 SHOULD 据此在连接前预校验自己的签发者。缺省时，节点接受任何被 NIP 验证链信任的 CA。|
| `profiles` | object | 可选 | 叠加在基础节点角色上的结构化协议 profile，见 §4.2a。Consumer MUST 忽略无法识别的 profile key。|

### 4.2 capabilities 字段

| 能力键 | 类型 | 描述 |
|--------|------|------|
| `query` | bool | 支持 QueryFrame（单次查询）|
| `stream_query` | bool | 支持流式查询（StreamFrame 响应）|
| `aggregate` | bool | 支持聚合查询（QueryFrame.aggregate）|
| `subscribe` | bool | 支持变更订阅（DiffFrame 推送）|
| `subscribe_filter` | bool | 订阅时支持携带 filter 条件 |
| `vector_search` | bool | 支持向量相似搜索 |
| `token_budget_hint` | bool | 支持根据 CGN 预算裁剪响应 |
| `ext_frame` | bool | 支持扩展帧头（大帧模式）|
| `e2e_enc` | bool | 支持 NCP E2E 加密（ENC=1，见 NPS-1-NCP §7.4）|
| `inline_anchor` | bool | 支持在响应中内联返回更新后的 AnchorFrame |

### 4.2a profiles 字段

`profiles` 是可选对象，用于声明叠加在基础节点角色之上的结构化能力 profile。Profile **不会**引入新的 `node_type`。不理解某个 profile key 的 consumer MUST 忽略该 key，并继续使用基础 NWP 角色、action registry 与 capability flags。

#### LLM / Thinking Profile（`profiles.llm`）

携带 `profiles.llm` 的节点是 **具备 LLM 能力的 Action Node 或 Complex Node**。产品文档 MAY 称其为 "Thinking Node"，但规范化 NWM `node_type` 仍然是 `"action"` 或 `"complex"`：

- 端点只运行模型 action 并返回结果时，用 `"action"`。
- 端点还拥有 memory、工具编排、图遍历、会话状态或其他组合逻辑时，用 `"complex"`。
- 节点 SHOULD 在 NDP/NIP `capabilities` 中声明 `llm:complete`；当 `profiles.llm.actions` 包含 `"llm.complete"` 时，MUST 实现 §7.5 的 `llm.complete` ActionFrame contract。
- 若 `supports_stream = true`，节点 SHOULD 同时声明 `llm:stream`；若 `supports_tools = true`，SHOULD 声明 `llm:tool_call`。
- 节点若声明 `context.supported = true`，MUST 同时声明 `llm:context`、列出已实现的生命周期 action，并完整实现 §7.6，禁止静默降级为 stateless。

**`profiles.llm` 字段**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `profile_version` | string | 可选 | LLM profile schema 版本。`"0.1"` 为 stateless profile；`"0.2"` 增加下方可选的有状态 context descriptor。 |
| `actions` | array[string] | 可选 | 本节点实现的标准 LLM action id。默认：`["llm.complete"]` |
| `provider` | string | 可选 | Provider/runtime 家族，如 `"willow"`、`"ollama"`、`"openai-compatible"`。仅作提示 |
| `default_model` | string | 可选 | 请求未提供 provider-specific 路由提示时使用的模型 id |
| `models` | array | 可选 | 模型描述对象，见下表 |
| `supports_stream` | bool | 可选 | `llm.complete` 是否接受 `stream=true` 并返回 StreamFrame 序列 |
| `supports_tools` | bool | 可选 | 是否支持 tool definition 与 tool-call response |
| `supports_json_mode` | bool | 可选 | 是否支持结构化 JSON/object completion |
| `supports_embeddings` | bool | 可选 | 是否支持 `llm.embed` 等 embedding action |
| `supports_rerank` | bool | 可选 | 是否支持 `llm.rerank` 等 rerank action |
| `reasoning_visibility` | string | 可选 | `"none"` / `"summary"` / `"trace"`。`trace` 会暴露 provider reasoning artifact，属于部署敏感能力 |
| `privacy` | object | 可选 | Operator 隐私策略提示，见下表 |
| `context` | object | 可选 | 有状态 LLM context 支持与运行限制；要求 `profile_version = "0.2"` 并实现 §7.6 |

**模型描述对象**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `id` | string | 必填 | `LlmCompleteActionRequest.model` 可接受的模型 id |
| `display_name` | string | 可选 | 人类可读模型名称 |
| `modalities` | array[string] | 可选 | 支持模态，如 `["text"]` 或 `["text","image"]` |
| `context_window` | uint32 | 可选 | 原生模型 token 计的最大输入上下文窗口 |
| `max_output_tokens` | uint32 | 可选 | 该节点允许的最大输出 token 数 |
| `tokenizer` | string | 可选 | 用于估算与 CGN hint 的 tokenizer id |
| `cgn_profile` | string | 可选 | 已知时，引用 `cgn-profiles.yaml` 中的 CGN 转换 profile id |

**隐私描述对象**

| 字段 | 类型 | 描述 |
|------|------|------|
| `retention` | string | Prompt/response 保留策略，如 `"none"` / `"session"` / `"30d"` |
| `training` | bool | Prompt/response 内容是否可用于模型训练 |
| `region` | string | 可选处理或存储区域提示 |

**有状态 context 描述符（`profiles.llm.context`）**

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `supported` | bool | 必填 | 本节点是否实现 §7.6 contract。`false` 不得被理解为部分支持。 |
| `operations` | array[string] | 支持时必填 | 已实现 operation：`create`、`append`、`fork`、`reset`、`release` 的子集。 |
| `persistence` | string | 支持时必填 | `connection`、`process` 或 `durable`，保证见 §7.6.5。 |
| `max_contexts_per_principal` | uint32 | 支持时必填 | 每个认证 principal/security scope 的最大活跃 context 数。 |
| `max_ttl_seconds` | uint32 | 支持时必填 | 接受的最大 idle TTL。 |
| `tombstone_seconds` | uint32 | 支持时必填 | 节点承诺 released/expired tombstone 可见的最短时间。 |

**示例**

```json
{
  "node_type": "action",
  "capabilities": { "query": false, "stream_query": false, "token_budget_hint": true },
  "actions": {
    "llm.complete": {
      "description": "Complete a chat conversation",
      "params_anchor": "nps:system:llm.complete:request",
      "result_anchor": "nps:system:llm.complete:response",
      "async": true,
      "required_capability": "llm:complete"
    },
    "llm.context.status": {
      "description": "Inspect an LLM context or recover an idempotent create outcome",
      "params_anchor": "nps:system:llm.context.status:request",
      "result_anchor": "nps:system:llm.context.status:response",
      "async": false,
      "required_capability": "llm:context"
    },
    "llm.context.release": {
      "description": "Release an LLM context",
      "params_anchor": "nps:system:llm.context.release:request",
      "result_anchor": "nps:system:llm.context.release:response",
      "async": false,
      "required_capability": "llm:context"
    }
  },
  "profiles": {
    "llm": {
      "profile_version": "0.2",
      "provider": "willow",
      "default_model": "willow-small",
      "actions": ["llm.complete", "llm.context.status", "llm.context.release"],
      "supports_stream": true,
      "supports_tools": true,
      "reasoning_visibility": "summary",
      "context": {
        "supported": true,
        "operations": ["create", "append", "fork", "reset", "release"],
        "persistence": "process",
        "max_contexts_per_principal": 32,
        "max_ttl_seconds": 3600,
        "tombstone_seconds": 86400
      },
      "models": [
        {
          "id": "willow-small",
          "modalities": ["text"],
          "context_window": 128000,
          "max_output_tokens": 8192,
          "tokenizer": "cl100k_base",
          "cgn_profile": "oa.reasoning"
        }
      ],
      "privacy": { "retention": "none", "training": false, "region": "us" }
    }
  }
}
```

### 4.3 auth 字段

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `required` | bool | 必填 | 是否要求身份认证 |
| `identity_type` | string | 条件必填 | `"nip-cert"` / `"bearer"` / `"none"` |
| `trusted_issuers` | array | 条件必填 | 受信任的 CA URL 列表（identity_type 为 nip-cert 时必填）|
| `required_capabilities` | array | 可选 | Agent MUST 持有的能力列表，如 `["nwp:query"]` |
| `scope_check` | string | 可选 | scope 校验模式：`"prefix"`（默认）/ `"exact"` |
| `ocsp_url` | string | 可选 | OCSP 验证端点 |

### 4.4 rate_limits 字段

| 字段 | 类型 | 描述 |
|------|------|------|
| `requests_per_minute` | uint32 | 每 Agent 每分钟最大请求数 |
| `requests_per_day` | uint32 | 每 Agent 每日最大请求数 |
| `max_concurrent_streams` | uint32 | 每 Agent 最大并发流数 |
| `max_subscriptions` | uint32 | 每 Agent 最大并发订阅数 |

### 4.4a sla 字段

参考性的 SLO 承诺。客户端（尤其是 marketplace / NeuronHub 聚合器）据此展示或过滤，协议层不做稽核。所有子字段均为可选。

| 字段 | 类型 | 描述 |
|------|------|------|
| `p95_latency_ms` | uint32 | 自声明的端到端 P95 延迟（毫秒），以节点 `/.nwm` 参考端点为测量点 |
| `availability` | string | 自声明的可用性目标，取十进制小数字符串，例如 `"0.999"` 表示「三个 9」。格式：`0\.[0-9]+`；缺省时客户端 SHOULD 按 best effort 理解。|
| `sla_tier` | string | 可选的 marketplace 挂牌档位名：`"best-effort"` / `"standard"` / `"premium"`。该枚举之外的自由字符串保留给将来扩展，当前客户端 SHOULD 忽略。|

### 4.4b billing 字段

参考性的商业元数据。协议层不授权、不计量、也不结算费用；本字段的存在是为了让 marketplace 挂牌、agent 自动扩缩容与预算闸门能在调用前做出知情决策。所有子字段均为可选。

| 字段 | 类型 | 描述 |
|------|------|------|
| `metering_profile` | string | 计量模型标识：`"free"` / `"metered"` / `"flat-rate"`。该枚举之外的自由取值为保留值，保守的客户端 SHOULD 按 `"metered"` 处理。|
| `billing_unit` | string | `metering_profile = "metered"` 时的计量单位，例如 `"per-token"` / `"per-request"` / `"per-cgn"` / `"per-second"`。|
| `price_hint` | string | 指示性价格，采用 ISO-4217 货币前缀 + 十进制数的形式，例如按 `billing_unit` 计的 `"USD 0.0002"`。仅为提示——以运营方的外部合同为准。|
| `currency` | string | ISO-4217 货币代码（例如 `"USD"`、`"EUR"`、`"CNY"`）。可选的便利字段；`price_hint` 本身已含货币前缀。|

### 4.4c alpha.19 可移植 metadata 归一化（NWP v0.22）

以下要求 ID 由
[`alpha19_hardening_vectors.json`](conformance/nwp/alpha19_hardening_vectors.json)
验证：

1. **NWP-P19-01 —— stability。** producer MUST 只发 `experimental`、`stable`
   或 `deprecated`；缺省归一化为 `stable`。consumer 保留未知 raw value，但在过滤时
   归一化为 `experimental`，且 MUST NOT 把它按 stable 排名。
2. **NWP-P19-02 —— SLA。** `p95_latency_ms` 若存在则为正 uint32；
   `availability` 为 `(0, 1]` 内的小数；三个标准 `sla_tier` 保持既定顺序。
   未知 tier 保留但不参与排名。无效 advisory 子字段被忽略并记录诊断，不使其他 manifest 字段失效。
3. **NWP-P19-03 —— billing。** `free` 禁止 `billing_unit`、`price_hint`、`currency`；
   `metered` 要求非空 `billing_unit`；`price_hint` MUST 匹配
   `^[A-Z]{3} [0-9]+(\\.[0-9]+)?$`；单独的 `currency` 若存在必须与其前缀一致。
   未知 metering profile 归一化为 `metered`，但不得虚构价格。
4. **NWP-P19-04 —— action override。** action 层 metadata 只覆盖它提供的字段；
   缺失字段回退到已归一化的 top-level 值。无效 override 子字段被忽略并回退，
   不得擦除合法 top-level 值。

### 4.5 NWM 完整示例

```json
{
  "nwp": "0.4",
  "node_id": "urn:nps:node:api.example.com:orders",
  "node_type": "complex",
  "display_name": "Order Management Node",
  "manifest_version": 7,
  "manifest_updated_at": "2026-06-03T00:00:00Z",
  "min_agent_version": "0.3",
  "wire_formats": ["ncp-capsule", "msgpack", "json"],
  "preferred_format": "ncp-capsule",
  "schema_anchors": {
    "order":   "sha256:a3f9b2c1...",
    "product": "sha256:b2c1d3e4..."
  },
  "capabilities": {
    "query": true,
    "stream_query": true,
    "aggregate": true,
    "subscribe": true,
    "subscribe_filter": true,
    "vector_search": false,
    "token_budget_hint": true,
    "ext_frame": false,
    "e2e_enc": false,
    "inline_anchor": true
  },
  "data_sources": ["rds:orders_db"],
  "auth": {
    "required": true,
    "identity_type": "nip-cert",
    "trusted_issuers": ["https://ca.mycompany.com"],
    "required_capabilities": ["nwp:query", "nwp:invoke"],
    "scope_check": "prefix"
  },
  "rate_limits": {
    "requests_per_minute": 300,
    "requests_per_day": 50000,
    "max_concurrent_streams": 10,
    "max_subscriptions": 5
  },
  "stability": "stable",
  "sla": {
    "p95_latency_ms": 250,
    "availability": "0.999",
    "sla_tier": "standard"
  },
  "billing": {
    "metering_profile": "metered",
    "billing_unit": "per-request",
    "price_hint": "USD 0.0002",
    "currency": "USD"
  },
  "actions": {
    "orders.create": {
      "description": "Create a new order",
      "params_anchor": "sha256:create_params...",
      "result_anchor": "sha256:order...",
      "async": true,
      "idempotent": true,
      "timeout_ms_default": 10000,
      "timeout_ms_max": 60000,
      "required_capability": "nwp:invoke",
      "stability": "stable",
      "sla": { "p95_latency_ms": 800, "sla_tier": "premium" },
      "billing": { "metering_profile": "metered", "billing_unit": "per-request", "price_hint": "USD 0.001" }
    },
    "orders.cancel": {
      "description": "Cancel an existing order",
      "params_anchor": "sha256:cancel_params...",
      "result_anchor": "sha256:cancel_result...",
      "async": false,
      "idempotent": true,
      "timeout_ms_default": 5000,
      "timeout_ms_max": 10000,
      "required_capability": "nwp:invoke",
      "stability": "experimental"
    }
  },
  "endpoints": {
    "query":     "nwp://api.example.com/orders/query",
    "stream":    "nwp://api.example.com/orders/stream",
    "invoke":    "nwp://api.example.com/orders/invoke",
    "subscribe": "nwp://api.example.com/orders/subscribe",
    "actions":   "nwp://api.example.com/orders/actions",
    "schema":    "nwp://api.example.com/orders/.schema"
  },
  "tokenizer_support": ["cl100k_base", "claude"]
}
```

**NWM 条件请求**

Agent SHOULD 缓存 NWM 并利用 `manifest_version` 做条件请求：HTTP 模式通过 `If-None-Match: <manifest_version>` 请求头（整数字符串，例如 `If-None-Match: 7`），服务端若清单未变更则返回 `304 Not Modified`。服务端 MUST 在每次 `GET /.nwm` 响应中携带 `X-NWM-Version: <manifest_version>`，使 Agent 无需整份重取即可发现缓存过期。`manifest_updated_at` 则提供最近一次结构性变更的人类可读时间戳。

### 4.6 NWM Action 注册表

`actions` 字段为 `{action_id: ActionSpec}` 字典，Action/Complex/Anchor Node MUST 在此声明所有可调用操作。

**ActionSpec 字段定义**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `description` | string | 可选 | 操作的人类可读说明 |
| `params_anchor` | string | 可选 | 参数 Schema 的 anchor_id（Agent 用于验证 ActionFrame.params）|
| `result_anchor` | string | 可选 | 结果 Schema 的 anchor_id（成功时 CapsFrame 使用此 anchor_ref）|
| `async` | bool | 必填 | 是否支持异步执行（true 时可在 ActionFrame 中设 async=true）|
| `idempotent` | bool | 可选 | 操作是否幂等（true 时 Agent 可安全重试）|
| `timeout_ms_default` | uint32 | 可选 | 默认超时毫秒数 |
| `timeout_ms_max` | uint32 | 可选 | 最大允许超时毫秒数 |
| `required_capability` | string | 可选 | 调用此操作所需的 NIP 能力，如 `"nwp:invoke"` |
| `min_assurance_level` | string | 可选 | per-action 保证级别覆盖：`"anonymous"` / `"attested"` / `"verified"`。存在时覆盖顶层 NWM `min_assurance_level`，仅对本操作生效。请求级别不足时 MUST 返回 `NWP-AUTH-ASSURANCE-TOO-LOW`。（NPS-RFC-0003）|
| `stability` | string | 可选 | per-action 生命周期阶段覆盖（`"experimental"` / `"stable"` / `"deprecated"`）。存在时对本 action 覆盖顶层 NWM `stability`。即使节点级 stability 为 `"stable"`，marketplace 客户端 SHOULD 仍然把 deprecated 的 action 显式呈现出来。|
| `sla` | object | 可选 | per-action SLO 覆盖；形状与顶层 `sla` 字段（§4.4a）相同。存在时，此处给出的字段仅对本 action 覆盖顶层同名字段；未给出的子字段回落到顶层取值。|
| `billing` | object | 可选 | per-action 商业元数据覆盖；形状与顶层 `billing` 字段（§4.4b）相同。字段级回落语义与 `sla` 一致。|

**`/actions` 端点**

Agent 发起 `GET /actions`，节点返回 NWM `actions` 字段的完整 JSON（便于动态发现，无需下载整个 NWM）：

```json
{
  "node_id": "urn:nps:node:api.example.com:orders",
  "actions": {
    "orders.create": { ... },
    "orders.cancel": { ... }
  }
}
```

---

## 5. Schema 获取流程

Agent 通过以下流程获取 Node 的 Schema（AnchorFrame 由 Node 发布，Agent 只读引用）：

```
Agent                              Node
  │                                  │
  │── GET /.nwm ─────────────────→   │  1. 读取清单，获取 schema_anchors
  │←── NWM JSON ──────────────────   │     { "order": "sha256:a3f9..." }
  │                                  │
  │── GET /.schema ──────────────→   │  2. 获取完整 AnchorFrame（按需）
  │←── AnchorFrame JSON ──────────   │     Agent 本地缓存
  │                                  │
  │── QueryFrame(anchor_ref) ────→   │  3. 查询只携带 anchor_ref
  │←── CapsFrame(anchor_ref) ─────   │
```

Agent SHOULD 在首次连接时预加载 NWM 中声明的所有 schema_anchors 对应的 AnchorFrame，减少后续请求延迟。

---

## 6. QueryFrame (0x10)

用于 Memory Node 的结构化数据查询。

### 6.1 字段定义

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `frame` | uint8 | 必填 | 固定值 `0x10` |
| `type` | string | 可选 | 保留查询类型标识，见 §12。设置后按对应类型的字段语义解释，`anchor_ref` 由该类型重新定义；不设置则按下方默认 per-anchor 查询行为 |
| `anchor_ref` | string | 条件必填 | Schema anchor_id；聚合查询可省略；`type` 选中的保留类型若不需要可省略 |
| `auto_anchor` | bool | 可选 | true 时若 anchor 过期，Node 在响应中自动附加最新 AnchorFrame，默认 true |
| `stream` | bool | 可选 | true 时触发流式查询模式（见 §6.6），响应为 StreamFrame 序列而非 CapsFrame |
| `aggregate` | object | 可选 | 聚合操作（见 §6.7）；设置后 `anchor_ref` 可省略 |
| `filter` | object | 可选 | 过滤条件，见 §6.2 |
| `fields` | array | 可选 | 返回字段列表；省略表示返回全部字段 |
| `limit` | uint32 | 可选 | 最大返回条数，默认 20，最大 1000；流式查询时为每帧最大条数 |
| `cursor` | string | 可选 | 分页游标，来自上一响应的 `next_cursor` |
| `order` | array | 可选 | 排序规则，见 §6.3 |
| `vector_search` | object | 可选 | 向量相似搜索，见 §6.4 |
| `token_budget` | uint32 | 可选 | CGN 预算上限（原生模式等价于 `X-NWP-Budget`）|
| `tokenizer` | string | 可选 | 使用的 tokenizer 标识（原生模式等价于 `X-NWP-Tokenizer`）|
| `depth` | uint8 | 可选 | 节点图谱遍历深度，默认 1，最大 5（原生模式等价于 `X-NWP-Depth`）|
| `request_id` | string | 可选 | UUID v4，用于请求追踪；节点在响应和日志中原样回传 |

### 6.2 Filter 语法

| 运算符 | 含义 | 示例 |
|--------|------|------|
| `$eq` | 等于 | `{ "status": { "$eq": "active" } }` |
| `$ne` | 不等于 | `{ "status": { "$ne": "deleted" } }` |
| `$lt` | 小于 | `{ "price": { "$lt": 500 } }` |
| `$lte` | 小于等于 | `{ "price": { "$lte": 500 } }` |
| `$gt` | 大于 | `{ "stock": { "$gt": 0 } }` |
| `$gte` | 大于等于 | `{ "rating": { "$gte": 4.0 } }` |
| `$in` | 在列表中 | `{ "category": { "$in": ["phone", "tablet"] } }` |
| `$nin` | 不在列表中 | `{ "tag": { "$nin": ["discontinued"] } }` |
| `$contains` | 字符串包含（大小写敏感）| `{ "name": { "$contains": "Pro" } }` |
| `$between` | 范围（含两端）| `{ "price": { "$between": [100, 500] } }` |
| `$exists` | 字段是否存在 | `{ "thumbnail": { "$exists": true } }` |
| `$regex` | 正则匹配（UTF-8）| `{ "sku": { "$regex": "^PROD-[0-9]{4}$" } }` |
| `$and` | 逻辑与 | `{ "$and": [ {...}, {...} ] }` |
| `$or` | 逻辑或 | `{ "$or": [ {...}, {...} ] }` |
| `$not` | 逻辑非 | `{ "$not": { "status": { "$eq": "deleted" } } }` |

**`$regex` 安全约束**：模式长度 ≤ 256 字符；禁止嵌套量词（如 `(a+)+`）；节点 MUST 做 ReDoS 检测，违规返回 `NWP-QUERY-REGEX-UNSAFE`。

Filter 嵌套深度 MUST ≤ 8 层。

### 6.3 排序规则

```json
{ "order": [{ "field": "price", "dir": "ASC" }, { "field": "name", "dir": "ASC" }] }
```

### 6.4 向量搜索

```json
{
  "vector_search": {
    "field": "embedding",
    "vector": [0.12, -0.34, 0.56],
    "top_k": 10,
    "threshold": 0.85,
    "metric": "cosine"
  }
}
```

支持 `metric`：`cosine`（默认）、`euclidean`、`dot_product`。节点通过 `capabilities.vector_search=true` 声明，不支持时返回 `NWP-QUERY-VECTOR-UNSUPPORTED`。

### 6.5 单次查询完整示例

```json
{
  "frame": "0x10",
  "anchor_ref": "sha256:a3f9b2c1...",
  "auto_anchor": true,
  "filter": {
    "$and": [
      { "category": { "$eq": "electronics" } },
      { "price": { "$lt": 500 } },
      { "stock": { "$gt": 0 } }
    ]
  },
  "fields": ["id", "name", "price", "stock"],
  "limit": 20,
  "order": [{ "field": "price", "dir": "ASC" }],
  "token_budget": 800,
  "tokenizer": "cl100k_base",
  "request_id": "550e8400-e29b-41d4-a716-446655440001"
}
```

### 6.6 流式查询协议

当 QueryFrame 中 `stream: true`（或使用 `/stream` 子路径）时，节点以 **StreamFrame (0x03) 序列**响应，而非单个 CapsFrame。需要节点 `capabilities.stream_query=true`。

**流式查询流程**

```
Agent                              Node
  │                                  │
  │── QueryFrame(stream:true) ────→  │
  │                                  │  分批查询，每批 limit 条
  │  ←── StreamFrame(seq=0) ───────  │  首帧，含 anchor_ref 和 estimated_total
  │  ←── StreamFrame(seq=1) ───────  │  后续帧，data 为下一批记录
  │       ...                        │
  │  ←── StreamFrame(is_last=true) ─ │  最终帧，is_last=true，data 可为空
```

**首帧附加字段（StreamFrame 扩展）**

流式查询时，首帧（seq=0）的 `data` 数组前 SHOULD 携带元数据帧头（通过 StreamFrame `error_code` 字段的对称扩展定义，或在节点实现中附加 `meta` 字段）：

| 字段 | 类型 | 描述 |
|------|------|------|
| `estimated_total` | uint64 | 符合 filter 条件的记录总估算数；-1 表示未知 |
| `request_id` | string | 回传 QueryFrame 中的 request_id |

**分页与流的关系**

- 流式查询不使用 `cursor`，记录按 `order` 连续推送，直到满足 `limit × 帧数` 或全量推送完毕
- Agent 如需提前终止，发送引用该 QueryFrame `request_id` 的 ErrorFrame，或直接断开连接。节点按 `request_id` 路由取消信号，且 MUST NOT 要求流式查询使用 SubscribeFrame 形态的取消消息。
- 节点 MUST 在连接断开后停止推送

### 6.7 聚合查询

当 QueryFrame 中包含 `aggregate` 字段时，节点返回聚合结果而非原始记录。需要节点 `capabilities.aggregate=true`。

**aggregate 字段定义**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `operations` | array | 必填 | 聚合操作列表，见下 |
| `group_by` | array | 可选 | 分组字段列表，如 `["category", "status"]` |
| `having` | object | 可选 | 分组后过滤（与 filter 语法相同，但字段名为 alias）|

**operation 元素**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `func` | string | 必填 | `COUNT` / `SUM` / `AVG` / `MIN` / `MAX` / `COUNT_DISTINCT` |
| `field` | string | 条件必填 | 聚合字段（`COUNT` 可省略，表示行数）|
| `alias` | string | 必填 | 结果字段名 |

**聚合查询示例**

```json
{
  "frame": "0x10",
  "filter": { "status": { "$eq": "active" } },
  "aggregate": {
    "operations": [
      { "func": "COUNT", "alias": "total" },
      { "func": "SUM",   "field": "price",  "alias": "revenue" },
      { "func": "AVG",   "field": "rating", "alias": "avg_rating" }
    ],
    "group_by": ["category"],
    "having": { "total": { "$gt": 10 } }
  },
  "order": [{ "field": "revenue", "dir": "DESC" }],
  "request_id": "550e8400-e29b-41d4-a716-446655440002"
}
```

**聚合响应（CapsFrame）**

聚合响应不使用业务 schema，`anchor_ref` 固定为 `"nps:system:aggregate:result"`：

```json
{
  "frame": "0x04",
  "anchor_ref": "nps:system:aggregate:result",
  "count": 3,
  "data": [
    { "category": "electronics", "total": 142, "revenue": 89450.00, "avg_rating": 4.3 },
    { "category": "clothing",    "total": 87,  "revenue": 12300.00, "avg_rating": 4.1 },
    { "category": "books",       "total": 56,  "revenue": 3200.00,  "avg_rating": 4.6 }
  ]
}
```

---

## 7. ActionFrame (0x11)

用于 Action Node 和 Complex Node 的操作调用。

### 7.1 字段定义

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `frame` | uint8 | 必填 | 固定值 `0x11` |
| `action_id` | string | 必填 | 操作标识符，格式：`{domain}.{verb}`；系统保留操作见 §7.3 |
| `params` | object | 可选 | 操作参数，Schema 由 NWM actions.{action_id}.params_anchor 定义 |
| `idempotency_key` | string | 可选 | 幂等键（UUID v4），有效期 24 小时 |
| `timeout_ms` | uint32 | 可选 | 超时毫秒数，默认 5000，最大 300000 |
| `async` | bool | 可选 | true 表示异步执行，响应返回 `task_id` |
| `callback_url` | string | 可选 | 异步任务完成时的回调 URL（`https://`）|
| `priority` | string | 可选 | 任务优先级：`"low"` / `"normal"`（默认）/ `"high"` |
| `request_id` | string | 可选 | UUID v4，用于请求追踪（回传至响应和 task status）|

### 7.2 异步任务状态机

```
PENDING → RUNNING → COMPLETED
                  ↘ FAILED
                  ↘ CANCELLED
```

异步执行时，初始响应（CapsFrame）：

```json
{
  "task_id": "uuid-v4",
  "status": "pending",
  "poll_url": "nwp://api.example.com/orders/actions/status/uuid-v4",
  "estimated_ms": 3000,
  "request_id": "550e8400-..."
}
```

### 7.3 系统保留操作

所有支持异步 Action 的节点 MUST 实现：

| action_id | 描述 | 必填参数 | 响应 |
|-----------|------|---------|------|
| `system.task.status` | 轮询任务状态 | `{ "task_id": "uuid" }` | 任务状态对象（见下）|
| `system.task.cancel` | 取消任务 | `{ "task_id": "uuid" }` | `{ "cancelled": true }` 或错误 |

**`system.task.status` 响应**

```json
{
  "task_id": "uuid-v4",
  "status": "running",
  "progress": 0.42,
  "created_at": "2026-04-14T10:00:00Z",
  "updated_at": "2026-04-14T10:00:05Z",
  "request_id": "550e8400-...",
  "result": null,
  "error": null
}
```

### 7.4 完整示例

```json
{
  "frame": "0x11",
  "action_id": "orders.create",
  "params": { "product_id": 1001, "quantity": 2 },
  "idempotency_key": "550e8400-e29b-41d4-a716-446655440000",
  "timeout_ms": 10000,
  "async": true,
  "callback_url": "https://agent.myapp.com/callbacks/nwp",
  "priority": "normal",
  "request_id": "550e8400-e29b-41d4-a716-446655440003"
}
```

### 7.5 标准 LLM Completion Action（`llm.complete`）

NWP 标准化 `llm.complete` action，使 SDK 和 agent runtime 不再需要为普通
模型 completion 自行维护私有 ActionFrame payload codec。
服务该 action 的节点 SHOULD 同时在 NWM `profiles.llm`（§4.2a）中声明 LLM/Thinking
Profile，使客户端在发起模型请求前即可发现模型列表、stream/tool 支持、隐私提示与
reasoning 暴露策略。

**请求绑定**

- wire frame 是 `ActionFrame`，且 `action_id = "llm.complete"`。
- `ActionFrame.params` MUST 包含 `LlmCompleteActionRequest` 对象。
- `params.kind` 出现时 MUST 为 `"llm.complete"`。生产者 SHOULD 发出该字段，
  用作自描述和兼容旧客户端；消费者 MAY 在 `action_id` 已是 `"llm.complete"`
  时接受缺省 `kind`。
- `ActionFrame.async = true` 表示异步执行。`params.stream = true` 表示立即
  返回 `StreamFrame` 序列。两者 MUST NOT 同时使用；服务端 SHOULD 以
  `NWP-ACTION-PARAMS-INVALID` 拒绝该组合。

**`LlmCompleteActionRequest` 字段**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `kind` | string | 可选 | 自描述字段，规范值为 `"llm.complete"` |
| `model` | string | 必填 | 接收端 LLM node 理解的模型标识 |
| `max_tokens` | uint32 | 可选 | 最大生成 token 数 |
| `stream` | bool | 可选 | 为 true 时，响应为 `StreamFrame` 序列，而不是同步 `CapsFrame` |
| `messages` | array | 必填 | 有序对话消息 |
| `tools` | array | 可选 | 可供模型使用的工具定义 |
| `context` | `LlmContextRequestDto` | 可选 | §7.6 定义的有状态 context operation；缺省时保持 stateless 完整历史语义。 |

**`LlmMessageDto` 字段**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `role` | string | 必填 | `"system"` / `"user"` / `"assistant"` / `"tool"` |
| `content` | string | 可选 | 消息文本或序列化后的工具结果 |
| `tool_call_id` | string | 可选 | `"tool"` 消息引用的 tool-call id |
| `tool_name` | string | 可选 | `"tool"` 消息引用的工具名 |
| `tool_calls` | array | 可选 | assistant 消息发出的工具调用 |

**工具字段**

`LlmToolCallDto` 使用 `{ "call_id", "tool_name", "arguments_json" }`。
`arguments_json` 是 JSON 字符串，用于保留 LLM 后端收到或发出的精确参数对象。
`LlmToolDefinitionDto` 使用 `{ "name", "description", "parameters" }`，
其中每个 `ToolParameterDto` 使用 `{ "name", "type", "description", "required" }`。
标准 `type` 值为 `"string"`、`"number"`、`"boolean"`、`"object"`、`"array"`。

**成功响应语义**

| 请求模式 | 成功响应 |
|---------|----------|
| `async=false`, `stream=false` | `CapsFrame`，`anchor_ref = "nps:system:llm.complete:response"`；ActionFrame 携带 `request_id` 时将其复制到响应；`data[0]` 为 `LlmCompleteActionResponse` |
| `async=true`, `stream=false` | `AsyncActionResponse` ack；任务完成后，`system.task.status.result` 为 `LlmCompleteActionResponse` |
| `stream=true` | `StreamFrame` 序列；首个 chunk 携带 `anchor_ref = "nps:system:llm.complete:stream"`，`data[]` 为 `LlmCompleteStreamChunkDto` |

Fire-and-forget 不是 `llm.complete` 的独立模式。客户端若不关心结果，MAY
提交异步请求并忽略 poll URL，但服务端仍遵循异步任务契约。

**`LlmCompleteActionResponse` 字段**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `stop_reason` | enum | 必填 | `"end_turn"` / `"tool_use"` / `"tool_calls"` / `"max_tokens"` / `"length"` / `"error"` |
| `content` | string | 可选 | 非工具 completion 的最终生成文本 |
| `tool_calls` | array | 可选 | 模型请求的工具调用 |
| `error` | string | 可选 | 模型/提供商层 completion error，见下方错误规则 |
| `usage` | object | 可选 | 本次调用的实际模型/provider 用量，见下方 `LlmUsageDto` |
| `context` | `LlmContextReceiptDto` | 可选 | 已提交的有状态 context receipt；stateless 请求必须省略。 |

流式响应中，`LlmCompleteStreamChunkDto.content_delta` 携带该 chunk 的新增文本。
最终 chunk SHOULD 设置 `stop_reason`；异常终止 SHOULD 使用 terminal
`ErrorFrame` 或 `StreamFrame.error_code`。
终止 chunk MAY 携带 `usage`。成功 stateful stream 的 context receipt 只允许出现
在终止 chunk；非终止 chunk MUST 省略 receipt，并 SHOULD 省略 usage。

**`LlmUsageDto` 字段**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `input_tokens` | uint32 | 可选 | 模型逻辑输入 token 总量，包含被复用的 prefix |
| `output_tokens` | uint32 | 可选 | 本次 completion 由模型生成的 token 数 |
| `cache_hit` | bool | 可选 | 模型 runtime 是否复用了 prefix/KV cache 条目 |
| `reused_tokens` | uint32 | 可选 | 从 prefix/KV cache 复用且未重新计算的输入 token 数 |
| `evaluated_tokens` | uint32 | 可选 | 本次调用中由模型新计算的输入 token 数 |
| `wire_input_bytes` | uint64 | 可选 | 在 NWP decoder 边界实测的完整序列化 ActionFrame payload 字节数；位于 NCP 解密后，并排除 NCP header/TLS/响应字节 |

不同 provider 暴露的计量粒度不同，因此所有 usage 字段均为可选。`usage` 中的值
MUST 是 runtime/provider 的实际观测值，不得填入估算值。`CapsFrame.token_est` 仍是
CGN 估算，不能替代 `usage`。`CapsFrame.cached` 表示完整 NWP 响应命中服务端响应缓存，
与 `usage.cache_hit` 不同。三个输入计量字段都已知时，生产端 SHOULD 满足
`reused_tokens + evaluated_tokens = input_tokens`。
只有 `reused_tokens > 0` 时才可令 `cache_hit = true`。`wire_input_bytes` MUST
来自已接纳 wire payload 的实测值，不得通过重新序列化 DTO 获得。Provider 无法
观测的 token 字段必须省略，不得按 prompt 长度推导。

**错误规则**

协议、校验、鉴权、超时、provider dispatch 失败 SHOULD 按正常 NWP 错误映射
返回 `ErrorFrame`。`LlmCompleteActionResponse.error` 仅用于“模型层失败本身是
一次成功 action result”的情况，例如 provider 返回结构化的 model refused/error
completion，而非抛出传输或服务端错误。

**字段命名与编码**

规范 JSON 字段名为 snake_case。SDK 生产者 MUST 发出 snake_case。SDK 消费者
SHOULD 兼容 PascalCase/大小写不敏感属性名。MessagePack payload map MUST 使用
与 JSON 相同的规范 snake_case key。

### 7.6 有状态 LLM Context 与增量 Completion

本节落实 NPS-CR-0011。它是 §7.5 的 opt-in 扩展，不新增 frame family。不含
`LlmCompleteActionRequest.context` 的请求保持 stateless，发送完整有序消息列表。
携带 `context` 的请求必须精确执行所请求的状态迁移或返回 ErrorFrame；服务端
MUST NOT 静默重建 stateless prompt，也不得报告虚假 cache hit。

#### 7.6.1 Request 与 receipt DTO

Stateful `llm.complete` ActionFrame MUST 携带 `idempotency_key`。`messages` 在
`create`/`reset` 中表示完整初始/替换 transcript，在 `append`/`fork` 中只表示
`base_version` 之后的有序增量。只有 `fork` 允许空消息数组。

**`LlmContextRequestDto`**

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `operation` | enum | 必填 | `create`、`append`、`fork` 或 `reset`。 |
| `context_id` | string | 条件必填 | 除 `create` 外均必填；`create` 禁止。 |
| `base_version` | uint64 | 条件必填 | `append`、`fork`、`reset` 必填，MUST 等于已提交版本。 |
| `ttl_seconds` | uint32 | 可选 | 请求 idle TTL；服务端 MAY 收窄到广告上限。 |

`ttl_seconds` 出现时必须大于零。省略时，`create` 使用节点默认值；`append` 与
`reset` 保留 context 当前的实际 TTL；`fork` 继承源 context 的剩余 TTL。显式值、
继承值或默认值都 MAY 被收窄到 `max_ttl_seconds`；TTL 有界时，receipt MUST 返回
实际 expiry。

Context ID 是大小写敏感、无 padding 的 base64url 字符串，使用至少 128 bit
密码学安全随机数生成。生产者 MUST 发出 22–128 个 `[A-Za-z0-9_-]` ASCII 字符。
ID 不得编码 NID、tenant、model、数据库 key 或其他敏感 metadata。它只是 locator，
不是授权凭据。

**`LlmContextReceiptDto`**

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `context_id` | string | 必填 | 不透明 context 标识。 |
| `version` | uint64 | 必填 | operation 后的已提交版本；create/fork 从 1 开始，append/reset/release 恰好递增一次。 |
| `operation` | enum | 必填 | `create`、`append`、`fork`、`reset` 或 `release`。 |
| `state` | enum | 必填 | completion mutation 为 `active`；release 为 `released`。 |
| `expires_at` | RFC 3339 timestamp | 可选 | 有界时的实际 idle expiry。 |
| `parent_context_id` | string | fork 必填 | 源 context。 |
| `parent_version` | uint64 | fork 必填 | fork 使用的不可变源版本。 |

Unary success 在 `LlmCompleteActionResponse.context` 携带 receipt。Async ack 不
提交 context；receipt 只出现在已完成的 `system.task.status.result`。Streaming
success 只在终止 chunk 携带 receipt。Stateless response 与非终止 chunk MUST
省略 receipt。

#### 7.6.2 生命周期 action

| Action | Request DTO | 成功响应 |
|--------|-------------|----------|
| `llm.context.status` | `LlmContextStatusRequestDto`，`context_id` / `idempotency_key` 恰好出现一个 | CapsFrame `anchor_ref = "nps:system:llm.context.status:response"`，`data[0] = LlmContextStatusDto` |
| `llm.context.release` | `LlmContextReleaseRequestDto`，含 `context_id`、`base_version`；ActionFrame MUST 携带 `idempotency_key` | CapsFrame `anchor_ref = "nps:system:llm.context.release:response"`，`data[0] = LlmContextReceiptDto` |

`LlmContextStatusDto` 包含必填 `state`（`busy`、`active`、`released`、`expired`、
`failed`），以及可选 `context_id`、`version`、`expires_at`、`request_id`、
`error_code`。Active/released/expired 必须携带 ID 与 version。运行中的 create
报告 `busy`，但 commit 前必须省略 ID/version；失败 create 报告 `failed`、省略
ID/version 并携带终结错误码。Status 只观察状态，MUST NOT 刷新 TTL。

Release 把 vN 递增为 released tombstone vN+1。相同 owner 与 ActionFrame key
replay 时幂等。对 released context 的 mutation 返回
`NWP-LLM-CONTEXT-NOT-FOUND`；owner 在广告 retention window 内仍可用 status
查看 tombstone。

#### 7.6.3 Binding 与授权

Create/reset 把 context 绑定到解析后的 model ID、有序 system message、canonical
tool definition，以及 prefix 复用所需 provider/runtime compatibility revision。
Append/fork MUST 保持 binding；delta 禁止 system-role message。省略 `tools` 表示
复用绑定定义，出现时必须 canonical 相等。必填 `model` 必须解析到绑定模型。
不一致返回 `NWP-LLM-CONTEXT-BINDING-MISMATCH`。

Stateful `llm.complete` mutation 同时要求 `llm:complete` 与 `llm:context`；
streaming/tools 仍分别额外要求 `llm:stream` / `llm:tool_call`。Status/release
要求 `llm:context` 加 owner 授权，但不要求 caller 继续持有模型调用权。有状态
coordinator API 必须在每次 admission 与 commit 检查时，将完整的必需 capability
集合传给部署侧 NIP authorizer。未安装 authorizer 的 coordinator 必须以
`NWP-LLM-CONTEXT-FORBIDDEN` fail closed，不得把缺少 hook 当作已授权。Owner 是认证 NID 加节点认证后的
tenant/workspace security scope；scope 来自已接纳身份与部署 policy，不得来自
客户端可控 context 字段。每次 operation MUST 重做 NIP expiry、revocation、
assurance、scope、capability 检查；长时间工作还 MUST 在 commit 前复检。认证
non-owner 返回 `NWP-LLM-CONTEXT-FORBIDDEN`。

#### 7.6.4 原子状态迁移

Create 提交 v1。Append/reset 对 `base_version` 做 CAS 并提交 vN+1。Fork 在
admission 时原子读取 parent 快照，创建 child v1，且永不修改 parent；之后 parent
mutation 不使已接纳 fork 快照失效。每个 context 同时至多一个 mutation
reservation；陈旧/并发失败方收到 `NWP-LLM-CONTEXT-VERSION-CONFLICT`，
ErrorFrame hint 携带当前版本。

Completion mutation 只有在非 `error` stop reason 的成功终结结果后才提交；提交
同时包含请求 delta 与终结 assistant text/tool calls。以普通 `end_turn` 表示的
结构化 refusal MAY 提交。校验/授权/provider failure、timeout、cancel、
`stop_reason = "error"` 或异常 stream 终止 MUST 保持旧 transcript/version 不变，
并释放 reservation。

服务端必须先按 NWM 广告的 persistence level 原子提交，再暴露终结 receipt。有效
reservation 运行期间阻止 idle expiry。成功 create/append/fork/reset 开始或刷新
TTL；status 与失败/取消 mutation 不刷新。若工作期间旧 TTL 已过期且 reservation
abort，context 直接转为 expired。自动 expiry 记录最后已提交版本，不递增。

#### 7.6.5 重连、重启与幂等

NWM persistence 值语义如下：

- `connection`：只在创建它的 NCP 连接上跨请求存活；
- `process`：仅在重连仍路由到同一进程时存活，不跨进程重启或其他实例；
- `durable`：在同一 logical node NID 与 endpoint identity 下跨进程重启存活。

Connection/process context 只在 node instance 范围内有效，客户端 SHOULD 固定到
创建 endpoint。本节不定义跨实例 migration/分布式 context store。状态丢失返回
not-found/expired，MUST NOT 创建替代 context。

现有 24 小时 ActionFrame idempotency window 同时保留 owner-scoped
key-to-outcome，即使 context TTL 更短。首个请求仍运行时，duplicate 返回
`NWP-ACTION-IDEMPOTENCY-CONFLICT`，MUST NOT 加入 live stream。完成后的
unary/async 使用 cached-result 规则。Streaming replay 使用新的 StreamFrame
sequence，返回逻辑一致的有序 text、tool calls、stop reason、usage 与终结 receipt，
不得重新生成或 commit。按 idempotency key 调用 `llm.context.status` 可恢复丢失的
create response；24 小时后 key lookup MAY 返回 not-found。

#### 7.6.6 Expiry、限制与错误

Released/expired tombstone SHOULD 对 owner 至少保留 `tombstone_seconds`。对
expiry tombstone 的 mutation 返回 `NWP-LLM-CONTEXT-EXPIRED`；移除 tombstone 后
返回 not-found。

| 错误 | NPS status | 含义 |
|------|------------|------|
| `NWP-CHALLENGE-INVALID` | `NPS-CLIENT-BAD-FRAME` | Challenge 输入无效 |
| `NWP-CHALLENGE-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | 未知或无权访问的 challenge handle |
| `NWP-CHALLENGE-BINDING-CONFLICT` | `NPS-CLIENT-CONFLICT` | 同操作 key 对应不同摘要 |
| `NWP-CHALLENGE-NOT-APPROVED` | `NPS-CLIENT-CONFLICT` | Challenge 未批准执行 |
| `NWP-CHALLENGE-EXPIRED` | `NPS-CLIENT-CONFLICT` | Challenge 在执行 claim 前过期 |
| `NWP-CHALLENGE-STATE-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | 持久 challenge 状态不可用 |
| `NWP-FEATURE-REQUIREMENTS-INVALID` | `NPS-CLIENT-BAD-FRAME` | HTTP 必需特性头无效或重复 |
| `NWP-FEATURE-REQUIRED-UNSUPPORTED` | `NPS-CLIENT-CONFLICT` | 派发前无法满足精确必需特性 |
| `NWP-LLM-CONTEXT-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | 未知/released context，或 idempotency lookup 已过期。 |
| `NWP-LLM-CONTEXT-EXPIRED` | `NPS-CLIENT-GONE` | Idle-expiry tombstone 仍存在。 |
| `NWP-LLM-CONTEXT-VERSION-CONFLICT` | `NPS-CLIENT-CONFLICT` | 版本陈旧或存在并发 mutation reservation。 |
| `NWP-LLM-CONTEXT-BINDING-MISMATCH` | `NPS-CLIENT-CONFLICT` | model/system/tools/runtime binding 不一致。 |
| `NWP-LLM-CONTEXT-FORBIDDEN` | `NPS-AUTH-FORBIDDEN` | Caller 非 owner，或缺少 scope/capability。 |
| `NWP-LLM-CONTEXT-LIMIT-EXCEEDED` | `NPS-LIMIT-RESOURCE` | 达到每 principal 活跃 context 上限。 |
| `NWP-LLM-CONTEXT-OPERATION-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | 节点支持 context，但不支持该 operation。 |

字段组合不合法使用 `NWP-ACTION-PARAMS-INVALID`。本节所有失败均走 ErrorFrame
或 terminal stream error，不得使用 `LlmCompleteActionResponse.error`。

#### 7.6.7 合规与 benchmark 声明

六 SDK MUST 执行 `conformance/nwp/llm_context_vectors.json`。声称 stateful 节省的
benchmark 必须在 stateless 与 strict-native stateful 模式下比较同一组多轮
role/tool 语义。第二轮 stateful MUST 只发送 delta，并同时证明更低的
`wire_input_bytes` 与 runtime 实测 `evaluated_tokens`。MUST 禁用协议 fallback。

---

### 7.7 受治理操作 Challenge（NWP v0.23）

以下规则定义 experimental 的 `nps.governed_challenge` revision 1，
依赖 `nps.production_runtime` revision 1。使用投影时另要求 context revision 1。
这些 profile 不替代产品审批策略，不新增帧类型。

#### 7.7.1 初始操作和绑定

要求认证主体、逻辑 audience/target、action ID 和 1..128 UTF-8 字节非空幂等 key。
使用现有 ActionFrame。先验证参数和授权，缺 key 则不创建状态或副作用。

绑定 authority、subject、action、audience、冻结的已接受参数、操作 key、expiry、
选定 feature revisions。摘要遵循 NIP §7.8 / NWP §9.7，即使 native 无 projection header 也一样。
Handle/proof 不纳入原始业务摘要，冻结参数不可变。

持久操作 key 是 (authority,subject,audience,action,idempotency_key)。
同 key 同摘要返回原 pending/terminal 操作；同 key 不同摘要冲突，不新建 challenge。
每次调用（包括取结果）重新授权。返回 handle 前，操作记录和 challenge 原子持久化。

#### 7.7.2 响应

标准 Caps.data 承载已注册 Anchor schema 的 profile record，不能用任意 JSON 替代 Caps。
HTTP pending 可返回 202；native 使用相同 record：

| 字段 | 规则 |
|---|---|
| challenge_id | 32 随机字节的无填充 base64url |
| state | pending / approved / executing / completed / denied / expired / superseded / indeterminate |
| action_id | 原操作 ID |
| audience | 原逻辑目标 |
| request_digest | sha256: 加 64 小写 hex |
| expires_at | Unix 秒整数，限制执行准入 |
| methods | 最多 8 个唯一产品方法 ID，每项最多 64 ASCII 字节 |
| operation_id | opaque 服务端 ID，最多 128 字节 |
| result | 仅 completed 时携带原 action 完整 typed Caps 值 |
| error | 有界操作/profile 错误，不含审批凭据 |

主体/authority 绑定存服务端，不泄漏私有策略。默认 TTL 300 秒，上限 900，服务端时钟
权威。终态后结果/去重至少保留 86400 秒并公布实际窗口；窗口外不自动重试。

#### 7.7.3 协议动作

注册 nwp.challenge.status、nwp.challenge.resume、nwp.challenge.cancel，
使用现有 action discovery、ActionFrame、Caps。Params 仅含 challenge_id，
服务端读取冻结原操作。不传 OTP、审批 token、密码。要求原 subject/audience 和产品许可。

status 无副作用；resume 仅 approved 时，在重查当前 grant/revocation/scope、摘要、
expiry 后原子 claim approved -> executing。重复 resume 返回同一状态/结果，不重复执行。
cancel 原子地将 pending/approved -> denied；executing 后只报告现状，不声称回滚。
未知或错误 subject handle 返回同样的 not-found，知道 handle 不足以读取/审批/执行。

产品认证审批流程负责 pending -> approved/denied，不能改参数/绑定。
Superseded 结束旧 challenge；不同参数用新逻辑操作/key。
省略本 profile 不能绕过产品对受治理操作的审批要求。

#### 7.7.4 状态和执行所有权

合法转移：pending -> approved/denied/expired/superseded；
approved -> executing/denied/expired/superseded；executing -> completed/indeterminate。
Completed 可保存已知成功或失败。审批本身不执行；expiry 限制 claim，不回滚 executing。
终态不可变，只有可信 reconciliation 可依据持久证据将 indeterminate -> completed，
不能自动重复 mutation。

Claim 与操作记录原子提交。副作用同库则与结果一并事务提交；外部系统则使用相同操作
身份的幂等 sink，否则崩溃后维持 indeterminate。一次 claim 不证明一次外部 effect。
并发、重启和 Anchor failover 共享操作存储/fencing；状态不可用则安全拒绝。

#### 7.7.5 错误和限制

| 错误 | 状态 | HTTP |
|---|---|---|
| NWP-CHALLENGE-INVALID | NPS-CLIENT-BAD-FRAME | 400 |
| NWP-CHALLENGE-NOT-FOUND | NPS-CLIENT-NOT-FOUND | 404 |
| NWP-CHALLENGE-BINDING-CONFLICT | NPS-CLIENT-CONFLICT | 409 |
| NWP-CHALLENGE-NOT-APPROVED | NPS-CLIENT-CONFLICT | 409 |
| NWP-CHALLENGE-EXPIRED | NPS-CLIENT-CONFLICT | 409 |
| NWP-CHALLENGE-STATE-UNAVAILABLE | NPS-SERVER-UNAVAILABLE | 503 |

默认每 subject 最多 16、全局 4096 个 pending/approved。容量拒绝先于状态/副作用分配。
Status/resume/cancel 使用节点 rate policy。日志不得包含审批 proof、投影或业务参数。

#### 7.7.6 Schema、保留期与响应边界

Authority 指接收方配置的逻辑操作所有者命名空间，在副本和网关路由间保持稳定，
不是紧邻投影 issuer/key。切换入口网关不得创建第二个去重命名空间。
原摘要只排除 profile 生成的 challenge 元数据；名为 challenge_id 等的业务 params
仍属于签名正文，不能按字段名任意剔除。

管理动作必须拒绝 callback_url 和 async=true。其 timeout/priority 只控制管理请求，
不得替换冻结原操作字段；管理幂等 key 不得替换已存原操作 key。
服务端 now >= expires_at 时必须拒绝执行 claim；初始过期时间必须晚于创建时间，
且不超过创建后 900 秒。

ActionFrame 既有 UUID-v4 幂等 key 约束仍适用；1..128 字节上限不放宽该规则。
服务通过 NWM `profiles.governed_challenge = {"revision":1,"retention_seconds":86400}`
声明持久保留期，retention_seconds 为至少 86400 的 uint32；变更时更新 manifest version。
本地有限存储无法保留已有承诺时必须拒绝新操作，不得提前驱逐记录。

固定 Anchor 定义见 [challenge-schemas.json](conformance/alpha20/challenge-schemas.json)。
三个管理动作的 params schema 仅含 challenge_id；响应为 count=1 的标准 Caps，
其 data[0] 为完整 challenge record。result 和 error 不适用时显式为 null；
result 非 null 时是原操作的标准 Caps 对象，不是任意替代 envelope。
completed 的已知成功必须有 result 且 error=null；已知失败必须有 error 且 result=null。
其他状态 result 必须为 null；既有帧/响应上限仍适用。

Record 另有 `retained_until`（Unix 秒 uint64，可空）：终态必须填写，
且至少为终态提交时间加所承诺保留期；其他状态为 null。可信 reconciliation
不得缩短已承诺期限。所有时间值不超过 9007199254740991。
Methods ID 匹配 `[a-z][a-z0-9_.:-]{0,63}`；operation_id 为 1..128 UTF-8 字节。
Action/audience/digest 采用 §9.7 和 NIP §7.8 的边界。

初始治理调用只创建/返回待审批或已审批记录，不执行原 mutation。
status 返回当前记录；resume 在 pending/denied/superseded 返回 NOT-APPROVED，
expired 返回 EXPIRED；executing/completed/indeterminate 返回原状态/结果。
这些拒绝使用本节对应 NWP-CHALLENGE-* 错误。未知或错误主体仍统一 NOT-FOUND。
HTTP pending/approved/executing 使用 202，其余记录使用 200；错误使用错误表映射。
副作用确定性取自 state/result/error，不得仅按 HTTP 成功状态推断。

---

## 8. SubscribeFrame 概览 (0x12)

用于在 Memory Node 与 Anchor Node 上建立变更订阅。权威的 v0.13 线格式定义在 §13（CR-0006）：`subscription_id`、`filter`、`heartbeat_interval_ms`、`max_events` 与不透明 `cursor`。

更早的 alpha 草案曾使用 `action`、`stream_id`、`heartbeat_interval` 与 `resume_from_seq`。这些名称在 NWP v0.13 已废止，合规的 alpha.11+ 生产者 MUST NOT 再发出它们。消费者 MAY 仅将其作为 pre-alpha.11 的兼容回退接受，但 MUST 在内部归一化为 §13 定义的字段。

### 8.1 HTTP 模式下的订阅（SSE）

```
POST /nwp/orders/subscribe HTTP/1.1
Content-Type: application/nwp-frame

[SubscribeFrame bytes]

HTTP/1.1 200 OK
Content-Type: text/event-stream

data: [DiffFrame bytes, base64]

data: [DiffFrame bytes, base64]
```

---

## 9. HTTP 头（HTTP 模式）

### 9.1 请求头

| 头 | 必填 | 描述 |
|----|------|------|
| `X-NWP-Agent` | 条件必填 | Agent NID（auth.required=true 时必填）|
| `Authorization` | 条件必填 | `auth.identity_type = "bearer"` 时的 HTTP bearer 凭据。原生模式不使用；原生模式的认证通过 NCP/NIP IdentFrame 绑定到会话。|
| `X-NWP-Budget` | 可选 | CGN 预算上限（uint32）|
| `X-NWP-Tokenizer` | 可选 | Agent 使用的 tokenizer 标识 |
| `X-NWP-Depth` | 可选 | 节点图谱遍历深度，默认 1，最大 5 |
| `X-NWP-Encoding` | 可选 | 请求编码 Tier：`json`/`msgpack`，默认 `msgpack` |
| `X-NWP-Request-ID` | 可选 | UUID v4，请求追踪 ID；节点在响应头中原样回传 |
| `If-None-Match` | 可选 | NWM 条件请求；值为 `manifest_version` |
| `Content-Type` | 必填 | `application/nwp-frame` |

### 9.2 响应头

| 头 | 描述 |
|----|------|
| `X-NWP-Schema` | 响应使用的 anchor_id |
| `X-NWP-Tokens` | 实际 CGN 消耗 |
| `X-NWP-Tokens-Native` | 原生 token 消耗 |
| `X-NWP-Tokenizer-Used` | 实际使用的 tokenizer |
| `X-NWP-Cached` | `true` 表示命中缓存 |
| `X-NWP-Node-Type` | 节点类型 |
| `X-NWP-Request-ID` | 回传请求方的 `X-NWP-Request-ID`（若未提供，节点 MAY 自动生成）|
| `X-NWP-Rate-Limit` | 每分钟请求上限 |
| `X-NWP-Rate-Remaining` | 本分钟剩余请求数 |
| `X-NWP-Rate-Reset` | 限速窗口重置时间（Unix 时间戳）|
| `Content-Type` | `application/nwp-capsule`（正常响应）/ `application/nwp-error+json`（错误响应）|

### 9.3 原生模式字段映射

| HTTP 头 | QueryFrame 字段 | ActionFrame 字段 |
|---------|----------------|-----------------|
| `X-NWP-Agent` | — (HelloFrame `agent_id` 握手时已声明) | 同左 |
| `X-NWP-Budget` | `token_budget` | — |
| `X-NWP-Tokenizer` | `tokenizer` | — |
| `X-NWP-Depth` | `depth` | — |
| `X-NWP-Request-ID` | `request_id` | `request_id` |

### 9.4 HTTP 模式错误响应格式

HTTP 模式下，错误响应使用以下格式，`Content-Type: application/nwp-error+json`：

```json
{
  "status": "NPS-CLIENT-NOT-FOUND",
  "error": "NWP-ACTION-NOT-FOUND",
  "message": "Action 'orders.ship' is not registered on this node",
  "details": { "action_id": "orders.ship" },
  "request_id": "550e8400-e29b-41d4-a716-446655440003"
}
```

HTTP 状态码由 NPS 状态码映射决定，见 [status-codes.cn.md](status-codes.cn.md)。

**字段说明**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `status` | string | 必填 | NPS 状态码 |
| `error` | string | 必填 | 协议级错误码（如 `NWP-ACTION-NOT-FOUND`）|
| `message` | string | 可选 | 人类可读描述 |
| `details` | object | 可选 | 结构化错误附加信息 |
| `request_id` | string | 可选 | 回传请求中的 `X-NWP-Request-ID` |

### 9.5 HTTP Binding 拒绝错误码

HTTP overlay 实现 MUST 在请求体被接受为 NWP frame 之前，对传输绑定前置条件使用规范化 NWP 错误码。这些错误归属 NWP，因为它们决定 HTTP exchange 是否能恢复出 NWP `QueryFrame`、`ActionFrame` 或 `SubscribeFrame`；它们不是实现本地 adapter 私有码。

| 拒绝类型 | 触发条件 | 错误码 | NPS 状态码 |
|----------|----------|--------|------------|
| Origin 被拒绝 | 面向浏览器的 HTTP binding 因 CORS 或等价 origin policy 拒绝请求来源 | `NWP-HTTP-ORIGIN-FORBIDDEN` | `NPS-AUTH-FORBIDDEN` |
| Content-Type 不支持 | 请求体 media type 不是受支持的 NWP frame media type | `NWP-HTTP-CONTENT-TYPE-UNSUPPORTED` | `NPS-CLIENT-BAD-FRAME` |
| Accept 无法满足 | `Accept` 拒绝了节点能返回的所有 response media type | `NWP-HTTP-ACCEPT-UNSATISFIABLE` | `NPS-CLIENT-BAD-PARAM` |
| Request-id 回传不匹配 | 客户端观察到响应 `X-NWP-Request-ID` 未回传请求头中的值 | `NWP-HTTP-REQUEST-ID-MISMATCH` | `NPS-CLIENT-BAD-PARAM` |
| Frame body 无法解析 | HTTP body 不能解析为任何受支持的 NWP frame envelope 或 NCP 承载的 NWP frame | `NWP-HTTP-FRAME-BODY-MALFORMED` | `NPS-CLIENT-BAD-FRAME` |
| Body 过大 | 请求 body 超出服务端公告或配置的 NWP body 上限 | `NWP-HTTP-BODY-TOO-LARGE` | `NPS-LIMIT-PAYLOAD` |
| 已声明但未实现 | NWM 在 discovery 中声明了某 capability 或 profile，但节点当前无法服务 | `NWP-CAPABILITY-ADVERTISED-UNIMPLEMENTED` | `NPS-SERVER-UNSUPPORTED` |

`NWP-CAPABILITY-ADVERTISED-UNIMPLEMENTED` 与 `NWP-QUERY-VECTOR-UNSUPPORTED` 等能力专用 unsupported 错误不同：manifest 如实声明不支持该能力时使用专用错误；只有在 rollout 窗口、后端被禁用或 discovery 状态不一致，导致“已声明但无法服务”时，才使用 `NWP-CAPABILITY-ADVERTISED-UNIMPLEMENTED`。

---

### 9.6 依赖特性的 HTTP 准入（NWP v0.23）

特性 ID、revision 描述符及生命周期策略遵循 NCP §2.6.3。
feature-registry.json 中的必需依赖也必须显式列入请求的精确特性对集合；
依赖缺失或不兼容时，在派发前以 NWP-FEATURE-REQUIRED-UNSUPPORTED 拒绝。
以下要求按需启用，但理解 profile 的入口对基础操作中的畸形特性头也必须拒绝。

**NWP-FTR-01 — 声明。** NWM 可提供
`profiles.feature_negotiation = {"version":1,"feature_offer":[...]}`。
该 profile 原始 JSON 值不得超过 16384 UTF-8 字节，同时受现有 manifest 上限约束。
Offer 改变时必须更新 manifest version。声明覆盖服务入口，更窄的 action 支持
必须在副作用前重新验证。发现不是准入或授权。

**NWP-FTR-02 — 精确 Header。** 依赖特性的请求必须携带单个 ASCII
`X-NPS-Required-Features`：按 ID 升序排列的逗号分隔 `id=revision`，
不得含空白、重复或前导零，最多 32 对、4096 字节。
必须在框架合并前拒绝重复 header 实例。每个 revision 对该操作都是精确且必需的。
语法无效映射到 `NWP-FEATURE-REQUIREMENTS-INVALID` / HTTP 400。

**NWP-FTR-03 — 派发门槛。** 入口必须在调用处理器前检查当前启用 revision、
依赖、具体操作支持及认证准入策略。不支持或已撤销时，以
`NWP-FEATURE-REQUIRED-UNSUPPORTED` / HTTP 409 拒绝，处理器调用数为零。
特性声明不得扩大已准入权限。网关每跳重新验证；原生和 HTTP 的选择互相独立。
上下文投影的请求摘要必须绑定精确必需特性集合。

**NWP-FTR-04 — 路由保证。** 客户端发送依赖特性的操作前，必须从实际服务该操作
的认证路由取得明确的 version-1 声明。路由必须对每个可能后端强制准入，
或在转发前拒绝；混合新旧路由在能保证此前不得声明支持。
无法确认保证时，客户端必须本地失败，不得重定向/降级到未验证路由。
旧服务忽略 header 不是安全回退，事后响应检查不能阻止副作用。

**NWP-FTR-05 — 响应。** 成功响应必须在 `X-NPS-Selected-Features` 中回显
精确接受集合；失败响应不得声明选择成功。成功回显缺失、畸形或不匹配，是操作结果
不确定的客户端协议错误，绝不构成重试 mutation 的许可。
未使用 profile 的调用省略必需项头并保持基础行为。

---

### 9.7 调用上下文载体与请求绑定

**NWP-CTX-01 — 原生载体。** QueryFrame、ActionFrame、SubscribeFrame 可携带
`invocation_context`，其值为包含 NIP §7.8 JSON envelope 的无填充 base64url
字符串，并携带 `required_features` 精确特性对数组，形如
`{"id":"nps.invocation_context","revision":1}`。此请求数组不同于 Hello 的
必需 ID 数组。特性对必须唯一、按 ID 排序且最多 32 项，每对都必须已在准入会话
的选择集合中。投影必须要求 context revision 1。理解特性的接收方必须在派发前
拒绝未协商要求；客户端不得向旧会话发送此类请求。本绑定不向其他帧添加投影载体。

**NWP-CTX-02 — HTTP 载体。** 使用单个 `X-NPS-Invocation-Context` 及 §9.6
的精确 `X-NPS-Required-Features` 集合，包含 context revision 1。
投影 HTTP 请求正文不得再携带原生专用 `invocation_context` 和
`required_features` 字段；多载体属于无效。网关须显式转换，不同时保留两种载体。
编码上下文字符串最多 21846 ASCII 字节，解码 envelope 最多 16384 字节；
非规范 base64url 和重复实例无效。

**NWP-CTX-03 — 逻辑请求。** 本绑定的摘要 `protocol` 固定为 `nwp`；
ActionFrame 的 `operation` 为精确 `action_id`，QueryFrame 为
`nwp.query`，SubscribeFrame 为 `nwp.subscribe`。
`target` 是由认证路由确定且与接收方配置核对的精确逻辑服务/资源 ID，
不得复制未经验证的调用方权限 header。
`idempotency_key` 是实际请求字段或 null，仍受其 schema 约束。
`required_features` 是适用载体中请求的精确特性对集合。

**NWP-CTX-04 — 完整语义正文。** 对 JSON 兼容请求，`body.value` 为已解析
NWP 请求对象，移除 `frame`、`request_id`、`invocation_context`、
`required_features`；仅对 ActionFrame 再移除已绑定到外层对象的
`action_id` 和 `idempotency_key`。
保留全部其他字段，包括存在的 params、filters、query type、callback_url、
priority、async、timeout、订阅选项。保留字段是否存在的差异；
摘要验证前不插入默认值、不归一化值。HTTP path/query/header 语义必须先按注册
绑定映射为逻辑请求；未被绑定表示的 header 不得影响权限或副作用。
非 I-JSON 值必须使用注册的 byte-body profile，否则拒绝，不自行发明跨语言转换。
排除的是传输 trace 关联与投影，不是业务输入。交给 handler 的必须是验证过的 I-JSON
值，不得再按更高精度重解析原始数字文本。数值语义为有限 IEEE-754 binary64；
超出 ±9007199254740991 的整数或需要高精度 decimal 的 schema 必须使用声明的
字符串/字节表示。不得签名舍入值，却让 handler 执行另一个高精度值。
排除的关联字段不得驱动授权、
操作选择、业务去重或业务决策；仍允许协议关联回显。

**NWP-CTX-05 — 权限与派发。** 必须先完成 NIP §7.8 验证及原子重放准入，
且插入重放记录前完成无业务副作用的操作 schema/绑定验证；无效操作输入不得消耗
projection ID。验证及重放准入完成后，
才能调用操作处理器、操作驱动的 callback、外部 probe 或产生业务副作用。
验证/重放存储本身属于准入工作，不是业务派发。
公网/不可信投影载体在入口移除，使用本地已准入权限生成新投影。
紧邻网关身份和原始 subject 必须区分。调用方不能靠填字段选择可信 issuer
策略、本地开发模式、capabilities 或 scope。所有服务路由落实强制验证前不得
声明投影/特性支持。换跳或修改请求后重新投影，不代表允许重试不确定 mutation。

---

## 10. 完整请求响应示例

**HTTP 模式查询请求**

```
POST /nwp/orders/query HTTP/1.1
Host: api.example.com:17433
X-NWP-Agent: urn:nps:agent:ca.innolotus.com:550e8400
X-NWP-Budget: 1200
X-NWP-Tokenizer: cl100k_base
X-NWP-Request-ID: 550e8400-e29b-41d4-a716-446655440001
Content-Type: application/nwp-frame

[QueryFrame: { anchor_ref, filter, fields, limit, auto_anchor }]
```

**成功响应**

```
HTTP/1.1 200 OK
X-NWP-Schema: sha256:a3f9...
X-NWP-Tokens: 380
X-NWP-Request-ID: 550e8400-e29b-41d4-a716-446655440001
X-NWP-Rate-Limit: 300
X-NWP-Rate-Remaining: 248
Content-Type: application/nwp-capsule

[CapsFrame]
```

**错误响应**

```
HTTP/1.1 404 Not Found
X-NWP-Request-ID: 550e8400-e29b-41d4-a716-446655440001
Content-Type: application/nwp-error+json

{ "status": "NPS-CLIENT-NOT-FOUND", "error": "NWP-QUERY-FIELD-UNKNOWN", ... }
```

---

## 11. Complex Node — 节点图谱

Complex Node 在 NWM 中声明子节点引用：

```json
{
  "graph": {
    "refs": [
      { "rel": "user",    "node": "nwp://api.myapp.com/users" },
      { "rel": "payment", "node": "nwp://pay.myapp.com/transactions" }
    ],
    "max_depth": 2
  }
}
```

Agent 通过 `X-NWP-Depth` 头（HTTP 模式）或 QueryFrame `depth` 字段（原生模式）控制遍历深度。节点 MUST 检测循环引用（返回 `NWP-GRAPH-CYCLE`），并维护子节点 URL 白名单（防 SSRF）。

---

## 12. 保留查询类型

`QueryFrame`（§6.1）与 `SubscribeFrame`（§8.1）的 `type` 字段允许请求选入**保留查询类型**，由本规范定义其语义。`topology.*` 命名空间保留给 Anchor Node 上的集群拓扑操作；以下保留命名空间对所列节点角色**强制要求**。

| 命名空间 | 所属角色 | 强制等级 | 操作 |
|----------|---------|---------|------|
| `topology.*` | Anchor Node（§2.1）| NPS-AaaS Profile L2（[services/NPS-AaaS-Profile.cn.md §4.3](services/NPS-AaaS-Profile.cn.md)）| `topology.snapshot`（§12.1）、`topology.stream`（§12.2）|

`type` 缺省时按默认 per-anchor 查询/订阅语义（§6、§8）执行。`type` 设置后，本节定义的字段生效；与之冲突的标准字段（如 `anchor_ref`、顶层 `filter`）将被忽略，除非保留类型自身的 schema 显式带上它们。

实现遇到无法识别的保留 `type` 值时，MUST 用 `NWP-RESERVED-TYPE-UNSUPPORTED`（§13）显式拒绝，使调用方能区分"未知保留操作"与"action_id 不存在"（`NWP-ACTION-NOT-FOUND`），从而正确决定是否回退或报错。

### 12.1 `topology.snapshot`

一次性获取 Anchor Node 的集群拓扑快照。

| 属性 | 取值 |
|------|------|
| 帧 | QueryFrame（0x10），`type = "topology.snapshot"` |
| 强制对象 | 全部 Anchor Node（NPS-AaaS Profile L2 及以上）|
| 幂等 | 是 |
| 缓存 | 响应 MAY 由客户端缓存；响应中的 `version` 字段用于与后续 `topology.stream` 事件做校验对齐 |

**请求字段**（QueryFrame 顶层，叠加在 §6.1 标准字段之上）：

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `type` | string | 必填 | 常量 `"topology.snapshot"` |
| `topology` | object | 必填 | 拓扑相关参数容器，见下 |
| `topology.scope` | string | 必填 | `"cluster"` 表示 Anchor 自身集群；`"member"` 表示单个成员（要求带 `topology.target_nid`）|
| `topology.include` | array of strings | 可选 | `["members", "capabilities", "tags", "metrics"]` 的子集，默认 `["members"]`。`capabilities` 与 `metrics` 的 schema 由实现自定，可为空 |
| `topology.depth` | uint8 | 可选 | 控制对子 Anchor 成员的递归。`1`（默认）只把子 Anchor 当引用列出；`2+` 递归展开。Anchor Node MAY 设上限并以 `truncated: true` 提示截断。在 L2，深度 ≥ 2 的子 Anchor 递归为 **可选**；客户端 SHOULD 自行对每个子 Anchor 分别发 snapshot |
| `topology.target_nid` | string | 条件必填 | 当 `topology.scope = "member"` 时必填，指目标成员的 NID |

**响应**：`CapsFrame (0x04)`，`anchor_ref = "nps:system:topology:snapshot"`，`data` 数组含一条快照对象。

```json
{
  "frame": "0x04",
  "anchor_ref": "nps:system:topology:snapshot",
  "count": 1,
  "data": [{
    "version": 142,
    "anchor_nid": "urn:nps:node:labacacia:host-abc123",
    "cluster_size": 23,
    "members": [
      {
        "nid": "urn:nps:agent:labacacia:host-abc123-sess-aaa",
        "node_roles": ["memory"],
        "activation_mode": "ephemeral",
        "tags": ["dev", "library"],
        "joined_at": "2026-04-15T10:23:00Z",
        "last_seen": "2026-04-26T14:55:00Z"
      },
      {
        "nid": "urn:nps:node:labacacia:host-def456",
        "node_roles": ["anchor"],
        "activation_mode": "resident",
        "child_anchor": true,
        "member_count": 7,
        "tags": ["sub-cluster", "training"]
      }
    ],
    "truncated": false
  }]
}
```

**Snapshot payload 字段**：

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `version` | uint64 | 必填 | 单调递增的拓扑版本号。仅在 Anchor 重启 / rebase 时重置（§12.3）|
| `anchor_nid` | string | 必填 | 响应方 Anchor Node 的 NID |
| `cluster_size` | uint32 | 必填 | 直接成员总数，与 `topology.depth` 截断无关 |
| `members` | array of member objects | 必填 | 见下方成员对象 schema |
| `truncated` | bool | 可选 | 当 `topology.depth` 上限被触发时为 true；否则省略或 false |

**Member 对象 schema**：

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `nid` | string | 必填 | 成员 NID |
| `node_roles` | array of strings | 必填 | NDP `node_roles`——该成员声明的完整角色集（NPS-4 §3.1）|
| `activation_mode` | string | 必填 | NDP `activation_mode`，取 `ephemeral` / `resident` / `hybrid`（NPS-4）|
| `child_anchor` | bool | 可选 | 该成员自身是否为子集群的 Anchor；为真时隐含必带 `member_count` |
| `member_count` | uint32 | 条件必填 | `child_anchor = true` 时必填，表示子 Anchor 直接成员数 |
| `tags` | array of strings | 可选 | NDP 声明的 tag |
| `joined_at` | string | 可选 | RFC 3339 时间戳，首次观测时间 |
| `last_seen` | string | 可选 | RFC 3339 时间戳，最近一次 NDP `Announce` |
| `capabilities` | object | 可选 | 仅当 `topology.include` 包含 `capabilities` 时返回；schema 由实现自定 |
| `metrics` | object | 可选 | 仅当 `topology.include` 包含 `metrics` 时返回；schema 由实现自定 |

### 12.2 `topology.stream`

Anchor Node 集群拓扑的持续变更事件流。

| 属性 | 取值 |
|------|------|
| 帧 | SubscribeFrame（0x12），`type = "topology.stream"` |
| 强制对象 | 全部 Anchor Node（NPS-AaaS Profile L2 及以上）|
| 可取消 | 是 —— 关闭订阅传输，或收到/发出终止性 ErrorFrame |

**请求字段**（SubscribeFrame 顶层，叠加在 §13.1 标准字段之上）：

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `type` | string | 必填 | 常量 `"topology.stream"` |
| `subscription_id` | string | 必填 | 客户端生成的 UUID v4，用于关联拓扑流与推送的 DiffFrame |
| `cursor` | string | 可选 | 上一次订阅中服务端签发的不透明续订 cursor。存在时，`cursor` 优先于 `topology.since_version` |
| `topology` | object | 必填 | 拓扑相关参数容器，见下 |
| `topology.scope` | string | 必填 | `"cluster"`（默认指 Anchor 自身集群）；其它 scope 留待将来 |
| `topology.filter` | object | 可选 | 减少事件量。支持键：`tags_any`（数组，命中任一）、`tags_all`（数组，全部命中）、`node_roles`（数组——按角色过滤，匹配 `node_roles` 与给定值有交集的成员）。Anchor Node MUST 以 `NWP-TOPOLOGY-FILTER-UNSUPPORTED` 拒绝未识别 key |
| `topology.since_version` | uint64 | 可选 | 拓扑语境下的初次续订提示，供已持有快照 `version` 但尚无不透明 `cursor` 的客户端使用。Anchor Node MUST 在可行时重播缺失事件；版本超出保留窗口时 MUST 推送 `resync_required` 事件，客户端 MUST 重发 `topology.snapshot` |

`type = "topology.stream"` 时，`cursor` 是 v0.13 的规范续订机制。`topology.since_version` 仅在没有 cursor 可用时作为拓扑语境下的引导提示被接受；两者同时出现时，`cursor` 优先。

**事件**通过 `DiffFrame (0x02)` 推送，携带 §13.2 的订阅事件信封。在 `topology.stream` 订阅中，`subscription_id` 标识该流，`event_type` 取下表保留拓扑事件类型之一（在默认 `"create" / "update" / "delete"` 枚举之外扩展），`seq` 是事件后的拓扑版本（§12.3），`payload` 携带类型相关数据。

| `event_type` | 触发条件 | `payload` 形态 |
|--------------|---------|---------------|
| `member_joined` | NDP `Announce` 中 `cluster_anchor` 指向本 Anchor | 完整成员对象（§12.1）|
| `member_left` | 成员显式离开或超过 NDP 在线 TTL | `{ "nid": "urn:nps:..." }` |
| `member_updated` | 既有成员元数据变更（tags、activation_mode、capabilities 等）| `{ "nid": "urn:nps:...", "changes": { "<字段>": <新值>, ... } }` —— 仅字段级 diff，由客户端自行合并 |
| `anchor_state` | Anchor 集群状态变更且与订阅方相关。payload 携带判别字段 `field`，用于选择下列子类型之一 | `{ "field": "<子类型>", "details": { ... } }` |
| `resync_required` | 订阅方 MUST 拆除本地视图，重新发起一次 `topology.snapshot`，再建立新的 `topology.stream` 订阅。触发条件：(a) `topology.since_version` 已无法重播；(b) 任何使既有版本计数器失效的 `anchor_state` 子类型（例如 `version_rebased`）；(c) 服务端状态丢失，需要重新订阅 | `{ "reason": "<version_too_old \| anchor_rebased \| server_state_lost>" }`。本事件 MAY 不带 `seq`，订阅方 MUST 重新发起 `topology.snapshot` |

**`anchor_state` 子类型**（由 payload 的 `field` 判别字段选出）：

| `field` | Phase | 触发条件 | `details` 形态 |
|---------|-------|---------|---------------|
| `version_rebased` | Phase 1–2 | Anchor 重启并重置了自己的单调 `version` 计数器（§12.3）。订阅方 MUST 视同 `resync_required` 处理 | `{ "previous_version": <uint64>, "new_version": <uint64> }` |
| `anchor_failover` | 已定案（NPS-CR-0009）| 活跃 Anchor 已把集群所有权移交给同集群的另一个 Anchor（多 Anchor 高可用，AaaS L3）。每次所有权转移时推送；被围栏（fenced）的前任 leader MUST 发送一条终止性 `anchor_failover` 后关闭自己的流 | `{ "successor_nid": "urn:nps:...", "cluster_epoch": <uint64>, "reason": "planned" \| "active_lost" }` |
| `anchor_quorum_lost` | 已定案（NPS-CR-0009）| Anchor 集群无法维持所有权 quorum，集群转为只读（degraded）。Anchor 以 `NWP-ANCHOR-NOT-LEADER` 拒绝拓扑写入，并标记 `health: "degraded"`（NDP §3.2）| `{ "quorum_size": <uint32>, "available": <uint32> }` |

**集群所有权栅栏（NPS-CR-0009）。** 在多 Anchor 集群中，任一时刻至多有一个 Anchor 是活跃所有者，由单调递增的 `cluster_epoch`（uint64，从 1 开始）标识。每个 `topology.snapshot` / `topology.stream` 响应以及每次改变拓扑的写入都携带当前 `cluster_epoch`。standby（或处于只读 degraded 状态）的 Anchor MUST 以 `NWP-ANCHOR-NOT-LEADER`（→ `NPS-CLIENT-CONFLICT`）拒绝拓扑写入；活跃 Anchor MUST 以 `NWP-ANCHOR-EPOCH-FENCED` 拒绝任何携带**更高** `cluster_epoch` 的入站帧（把已被取代的 leader 围栏掉）。单 Anchor 集群保持 `cluster_epoch = 1`，且永不推送 `anchor_failover` / `anchor_quorum_lost`。见 [NPS-CR-0009](cr/NPS-CR-0009-multi-anchor-ha.md)。

实现 MUST 把无法识别的 `anchor_state.field` 取值按前向兼容处理并忽略，而不是拆除订阅，使将来的 Phase 3 子类型可以在不破坏 wire 兼容的前提下引入。

标准 SubscribeFrame 心跳（§13.2）行为不变。v0.13 的取消是传输层的：任意一方 MAY 在有错误原因可用时先发出终止性 ErrorFrame，再关闭订阅流。

### 12.3 版本与一致性模型

**保证**：

- `version: V` 的 `topology.snapshot` 反映恰好 `V` 次拓扑变更后的集群状态。
- `seq: V` 的 `topology.stream` 事件反映恰好 `V` 次变更后的集群状态。
- `version: V` 快照叠加之后 `V+1, V+2, …` 的事件序列，得到一致的实时视图。

**不保证**：

- 实时投递延迟 —— 事件 MAY 批量推送。
- 多个 Anchor Node 之间的事件全序 —— 每个 Anchor 维护独立 `version` 计数器。
- 与非拓扑事件（成员节点上的 Action / Query 流量）的全序。

**重启与 rebase**：Anchor Node MAY 在重启时 rebase 自己的 `version` 计数器。rebase 时 MUST 向所有活跃订阅推送 `anchor_state` 事件并附 `field: "version_rebased"`；订阅方 MUST 视同 `resync_required` 处理，重新发起 `topology.snapshot`。

### 12.4 不在范围内

- **Capability / metrics 字段 schema 标准化**：§12.1 的 `capabilities` 与 `metrics` 内容由实现自定，待积累足够实现样本后由后续 CR 再标准化。
- **跨集群联邦查询**：在多个 Anchor Node 之间联合查询拓扑，属 NPS-AaaS Profile L3 / NPS Cloud 范畴，本节仅覆盖单 Anchor。
- **授权模型 —— 最小约束（Phase 1–2）**：Anchor Node MUST 在服务任何 `topology.*` 请求之前强制执行以下最小约束：

  1. **Capability 闸门（按接口区分）**：Phase 1–2 区分两个授权接口：
     - `topology.snapshot`（单次拉取，§12.1）：请求方 NID MUST 在 `IdentFrame.capabilities`（NPS-3 §5.1）中声明 `topology:read`；缺少该 capability MUST 返回 `NWP-TOPOLOGY-UNAUTHORIZED`。
     - `topology.stream`（长连接订阅，§12.2）：请求方 MUST 在 `IdentFrame.capabilities` 中同时声明 `topology:read` 与 `topology:subscribe`。Phase 2 的 Anchor Node MUST 强制校验 `topology:subscribe` capability（缺失即视为授权失败），使订阅权限可与快照读取权限分离；尚未强制 `topology:subscribe` 的 Anchor Node MUST 至少强制 `topology:read`。无法强制 `topology:subscribe` 的节点 MUST 在 NWM `stability` 元数据中明确记录该未强制情况。

     IdentFrame 由请求方私钥签名，因此该声明是完整性受保护的，但仍是自声明——在 Phase 1–2 并非 CA 背书。
  2. **NDP 角色交叉校验（纵深防御）**：Anchor SHOULD 额外校验请求方最近一次收到的 `AnnounceFrame`（在 TTL 内）声明的 `node_roles` 含 `"anchor"`。不匹配时 SHOULD 返回带 `hint` 的 `NWP-TOPOLOGY-UNAUTHORIZED`。缺少 `AnnounceFrame` MUST NOT 阻断已通过 capability 闸门的请求方。
  3. **流中拒绝（仅订阅）**：对已建立的 `topology.stream` 订阅，若 Anchor 撤销了请求方的 capability 集合（例如收到 CA 的 RevokeFrame、NID 过期或 scope 收窄），服务端 MUST 在流上推送一条终止性 `NWP-TOPOLOGY-UNAUTHORIZED` 事件，然后关闭该流。该事件使用标准 DiffFrame 信封，`event_type = "error"`，payload 为 `{ "code": "NWP-TOPOLOGY-UNAUTHORIZED", "reason": "<revoked | expired | scope_narrowed>" }`。Anchor Node MUST NOT 静默丢弃订阅方——必须给出一次干净的拒绝事件，客户端才能把授权失效与传输层断连区分开。
  4. **声誉交互（NPS-RFC-0004 `reputation_policy`）**：当接收方 Anchor 声明了 `reputation_policy`（NWM Phase 2 字段，见 [NPS-RFC-0004 §4.4](rfcs/NPS-RFC-0004-nid-reputation-log.md)），且在 `topology.stream` 订阅活跃期间请求方 NID 的声誉分跌破配置阈值时，Anchor SHOULD 推送一条终止性事件，其 `payload.code = "NWP-AUTH-REPUTATION-BLOCKED"`，并携带匹配的 `incident`、`severity` 与账本条目 `seq`（按错误码 §14），然后关闭该流。对于初次握手阶段（请求时）的声誉拒绝，适用标准的同步 `NWP-AUTH-REPUTATION-BLOCKED` 错误码，订阅根本不会被建立。未声明 `reputation_policy` 的 Anchor 没有评估声誉的义务。
  5. **Phase 3 [RFC-0002 stable]**：Anchor SHOULD 额外校验由 CA 背书的 `id-nps-node-roles` 证书扩展（将由后续 RFC-0002 修订定义），以弥合自声明缺口，把角色声明绑定到 CA 签发的证书上。

  更细粒度的按集群命名空间或 ACL 策略仍由实现自定，留待后续 CR 跟踪。
- **浏览器端传输（WebSocket）**：`npsd` 是否暴露 WebSocket 端点供浏览器消费另案跟踪。本节定义的拓扑查询语义与传输层无关。

---

## 13. SubscribeFrame (0x12) —— 正式规范

SubscribeFrame 在 Memory Node 或 Anchor Node 上开启一个服务端推送订阅。服务端以 DiffFrame 消息流式推送匹配的事件，直到订阅被关闭。

### 13.1 请求字段

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `frame` | uint8 | 必填 | 固定值 `0x12` |
| `subscription_id` | string | 必填 | 客户端生成的 UUID v4；用于关联事件与取消订阅 |
| `operation` | string | 可选 | `open`（默认）、`renew` 或 `close`。`renew`/`close` 定位已有 `subscription_id`，并 MUST 省略 target/filter 字段。|
| `type` | string | 可选 | §12 定义的保留订阅类型标识符。设置后，类型特定字段生效，且 `anchor_ref` 的语义由该类型定义。缺省时按下述 per-anchor 订阅行为处理 |
| `anchor_ref` | string | 条件必填 | 所订阅数据的 anchor_id。默认 per-anchor 订阅时必填；当保留 `type` 自行定义目标语义时省略（例如 `topology.stream`）|
| `filter` | object | 可选 | 与 QueryFrame `filter`（§6）相同的过滤语法；缺省则匹配全部事件 |
| `heartbeat_interval_ms` | uint32 | 可选 | 设置后，服务端 MUST 按此间隔发出心跳 DiffFrame（空 payload，`event_type = "heartbeat"`）；默认 0（无心跳）|
| `max_events` | uint32 | 可选 | 服务端在推送这么多事件后关闭订阅；0 = 不限 |
| `cursor` | string | 可选 | 从先前位置恢复；若 cursor 已过期，服务端 MUST 返回 `NWP-SUBSCRIBE-SEQ-TOO-OLD` |
| `lease_seconds` | uint32 | 可选 | `open` 或 `renew` 请求的 lease。仅在声明 `subscription_policy` 时有效；服务端 clamp 到 `max_lease_seconds`。零值非法。|

### 13.2 生命周期

1. 客户端发送 SubscribeFrame → 服务端以 CapsFrame 响应（回显 `subscription_id`，`status = "open"`）
2. 服务端流式推送 DiffFrame 事件；每个事件在 EXT 头或等价的传输层元数据中携带 `subscription_id`
3. 客户端通过关闭订阅传输来取消；服务端在达到 `max_events` 时也 MAY 主动关闭
4. 服务端发生错误时，MUST 在关闭前发送一个带相应 `NWP-SUBSCRIBE-*` 码的终止 ErrorFrame

订阅推送的 DiffFrame 在标准 DiffFrame 字段之外，增加以下事件信封字段：

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `subscription_id` | string | 必填 | 关联的订阅 ID |
| `seq` | uint64 | 除终止性 `resync_required` 外必填 | 订阅内单调递增的事件序号；保留订阅类型 MAY 将其绑定到某个领域特定的版本号，例如拓扑版本（§12.3）|
| `event_type` | string | 必填 | 默认订阅取 `"create"` / `"update"` / `"delete"` / `"heartbeat"` / `"error"`。保留订阅类型（§12）MAY 定义额外取值 |
| `timestamp` | string | 可选 | 变更时间（ISO 8601）|
| `payload` | object | 可选 | 事件特定负载。心跳使用空 payload |
| `cgn_est` | uint32 | 可选 | 本次推送事件负载的 CGN token 成本估计。节点 SHOULD 在每个推送的 DiffFrame 上填充该字段，使订阅方能按 [token-budget.md §7.2](token-budget.md) 在 Agent 侧做累计预算核算。缺省表示节点不提供逐事件估计；Agent MAY 本地按 UTF-8/4 回退估算 |

**Cursor 语义**

- Cursor 值是服务端生成的不透明字符串。客户端 MUST NOT 解析、比较或自行构造它们。
- 当 Agent 检测到 `seq` 断档时，SHOULD 用它收到的最新服务端签发 `cursor` 重新订阅。
- 节点 SHOULD 保留最近的 cursor 位置（推荐：10 分钟或 10,000 条事件，先到者为准）。
- 若 `cursor` 落在保留窗口之外，节点 MUST 返回 `NWP-SUBSCRIBE-SEQ-TOO-OLD`，或发出该保留类型特定的终止性 resync 事件（例如 `topology.stream` 的 `resync_required`）。

### 13.3 错误码

以下错误码（定义于 §14）适用于 SubscribeFrame 操作：

- `NWP-SUBSCRIBE-STREAM-NOT-FOUND` —— subscription_id 未知或已关闭
- `NWP-SUBSCRIBE-LIMIT-EXCEEDED` —— 达到服务端并发订阅上限
- `NWP-SUBSCRIBE-FILTER-UNSUPPORTED` —— 本节点不支持该 filter 表达式
- `NWP-SUBSCRIBE-INTERRUPTED` —— 服务端中断
- `NWP-SUBSCRIBE-SEQ-TOO-OLD` —— cursor 位置已不可用

### 13.4 alpha.19 可续订订阅 Profile（NWP v0.22）

5. **NWP-P19-05 —— 能力真实性。** 节点只有声明 `subscription_policy` 才支持
   lease operation。default 与 maximum MUST 为正，且 `renew_before_seconds`
   MUST 小于 maximum。
6. **NWP-P19-06 —— open。** 缺省 `operation` 表示 `open`。服务端采用请求值或
   `default_lease_seconds`，clamp 到 maximum，并在 opening CapsFrame 返回
   `lease_seconds` 与 RFC 3339 `expires_at`。
7. **NWP-P19-07 —— binding。** renew/close 必须绑定 open 时捕获的同一认证 principal、
   target 与保留 `type`。不同 principal 得到既有授权错误，且不得获知订阅状态。
8. **NWP-P19-08 —— renew。** expiry 前接受的合法 renew 把 deadline 替换为
   `accepted_at + effective_lease_seconds` 并返回新 deadline；不得回退 cursor 或重置 event `seq`。
9. **NWP-P19-09 —— expiry。** `now >= expires_at` 时服务端原子关闭订阅；transport
   可写时最多发送一次携带 `NWP-SUBSCRIBE-LEASE-EXPIRED` 的 terminal event，并释放资源。
   expiry 后 renew 返回同一错误，且不得复活订阅。
10. **NWP-P19-10 —— 非法请求。** 零 lease、非法 policy bounds、renew/close
    携带 target/filter，或未知 operation，均以 `NWP-SUBSCRIBE-LEASE-INVALID`
    拒绝，且不得改变订阅。

---

## 14. 错误码

| 错误码 | NPS 状态码 | 描述 |
|--------|-----------|------|
| `NWP-AUTH-NID-SCOPE-VIOLATION` | `NPS-AUTH-FORBIDDEN` | Agent scope 不覆盖目标节点 |
| `NWP-AUTH-NID-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | NID 证书已过期 |
| `NWP-AUTH-NID-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | NID 已被吊销 |
| `NWP-AUTH-NID-UNTRUSTED-ISSUER` | `NPS-AUTH-UNAUTHENTICATED` | NID 颁发者不在 trusted_issuers 中 |
| `NWP-AUTH-NID-CAPABILITY-MISSING` | `NPS-AUTH-FORBIDDEN` | Agent 缺少节点要求的能力 |
| `NWP-AUTH-ASSURANCE-TOO-LOW` | `NPS-AUTH-FORBIDDEN` | Agent 的 `assurance_level` 低于节点 `min_assurance_level`（或 ActionSpec 的 per-action 覆盖）。响应 SHOULD 在 `hint` 字段附 CA 注册 URL。（NPS-RFC-0003）|
| `NWP-AUTH-REPUTATION-BLOCKED` | `NPS-AUTH-FORBIDDEN` | 接收 Node 的 `reputation_policy`（Phase 2 NWM 字段 —— 见 [NPS-RFC-0004 §4.4](rfcs/NPS-RFC-0004-nid-reputation-log.cn.md)）命中了对发起方 `subject_nid` 的 `reject_on` 规则。响应 SHOULD 携带匹配的 `incident` + `severity` + 日志条目 `seq` 便于追溯。产生此错误的字段形态在 NWP v0.13（Phase 2）落地；错误码本身在 NWP v0.13（Phase 1）就保留，让 SDK 可以提前识别而无需额外 spec bump。（NPS-RFC-0004）|
| `NWP-QUERY-FILTER-INVALID` | `NPS-CLIENT-BAD-PARAM` | Filter 语法不合法或嵌套超限 |
| `NWP-QUERY-FIELD-UNKNOWN` | `NPS-CLIENT-BAD-PARAM` | fields 中引用了不存在的字段 |
| `NWP-QUERY-CURSOR-INVALID` | `NPS-CLIENT-BAD-PARAM` | cursor 值无法解码或已过期 |
| `NWP-QUERY-REGEX-UNSAFE` | `NPS-CLIENT-BAD-PARAM` | `$regex` 模式被拒绝（ReDoS 风险或超长）|
| `NWP-QUERY-VECTOR-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | 节点不支持向量搜索 |
| `NWP-QUERY-AGGREGATE-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | 节点不支持聚合查询 |
| `NWP-QUERY-AGGREGATE-INVALID` | `NPS-CLIENT-BAD-PARAM` | aggregate 结构不合法（未知 func、alias 重复等）|
| `NWP-QUERY-STREAM-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | 节点不支持流式查询 |
| `NWP-ACTION-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | action_id 不存在 |
| `NWP-ACTION-PARAMS-INVALID` | `NPS-CLIENT-UNPROCESSABLE` | 操作参数 Schema 校验失败 |
| `NWP-ACTION-IDEMPOTENCY-CONFLICT` | `NPS-CLIENT-CONFLICT` | 相同 idempotency_key 的请求正在进行中 |
| `NWP-LLM-CONTEXT-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | 有状态 LLM context 未知/已 release，或 idempotency lookup 已过期（§7.6）|
| `NWP-LLM-CONTEXT-EXPIRED` | `NPS-CLIENT-GONE` | 有状态 LLM context 存在 idle-expiry tombstone（§7.6）|
| `NWP-LLM-CONTEXT-VERSION-CONFLICT` | `NPS-CLIENT-CONFLICT` | `base_version` 陈旧，或并发 mutation 持有 reservation（§7.6）|
| `NWP-LLM-CONTEXT-BINDING-MISMATCH` | `NPS-CLIENT-CONFLICT` | model/system/tools/runtime binding 与已提交 context 不一致（§7.6）|
| `NWP-LLM-CONTEXT-FORBIDDEN` | `NPS-AUTH-FORBIDDEN` | Caller 不是 context owner，或缺少 scope/capability（§7.6）|
| `NWP-LLM-CONTEXT-LIMIT-EXCEEDED` | `NPS-LIMIT-RESOURCE` | 达到每 principal 活跃 context 上限（§7.6）|
| `NWP-LLM-CONTEXT-OPERATION-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | 节点支持 context，但不支持请求的生命周期 operation（§7.6）|
| `NWP-TASK-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | task_id 不存在 |
| `NWP-TASK-ALREADY-CANCELLED` | `NPS-CLIENT-CONFLICT` | 任务已被取消 |
| `NWP-TASK-ALREADY-COMPLETED` | `NPS-CLIENT-CONFLICT` | 任务已完成，无法取消 |
| `NWP-TASK-ALREADY-FAILED` | `NPS-CLIENT-CONFLICT` | 任务已失败，无法取消 |
| `NWP-SUBSCRIBE-STREAM-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | 订阅操作引用的 `subscription_id` 不存在或已关闭 |
| `NWP-SUBSCRIBE-LIMIT-EXCEEDED` | `NPS-LIMIT-EXCEEDED` | 超出最大并发订阅数 |
| `NWP-SUBSCRIBE-FILTER-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | 节点不支持带 filter 的订阅 |
| `NWP-SUBSCRIBE-INTERRUPTED` | `NPS-SERVER-UNAVAILABLE` | 订阅流因数据源中断而终止 |
| `NWP-SUBSCRIBE-SEQ-TOO-OLD` | `NPS-CLIENT-CONFLICT` | `cursor` 落在节点保留窗口之外；需全量重查或走保留类型的 resync 流程 |
| `NWP-SUBSCRIBE-LEASE-INVALID` | `NPS-CLIENT-BAD-PARAM` | 可续订订阅 operation 或 lease/policy bounds 非法 |
| `NWP-SUBSCRIBE-LEASE-EXPIRED` | `NPS-CLIENT-GONE` | 订阅 lease 已到 deadline，不能 renew 或复活 |
| `NWP-BUDGET-EXCEEDED` | `NPS-LIMIT-BUDGET` | 响应将超过 token 预算 |
| `NWP-DEPTH-EXCEEDED` | `NPS-CLIENT-BAD-PARAM` | depth 超过节点允许的 max_depth |
| `NWP-GRAPH-CYCLE` | `NPS-CLIENT-UNPROCESSABLE` | 节点图谱存在循环引用 |
| `NWP-NODE-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | 底层数据源暂不可用 |
| `NWP-MANIFEST-VERSION-UNSUPPORTED` | `NPS-CLIENT-BAD-PARAM` | Agent NPS 版本低于 min_agent_version |
| `NWP-MANIFEST-NODE-TYPE-REMOVED` | `NPS-CLIENT-BAD-FRAME` | NWM `node_type` 包含已移除的遗留值 `"gateway"`（NPS-CR-0001）；响应 SHOULD 携带指向 NPS-CR-0001 的 `hint` |
| `NWP-MANIFEST-NODE-TYPE-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | NWM `node_type` 包含无法识别的值（`"gateway"` 遗留情况请用 `NWP-MANIFEST-NODE-TYPE-REMOVED`）|
| `NWP-RATE-LIMIT-EXCEEDED` | `NPS-LIMIT-RATE` | 超出频率限制 |
| `NWP-RESERVED-TYPE-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | `QueryFrame` 或 `SubscribeFrame` 的 `type` 为无法识别的 reserved-type 标识符（§12）。与 `NWP-ACTION-NOT-FOUND` 不同——未知操作数是 `type` 而非 `action_id`。|
| `NWP-HTTP-ORIGIN-FORBIDDEN` | `NPS-AUTH-FORBIDDEN` | HTTP overlay origin policy 拒绝调用方（§9.5）|
| `NWP-HTTP-CONTENT-TYPE-UNSUPPORTED` | `NPS-CLIENT-BAD-FRAME` | HTTP overlay 请求 `Content-Type` 不是受支持的 NWP frame media type（§9.5）|
| `NWP-HTTP-ACCEPT-UNSATISFIABLE` | `NPS-CLIENT-BAD-PARAM` | HTTP overlay 请求 `Accept` 无法由任何受支持的 response media type 满足（§9.5）|
| `NWP-HTTP-REQUEST-ID-MISMATCH` | `NPS-CLIENT-BAD-PARAM` | 响应 `X-NWP-Request-ID` 未回传请求 ID（§9.5）|
| `NWP-HTTP-FRAME-BODY-MALFORMED` | `NPS-CLIENT-BAD-FRAME` | HTTP body 无法解析为受支持的 NWP frame envelope（§9.5）|
| `NWP-HTTP-BODY-TOO-LARGE` | `NPS-LIMIT-PAYLOAD` | HTTP 请求 body 超出服务端 NWP body 上限（§9.5、§16.5.1）|
| `NWP-CAPABILITY-ADVERTISED-UNIMPLEMENTED` | `NPS-SERVER-UNSUPPORTED` | NWM 声明了当前节点无法服务的 capability/profile（§9.5）|
| `NWP-TOPOLOGY-UNAUTHORIZED` | `NPS-AUTH-FORBIDDEN` | 调用方无权读取该 Anchor 的拓扑（§12）。授权策略由实现自定，详见 §12.4 |
| `NWP-TOPOLOGY-UNSUPPORTED-SCOPE` | `NPS-CLIENT-BAD-PARAM` | 该 Anchor 不实现请求的 `topology.scope` |
| `NWP-TOPOLOGY-DEPTH-UNSUPPORTED` | `NPS-CLIENT-BAD-PARAM` | 请求的 `topology.depth` 超过该 Anchor 上限 |
| `NWP-TOPOLOGY-FILTER-UNSUPPORTED` | `NPS-CLIENT-BAD-PARAM` | `topology.filter` 含未识别 key |
| `NWP-ANCHOR-NOT-LEADER` | `NPS-CLIENT-CONFLICT` | 拓扑写请求到达了 standby（或降级只读）Anchor；只有当前 `cluster_epoch` 的活跃属主才可接受写入（§12.2，NPS-CR-0009）|
| `NWP-ANCHOR-EPOCH-FENCED` | `NPS-CLIENT-CONFLICT` | 入站帧携带的 `cluster_epoch` 高于接收方自身；接收方是被取代的旧 leader，自我隔离（§12.2，NPS-CR-0009）|
| `NWP-BRIDGE-DIRECTION-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | 请求到达了该 Bridge Node 未声明的方向/协议 —— 见 §16.2；响应 SHOULD 在 `hint` 中携带 `bridge_protocols` 与 `bridge_inbound_protocols` 两个数组（NPS-CR-0010）|

---

## 15. 安全考量

### 15.1 Scope 强制校验
节点 MUST 在每次请求时校验 Agent NID 的 scope。超出 scope 的请求 MUST 返回 `NWP-AUTH-NID-SCOPE-VIOLATION`，不得返回任何数据。

### 15.2 SSRF 防护
Complex Node 解析子节点引用时，MUST 维护允许的节点 URL 前缀白名单，禁止访问内网地址（RFC 1918）。

### 15.3 Token Budget 强制执行
超过预算时，节点 SHOULD 优先裁剪响应内容（字段精简 → 摘要 → 截断记录数）；无法裁剪时 MUST 返回 `NWP-BUDGET-EXCEEDED`，不得直接截断数据。详见 [token-budget.cn.md §4.3](token-budget.cn.md)。

### 15.4 频率限制
节点 SHOULD 对每个 Agent NID 实施频率限制。超限时返回 `NWP-RATE-LIMIT-EXCEEDED` 并附加 `X-NWP-Rate-Reset` 头。未认证请求 SHOULD 使用 IP 维度限制。

### 15.5 Filter 注入防护
- 字段名 MUST 仅含字母/数字/下划线/点，长度 ≤ 128 字符
- `$regex` MUST 经过 ReDoS 检测；Filter 嵌套深度 ≤ 8
- 节点 MUST 使用参数化查询，禁止字符串拼接

### 15.6 callback_url 防滥用
- ActionFrame `callback_url` MUST 为 `https://` 前缀
- 节点 SHOULD 对回调 URL 做 SSRF 检查（禁止内网地址）
- 节点 SHOULD 对 callback 推送失败做指数退避重试（最多 3 次），之后放弃并标记任务为 `COMPLETED` 而非无限重试

### 15.7 拓扑读取
实现 §12 的 Anchor Node MUST 把 `topology.snapshot` 与 `topology.stream` 视作鉴权接口。最小授权约束定义在 §12.4：在 Phase 1–2，请求方 NID MUST 在 `IdentFrame.capabilities` 中声明 `topology:read`（主闸门）；NDP `node_roles` 交叉校验是纵深防御性质的 SHOULD。未授权调用方 MUST 收到 `NWP-TOPOLOGY-UNAUTHORIZED`，不得静默返回空结果，避免对集群成员关系做 oracle 攻击。

---

## 16. Bridge Node 合规性

Bridge Node 类型与 `bridge_target` 对象 schema 由 NPS-CR-0001 引入（§2.1），并在 NWP v0.13 标准化。
[NPS-CR-0010](cr/NPS-CR-0010-bridge-bidirectional.md) 将 Bridge Node 定案为**双向**，并把本章拆为
两个独立的合规 profile。本章形式化这两个 profile、给出它们共用的规范性错误映射，并提供规范化的
`bridge_target` 往返测试向量，使所有 SDK 的 Bridge 实现在线格式上保持一致。

### 16.1 合规 profile

Bridge Node MUST 至少声明下述两个 profile 之一，并且 MUST 在线上声明（§2.1、NPS-4 §3.1）。
两个都不声明就不是合规的 Bridge Node。Bridge Node MAY 同时声明两个。
**只声明出向** —— 也就是 alpha.15 之前唯一存在的那个 profile —— 的实现**无需任何改动即保持完全合规**。

#### 16.1.1 出向 profile（NPS → 外部）

通过声明非空的 `bridge_protocols` 来主张。合规的出向 Bridge Node MUST：

1. 在其 NWM（§4.1）中公告 `node_type: "bridge"`，并通过 NDP `bridge_protocols`（NPS-4 §3.1）公告所支持的外部协议。
2. 接受携带 `bridge_target` 对象的入站 NWP 帧；缺少该对象、或 `bridge_target` schema 校验失败的帧，MUST 以 `NWP-BRIDGE-TARGET-INVALID` 拒绝。
3. 对照已公告的集合校验 `bridge_target.protocol`；没有注册 dispatcher 的协议 MUST 返回 `NWP-BRIDGE-PROTOCOL-UNSUPPORTED`（不得静默放行）。
4. 把 `bridge_target` 中的未知字段视为不透明透传，MUST NOT 因其失败（前向兼容）。
5. **每次请求无状态**，MUST NOT 参与集群拓扑（纯 Bridge Node 上 `topology.*` MUST 返回 `NWP-RESERVED-TYPE-UNSUPPORTED`）。

合规的出向 Bridge Node SHOULD：

1. 在拨号上游前对 `bridge_target.endpoint` 施加 SSRF 防护（NPS-2 §15.2）。
2. 把 `bridge_target.extras.headers` 原样透传给上游 HTTP 请求，逐跳（hop-by-hop）头除外。

#### 16.1.2 入向 profile（外部 → NPS）

通过声明非空的 `bridge_inbound_protocols` 来主张。合规的入向 Bridge Node MUST：

1. 在其 NWM（§4.1）中公告 `node_type: "bridge"`，并通过 NDP `bridge_inbound_protocols`（NPS-4 §3.1）公告所服务的协议。
2. 为每个已声明的协议暴露一个服务端端点，使用该协议的原生线格式，并且**不得**要求外部客户端具备任何 NPS 寻址、NID 或帧的知识。
3. 把其所代理的 NPS 节点投射到外部协议的对象模型上：Memory Node 投射到该协议的读取面，Action / Complex Node 投射到调用面。对 **MCP** 而言，这意味着必须提供 `initialize`、`ping`、`tools/list`、`tools/call`、`resources/list`、`resources/read` —— **省略 `resources/*` 的入向 MCP Bridge 不合规**。
4. 按 §16.3 把 NWP / NPS 错误码确定性地映射到外部协议的错误空间。
5. 对未在 `bridge_inbound_protocols` 中声明的协议的请求，MUST 以 `NWP-BRIDGE-DIRECTION-UNSUPPORTED` 拒绝；响应 SHOULD 在 `hint` 中携带两个已声明的数组。
6. **每次请求无状态**，MUST NOT 参与集群拓扑。

合规的入向 Bridge Node SHOULD：

1. 对翻译后的 NWP 帧施加被代理节点自身的授权策略（§7），而不是赋予外部客户端环境权限（ambient authority）。
2. 让 NPS 状态码语义穿透映射 —— 把不同的 NPS 状态类**塌并**到同一个外部错误码上，是**合规失败**，而不是实现质量问题。

### 16.2 方向声明

`bridge_protocols` 与 `bridge_inbound_protocols` 是同一取值域（`"http"`、`"grpc"`、`"mcp"`、`"a2a"`）上的
两个独立集合。同一协议 MAY 同时出现在两者中（双向桥接）。声明 `node_roles: ["bridge"]` 的节点
MUST 保证两者至少一个非空。接收方 MUST 把缺省的 `bridge_inbound_protocols` 当作 `[]` —— 这正是
alpha.16 之前的纯出向 Bridge Node。（NPS-CR-0010 §3.2）

### 16.3 错误映射（规范性）

两个方向都要把 NPS 状态码翻译进（或翻译出）某个外部协议的错误空间。alpha.15 之前，这套映射
**每个协议被实现了两遍** —— 一遍在出向 dispatcher 里，一遍在当时独立的 `compat/*-ingress` 包里 ——
两份副本已经漂移。因此本映射是规范性的，且 MUST 由**同一份**实现同时服务两个方向。

**MCP（JSON-RPC 2.0）。** NPS 状态 → JSON-RPC 错误码：

| NPS 状态 | JSON-RPC 码 | 说明 |
|---|---|---|
| `NPS-CLIENT-BAD-FRAME` | `-32600`（Invalid Request）| |
| `NPS-CLIENT-BAD-PARAM`、`NPS-CLIENT-UNPROCESSABLE` | `-32602`（Invalid params）| |
| `NPS-CLIENT-NOT-FOUND` | `tools/call` 中未知 tool → `-32601`（Method not found）；`resources/read` 中未知 URI → `-32602` | 这个区分有实际意义：对 MCP 客户端而言，未知 **tool** 是「方法不存在」，而未知 **resource** 是「参数错了」 |
| `NPS-CLIENT-GONE` | `-32602` | |
| `NPS-CLIENT-CONFLICT` | `-32004`（实现自定义）| |
| `NPS-AUTH-UNAUTHENTICATED` | `-32001`（实现自定义）| MUST 是 JSON-RPC **错误**，绝不能是携带错误负载的成功响应 |
| `NPS-AUTH-FORBIDDEN` | `-32003`（实现自定义）| MUST NOT 塌并到 `-32001` |
| `NPS-LIMIT-RATE`、`NPS-LIMIT-BUDGET`、`NPS-LIMIT-PAYLOAD`、`NPS-LIMIT-RESOURCE` | `-32005`（实现自定义）| |
| `NPS-SERVER-UNSUPPORTED` | `-32601`（Method not found）| 含 `NWP-BRIDGE-DIRECTION-UNSUPPORTED` |
| `NPS-SERVER-INTERNAL`、`NPS-SERVER-UNAVAILABLE`、`NPS-SERVER-TIMEOUT`、`NPS-DOWNSTREAM-UNAVAILABLE` | `-32603`（Internal error）| 上游节点故障 |
| 分发前解析失败 | `-32700`（Parse error）| |

实现自定义码 `-32002` 被**保留，MUST NOT 发出**。alpha.15 之前 .NET Bridge 用它表示「tool 不存在」；
NPS-CR-0010 改为把未知 tool 映射到 `-32601`（Method not found）—— 这本来就是 MCP 客户端已经理解的语义 ——
并且**保留** `-32002` 不再复用，以免仍按旧行为编写的客户端把别的错误误读成「工具不存在」。

反方向（JSON-RPC 错误 → NPS 状态）是本表的逆映射；在逆映射非单射之处，实现 MUST 选择**最具体**的
NPS 状态，绝不能一律回落到 `NPS-SERVER-INTERNAL`。

**gRPC。** NPS 状态 → gRPC 状态码：

| NPS 状态 | gRPC 状态 |
|---|---|
| `NPS-CLIENT-BAD-FRAME`、`NPS-CLIENT-BAD-PARAM`、`NPS-CLIENT-UNPROCESSABLE` | `INVALID_ARGUMENT` |
| `NPS-CLIENT-NOT-FOUND`、`NPS-CLIENT-GONE` | `NOT_FOUND` |
| `NPS-CLIENT-CONFLICT` | `ABORTED` |
| `NPS-AUTH-UNAUTHENTICATED` | `UNAUTHENTICATED` |
| `NPS-AUTH-FORBIDDEN` | `PERMISSION_DENIED` |
| `NPS-LIMIT-RATE`、`NPS-LIMIT-BUDGET`、`NPS-LIMIT-PAYLOAD`、`NPS-LIMIT-RESOURCE` | `RESOURCE_EXHAUSTED` |
| `NPS-SERVER-UNSUPPORTED` | `UNIMPLEMENTED` |
| `NPS-SERVER-INTERNAL` | `INTERNAL` |
| `NPS-SERVER-UNAVAILABLE`、`NPS-DOWNSTREAM-UNAVAILABLE` | `UNAVAILABLE` |
| `NPS-SERVER-TIMEOUT` | `DEADLINE_EXCEEDED` |

**A2A。** NPS 状态 → A2A task 状态：client 类错误以 `failed` 终止 task，并在失败详情中**原样保留** NPS 码；
server 类错误以 `failed` 终止且可重试。A2A Bridge MUST NOT 把错误静默降级成 `completed` 的 task。

实现 MUST NOT 把不同的 NPS 状态类塌并到同一个外部错误码上（§16.1.2 SHOULD-2 使之可观测）；这么做是合规失败。

### 16.4 `bridge_target` 测试向量

规范 wire 形状为 `{ "protocol", "endpoint", "extras"? }`（SDK in-memory form）；`headers` 与其他按协议变化的参数位于 `extras` 中。所有六个 SDK MUST 对这些向量做一致往返（`from_dict(to_dict(x)) == x`）：

```json
{ "protocol": "http", "endpoint": "https://api.example.com/v1/orders" }
```

```json
{
  "protocol": "http",
  "endpoint": "https://api.example.com/v1/orders",
  "extras": { "method": "POST", "headers": { "X-Tenant": "acme" } }
}
```

```json
{ "protocol": "grpc", "endpoint": "grpc.example.com:443/orders.OrderService/Create" }
```

```json
{ "protocol": "mcp", "endpoint": "https://mcp.example.com/sse", "extras": { "tool": "create_order" } }
```

向量规则：

- `protocol` 与 `endpoint` 必填，且 MUST 原样保留。
- `extras` 为空或缺失时 MUST 从序列化结果中省略（不得输出为 `null`）。
- `BridgeNodeDescriptor` 序列化 `supported_protocols` 时使用 **排序后** 数组，以保证各 SDK 输出稳定。

### 16.5 可移植 Node 与 Bridge 服务端 Profile

NWP v0.20 为 SDK 承载的服务端定义与传输无关的决策 profile，不增加 frame type。实现 MAY 暴露
框架专用 middleware，但 admission、dispatch、取消与错误决策 MUST 与
`spec/conformance/nwp/` 中的共享向量一致。

#### 16.5.1 Node admission 与 dispatch

声明可移植 profile 的 Memory、Action、Complex Node 服务端 MUST：

1. 以 `application/nwp-manifest+json` 提供 `/.nwm`，且必须使用 `GET`；方法拒绝 MUST
   返回 HTTP 405 并携带 `Allow: GET`。Memory 与 Complex Node MUST 分发 `QueryFrame`；
   Action 与 Complex Node MUST 分发 `ActionFrame`。
2. `/query` 与 `/invoke` 必须使用 `POST`。方法拒绝发生在 frame admission 之前，返回 HTTP 405，
   且 MUST 携带 `Allow: POST`。
3. 接受 `application/nwp-frame`。在 alpha.17 兼容窗口内还 MUST 接受旧请求 media type
   `application/x-nps-frame`，但 MUST 只发送规范响应 media type：
   `application/nwp-capsule`、`application/nwp-error+json` 与
   `application/nwp-manifest+json`。旧别名已弃用，并在 alpha.18 从必需 profile 中移除。
4. 解码前执行有限且可配置的 HTTP body 上限。超限 MUST 返回 HTTP 413，以及
   `NPS-LIMIT-PAYLOAD` / `NWP-HTTP-BODY-TOO-LARGE`。
5. 对不支持的 `Content-Type`、无法满足的 `Accept` 与畸形 body 返回 §9.5 规范错误；HTTP
   错误 body MUST 使用 `application/nwp-error+json`。
6. 端到端保留请求关联标识：HTTP 模式使用 `X-NWP-Request-ID`，native 模式使用 frame
   `request_id`。具有关联字段的响应 MUST 回传该值。
7. 把调用方取消传播到解码和 provider/handler 工作。若响应提交前观察到取消，服务端 MUST
   直接中止，不得合成 ErrorFrame 或 HTTP 错误响应。
8. 记录且仅记录一个终态 telemetry outcome：`success`、`rejected`、`cancelled` 或
   `timeout`；telemetry 系统允许时 SHOULD 附带关联标识。

native 模式下，不受支持的已解码 frame type MUST 产生 `NPS-CLIENT-BAD-FRAME` /
`NWP-NATIVE-FRAME-UNSUPPORTED` 的 `ErrorFrame`。成功 admission 后的 provider 失败继续使用
`NWP-NATIVE-DISPATCH-FAILED`，除非存在更具体的协议错误。

规范用例为 `portable_node_server_vectors.json`。

#### 16.5.2 Bridge preflight 与生命周期

声明可移植 profile 的出向 Bridge 服务端 MUST 在拨号前执行：

1. 校验 `bridge_target` 形状，然后在已注册 dispatcher 集合中解析 `protocol`。缺少协议为
   `NPS-CLIENT-UNPROCESSABLE` / `NWP-BRIDGE-TARGET-INVALID`；格式正确但未注册的协议为
   `NPS-SERVER-UNSUPPORTED` / `NWP-BRIDGE-PROTOCOL-UNSUPPORTED`。
2. 校验绝对 endpoint scheme，并施加配置的 HTTPS、prefix allowlist、private/loopback
   地址策略。拒绝使用 `NPS-CLIENT-UNPROCESSABLE` /
   `NWP-BRIDGE-ENDPOINT-INVALID`。SDK 策略决策 MUST 可重复，且字面地址输入 MUST NOT
   依赖 DNS；拨号时解析的 host 仍须接受防 DNS rebinding 的地址检查。
3. 对 preflight、连接、响应头、body 翻译和响应发送施加一个有限 deadline。deadline 耗尽时
   返回 HTTP 504，以及 `NPS-SERVER-TIMEOUT` /
   `NWP-BRIDGE-UPSTREAM-FAILED`；取消优先于超时，且不得合成响应。
4. 目标协议提供关联/metadata 机制时，把关联标识传播到外部请求，并保留同步/异步 Action
   task mode。获准的异步 action 返回 `NPS-OK-ACCEPTED`。
5. 按 §16.5.1 相同规则发出一个终态 telemetry outcome。

规范用例为 `bridge_lifecycle_vectors.json`。六个参考 SDK MUST 在 CI 中执行两套 v0.20
向量；HTTP 与 native 宿主的决策、status/error 对、响应 media type 和关联行为必须等价。

---

## 17. 变更历史

| 版本 | 日期 | 变更 |
|------|------|------|
| 0.23 | 2026-09-09 | Alpha.20 feature-dependent HTTP 准入：NWM 声明、精确必需特性头、全后端路由保证、零派发拒绝及成功回显；未使用时保持基础行为。 另定义上下文载体/完整请求绑定，以及治理 challenge 状态、固定 Anchor、管理动作与持久保留。 |
| 0.22 | 2026-08-31 | alpha.19 NWM/订阅加固：确定性的 stability/SLA/billing 归一化与 action fallback；声明式可续订 subscription policy；带 principal binding、lease clamp、显式 deadline、不可复活 expiry 的 open/renew/close operation，以及共享合规向量。新增两个订阅错误码；Depends-On 推进至 NCP 0.12、NIP 0.15、NDP 0.13。|
| 0.21 | 2026-08-12 | **NPS-CR-0011 有状态 LLM context/增量 completion**：NWM LLM profile 0.2 显式广告 context operation/persistence/limit；`llm.complete` 新增可选的 owner-bound 不透明 context request 与终结 receipt；新增 status/release 生命周期 action、CAS 版本、binding 校验、原子 cancel/stream commit、重启/幂等语义、实测 `wire_input_bytes`、七个确定性错误与共享合规向量。Stateless completion 保持兼容，stateful 请求绝不静默降级。Depends-On NIP 推进到 v0.14，以使用 `llm:context`。不新增 frame type。 |
| 0.20 | 2026-07-29 | 新增 §16.5 可移植 Node/Bridge 服务端 profile 与跨语言共享向量。标准化 HTTP/native admission、角色分发、alpha.17 兼容窗口内的规范/旧 MIME 处理、有限 body 上限、取消、关联标识传播、Bridge dispatcher/SSRF/deadline preflight、异步 task mode 与终态 telemetry outcome。新增 `NWP-HTTP-BODY-TOO-LARGE` → `NPS-LIMIT-PAYLOAD`；不增加 frame type。Depends-On 推进到 NCP v0.11 与 NIP v0.13。 |
| 0.19 | 2026-07-23 | **NPS-CR-0010 Bridge Node 是双向的**：解决规范自身的矛盾 —— §2.1 节点分类表、"已移除类型"注记、以及 NPS-CR-0001 本身都把 Bridge Node 定义为 NPS↔非-NPS 双向翻译，而 §2.1 的 callout 与规范性 MUST 列表却把它收窄成仅 NPS→外部。该收窄的唯一存在理由是让 `Bridge` 一名与当时独立的 `compat/*-ingress` 包区分开；那些包现已并入 Bridge 包，限制解除。Bridge Node 语义重构为 **出向**（不变）+ **入向**（新增）两组 MUST；MCP 入向 MUST 同时提供 `resources/*` 与 `tools/*`。§16 拆为两个独立合规 profile（§16.1.1 出向 / §16.1.2 入向）、规范性方向声明（§16.2）、以及规范性的分协议错误映射表（§16.3）—— 后者 MUST 由同一份实现同时服务两个方向。明确「角色 vs 库」边界：只有发出 `node_roles: ["bridge"]` 公告的部署才是 Bridge Node。`Depends-On` 升级 NDP 至 v0.11（定义 `bridge_inbound_protocols`）。新增一个错误码 `NWP-BRIDGE-DIRECTION-UNSUPPORTED`。Additive 且向后兼容：纯出向 Bridge Node 无需改动即保持合规。（正文中文翻译已同步 §2.1 与 §16 全章；由 edge 线 0.16 重编号 —— 已发布的 alpha.16 线独立把 0.15–0.17 用于下方 LLM profile 系列。）|
| 0.18 | 2026-07-23 | **NPS-CR-0009 多 Anchor 高可用**：定案 §12.2 的两个 `anchor_state` 子类型 `anchor_failover`（`successor_nid` / `cluster_epoch` / `reason`）与 `anchor_quorum_lost`（`quorum_size` / `available`），解除原 Phase-3 占位的「MUST NOT emit」限制。拓扑响应/写入新增 `cluster_epoch`（uint64）所有权栅栏；standby 的写入以 `NWP-ANCHOR-NOT-LEADER` 拒绝，被取代的 leader 以 `NWP-ANCHOR-EPOCH-FENCED` 围栏。Additive 且按 Phase 门控：单 Anchor 集群保持 `cluster_epoch = 1`，不受影响。`Depends-On` 升级 NDP 至 v0.10（定义 AnnounceFrame 上的 `cluster_epoch` 与最高 epoch 解析规则）。新增两个错误码。|
| 0.17 | 2026-07-05 | 新增 §9.5 HTTP Binding 拒绝错误码，为 HTTP overlay 前置条件拒绝和“已声明但未实现”的 capability rollout 窗口增加 6 个规范化 NWP 错误码：`NWP-HTTP-ORIGIN-FORBIDDEN`、`NWP-HTTP-CONTENT-TYPE-UNSUPPORTED`、`NWP-HTTP-ACCEPT-UNSATISFIABLE`、`NWP-HTTP-REQUEST-ID-MISMATCH`、`NWP-HTTP-FRAME-BODY-MALFORMED`、`NWP-CAPABILITY-ADVERTISED-UNIMPLEMENTED`。共享 `error-codes.md` 升级到 v1.6。 |
| 0.16 | 2026-07-04 | 新增 §4.2a NWM `profiles` 与标准 LLM/Thinking Profile（`profiles.llm`），用于模型服务型 Action/Complex Node。明确 "Thinking Node" 是产品层别名，不是新的 `node_type`；粗粒度发现使用 NIP/NDP `llm:*` capabilities，具体模型、流式、工具、隐私与 reasoning 暴露元数据放在 NWM。无新增 frame type 或错误码。`Depends-On` NIP 升级到 v0.11，以使用 `llm:*` capability 注册表。 |
| 0.15 | 2026-07-04 | 新增 §7.5，标准化 `llm.complete` ActionFrame contract：typed request/response DTO 形状、stop_reason 枚举、tool call 字段名、同步/异步/流式响应语义、ErrorFrame 与 payload error 的边界，以及 snake_case JSON/MessagePack key 策略。无新增 frame type 或错误码。 |
| 0.14 | 2026-06-12 | 新增 §16 **Bridge Node 合规性**：形式化 MUST/SHOULD 要求（公告 `node_type: "bridge"`、校验 `bridge_target.protocol`、未知字段不透明透传、无状态 / 不参与拓扑）+ 规范化 `bridge_target` 往返测试向量（http / grpc / mcp，含 `extras`），六个 SDK 必须一致往返。原 §16 变更历史重编号为 §17。无新错误码；`Depends-On` 无变化。 |
| 0.14 | 2026-06-03 | NWM `manifest_version` 类型由不透明字符串（ETag）改为 uint32 单调计数器（从 1 开始，每次结构性变更递增）。新增 NWM 字段 `manifest_updated_at`（ISO 8601，可选），记录最近一次变更时间戳。服务端 MUST 在每次 `GET /.nwm` 响应中返回 `X-NWM-Version: <manifest_version>`；Agent 使用 `If-None-Match: <uint32>` 做条件请求。无新错误码；缓存命中沿用 `304 Not Modified`。 |
| 0.13 | 2026-05-28 | §13 SubscribeFrame (0x12) 正式规范（关闭 CR-0006）：字段表（subscription_id、filter、heartbeat_interval_ms、max_events、cursor）、生命周期（open→active→heartbeat→close）、错误码索引。§12.4 `topology:subscribe` 的强制级别由 SHOULD 提升为 MUST；无法强制的节点 MUST 在 NWM stability 元数据中记录该未强制情况。NWM 新增可选字段 `trust_anchors`（CA NID URN 数组）。BridgeNode `bridge_target` 对象 schema 标准化（protocol + endpoint + extras 载体）。 |
| 0.12 | 2026-05-11 | 补齐 NPS-CR-0002 Phase 2 的规范缺口。§8.2 DiffFrame 扩展表新增可选字段 `cgn_est`（uint32），按 [token-budget.cn.md §7.2](token-budget.cn.md) 用于推送流上的逐事件 CGN 上报；表格列重排以包含「必填」。§12.2 `topology.stream` 事件表：`anchor_state` 行新增显式子类型判别 schema（`version_rebased` 定义于 Phase 1–2；`anchor_failover` 与 `anchor_quorum_lost` 保留为 Phase 3 占位槽 —— stable 之前实现 MUST NOT 发出 Phase 3 子类型，且 MUST 忽略未知子类型以保证前向兼容）；`resync_required` 的触发条件与 `reason` 枚举扩宽（`version_too_old` / `anchor_rebased` / `server_state_lost`）。§12.4 Phase 1–2 授权模型扩充：(a) capability 闸门按接口拆分 —— `topology.snapshot` 要求 `topology:read`；`topology.stream` 要求 `topology:read`，且在 Phase 2 SHOULD 额外要求 `topology:subscribe`（Phase 3 为 MUST）；(b) 新增流中拒绝规则 —— capability 被撤销时，服务端 MUST 推送终止性 `NWP-TOPOLOGY-UNAUTHORIZED` 事件并关闭流；(c) 新增声誉交互 —— 对活跃订阅，声明了 `reputation_policy` 的 Anchor 在订阅方声誉跌破阈值时 SHOULD 推送终止性 `NWP-AUTH-REPUTATION-BLOCKED` 并关闭流。无新错误码；复用既有的 `NWP-TOPOLOGY-UNAUTHORIZED` 与 `NWP-AUTH-REPUTATION-BLOCKED`。`Depends-On` 无变化。详见 issue #41。 |
| 0.11 | 2026-05-10 | NWM 新增可选顶层字段 `stability`（`experimental`/`stable`/`deprecated`）、`sla`（对象：`p95_latency_ms`、`availability`、`sla_tier`）与 `billing`（对象：`metering_profile`、`billing_unit`、`price_hint`、`currency`）（§4.1、§4.4a、§4.4b）。ActionSpec（§4.6）新增对应的 per-action `stability` / `sla` / `billing` 覆盖，并支持字段级回落到顶层取值。所有字段均为参考性（协议层不强制）且向后兼容 —— 0.11 之前的清单一律视为 `stability="stable"` 且无 SLO/计费元数据。使 marketplace / NeuronHub 客户端可按生命周期阶段与商业档位过滤、告警或排序，满足 AaaS-Profile 的发现需求。无新错误码；`Depends-On` 无变化。详见 issue #36。 |
| 0.10 | 2026-05-01 | §12.4 授权模型把原先的「由实现自定」替换为 Phase 1–2 最小约束：Anchor Node MUST 要求 `IdentFrame.capabilities` 含 `topology:read`（capability 闸门，自声明但已签名）；SHOULD 交叉校验 NDP `node_roles` 含 `"anchor"` 作为纵深防御；Phase 3 [RFC-0002 stable] 增加 CA 背书的 `id-nps-node-roles` 证书扩展。§14.7 更新为引用 §12.4 定义的最小约束，取代原先「SHOULD restrict」的模糊措辞。`Depends-On` NIP 升级为 v0.6（定义 `topology:read` capability）。 |
| 0.9 | 2026-05-01 | **破坏性更名（pre-1.0）**：拓扑成员对象字段 `node_kind` 更名为 `node_roles`（§12.1）；拓扑流过滤键 `node_kind` 更名为 `node_roles`（§12.2）。§2.1 更新 `node_kind` 引用为 `node_roles`。新增 §2.1 **节点角色解析**：`node_roles`（NDP，发现层，数组）与 `node_type`（NWM，服务层，字符串）是两个独立字段——`node_type` MUST 为 `node_roles` 中的一项；验证方 SHOULD 对照缓存 NDP 数据校验。§4.1 `node_type` 描述更新，补充跨协议约束及 §2.1 指针。§14.7 `node_kind` 引用更新为 `node_roles`。Depends-On NDP 升级为 v0.6。修复 M1 命名消歧问题。 |
| 0.8 | 2026-04-27 | 新增 §12 **保留查询类型**，引入 `topology.*` 命名空间，强制于 NPS-AaaS Profile L2：`topology.snapshot`（QueryFrame，`type="topology.snapshot"`）与 `topology.stream`（SubscribeFrame，`type="topology.stream"`）。QueryFrame §6.1 与 SubscribeFrame §8.1 各新增可选顶层字段 `type` 用于选入保留类型。DiffFrame §8.2 `event_type` 通过保留订阅类型扩展枚举 —— `topology.stream` 增加 `member_joined` / `member_left` / `member_updated` / `anchor_state` / `resync_required`。新增 4 条错误码：`NWP-TOPOLOGY-UNAUTHORIZED`、`NWP-TOPOLOGY-UNSUPPORTED-SCOPE`、`NWP-TOPOLOGY-DEPTH-UNSUPPORTED`、`NWP-TOPOLOGY-FILTER-UNSUPPORTED`（§13）。新增 §14.7 拓扑读取安全节。原 §12 错误码 / §13 安全考量 / §14 变更历史顺延为 §13 / §14 / §15 以容纳新章节。详见 [NPS-CR-0002](cr/NPS-CR-0002-anchor-topology-queries.md)。 |
| 0.7 | 2026-04-26 | **破坏性。** §2.1 节点类型：移除 `Gateway Node`；替换为 **Anchor Node**（集群控制平面 + NOP 路由 —— 继承既有角色）与 **Bridge Node**（NPS↔非-NPS 协议翻译 —— 全新）。NWM `node_type` 枚举更新；遗留 `"gateway"` MUST 拒绝。Anchor Node 详细语义（§2.1 内嵌）覆盖成员分派 + 可选注册表；Bridge Node 语义覆盖 HTTP/gRPC/MCP/A2A 目标适配器。Depends-On 升级为 NDP v0.8，引入 `node_kind` 数组形式 + `cluster_anchor` + `bridge_protocols` 字段。详见 [NPS-CR-0001](cr/NPS-CR-0001-anchor-bridge-split.md)。 |
| 0.6 | 2026-04-25 | NWM 新增可选顶层字段 `min_assurance_level`（`anonymous` / `attested` / `verified`），允许在单个 ActionSpec 上以 `min_assurance_level` 做 per-action 覆盖（§4.6）。新增错误码 `NWP-AUTH-ASSURANCE-TOO-LOW`（`NPS-AUTH-FORBIDDEN`）。`Depends-On` 升级为 NCP v0.7（NPS-RFC-0001）+ NIP v0.9（NPS-RFC-0003）。详见 [NPS-RFC-0003](rfcs/NPS-RFC-0003-agent-identity-assurance-levels.cn.md)。 |
| 0.4 | 2026-04-14 | §3.2 新增 `/actions` 子路径；§4.1 NWM 新增 `actions` 字段；§4.2 capabilities 新增 stream_query、aggregate；§4.6 NWM Action 注册表（ActionSpec、params_anchor/result_anchor/async/idempotent）；QueryFrame §6.1 新增 `stream`、`aggregate`、`request_id`；§6.6 流式查询协议（StreamFrame 序列、estimated_total、提前终止）；§6.7 聚合查询（COUNT/SUM/AVG/MIN/MAX/COUNT_DISTINCT、group_by、having）；ActionFrame §7.1 新增 `request_id`；SubscribeFrame §8.1 新增 `resume_from_seq`；§8.2 DiffFrame 扩展字段（seq 单调递增、event_type、timestamp）及断线恢复语义；§9.1/9.2 新增 X-NWP-Request-ID；§9.4 HTTP 模式错误响应格式（application/nwp-error+json）；§10 更新完整示例（含错误响应）；§13.6 callback_url 防滥用安全节；新增 5 条错误码（AGGREGATE-UNSUPPORTED/-INVALID、STREAM-UNSUPPORTED、SUBSCRIBE-SEQ-TOO-OLD、task cancel 系列）|
| 0.3 | 2026-04-14 | SubscribeFrame (0x12)；auto_anchor；Filter $not/$exists/$regex；ActionFrame callback_url/priority；system.task.*；NWM min_agent_version/rate_limits；§14.4/14.5 安全节 |
| 0.2 | 2026-04-12 | 统一端口 17433；AnchorFrame 改为 Node 发布；CGN 计量；NPS 状态码映射 |
| 0.1 | 2026-04-10 | 初始规范 |

---

*归属：LabAcacia / INNO LOTUS PTY LTD · Apache 2.0*
