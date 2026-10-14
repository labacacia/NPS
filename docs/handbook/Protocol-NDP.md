# Protocol: NDP — Neural Discovery Protocol

**Status:** ✅ Released alpha.19 reference

**Released spec**: `spec/NPS-4-NDP.md` v0.13 · **Port**: 17433 (shared) / 17436 (optional dedicated)

> **Alpha.19 release: NDP 0.13 Proposed.** It closes
> durable sequence/epoch recovery plus restart/partition/stale/equal-epoch
> fault handling across six SDKs. DNS TXT lookup/parse/fallback is implemented
> in all six. See [Alpha.19 Current Status](./Alpha19-Current-Status.md).

NDP is DNS for the AI era. Where DNS maps human-readable domain names to IP addresses, NDP maps NPS identities to physical endpoints and capability profiles — without a central registry. Nodes announce their own presence and capabilities; resolvers cache those announcements with a TTL. Agents discover nodes by querying the local registry, DNS TXT records, or the NPS Cloud Registry, in that priority order.

Related: [Protocol NWP](./Protocol-NWP.md) | [SDK Building a Bridge Node](./SDK-Building-a-Bridge-Node.md) | [Operator Daemons Reference](./Operator-Daemons-Reference.md)

> **alpha.18 release:** NDP v0.12 is the portable registry profile. It includes CR-0009 `cluster_epoch` fencing for multi-Anchor HA, CR-0010 `bridge_inbound_protocols` discovery, monotonic `graph_seq`, deterministic split/rollback handling, and shared registry conformance cases. Discovery advertises capability and reachability; it does not define LLM request semantics.

---

## Design Philosophy

NDP is explicitly decentralized: there is no required central registry. Any node can announce itself and any resolver can cache the result. The three resolution modes provide a fallback chain:

| Mode | Use case | Priority |
|------|----------|----------|
| Local registry (UDP multicast) | Intranet / LAN discovery | Highest |
| DNS TXT records | Public internet, distributed, no infrastructure needed | Medium |
| NPS Cloud Registry | Global centralized fallback | Lowest |

This layered approach lets an intranet deployment work entirely without external dependencies, a public deployment work without any custom registry infrastructure, and large production deployments use the NPS Cloud Registry for global reach.

---

## DNS TXT Discovery (alpha.5)

A Node MAY publish discovery information in standard DNS TXT records, enabling zero-infrastructure discovery for any Agent that can perform a DNS lookup.

**Node discovery record format:**
```
_nps-node.api.example.com.  IN TXT  "v=nps1 type=memory port=17434 nid=urn:nps:node:api.example.com:products fp=sha256:a3f9..."
```

**CA discovery record format:**
```
_nps-ca.mycompany.com.      IN TXT  "v=nps1 ca=https://ca.mycompany.com/.well-known/nps-ca"
```

**TXT record keys:**

| Key | Required | Description |
|-----|----------|-------------|
| `v` | yes | Version; fixed `nps1` (compact DNS convention; distinct from the NCP preamble `NPS/1.0\n`) |
| `nid` | yes | Node NID |
| `type` | no | Node type: `memory` / `action` / `complex` |
| `port` | no | Port (default 17434) |
| `fp` | no | Node certificate fingerprint for pinning |
| `ca` | conditional | CA discovery endpoint (required for CA records) |

**SDK resolver functions** (`resolveViaDns` / `ResolveViaDns` / `resolve_via_dns`) were added across all six SDKs in alpha.5. Each SDK supports a mockable `DnsTxtLookup` interface so implementations can be tested without real DNS infrastructure. In production, the system DNS resolver is used by default.

---

## Frame Types

### AnnounceFrame (0x30)

A Node or Agent broadcasts its presence and capabilities. Receivers cache the announcement for `ttl` seconds. A `ttl` of `0` is a shutdown notification — receivers MUST remove the entry from their cache.

**Key fields:**

| Field | Type | Description |
|-------|------|-------------|
| `nid` | string | Publisher NID |
| `addresses` | array | Physical address list; each entry: `{host, port, protocol}` |
| `capabilities` | array | Capability list (reuses NIP capability vocabulary) |
| `ttl` | uint32 | Cache validity in seconds; `0` = offline/shutdown |
| `timestamp` | string | Broadcast time (ISO 8601 UTC) |
| `node_roles` | array of strings | All roles this publisher carries (see below) |
| `activation_mode` | string | `ephemeral` / `resident` / `hybrid` (see Activation Semantics below) |
| `activation_endpoint` | object | Push target for `resident` / `hybrid` publishers; same shape as `addresses[]` entry. REQUIRED when `activation_mode` is `resident` or `hybrid` |
| `cluster_anchor` | string (NID) | For non-Anchor nodes joining a cluster: identifies the Anchor Node they register with. Absent for standalone nodes and Anchor Nodes themselves. (NPS-CR-0001) |
| `bridge_protocols` | array of strings | For Bridge Nodes: supported external protocols (see Bridge Node section). MUST be absent for non-Bridge nodes. (NPS-CR-0001) |
| `bridge_inbound_protocols` | array of strings | External protocols accepted by inbound Bridge adapters. Direction is explicit and independent from outbound `bridge_protocols` (NPS-CR-0010). |
| `cluster_epoch` | uint64 | Monotonic Anchor-cluster leadership epoch used to fence stale leaders and reject split-brain announcements (NPS-CR-0009). |
| `graph_seq` | uint64 | Monotonic signed announcement sequence used for replay/rollback and conflict detection. |
| `heartbeat_interval_ms` | uint32 | How often this node re-announces itself (milliseconds); default `60000` (`0` = disabled). Receivers SHOULD treat the node as offline if no AnnounceFrame arrives within 3× this interval — see staleness below (NDP v0.9) |
| `spawn_spec_ref` | string ref → SpawnSpec | Reference the publishing daemon resolves to a structured **SpawnSpec** object (OCI image + command + resource_limits) for constructing an Agent process on demand (ephemeral/hybrid cold start; Profile L3). The type changed from a plain URI string to a structured schema object in NDP v0.9 — see SpawnSpec Schema below |
| `health` | string | Publisher liveness self-report (NDP v0.9): `"healthy"` / `"degraded"` / `"draining"`. Absent ⇒ `"healthy"`. `"draining"` signals shutdown — SHOULD NOT receive new traffic |
| `last_seen` | string | ISO 8601 UTC liveness beat (NDP v0.9). When present, a Registry uses `last_seen + ttl` (not `timestamp + ttl`) as the resolve-time freshness deadline |
| `signature` | string | Ed25519 signature with the publisher's IdentFrame private key — prevents announcement forgery |

### node_roles Field (renamed from node_kind in NDP v0.6)

`node_roles` is an array of strings carrying all roles this publisher supports. Valid values: `"memory"`, `"action"`, `"complex"`, `"anchor"`, `"bridge"`. Single-role nodes send a one-element array.

The legacy value `"gateway"` was removed in v1.0-alpha.3 (NPS-CR-0001). Parsers MUST reject it with `NDP-ANNOUNCE-ROLE-REMOVED`. Any other unrecognized value MUST be rejected with `NDP-ANNOUNCE-ROLE-UNKNOWN`.

**Backward compatibility:** NDP v0.6 renamed `node_kind` to `node_roles`. Parsers MUST accept the old field name `node_kind` as a parse-time alias through the alpha transition window. Pre-alpha.3 publishers that omit the field entirely are treated as declaring a single role matching `node_type`.

**Cross-protocol constraint:** the NWM `node_type` (single string, NWP service layer) MUST be one of the values declared in `node_roles`. Validators SHOULD verify this against cached NDP data.

### Activation Semantics

`activation_mode` tells receivers how to deliver frames to this publisher:

| Mode | Sender behavior | Receiver expectation |
|------|-----------------|----------------------|
| `ephemeral` | Deliver via inbox + pull; publisher may not be running when frame arrives | Frame is queued in the publisher's per-NID inbox |
| `resident` | Push over long-lived connection to `activation_endpoint` | Publisher accepts pushed frames on the declared endpoint at all times |
| `hybrid` | Attempt push to `activation_endpoint` first; fall back to inbox if unreachable within wake budget | Publisher wakes from hibernation on first frame; push target resumes once awake |

Backward compatibility: NPS v1.0-alpha.2 publishers did not emit `activation_mode`. Receivers MUST treat an absent field as `ephemeral`. A receiver MUST NOT reject an `AnnounceFrame` solely for lacking this field.

### Heartbeat and Announce Staleness (NDP v0.9)

`heartbeat_interval_ms` (uint32, default `60000`, `0` = disabled) declares how often a node re-announces itself. Receivers SHOULD treat a node as offline once `3× heartbeat_interval_ms` has elapsed with no fresh AnnounceFrame, returning `NDP-ANNOUNCE-STALE` (mapped to `NPS-CLIENT-NOT-FOUND`) for that NID.

This announce-time staleness is distinct from resolve-time staleness: a Registry also computes a freshness deadline of `(last_seen ?? timestamp) + ttl` per entry and returns `NDP-RESOLVE-STALE` rather than serve an expired endpoint. The resolved object SHOULD echo the entry's `health` so callers can avoid a `draining` node even while it is still within TTL.

### SpawnSpec Schema (resolved form of spawn_spec_ref, NDP v0.9)

In NDP v0.9 the `spawn_spec_ref` type changed from a plain URI string to a structured **SpawnSpec** schema object describing how an ephemeral Agent node is instantiated on demand (Profile L3). Resolution rules (inline `spawnspec:` base64url-JSON data URI or an `https://`/`nwp://` URL) are standardized by NPS-CR-0007 §5.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `oci_image` | string | yes | OCI container image reference, e.g. `"registry.example.com/my-agent:v1.2"` |
| `command` | array[string] | no | Command override (Docker `CMD` equivalent) |
| `resource_limits` | object | no | Resource constraints: `cpu_millicores` (uint32) and `memory_mb` (uint32) |

Only relevant at Profile L3 (spawn-capable registries). Nodes at L1/L2 MAY include it; L1/L2 receivers SHOULD ignore it.

### Bridge Node: bridge_protocols (NPS-CR-0001)

Nodes that declare `"bridge"` in `node_roles` MUST also declare `bridge_protocols` — the list of external protocols this Bridge Node can translate to. Standard values:

| Value | External protocol |
|-------|------------------|
| `"http"` | HTTP / HTTPS (REST and streaming) |
| `"grpc"` | gRPC (unary and streaming) |
| `"mcp"` | Model Context Protocol |
| `"a2a"` | Agent-to-Agent protocol |

Third-party adapters MAY register additional values via future CRs. The `bridge_protocols` field MUST be absent for nodes that do not declare `"bridge"` in `node_roles`.

Bridge Nodes are stateless per request — they translate inbound NPS frames into the target protocol format and translate the response back into NPS frames (`CapsFrame`). They do not participate in cluster topology.

Direction note: Bridge Nodes translate NPS frames *outbound* to external protocols. The `compat/*-ingress` adapters (`mcp-ingress`, `a2a-ingress`, `grpc-ingress`) go in the opposite direction — they translate external protocol traffic *inbound* into NPS.

### Signed Announces: NdpAnnounceValidator

Every `AnnounceFrame` MUST carry a `signature` field — an Ed25519 signature by the publisher's NIP private key over the canonical body, canonicalized per RFC 8785 JCS. This prevents announcement forgery: an attacker cannot claim to be a NID they do not control.

Receivers MUST verify the signature before caching or acting on an `AnnounceFrame`. Verification MUST precede deduplication, conflict detection, and storage. Verification failure returns `NDP-ANNOUNCE-SIGNATURE-INVALID`. The `NdpAnnounceValidator` pattern (implemented in the .NET SDK as `NPS.NDP.Validation.NdpAnnounceValidator`) encapsulates signature verification plus NID-key binding check.

#### Signed canonical form — now normative & cross-SDK consistent (NDP v0.9 §7.4 "Signed scope")

The exact bytes covered by `signature` are now spelled out normatively and aligned identically across all six SDKs (.NET, Python, TypeScript, Java, Rust, Go):

- The signed body covers **all emitted AnnounceFrame wire fields EXCEPT** `signature`, `health`, `last_seen`, and the `frame` discriminant. (`health` and `last_seen` are mutable liveness fields and are intentionally outside the signature so a node can update them without re-signing.)
- Absent / `null` optional fields are **omitted** from the canonical body — they MUST NOT be serialized as JSON `null`.
- `heartbeat_interval_ms` **is signed**. When absent on the wire, verifiers canonicalize it to the default `60000` before verifying; an explicit `0` (heartbeat disabled) is signed literally as `0` and MUST NOT be coerced to the default.

> **⚠ Breaking (alpha.15):** before this realignment each SDK canonicalized the announce body slightly differently (e.g. emitting `null` optionals, or diverging on the `heartbeat_interval_ms` default). **Old per-SDK-divergent signed announcements may fail cross-SDK verification** against an alpha.15 verifier — re-sign affected announcements with an alpha.15 SDK. The frame **version number is unchanged (NDP v0.9)**; only the canonical signed scope was made normative.

---

### ResolveFrame (0x31)

Resolves an `nwp://` URL to a physical endpoint. The request carries the `target` URL and an optional `requester_nid` for authorization checks. The response carries the resolved `{host, port, cert_fingerprint, ttl}`.

If the target cannot be resolved across all available modes, the resolver returns `NDP-RESOLVE-NOT-FOUND`. If multiple registries return conflicting results, `NDP-RESOLVE-AMBIGUOUS` is returned.

### GraphFrame (0x32) — §5 Topology Snapshot

In NDP v0.8 the GraphFrame was rewritten to a **topology-snapshot** format with explicit node and edge lists, replacing the prior `initial_sync` / `patch` / `seq` scheme. It carries a full or partial topology snapshot for registry-to-registry gossip and change subscriptions.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `graph_id` | string | yes | Opaque identifier for this snapshot (UUID v4 or stable registry key) |
| `nodes` | array | yes | Array of `NdpGraphNode` objects; **maximum 256** |
| `edges` | array | yes | Array of `NdpGraphEdge` objects; **maximum 1024** |
| `ttl` | uint32 | no | Seconds this snapshot is considered fresh; default `60` |
| `metadata` | object | no | Arbitrary key-value metadata attached to this snapshot |

**NdpGraphNode:** `nid` (string, required), `cluster_anchor` (string NID, optional), `node_roles` (array[string], optional — same vocabulary as AnnounceFrame).

**NdpGraphEdge:** `from_nid` (string, required), `to_nid` (string, required), `latency_ms` (uint32, optional), `protocol` (string, optional — `"tcp"` / `"quic"` / `"http"`).

**Validation** (graph guard — now enforced by all six SDKs, alpha.15):
- `nodes.length` > 256 or `edges.length` > 1024 → `NDP-GRAPH-TOO-LARGE` (`NPS-LIMIT-PAYLOAD`).
- Every `from_nid` / `to_nid` MUST appear in `nodes`, and no self-edge (`from_nid == to_nid`) → otherwise `NDP-GRAPH-INVALID`.

The legacy `NDP-GRAPH-SEQ-GAP` error remains defined for the older contiguous-sequence semantics.

---

## Agent Initialization Flow

```
Agent starts
  |
  +-- 1. NIP: load IdentFrame (from file or fetch from CA)
  |
  +-- 2. NDP: send AnnounceFrame (broadcast agent online)
  |
  +-- 3. NDP: subscribe to GraphFrame (receive node topology)
  |         <- GraphFrame(initial_sync=true)   [full graph]
  |         <- GraphFrame(initial_sync=false)  [incremental updates, ongoing]
  |
  +-- Ready: begin NWP requests
```

---

## Relationship to NWP Topology Queries (NPS-CR-0002)

NDP is the data store and delivery mechanism for cluster membership. NWP §12 is the *query surface* over that data.

When a node joins a cluster, it sends an `AnnounceFrame` with `cluster_anchor` naming its Anchor Node's NID. The Anchor Node receives this announcement and adds the member to its internal topology registry. That registry is then exposed via the reserved NWP query types `topology.snapshot` and `topology.stream` (see [Protocol NWP](./Protocol-NWP.md)).

In other words: NDP feeds topology data into the Anchor Node; NWP §12 is how clients read it back out.

---

## Registry Security Profiles (NDP v0.8)

Every NDP Registry deployment MUST declare exactly one of three security profiles. The profile is configuration of the Registry (not of any AnnounceFrame): it determines which AnnounceFrames the Registry accepts, retains, and serves. Implementations MUST refuse to start without an explicit profile (no implicit default).

| Profile | Issuer allowlist | CA-attested NID | Replay window | Federation |
|---------|------------------|-----------------|---------------|------------|
| `local-dev` (LOCAL_DEV) | not enforced | not required | `0` (replay defense disabled) | not allowed |
| `org-private` (ORG_PRIVATE) | required (set of CA fingerprints) | SHOULD | `300s` | not allowed |
| `public-federated` (PUBLIC_FEDERATED) | enforced via CA trust chain | MUST | `300s` | allowed (bilateral trust agreement) |

- **`local-dev`**: single-host / single-developer only; MUST refuse to start on a non-loopback interface without a logged operator override. Not for any traffic-bearing service.
- **`org-private`**: a single organization's intranet registry; non-allowlisted signing chains rejected with `NDP-ISSUER-NOT-ALLOWED`; absent-but-required CA-attested NID rejected with `NDP-CA-ATTEST-REQUIRED`. Federation disabled.
- **`public-federated`**: public-internet registries (e.g. NPS Cloud); CA-attested NID MUST be required; federation MAY be enabled.

---

## Federation Forwarding (NDP §9, v0.8)

Only a `public-federated` registry forwards AnnounceFrames across federation links. The 3-hop federation loop guard below is now enforced by all six SDKs (alpha.15). When such a registry receives an AnnounceFrame from a peer registry it MUST:

1. Forward the frame to its own subscribers, appending its forwarding NID to the `ndp-forwarded-by` request header (comma-separated list of NIDs).
2. Drop the frame and return `NDP-FEDERATION-LOOP` if its own NID already appears in `ndp-forwarded-by` (loop detection).
3. Drop the frame silently if the hop count (length of `ndp-forwarded-by`) exceeds **3** hops.

`local-dev` and `org-private` registries MUST NOT forward AnnounceFrames; they MAY log a warning if a forwarded frame arrives.

**`ndp-forwarded-by` header format** — comma-separated NPS NIDs, one entry per hop:
```
ndp-forwarded-by: urn:nps:agent:registry-a.example.com:r1, urn:nps:agent:registry-b.example.com:r2
```

The `nps-ledger` daemon mirrors this loop-detection scheme on `POST /v1/log/federation/push` via the `X-NPS-Forwarded-By` header (same max 3 hops, same `NDP-FEDERATION-LOOP`).

---

## Error Codes

| Error Code | NPS Status | Description |
|------------|------------|-------------|
| `NDP-RESOLVE-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | `nwp://` address could not be resolved across any available mode |
| `NDP-RESOLVE-AMBIGUOUS` | `NPS-CLIENT-CONFLICT` | Conflicting resolution results from multiple registries |
| `NDP-RESOLVE-TIMEOUT` | `NPS-SERVER-TIMEOUT` | Resolution request timed out |
| `NDP-RESOLVE-STALE` | `NPS-CLIENT-NOT-FOUND` | Resolved entry's freshness deadline `(last_seen ?? timestamp) + ttl` is in the past; stale registration MUST NOT be served (NDP v0.9) |
| `NDP-ANNOUNCE-SIGNATURE-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | AnnounceFrame signature verification failed |
| `NDP-ANNOUNCE-NID-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | NID in AnnounceFrame does not match the signing certificate |
| `NDP-ANNOUNCE-ROLE-REMOVED` | `NPS-CLIENT-BAD-FRAME` | `node_roles` contains the retired `"gateway"` value (NPS-CR-0001); response SHOULD include a `hint` pointing to NPS-CR-0001 |
| `NDP-ANNOUNCE-ROLE-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | `node_roles` contains an unrecognized value |
| `NDP-ANNOUNCE-STALE` | `NPS-CLIENT-NOT-FOUND` | AnnounceFrame heartbeat has expired (3× `heartbeat_interval_ms` elapsed with no re-announce) (NDP v0.9) |
| `NDP-ANNOUNCE-CONFLICT` | `NPS-CLIENT-CONFLICT` | Two AnnounceFrames share the same `nid` and `graph_seq` but differ in content (registry poisoning attempt) (NDP v0.7) |
| `NDP-GRAPH-SEQ-ROLLBACK` | `NPS-CLIENT-BAD-FRAME` | AnnounceFrame `graph_seq` ≤ the last accepted value for this NID (rollback attempt) (NDP v0.7) |
| `NDP-GRAPH-SEQ-GAP` | `NPS-STREAM-SEQ-GAP` | GraphFrame sequence numbers are not contiguous |
| `NDP-GRAPH-TOO-LARGE` | `NPS-LIMIT-PAYLOAD` | GraphFrame `nodes` > 256 or `edges` > 1024 (NDP v0.8) |
| `NDP-GRAPH-INVALID` | `NPS-CLIENT-BAD-FRAME` | GraphFrame edge references a NID not in the nodes list, or a self-edge was detected (NDP v0.8) |
| `NDP-ISSUER-NOT-ALLOWED` | `NPS-AUTH-FORBIDDEN` | AnnounceFrame issuer (signing CA) is not in the active registry profile's issuer allowlist (NDP v0.7) |
| `NDP-CA-ATTEST-REQUIRED` | `NPS-AUTH-UNAUTHENTICATED` | Active registry profile requires a CA-attested NID and the certificate chain does not anchor in the configured trust roots (NDP v0.7) |
| `NDP-FEDERATION-LOOP` | `NPS-CLIENT-CONFLICT` | AnnounceFrame already carries the receiving registry's NID in `ndp-forwarded-by`, or hop count exceeds 3 (NDP v0.8 §9) |
| `NDP-REGISTRY-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | NDP Registry temporarily unavailable |

---

> Last reviewed at suite version: v1.0.0-alpha.19
>
> Alpha.19 source status reconciled on 2026-09-05.
