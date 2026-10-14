# Daemon: bundle-overlay

**Status:** ✅ Latest published package — v1.0.0-alpha.19

> **Alpha.19 release:** all four standalone daemon
> trees, Docker contexts, built-in non-root health checks, compose wiring and
> canonical repository names reconcile with NPS-Dev. Candidate image builds
> were local validation only; no image registry publication occurred. See
> [Alpha.19 Current Status](./Alpha19-Current-Status.md).

> **Audience:** Operators and release shepherds
> **Source-of-truth precedence:** `spec/` documents in [`labacacia/NPS`](https://github.com/labacacia/NPS/tree/main/spec) win over this page if they disagree.

`bundle-overlay` is not a runtime daemon. It is the distribution-packaging meta-layer that produces the public `labacacia/NPS-Daemons` repository. It contains the top-level `docker-compose.yml`, `README.md`, `CHANGELOG.md`, and `NOTICE` that aggregate across the four OSS daemons. Operators who clone `labacacia/NPS-Daemons` are consuming the output of this layer.

- **Source:** `NPS-Dev/tools/daemons/bundle-overlay/`
- **Distribution:** the root of `labacacia/NPS-Daemons` (public)
- **Sync script:** `NPS-Dev/tools/release/sync-nps-daemons.sh`

---

## Contents

| File | Role |
|------|------|
| `docker-compose.yml` | Top-level compose file; four services (`npsd`, `nps-runner`, `nps-ingress`, `nps-registry`), each with a `build:` context pointing at its daemon subdirectory plus an `image:` tag `labacacia/<name>:VERSION` naming the locally built result |
| `README.md` | Public-facing README for `labacacia/NPS-Daemons`; describes the three-layer architecture and quick-start instructions |
| `CHANGELOG.md` | Aggregated daemon changelog; single place for operators to track all daemon changes across releases |
| `NOTICE` | Copyright and license attribution |

---

## docker-compose.yml (at v1.0.0-alpha.19)

```yaml
services:

  npsd:
    build:
      context: ./npsd
      dockerfile: Dockerfile
    image: labacacia/npsd:1.0.0-alpha.19
    restart: unless-stopped
    ports:
      - "127.0.0.1:17433:17433"
    volumes:
      - npsd-data:/data
    environment:
      NPSD_HOST: 0.0.0.0
      NPSD_PORT: 17433
      NPSD_DATA_DIR: /data

  nps-runner:
    build:
      context: ./nps-runner
      dockerfile: Dockerfile
    image: labacacia/nps-runner:1.0.0-alpha.19
    restart: unless-stopped
    depends_on:
      - npsd

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

  nps-registry:
    build:
      context: ./nps-registry
      dockerfile: Dockerfile
    image: labacacia/nps-registry:1.0.0-alpha.19
    restart: unless-stopped
    ports:
      - "${NPS_REGISTRY_PORT:-17436}:17436"

volumes:
  npsd-data:
```

Key notes:

- Every service pairs `build:` with `image:`. Because a `build:` context is present, the
  `image:` value is simply the **tag Compose applies to the image it builds from source** —
  it is not a registry coordinate. The project publishes no container images, so
  `docker pull labacacia/npsd:…` will fail; bring the stack up with
  `docker compose up -d --build` instead.
- `npsd` binds host-side to `127.0.0.1` (loopback only) even though the container uses `0.0.0.0`. Public ingress goes through `nps-ingress`.
- `nps-runner` has no port mappings — it has no HTTP surface.
- Ingress and registry host ports are configurable via `NPS_INGRESS_PORT` and `NPS_REGISTRY_PORT` at compose launch time.

---

## Image-tag-vs-suite_version invariant

Every image tag in `docker-compose.yml` must equal the suite version oracle. CI Assertion C enforces this:

- CI reads the suite version from the oracle (e.g. `1.0.0-alpha.19`).
- It scans every `image:` line in `docker-compose.yml` for tags.
- It fails if any tag does not match the oracle.

This catches the common mistake of updating one daemon's image tag while forgetting to update the others, or bumping the suite version without updating the compose file. The assertion runs as part of the release CI gate before any push to `labacacia/NPS-Daemons`.

---

## Publish-overlay model

The monorepo uses a two-level publish-overlay pattern:

```
NPS-Dev/tools/daemons/
├── bundle-overlay/          ← root-level files for labacacia/NPS-Daemons
│   ├── docker-compose.yml   ← references all four daemons
│   ├── README.md
│   └── CHANGELOG.md
└── npsd/
│   ├── publish-overlay/     ← npsd-specific files for its slot in the bundle
│   │   ├── NpsdPublish.csproj
│   │   ├── Dockerfile
│   │   └── nuget.config
│   └── ...
└── nps-runner/
    ├── publish-overlay/     ← runner-specific overrides
    └── ...
```

`sync-nps-daemons.sh` first copies `bundle-overlay/` to the root of `labacacia/NPS-Daemons`, then overlays each daemon's `publish-overlay/` into the corresponding subdirectory. The result is a self-contained distribution repo where each subdirectory is buildable independently.

---

## Aggregated CHANGELOG

`bundle-overlay/CHANGELOG.md` is the operator-facing change log for the entire `labacacia/NPS-Daemons` distribution. It aggregates entries from each of the four daemon subdirectory CHANGELOGs into a single chronological list, so operators do not need to check four separate files to understand what changed in a release.

The per-daemon `CHANGELOG.md` files remain the source of truth for individual daemon changes. The aggregated file is regenerated by `sync-nps-daemons.sh` at release time.

---

## Release history note: alpha.12 withdrawn

The current bundle pins the **alpha.19** daemon set, built against `MessagePack 3.1.7`. **alpha.12 was withdrawn** before general use: its NuGet packages shipped the vulnerable `MessagePack 3.0.300` (NU1903). **alpha.13 superseded it**, rebuilding the daemon set against `MessagePack 3.1.7` (carried forward through alpha.19); operators must skip alpha.12 entirely and pin `1.0.0-alpha.19`.

---

## What is NOT in the bundle

The two private trust-anchor daemons are excluded:

- `nps-cloud-ca` → published separately to `labacacia/NPS-Cloud-CA`
- `nps-ledger` → published separately to `labacacia/NPS-Ledger`

Operators who need those daemons must have NPS Cloud access. See [Daemon NPS-Cloud-CA](./Daemon-NPS-Cloud-CA.md) and [Daemon NPS-Ledger](./Daemon-NPS-Ledger.md).

---

## Cross-links

- [Operator Quickstart Bundle](./Operator-Quickstart-Bundle.md) — end-to-end quickstart using this docker-compose
- [Release Process](./Release-Process.md) — how `sync-nps-daemons.sh` and the publish-overlay model work
- [Daemon NPSd](./Daemon-NPSd.md) — npsd details
- [Daemon NPS-Runner](./Daemon-NPS-Runner.md) — nps-runner details
- [Daemon NPS-Ingress](./Daemon-NPS-Ingress.md) — nps-ingress details
- [Daemon NPS-Registry](./Daemon-NPS-Registry.md) — nps-registry details
- [Operator Daemons Reference](./Operator-Daemons-Reference.md)

---

*Last reviewed at suite version: v1.0.0-alpha.19*
