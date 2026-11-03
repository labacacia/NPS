English | [中文](ncp-transfer-method-2026-11-02.cn.md)

# Preregistered schema-batch transfer comparison

Date: 2026-11-02; D20-04 #168. Preregister before formal execution. This measures
complete outside-model data transfer, not model context/billing or the earlier HTTP
aggregate workflow. No winning protocol, percentage or resource saving is assumed.

## Controlled task and arms

Use the D20-01 seed-20261102 order/refund values at100/1000/100000 rows. Every arm
returns every row, field and exact minor-unit amount in the same order. The transport
schema uses int32 for these known bounded amounts and string/string/string/bool for
id/period/reason/refunded; this is not the legacy result store's int64 schema hash.
An independent oracle verifies exact rows, IDs, counts, sums and final schema.

Six variants: ordinary HTTPS JSON object rows (identity/gzip), ordinary HTTPS generic
MessagePack object rows (identity/gzip), selected native NCP tuple streams (JSON/MessagePack).
HTTP bodies contain the same complete schema plus rows; native includes Anchor, all
Stream envelopes, request/admission and reverse credit. Generic MessagePack uses the
existing library and is an alternative representation, not Arrow/Flight/Protobuf.
Native gzip is not invented. Compare actual compressed HTTP, not only raw JSON length.

Both services use one configured synthetic source and independently pinned mutual TLS,
fresh authentication with TLS resumption disabled, current host peer/source checks and
the same immutable source values. Native uses the public session/publisher/reader and
an existing service-owned read-only ActionFrame to trigger each snapshot; it does not
reinterpret Nop/Caps or introduce a new protocol operation. HTTP executes the equivalent
known fixture read command. Both include request bytes/time. Other SDKs and general
NIP/PKI/NWP result subscriptions remain unqualified by this fixture.

## Matrix and boundaries

3 scales × 6 variants × cold/warm connection × stable/schema-churn task =72 conditions.
Stable transfers one snapshot. Churn transfers the original snapshot then a fresh complete
snapshot with one added nullable note field; old state stays immutable. New epoch/schema
must be bound and exact final source equality verified. This measures full resynchronization,
not live mutation/delta superiority. Delta correctness/fault evidence remains separate;
no delta savings claim is advertised without an equivalent selective-update baseline.

Each condition has3 discarded warmups and30 measured repetitions in an isolated test
process. Cold opens/disposes its frontend each repetition and includes TCP/TLS/native
preamble/Hello/Caps; warm reuses admitted/persistent connections, with setup outside the
timed boundary for all arms. Source generation/certificate/server setup are untimed.
Actual response encoding/compression, object-to-tuple projection, canonical schema hashing,
all request/response/control I/O, decode/retention and cold frontend shutdown are timed.
Every phase still runs its exact oracle. Changing source variant itself is prepared equally
outside timing; its full changed-schema transfer is inside.

Measure wall latency, co-host process CPU delta and managed total-allocated-byte delta
per repetition, including counter overhead. CPU/allocation include client/server/TLS/GC/
serialization and are not attributed to a protocol subsystem. Byte counters below TLS
include both directions, handshake/HTTP headers/NCP framing and credits, excluding TCP/IP.
Record actual process-tree peak RSS over the isolated condition; retained tuple bytes are
a separate logical quantity. No forced GC or pooled-cache reset is introduced.

Report all samples and median/p95, wire bytes, allocation and peak metadata. Formal profile:
4 logical processors,320MiB managed heap policy,512MiB process-tree RSS ceiling and120s
per workflow. A failure/timeout/OOM/missing sample remains failed/unknown evidence; never
drop it to create a gain. No provider calls; model-visible bytes/usage are not fabricated.

## Evidence and acceptance

Commit methodology/capture code before a formal run. Hash exact code/method/oracle revisions,
record runtime/OS/configuration and executed test results. Require all72 unique conditions,
30 samples each, source/schema/row equality, declared connection semantics and finite
nonnegative resource samples. Historical incomplete/failed runs remain separate artifacts.
Do not infer native results from earlier two-hop HTTP timings or combine model replay
usage with these transport numbers. Equal/worse/small-payload/churn regressions remain
visible and trigger scope/design review before broad ports. Existing alpha/beta/P20 and
security/registry gates are not changed by this experiment.

## Capture implementation details (before formal execution)

`tools/benchmarks/ncp-transfer-runtime.py OUTPUT` builds Release from a clean committed
revision, hashes the source and actual test assembly, then invokes `dotnet vstest` once
per condition. This avoids an unnecessary outer MSBuild runner while retaining the
entire executed test-platform/testhost process tree in the memory boundary. `/proc`
RSS is sampled every10ms; report the observed concurrent peak and a conservative sum
of live per-process VmHWM high-water marks. Both must fit512MiB. The sampled peak is
not an assertion of continuous kernel-level observation or per-protocol attribution.
Cold shutdown refers to the client frontend; background server cleanup can overlap
subsequent scheduling. Warm connections perform one complete setup read outside timing
for every arm, in addition to the three discarded warmups. HTTP gzip uses the platform
GZipStream Fastest policy; MessagePack uses the existing NPS JsonElement library formatter.
The native fixture's read-only NWP Action request uses the existing .NET codec; portable
schema-batch response bytes do not qualify other SDKs' legacy NWP request mappings.
Partial successful samples and process failures remain in the capture directory.

## Memory correction and matched TCP profile (before second formal attempt)

The first `a868c02` attempt is retained in
[runs/2026-11-02-ncp-transfer-first-attempt.json](runs/2026-11-02-ncp-transfer-first-attempt.json).
Of 66 completed conditions, 49 passed and 17 failed: all 16 HTTP 100,000-row cases failed (15 observed
OutOfMemoryException, one HTTP 500 with unproven cause), and native JSON cold schema-churn completed 30 samples but exceeded
RSS. One later condition was interrupted; five were unrun. This attempt does not qualify
resource efficiency; missing/interrupted samples remain unknown. It was stopped after
both failures were established to correct the fixture before repeating the full matrix.

The correction keeps source DOMs in explicitly owned/disposed documents with bounded
fixture buffer estimates, removes large shared-pool serializer staging from source construction
and response encoding, explicitly releases consumed prior-iteration snapshot references,
and decodes ordinary JSON/MessagePack directly into named record DTOs with their
established library decoders instead of retaining/cloning a whole-response JSON DOM. Every source/retained-old-snapshot
oracle and 320 MiB / 512 MiB / 120 s limit remains. There is no forced collection, pool reset,
preencoded response or sample exclusion. Invoke the installed SDK's same vstest.console.dll
directly to remove the redundant CLI launcher; the remaining entire actual process tree
still counts, including testhost. Runner/version/assembly hashes are recorded.

The first fixture explicitly requested TCP no-delay for HTTP while native left platform
settings unchanged. Latency cannot isolate a protocol cause. The repeat explicitly
requests NoDelay on both native endpoints and HTTP client/server, without changing any
production transport default. Capture guards require this declared setting. Treat the
repeat as its own configuration/revision; do not replace the old latency/RSS values or
attribute differences between attempts solely to codec changes. All 72 conditions are
required again, with the same 3-discarded / 30-measured plan and independent oracles.

## Shared immutable source correction (before third formal attempt)

The second `fd4aa11` attempt is retained in
[runs/2026-11-02-ncp-transfer-second-attempt.json](runs/2026-11-02-ncp-transfer-second-attempt.json):
57 completed conditions, 53 passes and 4 failures (two RSS-only failures after complete
semantic samples, two observed OOMs). One subsequent condition was interrupted and
14 unrun. No completed-matrix or resource-efficiency claim follows from either attempt.
The source fixture still retained two large JSON DOMs in every condition, competing
with decoded immutable snapshots and response buffers under the co-host memory policy.

Prepare immutable named records with shared unchanged string values for both variants,
not retained JSON DOMs. The original row DTO has exactly five fields and the changed
one six, including explicit nullable note. Existing JSON/MessagePack library map encoders
still encode actual responses inside timing; native named-object-to-tuple projection
also remains inside timing. No preencoded body, custom binary algorithm, GC/pool reset,
limit increase or omitted old-snapshot check. The independent source/schema/row oracle
is unchanged. Each capture now binds the actual loaded test-assembly file hash and
explicit source-storage revision; the verifier rejects mixed assemblies/configurations.
All 72 conditions must be repeated with the same warmup/repeat/TCP/resource rules.

## Direct asserted executor and streaming HTTP (before fourth formal attempt)

The third `775d2cf` test-platform attempt is retained in
[runs/2026-11-02-ncp-transfer-third-attempt.json](runs/2026-11-02-ncp-transfer-third-attempt.json):
65 completed conditions, 59 passes and 6 RSS-only failures; every completed condition
finished all semantic assertions and30samples. One later condition was interrupted,
six unrun. The source-record correction did not qualify that whole-process-tree profile.

The installed test SDK already emits an executable test assembly. Replace only its
placeholder Main through the supported GenerateProgramFile setting. An explicit
`--case` entry invokes the exact same fixture/assertions once, with no VSTest parent;
ordinary VSTest discovery/regression remains. A completion receipt binds one executed/
passed fixture, its actual assembly hash and exact raw-record hash. It is an executor
receipt, not a fabricated TRX or general qualification result. The driver counts the
whole actual direct process tree with the same512MiB ceiling. Earlier platform-inclusive
RSS values remain failed and are not reclassified. No inference compares the two
harness configurations as a protocol-specific memory saving.

An initial uncommitted direct diagnostic still observed an HTTP failure. Ordinary HTTP
now uses the existing libraries' stream encoding/decoding APIs and streaming GZipStream,
with ResponseHeadersRead; it retains the same complete named records without duplicate
whole encoded/compressed/HttpContent buffer arrays. Encoding,compression,all response
reading/decoding/retention and actual HTTP/1.1 chunk framing remain measured; the below-TLS
counter includes that framing. The task still returns every row, not a selective or lazy
result. Native code,immutable source,oracles,TCP settings,limits and repeat counts stay.
Each raw record and receipt must identify direct_fixture, and all72conditions run anew.
