# ADR: Schema-batch reference session binding

- Status: Accepted for experimental .NET reference; wider runtime/SDK qualification pending.
- Date: 2026-11-02; D20-04 #168; [CR-0017](../../spec/cr/NPS-CR-0017-schema-batches.md).
- Basis: user-authorized continuous implementation and pre-1.0 author review.

## Gap and decision

Passing a FeatureSelection to a local receiver does not establish actual negotiation.
The native public APIs previously ignored CR-0012 offers and could return a base session
without acknowledging a required feature. Extend existing Hello/handshake Caps with
the already specified bounded fields; no new frame, endpoint or crypto primitive.
This increment admits only nps.schema_batches=1 under explicit local opt-in on both
ends. Other optional IDs are omitted and required unsupported IDs reject. Broader
optional-feature dependencies and six-SDK qualification remain their existing tracks.

Validate the actual raw Hello before typed decoding loses duplicate keys. Bound a
complete feature-bearing handshake payload to 16 KiB and the smaller negotiated/local
payload ceiling. Select only locally enabled exact revisions, acknowledge even an
empty set, and validate the response against the offer/required set. Old missing echoes,
unoffered/repeated/wrong revisions and malformed selections fail before returning an
application session. Never carry selection across reconnect or renegotiate via data Caps.

The first six malicious-peer cases exposed a real bug: a MessagePack selection revision
1.0 normalized to integer 1 before validation and admitted a session. Five cases passed
and that case failed. The guard now validates selection numeric token types before
normalization; all six then pass. Independent handshake metadata may still contain
floating values; the new restriction applies to revision syntax, not arbitrary metadata.

## Transport, authority and lifetime

The qualified reference subset requires a mutually authenticated SslStream on both
ends before selecting this feature. The client gains a host-owned authentication hook
matching the server's existing boundary; pins/PKI and source/identity authorization
remain host responsibilities. A wrapper flag or feature offer is not identity evidence.
Other verified carriers need explicit implementation/qualification before being claimed.
Unselected base connections retain existing behavior, including configured plaintext
development transports. No default profile or package registration enables the feature.

NcpSession stores immutable validated selections. Its factory applies the negotiated
payload and encoding ceilings, allows one live reference receiver, rejects after session
disposal and disposes its receiver on close. The caller still serializes ingress,
checks live source policy before each delivery and disposes on unexpected EOF or policy
withdrawal. GetStream is a low-level transport API, not a resource grant or subscription
scheduler. Receiver-local construction remains available for pure codec qualification
and never counts as remote authority.

## Evidence and retained boundaries

Shared fixed-byte work exposed omitted patch_format being overwritten by typed decoding
and explicit value:null being conflated with missing Value. An initial 100-case authoring
set passed 84, then 98 after default restoration, then all 100 after preserving explicit
null. That pre-canonical authoring set differs from the final 106-case canonical corpus.
A canonical MessagePack baseline independently failed on legacy .NET PascalCase keys;
Python/TypeScript source uses snake_case. The selected profile and feature-bearing
handshake now project canonical fields through the existing MessagePack library and
source-generated JSON metadata. Legacy unselected codecs remain unchanged. Raw token
validation precedes projection; its bounded allocation/CPU is part of future measurements.

A separate linked schema-string supplement then reproduced 18 failures out of 20
encoded Unicode/HTML/control cases: legacy JSON string escaping differed from
[RFC 8785 string serialization](https://www.rfc-editor.org/rfc/rfc8785.html#section-3.2.2.2).
The profile's public ComputeAnchorId uses the existing ordered schema structure with
the strict string primitive extracted unchanged from the qualified invocation slice.
This is a minimal scope-supporting reuse for SB-01 schema identity, not a new canonical
algorithm, signature scheme or authority. The original 106-case freeze and unselected
legacy AnchorIdComputer behavior stay unchanged; no Unicode normalization is applied.
The supplement covers the materialized field shape (nullable present, null semantic
omitted), not arbitrary JSON/default inference. Invocation bytes and constraints retain
their independent signature/replay regression evidence.

The actual pinned mTLS fixture uses public client/server/session APIs, independent
client/server certificates and two encodings. It transfers a staged full baseline,
waits for an actual reverse credit frame, applies an epoch/schema/base-bound diff and
matches the source sum. Negative cases cover local/server opt-out, plaintext refusal,
ambiguous/oversized raw Hello, old/malicious selection, reconnect, tier mismatch and
lifetime. These pins and one fixture source are not general NIP/PKI qualification,
native result operations, NWP live-result subscriptions or all-backend authority.

Existing D20-00 result semantics and predecessor evidence remain unchanged. The
[additive candidate successor](2026-11-02-additive-schema-batch-registration.md) records
registry 0.3 and the two exact current replacements. Portable reference consumption is
recorded separately; source-scale/fault/resource comparisons and
equivalent compressed HTTP/alternative baselines remain D20-04 IN_PROGRESS. No measured
efficiency, model-token/billing benefit, six-SDK qualification, merge or publication is
inferred. Required security/registry gates and #129/#130/#131/#132/#133/#134 remain intact.
