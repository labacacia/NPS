[English Version](./NPS-1-NCP.md) | 中文版

# NPS-1: Neural Communication Protocol (NCP)

**Spec Number**: NPS-1  
**Status**: Proposed  
**Version**: 0.13
**Date**: 2026-09-09
**Port**: 17433（默认，全协议族共用）  
**Authors**: Ori Lynn / INNO LOTUS PTY LTD  

> 本文档为 NCP 详细规范。套件总览见 [NPS-0-Overview.cn.md](NPS-0-Overview.cn.md)。

---

## 1. Terminology（术语）

本文档中的关键字 "MUST"、"MUST NOT"、"REQUIRED"、"SHOULD"、"SHOULD NOT"、"MAY" 按照 RFC 2119 解释。

---

## 2. 协议概述

NCP 是 NPS 的帧格式与通信基础层，定义 AI 节点间的帧结构、编码层级和语义压缩机制。所有上层协议（NWP/NIP/NDP/NOP）均以 NCP 帧为载体传输。

### 2.1 角色

- **Sender**：发起帧的一方（Agent 或节点）
- **Receiver**：接收并处理帧的一方
- **Relay**：透明转发帧，不修改 Payload（可选，用于代理场景）

### 2.2 传输模式

NCP 支持两种传输模式，帧格式完全一致，仅承载方式不同：

| 模式 | 承载 | 适用场景 | 推荐阶段 |
|------|------|---------|---------|
| **HTTP 模式** | NCP 帧序列化在 HTTP Body 中，配合 `X-NWP-*` 请求头 | Overlay 部署、与现有 Web 基础设施兼容、防火墙友好 | Phase 1 推荐 |
| **原生模式** | 直接 TCP/QUIC 连接，NCP 帧为线上格式，无 HTTP 开销 | 高性能 Agent-to-Agent、低延迟场景 | Phase 2+ |

**HTTP 模式**

```
POST /nwp/products/query HTTP/1.1
Host: api.example.com:17433
Content-Type: application/nwp-frame
X-NWP-Agent: urn:nps:agent:ca.innolotus.com:550e8400

[NCP Frame bytes / JSON]
```

**原生模式**

```
TCP connect → api.example.com:17433
→ HelloFrame (握手，0x06)
← CapsFrame (能力协商，0x04)
→ QueryFrame (0x10)
← CapsFrame (响应，0x04)
```

两种模式的帧 Payload 完全相同。原生模式下，错误通过 ErrorFrame (0xFE) 返回；HTTP 模式下，同时返回 HTTP 状态码（映射见 [status-codes.cn.md](status-codes.cn.md)）。

### 2.3 统一端口

NPS 全协议族默认共用 **端口 17433**。不同协议帧通过帧类型码（Frame Type）路由：

| 帧类型范围 | 协议 |
|-----------|------|
| 0x01–0x0F | NCP |
| 0x10–0x1F | NWP |
| 0x20–0x2F | NIP |
| 0x30–0x3F | NDP |
| 0x40–0x4F | NOP |
| 0xF0–0xFF | Reserved（含 ErrorFrame 0xFE）|

实现 MAY 为各协议分配独立端口用于隔离部署，但默认行为 MUST 支持单端口复用。

### 2.4 帧交换模式

| 模式 | 描述 | 典型帧序列 |
|------|------|-----------|
| 请求-响应 | 单次查询或操作 | QueryFrame → CapsFrame |
| 流式推送 | 大数据集或实时数据 | QueryFrame → StreamFrame × N → StreamFrame(is_last=true) |
| 增量订阅 | 变更通知 | 订阅请求 → DiffFrame × N |
| 多 Agent 同步 | 任务状态对齐 | AlignStream × N（见 NPS-5） |

### 2.5 连接状态机

```
┌──────────┐  connect()  ┌──────────────────┐  anchor_ok  ┌─────────────┐
│  CLOSED  │ ──────────→ │ ANCHOR_NEGOTIATE  │ ──────────→ │ ESTABLISHED │
└──────────┘             └──────────────────┘             └─────────────┘
                                │ timeout/error                  │ close()
                                ↓                                ↓
                          ┌──────────┐                   ┌──────────────┐
                          │  FAILED  │                   │   CLOSING    │
                          └──────────┘                   └──────────────┘
                                                                 │
                                                                 ↓
                                                           ┌──────────┐
                                                           │  CLOSED  │
                                                           └──────────┘
```

### 2.6 连接握手序列（原生模式）

原生模式下，连接建立 MUST 遵循以下握手流程：

```
Client (Agent)                        Server (Node)
      │                                     │
      │── TCP/QUIC connect ──────────────→  │
      │── 8 字节 "NPS/1.0\n"（前导）─────→  │  校验；不匹配则静默关闭（不发 ErrorFrame）
      │── HelloFrame (0x06) ─────────────→  │  声明客户端版本与能力
      │                                     │  版本协商 & 能力交集计算
      │  ←──────────────── CapsFrame (0x04) │  返回协商后的服务端能力
      │                                     │  (若不兼容则返回 ErrorFrame + 断开)
      │  [可选] 预加载 AnchorFrame            │
      │── GET /.schema ──────────────────→  │
      │  ←──────────────── AnchorFrame(s)   │
      │                                     │
      │  ── ESTABLISHED ───────────────── ESTABLISHED
```

**版本协商规则**

- Server MUST 选择 min(client.nps_version, server.nps_version) 作为会话版本
- 若 client.min_version > server.nps_version，Server MUST 返回 `NCP-VERSION-INCOMPATIBLE` 并关闭连接
- 协商结果是会话级编码策略（encoding policy），从 `client.supported_encodings` 按优先级选择，并受服务端支持集合约束。服务端在多个编码都可用时 SHOULD 尊重客户端优先级。
- 一个会话 SHOULD 使用稳定的默认编码策略：生产环境用 Tier-2 MsgPack，开发/兼容场景用 Tier-1 JSON，Tier-3 BinaryVector 只作为已协商的向量密集型帧专用优化。仅当符合已协商策略时，才允许逐帧使用不同 Tier（例如协商前 HelloFrame 用 JSON、普通帧用 MsgPack、NWP 向量检索 QueryFrame 用 BinaryVector）。实现 MUST NOT 把帧头 tier bits 理解为应用层可随意切换编码的许可。
- 只有当双方都支持 Tier-3 BinaryVector v1 时，服务端 MAY 启用 `binary_vector.v1`；启用它并不表示 Tier-3 成为会话默认编码。
- 协商后的 `max_frame_payload` = min(client.max_frame_payload, server.max_frame_payload)

**身份与授权绑定**

- `HelloFrame` 只是传输 / 会话层的能力协商帧。除可选的 `agent_id` 路由提示外，它 MUST NOT 携带 bearer token、授权 scope 或任何其他凭据。
- 原生模式下，当所选的上层协议要求认证时，客户端 MUST 在服务端握手 `CapsFrame` 之后、第一个需认证的 NWP/NOP 帧之前，每连接一次地出示 NIP `IdentFrame`。服务端把该 `IdentFrame` 中验证通过的 NID、capabilities、scope 与 assurance level 绑定到该 NCP 会话。当该 Anchor 随后发生集群所有权转移（多 Anchor 高可用，[NPS-CR-0009](cr/NPS-CR-0009-multi-anchor-ha.md)）时，服务端 MAY 关闭该原生连接；在连接丢失、或恢复（resumption）尝试后收到 `NCP-NID-MISMATCH` 的情况下，客户端 MUST 重新解析该集群（NDP §9，取最高 `cluster_epoch`）或改用 NWP `anchor_failover` 事件（NPS-2 §12.2）给出的 `successor_nid`，并对新的活跃 Anchor 重新建立会话。原生模式传输依 NPS-RFC-0006（Accepted）为规范性内容。
- NWP `QueryFrame`、`ActionFrame` 与 `SubscribeFrame` 不含逐消息的 auth-token 字段。原生模式下消息的授权，基于会话绑定的身份再加上逐消息的 target / scope 数据来判定。
- HTTP / Overlay 模式不使用 `HelloFrame`；使用 bearer 凭据的部署在 HTTP 传输信封中出示凭据（例如 `Authorization: Bearer ...`），与 NWP 请求头并列。bearer 凭据不嵌入 NWP 帧 payload 内部。

**错误处理**

若握手失败（版本不兼容、能力集为空等），Server MUST 返回：

```json
{
  "frame": "0xFE",
  "status": "NPS-PROTO-VERSION-INCOMPATIBLE",
  "error": "NCP-VERSION-INCOMPATIBLE",
  "message": "No compatible NPS version",
  "details": { "server_version": "0.4", "client_min_version": "0.5" }
}
```

### 2.6.1 连接前导（原生模式）

> **状态**：必选（NCP v0.7 起，由 [NPS-RFC-0001](rfcs/NPS-RFC-0001-ncp-connection-preamble.cn.md) 引入）。

原生模式下，每条 NCP 连接 MUST 在传输层（TCP / QUIC）握手完成之后、首个 `HelloFrame` 发出之前，由客户端发出一段 **8 字节常量前导**。HTTP 模式不受影响（`Content-Type: application/nwp-frame` 头部承担同样的识别职责）。

**线上格式**

```
偏移   0   1   2   3   4   5   6   7
       ┌───┬───┬───┬───┬───┬───┬───┬───┐
       │ N │ P │ S │ / │ 1 │ . │ 0 │\n │
       └───┴───┴───┴───┴───┴───┴───┴───┘
       0x4E 50  53  2F  31  2E  30  0A
```

十六进制：`4E 50 53 2F 31 2E 30 0A`。前导**不是**帧 —— 没有头部、没有 Flags、没有长度字段。帧类型字节 `0x4E` 已保留（见 [frame-registry.yaml](frame-registry.yaml) `reservations:` 段），MUST NOT 分配给任何 NCP 帧。

**客户端行为**

- 客户端 MUST 在单次 buffer 写入中发出这 8 字节（不与应用逻辑交错；不分多次写 —— 8 字节远低于任何传输 MTU）。
- 客户端 MAY 把 `HelloFrame` 紧跟前导一起 pipeline（无需等待 round-trip），因为服务端是按字节比较常量。

**服务端行为**

1. 接受原生模式连接后，服务端读取恰好 8 字节。
2. 若与 `b"NPS/1.0\n"` 完全匹配，进入帧解析流程。
3. 若不匹配：
   - 服务端 MUST 在 500 ms 内关闭连接。
   - 服务端 MUST NOT 发送 `ErrorFrame` —— 对端尚未确认是否说 NCP，发出帧字节会向扫描器泄露协议细节。
   - 服务端 MAY 记录前 32 字节用于运维诊断。
4. 若 10 秒内收到的字节少于 8 字节，按超时处理，静默关闭。

**主版本语义**

- `b"NPS/2.0\n"` 等未来主版本前导已保留。
- v1.x 服务端读到 `NPS/2.0\n` 时 MUST 关闭连接（未知 major）。MAY 在关闭之前写一条 32 字节的诊断 `NPS-PREAMBLE-UNSUPPORTED-VERSION\n` —— 因为对端已自我标识为 NPS 说话方，所以允许这次"破例"发数据。
- 次版本字节（`1.0` 中的 `0`）已保留；v1 客户端 MUST 发 `0`，v1 服务端 MUST 仅接受 `0`。未来 RFC 可能定义次版本协商。

**错误**

| 错误码 | 触发条件 |
|--------|---------|
| `NCP-FEATURE-NEGOTIATION-INVALID` | `NPS-CLIENT-BAD-FRAME` | 特性协商元数据或选择无效 |
| `NCP-FEATURE-REQUIRED-UNSUPPORTED` | `NPS-CLIENT-CONFLICT` | 必需原生特性没有合格共同 revision |
| `NCP-PREAMBLE-INVALID`（`NPS-PROTO-PREAMBLE-INVALID`）| 首 8 字节不匹配常量；500 ms 内静默关闭。|
| （超时）| 10 秒内字节不足 8；静默关闭。线上不发任何编码。|

**服务端状态机（原生模式）**

```
[LISTEN] ──accept──→ [PREAMBLE-WAIT] ──8 字节匹配──→ [FRAMING] ──HelloFrame──→ [HANDSHAKE] ──CapsFrame──→ [ESTABLISHED]
                          │
                          ├──不匹配──→ [CLOSING]（≤ 500 ms）
                          └──10 秒超时──→ [CLOSING]（静默）
```

完整设计动机、备选方案、SDK 推进 phase，参见 [NPS-RFC-0001](rfcs/NPS-RFC-0001-ncp-connection-preamble.cn.md)。

### 2.6.2 原生服务端互操作 Profile（NCP v0.11）

本 Profile 定义可移植的原生模式 NCP 服务端在外部可观测层面的最小行为要求，对 TCP 与 QUIC 流式实现同等适用。
[`conformance/ncp/native_server_handshake_vectors.json`](conformance/ncp/native_server_handshake_vectors.json)
中的语言无关用例为规范性内容。

**传输与读取边界**

1. 配置了传输层认证时，服务端 MUST 在读取 NCP 前导之前先完成认证或流封装 hook。
   要求认证流的部署，若未配置 hook 或 hook 返回原始未认证流，MUST 在本地失败。
2. 服务端 MUST 对前导读取设定上界，默认上限为 10 秒。
   非法或不完整的前导按 §2.6.1 静默关闭。
3. 前导有效之后，服务端 MUST 对完整 Hello 读取（头部加 Payload）单独设定上界，默认上限为 5 秒。
4. 服务端 MUST 在为 Hello Payload 分配内存之前先检查帧头。
   `max_hello_payload` 默认 65,535 字节，并 MUST 可配置为更低的部署上限。
5. 第一个帧 MUST 是未加密的 Tier-1 JSON `HelloFrame`，且 `EXT=0`。帧类型错误、编码 Tier 为保留值、
   `ENC=1`、`EXT=1`、头部/Payload 格式错误、Payload 超限或 Hello 超时，均导致静默关闭。
   这些准入前失败 MUST NOT 发出 `ErrorFrame`。
6. 一旦成功解码 Hello，服务端 MUST 要么发送一个握手 `CapsFrame` 并建立会话，要么发送一个 `ErrorFrame`
   并关闭连接。在连接仍处于握手状态时，服务端 MUST NOT 继续读取应用帧。

服务端 MUST 对外暴露有界的前导与 Hello 超时。部署 MAY 调低默认值，但 MUST NOT 在不受信任的监听端口上同时禁用这两个上限。

**确定性协商**

服务端按其配置的 Profile 对解码后的 Hello 求值：

- 双端都声明一个闭区间 `min_version`-`nps_version` 范围。省略 `min_version` 等同于该端的 `nps_version`。
  版本使用严格的 `major.minor` 形式。会话版本取交集中的最高版本。无交集则返回 `NCP-VERSION-INCOMPATIBLE`。
- 稳定默认编码取客户端有序 `supported_encodings` 列表中第一个同时被服务端启用、且为 `msgpack` 或 `json`
  的 token。无稳定交集则返回 `NCP-ENCODING-UNSUPPORTED`。
- `binary_vector.v1` 仅在双端都声明时启用。它在 `enabled_encodings` 中追加在稳定默认之后，且永不成为默认值。
- `supported_protocols` 取双方列表的交集，按客户端偏好顺序排列。其中 MUST 含 `ncp`，否则服务端返回
  `NCP-VERSION-INCOMPATIBLE`。
- `max_frame_payload` 与 `max_concurrent_streams` 取双端通告值的最小值。上限为零属非法。
- `ext_support` 仅在双端都通告 `true` 时启用。

握手 `CapsFrame` 的 Payload 由握手状态区分，使用以下可移植字段。既有字段保持增量且向后兼容：

| 字段 | 类型 | 必填 | 描述 |
|---|---|---:|---|
| `node_id` | string | 必填 | 服务端 NID |
| `caps` | array[string] | 必填 | 服务端应用能力 |
| `session_version` | string | 必填 | 协商出的协议版本 |
| `negotiated_encoding` | string | 必填 | 稳定默认编码：`msgpack` 或 `json` |
| `enabled_encodings` | array[string] | 必填 | 稳定默认编码，其后跟随协商出的扩展编码 |
| `supported_protocols` | array[string] | 必填 | 协商出的上层协议交集 |
| `max_frame_payload` | uint32 | 必填 | 协商出的 Payload 上限 |
| `ext_support` | bool | 必填 | 协商出的扩展帧头支持 |
| `max_concurrent_streams` | uint32 | 必填 | 协商出的并发流上限 |
| `anchor_ref` | string | 可选 | 首次提供的 Schema 引用 |
| `payload` | object | 可选 | 实现自定义的元数据 |

本 Profile 不新增任何帧类型或错误码。

---

### 2.6.3 Feature revision 协商（NCP v0.13）

此可选 alpha.20 profile 复用 Hello 和握手 Caps，不分配新帧类型。
未使用时保留原有基础行为及兼容下限。设计记录见
[CR-0012](cr/NPS-CR-0012-feature-negotiation.cn.md)。

**NCP-FTR-01 — 有界类型。** Hello 可携带 `feature_negotiation: 1`、
`feature_offer` 和 `required_features`。没有 version 1 的 offer 无效。
Offer 最多 32 项，形如
`{"id":"example.echo","revisions":[{"revision":1,"lifecycle":"stable"}]}`。
ID 必须匹配 ASCII `^[a-z][a-z0-9]*([.][a-z][a-z0-9_-]*)+$`，最多 96 字节；
`nps.` 保留给公开 NPS profile。每个 ID 有 1..16 个唯一、升序的 uint16
revision（1..65535），每个 revision 的 lifecycle 为 experimental、stable 或
deprecated。必需 ID 数组最多 32 个唯一的已提出 ID。缺失可选数组视为空。
特性结构中的未知成员、重复 JSON key/ID/revision、布尔或小数 revision、
不支持的版本，必须以 `NCP-FEATURE-NEGOTIATION-INVALID` 拒绝。

JSON 整数字段必须使用十进制数字语法，不含小数或指数。

[特性注册表](feature-registry.json) 定义保留 ID/revision 及依赖。
必需依赖 revision 必须显式提出并选中，不得添加未提出 revision。
所选依赖冲突为 NCP-FEATURE-NEGOTIATION-INVALID。注册存在不证明 SDK 已支持。

**NCP-FTR-02 — Payload 上限。** 携带特性的 Hello 或握手 Caps，其完整编码
payload 必须不超过 16384 字节，同时受现有帧/准入上限约束，取较小者。
该上限包含基础字段，而非仅特性值，从而避免依赖重新序列化计算字节数。
特性解析前可优先返回现有帧尺寸错误。纯基础握手保留原上限。
独立 JSON 块向量测试解析边界；完整帧向量还须计入基础字段。

**NCP-FTR-03 — 选择。** Version-1 服务端必须先校验，再对每个共有 ID 选择
本地策略允许的最高共同 revision；省略未知可选 ID。Experimental 要求双方显式
启用，deprecated 要求本地策略允许。远端 lifecycle 标签不得放宽本地策略。
必需 ID 未提出时无效；无合格共同 revision 时以
`NCP-FEATURE-REQUIRED-UNSUPPORTED` 拒绝。必须验证所选组合的跨特性约束；
非法组合直接拒绝，不静默改变确定性选择。

**NCP-FTR-04 — 确认。** 握手 Caps 必须返回 `feature_negotiation: 1` 及
显式 `selected_features` 数组，可为空，最多 32 项，形如
`{"id":"example.echo","revision":1}`。客户端必须在业务写入前拒绝重复、
未提出、策略不允许或无效的选择。缺失必需 ID 时返回
`NCP-FEATURE-REQUIRED-UNSUPPORTED`。旧 Caps 无确认视为空选择：
有必需项则关闭且不派发，否则可继续纯基础行为。

**NCP-FTR-05 — 生命周期与权限。** 选择仅属于当前已准入 session。
重连/故障转移必须重新选择和身份准入。业务 Caps 不得重新协商活动会话。
撤销特性须停止准入、排空/关闭并重新协商。每次操作必须在写入前检查精确
必需 revision。支持特性不授予 subject、能力、scope 或 assurance。
扩展握手拒绝后，仅在本地策略允许、不携带必需特性或业务请求时，才可建立
新的基础连接；不得以握手降级为由重放已派发操作。

---

## 3. 帧格式

### 3.1 固定头（4 字节，默认） / 扩展头（8 字节，大帧模式）

**默认帧头（4 字节）**

```
 Byte 0          Byte 1          Byte 2–3
┌───────────────┬───────────────┬───────────────────────────┐
│  Frame Type   │     Flags     │      Payload Length       │
│   (1 byte)    │   (1 byte)    │        (2 bytes, BE)      │
└───────────────┴───────────────┴───────────────────────────┘
```

**扩展帧头（8 字节，EXT=1 时）**

```
 Byte 0          Byte 1          Byte 2–5               Byte 6–7
┌───────────────┬───────────────┬───────────────────────┬──────────────┐
│  Frame Type   │     Flags     │   Payload Length      │   Reserved   │
│   (1 byte)    │   (1 byte)    │   (4 bytes, BE)       │  (2 bytes)   │
└───────────────┴───────────────┴───────────────────────┴──────────────┘
```

- **Frame Type**：帧类型码，见帧命名空间（§2.3）
- **Flags**：控制标志位，见 §3.2
- **Payload Length**：默认 2 字节（最大 65,535），扩展模式 4 字节（最大 4,294,967,295）
- **Reserved**：2 字节（Byte 6–7，仅扩展头存在）；发送方 MUST 置 0，接收方 MUST 忽略。扩展头因此共 8 字节：1（类型）+ 1（标志）+ 4（长度）+ 2（保留）。

### 3.2 Flags 字段（逐位定义）

```
Bit 7   Bit 6   Bit 5   Bit 4   Bit 3   Bit 2   Bit 1   Bit 0
┌───────┬───────┬───────┬───────┬───────┬───────┬───────┬───────┐
│  EXT  │  RSV  │  RSV  │  RSV  │  ENC  │ FINAL │  T1   │  T0  │
└───────┴───────┴───────┴───────┴───────┴───────┴───────┴───────┘
```

| 位 | 名称 | 描述 |
|----|------|------|
| 0–1 | T0, T1 | 编码 Tier：`00`=Tier-1 JSON，`01`=Tier-2 MsgPack，`10`=Tier-3 BinaryVector v1，`11`=Reserved |
| 2 | FINAL | 流式帧最终块标志（StreamFrame 专用，其他帧固定为 1） |
| 3 | ENC | Payload 使用应用层 E2E 加密（见 §7.4）；TLS 传输层加密独立，开发模式可为 0 |
| 4–6 | RSV | 保留，MUST 为 0，接收方 MUST 忽略 |
| 7 | EXT | 扩展帧头标志：`0`=默认 4 字节帧头（Payload ≤ 64KB），`1`=扩展 8 字节帧头（Payload ≤ 4GB） |

### 3.3 帧大小配置

| 配置项 | 默认值 | 最大值 | 说明 |
|--------|--------|--------|------|
| `max_frame_payload` | 65,535 (64KB) | 4,294,967,295 (4GB) | 单帧最大 Payload 字节数 |

- 默认帧头（EXT=0）：Payload 最大 **65,535 字节**，适用于绝大多数场景
- 扩展帧头（EXT=1）：Payload 最大 **4,294,967,295 字节**，适用于大 embedding 或文档批量传输
- 实现 MUST 支持默认模式，SHOULD 支持扩展模式
- Payload 超过当前连接协商的 `max_frame_payload` 时，发送方 MUST 使用 StreamFrame 分片传输
- `max_frame_payload` 的协商通过 CapsFrame 在连接建立时完成

**分片规则**

- StreamFrame 分片：seq 从 0 递增，最后一片 FINAL=1
- 分片内每帧 MUST 遵守当前连接的 `max_frame_payload` 限制

---

## 4. 帧类型

### 4.1 AnchorFrame (0x01)

Schema 锚点帧，由 **Node 发布**，用于建立全局 Schema 引用，消除后续重复传输。

**字段定义**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `frame` | uint8 | 必填 | 固定值 `0x01` |
| `anchor_id` | string | 必填 | Schema 的 SHA-256 摘要，格式：`sha256:{64位hex}` |
| `schema` | object | 必填 | Schema 定义对象 |
| `schema.fields` | array | 必填 | 字段描述数组，见下 |
| `ttl` | uint32 | 可选 | 缓存有效期（秒），默认 3600，0 表示不缓存 |
| `supersedes` | string | 可选 | 本帧替代的旧 AnchorFrame 的 `anchor_id`。生产端 MAY 设置；当新旧两帧都在缓存中时，消费端 SHOULD 优先使用新帧（见 §5.5）|
| `valid_from` | string | 可选 | ISO-8601 UTC 时间戳，标记时序有效性窗口起点（如 `2026-05-10T00:00:00Z`）。在该时刻之前，锚点 MUST 视为无效（见 §5.5）|
| `valid_until` | string | 可选 | ISO-8601 UTC 时间戳，标记时序有效性窗口终点。在该时刻之后，锚点 MUST 视为无效（见 §5.5）|

**schema.fields 元素**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `name` | string | 必填 | 字段名 |
| `type` | string | 必填 | 数据类型：`string`/`uint64`/`int64`/`decimal`/`bool`/`timestamp`/`bytes`/`object`/`array` |
| `semantic` | string | 可选 | 语义标注，格式：`{domain}.{concept}`（见语义类型系统）|
| `nullable` | bool | 可选 | 是否可为 null，默认 false |

**anchor_id 计算规则**

1. 将 `schema` 字段按 **RFC 8785（JSON Canonicalization Scheme, JCS）** 序列化为规范化 JSON  
   — JCS 保留 Unicode 字符串原样，不做归一化；对象 key 按 UTF-16 排序，不输出多余空白（[RFC 8785 §3.1](https://www.rfc-editor.org/rfc/rfc8785.html#section-3.1)）。
   — 实现 MUST 使用 JCS 标准库（不得自行实现），以确保跨语言 SDK（.NET/Python/TS）一致
2. 计算 JCS 输出的 UTF-8 字节流的 SHA-256 摘要
3. 格式化为 `sha256:{lowercase hex}`

> **注**：JCS 参考实现列表见 https://www.rfc-editor.org/rfc/rfc8785#appendix-A

**示例**

```json
{
  "frame": "0x01",
  "anchor_id": "sha256:a3f9b2c1d4e5f6789012345678901234567890abcdef1234567890abcdef12",
  "schema": {
    "fields": [
      { "name": "id",    "type": "uint64",  "semantic": "entity.id" },
      { "name": "name",  "type": "string",  "semantic": "entity.label" },
      { "name": "price", "type": "decimal", "semantic": "commerce.price.usd" },
      { "name": "stock", "type": "uint64",  "semantic": "commerce.inventory.count" }
    ]
  },
  "ttl": 3600
}
```

**语义类型系统（部分）**

| Semantic | 含义 |
|----------|------|
| `entity.id` | 实体唯一标识符 |
| `entity.label` | 实体人类可读名称 |
| `entity.description` | 实体描述文本 |
| `entity.timestamp.created` | 创建时间 |
| `entity.timestamp.updated` | 更新时间 |
| `commerce.price.usd` | 美元价格 |
| `commerce.price.cny` | 人民币价格 |
| `commerce.inventory.count` | 库存数量 |
| `geo.latitude` | 纬度 |
| `geo.longitude` | 经度 |

---

### 4.2 DiffFrame (0x02)

增量数据帧，仅传输变更字段的 patch，适用于订阅或轮询场景。

**字段定义**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `frame` | uint8 | 必填 | 固定值 `0x02` |
| `anchor_ref` | string | 必填 | 基准 Schema 的 anchor_id |
| `base_seq` | uint64 | 必填 | 本 Diff 基于的版本序号 |
| `patch_format` | string | 可选 | 补丁格式：`json_patch`（默认）或 `binary_bitset`（Tier-2 推荐）|
| `patch` | array \| bytes | 必填 | 补丁数据（格式由 patch_format 决定，见下）|
| `entity_id` | string | 可选 | 被更新实体的 ID |

**patch_format: json_patch**（默认，Tier-1 JSON 场景）

`patch` 为 JSON Patch 操作数组（RFC 6902），每个元素含 `op`、`path`、`value`。

**patch_format: binary_bitset**（Tier-2 MsgPack 场景，减少 15–20% 体积）

`patch` 为二进制字节流（MsgPack `bin` 类型），结构如下：

```
┌──────────────────────────────────────────────────────┐
│  Changed Fields Bitset（ceil(N/8) bytes，N=Schema字段数）│
│  Bit i=1 表示第 i 个字段有变更（按 schema.fields 顺序）  │
├──────────────────────────────────────────────────────┤
│  Values（仅包含变更字段的新值，按字段顺序，MsgPack 编码）  │
└──────────────────────────────────────────────────────┘
```

规则：
- 发送方 MUST 仅在 Tier-2（MsgPack）编码帧中使用 `binary_bitset`
- 接收方若不支持 `binary_bitset`，MUST 返回 `NCP-DIFF-FORMAT-UNSUPPORTED`
- `patch_format` 省略时默认为 `json_patch`

**示例（json_patch）**

```json
{
  "frame": "0x02",
  "anchor_ref": "sha256:a3f9b2c1...",
  "base_seq": 42,
  "patch_format": "json_patch",
  "entity_id": "product:1001",
  "patch": [
    { "op": "replace", "path": "/price", "value": 299.00 },
    { "op": "replace", "path": "/stock", "value": 48 }
  ]
}
```

---

### 4.3 StreamFrame (0x03)

流式数据块帧，用于大数据集、实时推送或超帧大小的 Payload 分片。

**字段定义**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `frame` | uint8 | 必填 | 固定值 `0x03` |
| `stream_id` | string | 必填 | 流的唯一标识符（UUID v4） |
| `seq` | uint32 | 必填 | 块序号，从 0 递增，接收方按 seq 重组 |
| `is_last` | bool | 必填 | true 表示此为最后一块 |
| `anchor_ref` | string | 可选 | Schema anchor_id（首块携带，后续块可省略） |
| `data` | array | 必填 | 本块数据，符合 anchor_ref 所指 Schema |
| `window_size` | uint32 | 可选 | 接收方可缓冲的剩余帧数（见流量控制语义）|
| `error_code` | string | 可选 | 非空时表示流异常终止，is_last 强制为 true |

**流量控制语义**

NCP 提供应用层语义背压，补充 TCP/QUIC 传输层流量控制：

- **初始窗口**：发送方在首帧（seq=0）携带 `window_size`，声明接收方剩余可接受帧数
- **窗口消耗**：每发送一帧，接收方逻辑窗口减 1
- **窗口更新**：接收方准备好接收更多数据时，发送一个反向 StreamFrame（`data=[]`, `is_last=false`），携带新的 `window_size` 以刷新窗口
- **背压暂停**：接收方发送 `window_size=0` 的更新帧，发送方 MUST 暂停，直到收到非零 `window_size`
- **省略语义**：`window_size` 未设置时，表示不启用应用层流量控制（依赖底层传输）
- 发送方在窗口耗尽后继续发送，接收方 SHOULD 返回 `NCP-STREAM-WINDOW-OVERFLOW` 并可终止流

---

### 4.4 CapsFrame (0x04)

封装完整响应体，最常见的响应帧类型。也用于连接建立时的能力协商。

**字段定义**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `frame` | uint8 | 必填 | 固定值 `0x04` |
| `anchor_ref` | string | 必填 | Schema anchor_id |
| `count` | uint32 | 必填 | `data` 数组长度，MUST 等于 `len(data)` |
| `data` | array | 必填 | 数据记录数组，每条记录符合 anchor_ref Schema |
| `next_cursor` | string | 可选 | 下一页游标，Base64-URL 编码，null 表示最后一页 |
| `token_est` | uint32 | 可选 | 本响应预估 CGN 消耗（见 [token-budget.cn.md](token-budget.cn.md)）|
| `tokenizer_used` | string | 可选 | 实际使用的 tokenizer 标识 |
| `cached` | bool | 可选 | true 表示本响应来自服务端缓存 |
| `request_id` | string | 可选 | 将 unary 响应与原始请求关联。同步 NWP 操作的生产端 SHOULD 复制请求帧的 `request_id` |
| `inline_anchor` | object | 可选 | Schema 已更新时内联返回最新 AnchorFrame，避免额外 RTT（见 §5.4）|

`cached` 描述的是完整 NWP 响应缓存，不得解释为模型 prefix/KV cache 命中。
底层 runtime 能报告模型缓存使用量时，协议专属 payload MAY 提供独立的 cache usage 对象。

**连接协商 CapsFrame**

原生握手使用 §2.6.2 的专用顶层 payload，类型由二进制头的 0x04 标识，
不是下方应用响应的 anchor_ref/count/data schema。特性协商字段不得嵌入 data。
仅协商该扩展的握手使用以下示例；纯基础握手不携带 feature 字段。

```json
{
  "node_id": "urn:nps:agent:example.test:server",
  "caps": [
    "ncp"
  ],
  "session_version": "0.13",
  "negotiated_encoding": "json",
  "enabled_encodings": [
    "json"
  ],
  "supported_protocols": [
    "ncp"
  ],
  "max_frame_payload": 65535,
  "ext_support": false,
  "max_concurrent_streams": 32,
  "feature_negotiation": 1,
  "selected_features": []
}
```

**数据响应示例**

```json
{
  "frame": "0x04",
  "anchor_ref": "sha256:a3f9b2c1...",
  "count": 2,
  "data": [
    { "id": 1001, "name": "iPhone 15 Pro", "price": 999.00, "stock": 42 },
    { "id": 1002, "name": "MacBook Air M3", "price": 1299.00, "stock": 15 }
  ],
  "next_cursor": "eyJpZCI6MTAwM30",
  "token_est": 180
}
```

---

### 4.5 AlignFrame (0x05) — Deprecated

> ⚠️ AlignFrame 已在 NCP v0.7 中标记为 **Deprecated**。
> 请使用 NOP AlignStream (0x43)，见 [NPS-5-NOP.cn.md](NPS-5-NOP.cn.md)。
> AlignFrame 将在 NPS v1.0 中移除。

---

### 4.6 HelloFrame (0x06)

原生模式客户端握手帧，**由 Agent（客户端）在连接建立后首先发送**，声明自身协议版本与能力。Server 以 CapsFrame 响应（见 §2.6）。

> **命名说明**：NIP 层已定义 IdentFrame (0x20) 用于身份证书交换。NCP HelloFrame 仅负责版本与能力协商，不携带身份信息，两者职责明确分离。

**字段定义**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `frame` | uint8 | 必填 | 固定值 `0x06` |
| `nps_version` | string | 必填 | 客户端支持的最高 NPS 版本，格式 `"major.minor"` |
| `min_version` | string | 可选 | 客户端支持的最低 NPS 版本，默认与 `nps_version` 相同 |
| `supported_encodings` | array[string] | 必填 | 支持的编码格式列表，如 `["binary_vector.v1", "msgpack", "json"]`，按优先级降序 |
| `supported_protocols` | array[string] | 必填 | 支持的上层协议列表，如 `["ncp", "nwp", "nip"]` |
| `agent_id` | string | 可选 | Agent 的 NID（NIP 身份标识符），格式 `urn:nps:agent:{domain}:{id}` |
| `max_frame_payload` | uint32 | 可选 | 客户端可接受的最大帧 Payload 字节数，默认 65535 |
| `ext_support` | bool | 可选 | 是否支持扩展帧头（EXT=1），默认 false |
| `max_concurrent_streams` | uint32 | 可选 | 客户端可处理的最大并发流数，默认 32 |
| `e2e_enc_algorithms` | array[string] | 可选 | 客户端支持的 E2E 加密算法列表（见 §7.4），如 `["aes-256-gcm"]` |
| `ping_interval_ms` | uint32 | 可选 | 期望的 keepalive 间隔（毫秒）；0 = 禁用（默认）。非零时双端 SHOULD 按该间隔发送 `NopFrame`，并 MUST 在 `3 × ping_interval_ms` 内发出一个（NCP v0.8 §7.6）。|

**规则**

- HelloFrame MUST 是原生模式下客户端发送的第一个帧，发送前 MUST NOT 发送任何其他帧
- HTTP 模式不使用 HelloFrame（能力通过 `X-NWP-*` 请求头传递）
- HelloFrame 不携带加密（ENC=0），Tier 建议使用 Tier-1 JSON（握手阶段，编码尚未协商）
- Server 收到 HelloFrame 后，MUST 在 5 秒内回复 CapsFrame 或 ErrorFrame，否则客户端 SHOULD 断开连接

**HelloFrame 示例**

```json
{
  "frame": "0x06",
  "nps_version": "0.4",
  "min_version": "0.3",
  "supported_encodings": ["binary_vector.v1", "msgpack", "json"],
  "supported_protocols": ["ncp", "nwp"],
  "agent_id": "urn:nps:agent:ca.innolotus.com:550e8400",
  "max_frame_payload": 65535,
  "ext_support": false,
  "max_concurrent_streams": 16,
  "e2e_enc_algorithms": ["aes-256-gcm", "chacha20-poly1305"]
}
```

---

### 4.7 ErrorFrame (0xFE)

NPS 统一错误帧，所有协议层共用。在原生模式下承载错误响应。

**字段定义**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `frame` | uint8 | 必填 | 固定值 `0xFE` |
| `status` | string | 必填 | NPS 状态码（见 [status-codes.cn.md](status-codes.cn.md)）|
| `error` | string | 必填 | 协议级错误码（如 `NCP-ANCHOR-NOT-FOUND`）|
| `message` | string | 可选 | 人类可读错误描述 |
| `details` | object | 可选 | 错误相关的结构化数据（如 `anchor_ref`、`stream_id`）|

**示例**

```json
{
  "frame": "0xFE",
  "status": "NPS-CLIENT-NOT-FOUND",
  "error": "NCP-ANCHOR-NOT-FOUND",
  "message": "Schema anchor not found in cache, please resend AnchorFrame",
  "details": { "anchor_ref": "sha256:a3f9b2c1..." }
}
```

---

### 4.8 NopFrame (0x07) — Keepalive / Heartbeat

无 Payload 的空帧。用作空闲连接上的 keepalive 探测，防止 NAT / 防火墙超时断链，并用于探测对端失活（NCP v0.8）。

**字段定义**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `frame` | uint8 | 必填 | 固定值 `0x07` |

**规则**

- 在已建立的原生模式连接上，NopFrame MAY 在任意时刻发送（握手期间不可发送）
- 接收方 MUST 接受 NopFrame，且 MUST NOT 以 ErrorFrame 响应；SHOULD 回以另一个 NopFrame
- 当 HelloFrame 中声明了 `ping_interval_ms > 0` 时，双端 SHOULD 按声明的间隔发送 NopFrame。若在 `3 × ping_interval_ms` 内未收到任何类型的帧，该端 SHOULD 发送 `NCP-KEEPALIVE-TIMEOUT` 并关闭连接
- NopFrame MUST NOT 携带任何 Payload；Payload 长度非零的 NopFrame MUST 以 `NCP-FRAME-PAYLOAD-TOO-LARGE` 拒绝（按 0 字节上限违规处理）

---

## 5. Schema 锚定机制

### 5.1 Schema 所有权

AnchorFrame 由 **Node（数据模型拥有者）** 发布。Agent 通过读取 Node 清单或 Schema 端点获取 AnchorFrame，本地缓存后在后续请求中引用。

### 5.2 标准流程

```
Agent                              Node
  │                                  │
  │── GET /.nwm ─────────────────→│  读取节点清单，获取 schema_anchors
  │←── NWM(schema_anchors) ───────│
  │                                  │
  │── GET /.schema ──────────────→│  获取完整 AnchorFrame（可选，按需）
  │←── AnchorFrame(schema) ───────│  Agent 本地缓存 anchor_id → schema
  │                                  │
  │── QueryFrame(anchor_ref) ────→│  请求只携带 anchor_ref
  │←── CapsFrame(anchor_ref) ─────│  响应也只引用 anchor_ref
  │                                  │
  │── QueryFrame(anchor_ref) ────→│  后续请求无需重传 Schema
  │←── CapsFrame(anchor_ref) ─────│
```

### 5.3 缓存语义

- Agent MUST 缓存从 Node 获取的 AnchorFrame，以 `anchor_id` 为 key
- 缓存有效期为 AnchorFrame 中的 `ttl` 秒
- Agent SHOULD 在连接建立后预加载所有可能用到的 AnchorFrame

### 5.4 缓存未命中处理

#### 5.4.1 Schema 已更新（过期 anchor_ref）

若 Agent 引用了已过期的 `anchor_ref`，Node 发现该 Schema 版本已更新时，处理行为取决于请求端 `auto_anchor` 标志（在 NWP QueryFrame 中定义）：

- **auto_anchor=true（默认）**：Node SHOULD 在 CapsFrame 响应中附加 `inline_anchor` 字段，包含最新 AnchorFrame，避免额外 RTT：

```json
{
  "frame": "0x04",
  "anchor_ref": "sha256:new_anchor...",
  "count": 2,
  "data": [ ... ],
  "inline_anchor": {
    "frame": "0x01",
    "anchor_id": "sha256:new_anchor...",
    "schema": { "fields": [ ... ] },
    "ttl": 3600
  }
}
```

  Agent 收到含 `inline_anchor` 的响应后，MUST 更新本地缓存（替换旧 `anchor_id` 对应的 Schema）。

- **auto_anchor=false**：Node SHOULD 返回带旧 anchor_id 的 `NCP-ANCHOR-STALE`，由 Agent 主动请求新 AnchorFrame。

#### 5.4.2 未知 anchor_ref

若 Agent 引用了 Node 完全不认识的 `anchor_ref`（如手工构造、跨节点误引用），Node MUST 返回：

```json
{
  "frame": "0xFE",
  "status": "NPS-CLIENT-NOT-FOUND",
  "error": "NCP-ANCHOR-NOT-FOUND",
  "details": { "anchor_ref": "sha256:..." }
}
```

> **注**：`NCP-ANCHOR-STALE` 表示 anchor 存在但已更新；`NCP-ANCHOR-NOT-FOUND` 表示 anchor 从未被此 Node 注册。

### 5.5 Schema 演进

Schema 会随时间演进。NCP 定义了一套兼容性分类法，并规定了 AnchorFrame 跨版本生命周期内生产端与消费端的义务。§4.1 中的可选字段 `supersedes`、`valid_from`、`valid_until` 承载本节使用的版本血缘和时序有效性元数据。

#### 5.5.1 兼容性分类

任何 Schema 变更 MUST 归入下列兼容性分类之一。生产端 SHOULD 在发布说明中记录类别；消费端 MAY 据此判断是否必须重新拉取锚点。

| 分类 | 描述 | 向后兼容 |
|------|------|----------|
| `additive_optional` | 在 `schema.fields` 中新增可选字段（即 `nullable=true` 或带文档化默认值）| 是 |
| `additive_required` | 在 `schema.fields` 中新增必填字段（无默认值）| 否 |
| `type_widening` | 在不丢失既有合法值的前提下，扩大已有字段的类型（如 `uint32` → `uint64`、`string` 枚举 → 开放 `string`）| 是 |
| `type_narrowing` | 收窄已有字段的类型或取值域（如 `string` → 枚举、`int64` → `uint32`）| 否 |
| `semantic_change` | 线格式不变但已有字段的含义发生变化（如单位、币种、所指对象）| 视情况 |
| `field_remove` | 从 `schema.fields` 中移除已有字段 | 否 |

任何归类为 `additive_required`、`type_narrowing` 或 `field_remove` 的变更都属于**破坏性变更**，MUST 产生新的 `anchor_id`。生产端 MUST 在新 AnchorFrame 上将 `supersedes` 设置为旧的 `anchor_id`，以便消费端追溯版本血缘。

对于 `semantic_change`，除非生产端能通过外带文档证明无消费端依赖原有含义，否则 MUST 视同破坏性变更处理。

#### 5.5.2 时序有效性

当设置了 `valid_from` 和/或 `valid_until` 时，AnchorFrame 仅在半开区间 `[valid_from, valid_until)` 内有效。窗口之外：

- 消费端 MUST NOT 将引用此锚点的数据视为新鲜
- 一旦 `valid_until` 已过，消费端 SHOULD 立即从缓存中淘汰该锚点，无视剩余 `ttl`
- 生产端 MUST 对任何已过期且已被淘汰的 `anchor_ref` 返回 `NCP-ANCHOR-NOT-FOUND`

若设置了 `valid_until`，生产端 **MUST** 在响应时间落在过期 60 秒内（即 `now ≥ valid_until − 60s`）的所有 CapsFrame 中携带 `inline_anchor`（包含后继 AnchorFrame）。这一约束界定了消费端仍在使用即将过期锚点的最坏窗口，确保后继锚点在当前锚点失效前抵达消费端。

#### 5.5.3 生产端 / 消费端义务

- **生产端 MUST NOT** 在前一个 AnchorFrame 的 `ttl` 窗口内引入破坏性变更（`additive_required`、`type_narrowing`、`field_remove` 或未缓解的 `semantic_change`）。后继锚点的 `valid_from` MUST ≥ 前序锚点的 `valid_until`（如已设置），否则 MUST ≥ `prior_publish_time + prior.ttl`。这保证缓存了前序锚点的消费端不会在窗口中段观测到破坏性切换。
- **生产端 MUST** 在每个替代旧版的 AnchorFrame 上设置 `supersedes`，无论兼容性分类如何
- **消费端 MUST** 容忍 `additive_optional` 变更——即引用已知锚点的负载中出现未知额外字段时，即使消费端尚未刷新锚点，也 MUST NOT 因此失败
- **消费端 SHOULD** 在锚点及其后继（通过 `supersedes` 关联）同时存在于缓存中时优先使用更新的 AnchorFrame，并 SHOULD 主动淘汰被替代项
- **消费端 MUST** 在每次引用时校验时序窗口：如 `valid_until` 已过，MUST 在发起新的引用请求前刷新

---

## 6. 状态码与错误码

NPS 采用两级错误体系：

1. **NPS 状态码**：传输层状态分类，见 [status-codes.cn.md](status-codes.cn.md)
2. **协议错误码**：具体错误标识，前缀标识所属协议

### NCP 错误码

| 错误码 | NPS 状态码 | 描述 |
|--------|-----------|------|
| `NCP-ANCHOR-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | anchor_ref 引用的 Schema 不存在 |
| `NCP-ANCHOR-SCHEMA-INVALID` | `NPS-CLIENT-BAD-FRAME` | AnchorFrame 中 Schema 格式不合法 |
| `NCP-ANCHOR-ID-MISMATCH` | `NPS-CLIENT-CONFLICT` | 相同 anchor_id 收到不同 Schema（锚点污染攻击防御）|
| `NCP-FRAME-UNKNOWN-TYPE` | `NPS-CLIENT-BAD-FRAME` | 未知帧类型码 |
| `NCP-FRAME-PAYLOAD-TOO-LARGE` | `NPS-LIMIT-PAYLOAD` | Payload 超过协商的 max_frame_payload |
| `NCP-FRAME-FLAGS-INVALID` | `NPS-CLIENT-BAD-FRAME` | Flags 字段中保留位非零 |
| `NCP-BINARY-VECTOR-MALFORMED` | `NPS-CLIENT-BAD-FRAME` | Tier-3 BinaryVector Payload 格式不合法 |
| `NCP-BINARY-VECTOR-DIM-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | BinaryVector marker 的维度与向量段不匹配 |
| `NCP-BINARY-VECTOR-INDEX-INVALID` | `NPS-CLIENT-BAD-FRAME` | BinaryVector marker 引用了不存在的向量段 |
| `NCP-BINARY-VECTOR-DTYPE-UNSUPPORTED` | `NPS-CLIENT-BAD-FRAME` | BinaryVector marker 使用了不支持的 dtype |
| `NCP-BINARY-VECTOR-TRUNCATED` | `NPS-CLIENT-BAD-FRAME` | BinaryVector 向量段被截断 |
| `NCP-STREAM-SEQ-GAP` | `NPS-STREAM-SEQ-GAP` | StreamFrame 序号不连续 |
| `NCP-STREAM-NOT-FOUND` | `NPS-STREAM-NOT-FOUND` | stream_id 引用的流不存在 |
| `NCP-STREAM-LIMIT-EXCEEDED` | `NPS-STREAM-LIMIT` | 超出单连接最大并发流数 |
| `NCP-ENCODING-UNSUPPORTED` | `NPS-SERVER-ENCODING-UNSUPPORTED` | 请求的编码 Tier 不被支持 |
| `NCP-ANCHOR-STALE` | `NPS-CLIENT-CONFLICT` | anchor_ref 存在但 Schema 已更新（配合 inline_anchor 使用）|
| `NCP-DIFF-FORMAT-UNSUPPORTED` | `NPS-CLIENT-BAD-FRAME` | DiffFrame 使用了接收方不支持的 patch_format |
| `NCP-VERSION-INCOMPATIBLE` | `NPS-PROTO-VERSION-INCOMPATIBLE` | 客户端 min_version 高于 Server 支持版本 |
| `NCP-STREAM-WINDOW-OVERFLOW` | `NPS-STREAM-LIMIT` | 发送方在窗口耗尽后继续发送 |
| `NCP-PREAMBLE-INVALID` | `NPS-PROTO-PREAMBLE-INVALID` | 原生模式连接首 8 字节非 `b"NPS/1.0\n"`；服务端 500 ms 内静默关闭（不发 ErrorFrame）—— 见 §2.6.1 |
| `NCP-REKEY-REQUIRED` | `NPS-PROTO-VERSION-INCOMPATIBLE` | E2E 加密信道已达到 rekey 阈值（2^32 帧或 24 小时）；对端 MUST 在继续发送加密帧之前发起密钥轮换 |
| `NCP-KEEPALIVE-TIMEOUT` | `NPS-SERVER-TIMEOUT` | 在 3 × `ping_interval_ms` 内未收到任何帧（含 NopFrame）；连接将被关闭 |
| `NCP-EARLY-DATA-REJECTED` | `NPS-PROTO-VERSION-INCOMPATIBLE` | NPS 帧出现在 QUIC/TLS 0-RTT early data 中；NPS 应用数据只能在握手确认后开始 |

HTTP 模式下的状态码映射见 [status-codes.cn.md](status-codes.cn.md)。

---

## 7. 安全考量

### 7.1 重放攻击防御
CapsFrame 和 QueryFrame 应在 TLS 层由传输协议防御重放。若在非 TLS 场景（开发模式）下，Agent SHOULD 在请求中携带 `nonce` 字段。

### 7.2 锚点污染（Anchor Poisoning）
Schema 由 Node 发布，Agent 只读引用。Node MUST 对 anchor_id 做幂等校验——相同 anchor_id 对应的 Schema MUST 始终一致。Agent 若检测到同一 anchor_id 返回了不同 Schema，SHOULD 断开连接并报告安全事件。

### 7.3 流式洪泛（Stream Flooding）
Node SHOULD 限制单连接最大并发流数（推荐默认值：32，通过 CapsFrame `max_concurrent_streams` 协商）。超出限制时返回 `NCP-STREAM-LIMIT-EXCEEDED`。

### 7.4 E2E 加密（ENC 标志正式定义）

> **密钥轮换（Rekeying，NCP v0.7）**：使用 ENC=1 时，发送方 MUST 在触及以下任一阈值之前发起 rekey：(a) 当前密钥已发送 2^32 帧，(b) 密钥建立至今已满 24 小时。触发方发送 `error: "NCP-REKEY-REQUIRED"` 的 ErrorFrame 作为优雅信号；对端 MUST 在继续发送加密帧之前完成密钥轮换以作确认。密钥轮换沿用与初始密钥派生相同的 ECDH + HKDF 算法，实现 SHOULD 使用 X25519。

当帧头 Flags **ENC=1** 时，帧 Payload 使用应用层端到端加密。E2E 加密独立于 TLS 传输层，适用于多跳 Relay 场景中 Relay 不可信的情况。

**算法选择**

通过握手 CapsFrame 的 `e2e_enc_algorithms` 字段协商，优先级从高到低：

| 算法 | 标识符 | 密钥长度 | Nonce | Tag |
|------|--------|----------|-------|-----|
| AES-256-GCM | `aes-256-gcm` | 256-bit | 12 bytes | 16 bytes |
| ChaCha20-Poly1305 | `chacha20-poly1305` | 256-bit | 12 bytes | 16 bytes |

**ENC=1 时 Payload 布局**

```
┌────────────────┬──────────────────────────────────────┬─────────────┐
│   Nonce        │   Encrypted Payload                  │  Auth Tag   │
│  (12 bytes)    │   (原始 Payload 密文)                 │  (16 bytes) │
└────────────────┴──────────────────────────────────────┴─────────────┘
```

- `Payload Length`（帧头中）= 12 + len(encrypted) + 16
- Nonce MUST 对每个帧唯一，推荐使用随机生成（CSPRNG）
- AAD（Additional Authenticated Data）= Frame Header（4 或 8 字节）
- 密钥管理：E2E 密钥通过 NIP（Neural Identity Protocol）的密钥交换机制分发，不在 NCP 层定义

**实现要求**

- 若 HelloFrame 中 `e2e_enc_algorithms` 为空，表示不支持 E2E 加密，两端 MUST NOT 设置 ENC=1
- 若 ENC=1 但会话未协商 E2E 算法，接收方 MUST 返回 `NCP-ENC-NOT-NEGOTIATED` 并丢弃该帧
- 开发/调试模式下（仅限非生产环境），ENC 可为 0，但实现 SHOULD 在日志中警告

**新增错误码**

| 错误码 | NPS 状态码 | 描述 |
|--------|-----------|------|
| `NCP-ENC-NOT-NEGOTIATED` | `NPS-CLIENT-BAD-FRAME` | ENC=1 但会话未协商 E2E 加密算法 |
| `NCP-ENC-AUTH-FAILED` | `NPS-CLIENT-BAD-FRAME` | E2E 加密 Auth Tag 验证失败（可能被篡改）|

### 7.5 原生模式 TLS 绑定与双向认证

原生模式的传输安全层由 **[NPS-RFC-0006](rfcs/NPS-RFC-0006-ncp-native-transport.md) §6**（Proposed）规范性定义。概括而言，非 `local-dev` 的原生模式连接 MUST：

- 在 **TLS 封装**的传输（TCP）或 QUIC 内建的 TLS 1.3 之上协商套件级 ALPN token **`nps/1.0`**；禁止 STARTTLS 式的带内升级。
- 使用基于 NIP 签发证书（RFC-0002 X.509 NID profile）的 **mutual TLS**。服务端把客户端证书中的 NID 绑定到该 NCP 会话，并 MUST 以 `NCP-NID-MISMATCH` 拒绝证书 NID 与 `IdentFrame` NID 不一致的情况。
- 可选地为重复的短连接提供 TLS 1.3 **会话恢复票据（session-resumption ticket）**，其有效期受证书有效期与票据寿命（≤ 24 小时）双重约束；恢复只省去 TLS 握手，绝不跳过 RFC-0001 前导或 HelloFrame。

该绑定即传输层准入闸门，由 `nps-ingress`（L2）守护进程终结。

### 7.6 Keepalive 与对端失活检测（NCP v0.8）

长连接形态的原生模式连接（TCP/QUIC）可能被 NAT 设备或防火墙静默切断。NopFrame（§4.8）提供应用层 keepalive：

- 若双方在各自的 HelloFrame/CapsFrame 中都声明了 `ping_interval_ms > 0`，则在没有其他流量时，双方 SHOULD 按大致该间隔发送 NopFrame
- 可接受的最小间隔为 **1000 ms**（1 秒）；更小的取值 MUST 按 1000 ms 处理
- 在 `3 × ping_interval_ms` 内未收到任何类型帧的一端，MUST 发送 `NCP-KEEPALIVE-TIMEOUT` 并在 500 ms 内关闭连接
- 若仅有一方声明了 `ping_interval_ms`，另一方 SHOULD 采用该声明值作为共同间隔
- TCP keepalive（`SO_KEEPALIVE`）与 QUIC idle timeout 属互补机制；实现 SHOULD 在应用层 NopFrame 协议之外，同时在传输层配置这两者

### 7.7 alpha.19 连接加固 Profile（NCP v0.12）

以下要求 ID 冻结
[`runtime_hardening_vectors.json`](conformance/ncp/runtime_hardening_vectors.json)
所验证的可移植 runtime 行为：

1. **NCP-P19-01 —— 有效间隔。** 令 `C`、`S` 为 client 与 server 提供的非零
   `ping_interval_ms`。两者都为零时禁用应用层 keepalive；否则先忽略零值，再取
   `max(1000, min(C, S))`。
2. **NCP-P19-02 —— 接收时钟。** 只有完整且合法的入站 NCP 帧被接纳时才重置
   dead-peer 时钟。本地发送、残缺 header、畸形帧和仅传输层活动均不得重置。
3. **NCP-P19-03 —— 空闲探针。** 一个有效间隔内未发送应用帧时，一端 MUST
   恰好排入一个无 payload 的 NopFrame。普通出站流量重置 idle-send 时钟并取代探针。
4. **NCP-P19-04 —— 确定性关闭。** 连续 `3 × effective_interval` 未收到合法入站帧时，
   连接原子进入 `closing`，最多发送一个 `NCP-KEEPALIVE-TIMEOUT`，取消所有 stream，
   并在 500 ms 内关闭 carrier。timeout error 之后不得再发送应用帧。
5. **NCP-P19-05 —— Nop 形态。** payload 长度非零的 NopFrame 以
   `NCP-FRAME-PAYLOAD-TOO-LARGE` 拒绝，且不得重置任一时钟。
6. **NCP-P19-06 —— 禁止 early application data。** NPS preamble、HelloFrame
   和其他 NPS 帧 MUST NOT 作为 QUIC/TLS 0-RTT 数据发送或接收。服务端检测到时以
   `NCP-EARLY-DATA-REJECTED` 拒绝；握手确认后允许重试。
7. **NCP-P19-07 —— QUIC migration 身份。** 只有握手确认后才允许 QUIC path
   migration；迁移保留既有 NCP session，不得改变证书/IdentFrame NID 绑定。
   绑定变化以 `NCP-NID-MISMATCH` 拒绝并关闭连接。
8. **NCP-P19-08 —— 双层背压。** QUIC/TCP carrier credit 与 NCP StreamFrame
   应用窗口同时生效。carrier credit 不得授权发送方越过已耗尽的 NCP 窗口。
   NopFrame 不计入 stream 窗口，但仍受 carrier 可写性约束。

---

### 7.8 生产运行时 Profile（NCP v0.13）

选择 experimental 的 `nps.production_runtime` revision 1 后，以下要求必须执行。
未选择时保持基础行为；不新增帧类型或心跳机制。

#### 7.8.1 公共职责

六 SDK 以各自语言类型提供 SessionFactory（连接/协商/准入）、SessionPool（完整安全
key 和有界等待）、InvocationDispatcher（先注册关联再写入、单次本地终态）、
RetryDecision（依据派发确定性/操作契约/期限的纯决策）和 DrainController。
准入权限不能任意修改。保留底层 stream API，但绕过托管 dispatcher 时必须明确不具有
本 profile 保证。

#### 7.8.2 状态与结果

Session：connecting -> negotiating -> admitting -> active -> draining -> closed。
建立失败释放资源；closed 不能回池。重连新建 session 并重新 NIP 准入。

Invocation：queued -> writing -> awaiting -> completed；
另有 rejected-before-dispatch、cancelled-before-dispatch、expired-before-dispatch、
indeterminate 终态。首次写入尝试中/之后的传输失败默认不确定，除非认证服务端明确证明
未派发；本地/TCP 已写入不能证明业务执行。有效关联远程终态才形成已知结果。
相同重复终态忽略，冲突终态关闭 session，不能再次调用应用完成回调。

Session 内禁止复用关联 ID。终态 tombstone 保留到 session 关闭；达到容量则 drain/
重连，不能驱逐后复用。未知关联 ID 关闭。取消后的迟到终态只能补诊断证据，不能改写
已返回的本地结果。

#### 7.8.3 资源上限

默认连接/握手/准入各 5 秒，请求总期限 30 秒（含排队、写入、等待、重试），drain
30 秒。每完整 pool key 最多 8 个 session，全局 64 个、待 acquire 最多 256。
每 session 最多 32 个排队帧或 1 MiB payload，任一先到即限制；tombstone 默认 4096。
可配置其他有限正值，但不能超过协商的 payload/concurrency 上限。所有等待服从剩余
期限。心跳/idle 仍用 NCP §7.6/7.7，不新增矛盾协商。队列满在派发前拒绝。
流消费缓慢时使用现有 flow control；超协商窗口的 peer 使 session 失败。

Pool key 包含 endpoint 身份、subject、credential generation、assurance/scope 策略
指纹、features、encoding/security profile。不同租户/委托范围不能因 host/port 相同
而复用。撤销/轮换使相应池失效；全局限制同时约束 key 数。

#### 7.8.4 重试

写入前确认未派发可在期限和预算内重试；服务端明确未派发的临时拒绝还需操作允许。
未知结果的只读操作仅在声明 replay-safe 且符合 freshness/cost 语义时重试。
未知 mutation 没有持久化去重证明则绝不自动重试；有证明时必须相同 principal/key/
digest、在保留期和许可目标范围内。已知远程终态不自动重放。

默认总共 2 次尝试。第 retry_index 次重试延迟均匀取
0..min(1000 ms,100 ms * 2^(retry_index-1))；注入时钟/随机源以测试。
延迟和服务端 hint 都消耗原总期限，超过剩余预算则终止。
重连成功不能证明前次未执行。跨 owner 重试必须共享持久化去重范围，否则 indeterminate。

#### 7.8.5 取消与 drain

首次写入前取消阻止派发；之后只停止本地等待，并传播已协商的操作/NOP 取消机制，
不发明新 frame。无远程回滚/终态证据则结果仍不确定，不承诺副作用撤销。
Drain 拒绝新 acquire/操作，有界等待并请求受支持取消，最后关闭，
未决 mutation 报 indeterminate。取消、超时、close 竞争仍只交付一次本地完成。

#### 7.8.6 故障转移与观测

断线重新解析 active Anchor，执行单调 cluster_epoch fencing，再协商和身份准入。
清理旧节点 route/capability 缓存；旧 owner 或 resumption ticket 不构成授权。
记录连接、协商、排队、尝试、重试、重连、取消、drain、failover 的计数/耗时、
结果确定性和有界关联 ID。禁止凭据/投影/body 日志，以及 subject/scope/key 的高基数标签。

#### 7.8.7 错误

queue-full、deadline-before-dispatch、cancelled-before-dispatch、indeterminate、
correlation-conflict、session-closed 为 typed API 结果，不自动分配新 wire 错误。
保留原协议错误；操作确需新 wire 错误时另做 registry 变更。

操作取消绑定见 NWP §7.3 和 NOP §3.6/§5.2。没有持久化去重的明确保留期、
主体/key/digest 绑定和跨 owner 范围保证时，不得将操作标为可安全自动重放。
产品状态名或成功连接不构成这些保证。

---

### 7.9 实验schema批次候选契约

可选[schema批次修订1](profiles/schema-batches.cn.md)及
[CR-0017](cr/NPS-CR-0017-schema-batches.md)在既有帧上明确有序行、完整流基线和绑定
epoch/schema/基线版本的差量。须精确选择`nps.schema_batches=1`且本地显式启用实验修订；
整套版本、锚点或registry条目均不是准入或来源权限。未选择操作保留基础行为。当前.NET
参考仅限明确验收的mTLS子集；候选注册不证明六SDK、NWP实时结果流或效率/计费合格。

---

## 8. 编码层级

所有帧类型支持以下编码层级，通过帧头 Flags T0/T1 位标识：

| Tier | 格式 | Flag | 适用场景 |
|------|------|------|---------|
| Tier-1 | JSON | `00` | 开发调试、兼容模式 |
| Tier-2 | MsgPack（Binary） | `01` | 生产环境，~60% 体积压缩 |
| Tier-3 | BinaryVector v1 | `10` | 向量密集型帧；MessagePack 元数据 + little-endian float32 原始向量段 |
| — | Reserved | `11` | 保留 |

默认值是策略级默认：生产环境使用 Tier-2，开发/兼容场景使用 Tier-1。实现 SHOULD 在握手或配置阶段选择稳定的会话编码策略，并持续一致地应用；只有当策略明确允许某类帧或某个阶段使用不同 Tier 时，该帧 MAY 使用不同 Tier。

Tier-3 是可选能力。除非会话已协商 `binary_vector.v1`，发送方 MUST NOT 发出 `Flags.T1T0 = 10`；不支持或未协商 Tier-3 的接收方 MUST 以 `NCP-ENCODING-UNSUPPORTED` 拒绝该帧。`Flags.T1T0 = 11` 仍为保留值，MUST 以 `NCP-FRAME-FLAGS-INVALID` 拒绝。

### 8.1 Tier-3 BinaryVector v1 Payload

Tier-3 BinaryVector v1 是帧 Payload 的编码格式，不替代 NWP 语义。帧类型字节仍决定逻辑帧类型；Tier-3 Payload 只负责把该帧的元数据与稠密向量段编码到线上格式。

协商 token：`binary_vector.v1`。

二进制布局：

| Offset | Size | 字段 | 编码 |
|--------|------|------|------|
| 0 | 4 | Magic | ASCII `NPBV` |
| 4 | 1 | Version | `0x01` |
| 5 | 1 | Flags | v1 中固定 `0x00`；接收方 MUST 拒绝非零值 |
| 6 | 2 | `vector_count` | uint16，大端 |
| 8 | 4 | `metadata_len` | uint32，大端 |
| 12 | 4 | Reserved | MUST 为 0 |
| 16 | `metadata_len` | Metadata | MessagePack map，字段名与 Tier-2 相同 |
| ... | variable | Vector segments | 重复的 `dim:uint32_be` + `dim` 个 little-endian float32 |

Metadata MUST 保留逻辑帧对象。被移入二进制段的向量字段用 marker 对象替换：

```json
{
  "$nps_binary_vector": 0,
  "dtype": "float32",
  "dim": 1536
}
```

NCP v0.9 的标准绑定是 NWP `QueryFrame.vector_search.vector`。marker index 从 0 开始，MUST 指向后续向量段之一。`dtype` MUST 为 `float32`；向量值为 IEEE-754 binary32 little-endian。MatrixTensor、float16、量化 int8、多向量 schema 绑定留给后续 CR。

---

## 9. 实现注意事项

- AnchorFrame 缓存上限建议 1000 条/连接，超出时 LRU 淘汰
- MsgPack（Tier-2）编解码推荐使用 MessagePack 官方库，不建议自行实现
- StreamFrame seq 溢出（uint32 到达 0xFFFFFFFF）时，新流 MUST 使用新 stream_id
- 扩展帧头模式（EXT=1）的支持 SHOULD 在 CapsFrame 能力协商中声明
- **anchor_id 计算**：必须使用 RFC 8785 JCS 标准库，禁止自行实现规范化逻辑（跨语言 SDK 一致性）
- **HelloFrame 超时**：Server 若 5 秒内未收到 HelloFrame，SHOULD 发送 ErrorFrame 并关闭连接
- **DiffFrame binary_bitset**：仅在双端通过 CapsFrame 协商支持后使用；Tier-1 JSON 模式下禁止使用
- **E2E 加密 Nonce**：每帧独立随机 Nonce（12 bytes, CSPRNG），禁止重用；密钥轮换频率 SHOULD 不低于每 2^32 帧一次
- **inline_anchor 处理**：Agent 收到 `inline_anchor` 后必须先验证 anchor_id（JCS+SHA-256），验证通过后再更新缓存
- **Tier-2 性能优化（.NET）**：`Tier2MsgPackCodec` 建议使用 MessagePack AOT 代码生成，避免高频帧解析的反射开销（实现建议，不影响协议互操作性）

---

## 10. 变更历史

| 版本 | 日期 | 变更 |
|------|------|------|
| 0.13 | 2026-09-09 | Alpha.20 可选 feature revision 协商：有界类型、完整握手 payload 上限、确定性选择、必需特性派发前拒绝和会话范围；不新增帧，不改变基础兼容下限。 另定义生产运行时职责、有界重试/排空及结果确定性，纠正 JCS Unicode 与握手 Caps 示例。 |
| 0.12 | 2026-08-31 | alpha.19 连接加固 Profile：确定性的有效 keepalive 间隔与收发时钟、单次 timeout-close 迁移、无 payload Nop 强制、拒绝 QUIC/TLS 0-RTT NPS 帧、保持身份绑定的 QUIC migration，以及 carrier/application 双层背压组合。新增共享 runtime hardening 向量与 `NCP-EARLY-DATA-REJECTED`。|
| 0.11 | 2026-07-29 | 新增 §2.6.2 原生服务端互操作 Profile：认证先于前导的顺序要求、前导与 Hello 分别有界读取、分配安全的 Hello 上限、准入前失败的静默行为、确定性的版本/编码/协议/上限协商、可移植的握手 Caps 字段，以及共享的 `native_server_handshake_vectors.json`。属增量变更；无新增帧类型与错误码。 |
| 0.10 | 2026-07-05 | 原生模式传输正式成为规范性内容（NPS-RFC-0006 **Accepted**）。新增原生模式下**跨 Anchor 故障转移的会话连续性**（NPS-CR-0009）：多 Anchor 所有权转移后，若连接丢失或恢复尝试返回 `NCP-NID-MISMATCH`，客户端经 NDP §9（取最高 `cluster_epoch`）重新解析，或使用 NWP `anchor_failover` 的 `successor_nid`，并重新建立会话。无新增帧与错误码（复用 `NCP-NID-MISMATCH`）。 |
| 0.9 | 2026-06-27 | 激活 Tier-3 BinaryVector v1（`Flags.T1T0 = 10`），协商 token 为 `binary_vector.v1`；定义 `NPBV` Payload 布局、MessagePack metadata marker、float32 little-endian 向量段，以及 NWP `QueryFrame.vector_search.vector` 绑定；`0b11` 仍为保留值。 |
| 0.8 | 2026-06-12 | 新增 §7.5 原生模式 TLS 绑定与双向认证，归纳 **NPS-RFC-0006 §6**（草案→提议）：套件级 ALPN `nps/1.0`（取代临时值 `ncp/1`）、原生模式 over TCP 强制 TLS 封装、与 NIP 证书的 mTLS + session-NID 绑定、TLS 1.3 会话恢复票据；新增错误码 `NCP-NID-MISMATCH`。gate `nps-ingress`（L2）守护进程。 |
| 0.8 | 2026-06-03 | **NopFrame (0x07)** keepalive/心跳帧：空 Payload、双向；`HelloFrame.ping_interval_ms` 声明期望间隔（0 = 禁用，默认）；在 3 × 间隔内未收到任何帧时返回 `NCP-KEEPALIVE-TIMEOUT` 错误码；§7.6 对端失活检测规则。 |
| 0.7 | 2026-05-31 | 通过 HelloFrame/CapsFrame 协商 `max_concurrent_streams`（uint32，默认 32）；超限时返回 `NCP-STREAM-LIMIT-EXCEEDED`；拒绝进行中的流时 MUST 发送 mid-stream ErrorFrame（此前为 MAY）；E2E rekey 协议（§7.4）：2^32 帧或 24 小时触发，以 `NCP-REKEY-REQUIRED` 作为信号；QUIC 流映射：每个 NCP 信道一条双向流，HelloFrame 走 stream 0。详见 [NPS-RFC-0006](rfcs/NPS-RFC-0006-ncp-native-transport.md)。 |
| 0.6 | 2026-04-25 | 新增 §2.6.1 原生模式连接前导（8 字节常量 `b"NPS/1.0\n"`）；在 `frame-registry.yaml` 中保留帧类型字节 0x4E；新增错误码 `NCP-PREAMBLE-INVALID` 和状态码 `NPS-PROTO-PREAMBLE-INVALID`。详见 [NPS-RFC-0001](rfcs/NPS-RFC-0001-ncp-connection-preamble.cn.md)。 |
| 0.4 | 2026-04-14 | 新增 IdentFrame (0x06) 握手帧；新增 §2.6 连接握手序列及版本协商规则；anchor_id 计算明确引用 RFC 8785 JCS；DiffFrame 新增 patch_format 字段（json_patch / binary_bitset）；CapsFrame 新增 inline_anchor；StreamFrame 流量控制语义正式化（window_size 协议）；§7.4 E2E 加密节（ENC 标志、AES-256-GCM / ChaCha20-Poly1305、Payload 布局）；§5.4 auto-anchor 协议（NCP-ANCHOR-STALE + inline_anchor）；新增错误码 NCP-ANCHOR-STALE、NCP-DIFF-FORMAT-UNSUPPORTED、NCP-VERSION-INCOMPATIBLE、NCP-STREAM-WINDOW-OVERFLOW、NCP-ENC-NOT-NEGOTIATED、NCP-ENC-AUTH-FAILED |
| 0.3 | 2026-04-12 | 传输双模（HTTP/原生）；统一端口 17433；可配置帧大小（EXT 位）；ErrorFrame (0xFE)；NPS 状态码体系；Tier-3 标记 Reserved；AnchorFrame 所有权明确为 Node 发布；Token 估算改用 CGN |
| 0.2 | 2026-04-10 | AnchorFrame/DiffFrame/StreamFrame/CapsFrame/AlignFrame 定义；Flags 字段逐位定义；AlignFrame 标记 Deprecated |
| 0.1 | 2026-03-01 | 初始帧格式与编码层级定义 |

---

*归属：LabAcacia / INNO LOTUS PTY LTD · Apache 2.0*
