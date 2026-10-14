# Protocol: NOP — Neural Orchestration Protocol

**Status:** ✅ Released alpha.19 reference

**Released spec**: `spec/NPS-5-NOP.md` v0.10 · **Port**: 17433 (shared) / 17437 (optional dedicated)
**Supersedes**: NCP AlignFrame (0x05) — deprecated, removed in NPS v1.0

> **Alpha.19 release: NOP 0.10 Proposed.** It closes
> bounded replay/eviction, TTL and aggregation semantics under loss, reorder,
> duplicate and timeout faults across six SDKs. See
> [Alpha.19 Current Status](./Alpha19-Current-Status.md).

NOP is the orchestration layer of NPS — a combination of SMTP, message queue, and workflow engine at the wire level. It defines how Agents coordinate multi-step tasks: breaking work into DAGs, delegating subtasks to Worker Agents with bounded scope, synchronizing partial results at fan-in barriers, and streaming progress back to the Orchestrator. NOP depends on NCP (framing), NWP (action invocation), and NIP (scope enforcement at every delegation step).

Related: [Protocol NIP](./Protocol-NIP.md) | [Reference: Cognon Budget](./Reference-Cognon-Budget.md) | [SDK Building an Anchor Node](./SDK-Building-an-Anchor-Node.md)

> **alpha.18 release:** NOP v0.9 defines the portable orchestrator profile. It requires bounded DAG validation, deterministic dispatch/status/cancel behavior, lease-safe L3 execution, and CR-0009 HA re-resolution after leadership or lease changes. CR-0007 is implemented; NOP remains above NWP and does not introduce a separate model-serving wire contract.

---

## Protocol Overview

The NOP stack position:

```
NOP (Multi-Agent Orchestration)
  +-- NWP (Data & Operation Access)
  |     +-- NCP (Transport Frames & Encoding)
  +-- NIP (Identity & Scope Validation)
```

The Orchestrator calls Worker Agent operations via NWP `ActionFrame`; Worker Agents push intermediate and final results back to the Orchestrator via `AlignStream`; every delegation step is validated by NIP against a carved-down scope.

NOP replaces the deprecated NCP `AlignFrame (0x05)`, which lacked task context binding and NIP identity enforcement. All new code MUST use `AlignStream (0x43)`.

---

## DAG Model: TaskFrame (0x40)

The Orchestrator submits a `TaskFrame` containing a complete Directed Acyclic Graph (DAG) describing the multi-Agent workflow.

### Hard Limits

| Limit | Value | Error on violation |
|-------|-------|-------------------|
| Max DAG nodes | **32** | `NOP-TASK-DAG-TOO-LARGE` |
| Max delegation chain depth | **3 levels** | `NOP-DELEGATE-CHAIN-TOO-DEEP` |
| Overall task timeout | max **3,600,000 ms** (1 hour) | `NOP-TASK-TIMEOUT` |
| CEL condition expression length | max **512 characters** | `NOP-CONDITION-EVAL-ERROR` |
| `input_mapping` JSONPath nesting depth | max **8 levels** | `NOP-INPUT-MAPPING-ERROR` |

### DAG Structure

A DAG consists of `nodes` (vertices) and `edges` (directed edges).

**DAG node fields:**

| Field | Description |
|-------|-------------|
| `id` | Node identifier (unique within DAG) |
| `action` | Operation URL (`nwp://...`) |
| `agent` | NID of the Worker Agent to execute this node |
| `input_from` | Upstream node IDs this node depends on; empty/absent = root node |
| `input_mapping` | JSONPath expressions mapping upstream outputs to this node's params: `{"local_param": "$.upstream_id.result.field"}` |
| `timeout_ms` | Per-node timeout; takes precedence over TaskFrame's global `timeout_ms` |
| `retry_policy` | `{max_retries, backoff: "fixed"/"linear"/"exponential", initial_delay_ms, max_delay_ms, retry_on: [error_codes]}` |
| `condition` | CEL subset expression; if evaluates to `false`, the node is skipped (`SKIPPED` status). If a terminal node is skipped, the task ends `COMPLETED`. |
| `compensate_action` | NID URI (`nwp://...`) of the compensating action invoked if this node must be reversed during saga rollback; if absent, the node is treated as non-reversible (added in NOP v0.6) |
| `compensate_params_mapping` | Maps this node's output field names → `compensate_action` input param names (JSONPath rooted at this node's result, e.g. `"$.charge_id"`) (added in NOP v0.6) |

The DAG MUST have at least one root node (no incoming edges) and at least one terminal node (no outgoing edges). Cycles are rejected with `NOP-TASK-DAG-CYCLE`.

### TaskFrame Top-Level Fields

| Field | Description |
|-------|-------------|
| `task_id` | UUID v4 task identifier |
| `dag` | The DAG object with `nodes` and `edges` arrays |
| `timeout_ms` | Overall task timeout (default 30000, max 3600000) |
| `max_retries` | Global max retries for any single node (default 2) |
| `priority` | `"low"` / `"normal"` / `"high"` |
| `callback_url` | `https://` URL for task completion/failure notification |
| `preflight` | If true, perform a resource pre-flight check before execution |
| `compensation_policy` | Saga compensation policy: `"best_effort"` (default) or `"strict"` — controls how compensation failures are handled (added in NOP v0.6; see Saga Compensation below) |
| `callback_secret` | HMAC-SHA256 key (base64url, 32 bytes) for webhook callback signing (added in NOP v0.6). When present, the Orchestrator MUST send `X-NPS-Signature: sha256=<HMAC-SHA256-hex>` computed over the raw JSON body on every POST to `callback_url`. Receivers SHOULD reject callbacks missing the header with `400`; error code `NOP-CALLBACK-HMAC-MISSING`. |
| `result_ttl_seconds` | uint32, default **3600** (omitted from the wire at default). How long the Orchestrator retains the task result after completion; after the TTL expires, result retrieval returns `NOP-TASK-RESULT-EXPIRED` (added in NOP v0.7). |
| `context` | Pass-through context: `{session_id, trace_id, span_id, trace_flags, baggage, custom}` — supports OpenTelemetry W3C TraceContext for distributed tracing across Agent hops |
| `request_id` | UUID v4 for tracing |

---

## DelegateFrame (0x41)

The Orchestrator emits a `DelegateFrame` per DAG node to dispatch a subtask to the designated Worker Agent.

**Key fields:**

| Field | Description |
|-------|-------------|
| `parent_task_id` | Parent `task_id` |
| `subtask_id` | UUID v4 for this subtask |
| `node_id` | Corresponding DAG node `id` |
| `target_agent_nid` | NID of the Worker Agent being delegated to |
| `action` | Operation URL (`nwp://`) |
| `params` | Processed input parameters (after `input_mapping` evaluation) |
| `delegated_scope` | Carved-down NIP scope: `{nodes, actions, max_token_budget}` |
| `deadline_at` | Subtask deadline (ISO 8601 UTC) |
| `idempotency_key` | Idempotency key — MUST be the same value on retries |
| `priority` | Inherited from `TaskFrame.priority` |
| `target_cluster_anchor` | NID of the cluster anchor to route this subtask to, for cross-cluster delegation (added in NOP v0.6). When present, the Orchestrator MUST route the DelegateFrame to a Worker Agent registered under the specified cluster anchor. |
| `context` | Inherited from `TaskFrame.context` with `span_id` updated to the current DelegateFrame span |

### Delegation Chain Depth Limit

The maximum delegation chain depth is **3 levels**, counted as entities in the chain:

- Level 1: Orchestrator
- Level 2: Worker Agent (first delegation)
- Level 3: Sub-Worker Agent (second delegation from a Worker)

A fourth entity (Sub-Sub-Worker) would be at level 4, violating the limit. Equivalently, at most 2 `DelegateFrame` hops are permitted. Violations return `NOP-DELEGATE-CHAIN-TOO-DEEP`.

### Scope Enforcement

`delegated_scope.nodes`, `delegated_scope.actions`, and `delegated_scope.max_token_budget` MUST all be subsets of the parent Agent's scope. The CA enforces this at signing time; violations are rejected with `NOP-DELEGATE-SCOPE-VIOLATION`. This is the no-scope-expansion principle inherited from NIP — no Agent in the delegation chain can grant permissions it does not itself possess.

---

## SyncFrame (0x42): K-of-N Fan-In Barrier

`SyncFrame` is a synchronization barrier that waits for dependent subtasks to complete before the DAG proceeds. It supports K-of-N semantics: wait for at least K out of N upstream subtasks to succeed.

**Key fields:**

| Field | Description |
|-------|-------------|
| `task_id` | Parent task |
| `sync_id` | UUID v4 sync point identifier |
| `wait_for` | Array of `subtask_id` values to wait for |
| `min_required` | K: minimum successful subtasks to unblock; 0 or absent = all must succeed |
| `aggregate` | Result aggregation: `"merge"` (default) / `"first"` / `"all"` / `"fastest_k"` / `"weighted_first_k"` / `"merge_all"` |
| `timeout_ms` | Wait timeout; returns `NOP-SYNC-TIMEOUT` on expiry |

When `min_required: K` with K < N, the barrier unblocks as soon as K subtasks succeed. The Orchestrator SHOULD then send cancel signals to the remaining in-flight subtasks.

**Aggregation strategies:**
- `merge`: merge all successful result fields into one object (later keys overwrite)
- `first`: use the first subtask to succeed
- `all`: preserve all results as an array
- `fastest_k`: collect the `min_required` fastest completions in `all` format
- `weighted_first_k`: select the `min_required` results with the highest confidence scores (requires a `result.score` field in each subtask result); merged in `all` format (added in NOP v0.6)
- `merge_all`: merge all successful results into a single object preserving all fields; unlike `merge`, array-valued fields are concatenated rather than overwritten (added in NOP v0.6)

---

## AlignStreamFrame (0x43): Streaming Progress

`AlignStream` carries partial results and progress updates from a running subtask back to the Orchestrator. It replaces the deprecated NCP `AlignFrame (0x05)` and adds DAG context binding and NIP identity enforcement.

**Key fields:**

| Field | Description |
|-------|-------------|
| `stream_id` | UUID v4 stream identifier |
| `task_id` | Associated parent `task_id` |
| `subtask_id` | Associated subtask |
| `seq` | Strictly increasing sequence number from 0 |
| `payload_ref` | `anchor_ref` of the payload schema |
| `data` | Intermediate result data |
| `window_size` | Token-level backpressure window in CGN units (not bytes; see CGN Budget Flow below) |
| `is_final` | `true` marks the final result frame for this subtask |
| `ack_seq` | Acknowledge all frames with `seq ≤ ack_seq` received and processed; used in the sliding-window ACK/NAK protocol (added in NOP v0.6; see AlignStream ACK/NAK Window below) |
| `nak_seq` | Negative-acknowledge: request retransmission from `nak_seq`. The sender MUST resend from `nak_seq`; if the frame is no longer buffered, the sender returns `NOP-STREAM-NAK-UNRESOLVABLE` (added in NOP v0.6) |
| `sender_nid` | Sender NID — receiver MUST verify this matches the connection identity |
| `error` | Optional error object on final frame: `{code, message, retryable}` |

**Comparison with NCP StreamFrame:**

| | StreamFrame (0x03) | AlignStream (0x43) |
|--|--------------------|--------------------|
| Use case | General data streams (query results) | Multi-Agent task intermediate results |
| Context | None | Carries `task_id` + `subtask_id` |
| Identity binding | None | `sender_nid` mandatory verification |
| Backpressure unit | Bytes / frame count | CGN Token count |
| Error propagation | None | `error` field with task-failure semantics |

### AlignStream ACK/NAK Window (NOP v0.6)

In addition to the token-level CGN backpressure window, AlignStream carries a sliding-window
ACK/NAK protocol for reliable, in-order delivery of intermediate result frames:

- **Window size** is fixed at **`window_size = 16`** outstanding (unacknowledged) frames. The
  sender MAY have at most 16 frames in flight beyond the receiver's last `ack_seq`.
- **`ack_seq`** — the receiver periodically returns an AlignStream carrying `ack_seq`, cumulatively
  acknowledging every frame with `seq ≤ ack_seq` as received and processed. The sender then slides
  its window forward and MAY release the acknowledged frames from its retransmission buffer.
- **`nak_seq`** — on a detected gap, the receiver sends `nak_seq` requesting retransmission from
  that sequence number. The sender MUST resend from `nak_seq`. A non-contiguous sequence that the
  receiver still detects is reported as `NOP-STREAM-NAK` (`NPS-STREAM-SEQ-GAP`).
- If a NAK requests a frame the sender has already evicted from its buffer, the sender returns
  `NOP-STREAM-NAK-UNRESOLVABLE` (`NPS-STREAM-SEQ-GAP`) and the stream is unrecoverable (added in NOP v0.7).

---

## Condition Expressions: CEL Subset

The DAG node `condition` field uses a subset of CEL (Common Expression Language):

- Numeric comparisons: `>`, `>=`, `<`, `<=`, `==`, `!=`
- Boolean logic: `&&`, `||`, `!`
- JSONPath access to upstream results: `$.<node_id>.<field>` syntax
- String comparison and `in` operator

When `condition` evaluates to `false`, the node is marked `SKIPPED`. A `SKIPPED` terminal node does not cause overall task failure — the task ends `COMPLETED`. Evaluation errors (syntax error, reference to non-existent field) return `NOP-CONDITION-EVAL-ERROR`.

Expression length is capped at **512 characters**.

---

## Async vs Sync Invocation

NOP subtasks are fundamentally async — each DAG node is dispatched via `DelegateFrame` and the Orchestrator waits for `AlignStream(is_final=true)`. However:

- **Idempotency:** retries MUST use the same `subtask_id` and `idempotency_key`.
- **Timeout handling:** each node has an optional `timeout_ms`; if not set, the global TaskFrame `timeout_ms` applies. Deadline is expressed in `DelegateFrame.deadline_at`. Expiry returns `NOP-DELEGATE-TIMEOUT`.
- **Retry backoff:** `"fixed"` (constant delay), `"linear"` (delay scales with attempt count), `"exponential"` (default; delay doubles each attempt), all capped by `max_delay_ms`.

**Resource pre-flight** (`preflight: true` on `TaskFrame`): before dispatching any subtask, the Orchestrator sends a lightweight `DelegateFrame(action="preflight")` to each Worker Agent. The Agent responds with its availability, available CGN budget, and estimated queue time. If any Agent reports `available: false`, the Orchestrator MUST abort the entire task and return `NOP-RESOURCE-INSUFFICIENT`.

---

## CGN Budget Flow

Cognon (CGN) budget flows through the DAG dispatch chain. When the Orchestrator creates a `TaskFrame`, it sets `context` fields and per-node scopes that include `max_token_budget`. This budget is carved down at each `DelegateFrame` hop (scope carving principle from NIP).

`AlignStream.window_size` provides token-level backpressure in CGN units. The `window_size` value represents the maximum CGN cost the receiver can currently absorb. Before sending each `data` frame, the sender estimates the CGN cost and checks the current window balance. If the estimated cost exceeds the window, the sender pauses until the receiver restores the window by emitting a reverse `AlignStream(data=null, window_size=N)`.

For pre-flight, the Orchestrator provides `cgn_est` (estimated Cognons) to help Workers self-assess availability. (The spec §4.2 example long carried the pre-alpha.5.2 name `estimated_npt` — an errata leftover of the NPT→CGN rename; `cgn_est` is the suite-wide field name.)

See [Reference: Cognon Budget](./Reference-Cognon-Budget.md) for CGN computation details.

---

## Saga Compensation (NOP v0.6)

DAG nodes that produce externally observable side effects (charges, bookings, provisioning,
message dispatch) MAY declare a **compensating action** that semantically reverses those effects.
When a downstream failure forces a rollback, the compensating actions of already-completed
predecessor nodes are invoked in **reverse topological order** — implementing the saga pattern.

**Per-node fields:**
- `compensate_action` — NID URI (`nwp://...`) of the compensating action; if absent, the node is non-reversible.
- `compensate_params_mapping` — maps `compensate_action` param names → JSONPath expressions rooted at this node's result (e.g. `"$.charge_id"`).

**TaskFrame-level `compensation_policy`:**

| Value | Behavior |
|-------|----------|
| `"best_effort"` (default) | Compensation failures are logged; the orchestrator continues compensating remaining predecessors, then terminates the saga as `FAILED` |
| `"strict"` | Any compensation failure terminates the saga immediately with `NOP-COMPENSATION-FAILED`; remaining predecessors are NOT compensated |

**Saga trigger:** if a node reaches `FAILED` (after exhausting `retry_policy.max_retries`) and has
predecessors that successfully reached `COMPLETED`, the orchestrator invokes each such predecessor's
`compensate_action` in reverse topological order, transitioning it `COMPLETED → COMPENSATING →
COMPENSATED` (or `COMPENSATION_FAILED` on error). If `compensation_policy="strict"` and a failed-node
predecessor lacks a `compensate_action`, the orchestrator returns `NOP-COMPENSATION-NOT-SUPPORTED`
(terminal) and invokes no compensations. Compensating actions MUST be idempotent and SHOULD use a
stable `idempotency_key` of the form `<parent_task_id>:<node_id>:compensate`.

```json
{
  "id": "charge",
  "action": "nwp://payments.example.com/charge/invoke",
  "agent": "urn:nps:agent:...:payments",
  "compensate_action": "nwp://payments.example.com/refund/invoke",
  "compensate_params_mapping": { "charge_id": "$.charge_id", "amount": "$.amount" }
}
```

---

## Webhook Callback Signing (NOP v0.6)

When `TaskFrame.callback_secret` is set, the Orchestrator MUST sign every webhook POST to
`callback_url`:

- Compute `HMAC-SHA256(callback_secret, raw_json_body)` and send it as
  `X-NPS-Signature: sha256=<hex>`.
- Receivers SHOULD reject any callback that is missing the `X-NPS-Signature` header with HTTP
  `400`; the corresponding NOP error is `NOP-CALLBACK-HMAC-MISSING` (`NPS-AUTH-UNAUTHENTICATED`).
- `callback_url` MUST use the `https://` scheme and SHOULD be SSRF-validated; delivery failures
  use exponential backoff (max 3 attempts) before being abandoned and logged.

---

## Task Execution State Machine

```
          +----------+
          | PENDING  |  TaskFrame submitted, awaiting scheduling
          +----+-----+
               |
               v
       +--------------+
       |  PREFLIGHT   |  Resource pre-flight (when preflight=true)
       +------+-------+
              |
              v
          +--------+
          | RUNNING |  Subtasks executing
          +----+----+
               |
      +--------+--------+----------+
      v                 v          v
+------------+     +--------+  +----------+
|WAITING_SYNC|     | FAILED |  |CANCELLED |
|(sync barrier)    +---+----+  +----------+
+------+-----+        |
       |               | Exceeds max_retries
       v               v
  +-----------+
  | COMPLETED |
  +-----------+
```

Subtask states: `PENDING` → `RUNNING` → `COMPLETED` / `FAILED` / `CANCELLED` / `SKIPPED`

A `COMPLETED` subtask MAY additionally enter the saga-compensation lifecycle when a downstream
failure triggers rollback (NOP v0.6):

```
COMPLETED ──→ COMPENSATING ──→ COMPENSATED          (reversal acknowledged)
                  │
                  └────────→ COMPENSATION_FAILED    (reversal returned an error — terminal)
```

`COMPENSATED` / `COMPENSATION_FAILED` are terminal saga states; they do NOT change the parent
task's outcome (which remains `FAILED`) but MUST be reported in the final callback payload so
callers can audit which side effects were reversed.

**Cancellation:** the Orchestrator may cancel a running task by (a) disconnecting (Worker Agents MUST detect connection close and stop), (b) sending `DelegateFrame(action="cancel")`, or (c) calling `system.task.cancel` via NWP. On receiving a cancel signal, Worker Agents MUST emit `AlignStream(is_final=true, error.code="NOP-TASK-CANCELLED")`.

---

## Hard Limits Summary

| Limit | Value | Error code |
|-------|-------|-----------|
| Max DAG nodes | 32 | `NOP-TASK-DAG-TOO-LARGE` |
| Max delegation chain depth | 3 (Orchestrator + 2 hops) | `NOP-DELEGATE-CHAIN-TOO-DEEP` |
| Max task timeout | 3,600,000 ms (1 hour) | `NOP-TASK-TIMEOUT` |
| Max CEL condition length | 512 characters | `NOP-CONDITION-EVAL-ERROR` |
| Max `input_mapping` JSONPath nesting | 8 levels | `NOP-INPUT-MAPPING-ERROR` |
| `callback_url` scheme | `https://` only; SSRF-validated | `NOP-TASK-DAG-INVALID` |

---

## Error Codes

| Error Code | NPS Status | Description |
|------------|------------|-------------|
| `NOP-TASK-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | `task_id` does not exist |
| `NOP-TASK-TIMEOUT` | `NPS-SERVER-TIMEOUT` | Overall task timeout exceeded |
| `NOP-TASK-DAG-INVALID` | `NPS-CLIENT-BAD-FRAME` | DAG format invalid (missing root/terminal, field errors, etc.) |
| `NOP-TASK-DAG-CYCLE` | `NPS-CLIENT-BAD-FRAME` | DAG contains a cycle |
| `NOP-TASK-DAG-TOO-LARGE` | `NPS-CLIENT-BAD-FRAME` | DAG node count exceeds 32 |
| `NOP-TASK-ALREADY-COMPLETED` | `NPS-CLIENT-CONFLICT` | Task already completed; cannot resubmit |
| `NOP-TASK-CANCELLED` | `NPS-CLIENT-CONFLICT` | Task has been cancelled |
| `NOP-DELEGATE-SCOPE-VIOLATION` | `NPS-AUTH-FORBIDDEN` | `delegated_scope` exceeds parent Agent scope |
| `NOP-DELEGATE-REJECTED` | `NPS-CLIENT-UNPROCESSABLE` | Worker Agent rejected delegation (insufficient capability or overloaded) |
| `NOP-DELEGATE-CHAIN-TOO-DEEP` | `NPS-CLIENT-BAD-PARAM` | Delegation chain depth exceeds 3 levels (at most 2 DelegateFrame hops) |
| `NOP-DELEGATE-TIMEOUT` | `NPS-SERVER-TIMEOUT` | Subtask did not complete before `deadline_at` |
| `NOP-SYNC-TIMEOUT` | `NPS-SERVER-TIMEOUT` | SyncFrame barrier timed out waiting for dependent subtasks |
| `NOP-SYNC-DEPENDENCY-FAILED` | `NPS-CLIENT-UNPROCESSABLE` | Failure count exceeded K-of-N tolerance |
| `NOP-STREAM-SEQ-GAP` | `NPS-STREAM-SEQ-GAP` | AlignStream sequence number non-contiguous |
| `NOP-STREAM-NID-MISMATCH` | `NPS-AUTH-UNAUTHENTICATED` | AlignStream `sender_nid` does not match connection identity |
| `NOP-RESOURCE-INSUFFICIENT` | `NPS-SERVER-UNAVAILABLE` | Pre-flight found one or more Worker Agents with insufficient resources (CGN or capability) |
| `NOP-CONDITION-EVAL-ERROR` | `NPS-CLIENT-BAD-PARAM` | DAG node condition expression evaluation failed |
| `NOP-INPUT-MAPPING-ERROR` | `NPS-CLIENT-UNPROCESSABLE` | `input_mapping` JSONPath could not be resolved or target field does not exist |
| `NOP-COMPENSATION-FAILED` | `NPS-CLIENT-UNPROCESSABLE` | Terminal — a node's `compensate_action` returned an error during saga rollback (NOP v0.6) |
| `NOP-COMPENSATION-NOT-SUPPORTED` | `NPS-CLIENT-UNPROCESSABLE` | Terminal — a predecessor that must be compensated has no `compensate_action` and `compensation_policy="strict"` (NOP v0.6) |
| `NOP-CALLBACK-HMAC-MISSING` | `NPS-AUTH-UNAUTHENTICATED` | Callback recipient rejected delivery because `X-NPS-Signature` was absent while `callback_secret` was set (NOP v0.6) |
| `NOP-STREAM-NAK` | `NPS-STREAM-SEQ-GAP` | AlignStream gap detected; receiver issued a NAK requesting retransmission (NOP v0.6) |
| `NOP-TASK-RESULT-EXPIRED` | `NPS-CLIENT-NOT-FOUND` | Task result requested after `result_ttl_seconds` elapsed; result no longer retained (NOP v0.7) |
| `NOP-STREAM-NAK-UNRESOLVABLE` | `NPS-STREAM-SEQ-GAP` | NAK retransmission requested for a frame no longer buffered by the sender (NOP v0.7) |
| `NOP-CLAIM-CONFLICT` | `NPS-CLIENT-CONFLICT` | TaskFrame already leased by a live runner lease (CR-0007 §4.2) |
| `NOP-SPAWN-SPEC-INVALID` | `NPS-CLIENT-BAD-PARAM` | `spawn_spec_ref` could not be resolved or failed SpawnSpec schema validation (CR-0007 §5) |
| `NOP-RUNTIME-IDLE-TIMEOUT` | `NPS-SERVER-TIMEOUT` | L3 worker exceeded its idle timeout before completing the node (CR-0007 §6) |
| `NOP-RUNTIME-MAX-RUNTIME` | `NPS-SERVER-TIMEOUT` | L3 worker exceeded its max runtime before completing the node (CR-0007 §6) |

---

## L3 Runtime Integration (nps-runner, CR-0007)

> Normative contract: **NPS-CR-0007**. The NPS-Node Profile **L3 (on-demand / FaaS)** tier
> materializes an agent process only when a task arrives; the runtime that does so is the
> `nps-runner` daemon (added in NOP v0.7).

- **Task-claim protocol** — a runner atomically leases the head of a per-NID inbox:
  `{ task_id, runner_nid, lease_seconds (clamped [10,600]), dedup_key = sha256(task_id ‖ dag_hash) }`.
  *Granted* marks the task `LEASED`; the runner MUST renew the lease before expiry. A live lease
  already held by another runner yields `NOP-CLAIM-CONFLICT`. An expired lease is reclaimable, and
  the `dedup_key` ensures a side-effect-bearing node in a terminal state is not re-executed
  (at-least-once with dedup). Result reporting is idempotent, keyed by `(task_id, node_id, dedup_key)`.
- **`spawn_spec_ref` → SpawnSpec** — resolves to
  `{ image (OCI ref, required), command?, env?, resource_limits? {cpu, memory, cgn_budget}, idle_timeout_seconds?, max_runtime_seconds? }`,
  inline (`spawnspec:` base64url-JSON data URI) or an `https://`/`nwp://` URL. Resolution failure ⇒
  `NOP-SPAWN-SPEC-INVALID`.
- **Lifecycle enforcement** — idle timeout (`NOP-RUNTIME-IDLE-TIMEOUT`) and max runtime
  (`NOP-RUNTIME-MAX-RUNTIME`); SpawnSpec values take precedence over runner policy. On a terminal
  condition the runner PATCHes node status (`done → COMPLETED`, failed/idle/max-runtime → `FAILED`).
  A `FAILED` L3 node with a `compensate_action` triggers the same reverse-topological saga rollback
  as L1/L2.

---

> Last reviewed at suite version: v1.0.0-alpha.19
>
> Alpha.19 source status reconciled on 2026-09-05.
