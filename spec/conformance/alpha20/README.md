# Alpha.20 shared corpus

Status: partial, not frozen. Tracking: P20-108 / NPS-Dev #129.

`error_status_vectors.json` fixes all 17 alpha.20 protocol-error → NPS-status → HTTP
mappings. Six SDK test suites consume it; declarations/mappings do not enable the
underlying feature or prove that every runtime route emits the correct error.
HTTP status alone, including 409/500/503, never grants permission to retry a mutation.

`context_crypto_vectors.json` freezes six request/payload JCS strings, SHA-256
request digests, domain-separated Ed25519 signature inputs/signatures and exact
JSON/base64url carriers. The included all-01 seed is a **public synthetic test key**,
never a real identity credential. Cases cover ASCII, composed/decomposed Unicode,
UTF-16 object-key order, binary64 number rendering and semantic invocation options.
The `numbers` fixture is deliberately JCS-only: its `1e21` value is outside the
profile safe-integer range and must fail request admission even though the signature
is cryptographically valid. This distinction is recorded in the fixture metadata.
Run `node tools/conformance/check-alpha20-context-vectors.mjs` after TypeScript
`npm ci`; CI runs it in the TypeScript job. It verifies fixed bytes and signatures,
plus mutations of every signed payload/request field and domain separator.
This byte oracle uses already parsed fixture objects: it does **not** validate raw
JSON duplicates/Unicode/schema, channel trust, receiver policy, time or durable replay.
Independent SDK parsing/admission and shared replay-store fault tests remain required.

`challenge-schemas.json` contains fixed challenge record/handle-parameter Anchors
and their canonical bytes/hashes, generated with the existing standard JCS dependency.
Hash integrity is checked by the oracle; this does not establish six-SDK decoding.
The profile registry lives at `spec/feature-registry.json`; all five revisions remain
experimental, with explicit runtime and projection-dependent constraints.

Data-workflow contract artifacts (D20-00): `data_result_contract.json` specifies the
experimental profile; `data_result_vectors.json` fixes portable expected outcomes;
`data_workflow_gates.json` retains all 12 alpha gates, 12 separate beta gates and 57
prior P20 tasks; `data_workflow_methodology.json` preregisters the synthetic task.
Run `python3 tools/conformance/check-data-workflow-contract.py` and its unittest
suite. These checks establish contract consistency only. They do not complete the
prior P20-108 missing corpus, enable a runtime or provide provider usage.

The original `data_workflow_manifest.json` and all twelve predecessor artifact hashes
remain fixed. `data_workflow_freeze_v1/` preserves the original registry 0.2/matrix bytes;
`data_workflow_successor.json` records the exact additive schema-batch registry 0.3 and
the matrix's shared-version change. The checker validates both generations and rejects
old-feature/contract changes even if a new hash is supplied. See the
[successor decision](../../../docs/adr/2026-11-02-additive-schema-batch-registration.md).
No result contract, alpha/beta gate, prior task or protocol/suite version is relaxed.

`schema_batch_contract.json` and `schema_batch_vectors.json` add CR-0017's 106 fixed
complete JSON/MessagePack frames/scenarios. Both encodings use canonical snake_case
metadata keys; the verifier never silently rewrites keys for one SDK. Expected rows,
version and terminal status are literal shared oracle data, not captured SDK output.
`schema_batch_manifest.json` fixes the paired profile/contract/corpus bytes. Run
`python3 tools/conformance/check-schema-batch-vectors.py` and the separate integrity
tests. The minimal independent fixture encoder/decoder exists only for authoring and
integrity checking; runtime uses the established MessagePack library. .NET's receiver
executes the same fixed bytes, with synthetic local selection/time/lower-limit setup.
That proves reference receiver consumption, not independent admission, all SDKs,
P20-108 completion or transport/resource/billing efficiency.

`schema_batch_hash_vectors.json` adds ten independent materialized-schema JCS cases,
including Unicode, HTML-sensitive text, controls, non-BMP text and composed/decomposed
strings. `schema_batch_hash_manifest.json` links their exact bytes to the unchanged
original wire manifest; the 106-case freeze is not regenerated for this supplement.
The same checker verifies this link and strict canonical bytes/hashes. .NET executes
all ten through both encodings, with invalid-surrogate rejection tested separately.

`feature_handshake_boundary_vectors.json` freezes six full default-header Tier-1
Hello/Caps byte sequences: small, exactly 16384-byte payload, and one byte above.
The bound includes base fields and trailing JSON whitespace. The oracle validates
framing lengths and the NCP-FTR-02 bound only, not complete handshake admission.

`feature_negotiation_vectors.json` contains exact UTF-8 JSON negotiation blocks and
HTTP header instances with expected parser/selection outcomes under CR-0012.
`local` and policy switches are trusted test configuration, never wire assertions.
The corpus includes duplicate keys, strict numeric types, revision/ID/count/byte
boundaries, experimental opt-in and a forged lifecycle label. Fixture IDs refine
the CR scenario IDs; several fixtures may cover one scenario.

Run `python3 tools/conformance/check-alpha20-feature-vectors.py` from the repository
root. The standard-library oracle is a specification check, not a production SDK,
cryptographic verifier or substitute for transport/handler tests. CI runs it explicitly.

Still required before P20-108 completion: frame-enveloped bytes, client selection
validation, cross-feature dependencies, admission/dispatch counters, unsafe HTTP
routes, response loss, reconnect/failover, context signatures/replay, runtime faults
and challenge state races. Six SDKs must independently consume the shared corpus;
passing this oracle alone must never be reported as SDK parity or release readiness.
