English | [中文版](./nps-nop.md)

# NPS.NOP API Reference

`NPS.NOP` provides a multi-agent DAG orchestration engine: task definition, subtask delegation, K-of-N synchronization, condition evaluation, input mapping, result aggregation, callback notification, §8.2 delegation-chain depth limiting, and §8.4 SSRF protection.

---

## DI Registration

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
// Registers: INopOrchestrator (singleton NopOrchestrator), INopTaskStore (singleton InMemoryNopTaskStore)
```

---

## NopOrchestratorOptions

Namespace: `NPS.NOP.Orchestration`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MaxConcurrentNodes` | `int` | `ProcessorCount × 2` | Maximum concurrent nodes per task |
| `ValidateSenderNid` | `bool` | `true` | Whether to validate that `AlignStreamFrame.SenderNid` matches the DAG node's agent NID |
| `EnableCallback` | `bool` | `true` | Whether to POST the callback URL on task completion (fire-and-forget) |
| `CallbackTimeoutMs` | `int` | `10000` | Callback HTTP request timeout (ms) |
| `CallbackRetryBaseDelayMs` | `int` | `1000` | Base delay for exponential backoff between callback retries (ms); set to 0 in tests |
| `DefaultAggregateStrategy` | `string` | `"merge"` | Default aggregation strategy for terminal nodes when no SyncFrame is present |

---

## NOP Frame Types

Namespace: `NPS.NOP.Frames`

### TaskFrame

Task definition frame; submitted to the orchestrator to start a DAG task:

```csharp
public sealed record TaskFrame : IFrame
{
    public required string       TaskId        { get; init; } // UUID v4
    public required TaskDag      Dag           { get; init; }
    public          uint         TimeoutMs     { get; init; } = 30_000; // max 3 600 000
    public          byte         MaxRetries    { get; init; } = 2;
    public          string       Priority      { get; init; } = "normal" // "low"|"normal"|"high"
    public          string?      CallbackUrl   { get; init; } // must be https:// (SSRF protection)
    public          string?      CallbackSecret { get; init; } // base64url 32-byte HMAC key
    public          bool         Preflight     { get; init; }
    public          TaskContext? Context       { get; init; }
    public          string?      RequestId     { get; init; }
    public          int          DelegateDepth { get; init; } // 0 = root task; rejected if ≥ 3
    public          string       CompensationPolicy { get; init; } = "best_effort"; // "best_effort"|"strict"
}
```

---

### DelegateFrame

Subtask delegation frame sent by the orchestrator to a Worker Agent:

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
    public          int          DelegateDepth    { get; init; } // = parent task DelegateDepth + 1
}
```

---

### SyncFrame

Synchronization barrier frame; waits for multiple subtasks to complete (supports K-of-N):

```csharp
public sealed record SyncFrame : IFrame
{
    public required string                  TaskId      { get; init; }
    public required string                  SyncId      { get; init; }
    public required IReadOnlyList<string>   WaitFor     { get; init; } // list of subtask IDs to wait for
    public          uint                    MinRequired { get; init; } // K-of-N; 0 = all
    public          string                  Aggregate   { get; init; } = "merge"
    public          uint?                   TimeoutMs   { get; init; }
}
```

---

### AlignStreamFrame

Directed task stream frame; returned by a Worker to the orchestrator as intermediate or final results:

```csharp
public sealed record AlignStreamFrame : IFrame
{
    public required string       StreamId   { get; init; }
    public required string       TaskId     { get; init; }
    public required string       SubtaskId  { get; init; }
    public required ulong        Seq        { get; init; } // strictly monotonically increasing, 0-based
    public          string?      PayloadRef { get; init; }
    public          JsonElement? Data       { get; init; }
    public          uint?        WindowSize { get; init; }
    public required bool         IsFinal    { get; init; }
    public required string       SenderNid  { get; init; }
    public          StreamError? Error      { get; init; } // non-null when IsFinal=true and failed
}
```

---

## DAG Model

Namespace: `NPS.NOP.Models`

### TaskDag / DagNode / DagEdge

```csharp
public sealed record TaskDag
{
    public required IReadOnlyList<DagNode> Nodes { get; init; }
    public required IReadOnlyList<DagEdge> Edges { get; init; }
}

public sealed record DagNode
{
    public required string                                    Id           { get; init; }
    public required string                                    Action       { get; init; } // nwp:// URL
    public required string                                    Agent        { get; init; } // Worker NID
    public          IReadOnlyList<string>?                    InputFrom    { get; init; } // dependency node IDs
    public          IReadOnlyDictionary<string, JsonElement>? InputMapping { get; init; } // JSONPath mappings
    public          uint?                                     TimeoutMs    { get; init; }
    public          RetryPolicy?                              RetryPolicy  { get; init; }
    public          string?                                   Condition    { get; init; } // CEL condition expression
    public          uint                                      MinRequired  { get; init; } // K-of-N
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
    public IReadOnlyList<string>?  RetryOn        { get; init; } // retryable error codes

    // Compute the delay for the nth retry attempt (milliseconds)
    uint ComputeDelayMs(int attempt)
}
```

Backoff strategies (`BackoffStrategy`): `"fixed"`, `"linear"`, `"exponential"`.

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

### TaskState (enum)

```csharp
public enum TaskState
{
    Pending,       // submitted, waiting to start
    Preflight,     // running preflight checks
    Running,       // executing
    WaitingSync,   // waiting at a SyncFrame barrier
    Completed,     // successfully completed
    Failed,        // failed
    Cancelled,     // cancelled
    Skipped,       // skipped by condition evaluation
}
```

### Priority / Aggregate Strategy Constants

```csharp
TaskPriority.Low     // "low"
TaskPriority.Normal  // "normal"
TaskPriority.High    // "high"

AggregateStrategy.Merge    // "merge"     — last-write-wins JSON object merge
AggregateStrategy.First    // "first"     — return the first successful result
AggregateStrategy.All      // "all"       — return all results as a JSON array
AggregateStrategy.FastestK // "fastest_k" — return the first K completed results
```

---

## INopOrchestrator

Namespace: `NPS.NOP.Orchestration`

```csharp
public interface INopOrchestrator
{
    // Execute the full task lifecycle (DAG validation → preflight → execution → callback)
    Task<NopTaskResult> ExecuteAsync(TaskFrame task, CancellationToken ct = default)

    // Request cancellation of a running task
    Task CancelAsync(string taskId, CancellationToken ct = default)

    // Query the current state of a task
    Task<NopTaskRecord?> GetStatusAsync(string taskId, CancellationToken ct = default)
}
```

---

## NopOrchestrator

Namespace: `NPS.NOP.Orchestration`

Full implementation of `INopOrchestrator`:

```csharp
public sealed class NopOrchestrator : INopOrchestrator
{
    public NopOrchestrator(
        INopWorkerClient          worker,
        INopTaskStore             store,
        NopOrchestratorOptions?   opts        = null,
        ILogger<NopOrchestrator>? log         = null,
        IHttpClientFactory?       httpFactory = null)
}
```

`ExecuteAsync` internal execution order:

1. **Delegation-chain depth check**: `DelegateDepth >= MaxDelegateChainDepth (3)` → return `DelegateChainTooDeep` error
2. **callback_url validation**: non-https:// or private IP → return SSRF error
3. **DAG validation**: node count ≤ 32, acyclic, topological sort
4. **Preflight (optional)**: call `INopWorkerClient.PreflightAsync` for each agent
5. **State persistence**: `TaskState.Running`
6. **DAG execution**: run nodes concurrently in topological order, handling conditions, input mapping, retries, and sync barriers
7. **Result aggregation**: apply `AggregateStrategy` to terminal nodes
8. **Callback (fire-and-forget)**: POST `CallbackUrl` with exponential backoff, up to `CallbackMaxRetries` attempts; when `CallbackSecret` is set, include `X-NPS-Signature: sha256=<hex>` over the raw JSON body

---

## NopTaskResult

Namespace: `NPS.NOP.Orchestration`

```csharp
public sealed class NopTaskResult
{
    public required string       TaskId           { get; init; }
    public required TaskState    FinalState        { get; init; } // Completed | Failed | Cancelled
    public          JsonElement? AggregatedResult  { get; init; }
    public          string?      ErrorCode         { get; init; }
    public          string?      ErrorMessage      { get; init; }
    public          IReadOnlyDictionary<string, JsonElement> NodeResults { get; init; }

    public static NopTaskResult Success(string taskId, JsonElement? aggregatedResult,
        IReadOnlyDictionary<string, JsonElement> nodeResults)
    public static NopTaskResult Failure(string taskId, string errorCode, string errorMessage)
    public static NopTaskResult Cancelled(string taskId, string reason)
}
```

---

## INopWorkerClient

Namespace: `NPS.NOP.Orchestration`

Implement this interface to integrate a real Worker Agent (use a stub in tests):

```csharp
public interface INopWorkerClient
{
    // Send a DelegateFrame and receive a stream of AlignStreamFrames
    IAsyncEnumerable<AlignStreamFrame> DelegateAsync(
        DelegateFrame frame, CancellationToken ct = default)

    // Preflight probe: check whether the agent is available and estimate resources
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
    public required string                AgentNid           { get; init; }
    public required bool                  Available          { get; init; }
    public          long?                 AvailableNpt       { get; init; }
    public          int?                  EstimatedQueueMs   { get; init; }
    public          IReadOnlyList<string>? Capabilities      { get; init; }
    public          string?               UnavailableReason  { get; init; }
}
```

---

## DAG Validator

Namespace: `NPS.NOP.Validation`

```csharp
public static class DagValidator
{
    // Validate a DAG and return the topological order (Kahn's algorithm)
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

Validation rules (NPS-5 §3.1):
- Node count ≤ `NopConstants.MaxDagNodes (32)`
- No isolated nodes (every edge endpoint must exist)
- Acyclic (directed acyclic graph)
- Condition expression length ≤ `NopConstants.MaxConditionLength (512)`

---

## callback_url Validator

Namespace: `NPS.NOP.Validation`

```csharp
public static class NopCallbackValidator
{
    // Validate a callback_url (returns null if valid; returns an error string if invalid)
    static string? ValidateCallbackUrl(string callbackUrl)

    // Check whether a host is a private/loopback address (SSRF protection)
    static bool IsPrivateHost(string host)
}
```

`ValidateCallbackUrl` rules:

| Condition | Error keyword |
|-----------|---------------|
| Empty string | `must not be empty` |
| Not a valid URI | `is not a valid absolute URI` |
| Not https:// | `MUST use the https://` |
| Private / loopback IP | `SSRF` |

`IsPrivateHost` rejects: `localhost`, `127.x.x.x`, `10.x.x.x`, `172.16–31.x.x`, `192.168.x.x`, `169.254.x.x`, `0.0.0.0`, `::1`, `fe80::`.

---

## Condition Evaluator

Namespace: `NPS.NOP.Orchestration`

```csharp
public static class NopConditionEvaluator
{
    // Evaluate a CEL-subset condition expression against the context (upstream node results)
    // Supports: ==, !=, <, <=, >, >=, in, &&, ||, !, field paths ("node1.field")
    static bool Evaluate(string condition, IReadOnlyDictionary<string, JsonElement> context)
}
```

**Example conditions:**

```
// Skip low-priced items
"node1.price > 50"

// Multiple conditions combined
"node1.status == 'active' && node2.count > 0"

// Membership test
"node1.category in ['electronics', 'books']"
```

---

## Input Mapper

Namespace: `NPS.NOP.Orchestration`

```csharp
public static class NopInputMapper
{
    // Resolve a single JSONPath expression ("$.node1.result.id" format)
    static JsonElement? Resolve(string path, IReadOnlyDictionary<string, JsonElement> context)

    // Build a params object from an InputMapping dictionary
    static JsonElement BuildParams(
        IReadOnlyDictionary<string, JsonElement>? inputMapping,
        IReadOnlyDictionary<string, JsonElement>  context)
}
```

**Example:**

```json
// DagNode.InputMapping example
{
  "userId":   "$.node1.result.user_id",
  "orderId":  "$.node2.result.order_id"
}
```

---

## Result Aggregator

Namespace: `NPS.NOP.Orchestration`

```csharp
public static class NopResultAggregator
{
    // Aggregate a list of results according to the strategy
    static JsonElement Aggregate(string strategy, IReadOnlyList<JsonElement> results, int minRequired = 0)

    // "merge": JSON object merge (last-write-wins)
    static JsonElement Merge(IReadOnlyList<JsonElement> results)

    // "all": return all results as a JSON array
    static JsonElement BuildArray(IReadOnlyList<JsonElement> results)

    // Aggregate DAG terminal nodes
    static JsonElement AggregateEndNodes(
        IReadOnlyList<string>                    endNodeIds,
        IReadOnlyDictionary<string, JsonElement> allResults,
        string strategy = AggregateStrategy.Merge)
}
```

---

## Task Store

### INopTaskStore

Namespace: `NPS.NOP.Orchestration`

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
    public required string    NodeId       { get; init; }
    public required string    SubtaskId    { get; init; }
    public volatile TaskState State;
    public          JsonElement? Result    { get; set; }
    public          string?   ErrorCode    { get; set; }
    public          string?   ErrorMessage { get; set; }
    public          int       AttemptCount { get; set; }
}
```

---

## Protocol Constants

Namespace: `NPS.NOP`

```csharp
NopConstants.MaxDagNodes            = 32        // max nodes per DAG
NopConstants.MaxDelegateChainDepth  = 3         // max delegation chain depth (root = 0)
NopConstants.MaxConditionLength     = 512       // max CEL condition expression length (characters)
NopConstants.MaxInputMappingDepth   = 8         // max JSONPath nesting depth
NopConstants.DefaultTimeoutMs       = 30_000    // default task timeout (ms)
NopConstants.MaxTimeoutMs           = 3_600_000 // max task timeout (1 hour)
NopConstants.DefaultAnchorTtl       = 3_600     // AnchorFrame default TTL (seconds)
NopConstants.CallbackMaxRetries     = 3         // max callback retry attempts
```

---

## NOP Error Codes

Namespace: `NPS.NOP`

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

## Complete Usage Example

```csharp
// Define a two-node serial DAG: fetch → summarize
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
                Condition   = "fetch.count > 0",
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

## Related Documentation

- [DAG Orchestration Concepts](../../concepts/dag-orchestration.en.md)
- [NOP Specification](../../../spec/NPS-5-NOP.md)
- [NPS.NIP API Reference](./nps-nip.en.md)
