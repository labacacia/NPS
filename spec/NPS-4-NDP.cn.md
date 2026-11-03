[English Version](./NPS-4-NDP.md) | 中文版

# NPS-4: Neural Discovery Protocol (NDP)

**Spec Number**: NPS-4
**Status**: Proposed
**Version**: 0.14
**Date**: 2026-09-09
**Port**: 17433（默认，共用）/ 17436（可选独立）
**Authors**: Ori Lynn / INNO LOTUS PTY LTD
**Depends-On**: NPS-1 (NCP v0.13)、NPS-3 (NIP v0.16)

---

## 1. Terminology

本文档中的关键字 "MUST"、"SHOULD"、"MAY" 按照 RFC 2119 解释。

---

## 2. 协议概述

NDP 是 NPS 的发现与注册协议。Agent、Node 与 Registry 通过 NDP 发布带签名的 presence 记录、把 `nwp://` 目标解析到仍然存活的物理端点、交换有界拓扑快照，并执行 liveness 与 Registry 安全 profile 策略。DNS TXT 可以承载 NDP 发现提示，但 NDP 不是 DNS 的替代品：它把 NPS 身份、能力、角色、端点、新鲜度和信任策略绑定进一条签名发现记录。

### 2.1 解析模式

| 模式 | 适用场景 | 优先级 |
|------|---------|--------|
| 本地注册表（UDP Multicast）| 内网/局域网 | 最高 |
| DNS TXT 记录 | 公网，分布式 | 中 |
| NPS Cloud Registry | 全局，中心化 | 最低（兜底）|

---

## 3. 帧类型

### 3.1 AnnounceFrame (0x30)

节点或 Agent 广播自身存在与能力。

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `frame` | uint8 | 必填 | 固定值 `0x30` |
| `nid` | string | 必填 | 发布者 NID |
| `node_type` | string | 条件必填 | 节点类型（node 实体必填）|
| `addresses` | array | 必填 | 物理地址列表，每项含 `host`/`port`/`protocol` |
| `capabilities` | array | 必填 | 能力列表（复用 NIP capabilities 定义）|
| `ttl` | uint32 | 必填 | 广播有效期（秒），0 = 下线通知 |
| `timestamp` | string | 必填 | 广播时间（ISO 8601 UTC）|
| `graph_seq` | uint64 | 条件必填 | per-NID 单调递增的配置序号。NDP 0.12 发布者 MUST 携带。出于兼容，`local-dev` Registry MAY 把旧发布者缺失的该值按 `0` 处理；`org-private` 与 `public-federated` Registry MUST 以 `NDP-ANNOUNCE-PROFILE-VIOLATION` 拒绝缺失值。|
| `activation_mode` | string | 条件必填 | 取值 `ephemeral` / `resident` / `hybrid` 之一。声明符合 NPS-Node Profile L1+ 的发布者 MUST 携带；未合规发布者可不带。接收方 MUST 把缺失字段按 `ephemeral` 处理（与 NPS v1.0-alpha.2 发布者保持向后兼容）。见 §3.1.1。 |
| `node_roles` | string 数组 | 可选 | 该发布者承担的节点功能角色列表。每个值取自 `"memory"`、`"action"`、`"complex"`、`"anchor"`、`"bridge"`。遗留值 `"gateway"` 在 v1.0-alpha.3 移除（NPS-CR-0001），解析器 MUST 以 `NDP-ANNOUNCE-ROLE-REMOVED` 拒绝；其他无法识别的值 MUST 以 `NDP-ANNOUNCE-ROLE-UNKNOWN` 拒绝。单角色节点发送一元素数组（`"node_roles": ["memory"]`）。缺省表示"按 `node_type` 字段单角色"，接收方 SHOULD 回退到 `node_type`。解析器 MUST 在 alpha 过渡期内把遗留字段名 `node_kind` 当作 `node_roles` 的别名接受。（NPS-CR-0001；在 NDP v0.8 由 `node_kind` 更名而来——见 M1 命名消歧修复）|
| `cluster_anchor` | string (NID) | 可选 | 加入集群的非-Anchor 节点，标识它注册到的 Anchor Node 的 NID。独立节点和 Anchor Node 自己 MUST 不带。（NPS-CR-0001）|
| `bridge_protocols` | string 数组 | 可选 | 在 `node_roles` 中声明 `"bridge"` 的节点列出该节点**出向**（NPS → 外部）翻译的外部协议。标准值 `"http"` / `"grpc"` / `"mcp"` / `"a2a"`。开放枚举；第三方适配器 MAY 通过未来 CR 注册更多值。未声明 `"bridge"` 的节点 MUST 不带。语义未被 NPS-CR-0010 改变。（NPS-CR-0001）|
| `bridge_inbound_protocols` | string 数组 | 可选 | 在 `node_roles` 中声明 `"bridge"` 的节点列出该节点**入向服务**（外部 → NPS）的外部协议 —— 即它为哪些协议暴露原生服务端端点，使外部客户端无需任何 NPS 知识即可调用。取值域与 `bridge_protocols` 相同。缺省或为空 ⇒ 该节点不暴露入向面（即纯出向 Bridge Node，也是 alpha.15 之前唯一存在的形态）。同一协议 MAY 同时出现在两个数组中。声明 `"bridge"` 的节点 MUST 保证 `bridge_protocols` / `bridge_inbound_protocols` 至少一个非空。未声明 `"bridge"` 的节点 MUST 不带。（NPS-CR-0010）|
| `activation_endpoint` | object | 条件必填 | `resident` / `hybrid` 模式的推送目标，结构同 `addresses[]` 每项。`activation_mode ∈ {resident, hybrid}` 时 MUST 存在；其他模式下 MUST 不出现。 |
| `heartbeat_interval_ms` | uint32 | 可选 | 发布方重新发送 AnnounceFrame 的心跳间隔（毫秒）；默认 60000。接收方 SHOULD 在 3× 该间隔内未收到重新公告时把节点视为离线。 |
| `spawn_spec_ref` | string | 可选 | 发布方 daemon 用于按需具现化 Agent 进程的不透明引用。对 `ephemeral` 以及 `hybrid` 冷启动有意义。解析后的 SpawnSpec schema 见 §3.1.2；解析规则（内联 `spawnspec:` base64url-JSON data URI 或 `https://` / `nwp://` URL）由 [NPS-CR-0007](cr/NPS-CR-0007-nop-l3-runtime-integration.md) §5 标准化。 |
| `health` | string | 可选 | 发布方 liveness 自报告（NDP v0.9）。取值 `"healthy"` / `"degraded"` / `"draining"`。缺省时接收方按 `"healthy"` 处理。`"draining"` 表示节点正在下线，SHOULD NOT 再接收新流量。 |
| `last_seen` | string | 可选 | 发布方最近一次 liveness beat 的 ISO 8601 UTC 时间戳（NDP v0.9）。存在时 Registry 用 `last_seen + ttl`（而不是 `timestamp + ttl`）作为解析期新鲜度截止（§3.2）。缺省时回退到 `timestamp`。 |
| `cluster_epoch` | uint64 | 可选 | 多 Anchor 集群所有权栅栏（NPS-CR-0009）。本 Anchor 持有其 `cluster_anchor` 集群所有权时所处的 epoch；从 1 开始，每次所有权转移严格递增。缺省 ⇒ 按 1 处理（单 Anchor）。Registry 把某个 `cluster_anchor` NID 解析为 `cluster_epoch` 最高的那条记录（§9）。 |
| `signature` | string | 必填 | IdentFrame 私钥签名，防止伪造 |

**示例（L1+ 发布者，ephemeral）**

```json
{
  "frame": "0x30",
  "nid": "urn:nps:node:api.example.com:products",
  "node_type": "memory",
  "addresses": [
    { "host": "10.0.0.5", "port": 17434, "protocol": "nwp" }
  ],
  "capabilities": ["nwp:query", "nwp:stream"],
  "ttl": 300,
  "timestamp": "2026-04-10T00:00:00Z",
  "activation_mode": "ephemeral",
  "signature": "ed25519:..."
}
```

**示例（L2+ 发布者，resident）**

```json
{
  "frame": "0x30",
  "nid": "urn:nps:agent:labacacia:writer-42",
  "addresses": [
    { "host": "10.0.0.5", "port": 17434, "protocol": "nwp" }
  ],
  "capabilities": ["nwp:invoke"],
  "ttl": 300,
  "timestamp": "2026-04-24T00:00:00Z",
  "activation_mode": "resident",
  "activation_endpoint": { "host": "10.0.0.5", "port": 17440, "protocol": "nwp" },
  "signature": "ed25519:..."
}
```

#### 3.1.1 激活语义

`activation_mode` 告诉接收方发布者期望哪种方向的通信：推送（push）还是拉取（pull）：

| 模式 | 发送方行为 | 接收方预期 |
|------|----------|----------|
| `ephemeral` | 走发布方的 per-NID inbox + pull 交付；帧到达时发布方进程可能不在运行。 | 帧进入发布方 inbox，等待发布方拉取。 |
| `resident` | 通过长连接向 `activation_endpoint` 推送。 | 发布方在声明的端点上接收推送帧。 |
| `hybrid` | 先尝试推送到 `activation_endpoint`；若在发布方 wake budget 内不可达，则回退到 inbox。 | 发布方收到第一帧时从 hibernate 中唤醒；唤醒后推送目标可用。 |

**向后兼容**：NPS v1.0-alpha.2 发布者不会发出 `activation_mode`。接收方 MUST 把缺失字段按 `ephemeral` 处理，确保 alpha.2 AnnounceFrame 仍能正确解析。接收方 MUST NOT 因为缺少该字段而拒绝一个 AnnounceFrame。

**合规参考**：合规框架及每一级的字段要求，见 [NPS-Node Profile §1.3 / §6](./services/NPS-Node-Profile.cn.md)。

#### 3.1.2 SpawnSpec Schema（`spawn_spec_ref` 的解析结果）

`spawn_spec_ref`（§3.1）解析得到的 **SpawnSpec** 描述如何按需实例化一个 ephemeral Agent 进程（NDP v0.9，Profile L3）。解析规则（data URI vs URL）由 [NPS-CR-0007](cr/NPS-CR-0007-nop-l3-runtime-integration.md) §5 标准化。

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `oci_image` | string | 必填 | OCI 容器镜像引用，例如 `"registry.example.com/my-agent:v1.2"` |
| `command` | array[string] | 可选 | 命令覆盖（等价于 Docker `CMD`）|
| `resource_limits` | object | 可选 | 资源约束 |

**resource_limits 对象**

| 字段 | 类型 | 描述 |
|------|------|------|
| `cpu_millicores` | uint32 | CPU 上限，单位 millicore（例如 `500` = 0.5 vCPU）|
| `memory_mb` | uint32 | 内存上限，单位 MiB |

该字段只对参与 Profile L3 的 spawn-capable Registry / runner 有意义。L1/L2 节点 MAY 携带该字段；L1/L2 接收方 SHOULD 忽略它。

---

### 3.2 ResolveFrame (0x31)

解析 `nwp://` URL 到物理端点。

**请求**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `frame` | uint8 | 必填 | 固定值 `0x31` |
| `target` | string | 必填 | 待解析的 `nwp://` URL |
| `requester_nid` | string | 可选 | 请求方 NID（用于授权检查）|

**响应**

```json
{
  "target": "nwp://api.example.com/products",
  "resolved": {
    "host": "10.0.0.5",
    "port": 17434,
    "cert_fingerprint": "sha256:a3f9...",
    "ttl": 300,
    "health": "healthy"
  }
}
```

#### 3.2.1 解析期陈旧检查（NDP v0.9）

Registry MUST 以 `(last_seen ?? timestamp) + ttl` 计算每条注册记录的新鲜度截止。当 `ResolveFrame` 将返回的条目已经超过该截止时间，Registry MUST 返回 `NDP-RESOLVE-STALE`，而不是继续提供陈旧端点。陈旧端点很可能把流量路由到已经离线的节点。Registry MAY 主动清理过期条目；在清理之前，解析期陈旧检查是权威保护。`resolved` 对象 SHOULD 回显条目的 `health`，使调用方即便在 TTL 未过期时也能避开 `draining` 节点。

#### 3.2.2 Bridge 能力发现（NDP v0.12）

针对某个 Bridge 协议的 Registry 发现查询，MUST 只考虑在 `node_roles` 中声明 `"bridge"`（或 `node_type` 回退值为 `"bridge"`）、且 `health` 不为 `"draining"` 的存活条目。**出向**（outbound）查询匹配 `bridge_protocols`；**入向**（inbound）查询匹配 `bridge_inbound_protocols`。Registry MUST NOT 由出向声明推断入向支持，反之亦然。

可移植的发现结果是按 ordinal 升序排序的完整候选 NID 集合。NDP 不挑选某一个候选，也不定义负载均衡；该决策属于 NWP Bridge 客户端，由其在应用端点、信任与运行时策略之后作出。

---

### 3.3 GraphFrame (0x32) — 拓扑快照

节点图谱快照与联邦格式。GraphFrame 携带完整或局部拓扑快照，包含显式的节点列表和边列表（NDP v0.8；替代此前的 `initial_sync` / `patch` / `seq` 格式）。

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `frame` | uint8 | 必填 | 固定值 `0x32` |
| `graph_id` | string | 必填 | 该图谱快照的不透明标识（UUID v4 或稳定 Registry key）|
| `nodes` | array | 必填 | `NdpGraphNode` 对象数组；最大 256 |
| `edges` | array | 必填 | `NdpGraphEdge` 对象数组；最大 1024 |
| `ttl` | uint32 | 可选 | 快照新鲜度秒数；默认 60 |
| `metadata` | object | 可选 | 附加在该快照上的任意 key-value 元数据 |

**NdpGraphNode**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `nid` | string | 必填 | 图中节点的 NID，例如 `urn:nps:node:{domain}:{id}` 或 `urn:nps:agent:{domain}:{id}` |
| `cluster_anchor` | string | 可选 | 该节点所属 cluster anchor 的 NID |
| `node_roles` | array[string] | 可选 | 角色标签（例如 `["memory"]` 或 `["action", "bridge"]`），词汇与 AnnounceFrame 相同 |

**NdpGraphEdge**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `from_nid` | string | 必填 | 源节点 NID |
| `to_nid` | string | 必填 | 目标节点 NID |
| `latency_ms` | uint32 | 可选 | 观测到的往返延迟，单位毫秒 |
| `protocol` | string | 可选 | 传输协议（`"tcp"` / `"quic"` / `"http"`）|

**校验**
- `nodes.length` MUST NOT 超过 256；违反时返回 `NDP-GRAPH-TOO-LARGE`。
- `edges.length` MUST NOT 超过 1024；违反时返回 `NDP-GRAPH-TOO-LARGE`。
- `edges` 中每个 `from_nid` 与 `to_nid` MUST 出现在 `nodes` 中；违反时返回 `NDP-GRAPH-INVALID`。
- 节点 MUST NOT 存在自环边（`from_nid == to_nid`）；违反时返回 `NDP-GRAPH-INVALID`。

---

## 4. Agent 初始化流程

```
Agent 启动
  │
  ├─ 1. NIP：加载 IdentFrame（从文件或 CA 自动获取）
  │
  ├─ 2. NDP：发送 AnnounceFrame（发布 presence、端点、角色、liveness）
  │
  ├─ 3. NDP：解析 peer，并接收 GraphFrame 拓扑快照
  │         ← ResolveFrame 响应目标端点
  │         ← Registry / Anchor 返回 GraphFrame snapshot
  │
  └─ 就绪：可发起 NWP 访问
```

---

## 5. DNS TXT 记录规范

节点可通过 DNS TXT 记录发布发现信息，无需中心化 Registry：

```
# 节点发现记录
_nps-node.api.example.com.  IN TXT  "v=nps1 type=memory port=17434 nid=urn:nps:node:api.example.com:products fp=sha256:a3f9..."

# CA 发现记录
_nps-ca.mycompany.com.      IN TXT  "v=nps1 ca=https://ca.mycompany.com/.well-known/nps-ca"
```

**TXT 记录字段**

| 键 | 必填 | 描述 |
|----|------|------|
| `v` | 必填 | 版本，固定 `nps1` |
| `type` | 可选 | 节点类型：`memory`/`action`/`complex` |
| `port` | 可选 | 端口（默认 17434）|
| `nid` | 必填 | 节点 NID |
| `fp` | 可选 | 节点证书指纹 |
| `ca` | 条件必填 | CA 发现端点（CA 记录必填）|

> **版本字符串格式说明**：DNS TXT `v` 键的值使用紧凑格式 `nps1`，遵循 DNS TXT 记录惯例（小写、无空格）。NCP 原生模式连接 preamble 使用 RFC 风格令牌 `NPS/1.0\n`（见 NPS-1 §2.6.1）。二者均标识 NPS 协议第 1 版，但编码形式不同，作用于不同的协议层，不可互换。

---

## 9. 联邦转发

**多 Anchor 集群解析（NPS-CR-0009）。** 解析某个 `cluster_anchor` NID 时，若存在多于一条存活的 Anchor 公告，Registry MUST 返回 `cluster_epoch` 最高的那一条。同一集群出现两条存活公告宣称**相同**的 `cluster_epoch` 属于脑裂（split-brain）故障，MUST 以 `NDP-CLUSTER-SPLIT`（→ `NPS-CLIENT-CONFLICT`）报告，而不得任意挑选其一。联邦 Registry 之间传播 `(cluster_anchor, cluster_epoch, active_nid)` 三元组；Registry MUST 优先采用从 peer 收到的更高 `cluster_epoch`，且 MUST NOT 把某个集群降级到更低的 epoch（每集群单调）。见 [NPS-CR-0009](cr/NPS-CR-0009-multi-anchor-ha.md)。


当 `public-federated` Registry 从 peer Registry 收到一个经联邦链路转发的 AnnounceFrame 时，它 MUST：

1. 将转发方 NID 追加到 `ndp-forwarded-by` 请求头（逗号分隔 NID 列表），再转发给自己的订阅者。
2. 如果自己的 NID 已出现在 `ndp-forwarded-by` 中，MUST 丢弃该帧并返回 `NDP-FEDERATION-LOOP`（环路检测）。
3. 如果 hop count（`ndp-forwarded-by` 列表长度）超过 **3** hops，MUST 静默丢弃该帧。

`local-dev` 与 `org-private` Registry MUST NOT 转发 AnnounceFrame；若收到转发帧，MAY 记录 warning。

**`ndp-forwarded-by` Header 格式**

NPS NID 的逗号分隔列表，每一项表示一跳：

```http
ndp-forwarded-by: urn:nps:agent:registry-a.example.com:r1, urn:nps:agent:registry-b.example.com:r2
```

---

## 6. 错误码

| 错误码 | NPS 状态码 | 描述 |
|--------|-----------|------|
| `NDP-RESOLVE-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | nwp:// 地址无法解析 |
| `NDP-RESOLVE-AMBIGUOUS` | `NPS-CLIENT-CONFLICT` | 解析结果存在冲突（多个不一致的注册）|
| `NDP-RESOLVE-TIMEOUT` | `NPS-SERVER-TIMEOUT` | 解析请求超时 |
| `NDP-RESOLVE-STALE` | `NPS-CLIENT-NOT-FOUND` | 被解析条目的新鲜度截止 `(last_seen ?? timestamp) + ttl` 已过期；该注册已陈旧，MUST NOT 被返回（NDP v0.9 §3.2.1）|
| `NDP-CLUSTER-SPLIT` | `NPS-CLIENT-CONFLICT` | 同一 `cluster_anchor` 下两条存活的 Anchor 公告宣称相同的 `cluster_epoch`；集群处于脑裂状态，MUST NOT 任意解析（NDP v0.10 §9，NPS-CR-0009）|
| `NDP-ANNOUNCE-SIGNATURE-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | AnnounceFrame 签名验证失败 |
| `NDP-ANNOUNCE-NID-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | AnnounceFrame 中 NID 与签名证书不一致 |
| `NDP-ANNOUNCE-ROLE-REMOVED` | `NPS-CLIENT-BAD-FRAME` | `node_roles` 包含已移除的遗留值 `"gateway"`（NPS-CR-0001）；响应 SHOULD 携带指向 NPS-CR-0001 的 `hint` 字段 |
| `NDP-ANNOUNCE-ROLE-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | `node_roles` 包含无法识别的值（`"gateway"` 遗留情况请用 `NDP-ANNOUNCE-ROLE-REMOVED`）|
| `NDP-ANNOUNCE-CONFLICT` | `NPS-CLIENT-CONFLICT` | 两个 AnnounceFrame 拥有相同的 `nid` 与 `graph_seq` 但内容不同（注册表投毒企图）|
| `NDP-ANNOUNCE-PROFILE-VIOLATION` | `NPS-AUTH-FORBIDDEN` | AnnounceFrame 违反当前 Registry 安全 profile 约束，且没有更具体的 NDP 错误码可用 |
| `NDP-GRAPH-SEQ-ROLLBACK` | `NPS-CLIENT-BAD-FRAME` | AnnounceFrame 的 `graph_seq` 小于接收方已为该 NID 接受的最高值（回滚企图）|
| `NDP-GRAPH-SEQ-GAP` | `NPS-STREAM-SEQ-GAP` | GraphFrame 序号不连续 |
| `NDP-FEDERATION-LOOP` | `NPS-CLIENT-CONFLICT` | 联邦转发通过 `ndp-forwarded-by` hop 列表检测到环路 |
| `NDP-STATE-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | 要求持久化的 Registry 无法持久读取或提交恢复状态 |
| `NDP-STATE-CORRUPT` | `NPS-SERVER-INTERNAL` | 持久化 Registry 恢复状态未通过 schema、checksum 或单调 fence 校验 |
| `NDP-ISSUER-NOT-ALLOWED` | `NPS-AUTH-FORBIDDEN` | AnnounceFrame 的签发者（签名 CA）不在当前注册表 profile 的签发者白名单中 |
| `NDP-CA-ATTEST-REQUIRED` | `NPS-AUTH-UNAUTHENTICATED` | 当前注册表 profile 要求 CA 背书的 NID，但 AnnounceFrame 证书链未锚定到配置的信任根 |
| `NDP-REGISTRY-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | NDP Registry 暂时不可用 |
| `NDP-GRAPH-TOO-LARGE` | `NPS-LIMIT-PAYLOAD` | GraphFrame `nodes` > 256 或 `edges` > 1024 |
| `NDP-GRAPH-INVALID` | `NPS-CLIENT-BAD-FRAME` | GraphFrame 边引用了不在 `nodes` 列表中的 NID，或存在自环边 |
| `NDP-ANNOUNCE-STALE` | `NPS-CLIENT-NOT-FOUND` | 超过 `heartbeat_interval_ms × 3` 未收到重新公告，AnnounceFrame 心跳已过期 |

HTTP 模式下的状态码映射见 [status-codes.cn.md](status-codes.cn.md)。

---

## 7. 安全考量

### 7.1 Announce 防伪
接收方 MUST 验证 AnnounceFrame 中的 signature，确认发布者持有对应 NID 的私钥，防止伪造节点广播。

### 7.2 Registry 防污染
中心化 Registry（NPS Cloud）MUST 要求注册方提供有效 IdentFrame，并通过 NIP CA 验证后才接受 AnnounceFrame 入库。

### 7.3 注册表安全 Profile

每个 NDP Registry 部署 MUST 显式声明三种安全 profile 之一。Profile 是 Registry 自身的配置，而不是 AnnounceFrame 的配置：profile 决定 Registry 接受、保留、对外提供哪些 AnnounceFrame。实现 MUST 在未声明 profile 时拒绝启动（不存在隐式默认）。

| Profile | 签发者白名单 | CA 背书 NID | 重放窗口 | 联邦 |
|---------|-------------|------------|---------|------|
| `local-dev` | 不强制 | 不要求 | `0`（重放防御关闭）| 不允许 |
| `org-private` | 必填（CA 证书指纹集合）| SHOULD | `300s` | 不允许 |
| `public-federated` | 通过 CA 信任链强制 | MUST | `300s` | 允许（需双边信任协议，见 §7.6）|

**`local-dev`**：仅用于单机或单开发者环境。如果 Registry 监听任何非 loopback 接口，MUST 拒绝以 `local-dev` 启动，除非操作员设置覆盖标志；该覆盖 MUST 在每次启动日志中记录。任何承载真实流量的服务 MUST NOT 使用 `local-dev`。

**`org-private`**：单一组织的内网注册表。运营方配置非空的 CA 证书指纹集合作为签发者白名单；签名链未终结于白名单内 CA 的 AnnounceFrame MUST 以 `NDP-ISSUER-NOT-ALLOWED` 拒绝。CA 背书的 NID（其所有权由组织 CA 背书的 NID，见 NPS-3 §6）SHOULD 强制；若强制且缺失，以 `NDP-CA-ATTEST-REQUIRED` 拒绝。跨注册表联邦 MUST 关闭。

**`public-federated`**：公网注册表（如 NPS Cloud Registry）。CA 背书 NID MUST 强制：证书链未锚定到已配置的公开信任根的 AnnounceFrame，Registry MUST 以 `NDP-CA-ATTEST-REQUIRED` 拒绝。签发者白名单隐含在该信任根集合中。跨注册表联邦 MAY 按 §7.6 启用。

### 7.4 AnnounceFrame 防投毒

无论何种 profile，每个 NDP Registry MUST 强制以下规则：

1. **签名覆盖范围**。`signature` 字段 MUST 覆盖 AnnounceFrame canonical body：所有已发出的 AnnounceFrame wire 字段，但排除 `signature`、`health`、`last_seen` 以及 `frame` 这类传输/核心帧判别字段。值缺失或为 `null` 的可选字段 MUST 从 canonical body 中省略，不得序列化为 JSON `null`。`heartbeat_interval_ms` 参与签名；若 wire 上缺失，验证方 MUST 按默认值 `60000` 进行 canonicalize。接收方 MUST 在任何注册表侧状态变更前完成签名验证；签名验证 MUST 先于去重、冲突检测和持久化。
2. **重放防御**。`timestamp` 偏离当前时间超过 profile 重放窗口（过去或未来）的 AnnounceFrame，MUST 以 `NDP-ANNOUNCE-SIGNATURE-INVALID` 拒绝。重放窗口 `0`（仅 `local-dev` profile）禁用该检查。
3. **重复抑制**。`(nid, graph_seq)` 与完整 wire 内容都与已接受条目完全相同的 AnnounceFrame，MUST 静默丢弃（不返回错误，不变更状态）。多播环境下的字节级重复属正常现象。
4. **Liveness 刷新**。当 `(nid, graph_seq)` 与 canonical 签名体均匹配，仅 `health` 或 `last_seen` 发生变化时，Registry MUST 刷新这两个 advisory 字段并重算新鲜度，且 MUST NOT 把该帧视为冲突或回滚。
5. **冲突拒绝**。`(nid, graph_seq)` 与已有条目相同但 canonical 签名体不同的 AnnounceFrame，MUST 以 `NDP-ANNOUNCE-CONFLICT` 拒绝。被拒帧与已有条目 SHOULD 同步记入审计日志供运营方排查；冲突帧是私钥泄露或发布者集群配置错误的证据。

Canonicalize 按 Unicode 码点升序递归排序对象键、保留数组顺序、不输出无意义空白、并省略缺失或为 `null` 的可选字段。Registry SHOULD 比对该 canonical UTF-8 体的 SHA-256 摘要，而不是另存一份序列化副本。

可移植的准入顺序为规范性要求：

1. 验证签名与 NID 绑定；
2. 应用重放窗口与 Registry profile 检查；
3. canonicalize 并比对 `graph_seq` / 所覆盖内容；
4. 处理合法的 `ttl = 0` 下线公告；
5. 以 `NDP-ANNOUNCE-STALE` 拒绝已经陈旧的存活公告；
6. 插入、刷新或替换该条目。

任何失败的步骤都不得变更 Registry 内容或 per-NID 序列栅栏。在执行上述准入顺序之前，Registry MUST 校验 §3.1 的 wire 类型。特别地：`ttl` MUST 存在且为 `uint32`；`graph_seq` 存在时 MUST 为 `uint64`，即便 `local-dev` 允许该字段缺失；每个 Bridge 协议字段存在时 MUST 为仅含非空字符串的数组。格式错误的已存在值不等价于缺失的兼容值，MUST 以 `NDP-ANNOUNCE-PROFILE-VIOLATION` 拒绝。对存活公告而言，新鲜度为 `(last_seen ?? timestamp) + ttl`；Registry MUST NOT 用自身的本地收帧时间延长它。合法的下线公告移除存活条目，但保留其已接受的序列栅栏。

### 7.5 Graph 序列回滚防御

AnnounceFrame（以及对应 GraphFrame）的 `graph_seq` 字段是 per-NID 单调递增计数器。接收方 MUST 跟踪每个 NID 已接受的最高 `graph_seq`。更小的值 MUST 以 `NDP-GRAPH-SEQ-ROLLBACK` 拒绝。相等的值按 §7.4 处理：完全重传被抑制，仅 advisory 的 liveness 更新刷新条目，所覆盖内容发生变化则为 `NDP-ANNOUNCE-CONFLICT`。更大的值替换先前条目。这防止攻击者捕获旧 AnnounceFrame 后再次发布以使节点状态降级。在 `org-private` 与 `public-federated` profile 下，已跟踪最高值 MUST 在 Registry 重启后持久化；`local-dev` MAY 仅保存在内存中。

### 7.6 跨注册表联邦

联邦——从对等注册表导入 AnnounceFrame——仅在 `public-federated` profile 下允许。两个 Registry 之间的联邦需要 **双边信任协议**：

1. 双方运营方 MUST 通过带外渠道交换 CA 根证书集合；各方将对方的根证书配置为联邦专用信任集合，与本机原生信任集合区分。
2. 从联邦对等方导入的 AnnounceFrame，MUST 用导入侧本地 CA 信任链（原生 ∪ 联邦专用）重新校验。在对等方有效的 AnnounceFrame 在本侧并不隐式有效。
3. 联邦通道本身 MUST 经过认证（基于 NIP 颁发证书的双向 TLS）。
4. 联邦条目 MUST 标注来源注册表标识，使下游消费者可基于来源应用差异化策略（例如排序、过滤）。
5. 任何一方 MAY 单方面撤销协议；撤销后导入侧 Registry MUST 在一个 TTL 窗口内使所有来自被撤销对等方的联邦条目失效。

联邦永不可传递：信任协议 A↔B 与 B↔C 不构成 A↔C。

### 7.7 注册表运营方信任级别

Registry 所采用的 profile 隐含相应的运营方信任级别。合规部署 MUST 满足其所声明 profile 的运营方信任级别要求。

| Profile | 运营方 | CA 范围 | 审计要求 |
|---------|-------|---------|---------|
| `local-dev` | 个人（单开发者 / CI 临时实例）| 无 / 自签 | 无 |
| `org-private` | 具名组织 | 组织 CA | 内部审计（年度签发者白名单复核、配置变更签名）|
| `public-federated` | 具备问责能力的法人主体（如 NPS Cloud）| 公开 CA 链 | 外部审计（独立第三方年度审计，覆盖签发者白名单溯源、联邦协议、密钥托管、事件响应）|

Registry MUST NOT 声明其无法满足相应运营方信任要求的 profile。

### 7.8 alpha.19 持久恢复 Profile（NDP v0.13）

以下要求 ID 冻结 restart 与 partition 行为；共享 transcript 位于
[`recovery_fence_vectors.json`](conformance/ndp/recovery_fence_vectors.json)。

1. **NDP-P19-01 —— 持久状态。** `org-private` 与 `public-federated` Registry
   MUST 至少持久保存每个 NID 已接受的最高 `graph_seq` 及 canonical body digest、
   每个集群已接受的最高 ownership epoch，以及每个存活条目的 freshness deadline、
   origin 与 trust class。
2. **NDP-P19-02 —— 可见前提交。** 推进 sequence 或 epoch fence 的 mutation
   MUST 先原子提交新 fence 与条目，之后才能 acknowledge、供 ResolveFrame 返回或转发给 peer。
3. **NDP-P19-03 —— 恢复顺序。** 启动时必须先恢复并校验 sequence/epoch fence，
   再接收流量；随后移除过期 live entry，但保留其 fence。恢复出的较低 sequence/epoch
   永不得替换更高的持久值。
4. **NDP-P19-04 —— 同 epoch 脑裂。** restart 或 partition healing 后，同一最高
   cluster epoch 上的两个不同存活 owner 仍是 `NDP-CLUSTER-SPLIT`；不得用到达时间裁决。
5. **NDP-P19-05 —— 损坏策略。** 要求持久化的 Registry 在 schema、checksum
   或单调校验失败时 MUST 以 `NDP-STATE-CORRUPT` 拒绝 ready，不得静默从空状态启动。
6. **NDP-P19-06 —— store 不可用。** 无法持久读取或提交状态时返回
   `NDP-STATE-UNAVAILABLE`，且 MUST NOT acknowledge 受影响的 announce/federation mutation。
7. **NDP-P19-07 —— local-dev 边界。** `local-dev` MAY 使用纯内存状态，但 MUST
   在 health/capability 输出声明 `recovery = "volatile"`；其余 profile MUST 声明
   `recovery = "durable"`。
8. **NDP-P19-08 —— 联邦 provenance。** 恢复的联邦条目保留 origin Registry 与
   trust class。撤销双边信任协议时，使匹配的恢复条目失效，但不降低无关 fence。

---

### 7.9 生产运行时发现绑定（NDP v0.14）

以下要求适用于使用 nps.production_runtime revision 1 的客户端，
复用既有发现字段，不把 Announce 能力声明变成授权捷径。

1. **NDP-P20-01 — 新鲜度。** 选择新目标前，验证解析记录的 timestamp/last_seen/ttl
   及既有签名、profile、fence 规则。过期或 health=draining 的记录不得接收新的
   profile 操作。正面 health 报告不证明身份或特性支持。
2. **NDP-P20-02 — Owner fence。** cluster_anchor 解析到唯一最高存活 cluster_epoch
   owner。同 epoch 分裂、可信解析不可用或 fence 回退按既有 NDP 错误安全拒绝。
   不得为方便而回退缓存的旧 owner。
3. **NDP-P20-03 — 新绑定。** Endpoint/owner/epoch 改变时，使旧 peer 的路由、
   特性缓存及相关连接池会话失效。新连接重新 NCP 协商和 NIP 准入。
   Announce capabilities、Graph 成员关系或 TLS 恢复都不能授予旧选择。
4. **NDP-P20-04 — HTTP 证据。** 依赖特性的 HTTP 派发前，获取端点的认证 NWM
   并执行 NWP §9.6 全后端路由保证。发现缓存只是线索，不是授权；
   key 包含解析 peer 身份/epoch、路由及 manifest version，不能延长新鲜度，
   也不能替代当前 handler 准入。
5. **NDP-P20-05 — 资格记录。** 运行时证据记录 registry 身份、解析 NID、endpoint、
   cluster_epoch、graph_seq、新鲜度截止时间、manifest version 和所选特性 revision，
   不记录凭据或敏感 scope。此为本地/资格记录，不新增签名 Announce 字段。
   故障转移仅在 NCP §7.8 明确的共享持久去重契约下允许重试不确定 mutation；
   路由成功绝不证明前次未执行。

---

参考实现说明：显式选择的[易失结果路由组件](../impl/dotnet/src/NPS.NDP/Discovery/README.cn.md)及[静态/双节点夹具](../docs/samples/ndp-result-routing.cn.md)仅验证所声明的 HTTP 参考路径，不代表完整生产运行时或持久注册表 Profile 已通过。

## 8. 变更历史

| 版本 | 日期 | 变更 |
|------|------|------|
| 0.14 | 2026-09-09 | Alpha.20 生产运行时发现绑定：排除过期/draining 目标、owner fence、换节点重新准入、HTTP 路由保证与有界资格记录，不新增 Announce 字段。 |
| 0.13 | 2026-08-31 | alpha.19 持久恢复 Profile：原子持久 sequence/epoch fence、有序 restart recovery、保留同 epoch 脑裂、状态损坏/不可用时 fail closed、显式 volatile/durable 能力真实性、联邦 provenance，以及共享 restart/partition transcript。|
| 0.12 | 2026-07-29 | 定义 Registry Conformance profile：additive 的 `graph_seq` wire 字段及其兼容行为；确定性的签名体 canonicalization；有序的 签名/profile/重放/冲突/陈旧 准入流程；完全重复与 advisory liveness 刷新的语义区分；条目过期/下线后仍保留序列栅栏；最高 epoch 的集群解析与脑裂行为；按方向区分且已排序的 Bridge 能力发现；以及跨语言共享的 canonicalization / Registry 测试向量。|
| 0.11 | 2026-07-12 | **NPS-CR-0010 Bridge Node 是双向的**：AnnounceFrame (0x30) 新增可选 `bridge_inbound_protocols`（string 数组，取值域同 `bridge_protocols`）—— 该节点**入向服务**（外部 → NPS）的外部协议集。`bridge_protocols` 保持其原有含义不变（**出向**桥接的协议集）。缺省/为空 ⇒ 不暴露入向面，即 alpha.16 之前的纯出向 Bridge Node。声明 `"bridge"` 的节点 MUST 保证两个数组至少一个非空。Additive / 向后兼容；无新增 NDP 错误码。|
| 0.10 | 2026-07-05 | **NPS-CR-0009 多 Anchor 高可用**：AnnounceFrame (0x30) 新增可选 `cluster_epoch`（uint64，默认 1）—— 集群所有权栅栏。新增 §9 解析规则：对一个 `cluster_anchor` NID 解析出 `cluster_epoch` 最高的存活 Anchor；同 epoch 的脑裂 → `NDP-CLUSTER-SPLIT`；联邦 Registry 传播 `(cluster_anchor, cluster_epoch, active_nid)` 三元组并优先取更高 epoch（每集群单调）。新增一个错误码 `NDP-CLUSTER-SPLIT`。Additive / 向后兼容（单 Anchor 集群保持 epoch 1）。|
| 0.9 | 2026-06-12 | AnnounceFrame (0x30) 新增两个可选 liveness 字段 —— `health`（`healthy`/`degraded`/`draining`，缺省 ⇒ `healthy`）和 `last_seen`（ISO 8601 心跳时间）。新增 §3.2.1 **解析期陈旧检查**：当被解析条目的新鲜度截止 `(last_seen ?? timestamp) + ttl` 已过期时，Registry MUST 返回 `NDP-RESOLVE-STALE` 而非提供已失效端点；resolved 对象 SHOULD 回显 `health`。1 个新错误码 `NDP-RESOLVE-STALE`。向后兼容（两字段均可选）。|
| 0.9 | 2026-06-03 | AnnounceFrame 新增 `heartbeat_interval_ms`（uint32，默认 60000）和 `spawn_spec_ref`（解析为 SpawnSpec 的字符串引用；§3.1.2 schema —— OCI image + command + resource_limits；解析规则见 NPS-CR-0007 §5）；新增 `NDP-ANNOUNCE-STALE` 错误码（公告期陈旧，区别于 §3.2.1 解析期 `NDP-RESOLVE-STALE`）。|
| 0.8 | 2026-05-31 | §3.3 GraphFrame 改写为拓扑快照格式：`graph_id`（UUID v4）、`nodes`（NdpGraphNode 数组，含 nid/cluster_anchor/node_roles）、`edges`（NdpGraphEdge 数组，含 from_nid/to_nid/latency_ms/protocol）、`ttl`、`metadata`；最大 256 nodes / 1024 edges；新增 `NDP-GRAPH-TOO-LARGE` 与 `NDP-GRAPH-INVALID` 错误码；新增 §9 联邦转发（public-federated MUST 转发 AnnounceFrame、`ndp-forwarded-by` header、最多 3 hops、`NDP-FEDERATION-LOOP`）。|
| 0.7 | 2026-05-10 | 新增 §7.3–§7.7：三个具名注册表安全 profile（`local-dev` / `org-private` / `public-federated`）、AnnounceFrame 防投毒规则（签名覆盖、重放防御、重复抑制、冲突拒绝）、graph_seq 回滚防御、跨注册表联邦要求、运营方信任级别表。新增错误码：`NDP-ANNOUNCE-CONFLICT`、`NDP-GRAPH-SEQ-ROLLBACK`、`NDP-ISSUER-NOT-ALLOWED`、`NDP-CA-ATTEST-REQUIRED`。（Issue #33）|
| 0.6 | 2026-05-01 | **破坏性更名（pre-1.0）**：`node_kind` 更名为 `node_roles`（仅接受 string 数组；废弃单字符串形式）。解析器 MUST 在 alpha.5 之前把 `node_kind` 当别名接受以保持向后兼容。新增约束：NWP NWM `node_type` MUST 为 `node_roles` 中的一项（见 NWP §2.1 节点角色解析）。修复 M1 命名消歧问题——`node_kind`（多角色发现字段）与 `node_type`（单运营角色）名字相近但语义分叉，且无跨协议约束文档。|
| 0.5 | 2026-04-26 | AnnounceFrame (0x30) 新增三个 additive 字段以支撑 NPS-CR-0001 —— `node_kind`（string 或 string 数组；取值 `"memory"` / `"action"` / `"complex"` / `"anchor"` / `"bridge"`；遗留 `"gateway"` 拒绝）、`cluster_anchor`（NID —— 标识非-Anchor 集群成员注册到的 Anchor）、`bridge_protocols`（string 数组 —— 给 `"bridge"` 节点；标准值 `"http"` / `"grpc"` / `"mcp"` / `"a2a"`）。全部 additive 且向后兼容：alpha.3 之前的发布者不发 `node_kind`，接收方回退到 `node_type`。Depends-On 升级为 NCP v0.7（NPS-RFC-0001）+ NIP v0.9（NPS-RFC-0003）。|
| 0.4 | 2026-04-24 | AnnounceFrame (0x30) 新增三个 additive 字段 —— `activation_mode`（NPS-Node Profile L1+ 必填）、`activation_endpoint`（`resident` / `hybrid` 必填）、`spawn_spec_ref`（L3，可选）。新增 §3.1.1 激活语义小节与 alpha.3 前发布者的向后兼容规则。`Depends-On` NCP 版本订正为 v0.5。 |
| 0.3 | 2026-04-19 | Status Draft → Proposed；双语统一（EN 主 + CN 副镜像）；无 wire 层变更 |
| 0.2 | 2026-04-12 | 统一端口 17433；错误码改用 NPS 状态码映射；完善错误码列表 |
| 0.1 | 2026-04-10 | 初始骨架：AnnounceFrame/ResolveFrame/GraphFrame、DNS TXT 规范、初始化流程 |

---

*归属：LabAcacia / INNO LOTUS PTY LTD · Apache 2.0*
