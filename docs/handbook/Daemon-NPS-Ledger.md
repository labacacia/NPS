# Daemon: nps-ledger

**Status:** ✅ Latest published package — v1.0.0-alpha.19

> **Audience:** Operators running a reputation log instance; AaaS operators peering with one
> **Distribution note:** `labacacia/NPS-Ledger` is a **private** repository. This page documents the protocol surface only.
> **Source-of-truth precedence:** `spec/` documents in [`labacacia/NPS`](https://github.com/labacacia/NPS/tree/main/spec) win over this page if they disagree.

`nps-ledger` is the NID reputation log daemon, implementing [NPS-RFC-0004](https://github.com/labacacia/NPS/blob/main/spec/rfcs/NPS-RFC-0004-nid-reputation-log.md). It is a Certificate-Transparency-style append-only log that records NID reputation events (rate-limit violations, revocations, policy breaches, and similar incidents). Auditors can verify entries are included in the log via RFC 9162 Merkle inclusion proofs.

`nps-ledger` is part of the NPS Cloud trust-anchor layer. It ships from a private repository in the `labacacia` organization and is available with NPS Cloud. For the OSS layer, see [Daemon NIP-CA-Server](./Daemon-NIP-CA-Server.md).

- **Source:** `NPS-Dev/tools/daemons/nps-ledger/`
- **Distribution:** `labacacia/NPS-Ledger` — **PRIVATE** (NPS Cloud product)
- **Docker image:** `labacacia/nps-ledger:1.0.0-alpha.19` — a local build tag only. No image is pushed to any registry, private or public; operators with repository access build it from the repo `Dockerfile` (`docker build -t labacacia/nps-ledger:1.0.0-alpha.19 .`).
- **Default port:** `:17440`
- **Layer:** L3

---

## Phase rollout

| Phase | Alpha | What shipped |
|-------|-------|-------------|
| Phase 1 | alpha.3 | HTTP API skeleton; entry POST/GET endpoints with in-memory store |
| Phase 2 | alpha.4 | SQLite persistence; RFC 9162 Merkle tree; operator-signed STH; inclusion proof endpoint |
| Phase 3 | alpha.5 | STH gossip federation (`GossipState` + `GossipService` + `GET /v1/log/gossip/sth`); 13 gossip tests added to test baseline |
| Phase 4 | alpha.11 | Batch federation push (`POST /v1/log/federation/push`) with `X-NPS-Forwarded-By` loop detection (NDP §9, max 3 hops, `NDP-FEDERATION-LOOP`); operational probes (`/healthz`, `/readyz`, `/metrics`) and `SIGTERM` graceful shutdown (30 s drain) |

---

## API surface

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/log/entries` | Append a `ReputationLogEntry`. Returns `201` with the stored entry, server-assigned `seq`, and server-stamped `timestamp`. Signature field must be present in `ed25519:{base64url}` form. |
| `GET` | `/v1/log/entries?nid=<subject_nid>&since=<seq>` | Query entries for a given NID whose `seq` is greater than `since`. Both parameters optional. |
| `GET` | `/v1/log/sth` | Return the current Signed Tree Head: `log_id`, `tree_size`, `timestamp`, `sha256_root_hash` (RFC 9162 binary Merkle root, hex-encoded), and `signature` (`ed25519:{base64url}` over the canonical JSON excluding the `signature` field). |
| `GET` | `/v1/log/proof?seq=<seq>` | Return an RFC 9162 §2.1.3 inclusion proof: `seq`, `leaf_index`, `tree_size`, `leaf_hash`, and `audit_path` (hex-encoded, leaf-to-root order). |
| `GET` | `/v1/log/gossip/sth` | Return this operator's current STH plus cached peer STHs received during the gossip cycle. Shape: `{own_sth, peer_sths: [{log_id, received_at, sth}]}`. |
| `POST` | `/v1/log/federation/push` | Batch push of reputation entries to a peer log (added in alpha.11). Accepts a batch of `ReputationLogEntry` records. Forwarding peers stamp `X-NPS-Forwarded-By` so loops can be detected per NDP §9 (max 3 hops); exceeding the hop limit or re-observing this log's own NID in the forwarding chain returns `NDP-FEDERATION-LOOP`. |
| `GET` | `/health` | Liveness probe (legacy NPS health envelope). |
| `GET` | `/healthz` | Kubernetes-style liveness probe (added in alpha.11). |
| `GET` | `/readyz` | Kubernetes-style readiness probe (added in alpha.11). |
| `GET` | `/metrics` | Prometheus metrics exposition (added in alpha.11). |

The `/healthz`·`/readyz` probes are rendered by the transport-neutral `HealthProbeRenderer` (alpha.14) shared across the daemon set.

---

## `/health` response example

```json
{
  "status": "ok",
  "daemon": "nps-ledger",
  "version": "1.0.0-alpha.19",
  "layer": 3,
  "role": "CT-style NID reputation log",
  "phase": 3,
  "entries": 127,
  "storage": "sqlite",
  "log_id": "urn:nps:log:operator-a1b2c3d4e5f6a7b8",
  "operator_pub_key": "ed25519:BASE64URL",
  "gossip_peers": 2,
  "gossip_interval_s": 30
}
```

---

## Configuration (env vars)

### Required

| Variable | Default | Purpose |
|----------|---------|---------|
| `NPSLEDGER_DATA_DIR` | `/data/nps-ledger` | Directory for the operator keypair (`operator.ed25519.pkcs8`, POSIX mode `0600`) and, if `NPSLEDGER_SQLITE_PATH` is unset, the default SQLite file. |

### Optional

| Variable | Default | Purpose |
|----------|---------|---------|
| `NPSLEDGER_PORT` | `17440` | TCP port to bind. |
| `NPSLEDGER_HOST` | `0.0.0.0` | Bind address. |
| `NPSLEDGER_SQLITE_PATH` | *(in-memory)* | Path to the SQLite database file. When unset, an ephemeral in-memory store is used (dev/test only). |
| `NPSLEDGER_LOG_ID` | *(derived from keypair)* | Override the `log_id` in STH responses. Default: `urn:nps:log:operator-{8-byte-fingerprint-hex}`. |
| `NPSLEDGER_PEERS` | `[]` | JSON array of gossip peer objects for Phase 3 STH federation. Each element: `{log_id, endpoint, pub_key?}`. Example: `[{"log_id":"urn:nps:log:peer","endpoint":"http://peer2:17440","pub_key":"ed25519:BASE64URL"}]`. |
| `NPSLEDGER_GOSSIP_INTERVAL_S` | `30` | Gossip cycle interval in seconds. Clamped to the range 10–3600. |

### Operator keypair

On first boot, `nps-ledger` generates an Ed25519 keypair and persists it at `${NPSLEDGER_DATA_DIR}/operator.ed25519.pkcs8` (POSIX mode `0600`). The `log_id` is derived from the keypair fingerprint unless `NPSLEDGER_LOG_ID` overrides it.

Retrieve the public key for peering with other ledger instances via `GET /health → operator_pub_key`.

---

## STH gossip (Phase 3, alpha.5)

The gossip subsystem implements RFC-0004 §4.5. On every interval tick, `GossipService` fetches `GET {peer}/v1/log/gossip/sth` from each configured peer, verifies the signature (if `pub_key` is configured for that peer), enforces monotonicity (tree_size must not decrease), and caches the accepted STH in `GossipState`.

Clients can call `GET /v1/log/gossip/sth` to retrieve this operator's current STH alongside all cached peer STHs, enabling cross-ledger consistency checks without directly contacting the peers.

### Peer configuration format

```json
[
  {
    "log_id": "urn:nps:log:operator-a1b2c3d4",
    "endpoint": "http://ledger2.internal:17440",
    "pub_key": "ed25519:BASE64URL"
  }
]
```

`pub_key` is strongly recommended in production. Without it, the daemon accepts STHs without cryptographic verification and logs a warning.

---

## Batch federation push (alpha.11)

`POST /v1/log/federation/push` lets one reputation log forward a batch of `ReputationLogEntry` records to a peer, complementing the pull-based STH gossip described above. Each hop appends its log identity to the `X-NPS-Forwarded-By` header. Following NDP §9 federation forwarding rules, a receiving log rejects a push with `NDP-FEDERATION-LOOP` when the forwarding chain exceeds **3 hops** or when this log's own NID already appears in `X-NPS-Forwarded-By` (indicating a cycle).

### ReputationLogClient (RFC-0004 Phase 2, from alpha.7)

All six SDKs (Python / TypeScript / Go / Java / Rust / .NET) ship a `ReputationLogClient` for interacting with this daemon. It speaks the CT-style reputation-log protocol with **dual Ed25519 signatures** and verifies the cryptographic chain end-to-end: it parses the `SignedTreeHead`, validates an `InclusionProof`, and recomputes the RFC 9162 Merkle fold to confirm an entry is included in the published tree. This is the recommended client surface for auditors and AaaS peers rather than calling the raw HTTP endpoints directly.

---

## Fork detection

When the gossip cycle receives a peer STH whose `tree_size` is less than the previously accepted `tree_size` for that peer, the daemon emits error code `NIP-REPUTATION-GOSSIP-FORK` and halts acceptance from that peer for the remainder of the current run. This guards against split-log / fork attacks. Investigate and restart the daemon to resume peering once the fork is resolved.

---

## Storage

SQLite with WAL mode. The Merkle tree is built in memory on demand from the full leaf-hash list. STHs are computed fresh on every `GET /v1/log/sth` request and signed with the operator's Ed25519 key.

For production deployments, always set `NPSLEDGER_SQLITE_PATH` to a durable path on a persistent volume.

---

## Common operational issues

**`operator.ed25519.pkcs8` not found after restart**

`NPSLEDGER_DATA_DIR` is not mounted to a persistent volume. The daemon generates a new keypair on each start, producing a different `log_id` and making all previously issued STHs unverifiable against the new key. Mount a persistent volume before first boot.

**Gossip peers show `gossip_peers: 0` in /health**

`NPSLEDGER_PEERS` is not set or is not valid JSON. Verify the JSON syntax and that the `endpoint` URL is reachable from the daemon container.

**Fork error on startup with a peer**

The peer's tree appears to have regressed (its `tree_size` is lower than the last accepted value in memory). Because the daemon restarts with an empty in-memory peer-state, this typically indicates the peer itself was reset. Review the peer's log, verify its SQLite database is intact, and restart this daemon to re-bootstrap the monotonicity baseline.

---

## Cross-links

- [Operator Reputation Log](./Operator-Reputation-Log.md) — operator how-to guide for submitting entries and auditing
- [Protocol NIP](./Protocol-NIP.md) — NIP §5.1.2 defines the `ReputationLogEntry` shape
- [Daemon NPS-Cloud-CA](./Daemon-NPS-Cloud-CA.md) — the complementary private trust-anchor CA daemon
- [Operator Daemons Reference](./Operator-Daemons-Reference.md)

---

*Last reviewed at suite version: v1.0.0-alpha.19*
