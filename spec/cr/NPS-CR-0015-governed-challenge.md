English | [中文版](./NPS-CR-0015-governed-challenge.cn.md)

# NPS-CR-0015: Governed Action Challenge

- Status: Draft
- Target: v1.0.0-alpha.20
- Date: 2026-09-09
- Author: Codex
- Discussion: [#127](https://github.com/labacacia/NPS-Dev/issues/127)
- Trace: EPIC-007, ADR-004 D20-03, A20-R009/R013/R014
- Depends on: CR-0013 context binding and CR-0014 outcome semantics

## 1. Summary

Normative integration: NWP §7.7 and the fixed challenge Anchors refine stable authority
namespaces, exact retention deadlines, management-action restrictions and typed Caps
results. These details supersede underspecified record/response details below.
Runtime, concurrency and recovery qualification remain pending.

An optional NWP action profile pauses a logical mutation for product-owned approval, then admits it
through durable operation deduplication. The opaque handle identifies server state, not permission.
Reserve nps.governed_challenge revision 1 (experimental). No new frame type or universal exactly-once
promise is introduced by this draft.

## 2. Motivation

Passport has parameter hashing, expiry and consumed/denied state, but one state flag cannot prove
an external side effect executed once. Clients need interoperable pending/terminal behavior while
products retain approval methods, UI, account policy and transactional storage.

## 3. Proposed contract

### 3.1 Binding and initial action

A governed operation requires authenticated subject, logical audience/target, action ID and a
nonempty idempotency key (1..128 UTF-8 bytes). Existing ActionFrame carries the operation.
An opted-in server validates parameters and authorization before creating state. Missing key
rejects before creating challenge or effect.

Bind issuer authority, subject, action, audience, frozen accepted parameters, operation key, expiry
and selected feature revisions. The operation digest uses CR-0013 logical request digest rules,
even when the context is native and no projection header exists. Challenge metadata and proof
handle are excluded from the original operation digest; original business parameters are immutable.

Durable operation key is (authority,subject,audience,action,idempotency_key). Same key and same
digest returns the existing pending/terminal operation. Same key and different digest conflicts
without creating a second challenge. Authorization is rechecked on every call, including results.
Store the initial operation and challenge atomically before returning a handle.

### 3.2 Challenge response

The standard Caps response carries a profile-defined JSON record in data with its registered
Anchor schema; it is not arbitrary JSON substituted for a Caps payload. HTTP may use 202 for pending;
native uses the same Caps record. Fields:

| Field | Type / rule |
|---|---|
| challenge_id | opaque unpadded base64url of 32 random bytes |
| state | pending / approved / executing / completed / denied / expired / superseded / indeterminate |
| action_id | original registered action |
| audience | original logical target |
| request_digest | sha256: plus 64 lowercase hex digits |
| expires_at | integer Unix seconds; before execution admission |
| methods | unique product method identifiers, at most 8; each at most 64 ASCII bytes |
| operation_id | opaque server operation identifier, at most 128 bytes |
| result | only for completed; original action's typed result |
| error | bounded original/action error or profile error; never approval credentials |

Subject/authority binding remains server-side; do not expose private policy/account details.
TTL defaults to 300 seconds, maximum 900, server clock authoritative. Persist result/deduplication
records for at least 86400 seconds after terminal transition and publish the exact retention period.
A client MUST NOT automatically retry after the advertised retention window.

### 3.3 Protocol actions

Register nwp.challenge.status, nwp.challenge.resume and nwp.challenge.cancel using existing NWP
action discovery, ActionFrame and Caps responses. Params contain only challenge_id; original frozen
operation is loaded from state. No reusable OTP, approval token or user password travels in these
actions. Each action requires the same authenticated subject/audience and product permission.

status returns current state without effects. resume requires approved state, rechecks current
grant/revocation/scope, original digest and expiry, then atomically claims approved -> executing.
Repeated resume returns the same operation state/result; it never creates another execution.
cancel transitions pending/approved -> denied atomically. Cancel after executing reports current
state and cannot claim rollback. Unknown or wrong-subject handles return the same bounded not-found
error. Handle knowledge does not let another subject view, approve, resume or cancel.

Human/step-up systems update pending -> approved or denied via product-owned authenticated flows.
They cannot modify frozen parameters or rebind the operation. Supersession ends the old challenge;
a new logical operation/key is required for different parameters. Direct execute bypass is not
allowed merely because a client omitted this profile on a policy-governed action.

### 3.4 State transitions and execution ownership

Allowed transitions:

- pending -> approved / denied / expired / superseded;
- approved -> executing / denied / expired / superseded;
- executing -> completed / indeterminate.

Completed includes a durable terminal action result or known terminal action failure. A pending
approval does not itself start execution. Expiry applies before executing; it does not undo an
already executing side effect. Terminal state is immutable except an explicit trusted reconciliation
process may resolve indeterminate -> completed with durable execution evidence. Reconciliation
never automatically repeats the mutation.

Claim ownership and durable operation record must commit atomically. If the effect shares the same
transactional store, commit effect and result together. Otherwise use an idempotent external sink
with the same durable operation identity, or retain indeterminate after an ambiguous crash.
At-most-one claim is not proof of at-most-one external effect. Concurrent callers, restart and
Anchor failover share the operation store/fencing scope; unavailable state fails closed.

### 3.5 Errors and limits

| Proposed error | Status | HTTP |
|---|---|---|
| NWP-CHALLENGE-INVALID | NPS-CLIENT-BAD-FRAME | 400 |
| NWP-CHALLENGE-NOT-FOUND | NPS-CLIENT-NOT-FOUND | 404 |
| NWP-CHALLENGE-BINDING-CONFLICT | NPS-CLIENT-CONFLICT | 409 |
| NWP-CHALLENGE-NOT-APPROVED | NPS-CLIENT-CONFLICT | 409 |
| NWP-CHALLENGE-EXPIRED | NPS-CLIENT-CONFLICT | 409 |
| NWP-CHALLENGE-STATE-UNAVAILABLE | NPS-SERVER-UNAVAILABLE | 503 |

Bound active pending challenges per subject and global storage; defaults 16 per subject and 4096
total pending/approved challenges. Capacity rejection occurs before mutation/state allocation.
Rate-limit status/resume/cancel with existing node policy. Logs contain bounded operation/state
identifiers, not approval proofs, projection headers or business parameters.

## 4. SDK changes

All six SDKs expose typed challenge state/result, status/resume/cancel APIs and explicit
indeterminate outcomes. Automatic polling must be bounded by caller deadline, server TTL and
rate hints; never auto-approve. Product approval/persistence is injected, not copied from Passport UI.

## 5. Conformance scenarios

CHL-001: initial same-key same-digest repeats return same handle.
CHL-002: same key/different parameters conflicts without effect.
CHL-003: wrong subject/audience/authority cannot use handle.
CHL-004: two approved resumes race; one durable execution claim.
CHL-005: lost resume reply returns persisted operation result on retry.
CHL-006: crash after uncertain external effect reports indeterminate without re-execution.
CHL-007: expiry before claim rejects; expiry after claim does not imply rollback.
CHL-008: revocation/scope change before resume rejects.
CHL-009: cancel races resume through atomic state transition.
CHL-010: approval cannot modify frozen parameters or grant authority by handle alone.
CHL-011: shared store/fence prevents duplicate claims across restart/failover.
CHL-012: storage unavailable or pending quota reached fails before effect.
CHL-013: no-profile caller cannot bypass product approval policy.
CHL-014: result retention boundary prevents unsafe automatic replay.
CHL-015: status/error/logs do not leak other subject's challenge or approval secrets.
CHL-016: standard Caps/schema encoding passes six-SDK decoding.

## 6. Migration

Map Passport state/methods into this profile only after durable concurrency/recovery proof.
Retain product approval UX and notification channels. Adapters translate old challenge models at
an explicit boundary; existing products are not assumed compliant from their state names alone.

## 7. Out of scope

Cross-node portable signed approval receipts, UI/MFA protocols, business transaction coordination
implementation, forcing all actions to use approval, universal exactly-once network delivery.

## 8. Acceptance

- [ ] EN/CN action/schema/error and feature registry integration.
- [ ] CHL-001..016 executable vectors and concurrency/crash tests.
- [ ] Six-SDK behavior and Passport reference flow.
- [ ] Durable operation retention, replica failover and reconciliation evidence.

## 9. Proposed changelog

Add governed action challenge handles and portable state/resume semantics with durable deduplication
and explicit uncertain outcomes.

## 10. Follow-up

P20-108 freezes actual Anchor schemas/hashes and vectors. Product store implementation must prove
the selected transactional/idempotent-sink strategy before claiming single execution.
