English | [中文](schema-batches.cn.md)

# NCP schema batches — candidate revision 1

Date: 2026-11-02. Experimental, disabled by default. [CR-0017](../cr/NPS-CR-0017-schema-batches.md),
D20-04 #168. Proposed exact selection: `nps.schema_batches=1`. This is an additive
candidate, not a suite/registry support assertion. The .NET opt-in mTLS reference now
implements exact session selection; wider binding/suite qualification remains pending.
Frozen D20-00 result artifacts and unselected base frame semantics are unchanged.

## Admission and representation

Both hosts must explicitly enable this revision and verify selection on the actual
admitted connection/operation before interpreting positional data. A caller boolean,
anchor, version, stream ID or feature offer supplies no identity or resource authority.
Bind a receiver to one authenticated producer, authorized source and subscription;
recheck live policy before every delivery. This profile does not register a NWP result
stream/action, bypass NIP, or advertise an endpoint. Reconnect/failover requires fresh
admission and a complete source snapshot. Never replay an unproven mutation.

The initial .NET reference selects this feature only on a mutually authenticated TLS
session. It verifies bounded raw Hello/handshake Caps, exact selection and old-peer
omission before returning an application session. The session factory owns one live
receiver, applies negotiated payload/tier limits and disposes it on session close.
General NIP identity/source policy and other carriers require separate qualification;
see the [session binding ADR](../../docs/adr/2026-11-02-schema-batch-session-binding.md).

**SB-01.** Anchor schema field order is significant. Verify `anchor_id` using the existing
AnchorIdComputer JCS/SHA-256 algorithm; do not use the legacy cache's sorted-field helper
for positional meaning. Limit 64 unique nonempty field names of at most 64 UTF-8 bytes.
The reference subset is string, bool, int32 and uint64 (restricted to 0..9007199254740991),
with explicit nullable fields. Strings are UTF-8 JSON strings; float/decimal/exponent
numeric tokens are unsupported. Schema TTL is 1..900 seconds, absolute and checked
with monotonic elapsed time as well as wall time. Expiry is exclusive.

**SB-02.** Each `CapsFrame.data` or `StreamFrame.data` item is an ordered JSON array with
exactly one value per schema field, including explicit nulls. No omitted trailing cell,
extra cell, type coercion, unknown schema or inferred object-to-tuple conversion.
MessagePack encodes these existing arrays as arrays. Legacy object rows remain objects.
This says nothing about model-side JSON tokenization. Caps.count equals row count;
unary Caps never creates a versioned delta baseline in revision 1.

Both encodings use canonical snake_case envelope/schema/patch member names in this
profile, including feature-bearing native handshake Caps. Do not serialize language
property names as wire keys or rewrite fixture keys to make one SDK pass. The .NET
reference uses an explicitly selected SchemaBatchFrameCodec/session encoder; its
unselected legacy codecs retain their existing behavior. Base-only old handshake Caps
may be recognized only as missing feature acknowledgment, never as a selected revision.

## Baseline and diffs

**SB-03.** One receiver owns at most one schema and one baseline epoch. `stream_id` is a
UUID v4 identifying that epoch. A newly admitted full baseline starts with seq=0 and an
explicit anchor_ref. Later chunks increment seq without wrap and may omit anchor_ref;
if present it must equal the initial schema. Stage the complete snapshot privately;
only is_last=true without error publishes version 0. Empty baselines are allowed.
Abnormal final chunks, gaps, duplicates, reordering, another stream ID or schema changes
discard the partial snapshot and terminate this receiver. No partial source is exposed.

**SB-04.** A Diff applies only after a completed baseline. Its entity_id must equal that
baseline's stream_id, anchor_ref must equal its schema, and base_seq must equal the
current data version. This epoch is an identity for a changing dataset, not an immutable
result handle; the full state identity is (producer/subscription, epoch, schema, version).
The reference accepts only json_patch, 1..256 unique replace operations at canonical
JSON Pointer `/<row index>/<field index>` paths, with no leading zeroes, signs, escapes
or from. Both indices must exist, and every new value must satisfy its schema type.
Apply all replacements atomically, then increment version by one without overflow.
An explicit `value:null` is present and valid for a nullable cell; an omitted value is
invalid. Omitted patch_format selects json_patch; explicit null does not select a default.
No append/remove/move/copy, schema patch or binary_bitset fallback. Unsupported formats
fail explicitly. Duplicate/wrong-base updates never silently replay or apply twice.

**SB-05.** Any schema/update/sequence/window/cancellation failure makes the receiver
terminal and clears retained data. Recovery uses a new receiver and freshly authorized
full Stream baseline, with a new epoch ID, then version 0. Missing/stale schema requires
resending the verified Anchor too. Do not guess a base or carry cache/credit across
reconnect. Deterministic rejection is a valid outcome; invisible partial recovery is not.

## Credit and limits

**SB-06.** Receiver-local initial credit is configured before the first delivery, at
most 8 frames. A sender's first window_size may narrow but cannot enlarge that credit;
subsequent data frames cannot refresh it. Each accepted chunk spends one credit.
The consumer grants new bounded absolute credit only after draining/processing, using
the existing reverse empty StreamFrame mechanism on its actual binding. Zero credit
pauses sending; a received data frame at zero terminates with stream-window-overflow.
Control frames are identified by direction/state, never by treating an empty data
chunk as an ACK on the same direction. No unbounded application queue is introduced.

**SB-07.** Reference hard ceilings: 65536 encoded payload bytes/frame, 512 rows/chunk,
100000 retained rows, 33554432 compact UTF-8 tuple bytes/snapshot, 64 fields, 256 patch
operations, 8 credit and one epoch/schema per receiver. The reference counts and owns
the same compact JsonSerializer/Utf8JsonWriter tuple representation, including outer
array punctuation; this is not RFC 8785 JCS for arbitrary data. Peer whitespace cannot
remain in owned rows after it was excluded from that count.
Decoder allocation and process RSS are separate from retained bytes;
measure them. The smaller negotiated/local limit always wins. Overflow/cancellation
never returns partial success or keeps a live baseline. Host owns the bounded decoder,
serialized ingress and cancellation/consumer lifetime; the receiver is not thread safe.

## Errors, evidence and qualification

**SB-08.** Reuse existing errors where meaningful: unknown schema AnchorNotFound;
schema/hash/type AnchorSchemaInvalid or AnchorIdMismatch; schema switch AnchorStale;
chunk/update/base ordering StreamSeqGap; wrong epoch StreamNotFound; credit
StreamWindowOverflow; row/byte/count limit StreamLimitExceeded/FramePayloadTooLarge;
unsupported diff DiffFormatUnsupported. Emit only bounded generic diagnostics, never
rows or arbitrary peer strings. Canceled local work propagates cancellation.

**SB-09.** Qualification requires actual encoded frames and a real negotiated binding,
source-oracle round trips/recovery, adversarial cases, credit/slow-consumer/cancellation
and negotiated payload limits. A locally exercised receiver is not a native transport,
NWP policy or all-SDK pass. Compare complete schema/framing plus data, including schema
churn, with equivalent gzip JSON and alternative representations before advertising
any gain. Record allocation/CPU/peak-memory boundaries and negative results. No model
token, billing, Arrow/Flight, Protobuf or transport superiority follows from this contract.
