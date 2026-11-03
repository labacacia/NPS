English | [中文版](./2026-10-19-alpha20-data-workflow-plan.cn.md)

# Plan: alpha.20 — Verifiable Agent Data Workflows

Current scope update2026-11-03: the user accepted bounded experimentalalpha20 and local CI. [Accepted closeout ADR](../adr/2026-11-03-alpha20-experimental-closeout.md) and its original-ID dispositions supersede earlier whole-production applicability in this plan; original576/beta/adoption work stays explicit later. Current experimental exit uses the frozen seven reference configurations, six helper/package gates, current security/provenance and134handoff; no publication authority.

- Date: 2026-10-19.
- Status: accepted bounded experimental implementation/qualification COMPLETE; actual release/full production/beta separate.
- Epic: [NPS-A20-DATA #163](https://github.com/labacacia/NPS-Dev/issues/163).
- Authoring authority: `labacacia/NPS-Dev`; released suite baseline: `v1.0.0-alpha.19`.
- Planning source inspected: `main@8e27526`; this is not an alpha.20 release commit.
- Decision: [data-workflow focus ADR](../adr/2026-10-19-alpha20-data-workflow-focus.md).
- Ownership: NPS maintainers; individual assignees, dates and budgets remain unassigned.

Execution (2026-11-03): D20-00..08 accepted experimental AC COMPLETE; [D08 local evidence](2026-11-03-alpha20-experimental-ac-evidence.md). Actual suite release gates remain134. Epic #163 owns current state. [D07 evidence](2026-11-03-ndp-reference-ac-evidence.md).

## Outcome and reviewer path

An Agent completes a large-data task by choosing bounded operations while the runtime
queries, computes and transfers bulk data outside model context. The model receives
aggregates, authorized samples and result references, can drill down to source records,
and sees truthful execution outcomes. Model cost, network efficiency and reliability
are measured independently.

Read this plan for scope and acceptance, the ADR for rationale, then the linked
execution issues for implementation evidence. The initial request covered planning.
On 2026-11-02 the user authorized continuous Epic implementation in dependency order.
Current operating status and per-AC evidence live in Epic #163 and its child issues;
prerequisite gates and the separate publication decision remain in effect.

## Scope and protocol boundaries

| Protocol | Responsibility | alpha.20 delivery focus |
| --- | --- | --- |
| NCP | Transport schema-bound records and changes | Repeated-schema batches, bounded streams, deltas and resynchronization |
| NWP | Expose data and operations | Query/compute, result-resource lifecycle and bounded inspection |
| NIP | Bind identity and constrained authority | Result access, delegation, revocation and service-policy enforcement inputs |
| NDP | Resolve live endpoints and coarse capabilities | Freshness, admission on peer changes and explicit state ownership |
| NOP | Compose operations and report execution | Authorized result handoff, bounded retries, cancellation and recovery |

NWP leads the product contract. Start with NWP/NIP, measure NCP, prove a single-node
workflow, then add NOP/NDP multi-node qualification. Static endpoint configuration
remains sufficient for the single-node sample. Protocol versions advance only for
accepted semantic changes; there is no quota of new features or cosmetic bumps.

Keep the existing frame families and Query/Action operations where they fit.
New wire fields, operation names, feature IDs, storage guarantees and revisions
require the D20-00 contract decision and #129 freeze. The conceptual result descriptor
below is a requirements list, not a registered schema or an implemented API:

| Concept | Meaning that must be specified |
| --- | --- |
| Result locator and issuer | Locate a result and its serving authority; possession grants no access |
| Schema reference | Identify record structure; it does not contain or replace dataset contents |
| Snapshot/version | Bind reads, derived computations and evidence to one consistent dataset state |
| Owner and policy | Identify applicable principal/tenant, operations and row/field restrictions |
| Row count and bounded preview | Expose only metadata and records the caller is permitted to inspect |
| Expiry and release | State retention, cleanup, restart behavior and deterministic failure after expiry |
| Provenance | Trace computations, source records and upstream snapshot versions |

A schema AnchorFrame is distinct from an Anchor Node (cluster routing). A result
reference is distinct from both, and from a model provider's context/cache identifier.

## Baseline and reconciliation with existing work

The published release is alpha.19. The development specifications already contain
alpha.20 candidates: NCP 0.13, NWP 0.23, NIP 0.16, NDP 0.14 and NOP 0.11.
[The lifecycle matrix](../../spec/alpha20-lifecycle.md) and
[feature registry](../../spec/feature-registry.json) explicitly distinguish candidate
contracts from runtime qualification.

The existing [EPIC-007 #123](https://github.com/labacacia/NPS-Dev/issues/123) tracks
production contracts and beta readiness. This plan supplies the data-workflow product
focus and links that workstream; it does not erase its history or duplicate its P20 IDs.

| Existing work | Relationship to this plan |
| --- | --- |
| #124..#128, closed | Reuse negotiation, invocation binding, runtime, challenge and transport design history; closure is not proof of the new data workflow |
| #129, open | Own the final EN/CN contract, version and shared-vector freeze, including accepted data-profile deltas |
| #130, open | Own six-SDK behavior and API parity; D20-08 adds this profile's acceptance |
| #131, open | Own daemon/bridge integration of the accepted scope |
| #132, open | Reconcile the existing eight-adopter inventory and each binding commitment |
| #133, open | Own applicable mixed-version, security, fault and soak execution |
| #134, open | Own candidate materialization, publication decision and separate beta decision |
| #156 / #157 / #158, open | Account for Python canonical HTTP handling, the supported .NET HTTP client path and structured client errors |
| #155, open PR | Account for EXT-header interoperability evidence when qualifying transport |

D20-00 maps every A20 gate and relevant P20 obligation to a retained, adapted or
explicitly deferred disposition, with rationale, decision authority, owner role and
replacement evidence. Existing requirements apply until their authority records a
change. This document cannot waive external ChangeControl requirements.

| Existing gate family | Reconciliation and evidence owner |
| --- | --- |
| A20-G01 inventory | D20-00 / D20-08, retaining #132 |
| A20-G02..G04 design, versions and vectors | D20-00 / D20-02..D20-04, delivered through #129 |
| A20-G05..G06 SDKs and infrastructure | D20-08, retaining #130 / #131 |
| A20-G07..G08 adopters and adapter boundaries | D20-00 / D20-08, retaining #132; any exclusions need explicit disposition |
| A20-G09..G10 qualification and security | D20-03 / D20-05..D20-08, retaining #133 |
| A20-G11..G12 reproducibility and approval/publication | D20-08 hands off to #134; creating this plan is not publication approval |

Existing negotiated invocation-context and execution-certainty contracts support this
flow and remain applicable where selected. Governed challenge stays experimental and
is required only on operations that select its contract; its completed design is not
deleted. Deferring it from a new path cannot remove an existing consumer commitment.
Beta gates, including the separate clean interval and promotion decision, remain a
separate workstream. Reconcile #133's existing soak obligations explicitly before
changing its execution scope.

## Reference scenario and main flow

Use a seeded synthetic order/refund dataset with 100,000 records for the large case.
The task identifies refund-reason groups with abnormal growth over fixed time windows,
returns exact supporting aggregates and allows authorized drill-down. D20-00 freezes
the task, anomaly rule, schema, reference answers, field policies and evidence budget.
The data scale and scenario are planned fixtures, not claims of existing capability.

```text
User question -> model selects bounded query/compute operations
             -> NWP authenticates via NIP and checks resource/egress policy
             -> runtime queries and stores a version-bound result
             -> NCP transfers batches/deltas where needed
             -> model receives summary, bounded samples and result locators
             -> follow-up NWP inspection verifies source records

Later: NOP passes authorized references between workers;
       NDP resolves endpoints without implying that result state is replicated.
```

The model boundary is explicit: bulk intermediate results remain in the runtime unless
an authorized bounded inspection requests them. Model providers are an external data
egress boundary. Tool output is untrusted data and cannot change policy or authority.

## Non-goals

- Modifying model weights, tokenizers or provider billing rules.
- Claiming automatic model-token savings from binary transport, schema hashes or cache IDs.
- Building a new general-purpose database, query language or compression format.
- Mandatory global discovery, autonomous multi-Agent reasoning at every step, or deployment
  of every daemon to run the single-node example.
- Expanding CA/HSM/reputation infrastructure or promoting NIP Phase-3 by default.
- Shipping complete governed-challenge product UX or asserting beta readiness.
- Reviving archived repositories or requiring independent lifecycle products as release targets.

## Measurement and acceptance design

| Path | Model input and execution | What the comparison isolates |
| --- | --- | --- |
| A | Full JSON records enter model context | Direct-context baseline and context-limit failures |
| B | Schema plus positional rows enter model context | Representation benefit and any accuracy cost |
| C | Ordinary HTTP tools compute externally and return bounded evidence | Context-routing benefit without NCP |
| D | Equivalent NPS workflow with the same operations and evidence | Additional NPS transport, interoperability and recovery value |

C and D use the same source, compute logic, policy, visible evidence and model
configuration. A and B receive the equivalent task and data at each supported scale.
If a complete input does not fit, record a context-limit failure; do not truncate it
and count the task as equivalent. Measure 100, 1,000 and 100,000 rows and schema/update
variations, with enough preregistered repeats to report distributions.

Record actual provider input/output and available reasoning/cache usage across all
turns; unknown fields remain unknown. Report cold and warm cache cases separately.
Report wire bytes at a consistent complete framing boundary, compressed and uncompressed
HTTP baselines, runtime memory/CPU where available, latency distributions, task
correctness and fault outcomes. CGN/byte estimates, deterministic runtime observations
and provider usage remain separate evidence classes. Costs, if reported, identify the
dated provider price schedule and all applicable usage categories.

D20-00/01 freeze repetition count, model configurations, correctness tolerances,
resource/latency budgets and the selection rule before qualification. Initial orders
and aggregation answers must match the exact reference oracle; model conclusions and
record citations have an explicit scoring rubric. There is no predetermined savings
percentage. Absent a measurable advantage over C, publish that finding and review the
NCP scope before broad porting; do not attribute C's context-routing benefit to NCP.
A negative benchmark triggers a recorded decision rather than silently redefining
success.

The current [CGN benchmark](../benchmarks/token-savings.md) uses byte-based estimates
and hand-written traffic fixtures; the [context benchmark](../benchmarks/llm-context-savings.md)
uses an instrumented deterministic runtime. Neither establishes a provider billing
reduction. D20-01 corrects maintained claims and maps historical fixed savings release
thresholds in publishing docs and CI before changing any gate.

## Impact assessment and invariants

- Architecture: add a result-resource boundary inside NWP; keep storage/compute/provider
  details behind runtime boundaries. NCP supplies framing, NIP authority, NDP discovery
  and NOP composition. No unrelated layering or new protocol family is required.
- Security: server-side authorization on every operation, tenant isolation, constrained
  delegation, metadata/aggregate disclosure policy and revocation on existing/derived
  results. Identity possession alone is not resource permission.
- Data: snapshots, provenance, expiry/release, bounded storage and explicit restart and
  failover guarantees. Derived results cannot escape source policy by acquiring a new ID.
- Dependencies: no new storage engine, serialization dependency or real-model provider
  is selected by this plan. Choose after contract and comparative evidence.
- Operations: backpressure, timeouts, cancellation, capacity rejection and truthful
  indeterminate outcomes. Neither retry IDs nor cancellation imply exactly-once external
  effects or rollback.
- Documentation: update paired contracts, public APIs, samples, lifecycle/support claims,
  benchmarks and release instructions at the same canonical source.
- Trust boundaries: model/tool input -> policy gate -> result store/compute -> worker or
  external provider. Logs/evidence retain hashes, identifiers and decisions with redaction,
  not credentials or unnecessary payloads.

## Ordered implementation packages

| ID | Result | Depends on | Current state |
| --- | --- | --- | --- |
| [D20-00](https://github.com/labacacia/NPS-Dev/issues/164) | Reconcile baseline, gates and data-workflow contracts | Entry | COMPLETE (scoped reference/design AC) |
| [D20-01](https://github.com/labacacia/NPS-Dev/issues/165) | Establish fair benchmarks and correct token-savings claims | D20-00 | COMPLETE (scoped reference/design AC) |
| [D20-02](https://github.com/labacacia/NPS-Dev/issues/166) | Deliver the NWP result-resource lifecycle | D20-00 | COMPLETE (scoped reference/design AC) |
| [D20-03](https://github.com/labacacia/NPS-Dev/issues/167) | Enforce result authorization and constrained delegation | D20-00 | COMPLETE (scoped reference/design AC) |
| [D20-04](https://github.com/labacacia/NPS-Dev/issues/168) | Qualify NCP repeated-schema batch and delta transport | D20-00, D20-01 | COMPLETE (scoped reference/design AC) |
| [D20-05](https://github.com/labacacia/NPS-Dev/issues/169) | Demonstrate a single-node workflow with real-model evidence | D20-01, D20-02, D20-03, D20-04 | COMPLETE (scoped reference/design AC) |
| [D20-06](https://github.com/labacacia/NPS-Dev/issues/170) | Compose NOP tasks with authorized result references | D20-05 | COMPLETE (scoped reference/design AC) |
| [D20-07](https://github.com/labacacia/NPS-Dev/issues/171) | Qualify NDP multi-node discovery and state ownership | D20-05 | COMPLETE (scoped reference/design AC) |
| [D20-08](https://github.com/labacacia/NPS-Dev/issues/172) | Integrate data-workflow acceptance into suite qualification | D20-05, D20-06, D20-07 | COMPLETE |

D20-02 and D20-03 can develop after the same frozen contract, but remote result exposure
requires their integration. D20-04 follows the benchmark design, not a presumed token
saving. D20-05 is the evidence gate before broad SDK porting or multi-node expansion.
D20-06 and D20-07 can proceed after D20-05. D20-08 coordinates existing workstreams.

### D20-00 — Reconcile baseline, gates and data-workflow contracts

- Tracker: [#164](https://github.com/labacacia/NPS-Dev/issues/164); dependencies: none.
- Affected areas: spec/alpha20-lifecycle.md; spec/feature-registry.json; spec/NPS-{1..5}-*.md; docs/adr; existing ChangeControl EPIC-007 references.
- Work: Inventory released alpha.19 behavior, candidate alpha.20 contracts and actual runtime evidence. Map all A20-G01..G12 and relevant existing P20-* work to retained/adapted/deferred dispositions, with decision reference and owner role. Preserve existing security and compatibility gates until their authority records a change. Specify a result reference's issuer, schema, immutable snapshot/version, owner/scope, bounded preview, expiry and release semantics; these are semantic requirements, not preassigned wire fields. Reuse Query/Action and existing negotiation. Decide snapshot storage, restart behavior, revocation/derived-data rules, model-egress boundary and whether any operation needs governed challenge. Freeze protocol/version/vector changes through #129 before shared implementation.
- Verification: A reviewer can trace each existing gate and each new operation to its contract, owner boundary, failure behavior and planned executable vector. Required-feature requests reject before dispatch on unsupported peers. Contract decisions include alternatives and migration; no placeholder field becomes normative by appearing in the plan.
- Documentation: Update EN/CN protocol contracts and lifecycle together via #129; record detailed contract ADRs and a gate-disposition ledger. Preserve old issue IDs and candidate history.
- Exit: A minimal contract, benchmark methodology/acceptance criteria and reconciliation ledger are reviewable and accepted; unresolved choices are explicit implementation blockers, not invented guarantees.

### D20-01 — Establish fair benchmarks and correct token-savings claims

- Tracker: [#165](https://github.com/labacacia/NPS-Dev/issues/165); dependencies: D20-00.
- Affected areas: docs/benchmarks/token-savings.md; docs/benchmarks/llm-context-savings.md; docs/benchmarks/wire-size.md; impl/dotnet/benchmarks/NPS.Benchmarks.TokenSavings; README EN/CN; spec/NPS-0-Overview EN/CN; docs/publishing EN/CN.
- Work: Separate model-visible tokens, observed provider usage, wire bytes, runtime evaluation/cache metrics and CGN estimates. Preserve historical measurements with explicit limitations. Compare four paths: full JSON in context; schema-plus-rows in context; ordinary HTTP tools with external data processing; equivalent NPS tools. Use the same logical task, data, permitted evidence, model configuration and output budget. Record both compressed and uncompressed network baselines including full framing consistently. Compare Arrow/Flight or Protobuf for the transport claim where appropriate, or explicitly exclude the claim. Reconcile historical fixed 30%/50% release thresholds through the gate ledger rather than silently dropping them.
- Verification: Seeded scale sweeps (100, 1,000 and 100,000 rows), cold/warm caches, one-shot/schema-churn/steady-state workloads, bounded updates and exact reference answers are reproducible. Oversized prompts are reported as unsupported/context-limit failures, never silently truncated. Usage includes every model round and available output/reasoning/cache categories; unavailable categories remain unknown. Before running, freeze repetition count, correctness tolerance, latency/memory budgets and selection rule; publish raw observations and distributions, including negative results.
- Documentation: Publish methodology, machine-readable evidence schema and claim-to-evidence map; distinguish estimation/simulation from provider observations and actual cost calculations.
- Exit: The harness can attribute gains to representation, context routing or NCP independently; no unconditional model-token or billing savings claim remains in maintained entry documents.

### D20-02 — Deliver the NWP result-resource lifecycle

- Tracker: [#166](https://github.com/labacacia/NPS-Dev/issues/166); dependencies: D20-00.
- Affected areas: spec/NPS-2-NWP EN/CN; impl/dotnet/src/NPS.NWP/MemoryNode; impl/dotnet/src/NPS.NWP/Actions; SDK NWP contracts.
- Work: Reuse query/filter/fields/order/aggregate/actions for result creation, bounded inspection, follow-up computation and explicit release. Bind drill-down and derived results to the recorded snapshot and schema; distinguish snapshot reads from live subscriptions. Specify expiry, deletion, retention limits, capacity rejection, provenance and restart behavior. Model handles are locators and must pass the D20-03 authorization gate before exposure; do not add a bearer URL or a new general query language.
- Verification: Exact rows/counts/aggregates match the reference oracle; pagination and subsequent inspection cannot cross snapshots silently. Cover expiry/release, schema changes, malformed/unknown references, capacity exhaustion, bounded response sizes, cancellation and documented restart behavior. Execute through supported public clients, including structured errors, rather than only DTO round-trips.
- Documentation: Provide EN/CN operation/API examples, result lifecycle and recovery instructions, and storage/retention ADR.
- Exit: The reference server implements the frozen result contract with bounded resource use and audit-friendly provenance; remote exposure is gated on D20-03.

### D20-03 — Enforce result authorization and constrained delegation

- Tracker: [#167](https://github.com/labacacia/NPS-Dev/issues/167); dependencies: D20-00.
- Affected areas: spec/NPS-3-NIP EN/CN; NWP authorization boundaries; NIP verifier/delegation implementations; daemon/bridge context integration.
- Work: Bind each result operation to verified principal, tenant/resource scope, permitted operations and server policy. Define row/field access where claimed; carry identity through adapters without trusting caller-supplied context. A recipient of a delegated result must be authorized independently and cannot exceed the grantor's authority. Define source/derived-result policy, revocation and live-stream behavior, retention/deletion and external-model egress checks. Reuse NIP identity/trust and accepted invocation binding; avoid new cryptographic primitives.
- Verification: Cross-tenant guesses and copied handles, scope/field escalation, unauthorized aggregates, forged headers/projections, replayed grants and wrong audiences fail closed. Test delegated reads and revocation after derivation and during streams. Unauthorized metadata/errors/previews do not leak protected counts/fields; logs redact credentials and payloads. Outbound data reaches an external model only under the selected service policy.
- Documentation: Update EN/CN threat/data-flow documentation, permission examples and audit/redaction rules.
- Exit: A result handle confers no authority by possession; positive/negative access cases execute at real server and bridge boundaries. D20-02 and D20-03 integrate before remote workflow qualification.

### D20-04 — Qualify NCP repeated-schema batch and delta transport

- Tracker: [#168](https://github.com/labacacia/NPS-Dev/issues/168); dependencies: D20-00, D20-01.
- Affected areas: spec/NPS-1-NCP EN/CN; NCP codecs/anchor caches; NWP streaming binding; spec/conformance; wire-size benchmarks.
- Work: Verify the actual SDK representation before assuming positional rows remove field names. Reuse existing Anchor/Caps/Stream/Diff semantics; add a negotiated revision only for a demonstrated contract gap. Bind batches/diffs to their schema and data-base version; specify schema changes, sequence gaps, replay/resync and bounded buffering. Treat native/binary gains as transport results independently of model tokens.
- Verification: Round-trip equivalent data, missing/stale schema, reordered/gapped/duplicate updates, wrong delta base, reconnect, slow consumers, window exhaustion, cancellation and oversized frames. Correct resynchronization produces the same snapshot as the source oracle. Measure compressed HTTP and alternative-format baselines at the same boundary; report small-payload/schema-churn regressions and allocation/peak-memory behavior.
- Documentation: Publish EN/CN encoding/negotiation/recovery rules and transport benchmarks. Preserve alpha.19 behavior when new semantics are not negotiated.
- Exit: A bounded repeated-schema stream recovers safely or returns a deterministic error; every advertised transport gain is measured rather than inferred from CGN.

### D20-05 — Demonstrate a single-node workflow with real-model evidence

- Tracker: [#169](https://github.com/labacacia/NPS-Dev/issues/169); dependencies: D20-01, D20-02, D20-03, D20-04.
- Affected areas: demos/ (new self-contained sample); deploy/dev-stack; reference .NET runtime; Python SDK public client; benchmark evidence.
- Work: Build one synthetic order/refund-analysis workflow with 100,000 seeded records and exact expected aggregates/anomalies. Use the .NET reference server plus Python public client initially. The model selects bounded operations; the runtime handles bulk data and returns aggregates, samples and authorized result references. Allow drill-down to records and trace their snapshot/version. Keep the full dataset outside context by default, with an explicit inspected-result budget. A real provider run is an execution task requiring configured credentials and a declared spend budget; no such run is claimed now.
- Verification: Run all four D20-01 paths where supported. Verify task correctness, faithful evidence references and follow-up drill-down; count all model turns and retries. Separate deterministic CI from opt-in real-provider measurement. Measure small and large datasets, cold/warm context caches and one update cycle. Missing model credentials is reported as missing evidence, never replaced by simulated billing.
- Documentation: Provide reproducible EN/CN setup, commands, fixtures and sanitized evidence. Sample depends on public SDK APIs; do not revive archived NPS-Examples or require private monorepo-relative packages in the distributed version.
- Exit: The same task completes through an ordinary HTTP and an NPS tool workflow with comparable evidence. Publish whether NPS adds measurable value; equal or worse results trigger documented scope/design review before broad porting.

### D20-06 — Compose NOP tasks with authorized result references

- Tracker: [#170](https://github.com/labacacia/NPS-Dev/issues/170); dependencies: D20-05.
- Affected areas: spec/NPS-5-NOP EN/CN; NOP task/delegation/AlignStream implementations; tools/daemons/nps-runner; shared fault vectors.
- Work: Compose query -> analysis -> report using result references and least-privilege grants. Declare task inputs, result ownership and cleanup after downstream completion/expiry. Plans may be deterministic or model-produced; model involvement is not required for each step. Preserve stable operation identity, bounded retry/deadline budgets, durable deduplication claims and not_dispatched/known_terminal/indeterminate outcomes. Cancellation cannot promise rollback of committed external effects.
- Verification: Two authorized workers exchange results without routing bulk payloads through model context. Test unauthorized handoff, expired inputs, duplicate delivery, lost responses, restart, cancellation races, partial failures and late terminal evidence. Unknown mutations are not automatically replayed; downstream tasks do not consume unproven outputs. Measure referenced transfer costs as well as model-visible bytes.
- Documentation: Add EN/CN composition, ownership and recovery examples and outcome evidence mapped to existing runtime contracts.
- Exit: A reference DAG completes with valid provenance and bounded resources; failure cases produce truthful outcomes without duplicated mutations or implied rollback.

### D20-07 — Qualify NDP multi-node discovery and state ownership

- Tracker: [#171](https://github.com/labacacia/NPS-Dev/issues/171); dependencies: D20-05.
- Affected areas: spec/NPS-4-NDP EN/CN; NDP resolution/liveness; NWM capability manifests; tools/daemons/nps-registry; multi-node sample.
- Work: Discover endpoints, coarse capabilities and required feature revisions; keep detailed schemas in NWP manifests. Validate TTL, health/drain and owner epochs and renegotiate on peer changes. Keep a static-endpoint single-node path. Record whether a result is pinned to its issuer, backed by shared durable storage or unavailable after failover; endpoint replacement cannot invent replicated state.
- Verification: Stale/draining records are excluded, spoofed records cannot grant authority, required unsupported features reject before dispatch, and peer changes trigger fresh admission. Test stale capabilities, issuer loss, state unavailable at a successor, sequence/epoch rollback and split-brain. Report unsupported state migration explicitly; do not silently rerun mutations.
- Documentation: Provide EN/CN static and registry deployment paths, state-ownership/failover guarantees and tested topology.
- Exit: A multi-node workflow resolves valid capabilities and reports state loss/recovery correctly while the single-node workflow remains independently runnable.

### D20-08 — Integrate data-workflow acceptance into suite qualification

- Tracker: [#172](https://github.com/labacacia/NPS-Dev/issues/172); dependencies: D20-05, D20-06, D20-07.
- Affected areas: existing SDK/daemon/qualification issues; spec/alpha20-lifecycle EN/CN; release support ledger; tools/release; docs/publishing EN/CN.
- Work: Extend existing #129/#130/#131/#133 tracks with the accepted data-profile vectors and public API/error behavior instead of creating competing SDK implementations. Qualify all six SDKs for every claimed shared surface and the reconciled native/HTTP compatibility matrix. Link adopter evidence via #132 without imposing independent products as mandatory release members. Reconcile every old gate and fixed savings threshold via D20-00 decisions. Prepare a reproducible pre-release candidate using NPS-Dev sources and release ownership tooling.
- Verification: Exact revisions, toolchains, fixture hashes, executed pair/binding cells, unsupported paths, fault/security results and residual findings are recorded. Zero-dispatch rejection is observed where required. Verify package dry-runs, source/distribution drift and applicable dependency/security gates. No advertised feature has codec-only evidence. Each retained old gate has evidence or an explicit unresolved blocker; never convert an unsupported/failed cell into a pass.
- Documentation: Complete EN/CN contracts, public APIs, migration, support/experimental ledger, samples, measured claims and release notes. Preserve the separate beta gate set and publication ledger.
- Exit: All reconciled alpha.20/data-workflow gates have evidence and a pre-release handoff to #134 exists. This issue does not publish, claim beta readiness, or close the parent based solely on issue creation.

## Rollout, compatibility and rollback

1. Record D20-00 scope/gate decisions and freeze additive EN/CN contracts/vectors through #129.
2. Implement the reference runtime and one public client with explicit experimental opt-in;
   preserve unchanged alpha.19 behavior. Unsupported required semantics reject before dispatch.
3. Complete single-node evidence, then task/discovery integration and all claimed six-SDK
   behaviors. Each compatibility cell records exact versions, binding, fixture and result.
   Reuse the lifecycle matrix; codec presence is not runtime qualification.
4. Materialize a reproducible candidate through existing release ownership tooling only
   after acceptance. Synchronize the corresponding sources from NPS-Dev to NPS, six SDKs,
   NPS-Daemons and NIP-CA-Server. Independent lifecycle repositories remain independent.
5. Hand the qualified candidate to #134 for the existing release/publication process.

Rollback disables new capability advertisement and admission before reverting the runtime;
it must reject new required semantics rather than expand references into unbounded payloads.
Already-created references receive the frozen retention/recovery treatment. If incompatible
storage changes become necessary, their ADR must define migration/export and recovery before
deployment. Published package versions are immutable; fixes use a later version. No rollback
can retract data already sent to an authorized external provider.

## Decisions and open questions

The focus ADR accepts direction only. D20-00 must decide result-store durability/capacity,
reference/snapshot semantics, source-policy changes and derived data, delegated access,
supported compute subset, egress policy, exact experimental revision and the old-gate
dispositions before dependent implementation. D20-01 freezes benchmark parameters before
measurement. Real-provider configuration and spend budget are not assumed to exist.

The present checkout has orientation/architecture guidance on pending normalization PR
#162, with no glossary file there at inspection; use the existing spec terminology until
that documentation work is reconciled. This is a documentation gap, not a reason to invent
another protocol vocabulary.

## Definition of done and evidence

- Each claimed operation has accepted EN/CN contracts, public API documentation and shared
  positive/negative/fault vectors, with implementation PR and exact revision links.
- The reference scenario completes with correct aggregates, auditable drill-down and bounded
  model-visible data. All four comparison paths are reported or explicitly unsupported.
- Result access, delegation, revocation, expiry and model-egress controls execute at real
  boundaries; schema and delta recovery, retry/cancel/restart/failover results are recorded.
- All six SDKs satisfy the claimed shared data contract and applicable native/HTTP matrix;
  unsupported or unexecuted paths cannot count as passed.
- Every retained/adapted old gate has its mapped evidence; unresolved requirements remain blockers.
  No applicable security/compatibility gate is waived by this plan.
- Published claims match the observed metrics, including failures or lack of advantage.
- A reproducible pre-release handoff exists for #134. Publication and beta promotion remain
  separately tracked decisions, not conditions satisfied by creating this Epic.

No implementation work package is complete at plan creation.
