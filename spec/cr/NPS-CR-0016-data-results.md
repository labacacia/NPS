# NPS-CR-0016: Version-bound Data Result Resources

- Status: Draft (contract authored; runtime qualification pending).
- Target: v1.0.0-alpha.20.
- Date: 2026-11-02.
- Discussion: [D20-00 #164](https://github.com/labacacia/NPS-Dev/issues/164); normative integration #129.
- Decision: [result resource boundary](../../docs/adr/2026-11-02-data-result-resource-boundary.md).

## Summary and motivation

Expose finite, immutable, authorized results using existing NWP actions so Agents can
inspect/compute without routing bulk intermediate data through model context.
Schema compression, context routing and provider accounting remain separate claims.

## Specification changes

The exact optional contract is [data-results.md](../profiles/data-results.md), with its
paired Chinese text and machine contract/corpus under spec/conformance/alpha20.
Register nps.data_results revision 1 as experimental/default-off. Use no new frame,
endpoint, authentication method or change to unselected base operations. Existing
negotiation and ActionFrame.Params already provide the extension boundary. The core
protocol versions remain unchanged; the feature registry advances to 0.2,
the error registry to 1.12 and status mapping to 0.9 for six result-specific codes.

## SDK changes

.NET provides the first result-serving reference implementation; Python is the first
public consumer. TypeScript, Go, Java and Rust execute the same shared contracts
before advertising any claimed operation. API idioms may differ; observable behavior,
errors, policy and snapshot semantics cannot. No SDK feature is enabled by this CR.

## Conformance changes

Add portable scenarios and an independent contract oracle, gate-reconciliation ledger
and preregistered methodology. Exact bytes/digests identify a frozen corpus.
Oracle output is design evidence; public-route/SDK/runtime qualification remains
D20-02..08. Existing P20-108 missing corpus is retained, not declared complete.

## Migration and out of scope

Alpha.19 operations continue unchanged. Required new features fail before dispatch
on old/disabled peers; do not silently expand handles to full payloads.
No new CA-to-CA grant protocol, durable/cross-node store promise, general query
language, arbitrary SQL execution, model-provider integration or business write action.
No archived/independent repository is added to the release train.

## Acceptance

- [x] Semantics, limits, trust/data boundaries, errors, recovery and reference operations specified.
- [x] Paired EN/CN profile and machine-readable gate/methodology artifacts authored.
- [x] Contract oracle, 39 shared fixed-byte scenarios and 18 negative/boundary self-tests verified locally.
- [ ] Data-result runtime and public clients qualified independently.
- [ ] Six-SDK/adapter and alpha.19 compatibility evidence accepted through #129/#130/#133.

## Proposed changelog entry

Experimental result resources: issuer/snapshot-bound references, bounded inspection,
source-policy-aware access, process-local retention and explicit recovery semantics.
Availability depends on the qualified support ledger, not the package version.

## Open questions

Revision 1 deliberately selects process-local retention and conservative live-policy
intersection. Durable storage, multi-host result migration and recursive resource
delegation require later contracts, not speculative interfaces in this phase.
Real-provider choice and spend cap remain execution configuration, and block paid runs
until recorded. No current result or provider-token saving is claimed.
