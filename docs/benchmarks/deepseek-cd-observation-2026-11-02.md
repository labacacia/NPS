English | [中文](deepseek-cd-observation-2026-11-02.cn.md)

# DeepSeek bounded C/D evidence observation

All 30 bounded-evidence replies matched the exact frozen JSON oracle. C and D had
identical provider input/output token counts at each scale. This supports external
deterministic computation followed by bounded evidence reproduction; it establishes
no NCP-specific model-token benefit and is not a live model-planned tool workflow.

[Raw shared ledger snapshot](runs/2026-11-02-deepseek-four-arm.json) retains A/B and C/D,
raw usage, visible synthetic answers, request/source/runner/prompt hashes and charges.
[Runtime evidence](runtime-http-observation-2026-11-02.md) records the actual prior
HTTP workflows; [method/scope](runtime-comparison-method-2026-11-02.md) defines replay.

Requested/returned model: `deepseek-flash`; five repeats per scale/arm, temperature 0,
thinking disabled, maximum output 2048, JSON output, no tools/retries/truncation. C/D
receive byte-identical previously captured tool evidence, including computed groups
and anomaly predicate plus bounded real source records/citations. The machine-JSON task
already has a deterministic runtime answer and does not require an LLM. Runtime origin
references were verified at capture and released before this historical evidence replay.
The shared system instruction/output schema remain consistent with A/B; the C/D prelude
explicitly says the displayed rows are examples and the computed groups cover all rows.

| Rows | A full JSON: exact / input tokens | B schema+rows: exact / input tokens | C ordinary tools: exact / input→output | D NPS tools: exact / input→output |
| --- | --- | --- | --- | --- |
| 100 | 0/5 / 3146 | 0/5 / 1884 | 5/5 / 1165→254 | 5/5 / 1165→254 |
| 1000 | 0/5 / 29246 | 0/5 / 16284 | 5/5 / 1170→254 | 5/5 / 1170→254 |
| 100000 | 0/5 / context rejected | 0/5 / context rejected | 5/5 / 1143→272 | 5/5 / 1143→272 |

Every repeat had the counts shown. Prompt counts include instructions and evidence,
including opaque reference text; token counts need not increase with source size.
The rejected A/B requests have unknown usage, not invented zeroes. Their admitted
100/1000-row answers were incorrect, so no correct-task cost ratio is inferred.
Available cache counters are retained as observed; no forced-cold provider run or
missing reasoning/cache class is inferred from absence. Only visible output is stored.

C/D's conservative peak-rate usage upper bound is USD 0.0197940. Total successful/probe
usage upper bound is USD 0.1017384; ten earlier usage-unknown failures still retain
USD 3.20. Combined shared accounting is USD 3.3017384, leaving USD 6.6982616 of the
original USD 10 cap. This is not an invoice. Peak rates and alias routing were rechecked
on 2026-11-02 in [official pricing](https://api-docs.deepseek.com/quick_start/pricing/).
The original live ledger remains authoritative for any later call; it was extended,
never reset, and each request was reserved before sending.

Interpretation: model context reduction comes from performing the finite data task
outside the model and returning bounded evidence. Ordinary HTTP does that too. Current
NPS HTTP has higher measured wire/latency costs in this topology; result semantics,
authorization and provenance remain separate benefits to qualify. Repeated-schema NCP
transport value still requires D20-04. Schema/update workloads, exact shared invariant
coverage, online tool orchestration, six-SDK and security/release gates remain open.
