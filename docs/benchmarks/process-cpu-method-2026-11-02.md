English | [中文](process-cpu-method-2026-11-02.cn.md)

# Preregistered process CPU observation extension

Date: 2026-11-02. D20-01 #165 / Epic #163. Commit this method and instrumentation before
execution. The earlier 1860c19 matrix qualified its declared correctness/RSS/time/evidence
budgets, but did not collect CPU time. Four configured processors and thread counts are
not CPU consumption; historical captures keep that metric unknown.

Repeat the same full 144-condition schema/update matrix with 30 measured + 3 warmup flows
per condition. Keep source mutations, operations, C/D order, connection/compression
states, 4-CPU affinity, 320MiB managed deployment and 512MiB RSS/120s/32768-byte gates.
No new operation, provider call, performance threshold or forced garbage collection.

For every flow, read the current test process's
[`Process.TotalProcessorTime`](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.totalprocessortime?view=net-10.0) before
the existing timed workflow. After the workflow and cold frontend shutdown, refresh
the process counter and record the difference in milliseconds. Keep 30 individual CPU
deltas, then median/nearest-rank p95/min/max, paired with the same 30 latency/TLS/RSS
observations. Warmup CPU samples are discarded with the other warmup metrics.
CPU counter-query overhead brackets the workflow and is not subtracted. The existing
wall-clock boundary stays unchanged; no CPU estimate is derived from elapsed time.

The counter covers user/kernel CPU across the co-host frontend, gateway/server,
crypto/SQLite and GC threads. It excludes other processes and the external provider;
asynchronous/background work can cross sampling windows. Parallel CPU time can exceed
wall time; operating-system counter resolution can yield zero for a short flow.
Zero is an observed value, not missing evidence. This is a deployment/path comparison,
not native-NCP-only CPU attribution or a general production utilization claim.

Record the exact boundary string and 30 finite nonnegative samples for each of 144
observations. The structural schema keeps CPU fields optional for old records; current
experiment drivers require them. Semantic guards reject missing/short/boolean/negative/
nonfinite samples and altered attribution. Summaries retain null for historical missing
CPU, never zero. All 18 actual TRX cases, independent source/oracle/citations and other
unchanged gates must still pass before the CPU experiment qualifies.

Run the existing driver with a new absolute output directory:

```sh
python3 tools/benchmarks/data-workflow-changes.py --execute --output <new-absolute-observation-directory>
```

Retain the new revision, source/method/schema hashes, raw failures or samples and
distributions separately. Never overwrite/relabel earlier positive or negative runs.
The shared provider ledger and USD 10 budget are untouched.

Actual [3b624e4 observations](process-cpu-observation-2026-11-02.md) passed all 18 cases,
144 configurations and the unchanged gates with all CPU samples present. Earlier CPU
remains unknown, and earlier failed resource attempts remain failures.
