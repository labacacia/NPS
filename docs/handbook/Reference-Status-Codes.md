# Reference: Status Codes

**Status:** ✅ Reviewed for v1.0.0-alpha.19 release

NPS status codes are the **coarse classification layer** of the two-level error system. Where protocol error codes (e.g. `NCP-ANCHOR-NOT-FOUND`) tell you exactly what went wrong, status codes group errors into broad transport-visible categories used for retry logic, HTTP mapping in overlay mode, and SDK-level error classification.

One status class can contain many protocol error codes. For example, `NPS-CLIENT-NOT-FOUND` covers `NCP-ANCHOR-NOT-FOUND`, `NWP-ACTION-NOT-FOUND`, `NWP-TASK-NOT-FOUND`, `NDP-RESOLVE-NOT-FOUND`, and more.

## The Two-Level System

```
Fine-grained layer            Coarse layer          HTTP (overlay mode only)
NCP-ANCHOR-NOT-FOUND     →   NPS-CLIENT-NOT-FOUND  →   404
NWP-BUDGET-EXCEEDED      →   NPS-LIMIT-BUDGET      →   429
NIP-CERT-EXPIRED         →   NPS-AUTH-UNAUTHENTICATED → 401
NIP-REPUTATION-GOSSIP-FORK → NPS-SERVER-INTERNAL   →   500
```

In **native mode**, errors are carried as status-code strings inside an `ErrorFrame` (0xFE). In **HTTP / overlay mode**, the NPS status additionally maps to a standard HTTP status code. The full protocol error code is always present in the error frame body for diagnostics.

For the complete list of protocol error codes, see [Reference: Error Codes](./Reference-Error-Codes.md).

## Status Code Format

```
NPS-{CATEGORY}[-{DETAIL}]
```

For example: `NPS-OK`, `NPS-CLIENT-NOT-FOUND`, `NPS-SERVER-UNSUPPORTED`.

## Status Categories

| Category | Meaning | HTTP Analogue |
|----------|---------|---------------|
| `OK` | Successful operation | 2xx |
| `CLIENT` | Client-side error (format, params, permission) | 4xx |
| `AUTH` | Authentication or authorization failure | 401 / 403 |
| `LIMIT` | Resource or quota limits exceeded | 413 / 429 |
| `SERVER` | Server-side fault or unsupported operation | 5xx / 501 |
| `STREAM` | Stream transport errors | — |
| `PROTO` | Protocol-level pre-handshake errors; not always emitted on the wire | — |

---

## Success Codes

| NPS Status | HTTP | Description |
|------------|------|-------------|
| `NPS-OK` | 200 | Operation succeeded |
| `NPS-OK-ACCEPTED` | 202 | Asynchronous operation accepted |
| `NPS-OK-NO-CONTENT` | 204 | Operation succeeded with no response body |

---

## Client Errors (CLIENT)

These indicate a problem with the request itself — bad framing, missing resources, or state conflicts.

| NPS Status | HTTP | Description |
|------------|------|-------------|
| `NPS-CLIENT-BAD-FRAME` | 400 | Frame format invalid; the frame cannot be parsed or violates structural constraints |
| `NPS-CLIENT-BAD-PARAM` | 400 | Request parameter invalid; the frame is well-formed but a field value is rejected |
| `NPS-CLIENT-NOT-FOUND` | 404 | Target resource does not exist |
| `NPS-CLIENT-CONFLICT` | 409 | Resource state conflict (duplicate key, stale anchor, task already in terminal state) |
| `NPS-CLIENT-GONE` | 410 | Resource permanently removed |
| `NPS-CLIENT-UNPROCESSABLE` | 422 | Request is syntactically valid but semantically unprocessable |

> **Normative consistency note (status v0.7 / error registry v1.9).** The error registry still references `NPS-CLIENT-RATE-LIMITED`, `NPS-CLIENT-REQUEST-TOO-LARGE`, and generic `NPS-LIMIT-EXCEEDED`, while the authoritative status table standardizes `NPS-LIMIT-RATE`, `NPS-LIMIT-PAYLOAD`, and `NPS-LIMIT-RESOURCE`. This page follows `spec/status-codes.md`; protocol-error mappings remain as written in `spec/error-codes.md` until that upstream inconsistency is reconciled.

---

## Authentication and Authorization (AUTH)

| NPS Status | HTTP | Description |
|------------|------|-------------|
| `NPS-AUTH-UNAUTHENTICATED` | 401 | No identity credential provided, or the provided credential is invalid, expired, or revoked |
| `NPS-AUTH-FORBIDDEN` | 403 | Identity is valid but lacks permission for this operation or resource |

---

## Resource Limits (LIMIT)

| NPS Status | HTTP | Description |
|------------|------|-------------|
| `NPS-LIMIT-RATE` | 429 | Request rate exceeded; check the `X-NWP-Rate-Reset` header for the reset timestamp |
| `NPS-LIMIT-BUDGET` | 429 | Cognon (CGN) token budget exceeded; see [Reference: Cognon Budget](./Reference-Cognon-Budget.md) |
| `NPS-LIMIT-PAYLOAD` | 413 | Payload exceeds the maximum negotiated frame size |
| `NPS-LIMIT-RESOURCE` | 429 | Bounded live-resource count exceeded, such as contexts, leases, or retained objects |

---

## Server Errors (SERVER)

| NPS Status | HTTP | Description |
|------------|------|-------------|
| `NPS-SERVER-INTERNAL` | 500 | Internal server error; the node encountered an unexpected condition |
| `NPS-SERVER-UNSUPPORTED` | 501 | The server does not implement the requested operation — for example, an unrecognized reserved `type` value in `QueryFrame` or `SubscribeFrame` |
| `NPS-SERVER-UNAVAILABLE` | 503 | Service temporarily unavailable; the node itself cannot serve the request |
| `NPS-SERVER-TIMEOUT` | 408/504 | Operation timed out on the server side |
| `NPS-SERVER-ENCODING-UNSUPPORTED` | 415 | Requested encoding tier is not supported by this node |
| `NPS-DOWNSTREAM-UNAVAILABLE` | 502/503 | A required downstream service that the node depends on (reputation log operator, external auth service, etc.) is unreachable. Distinct from `NPS-SERVER-UNAVAILABLE` — clients may retry against an alternate downstream when policy permits (NPS-RFC-0004) |

> **`NPS-SERVER-UNSUPPORTED` added in alpha.5.** HTTP 501 is now the correct mapping for nodes that receive a `QueryFrame` or `SubscribeFrame` with an unrecognized `type` field (e.g. `topology.snapshot` on a node that predates NPS-CR-0002). Prior to alpha.5 these requests would have been incorrectly mapped to 400 or 404.

---

## Stream Errors (STREAM)

Stream errors are emitted during active frame streaming. They are classified separately from client/server errors because they require stream-level recovery logic (reconnect, seq gap handling) rather than generic request retry.

| NPS Status | HTTP | Description |
|------------|------|-------------|
| `NPS-STREAM-SEQ-GAP` | 422 | Sequence gap detected; stream must be reconnected |
| `NPS-STREAM-NOT-FOUND` | 404 | Referenced `stream_id` does not exist |
| `NPS-STREAM-LIMIT` | 429 | Concurrent stream limit exceeded |

---

## Protocol-Level Errors (PROTO)

Pre-handshake or transport-bracketing errors. These are detected before any frame parser runs — or in place of it. In native mode they are generally **not emitted as ErrorFrames** because the peer has not been confirmed to speak NCP; the connection is closed. The status codes still exist for SDK-internal telemetry: logs, metrics, and close-reason classification.

| NPS Status | HTTP | Description |
|------------|------|-------------|
| `NPS-PROTO-VERSION-INCOMPATIBLE` | 426 | Client `min_version` exceeds the server's maximum supported version (HelloFrame negotiation failure). HTTP mode emits 426 Upgrade Required; native mode disconnects after at most one ErrorFrame. |
| `NPS-PROTO-PREAMBLE-INVALID` | 400 (not emitted) | Native-mode connection opened with bytes other than the constant preamble `b"NPS/1.0\n"`. Server closes silently within 500 ms; no ErrorFrame is sent. (NPS-RFC-0001) |

---

## Native-Mode Error Frame

In native mode, errors are returned via an NCP ErrorFrame (type `0xFE`):

```json
{
  "frame": "0xFE",
  "status": "NPS-CLIENT-NOT-FOUND",
  "error": "NCP-ANCHOR-NOT-FOUND",
  "anchor_ref": "sha256:...",
  "message": "Schema anchor not found in cache; please resend AnchorFrame"
}
```

The `status` field carries the NPS status code (coarse). The `error` field carries the protocol error code (fine-grained). Both are always present. Clients SHOULD branch on `status` for retry logic and surface `error` for diagnostics.

---

## See Also

- [Reference: Error Codes](./Reference-Error-Codes.md) — the full list of fine-grained protocol error codes

---

*Last reviewed at suite version: v1.0.0-alpha.19 release*
