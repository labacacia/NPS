# Operator Daemons Reference

> **Audience:** Operators (devops / SREs deploying NPS infrastructure)
> **Status:** ✅ Latest published packages — v1.0.0-alpha.19
> **Source-of-truth precedence:** `spec/` documents in [`labacacia/NPS`](https://github.com/labacacia/NPS/tree/main/spec) win over this page if they disagree.

> **Alpha.19 release:** npsd native NCP/durable
> inbox/renewal/Announce, runner durable OCI lease execution, ingress native
> TLS/mTLS admission and the reconciled standalone bundle are implemented with
> claim-scoped manifests. See [Alpha.19 Current Status](./Alpha19-Current-Status.md).
> The alpha.19 source and local-build image examples below are the published contract.

This page is the single-page reference for all NPS daemons. Four daemons ship publicly in the `labacacia/NPS-Daemons` bundle; two additional daemons are private to the NPS Cloud platform.

> **Docker images are built locally, never pulled.** The project publishes **no** container
> images — not to Docker Hub, not to GHCR, not to any private registry. The `Docker image`
> row in each table below is the tag that `docker compose` applies to the image it **builds
> from the daemon's `Dockerfile`**, because every compose service declares `build:` alongside
> `image:`. Bring a stack up with `docker compose up -d --build`; `docker pull` / `docker run`
> against these names will fail with "manifest unknown".

> **Operational endpoints (alpha.6+).** **npsd** and **nip-ca-server** expose `GET /healthz`
> (liveness), `GET /readyz` (readiness), and `GET /metrics` (Prometheus format) alongside the
> legacy `GET /health`. The `/healthz`·`/readyz` probes are rendered by the transport-neutral
> `HealthProbeRenderer` (alpha.14) shared across the daemon set, so probe payloads are
> consistent regardless of host transport. On `SIGTERM` they perform a graceful drain
> (default 30 s) before exit. **nip-ca-server** serves `/metrics` on its **management port
> `17436`** — the public CA port `17435` no longer exposes `/metrics`. The bundle ships a
> single root `docker-compose.yml` alongside the four daemon source directories; there is
> no `deploy/` tree and no `Makefile` (verified against `v1.0.0-alpha.19`).

---

## npsd — NPS Daemon (Layer 1)

| Property | Value |
|----------|-------|
| **Port** | `17433` (default, loopback) |
| **Distribution** | `labacacia/NPS-Daemons` (public) |
| **Docker image** | `labacacia/npsd:{suite_version}` (local build tag) |
| **Layer** | L1 — host-local NCP/NIP/NDP/NWP |

### Purpose

`npsd` is the host-local NPS daemon. It is the foundation of every NPS deployment:

- Binds `127.0.0.1:17433` by default. Direct Internet exposure is `nps-ingress`'s job.
- Generates and persists the host's root Ed25519 keypair on first start (`root.ed25519.pkcs8`, mode `0600`). This satisfies Node-Profile L1 conformance case `TC-N1-NIP-01`.
- Issues **sub-NIDs** for agents hosted on this machine, signed with the root key. Sub-NID records are stored in a SQLite database.
- Maintains a **per-NID inbox queue** with long-poll, ack, priority, TTL, and depth caps.
- Serves `GET /.nwm` (daemon-self Neural Web Manifest), `GET /health`, plus `GET /healthz`, `GET /readyz`, and `GET /metrics` (alpha.6+).

### Required environment variables

| Variable | Default | Purpose |
|----------|---------|---------|
| `NPSD_PORT` | `17433` | TCP port to bind. |
| `NPSD_HOST` | `127.0.0.1` | Bind address. Use `0.0.0.0` only inside an isolated network namespace. |
| `NPSD_DATA_DIR` | `~/.local/share/npsd` | Persistent state: root keypair file + sub-NID SQLite database. |
| `NPSD_HOST_NID_PREFIX` | `urn:nps:host:{HostFingerprint}` | NID prefix when minting sub-NIDs. Override only if the host is registered under a different NID with an upstream CA. |
| `NPSD_SUB_NID_VALIDITY_DAYS` | `7` | Default validity window for issued sub-NIDs. |
| `NPSD_MAX_INBOX_DEPTH_PER_NID` | `1024` | Max pending messages per NID before deposits return `429`. |
| `NPSD_MAX_INBOX_MESSAGE_BYTES` | `65536` | Per-message payload cap (matches NCP default frame size). |
| `NPSD_MAX_INBOX_WAIT_SECONDS` | `30` | Maximum long-poll wait duration. Larger request values are clamped. |

### `/health` response

```json
{
  "status": "ok",
  "daemon": "npsd",
  "version": "1.0.0-alpha.19",
  "layer": "L1",
  "role": "node",
  "port": 17433,
  "host_nid": "urn:nps:host:example.com:host-01",
  "host_nid_fpr": "a1b2c3d4e5f6...",
  "uptime_s": 3842
}
```

### Key API endpoints

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/agents` | Issue a new sub-NID. Body: `{identifier?, capabilities[], scope?, agent_pub_key?, metadata?}`. If `agent_pub_key` is omitted, npsd mints an Ed25519 keypair and returns the private half **once**. |
| `GET` | `/v1/agents` | List issued sub-NIDs (newest first). Query: `?limit=N&offset=M`. |
| `GET` | `/v1/agents/{nid}` | Return the persisted record for a NID. |
| `POST` | `/v1/agents/{nid}/revoke` | Revoke a sub-NID. Body: `{reason?}`. |
| `POST` | `/v1/inbox/{nid}` | Deposit a message to a NID's inbox. |
| `GET` | `/v1/inbox/{nid}` | Long-poll for messages. Query: `?wait=N&batch=B`. |
| `DELETE` | `/v1/inbox/{nid}/{message_id}` | Ack and remove a message. Idempotent. |
| `GET` | `/v1/inbox/{nid}/depth` | Current pending message count. |
| `GET` | `/.nwm` | Daemon-self Neural Web Manifest. |
| `GET` | `/health` | Liveness probe (legacy shape). |
| `GET` | `/healthz` | Kubernetes-style liveness probe. |
| `GET` | `/readyz` | Readiness probe (accepting traffic). |
| `GET` | `/metrics` | Prometheus-format metrics. |

### Scaling

- **Development**: single instance is sufficient.
- **HA**: `npsd` is a host-local daemon by design. Run one instance per machine. For shared state across instances, configure `NPSD_DATA_DIR` to point at a shared volume (SQLite WAL mode) or replace the storage backend with an external store. Sub-NID records are append-only so WAL mode performs well in most cases.

### Alpha.19 release disposition

- Resident/hybrid socket push remains optional future scope; the profile
  advertises ephemeral HTTP pull with durable acknowledgement.
- Managed agents emit signed ephemeral AnnounceFrames with persistent sequence
  state; BYO-key agents remain an explicit opt-out.
- Eligible managed sub-NIDs renew in place; too-early, revoked and expired
  requests fail closed.
- The 20-case L1 manifest is exhaustive but still exposes incomplete required
  cases, so full Node L1 certification is not claimed.

---

## nps-runner — Task Executor

| Property | Value |
|----------|-------|
| **Port** | None (connects outbound to npsd) |
| **Distribution** | `labacacia/NPS-Daemons` (public, bundled with npsd) |
| **Docker image** | `labacacia/nps-runner:{suite_version}` (local build tag) |

### Purpose

Alpha.19 `nps-runner` is the task scheduler and FaaS runtime. It implements
portable OCI SpawnSpec,
durable shared-file SQLite claims/terminal dedup, restart fencing/reclaim and
lease-loss cancellation. It does not bind a protocol server port; all protocol
communication is outbound to npsd. Full TaskFrame DAG/Saga L3 certification is
not claimed.

Typical ratio: **1 npsd : N runners** (N = number of machines or worker pools).

### NOP L3 runtime lease (CR-0007)

Since alpha.13, nps-runner implements the **NOP L3 runtime integration** lease protocol
([NPS-CR-0007](https://github.com/labacacia/NPS/blob/main/spec/cr/NPS-CR-0007-nop-l3-runtime-integration.md)).
A runner claims a `TaskFrame` from the inbox, takes an exclusive **lease** on it, and renews
the lease while executing; a concurrent claim on a leased task returns `NOP-CLAIM-CONFLICT`
(HTTP 409). On lease expiry another runner may re-claim, and already-terminal DAG nodes are
not re-executed (`dedup_key` match). The current reference build ships this as a **NOP L3
lease** integration; see the [NPS-Node-L3 conformance suite](https://github.com/labacacia/NPS/blob/main/spec/services/conformance/NPS-Node-L3.md)
(`TC-N3-Claim-*`, `TC-N3-Spawn-*`, `TC-N3-Life-*`).

### Required environment variables

| Variable | Default | Purpose |
|----------|---------|---------|
| `NPSD_URL` | `http://127.0.0.1:17433` | npsd base URL this runner connects to. In Docker Compose use the service name: `http://npsd:17433`. |
| `NPS_RUNNER_AGENT_ID` | `nps-runner` | Identifier used when self-registering the runner's sub-NID with npsd. |
| `NPS_RUNNER_POLL_INTERVAL_MS` | `1000` | Inbox poll interval and long-poll `wait` window in milliseconds. |
| `NPS_RUNNER_MAX_CONCURRENT_WORKERS` | `8` | Cap on simultaneously running worker processes. Messages are re-queued when the cap is reached. |
| `NPS_RUNNER_LOG_DIR` | `/tmp/nps-runner-logs` | Directory for per-worker `{task_id}.log` capture files. |

### Liveness

nps-runner does not expose an HTTP port. Liveness is inferred from the npsd sub-NID heartbeat. Check that the runner's NID exists via `GET /v1/agents` on the connected npsd instance.

### Spawn-spec message format

Deposit a JSON message to the runner's inbox (`POST /v1/inbox/{runner-nid}`, `Content-Type: application/json`):

```json
{
  "task_id": "abc123",
  "reply_to": "<nid-to-notify>",
  "command": "my-tool",
  "args": ["--flag", "value"],
  "work_dir": "/home/user/project",
  "env": { "MY_ENV_VAR": "value" },
  "idle_timeout_seconds": 600,
  "max_runtime_seconds": 3600
}
```

On completion, nps-runner posts a notification JSON to `reply_to` containing `task_id`, `exit_code`, `killed_reason`, `log_path`, `started_at`, and `finished_at`. `killed_reason` is `null` for a clean exit, or one of `"idle_timeout"`, `"max_runtime"`, `"shutdown"`, `"exception"`.

### Concurrency

Workers share a single concurrency pool capped by `NPS_RUNNER_MAX_CONCURRENT_WORKERS`. If the cap is reached, the inbox message stays unacked and re-appears on the next poll cycle. Scale horizontally by running multiple nps-runner instances against the same npsd — each self-registers with a unique sub-NID.

---

## nps-ingress — Ingress

| Property | Value |
|----------|-------|
| **Port** | `8080` (HTTP; `443` in production via reverse proxy) |
| **Distribution** | `labacacia/NPS-Daemons` (public) |
| **Docker image** | `labacacia/nps-ingress:{suite_version}` (local build tag) |

### Purpose

Alpha.19 routes public NCP HTTP-mode traffic to npsd and also terminates native
TLS 1.3 NCP with ALPN
`nps/1.0`, default-on mTLS and inline certificate/session-NID binding. Rate
limiting, NeuronHub authentication, CGN debit, reputation and broad DDoS
controls are not advertised transport capabilities; their ownership is
product/AaaS/optional-composition/deployment scope.

> **Naming note.** The spec-level role of "cluster control plane that routes NPS frames into NOP" is called **Anchor Node** (renamed from Gateway Node by NPS-CR-0001). The `nps-ingress` process MAY host an Anchor Node middleware via `NPS.NWP.Anchor`; that wiring remains in progress as of alpha.13.

### Current status (released alpha.19)

Released alpha.19 keeps the public-facing HTTP listener and adds native NCP
TLS/mTLS admission. Product-level rate limiting, authentication, CGN debit,
reputation lookup and optional Anchor Node composition are not advertised as
transport capabilities. The deployment surface (process name, local-build
Docker image tag, and ports) remains stable.

### Alpha.19 release disposition

Native endpoint wiring is now implemented: bounded preamble/Hello/Ident
admission, TLS 1.3 + ALPN, client-chain validation, NID binding, full-duplex
proxying and four real-socket TLS cases. This is transport-IUT evidence only;
topology, Bridge, HA and Registry families do not become full-L2 claims.

The MCP, A2A, and gRPC **ingress compatibility packages** (`LabAcacia.McpIngress` / `LabAcacia.A2aIngress` / `LabAcacia.GrpcIngress`) shipped on the suite train from alpha.15 and are now **deprecated** — they are skipped from alpha.18 onward in favour of the bidirectional `LabAcacia.NPS.NWP.Bridge` package (CR-0010). See [nps-ingress](./Daemon-NPS-Ingress.md) for the per-package detail and last published versions.

### Required environment variables

| Variable | Default | Purpose |
|----------|---------|---------|
| `NPSINGRESS_HOST` | `0.0.0.0` | Bind address. The ingress daemon is intentionally Internet-facing, unlike npsd. |
| `NPSINGRESS_PORT` | `8080` | TCP port. Production deployments terminate TLS on `:443` via a reverse proxy. |

### HTTP-mode TLS termination

The container exposes plain HTTP on port 8080. Place it behind nginx, Caddy, or Traefik for TLS. Set `NPSINGRESS_PORT` on the host side to control the exposed port; the container always binds 8080 internally.

### `/health` response

```json
{
  "status": "ok",
  "daemon": "nps-ingress",
  "version": "1.0.0-alpha.19",
  "uptime_s": 120
}
```

---

## nps-registry — Node Registry

| Property | Value |
|----------|-------|
| **Port** | `17436` |
| **Distribution** | `labacacia/NPS-Daemons` (public) |
| **Docker image** | `labacacia/nps-registry:{suite_version}` (local build tag) |

### Purpose

`nps-registry` is the cross-machine NDP discovery registry. Each per-host `npsd` only knows its local agents. Cross-machine `Resolve` and `Graph` queries go to `nps-registry`. It aggregates `AnnounceFrame` registrations from multiple machines and exposes the full cluster topology.

Required for **AaaS L2-08**: the `topology.snapshot` / `topology.stream` reserved query types (NPS-2 §12) on an Anchor Node that maintains a member registry depend on data served by this daemon.

### Storage backend

By default, `nps-registry` runs with an ephemeral in-memory store. Set `NPSREGISTRY_SQLITE_PATH` to a persistent file path for production. Announcements are stored with TTL-based lazy expiry. A monotonic per-cluster `seq` counter bumps on every `Announce` or eviction, enabling clients to detect incremental changes efficiently.

### Required environment variables

| Variable | Default | Purpose |
|----------|---------|---------|
| `NPSREGISTRY_PORT` | `17436` | TCP port to bind (NDP optional-dedicated per NPS-4). |
| `NPSREGISTRY_HOST` | `0.0.0.0` | Bind address. The registry is intentionally network-facing. |
| `NPSREGISTRY_SQLITE_PATH` | *(in-memory)* | Path to the SQLite database file for persistent storage. |

### Key API endpoints

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/announce` | Register or refresh a node announcement. Accepts an NDP `AnnounceBody`. TTL defaults to the announced value (or 300 s if unset). |
| `GET` | `/v1/resolve?nid=<nid>` | Resolve a single NID to its current announcement. Returns `404` if unknown or expired. |
| `GET` | `/v1/graph` | Return all live announcements as a JSON array plus a `seq` monotonic counter. |
| `GET` | `/health` | Liveness probe. |

### `/health` response

```json
{
  "status": "ok",
  "daemon": "nps-registry",
  "version": "1.0.0-alpha.19",
  "storage": "sqlite",
  "seq": 17,
  "uptime_s": 3600
}
```

### Scaling

Run one `nps-registry` instance per cluster. It supports NDP §9 federation
forwarding and highest-epoch Anchor resolution, but does not provide replicated
database state between co-located replicas; that HA boundary remains future
work rather than an alpha.6+ current promise.

---

## nps-ledger — NID Reputation Log

| Property | Value |
|----------|-------|
| **Port** | `17440` |
| **Distribution** | `labacacia/NPS-Ledger` (PRIVATE — NPS Cloud) |
| **Docker image** | Private registry |

### Purpose

`nps-ledger` implements the Certificate-Transparency-style append-only NID reputation log defined by [NPS-RFC-0004](https://github.com/labacacia/NPS/blob/main/spec/rfcs/NPS-RFC-0004-nid-reputation-log.md). Entries are signed behavioral observations (rate-limit violations, revocations, scraping patterns, etc.) about specific NIDs, committed to a Merkle tree.

`nps-ledger` is **not** part of the public OSS bundle. It ships as a private image with NPS Cloud. OSS operators who need a reputation log can implement the RFC-0004 HTTP API independently.

### Phase feature set

| Phase | Suite version | Capability |
|-------|--------------|-----------|
| 1 | alpha.3 | HTTP API: `POST /v1/log/entries` (submit) + `GET /v1/log/entries` (query). No Merkle proofs. |
| 2 | alpha.4 | RFC 9162 Merkle tree, operator-signed STH (`GET /v1/log/sth`), inclusion proofs (`GET /v1/log/proof?seq=N`). Operator keypair generated on first boot at `${NPSLEDGER_DATA_DIR}/operator.ed25519.pkcs8`. |
| 3 | alpha.5 | STH Gossip Protocol. `GET /v1/log/gossip/sth` returns `own_sth` + `peer_sths`. Background gossip push-pull cycle with fork detection. |
| — | alpha.11 | Federation push: `POST /v1/log/federation/push` — batch reputation-entry push between federated registries with `X-NPS-Forwarded-By` loop detection (NDP §9, max 3 hops, `NDP-FEDERATION-LOOP`). |

### Required environment variables

| Variable | Default | Purpose |
|----------|---------|---------|
| `NPSLEDGER_PORT` | `17440` | TCP port to bind. |
| `NPSLEDGER_HOST` | `0.0.0.0` | Bind address. |
| `NPSLEDGER_DATA_DIR` | *(current dir)* | Directory for the operator keypair (`operator.ed25519.pkcs8`) and default SQLite file. |
| `NPSLEDGER_SQLITE_PATH` | *(in-memory)* | SQLite database path. Unset → ephemeral (useful for dev/test). |
| `NPSLEDGER_LOG_ID` | *(derived from keypair)* | Override the `log_id` in STH responses. Default: `urn:nps:log:operator-{8-byte-fpr-hex}`. |
| `NPSLEDGER_PEERS` | *(empty)* | Comma-separated `host:port` list of peer ledger operators for STH gossip. |
| `NPSLEDGER_GOSSIP_INTERVAL_S` | `30` | Gossip push-pull cycle interval in seconds (min 10, max 3600). |

### `/health` response

```json
{
  "status": "ok",
  "daemon": "nps-ledger",
  "version": "1.0.0-alpha.19",
  "phase": 3,
  "storage": "sqlite",
  "log_id": "urn:nps:log:operator-a1b2c3d4e5f6g7h8",
  "operator_pub_key": "ed25519:<base64url>",
  "gossip_peers": 2,
  "uptime_s": 7200
}
```

For full operating instructions, see [Operator Reputation Log](./Operator-Reputation-Log.md).

---

## nps-cloud-ca — NPS Cloud Certificate Authority

| Property | Value |
|----------|-------|
| **Port** | `17435` |
| **Distribution** | `labacacia/NPS-Cloud-CA` (PRIVATE — NPS Cloud) |
| **GA target** | 2027 Q1+ |

### Purpose

`nps-cloud-ca` is the managed NIP Certificate Authority for NPS Cloud customers. It issues, renews, and revokes Ed25519 NID certificates for Agents and Nodes at cloud scale. It is not available in the OSS distribution.

**OSS alternative**: for a self-hosted NIP CA, use `nip-ca-server` (see below).

---

## nip-ca-server — OSS NIP Certificate Authority

| Property | Value |
|----------|-------|
| **Port** | `17435` (public CA, default via Docker Compose); `17436` (management — `/metrics`, `/healthz`, `/readyz`) |
| **Distribution** | `labacacia/NIP-CA-Server` (PUBLIC) |
| **Repository** | [github.com/labacacia/NIP-CA-Server](https://github.com/labacacia/NIP-CA-Server) |
| **Docker image** | Built locally from the repo `Dockerfile` — none published to GHCR or Docker Hub |

### Purpose

`nip-ca-server` is the self-hostable NIP Certificate Authority — a single-binary ASP.NET Core service that issues, renews, and revokes Ed25519 NID certificates for NPS Agents and Nodes per NPS-3 §8.

### Storage backends

| Backend | How to enable |
|---------|--------------|
| **PostgreSQL** (production) | Set `CONNECTIONSTRINGS__POSTGRES`; the bundled `docker-compose.yml` ships a Postgres 16 sidecar. |
| **SQLite** (embedded / single-binary) | Use `AddNipCaWithSqlite()` in startup configuration (alpha.5.2). |

### Required environment variables

| Variable | Required | Purpose |
|----------|----------|---------|
| `NIPCA__CANID` | yes | CA NID, e.g. `urn:nps:org:ca.example.com` |
| `NIPCA__KEYPASSPHRASE` | yes | Passphrase for the AES-256-GCM + PBKDF2 encrypted CA key file |
| `NIPCA__BASEURL` | yes | Public HTTPS base URL of this CA |
| `CONNECTIONSTRINGS__POSTGRES` | yes (Postgres mode) | Postgres connection string |
| `NIPCA__OPERATORAPIKEY` | no | Bearer token for write endpoints; omit to disable auth (dev only) |
| `NIPCA__ACMEENABLED` | no | `true` to enable ACME RFC 8555 + `agent-01` challenge (NPS-RFC-0002, EXPERIMENTAL) |
| `NIPCA__AGENTCERTVALIDITYDAYS` | no | Agent certificate validity window (default `30`) |
| `NIPCA__NODECERTVALIDITYDAYS` | no | Node certificate validity window (default `90`) |
| `NIPCA__ALLOWEDCAPABILITIES` | no | Comma-separated capability allowlist; unlisted caps return `403` |

### Key API endpoints

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/v1/agents/register` | Register Agent, issue `IdentFrame` (Ed25519) |
| `POST` | `/v1/agents/register-x509` | Register Agent, dual-trust frame (Ed25519 + X.509, NPS-RFC-0002) |
| `POST` | `/v1/agents/{nid}/renew` | Renew Agent certificate |
| `POST` | `/v1/agents/{nid}/revoke` | Revoke Agent certificate |
| `GET` | `/v1/agents/{nid}/verify` | Verify / OCSP for an Agent NID |
| `POST` | `/v1/nodes/register` | Register Node, issue `IdentFrame` |
| `POST` | `/v1/nodes/register-x509` | Register Node, dual-trust frame |
| `GET` | `/v1/ca/cert` | CA public key |
| `GET` | `/v1/crl` | Certificate Revocation List |
| `GET` | `/.well-known/nps-ca` | CA discovery document |
| `GET` | `/health` | Liveness probe (legacy shape, public CA port) |

> **Management port (alpha.6+).** `nip-ca-server` exposes `GET /healthz`, `GET /readyz`,
> and `GET /metrics` (Prometheus format) on its **management port `17436`**. The public CA
> port `17435` no longer serves `/metrics`. Keep `17436` off the public Internet (bind it to
> an internal interface or restrict it via firewall). On `SIGTERM` the server drains
> in-flight requests for up to 30 s before exiting.

Write endpoints require `Authorization: Bearer <token>` when `NIPCA__OPERATORAPIKEY` is set.

### Quick start

```bash
git clone https://github.com/labacacia/NIP-CA-Server.git && cd nip-ca-server

cat > .env <<'EOF'
NIPCA__CANID=urn:nps:org:ca.example.com
NIPCA__BASEURL=https://ca.example.com
NIPCA__KEYPASSPHRASE=change-me-to-a-long-random-string
POSTGRES_PASSWORD=change-me-too
EOF

docker compose up -d --build
curl http://localhost:17435/health
```

The `nip-ca` service builds from the repository `Dockerfile`; there is no prebuilt image to
pull. (The `postgres:16-alpine` sidecar is a stock upstream image and is pulled normally.)

---

## nps-probe — Conformance CLI

| Property | Value |
|----------|-------|
| **Type** | Command-line tool (no long-running port) |
| **Version** | v0.2 |
| **Distribution** | `labacacia/NPS-Daemons` tooling (PUBLIC) |

### Purpose

`nps-probe` is the conformance / smoke-test CLI for NPS deployments. It drives an
implementation under test as a paired peer and runs a battery of checks against the
expected NPS surface (handshake, identity, discovery, inbox, and — as of v0.2 — NWM
`trust_anchors` validation as **Check 5**). Use it to validate a freshly stood-up
cluster or as part of CI before claiming a conformance level. For the full
self-attestation flow see [Operator Conformance Certification](./Operator-Conformance-Certification.md).

---

## NPS-Console — Cluster Manifest Manager (stub)

| Property | Value |
|----------|-------|
| **Version** | v0.1 (stub) |
| **Distribution** | preview |

### Purpose

`NPS-Console` is an early v0.1 **stub** for centralized management of NWP/NWM
cluster manifests. The current build exposes only `GET /health` and `GET /v1/nodes`;
it is a preview surface and not yet a production component.

---

## See also

- [Operator Quickstart: Daemon Bundle](./Operator-Quickstart-Bundle.md) — getting the four OSS daemons running end-to-end
- [Operator AaaS Profile](./Operator-AaaS-Profile.md) — compliance levels for services exposing NPS endpoints to agents
- [Operator Reputation Log](./Operator-Reputation-Log.md) — operating nps-ledger and the STH gossip federation

---

> Last reviewed at suite version: v1.0.0-alpha.19
>
> Alpha.19 source status reconciled through NPS-Dev PR #115 on 2026-09-05.
