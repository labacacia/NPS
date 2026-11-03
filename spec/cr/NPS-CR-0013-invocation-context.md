English | [中文版](./NPS-CR-0013-invocation-context.cn.md)

# NPS-CR-0013: Authenticated Invocation Context

- Status: Draft
- Target: v1.0.0-alpha.20
- Date: 2026-09-09
- Author: Codex
- Discussion: [#125](https://github.com/labacacia/NPS-Dev/issues/125)
- Parent: [EPIC-007](https://github.com/innolotus/ChangeControl/issues/7)
- Trace: ADR-004 D20-02; A20-R005..R008

## 1. Summary

Normative follow-up in NIP §7.8 / NWP §9.7 specifies native request carriers,
per-request exact feature pairs, complete semantic-body binding, UTF-8 array ordering
and deterministic identifier/size limits. Those sections supersede underspecified
carrier/body details in this historical proposal. Implementation remains incomplete.

Define authority derived from verified admission and a short-lived signed projection between
authenticated gateway hops. Caller correlation is never authority. This Draft proposes new NWP/NIP
profile text and errors; existing IdentFrame encodings and released versions remain unchanged.

## 2. Motivation

BKE accepts a product access envelope with optional signing configuration; BKL has a local-owner
fallback. These are ingress qualification inputs, not proof of remote exploits. Header names and
matching tenant strings cannot establish who asserted authority. Products retain IAM decisions.

## 3. Proposed contract

### 3.1 In-memory context

Admission produces two separate typed objects:

- caller facts: request_id, traceparent, requested deadline, idempotency_key, bounded hints;
- admitted authority: subject NID, verified assurance, capabilities, deployment scope, issuer and
  transport/session provenance.

A caller deadline may shorten but never extend the server limit. An idempotency key is not identity.
Native authority comes from verified NIP session admission, never Hello.agent_id. Local-development
authority requires an explicit server deployment mode unavailable to remote caller selection.
Adapters cannot construct admitted authority by deserializing a request body.

### 3.2 Wire envelope and canonicalization

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
| issuer | admitted gateway NID, at most 512 UTF-8 bytes |
| key_id | configured key identifier, 1..128 ASCII bytes |
| audience | exact logical receiving service ID, 1..512 UTF-8 bytes |
| subject | original admitted subject NID, at most 512 UTF-8 bytes |
| assurance | anonymous / attested / verified; receiver must permit this assertion |
| capabilities | sorted unique strings, at most 64, each at most 128 UTF-8 bytes |
| scope | sorted unique strings of deployment-registered scope identifiers, at most 64, each 256 UTF-8 bytes |
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

### 3.3 Request digest

The digest is SHA-256 over UTF-8 `NPS-INVOKE-REQUEST/1`, one zero byte and JCS of this exact object:

```json
{"protocol":"nwp","operation":"example.echo","target":"service:echo","idempotency_key":null,"required_features":[{"id":"nps.invocation_context","revision":1}],"body":{"encoding":"json","value":{"text":"hello"}}}
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

### 3.4 Time, peer binding and replay

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

### 3.5 Error proposals

| Error | NPS status | HTTP |
|---|---|---|
| NIP-CONTEXT-INVALID | NPS-CLIENT-BAD-FRAME | 400 |
| NIP-CONTEXT-UNTRUSTED | NPS-AUTH-FORBIDDEN | 403 |
| NIP-CONTEXT-BINDING-MISMATCH | NPS-AUTH-FORBIDDEN | 403 |
| NIP-CONTEXT-EXPIRED | NPS-AUTH-FORBIDDEN | 403 |
| NIP-CONTEXT-REPLAY | NPS-CLIENT-CONFLICT | 409 |
| NIP-CONTEXT-VERIFIER-UNAVAILABLE | NPS-SERVER-UNAVAILABLE | 503 |

Return bounded diagnostics; malformed signature/unknown key is untrusted. Future-issued or invalid
lifetime timestamps are invalid. Errors are proposed assignments pending registry integration.

## 4. SDK changes

.NET/Python/TypeScript/Go/Java/Rust each need strict parsing, JCS request digest/signature handling,
immutable admitted context, configured issuer transport binding and an injected durable replay-store
interface with atomic semantics. Product IAM and transport authentication are injected policies,
not generic trust-all callbacks. Validate parsing before typed deserialization can discard fields.

## 5. Conformance scenarios

CTX-001: same logical JSON yields identical JCS/digest/signature bytes across six SDKs.
CTX-002: wrong signature, issuer key, peer issuer or audience rejects.
CTX-003: changed body/target/operation/features/idempotency key rejects.
CTX-004: missing key or replay store rejects before dispatch.
CTX-005: simultaneous duplicate projection across replicas admits once.
CTX-006: replay after process restart still rejects before retention expiry.
CTX-007: time boundary, excessive lifetime and future issue tests.
CTX-008: duplicate JSON/header, ambiguous base64url, unknown fields and limits reject.
CTX-009: public spoofed context is stripped; Hello.agent_id is not authority.
CTX-010: changed gateway hop requires re-projection; preserved logical subject is authorized.
CTX-011: receiver delegation ceiling cannot be expanded by signed scope.
CTX-012: fresh projection plus same operation key reaches durable deduplication on lost reply.
CTX-013: key revocation invalidates outstanding projections.
CTX-014: explicit local mode cannot be selected by a remote request.

## 6. Migration

BKE/BKL/Ivy/Passport/Root map product authority to this profile only after proving ingress
authentication and deployment mode. Existing SDK issuance wrappers remain; this adds no new NIP
certificate format. Do not enable feature advertisement until enforcement is installed end to end.

## 7. Out of scope

Product tenancy vocabulary, approval UI, distributed business transactions, general bearer tokens,
rewriting NIP certificate signatures or declaring source observations exploitable.

## 8. Acceptance

- [ ] EN/CN normative integration and errors/version updates (P20-105/107).
- [ ] CTX-001..014 frozen byte/security fixtures (P20-108).
- [ ] Six-SDK positive/negative parity and durable replay tests.
- [ ] Gateway ingress/replica/restart qualification and redacted logs.

## 9. Proposed changelog

Add request-bound authenticated invocation projections with strict canonical signatures, trusted
peer/issuer binding and durable replay admission.

## 10. Follow-up

Feature registry lifecycle promotion waits for shared vectors; product scope vocabularies remain
deployment-owned. This authoring task does not mark profile implementation or qualification complete.
