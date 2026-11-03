English | [中文](2026-11-03-alpha20-experimental-closeout.cn.md)

# ADR: Close alpha.20 as a bounded experimental data-workflow candidate

- Status: Accepted by the user,2026-11-03 (Australia/Adelaide).
- Authority: the user selected “按已验证实验范围收口 alpha.20，重审旧门槛，全量生产能力排后续（建议）” and instructed “暂时不用Github Actions，CI跑本地”. This supersedes the pending choice in the [scope proposal](../plans/2026-11-03-alpha20-scope-decision-proposal.md).
- Trace: Epic163, D20-00/08, existing129..134; supplements the [direction ADR](2026-10-19-alpha20-data-workflow-focus.md).

## Decision and claims

Alpha.20 closes the verified experiment. Its outcome is correct, bounded Agent data
work outside model context, with authorized evidence and truthful failure/recovery.
NWP/NIP result HTTP is supported only for the .NET/Python public result clients to
the .NET reference server. NCP schema batches remain an opt-in .NET native reference;
NOP is a four-local-worker reference; NDP is a volatile two-node reference. Existing
six-SDK parsing/error helpers and regressions remain qualified as helper/codec evidence.
They do not claim full data-result/runtime interoperability. Default advertisement
stays disabled. Native result-resource operations, unimplemented SDK result paths,
remote production NOP, durable production NDP, full production-runtime/governed-challenge
profiles and adoption across eight products are later work, not alpha.20 claims.

Measured negative NCP results and the ordinary-tool/NPS-tool80/80 outcomes remain
published with exact sources and limits. Alpha.20 replaces the legacy universal30%CGN/
50%wire release thresholds with exact correctness, resource bounds, fair independent
comparison and honest measured claims. Historical thresholds/results are preserved,
not marked achieved. No universal network, token, latency or provider-billing benefit
is promised. Broad NCP porting is deferred; this records the direction ADR's required
post-measurement scope decision rather than inferring value from the protocol name.

## Applicable qualification and original IDs

The current [experimental matrix](../qualification/alpha20-experimental-matrix.json)
freezes required evidence for claimed paths. Each executed row must refer to actual
source-bound functional/network/counter or historical measurement artifacts; parser
cases cannot satisfy a runtime row. The old576-cell matrix stays unchanged and wholly
unexecuted in its historical ledger; full-production execution is deferred under133.
[Original-ID reconciliation](../qualification/2026-11-03-alpha20-gate-reconciliation.json)
retains all12 A20 gates and57 P20 IDs with explicit adapted/deferred/release dispositions.
Completing an adapted experimental gate does not close the original whole-production
obligation or turn an unsupported/failed cell into a pass. Existing129..134 remain the
follow-up tracks; no duplicate production implementation or fake completed child is made.

Current-scope security/privacy negatives, receiver authority, zero-dispatch rejection,
outcome certainty, bounded memory/data egress, dependency/security checks, provenance
and reproducibility remain mandatory. A20-G10/G11 are not deferred wholesale. General
Python NwpClient #156 is a reproduced failure; the qualified ResultHttpClient path is
separate. General HTTP contract/error issues156/157/158 stay open under130; no overall
NWP HTTP qualification is claimed by experimental scoping.

Full-production/adopter ownership and migration remain132/133 follow-ups. Current
repository roles and release membership remain as in the topology ADR; independent
Model-Runtime/Console/Paper and archived products are not mandatory experimental
adopters and are not migrated. No repository role or wire/package coordinate changes.

## Local CI and publication boundary

Local CI is the current execution authority. Automatic push/PR/schedule/repository
notification triggers are disabled in validation workflows; retained manual definitions
are not invoked. Use the local runner and fresh source-bound logs/receipts. Existing
remote results remain historical evidence. Do not forge remote check statuses or
require GitHub Actions secrets for local checks. No model API calls are part of CI.

A missing Cargo token or GitHub secret does not block compile, test, audit or local
unpublished packaging. Actual publish authority, Maven signing, SourceLink from the
real release source, final version20 materialization/projection coherence and a valid
release-candidate receipt remain explicit134 prerequisites before tagging/publishing.
Local unsigned/non-Git snapshots are labeled as such. D08 must deliver reproducible
source-bound experimental artifacts and a truthful134 handoff; it does not claim
ready-to-tag or approval to publish. All12 beta gates and the21-day interval remain
separate, unqualified and unwaived. No tag, registry upload, Gitee write or publication
is authorized by this decision.

## Verification and completion

Freeze this decision/matrix before the local run. Require six-SDK regressions and
helper parity, actual claimed-path negatives/successes, current dependency checks,
full-history secret scan, version/oracle consistency, immutable original12 data assets,
source/package provenance and the recorded benchmark/model evidence without rerunning
closed paid captures. Preserve failures and exact source/toolchain/test artifacts.
D08/Epic163 complete only when those current experimental AC pass and all future or
release obligations have traceable dispositions/handoff. Separate #134 remains open.
