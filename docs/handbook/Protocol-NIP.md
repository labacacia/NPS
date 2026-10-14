# Protocol: NIP — Neural Identity Protocol

**Status:** ✅ Released alpha.19 reference

**Released spec**: `spec/NPS-3-NIP.md` v0.15 · **Port**: 17433 (shared) / 17435 (optional dedicated)

> **Alpha.19 release: NIP 0.15 Proposed.** It closes
> certificate renewal and signed CRL/OCSP freshness/fail-closed behavior across
> six SDKs. Phase-3 enforcement remains advisory; the beta.1 flag day is not
> activated. See [Alpha.19 Current Status](./Alpha19-Current-Status.md).

NIP is the identity and trust backbone of NPS. It issues verifiable Neural Identities (NIDs) to every AI Agent, NWP Node, and human Operator, carries capability declarations and scope-bound permissions, supports trust-chain propagation across organizations, and provides real-time revocation. The analogy is TLS/PKI: where TLS secures the transport channel, NIP secures the *identity of the communicating parties* regardless of transport.

Related: [SDK Identity and Authentication](./SDK-Identity-and-Authentication.md) | [Operator Reputation Log](./Operator-Reputation-Log.md) | [Protocol NWP](./Protocol-NWP.md)

---

## NID Format

A Neural Identity (NID) is a URN with the following structure:

```
urn:nps:{entity-type}:{issuer-domain}:{identifier}

Examples:
  urn:nps:agent:ca.innolotus.com:550e8400-e29b-41d4    <- AI agent
  urn:nps:node:api.myapp.com:products                   <- NWP node
  urn:nps:org:mycompany.com                             <- Organization CA
```

`entity-type` is one of `agent`, `node`, or `org`. `issuer-domain` is an RFC 1034 domain. `identifier` may contain alphanumerics, hyphens, underscores, and dots.

### NID Lifecycle

1. **Issue:** The Org CA issues an `IdentFrame` containing the NID, public key, capabilities, scope, and CA signature. Agent certificates have 30-day validity (auto-renewal supported); Node certificates have 90-day validity; Operator certificates have 1-year validity.
2. **Use:** The Agent presents its `IdentFrame` on every new connection. The receiving Node verifies the signature, checks expiry, validates capabilities, and confirms scope coverage.
3. **Revoke:** The CA issues a `RevokeFrame` (0x22). Revocation takes effect immediately. Nodes verify revocation via OCSP (when configured) or local CRL.

### CA Hierarchy

```
Root CA (offline, rarely used)
    |
    +-- Org Intermediate CA (one per organization, 1-year validity)
            +-- Agent Certificate     (30-day validity, auto-renewal)
            +-- Node Certificate      (90-day validity)
            +-- Operator Certificate  (1-year validity, bound to MFA)
```

Primary signature algorithm: **Ed25519** (32-byte keys, high-frequency verification performance). Fallback: ECDSA P-256 for compatibility scenarios. Public keys are encoded as `{algorithm}:{base64url(DER)}`, e.g. `ed25519:MCowBQYDK2VwAyEA...`.

---

## Frame Types

### IdentFrame (0x20)

The Agent identity declaration frame. Sent as the handshake frame on every new native-mode connection, and included in HTTP-mode requests via headers.

**Key fields:**

| Field | Type | Description |
|-------|------|-------------|
| `nid` | string | Agent NID |
| `pub_key` | string | Public key in `{alg}:{base64url}` format |
| `capabilities` | array | Capabilities held by this agent (see Capability Registry below) |
| `scope` | object | Access scope: `{nodes: [...], actions: [...], max_token_budget: N}` |
| `issued_by` | string | Issuer NID (Org CA) |
| `issued_at` / `expires_at` | string | ISO 8601 UTC timestamps |
| `serial` | string | Certificate serial (globally unique per Org CA, hex) |
| `signature` | string | CA's Ed25519 signature over the canonical frame (with `signature` field excluded), computed per RFC 8785 JCS |
| `cert_format` | string (enum) | Certificate encoding format: `"x509-der"` / `"raw-pubkey"`. Excluded from the signed canonical JSON. Added in NIP v0.7. |
| `cert_chain` | array | Required when `cert_format = "x509-der"`: DER-encoded certificate chain, base64url-encoded, leaf first. Omitted (not present in the canonical form) when `cert_format = "raw-pubkey"`. Excluded from the signed canonical JSON. Added in NIP v0.7. |
| `assurance_level` | string | `"anonymous"` / `"attested"` / `"verified"` — see Three-Tier Assurance Levels below |
| `lineage` | object | Optional signed lineage metadata; present when the NID is an orchestrator group (`role = "group"`) or short-lived session (`role = "session"`). Part of the signed canonical JSON. See Group / Session NIDs below. Added in NIP v0.7 (NPS-CR-0003). |
| `ocsp_staple` | string | Optional base64url-encoded DER OCSP response stapled by the Agent at send time. Receivers SHOULD verify the staple signature; an expired staple returns `NIP-OCSP-STAPLE-EXPIRED`. Excluded from the signed canonical JSON. Added in NIP v0.9. |
| `node_roles` | array[string] | Optional self-declared node-role tags, e.g. `["memory", "orchestrator"]` (same vocabulary as NDP `AnnounceFrame.node_roles`). Phase 1–2: informational; Phase 3 flag day MUST match the `id-nps-node-roles` X.509 extension (OID `1.3.6.1.4.1.65715.2.2`), mismatch returns `NIP-CERT-NODE-ROLES-MISMATCH`. Excluded from the Ed25519-signed payload. Added in NIP v0.10. |
| `metadata` | object | Optional: `{model_family, tokenizer, runtime}` — not included in signature computation; used for CGN auto-matching |

The signature is computed over the canonical JSON of the `IdentFrame` with the `signature` field removed and object keys sorted alphabetically with no extra whitespace (RFC 8785 JCS). The `cert_format`, `cert_chain`, `ocsp_staple`, `node_roles`, and `metadata` fields are excluded from the signed canonical form; `lineage` is included.

> **`node_kind` → `node_roles`:** `node_kind` was an accepted alias for the role declaration **through alpha.5 only**. From alpha.6 onward clients MUST send `node_roles` (and `topology.filter.node_roles`).

### TrustFrame (0x21) — spec §5.2

Cross-CA trust-chain propagation and capability grant (commercial feature, NPS Cloud multi-org deployments). A TrustFrame lets one CA (the **grantor**) authorise another CA (the **grantee**) to issue IdentFrames whose `capabilities` are accepted by Nodes that already trust the grantor — without the grantee being added to each Node's `trusted_issuers` list. The grant is scoped to a capability subset and a set of `nwp://` URL patterns, and is verifiable end-to-end via the grantor's signature. The full field table landed in NIP v0.8.

**Fields:** `grantor_nid` (NID of the granting CA; MUST be in the verifier's `trusted_issuers`), `grantee_ca` (NID of the receiving CA), `trust_scope` (capability subset of the §5.1 enum), `nodes` (`nwp://` URL patterns; `*` matches one path segment, `**` matches multiple), `issued_at`, `expires_at`, `serial` (16-char zero-padded hex, for RevokeFrame tracking), `signer_nid` (MUST be `grantor_nid` or an operator under it), and `signature` (`ed25519:` or `ecdsa-p256:`).

The signature is computed over the canonical JSON with `signature` removed (keys sorted, no whitespace) — same rule as IdentFrame. The Ed25519-signed payload covers **all** TrustFrame fields except `signature` — including `issued_at`, `serial`, and `signer_nid` (added to the signed body for revocation/audit traceability in NIP v0.8). TrustFrame-specific errors: `NIP-TRUST-FRAME-INVALID`, `NIP-TRUST-FRAME-EXPIRED`, `NIP-TRUST-FRAME-GRANTOR-REVOKED`, `NIP-TRUST-FRAME-SCOPE-EXCEEDS-GRANTOR`, `NIP-TRUST-FRAME-NODES-PATTERN-INVALID` (see Error Codes).

> **⚠ Breaking (alpha.15 — signed-payload realignment):** the TrustFrame/RevokeFrame Ed25519-signed payloads were realigned to the current NPS-3 v0.10 field set (TrustFrame signs `issued_at` / `serial` / `signer_nid`; RevokeFrame signs `target_nid` / `serial` / `reason` / `revoked_at` / `parent_nid` / `signer_nid`), and revocation status is surfaced as `NIP-CERT-REVOKED`. Signed frames produced by the **old alpha.14-era SDK shape no longer verify** after upgrading — re-issue affected TrustFrames/RevokeFrames from an alpha.15 CA. The frame **version number is unchanged (NIP v0.10)**; only the canonical signed body was corrected for cross-SDK consistency.

### RevokeFrame (0x22) — spec §5.3

Revokes an NID, all certificates under an NID, or a specific certificate identified by serial. Emitted by an issuing CA (or an authorised operator under it) and pushed to subscribed Nodes via the NIP push channel; receivers poison their local cert / OCSP cache and reject subsequent requests under the revoked target. A RevokeFrame is **fire-and-forget** at the protocol layer (no success response); receivers emit an `ErrorFrame` only when the frame is malformed or unauthorised. Takes effect immediately upon receipt.

**Fields:** `target_nid` (agent / node / group / org NID), `serial` (optional — scopes the revocation to one cert; if omitted ALL certs for `target_nid` are revoked), `reason`, `revoked_at`, `parent_nid` (**required when `reason = "parent_revoked"`**, NPS-CR-0003; the group NID whose cascade triggered this), `signer_nid`, and `signature` (`ed25519:` or `ecdsa-p256:`). The Ed25519-signed payload covers all of these fields with `signature` removed (RFC 8785 JCS; same rule as IdentFrame/TrustFrame).

**`parent_nid` ↔ `parent_revoked` guard (enforced by all SDKs, alpha.15):** `parent_nid` is **required when `reason = "parent_revoked"`** and **MUST be omitted for any other reason**. A frame that sets `parent_nid` without `reason = "parent_revoked"`, or that uses `reason = "parent_revoked"` without `parent_nid`, is rejected with `NIP-REVOKE-FRAME-INVALID`.

Valid `reason` values: `key_compromise`, `ca_compromise`, `affiliation_changed`, `superseded`, `cessation_of_operation`, and `parent_revoked` (NPS-CR-0003 — cascade emitted by the CA on a session NID whose group NID was revoked; `parent_nid` MUST be set). Receivers encountering an unknown `reason` MUST treat it as `key_compromise` (most restrictive) and MAY report `NIP-REVOKE-FRAME-REASON-UNKNOWN` to the publishing CA; they MUST NOT silently demote it. RevokeFrame-specific errors: `NIP-REVOKE-FRAME-INVALID`, `NIP-REVOKE-FRAME-UNAUTHORIZED-ISSUER`, `NIP-REVOKE-FRAME-SERIAL-MISMATCH`, `NIP-REVOKE-FRAME-REASON-UNKNOWN` (see Error Codes).

---

## Capability Registry

Capabilities are declared in `IdentFrame.capabilities` as a string array. Nodes check this array before granting access to specific operations.

| Capability | Description |
|------------|-------------|
| `nwp:query` | May query Memory Nodes |
| `nwp:action` | May invoke Action Nodes |
| `nwp:stream` | May receive `StreamFrame` responses |
| `ncp:stream` | May initiate NCP streaming |
| `nop:delegate` | May delegate subtasks to other Agents |
| `nop:orchestrate` | May act as an Orchestrator and emit `TaskFrame`s |
| `topology:read` | May read Anchor Node topology data via `topology.snapshot` / `topology.stream` (NPS-2 §12). Added in NIP v0.6 (alpha.5). Anchor Nodes MUST require this capability at Phase 1–2 per NPS-2 §12.4. Self-declared and key-signed; CA-attested role binding deferred to Phase 3. |

**Standard `llm:*` capability strings (NIP v0.11, alpha.16):** `llm:complete`,
`llm:stream`, `llm:tool_call`, `llm:embed`, `llm:rerank` — registered for NWP
LLM/Thinking Profile discovery and authorization; a TrustFrame `trust_scope`
may cover these capabilities like any other.

Capability declarations are signed by the issuing CA inside `IdentFrame.signature`. An Agent cannot add capabilities to its own certificate — only the CA can grant them at issuance time.

**Stateful LLM context (NIP v0.14, alpha.18 release):** `llm:context` authorizes owner-bound NWP context lifecycle operations and is admitted in `TrustFrame.trust_scope`. Create/append/fork/reset also require `llm:complete`; owner-authorized status/release require `llm:context`. Possession of a context ID alone grants no authority.

---

## Three-Tier Assurance Levels (NPS-RFC-0003)

NPS-RFC-0003 (Accepted, Phase 1 landed) defines three assurance levels for Agent identities, modeled on NIST SP 800-63 IAL and CA/B Forum DV/OV/EV certificates. The level travels in `IdentFrame.assurance_level` and is the source of truth for Node policy decisions via `NWM.min_assurance_level`.

| Level | Enum value | Minimum CA criteria | Typical use |
|-------|------------|---------------------|-------------|
| L0 | `"anonymous"` | Self-signed NID, or CA-signed without out-of-band identity binding | Hobbyist Agents, dev/test, free read-only endpoints |
| L1 | `"attested"` | NID signed by an RFC-0002-compliant CA; CA attests possession of the NID private key (ACME `agent-01` challenge); contact email or domain verified | Most production Agents; default rate-limit tier |
| L2 | `"verified"` | L1 criteria plus CA binds the operator's legal identity (corporate registration or signed AaaS-operator attestation) | Regulated integrations, paid premium tiers, contract-grade orchestration |

The levels form an **ordered enum**: `anonymous < attested < verified`. A request whose level is below the Node's required level MUST be rejected with `NWP-AUTH-ASSURANCE-TOO-LOW` (`NPS-AUTH-FORBIDDEN`).

Default is `"anonymous"` — pre-RFC-0003 NIDs and any NID lacking the `assurance_level` field are treated as L0.

### AssuranceLevel.fromWire("") — Empty String Fix (alpha.5)

Prior to alpha.5, some SDKs serialized a missing or null `assurance_level` as an empty string `""` on the wire instead of omitting the field. This caused parsing errors in receiving SDKs that did not handle the empty-string case.

**Spec fix (cross-SDK parity):** an `assurance_level` field containing an empty string `""` MUST be treated as equivalent to the field being absent, meaning the level resolves to `"anonymous"`. This fix was applied across all six SDKs (.NET, Python, TypeScript, Java, Rust, Go) in alpha.5.

### Forward Compatibility: NIP-ASSURANCE-UNKNOWN

An implementation receiving an `assurance_level` value not in the defined enum (`anonymous`, `attested`, `verified`) MUST return `NIP-ASSURANCE-UNKNOWN` (`NPS-CLIENT-BAD-FRAME`). Implementations MUST NOT silently demote an unknown value to `anonymous` — doing so would create a security loophole when future higher-assurance levels are introduced by a later spec revision.

---

## Orchestrator Group / Session NIDs (NPS-CR-0003)

NPS-CR-0003 (NIP v0.7) adds an orchestrator group / session lineage model on top of ordinary agent NIDs.

**Reserved identifier prefixes** (on `entity-type = agent`):

| Prefix | Role | Notes |
|--------|------|-------|
| `group-` | Orchestrator group NID — trust anchor for a fleet of session NIDs. | Longer-lived (default 365 days), revocable. e.g. `urn:nps:agent:ca.example.com:group-7f3c9e1a-...` |
| `session-` | Short-lived session NID issued under a group. | Portion after `session-` MUST be `{unix-timestamp}-{random}` (random ≥ 8 hex). Default validity 1 hour, max 24 hours. |

Prefixes are informational; receivers MUST NOT reject a NID solely on its prefix. The authoritative role is carried in the **signed** `IdentFrame.lineage.role` field.

**`IdentFrame.lineage`** is part of the signed canonical JSON (unlike `metadata`). Fields: `role` (`"group"` / `"session"`), `parent_nid` and `group_nid` (required when `role = "session"`), `session_id`, and optional `purpose` / `owner_user_id` / `owner_key_id`. The trust chain is: human owner → Operator key → orchestrator group NID → short-lived session NID.

**Cascade revocation:** revoking a group NID cascade-revokes every live session under it; the per-session RevokeFrame carries `reason = "parent_revoked"` with `parent_nid` set to the group NID. The verification flow gains **step 3a** (chain check): when `lineage.parent_nid` is present, the verifier OCSP-looks-up the parent and rejects with `NIP-CERT-PARENT-REVOKED` if the parent is revoked or expired.

**CA endpoints** (see NIP CA Server OSS API): `POST /v1/orchestrators/groups/register`, `POST /v1/orchestrators/groups/{group_nid}/sessions/issue` (Group JWS or Operator Cert), `POST /v1/orchestrators/groups/{group_nid}/revoke`, `GET /v1/orchestrators/groups/{group_nid}/sessions`.

New error codes: `NIP-CA-GROUP-REVOKED`, `NIP-CA-PARENT-NOT-FOUND`, `NIP-CA-PARENT-NOT-GROUP`, `NIP-CA-SESSION-VALIDITY-INVALID`, `NIP-CA-JWS-INVALID`, `NIP-CA-JWS-EXPIRED`, `NIP-CERT-PARENT-REVOKED` (see Error Codes).

---

## OCSP Stapling (NIP v0.9)

An Agent MAY attach a pre-fetched OCSP response in `IdentFrame.ocsp_staple` (base64url DER `OCSPResponse`), letting the receiving Node verify revocation status without a live OCSP round-trip. The receiver decodes the staple, verifies its signature against the issuing CA cert (from `cert_chain` or local trust store), checks `thisUpdate` / `nextUpdate` (past `nextUpdate` → `NIP-OCSP-STAPLE-EXPIRED`), and checks `certStatus` for the `serial` (revoked → `NIP-CERT-REVOKED`). A staple that passes all checks supersedes cached revocation state. When `ocsp_staple` is absent, the Node MAY perform an online OCSP lookup to `NWM.ocsp_url` (RECOMMENDED for `"verified"` endpoints). A Phase 3 flag day at `v1.0.0-beta.1` makes staple handling mandatory.

---

## X.509 + ACME Path (NPS-RFC-0002)

NPS-RFC-0002 proposes replacing the NIP custom certificate format with standard X.509v3 certificates and moving issuance to ACME (RFC 8555) with an `agent-01` challenge type. The primary motivation is tooling reuse: OpenSSL, step-ca, HashiCorp Vault PKI, HSM vendors, and cert-manager for Kubernetes can then sign, validate, and store NIP certificates without custom integration.

**IANA PEN assigned (NPS-CR-0004, NIP v0.7).** IANA Private Enterprise Number **65715** was assigned to LabAcacia on **2026-05-08**. All NPS X.509 OIDs now anchor to the arc `1.3.6.1.4.1.65715`, replacing the provisional, unregistered arc `1.3.6.1.4.1.99999`. Certificates issued under the old provisional arc MUST be revoked and re-issued under the PEN arc.

**X.509 extension OID registry (PEN 65715 arc):**

| OID | Name | ASN.1 type | Description |
|-----|------|-----------|-------------|
| `1.3.6.1.4.1.65715.2.1` | `id-nid-assurance-level` | UTF8String | Agent assurance level (`anonymous` / `attested` / `verified`). CA MUST populate when issuing. |
| `1.3.6.1.4.1.65715.2.2` | `id-nps-node-roles` | SEQUENCE OF UTF8String | Role tags carried in `IdentFrame.node_roles` / `AnnounceFrame.node_roles`. CA SHOULD populate from the enrollment request. Phase 3 enforcement (NIP v0.9). |
| `1.3.6.1.4.1.65715.2.3` | `id-nps-capabilities` | SEQUENCE OF UTF8String | Capability strings (same vocabulary as `IdentFrame.capabilities`). CA MAY populate as a CA-attested alternative to the unsigned `capabilities` field. Phase 3 (NIP v0.9). |

The provisional arc `1.3.6.1.4.1.99999.1` (used by the pre-PEN prototype):
- MUST NOT be used for conformance testing at any compliance level
- MUST NOT be used for production NID issuance
- MUST NOT be used in cross-organization interoperability tests

---

## Reputation Log Entry Shape (NPS-RFC-0004)

NPS-RFC-0004 (Accepted, Phase 1 landed) defines a Certificate-Transparency-style append-only log for NID behavioral incidents. Where `assurance_level` answers "who is this Agent?", reputation log entries answer "how has this NID behaved?" — published so any party (Node, auditor, downstream operator) can assess a NID's track record without a pre-existing relationship with the publisher.

A reputation entry is a signed JSON object with 12 fields:

| Field | Description |
|-------|-------------|
| `v` | Schema version (`1`) |
| `log_id` | NID of the log operator that appended this entry |
| `seq` | Monotonically increasing per-`log_id` sequence number (set by log operator on append) |
| `timestamp` | Log operator's commit time (RFC 3339 UTC) |
| `subject_nid` | The NID this entry is about |
| `incident` | Incident type (see vocabulary below) |
| `severity` | `info` / `minor` / `moderate` / `major` / `critical` |
| `window` | Optional observation window `{start, end}` |
| `observation` | Optional machine-readable per-incident detail (e.g. `{requests: 45000, threshold: 300}`) |
| `evidence_ref` | Optional URL where richer evidence lives |
| `evidence_sha256` | Optional SHA-256 of the evidence blob for tamper detection |
| `issuer_nid` | NID of the party making the assertion |
| `signature` | Ed25519 signature by `issuer_nid`'s private key over the entry minus `signature`, canonicalized per RFC 8785 JCS |

The dual-signature model: the issuer signs the entry; the log operator then re-signs the full entry (including the issuer signature) with the `log_id` key as an ordering commitment.

### Incident Vocabulary

| Value | Meaning |
|-------|---------|
| `cert-revoked` | CA revoked the subject NID's certificate |
| `rate-limit-violation` | Sustained violation of published rate limits |
| `tos-violation` | Violated an AaaS Anchor Node's published terms |
| `scraping-pattern` | Behavior matched scraper heuristics |
| `payment-default` | CGN or fiat default on a committed transaction |
| `contract-dispute` | Unresolved contractual breach on an async NOP task |
| `impersonation-claim` | Third-party claim that subject NID is impersonating them |
| `positive-attestation` | Explicit positive signal (e.g. independent audit passed) |

Receivers MUST treat unknown `incident` values as opaque pass-through (forward compatibility).

---

## Phase 3 STH Gossip (alpha.5)

Phase 3 of NPS-RFC-0004 introduced Signed Tree Head (STH) gossip to enable fork detection across independent log operators. The `nps-ledger` daemon exposes:

```
GET /v1/log/gossip/sth
```

This endpoint returns the current STH that the ledger has committed (tree size, root hash, timestamp, and signature). Other log operators fetch STHs from peers and compare root hashes. If two operators disagree on the tree root at the same tree size, a fork is detected.

**Error code for fork detection:** `NIP-REPUTATION-GOSSIP-FORK` — emitted when the ledger detects a conflicting STH from a gossip peer, indicating either log corruption or a malicious log operator attempt to present different views to different observers.

The STH gossip protocol is implemented in the `nps-ledger` daemon (`GossipState` + `GossipService`) and shipped in alpha.5 with 13 test cases covering STH fetch, gossip exchange, and fork detection.

---

## NIP Verification Flow

When a Node receives an `IdentFrame`:

1. Check `expires_at > now` — expired: `NIP-CERT-EXPIRED`
2. Check `issued_by` is in the NWM `trusted_issuers` — not trusted: `NIP-CERT-UNTRUSTED-ISSUER`
3. Verify `signature` with the issuer CA's public key — invalid: `NIP-CERT-SIGNATURE-INVALID`
3a. **(NPS-CR-0003)** If `lineage.parent_nid` is present, OCSP-lookup the parent — revoked or expired: `NIP-CERT-PARENT-REVOKED`
4. OCSP lookup (when NWM configures `ocsp_url`) or local CRL check — revoked: `NIP-CERT-REVOKED`
5. Check `capabilities` contains what the node requires — missing: `NIP-CERT-CAPABILITY-MISSING`
6. Check `scope.nodes` covers the target node path — not covered: `NWP-AUTH-NID-SCOPE-VIOLATION`

All checks pass → authorize the request.

Step 3a (chain check) is mandatory whenever `lineage.parent_nid` is present, regardless of the session NID's own validity window. When the IdentFrame's `issued_by` is a `grantee_ca` (not directly in `trusted_issuers`), step 3 MAY still succeed via a valid, unexpired TrustFrame (§5.2) from a trusted `grantor_nid` covering the requested capability and node path; a failed TrustFrame check short-circuits with the corresponding `NIP-TRUST-FRAME-*` code.

---

## NIP CA Server OSS API

The reference NIP CA Server (`tools/nip-ca-server/`) exposes:

| Method | Path | Description |
|--------|------|-------------|
| POST | `/v1/agents/register` | Register an agent; returns NID + IdentFrame |
| POST | `/v1/agents/{nid}/renew` | Renew certificate (up to 7 days before expiry) |
| POST | `/v1/agents/{nid}/revoke` | Revoke an NID |
| GET | `/v1/agents/{nid}/verify` | Verify NID validity (OCSP lookup) |
| POST | `/v1/nodes/register` | Register an NWP node |
| POST | `/v1/orchestrators/groups/register` | (NPS-CR-0003) Register an orchestrator group; returns IdentFrame with `lineage.role = "group"` |
| POST | `/v1/orchestrators/groups/{group_nid}/sessions/issue` | (NPS-CR-0003) Issue a short-lived session NID under the group (Group JWS or Operator Cert) |
| POST | `/v1/orchestrators/groups/{group_nid}/revoke` | (NPS-CR-0003) Revoke the group AND cascade-revoke every live session under it |
| GET | `/v1/orchestrators/groups/{group_nid}/sessions` | (NPS-CR-0003) List sessions issued under this group (audit) |
| GET | `/v1/ca/cert` | CA public-key certificate |
| GET | `/v1/crl` | Certificate revocation list |
| GET | `/.well-known/nps-ca` | CA discovery endpoint |

A three-tier Registration Authority (RA) model (NPS-CR-0005, stub) adds opt-in enrollment endpoints under `/v1/enrollment/...` (bootstrap tokens, pending-registration queue) selected via `NipCaOptions.EnrollmentTier`; the default tier remains `operator_only`.

### Typed remote CA client + revocation artifacts (alpha.14 / alpha.15)

- **`NipCaClient`** — a typed remote NIP CA client (added in alpha.14) handling CA discovery, CRL retrieval, Ed25519 register / renew / revoke / verify, and RFC-0002 X.509 registration against the OSS CA above.
- **CRL revocation artifacts** — `GET /v1/crl` now includes an `issued_at` timestamp plus a **detached CA signature** over the list, so consumers can verify CRL authenticity and freshness independently.
- **`/.well-known/nps-ca` cleanup** — the discovery document no longer advertises an unmapped `/ocsp` endpoint (online OCSP is reached via `NWM.ocsp_url`, see OCSP Stapling above).
- **Store API** — `INipCaStore.ListAsync()` plus an `InMemoryNipCaStore` reference implementation back the CRL and audit surfaces.

---

## Error Codes

| Error Code | NPS Status | Description |
|------------|------------|-------------|
| `NIP-CERT-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | Certificate has expired |
| `NIP-CERT-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | Certificate has been revoked |
| `NIP-CERT-SIGNATURE-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | Certificate signature verification failed |
| `NIP-CERT-UNTRUSTED-ISSUER` | `NPS-AUTH-UNAUTHENTICATED` | Issuer not in the trust list |
| `NIP-CERT-CAPABILITY-MISSING` | `NPS-AUTH-FORBIDDEN` | Certificate missing a required capability |
| `NIP-CERT-SCOPE-VIOLATION` | `NPS-AUTH-FORBIDDEN` | Certificate scope does not cover the target path |
| `NIP-ASSURANCE-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | `IdentFrame.assurance_level` disagrees with the cert extension `id-nid-assurance-level` (downgrade-attack defense; enforced in Phase 3) |
| `NIP-ASSURANCE-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | `assurance_level` carries a value outside the defined enum |
| `NIP-REPUTATION-ENTRY-INVALID` | `NPS-CLIENT-BAD-FRAME` | Reputation log entry signature fails verification or canonical form is malformed |
| `NIP-REPUTATION-LOG-UNREACHABLE` | `NPS-DOWNSTREAM-UNAVAILABLE` | A log operator referenced by a Node's `reputation_policy` cannot be reached during admission evaluation |
| `NIP-CA-NID-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | NID does not exist |
| `NIP-CA-NID-ALREADY-EXISTS` | `NPS-CLIENT-CONFLICT` | NID already registered (duplicate registration) |
| `NIP-CA-RENEWAL-TOO-EARLY` | `NPS-CLIENT-BAD-PARAM` | Renewal window not yet open |
| `NIP-CA-SCOPE-EXPANSION-DENIED` | `NPS-AUTH-FORBIDDEN` | Requested scope exceeds the parent scope (no-scope-expansion principle) |
| `NIP-OCSP-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | OCSP service temporarily unavailable |
| `NIP-OCSP-STAPLE-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | `IdentFrame.ocsp_staple` `nextUpdate` has elapsed — staple is stale; Agent must refresh and resend (NIP v0.9) |
| `NIP-CERT-NODE-ROLES-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | `IdentFrame.node_roles` does not match the `id-nps-node-roles` X.509 extension; Phase 3 enforcement (NIP v0.10) |
| `NIP-TRUST-FRAME-INVALID` | `NPS-CLIENT-BAD-FRAME` | TrustFrame signature or format is invalid (§5.2) |
| `NIP-TRUST-FRAME-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | TrustFrame `expires_at` is in the past (§5.2) |
| `NIP-TRUST-FRAME-GRANTOR-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | TrustFrame `grantor_nid`'s own CA certificate is revoked or expired (§5.2) |
| `NIP-TRUST-FRAME-SCOPE-EXCEEDS-GRANTOR` | `NPS-AUTH-FORBIDDEN` | TrustFrame `trust_scope` contains a capability the grantor itself does not hold (no-scope-expansion) (§5.2) |
| `NIP-TRUST-FRAME-NODES-PATTERN-INVALID` | `NPS-CLIENT-BAD-FRAME` | TrustFrame `nodes` entry is not a valid `nwp://` URL pattern (§5.2) |
| `NIP-REVOKE-FRAME-INVALID` | `NPS-CLIENT-BAD-FRAME` | RevokeFrame is malformed (missing field, bad signature, or invalid canonical form) (§5.3) |
| `NIP-REVOKE-FRAME-UNAUTHORIZED-ISSUER` | `NPS-AUTH-FORBIDDEN` | RevokeFrame `signer_nid` is not authorised to revoke `target_nid` (§5.3) |
| `NIP-REVOKE-FRAME-SERIAL-MISMATCH` | `NPS-CLIENT-BAD-PARAM` | RevokeFrame `serial` present but matches no currently-issued cert for `target_nid` (§5.3) |
| `NIP-REVOKE-FRAME-REASON-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | RevokeFrame `reason` outside the defined enum; receivers treat it as `key_compromise` (§5.3) |
| `NIP-CA-GROUP-REVOKED` | `NPS-AUTH-FORBIDDEN` | Cannot issue a session under a group NID that has been revoked (NPS-CR-0003) |
| `NIP-CA-PARENT-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | The `parent_nid` / group NID referenced by a session-issue request does not exist (NPS-CR-0003) |
| `NIP-CA-PARENT-NOT-GROUP` | `NPS-CLIENT-BAD-PARAM` | The referenced parent NID exists but is not registered as `lineage.role = "group"` (NPS-CR-0003) |
| `NIP-CA-SESSION-VALIDITY-INVALID` | `NPS-CLIENT-BAD-PARAM` | Requested session validity below 60 s or above the CA's configured maximum (NPS-CR-0003) |
| `NIP-CA-JWS-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | Group-JWS authorisation on a session-issue request fails signature, header, or shape validation (NPS-CR-0003) |
| `NIP-CA-JWS-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | Group-JWS `iat` outside the CA's clock-skew window (default ±5 min) (NPS-CR-0003) |
| `NIP-CERT-PARENT-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | A session NID's parent / group NID is revoked or expired (chain check, step 3a) (NPS-CR-0003) |

---

> Last reviewed at suite version: v1.0.0-alpha.19
>
> Alpha.19 source status reconciled on 2026-09-05.
