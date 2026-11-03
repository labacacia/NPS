English | [中文](result-invariant-observation-2026-11-02.cn.md)

# Result invariant HTTP execution

Date: 2026-11-02. Trace: D20-02 #166 / D20-03 #167. This records correctness and
rejection ordering, not latency, native NCP qualification or provider billing.

`ResultFrozenInvariantHttpTests` reads all 39 unchanged cases from the frozen
[semantic corpus](../../spec/conformance/alpha20/data_result_vectors.json), checks
their encoded input bytes and asserts the recorded outcome and actual ExecuteAsync
entry-count delta. Setup requests are excluded from that delta. The test uses real
ASP.NET HTTP middleware, result store and live resource policy, with explicitly
synthetic server-owned authentication/digest fixtures. Input authority flags are
never deserialized as permissions or sent as trusted caller assertions.

| Cases | Actual server setup / HTTP observation |
| --- | --- |
| F01–F07 | Missing/wrong feature, disabled service, withdrawn route, unselected projection, unsigned Agent header and unavailable verifier reject without dispatch. |
| F08–F16 | Owner page, another tenant/subject, withdrawn source, exact expiry and one second before expiry, release, replacement empty store and distinct unproven successor issuer. |
| F17–F23 | Changed snapshot/schema, cursor rebound to a different field selection, limits 0/1001/boolean, and a captured single row larger than the page budget. |
| F24–F29 | Removed field permission, service model-egress denial/allowance, exact recipient grant, revocation and rejected recursive grant followed by unauthorized third-party HTTP read. |
| F30–F31 | Unauthorized descriptor/preview disclosure through read, and unsupported `result.execute_sql` with canonical 422. |
| F32–F39 | Actual 16-result owner capacity, create, changed-content same key, exact COUNT, owner release, unauthorized mismatched/expired read, repeated authorized release. |

The corpus is a semantic oracle, not serialized ActionFrames. Its conceptual
`result.describe` (F30) maps to the descriptor/preview surface of `result.read`;
there is no fifth wire action. Its expiry instant 1000 maps to the actual store's
300-second TTL (advance 300 or 299 seconds), preserving exclusive expiry. Restart
cases replace the actual host/store rather than toggling a request's issuer flag.
Every denial also checks that its response omits the retained locator/snapshot,
protected field name and row-count metadata. Success cases inspect actual rows,
count totals and release receipts.

An initial actual mTLS regression reproduced unauthorized read entering ExecuteAsync
before rejection. Admission now preflights live resource policy, bindings and known
capacity without capture, release, reservation or replay consumption. Execution keeps
its live/atomic checks. Separate real Kestrel mTLS/Ed25519/SQLite tests observe zero
dispatch on resource and snapshot denial, and deny a signed recipient read, issue a
legitimate grant, then accept the **same proof**. This establishes that that policy
denial did not consume the projection ID. It does not authorize generic gateways or
production PKI. See the [binding ADR](../adr/2026-11-02-result-invocation-http-binding.md)
and [host instructions](../../impl/dotnet/src/NPS.NIP/Invocation/README.md).

The selected HTTP/result/client/gateway/middleware tests plus the existing native
anchor regression passed 143 tests. The final full suite passed all 1282 tests.
The initial full run had 1239/1240 passing and an existing native anchor timing
failure (1264 ms); its isolated rerun passed without changing the timing threshold.
Frozen contract integrity, 18 contract tests and 32 offline benchmark guards also pass.

This preflight adds policy and bounded local page work. The earlier
[HTTP measurements](runtime-http-observation-2026-11-02.md) remain observations for
their recorded `a4d80af` revision; they are not measurements of this implementation.
Schema/update workload qualification and the remaining Epic AC stay separately tracked.
