# NPS mirror, runtime and observability compatibility decisions

Status: accepted
Decision: EPIC-005-COMPAT-010
Decision: EPIC-005-COMPAT-011
Date: 2026-09-01
Owner: NPS source-of-truth and release maintainers

## Context

GitHub repositories use the canonical names recorded by ProjectsControl.
Existing Gitee mirrors, OCI image names, daemon executables, service names,
configuration keys, data paths, metric names and logger categories use
lowercase operational identities established before repository migration.
Those runtime coordinates are consumed by release automation, Compose files,
operator scripts, dashboards and alerts; they are not product repository
names.

At decision time 11 of the 13 configured Gitee targets resolve through
`git ls-remote`; `NPS-grpc-ingress` and `NPS-examples` were unavailable or
timed out. Their target slots remain explicit so a transient mirror outage does
not silently change identity. Public Docker registry availability could not be
confirmed, so image names are treated as deployment contracts proven by the
source and release manifests, not claimed as publicly retrievable artifacts.

## Decision — Gitee mirror slugs

- GitHub source repositories use canonical names such as `NPS-SDK-Python` and
  `NIP-CA-Server`.
- Gitee targets keep their existing exact slugs such as `NPS-sdk-py`,
  `NPS-sdk-ts`, `nip-ca-server` and `nps-daemons`.
- Mirror automation must keep an explicit GitHub-source to Gitee-target map;
  it must not derive the target by lowercasing or rewriting a canonical name.
- A missing mirror is an operational incident, not authorization to select a
  new slug automatically.

## Decision — daemon and image coordinates

- Existing executable, service, image, container, configuration and data-path
  names remain supported, including `npsd`, `nps-runner`, `nps-ingress`,
  `nps-registry`, `nip-ca-server`, `nps-cloud-ca`, `nps-ledger`,
  `nps-anchord`, `NPS_*`, `NIPCA_*` and their documented paths.
- Repository display casing does not flow into runtime coordinates.
- New daemon coordinates must follow the existing protocol/component naming
  scheme and must not create aliases solely to resemble repository names.

## Observability namespace decision

Prometheus metric names and structured logger categories are operator-facing
APIs. The current production set includes:

- `nps_active_connections` and `nps_frames_processed_total`;
- `nip_ca_certs_issued_total`, `nip_ca_certs_revoked_total` and
  `nip_ca_csr_rejected_total`;
- `nps-registry.announce` and `nps-registry.federation` logger categories.

These names use canonical protocol/component runtime identities and do not
encode retired repository names. They remain stable. The owning daemon or
protocol component owns new metric/logger names; renaming requires dashboard,
alert, log-query and retention-window migration with dual emission where
practical.

The organization-wide semantic review found nine old-product-family matches in
observability files owned by Phase 4 repositories; they are code/logger type
families already retained by their accepted repository decisions. Three Banyan
Knowledge observability matches remain governed by EPIC-003/Phase 5. No new
Phase 4 rename is required.

## Alternatives considered

1. Rename mirrors, images and telemetry to repository casing — rejected because
   it breaks automation, deployments, dashboards and alerts without changing
   product semantics.
2. Publish old and new operational identities indefinitely — rejected because
   it fragments monitoring and release authority.
3. Keep stable operational identities with explicit source/target mappings and
   ownership — accepted.

## Consequences and removal gate

Operators must use exact documented technical spellings. A future rename
requires mirror/image/runtime consumer inventory, dual sync or dual publish,
configuration/data migration, dashboard and alert migration, compatibility and
rollback tests, a published support window and major-version approval.

These mirror, runtime and observability identifiers have no scheduled removal
date.
