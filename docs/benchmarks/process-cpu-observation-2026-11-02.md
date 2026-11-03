English | [中文](process-cpu-observation-2026-11-02.cn.md)

# Observed process CPU and complete HTTP workload metrics

Date: 2026-11-02. D20-01 #165 / Epic #163. Measured source
`3b624e430a05a199860c9023dc70ad0bb71fa647`, Release/Linux x64/.NET10.0.14.
The [CPU preregistration](process-cpu-method-2026-11-02.md) adds actual process CPU
counters to the unchanged schema/update experiment; it adds no CPU performance gate.

All 18 actual TRX cases and 144 configurations passed independent source/hash/answer/
citation, pairing and CPU validation: 4320 measured workflows plus 432 warmups,
30 CPU samples per configuration. Maximum process RSS was **477.59MiB**, below the
unchanged 512MiB gate with fixed 4-CPU/320MiB-managed profile; the 120-second and
32768-byte evidence gates also passed. Maximum model-visible text was 2385 bytes.
No provider calls or shared-budget changes.

| 100000 rows, warm frontend, gzip response | Ordinary C CPU median/p95 ms | NPS D CPU median/p95 ms | Wall median C / D ms |
| --- | ---: | ---: | ---: |
| Original schema, 0% update | 344.68 / 366.67 | 446.59 / 462.90 | 335.96 / 408.86 |
| Original schema, 1% update | 350.28 / 364.86 | 449.64 / 462.25 | 341.83 / 408.25 |
| Original schema, 10% update | 347.32 / 373.75 | 444.97 / 459.60 | 338.05 / 404.59 |
| Nullable field, 0% update | 386.25 / 402.90 | 478.81 / 496.11 | 374.56 / 437.38 |
| Nullable field, 1% update | 382.24 / 399.63 | 473.82 / 496.44 | 372.75 / 437.57 |
| Nullable field, 10% update | 382.28 / 399.18 | 476.42 / 488.69 | 370.79 / 433.71 |

Across 72 paired conditions, D has higher median wall latency and TLS-stream bytes
in all 72, and higher median CPU in 71. The exception is 100000/original-schema/0%-update/
cold/identity: C CPU median/p95 **541.39/910.82ms**, D **461.81/819.14ms**; wall medians
are **352.54/405.83ms**. Preserve that exception and its wide raw distribution. Process
counters include parallel/background/GC work and sampling overhead; this does not
isolate a protocol cause. No samples or first conditions were removed retrospectively.
The [raw artifact](runs/2026-11-02-schema-update-cpu-qualified.json) and
[full distributions](runs/2026-11-02-schema-update-cpu-summary.json) retain every metric.

CPU deltas cover the co-host frontend, gateway/receiver, crypto/SQLite and GC threads;
parallel CPU can exceed wall time. The source transition, retained-old-snapshot probes,
shared citation setup and cleanup stay outside measured workflows. Background work can
cross a sample window. These are path/deployment observations, not native-NCP-only CPU
or protocol-specific memory gains. Earlier captures keep CPU unknown; they were not
backfilled from timing, thread counts or this run.

All mutation amounts and nullable fields match the independent source oracle. Old
snapshots/cursors remain unchanged; new results reject rebound cursors. Repeated
post-transition snapshots do not measure live updates, delta propagation or streaming.
The two earlier failed schema/update attempts remain committed negative evidence;
the separate 1860c19 passing resource run remains historical at its own revision.

The [NCP scope decision](../adr/2026-11-02-ncp-measurement-scope.md) remains: no native
NCP/Arrow/Flight/Protobuf superiority claim from this HTTP topology. Actual A/B and C/D
provider observations remain their original-source experiments, including incorrect
answers/context rejections and conservative unknown-usage reservations. This process
CPU run does not add model reasoning or billing evidence. Suite/native/six-SDK/security/
release gates remain independent. D20-01 completion is assessed in its canonical AC
record; this artifact does not merge, advertise or publish anything.
