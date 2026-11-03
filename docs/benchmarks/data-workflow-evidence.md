English | [中文版](./data-workflow-evidence.cn.md)

# Alpha.20 data-workflow evidence

Trace: D20-01 #165, Epic #163. D20-01 status: COMPLETE for benchmark/evidence AC;
stack integration, security, SDK and publication gates remain separate.
[Methodology and exact answers](../../spec/conformance/alpha20/data_workflow_methodology.json)
are frozen before qualification. This page distinguishes executed local preparation
from separately executed public-route and model-provider results. Follow the dated
observations below for actual execution; the preparation tool never performs those calls.

## Run without credentials or third-party packages

```bash
python3 tools/benchmarks/data-workflow-evidence.py --rows 100 1000 100000
python3 -m unittest discover -s tools/benchmarks/tests -p 'test_data_workflow_evidence.py' -v
```

The tool makes no network/provider calls. A/B are measured compact UTF-8 representations
of the same five-column synthetic dataset; B includes its schema once.
Both include all records. Deterministic gzip uses mtime=0.
The local compute path checks exact aggregates and anomaly groups against the separately
frozen oracle; rates use integer cross multiplication, not rounded percentages.

For the 100,000-row fixture (Python 3.14.4 on the development host), local preparation
on 2026-11-02 observed:

| Representation | UTF-8 bytes | gzip representation bytes |
| --- | ---: | ---: |
| A: object JSON | 9,758,001 | 297,133 |
| B: schema plus rows | 4,858,133 | 265,832 |

Dataset SHA-256: `934291a490f2df764354ecfc3ca292e4a0573e35fcf7c74f462f19b8773e12dd`.
These are representation/compression measurements, **not** HTTP/NCP complete wire bytes,
model-input token counts, context-fit evidence, public-route execution or provider cost.
Different Python/zlib versions may change gzip output; record the execution environment
when comparing. The one preparation duration is not the 30-repeat transport experiment.

## Missing evidence stays missing

C (ordinary HTTP runtime) and D (NPS runtime) are NOT_EXECUTED without captures.
Their wire bytes and provider usage remain null, not zero or a byte-derived estimate.
A/B provider usage and context fit also remain unknown. The tool prints no token-saving
percentage and does not call a fixture ratio NCP's additional benefit.

Qualification mode must not silently pass without public-route evidence:

```bash
python3 tools/benchmarks/data-workflow-evidence.py --rows 100 --require-runtime-captures
```

Without captures this exits 2, after emitting an IN_PROGRESS report.
A nonzero exit is an expected missing-evidence result, not permission to substitute mocks.
The previous CGN and context benchmark numbers remain historical estimates/simulation;
their publishing thresholds are retained until an explicit gate decision changes them.

## Supply captured observations

```bash
python3 tools/benchmarks/data-workflow-evidence.py --captures /absolute/path/run-captures.json --require-runtime-captures
```

The file is an array of C/D captures with:

- arm, rows, fixture_sha256 and an exact normalized answer matching the oracle;
- cache_state (cold/warm), exact model_visible_utf8, observed wire_bytes and 30 latency_ms samples;
- source.revision, binding, boundary, request_id and environment;
- optional provider_usage with explicitly mapped nonnegative counters; unavailable categories stay null;
- provider/model_id, a positive finite spend_cap and usage_mapping whenever provider usage is supplied.

The Release HTTP runner additionally records per-workflow TLS bytes/compression/RSS,
explicit build/egress policy and whole-process peak memory. Its structural
[machine-readable capture schema](http-runtime-capture.schema.json) also permits
retained RSS failures. `data-workflow-runtime.py` and `data-workflow-changes.py`
enforce exact sources/answers, full matrices, paired text and passed actual TRX runs;
schema validity alone never establishes authenticity or qualification.

C/D pairs must have identical model-visible text, environment, measurement boundary
and provider/model/accounting identities. A mismatch is rejected instead of attributing
a changed prompt or measurement boundary to NCP. Payloads exceeding 32 KiB of permitted
visible evidence, wrong answers/datasets, duplicate arm/cache entries, inadequate repeats,
boolean/negative usage and missing provenance are rejected.

Accepted files remain **supplied captures, not live-authenticated observations by this
tool**. It cannot establish their authenticity, correct TLS accounting or whether the
runtime really enforced policy. Public API/native/HTTP/security runs must supply their
own exact source and environment evidence. No supplied file alone satisfies six-SDK,
real-provider or release acceptance. Provider pricing/cost is not inferred.

The report publishes payload hashes/lengths, not the raw model text or credentials.
Do not put production captures, API keys, sensitive prompts or customer data in Git.

## Claim-to-evidence map and current limits

The separate opt-in [DeepSeek runner](../../tools/benchmarks/data-workflow-deepseek.py)
now records actual A/B calls under the user-approved shared USD 10 cap. Its
[2026-11-02 observations](deepseek-ab-observation-2026-11-02.md) retain failed exact
answers, context rejections, raw usage and conservative reservations. This changes
neither the preparation tool's no-call behavior nor the scope of any transport result.

| Claim / question | Applicable evidence and limit |
| --- | --- |
| Object JSON versus schema/rows is smaller | Above representation/gzip bytes; no tokenizer, billing or complete-network inference. |
| A/B real model task | [A/B observation](deepseek-ab-observation-2026-11-02.md): 20 admitted replies failed exact scoring; 10 large inputs rejected, never truncated. |
| Correct external runtime compute and bounded records | [Frozen oracle execution](result-oracle-observation-2026-11-02.md), [39 semantic invariant executions](result-invariant-observation-2026-11-02.md); authentication limits and independent real mTLS coverage explicit. |
| Ordinary HTTP versus NPS two-hop runtime | [a4d80af measured observations](runtime-http-observation-2026-11-02.md): 720 measured workflows/24 conditions, negative latency/bytes for this NPS topology. Later code is not retroactively measured by that experiment. |
| Real C/D visible-token usage | [C/D observation](deepseek-cd-observation-2026-11-02.md): 30 exact reproductions with identical C/D usage; runtime already computed the answer. Not D20-05 live tool reasoning or NCP token savings. |
| Schema/update behavior and costs | [1860c19 observation](schema-update-http-observation-2026-11-02.md): 18/18 cases, 144 configurations, 4320 measured workflows and 487.16MiB peak at declared budget. Both prior failed attempts retained. Controlled transition then fresh snapshots; CPU time uncollected, no transition-cost/concurrent-update/delta claim. |
| Actual process CPU consumption | [3b624e4 observation](process-cpu-observation-2026-11-02.md): another complete 144-condition/4320-flow run, 30 actual CPU samples each, 477.59MiB peak; CPU median higher on D in 71/72 pairs, with the exception retained. Co-host process boundary, not per-protocol attribution. |
| Native NCP, Arrow/Flight/Protobuf efficiency | Explicitly excluded from these HTTP comparisons; [scope decision](../adr/2026-11-02-ncp-measurement-scope.md). D20-04 remains responsible for its declared qualification after prerequisites. |
| Provider total spend | Dedicated shared ledger records observed categories and conservative unknown-usage reservations, not an invoice. Unknown usage blocks a complete actual-cost ratio. |
| Suite readiness | D20-08/security/release gates remain; a result-reference fixture is not six-SDK or production PKI qualification. |

Record every model round under explicit provider/model/budget configuration. Do not
divide successful C/D cost by incorrect A/B answers as an equivalent-correct-task cost.
Historical CGN/fixed thresholds remain reconciled by the gate ledger. The negative
HTTP result triggers the linked scope decision, not a changed acceptance threshold.

## D20-01 AC completion evidence

| Acceptance item | Observed verification |
| --- | --- |
| Separate representations, runtime, provider usage and estimates | Maintained entry claims corrected in b42d079; this map and JSON capture schema separate local representation bytes/CGN, actual TLS/RSS/CPU and provider records. No byte-derived billing claim. |
| Four arms / same task / complete context failures | Original A/B and C/D records retain all scales, configured model/temperature/output budget and five repeats. A/B wrong answers and large context rejections remain failures, not truncated successes. C/D historical bounded reproduction is explicitly not live tool reasoning. |
| Reproducible workloads and exact answers | Frozen seed/oracle plus the preregistered schema/update method; 1860c19 and 3b624e4 each execute all 18 source conditions/144 configurations/4320 measured+432 warmup flows. Independent generator, exact integer answers/citations, old-snapshot probes and actual TRX all pass. |
| Complete comparable metrics / budgets | Cold/warm frontend and identity/gzip responses at the same TLS-stream boundary; all 30 latency/byte/RSS and latest CPU samples retained. Original 120s/512MiB/32KiB gates pass; CPU attribution includes co-host/GC/counter overhead and all earlier missing CPU remains unknown. |
| Honest negative observations / no predetermined win | Two failed schema/update attempts and earlier memory failures remain separate raw evidence. All 72 latest paired wall/byte medians favor ordinary HTTP; 71 CPU medians do, with the exception retained. The NCP scope ADR records the decision; native/Arrow/Flight/Protobuf superiority is explicitly excluded from this HTTP claim. |
| Complete provider accounting and claim limits | All attempts/probes and available usage classes retained in the shared USD10 ledger. Successful/probe peak-rate bound USD0.1017384 plus USD3.20 unknown-usage reservations; conservative total USD3.3017384, not an invoice or equivalent-correct-task savings ratio. No paid calls in runtime/CPU experiments. |
| Reviewable evidence / retained gates | Paired methods/observations, raw records, distributions and structural schema committed; 39 offline benchmark guards, frozen 39 semantic-design cases/12 alpha gates/57 prior tasks and source CI pass. Historical fixed thresholds and all original alpha/beta/security/qualification obligations remain retained. |

This completes the measurement package, not D20-02/03, D20-04 transport qualification,
D20-05 live Agent, six-SDK acceptance or #134 publication. Required dependency/registry
security checks still fail; no merge or waiver. Continue the authorized Epic using
the canonical issue dependencies rather than treating this evidence checkpoint as release readiness.
