# Daemon: nps-cloud-ca

**Status:** ✅ Latest published package — v1.0.0-alpha.19

> **Audience:** NPS Cloud subscribers and operators
> **Distribution note:** `labacacia/NPS-Cloud-CA` is a **private** repository. This page documents only the protocol-visible interface. Internal product and billing details are in the private repo.
> **Source-of-truth precedence:** `spec/` documents in [`labacacia/NPS`](https://github.com/labacacia/NPS/tree/main/spec) win over this page if they disagree.

`nps-cloud-ca` is the NPS Cloud Certificate Authority — the multi-tenant, billing-aware NID CA operated by INNO LOTUS PTY LTD for NPS Cloud subscribers. It issues NID certificates for agents and nodes across organisations, handles CRL/OCSP, and integrates with the full ACME workflow defined in NPS-RFC-0002.

- **Source:** `NPS-Dev/tools/daemons/nps-cloud-ca/`
- **Distribution:** `labacacia/NPS-Cloud-CA` — **PRIVATE** (NPS Cloud product)
- **Docker image:** `labacacia/nps-cloud-ca:1.0.0-alpha.19` — a local build tag only. No image is pushed to any registry, private or public; subscribers with repository access build it from the repo `Dockerfile` (`docker build -t labacacia/nps-cloud-ca:1.0.0-alpha.19 .`).
- **Default port:** `:17435` (NIP optional-dedicated per NPS-3 §1)
- **Layer:** L3
- **Timeline:** Ships publicly with NPS Cloud GA, planned 2027 Q1+

---

## nps-cloud-ca vs nip-ca-server

| Aspect | nps-cloud-ca | nip-ca-server |
|--------|--------------|---------------|
| Tenancy | Multi-tenant — each subscriber has isolated NID namespace | Single-tenant — one organisation per deployment |
| Billing | Integrated with NPS Cloud billing | None |
| SLA | Commercial SLA (NPS Cloud agreement) | Best-effort OSS |
| Distribution | Private (`innolotus` org) | Public (`labacacia` org) |
| Availability | NPS Cloud GA (2027 Q1+) | Available now |
| Use for self-hosting today | No — not yet available | Yes — see [Daemon NIP-CA-Server](./Daemon-NIP-CA-Server.md) |

**For any self-hosted CA need today, use [Daemon NIP-CA-Server](./Daemon-NIP-CA-Server.md).**

---

## Implementation status (alpha.18)

The current release is a **Phase 1 deferral skeleton**. The URL surface is present but all issuance endpoints return `NIP-CA-NOT-READY` (HTTP 503) with a pointer to the OSS CA so callers fail informatively. The process name, port, and Docker image tag are stable from alpha.3 to lock in the deployment surface.

The daemon's own X.509 and ACME pipeline remains planned for a future release alongside NPS-RFC-0002.

> **CA client/CRL alignment (alpha.14).** The shared NIP CA surface gained a typed remote
> client (`NipCaClient`: CA discovery, CRL retrieval, Ed25519 register/renew/revoke/verify,
> RFC-0002 X.509 registration) and revocation-artifact changes that this daemon's protocol
> surface tracks: `/v1/crl` now carries an `issued_at` timestamp plus a detached CA signature,
> and the `/.well-known/nps-ca` discovery document no longer advertises an unmapped `/ocsp`
> entry. See [Daemon NIP-CA-Server](./Daemon-NIP-CA-Server.md) for the reference OSS implementation.

---

## Operator API (protocol-visible interface)

These endpoints define the public protocol surface. Internal product behaviour (billing hooks, tenant isolation, rate limits) is documented in the private repo.

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/nid/issue` | Issue a NID for an agent or node. Requires valid subscriber credentials. Body shape: NIP §5 NID issuance request. |
| `DELETE` | `/v1/nid/{nid}/revoke` | Revoke a NID. Body: `{reason?}`. |
| `GET` | `/v1/nid/{nid}/status` | Query the status of a NID (active / revoked / expired). Returns the current `IdentFrame` metadata. |
| `GET` | `/v1/crl` | Certificate Revocation List for all NIDs issued by this CA instance. |
| `GET` | `/v1/ocsp` | OCSP responder endpoint. |
| `POST` | `/v1/orchestrators/groups/{group}/register` | CR-0003: register an orchestrator group; mints a `group-`-prefixed NID. |
| `DELETE` | `/v1/orchestrators/groups/{group}/revoke` | CR-0003: revoke a group NID (cascades to children via `parent_revoked`). |
| `POST` | `/v1/orchestrators/groups/{group}/sessions/issue` | CR-0003: issue a `session-`-prefixed NID under a group, recording `lineage`. |
| `GET` | `/v1/orchestrators/groups/{group}/sessions` | CR-0003: list active session NIDs for a group. |
| `GET` | `/.well-known/nps-ca` | CA discovery document (NID, public key, policy URL). |
| `GET` | `/health` | Standard NPS health envelope (legacy). |
| `GET` | `/healthz` | Kubernetes-style liveness probe (added alpha.6+). |
| `GET` | `/readyz` | Kubernetes-style readiness probe (added alpha.6+). |

> **CR-0005 RA model:** registration may flow through a Registration Authority — bootstrap tokens admit a node into a pending-registration queue that an operator approves before issuance. See [Daemon NIP-CA-Server](./Daemon-NIP-CA-Server.md) for the shared RA mechanics.

> **IANA PEN 65715:** all NPS X.509 OIDs anchor to the IANA-assigned arc `1.3.6.1.4.1.65715` (CR-0004, assigned 2026-05-08), replacing the provisional `1.3.6.1.4.1.99999` arc. Issued certificates carry `id-nps-node-roles` (`65715.2.2`) and `id-nps-capabilities` (`65715.2.3`); `IdentFrame.ocsp_staple` (base64url DER OCSP) is supported.

> **Metrics port:** `/metrics` is exposed on the **management port 17436**, never on the public CA port 17435.

---

## `/health` response example

```json
{
  "status": "ok",
  "daemon": "nps-cloud-ca",
  "version": "1.0.0-alpha.19",
  "layer": 3,
  "role": "NPS Cloud NID Certificate Authority",
  "port": 17435
}
```

During the Phase 1 skeleton period, the health endpoint returns `200 ok` while issuance endpoints return `503 NIP-CA-NOT-READY`.

---

## Configuration (env vars)

| Variable | Default | Purpose |
|----------|---------|---------|
| `NPSCLOUDCA_PORT` | `17435` | Public CA port to bind. NIP optional-dedicated per NPS-3 §1. Does **not** expose `/metrics`. |
| `NPSCLOUDCA_MGMT_PORT` | `17436` | Management port. Hosts `/metrics` (and operational probes) separately from the public CA port. |
| `NPSCLOUDCA_HOST` | `0.0.0.0` | Bind address. |

On `SIGTERM` the daemon performs a graceful shutdown with a 30 s drain window before exiting.

Full configuration (tenant database, billing integration, CA key management) is documented in the private `labacacia/NPS-Cloud-CA` repository.

---

## Cross-links

- [Daemon NIP-CA-Server](./Daemon-NIP-CA-Server.md) — the OSS self-hosted CA equivalent; available today
- [Protocol NIP](./Protocol-NIP.md) — Neural Identity Protocol; NID certificate format and issuance semantics
- [Daemon NPS-Ledger](./Daemon-NPS-Ledger.md) — the companion private reputation log daemon

---

*Last reviewed at suite version: v1.0.0-alpha.19*
