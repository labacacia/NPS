# Alpha.19 Current Status

> **Released:** 2026-09-06 after the complete NPS-Dev alpha.19 debt-closure
> train through [PR #122](https://github.com/labacacia/NPS-Dev/pull/122).
>
> **Release boundary:** **v1.0.0-alpha.19 is published.** The six SDKs are
> available from their registries, and source releases exist for the daemon
> bundle and `nip-ca-server`. The project does not publish container images;
> daemon images continue to be built locally from the tagged source.

This page records the released alpha.19 boundary and the explicit non-claims
that remain after debt closure. Alpha.18 remains available as the previous
release, but new installations should pin alpha.19.

## Released protocol set

| Protocol | Previous alpha.18 | Released alpha.19 | Release delta |
|---|---|---|---|
| NCP | 0.11 | **0.12 Proposed** | Runtime keepalive timers, deterministic timeout closure, QUIC migration/0-RTT/flow-control/backpressure policy and shared fault vectors |
| NWP | 0.21 | **0.22 Proposed** | NWM normalization, renewable subscription lease/SLA/billing metadata and portable failure behavior |
| NIP | 0.14 | **0.15 Proposed** | Renewal boundaries, signed CRL/OCSP freshness, fail-closed revocation and Phase-3 advisory behavior without the flag day |
| NDP | 0.12 | **0.13 Proposed** | Durable sequence/epoch recovery, restart/partition fencing and registry fault behavior |
| NOP | 0.9 | **0.10 Proposed** | Bounded replay/eviction, TTL, aggregation and loss/reorder/duplicate/timeout behavior |

The normative release was frozen in
[PR #100](https://github.com/labacacia/NPS-Dev/pull/100). RFC-0001 through
RFC-0005 are Active, RFC-0006 is Accepted, and CR-0011 is Implemented in the
current source record from [PR #114](https://github.com/labacacia/NPS-Dev/pull/114).
Deferred compatibility transitions, the NIP Phase-3 flag day, QUIC v2 and other
explicit alpha.20/future work are not activated.

## Six-SDK release

| SDK | Published package | Alpha.19 evidence |
|---|---|---|
| .NET | 1.0.0-alpha.19 | Executes all 47 shared P19-1 hardening vectors |
| Python | 1.0.0a19 | Executes all 47 shared P19-1 hardening vectors |
| TypeScript | 1.0.0-alpha.19 | Executes all 47 shared P19-1 hardening vectors |
| Go | v1.0.0-alpha.19 | Executes all 47 shared P19-1 hardening vectors |
| Java | 1.0.0-alpha.19 | Executes all 47 shared P19-1 hardening vectors |
| Rust | 1.0.0-alpha.19 | Executes all 47 shared P19-1 hardening vectors |

The runtime parity implementation and evidence are in
[PR #101](https://github.com/labacacia/NPS-Dev/pull/101), with final package
materialization and version synchronization in PRs #121 and #122. C++ and PHP
remain placeholders and are not part of the six-SDK claim.

## Daemon release

| Daemon | Alpha.19 implemented boundary | Explicit non-claim |
|---|---|---|
| `npsd` | Unified HTTP/native NCP admission; durable SQLite inbox/ack/TTL/priority; managed sub-NID renewal; signed ephemeral NDP Announce with restart-stable key/sequence | Resident/hybrid push and full Node L1 certification are not claimed |
| `nps-runner` | Portable OCI SpawnSpec/reference resolution; durable shared-file SQLite leases and terminal dedup; restart fencing/reclaim; lease-loss cancellation | Full TaskFrame DAG/Saga L3 certification and generic cross-host filesystem guarantees are not claimed |
| `nps-ingress` | TLS 1.3 native NCP, ALPN `nps/1.0`, default-on mTLS, inline certificate/session-NID binding, bounded admission and full-duplex proxying; four real-socket TLS cases | It is transport-IUT evidence, not full Node L2 certification; product auth, billing and broad DDoS controls are not advertised transport capabilities |
| `nps-registry` | SQLite Announce/Resolve/Graph, highest-epoch Anchor resolution and federated cluster-tuple ingest; live npsd signed-announcement integration | It does not cryptographically validate every AnnounceFrame against an IdentFrame and does not provide replicated-database HA |
| bundle overlay | Four standalone source trees, Docker contexts, built-in health probes and compose wiring reconcile with NPS-Dev | Images are built locally from the tagged source; no registry image is published |

Daemon closure is reviewed across
[PRs #102–#110](https://github.com/labacacia/NPS-Dev/pull/110). The current
case-scoped evidence remains deliberately narrower than certification:

- npsd enumerates all **20** Node L1 cases, with incomplete required cases still
  visible;
- nps-ingress records the four executed `TC-N2-Tls-*` cases without a full L2
  claim;
- nps-runner records ten strict L3 cases with partial/unexecuted cases visible;
- Node L2 is v0.7 with **38** cases, including explicit L2-01..L2-07
  dispositions, but no deployment is certified merely by catalog presence.

## Documentation truth

The current CR/RFC matrices and question dispositions are reviewed in
[PR #114](https://github.com/labacacia/NPS-Dev/pull/114); superseded roadmap
statements are time-bounded in
[PR #115](https://github.com/labacacia/NPS-Dev/pull/115). This Wiki page does
not replace those machine-readable ledgers. It summarizes them for readers of
the alpha.19 release Wiki.

## Release verification

English/Chinese parity is reconciled in NPS-Dev PR #116 and NPS PR
#16. The Go runtime-version gap is closed by the tested `core.Version` API.
Post-merge source-of-truth, version synchronization, security, package-shape
and six-language test gates passed before publication. GitHub and Gitee tags
and prerelease pages were verified after publication, together with NuGet,
PyPI, npm, Maven Central, crates.io, the authenticated Nexus feed and the Go
module proxy.

> Last reviewed at suite version: v1.0.0-alpha.19
