# Daemon: nps-runner

**Status:** ✅ Latest published package — v1.0.0-alpha.19

> **Alpha.19 release:** portable OCI SpawnSpec and
> reference resolution, durable shared-file SQLite leases/terminal dedup,
> restart fencing/reclaim and lease-loss worker cancellation are implemented.
> Full TaskFrame DAG/Saga L3 certification and generic cross-host filesystem
> guarantees remain unclaimed. See [Alpha.19 Current Status](./Alpha19-Current-Status.md).

> **Audience:** Operators
> **Source-of-truth precedence:** `spec/` documents in [`labacacia/NPS`](https://github.com/labacacia/NPS/tree/main/spec) win over this page if they disagree.

`nps-runner` is the NPS FaaS task executor. It watches the local [Daemon NPSd](./Daemon-NPSd.md) inbox for JSON spawn-spec messages, spawns worker subprocesses on demand, and manages their full lifecycle: stdout/stderr capture, idle-timeout and max-runtime enforcement, concurrency cap, and completion notifications back into the inbox.

- **Source:** `NPS-Dev/tools/daemons/nps-runner/`
- **Distribution:** `labacacia/NPS-Daemons` (public), assembled via `tools/release/sync-nps-daemons.sh`
- **Docker image:** `labacacia/nps-runner:1.0.0-alpha.19` — the tag Compose applies to the image it **builds** from `nps-runner/Dockerfile`. No image is published to any registry; use `docker compose up -d --build`.
- **Exposed port:** none for protocol traffic — `nps-runner` communicates entirely through the `npsd` inbox. As of alpha.13 it exposes operability endpoints (`/healthz`, `/readyz`, `/metrics`) on a local management port for probes and scraping.
- **Layer:** L1

---

## Relationship to npsd

`nps-runner` is architecturally subordinate to `npsd`. On startup it self-registers a sub-NID (identifier `NPS_RUNNER_AGENT_ID`, capabilities `["spawn"]`) via `POST /v1/agents` on the local `npsd`. Spawn requests are delivered as inbox messages to that NID; `nps-runner` polls `GET /v1/inbox/{runner-nid}` on a configurable long-poll cycle.

This design provides failure isolation: a worker crash cannot take down the protocol layer. `npsd` must be running before `nps-runner` starts; in docker-compose the `depends_on: npsd` relationship enforces this.

One `npsd` may serve any number of `nps-runner` instances. Each registers its own sub-NID (configured by `NPS_RUNNER_AGENT_ID`), so names must be unique per host when running multiple runners.

---

## Published alpha.18 NOP L3 runtime integration (CR-0007)

As of alpha.13 `nps-runner` implements the [NPS-CR-0007](https://github.com/labacacia/NPS/blob/main/spec/cr/NPS-CR-0007-nop-l3-runtime-integration.md) NOP Layer-3 runtime integration (NOP v0.7, NPS-Node Profile L3). This standardizes how a runner claims, resolves, and bounds NOP `TaskFrame` work so that multiple runners can share an inbox without double-execution.

### Task-claim lease protocol

A runner claims the head of a per-NID inbox by issuing an **atomic lease** rather than a bare ack:

- The claim carries `runner_nid`, a `dedup_key`, and a requested `lease_seconds` (the server clamps to `[10, 600]`).
- **Granted** — the inbox marks the task `LEASED` with `(runner_nid, lease_expiry)`. The runner MUST renew the lease (heartbeat) before expiry while the task runs.
- **Conflict** — if the task is already `LEASED` by a live lease, the claim is rejected with `NOP-CLAIM-CONFLICT` (→ `NPS-CLIENT-CONFLICT`, HTTP 409). The other runner already owns it.
- **Reclaim** — if the prior lease has expired, a new claim succeeds; the `dedup_key` ensures a terminal node is never re-run (at-least-once execution with a dedup guard).

In the published alpha.18 behavior described here, a runner is stateless beyond
its active lease set: a crash releases its leases after the lease TTL. The
alpha.19 release replaces that boundary with durable shared-file
claims, process-instance fencing and persistent terminal dedup; see the status
banner above.

### `spawn_spec_ref` → SpawnSpec resolution

The `spawn_spec_ref` reference on a task (NDP AnnounceFrame field, NPS-4 §3.1) is an opaque string the runner resolves to a structured **SpawnSpec** object (OCI image + command + `resource_limits`, NDP §3.1.2). It may be either an inline `spawnspec:` data URI carrying base64url-encoded JSON, or an `https://` / `nwp://` URL. If the reference cannot be resolved or the resolved object fails SpawnSpec schema validation, the runner rejects it with `NOP-SPAWN-SPEC-INVALID` (→ `NPS-CLIENT-BAD-PARAM`, HTTP 400).

### Idle / max-runtime enforcement

The runner enforces the SpawnSpec runtime bounds and reports breaches as NOP error codes:

| Bound | Source | Error on breach |
|-------|--------|-----------------|
| Idle timeout | SpawnSpec `idle_timeout_seconds` → runner policy | `NOP-RUNTIME-IDLE-TIMEOUT` (→ `NPS-SERVER-TIMEOUT`, 504) — worker exceeded idle timeout; node `FAILED` |
| Max runtime | SpawnSpec `max_runtime_seconds` → runner policy | `NOP-RUNTIME-MAX-RUNTIME` (→ `NPS-SERVER-TIMEOUT`, 504) — worker exceeded max runtime; node `FAILED` |

---

## Spawn-spec message format

To dispatch a task, deposit a message to the runner's inbox NID:

```
POST /v1/inbox/{runner-nid}
Content-Type: application/json
X-Nps-Inbox-Ttl-Seconds: 3600
```

Body:

```json
{
  "task_id": "abc123",
  "reply_to": "urn:nps:agent:host:caller-nid",
  "command": "claude",
  "args": ["remote-control", "--port", "1380", "--permission-mode", "bypassPermissions"],
  "work_dir": "/home/wind/project",
  "env": {
    "ANTHROPIC_API_KEY": "...",
    "CLAUDE_CODE_SANDBOXED": "1"
  },
  "idle_timeout_seconds": 600,
  "max_runtime_seconds": 3600
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `task_id` | string | no | Caller-supplied ID; auto-generated (UUID) if absent |
| `reply_to` | string | no | NID that receives the completion notification on worker exit |
| `command` | string | **yes** | Executable name or absolute path |
| `args` | string[] | no | Positional arguments |
| `work_dir` | string | no | Working directory; defaults to nps-runner's CWD |
| `env` | object | no | Extra env vars merged on top of the inherited environment |
| `idle_timeout_seconds` | int | no | Kill worker after N seconds with no stdout/stderr output |
| `max_runtime_seconds` | int | no | Hard wall-clock limit; ceiling is 4 hours |

---

## Worker lifecycle

1. Message arrives in inbox → deserialize spawn spec → check concurrency cap.
2. If at cap (`NPS_RUNNER_MAX_CONCURRENT_WORKERS`): message stays unacked and reappears on the next poll cycle.
3. Otherwise: spawn subprocess with the given `command` / `args` / `env` / `work_dir`.
4. stdout + stderr are captured to `NPS_RUNNER_LOG_DIR/{task_id}.log`.
5. Monitor loop (5 s tick) enforces `idle_timeout_seconds` and `max_runtime_seconds`.
6. On exit (any cause): ack the inbox message. If `reply_to` is set, POST a completion notification to that NID.

### Completion notification payload

```json
{
  "task_id": "abc123",
  "exit_code": 0,
  "killed_reason": null,
  "log_path": "/tmp/nps-runner-logs/abc123.log",
  "started_at": "2026-05-03T10:00:00.000Z",
  "finished_at": "2026-05-03T10:05:00.000Z"
}
```

`killed_reason` is one of `"idle_timeout"`, `"max_runtime"`, `"shutdown"`, `"exception"`, or `null` (clean exit).

---

## Concurrency model

`nps-runner` maintains a counter of simultaneously running worker processes. When the count reaches `NPS_RUNNER_MAX_CONCURRENT_WORKERS` (default 8), new spawn-spec messages are left unacked in the inbox and retried on the next poll interval. This backpressure is cooperative — there is no separate thread pool; each worker is a child process managed by the daemon.

---

## Configuration (env vars)

| Variable | Default | Required | Purpose |
|----------|---------|----------|---------|
| `NPSD_URL` | `http://127.0.0.1:17433` | no | `npsd` base URL used for self-registration and inbox polling. |
| `NPS_RUNNER_AGENT_ID` | `nps-runner` | no | Identifier used when self-registering the runner's sub-NID. Must be unique per host when running multiple instances. |
| `NPS_RUNNER_POLL_INTERVAL_MS` | `1000` | no | Inbox poll interval in milliseconds; also sets the long-poll `wait` window. |
| `NPS_RUNNER_MAX_CONCURRENT_WORKERS` | `8` | no | Maximum simultaneously running worker processes. |
| `NPS_RUNNER_LOG_DIR` | `/tmp/nps-runner-logs` | no | Directory for per-worker `{task_id}.log` files. |

### docker-compose.yml service definition (bundle-overlay)

```yaml
nps-runner:
  build:
    context: ./nps-runner
    dockerfile: Dockerfile
  image: labacacia/nps-runner:1.0.0-alpha.19
  restart: unless-stopped
  depends_on:
    - npsd
```

`nps-runner` needs no port mappings. Its only external interface is the `npsd` inbox.

> The `image:` line is a **local build tag**, not a registry coordinate: the service declares
> `build:`, so Compose compiles the daemon from `nps-runner/Dockerfile` and names the result.
> The project publishes no container images, so `docker pull` against this name will fail —
> bring the stack up with `docker compose up -d --build`.

---

## Health check and operability

As of alpha.13 `nps-runner` exposes operability endpoints on a local management port for container and systemd probes:

- `GET /healthz` — liveness (process is up). Returns `200 OK`.
- `GET /readyz` — readiness (sub-NID registered with `npsd`, inbox poll loop active). Returns `200 OK` when ready, `503` otherwise.
- `GET /metrics` — Prometheus exposition (active worker count, lease counts, spawn/claim/timeout totals).

The `/healthz`·`/readyz` probes are rendered by the transport-neutral `HealthProbeRenderer` (alpha.14) shared across the daemon set.

Liveness is also observable via log output — look for the `nps-runner ready` startup line and the periodic poll/spawn log lines.

```
nps-runner ready — NID=urn:nps:agent:host:nps-runner  npsd=http://127.0.0.1:17433 ...
```

When running in Docker, monitor with `docker compose logs -f nps-runner`.

### Graceful shutdown

On `SIGTERM`, `nps-runner` shuts down gracefully with a **30-second drain window**: it stops claiming new tasks, releases (or lets expire) its active leases, signals running workers (`killed_reason: "shutdown"`), and exits once they terminate or the drain window elapses. Use `SIGTERM` (the default for `docker stop` / systemd) rather than `SIGKILL` so leases are released cleanly and in-flight workers can finish or be reported.

---

## Why nps-runner is a separate daemon

Resource profile, failure isolation, and trust boundary all differ significantly between the protocol layer and the worker scheduler:

- A worker crash must not take the NCP layer (`npsd`) down.
- `nps-runner` executes user-supplied commands; `npsd` must not have a permission surface for that.
- Horizontal scaling is independent — you may run more runners on a machine without touching `npsd`.

See `docs/daemons/architecture.md` in the `labacacia/NPS-Daemons` distribution for the full rationale.

---

## Common operational issues

**nps-runner exits immediately**

`npsd` is not running or `NPSD_URL` is misconfigured. Start `npsd` first and verify `curl http://127.0.0.1:17433/health` returns `200`.

**Workers queue up but never start**

The concurrency cap (`NPS_RUNNER_MAX_CONCURRENT_WORKERS`) has been reached. Either increase the cap or wait for running workers to finish. Check `NPS_RUNNER_LOG_DIR` for log files from long-running workers.

**Sub-NID registration returns 409 on restart**

This is expected and harmless. The `409 Conflict` from `POST /v1/agents` means the NID was already issued on a previous run. `nps-runner` treats this as success and reuses the existing NID.

---

## Cross-links

- [Protocol NOP](./Protocol-NOP.md) — NPS Orchestration Protocol; tasks dispatched through nps-runner often execute NOP DAG steps
- [Daemon NPSd](./Daemon-NPSd.md) — the inbox host that nps-runner polls
- [Operator Daemons Reference](./Operator-Daemons-Reference.md)
- [Operator Quickstart Bundle](./Operator-Quickstart-Bundle.md)

---

*Last reviewed at suite version: v1.0.0-alpha.19*
