# Daemon: nps-ingress

**Status:** ✅ Latest published package — v1.0.0-alpha.19

> **Alpha.19 release:** the daemon terminates native
> TLS 1.3 NCP with ALPN `nps/1.0`, default-on mTLS, inline certificate/session
> NID binding, bounded admission and full-duplex backend proxying. Its four
> real-socket TLS cases are role-scoped evidence, not full Node L2
> certification. See [Alpha.19 Current Status](./Alpha19-Current-Status.md).

> **Audience:** Operators
> **Source-of-truth precedence:** `spec/` documents in [`labacacia/NPS`](https://github.com/labacacia/NPS/tree/main/spec) win over this page if they disagree.

`nps-ingress` is the public Internet ingress daemon for the NPS suite. Alpha.19
retains NCP-over-HTTP behind an external TLS proxy and adds the native TLS/mTLS
transport boundary described above. Rate limiting, NeuronHub authentication,
CGN debit, reputation and broad
DDoS controls are product/AaaS/optional-composition/deployment concerns, not
transport capabilities advertised by this daemon.

- **Source:** `NPS-Dev/tools/daemons/nps-ingress/`
- **Distribution:** `labacacia/NPS-Daemons` (public), assembled via `tools/release/sync-nps-daemons.sh`
- **Docker image:** `labacacia/nps-ingress:1.0.0-alpha.19` — the tag Compose applies to the image it **builds** from `nps-ingress/Dockerfile`. No image is published to any registry; use `docker compose up -d --build`.
- **Default port:** `:8080` (HTTP). Production deployments terminate TLS on `:443` via a reverse proxy (nginx, Caddy, or Traefik) in front of this daemon.
- **Layer:** L2

---

## NCP HTTP mode

In HTTP mode, each `POST` request body carries exactly one NPS frame; the response body carries exactly one frame. This makes NPS deployable over any HTTP/1.1 or HTTP/2 transport — useful for firewall-restricted environments that cannot establish raw TCP connections to port 17433.

Path-based routing: `nps-ingress` examines the `Host` header and request path to determine which backing `npsd` node to forward to. In a single-node deployment all traffic routes to `http://127.0.0.1:17433`. Multi-node routing is configured via the upstream registry at `nps-registry`.

When `nps-ingress` proxies a `GET /.nwm` manifest fetch, it preserves the upstream `X-NWM-Version` response header (the NWM's `manifest_version` uint32 counter, NWP v0.14) so external clients can perform `If-None-Match: <manifest_version>` conditional requests and receive `304 Not Modified` on unchanged manifests.

---

## HTTP-mode implementation status

The original Phase 1 skeleton — a public HTTP listener with a `/health` endpoint and a stable deployment surface (process name, Docker image tag, port) — has been present since alpha.3 to keep the deployment topology stable from the beginning of the daemon ecosystem.

As of alpha.13, `nps-ingress` ships working **HTTP-mode ingress**: it accepts NCP-over-HTTP frame requests on its public listener, applies the configured `X-Forwarded-For` / `X-Forwarded-Proto` handling, and forwards frames upstream to `npsd` at port 17433 (single-node) or to the node selected via `nps-registry` (multi-node). It also exposes the operability endpoints `/healthz`, `/readyz`, and `/metrics` (see below), and preserves the `X-NWM-Version` response header on proxied `GET /.nwm` fetches (NWP v0.14).

Some advanced ingress logic — rate limiting, NeuronHub-customer authentication, CGN debit triggering, NPS-RFC-0004 reputation checks, and Anchor Node middleware wiring per NPS-CR-0001 — remains in progress. The `nps-ingress` process MAY host an Anchor Node middleware via `NPS.NWP.Anchor`; that wiring is deferred until the Anchor Node middleware is stable. TLS is terminated by a reverse proxy in front of this daemon (see below). The release docs also align the native NCP TLS/mTLS contract at the SDK/spec layer; daemon endpoint wiring remains a follow-up.

### Alpha.19 release disposition

Native TLS endpoint wiring is implemented and tested. The other items in the
preceding HTTP-mode paragraph are not reclassified as missing transport
features: their product/AaaS/optional-composition/deployment ownership is
explicit, and `nps-ingress` does not advertise those capabilities. Topology,
Bridge, HA and Registry conformance families belong to other IUT roles, so the
daemon retains only claim-scoped TLS evidence.

The `/healthz`·`/readyz` probes are rendered by the transport-neutral `HealthProbeRenderer` (alpha.14) shared across the daemon set.

---

## Protocol-bridge compatibility packages (deprecated)

Separate from this Internet-ingress **daemon**, the suite formerly shipped three protocol-specific compatibility packages for external-to-NPS ingress. Their last published release is v1.0.0-alpha.16 (2026-07-23) — a prepared alpha.17 deprecation release was never published — and they are intentionally skipped from alpha.18 onward because the supported replacement is the bidirectional `LabAcacia.NPS.NWP.Bridge` package defined by CR-0010.

| Package | Bridges |
|---------|---------|
| `LabAcacia.McpIngress` | External MCP to NWP compatibility adapter; deprecated, last published at alpha.16 |
| `LabAcacia.A2aIngress` | External A2A to NOP compatibility adapter; deprecated, last published at alpha.16 |
| `LabAcacia.GrpcIngress` | External gRPC to NWP compatibility adapter; deprecated, last published at alpha.16 |

New integrations should use `LabAcacia.NPS.NWP.Bridge`, declare direction explicitly, and advertise inbound protocols through NDP `bridge_inbound_protocols`.

---

## Naming note

This is the **process** called `nps-ingress`. The spec-level role of "cluster control plane that routes NPS frames into a NOP DAG" has been renamed **Anchor Node** in the NWP specification by [NPS-CR-0001](https://github.com/labacacia/NPS/blob/main/spec/cr/NPS-CR-0001-anchor-bridge-split.md). The two concepts are related but distinct: `nps-ingress` is the deployment binary; Anchor Node is the spec-level role it hosts.

---

## HTTP-mode TLS termination

The `nps-ingress` container itself speaks plain HTTP on port 8080. Production deployments place a TLS-terminating reverse proxy in front:

```
Internet → nginx/Caddy/Traefik :443 (TLS) → nps-ingress :8080 (HTTP) → npsd :17433
```

Configure your reverse proxy to set `X-Forwarded-For` and `X-Forwarded-Proto` so that `nps-ingress` can log the real client address and detect the original scheme.

---

## Health and operability endpoints

As of alpha.13 `nps-ingress` exposes standard operability endpoints alongside the legacy `/health` probe:

- `GET /healthz` — liveness (process is up). Returns `200 OK`.
- `GET /readyz` — readiness (upstream `npsd` reachable and listener bound). Returns `200 OK` when ready, `503` otherwise.
- `GET /metrics` — Prometheus exposition (request counts, upstream-forward latency, 5xx/502 totals).
- `GET /health` — legacy JSON probe (retained for compatibility).

### `/health` response example

```json
{
  "status": "ok",
  "daemon": "nps-ingress",
  "version": "1.0.0-alpha.19",
  "layer": 2,
  "role": "internet-ingress",
  "port": 8080
}
```

### Graceful shutdown

On `SIGTERM`, `nps-ingress` drains gracefully over a **30-second window**: it stops accepting new connections, lets in-flight forwarded requests complete, and then exits. Because the ingress daemon is stateless, no state is lost; the drain simply avoids dropping requests mid-flight during a rolling deploy. Use `SIGTERM` (the default for `docker stop` / systemd) rather than `SIGKILL`.

---

## Configuration (env vars)

| Variable | Default | Required | Purpose |
|----------|---------|----------|---------|
| `NPSINGRESS_PORT` | `8080` | no | TCP port to bind. Production deployments terminate TLS externally; this daemon only listens HTTP. |
| `NPSINGRESS_HOST` | `0.0.0.0` | no | Bind address. The default is public — unlike `npsd`, `nps-ingress` is intentionally Internet-facing. |

### docker-compose.yml service definition (bundle-overlay)

```yaml
nps-ingress:
  build:
    context: ./nps-ingress
    dockerfile: Dockerfile
  image: labacacia/nps-ingress:1.0.0-alpha.19
  restart: unless-stopped
  ports:
    - "${NPS_INGRESS_PORT:-8080}:8080"
  depends_on:
    - npsd
```

The host port is configurable via the `NPS_INGRESS_PORT` environment variable at compose launch time (default `8080`).

> The `image:` value is only the name Compose gives the image it builds from
> `nps-ingress/Dockerfile` — the service declares `build:`, and the project publishes no
> container images to any registry. `docker pull labacacia/nps-ingress:1.0.0-alpha.19` will
> fail; start the stack with `docker compose up -d --build`.

---

## HA and load-balancer placement

`nps-ingress` is stateless (all state lives in `npsd` and `nps-registry`). It is safe to run multiple instances behind a load balancer — no session affinity is required. Each instance independently routes to the same backing `npsd`.

Recommended production topology:

```
LB :443 (TLS) ─┬─ nps-ingress-1 :8080 ─┐
               └─ nps-ingress-2 :8080 ─┴─ npsd :17433
```

---

## Common operational issues

**Port 8080 already in use**

Set `NPSINGRESS_PORT` to an available port, or stop the conflicting service. Check with `ss -tlnp | grep 8080`.

**All requests return 502 or hang**

`npsd` at `127.0.0.1:17433` is not reachable from inside the `nps-ingress` container. In Docker, ensure both containers share a network (in docker-compose, services in the same file share a default bridge) and that `npsd` is healthy before `nps-ingress` starts handling traffic.

---

## Cross-links

- [Protocol NCP](./Protocol-NCP.md) — wire format; HTTP mode is defined in NCP §2.2
- [Daemon NPSd](./Daemon-NPSd.md) — upstream target for forwarded frames
- [Daemon NPS-Registry](./Daemon-NPS-Registry.md) — topology store queried for multi-node routing
- [Operator Daemons Reference](./Operator-Daemons-Reference.md)
- [Operator Quickstart Bundle](./Operator-Quickstart-Bundle.md)

---

*Last reviewed at suite version: v1.0.0-alpha.19*
