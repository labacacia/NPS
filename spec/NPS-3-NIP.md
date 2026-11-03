English | [中文版](./NPS-3-NIP.cn.md)

# NPS-3: Neural Identity Protocol (NIP)

**Spec Number**: NPS-3
**Status**: Proposed
**Version**: 0.16
**Date**: 2026-09-09
**Port**: 17433 (default, shared) / 17435 (optional dedicated)
**Authors**: Ori Lynn / INNO LOTUS PTY LTD
**Depends-On**: NPS-1 (NCP v0.13)

---

## 1. Terminology

The key words "MUST", "MUST NOT", "SHOULD", and "MAY" in this document are to be interpreted as described in RFC 2119.

---

## 2. Protocol Overview

NIP issues a verifiable Neural Identity (NID) to every AI agent, NWP node, and human operator, carrying capability declarations and scope-bound permissions. It supports trust-chain propagation and real-time revocation. NIP is the heart of the NPS security model.

### 2.1 Actors

| Role | Description |
|------|-------------|
| Root CA | Root certificate authority; stored offline |
| Org CA | Organization intermediate CA; one per organization |
| Agent | AI agent holding an NID certificate |
| Node | NWP node holding an NID certificate |
| Operator | Human administrator holding an Operator certificate |
| Verifier | The party verifying an NID (usually a Node) |

### 2.2 CA Hierarchy

```
Root CA (offline, rarely used)
    │
    └── Org Intermediate CA (one per org, 1-year validity)
            ├── Agent Certificate    (30-day validity, auto-renewal supported)
            ├── Node Certificate     (90-day validity)
            └── Operator Certificate (1-year validity, bound to MFA)
```

---

## 3. NID Format

```abnf
nid           = "urn:nps:" entity-type ":" issuer-domain ":" identifier
entity-type   = "agent" / "node" / "org"
issuer-domain = <RFC 1034 domain>
identifier    = 1*(ALPHA / DIGIT / "-" / "_" / ".")
```

**Examples**

```
urn:nps:agent:ca.innolotus.com:550e8400-e29b-41d4    ← AI agent
urn:nps:node:api.myapp.com:products                   ← NWP node
urn:nps:org:mycompany.com                              ← Organization CA
```

### 3.1 Reserved identifier prefixes (NPS-CR-0003)

Two identifier prefixes are reserved on NIDs of `entity-type = agent` and signal a structural role within the orchestrator / session lineage model. Both follow the existing identifier ABNF (`1*(ALPHA / DIGIT / "-" / "_" / ".")`).

| Prefix | Role | Example |
|--------|------|---------|
| `group-` | Orchestrator group NID — the trust anchor for a fleet of session NIDs issued by the CA on the group's behalf. Longer-lived (default 365 days) and revocable. | `urn:nps:agent:ca.example.com:group-7f3c9e1a-b2d8-4c6f-9a01` |
| `session-` | Short-lived session NID issued under a group. The portion after `session-` MUST be of the form `{unix-timestamp}-{random}` where `{random}` is at least 8 hex characters. Default validity 1 hour, max 24 hours. | `urn:nps:agent:ca.example.com:session-1714672800-f3a92c0b` |

Identifiers without these prefixes continue to behave as ordinary agent NIDs. Receivers MUST NOT reject a NID solely on its prefix; the prefix is informational and the authoritative role is carried in the signed `IdentFrame.lineage.role` field (§5.1.3). The protocol-level `nop:orchestrate` capability (NPS-5) continues to express the *role* of being an orchestrator and is independent of this prefix convention.

---

## 4. Signature Algorithms

| Algorithm | Use | Key length |
|-----------|-----|-----------|
| **Ed25519** (primary) | High-frequency agent verification | 32-byte private key / 32-byte public key |
| **ECDSA P-256** (fallback) | Compatibility scenarios | 32-byte private key / 64-byte public key |

Public-key encoding: `{algorithm}:{base64url(DER)}`, e.g. `ed25519:MCowBQYDK2VwAyEA...`.

---

## 5. Frame Types

### 5.1 IdentFrame (0x20)

Agent identity declaration carrying the certificate. Sent as the handshake frame on every new connection.

**Field definitions**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `frame` | uint8 | required | Fixed value `0x20` |
| `nid` | string | required | Agent NID |
| `pub_key` | string | required | Public key; format `{alg}:{base64url}` |
| `capabilities` | array | required | Capabilities held by the agent |
| `scope` | object | required | Access-scope declaration |
| `issued_by` | string | required | Issuer NID (Org CA) |
| `issued_at` | string | required | Issue time (ISO 8601 UTC) |
| `expires_at` | string | required | Expiration time (ISO 8601 UTC) |
| `serial` | string | required | Certificate serial (globally unique per Org CA, hex) |
| `signature` | string | required | CA's signature over this frame; format `{alg}:{base64url}` |
| `cert_format` | string (enum) | required | Certificate encoding format. One of `"x509-der"` / `"raw-pubkey"`. Excluded from the signed canonical JSON (see §5.1.3 canonicalisation note). |
| `cert_chain` | array of string | required when `cert_format = "x509-der"` | DER-encoded certificate chain, base64url-encoded, leaf first. When `cert_format = "raw-pubkey"`, `cert_chain` MUST be omitted (not present in the serialised frame and not included in the canonical form). Excluded from the signed canonical JSON (see §5.1.3 canonicalisation note). |
| `metadata` | object | optional | Agent metadata — see below |
| `assurance_level` | string | optional | One of `"anonymous"` / `"attested"` / `"verified"` (see §5.1.1). REQUIRED when the NID's certificate carries the `id-nid-assurance-level` extension. Receivers MUST treat an absent field as `"anonymous"` (backward compatibility with v1.0-alpha.2 publishers). When both field and cert extension are present, they MUST carry the same value — **phase gate**: Phase 1–2 (current) enforcement is opt-in (SHOULD check, MAY enforce); starting Phase 3 flag day (see NPS-RFC-0003 §8.1) enforcement is MUST, violation returns `NIP-ASSURANCE-MISMATCH`. (NPS-RFC-0003) |
| `lineage` | object | optional | Signed lineage metadata. Present when the NID is an orchestrator group (`role = "group"`) or a short-lived session (`role = "session"`). See §5.1.3. (NPS-CR-0003) |
| `ocsp_staple` | string | optional | Base64url-encoded DER OCSP response stapled by the Agent at send time (NIP v0.9 §5.1.4). Receivers SHOULD verify the staple signature; an expired staple returns `NIP-OCSP-STAPLE-EXPIRED`. When absent, receivers MAY perform an online OCSP lookup per the `ocsp_url` declared in the NWM. |
| `node_roles` | array[string] | optional | Self-declared node-role tags, e.g. `["memory", "orchestrator"]` (NIP v0.10). Same vocabulary as NDP `AnnounceFrame.node_roles`. Phase 1–2: self-declared, informational. Phase 3 flag day: MUST match the `id-nps-node-roles` X.509 extension (`1.3.6.1.4.1.65715.2.2`); mismatch returns `NIP-CERT-NODE-ROLES-MISMATCH`. |

**`metadata` fields (optional)**

| Field | Type | Description |
|-------|------|-------------|
| `model_family` | string | Model-family identifier used by the agent, e.g. `"openai/gpt-4o"`, `"anthropic/claude-4"` |
| `tokenizer` | string | Tokenizer identifier used by the agent, e.g. `"cl100k_base"` |
| `runtime` | string | Agent runtime identifier, e.g. `"langchain/0.2"`, `"autogen/0.4"` |

The `metadata` field is not included in the signature computation; an agent MAY set it at runtime. Nodes use the `tokenizer` in `metadata` to perform CGN auto-matching (see [token-budget.md](./token-budget.md)).

**Trust boundary for unsigned `metadata` (normative — closes issue #39)**

Because `metadata` is outside the signature, every value inside it is **agent-supplied and unverified**. Nodes MUST NOT use any field of `IdentFrame.metadata` (including but not limited to `model_family`, `tokenizer`, and `runtime`) as input to:

1. **Billing or settlement** — actual CGN charged to a tenant or counted against a paid quota,
2. **Quota elevation** — granting a larger token budget, higher concurrency, or any rate-limit tier above the default,
3. **Reputation scoring** — any persisted trust signal that affects future routing or pricing,
4. **Security or authorization decisions** — admittance to capability-gated endpoints, policy bypasses, or assurance-level checks.

For these uses a Node MUST instead consume a **verified** signal — an attribute carried in the signed IdentFrame body, the X.509 NID certificate (NPS-RFC-0002 extensions), or a CA-attested side channel — or a Node-internal **observed** measurement. Treating unsigned metadata as authoritative is a conformance violation.

**Three-tier tokenizer trust model**

To make the boundary above operational for token accounting, NPS defines three distinct tokenizer signals. Implementations MUST keep them separable in code paths and logs.

| Tier | Source | Trust | Permitted uses |
|------|--------|-------|----------------|
| `declared_tokenizer` | Agent-supplied via `IdentFrame.metadata.tokenizer` or the `X-NWP-Tokenizer` request header. **Unsigned.** | Agent self-claim only. | **Estimation hints only**: shaping pre-flight CGN estimates, cache-key disambiguation, telemetry. MUST NOT drive billing, quota elevation, reputation, or security. |
| `verified_tokenizer` | CA-attested or platform-attested binding of a tokenizer identifier to the NID — e.g. an X.509 NID-cert extension, an AaaS-operator attestation under NPS-AaaS-Profile, or another signed channel agreed by the deployment. | Cryptographically verifiable. | Authoritative for policy, billing, quota tiers, and assurance-gated access. Settlement-grade flows MUST gate on this tier. |
| `observed_tokenizer_profile` | Node-side statistical or behavioral inference (e.g. byte-length distributions, response-shape fingerprinting, sampled re-tokenization). | Node-internal measurement. | Abuse detection, drift alerts, anomaly scoring, and inputs to a Node's own reputation engine. MUST NOT be exported to peers as a verified claim. |

When more than one tier is available the Node MUST prefer the highest-trust tier for any decision the boundary above restricts; lower tiers MAY be retained alongside for diagnostic comparison.

**Standard `capabilities` values**

| Capability | Description |
|------------|-------------|
| `nwp:query` | May query Memory Nodes |
| `nwp:action` | May invoke Action Nodes |
| `nwp:stream` | May receive StreamFrame responses |
| `ncp:stream` | May initiate NCP streaming |
| `nop:delegate` | May delegate subtasks to other agents |
| `nop:orchestrate` | May act as an orchestrator and emit TaskFrames |
| `topology:read` | May read Anchor Node topology data via reserved query types `topology.snapshot` / `topology.stream` (NPS-2 §12); Anchor Nodes MUST require this capability at Phase 1–2 per NPS-2 §12.4. Self-declared and signed at Phase 1–2; CA-attested role binding deferred to Phase 3 (RFC-0002 amendment). |
| `llm:complete` | May invoke the standard NWP `llm.complete` action on an LLM/Thinking Profile node (NPS-2 §4.2a, §7.5) |
| `llm:context` | May operate owner-bound stateful LLM contexts under NWP §7.6. Create/append/fork/reset also require `llm:complete`; status/release require this capability plus ownership only. Possession of a context ID grants no authority. |
| `llm:stream` | May receive streaming LLM completion responses (`LlmCompleteActionRequest.stream = true`) |
| `llm:tool_call` | May submit tool definitions and receive tool-call requests in LLM completion flows |
| `llm:embed` | May invoke standard embedding actions when a node advertises embedding support |
| `llm:rerank` | May invoke standard rerank actions when a node advertises rerank support |

**`scope` field**

```json
{
  "nodes":            ["nwp://api.myapp.com/*"],
  "actions":          ["orders:read", "orders:create"],
  "max_token_budget": 50000
}
```

**Signature computation**

The signature is computed over the canonical JSON of the IdentFrame with the `signature` field removed (keys sorted alphabetically, no whitespace).

**Full example**

```json
{
  "frame": "0x20",
  "nid": "urn:nps:agent:ca.innolotus.com:550e8400-e29b-41d4",
  "pub_key": "ed25519:MCowBQYDK2VwAyEA...",
  "capabilities": ["nwp:query", "nwp:action", "ncp:stream"],
  "scope": {
    "nodes":            ["nwp://api.myapp.com/*"],
    "actions":          ["orders:read", "orders:create"],
    "max_token_budget": 50000
  },
  "issued_by":  "urn:nps:org:mycompany.com",
  "issued_at":  "2026-04-10T00:00:00Z",
  "expires_at": "2026-05-10T00:00:00Z",
  "serial":     "0x0A3F9C",
  "signature":  "ed25519:3045022100..."
}
```

---

### 5.1.1 Assurance Levels (NPS-RFC-0003)

NPS defines three **assurance levels** an Agent identity may carry, modelled on NIST SP 800-63 IAL and CA/B Forum DV/OV/EV. The level travels in `IdentFrame.assurance_level` and is the source of truth for Node policy decisions (`NWM.min_assurance_level`); when X.509 NID certificates are in use (NPS-RFC-0002), the cert MUST also carry the `id-nid-assurance-level` extension and the two MUST agree — **phase gate**: enforcement is opt-in in Phase 1–2 (current); mandatory starting Phase 3 flag day (see NPS-RFC-0003 §8.1).

| Level | Enum value | Minimum CA criteria | Typical use |
|-------|------------|---------------------|-------------|
| L0 | `"anonymous"` | Self-signed, OR CA-signed without out-of-band identity binding | Hobbyist Agents, dev / test, free read-only endpoints |
| L1 | `"attested"` | NID signed by an RFC-0002-compliant CA; CA attests possession of the NID private key (e.g. ACME `agent-01` challenge); contact email or domain verified. The `id-nid-assurance-level` X.509 extension uses IANA-assigned OID `1.3.6.1.4.1.65715.2.1`; the companion `id-nps-node-roles` extension uses `1.3.6.1.4.1.65715.2.2` (PEN 65715 assigned to LabAcacia 2026-05-08, NPS-CR-0004; see NPS-RFC-0002 §10 OQ-2). | Most production Agents; default rate-limit tier |
| L2 | `"verified"` | L1 criteria **plus** CA binds the operator's legal identity (corporate registration for org-NIDs, signed AaaS-operator attestation for hosted Agents) | Regulated integrations, paid premium tiers, contract-grade orchestration |

Default is `"anonymous"` — pre-RFC-0003 NIDs and any NID lacking the field are treated as L0. A Node demanding stricter levels declares `min_assurance_level` in its NWM (NPS-2 §4.1, §4.3).

The values are an **ordered enum**: `anonymous < attested < verified`. A request whose level is below the Node's required level MUST be rejected with `NWP-AUTH-ASSURANCE-TOO-LOW` (`NPS-AUTH-FORBIDDEN`).

**Forward compatibility**: An implementation receiving an `assurance_level` value not present in this enum MUST treat it as a protocol error and return `NIP-ASSURANCE-UNKNOWN` (`NPS-CLIENT-BAD-FRAME`). Implementations MUST NOT silently demote an unknown value to `anonymous` — silent demotion would create a security loophole for future higher-assurance levels introduced by a later spec revision.

See [NPS-RFC-0003](rfcs/NPS-RFC-0003-agent-identity-assurance-levels.md) for full design rationale, including the X.509 critical-extension flip planned in coordination with NPS-RFC-0002.

---

### 5.1.2 Reputation Log Entry (NPS-RFC-0004)

> Companion to assurance levels (§5.1.1). Where `assurance_level` answers *"who is this Agent?"*, reputation log entries answer *"how has this NID behaved?"* — published as a Certificate-Transparency-style append-only log so that any party (Node, auditor, downstream operator) can reach the same evidence about the same NID without a pre-existing relationship with the publisher.

A reputation entry is a signed JSON object that an issuer (typically an AaaS Anchor Node, an auditor, or a CA) publishes against a `subject_nid` to record one observed behavioral incident. Entries are aggregated by **log operators** that maintain an append-only history; the wire format below is the entry shape itself, independent of any specific log operator's HTTP surface (see [NPS-RFC-0004 §4.3](rfcs/NPS-RFC-0004-nid-reputation-log.md) for the operator interface).

**Field definitions**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `v` | uint8 | required | Schema version. This document defines `1`. |
| `log_id` | string (NID) | required | NID of the log operator that appended (or will append) this entry. MAY equal `issuer_nid` for self-published entries. |
| `seq` | uint64 | required | Monotonically increasing per-`log_id` sequence number. Set by the log operator on append. |
| `timestamp` | string (RFC 3339 UTC) | required | Log operator's commit time. |
| `subject_nid` | string (NID) | required | The NID this entry is *about*. |
| `incident` | string (enum) | required | One of the values in §5.1.2.1, or a forward-compatible unknown value (receivers MUST preserve unknown values). |
| `severity` | string (enum) | required | One of `info` / `minor` / `moderate` / `major` / `critical`. |
| `window` | object | optional | `{ start: ISO 8601, end: ISO 8601 }` describing the observation window. |
| `observation` | object | optional | Free-form, machine-readable, per-incident-type detail (e.g. `{requests: 45000, threshold: 300}` for a rate-limit violation). |
| `evidence_ref` | string (URL) | optional | URL where richer evidence (logs, transcripts) lives. |
| `evidence_sha256` | string (hex) | optional | SHA-256 of the evidence blob; lets queriers detect tampering with the off-log evidence. |
| `issuer_nid` | string (NID) | required | NID of the party making the assertion. MAY equal `log_id`. |
| `signature` | string | required | `{alg}:{base64url}` Ed25519 signature by `issuer_nid`'s private key over the **entry minus `signature`**, canonicalised per RFC 8785 (JCS). Log operators verify this signature, then re-sign the full entry with their `log_id` key for the ordering commitment (dual-signature model). |

#### 5.1.2.1 Incident vocabulary (initial)

| Value | Meaning |
|-------|---------|
| `cert-revoked` | CA revoked the `subject_nid`'s cert. |
| `rate-limit-violation` | Sustained violation of published rate limits. |
| `tos-violation` | Violated an AaaS Anchor Node's published terms. |
| `scraping-pattern` | Behavior matched scraper heuristics. |
| `payment-default` | CGN or fiat default on a committed transaction. |
| `contract-dispute` | Unresolved contractual breach on an async NOP task. |
| `impersonation-claim` | Third-party claim that `subject_nid` is impersonating them. |
| `positive-attestation` | Explicit positive signal (e.g. independent audit passed). |

Receivers MUST treat unknown `incident` values as opaque pass-through (forward compatibility); operator-side filters MAY ignore them. See [NPS-RFC-0004 §4.2](rfcs/NPS-RFC-0004-nid-reputation-log.md) for the long-form rationale.

**Errors**

| Code | When |
|------|------|
| `NIP-CONTEXT-INVALID` | `NPS-CLIENT-BAD-FRAME` | Invalid projection format, bounds or lifetime |
| `NIP-CONTEXT-UNTRUSTED` | `NPS-AUTH-FORBIDDEN` | Untrusted issuer/key or invalid signature |
| `NIP-CONTEXT-BINDING-MISMATCH` | `NPS-AUTH-FORBIDDEN` | Projection request/subject/audience or policy mismatch |
| `NIP-CONTEXT-EXPIRED` | `NPS-AUTH-FORBIDDEN` | Projection validity window expired |
| `NIP-CONTEXT-REPLAY` | `NPS-CLIENT-CONFLICT` | Valid projection was already admitted |
| `NIP-CONTEXT-VERIFIER-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | Verification or durable replay infrastructure unavailable |
| `NIP-REPUTATION-ENTRY-INVALID` (`NPS-CLIENT-BAD-FRAME`) | Entry signature fails verification or canonical form is malformed. |
| `NIP-REPUTATION-LOG-UNREACHABLE` (`NPS-DOWNSTREAM-UNAVAILABLE`) | A log operator referenced by a Node's `reputation_policy` cannot be reached during admission evaluation. |
| `NWP-AUTH-REPUTATION-BLOCKED` (`NPS-AUTH-FORBIDDEN`) | Reputation policy matched a `reject_on` rule on the requesting `subject_nid`. (Defined in NPS-2 §6 — Anchor / Memory / Action / Complex / Bridge Nodes that opt in.) |

> Phase 1 of NPS-RFC-0004 standardises the entry wire format and the signature rule above; the Merkle-tree / Signed Tree Head / inclusion-proof surface (RFC 9162-style) and the operator HTTP API land in Phase 2 alongside the NDP discovery hint. See the RFC for the full phasing.

---

### 5.1.3 Lineage (NPS-CR-0003)

`IdentFrame.lineage` carries the signed parent-chain metadata that links a short-lived session NID back to its orchestrator group NID, and that records who owns / authorised the group. The trust chain is:

```
human owner  →  Operator key (§2.1)  →  orchestrator group NID  →  short-lived session NID
```

`lineage` is part of the **signed** canonical JSON of the IdentFrame (unlike `metadata`, which is excluded). Modifying any sub-field invalidates the CA signature — this is what makes the chain *verifiable* end-to-end: a Node admitting a session NID can prove that `lineage.parent_nid` was authoritatively asserted by the CA at issuance time, not retro-fitted on the wire.

**Field definitions**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `role` | enum (`"group"` / `"session"`) | required when `lineage` is present | Lineage role of this NID. |
| `parent_nid` | string (NID) | required when `role = "session"` | Immediate parent NID. For a 1-level chain (group → session), equals `group_nid`. |
| `group_nid` | string (NID) | required when `role = "session"` | Group NID at the root of the chain. Equals `parent_nid` for a 1-level chain; reserved for future deeper chains. |
| `session_id` | string | required when `role = "session"` | Stable id of this execution; matches the `session-...` portion of the NID identifier (§3.1). Echoes back so consumers can index without parsing the URN. |
| `purpose` | string (≤256 UTF-8 bytes) | optional | Free-form, human-readable label for what this execution is for, e.g. `"order-classification-job"`. |
| `owner_user_id` | string | optional | Stable identifier of the human owner this group is acting on behalf of (e.g. internal user UUID). SHOULD be set when `role = "group"` and the deployment knows the owner. |
| `owner_key_id` | string | optional | `kid` hint identifying the owner-key (Operator key, OIDC `sub`, hardware-token id) that authorised creation of the group. |

**Group-NID example**

```json
"lineage": {
  "role":          "group",
  "owner_user_id": "user-7f3c9e1a",
  "owner_key_id":  "op-kid-2026-04"
}
```

**Session-NID example**

```json
"lineage": {
  "role":          "session",
  "parent_nid":    "urn:nps:agent:ca.example.com:group-7f3c9e1a-b2d8-4c6f-9a01",
  "group_nid":     "urn:nps:agent:ca.example.com:group-7f3c9e1a-b2d8-4c6f-9a01",
  "session_id":    "session-1714672800-f3a92c0b",
  "purpose":       "data-extraction-job-42",
  "owner_user_id": "user-7f3c9e1a",
  "owner_key_id":  "op-kid-2026-04"
}
```

**Backward compatibility**: pre-CR-0003 publishers omit `lineage`; pre-CR-0003 verifiers that strict-canonicalise (sort + omit `signature` / `metadata` / `cert_format` / `cert_chain`) will drop unknown fields if their DTO does not carry them, in which case signature verification of a CR-0003 frame fails. New senders aware of CR-0003 produce CR-0003 frames; old verifiers MUST be upgraded to verify them. Ordinary agent NIDs without `lineage` continue to verify exactly bit-compatibly.

**Errors**

| Code | When |
|------|------|
| `NIP-CA-GROUP-REVOKED` (`NPS-AUTH-FORBIDDEN`) | Cannot issue a session under a group that has been revoked. |
| `NIP-CA-PARENT-NOT-FOUND` (`NPS-CLIENT-NOT-FOUND`) | The `parent_nid` / group NID referenced by a session-issue request does not exist. |
| `NIP-CA-PARENT-NOT-GROUP` (`NPS-CLIENT-BAD-PARAM`) | The referenced parent NID exists but is not registered as `lineage.role = "group"`. |
| `NIP-CA-SESSION-VALIDITY-INVALID` (`NPS-CLIENT-BAD-PARAM`) | Requested session validity below 60 seconds or above the CA's configured maximum. |
| `NIP-CA-JWS-INVALID` (`NPS-AUTH-UNAUTHENTICATED`) | Group-JWS authorisation on a session-issue request fails signature, header, or shape validation. |
| `NIP-CA-JWS-EXPIRED` (`NPS-AUTH-UNAUTHENTICATED`) | Group-JWS `iat` outside the CA's clock-skew window (default ±5 minutes). |
| `NIP-CERT-PARENT-REVOKED` (`NPS-AUTH-UNAUTHENTICATED`) | A session NID's parent / group NID is revoked or expired (chain check, §7 step 3a). |

See [NPS-CR-0003](cr/NPS-CR-0003-orchestrator-group-session-nids.md) for the full motivation, JWS shape, and migration impact.

---

### 5.1.4 OCSP Stapling (NIP v0.9)

An Agent MAY attach a pre-fetched OCSP response to the `ocsp_staple` field of its IdentFrame, enabling the receiving Node to verify certificate revocation status without a live OCSP round-trip.

**Verification rules (receiver)**

1. Decode `ocsp_staple` from base64url to obtain the DER-encoded `OCSPResponse`.
2. Verify the OCSP response signature against the issuing CA's certificate (extracted from `cert_chain` or the Node's local trust store).
3. Check `thisUpdate` and `nextUpdate`: if the current time is after `nextUpdate`, return `NIP-OCSP-STAPLE-EXPIRED`.
4. Check `certStatus` in `SingleResponse` for the certificate identified by `serial`. If `revoked`, return `NIP-CERT-REVOKED`.
5. A staple that passes all checks supersedes any cached revocation state for this serial; the Node SHOULD NOT make a live OCSP request.

When `ocsp_staple` is absent, the Node MAY perform an online OCSP lookup to the URL declared in `NWM.ocsp_url` (§4.1). This lookup is optional but RECOMMENDED for `"verified"` assurance level endpoints.

**X.509 OID Registry (NIP v0.9 — PEN 65715 arc)**

| OID | Name | ASN.1 type | Description |
|-----|------|-----------|-------------|
| `1.3.6.1.4.1.65715.2.1` | `id-nid-assurance-level` | UTF8String | Agent assurance level (`anonymous` / `attested` / `verified`). CA MUST populate when issuing. |
| `1.3.6.1.4.1.65715.2.2` | `id-nps-node-roles` | SEQUENCE OF UTF8String | Role tags carried in `AnnounceFrame.node_roles` / `IdentFrame.node_roles`. CA SHOULD populate from the enrollment request. Phase 3 enforcement. |
| `1.3.6.1.4.1.65715.2.3` | `id-nps-capabilities` | SEQUENCE OF UTF8String | Capability strings (same vocabulary as `IdentFrame.capabilities`). CA MAY populate as a CA-attested alternative to the unsigned `capabilities` field. Phase 3. |

---

### 5.2 TrustFrame (0x21)

Cross-CA trust-chain propagation and capability grant. A TrustFrame lets one CA (the **grantor**) authorise another CA (the **grantee**) to issue IdentFrames whose `capabilities` are accepted by Nodes that already trust the grantor — without the grantee being added to each Node's `trusted_issuers` list. The grant is scoped to a capability subset and a set of `nwp://` URL patterns; it is verifiable end-to-end via the grantor's signature. Open SDKs carry the full wire frame and MAY provide basic validation for explicitly pinned grantor anchors; managed multi-CA federation, trust-anchor discovery, revocation feeds, and commercial trust-chain policy are NPS Cloud concerns.

**Field definitions**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `frame` | uint8 | required | Fixed `0x21`. |
| `grantor_nid` | string (NID) | required | NID of the granting CA. MUST be in the verifying Node's `trusted_issuers` for the TrustFrame to take effect. |
| `grantee_ca` | string (NID) | required | NID of the receiving CA. IdentFrames whose `issued_by` equals `grantee_ca` are admitted under the grant. |
| `trust_scope` | array of string | required | Capability strings the trust covers. MUST be a subset of the standard `capabilities` enum defined in §5.1 (`nwp:query` / `nwp:action` / `nwp:stream` / `ncp:stream` / `nop:delegate` / `nop:orchestrate` / `topology:read` / `llm:complete` / `llm:context` / `llm:stream` / `llm:tool_call` / `llm:embed` / `llm:rerank`). A grantee MUST NOT issue downstream IdentFrames carrying capabilities outside this set. |
| `nodes` | array of string | required | `nwp://` URL patterns this trust applies to. `*` matches a single path segment; `**` matches multiple segments. Empty array means the grant covers no nodes (effectively inert). |
| `issued_at` | string (ISO 8601 UTC) | required | Issuance time. |
| `expires_at` | string (ISO 8601 UTC) | required | Expiry time. After this instant the frame MUST be rejected with `NIP-TRUST-FRAME-EXPIRED`. |
| `serial` | string (hex) | required | 16-character zero-padded hex serial number used for revocation tracking via RevokeFrame (§5.3). |
| `signer_nid` | string (NID) | required | NID whose private key signs this frame. MUST be either `grantor_nid` itself or an authorised delegated `agent` NID under `grantor_nid`'s domain, recognised by local policy as an operator/delegated signer. |
| `signature` | string | required | `{alg}:{base64url}` — `ed25519:` (Ed25519) or `ecdsa-p256:` (ECDSA P-256). |

**Signature computation**

The signature is computed over the canonical JSON of the TrustFrame with the `signature` field removed (keys sorted alphabetically, no whitespace) — same canonicalisation rule as IdentFrame in §5.1. Supported algorithms: Ed25519 (`ed25519:`) and ECDSA P-256 (`ecdsa-p256:`).

**Errors**

| Code | When |
|------|------|
| `NIP-TRUST-FRAME-INVALID` (`NPS-CLIENT-BAD-FRAME`) | TrustFrame is malformed (missing required field, signature verification fails, or canonical form is invalid). |
| `NIP-TRUST-FRAME-EXPIRED` (`NPS-AUTH-UNAUTHENTICATED`) | Current time is past `expires_at`. |
| `NIP-TRUST-FRAME-GRANTOR-REVOKED` (`NPS-AUTH-UNAUTHENTICATED`) | The `grantor_nid`'s own CA certificate is revoked or expired at verification time. |
| `NIP-TRUST-FRAME-SCOPE-EXCEEDS-GRANTOR` (`NPS-AUTH-FORBIDDEN`) | `trust_scope` contains a capability that the grantor itself does not hold (no-scope-expansion principle, §10.3). |
| `NIP-TRUST-FRAME-NODES-PATTERN-INVALID` (`NPS-CLIENT-BAD-FRAME`) | An entry in `nodes` is not a syntactically valid `nwp://` URL pattern, or uses wildcards in a position where they are not permitted. |

**Full example**

```json
{
  "frame":       "0x21",
  "grantor_nid": "urn:nps:org:org-a.com",
  "grantee_ca":  "urn:nps:org:org-b.com",
  "trust_scope": ["nwp:query", "nwp:action"],
  "nodes":       ["nwp://api.org-a.com/public/**"],
  "issued_at":   "2026-05-11T00:00:00Z",
  "expires_at":  "2026-12-31T00:00:00Z",
  "serial":      "00000000000A3F9C",
  "signer_nid":  "urn:nps:org:org-a.com",
  "signature":   "ed25519:3045022100..."
}
```

---

### 5.3 RevokeFrame (0x22)

Revokes an NID, all certificates issued under an NID, or a specific certificate identified by serial. RevokeFrames are emitted by an issuing CA (or by an authorised delegated agent under that CA) and pushed to subscribed Nodes via the NIP push channel; receivers update their local cert / OCSP cache and reject subsequent requests authenticated under the revoked target. A RevokeFrame is **fire-and-forget** at the protocol layer (no response frame on success); receivers MUST emit an `ErrorFrame` only when the frame itself is malformed or unauthorised.

**Field definitions**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `frame` | uint8 | required | Fixed value `0x22`. |
| `target_nid` | string (NID) | required | NID being revoked. MAY be an agent NID, node NID, group NID, or org NID. |
| `serial` | string (hex) | optional | Specific certificate serial under `target_nid` to revoke. If present, MUST match the serial of a currently-issued cert for `target_nid`. If omitted, ALL currently-issued certs for `target_nid` are revoked (effectively revoking the NID itself). |
| `reason` | string (enum) | required | One of the values defined in the reason table below. |
| `revoked_at` | string (ISO 8601 UTC) | required | Wall-clock instant at which revocation takes effect. Receivers SHOULD reject any request whose IdentFrame `issued_at` is at or before `revoked_at`. |
| `parent_nid` | string (NID) | conditional | REQUIRED when `reason = "parent_revoked"`; identifies the group NID whose cascade triggered this revocation (NPS-CR-0003). MUST be omitted for any other reason. |
| `signer_nid` | string (NID) | required | NID of the CA or delegated agent signing this revocation. MUST be either the issuing CA of `target_nid`, an authorised `agent` NID under that CA's domain holding the `nip:revoke` capability, or — for cascade revocations — the CA that issued `parent_nid`. |
| `signature` | string | required | `{alg}:{base64url}` signature by `signer_nid`'s private key. Supported algorithms: `ed25519:` (Ed25519) or `ecdsa-p256:` (ECDSA P-256). |

**`reason` enum**

| Value | Description |
|-------|-------------|
| `key_compromise` | Agent / node private key is known or suspected to be compromised. Highest urgency. |
| `ca_compromise` | The issuing CA's signing key is compromised; ALL certificates under that CA are implicitly revoked, and a per-NID RevokeFrame MAY be emitted for each affected child. |
| `affiliation_changed` | Agent left the issuing organisation; ALL of its capabilities are revoked even if individual certs have not yet expired. |
| `superseded` | Replaced by a re-issued certificate carrying the same NID (e.g. routine rotation that needs to invalidate the prior serial). The `serial` field SHOULD be set to identify the superseded cert. |
| `cessation_of_operation` | Agent / node has been decommissioned and will never present credentials again. |
| `parent_revoked` | (NPS-CR-0003) Cascade emitted by the CA on a session-NID whose group NID was revoked. The `parent_nid` field MUST be set to the group NID. Distinguishes ancestry-driven invalidation from a session revoked on its own merits. |

**Forward compatibility**: receivers encountering a `reason` value not present in the table above MUST treat it as `key_compromise` (the most restrictive interpretation). Receivers MAY additionally log the unknown value for diagnostic purposes and MAY emit `NIP-REVOKE-FRAME-REASON-UNKNOWN` to the publishing CA over a side channel, but MUST NOT silently demote an unknown reason to a less restrictive one.

**Signature computation**

The signature is computed over the canonical JSON of the RevokeFrame with the `signature` field removed (keys sorted alphabetically, no whitespace) — same canonicalisation rule as IdentFrame in §5.1 (RFC 8785 JCS).

**Errors**

| Code | When |
|------|------|
| `NIP-REVOKE-FRAME-INVALID` (`NPS-CLIENT-BAD-FRAME`) | RevokeFrame is malformed (missing required field, signature verification fails, or canonical form is invalid). |
| `NIP-REVOKE-FRAME-UNAUTHORIZED-ISSUER` (`NPS-AUTH-FORBIDDEN`) | `signer_nid` is not authorised to revoke `target_nid` (not the issuing CA of `target_nid`, not an authorised delegated `agent` under that CA holding `nip:revoke`, and — for `parent_revoked` — not the CA that issued `parent_nid`). |
| `NIP-REVOKE-FRAME-SERIAL-MISMATCH` (`NPS-CLIENT-BAD-PARAM`) | `serial` is present but does not match any currently-issued cert for `target_nid`. |
| `NIP-REVOKE-FRAME-REASON-UNKNOWN` (`NPS-CLIENT-BAD-FRAME`) | `reason` carries a value outside the defined enum. Receivers treat the revocation as `key_compromise` for safety (see Forward compatibility above); this code is reported back to the publishing CA so the publish can be corrected. |

**Full example**

```json
{
  "frame":      "0x22",
  "target_nid": "urn:nps:agent:ca.innolotus.com:550e8400-e29b-41d4",
  "serial":     "0x0A3F9C",
  "reason":     "key_compromise",
  "revoked_at": "2026-04-10T12:00:00Z",
  "signer_nid": "urn:nps:org:ca.innolotus.com",
  "signature":  "ed25519:3045022100..."
}
```

**Cascade example (`parent_revoked`)**

```json
{
  "frame":      "0x22",
  "target_nid": "urn:nps:agent:ca.example.com:session-1714672800-f3a92c0b",
  "reason":     "parent_revoked",
  "revoked_at": "2026-04-10T12:00:00Z",
  "parent_nid": "urn:nps:agent:ca.example.com:group-7f3c9e1a-b2d8-4c6f-9a01",
  "signer_nid": "urn:nps:org:ca.example.com",
  "signature":  "ed25519:..."
}
```

`parent_revoked` (NPS-CR-0003) is set by the CA on a session-NID `RevokeFrame` it emits as part of cascade revocation when the session's group NID was revoked. It distinguishes a session that was forcibly invalidated by ancestry from one revoked on its own merits.

---

## 6. Certificate Lifecycle

```
Registration     Auto-renewal           Revocation        CA rotation
     │                 │                     │                │
     ↓                 ↓                     ↓                ↓
CA issues         7 days pre-expiry     RevokeFrame      Old + new CA
IdentFrame        Agent triggers         takes effect     in parallel
30-day validity   CA issues new cert     immediately       for 30 days
                  Both valid 1 hour      Nodes reject      Smooth rollover
                                         all requests
                                         for that NID
```

### 6.1 Short-lived / renewable cert profile (edge mTLS)

Edge deployments that terminate native-mode mutual TLS at an `nps-ingress` (L2) gateway
(NPS-RFC-0006 §6.3) benefit from **short-lived certificates** to minimise revocation exposure
without per-request OCSP round-trips. This profile is additive to the standard validity tiers
(§2.1) and is selected by the CA at issuance.

| Property | Standard Node cert | **Short-lived edge cert** |
|----------|--------------------|---------------------------|
| Validity | 90 days | **1–24 hours** (default 6 h) |
| Renewal trigger | 7 days pre-expiry | **renewal window = 25% of validity remaining** |
| Renewal overlap | both valid 1 h | both valid until the old cert's `expires_at` |
| Revocation reliance | OCSP / CRL | **lifetime ≤ OCSP cache TTL** — short validity is the primary revocation mechanism; OCSP is defense-in-depth |
| OCSP-staple interaction | optional | the gateway SHOULD staple (`IdentFrame.ocsp_staple`, NIP v0.9 §8.2); a stapled response older than the cert's remaining validity MUST be refreshed (`NIP-OCSP-STAPLE-EXPIRED`) |

Rules:

1. A short-lived edge cert MUST carry validity in `[1h, 24h]`. A request to issue outside this
   range returns `NIP-CA-SESSION-VALIDITY-INVALID` (reused from the session-NID path, §5).
2. The CA SHOULD support **online renewal** (ACME `agent-01`, NPS-RFC-0002) so an edge node
   self-renews before the renewal window without operator action.
3. A relying party (e.g. `nps-ingress`) MUST reject a presented short-lived cert whose
   `expires_at ≤ now` with `NIP-CERT-EXPIRED`; because validity is short, relying parties MAY
   skip per-request OCSP when the cert lifetime is ≤ their configured OCSP cache TTL.
4. Short-lived edge certs participate in TLS 1.3 session resumption (NPS-RFC-0006 §6.4): a
   resumption ticket MUST NOT outlive the certificate it was issued under.

### 6.2 Alpha.19 renewal interoperability profile (NIP v0.15)

Shared cases live in
[`renewal_revocation_vectors.json`](conformance/nip/renewal_revocation_vectors.json).

1. **NIP-P19-01 — renewal window.** Standard certificates open renewal seven
   days before `not_after`; short-lived edge certificates open at 25% of their
   original validity remaining. The boundary is inclusive. Earlier requests
   return `NIP-CA-RENEWAL-TOO-EARLY` without changing state.
2. **NIP-P19-02 — identity continuity.** Renewal authenticates the current,
   unexpired certificate and preserves NID, subject, lineage, assurance level,
   capabilities, and scope unless an Operator-authorized separate mutation has
   already narrowed them. Renewal MUST NOT expand authority.
3. **NIP-P19-03 — atomic issue.** A successful renewal commits a new unique
   serial and audit record before returning it. The old certificate remains
   independently valid until its own expiry or revocation.
4. **NIP-P19-04 — idempotency.** A renewal request carries an idempotency key.
   Repeating the same key and canonical request returns the same issued serial;
   reusing it with different content is `NIP-CA-SERIAL-DUPLICATE` and never
   issues another certificate.
5. **NIP-P19-05 — resumption bound.** TLS session tickets and cached admission
   bound to the old certificate expire no later than that certificate and are
   never extended merely because a replacement was issued.

---

## 7. Verification Flow

```
Node receives IdentFrame
  │
  ├─ 1. Check expires_at > now  (expired → NIP-CERT-EXPIRED)
  ├─ 2. Check issued_by is in NWM trusted_issuers  (not trusted → NIP-CERT-UNTRUSTED-ISSUER)
  ├─ 3. Verify signature with issued_by CA public key  (fail → NIP-CERT-SIGNATURE-INVALID)
  ├─ 3a. (NPS-CR-0003) If lineage.parent_nid is present, OCSP-lookup the parent
  │      (revoked or expired → NIP-CERT-PARENT-REVOKED)
  ├─ 4. OCSP lookup (when NWM configures ocsp_url) or local CRL check  (revoked → NIP-CERT-REVOKED)
  ├─ 5. Check capabilities contains what the node requires  (missing → NIP-CERT-CAPABILITY-MISSING)
  └─ 6. Check scope.nodes covers the target node path  (not covered → NWP-AUTH-NID-SCOPE-VIOLATION)
        All checks pass → authorize the request
```

Step **3a** (chain check) is mandatory whenever `lineage.parent_nid` is present, regardless of whether the session NID itself is still inside its own validity window. Combined with the CA-side cascade documented in §5.3 (`parent_revoked` reason), it provides defense-in-depth: a misconfigured CA that fails to record the cascade still gets caught at the verifier; a CA that did record it still publishes the entries to the CRL for simple consumers.

**Note on TrustFrame chains (§5.2)**: when the IdentFrame's `issued_by` is *not* in the Node's `trusted_issuers` directly, step 3 MAY still succeed via a TrustFrame chain. After step 3 (CA signature check), if the IdentFrame was issued by a `grantee_ca`, the Node SHOULD verify that a valid, unexpired TrustFrame exists from a matching `grantor_nid` (which IS in `trusted_issuers`) covering the requested capability and target node path. The TrustFrame itself MUST pass its own checks (§5.2 error table) — failure short-circuits admission with the corresponding `NIP-TRUST-FRAME-*` code instead of falling through to `NIP-CERT-UNTRUSTED-ISSUER`.

**Inbound RevokeFrame handling (§5.3)**: RevokeFrames arrive asynchronously over the NIP push channel and are not part of the per-IdentFrame admission flow above. When a Node receives a RevokeFrame it MUST:

```
Node receives RevokeFrame (push channel)
  │
  ├─ R1. Parse the frame and verify signer_nid signature over canonical JSON
  │      (fail → emit ErrorFrame NIP-REVOKE-FRAME-INVALID; do not apply)
  ├─ R2. Verify signer_nid is authorised to revoke target_nid — see §5.3 signer_nid rules
  │      (fail → emit ErrorFrame NIP-REVOKE-FRAME-UNAUTHORIZED-ISSUER; do not apply)
  ├─ R3. If `serial` is present, verify it matches a known issued cert for target_nid
  │      (mismatch → emit ErrorFrame NIP-REVOKE-FRAME-SERIAL-MISMATCH; do not apply)
  ├─ R4. If `reason` is outside the defined enum, treat as `key_compromise` (most restrictive)
  │      and emit ErrorFrame NIP-REVOKE-FRAME-REASON-UNKNOWN to the publisher
  ├─ R5. Poison the local cert / OCSP cache for target_nid
  │      (if `serial` present, scope to that cert; otherwise scope to all certs for target_nid)
  └─ R6. No success response is sent — RevokeFrame is fire-and-forget at the protocol layer.
```

After R5 takes effect, any subsequent IdentFrame for `target_nid` (or for the specific `serial` if scoped) will fail step 4 of the IdentFrame verification flow above with `NIP-CERT-REVOKED`.

### 7.5 Phase-3 enforcement mode (NIP v0.11)

The CA-attestation checks that become mandatory at the Phase-3 flag day
(`v1.0.0-beta.1`, NPS-RFC-0003 §8.1) are gated on a single receiver-side
verification-policy flag, **`phase3_enforcement`**, so a deployment MAY opt into the
stricter behaviour **ahead of** the flag day — making the flag day a configuration
default change rather than a code change.

- **Phase 1–2 (current)** — `phase3_enforcement` defaults to `false`. Receivers SHOULD
  perform the checks below and MAY reject on failure; while `false`, a check failure is
  advisory (log / metric) and MUST NOT by itself reject the IdentFrame.
- **`phase3_enforcement = true`** (opt-in now; the default at the flag day) — a receiver
  MUST enforce all of the following against an X.509 NID certificate (NPS-RFC-0002).
  Each check applies **only when the corresponding certificate extension is present**, so
  a self-declared (Phase-1–2) NID that carries no attestation extensions is unaffected:

  | Check | Requirement | Error code |
  |-------|-------------|------------|
  | Assurance | `IdentFrame.assurance_level` equals the `id-nid-assurance-level` extension | `NIP-ASSURANCE-MISMATCH` |
  | Node roles | every entry in `IdentFrame.node_roles` appears in `id-nps-node-roles` (`65715.2.2`) — no un-attested role | `NIP-CERT-NODE-ROLES-MISMATCH` |
  | Capabilities | every entry in `IdentFrame.capabilities` appears in `id-nps-capabilities` (`65715.2.3`) — no un-attested capability | `NIP-CERT-CAPABILITIES-EXCEEDED` |
  | OCSP staple | `IdentFrame.ocsp_staple` is present and unexpired (`nextUpdate` in the future) | `NIP-OCSP-STAPLE-EXPIRED` |

- **Flag day (`v1.0.0-beta.1`)** — `phase3_enforcement` defaults to `true`;
  `public-federated` deployments (NDP §7) MUST NOT set it to `false`. `local-dev` /
  `org-private` MAY keep it `false`.

The role and capability checks are **subset** checks (the frame MUST NOT claim more than
the CA attested); under-claiming is allowed. This closes the self-declaration gap that
Phase-1–2 leaves open (§5.1, §12.4 of NPS-2) without a wire-format change.

### 7.6 Portable CA and Verification Profile (NIP v0.13)

This profile makes the §7 verifier and the open CA lifecycle deterministic
across SDKs. The language-neutral
[`revocation_policy_vectors.json`](conformance/nip/revocation_policy_vectors.json)
and [`signed_crl_vectors.json`](conformance/nip/signed_crl_vectors.json) cases
are normative.

**Verification order**

A full verifier MUST evaluate applicable checks in this order and stop at the
first failure:

1. expiry;
2. directly trusted issuer or a valid pinned-grantor TrustFrame path;
3. frame signature, optional X.509 chain, and Phase-3 checks;
4. parent/group status when lineage is present;
5. live revocation policy;
6. required capabilities;
7. target-node scope.

The public result keeps the existing §7 step numbers: live revocation remains
step 4, capabilities step 5, and scope step 6.

**Revocation modes**

Receivers MUST expose a `revocation_mode` policy with two values:

- `if_configured` is the alpha compatibility default. With no configured source,
  verification continues after recording a diagnostic.
- `required` rejects when no source is configured with
  `NIP-OCSP-UNAVAILABLE`. Public-federated deployments MUST use this mode.

Configured sources are consulted in the following fixed order:

1. a verified local CRL/cache;
2. a live revocation callback;
3. an `INipCaStore`-equivalent lookup by serial;
4. the CA's advertised OCSP endpoint.

A revoked result stops immediately with `NIP-CERT-REVOKED`. An exception,
transport failure, non-success HTTP response, or malformed response from a
configured source fails closed with `NIP-OCSP-UNAVAILABLE`. The existing
`ocsp_fail_open` compatibility escape applies only to OCSP availability
failures; it MUST default to `false` and MUST NOT be enabled in
public-federated deployments. A configured, current local CRL that does not
contain the serial satisfies `required` mode without an online lookup.

**Portable open CA lifecycle**

- Register MUST reject a duplicate NID and duplicate serial.
- Renew MUST append a new serial while retaining prior records for audit.
- Revoke MUST update the current live record and make it observable to live
  verification and the next CRL response.
- CA stores MUST support lookup by NID and serial, revoked enumeration, parent
  enumeration, and full enumeration. Administrative full enumeration is exposed
  as `GET /v1/certificates` and requires Operator authorization.
- `/.well-known/nps-ca.endpoints.ocsp` MUST identify a served endpoint. A CA
  MUST omit an endpoint it does not serve.

`GET /v1/crl` returns a signed JSON artifact:

```json
{
  "issued_by": "urn:nps:org:ca.example.com",
  "issued_at": "2026-07-29T01:00:00Z",
  "entries": [
    {
      "nid": "urn:nps:agent:ca.example.com:agent-001",
      "serial": "0x0001",
      "revoked_at": "2026-07-29T00:00:00Z",
      "reason": "key_compromise"
    }
  ],
  "signature": "ed25519:..."
}
```

The signature covers the RFC 8785 canonical JSON object formed by removing
`signature` and recursively sorting `issued_by`, `issued_at`, and `entries`
content. Clients MUST verify it with the CA public key from
`/.well-known/nps-ca` before treating the entries as a local revocation source.
Entries MUST be emitted in ascending `(revoked_at, serial, nid)` order so the
same CA state and `issued_at` produce a deterministic artifact body.

This profile adds one administrative HTTP endpoint and no frame field or error
code.

### 7.7 Alpha.19 revocation freshness and advisory profile (NIP v0.15)

6. **NIP-P19-06 — freshness.** A CRL, cache entry, staple, or OCSP result is
   current only while `this_update <= now < next_update`. At the boundary it is
   stale and cannot satisfy `revocation_mode = required`.
7. **NIP-P19-07 — stale fallback.** A stale source is skipped in the fixed §7.6
   source order when a later configured source can answer. If no later source
   produces a current definitive result, verification fails with
   `NIP-REVOCATION-STATE-STALE`.
8. **NIP-P19-08 — unknown and timeout.** A definitive OCSP `unknown` result
   fails with `NIP-OCSP-UNKNOWN`. Timeout, transport failure, malformed data,
   and non-success HTTP remain `NIP-OCSP-UNAVAILABLE`. `ocsp_fail_open` never
   converts `revoked`, `unknown`, stale, or invalidly signed data into success.
9. **NIP-P19-09 — cache replacement.** A current result may replace an older
   result for the same issuer/serial only after signature and interval
   validation. A response with an earlier `this_update` cannot roll back cache
   state.
10. **NIP-P19-10 — Phase-3 advisory.** Advisory evaluation returns a stable,
    sorted list of the exact Phase-3 checks that would reject an IdentFrame,
    including error code and field. It MUST NOT change `phase3_enforcement`,
    mutate trust/revocation state, or reject the current Phase 1–2 request.

---

### 7.8 Authenticated invocation projection (NIP v0.16)

The following requirements define experimental `nps.invocation_context` revision 1.
They do not change IdentFrame signatures or certificate formats.

#### 7.8.1 In-memory context

Admission produces two separate typed objects:

- caller facts: request_id, traceparent, requested deadline, idempotency_key, bounded hints;
- admitted authority: subject NID, verified assurance, capabilities, deployment scope, issuer and
  transport/session provenance.

A caller deadline may shorten but never extend the server limit. An idempotency key is not identity.
Native authority comes from verified NIP session admission, never Hello.agent_id. Local-development
authority requires an explicit server deployment mode unavailable to remote caller selection.
Adapters cannot construct admitted authority by deserializing a request body.

#### 7.8.2 Wire envelope and canonicalization

Reserve feature ID `nps.invocation_context`, revision 1, experimental until vectors and release
gates promote it. Both hops require feature support. HTTP header `X-NPS-Invocation-Context` carries
unpadded base64url of UTF-8 JSON for exactly `{"payload":{...},"signature":"..."}`.
Reject repeated headers, non-canonical base64url, duplicate keys, unknown fields, invalid Unicode,
non-I-JSON numbers and decoded envelopes over 16 KiB (or a smaller local limit).

The payload has exactly these fields:

| Field | Type / limit |
|---|---|
| profile | literal nps.invocation_context |
| revision | integer 1 |
| issuer | admitted gateway NID, 1..512 UTF-8 bytes |
| key_id | configured identifier matching `[A-Za-z0-9._:-]{1,128}` |
| audience | exact logical receiving service ID, 1..512 UTF-8 bytes |
| subject | original admitted subject NID, 1..512 UTF-8 bytes |
| assurance | anonymous / attested / verified; receiver must permit this assertion |
| capabilities | sorted unique strings, at most 64, each 1..128 UTF-8 bytes |
| scope | sorted unique strings of deployment-registered scope identifiers, at most 64, each 1..256 UTF-8 bytes |
| issued_at | integer Unix seconds |
| expires_at | integer Unix seconds |
| projection_id | 32 lowercase hex characters from 16 cryptographically random bytes |
| request_digest | lowercase sha256: followed by 64 lowercase hex digits |

Scope entries are opaque identifiers in a receiver-configured vocabulary, not policy expressions.
No wildcards gain authority unless the receiver's product policy independently interprets them.
All strings are compared exactly; no case folding or Unicode normalization is added implicitly.

Signature input is UTF-8 bytes of `NPS-CONTEXT-PROJECTION/1`, one zero byte, then RFC 8785/JCS
canonical payload bytes. Signature is a 64-byte Ed25519 signature encoded unpadded base64url.
The unsigned outer envelope has no algorithm switch. Keys come only from the receiver's configured
(issuer,key_id) mapping; never accept embedded keys. Existing IdentFrame canonicalization is unchanged.

#### 7.8.3 Request digest

The digest is SHA-256 over UTF-8 `NPS-INVOKE-REQUEST/1`, one zero byte and JCS of this exact object:

```json
{"protocol":"nwp","operation":"example.echo","target":"service:echo","idempotency_key":null,"required_features":[{"id":"nps.invocation_context","revision":1}],"body":{"encoding":"json","value":{"params":{"text":"hello"}}}}
```

protocol and operation are registry identifiers, target is the exact logical service/resource,
idempotency_key is a string or null, required_features is sorted by ID with no duplicate IDs,
and body has either `{"encoding":"json","value":<I-JSON value>}` or
`{"encoding":"bytes","content_type":"<exact canonical media type>","sha256":"sha256:<hex>"}`.
A byte profile must register its canonical content type; unregistered/ambiguous forms reject.
Native/HTTP serialization mechanics are excluded; the resolved operation/target and parsed body
must be identical at issuer and receiver. Include accepted semantic query parameters in value.
Trace IDs and the projection envelope are excluded. Registered operation schemas must include
every input that affects authority or side effects; hidden header-driven semantics are prohibited.

A gateway that changes operation, target, body or required features must authenticate and create
a fresh projection for the next hop; it cannot forward a signature for different semantics.

#### 7.8.4 Time, peer binding and replay

Require 0 < expires_at-issued_at <= 60 seconds and Unix integers in 0..9007199254740991.
Clock skew allowance is at most 5 seconds. Reject issued_at > now+5 or now >= expires_at+5.
Receivers may enforce shorter windows. On separately authenticated TLS/mTLS or equivalent
deployment transport, bind the peer to its configured allowed issuer set. A valid signature alone
is insufficient; public inputs are stripped before fresh projection and cannot select that set.

Verify size/schema, channel issuer, configured key, signature, audience/subject bindings, time,
request digest and local admission policy before deriving authority. Admitted capabilities/scope
are constrained by receiver policy; a gateway cannot exceed its configured delegation ceiling.
The principal from the original public connection is asserted by the trusted issuer, not assumed
to equal the immediate gateway identity.

Replay admission is an atomic insert-if-absent for (issuer,projection_id), retained at least until
expires_at+5. The store must survive process restarts and be shared across replicas receiving the
same audience. Missing/unavailable replay protection fails closed. An invalid request must not
consume a replay ID; simultaneous valid duplicates admit at most one. A lost reply requires a fresh
projection for retry while retaining logical operation identity. Replay protection does not replace
durable operation deduplication, and inserting an ID does not prove an action executed.

Rotate via configured key overlap, bounded by maximum lifetime plus skew. Remove revoked issuer keys
immediately; do not accept a key merely because its projection was issued before revocation.
Log bounded identifiers/error categories, never the projection header, signature or sensitive scope.

#### 7.8.5 Deterministic bounds and carrier binding

JSON revision and timestamp fields MUST use decimal-digit integer syntax, without
fractions or exponents. They remain subject to the value ranges above.

Capabilities and scope MUST be sorted by unsigned UTF-8 byte order, with exact
duplicate rejection; this array ordering is separate from JCS object-key ordering.
Issuer and subject MUST be valid NIDs under existing NIP syntax. Protocol is an ASCII
registry identifier of 1..32 bytes; operation is 1..256 UTF-8 bytes; target is
1..512 UTF-8 bytes; a non-null idempotency key is 1..128 UTF-8 bytes and must also
satisfy its operation schema. Required feature pairs use NCP §2.6.3 types, are
unique and sorted by ID, at most 32. The complete canonical request-digest object
MUST NOT exceed 1048576 bytes; existing smaller request/frame limits still apply.

NWP §9.7 defines HTTP and native carriers and the exact logical request construction.
Both use the same envelope and signature algorithm. A projection is not a bearer
credential, and MUST NOT be accepted without the independent authenticated-channel
issuer binding. Public/untrusted inbound projection carriers MUST be removed before
a gateway creates a fresh projection from locally admitted authority.

The verifier MUST return NIP-CONTEXT-INVALID for schema/encoding/size/invalid-lifetime
or future-issued errors; NIP-CONTEXT-UNTRUSTED for signature/key/issuer failures;
NIP-CONTEXT-BINDING-MISMATCH for audience/subject/request bindings or delegation-policy
mismatch; NIP-CONTEXT-EXPIRED for expiry; NIP-CONTEXT-REPLAY for a duplicate valid
projection; and NIP-CONTEXT-VERIFIER-UNAVAILABLE for unavailable verification/replay
infrastructure. None permits application dispatch. Failure details MUST be bounded
and MUST NOT expose the envelope or sensitive scope.

---

## 8. NIP CA Server OSS API

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/v1/agents/register` | Operator Cert | Register an agent; returns NID + IdentFrame |
| POST | `/v1/agents/{nid}/renew` | Agent Cert | Renew a certificate (up to 7 days before expiry) |
| POST | `/v1/agents/{nid}/revoke` | Operator Cert | Revoke an NID |
| GET | `/v1/agents/{nid}/verify` | None | Verify NID validity (OCSP lookup) |
| POST | `/v1/nodes/register` | Operator Cert | Register an NWP node; issues a Node Certificate |
| POST | `/v1/orchestrators/groups/register` | Operator Cert | (NPS-CR-0003) Register an orchestrator group; returns IdentFrame with `lineage.role = "group"` |
| POST | `/v1/orchestrators/groups/{group_nid}/sessions/issue` | Group JWS *or* Operator Cert | (NPS-CR-0003) Issue a short-lived session NID under the group |
| POST | `/v1/orchestrators/groups/{group_nid}/revoke` | Operator Cert | (NPS-CR-0003) Revoke the group AND cascade-revoke every live session under it |
| GET | `/v1/orchestrators/groups/{group_nid}/sessions` | Operator Cert | (NPS-CR-0003) List sessions issued under this group (audit) |
| GET | `/v1/ca/cert` | None | CA public-key certificate |
| GET | `/v1/certificates` | Operator Cert | Enumerate issued certificate records for audit |
| GET | `/v1/crl` | None | Certificate revocation list |
| GET | `/.well-known/nps-ca` | None | CA discovery endpoint |

**`/.well-known/nps-ca` response**

```json
{
  "nps_ca": "0.1",
  "issuer": "urn:nps:org:ca.mycompany.com",
  "display_name": "MyCompany NPS CA",
  "public_key": "ed25519:MCowBQYDK2VwAyEA...",
  "algorithms": ["ed25519", "ecdsa-p256"],
  "endpoints": {
    "register": "https://ca.mycompany.com/v1/agents/register",
    "verify":   "https://ca.mycompany.com/v1/agents/{nid}/verify",
    "node_verify": "https://ca.mycompany.com/v1/nodes/{nid}/verify",
    "crl":      "https://ca.mycompany.com/v1/crl"
  },
  "capabilities": ["agent", "node", "operator", "orchestrator-group"],
  "max_cert_validity_days": 30
}
```

### 8.1 Registration Authority (RA) endpoints (NPS-CR-0005, Implemented)

NPS-CR-0005 introduces a three-tier Registration Authority (RA) model that admits agents into the CA along policy-driven paths *in addition to* the Operator-credential `POST /v1/agents/register` flow above. The default tier remains `operator_only` — the new tiers are opt-in via `NipCaOptions.EnrollmentTier` and add the following endpoints:

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/v1/enrollment/tokens` | Operator Cert | (NPS-CR-0005, Tier 2) Mint a single-use, NID-scoped bootstrap token. |
| GET  | `/v1/enrollment/pending` | Operator Cert | (NPS-CR-0005, Tier 3) List registration requests awaiting admin decision. |
| POST | `/v1/enrollment/pending/{id}/approve` | Operator Cert | (NPS-CR-0005, Tier 3) Approve a pending request and issue the IdentFrame. |
| POST | `/v1/enrollment/pending/{id}/reject` | Operator Cert | (NPS-CR-0005, Tier 3) Reject a pending request with an operator-supplied reason. |

The three tiers (allowlist / bootstrap token / pending queue), the error codes (`NIP-RA-TOKEN-INVALID`, `NIP-RA-TOKEN-EXPIRED`, `NIP-RA-NID-NOT-ALLOWED`, `NIP-RA-PENDING-REJECTED`), request/response shapes, verification flow, and `NipCaOptions.EnrollmentTier` selector are normatively specified by **[NPS-CR-0005](./cr/NPS-CR-0005-nip-ca-ra-model.md)** and implemented by all six SDK CA surfaces. This section is an API index, not a future-work placeholder.

---

## 9. Error Codes

| Error Code | NPS Status | Description |
|------------|------------|-------------|
| `NIP-CERT-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | Certificate has expired |
| `NIP-CERT-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | Certificate has been revoked |
| `NIP-CERT-SIGNATURE-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | Certificate signature verification failed |
| `NIP-CERT-UNTRUSTED-ISSUER` | `NPS-AUTH-UNAUTHENTICATED` | Issuer not in the trust list |
| `NIP-CERT-CAPABILITY-MISSING` | `NPS-AUTH-FORBIDDEN` | Certificate missing a required capability |
| `NIP-CERT-SCOPE-VIOLATION` | `NPS-AUTH-FORBIDDEN` | Certificate scope does not cover the target path |
| `NIP-CERT-CAPABILITIES-EXCEEDED` | `NPS-AUTH-FORBIDDEN` | `IdentFrame.capabilities` claims a capability not present in the CA-attested `id-nps-capabilities` extension; Phase-3 enforcement (NIP v0.12 §7.5) |
| `NIP-CA-NID-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | NID does not exist |
| `NIP-CA-NID-ALREADY-EXISTS` | `NPS-CLIENT-CONFLICT` | NID already exists (duplicate registration) |
| `NIP-CA-SERIAL-DUPLICATE` | `NPS-CLIENT-CONFLICT` | Certificate serial already in use |
| `NIP-CA-RENEWAL-TOO-EARLY` | `NPS-CLIENT-BAD-PARAM` | Renewal window not yet open |
| `NIP-CA-SCOPE-EXPANSION-DENIED` | `NPS-AUTH-FORBIDDEN` | Requested scope exceeds the parent scope |
| `NIP-OCSP-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | OCSP service temporarily unavailable |
| `NIP-OCSP-STAPLE-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | `IdentFrame.ocsp_staple` `nextUpdate` has elapsed — staple is stale; Agent must refresh and resend (NIP v0.9 §5.1.4) |
| `NIP-OCSP-UNKNOWN` | `NPS-AUTH-UNAUTHENTICATED` | A valid OCSP response cannot establish status for the requested issuer/serial and therefore fails closed |
| `NIP-REVOCATION-STATE-STALE` | `NPS-SERVER-UNAVAILABLE` | No configured revocation source produced a current result; stale state cannot satisfy required mode |
| `NIP-CERT-NODE-ROLES-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | `IdentFrame.node_roles` does not match the `id-nps-node-roles` X.509 extension; Phase 3 enforcement (NIP v0.10) |
| `NIP-TRUST-FRAME-INVALID` | `NPS-CLIENT-BAD-FRAME` | TrustFrame signature or format is invalid — see §5.2 |
| `NIP-TRUST-FRAME-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | TrustFrame `expires_at` is in the past — see §5.2 |
| `NIP-TRUST-FRAME-GRANTOR-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | TrustFrame `grantor_nid`'s own CA certificate is revoked or expired — see §5.2 |
| `NIP-TRUST-FRAME-SCOPE-EXCEEDS-GRANTOR` | `NPS-AUTH-FORBIDDEN` | TrustFrame `trust_scope` contains a capability the grantor itself does not hold (no-scope-expansion principle, §10.3) — see §5.2 |
| `NIP-TRUST-FRAME-NODES-PATTERN-INVALID` | `NPS-CLIENT-BAD-FRAME` | TrustFrame `nodes` entry is not a valid `nwp://` URL pattern — see §5.2 |
| `NIP-REVOKE-FRAME-INVALID` | `NPS-CLIENT-BAD-FRAME` | RevokeFrame is malformed (missing required field, signature verification fails, or canonical form is invalid) — see §5.3 |
| `NIP-REVOKE-FRAME-UNAUTHORIZED-ISSUER` | `NPS-AUTH-FORBIDDEN` | RevokeFrame `signer_nid` is not authorised to revoke `target_nid` — see §5.3 |
| `NIP-REVOKE-FRAME-SERIAL-MISMATCH` | `NPS-CLIENT-BAD-PARAM` | RevokeFrame `serial` is present but does not match any currently-issued cert for `target_nid` — see §5.3 |
| `NIP-REVOKE-FRAME-REASON-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | RevokeFrame `reason` carries a value outside the defined enum; receivers treat it as `key_compromise` — see §5.3 |
| `NIP-ASSURANCE-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | `IdentFrame.assurance_level` does not match the cert extension `id-nid-assurance-level` (downgrade-attack defense) — see §5.1.1 |
| `NIP-ASSURANCE-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | `assurance_level` carries a value outside the defined enum (`anonymous` / `attested` / `verified`) — see §5.1.1 |
| `NIP-REPUTATION-ENTRY-INVALID` | `NPS-CLIENT-BAD-FRAME` | Reputation log entry signature fails verification or canonical form is malformed — see §5.1.2 (NPS-RFC-0004) |
| `NIP-REPUTATION-LOG-UNREACHABLE` | `NPS-DOWNSTREAM-UNAVAILABLE` | A log operator referenced by a Node's `reputation_policy` cannot be reached during admission evaluation — see §5.1.2 (NPS-RFC-0004) |
| `NIP-CA-GROUP-REVOKED` | `NPS-AUTH-FORBIDDEN` | Cannot issue a session under a group NID that has been revoked — see §5.1.3 (NPS-CR-0003) |
| `NIP-CA-PARENT-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | The `parent_nid` / group NID referenced by a session-issue request does not exist — see §5.1.3 (NPS-CR-0003) |
| `NIP-CA-PARENT-NOT-GROUP` | `NPS-CLIENT-BAD-PARAM` | The referenced parent NID exists but is not registered as `lineage.role = "group"` — see §5.1.3 (NPS-CR-0003) |
| `NIP-CA-SESSION-VALIDITY-INVALID` | `NPS-CLIENT-BAD-PARAM` | Requested session validity below 60 seconds or above the CA's configured maximum — see §5.1.3 (NPS-CR-0003) |
| `NIP-CA-JWS-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | Group-JWS authorisation on a session-issue request fails signature, header, or shape validation — see §5.1.3 (NPS-CR-0003) |
| `NIP-CA-JWS-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | Group-JWS `iat` outside the CA's clock-skew window (default ±5 minutes) — see §5.1.3 (NPS-CR-0003) |
| `NIP-CERT-PARENT-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | A session NID's parent / group NID is revoked or expired (chain check, §7 step 3a) — see §5.1.3 (NPS-CR-0003) |

HTTP-mode status mapping: see [status-codes.md](./status-codes.md).

---

## 10. Security Considerations

### 10.1 Key storage
CA private keys MUST be stored in an HSM or in an encrypted key file (AES-256-GCM). The OSS reference implementation uses encrypted files and reserves an HSM interface.

### 10.2 Timing-attack mitigation
OCSP response times SHOULD be normalized (fixed 200 ms delay) to prevent certificate-status inference via response-time side channels.

### 10.3 No-scope-expansion principle
At every link in the delegation chain, scope MUST NOT exceed that of its parent. CAs MUST enforce this at issuance time.

---

## 11. Change Log

| Version | Date | Changes |
|---------|------|---------|
| 0.16 | 2026-09-09 | Alpha.20 experimental authenticated invocation projection: bounded JCS/Ed25519 envelope, complete request binding, trusted channel issuer, delegation ceilings and durable atomic replay admission. |
| 0.15 | 2026-08-31 | Alpha.19 renewal/revocation hardening: inclusive standard/short-lived renewal windows, identity continuity, atomic/idempotent issuance, old-cert ticket bounds, explicit revocation freshness and stale fallback, fail-closed OCSP unknown, rollback-safe cache replacement, deterministic non-enforcing Phase-3 advisory output, and shared vectors. Corrects §8.1 CR-0005 RA status from stub to Implemented. Depends-On advances to NCP 0.12. |
| 0.14 | 2026-08-12 | Added standard `llm:context` capability for the NWP §7.6 stateful LLM context lifecycle and admitted it into TrustFrame `trust_scope`. Completion mutations also require `llm:complete`; owner-authorized status/release remain available with `llm:context` alone. Context identifiers are locators, not bearer authorization. No new frame field or NIP error code. |
| 0.13 | 2026-07-29 | Added §7.6 Portable CA and Verification Profile: deterministic verification/source order, `if_configured` and fail-closed `required` revocation modes, signed deterministic CRL semantics, full CA-store enumeration, and authenticated `GET /v1/certificates`. Added shared revocation-policy and signed-CRL vectors. No new frame field or error code. |
| 0.12 | 2026-07-23 | New §7.5 **Phase-3 enforcement mode**: a receiver-side `phase3_enforcement` verification-policy flag that turns the Phase-1–2 opt-in CA-attestation checks (assurance / node_roles / capabilities / OCSP-staple) into hard MUSTs ahead of the `v1.0.0-beta.1` flag day — so the flag day is a default change, not a code change. Role/capability checks are subset checks (no un-attested claims); each applies only when the corresponding cert extension is present (self-declared NIDs unaffected). One new error code `NIP-CERT-CAPABILITIES-EXCEEDED` (`NPS-AUTH-FORBIDDEN`). Additive / backward-compatible. (Renumbered from the edge-line 0.11 — the released alpha.16 line had independently used 0.11 for the LLM capability strings below.) |
| 0.11 | 2026-07-04 | Adds standard LLM capability strings (`llm:complete`, `llm:stream`, `llm:tool_call`, `llm:embed`, `llm:rerank`) for NWP LLM/Thinking Profile discovery and authorization. TrustFrame `trust_scope` may cover these capabilities. No new frame fields or error codes. |
| 0.10 | 2026-06-12 | New §6.1 **Short-lived / renewable cert profile (edge mTLS)**: 1–24 h validity tier (default 6 h) for native-mode mTLS terminated at `nps-ingress` (NPS-RFC-0006 §6.3); renewal window = 25% remaining; ACME `agent-01` online self-renewal; lifetime ≤ OCSP cache TTL so short validity is the primary revocation mechanism; OCSP-staple refresh interaction (`NIP-OCSP-STAPLE-EXPIRED`); resumption-ticket bounded to cert validity (NPS-RFC-0006 §6.4). Reuses existing error codes (`NIP-CA-SESSION-VALIDITY-INVALID`, `NIP-CERT-EXPIRED`); no new codes. |
| 0.10 | 2026-06-03 | `IdentFrame.node_roles` (`string[]`, optional) — self-declared role tags, same vocabulary as `NDP.AnnounceFrame.node_roles`; Phase 3 gate against `id-nps-node-roles` X.509 extension (OID 65715.2.2); `NIP-CERT-NODE-ROLES-MISMATCH` error code. |
| 0.9 | 2026-05-31 | `IdentFrame.ocsp_staple` (base64url DER OCSPResponse): §5.1.4 stapling rules (signature verify, `nextUpdate` check, `NIP-OCSP-STAPLE-EXPIRED`); X.509 OID table: `id-nid-assurance-level` 65715.2.1, `id-nps-node-roles` 65715.2.2, `id-nps-capabilities` 65715.2.3 (PEN 65715 — NPS-CR-0004). |
| 0.8 | 2026-05-11 | Expanded §5.2 TrustFrame: added full field definition table (10 fields, including new required `issued_at`, `serial`, and `signer_nid` for revocation/audit traceability), explicit signature-canonicalisation rule (matching §5.1 IdentFrame), error sub-table, and full example. Added TrustFrame verification note to §7 (admit IdentFrames whose `issued_by` is a `grantee_ca` when a valid TrustFrame from a trusted `grantor_nid` covers the request). Four new error codes (§9): `NIP-TRUST-FRAME-EXPIRED`, `NIP-TRUST-FRAME-GRANTOR-REVOKED`, `NIP-TRUST-FRAME-SCOPE-EXCEEDS-GRANTOR`, `NIP-TRUST-FRAME-NODES-PATTERN-INVALID`. |
| 0.7 | 2026-05-07 | **NPS-CR-0003**: Orchestrator group NIDs and short-lived session NIDs. New §3.1 reserves `group-` / `session-` identifier prefixes on `entity-type = agent`. New signed `IdentFrame.lineage` object with `role` / `parent_nid` / `group_nid` / `session_id` / `purpose` / `owner_user_id` / `owner_key_id` (§5.1.3). New revocation reason `parent_revoked` (§5.3). New verification step **3a** for chain-check (§7). Four new CA endpoints under `/v1/orchestrators/groups/...` (§8). Seven new error codes (§9): `NIP-CA-GROUP-REVOKED`, `NIP-CA-PARENT-NOT-FOUND`, `NIP-CA-PARENT-NOT-GROUP`, `NIP-CA-SESSION-VALIDITY-INVALID`, `NIP-CA-JWS-INVALID`, `NIP-CA-JWS-EXPIRED`, `NIP-CERT-PARENT-REVOKED`. Backward-compatible for ordinary single-NID flows; opt-in for orchestrators. |
| 0.6 | 2026-05-01 | Added `topology:read` to the standard `capabilities` registry (§5.1 capabilities table). This capability is required by Anchor Nodes at Phase 1–2 to gate access to topology read operations (`topology.snapshot` / `topology.stream`) as defined by the NPS-2 §12.4 minimum authorization binding (M6). Self-declared and key-signed at Phase 1–2; CA-attested role binding (`id-nps-node-roles` cert extension) deferred to Phase 3 pending RFC-0002 stabilization. |
| 0.5 | 2026-04-26 | Added §5.1.2 Reputation Log Entry — wire format for Certificate-Transparency-style append-only behavioral records about a `subject_nid`. Phase 1 ships the entry shape (12 fields including dual-signature requirements over JCS-canonicalised form), the initial 8-value `incident` enum (`cert-revoked` / `rate-limit-violation` / `tos-violation` / `scraping-pattern` / `payment-default` / `contract-dispute` / `impersonation-claim` / `positive-attestation`), and the 5-step `severity` enum. New error codes `NIP-REPUTATION-ENTRY-INVALID` and `NIP-REPUTATION-LOG-UNREACHABLE`. Merkle / STH / inclusion-proof surface deferred to Phase 2 per [NPS-RFC-0004](rfcs/NPS-RFC-0004-nid-reputation-log.md) §8.1. |
| 0.4 | 2026-04-25 | Added §5.1.1 Assurance Levels (`anonymous` / `attested` / `verified`); IdentFrame gains optional `assurance_level` field with backward-compatible default `anonymous`; new error codes `NIP-ASSURANCE-MISMATCH` and `NIP-ASSURANCE-UNKNOWN`. See [NPS-RFC-0003](rfcs/NPS-RFC-0003-agent-identity-assurance-levels.md). `Depends-On` NCP version corrected to `v0.6` per NPS-RFC-0001. |
| 0.2 | 2026-04-12 | Unified port 17433; added `metadata` field to IdentFrame (tokenizer auto-match); error codes switched to NPS-status-code mapping; error-code list completed |
| 0.1 | 2026-04-10 | Initial spec: NID format, IdentFrame/TrustFrame/RevokeFrame, CA Server API, verification flow |

---

*Copyright: LabAcacia / INNO LOTUS PTY LTD · Apache 2.0*
