English | [中文版](./README.cn.md)

# NPS Change Requests (CRs)

A **Change Request (CR)** is a lightweight design artifact used during the
**pre-1.0** phase of the NPS suite to record the intent, motivation, and
shape of a specification or implementation change before (or alongside)
the code that lands it.

## CR vs RFC

| Aspect | CR (this directory) | RFC ([../rfcs/](../rfcs/)) |
|---|---|---|
| Phase | Pre-1.0 (alpha / beta) | Post-1.0 stable |
| Gating | None — implementation may proceed without external acceptance | Required — Accepted on `dev` PR before any implementation |
| Reviewers | Author + (optionally) one independent reviewer | Shepherd + community discussion window |
| Numbering | `NPS-CR-NNNN` | `NPS-RFC-NNNN` |
| Lifecycle | Draft → Implemented (or Withdrawn) | Draft → Proposed → Accepted → Activated → (Superseded) |
| Status retention | Kept as historical record after implementation | Permanent record; supersession via new RFC |
| Scope | Single coherent change, often spanning spec + impl + tests | Same |

CRs exist because, while the suite is still 0.x and has no production
deployments to protect, the friction of the full RFC process (shepherd
assignment, formal discussion window, accepted-on-merge ceremony) buys
little. Once the suite reaches v1.0, all spec-affecting changes
**MUST** instead be drafted as RFCs under [../rfcs/](../rfcs/) and
follow the process documented there.

## How a CR is structured

A CR document covers, at minimum:

1. **Summary** — one paragraph stating what changes.
2. **Motivation** — why this change is needed now.
3. **Specification changes** — exact text or diff for `spec/` files.
4. **SDK changes** — language-by-language impact.
5. **Conformance changes** — added or modified test items.
6. **Migration impact** — internal and external users.
7. **Out of scope** — explicit non-changes to prevent scope creep.
8. **Acceptance criteria** — concrete completion checklist.
9. **CHANGELOG entry** — proposed text.
10. **Open questions** — items the author wants reviewers to weigh in on.

The structure mirrors a formal RFC closely enough that a CR can be
promoted to an RFC verbatim if it ever needs to be (e.g. if a CR drafted
late in 0.x is still pending when v1.0 is cut).

## Index

| CR | Title | Target | Status |
|---|---|---|---|
| [NPS-CR-0017](./NPS-CR-0017-schema-batches.md) | Bounded schema batches and diff recovery | v1.0.0-alpha.20 | Draft |
| [NPS-CR-0016](./NPS-CR-0016-data-results.md) | Version-bound data result resources | v1.0.0-alpha.20 | Draft |
| [NPS-CR-0015](./NPS-CR-0015-governed-challenge.md) | Governed action challenge | v1.0.0-alpha.20 | Draft |
| [NPS-CR-0014](./NPS-CR-0014-production-runtime.md) | Bounded production runtime | v1.0.0-alpha.20 | Draft |
| [NPS-CR-0013](./NPS-CR-0013-invocation-context.md) | Authenticated invocation context | v1.0.0-alpha.20 | Draft |
| [NPS-CR-0012](./NPS-CR-0012-feature-negotiation.md) | Feature revision negotiation in native handshake and HTTP admission | v1.0.0-alpha.20 | Draft |
| [NPS-CR-0001](./NPS-CR-0001-anchor-bridge-split.md) | Split Gateway Node into Anchor Node + Bridge Node | v1.0-alpha.3 | Implemented (2026-04-26) |
| [NPS-CR-0002](./NPS-CR-0002-anchor-topology-queries.md) | Standard topology query types for Anchor Node (`topology.snapshot` / `topology.stream`) | v1.0-alpha.4 | Implemented (2026-04-27) |
| [NPS-CR-0003](./NPS-CR-0003-orchestrator-group-session-nids.md) | Orchestrator group NIDs and short-lived session NIDs | v1.0-alpha.6 | Implemented (2026-05-28) |
| [NPS-CR-0004](./NPS-CR-0004-pen-wirein.md) | Wire in IANA-assigned PEN 65715 (replace provisional `1.3.6.1.4.1.99999`) | v1.0-alpha.6 | Implemented (2026-05-08) |
| [NPS-CR-0005](./NPS-CR-0005-nip-ca-ra-model.md) | NIP CA Registration Authority (RA) model — three-tier admission (allowlist / bootstrap token / pending queue) | v1.0-alpha.7 | Implemented |
| [NPS-CR-0006](./NPS-CR-0006-subscribe-frame.md) | SubscribeFrame (NWP §13) formal specification — field table, lifecycle, cursor resume; frame-registry promotion `proposed → stable` | v1.0.0-alpha.11 | Implemented (2026-05-28) |
| [NPS-CR-0007](./NPS-CR-0007-nop-l3-runtime-integration.md) | NOP ↔ Node Profile L3 runtime integration | v1.0.0-alpha.13 | Implemented (2026-06-13) |
| [NPS-CR-0008](./NPS-CR-0008-tier3-binary-vector.md) | Tier-3 BinaryVector v1 encoding | v1.0-alpha.14 | Implemented (2026-06-26) |
| [NPS-CR-0009](./NPS-CR-0009-multi-anchor-ha.md) | Multi-Anchor high availability (`anchor_failover` / `anchor_quorum_lost`, `cluster_epoch` fence, NDP resolution) | v1.0.0-alpha.17 | Implemented |
| [NPS-CR-0010](./NPS-CR-0010-bridge-bidirectional.md) | Bridge Node is bidirectional — resolve the outbound-only contradiction, add `bridge_inbound_protocols` direction declaration, split §16 conformance, consolidate `compat/*-ingress` into the Bridge package | v1.0.0-alpha.17 | Implemented |
| [NPS-CR-0011](./NPS-CR-0011-stateful-llm-context.md) | Stateful LLM context and delta completion — owner-bound opaque contexts, CAS versions, atomic commit, lifecycle actions, discovery, and measurable reuse | v1.0.0-alpha.18 | Implemented |

## Authoring a new CR

Latest draft: [NPS-CR-0012: Feature Revision Negotiation](./NPS-CR-0012-feature-negotiation.md)
— target v1.0.0-alpha.20; Draft; discussion NPS-Dev #124.

1. Pick the next available `NNNN` from the index above.
2. Copy the structure of an existing CR (e.g. CR-0001) — there is no
   formal template file; the structure is intentionally light.
3. Write English (`NPS-CR-NNNN-<slug>.md`). A Chinese counterpart is
   **not required** for CRs — they are informal pre-1.0 planning artifacts.
   The downstream `spec/*.md` text that lands the change will carry the
   standard English-primary + Chinese-secondary pair per project convention.
4. Add the row to the Index table above.
5. Open a `feat/cr-NNNN-<slug>` branch and start implementing.
