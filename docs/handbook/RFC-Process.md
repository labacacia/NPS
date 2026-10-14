# RFC Process

**Status:** ✅ Reviewed for v1.0.0-alpha.19 release

An RFC (Request for Comments) is the formal mechanism for proposing and deciding non-trivial changes to the NPS suite. The source documents live in `spec/rfcs/` inside the NPS-Dev monorepo.

> **Rule of thumb:** if landing the PR would force every SDK to either adopt or explicitly reject the change, it needs an RFC.

---

## RFC vs CR

| Aspect | RFC | [CR](./CR-Process.md) |
|--------|-----|-----------------|
| Phase | Post-1.0 stable | Pre-1.0 (alpha / beta) |
| Gating | Required — Accepted on `dev` PR before any implementation | None — implementation may proceed without external acceptance |
| Reviewers | Shepherd + community discussion window | Author + optional independent reviewer |
| Numbering | `NPS-RFC-NNNN` | `NPS-CR-NNNN` |
| Lifecycle | Draft → Accepted → Active → (Superseded) | Draft → Implemented (or Withdrawn) |
| Chinese counterpart | Required | Not required |

Once the suite reaches v1.0.0, all spec-affecting changes **MUST** be drafted as RFCs. Before v1.0.0 (while still in alpha / beta), CRs are the lighter-weight mechanism.

---

## When to Write an RFC

An RFC **MUST** be opened before any PR that:

- Assigns a Reserved bit, code, or namespace (e.g. an encoding tier, a new frame type, a new NPS status code range)
- Adds or removes a frame in `spec/frame-registry.yaml`
- Introduces a new node type or endpoint sub-path
- Changes on-the-wire semantics of an existing frame (breaking or silent)
- Defines a new authentication method or key algorithm
- Adds a new protocol layer to the suite (`NPS-6-*`)
- Raises the minimum Agent version (`min_agent_version` bump)

An RFC **SHOULD** be opened (but may be skipped with shepherd consent) for:

- Large cross-SDK refactors
- New compatibility bridges (e.g. a third-party protocol adapter)
- Policy changes: versioning, deprecation windows, release cadence

An RFC is **NOT** required for:

- Bug fixes and spec clarifications (typos, ambiguity resolution)
- Non-breaking SDK additions (new convenience helpers, new optional fields in already-extensible structs)
- Documentation, benchmarks, examples, CI changes
- Tool implementations (e.g. a new NIP CA Server backend)

---

## Template: `spec/rfcs/template.md`

Copy `spec/rfcs/template.md` when starting a new RFC. The sections and what each means:

| Section | Purpose |
|---------|---------|
| **Front matter** | RFC number, status, author, shepherd, dates, affected specs and SDKs. Fill in on PR open; the shepherd fills in `Accepted` and `Activated` dates. |
| **§1 Summary** | Two to three sentences. State *what* changes and *who* is affected — not yet *why*. Sufficient for a reader to decide "is this relevant to me?" |
| **§2 Motivation** | What problem does this solve? Why now? Use concrete data: measured latency, measured size, a real incident. If responding to external pressure (IANA policy, security disclosure), link to it. |
| **§3 Non-Goals** | Explicit scope limits. "This RFC does **not** address X" prevents weeks of review-cycle drift. |
| **§4 Detailed Design** | The normative content: §4.1 Wire Format / Frame Changes (bit-exact layout, field table, `frame-registry.yaml` diff inline); §4.2 Manifest / NWM changes; §4.3 Error Codes (with NPS status code mappings); §4.4 State machines / flows; §4.5 Backward compatibility analysis. |
| **§5 Alternatives Considered** | At least two alternatives including "do nothing." Spell out what the do-nothing option costs. |
| **§6 Drawbacks & Risks** | Migration cost, attack surface, ecosystem fragmentation, reversibility. |
| **§7 Security Considerations** | Mandatory — even "none" must be stated. New parser paths, new crypto assumptions, DoS vectors. |
| **§8 Implementation Plan** | Phasing table (at most 4 phases, each individually shippable and reversible); SDK Coverage Matrix (each SDK owner signs off or declares a follow-up); test plan; benchmarks. |
| **§9 Empirical Data** | Numbers: benchmarks, prototype measurements. "It will probably be faster" is not data. |
| **§10 Open Questions** | Unresolved items. Each entry MUST have a resolution or explicit defer-with-tracking-issue before the RFC moves to Accepted. |
| **§11 Future Work** | What this RFC deliberately defers. Link to follow-up RFCs when they open. |
| **§12 References** | Related RFCs, external specs, prior art, discussion threads. |
| **Appendix A** | Revision history table. |

The Chinese counterpart (`NPS-RFC-NNNN-{slug}.cn.md`) is required alongside the English primary.

---

## Status Lifecycle

```
Draft ─────► Accepted ─────► Active ─────► Superseded
 │               │
 │               └──► Withdrawn   (author pulls it)
 └──► Rejected
```

| State | Meaning | How to change |
|-------|---------|---------------|
| `Draft` | Author is writing / revising; PR open against `dev` | Author pushes commits |
| `Accepted` | Merged to `dev`; implementation may begin across SDKs | Shepherd merges PR after approval threshold met |
| `Active` | Implementation landed in ≥1 reference SDK; frame/spec changes integrated | Follow-up PR flips header; requires ≥1 SDK + green CI |
| `Superseded` | A later RFC replaces this one | The replacing RFC sets `Supersedes:` and the old one is edited to set `Superseded-By:` |
| `Withdrawn` | Author no longer pursues it; preserved as history | Author edits header |
| `Rejected` | Discussion reached "no" consensus; preserved as history | Shepherd edits header with summary of why |

Rejected and Withdrawn RFCs are **never deleted** — the argument is the deliverable.

---

## Approval Threshold

Acceptance requires, at minimum:

- **1 shepherd** (a maintainer who is not the author) explicitly approves the PR
- **≥ 7 calendar days** of public discussion window (longer for breaking changes — see table below)
- **Open questions** resolved or explicitly deferred with tracking issues
- **SDK-coverage matrix** filled: each SDK owner either signs off or declares a non-blocking follow-up

Extended windows:

| Change type | Minimum discussion window |
|-------------|---------------------------|
| New frame / new Reserved bit assignment | 14 days |
| Breaking wire-format change | 21 days |
| Raising `min_agent_version` | 21 days + one month deprecation notice |

---

## Phased Delivery

Some RFCs define multi-phase rollouts where each phase is a separately shippable increment. The implementation plan (§8.1) carries the phase table. Appendix A (or a dedicated Appendix) tracks which phase is currently active.

Typical phase structure:

| Phase | Scope | Exit criterion |
|-------|-------|----------------|
| 1 | Spec merged + reference .NET SDK behind feature flag | Unit tests green; one interop test between two SDKs |
| 2 | All 6 SDKs implement; flag still default-off | Cross-SDK interop matrix green |
| 3 | Flag flips to default-on | No regressions in benchmark suites for 1 release cycle |
| 4 | Remove flag; behavior mandatory | Deprecated fallback codepath removed |

### The Flag-Day Rule for Phase 3

Any RFC phase that changes wire semantics for existing deployments requires **≥ 21-day advance notice** posted in NPS-Dev GitHub Discussions before the phase activates. This rule exists to protect operators who cannot do an immediate upgrade the moment a new alpha ships. RFC-0003 §8.1 documents this requirement explicitly.

---

## Numbering and Filenames

- `NPS-RFC-{NNNN}-{kebab-slug}.md` — English primary
- `NPS-RFC-{NNNN}-{kebab-slug}.cn.md` — Chinese mirror

`NNNN` is a zero-padded four-digit number assigned on PR open, monotonically increasing. Never reuse a number, even for Withdrawn / Rejected RFCs.

---

## How to Open an RFC

1. Copy `spec/rfcs/template.md` → `NPS-RFC-{next}-{slug}.md`
2. Copy `spec/rfcs/template.cn.md` → `NPS-RFC-{next}-{slug}.cn.md` and translate
3. Fill in the front matter (`Status: Draft`)
4. Open PR against `dev`, title format: `rfc: NPS-RFC-{NNNN} — {one-line summary}`
5. Tag at least one shepherd for review
6. Iterate on the PR; squash-merge is acceptable — the argument inside the RFC is what must be preserved

Once merged to `dev` with `Status: Accepted`, implementation work can start in any SDK. The RFC's `Status:` is updated to `Active` in a follow-up PR once at least one reference SDK ships the change.

---

## Worked Examples

### RFC-0001 — Add NCP connection preamble (smallest, cleanest example)

**What it does:** Defines an 8-byte constant preamble `b"NPS/1.0\n"` that every NCP native-mode client MUST send immediately after the transport handshake and before its first `HelloFrame`. HTTP mode is not affected. Adds one error code (`NCP-PREAMBLE-INVALID`) and one status code (`NPS-PROTO-PREAMBLE-INVALID`).

**Why it is the simplest example:** Single-phase delivery. Single-concern scope. Accepted via pre-1.0 fast-track (shepherd = author, expedited window because it had no alternative design space). The motivation is verifiable: a constant preamble lets the server reject misrouted traffic in the first `read(8)` with zero frame-parser exposure, and gives NPS 2.x a clean wire-level compatibility gate against 1.x servers.

**Lesson:** An RFC this narrow usually indicates a well-scoped problem. If yours cannot be explained in 2–3 sentences of motivation, consider narrowing scope before opening the PR.

---

### RFC-0002 — Adopt X.509 + ACME for NID certificates (long-running EXPERIMENTAL)

This RFC spent a deliberately extended period in a `Proposed` / EXPERIMENTAL state because one of its normative requirements — assigning a private OID for the `nid-assurance-level` X.509 extension — depended on receipt of an IANA Private Enterprise Number (PEN). While the PEN was pending, the RFC used a provisional OID (`1.3.6.1.4.1.99999.1`) and carried an EXPERIMENTAL gate in the spec text.

**The dependency has since resolved:** IANA **PEN 65715** was assigned 2026-05-08. CR-0004 wired it in — all NPS X.509 OIDs now anchor to `1.3.6.1.4.1.65715`, replacing the provisional `1.3.6.1.4.1.99999` arc. Certificates issued under the old arc MUST be revoked and re-issued.

**Lesson for multi-dependency RFCs:** gate the provisional behavior explicitly in the spec text so operators know exactly when the behavior will stabilize. Track the external dependency (the IANA PEN submission) in an open issue linked from the RFC's §10 Open Questions. Do not block other work on the external dependency; mark the gated sections clearly and move on.

---

### RFC-0004 — NID reputation log (multi-phase rollout across three alpha versions)

This RFC introduced an append-only, Certificate-Transparency-style audit log for NID events. It was delivered in three phases across three alpha releases:

- **Phase 1 (alpha.3):** Core log append and per-entry query. `.NET` reference implementation only.
- **Phase 2 (alpha.4):** K-of-N sync barrier; cross-SDK client ports.
- **Phase 3 (alpha.5):** STH gossip federation — `GossipState` + `GossipService` + `GET /v1/log/gossip/sth`. 13 new tests in the `.NET` test suite.

**The flag-day notice in practice:** Phase 3 activated a gossip endpoint visible to peer nodes. The required 21-day advance notice was posted in GitHub Discussions before the alpha.5 release tag was created. Each phase exit criterion in §8.1 was verified by CI green before the phase was marked active in the RFC header.

**Lesson:** Multi-phase RFCs need a clear exit criterion per phase. Without it, "Phase 2 is done" becomes an argument, not a fact.

---

### RFC-0005 — Reputation Policy Enforcement

This RFC builds on RFC-0004's reputation log by defining how reputation signals are *enforced* as policy (rather than merely recorded). It governs how nodes consult the append-only reputation log when making trust decisions.

**Lesson:** RFC-0005 illustrates the natural progression from an observability primitive (RFC-0004's CT-style log) to a policy layer that consumes it — a follow-on RFC referencing the §11 Future Work of its predecessor.

---

### RFC-0006 — NCP native-mode transport (Draft)

**Status: Draft.** This RFC specifies NCP's native (non-HTTP) transport: TCP length-prefix framing with the `NcpNativeClient` / `NcpServer` / `NcpSession` surface (the .NET reference implementation landed in alpha.11). It pairs with RFC-0001's connection preamble, which every native-mode client MUST send before its first `HelloFrame`.

**Lesson:** A `Draft` RFC can still have reference-implementation work underway behind the feature flag (per the Phase 1 exit criterion) while the wire spec is finalized.

---

## Current RFCs at a Glance

| RFC | Title | Status |
|-----|-------|--------|
| RFC-0001 | NCP connection preamble | Active |
| RFC-0002 | X.509 + ACME for NID certificates | Active (PEN 65715 assigned; provisional OID retired via CR-0004) |
| RFC-0004 | NID reputation log | Active |
| RFC-0005 | Reputation Policy Enforcement | Active |
| RFC-0006 | NCP native-mode transport | Draft |

---

## Related Pages

- [CR Process](./CR-Process.md) — lightweight pre-1.0 change mechanism
- [Specs Index](./Specs-Index.md) — index of all RFCs and CRs alongside protocol specs

---

*Last reviewed at suite version: v1.0.0-alpha.19 release*
