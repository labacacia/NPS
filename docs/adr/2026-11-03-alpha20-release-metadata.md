English | [中文](2026-11-03-alpha20-release-metadata.cn.md)

# Alpha.20 release metadata succession

Date: 2026-11-03. Status: accepted for local candidate preparation under #134.

The user authorized reversible alpha.20 candidate preparation and local preflight on
2026-11-03. This decision permits only the top-level `suite_version` in
`spec/version-matrix.yaml` to advance from `1.0.0-alpha.19` to `1.0.0-alpha.20`.
It authorizes no publication, tags, Actions, feature, protocol, contract, fault or
threshold changes.

The original twelve-artifact freeze, its manifest and `data_workflow_freeze_v1`
remain historical and byte-for-byte unchanged. The existing additive successor
record remains byte-for-byte unchanged. A separate release metadata record binds
the additive matrix predecessor hash and current matrix hash. The checker reverses
only the single exact top-level suite marker and requires the resulting bytes to
match the existing additive matrix hash before applying the original proof.
Older checkouts without the new record continue through the original freeze path;
alpha.20 matrix bytes require the new strict record. Updating its current hash
cannot authorize any other matrix edit. This is candidate evidence, not ready-to-tag
or registry/signing authority.
