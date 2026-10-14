# NPS Handbook

> **Status:** ✅ Wiki reviewed for the released v1.0.0-alpha.19 suite.
>
> Latest published SDK suite: **v1.0.0-alpha.19** (2026-09-06). The public
> daemon bundle and `nip-ca-server` are published on the same train and are
> also at **v1.0.0-alpha.19**.
>
> **Alpha.19 release:** protocol versions are NCP 0.12,
> NWP 0.22, NIP 0.15, NDP 0.13 and NOP 0.10; all six SDK source trees execute
> the shared hardening vectors, and the four public daemon source trees have
> completed their debt-closure reconciliation. See the verified boundary and
> explicit non-claims in [Alpha.19 Current Status](./Alpha19-Current-Status.md).

> 🌐 New to NPS? Start at the [overview site](https://nps.labacacia.com) for a 5-minute orientation, then come back here for deep-dives.

The **Neural Protocol Suite (NPS)** is a 5-protocol family for Agent-to-Agent
and Agent-to-Service communication. The canonical specification lives in
[`labacacia/NPS`](https://github.com/labacacia/NPS); this
handbook is the *narrative* layer — tutorials, how-tos, deployment guides, and
cross-cutting references that don't fit naturally inside a normative spec
document.

## Choose your starting point

| If you are… | Start here |
|---|---|
| **New to NPS** and want the 5-minute orientation | [What Is NPS](./What-Is-NPS.md) → [Protocol Stack Architecture](./Protocol-Stack-Architecture.md) → [Glossary](./Glossary.md) |
| A **developer** building an Agent or Node against NPS | [SDK Quickstart](./SDK-Quickstart.md) → pick your language ([.NET](./SDK-DotNet.md) · [Python](./SDK-Python.md) · [TypeScript](./SDK-TypeScript.md) · [Java](./SDK-Java.md) · [Go](./SDK-Go.md) · [Rust](./SDK-Rust.md)) → walk [Building an Anchor Node](./SDK-Building-an-Anchor-Node.md) |
| An **operator** deploying NPS daemons or running an AaaS service | [Operator Quickstart Bundle](./Operator-Quickstart-Bundle.md) → [Daemons Reference](./Operator-Daemons-Reference.md) → individual daemon pages → [AaaS Profile](./Operator-AaaS-Profile.md) |
| Looking to **see code in action** | [Example: ingress-playground](./Example-Ingress-Playground.md) · [Example: cross-sdk-interop](./Example-Cross-SDK-Interop.md) · [Example: nwp-graph-walk](./Example-NWP-Graph-Walk.md) |
| A **protocol designer** writing an RFC or CR against the spec | [Specs Index](./Specs-Index.md) → [RFC Process](./RFC-Process.md) → [CR Process](./CR-Process.md) |
| A **contributor** opening a PR or shepherding a release | [Contributing Guide](./Contributing-Guide.md) → [Release Process](./Release-Process.md) → [Repository Topology](./Repository-Topology.md) |

## Reference (cross-cutting)

- [Reference: Error Codes](./Reference-Error-Codes.md) — every `<DOMAIN>-<NAME>` code, mapped to status code + HTTP
- [Reference: Status Codes](./Reference-Status-Codes.md) — `NPS-*` status family
- [Reference: Frame Registry](./Reference-Frame-Registry.md) — every frame type byte and its owning protocol
- [Reference: Cognon Budget](./Reference-Cognon-Budget.md) — token-cost model (formerly NPT, renamed in alpha.5.2)

## Per-protocol pages

- [NCP](./Protocol-NCP.md) — Neural Communication Protocol (framing, encoding, native + HTTP modes)
- [NWP](./Protocol-NWP.md) — Neural Web Protocol (HTTP-equivalent surface; topology queries)
- [NIP](./Protocol-NIP.md) — Neural Identity Protocol (NID certs, assurance levels, reputation log)
- [NDP](./Protocol-NDP.md) — Neural Discovery Protocol (DNS TXT, AnnounceFrame, GraphFrame)
- [NOP](./Protocol-NOP.md) — Neural Orchestration Protocol (DAG dispatch, delegation chains)

## Per-language SDKs

- [.NET / C#](./SDK-DotNet.md)
- [Python](./SDK-Python.md)
- [TypeScript](./SDK-TypeScript.md)
- [Java](./SDK-Java.md)
- [Go](./SDK-Go.md)
- [Rust](./SDK-Rust.md)

## Per-daemon pages

- [npsd](./Daemon-NPSd.md) — host-local identity, inbox, and protocol daemon
- [nps-runner](./Daemon-NPS-Runner.md) — task executor
- [nps-ingress](./Daemon-NPS-Ingress.md) — native TLS/mTLS NCP ingress
- [nps-registry](./Daemon-NPS-Registry.md) — node / member registry
- [nps-ledger](./Daemon-NPS-Ledger.md) — NID reputation log + STH gossip
- [nps-cloud-ca](./Daemon-NPS-Cloud-CA.md) — private NPS Cloud CA
- [nip-ca-server](./Daemon-NIP-CA-Server.md) — OSS reference NIP CA
- [bundle-overlay](./Daemon-Bundle-Overlay.md) — release packaging meta

## Suite version

This handbook tracks the suite version declared in
[`NPS/version.yaml`](https://github.com/labacacia/NPS/blob/main/version.yaml)
— latest published SDK packages are **v1.0.0-alpha.19**. This handbook is reviewed
against the **released v1.0.0-alpha.19 suite** (2026-09-06): NCP 0.12, NWP 0.22,
NIP 0.15, NDP 0.13, and NOP 0.10. It covers the portable six-SDK runtime,
current hardening vectors, daemon debt closure, multi-Anchor HA, bidirectional
Bridge semantics, and the CR-0011 stateful LLM context contract.

Pages are re-reviewed at each suite release. Normative protocol details always
come from `NPS/spec`; package availability comes from the relevant
registry or GitHub release.

For the release boundary, verification evidence, and explicit non-claims, see
[Alpha.19 Current Status](./Alpha19-Current-Status.md).

*Wiki current-status reconciliation: released v1.0.0-alpha.19 through NPS-Dev
PR #122 (2026-09-06).*

---

> **Handbook hygiene rule.** This handbook complements but never substitutes for the
> normative `spec/` documents. When a wiki page and a `spec/` document
> disagree, **the spec wins**; open an issue against the handbook page to fix it.
