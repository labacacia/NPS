# D20-06 result handoff implementation

Status: COMPLETE for experimental bounded reference AC. Epic #163 / #170; continuous implementation authorized.
[Final AC/evidence](2026-11-03-nop-reference-ac-evidence.md) supersedes historical pending checkpoints below.
D20-05 scoped reference AC is COMPLETE at897fd41, normal CI41566234176 all10jobs pass.
Security advisories remain unwaived for D20-08; no release qualification follows.

Reuse the existing NopOrchestrator entry point, Task/Delegate/AlignStream models,
DagValidator and NopInputMapper. A host-selected RuntimeBinding selects the strict
experimental path; null leaves legacy behavior unchanged. The reference executes
at most64 nodes serially, with64KiB task/stream bounds,16frames per operation,
256KiB retained outputs and an original task deadline of at most120seconds.
Callbacks, preflight, conditions, K-of-N and compensation actions are rejected before
dispatch in this narrow path; do not advertise complete production_runtime support.

| AC | Change / verification | State |
| --- | --- | --- |
| Stable operation identity and duplicate outcomes | Atomic caller/task key plus immutable constrained canonical digest; persist random UUID node/subtask/operation IDs before dispatch; pending duplicate returns running, terminal duplicate returns first result | Initial23runtime/fault tests pass |
| Truthful certainty and retry | Independently attested peer, exact task/subtask/stream/contiguous sequence and final-only outcome; only positive not_dispatched may retry within original budget; unknown stops downstream | Initial23tests include14invalid-evidence cases |
| Restart, cancellation and terminal race | Reference SQLite host journal, exclusive local owner, permanent bounded tombstones, terminal CAS; restart pending becomes indeterminate; ignored cancellation cannot extend deadline | Initial23tests include crash-after-admission, ignored cancellation and late effect |
| Real authorized result handoff | Query -> analysis -> report through two actual mTLS/NIP worker identities and least-privilege result grants; ownership and release explicit | Pending |
| Failure and resource evidence | Unauthorized/expired/revoked grants, partial result loss, bounded deadline/RSS and exact resource/descriptor/model-visible byte accounting | Pending |
| Reproduction, review and EN/CN docs | Existing sample/demo structure; exact source/toolchain/fault receipts, high-risk independent review, full.NET and frozen contracts | Pending |

The journal interface isolates persistence from NOP SDK core. SQLite adapter belongs
to the existing NopDag host sample. It supports one exclusively owned local database,
256 permanent task records with no eviction, bounded busy timeout and FULL synchronous
transactions. It does not promise shared replicas, network filesystems, durable result
snapshot migration or exactly-once external effects. Existing process-local resource
snapshots/grants are separately checked at each use and do not survive issuer loss.

Runtime output never proves authority by itself: authenticated transport must bind the
actual worker, and result operations independently enforce current NIP/resource policy.
Descriptors remain locators; transparent task context and empty delegated-scope placeholders
cannot grant access. Model calls are unnecessary for deterministic composition; no new
paid calls are planned. The original USD10 ledger remains unchanged after D05.

Next: complete actual worker handoff/fault/resource evidence, review and document each
supported boundary before D20-06 closure; then D20-07 and D20-08 in dependency order.

## Current verification

39 focused tests pass after the initial review fixes: runtime evidence/replay/cancellation/
restart/capacity/expiry checks plus real100/1000/100000 mTLS worker handoff and six
negative handoff scenarios. Full.NET1636/1636 passes, including additional null-input guards
and precancelled pending/unknown replay assertions. Source-byte/feature/spec-version checks pass; no matrix or default feature offer
is changed. Actual physical-resource capture and published cost distributions remain
pending; D20-06 stays IN_PROGRESS.

The SQLite native pin is a minimal scope-supporting change tied to the new durable host
journal AC: official NuGet supplies bundle3.0.5, while GHSA-2m69-gcr7-jv3q affects the
otherwise selected2.1.11 native library. The sample resolves the new bundle; full tests
verify compatibility. Existing suite advisory/security/registry/P20 obligations are not
waived or marked passed by this local dependency correction.

## Preregistered capture increment

The [paired controlled method](../benchmarks/nop-result-handoff-method.md) declares
16direct asserted conditions before capture: three scales/three positive repeats and
seven single fault cases. Actual post-create audit503 now preserves client OutcomeUnknown
and retains the original unresolved allocation key without cleanup replay. Exact per-
fault dispatch/capture/handler/release/error checks pass. Full.NET1637passes before the
legacy contractless null-member guard;41focused checks pass including that additional
wire guard. PR206/105be801 normalCI41568734181 all10jobs PASS.

The capture tool/entry/recipes are committed before any measurement. Current capture
status: NOT_STARTED; D20-06 remainsIN_PROGRESS. Closed verification checks the complete
fixed matrix, actual backend controls/OS affinity/process/profile bindings, source/
assembly/dependency hashes, exact oracle/provenance/five distinct rows, specific failures
and observed cost bounds. Independent high-risk review findings are fixed and checked;
no new provider call or ledger change. Physical evidence and its final AC decision follow
this preregistration, not an inferred pass from a controller implementation.

## First controlled capture and correction

PR207/d4fbf5e normalCI41571306303 all10jobs PASS; full.NET1637 before the final
compatibility guard and41focused checks including it. The first complete16-condition
actual capture passes every functional assertion but fails one resource-counter
observation at descendant exit. All16raw cases, receipts and source/assembly hashes
remain [published](../benchmarks/runs/nop-handoff-d4fbf5e-attempt-01/index.json); the
matrix is NOT resource-qualified. Largest observed high-water sum274300928bytes
does not erase a counter failure.

500-child diagnostics confirm kernelESRCH/VmRSS exit races; the immediate correction
still fails, while a bounded1ms terminal-state confirmation has zero diagnostic errors.
Only confirmed terminal/disappeared descendants may be skipped. Missing live/root/
unreadable counters stay sticky failures. Seven new regressions/full demo94pass;
failed-packet verifier regressions preserve passed:false. Original4CPU/320MiB/512MiB/
120s/10ms limits remain unchanged. This shared sampler correction minimally supports
the currentD06resource AC and does not change archived D05measurement recipes.

Next: commit the correction before a new complete16-condition capture in a new output
directory. Preserve first failure; never replace one case, relax limits or claim a
pass from free diagnostics. D20-06 remainsIN_PROGRESS. No provider calls/ledger changes.

## Kernel-v2 preregistration

The complete second v1 matrix atce2e483 also passes16functional assertions but fails
one1000-row exit counter. Both full matrices remain failed/published. Stop empirical
delay extensions and selective retries. Adopt the separately scoped kernel-v2
[decision](../adr/2026-11-03-nop-reference-memory-accounting.md): mandatory full-current-
image controller HWM plus complete exact-child wait4 maximum, conservative pair bound,
same limits, trusted frozen no-subprocess topology and all-thread scope checks.
Only child procfs gaps are optional visible diagnostics; root/scope/kernel/deadline
failures remain qualification failures. Raw pre-exec SELF history is preserved separately;
no current-image baseline subtraction/reset or exec occurs.

14tool tests pass; both old packets verify16functional cases with passed:false.
PR208.NET1638/D05demo94pass; missing indexed typed logs caused its feature-oracle CI
failure and are now explicitly included, not ignored by the verifier. No new NPS
kernel-v2 capture has run before its source commit. D20-06 remainsIN_PROGRESS.

## Final experimental reference qualification

Committed7c4d890 kernel-v2 all16conditions PASS, conservative bound275349504bytes.
Closed source/oracle/kernel/actualCPU/heap/affinity/PID/cost packet independently verifies.
Both v1failures and all16kernel-v2exitprocfsdiagnostic gaps remain visible. CI20941576423537
all10PASS (.NET1638,94demo,14tools). See the paired observation and finalACledger for
exact boundaries/counters. Additional current-packet integrity guards are added with
publication evidence. D20-06 reference AC COMPLETE; nextD20-07 thenD20-08, no newmodel/
budget/merge/projection/publication or security/registry/P20/generic-resource waiver.
