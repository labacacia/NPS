# The Five Protocols

> English | [中文版](protocols.cn.md)

NPS is split into five layers so each concern has a single home. The dependency chain is:

```
NCP ← NWP ← NIP ← NDP
NCP + NWP + NIP ← NOP
```

Every protocol uses the same default port (**17433**) and routes on the 1-byte frame type field.

---

## NCP — Neural Communication Protocol

**Role:** Wire format and framing. The closest analogue is **HTTP/2 frames**.

NCP defines the 4-byte (or 8-byte EXT) frame header, the 1-byte flags byte, the two encoding tiers (Tier-1 JSON and Tier-2 MsgPack), and the five core frame types.

| Frame | Code | Purpose |
|-------|------|---------|
| `AnchorFrame`  | 0x01 | Publishes a content-addressed schema |
| `DiffFrame`    | 0x02 | Evolves an anchor — schema migration |
| `StreamFrame`  | 0x03 | One chunk of a streamed response |
| `CapsFrame`    | 0x04 | Node capability / response envelope |
| `ErrorFrame`   | 0xFE | Unified error across every protocol |

**Key design decisions:**

- Frame size: 64 KB default (EXT=0); 4 GB extended (EXT=1) for large payloads
- Encoding: MsgPack by default in production (~60 % smaller than JSON); JSON for debug and interop
- `AnchorFrame` TTL defaults to 3600 seconds

Read the full spec: [NPS-1 NCP](https://github.com/labacacia/NPS/blob/main/spec/NPS-1-NCP.md).

---

## NWP — Neural Web Protocol

**Role:** Request / response semantics. The analogue is **HTTP**.

NWP defines how an agent queries or acts against a node, carrying the `anchor_ref` that identifies which schema the exchange is bound to.

| Frame | Code | Purpose |
|-------|------|---------|
| `QueryFrame`  | 0x10 | Paginated / filtered query against a Memory Node |
| `ActionFrame` | 0x11 | Invoke an action (sync or async) on a node |

**Four node types:**

- **Memory Node** — owns an anchor and serves queries (your product catalog, your KB)
- **Action Node** — exposes callable actions (send email, place order)
- **Anchor Node** — stateless router to NOP DAGs (renamed from Gateway Node by NPS-CR-0001) and **Bridge Node** — NPS↔non-NPS protocol translation
- **Agent Node** — speaks NWP + NOP, often an LLM-driven executor

Read the full spec: [NPS-2 NWP](https://github.com/labacacia/NPS/blob/main/spec/NPS-2-NWP.md).

---

## NIP — Neural Identity Protocol

**Role:** Cryptographic identity. The analogue is **TLS + PKI**.

Every node and every agent has an Ed25519 keypair and an **NID** — a URN of the form `urn:nps:node:{authority}:{name}`. Identity is not bolted on; `IdentFrame` is a first-class frame.

| Frame | Code | Purpose |
|-------|------|---------|
| `IdentFrame`  | 0x20 | Node identity declaration |
| `TrustFrame`  | 0x21 | Delegation / trust assertion (with scopes + expiry) |
| `RevokeFrame` | 0x22 | Revoke an NID |

**Why Ed25519:** performance-first. Agents verify signatures at high frequency — RSA-2048 would burn milliseconds per frame.

**Key storage:** the reference implementation persists keys with AES-256-GCM + PBKDF2-SHA256 at 600,000 iterations. A standalone **NIP CA Server** ships in six languages (C# / Python / TypeScript / Java / Rust / Go).

Read the full spec: [NPS-3 NIP](https://github.com/labacacia/NPS/blob/main/spec/NPS-3-NIP.md).

---

## NDP — Neural Discovery Protocol

**Role:** Node discovery. The analogue is **DNS**.

An agent needs to resolve `nwp://api.example.com/products` to an actual (host, port, protocol) tuple. NDP is how that resolution works without hard-coding a central registry.

| Frame | Code | Purpose |
|-------|------|---------|
| `AnnounceFrame` | 0x30 | Publish a node's reachability + capabilities + TTL |
| `ResolveFrame`  | 0x31 | Ask or answer "what is the address of this NID?" |
| `GraphFrame`    | 0x32 | Topology sync between resolvers |

**Announces are signed** — a resolver validates the signature against a known public key before accepting the record. TTL = 0 means "shutting down, evict me."

Read the full spec: [NPS-4 NDP](https://github.com/labacacia/NPS/blob/main/spec/NPS-4-NDP.md).

---

## NOP — Neural Orchestration Protocol

**Role:** Multi-agent task orchestration. The analogue is **SMTP + message queues + workflow engines** rolled together.

NOP takes a DAG — nodes execute actions, edges pass outputs — and provides delegation, fan-in barriers, streaming progress, and signed delegation chains.

| Frame | Code | Purpose |
|-------|------|---------|
| `TaskFrame`        | 0x40 | Submit a DAG for execution |
| `DelegateFrame`    | 0x41 | Per-node invocation emitted by the orchestrator to an agent |
| `SyncFrame`        | 0x42 | Fan-in barrier — wait for K-of-N upstream subtasks |
| `AlignStreamFrame` | 0x43 | Streaming progress / partial result from a subtask |

**Hard limits (NPS-5 §8.2):**

- Max 32 nodes per DAG
- Max 3 levels of delegate chain
- Max timeout 3,600,000 ms (1 hour)
- `callback_url` is SSRF-validated by the orchestrator

Read the full spec: [NPS-5 NOP](https://github.com/labacacia/NPS/blob/main/spec/NPS-5-NOP.md).

---

## Frame Type Namespace (one-page reference)

| Range | Protocol | Frames |
|-------|----------|--------|
| `0x01–0x0F` | NCP | Anchor, Diff, Stream, Caps (Hello reserved) |
| `0x10–0x1F` | NWP | Query, Action |
| `0x20–0x2F` | NIP | Ident, Trust, Revoke |
| `0x30–0x3F` | NDP | Announce, Resolve, Graph |
| `0x40–0x4F` | NOP | Task, Delegate, Sync, AlignStream |
| `0xFE`      | System | ErrorFrame — shared across all layers |

Machine-readable registry: [frame-registry.yaml](https://github.com/labacacia/NPS/blob/main/spec/frame-registry.yaml).

---

## Related documents

- [Error codes](https://github.com/labacacia/NPS/blob/main/spec/error-codes.md) — `{PROTOCOL}-{CATEGORY}-{DETAIL}` namespace
- [Status codes](https://github.com/labacacia/NPS/blob/main/spec/status-codes.md) — NPS native status codes + HTTP mapping
- [Token Budget](https://github.com/labacacia/NPS/blob/main/spec/token-budget.md) — CGN metering specification
- [AaaS Profile](https://github.com/labacacia/NPS/blob/main/spec/services/NPS-AaaS-Profile.md) — Agent-as-a-Service compliance levels (L1 / L2 / L3)

---

📖 For tutorials, references, and operator guides, see the [NPS handbook](https://github.com/labacacia/NPS/blob/main/docs/handbook/Home.md).
