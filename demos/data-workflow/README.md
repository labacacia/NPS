English | [中文](README.cn.md)

# Bounded order/refund tool workflow

Status: COMPLETE for D20-05 experimental reference and measurement AC. The controlled
matrix completes all 80 workflows with exact answers and faithful citations; the actual
process-tree conservative peak is 435.71484375 MiB under the unchanged 512 MiB gate.
[Controlled observation and limits](../../docs/benchmarks/data-workflow-agent-controlled-observation-2026-11-03.md).
The first matrix's two model failures and 558 MiB resource failure remain preserved.
Production, all-SDK and publication qualification remain separate.

## Capability and boundaries

Analyze 100, 1,000 or 100,000 synthetic orders using four bounded operations:
create an immutable result, read an explicit page, aggregate COUNT/SUM, release.
Ordinary named JSON endpoints and projected NPS share the same public SDK operator
engine, source, principal, resource policy and process-local store. NPS additionally
performs affirmative discovery and a separate authenticated gateway/receiver hop.
This comparison does not give the ordinary arm a raw-data-only implementation.

The public Python client uses mTLS. Its TLS identity maps to a current server-provisioned
NIP Ident checked against a synthetic CA/revocation store. The gateway removes incoming
projection headers, signs a fresh receiver-bound proof and connects with its own TLS
identity. The receiver verifies current identity/resource policy and durable SQLite
replay before execution. A separate operator certificate permits exactly one source
update; it is never a model tool. These ephemeral, pinned loopback identities are
**synthetic provisioning, not a production PKI enrollment/rotation implementation**.

The demo uses declared public SDK packages. Development builds are unpublished
alpha.20 snapshots; published alpha.19 does not contain these APIs. It applies the
Codebase per-demo contract at `9d4010702e14213a8749aee3a7c0af0ceaa81478` within the
existing flat demo layout. Other demos and repository roles are independent.

## Setup

Prerequisites: .NET 10 SDK, Python 3.11+, network access to NuGet/PyPI for dependencies.
Commands below run **inside this demo directory**. Never install packages globally.

```bash
python3 -m venv .demo-output/venv
.demo-output/venv/bin/python -m pip install --upgrade pip
```

If the OS Python lacks `ensurepip`, install its venv support or create this environment
with an existing managed Python/pip. The official pip `--python <venv-python>` option
can install into a `python3 -m venv --without-pip` environment; no TLS bypass is needed.

In canonical NPS-Dev only, build SDK artifacts from committed clean SDK sources:

```bash
.demo-output/venv/bin/python build-packages.py
```

This emits `.demo-output/packages/<source-prefix>/source.json`, four NuGet packages,
symbols and a Python wheel, with exact hashes/source revision. It never publishes,
changes canonical version files or calls a model. Later builds use revision-specific
development versions to avoid package-cache collisions. When SDK source dependencies
have not been restored, restore the canonical SDK solution first using its configured
feeds. Distributed copies instead receive this already-built package directory; their
setup/server/check code requires no private monorepo source path.

Use the emitted directory as `PACKAGE_DIR` below (or a delivered `packages` directory):

```bash
PACKAGE_DIR=.demo-output/packages/<source-prefix>
.demo-output/venv/bin/python -m pip install "$PACKAGE_DIR"/nps_lib-*.whl
SDK_VERSION=$(.demo-output/venv/bin/python -c 'import json,sys; print(json.load(open(sys.argv[1]))["nuget_version"])' "$PACKAGE_DIR/source.json")
dotnet restore server/NPS.Demo.DataWorkflow.csproj --source "$PACKAGE_DIR" --source https://api.nuget.org/v3/index.json -p:NpsDemoSdkVersion="$SDK_VERSION"
dotnet build server/NPS.Demo.DataWorkflow.csproj -c Release --no-restore -p:NpsDemoSdkVersion="$SDK_VERSION"
```

## Run and verify

```bash
.demo-output/venv/bin/python check.py --packages "$PACKAGE_DIR"
```

Expect three PASS lines and a fresh `.demo-output/check-*/receipt.json`. The check starts
actual loopback HTTPS/mTLS servers, verifies both arms against the extracted frozen
D20-01 oracle, five inspected supporting order IDs, create replay, snapshot/schema
conflicts, release, current authorization and a one-percent amount/schema update.
Old snapshots and same-key replay remain unchanged; new results carry new snapshot
and schema fences. It checks the operator is denied to the owner, the operator cannot
use the public owner route, direct receiver bypass fails and duplicate JSON is rejected.
These are actual negative-path requests; assertion failures exit nonzero.

`--rows 100` runs a shorter diagnostic. The receipt records exact SDK provenance,
assembly/demo-source hashes and results. It contains synthetic data only. The default
check uses no model API or credentials.

Optional additional tool guards:

```bash
.demo-output/venv/bin/python -m pip install pytest pytest-asyncio
.demo-output/venv/bin/python -m pytest tests -q -o asyncio_default_fixture_loop_scope=function
```

For an interactive local server, select a **fresh absolute path** below `.demo-output`:

```bash
dotnet server/bin/Release/net10.0/NPS.Demo.DataWorkflow.dll --output "$PWD/.demo-output/manual-01" --rows 100000
```

It prints only the connection configuration path. Use its owner CA/certificate/key
with an HTTPS client. POST `ordinary/create` with
`{"params":{"source_id":"orders"},"idempotency_key":"<UUID-v4>"}`; NPS uses the
installed Python `ResultHttpClient`. Shutdown with Ctrl-C. One-hour credentials are
valid only for that running host; restarting does not migrate results.

## Limits, effects and cleanup

The synthetic source accepts `orders` plus an optional allowlist filter: `refunded:true`,
`period:baseline|current`, `reason:delivery|quality|duplicate|other`. It rejects source
field/order query extensions, URLs, SQL and scripts before capture. Reads can select
fields. Result TTL is 1–900 seconds, default 300; snapshot retention is process-local.
The operator update adds 100 minor units to the first one percent of records and adds
nullable `note`; it changes data version, not resource policy version.

`WorkflowTools` gives the model four host-bound operations, at most four allocations,
16 calls, five rows per read, 20 cumulative inspected rows and 32 KiB cumulative tool
output. It admits only actual returned descriptors, computes anomaly arithmetic from
executed aggregates and rejects fabricated citations. Exact release/unknown-allocation
outcomes are retained for cleanup/recovery; no implicit retries or new-key recovery.
The [live Agent](LIVE.md) is implemented and verified with an offline provider fixture plus actual mTLS tools;
its real-provider matrix is complete and all failed cases remain preserved.

Artifacts, certificates/private keys, SQLite, logs, environments and packages remain
under Git-ignored `.demo-output/`. Run directories have owner-only Unix permissions;
private keys are mode 0600. Stop owned server processes, then delete only selected
generated run directories inside `.demo-output`. Known results are explicitly released
by checks; failures/cancellation do not prove rollback. Never delete/reset the shared
provider-spend ledger as demo cleanup. No customer data or provider secret is required.

Existing MessagePack 3.1.7 dependency advisory NU1902 remains open/unwaived. This local
reference demo does not qualify every SDK, public deployment, transport, native result
binding or production certificate lifecycle. No native compression or provider token
saving is assumed; D20-04 measured native transfer worse than matched HTTP gzip.

[Implementation and remaining AC](../../docs/plans/2026-11-02-data-workflow-demo-implementation.md).

The [controlled closed packet](evidence/live-tool-agent-bounded-v2-126c5df/index.json)
includes measured recipes, all visible responses/tool traces and a sanitized ledger
extract. Verify without credentials, SDK, private original ledger or network calls:

```bash
python3 summarize.py --packet evidence/live-tool-agent-bounded-v2-126c5df
python3 summarize.py --packet evidence/live-tool-agent-afafebd
```

These separately verify all file/recipe hashes, paid observations, usage, charges,
answers, actual inspected citations and update fences. The controlled packet also
checks the original CPU/heap/RSS/deadline guards. The original USD10 ledger retains
all 1,118 attempts and historical USD3.20 unknown reservations: conservative total
USD4.3990379, remaining USD5.6009621, not an invoice. Do not execute either completed
matrix again. Current failure-path fixes follow the sealed measured recipes; see the
[scope decision](../../docs/adr/2026-11-03-data-workflow-agent-scope.md).
