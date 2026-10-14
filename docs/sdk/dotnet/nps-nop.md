[English Version](./nps-nop.en.md) | 中文版

# NPS.NOP API 参考

`NPS.NOP` 提供多 Agent DAG 编排引擎：任务定义、子任务委托、K-of-N 同步、条件求值、输入映射、结果聚合、回调通知，以及 §8.2 委托链深度限制和 §8.4 SSRF 防护。

---

## DI 注册

```csharp
builder.Services.AddNopOrchestrator(opt =>
{
    opt.MaxConcurrentNodes       = Environment.ProcessorCount * 2;
    opt.ValidateSenderNid        = true;
    opt.EnableCallback           = true;
    opt.CallbackTimeoutMs        = 10_000;
    opt.CallbackRetryBaseDelayMs = 1000;
    opt.DefaultAggregateStrategy = "merge";
}, useInMemoryStore: true);
// 注册：INopOrchestrator(singleton NopOrchestrator)、INopTaskStore(singleton InMemoryNopTaskStore)
```

---

## NopOrchestratorOptions

命名空间：`NPS.NOP.Orchestration`

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `MaxConcurrentNodes` | `int` | `ProcessorCount × 2` | 单任务最大并发节点数 |
| `ValidateSenderNid` | `bool` | `true` | 是否校验 `AlignStreamFrame.SenderNid` 与 DAG 节点 Agent NID 一致 |
| `EnableCallback` | `bool` | `true` | 是否在任务完成后 POST 回调 URL（fire-and-forget）|
| `CallbackTimeoutMs` | `int` | `10000` | 回调 HTTP 请求超时（毫秒）|
| `CallbackRetryBaseDelayMs` | `int` | `1000` | 回调指数退避基础延迟（毫秒）；测试时设为 0 |
| `DefaultAggregateStrategy` | `string` | `"merge"` | 无 SyncFrame 时终止节点的默认聚合策略 |

---

## NOP 帧类型

命名空间：`NPS.NOP.Frames`

### TaskFrame

任务定义帧，提交给编排器启动一个 DAG 任务：

```csharp
public sealed record TaskFrame : IFrame
{
    public required string       TaskId        { get; init; } // UUID v4
    public required TaskDag      Dag           { get; init; }
    public          uint         TimeoutMs     { get; init; } = 30_000; // 最大 3 600 000
    public          byte         MaxRetries    { get; init; } = 2;
    public          string       Priority      { get; init; } = "normal" // "low"|"normal"|"high"
    public          string?      CallbackUrl   { get; init; } // 必须 https://（SSRF 防护）
    public          string?      CallbackSecret { get; init; } // base64url 32 字节 HMAC key
    public          bool         Preflight     { get; init; }
    public          TaskContext? Context       { get; init; }
    public          string?      RequestId     { get; init; }
    public          int          DelegateDepth { get; init; } // 0 = 根任务；≥ 3 时被拒绝
    public          string       CompensationPolicy { get; init; } = "best_effort"; // "best_effort"|"strict"
}
```

---

### DelegateFrame

子任务委托帧，编排器发给 Worker Agent：

```csharp
public sealed record DelegateFrame : IFrame
{
    public required string       ParentTaskId    { get; init; }
    public required string       SubtaskId       { get; init; }
    public required string       NodeId          { get; init; }
    public required string       TargetAgentNid  { get; init; }
    public required string       Action          { get; init; } // "nwp://..." | "preflight" | "cancel"
    public          JsonElement? Params           { get; init; }
    public required JsonElement  DelegatedScope   { get; init; }
    public required string       DeadlineAt       { get; init; } // ISO 8601 UTC
    public          string?      IdempotencyKey   { get; init; }
    public          string?      Priority         { get; init; }
    public          TaskContext? Context          { get; init; }
    public          int          DelegateDepth    { get; init; } // = 父任务 DelegateDepth + 1
}
```

---

### SyncFrame

同步屏障帧，等待多个子任务完成（支持 K-of-N）：

```csharp
public sealed record SyncFrame : IFrame
{
    public required string                  TaskId      { get; init; }
    public required string                  SyncId      { get; init; }
    public required IReadOnlyList<string>   WaitFor     { get; init; } // 等待的子任务 ID 列表
    public          uint                    MinRequired { get; init; } // K-of-N；0 = 全部
    public          string                  Aggregate   { get; init; } = "merge"
    public          uint?                   TimeoutMs   { get; init; }
}
```

---

### AlignStreamFrame

有向任务流帧，Worker 返回给编排器的中间/最终结果：

```csharp
public sealed record AlignStreamFrame : IFrame
{
    public required string       StreamId  { get; init; }
    public required string       TaskId    { get; init; }
    public required string       SubtaskId { get; init; }
    public required ulong        Seq       { get; init; } // 严格单调递增，0-based
    public          string?      PayloadRef { get; init; }
    public          JsonElement? Data       { get; init; }
    public          uint?        WindowSize { get; init; }
    public required bool         IsFinal   { get; init; }
    public required string       SenderNid { get; init; }
    public          StreamError? Error     { get; init; } // IsFinal=true 且失败时非 null
}
```

---

## DAG 模型

命名空间：`NPS.NOP.Models`

### TaskDag / DagNode / DagEdge

```csharp
public sealed record TaskDag
{
    public required IReadOnlyList<DagNode> Nodes { get; init; }
    public required IReadOnlyList<DagEdge> Edges { get; init; }
}

public sealed record DagNode
{
    public required string                                   Id           { get; init; }
    public required string                                   Action       { get; init; } // nwp:// URL
    public required string                                   Agent        { get; init; } // Worker NID
    public          IReadOnlyList<string>?                   InputFrom    { get; init; } // 依赖节点 ID
    public          IReadOnlyDictionary<string, JsonElement>? InputMapping { get; init; } // JSONPath 映射
    public          uint?                                    TimeoutMs    { get; init; }
    public          RetryPolicy?                             RetryPolicy  { get; init; }
    public          string?                                  Condition    { get; init; } // CEL 条件表达式
    public          uint                                     MinRequired  { get; init; } // K-of-N
}

public sealed record DagEdge(string From, string To);
```

### RetryPolicy

```csharp
public sealed record RetryPolicy
{
    public byte                    MaxRetries     { get; init; }
    public string                  Backoff        { get; init; } = "exponential"
    public uint                    InitialDelayMs { get; init; } = 1000;
    public uint                    MaxDelayMs     { get; init; } = 30_000;
    public IReadOnlyList<string>?  RetryOn        { get; init; } // 可重试的错误码列表

    // 计算第 attempt 次重试的延迟（毫秒）
    uint ComputeDelayMs(int attempt)
}
```

退避策略（`BackoffStrategy`）：`"fixed"`、`"linear"`、`"exponential"`。

### TaskContext

```csharp
public sealed record TaskContext
{
    public string?                                SessionId  { get; init; }
    public string?                                TraceId    { get; init; }
    public string?                                SpanId     { get; init; }
    public byte?                                  TraceFlags { get; init; }
    public IReadOnlyDictionary<string, string>?   Baggage    { get; init; }
    public JsonElement?                           Custom     { get; init; }
}
```

### TaskState（枚举）

```csharp
public enum TaskState
{
    Pending,       // 已提交，等待开始
    Preflight,     // 预检中
    Running,       // 执行中
    WaitingSync,   // 等待 SyncFrame 同步屏障
    Completed,     // 成功完成
    Failed,        // 失败
    Cancelled,     // 已取消
    Skipped,       // 被条件跳过
}
```

### 优先级 / 聚合策略常量

```csharp
TaskPriority.Low     // "low"
TaskPriority.Normal  // "normal"
TaskPriority.High    // "high"

AggregateStrategy.Merge    // "merge"  — 最后写入胜出（JSON 对象合并）
AggregateStrategy.First    // "first"  — 取第一个成功结果
AggregateStrategy.All      // "all"    — 以数组形式返回全部结果
AggregateStrategy.FastestK // "fastest_k" — 取最快的 K 个
```

---

## INopOrchestrator

命名空间：`NPS.NOP.Orchestration`

```csharp
public interface INopOrchestrator
{
    // 执行完整任务生命周期（DAG 校验 → 预检 → 执行 → 回调）
    Task<NopTaskResult> ExecuteAsync(TaskFrame task, CancellationToken ct = default)

    // 请求取消正在执行的任务
    Task CancelAsync(string taskId, CancellationToken ct = default)

    // 查询任务当前状态
    Task<NopTaskRecord?> GetStatusAsync(string taskId, CancellationToken ct = default)
}
```

---

## NopOrchestrator

命名空间：`NPS.NOP.Orchestration`

`INopOrchestrator` 的完整实现：

```csharp
public sealed class NopOrchestrator : INopOrchestrator
{
    public NopOrchestrator(
        INopWorkerClient         worker,
        INopTaskStore            store,
        NopOrchestratorOptions?  opts       = null,
        ILogger<NopOrchestrator>? log       = null,
        IHttpClientFactory?      httpFactory = null)
}
```

`ExecuteAsync` 内部执行顺序：

1. **委托链深度检查**：`DelegateDepth >= MaxDelegateChainDepth (3)` → 返回 `DelegateChainTooDeep` 错误
2. **callback_url 校验**：非 https:// 或私有 IP → 返回 SSRF 错误
3. **DAG 校验**：节点数 ≤ 32，无环，拓扑排序
4. **预检（可选）**：对每个 Agent 调用 `INopWorkerClient.PreflightAsync`
5. **状态持久化**：`TaskState.Running`
6. **DAG 执行**：按拓扑序并发执行节点，处理条件、输入映射、重试和同步屏障
7. **结果聚合**：对终止节点应用 `AggregateStrategy`
8. **回调（fire-and-forget）**：POST `CallbackUrl`，指数退避重试最多 `CallbackMaxRetries` 次；设置 `CallbackSecret` 时，对原始 JSON body 计算并附带 `X-NPS-Signature: sha256=<hex>`

---

## NopTaskResult

命名空间：`NPS.NOP.Orchestration`

```csharp
public sealed class NopTaskResult
{
    public required string       TaskId          { get; init; }
    public required TaskState    FinalState      { get; init; } // Completed | Failed | Cancelled
    public          JsonElement? AggregatedResult { get; init; }
    public          string?      ErrorCode        { get; init; }
    public          string?      ErrorMessage     { get; init; }
    public          IReadOnlyDictionary<string, JsonElement> NodeResults { get; init; }

    public static NopTaskResult Success(string taskId, JsonElement? aggregatedResult,
        IReadOnlyDictionary<string, JsonElement> nodeResults)
    public static NopTaskResult Failure(string taskId, string errorCode, string errorMessage)
    public static NopTaskResult Cancelled(string taskId, string reason)
}
```

---

## INopWorkerClient

命名空间：`NPS.NOP.Orchestration`

实现此接口以对接真实 Worker Agent（测试时使用 Stub）：

```csharp
public interface INopWorkerClient
{
    // 发送 DelegateFrame 并接收 AlignStreamFrame 流
    IAsyncEnumerable<AlignStreamFrame> DelegateAsync(
        DelegateFrame frame, CancellationToken ct = default)

    // 预检探测：检查 Agent 是否可用并估算资源
    Task<PreflightResult> PreflightAsync(
        string agentNid, string action,
        long estimatedNpt = 0,
        IReadOnlyList<string>? requiredCapabilities = null,
        CancellationToken ct = default)
}
```

### PreflightResult

```csharp
public sealed record PreflightResult
{
    public required string               AgentNid          { get; init; }
    public required bool                 Available         { get; init; }
    public          long?                AvailableNpt      { get; init; }
    public          int?                 EstimatedQueueMs  { get; init; }
    public          IReadOnlyList<string>? Capabilities    { get; init; }
    public          string?              UnavailableReason { get; init; }
}
```

---

## DAG 校验器

命名空间：`NPS.NOP.Validation`

```csharp
public static class DagValidator
{
    // 校验 DAG 并返回拓扑序（Kahn's 算法）
    static DagValidationResult Validate(TaskDag dag)
}

public sealed record DagValidationResult
{
    public bool                     IsValid          { get; init; }
    public string?                  ErrorCode        { get; init; }
    public string?                  ErrorMessage     { get; init; }
    public IReadOnlyList<string>?   TopologicalOrder { get; init; }

    public static DagValidationResult Success(IReadOnlyList<string> order)
    public static DagValidationResult Failure(string errorCode, string message)
}
```

校验规则（NPS-5 §3.1）：
- 节点数 ≤ `NopConstants.MaxDagNodes (32)`
- 无孤立节点（每个边的端点必须存在）
- 无环（有向无环图）
- 条件表达式长度 ≤ `NopConstants.MaxConditionLength (512)`

---

## callback_url 校验器

命名空间：`NPS.NOP.Validation`

```csharp
public static class NopCallbackValidator
{
    // 验证 callback_url（返回 null 表示合法；返回错误字符串表示非法）
    static string? ValidateCallbackUrl(string callbackUrl)

    // 判断主机是否为私有/环回地址（SSRF 防护）
    static bool IsPrivateHost(string host)
}
```

`ValidateCallbackUrl` 规则：

| 条件 | 错误信息关键词 |
|------|---------------|
| 空字符串 | `must not be empty` |
| 非合法 URI | `is not a valid absolute URI` |
| 非 https:// | `MUST use the https://` |
| 私有 / 环回 IP | `SSRF` |

`IsPrivateHost` 拒绝范围：`localhost`、`127.x.x.x`、`10.x.x.x`、`172.16–31.x.x`、`192.168.x.x`、`169.254.x.x`、`0.0.0.0`、`::1`、`fe80::`。

---

## 条件求值器

命名空间：`NPS.NOP.Orchestration`

```csharp
public static class NopConditionEvaluator
{
    // 对上下文（上游节点结果）求值 CEL 子集条件表达式
    // 支持：==、!=、<、<=、>、>=、in、&&、||、!、字段路径（"node1.field"）
    static bool Evaluate(string condition, IReadOnlyDictionary<string, JsonElement> context)
}
```

**示例条件：**

```
// 跳过低价商品
"node1.price > 50"

// 多条件组合
"node1.status == 'active' && node2.count > 0"

// 成员检查
"node1.category in ['electronics', 'books']"
```

---

## 输入映射器

命名空间：`NPS.NOP.Orchestration`

```csharp
public static class NopInputMapper
{
    // 解析单个 JSONPath 表达式（"$.node1.result.id" 格式）
    static JsonElement? Resolve(string path, IReadOnlyDictionary<string, JsonElement> context)

    // 从 InputMapping 字典构建 params 对象
    static JsonElement BuildParams(
        IReadOnlyDictionary<string, JsonElement>? inputMapping,
        IReadOnlyDictionary<string, JsonElement>  context)
}
```

**示例：**

```json
// DagNode.InputMapping 示例
{
  "userId":   "$.node1.result.user_id",
  "orderId":  "$.node2.result.order_id"
}
```

---

## 结果聚合器

命名空间：`NPS.NOP.Orchestration`

```csharp
public static class NopResultAggregator
{
    // 按策略聚合结果列表
    static JsonElement Aggregate(string strategy, IReadOnlyList<JsonElement> results, int minRequired = 0)

    // "merge"：JSON 对象合并（最后写入胜出）
    static JsonElement Merge(IReadOnlyList<JsonElement> results)

    // "all"：以 JSON 数组返回所有结果
    static JsonElement BuildArray(IReadOnlyList<JsonElement> results)

    // 对 DAG 终止节点聚合
    static JsonElement AggregateEndNodes(
        IReadOnlyList<string>                    endNodeIds,
        IReadOnlyDictionary<string, JsonElement> allResults,
        string strategy = AggregateStrategy.Merge)
}
```

---

## 任务存储

### INopTaskStore

命名空间：`NPS.NOP.Orchestration`

```csharp
public interface INopTaskStore
{
    Task SaveAsync(NopTaskRecord record, CancellationToken ct = default)
    Task<NopTaskRecord?> GetAsync(string taskId, CancellationToken ct = default)
    Task UpdateStateAsync(string taskId, TaskState state, CancellationToken ct = default)
    Task UpdateSubtaskAsync(
        string taskId, string nodeId, string subtaskId, TaskState state,
        JsonElement? result = null, string? errorCode = null, string? errorMsg = null,
        int attempt = 1, CancellationToken ct = default)
}
```

### NopTaskRecord / NopSubtaskRecord

```csharp
public sealed class NopTaskRecord
{
    public required string     TaskId      { get; init; }
    public required TaskFrame  Frame       { get; init; }
    public required DateTime   StartedAt   { get; init; }
    public volatile TaskState  State;
    public          DateTime?  CompletedAt { get; set; }
    public Dictionary<string, NopSubtaskRecord> Subtasks { get; }
}

public sealed class NopSubtaskRecord
{
    public required string    NodeId      { get; init; }
    public required string    SubtaskId   { get; init; }
    public volatile TaskState State;
    public          JsonElement? Result   { get; set; }
    public          string?   ErrorCode   { get; set; }
    public          string?   ErrorMessage { get; set; }
    public          int       AttemptCount { get; set; }
}
```

---

## 协议常量

命名空间：`NPS.NOP`

```csharp
NopConstants.MaxDagNodes            = 32      // 单 DAG 最大节点数
NopConstants.MaxDelegateChainDepth  = 3       // 最大委托链深度（根 = 0）
NopConstants.MaxConditionLength     = 512     // CEL 条件表达式最大字符数
NopConstants.MaxInputMappingDepth   = 8       // JSONPath 最大嵌套深度
NopConstants.DefaultTimeoutMs       = 30_000  // 默认任务超时（毫秒）
NopConstants.MaxTimeoutMs           = 3_600_000 // 最大任务超时（1 小时）
NopConstants.DefaultAnchorTtl       = 3_600   // AnchorFrame 默认 TTL（秒）
NopConstants.CallbackMaxRetries     = 3       // 回调最大重试次数
```

---

## NOP 错误码

命名空间：`NPS.NOP`

```csharp
NopErrorCodes.TaskNotFound          // "NOP-TASK-NOT-FOUND"
NopErrorCodes.TaskTimeout           // "NOP-TASK-TIMEOUT"
NopErrorCodes.TaskDagInvalid        // "NOP-TASK-DAG-INVALID"
NopErrorCodes.TaskDagCycle          // "NOP-TASK-DAG-CYCLE"
NopErrorCodes.TaskDagTooLarge       // "NOP-TASK-DAG-TOO-LARGE"
NopErrorCodes.TaskAlreadyCompleted  // "NOP-TASK-ALREADY-COMPLETED"
NopErrorCodes.TaskCancelled         // "NOP-TASK-CANCELLED"
NopErrorCodes.DelegateChainTooDeep  // "NOP-DELEGATE-CHAIN-TOO-DEEP"
NopErrorCodes.DelegateTimeout       // "NOP-DELEGATE-TIMEOUT"
NopErrorCodes.SyncTimeout           // "NOP-SYNC-TIMEOUT"
NopErrorCodes.SyncDependencyFailed  // "NOP-SYNC-DEP-FAILED"
NopErrorCodes.StreamSeqGap          // "NOP-STREAM-SEQ-GAP"
NopErrorCodes.StreamNidMismatch     // "NOP-STREAM-NID-MISMATCH"
NopErrorCodes.ConditionEvalError    // "NOP-CONDITION-EVAL-ERROR"
NopErrorCodes.InputMappingError     // "NOP-INPUT-MAPPING-ERROR"
```

---

## 完整使用示例

```csharp
// 定义双节点串行 DAG：fetch → summarize
var task = new TaskFrame
{
    TaskId      = Guid.NewGuid().ToString("D"),
    TimeoutMs   = 60_000,
    CallbackUrl = "https://webhook.example.com/nop/done",
    Dag = new TaskDag
    {
        Nodes =
        [
            new DagNode
            {
                Id     = "fetch",
                Action = "nwp://data.example.com/products/query",
                Agent  = "urn:nps:agent:ca.example.com:data-agent",
            },
            new DagNode
            {
                Id        = "summarize",
                Action    = "nwp://llm.example.com/summarize",
                Agent     = "urn:nps:agent:ca.example.com:llm-agent",
                InputFrom = ["fetch"],
                InputMapping = new Dictionary<string, JsonElement>
                {
                    ["data"] = JsonSerializer.SerializeToElement("$.fetch.result"),
                },
                Condition = "fetch.count > 0",
                RetryPolicy = new RetryPolicy { MaxRetries = 2, Backoff = "exponential" },
            },
        ],
        Edges = [new DagEdge("fetch", "summarize")],
    },
};

var result = await orchestrator.ExecuteAsync(task);

if (result.FinalState == TaskState.Completed)
{
    Console.WriteLine(result.AggregatedResult);
    Console.WriteLine(result.NodeResults["summarize"]);
}
else
{
    Console.WriteLine($"Failed: {result.ErrorCode} — {result.ErrorMessage}");
}
```

---

## 相关文档

- [DAG 编排概念](../../concepts/dag-orchestration.md)
- [NOP 规范](../../../spec/NPS-5-NOP.md)
- [NPS.NIP API 参考](./nps-nip.md)
