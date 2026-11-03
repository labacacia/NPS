# ADR: Data result resources and authority boundary

- Status: Accepted for the experimental revision-1 contract; runtime qualification pending.
- Date: 2026-11-02.
- Trace: D20-00 #164; #129; [CR-0016](../../spec/cr/NPS-CR-0016-data-results.md).
- Basis: user-authorized continuous alpha.20 execution; author review under the repository's pre-1.0 CR policy.

## Context

NWP has query/actions but its reference MemoryNode provider currently returns named
dictionaries and live pages, not proven immutable result resources. Hand-written
positional-array benchmarks cannot prove SDK representation or provider savings.
ActionContext.AgentNid currently comes from X-NWP-Agent; it is not evidence of
verified caller authority. NIP TrustFrame grants CA trust, not per-result access.
These distinctions must survive integration rather than being hidden by a convenient API.

## Decision

Use the [paired profile](../../spec/profiles/data-results.md) as an optional
ActionFrame/Caps contract, with issuer/snapshot/schema-bound resources, immutable
capture and no default preview. The reference store promises process retention only,
bounded owner/global capacity and explicit unavailability on restart/unproven failover.
Do not introduce a new storage dependency or claim durability.

NIP verifies identity and constrained authority. Resource grants and source-policy
intersection belong to the NWP serving boundary; metadata, aggregates, receipts and
model egress are authorized too. Native/HTTP hosts must provide a verified principal
before a result provider is remotely exposed; a raw AgentNid header is insufficient.
No new key/CA mechanism is needed. Recipient-specific grants cannot recursively expand.

Freeze the exact four actions and their portable expected failure cases before runtime
implementation. Defaults are 300-second retention (maximum 900), 1000 rows/64 KiB per
read, 16 results/owner, 100000 rows/32 MiB per result and 128 MiB total retained payload.
These are conservative reference limits subject to measured validation, not throughput promises.

Retain all original alpha/beta gates and 57 P20 obligations in the ledger. The new
contract corpus cannot waive or complete the old P20-108 fault/security corpus.
No threshold in publishing policy is removed by redefining the metric.
The repository CR process permits author-reviewed alpha implementation without an
extra external acceptance ceremony; independent required CI/reviews before merge remain.

## Alternatives considered

- Reuse a schema hash as the result locator: rejected because it binds structure only.
- Persist immediately in a new database: rejected until a durable contract and workload justify it.
- Treat a signed identity or TrustFrame as permission to read every result: rejected because
  issuer trust and resource authorization are different boundaries.
- Return full rows to every model and only compress the wire: retained as a benchmark baseline,
  not evidence of model-token savings.
- Build the entire old/new profile framework before a snapshot contract: rejected as the
  implementation order, while prior contract-freeze obligations remain explicit.

## Security and operations

Verified principal injection is a prerequisite for remote serving. Access, source-policy
changes, recipient grants and egress are checked independently of model assertions.
Reference entropy is not authorization. Clock rollback cannot extend TTL. Partial capture
fails without publishing a descriptor; post-capture cancellation may retain state and must
not claim non-execution. Unknown mutation outcomes are not safe retries.
Use synthetic data, bounded errors and minimized audit records.

### Capture allocation under the declared workload

The schema/update experiments retain failures rather than weaken the 512MiB RSS
gate. After removing a fixture UTF-16 fingerprint copy, minimized diagnostics still
observed `result.create` returning 500 with `System.OutOfMemoryException` under the
explicit 320MiB managed deployment. Other unclassified failures are not assumed to
share that cause. See the [preregistered method](../benchmarks/schema-update-method-2026-11-02.md).

Use one row-name validation set per capture and read each decoded property name
once. Copy the already validated rows with an owned ArrayBufferWriter/Utf8JsonWriter,
flush/check size between rows, then retain an exact independent byte array. Preserve
the existing depth-64 writer semantics, final array framing in the byte cap and
atomic authorization/capacity publication. No authority, TTL, digest, frame or new
dependency is introduced; ordinary HTTP and NPS use the same store.

[.NET 10.0.14 serializer source](https://github.com/dotnet/runtime/blob/v10.0.14/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/JsonSerializer.Write.ByteArray.cs)
uses pooled staging plus a final byte-array copy. Avoiding that whole-dataset staging
is a mitigation for allocation pressure, not proof that the pool alone caused every
failure. This is an implementation resource tradeoff, not NCP-specific memory savings.
Keep both negative attempts and rerun the declared workload at the unchanged limits;
correctness tests alone do not qualify the measured resource budget. Host source
memory and transient copying remain outside the logical retained-byte accounting.

## Consequences

A small result API can be tested against exact source data without implying a database,
query language, replicated runtime or provider billing benefit. Unsupported durability and
failover are explicit failures. Shared contract/oracle results do not qualify six SDKs.
This record accepts design choices; it does not mark D20-00, #129 or the Epic complete.
