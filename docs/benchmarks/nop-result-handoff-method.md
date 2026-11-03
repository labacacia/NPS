English | [中文](nop-result-handoff-method.cn.md)

# NOP result-handoff reference qualification method

Trace: D20-06 #170 / Epic #163. This method qualifies a bounded reference workflow;
it is not an efficiency comparison or a provider-cost experiment. Commit the complete
executor, fixture, capture tool and [fixed method](nop-result-handoff-method.json)
before the first capture. Never edit measured source or overlap local builds/tests
while capture is running. Preserve every failed/partial attempt in its own directory.

The four-node DAG is orders/refunds -> analysis -> report. Two host-bound roles use
distinct mTLS/current NIP identities for actual public gateway/receiver data requests.
Alice owns snapshots; Bob receives exact snapshot/field/aggregate-only grants. The
report reads five source rows; Alice revokes grants/releases known results. Role
execution dispatch is local, so remote NOP/AlignStream transport is not qualified.
A post-create audit503 explicitly exercises client OutcomeUnknown; the original
creation key/parameters remain unresolved for a separate owner decision. Cleanup
never automatically replays that mutation or asserts rollback.

Sixteen conditions:100/1000/100000 rows with three positive repeats each, plus seven
single100-row faults: missing grant, revoked grant, hidden field, revoked worker,
expired input, lost query terminal, and actual post-create audit reply failure.
The independent frozen D20-01 oracle checks exact counts/sums. Failed/unknown
inputs cannot run a report. Every condition checks the duplicate's zero new dispatch
and its immutable parent outcome. The runtime fault suite separately checks restart,
missing database/schema, cancellation/late results, capacity/retention and bad frames.

Each condition runs one fresh direct asserted .NET fixture process with no VSTest
parent. The controller installs actual four-CPU affinity and DOTNET_PROCESSOR_COUNT4,
320MiB managed heap hard limit,512MiB actual controller+all-live-descendant physical
RSS/conservative live VmHWM sum and120s entire-case deadline. Sampling is10ms and
violations are sticky. Actual child OS affinity, backend CPU/GC bytes and process IDs
are bound into the evidence. No forcedGC or pool reset is introduced. Fresh processes
are explicit functional qualification conditions, not warm-service performance data.

Record separately:

- Actual serialized local Delegate/AlignStream JSON bytes. They are not remote NOP wire bytes.
- Both public-client/gateway and gateway/receiver TCP stream bytes, including HTTP/TLS and all negative/cleanup calls; excluding TCP/IP packet overhead.
- Fixture wall/CPU/allocation counters, whole actual process-tree memory guards and original source/assembly/dependency hashes.
- Model-visible bytes0 because this deterministic plan makes no model request. No token usage, cache hit, model latency, billing or savings is inferred.
- Exact per-scenario dispatch/allocation/handler/release/unresolved counts, error and execution certainty. Successful reporting does not prove universal reliability or production readiness.

From clean canonical NPS-Dev, after committing/preregistering these sources:

```bash
dotnet build impl/dotnet/tests/NPS.Tests/NPS.Tests.csproj -c Release --no-restore
python3 tools/benchmarks/nop-result-handoff.py --execute /absolute/fresh/owned/capture-directory
python3 tools/benchmarks/nop-result-handoff.py --packet /absolute/fresh/owned/capture-directory
```

The output must be a new absolute directory outside tracked sources. Execution checks
source cleanliness and assembly/dependency drift, captures each actual process,
retains typed logs and failed records, then closes the packet. Verification requires
only standard Python, no SDK, provider key, original private ledger or network.
The closed packet includes inert measured recipes, frozen oracle, actual dependency
manifest, raw observations, executed receipts, resource records and a hash index.
Ephemeral keys, credentials, server configuration and SQLite files are not exported.
Hash provenance is an integrity/reproduction record, not a cryptographic attestation.

All16conditions and exact negative assertions must pass before this reference resource
AC is COMPLETE. Every failed attempt stays published with its reason. Broad native/
remote/six-SDK/production runtime, original security/registry/P20 and release gates remain
separate. Existing shared DeepSeek accounting is unchanged; no model calls are needed.

## Preserved first capture and procfs correction

Source d4fbf5e completes all16 functional scenarios. The resource collector loses a
counter during one100-row descendant exit, so the full first matrix remains failed,
not resource-qualified. Its complete [closed packet](runs/nop-handoff-d4fbf5e-attempt-01/index.json)
contains all16outcomes, actual backend/assembly/resource/cost records and the failing
guard. Largest observed conservative high-water sum274300928bytes is below512MiB;
that does not erase a missing-counter failure. PR207 normalCI41571306303 all10jobs pass.

Independent500-short-child diagnostics reproduce kernel ESRCH and VmRSS removal during
exit; an immediate status recheck remains racy. A bounded1ms recheck accepts only an
actual kernel terminal/disappeared descendant. Root, permission and still-live missing
counters continue to fail closed. Seven regression checks cover these distinctions;
94demo checks pass. All [diagnostic attempts](runs/nop-counter-diagnosis-2026-11-03/attempt-01.json),
including the insufficient immediate correction, remain separate from qualification.
A new source commit must precede a new complete16-condition capture; no selective
replacement of the failed group or lowered resource limit is permitted.

Read-only integrity/functional verification of the retained failed matrix:

```bash
python3 tools/benchmarks/nop-result-handoff.py --packet docs/benchmarks/runs/nop-handoff-d4fbf5e-attempt-01 --allow-resource-failures
```

This explicitly returns `passed:false` and the failed resource condition. Default
verification still rejects failed matrices. No provider calls, ledger changes or
resource waiver occurred. D20-06 remains IN_PROGRESS pending controlled qualification.

## Method v2: full fixture-lifetime accounting

The second complete v1 matrix atce2e483 also passes16functional scenarios but loses
one1000-row exit observation. Its [closed failed packet](runs/nop-handoff-ce2e483-attempt-02/index.json)
remains failed. PR208 normalCI41573317003 has9/10passes, including.NET1638 and the
94-check demo; the fixture-oracle job correctly detects indexed typed logs missing
from Git because of ignore rules. The next increment explicitly tracks only those
reviewed one-line completion logs; closed verification remains strict.

The current [fixed method](nop-result-handoff-method.json) is now kernel-v2, separately
preregistered before any new NPS capture. The [accounting ADR](../adr/2026-11-03-nop-reference-memory-accounting.md)
defines its trusted two-process/current-controller-image boundary, exact wait4 owner,
mandatory controller VmHWM and complete fixture peak, conservative maxima sum,
visible incomplete child procfs diagnostics and unchanged resource limits. It does not
generalize to arbitrary subprocess trees. Raw inherited SELF usage remains diagnostic;
the controller never resets/subtracts its current-image baseline.

The frozen fixture never spawns processes; all observed expansions/unreadable scopes
fail. Fourteen standard-Python tests cover actual child lifetime accounting, missing/
wrong PID, zero peaks, bad units, scope expansion/unavailability, original deadlines,
root HWM failure, incomplete child diagnostics and inherited launch history. They are
verification tests, not a replacement for the full16-condition NPS matrix.

Default verification still rejects BOTH old failed matrices. Explicit
--allow-resource-failures reports16functional passes and passed:false for each. No
old outcome is reclassified by the new source or accounting boundary.
