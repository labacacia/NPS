# ADR: Rebuild the NPS repository topology

- Status: Accepted
- Date: 2026-10-14
- Decision owners: LabAcacia NPS maintainers

## Context

The LabAcacia organization accumulated source repositories, release mirrors,
product experiments, compatibility packages, placeholders, and a separate Wiki
mirror. Several names described an implementation detail rather than the
repository's current role. The suite version manifest also treated an external
consumer repository as a required NPS release target.

This made it difficult to answer three basic questions: where a change starts,
which repositories must move together for a release, and which repositories are
independent products rather than suite artifacts.

## Decision

1. `labacacia/NPS-Dev` remains the private authoring source of truth for the
   protocol specifications, shared implementation sources, conformance assets,
   and release tooling.
2. `labacacia/NPS` is the public suite entry point, documentation site, version
   oracle, and reviewed projection of the specification. It was renamed from
   `NPS-Release` because it is the canonical public project, not a transient
   release staging area.
3. The six language SDK repositories, `NPS-Daemons`, and `NIP-CA-Server` are
   release projections. They remain in the coordinated suite release train.
4. `NPS-Model-Runtime`, `NPS-Console`, and `NPS-Paper` are independent
   lifecycle repositories. They consume NPS but do not gate the suite version.
5. The MCP, A2A, and gRPC ingress repositories are archived. Their supported
   replacement boundary is `LabAcacia.NPS.NWP.Bridge`; their history remains
   readable for migration and provenance.
6. `NPS-Cloud-CA` and `NPS-Ledger` are archived until each has a real,
   independently deployed GA boundary. Their current source continues to live
   in `NPS-Dev`.
7. Maintained Wiki content moves into `NPS/docs/handbook`. The standalone
   `NPS-Release-Wiki` mirror is archived after migration, and the GitHub Wiki is
   disabled to prevent two documentation authorities.
8. Empty placeholder repositories `NPS-SDK-CPP`, `NPS-SDK-PHP`, and
   `NPS-Studio` are removed. Complete Git bundles were verified before deletion.
   A repository will be recreated only when implementation, ownership, and a
   release path exist.
9. `orilynn-studio/nps-orchestrator` is an external consumer. It is removed
   from the required suite version matrix and from release-coherence checks.
10. `NPS-Examples` is archived because its projects reference private monorepo
    paths and retired ingress packages. It may return only as a self-contained,
    package-based example repository.

## Naming changes

| Previous name | Current name | Reason |
| --- | --- | --- |
| `NPS-Release` | `NPS` | Canonical public suite entry point |
| `NPS-LLM-Framework` | `NPS-Model-Runtime` | Describes the runtime boundary without tying it to a generic framework label |
| `NPS-NWP-Manager` | `NPS-Console` | Product scope extends beyond a single protocol and is an operator console |

Runtime protocol names, package coordinates, and wire identifiers are not
renamed by this decision.

## Alternatives considered

- Keep every repository and document the differences. Rejected because empty
  placeholders and redundant documentation surfaces still create false signals.
- Collapse every project into the monorepo. Rejected because public release
  artifacts and independently deployed products need their own lifecycle and
  access boundaries.
- Make every downstream consumer part of the release train. Rejected because a
  consumer owned outside LabAcacia must not block publishing the protocol suite.

## Security and recovery

- Deleted repositories were resolved explicitly and backed up as complete,
  verified Git bundles before remote deletion.
- Archived repositories remain read-only and preserve issues, tags, and history.
- No package coordinate, protocol token, or wire-level identifier changes as a
  consequence of repository renaming.
- The Console remains private and is explicitly classified as a non-production
  prototype until outbound request controls, authentication, and persistence are
  designed and reviewed.

## Consequences

- Contributors have one authoring source and one public suite entry point.
- Release automation has a smaller, organization-owned required set.
- Historical links to renamed GitHub repositories continue to redirect, while
  maintained documentation uses the current names.
- Reintroducing an archived or removed repository requires a new decision with
  a concrete owner, boundary, tests, and release or deployment plan.

## SDK projection input clarification — 2026-11-03

D20-08 implements the existing six SDK release boundaries with
`tools/release/sdk-ownership-map.json`. Each projection includes its language-owned
source, tests, build manifests and dependency locks, SDK documentation, and the
shared LICENSE/NOTICE/conformance assets. Root build files and locks participate in
Assertion J; source parity must not silently ignore dependency or build changes.
`prepare-sdk-projections.py` copies only tracked inputs from a clean authoring
commit into a fresh directory outside Git worktrees, with file hashes and source
provenance. This is an isolated qualification input, not synchronization or
publication. Actual release synchronization still needs reviewed drift and the
release workflow. Repository roles, the three independent lifecycles and archived
repositories above are unchanged.
