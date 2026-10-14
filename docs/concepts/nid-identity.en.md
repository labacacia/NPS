English | [中文版](./nid-identity.md)

# NID Identity System

NPS builds Agent identity directly into the protocol layer, replacing the conventional approach of relying on HTTP headers or application-level tokens.

---

## NID Format

A **NID (Neural Identity Descriptor)** is the unique identifier for nodes and agents in NPS:

```
urn:nps:{entity-type}:{ca-host}:{id}
```

| Field | Description | Example |
|-------|-------------|---------|
| `entity-type` | Entity type | `agent`, `node`, `org` |
| `ca-host` | Domain of the CA that issued this NID | `ca.example.com` |
| `id` | Unique entity identifier; organization CA NIDs omit this segment | `gpt-agent-01` |

**Examples:**

```
urn:nps:agent:ca.example.com:gpt-agent-01    # Agent NID
urn:nps:node:api.example.com:products         # Node NID
urn:nps:org:ca.example.com                    # Organization CA NID (no id segment)
```

---

## Certificate Structure

Each NID is backed by an `IdentFrame` issued by a NIP CA:

| Field | Description |
|-------|-------------|
| `nid` | Unique identity descriptor |
| `pub_key` | Ed25519 public key (`ed25519:{base64url}`) |
| `capabilities` | Declared capability list (e.g. `nwp:query`) |
| `scope` | Allowed node path set (JSON) |
| `issued_by` | NID of the issuing CA |
| `issued_at` / `expires_at` | Issuance/expiry timestamps (ISO 8601 UTC) |
| `serial` | Globally unique certificate serial number |
| `signature` | CA's Ed25519 signature over the canonical JSON payload |

---

## Signing Mechanism

All NIP frames (IdentFrame, TrustFrame, RevokeFrame, AnnounceFrame) use **Ed25519** signatures:

1. Serialize the payload to **canonical JSON** (RFC 8785 JCS — fields sorted alphabetically, no whitespace)
2. Sign the UTF-8 bytes of the canonical JSON with Ed25519
3. Store the Base64URL-encoded signature in the `signature` field

Key storage: the CA private key is encrypted with **PBKDF2-SHA256 (600 000 iterations) + AES-256-GCM** and saved to disk, managed by `NipKeyManager`.

---

## CA Issuance Flow

```
Agent/Node                CA Server (NipCaService)
    │                           │
    │──── POST /v1/agents/register ─────▶│
    │   { identifier,            │
    │     pubKey, capabilities,  │
    │     scopeJson }            │
    │                           │── build NID
    │                           │── validate public key format
    │                           │── sign IdentFrame
    │                           │── store NipCertRecord
    │◀──── IdentFrame ──────────│
    │                           │
```

---

## Node-Side Six-Step Verification

When a node receives a request carrying an IdentFrame, `NipIdentVerifier` performs checks in this order:

| Step | Check | Failure Reason |
|------|-------|----------------|
| 1 | `expires_at > now` | Certificate has expired |
| 2 | `issued_by` is in the node's trust list | Untrusted CA |
| 3 | Ed25519 signature is valid | Invalid signature (possible tampering) |
| 4 | Local CRL + OCSP lookup | Certificate has been revoked |
| 5 | `capabilities` includes all required capabilities | Missing required capability |
| 6 | `scope` covers the current request path | Insufficient scope (authorization violation) |

Any failing step rejects the request with the corresponding error code.

---

## Scope Format

`scope` is a JSON object; the `paths` array lists allowed paths with wildcard support:

```json
{
  "paths": [
    "/products/*",
    "/orders/**",
    "/admin/reports"
  ]
}
```

| Pattern | Matches |
|---------|---------|
| `/products/123` | Exact match |
| `/products/*` | Single-segment wildcard (no sub-paths) |
| `/orders/**` | Multi-segment wildcard (all sub-paths) |

---

## Certificate Renewal and Revocation

### Renewal

Certificates can only be renewed within the **renewal window** (`RenewalWindowDays` days before expiry). Requests outside the window return `NIP-CA-RENEWAL-TOO-EARLY`.

```
POST /v1/agents/urn%3Anps%3Aagent%3Aca.example.com%3Agpt-agent-01/renew
→ New IdentFrame (updated issued_at / expires_at / signature)
```

### Revocation

Takes effect immediately — written to the CA database, returned as a `RevokeFrame`, and published through CRL / online verification:

```
POST /v1/agents/{nid}/revoke
{ "reason": "key_compromise" }
→ RevokeFrame
```

Common `reason` values: `key_compromise`, `affiliation_changed`, `superseded`, `cessation_of_operation`.

---

## Online Status Checks

Verification step 4 can call the CA's OCSP-style verify endpoint for real-time revocation status:

```
GET /v1/agents/{nid}/verify
→ { "valid": true } | { "valid": false, "error": "NIP-CERT-REVOKED" }
```

These verify responses are enforced to take a minimum of 200 ms (`NormalizeOcspResponseTime = true`) to prevent timing side-channel attacks.

---

## Related Documentation

- [NPS.NIP API Reference](../sdk/dotnet/nps-nip.en.md)
- [NIP Specification](../../spec/NPS-3-NIP.md)
