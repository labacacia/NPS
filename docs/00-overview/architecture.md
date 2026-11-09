# Architecture

## Purpose and lifecycle

Public specification, handbook, and release-metadata projection from NPS-Dev.

## Boundaries

spec/: released specifications.

docs/handbook/: handbook.

version.yaml: suite manifest.

CHANGELOG files: release history.

Keep responsibilities and dependency direction explicit. Do not add layers or cross-repository sources of truth without a concrete requirement. Record architecture-significant decisions under `docs/04-decisions/`.
