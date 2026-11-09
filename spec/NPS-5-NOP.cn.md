[English Version](./NPS-5-NOP.md) | 中文版

# NPS-5: Neural Orchestration Protocol (NOP)

**Spec Number**: NPS-5
**Status**: Proposed
**Version**: 0.11
**Date**: 2026-09-09
**Port**: 17433（默认，共用）/ 17437（可选独立）
**Authors**: Ori Lynn / INNO LOTUS PTY LTD
**Depends-On**: NPS-1 (NCP v0.13)、NPS-2 (NWP v0.23)、NPS-3 (NIP v0.16)、NPS-4 (NDP v0.14)
**Supersedes**: NCP AlignFrame (0x05)

> 本文档为 NOP 详细规范。套件总览见 [NPS-0-Overview.cn.md](NPS-0-Overview.cn.md)。

---

## 1. Terminology

本文档中的关键字 "MUST"、"MUST NOT"、"SHOULD"、"MAY" 按照 RFC 2119 解释。

---

## 2. 协议概述

NOP 定义多 Agent 协作的任务分发、委托、同步与结果汇聚。基于 NCP AlignFrame (0x05) 演进为 AlignStream (0x43)，支持有向无环图（DAG）式任务流、跨 Agent 中间结果共享、资源预检、K-of-N 同步屏障，以及 OpenTelemetry 分布式追踪。

### 2.1 角色

| 角色 | 描述 |
|------|------|
| **Orchestrator** | 发起 TaskFrame，分解并分配子任务，汇聚最终结果 |
| **Worker Agent** | 执行子任务，通过 AlignStream 返回中间或最终结果 |
| **Relay Agent** | 透明转发，不执行任务（可选，用于隔离网络区域）|

### 2.2 与 NCP AlignFrame 的关系

NCP AlignFrame (0x05) 已标记为 **Deprecated**。AlignStream (0x43) 在其基础上增加：

- 任务 DAG 上下文（task_id 绑定）
- 跨 Agent 中间结果共享
- 超时与重试语义（per-node 粒度）
- NIP 身份绑定（每个委托步骤验证 sender_nid）
- Token 级背压控制

### 2.3 NOP 在 NPS 协议栈中的位置

```
NOP（多 Agent 编排）
  ├── NWP（数据与操作访问）
  │     └── NCP（传输帧 & 编码）
  └── NIP（身份认证 & scope 校验）
```

Orchestrator 通过 NWP ActionFrame 调用 Worker Agent 的节点操作；Worker Agent 通过 AlignStream 向 Orchestrator 推送中间/最终结果；每一步委托均须通过 NIP 验证 scope。

---

## 3. 帧类型

### 3.1 TaskFrame (0x40)

Orchestrator 向自身运行时或协调节点提交的整体任务定义。

**字段定义**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `frame` | uint8 | 必填 | 固定值 `0x40` |
| `task_id` | string | 必填 | 任务唯一标识（UUID v4）|
| `dag` | object | 必填 | DAG 定义，见 §3.1.1 |
| `timeout_ms` | uint32 | 可选 | 整体任务超时（毫秒），默认 30000，最大 3600000（1 小时）|
| `max_retries` | uint8 | 可选 | 全局最大重试次数（单个节点超过时整体失败），默认 2 |
| `priority` | string | 可选 | 任务优先级：`"low"` / `"normal"`（默认）/ `"high"` |
| `callback_url` | string | 可选 | 任务完成/失败时的回调 URL（`https://`，见 §9.4）|
| `preflight` | bool | 可选 | true 时先执行资源预检（§4），通过后再正式执行，默认 false |
| `compensation_policy` | string | 可选 | Saga 补偿策略：`"best_effort"`（默认）或 `"strict"`，见 §3.1.6 |
| `callback_secret` | string | 可选 | webhook 投递签名用的 HMAC-SHA256 key（base64url，32 字节）。存在时 Orchestrator MUST 对原始 JSON body 计算 `X-NPS-Signature: sha256=<HMAC-SHA256-hex>` 并随每次 POST 到 `callback_url` 一起发送。接收方 SHOULD 在缺少该 header 时以 400 拒绝。错误码：`NOP-CALLBACK-HMAC-MISSING`。 |
| `result_ttl_seconds` | uint32 | 可选 | Orchestrator 在任务完成后保留任务结果的时长（秒），默认 3600。TTL 过期后再获取结果返回 `NOP-TASK-RESULT-EXPIRED`。（NOP v0.7）|
| `context` | object | 可选 | 透传上下文，见 §3.1.2 |
| `request_id` | string | 可选 | UUID v4，用于请求追踪 |

#### §3.1.1 dag 字段

DAG 由 `nodes`（顶点）和 `edges`（有向边）组成，描述子任务的执行顺序与数据流向。

**DAG 节点字段**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `id` | string | 必填 | 节点唯一标识（DAG 内唯一）|
| `action` | string | 必填 | 操作 URL（`nwp://...`）|
| `agent` | string | 必填 | 执行此节点的 Worker Agent NID |
| `input_from` | array | 可选 | 依赖的上游节点 id 列表；空或 null 表示起点节点 |
| `input_mapping` | object | 可选 | 上游输出字段 → 本节点输入参数的映射，见 §3.1.3 |
| `timeout_ms` | uint32 | 可选 | 本节点超时（毫秒）；优先级高于 TaskFrame 全局 timeout_ms |
| `retry_policy` | object | 可选 | 本节点重试策略，见 §3.1.4 |
| `condition` | string | 可选 | 条件表达式（CEL 子集），为 false 时跳过本节点（见 §3.1.5）|
| `compensate_action` | string | 可选 | 当本节点需要被回滚时调用的补偿操作 NID URI（见 §3.1.6）|
| `compensate_params_mapping` | object | 可选 | 将本节点输出字段名映射为 `compensate_action` 输入参数名（见 §3.1.6）|

**DAG 校验规则**
- MUST 是有向无环图（不含环路，违反返回 `NOP-TASK-DAG-CYCLE`）
- MUST 至少有一个起点节点（无入边）和一个终点节点（无出边）
- 最大节点数：**32**（违反返回 `NOP-TASK-DAG-TOO-LARGE`）
- 最大委托链深度：**3 层** —— 链中每个实体算一层（Orchestrator = 第 1 层，Worker = 第 2 层，Sub-Worker = 第 3 层）；第 4 个实体（Sub-Sub-Worker）将超出上限。等价地：最多允许 2 次 `DelegateFrame` 跳转。违反返回 `NOP-DELEGATE-CHAIN-TOO-DEEP`。

**DAG 示例**

```json
{
  "nodes": [
    {
      "id": "fetch",
      "action": "nwp://api.example.com/products/query",
      "agent":  "urn:nps:agent:...:fetcher",
      "timeout_ms": 5000
    },
    {
      "id": "analyze",
      "action": "nwp://ml.example.com/inference/invoke",
      "agent":  "urn:nps:agent:...:analyzer",
      "input_from": ["fetch"],
      "input_mapping": { "products": "$.fetch.data" },
      "retry_policy": { "max_retries": 3, "backoff": "exponential" }
    },
    {
      "id": "report",
      "action": "nwp://report.example.com/generate/invoke",
      "agent":  "urn:nps:agent:...:reporter",
      "input_from": ["analyze"],
      "input_mapping": { "analysis": "$.analyze.result" },
      "condition": "$.analyze.result.score > 0.7"
    }
  ],
  "edges": [
    { "from": "fetch",   "to": "analyze" },
    { "from": "analyze", "to": "report"  }
  ]
}
```

#### §3.1.2 context 字段

| 字段 | 类型 | 描述 |
|------|------|------|
| `session_id` | string | Agent 会话标识（跨请求复用）|
| `trace_id` | string | OpenTelemetry Trace ID（16 字节十六进制，32 字符）|
| `span_id` | string | 当前 Span ID（8 字节十六进制，16 字符）|
| `trace_flags` | uint8 | OpenTelemetry Trace Flags（如 `0x01` = sampled）|
| `baggage` | object | OpenTelemetry Baggage（键值对，透传至所有子任务）|
| `custom` | object | 应用自定义上下文（透明传递，NOP 不解析）|

实现 SHOULD 支持 OpenTelemetry W3C TraceContext 格式，以便在现有监控系统（Jaeger / Zipkin / Tempo）中可视化多 Agent 任务跳转链路。

#### §3.1.3 input_mapping 字段

`input_mapping` 使用 JSONPath 表达式将上游节点的输出结果字段映射到本节点的输入参数：

```json
{
  "input_mapping": {
    "local_param_name": "$.upstream_node_id.result.field_path"
  }
}
```

- 键：本节点 ActionFrame.params 中的参数名
- 值：JSONPath 表达式，根节点 `$` 表示 Orchestrator 上下文（包含所有上游节点的结果）
- 多个上游结果需合并时，可使用 `["$.node_a.result", "$.node_b.result"]` 数组

#### §3.1.4 retry_policy 字段

| 字段 | 类型 | 描述 |
|------|------|------|
| `max_retries` | uint8 | 最大重试次数（覆盖 TaskFrame 全局 max_retries）|
| `backoff` | string | 退避策略：`"fixed"` / `"linear"` / `"exponential"`（默认）|
| `initial_delay_ms` | uint32 | 首次重试延迟（毫秒），默认 1000 |
| `max_delay_ms` | uint32 | 最大延迟上限（毫秒），默认 30000 |
| `retry_on` | array | 触发重试的错误码列表；省略时对所有失败重试 |

退避公式：`delay = min(initial_delay_ms * backoff_factor^attempt, max_delay_ms)`
- `fixed`: factor=1；`linear`: factor=attempt；`exponential`: factor=2^attempt

#### §3.1.5 condition 字段

`condition` 使用 **CEL（Common Expression Language）子集**，支持：

- 数值比较：`>`, `>=`, `<`, `<=`, `==`, `!=`
- 布尔逻辑：`&&`, `||`, `!`
- JSONPath 访问上游结果（使用 `$.<node_id>.<field>` 语法）
- 字符串比较和 `in` 操作

condition 为 `false` 时，节点被跳过（状态标记为 `SKIPPED`）。若终点节点被跳过，TaskFrame 以 `COMPLETED` 结束（非 FAILED）。

#### §3.1.6 补偿语义（Saga）

已经产生外部可观察副作用的 DAG 节点（扣费、预订、资源预置、消息投递等）MAY 声明一个**补偿操作**，用于在语义上反转这些副作用。当下游失败迫使 Orchestrator 回滚时，已成功完成的前驱节点的补偿操作将按**逆拓扑顺序**被调用——这就是 saga 模式。

**节点级字段**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `compensate_action` | string | 可选 | 补偿操作 NID URI（`nwp://...`）；省略表示本节点不可逆 |
| `compensate_params_mapping` | object | 可选 | 将本节点输出字段名映射为 `compensate_action` 的输入参数名。键为 `compensate_action` 的参数名；值为以本节点结果为根的 JSONPath 表达式（如 `"$.charge_id"`）|

**TaskFrame 级字段**

`compensation_policy` 控制如何处理补偿失败：

| 取值 | 行为 |
|------|------|
| `"best_effort"`（默认）| 补偿失败仅记录日志；Orchestrator 继续补偿其余前驱节点，随后将整个 saga 标记为 FAILED 终结 |
| `"strict"` | 任意一次补偿失败立即终止整个 saga 并返回 `NOP-COMPENSATION-FAILED`；其余前驱节点 NOT 再补偿 |

**Saga 触发条件**

当节点 `N` 进入 `FAILED`（已耗尽 `retry_policy.max_retries`）且 `N` 存在已成功 `COMPLETED` 的前驱节点时，Orchestrator SHOULD 按**逆拓扑顺序**（最近完成的前驱优先）依次调用各前驱的 `compensate_action`。Orchestrator MUST：

1. 将每个被补偿的前驱标记为 `COMPENSATING`，并下发 `action=compensate_action` 的 DelegateFrame，`params` 由 `compensate_params_mapping` 应用于该前驱已存储的结果生成。
2. 补偿成功 → 前驱转入 `COMPENSATED`。
3. 补偿失败 → 前驱转入 `COMPENSATION_FAILED`，并按 `compensation_policy`（见上）处理。
4. 没有声明 `compensate_action` 的前驱被跳过。如果 `compensation_policy="strict"` 且任一需要补偿的前驱缺少 `compensate_action`，Orchestrator MUST 返回 `NOP-COMPENSATION-NOT-SUPPORTED`（终态），且不为该 saga 触发任何补偿调用。

补偿操作 MUST 是幂等的——MAY 按前驱节点的 `retry_policy` 重试，并 SHOULD 使用从 `<parent_task_id>:<node_id>:compensate` 推导的稳定 `idempotency_key`。

**补偿示例**

```json
{
  "id": "charge",
  "action": "nwp://payments.example.com/charge/invoke",
  "agent": "urn:nps:agent:...:payments",
  "compensate_action": "nwp://payments.example.com/refund/invoke",
  "compensate_params_mapping": { "charge_id": "$.charge_id", "amount": "$.amount" }
}
```

如果下游 `ship` 节点在 `charge` 完成之后失败，Orchestrator 会下发 `nwp://payments.example.com/refund/invoke`，参数为 `{ "charge_id": <已捕获>, "amount": <已捕获> }`，将 `charge` 节点的状态从 `COMPLETED → COMPENSATING → COMPENSATED`。

**完整 TaskFrame 示例**

```json
{
  "frame": "0x40",
  "task_id": "550e8400-e29b-41d4-a716-446655440000",
  "dag": {
    "nodes": [
      { "id": "fetch",   "action": "nwp://api.example.com/products/query",    "agent": "urn:nps:agent:...:fetcher",  "timeout_ms": 5000 },
      { "id": "analyze", "action": "nwp://ml.example.com/inference/invoke",   "agent": "urn:nps:agent:...:analyzer", "input_from": ["fetch"], "input_mapping": { "products": "$.fetch.data" } },
      { "id": "report",  "action": "nwp://report.example.com/generate/invoke","agent": "urn:nps:agent:...:reporter", "input_from": ["analyze"], "condition": "$.analyze.result.confidence > 0.8" }
    ],
    "edges": [
      { "from": "fetch",   "to": "analyze" },
      { "from": "analyze", "to": "report"  }
    ]
  },
  "timeout_ms": 60000,
  "max_retries": 2,
  "priority": "normal",
  "callback_url": "https://orchestrator.myapp.com/nop/callbacks",
  "preflight": true,
  "context": {
    "trace_id": "4bf92f3577b34da6a3ce929d0e0e4736",
    "span_id":  "00f067aa0ba902b7",
    "trace_flags": 1,
    "session_id": "sess-abc123"
  },
  "request_id": "550e8400-e29b-41d4-a716-446655440001"
}
```

---

### 3.2 DelegateFrame (0x41)

Orchestrator 将 DAG 中的单个子任务委托给 Worker Agent 执行。

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `frame` | uint8 | 必填 | 固定值 `0x41` |
| `parent_task_id` | string | 必填 | 父任务 task_id |
| `subtask_id` | string | 必填 | 子任务唯一标识（UUID v4）|
| `node_id` | string | 必填 | 对应 DAG 中的节点 id |
| `target_agent_nid` | string | 必填 | 被委托 Worker Agent 的 NID |
| `action` | string | 必填 | 操作 URL（`nwp://`）|
| `params` | object | 可选 | 操作参数（经 input_mapping 处理后的结果）|
| `delegated_scope` | object | 必填 | 从父 scope 裁剪的子集（MUST NOT 扩大）|
| `deadline_at` | string | 必填 | 子任务截止时间（ISO 8601 UTC）|
| `idempotency_key` | string | 可选 | 幂等键（重试时使用相同的值）|
| `priority` | string | 可选 | 继承自 TaskFrame.priority |
| `target_cluster_anchor` | string | 可选 | 本子任务要路由到的 cluster anchor 的 NID（跨集群委托，NOP v0.6）。存在时，Orchestrator MUST 把该 DelegateFrame 路由给注册在指定 cluster anchor 下的 Worker Agent。在多 Anchor 高可用（[NPS-CR-0009](cr/NPS-CR-0009-multi-anchor-ha.md)）下，Orchestrator MUST 把 `target_cluster_anchor` 解析到该集群**当前活跃**的 Anchor（`cluster_epoch` 最高者，NDP §9）；发生 `anchor_failover` 时，指向该集群的在途（in-flight）委托 MUST 在重试前重新解析到继任者。|
| `context` | object | 可选 | 透传上下文（从 TaskFrame.context 继承，span_id 更新为当前 Delegate span）|

**Scope 裁剪原则**

`delegated_scope` 中的 `nodes`、`actions`、`max_token_budget` 均 MUST 是父 Agent scope 的子集。CA 在签发时强制校验，违反则拒绝并返回 `NOP-DELEGATE-SCOPE-VIOLATION`。

**Worker Agent 拒绝响应**

若 Worker Agent 无法接受委托（负载、能力不足等），响应 CapsFrame：

```json
{
  "frame": "0x04",
  "anchor_ref": "nps:system:delegate:rejected",
  "count": 1,
  "data": [{
    "subtask_id": "uuid-v4",
    "error": "NOP-DELEGATE-REJECTED",
    "reason": "capacity_exceeded",
    "retry_after_ms": 5000
  }]
}
```

---

### 3.3 SyncFrame (0x42)

多 Agent 状态同步屏障，等待依赖子任务完成后再继续，支持 K-of-N 语义。

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `frame` | uint8 | 必填 | 固定值 `0x42` |
| `task_id` | string | 必填 | 父任务 task_id |
| `sync_id` | string | 必填 | 同步点唯一标识（UUID v4）|
| `wait_for` | array | 必填 | 需等待的 subtask_id 列表 |
| `min_required` | uint32 | 可选 | K-of-N：最少需要多少个子任务成功才继续；省略或 0 表示全部成功（默认）|
| `aggregate` | string | 可选 | 结果聚合策略：`"merge"`（默认）/ `"first"` / `"all"` / `"fastest_k"` / `"weighted_first_k"` / `"merge_all"`，见 §3.3.1 与 §3.3.2 |
| `timeout_ms` | uint32 | 可选 | 等待超时（毫秒），超时返回 `NOP-SYNC-TIMEOUT` |

#### §3.3.1 K-of-N 同步语义

`min_required` 字段支持以下场景：

| 场景 | 配置 | 行为 |
|------|------|------|
| 全部完成 | `min_required` 省略或 0 | 等待所有 wait_for 子任务成功 |
| 任意 K 个完成 | `min_required: K` | K 个子任务成功后立即继续，其余取消 |
| 冗余容错 | `min_required: N-1`（N-1 of N）| 允许 1 个节点失败仍继续 |

K < N 时，同步屏障通过后，Orchestrator SHOULD 向剩余未完成子任务发送取消信号（DelegateFrame with action="cancel"）。

#### §3.3.2 聚合策略定义

| 策略 | 描述 |
|------|------|
| `merge` | 将所有成功子任务的结果字段合并为单个对象（相同键后者覆盖前者）|
| `first` | 取第一个成功完成的子任务结果 |
| `all` | 将所有结果保留为数组 `[result_a, result_b, ...]` |
| `fastest_k` | 取最先完成的 `min_required` 个结果（合并为 `all` 格式）|
| `weighted_first_k` | 取置信度分数最高的 `min_required` 个结果（要求每个子任务结果中带有 `result.score` 字段）；合并为 `all` 格式（NOP v0.6）|
| `merge_all` | 将所有成功子任务的结果合并为单个对象，保留全部字段；与 `merge` 不同，数组类型的字段是拼接而非覆盖（NOP v0.6）|

**SyncFrame 完成响应（CapsFrame）**

```json
{
  "frame": "0x04",
  "anchor_ref": "nps:system:sync:result",
  "count": 1,
  "data": [{
    "sync_id": "uuid-v4",
    "task_id": "uuid-v4",
    "status": "completed",
    "completed": ["subtask-a", "subtask-b"],
    "skipped":   ["subtask-c"],
    "failed":    [],
    "results": {
      "subtask-a": { ... },
      "subtask-b": { ... }
    },
    "aggregated": { ... }
  }]
}
```

---

### 3.4 AlignStream (0x43)

有向任务流，替代 NCP AlignFrame (0x05)。携带 DAG 上下文和 NIP 身份绑定。

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `frame` | uint8 | 必填 | 固定值 `0x43` |
| `stream_id` | string | 必填 | 流唯一标识（UUID v4）|
| `task_id` | string | 必填 | 关联的父任务 task_id |
| `subtask_id` | string | 必填 | 关联的子任务 subtask_id |
| `seq` | uint64 | 必填 | 消息序号，严格递增，从 0 开始 |
| `payload_ref` | string | 可选 | CapsFrame 的 anchor_ref（中间结果引用）|
| `data` | object | 可选 | 中间结果数据 |
| `window_size` | uint32 | 可选 | 背压窗口大小（单位：CGN Token 数），见 §3.4.1 |
| `is_final` | bool | 必填 | true 表示流结束（最终结果帧）|
| `ack_seq` | uint64 | 可选 | 确认 `seq ≤ ack_seq` 的所有帧均已收到并处理完毕。用于滑动窗口协议（NOP v0.6 §3.4.2）。 |
| `nak_seq` | uint64 | 可选 | 否定确认：请求从 `nak_seq` 开始重传。发送方 MUST 从 `nak_seq` 起重发；若该帧已不可用，发送方返回 `NOP-STREAM-NAK-UNRESOLVABLE`。 |
| `sender_nid` | string | 必填 | 发送方 NID（接收方 MUST 验证与连接身份一致）|
| `error` | object | 可选 | 错误信息（is_final=true 时可携带，表示子任务失败）|

**error 对象**

| 字段 | 类型 | 描述 |
|------|------|------|
| `code` | string | NOP 错误码 |
| `message` | string | 人类可读错误描述 |
| `retryable` | bool | 是否可重试 |

#### §3.4.1 Token 级背压控制

`window_size` 表示接收方当前可以处理的 **CGN Token 数量上限**（而非字节数），直接对应下游推理端的处理能力：

- 发送方在发送每帧 `data` 前，SHOULD 估算本帧 CGN 消耗，并检查当前窗口余量
- 若估算消耗超出余量，SHOULD 暂停推送，等待接收方通过反向 AlignStream（仅更新 window_size）恢复窗口
- 窗口确认机制：接收方消费后，发送 `data=null, window_size=N`（N 为新增可用量）的 AlignStream 帧恢复窗口

**与 NCP StreamFrame 的区别**

| 维度 | StreamFrame (0x03) | AlignStream (0x43) |
|------|-------------------|--------------------|
| 场景 | 通用数据流（NWP 查询结果等）| 多 Agent 任务协作中间结果 |
| 上下文 | 无 | 携带 task_id + subtask_id |
| 身份绑定 | 无 | sender_nid 强制验证 |
| 背压单位 | 字节 / 帧数 | CGN Token 数 |
| 错误传播 | 无 | error 字段（任务失败语义）|

### 3.5 可移植 Orchestrator 合规性 Profile（v0.9）

本节定义共享的 Alpha.17 合规性 transcript 所使用的确定性 profile。生产环境的
Orchestrator MAY 并发执行相互独立的节点，但 MUST 产生相同的终态、映射值、
聚合结果、补偿集合、重试次数与安全决策。

#### 3.5.1 预检与调度

1. 在任何回调、预检探针或 worker 下发之前，MUST 先校验完整的 DAG。当同时有
   多个节点就绪时，Kahn 拓扑排序 MUST 选择字典序最小的节点 ID。
2. MUST 在 worker 下发之前校验 `callback_url` 与 `callback_secret`。
3. `preflight=true` 时，按字典序对互不相同的 `(agent, action)` 组合逐一探针。
   任意一次拒绝都以 `NOP-RESOURCE-INSUFFICIENT` 终止任务；此时不下发任何
   任务节点。
4. 合规模式（conformance mode）每次只按稳定拓扑顺序执行一个就绪节点，并发出
   规范事件 `task:running`、`<node>:attempt:<n>`、
   `<node>:completed|skipped|failed`、可选的
   `<node>:compensating|compensated|compensation_failed`，以及唯一一个终态
   `task:completed|failed|cancelled` 事件。

#### 3.5.2 条件、映射、重试与超时

- 节点的 `condition` 在该节点首次变为可运行时，针对已完成前驱结果的不可变
  快照，恰好求值一次。`false` 产生 `SKIPPED`；求值错误产生
  `NOP-CONDITION-EVAL-ERROR`，且不重试。
- `input_mapping` 在 condition 之后、首次下发之前解析。映射错误产生
  `NOP-INPUT-MAPPING-ERROR`，且不重试。
- `max_retries` 指首次尝试之后额外允许的尝试次数。每次尝试复用相同的
  `subtask_id` 与 `idempotency_key`。仅当仍有剩余尝试次数、worker 将该失败
  标记为可重试，且 `retry_on` 省略或包含该错误码时，失败才会被重试。
- 节点级截止时间被突破对应 `NOP-DELEGATE-TIMEOUT`，整体截止时间被突破对应
  `NOP-TASK-TIMEOUT`。一旦取消在终态竞争中胜出，就不再下发任何后续尝试或
  补偿，任务以 `NOP-TASK-CANCELLED` 结束。

#### 3.5.3 依赖与聚合规则

`SKIPPED` 满足依赖关系但不贡献任何结果。当 K 个前驱处于 `COMPLETED` 或
`SKIPPED` 时，K-of-N 屏障通过；一旦 K 已不可能达成，屏障立即失败。终点节点的
结果在聚合前按稳定拓扑顺序排列。因此 `merge` 在该顺序下应用「后者覆盖前者」；
`all` 保持该顺序；`fastest_k` 使用显式的完成顺序，并以节点 ID 作为平局裁决；
`weighted_first_k` 按数值型 `score` 降序排序，再按节点 ID 排序；`merge_all`
按拓扑顺序拼接数组，同时对非数组值应用「后者覆盖前者」。

#### 3.5.4 Saga 补偿

节点进入终态失败时，补偿候选集合是该失败节点的所有传递性已完成祖先节点。
候选按稳定拓扑顺序的逆序执行。`strict` 在下发任何补偿之前先校验每个候选都
具有 `compensate_action`，否则返回 `NOP-COMPENSATION-NOT-SUPPORTED`。
`best_effort` 下，缺少补偿操作的候选被跳过，且补偿失败不会中止后续补偿。
`strict` 下，第一次补偿失败即以 `NOP-COMPENSATION-FAILED` 终止 saga。补偿
使用稳定键 `<task_id>:<node_id>:compensate`。

#### 3.5.5 委托与 runner 租约

每一次委托尝试 MUST 通过 NDP 重新解析 `target_cluster_anchor`，并选出唯一的、
epoch 最高的存活 Anchor。下发之前 MUST 拒绝脑裂（split brain）、scope 扩大
以及目标缺失的情况，重试时 MUST 复用相同的幂等键。runner 租约续约仅在租约
存活且属主为同一 `runner_nid` 时才成功；已过期、已释放或已被回收的租约 MUST
返回 `NOP-CLAIM-CONFLICT`。规范的 `dedup_key` 是对 UTF-8 编码的 `task_id`、
一个 NUL 分隔字节、以及 UTF-8 编码的 `dag_hash` 计算 SHA-256 后得到的小写
十六进制串。

#### 3.5.6 alpha.19 replay、保留与聚合 Profile（NOP v0.10）

共享 transcript 位于
[`replay_retention_vectors.json`](conformance/nop/replay_retention_vectors.json)。

1. **NOP-P19-01 —— replay key。** replay key 为 `(caller_nid, task_id)`。
   Orchestrator 在 dispatch 前原子记录该 key 与覆盖 TaskFrame 不可变字段的 canonical digest。
2. **NOP-P19-02 —— 相同重复。** key 与 digest 相同的重复请求返回已记录的
   running/terminal outcome，MUST NOT 再次 dispatch、compensate 或 callback。
3. **NOP-P19-03 —— 冲突重复。** 同一 key 搭配不同 digest 时以
   `NOP-REPLAY-CONFLICT` 拒绝，且不得修改任一记录。
4. **NOP-P19-04 —— result TTL。** `result_ttl_seconds` 从原子 terminal-state
   commit 起用 monotonic clock 计时。`now >= expires_at` 时返回
   `NOP-TASK-RESULT-EXPIRED`，且不得泄露原 result。
5. **NOP-P19-05 —— replay tombstone。** result expiry 后，tombstone 继续保留实现
   声明的 `replay_tombstone_seconds`（最小 300、默认 3600、最大 86400）。
   tombstone 自身过期前，相同重复仍返回 expired，且不得重新 dispatch。
6. **NOP-P19-06 —— 有界 eviction。** 容量压力下先移除已过期 tombstone，再移除
   最旧 terminal record；绝不移除 running record。无安全 slot 时以
   `NOP-REPLAY-LIMIT` 拒绝准入。
7. **NOP-P19-07 —— terminal race。** 恰好一个 terminal state 胜出。迟到的 worker
   result、timeout callback 或 cancellation ack 只进入审计，不得替换结果或延长 TTL。
8. **NOP-P19-08 —— weighted selection。** `weighted_first_k` 只接受有限数值型
   `result.score`，按 score 降序、再按 node ID 排序，并恰好返回 `min_required` 项。
   缺失/非有限 score 为 `NOP-AGGREGATION-INVALID`。
9. **NOP-P19-09 —— merge-all。** `merge_all` 按稳定拓扑顺序访问成功结果；
   array/array 冲突执行拼接，其他冲突采用后值，且不得修改输入 object。
10. **NOP-P19-10 —— 部分输入。** failed result 永不参与 aggregate；`SKIPPED`
    继续满足既有 barrier 规则但不贡献值；K 已不可达时必须 terminal failure，不能部分成功。

---

### 3.6 生产运行时结果绑定（NOP v0.11）

选择 nps.production_runtime revision 1 后，本节细化重试、取消与终态证据；
未选择时保持基础 profile 行为。

1. **NOP-P20-01 — 独立预算。** 既有 max_retries 计编排额外尝试，
   NCP runtime attempts 包含首次尝试。两种预算及原任务剩余期限同时约束，
   嵌套重试层不得隐式放大次数。Worker retryable 或超时不证明未派发；
   每次重试还须通过 NCP §7.8 确定性/幂等门槛。
2. **NOP-P20-02 — 取消确定性。** 断开/取消要求协作停止，不能回滚已提交或外部效果。
   §5.2 的 stop 要求是尽可能停止后续工作，不是假承诺回滚。
   没有权威终态证据时暴露 indeterminate，不宣称 mutation 未执行。
   取消、超时和迟到结果仍按 §3.5.6 只提交一个本地终态。
3. **NOP-P20-03 — 终态线证据。** 本协商 profile 的 final AlignStream 必须包含
   顶层 execution_outcome，只能为 not_dispatched、known_terminal、indeterminate；
   非 final 帧禁止此字段。Indeterminate 必须携带
   error.code=NOP-EXECUTION-INDETERMINATE 且 error.retryable=false。
   缺失、非法或矛盾证据是协议故障，不证明可安全重放。未收到远端终帧时，
   本地运行时自行记录 indeterminate。
4. **NOP-P20-04 — 父任务与 callback。** 保留既有父任务 FAILED/CANCELLED/COMPLETED
   枚举。本 profile 的结果/callback 对象另含 execution_outcome；
   任一已开始 mutation 未决即为 indeterminate。全部未派发则为 not_dispatched，
   否则全部结果已知时为 known_terminal。只提交一个终态记录和一个 callback intent；
   传递重试不能启动 worker、补偿或新终态决策。迟到结果仅作审计证据，
   不是第二次 callback 决策。
5. **NOP-P20-05 — 持久重放声明。** 既有 replay cache/tombstone 不证明持久操作去重。
   声明该契约须具备跨重启/副本的原子 principal/key/digest 记录、明确保留期与
   owner 范围，且承诺到期前不驱逐。容量不能保全承诺时拒绝准入。
   缺少证明时不得重放未知 mutation，包括 Anchor 故障转移后。
6. **NOP-P20-06 — 补偿。** 前驱效果未知时不得盲补偿，也不得计为完成输入。
   补偿本身受确定性、身份、期限及幂等规则约束。任务失败或取消不意味着外部效果全撤销。

---

## 4. 资源预检（Pre-flight）

当 TaskFrame `preflight: true` 时，Orchestrator 在正式执行 DAG 前，向各 Worker Agent 发送轻量级探针，确认资源可用性。

### 4.1 预检流程

```
Orchestrator                        Worker Agent(s)
  │                                       │
  │── DelegateFrame(action="preflight") → │  探针请求（每个 DAG 节点对应的 Agent）
  │  ←── CapsFrame(preflight result) ─── │  回报可用性
  │                                       │
  │  所有 Agent 可用 → 正式执行           │
  │  任意 Agent 不可用 → 中止并返回错误   │
```

### 4.2 预检请求（DelegateFrame 扩展）

`action="preflight"` 时，`params` 包含：

```json
{
  "cgn_est": 1500,
  "required_capabilities": ["nwp:invoke", "ml:inference"],
  "action": "nwp://ml.example.com/inference/invoke"
}
```

### 4.3 预检响应（CapsFrame）

```json
{
  "frame": "0x04",
  "anchor_ref": "nps:system:preflight:result",
  "count": 1,
  "data": [{
    "agent_nid": "urn:nps:agent:...:analyzer",
    "available": true,
    "available_npt": 8000,
    "estimated_queue_ms": 200,
    "capabilities": ["nwp:invoke", "ml:inference"]
  }]
}
```

若 `available: false`，Orchestrator MUST 中止整个 TaskFrame 并返回 `NOP-RESOURCE-INSUFFICIENT`。

---

## 5. 任务执行状态机

```
              ┌──────────┐
              │ PENDING  │  TaskFrame 已提交，等待调度
              └────┬─────┘
                   │ 开始调度
                   ↓
           ┌──────────────┐
           │  PREFLIGHT   │  资源预检中（preflight=true 时）
           └──────┬───────┘
                  │ 预检通过
                  ↓
              ┌──────────┐
              │ RUNNING  │  子任务执行中
              └────┬─────┘
                   │
         ┌─────────┼─────────┐
         ↓         ↓         ↓
  ┌────────────┐ ┌──────┐ ┌──────────┐
  │WAITING_SYNC│ │FAILED│ │CANCELLED │
  │（同步屏障）│ └──┬───┘ └──────────┘
  └──────┬─────┘   │ 超过 max_retries
         │依赖完成  │ Orchestrator 收到 FAILED 通知
         ↓         ↓
    ┌───────────┐
    │ COMPLETED │
    └───────────┘
```

子任务状态：`PENDING` → `RUNNING` → `COMPLETED` / `FAILED` / `CANCELLED` / `SKIPPED`

当下游失败触发 saga 回滚时，已 `COMPLETED` 的子任务 MAY 进入补偿生命周期（详见 §3.1.6）：

```
COMPLETED ──→ COMPENSATING ──→ COMPENSATED          （回滚成功）
                  │
                  └────────→ COMPENSATION_FAILED   （终态——补偿操作返回错误）
```

| 状态 | 含义 |
|------|------|
| `COMPENSATING` | 节点的 `compensate_action` 已下发，等待结果 |
| `COMPENSATED` | 节点已成功反转 |
| `COMPENSATION_FAILED` | 终态——`compensate_action` 返回错误且未能成功重试 |

`COMPENSATED` 与 `COMPENSATION_FAILED` 是 saga 终态；它们 NOT 改变父 TaskFrame 的最终结果（一旦原始失败触发 saga，TaskFrame 仍为 `FAILED`），但 MUST 在最终回调载荷中报告，方便调用方审计哪些副作用已被反转。

### 5.1 重试语义

- 按节点 `retry_policy` 策略执行（默认指数退避）
- 重试时 MUST 使用相同的 `subtask_id` 和 `idempotency_key`
- 超过 `max_retries` 后，标记本子任务为 FAILED
- 子任务 FAILED 是否导致整体 FAILED，取决于 SyncFrame `min_required`：
  - 若失败数 > N - min_required，整体 FAILED
  - 否则（K-of-N 已满足），整体继续执行

### 5.2 任务取消

任意时刻，Orchestrator 可通过以下方式取消任务：

1. **直接断开连接**：Worker Agent MUST 检测到连接断开并停止执行
2. **发送取消 DelegateFrame**：`action="cancel"`, `params: { "task_id": "...", "subtask_id": "..." }`
3. **调用 NWP `system.task.cancel`**（若节点支持）

Worker Agent 收到取消信号后，MUST 停止执行并返回 `AlignStream(is_final=true, error.code="NOP-TASK-CANCELLED")`。

---

## 6. 多 Agent 协作完整流程

```
Orchestrator                              Worker B（数据）  Worker C（推理）
  │                                           │                   │
  │── TaskFrame(preflight=true) ─────────→   │                   │
  │      DelegateFrame(preflight) ─────────→ │                   │
  │      DelegateFrame(preflight) ────────────────────────────→  │
  │  ←── CapsFrame(available=true) ───────── │                   │
  │  ←── CapsFrame(available=true) ────────────────────────────  │
  │                                           │                   │
  │── DelegateFrame(fetch-data) ───────────→ │                   │
  │      Worker B 调用 NWP QueryFrame         │                   │
  │  ←── AlignStream(seq=0, data=products) ─ │                   │
  │  ←── AlignStream(is_final=true) ──────── │                   │
  │                                           │                   │
  │── DelegateFrame(analyze, products) ───────────────────────→  │
  │      Worker C 调用推理节点                                      │
  │  ←── AlignStream(seq=0, progress=0.5) ─────────────────────  │
  │  ←── AlignStream(is_final=true, result) ───────────────────  │
  │                                           │                   │
  │── SyncFrame(wait_for=[fetch,analyze])     │                   │
  │   sync 通过，aggregated result 已就绪     │                   │
  │                                           │                   │
  │  → callback_url POST（任务完成通知）      │                   │
```

---

## 7. 错误码

| 错误码 | NPS 状态码 | 描述 |
|--------|-----------|------|
| `NOP-EXECUTION-INDETERMINATE` | `NPS-SERVER-INTERNAL` | 执行结果不确定，不可自动重试 |
| `NOP-TASK-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | task_id 不存在 |
| `NOP-TASK-TIMEOUT` | `NPS-SERVER-TIMEOUT` | 整体任务超时 |
| `NOP-TASK-DAG-INVALID` | `NPS-CLIENT-BAD-FRAME` | DAG 格式不合法（缺少起点/终点、字段错误等）|
| `NOP-TASK-DAG-CYCLE` | `NPS-CLIENT-BAD-FRAME` | DAG 中存在环路 |
| `NOP-TASK-DAG-TOO-LARGE` | `NPS-CLIENT-BAD-FRAME` | DAG 节点数超过上限（默认 32）|
| `NOP-TASK-ALREADY-COMPLETED` | `NPS-CLIENT-CONFLICT` | 任务已完成，无法重复提交 |
| `NOP-TASK-CANCELLED` | `NPS-CLIENT-CONFLICT` | 任务已被取消 |
| `NOP-DELEGATE-SCOPE-VIOLATION` | `NPS-AUTH-FORBIDDEN` | delegated_scope 超出父 Agent scope |
| `NOP-DELEGATE-REJECTED` | `NPS-CLIENT-UNPROCESSABLE` | Worker Agent 拒绝接受委托（能力不足或超负载）|
| `NOP-DELEGATE-CHAIN-TOO-DEEP` | `NPS-CLIENT-BAD-PARAM` | 委托链深度超过 3 层上限（按实体计数：Orchestrator=1、Worker=2、Sub-Worker=3；第 4 个实体触发此错误；最多允许 2 次 `DelegateFrame` 跳转，见 §3.1.1）|
| `NOP-DELEGATE-TIMEOUT` | `NPS-SERVER-TIMEOUT` | 子任务在 deadline_at 前未完成 |
| `NOP-SYNC-TIMEOUT` | `NPS-SERVER-TIMEOUT` | SyncFrame 等待依赖任务超时 |
| `NOP-SYNC-DEPENDENCY-FAILED` | `NPS-CLIENT-UNPROCESSABLE` | 等待的依赖子任务已失败（且失败数超出 K-of-N 容忍范围）|
| `NOP-STREAM-SEQ-GAP` | `NPS-STREAM-SEQ-GAP` | AlignStream 序号不连续 |
| `NOP-STREAM-NID-MISMATCH` | `NPS-AUTH-UNAUTHENTICATED` | AlignStream sender_nid 与连接身份不一致 |
| `NOP-RESOURCE-INSUFFICIENT` | `NPS-SERVER-UNAVAILABLE` | 预检发现一个或多个 Worker Agent 资源不足（CGN / capability）|
| `NOP-CONDITION-EVAL-ERROR` | `NPS-CLIENT-BAD-PARAM` | DAG 节点 condition 表达式求值失败（语法错误或引用了不存在的字段）|
| `NOP-INPUT-MAPPING-ERROR` | `NPS-CLIENT-UNPROCESSABLE` | input_mapping JSONPath 无法解析或目标字段不存在 |
| `NOP-COMPENSATION-FAILED` | `NPS-CLIENT-UNPROCESSABLE` | 终态——saga 回滚过程中某节点的 `compensate_action` 返回错误（见 §3.1.6）|
| `NOP-COMPENSATION-NOT-SUPPORTED` | `NPS-CLIENT-UNPROCESSABLE` | 终态——存在需要补偿的前驱缺少 `compensate_action` 且 `compensation_policy="strict"` |
| `NOP-CALLBACK-HMAC-MISSING` | `NPS-AUTH-UNAUTHENTICATED` | callback 接收方拒绝投递，因为缺少 `X-NPS-Signature` header；`callback_secret` 已设置但签名未计算 |
| `NOP-CALLBACK-INVALID` | `NPS-CLIENT-BAD-PARAM` | callback URL 未通过 scheme、user-info、DNS、公网地址或重定向校验 |
| `NOP-CALLBACK-HMAC-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | callback HMAC 格式非法，或与原始 body 的精确比对不匹配 |
| `NOP-TASK-RESULT-EXPIRED` | `NPS-CLIENT-NOT-FOUND` | `result_ttl_seconds` 已过后才请求任务结果；结果不再保留 |
| `NOP-REPLAY-CONFLICT` | `NPS-CLIENT-CONFLICT` | 同一 `(caller_nid, task_id)` 被不同的 TaskFrame 不可变内容复用 |
| `NOP-REPLAY-LIMIT` | `NPS-LIMIT-RESOURCE` | replay ledger 容量耗尽，且没有可安全移除的过期 tombstone 或 terminal record |
| `NOP-AGGREGATION-INVALID` | `NPS-CLIENT-BAD-PARAM` | 聚合输入违反所选 strategy，例如 `weighted_first_k` 缺少有限数值 score |
| `NOP-STREAM-NAK-UNRESOLVABLE` | `NPS-STREAM-SEQ-GAP` | NAK 请求重传的帧已不在发送方缓冲区中（该帧已被淘汰）|
| `NOP-CLAIM-CONFLICT` | `NPS-CLIENT-CONFLICT` | TaskFrame 已被一个存活的 runner 租约认领（NPS-CR-0007 §4.2）|
| `NOP-SPAWN-SPEC-INVALID` | `NPS-CLIENT-BAD-PARAM` | `spawn_spec_ref` 无法解析，或未通过 SpawnSpec schema 校验（NPS-CR-0007 §5）|
| `NOP-RUNTIME-IDLE-TIMEOUT` | `NPS-SERVER-TIMEOUT` | L3 worker 在完成该节点前超过了 idle timeout（NPS-CR-0007 §6）|
| `NOP-RUNTIME-MAX-RUNTIME` | `NPS-SERVER-TIMEOUT` | L3 worker 在完成该节点前超过了 max runtime（NPS-CR-0007 §6）|

---

## 8. L3 运行时集成（nps-runner）

> 规范性契约：**NPS-CR-0007**（`cr/NPS-CR-0007-nop-l3-runtime-integration.md`）。
> 本节是 spec 内摘要；完整字段表与合规套件以该 CR 为准。

NPS-Node Profile **L3（按需 / FaaS）** 层级只在任务到达时才具现化 Agent 进程。承担这件事的运行时是
`nps-runner` daemon。本节定义 `nps-runner` 与 NOP 编排层之间的规范性接口。

### 8.1 任务认领协议

runner 原子地租约（lease）某个 per-NID 收件箱的队首任务：`{ task_id, runner_nid, lease_seconds
（钳制在 [10,600]）, dedup_key = sha256(task_id ‖ dag_hash) }`。结果分三种：

- **Granted** —— 收件箱把该任务标记为 `LEASED (runner_nid, lease_expiry)`；节点运行期间 runner MUST 在
  租约到期前续约。一次续约把 `lease_expiry` 延长 `lease_seconds`，MUST 携带相同的 `runner_nid`；若续约在租约已过期并被回收之后才到达，MUST 以 `NOP-CLAIM-CONFLICT` 拒绝。
- **Conflict** —— 已存在存活租约 ⇒ `NOP-CLAIM-CONFLICT`；runner 退避重试。
- **Reclaim** —— 已过期的租约可被回收；`dedup_key` 保证已处于终态、带副作用的节点**不会**被重复执行
  （at-least-once + 去重）。

结果上报是幂等的，以 `(task_id, node_id, dedup_key)` 为键。

### 8.2 `spawn_spec_ref` 内容 schema

`spawn_spec_ref`（NDP AnnounceFrame，NPS-4 §3.1）解析为一个 **SpawnSpec**：
`{ image（OCI ref，必填）, command?, env?, resource_limits? {cpu, memory, cgn_budget},
idle_timeout_seconds?, max_runtime_seconds? }`。它 MAY 是内联的 `spawnspec:` base64url-JSON
data URI，也 MAY 是 `https://` / `nwp://` URL，其响应体 MUST 通过该 schema 校验。解析失败 ⇒
`NOP-SPAWN-SPEC-INVALID`。

对于远程引用，runner MUST 对初始 URL 以及每一次重定向应用 §9.4 中的目标地址规则。DNS 解析
MUST fail closed —— 除非返回的每一个地址都是公网地址；传输层 MUST 连接到其中一个已校验的
地址，同时保留原始主机名用于 TLS SNI 与证书校验。实现 MUST 强制有限的重定向次数、响应体
大小与请求时长上限。

### 8.3 生命周期强制

| 限制 | 优先级（高→低）| 违反时的终态 |
|------|---------------|-------------|
| Idle timeout | SpawnSpec `idle_timeout_seconds` → runner 策略 | `NOP-RUNTIME-IDLE-TIMEOUT` |
| Max runtime | SpawnSpec `max_runtime_seconds` → runner 策略 | `NOP-RUNTIME-MAX-RUNTIME` |

出现任一终态条件时，runner 以 PATCH 把节点状态写回 Orchestrator 的任务存储：`done → COMPLETED`，
`failed` / idle / max-runtime → `FAILED`。带 `compensate_action` 的 `FAILED` L3 节点触发与 L1/L2
相同的逆拓扑 saga 回滚（§3.1.6）；L3 不引入新的 saga 路径。

### 8.4 合规性

L3 部署由 `services/conformance/NPS-Node-L3.md`（`TC-N3-*`）测试：认领冲突、租约回收 + 去重、
spawn-spec 解析、idle / max-runtime 违反、3 节点 DAG 端到端，以及 L3 saga 触发。

---

## 9. 安全考量

### 9.1 任务注入防御
Orchestrator MUST 验证接收到的 TaskFrame 来自可信 NID（通过 NIP 证书验证）。Worker Agent SHOULD 只接受已通过 NIP 验证的 DelegateFrame，拒绝未认证或 scope 不匹配的委托请求。

### 9.2 DAG 资源限制
实现 MUST 限制：
- DAG 最大节点数：**32**
- 最大委托链深度：**3 层**
- `condition` 表达式长度：**≤ 512 字符**
- `input_mapping` JSONPath 嵌套深度：**≤ 8 层**

### 9.3 审计追踪
每个 DelegateFrame 的执行记录 SHOULD 写入审计日志，包含：

- `sender_nid`（Orchestrator）
- `target_agent_nid`（Worker）
- `subtask_id`
- `parent_task_id`
- `timestamp`
- `trace_id`（来自 context，用于与 OpenTelemetry 系统关联）

### 9.4 callback_url 防滥用
- TaskFrame `callback_url` MUST 为 `https://` 前缀
- Orchestrator MUST 拒绝带 user-info 的 URL，以及 loopback、私有地址、链路本地、
  未指定地址、组播或其他任何非公网的主机。
- 投递前 MUST 先解析主机名。返回的每一个 A/AAAA 地址都 MUST 通过同样的公网地址
  检查，且 HTTP 传输层 MUST 连接到其中一个已校验的地址，同时保留原始主机名用于
  TLS SNI 与证书校验。重定向 MUST 重复完整的校验流程，且 MUST NOT 复用未经校验
  的目标地址。
- 存在 `callback_secret` 时，发送方 MUST 对原始 body 逐字节签名，接收方 MUST 以
  常数时间比较小写形式的 `sha256=<hex>` HMAC。签名缺失、格式非法或不匹配时一律
  fail closed。
- 回调推送失败时 SHOULD 指数退避重试（最多 3 次），之后放弃并记录日志

### 9.5 Scope 委托链安全
每一层委托都必须通过 NIP CA 验证 `delegated_scope` 不超出父 scope。不得绕过 CA 直接委托超出权限的子任务。

---

## 10. 变更历史

| 版本 | 日期 | 变更 |
|------|------|------|
| 0.11 | 2026-09-09 | Alpha.20 生产运行时结果绑定：独立重试预算、取消确定性、AlignStream/callback execution_outcome、持久重放声明和保守补偿；新增 NOP-EXECUTION-INDETERMINATE。 |
| 0.10 | 2026-08-31 | alpha.19 有界 replay/retention Profile：caller-bound replay key 与 digest、重复/冲突行为、monotonic result TTL、有界 replay tombstone 与安全 eviction、唯一 terminal winner，以及确定性的 `weighted_first_k` / `merge_all` / 部分输入语义。新增三个错误码与共享 transcript；Depends-On 推进到 alpha.19 NCP/NWP/NIP/NDP 基线。|
| 0.9 | 2026-07-29 | **Alpha.17 可移植 Orchestrator profile**：确定性的 DAG 预检与合规性调度；condition / input_mapping 单次求值规则；重试、超时、取消、K-of-N 与聚合顺序；逆拓扑的 saga 上报；fail-closed 的 callback DNS/SSRF 与 HMAC 校验；每次尝试都重新解析 Anchor；严格的「存活 + 同属主」租约续约与规范 dedup key。新增共享的编排与运行时/安全 transcript；在六个 SDK 与 runner 关卡通过后，将 NPS-CR-0007 提升为 Implemented。 |
| 0.8 | 2026-07-05 | 多 Anchor 高可用交互（NPS-CR-0009）：`DelegateFrame.target_cluster_anchor` MUST 解析到目标集群当前活跃的 Anchor（`cluster_epoch` 最高者，NDP §9）；发生 `anchor_failover` 时，在途委托 MUST 在重试前重新解析到 `successor_nid`。租约续约语义正式化（§8）：一次续约延长 `lease_expiry`，MUST 匹配 `runner_nid`；若租约已过期并被回收，则以 `NOP-CLAIM-CONFLICT` 拒绝。无新增错误码。 |
| 0.7 | 2026-06-12 | **NPS-CR-0007 —— NOP↔L3 运行时集成**：新增 §8（任务认领协议：原子租约 + `dedup_key`、`NOP-CLAIM-CONFLICT`；`spawn_spec_ref` SpawnSpec 内容模式；idle/max-runtime 生命周期强制；幂等结果上报）；4 个新错误码（`NOP-CLAIM-CONFLICT`、`NOP-SPAWN-SPEC-INVALID`、`NOP-RUNTIME-IDLE-TIMEOUT`、`NOP-RUNTIME-MAX-RUNTIME`）；新增 `services/conformance/NPS-Node-L3.md`（`TC-N3-*`）；Security/Changelog 重编号为 §9/§10。gate `nps-runner` L3 FaaS 运行时。 |
| 0.7 | 2026-06-03 | TaskFrame 新增 `result_ttl_seconds`（uint32，默认 3600）—— TTL 过期后返回 `NOP-TASK-RESULT-EXPIRED`；新增 `NOP-STREAM-NAK-UNRESOLVABLE` 错误码，用于帧已被淘汰时的 NAK 重传 |
| 0.6 | 2026-05-31 | TaskFrame 新增 `callback_secret`（HMAC-SHA256 key）—— webhook 回调携带 `X-NPS-Signature` header，`NOP-CALLBACK-HMAC-MISSING`；DelegateFrame 新增 `target_cluster_anchor` 用于跨集群路由；SyncFrame 新增 `weighted_first_k` / `merge_all` 聚合策略（§3.3.2）；AlignStream 新增 `ack_seq` / `nak_seq` 滑动窗口 ACK（§3.4.2） |
| 0.5 | 2026-05-10 | 补偿/Saga 语义（issue #34）：节点级 `compensate_action` / `compensate_params_mapping`；TaskFrame 级 `compensation_policy`（默认 `best_effort` / 可选 `strict`）；子任务 saga 状态 `COMPENSATING` / `COMPENSATED` / `COMPENSATION_FAILED`；§3.1.6 saga 触发逻辑（下游 FAILED 时按逆拓扑顺序补偿已完成前驱）；2 个新错误码：`NOP-COMPENSATION-FAILED`、`NOP-COMPENSATION-NOT-SUPPORTED` |
| 0.4 | 2026-04-19 | Status / Depends-On 版本号同步；与 NCP v0.7 / NWP v0.13 / NIP v0.9 文字对齐 |
| 0.3 | 2026-04-14 | DAG 节点粒度增强（per-node timeout/retry_policy/condition/input_mapping）；§3.1.2 context 字段支持 OpenTelemetry W3C Trace（trace_id/span_id/trace_flags/baggage）；§3.1.3 input_mapping JSONPath 映射；§3.1.4 retry_policy（fixed/linear/exponential）；§3.1.5 condition CEL 子集；DelegateFrame 新增 idempotency_key/priority/context/node_id；SyncFrame 新增 min_required（K-of-N 语义）和 §3.3.1/§3.3.2 聚合策略；AlignStream 新增 subtask_id/error 字段，§3.4.1 Token 级背压；§4 资源预检（preflight）协议；§5 扩展状态机（PREFLIGHT/SKIPPED）和任务取消机制；§6 完整多 Agent 流程图；5 个新错误码（RESOURCE-INSUFFICIENT、CONDITION-EVAL-ERROR、INPUT-MAPPING-ERROR、DELEGATE-TIMEOUT、TASK-CANCELLED）；§8.4 callback_url 防滥用；Depends-On 更新至 NCP v0.7 / NWP v0.13 |
| 0.2 | 2026-04-12 | 统一端口 17433；错误码改用 NPS 状态码映射；完善错误码列表 |
| 0.1 | 2026-04-10 | 初始规范：TaskFrame/DelegateFrame/SyncFrame/AlignStream，DAG 执行模型，替代 NCP AlignFrame |

---

*归属：LabAcacia / INNO LOTUS PTY LTD · Apache 2.0*
