# SDK — Go

**Status:** ✅ Latest published module — v1.0.0-alpha.19 (released 2026-09-06)

> **Alpha.19 release:** this SDK executes all 47
> shared P19-1 hardening vectors for NCP 0.12, NWP 0.22, NIP 0.15, NDP 0.13
> and NOP 0.10. The install command below pins the published alpha.19 package. See [Alpha.19 Current Status](./Alpha19-Current-Status.md).

Go reference implementation of the Neural Protocol Suite. Covers all five sub-protocols: NCP, NWP, NIP, NDP, and NOP.

---

## Published capability set (accumulated through alpha.19)

Everything in this section is present in the published `v1.0.0-alpha.19` module; the release tag on each entry is where that capability first landed. Beyond the alpha.13 client baseline, the Go SDK carries the following capability-level additions (exact identifier names may differ by language — see the source):

- **NCP Tier-3 BinaryVector (`binary_vector.v1`)** (NCP v0.9, alpha.14) — a third encoding tier for compact float-vector (embedding) payloads on `QueryFrame`. Negotiated via caps and only used when both peers advertise `binary_vector.v1`. Malformed payloads surface as documented client errors (`NCP-BINARY-VECTOR-*` → `NPS-CLIENT-BAD-FRAME`); the reserved tier bits return `NCP-FRAME-FLAGS-INVALID`. The Go native-serving path was hardened in alpha.15 (correct extended-header bit, no shared-tier mutation, rejects non-finite float32).
- **Inbound NWP Bridge server adapters** (alpha.14) — lets external MCP / A2A clients call local NPS actions (the inverse of the outbound Bridge Node). Secure-by-default: valid `X-NWP-Agent` NID + a configured verifier, bounded request bodies (→ 413), dispatch timeout (→ 504), sanitized client errors, and an action allowlist. See [SDK Building a Bridge Node](./SDK-Building-a-Bridge-Node.md).
- **Native-mode NWP serving** (alpha.14) — Memory / Action Nodes serve `QueryFrame` / `ActionFrame` directly over a native NCP session rather than a hand-rolled frame loop.
- **NIP signed-payload realignment** (alpha.15, **breaking**) — TrustFrame / RevokeFrame now sign the current NPS-3 field set (`issued_at`, `serial`, `signer_nid`, `target_nid`) and use current revocation naming (`NIP-CERT-REVOKED`). Signed frames produced by the old alpha.14-era shape no longer verify. See [SDK Identity and Authentication](./SDK-Identity-and-Authentication.md).
- **NDP AnnounceFrame signed canonical form** (alpha.15, **breaking**) — the signed body is now normative and cross-SDK consistent (sign all wire fields except `signature` / `health` / `last_seen` / `frame`; omit null optionals; `heartbeat_interval_ms` canonicalised to the default `60000` only when absent, explicit `0` signed literally).
- **Server and orchestration parity with the .NET reference** (alpha.17) — native NCP transport, NWP Action / Complex / Memory Nodes, bidirectional Bridges, NIP CA plus full verification, NOP orchestration, and daemon observability/telemetry, on the existing `net/http` server binding.
- **Portable profiles and shared conformance vectors** (alpha.17) — the Go SDK executes the same language-neutral fixtures as the other five SDKs for NCP 0.11 native-server handshakes, NWP 0.20 Node/Bridge serving, NIP 0.13 CA/revocation, NDP 0.12 registry admission, and NOP 0.9 orchestration.
- **NPS-CR-0011 / NWP 0.21 stateful LLM context** (alpha.18) — owner-bound opaque context IDs with `create` / `append` / `fork` / `reset` / `status` / `release`, compare-and-swap versions, atomic unary and async cancellation, NWM 0.2 discovery, and `llm:context` authorization under NIP 0.14, validated against the 19 shared CR-0011 conformance vectors. Stateless completion stays compatible; stateful requests never silently fall back. Stateful NDJSON streaming commits the terminal frame atomically, aborts (never caches) failed or incomplete streams, and replays a completed sequence idempotently under a fresh server-owned `stream_id`.
- **Official NWP LLM usage telemetry** (alpha.18) — `input_tokens`, `output_tokens`, prefix/KV-cache hit, reused tokens, and evaluated tokens, plus unary `CapsFrame.request_id` correlation echoed by the native NWP server helpers; `CapsFrame.cached` stays distinct from model prefix/KV-cache reuse. Adds `NPS-LIMIT-RESOURCE` for bounded live-object limits and `wire_input_bytes` on the LLM usage DTO.

---

## Installation

```bash
go get github.com/labacacia/NPS-sdk-go@v1.0.0-alpha.19
```

**Requirements:** Go 1.23+ (alpha.18 lowered the support floor from 1.25 and pinned the newest compatible `golang.org/x/crypto` line; the minimum toolchain is covered by the release gate).

**Tests:** 106 passing.

**Runtime version boundary:** the published alpha.19 module exports the
compile-time `core.Version` API:

```go
import "github.com/labacacia/NPS-sdk-go/core"

fmt.Println(core.Version)
```

The constant is mechanically checked against the module `VERSION` file and is
updated with it during release preparation. Applications may also use Go build
information (`runtime/debug.ReadBuildInfo`) for dependency-version diagnostics.

---

## Package layout

| Package | Protocol | Description |
|---------|----------|-------------|
| `github.com/labacacia/NPS-sdk-go/core` | NCP | Frame types, header codec, `FrameRegistry`, `AnchorFrameCache` |
| `github.com/labacacia/NPS-sdk-go/ncp` | NCP | `AnchorFrame`, `DiffFrame`, `StreamFrame`, `CapsFrame`, `HelloFrame` (`PingIntervalMs`), `NopFrame` (0x07 keepalive), `ErrorFrame` |
| `github.com/labacacia/NPS-sdk-go/nwp` | NWP | `QueryFrame`, `ActionFrame`, `NwpClient` (HTTP mode; reads `X-NWM-Version` and `manifest_version`/`manifest_updated_at` for conditional `.nwm` re-fetch); `ErrAuth*` / `ErrQuery*` / … error code constants |
| `github.com/labacacia/NPS-sdk-go/nip` | NIP | `IdentFrame` (v2 dual-trust; `NodeRoles` self-declared role tags), `TrustFrame`, `RevokeFrame`, `NipIdentity` (Ed25519), `NipIdentVerifier` (RFC-0002 §8.1 dual-trust), `AssuranceLevel` (RFC-0003), `ReputationLogClient` (RFC-0004 Phase 2, added in alpha.7) |
| `github.com/labacacia/NPS-sdk-go/nip/x509` | NIP / RFC-0002 | `IssueLeaf`, `IssueRoot`, `Verify` — NPS X.509 NID certs on stdlib `crypto/x509` (anchored to IANA PEN 65715) |
| `github.com/labacacia/NPS-sdk-go/nip/acme` | NIP / RFC-0002 | `Client` + `Server` (in-process) + JWS/messages — ACME `agent-01` flow |
| `github.com/labacacia/NPS-sdk-go/ndp` | NDP | `AnnounceFrame` (`SpawnSpecRef` structured schema object; `HeartbeatIntervalMs`), `ResolveFrame`, `GraphFrame`, `InMemoryNdpRegistry`, `NdpAnnounceValidator`; DNS TXT fallback (`ResolveViaDns`, `DnsTxtLookup`, `ParseNpsTxtRecord`) |
| `github.com/labacacia/NPS-sdk-go/nop` | NOP | `TaskFrame` (`ResultTtlSeconds`), `DelegateFrame`, `SyncFrame`, `AlignStreamFrame`, `NopClient` |

---

## Key types and functions

### `NpsFrameCodec` — encode an AnchorFrame

```go
import (
    "github.com/labacacia/NPS-sdk-go/core"
    "github.com/labacacia/NPS-sdk-go/ncp"
)

reg   := core.CreateFullRegistry()
codec := core.NewNpsFrameCodec(reg)

frame := &ncp.AnchorFrame{
    AnchorID: "sha256:abc123",
    Schema:   core.FrameDict{"type": "object", "version": "1"},
    TTL:      3600,
}

wire, err := codec.Encode(frame.FrameType(), frame.ToDict(), core.EncodingTierMsgPack, true)
// wire is ready to send over the network

ft, dict, err := codec.Decode(wire)
received := ncp.AnchorFrameFromDict(dict)
```

### `AnchorFrameCache`

```go
cache := core.NewAnchorFrameCache()

schema   := core.FrameDict{"type": "object", "fields": []any{"name", "value"}}
anchorID, err := cache.Set(schema, 3600) // 1-hour TTL

schema, err = cache.GetRequired(anchorID) // returns error if expired
```

### `NwpClient`

```go
import "github.com/labacacia/NPS-sdk-go/nwp"

client := nwp.NewNwpClient("http://node.example.com:17433")

// Query
qf := &nwp.QueryFrame{AnchorRef: "sha256:abc123", Filters: map[string]any{"status": "active"}}
capsFrame, err := client.Query(ctx, qf)

// Stream
frames, err := client.Stream(ctx, qf)
for _, sf := range frames {
    fmt.Println(sf.Payload)
}

// Invoke (sync action)
af := &nwp.ActionFrame{Action: "create", Payload: map[string]any{"name": "item"}}
result, err := client.Invoke(ctx, af)

// Async invoke — check result.Async.TaskID
af.Async = true
result, err = client.Invoke(ctx, af)
fmt.Println(result.Async.TaskID)
```

`NwpClient` uses `context.Context` for cancellation throughout. Pass a timeout context to enforce deadlines.

### `NipIdentity`

```go
import "github.com/labacacia/NPS-sdk-go/nip"

id, err := nip.Generate()
fmt.Println(id.PubKeyString()) // "ed25519:<hex>"

// Sign a frame dict
payload := core.FrameDict{"nid": "urn:nps:node:example.com:agent", "pub_key": id.PubKeyString()}
sig := id.Sign(payload)
ok  := id.Verify(payload, sig)

// Verify with just the public key string (no private key needed)
ok = nip.VerifyWithPubKeyStr(payload, "ed25519:<hex>", sig)

// Save / Load (AES-256-GCM + PBKDF2-SHA256, 600k iterations)
err = id.Save("/path/to/identity.json", "my-passphrase")
loaded, err := nip.Load("/path/to/identity.json", "my-passphrase")
```

### `AssuranceLevel` — empty-string case

The Go SDK uses `if wire == ""` to guard the empty-string case:

```go
import "github.com/labacacia/NPS-sdk-go/nip"

level := nip.AssuranceLevelFromWire("")    // → nip.AssuranceLevelAnonymous
level  = nip.AssuranceLevelFromWire("L1") // → nip.AssuranceLevelL1
```

An empty string returns `Anonymous` rather than an error.

### DNS TXT fallback (`ResolveViaDns`)

```go
import "github.com/labacacia/NPS-sdk-go/ndp"

result := ndp.ResolveViaDns("nwp://example.com/agent", nil)
// result.Host, result.Port, result.Protocol
// pass a DnsTxtLookup implementation to inject a mock in tests
```

### `NopClient` — orchestration

```go
import "github.com/labacacia/NPS-sdk-go/nop"

client := nop.NewNopClient("http://orchestrator.example.com:17433")

tf := &nop.TaskFrame{
    TaskID: "task-" + uuid,
    DAG:    map[string]any{...},
}
taskID, err := client.Submit(ctx, tf)

// Poll status
status, err := client.GetStatus(ctx, taskID)
fmt.Println(status.State()) // "running"

// Wait for completion (polls every 500ms)
status, err = client.Wait(ctx, taskID, nil)
fmt.Println(status.State())        // "completed"
fmt.Println(status.NodeResults())  // map[string]any

err = client.Cancel(ctx, taskID)
```

### Backoff strategies (NOP)

```go
delay := nop.ComputeDelayMs(nop.BackoffExponential, 100, 5000, attempt)
// Also: nop.BackoffFixed, nop.BackoffLinear
```

---

## Idiomatic Go patterns

- All errors are returned as the last return value — no panics in the hot path.
- `context.Context` is the first argument on every network call; use it for cancellation and deadlines.
- Streaming results are returned as slices (`[]StreamFrame`) rather than channels; integrate with your own channels if you need fan-out.
- The `DnsTxtLookup` interface on `ResolveViaDns` lets you inject any DNS backend, including test stubs.

---

## Frame type reference

| Frame | Type code | Package |
|-------|-----------|---------|
| `AnchorFrame` | 0x01 | `ncp` |
| `DiffFrame` | 0x02 | `ncp` |
| `StreamFrame` | 0x03 | `ncp` |
| `CapsFrame` | 0x04 | `ncp` |
| `HelloFrame` | 0x06 | `ncp` |
| `NopFrame` | 0x07 | `ncp` |
| `ErrorFrame` | 0xFE | `ncp` |
| `QueryFrame` | 0x10 | `nwp` |
| `ActionFrame` | 0x11 | `nwp` |
| `IdentFrame` | 0x20 | `nip` |
| `TrustFrame` | 0x21 | `nip` |
| `RevokeFrame` | 0x22 | `nip` |
| `AnnounceFrame` | 0x30 | `ndp` |
| `ResolveFrame` | 0x31 | `ndp` |
| `GraphFrame` | 0x32 | `ndp` |
| `TaskFrame` | 0x40 | `nop` |
| `DelegateFrame` | 0x41 | `nop` |
| `SyncFrame` | 0x42 | `nop` |
| `AlignStreamFrame` | 0x43 | `nop` |

---

## Running tests

```bash
go test ./...
```

---

## See also

- [SDK Quickstart](./SDK-Quickstart.md) — language-agnostic first steps and install table
- [SDK Identity and Authentication](./SDK-Identity-and-Authentication.md) — NipIdentity, IdentFrame, trust chain
- [SDK Common Patterns](./SDK-Common-Patterns.md) — anchor cache, streaming, DNS TXT fallback

---

*Last reviewed at suite version: v1.0.0-alpha.19*
