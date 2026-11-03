English | [中文版](./data-results.cn.md)

# NWP Data Result Resources — revision 1

- Status: Candidate contract, experimental and disabled by default.
- Date: 2026-11-02.
- Target: alpha.20; [CR-0016](../cr/NPS-CR-0016-data-results.md).
- Trace: [D20-00 #164](https://github.com/labacacia/NPS-Dev/issues/164), #129.
- Exact machine contract: [data_result_contract.json](../conformance/alpha20/data_result_contract.json).
- This contract freezes semantics for implementation; it does not assert an available SDK or runtime.

## 1. Boundary and admission

**DR-01.** Reuse ActionFrame/Params, NWM ActionSpec, CapsFrame and existing native/HTTP
bindings. No new frame, node type, endpoint path, identity algorithm or default feature
is introduced. The optional feature is `nps.data_results` revision 1 (experimental).
Both endpoints must locally enable it. Every operation below requires exact selection
and `nwp:action` plus the applicable node/action scope, followed by resource policy.

Native admission follows NCP feature selection; HTTP follows NWP §9.6 including the
all-backend route guarantee and exact success echo. Unsupported/withdrawn requirements
reject with zero handler calls. Required-feature omission is rejected on these
profile operations, not interpreted as a base action. Existing unrelated operations
keep alpha.19 behavior. Registering a feature or publishing a package does not enable it.
If an invocation projection is used, require `nps.invocation_context=1` and its full
binding. Direct verified NIP callers do not need a projected identity.

## 2. Result and snapshot

**DR-02.** A result is a finite authorized, immutable snapshot owned by the serving
issuer and principal. Its descriptor contains `result_ref`, `issuer_nid`,
`snapshot_id`, `schema_ref`, `row_count`, `created_at`, `expires_at`,
and `retention="process"`. Times are integer UTC Unix seconds; references are
opaque random locators of at least 128 bits of entropy. They are not credentials.
Descriptor shape is a profile payload inside CapsFrame, never a replacement envelope.
Full rows, credentials and bearer grants are forbidden in the descriptor.
The descriptor and counts themselves require authorization.

`snapshot_id` binds the actual captured records, stable order, source version and
schema. A schema hash identifies structure only. Create materializes an authorized
source snapshot atomically or fails; a source that cannot provide consistent capture
is unsupported. Ordinary NWP pagination is not proof of a captured snapshot.
Hash generation uses existing JCS rules where a schema Anchor is required; no new
canonicalization algorithm is defined.

## 3. Operations

| Action ID | Required parameters | Result |
| --- | --- | --- |
| `result.create` | advertised source ID and existing query/filter/fields/order subset | descriptor only |
| `result.read` | result_ref, snapshot_id, schema_ref, bounded limit; optional fields/cursor | descriptor plus bounded ordered rows and next cursor |
| `result.aggregate` | result_ref, snapshot_id, schema_ref and existing aggregate operations/group_by | bounded aggregates plus source descriptor/provenance |
| `result.release` | result_ref | owner-authorized idempotent release receipt |

**DR-03.** The selected profile fixes operation meanings; implementations advertise
only supported source IDs and compute subsets in NWM. Unknown operations/unsupported
queries reject before source access, never silently approximate a result.
No source URL/SQL/script supplied by the caller is executed.
Create has no default preview. Read requires a positive limit no greater than 1000;
a page is no larger than 65536 canonical JSON UTF-8 bytes before wire compression.
A single oversized row fails explicitly, rather than returning a truncated record.
Schema and provenance remain available without forcing bulk data into model context.

Aggregation reuses existing NWP operations; its predicates run over authorized
records and visible fields only. Group keys, counts and aggregate output are protected
data. Money uses exact integer minor units or the declared decimal representation;
rates/rounding do not alter the underlying exact reference counts.
Any future derived result retains source provenance, cannot outlive the earliest
source and cannot broaden access. This revision does not introduce a general query language.

## 4. Policy, delegation and model egress

**DR-04.** Authentication must come from a verified native session or HTTP verifier,
not the X-NWP-Agent header, a model claim, unsigned metadata or a copied handle.
Tenant/resource bindings come from trusted service policy. Every operation, including
metadata, replay receipts and aggregate inspection, rechecks current authority.

**DR-05.** Resource authorization stays in the result-serving policy boundary.
NIP supplies verified subject, capability and constrained delegation evidence.
TrustFrame is CA-to-CA trust; it is not a result grant. A worker needs an explicit
server-owned, recipient-bound resource grant as well as its own verified NIP
identity/node/action scope. Allowed operations and fields are intersected with the
live grantor/source policy; row predicates can only be narrowed by adding restrictions.
This revision does not permit recursive result-grant delegation.

Owner/source revocation immediately blocks subsequent reads of retained or derived
results and ends a continuing stream at its next authorization boundary; buffered
data already delivered cannot be retracted. A source permission change does not silently
rewrite the captured snapshot. If its current policy is no longer compatible, deny.
Policy/verifier unavailability fails closed. Aggregate/metadata errors cannot become
an existence or cross-tenant oracle.

**DR-06.** Data inspection for external model input requires an explicit service-side
egress decision in addition to read permission. The model's request is not that decision.
Results default to runtime-only with zero preview rows. A bridge cannot strip or
invent tenant/authority; projections require their own negotiated revision and verifier.
Audit events record principal/action/outcome and bounded identifiers, not full rows,
prompts, credentials or signing material.

## 5. Retention, capacity and retry

**DR-07.** The reference profile promises process-local retention only. Default TTL
is 300 seconds, maximum 900; expiry is exclusive (`now >= expires_at` is expired).
Remaining TTL never increases on clock rollback. An issuer restart, unproven successor
or release makes the result unavailable; there is no implied durable storage or
cross-node recovery. Re-query creates a new snapshot explicitly, never a silent replay.
A service may claim durable recovery only under a separately qualified contract.

Reference bounds are: 16 live results per owner; 100,000 rows and 32 MiB per result;
128 MiB total retained payload. Charge all retained row/descriptor/cursor and dedup
state or provide a separate bounded overhead allowance. Enforce global and per-owner
limits atomically before publish; reject capacity rather than evict a live promised
result. Expired state may be removed. A request can lower but cannot raise these limits.

**DR-08.** Create uses a caller/issuer/action-bound idempotency key and frozen logical
request digest. Same key/content returns the existing authorized descriptor until
its expiry within the same process; changed content conflicts. Release is owner-bound
and idempotent within this window. Expiry/restart does not authorize replay of external
mutations. No operation in this revision performs an external business mutation.
Timeout/cancellation after capture may leave a retained result; report uncertainty or
recover it by the same key, rather than claiming no allocation occurred.

## 6. Cursor, delta and recovery

**DR-09.** A cursor binds issuer, result, snapshot, schema, principal, policy version
and offset. Validate its binding before returning rows. Source updates cannot move a
cursor to a live/new snapshot. Changing fields/filter invalidates a cursor rather
than skipping/duplicating records. Row order is fixed at creation with a stable
record-ID tie-break. Wrong snapshot/schema/base returns conflict and requires explicit
rebase. Unsupported delta encoding is rejected; a snapshot is not a live subscription.
NCP stream/delta qualification remains D20-04; this document does not assert it.

## 7. Error and compatibility mapping

Use existing ErrorFrame and NPS status classes. Action validation uses
`NWP-ACTION-PARAMS-INVALID`; feature rejection uses its existing feature error.
Other profile failures use the existing bounded action error envelope. The six
NWP-RESULT codes below are registered in the paired error/status tables; fixture
labels map to these exact wire codes in data_result_contract.json.

| Fixture label | Wire error code | NPS status / HTTP |
| --- | --- | --- |
| INVALID_PARAMS | NWP-ACTION-PARAMS-INVALID | NPS-CLIENT-UNPROCESSABLE / 422 |
| UNAUTHENTICATED | NWP-RESULT-IDENTITY-REQUIRED | NPS-AUTH-UNAUTHENTICATED / 401 |
| RESULT_UNAVAILABLE | NWP-RESULT-NOT-FOUND | NPS-CLIENT-NOT-FOUND / 404 |
| SNAPSHOT_MISMATCH | NWP-RESULT-SNAPSHOT-MISMATCH | NPS-CLIENT-CONFLICT / 409 |
| CAPACITY | NWP-RESULT-CAPACITY | NPS-LIMIT-RESOURCE / 429 |
| POLICY_UNAVAILABLE | NWP-RESULT-POLICY-UNAVAILABLE | NPS-SERVER-UNAVAILABLE / 503 |
| KEY_CONFLICT | NWP-RESULT-REPLAY-CONFLICT | NPS-CLIENT-CONFLICT / 409 |
| FEATURE_UNSUPPORTED | NWP-FEATURE-REQUIRED-UNSUPPORTED | NPS-CLIENT-CONFLICT / 409 |

Errors contain no record values, other-tenant occupancy or credential data.
Only authorized callers receive snapshot mismatch details.

**DR-10.** Emit no profile advertisement until the advertised operation/binding is
implemented and qualified. Gate tests exercise actual client/server routes before
claiming zero dispatch. An oracle of the contract remains design evidence, not those
runtime tests. Compatible clients reject required unsupported features; optional
unselected operation paths keep base behavior and never treat references as full rows.

## 8. Verification and promotion

[Portable scenarios](../conformance/alpha20/data_result_vectors.json) cover
admission, identity/scope, metadata, row/field restrictions, expiry, source and grant
revocation, delegation, cursor/snapshot mismatch, process loss, capacity and replay.
D20-02/03/04/08 must execute the same invariants through public APIs and both claimed
bindings. Frozen JSON/UTF-8 bytes and a contract oracle establish design consistency
only; six-SDK, bridge, real-provider and failover evidence remain required.

[D20-00 gate ledger](../conformance/alpha20/data_workflow_gates.json) retains all
12 alpha gates, 12 separate beta gates and 57 prior P20 tasks; none is waived.
[Preregistered methodology](../conformance/alpha20/data_workflow_methodology.json)
fixes the task and scoring before measurement; unconfigured model/provider/budget
fields block real-provider execution, not permission to fabricate measurements.
