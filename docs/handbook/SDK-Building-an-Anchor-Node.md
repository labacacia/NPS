# SDK Tutorial: Building an Anchor Node

**Status:** ✅ Reviewed for v1.0.0-alpha.19 release

> **Audience:** Developers standing up a cluster entry point that routes NPS traffic and optionally exposes topology query endpoints.
> **Source-of-truth precedence:** `spec/` documents win over this page if they disagree.

An **Anchor Node** is the stateless-per-request control plane and external entry point for an NPS cluster. It accepts inbound NWP ActionFrame and QueryFrame traffic addressed to the cluster's NID, dispatches work to internal member nodes via NOP TaskFrames, and optionally maintains a registry of those member nodes. This tutorial walks through everything needed to go from zero to a conformant Anchor Node, including the topology query surface mandatory at AaaS Profile L2.

> **Naming note:** Anchor Node was called "Gateway Node" before v1.0-alpha.3. The `node_type: "gateway"` wire value was removed by [NPS-CR-0001](../../spec/cr/NPS-CR-0001-anchor-bridge-split.md). Any code that still emits or parses `"gateway"` will receive `NWP-MANIFEST-NODE-TYPE-REMOVED`.

---

## Table of contents

1. [What an Anchor Node is (and is not)](#what-an-anchor-node-is-and-is-not)
2. [Step 1 — Declare the NWM manifest](#step-1--declare-the-nwm-manifest)
3. [Step 2 — Register ActionSpecs](#step-2--register-actionspecs)
4. [Step 3 — Implement topology queries](#step-3--implement-topology-queries)
5. [Step 4 — Wire the topology:read capability gate](#step-4--wire-the-topologyread-capability-gate)
6. [AaaS compliance levels satisfied](#aaas-compliance-levels-satisfied)
7. [Node-Profile conformance test cases](#node-profile-conformance-test-cases)
8. [Anti-patterns](#anti-patterns)

---

## What an Anchor Node is (and is not)

| It IS | It is NOT |
|-------|-----------|
| The single entry point for a cluster of NWP nodes | A proxy or load balancer |
| Stateless per request (routes ActionFrame → NOP TaskFrame) | A session store |
| Optionally the keeper of a long-lived member registry | A Bridge Node (that is a different role — see [SDK Building a Bridge Node](./SDK-Building-a-Bridge-Node.md)) |
| The mandatory host for `topology.snapshot` / `topology.stream` at L2 | Required to run business logic |

A cluster MUST have at least one Anchor Node. High-availability deployments MAY run multiple; the consensus protocol between them is implementation-defined (deferred to AaaS Profile L3).

An Anchor Node MAY simultaneously declare additional roles (for example `["anchor", "memory"]`) — the NDP `Announce` frame carries `node_roles` as an array.

---

## Step 1 — Declare the NWM manifest

Every NWP node MUST expose a manifest at `GET /.nwm` with `Content-Type: application/nwp-manifest+json`.

As of **NWP v0.14**, the manifest carries `manifest_version` (uint32 monotonic counter, starts at 1, incremented by 1 on every structural change) and `manifest_updated_at` (ISO 8601 timestamp). The server MUST emit an `X-NWM-Version: <uint32>` HTTP response header on every `GET /.nwm` and SHOULD honour `If-None-Match: <uint32>` conditional requests, returning `304 Not Modified` when the caller's version matches the current one.

For an Anchor Node:

- `node_type` MUST be `"anchor"` — NOT `"gateway"` (removed).
- `node_roles` in NDP `Announce` MUST include `"anchor"`.
- `node_type` MUST be a member of `node_roles`. Validators check this cross-protocol constraint.

**Minimal valid Anchor NWM:**

```json
{
  "nwp": "0.14",
  "node_id": "urn:nps:node:api.example.com:agent-service",
  "node_type": "anchor",
  "display_name": "Example AaaS Anchor",
  "manifest_version": 1,
  "manifest_updated_at": "2026-06-13T00:00:00Z",
  "wire_formats": ["ncp-capsule", "msgpack", "json"],
  "preferred_format": "msgpack",
  "capabilities": {
    "query": false,
    "stream_query": false,
    "subscribe": false,
    "token_budget_hint": true
  },
  "auth": {
    "required": true,
    "identity_type": "nip-cert",
    "trusted_issuers": ["https://ca.example.com"]
  },
  "endpoints": {
    "invoke": "nwp://api.example.com/agent-service/invoke",
    "schema": "nwp://api.example.com/agent-service/.schema"
  }
}
```

Add `min_assurance_level` at the top level if you want to require a minimum identity tier for all requests to this node:

```json
"min_assurance_level": "attested"
```

**NDP Announce (must match):**

```json
{
  "frame": "0x30",
  "nid": "urn:nps:node:api.example.com:agent-service",
  "node_roles": ["anchor"],
  "activation_mode": "resident",
  "cluster_anchor": null
}
```

Use `node_roles`, never the legacy `node_kind` field. `node_kind` was an accepted parse-only alias **through alpha.5 only**; from alpha.6 onward clients MUST send `node_roles` (including in `topology.filter.node_roles`) and `node_kind` is no longer accepted.

**Signed AnnounceFrame canonical form (NDP v0.9, normative as of alpha.15):** When your Anchor signs its AnnounceFrame, the signed canonical form is now normative and identical across all six SDKs. The signed body covers **all emitted AnnounceFrame wire fields except** `signature`, `health`, `last_seen`, and the `frame` discriminant. Absent or null optionals are **omitted** (not serialized as `null`). `heartbeat_interval_ms` is signed and canonicalized to the default `60000` **only when absent**; an explicit `0` (heartbeat disabled) is signed literally. This is a **breaking** change: announcements signed by older per-SDK-divergent canonicalisers may fail cross-SDK verification after upgrading.

---

## Step 2 — Register ActionSpecs

The `actions` field in the NWM is the Anchor's service catalog. Every callable operation MUST appear here.

```json
"actions": {
  "analysis.run": {
    "description": "Run a multi-step data analysis pipeline",
    "params_anchor": "sha256:abc123...",
    "result_anchor": "sha256:def456...",
    "async": true,
    "idempotent": false,
    "timeout_ms_default": 60000,
    "timeout_ms_max": 300000,
    "required_capability": "nwp:action",
    "min_assurance_level": "attested"
  },
  "catalog.search": {
    "description": "Search the product catalog",
    "async": false,
    "idempotent": true,
    "timeout_ms_default": 5000,
    "required_capability": "nwp:query",
    "min_assurance_level": "anonymous"
  }
}
```

Key decisions:

- `async: true` actions MUST also implement `system.task.status` and `system.task.cancel` system operations.
- `min_assurance_level` on an ActionSpec overrides the node-wide value for that specific action.
- `idempotent: true` tells callers the action is safe to retry — set this accurately (see [SDK Common Patterns](./SDK-Common-Patterns.md) §Retries).
- Use `cgn_est` (Cognon estimate) in responses. Do NOT use the deprecated `estimated_npt` field — it was renamed in alpha.5.2.

**ActionFrame → TaskFrame dispatch:**

An Anchor Node's core job is to receive an `ActionFrame (0x11)`, validate the caller's NID and scope, and emit a NOP `TaskFrame (0x40)` to the internal orchestration layer:

```
Consumer Agent            Anchor Node              NOP Orchestrator
  │                           │                          │
  │── ActionFrame ──────────→ │                          │
  │   action_id: analysis.run │                          │
  │                           │── verify NID + scope     │
  │                           │── build TaskFrame ─────→ │
  │                           │   (DAG, context, budget) │
  │                           │                          │── DelegateFrame → Workers
  │                           │  ←── AlignStream(result) │
  │ ←── CapsFrame(result) ─── │                          │
```

---

## Step 3 — Implement topology queries

If your Anchor Node maintains a member registry (member nodes register by sending NDP Announce frames with `cluster_anchor` pointing to your NID), you MUST expose two reserved query types. This is **mandatory at AaaS Profile L2** (requirement L2-08).

### topology.snapshot

A single-shot query returning the current cluster state. Received as a `QueryFrame (0x10)` with `type = "topology.snapshot"`.

**Request shape (NWP §12.1):**

```json
{
  "frame": "0x10",
  "type": "topology.snapshot",
  "topology": {
    "scope": "cluster",
    "include": ["members", "capabilities"]
  }
}
```

**Response (`CapsFrame 0x04`):**

```json
{
  "frame": "0x04",
  "anchor_ref": "nps:system:topology:snapshot",
  "count": 1,
  "data": [{
    "version": 142,
    "anchor_nid": "urn:nps:node:api.example.com:agent-service",
    "cluster_size": 5,
    "members": [
      {
        "nid": "urn:nps:agent:api.example.com:worker-1",
        "node_roles": ["action"],
        "activation_mode": "resident",
        "joined_at": "2026-04-15T10:00:00Z",
        "last_seen": "2026-05-03T09:00:00Z"
      }
    ],
    "truncated": false
  }]
}
```

**Version counter rules:**

- `version` is a monotonically increasing uint64 incremented on every topology mutation (member join, leave, or update).
- It MUST survive in-process restarts via a rebase operation: emit an `anchor_state` event with `field: "version_rebased"` to all active stream subscribers, and issue a fresh base value. Subscribers treat this as `resync_required`.

### topology.stream

A continuous event feed. Received as a `SubscribeFrame (0x12)` with `type = "topology.stream"`.

As of **NWP v0.13 (CR-0006)** the `SubscribeFrame` is formally specified in NWP §13: it carries a `subscription_id` (UUID v4), a QueryFrame-compatible filter, `heartbeat_interval_ms`, `max_events`, and an opaque `cursor` that the server returns and the client replays for lossless resume after a disconnect.

```json
{
  "frame": "0x12",
  "action": "subscribe",
  "type": "topology.stream",
  "subscription_id": "550e8400-e29b-41d4-a716-446655440000",
  "heartbeat_interval_ms": 30000,
  "max_events": 0,
  "cursor": null,
  "topology": {
    "scope": "cluster",
    "filter": {
      "node_roles": ["action"]
    }
  }
}
```

To resume after a disconnect, re-send the `SubscribeFrame` with the last `cursor` value the server emitted; the server replays from that point without gaps.

Push events as `DiffFrame (0x02)`. The `event_type` field uses topology-specific values:

| `event_type` | Trigger |
|--------------|---------|
| `member_joined` | NDP Announce received with `cluster_anchor` = this Anchor's NID |
| `member_left` | Member offline or TTL expired |
| `member_updated` | Tags, `activation_mode`, or capabilities changed |
| `anchor_state` | Internal Anchor state change (e.g., `version_rebased`) |
| `resync_required` | Subscriber's `since_version` is outside the retention window |

Cancellation: the subscriber sends `SubscribeFrame(action="unsubscribe", subscription_id=...)`.

**Consistency guarantee:** a snapshot at `version: V` combined with all stream events `V+1, V+2, …` yields a consistent live view. Per-event latency is not guaranteed.

---

## Step 4 — Wire the topology:read capability gate

All `topology.*` requests MUST be authenticated before serving. The minimum binding at Phase 1–2 (NWP §12.4, M6 — landed in alpha.5):

**Primary gate — capability check:**

The requesting NID MUST declare `topology:read` in its `IdentFrame.capabilities`. If absent, return `NWP-TOPOLOGY-UNAUTHORIZED` (`NPS-AUTH-FORBIDDEN`). Do NOT return a silent empty response.

For the live `topology.stream` feed specifically, the requester MUST also declare `topology:subscribe`. This capability was SHOULD at NWP v0.12 and became MUST at **NWP v0.13 (CR-0006, §12.4)** for the authorization model around `SubscribeFrame`.

In HTTP mode, the request will carry an `X-NWP-Capabilities` header (derived from the agent's IdentFrame). Check it:

```
// Pseudo-code (server-side middleware)
if request.type in {"topology.snapshot", "topology.stream"}:
    caps = parse_capabilities(request.headers["X-NWP-Capabilities"])
    if "topology:read" not in caps:
        return error(
            code    = "NWP-TOPOLOGY-UNAUTHORIZED",
            status  = "NPS-AUTH-FORBIDDEN"
        )
```

The .NET reference implementation ships `AnchorNodeMiddleware` which provides this check as an ASP.NET Core middleware; wire it before your topology route handlers.

**Defense-in-depth — NDP role cross-check (SHOULD):**

Additionally SHOULD verify that the requester's most recent `AnnounceFrame` (within its NDP TTL) declares `node_roles` containing `"anchor"`. A mismatch SHOULD return `NWP-TOPOLOGY-UNAUTHORIZED` with a `hint`. An absent AnnounceFrame MUST NOT block a requester that has passed the capability gate.

**Phase 3 (future):**

When RFC-0002 stabilizes, Anchors SHOULD additionally verify a CA-attested `id-nps-node-roles` cert extension to close the self-declaration gap. No action required now.

**Error mapping:**

| Situation | Error code | NPS status |
|-----------|-----------|------------|
| `topology:read` capability missing | `NWP-TOPOLOGY-UNAUTHORIZED` | `NPS-AUTH-FORBIDDEN` |
| `topology.scope` value not implemented | `NWP-TOPOLOGY-UNSUPPORTED-SCOPE` | `NPS-CLIENT-BAD-PARAM` |
| `topology.depth` exceeds maximum | `NWP-TOPOLOGY-DEPTH-UNSUPPORTED` | `NPS-CLIENT-BAD-PARAM` |
| Unknown `topology.filter` key | `NWP-TOPOLOGY-FILTER-UNSUPPORTED` | `NPS-CLIENT-BAD-PARAM` |
| Unrecognized `type` value | `NWP-RESERVED-TYPE-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` |

---

## AaaS compliance levels satisfied

| Level | What your Anchor Node needs | Notes |
|-------|---------------------------|-------|
| **L1 Basic** | NWM with `node_type: "anchor"` + NIP auth + action catalog | Satisfies AaaS Profile §4.2 |
| **L2 Standard** | L1 + NOP TaskFrame dispatch + CGN Token Budget + `topology.snapshot` / `topology.stream` + `topology:read` gate | Satisfies AaaS Profile §4.3 including L2-08 |
| **L3 Advanced** | L2 + Vector Proxy Layer + K-of-N fault tolerance + audit log | Satisfies AaaS Profile §4.4 |

An Anchor Node operator claiming AaaS L2 MUST also satisfy **Node-Profile L1** for the daemon hosting the Anchor. An Anchor maintaining an active member registry and claiming AaaS L2-08 MUST also satisfy **Node-Profile L2**.

---

## Node-Profile conformance test cases

**Node-Profile L1 test suite** (`spec/services/conformance/NPS-Node-L1.md`) — 21 test cases, all labeled `TC-N1-*`. Your Anchor must pass these for the host daemon before making any L2 claims.

Key L1 tests relevant to Anchor operators:

| Test ID | Area | What it checks |
|---------|------|---------------|
| TC-N1-NCP-01 | Wire format | Decodes and encodes all L1 frame types including IdentFrame and AnchorFrame |
| TC-N1-NCP-02 | Handshake | Completes Hello + Anchor handshake against a conformant peer |
| TC-N1-NIP-02 | Identity | Signs and verifies IdentFrames on every inbound connection |
| TC-N1-NDP-01 | Discovery | Emits AnnounceFrames with `activation_mode` |
| TC-N1-NWP-01 | Inbox | Maintains per-NID inbox; accepts ActionFrame deliveries |

**Node-Profile L2 requirements** (detailed `TC-N2-*` IDs are tracked for Phase 2 — see `spec/NPS-Roadmap.md`). Headline additions: Tier-2 MsgPack MUST, `resident` activation mode MUST, push delivery MUST, `topology.snapshot` / `topology.stream` MUST for Anchor Nodes with a member registry.

Passing implementations MAY copy the `NPS-NODE-L1-CERTIFIED.md` template to their repository root as a self-attestation.

---

## Anti-patterns

| Anti-pattern | Correct form | Why |
|-------------|--------------|-----|
| `"node_type": "gateway"` | `"node_type": "anchor"` | `"gateway"` was removed by CR-0001; parsers MUST reject it with `NWP-MANIFEST-NODE-TYPE-REMOVED` |
| `"node_roles": ["gateway"]` in NDP | `"node_roles": ["anchor"]` | Wire value removed; returns `NDP-ANNOUNCE-ROLE-REMOVED` |
| `"estimated_npt": 2000` in ActionSpec | `"cgn_est": 2000` | `estimated_npt` was renamed in alpha.5.2; old field is ignored |
| Serving topology queries without capability check | Check `topology:read` in IdentFrame capabilities | Silent empty responses create oracle attacks against cluster membership |
| Returning topology events before the handshake is authorized | Gate on the primary capability check first | Leaks cluster structure to unauthenticated callers |
| Hard-coding `"node_kind"` in publish path | Use `"node_roles"` | `node_kind` is a parse-only alias through alpha.5; stop emitting it |

---

## See also

- [Protocol NWP](./Protocol-NWP.md) — full NWP spec (v0.14) including QueryFrame §6, SubscribeFrame §13, topology §12
- [Operator AaaS Profile](./Operator-AaaS-Profile.md) — full L1/L2/L3 compliance requirements
- [Operator Conformance Certification](./Operator-Conformance-Certification.md) — test runner, self-attestation templates
- [SDK Building a Bridge Node](./SDK-Building-a-Bridge-Node.md) — the sibling node type for NPS↔external translation

---

*Last reviewed at suite version: v1.0.0-alpha.19 release*
