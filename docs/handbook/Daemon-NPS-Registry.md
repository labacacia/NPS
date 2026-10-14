# Daemon: nps-registry

**Status:** ✅ Latest published package — v1.0.0-alpha.19

> **Alpha.19 release:** the SQLite registry retains
> Announce/Resolve/Graph, highest-epoch Anchor selection and federated cluster
> tuple ingest, and interoperates with npsd managed-agent signed announcements.
> Announce signature/IdentFrame cryptographic validation and replicated-database
> HA remain explicit non-claims. See [Alpha.19 Current Status](./Alpha19-Current-Status.md).

> **Audience:** Operators
> **Source-of-truth precedence:** `spec/` documents in [`labacacia/NPS`](https://github.com/labacacia/NPS/tree/main/spec) win over this page if they disagree.

`nps-registry` is the cross-machine NDP (Neural Discovery Protocol, NPS-4 **v0.13**) registry for an NPS cluster. Where `npsd` knows only about its own host-local sessions, `nps-registry` aggregates AnnounceFrame records from multiple machines and answers NDP `Resolve` and `Graph` queries cluster-wide. It is the topology store that Anchor Node middleware queries to serve NWP `topology.snapshot` and `topology.stream` requests, and it is required for AaaS L2 conformance requirement L2-08.

- **Source:** `NPS-Dev/tools/daemons/nps-registry/`
- **Distribution:** `labacacia/NPS-Daemons` (public), assembled via `tools/release/sync-nps-daemons.sh`
- **Docker image:** `labacacia/nps-registry:1.0.0-alpha.19` — the tag Compose applies to the image it **builds** from `nps-registry/Dockerfile`. No image is published to any registry; use `docker compose up -d --build`.
- **Default port:** `:17436` (NDP optional-dedicated port per NPS-4)
- **Layer:** L2

---

## What nps-registry stores

Each record corresponds to one NDP `AnnounceBody` deposited via `POST /v1/announce`. Records are indexed by NID and capability. A monotonic per-cluster `seq` counter increments on every announce or TTL eviction, so clients can detect changes cheaply by polling `GET /v1/graph` and comparing `seq`.

TTL-based lazy expiry: records are not deleted by a background timer. Instead, expired records are filtered out on read. Each announce refreshes the TTL; the default TTL is the value in the announce body, or 300 seconds if unset.

### AnnounceFrame fields (NDP v0.9-v0.12)

As of NDP v0.9 the AnnounceFrame carries two additional fields the registry honors:

- **`heartbeat_interval_ms`** (uint32, optional, default `60000`) — how often the node re-announces itself. The registry SHOULD treat a node as offline if no AnnounceFrame arrives within **3×** this interval; a stale heartbeat surfaces as `NDP-ANNOUNCE-STALE` (→ `NPS-CLIENT-NOT-FOUND`). This is announce-time staleness, distinct from resolve-time `NDP-RESOLVE-STALE`.
- **`spawn_spec_ref`** (structured schema object) — as of NDP v0.9 this field's type changed from a bare URI string to a structured reference that resolves to a **SpawnSpec** (OCI image + command + `resource_limits`, NDP §3.1.2). It describes how an ephemeral/hybrid agent node is cold-started on demand; resolution rules are standardized by [NPS-CR-0007](https://github.com/labacacia/NPS/blob/main/spec/cr/NPS-CR-0007-nop-l3-runtime-integration.md) §5.
- **`cluster_epoch`** — monotonically fences stale Anchor leaders; equal highest epochs are treated as `NDP-CLUSTER-SPLIT`, never resolved arbitrarily (CR-0009).
- **`bridge_inbound_protocols`** — declares external protocols accepted by inbound Bridge adapters independently from outbound `bridge_protocols` (CR-0010).
- **`graph_seq`** — signed monotonic sequence for rollback and conflicting-announcement detection.

---

## API surface

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/announce` | Register or refresh a node announcement. Accepts an NDP `AnnounceBody`; returns the stored entry. TTL defaults to the announced value or 300 s if unset. |
| `GET` | `/v1/resolve?nid=<nid>` | Resolve a single NID to its current announcement. Returns `404` if unknown or expired. |
| `GET` | `/v1/graph` | Return all live (non-expired) announcements. As of NDP v0.8 the GraphFrame (0x32) uses the **§5 topology-snapshot** format: `graph_id` (UUID v4), `nodes` (NID / `cluster_anchor` / `node_roles`), `edges` (`from_nid` / `to_nid` / `latency_ms` / `protocol`), `ttl`, and `metadata`, plus the `seq` monotonic counter for client-side change detection. Limits: max **256 nodes** / **1024 edges** (`NDP-GRAPH-TOO-LARGE`); an edge referencing an unknown NID or a self-edge yields `NDP-GRAPH-INVALID`. |
| `GET` | `/healthz` | Liveness probe (process is up). Returns `200 OK`. |
| `GET` | `/readyz` | Readiness probe (storage open, ready to serve). Returns `200 OK` when ready, `503` otherwise. |
| `GET` | `/metrics` | Prometheus exposition (record count, announce/resolve rates, graph `seq`, federation-forward counters). |
| `GET` | `/health` | Legacy JSON liveness probe. Returns `status`, `storage`, and current graph `seq` (retained for compatibility). |

---

## `/health` response example

```json
{
  "status": "ok",
  "daemon": "nps-registry",
  "version": "1.0.0-alpha.19",
  "layer": 2,
  "role": "NDP cross-machine discovery registry",
  "storage": "sqlite",
  "seq": 42
}
```

The `/healthz`·`/readyz` probes are rendered by the transport-neutral `HealthProbeRenderer` (alpha.14) shared across the daemon set.

### Graceful shutdown

On `SIGTERM`, `nps-registry` drains gracefully over a **30-second window**: it stops accepting new announces/queries, flushes any pending writes to its store, and exits. Use `SIGTERM` (the default for `docker stop` / systemd) rather than `SIGKILL` so the persistent store and graph-sequence tracking are written out cleanly.

---

## Configuration (env vars)

| Variable | Default | Required | Purpose |
|----------|---------|----------|---------|
| `NPSREGISTRY_PORT` | `17436` | no | TCP port to bind. NDP optional-dedicated per NPS-4. |
| `NPSREGISTRY_HOST` | `0.0.0.0` | no | Bind address. A registry is intentionally network-facing so all machines in the cluster can reach it. |
| `NPSREGISTRY_SQLITE_PATH` | *(in-memory)* | no | Path to the SQLite database file. When unset, an ephemeral in-memory store is used (useful for dev/test; data is lost on restart). Set to a persistent path for production. |

### docker-compose.yml service definition (bundle-overlay)

```yaml
nps-registry:
  build:
    context: ./nps-registry
    dockerfile: Dockerfile
  image: labacacia/nps-registry:1.0.0-alpha.19
  restart: unless-stopped
  ports:
    - "${NPS_REGISTRY_PORT:-17436}:17436"
```

> The `image:` tag names the image Compose **builds** from `nps-registry/Dockerfile` — it is
> not a registry coordinate. The project publishes no container images, so `docker pull`
> against this name will fail. Use `docker compose up -d --build`.

To enable persistence, add a volume mount and set `NPSREGISTRY_SQLITE_PATH`:

```yaml
nps-registry:
  build:
    context: ./nps-registry
    dockerfile: Dockerfile
  image: labacacia/nps-registry:1.0.0-alpha.19
  restart: unless-stopped
  ports:
    - "17436:17436"
  volumes:
    - registry-data:/data
  environment:
    NPSREGISTRY_SQLITE_PATH: /data/registry.db
```

---

## Member-registry semantics

Nodes `ANNOUNCE` to the registry by posting an NDP `AnnounceBody`. The registry:

1. Validates the presence of a signature on the announce body (structural check in alpha.5; full Ed25519 verification against the node's IdentFrame is a follow-up milestone).
2. Upserts the record keyed by NID, resetting the TTL on each call.
3. Bumps the cluster graph `seq` counter.

When a node goes offline, its record expires after its TTL elapses. Nodes are expected to re-announce before expiry (typically every TTL/2 seconds, or per the AnnounceFrame `heartbeat_interval_ms`).

---

## Security profiles (NDP §7)

As of NDP v0.7+ a registry runs under one of three named **SecurityProfile** levels (enum `LOCAL_DEV` / `ORG_PRIVATE` / `PUBLIC_FEDERATED`; the on-wire / config profile names are `local-dev` / `org-private` / `public-federated`):

| SecurityProfile | Profile name | Issuer allowlist | CA-attested NID | Federation |
|-----------------|--------------|------------------|-----------------|------------|
| `LOCAL_DEV` | `local-dev` | none — accepts any signed announce (dev/test) | not required | disabled |
| `ORG_PRIVATE` | `org-private` | required (non-empty set of CA fingerprints); non-allowlisted chains rejected with `NDP-ISSUER-NOT-ALLOWED` | SHOULD be required (`NDP-CA-ATTEST-REQUIRED` if absent) | disabled |
| `PUBLIC_FEDERATED` | `public-federated` | implicit in the configured public trust root set | MUST be required (`NDP-CA-ATTEST-REQUIRED`) | MAY be enabled per §7.6 |

Anti-poisoning rules apply at `org-private`/`public-federated`: AnnounceFrame `graph_seq` is a per-NID monotonic counter, and any announce whose `graph_seq` is ≤ the tracked maximum for that NID is rejected with `NDP-GRAPH-SEQ-ROLLBACK` (the tracked maximum persists across restarts at these profile levels; `local-dev` MAY keep it in memory only).

---

## Federation forwarding (NDP §9)

A `public-federated` registry that receives an AnnounceFrame forwarded from a peer registry MUST:

1. **Forward** the frame to its own subscribers, appending its own NID to the `ndp-forwarded-by` request header (a comma-separated list of NIDs).
2. **Drop and reject** the frame with `NDP-FEDERATION-LOOP` if its own NID already appears in `ndp-forwarded-by` (loop detection).
3. **Drop silently** if the hop count (length of the `ndp-forwarded-by` list) exceeds **3** hops.

`ndp-forwarded-by` header format:

```
ndp-forwarded-by: urn:nps:agent:registry-a.example.com:r1, urn:nps:agent:registry-b.example.com:r2
```

Federation is bilateral and never transitive (a trust agreement A↔B and B↔C does not establish A↔C), and the federation channel itself MUST use mutual TLS with NIP-issued certificates. Federation is permitted only in the `public-federated` profile.

---

## Relationship to NWP topology queries (NPS-CR-0002)

NWP topology queries (`topology.snapshot`, `topology.stream`) from Anchor Nodes are backed by `nps-registry`. The Anchor Node middleware issues `GET /v1/graph` or `GET /v1/resolve?nid=…` to retrieve member data and returns it to the requesting agent.

Sub-Anchor recursion (NPS-CR-0002 OQ-1) allows an Anchor Node to fan out topology queries to sub-Anchor registries. This is optional at L2 and not required for L1 conformance.

---

## Scaling

`nps-registry` is typically run **once per cluster**, fronted by an internal load balancer for HA. It is network-facing by default (`0.0.0.0`) and should be placed on an internal network segment not reachable from the public Internet.

For single-machine deployments, you can omit `nps-registry` entirely — `npsd`'s local session data is sufficient and the registry defaults to the in-memory ephemeral store if no path is set.

NDP §9 cross-registry **federation forwarding** of AnnounceFrames (with `ndp-forwarded-by` loop detection and a 3-hop limit) is supported at the `public-federated` profile — see "Federation forwarding" above. HA cluster mode / gossip-style state replication between co-located registry replicas remains planned for a future milestone.

---

## Common operational issues

**Resolve returns 404 for a NID that should be registered**

Either the node's announcement TTL has expired, or the node has not called `POST /v1/announce` for this registry instance. Check with `GET /v1/graph` to see all live records. Ensure nodes are re-announcing before their TTL lapses.

**`seq` counter not advancing after announcements**

The registry may be using an in-memory store and has been restarted (clearing all state), or announcements are being rejected. Check the response body from `POST /v1/announce` for an error payload.

---

## Cross-links

- [Protocol NDP](./Protocol-NDP.md) — NPS Discovery Protocol; the announce/resolve/graph semantics
- [Operator AaaS Profile](./Operator-AaaS-Profile.md) — L2-08 conformance requirement backed by this daemon
- [Daemon NPS-Ingress](./Daemon-NPS-Ingress.md) — Anchor Node ingress that queries this registry for routing
- [Operator Daemons Reference](./Operator-Daemons-Reference.md)
- [Operator Quickstart Bundle](./Operator-Quickstart-Bundle.md)

---

*Last reviewed at suite version: v1.0.0-alpha.19*
