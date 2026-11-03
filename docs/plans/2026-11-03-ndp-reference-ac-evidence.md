English | [中文](2026-11-03-ndp-reference-ac-evidence.cn.md)

# D20-07 reference acceptance evidence

Trace: #171 / Epic #163. Status: **COMPLETE for the bounded experimental reference AC**. [Clean-commit CI41591866781](https://github.com/labacacia/NPS-Dev/actions/runs/41591866781): all **10** jobs PASS (six SDK regressions, actual package-based mTLS demo, oracle, CA/container and NativeAOT checks). Local full suite **1666/1666 PASS**, including **28** new cases; [source-bound executed summary and initial failure](../qualification/2026-11-03-ndp-reference-tests.json). Implementation: [#211](https://github.com/labacacia/NPS-Dev/pull/211), `02443a3a4e6e096309b9c562de8ec1bc658b84b9`.
Scope: explicitly selected volatile .NET route admission and two actual HTTPS result
nodes; no production-runtime/durable-daemon/native/all-SDK qualification.

| AC | Implementation and observable check |
|---|---|
| Endpoint/capability discovery | NdpResultDiscovery admits bounded canonical signatures only after independent CURRENT host NIP/key/cluster/role/epoch/endpoint policy; Resolve treats capabilities as hints. |
| Freshness, draining, rollback, split | NdpResultDiscoveryTests covers signed and monotonic expiry, wall rollback, unsigned heartbeat no extension, sticky drain, retained sequence/digest/epoch fences, capacity, equal-epoch conflict, no lower-owner fallback, current policy changes and cold generation. |
| Required unsupported feature rejects | NdpCoarseCapabilityCannotBypassWithdrawnNwmOrCurrentNip primes affirmative NWM support, withdraws it with a higher manifest version and observes zero additional POST/provider/capture; then revokes CURRENT NIP with zero additional receiver/provider/capture. |
| Peer change requires fresh admission | NdpTwoNodeTransferRequiresFreshAdmissionAndCannotMigrateOldResult executes different-NID and same-NID/epoch endpoint transfer. Separate actual server certificates, public clients, NWM, receiver audiences/projection/replay/stores. Saved route rejects before B POST; fresh B client enforces its selected TLS pin/current NIP/NWM. |
| Issuer loss/state ownership | Same-NID empty successor returns NOT-FOUND without capture; different issuer rejects the old descriptor locally without dispatch. A released local resource remains NOT-FOUND without recreate. This is independent-store/missing-resource evidence, not crash or durable migration. |
| Explicit recovery/no mutation replay | Discovery never sends or replays operations. Old result retains original admission binding and cannot silently bind to B. Only an explicit NEW owner query creates B state. Unknown original create keys/query/TTL remain governed by D05/D06; their indeterminate cases are not reclassified here. |
| Static path | RealGatewayUsesTwoAuthenticatedLegsAndFreshProjectionForEveryOperation runs independently of discovery; after transfer a separately configured A client can read/release retained A state if its receiver policy still permits it. |
| EN/CN/docs and truthful scope | Public Discovery README, docs/samples/ndp-result-routing, NDP §7.9 reference notes and routing ADR. Existing defaults, wire versions and registry daemon remain unchanged. |

The fixture identity seam defaults to the existing NID, preserving every earlier test.
A minimal scope-supporting SDK fix distinguishes locally invalid descriptors (known
no dispatch) from invalid received descriptors (still uncertain); the different-issuer
transfer regression observes that distinction. Independent decision-tier review of
identity/fences/parser/transport/test claims completed; duplicate-name rejection and
per-operation validation/cache-withdrawal evidence were tightened before qualification.

Initial full local run failed: 86 host inotify-quota errors and one existing native
round-trip assertion at 539ms against 500ms. Preserve that result; no threshold was
changed. Re-run uses only command-scoped DOTNET_USE_POLLING_FILE_WATCHER=1 and
DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false to avoid configuration file listeners.
This functional qualification makes no resource, latency, token or cost claim.

No paid model calls; original USD10 ledger remains unchanged. Security advisories,
registry credential inventory, prior P20/beta/compatibility/adopter and release gates
remain unwaived. No merge, projection or publication; #134 remains separate.
Next agent action after verified D07: D20-08 #172 integration/support/security/candidate audit.
