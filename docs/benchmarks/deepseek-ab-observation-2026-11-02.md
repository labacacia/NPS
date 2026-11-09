English | [中文](deepseek-ab-observation-2026-11-02.cn.md)

# DeepSeek A/B observations — 2026-11-02

These are actual API observations for the preregistered synthetic refund task,
not a successful workflow qualification. All 20 admitted A/B runs failed the exact
numeric oracle. All ten 100000-row attempts were rejected for context length;
inputs were never truncated. C/D were not executed.

## Run configuration and reproducibility

- Method: [frozen methodology](../../spec/conformance/alpha20/data_workflow_methodology.json).
- Model: `deepseek-flash`, temperature 0, thinking disabled, JSON output, max output 2048.
- Five repeats per arm/scale, A then B interleaved, 100 / 1000 / 100000 complete rows.
- The same task/system text and exact source are used; only object JSON vs schema/positional rows differs.
- User authorized both model-ID probes and a shared USD 10 API cap on 2026-11-02.
- Both requested IDs returned `deepseek-flash`; [probe observations](runs/2026-11-02-deepseek-connectivity.json).
- [Runner](../../tools/benchmarks/data-workflow-deepseek.py) SHA-256:
  `26a8580009e95cfb217f3e40d285276d11d2e1196dcf68c8f4afbbf108932e86`.
- [Run/budget ledger](runs/2026-11-02-deepseek-ab.json) SHA-256:
  `c65e69ace31d107f0b057a595cf15c988a91f98f38487e71281ac6eb85c08022`.
- Method SHA-256: `7222aabd197e96ebe52ec5d6cb5cfad8c2d4e81d25ac239abf24951fd3aab26c`.
- Fixture generator SHA-256: `3604bbe5ade88385fbfbef4cf974719cb43682f85aa4961fb4794516f1cf6931`.

The runner reads a local credential store, disables redirects/retries/provider tools,
reserves a conservative USD 0.32 before each request and preserves that reservation
when usage/outcome is unknown. Resume with the **same live ledger**, not a new file.
Credentials are not part of the artifact. Offline budget/scoring tests make no calls.

```sh
python3 -m unittest discover -s tools/benchmarks/tests -p 'test_data_workflow*.py'
python3 tools/benchmarks/data-workflow-deepseek.py --credentials /secure/local-credentials.json --output /persistent/approved-run.json --execute
```

This paid command requires its recorded authorization and current provider-price
verification. Creating another ledger does not reset or increase the user's budget.

## Observed results

Input token counts include the common prompt and are identical across five repeats
within each admitted arm. Latency is client-side request/response elapsed time, not
isolated NCP transport latency. p95 uses nearest rank with five observations.

| Rows | Arm | Provider input tokens | Exact answers | Median / p95 ms |
| --- | --- | ---: | ---: | ---: |
| 100 | A full object JSON | 3146 | 0/5 | 1951.034 / 2495.496 |
| 100 | B schema + positional rows | 1884 | 0/5 | 1910.186 / 2827.071 |
| 1000 | A full object JSON | 29246 | 0/5 | 2238.488 / 2741.554 |
| 1000 | B schema + positional rows | 16284 | 0/5 | 2054.354 / 2117.396 |
| 100000 | A full object JSON | unknown; context rejected | 0/5 | 6896.265 / 10174.299 |
| 100000 | B schema + positional rows | unknown; context rejected | 0/5 | 4117.591 / 6019.266 |

B reduced observed input tokens by about 40.1% at 100 rows and 44.3% at 1000 rows.
Both representations produced incorrect counts or sums in every admitted attempt.
For example, the first A/100 answer returned 13 current delivery orders and 24576
refunded minor units; the exact source has 12 and 23576. Naming the correct anomaly
alone does not satisfy the frozen exact aggregate acceptance.

Do not generalize this non-thinking model/configuration to every model or task.
The experiment gives a concrete failed direct-context baseline. It supports testing
external exact computation; it does not establish NPS's advantage over ordinary
HTTP tools, which requires the missing correctness-equal C/D comparison.

## Usage, cache and budget interpretation

Raw provider usage and synthetic visible answers are retained. Warm cache hits are
observable in provider counters; the experiment does not force a cold provider cache.
Do not add cache hits to prompt tokens: those classes overlap. Missing reasoning/cache
creation classes remain unknown. No full-wire/TLS bytes or runtime memory were measured.

Using the [dated official peak cache-miss rates](https://api-docs.deepseek.com/quick_start/pricing/)
as a conservative bound, the 20 observed responses total USD 0.0819372; probes add
USD 0.0000072. This is not an invoice. The ten rejected calls returned no usage,
so the ledger conservatively retains USD 3.20 in reservations rather than inventing
zero charges. Total accounted upper bound is USD 3.2819444, below the USD 10 cap.
Remaining shared budget is conservatively USD 6.7180556 until those unknown charges
can be reconciled. Successful-response bounds overstate warm-cache/off-peak prices.

D20-01 remains IN_PROGRESS: C/D public routes, full transport accounting, memory,
schema/update variations and comparison against an appropriate existing transport
baseline still need evidence. These observations neither qualify D20-05 nor relax
an existing release gate.
