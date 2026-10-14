# NPS alpha.19 Debt Inventory and Baseline Freeze

中文版本：[alpha19-debt-inventory.cn.md](alpha19-debt-inventory.cn.md)

**Status**: P19-0 baseline
**Created**: 2026-08-31
**Release target**: `v1.0.0-alpha.19`
**Tracking**: [NPS-Dev#91](https://github.com/labacacia/NPS-Dev/issues/91) · [ChangeControl EPIC-004](https://github.com/innolotus/ChangeControl/issues/4)

## 1. Purpose

Alpha.20 is expected to introduce substantial new design. Alpha.19 is therefore
the debt-closure and baseline-freeze release for the existing/pre-alpha.20 NPS
contract. It must close protocol, six-SDK, daemon, executable-conformance,
documentation-truth, and release-distribution debt before new design begins.

P19-0 freezes the inventory. Completing P19-0 does **not** mean the listed debt
is implemented; it means every candidate has a stable ID, evidence, owner,
disposition, downstream scope, and objective closure gate.

## 2. Debt test and dispositions

An item is alpha.19 debt when at least one condition holds:

1. it was promised for alpha.18 or an earlier completed milestone;
2. a current normative `MUST` lacks executable reference behavior;
3. an Implemented/capability/status claim lacks runtime or negative-path proof;
4. the six supported SDKs differ in current-contract behavior;
5. a daemon exposes a current contract but lacks required runtime, persistence,
   admission, or certification;
6. canonical/distributed source, fixtures, versions, or current docs have
   unexplained drift.

Allowed dispositions:

| Disposition | Meaning | What closes the item |
|---|---|---|
| **Implement** | Current behavior is missing or incomplete | Code/spec/docs plus executable acceptance evidence |
| **Correct claim** | Behavior exists, but current status/docs/matrices are false | Reconciled current docs/status plus evidence links |
| **Verify and close** | Behavior appears present but parity/coverage is unproven | Cross-runtime or distribution verification evidence |
| **Proven future** | The item was explicitly never part of the pre-alpha.20 contract | Normative/roadmap evidence, owner, and future target; no contradictory current claim |

Relabeling unresolved current behavior as “future” is not a valid closure.

## 3. Protocol and shared-contract register

| ID | Priority | Debt and source evidence | Disposition / owner | Closure evidence | Downstream |
|---|---|---|---|---|---|
| **P19-P01** | P0 | NCP 0.12 hardening was removed from alpha.18: runtime keepalive timers, deterministic timeout closure, QUIC migration/0-RTT/flow-control/backpressure, and idle/dead-peer/oversized-Nop vectors. See [`spec/NPS-Roadmap.md`](../spec/NPS-Roadmap.md). | Implement · NCP + six SDK owners | EN/CN NCP 0.12 delta; shared fixtures executed by all six SDKs; deterministic close reasons and timer tests | NPS, six SDK repos, ingress/daemons |
| **P19-P02** | P0 | NDP 0.13 persistence/recovery was removed from alpha.18: sequence/epoch fences across restart, partition, stale, equal-epoch split, loop, and recovery. | Implement · NDP + six SDK owners | EN/CN NDP 0.13 delta; persistent-store contract; shared restart/partition/fence fixtures pass in all six SDKs | NPS, six SDK repos, nps-registry |
| **P19-P03** | P0 | NOP 0.10 bounded replay/eviction, TTL, `weighted_first_k`, `merge_all`, and loss/reorder/duplicate/timeout behavior was removed from alpha.18. | Implement · NOP + six SDK owners | EN/CN NOP 0.10 delta and fault fixtures execute identically across six runtimes | NPS, six SDK repos, nps-runner |
| **P19-P04** | P0 | NWP resumable-subscription enforcement and portable stability/SLA/billing metadata were planned for alpha.18 but not delivered by the unrelated 0.21 bump. | Implement · NWP + six SDK owners | Normative delta; cancellation/resume/failure tests; metadata round trips and current docs in six SDKs | NPS, six SDK repos, Node profiles |
| **P19-P05** | P0 | NIP short-lived certificate renewal interoperability, fail-closed OCSP/CRL timeout/stale/unknown behavior, and Phase-3 rejection advisory were planned but not delivered by the unrelated 0.14 bump. | Implement · NIP/CA + six SDK owners | Renewal interoperability and revocation fault suites; deterministic advisory output; EN/CN parity | NPS, six SDK repos, NIP-CA-Server, ingress |
| **P19-P06** | P0 | `frame-registry.yaml` 0.15 and dependent version/overview/error/status surfaces moved with P19-P01..P03. | Implement · shared spec owner | Registry 0.15, version matrix, overview, errors/statuses, generated/constants checks and CN mirror agree | All required train repositories |

## 4. SDK and implementation-parity register

| ID | Priority | Debt and source evidence | Disposition / owner | Closure evidence | Downstream |
|---|---|---|---|---|---|
| **P19-S01** | P0 | Alpha.19’s standing rule requires behavior in .NET, Python, TypeScript, Go, Java, and Rust; DTO/catalog presence does not prove timers, persistence, expiry, replay, cancellation, admission, or recovery. | Implement/verify · six SDK owners | One behavior matrix keyed to P19-P01..P05; every required cell links an executable test and package surface | Six standalone SDK repos |
| **P19-S02** | P1 | The release wiki records no exported Go runtime `Version` constant and calls it a known gap, while Go already has a distribution `VERSION` file. | Implement or correct claim · Go owner | `core.Version` (or documented deliberate package-metadata API) with test; release wiki reconciled | NPS-SDK-Go, NPS/docs/handbook |
| **P19-S03** | P1 | Historical roadmap text says all SDKs lack NDP DNS TXT resolution, but current .NET/Python/TS/Go/Java/Rust source and tests contain DNS TXT lookup/parse/fallback paths. | Correct claim and verify · NDP/docs owners | Six-SDK DNS TXT test inventory recorded; historical row marked superseded; no current gap claim remains | NPS, wiki, six SDK docs |

## 5. Daemon register

| ID | Priority | Debt and source evidence | Disposition / owner | Closure evidence | Downstream |
|---|---|---|---|---|---|
| **P19-D01** | P0 | `nps-ingress` source contains TLS 1.3, ALPN `nps/1.0`, mTLS, inline session-NID binding, and proxying, while [`tools/daemons/nps-ingress/README.md`](https://github.com/labacacia/NPS-Dev/blob/main/tools/daemons/nps-ingress/README.md), health `todo`, and architecture text still describe parts as skeleton/future. | Correct claim · ingress/docs owner | Source/health/README/architecture/changelog agree; inline binding and half-close tests cited | NPS-Daemons, NPS/docs/handbook |
| **P19-D02** | P0 | Full applicable `TC-N2-Tls-*`, `TC-N2-*`, and `TC-N2-HA-*` evidence plus current-contract rate limit/auth/CGN/reputation/Anchor admission is not complete for ingress. | Implement · ingress + conformance owners | Executable family manifests; admission negative paths; no advertised unsupported capability | NPS-Daemons, Node L2 certification |
| **P19-D03** | P1 | `nps-runner` already implements portable OCI SpawnSpec execution and periodic lease renewal, but historical roadmap/status text still lists them as remaining. | Correct claim and verify · runner/docs owner | OCI/renewal tests cited in current docs; obsolete remaining-work claims removed or labeled historical | NPS-Daemons, NPS/docs/handbook |
| **P19-D04** | P0 | Runner lease/dedup state is in-process while current wording claims exactly-one-runner behavior; deployment L3 certification, crash/restart/reclaim, lease-loss cancellation, and terminal ownership need shared/durable proof. | Implement · runner owner | Multi-runner durable-store tests; crash/reclaim and lease-loss fault tests; L3 manifest | NPS-Daemons, NOP runtime docs |
| **P19-D05** | P0 | [`tools/daemons/npsd/README.md`](https://github.com/labacacia/NPS-Dev/blob/main/tools/daemons/npsd/README.md) and changelog identify native NCP transport as unfinished while the Node profile contains current NCP requirements. | Implement · npsd/NCP owner | Native preamble/handshake/close behavior and applicable Node tests pass; docs/status agree | NPS-Daemons, ingress integration |
| **P19-D06** | P0 | `npsd` inbox is in-memory, while `TC-N1-NWP-02` requires inbox persistence across restart; resident push remains documented future work. | Implement · npsd/NWP owner | Durable queue migration/restart/TTL/priority/ack tests and resident-delivery disposition; L1 manifest | NPS-Daemons, runner |
| **P19-D07** | P1 | `npsd` documents NDP AnnounceFrame emission and sub-NID renewal as unfinished. | Implement · npsd/NDP/NIP owner | Signed announce/liveness tests and renewal/revocation/restart tests; health/docs updated | NPS-Daemons, nps-registry |

## 6. Conformance register

| ID | Priority | Debt and source evidence | Disposition / owner | Closure evidence | Downstream |
|---|---|---|---|---|---|
| **P19-C01** | P0 | [`NPS-Node-L1.md`](../spec/services/conformance/NPS-Node-L1.md) defines 20 case headings (legacy count prose incorrectly said 21), while its reference-suite table still lists .NET planned and Python/TS TODO. SDK conformance catalogs are not executable certification. | Implement · Node conformance + runtime owners | Runnable L1 harness/manifests for every advertised implementation; all mandatory cases pass and optional `na` is legal | Daemons, SDK/release docs |
| **P19-C02** | P0 | [`NPS-Node-L2.md`](../spec/services/conformance/NPS-Node-L2.md) defines topology, TLS, bridge, and HA families but still lists executable Python/TS TODOs and ingress lacks full evidence. | Implement · Node conformance + daemon/SDK owners | Executable family manifests; family-level all-or-none rules enforced; applicable runtimes pass | Daemons, six SDKs |
| **P19-C03** | P0 | [`NPS-Node-Profile.md`](../spec/services/NPS-Node-Profile.md) still marks detailed L2 IDs and L2 suite TODO; L2-01..L2-07 are tracked only as follow-up work. | Implement current normative cases or prove future · profile owner | Each L2 requirement has stable ID, normative classification, case mapping, and implementation disposition; no unsupported L2 claim | Node profile and certification templates |
| **P19-C04** | P1 | L3 cases exist and runner claims Layer-3 behavior, but deployment-specific certification remains separate/unrecorded. | Implement applicable current contract or narrow claim · runner/profile owner | Explicit L3 claim boundary and executable manifest for claimed families | NPS-Daemons, release wiki |

## 7. Specification and documentation-truth register

| ID | Priority | Debt and source evidence | Disposition / owner | Closure evidence | Downstream |
|---|---|---|---|---|---|
| **P19-T01** | P0 | NPS-CR-0005 is `Implemented`, six SDKs contain RA policy/service/tests, yet NIP §8.1 still labels RA endpoints a `stub` and says bodies land when the CR reaches Implemented. | Correct normative text and verify · NIP owner | EN/CN NIP §8.1 matches the implemented contract; CR checklist/status and six-SDK/CA evidence reconcile | NPS, six SDKs, NIP-CA-Server |
| **P19-T02** | P0 | Accepted/Active RFC coverage matrices still contain `_TBD_`/`pending` cells despite later implementations, notably RFC-0001/0002/0003/0004. | Correct claim and verify · RFC owners | EN/CN matrices match source/tests; genuine missing cells become implementation items instead of stale pending text | NPS and wiki |
| **P19-T03** | P1 | Historical roadmap rows contain superseded “all SDKs missing” and daemon-skeleton statements without sufficient current-state labeling. | Correct claim · roadmap owner | Historical facts retained but superseded rows clearly link the closing release/evidence | Release roadmap and wiki |
| **P19-T04** | P0 | Daemon root/readme/architecture/health status spans skeleton-era and current implementations; `nps-registry`/`nps-ledger` descriptions also lag shipped SQLite/Merkle/gossip work. | Correct claim and verify · daemon/docs owners | One current capability matrix generated or mechanically checked against health/tests; EN/CN and release wiki agree | NPS-Daemons, ledger, wiki |
| **P19-T05** | P0 | The repository requires EN/CN changes together; alpha.19 adds normative and operational content across many surfaces. | Verify and close · documentation owners | Structural/semantic parity checks pass; no untranslated normative delta | NPS and wiki |
| **P19-T06** | P1 | Root `CLAUDE.md`, README/version tables and source-layout descriptions include stale version or phase language. | Correct claim · repository docs owner | Current-entry docs match `version-matrix.yaml`, actual source and release topology; consistency scripts pass | Contributor onboarding |

## 8. Release and distribution register

| ID | Priority | Debt and source evidence | Disposition / owner | Closure evidence | Downstream |
|---|---|---|---|---|---|
| **P19-R01** | P0 | NPS-Dev is the source of truth; Release, SDK and daemon repos must be materialized with deletion semantics and documented distribution-only exceptions. | Verify and close · release owner | Zero unexplained Dev→Release/SDK/daemon drift reports after materialization | All required train repos |
| **P19-R02** | P0 | Each standalone must carry the fixtures it actually executes; catalog-only or source-tree-only fixtures do not prove the package. | Implement/verify · release + SDK owners | Clean-checkout package tests execute vendored fixtures in every standalone | Six SDK repos |
| **P19-R03** | P0 | Release family invariants are eight Rust crates including `nps-conformance` and eleven NuGet packages, plus PyPI/npm/Maven/Go and image/release artifacts. | Verify and close · release owner | Machine-readable artifact inventory, package dry-runs, minimum-toolchain tests, dependency/security scans | Registries and release pages |
| **P19-R04** | P0 | Registry preflight must prove publish authority rather than anonymous read access; `version.yaml` is bumped last. | Verify and close · release owner | Permission checks recorded, secrets excluded, version oracle consistent, pre-release review green | All registries |
| **P19-R05** | P0 | Publishing is irreversible and is not authorized by implementation completion. | Enforce gate · release owner/user | Explicit approval recorded after pre-release review; tag/package/image/release verification logged | GitHub, Gitee, registries |

## 9. Proven-future and excluded register

These items are not alpha.19 debt unless new evidence shows a contradictory
current/pre-alpha.20 contract claim.

| ID | Excluded scope | Evidence/boundary | Re-entry rule |
|---|---|---|---|
| **P19-X01** | NIP Phase-3 flag day | Current roadmap explicitly targets beta.1 | Only by explicit Epic/design amendment |
| **P19-X02** | Multi-region NPS Cloud CA, production HSM, cross-CA trust | Roadmap Phase 3 / 2027 Q1+ | Separate Cloud milestone/RFC |
| **P19-X03** | NPS 1.0 freeze and standards work | Beta/Phase 4 roadmap | Separate stabilization/standards plan |
| **P19-X04** | C++ and PHP promotion | Explicitly de-scoped placeholder SDKs since alpha.13 | Source + tests plus explicit supported-SDK decision |
| **P19-X05** | NPS Studio and NPS-NWP-Manager completion | Intentional planned/stub product repositories | Separate product Epic/milestone |
| **P19-X06** | New alpha.20 protocol/product design | Product decision: alpha.19 is debt closure | Alpha.20 design record |
| **P19-X07** | Retired compat-ingress v0.2 feature TODOs | Compat ingress packages left the mandatory train at alpha.18 in favor of NWP Bridge | Re-enter only if a supported current consumer is proven |
| **P19-X08** | Tag/package/image publication during implementation | Separate approval required | Successful P19 pre-release review plus explicit approval |

## 10. Repository ownership map

| Workstream | Canonical owner | Materialized/affected repositories |
|---|---|---|
| Specs, shared fixtures, six implementations | `labacacia/NPS-Dev` | NPS and six SDK repos |
| Public daemon bundle | `labacacia/NPS-Dev/tools/daemons` | `labacacia/NPS-Daemons` |
| CA distribution | `labacacia/NPS-Dev/tools/nip-ca-server` | `labacacia/NIP-CA-Server` |
| Ledger/Cloud CA distributions | NPS-Dev daemon source | `labacacia/NPS-Ledger`, `labacacia/NPS-Cloud-CA` as required |
| Public release documentation | NPS-Dev/NPS | `NPS/docs/handbook` |
| Cross-repository governance | ChangeControl EPIC-004 | Product issues/PRs link the Epic |

## 11. Product issue map

| Tracking issue | Inventory scope |
|---|---|
| [#91](https://github.com/labacacia/NPS-Dev/issues/91) | P19-0 master inventory and baseline freeze |
| [#92](https://github.com/labacacia/NPS-Dev/issues/92) | NCP: P19-P01, P19-P06 (NCP portion), P19-S01 |
| [#93](https://github.com/labacacia/NPS-Dev/issues/93) | NDP: P19-P02, P19-S01, P19-S03 |
| [#94](https://github.com/labacacia/NPS-Dev/issues/94) | NOP: P19-P03, P19-S01 |
| [#95](https://github.com/labacacia/NPS-Dev/issues/95) | NWP/NIP: P19-P04, P19-P05, P19-S01, P19-T01 |
| [#96](https://github.com/labacacia/NPS-Dev/issues/96) | Daemons: P19-D01–P19-D07, P19-C04, P19-T04 |
| [#97](https://github.com/labacacia/NPS-Dev/issues/97) | Conformance: P19-S01, P19-C01–P19-C04, P19-T02, P19-T05 |
| [#98](https://github.com/labacacia/NPS-Dev/issues/98) | Release/docs: P19-S02, P19-T02–P19-T06, P19-R01–P19-R05 |

An ID may appear in more than one issue when implementation and independent
conformance/documentation closure are both required. Issue #91 owns additions
or changes to the frozen inventory; the scoped issues own delivery evidence.

## 12. P19-0 completion gate

P19-0 is complete when:

- every inventory row has a stable ID, evidence, owner, disposition, closure
  evidence, and downstream scope;
- every implementation item is represented in the alpha.19 work graph and
  product issue tracking;
- every proven-future item has a non-contradictory boundary and re-entry rule;
- EN/CN inventory structure and meaning agree;
- the alpha.19 roadmap links this inventory and places P19-0 before spec/runtime
  implementation;
- future discoveries append new IDs rather than silently changing the meaning
  of an existing ID.

No alpha.19 implementation item is considered complete by this document alone.
