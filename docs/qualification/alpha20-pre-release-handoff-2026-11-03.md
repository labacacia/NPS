English | [中文](alpha20-pre-release-handoff-2026-11-03.cn.md)

# D20-08 experimental handoff to #134

The user accepted the [bounded closeout decision](../adr/2026-11-03-alpha20-experimental-closeout.md)
on2026-11-03 and selected local CI. The prior scope-decision blocker is resolved.
D08 is COMPLETE for that experimental scope: its seven configurations and current
local security/source/package checks pass ([AC evidence](../plans/2026-11-03-alpha20-experimental-ac-evidence.md)). This handoff never claims ready-to-tag.
[Machine handoff](2026-11-03-alpha20-pre-release-handoff.json), [original-ID dispositions](2026-11-03-alpha20-gate-reconciliation.json),
[experimental matrix](alpha20-experimental-matrix.json).

Current mandatory evidence covers the claimed .NET/Python result HTTP paths, .NET
schema batches in both encodings, local NOP handoff and two volatile NDP transfer
configurations. Six SDK helper/codec/regression/package checks are separate. Actual
privacy/authority/egress/cancellation/recovery negatives, dependency checks, immutable
fixtures and source/package reproducibility stay mandatory. Original576 production
cells remain unexecuted, and whole-production/adopter obligations retain their IDs
under129..133. General client156/157/158 remains unqualified; the existing #156 failure
receipt is preserved. Experimental scoping does not hide that failure.

Local runner: [commands and boundaries](../../tools/ci/README.md). Automatic validation
Actions triggers are disabled; no Actions are invoked. GitHub secrets inventory and
Cargo authentication are not local CI prerequisites. The provided personal credential
file was checked recursively again: no identified Cargo/crates.io fields/markers,
and standard Cargo token environment variables are absent. No secret values are
recorded, uploaded or requested for local packaging. Rust local package verification
already passes without a publish token.

[Retained source-bound package results](runs/sdk-isolation-a9e4f05/results.json)
remain historical producer/consumer evidence. The local package gate verifies their
logs, inspection hashes and exact current payload-source equivalence; it does not
claim deleted archive bytes were rehashed. A fresh source-bound wheel/four NuGet demo
snapshot is built locally. New local receipts must identify their actual source and
all frozen parameter variants; old remote CI results remain historical only.

Actual registry ownership/scoped-token authority, signing, SourceLink from real release
Git inputs, final version20/public synchronization and zero unexplained drift remain
#134 prerequisites. Current public release versions remain19; local development
artifacts are explicitly unpublished. The last read-only comparison reported60spec/
292SDK differences; those old numbers are not silently treated as current clean parity.
No release clone is reset or projected by local CI. A real candidate receipt is captured
only after its actual committed version/source/upstream and required release checks
are coherent, never by relabeling an alpha19 receipt. Publication/beta remain separate.

No experimental D00..08 work remains. Next separate track:134 must resolve actual
release prerequisites and obtain its existing publication decision; full-production/
beta follow-ups remain open. No publication or tag is authorized here.
