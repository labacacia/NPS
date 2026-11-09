# D20-05 implementation coverage

Status: COMPLETE for experimental reference and measurement AC. Epic #163 / child
#169; continuous implementation authorized. Controlled qualification fills the original
resource gap without rewriting the first matrix's failures. Integration/all-SDK/security/
registry/P20 and publication gates remain separate and unwaived.

## Scaffold and boundaries

Use the existing flat `demos/data-workflow/` layout with a .NET10server and Python
public SDK client/runner. Apply Codebase's demo contract at
9d4010702e14213a8749aee3a7c0af0ceaa81478; no root regeneration/application layers.
Server has normal NuGet PackageReferences; Python installs a public-API wheel.
No runtime private SDK paths or copied production implementation. Unpublished SDK
artifacts bind clean source109ea55e11609645aa8524b36e0c33f55e5733fa and exact hashes;
canonical/released versions remain alpha.19. Archived experiment recipes are inert
reproducibility evidence, not divergent SDK implementations.

Generated keys,SQLite,environments,logs and working captures stay in ignored
`.demo-output`. Only explicitly sanitized closed evidence is committed. Existing
repository roles/release boundaries and unrelated demos remain independent.

| AC | Executed evidence | State |
| --- | --- | --- |
| Exact finite100000-row source/anomalies | Frozen D20-01oracle; actual SDK/HTTP at100/1000/100000; actual model at100/100000,baseline/update | COMPLETE evidence |
| Public.NET/Python lifecycle/current authorization | Installed fourNuGets+wheel,real two-leg mTLS/currentNIP/freshprojection/SQLite replay; same public provider/source/policy/store for ordinary adapter | COMPLETE reference evidence |
| Actual model selection,faithful drill-down | 80workflows/530requests/490actualturns/40paidprimes;39/40success per arm; two final errors rejected and preserved;50offline guards; five inspected rows each | COMPLETE measurement,not100%model reliability |
| Fair ordinary/NPS comparisons and update | Same tools/task/operators/policy/budgets; allfive repeats; both old snapshots unchanged,new fences and exactamountdelta; rawA/B historical failures retained separately | COMPLETE evidence and negative scope decision |
| Model usage/cache/spend/failures | All530new paid attempts map exactly to observations; actual cache counts; primes hash-identical to first real request; no automatic retries; original60attempts preserved | COMPLETE accounting |
| 512MiB physical resource envelope | Separate complete controlled matrix at126c5df: all80guards pass; actual4CPU/320MiB-managed/512MiB-actual-tree/120s; RSS451653632/HWM456880128bytes | COMPLETE controlled evidence; original558MiB failure preserved |
| Reproducible independent delivery | EN/CN setup/run/check/negative/cleanup/limits; independent installed-package distribution passes actualthree-scales;49guards pass/1private-source-file comparison skipped; closed live packet verifies without key,SDK,private original ledger or network | COMPLETE: current87checks; independent86pass/1expected private-source comparison skip; actualthree-scale checks pass |

## Executed source and artifacts

Python client source109ea55 ([PR200](https://github.com/labacacia/NPS-Dev/pull/200)):
55new contract tests/92.95%module coverage;fullSDK1513pass/92.13%.
Real local server/delivery46a9c0b ([PR201](https://github.com/labacacia/NPS-Dev/pull/201)).
Live Agent sourceafafebd15a6cebacfaede01193225e4422ae05f5
([PR202](https://github.com/labacacia/NPS-Dev/pull/202)) preregistered before calls.
CI20041549430033,20141550846343,20241552367438 allpass;202 includes actual package
build/install/three-scale mTLS/offline-Agent checks without model credentials.

[EN observation](../benchmarks/data-workflow-agent-observation-2026-11-03.md) /
[中文](../benchmarks/data-workflow-agent-observation-2026-11-03.cn.md),
[closed packet](../../demos/data-workflow/evidence/live-tool-agent-afafebd/index.json),
[scope/resource decision](../adr/2026-11-03-data-workflow-agent-scope.md).
Every case and failure,usage/charge/recipe/package/assembly hash is preserved.
Actual first cold turns:0/40cache hits;warm:40/40. No exclusive NPS token/fee/latency
advantage established. Prior A/B20wrong+10context failures and C/D30evidence
reproductions remain historical; neither is relabeled as a live tool Agent.

## Trust, limits and outcomes

Owner mTLS possession maps to a server-provisioned current NIP Ident and source
policy. Gateway strips inbound projection hints and uses a separate authenticated
receiver leg. Model arguments cannot choose network/tenant/key/principal or SQL/code.
Operator-only source update is not a fifth result action. Synthetic provisioned trust
is not production PKI. Native result/conditional derived/stream contracts remain
unsupported/unregistered; no default feature offer or frame number changes.

Both arms keep bulk rows outside model context;actual inspection is five rows with
6.7–6.8KiBtypical cumulative tool output,not provider token counts. Host maps actual
descriptors,checks exact aggregate-derived answers and actual complete citations.
Two model failures remain failures. Unknown create/cancellation/release outcomes are
not asserted rolled back; no implicit new-key recovery. Source version differs from
resource policy version. Cleanup and expiry remain explicit.

## Controlled qualification and next action

Measured source126c5df2ca069309c231b2ef6b5e58778f503b2a was committed/preregistered
before new calls ([PR204](https://github.com/labacacia/NPS-Dev/pull/204)). The full80case
v2 matrix completes40/40 perarm with faithful five-row citations;528paid requests
include488actual turns and40primes. All resource guards pass under unchanged controls:
RSS451653632/HWM456880128bytes, maximum21.899014s. Both updates preserve old snapshots
and create new fences. No new provider/tool/cleanup/unknown failures or automatic retry.

[Controlled EN observation](../benchmarks/data-workflow-agent-controlled-observation-2026-11-03.md) /
[中文](../benchmarks/data-workflow-agent-controlled-observation-2026-11-03.cn.md),
[closed packet](../../demos/data-workflow/evidence/live-tool-agent-bounded-v2-126c5df/index.json).
All failed/passing no-model resource preflights remain in their separate packet.
The source/launch correction is shared by both arms, not protocol memory superiority.
Comparable model usage does not establish an NPS-exclusive token/fee/latency benefit.

Two independent-review fixes follow sealed measurement: preserve unknown ordinary
creation keys and forbid settling foreign/sealed reservations.13fault regressions
pass; current full demo87/87, independent installed-package86pass/1expected private-
source comparison skip. Both packets verify with standard Python only. Independent
actual100/1000/100000 mTLS workflows pass after copying current demo sources.

All paid calls use the SAME original `.artifacts/alpha20/deepseek-ab-run.json`.
USD10cap; all1118attempts retained. Newv2boundUSD0.5478462;totalUSD4.3990379,
remainingUSD5.6009621 includes historicalUSD3.20unknown reservations. Upper bounds,
not invoices. No more calls are needed for D05 and neither matrix is executed again.

Next agent action: D20-06 #170 authorized result handoff and truthful task outcomes,
then D20-07 and D20-08 in dependency order. No merge,projection,publication or
security/dependency/registry/P20/resource waiver.
