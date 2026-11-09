English | [中文](alpha20-support-2026-11-03.cn.md)

# Current alpha.20 support and retained gates

D20-08 is COMPLETE for the accepted bounded experimental scope. The released suite/packages remain alpha.19; source-bound
alpha.20 development packages are local verification artifacts. No runtime profile
is enabled simply by importing an SDK. The [machine support ledger](2026-11-03-alpha20-support-ledger.json)
and [gate reconciliation](2026-11-03-alpha20-gate-reconciliation.json) supplement the
original12-artifact freeze, which remains unchanged.

| Surface | Executed evidence | Limits |
|---|---|---|
| Feature/error helpers | Six SDK shared parser and mapping tests | No complete negotiation, admission or transport/profile qualification |
| Context bytes/admission | .NET/TypeScript receiver helpers; Python/Go byte tests | Independent channel/policy and route integration still mandatory; Java/Rust context parity unqualified |
| Durable replay | .NET/TypeScript single-host SQLite references | No multi-host shared store, rollback proof or business idempotency |
| Result HTTP | .NET/Python public clients to .NET reference server | Native result binding and other SDK result paths unsupported |
| Schema batches | .NET opt-in native reference, paired measurements | Other SDKs unsupported; measured transfer was worse than matched HTTPgzip; no universal savings claim |
| NOP result handoff | Four local .NET worker roles and fault/restart evidence | No remote/all-SDK production runtime profile |
| NDP result routing | Two-node .NET volatile admission, fresh host authority checks | No durable discovery/full production runtime qualification |

[Standalone SDK/package evidence](runs/sdk-isolation-a9e4f05/README.md) records
1538 SDK-only .NET tests,1513 Python tests/92.13% coverage,1324 TypeScript tests,
Go package tests,744 Java and765 Rust tests. The128 daemon-owned .NET tests excluded
by the established standalone boundary are covered separately by full authoring CI;
they are not silently skipped as standalone SDK passes. Packaged npm/Python/NuGet/
Java/Go consumer evidence is separate from source tests. Rust's full candidate gate
and correction CI results must be read from the current packet, not inferred.

The user accepted [bounded experimental closeout](../adr/2026-11-03-alpha20-experimental-closeout.md)
on2026-11-03 and instructed local CI. The old576-cell whole-production matrix remains
unexecuted/deferred under133. Original12alpha/57P20 IDs retain whole-obligation
completion=false, with explicit current-scope adaptation/deferral/release dispositions.
Current runtime acceptance uses the frozen seven configurations, not codec counts.
Universal30%CGN/50%wire experimental thresholds are replaced by exact correctness,
resource bounds and fair recorded comparisons; historical thresholds are not achieved.
Beta/adopter/full-production obligations remain future/separate. Existing156/157/158
remain open; only the separate ResultHttpClient path is qualified.

[Local CI evidence](runs/experimental-closeout-0c4bfdf/README.md) passes current security/privacy/provenance/
regression/package gates for scoped D08 closure. No GitHub Actions or remote secrets are
required. Historical remote scanner passes/failures remain evidence, not the current
execution authority. The credential file recheck found no identified Cargo token;
this does not block local Rust verification. Actual publishing authority, Maven
signing, SourceLink, finalversion20/publicprojection coherence and publication stay134.
The closed local receipt distinguishes original full execution from its offline-only argument correction; it never grants a ready-tag designation.
