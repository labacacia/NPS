# ADR: Bounded reference schema-batch transfer

- Status: Accepted reference implementation; correctness/scale evidence recorded, comparative resource evidence pending.
- Date: 2026-11-02; D20-04 #168, CR-0017; author review under pre-1.0 rules.

## Outcome and boundary

Connect the qualified session/codec/receiver to actual bounded publication, consumer
credit and current host source policy. Helpers serve a dedicated already negotiated
mTLS connection. They do not register a source/endpoint, create NIP identity or result
grants, install NWP live-result streaming or infer authority from schema/epoch IDs.
The host binds authorization callbacks to the real peer and source and owns source
capture correctness. General PKI/NIP, daemons/adopters and SDK ports keep their gates.

## Flow and resource rules

Authorize before source capture, schema/row exposure and each actual data write.
Capture one immutable schema/row snapshot through a trusted host callback. Publish
Anchor then contiguous Stream chunks; first data credit is conservatively one frame,
so a newly connected consumer never needs to accept a producer-assumed eight frames.
The consumer subsequently grants its configured bounded window (at most eight) after
draining it. Zero pauses publication. Reverse controls must be empty/nonfinal, refer
to the same epoch and match the next expected chunk watermark; invalid or replayed
controls fail deterministically. This reference consumer grants at window boundaries,
avoiding ambiguous shrinking credits with in-flight data. No background read queue.

Use exact session tier/payload and the existing row/chunk/snapshot hard limits. Staging
is at most one chunk; source capture is host-owned immutable storage, not a hidden
second general result store. Small local write counters/progress report no rows, field
names or raw data. A send receipt proves local write completion, not receiver commit.
Actual receiver snapshot/source-oracle equality is the end-to-end completion evidence.

The reader owns a session-bound receiver, rechecks host authority before read/delivery,
and returns only complete immutable baseline/update state. Diffs retain the established
epoch/schema/version and exact typed replace subset. A source/schema change requires
a freshly authorized complete baseline. A host must cancel on authority withdrawal
while blocked; a callback is not a global revocation subscription.

## Failure and verification

Caller cancellation, timeout, policy/capture/encode/write/read faults terminate the
helper and close this dedicated session. Previously delivered bytes cannot be retracted.
There is no automatic retry, rollback, reconnect inheritance or claim that an uncertain
write was not delivered. Credit reads have bounded waits in addition to caller deadlines.

Before qualification execute actual pinned mTLS/public APIs at the three declared
scales, exact rows/sums and at least one update, slow/zero/exhausted/spoofed credit,
source/consumer revocation, cancellation before capture and while awaiting credit,
oversized frames, EOF and new-baseline recovery. Then preregister and execute equal
compressed HTTP/alternative-format comparisons with CPU/allocation/peak boundaries.
No efficiency or billing gain follows from this design; D20-04 remains IN_PROGRESS.

Executed local evidence: 22 actual pinned mTLS/public-helper cases pass, including
both encodings at100/1000/100000 rows with exact ID/sum oracle, one bound update,
zero/spoofed/replayed credit, denial/cancellation, timeout, oversized source row and
partial EOF/fresh-baseline recovery. The first reconnect attempt failed the strict
mutual-authentication check on a resumed TLS connection. The fixture now explicitly
disables TLS resumption to exercise fresh independent authentication; production
selection was not relaxed. Resumed-session and general NIP delegation remain unqualified.
212 combined schema/receiver/session/transfer cases pass. Final full regression and
comparative CPU/allocation/peak evidence are recorded separately after execution.

The independently preregistered [transfer methodology](../benchmarks/ncp-transfer-method-2026-11-02.md)
([中文](../benchmarks/ncp-transfer-method-2026-11-02.cn.md)) now has a72-condition
real HTTP/native capture fixture. Initial one-iteration correctness smoke passed72/72;
formal Release30-sample resource evidence is still pending at this implementation
checkpoint. The driver retains failures and process-tree memory guards. Complete
schema replacement is measured as full resynchronization, without an unmeasured
live-delta performance claim.

Qualification update: measured42a0b05 now passes all72conditions/2160samples plus216warmups
with320.12109375MiB observed whole-actual-tree peak. All24native medians are higher than
matched gzip HTTP in wall/CPU/allocation/wire bytes; no efficiency gain is claimed.
See the [complete observation](../benchmarks/ncp-transfer-observation-2026-11-02.md),
[AC record](../plans/2026-11-02-ncp-reference-ac-evidence.md) and
[scope decision](2026-11-02-ncp-qualified-reference-scope.md). Earlier failed attempts remain
failed at their own source/host configurations. General NIP/NWP/SDK/release gates stay.
