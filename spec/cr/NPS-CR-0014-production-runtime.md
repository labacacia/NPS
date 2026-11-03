English | [中文版](./NPS-CR-0014-production-runtime.cn.md)

# NPS-CR-0014: Bounded Production Runtime

- Status: Draft
- Target: v1.0.0-alpha.20
- Date: 2026-09-09
- Discussion: [#126](https://github.com/labacacia/NPS-Dev/issues/126)
- Trace: EPIC-007, ADR-004 D20-04, A20-R009..R012
- Author: Codex

## 1. Summary

Normative integration: NCP §7.8, NDP §7.9 and NOP §3.6 define runtime responsibilities,
fresh discovery/admission and explicit execution certainty. The profile remains
experimental; SDK and cross-product qualification are not complete.

Specify portable request outcome, retry, bounded dispatch and session lifecycle contracts above
existing NCP/NIP/NDP/NOP primitives. Reserve feature nps.production_runtime revision 1,
experimental until shared qualification. This draft does not replace existing wire definitions.

## 2. Motivation

Ivy implements correlation, queues and cancellation above a raw NcpSession stream; Kernel implements
its own connect retry/session states. Deleting these loops before the SDK exposes their required
semantics would remove working behavior. Shared outcomes must distinguish retryable connection
failure from uncertain business execution.

## 3. Proposed contract

### 3.1 Public responsibilities

Six SDKs expose equivalent capabilities with language-idiomatic types:

| Component | Contract |
|---|---|
| SessionFactory | Connect, negotiate, admit identity; return immutable peer/feature/security binding |
| SessionPool | Acquire by full security key with bounded pending queue; release only healthy sessions |
| InvocationDispatcher | Register correlation before write, send typed frames, dispatch exactly one local terminal result |
| RetryDecision | Pure decision from dispatch certainty, operation contract, deadline and remaining budget |
| DrainController | Stop admission, bounded wait/cancel, close with unresolved-operation evidence |

No caller receives a generic setter for admitted authority. Low-level stream APIs may remain,
but bypassing the managed dispatcher forfeits this profile's guarantees and must be explicit.

### 3.2 States and outcomes

Session states: connecting -> negotiating -> admitting -> active -> draining -> closed.
Any establishment failure closes its resources; closed sessions cannot return to a pool.
Reconnect produces a new session and fresh NIP admission, not authorization resumption.

Invocation states: queued -> writing -> awaiting -> completed, with rejected-before-dispatch,
cancelled-before-dispatch, expired-before-dispatch or indeterminate terminal alternatives.
A transport failure during/after the first write attempt is indeterminate unless positive server
evidence proves no dispatch. Bytes written locally or received by TCP do not prove execution.
A valid correlated remote terminal result establishes a known remote outcome. Repeated identical
terminal results are ignored; conflicting terminal results close the session and report a protocol
fault without invoking the application completion callback again.

Maintain a bounded terminal-correlation tombstone set for the remaining session lifetime. When
its configured capacity is reached, drain/reconnect rather than evicting and risking ID reuse.
No correlation ID may be reused within a session. Unknown correlation IDs close the session.
Draining preserves tombstones until close. A late terminal after local cancellation may update
diagnostic evidence but cannot reverse the already-delivered local terminal outcome.

### 3.3 Resource limits

Default local operational limits (applications may configure other finite positive values):

- connect 5 s; handshake 5 s; admission 5 s;
- total request deadline 30 s including queue/write/wait and retries;
- drain 30 s; pool acquisition uses the request's remaining deadline;
- at most 8 pooled sessions per complete pool key, 64 total sessions and 256 pending acquisitions;
- at most 32 queued frames and 1 MiB queued payload bytes per session; whichever limit is hit first;
- terminal tombstone limit 4096 per session.

Negotiated payload/concurrency ceilings always bound local settings. Keepalive/idle rules remain
NCP §7.6/7.7; this profile adds no conflicting heartbeat negotiation. All waits/callbacks honor
remaining deadlines. Queue saturation rejects before dispatch, without hidden unbounded buffers.
For streaming, stop accepting new frames before local buffers overflow and use the protocol's
existing flow control. A peer exceeding the negotiated window fails the session.

The pool key includes endpoint identity, admitted subject, credential generation, assurance/scope
policy fingerprint, selected features and encoding/security profile. Different tenants or delegated
scopes cannot share a session merely because host/port matches. Credential revocation or rotation
invalidates affected pool entries. Bound the total number of keys by the global session/queue limits.

### 3.4 Retry decision

| Evidence | Automatic retry |
|---|---|
| Before any write attempt, no dispatch | Allowed within total deadline and attempt budget |
| Explicit authenticated remote rejection proving no dispatch | Allowed only if operation/profile allows retry and rejection is transient |
| Unknown outcome for read-only operation declared replay-safe | Allowed only under that operation's freshness/cost semantics |
| Unknown outcome for mutation without durable deduplication proof | Never |
| Unknown outcome with server-declared durable idempotency contract | Only same principal/operation key/digest within server retention and permitted target scope |
| Known remote terminal result | Do not replay automatically |

Default attempt budget is 2 total attempts, not 2 retries. Default delay is uniformly sampled
between zero and min(1000 ms, 100 ms * 2^(retry_index-1)); delay consumes the original deadline.
An injected random/clock interface makes tests deterministic. A retry requiring sleep beyond the
remaining deadline terminates. Server retry hints may lengthen delay only within remaining budget.
Reconnect success never proves an earlier operation unexecuted. Failover retries require the same
durable deduplication scope at the new owner; otherwise return indeterminate.

### 3.5 Cancellation and drain

Cancellation before first write prevents dispatch. During/after writing it stops local waiting and
propagates only the cancellation mechanism defined by the negotiated operation/NOP profile.
No invented cancellation frame is allocated here. Without remote rollback/terminal evidence the
outcome remains indeterminate; cancellation does not claim that side effects were undone.

Drain rejects new acquisitions/operations, waits for admitted work within the drain deadline,
requests supported cancellation, then closes and reports unresolved mutations as indeterminate.
A receiver must not advertise draining as proof that previously written operations were unexecuted.
One local completion per invocation applies to timeout/cancel/close races.

### 3.6 Failover and observability

On connection loss, re-resolve active Anchor, honor monotonic cluster_epoch fencing and then
negotiate/admit anew. Clear route/capability caches that belong to the old peer. Do not use a stale
owner or transport resumption ticket as authorization. Never replay an uncertain mutation across
owners without shared durable operation evidence.

Emit connection/negotiation/queue/attempt/retry/reconnect/cancel/drain/failover counters and durations,
outcome certainty and bounded request/correlation identifiers. Do not use subject/scope/operation
keys as unbounded metric labels or log credentials, projections or request bodies.

### 3.7 Error proposals

Runtime API errors are typed outcomes, not automatically new wire error assignments:
queue-full, deadline-before-dispatch, cancelled-before-dispatch, indeterminate,
correlation-conflict and session-closed. Map existing protocol errors without erasing their
original code. Wire additions, if necessary for a specific operation, require its own registry entry.

## 4. SDK changes

.NET, Python, TypeScript, Go, Java and Rust implement the public responsibilities, immutable session
selection, bounded resources and outcome certainty. Reuse existing frame/identity primitives.
Product LLM/result mapping stays outside the generic dispatcher. Retain legacy frame-log readers
when removing Kernel primitives; do not change persisted data silently.

## 5. Conformance scenarios

RUN-001: queue saturation rejects without write.
RUN-002: write/response loss for mutation returns indeterminate without blind retry.
RUN-003: same-key same-digest retry is allowed only with durable retention evidence.
RUN-004: different principal/scope/credential generation never shares a session.
RUN-005: cancel/timeout/remote-result races produce one local completion.
RUN-006: duplicate terminal ignored; conflicting terminal/unknown correlation closes.
RUN-007: tombstone capacity triggers drain, not unsafe eviction.
RUN-008: connect/handshake/admission failures dispose all resources.
RUN-009: retry jitter and hints consume the original deadline/attempt budget.
RUN-010: stream/window/byte limits hold under a slow consumer.
RUN-011: drain rejects new work and reports unresolved mutations truthfully.
RUN-012: stale Anchor and changed epoch require resolution/identity admission.
RUN-013: failover without shared deduplication cannot replay uncertain mutation.
RUN-014: metrics/logs have bounded labels and no credentials/projections.
RUN-015: base clients without negotiated profile retain existing semantics.

## 6. Migration

First ship managed runtime APIs and vectors; then converge Ivy/BKE/Kernel incrementally.
Retained low-level paths require explicit exception and rollback scope in P20-409.
Defaults are finite operational policy, not a promise that all deployments have equal latency.

## 7. Out of scope

Product transaction implementation, consensus algorithm, universal exactly-once delivery,
new heartbeat/cancel frame types, automatic retry of every exception.

## 8. Acceptance

- [ ] EN/CN normative integration and compatibility/version review.
- [ ] RUN-001..015 deterministic shared vectors, fake clocks and dispatch counters.
- [ ] Six SDK parity and bounded memory/queue behavior demonstrated.
- [ ] Cross-product cancellation, reconnect, drain and failover qualification.

## 9. Proposed changelog

Add bounded managed runtime contracts with dispatch certainty, safe retry decisions and
fresh admission after reconnect/failover.

## 10. Follow-up

Operation-specific cancellation and durable deduplication contracts must be linked during
normative integration; where absent, the runtime fails conservatively rather than inventing support.
