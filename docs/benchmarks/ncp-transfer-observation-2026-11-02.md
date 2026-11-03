English | [中文](ncp-transfer-observation-2026-11-02.cn.md)

# Observed native schema-batch and streaming HTTP transfer

Date: 2026-11-02; D20-04 #168, Epic #163. Measured source
`42a0b05865d4206b612cc55f2e80f2505cb6ea67`, Release/Linux x64/.NET 10.0.14.

The native .NET reference has higher median latency, process CPU, allocation and
TLS-stream bytes than the same-encoding HTTP gzip baseline in **all 24 paired
conditions**, including small payloads and schema churn. All 72 conditions pass exact
source/schema/row and resource checks: 2,160 measured workflows plus 216 discarded
warmups, 30 samples each. Observed whole-process-tree peak and conservative live
high-water guard are **320.12109375 MiB**, within the unchanged 512 MiB ceiling.
No model/provider calls occurred; model usage and model-visible bytes remain unknown.

## Complete transfer comparison

The [preregistered method and successive corrections](ncp-transfer-method-2026-11-02.md)
cover six actual mTLS variants at 100/1,000/100,000 rows, cold/warm connections and
original/changed-schema tasks. Every row, ID, primitive value, sum and schema matches
an independent D20-01 oracle. Churn transfers two complete snapshots and checks the
retained original, fresh schema and epoch; it does not measure delta efficiency.

At 100,000 rows, warm connection, original schema:

| Path | Wall median / p95 ms | CPU median / p95 ms | Allocated MiB median | TLS-stream MiB median |
| --- | ---: | ---: | ---: | ---: |
| HTTP JSON identity | 223.62 / 544.88 | 286.76 / 370.73 | 30.22 | 9.3672 |
| HTTP JSON gzip | 274.07 / 567.18 | 292.60 / 529.42 | 30.05 | 0.4002 |
| HTTP MessagePack identity | 124.55 / 169.18 | 157.46 / 242.75 | 24.18 | 7.0735 |
| HTTP MessagePack gzip | 107.41 / 123.04 | 151.41 / 224.41 | 24.09 | 0.3639 |
| Native NCP JSON tuples | 540.65 / 586.55 | 745.30 / 823.83 | 187.31 | 4.6617 |
| Native NCP MessagePack tuples | 594.39 / 622.00 | 849.48 / 929.67 | 239.14 | 3.3394 |

Relative to uncompressed same-encoding HTTP object rows, native tuple wire bytes are
50.23% lower for JSON and 52.79% lower for MessagePack in this one condition. Ordinary
gzip removes much more repeated structure here. This is not a demonstrated advantage
over a compressed baseline or a model-billing saving. The MessagePack arm is the
existing library's ordinary named maps; Arrow/Flight/Protobuf were not evaluated.

Small payload and full schema-resynchronization examples, all warm connections:

| Rows / task | HTTP JSON gzip / native JSON median ms | HTTP MessagePack gzip / native MessagePack median ms |
| --- | ---: | ---: |
| 100 / original | 1.37 / 2.56 | 1.18 / 3.08 |
| 1,000 / original | 6.07 / 22.92 | 4.47 / 27.02 |
| 100,000 / schema churn | 545.49 / 1076.09 | 260.46 / 1206.04 |

No conditions or first samples were removed. The
[full 72-condition median/p95 CSV](runs/2026-11-02-ncp-transfer-summary.csv),
[packet and source index](runs/2026-11-02-ncp-transfer-qualified.json), and linked
original raw files/receipts retain all distributions, wire bytes and allocation values.

## Boundaries and reproduction

Counters cover actual client/server work in one direct fixture executable, including
TLS, request/response encoding, native projection/strict validation, compression, GC
and client cold shutdown. Process CPU can exceed wall time. Managed total allocation
is separate from retained tuple bytes and RSS; it is not a per-protocol memory measure.
Both-direction counters below TLS include HTTP chunk framing, native handshakes/Anchor/
Stream/credit and TLS records, excluding TCP/IP packet overhead. Warm setup performs
one complete read outside timing, followed by three discarded warmups. Cold includes
connection/admission. Server/certificate/source creation is outside workflow timing,
inside observed process-tree lifetime. No forced GC or pool reset occurs.

Native public session/publisher/reader handles a provisioned source with current host
peer/source callbacks. Ordinary HTTP uses typed named-record decoders and standard
streaming serializers. These are implementation/deployment observations; they do not
isolate a codec cause. Independent pins/fresh full mTLS are not general NIP/PKI,
resumed-session or NWP result-stream qualification. Native has no invented gzip tier.

```sh
# Offline verification of published original raw bytes, receipts and every condition
python3 tools/benchmarks/ncp-transfer-runtime.py --verify-packet docs/benchmarks/runs/2026-11-02-ncp-transfer-qualified.json
python3 -m unittest discover -s tools/benchmarks/tests -p 'test_ncp_transfer*.py'
# At exact measured source, with .NET 10 dependencies restored, use a new output path
python3 tools/benchmarks/ncp-transfer-runtime.py /absolute/path/new-capture
python3 tools/benchmarks/ncp-transfer-runtime.py /absolute/path/new-capture --verify
```

The explicit fixture executor calls the same assertions as normal VSTest; the latter
still passes 1,597/1,597 at measured source. A completion receipt binds one declared
condition, actual assembly hash and exact raw-record hash. It is not a fabricated TRX
or a general SDK support certificate. Whole actual process-tree memory is counted.

## Retained attempts and decision

| Source | Complete conditions / passes / failures | Interrupted / unrun | Observed failure |
| --- | --- | --- | --- |
| a868c02 | 66 / 49 / 17 | 1 / 5 | 15 HTTP OOM; one HTTP 500 cause unproven; one native churn RSS failure |
| fd4aa11 | 57 / 53 / 4 | 1 / 14 | Two OOM; two RSS failures |
| 775d2cf | 65 / 59 / 6 | 1 / 6 | Six RSS failures after complete semantic samples |

The [first](runs/2026-11-02-ncp-transfer-first-attempt.json),
[second](runs/2026-11-02-ncp-transfer-second-attempt.json), and
[third](runs/2026-11-02-ncp-transfer-third-attempt.json) retain complete/partial data and
unrun/interrupted states. Source, buffering, TCP and executor corrections were recorded
before each new formal attempt. Host configurations differ; earlier RSS failures are
not reclassified and cross-attempt differences are not protocol-specific savings.
Uncommitted diagnostics are exploratory and are not mixed into this qualifying run.

The [scope decision](../adr/2026-11-02-ncp-qualified-reference-scope.md) retains the
experimental bounded reference for schema/epoch/base/credit/recovery semantics, with
no compressed-transfer superiority or token claim. D20-05 proceeds on public bounded
result/tool operations; broader SDK qualification remains D20-08. Default feature
offers, the old 30%/50% obligations, security/registry gates and publication remain
unchanged. [D20-04 AC evidence](../plans/2026-11-02-ncp-reference-ac-evidence.md).
