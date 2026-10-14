# SDK — Java

**Status:** ✅ Latest published artifact — v1.0.0-alpha.19 (released 2026-09-06)

> **Alpha.19 release:** this SDK executes all 47
> shared P19-1 hardening vectors for NCP 0.12, NWP 0.22, NIP 0.15, NDP 0.13
> and NOP 0.10. The install command below pins the published alpha.19 package. See [Alpha.19 Current Status](./Alpha19-Current-Status.md).

Java client library for the Neural Protocol Suite. Covers all five protocols: NCP, NWP, NIP, NDP, and NOP.

---

## Published capability set (accumulated through alpha.19)

The Java SDK tracks the suite feature train; everything in this section is present in the published `1.0.0-alpha.19` artifact, and the release tag on each entry is where that capability first landed. Beyond the alpha.13 client baseline, it carries the following capability-level additions (exact type names may differ by language — see the source):

- **NCP Tier-3 BinaryVector (`binary_vector.v1`)** (NCP v0.9, alpha.14) — a third encoding tier for compact float-vector (embedding) payloads on `QueryFrame`. Negotiated via caps and only used when both peers advertise `binary_vector.v1`. Malformed payloads surface as documented client errors (`NCP-BINARY-VECTOR-*` → `NPS-CLIENT-BAD-FRAME`); the reserved tier bits return `NCP-FRAME-FLAGS-INVALID`.
- **Inbound NWP Bridge server adapters** (alpha.14) — lets external MCP / A2A clients call local NPS actions (the inverse of the outbound Bridge Node). Secure-by-default: valid `X-NWP-Agent` NID + a configured verifier, bounded request bodies (→ 413), dispatch timeout (→ 504), sanitized client errors, and an action allowlist. See [SDK Building a Bridge Node](./SDK-Building-a-Bridge-Node.md).
- **Native-mode NWP serving** (alpha.14) — Memory / Action Nodes serve `QueryFrame` / `ActionFrame` directly over a native NCP session rather than a hand-rolled frame loop.
- **NIP signed-payload realignment** (alpha.15, **breaking**) — TrustFrame / RevokeFrame now sign the current NPS-3 field set (`issued_at`, `serial`, `signer_nid`, `target_nid`) and use current revocation naming (`NIP-CERT-REVOKED`). Signed frames produced by the old alpha.14-era shape no longer verify. See [SDK Identity and Authentication](./SDK-Identity-and-Authentication.md).
- **NDP AnnounceFrame signed canonical form** (alpha.15, **breaking**) — the signed body is now normative and cross-SDK consistent (sign all wire fields except `signature` / `health` / `last_seen` / `frame`; omit null optionals; `heartbeat_interval_ms` canonicalised to the default `60000` only when absent, explicit `0` signed literally).
- **Server and orchestration parity with the .NET reference** (alpha.17) — native NCP transport, NWP Action / Complex / Memory Nodes, bidirectional Bridges, NIP CA plus full verification, NOP orchestration, and daemon observability/telemetry, on the existing JDK `com.sun.net.httpserver` binding.
- **Portable profiles and shared conformance vectors** (alpha.17) — the Java SDK executes the same language-neutral fixtures as the other five SDKs for NCP 0.11 native-server handshakes, NWP 0.20 Node/Bridge serving, NIP 0.13 CA/revocation, NDP 0.12 registry admission, and NOP 0.9 orchestration.
- **NPS-CR-0011 / NWP 0.21 stateful LLM context** (alpha.18) — owner-bound opaque context IDs with `create` / `append` / `fork` / `reset` / `status` / `release`, compare-and-swap versions, atomic unary and async cancellation, NWM 0.2 discovery, and `llm:context` authorization under NIP 0.14, validated against the 19 shared CR-0011 conformance vectors. Stateless completion stays compatible; stateful requests never silently fall back. Stateful NDJSON streaming commits the terminal frame atomically, aborts (never caches) failed or incomplete streams, and replays a completed sequence idempotently under a fresh server-owned `stream_id`.
- **Official NWP LLM usage telemetry** (alpha.18) — `input_tokens`, `output_tokens`, prefix/KV-cache hit, reused tokens, and evaluated tokens, plus unary `CapsFrame.request_id` correlation echoed by the native NWP server helpers; `CapsFrame.cached` stays distinct from model prefix/KV-cache reuse. Adds `NPS-LIMIT-RESOURCE` for bounded live-object limits and `wireInputBytes` on the LLM usage DTO.

---

## Installation

### Gradle (Kotlin DSL)

```kotlin
dependencies {
    implementation("com.labacacia.nps:nps-java:1.0.0-alpha.19")
}
```

### Maven

```xml
<dependency>
  <groupId>com.labacacia.nps</groupId>
  <artifactId>nps-java</artifactId>
  <version>1.0.0-alpha.19</version>
</dependency>
```

**Requirements:** Java 21+, Gradle 8.x. The library uses Java records and sealed classes introduced in Java 16/17 and finalised in Java 21.

**Tests:** 122 passing.

### Runtime dependencies

| Dependency | Version | Purpose |
|------------|---------|---------|
| `org.msgpack:msgpack-core` | 0.9.11 | Tier-2 MsgPack encoding |
| `com.fasterxml.jackson.core:jackson-databind` | 2.18.9 | Tier-1 JSON encoding |
| `org.slf4j:slf4j-api` | 2.0.13 | Logging facade |
| `org.bouncycastle:bcprov-jdk18on` | 1.84 | X.509 builder (RFC-0002) |
| `org.bouncycastle:bcpkix-jdk18on` | 1.84 | X.509 builder (RFC-0002) |

> The MessagePack / Jackson / BouncyCastle versions above were raised in alpha.18, clearing every runtime dependency advisory reported by the alpha.18 OSV gate.

---

## Package layout

| Package | Description |
|---------|-------------|
| `com.labacacia.nps.core` | Frame header, `NpsFrameCodec` (Tier-1 JSON / Tier-2 MsgPack), `NpsRegistries`, `AnchorFrameCache`, exceptions |
| `com.labacacia.nps.ncp` | NCP frames: `AnchorFrame`, `DiffFrame`, `StreamFrame`, `CapsFrame`, `HelloFrame`, `NopFrame` (0x07 keepalive), `ErrorFrame`. `HelloFrame.pingIntervalMs` drives keepalive/dead-peer detection |
| `com.labacacia.nps.nwp` | NWP frames: `QueryFrame`, `ActionFrame`, `AsyncActionResponse`; `NwpClient` (HTTP; reads `X-NWM-Version` and `manifest_version`/`manifest_updated_at` for conditional `.nwm` re-fetch); `NwpErrorCodes` (30 constants) |
| `com.labacacia.nps.nip` | NIP frames: `IdentFrame` (incl. `nodeRoles` self-declared role tags), `TrustFrame`, `RevokeFrame`; `NipIdentity` (Ed25519); `NipIdentVerifier` (RFC-0002 §8.1 dual-trust); `AssuranceLevel` (RFC-0003); `ReputationLogClient` (RFC-0004 Phase 2, added in alpha.7) |
| `com.labacacia.nps.nip.x509` | RFC-0002 X.509 NID certs: `NipX509Builder`, `NipX509Verifier`, `Ed25519PublicKeys`, `NpsX509Oids` (anchored to IANA PEN 65715) |
| `com.labacacia.nps.nip.acme` | RFC-0002 ACME `agent-01`: `AcmeClient`, `AcmeServer` (in-process), `AcmeJws`, `AcmeMessages` |
| `com.labacacia.nps.ndp` | NDP frames: `AnnounceFrame` (`spawnSpecRef` structured schema object; `heartbeatIntervalMs`), `ResolveFrame`, `GraphFrame`; `InMemoryNdpRegistry`; `NdpAnnounceValidator`; `resolveViaDns` (DNS TXT fallback); `DnsTxtLookup`, `SystemDnsTxtLookup`, `NpsDnsTxt` |
| `com.labacacia.nps.nop` | NOP frames: `TaskFrame` (`resultTtlSeconds`), `DelegateFrame`, `SyncFrame`, `AlignStreamFrame`; `BackoffStrategy`; `NopTaskStatus` |

---

## Key classes

### `NpsFrameCodec` — encode an AnchorFrame

```java
import com.labacacia.nps.core.codec.NpsFrameCodec;
import com.labacacia.nps.core.registry.NpsRegistries;
import com.labacacia.nps.ncp.AnchorFrame;

var codec  = new NpsFrameCodec(NpsRegistries.createFull());
var schema = java.util.Map.of(
    "fields", java.util.List.of(
        java.util.Map.of("name", "id",    "type", "uint64"),
        java.util.Map.of("name", "price", "type", "decimal")
    )
);
var frame  = new AnchorFrame("sha256:abc123", schema, null, null, null, 3600);

byte[] wire   = codec.encode(frame);                  // Tier-2 MsgPack by default
var    result = (AnchorFrame) codec.decode(wire);
```

To use JSON encoding:

```java
import com.labacacia.nps.core.EncodingTier;

byte[] jsonWire = codec.encode(frame, EncodingTier.JSON);
```

### `NwpClient`

```java
import com.labacacia.nps.nwp.NwpClient;
import com.labacacia.nps.nwp.QueryFrame;
import com.labacacia.nps.nwp.ActionFrame;
import com.labacacia.nps.ncp.CapsFrame;

var client = new NwpClient("http://node.example.com:17433");

// Query
var query = new QueryFrame("sha256:abc123", null, null, null, 50, null);
CapsFrame caps = client.query(query);

// Action invoke
var action = new ActionFrame("run", java.util.Map.of("input", "hello"), null, false);
Object result = client.invoke(action);  // NpsFrame or Map<String,Object>

// Stream
import com.labacacia.nps.ncp.StreamFrame;
List<StreamFrame> frames = client.stream(query);
```

### `NipIdentity`

```java
import com.labacacia.nps.nip.NipIdentity;
import java.nio.file.Path;
import java.util.Map;

// Generate keypair
var identity = NipIdentity.generate();
System.out.println(identity.pubKeyString()); // "ed25519:<hex>"

// Sign a payload
var payload = Map.<String, Object>of("nid", "urn:nps:node:example.com:data");
String sig  = identity.sign(payload);        // "ed25519:<base64>"
boolean ok  = identity.verify(payload, sig); // true

// Persist and load (AES-256-GCM + PBKDF2)
identity.save(Path.of("my-node.key"), "my-passphrase");
var loaded = NipIdentity.load(Path.of("my-node.key"), "my-passphrase");
```

### `AssuranceLevel` — empty-string fix (alpha.5)

`AssuranceLevel.fromWire("")` returns `ANONYMOUS` instead of throwing. The check is `wire == null || wire.isEmpty()`.

```java
import com.labacacia.nps.nip.AssuranceLevel;

AssuranceLevel.fromWire("")    // → ANONYMOUS  (not exception)
AssuranceLevel.fromWire("L1") // → L1
```

### `AnchorFrameCache`

```java
import com.labacacia.nps.core.cache.AnchorFrameCache;

var cache = new AnchorFrameCache();
var frame = new AnchorFrame("sha256:...", schema, null, null, null, 3600);
cache.set(frame);

var retrieved = cache.get("sha256:...");  // null if expired
```

---

## Kotlin interop

The library works without any wrapper. Java records map naturally to Kotlin data classes; sealed classes map to sealed class hierarchies. No source-set configuration is required — add the Gradle dependency as normal and import the Java packages directly in Kotlin files.

---

## Error handling

| Exception | When |
|-----------|------|
| `NpsCodecError` (unchecked) | Unknown frame type, payload too large, decode failure |
| `AnchorFrameCache.AnchorNotFoundError` | `getRequired()` for a missing or expired anchor |
| `AnchorFrameCache.AnchorPoisonError` | Attempt to overwrite a cached anchor with a different schema |
| `IOException` | Network error in `NwpClient` |
| `RuntimeException` | Non-2xx HTTP response from `NwpClient` |

---

## Frame type reference

| Frame | Type code | Protocol |
|-------|-----------|---------|
| `AnchorFrame` | 0x01 | NCP |
| `DiffFrame` | 0x02 | NCP |
| `StreamFrame` | 0x03 | NCP |
| `CapsFrame` | 0x04 | NCP |
| `HelloFrame` | 0x06 | NCP |
| `NopFrame` | 0x07 | NCP |
| `ErrorFrame` | 0xFE | NCP |
| `QueryFrame` | 0x10 | NWP |
| `ActionFrame` | 0x11 | NWP |
| `IdentFrame` | 0x20 | NIP |
| `TrustFrame` | 0x21 | NIP |
| `RevokeFrame` | 0x22 | NIP |
| `AnnounceFrame` | 0x30 | NDP |
| `ResolveFrame` | 0x31 | NDP |
| `GraphFrame` | 0x32 | NDP |
| `TaskFrame` | 0x40 | NOP |
| `DelegateFrame` | 0x41 | NOP |
| `SyncFrame` | 0x42 | NOP |
| `AlignStreamFrame` | 0x43 | NOP |

---

## Building and testing

```bash
# Run all tests
./gradlew test

# Build JAR
./gradlew build
```

Test classes: `AnchorFrameCacheTest` (12), `FrameHeaderTest` (8), `NpsFrameCodecTest` (15), `NdpTest` (35), `NipIdentityTest` (13), `NipX509Tests` (5), `AcmeAgent01Tests` (2), `NopTest` (18). Total: 122.

---

## See also

- [SDK Quickstart](./SDK-Quickstart.md) — language-agnostic first steps and install table
- [SDK Identity and Authentication](./SDK-Identity-and-Authentication.md) — NipIdentity, IdentFrame, trust chain
- [SDK Common Patterns](./SDK-Common-Patterns.md) — anchor cache, streaming, DNS TXT fallback

---

*Last reviewed at suite version: v1.0.0-alpha.19*
