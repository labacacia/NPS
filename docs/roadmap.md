English | [中文](roadmap.cn.md)

# Roadmap

The latest published suite is **1.0.0-alpha.20**, released 2026-11-10. This roadmap distinguishes delivered work, remaining engineering work and release decisions.

## Delivered milestones

| Milestone | Result |
| --- | --- |
| Protocol foundation | Five versioned protocols with shared frame, error and status registries |
| SDK ecosystem | .NET, Python, TypeScript, Java, Rust and Go packages published at alpha.20 |
| Reference services | Four public daemons and the separately deployed NIP CA, with locally verified container builds |
| Alpha.20 data-workflow experiment | Bounded result resources and authorization, repeated-schema batches, local task handoff and volatile node transfer |
| Alpha.20 publication | Verified registry artifacts, GitHub/Gitee releases and the suite version manifest |

Read the [alpha.20 release](https://github.com/labacacia/NPS/releases/tag/v1.0.0-alpha.20), [SDK guide](sdks.md), [support ledger](qualification/alpha20-support-2026-11-03.md) and [experimental acceptance evidence](plans/2026-11-03-alpha20-experimental-ac-evidence.md) for the delivered scope.

## Engineering follow-up

| Area | Remaining work |
| --- | --- |
| General NWP HTTP clients | Canonical MIME negotiation and capsule decoding in Python, a supported .NET public HTTP path and preservation of protocol error details |
| Six-SDK data workflows | Qualify the additional operations, transports and language combinations beyond the accepted reference configurations |
| Production runtime | Complete the production negotiation, invocation-context, bounded runtime and governed-action contracts and qualify their implementations |
| Orchestration and discovery | Qualify remote task handoff, durable discovery/state recovery and multi-node failure behavior |
| Adopter integration | Assign product-specific ownership and run the required integration and migration qualification |

The [accepted scope decision](adr/2026-11-03-alpha20-experimental-closeout.md) and [original gate dispositions](qualification/2026-11-03-alpha20-gate-reconciliation.json) retain the outstanding production obligations. Work is ordered from normative contracts through SDKs and services to adoption and qualification.

## Beta decision

Beta readiness requires a review of the published alpha.20 against the beta gates, followed by an explicit decision on beta.1, another alpha or a longer observation period. A beta release date has not been set.

The applicable gate review remains a follow-up to the delivered experiment. Release numbering alone does not establish completion of the production matrix.

## Design exploration

The result-resource contract leaves durable storage, multi-host migration and recursive delegation for later design. Schema-batch adoption in result streams and subscriptions also needs its own policy, binding and value assessment.

See [CR-0016](../spec/cr/NPS-CR-0016-data-results.md) and [CR-0017](../spec/cr/NPS-CR-0017-schema-batches.md) for these boundaries. The existing HTTP gzip and ordinary-tool measurements remain part of the evidence used to choose future work.

## Project boundaries

NPS-Dev owns authoring and release tooling. NPS, six SDKs, NPS-Daemons and NIP-CA-Server form the suite release projections. Model-Runtime, Console and Paper have independent lifecycles under the [repository topology decision](adr/2026-10-14-repository-topology.md).

[Project home](../README.md) · [SDKs](sdks.md) · [Release history](../CHANGELOG.md)
