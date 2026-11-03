English | [中文版](./error-codes.cn.md)

# NPS Unified Error Code Namespace

**Version**: 1.12
**Date**: 2026-11-02

Error code format: `{PROTOCOL}-{CATEGORY}-{DETAIL}`

NPS uses a two-level error system:
1. **Protocol error codes** (this document): specific error identifiers prefixed with the owning protocol
2. **NPS status codes**: transport-layer status classification + HTTP mapping — see [status-codes.md](status-codes.md)

---

## NCP Error Codes

| Error Code | NPS Status Code | Description |
|------------|-----------------|-------------|
| `NCP-FEATURE-NEGOTIATION-INVALID` | `NPS-CLIENT-BAD-FRAME` | Invalid feature negotiation metadata or selection |
| `NCP-FEATURE-REQUIRED-UNSUPPORTED` | `NPS-CLIENT-CONFLICT` | Required native feature has no eligible common revision |
| `NCP-ANCHOR-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | Schema referenced by `anchor_ref` does not exist; agent must re-fetch the AnchorFrame |
| `NCP-ANCHOR-SCHEMA-INVALID` | `NPS-CLIENT-BAD-FRAME` | Schema in AnchorFrame is malformed |
| `NCP-ANCHOR-ID-MISMATCH` | `NPS-CLIENT-CONFLICT` | Different Schemas received for the same `anchor_id` (anchor-pollution defense) |
| `NCP-FRAME-UNKNOWN-TYPE` | `NPS-CLIENT-BAD-FRAME` | Unknown frame type code |
| `NCP-FRAME-PAYLOAD-TOO-LARGE` | `NPS-LIMIT-PAYLOAD` | Payload exceeds the negotiated `max_frame_payload` |
| `NCP-FRAME-FLAGS-INVALID` | `NPS-CLIENT-BAD-FRAME` | Reserved bits in the flags field are non-zero |
| `NCP-BINARY-VECTOR-MALFORMED` | `NPS-CLIENT-BAD-FRAME` | Tier-3 BinaryVector payload is malformed |
| `NCP-BINARY-VECTOR-DIM-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | BinaryVector marker dimension does not match the vector segment |
| `NCP-BINARY-VECTOR-INDEX-INVALID` | `NPS-CLIENT-BAD-FRAME` | BinaryVector marker references a missing vector segment |
| `NCP-BINARY-VECTOR-DTYPE-UNSUPPORTED` | `NPS-CLIENT-BAD-FRAME` | BinaryVector marker uses an unsupported dtype |
| `NCP-BINARY-VECTOR-TRUNCATED` | `NPS-CLIENT-BAD-FRAME` | BinaryVector vector segment is truncated |
| `NCP-STREAM-SEQ-GAP` | `NPS-STREAM-SEQ-GAP` | StreamFrame sequence numbers are not contiguous |
| `NCP-STREAM-NOT-FOUND` | `NPS-STREAM-NOT-FOUND` | The stream referenced by `stream_id` does not exist |
| `NCP-STREAM-LIMIT-EXCEEDED` | `NPS-STREAM-LIMIT` | Maximum concurrent streams per connection exceeded |
| `NCP-ENCODING-UNSUPPORTED` | `NPS-SERVER-ENCODING-UNSUPPORTED` | The requested encoding tier is not supported |
| `NCP-ANCHOR-STALE` | `NPS-CLIENT-CONFLICT` | `anchor_ref` exists but the schema has been updated; the response carries the latest AnchorFrame via `CapsFrame.inline_anchor` |
| `NCP-DIFF-FORMAT-UNSUPPORTED` | `NPS-CLIENT-BAD-FRAME` | DiffFrame uses a `patch_format` the receiver does not support (e.g., `binary_bitset`) |
| `NCP-VERSION-INCOMPATIBLE` | `NPS-PROTO-VERSION-INCOMPATIBLE` | Client `min_version` is higher than the server's maximum supported version (handshake failure) |
| `NCP-STREAM-WINDOW-OVERFLOW` | `NPS-STREAM-LIMIT` | Sender continues to emit StreamFrames after the application-layer flow-control window is exhausted |
| `NCP-ENC-NOT-NEGOTIATED` | `NPS-CLIENT-BAD-FRAME` | Received an ENC=1 frame but no E2E encryption algorithm was negotiated for the session (not declared in HelloFrame) |
| `NCP-ENC-AUTH-FAILED` | `NPS-CLIENT-BAD-FRAME` | E2E encryption auth-tag verification failed; the frame may have been tampered with |
| `NCP-PREAMBLE-INVALID` | `NPS-PROTO-PREAMBLE-INVALID` | Native-mode connection opened with bytes other than the constant preamble `b"NPS/1.0\n"`; server closes the connection silently without emitting an ErrorFrame (NPS-RFC-0001) |
| `NCP-NID-MISMATCH` | `NPS-AUTH-UNAUTHENTICATED` | Native-mode mTLS client-certificate NID does not match the session `IdentFrame` NID, or a resumed TLS session's certificate NID differs from the ticket-bound NID (NPS-RFC-0006 §6.3–§6.4) |
| `NCP-KEEPALIVE-TIMEOUT` | `NPS-SERVER-TIMEOUT` | No frame (including NopFrame) received within 3 × `ping_interval_ms`; connection will be closed (NCP v0.8 §7.6) |
| `NCP-EARLY-DATA-REJECTED` | `NPS-PROTO-VERSION-INCOMPATIBLE` | NPS application data appeared in QUIC/TLS 0-RTT early data and was rejected before handshake confirmation (NCP v0.12 §7.7) |

---

## NWP Error Codes

| Error Code | NPS Status Code | Description |
|------------|-----------------|-------------|
| `NWP-RESULT-IDENTITY-REQUIRED` | `NPS-AUTH-UNAUTHENTICATED` | A verified principal is required for result operations |
| `NWP-RESULT-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | Absent, unauthorized, expired, released or unavailable result; identical disclosure |
| `NWP-RESULT-SNAPSHOT-MISMATCH` | `NPS-CLIENT-CONFLICT` | Authorized snapshot/schema/cursor binding mismatch |
| `NWP-RESULT-CAPACITY` | `NPS-LIMIT-RESOURCE` | Bounded result-store capacity cannot preserve the requested result |
| `NWP-RESULT-POLICY-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | Required policy/verification service unavailable; fail closed |
| `NWP-RESULT-REPLAY-CONFLICT` | `NPS-CLIENT-CONFLICT` | Owner-bound key reused with different frozen request content |
| `NWP-CHALLENGE-INVALID` | `NPS-CLIENT-BAD-FRAME` | Invalid challenge input |
| `NWP-CHALLENGE-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | Unknown or unauthorized challenge handle |
| `NWP-CHALLENGE-BINDING-CONFLICT` | `NPS-CLIENT-CONFLICT` | Same operation key with a different digest |
| `NWP-CHALLENGE-NOT-APPROVED` | `NPS-CLIENT-CONFLICT` | Challenge is not approved for execution |
| `NWP-CHALLENGE-EXPIRED` | `NPS-CLIENT-CONFLICT` | Challenge expired before execution claim |
| `NWP-CHALLENGE-STATE-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | Durable challenge state unavailable |
| `NWP-FEATURE-REQUIREMENTS-INVALID` | `NPS-CLIENT-BAD-FRAME` | Invalid or repeated required-feature HTTP header |
| `NWP-FEATURE-REQUIRED-UNSUPPORTED` | `NPS-CLIENT-CONFLICT` | Required exact feature unavailable before dispatch |
| `NWP-AUTH-NID-SCOPE-VIOLATION` | `NPS-AUTH-FORBIDDEN` | Agent scope does not cover the target node path |
| `NWP-AUTH-NID-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | NID certificate has expired |
| `NWP-AUTH-NID-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | NID has been revoked |
| `NWP-AUTH-NID-UNTRUSTED-ISSUER` | `NPS-AUTH-UNAUTHENTICATED` | NID issuer is not in `trusted_issuers` |
| `NWP-AUTH-NID-CAPABILITY-MISSING` | `NPS-AUTH-FORBIDDEN` | Agent is missing a capability required by the node (e.g., `nwp:query`) |
| `NWP-AUTH-ASSURANCE-TOO-LOW` | `NPS-AUTH-FORBIDDEN` | Agent's assurance level is below the node's `min_assurance_level` (NPS-2 §4.1) or per-action override (§4.6); response SHOULD include a `hint` pointing to a CA enrolment URL (NPS-RFC-0003) |
| `NWP-AUTH-REPUTATION-BLOCKED` | `NPS-AUTH-FORBIDDEN` | **Deprecated** (RFC-0005): use `NWP-REPUTATION-REJECTED` / `NWP-REPUTATION-BANNED` instead. Retained as alias for one alpha cycle. |
| `NWP-REPUTATION-THROTTLED` | `NPS-CLIENT-RATE-LIMITED` | Request rate-limited by reputation policy (`throttle_on` rule matched). Response includes `Retry-After: 60` header (NPS-RFC-0005) |
| `NWP-REPUTATION-REJECTED` | `NPS-AUTH-FORBIDDEN` | Request rejected by reputation policy (`reject_on` rule matched). Response body includes `matched_incident` + `matched_severity` (NPS-RFC-0005) |
| `NWP-REPUTATION-BANNED` | `NPS-AUTH-FORBIDDEN` | Request rejected and NID temporarily banned (`ban_on` rule matched or active ban cache entry). Response SHOULD include `X-NWP-Ban-Expires` Unix timestamp (NPS-RFC-0005) |
| `NWP-QUERY-FILTER-INVALID` | `NPS-CLIENT-BAD-PARAM` | Filter syntax is invalid or nests deeper than 8 levels |
| `NWP-QUERY-FIELD-UNKNOWN` | `NPS-CLIENT-BAD-PARAM` | `fields` references an unknown field |
| `NWP-QUERY-CURSOR-INVALID` | `NPS-CLIENT-BAD-PARAM` | Cursor cannot be decoded or has expired |
| `NWP-QUERY-REGEX-UNSAFE` | `NPS-CLIENT-BAD-PARAM` | `$regex` pattern rejected (ReDoS risk, excessive length, or nested quantifiers) |
| `NWP-QUERY-VECTOR-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | Node does not support vector search |
| `NWP-QUERY-AGGREGATE-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | Node does not support aggregate queries (`capabilities.aggregate=false`) |
| `NWP-QUERY-AGGREGATE-INVALID` | `NPS-CLIENT-BAD-PARAM` | Aggregate structure is invalid (unknown `func`, duplicate alias, missing required fields, etc.) |
| `NWP-QUERY-STREAM-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | Node does not support streaming queries (`capabilities.stream_query=false`) |
| `NWP-ACTION-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | `action_id` is not registered on the node |
| `NWP-ACTION-PARAMS-INVALID` | `NPS-CLIENT-UNPROCESSABLE` | Action params fail schema validation |
| `NWP-ACTION-IDEMPOTENCY-CONFLICT` | `NPS-CLIENT-CONFLICT` | A request with the same `idempotency_key` is already in progress |
| `NWP-LLM-CONTEXT-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | Stateful LLM context is unknown/released, or owner-scoped idempotency lookup is outside its retention window (NWP §7.6) |
| `NWP-LLM-CONTEXT-EXPIRED` | `NPS-CLIENT-GONE` | Context has an idle-expiry tombstone; the caller must create or reset another context (NWP §7.6) |
| `NWP-LLM-CONTEXT-VERSION-CONFLICT` | `NPS-CLIENT-CONFLICT` | `base_version` is stale or another mutation owns the context reservation; hint SHOULD carry the current version (NWP §7.6) |
| `NWP-LLM-CONTEXT-BINDING-MISMATCH` | `NPS-CLIENT-CONFLICT` | Resolved model, system messages, tool definitions, or runtime compatibility revision differs from the committed binding (NWP §7.6) |
| `NWP-LLM-CONTEXT-FORBIDDEN` | `NPS-AUTH-FORBIDDEN` | Authenticated caller is not the context owner or lacks required scope/capability (NWP §7.6) |
| `NWP-LLM-CONTEXT-LIMIT-EXCEEDED` | `NPS-LIMIT-RESOURCE` | Per-principal live-context limit reached; hint SHOULD carry the advertised limit (NWP §7.6) |
| `NWP-LLM-CONTEXT-OPERATION-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | Node supports stateful context but did not advertise/implement the requested lifecycle operation (NWP §7.6) |
| `NWP-TASK-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | Asynchronous task referenced by `task_id` does not exist |
| `NWP-TASK-ALREADY-CANCELLED` | `NPS-CLIENT-CONFLICT` | Task has been cancelled; further operations are not permitted |
| `NWP-TASK-ALREADY-COMPLETED` | `NPS-CLIENT-CONFLICT` | Task has completed and cannot be cancelled |
| `NWP-TASK-ALREADY-FAILED` | `NPS-CLIENT-CONFLICT` | Task has failed and cannot be cancelled |
| `NWP-SUBSCRIBE-STREAM-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | `stream_id` referenced by `unsubscribe` does not exist |
| `NWP-SUBSCRIBE-LIMIT-EXCEEDED` | `NPS-LIMIT-EXCEEDED` | Exceeded the node's maximum concurrent subscriptions |
| `NWP-SUBSCRIBE-FILTER-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | Node does not support subscriptions with a filter |
| `NWP-SUBSCRIBE-INTERRUPTED` | `NPS-SERVER-UNAVAILABLE` | Subscription stream terminated because the underlying data source was interrupted |
| `NWP-SUBSCRIBE-SEQ-TOO-OLD` | `NPS-CLIENT-CONFLICT` | `resume_from_seq` is outside the node's buffer window (recommended: 10 min or 10,000 records); agent must re-query from scratch before re-subscribing |
| `NWP-SUBSCRIBE-LEASE-INVALID` | `NPS-CLIENT-BAD-PARAM` | Renewable-subscription operation or lease/policy bounds are invalid (NWP v0.22 §13.4) |
| `NWP-SUBSCRIBE-LEASE-EXPIRED` | `NPS-CLIENT-GONE` | Subscription lease reached its deadline and cannot be renewed or resurrected (NWP v0.22 §13.4) |
| `NWP-BUDGET-EXCEEDED` | `NPS-LIMIT-BUDGET` | Response would exceed the `X-NWP-Budget` limit |
| `NWP-CGN-LIMIT-EXCEEDED` | `NPS-CLIENT-REQUEST-TOO-LARGE` | Response would exceed the effective CGN budget (`min(cgn_limit, X-NWP-Budget)`); trimming was not possible. Response body SHOULD include `effective_budget` and `estimated_cgn`. (token-budget.md §7.4) |
| `NWP-DEPTH-EXCEEDED` | `NPS-CLIENT-BAD-PARAM` | `X-NWP-Depth` exceeds the node's permitted `max_depth` |
| `NWP-GRAPH-CYCLE` | `NPS-CLIENT-UNPROCESSABLE` | Node graph contains a cyclic reference |
| `NWP-NODE-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | Underlying data source is temporarily unavailable |
| `NWP-MANIFEST-VERSION-UNSUPPORTED` | `NPS-CLIENT-BAD-PARAM` | Agent's NPS version is lower than the node's `min_agent_version` |
| `NWP-MANIFEST-NODE-TYPE-REMOVED` | `NPS-CLIENT-BAD-FRAME` | NWM `node_type` contains the removed legacy value `"gateway"` (NPS-CR-0001); use `"anchor"` or `"bridge"`. Response SHOULD include a `hint` pointing to NPS-CR-0001. |
| `NWP-MANIFEST-NODE-TYPE-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | NWM `node_type` contains an unrecognized value (not a known-removed legacy value — use `NWP-MANIFEST-NODE-TYPE-REMOVED` for that case). |
| `NWP-RATE-LIMIT-EXCEEDED` | `NPS-LIMIT-RATE` | Rate limit exceeded; reset timestamp is in the `X-NWP-Rate-Reset` header |
| `NWP-RESERVED-TYPE-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | `QueryFrame` or `SubscribeFrame` `type` field contains an unrecognized reserved-type identifier; this node does not implement the requested reserved operation (NWP §12). Distinct from `NWP-ACTION-NOT-FOUND` — use this code when the `type` field is the unknown operand, not `action_id`. |
| `NWP-HTTP-ORIGIN-FORBIDDEN` | `NPS-AUTH-FORBIDDEN` | HTTP overlay origin policy rejected the caller before a NWP frame was admitted (NWP §9.5) |
| `NWP-HTTP-CONTENT-TYPE-UNSUPPORTED` | `NPS-CLIENT-BAD-FRAME` | HTTP overlay request `Content-Type` is not a supported NWP frame media type (NWP §9.5) |
| `NWP-HTTP-ACCEPT-UNSATISFIABLE` | `NPS-CLIENT-BAD-PARAM` | HTTP overlay request `Accept` cannot be satisfied by any supported response media type (NWP §9.5) |
| `NWP-HTTP-REQUEST-ID-MISMATCH` | `NPS-CLIENT-BAD-PARAM` | Response `X-NWP-Request-ID` does not echo the request ID (NWP §9.5) |
| `NWP-HTTP-FRAME-BODY-MALFORMED` | `NPS-CLIENT-BAD-FRAME` | HTTP body cannot be parsed as a supported NWP frame envelope (NWP §9.5) |
| `NWP-HTTP-BODY-TOO-LARGE` | `NPS-LIMIT-PAYLOAD` | HTTP request body exceeds the portable Node server's configured limit (NWP §9.5 / §16.5.1) |
| `NWP-CAPABILITY-ADVERTISED-UNIMPLEMENTED` | `NPS-SERVER-UNSUPPORTED` | NWM advertises a capability/profile that the node currently cannot serve; distinct from truthful unsupported-feature codes such as `NWP-QUERY-VECTOR-UNSUPPORTED` (NWP §9.5) |
| `NWP-TOPOLOGY-UNAUTHORIZED` | `NPS-AUTH-FORBIDDEN` | Caller lacks permission to read this Anchor's topology (NPS-2 §12); authorization policy is implementation-defined per §12.4 (NPS-CR-0002) |
| `NWP-ANCHOR-NOT-LEADER` | `NPS-CLIENT-CONFLICT` | Topology write sent to a standby or read-only-degraded Anchor; only the active cluster owner accepts writes (NPS-CR-0009) |
| `NWP-ANCHOR-EPOCH-FENCED` | `NPS-CLIENT-CONFLICT` | Inbound frame carries a higher `cluster_epoch` than the receiving Anchor; the receiver is a superseded leader and is fenced (NPS-CR-0009) |
| `NWP-TOPOLOGY-UNSUPPORTED-SCOPE` | `NPS-CLIENT-BAD-PARAM` | `topology.scope` value is not implemented by this Anchor Node (NPS-CR-0002) |
| `NWP-TOPOLOGY-DEPTH-UNSUPPORTED` | `NPS-CLIENT-BAD-PARAM` | Requested `topology.depth` exceeds this Anchor Node's configured maximum (NPS-CR-0002) |
| `NWP-TOPOLOGY-FILTER-UNSUPPORTED` | `NPS-CLIENT-BAD-PARAM` | `topology.filter` contains an unrecognized key or unsupported operator (NPS-CR-0002) |
| `NWP-BRIDGE-DIRECTION-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | The request targets a protocol/direction pair this Bridge Node did not declare — an inbound request for a protocol absent from `bridge_inbound_protocols`, or an outbound `bridge_target.protocol` absent from `bridge_protocols` (NPS-4 §3.1). The response SHOULD carry both declared arrays in `hint`. Distinct from `NWP-ACTION-PARAMS-INVALID`: the protocol/direction is well-formed but unserved, not malformed. (NPS-CR-0010) |
| `NWP-BRIDGE-TARGET-INVALID` | `NPS-CLIENT-UNPROCESSABLE` | **Outbound.** The invocation carries no valid `bridge_target` object, or it fails schema validation (missing `protocol` / `endpoint`). *Registered by NPS-CR-0010; shipped in the .NET Bridge since alpha.7 without ever being registered here.* |
| `NWP-BRIDGE-PROTOCOL-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | **Outbound.** `bridge_target.protocol` is well-formed but this Bridge Node has no registered dispatcher for it. Distinct from `NWP-BRIDGE-DIRECTION-UNSUPPORTED`, which concerns the *declared* protocol/direction sets rather than the dispatcher table. *Registered by NPS-CR-0010.* |
| `NWP-BRIDGE-ENDPOINT-INVALID` | `NPS-CLIENT-UNPROCESSABLE` | **Outbound.** `bridge_target.endpoint` is not a valid URL, or is disallowed by the Bridge's SSRF policy (NWP §15.2). *Registered by NPS-CR-0010.* |
| `NWP-BRIDGE-UPSTREAM-FAILED` | `NPS-DOWNSTREAM-UNAVAILABLE` | **Outbound.** The external call failed at the transport layer, timed out, or returned a response the Bridge could not translate back into NWP frames. Distinct from a *successful* translation of an upstream error status, which maps per NWP §16.3. *Registered by NPS-CR-0010.* |
| `NWP-BRIDGE-SERVER-TOOL-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | **Inbound.** The foreign client named a tool / action / resource this Bridge does not expose. *Registered by NPS-CR-0010.* |
| `NWP-BRIDGE-SERVER-DISPATCHER-MISSING` | `NPS-SERVER-INTERNAL` | **Inbound.** The Bridge was started without a configured backend for the NPS node it fronts — a deployment fault, not a client fault. *Registered by NPS-CR-0010; the .NET Bridge previously emitted the non-existent status `NPS-SERVER-NOT-IMPLEMENTED` here.* |
| `NWP-BRIDGE-SERVER-DISPATCH-FAILED` | `NPS-SERVER-INTERNAL` | **Inbound.** Dispatch to the fronted NPS node failed unexpectedly. *Registered by NPS-CR-0010.* |

---

## NIP Error Codes

| Error Code | NPS Status Code | Description |
|------------|-----------------|-------------|
| `NIP-CONTEXT-INVALID` | `NPS-CLIENT-BAD-FRAME` | Invalid projection format, bounds or lifetime |
| `NIP-CONTEXT-UNTRUSTED` | `NPS-AUTH-FORBIDDEN` | Untrusted issuer/key or invalid signature |
| `NIP-CONTEXT-BINDING-MISMATCH` | `NPS-AUTH-FORBIDDEN` | Projection request/subject/audience or policy mismatch |
| `NIP-CONTEXT-EXPIRED` | `NPS-AUTH-FORBIDDEN` | Projection validity window expired |
| `NIP-CONTEXT-REPLAY` | `NPS-CLIENT-CONFLICT` | Valid projection was already admitted |
| `NIP-CONTEXT-VERIFIER-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | Verification or durable replay infrastructure unavailable |
| `NIP-CERT-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | Certificate has expired (`expires_at < now`) |
| `NIP-CERT-NODE-ROLES-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | `IdentFrame.node_roles` does not match the `id-nps-node-roles` X.509 extension; Phase 3 enforcement (NIP v0.10) |
| `NIP-CERT-CAPABILITIES-EXCEEDED` | `NPS-AUTH-FORBIDDEN` | `IdentFrame.capabilities` claims a capability absent from the CA-attested `id-nps-capabilities` extension; Phase-3 enforcement (NIP v0.12) |
| `NIP-OCSP-STAPLE-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | `IdentFrame.ocsp_staple` `nextUpdate` has elapsed — staple is stale; Agent must refresh and resend (NIP v0.9 §5.1.4) |
| `NIP-CERT-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | Certificate has been revoked (found in CRL or OCSP) |
| `NIP-CERT-SIGNATURE-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | Certificate signature verification failed |
| `NIP-CERT-UNTRUSTED-ISSUER` | `NPS-AUTH-UNAUTHENTICATED` | Issuer is not in `trusted_issuers` |
| `NIP-CERT-CAPABILITY-MISSING` | `NPS-AUTH-FORBIDDEN` | Certificate is missing a capability required by the node |
| `NIP-CERT-SCOPE-VIOLATION` | `NPS-AUTH-FORBIDDEN` | Certificate scope does not cover the target path or operation |
| `NIP-CA-NID-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | NID does not exist in the CA database |
| `NIP-CA-NID-ALREADY-EXISTS` | `NPS-CLIENT-CONFLICT` | NID already exists (duplicate registration) |
| `NIP-CA-SERIAL-DUPLICATE` | `NPS-CLIENT-CONFLICT` | Certificate serial number already in use |
| `NIP-CA-RENEWAL-TOO-EARLY` | `NPS-CLIENT-BAD-PARAM` | More than 7 days until expiry; renewal window not yet open |
| `NIP-CA-SCOPE-EXPANSION-DENIED` | `NPS-AUTH-FORBIDDEN` | Requested scope exceeds the parent scope (delegation-chain violation) |
| `NIP-OCSP-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | OCSP service temporarily unavailable |
| `NIP-OCSP-UNKNOWN` | `NPS-AUTH-UNAUTHENTICATED` | A valid OCSP response cannot establish status for the requested issuer/serial and fails closed (NIP v0.15 §7.7) |
| `NIP-REVOCATION-STATE-STALE` | `NPS-SERVER-UNAVAILABLE` | No configured revocation source produced a current result; stale state cannot satisfy required mode (NIP v0.15 §7.7) |
| `NIP-TRUST-FRAME-INVALID` | `NPS-CLIENT-BAD-FRAME` | TrustFrame signature or format is invalid — see NPS-3 §5.2 |
| `NIP-TRUST-FRAME-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | TrustFrame `expires_at` is in the past — see NPS-3 §5.2 |
| `NIP-TRUST-FRAME-GRANTOR-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | TrustFrame `grantor_nid`'s own CA certificate is revoked or expired — see NPS-3 §5.2 |
| `NIP-TRUST-FRAME-SCOPE-EXCEEDS-GRANTOR` | `NPS-AUTH-FORBIDDEN` | TrustFrame `trust_scope` contains a capability that the grantor itself does not hold (no-scope-expansion principle) — see NPS-3 §5.2 |
| `NIP-TRUST-FRAME-NODES-PATTERN-INVALID` | `NPS-CLIENT-BAD-FRAME` | TrustFrame `nodes` entry is not a valid `nwp://` URL pattern (e.g. malformed wildcard) — see NPS-3 §5.2 |
| `NIP-REVOKE-FRAME-INVALID` | `NPS-CLIENT-BAD-FRAME` | RevokeFrame is malformed (missing required field, signature verification fails, or canonical form is invalid) — see NPS-3 §5.3 |
| `NIP-REVOKE-FRAME-UNAUTHORIZED-ISSUER` | `NPS-AUTH-FORBIDDEN` | RevokeFrame `signer_nid` is not authorised to revoke `target_nid` — see NPS-3 §5.3 |
| `NIP-REVOKE-FRAME-SERIAL-MISMATCH` | `NPS-CLIENT-BAD-PARAM` | RevokeFrame `serial` is present but does not match any currently-issued cert for `target_nid` — see NPS-3 §5.3 |
| `NIP-REVOKE-FRAME-REASON-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | RevokeFrame `reason` carries a value outside the defined enum; receivers treat it as `key_compromise` (most restrictive) — see NPS-3 §5.3 |
| `NIP-ASSURANCE-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | `IdentFrame.assurance_level` does not match the cert extension `id-nid-assurance-level` (downgrade-attack defence) — see NPS-3 §5.1.1 (NPS-RFC-0003) |
| `NIP-ASSURANCE-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | `assurance_level` carries a value outside the defined enum (`anonymous` / `attested` / `verified`) — see NPS-3 §5.1.1 (NPS-RFC-0003) |
| `NIP-REPUTATION-ENTRY-INVALID` | `NPS-CLIENT-BAD-FRAME` | Reputation log entry signature fails verification or canonical (RFC 8785 JCS) form is malformed — see NPS-3 §5.1.2 (NPS-RFC-0004) |
| `NIP-REPUTATION-LOG-UNREACHABLE` | `NPS-DOWNSTREAM-UNAVAILABLE` | A log operator referenced by a Node's `reputation_policy` cannot be reached during admission evaluation — see NPS-3 §5.1.2 (NPS-RFC-0004) |
| `NIP-REPUTATION-GOSSIP-FORK` | `NPS-SERVER-INTERNAL` | Cross-peer STH consistency check failed; possible Merkle tree fork detected — see NPS-RFC-0004 §4.5 |
| `NIP-REPUTATION-GOSSIP-SIG-INVALID` | `NPS-CLIENT-BAD-FRAME` | Peer STH signature verification failed during gossip exchange — see NPS-RFC-0004 §4.5 |
| `NIP-CERT-FORMAT-INVALID` | `NPS-CLIENT-BAD-FRAME` | `IdentFrame.cert_chain` is not DER-encoded X.509 or fails ASN.1 parsing — see NPS-RFC-0002 §4.3 |
| `NIP-CERT-EKU-MISSING` | `NPS-CLIENT-BAD-FRAME` | Required NPS EKU (`agent-identity` or `node-identity`) absent or non-critical on the leaf cert — see NPS-RFC-0002 §4.1 / §4.3 |
| `NIP-CERT-SUBJECT-NID-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | X.509 leaf cert subject CN / SAN URI does not match the `IdentFrame.nid` field — see NPS-RFC-0002 §4.3 |
| `NIP-ACME-CHALLENGE-FAILED` | `NPS-CLIENT-BAD-FRAME` | ACME `agent-01` challenge validation failed (token mismatch, signature verification failure, or replay) — see NPS-RFC-0002 §4.4 |
| `NIP-CA-GROUP-REVOKED` | `NPS-AUTH-FORBIDDEN` | Cannot issue a session under a group NID that has been revoked — see NPS-3 §5.1.3 (NPS-CR-0003) |
| `NIP-CA-PARENT-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | The `parent_nid` / group NID referenced by a session-issue request does not exist — see NPS-3 §5.1.3 (NPS-CR-0003) |
| `NIP-CA-PARENT-NOT-GROUP` | `NPS-CLIENT-BAD-PARAM` | The referenced parent NID exists but is not registered as `lineage.role = "group"` — see NPS-3 §5.1.3 (NPS-CR-0003) |
| `NIP-CA-SESSION-VALIDITY-INVALID` | `NPS-CLIENT-BAD-PARAM` | Requested session validity below 60 seconds or above the CA's configured maximum — see NPS-3 §5.1.3 (NPS-CR-0003) |
| `NIP-CA-JWS-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | Group-JWS authorisation on a session-issue request fails signature, header, or shape validation — see NPS-3 §5.1.3 (NPS-CR-0003) |
| `NIP-CA-JWS-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | Group-JWS `iat` outside the CA's clock-skew window (default ±5 minutes) — see NPS-3 §5.1.3 (NPS-CR-0003) |
| `NIP-CERT-PARENT-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | A session NID's parent / group NID is revoked or expired (chain check, NPS-3 §7 step 3a) — see NPS-3 §5.1.3 (NPS-CR-0003) |

---

## NDP Error Codes

| Error Code | NPS Status Code | Description |
|------------|-----------------|-------------|
| `NDP-RESOLVE-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | `nwp://` address cannot be resolved to a physical endpoint |
| `NDP-RESOLVE-AMBIGUOUS` | `NPS-CLIENT-CONFLICT` | Resolution result is conflicting (multiple inconsistent registrations) |
| `NDP-RESOLVE-TIMEOUT` | `NPS-SERVER-TIMEOUT` | Resolution request timed out |
| `NDP-RESOLVE-STALE` | `NPS-CLIENT-NOT-FOUND` | Resolved entry's freshness deadline `(last_seen ?? timestamp) + ttl` is in the past; the registration is stale and MUST NOT be served (NDP v0.9 §3.2.1) |
| `NDP-ANNOUNCE-STALE` | `NPS-CLIENT-NOT-FOUND` | AnnounceFrame heartbeat has expired (3× `heartbeat_interval_ms` elapsed with no re-announce) (NDP v0.9) |
| `NDP-ANNOUNCE-SIGNATURE-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | AnnounceFrame signature verification failed |
| `NDP-ANNOUNCE-NID-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | NID in AnnounceFrame does not match the signing certificate |
| `NDP-ANNOUNCE-ROLE-REMOVED` | `NPS-CLIENT-BAD-FRAME` | AnnounceFrame `node_roles` contains the removed legacy value `"gateway"` (NPS-CR-0001); use `"anchor"` or `"bridge"`. Response SHOULD include a `hint` pointing to NPS-CR-0001. |
| `NDP-ANNOUNCE-ROLE-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | AnnounceFrame `node_roles` contains an unrecognized value (not a known-removed legacy value — use `NDP-ANNOUNCE-ROLE-REMOVED` for that case). |
| `NDP-ANNOUNCE-CONFLICT` | `NPS-CLIENT-CONFLICT` | Two AnnounceFrames share the same `nid` and `graph_seq` but differ in covered content (registry-poisoning attempt; see NPS-4 §7.4) |
| `NDP-ANNOUNCE-PROFILE-VIOLATION` | `NPS-AUTH-FORBIDDEN` | AnnounceFrame violates an active registry security profile constraint not covered by a more specific NDP error (for example, address policy under an org/private or public/federated profile) |
| `NDP-GRAPH-SEQ-ROLLBACK` | `NPS-CLIENT-BAD-FRAME` | AnnounceFrame `graph_seq` is lower than the highest value previously accepted for that NID (rollback attempt; see NPS-4 §7.5) |
| `NDP-GRAPH-SEQ-GAP` | `NPS-STREAM-SEQ-GAP` | GraphFrame sequence numbers are not contiguous |
| `NDP-GRAPH-INVALID` | `NPS-CLIENT-BAD-FRAME` | GraphFrame edge references a NID not present in the `nodes` list, or contains a self-edge |
| `NDP-GRAPH-TOO-LARGE` | `NPS-LIMIT-PAYLOAD` | GraphFrame exceeds the topology snapshot limits (`nodes` > 256 or `edges` > 1024) |
| `NDP-FEDERATION-LOOP` | `NPS-CLIENT-CONFLICT` | Federation forwarding detected a loop via the `ndp-forwarded-by` hop list |
| `NDP-CLUSTER-SPLIT` | `NPS-CLIENT-CONFLICT` | Two live Anchor announcements advertise the same `cluster_epoch` for one `cluster_anchor` cluster (split-brain); the Registry refuses to resolve arbitrarily (NPS-CR-0009) |
| `NDP-ISSUER-NOT-ALLOWED` | `NPS-AUTH-FORBIDDEN` | AnnounceFrame issuer (signing CA) is not in the active registry profile's issuer allowlist (see NPS-4 §7.3) |
| `NDP-CA-ATTEST-REQUIRED` | `NPS-AUTH-UNAUTHENTICATED` | Active registry profile requires a CA-attested NID and the AnnounceFrame's certificate chain does not anchor in the configured trust roots (see NPS-4 §7.3) |
| `NDP-REGISTRY-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | NDP Registry temporarily unavailable |
| `NDP-STATE-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | A persistence-required Registry cannot durably read or commit recovery state (NDP v0.13 §7.8) |
| `NDP-STATE-CORRUPT` | `NPS-SERVER-INTERNAL` | Persisted Registry state fails schema, checksum, or monotonic-fence validation (NDP v0.13 §7.8) |

---

## NOP Error Codes

| Error Code | NPS Status Code | Description |
|------------|-----------------|-------------|
| `NOP-EXECUTION-INDETERMINATE` | `NPS-SERVER-INTERNAL` | Execution outcome uncertain; not automatically retryable |
| `NOP-TASK-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | `task_id` does not exist |
| `NOP-TASK-TIMEOUT` | `NPS-SERVER-TIMEOUT` | Overall task execution timed out |
| `NOP-TASK-DAG-INVALID` | `NPS-CLIENT-BAD-FRAME` | DAG structure is invalid (missing entry/exit node, field error, etc.) |
| `NOP-TASK-DAG-CYCLE` | `NPS-CLIENT-BAD-FRAME` | DAG contains a cycle |
| `NOP-TASK-DAG-TOO-LARGE` | `NPS-CLIENT-BAD-FRAME` | DAG node count exceeds the limit (default 32) |
| `NOP-TASK-ALREADY-COMPLETED` | `NPS-CLIENT-CONFLICT` | Task has completed; cannot be resubmitted |
| `NOP-TASK-CANCELLED` | `NPS-CLIENT-CONFLICT` | Task has been cancelled |
| `NOP-DELEGATE-SCOPE-VIOLATION` | `NPS-AUTH-FORBIDDEN` | `delegated_scope` exceeds the parent agent's scope |
| `NOP-DELEGATE-REJECTED` | `NPS-CLIENT-UNPROCESSABLE` | Target agent declined the delegation (insufficient capability or overload); response includes `retry_after_ms` |
| `NOP-DELEGATE-CHAIN-TOO-DEEP` | `NPS-CLIENT-BAD-PARAM` | Delegation chain depth exceeds the limit (default 3 levels) |
| `NOP-DELEGATE-TIMEOUT` | `NPS-SERVER-TIMEOUT` | Subtask did not complete before `deadline_at` |
| `NOP-SYNC-TIMEOUT` | `NPS-SERVER-TIMEOUT` | SyncFrame timed out waiting for dependent tasks |
| `NOP-SYNC-DEPENDENCY-FAILED` | `NPS-CLIENT-UNPROCESSABLE` | Dependency subtask has failed (and failure count exceeds the K-of-N tolerance) |
| `NOP-STREAM-SEQ-GAP` | `NPS-STREAM-SEQ-GAP` | AlignStream sequence numbers are not contiguous |
| `NOP-STREAM-NID-MISMATCH` | `NPS-AUTH-UNAUTHENTICATED` | AlignStream `sender_nid` does not match the connection identity |
| `NOP-RESOURCE-INSUFFICIENT` | `NPS-SERVER-UNAVAILABLE` | Preflight found one or more Worker Agents lack sufficient resources (CGN or capabilities) |
| `NOP-CONDITION-EVAL-ERROR` | `NPS-CLIENT-BAD-PARAM` | DAG node `condition` expression failed to evaluate (syntax error or missing referenced field) |
| `NOP-INPUT-MAPPING-ERROR` | `NPS-CLIENT-UNPROCESSABLE` | `input_mapping` JSONPath could not be resolved or target field is missing |
| `NOP-COMPENSATION-FAILED` | `NPS-CLIENT-UNPROCESSABLE` | Terminal — node `compensate_action` returned an error during saga rollback |
| `NOP-COMPENSATION-NOT-SUPPORTED` | `NPS-CLIENT-UNPROCESSABLE` | Terminal — predecessor that must be compensated has no `compensate_action` (and `compensation_policy="strict"`) |
| `NOP-CLAIM-CONFLICT` | `NPS-CLIENT-CONFLICT` | TaskFrame already leased by a live `nps-runner` lease (NPS-CR-0007 §4.2) |
| `NOP-SPAWN-SPEC-INVALID` | `NPS-CLIENT-BAD-PARAM` | `spawn_spec_ref` could not be resolved or failed SpawnSpec schema validation (NPS-CR-0007 §5) |
| `NOP-RUNTIME-IDLE-TIMEOUT` | `NPS-SERVER-TIMEOUT` | L3 worker exceeded its idle timeout before completing the node (NPS-CR-0007 §6) |
| `NOP-RUNTIME-MAX-RUNTIME` | `NPS-SERVER-TIMEOUT` | L3 worker exceeded its max runtime before completing the node (NPS-CR-0007 §6) |
| `NOP-CALLBACK-HMAC-MISSING` | `NPS-AUTH-UNAUTHENTICATED` | Callback recipient rejected delivery because `X-NPS-Signature` header was absent; `callback_secret` was set but signature was not computed (NOP v0.6) |
| `NOP-CALLBACK-INVALID` | `NPS-CLIENT-BAD-PARAM` | Callback URL failed scheme, user-info, DNS, public-address, or redirect validation (NOP v0.9) |
| `NOP-CALLBACK-HMAC-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | Callback HMAC was malformed or did not match the exact raw body (NOP v0.9) |
| `NOP-TASK-RESULT-EXPIRED` | `NPS-CLIENT-NOT-FOUND` | Task result requested after `result_ttl_seconds` elapsed; result no longer retained (NOP v0.7) |
| `NOP-REPLAY-CONFLICT` | `NPS-CLIENT-CONFLICT` | The same `(caller_nid, task_id)` was reused with different immutable TaskFrame content (NOP v0.10 §3.5.6) |
| `NOP-REPLAY-LIMIT` | `NPS-LIMIT-RESOURCE` | Replay ledger is full and no expired tombstone or terminal record can be safely evicted (NOP v0.10 §3.5.6) |
| `NOP-AGGREGATION-INVALID` | `NPS-CLIENT-BAD-PARAM` | Aggregation input violates the selected strategy, such as a missing/non-finite `weighted_first_k` score (NOP v0.10 §3.5.6) |
| `NOP-STREAM-NAK-UNRESOLVABLE` | `NPS-STREAM-SEQ-GAP` | NAK retransmission requested for a frame no longer available in sender's buffer (frame has been evicted) (NOP v0.7) |

---

*Copyright: LabAcacia / INNO LOTUS PTY LTD · Apache 2.0*
