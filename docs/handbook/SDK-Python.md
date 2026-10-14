# SDK — Python

**Status:** ✅ Latest published package — v1.0.0-alpha.19 (released 2026-09-06)

> **Alpha.19 release:** this SDK executes all 47
> shared P19-1 hardening vectors for NCP 0.12, NWP 0.22, NIP 0.15, NDP 0.13
> and NOP 0.10. The install command below pins the published alpha.19 package. See [Alpha.19 Current Status](./Alpha19-Current-Status.md).

Python client library for the Neural Protocol Suite. Covers all five protocols: NCP, NWP, NIP, NDP, and NOP.

---

## Installation

```bash
pip install nps-lib==1.0.0a19
```

For development extras (pytest, coverage, linting):

```bash
pip install "nps-lib[dev]==1.0.0a19"
```

> **Package name:** The PyPI distribution is `nps-lib`. The name `nps-sdk` is taken by an unrelated package (Ingenico). The Python import namespace is always `nps_sdk`.

**Requirements:** Python 3.11+. Dependencies: `msgpack`, `httpx`, `cryptography`.

**Tests:** 221+ passing, ≥ 90% coverage target.

> **Suite version:** This SDK tracks suite `v1.0.0-alpha.19` (NCP 0.11 · NWP 0.21 · NIP 0.14 · NDP 0.12 · NOP 0.9). alpha.12 was withdrawn; pin `nps-lib==1.0.0a19`.

---

## Module layout

| Module | Description |
|--------|-------------|
| `nps_sdk.core` | Frame header, `NpsFrameCodec` (Tier-1 JSON / Tier-2 MsgPack), `FrameRegistry`, `AnchorFrameCache`, exceptions |
| `nps_sdk.ncp` | NCP frames: `AnchorFrame`, `DiffFrame`, `StreamFrame`, `CapsFrame`, `HelloFrame` (incl. `ping_interval_ms`), `ErrorFrame`, `NopFrame` (keepalive/heartbeat) |
| `nps_sdk.nwp` | NWP frames: `QueryFrame`, `ActionFrame`; async `NwpClient`; `NwpErrorCodes` (30 constants) |
| `nps_sdk.nip` | NIP frames: `IdentFrame` (v2 dual-trust, incl. `node_roles`), `TrustFrame`, `RevokeFrame`; `NipIdentity` (Ed25519); `NipIdentVerifier` + `NipVerifierOptions` (RFC-0002 §8.1 dual-trust); `AssuranceLevel` (RFC-0003) |
| `nps_sdk.nip.x509` | RFC-0002 X.509 NID certs: `NipX509Builder`, `NipX509Verifier`, `NpsX509Oids` |
| `nps_sdk.nip.acme` | RFC-0002 ACME `agent-01`: `AcmeClient`, `AcmeServer` (in-process), JWS helpers, messages |
| `nps_sdk.ndp` | NDP frames: `AnnounceFrame` (incl. structured `spawn_spec_ref` + `heartbeat_interval_ms`), `ResolveFrame`, `GraphFrame`; in-memory registry and validator; `resolve_via_dns` DNS TXT fallback |
| `nps_sdk.ndp.dns_txt` | `resolve_via_dns(target, dns_lookup=None)` — looks up `_nps-node.<host>` TXT records when a target is not in the in-memory registry |
| `nps_sdk.nop` | NOP frames: `TaskFrame` (incl. `result_ttl_seconds`), `DelegateFrame`, `SyncFrame`, `AlignStreamFrame`; async `NopClient` |

---

## Type hints

The entire public API is fully annotated. A `py.typed` marker file is included so that mypy and pyright resolve types without extra configuration.

---

## Key classes

### `NpsFrameCodec`

Encodes and decodes NPS frames. Defaults to Tier-2 MsgPack.

```python
from nps_sdk.core.codec import NpsFrameCodec
from nps_sdk.core.registry import FrameRegistry

registry = FrameRegistry.create_default()
codec    = NpsFrameCodec(registry)

wire   = codec.encode(frame)   # bytes — MsgPack by default
result = codec.decode(wire)    # NpsFrame subclass
```

To encode as JSON (debugging only):

```python
from nps_sdk.core.codec import EncodingTier

wire_json = codec.encode(frame, tier=EncodingTier.JSON)
```

### `AnchorFrame`

Content-addressed schema descriptor. The `anchor_id` is a `sha256:<hex>` string computed over the canonical JSON representation of the schema.

```python
from nps_sdk.core.codec import NpsFrameCodec
from nps_sdk.core.registry import FrameRegistry
from nps_sdk.ncp.frames import AnchorFrame, FrameSchema, SchemaField

registry = FrameRegistry.create_default()
codec    = NpsFrameCodec(registry)

schema = FrameSchema(fields=(
    SchemaField(name="id",    type="uint64"),
    SchemaField(name="price", type="decimal", semantic="commerce.price.usd"),
))
frame  = AnchorFrame(anchor_id="sha256:abc123", schema=schema, ttl=3600)

wire   = codec.encode(frame)   # bytes — Tier-2 MsgPack by default
result = codec.decode(wire)    # → AnchorFrame
```

### `AnchorFrameCache`

Store and retrieve schemas by anchor ID with TTL eviction:

```python
from nps_sdk.core.cache import AnchorFrameCache

cache     = AnchorFrameCache()
anchor_id = cache.set(frame)               # returns canonical sha256 anchor_id
frame     = cache.get_required(anchor_id)  # raises if missing or expired
```

### `NwpClient` (async)

```python
import asyncio
from nps_sdk.nwp import NwpClient, QueryFrame

async def main():
    async with NwpClient("https://node.example.com") as client:
        caps = await client.query(QueryFrame(anchor_ref="sha256:abc123", limit=50))
        print(caps.count, caps.data)

asyncio.run(main())
```

Streaming:

```python
async with NwpClient("https://node.example.com") as client:
    async for chunk in client.stream(QueryFrame(anchor_ref="sha256:abc123")):
        print(chunk.seq, chunk.data)
```

Action invocation:

```python
from nps_sdk.nwp import ActionFrame

async with NwpClient("https://node.example.com") as client:
    result = await client.invoke(
        ActionFrame(action_id="orders.create", params={"sku": "X-101", "qty": 1})
    )
```

A synchronous fallback is available by wrapping coroutines with `asyncio.run()`.

### `NipIdentity`

Ed25519 identity management:

```python
from nps_sdk.nip.identity import NipIdentity

# Generate and save an encrypted keypair (AES-256-GCM + PBKDF2)
identity = NipIdentity.generate("ca.key", passphrase="my-secret")

# Load from file
identity = NipIdentity()
identity.load("ca.key", passphrase="my-secret")

# Sign a NIP frame payload (canonical JSON, no 'signature' field)
sig = identity.sign(ident_frame.unsigned_dict())

# Verify
ok = NipIdentity.verify_signature(identity.pub_key_string, payload, sig)
```

### `AssuranceLevel` — empty-string fix (alpha.5)

`AssuranceLevel.from_wire("")` returns `ANONYMOUS` instead of raising `ValueError`. This is a spec §5.1.1 fix; the check is `if not wire: return AssuranceLevel.ANONYMOUS`. Prior to alpha.5, an empty string raised `ValueError`, breaking any code that handled nodes which omit the assurance level field entirely.

```python
from nps_sdk.nip.identity import AssuranceLevel

level = AssuranceLevel.from_wire("")    # → AssuranceLevel.ANONYMOUS  (not ValueError)
level = AssuranceLevel.from_wire("L1") # → AssuranceLevel.L1
```

### DNS TXT fallback resolution

When a target is not in the in-memory NDP registry, `resolve_via_dns` looks up `_nps-node.<host>` TXT records in the format `v=nps1 nid=... port=... fp=...`:

```python
from nps_sdk.ndp.dns_txt import resolve_via_dns

result = resolve_via_dns("nwp://example.com/data/items")
# inject a custom resolver callable for testing:
# result = resolve_via_dns("nwp://example.com/data/items", dns_lookup=my_mock_fn)
```

---

## NWP HTTP Overlay paths

`NwpClient` communicates via HTTP with `Content-Type: application/x-nps-frame`.

| Operation | Path | Request Frame | Response Frame |
|-----------|------|---------------|----------------|
| Schema anchor | `POST /anchor` | `AnchorFrame` | 204 |
| Structured query | `POST /query` | `QueryFrame` | `CapsFrame` |
| Streaming query | `POST /stream` | `QueryFrame` | `StreamFrame` chunks |
| Action invocation | `POST /invoke` | `ActionFrame` | raw result or `AsyncActionResponse` |

---

## Running tests

```bash
pytest                 # all tests + coverage report
pytest -k test_nip     # NIP tests only
```

---

## Feature set (accumulated through alpha.19)

This section is cumulative: everything listed below is present in the published `1.0.0a19` package. The release tag on each block is where the capability first landed.

The Python SDK ships the alpha.13 parity surface plus the alpha.14 and alpha.15 release additions:

- **NCP** — `NopFrame` keepalive/heartbeat; `HelloFrame.ping_interval_ms` (0 disables; dead-peer threshold = 3 × interval).
- **NIP** — `IdentFrame.node_roles` (self-declared node-role tags; excluded from the Ed25519-signed payload, same pattern as `cert_format`/`cert_chain`).
- **NDP** — `AnnounceFrame.spawn_spec_ref` as a structured schema object (was a URI string); `AnnounceFrame.heartbeat_interval_ms` (default 60000 ms, 0 disables).
- **NOP** — `TaskFrame.result_ttl_seconds` (default 3600 s, omitted from wire at default).
- **NWP** — `X-NWM-Version` response header; NWM `manifest_version` / `manifest_updated_at`.
- **`ReputationLogClient`** — CT-style reputation log with dual Ed25519 signatures (`SignedTreeHead`, `InclusionProof`, RFC 9162 Merkle fold). Available since **alpha.7**.

The alpha.15 release adds:

- **NCP Tier-3 BinaryVector** (`binary_vector.v1`) — `EncodingTier.BINARY_VECTOR`, a compact float-vector encoding tier for `QueryFrame.vector_search.vector`. Negotiated via caps and used only when both peers advertise `binary_vector.v1`; `NPBV`-prefixed payload with MessagePack metadata markers and little-endian `float32` segments. Malformed payloads raise documented client errors (`NCP-BINARY-VECTOR-*` → `NPS-CLIENT-BAD-FRAME`); the reserved tier `0b11` → `NCP-FRAME-FLAGS-INVALID`.
- **Inbound NWP Bridge server** — serve local NPS actions to external MCP / A2A clients (inverse of the outbound Bridge Node). Secure-by-default: a valid `X-NWP-Agent` NID plus a verifier hook, an action allowlist, bounded request bodies (default 1 MB → 413), a dispatch timeout (default 30 s → 504), and sanitized client errors.
- **Native-mode NWP serving** — serve `QueryFrame` / `ActionFrame` directly over a native NCP session rather than only the HTTP overlay.
- **Typed remote NIP CA client** — CA discovery, CRL retrieval, and Ed25519 register / renew / revoke / verify, including RFC-0002 X.509 registration.
- **NIP TrustFrame/RevokeFrame signed-payload realignment** — the Ed25519-signed payload now includes the current NPS-3 fields (`issued_at`, `serial`, `signer_nid`, `target_nid`) and uses current revocation naming (`NIP-CERT-REVOKED`). **Breaking:** signed frames produced by the old alpha.14-era SDK shape no longer verify after upgrading.

> The `EncodingTier.BINARY_VECTOR` and `binary_vector.v1` names are confirmed for the Python SDK; the inbound Bridge server, native-mode serving, and CA-client surfaces follow the .NET reference capability ([SDK DotNet](./SDK-DotNet.md)) — consult the Python module reference for exact symbol names.

The alpha.16 release re-issued the alpha.15 package set (those version numbers were already taken on the public registries); it adds no new Python surface.

The alpha.17 release adds:

- **Server and orchestration parity with the .NET reference** — native NCP transport, NWP Action / Complex / Memory Nodes, bidirectional Bridges, NIP CA plus full verification, NOP orchestration, and daemon observability/telemetry are now available in the Python SDK, not only the client surface.
- **Portable profiles and shared conformance vectors** — the Python SDK executes the same language-neutral fixtures as the other five SDKs for NCP 0.11 native-server handshakes, NWP 0.20 Node/Bridge serving, NIP 0.13 CA/revocation, NDP 0.12 registry admission, and NOP 0.9 orchestration.

The alpha.18 release adds:

- **NPS-CR-0011 / NWP 0.21 stateful LLM context** — owner-bound opaque context IDs with `create` / `append` / `fork` / `reset` / `status` / `release`, compare-and-swap versions, atomic unary and async cancellation, NWM 0.2 discovery, and `llm:context` authorization under NIP 0.14. Validated against the 19 shared CR-0011 conformance vectors. Stateless completion remains compatible; stateful requests never silently fall back.
- **Stateful NDJSON streaming** — atomic terminal-frame commit, abnormal-termination abort (failed or incomplete streams are never cached), and idempotent replay of a completed sequence under a fresh server-owned `stream_id`. `stream=true` is incompatible with async acknowledgement.
- **Official NWP LLM usage telemetry** — `input_tokens`, `output_tokens`, prefix/KV-cache hit, reused tokens, and evaluated tokens, plus unary `CapsFrame.request_id` correlation echoed by the native NWP server helpers. `CapsFrame.cached` stays distinct from model prefix/KV-cache reuse.
- **`NPS-LIMIT-RESOURCE`** for bounded live-object limits, and `wire_input_bytes` on the LLM usage DTO for decoder-boundary request measurement.
- The stateful LLM Action coordinator **fails closed** when no deployment authorizer is configured, and passes the exact admission/commit capability set (`llm:complete` + `llm:context`, plus stream/tool capabilities when used) to that authorizer.

> The alpha.17/alpha.18 entries describe the suite-level capability as delivered in the Python SDK; consult the Python module reference for exact symbol names.

---

## See also

- [SDK Quickstart](./SDK-Quickstart.md) — language-agnostic first steps and install table
- [SDK Identity and Authentication](./SDK-Identity-and-Authentication.md) — NipIdentity, IdentFrame, trust chain
- [SDK Common Patterns](./SDK-Common-Patterns.md) — anchor cache, streaming, DNS TXT fallback

---

*Last reviewed at suite version: v1.0.0-alpha.19*
