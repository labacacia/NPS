# ADR: Explicit result HTTP discovery and owned client transport

- Status: Accepted experimental reference scope.
- Date: 2026-11-02.
- Trace: D20-02 #166; D20-03 #167; NWP §4.1, §9.6–9.7.

## Context and decision

A registered action is insufficient evidence that its authenticated route and every
backend implement a negotiated feature. A result client must discover affirmative
support before sending an operation and preserve uncertainty after dispatch.

Add the existing optional NWM fields to the .NET model. Hosts can explicitly install
`ActionNodeManifestSource`, supplying immutable metadata snapshots and a persistent,
monotonic structural revision. The source checks node binding, feature syntax and
bounded size, and rejects observed rollback or changed content under one revision.
`/.nwm` returns `X-NWM-Version`; an unchanged integer `If-None-Match` returns 304.
The action/schema registry comes from the same source. Registration does not create
an offer or enable result execution. The host owns persistent revision management,
atomic offer/route-policy rollout and the all-backend guarantee across restart.

`ResultHttpClient` pins an HTTPS node route and issuer, fetches fresh NWM before each
operation, and checks authentication requirements, target, action capability, version,
profile and exact revision offers. It retains observed manifest revision/content to
reject rollback and same-version substitution. The owned `SocketsHttpHandler` forces
redirects and cookies off after transport configuration. Reusing an arbitrary injected
HttpClient was rejected because it can follow redirects before the client validates
the response, forwarding authority to another route. TLS/mTLS trust remains an explicit
host responsibility. A request signer selects the projected leg and receives the exact
body, pinned node and required feature pairs; upstream caller admission remains separate.

## Failure, security and consequences

Discovery failure precedes signing and POST. A valid offer is discovery, never authority.
The receiver still verifies identity, request, replay and live resource/model policy.
After POST, success requires a bounded capsule and exact selected-feature echo; errors
require the canonical structured media type without a success echo. Malformed or
incomplete responses, transport failures and 5xx errors preserve uncertain execution.
No automatic retry, pagination or legacy fallback occurs. Cancellation propagates and
does not prove rollback. Recover an uncertain create with the original UUID-v4 key,
unchanged logical parameters and a fresh identity proof while retention permits.

Response parsing bounds bytes/depth, rejects duplicate decoded keys and invalid typed
descriptors, and checks issuer/snapshot/schema consistency. Error objects expose bounded
codes and status, never raw response bodies or credentials. There is no added dependency,
new endpoint, feature revision, credential or change to repository release ownership.

Loopback mTLS tests cover the public client through the real receiver, lifecycle,
pagination, COUNT/SUM, structured errors, capability withdrawal, malformed/oversized
responses, missing echo recovery, redirects and revision fences. Test-only explicit
offers do not qualify production routes. Upstream gateway authentication, owner CA
revocation, native/general numeric and six-SDK acceptance remain D20-03/later gates.
