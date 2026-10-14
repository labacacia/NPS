# Daemon: nip-ca-server

**Status:** ✅ Latest published package — v1.0.0-alpha.19

> **Audience:** Operators running a self-hosted NIP Certificate Authority
> **Source-of-truth precedence:** `spec/` documents in [`labacacia/NPS`](https://github.com/labacacia/NPS/tree/main/spec) win over this page if they disagree.

`nip-ca-server` is the open-source reference implementation of the NIP Certificate Authority role. It is a single-binary ASP.NET Core service that issues, renews, and revokes Ed25519 NID certificates for NPS Agents and Nodes, per [NPS-3 NIP §8](https://github.com/labacacia/NPS/blob/main/spec/NPS-3-NIP.md). This is the CA option available today for any self-hosted NPS deployment.

- **Source:** `NPS-Dev/tools/nip-ca-server/` (lives outside `tools/daemons/` — it has its own distribution repo)
- **Distribution:** `labacacia/NIP-CA-Server` — **PUBLIC**
- **Docker image:** none published. The repository's `docker-compose.yml` declares a `build:` context for the `nip-ca` service, so the image is compiled locally from the repo `Dockerfile` at `docker compose up --build` time. There is no image on GHCR or Docker Hub to pull.
- **Default port:** `:17435` (plain HTTP; TLS terminated externally)
- **Note:** Not part of the `labacacia/NPS-Daemons` bundle — distributed separately

---

## New in alpha.5.2: SQLite backend

`SqliteNipCaStore` is a new embedded SQLite storage backend that eliminates the PostgreSQL dependency for single-operator or development deployments. The `INipCaStore` interface is pluggable, so custom backends can be injected.

| Store | DI registration | Use case |
|-------|----------------|----------|
| `NpgSqlNipCaStore` (PostgreSQL) | `AddNipCa(configure)` | Multi-operator production deployments needing external DB |
| `SqliteNipCaStore` (SQLite) | `AddNipCaWithSqlite(configure, connectionString)` | Single-operator or embedded deployments (new in alpha.5.2) |
| Custom store | `AddNipCa(configure, INipCaStore)` | Bespoke backend injection |

---

## Quick start (Docker)

```bash
git clone https://github.com/labacacia/NIP-CA-Server.git
cd nip-ca-server

cat > .env <<'EOF'
NIPCA__CANID=urn:nps:org:ca.example.com
NIPCA__BASEURL=https://ca.example.com
NIPCA__KEYPASSPHRASE=change-me-to-a-long-random-string
POSTGRES_PASSWORD=change-me-too
EOF

docker compose up -d --build
curl http://localhost:17435/health
```

`--build` is required on the first run: the `nip-ca` service is built from the repository
`Dockerfile`, not pulled — no prebuilt `nip-ca-server` image is published to any registry.
(The `postgres:16-alpine` sidecar is a stock upstream image and is pulled normally.) To build
the image on its own, without Compose:

```bash
docker build -t nip-ca-server:1.0.0-alpha.19 .
```

---

## API surface

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/v1/agents/register` | Register Agent; issue `IdentFrame` (Ed25519) |
| `POST` | `/v1/agents/register-x509` | Register Agent; issue dual-trust frame (Ed25519 + X.509 chain, NPS-RFC-0002) |
| `POST` | `/v1/agents/{nid}/renew` | Renew Agent certificate |
| `POST` | `/v1/agents/{nid}/revoke` | Revoke Agent certificate |
| `GET` | `/v1/agents/{nid}/verify` | Verify / OCSP for an Agent NID |
| `POST` | `/v1/nodes/register` | Register Node; issue `IdentFrame` (Ed25519) |
| `POST` | `/v1/nodes/register-x509` | Register Node; issue dual-trust frame (Ed25519 + X.509 chain, NPS-RFC-0002) |
| `POST` | `/v1/nodes/{nid}/renew` | Renew Node certificate |
| `POST` | `/v1/nodes/{nid}/revoke` | Revoke Node certificate |
| `GET` | `/v1/nodes/{nid}/verify` | Verify / OCSP for a Node NID |
| `POST` | `/v1/orchestrators/groups/{group}/register` | CR-0003: register an orchestrator group; mints a `group-`-prefixed NID |
| `DELETE` | `/v1/orchestrators/groups/{group}/revoke` | CR-0003: revoke a group NID (children cascade via `parent_revoked`) |
| `POST` | `/v1/orchestrators/groups/{group}/sessions/issue` | CR-0003: issue a `session-`-prefixed NID under a group, recording `lineage` |
| `GET` | `/v1/orchestrators/groups/{group}/sessions` | CR-0003: list active session NIDs for a group |
| `GET` | `/v1/ca/cert` | CA public key |
| `GET` | `/v1/crl` | Certificate Revocation List (carries `issued_at` + a detached CA signature, alpha.14) |
| `GET` | `/.well-known/nps-ca` | CA discovery document (no longer advertises an unmapped `/ocsp`, alpha.14) |
| `GET` | `/health` | Liveness probe; returns `200` when ready (legacy envelope) |
| `GET` | `/healthz` | Kubernetes-style liveness probe (added alpha.6+) |
| `GET` | `/readyz` | Kubernetes-style readiness probe (added alpha.6+) |

Write endpoints (`register`, `register-x509`, `renew`, `revoke`, and the `orchestrators/groups/*` mutations) require `Authorization: Bearer <token>` when `NIPCA__OPERATORAPIKEY` is set.

> **CR-0005 RA model (Registration Authority):** registration may be gated behind an RA. A bootstrap token admits a node into a **pending-registration** queue; an operator approves the pending registration before the CA issues a certificate. This lets the CA delegate identity-proofing to a separate authority while retaining issuance control.

> **`/metrics` is served on the management port 17436**, never on the public CA port 17435. The public CA port no longer exposes `/metrics`.

---

## IANA PEN 65715 OID arc (CR-0004)

IANA **Private Enterprise Number 65715** was assigned to the NPS Committee on **2026-05-08**. All NPS X.509 OIDs now anchor to `1.3.6.1.4.1.65715`, replacing the provisional `1.3.6.1.4.1.99999` arc that earlier alphas used. Issued certificates carry the NPS extension OIDs:

| OID | Name | Encoding |
|-----|------|----------|
| `1.3.6.1.4.1.65715.2.2` | `id-nps-node-roles` | ASN.1 `SEQUENCE OF UTF8String` |
| `1.3.6.1.4.1.65715.2.3` | `id-nps-capabilities` | ASN.1 `SEQUENCE OF UTF8String` |

The server also supports `IdentFrame.ocsp_staple` (base64url DER OCSP) for stapled revocation responses.

> **Migration:** certificates issued under the old provisional `…99999` arc MUST be revoked and re-issued under PEN 65715.

## CA client & revocation artifacts (alpha.14)

alpha.14 added a typed remote client and tightened the revocation artifacts:

- **`NipCaClient`** — a typed remote client for this CA: CA discovery, CRL retrieval, Ed25519
  register / renew / revoke / verify, and RFC-0002 X.509 registration. SDK callers no longer
  hand-roll the HTTP surface.
- **`/v1/crl`** now includes an `issued_at` timestamp **and a detached CA signature** over the
  list, so relying parties can verify the CRL independently of the transport.
- **`/.well-known/nps-ca`** no longer advertises an unmapped `/ocsp` endpoint — the discovery
  document only lists routes the server actually serves.
- **`INipCaStore.ListAsync()`** was added to the store interface (with an `InMemoryNipCaStore`
  implementation) so backends can enumerate issued certificates for CRL assembly.

---

## ACME path (NPS-RFC-0002) — EXPERIMENTAL

The `agent-01` ACME challenge (RFC 8555 + NPS-RFC-0002) is enabled by setting `NIPCA__ACMEENABLED=true`. With PEN 65715 now assigned, the ACME path issues under the IANA arc rather than the former provisional OID. The ACME pipeline itself remains EXPERIMENTAL — review the RFC-0002 gate before relying on it in production.

---

## Configuration (env vars)

### Required

| Variable | Purpose |
|----------|---------|
| `NIPCA__CANID` | CA NID, e.g. `urn:nps:org:ca.example.com` |
| `NIPCA__KEYPASSPHRASE` | Passphrase for the encrypted CA key file (AES-256-GCM + PBKDF2 at rest) |
| `NIPCA__BASEURL` | Public HTTPS base URL of this CA |
| `CONNECTIONSTRINGS__POSTGRES` | Postgres connection string (required when using the default PostgreSQL store) |

### Optional

| Variable | Default | Purpose |
|----------|---------|---------|
| `NIPCA__DISPLAYNAME` | `NPS CA` | Human-readable CA name |
| `NIPCA__KEYFILEPATH` | `/data/ca.key.enc` | Encrypted CA key file path |
| `NIPCA__AGENTCERTVALIDITYDAYS` | `30` | Agent certificate validity window |
| `NIPCA__NODECERTVALIDITYDAYS` | `90` | Node certificate validity window |
| `NIPCA__RENEWALWINDOWDAYS` | `7` | Days before expiry that renewal opens |
| `NIPCA__NORMALIZEOCSPRESPONSETIME` | `true` | Round OCSP `producedAt` to the second |
| `NIPCA__OPERATORAPIKEY` | — | Bearer token required on all write endpoints; omit to disable auth (dev only) |
| `NIPCA__ALLOWEDCAPABILITIES` | — | Comma-separated capability allowlist; requests with unlisted capabilities are rejected with `403` |
| `NIPCA__ACMEENABLED` | `false` | Enable ACME RFC 8555 + `agent-01` challenge (NPS-RFC-0002, EXPERIMENTAL) |
| `NIPCA__ACMEPATHPREFIX` | `/acme` | HTTP route prefix for ACME endpoints |
| `NIPCA__MGMTPORT` | `17436` | Management port serving `/metrics` (and operational probes), separate from the public CA port |

---

## TLS

The container exposes plain HTTP on port 17435. Run it behind nginx, Caddy, or Traefik for TLS termination. `NIPCA__BASEURL` must point at the public HTTPS endpoint.

---

## `/health` response

Returns `200` with a standard NPS health envelope when the service is ready. Returns `503` during startup or if the CA key cannot be loaded. `/healthz` and `/readyz` provide Kubernetes-style liveness/readiness probes, backed by the transport-neutral `HealthProbeRenderer` (alpha.14) shared across the daemon set.

## Graceful shutdown

On `SIGTERM` the daemon performs a graceful shutdown with a 30 s drain window, allowing in-flight issuance/revocation requests to complete before the process exits.

---

## Multi-language client examples

The `example/` directory contains five reference client ports (Python, TypeScript, Java, Rust, Go). These are **educational only** — they are frozen at v1.0.0-alpha.2, not maintained, and not published. They demonstrate how to call the NIP CA HTTP API from each language. See `example/README.md` for the path to revive one as a maintained SDK port.

---

## Common operational issues

**CA key file missing on restart**

`NIPCA__KEYFILEPATH` points to a path that is not mounted to a persistent volume. The encrypted key file must survive restarts. Mount `/data` (or the configured key path) to a durable volume.

**Write endpoints return `401 Unauthorized`**

`NIPCA__OPERATORAPIKEY` is set but the request is not including `Authorization: Bearer <token>`. Add the header or clear the env var for dev environments.

**`403` on register with certain capabilities**

`NIPCA__ALLOWEDCAPABILITIES` is set and the requested capability is not in the allowlist. Either add the capability to the allowlist or remove the restriction.

---

## Cross-links

- [Daemon NPS-Cloud-CA](./Daemon-NPS-Cloud-CA.md) — the managed multi-tenant NPS Cloud equivalent (private, 2027 Q1+)
- [Protocol NIP](./Protocol-NIP.md) — NIP §8 defines the registration and certificate format
- [SDK Identity and Authentication](./SDK-Identity-and-Authentication.md) — how SDK clients call this CA

---

*Last reviewed at suite version: v1.0.0-alpha.19*
