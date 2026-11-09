English | [中文](README.cn.md)

# Experimental alpha.20 local qualification

`local-ci.json` is an **amended/composed** passing receipt, not a claim that every
suite ran at the later HEAD. The complete local run used
`0c4bfdf694e07ca0b60a9eb67cfd4a1e8a15e804`; its unmodified failed receipt/logs are in
`attempt-01/`. Every test/audit/runtime stage passed, but the last offline NCP verifier
received a directory instead of its JSON manifest. Fix source
`847087332a7872aaf22e6449cf38ee1298187e56` changes exactly that one argument.
`offline-retry/` records all three actual offline verifier exits0 and the exact patch.
The amended receipt binds both revisions, original failure/log hashes, unchanged
matrix/SDK/test/fixture/audit inputs and the one-line patch hash. Successful workload
stages were reused; they were not falsely relabeled as execution at the fixed HEAD.
The historical composition recipe is included as an audit record with host-local paths;
for a fresh reproduction run the fixed canonical local runner with new output paths.

Actual inventories: .NET1682/0failed/0skipped, Python1513 with existing90% coverage
floor reached, TypeScript1324, Java744, Rust765/allfeatures, Go fresh package tests.
Java CA's real registration/signature/certificate/restart and packaged jar probe pass.
Seven security categories pass: full Git history/workflow semantics, npm, reachable Go,
RustSec, resolved Python, resolved MavenOSV and transitive NuGet. Advisory/module metadata
and warnings remain in the logs; scanner PASS does not claim all imaginable vulnerabilities
or a production security audit. Toolchain versions are in attempt-01/toolchains.log.

Seven distinct configuration rows pass with every frozen test identity required:
.NET result HTTP, Python public ResultHttpClient to .NET over real mTLS at100/1000/
100000rows, .NET schema batches JSON and MessagePack including every fault/recovery
variant, local four-worker NOP, volatile NDP different-issuer transfer and same-issuer/
new-endpoint transfer. Original576production cells are not filled. The native handshake
fixture remains0.11 with explicit schema_batches revision1; this is reference-feature
proof, not full NCP0.13 or production PKI qualification. No default advertisements.

The helper-package stage checks historical actual six-family consumers/logs/inspection
hashes and exact current payload-source equivalence. Historical deleted archive bytes
are not claimed rehashed. Current mTLS work builds/installs fresh source-bound wheel/
four NuGet artifacts. Original source/packaging/signing limitations stay explicit.
Sealed NCP/model/NOP packets are verified offline; original paid captures are not rerun.
No new model calls, Actions, registry upload, tag, public projection or publication.

All tracked bytes/modes were validated against Git objects before and after execution.
Assertions and installed-wheel imports were protected from PYTHONOPTIMIZE/PYTHONPATH.
Full inventories and exact variants prevent partial/skipped runs counting as passes.
`index.json` binds all closed packet files; frozen-matrix.json is the preregistration.
GitHub validation workflows were disabled under the user's local-CI instruction.
Public source/version/signing/SourceLink/registry authority and beta remain #134/later
prerequisites. Local experimental qualification does not imply ready-to-tag.
