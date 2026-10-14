# SDK — TypeScript

**Status:** ✅ Latest published package — v1.0.0-alpha.19 (released 2026-09-06)

> **Alpha.19 release:** this SDK executes all 47
> shared P19-1 hardening vectors for NCP 0.12, NWP 0.22, NIP 0.15, NDP 0.13
> and NOP 0.10. The install command below pins the published alpha.19 package. See [Alpha.19 Current Status](./Alpha19-Current-Status.md).

TypeScript / Node.js SDK for the Neural Protocol Suite. Covers all five protocols: NCP, NWP, NIP, NDP, and NOP. Dual ESM + CJS build; works in Node.js 22+ and in the browser via the ESM bundle.

---

## Installation

```bash
npm install @labacacia/nps-sdk@1.0.0-alpha.19
```

**Requirements:** Node.js 22+. The ESM build also works in modern browsers (Chrome 120+, Firefox 121+, Safari 17+) via a bundler.

> **`alpha` dist-tag:** `npm install @labacacia/nps-sdk@alpha` currently resolves to `1.0.0-alpha.19`; the unqualified npm `latest` tag intentionally remains on alpha.7. alpha.12 was withdrawn; pin the explicit version for reproducible builds.

**Tests:** 284+ passing, ≥ 98% coverage.

---

## Dual build: ESM and CJS

The package ships both an ESM build (for Node.js 22+ native modules and browser bundlers) and a CommonJS build (for legacy Node.js toolchains and Jest). The correct build is selected automatically via the `exports` field in `package.json`. No extra configuration is needed.

---

## Module layout

```typescript
import { ... } from "@labacacia/nps-sdk";           // everything (re-export barrel)
import { ... } from "@labacacia/nps-sdk/core";       // codec, frames, registry, cache
import { ... } from "@labacacia/nps-sdk/ncp";        // AnchorFrame, CapsFrame, StreamFrame, HelloFrame, NopFrame, …
import { ... } from "@labacacia/nps-sdk/nwp";        // NwpClient, QueryFrame, ActionFrame, NwpErrorCodes
import { ... } from "@labacacia/nps-sdk/nip";        // NipIdentity, IdentFrame (incl. nodeRoles), TrustFrame, RevokeFrame, AssuranceLevel
import { ... } from "@labacacia/nps-sdk/ndp";        // InMemoryNdpRegistry, NdpAnnounceValidator, AnnounceFrame, resolveWithDns
import { ... } from "@labacacia/nps-sdk/nop";        // NopClient, NopTaskStatus, TaskFrame, …
```

All types are exported from their respective sub-paths. TypeScript strict mode is used throughout; every public symbol has an explicit type signature.

---

## Key classes

### `NpsFrameCodec`

```typescript
import { NpsFrameCodec, EncodingTier, createDefaultRegistry } from "@labacacia/nps-sdk/core";

const codec = new NpsFrameCodec(createDefaultRegistry());

// Encode (defaults to MsgPack — production)
const wire = codec.encode(frame);

// Encode as JSON (debugging / interop)
const json = codec.encode(frame, { overrideTier: EncodingTier.JSON });

// Decode
const decoded = codec.decode(wire);

// Peek at the header without decoding the payload
const header = NpsFrameCodec.peekHeader(wire);
console.log(header.frameType, header.isExtended, header.payloadLength);
```

### `AnchorFrame` — encode and decode (3 lines)

```typescript
import { NpsFrameCodec, createDefaultRegistry } from "@labacacia/nps-sdk/core";
import { AnchorFrame } from "@labacacia/nps-sdk/ncp";

const codec  = new NpsFrameCodec(createDefaultRegistry());
const frame  = new AnchorFrame("sha256:abc123", { fields: [{ name: "price", type: "decimal" }] });
const wire   = codec.encode(frame);   // Uint8Array — MsgPack
const result = codec.decode(wire);    // AnchorFrame
```

### `NwpClient`

```typescript
import { NwpClient, QueryFrame, ActionFrame } from "@labacacia/nps-sdk/nwp";

const client = new NwpClient("http://node.example.com:17433");

// Query
const caps = await client.query(new QueryFrame("sha256:<anchor-id>", { active: true }, 20));
console.log(caps.count, caps.data);

// Stream
for await (const chunk of client.stream(new QueryFrame("sha256:<anchor-id>"))) {
  console.log(chunk.seq, chunk.data);
}

// Action invoke
const result = await client.invoke(new ActionFrame("summarise", { maxTokens: 500 }));
```

### `NipIdentity`

```typescript
import { NipIdentity } from "@labacacia/nps-sdk/nip";

// Generate and persist
const id = NipIdentity.generate();
id.save("./my-key.json", process.env.KEY_PASS!);

// Load and sign
const loaded = NipIdentity.load("./my-key.json", process.env.KEY_PASS!);
const sig    = loaded.sign({ action: "announce", nid: "urn:nps:node:example.com:data" });
const ok     = loaded.verify({ action: "announce", nid: "urn:nps:node:example.com:data" }, sig);
```

### `AssuranceLevel` — empty-string fix (alpha.5)

`AssuranceLevel.fromWire("")` returns `Anonymous` instead of `Unknown`. The check is `if (!wire)` at the top of the method. Prior to alpha.5 the empty string silently mapped to `Unknown`, which was incorrect per spec §5.1.1.

```typescript
import { AssuranceLevel } from "@labacacia/nps-sdk/nip";

AssuranceLevel.fromWire("")    // → AssuranceLevel.Anonymous  (not Unknown)
AssuranceLevel.fromWire("L1") // → AssuranceLevel.L1
```

### DNS TXT fallback (`resolveWithDns`)

```typescript
import { resolveWithDns, SystemDnsTxtLookup, parseNpsTxtRecord } from "@labacacia/nps-sdk/ndp";

// Uses the system resolver by default
const result = await resolveWithDns("nwp://example.com/data/items");

// Inject a mock for testing
const result2 = await resolveWithDns("nwp://example.com/data/items", myMockLookup);
```

The `DnsTxtLookup` interface allows injecting any resolver (useful for tests or custom DNS-over-HTTPS).

---

## NWP error codes

30 NWP wire error code string constants are exported from `@labacacia/nps-sdk/nwp` as `NwpErrorCodes`:

```typescript
import { NwpErrorCodes } from "@labacacia/nps-sdk/nwp";

// Example constants:
// NwpErrorCodes.AUTH_MISSING, NwpErrorCodes.QUERY_ANCHOR_NOT_FOUND,
// NwpErrorCodes.TOPOLOGY_UNAUTHORIZED, NwpErrorCodes.RESERVED_TYPE_UNSUPPORTED
```

---

## Development

```bash
# Install (no symlinks required on restricted filesystems)
npm install --no-bin-links

# Test
node node_modules/vitest/vitest.mjs run

# Test + coverage
node node_modules/vitest/vitest.mjs run --coverage

# Build (ESM + CJS)
node node_modules/tsup/dist/cli-default.js
```

---

## Feature set (accumulated through alpha.19)

This section is cumulative: everything listed below is present in the published `1.0.0-alpha.19` package. The release tag on each block is where the capability first landed.

The TypeScript SDK ships the alpha.13 parity surface plus the alpha.14 and alpha.15 release additions:

- **NCP** — `NopFrame` keepalive/heartbeat; `HelloFrame.ping_interval_ms` (0 disables; dead-peer threshold = 3 × interval).
- **NIP** — `IdentFrame.nodeRoles` (self-declared node-role tags; excluded from the Ed25519-signed payload).
- **NDP** — `AnnounceFrame.spawnSpecRef` as a structured schema object (was a URI string); `AnnounceFrame.heartbeatIntervalMs` (default 60000 ms, 0 disables).
- **NOP** — `TaskFrame.resultTtlSeconds` (default 3600 s, omitted from wire at default).
- **NWP** — `X-NWM-Version` response header; NWM `manifestVersion` / `manifestUpdatedAt`.
- **`ReputationLogClient`** — CT-style reputation log with dual Ed25519 signatures (`SignedTreeHead`, `InclusionProof`, RFC 9162 Merkle fold). Shipped in the TypeScript SDK since **alpha.6** (the other five SDKs gained it in alpha.7).

The alpha.15 release adds:

- **NCP Tier-3 BinaryVector** (`binary_vector.v1`) — `EncodingTier.BinaryVector` (alias `EncodingTier.BINARY_VECTOR`) in both the OOP and functional NCP codecs, a compact float-vector encoding tier for `QueryFrame.vector_search.vector`. Negotiated via caps and used only when both peers advertise `binary_vector.v1`; `NPBV`-prefixed payload with MessagePack metadata markers and little-endian `float32` segments. Malformed payloads throw documented client errors (`NCP-BINARY-VECTOR-*` → `NPS-CLIENT-BAD-FRAME`); the reserved tier `0b11` → `NCP-FRAME-FLAGS-INVALID`.
- **Inbound NWP Bridge server** — serve local NPS actions to external MCP / A2A clients (inverse of the outbound Bridge Node). Secure-by-default: a valid `X-NWP-Agent` NID plus a verifier hook, an action allowlist, bounded request bodies (default 1 MB → 413), a dispatch timeout (default 30 s → 504), and sanitized client errors.
- **Native-mode NWP serving** — serve `QueryFrame` / `ActionFrame` directly over a native NCP session rather than only the HTTP overlay.
- **Typed remote NIP CA client** — CA discovery, CRL retrieval, and Ed25519 register / renew / revoke / verify, including RFC-0002 X.509 registration.
- **NIP TrustFrame/RevokeFrame signed-payload realignment** — the Ed25519-signed payload now includes the current NPS-3 fields (`issued_at`, `serial`, `signer_nid`, `target_nid`) and uses current revocation naming (`NIP-CERT-REVOKED`). **Breaking:** signed frames produced by the old alpha.14-era SDK shape no longer verify after upgrading.

> The `EncodingTier.BinaryVector` / `binary_vector.v1` names are confirmed for the TypeScript SDK; the inbound Bridge server, native-mode serving, and CA-client surfaces follow the .NET reference capability ([SDK DotNet](./SDK-DotNet.md)) — consult the TypeScript module reference for exact symbol names.

The alpha.16 release re-issued the alpha.15 package set (those version numbers were already taken on the public registries); it adds no new TypeScript surface.

The alpha.17 release adds:

- **Server and orchestration parity with the .NET reference** — native NCP transport, NWP Action / Complex / Memory Nodes, bidirectional Bridges, NIP CA plus full verification, NOP orchestration, and daemon observability/telemetry, on top of the existing Web Fetch handler binding.
- **Portable profiles and shared conformance vectors** — the TypeScript SDK executes the same language-neutral fixtures as the other five SDKs for NCP 0.11 native-server handshakes, NWP 0.20 Node/Bridge serving, NIP 0.13 CA/revocation, NDP 0.12 registry admission, and NOP 0.9 orchestration.

The alpha.18 release adds:

- **NPS-CR-0011 / NWP 0.21 stateful LLM context** — owner-bound opaque context IDs with `create` / `append` / `fork` / `reset` / `status` / `release`, compare-and-swap versions, atomic unary and async cancellation, NWM 0.2 discovery, and `llm:context` authorization under NIP 0.14. Validated against the 19 shared CR-0011 conformance vectors. Stateless completion remains compatible; stateful requests never silently fall back.
- **Stateful NDJSON streaming** — atomic terminal-frame commit, abnormal-termination abort (failed or incomplete streams are never cached), and idempotent replay of a completed sequence under a fresh server-owned `stream_id`. `stream=true` is incompatible with async acknowledgement.
- **Official NWP LLM usage telemetry** — `input_tokens`, `output_tokens`, prefix/KV-cache hit, reused tokens, and evaluated tokens, plus unary `CapsFrame.request_id` correlation echoed by the native NWP server helpers. `CapsFrame.cached` stays distinct from model prefix/KV-cache reuse.
- **`NPS-LIMIT-RESOURCE`** for bounded live-object limits, and `wireInputBytes` on the LLM usage DTO for decoder-boundary request measurement.
- The runtime `VERSION` export is now derived from `package.json`, so a package bump can no longer leave the runtime banner behind.

> The alpha.17/alpha.18 entries describe the suite-level capability as delivered in the TypeScript SDK; consult the TypeScript module reference for exact symbol names.

---

## See also

- [SDK Quickstart](./SDK-Quickstart.md) — language-agnostic first steps and install table
- [SDK Identity and Authentication](./SDK-Identity-and-Authentication.md) — NipIdentity, IdentFrame, trust chain
- [SDK Common Patterns](./SDK-Common-Patterns.md) — anchor cache, streaming, DNS TXT fallback

---

*Last reviewed at suite version: v1.0.0-alpha.19*
