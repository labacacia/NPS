[English Version](./dag-orchestration.en.md) | 中文版

# DAG 编排

NOP（Neural Orchestration Protocol）使用**有向无环图（DAG）**描述多 Agent 任务，编排器负责按拓扑序调度各节点、传递中间结果并聚合最终输出。

---

## 核心概念

### 任务（Task）

一次完整的多 Agent 协作请求，由 `TaskFrame` 描述，包含：

- **DAG 定义**：节点集合 + 有向边集合
- **全局配置**：超时、重试策略、优先级、回调 URL
- **委托深度**：`DelegateDepth`（根任务为 0，转发时递增）

### 节点（DagNode）

DAG 的每个顶点对应一个 Agent 执行单元，包含：

- `action`：目标操作（`nwp://` URL）
- `agent`：执行该操作的 Worker NID
- `input_from`：上游节点 ID 列表（显式依赖）
- `input_mapping`：从上游结果提取参数的 JSONPath 映射
- `condition`：CEL 表达式，为 `false` 时跳过该节点
- `retry_policy`：独立重试策略（覆盖全局 `MaxRetries`）

### 边（DagEdge）

有向边 `From → To` 声明执行顺序依赖，不含数据传递语义——数据传递通过 `InputFrom` + `InputMapping` 控制。

---

## 执行流程

```
TaskFrame
    │
    ▼
DAG 校验（DagValidator）
  - 节点数 ≤ 32
  - 无环
  - 拓扑排序
    │
    ▼
预检（可选，Preflight）
  - 对每个 Agent 发送预检探测
  - 评估资源可用性
    │
    ▼
按拓扑序并发执行
  ┌─────────────────────┐
  │  对每个就绪节点：      │
  │  1. 求值条件表达式    │
  │  2. 执行输入映射      │
  │  3. 发送 DelegateFrame│
  │  4. 接收 AlignStream  │
  │  5. 重试（失败时）    │
  └─────────────────────┘
    │
    ▼
等待 SyncFrame（若存在）
    │
    ▼
聚合终止节点结果（AggregateStrategy）
    │
    ▼
NopTaskResult（Completed | Failed | Cancelled）
    │
    ▼
回调 POST（fire-and-forget，最多重试 3 次）
```

---

## 输入映射

`InputMapping` 是 `{ 参数名 → JSONPath }` 字典，将上游节点结果的字段提取为当前节点的输入参数：

```json
{
  "userId":  "$.fetch.result.user_id",
  "orderId": "$.enrich.result.order_id"
}
```

`$` 根路径后的第一段是上游节点 ID，后续段是 JSON 字段路径。`NopInputMapper.BuildParams` 在运行时解析并构建 `params` 对象。

---

## 条件求值

`DagNode.Condition` 是 CEL 子集表达式，上下文为已完成节点的结果集：

```
// 仅当 fetch 节点有数据时执行
"fetch.count > 0"

// 多条件
"fetch.status == 'active' && enrich.score >= 0.8"

// 成员检查
"fetch.category in ['electronics', 'books']"
```

条件为 `false` 时，该节点状态置为 `Skipped`，其下游依赖将跳过等待（视作成功）。

---

## K-of-N 同步

`SyncFrame.MinRequired` 定义最少需要成功完成的前置任务数。`0` 表示全部需要成功（默认）。

```csharp
// 3 个数据源，任意 2 个成功即可继续
new DagNode
{
    Id          = "aggregate",
    InputFrom   = ["source1", "source2", "source3"],
    MinRequired = 2,
}
```

`AggregateStrategy.FastestK` 可配合 K-of-N 使用，取最先完成的 K 个结果。

---

## 委托链深度

NOP 支持嵌套编排——Worker Agent 本身可以是另一个编排器，接收并转发 TaskFrame：

```
Client
 └─▶ Orchestrator A  (depth=0)
      └─▶ Worker B / Orchestrator B  (depth=1)
           └─▶ Worker C             (depth=2)
                └─▶ 拒绝 (depth=3 ≥ MaxDelegateChainDepth)
```

`NopConstants.MaxDelegateChainDepth = 3`，超过时返回 `NOP-DELEGATE-CHAIN-TOO-DEEP`，防止无限递归委托。

---

## 回调机制

任务完成（成功或失败）后，编排器以 fire-and-forget 方式 POST `callback_url`，携带 `NopTaskResult` 的 JSON 序列化：

- **指数退避**：延迟 = `CallbackRetryBaseDelayMs × 2^(attempt-1)`（默认：0s、1s、2s）
- **最大重试**：`CallbackMaxRetries = 3`
- **HMAC 签名**：设置 `callback_secret` 时，`X-NPS-Signature: sha256=<hex>` 覆盖原始 callback JSON body
- **回调失败**：不影响任务本身的 `FinalState`（非致命错误）

`callback_url` 校验规则（§8.4）：
- 必须为 `https://` scheme
- 拒绝私有/环回地址（SSRF 防护）

---

## 聚合策略

| 策略 | 行为 |
|------|------|
| `merge` | 将所有结果合并为单个 JSON 对象（最后写入胜出）|
| `first` | 返回第一个成功完成节点的结果 |
| `all` | 以 JSON 数组形式返回所有结果 |
| `fastest_k` | 返回最先完成的 K 个结果的数组 |

---

## 错误处理

| 场景 | 行为 |
|------|------|
| 节点失败（可重试） | 按 `RetryPolicy` 指数退避重试 |
| 节点失败（不可重试） | 状态 → `Failed`；终止节点失败时整个任务失败 |
| 条件为 `false` | 节点状态 → `Skipped`；下游继续执行 |
| 超时 | 状态 → `Failed`，错误码 `NOP-TASK-TIMEOUT` |
| 取消 | 状态 → `Cancelled`，正在执行的子任务收到取消信号 |

---

## 相关文档

- [NPS.NOP API 参考](../sdk/dotnet/nps-nop.md)
- [NOP 规范](../../spec/NPS-5-NOP.md)
