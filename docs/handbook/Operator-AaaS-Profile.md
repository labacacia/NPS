# Operator: AaaS Profile (L1 / L2 / L3)

> **Audience:** Operators (especially AaaS providers — Agent-as-a-Service vendors)
> **Status:** ✅ Reviewed for v1.0.0-alpha.19
> **Source-of-truth precedence:** `spec/` documents in [`labacacia/NPS`](https://github.com/labacacia/NPS/tree/main/spec) win over this page if they disagree.

The **NPS-AaaS Profile** (Agent-as-a-Service Compliance Specification) defines what a *service* must expose to be considered a conformant NPS AaaS provider. It answers: "Does my service present the right NPS endpoints to AI agents?" The companion **Node Profile** answers a separate question: "Is my host a conformant participant in the NPS network?" The two are orthogonal — see [Operator Conformance Certification](./Operator-Conformance-Certification.md) for the relationship.

**Spec**: `spec/services/NPS-AaaS-Profile.md` (v0.7, Status: Proposed)
**Depends-On**: NCP v0.7, NWP v0.13, NIP v0.9, NDP v0.8, NOP v0.6, token-budget v0.5

---

## What is Agent-as-a-Service (AaaS)?

AaaS is a cloud service model in which providers expose AI Agent capabilities through standardized NPS endpoints to consumers — other Agents or human applications — without requiring consumers to understand internal implementation details.

Without AaaS, each platform exposes an incompatible API and agents must carry per-platform adapters. NPS-AaaS solves this with:

| Problem | NPS-AaaS solution |
|---------|------------------|
| Incompatible APIs across Agent platforms | Unified **Anchor Node** entry point + **Bridge Node** (NPS ↔ external) + NWP standard frame protocol |
| Non-standard, unobservable internal orchestration | NOP DAG orchestration + OpenTelemetry tracing |
| High token overhead for AI accessing traditional DBs | Vector Proxy Layer vectorization middleware |
| No Agent identity / permission standard | NIP NID identity + scope delegation chain |
| No service quality guarantees | CGN-Estimate Token Budget + back-pressure control (CGN-Billing for commercial settlement) |

---

## Architecture overview

```
Consumer Agent
      │
      ▼
┌─────────────────────────────────┐
│  Anchor Node (NWP type)         │  ← Service entry, external API
│  • Authentication (NIP)         │
│  • Routing / Rate limit / CGN   │
│  • Service catalog (NWM)        │
└──────────┬──────────────────────┘
           │ DelegateFrame (NOP)
           ▼
┌─────────────────────────────────┐
│  Orchestration Layer (NOP)      │  ← Business coordination
│  • DAG task decomposition       │
│  • K-of-N sync / preflight      │
│  • Retry / timeout / cancel     │
└──────┬────────────┬─────────────┘
       │            │
       ▼            ▼
┌────────────┐ ┌────────────────────┐
│ Action Node│ │ Memory Node        │
│ (Worker)   │ │ + Vector Proxy     │
└────────────┘ └────────────────────┘
```

**Anchor Node** is the cluster front door: stateless per-request, routes inbound NWP `ActionFrame`s into NOP `TaskFrame`s. It optionally maintains a member-node registry for topology queries (mandatory at L2).

**Bridge Node** (introduced by NPS-CR-0001) is the outbound edge: translates NPS frames to non-NPS protocols (HTTP, gRPC, MCP, A2A). Direction: NPS → external. The `compat/*-ingress` packages went the other direction (external → NPS); these — `LabAcacia.McpIngress` / `LabAcacia.A2aIngress` / `LabAcacia.GrpcIngress` — were last published at alpha.16 (2026-07-23; a prepared alpha.17 deprecation release was never published) and left the suite train at alpha.18. Their maintained replacement is the inbound surface of `LabAcacia.NPS.NWP.Bridge`.

---

## Compliance Levels

The AaaS Profile defines three tiers:

| Level | Name | One-line summary |
|-------|------|-----------------|
| **L1** | Basic | Anchor Node + NIP auth + NWM service catalog |
| **L2** | Standard | L1 + NOP orchestration + OTel tracing + CGN-Estimate Token Budget + topology queries + reputation policy |
| **L3** | Advanced | L2 + Vector Proxy Layer + K-of-N fault tolerance + audit log + CGN-Billing settlement records |

---

## Level 1 — Basic Compliance

Level 1 is the minimum viable NPS surface. An L1 service can be discovered, authenticated, and invoked by any NPS-capable agent.

| Req ID | Requirement | Protocol |
|--------|-------------|---------|
| L1-01 | MUST deploy an Anchor Node as the sole service entry point | NWP |
| L1-02 | MUST verify consumer NID via NIP on every inbound connection | NIP |
| L1-03 | MUST publish a NWM manifest listing all available Actions | NWP |
| L1-04 | MUST support the NPS unified port `17433` | NCP |
| L1-05 | MUST return NPS standard status codes and error frames | NCP |
| L1-06 | SHOULD support both HTTP mode and native mode dual transport | NCP |

**Required spec versions at L1**: NCP v0.7, NWP v0.13, NIP v0.9, NDP v0.8.

**NWM manifest minimum shape**:

```json
{
  "nwm_version": "0.10",
  "node_type": "anchor",
  "node_id": "nwp://api.example.com/agent-service",
  "capabilities": ["nwp:invoke", "nip:delegate"],
  "actions": [ ... ],
  "auth": { "required": true, "min_nip_version": "0.4", "required_scopes": ["agent:invoke"] }
}
```

> The `node_type` value MUST be `"anchor"`. The legacy value `"gateway"` was removed by NPS-CR-0001 and parsers MUST reject it.

---

## Level 2 — Standard Compliance

Level 2 adds full NOP orchestration, observability, topology queries, and a reputation policy. L2 is the recommended minimum for production AaaS deployments.

| Req ID | Requirement | Protocol |
|--------|-------------|---------|
| L2-01 | MUST use NOP TaskFrame for internal task orchestration | NOP |
| L2-02 | MUST inject OpenTelemetry trace in `TaskFrame.context` | NOP |
| L2-03 | MUST support CGN-Estimate Token Budget with `token_est` in responses (budget / quota / telemetry surface only — not commercial settlement) | CGN-Estimate |
| L2-04 | MUST support NOP preflight mechanism | NOP |
| L2-05 | MUST implement NOP retry and timeout semantics | NOP |
| L2-06 | SHOULD support async Actions (`ActionFrame.async=true`) | NWP |
| L2-07 | SHOULD implement AlignStream back-pressure control | NOP |
| L2-08 | MUST implement `topology.snapshot` and `topology.stream` reserved query types on Anchor Nodes that maintain a member registry (NPS-2 §12). Version counter MUST be monotonic. | NWP §12 |
| L2-09 | SHOULD configure a `reputation_policy` in the NWM and consult at least one NPS-RFC-0004-compliant log operator on agent admission | NPS-RFC-0004 |

### L2-08: topology queries and `topology:read`

L2-08 requires that an Anchor Node maintaining a member registry respond to the `topology.snapshot` and `topology.stream` reserved NWP query types. Since alpha.5, the `AnchorNodeMiddleware` enforces a **`topology:read` capability gate** on these queries: the consumer's NIP scope must include `topology:read` for the request to proceed. This is a per-request authorization check, not a manifest-level flag.

Implications for L2-08 operators:
- Ensure your Anchor Node is backed by an `nps-registry` instance (or equivalent member store).
- Issue consumer NIDs with `topology:read` in their scope when you want to grant topology visibility.
- Claiming L2-08 means the host running the Anchor MUST satisfy **Node-Profile L1**. If the Anchor maintains an active member registry, it SHOULD also satisfy Node-Profile L2. See [Operator Conformance Certification](./Operator-Conformance-Certification.md).

### L2-09: reputation policy in NWM

The recommended minimum policy for L2 AaaS deployments is:

```yaml
reputation_policy:
  required_logs: ["log:labacacia-primary"]
  reject_on:
    - { incident: "cert-revoked", severity: ">=minor" }
    - { incident: "rate-limit-violation", severity: ">=major", within_days: 30 }
    - { incident: "tos-violation", severity: ">=major", within_days: 30 }
```

Publish this under the `reputation_policy` key in your NWM. See [Operator Reputation Log](./Operator-Reputation-Log.md) for operating a reputation log or connecting to an existing one.

---

## Level 3 — Advanced Compliance

Level 3 adds vectorized data access, K-of-N fault tolerance, audit logging, and — as of
spec v0.7 — **CGN-Billing** commercial-settlement requirements. Headline requirements:

| Req ID | Requirement | Protocol |
|--------|-------------|---------|
| L3-01 | MUST deploy a Vector Proxy Layer for vectorized queries | NWP |
| L3-02 | MUST support NWP `vector_search` interface (NWP §6.4) | NWP §6.4 |
| L3-03 | MUST implement K-of-N sync fault tolerance (`SyncFrame.min_required`) | NOP |
| L3-04 | MUST maintain audit logs (NOP §8.3) | NOP |
| L3-05 | MUST implement scope delegation chain security (max 3 levels) | NIP + NOP |
| L3-06 | SHOULD support automatic schema discovery (DB schema → AnchorFrame) | NWP |
| L3-07 | SHOULD support hot vector index updates (incremental rebuild on data changes) | Vector Proxy |
| L3-08 | MUST emit **CGN-Billing** records (not generic CGN / CGN-Estimate) for any commercial settlement flow, per token-budget §2.1 / §6.3. Implies `verified_tokenizer`-tier resolution (NIP §5.1), NID-signed metering records, no sampling, no byte-size fallback, and audit-log integration sufficient for dispute / chargeback. The `X-NWP-Tokens-Profile`, `X-NWP-Billing-Record`, and `X-NWP-Billing-Tokenizer-Tier` response headers MUST appear on every billed response. | CGN-Billing + NIP + NPS-RFC-0004 |

> **CGN-Estimate vs CGN-Billing (token-budget v0.5).** CGN split into two profiles:
> **CGN-Estimate** covers budgets / quota / telemetry (sampling and byte-size fallback
> permitted, ±5 % drift, no signing) and is what L2-03 requires; **CGN-Billing** covers
> commercial settlement and is what L3-08 requires (verified tokenizer, NID-signed records,
> no sampling/fallback, audit-log integration, version-pinned exchange-rate table). Absent
> the `X-NWP-Tokens-Profile` header, a response defaults to CGN-Estimate.

**Required spec versions at L3**: same as L2, plus NOP v0.6 (K-of-N sync) and token-budget v0.5 (CGN-Billing).

---

## How to self-certify

1. Implement and test against the AaaS compliance level you are claiming.
2. For Node-Profile certification (required at L2-08), run the conformance test suite and fill out the `NPS-NODE-L1-CERTIFIED.md` template.
3. Publish the filled-in template at your repository root.
4. Reference your certification in your NWM under a `conformance` key (convention, not yet normative).

Full certification guidance is in [Operator Conformance Certification](./Operator-Conformance-Certification.md).

---

## Required spec versions per level (summary)

| Spec | L1 | L2 | L3 |
|------|----|----|----|
| NCP | v0.7 | v0.7 | v0.7 |
| NWP | v0.13 | v0.13 | v0.13 |
| NIP | v0.9 | v0.9 | v0.9 |
| NDP | v0.8 | v0.8 | v0.8 |
| NOP | — | v0.6 | v0.6 |
| token-budget | — | v0.5 (CGN-Estimate) | v0.5 (CGN-Estimate + CGN-Billing) |
| NPS-RFC-0004 | — | Phase 3 (STH gossip) | Phase 3 |

> These are the spec versions the **AaaS Profile v0.7** depends on (its `Depends-On`
> line). The suite as a whole is at v1.0.0-alpha.19; individual protocol specs have
> advanced further (e.g. NCP v0.11, NWP v0.21, NIP v0.14, NDP v0.12, NOP v0.9) — the AaaS
> requirements are pinned to the versions above.

---

## See also

- [Operator Conformance Certification](./Operator-Conformance-Certification.md) — how to run the conformance test suite and publish an attestation
- [Protocol NWP](./Protocol-NWP.md) — NWP spec, including the Anchor Node semantics and topology query types (§12)
- [Operator Reputation Log](./Operator-Reputation-Log.md) — operating or consuming a reputation log

---

*Last reviewed at suite version: v1.0.0-alpha.19*

