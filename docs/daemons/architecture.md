English | [中文版](./architecture.cn.md)

# NPS Daemon Architecture

> Reference deployment model with six conceptual roles across three layers.
> The active distribution currently ships four roles in NPS-Daemons and the
> trust-anchor role in NIP-CA-Server. The former Cloud CA and Ledger prototypes
> are archived; their sections below preserve design history, not deployable
> release-train components.
>
> Status — alpha.18 source / alpha.19 debt-closure boundary: `npsd` multiplexes
> HTTP and local-dev native NCP on its configured loopback port, with bounded
> preamble/Hello negotiation and Anchor ACK/cache. Its per-NID inbox now uses
> durable SQLite with restart-safe TTL, priority and acknowledgement semantics.
> Resident/hybrid push is deliberately declined for this ephemeral L1 profile,
> and full Node L1 remains unclaimed. `nps-ingress` implements the native
> TLS 1.3 / ALPN `nps/1.0` / mTLS / session-NID binding transport boundary.
> The applicable `TC-N2-Tls-01..04` family is executable; broader admission
> roadmap items are explicitly dispositioned and not claimed as transport
> capabilities. `nps-runner` implements inbox dispatch, portable OCI SpawnSpec
> resolution/execution, bounded worker lifecycle, and durable SQLite task leases
> with cross-process claim, restart/reclaim, terminal dedup and stale-owner
> fencing evidence. Full TaskFrame DAG/Saga L3 certification is not claimed;
> its per-case manifest records the exact boundary. Other daemon closure work
> is tracked separately.

---

## The three layers

```
┌──────────────────────────────────────────────────────────────────────┐
│ Layer 3 — Trust anchor       (NPS Cloud, 2027 Q1+)                   │
│ ┌──────────────────┐                  ┌──────────────────┐           │
│ │ ⑤ nps-cloud-ca   │                  │ ⑥ nps-ledger     │           │
│ │ X.509 issuance   │                  │ K-of-N audit log │           │
│ │ CRL / OCSP       │                  │ tamper-evident   │           │
│ └────────┬─────────┘                  └────────┬─────────┘           │
└──────────┼─────────────────────────────────────┼─────────────────────┘
           ▼                                     ▼
┌──────────────────────────────────────────────────────────────────────┐
│ Layer 2 — Network entry       (per ingress kind, on demand)          │
│ ┌──────────────────┐                  ┌──────────────────┐           │
│ │ ③ nps-ingress    │                  │ ④ nps-registry   │           │
│ │ Public Internet  │                  │ Cross-machine    │           │
│ │ NPS-over-TLS     │                  │ NDP resolve+graph│           │
│ │ TLS + NID bind   │                  │ (L2 stage)       │           │
│ └────────┬─────────┘                  └────────┬─────────┘           │
└──────────┼─────────────────────────────────────┼─────────────────────┘
           ▼                                     ▼
┌──────────────────────────────────────────────────────────────────────┐
│ Layer 1 — Host-local          (one of each per machine)              │
│ ┌──────────────────┐                  ┌──────────────────┐           │
│ │ ① npsd           │ ◀── inbox ─────  │ ② nps-runner     │           │
│ │ NCP wire +       │                  │ FaaS scheduler   │           │
│ │ root keypair +   │ ── spawn-spec ─▶ │ ephemeral worker │           │
│ │ session reg +    │                  │ lifecycle (L3)   │           │
│ │ inbox            │                  │                  │           │
│ │ port :17433      │                  │                  │           │
│ └──────────────────┘                  └──────────────────┘           │
└──────────────────────────────────────────────────────────────────────┘
```

## Six conceptual roles

### Layer 1 — Host-local (one of each per machine)

#### ① `npsd` — Protocol ingress + state host

- **Listens on** `127.0.0.1:17433` by default and losslessly multiplexes HTTP control traffic with local-dev native NCP on that unified port.
- **Implemented native boundary**: bounded [NPS-RFC-0001](https://github.com/labacacia/NPS/blob/main/spec/rfcs/NPS-RFC-0001-ncp-connection-preamble.md) preamble and Hello/Caps negotiation, negotiated encoding/payload enforcement, canonical AnchorFrame ACK/cache, and deterministic silent/error close behavior. Public TLS 1.3/mTLS termination belongs to `nps-ingress`.
- **Implemented state boundary**: root Ed25519 keypair management; sub-NID issuance and bounded pre-expiry renewal; publisher-signed ephemeral NDP announcements with persistent monotonic sequence; and a durable per-NID SQLite inbox used for `ephemeral` HTTP pull with explicit acknowledgement. Managed agent keys are encrypted at rest. Identity/announcement sequence, undelivered frames, absolute TTL, priority order and acknowledgements survive daemon restart.
- **Explicit limit**: full Node L1 is not claimed. All 20 L1 cases, their current evidence states, runnable gates and remaining gaps are recorded in [`NPS-NODE-L1-MANIFEST.json`](https://github.com/labacacia/NPS-Dev/blob/main/tools/daemons/npsd/conformance/NPS-NODE-L1-MANIFEST.json). Resident/hybrid push is optional at L1 and explicitly declined by this profile. BYO-key agents are not auto-announced because npsd does not possess their private key and must not sign as the publisher.
- **Why always-on**: identity and inbox cannot follow a session — persistent state needs a host. Every NPS client on the machine (MCP shim, resident agent, worker, ingress shim) connects through this daemon.
- **Why not merged into anything else**: protocol layer is business-agnostic and shared by all upper layers; folding it into a business process pollutes the trust domain.
- **Reference compliance**: target is `NPS-Node Profile L1` (see [`spec/services/NPS-Node-Profile.md`](https://github.com/labacacia/NPS/blob/main/spec/services/NPS-Node-Profile.md) and the L1 conformance suite at [`spec/services/conformance/NPS-Node-L1.md`](https://github.com/labacacia/NPS/blob/main/spec/services/conformance/NPS-Node-L1.md)).

#### ② `nps-runner` — Task scheduler / FaaS runtime *(L3 stage)*

- **Implemented runtime boundary**: self-registers with local `npsd`, polls its
  inbox, validates inline or referenced portable OCI SpawnSpecs, executes OCI
  containers (with a legacy direct-subprocess compatibility path), bounds
  concurrency/idle/max-runtime, captures logs, and reports completion.
- **Implemented ownership behavior**: atomically claims and periodically renews
  leases in a persistent SQLite file shared by coordinating runner processes.
  Fresh per-start instance identities fence stale processes; terminal records
  survive restart; lease loss cancels the worker without terminal write, inbox
  ack, or notification.
- **Explicit limit**: full Node L3 certification is not claimed because the
  current inbox contract executes individual SpawnSpecs rather than multi-node
  TaskFrames. The exact verified/partial/unexecuted cases are in
  [`NPS-NODE-L3-MANIFEST.json`](https://github.com/labacacia/NPS-Dev/blob/main/tools/daemons/nps-runner/conformance/NPS-NODE-L3-MANIFEST.json),
  with the broader capability boundary in
  [`NPS-RUNNER-CAPABILITIES.json`](https://github.com/labacacia/NPS-Dev/blob/main/tools/daemons/nps-runner/conformance/NPS-RUNNER-CAPABILITIES.json).
- **Why always-on**: messages arrive any time; the scheduler must be there to spawn. Workers are ephemeral but the scheduler is not.
- **Why separated from `npsd`**: resource profile is radically different — `npsd` is steady-state, `nps-runner` produces process-spawn bursts; failure isolation is critical (a worker crash must not take the NCP layer down); trust boundary differs (`nps-runner` runs user-supplied Agent SDK code, the protocol layer must not have that permission surface).

### Layer 2 — Network entry (per ingress kind, on demand)

#### ③ `nps-ingress` — Internet ingress

- **Implemented transport boundary**: terminates native NCP over TLS 1.3 with ALPN `nps/1.0`; default-on mTLS validates NIP client certificates to configured trust anchors; the post-Caps `IdentFrame` NID is bound to the certificate NID before that Ident is forwarded; handshake time/frame size and pre-admission framing are bounded; accepted streams are proxied to the local NCP backend and responses are drained after client half-close. `TC-N2-Tls-01..04` execute over real sockets.
- **Boundary disposition**: topology, Bridge, HA and Registry families apply to other IUT roles. Rate limiting, NeuronHub customer auth, CGN debit, reputation, Anchor middleware and broad DDoS controls are not advertised as transport capabilities; their product/AaaS/optional-composition/deployment ownership is recorded in [`NPS-INGRESS-ADMISSION-DISPOSITION.json`](https://github.com/labacacia/NPS-Dev/blob/main/tools/daemons/nps-ingress/conformance/NPS-INGRESS-ADMISSION-DISPOSITION.json).
- **Why always-on**: the publicly bound port must always be listening.
- **Why separated from `npsd`**: `npsd` is bound to loopback by default; the ingress daemon binds public addresses. Attack surface, security posture, and operational scope differ. `npsd` failure affects all local NPS traffic; ingress failure only affects external inbound — the two MUST be independently restartable.
- **Note**: This is a **process-level** name; the *spec*-level role of cluster control plane is now called **Anchor Node** in NWP, see [NPS-CR-0001](https://github.com/labacacia/NPS/blob/main/spec/cr/NPS-CR-0001-anchor-bridge-split.md). The `nps-ingress` process MAY host an Anchor Node middleware but is not constrained to.

#### ④ `nps-registry` — Discovery registry *(L2 stage, optionally hosted)*

- **Centralised** NDP discovery service for cross-machine resolution. Responds to `Resolve` and `Graph` queries; aggregates registrations from multiple machines.
- **Per-host `npsd` only knows local sessions**; cross-machine queries go to a registry.
- **Why always-on**: discovery requests arrive arbitrarily; the registry cannot cold-start.
- **Why independent of `npsd`**: `npsd` is one-per-machine; registry is one-per-network (or HA cluster). Not needed at L1; appears at L2. NeuronHub will run one for its customers; LabAcacia will run a public-good open-source one.

### Layer 3 — Trust anchor *(NPS Cloud, 2027 Q1+)*

#### ⑤ `nps-cloud-ca` — Certificate authority (archived design role)

- **NIP trust root** that issues cross-organisation NID certificates and serves CRL / OCSP. LabAcacia operates an open-source instance; commercial users may operate private CAs.
- **Why always-on**: revocation queries must respond in real time; issuance requests arrive arbitrarily.
- **Why independent of `nps-registry`**: CA is the *source* of trust; registry is an *index* of information. CA failure prevents new NID onboarding and revocation responses; registry failure prevents discovery only. Trust level, operations process, audit requirements all differ. CA private keys MUST be HSM- or physical-security-protected; registry has no equivalent constraint.

#### ⑥ `nps-ledger` — Audit / compliance log collector (archived design role)

- **Append-only collection point** for the K-of-N audit consensus that NeuronHub CGN settlement ultimately depends on. Implements the Certificate-Transparency-style log defined by [NPS-RFC-0004](https://github.com/labacacia/NPS/blob/main/spec/rfcs/NPS-RFC-0004-nid-reputation-log.md): submission, query, signed tree head, inclusion proofs.
- **Why always-on**: audit logs are a continuous append stream; the receiver cannot be down or there will be gaps.
- **Why independent**: audit isolation requirements are the strongest of all six daemons — `nps-ledger` MUST NOT trust the processes it audits, so it must run in its own trust domain. Even if `npsd` is compromised, `nps-ledger` should still produce independent evidence.

---

## Naming and binary layout

| Daemon | Folder | Project | Binary | NuGet package |
|--------|--------|---------|--------|----------------|
| ① | `NPS-Daemons/npsd/` | `Npsd.csproj` | `npsd` | `LabAcacia.NPS.Daemon.Npsd` |
| ② | `NPS-Daemons/nps-runner/` | `NpsRunner.csproj` | `nps-runner` | `LabAcacia.NPS.Daemon.Runner` |
| ③ | `NPS-Daemons/nps-ingress/` | `NpsIngress.csproj` | `nps-ingress` | `LabAcacia.NPS.Daemon.Ingress` |
| ④ | `NPS-Daemons/nps-registry/` | `NpsRegistry.csproj` | `nps-registry` | `LabAcacia.NPS.Daemon.Registry` |
| ⑤ | `NIP-CA-Server/` | `NPS.NipCaServer.csproj` | `NPS.NipCaServer` | NIP CA packages |
| ⑥ | archived | — | — | design history only |

## Historical alpha-to-beta phasing record

The table below preserves the alpha.4 planning snapshot; it is not a current
capability matrix. Current `nps-ingress` and `nps-runner` behavior and gaps are
stated in their sections above and in the daemon READMEs.

| Daemon | alpha.3 | alpha.4 (this release) | alpha.11+ | beta / 1.0 |
|--------|---------|------------------------|----------|------------|
| `npsd` | L1 minimum: HTTP listener on `127.0.0.1:17433`, root keypair generation, `/.nwm`, baseline `/health`, AnnounceFrame on local NDP | L1+: sub-NID issuance, per-NID inbox queue, NCP native-mode preamble runtime (NPS-RFC-0001) | Push to resident agents | Conformance with full L1 + L2 |
| `nps-runner` | Skeleton + Generic-Host worker; documents the inbox-watch contract | Inbox poller wired through `npsd` | Spawn lifecycle, isolation | L3 conformance |
| `nps-ingress` | Skeleton + HTTP listener on `0.0.0.0:443` (TLS off); documents NeuronHub integration points | Anchor Node middleware (NPS-CR-0001 wiring) | Reputation lookup (NPS-RFC-0004) | DDoS, CGN debit |
| `nps-registry` | Skeleton + HTTP listener; placeholder `Resolve`/`Graph` endpoints returning `NDP-REGISTRY-UNAVAILABLE` | **Real SQLite-backed registry**: Announce / Resolve / Graph all live; TTL lazy expiry; monotonic graph seq; file or in-memory via env | HA cluster mode | Federation |
| `nps-cloud-ca` | Skeleton; defers to `tools/nip-ca-server*` (six per-language OSS CAs) for actual issuance | NPS-RFC-0002 X.509 + ACME wiring (skeleton; full issuance in nip-ca-server) | HSM integration | Cross-CA trust |
| `nps-ledger` | Skeleton + in-memory log honouring the NPS-RFC-0004 Phase 1 entry shape | **Phase 2**: SQLite persistence, RFC 9162 Merkle tree, operator-signed STH, `/v1/log/proof` inclusion proof endpoint | STH gossip, public mirror | Public log accreditation |

---

*Copyright: LabAcacia / INNO LOTUS PTY LTD · Apache 2.0*
