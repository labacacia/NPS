# Example: Cross-SDK Interop

**Status:** ✅ Reviewed for v1.0.0-alpha.19

**Repo:** `labacacia/NPS-Examples`, directory: `cross-sdk-interop/` (source in NPS-Dev `demos/cross-sdk-interop/`)

This demo catches behavioral drift between SDKs that per-SDK unit tests cannot catch: encoding byte-for-byte parity, edge-case input handling, and empty-string semantics. A shell script starts an NWP server, fans out calls from multiple language clients, and diffs the outputs.

---

## Purpose

Per-SDK unit tests verify that each SDK is internally consistent. They cannot verify that the `.NET` SDK and the Python SDK produce the same wire output for the same input — because each runs in isolation. Cross-SDK interop tests bridge this gap.

The central question this demo answers:

> **Any HTTP client can talk to an NWP node. No SDK required.**

If four stdlib clients in four different languages all receive the exact same `{anchor_ref, count, data[]}` payload from the same server, then NWP wire-format compatibility is a property of the protocol, not an artifact of a shared library.

---

## Structure

```
demos/cross-sdk-interop/
├── run.sh                           # start server, fan out to each client, diff canonical outputs
├── assurance-level-parity.sh        # SDK fromWire("") == ANONYMOUS parity check
├── server/
│   ├── NPS.Demo.InteropServer.csproj
│   └── Program.cs                    # 3-row products Memory Node
└── clients/
    ├── dotnet/
    │   ├── NPS.Demo.InteropClient.csproj
    │   └── Program.cs                # HttpClient POST /query
    ├── python.py                     # urllib POST /query
    ├── nodejs.mjs                    # fetch() POST /query
    └── go.go                         # net/http POST /query
```

The four clients use only their language's standard library — no `pip install nps-lib`, no `npm install @labacacia/nps-sdk`, no `go get`. This is intentional: if NPS requires an SDK to be consumed, it hasn't achieved protocol-level compatibility.

---

## How to Run

```bash
git clone https://github.com/labacacia/nps.git
cd nps

# Wire-format interop (stdlib-only clients, no SDK needed)
bash demos/cross-sdk-interop/run.sh

# SDK behaviour parity: AssuranceLevel.fromWire("") == ANONYMOUS across all SDKs
bash demos/cross-sdk-interop/assurance-level-parity.sh
```

`run.sh` requires .NET 10 SDK for the server and dotnet client. Optional (auto-detected per-client on `PATH`): `python3` ≥ 3.9, `node` ≥ 18, `go` ≥ 1.22. Missing runtimes are skipped gracefully — the interop claim is proven by any two clients matching, not by requiring all four.

---

## Wire-Format Interop Test (`run.sh`)

The script:

1. Builds and starts the .NET NWP Memory Node on `http://127.0.0.1:17491`
2. Invokes each available client (dotnet, python3, node, go) with a `POST /query` to that server
3. Strips the intentional `"client"` tag from each response
4. Diffs the remaining canonical payloads

Successful output (with .NET, Python, Node on PATH; Go absent):

```
── cross-sdk-interop ──
[build] dotnet server + client
[start] server on http://127.0.0.1:17491
[client:dotnet]
{
  "client": "dotnet",
  "count": 3,
  "anchor_ref": "sha256:a734e8fa431d8f0ea186f0aee2297de63ae38b5b405edf5dfb3c6b199af64b7f",
  "data": [
    { "id": 301, "name": "Ergonomic Keyboard",   "price": 129 },
    { "id": 302, "name": "Mechanical Trackball", "price": 79.5 },
    { "id": 303, "name": "Laminar Desk Lamp",    "price": 49 }
  ]
}

[client:python]  { "client": "python",  "count": 3, "anchor_ref": "sha256:a734e8fa…", "data": […identical 3 rows…] }
[client:nodejs]  { "client": "nodejs",  "count": 3, "anchor_ref": "sha256:a734e8fa…", "data": […identical 3 rows…] }
[client:go]      skipped — go not on PATH

── diff canonical outputs ──
  ✓ python == dotnet
  ✓ nodejs == dotnet
── result: interop verified across 3 clients ──
```

The key observable: after stripping `"client"`, the payloads are byte-identical — same `anchor_ref`, same `count`, same row ordering, same numeric representation. The `anchor_ref` is a SHA-256 over the node's advertised schema; if the schema hasn't changed, every client in every language gets the same anchor.

---

## The Assurance-Level Parity Test (`assurance-level-parity.sh`)

This test calls `AssuranceLevel.fromWire("")` (the empty-string case) in Python, TypeScript, Java, and Go, then verifies that all installed SDKs return `"anonymous"` — not `null`, not an exception, not a panic.

The test was added because of an incident in alpha.5:

### Failure Case Study: the Alpha.5 Empty-String Incident

The `AssuranceLevel.fromWire("")` method was supposed to be equivalent to `fromWire(null)` and return `ANONYMOUS`. In alpha.5, Python and TypeScript received a bug fix for this case (NPS-RFC-0003 §5.1.1). Java and Go were initially missed — they still returned `null` or threw an exception on empty string.

This class of bug is invisible to per-SDK unit tests unless each SDK explicitly tests the empty-string case. But it is trivially caught by a cross-SDK test that calls the same input and diffs the outputs.

The fix was to add Java and Go to the parity matrix before tagging alpha.5. Now the `assurance-level-parity.sh` script runs against all four SDKs in CI on every PR that touches `AssuranceLevel` or the NIP identity layer.

**This is the canonical example of why cross-SDK tests exist:** some invariants can only be verified by running multiple SDKs together against the same input.

---

## Six-SDK Feature Parity (alpha.18)

As of v1.0.0-alpha.19, all six SDKs (Python / TypeScript / Go / Java / Rust / .NET) ship the same protocol feature set — including the CR-0011 stateful LLM context / delta completion contract — and the cross-SDK matrix exercises each of these for byte- and behavior-level parity:

- **NCP `NopFrame` (0x07)** — zero-payload keepalive/heartbeat (NCP v0.8); either peer MAY send it after the handshake. Paired with `HelloFrame.ping_interval_ms` (uint32, 0 = disabled).
- **NCP Tier-3 `binary_vector.v1`** — a third encoding tier for compact float-vector (embedding) payloads on `QueryFrame` (NCP v0.9). Negotiated via caps; used only when both peers advertise `binary_vector.v1`. Layout is a 16-byte prefix (`NPBV` magic, version, vector_count, metadata_len) + MessagePack metadata + per-vector `dim` (uint32 BE) + float32-LE segments. Malformed payloads return documented client errors (`NCP-BINARY-VECTOR-MALFORMED` / `-DIM-MISMATCH` / `-INDEX-INVALID` / `-DTYPE-UNSUPPORTED` / `-TRUNCATED` → `NPS-CLIENT-BAD-FRAME`); the reserved tier `0b11` → `NCP-FRAME-FLAGS-INVALID`.
- **NIP `node_roles`** — `IdentFrame.node_roles` self-declared node-role tags (NIP v0.10), the current name for the topology/discovery role field. The legacy `node_kind` alias was accepted through alpha.5 only.
- **NIP TrustFrame/RevokeFrame signed-payload realignment** — the Ed25519-signed payload now covers the current NPS-3 fields (`issued_at`, `serial`, `signer_nid`, `target_nid`) and current revocation naming (`NIP-CERT-REVOKED`) (NIP v0.10). Signed frames produced by the old alpha.14-era SDK shape no longer verify after upgrading (breaking).
- **NDP `spawn_spec_ref` schema object** — the `AnnounceFrame.spawn_spec_ref` type changed from a URI string to a structured SpawnSpec schema object (NDP v0.9), alongside `heartbeat_interval_ms`.
- **NDP AnnounceFrame signed canonical form** — the signed body is now normative and cross-SDK consistent (NDP v0.9 §3.1): it covers all emitted AnnounceFrame wire fields except `signature`, `health`, `last_seen`, and the `frame` discriminant; absent/null optionals are omitted (not serialized as `null`); `heartbeat_interval_ms` is signed and canonicalized to the default `60000` only when absent, while an explicit `0` (disabled) is signed literally. Old per-SDK-divergent signed announcements may fail cross-SDK verification (breaking).
- **Inbound NWP Bridge server adapters** — `McpServerBridge` / `A2aServerBridge` (and the ASP.NET `AddBridgeServer` / `UseBridgeServer` helpers) let external MCP / A2A clients call local NPS actions — the inverse of the outbound Bridge Node. Secure-by-default: requires a valid `X-NWP-Agent` NID plus a configured verifier hook, bounded request bodies (`MaxRequestBodyBytes`, default 1 MB → 413), a dispatch timeout (`DispatchTimeoutMs`, default 30 s → 504), sanitized client errors, and an action allowlist.
- **Native-mode NWP serving** — `NwpNativeNodeServer`: Memory / Action Nodes serve `QueryFrame` / `ActionFrame` directly over an `NcpSession` / native NCP stream, with no hand-rolled frame loop.
- **NOP `result_ttl_seconds`** — `TaskFrame.result_ttl_seconds` (uint32, default 3 600 s, omitted from the wire at default) (NOP v0.7).
- **NWP `X-NWM-Version`** — the `X-NWM-Version` response-header constant plus `manifest_version` / `manifest_updated_at` on `GET /.nwm` (NWP v0.14).

A good cross-SDK invariant is one where all six SDKs must produce identical wire bytes or identical decoded values for any of the fields above — for example, that omitting `result_ttl_seconds` at its default produces byte-identical TaskFrames across all six encoders, or that a default-`heartbeat_interval_ms` AnnounceFrame yields a byte-identical signed canonical form across all six signers.

---

## How to Add a New Cross-SDK Test

1. **Identify a wire-level invariant.** What value should every SDK produce for a given input? The invariant must be observable from the wire (e.g. a method return value, a serialized byte sequence, a field in a response frame).
2. **Write a shell script** that calls each SDK's implementation of the invariant with the same input. Use the existing scripts as structural models.
3. **Compare outputs via `diff` or `jq`.** Strip any intentionally differing fields (e.g. timestamps, random IDs) before comparing.
4. **Add the script to the CI matrix** in NPS-Dev's CI configuration so it runs on PRs that touch the relevant SDK code.

Good candidates for cross-SDK tests:
- Any method that parses a wire value into an enum or constant (like `AssuranceLevel.fromWire`)
- Any method that serializes a frame to bytes (byte-equality check)
- Any edge case documented in a spec (null input, empty string, max-length value)

---

## CI Integration

The cross-SDK interop tests run in NPS-Dev CI on every PR. Failures block merge. Each test script exits with a non-zero code if any available SDK produces an unexpected result.

Runtimes that are not on the CI runner's PATH are skipped gracefully — the test reports "skipped" rather than "failed" for absent runtimes. The CI matrix is configured to have all six runtimes available so that "skipped" does not occur in CI (only in local runs where a developer doesn't have all runtimes installed).

---

## Toolchain Matrix

| Client | Minimum runtime | Install-free deps |
|--------|-----------------|-------------------|
| dotnet | .NET 10 SDK | `HttpClient` (stdlib) |
| python | Python 3.9+ | `urllib` (stdlib) |
| nodejs | Node 18+ | global `fetch()` (stdlib) |
| go | Go 1.22+ | `net/http` (stdlib) |

---

## Limitations

- **JSON-Overlay only.** The wire-format interop test uses the unframed `application/json` flavor of NWP at `/query`. The framed wire (`application/x-nps-frame`, 4-byte header + tier-encoded payload) is tested by each SDK's own test suite, not by this demo.
- **Single endpoint.** Only `/query` is exercised. `/anchor`, `/.nwm`, `/stream`, `/invoke` are not validated here.
- **No NIP.** Clients send no `X-NWP-Agent`; the server accepts anonymous calls.

---

## Related Pages

- [SDK Identity and Authentication](./SDK-Identity-and-Authentication.md) — NIP and assurance levels
- [SDK Quickstart](./SDK-Quickstart.md) — getting started with any NPS SDK

---

*Last reviewed at suite version: v1.0.0-alpha.19*
