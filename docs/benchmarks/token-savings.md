# Cognon Savings Benchmark

Measures Cognon (CGN) consumption for representative Agent↔Node
interactions, comparing an idiomatic REST implementation against the
equivalent NWP traffic.

**Counter**: `ceil(UTF-8 bytes / 4)` per `spec/token-budget.md §2.2` —
the tokenizer-agnostic baseline. Both protocols are measured with the
same ruler, so the ratio is an apples-to-apples comparison.

**Target** (Phase 1 exit criterion): ≥ 30% CGN savings across scenarios.

## Results

| Scenario | Calls | REST CGN | NWP Anchor (once) | NWP steady-state | NWP total | Savings |
| --- | ---:| ---:| ---:| ---:| ---:| ---:|
| S1 — Memory Node: list top 10 products | 10 | 7,040 | 78 | 3,930 | 4,008 | **43.1 %** |
| S2 — Action Node: create order (3 items) | 10 | 3,690 | 146 | 1,920 | 2,066 | **44.0 %** |
| S3 — Complex Node: order → customer → products graph | 5 | 1,890 | 100 | 765 | 865 | **54.2 %** |
| **Aggregate** | — | **12,620** | — | — | **6,939** | **45.0 %** |

## Scenarios

### S1 — Memory Node: list top 10 products

Ten sequential reads of the same 10-row product list. REST re-sends JSON keys and schema-carrying envelopes every time; NWP amortises a single AnchorFrame across the session and returns rows as positional arrays referenced by anchor_ref.

- Calls: 10
- REST CGN: 7,040
- NWP CGN (one-shot 78 + steady 3930): 4,008
- Savings: **43.1 %**

### S2 — Action Node: create order (3 items)

Ten order-creation invocations. REST uses verbose field names on both request and response; NWP ActionFrame sends positional arrays against an amortised params+result schema AnchorFrame.

- Calls: 10
- REST CGN: 3,690
- NWP CGN (one-shot 146 + steady 1920): 2,066
- Savings: **44.0 %**

### S3 — Complex Node: order → customer → products graph

Five traversals of an order → customer + items.products graph. REST requires four separate fetches per traversal; NWP Complex Node returns the joined graph as a single capsule referencing a shared schema bundle.

- Calls: 5
- REST CGN: 1,890
- NWP CGN (one-shot 100 + steady 765): 865
- Savings: **54.2 %**

## Reproducibility

```bash
dotnet run --project impl/dotnet/benchmarks/NPS.Benchmarks.TokenSavings -- --emit
```

Fixtures live in `Scenarios.cs` and are intentionally deterministic so
re-runs produce byte-identical output.
