English | [中文](alpha20-sdk-isolation-plan-2026-11-03.cn.md)

# D20-08 standalone SDK verification

The shared parser fixtures previously depended on fixed private-monorepo relative
paths in Python, TypeScript, Go and Rust tests. D20-08 uses the existing nearest
conformance-root lookup pattern, shared fixtures and canonical implementations.
TypeScript additionally exposes existing experimental helpers through explicit
`/alpha20` and optional `/alpha20/sqlite` package subpaths. These are public import
surfaces, not runtime/profile advertisements or complete compatibility cells.

Commit this source before preparing all six standalone SDK trees with the reviewed
ownership map and `tools/release/prepare-sdk-projections.py`. Record source revision,
map and projection hashes, toolchains, exact commands, exits and test counts.
Run the ordinary language suites from the standalone roots with shared conformance
assets, without monorepo-only paths. Python's existing coverage floor remains90%.
Build actual packages and exercise public imports separately; a source import alone
cannot prove a packaged entrypoint. Unpublished version overrides, if used, must be
source-bound and confined to isolated inputs; canonical suite/packages remain19
until the separate reviewed release workflow. Preserve failures before corrections.

The preparation tool's regression suite checks hidden tracked-file/map edits,
traversal/globs, dirty source, missing scope, symlink input/output, overwrite,
conflicting owners, executable mode, lockfile tampering, missing projection and
six-SDK byte provenance. Decision-tier review is bounded to supply-chain changes;
the parent executes verification. No tool may publish or change existing clones.
A passing isolated suite does not fill the576-cell native/HTTP runtime matrix,
waive old gates or prove publishing authority. D20-08 remains IN_PROGRESS.
