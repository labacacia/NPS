# ADR: Focus alpha.20 on verifiable Agent data workflows

- Status: Accepted direction; detailed contracts and runtime qualification pending.
- Date: 2026-10-19.
- Decision basis: User accepted the NCP/data-workflow direction and requested a plan and Epic.
- Epic: [NPS-A20-DATA #163](https://github.com/labacacia/NPS-Dev/issues/163).
- Delivery plan: [English](../plans/2026-10-19-alpha20-data-workflow-plan.md) /
  [中文](../plans/2026-10-19-alpha20-data-workflow-plan.cn.md).

## Context

Schema references and binary framing can reduce repeated structure on a network.
They do not, by themselves, reduce model-visible tokens when a runtime expands the
same data into the same messages. Existing byte-based CGN and deterministic-context
benchmarks cannot establish provider billing savings.

NPS already has schema anchors, streams/diffs, NWP queries/actions/aggregation, NIP
identity/delegation, NDP discovery and NOP execution contracts. Alpha.20 candidates
also exist under #123; the new focus must reuse and reconcile that work, rather than
declare its candidate specifications either unimplemented in full or already qualified.

## Decision

1. Make a complete large-data workflow the alpha.20 product outcome. The model selects
   bounded operations; runtime code handles bulk data and returns authorized evidence.
2. Put result-resource semantics in NWP, verified identity and constrained authority in
   NIP, schema-bound transport in NCP, discovery in NDP and task composition in NOP.
   Storage, computation, model-adapter internals and query optimization remain runtime
   implementation concerns.
3. Define version-bound result references, lifecycle and access policy before
   implementation. A locator is not a bearer credential; a schema anchor cannot
   substitute for data and an endpoint failover cannot imply state replication.
4. Prove one single-node workflow before broad SDK porting and multi-node expansion.
   Qualify all six SDKs for the claimed shared surface before suite publication.
5. Measure representation, context routing and NPS-specific effects separately with
   full-JSON, schema-plus-rows, ordinary-HTTP-tool and equivalent-NPS paths.
   Predeclare evaluation criteria; report negative findings and unknown usage.
6. Reconcile the existing #123/ChangeControl gates through D20-00; retain all applicable
   requirements until their authority records a change. Preserve P20 IDs and use the
   existing #129..#134 implementation/qualification/release tracks.
7. Keep governed challenge experimental where unselected and beta promotion separate.
   Do not invent mandatory features, cosmetic version bumps or savings percentages.
8. Preserve the accepted [repository topology](2026-10-14-repository-topology.md):
   NPS-Dev is canonical; public release repositories are synchronized at release time.
   No repository role or release membership changes in this decision.

## Alternatives considered

- Continue framing the release around automatic token savings from schema references.
  Rejected because savings depend on the final model input and provider accounting.
- Build a general-purpose high-performance data format first. Rejected as the initial
  goal because existing formats and compressed HTTP are credible baselines and a
  transport result alone does not establish Agent workflow value.
- Implement every candidate governance/runtime feature and adopt it in every product
  before testing one data workflow. Rejected as the product sequence; existing binding
  obligations still need explicit gate reconciliation, not silent cancellation.
- Deliver only a model-side compact text format. Keep as a comparison, not the sole
  direction, because parsing accuracy, repeated inspection and runtime composition
  require independent validation.

## Security and operational considerations

Authorize every reference operation and derived result under explicit service policy;
cover tenant isolation, metadata, delegation, revocation, expiry and model egress.
Use synthetic fixtures and sanitized evidence. Tool data cannot change permissions.
Keep buffering, retained results and retries bounded. Do not automatically retry
unknown mutations or equate cancellation with rollback. Detailed persistence, migration,
retention and failover promises require contract ADRs before implementation.

## Consequences

The first delivery is an explicit contract and reproducible experiment, followed by a
reference workflow and qualified interoperability. The suite can retain useful NIP/NDP/NOP
behavior without expanding each protocol merely for release symmetry.
No additional NCP value over the equivalent HTTP workflow is a valid measured result,
but requires an explicit design/scope decision before broad porting.
This ADR accepts direction only: no wire revision, storage dependency, package bump,
runtime pass, release publication or beta promotion is approved by this file.

## Post-measurement scope decision — 2026-11-03

The user accepted bounded experimentalalpha20 closeout and full-production work later.
The [accepted closeout ADR](2026-11-03-alpha20-experimental-closeout.md) records the
required decision after negative NCP measurements, exact old-ID dispositions and
local CI authority. Historical failed/unexecuted cells are not promoted; publication
and beta remain separate. This addendum does not change repository roles.
