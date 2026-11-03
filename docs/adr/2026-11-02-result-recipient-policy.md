# ADR: Recipient grants bind one captured result

- Status: Accepted experimental resource-plane implementation; NIP/bridge qualification pending.
- Date: 2026-11-02.
- Trace: D20-03 #167, integrated with D20-02 #166.

## Context

The store initially supplied source/owner/field/action policy inputs without an exact
result reference. That is insufficient for a recipient grant: authorizing one owner's
source could unintentionally cover another result from that same source. Identity and
source permission alone do not establish permission to read each retained snapshot.

## Decision

Pass the server-owned result reference, snapshot and captured owner-policy revision
to the live policy callback for retained operations and replay. Add a bounded
ResultResourcePolicy with explicit source authority and recipient grants. Unknown
sources/subjects deny. A grant names one issuer/result/snapshot, recipient, operations,
fields and expiry. Only read/aggregate are delegable; recipients cannot release,
create on behalf of an owner or recursively grant.

The store creates ResultGrantScope only after owner-authorized lookup. Its constructor
is internal and its state immutable. This is an in-process policy input, not a signed
credential, new ActionFrame operation or caller-authored grant. Grant administration
is a trusted host API and is not registered on /invoke. A copied reference or grant
management ID grants no authority. Multiple narrow grants are not unioned implicitly.

Current source/owner policy constrains every operation, even existing grants. Source
revocation, delegation withdrawal, narrower operations/fields and model-egress changes
apply immediately at the next check. Changing the row-policy revision conservatively
invalidates retained snapshots and requires explicit recapture; it never silently
rewrites rows or revives an old grant. Model egress needs both live source permission
and an explicit recipient egress grant. Expiry cannot exceed the original result.

The process policy separately bounds 128 source authorities, 4096 grants and 16 MiB
logical source/grant metadata (serialized values plus fixed overhead). Deployments
may lower all limits. Atomic admission rejects capacity without evicting live grants.
Grant expiry uses monotonic elapsed time and a nondecreasing wall-clock observation.
Restart loses grants consistently with process-only result retention; no durable
identity replay or replicated grant guarantee is introduced.

## Alternatives and qualification

Source-wide recipient permissions and bearer handles would broaden authority. A new
cryptographic grant format would duplicate NIP and create an unreviewed wire contract.
Persisting grants would imply unavailable result recovery. These alternatives are not
needed for the accepted process-local reference profile.

Resource-plane tests run with trusted fixture principals. HTTP tests execute actual
middleware/store/policy decisions and assert handler/capture counters; they do not
verify NIP proof possession or a real bridge. The host must connect owner NIP/source
revocation to current source authority and provide verified caller/capability/scope,
canonical request binding and authenticated channel identity. Existing TypeScript
projection/JCS/durable single-host replay components should be reused when qualifying
the invocation-context leg; their existence alone is not transport qualification.

## Data flow and auditing

Verified NIP/host admission → exact store resource lookup → current source/recipient
intersection → bounded result → separate model-egress check. Untrusted headers,
projections and model tool arguments cannot update source authority or issue grants.
TrustFrame remains CA trust, not per-result permission.

Host audits record verified principal, action, outcome/error category and bounded
hashed identifiers. They must not contain rows, aggregate values, counts, field lists,
credentials, request/projection bodies or signatures. Policy errors expose only the
registered generic unavailable/capacity messages. No audit persistence or NIP/bridge
support is claimed by installing this policy class.
