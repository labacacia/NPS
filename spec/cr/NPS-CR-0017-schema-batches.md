# NPS-CR-0017: Bounded schema batches and diff recovery

- Status: Draft; author-reviewed for experimental reference implementation.
- Date: 2026-11-02; target alpha.20; D20-04 #168, qualification #129/#130/#133.
- Contract: [English](../profiles/schema-batches.md) / [中文](../profiles/schema-batches.cn.md).

## Motivation and decision

The existing .NET JsonElement formatter writes object rows as MessagePack maps,
including each property name. Caps/Stream carry arbitrary JsonElement records;
neither codec infers positional rows from an anchor. Diff carries an anchor/base
sequence and optional entity ID but no receiver currently enforces their relationship
to a completed stream. Representation tests execute both actual codecs to establish
this gap. Serialization is not resynchronization, and wire size is not model billing.

Use existing Anchor/Caps/Stream/Diff frames with explicit profile semantics:
ordered array rows, a complete Stream baseline identified by stream_id, version zero
only after its final chunk, and Diff entity_id bound to that baseline epoch. Every
accepted diff advances that epoch's version by one. Schema change, reconnect or any
invalid update requires a newly authorized complete baseline. No frame/property,
compression format, automatic tuple conversion, general JSON Patch interpreter or
identity mechanism is added. Unary Caps is explicitly unversioned and cannot establish
a diff base. This avoids inventing an implicit version for legacy Caps.

The proposed exact feature is nps.schema_batches=1, experimental/default-off. The
profile remains a candidate until its complete runtime/SDK evidence is reviewed.
The original D20-00 manifest and all predecessor hashes remain unchanged;
[additive registration](../../docs/adr/2026-11-02-additive-schema-batch-registration.md)
records current registry 0.3 and preserves the original registry/matrix snapshots.
Neither registration, package nor helper installation enables the feature.

## SDK, migration and conformance

.NET qualifies the bounded reference first. Other SDK ports and support-ledger claims
remain D20-08/#130. Unselected alpha.19 codecs and operations retain their behavior.
Tests cover real codec representation, schema/order/type/TTL, complete baseline,
duplicate/gapped/reordered chunks and diffs, wrong epoch/base, atomic patch rejection,
credit, cancellation, reconnect and source-oracle recovery. Resource and efficiency
measurements must include complete framing/schema and equivalent baseline work.
No gain is assumed, including for small payloads or schema churn.

## Acceptance and proposed changelog

- [x] Actual codec representation checked and explicit recovery contract authored.
- [x] Local bounded reference and fault/recovery tests executed through encoded frames (43 cases); not transport qualification.
- [x] Reference mTLS binding, canonical fixed-frame corpus and constrained registry successor integrated; broader SDK/admission qualification remains separate.
- [ ] Same-boundary compression/alternative baselines, CPU/allocation/peak evidence recorded.
- [ ] EN/CN module and operation records match supported behavior.

Proposed entry: experimental schema-bound batches and deterministic full-baseline
recovery for optional transports; no provider-token saving or general stream support
is implied. Data-result streams and NWP subscription adoption require separate policy
and binding qualification. Open: measured reference value and six-SDK qualification,
not a predetermined compression percentage.
