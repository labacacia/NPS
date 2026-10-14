# Contributing Guide

**Status:** ✅ Reviewed for v1.0.0-alpha.19 release

Thank you for your interest in contributing to the Neural Protocol Suite. This page covers issue routing, labels, PR conventions, code style per language, documentation standards, and security disclosure.

---

## Issue Routing

**ALL issues go to `labacacia/NPS-Dev`** — the source monorepo. This applies to:

- Spec bugs and design discussions
- Implementation bugs found in any SDK or ingress adapter
- Cross-repo drift findings (e.g. a version mismatch between repos)
- CI failures and release process problems
- Documentation gaps

**NEVER file bugs in satellite repos** (`labacacia/NPS-SDK-Python`, `labacacia/NPS-SDK-TypeScript`, `labacacia/NPS-SDK-Java`, `labacacia/NPS-SDK-Rust`, `labacacia/NPS-SDK-Go`, `labacacia/NPS-SDK-DotNet`, `labacacia/NPS-Daemons`, `labacacia/NIP-CA-Server`, etc.). Those repos are distribution-only — they receive one-way sync pushes from NPS-Dev. Issues filed there will be closed as out-of-scope with a redirect to NPS-Dev.

The one exception: issues with the *demo content itself* (typos, unclear output, outdated results) may be filed against `labacacia/NPS-Examples`. Protocol bugs found through a demo still go to NPS-Dev.

---

## Label Taxonomy

Labels in `labacacia/NPS-Dev`:

| Label | Meaning |
|-------|---------|
| `severity:critical` | Data loss, security vulnerability, or complete functional breakage |
| `severity:major` | Significant behavioral bug or spec violation; blocks a release |
| `severity:minor` | Edge-case bug, cosmetic issue, or quality-of-life gap |
| `area:spec` | Spec documents in `spec/` |
| `area:sdk` | SDK implementations in `impl/` |
| `area:ingress` | Compatibility ingress adapters in `compat/` |
| `area:daemon` | Daemon implementations in `tools/daemons/` |
| `area:docs` | Documentation under `docs/` |
| `area:ci` | CI scripts, release tooling, version-sync checks |
| `bug` | Something is broken relative to spec or documented behavior |
| `enhancement` | New capability or quality improvement |
| `documentation` | Doc-only change |

Apply the most specific `area:` label. Apply both `severity:` and `area:` for bugs.

---

## Issue Prefixes

Use a prefix in the issue title to aid triage:

| Prefix | Use for |
|--------|---------|
| `spec:` | Specification questions and design discussions |
| `impl:` | Implementation bugs and fixes |
| `sdk:` | SDK-related (Python / TypeScript / Java / Rust / Go / .NET) |
| `docs:` | Documentation improvements |

---

## PR Conventions

- **Reference the issue.** Include `Closes labacacia/NPS-Dev#<n>` in the PR body for any fix. Cross-repo PRs (e.g. a fix that lands across NPS-Dev and a distribution repo) reference the same NPS-Dev issue.
- **One concern per PR.** Spec change + tests + CHANGELOG update are one concern. Mixing unrelated changes into a PR delays review.
- **Commit message format:** `{scope}: {summary}` — e.g. `spec(NCP): add EXT flag for configurable frame size` or `sdk(python): fix AssuranceLevel.fromWire("") returning null`.
- **CHANGELOG required** for any user-visible change. Each sub-project has its own `CHANGELOG.md`; the umbrella `CHANGELOG.md` in NPS-Dev also receives an entry. The release CI (`check-source-of-truth.py` Assertion D) will block the release if a CHANGELOG entry is missing.
- **Spec changes that affect wire format or frame structure** require a version bump to the spec document and to `spec/frame-registry.yaml`. Breaking spec changes require an RFC (see [RFC Process](./RFC-Process.md)).
- **Verify before closing.** Post evidence — grep output, test run logs, or CI link — when closing an issue. Do not over-claim commit messages ("fix all SDK issues" when only one SDK was touched).

---

## Code Style Per Language

### .NET (C#)

- Framework: **.NET 10** (LTS)
- Nullable: enabled (`<Nullable>enable</Nullable>` in all csproj files)
- Testing: **xunit.v3** — the legacy `xunit` package is deprecated; do not use it
- Coverage target: ≥ 90% line coverage on new code
- Namespace pattern: `NPS.{Protocol}.{Layer}` — e.g. `NPS.NCP.Frames`, `NPS.NWP.Anchor`
- NuGet package names: `NPS.Core`, `NPS.NWP`, `NPS.NIP`, etc.

### Python

- Version: Python 3.11+
- Style: PEP 8, 4-space indentation
- Type hints: required on all public functions and class members
- Testing: pytest
- PyPI distribution name: `nps-lib` (note: `nps-sdk` is taken by an unrelated project)
- Import namespace: `nps_sdk`

### TypeScript

- Strict mode: `"strict": true` in all `tsconfig.json` files
- Testing: Jest or Vitest
- Package name: `@labacacia/nps-sdk`
- Target environments: Node.js + browser (dual build)

### Java

- Version: Java 21
- Build: Gradle KTS (`build.gradle.kts`)
- Testing: JUnit 5

### Rust

- Channel: stable
- Testing: `cargo test`
- Lint: `clippy` clean before PR

### Go

- Testing: standard `go test ./...`
- No third-party test frameworks required

---

## Documentation Standards

Every documentation file MUST follow these rules:

1. **Bilingual parity is a release blocker.** English primary (`name.md`) + Chinese secondary (`name.cn.md`) must be updated together. A release will not be tagged if one language is stale relative to the other.
2. **Language switcher at the top of every doc.** First line of the English version: `English | [中文版](./name.cn.md)`. First line of the Chinese version: `[English Version](./name.md) | 中文版`.
3. **Internal links must stay within the same language.** CN docs link to CN docs; EN docs link to EN docs. Never cross-link between language versions for content links (only the language switcher itself crosses).
4. **Spec docs carry a front matter header** (Spec Number, Status, Version, Date, Port, Authors, Depends-On). See `spec/rfcs/README.md` for the required format.

---

## Security Vulnerability Disclosure

**Do NOT file public GitHub issues for security vulnerabilities.**

If you discover a security vulnerability in any NPS component:

- **Email:** security@labacacia.com
- **Alternative:** Use the GitHub Security Advisory feature in `labacacia/NPS-Dev` if configured

Include: a description of the vulnerability, the affected component(s), reproduction steps, and your assessment of severity. You will receive an acknowledgment within 72 hours.

Do not disclose publicly until a patch has been prepared and a coordinated disclosure date agreed.

---

## Work Discipline

- **Grep-verify before closing an issue.** Post evidence (grep output, test run, CI link) that the problem is resolved. "I think this is fixed" is not sufficient.
- **Do not over-claim commit messages.** If you fix Python and TypeScript only, say so — do not write "fix all SDKs" unless you have verified all six.
- **Check existing entries before creating new ones.** Avoid duplicate issues and duplicate CHANGELOG entries.
- **Never hardcode credentials.** If a test or script needs a secret, use an environment variable and document which variable is expected.

---

## Related Pages

- [Release Process](./Release-Process.md) — how a release is prepared and tagged
- [Repository Topology](./Repository-Topology.md) — which repos exist and how they relate

---

*Last reviewed at suite version: v1.0.0-alpha.19 release*
