# Repository Topology

NPS uses one private authoring repository and a deliberately small set of public
release projections.

## Authoring and release train

| Repository | Role | Release relationship |
| --- | --- | --- |
| `labacacia/NPS-Dev` | Private source for specifications, implementations, fixtures, and release tooling | All coordinated changes start here |
| [`labacacia/NPS`](https://github.com/labacacia/NPS) | Public suite entry, documentation, specifications, and version oracle | Required |
| Six `NPS-SDK-*` repositories | Language-specific package distributions | Required |
| [`NPS-Daemons`](https://github.com/labacacia/NPS-Daemons) | Public daemon bundle | Required |
| [`NIP-CA-Server`](https://github.com/labacacia/NIP-CA-Server) | Standalone self-hosted CA | Required |

The suite release train contains only those repositories. An application that
uses NPS is not automatically a release target.

## Independent projects

- [`NPS-Model-Runtime`](https://github.com/labacacia/NPS-Model-Runtime) —
  incubating NPS-aware model runtime; private.
- [`NPS-Console`](https://github.com/labacacia/NPS-Console) — incubating operator
  console; private and not production-ready.
- [`NPS-Paper`](https://github.com/labacacia/NPS-Paper) — research paper and
  supporting material.

These projects consume published NPS interfaces and follow their own versioning.
The external `orilynn-studio/nps-orchestrator` repository is also a consumer and
does not gate an NPS release.

## Archived boundaries

- `NPS-MCP-Ingress`, `NPS-A2A-Ingress`, and `NPS-gRPC-Ingress` are superseded by
  the NWP Bridge packages.
- `NPS-Cloud-CA` and `NPS-Ledger` have no independent GA boundary; their source
  remains in `NPS-Dev`.
- `NPS-Examples` is archived until its examples use public packages and run
  without private monorepo paths.
- The former `NPS-Release-Wiki` content now lives in this handbook.

Empty C++, PHP, and Studio placeholder repositories were removed on 2026-10-14
after verified Git bundle backups were created.

## Flow

```text
NPS-Dev
  ├─ spec/ ───────────────────────────> NPS
  ├─ impl/{six languages}/ ───────────> six SDK repositories
  ├─ tools/daemons/ ──────────────────> NPS-Daemons
  └─ tools/nip-ca-server/ ────────────> NIP-CA-Server

published specifications and SDKs ───> independent projects and consumers
```

Normative details always come from `NPS/spec`; the handbook explains how to use
and operate the suite.

> Last reviewed at suite version: v1.0.0-alpha.19
