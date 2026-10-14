# Specs Index

**Status:** ✅ Released alpha.19 index, verified 2026-09-06

This page is an index of all NPS protocol specifications, shared reference documents, RFCs, and CRs. It is a navigation aid only.

> **The `spec/` directory in NPS is the normative source of truth.** This handbook page never overrides the spec. If this page and a spec document disagree, the spec document wins.

The canonical spec files are in the `spec/` directory of [`labacacia/NPS`](https://github.com/labacacia/NPS/tree/main/spec) (distribution) and their source in [`labacacia/NPS-Dev`](https://github.com/labacacia/NPS-Dev/tree/main/spec) (authoring). Both are identical for released versions.

For the alpha.19 release boundary and verification record, see
[Alpha.19 Current Status](./Alpha19-Current-Status.md). NPS is the normative
distribution source for the published alpha.19 suite.

## Alpha.19 Release

| Spec | Version | Status | Release focus |
|---|---|---|---|
| NPS-1 NCP | **0.12** | Proposed | Runtime keepalive/closure and QUIC/backpressure policy |
| NPS-2 NWP | **0.22** | Proposed | NWM normalization and renewable subscription metadata |
| NPS-3 NIP | **0.15** | Proposed | Renewal and live-revocation freshness/fail-closed policy |
| NPS-4 NDP | **0.13** | Proposed | Durable sequence/epoch recovery and partition fencing |
| NPS-5 NOP | **0.10** | Proposed | Bounded replay/TTL/eviction and aggregate fault behavior |

The candidate shared registry is `frame-registry.yaml` 0.15 and error codes are
1.10. These versions are authoring state, not a release announcement.

---

## Released Alpha.18 Protocol Specifications

| Spec | Version | Status | Date | What it covers |
|------|---------|--------|------|----------------|
| [NPS-0 Overview](https://github.com/labacacia/NPS/blob/main/spec/NPS-0-Overview.md) | v0.4 | Proposed | 2026-04-19 | Suite architecture overview, design principles, frame namespace, encoding tiers, node types, security overview, versioning policy, relationship to existing protocols |
| [NPS-1 NCP](https://github.com/labacacia/NPS/blob/main/spec/NPS-1-NCP.md) | v0.11 | Proposed | 2026-08-12 | Unified framing; JSON, MessagePack, and negotiated BinaryVector tiers; HTTP/native carriers; Hello/Caps negotiation, keepalive, flow control, unary correlation, failover continuity, and portable native-server interoperability profile |
| [NPS-2 NWP](https://github.com/labacacia/NPS/blob/main/spec/NPS-2-NWP.md) | v0.21 | Proposed | 2026-08-12 | Memory/Action/Complex/Anchor/Bridge semantics; manifests, query/action/subscribe, multi-Anchor HA (CR-0009), bidirectional Bridge adapters (CR-0010), portable node servers, and opt-in stateful LLM context/delta completion (CR-0011) |
| [NPS-3 NIP](https://github.com/labacacia/NPS/blob/main/spec/NPS-3-NIP.md) | v0.14 | Proposed | 2026-08-12 | NIDs, CA hierarchy, signed identity/trust/revocation frames, assurance, role/capability enforcement, portable CA/verification profile, and owner-bound `llm:context` authorization |
| [NPS-4 NDP](https://github.com/labacacia/NPS/blob/main/spec/NPS-4-NDP.md) | v0.12 | Proposed | 2026-08-12 | Announce/resolve/graph discovery, activation and SpawnSpec, federation, `cluster_epoch` HA fencing, directional Bridge discovery, and portable registry conformance |
| [NPS-5 NOP](https://github.com/labacacia/NPS/blob/main/spec/NPS-5-NOP.md) | v0.9 | Proposed | 2026-08-12 | Multi-agent DAG orchestration, delegation, streams/barriers, saga/callback handling, lease-safe L3 runtime integration, HA re-resolution, and portable orchestrator profile |

### Protocol Dependency Graph

```
NCP (NPS-1)
  └── NWP (NPS-2)
        └── NIP (NPS-3)
              └── NDP (NPS-4)

NCP + NWP + NIP ──► NOP (NPS-5)
```

---

## Shared Reference Documents

| Document | Version | What it covers |
|----------|---------|----------------|
| `spec/error-codes.md` | v1.9 | Unified error code registry — all `{PROTOCOL}-{CATEGORY}-{DETAIL}` codes with NPS status code mappings. The authoritative list for all protocol layers. |
| `spec/status-codes.md` | v0.7 | NPS native status code family and HTTP status code mappings. Used in both native mode and HTTP mode responses. |
| `spec/token-budget.md` | v0.7 | Cognon (CGN) budget specification — the standardized token-unit formerly called NPT. Two profiles: CGN-Estimate (sampling/fallback permitted) and CGN-Billing (`verified_tokenizer`, NID-signed records); §4.2 headers `X-NWP-Tokens-Profile` / `X-NWP-Billing-Record` / `X-NWP-Billing-Tokenizer-Tier`. Tokenizer resolution chain: explicit declaration → auto-match → UTF-8/4 fallback. |
| `spec/frame-registry.yaml` | v0.14 | Machine-readable frame type registry. Consumed by CI (`check-source-of-truth.py`). The canonical namespace for all `0x01`–`0xFF` frame type codes. New frame types MUST be registered here before merging. |
| `spec/transport-profile.md` | v0.1 | Native-mode TCP/QUIC transport profile (RFC-0006) — length-prefix framing and the `NcpNativeClient` / `NcpServer` / `NcpSession` reference model. |
| `spec/NPS-Roadmap.md` | v0.8 | Phase 0–4 roadmap and the alpha.18 pre-beta hardening plan. |

---

## RFCs

Source: `spec/rfcs/` in NPS-Dev.

| RFC | Title | Status | Alpha version landed |
|-----|-------|--------|---------------------|
| [RFC-0001](https://github.com/labacacia/NPS-Dev/blob/main/spec/rfcs/NPS-RFC-0001-ncp-connection-preamble.md) | Add NCP connection preamble for native-mode traffic identification | **Active** | alpha.3; six-SDK Phase 2 helper/test evidence current |
| [RFC-0002](https://github.com/labacacia/NPS-Dev/blob/main/spec/rfcs/NPS-RFC-0002-x509-acme-nid-certs.md) | Adopt X.509 + ACME for NID certificates | **Active** | PEN 65715 assigned; six-SDK X.509 + `agent-01` evidence current |
| [RFC-0003](https://github.com/labacacia/NPS-Dev/blob/main/spec/rfcs/NPS-RFC-0003-agent-identity-assurance-levels.md) | Three-tier Agent identity assurance levels for anti-scraping / trust gating | **Active** | Phase 1–2 current; Phase-3 flag day remains future |
| [RFC-0004](https://github.com/labacacia/NPS-Dev/blob/main/spec/rfcs/NPS-RFC-0004-nid-reputation-log.md) | Append-only NID reputation log (Certificate Transparency for Agents) | **Active** | Six-SDK client/proof behavior; .NET + `nps-ledger` reference operator |
| [RFC-0005](https://github.com/labacacia/NPS-Dev/blob/main/spec/rfcs/NPS-RFC-0005-reputation-policy-enforcement.md) | Reputation Policy Enforcement | **Active** | Current allow/process-local/fail-most-restrictive defaults |
| [RFC-0006](https://github.com/labacacia/NPS-Dev/blob/main/spec/rfcs/NPS-RFC-0006-ncp-native-transport.md) | NCP native-mode transport (TCP length-prefix framing) | **Accepted** | Six-SDK source plus candidate daemon evidence; not silently promoted to Active |

See [RFC Process](./RFC-Process.md) for the full lifecycle description and how to open a new RFC.

---

## CRs (Change Requests)

Source: `spec/cr/` in NPS-Dev. CRs are pre-1.0 planning artifacts; after v1.0.0 all spec changes use the RFC process.

| CR | Title | Status | Alpha version landed |
|----|-------|--------|---------------------|
| [CR-0001](https://github.com/labacacia/NPS-Dev/blob/main/spec/cr/NPS-CR-0001-anchor-bridge-split.md) | Split Gateway Node into Anchor Node + Bridge Node | Implemented | alpha.3 (2026-04-26) |
| [CR-0002](https://github.com/labacacia/NPS-Dev/blob/main/spec/cr/NPS-CR-0002-anchor-topology-queries.md) | Standard topology query types for Anchor Node (`topology.snapshot` / `topology.stream`) | Implemented | alpha.4 (2026-04-27) |
| [CR-0003](https://github.com/labacacia/NPS-Dev/blob/main/spec/cr/NPS-CR-0003-orchestrator-group-session-nids.md) | Group / session NIDs (`group-` / `session-` prefixes, `IdentFrame.lineage`) | Implemented | NIP v0.8 |
| [CR-0004](https://github.com/labacacia/NPS-Dev/blob/main/spec/cr/NPS-CR-0004-pen-wirein.md) | IANA PEN 65715 OID wire-in (replaces provisional `…99999` arc) | Implemented | NIP v0.8 (PEN assigned 2026-05-08) |
| [CR-0005](https://github.com/labacacia/NPS-Dev/blob/main/spec/cr/NPS-CR-0005-nip-ca-ra-model.md) | NIP-CA registration-authority model (bootstrap tokens, pending registrations) | Implemented | NIP v0.8 |
| [CR-0006](https://github.com/labacacia/NPS-Dev/blob/main/spec/cr/NPS-CR-0006-subscribe-frame.md) | SubscribeFrame formal specification (NWP §13) | Implemented | NWP v0.13; six-SDK wire/cursor evidence current |
| [CR-0007](https://github.com/labacacia/NPS-Dev/blob/main/spec/cr/NPS-CR-0007-nop-l3-runtime-integration.md) | NOP L3 runtime integration (nps-runner lease) | Implemented | NOP v0.7 |
| [CR-0008](https://github.com/labacacia/NPS-Dev/blob/main/spec/cr/NPS-CR-0008-tier3-binary-vector.md) | Tier-3 BinaryVector v1 encoding (`binary_vector.v1`) | Implemented | NCP v0.9 |
| [CR-0009](https://github.com/labacacia/NPS-Dev/blob/main/spec/cr/NPS-CR-0009-multi-anchor-ha.md) | Multi-Anchor HA, leadership epochs, and stale-leader fencing | Implemented | alpha.17 / NWP v0.18 |
| [CR-0010](https://github.com/labacacia/NPS-Dev/blob/main/spec/cr/NPS-CR-0010-bridge-bidirectional.md) | Bidirectional Bridge Node profiles and inbound protocol discovery | Implemented | alpha.17 / NWP v0.19 |
| [CR-0011](https://github.com/labacacia/NPS-Dev/blob/main/spec/cr/NPS-CR-0011-stateful-llm-context.md) | Stateful LLM context and delta completion | Implemented | alpha.18 / NWP v0.21 |

See [CR Process](./CR-Process.md) for the full description and authoring guide.

---

## Services Layer Specs

| Document | Version | What it covers |
|----------|---------|----------------|
| `spec/services/NPS-AaaS-Profile.md` | v0.7 | Agent-as-a-Service compliance profile (service side). Defines Anchor Node + Bridge Node compliance levels (L1/L2/L3), Vector Proxy Layer, capability gating. NPS-CR-0001 split the original Gateway Node here. |
| `spec/services/NPS-Node-Profile.md` | v0.2 candidate | Node-side compliance spec. Defines L1/L2/L3 compliance levels and `activation_mode` values (ephemeral / resident / hybrid). Orthogonal to AaaS Profile. |

### Conformance Test Suites

| Document | What it covers |
|----------|----------------|
| `spec/services/conformance/NPS-Node-L1.md` | L1 conformance test suite v0.1 — **20** `TC-N1-*` case headings using paired-peer methodology |
| `spec/services/conformance/NPS-Node-L2.md` | L2 conformance test suite v0.7 — **38** case headings; catalog/component evidence is not deployment certification |
| `spec/services/conformance/NPS-NODE-L1-CERTIFIED.md` | L1 self-declaration template |

---

## Related Pages

- [RFC Process](./RFC-Process.md) — how to open and shepherd an RFC
- [CR Process](./CR-Process.md) — lightweight pre-1.0 change process

---

> Last reviewed at suite version: v1.0.0-alpha.19
>
> Alpha.19 source status reconciled through NPS-Dev PR #115 on 2026-09-05.
