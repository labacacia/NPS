English | [中文](runtime-http-observation-2026-11-02.cn.md)

# Paired HTTP runtime observation

The a4d80af experiment passed exact-answer, paired-evidence and declared memory-budget
checks for all 720 measured workflows (plus 72 warmups). This validates the benchmark
execution, not whole-suite/profile conformance. The ordinary HTTPS path was faster and
used fewer bytes than the current NPS two-hop HTTP prototype. No native NCP benefit or
protocol-specific model-token/memory saving is established.

Source: [raw observations](runs/2026-11-02-runtime-http-qualified.json),
[distributions](runs/2026-11-02-runtime-http-summary.json),
[measurement scope](runtime-comparison-method-2026-11-02.md).

Release .NET 10.0.14, Ubuntu 26.04.1 x64; four-CPU affinity; 320MiB managed heap within
the unchanged 512MiB process RSS ceiling. All three scales used identical immutable
source/store operators, current CA/source/model-egress checks, and actual mTLS. C/D
model-bound text was byte-identical and below 32768 bytes. No model was called here.

Warm frontend, gzip-response subset (30 observations each; requests uncompressed):

| Rows | Path | Median / p95 ms | Median TCP/TLS stream bytes |
| --- | --- | --- | --- |
| 100 | ordinary HTTPS | 7.88 / 10.45 | 6724 |
| 100 | NPS gateway HTTP | 80.51 / 83.28 | 75959.5 |
| 1000 | ordinary HTTPS | 13.99 / 16.24 | 7562 |
| 1000 | NPS gateway HTTP | 75.07 / 82.56 | 77622 |
| 100000 | ordinary HTTPS | 421.76 / 447.51 | 7578.5 |
| 100000 | NPS gateway HTTP | 499.32 / 513.00 | 77679 |

All connection/compression combinations and every sample remain in the raw artifact;
this table does not select only favorable results. The 100000-row process peak was
468.58MiB. RSS belongs to the co-host process, not an isolated protocol allocation.
Bytes include both directions, TLS records and HTTP framing; NPS includes both public
and gateway egress. TCP/IP packet overhead and separately declared shared provenance
setup/final cleanup are excluded consistently. Gateway downstream connections are
currently per operation; discovery, extra handshakes/proofs/policy are real included
costs. This is not a claim that all NPS deployments have this topology or overhead.

Earlier attempts remain negative evidence: default GC peaked at 1462.07MiB; the 256MiB
heap attempt rejected a create without capturing its exact error code; cached-source
384MiB and row-wise 384MiB runs peaked at 544.16 and 537.17MiB. Their raw artifacts are
retained alongside the passed deployment. Code/source/configuration changes cannot
be separated into a causal performance estimate from these runs alone.

Scope decision: retain result lifecycle/provenance/authorization as experimental
semantics; claim no small-tool HTTP wire advantage from these observations. Ordinary
tools provide the same context reduction by computing outside the model. NCP repeated
schema/batch/delta value requires the separate D20-04 transport comparison and appropriate
baselines; native NCP/Arrow/Flight/Protobuf superiority is explicitly excluded here.
Schema/update matrix, shared invariant coverage and suite security/release gates remain.
Bounded-evidence model replay is measured separately and is not the D20-05 live tool demo.
