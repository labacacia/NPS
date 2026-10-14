# CR (Change Request) Process

**Status:** ✅ Reviewed for v1.0.0-alpha.19 release

A Change Request (CR) is the lightweight design artifact used during the **pre-1.0** phase of the NPS suite to record the intent, motivation, and shape of a specification or implementation change before (or alongside) the code that lands it. The source documents live in `spec/cr/` inside NPS-Dev.

---

## CR vs RFC

| Aspect | CR (this process) | [RFC](./RFC-Process.md) |
|--------|-------------------|---------------------|
| Phase | Pre-1.0 (alpha / beta) | Post-1.0 stable |
| Gating | None — implementation may proceed without external acceptance | Required — Accepted on `dev` PR before any implementation |
| Reviewers | Author + optionally one independent reviewer | Shepherd + community discussion window |
| Numbering | `NPS-CR-NNNN` | `NPS-RFC-NNNN` |
| Lifecycle | Draft → Implemented (or Withdrawn) | Draft → Accepted → Active → (Superseded) |
| Status retention | Kept as historical record after implementation | Permanent record; supersession via new RFC |
| Chinese counterpart | Not required | Required |

CRs exist because, while the suite is still 0.x and has no production deployments to protect, the friction of the full RFC process (shepherd assignment, formal discussion window, accepted-on-merge ceremony) buys little. Once the suite reaches v1.0.0, all spec-affecting changes **MUST** instead be drafted as RFCs.

---

## When to Use a CR

Use a CR when the change is:

- **Scoped and near-term** — ships in the next alpha, affects a bounded surface
- **Not introducing genuinely new protocol surface** — renaming an existing field or value, splitting an existing node type, deprecating a capability, adding a query subtype to an existing namespace
- **Not requiring cross-community discussion** — the decision is internally obvious and can be reviewed by the author + one maintainer

Concrete examples of CR-appropriate changes:

- Renaming `node_kind` → `node_roles` in the NDP AnnounceFrame
- Splitting the `Gateway Node` type into `Anchor Node` + `Bridge Node`
- Adding `topology.snapshot` / `topology.stream` query subtypes to the Anchor Node namespace
- Deprecating the `gateway` wire value with a clear error and alias window

Use an [RFC](./RFC-Process.md) instead when:

- The change introduces a new frame type or assigns a Reserved bit
- The change affects wire semantics for existing implementations in a breaking way
- The change requires broad community consensus or an extended discussion window

---

## CR Structure

A CR document covers, at minimum:

1. **Summary** — one paragraph stating what changes
2. **Motivation** — why this change is needed now
3. **Specification changes** — exact text or diff for `spec/` files
4. **SDK changes** — language-by-language impact
5. **Conformance changes** — added or modified test items
6. **Migration impact** — internal and external users
7. **Out of scope** — explicit non-changes to prevent scope creep
8. **Acceptance criteria** — concrete completion checklist
9. **CHANGELOG entry** — proposed text
10. **Open questions** — items the author wants reviewers to weigh in on

There is no formal template file; use an existing CR (e.g. CR-0001) as the structural model. The structure mirrors a formal RFC closely enough that a CR can be promoted to an RFC verbatim if it is still pending when v1.0.0 is cut.

---

## Hard Rejection vs Alias-Acceptance Pattern

When a CR retires an old value or field name, there are two strategies for how the implementation handles old inputs:

### Hard Rejection

Old value immediately triggers a specific `-REMOVED` error. No silent fallthrough.

**When to use:** Write paths that produce outbound traffic. If old values slip through undetected on write, downstream nodes may silently misinterpret them. Hard rejection makes the breakage immediate and visible.

**Example — CR-0001, `node_type: "gateway"` in NDP AnnounceFrame:**
- Any node that announces `node_type: "gateway"` receives `NDP-ANNOUNCE-ROLE-REMOVED`
- The error response SHOULD include a `hint` pointing to NPS-CR-0001 and specifying the replacement values (`"anchor"` or `"bridge"`)
- No grace period; takes effect in the same alpha that introduces the change

### Alias-Acceptance

Old value is silently accepted through a specified alpha version, with a deprecation log emitted. The implementation maps the old value to the new one internally.

**When to use:** Read paths where old inputs arrive from external callers who have not yet updated. Silent acceptance prevents unexpected 4xx errors on upgrade; the deprecation log makes the alias visible in operator logs.

**Example — CR-0001, `node_kind` field alias in NDP AnnounceFrame:**
- Parsers MUST accept the legacy field name `node_kind` as an alias for `node_roles` during the alpha transition window
- `AnchorNodeMiddleware` instruments `node_kind` usage in telemetry so operators can identify callers that have not migrated
- The alias window was specified as "through alpha.5" — meaning the field is removed in alpha.6

**Rule:** prefer alias-acceptance for read paths; hard rejection for write paths that could cause downstream silent failures.

---

## The "Alias Accepted Through alpha.N" Timing Rule

When a CR specifies an alias window, the **removal version must be stated explicitly** in the CR document and must be at least one full alpha cycle ahead of the alpha that introduces the change.

Example: CR-0001 landed in alpha.3 and declared that `node_kind` is accepted through alpha.5. This gives operators two full release cycles to migrate. Removal in alpha.6 is the earliest acceptable removal point.

The removal commitment is binding. If removal slips, a new CR entry must be filed extending the window and updating the deprecation log message to reflect the new target.

---

## How to Author a CR

1. Pick the next available `NNNN` from the index in `spec/cr/README.md`
2. Copy the structure of an existing CR (e.g. CR-0001) — there is no formal template file; the structure is intentionally light
3. Write English (`NPS-CR-NNNN-<slug>.md`) — a Chinese counterpart is not required for CRs; they are informal pre-1.0 planning artifacts. The downstream `spec/*.md` text that lands the change will carry the standard EN + CN pair per project convention
4. Add the row to the Index table in `spec/cr/README.md`
5. Open a `feat/cr-NNNN-<slug>` branch and start implementing

---

## Worked Examples

### CR-0001 — Gateway Node → Anchor Node + Bridge Node

**What it changed:** The NWP node taxonomy had a single `Gateway Node` type carrying two distinct roles (cluster control plane and external entrypoint vs. stateless NPS↔non-NPS protocol translation). CR-0001 split it into `Anchor Node` (cluster-entry / NOP-routing) and `Bridge Node` (NPS→external protocol translation).

**Hard rejection on write path:** Any implementation that announces `node_type: "gateway"` receives `NWP-MANIFEST-NODE-TYPE-REMOVED`. Any implementation that includes `node_roles: ["gateway"]` or `node_kind: "gateway"` in an AnnounceFrame receives `NDP-ANNOUNCE-ROLE-REMOVED`. No grace period.

**Alias-acceptance on read path:** The legacy field name `node_kind` is accepted as an alias for `node_roles` through alpha.5. `AnchorNodeMiddleware` instruments usage in telemetry.

**Direction note for package naming:** The pre-existing `compat/{mcp,a2a,grpc}-bridge` packages carry the *inverse* direction (external → NPS ingress). CR-0001 renamed these to `compat/{mcp,a2a,grpc}-ingress` to free the "Bridge" word for the new NPS→external Bridge Node type. The corresponding GitHub/Gitee repos were renamed `NPS-{mcp,a2a,grpc}-ingress` as part of the alpha.3 release sync.

**Status:** Implemented in v1.0-alpha.3 (2026-04-26).

---

### CR-0002 — Standard topology query types for Anchor Node

**What it changed:** CR-0001 introduced the Anchor Node type and stated that Anchor Nodes maintain cluster topology but did not specify how that topology is read. CR-0002 filled the gap by reserving two standard query types: `topology.snapshot` (one-shot full topology retrieval via NWP `Query`) and `topology.stream` (continuous topology change feed via NWP `Subscribe`).

**No old value to reject:** CR-0002 is purely additive — no existing field or value was retired. There is no hard-rejection case and no alias-acceptance case. The only migration concern is that implementations must add the new query handlers.

**Status:** Implemented in v1.0-alpha.4 (2026-04-27). `topology.snapshot` and `topology.stream` are mandatory at NPS-AaaS Profile L2 and above.

---

### CR-0006 — NWP SubscribeFrame

**What it changed:** Formalized NWP `SubscribeFrame` as §13 of the NWP spec — a `subscription_id` (UUID v4), a QueryFrame-compatible filter, `heartbeat_interval_ms`, `max_events`, and an opaque `cursor` for lossless resume. It also promoted the `topology:subscribe` capability from SHOULD to MUST in the authorization model (NWP §12.4) and standardized the `bridge_target` schema (`protocol`, `endpoint`, `headers`).

**Relation to CR-0002:** CR-0002 reserved the `topology.stream` query subtype; CR-0006 specifies the concrete frame and subscription lifecycle that delivers that stream.

**Status:** Accepted 2026-05-28. Landed in NWP v0.13.

---

### CR-0007 — NOP L3 runtime integration

**What it changed:** Integrated the NOP L3 runtime with the `nps-runner` daemon via a task lease, so orchestrated NOP tasks can be executed by a runtime lease holder.

**Status:** Implemented (NOP v0.7 / `nps-runner`).

---

## Current CRs at a Glance

| CR | Title | Status |
|----|-------|--------|
| CR-0001 | Gateway Node → Anchor + Bridge Node | Implemented (alpha.3) |
| CR-0002 | Standard topology query types | Implemented (alpha.4) |
| CR-0003 | Group / session NIDs | Implemented (NIP) |
| CR-0004 | IANA PEN 65715 wire-in | Implemented 2026-05-08 (landed v1.0-alpha.6) |
| CR-0005 | NIP-CA RA model | Implemented (landed v1.0-alpha.7) |
| CR-0006 | NWP SubscribeFrame | Accepted 2026-05-28 (landed v1.0.0-alpha.11, NWP v0.13) |
| CR-0007 | NOP L3 runtime integration | Implemented (NOP v0.7) |

---

## Related Pages

- [RFC Process](./RFC-Process.md) — formal post-1.0 change mechanism
- [Specs Index](./Specs-Index.md) — index of all CRs and RFCs alongside protocol specs

---

*Last reviewed at suite version: v1.0.0-alpha.19 release*
