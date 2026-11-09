English | [中文版](./status-codes.cn.md)

# NPS Native Status Codes and HTTP Mapping

**Version**: 0.9
**Date**: 2026-11-02

NPS defines a native status-code system independent of HTTP. Native mode uses NPS status codes directly; HTTP / Overlay mode additionally provides a mapping to HTTP status codes.

---

## Status Code Format

```
{PROTOCOL}-{CATEGORY}-{DETAIL}
```

All uppercase, hyphen-separated. For the full error-code list, see [error-codes.md](error-codes.md).

---

## NPS Native Status Categories

| Category | Meaning | Analogue |
|----------|---------|----------|
| `OK` | Successful operation | HTTP 2xx |
| `CLIENT` | Client-side error (format, params, permission) | HTTP 4xx |
| `SERVER` | Server-side error (internal fault, unavailable) | HTTP 5xx |
| `STREAM` | Stream transport-related errors | — |
| `AUTH` | Authentication / authorization errors | HTTP 401/403 |
| `LIMIT` | Resource or quota limits | HTTP 429 |
| `PROTO` | Protocol-level pre-handshake error (version mismatch, malformed connection preamble); not always emitted on the wire | — |

---

## NPS Status → HTTP Mapping

> This mapping is used only in HTTP / Overlay mode. In native mode, errors are carried directly as status-code strings inside NCP frames.

### Success

| NPS Status | HTTP | Description |
|------------|------|-------------|
| `NPS-OK` | 200 | Operation succeeded |
| `NPS-OK-ACCEPTED` | 202 | Asynchronous operation accepted |
| `NPS-OK-NO-CONTENT` | 204 | Operation succeeded with no response body |

### Client Errors (CLIENT)

| NPS Status | HTTP | Description |
|------------|------|-------------|
| `NPS-CLIENT-BAD-FRAME` | 400 | Frame format invalid |
| `NPS-CLIENT-BAD-PARAM` | 400 | Request parameter invalid |
| `NPS-CLIENT-NOT-FOUND` | 404 | Target resource does not exist |
| `NPS-CLIENT-CONFLICT` | 409 | Resource state conflict |
| `NPS-CLIENT-GONE` | 410 | Resource permanently removed |
| `NPS-CLIENT-UNPROCESSABLE` | 422 | Request semantics invalid |

### Authentication & Authorization (AUTH)

| NPS Status | HTTP | Description |
|------------|------|-------------|
| `NPS-AUTH-UNAUTHENTICATED` | 401 | No identity credential provided or credential invalid |
| `NPS-AUTH-FORBIDDEN` | 403 | Identity is valid but lacks permission |

### Resource Limits (LIMIT)

| NPS Status | HTTP | Description |
|------------|------|-------------|
| `NPS-LIMIT-RATE` | 429 | Request rate exceeded |
| `NPS-LIMIT-BUDGET` | 429 | Token budget exceeded |
| `NPS-LIMIT-PAYLOAD` | 413 | Payload exceeds maximum frame size |
| `NPS-LIMIT-RESOURCE` | 429 | Bounded live-resource count exceeded (contexts, leases, or equivalent retained objects) |

### Server Errors (SERVER)

| NPS Status | HTTP | Description |
|------------|------|-------------|
| `NPS-SERVER-INTERNAL` | 500 | Internal error |
| `NPS-SERVER-UNSUPPORTED` | 501 | The server does not implement the requested operation (e.g. an unrecognized reserved query type in `QueryFrame` or `SubscribeFrame`) |
| `NPS-SERVER-UNAVAILABLE` | 503 | Service temporarily unavailable |
| `NPS-SERVER-TIMEOUT` | 408/504 | Operation timed out |
| `NPS-SERVER-ENCODING-UNSUPPORTED` | 415 | Requested encoding tier is not supported |
| `NPS-DOWNSTREAM-UNAVAILABLE` | 502/503 | A required downstream service that the node depends on (reputation log operator, external auth service, etc.) is unreachable; distinguish from `NPS-SERVER-UNAVAILABLE` so clients can retry against an alternate downstream when policy permits (NPS-RFC-0004) |

### Stream (STREAM)

| NPS Status | HTTP | Description |
|------------|------|-------------|
| `NPS-STREAM-SEQ-GAP` | 422 | Sequence gap |
| `NPS-STREAM-NOT-FOUND` | 404 | Stream id does not exist |
| `NPS-STREAM-LIMIT` | 429 | Concurrent stream limit exceeded |

### Protocol-Level (PROTO)

> Pre-handshake or transport-bracketing errors. These are detected before any frame parser runs (or in lieu of it). In native mode they are usually NOT emitted as ErrorFrames — the connection is closed silently because the peer has not been confirmed to speak NCP. The status codes still exist for SDK-internal telemetry (logs, metrics, classification of close reasons).

| NPS Status | HTTP | Description |
|------------|------|-------------|
| `NPS-PROTO-VERSION-INCOMPATIBLE` | 426 | Client `min_version` exceeds server's max supported version (HelloFrame negotiation failure). HTTP mode emits 426 Upgrade Required; native mode disconnects after a single ErrorFrame. |
| `NPS-PROTO-PREAMBLE-INVALID` | 400 (not emitted) | Native-mode connection opened with bytes other than the constant preamble `b"NPS/1.0\n"`. Server closes silently within 500 ms; no ErrorFrame is sent. (NPS-RFC-0001) |

---

## Protocol-Error → NPS-Status Mapping

Each protocol's specific error code (e.g. `NCP-ANCHOR-NOT-FOUND`) maps to the corresponding NPS status category:

| Protocol Error | NPS Status |
|----------------|-----------|
| `NCP-FEATURE-NEGOTIATION-INVALID` | `NPS-CLIENT-BAD-FRAME` |
| `NCP-FEATURE-REQUIRED-UNSUPPORTED` | `NPS-CLIENT-CONFLICT` |
| `NWP-RESULT-IDENTITY-REQUIRED` | `NPS-AUTH-UNAUTHENTICATED` |
| `NWP-RESULT-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` |
| `NWP-RESULT-SNAPSHOT-MISMATCH` | `NPS-CLIENT-CONFLICT` |
| `NWP-RESULT-CAPACITY` | `NPS-LIMIT-RESOURCE` |
| `NWP-RESULT-POLICY-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` |
| `NWP-RESULT-REPLAY-CONFLICT` | `NPS-CLIENT-CONFLICT` |
| `NWP-CHALLENGE-INVALID` | `NPS-CLIENT-BAD-FRAME` |
| `NWP-CHALLENGE-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` |
| `NWP-CHALLENGE-BINDING-CONFLICT` | `NPS-CLIENT-CONFLICT` |
| `NWP-CHALLENGE-NOT-APPROVED` | `NPS-CLIENT-CONFLICT` |
| `NWP-CHALLENGE-EXPIRED` | `NPS-CLIENT-CONFLICT` |
| `NWP-CHALLENGE-STATE-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` |
| `NOP-EXECUTION-INDETERMINATE` | `NPS-SERVER-INTERNAL` |
| `NWP-FEATURE-REQUIREMENTS-INVALID` | `NPS-CLIENT-BAD-FRAME` |
| `NWP-FEATURE-REQUIRED-UNSUPPORTED` | `NPS-CLIENT-CONFLICT` |
| `NCP-ANCHOR-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` |
| `NCP-ANCHOR-SCHEMA-INVALID` | `NPS-CLIENT-BAD-FRAME` |
| `NCP-ANCHOR-ID-MISMATCH` | `NPS-CLIENT-CONFLICT` |
| `NCP-FRAME-UNKNOWN-TYPE` | `NPS-CLIENT-BAD-FRAME` |
| `NCP-FRAME-PAYLOAD-TOO-LARGE` | `NPS-LIMIT-PAYLOAD` |
| `NCP-FRAME-FLAGS-INVALID` | `NPS-CLIENT-BAD-FRAME` |
| `NCP-BINARY-VECTOR-MALFORMED` | `NPS-CLIENT-BAD-FRAME` |
| `NCP-BINARY-VECTOR-DIM-MISMATCH` | `NPS-CLIENT-BAD-FRAME` |
| `NCP-BINARY-VECTOR-INDEX-INVALID` | `NPS-CLIENT-BAD-FRAME` |
| `NCP-BINARY-VECTOR-DTYPE-UNSUPPORTED` | `NPS-CLIENT-BAD-FRAME` |
| `NCP-BINARY-VECTOR-TRUNCATED` | `NPS-CLIENT-BAD-FRAME` |
| `NCP-STREAM-SEQ-GAP` | `NPS-STREAM-SEQ-GAP` |
| `NCP-STREAM-NOT-FOUND` | `NPS-STREAM-NOT-FOUND` |
| `NCP-STREAM-LIMIT-EXCEEDED` | `NPS-STREAM-LIMIT` |
| `NCP-ENCODING-UNSUPPORTED` | `NPS-SERVER-ENCODING-UNSUPPORTED` |
| `NCP-VERSION-INCOMPATIBLE` | `NPS-PROTO-VERSION-INCOMPATIBLE` |
| `NCP-PREAMBLE-INVALID` | `NPS-PROTO-PREAMBLE-INVALID` |
| `NIP-CONTEXT-INVALID` | `NPS-CLIENT-BAD-FRAME` |
| `NIP-CONTEXT-UNTRUSTED` | `NPS-AUTH-FORBIDDEN` |
| `NIP-CONTEXT-BINDING-MISMATCH` | `NPS-AUTH-FORBIDDEN` |
| `NIP-CONTEXT-EXPIRED` | `NPS-AUTH-FORBIDDEN` |
| `NIP-CONTEXT-REPLAY` | `NPS-CLIENT-CONFLICT` |
| `NIP-CONTEXT-VERIFIER-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` |
| `NIP-ASSURANCE-MISMATCH` | `NPS-CLIENT-BAD-FRAME` |
| `NIP-ASSURANCE-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` |
| `NIP-REPUTATION-ENTRY-INVALID` | `NPS-CLIENT-BAD-FRAME` |
| `NIP-REPUTATION-LOG-UNREACHABLE` | `NPS-DOWNSTREAM-UNAVAILABLE` |
| `NWP-AUTH-ASSURANCE-TOO-LOW` | `NPS-AUTH-FORBIDDEN` |
| `NWP-AUTH-REPUTATION-BLOCKED` | `NPS-AUTH-FORBIDDEN` |
| `NWP-AUTH-NID-*` | `NPS-AUTH-*` (maps per specific error) |
| `NWP-BUDGET-EXCEEDED` | `NPS-LIMIT-BUDGET` |
| `NWP-LLM-CONTEXT-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` |
| `NWP-LLM-CONTEXT-EXPIRED` | `NPS-CLIENT-GONE` |
| `NWP-LLM-CONTEXT-VERSION-CONFLICT` | `NPS-CLIENT-CONFLICT` |
| `NWP-LLM-CONTEXT-BINDING-MISMATCH` | `NPS-CLIENT-CONFLICT` |
| `NWP-LLM-CONTEXT-FORBIDDEN` | `NPS-AUTH-FORBIDDEN` |
| `NWP-LLM-CONTEXT-LIMIT-EXCEEDED` | `NPS-LIMIT-RESOURCE` |
| `NWP-LLM-CONTEXT-OPERATION-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` |
| `NWP-RESERVED-TYPE-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` |
| `NWP-HTTP-ORIGIN-FORBIDDEN` | `NPS-AUTH-FORBIDDEN` |
| `NWP-HTTP-CONTENT-TYPE-UNSUPPORTED` | `NPS-CLIENT-BAD-FRAME` |
| `NWP-HTTP-ACCEPT-UNSATISFIABLE` | `NPS-CLIENT-BAD-PARAM` |
| `NWP-HTTP-REQUEST-ID-MISMATCH` | `NPS-CLIENT-BAD-PARAM` |
| `NWP-HTTP-FRAME-BODY-MALFORMED` | `NPS-CLIENT-BAD-FRAME` |
| `NWP-CAPABILITY-ADVERTISED-UNIMPLEMENTED` | `NPS-SERVER-UNSUPPORTED` |
| `NWP-NODE-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` |
| `NWP-ANCHOR-NOT-LEADER` | `NPS-CLIENT-CONFLICT` |
| `NWP-ANCHOR-EPOCH-FENCED` | `NPS-CLIENT-CONFLICT` |
| `NWP-BRIDGE-DIRECTION-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` |
| `NDP-CLUSTER-SPLIT` | `NPS-CLIENT-CONFLICT` |
| `NIP-CERT-CAPABILITIES-EXCEEDED` | `NPS-AUTH-FORBIDDEN` |
| `NIP-CERT-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` |
| `NIP-CERT-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` |
| `NIP-CERT-SCOPE-VIOLATION` | `NPS-AUTH-FORBIDDEN` |
| `NDP-RESOLVE-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` |
| `NDP-RESOLVE-AMBIGUOUS` | `NPS-CLIENT-CONFLICT` |
| `NDP-RESOLVE-TIMEOUT` | `NPS-SERVER-TIMEOUT` |
| `NDP-RESOLVE-STALE` | `NPS-CLIENT-NOT-FOUND` |
| `NDP-ANNOUNCE-STALE` | `NPS-CLIENT-NOT-FOUND` |
| `NDP-ANNOUNCE-SIGNATURE-INVALID` | `NPS-AUTH-UNAUTHENTICATED` |
| `NDP-ANNOUNCE-NID-MISMATCH` | `NPS-CLIENT-BAD-FRAME` |
| `NDP-ANNOUNCE-ROLE-REMOVED` | `NPS-CLIENT-BAD-FRAME` |
| `NDP-ANNOUNCE-ROLE-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` |
| `NDP-ANNOUNCE-CONFLICT` | `NPS-CLIENT-CONFLICT` |
| `NDP-ANNOUNCE-PROFILE-VIOLATION` | `NPS-AUTH-FORBIDDEN` |
| `NDP-GRAPH-SEQ-ROLLBACK` | `NPS-CLIENT-BAD-FRAME` |
| `NDP-GRAPH-SEQ-GAP` | `NPS-STREAM-SEQ-GAP` |
| `NDP-GRAPH-INVALID` | `NPS-CLIENT-BAD-FRAME` |
| `NDP-GRAPH-TOO-LARGE` | `NPS-LIMIT-PAYLOAD` |
| `NDP-FEDERATION-LOOP` | `NPS-CLIENT-CONFLICT` |
| `NDP-ISSUER-NOT-ALLOWED` | `NPS-AUTH-FORBIDDEN` |
| `NDP-CA-ATTEST-REQUIRED` | `NPS-AUTH-UNAUTHENTICATED` |
| `NDP-REGISTRY-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` |
| `NOP-TASK-TIMEOUT` | `NPS-SERVER-TIMEOUT` |
| `NOP-DELEGATE-SCOPE-VIOLATION` | `NPS-AUTH-FORBIDDEN` |

For the complete mapping, see each sub-protocol's error-code chapter.

---

## Native-Mode Error Frame Format

In native mode, errors are returned via an NCP error frame:

```json
{
  "frame": "0xFE",
  "status": "NPS-CLIENT-NOT-FOUND",
  "error": "NCP-ANCHOR-NOT-FOUND",
  "anchor_ref": "sha256:...",
  "message": "Schema anchor not found in cache, please resend AnchorFrame"
}
```

Frame type `0xFE` is the NPS unified error frame (allocated from the Reserved range), shared across all protocol layers.

---

*Copyright: LabAcacia / INNO LOTUS PTY LTD · Apache 2.0*
