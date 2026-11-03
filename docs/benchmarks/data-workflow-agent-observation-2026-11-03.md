English | [中文](data-workflow-agent-observation-2026-11-03.cn.md)

# Actual tool-Agent observation — 2026-11-03

Both ordinary HTTP and NPS complete 39/40 workflows with exact answers and faithful
inspected citations. Two model errors are rejected, not repaired or counted as passes.
This supports a bounded tool workflow; it establishes no NPS-specific token, fee or
latency advantage. Ordinary HTTP already obtains the same external-compute benefit.

## Measured source and method

Measured Agent source: `afafebd15a6cebacfaede01193225e4422ae05f5` ([PR202](https://github.com/labacacia/NPS-Dev/pull/202)),
preregistered before calls. Server/SDK delivery increment [PR201](https://github.com/labacacia/NPS-Dev/pull/201),
SDK package source `109ea55e11609645aa8524b36e0c33f55e5733fa`: four unpublished
NuGet `1.0.0-alpha.20.dev.1` packages and Python `1.0.0a20.dev1`. Actual server assembly
and every measured recipe/package hash are in the
[closed packet](../../demos/data-workflow/evidence/live-tool-agent-afafebd/index.json).
Published suite versions remain alpha.19; none of these artifacts were registry-published.

[Method](../../demos/data-workflow/evidence/live-tool-agent-afafebd/live-method.json): 100/100000 rows × baseline/update
× requested cold/warm × ordinary/NPS × five repeats =80workflows. The same model,
tools, source, operators, principal, policy, budgets and task apply. Model output
selects actual operations through installed public SDK APIs and real mTLS endpoints;
this is not reproduction of saved runtime evidence. Ordinary endpoints use the same
SDK provider as their internal operator engine; this controls backend semantics,
not the fastest possible ordinary implementation.

`deepseek-flash`, temperature0, thinking disabled, max2048output tokens. Official
[model/prices](https://api-docs.deepseek.com/quick_start/pricing/),
[tools](https://api-docs.deepseek.com/guides/tool_calls/),
[cache](https://api-docs.deepseek.com/guides/kv_cache/) checked2026-11-03. Underlying
Flash-1017 is the documented mapping, not an independently inspected weights identity.
The provider returned the requested alias on all calls.

## All observations

Pairs below are **ordinary · NPS**. Each cell includes all five attempts, including
failures. Input/output counts sum every request in a workflow; warm includes its paid
prime. Values are medians, not promised savings or causal estimates.

| Rows / revision / requested cache | Correct + faithful | Model requests | Input tokens | Output tokens |
| --- | --- | --- | --- | --- |
| 100 / v1 / cold | 5/5 · 5/5 | 6 · 6 | 16240 · 16220 | 1430 · 1429 |
| 100 / v1 / warm | 5/5 · 5/5 | 7 · 7 | 17246 · 17175 | 1503 · 1501 |
| 100 / v2 / cold | 5/5 · 5/5 | 6 · 6 | 16284 · 16238 | 1445 · 1459 |
| 100 / v2 / warm | 5/5 · 5/5 | 7 · 7 | 17232 · 17213 | 1531 · 1518 |
| 100,000 / v1 / cold | 4/5 · 4/5 | 6 · 6 | 16283 · 16286 | 1428 · 1431 |
| 100,000 / v1 / warm | 5/5 · 5/5 | 7 · 7 | 17375 · 17295 | 1508 · 1487 |
| 100,000 / v2 / cold | 5/5 · 5/5 | 6 · 6 | 16289 · 16413 | 1426 · 1437 |
| 100,000 / v2 / warm | 5/5 · 5/5 | 7 · 7 | 17400 · 17315 | 1527 · 1497 |

530paid model requests =490actual conversation turns +40cache primes; zero automatic
retries and zero provider HTTP/unknown failures in this experiment. All80conditions
are retained. Total actual usage:1,358,087input /118,356output tokens, with997,888input
cache-hit /360,199miss tokens. Cold first real turns had0/40any-cache hits; warm had40/40.
The prime and first real request hashes match in every warm condition. Cache is
best-effort; these observations do not guarantee future hits.

Ordinary totals:264requests,679,717input/59,511output tokens. NPS totals:266requests,
678,370input/58,845output tokens. Different opaque locators/nonces and model trajectories
prevent attributing the small differences to protocol efficiency. Order was paired
ordinary then NPS, not randomized. One model/account/host/seed and five repeats per
condition do not establish general superiority.

Every workflow inspected five rows. Typical nine-operation workflows returned6,701
(small) /6,827(large) cumulative tool-output bytes; those are not provider token counts.
The ordinary failure used13operations and a different four-result plan, still within
all inspection/allocation/proposal limits. No tool dispatch, cleanup or unknown-create
failure occurred. Actual public HTTP body medians were6,142/6,214bytes ordinary and
18,236/18,308bytes NPS; these exclude HTTP/TLS headers and the internal gateway leg.
They are not full transport-wire measurements. Full per-case latency/CPU/RSS/body/
usage and distributions are in the [case CSV](runs/2026-11-03-data-workflow-agent-cases.csv),
[summary CSV](runs/2026-11-03-data-workflow-agent-summary.csv) and raw packet.

Local sampled Agent+server peak RSS was558.44140625MiB. D05 preregistered this as a
sampled observation, not a hard limit; it does **not** qualify a512MiB envelope or show
memory savings. D01/D04's separate resource qualifications are unchanged. Retain the
unqualified suite/adopter resource obligation for D20-08; do not import this as a pass.

## Failures and lifecycle evidence

- NPS,100000/v1/cold/repeat1: aggregates are correct, but one final citation drops
  characters from `schema_ref`. Exact provenance validation rejects it.
- Ordinary,100000/v1/cold/repeat2: the model chooses four period-filtered results and
  returns raw group arrays instead of the required nested final-answer/evidence shape.
  Final validation rejects it. Actual inspected rows and releases remain recorded.

Neither case is silently corrected, omitted or rerun. The host does not trust a model
to manufacture references or promote an invalid answer. Model correctness is97.5%
for this task/run, not a universal reliability guarantee. Future output handles can
avoid copying long identifiers; that is a new design/measurement, not a relabeling
of these failures.

One operator-only update per scale changes the first1%of amounts by100minor units and
adds nullable note. Both old snapshots remained live/unchanged at the update check;
new snapshot/schema fences changed. Total amount deltas100/100000match the source rule.
Both arms execute exact post-update answers/citations. The default three-scale check
also rejects stale/wrong schema/snapshot, direct receiver bypass, operator/owner role
confusion, duplicate JSON and released-result access. Unknown allocation/expiry semantics
remain explicit; no fifth remote result action or native result binding is introduced.

## Spend, reproduction and scope

The SAME original USD10ledger was used. Original60attempts, its original probe/prompt
binding and USD3.20unknown reservations remain intact. New530requests add
**USD0.5494533 peak cache-miss upper bound**, including all primes and failed answers.
Shared conservative total is**USD3.8511917**, leavingUSD6.1488083. These are not invoices;
cache-hit/off-peak rates can be lower. The sanitized ledger extract preserves prior
history by exact digest and bound; all new per-request usage/charges are included.
No credentials, private TLS keys, raw error bodies or hidden reasoning are exported.

Without a key, original private ledger, SDK or network call:

```bash
python3 demos/data-workflow/summarize.py --packet demos/data-workflow/evidence/live-tool-agent-afafebd
```

The closed index checks every exported file, bundled measured recipe, every paid
request/observation/charge, prime equality, source oracle, actually read citations,
inspection budgets and update fences, then recomputes the report. Run
[setup/check/guards](../../demos/data-workflow/README.md) for actual local endpoints.
Canonical demo tests50/50pass; independent installed-package distribution49pass/1skip
(the private original-source-file comparison is absent; runtime/oracle/packet checks pass).
No additional model calls are used for reproduction. CI20241552367438passes all tenjobs,
including package builds and real deterministic mTLS/offline-Agent checks without keys.

The first measurement is complete; D20-05 remains IN_PROGRESS until its512MiB
resource qualification is resolved. The [scope decision](../adr/2026-11-03-data-workflow-agent-scope.md). D20-06/07 can subsequently address
actual authorized handoff and ownership/coordination; broad porting and D20-08 must retain
per-SDK/binding/resource/security/P20 qualifications. No default capability, production
PKI, repository boundary, release projection, beta or publication decision changes.


Subsequent full controlled qualification is recorded separately in the [controlled observation](data-workflow-agent-controlled-observation-2026-11-03.md). D20-05 is now COMPLETE for experimental reference AC; this first matrix and its failed outcomes remain unchanged.
