# ADR: Fresh result projection after public connection admission

- Status: Accepted experimental reference scope; production routes remain opt-in.
- Date: 2026-11-02.
- Trace: D20-03 #167, NIP §7.8, NWP §9.6–9.7; builds on the receiver and discovery ADRs.

## Context and decision

The authenticated receiver cannot establish the original public caller's identity
by validating the gateway's signature alone. A public carrier must never select
the gateway issuer, subject, key, tenant or receiver policy. Its signature also
cannot be reused after changing target, body or feature requirements.

`NipResultHttpProjector` strips the incoming projection header and constructs a new
logical request from the exact outgoing original ActionFrame, pinned receiver and
explicit required features. A mandatory trusted host callback authenticates the
actual public HttpContext, verifies live NIP/revocation and node/action policy,
and narrows capabilities/scope. It runs across live key resolution and must agree
on subject. Exact configured ceilings constrain its output even if configured
sets use a case-insensitive comparer. Bound lifetime to the admitted certificate/
authority and at most 60 seconds. Recheck live key identity and enablement before
signing. Emit a fresh random projection ID for every attempt, retaining the logical
operation key in the request digest. Reuse platform Ed25519 and the existing
integer-only JCS/signature rules; no algorithm or wire envelope changes.

The host still owns public-leg feature admission, bounded input/schema validation,
TLS/mTLS authentication, trusted CA configuration and current resource/model policy.
The class installs no endpoint, feature advertisement, local-development identity or
default anonymous authorization. Deserializing `NipGatewayAuthority` from public input
is prohibited. Its callback must independently verify possession or a qualified
authenticated session; a CA-signed public IdentFrame alone is not authentication.

Add `ResultHttpClient.SendFrameAsync` as the minimal forwarding API: preserve exact
semantic body/presence without a DTO inserting defaults or dropping inputs. It keeps
discovery, pinned TLS route, fresh signer, bounded capsule/error and no-retry checks.
It returns raw JSON payload and intentionally leaves typed descriptor consistency to
the gateway's result API boundary. A public response echoes only public-leg selection,
not the downstream leg's selection. Raw byte/header forwarding was rejected because
it mixes two independently admitted feature and identity boundaries.

## Live revocation and evidence

Reference integration uses actual Kestrel mTLS on two distinct paths/connections,
independently provisioned public Alice/Bob transport pins, a gateway receiver pin,
CA-signed IdentFrames and the real SQLite CA store. The host maps actual TLS peers
to server-provisioned identities, never Agent/forwarding headers. The existing NIP
verifier checks trusted CA signature, required live revocation, capability and node
scope; the host additionally checks the exact operation. These transport pins are
explicit test provisioning, not general public-PKI/ACME qualification.

The receiver rechecks the original subject's current CA status on every admission
and execution, so an already signed proof cannot survive recipient revocation. Its
single registered source has a server-owned Alice identity binding: before every
result operation, including Bob's retained grants, refresh owner CA status and disable
source authority on revocation. An unavailable CA fails closed. A valid owner status
never silently re-enables a separately withdrawn source. This is an explicit host
integration pattern, not a new general CA subscription or persistent grant service.
Revocation does not retract data already delivered.

## Consequences and validation

Copied public carriers/Agent hints cannot select caller identity or pass through;
unmapped TLS peers, revoked identities, unavailable CA and withdrawn signing keys
produce no downstream operation. Receiver replay remains mandatory and durable.
Partial replies still require a fresh projection plus the original logical key;
no automatic retry or claim of rollback is added. Optional receiver/resource audits
continue to minimize identifiers and exclude proofs/rows/credentials.

Tests cover real two-hop lifecycle, same-key recovery, fresh IDs, header stripping,
public and retained-recipient revocation, owner revocation, fail-closed CA/key changes,
exact ceilings, malformed authority and cancellation. Native sessions, arbitrary
numeric/byte profiles, public PKI provisioning, generic MCP/A2A/gRPC gateway migration,
multi-host replay and all-six-SDK qualification remain separate obligations. No
result source, release projection or independently managed repository is added.
