# NPS Tier-2 MsgPack Wire-Size Benchmark

Compares Tier-1 JSON (`Tier1JsonCodec`) against Tier-2 MsgPack
(`Tier2MsgPackCodec`) payload sizes for representative NPS frames.
Both codecs serialise the **same** `IFrame` instance, so the ratio is
an apples-to-apples payload-only comparison (no header, no compression).

**Target** (Phase 2 exit criterion, `spec/NPS-Roadmap.md`): Tier-2 body ≤
50 % of Tier-1 JSON — i.e. **≥ 50 % reduction**.

## Results

| Scenario | Tier-1 JSON (bytes) | Tier-2 MsgPack (bytes) | Ratio (Tier-2 / Tier-1) | Reduction |
| --- | ---:| ---:| ---:| ---:|
| S1 — AnchorFrame (11-field product schema) | 1,040 | 774 | 74.4 % | **25.6 %** |
| S2 — CapsFrame (10 product rows, positional) | 1,538 | 179 | 11.6 % | **88.4 %** |
| S3 — ActionFrame (orders.create with 3 line items) | 455 | 154 | 33.8 % | **66.2 %** |
| S4 — CapsFrame (orders.create response) | 407 | 155 | 38.1 % | **61.9 %** |
| S5 — CapsFrame (order→customer+products graph) | 455 | 156 | 34.3 % | **65.7 %** |
| **Aggregate** | **3,895** | **1,418** | **36.4 %** | **63.6 %** |

## Scenarios

### S1 — AnchorFrame (11-field product schema)

Schema anchor carrying 11 field descriptors with semantic annotations. Transmitted once per session; subsequent frames reference by anchor_id only.

- Tier-1 JSON:    1,040 bytes
- Tier-2 MsgPack: 774 bytes
- Ratio:          74.4 %   (**25.6 % reduction**)

### S2 — CapsFrame (10 product rows, positional)

Memory Node capsule: 10 product rows encoded as positional arrays, referencing the anchor from S1. Representative of the steady-state response shape once the schema has been pinned.

- Tier-1 JSON:    1,538 bytes
- Tier-2 MsgPack: 179 bytes
- Ratio:          11.6 %   (**88.4 % reduction**)

### S3 — ActionFrame (orders.create with 3 line items)

Action Node request for a compact order invocation. Positional item tuples + address/payment arrays against a bundled params schema.

- Tier-1 JSON:    455 bytes
- Tier-2 MsgPack: 154 bytes
- Ratio:          33.8 %   (**66.2 % reduction**)

### S4 — CapsFrame (orders.create response)

Action Node response capsule: confirmed order with line totals, cost breakdown, and shipping address as positional arrays.

- Tier-1 JSON:    407 bytes
- Tier-2 MsgPack: 155 bytes
- Ratio:          38.1 %   (**61.9 % reduction**)

### S5 — CapsFrame (order→customer+products graph)

Complex Node capsule returning a joined order/customer/product graph as nested positional arrays + a keyed product dictionary.

- Tier-1 JSON:    455 bytes
- Tier-2 MsgPack: 156 bytes
- Ratio:          34.3 %   (**65.7 % reduction**)

## Reproducibility

```bash
dotnet run --project impl/dotnet/benchmarks/NPS.Benchmarks.WireSize -- --emit
```

Fixtures live in `Scenarios.cs` and are intentionally deterministic so
re-runs produce byte-identical output. Regression tests in
`NPS.Tests/Benchmarks/WireSizeRegressionTests.cs` lock the aggregate and
per-scenario reductions against the Phase 2 target.
