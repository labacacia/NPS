English | [中文](schema-update-method-2026-11-02.cn.md)

# Preregistered HTTP schema/update workload extension

Date: 2026-11-02. D20-01 #165, scoped correctness support for D20-02/03.
Commit this method and harness before executing observations. The frozen D20-00
methodology, seed, original answers and earlier observations remain unchanged.

Execute all 3 scales × 2 schema conditions × 3 update fractions × 2 connection states
× 2 response compressions × 2 HTTP paths: 144 observations, 30 measured and 3 warmup
workflows each (4320 measured, 432 warmup). Same 120-second per-workflow, 512MiB
whole-process RSS and 32768-byte evidence gates. Same explicit 4-CPU/320MiB-managed
deployment, actual TLS-stream byte boundary, supported public NPS client/two-hop
gateway and direct ordinary HTTPS operator path as the prior runtime method. No model
calls, native NCP, stream/delta efficiency or per-protocol memory claim.

Run after committing a clean harness:

```sh
python3 tools/benchmarks/data-workflow-changes.py --execute --output <new-absolute-observation-directory>
```

Start each condition with the unchanged five-field frozen source and capture an old
result/page/cursor. In a sequential operator-controlled source transition, update the
first N stable record IDs by adding exactly 100 to `amount_minor`; N equals scale
times 0, 0.01 or 0.1. Retain record IDs, order, period, reason and refunded flags.
For `add-nullable-field`, append `note: null` to every row and nullable string `note`
to the existing FrameSchema. No other mutation occurs. Source version is the original
`seed-20261102-immutable-v1` for none/0; otherwise
`seed-20261102-update-{N}-nullable-{0|1}`. Cache roots are invalidated on scale or
condition changes. Timed repeated workflows use that now-immutable post-transition
source; this measures fresh snapshot workflows after a change, not live mutation
throughput, delta propagation or the transition cost itself.

Before timing, prove old page contents and old cursor continue to address the old
snapshot; a fresh capture has a changed snapshot when a mutation occurred and changed
schema exactly when nullable field was added. Old cursor against that new capture
must return canonical snapshot mismatch. None/0 is the no-change control. Verify the
new first row's amount and optional null field. After timing, inspect/release the old
result again. Old/new probe, origin/citation setup and cleanup traffic/time are excluded
from measured workflows and explicitly recorded as such.

Each measured workflow repeats create/count/filter/aggregate/read/release with the
same operators as before, preserving source/capture authorization and model egress.
Exact expected answers retain frozen counts/anomalies/citation IDs and add 100 for
each updated refunded row to its reason/period sum. An independent Python generator
and analyzer reconstruct the post-transition rows, fixture hash, supporting records
and answers; the validator requires all 144 unique configurations and all 18 actual
TRX cases passing. C/D within each condition expose byte-identical evidence; nullable
fields are included in the shared supporting row evidence. All failures/logs/raw
captures remain retained and cannot be relabeled as qualification.

Changing amount only gives a controlled arithmetic mutation without redefining the
frozen anomaly task. It does not exercise inserts/deletes/reordering, refund-status
changes or concurrent source mutation. A/B paid observations remain their recorded
original source, not provider measurements of this extension. Any broader transport
or live-model claim needs its own applicable evidence and budget accounting.

## Retained first attempt and diagnosis before rerun

The 01de64f attempt ran all 18 cases: 14 passed and 4 failed, retaining 124 complete
configuration observations in the [negative artifact](runs/2026-11-02-schema-update-320m-attempt.json).
No complete 144-condition comparison or successful memory qualification follows.
One failure explicitly reports OutOfMemoryException while transcoding the full fixture
to UTF-16 for hashing. Two measured NPS operations returned 500/NWP-NODE-UNAVAILABLE;
another fresh-create failure lacked detailed status metadata. Their causes are not
inferred from the transcoding trace. Raw logs/TRX and all failures stay retained.

Before a new clean-source attempt, compute the same source fingerprint with row-wise
UTF-8 streaming into SHA-256, and generate typed fixture rows rather than a dictionary
for every row. These remove avoidable fixture allocations, not result operations or
retained-old-snapshot checks. Record the last operation and at most 64 server exception
type names, depth at most four; never format log state/messages/stacks/scopes/request
data. Privacy tests require the formatter to remain uncalled. Keep all conditions,
repetitions and the same 320MiB managed/512MiB RSS gates; do not force garbage collection.
The next revision/attempt is distinct, with a new output directory and provenance.

The f584f47 streaming-hash attempt also executed all 18 conditions: 14 passed, 4 failed,
retaining [118 complete observations](runs/2026-11-02-schema-update-stream-hash-attempt.json).
One measured result.create 500 now has explicit server OutOfMemoryException diagnostics.
An ordinary HTTP500 and two fresh-create errors lack a retained specific cause; do not
assign one. Streaming fixture hashing alone therefore did not qualify the workload.

Before the next clean-source run, reduce shared store capture allocations: reuse row-name
validation, decode each property name once, and serialize the immutable copy through an
owned writer rather than a whole-dataset serializer pool buffer. This changes both C/D
equally. Exact UTF-8 escape/array byte-boundary, same-snapshot/replay and no-published-key
on failure tests guard semantics. Ordinary HTTP failures also retain HTTP status through
the existing measurement error path without inventing a protocol code. Keep the same
mutation/repetition/heap/RSS/latency/evidence conditions, old-snapshot probes and raw failures.
The [resource ADR](../adr/2026-11-02-data-result-resource-boundary.md) records the allocation
decision and its limits; a subsequent passing run, if observed, cannot erase these failures.

The 1860c19 run then [passed all 18 cases/144 configurations](schema-update-http-observation-2026-11-02.md)
at 487.16MiB peak. Both failed attempts remain separate evidence. This revision did not
collect process CPU time; processor/thread counts cannot substitute for consumption.
The remaining available CPU-counter measurement follows the separately preregistered
[CPU observation method](process-cpu-method-2026-11-02.md) before final D20-01 AC closure.
Do not invent CPU data for earlier captures.
