English | [中文](runtime-comparison-method-2026-11-02.cn.md)

# Ordinary HTTPS versus NPS reference runtime: measurement scope

Status: default-GC run at ca55bf2 retained exact answers but failed the 512MiB RSS gate;
bounded-deployment rerun is required before qualification.
D20-01 follows the [frozen methodology](../../spec/conformance/alpha20/data_workflow_methodology.json)
and [exact runtime oracle](result-oracle-observation-2026-11-02.md).
This supplement defines boundaries without changing frozen requirements.

## Controlled paths and evidence

Both paths share the seeded immutable source, stable order, result store and exact
COUNT/Int64 SUM operators. C serves ordinary HTTPS JSON create/aggregate/read/release
tools. D uses the supported NPS client, explicit versioned discovery, public mTLS
gateway, fresh NIP projection and separately authenticated receiver. Both enforce
current CA/source policy. Ordinary tools also process data outside the model; this
harness makes no external-model calls.

Each workflow independently captures all orders and refunds, obtains the two aggregates,
reads a bounded refund page and releases both results. Every answer must match the oracle.
Before measurement, prepare one shared live source reference and bounded first page.
Measured full-source captures must share its snapshot hash. Both arms cite that common
immutable provenance and the same five actual source rows, giving byte-identical
model-visible text. Shared provenance setup/final cleanup are separate; temporary
create/release costs are measured. The provenance result is released afterward, so
exported evidence is historical, not an ongoing follow-up session.

## Repeats, compression, connections and bytes

Run Release builds at 100/1000/100000 rows. For each arm/connection/compression
combination, perform three warmups and 30 measured workflows: 24 observations,
720 measured workflows and 72 warmups. Alternate the first arm by connection state.
Identity and gzip responses are separate observations; both paths use uncompressed
requests. Server-observed gzip response counts must confirm actual compression.

Cold means a new frontend connection per workflow; warm reuses that connection.
TLS resumption is disabled. No empty CPU/source/provider-cache claim is made.
D currently creates a downstream client per operation; that deployment choice,
repeated discovery and additional authentication are included and cannot be
generalized into inherent NCP cost or benefit.

Counting streams below TLS observe both directions, including TLS records and HTTP/1.1
headers, chunking and payload. D sums frontend and gateway egress; C measures its direct
stream. No content, credentials, proofs or keys are recorded. Cold frontend shutdown is
included; gateway egress closes before the public reply. Warm setup is in warmups and
final shutdown is separate. TCP/IP packet headers/retransmissions are outside this
TCP-stream boundary on both paths. These are not Ethernet packet captures or payload
byte estimates.

Retain all 30 latency/byte values and report median/nearest-rank p95. RSS snapshots
and OS peak cover the entire co-hosted test process and enforce 512MiB; they cannot
attribute protocol-specific memory savings. Failed memory observations remain on disk
and do not qualify. This one-shot HTTP experiment makes no native NCP, Arrow/Flight/
Protobuf, schema-churn/update, failover or general public-PKI efficiency claim. Remaining
D20-01/04/08 obligations and historical gates stay visible.

## Execute

Commit the reviewed harness, keep the worktree clean and choose a new absolute directory:

```sh
python3 tools/benchmarks/data-workflow-runtime.py --execute --output /absolute/new/runtime-observations
```

The driver preserves actual .NET TRX/log/raw JSON and checks revision, fixture, exact
answer/citations, complete distributions, observed compression, paired model text and
memory status. It emits separate identity/gzip files for the existing evidence validator,
refuses overwrite and never calls a paid provider. Offline guards reject incomplete
matrices, source drift, fabricated counters, changed citations, invented usage and false
memory passes. A correctness-only test run is not the 30-repeat experiment.

## Observed default-GC failure and bounded rerun

The ca55bf2 experiment completed all 720 measured workflows with correct answers and
equal C/D model text. Both 100000-row paths exceeded the unchanged RSS ceiling;
the co-host process peak reached 1462.07MiB. Retain all negative observations rather
than relabeling them successful. RSS alone does not prove a durable object leak.

Review found avoidable per-row scalar JSON-document cloning in the aggregate loop.
The source document remains alive until final groups are independently serialized,
so remove those clones without changing results. This is a minimal D20-02 supporting
correction for the observed D20-01 allocation failure. Add managed-heap/thread samples.

An initial 256MiB heap attempt at 8daac11 completed the smaller scales but rejected
a 100000-row result create; its bounded error code was not recorded, so do not label
the rejection an OOM diagnosis. Preserve that failed attempt and improve failure capture.

The next rerun explicitly sets four-CPU OS affinity and a 384MiB managed-heap hard limit
for both paths, retaining the original 512MiB process RSS check. The earlier CLR already
reported four processors; this is not evidence of reducing its processor count.
The launch receipt records actual CPU IDs and limits. This is a declared deployment
configuration plus a code change, not an isolated causal estimate of either change.
No forced GC is inserted into measured workflows. Default-GC failure remains reportable.

Preload/cache the immutable fixture source equally for both paths; each result capture
still copies data in the actual store. This removes repeated fixture-generation work
and changes the declared source setup, not source consistency or protocol semantics.
The operator explicitly enables model egress for only this preregistered synthetic
source. Both ordinary and NPS operations enforce the store's ToModel checks; missing
egress provenance cannot qualify evidence for external-model calls. Preserve per-arm
progress and bounded failure code/heap/thread observations if an experiment stops.

The cached-source/384MiB run at 83fc03b completed all groups correctly but still
failed RSS (peak 544.16MiB). Preserve this attempt too. Subsequent inspection replaces
whole-array DOM parsing for every read/aggregate with row-wise Utf8JsonReader traversal;
only bounded distinct-group scalar keys are cloned. This preserves the 384MiB heap
configuration and 512MiB RSS check, rather than changing the process acceptance bound.

The d2ba61f row-wise/384MiB experiment also retained exact answers but peaked at
537.17MiB. Its raw failure is preserved. The next declared deployment partition assigns
320MiB to managed heap and reserves 192MiB of the unchanged 512MiB process budget for
native/JIT/hosting overhead. Both paths use the same partition; no forced collection
or protocol-specific memory-saving claim is added.

## Bounded model evidence replay

`data-workflow-deepseek-runtime.py` first requires the complete passed actual Release
matrix, matching source/citations, exact C/D text, operator egress policy and RSS gate.
Only then can it observe five DeepSeek replies per scale/arm with the same temperature,
non-thinking mode and output limit as A/B. C/D use identical captured tool evidence;
the runtime already computed the groups and anomaly predicate. The frozen machine-JSON
task itself does not require an LLM. This measures provider usage and exact evidence
reproduction, not a live model-planned tool workflow or an NCP-specific token saving.

The script must reuse the existing shared A/B budget ledger. Its extension freezes
runtime/source/runner/prompt hashes without changing the original cap, probes, prompt
or charges. Reserve before every request, preserve unknown usage charges, never retry
an attempted/uncertain key, and retain raw observed usage plus synthetic visible answers.
Duplicate keys/non-JSON numbers/float-for-integer substitutions fail exact scoring.
No hidden reasoning or credential values are retained. Both original alias probes and
all later requests count against the user's one USD 10 cap.
