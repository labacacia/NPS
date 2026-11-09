# ADR: Reference result binding qualification

- Status: Accepted experimental reference scope; suite/native qualification remains pending.
- Date: 2026-11-02.
- Trace: D20-02 #166, D20-03 #167, D20-08 #172; DR-01/DR-10.
- Basis: frozen data-result contract and user-authorized continuous implementation;
  author review under the pre-1.0 CR process. No original gate or contract is waived.

## Context

The candidate contract can bind to native and canonical HTTP. A candidate binding is
not an implemented/qualified endpoint. DR-10 requires qualification per operation/
binding before advertisement. The reference has actual public-client/two-hop HTTP
evidence; native result calls lack its required verified host binding. Treating a
package version or HTTP gate as native support would cross that trust boundary.

Framed regression tests reproduced native dispatch skipping `AuthorizeAsync`: the
controlled-denial probe entered Execute, and real result-provider denials occurred
after Execute entry or escaped without the controlled ErrorFrame. All four new cases
failed before the fix. The scoped fix calls authorization first with the same context
and serializes known provider errors, preserving default no-op providers.

## Decision and evidence boundaries

| Reference path | Implemented/observed scope | Advertisement |
| --- | --- | --- |
| Canonical HTTP with invocation-context revision1 | Integer-only original request binding; actual two-hop provisioned mTLS, NIP/CA checks, durable single-host replay, live result policy, public client, lifecycle/oracle/budget/cancellation/revocation evidence. | Explicit fixture-only offers; registration and package version install none. A real host must qualify its actual identities, keys, authorities and all backends. |
| Direct-identity canonical HTTP | Mandatory host-verifier integration API and synthetic identity/digest HTTP tests. No independently qualified direct production/reference identity endpoint is supplied here. | No automatic offer; separate real verifier/route qualification required. |
| Native result operations | Explicit unsupported route. Framed JSON/MessagePack tests assert zero Execute, verifier and capture calls, with canonical feature-unavailable error. | No result offer, positive native identity/session or data-result availability claim. |

Complete D20-02/03 reference AC against the claimed projected HTTP path and verified
negative unsupported paths. Native unavailability is never counted as a positive pass.
The conditional future-derived-result and continuing-stream constraints remain in the
contract: this slice creates no result-from-result resource, new derived TTL or result
stream action. Filtered source capture reapplies live source authority; aggregate/page
responses retain the original descriptor. New support requires its own implementation
and qualification rather than inferring it from these tests.

Calling the existing native action authorization hook and mapping its controlled error
is a minimal scope-supporting change for current DR-01 zero-dispatch and D20-03 authority
AC. It adds no native NIP framework, identity type, crypto primitive, negotiation field,
frame, replay promise, NCP batch/delta feature or new action. NCP D20-04 transport work
keeps its separate gate. The host still owns all actual identity and scope evidence.

## Consequences and retained obligations

Reference lifecycle and authority completion can be assessed without pretending every
candidate transport is available. Supported subset and qualification limits must remain
visible in module/API/AC records. The native regressions use a scripted transport and
real codecs, not network/PKI qualification. The HTTP topology uses provisioned pins and
one explicitly configured source, not general production PKI or arbitrary bridge migration.

D20-08 retains six-SDK, daemon/adopter, compatibility, fault/security and support-ledger
qualification through #129/#130/#131/#132/#133. Any retained native/adopter commitment
still needs implementation or an explicit disposition by its gate authority before
release. This record does not change those obligations, the frozen wire contract,
repository topology or #134 publication/beta decision. Required security/registry checks
remain unwaived; no merge, release projection or remote advertisement is performed.
