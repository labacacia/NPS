# Operator: Node Conformance & Certification

> **Audience:** Operators + node implementers
> **Status:** ✅ Reviewed for v1.0.0-alpha.19 release
> **Source-of-truth precedence:** `spec/` documents in [`labacacia/NPS`](https://github.com/labacacia/NPS/tree/main/spec) win over this page if they disagree.

NPS has two orthogonal compliance profiles. This page explains how they relate and how to run the conformance test suite and publish a self-attestation.

---

## Node-Profile vs AaaS-Profile: orthogonal

| Profile | Question answered | Who cares |
|---------|-------------------|-----------|
| **NPS-Node Profile** | "Is my *host* (daemon / embedded runtime) a conformant participant in the NPS network?" | Node implementers; operators running daemons |
| **NPS-AaaS Profile** | "Does my *service* expose the right NPS endpoints to AI agents?" | AaaS vendors; teams building agent-facing APIs |

A deployment may carry **both** badges, **either**, or **neither**. The two are additive, not mutually exclusive. The cross-profile contract (introduced in alpha.5) is:

- An **AaaS Anchor Node operator claiming L2-08** MUST satisfy **Node-Profile L1** for the host running the Anchor.
- An **Anchor that maintains an active member registry** (i.e., uses `nps-registry` or equivalent) SHOULD also satisfy **Node-Profile L2**.

In other words: topology query capability (L2-08) implies the underlying host has passed the baseline L1 node test suite. A registry-backed Anchor is held to the interactive L2 standard.

---

## Node-Profile Level 1: 21 test cases

The `NPS-Node-L1` conformance suite (`spec/services/conformance/NPS-Node-L1.md`) defines **21 `TC-N1-*` test cases** in five domains:

| Domain | Cases | What they cover |
|--------|-------|-----------------|
| NCP — Wire format | TC-N1-NCP-01 through TC-N1-NCP-04 | Tier-1 JSON frame round-trip; Hello+Anchor handshake; loopback listener default; Tier-2 negotiation hygiene |
| NIP — Identity | TC-N1-NIP-01 through TC-N1-NIP-04 | Root keypair generation and permissions; IdentFrame sign/verify; NID format; sub-NID issuance (optional) |
| NDP — Discovery | TC-N1-NDP-01 through TC-N1-NDP-04 | AnnounceFrame carries `activation_mode = "ephemeral"`; AnnounceFrame signature; ResolveFrame response; GraphFrame subscription (optional) |
| NWP — Inbox and delivery | TC-N1-NWP-01 through TC-N1-NWP-05 | Inbox accepts ActionFrame; inbox persists across restart; FIFO pull order; 100 QPS baseline; push path (optional) |
| Observability | TC-N1-OBS-01 through TC-N1-OBS-03 | Structured log entry per frame direction; required log fields; log destination flexibility |

Three cases (NIP-04, NDP-04, NWP-05) may be recorded as **N/A** if the implementation declines the optional capability; they do not count as failures. All other 18 cases MUST be `pass`.

### Test methodology: paired-peer

Every L1 case is run with a **peer** — any implementation that already passes L1, or the .NET reference SDK (`NPS.Core` + `NPS.NDP` + `NPS.NIP` + `NPS.NWP` at v1.0.0-alpha.11 or later). Run every case against the Implementation Under Test (IUT) paired with the peer. Use a fresh IUT state (wipe the key store, registry, and inbox) between cases unless the case is explicitly continuation-oriented.

Test environment requirements:
- Network: loopback only; no external DNS or inter-host routing required.
- Clock: wall clock within ±5 s of the peer (ISO 8601 UTC timestamps).
- File system: writable directory for the IUT's key store and inbox persistence.
- Wire encoding: Tier-1 JSON MUST be exercised; Tier-2 MsgPack cases are deferred to L2.

---

## Node-Profile Level 2: published suite

Level 2 extends L1 with interactive requirements. The conformance document
(`spec/services/conformance/NPS-Node-L2.md`) is **published** (Draft v0.3) and currently
covers the **L2-08 topology read-back** requirement (NPS-CR-0002) plus the **NCP-over-TLS
ingress** admission gate (NPS-RFC-0006 §6). The remaining L2 requirements (L2-01 through
L2-07 — NOP orchestration, OTel tracing, CGN Token Budget, preflight, retry/timeout, async
actions, AlignStream back-pressure) have their test cases tracked in follow-up CRs and are
out of scope for the current L2 document.

### L2-08 topology cases (12, all MUST pass)

The L2-08 scope defines **12 `TC-N2-*` cases** — all MUST pass; partial claims are not
allowed:

| Group | Cases | What they cover |
|-------|-------|-----------------|
| Topology snapshot | `TC-N2-AnchorTopo-01` … `-03` | 3-member snapshot; version monotonicity; sub-Anchor `child_anchor` / `member_count` |
| Topology negative paths | `TC-N2-AnchorTopo-04` … `-08` | one MUST-reject per error code: `NWP-TOPOLOGY-UNAUTHORIZED` (missing `topology:read`), `NWP-TOPOLOGY-DEPTH-UNSUPPORTED`, `NWP-TOPOLOGY-UNSUPPORTED-SCOPE`, `NWP-TOPOLOGY-FILTER-UNSUPPORTED`, `NWP-RESERVED-TYPE-UNSUPPORTED` |
| Topology stream | `TC-N2-AnchorStream-01` … `-04` | `member_joined`; `member_left` on TTL expiry; resume from `topology.since_version`; `resync_required` when version too old |

### NCP-over-TLS ingress cases (NPS-RFC-0006 §6)

For an IUT terminating native-mode NCP-over-TLS at an L2 ingress (e.g. `nps-ingress`):

| Case | Covers |
|------|--------|
| `TC-N2-Tls-01` | ALPN `nps/1.0` negotiated over TLS 1.3; unknown ALPN failed with alert `no_application_protocol` (120) |
| `TC-N2-Tls-02` | Mutual TLS required (`RequireClientCert = true`) |
| `TC-N2-Tls-03` | Client cert validates to a trust anchor and binds the session NID |
| `TC-N2-Tls-04` | IdentFrame/certificate NID mismatch → `NCP-NID-MISMATCH` |

The paired peer for L2 is any L2-passing implementation, or the .NET reference
(`NPS.NWP.Anchor` + `NPS.NDP`) at v1.0.0-alpha.11 or later. Copy
`NPS-NODE-L2-CERTIFIED.md` to your repository root, fill it in, and sign with the IUT's
root key — same flow as L1.

---

## Node-Profile Level 3: published suite (runtime / FaaS)

The L3 conformance document (`spec/services/conformance/NPS-Node-L3.md`, Draft v0.1) covers
the **NOP L3 runtime integration** introduced by [NPS-CR-0007](https://github.com/labacacia/NPS/tree/main/spec/cr/)
— the `nps-runner` ↔ NOP orchestration interface. L3 is strictly additive over the NOP
orchestration semantics and the L1/L2 node suites; it adds the task-claim protocol,
`spawn_spec_ref` resolution, and idle/max-runtime lifecycle enforcement.

| Group | Cases | What they cover |
|-------|-------|-----------------|
| Task-claim protocol | `TC-N3-Claim-01` … `-03` | concurrent claim → exactly one granted, other gets `NOP-CLAIM-CONFLICT` (409); lease-expiry reclaim without re-executing terminal nodes (`dedup_key`); lease renewal |
| `spawn_spec_ref` resolution | `TC-N3-Spawn-01` … `-03` | inline `spawnspec:` base64url-JSON; `https://`/`nwp://` fetch + schema validation; unresolvable/invalid → `NOP-SPAWN-SPEC-INVALID` (400) |
| Lifecycle enforcement | `TC-N3-Life-01` … `-02` | `NOP-RUNTIME-IDLE-TIMEOUT` / `NOP-RUNTIME-MAX-RUNTIME` (504); node `FAILED`; worker reaped |
| End-to-end & saga | `TC-N3-DAG-01`, `TC-N3-Saga-01` | 3-node linear DAG end-to-end; reverse-topological saga rollback (`COMPENSATING → COMPENSATED`) |

Third-party certification (NPS Cloud CA) remains targeted for L3 in 2027 Q1+; self-certification
is sufficient at this release.

---

## Running checks with nps-probe

The `nps-probe` conformance CLI (v0.2) drives an implementation under test as a paired peer
and runs the standard NPS surface checks (handshake, identity, discovery, inbox). As of
v0.2 it adds **Check 5: NWM `trust_anchors` validation**, verifying that an Anchor's NWM
declares a well-formed `trust_anchors` array of CA NID URNs (NWP §13 / CR-0006). Run it
against a freshly stood-up cluster or in CI before claiming a conformance level.

---

## Conformance harness package: `LabAcacia.NPS.Conformance`

The `LabAcacia.NPS.Conformance` package ships the reusable conformance **harness** behind the
test suites. It carries the **Node L1 and L2 case catalogs** as data — the `TC-N1-*` and
`TC-N2-*` case definitions described above — plus `TC-N1`/`TC-N2` helper drivers that stand up
a paired peer, run each case against the Implementation Under Test, and emit the results
manifest shape shown below. Implementers embed the harness in their own test project rather
than re-encoding the case catalog by hand, which keeps third-party suites in lock-step with the
published catalogs as cases are added.

---

## Self-attestation process

1. **Build and run the test suite** against your implementation. The reference suite for .NET 10 + xUnit is at `impl/dotnet/tests/NPS.Daemon.Conformance.Tests/` (planned; tracked alongside NPS Daemon MVP) and builds on the `LabAcacia.NPS.Conformance` harness (Node L1/L2 case catalogs + `TC-N1`/`TC-N2` helpers). Python and TypeScript suites are planned for Phase 2.

2. **Produce a results manifest** (JSON) summarizing per-case outcomes:

   ```json
   {
     "profile": "NPS-Node-L1",
     "profile_version": "0.1",
     "iut": {
       "name": "my-daemon",
       "version": "1.0.0",
       "nid": "urn:nps:node:example.com:host-01"
     },
     "peer": {
       "name": "nps-dotnet-reference",
       "version": "1.0.0-alpha.11"
     },
     "run": {
       "date": "2026-05-03T00:00:00Z",
       "environment": "linux-x64 / 2 vCPU / 4 GB"
     },
     "cases": [
       { "id": "TC-N1-NCP-01", "result": "pass" },
       { "id": "TC-N1-NCP-02", "result": "pass" }
     ],
     "summary": { "pass": 21, "fail": 0, "skip": 0, "na": 0 }
   }
   ```

   Certification is granted when all 21 cases are `pass` or `na`.

3. **Copy the attestation template** from `spec/services/conformance/NPS-NODE-L1-CERTIFIED.md` in [NPS](https://github.com/labacacia/NPS/tree/main/spec/services/conformance/) to your repository root.

4. **Fill in the template** with your IUT details and embed the results manifest.

5. **Sign the attestation block** with your implementation's root key (see Signing section below).

6. Publish the filled-in `NPS-NODE-L1-CERTIFIED.md` at your repository root as a public claim of compliance.

Self-certification is sufficient for L1, L2, and L3 at this release. Third-party certification via NPS Cloud CA is targeted for L3 in 2027 Q1+.

---

## Signing canonicalization: RFC 8785 JCS

The signed attestation block uses **RFC 8785 JSON Canonicalization Scheme (JCS)** — the same canonicalization used throughout NPS for IdentFrames, AnnounceFrames, and reputation log entries.

JCS rules that matter for the attestation:
- **Encoding**: UTF-8 throughout.
- **Unicode escaping**: control characters (`U+0000`–`U+001F`) MUST be escaped; other characters SHOULD NOT be escaped.
- **Numbers**: IEEE 754 double-precision; no trailing zeros after the decimal point; exponent notation for very large or very small values.
- **Key ordering**: object keys are sorted lexicographically by Unicode code point (ascending).
- **Signature field**: omit the `signature` field from the object before canonicalizing and signing, then attach the resulting signature as the `signature` field.

Sign with the IUT's root Ed25519 private key. The signature value MUST be encoded as `ed25519:<base64url(sig-bytes)>`.

---

## Cross-profile: AaaS L2-08 implies Node-Profile L1

The cross-profile contract in full:

| AaaS claim | Node-Profile obligation |
|-----------|------------------------|
| Claiming **any AaaS level** | No Node-Profile obligation (profiles are orthogonal by default) |
| Claiming **AaaS L2-08** (topology queries) | MUST satisfy **Node-Profile L1** for the host running the Anchor |
| Claiming **AaaS L2-08** with an **active member registry** | SHOULD satisfy **Node-Profile L2** |

This means: if your Anchor exposes `topology.snapshot` and `topology.stream`, your host daemon must have passed the 21 L1 test cases. If your Anchor is backed by a live member registry (via `nps-registry` or equivalent), you are held to the L2 interactive standard.

---

## Where to find the templates

All conformance documents and attestation templates are in the `spec/services/conformance/` directory of the [NPS repository](https://github.com/labacacia/NPS/tree/main/spec/services/conformance/):

| File | Purpose |
|------|---------|
| `NPS-Node-L1.md` | L1 conformance suite (21 `TC-N1-*` test cases, paired-peer methodology, results manifest schema) |
| `NPS-NODE-L1-CERTIFIED.md` | L1 self-declaration template; copy to your repository root when all 21 cases pass |
| `NPS-Node-L2.md` | L2 conformance suite (Draft v0.3 — 12 `TC-N2-AnchorTopo*`/`AnchorStream*` cases for L2-08, plus `TC-N2-Tls-*` NCP-over-TLS ingress cases) |
| `NPS-NODE-L2-CERTIFIED.md` | L2 self-declaration template |
| `NPS-Node-L3.md` | L3 conformance suite (Draft v0.1 — `TC-N3-Claim-*` / `TC-N3-Spawn-*` / `TC-N3-Life-*` / `TC-N3-DAG-01` / `TC-N3-Saga-01`, NPS-CR-0007 runtime integration) |

The remaining L2 requirement cases (L2-01 … L2-07) and an `NPS-NODE-L3-CERTIFIED.md` template
will be published as the corresponding follow-up CRs land (see NPS-Roadmap).

---

## See also

- [Operator AaaS Profile](./Operator-AaaS-Profile.md) — AaaS compliance levels and the L2-08 / topology:read cross-profile contract
- [SDK Building an Anchor Node](./SDK-Building-an-Anchor-Node.md) — how to implement an Anchor Node using the NPS SDK

---

*Last reviewed at suite version: v1.0.0-alpha.19 release*
