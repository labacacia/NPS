# ADR: Additive schema-batch registration after the result freeze

- Status: Accepted additive candidate successor; no release or wider runtime qualification.
- Date: 2026-11-02; D20-04 #168, D20-00 #164; CR-0017.
- Basis: the authorized D20-04 explicitly allows a negotiated revision for a demonstrated
  contract gap; pre-1.0 author review. Existing result semantics and gate authority remain.

## Decision

Register experimental/default-off nps.schema_batches revision 1 in feature registry 0.3
and update only that shared version in the matrix. The demonstrated gaps and reference
evidence are CR-0017, #188/#189/#190. Protocol and suite/package versions do not change.
Registration describes a candidate contract, not an offer, deployment or all-SDK pass.

The D20-00 manifest remains byte-for-byte unchanged, SHA-256
24ee510e3dd951b415d54ec0ea26e730d1ea2a2573ab2278110d06b0897ca3e7.
Preserve the original registry 0.2 and version matrix bytes in a v1 baseline directory;
their hashes must equal the original manifest. A separate explicit successor record
links the predecessor manifest, exact old files and exact new hashes. All ten other
frozen files continue to verify at their original paths and hashes. No old hash is
silently updated to fit new content.

The checker validates the original manifest itself, all twelve original artifacts and
the current successor. It permits exactly one new feature descriptor and the feature
registry 0.2-to-0.3 matrix change. It rejects changed old descriptors, removed requirements,
changed protocol/suite versions, replacement-path expansion, tampered baseline/current
bytes and stale hashes. Negative tests verify these boundaries. The original 39 result
cases, 12 retained alpha gates and 57 prior tasks remain unchanged.

## Consequences

This successor evolves optional feature inventory without reopening or rewriting the
result-resource contract. Separate schema-batch vectors/manifest track the new profile;
their design oracle never substitutes for encoded/native/SDK/resource evidence.
Source-scale, live policy/consumer/fault qualification and fair compressed HTTP/alternative
comparisons remain D20-04. Six SDKs, daemons/adopters and every prior P20 obligation stay
in #129/#130/#131/#132/#133/D20-08. The security/registry-inventory gates and #134
publication decision remain unwaived; no repository role or projection changes.
