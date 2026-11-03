# ADR: Result actions own process-local replay

- Status: Accepted experimental implementation boundary; remote qualification pending.
- Date: 2026-11-02.
- Trace: D20-02 #166; scope-supporting change for its expiry/revocation/public-route AC.
- Contract: [data results](../../spec/profiles/data-results.md).

## Context and decision

ActionNode's generic cache retains action results for 24 hours and scopes them using
AgentNid request metadata. A result resource lives at most 900 seconds, belongs to a
verified tenant/principal and requires live source/grant/field/egress authorization.
Returning a cached descriptor/page could bypass its expiry or release despite a
provider's initial identity check. Reusing that cache is unsafe for this profile.

Add `IActionNodeProvider.ManagesIdempotency`, default false. Only providers that own
all replay, binding, policy and expiry logic opt out of the middleware cache.
ResultActionProvider opts out for every operation. Create replay is then atomic in
ResultResourceStore with tenant/subject/source/key/digest binding and bounded TTL.
Read and aggregate always execute live policy checks. Release retains bounded metadata.
Other providers retain their existing generic replay path and tests.

ActionFrame's JSON serialization also explicitly excludes its internal FrameType/
PreferredTier properties, as its existing MessagePack binding already does. Otherwise
typed public HTTP requests accidentally emit those SDK-only fields and fail the strict
profile parser. This is a scope-supporting wire fix for D20-02 public-route AC, covered
by typed ActionFrame HTTP round trips and the broader NWP/serialization regression.

For this HTTP adapter, ActionContext carries the host HttpContext and a cloned parsed
request preserving field presence. These are not credentials or persisted state.
Only the deployment verifier may derive ResultPrincipal and a canonical logical
request digest from an authenticated proof. Native/background contexts cannot claim
HTTP admission. Provider-selected features are echoed only after synchronous success.
The provider has a finite 64 KiB request limit and canonical error media type; legacy
provider defaults remain. No new source/storage/authentication dependency is added.

## Alternatives, security and consequences

Extending generic cache TTL or adding the raw AgentNid to a key cannot provide live
resource policy. Replacing all existing ActionNode authentication/replay is broader
than D20-02. A mandatory deployment verifier plus fail-closed opt-in keeps this slice
reviewable; it does not qualify NIP/bridge/native support or advertise a route.

The source validator is pure and capture is allowlisted, consistent and row-filtered.
No arbitrary source URL, SQL or script is executed. Identity/feature-policy failures
are bounded and sanitized. Provider callbacks are trusted integration code; errors
and logs must still follow the profile's minimization rules.

HTTP integration tests observe actual capsule/error boundaries, handler/capture
counts, policy revocation, expiry, bounded requests and model-egress checks. They use
a synthetic admission fixture; real NIP proof possession, JCS request binding,
route discovery and bridge behavior remain D20-03/public-client qualification.
No production authentication mechanism is introduced by that fixture.
