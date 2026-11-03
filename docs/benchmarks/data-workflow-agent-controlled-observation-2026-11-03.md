English | [中文](data-workflow-agent-controlled-observation-2026-11-03.cn.md)

# Controlled actual tool-Agent observation — 2026-11-03

Both arms complete40/40workflows with exact independent source-oracle answers and
five actually inspected supporting citations. All80resource guards pass the unchanged
four-CPU/320MiB-managed/512MiB-actual-process-tree/120s limits. This qualifies the
experimental D20-05 reference workflow, not a general NPS token/fee/performance advantage.

## Provenance and corrections

Measured source `126c5df2ca069309c231b2ef6b5e58778f503b2a`,
[PR204](https://github.com/labacacia/NPS-Dev/pull/204), committed/preregistered before
any v2call. SDK artifacts remain source109ea55e11609645aa8524b36e0c33f55e5733fa,
four unpublished NuGet1.0.0-alpha.20.dev.1 packages/Python1.0.0a20.dev1. Actual
assembly, all12measured Python/C#recipe hashes, package hashes and source method are
in the [closed packet](../../demos/data-workflow/evidence/live-tool-agent-bounded-v2-126c5df/index.json).

The [first matrix](data-workflow-agent-observation-2026-11-03.md) remains80cases,
78successes/two rejected model errors and558.44140625MiBunqualified RSS. No old case
or cost is changed. All four failed no-model resource preflights and two full passing
replays remain [preserved](../../demos/data-workflow/evidence/resource-preflight-2026-11-03/index.json).
The correction installs actual CPU/heap controls, serializes one owned source snapshot
and memoizes only three fixed immutable source queries per revision. Both arms use
the same producer/cache/policy/store. This is implementation/launch work, not a protocol
memory improvement. No forcedGC/pool reset, larger ceiling or smaller dataset is used.

A separately registered full80case matrix repeats both arms/all five repeats at100/
100000rows,baseline/update,cold/warm-requested. New case keys and a separate experiment
namespace seal all590prior attempts in the SAMEUSD10ledger. This is not selective
rerunning of old failures. Prompt/tool schemas/model settings remain fixed:deepseek-flash,
temperature0,thinkingdisabled,max2048outputtokens. Official
[model/prices](https://api-docs.deepseek.com/quick_start/pricing/),
[tool calls](https://api-docs.deepseek.com/guides/tool_calls/),
[cache](https://api-docs.deepseek.com/guides/kv_cache/) checked2026-11-03.

## All results

Pairs are **ordinary · NPS**, allfive attempts percell. Token/request medians include
all requests in each workflow; warm includes its paid identical-request prime.

| Rows/revision/requested cache | Exact + faithful | Model requests | Input tokens | Output tokens |
| --- | --- | --- | --- | --- |
| 100/v1/cold | 5/5 · 5/5 | 6 · 6 | 16166 · 16180 | 1428 · 1428 |
| 100/v1/warm | 5/5 · 5/5 | 7 · 7 | 17310 · 17199 | 1520 · 1493 |
| 100/v2/cold | 5/5 · 5/5 | 6 · 6 | 16321 · 16208 | 1464 · 1448 |
| 100/v2/warm | 5/5 · 5/5 | 7 · 7 | 17211 · 17194 | 1517 · 1518 |
| 100,000/v1/cold | 5/5 · 5/5 | 6 · 6 | 16412 · 16254 | 1464 · 1427 |
| 100,000/v1/warm | 5/5 · 5/5 | 7 · 7 | 17239 · 17323 | 1486 · 1509 |
| 100,000/v2/cold | 5/5 · 5/5 | 6 · 6 | 16358 · 16425 | 1436 · 1449 |
| 100,000/v2/warm | 5/5 · 5/5 | 8 · 7 | 18574 · 17445 | 1520 · 1512 |

528paid requests =488actual conversation turns +40cache primes. Actual usage totals
1,353,174input/118,245output tokens;992,384inputcache-hit/360,790miss. First real cold
turns had0/40any-cache hits;warm40/40. Every warm prime/first request hash matches.
No automatic retries, provider errors/unknown usage, tool failure, cleanup failure
or unknown allocation occurred. The two source updates retain live unchanged old
snapshots and produce new snapshot/schema fences with amount deltas100/100000.

Ordinary40/40:266requests,679,616input/59,255output tokens. NPS40/40:262requests,
673,558input/58,990output tokens. Different nonces/opaque IDs/model trajectories and
paired non-randomized order prevent attributing small differences to protocol
savings. One model/account/host/seed and five repeats do not establish universal
reliability. Ordinary tools already obtain the same benefit of external computation.
All successful cases inspect five rows; typical cumulative tool output6,701/6,827bytes
is not provider token usage. Public HTTP body counters exclude headers/TLS/internal
gateway traffic. Full actual cases/distributions remain in
[case CSV](runs/2026-11-03-data-workflow-agent-controlled-cases.csv),
[summary CSV](runs/2026-11-03-data-workflow-agent-controlled-summary.csv) and raw packet.

## Resource qualification and accounting

Actual four-CPU affinity is recorded. Each server independently reports4processors
and335544320availableGCbytes.10ms sampling includes the Agent and all actual descendants;
both concurrent RSS and conservative live VmHWM sum are guarded, with sticky failures
and120swhole-workflow budget including primes/cleanup. Provider requests check the
resource gate before reservation/network and after observed settlement, with remaining
time as request timeout. Recorded resource scope excludes provider compute.

All80guards pass: observed concurrent peak**451653632bytes(430.73046875MiB)**,
conservative high-water sum**456880128bytes(435.71484375MiB)**; longest recorded
workflow21.899014s,below120s. This is the actual controlled implementation/resource
scope. It does not turn v1's558MiB into a pass or prove every schema/adopter fits.

New528calls add**USD0.5478462**conservative peak cache-miss bound. Original60attempts,
v1's530calls and all prior unknown reservations remain sealed. Shared total
**USD4.3990379**,remainingUSD5.6009621; historicalUSD3.20unknown remains charged.
Amounts are upper bounds,not invoices; all primes/turns are included. The new closed
extract binds all590prior attempts by exact digest/bound. No new budget/reset or
unobserved billing is introduced.

## Independent review and reproduction

The bounded high-risk review found two failure-path issues not exercised by this
successful matrix: ordinary response loss could lose an unknown-create recovery key,
and a helper could settle a foreign/sealed old reservation. After this packet was
sealed, minimal wrappers/namespace/prefix checks fixed those paths.13fault regressions
pass. These changes do not relabel v1, alter measured v2recipes or trigger extra paid
runs. Both actual measured recipe sets remain bundled and independently verifiable.

SDK-free/no-key/no-network verification:

```bash
python3 demos/data-workflow/summarize.py --packet demos/data-workflow/evidence/live-tool-agent-bounded-v2-126c5df
```

The verifier checks closed file/recipe hashes, all528paid observations/usage/charges,
all80exact answers and actual cited rows, cache prime equality, original fixed resource
limits, actual backend CPU/heap and Agent/server process IDs, both RSS guards, deadlines
and update fences, then recomputes the report. Default local checks never call models.

D20-05 is COMPLETE for the accepted experimental reference and measurement AC after
[scope/resource review](../adr/2026-11-03-data-workflow-agent-scope.md). Old v1failures,
rawA/B20wrong+10context failures and C/D30evidence reproductions remain separate.
Continue D20-06/07 authorized handoff/coordination; D20-08 retains full per-SDK/binding/
security/resource/P20 qualification. No default capability, productionPKI, repository
boundary, release projection, beta or publication decision changes. NU1902 and registry/
security obligations remain unwaived;#134publishing remains separate.
