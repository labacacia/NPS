# Daemon: npsd

**Status:** ✅ Latest published package — v1.0.0-alpha.19

> **Alpha.19 release:** native NCP now coexists with
> the HTTP control API; inbox/ack/TTL/priority state is durable SQLite; managed
> sub-NIDs renew in place and emit signed ephemeral NDP announcements with
> restart-stable key/sequence state. Resident/hybrid push and full Node L1
> certification remain unclaimed. See [Alpha.19 Current Status](./Alpha19-Current-Status.md).

> **Audience:** Operators
> **Source-of-truth precedence:** `spec/` documents in [`labacacia/NPS`](https://github.com/labacacia/NPS/tree/main/spec) win over this page if they disagree.

`npsd` is the host-local NPS protocol daemon — the L1 state host that every other NPS daemon and every local agent talks to first. It holds the host's root Ed25519 keypair, issues sub-NIDs for local agents on demand, maintains a per-NID inbox queue, and exposes the daemon's own Neural Web Manifest. Public Internet ingress is handled by [Daemon NPS-Ingress](./Daemon-NPS-Ingress.md); `npsd` itself binds loopback only.

- **Source:** `NPS-Dev/tools/daemons/npsd/`
- **Distribution:** `labacacia/NPS-Daemons` (public), assembled via `tools/release/sync-nps-daemons.sh`
- **Docker image:** `labacacia/npsd:1.0.0-alpha.19` — the tag Compose applies to the image it **builds** from `npsd/Dockerfile`. No image is published to any registry; use `docker compose up -d --build`.
- **Default port:** `127.0.0.1:17433` (loopback only — never expose directly to the Internet)
- **Layer:** L1

---

## What npsd does

1. **Root keypair management** — On first start `npsd` generates an Ed25519 root keypair and persists it to `${NPSD_DATA_DIR}/root.ed25519.pkcs8` with POSIX mode `0600`. This satisfies NPS-Node Profile conformance test `TC-N1-NIP-01`.
2. **Sub-NID issuance** — Mints child NIDs derived from the host root NID. Carrier IdentFrames are signed with the root key. Records are stored in `${NPSD_DATA_DIR}/sub-nids.sqlite`.
3. **Per-NID inbox queue** — Short-term in-memory queue per sub-NID with long-poll, ack, configurable depth caps, message priority, and TTL. Resident agents poll their own inbox or long-poll for push-style delivery.
4. **`GET /.nwm`** — Daemon-self Neural Web Manifest declaring all routes. Responses carry the `X-NWM-Version` header (the manifest's `manifest_version` uint32 counter); clients MAY use `If-None-Match: <manifest_version>` for conditional `304 Not Modified` requests (NWP v0.14).
5. **Operability endpoints** — `GET /healthz` (liveness), `GET /readyz` (readiness), and `GET /metrics` (Prometheus exposition) for Docker `HEALTHCHECK` / systemd probes and scraping. The `/healthz`·`/readyz` probes are rendered by the transport-neutral `HealthProbeRenderer` (alpha.14) shared across the daemon set. The legacy `GET /health` JSON probe remains available.

---

## API surface

### Sub-NID management

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/v1/agents` | Issue a new sub-NID. Body: `{identifier?, capabilities[], scope?, agent_pub_key?, metadata?}`. Returns `{frame: IdentFrame, minted_private_key?}`. If `agent_pub_key` is omitted, npsd mints an Ed25519 keypair and returns the private half **once** as `ed25519-raw:{base64url}`. |
| `GET` | `/v1/agents` | List issued sub-NIDs (newest first). Query: `?limit=N&offset=M`. |
| `GET` | `/v1/agents/{nid}` | Return the persisted record for a NID. |
| `POST` | `/v1/agents/{nid}/revoke` | Mark the NID revoked. Body: `{reason?}` (e.g. `"key_compromise"`). |

### Inbox

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/v1/inbox/{nid}` | Deposit a message addressed to `{nid}`. Headers: `Content-Type`, `X-Nps-Inbox-Priority` (int, default 0; higher drains first), `X-Nps-Inbox-Ttl-Seconds` (int, default 600). Returns `{message_id, enqueued_at, expires_at}`. `404` if recipient not on this host; `403` if revoked; `429` if inbox full; `413` if payload exceeds per-message cap. |
| `GET` | `/v1/inbox/{nid}` | Long-poll for messages. Query: `?wait=N` (seconds, clamped to `NPSD_MAX_INBOX_WAIT_SECONDS`), `?batch=B` (max messages returned, default 16). Returns `{nid, count, messages: [{message_id, enqueued_at, expires_at, priority, content_type, payload_b64}]}`. Empty array on timeout. |
| `DELETE` | `/v1/inbox/{nid}/{message_id}` | Ack a message, removing it from the queue. Idempotent — second call returns `404`. |
| `GET` | `/v1/inbox/{nid}/depth` | Current pending count for the NID. |

### Daemon

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/healthz` | Liveness probe (process is up). Returns `200 OK`. |
| `GET` | `/readyz` | Readiness probe (root keypair loaded, data dir writable, ready to serve). Returns `200 OK` when ready, `503` otherwise. |
| `GET` | `/metrics` | Prometheus exposition (inbox depth, sub-NID counts, request latency, etc.). |
| `GET` | `/health` | Legacy JSON liveness probe (retained for compatibility). |
| `GET` | `/.nwm` | Daemon-self Neural Web Manifest. Carries `X-NWM-Version` response header (NWP v0.14). |

---

## `/health` response example

```json
{
  "status": "ok",
  "daemon": "npsd",
  "version": "1.0.0-alpha.19",
  "layer": 1,
  "role": "protocol-access-host",
  "port": 17433,
  "host_nid": "urn:nps:host:a1b2c3d4e5f6a7b8",
  "host_nid_fpr": "a1b2c3d4e5f6a7b8"
}
```

All error responses carry `{error, status, message}` per the NPS error-code namespace.

---

## Graceful shutdown

On `SIGTERM`, `npsd` performs a graceful shutdown with a **30-second drain window**: it stops accepting new connections, allows in-flight requests and long-poll waits to complete, flushes pending state, and then exits. Send `SIGTERM` (the default for `docker stop` and systemd) rather than `SIGKILL` so inbox and sub-NID state are persisted cleanly.

---

## Configuration (env vars)

| Variable | Default | Required | Purpose |
|----------|---------|----------|---------|
| `NPSD_PORT` | `17433` | no | TCP port to bind. |
| `NPSD_HOST` | `127.0.0.1` | no | Bind address. Use `0.0.0.0` only inside an isolated network namespace — never expose `npsd` directly to the public Internet. |
| `NPSD_DATA_DIR` | `~/.local/share/npsd` | no | Persistent state directory. Holds `root.ed25519.pkcs8` (POSIX `0600`) and `sub-nids.sqlite`. |
| `NPSD_HOST_NID_PREFIX` | `urn:nps:host:{HostFingerprint}` | no | NID prefix used when minting sub-NIDs. Override only when the host has been registered with an upstream CA under a different NID. |
| `NPSD_SUB_NID_VALIDITY_DAYS` | `7` | no | Default validity window for issued sub-NIDs. |
| `NPSD_MAX_INBOX_DEPTH_PER_NID` | `1024` | no | Max pending messages per NID before deposits return `429`. |
| `NPSD_MAX_INBOX_MESSAGE_BYTES` | `65536` | no | Per-message payload cap (matches NCP default frame size of 64 KB). |
| `NPSD_MAX_INBOX_WAIT_SECONDS` | `30` | no | Maximum long-poll wait time. Larger values are clamped to this ceiling. |

### docker-compose.yml service definition (bundle-overlay)

```yaml
npsd:
  build:
    context: ./npsd
    dockerfile: Dockerfile
  image: labacacia/npsd:1.0.0-alpha.19
  restart: unless-stopped
  ports:
    - "127.0.0.1:17433:17433"   # loopback only — public ingress is nps-ingress
  volumes:
    - npsd-data:/data
  environment:
    NPSD_HOST: 0.0.0.0
    NPSD_PORT: 17433
    NPSD_DATA_DIR: /data
```

> Because the service declares `build:`, the `image:` line only names the artefact Compose
> builds locally from `npsd/Dockerfile` — the project publishes no container images, so
> `docker pull labacacia/npsd:1.0.0-alpha.19` will not resolve. Start with
> `docker compose up -d --build`.

> The compose service uses `NPSD_HOST: 0.0.0.0` because Docker networking provides its own isolation. The `ports` binding pins host-side to `127.0.0.1` so the container port is still not reachable from outside the machine.

---

## Scaling (published alpha.18)

`npsd` is designed as a **single instance per machine**. Its state — the root keypair and the sub-NID SQLite database — are host-local by design. There is no built-in HA replication; each machine in a cluster runs its own `npsd`.

State is stored at `NPSD_DATA_DIR` (default `~/.local/share/npsd` on Linux, `/data` in Docker). Mount a named volume or a persistent host path at that location for durability across container restarts.

For high-availability scenarios where a machine hosts multiple workers, the inbox queue is in-memory by default. An inbox restart loses unacked messages; agents should be prepared to re-submit or tolerate duplicate delivery after a restart.

---

## Alpha.19 release disposition

- Push delivery to resident/hybrid agent sockets remains optional future scope;
  this profile advertises ephemeral HTTP pull with durable acknowledgement.
- Managed agents now emit publisher-signed AnnounceFrames with persistent
  monotonic `graph_seq`; BYO-key agents remain an explicit opt-out because
  npsd does not possess their private key.
- Eligible managed sub-NIDs now renew in place. Too-early, revoked and expired
  credentials fail closed.
- The exhaustive 20-case L1 manifest retains incomplete required cases, so no
  full Node L1 certification is claimed.

---

## Common operational issues

**Key file not found on startup**

`npsd` logs `root keypair not found; generating` and creates a new one. If the data directory is not mounted or has wrong permissions, the file will not persist. Ensure `NPSD_DATA_DIR` points to a writable directory and that the directory is mounted to a durable volume in Docker.

**Port 17433 conflict**

Another process is already bound to `127.0.0.1:17433`. Common culprits: a previous `npsd` instance still running, or a leftover container. Check with `ss -tlnp | grep 17433` and terminate the conflicting process, or override to a different port via `NPSD_PORT`.

**`429 Too Many Requests` on inbox deposit**

The recipient NID's inbox has hit `NPSD_MAX_INBOX_DEPTH_PER_NID` (default 1024). Either the consumer is not draining messages or the depth cap is too low for the workload. Increase the cap or ensure the agent is polling and acking messages in a timely fashion.

---

## Spec references

- [NPS-Node Profile](https://github.com/labacacia/NPS/blob/main/spec/services/NPS-Node-Profile.md) — compliance specification this daemon targets
- [NPS-Node-L1 conformance suite](https://github.com/labacacia/NPS/blob/main/spec/services/conformance/NPS-Node-L1.md) — 20 `TC-N1-*` case headings
- [Protocol NCP](./Protocol-NCP.md) — wire layer
- [Protocol NIP](./Protocol-NIP.md) — root keypair / IdentFrame semantics

## Cross-links

- [Daemon NPS-Runner](./Daemon-NPS-Runner.md) — task scheduler that self-registers a sub-NID and polls `npsd` inbox
- [Daemon NPS-Ingress](./Daemon-NPS-Ingress.md) — public Internet ingress; forwards frames upstream to `npsd`
- [Operator Daemons Reference](./Operator-Daemons-Reference.md)
- [Operator Quickstart Bundle](./Operator-Quickstart-Bundle.md)

---

*Last reviewed at suite version: v1.0.0-alpha.19*
