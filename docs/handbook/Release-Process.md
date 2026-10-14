# Release Process

**Status:** ✅ Reviewed for v1.0.0-alpha.19

This page documents how an NPS suite release is prepared and published. The process is designed around a single-oracle version model: one file is authoritative, and all other files must match it.

---

## Single-Source-of-Truth Model

NPS-Dev is the authoring source for everything. Distribution repos (`labacacia/NPS-SDK-Python`, `labacacia/NPS-SDK-TypeScript`, `labacacia/NPS-Daemons`, etc.) receive one-way sync pushes from NPS-Dev via the scripts in `tools/release/`. **Never make breaking changes directly in a distribution repo** — they will be overwritten on the next sync.

### The Single Oracle Rule

`NPS/version.yaml` — specifically the `suite_version` field — is **the one machine-readable source of truth for the NPS suite version**. The CI tooling (`check-version-sync.py`, `check-source-of-truth.py`) reads the version from this file and only this file.

Rules:
1. Bumping `suite_version` here is the **last step** of a release.
2. No other file (CHANGELOG, README, csproj, `pyproject.toml`, etc.) may serve as the version oracle. Those files MUST match this value, not the other way around.
3. `CHANGELOG.md` files are the **human notification medium** — they must be updated as part of the same release commit but are never consulted by CI for the authoritative version.

---

## No Per-Protocol Versioning

Any protocol or package change bumps the **whole-suite version uniformly**. Per-package or per-protocol sub-versions are forbidden.

This rule was codified after the alpha.5.2 incident, where some distribution repos were tagged at `1.0.0-alpha.5` while others had `1.0.0-alpha.5.1`. The drift caused operator confusion about which repos were in sync. Assertion B in `check-source-of-truth.py` now guards against this by verifying that all SDK manifests match `suite_version`.

---

## Version Schema

| Type | Format | When used |
|------|--------|-----------|
| Alpha release | `1.0.0-alpha.N` | Every alpha milestone, including hotfixes and re-cuts |

**Alpha has no sub-versions.** Since alpha.6 the suite policy is to advance `1.0.0-alpha.N` → `1.0.0-alpha.N+1` for *every* release, including hotfixes and re-cuts. The old `1.0.0-alpha.N.M` hotfix format (e.g. `1.0.0-alpha.5.1`) was retired after the alpha.5.2 drift incident — there is no `alpha.5.x` going forward. A patch on top of an alpha simply becomes the next whole alpha.

The current latest released suite version is **v1.0.0-alpha.19** (released 2026-09-06). All releases bump the suite-wide version uniformly. There are no partial hotfixes that touch only one repo.

---

## Release Sequence

The full release follows these steps in order:

1. **Bump impl/ package versions.** Update all language SDK manifests in NPS-Dev: `pyproject.toml`, `package.json`, `Cargo.toml`, `gradle.properties`, all `.csproj` files under `impl/dotnet/src/`.

2. **Bump daemon source csproj versions.** Update the `<Version>` element in each daemon's source csproj (`tools/daemons/*/\*.csproj`).

3. **Bump publish-overlay csproj versions.** Update the `<Version>` element in each daemon's `publish-overlay/*.csproj` to match. (Assertion A checks this parity.)

4. **Update docker-compose image tags.** Update `image: labacacia/<name>:VERSION` lines in `tools/daemons/bundle-overlay/docker-compose.yml` and in `nps-orchestrator/docker-compose.yml` (external repo, resolved via `--ext-root`).

5. **Update all CHANGELOG.md files.** Each sub-project has its own `CHANGELOG.md`; the umbrella `CHANGELOG.md` in NPS-Dev also receives a `## [<version>]` entry. Sub-project changelogs tracked by Assertion D: all daemons, `tools/nip-ca-server/`, and the monorepo root.

6. **Update spec version numbers** if any spec files changed in this release. Bump the `**Version**:` field in the relevant `spec/NPS-*.md` files and update the tables in `spec/NPS-0-Overview.md` and in `CLAUDE.md`.

7. **Bump `NPS/version.yaml` `suite_version`.** This is the last write step in NPS-Dev before running CI.

8. **Run CI — Assertions A–E must all pass** (Assertion E is warn-only; A–D are hard failures):
   - **Assertion A:** Each daemon source csproj `<Version>` equals its publish-overlay `<Version>`
   - **Assertion B:** All SDK manifests (`pyproject.toml`, `package.json`, `Cargo.toml`, `gradle.properties`, all `.csproj` files) match `suite_version`
   - **Assertion C:** All `image: labacacia/<name>:VERSION` lines in docker-compose files match `suite_version`
   - **Assertion D:** Every CHANGELOG.md listed in `source-allowlist.yaml` contains a `## [<suite_version>]` entry
   - **Assertion E:** README banner drift scan (warn-only — CI never fails on E alone)

9. **Run sync scripts** for each distribution repo:
   - `tools/release/sync-nps-daemons.sh` → `labacacia/NPS-Daemons` (+ Gitee mirror)
   - `tools/release/sync-nip-ca-server.sh` → `labacacia/NIP-CA-Server` (+ Gitee mirror)
   - `tools/release/sync-nps-cloud-ca.sh` → `labacacia/NPS-Cloud-CA` (private)
   - `tools/release/sync-nps-ledger.sh` → `labacacia/NPS-Ledger` (private)
   - SDK sync scripts for each of the six language SDK distribution repos
   - *(No longer run since alpha.18)* Ingress sync scripts for `NPS-MCP-Ingress`, `NPS-A2A-Ingress`, `NPS-gRPC-Ingress` — the three compat ingress repos were last published at v1.0.0-alpha.16 (an alpha.17 deprecation release was prepared but never published) and left the synchronized release train at alpha.18; they are marked `expected: skip` in `version.yaml`

10. **Tag the release in NPS.** Create a `v{suite_version}` git tag in `labacacia/NPS`. The tag is the public release marker.

### Required Env Vars for Sync Scripts

| Variable | Used by |
|----------|---------|
| `GITHUB_TOKEN` | All sync scripts — PAT with `repo` scope on the target labacacia/innolotus repos |
| `GITEE_TOKEN` | Sync scripts that mirror to Gitee — PAT with `projects` scope |

Use `DRY_RUN=1` to execute all steps except the final pushes. Use `SKIP_GITEE=1` to push only to GitHub and skip Gitee mirroring.

---

## Drift-Prevention CI: Assertions A–E

The CI script `tools/scripts/check-source-of-truth.py` reads `NPS/version.yaml` (via `--suite-version`) and verifies five assertions:

| Assertion | What it checks | Failure mode |
|-----------|---------------|--------------|
| A | Each daemon source csproj `<Version>` == its publish-overlay `<Version>` | Hard fail — blocks release |
| B | All SDK manifests match `suite_version` | Hard fail — the alpha.5.2 incident motivator |
| C | docker-compose image tags match `suite_version` | Hard fail |
| D | Each CHANGELOG.md in `assertion_d_changelog_entries` contains `## [<suite_version>]` | Hard fail |
| E | README banners don't contain stale version strings | Warn only — never blocks |

The list of files checked by Assertions A–D is defined in `tools/scripts/source-allowlist.yaml`.

---

## Hotfix Flow

Under the no-sub-version policy, a hotfix advances to the **next whole alpha** (e.g. `1.0.0-alpha.12` → `1.0.0-alpha.13`) and follows the same sequence as any full release. There are **no partial hotfixes** — every distribution repo must be bumped and synced together. A hotfix that touches only one SDK still requires bumping all other SDK manifests (even if their content is unchanged) so that Assertion B passes.

### Worked example: alpha.13 re-cut

alpha.16 is the most recent example of the same policy from the other direction: a release train that had been prepared as an alpha.15 refresh found the `1.0.0-alpha.15` version already occupied on the public registries, so it advanced to **alpha.16** rather than reusing or sub-versioning the taken number.

alpha.13 is a real example of a re-cut release. **alpha.12 was withdrawn** because it shipped a vulnerable `MessagePack 3.0.300` dependency (NU1903) together with a native-mode handshake bug. Rather than publishing an `alpha.12.1` sub-version (which the policy forbids), the suite advanced to **alpha.13** with `MessagePack 3.1.7`, superseding the withdrawn alpha.12 entirely. `version.yaml` remained the single oracle throughout — `suite_version` was bumped straight to `1.0.0-alpha.13`.

---

## Past Incident: alpha.5.2 Version Drift

During the transition from alpha.5 to alpha.5.1, some repos were synced with version `1.0.0-alpha.5` while others received `1.0.0-alpha.5.1`. This caused operator confusion about which packages were compatible.

Root cause: the sync scripts were invoked in a non-atomic order and one was re-run with an old version value.

Fix: Assertion B in `check-source-of-truth.py` now blocks the release if any SDK manifest does not match `suite_version`. The `version.yaml` bump is now explicitly the last authoring step (step 7 above), ensuring CI catches drift before any sync scripts run.

---

## Related Pages

- [Repository Topology](./Repository-Topology.md) — the full map of repos and sync-script assignments
- [Contributing Guide](./Contributing-Guide.md) — PR conventions and CHANGELOG requirements

---

*Last reviewed at suite version: v1.0.0-alpha.19*
