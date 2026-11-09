English | [中文](2026-11-02-ncp-reference-ac-evidence.cn.md)

# D20-04 reference AC evidence

Trace: D20-04 #168 / Epic #163. Status: **COMPLETE for the experimental .NET reference
scope below**. Prerequisites D20-00/D20-01 are COMPLETE. This is an AC decision;
stacked integration, six-SDK qualification and publication remain separate.

## Acceptance and evidence

| AC | Result | Executed evidence |
| --- | --- | --- |
| AC1 Actual representation, schema and negotiated revision | COMPLETE | Original object-row codecs demonstrably retain names. Explicit opt-in schema_batches=1 uses canonical JSON/MessagePack positional rows, ordered materialized schema/JCS SHA256 and strict bounded primitives. 106 fixed wire cases plus 10 independently authored Unicode/JCS schemas execute directly; registry 0.3 is a constrained additive successor with original 12 result predecessors retained. |
| AC2 Anchor/schema/base/sequence and resynchronization | COMPLETE | Missing/stale/wrong hash, duplicate/gapped/reordered chunks, replay, wrong epoch/base, expiry, typed/null/atomic Diff and terminal-state cases pass; actual complete baseline/update preserves prior snapshot. Fresh authorized baseline/session recovers after partial EOF. Schema churn binds a fresh full schema and epoch; no automatic fallback or delta-efficiency claim. |
| AC3 Bounded actual transfer and host source policy | COMPLETE | 22 actual independent-pin mTLS/public publisher/reader flows cover both encodings at100/1000/100000 rows, exact IDs/sums, source/consumer denial, pre-cancel, waiting-credit cancellation/revocation, zero/pause/resume/exhausted/spoofed/replayed/missing credit, timeout, oversized source row and EOF/recovery. 212 combined schema/session/transfer regressions pass at010781b. Callbacks bind the actual peer/source and remain host-owned authority. |
| AC4 Equivalent compressed/alternative transport and resources | COMPLETE | Preregistered method and corrections precede measured42a0b05. All72conditions ×30samples +3warmups, exact independent source/schema/row checks, actual bidirectional below-TLS bytes, co-host CPU/allocation and whole actual process-tree RSS pass. Peak/guard320.12109375MiB within512MiB with4CPU/320MiB-managed/120s policy. All24native-vs-matched-gzip medians are higher on all4metrics; small/churn negatives and all failed attempts remain visible. |
| AC5 EN/CN contract and baseline compatibility | COMPLETE | Paired schema-batch profile, selected canonical encoding, negotiation/recovery and benchmark documents exist. No unselected codec/default-off feature offer is changed. Original result freeze and106-case manifest bytes unchanged. Feature/version/contract and full.NET1597/1597 checks pass at measured source; source CI41542003619 succeeds. Broader compatibility cells remain D20-08. |
| AC6 Truthful claims and decision before broad ports | COMPLETE | Scope ADR explicitly retains experimental reference semantics with no compressed superiority, universal memory, delta or token claim. Actual raw files/receipts/source index and all distributions are published; standalone verifier rejects mixed/missing/faked/budget-failed evidence. Default offers, old savings/security/registry gates and publishing stay unchanged. |

## Reviewable source and reproduction

- #188/aef0d0c receiver; #189/ceda186 exact selected mTLS/session binding;
  #190/92f22aa counted compact row ownership; #191/a200679 additive registry successor.
- #192/864069e canonical selected codecs/portable cases; #193/936c29a strict JCS;
  #194/010781b actual bounded publisher/reader and22flows.
- #195/a868c02 initial preregistered capture; #196/fd4aa11 memory/configuration correction;
  #197/775d2cf shared immutable source; #198/42a0b05 direct asserted executor/streaming HTTP.
- Measured exact revision: `42a0b05865d4206b612cc55f2e80f2505cb6ea67`.
  Test assembly SHA256: `04045b765914280fad9c70f63b3d8db1a5ac23dfb2a80e7e3b6adf7dfc969b3a`.
- [Observation/limits](../benchmarks/ncp-transfer-observation-2026-11-02.md),
  [method](../benchmarks/ncp-transfer-method-2026-11-02.md),
  [complete packet](../benchmarks/runs/2026-11-02-ncp-transfer-qualified.json),
  [all72median/p95 rows](../benchmarks/runs/2026-11-02-ncp-transfer-summary.csv),
  [scope decision](../adr/2026-11-02-ncp-qualified-reference-scope.md).

```sh
python3 tools/benchmarks/ncp-transfer-runtime.py --verify-packet docs/benchmarks/runs/2026-11-02-ncp-transfer-qualified.json
python3 -m unittest discover -s tools/benchmarks/tests -p 'test_ncp_transfer*.py'
python3 tools/conformance/check-schema-batch-vectors.py
python3 tools/conformance/check-data-workflow-contract.py
```

## Remaining suite obligations

Provisioned pins/current callbacks qualify one .NET source/consumer reference, not
general PKI/NIP delegation or arbitrary production source policy. The fixture's
read-only NWP Action request is service-owned; native result operations, resumed peer
proof, NWP result subscriptions, live schema/delta efficiency and all-SDK public API
cells are unqualified here. Resource evidence covers the declared5/6-field dataset,
not every allowed schema. Allocated bytes/logical tuple bytes/RSS are distinct.

D20-05..08 and old P20 tasks remain their own work. Required dependency advisory and
registry credential inventory remain failed/unwaived; no merge, release projection,
publication, beta readiness or parent completion follows from this child. No paid
calls or shared provider-ledger changes occurred in this work.

Next: agent starts authorized D20-05 #169, builds the self-contained .NET/Python
bounded-result tool demo and verifies source references/drill-down/live tool behavior
against ordinary HTTP under the existing shared spend ledger. No further approval
is required to implement that already-authorized next child once this AC record is
committed and the canonical tracker is reconciled.
