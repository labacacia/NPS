English | [中文版](./NPS-CR-0012-feature-negotiation.cn.md)

# NPS-CR-0012: Feature Revision Negotiation

- **Status**: Draft
- **Target**: v1.0.0-alpha.20
- **Date**: 2026-09-09
- **Author**: Codex (Epic design implementation)
- **Discussion**: [NPS-Dev #124](https://github.com/labacacia/NPS-Dev/issues/124)
- **Epic**: [ChangeControl EPIC-007](https://github.com/innolotus/ChangeControl/issues/7)
**Touches**: NCP Hello/handshake Caps, NWP NWM/HTTP admission, error registry and six-SDK vectors

## 1. Summary

Normative integration: NCP §2.6.3 / NWP §9.6 clarify the byte bound: complete
feature-bearing native handshake payloads are capped at 16 KiB including base fields;
HTTP discovery caps the raw JSON profile value. This supersedes the ambiguous
standalone-block wording in the historical proposal below. Full wire vectors remain pending.

Add bounded, explicit feature revision offers and selections to existing carriers. Reuse Hello
(0x06), handshake Caps (0x04) and NWM; allocate no new frame type. Required support must be
affirmatively established before dispatch. This document proposes exact additions; it does not
change the released normative specifications or claim implemented alpha.20 support.

## 2. Motivation

Suite version does not prove support for a particular optional profile. Old peers may ignore
unknown fields or headers, and product adapters may support a handshake without the requested
application semantics. A required feature must never silently degrade into a legacy operation.
This implements ADR-004 D20-01 and A20-R001..R004.

## 3. Proposed specification additions

### 3.1 Types and limits

All names below are JSON snake_case. Arrays are sets: duplicate IDs/revisions are invalid.
Feature ID: ASCII pattern `^[a-z][a-z0-9]*(\.[a-z][a-z0-9_-]*)+$`, at most 96 bytes.
The `nps.` namespace is reserved for published NPS profiles; examples use `example.`.
Each ID maps to 1..16 revision descriptors, sorted by ascending revision. Revision is a positive
uint16 (1..65535). Lifecycle is exactly experimental, stable or deprecated, attached to each
revision. No automatic revision ordering implies semantic compatibility.

`feature_offer`: array of at most 32 objects
`{"id":"example.echo","revisions":[{"revision":1,"lifecycle":"stable"}]}`.
`required_features`: array of at most 32 offered feature IDs.
`selected_features`: array of at most 32 `{"id":"example.echo","revision":1}` objects.
At most 32 IDs may occur across a transaction. Unknown object members in these structures,
duplicate JSON keys, wrong types, fractional revisions and unsupported negotiation versions
are invalid. UTF-8 JSON for the negotiation block is capped at 16 KiB, also subject to the
negotiated frame/request limit; the smaller bound wins. Missing optional arrays mean empty.

Experimental revisions require explicit local opt-in on both peers; receivers MUST NOT use a
remote lifecycle assertion to bypass their own lifecycle policy. Deprecated revisions remain
eligible only when local policy permits. Stable meaning cannot be incompatibly repurposed.
The feature registry defines each revision's complete semantics and cross-feature constraints.

### 3.2 Native mode

Add optional Hello fields `feature_negotiation: 1`, `feature_offer`, `required_features`.
Presence of offer/required fields without negotiation version is invalid.

A version-1 server validates the offer, intersects revision numbers with its locally enabled
revisions, then selects the highest mutually eligible revision for each shared ID. Local policy
filters the eligible set before selection. Required IDs must occur in the offer and in the
result; an unknown required ID or empty required intersection rejects the handshake. Optional
unknown IDs are omitted. Cross-feature constraints are validated before accepting the selection;
an invalid combination rejects rather than silently changing deterministic selection.

Handshake Caps adds `feature_negotiation: 1` and `selected_features`, including an explicit empty
array when nothing is selected. Selected revisions MUST be a subset of the client's offer.
Clients reject duplicate, unoffered or policy-ineligible selections and missing required IDs.
Offers/selections are valid only during handshake and MUST NOT be accepted as application Caps
extensions that renegotiate an active connection.

If an old server omits the version/selection acknowledgment, the client considers the selected
set empty. It MUST close without application dispatch when any feature is required.
If no feature is required, it MAY proceed with unchanged base behavior. A rejected extension
handshake MAY be retried on a fresh base-only connection only when local policy allows and no
required feature or application request is carried forward. Never replay a dispatched operation.

Selection belongs to this admitted session. Reconnect/failover requires fresh negotiation and
identity admission. Feature support grants no capabilities, scope or assurance by itself.
An application API MUST reject an operation whose required revision is absent from the session.
An active feature cannot be silently withdrawn: stop admission/drain/close and renegotiate.

Example Hello additions:

```json
{"feature_negotiation":1,"feature_offer":[{"id":"example.echo","revisions":[{"revision":1,"lifecycle":"stable"},{"revision":2,"lifecycle":"stable"}]}],"required_features":["example.echo"]}
```

If the server supports revisions 1 and 3, the only common selected revision is 1:

```json
{"feature_negotiation":1,"selected_features":[{"id":"example.echo","revision":1}]}
```

### 3.3 HTTP mode

Add optional NWM profile:
`profiles.feature_negotiation = {"version":1,"feature_offer":[...]}`.
NWM's existing manifest version changes when the feature offer changes. This describes all
operations on the advertised service endpoint; narrower action-specific support must be checked
again by the operation handler before any side effect. Discovery alone is not admission.

For an operation that uses selected features, send `X-NPS-Required-Features` as a single ASCII
header: comma-separated `id=revision` pairs in ascending ID order, no whitespace, duplicates
or leading zeroes. All entries are required exact revisions for that request; even a feature
optional during discovery becomes required when its semantics are used. Limit: 32 pairs and
4096 bytes. Repeated header instances are invalid; gateways must reject ambiguous duplicates.

Before dispatch, the endpoint parses the header and validates each exact revision against its
current enabled set, feature dependencies, operation support and authenticated admission policy.
Unsupported or withdrawn support fails before invoking a handler. Successful responses echo
the accepted pairs in `X-NPS-Selected-Features`; failed requests have no selected-set assertion.
Malformed or absent echo on success is a client protocol error with an uncertain operation
outcome, not permission to repeat the mutation.

Before sending a feature-dependent operation, clients MUST obtain an affirmative version-1
advertisement over the authenticated route that serves the operation. A route advertising this
profile MUST enforce it on every possible backend, or reject the request before forwarding.
Mixed old/new backends MUST NOT advertise this profile until ingress can guarantee enforcement
and dispatch only to compatible targets. Do not follow redirects to unverified routes or fall
back to an old endpoint for required features. If this route guarantee cannot be established,
fail locally without sending the operation. An old server ignoring headers is NOT a safe
fallback, and detecting that fact in a response is too late to prevent side effects.

A client that does not use this profile omits the header and retains base behavior.
A profile-aware server still validates malformed feature headers, even on a base operation.
Native and HTTP selections are separate; gateways revalidate each downstream leg and cannot
translate a feature claim into authorization. Bind these requirements into the request digest
where authenticated context projection is used (P20-102).

### 3.4 Proposed error assignments

These are proposed additions to error-codes.md, not already assigned released errors.

| Error | Status | HTTP | Condition |
|---|---|---|---|
| NCP-FEATURE-NEGOTIATION-INVALID | NPS-CLIENT-BAD-FRAME | 400 | Malformed/oversized offer, version, duplicate or invalid selection |
| NCP-FEATURE-REQUIRED-UNSUPPORTED | NPS-CLIENT-CONFLICT | 409 | Required native support absent or no eligible common revision |
| NWP-FEATURE-REQUIREMENTS-INVALID | NPS-CLIENT-BAD-FRAME | 400 | Invalid HTTP required header |
| NWP-FEATURE-REQUIRED-UNSUPPORTED | NPS-CLIENT-CONFLICT | 409 | HTTP exact revision unavailable for this operation |

Protocol clients also surface the matching local error when an old peer cannot emit it.
Error details identify bounded feature IDs/revisions, not credentials or request bodies.
Existing transport/frame size errors may take precedence if parsing cannot reach this block.

## 4. SDK changes

| SDK | Required delta |
|---|---|
| .NET | Typed offer/selection models, native negotiation validation, HTTP admission/client helpers |
| Python | Same algorithm and strict integer/duplicate parsing; no bool-as-integer acceptance |
| TypeScript | Same algorithm with uint16/integer checks and bounded header parser |
| Go | Same models, deterministic selection and reconnect reset |
| Java | Same immutable selection state and required-feature pre-dispatch check |
| Rust | Same typed lifecycle/revision validation and admitted session selection |

All six preserve legacy operation behavior when this feature is unused. Do not turn unknown
fields into advertised support. P20-105/107/108/109 own final version changes, normative text,
shared fixtures and compatibility freeze; P20-201..208 own implementation/parity.

## 5. Conformance additions

| Vector ID | Input/scenario | Required result |
|---|---|---|
| FTR-001 | Offer [1,2], server [1,3] | Select 1 |
| FTR-002 | Unknown optional ID | Omit selection, base behavior remains |
| FTR-003 | Required ID with no common revision | Reject before dispatch |
| FTR-004 | Old Caps without acknowledgment | Required: close; no required: base-only |
| FTR-005 | Server selects unoffered revision | Client rejects before application write |
| FTR-006 | Duplicate ID/revision/key, bool/fraction/zero/overflow | Invalid |
| FTR-007 | Experimental revision without either local opt-in | Not eligible |
| FTR-008 | Feature withdrawn after HTTP discovery | Reject before handler |
| FTR-009 | Old HTTP target or unsafe mixed backend route | Client/ingress blocks before dispatch |
| FTR-010 | Missing success echo after dispatch | Protocol error; no automatic mutation retry |
| FTR-011 | Reconnect/failover or different gateway leg | Fresh selection and admission |
| FTR-012 | Limits exceeded, repeated header, leading zero | Invalid |
| FTR-013 | Base alpha.19↔alpha.20 operation, no feature use | Existing behavior preserved |
| FTR-014 | Invalid cross-feature combination | Reject before dispatch |
| FTR-015 | Forged feature support or remote stable label | No authority/policy expansion |
| FTR-016 | Required ID absent from offer | Invalid |

These are specification scenarios, not claims of executable coverage. P20-108 materializes
positive/negative wire fixtures and dispatch-count assertions in the shared corpus.

## 6. Migration impact

Additive fields still require decoder compatibility proof. Native callers must distinguish
base-only legacy fallback from feature-dependent calls. HTTP operators must qualify ingress
and backend routing before advertising enforcement. No package upgrade alone enables a feature.
Retain old operation defaults and minimum compatible versions unless P20-105 proves a break.

## 7. Out of scope

Feature-specific runtime/context/challenge behavior; actual beta promotion; new frame types;
altering NIP identity formats; treating negotiation as cryptographic peer authentication.

## 8. Acceptance criteria

- [ ] NCP/NWP EN/CN normative additions, errors and version changes integrated (P20-105/107).
- [ ] Registry review establishes real feature IDs/revisions and constraints.
- [ ] FTR-001..016 executable shared fixtures frozen (P20-108).
- [ ] Six SDKs implement equivalent behavior and pass compatibility matrices (P20-201..208/109).
- [ ] HTTP mixed-backend enforcement and native pre-dispatch rejection demonstrated.

P20-101 authors this CR; these implementation gates keep it Draft until implemented.

## 9. Proposed changelog

Add explicit feature revision negotiation in native handshake and HTTP admission, with bounded
metadata, fail-closed required support and legacy base-mode compatibility.

## 10. Open implementation decisions

The wire proposal above is concrete for review. Production feature ID allocation belongs to the
owning profile CRs; full shared fixture encoding and release version freeze remain P20-105/108.
No external RFC acceptance ceremony is required for an alpha CR under this repository's process.
