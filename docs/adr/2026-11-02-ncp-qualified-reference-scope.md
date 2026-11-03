# ADR: NCP reference scope after native transfer qualification

- Status: Accepted experimental reference and scope constraint; pre-1.0 author review.
- Date: 2026-11-02; D20-04 #168, Epic #163, CR-0017.
- Predecessor: [HTTP measurement scope](2026-11-02-ncp-measurement-scope.md).

## Evidence

The [native/HTTP observation](../benchmarks/ncp-transfer-observation-2026-11-02.md)
([中文](../benchmarks/ncp-transfer-observation-2026-11-02.cn.md)) at exact source
42a0b05865d4206b612cc55f2e80f2505cb6ea67 passes all 72 conditions, 2,160 measured
workflows and 216 warmups, with exact source/schema/rows and unchanged declared
resource checks. Every native median latency, CPU, allocation and TLS-stream byte
value is higher than the same-encoding HTTP gzip counterpart in all 24 pairs.
At 100,000/warm/original, tuple wire bytes are lower than raw same-encoding object
rows by 50.23%/52.79%, while gzip HTTP is substantially smaller. These are distinct
comparisons; neither establishes a model-token or provider-cost advantage.

The actual helpers also qualify bounded credit, current host source policy, exact
schema/epoch/base updates, deterministic errors and explicit fresh-baseline recovery.
Fixed portable cases and Unicode/JCS supplements preserve exact bytes and the
original result-profile freeze. Provisioned pins are not general NIP delegation,
resumed-session qualification, NWP result subscriptions or all-SDK evidence.

Earlier resource failures and uncommitted diagnostics remain separate. The passing
capture uses shared named source records, standard streaming HTTP and an explicit
asserted fixture executable. It counts the entire actual process tree. Replacing
source buffers or removing an unneeded test platform is not a protocol memory gain.

## Decision

Accept D20-04's scoped .NET reference correctness/resource/evidence AC. Keep
nps.schema_batches revision 1 experimental, explicitly selected and disabled by
default. Retain its existing schema/epoch/version/credit/recovery responsibility.
Do not advertise superiority over compressed HTTP, universal memory savings,
live-delta savings, Arrow/Flight/Protobuf superiority or LLM billing reductions.

Continue authorized D20-05 using public bounded result/tool operations and actual
end-to-end model/tool evidence. Ordinary HTTP can already keep bulk data outside
context; the demo must show whether result identity, bounded inspection, grants,
recovery and composition add practical value. It cannot assume a transport saving
or force the native batch path into a tool workflow to manufacture a difference.

D20-08 can qualify accepted shared surfaces after its prerequisites. It must report
actual per-SDK/binding cells and retain unsupported paths. Performance optimization
can be evaluated in a separate measured revision if needed. Current co-host counters
include materially different implementation/validation/projection work, so they do
not isolate the cause of the negative native result.

## Consequences and gates

The [D20-04 AC record](../plans/2026-11-02-ncp-reference-ac-evidence.md) is COMPLETE
for this reference scope. This does not merge stacked PRs or qualify production
PKI/source policy, daemons, six SDKs, every schema/resource shape or the full suite.
No frame number, default offer, result operation, repository role or release boundary
changes. No old 30%/50% obligation, alpha/beta/P20 gate, dependency advisory,
registry credential inventory or publication requirement is removed or lowered.
#134 publishing remains a separate decision after the complete pre-release workflow.
