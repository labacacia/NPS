English | [中文版](./NPS-4-NDP.cn.md)

# NPS-4: Neural Discovery Protocol (NDP)

**Spec Number**: NPS-4
**Status**: Proposed
**Version**: 0.14
**Date**: 2026-09-09
**Port**: 17433 (default, shared) / 17436 (optional dedicated)
**Authors**: Ori Lynn / INNO LOTUS PTY LTD
**Depends-On**: NPS-1 (NCP v0.13), NPS-3 (NIP v0.16)

---

## 1. Terminology

The key words "MUST", "SHOULD", and "MAY" in this document are to be interpreted as described in RFC 2119.

---

## 2. Protocol Overview

NDP is the NPS discovery and registry protocol. Agents, Nodes, and Registries use NDP to publish signed presence records, resolve `nwp://` targets to live physical endpoints, exchange bounded topology snapshots, and apply liveness / registry-profile policy. DNS TXT can carry NDP discovery hints, but NDP is not a replacement for DNS: it binds NPS identities, capabilities, roles, endpoints, freshness, and trust policy into a signed discovery record.

### 2.1 Resolution Modes

| Mode | Use case | Priority |
|------|----------|----------|
| Local registry (UDP multicast) | Intranet / LAN | Highest |
| DNS TXT records | Public internet, distributed | Medium |
| NPS Cloud Registry | Global, centralized | Lowest (fallback) |

---

## 3. Frame Types

### 3.1 AnnounceFrame (0x30)

A node or agent broadcasts its presence and capabilities.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `frame` | uint8 | required | Fixed value `0x30` |
| `nid` | string | required | Publisher NID |
| `node_type` | string | conditional | Node type (required for node entities) |
| `addresses` | array | required | Physical address list; each entry carries `host` / `port` / `protocol` |
| `capabilities` | array | required | Capability list (reuses the NIP capability vocabulary) |
| `ttl` | uint32 | required | Broadcast validity in seconds; `0` = offline notification |
| `timestamp` | string | required | Broadcast time (ISO 8601 UTC) |
| `graph_seq` | uint64 | conditional | Per-NID monotonic configuration sequence. NDP 0.12 publishers MUST emit it. For compatibility, a `local-dev` Registry MAY treat an absent value from an older publisher as `0`; `org-private` and `public-federated` Registries MUST reject an absent value with `NDP-ANNOUNCE-PROFILE-VIOLATION`. |
| `activation_mode` | string | conditional | One of `ephemeral` / `resident` / `hybrid`. REQUIRED for publishers claiming NPS-Node Profile L1+ compliance. OPTIONAL for pre-L1 publishers. Receivers MUST treat an absent field as `ephemeral` (backward compatibility with NPS v1.0-alpha.2 publishers). See §3.1.1. |
| `node_roles` | array of strings | optional | Node-functionality role(s) carried by this publisher. Each value is one of `"memory"`, `"action"`, `"complex"`, `"anchor"`, `"bridge"`. The legacy value `"gateway"` was removed in v1.0-alpha.3 (NPS-CR-0001); parsers MUST reject it with `NDP-ANNOUNCE-ROLE-REMOVED`. Any other unrecognized value MUST be rejected with `NDP-ANNOUNCE-ROLE-UNKNOWN`. Single-role nodes may send a one-element array (`"node_roles": ["memory"]`). Absent means "single role per `node_type`" — i.e. the receiver SHOULD fall back to the `node_type` field. Parsers MUST also accept the legacy field name `node_kind` as an alias for `node_roles` during the alpha transition window. (NPS-CR-0001; renamed from `node_kind` in NDP v0.8 — see M1 naming fix) |
| `cluster_anchor` | string (NID) | optional | For non-Anchor nodes joining a cluster, identifies the Anchor Node they register with. Absent for standalone nodes and for Anchor Nodes themselves. (NPS-CR-0001) |
| `bridge_protocols` | array of strings | optional | For nodes declaring `"bridge"` in `node_roles`, lists the external protocols this node translates **outbound** (NPS → external). Standard values: `"http"`, `"grpc"`, `"mcp"`, `"a2a"`. Open-ended; third-party adapters MAY register additional values via future CRs. MUST be absent for nodes that do not declare `"bridge"`. Semantics unchanged by NPS-CR-0010. (NPS-CR-0001) |
| `bridge_inbound_protocols` | array of strings | optional | For nodes declaring `"bridge"` in `node_roles`, lists the external protocols this node **serves inbound** (external → NPS) — i.e. protocols for which it exposes a native server endpoint that foreign clients can call without any NPS knowledge. Same value domain as `bridge_protocols`. Absent or empty ⇒ the node exposes no inbound surface (an outbound-only Bridge Node, the only kind that existed through alpha.15). A protocol MAY appear in both arrays. A node declaring `"bridge"` MUST have at least one of `bridge_protocols` / `bridge_inbound_protocols` non-empty. MUST be absent for nodes that do not declare `"bridge"`. (NPS-CR-0010) |
| `activation_endpoint` | object | conditional | Push target for `resident` / `hybrid` publishers; same shape as an `addresses[]` entry. REQUIRED when `activation_mode ∈ {resident, hybrid}`; MUST be absent otherwise. |
| `heartbeat_interval_ms` | uint32 | optional | How often this node will re-announce itself (milliseconds); default 60000. Receivers SHOULD treat the node as offline if no AnnounceFrame is received within 3× this interval. |
| `spawn_spec_ref` | string | optional | Opaque reference the publishing daemon resolves to a **SpawnSpec** to construct an agent process on demand. Meaningful for `ephemeral` and `hybrid` cold start. The resolved SpawnSpec schema is defined in §3.1.2; the resolution rules (inline `spawnspec:` base64url-JSON data URI or an `https://`/`nwp://` URL) are standardized by [NPS-CR-0007](cr/NPS-CR-0007-nop-l3-runtime-integration.md) §5 (NPS-Node Profile L3). |
| `health` | string | optional | Publisher liveness self-report (NDP v0.9). One of `"healthy"` / `"degraded"` / `"draining"`. Absent ⇒ receivers treat as `"healthy"` (backward compatible). `"draining"` signals the node is shutting down and SHOULD NOT receive new traffic. |
| `last_seen` | string | optional | ISO 8601 UTC timestamp of the publisher's most recent liveness beat (NDP v0.9). When present, a Registry uses `last_seen + ttl` (rather than `timestamp + ttl`) as the freshness deadline for resolve-time staleness (§3.2). Absent ⇒ fall back to `timestamp`. |
| `cluster_epoch` | uint64 | optional | Multi-Anchor cluster ownership fence (NPS-CR-0009). The epoch under which this Anchor holds ownership of its `cluster_anchor` cluster; starts at 1, strictly increases on each ownership transfer. Absent ⇒ treated as 1 (single-Anchor). Registries resolve a `cluster_anchor` NID to the entry with the highest `cluster_epoch` (§9). |
| `signature` | string | required | Signature with IdentFrame private key; prevents forgery |

**Example (L1+ publisher, ephemeral)**

```json
{
  "frame": "0x30",
  "nid": "urn:nps:node:api.example.com:products",
  "node_type": "memory",
  "addresses": [
    { "host": "10.0.0.5", "port": 17434, "protocol": "nwp" }
  ],
  "capabilities": ["nwp:query", "nwp:stream"],
  "ttl": 300,
  "timestamp": "2026-04-10T00:00:00Z",
  "activation_mode": "ephemeral",
  "signature": "ed25519:..."
}
```

**Example (L2+ publisher, resident)**

```json
{
  "frame": "0x30",
  "nid": "urn:nps:agent:labacacia:writer-42",
  "addresses": [
    { "host": "10.0.0.5", "port": 17434, "protocol": "nwp" }
  ],
  "capabilities": ["nwp:invoke"],
  "ttl": 300,
  "timestamp": "2026-04-24T00:00:00Z",
  "activation_mode": "resident",
  "activation_endpoint": { "host": "10.0.0.5", "port": 17440, "protocol": "nwp" },
  "signature": "ed25519:..."
}
```

#### 3.1.1 Activation semantics

`activation_mode` tells receivers whether the publisher expects outbound traffic by push or by pull:

| Mode | Sender behavior | Receiver expectation |
|------|-----------------|----------------------|
| `ephemeral` | Deliver via inbox + pull; publisher may not be running when the frame arrives. | Frame is queued in the publisher's per-NID inbox. |
| `resident` | Push over a long-lived connection to `activation_endpoint`. | Publisher accepts pushed frames on the declared endpoint. |
| `hybrid` | Attempt push to `activation_endpoint` first; fall back to inbox if the endpoint is unreachable within the publisher's wake budget. | Publisher wakes from hibernation on first frame; push target resumes once awake. |

**Backward compatibility**: NPS v1.0-alpha.2 publishers do not emit `activation_mode`. Receivers MUST handle an absent field as `ephemeral` so that alpha.2 AnnounceFrames continue to resolve correctly. A receiver MUST NOT reject an AnnounceFrame solely for lacking the field.

**Conformance reference**: See [NPS-Node Profile §1.3 and §6](./services/NPS-Node-Profile.md) for the compliance framing and per-level requirements around these fields.

#### 3.1.2 SpawnSpec Schema (resolved form of `spawn_spec_ref`)

Schema of the **SpawnSpec** that `spawn_spec_ref` resolves to (§3.1) — describes how an ephemeral Agent node is instantiated on demand (NDP v0.9, Profile L3). The resolution rules (data-URI vs URL) are standardized by [NPS-CR-0007](cr/NPS-CR-0007-nop-l3-runtime-integration.md) §5.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `oci_image` | string | required | OCI container image reference, e.g. `"registry.example.com/my-agent:v1.2"` |
| `command` | array[string] | optional | Command override (Docker `CMD` equivalent) |
| `resource_limits` | object | optional | Resource constraints |

**resource_limits object**

| Field | Type | Description |
|-------|------|-------------|
| `cpu_millicores` | uint32 | CPU limit in millicores (e.g. `500` = 0.5 vCPU) |
| `memory_mb` | uint32 | Memory limit in mebibytes |

Only relevant for nodes participating in Profile L3 (spawn-capable registries). Nodes at Profile L1/L2 MAY include this field; receivers at L1/L2 SHOULD ignore it.

---

### 3.2 ResolveFrame (0x31)

Resolves an `nwp://` URL to a physical endpoint.

**Request**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `frame` | uint8 | required | Fixed value `0x31` |
| `target` | string | required | The `nwp://` URL to resolve |
| `requester_nid` | string | optional | Requester NID (for authorization checks) |

**Response**

```json
{
  "target": "nwp://api.example.com/products",
  "resolved": {
    "host": "10.0.0.5",
    "port": 17434,
    "cert_fingerprint": "sha256:a3f9...",
    "ttl": 300,
    "health": "healthy"
  }
}
```

#### 3.2.1 Resolve-time staleness (NDP v0.9)

A Registry MUST compute a freshness deadline for each registered entry as
`(last_seen ?? timestamp) + ttl`. When a `ResolveFrame` would return an entry whose deadline is
already in the past, the Registry MUST instead return `NDP-RESOLVE-STALE` rather than serve a
stale endpoint (which would route traffic to a node that is very likely gone). A Registry MAY
proactively evict entries past their deadline; until eviction, the staleness check is the
authoritative guard. The resolved object SHOULD echo the entry's `health` so callers can avoid a
`draining` node even while it is still within its TTL.

#### 3.2.2 Bridge capability discovery (NDP v0.12)

A Registry discovery query for a Bridge protocol MUST consider only live entries
that declare `"bridge"` in `node_roles` (or whose `node_type` fallback is
`"bridge"`) and whose `health` is not `"draining"`. An **outbound** query matches
`bridge_protocols`; an **inbound** query matches `bridge_inbound_protocols`.
Registries MUST NOT infer inbound support from an outbound claim or vice versa.

The portable discovery result is the complete candidate NID set sorted in
ascending ordinal order. NDP does not choose one candidate or define load
balancing; that decision belongs to the NWP Bridge client after it applies
endpoint, trust, and runtime policy.

---

### 3.3 GraphFrame (0x32) — Topology Snapshot

Node-graph snapshot and federation format. Carries a full or partial topology snapshot with explicit node and edge lists (NDP v0.8; replaces the prior `initial_sync`/`patch`/`seq` format).

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `frame` | uint8 | required | Fixed value `0x32` |
| `graph_id` | string | required | Opaque identifier for this graph snapshot (UUID v4 or stable registry key) |
| `nodes` | array | required | Array of `NdpGraphNode` objects; maximum 256 |
| `edges` | array | required | Array of `NdpGraphEdge` objects; maximum 1024 |
| `ttl` | uint32 | optional | Seconds this snapshot should be considered fresh; default 60 |
| `metadata` | object | optional | Arbitrary key-value metadata attached to this snapshot |

**NdpGraphNode**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `nid` | string | required | NID of this graph node (for example `urn:nps:node:{domain}:{id}` or `urn:nps:agent:{domain}:{id}`) |
| `cluster_anchor` | string | optional | NID of the cluster anchor this node belongs to |
| `node_roles` | array[string] | optional | Role tags (e.g. `["memory"]` or `["action", "bridge"]`), same vocabulary as AnnounceFrame |

**NdpGraphEdge**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `from_nid` | string | required | Source node NID |
| `to_nid` | string | required | Destination node NID |
| `latency_ms` | uint32 | optional | Observed round-trip latency in milliseconds |
| `protocol` | string | optional | Transport protocol (`"tcp"` / `"quic"` / `"http"`) |

**Validation**
- `nodes.length` MUST NOT exceed 256; violation returns `NDP-GRAPH-TOO-LARGE`.
- `edges.length` MUST NOT exceed 1024; violation returns `NDP-GRAPH-TOO-LARGE`.
- Every `from_nid` and `to_nid` in `edges` MUST appear in `nodes`; violation returns `NDP-GRAPH-INVALID`.
- A node MUST NOT have a self-edge (`from_nid == to_nid`); violation returns `NDP-GRAPH-INVALID`.

---

## 4. Agent Initialization Flow

```
Agent starts
  │
  ├─ 1. NIP: load IdentFrame (from file or fetch from CA)
  │
  ├─ 2. NDP: send AnnounceFrame (publish presence, endpoints, roles, liveness)
  │
  ├─ 3. NDP: resolve peers and receive GraphFrame topology snapshots
  │         ← ResolveFrame response for target endpoint(s)
  │         ← GraphFrame snapshot(s) from Registry / Anchor
  │
  └─ Ready: can initiate NWP requests
```

---

## 5. DNS TXT Record Specification

A node MAY publish discovery information via DNS TXT records without requiring a central registry:

```
# Node discovery record
_nps-node.api.example.com.  IN TXT  "v=nps1 type=memory port=17434 nid=urn:nps:node:api.example.com:products fp=sha256:a3f9..."

# CA discovery record
_nps-ca.mycompany.com.      IN TXT  "v=nps1 ca=https://ca.mycompany.com/.well-known/nps-ca"
```

**TXT record keys**

| Key | Required | Description |
|-----|----------|-------------|
| `v` | required | Version; fixed `nps1` |
| `type` | optional | Node type: `memory` / `action` / `complex` |
| `port` | optional | Port (default 17434) |
| `nid` | required | Node NID |
| `fp` | optional | Node certificate fingerprint |
| `ca` | conditional | CA discovery endpoint (required for CA records) |

> **Version-string formats**: The DNS TXT `v` key uses the compact value `nps1` following DNS TXT record conventions (lowercase, no spaces). The NCP native-mode connection preamble uses the RFC-style token `NPS/1.0\n` (see NPS-1 §2.6.1). Both identify NPS protocol version 1, but in different encoding contexts; they are not interchangeable and operate at different protocol layers.

---

## 9. Federation Forwarding

**Multi-Anchor cluster resolution (NPS-CR-0009).** When resolving a `cluster_anchor` NID with more than one live Anchor announcement, a Registry MUST return the announcement with the highest `cluster_epoch`. Two live announcements advertising the **same** `cluster_epoch` for one cluster is a split-brain fault and MUST be reported with `NDP-CLUSTER-SPLIT` (→ `NPS-CLIENT-CONFLICT`) rather than resolved arbitrarily. Federated Registries propagate the `(cluster_anchor, cluster_epoch, active_nid)` tuple; a Registry MUST prefer a higher `cluster_epoch` received from a peer and MUST NOT downgrade a cluster to a lower epoch (monotonic per cluster). See [NPS-CR-0009](cr/NPS-CR-0009-multi-anchor-ha.md).


When a `public-federated` registry receives an AnnounceFrame from a peer registry (forwarded across a federation link), it MUST:

1. Forward the frame to its own subscribers by appending the forwarding NID to the `ndp-forwarded-by` request header (comma-separated list of NIDs).
2. Drop the frame and return `NDP-FEDERATION-LOOP` if its own NID already appears in `ndp-forwarded-by` (loop detection).
3. Drop the frame silently if the hop count (length of the `ndp-forwarded-by` list) exceeds **3** hops.

`local-dev` and `org-private` registries MUST NOT forward AnnounceFrames; they MAY log a warning if a forwarded frame arrives.

**`ndp-forwarded-by` Header Format**

Comma-separated list of NPS NIDs, one entry per hop:
```http
ndp-forwarded-by: urn:nps:agent:registry-a.example.com:r1, urn:nps:agent:registry-b.example.com:r2
```

---

## 6. Error Codes

| Error Code | NPS Status | Description |
|------------|------------|-------------|
| `NDP-RESOLVE-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | `nwp://` address could not be resolved |
| `NDP-RESOLVE-AMBIGUOUS` | `NPS-CLIENT-CONFLICT` | Conflicting resolution results (multiple inconsistent registrations) |
| `NDP-RESOLVE-TIMEOUT` | `NPS-SERVER-TIMEOUT` | Resolution request timed out |
| `NDP-RESOLVE-STALE` | `NPS-CLIENT-NOT-FOUND` | The resolved entry's freshness deadline `(last_seen ?? timestamp) + ttl` is in the past; the registration is stale and MUST NOT be served (NDP v0.9 §3.2.1) |
| `NDP-CLUSTER-SPLIT` | `NPS-CLIENT-CONFLICT` | Two live Anchor announcements for one `cluster_anchor` advertise the same `cluster_epoch`; the cluster is split-brain and MUST NOT be resolved arbitrarily (NDP v0.10 §9, NPS-CR-0009) |
| `NDP-ANNOUNCE-SIGNATURE-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | AnnounceFrame signature verification failed |
| `NDP-ANNOUNCE-NID-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | NID in AnnounceFrame does not match the signing certificate |
| `NDP-ANNOUNCE-ROLE-REMOVED` | `NPS-CLIENT-BAD-FRAME` | `node_roles` contains the removed legacy value `"gateway"` (NPS-CR-0001); response SHOULD include a `hint` pointing to NPS-CR-0001 |
| `NDP-ANNOUNCE-ROLE-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | `node_roles` contains an unrecognized value (see `NDP-ANNOUNCE-ROLE-REMOVED` for the `"gateway"` case specifically) |
| `NDP-ANNOUNCE-CONFLICT` | `NPS-CLIENT-CONFLICT` | Two AnnounceFrames share the same `nid` and `graph_seq` but differ in content (registry poisoning attempt) |
| `NDP-ANNOUNCE-PROFILE-VIOLATION` | `NPS-AUTH-FORBIDDEN` | AnnounceFrame violates an active registry security profile constraint not covered by a more specific NDP error |
| `NDP-GRAPH-SEQ-ROLLBACK` | `NPS-CLIENT-BAD-FRAME` | AnnounceFrame `graph_seq` is lower than the highest value the receiver has accepted for this NID (rollback attempt) |
| `NDP-GRAPH-SEQ-GAP` | `NPS-STREAM-SEQ-GAP` | GraphFrame sequence numbers are not contiguous |
| `NDP-FEDERATION-LOOP` | `NPS-CLIENT-CONFLICT` | Federation forwarding detected a loop via the `ndp-forwarded-by` hop list |
| `NDP-STATE-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | A persistence-required Registry cannot durably read or commit its recovery state |
| `NDP-STATE-CORRUPT` | `NPS-SERVER-INTERNAL` | Persisted Registry recovery state fails schema, checksum, or monotonic-fence validation |
| `NDP-ISSUER-NOT-ALLOWED` | `NPS-AUTH-FORBIDDEN` | AnnounceFrame issuer (signing CA) is not in the active registry profile's issuer allowlist |
| `NDP-CA-ATTEST-REQUIRED` | `NPS-AUTH-UNAUTHENTICATED` | Active registry profile requires a CA-attested NID and the AnnounceFrame's certificate chain does not anchor in the configured trust roots |
| `NDP-REGISTRY-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | NDP Registry temporarily unavailable |
| `NDP-GRAPH-TOO-LARGE` | `NPS-LIMIT-PAYLOAD` | GraphFrame `nodes` > 256 or `edges` > 1024 |
| `NDP-GRAPH-INVALID` | `NPS-CLIENT-BAD-FRAME` | GraphFrame edge references a NID not in the nodes list, or self-edge detected |
| `NDP-ANNOUNCE-STALE` | `NPS-CLIENT-NOT-FOUND` | AnnounceFrame heartbeat has expired (3× `heartbeat_interval_ms` elapsed with no re-announce) |

HTTP-mode status mapping: see [status-codes.md](./status-codes.md).

---

## 7. Security Considerations

### 7.1 Announce anti-forgery
Receivers MUST verify the `signature` on every AnnounceFrame to confirm the publisher holds the private key for the claimed NID; this prevents fake node announcements.

### 7.2 Registry anti-pollution
A centralized registry (NPS Cloud) MUST require a valid IdentFrame from the announcer and verify it through NIP CA before admitting an AnnounceFrame to the registry.

### 7.3 Registry security profiles

Every NDP Registry deployment MUST declare exactly one of three security profiles. The profile is configuration of the Registry, not of the AnnounceFrame: the profile determines which AnnounceFrames the Registry will accept, retain, and serve. Implementations MUST refuse to start without an explicit profile declaration (no implicit default).

| Profile | Issuer allowlist | CA-attested NID | Replay window | Federation |
|---------|------------------|-----------------|---------------|------------|
| `local-dev` | not enforced | not required | `0` (replay defense disabled) | not allowed |
| `org-private` | required (set of CA fingerprints) | SHOULD | `300s` | not allowed |
| `public-federated` | enforced via CA trust chain | MUST | `300s` | allowed (with bilateral trust agreement, §7.6) |

**`local-dev`**: Single-host or single-developer environments only. Registry MUST refuse to start in `local-dev` if it is reachable on any non-loopback interface unless an operator override flag is set; the override MUST be logged on every startup. Deployments MUST NOT use `local-dev` for any traffic-bearing service.

**`org-private`**: A single organization's intranet registry. The operator configures an issuer allowlist as a non-empty set of CA certificate fingerprints; AnnounceFrames whose signing chain does not terminate in an allowlisted CA MUST be rejected with `NDP-ISSUER-NOT-ALLOWED`. CA-attested NIDs (NIDs whose ownership is attested by the org CA, see NPS-3 §6) SHOULD be required; if required and absent, reject with `NDP-CA-ATTEST-REQUIRED`. Cross-registry federation MUST be disabled.

**`public-federated`**: Public-internet registries (e.g. NPS Cloud Registry). CA-attested NID MUST be required: Registry MUST reject any AnnounceFrame whose certificate chain does not anchor in a configured public trust root with `NDP-CA-ATTEST-REQUIRED`. The issuer allowlist is implicit in that trust root set. Cross-registry federation MAY be enabled per §7.6.

### 7.4 AnnounceFrame anti-poisoning

Independent of profile, every NDP Registry MUST enforce the following rules:

1. **Signed scope**. The `signature` field MUST cover the AnnounceFrame canonical body: all emitted AnnounceFrame wire fields except `signature`, `health`, `last_seen`, and any transport/core frame discriminant such as `frame`. Optional fields whose value is absent or `null` MUST be omitted from the canonical body rather than serialized as JSON `null`. `heartbeat_interval_ms` is signed; if absent on the wire, verifiers MUST canonicalize it as the default value `60000`. Receivers MUST verify the signature before any registry-side state mutation; signature verification MUST precede deduplication, conflict detection, and storage.
2. **Replay defense**. Receivers MUST reject an AnnounceFrame whose `timestamp` is more than the profile's replay window in the past or in the future, with `NDP-ANNOUNCE-SIGNATURE-INVALID`. A replay window of `0` (the `local-dev` profile only) disables this check.
3. **Duplicate suppression**. An AnnounceFrame whose `(nid, graph_seq)` and complete wire content exactly match an entry already accepted MUST be silently dropped (no error, no state change). Byte-equal duplicates are normal in multicast environments.
4. **Liveness refresh**. When `(nid, graph_seq)` and the canonical signed body match, but only `health` or `last_seen` changed, the Registry MUST refresh those advisory fields and recompute freshness without treating the frame as a conflict or rollback.
5. **Conflict rejection**. An AnnounceFrame whose `(nid, graph_seq)` matches an existing entry but whose canonical signed body differs MUST be rejected with `NDP-ANNOUNCE-CONFLICT`. Both the rejected frame and the existing entry SHOULD be logged for operator review; conflicting frames are evidence of either a key compromise or a misconfigured publisher cluster.

Canonicalization recursively sorts object keys in ascending Unicode code-point
order, preserves array order, emits no insignificant whitespace, and omits
absent or `null` optional fields. Registries SHOULD compare the SHA-256 digest
of this canonical UTF-8 body rather than retaining a second serialized copy.

The portable admission order is normative:

1. verify signature and NID binding;
2. apply replay-window and registry-profile checks;
3. canonicalize and compare `graph_seq` / covered content;
4. process a valid `ttl = 0` offline announcement;
5. reject an already stale live announcement with `NDP-ANNOUNCE-STALE`;
6. insert, refresh, or replace the entry.

No failed step may mutate registry contents or the per-NID sequence fence.
Before this admission order, Registries MUST validate the §3.1 wire types. In particular, `ttl`
MUST be a present `uint32`; a present `graph_seq` MUST be a `uint64` even where `local-dev` permits
the field to be absent; and each Bridge protocol field, when present, MUST be an array containing
only non-empty strings. A malformed present value is not equivalent to an absent compatibility
value and MUST be rejected with `NDP-ANNOUNCE-PROFILE-VIOLATION`.
For a live announcement, freshness is `(last_seen ?? timestamp) + ttl`; a
Registry MUST NOT extend it from its local receipt time. A valid offline
announcement removes the live entry but retains its accepted sequence fence.

### 7.5 Graph sequence rollback defense

The `graph_seq` field on an AnnounceFrame (and on the corresponding GraphFrame) is a per-NID monotonic counter. Receivers MUST track the highest `graph_seq` they have accepted for each NID. A lower value MUST be rejected with `NDP-GRAPH-SEQ-ROLLBACK`. An equal value is handled by §7.4: exact retransmission is suppressed, an advisory-only liveness update refreshes the entry, and changed covered content is `NDP-ANNOUNCE-CONFLICT`. A higher value replaces the prior entry. This prevents an attacker who captures an old AnnounceFrame from re-publishing it to demote a node's current state. The tracked maximum MUST persist across registry restarts at `org-private` and `public-federated` profile levels; `local-dev` MAY keep it in memory only.

### 7.6 Cross-registry federation

Federation — the import of AnnounceFrames from a peer registry — is permitted only in the `public-federated` profile. Federation between two registries requires a **bilateral trust agreement**:

1. Both operators MUST exchange CA root certificate sets out of band; each side configures the other's roots as a federated-only trust set, distinct from its own native trust set.
2. AnnounceFrames imported from a federated peer MUST be re-validated against the importing registry's local CA trust chain (native ∪ federated-only). An AnnounceFrame valid at the peer is not implicitly valid here.
3. The federation channel itself MUST be authenticated (mutual TLS using NIP-issued certificates).
4. Federated entries MUST be tagged with the originating registry identifier so that downstream consumers can apply per-source policy (e.g. ranking, filtering).
5. Either party MAY revoke the agreement unilaterally; revocation MUST cause the importing registry to invalidate all federated entries from the revoked peer within one TTL window.

Federation is never transitive: trust agreement A↔B and B↔C does not establish A↔C.

### 7.7 Registry operator trust levels

The profile a Registry runs under implies a corresponding operator-side trust level. Conformant deployments MUST satisfy the trust-level requirements for their declared profile.

| Profile | Operator | CA scope | Audit requirement |
|---------|----------|----------|-------------------|
| `local-dev` | self (single developer / CI ephemeral) | none / self-signed | none |
| `org-private` | named organization | organization CA | internal audit (annual review of issuer allowlist, signed configuration changes) |
| `public-federated` | accountable legal entity (e.g. NPS Cloud) | public CA chain | external audit (independent third-party annual audit covering issuer allowlist provenance, federation agreements, key custody, and incident response) |

A Registry MUST NOT advertise a profile whose operator trust requirements it cannot meet.

### 7.8 Alpha.19 persistent recovery profile (NDP v0.13)

The following requirement IDs freeze restart and partition behavior. Shared
transcripts live in
[`recovery_fence_vectors.json`](conformance/ndp/recovery_fence_vectors.json).

1. **NDP-P19-01 — durable state.** `org-private` and `public-federated`
   Registries MUST durably store, at minimum, the highest accepted `graph_seq`
   and canonical-body digest per NID, the highest accepted ownership epoch per
   cluster, and every live entry's freshness deadline, origin, and trust class.
2. **NDP-P19-02 — commit before visibility.** A mutation that advances a
   sequence or epoch fence MUST commit the new fence and entry atomically before
   it is acknowledged, served by ResolveFrame, or forwarded to a peer.
3. **NDP-P19-03 — recovery order.** Startup restores and validates sequence and
   epoch fences before accepting traffic. It then removes expired live entries
   without deleting their fences. A recovered lower sequence/epoch can never
   replace a higher persisted value.
4. **NDP-P19-04 — equal-epoch split.** Two different live owners at the same
   highest cluster epoch remain `NDP-CLUSTER-SPLIT` after restart or partition
   healing; wall-clock arrival order is not a tie-breaker.
5. **NDP-P19-05 — corruption policy.** A persistence-required Registry MUST
   refuse readiness with `NDP-STATE-CORRUPT` when schema, checksum, or monotonic
   validation fails. It MUST NOT silently start from an empty state.
6. **NDP-P19-06 — unavailable store.** A persistence-required Registry that
   cannot durably read or commit state reports `NDP-STATE-UNAVAILABLE` and MUST
   NOT acknowledge the affected announce/federation mutation.
7. **NDP-P19-07 — local-dev boundary.** `local-dev` MAY use memory-only state,
   but MUST declare `recovery = "volatile"`; the other profiles MUST declare
   `recovery = "durable"` in health/capability output.
8. **NDP-P19-08 — federation provenance.** Recovered federated entries retain
   their origin Registry and trust class. Revoking a bilateral trust agreement
   invalidates matching recovered entries without lowering unrelated fences.

---

### 7.9 Production runtime discovery binding (NDP v0.14)

These requirements apply to clients using nps.production_runtime revision 1.
They reuse existing discovery fields and add no AnnounceFrame capability-as-authority shortcut.

1. **NDP-P20-01 — freshness.** Before selecting a new target, validate the resolved
   entry's timestamp/last_seen/ttl and existing signature/profile/fence requirements.
   Expired entries and health=draining MUST NOT receive new profile operations.
   A positive health report is not proof of authentication or feature support.
2. **NDP-P20-02 — owner fence.** Resolve cluster_anchor to the unique highest live
   cluster_epoch owner. Equal-epoch split, unavailable trusted resolution or a
   regressed fence fails closed under existing NDP errors. A cached lower owner
   MUST NOT be used as a convenience fallback.
3. **NDP-P20-03 — new binding.** An endpoint/owner/epoch change invalidates old-peer
   route and feature cache entries and affected pooled sessions. New connections
   perform NCP negotiation and NIP admission again. Neither Announce capabilities,
   Graph membership nor TLS resumption grants the new peer an old selection.
4. **NDP-P20-04 — HTTP evidence.** Fetch the endpoint's authenticated NWM and enforce
   NWP §9.6's all-backend route guarantee before feature-dependent HTTP dispatch.
   Discovery caches are hints, not authorization. Their keys include resolved peer
   identity/epoch, route and manifest version; cached support never extends freshness
   or substitutes for current handler admission.
5. **NDP-P20-05 — qualification record.** Runtime evidence records registry identity,
   resolved NID, endpoint, cluster_epoch, graph_seq, freshness deadline, manifest
   version and the selected feature revisions, omitting credentials and sensitive
   scope. This is a local/qualification record, not a new signed Announce field.
   Failover only permits an uncertain mutation retry under NCP §7.8's explicit shared
   durable deduplication contract; routing success alone never proves non-execution.

---

Reference implementation note: the opt-in [volatile result-route component](../impl/dotnet/src/NPS.NDP/Discovery/README.md) and [static/two-node fixture](../docs/samples/ndp-result-routing.md) qualify only their declared HTTP reference path. They do not satisfy the full production-runtime or durable-registry profile.

## 8. Change Log

| Version | Date | Changes |
|---------|------|---------|
| 0.14 | 2026-09-09 | Alpha.20 production discovery binding: exclude stale/draining targets, owner fences, fresh admission on peer changes, HTTP route guarantees and bounded qualification records; no new Announce fields. |
| 0.13 | 2026-08-31 | Alpha.19 persistent recovery profile: atomic durable sequence/epoch fences, ordered restart recovery, equal-epoch split preservation, fail-closed corrupt/unavailable state handling, explicit volatile/durable capability truth, federation provenance, and shared restart/partition transcripts. |
| 0.12 | 2026-07-29 | Defines the Registry Conformance profile: additive `graph_seq` wire field and compatibility behavior; deterministic signed-body canonicalization; ordered signature/profile/replay/conflict/staleness admission; exact duplicate versus advisory liveness refresh semantics; retained sequence fences after expiry/offline; highest-epoch cluster resolution and split-brain behavior; direction-specific, sorted Bridge capability discovery; and shared cross-language canonicalization/registry vectors. |
| 0.11 | 2026-07-12 | **NPS-CR-0010 Bridge Node is bidirectional**: AnnounceFrame (0x30) gains optional `bridge_inbound_protocols` (array of strings, same value domain as `bridge_protocols`) — the external protocols the node **serves inbound** (external → NPS). `bridge_protocols` keeps its exact prior meaning (protocols bridged **outbound**). Absent/empty `bridge_inbound_protocols` ⇒ no inbound surface, i.e. exactly a pre-alpha.16 outbound-only Bridge Node. A node declaring `"bridge"` MUST have at least one of the two arrays non-empty. Additive / backward-compatible; no new NDP error codes. |
| 0.10 | 2026-07-05 | **NPS-CR-0009 multi-Anchor HA**: AnnounceFrame (0x30) gains optional `cluster_epoch` (uint64, default 1) — the cluster-ownership fence. New §9 resolution rule: resolve the highest-`cluster_epoch` live Anchor for a `cluster_anchor` NID; equal-epoch split-brain → `NDP-CLUSTER-SPLIT`; federated Registries propagate `(cluster_anchor, cluster_epoch, active_nid)` and prefer higher epoch (monotonic per cluster). One new error code `NDP-CLUSTER-SPLIT`. Additive / backward-compatible (single-Anchor clusters keep epoch 1). |
| 0.9 | 2026-06-12 | AnnounceFrame (0x30) gains two additive liveness fields — `health` (`healthy`/`degraded`/`draining`, absent ⇒ `healthy`) and `last_seen` (ISO 8601 liveness beat). New §3.2.1 **resolve-time staleness**: a Registry MUST return `NDP-RESOLVE-STALE` when a resolved entry's freshness deadline `(last_seen ?? timestamp) + ttl` is in the past, rather than serve a dead endpoint; the resolved object SHOULD echo `health`. One new error code `NDP-RESOLVE-STALE`. Backward-compatible (both fields optional). |
| 0.9 | 2026-06-03 | AnnounceFrame gains `heartbeat_interval_ms` (uint32, default 60000) and `spawn_spec_ref` (string ref resolving to a SpawnSpec; §3.1.2 schema — OCI image + command + resource_limits; resolution per NPS-CR-0007 §5); `NDP-ANNOUNCE-STALE` error code (announce-time staleness, distinct from §3.2.1 resolve-time `NDP-RESOLVE-STALE`). |
| 0.8 | 2026-05-31 | §3.3 GraphFrame rewritten to topology-snapshot format: `graph_id` (UUID v4), `nodes` (array of NdpGraphNode with nid/cluster_anchor/node_roles), `edges` (array of NdpGraphEdge with from_nid/to_nid/latency_ms/protocol), `ttl`, `metadata`; max 256 nodes / 1024 edges; `NDP-GRAPH-TOO-LARGE` and `NDP-GRAPH-INVALID` error codes; §9 federation forwarding (public-federated MUST forward AnnounceFrames, `ndp-forwarded-by` header, max 3 hops, `NDP-FEDERATION-LOOP`). |
| 0.7 | 2026-05-10 | New §7.3–§7.7 introduce three named registry security profiles (`local-dev` / `org-private` / `public-federated`), AnnounceFrame anti-poisoning rules (signed scope, replay defense, duplicate suppression, conflict rejection), graph-sequence rollback defense, cross-registry federation requirements, and an operator trust-level table. New error codes: `NDP-ANNOUNCE-CONFLICT`, `NDP-GRAPH-SEQ-ROLLBACK`, `NDP-ISSUER-NOT-ALLOWED`, `NDP-CA-ATTEST-REQUIRED`. (Issue #33) |
| 0.6 | 2026-05-01 | **Breaking rename (pre-1.0)**: `node_kind` renamed to `node_roles` (array of strings only; single-string form retired). Parsers MUST accept `node_kind` as a parse-time alias through alpha.5 for backward compat. Constraint added: NWP NWM `node_type` MUST be one of the values in `node_roles` (see NWP §2.1 Node Role Resolution). Fixes M1 naming-disambiguation issue — `node_kind` (multi-role discovery field) and `node_type` (single operative role) were confusingly similar with no documented cross-protocol constraint. |
| 0.5 | 2026-04-26 | AnnounceFrame (0x30) gains three additive fields supporting NPS-CR-0001 — `node_kind` (string OR array of strings; values `"memory"`/`"action"`/`"complex"`/`"anchor"`/`"bridge"`; legacy `"gateway"` rejected), `cluster_anchor` (NID — for non-Anchor members of a cluster), `bridge_protocols` (array of strings — for `"bridge"` nodes; standard values `"http"`/`"grpc"`/`"mcp"`/`"a2a"`). All additive and backward-compatible: pre-alpha.3 publishers omit `node_kind` and receivers fall back to `node_type`. Depends-On upgraded to NCP v0.7 (NPS-RFC-0001) + NIP v0.9 (NPS-RFC-0003). |
| 0.4 | 2026-04-24 | AnnounceFrame (0x30) gains three additive fields — `activation_mode` (required for NPS-Node Profile L1+), `activation_endpoint` (required for `resident` / `hybrid`), `spawn_spec_ref` (L3, optional). New §3.1.1 Activation semantics with backward-compatibility rule for pre-alpha.3 publishers. `Depends-On` NCP version corrected to v0.5. |
| 0.3 | 2026-04-19 | Status Draft → Proposed; bilingual unification (EN primary + CN mirror); no wire-layer change |
| 0.2 | 2026-04-12 | Unified port 17433; error codes switched to NPS-status-code mapping; error-code list completed |
| 0.1 | 2026-04-10 | Initial skeleton: AnnounceFrame / ResolveFrame / GraphFrame, DNS TXT spec, initialization flow |

---

*Copyright: LabAcacia / INNO LOTUS PTY LTD · Apache 2.0*
