English | [中文版](./dag-orchestration.md)

# DAG Orchestration

NOP (Neural Orchestration Protocol) describes multi-agent tasks as **Directed Acyclic Graphs (DAGs)**. The orchestrator schedules nodes in topological order, passes intermediate results between them, and aggregates the final output.

---

## Core Concepts

### Task

A complete multi-agent collaboration request, described by a `TaskFrame`, containing:

- **DAG definition**: node set + directed edge set
- **Global configuration**: timeout, retry policy, priority, callback URL
- **Delegation depth**: `DelegateDepth` (0 for root tasks; incremented on each forward)

### Node (DagNode)

Each vertex in the DAG corresponds to one Agent execution unit:

- `action`: target operation (`nwp://` URL)
- `agent`: Worker NID that will execute the action
- `input_from`: list of upstream node IDs (explicit dependencies)
- `input_mapping`: JSONPath expressions to extract params from upstream results
- `condition`: CEL expression — node is skipped when `false`
- `retry_policy`: per-node retry policy (overrides global `MaxRetries`)

### Edge (DagEdge)

A directed edge `From → To` declares execution order. Data flow is controlled separately through `InputFrom` + `InputMapping`.

---

## Execution Flow

```
TaskFrame
    │
    ▼
DAG Validation (DagValidator)
  - node count ≤ 32
  - acyclic
  - topological sort
    │
    ▼
Preflight (optional)
  - send preflight probe to each Agent
  - assess resource availability
    │
    ▼
Concurrent execution in topological order
  ┌─────────────────────────┐
  │  For each ready node:    │
  │  1. Evaluate condition   │
  │  2. Apply input mapping  │
  │  3. Send DelegateFrame   │
  │  4. Receive AlignStream  │
  │  5. Retry on failure     │
  └─────────────────────────┘
    │
    ▼
Wait for SyncFrame (if present)
    │
    ▼
Aggregate terminal node results (AggregateStrategy)
    │
    ▼
NopTaskResult (Completed | Failed | Cancelled)
    │
    ▼
Callback POST (fire-and-forget, up to 3 retries)
```

---

## Input Mapping

`InputMapping` is a `{ param-name → JSONPath }` dictionary that extracts upstream node result fields as input parameters for the current node:

```json
{
  "userId":  "$.fetch.result.user_id",
  "orderId": "$.enrich.result.order_id"
}
```

The `$` root is followed by the upstream node ID, then the JSON field path. `NopInputMapper.BuildParams` resolves these at runtime and builds the `params` object.

---

## Condition Evaluation

`DagNode.Condition` is a CEL-subset expression evaluated against the completed upstream node results:

```
// Only execute when fetch node returned data
"fetch.count > 0"

// Multiple conditions
"fetch.status == 'active' && enrich.score >= 0.8"

// Membership test
"fetch.category in ['electronics', 'books']"
```

When the condition is `false`, the node state is set to `Skipped`, and downstream dependents skip waiting for it (treated as successful).

---

## K-of-N Synchronization

`SyncFrame.MinRequired` defines the minimum number of upstream tasks that must succeed before proceeding. `0` means all must succeed (default).

```csharp
// Three data sources — any 2 succeeding is enough to continue
new DagNode
{
    Id          = "aggregate",
    InputFrom   = ["source1", "source2", "source3"],
    MinRequired = 2,
}
```

`AggregateStrategy.FastestK` pairs with K-of-N to take the first K completed results.

---

## Delegation Chain Depth

NOP supports nested orchestration — a Worker Agent can itself be an orchestrator, receiving and forwarding TaskFrames:

```
Client
 └─▶ Orchestrator A  (depth=0)
      └─▶ Worker B / Orchestrator B  (depth=1)
           └─▶ Worker C             (depth=2)
                └─▶ Rejected (depth=3 ≥ MaxDelegateChainDepth)
```

`NopConstants.MaxDelegateChainDepth = 3`. Exceeding this returns `NOP-DELEGATE-CHAIN-TOO-DEEP`, preventing infinite recursive delegation.

---

## Callback Mechanism

After task completion (success or failure), the orchestrator POSTs `callback_url` in a fire-and-forget fashion, carrying the JSON-serialized `NopTaskResult`:

- **Exponential backoff**: delay = `CallbackRetryBaseDelayMs × 2^(attempt-1)` (default: 0s, 1s, 2s)
- **Max retries**: `CallbackMaxRetries = 3`
- **HMAC signing**: when `callback_secret` is set, `X-NPS-Signature: sha256=<hex>` covers the raw callback JSON body
- **Callback failure**: does not affect the task's own `FinalState` (non-fatal)

`callback_url` validation rules (§8.4):
- Must use the `https://` scheme
- Private/loopback addresses are rejected (SSRF protection)

---

## Aggregation Strategies

| Strategy | Behavior |
|----------|----------|
| `merge` | Merge all results into a single JSON object (last-write-wins) |
| `first` | Return the result of the first successfully completed node |
| `all` | Return all results as a JSON array |
| `fastest_k` | Return an array of the first K completed results |

---

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Node failure (retryable) | Exponential backoff retry per `RetryPolicy` |
| Node failure (non-retryable) | State → `Failed`; if a terminal node fails, the whole task fails |
| Condition is `false` | Node state → `Skipped`; downstream continues |
| Timeout | State → `Failed`, error code `NOP-TASK-TIMEOUT` |
| Cancellation | State → `Cancelled`; in-flight subtasks receive a cancel signal |

---

## Related Documentation

- [NPS.NOP API Reference](../sdk/dotnet/nps-nop.en.md)
- [NOP Specification](../../spec/NPS-5-NOP.md)
