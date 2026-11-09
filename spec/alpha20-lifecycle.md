English | [中文版](./alpha20-lifecycle.cn.md)

# Alpha.20 compatibility and beta lifecycle matrix

- Status: bounded experimental reference configurations qualified; full-production/beta matrix deferred
- Date: 2026-09-09
- Trace: P20-109, A20-R001/R003, [#129](https://github.com/labacacia/NPS-Dev/issues/129)
- Machine-readable cases: [compatibility-matrix.json](./conformance/alpha20/compatibility-matrix.json)

## Version boundaries

The current oracle remains [version-matrix.yaml](./version-matrix.yaml).
The candidate contract targets below are pinned in that oracle by the normative
integration change. They are not package availability or runtime qualification claims.

| Contract | alpha.19 | alpha.20 candidate contract |
|---|---|---|
| NCP | 0.12 | 0.13 |
| NWP | 0.22 | 0.23 |
| NIP | 0.15 | 0.16 |
| NDP | 0.13 | 0.14 |
| NOP | 0.10 | 0.11 |
| Frame registry | 0.15 | 0.15 (no new frame type or flag assignment) |

Suite/package versions do not prove feature support. Existing compatibility floors remain
unchanged unless a breaking requirement is recorded with a migration decision.
NDP §7.9 defines runtime discovery/failover evidence; NOP §3.6 defines outcome certainty
and retry/cancellation binding. These are substantive deltas, not empty version bumps.
The feature registry is version 0.3; error/status mappings are 1.12/0.9.
Review/CI integration remains the P20-105/107 completion gate.

Current applicability: the user accepted the [bounded experimental closeout](../docs/adr/2026-11-03-alpha20-experimental-closeout.md)
on2026-11-03. The576-cell definition below is retained as the deferred full-production
matrix; it is not silently passed or required as an all-SDK runtime claim for this
experiment. Current acceptance is the separately frozen seven reference configurations
plus six helper/package gates and mandatory reference security/provenance/local CI.

## Bidirectional behavior

Each row applies to all 36 ordered client/server SDK pairs (.NET, Python, TypeScript,
Go, Java, Rust), separately on native and canonical HTTP paths: 576 required cells.
An unavailable binding is recorded as unsupported, not passed or silently skipped.

| Case | Client → server | Preconditions | Expected behavior |
|---|---|---|---|
| CMP-01 | alpha.19 → alpha.19 | unchanged baseline request | Existing behavior preserved |
| CMP-02 | alpha.19 → alpha.20 | no new feature fields | Existing behavior preserved; no new authority |
| CMP-03 | alpha.20 → alpha.19 | optional new features, safe legacy path | Omit new semantics and preserve baseline behavior |
| CMP-04 | alpha.20 → alpha.19 | required new feature | Client rejects before dispatch; zero handler invocations |
| CMP-05 | alpha.20 → alpha.20 | exact common enabled revision | Select and execute the agreed revision |
| CMP-06 | alpha.20 → alpha.20 | required revision mismatch | Reject before dispatch; zero handler invocations |
| CMP-07 | alpha.20 → alpha.20 | experimental revision not locally enabled | No selection; reject if required |
| CMP-08 | alpha.20 → alpha.20 | caller spoofs context/feature support | No authority expansion; reject untrusted projection |

Native fallback follows CR-0012: old peers need not understand optional offer metadata.
For HTTP, an authenticated discovery response alone is insufficient: the route must
guarantee admission enforcement on every backend before a required-feature request is
sent. A mixed deployment with an old backend fails that precondition. Response headers
cannot retroactively prevent side effects. A fallback must not carry new context or
challenge semantics that a legacy handler could accidentally consume.

## Feature lifecycle

| Surface | alpha.20 lifecycle | Beta promise |
|---|---|---|
| Existing unchanged protocol behavior | Baseline compatibility target | Included only when listed as supported in beta.1 manifest |
| Feature negotiation metadata version 1 | Proposed in CR-0012; qualification pending | No automatic promotion from package version |
| nps.invocation_context revision 1 | Experimental, explicit opt-in | Excluded until explicitly promoted with evidence |
| nps.production_runtime revision 1 | Experimental, explicit opt-in | Excluded until explicitly promoted with evidence |
| nps.governed_challenge revision 1 | Experimental, explicit opt-in | Excluded until explicitly promoted with evidence |
| nps.data_results revision 1 | [Experimental result resources](profiles/data-results.md); runtime qualification pending | Excluded until explicitly promoted with evidence |

For supported beta surfaces, wire fields, feature IDs/revisions, error meanings and
public SDK APIs evolve additively: no removal, incompatible repurposing or silent
authority expansion. Deprecation does not imply removal during beta. Experimental
revisions remain explicit opt-ins on both peers; a remote stable label cannot override
local policy. A semantic break requires a new revision and migration decision, not
reinterpretation of the same revision. Security emergencies require a published
advisory and containment/migration path, never an undocumented exception.

## Qualification evidence

Each executed cell records case ID, binding, client/server SDK and exact revision,
fixture hash, environment, time, observed result, handler invocation count and artifact
reference. CMP-04/06 require zero dispatch evidence, not merely a client exception.
CMP-08 requires unchanged admitted subject/scope/capabilities as well as rejection.
No result is prefilled in the matrix. P20-501 owns execution; P20-508 dispositions cannot
turn an unsupported or failed cell into a pass.

Beta remains conditional on all BETA-G01..12, the 21-day clean interval, residual-risk
report and separate publication decision. Internal mandatory adoption alone is not
beta qualification. This document does not authorize publication.

Current D20-08 evidence is recorded in the [support ledger](../docs/qualification/2026-11-03-alpha20-support-ledger.json) and [retained-gate reconciliation](../docs/qualification/2026-11-03-alpha20-gate-reconciliation.json). All576 required runtime cells remain unexecuted; parser/codec/regression evidence does not populate them. These current ledgers supplement, and never rewrite, the original12-artifact freeze.
