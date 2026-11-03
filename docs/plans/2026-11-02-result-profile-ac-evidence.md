English | [中文](2026-11-02-result-profile-ac-evidence.cn.md)

# D20-02/03 reference acceptance evidence and qualification limits

Date: 2026-11-02. Canonical operating records: [D20-02 #166](https://github.com/labacacia/NPS-Dev/issues/166),
[D20-03 #167](https://github.com/labacacia/NPS-Dev/issues/167), [Epic #163](https://github.com/labacacia/NPS-Dev/issues/163).
Status: COMPLETE for D20-02/03 experimental reference AC. This audits the claimed
projected HTTP reference and unsupported-route rejection, not every candidate binding.
The final full regression passes 1313/1313 tests; 39 offline evidence guards and the
unchanged frozen contract pass. No unsupported binding counts as a positive pass.
Source checkpoints: #183/73ed345 and
#184/1860c19. Earlier f584f47 retained a failed 14/18-case experiment. The shared-store
capture allocation fix passes the full 1303-test suite and 165 targeted cases; the
[1860c19 actual matrix](../benchmarks/schema-update-http-observation-2026-11-02.md)
passes 18/18 cases and 144 configurations/4320 measured workflows at 487.16MiB peak,
without changing resource gates. The later
[3b624e4 matrix](../benchmarks/process-cpu-observation-2026-11-02.md) adds all actual CPU
samples, passes the same 144 conditions and peaks at 477.59MiB. D20-01 benchmark AC are
complete; that does not close either result-profile task.
One unmodified native handshake
close test timed out in the initial full run; isolated and final full reruns passed
without changing its deadline.

The subsequent cancellation/delegation increment adds five real HTTP boundary cases;
all five and the full 1308-test suite pass. They strengthen the rows below without
claiming native support or altering product execution behavior.

| AC / boundary | Executed evidence | Remaining qualification |
| --- | --- | --- |
| Exact rows/counts/sums and provenance | ResultOracleWorkflowTests executes all 3 frozen scales through the supported .NET public client, actual two-hop mTLS and NIP proofs; each group and source ID matches the frozen oracle. | No live model tool Agent; that remains D20-05. |
| Snapshot/schema/cursor integrity | Store tests, real HTTP substituted-snapshot rejection, all 39 frozen semantic HTTP cases and 18 schema/update cases; actual 1860c19 repeated matrix passes all 144 configurations. Old pages/cursors stay on old source; new result rejects another result's cursor, even in the no-change control. | Keep historical failures and distinguish bounded HTTP reference qualification from native/multi-node scope. |
| Expiry/release/restart/capacity | Store and actual HTTP invariants exercise exclusive expiry, released/tombstoned owner receipts, actual replacement empty store/unproven successor, owner limit and zero-handler denials. Page responses are byte-bounded with continued pagination. | Process-local retention only; no persistence or cross-node result migration. Native/multi-node scope cannot be inferred. |
| Public client / discovery / recovery | Actual loopback client/gateway tests: pinned node/route, monotonic complete NWM, withdrawal/invalid offers, exact echo and bounded payloads. Added real two-hop tests prove pre-canceled zero dispatch and cancellation after allocation followed by same-key recovery with one capture. Standard OperationCanceledException propagates; it does not prove rollback. | Other bindings need their own boundary evidence; no native qualification follows from HTTP cancellation tests. |
| Principal/tenant/resource enforcement | Authenticated two-leg receiver binds signature/channel/issuer/audience/complete request and live CA/source/scope/field/operation/egress policy. Actual tenant test authenticates Bob independently, maps him to another tenant in server policy and denies read/aggregate despite a recipient-name grant; forged X-Tenant cannot change authority. | Provisioned transport pins/one source; not general PKI, global revocation subscriptions or all foreign-protocol bridges. |
| Zero dispatch / nonce ordering | Actual mTLS regression reproduced the prior Execute-entry defect. Preflight rejects resource/binding denials; a signed recipient proof denied before a lawful grant remains usable after that grant, proving no nonce was consumed. | Successful preflight is neither reservation nor authority; live execution/atomic publication must remain. Infrastructure and audit failures cannot be remapped into a resource 404. |
| Recipient delegation / derivation | Exact result/snapshot/field/operation/owner/expiry grants; recursion/third-party access rejects. Added two-hop tests deny ungranted SUM, allow granted COUNT, deny after grant revocation, and reject read/aggregate on both raw/filtered results after source or owner-CA revocation, with zero additional dispatch. Aggregate/page retains original descriptor. | No result-from-result creation/new derived TTL/authority; do not claim derived-store or live-result-stream qualification. No result streaming action is registered. |
| Metadata / model egress / logging | All 39 HTTP semantic cases assert outcome and actual handler delta; errors omit retained locator/snapshot/protected metadata. Independent mTLS audits omit rows/fields/raw handles/proofs. C/D fixture operators enforce service-owned ToModel; model cannot assert this permission. | Frozen semantic corpus authentication is explicitly synthetic. Real bounded provider replay is historical, not live handle follow-up. |
| Bounded operation and diagnosis | Store caps/no active eviction; negative attempts retained, 1860c19 and CPU-observed 3b624e4 full workloads pass unchanged gates. Streaming fixture hash, owned staging and bounded exception-type-only diagnostics have correctness/privacy evidence. | Logical retained bytes are not process RSS; no protocol-specific resource saving or default-deployment guarantee. |
| EN/CN contract / API / operations | Paired result/invocation module docs, profile, client examples and binding/discovery/gateway/storage ADRs explain host authority, no automatic offer, process-loss recovery and minimized audits. | Update final support/qualification ledger and examples with the exact implemented bindings before any advertisement/release. |

The [binding qualification ADR](../adr/2026-11-02-reference-result-binding-qualification.md)
records the disposition required by DR-10: the projected canonical HTTP reference has
actual positive evidence; direct-identity HTTP needs separate real verifier qualification;
native result operations remain unsupported with no offer. Native dispatch now invokes
AuthorizeAsync before Execute with the same context and maps controlled provider errors.
Four framed regressions failed before this fix and pass after it, including JSON/MessagePack
native result rejection before handler/verifier/capture. Scripted transport is not native
network/NIP qualification. This is minimal support for current zero-dispatch/authority AC,
not later NCP transport implementation. D20-08 and #129/#130/#131/#132/#133 still own
retained native, six-SDK, adopter and daemon commitments before release.

CI for #183 and initial #184 passes. Dependency advisories and registry credential
inventory checks still fail. No required check/review, old P20 obligation or publication
decision has been waived. No feature is advertised by registration, and no release
projection/independent repository has changed.

Next: advance authorized D20-04 against its completed D20-00/D20-01 prerequisites,
starting with actual SDK representations and schema/base/recovery invariants.
No suite/release completion follows from the reference.
