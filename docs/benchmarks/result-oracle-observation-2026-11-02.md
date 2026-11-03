English | [中文](result-oracle-observation-2026-11-02.cn.md)

# Frozen refund oracle through authenticated result operations

Date: 2026-11-02. Scope: D20-02/D20-03 reference workflow, deterministic runtime.
Method: [frozen methodology](../../spec/conformance/alpha20/data_workflow_methodology.json).

`FrozenRefundWorkflowMatchesExactOracleThroughSupportedPublicClient` executes the
frozen 100/1000/100000-row datasets through `ResultHttpClient`, actual public mTLS,
fresh gateway projections and the independently verified receiver. Source capture
uses an immutable seeded dataset in stable record-ID order and an actual schema
anchor computed by `AnchorFrameCache`; the only source query subset registered in
this fixture is `filter: {refunded: true}`. This does not register a production source
or expand the protocol's general query grammar.

The workflow captures all orders and separately captures refunded orders. It obtains
exact COUNT by reason/period from the first result and COUNT/Int64 SUM from the second,
then reads a bounded refund page for supporting record IDs. Every integer and the first
five current refund IDs per reason must equal the frozen oracle. Exact rational anomaly
evaluation identifies delivery at all three scales; rounding cannot change detection.

At 100000 rows the asserted source oracle is:

| Reason | Baseline orders / refunds / refund amount | Current orders / refunds / refund amount |
| --- | --- | --- |
| delivery | 13000 / 3000 / 3444000 | 12000 / 7000 / 23576000 |
| quality | 13000 / 3000 / 3555000 | 12000 / 0 / 0 |
| duplicate | 12000 / 2000 / 2296000 | 13000 / 0 / 0 |
| other | 12000 / 2000 / 2370000 | 13000 / 0 / 0 |

Amounts use integer minor units. Delivery's supporting IDs are `order:000052`,
`order:000056`, `order:000060`, `order:000064`, `order:000068`. Model-bound evidence
assembled from the actual aggregate responses, up to five rows and result/snapshot/
record citations stays within 32768 UTF-8 bytes. Full source rows are not sent to a model.
The two captures have distinct snapshot IDs and the same schema. Deliberately substituting
one snapshot ID into the other result produces the canonical HTTP 409 snapshot-mismatch
error. Both results are explicitly released, with exactly two source captures.

Run:

```sh
dotnet test impl/dotnet/tests/NPS.Tests/NPS.Tests.csproj --filter FullyQualifiedName~FrozenRefundWorkflowMatchesExactOracle
```

These three correctness executions are not the 30-repeat transport experiment or five-repeat
provider experiment. No provider call, token/cost saving, latency/RSS distribution, C/D transport
comparison, native/general-numeric qualification or all-SDK claim follows. They establish that
the real bounded reference path computes the answer before external-model egress. Existing
security/release gates and the shared API budget remain unchanged.
