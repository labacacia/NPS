# Reference: Error Codes

**Status:** ✅ Reviewed for v1.0.0-alpha.19 release

NPS uses a two-level error system. This page documents the **protocol error codes** — the fine-grained layer. Each code names exactly what went wrong in a specific protocol domain. The coarser layer, NPS status codes, classifies the error for transport routing; see [Reference: Status Codes](./Reference-Status-Codes.md).

## Code Format

Every error code follows the pattern:

```
DOMAIN-CATEGORY-DETAIL
```

All uppercase, hyphen-separated. The domain prefix matches the owning protocol: `NCP`, `NWP`, `NIP`, `NDP`, or `NOP`. The category groups related failures (e.g. `AUTH`, `QUERY`, `TASK`). The detail distinguishes the specific condition.

Examples: `NCP-ANCHOR-NOT-FOUND`, `NWP-AUTH-NID-EXPIRED`, `NOP-DELEGATE-CHAIN-TOO-DEEP`.

## The Two-Level System

```
Protocol error code   →   NPS status code         →   HTTP status (overlay mode)
NCP-ANCHOR-NOT-FOUND  →   NPS-CLIENT-NOT-FOUND    →   404
NWP-BUDGET-EXCEEDED   →   NPS-LIMIT-BUDGET        →   429
NIP-CERT-EXPIRED      →   NPS-AUTH-UNAUTHENTICATED →  401
```

Clients should branch on the **NPS status code** for generic retry/backoff logic. Use the **protocol error code** for precise diagnostic messages and agent-side recovery logic (for example, detecting `NCP-ANCHOR-STALE` to trigger a schema refresh).

---

> **Added in alpha.5**
>
> The following codes were introduced in v1.0.0-alpha.5:
>
> - `NWP-RESERVED-TYPE-UNSUPPORTED` — unrecognized reserved query/subscribe `type` field value
> - `NDP-ANNOUNCE-ROLE-REMOVED` — legacy `"gateway"` value in `node_roles` (NPS-CR-0001)
> - `NDP-ANNOUNCE-ROLE-UNKNOWN` — unrecognized value in `node_roles`
> - `NWP-MANIFEST-NODE-TYPE-REMOVED` — legacy `"gateway"` value in NWM `node_type` (NPS-CR-0001)
> - `NWP-MANIFEST-NODE-TYPE-UNKNOWN` — unrecognized NWM `node_type`
> - `NIP-CERT-EXPIRED`, `NIP-CERT-REVOKED`, `NIP-CERT-SIGNATURE-INVALID`, `NIP-CERT-UNTRUSTED-ISSUER`, `NIP-CERT-CAPABILITY-MISSING`, `NIP-CERT-SCOPE-VIOLATION`, `NIP-CERT-FORMAT-INVALID`, `NIP-CERT-EKU-MISSING`, `NIP-CERT-SUBJECT-NID-MISMATCH`, `NIP-ACME-CHALLENGE-FAILED` — NIP-namespaced certificate error codes (previously handled via NWP-AUTH-* codes)
> - `NIP-REPUTATION-GOSSIP-FORK` — Merkle tree fork detected during STH gossip (NPS-RFC-0004 §4.5)
> - `NIP-REPUTATION-GOSSIP-SIG-INVALID` — peer STH signature invalid during gossip exchange
> - `NWP-TOPOLOGY-UNAUTHORIZED`, `NWP-TOPOLOGY-UNSUPPORTED-SCOPE`, `NWP-TOPOLOGY-DEPTH-UNSUPPORTED`, `NWP-TOPOLOGY-FILTER-UNSUPPORTED` — topology query errors (NPS-CR-0002)

---

> **Added in alpha.6 – alpha.13**
>
> The following codes were introduced across v1.0.0-alpha.6 through v1.0.0-alpha.15:
>
> - `NCP-NID-MISMATCH` — native-mode mTLS / resumed-session NID mismatch (NPS-RFC-0006)
> - `NCP-KEEPALIVE-TIMEOUT` — no frame (incl. NopFrame) within 3 × `ping_interval_ms` (NCP v0.8)
> - `NCP-FRAME-FLAGS-INVALID` — reserved bits in the flags field are non-zero, including the reserved encoding tier `0b11` (NCP v0.9)
> - `NCP-BINARY-VECTOR-MALFORMED`, `NCP-BINARY-VECTOR-DIM-MISMATCH`, `NCP-BINARY-VECTOR-INDEX-INVALID`, `NCP-BINARY-VECTOR-DTYPE-UNSUPPORTED`, `NCP-BINARY-VECTOR-TRUNCATED` — Tier-3 BinaryVector (`binary_vector.v1`) payload validation; all map to `NPS-CLIENT-BAD-FRAME` (NCP v0.9)
> - `NWP-REPUTATION-THROTTLED`, `NWP-REPUTATION-REJECTED`, `NWP-REPUTATION-BANNED` — reputation-policy enforcement (NPS-RFC-0005); supersede the now-deprecated `NWP-AUTH-REPUTATION-BLOCKED`
> - `NWP-CGN-LIMIT-EXCEEDED` — response would exceed the effective Cognon budget (token-budget.md §7.4)
> - `NIP-CERT-NODE-ROLES-MISMATCH` — `IdentFrame.node_roles` vs `id-nps-node-roles` extension, Phase 3 (NIP v0.10)
> - `NIP-OCSP-STAPLE-EXPIRED` — stale `IdentFrame.ocsp_staple` (NIP v0.9)
> - `NIP-TRUST-FRAME-EXPIRED`, `NIP-TRUST-FRAME-GRANTOR-REVOKED`, `NIP-TRUST-FRAME-SCOPE-EXCEEDS-GRANTOR`, `NIP-TRUST-FRAME-NODES-PATTERN-INVALID` — TrustFrame validation (NPS-3 §5.2)
> - `NIP-REVOKE-FRAME-INVALID`, `NIP-REVOKE-FRAME-UNAUTHORIZED-ISSUER`, `NIP-REVOKE-FRAME-SERIAL-MISMATCH`, `NIP-REVOKE-FRAME-REASON-UNKNOWN` — RevokeFrame validation (NPS-3 §5.3)
> - `NIP-CA-GROUP-REVOKED`, `NIP-CA-PARENT-NOT-FOUND`, `NIP-CA-PARENT-NOT-GROUP`, `NIP-CA-SESSION-VALIDITY-INVALID`, `NIP-CA-JWS-INVALID`, `NIP-CA-JWS-EXPIRED`, `NIP-CERT-PARENT-REVOKED` — group / session NID issuance (NPS-CR-0003)
> - `NDP-RESOLVE-STALE`, `NDP-ANNOUNCE-STALE` — stale registration / expired announce heartbeat (NDP v0.9)
> - `NDP-ANNOUNCE-CONFLICT`, `NDP-GRAPH-SEQ-ROLLBACK`, `NDP-ISSUER-NOT-ALLOWED`, `NDP-CA-ATTEST-REQUIRED` — registry-integrity / federation (NDP v0.8, NPS-4 §7)
> - `NDP-GRAPH-INVALID`, `NDP-GRAPH-TOO-LARGE` — GraphFrame topology-snapshot validation (NDP v0.8)
> - `NDP-FEDERATION-LOOP` — federation forwarding loop detection (NDP §9, max 3 hops)
> - `NOP-COMPENSATION-FAILED`, `NOP-COMPENSATION-NOT-SUPPORTED` — saga rollback (NOP v0.6)
> - `NOP-CALLBACK-HMAC-MISSING` — webhook callback missing `X-NPS-Signature` (NOP v0.6)
> - `NOP-CLAIM-CONFLICT`, `NOP-SPAWN-SPEC-INVALID`, `NOP-RUNTIME-IDLE-TIMEOUT`, `NOP-RUNTIME-MAX-RUNTIME` — NOP L3 runtime lease (NPS-CR-0007)
> - `NOP-TASK-RESULT-EXPIRED` — result requested after `result_ttl_seconds` (NOP v0.7)
> - `NOP-STREAM-NAK-UNRESOLVABLE` — NAK retransmission for an evicted frame (NOP v0.7)

---

## NCP Error Codes

Neural Communication Protocol — wire format and framing layer.

| Code | NPS Status Code | HTTP Equivalent | Description |
|------|-----------------|-----------------|-------------|
| `NCP-ANCHOR-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | 404 | Schema referenced by `anchor_ref` does not exist; agent must re-fetch the AnchorFrame |
| `NCP-ANCHOR-SCHEMA-INVALID` | `NPS-CLIENT-BAD-FRAME` | 400 | Schema in AnchorFrame is malformed |
| `NCP-ANCHOR-ID-MISMATCH` | `NPS-CLIENT-CONFLICT` | 409 | Different schemas received for the same `anchor_id` (anchor-pollution defence) |
| `NCP-ANCHOR-STALE` | `NPS-CLIENT-CONFLICT` | 409 | `anchor_ref` exists but the schema has been updated; the response carries the latest AnchorFrame via `CapsFrame.inline_anchor` |
| `NCP-FRAME-UNKNOWN-TYPE` | `NPS-CLIENT-BAD-FRAME` | 400 | Unknown frame type code |
| `NCP-FRAME-PAYLOAD-TOO-LARGE` | `NPS-LIMIT-PAYLOAD` | 413 | Payload exceeds the negotiated `max_frame_payload` |
| `NCP-FRAME-FLAGS-INVALID` | `NPS-CLIENT-BAD-FRAME` | 400 | Reserved bits in the flags field are non-zero (e.g. the reserved encoding tier `0b11`) |
| `NCP-BINARY-VECTOR-MALFORMED` | `NPS-CLIENT-BAD-FRAME` | 400 | Tier-3 BinaryVector (`binary_vector.v1`) payload is malformed (NCP v0.9) |
| `NCP-BINARY-VECTOR-DIM-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | 400 | BinaryVector marker dimension does not match the vector segment (NCP v0.9) |
| `NCP-BINARY-VECTOR-INDEX-INVALID` | `NPS-CLIENT-BAD-FRAME` | 400 | BinaryVector marker references a missing vector segment (NCP v0.9) |
| `NCP-BINARY-VECTOR-DTYPE-UNSUPPORTED` | `NPS-CLIENT-BAD-FRAME` | 400 | BinaryVector marker uses an unsupported dtype (NCP v0.9) |
| `NCP-BINARY-VECTOR-TRUNCATED` | `NPS-CLIENT-BAD-FRAME` | 400 | BinaryVector vector segment is truncated (NCP v0.9) |
| `NCP-STREAM-SEQ-GAP` | `NPS-STREAM-SEQ-GAP` | 422 | StreamFrame sequence numbers are not contiguous |
| `NCP-STREAM-NOT-FOUND` | `NPS-STREAM-NOT-FOUND` | 404 | Stream referenced by `stream_id` does not exist |
| `NCP-STREAM-LIMIT-EXCEEDED` | `NPS-STREAM-LIMIT` | 429 | Maximum concurrent streams per connection exceeded |
| `NCP-STREAM-WINDOW-OVERFLOW` | `NPS-STREAM-LIMIT` | 429 | Sender continues emitting StreamFrames after the flow-control window is exhausted |
| `NCP-ENCODING-UNSUPPORTED` | `NPS-SERVER-ENCODING-UNSUPPORTED` | 415 | Requested encoding tier is not supported by this node |
| `NCP-DIFF-FORMAT-UNSUPPORTED` | `NPS-CLIENT-BAD-FRAME` | 400 | DiffFrame uses a `patch_format` the receiver does not support (e.g. `binary_bitset`) |
| `NCP-VERSION-INCOMPATIBLE` | `NPS-PROTO-VERSION-INCOMPATIBLE` | 426 | Client `min_version` is higher than the server's maximum supported version (handshake failure) |
| `NCP-ENC-NOT-NEGOTIATED` | `NPS-CLIENT-BAD-FRAME` | 400 | Received an ENC=1 frame but no E2E encryption algorithm was negotiated for the session |
| `NCP-ENC-AUTH-FAILED` | `NPS-CLIENT-BAD-FRAME` | 400 | E2E encryption auth-tag verification failed; the frame may have been tampered with |
| `NCP-PREAMBLE-INVALID` | `NPS-PROTO-PREAMBLE-INVALID` | 400 (not emitted) | Native-mode connection opened with bytes other than the constant preamble `b"NPS/1.0\n"`; server closes silently without emitting an ErrorFrame (NPS-RFC-0001) |
| `NCP-NID-MISMATCH` | `NPS-AUTH-UNAUTHENTICATED` | 401 | Native-mode mTLS client-certificate NID does not match the session `IdentFrame` NID, or a resumed TLS session's certificate NID differs from the ticket-bound NID (NPS-RFC-0006 §6.3–§6.4) |
| `NCP-REKEY-REQUIRED` | `NPS-PROTO-VERSION-INCOMPATIBLE` | 426 | E2E-encrypted channel has reached the rekey threshold (2^32 frames or 24 h); peer MUST initiate key rotation before sending more encrypted frames (NCP v0.7 §7.4) |
| `NCP-KEEPALIVE-TIMEOUT` | `NPS-SERVER-TIMEOUT` | 408/504 | No frame (including NopFrame) received within 3 × `ping_interval_ms`; connection will be closed (NCP v0.8 §7.6) |

---

## NWP Error Codes

Neural Web Protocol — query, action, subscription, manifest, and topology layer.

### Authentication

| Code | NPS Status Code | HTTP Equivalent | Description |
|------|-----------------|-----------------|-------------|
| `NWP-AUTH-NID-SCOPE-VIOLATION` | `NPS-AUTH-FORBIDDEN` | 403 | Agent scope does not cover the target node path |
| `NWP-AUTH-NID-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | 401 | NID certificate has expired |
| `NWP-AUTH-NID-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | 401 | NID has been revoked |
| `NWP-AUTH-NID-UNTRUSTED-ISSUER` | `NPS-AUTH-UNAUTHENTICATED` | 401 | NID issuer is not in `trusted_issuers` |
| `NWP-AUTH-NID-CAPABILITY-MISSING` | `NPS-AUTH-FORBIDDEN` | 403 | Agent is missing a capability required by the node (e.g. `nwp:query`) |
| `NWP-AUTH-ASSURANCE-TOO-LOW` | `NPS-AUTH-FORBIDDEN` | 403 | Agent's assurance level is below the node's `min_assurance_level`; response SHOULD include a `hint` pointing to a CA enrolment URL (NPS-RFC-0003) |
| `NWP-AUTH-REPUTATION-BLOCKED` | `NPS-AUTH-FORBIDDEN` | 403 | **Deprecated** (NPS-RFC-0005): use `NWP-REPUTATION-REJECTED` / `NWP-REPUTATION-BANNED` instead. Retained as an alias for one alpha cycle. |
| `NWP-REPUTATION-THROTTLED` | `NPS-CLIENT-RATE-LIMITED` | 429 | Request rate-limited by reputation policy (`throttle_on` rule matched). Response includes a `Retry-After: 60` header (NPS-RFC-0005) <!-- spec note: mapping per error-codes.md; status-codes.md still lists this family as NPS-LIMIT-RATE (upstream spec cleanup pending) --> |
| `NWP-REPUTATION-REJECTED` | `NPS-AUTH-FORBIDDEN` | 403 | Request rejected by reputation policy (`reject_on` rule matched). Response body includes `matched_incident` + `matched_severity` (NPS-RFC-0005) |
| `NWP-REPUTATION-BANNED` | `NPS-AUTH-FORBIDDEN` | 403 | Request rejected and NID temporarily banned (`ban_on` rule matched or active ban-cache entry). Response SHOULD include an `X-NWP-Ban-Expires` Unix timestamp (NPS-RFC-0005) |

### Query

| Code | NPS Status Code | HTTP Equivalent | Description |
|------|-----------------|-----------------|-------------|
| `NWP-QUERY-FILTER-INVALID` | `NPS-CLIENT-BAD-PARAM` | 400 | Filter syntax is invalid or nests deeper than 8 levels |
| `NWP-QUERY-FIELD-UNKNOWN` | `NPS-CLIENT-BAD-PARAM` | 400 | `fields` references an unknown field |
| `NWP-QUERY-CURSOR-INVALID` | `NPS-CLIENT-BAD-PARAM` | 400 | Cursor cannot be decoded or has expired |
| `NWP-QUERY-REGEX-UNSAFE` | `NPS-CLIENT-BAD-PARAM` | 400 | `$regex` pattern rejected due to ReDoS risk, excessive length, or nested quantifiers |
| `NWP-QUERY-VECTOR-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | 501 | Node does not support vector search |
| `NWP-QUERY-AGGREGATE-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | 501 | Node does not support aggregate queries (`capabilities.aggregate=false`) |
| `NWP-QUERY-AGGREGATE-INVALID` | `NPS-CLIENT-BAD-PARAM` | 400 | Aggregate structure is invalid (unknown `func`, duplicate alias, missing required fields, etc.) |
| `NWP-QUERY-STREAM-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | 501 | Node does not support streaming queries (`capabilities.stream_query=false`) |

### Actions and Tasks

| Code | NPS Status Code | HTTP Equivalent | Description |
|------|-----------------|-----------------|-------------|
| `NWP-ACTION-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | 404 | `action_id` is not registered on the node |
| `NWP-ACTION-PARAMS-INVALID` | `NPS-CLIENT-UNPROCESSABLE` | 422 | Action params fail schema validation |
| `NWP-ACTION-IDEMPOTENCY-CONFLICT` | `NPS-CLIENT-CONFLICT` | 409 | A request with the same `idempotency_key` is already in progress |
| `NWP-TASK-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | 404 | Asynchronous task referenced by `task_id` does not exist |
| `NWP-TASK-ALREADY-CANCELLED` | `NPS-CLIENT-CONFLICT` | 409 | Task has been cancelled; further operations are not permitted |
| `NWP-TASK-ALREADY-COMPLETED` | `NPS-CLIENT-CONFLICT` | 409 | Task has completed and cannot be cancelled |
| `NWP-TASK-ALREADY-FAILED` | `NPS-CLIENT-CONFLICT` | 409 | Task has failed and cannot be cancelled |

### Subscriptions

| Code | NPS Status Code | HTTP Equivalent | Description |
|------|-----------------|-----------------|-------------|
| `NWP-SUBSCRIBE-STREAM-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | 404 | `stream_id` referenced by `unsubscribe` does not exist |
| `NWP-SUBSCRIBE-LIMIT-EXCEEDED` | `NPS-LIMIT-EXCEEDED` | 429 | Exceeded the node's maximum concurrent subscriptions |
| `NWP-SUBSCRIBE-FILTER-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | 501 | Node does not support subscriptions with a filter |
| `NWP-SUBSCRIBE-INTERRUPTED` | `NPS-SERVER-UNAVAILABLE` | 503 | Subscription stream terminated because the underlying data source was interrupted |
| `NWP-SUBSCRIBE-SEQ-TOO-OLD` | `NPS-CLIENT-CONFLICT` | 409 | `resume_from_seq` is outside the node's buffer window (recommended: 10 min or 10,000 records); agent must re-query from scratch before re-subscribing |

### Budget, Limits, and Graph

| Code | NPS Status Code | HTTP Equivalent | Description |
|------|-----------------|-----------------|-------------|
| `NWP-BUDGET-EXCEEDED` | `NPS-LIMIT-BUDGET` | 429 | Response would exceed the `X-NWP-Budget` limit |
| `NWP-CGN-LIMIT-EXCEEDED` | `NPS-CLIENT-REQUEST-TOO-LARGE` | — | Response would exceed the effective CGN budget (`min(cgn_limit, X-NWP-Budget)`); trimming was not possible. Response body SHOULD include `effective_budget` and `estimated_cgn` (token-budget.md §7.4) <!-- spec note: mapping per error-codes.md; status code not yet in status-codes.md (upstream spec cleanup pending) --> |
| `NWP-DEPTH-EXCEEDED` | `NPS-CLIENT-BAD-PARAM` | 400 | `X-NWP-Depth` exceeds the node's permitted `max_depth` |
| `NWP-GRAPH-CYCLE` | `NPS-CLIENT-UNPROCESSABLE` | 422 | Node graph contains a cyclic reference |
| `NWP-RATE-LIMIT-EXCEEDED` | `NPS-LIMIT-RATE` | 429 | Rate limit exceeded; reset timestamp is in the `X-NWP-Rate-Reset` header |
| `NWP-NODE-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | 503 | Underlying data source is temporarily unavailable |

### Manifest and Node Type

| Code | NPS Status Code | HTTP Equivalent | Description |
|------|-----------------|-----------------|-------------|
| `NWP-MANIFEST-VERSION-UNSUPPORTED` | `NPS-CLIENT-BAD-PARAM` | 400 | Agent's NPS version is lower than the node's `min_agent_version` |
| `NWP-MANIFEST-NODE-TYPE-REMOVED` | `NPS-CLIENT-BAD-FRAME` | 400 | NWM `node_type` contains the removed legacy value `"gateway"` (NPS-CR-0001); use `"anchor"` or `"bridge"`. Response SHOULD include a `hint` pointing to NPS-CR-0001. |
| `NWP-MANIFEST-NODE-TYPE-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | 400 | NWM `node_type` contains an unrecognized value that is not a known-removed legacy value |

### Reserved Type and Topology (NPS-CR-0002)

| Code | NPS Status Code | HTTP Equivalent | Description |
|------|-----------------|-----------------|-------------|
| `NWP-RESERVED-TYPE-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | 501 | `QueryFrame` or `SubscribeFrame` `type` field contains an unrecognized reserved-type identifier; this node does not implement the requested reserved operation (NWP §12) |
| `NWP-TOPOLOGY-UNAUTHORIZED` | `NPS-AUTH-FORBIDDEN` | 403 | Caller lacks permission to read this Anchor's topology (NPS-CR-0002 §12.4) |
| `NWP-TOPOLOGY-UNSUPPORTED-SCOPE` | `NPS-CLIENT-BAD-PARAM` | 400 | `topology.scope` value is not implemented by this Anchor Node |
| `NWP-TOPOLOGY-DEPTH-UNSUPPORTED` | `NPS-CLIENT-BAD-PARAM` | 400 | Requested `topology.depth` exceeds this Anchor Node's configured maximum |
| `NWP-TOPOLOGY-FILTER-UNSUPPORTED` | `NPS-CLIENT-BAD-PARAM` | 400 | `topology.filter` contains an unrecognized key or unsupported operator |

---
| `NWP-HTTP-ORIGIN-FORBIDDEN` | `NPS-AUTH-FORBIDDEN` | HTTP overlay origin policy rejected the caller before a NWP frame was admitted (NWP v0.17 §9.5) |
| `NWP-HTTP-CONTENT-TYPE-UNSUPPORTED` | `NPS-CLIENT-BAD-FRAME` | HTTP overlay request `Content-Type` is not a supported NWP frame media type (§9.5) |
| `NWP-HTTP-ACCEPT-UNSATISFIABLE` | `NPS-CLIENT-BAD-PARAM` | HTTP overlay request `Accept` cannot be satisfied by any supported response media type (§9.5) |
| `NWP-HTTP-REQUEST-ID-MISMATCH` | `NPS-CLIENT-BAD-PARAM` | Response `X-NWP-Request-ID` does not echo the request ID (§9.5) |
| `NWP-HTTP-FRAME-BODY-MALFORMED` | `NPS-CLIENT-BAD-FRAME` | HTTP body cannot be parsed as a supported NWP frame envelope (§9.5) |
| `NWP-CAPABILITY-ADVERTISED-UNIMPLEMENTED` | `NPS-SERVER-UNSUPPORTED` | NWM advertises a capability/profile the node cannot currently serve (§9.5) |

## NIP Error Codes

Neural Identity Protocol — certificates, trust chains, assurance levels, and reputation.

### Certificate Validation

| Code | NPS Status Code | HTTP Equivalent | Description |
|------|-----------------|-----------------|-------------|
| `NIP-CERT-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | 401 | Certificate has expired (`expires_at < now`) |
| `NIP-CERT-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | 401 | Certificate has been revoked (found in CRL or OCSP) |
| `NIP-CERT-SIGNATURE-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | 401 | Certificate signature verification failed |
| `NIP-CERT-UNTRUSTED-ISSUER` | `NPS-AUTH-UNAUTHENTICATED` | 401 | Issuer is not in `trusted_issuers` |
| `NIP-CERT-CAPABILITY-MISSING` | `NPS-AUTH-FORBIDDEN` | 403 | Certificate is missing a capability required by the node |
| `NIP-CERT-SCOPE-VIOLATION` | `NPS-AUTH-FORBIDDEN` | 403 | Certificate scope does not cover the target path or operation |
| `NIP-CERT-FORMAT-INVALID` | `NPS-CLIENT-BAD-FRAME` | 400 | `IdentFrame.cert_chain` is not DER-encoded X.509 or fails ASN.1 parsing (NPS-RFC-0002 §4.3) |
| `NIP-CERT-EKU-MISSING` | `NPS-CLIENT-BAD-FRAME` | 400 | Required NPS EKU (`agent-identity` or `node-identity`) is absent or non-critical on the leaf cert (NPS-RFC-0002 §4.1/§4.3) |
| `NIP-CERT-SUBJECT-NID-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | 400 | X.509 leaf cert subject CN / SAN URI does not match `IdentFrame.nid` (NPS-RFC-0002 §4.3) |
| `NIP-CERT-NODE-ROLES-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | 400 | `IdentFrame.node_roles` does not match the `id-nps-node-roles` X.509 extension; Phase 3 enforcement (NIP v0.10) |
| `NIP-OCSP-STAPLE-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | 401 | `IdentFrame.ocsp_staple` `nextUpdate` has elapsed — staple is stale; Agent must refresh and resend (NIP v0.9 §5.1.4) |
| `NIP-CERT-PARENT-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | 401 | A session NID's parent / group NID is revoked or expired (chain check, NPS-3 §7 step 3a — NPS-CR-0003) |

### CA Operations

| Code | NPS Status Code | HTTP Equivalent | Description |
|------|-----------------|-----------------|-------------|
| `NIP-CA-NID-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | 404 | NID does not exist in the CA database |
| `NIP-CA-NID-ALREADY-EXISTS` | `NPS-CLIENT-CONFLICT` | 409 | NID already exists (duplicate registration) |
| `NIP-CA-SERIAL-DUPLICATE` | `NPS-CLIENT-CONFLICT` | 409 | Certificate serial number already in use |
| `NIP-CA-RENEWAL-TOO-EARLY` | `NPS-CLIENT-BAD-PARAM` | 400 | More than 7 days until expiry; renewal window not yet open |
| `NIP-CA-SCOPE-EXPANSION-DENIED` | `NPS-AUTH-FORBIDDEN` | 403 | Requested scope exceeds the parent scope (delegation-chain violation) |
| `NIP-ACME-CHALLENGE-FAILED` | `NPS-CLIENT-BAD-FRAME` | 400 | ACME `agent-01` challenge validation failed (token mismatch, signature failure, or replay — NPS-RFC-0002 §4.4) |
| `NIP-CA-GROUP-REVOKED` | `NPS-AUTH-FORBIDDEN` | 403 | Cannot issue a session under a group NID that has been revoked (NPS-3 §5.1.3 — NPS-CR-0003) |
| `NIP-CA-PARENT-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | 404 | The `parent_nid` / group NID referenced by a session-issue request does not exist (NPS-3 §5.1.3 — NPS-CR-0003) |
| `NIP-CA-PARENT-NOT-GROUP` | `NPS-CLIENT-BAD-PARAM` | 400 | The referenced parent NID exists but is not registered as `lineage.role = "group"` (NPS-3 §5.1.3 — NPS-CR-0003) |
| `NIP-CA-SESSION-VALIDITY-INVALID` | `NPS-CLIENT-BAD-PARAM` | 400 | Requested session validity below 60 s or above the CA's configured maximum (NPS-3 §5.1.3 — NPS-CR-0003) |
| `NIP-CA-JWS-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | 401 | Group-JWS authorisation on a session-issue request fails signature, header, or shape validation (NPS-3 §5.1.3 — NPS-CR-0003) |
| `NIP-CA-JWS-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | 401 | Group-JWS `iat` outside the CA's clock-skew window (default ±5 min) (NPS-3 §5.1.3 — NPS-CR-0003) |

### OCSP and Trust

| Code | NPS Status Code | HTTP Equivalent | Description |
|------|-----------------|-----------------|-------------|
| `NIP-OCSP-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | 503 | OCSP service temporarily unavailable |
| `NIP-TRUST-FRAME-INVALID` | `NPS-CLIENT-BAD-FRAME` | 400 | TrustFrame signature or format is invalid (NPS-3 §5.2) |
| `NIP-TRUST-FRAME-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | 401 | TrustFrame `expires_at` is in the past (NPS-3 §5.2) |
| `NIP-TRUST-FRAME-GRANTOR-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | 401 | TrustFrame `grantor_nid`'s own CA certificate is revoked or expired (NPS-3 §5.2) |
| `NIP-TRUST-FRAME-SCOPE-EXCEEDS-GRANTOR` | `NPS-AUTH-FORBIDDEN` | 403 | TrustFrame `trust_scope` contains a capability the grantor itself does not hold (no-scope-expansion principle — NPS-3 §5.2) |
| `NIP-TRUST-FRAME-NODES-PATTERN-INVALID` | `NPS-CLIENT-BAD-FRAME` | 400 | TrustFrame `nodes` entry is not a valid `nwp://` URL pattern (e.g. malformed wildcard — NPS-3 §5.2) |
| `NIP-REVOKE-FRAME-INVALID` | `NPS-CLIENT-BAD-FRAME` | 400 | RevokeFrame is malformed (missing required field, signature verification fails, or canonical form is invalid — NPS-3 §5.3) |
| `NIP-REVOKE-FRAME-UNAUTHORIZED-ISSUER` | `NPS-AUTH-FORBIDDEN` | 403 | RevokeFrame `signer_nid` is not authorised to revoke `target_nid` (NPS-3 §5.3) |
| `NIP-REVOKE-FRAME-SERIAL-MISMATCH` | `NPS-CLIENT-BAD-PARAM` | 400 | RevokeFrame `serial` is present but does not match any currently-issued cert for `target_nid` (NPS-3 §5.3) |
| `NIP-REVOKE-FRAME-REASON-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | 400 | RevokeFrame `reason` carries a value outside the defined enum; receivers treat it as `key_compromise` (most restrictive — NPS-3 §5.3) |

### Assurance and Reputation

| Code | NPS Status Code | HTTP Equivalent | Description |
|------|-----------------|-----------------|-------------|
| `NIP-ASSURANCE-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | 400 | `IdentFrame.assurance_level` does not match the cert extension `id-nid-assurance-level` (downgrade-attack defence — NPS-RFC-0003, NPS-3 §5.1.1) |
| `NIP-ASSURANCE-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | 400 | `assurance_level` is outside the defined enum (`anonymous` / `attested` / `verified`) |
| `NIP-REPUTATION-ENTRY-INVALID` | `NPS-CLIENT-BAD-FRAME` | 400 | Reputation log entry signature fails verification or canonical (RFC 8785 JCS) form is malformed (NPS-RFC-0004) |
| `NIP-REPUTATION-LOG-UNREACHABLE` | `NPS-DOWNSTREAM-UNAVAILABLE` | 502/503 | A log operator referenced by the node's `reputation_policy` cannot be reached during admission evaluation (NPS-RFC-0004) |
| `NIP-REPUTATION-GOSSIP-FORK` | `NPS-SERVER-INTERNAL` | 500 | Cross-peer STH consistency check failed; possible Merkle tree fork detected (NPS-RFC-0004 §4.5) |
| `NIP-REPUTATION-GOSSIP-SIG-INVALID` | `NPS-CLIENT-BAD-FRAME` | 400 | Peer STH signature verification failed during gossip exchange (NPS-RFC-0004 §4.5) |

---

## NDP Error Codes

Neural Discovery Protocol — address resolution, announcement, and graph synchronization.

| Code | NPS Status Code | HTTP Equivalent | Description |
|------|-----------------|-----------------|-------------|
| `NDP-RESOLVE-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | 404 | `nwp://` address cannot be resolved to a physical endpoint |
| `NDP-RESOLVE-AMBIGUOUS` | `NPS-CLIENT-CONFLICT` | 409 | Resolution result is conflicting (multiple inconsistent registrations) |
| `NDP-RESOLVE-TIMEOUT` | `NPS-SERVER-TIMEOUT` | 408/504 | Resolution request timed out |
| `NDP-RESOLVE-STALE` | `NPS-CLIENT-NOT-FOUND` | 404 | Resolved entry's freshness deadline `(last_seen ?? timestamp) + ttl` is in the past; the registration is stale and MUST NOT be served (NDP v0.9 §3.2.1) |
| `NDP-ANNOUNCE-SIGNATURE-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | 401 | AnnounceFrame signature verification failed |
| `NDP-ANNOUNCE-NID-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | 400 | NID in AnnounceFrame does not match the signing certificate |
| `NDP-ANNOUNCE-STALE` | `NPS-CLIENT-NOT-FOUND` | 404 | AnnounceFrame heartbeat has expired (3× `heartbeat_interval_ms` elapsed with no re-announce) (NDP v0.9) |
| `NDP-ANNOUNCE-ROLE-REMOVED` | `NPS-CLIENT-BAD-FRAME` | 400 | AnnounceFrame `node_roles` contains the removed legacy value `"gateway"` (NPS-CR-0001); use `"anchor"` or `"bridge"`. Response SHOULD include a `hint` pointing to NPS-CR-0001. |
| `NDP-ANNOUNCE-ROLE-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | 400 | AnnounceFrame `node_roles` contains an unrecognized value that is not a known-removed legacy value |
| `NDP-ANNOUNCE-CONFLICT` | `NPS-CLIENT-CONFLICT` | 409 | Two AnnounceFrames share the same `nid` and `graph_seq` but differ in covered content (registry-poisoning attempt; see NPS-4 §7.4) |
| `NDP-GRAPH-SEQ-ROLLBACK` | `NPS-CLIENT-BAD-FRAME` | 400 | AnnounceFrame `graph_seq` is less than or equal to the highest value previously accepted for that NID (rollback attempt; see NPS-4 §7.5) |
| `NDP-GRAPH-SEQ-GAP` | `NPS-STREAM-SEQ-GAP` | 422 | GraphFrame sequence numbers are not contiguous |
| `NDP-GRAPH-INVALID` | `NPS-CLIENT-BAD-FRAME` | 400 | GraphFrame edge references a NID not in the nodes list, or a self-edge is detected (NDP v0.8 §5) |
| `NDP-GRAPH-TOO-LARGE` | `NPS-LIMIT-PAYLOAD` | 413 | GraphFrame `nodes` > 256 or `edges` > 1024 (NDP v0.8 §5) |
| `NDP-FEDERATION-LOOP` | `NPS-CLIENT-CONFLICT` | 409 | AnnounceFrame forwarding loop: the registry's own NID already appears in `ndp-forwarded-by`, or the max 3-hop limit was exceeded (NPS-4 §9) |
| `NDP-ISSUER-NOT-ALLOWED` | `NPS-AUTH-FORBIDDEN` | 403 | AnnounceFrame issuer (signing CA) is not in the active registry profile's issuer allowlist (see NPS-4 §7.3) |
| `NDP-CA-ATTEST-REQUIRED` | `NPS-AUTH-UNAUTHENTICATED` | 401 | Active registry profile requires a CA-attested NID and the AnnounceFrame's certificate chain does not anchor in the configured trust roots (see NPS-4 §7.3) |
| `NDP-REGISTRY-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | 503 | NDP Registry temporarily unavailable |

---

## NOP Error Codes

Neural Orchestration Protocol — DAG task dispatch, delegation, synchronization, and streaming.

### Task Lifecycle

| Code | NPS Status Code | HTTP Equivalent | Description |
|------|-----------------|-----------------|-------------|
| `NOP-TASK-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | 404 | `task_id` does not exist |
| `NOP-TASK-TIMEOUT` | `NPS-SERVER-TIMEOUT` | 408/504 | Overall task execution timed out |
| `NOP-TASK-DAG-INVALID` | `NPS-CLIENT-BAD-FRAME` | 400 | DAG structure is invalid (missing entry/exit node, field error, etc.) |
| `NOP-TASK-DAG-CYCLE` | `NPS-CLIENT-BAD-FRAME` | 400 | DAG contains a cycle |
| `NOP-TASK-DAG-TOO-LARGE` | `NPS-CLIENT-BAD-FRAME` | 400 | DAG node count exceeds the limit (default: 32) |
| `NOP-TASK-ALREADY-COMPLETED` | `NPS-CLIENT-CONFLICT` | 409 | Task has completed; cannot be resubmitted |
| `NOP-TASK-CANCELLED` | `NPS-CLIENT-CONFLICT` | 409 | Task has been cancelled |

### Delegation

| Code | NPS Status Code | HTTP Equivalent | Description |
|------|-----------------|-----------------|-------------|
| `NOP-DELEGATE-SCOPE-VIOLATION` | `NPS-AUTH-FORBIDDEN` | 403 | `delegated_scope` exceeds the parent agent's scope |
| `NOP-DELEGATE-REJECTED` | `NPS-CLIENT-UNPROCESSABLE` | 422 | Target agent declined the delegation (insufficient capability or overload); response includes `retry_after_ms` |
| `NOP-DELEGATE-CHAIN-TOO-DEEP` | `NPS-CLIENT-BAD-PARAM` | 400 | Delegation chain depth exceeds the limit (default: 3 levels) |
| `NOP-DELEGATE-TIMEOUT` | `NPS-SERVER-TIMEOUT` | 408/504 | Subtask did not complete before `deadline_at` |

### Synchronization and Streaming

| Code | NPS Status Code | HTTP Equivalent | Description |
|------|-----------------|-----------------|-------------|
| `NOP-SYNC-TIMEOUT` | `NPS-SERVER-TIMEOUT` | 408/504 | SyncFrame timed out waiting for dependent tasks |
| `NOP-SYNC-DEPENDENCY-FAILED` | `NPS-CLIENT-UNPROCESSABLE` | 422 | Dependency subtask has failed and failure count exceeds the K-of-N tolerance |
| `NOP-STREAM-SEQ-GAP` | `NPS-STREAM-SEQ-GAP` | 422 | AlignStream sequence numbers are not contiguous |
| `NOP-STREAM-NID-MISMATCH` | `NPS-AUTH-UNAUTHENTICATED` | 401 | AlignStream `sender_nid` does not match the connection identity |
| `NOP-STREAM-NAK-UNRESOLVABLE` | `NPS-STREAM-SEQ-GAP` | 422 | NAK retransmission requested for a frame no longer available in the sender's buffer (frame has been evicted) (NOP v0.7) |
| `NOP-CALLBACK-HMAC-MISSING` | `NPS-AUTH-UNAUTHENTICATED` | 401 | Callback recipient rejected delivery because the `X-NPS-Signature` header was absent; `callback_secret` was set but the signature was not computed (NOP v0.6) |

### Resources and Conditions

| Code | NPS Status Code | HTTP Equivalent | Description |
|------|-----------------|-----------------|-------------|
| `NOP-RESOURCE-INSUFFICIENT` | `NPS-SERVER-UNAVAILABLE` | 503 | Preflight found one or more Worker Agents lack sufficient resources (CGN or capabilities) |
| `NOP-CONDITION-EVAL-ERROR` | `NPS-CLIENT-BAD-PARAM` | 400 | DAG node `condition` expression failed to evaluate (syntax error or missing referenced field) |
| `NOP-INPUT-MAPPING-ERROR` | `NPS-CLIENT-UNPROCESSABLE` | 422 | `input_mapping` JSONPath could not be resolved or target field is missing |

### Saga Compensation

| Code | NPS Status Code | HTTP Equivalent | Description |
|------|-----------------|-----------------|-------------|
| `NOP-COMPENSATION-FAILED` | `NPS-CLIENT-UNPROCESSABLE` | 422 | Terminal — node `compensate_action` returned an error during saga rollback (NOP v0.6) |
| `NOP-COMPENSATION-NOT-SUPPORTED` | `NPS-CLIENT-UNPROCESSABLE` | 422 | Terminal — a predecessor that must be compensated has no `compensate_action` (and `compensation_policy="strict"`) (NOP v0.6) |

### L3 Runtime Lease (NPS-CR-0007)

| Code | NPS Status Code | HTTP Equivalent | Description |
|------|-----------------|-----------------|-------------|
| `NOP-CLAIM-CONFLICT` | `NPS-CLIENT-CONFLICT` | 409 | TaskFrame already leased by a live `nps-runner` lease (NPS-CR-0007 §4.2) |
| `NOP-SPAWN-SPEC-INVALID` | `NPS-CLIENT-BAD-PARAM` | 400 | `spawn_spec_ref` could not be resolved or failed SpawnSpec schema validation (NPS-CR-0007 §5) |
| `NOP-RUNTIME-IDLE-TIMEOUT` | `NPS-SERVER-TIMEOUT` | 408/504 | L3 worker exceeded its idle timeout before completing the node (NPS-CR-0007 §6) |
| `NOP-RUNTIME-MAX-RUNTIME` | `NPS-SERVER-TIMEOUT` | 408/504 | L3 worker exceeded its max runtime before completing the node (NPS-CR-0007 §6) |
| `NOP-TASK-RESULT-EXPIRED` | `NPS-CLIENT-NOT-FOUND` | 404 | Task result requested after `result_ttl_seconds` elapsed; result no longer retained (NOP v0.7) |

---

## Alpha.17-alpha.18 Registry Additions

These current v1.9 registry entries supplement the protocol-grouped tables above.

| Code | NPS Status Code | Description |
|------|-----------------|-------------|
| `NWP-LLM-CONTEXT-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | Context or retained owner-scoped outcome is unknown. |
| `NWP-LLM-CONTEXT-EXPIRED` | `NPS-CLIENT-GONE` | Context has an idle-expiry tombstone. |
| `NWP-LLM-CONTEXT-VERSION-CONFLICT` | `NPS-CLIENT-CONFLICT` | CAS base version is stale or another mutation owns the reservation. |
| `NWP-LLM-CONTEXT-BINDING-MISMATCH` | `NPS-CLIENT-CONFLICT` | Model, system, tools, or runtime binding differs. |
| `NWP-LLM-CONTEXT-FORBIDDEN` | `NPS-AUTH-FORBIDDEN` | Caller is not the owner or lacks capability/scope. |
| `NWP-LLM-CONTEXT-LIMIT-EXCEEDED` | `NPS-LIMIT-RESOURCE` | Per-principal live-context limit reached. |
| `NWP-LLM-CONTEXT-OPERATION-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | Requested context operation was not advertised or implemented. |
| `NWP-HTTP-BODY-TOO-LARGE` | `NPS-LIMIT-PAYLOAD` | Portable Node HTTP body exceeds its configured limit. |
| `NWP-ANCHOR-NOT-LEADER` | `NPS-CLIENT-CONFLICT` | Topology write reached a standby/degraded Anchor. |
| `NWP-ANCHOR-EPOCH-FENCED` | `NPS-CLIENT-CONFLICT` | A superseded Anchor leader is fenced by a higher epoch. |
| `NWP-BRIDGE-DIRECTION-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | Protocol/direction pair was not declared. |
| `NWP-BRIDGE-TARGET-INVALID` | `NPS-CLIENT-UNPROCESSABLE` | Outbound bridge target is absent or invalid. |
| `NWP-BRIDGE-PROTOCOL-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | Outbound dispatcher is unavailable for the protocol. |
| `NWP-BRIDGE-ENDPOINT-INVALID` | `NPS-CLIENT-UNPROCESSABLE` | Endpoint URL or SSRF policy validation failed. |
| `NWP-BRIDGE-UPSTREAM-FAILED` | `NPS-DOWNSTREAM-UNAVAILABLE` | External transport or translation failed. |
| `NWP-BRIDGE-SERVER-TOOL-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | Inbound bridge action/tool is not exposed. |
| `NWP-BRIDGE-SERVER-DISPATCHER-MISSING` | `NPS-SERVER-INTERNAL` | Inbound bridge backend is not configured. |
| `NWP-BRIDGE-SERVER-DISPATCH-FAILED` | `NPS-SERVER-INTERNAL` | Inbound dispatch failed unexpectedly. |
| `NIP-CERT-CAPABILITIES-EXCEEDED` | `NPS-AUTH-FORBIDDEN` | Self-declared capability exceeds the CA-attested extension. |
| `NDP-ANNOUNCE-PROFILE-VIOLATION` | `NPS-AUTH-FORBIDDEN` | Announcement violates the active registry security profile. |
| `NDP-CLUSTER-SPLIT` | `NPS-CLIENT-CONFLICT` | Two live Anchors claim the same cluster epoch. |
| `NOP-CALLBACK-INVALID` | `NPS-CLIENT-BAD-PARAM` | Callback URL failed validation. |
| `NOP-CALLBACK-HMAC-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | Callback HMAC is malformed or does not match the raw body. |

## Disambiguation: NWP-ACTION-NOT-FOUND vs NWP-RESERVED-TYPE-UNSUPPORTED

These two codes cover different failure modes within `QueryFrame`, `ActionFrame`, and `SubscribeFrame`, and are easy to confuse.

**`NWP-ACTION-NOT-FOUND`** applies when:

- The caller names a specific action via `action_id`
- The `action_id` is a known operand type (looked up in the node's action registry)
- But no action with that identifier is registered at this endpoint

Use this code when the error is "I know what an action is, but this particular one does not exist here." Maps to `NPS-CLIENT-NOT-FOUND` (HTTP 404).

**`NWP-RESERVED-TYPE-UNSUPPORTED`** applies when:

- The `type` field of a `QueryFrame` or `SubscribeFrame` carries a reserved-namespace identifier (e.g. `topology.snapshot`, `topology.stream`)
- The node does not implement that entire reserved operation class

Use this code when the error is "I don't know how to handle this type of operation at all." The `type` field is the unknown operand, not `action_id`. Maps to `NPS-SERVER-UNSUPPORTED` (HTTP 501) because it represents a server capability gap, not a missing client-side resource.

**Quick rule**: "I don't have this action by name" → `NWP-ACTION-NOT-FOUND`. "I don't implement this class of operation" → `NWP-RESERVED-TYPE-UNSUPPORTED`.

---

## See Also

- [Reference: Status Codes](./Reference-Status-Codes.md) — the coarse two-level classification and HTTP mapping
- [Protocol NWP](./Protocol-NWP.md) — NWP action and query semantics in full

---

*Last reviewed at suite version: v1.0.0-alpha.19 release*
