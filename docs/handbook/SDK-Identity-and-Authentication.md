# SDK How-To: Identity and Authentication

**Status:** ✅ Reviewed for v1.0.0-alpha.19 release

> **Audience:** Developers integrating NPS identity into an Agent or Node implementation.
> **Source-of-truth precedence:** `spec/` documents win over this page if they disagree.

Every participant in the NPS network — Agent, Node, or Operator — holds a **Neural Identity (NID)**: a URN-format identifier backed by an Ed25519 (or ECDSA P-256 fallback) keypair and a CA-issued certificate. This page walks through obtaining a NID, constructing an IdentFrame, and verifying one on the receiving side. It also covers assurance levels (NPS-RFC-0003) and the behavior changes shipped in alpha.5 to fix the empty-string edge case across all six SDKs.

---

## Table of contents

1. [NID format](#nid-format)
2. [Getting a NID](#getting-a-nid)
3. [Generating a keypair](#generating-a-keypair)
4. [Presenting an IdentFrame](#presenting-an-identframe)
5. [Assurance levels](#assurance-levels)
6. [The empty-string bug fix (alpha.5)](#the-empty-string-bug-fix-alpha5)
7. [Receiver-side verification](#receiver-side-verification)
8. [TrustFrame / RevokeFrame signed-payload realignment (alpha.15)](#trustframe--revokeframe-signed-payload-realignment-alpha15)
9. [Gating actions with min_assurance_level](#gating-actions-with-min_assurance_level)
10. [Consulting the reputation log](#consulting-the-reputation-log)
11. [Forward compatibility: unknown assurance levels](#forward-compatibility-unknown-assurance-levels)

---

## NID format

A NID is a structured URN:

```
urn:nps:{entity-type}:{issuer-domain}:{identifier}
```

| Segment | Values | Example |
|---------|--------|---------|
| `entity-type` | `agent` / `node` / `org` | `agent` |
| `issuer-domain` | RFC 1034 domain of the issuing CA | `ca.example.com` |
| `identifier` | Stable per-entity ID (alphanumeric, `-`, `_`, `.`) | `550e8400-e29b-41d4` |

**Examples:**

```
urn:nps:agent:ca.example.com:550e8400-e29b-41d4    ← AI agent
urn:nps:node:api.myapp.com:products                 ← NWP node
urn:nps:org:mycompany.com                            ← Organization CA
```

Validation rule: the `identifier` segment MUST match `^[A-Za-z0-9\-_.]+$`. Reject any NID that fails this pattern with `NIP-CERT-SIGNATURE-INVALID` before attempting signature verification.

---

## Getting a NID

Two paths are available today; a third is planned once ACME support stabilizes.

### Path 1: NIP CA Server (OSS, self-hosted)

The NIP CA Server is an open-source ASP.NET Core service you run yourself:

```
POST /v1/agents/register           ← requires an Operator Certificate
→ 201 { "nid": "urn:nps:agent:...", "ident_frame": { ... } }
```

The CA returns a signed IdentFrame ready to use. Certificate validity is 30 days; auto-renewal opens 7 days before expiry via `POST /v1/agents/{nid}/renew`.

Discovery endpoint: `GET /.well-known/nps-ca` returns the CA's public key, supported algorithms, and endpoint URLs.

**Typed remote CA client (`NipCaClient`):** The SDK family ships a typed remote NIP CA client that wraps the OSS CA API — CA discovery (`/.well-known/nps-ca`), CRL retrieval, Ed25519 register / renew / revoke / verify, and RFC-0002 X.509 registration. Use it instead of hand-rolling HTTP calls against the CA. (Exact type name may differ by language — see the source.)

**Revocation artifacts:** The CA's `GET /v1/crl` response carries an `issued_at` timestamp plus a **detached CA signature** so relying parties can verify the CRL's authenticity and freshness. (The `/.well-known/nps-ca` discovery document no longer advertises an unmapped `/ocsp` endpoint.)

### Path 2: NPS Cloud CA (managed)

For production deployments that do not want to operate their own CA, NPS Cloud CA (a commercial service operated by INNO LOTUS PTY LTD) issues and manages certificates. Contact the NPS Cloud onboarding flow; the issued IdentFrame is structurally identical to a self-hosted CA output.

The Cloud CA is the only path today that can issue **`verified` (L2)** assurance-level certificates, because it performs the legal identity binding required at that tier.

### Path 3: ACME (future — not yet available for production)

NPS-RFC-0002 defines an ACME-compatible challenge type (`agent-01`) for automated NID issuance. It is currently gated behind an `EXPERIMENTAL` marker. As of NIP v0.9, the NPS X.509 OIDs anchor to the IANA-assigned **PEN 65715** (assigned to LabAcacia 2026-05-08, NPS-CR-0004), replacing the provisional `1.3.6.1.4.1.99999` arc — the `id-nid-assurance-level` extension is `1.3.6.1.4.1.65715.2.1`. Certificates issued under the old provisional arc MUST be revoked and re-issued. Do not use this path in production until RFC-0002 reaches Accepted.

---

## Generating a keypair

Every SDK exposes a static factory method on its identity class. The method generates a fresh Ed25519 keypair, creates a self-signed pre-registration IdentFrame, and returns both:

| SDK | Call |
|-----|------|
| .NET | `NipIdentity.Generate()` |
| Python | `NipIdentity.generate()` |
| TypeScript | `NipIdentity.generate()` |
| Java | `NipIdentity.generate()` |
| Rust | `NipIdentity::generate()` |
| Go | `nipidentity.Generate()` |

The returned object exposes:
- `PrivateKeyBytes` / `private_key_bytes` — 32-byte Ed25519 private scalar; **store in an HSM or encrypted file (mode 0600)**. Never log or transmit this.
- `PublicKeyEncoded` / `pub_key` — wire-format string `ed25519:{base64url(DER)}`.
- A draft IdentFrame with `pub_key` populated and `signature` left empty (you submit this to the CA for signing).

After the CA signs and returns the IdentFrame, persist the complete signed frame alongside the private key.

---

## Presenting an IdentFrame

An IdentFrame (type `0x20`) is sent as the **handshake frame** on every new connection — before any QueryFrame, ActionFrame, or SubscribeFrame. The frame is signed by the issuing CA over the canonical JSON of the frame with the `signature` field excluded (keys sorted alphabetically, no whitespace).

**Minimal required fields:**

```json
{
  "frame": "0x20",
  "nid": "urn:nps:agent:ca.example.com:my-agent-01",
  "pub_key": "ed25519:MCowBQYDK2VwAyEA...",
  "capabilities": ["nwp:query", "nwp:action"],
  "scope": {
    "nodes": ["nwp://api.example.com/*"],
    "actions": ["orders:read"],
    "max_token_budget": 50000
  },
  "issued_by": "urn:nps:org:ca.example.com",
  "issued_at": "2026-04-10T00:00:00Z",
  "expires_at": "2026-05-10T00:00:00Z",
  "serial": "0x0A3F9C",
  "signature": "ed25519:3045022100..."
}
```

**`assurance_level` field:**

Include `assurance_level` whenever your NID certificate carries the `id-nid-assurance-level` extension. If you omit it, receivers treat the identity as `"anonymous"` (backward compatible with pre-RFC-0003 NIDs).

When both the field and the cert extension are present, they MUST carry the same value. A mismatch (downgrade-attack defense) returns `NIP-ASSURANCE-MISMATCH`.

**`metadata` (optional, not signed):**

The `metadata` object is excluded from signature computation and MAY be updated at runtime. Nodes use it for CGN tokenizer auto-match:

```json
"metadata": {
  "model_family": "anthropic/claude-4",
  "tokenizer": "claude",
  "runtime": "custom/1.0"
}
```

**Certificate fields (`cert_format` / `cert_chain`, NIP v0.7+):**

The IdentFrame carries the issuing certificate so receivers can validate the chain:

- `cert_format` (required) — certificate encoding, one of `"x509-der"` or `"raw-pubkey"`.
- `cert_chain` (required when `cert_format = "x509-der"`) — array of base64url-encoded DER certificates, leaf first. MUST be omitted when `cert_format = "raw-pubkey"`.

Both fields are **excluded from the signed canonical JSON** (same exclusion pattern as `metadata`); see Receiver-side verification.

**`ocsp_staple` (optional, NIP v0.9):**

An Agent MAY attach a pre-fetched OCSP response in `ocsp_staple` (base64url-encoded DER `OCSPResponse`) so the receiving Node can check revocation status without a live OCSP round-trip. Receivers SHOULD verify the staple signature and `nextUpdate`; an elapsed `nextUpdate` returns `NIP-OCSP-STAPLE-EXPIRED`. When absent, the Node MAY perform an online OCSP lookup against `NWM.ocsp_url`.

**`node_roles` (optional, NIP v0.10):**

Self-declared node-role tags (e.g. `["memory", "orchestrator"]`), same vocabulary as NDP `AnnounceFrame.node_roles`. Like `cert_format` / `cert_chain`, `node_roles` is **excluded from the Ed25519-signed payload**. At Phase 1–2 it is self-declared and informational; at the Phase 3 flag day it MUST match the `id-nps-node-roles` X.509 extension (`1.3.6.1.4.1.65715.2.2`), and a mismatch returns `NIP-CERT-NODE-ROLES-MISMATCH` (`NPS-CLIENT-BAD-FRAME`). Use `node_roles`, never the legacy `node_kind` field (`node_kind` was a parse-only alias through alpha.5 only).

**`lineage` (optional, NPS-CR-0003):**

When the NID is an orchestrator **group** or a short-lived **session**, the IdentFrame carries a signed `lineage` object. Unlike `metadata`, `lineage` **is part of the signed canonical JSON** — tampering invalidates the CA signature. Group and session NIDs use reserved identifier prefixes (`group-` / `session-`):

```json
"lineage": {
  "role":       "session",
  "parent_nid": "urn:nps:agent:ca.example.com:group-7f3c9e1a-b2d8-4c6f-9a01",
  "group_nid":  "urn:nps:agent:ca.example.com:group-7f3c9e1a-b2d8-4c6f-9a01",
  "session_id": "session-1714672800-f3a92c0b",
  "purpose":    "data-extraction-job-42"
}
```

Group NIDs default to 365-day validity; session NIDs default to 1 hour (max 24 hours), with the portion after `session-` of the form `{unix-timestamp}-{random}` (≥8 hex chars). See Receiver-side verification for the parent chain-check step.

**Standard capability values to include:**

| Capability | Purpose |
|------------|---------|
| `nwp:query` | May query Memory Nodes |
| `nwp:action` | May invoke Action Nodes |
| `nwp:stream` | May receive StreamFrame responses |
| `ncp:stream` | May initiate NCP streaming |
| `nop:delegate` | May delegate subtasks |
| `nop:orchestrate` | May act as an orchestrator and emit TaskFrames |
| `topology:read` | May read Anchor Node topology (required if your Agent monitors cluster health) |

---

## Assurance levels

NPS-RFC-0003 defines three tiers. The tier travels in `IdentFrame.assurance_level` and is the input to `NWM.min_assurance_level` policy enforcement on Nodes.

| Level | Wire value | What the CA must do | When to use |
|-------|------------|---------------------|-------------|
| L0 | `"anonymous"` | Self-signed, or CA-signed without out-of-band identity check | Dev/test, public read-only endpoints, hobbyist Agents |
| L1 | `"attested"` | CA attests key possession (ACME `agent-01` challenge) and verifies contact email or domain | Most production Agents; standard rate-limit tier |
| L2 | `"verified"` | L1 plus CA binds the operator's legal identity (corporate registration or signed AaaS-operator attestation) | Regulated integrations, contract-grade orchestration, premium paid tiers |

The levels are **ordered**: `anonymous < attested < verified`. A request whose level is below the Node's declared minimum is rejected with `NWP-AUTH-ASSURANCE-TOO-LOW` (`NPS-AUTH-FORBIDDEN`). The response SHOULD include a `hint` pointing to a CA enrolment URL.

**Note on L1 availability:** `"attested"` formally requires RFC-0002 to be in Accepted status. The IANA PEN has now been assigned — **PEN 65715** (to LabAcacia, 2026-05-08, NPS-CR-0004) — and the `id-nid-assurance-level` X.509 extension uses OID `1.3.6.1.4.1.65715.2.1` (replacing the provisional `1.3.6.1.4.1.99999` arc; certs under the old arc MUST be revoked and re-issued). Confirm RFC-0002 is in Accepted status before relying on L1 for regulated use cases.

**Phase gate:** Phase 1–2 (current) enforcement is opt-in (`SHOULD check, MAY enforce`). Starting the Phase 3 flag day, enforcement is `MUST`, and violations return `NIP-ASSURANCE-MISMATCH`.

---

## The empty-string bug fix (alpha.5)

In versions before alpha.5, some SDKs mapped the empty string `""` to `null` during deserialization of `assurance_level`, while others threw a parse error or silently treated it as `"anonymous"`. This created cross-SDK interop failures when a publisher omitted the field entirely vs. sent an empty string.

**Correct behavior (all SDKs as of alpha.5):**

An absent `assurance_level` field AND an empty-string value `""` both mean "no assertion made" and MUST be treated as `"anonymous"`:

| SDK | Correct guard pattern |
|-----|-----------------------|
| Python | `level = wire if wire else "anonymous"` |
| TypeScript / Go | `level = wire == "" ? "anonymous" : wire` |
| .NET | `level = string.IsNullOrEmpty(wire) ? "anonymous" : wire` |
| Java | `level = (wire == null \|\| wire.isEmpty()) ? "anonymous" : wire` |
| Rust | `level = if wire.is_empty() { "anonymous" } else { wire }` |

The rationale: blank = no assertion made; the protocol default is the weakest tier. An empty string arriving on the wire is a publisher bug, not a security signal — treating it as an error would break backward compatibility with pre-RFC-0003 publishers.

---

## Receiver-side verification

When a Node receives an IdentFrame, it MUST perform these checks in order:

```
1.  Check expires_at > now                → NIP-CERT-EXPIRED
2.  Check issued_by ∈ NWM.trusted_issuers → NIP-CERT-UNTRUSTED-ISSUER
3.  Verify Ed25519 signature              → NIP-CERT-SIGNATURE-INVALID
3a. (CR-0003) If lineage.parent_nid present, OCSP-lookup the parent
                                          → NIP-CERT-PARENT-REVOKED
4.  OCSP staple / lookup / local CRL check → NIP-CERT-REVOKED
                                            (stale staple → NIP-OCSP-STAPLE-EXPIRED)
5.  Check required capabilities present   → NIP-CERT-CAPABILITY-MISSING
6.  Check scope.nodes covers target path  → NWP-AUTH-NID-SCOPE-VIOLATION
```

All checks pass → authorize the request.

Step **3a** (NPS-CR-0003) is mandatory whenever `lineage.parent_nid` is present, regardless of whether the session NID is still within its own validity window — a revoked or expired group NID cascades to its sessions (`parent_revoked` reason on the group's RevokeFrame).

At step 4, if the IdentFrame carries an `ocsp_staple` (NIP v0.9), verify the staple signature against the issuing CA cert and check `nextUpdate`; a passed staple supersedes cached revocation state and the Node SHOULD NOT make a live OCSP request. An elapsed `nextUpdate` returns `NIP-OCSP-STAPLE-EXPIRED`.

**Signature verification (canonical form):**

Reconstruct the JSON with the `signature` field removed (and the unsigned `metadata`, `cert_format`, `cert_chain`, and `node_roles` fields excluded), keys sorted alphabetically, no whitespace. Note `lineage` **is** signed and stays in. Feed the UTF-8 bytes to your Ed25519 verify function along with the public key from `pub_key`.

```
// Pseudo-code (applies to every SDK)
frame_copy = remove_field(ident_frame, "signature")
canonical  = json_serialize(frame_copy, sort_keys=true, compact=true)
ok = ed25519_verify(
    public_key = parse_pub_key(ident_frame.pub_key),
    message    = canonical.to_utf8_bytes(),
    signature  = parse_signature(ident_frame.signature)
)
```

**Optionally check assurance level against min_assurance_level:**

```
// Pseudo-code
required = nwm.min_assurance_level ?? "anonymous"
actual   = ident_frame.assurance_level ?? "anonymous"   // empty-string → anonymous (alpha.5 fix)
if assurance_rank(actual) < assurance_rank(required):
    return error NWP-AUTH-ASSURANCE-TOO-LOW
```

Where `assurance_rank("anonymous") = 0`, `assurance_rank("attested") = 1`, `assurance_rank("verified") = 2`.

---

## TrustFrame / RevokeFrame signed-payload realignment (alpha.15)

`TrustFrame (0x21)` and `RevokeFrame (0x22)` are signed over the **canonical JSON of the frame with the `signature` field removed** (keys sorted alphabetically, no whitespace) — the same RFC 8785 (JCS) canonicalisation rule as the IdentFrame (NPS-3 §5.2 / §5.3).

**Realignment (breaking):** As of **alpha.15** the SDKs' signed payload was realigned to the current NPS-3 field set. The signed body now covers the current per-frame fields — for the TrustFrame that includes `issued_at`, `serial`, and `signer_nid` (added at NIP v0.8 for revocation/audit traceability); for the RevokeFrame it includes `target_nid`, `serial`, `reason`, `revoked_at`, `signer_nid` (and `parent_nid` on a cascade). Revocation now reports using current naming — `NIP-CERT-REVOKED` for a revoked certificate. This is a **breaking** wire change: signed frames produced by the old alpha.14-era SDK shape no longer verify after upgrading. Re-sign any persisted TrustFrames / RevokeFrames with the alpha.15 SDK.

**RevokeFrame `parent_nid` ↔ `parent_revoked` guard:** The `parent_nid` field is governed by a strict conditional rule (NPS-3 §5.3):

- `parent_nid` is **REQUIRED** when `reason = "parent_revoked"` — it identifies the group NID whose cascade triggered this revocation (NPS-CR-0003).
- `parent_nid` **MUST be omitted** for any other reason.

The SDKs enforce this both ways: a `parent_revoked` revocation missing `parent_nid`, or a non-`parent_revoked` revocation that carries `parent_nid`, is rejected as malformed (`NIP-REVOKE-FRAME-INVALID`, `NPS-CLIENT-BAD-FRAME`). `parent_revoked` is the reason a CA sets on a session-NID RevokeFrame it emits as part of cascade revocation when the session's group NID was revoked, distinguishing ancestry-driven invalidation from a session revoked on its own merits.

---

## Gating actions with min_assurance_level

Set `min_assurance_level` at two levels:

**Node-wide (NWM top-level):**

```json
{
  "nwp": "0.14",
  "node_type": "action",
  "min_assurance_level": "attested",
  ...
}
```

All requests to this Node must present at least `"attested"`.

**Per-action override (ActionSpec):**

```json
"actions": {
  "orders.read": {
    "min_assurance_level": "anonymous"
  },
  "orders.delete": {
    "min_assurance_level": "verified"
  }
}
```

The per-action value takes precedence over the top-level NWM value for requests targeting that action. Requests presenting a level lower than the effective minimum MUST be rejected with `NWP-AUTH-ASSURANCE-TOO-LOW`.

**Runtime gate check (pseudo-code applicable to all SDKs):**

```
function check_assurance_gate(request, action_spec, nwm):
    // Per-action override takes precedence
    required = action_spec.min_assurance_level
               ?? nwm.min_assurance_level
               ?? "anonymous"

    actual = normalize_assurance(request.ident_frame.assurance_level)
    // normalize: null or "" → "anonymous"

    if assurance_rank(actual) < assurance_rank(required):
        raise AuthError(
            code = "NWP-AUTH-ASSURANCE-TOO-LOW",
            hint = nwm.auth.enrolment_url
        )
```

---

## Consulting the reputation log

The reputation log (NPS-RFC-0004) records behavioral incidents per NID in a Certificate-Transparency-style append-only feed. A Node may optionally configure a `reputation_policy` in its NWM to reject Agents with certain incident types.

All six SDKs (Python / TypeScript / Go / Java / Rust / .NET) ship a **`ReputationLogClient`** (RFC-0004 Phase 2, added in alpha.7) for querying and verifying log entries. Entries use a **dual Ed25519 signature** model — the issuer signs the entry, then the log operator re-signs for the ordering commitment — and the client verifies inclusion against a `SignedTreeHead` using an `InclusionProof` with an RFC 9162-style Merkle fold.

**At AaaS L2 (recommended minimum policy):**

Reject Agents with:
- An active `cert-revoked` incident of any severity.
- A `rate-limit-violation` or `tos-violation` incident of `major` or higher within the last 30 days.

**Querying the log at admission time:**

```
// Pseudo-code (ReputationLogClient, all six SDKs)
entries = reputation_log_client.query(subject_nid = agent_nid)
// Phase 2: verify each entry's inclusion proof against the SignedTreeHead
for entry in entries:
    if matches_reject_rule(entry, policy):
        raise AuthError(
            code = "NWP-AUTH-REPUTATION-BLOCKED",
            details = {
                incident: entry.incident,
                severity: entry.severity,
                seq:      entry.seq
            }
        )
```

If the log operator is unreachable and `reputation_policy.on_log_unreachable = "deny"`, return `NIP-REPUTATION-LOG-UNREACHABLE` (`NPS-DOWNSTREAM-UNAVAILABLE`). The default recommendation is `"allow"` (fail-open) unless you are operating a high-assurance endpoint.

---

## Forward compatibility: unknown assurance levels

If a future spec revision introduces a fourth tier (e.g., `"sovereign"`), older implementations will encounter an `assurance_level` value not in the current enum. The correct behavior is:

**Reject with `NIP-ASSURANCE-UNKNOWN` (`NPS-CLIENT-BAD-FRAME`) — do NOT silently demote to `"anonymous"`.**

Demotion creates a security hole: a publisher asserting a future higher-assurance level would be treated as unauthenticated rather than as "authenticity unknown". Explicit rejection lets the caller know it needs to upgrade.

```
// Pseudo-code
KNOWN_LEVELS = {"anonymous", "attested", "verified"}
level = normalize_assurance(wire_value)   // "" → "anonymous"
if level not in KNOWN_LEVELS:
    raise ProtocolError(code = "NIP-ASSURANCE-UNKNOWN")
```

---

## See also

- [Protocol NIP](./Protocol-NIP.md) — full NIP spec (v0.10) including TrustFrame, RevokeFrame, `cert_chain`/`ocsp_staple`/`node_roles`, lineage, the PEN 65715 OID arc (`id-nps-node-roles` 65715.2.2 / `id-nps-capabilities` 65715.2.3), CA hierarchy
- [Operator Reputation Log](./Operator-Reputation-Log.md) — operating an RFC-0004-compliant log

---

*Last reviewed at suite version: v1.0.0-alpha.19 release*
