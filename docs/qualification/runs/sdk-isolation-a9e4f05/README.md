English | [中文](README.cn.md)

# D20-08 standalone SDK and unpublished package evidence

Sources: `2d16f7d7f87e6185d7c82b25f93db98ee4d6a86d` (six SDK inputs) and
`a9e4f05eeb85c3d5e37a85e28975aa1a23a464b7` (TypeScript build order and remaining
Rust fixture correction). The source-bound projection receipts identify every
copied file; `results.json` gives exact scoped outcomes, artifact hashes and preserved
failures. Logs are retained with SHA-256. No registry upload or model call occurred.

Reproduce from clean source worktrees at those revisions, using
`tools/release/prepare-sdk-projections.py` outside all Git worktrees. Original suites:

~~~sh
# From each standalone SDK root, with the corresponding installed toolchain:
DOTNET_USE_POLLING_FILE_WATCHER=1 DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false dotnet test NPS.sln -c Release --nologo
python -m pytest -q # existing pyproject coverage floor90 remains active
npx --yes npm@11.11.0 ci --ignore-scripts
npm test # pretest builds the SQLite process workers at corrected source
npm run prepack
go test ./...
JAVA_HOME=/path/to/gradle-launcher-jdk ./gradlew test jar sourcesJar javadocJar generatePomFileForMavenJavaPublication --no-daemon
cargo test --workspace --all-features --locked
~~~

Observed toolchains: .NET10; Node22.22.1; npm11.11.0 for mutation/install,
Python3.14.4; Go1.26.10; Gradle8.14.3 with Java21 compiler/tests; Rust1.99.1.
The Gradle launcher JDK17 is separate. Rust uses task-local rustup/cargo homes.
.NET's standalone SDK compiles1538 tests:128 daemon-owned tests are excluded by the
existing conditional project boundary. Full source92bf98e CI covers1666 separately.
This difference is explicit and does not waive daemon qualification.

Package overrides occur only in isolated inputs: Python pyproject version
`1.0.0a20.dev0+2d16f7d7f87e`; npm package/lock root versions
`1.0.0-alpha.20.dev.a9e4f05eeb85`; Java Gradle version
`1.0.0-alpha.20.dev.2d16f7d7f87e`; Go local module-proxy version and VERSION
`v1.0.0-alpha.20.dev.2d16f7d7f87e`; NuGet uses pack properties Version,
RepositoryCommit/SourceRevisionId and explicit unpublished PackageReleaseNotes.
Canonical manifests remain alpha.19. Python isolated build uses build/hatchling;
twine checks both wheel and sdist. A fresh consumer venv installs the wheel and
executes all1513 existing tests using only copied tests/conformance, with no SDK
source tree present (92.13% coverage). The host ensurepip/bootstrap failure remains.

NuGet reuses the existing11-project family inventory and complete-family checker;
inspection confirms DLLs/PDBs, READMEs, licenses, source revision and exact internal
candidate dependencies. Empty SourceLink and Nexus advisory warnings remain release
blockers, not clean audit proof. Maven jar/source/javadoc/POM are built and inspected;
no signing task is invoked. Unsigned artifacts do not satisfy the publish/signing gate.
Go downloads the source-bound zip through a local file module proxy, with GONOSUMDB
limited to that unpublished module; ordinary dependency checks remain active.

The small public-API consumers are included for reproduction. Install the built npm
archive into a fresh consumer, copy `npm-check.mjs` beside its package.json and pass
the shared feature fixture as argv2. Rename JavaConsumer.java to Consumer.java and
compile/run with the built SDK jar and declared Jackson2.18.11 dependencies; pass the
same fixture. The NuGet consumer references all11 candidate packages, restored from
the local package directory plus nuget.org. GoConsumer.go/go.mod use the local proxy.
These consumers test packaged imports/parser behavior, not576 runtime matrix cells.

Rust's existing verifier retains all eight ordered cargo package unpack/compile
checks, workspace all-feature tests and clippy `-D warnings`. The first attempt hit
temporary-filesystem quota after six crates; the second completed all package checks
and tests, then found the task-local clippy component absent. Its final result must
be recorded separately after installing clippy and using disk-backed TMPDIR. No
passing receipt is inferred from either failure, and no `cargo publish` is used.

Final Rust attempt03 PASSED: all8 actual package verifications, all-feature workspace tests and clippy all-targets/all-features with warnings denied. See rust-candidate-attempt03.json; the two prior failed attempts remain in logs. This qualifies the local candidate, not publishing or untouched production-lock resolution.
