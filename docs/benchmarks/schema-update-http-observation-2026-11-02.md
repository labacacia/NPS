English | [中文](schema-update-http-observation-2026-11-02.cn.md)

# Observed HTTP schema/update workloads

Date: 2026-11-02. Trace: D20-01 #165, scoped resource support for D20-02/03.
Source `1860c1968aab5c45f293d2f38a3f18362309d437`, Release, Linux x64/.NET10.0.14.
The [preregistered extension](schema-update-method-2026-11-02.md) fixes mutation,
boundaries, repeats and budgets. This records actual loopback HTTP behavior, not
native NCP, a live model Agent or suite acceptance.

All 18 scale/schema/update cases passed. All 144 arm/connection/compression observations
passed independent Python fixture-hash/answer/citation checks and actual TRX validation:
4320 measured workflows plus 432 warmups, 30+3 per observation. None failed the unchanged
120-second per-workflow, 512MiB process-RSS or 32768-byte model-evidence gates.
Maximum recorded process RSS was **487.16MiB** under the same pinned 4-CPU/320MiB-managed
deployment; maximum model-visible text was **2385 bytes**. There were zero provider calls.

| 100000 rows, warm frontend, gzip responses | Ordinary C median/p95 ms | NPS D median/p95 ms | Median TLS-stream bytes C / D |
| --- | ---: | ---: | ---: |
| Original schema, 0% update | 340.24 / 364.00 | 409.87 / 435.99 | 7583.5 / 78640.5 |
| Original schema, 1% update | 340.38 / 357.00 | 407.37 / 431.74 | 7589 / 78679.5 |
| Original schema, 10% update | 340.83 / 355.21 | 410.76 / 433.76 | 7587 / 78684.5 |
| Nullable field, 0% update | 370.39 / 409.24 | 441.39 / 462.65 | 7585 / 78650 |
| Nullable field, 1% update | 368.04 / 384.81 | 440.10 / 460.53 | 7586.5 / 78678.5 |
| Nullable field, 10% update | 370.72 / 385.33 | 442.50 / 462.71 | 7593.5 / 78652 |

Across all 72 paired conditions, the NPS prototype had higher median latency and
TLS-stream bytes than ordinary HTTP. C is direct ordinary HTTPS; D includes public
gateway and receiver legs, fresh projections, per-operation discovery and currently
per-request downstream clients. Counters include both D legs' TLS/HTTP framing, not
TCP/IP packet headers. These topology differences prevent attributing this result to
native NCP framing alone. Both paths use the same source, store and operators.
Full cold/warm × identity/gzip distributions and all scales remain in the
[raw artifact](runs/2026-11-02-schema-update-http-qualified.json) and
[summary](runs/2026-11-02-schema-update-http-summary.json).

Source transition/probes/origin setup/cleanup are outside measured workflows. The
first N stable records gain exactly 100 minor units; optional nullable note is null.
Counts/anomaly IDs stay frozen; refunded sums change exactly. Old pages and cursors
retain old source contents, new captures bind the new schema/snapshot, and another
result's cursor rejects even for the no-change control. After transition the repeated
source is immutable. This is not transition latency, concurrent mutation, insertion/
deletion, delta propagation or live-stream throughput evidence.

The [first attempt](runs/2026-11-02-schema-update-320m-attempt.json) retained 124 complete
observations, 14/18 cases passing; the [streaming-hash attempt](runs/2026-11-02-schema-update-stream-hash-attempt.json)
retained 118, also 14/18. The former exposed fixture UTF-16 hashing OOM; the latter
confirmed a result.create server OOM. Other errors remained unclassified. These remain
failures. The passing revision streams the fixture hash, reduces row-name allocation
and stages captured UTF-8 in an owned buffer. It does not raise a budget, force GC,
remove old snapshots or establish that one allocator caused all earlier failures.

RSS is the whole co-host test process including fixture sources, TLS/crypto and GC,
not per-protocol memory saving. Provider observations remain the separately recorded
original-source A/B and C/D runs. This changed-source matrix supplies no provider token
or cost claim. CPU time was **not collected** in this revision; processor configuration
and thread counts are not CPU consumption. D20-01 remains IN_PROGRESS pending that
available-counter measurement and final AC reconciliation. The
[NCP scope decision](../adr/2026-11-02-ncp-measurement-scope.md) retains negative findings,
appropriate future native baselines and all existing gates. No release/advertisement follows.
