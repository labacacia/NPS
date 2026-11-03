English | [中文版](./CHANGELOG.cn.md)

# Changelog

All notable changes to the **NPS (Neural Protocol Suite)** project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Until NPS reaches v1.0 stable, every repository in the suite — spec, SDKs (.NET / Python / TypeScript / Java / Rust / Go), CA Servers, and compat bridges — is synchronized to the same pre-release version tag.

## [1.0.0-alpha.20] — Release candidate, unpublished

- Bounded experimental result resources/authorization, repeated-schema batches, local task handoff and volatile node transfer. Language support differs; full six-SDK runtime parity is not qualified.
- Preserve measured comparisons with equivalent HTTPgzip and ordinary tools; no universal token, fee or speed benefit is claimed.
- Align candidate versions and local CI/package verification. Full production, beta and actual publication remain separate gates.

## [1.0.0-alpha.19] — 2026-09-05

### Fixed

- Bounded superseded roadmap claims as dated snapshots and linked their closing
  releases/current evidence. The alpha.6 DNS TXT, alpha.7 reputation-client,
  alpha.5/alpha.17 daemon-skeleton/remaining-work and provisional-PEN records
  remain historical without presenting themselves as current gaps.
- Reconciled CR/RFC lifecycle, six-SDK coverage matrices and proposal-era
  question notes against current source. RFC-0001/0002/0003 are Active,
  RFC-0006 is consistently Accepted, CR-0011 is consistently Implemented, and
  an executable ledger prevents `_TBD_`/`pending` coverage from returning.
- Restored current-contract AaaS L2-01..L2-07 to the Node L2 suite instead of
  deferring them as future work. Suite v0.7 now has 38 cases; all six SDK
  catalogs and validators require L2-01..05 and allow L2-06/07 `na` only with
  a reasoned exception message.
- Aligned all six SDK conformance manifest emitters with Node L2 v0.6 and its
  31-case catalog. Validators now reject stale profile versions, incorrect
  summaries, partial-family `na`, and invalid single-/multi-Anchor HA
  applicability combinations.
- Made every advertised repository implementation evidence artifact runnable
  and scope-complete: npsd now enumerates all 20 L1 cases, ingress records its
  complete four-case TLS family, runner enumerates all ten L3 cases, and a
  machine-readable registry prevents a profile claim from losing its manifest.
- Corrected the Node L1 suite count to its 20 actual case headings, aligned the
  Node L2 runtime catalog and manifest version with all 31 v0.6 cases, and
  added a machine-readable evidence inventory that distinguishes executable,
  partial, catalog-only, and reference-IUT-not-applicable coverage without
  claiming certification.
- Replaced `nps-runner` process-local leases with durable SQLite coordination:
  atomic cross-process claims, process-instance fencing, expired-lease reclaim,
  restart-persistent terminal dedup, lease-loss worker cancellation, and
  terminal-before-ack ordering now have executable evidence. A case-level
  manifest explicitly withholds full L3 certification until the TaskFrame
  DAG/Saga deployment cases run.
- Fixed `nps-runner` production dependency injection and both monorepo and
  standalone Docker builds; CI now runs the daemon-owned runner test suite.

## [1.0.0-alpha.18] — 2026-08-15

### Added

- Added official NWP LLM usage telemetry (`input_tokens`, `output_tokens`,
  prefix/KV-cache hit, reused tokens, and evaluated tokens) plus unary
  `CapsFrame.request_id` correlation. Native NWP server helpers now echo request
  IDs consistently across all six SDKs; `CapsFrame.cached` is explicitly kept
  distinct from model prefix/KV-cache reuse (NPS-Dev#88).
- Added the candidate NPS-CR-0011 / NWP 0.21 stateful LLM context contract:
  owner-bound opaque context IDs, create/append/fork/reset/status/release,
  compare-and-swap versions, atomic unary/async cancellation semantics, NWM 0.2
  discovery, NIP 0.14 `llm:context` authorization, deterministic errors, and
  shared lifecycle/replay/restart/accounting vectors (NPS-Dev#90). Stateless
  completion remains compatible and stateful requests never silently fallback.
  All six reference servers implement stateful NDJSON streaming with atomic
  terminal-frame commit, abnormal-termination abort, and completed-sequence
  idempotent replay under a fresh `stream_id`.
- Added `NPS-LIMIT-RESOURCE` for bounded live-object limits and
  `LlmUsageDto.wire_input_bytes` for decoder-boundary request measurement.
- Added six-SDK black-box integration coverage for stateful LLM reconnect and
  lost-response recovery, single-winner concurrent append/CAS behavior, and
  process-local context loss after a server restart.
- Added a strict-native CR-0011 second-turn benchmark using the official
  MessagePack `ActionFrame` decoder. The deterministic gate verifies delta-only
  role/tool semantic parity with fallback disabled and separately reports lower
  decoder `wire_input_bytes` and runtime `evaluated_tokens`.

### Fixed

- Made all six stateful LLM Action coordinators fail closed when no deployment
  authorizer is configured, and pass the exact admission/commit capability set
  (`llm:complete` + `llm:context`, plus stream/tool capabilities when used) to
  that authorizer.
- Made the 19 shared CR-0011 conformance vectors fixture-driven in all six SDKs:
  tests now execute and validate each vector's `input`, `pre_state`, and
  `expected` contract instead of using the fixture only as an ID dispatch list.
- Aligned stateful LLM streaming across all six Action Server implementations:
  `stream=true` is incompatible with async acknowledgement, context receipts
  appear only on successful terminal chunks, failed or incomplete streams are
  aborted and never cached, and successful retries replay the exact sequence
  under a new server-owned stream identifier.
- Fixed the .NET NativeAOT MessagePack resolver for nullable `UInt64`, restoring
  Tier-2 round trips for NDP `AnnounceFrame.cluster_epoch`; optional primitive
  coverage now also exercises null and populated graph latency values
  (NPS-Dev#89).
- Derived the TypeScript runtime `VERSION` export from `package.json` so package
  bumps cannot leave the runtime banner behind.
- Lowered the Go SDK support floor to Go 1.23 and pinned the newest compatible
  `golang.org/x/crypto` line, with the minimum toolchain covered by the release
  gate.
- Upgraded Java MessagePack to 0.9.11, Jackson to 2.18.9, and BouncyCastle to
  1.84, clearing all runtime dependency advisories reported by the alpha.18
  OSV gate.

### Release Engineering

- Corrected the synchronized-release runbooks for the alpha.18 train: retired
  compatibility ingress packages are no longer required; Rust publishes eight
  crates including `nps-conformance`; standalone materialization deletes stale
  source and vendors conformance fixtures; Maven has a Python `zipfile`
  fallback; npm and crates.io preflight checks prove publish capability.
- Added a publish-and-run NativeAOT codec smoke gate and aligned the NuGet
  workflow/family checker to the 11-package alpha.18 SDK family.

## [1.0.0-alpha.17] — 2026-08-02

### Added

- Ported the .NET reference server and orchestration surface into Python, TypeScript, Java, Rust, and Go: native NCP transport, NWP Action/Complex/Memory Nodes and bidirectional bridges, NIP CA and full verification, NOP orchestration, daemon observability, and telemetry.
- Added NativeAOT-safe .NET frame codecs and publish validation for the official package family.
- Added portable, language-neutral profiles and shared conformance vectors for
  NCP 0.11 native-server handshakes, NWP 0.20 Node/Bridge serving, NIP 0.13
  CA/revocation behavior, NDP 0.12 registry admission, and NOP 0.9
  orchestration/runtime decisions; all six SDKs execute the same fixtures.
- Completed the CR-0007 shared implementation gate and added portable OCI
  SpawnSpec/reference resolution, lease renewal, lifecycle enforcement, and
  daemon-owned runtime vectors to `nps-runner`.

### Fixed

- Upgraded vulnerable Rust transport and PKI dependencies, the TypeScript test toolchain, and the minimum Go toolchain to 1.26.5; language-specific dependency audits now report no actionable vulnerabilities.
- Extended the source-of-truth checker with a narrow allowlist for documented retired identifiers so protocol history no longer creates false release blockers.
- Preserved standalone runner/ingress test access in publish overlays, restored Ledger's standalone SQLite dependency, and removed the vulnerable bundled SQLite runtime from daemon distributions.
- Made `check-version-sync.py` honor `expected: skip` so frozen or retired repositories are excluded cleanly from later release trains.
- Corrected portable NWP method rejection so `/.nwm` advertises `Allow: GET`,
  and added the missing shared cross-language vector.
- Rejected malformed NDP `graph_seq`, `ttl`, and Bridge protocol declarations
  consistently across all six SDKs without advancing registry sequence fences.
- Hardened remote runner SpawnSpec retrieval against private-address SSRF,
  redirect bypass, and DNS rebinding; lease loss no longer produces a stale
  terminal write or inbox acknowledgement.
- Made Debian daemon packaging independent of the caller's `umask` by
  normalizing `DEBIAN/` control-directory permissions before `dpkg-deb`
  validation.

### Release Engineering

- Prepared synchronized alpha.17 candidates for the protocol spec, all six SDK distribution repositories, daemon overlays, and package metadata without tagging or publishing.
- Prepared one final deprecated alpha.17 build of the MCP, A2A, and gRPC
  compatibility ingress packages; consumers should migrate to
  `LabAcacia.NPS.NWP.Bridge`, and alpha.18 removes these packages from the
  required synchronized train.

## [1.0.0-alpha.16] — 2026-07-23

### Fixed

- Re-issued the suite as alpha.16 because alpha.15 packages already exist on public registries.
- Fixed nps-daemons distribution test isolation so nps-ingress and nps-runner no longer compile test sources into application projects, while preserving internal test coverage via explicit test assembly visibility.

## [1.0.0-alpha.15] — 2026-06-28

### Added

- **NWP LLM/Thinking Profile**: NWM now defines `profiles.llm` for model-serving Action/Complex Nodes, with official .NET DTOs/helpers for model descriptors, streaming/tool support, privacy hints, and reasoning-disclosure policy. NIP v0.11 registers the standard `llm:*` capability strings for discovery and authorization.
- **NWP HTTP binding error registry**: NWP v0.17 / `error-codes.md` v1.6 add canonical codes for HTTP overlay transport rejections (`Origin`, `Content-Type`, `Accept`, request-id echo mismatch, unparseable frame bodies) plus advertised-but-unimplemented capability rollout windows, closing NPS-Dev#84.
- **NCP Tier-3 BinaryVector**: implemented `binary_vector.v1` across the SDK source trees and distribution repos, including malformed-frame/client-error conformance coverage.
- **Inbound NWP Bridge server adapters**: added MCP and A2A server-side Bridge adapters with ASP.NET hosting, JSON-RPC framing helpers, tool/agent-card metadata, and local action dispatch.

### Breaking Alpha Wire Changes

- **NIP TrustFrame/RevokeFrame signed payload realignment**: the signed payload now includes the current NPS-3 fields (`issued_at`, `serial`, `signer_nid`, `target_nid`) and uses the current revocation status/error naming (`NIP-CERT-REVOKED`). Signed frames produced by the old alpha.14-era SDK shape will no longer verify after upgrading.
- **NDP Announce signed canonical form realignment**: SDKs now sign the same canonical Announce body (`signature`/`health`/`last_seen`/`frame` excluded, null optionals omitted, `heartbeat_interval_ms` defaulted to `60000` only when absent, and explicit `0` signed literally as disabled). Old signed announcements that depended on the divergent per-SDK forms may fail cross-SDK verification.
- **NDP graph/revoke guard enforcement**: SDKs now enforce GraphFrame node/edge bounds and NIP revoke parent rules consistently, so malformed frames that previously slipped through are rejected with the documented protocol errors.

### Hardening

- Hardened inbound NWP Bridge server dispatch with required caller verification, bounded request bodies, dispatch timeouts, and sanitized client errors.
- Hardened native-mode parsing so malformed Tier-3 BinaryVector frames return documented client errors instead of server-internal failures, and Go native serving avoids shared negotiated-tier mutation.

### Release Engineering

- Synchronized the Dev source-of-truth into Release/spec, standalone SDK source trees, ingress/tool distribution repos, README banners, package manifests, lockfiles, and current alpha.15 version metadata.

## [1.0.0-alpha.14] — 2026-06-26

### .NET SDK

- **NuGet package family release hardening**: alpha.14 packs the full public .NET family atomically (`NPS.Core`, `NWP`, `NWP.Anchor`, `NWP.Bridge`, `NIP`, SQLite/PostgreSQL NIP storage, `NDP`, `NOP`, `Daemon.Observability`, `NPS.Conformance`, and MCP/A2A/gRPC ingress packages), emits `.snupkg` symbol packages, and validates the package + symbol set before pushing to Nexus or NuGet.org.
- **`NPS.NWP.Bridge` concrete adapters**: Bridge Node is no longer a type-only package. It now ships `IBridgeDispatcher`, `BridgeDispatcherRegistry`, a host-independent `BridgeNode`, ASP.NET `AddBridgeNode` / `UseBridgeNode`, and built-in HTTP/HTTPS, gRPC JSON unary, MCP JSON-RPC, and A2A JSON-RPC dispatchers. Outbound Bridge calls include endpoint allowlisting plus private/loopback SSRF guards by default.
- **Bridge target schema documentation**: added the canonical `bridge_target` schema for HTTP, gRPC JSON unary, MCP JSON-RPC, and A2A JSON-RPC dispatch.
- **Core codec DX**: `NpsFrameCodec` gained default factories and an instance `Peek(...)` helper alongside `PeekHeader(...)`.
- **Daemon observability decoupling**: health/readiness rendering is available through transport-neutral `HealthProbeRenderer`, while ASP.NET endpoint helpers reuse the same renderer.
- **NIP TrustFrame open boundary**: docs now distinguish open-source basic pinned-grantor validation from managed NPS Cloud federation, and `TrustFrameValidator` provides local validation helpers for open consumers.
- **Banyan integration security fixes**: `NipIdentVerifier` can consult a live revocation callback or `INipCaStore`, OCSP transport errors now fail closed by default, `NcpServer` supports a pre-preamble authenticated stream hook for TLS/mTLS, and native-mode Hello reads are bounded by timeout and payload size.
- **NIP CA revocation artifacts**: `/v1/crl` now includes `issued_at` plus a detached CA signature, `/.well-known/nps-ca` no longer advertises an unmapped `/ocsp` URL, and `INipCaStore` gained `ListAsync()` with a built-in `InMemoryNipCaStore`.
- **Typed remote NIP CA client**: `NipCaClient` covers CA discovery, CRL retrieval, Ed25519 registration/renewal/revocation/verification, and RFC-0002 X.509 registration flows for .NET consumers.
- **Native-mode NWP serving**: `NwpNativeNodeServer` lets Memory and Action Nodes serve `QueryFrame` and `ActionFrame` traffic directly over an `NcpSession` or native NCP stream, without hand-rolling the frame loop.
- **Conformance harness package**: new `LabAcacia.NPS.Conformance` package ships Node L1/L2 case catalogs, run manifests, and validation helpers for CI self-certification.
- **Local dev stack**: added a Docker Compose dev stack for loopback `npsd`, development NIP CA, and compat ingress smoke testing.

### Maintenance

- Centralized warning policy for alpha-stage XML documentation gaps and explicit NuGet audit suppression for `SQLitePCLRaw.lib.e_sqlite3` until an upstream fixed package exists.
- Updated .NET SDK quickstarts, package tables, and release-process docs for alpha.14.

## [1.0.0-alpha.13] — 2026-06-13

> **Supersedes the withdrawn `1.0.0-alpha.12`.** alpha.12 NuGet packages shipped early
> with a vulnerable `MessagePack 3.0.300` (NU1903) and a native-mode handshake bug, before
> the release review completed; they are deprecated. alpha.13 is the corrected release with
> the same feature set plus the security/handshake fixes.

### SDKs — functional parity (all six official SDKs)

- **Anchor Node server + CGN meter + Bridge types + reputation-policy evaluator** ported from the .NET reference into Python / TypeScript / Go / Java / Rust. The five non-.NET SDKs are promoted from client-only to **client + server**. Server bindings: Python = pure ASGI (zero dep), TypeScript = Web-standard `fetch(Request)→Response`, Go = `net/http` stdlib, Java = JDK `com.sun.net.httpserver`, Rust = framework-agnostic `handle()`. Each Anchor server is wire-verified against that language's existing `AnchorNodeClient`. Added a missing `nwp` error-codes module to TS/Go/Java/Rust and `NWP-CGN-LIMIT-EXCEEDED` + the three `NWP-REPUTATION-*` codes where absent.

### Spec

- **NCP v0.8** — **NPS-RFC-0006** promoted Draft → Proposed (native-mode transport binding). New §7.5: suite-wide ALPN `nps/1.0` (supersedes provisional `ncp/1`); TLS-wrapped framing mandated for native-mode-over-TCP (STARTTLS prohibited); mutual TLS with NIP certificates + session-NID binding (`NCP-NID-MISMATCH`); TLS 1.3 session-resumption tickets. Gates the `nps-ingress` (L2) daemon.
- **NWP v0.14** — new §16 Bridge Node Conformance: formal MUST/SHOULD requirements + canonical `bridge_target` round-trip test vectors (http / grpc / mcp) all six SDKs must round-trip identically. No new error codes.
- **NIP v0.10** — new §6.1 short-lived / renewable cert profile for edge mTLS (1–24 h validity, renewal window, ACME `agent-01` self-renewal, OCSP-staple interaction, resumption-ticket binding). Ties to `nps-ingress` mTLS termination. No new error codes.
- **NDP v0.9** — AnnounceFrame gains `health` (`healthy`/`degraded`/`draining`) + `last_seen` liveness fields; new §3.2.1 resolve-time staleness — Registry MUST return `NDP-RESOLVE-STALE` when `(last_seen ?? timestamp) + ttl` is in the past rather than serve a dead endpoint.
- **NOP v0.7** — **NPS-CR-0007** (NOP ↔ L3 runtime integration): §8 task-claim protocol (atomic lease + `dedup_key`, `NOP-CLAIM-CONFLICT`); `spawn_spec_ref` SpawnSpec content schema (OCI image / command / env / resource_limits); idle/max-runtime enforcement (`NOP-RUNTIME-IDLE-TIMEOUT` / `NOP-RUNTIME-MAX-RUNTIME`); idempotent result reporting; `NOP-SPAWN-SPEC-INVALID`. New `services/conformance/NPS-Node-L3.md` (`TC-N3-*`). Gates the `nps-runner` L3 FaaS runtime.

### Daemons (L2/L3)

- **nps-runner (L3)** — NPS-CR-0007 §4 task-claim lease protocol (`LeaseStore`): atomic per-task lease + `dedup_key`, `NOP-CLAIM-CONFLICT` on contention, expired-lease reclaim, terminal-node dedup; wired into the inbox dispatch. 8 unit tests.
- **nps-ingress (L2)** — NPS-RFC-0006 §6 native-mode TLS terminator (`NcpTlsListener`): ALPN `nps/1.0` over TLS 1.3, mutual TLS with NIP-certificate validation + session-NID binding (`NCP-NID-MISMATCH`), proxy to the local backend. 5 unit tests.

---

## [1.0.0-alpha.11] — 2026-05-28

### Spec

- **NCP v0.7** — `max_concurrent_streams` negotiation via HelloFrame/CapsFrame (uint16, default 32; overflow → `NCP-STREAM-LIMIT-EXCEEDED`); QUIC stream mapping (one bidirectional stream per NCP channel, HelloFrame on stream 0); rekeying at 2^32 frames or 24 h (`EXT rekey: true`, `NCP-REKEY-REQUIRED`); mid-stream ErrorFrame MAY→MUST.
- **NWP v0.13** — §13 SubscribeFrame formal specification (CR-0006 Accepted): `subscription_id` UUID v4, QueryFrame-compatible filter, `heartbeat_interval_ms`, `max_events`, opaque `cursor` for lossless resume. `topology:subscribe` SHOULD→MUST in §12.4. NWM `trust_anchors` (CA NID URN array). `bridge_target` schema standardized (`protocol`, `endpoint`, `headers`).
- **NIP v0.9** — `IdentFrame.ocsp_staple` (base64url DER OCSP response); X.509 extension OID table: `id-nps-node-roles` (65715.2.2, ASN.1 SEQUENCE OF UTF8String), `id-nps-capabilities` (65715.2.3); Phase 3 flag day at v1.0.0-beta.1 (`NIP-OCSP-STAPLE-EXPIRED` error code).
- **NDP v0.8** — GraphFrame (0x32) rewritten to §5 topology-snapshot format: `graph_id`, `nodes` (nid/cluster_anchor/node_roles), `edges` (from_nid/to_nid/latency_ms/protocol), `ttl`, `metadata`; max 256 nodes / 1024 edges (`NDP-GRAPH-INVALID`, `NDP-GRAPH-TOO-LARGE`). §9 federation forwarding: `public-federated` registries MUST forward AnnounceFrames; `ndp-forwarded-by` loop detection max 3 hops; `NDP-FEDERATION-LOOP`.
- **NOP v0.6** — AlignStream ack/NAK protocol (`window_size=16`, `ack_seq`/`nak_seq`, `NOP-STREAM-NAK`); `weighted_first_k` + `merge_all` aggregate strategies; DelegateFrame `target_cluster_anchor` for cross-cluster routing; webhook HMAC signing (`callback_secret`, `X-NPS-Signature: sha256=…`, `NOP-CALLBACK-HMAC-MISSING`).
- **CR-0006** (Accepted 2026-05-28) — SubscribeFrame §13 formal spec.
- **RFC-0006** (Draft) — NCP native-mode transport binding: TCP length-prefix framing, QUIC stream mapping, `max_concurrent_streams`, rekeying protocol.

### SDKs

- **All six SDKs** (Python / TypeScript / Go / Java / Rust / .NET) updated to alpha.11 feature set: NOP AlignStream ack/NAK (`ack_seq`/`nak_seq`); NOP aggregate strategies (`weighted_first_k`, `merge_all`); NOP `DelegateFrame.target_cluster_anchor`; NIP `IdentFrame.ocsp_staple`; NIP `id-nps-capabilities` OID constant (65715.2.3); NDP GraphFrame §5 format (`GraphNode`, `GraphEdge`); NWP `SubscribeFrame` CR-0006 format (`subscription_id`, `filter`, `heartbeat_interval_ms`, `max_events`, `cursor`); NWP `MemoryNodeOptions.trust_anchors`; NWM `trust_anchors` emission.

### .NET SDK

- **`NPS.NWP.Anchor` — `AnchorNodeOptions.TrustAnchors`**: CA NID URN array spliced into the `/.nwm` JSON response (NWP v0.13 §4.1). Receivers validate each URN starts with `urn:nps:`.
- **`NPS.NWP` — `SubscribeFrame` CR-0006 rewrite**: Old wire format (`action`, `stream_id`, `heartbeat_interval` in seconds, `resume_from_seq`) replaced by CR-0006 format (`subscription_id`, `filter`, `heartbeat_interval_ms`, `max_events`, `cursor`). **Breaking wire change** vs alpha.8–10; no transitional alias.
- **`NPS.NIP` — `IdentFrame.OcspStaple`**: New nullable field for base64url DER OCSP response (NIP v0.9 §8.2).
- **`NPS.NIP.X509` — `NpsX509Oids.IdNpsCapabilities`**: New constant `1.3.6.1.4.1.65715.2.3` for the agent capability set extension (NIP v0.9 §8.2).
- **`NPS.NOP` — AlignStream ack/NAK**: `AlignStreamFrame` gains `AckSeq` / `NakSeq` (NOP v0.6). `TaskFrame` gains `CallbackSecret` (HMAC webhook signing). `DelegateFrame` gains `TargetClusterAnchor`.
- **`NPS.NDP` — GraphFrame §5**: `NdpGraphNode` rewritten (nid / cluster_anchor / node_roles); `NdpGraphEdge` added (from_nid / to_nid / latency_ms / protocol); `GraphFrame` rewritten (graph_id / nodes / edges / ttl / metadata).
- **`NPS.NOP.Models` — `AggregateStrategy`**: New constants `WeightedFirstK` and `MergeAll`.

### Daemons

- **nps-ledger v1.0.0-alpha.11** — `POST /v1/log/federation/push`: batch reputation-entry ingestion from peer ledgers; `X-NPS-Forwarded-By` header loop detection (NDP §9, max 3 hops, `NDP-FEDERATION-LOOP`).
- **nps-probe v0.2** — Check 5: NWM `trust_anchors` validation (NWP v0.13 §4.1).
- **nps-orchestrator v1.0.0-alpha.11** — version bump.
- **NPS-NWP-Manager v0.1** — initial runnable stub: `GET /health`, `GET /v1/nodes?base_url=` (NWM fetch + in-memory cache), `GET /v1/nodes/list`.

---

## [1.0.0-alpha.10] — 2026-05-28

### Spec

- **NPS-CR-0005 — NIP CA Registration Authority (RA) model**: Three-tier admission control — `allowlist` (pre-approved NIDs), `bootstrap-token` (one-time `nps-bootstrap-*` tokens, SHA-256 hashed), `pending-queue` (operator-approval workflow). NIP CA Server gains `db/003_ra_model.sql` migration adding `nip_bootstrap_tokens` and `nip_pending_registrations` tables.

### SDKs

- **NOP saga compensation** across all six SDKs: `CompensationPolicy` type, `DagNode.compensate_action` / `compensate_params_mapping` fields, `TaskState` COMPENSATING / COMPENSATED states.
- **NDP security profiles** across all six SDKs: `SecurityProfile` enum — `LOCAL_DEV` / `ORG_PRIVATE` / `PUBLIC_FEDERATED`; ephemeral TTL cap enforced for LOCAL_DEV.
- **`IdentReputationPolicyHint`** type added across all six SDKs.
- **`IdentMetadata`** type added across all six SDKs (structured metadata attached to IdentFrame).

### .NET SDK

- **`NPS.NIP` — RA three-tier enrollment**: `NipCaOptions.EnrollmentTier`, `BootstrapTokenMaxTtl`, `AllowedNids`. `IBootstrapTokenStore` / `IPendingStore` interfaces + in-memory implementations. RA management endpoints (`POST /v1/ra/bootstrap-tokens`, `GET /v1/ra/pending`, `POST /v1/ra/pending/{id}/approve`, `POST /v1/ra/pending/{id}/reject`). Apply `db/003_ra_model.sql` before upgrading.

---

## [1.0.0-alpha.9] — 2026-05-28

### SDKs

- **RFC-0004 Phase 2 — `ReputationLogClient`** parity across all six SDKs: `SubmitAsync`, `QueryAsync`, `GetSthAsync`, `GetProofAsync`, `GetGossipSthAsync`; static `VerifyInclusion` performs RFC 9162 §2.1.3.2 Merkle audit-path verification. `SignedTreeHead` + `InclusionProof` wire types. Full test coverage in each language.
- **`AnchorNodeClient` test parity** — Python / Go / Java / Rust implementations each gain complete test suites (21–25 tests per language) covering all five topology event types, stream cancellation, mid-stream error propagation, event filtering, and URL normalization.
- **RFC-0003 assurance level** — `IdentFrame` assurance extraction and `IdentReputationPolicyHint` interface drafted across SDKs.

---

## [1.0.0-alpha.8] — 2026-05-28

### Spec

- **NPS-RFC-0005** (Reputation Policy Enforcement) promoted Draft → Accepted.
- **NPS-RFC-0002** (NPS X.509 OID Registry) promoted Draft → Accepted.

### .NET SDK

- **`NPS.NWP.Anchor` — `ReputationPolicyEvaluator` (RFC-0005)**: `IReputationPolicyEvaluator` interface; `DefaultReputationPolicyEvaluator` with in-process ban cache + per-NID log query cache; `AnchorNodeOptions.ReputationPolicy`; NWM `reputation_policy` JSON block publication. Three new error codes: `NWP-REPUTATION-THROTTLED`, `NWP-REPUTATION-REJECTED`, `NWP-REPUTATION-BANNED`. Response headers: `X-NWP-Reputation-Status`, `X-NWP-Ban-Expires`. `cgn_limit` pre-execution enforcement in `AnchorNodeMiddleware` (token-budget.md §7.2).
- **`NPS.NWP` — `SubscribeFrame` (0x12)**: Initial `SubscribeFrame` type added to the .NET SDK (pre-CR-0006 wire format, later rewritten in alpha.11).

---

## [1.0.0-alpha.7] — 2026-05-18

### .NET SDK

- **`NPS.NWP.Bridge` — Bridge Node HTTP/HTTPS dispatcher** (closes issue #66, NPS-CR-0001 §3.2 Phase 1): Full HTTP/HTTPS protocol adapter ships — `IBridgeDispatcher` / `BridgeDispatcherRegistry` abstractions, `HttpBridgeDispatcher` with SSRF guard (RFC-1918 rejection, `[GeneratedRegex]`), `BridgeNodeMiddleware` (ASP.NET Core — routes `GET /.nwm` and `POST /invoke`, wraps results in CapsFrame), `BridgeServiceExtensions` (`AddBridgeNode` / `UseBridgeNode`). gRPC / MCP / A2A adapters deferred to follow-up CRs per Phase 1 scope. CR-0001 status updated to "Implemented".

### TypeScript SDK

- **`MemoryNodeServer` — framework-agnostic HTTP handler** (closes issue #60): `IMemoryNodeProvider` interface + `MemoryNodeServer` class serving `GET /.nwm`, `GET /.schema`, `POST /query`, `POST /stream`; SHA-256 anchor_id computation; token budget trimming; auth gate; Node.js `http.IncomingMessage` adapter. 14 tests covering all endpoints, auth, budget trimming, and error cases.

### Go SDK

- **`nwp.MemoryNodeServer` — `http.Handler` Memory Node** (closes issue #60): Go equivalent of the TypeScript `MemoryNodeServer`. `IMemoryNodeProvider` + `StreamingProvider` interfaces; `ServeHTTP` routing; `go vet` clean.

### Demos

- **`cross-sdk-interop` — expanded to 4 test scenarios per client** (closes issue #63): All four clients (dotnet / python / node / go) now run `GET /.nwm` discovery + `POST /stream` NDJSON assembly in addition to the original `GET /.schema` + `POST /query` tests. Wire-format equivalence verified across all languages.

### Housekeeping

- Badge blocks added to all SDK and compat READMEs (EN + CN) in NPS-Dev and all mirror repos. Protocol version badges (`NCP v0.6`, `NWP v0.12`, `NIP v0.8`, `NDP v0.7`, `NOP v0.5`) now consistent across all 38 README files.
- Spec version comments in `README.md` directory tree updated to current (`NWP v0.12`, `NIP v0.8`, `NDP v0.7`).
- `impl/typescript/src/index.ts` `VERSION` corrected from stale `alpha.2` to `alpha.7`.

---

## [1.0.0-alpha.6] — 2026-05-14

### Spec

- **token-budget v0.5 — split CGN into CGN-Estimate and CGN-Billing profiles** (closes issue #40): `spec/token-budget.md` §2.1 now defines two named profiles with non-overlapping conformance requirements. **CGN-Estimate** covers budgets / quota / telemetry — sampling permitted, byte-size fallback permitted, ±5 % drift acceptable, no signing requirement. **CGN-Billing** covers commercial settlement — `verified_tokenizer`-tier (NIP §5.1) required, NID-signed metering records, no sampling, no byte-size fallback, audit-log integration, exchange-rate table version pinned inside the signed record. §4.2 adds three new response headers (`X-NWP-Tokens-Profile`, `X-NWP-Billing-Record`, `X-NWP-Billing-Tokenizer-Tier`); silence on the wire defaults to CGN-Estimate. §6 implementation notes split into 6.1 General / 6.2 CGN-Estimate / 6.3 CGN-Billing. AaaS-Profile bumped to **v0.7** with new **L3-08** requirement: any commercial settlement flow MUST emit CGN-Billing records (not generic CGN). Chinese version `token-budget.cn.md` synchronized (was v0.3 → now v0.5).

- **NPS-CR-0004 — IANA PEN 65715 wire-in**: IANA assigned Private Enterprise Number **65715** to the *Neuro Protocol Suites Committee* on 2026-05-08, replacing the prior unregistered provisional arc `1.3.6.1.4.1.99999`. All NPS X.509 OIDs now anchor to the assigned arc `1.3.6.1.4.1.65715`: `id-nps-eku-agent` = `1.3.6.1.4.1.65715.1.1`, `id-nps-eku-node` = `1.3.6.1.4.1.65715.1.2`, `id-nid-assurance-level` = `1.3.6.1.4.1.65715.2.1`, `id-nps-node-roles` = `1.3.6.1.4.1.65715.2.2`. **NPS-RFC-0002** promoted Draft → Proposed (EXPERIMENTAL banner removed; OQ-2 production-use gate closed). NIP version bumped to **0.8**. Roadmap R08 PEN-assignment blocker closed. **Backwards-incompatible** (no dual-OID transitional period): any certificate minted under `1.3.6.1.4.1.99999.*` MUST be revoked and re-issued before claiming compliance. Six NPS-Dev `impl/` SDKs already on 65715 (verified); the six `NPS-sdk-*` mirror repos and the four frozen `nip-ca-server/example/` ports are wired in this release. See `spec/cr/NPS-CR-0004-pen-wirein.md` and `pen-impact.md` (audit checklist, §9 execution order).

- **NPS-CR-0003 — Orchestrator group NIDs and short-lived session NIDs**: New CR splitting orchestrator identity from per-execution session identity. Reserves the `group-` and `session-` identifier prefixes on `urn:nps:agent:...` NIDs (NPS-3 §3.1); adds the signed `IdentFrame.lineage` object with `role` / `parent_nid` / `group_nid` / `session_id` / `purpose` / `owner_user_id` / `owner_key_id` (§5.1.3); adds the `parent_revoked` revocation reason (§5.3); inserts step **3a** in the verification flow for chain-check (§7); adds four CA endpoints under `/v1/orchestrators/groups/...` (§8); adds seven new error codes (`NIP-CA-GROUP-REVOKED`, `NIP-CA-PARENT-NOT-FOUND`, `NIP-CA-PARENT-NOT-GROUP`, `NIP-CA-SESSION-VALIDITY-INVALID`, `NIP-CA-JWS-INVALID`, `NIP-CA-JWS-EXPIRED`, `NIP-CERT-PARENT-REVOKED`). NIP version bumped to **0.7**. Backward-compatible for ordinary single-NID flows; opt-in for orchestrators. Reference impl: .NET; other SDKs deferred. See `spec/cr/NPS-CR-0003-orchestrator-group-session-nids.md`.

- **Release discipline — alpha has no sub-versions**: `docs/release-process.md` now makes `X.Y.Z-alpha.N → X.Y.Z-alpha.N+1` the only valid alpha advancement path. Active labels such as `1.0.0-alpha.5.1` or `1.0.0-alpha.5.2` are invalid; post-alpha.5 content folds into `1.0.0-alpha.6`.

- **NPS-CR-0002 Phase 2 spec complete (NWP v0.12)**: §8.2 adds the `cgn_est` field to `DiffFrame` for per-event Cognon estimation; §12.2 registers the `anchor_state` and `resync_required` event types; §12.4 expands the authorization model with the `topology:subscribe` capability, mid-stream rejection semantics, and the reputation-system interaction note; `spec/frame-registry.yaml` 0x02 (DiffFrame) and 0x12 (SubscribeFrame) descriptions bumped to reflect the new fields.

- **NPS-3-NIP §5.2 TrustFrame — complete spec**: New ten-field table including `serial` and `signer_nid`; Ed25519/JCS canonicalization rule for the signature; five new error codes (`NIP-TRUST-FRAME-INVALID`, `NIP-TRUST-FRAME-EXPIRED`, `NIP-TRUST-FRAME-GRANTOR-REVOKED`, `NIP-TRUST-FRAME-SCOPE-EXCEEDS-GRANTOR`, `NIP-TRUST-FRAME-NODES-PATTERN-INVALID`); §7 verification flow updated to integrate TrustFrame validation. CN mirror (`NPS-3-NIP.cn.md`) updated in lockstep.

- **NPS-3-NIP §5.3 RevokeFrame — complete spec**: New field table (including the conditional `parent_nid`); reason enum table with an explicit forward-compatibility policy; signature rule; four new error codes; §7 inbound-push verification step inserted. CN mirror (`NPS-3-NIP.cn.md`) updated in lockstep.

- **NPS-3-NIP §5.1 IdentFrame — `cert_chain` + `cert_format`**: IdentFrame field table gains `cert_chain` (DER certificate chain, encoded as a base64url array) and `cert_format` (`x509-der` | `raw-pubkey`).

- **NPS-CR-0003 — Draft → Accepted (2026-05-11)**: The Orchestrator group / session NIDs CR (above) advanced from Draft to Accepted on 2026-05-11. The .NET reference implementation already shipped in this release; non-.NET SDK ports remain deferred.

- **NPS-CR-0005 — NIP CA RA model (Draft)**: New CR drafted defining the NIP CA Server's Registration-Authority model as three enrollment-authorization tiers: **allowlist**, **bootstrap-token**, and **approval-queue**. Closes the gap where prior CRs left non-orchestrator enrollment policy implementation-defined.

- **`spec/frame-registry.yaml` — six frames promoted draft → proposed**: NWP `0x10` (QueryFrame), `0x11` (ActionFrame), `0x12` (SubscribeFrame) and NIP `0x20` (IdentFrame), `0x21` (TrustFrame), `0x22` (RevokeFrame) all moved from `status: draft` to `status: proposed` following the alpha.6 freeze of their field tables.

- **Conformance vectors**: NWP suite gained **49 new vectors** — `action_frame` (+13), plus new `subscribe_frame` and `query_frame` aggregation coverage. NIP suite gained **11 TrustFrame** vectors and **12 RevokeFrame** vectors, plus extended `IdentFrame` lineage and `assurance-level` coverage.

### .NET SDK

- **`NPS.NWP.Anchor` — NPS-CR-0002 Phase 2 alpha.6 boundary**: `topology.stream` push/notify remains wired through `AnchorNodeMiddleware` + `IAnchorTopologyService`, and the alpha.5 compatibility window for `topology.filter.node_kind` is now closed. Clients must send `topology.filter.node_roles`; the deprecated alias returns HTTP 400 / `NWP-TOPOLOGY-FILTER-UNSUPPORTED`.

- **`NPS.NIP` — group / session NID issuance**: `NipCaService` gains `RegisterGroupAsync` (mints `group-{uuid}` NIDs with longer-default validity), `IssueSessionAsync` (mints `session-{ts}-{rand}` NIDs that chain to a group via signed `lineage`), and `ListSessionsAsync` (audit). `RevokeAsync` cascades to live sessions when the target is a group, marking them with reason `parent_revoked`. `VerifyAsync` performs the new §7 step 3a chain check and returns `NIP-CERT-PARENT-REVOKED` when a session's parent is revoked or expired. New `NipGroupJws` helper performs the `EdDSA` flattened-JWS verification described in CR §3.5; the HTTP layer accepts either a group-JWS body (`Content-Type: application/jose+json`) or an Operator-API-key Bearer for `/v1/orchestrators/groups/{nid}/sessions/issue`. Storage schemas grow `nid_role` / `parent_nid` / `lineage_json` columns; PostgreSQL migration `db/002_orchestrator_session.sql` is idempotent and supports rolling upgrades. SQLite migration is automatic on `OpenAsync` via `PRAGMA table_info` discovery. New `NipCaOptions` knobs: `GroupCertValidityDays` (365), `SessionDefaultValidity` (1h), `SessionMaxValidity` (24h), `SessionMinValidity` (1m), `SessionJwsClockSkew` (5m).

### NIP CA Server

- **`/v1/orchestrators/groups/...` endpoints live**: Operator-authed `register` and `revoke`, dual-auth (group-JWS or Operator) `sessions/issue`, and Operator-authed audit `sessions` listing. `/.well-known/nps-ca` adds `"orchestrator-group"` to its `capabilities` array so clients can discover support. Apply `db/002_orchestrator_session.sql` before upgrading from v1.0-alpha.5.

### Tests

- **`NPS.Tests.Nip.OrchestratorGroupSessionTests`**: Sixteen new xUnit cases covering all five CR-0003 acceptance scenarios (group issuance, session chained to group, authority rejection, signed-metadata round-trip, group revocation cascading + future-block) plus boundary cases (validity bands, capability subset enforcement, JWS tampering, audit listing).

### Implementation

- **`npsd` + `nip-ca-server` — operability baseline**: Both daemons now expose `/healthz` (liveness), `/readyz` (readiness), and `/metrics` (Prometheus exposition format) endpoints. `SIGTERM` triggers a graceful shutdown with a 30-second drain window. JSON structured logging is provided via the new `NPS.Daemon.Observability` shared library; verbosity is controlled at startup by the `NPS_LOG_LEVEL` environment variable.

### Ops

- **`deploy/docker-compose/`**: New `docker-compose.yml` plus `.env.example` enabling one-command startup of the full NPS daemon stack.

- **`deploy/systemd/`**: New systemd unit files for `npsd` and `nip-ca-server`, plus an `install.sh` installer that drops the units in place and reloads `systemd`.

- **`Makefile`**: New top-level targets `up`, `down`, and `install-systemd` wrapping the docker-compose and systemd flows above.

---

## [1.0.0-alpha.5] — 2026-05-01

### Spec

- **NPS-RFC-0004 Phase 3 — STH Gossip Protocol**: New §4.5 STH Gossip Protocol added. Each log operator MUST expose `GET /v1/log/gossip/sth` returning `own_sth` + cached `peer_sths`. Background gossip push cycle (configurable via `NPSLEDGER_PEERS` + `NPSLEDGER_GOSSIP_INTERVAL_S`): fetch → verify Ed25519 signature → monotonicity check (tree_size MUST not decrease) → cache. Fork detection: tree_size regression logs `NIP-REPUTATION-GOSSIP-FORK` and halts acceptance from that peer. Two new error codes: `NIP-REPUTATION-GOSSIP-FORK` (`NPS-SERVER-INTERNAL`) and `NIP-REPUTATION-GOSSIP-SIG-INVALID` (`NPS-CLIENT-BAD-FRAME`) added to `spec/error-codes.md`. `spec/rfcs/NPS-RFC-0004` §4.5 renumbers: old §4.5 Error Codes → §4.7; old §4.7 Backward Compatibility → §4.8. OQ-1 (gossip protocol choice) resolved in favour of lightweight NPS-native variant. Phase 3 phasing row marked done.

- **AaaS-Profile v0.5 → v0.6 — L2-09 default `reputation_policy`**: New requirement L2-09 (SHOULD) added to §4.3 Level 2 table: configure `reputation_policy` and consult RFC-0004 log operator; minimum recommended policy: `fail_open: true`, reject on `cert-revoked ≥minor` and `scraping-pattern ≥major` within 30 days.

- **`spec/status-codes.md` / `spec/status-codes.cn.md` — `NPS-SERVER-UNSUPPORTED`**: New status code added (`501`) for server-side unimplemented operations (e.g., unrecognised reserved `type` on `QueryFrame`/`SubscribeFrame`). `NWP-RESERVED-TYPE-UNSUPPORTED` mapped to `NPS-SERVER-UNSUPPORTED` in the protocol-error table.

- **`spec/error-codes.md` — `NWP-RESERVED-TYPE-UNSUPPORTED`**: New error code added (`NPS-SERVER-UNSUPPORTED` → HTTP 501) for when a `QueryFrame`/`SubscribeFrame` `type` value is in the reserved namespace but not implemented by the node. Explicit disambiguation note vs `NWP-ACTION-NOT-FOUND` (404 for unknown `action_id`, not for unknown reserved types).

- **`spec/token-budget.md` §7.2 — per-event `cgn_est` field (SHOULD)**: Push streams SHOULD carry an `cgn_est` field on each `TopologyEventEnvelope` (and similar envelope types) carrying the UTF-8/4 byte estimate of the event payload. This lets agents track live token consumption without counting bytes client-side.

- **wire field rename — `AnchorActionSpec.estimated_npt → cgn_est`** (Breaking): Aligns with the CGN naming convention and matches `TopologyEventEnvelope.cgn_est`. **Wire breaking change** — clients pinning to the old key will see the field as null after upgrade. No alias retained. Closes [labacacia/NPS-Dev#17](https://github.com/labacacia/NPS-Dev/issues/17).

### .NET SDK

- **`NPS.NWP.Anchor` — `NWP-RESERVED-TYPE-UNSUPPORTED` enforcement**: `AnchorNodeMiddleware` now returns HTTP 501 / `NPS-SERVER-UNSUPPORTED` / `NWP-RESERVED-TYPE-UNSUPPORTED` when `/anchor/query` or `/anchor/subscribe` receive an unrecognised reserved `type` value. Previously returned 404 / `NWP-ACTION-NOT-FOUND` (incorrect per spec).

- **`NPS.NWP.Anchor` — `topology:read` capability gate**: New `AnchorNodeOptions.RequireTopologyCapability` (default `false`). When `true`, both `/query` and `/subscribe` check the `X-NWP-Capabilities` request header for `"topology:read"` (case-insensitive, comma-separated); missing capability returns HTTP 403 / `NPS-AUTH-FORBIDDEN` / `NWP-TOPOLOGY-UNAUTHORIZED`. New `NwpHttpHeaders.Capabilities = "X-NWP-Capabilities"` constant added.

- **`NPS.NWP.Anchor` — `cgn_est` on `TopologyEventEnvelope`**: New nullable `cgn_est: uint?` field on `TopologyEventEnvelope`, populated with `Math.Max(1, UTF8.GetByteCount(payload) / 4)` on every pushed event.

- **`NPS.NIP` — `AssuranceLevels.FromWireOrAnonymous("")` fix**: Empty string `""` now returns `Anonymous` (consistent with `null`). Previously `null` returned Anonymous but `""` would throw or return Unknown depending on the call path. Python, TypeScript, and Java SDKs received the same fix.

- **`NPS.NIP` — `SqliteNipCaStore`**: New `INipCaStore` implementation backed by SQLite (`Microsoft.Data.Sqlite`). Suitable for single-binary / embedded CA deployments without a PostgreSQL dependency. Capabilities stored as a JSON string; serials generated via an atomic `nip_serial` table. Factory: `await SqliteNipCaStore.OpenAsync(connectionString)`. Closes [labacacia/NPS-Dev#19](https://github.com/labacacia/NPS-Dev/issues/19).

- **`NPS.NIP` — pluggable `INipCaStore` injection**: `NipServiceExtensions` exposes two new overloads: `AddNipCa(configure, INipCaStore store)` accepts any store implementation; `AddNipCaWithSqlite(configure, sqliteConnectionString)` is a convenience wrapper that opens and migrates a SQLite database synchronously at startup. The existing `AddNipCa(configure)` retains its PostgreSQL behaviour but now validates that `ConnectionString` is non-empty and emits a clear error pointing to the alternatives. `NipCaOptions.ConnectionString` changed from `required string` to `string?` — callers using a custom store no longer need to set it. Closes [labacacia/NPS-Dev#18](https://github.com/labacacia/NPS-Dev/issues/18).

### Daemons

- **`daemons/nps-ledger/` — Phase 3: STH Gossip**: New `GossipState` singleton holds the peer configuration (`NPSLEDGER_PEERS` JSON array of `{log_id, endpoint, pub_key?}`) and caches the most recently validated `SignedTreeHead` per peer. New `GossipService` (`BackgroundService`) runs the gossip push cycle on every tick (`NPSLEDGER_GOSSIP_INTERVAL_S`, default 30 s): fetches `GET {peer}/v1/log/gossip/sth`, verifies the Ed25519 signature when `pub_key` is configured (skips with a warning when absent — dev-only), enforces monotonicity, caches on acceptance. New endpoint `GET /v1/log/gossip/sth` returns `{own_sth, peer_sths}`. `/health` updated to `version: "1.0.0-alpha.5"`, `phase: 3`, `gossip_peers`, `gossip_interval_s`. New `NPSLEDGER_PEERS` and `NPSLEDGER_GOSSIP_INTERVAL_S` env vars. csproj version → `1.0.0-alpha.5`.

### SDK Fixes

- **Python `nps_sdk` — `AssuranceLevel.from_wire("")` returns `ANONYMOUS`**: `if wire is None:` changed to `if not wire:` so both `None` and `""` return `ANONYMOUS` instead of raising `ValueError`.

- **TypeScript `@labacacia/nps-sdk` — `AssuranceLevel.fromWire("")` returns `Anonymous`**: `if (wire == null)` changed to `if (!wire)`.

- **Java `nps-sdk` — `AssuranceLevel.fromWire("")` returns `ANONYMOUS`**: `if (wire == null)` changed to `if (wire == null || wire.isEmpty())`.

### Tests

- Test count: **629 → 656** (all passing).
  - `GossipStateTests.cs` — 13 new tests: interval clamping, empty state invariants, `AcceptPeerSth` storage and update, `CurrentPeerSths` multi-peer, `ReceivedAt` timestamp, `FromEnvironment` parsing (no peers / valid JSON / pub_key / malformed fallback), NipSigner round-trip through cached STH.
  - `AnchorTopologyTests.cs` — `TopologyQuery_UnknownReservedType_Returns501WithCorrectCode` (renamed + updated from 404→501 assertion); new `TopologySnapshot_MissingCapability_Returns403`.
  - `AssuranceLevelTests.cs` — `FromWireOrAnonymous_NullOrEmpty_ReturnsAnonymous` (replaces prior merged test); `FromWireOrAnonymous_UnknownNonEmpty_Throws` (new — verifies spec m6 enforcement).

### Fixed

- **m8 — RFC-0002 §4.4 referenced non-existent / wrong NIP section**: `spec/rfcs/NPS-RFC-0002-x509-acme-nid-certs.md` §4.4 stated `Servers MUST implement timing-safe comparison per spec/NPS-3-NIP.md §10.2`. NIP §10.2 exists but covers OCSP response-time normalization — a different timing concern — not challenge token comparison. Fix: the cross-reference replaced with an inline self-contained requirement (`Servers MUST implement constant-time comparison when verifying the signed challenge token to prevent timing-oracle attacks`), with a note clarifying that §10.2 is not applicable here.

- **m7 — NWP §6.6 used SubscribeFrame to cancel a streaming query without explanation**: The cancellation instruction used `SubscribeFrame(action="unsubscribe")` to stop a streaming query — a subscription-semantics frame applied to a query state machine — with no design note. Fix: added a **Note** in §6.6 explaining that this is the protocol-wide stream-cancellation signal (intentional reuse), and that nodes route by `stream_id` regardless of whether the stream originated from QueryFrame or SubscribeFrame.

- **m6 — NIP §5.1.1 `assurance_level` had no forward-compatibility rule for unknown future values**: The spec defined `anonymous / attested / verified` but said nothing about what to do if a receiver encounters a value not in the enum — creating ambiguity about whether to silently demote to `anonymous` or reject. Fix: added a **Forward compatibility** paragraph to §5.1.1: unknown values MUST trigger `NIP-ASSURANCE-UNKNOWN` (NPS-CLIENT-BAD-FRAME); silent demotion to `anonymous` is explicitly prohibited to prevent a security loophole for future higher-assurance levels.

- **m5 — CERTIFIED.md attestation templates under-specified RFC 8785 canonicalization**: Both `NPS-NODE-L1-CERTIFIED.md` and `NPS-NODE-L2-CERTIFIED.md` said "RFC 8785 canonicalized" without specifying the key requirements (UTF-8, key ordering, IEEE 754 number format, Unicode escaping), leaving cross-language implementations at risk of producing different SHA-256 digests over the same logical manifest. Fix: added a **RFC 8785 JCS requirements** callout block to the Attestation section of both templates listing the four required properties and recommending a JCS-compliant library.

- **m4 — `spec/token-budget.md` v0.2 had no budget semantics for continuous-push operations**: The spec defined `X-NWP-Budget` for request/response but did not address streaming queries (`QueryFrame stream:true`) or SubscribeFrame push streams (`topology.stream`), leaving implementers without a policy. Fix: `token-budget.md` v0.2 → v0.3; new §7 "Streaming and Subscription Budget Policy" defines: §7.1 streaming queries — budget applies per-batch, node MUST trim or stop the batch; §7.2 push streams — `X-NWP-Budget` is not enforced by the node (agent-side control), `X-NWP-Tokens` SHOULD be reported per event; design rationale explains why server-side enforcement is incompatible with real-time delivery.

- **m3 — NOP §3.1.1 "3 levels" delegation-chain limit was ambiguous (roles vs. hops)**: The text said "Maximum delegation chain depth: 3 levels (Orchestrator → Worker → Sub-Worker)" without specifying whether a "level" counts entities or hops. `NOP-DELEGATE-CHAIN-TOO-DEEP` described it as "default 3 levels" with no clarifying count. Fix: §3.1.1 bullet rewritten to explicitly state that each entity counts as one level (Orchestrator=1, Worker=2, Sub-Worker=3; a fourth entity triggers the error) and adds the equivalent hop count (≤ 2 DelegateFrame hops). Error code description in the table updated identically.

- **m2 — NDP §5 DNS TXT `v=nps1` vs NCP preamble `NPS/1.0` version-string styles were unexplained**: Two different version-string formats for the same protocol version appeared in adjacent specs with no cross-reference or rationale, creating apparent inconsistency. Fix: added a callout note in NDP §5 explaining that `v=nps1` follows DNS TXT key-value conventions while `NPS/1.0\n` is an RFC-style transport-layer identifier; they serve different layers and are intentionally different encodings.

- **m1 — NCP §3.1 extended-header diagram had a broken bottom border and missing `Reserved` field description**: The ASCII art box for the 8-byte extended header had `┘──────────────┘` (missing a `┴` junction) where `┴──────────────┘` was required, making the Reserved cell appear detached. The field-description bullet list also omitted a `Reserved` entry, so readers could not confirm the 8-byte total. Fix: corrected the bottom border to `┴──────────────┘`; added a `**Reserved**: 2 bytes (Byte 6–7, extended header only); senders MUST set to 0, receivers MUST ignore. The extended header is therefore 8 bytes: 1 + 1 + 4 + 2.` bullet.

- **`spec/rfcs/NPS-RFC-0003` — critical-extension flip phase-gated**: RFC-0003 §4.2 previously stated unconditional MUST-check + MUST-close-with-MISMATCH for the cert extension, while the revision history noted the flip is "NOT yet active" — a downgrade-attack window. Fix: §4.2 field table and prose now split behavior by phase: Phase 1–2 (current) is SHOULD-check, enforcement opt-in; Phase 3 (flag day, see §8.1) is MUST-enforce. §8.1 Phase 3 row gains an explicit flag-day definition (≥ 21-day notice on NPS-Dev GitHub Discussions before activation). The `id-nid-assurance-level` critical-extension promotion section relabelled "(Phase 3 — not yet active)". Same phase gate added to NPS-3-NIP §5.1.1 IdentFrame field description and §5.1.1 prose.

- **`spec/rfcs/NPS-RFC-0002` — provisional OID explicitly gated**: Added `WARNING: EXPERIMENTAL` block to the RFC header and body; changed Status line to `Draft — EXPERIMENTAL`; hardened §10 OQ-2 from "application pending" to a list of hard MUST NOT constraints. Added conformance block note to `spec/NPS-3-NIP.md` §5.1.1 `"attested"` row: the provisional OID `1.3.6.1.4.1.99999.1` does not satisfy "RFC-0002-compliant" for conformance or production until IANA PEN is assigned.

- **`spec/NPS-2-NWP.md` — ghost field `auth.min_assurance_level` removed**: NWP §4.1 prose promised a per-action assurance-level override via `auth.min_assurance_level` on individual `ActionSpec` entries (§4.6), but no such field appeared in either the §4.3 auth table or the §4.6 ActionSpec table. Fixed: added `min_assurance_level: string (Optional)` directly to the §4.6 ActionSpec table (flat field, consistent with `required_capability`); corrected §4.1 prose and §15 v0.6 changelog row to drop the erroneous `auth.` prefix. (NPS-RFC-0003)

- **`spec/error-codes.md` v0.9 → v1.0**: Four NWP topology error codes (`NWP-TOPOLOGY-UNAUTHORIZED`, `NWP-TOPOLOGY-UNSUPPORTED-SCOPE`, `NWP-TOPOLOGY-DEPTH-UNSUPPORTED`, `NWP-TOPOLOGY-FILTER-UNSUPPORTED`) were defined in NPS-2 NWP §13 and referenced by NPS-CR-0002 §3.5 but omitted from the error-codes registry during the alpha.4 spec sync. Registrations added; wording is consistent with NWP §13 definitions.

- **M2 — missing error codes for `"gateway"` legacy-value rejection**: NPS-CR-0001 mandated MUST-reject for the removed `"gateway"` wire value in both `node_roles` (NDP) and `node_type` (NWP NWM), but assigned no specific error codes — each SDK could use any code, making it impossible for clients to distinguish "known-removed legacy value" from "completely invalid value". Fix: `spec/error-codes.md` v1.0 → v1.1 adds four codes: `NDP-ANNOUNCE-ROLE-REMOVED` and `NDP-ANNOUNCE-ROLE-UNKNOWN` (NDP), `NWP-MANIFEST-NODE-TYPE-REMOVED` and `NWP-MANIFEST-NODE-TYPE-UNKNOWN` (NWP). The `-REMOVED` codes apply to the specific `"gateway"` case (with a SHOULD hint to CR-0001); `-UNKNOWN` is the catch-all for any other unrecognized value. NPS-4-NDP §3.1 and §6, NPS-2-NWP §2.1 / §4.1 / §13, and NPS-CR-0001 §4 all updated to cite the new codes.

- **M8 — AaaS-Profile L2 / Node-Profile L2 cross-profile contract was implicit**: `NPS-AaaS-Profile.md` L2-08 required `topology.snapshot` / `topology.stream` on Anchor Nodes but made no reference to what the *host daemon* had to satisfy; `NPS-Node-Profile.md` §8 only said SHOULD for Node L1, leaving a certification gap where an Anchor could claim AaaS L2 on a non-conformant host. Fix: (1) `NPS-AaaS-Profile.md` v0.4 → v0.5: §4.3 L2-08 description extended — implementations claiming L2-08 MUST satisfy Node-Profile L1 for the Anchor host; active-registry Anchors SHOULD also satisfy Node-Profile L2. Depends-On bumped NWP v0.8 → v0.10, NIP v0.4 → v0.6, NDP v0.5 → v0.6. (2) `NPS-Node-Profile.md` v0.1 → v0.2: §4 NWP bullet extended — Anchor Nodes at L2 MUST implement `topology.snapshot` / `topology.stream` with AaaS L2-08 cross-reference; §8 relationship paragraph hardened: SHOULD → MUST for Node-Profile L1 on Anchor hosts; explicit MUST for Node-Profile L2 when claiming AaaS L2-08. Depends-On bumped to NCP v0.6, NWP v0.10, NIP v0.6, NDP v0.6.

- **M7 — L2 conformance suite had zero negative-path test cases**: All 7 TCs in `spec/services/conformance/NPS-Node-L2.md` §3 were happy-path only — an implementation that silently accepts `depth=999`, ignores unknown filter keys, or returns topology data to unauthenticated callers would pass L2 certification. The "every MUST-reject clause has a failure-path TC" invariant was unmet. Fix: `NPS-Node-L2.md` v0.1 → v0.2 adds 5 negative-path TCs (TC-N2-AnchorTopo-04 through -08): unauthorized access without `topology:read` → `NWP-TOPOLOGY-UNAUTHORIZED`; depth exceeds Anchor max → `NWP-TOPOLOGY-DEPTH-UNSUPPORTED`; unrecognized scope → `NWP-TOPOLOGY-UNSUPPORTED-SCOPE`; unrecognized filter key → `NWP-TOPOLOGY-FILTER-UNSUPPORTED`; unrecognized reserved type → `NWP-RESERVED-TYPE-UNSUPPORTED`. Total case count 7 → 12; certification threshold updated accordingly. Also fixed `node_kind` → `node_roles` in TC-N2-AnchorTopo-01 and -03 (M1 consistency).

- **M6 — topology read-back authorization was "implementation-defined" with no identity↔role binding**: NWP §12.4 deferred the authorization policy entirely to implementations and §14.7 only said SHOULD restrict to NIDs with `node_roles: "anchor"` — but `node_roles` only appears in NDP `AnnounceFrame` and NWM, not in `IdentFrame`. No mechanism existed to bind "I am an anchor" to the identity credential presented at connection time, leaving topology data (full cluster member table + capacity metrics) without a testable access gate. Fix: (1) NWP §12.4 "Authorization model" bullet replaced with a three-level minimum binding — Phase 1–2: Anchor Nodes MUST require `topology:read` in `IdentFrame.capabilities` (primary gate, key-signed but self-declared); SHOULD cross-check NDP `node_roles` contains `"anchor"` as defense-in-depth; Phase 3 [RFC-0002 stable]: SHOULD additionally verify CA-attested `id-nps-node-roles` cert extension. (2) §14.7 updated to reference the §12.4 defined minimum, removing the previous "until standardized" hedge. (3) `spec/NPS-3-NIP.md` v0.5 → v0.6: `topology:read` added to the standard `capabilities` registry. (4) NWP `Depends-On` NIP bumped to v0.6. Spec versions: NWP v0.9 → v0.10, NIP v0.5 → v0.6.

- **M5 — `spec/frame-registry.yaml` not bumped for alpha.4 frame changes**: `QueryFrame` (0x10), `SubscribeFrame` (0x12), and `DiffFrame` (0x02) all gained new optional fields or enum extensions in alpha.4 (NPS-CR-0002), but `frame-registry.yaml` remained at v0.9. The registry is the single machine-readable source of truth used by CI and codegen; leaving it stale means the frame surface changes are invisible to version diffing and git-blame. Fix: registry v0.9 → v0.10; `QueryFrame` and `SubscribeFrame` `protocol_version` bumped `0.5` → `0.8` (NWP v0.8 added the optional `type` field for reserved query namespaces); `DiffFrame` `protocol_version` bumped `0.5` → `0.6` (NCP v0.6 current; NPS-CR-0002 NWP §8.2 extended the `event_type` enum with `member_joined` / `member_left` / `member_updated`). All three entries have updated descriptions noting the new fields and their source CR.

- **M4 — `NWP-ACTION-NOT-FOUND` overloaded in §12 reserved-query rejection**: NWP §12 specified that unrecognized reserved `type` values on `QueryFrame` should be rejected with `NWP-ACTION-NOT-FOUND`, but that code is already defined in §7.1 for "`action_id` not registered". A client receiving `NWP-ACTION-NOT-FOUND` from a §12 query could not distinguish "wrong action_id" from "this node doesn't support this reserved operation" — making graceful fallback impossible without probing. Fix: `spec/error-codes.md` v1.1 → v1.2 adds `NWP-RESERVED-TYPE-UNSUPPORTED` (`NPS-SERVER-UNSUPPORTED`); NWP §12 rejection rule updated to use the new code; §13 error table updated. The description in error-codes.md and §13 includes an explicit disambiguation note vs `NWP-ACTION-NOT-FOUND`.

- **M3 — RFC-0004 §4.3/§4.4 Phase 2 features incorrectly presented as Phase 1**: RFC-0004 §4.3 listed all four HTTP endpoints (`POST /v1/log/entries`, `GET /v1/log/entries`, `GET /v1/log/sth`, `GET /v1/log/proof`) in a single block, implying `/sth` and `/proof` are Phase 1. §4.4 described NWM `reputation_policy` and NDP `/.nid/reputation` in present tense without any deferral marker, also implying Phase 1. Per §8.1 phasing table and Appendix A, Merkle tree + STH + inclusion proofs + `/.nid/reputation` + `reputation_policy` are all Phase 2 (targeted v1.0-alpha.5). Fix: §4.3 split into `4.3.1 Phase 1 — Submit and Query (current)` and `4.3.2 [Phase 2] — Merkle Integrity Proofs (deferred)` with an explicit deferral block; §4.4 gains a `[Phase 2 — deferred]` block at the top; §7 Merkle/STH security paragraph tagged `[Phase 2]`; §8.3 test items 2 and 3 tagged `[Phase 2]`. Applies to both EN and CN versions.

- **M1 — `node_kind` / `node_type` naming disambiguation**: `node_kind` (NDP `Announce`) and `node_type` (NWP NWM) had overlapping names but diverged semantics — `node_kind` was a multi-valued discovery-layer role list while `node_type` is a single operative role — with no documented cross-protocol constraint, creating a validation ambiguity. Fix: (1) `NPS-4-NDP.md` v0.5 → v0.6: `node_kind` renamed to `node_roles` (array-only; parsers MUST accept `node_kind` as alias through alpha.5); (2) `NPS-2-NWP.md` v0.8 → v0.9: new §2.1 *Node Role Resolution* table documenting the two-field design; `node_type` description updated — MUST be one of `node_roles` values; topology member table and `topology.filter` key renamed `node_roles`; §14.7 reference updated; (3) `spec/frame-registry.yaml` AnnounceFrame entry updated; (4) `spec/cr/NPS-CR-0001` §3.4 gains a historical migration note.

---

## [1.0.0-alpha.4] — 2026-04-30

### Spec

- **NPS-CR-0002 — Implemented (pre-1.0 fast-track)**: Reserved query types `topology.snapshot` and `topology.stream` on Anchor Nodes, mandatory at NPS-AaaS-Profile L2. New top-level NWP §12 covers both: `topology.snapshot` (QueryFrame, `type="topology.snapshot"`) returns the cluster's current member list with a monotonic `version` counter; `topology.stream` (SubscribeFrame, `type="topology.stream"`) pushes `member_joined` / `member_left` / `member_updated` / `anchor_state` / `resync_required` events as DiffFrames whose `seq` is the post-event topology version. QueryFrame §6.1 and SubscribeFrame §8.1 each gain an optional top-level `type` field for opting into reserved types; DiffFrame §8.2 `event_type` enum extends to topology event types under reserved subscribe types. Four new error codes: `NWP-TOPOLOGY-UNAUTHORIZED`, `NWP-TOPOLOGY-UNSUPPORTED-SCOPE`, `NWP-TOPOLOGY-DEPTH-UNSUPPORTED`, `NWP-TOPOLOGY-FILTER-UNSUPPORTED`. New §14.7 Topology Read-back security section. Existing NWP §12 Error Codes / §13 Security / §14 Changelog renumbered to §13 / §14 / §15. AaaS-Profile §4.3 gains L2-08 mandating both query types on Anchor Nodes that maintain a member registry; §2 intro placeholder ("reserved for v1.0-alpha.4") replaced with concrete L2 mandate. New conformance suite `spec/services/conformance/NPS-Node-L2.md` v0.1 covering L2-08 with seven `TC-N2-AnchorTopo-*` / `TC-N2-AnchorStream-*` test cases; companion `NPS-NODE-L2-CERTIFIED.md` self-attestation template. The remaining L2 requirements (L2-01..L2-07) are tracked in follow-up CRs.

- **NPS-RFC-0002 — Prototype landed (status: still Draft)**: .NET reference prototype for X.509 + ACME NID certificates. `IdentFrame` gains optional `cert_format` ("v1-proprietary" default | "v2-x509") and `cert_chain` (base64url DER) fields — non-breaking; v1 verifiers ignore the extra fields and remain functional (RFC §8.1 Phase 1 dual-trust). `NPS.NIP.X509` ships `NipX509Builder` (issues leaf + self-signed root with critical NPS EKU, SAN URI = NID, ASN.1-ENUMERATED `nid-assurance-level` extension) and `NipX509Verifier` (chain verify + EKU presence + subject/SAN match + assurance-level binding). `Ed25519X509SignatureGenerator` bridges `CertificateRequest` to NSec for Ed25519 signing per RFC 8410. `NipCaService.RegisterX509Async` issues v2 certs alongside the existing v1 path; `NipIdentVerifier` Step 3b layers X.509 chain validation on top of v1 signature check when X.509 trust roots are configured. `NPS.NIP.Acme` ships an ACME client + in-process server pair implementing the new `agent-01` challenge type (NPS-RFC-0002 §4.4): client signs the challenge token with the NID private key, server verifies against the account JWK, finalize accepts the CSR and issues an X.509 leaf cert. **Pebble (RFC 8555 reference) interop deferred to follow-up** — pebble doesn't implement the non-standard `agent-01` challenge so direct interop would only validate HTTP-01 conformance; `tools/pebble/setup.sh` ships the binary download helper. Four new error codes (`NIP-CERT-FORMAT-INVALID`, `NIP-CERT-EKU-MISSING`, `NIP-CERT-SUBJECT-NID-MISMATCH`, `NIP-ACME-CHALLENGE-FAILED`) added to `spec/error-codes.md` v0.8 → v0.9. **RFC §9 Empirical Data backfilled**: v1 IdentFrame 459 B; v2 IdentFrame 1512 B (+229%, 26% over §8.4 1200 B target — target raised to 1600 B); v1 verify 597.8 µs / v2 verify 1698.5 µs (2.84× v1, within 5× regression ceiling — absolute 50 µs target dropped as host-dependent and unrealistic on shared-tenant CI). RFC §10 OQ-1 resolved by data: bundle X.509 + ACME together (the prototype ran 7 working days for both vs ~5 days X.509-alone — splitting forces two cross-language port waves). Provisional OID arc `1.3.6.1.4.1.99999` used pending IANA PEN (OQ-2). Benchmark project `NPS.Benchmarks.NipCert` produces the report at `docs/benchmarks/nip-cert-prototype.md`. RFC remains Draft pending shepherd review; promotion to Proposed/Accepted blocked on cross-SDK port work + IANA PEN.

- **Version bumps**:
  - NPS-2 NWP: `v0.7` → `v0.8` (CR-0002 §12 Reserved Query Types + topology error codes + DiffFrame `event_type` extension).
  - AaaS-Profile: `v0.3` → `v0.4` (CR-0002 L2-08 row + §2 placeholder removal; `Depends-On` NWP bumped to v0.8).
  - `spec/error-codes.md`: `v0.8` → `v0.9` (RFC-0002 prototype — 4 new NIP-CERT-* / NIP-ACME-* codes).

### .NET SDK

- **`NPS.NWP.Anchor`**: New client-side surface under `NPS.NWP.Anchor.Client` namespace — `AnchorNodeClient` with `GetSnapshotAsync(...)` (typed Query) and `SubscribeAsync(...)` (typed `IAsyncEnumerable<TopologyEvent>`). Records: `TopologySnapshot`, `MemberInfo`, `MemberChanges`, `TopologyFilter`. `TopologyEvent` hierarchy: `MemberJoined` / `MemberLeft` / `MemberUpdated` / `AnchorState` / `ResyncRequired`. `AnchorNodeMiddleware` extended to route `/query` and `/subscribe` paths to a new `IAnchorTopologyService` injected via DI; existing `/invoke` path unchanged.

### Daemons

- **`daemons/nps-registry/` — SQLite-backed real registry**: alpha.3 shipped this daemon as a phase-1 skeleton with stub Announce / Resolve / Graph endpoints (returning empty payloads). alpha.4 replaces the stubs with `SqliteNdpRegistry` — a SQLite-backed `INdpRegistry` implementation co-locating the announcement table, an `expires_at` index for lazy purge, and a singleton `graph_meta` row holding the monotonic per-cluster graph sequence counter that bumps on every Announce / eviction. Lazy expiry on every read (`GetAll` / `Resolve`) means no background timer is needed. `CreateInMemory()` factory uses a private SQLite connection string with `mode=memory&cache=shared` plus a keep-alive connection — used as the daemon's no-config fallback when `NPSREGISTRY_SQLITE_PATH` is unset, and as the test-fixture entry point. New env var `NPSREGISTRY_SQLITE_PATH` selects the file path; `/health` exposes a new `storage` field (`"in-memory"` or the configured file path). New 10-test xUnit suite under `impl/dotnet/tests/NPS.Tests/Daemons/Registry/SqliteNdpRegistryTests.cs` — covers parity with `InMemoryNdpRegistryTests` (announce / refresh / TTL eviction / lazy purge / resolve / unknown-target nulls) plus SQLite-specific cases (graph seq monotonicity, file-backed persistence across reopens). New package dep: `Microsoft.Data.Sqlite 10.0.0`. csproj version → `1.0.0-alpha.4`. Closes the alpha.3 promise tracked at line "SQLite-backed real registration lands alpha.4". L2 federation (cross-machine gossip) remains queued for alpha.5+ per `docs/daemons/architecture.md` phasing.

- **`daemons/nps-ledger/` — Phase 2: SQLite + Merkle + STH + inclusion proofs**: alpha.3 shipped this daemon as a Phase 1 in-memory log honouring only the `ReputationLogEntry` shape, with `/v1/log/sth` returning a placeholder `{seq, timestamp, merkle_root: null}`. alpha.4 lands the full Phase 2 surface promised by NPS-RFC-0004 §4.3. New `LedgerStore` (SQLite) appends entries with monotonic `seq` (transactional `seq_counter` row + tx-scoped allocation), stamps a server `timestamp`, and persists the RFC 9162 §2 `LeafHash = SHA256(0x00 || canonical_body)` next to the body so STH / inclusion proofs don't need to re-canonicalise on every read. New `MerkleTree` ships an RFC 9162 binary tree implementation: `Mth` (Merkle Tree Hash) splits at the largest power of 2 below the range, `InclusionProof` returns the audit path leaf-to-root with the standard left-or-right sibling logic. New `OperatorIdentity` mirrors `npsd`'s `RootIdentity` — Ed25519 keypair persisted as PKCS#8 with `0600` POSIX perms at `${NPSLEDGER_DATA_DIR}/operator.ed25519.pkcs8`, public key encoded `ed25519:{base64url}`, `log_id` is `urn:nps:log:operator-{8-byte-fingerprint-hex}` unless `NPSLEDGER_LOG_ID` overrides; reused for STH signing. New `SignedTreeHead` / `InclusionProof` records define the on-the-wire JSON shape. `Program.cs` rewired: `/v1/log/entries` now appends to `LedgerStore`; `/v1/log/sth` returns a real signed STH (operator key signs the canonical-with-signature-excluded form via the existing `NipSigner` pipeline); new `GET /v1/log/proof?seq=N` returns the audit path + leaf hash + tree size for offline inclusion verification; `/health` exposes `phase: 2`, `storage`, `log_id`, `operator_pub_key`. New env vars `NPSLEDGER_DATA_DIR`, `NPSLEDGER_SQLITE_PATH`, `NPSLEDGER_LOG_ID`. Cross-log replay protection: `log_id` is covered by the STH signature so an STH from log "A" cannot verify under log "B" even if the signing key happens to be the same. New 20-test xUnit suite under `impl/dotnet/tests/NPS.Tests/Daemons/Ledger/` — 7 `MerkleTreeTests` (RFC 9162 known-vector checks: empty / 1 / 2 / 4 / 5 leaves; inclusion-proof verifies-at-every-index for n=7 via RFC 9162 §2.1.3.2 algorithm; single-leaf path is empty; out-of-range throws), 9 `LedgerStoreTests` (monotonic seq from 1 / server-side timestamp rewrite / count tracking / Query subject + since filters / leaf hash matches `SHA256(0x00 || canonical_body)` / `GetLeafHashAndIndex` returns correct 0-based index / unknown-seq returns null / persistence across reopens preserves count and continues numbering), 4 `SignedTreeHeadTests` (signature roundtrip verifies; tampering `sha256_root_hash` / `tree_size` / `log_id` each invalidates). Test count: 609 → 629, all passing. New package dep: `Microsoft.Data.Sqlite 10.0.0`. csproj version → `1.0.0-alpha.4`. Closes the alpha.3 commitment "persistence + Merkle tree + STH + inclusion proofs land in alpha.4". STH gossip across operators (NPS-RFC-0004 §6 federation) and full issuer-signature verification (requires nps-registry NID resolution) remain queued for alpha.5+.

- **`daemons/npsd/`**: New `TopologyRegistry` service ingests NDP `Announce` frames carrying `cluster_anchor` = npsd's NID, maintains an in-memory member map keyed by NID, monotonic per-Anchor `version` counter, and a configurable ring buffer (default retention: 256 events) for `topology.stream` replay. Implements `IAnchorTopologyService` so the standard `AnchorNodeMiddleware` `/query` and `/subscribe` routing surfaces topology operations without per-deployment glue. Restart strategy: rebase `version` to the snapshot count + emit `anchor_state.version_rebased` to active subscribers.

### Java SDK

- **NPS-RFC-0001 Phase 2 — Java preamble helper**: New `com.labacacia.nps.ncp.NcpPreamble` final class exposing the spec constants (`LITERAL`, `LENGTH`, `READ_TIMEOUT_MS`, `CLOSE_DEADLINE_MS`), `getBytes()` (defensive-copy), `matches`, `tryValidate(byte[], String[] reason)`, `validate` (throws `NcpPreambleInvalidException` carrying `errorCode` + `statusCode`), and `write(OutputStream)`. Reason strings are byte-identical to the .NET reference for cross-SDK telemetry parity; future-major (`NPS/2.x`) preambles are flagged distinctly from arbitrary garbage. New test class `NcpPreambleTest` — 14 cases, all passing. No new dependencies.
- **NPS-RFC-0002 — Java port (cross-SDK port wave, lead language)**: `impl/java/` ships full parity with the .NET reference prototype. New package `com.labacacia.nps.nip.x509`: `NipX509Builder` (issues leaf + self-signed root via BouncyCastle `X509v3CertificateBuilder` with critical NPS EKU, SAN URI = NID, ASN.1 ENUMERATED `nid-assurance-level` extension under provisional OID `1.3.6.1.4.1.99999.2.1`), `NipX509Verifier` (chain verify + EKU presence/critical check + subject CN / SAN URI match + assurance-level binding + chain-anchors-to-trusted-root check), `Ed25519PublicKeys` helper (raw 32-byte ↔ JCA `PublicKey` ↔ BC `SubjectPublicKeyInfo` conversions), `NpsX509Oids` constants. Cert signing reuses native JCA Ed25519 via `JcaContentSignerBuilder("Ed25519")` — no provider switch. New package `com.labacacia.nps.nip.acme`: `AcmeMessages` (DTO records for Directory / Account / Order / Identifier / Authorization / Challenge / FinalizePayload / ProblemDetail with Jackson annotations), `AcmeJws` (RFC 7515 flattened JWS signing using `alg: "EdDSA"` per RFC 8037 + RFC 7638 thumbprint), `AcmeClient` (Java 11+ `HttpClient`-based, drives newNonce → newAccount → newOrder → POST-as-GET authz → respond `agent-01` → finalize with PKCS#10 CSR → fetch PEM chain), `AcmeServer` (in-process JDK `com.sun.net.httpserver.HttpServer` for tests, full state machine: nonces / account JWKs / orders / authorizations / challenges / issued certs). `IdentFrame` extended with `cert_format` + `cert_chain` optional fields (non-breaking — existing 4-arg constructor preserved). New `NipIdentVerifier` + `NipVerifierOptions` implements RFC §8.1 Phase 1 dual-trust: v1 Ed25519 sig check (always) + Step 2 minimum assurance level (when configured) + Step 3b X.509 chain validation (only when `trustedX509Roots` is populated AND `cert_format == "v2-x509"`). New `AssuranceLevel` enum (RFC-0003 — backfills the parse-only path the Java SDK had previously deferred) and `NipErrorCodes` constants (all current NIP error code strings, including the 4 RFC-0002 codes). 7 new tests in `src/test/java/com/labacacia/nps/nip/`: `NipX509Tests` (5 cases — happy path, EKU stripped, subject NID mismatch, v1-only verifier accepts v2 by ignoring chain, v2 verifier rejects chain when trust roots missing), `AcmeAgent01Tests` (2 cases — full ACME round-trip with NipX509Verifier validation of issued PEM, tampered agent_signature triggers `NIP-ACME-CHALLENGE-FAILED`). Total Java SDK tests: 91 → 98, all passing. New dependencies: `org.bouncycastle:bcprov-jdk18on:1.79` + `bcpkix-jdk18on:1.79` (X.509 builder API only — JDK lacks public cert-building surface). Build version bumped to `1.0.0-alpha.4`. `IdentFrame.unsignedDict()` deliberately excludes `cert_format` / `cert_chain` from the v1 signed payload (the Ed25519 CA signature continues to cover only `nid` / `pub_key` / `metadata` / `assurance_level`).

### Python SDK

- **NPS-RFC-0001 Phase 2 — Python preamble helper**: New module `nps_sdk.ncp.preamble` exposing the spec constants (`LITERAL`, `LENGTH`, `BYTES`, `READ_TIMEOUT`, `CLOSE_DEADLINE`, `ERROR_CODE`, `STATUS_CODE`), `matches`, `try_validate(buf) -> (ok, reason)`, `validate` (raises `NcpPreambleInvalidError` carrying `error_code` + `status_code`), and sync/async `write` / `write_async` helpers. Reason strings are byte-identical to the .NET reference for cross-SDK telemetry parity. Re-exported from `nps_sdk.ncp` along with `NcpPreambleInvalidError`. New test file `tests/test_ncp_preamble.py` — 13 cases, all passing. No new dependencies.
- **NPS-RFC-0002 — Python port (cross-SDK port wave, second language)**: `impl/python/` ships full parity with the .NET / Java references. New module `nps_sdk.nip.x509`: `NipX509Builder` (uses `cryptography.x509.CertificateBuilder` with native `pyca/cryptography` Ed25519 — sign with `algorithm=None` per RFC 8410), `NipX509Verifier` (chain decode + EKU presence/critical + subject CN / SAN URI match + assurance-level binding + chain-anchors-to-trusted-root), `Ed25519PublicKeys`-equivalent helpers inline, `NpsX509Oids` constants. New module `nps_sdk.nip.acme`: `messages` (dataclass DTOs with `to_dict()`/`from_dict()` — Directory / Account / Order / Identifier / Authorization / Challenge / FinalizePayload / ProblemDetail / ChallengeRespondPayload), `jws` (RFC 7515 flattened JWS with `alg: "EdDSA"` per RFC 8037 + RFC 7638 thumbprint), `AcmeClient` (httpx `AsyncClient`-based, drives newNonce → newAccount → newOrder → POST-as-GET authz → respond `agent-01` → finalize with PKCS#10 CSR → fetch PEM chain), `AcmeServer` (stdlib `http.server.ThreadingHTTPServer` running in a daemon thread — full state machine: nonces / account JWKs / orders / authorizations / challenges / issued certs). `IdentFrame` extended with `assurance_level` (RFC-0003) + `cert_format` + `cert_chain` optional fields (non-breaking); `unsigned_dict()` deliberately excludes `cert_format` / `cert_chain` from the v1 signed payload (the Ed25519 CA signature continues to cover only the existing `nid` / `pub_key` / `capabilities` / `scope` / `issued_by` / `issued_at` / `expires_at` / `serial` / `metadata` / `assurance_level`). New `NipIdentVerifier` + `NipVerifierOptions` implements RFC §8.1 Phase 1 dual-trust: v1 Ed25519 sig check (always) + Step 2 minimum assurance level (when configured) + Step 3b X.509 chain validation (only when `trusted_x509_roots` is populated AND `cert_format == "v2-x509"`). New `AssuranceLevel` enum (RFC-0003 — backfills the parse-only path the Python SDK had previously deferred) and `error_codes` module (all current NIP error code strings, including the 4 RFC-0002 codes). 7 new tests in `tests/`: `test_nip_x509.py` (5 cases — happy path, EKU stripped, subject NID mismatch, v1-only verifier accepts v2 by ignoring chain, v2 verifier rejects chain when trust roots missing), `test_nip_acme_agent01.py` (2 cases — full ACME round-trip with NipX509Verifier validation of issued PEM, tampered agent_signature triggers `NIP-ACME-CHALLENGE-FAILED`). Total Python SDK tests: 191 → 198, all passing. **No new package dependencies** — `cryptography` / `httpx` / `msgpack` were already in `pyproject.toml`; the in-process ACME server uses stdlib `http.server.ThreadingHTTPServer` so no async-server dep was added. Build version bumped to `1.0.0-alpha.4`.

### TypeScript SDK

- **NPS-RFC-0001 Phase 2 — TypeScript preamble helper**: New module `src/ncp/preamble.ts` exposing the spec constants (`PREAMBLE_LITERAL`, `PREAMBLE_LENGTH`, `PREAMBLE_BYTES`, `PREAMBLE_READ_TIMEOUT_MS`, `PREAMBLE_CLOSE_DEADLINE_MS`, `PREAMBLE_ERROR_CODE`, `PREAMBLE_STATUS_CODE`), `preambleMatches`, `tryValidatePreamble`, `validatePreamble` (throws `NcpPreambleInvalidError` carrying `errorCode` + `statusCode`), and `writePreamble`. Reason strings are byte-identical to the .NET reference for cross-SDK telemetry parity. Re-exported from `src/ncp/index.ts`; `NCP_PREAMBLE_INVALID` added to `NCP_ERROR_CODES`. New test file `tests/ncp/preamble.test.ts` — 13 cases, all passing. No new dependencies.
- **NPS-RFC-0002 — TypeScript port (cross-SDK port wave, third language)**: `impl/typescript/` ships full parity with the .NET / Java / Python references. New module `nip/x509`: `issueLeaf` / `issueRoot` (built on `@peculiar/x509` `X509CertificateGenerator` with native Web Crypto Ed25519 — Node 18+ `globalThis.crypto.subtle` supports `{name: "Ed25519"}` natively), `verify` (chain decode + critical EKU presence + subject CN / SAN URI match + assurance-level binding via the ASN.1 ENUMERATED `nid-assurance-level` extension at provisional OID `1.3.6.1.4.1.99999.2.1` + chain-anchors-to-trusted-root), `oids.ts` constants. New module `nip/acme`: `messages.ts` (plain interface DTOs — Directory / Account / Order / Identifier / Authorization / Challenge / FinalizePayload / ProblemDetail / ChallengeRespondPayload / NewAccountPayload / NewOrderPayload), `wire.ts` constants (`agent-01`, `application/jose+json`, RFC 8555 status enum), `jws.ts` (RFC 7515 flattened JWS using `alg: "EdDSA"` per RFC 8037 + RFC 7638 thumbprint built on `@noble/ed25519` + `@noble/hashes/sha256`), `client.ts` (Node `fetch`-based, drives newNonce → newAccount → newOrder → POST-as-GET authz → respond `agent-01` → finalize with PKCS#10 CSR via `@peculiar/x509` `Pkcs10CertificateRequestGenerator` → fetch PEM chain), `server.ts` (in-process `node:http.createServer` for tests — full state machine: nonces / account JWKs / orders / authorizations / challenges / issued certs). `IdentFrame` extended with `assuranceLevel` (RFC-0003) + `certFormat` + `certChain` optional constructor options (non-breaking — existing 4-arg constructor preserved); `unsignedDict()` deliberately excludes `cert_format` / `cert_chain` from the v1 signed payload (the Ed25519 CA signature continues to cover only `nid` / `pub_key` / `metadata` / `assurance_level`). New `NipIdentVerifier` + `NipVerifierOptions` implements RFC §8.1 Phase 1 dual-trust: v1 Ed25519 sig check (always) + Step 2 minimum assurance level (when configured) + Step 3b X.509 chain validation (only when `trustedX509Roots` is populated AND `certFormat === "v2-x509"`). New `AssuranceLevel` class (RFC-0003 — backfills the parse-only path the TypeScript SDK had previously deferred) and `error-codes.ts` module (all current NIP error code strings, including the 4 RFC-0002 codes). `src/nip/index.ts` now re-exports `assurance-level` / `cert-format` / `error-codes` / `verifier` and namespace-exports `x509` / `acme` so consumers see the full surface from `@labacacia/nps-sdk`. 7 new tests under `tests/`: `nip-x509.test.ts` (5 cases — happy path, EKU stripped, subject NID mismatch, v1-only verifier accepts v2 by ignoring chain, v2 verifier rejects chain when trust roots missing), `nip-acme-agent01.test.ts` (2 cases — full ACME round-trip with X.509 verifier validation of issued PEM, tampered agent_signature triggers `NIP-ACME-CHALLENGE-FAILED`). A shared `tests/_rfc0002-keys.ts` helper bridges noble's raw 32-byte Ed25519 keys into Web Crypto `CryptoKeyPair` form (via fixed PKCS8 / SPKI prefixes per RFC 8410) so both SDK halves use the same key material in a single test case. Total TypeScript SDK tests: 264 → 271, all passing. New dependency: `@peculiar/x509@^1.12.0` (X.509 builder API — Node Web Crypto can sign/verify Ed25519 but cannot construct certs). Package version bumped to `1.0.0-alpha.4`.

### Go SDK

- **NPS-RFC-0001 Phase 2 — Go preamble helper**: New file `ncp/preamble.go` exposing the spec constants (`PreambleLiteral`, `PreambleLength`, `PreambleReadTimeoutSecs`, `PreambleCloseDeadlineMs`, `PreambleErrorCode`, `PreambleStatusCode`), `PreambleMatches`, `ValidatePreamble` (returns `*ErrPreambleInvalid` with structured `Reason`), and `WritePreamble(io.Writer)`. Reason strings are byte-identical to the .NET reference for cross-SDK telemetry parity. New test file `ncp/preamble_test.go` — 10 cases, all passing. No new module dependencies.
- **NPS-RFC-0002 — Go port (cross-SDK port wave, fourth language)**: `impl/go/` ships full parity with the .NET / Java / Python / TypeScript references. New package `nip/x509`: `IssueLeaf` / `IssueRoot` (built on stdlib `crypto/x509.CreateCertificate` with native Ed25519 — Go's stdlib has had Ed25519 X.509 issuance since 1.13, no third-party deps needed), `Verify` (chain decode + critical EKU presence + subject CN / SAN URI match + assurance-level binding via the ASN.1 ENUMERATED `nid-assurance-level` extension at provisional OID `1.3.6.1.4.1.99999.2.1` + chain-anchors-to-trusted-root via stdlib `Certificate.CheckSignatureFrom`), `oids.go` constants. New package `nip/acme`: `messages.go` (struct DTOs with `json` tags — Directory / Account / Order / Identifier / Authorization / Challenge / FinalizePayload / ProblemDetail / ChallengeRespondPayload), `wire.go` constants (`agent-01`, `application/jose+json`, RFC 8555 status enum), `jws.go` (RFC 7515 flattened JWS using `alg: "EdDSA"` per RFC 8037 + RFC 7638 thumbprint, all on stdlib `crypto/ed25519`), `client.go` (stdlib `net/http`-based, drives newNonce → newAccount → newOrder → POST-as-GET authz → respond `agent-01` → finalize with PKCS#10 CSR → fetch PEM chain), `server.go` (in-process `net/http.Server` for tests — full state machine: nonces / account JWKs / orders / authorizations / challenges / issued certs). `IdentFrame` extended with `AssuranceLevel` (RFC-0003) + `CertFormat` + `CertChain` non-breaking optional fields; `UnsignedDict()` deliberately excludes `cert_format` / `cert_chain` from the v1 signed payload. New `NipIdentVerifier` + `VerifierOptions` implements RFC §8.1 Phase 1 dual-trust: v1 Ed25519 sig (always) + Step 2 minimum assurance level (when configured) + Step 3b X.509 chain validation (only when `TrustedX509Roots` is populated AND `CertFormat == "v2-x509"`). To avoid a `nip → nip/x509 → nip` import cycle, `NipIdentVerifier` accepts an injected `X509ChainVerifier` function adapter — callers wire it to `nip/x509.Verify` (tests do exactly this in 3 lines). New `AssuranceLevel` struct (with `meetsOrExceeds`, `AssuranceFromWire`, `AssuranceFromRank`) and `error_codes.go` module (all current NIP error code wire strings, including the 4 RFC-0002 codes). 7 new tests in `nip/`: `nip_x509_test.go` (5 cases — happy path, EKU stripped, subject NID mismatch, v1-only verifier accepts v2 by ignoring chain, v2 verifier rejects chain when trust roots missing), `nip_acme_agent01_test.go` (2 cases — full ACME round-trip with X.509 verifier validation of issued PEM, tampered agent_signature triggers `NIP-ACME-CHALLENGE-FAILED`). Total Go SDK tests: 79 → 86, all passing. **No new module dependencies** — Go stdlib's `crypto/x509` covers cert issuance and verification end-to-end (compare with TS needing `@peculiar/x509`, Java needing BouncyCastle).

### Rust SDK

- **NPS-RFC-0001 Phase 2 — Rust preamble helper**: New module `nps_ncp::preamble` exposing the spec constants (`LITERAL`, `BYTES`, `LENGTH`, `READ_TIMEOUT_SECS`, `CLOSE_DEADLINE_MS`, `ERROR_CODE`, `STATUS_CODE`), `matches(&[u8])`, `validate(&[u8]) -> NpsResult<()>` (returns `NpsError::Frame(reason)` on mismatch), and `write(&mut impl Write)`. Reason strings are byte-identical to the .NET reference for cross-SDK telemetry parity. New integration test file `nps-ncp/tests/preamble_tests.rs` — 10 cases, all passing. No new crate dependencies.
- **NPS-RFC-0002 — Rust port (cross-SDK port wave, fifth language — completes the wave)**: `impl/rust/nps-nip/` ships full parity with the .NET / Java / Python / TypeScript / Go references. New module `nps_nip::x509` (with `oids`, `builder`, `verifier`): `issue_leaf` / `issue_root` built on `rcgen 0.13` with the `x509-parser` feature so `SubjectPublicKeyInfo::from_der` is available — this is what lets us sign certs over an *external* Ed25519 public key (the agent's) without forcing rcgen to know the subject's private key. `verify` parses chains via `x509-parser` and verifies Ed25519 signatures via `ed25519-dalek` directly: chain decode + critical EKU presence + subject CN / SAN URI match + assurance-level binding (custom ASN.1 ENUMERATED extension at provisional OID `1.3.6.1.4.1.99999.2.1`) + chain-anchors-to-trusted-root. The critical EKU is built as a hand-rolled `CustomExtension` because rcgen's `extended_key_usages` field doesn't expose the critical bit for `Other(...)` OIDs; the small DER OID encoder is included in `oids.rs`. New module `nps_nip::acme` (with `wire`, `messages`, `jws`, `client`, `server`): `jws.rs` (RFC 7515 flattened JWS with `alg: "EdDSA"` per RFC 8037 + RFC 7638 thumbprint, on `ed25519-dalek` + `sha2`), `client.rs` (`reqwest::Client` async, drives newNonce → newAccount → newOrder → POST-as-GET authz → respond `agent-01` → finalize with PKCS#10 CSR built via `rcgen::CertificateParams::serialize_request` → fetch PEM chain), `server.rs` (in-process `tiny_http::Server` running on a background thread for tests — full state machine: nonces / account JWKs / orders / authorizations / challenges / issued certs). `IdentFrame` extended with `assurance_level` (RFC-0003) + `cert_format` + `cert_chain` optional fields (non-breaking — existing test struct literals updated to include the new fields explicitly); `unsigned_dict()` deliberately excludes `cert_format` / `cert_chain` from the v1 signed payload. New `NipIdentVerifier` + `NipVerifierOptions` implements RFC §8.1 Phase 1 dual-trust: v1 Ed25519 sig (always) + Step 2 minimum assurance level (when configured) + Step 3b X.509 chain validation (only when `trusted_x509_roots_der` is populated AND `cert_format == "v2-x509"`). Both modules live in the same crate so the Rust port doesn't need the function-adapter pattern that the Go port used to break the cycle. New `AssuranceLevel` struct + `error_codes` module (all current NIP error code wire strings, including the 4 RFC-0002 codes). 7 new tests in `nps-nip/tests/`: `nip_x509_tests.rs` (5 cases — happy path, EKU stripped, subject NID mismatch, v1-only verifier accepts v2 by ignoring chain, v2 verifier rejects chain when trust roots missing), `nip_acme_agent01_tests.rs` (2 cases — full ACME round-trip with X.509 verifier validation of issued PEM, tampered agent_signature triggers `NIP-ACME-CHALLENGE-FAILED`). Total Rust SDK tests: 92 → 99, all passing. New crate dependencies: `rcgen 0.13` (X.509 builder, features `pem`+`crypto`+`ring`+`x509-parser`), `x509-parser 0.16` (cert parse + chain verify), `tiny_http 0.12` (in-process HTTP server for the test ACME endpoint), `time 0.3` (rcgen's date type). **Pre-existing version-pin fix bundled with this PR**: `Cargo.toml` had `nps-* = "=1.0.0-alpha.2"` while crates were declared `1.0.0-alpha.3` (workspace was unbuildable on dev before this PR); both rows are now `1.0.0-alpha.4`.

### Tools

- **`tools/nip-ca-server/example/python/` — RFC-0002 sync (un-frozen from alpha.2)**: New endpoints `POST /v2/agents/register` and `POST /v2/nodes/register` issue dual-trust v2 IdentFrames carrying both the v1 Ed25519 signature AND a 2-cert X.509 chain (leaf + self-signed root). Self-signed X.509 root auto-generated on first boot at `${NIP_CA_ROOT_CERT_FILE:/data/ca.root.der}` (5-year validity) and reused thereafter. `ca.issue_cert_x509(...)` is the new function that wraps `cryptography.x509.CertificateBuilder` with the NPS EKU + assurance-level extension and attaches `cert_format: "v2-x509"` + `cert_chain` to the existing v1 frame; the X.509 issuance code is inlined into `ca.py` rather than depending on the SDK to keep the reference port self-contained (matches the Java port's composite-build trade-off — Java *does* depend on the SDK; Python does not, keeping Docker contexts simpler). `/.well-known/nps-ca` advertises `cert_formats: ["v1-proprietary", "v2-x509"]` + new `register_v2` endpoint URL, schema version bumped to `nps_ca: "0.2"`. ACME server endpoints on the FastAPI app are deferred for cross-language parity with .NET (`tools/nip-ca-server/` C# reference also exposes only v1 endpoints; SDK's `AcmeServer` remains the canonical ACME reference). Version bumped to `1.0.0-alpha.4`.

- **`tools/nip-ca-server/example/rust/` — RFC-0002 sync (un-frozen from alpha.2)**: New endpoints `POST /v2/agents/register` and `POST /v2/nodes/register` issue dual-trust v2 IdentFrames carrying both the v1 Ed25519 signature AND a 2-cert X.509 chain (leaf + self-signed root). Self-signed X.509 root re-issued on each boot at `${NIP_CA_ROOT_CERT_FILE:/data/ca.root.der}` (5-year validity) — note: the Rust reference port re-derives a fresh self-signed cert from the persisted CA key on every boot rather than parsing back a stored DER, because `rcgen::Certificate` cannot be reconstructed from raw DER; clients should treat the root cert as a regeneratable artifact. `ca::issue_cert_x509(...)` wraps `rcgen::CertificateParams::signed_by` with the NPS EKU + assurance-level extension and attaches `cert_format: "v2-x509"` + `cert_chain` to the existing v1 frame; X.509 issuance is inlined in `ca.rs` rather than depending on the SDK to keep the reference port self-contained (matches Python / Go / TS trade-off — Java is the only port that pulls the SDK as a runtime dep). `/.well-known/nps-ca` advertises `cert_formats: ["v1-proprietary", "v2-x509"]` + new `register_v2` endpoint URL, schema version bumped to `nps_ca: "0.2"`. ACME server endpoints on the axum app are deferred for cross-language parity with .NET (SDK's `nps_nip::acme::AcmeServer` remains the canonical reference for ACME and can be embedded in production deployments separately). New crate dependencies: `rcgen 0.13`, `time 0.3`. Version bumped to `1.0.0-alpha.4`.

- **`tools/nip-ca-server/example/java/` — RFC-0002 sync (un-frozen from alpha.2)**: The Java reference CA Server now consumes the SDK via Gradle composite build (`includeBuild('../../../../impl/java')`); BouncyCastle is pulled transitively. New endpoints `POST /v2/agents/register` and `POST /v2/nodes/register` issue dual-trust v2 IdentFrames carrying both the v1 Ed25519 signature AND a 2-cert X.509 chain (leaf + self-signed root) signed by the same CA Ed25519 key. Self-signed X.509 root is generated on first boot at `${nip.ca.root-cert-file:/data/ca.root.der}` (5-year validity) and reused thereafter. `CaService.issueCertX509(...)` is the new method that wraps `NipX509Builder.issueLeaf` and attaches `cert_format: "v2-x509"` + `cert_chain` to the existing v1 frame. `/.well-known/nps-ca` advertises `cert_formats: ["v1-proprietary", "v2-x509"]` and a new `register_v2` endpoint URL. ACME server endpoints on the Spring app are deferred for cross-language parity with .NET (`tools/nip-ca-server/` C# reference also exposes only v1 endpoints; SDK's `AcmeServer` remains the canonical reference for ACME). Version bumped to `1.0.0-alpha.4`. Removed dead `class Duration` shim from `CaController.java` that shadowed `java.time.Duration` once `CaService` started using the latter.

- **`tools/nip-ca-server/example/ts/` — RFC-0002 sync (un-frozen from alpha.2)**: New endpoints `POST /v2/agents/register` and `POST /v2/nodes/register` issue dual-trust v2 IdentFrames carrying both the v1 Ed25519 signature AND a 2-cert X.509 chain (leaf + self-signed root). Self-signed X.509 root auto-generated on first boot at `${NIP_CA_ROOT_CERT_FILE:/data/ca.root.der}` (5-year validity) and reused thereafter. `ca.issueCertX509(...)` is the new function — it bridges `node:crypto` `KeyObject`s to `@peculiar/x509`'s Web Crypto API via fixed PKCS8 / SPKI Ed25519 prefixes (per RFC 8410), wraps `X509CertificateGenerator` with the NPS EKU + ASN.1 ENUMERATED `nid-assurance-level` extension, and attaches `cert_format: "v2-x509"` + `cert_chain` to the existing v1 frame. The X.509 issuance code is inlined into `ca.ts` rather than depending on the SDK to keep the reference port self-contained (matches the Python port's trade-off — Java *does* depend on the SDK via Gradle composite build; Python and TypeScript do not, keeping Docker contexts simpler). `/.well-known/nps-ca` advertises `cert_formats: ["v1-proprietary", "v2-x509"]` + new `register_v2` endpoint URL, schema version bumped to `nps_ca: "0.2"`. ACME server endpoints on the Fastify app are deferred for cross-language parity with .NET (`tools/nip-ca-server/` C# reference also exposes only v1 endpoints; SDK's `AcmeServer` remains the canonical ACME reference). New dependency: `@peculiar/x509@^1.12.0`. Version bumped to `1.0.0-alpha.4`.

- **`tools/nip-ca-server/example/go/` — RFC-0002 sync (un-frozen from alpha.2)**: New endpoints `POST /v2/agents/register` and `POST /v2/nodes/register` issue dual-trust v2 IdentFrames carrying both the v1 Ed25519 signature AND a 2-cert X.509 chain (leaf + self-signed root). Self-signed X.509 root auto-generated on first boot at `${NIP_CA_ROOT_CERT_FILE:/data/ca.root.der}` (5-year validity) and reused thereafter. `ca.IssueCertX509(...)` is the new function that wraps stdlib `crypto/x509.CreateCertificate` with the NPS EKU + assurance-level extension and attaches `cert_format: "v2-x509"` + `cert_chain` to the existing v1 frame; the X.509 issuance code is inlined into `ca/ca.go` rather than depending on the SDK (the Go reference port has always been self-contained, and Go's stdlib already covers Ed25519 X.509 — no module dep change needed). `/.well-known/nps-ca` advertises `cert_formats: ["v1-proprietary", "v2-x509"]` + new `register_v2` endpoint URL, schema version bumped to `nps_ca: "0.2"`. ACME server endpoints on the `net/http` mux are deferred for cross-language parity with .NET (SDK's `nip/acme.Server` remains the canonical ACME reference for in-process embedding). No new module dependencies.

---

## [1.0.0-alpha.3] — Unreleased (dev)

### Spec

- **New — `spec/services/NPS-Node-Profile.md` v0.1 Draft**: Node-side compliance specification (L1 Basic / L2 Interactive / L3 Autonomous), orthogonal to the service-side AaaS Profile. Introduces the `activation_mode` model (`ephemeral` / `resident` / `hybrid`) as the load-bearing axis for the three levels. L1 ships with 21 detailed requirement IDs (`N1-NCP-*` through `N1-OBS-*`); L2/L3 headline additions are listed, with detailed IDs tracked under NPS-Roadmap Phase 2/3.
- **New — `spec/services/conformance/NPS-Node-L1.md` v0.1**: Language-agnostic test suite for NPS-Node Profile L1 — 21 test cases (`TC-N1-*`), paired-peer methodology, results manifest schema. Reference suite location tracked at `impl/dotnet/tests/NPS.Daemon.Conformance.Tests/`. Historical note: the alpha.19 audit found 20 actual `TC-N1-*` headings; the original 21-case prose count was an editorial error.
- **New — `spec/services/conformance/NPS-NODE-L1-CERTIFIED.md`**: Self-attestation template for implementations claiming L1 compliance. Contains implementation identity, peer used, run environment, per-case outcome checklist, results manifest, and Ed25519 attestation block.
- **NPS-RFC-0001 — Accepted (pre-1.0 fast-track)**: Native-mode NCP connections now MUST begin with an 8-byte ASCII constant preamble `b"NPS/1.0\n"`. New §2.6.1 in `spec/NPS-1-NCP.md`. HTTP mode unaffected. Mismatched preamble triggers a silent close within 500 ms (no `ErrorFrame`, to avoid leaking framing details to scanners). 10-second timeout for short reads. New error code `NCP-PREAMBLE-INVALID` and new status code `NPS-PROTO-PREAMBLE-INVALID` (with new `PROTO` status category that also covers existing `NPS-PROTO-VERSION-INCOMPATIBLE`). Frame-type byte `0x4E` (ASCII `N`) reserved in `frame-registry.yaml` `reservations:` block — MUST NOT be assigned to any NCP frame.
- **NPS-RFC-0003 — Accepted (pre-1.0 fast-track)**: Three-tier Agent identity assurance levels (`anonymous` / `attested` / `verified`) for anti-scraping and trust gating. New NPS-3 §5.1.1; `IdentFrame` gains optional `assurance_level` field (default `anonymous`, backward-compatible). NWM gains optional top-level `min_assurance_level` plus per-action override on individual `ActionSpec`s. New error codes `NIP-ASSURANCE-MISMATCH`, `NIP-ASSURANCE-UNKNOWN`, `NWP-AUTH-ASSURANCE-TOO-LOW`. The X.509 critical-extension flip (§4.2) is deferred and stays inert until NPS-RFC-0002 ships in alpha.4 — Phase 1 here is parsing only; existing verifiers see no change in default behavior.
- **NPS-CR-0001 — Implemented (pre-1.0 fast-track)**: Split the legacy `Gateway Node` (a single NWP node type) into **Anchor Node** (cluster control plane + NOP routing — inherits the existing role, renamed) and **Bridge Node** (NPS↔non-NPS protocol translation — genuinely new, e.g. HTTP/gRPC/MCP/A2A targets). The wire value `node_type: "gateway"` is removed and parsers MUST reject it. NWP §2.1 rewritten with full Anchor + Bridge subsections; AaaS-Profile §2 renamed `Gateway Node` → `Anchor Node` and gained §2A `Bridge Node`. NPS-Node Profile §8 reference updated. NDP `Announce` gains three additive fields supporting multi-role declarations: `node_kind` (string OR array — values `memory` / `action` / `complex` / `anchor` / `bridge`), `cluster_anchor` (NID — for non-Anchor cluster members), `bridge_protocols` (array — for `bridge` nodes; standard values `http` / `grpc` / `mcp` / `a2a`). To avoid name collision with the new node-type **Bridge Node**, the inverted-direction packages historically published as `compat/{mcp,a2a,grpc}-bridge` (which translate *external protocols → NPS*, the reverse of Bridge Node) have been renamed `compat/{mcp,a2a,grpc}-ingress`; sibling GitHub/Gitee repos `NPS-{mcp,a2a,grpc}-bridge` are scheduled to be renamed `*-ingress` during the alpha.3 release sync (instructions in `tools/cr-0001-rename-instructions.md` — the platform-side rename itself MUST be performed manually).
- **NPS-RFC-0004 — Accepted (pre-1.0 fast-track)**: Certificate-Transparency-style append-only log for NID behavioral incidents. New NPS-3 §5.1.2 defines the entry wire format — 12-field signed JSON with dual-signature (issuer + log operator) over RFC 8785 (JCS) canonical form — plus an 8-value `incident` enum (`cert-revoked` / `rate-limit-violation` / `tos-violation` / `scraping-pattern` / `payment-default` / `contract-dispute` / `impersonation-claim` / `positive-attestation`) and a 5-step `severity` enum (`info` / `minor` / `moderate` / `major` / `critical`). Three new error codes: `NIP-REPUTATION-ENTRY-INVALID` (`NPS-CLIENT-BAD-FRAME`), `NIP-REPUTATION-LOG-UNREACHABLE` (mapped to brand-new `NPS-DOWNSTREAM-UNAVAILABLE` status), `NWP-AUTH-REPUTATION-BLOCKED` (`NPS-AUTH-FORBIDDEN` — reserved at NWP v0.7 even though the policy field shape lands in Phase 2 / NWP v0.8). Phase 1 here ships entry shape + reference types only; Merkle tree, STH, inclusion proofs, NDP `/.nid/reputation` discovery, and NWM `reputation_policy` parsing are deferred to Phase 2 (v1.0-alpha.4) per RFC §8.1.
- **Version bumps**:
  - NPS-1 NCP: `v0.5` → `v0.6` (NPS-RFC-0001).
  - NPS-2 NWP: `v0.5` → `v0.7` (NPS-RFC-0003 NWM additions + NPS-CR-0001 §2.1 Node Types rewrite + Anchor/Bridge subsections + NPS-RFC-0004 reservation of `NWP-AUTH-REPUTATION-BLOCKED`; `Depends-On` upgraded to NCP v0.6 + NIP v0.5 + NDP v0.5).
  - NPS-3 NIP: `v0.3` → `v0.5` (NPS-RFC-0003 §5.1.1 assurance levels + NPS-RFC-0004 §5.1.2 reputation log entry).
  - NPS-4 NDP: `v0.4` → `v0.5` (NPS-CR-0001 AnnounceFrame `node_kind` array form + `cluster_anchor` + `bridge_protocols`; `Depends-On` upgraded to NCP v0.6 + NIP v0.4).
  - AaaS-Profile: `v0.2` → `v0.3` (NPS-CR-0001 §2 rewrite; new §2A Bridge Node; full Depends-On refresh).
  - NPS-4 NDP: `v0.3` → `v0.4` — AnnounceFrame (0x30) gains three additive fields (`activation_mode`, `activation_endpoint`, `spawn_spec_ref`) and a new §3.1.1 Activation semantics section. Added fields are backward-compatible: receivers treat an absent `activation_mode` as `ephemeral`, so `v1.0-alpha.2` publishers continue to resolve correctly. `Depends-On` NCP version corrected to `v0.5`.
  - `spec/error-codes.md`: `v0.5` → `v0.8` (RFC-0001 + RFC-0003 + RFC-0004 rows — five new error codes total).
  - `spec/status-codes.md`: `v0.2` → `v0.4` (new `PROTO` category + RFC-0001 row; RFC-0003 mappings; new `NPS-DOWNSTREAM-UNAVAILABLE` status code + RFC-0004 mappings).
  - `frame-registry.yaml`: `v0.6` → `v0.9` — NDP frame `protocol_version` entries updated to `0.5` (CR-0001 fields); AnnounceFrame description expanded with the `node_kind` / `cluster_anchor` / `bridge_protocols` triplet; new top-level `reservations:` block reserving `0x4E` (NPS-RFC-0001); `IdentFrame (0x20)` `protocol_version` `0.3` → `0.4` and description expanded with `assurance_level` (NPS-RFC-0003).

### Daemons

- **New — `daemons/` top-level directory** containing six resident-service binaries that together form the reference NPS deployment topology. Architecture overview at [`docs/daemons/architecture.md`](docs/daemons/architecture.md). Each daemon ships with a kebab-case binary name, a multi-stage `Dockerfile`, and bilingual READMEs.
  - **Layer 1 (host-local):**
    - `daemons/npsd/` — `npsd` (`Npsd.csproj`, package `LabAcacia.NPS.Daemon.Npsd`). Listens on `127.0.0.1:17433`. L1 minimum: root Ed25519 keypair generation (PKCS#8, file mode `0600` so it satisfies NPS-Node-L1 `TC-N1-NIP-01`), `/.nwm` self-manifest, `/health`. Configurable via `NPSD_PORT` / `NPSD_HOST` / `NPSD_DATA_DIR`. NCP native-mode preamble runtime, inbox persistence, sub-NID issuance, and AnnounceFrame emission deferred to alpha.4.
    - `daemons/nps-runner/` — `nps-runner` (`NpsRunner.csproj`, package `LabAcacia.NPS.Daemon.Runner`). Phase 1 skeleton: Generic Host scaffolding with 30s heartbeat. Inbox watcher + `spawn_spec_ref` resolver + worker lifecycle land at L3 stage (alpha.5+).
  - **Layer 2 (network entry):**
    - `daemons/nps-ingress/` — `nps-ingress` (`NpsIngress.csproj`, package `LabAcacia.NPS.Daemon.Ingress`). Phase 1 skeleton: public HTTP listener on `:8080` + `/health` documenting planned milestones. TLS termination, rate-limit, NeuronHub auth, CGN debit, NPS-RFC-0004 reputation lookup, and NPS-CR-0001 Anchor Node middleware wiring land alpha.4 → alpha.5.
    - `daemons/nps-registry/` — `nps-registry` (`NpsRegistry.csproj`, package `LabAcacia.NPS.Daemon.Registry`). Phase 1 skeleton: HTTP listener on the NDP optional-dedicated port `17436`; `Resolve`/`Graph`/`Announce` URL surface returns `NDP-REGISTRY-UNAVAILABLE` (HTTP 503) so consumers can wire and gracefully fall back. SQLite-backed real registration lands alpha.4.
  - **Layer 3 (trust anchor — NPS Cloud, 2027 Q1+):**
    - `daemons/nps-cloud-ca/` — `nps-cloud-ca` (`NpsCloudCa.csproj`, package `LabAcacia.NPS.Daemon.CloudCa`). Phase 1 deferral skeleton on the NIP optional-dedicated port `17435`; `/v1/issue`, `/v1/revoke`, `/v1/crl`, `/v1/ocsp` return `NIP-CA-NOT-READY` (HTTP 503) and point at the six per-language `tools/nip-ca-server*` OSS CAs already shipped at alpha.2. The daemon's own X.509 + ACME pipeline arrives with NPS-RFC-0002 in alpha.4.
    - `daemons/nps-ledger/` — `nps-ledger` (`NpsLedger.csproj`, package `LabAcacia.NPS.Daemon.Ledger`). Phase 1: in-memory log honouring the NPS-RFC-0004 `ReputationLogEntry` shape with `POST /v1/log/entries`, `GET /v1/log/entries?nid=&since=`, `GET /v1/log/sth`. Persistence + Merkle tree + operator-signed STH + inclusion proofs land alpha.4.
- **`impl/dotnet/NPS.sln`**: all six daemon csproj files added; full-solution `dotnet build` covers libraries + bridges + ingresses + samples + benchmarks + tests + daemons.

### Implementations

- **.NET — `impl/dotnet/src/NPS.Core/Ncp/NcpPreamble.cs` (new)**: Phase 1 of NPS-RFC-0001. Reference helper exposing the constant preamble bytes plus async write / try-validate operations for the (forthcoming) native transport layer. No native transport itself ships in alpha.3; the helper is consumable by future `NPS.Native` work and by any third-party transport implementer. xUnit coverage: round-trip, mismatch, version-future bytes, status-mapping.
- **.NET — `impl/dotnet/src/NPS.NIP/AssuranceLevel.cs` (new)** + `IdentFrame.AssuranceLevel`, `NipVerifyContext.MinAssuranceLevel`, `NeuralWebManifest.MinAssuranceLevel`, `NipErrorCodes.{AssuranceMismatch, AssuranceUnknown}`, `NwpErrorCodes.AuthAssuranceTooLow`: Phase 1 of NPS-RFC-0003. Strongly-typed `AssuranceLevel` enum with ordered comparison + JSON converter for the wire string form (`"anonymous"` / `"attested"` / `"verified"`). IdentFrame and NWM round-trip the field; verifier carries it through to per-request context but does NOT yet enforce — Phase 1 is parse-only, default behavior unchanged. NPS.NIP and NPS.NWP packages bumped to `1.0.0-alpha.3`.
- **NPS Daemon reference implementation (L1-compliant)**: Still deferred to a follow-up milestone; will ship under `impl/dotnet/src/NPS.Daemon/` with the conformance suite under `impl/dotnet/tests/NPS.Daemon.Conformance.Tests/`.
- **Other 5 SDKs (Python / TypeScript / Java / Rust / Go)**: NPS-RFC-0001 Phase 2 — preamble helpers in each SDK — tracked for v1.0-alpha.4 per RFC §8.1.

### Tools

- **NIP CA Server promoted to its own publish repo** —
  [`labacacia/nip-ca-server`](https://github.com/labacacia/nip-ca-server)
  ([Gitee mirror](https://gitee.com/labacacia/nip-ca-server)). Up to and
  including v1.0.0-alpha.2 the server only shipped as a subdirectory of
  this monorepo; from v1.0.0-alpha.3 onward it has its own public
  release repo, its own CHANGELOG, and its own SemVer tags. The
  monorepo source of truth stays at `tools/nip-ca-server/`; the publish
  repo is materialized via `tools/release/sync-nip-ca-server.sh`. The
  repo carries its own `LICENSE` + `NOTICE` and depends on
  `LabAcacia.NPS.NIP` v1.0.0-alpha.3 from nuget.org so it builds
  outside the monorepo.
- **Five non-.NET CA Server ports retained as `tools/nip-ca-server/example/`** —
  Python / TypeScript / Java / Rust / Go. They remain useful as
  language-specific reads of the NPS-3 §8 surface but are frozen at
  v1.0.0-alpha.2 (no Docker images, no SemVer tags, not built by CI,
  not recommended for production). See `tools/nip-ca-server/example/README.md`
  for the path to revive one. They also ride along inside the
  standalone publish repo's `example/` directory.
- **`tools/release/`** — new `sync-nip-ca-server.sh` script
  materializes a publish-repo working tree from the monorepo (rsync +
  publish-overlay), commits, tags, and pushes to GitHub. Designed to be
  the template for sibling release scripts as more standalone publish
  repos are split out from the monorepo. Workflow: [`docs/release-process.md`](docs/release-process.md).
- **NPS Daemons promoted to publish repos** —
  [`labacacia/nps-daemons`](https://github.com/labacacia/nps-daemons)
  ([Gitee mirror](https://gitee.com/labacacia/nps-daemons)) bundles the
  four OSS daemons (`npsd`, `nps-runner`, `nps-ingress`,
  `nps-registry`) — the Layer-1 + Layer-2 of the NPS reference
  topology. Each subdirectory is self-contained (own README,
  Dockerfile, csproj depending on published `LabAcacia.NPS.*` NuGet
  packages), and the bundle's top-level `docker-compose.yml` spins them
  all up at once. The two Layer-3 trust-anchor daemons
  (`nps-cloud-ca`, `nps-ledger`) ship as **private** repos in the
  `innolotus` organisation —
  [`innolotus/nps-cloud-ca`](https://github.com/innolotus/nps-cloud-ca)
  and [`innolotus/nps-ledger`](https://github.com/innolotus/nps-ledger);
  they go public with NPS Cloud GA (2027 Q1+). Each daemon under
  `tools/daemons/<daemon>/` carries its own `publish-overlay/`
  (PackageReference csproj, self-contained Dockerfile, nuget.config),
  and `tools/daemons/bundle-overlay/` carries the bundle-level files
  (top-level README/CHANGELOG/docker-compose). Three new sync scripts
  (`sync-nps-daemons.sh`, `sync-nps-cloud-ca.sh`, `sync-nps-ledger.sh`)
  follow the `sync-nip-ca-server.sh` template. Public bundle is
  registered in `tools/mirror-to-gitee/sync-all.sh`; private repos
  are not mirrored.

### Compatibility

- The NDP additions in alpha.3 are strictly additive and backward-compatible with alpha.2 — `activation_mode` absence is defined to mean `ephemeral`; receivers MUST NOT reject an AnnounceFrame for lacking the field.
- The NCP connection preamble (NPS-RFC-0001) **is a breaking change for native-mode clients that predate alpha.3**. HTTP-mode clients are unaffected. Native mode is still Phase 2+ per `spec/NPS-Roadmap.md` with no GA shipments depending on it; taking the break here avoids a deprecation cycle after 1.0 GA. The .NET helper landing in alpha.3 is library-only (no transport ships yet), so no live deployments break.
- Existing L1-oblivious tooling continues to interoperate without modification.

---

## [1.0.0-alpha.2] — 2026-04-19

### Spec

- **Status bump**: All 11 spec documents advanced from `Draft` → `Proposed`, opening the public RFC window.
- **Version bumps**:
  - NPS-0 Overview: `v0.2` → `v0.3`
  - NPS-1 NCP: `v0.4` → `v0.5`
  - NPS-2 NWP: `v0.4` → `v0.5`
  - NPS-3 NIP: `v0.2` → `v0.3`
  - NPS-4 NDP: `v0.2` → `v0.3`
  - NPS-5 NOP: `v0.3` → `v0.4`
  - NPS-Roadmap: `v0.2` → `v0.3`
  - `frame-registry.yaml`: `v0.4` → `v0.5` (all `protocol_version` entries updated in sync)
  - `error-codes.md`: `v0.4` → `v0.5`
  - `status-codes.md`: `v0.1` → `v0.2`
  - `token-budget.md`: `v0.1` → `v0.2`
  - `services/NPS-AaaS-Profile.md`: `v0.1` → `v0.2`
- **Bilingual unification**: Every spec file now follows the project-wide convention — `X.md` is the English primary with a `English | [中文版](./X.cn.md)` switcher at the top, and `X.cn.md` is the Chinese secondary. Previous `X.en.md` suffix files removed.
- `Depends-On` cross-references updated to the new version numbers.

### Implementations (.NET, Python, TypeScript, Java, Rust, Go)

All six reference SDKs are synchronized to `1.0.0-alpha.2`. Until the project reaches `1.0` stable, all SDKs are released at the same pre-release tag regardless of their individual delta.

#### .NET — `impl/dotnet/` — `1.0.0-alpha.2`

- `NPS.Core` README now explicitly enumerates the NCP frame set (AnchorFrame / DiffFrame / StreamFrame / CapsFrame / HelloFrame / ErrorFrame).
- `NPS.NWP` README calls out all four node types (Memory / Action / Complex / Gateway).
- Status update: 495 tests green, including the new wire-size benchmark, Gateway Node middleware, and A2A Bridge.

#### Python — `impl/python/` — `1.0.0-alpha.2`

- Version bump from `0.2.0` → `1.0.0-alpha.2`; no functional changes beyond version alignment.
- 162 tests, 97% coverage.

#### TypeScript — `impl/typescript/` — `1.0.0-alpha.2`

- **Fixed — `NpsFrameCodec is not a constructor`**: `src/core/index.ts` now explicitly re-exports the shipped OOP API (`NpsFrameCodec`, `Tier1JsonCodec`, `Tier2MsgPackCodec`, `FrameRegistry`, `AnchorFrameCache`) from `./codec.js`, `./registry.js`, and `./cache.js`. The parallel functional API under `./codecs/` remains reachable by direct path but is no longer auto-exported (it collided with the class API on `FrameType` / `EncodingTier` / `FrameHeader`).
- **Fixed — missing Ed25519 runtime deps**: `@noble/ed25519` and `@noble/hashes` are now declared as runtime dependencies. Previously `src/nip/identity.ts` and `src/ndp/validator.ts` imported these modules but `package.json` did not list them, causing install failures for npm consumers.
- **Added — HelloFrame (NCP 0x06)**: New `HelloFrame` class in `src/ncp/frames.ts` with snake_case toDict/fromDict, registered in `ncp/registry.ts`. `FrameType.HELLO = 0x06` added between ALIGN and the NWP block.
- **Added — subpath exports**: `package.json` `exports` map now includes `./nwp`, `./nip`, `./ndp`, `./nop` (previously only `.`, `./core`, and `./ncp` were declared, but the README documented usage of all six).
- Node engine bumped to `>=22.0.0` to align with the shipped stack.
- 264 tests green (up from 158 previously passing + 5 file failures + 1 test failure).

#### Java — `impl/java/` — `1.0.0-alpha.2`

- Version bump from `0.1.0` → `1.0.0-alpha.2`; no functional changes beyond version alignment.
- 87 tests green.

#### Rust — `impl/rust/` — `1.0.0-alpha.2`

- Version bump from `0.1.0` → `1.0.0-alpha.2`; no functional changes beyond version alignment.
- 88 tests green across six workspace crates.

#### Go — `impl/go/` — `1.0.0-alpha.2`

- Version bump from `0.1.0` → `1.0.0-alpha.2`; no functional changes beyond version alignment.
- 75 tests green.

### Tools

#### NIP CA Server — 6 new language implementations

- `tools/nip-ca-server/` — ASP.NET Core 10 + SQLite (`1.0.0-alpha.2`)
- `tools/nip-ca-server-python/` — FastAPI + SQLite (`1.0.0-alpha.2`)
- `tools/nip-ca-server-ts/` — Fastify + better-sqlite3 (`1.0.0-alpha.2`)
- `tools/nip-ca-server-java/` — Spring Boot 3.4 + SQLite (`1.0.0-alpha.2`)
- `tools/nip-ca-server-rust/` — Axum + SQLite (`1.0.0-alpha.2`)
- `tools/nip-ca-server-go/` — stdlib `net/http` + SQLite (`1.0.0-alpha.2`)

All six share the same REST API surface per NPS-3 §8 and the same Docker Compose entrypoint.

### Compat Bridges

- `compat/mcp-ingress/` — `LabAcacia.McpIngress` exposes NWP Memory / Action / Complex Nodes as MCP 2024-11-05 servers (15 tests). Version `1.0.0-alpha.2`.
- `compat/a2a-ingress/` — `LabAcacia.A2aIngress` exposes Action / Complex / Gateway Nodes as Google A2A v0.2 servers; `GET /.well-known/agent.json`, JSON-RPC 2.0 `tasks/send` / `tasks/get` / `tasks/cancel` (18 tests). Version `1.0.0-alpha.2`.

### Docs

- Root `README` flipped to the project-wide convention: `README.md` is now English primary, `README.cn.md` is Chinese secondary. The previous `README.en.md` suffix file is gone.
- Every SDK and CA Server README gained a CN counterpart (`README.cn.md`) with a language switcher at the very top of both files.
- CLAUDE.md spec index table, repository tree, and version references updated to match the new spec versions.
- `docs/sdk/dotnet/index.en.md` link to the root README roadmap section repaired (`README.en.md` → `README.md`).

---

## [1.0.0-alpha.1] — 2026-04-10

Initial public alpha. See [Release-v1.0.0-alpha.1](https://github.com/LabAcacia/nps/releases/tag/v1.0.0-alpha.1) for the full summary.

---

[1.0.0-alpha.7]: https://github.com/LabAcacia/nps/releases/tag/v1.0.0-alpha.7
[1.0.0-alpha.6]: https://github.com/LabAcacia/nps/releases/tag/v1.0.0-alpha.6
[1.0.0-alpha.2]: https://github.com/LabAcacia/nps/releases/tag/v1.0.0-alpha.2
[1.0.0-alpha.1]: https://github.com/LabAcacia/nps/releases/tag/v1.0.0-alpha.1
