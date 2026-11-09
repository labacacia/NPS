# ADR: Qualified scope for result HTTP invocation identity

- Status: Accepted experimental implementation scope; broader qualification pending.
- Date: 2026-11-02.
- Trace: D20-03 #167, D20-02 #166; existing NIP §7.8 / NWP §9.7.

## Context and decision

The resource store and recipient policy require a verified principal. Raw AgentNid,
a copied handle and a CA-signed public identity document alone do not establish
authenticated request/session possession. Existing TypeScript code already separates
strict JSON/JCS bytes, Ed25519 projection verification, receiver policy and durable
single-host replay. Reuse those protocol rules when connecting the .NET reference.

Implement the existing projected HTTP leg: independently authenticated transport
constrains allowed gateway issuers; configured Ed25519 keys verify the domain-separated
NIP envelope; receiver policy constrains subject, assurance, capability, scope and
logical target. Bind the complete original ActionFrame body and exact feature pairs.
Validate operations before atomic replay admission. A successful admission belongs to
one host request object; execution revalidates that same request without consuming
the projection twice. A new request must use a fresh projection.

### Resource preflight before dispatch and replay

The frozen result invariants require zero handler calls for resource-policy and
snapshot denials. Checking only inside execution, although it withholds the reply,
does not satisfy that contract. Admission now performs a non-consuming store preflight
after identity verification. The host additionally invokes that preflight in NIP
`Authorize`, after cryptographic/channel/request validation and before durable replay
claim. The reference host records only verified local resource-denial classification
in private request state and preserves its profile error only for the resulting NIP
policy-denial code. Infrastructure/audit failures remain failures, never a masked 404.

Preflight checks known capacity, existing key conflicts, live resource policy,
expiry/release and snapshot/schema/cursor bindings. A one-row local probe checks an
unreturnable page before dispatch. It cannot capture a source, release a result,
reserve capacity, advance a cursor or consume replay. Execution still revalidates
and atomically checks actual capture size, so concurrent changes can reject after
admission. This adds local policy/page work; older runtime measurements remain
historical evidence for their recorded revision, not performance evidence for this code.

Keep unknown operations bounded: an optional Action Node provider hook supplies the
result profile's 422 after feature admission. Other providers retain their existing
404 by default. No new operation is registered. Moving all checks into the handler
was rejected because it violates the frozen zero-dispatch contract; reserving resources
in admission was rejected because denied requests must not consume authority/capacity.

The initial .NET reference operation schema accepts strings, booleans, null, objects,
arrays and decimal integer tokens within ±9007199254740991. Fractional/exponent tokens,
unsafe integers and arbitrary byte-body profiles are unsupported and reject before
replay admission. This is exact [RFC 8785 JCS](https://www.rfc-editor.org/rfc/rfc8785)
on that declared subset, not a new canonical format or general floating-point JCS
qualification. Five existing shared byte fixtures apply; the numeric renderer fixture
is explicitly unsupported. Generic NWP query/decimal bindings remain unqualified.
The original parsed integer values reach the handler, so a signed rounded number
cannot execute as a different high-precision value. IdentFrame signing is unchanged.

Use the existing SQLite dependency for a dedicated replay database. Every replica of
one audience on a host shares that same local file. Persist audience, capacity, schema,
clock fence and (issuer, projection ID) entries; use WAL/FULL and an immediate write
transaction. Missing files fail closed on normal open. First installation requires
explicit initialization of a nonexistent private file; an existing database is never
silently overwritten or reinitialized. Capacity rejects without evicting live entries.
Clock rollback fails closed. Multi-host/network filesystems are unsupported.

ResultActionProvider remains disabled by default. A separate explicit projected mode
requires exactly `nps.data_results=1,nps.invocation_context=1`; direct mode continues
to reject projections. Neither mode advertises support automatically. The serving
host supplies authenticated channel policy, live keys/revocation, tenant/revision,
resource grants and model-egress decisions. No new credential, algorithm, frame,
endpoint or global authentication dependency is introduced.

## Audit and failure behavior

Optional host audit callbacks receive typed minimized admission/operation events:
verified principal, operation, outcome and hashed result locator. They never receive
rows, aggregate values/counts, field lists, raw locators, projections, signatures or
keys. Unverified failures have no asserted subject. Configured audit failure is
unavailable, never permission to dispatch. A response/audit failure after allocation
does not prove non-execution; recover via the logical create key and a fresh projection.

## Verification and limits

Byte/schema/crypto tests use actual Ed25519 with fixed shared bytes and attack cases;
replay tests use actual files and independent connections. Loopback Kestrel tests use
real mTLS transport with explicit ephemeral certificate pins, signed gateway envelopes,
the durable replay store and actual result operations/policy. An authorized transport
peer with the wrong issuer binding must reject a copied valid signature before handler
dispatch. These test pins are not production PKI configuration.

This receiver-hop evidence does not qualify public upstream gateway authentication,
native sessions, CA-owner revocation feeds, multi-host replay, general decimal schemas,
supported public-client discovery or all six SDKs. No feature offer, release projection
or completion of the old full #129/P20-108 corpus follows from these components.

Subsequent reference increments are scoped separately in the
[client/discovery ADR](2026-11-02-result-http-discovery.md) and
[gateway/revocation ADR](2026-11-02-result-gateway-projection.md). They extend the
explicit test topology without turning this receiver decision into a suite-wide claim.
