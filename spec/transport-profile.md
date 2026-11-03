English | [中文版](./transport-profile.cn.md)

# NPS Transport Profile

> Historical draft — not a current conformance target. See the
> [transport reconciliation](./transport-reconciliation.md) for the conflict ledger
> and current NCP authority. The original text below is retained as history only.

**Title**: NPS Transport Profile  
**Version**: 0.1  
**Status**: Draft  
**Date**: 2026-05-10  
**Depends-On**: NPS-1-NCP v0.7

---

## 1. Overview

NPS is transport-agnostic at the protocol layer: NCP frames (defined in [NPS-1-NCP.md](NPS-1-NCP.md)) are a self-contained byte sequence whose meaning does not depend on the carrier. This profile standardises **which** transport bindings a conforming implementation MUST recognise, **how** each binding carries NCP frames on the wire, and **how** a server auto-detects the binding when multiple are exposed on a single endpoint.

The key words "MUST", "MUST NOT", "REQUIRED", "SHOULD", "SHOULD NOT", and "MAY" in this document are to be interpreted as described in RFC 2119.

This profile complements but does not replace NCP §2.2 (Transport Modes) and §2.6.1 (Connection Preamble). Where this document and NPS-1-NCP disagree, NPS-1-NCP is normative.

---

## 2. Transport Bindings

A conforming server MUST support **HTTP/1.1** and **TCP native**. All other bindings in this section are OPTIONAL but, when implemented, MUST conform to the rules below.

### 2.1 HTTP/1.1

- Method: `POST`
- Path: `/nps` (servers MAY expose protocol-specific paths such as `/nwp/...`; `/nps` is the canonical fallback for transport detection)
- Request `Content-Type` header: `application/x-nps-frame` MUST be sent by clients; servers MUST treat this value as authoritative evidence the body is an NCP frame.
- Response `Content-Type` header: `application/x-nps-frame` for native NCP frame responses.
- The HTTP body carries exactly one serialized NCP frame, or — when `Transfer-Encoding: chunked` is used — a sequence of frames concatenated in send order.
- Errors MAY be returned as an `ErrorFrame` (0xFE) inside an HTTP body and SHOULD additionally be reflected in the HTTP status line per §5.

### 2.2 HTTP/2

- All HTTP/1.1 rules in §2.1 apply.
- A single HTTP/2 connection MAY multiplex many request streams; each stream carries one logical NCP request/response pair.
- Server push MUST NOT be used to deliver NCP frames the client did not request.
- Implementations SHOULD prefer HTTP/2 over HTTP/1.1 when ALPN negotiation succeeds for `h2`.

### 2.3 WebSocket

- Connection is established via an HTTP/1.1 Upgrade (`Upgrade: websocket`) on path `/nps` (or an implementation-specific path).
- After the upgrade, NCP frames MUST be exchanged as **binary** WebSocket data frames. Text frames MUST NOT be used to carry NCP frames.
- One WebSocket data frame carries exactly one NCP frame; fragmentation across WebSocket continuation frames is permitted only at WebSocket-level boundaries and MUST NOT split an NCP frame across two NPS-level messages.

### 2.4 TCP Native

- Carrier: a single TCP connection (no HTTP envelope).
- Immediately after the TCP handshake completes, the client MUST emit the **8-byte connection preamble** specified in [NPS-1-NCP §2.6.1](NPS-1-NCP.md#261-connection-preamble-native-mode): `b"NPS/1.0\n"` (hex `4E 50 53 2F 31 2E 30 0A`).
- A server that does not receive the preamble MUST close the connection per the rules in §2.6.1; this profile does not weaken those requirements.
- After the preamble, the connection is a continuous bidirectional stream of NCP frames per [NPS-1-NCP §3](NPS-1-NCP.md).

### 2.5 QUIC

- Carrier: a QUIC connection negotiated via ALPN (see §3).
- Topology: **stream-per-session** — each NPS logical session occupies exactly one bidirectional QUIC stream. A long-lived session MAY remain on the same stream for its lifetime; a short request/response exchange MAY open a fresh stream and close it when complete.
- The 8-byte preamble specified in NPS-1-NCP §2.6.1 MUST be emitted by the client at the start of each stream, before the first NCP frame on that stream.
- 0-RTT data MAY be used; if it is, the preamble counts as part of the 0-RTT payload.

---

## 3. TLS

### 3.1 ALPN Token

The ALPN token `nps/1` identifies the NPS Transport Profile, version 1.x. Clients negotiating TLS for an NPS endpoint SHOULD offer `nps/1`. Servers SHOULD accept `nps/1` and SHOULD prefer it over generic tokens (`http/1.1`, `h2`) when the listening port is dedicated to NPS.

The `nps/1` token will be registered with the IANA TLS ALPN Protocol IDs registry.

### 3.2 TLS Version Requirements by Profile

| Deployment profile | TLS requirement |
|--------------------|-----------------|
| Public-federated (cross-organisation, internet-facing) | **MUST** use TLS 1.2 or higher; TLS 1.3 RECOMMENDED |
| Org-private (within a single organisation or trust domain) | **SHOULD** use TLS 1.2 or higher |
| Local-dev (loopback, single-host development) | **MAY** use TLS; plaintext is permitted |

Implementations MUST refuse SSLv3, TLS 1.0, and TLS 1.1 in the public-federated profile.

---

## 4. Detection Precedence Ladder

A server that exposes multiple bindings on a shared listener (for example, an HTTPS port that may also serve WebSocket upgrades or, for misrouted clients, a raw TCP preamble) MUST auto-detect the binding using the following ordered ladder. The **first** signal that produces a confident match wins; later signals MUST NOT override an earlier match.

| Priority | Signal | Selects |
|----------|--------|---------|
| 1 | TLS ALPN selected `nps/1` | NPS native (TCP, HTTP/2, or QUIC depending on TLS-layer carrier) |
| 2 | HTTP request `Content-Type: application/x-nps-frame` (and/or `Upgrade: websocket` on `/nps`) | HTTP / WebSocket binding (§2.1–§2.3) |
| 3 | First 8 bytes on the stream match the NCP native preamble `b"NPS/1.0\n"` | TCP native (§2.4) |
| 4 | Connection arrived on the default NPS port `7433` | Server MAY assume TCP native and read the preamble; a mismatch then closes per NPS-1-NCP §2.6.1 |

If none of the above match, the server MUST treat the connection as non-NPS and close it without emitting NPS framing on the wire.

> **Note on default port.** This profile reserves port **7433** as the default for transport-detection purposes. The NPS suite default operating port remains **17433** (NPS-1-NCP §2.3); 7433 is reserved for environments where a separate detection-only listener is desirable.

---

## 5. ErrorFrame → HTTP Status Mapping

In HTTP / WebSocket bindings, a server emitting an `ErrorFrame` (NCP 0xFE) SHOULD also reflect the failure in the HTTP status code. The following mappings are normative for the listed error codes; for codes not enumerated here, implementations SHOULD use the closest-match status code from [status-codes.md](status-codes.md).

| ErrorFrame code | HTTP status |
|------------------|-------------|
| `NCP-ANCHOR-ID-MISMATCH` | 409 Conflict |
| `NCP-ANCHOR-STALE` | 409 Conflict |
| `NCP-ANCHOR-NOT-FOUND` | 404 Not Found |
| `NCP-FRAME-TOO-LARGE` / `NCP-FRAME-PAYLOAD-TOO-LARGE` | 413 Content Too Large |
| `NCP-FRAME-UNKNOWN-TYPE` | 400 Bad Request |
| `NCP-FRAME-FLAGS-INVALID` | 400 Bad Request |
| `NCP-VERSION-INCOMPATIBLE` | 426 Upgrade Required |
| `NCP-ENCODING-UNSUPPORTED` | 415 Unsupported Media Type |
| `NCP-PREAMBLE-INVALID` | (no HTTP response — connection closed silently) |
| `NOP-COMPENSATION-FAILED` | 422 Unprocessable Content |
| `NWP-RESERVED-TYPE-UNSUPPORTED` | 501 Not Implemented |
| (any other protocol-level error) | 400 Bad Request |

Server-side faults (uncaught exception, internal error) MUST be returned as HTTP 500 with no `ErrorFrame` body unless the server is certain the peer has completed the NPS handshake.

---

## Change Log

| Version | Date | Notes |
|---------|------|-------|
| 0.1 | 2026-05-10 | Initial Draft. Defines HTTP/1.1, HTTP/2, WebSocket, TCP native, and QUIC bindings; ALPN token `nps/1`; detection precedence ladder; ErrorFrame → HTTP status mapping. Issue #31. |
