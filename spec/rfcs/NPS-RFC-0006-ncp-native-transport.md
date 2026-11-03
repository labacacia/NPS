English | [中文版](./NPS-RFC-0006-ncp-native-transport.cn.md)

<!--
Copyright 2026 INNO LOTUS PTY LTD
Developed under LabAcacia Open Source Initiative
Licensed under the Apache License, Version 2.0
-->

# NPS-RFC-0006: NCP Native Mode Transport Binding

> 2026-09-09 clarification: historical acceptance is retained. Conflicting older
> framing, early-data, rekey and admission prose does not override current NCP.
> See the [transport reconciliation](../transport-reconciliation.md), TRN-01..13,
> before implementing this RFC. This notice does not claim SDK qualification.

**RFC ID**: NPS-RFC-0006
**Status**: Accepted
**Version**: 0.2
**Date**: 2026-06-12
**Authors**: Ori Lynn / INNO LOTUS PTY LTD
**Depends-On**: NPS-1 (NCP) v0.7, NPS-3 (NIP) v0.9
**Issue**: GitHub NPS-Dev#45

---

## Abstract

NCP defines two transport modes: HTTP mode (NCP frames carried in HTTP bodies) and native
mode (direct TCP or QUIC connection). NCP v0.6 specified the connection preamble
(`b"NPS/1.0\n"`, NPS-RFC-0001) and the HelloFrame/CapsFrame handshake but left QUIC stream
mapping, rekeying, and conformance requirements implementation-defined.

This RFC formalises the native mode transport binding for both TCP and QUIC, enabling
interoperable implementations across all six reference SDKs.

---

## 1. Terminology

The key words "MUST", "MUST NOT", "REQUIRED", "SHOULD", "SHOULD NOT", and "MAY" are used
as per RFC 2119.

---

## 2. TCP Transport Binding

### 2.1 Connection Establishment

1. Client opens a TCP connection to the server's NCP port (default 17433).
2. Client MUST send the 8-byte connection preamble `b"NPS/1.0\n"` as the first bytes
   (NPS-RFC-0001).
3. Client MUST send a HelloFrame (`0x06`) immediately after the preamble, within 5 seconds.
4. Server MUST respond with a CapsFrame (`0x04`) within 5 seconds of receiving HelloFrame,
   or send an ErrorFrame and close the connection.

### 2.2 Frame Framing Over TCP

NCP frames over TCP use a 5-byte length-prefixed framing:

```
[4 bytes: payload length, big-endian uint32]
[1 byte:  frame-type byte (flags + type)]
[N bytes: frame payload]
```

Total frame size MUST NOT exceed the negotiated `max_frame_payload` (HelloFrame field).
If a frame exceeds this limit the receiver MUST return `NCP-FRAME-TOO-LARGE` and MAY
close the connection.

### 2.3 Connection Close

- Graceful close: sender sends a CapsFrame with `status = "closing"` before closing the
  TCP socket.
- Abrupt close: TCP FIN or RST; the peer SHOULD treat any in-flight requests as failed
  with `NCP-NODE-UNAVAILABLE`.

---

## 3. QUIC Transport Binding

### 3.1 ALPN

NCP-over-QUIC uses ALPN identifier **`nps/1.0`** (the suite-wide NPS native-mode ALPN token;
see §6.1). This supersedes the provisional `ncp/1` used in v0.1 of this draft. IANA
registration is pending a follow-up RFC; implementations MUST use `nps/1.0`.

### 3.2 Stream Mapping

- Each NCP logical channel maps to **one bidirectional QUIC stream**.
- HelloFrame MUST be sent on stream 0 (the first client-initiated bidirectional stream).
- The server's CapsFrame MUST be sent on the same stream 0.
- Subsequent frames use new bidirectional streams (stream 4, 8, 12, … per QUIC stream
  numbering rules for client-initiated bidirectional streams).
- Unidirectional QUIC streams are reserved for future use and MUST be ignored by current
  implementations.

### 3.3 Frame Framing Over QUIC

Frames over QUIC do not need length prefixing at the NCP layer because QUIC provides
stream-level framing. The frame encoding is:

```
[1 byte: frame-type byte (flags + type)]
[N bytes: frame payload]
```

Each NCP frame occupies exactly one QUIC STREAM frame sequence (may span multiple QUIC
packets but MUST be delivered atomically to the NCP layer).

### 3.4 Connection Close

Server or client closes the QUIC connection with `CONNECTION_CLOSE` frame, application
error code `0x00` for graceful close. Error code `0x01` indicates protocol violation.

### 3.5 HelloFrame on QUIC

HelloFrame over QUIC MUST set the `transport` field to `"quic"`. Servers that do not
support QUIC MUST return an ErrorFrame with `NCP-VERSION-INCOMPATIBLE` when they detect
the ALPN negotiation has yielded `nps/1.0` over QUIC unexpectedly.

---

## 4. Rekeying Protocol

When E2E encryption is active (NCP §7.4), implementations MUST rekey the session after:

- **2^32 frames** have been sent on the connection (either direction), OR
- **24 hours** of wall-clock time since the last key establishment

whichever occurs first. Implementations SHOULD rekey at 2^32 - 65536 frames (slightly
early) to avoid the limit being hit mid-frame.

### 4.1 In-band Rekeying Signal

The party initiating rekey sends a frame with the EXT header field `rekey: true`. The
receiving party MUST complete the current frame exchange, then perform key derivation using
the agreed KDF before sending the next frame.

### 4.2 Rekey Failure

If a peer continues sending encrypted frames past 2^32 frames without rekeying, the
receiver MUST send `NCP-REKEY-REQUIRED` and MUST close the connection.

---

## 5. `max_concurrent_streams` Negotiation

HelloFrame carries `max_concurrent_streams` (uint16). The server echoes its own cap in the
CapsFrame. The effective limit is `min(client_cap, server_cap)` and applies bidirectionally.

Implementations MUST track the count of open streams and MUST reject new stream-opening
frames that would exceed the limit with `NCP-STREAM-LIMIT-EXCEEDED`. Streams that are
fully closed (both sides have sent terminal frames) MUST be released from the count.

Default: 32. Minimum: 1. Maximum: 65535.

---

## 6. TLS Binding & Mutual Authentication

This section standardizes the transport-security layer for native-mode NCP, resolving the
prior open questions on ALPN and TLS framing (OQ-1, OQ-3).

### 6.1 ALPN token

Both native-mode transports advertise the suite-wide ALPN token **`nps/1.0`**:

- **TCP**: the connection is TLS-wrapped (§6.2) and negotiates ALPN `nps/1.0`.
- **QUIC**: ALPN `nps/1.0` is negotiated as part of the QUIC/TLS 1.3 handshake (§3.1).

A server that does not recognise `nps/1.0` MUST fail the TLS handshake with
`no_application_protocol` (TLS alert 120) rather than fall through to a default protocol.

### 6.2 TLS framing (resolves OQ-3)

Native-mode-over-TCP MUST use **TLS-wrapped** transport: TLS 1.3 is established first, and the
RFC-0001 preamble (`b"NPS/1.0\n"`) plus all NCP frames travel **inside** the TLS session.
STARTTLS-style in-band upgrade is **NOT** permitted for native mode (it leaves the preamble and
HelloFrame in cleartext). The `local-dev` profile (NPS-4 §7.2) MAY run without TLS for
loopback testing only. QUIC's TLS 1.3 is built in and satisfies this requirement intrinsically.

### 6.3 Mutual authentication (mTLS) with NIP certificates

When the higher-layer profile requires authentication, native mode MUST use **mutual TLS**:

- The server presents a NIP-issued certificate (NPS-3, RFC-0002 X.509 NID profile) whose NID
  matches the connection's advertised node identity.
- The client presents its NIP-issued certificate during the TLS handshake. The server MUST
  verify the chain to a configured trust anchor (`trust_anchors`, NWP v0.13 §4.1) and bind the
  certificate's NID to the NCP session.
- The post-handshake `IdentFrame` (§2 of NPS-1 native handshake) remains the authority for
  capabilities, scope, and assurance level; the mTLS certificate NID and the `IdentFrame` NID
  MUST match, else the server MUST close with `NCP-NID-MISMATCH`.

mTLS is the transport-layer admission gate that `nps-ingress` (L2) terminates; see the NPS-Node
Profile L2 requirements.

### 6.4 Session resumption

To reduce cold-start latency for repeated short-lived native connections, servers MAY issue a
**session-resumption ticket** (TLS 1.3 `NewSessionTicket`). Resumed sessions:

- MUST re-present the client certificate (the ticket does not waive mTLS); a resumed session
  whose certificate NID differs from the ticket's bound NID MUST be rejected with
  `NCP-NID-MISMATCH`.
- MUST still send the RFC-0001 preamble and a fresh HelloFrame; resumption shortcuts the TLS
  handshake only, not the NCP handshake.
- SHOULD be bounded to the certificate's remaining validity and a server-configured ticket
  lifetime (recommended ≤ 24 h, aligned with the rekeying window in §4).

---

## 7. Conformance Requirements

A native-mode conformant NCP implementation MUST:

1. Support the TCP transport binding (§2)
2. Send the RFC-0001 preamble before HelloFrame
3. Negotiate `max_concurrent_streams`
4. Honor the negotiated frame size limit
5. Implement rekeying when E2E encryption is active (§4)
6. Emit ErrorFrame before closing on any protocol violation
7. Use TLS-wrapped transport with ALPN `nps/1.0` (§6.1–§6.2) in all non-`local-dev` profiles
8. Perform mutual TLS with NIP certificates and bind the certificate NID to the NCP session,
   rejecting an `IdentFrame`/certificate NID mismatch with `NCP-NID-MISMATCH` (§6.3)

A native-mode conformant NCP implementation SHOULD:

1. Support the QUIC transport binding (§3)
2. Implement graceful close (CapsFrame `status="closing"`)
3. Offer TLS 1.3 session-resumption tickets for short-lived repeat connections (§6.4)

---

## 8. Security Considerations

Native mode connections MUST use TLS 1.3 or QUIC's built-in encryption in production
deployments (`local-dev` profile exempted per NPS-4 §7.2). The connection preamble
(`b"NPS/1.0\n"`) travels in the clear before TLS negotiation only when the transport is
raw TCP without STARTTLS; implementations SHOULD use TLS from the first byte where
possible (i.e. TLS wrapping the NCP native connection, with preamble sent inside TLS).

E2E encryption at the NCP frame layer (§7.4) is a defense-in-depth mechanism and does not
replace transport-layer TLS.

---

## 9. Question Dispositions

| ID | Question | Status |
|----|----------|--------|
| OQ-1 | IANA registration for ALPN `nps/1.0` | Resolved (value chosen, §6.1); IANA registration tracked in a follow-up RFC after the IETF Internet-Draft |
| OQ-2 | QUIC version: QUIC v1 (RFC 9000) only, or also QUIC v2? | Resolved for current contract: QUIC v1 only; QUIC v2 requires a future RFC amendment |
| OQ-3 | STARTTLS vs. TLS-wrapped: mandate one? | Resolved (§6.2 — TLS-wrapped mandated for native mode; STARTTLS prohibited) |

---

## 10. Changelog

| Version | Date | Changes |
|---------|------|---------|
| 0.2 status reconciliation | 2026-09-05 | Kept Accepted status aligned in both languages and fixed the current QUIC contract to v1; QUIC v2 requires a future amendment. |
| 0.2 acceptance | 2026-07-05 | Accepted; implementation activation remains separately gated. |
| 0.2 | 2026-06-12 | Draft → **Proposed**. Added §6 TLS Binding & Mutual Authentication: suite-wide ALPN `nps/1.0` (supersedes provisional `ncp/1`), TLS-wrapped framing mandated for native-mode-over-TCP (resolves OQ-3), mTLS with NIP certificates + session-NID binding (`NCP-NID-MISMATCH`), TLS 1.3 session-resumption tickets. Conformance §7 gains TLS/mTLS MUSTs. OQ-1/OQ-3 resolved. §7–§10 renumbered. |
| 0.1 | 2026-05-28 | Initial Draft |

---

*Attribution: LabAcacia / INNO LOTUS PTY LTD · Apache 2.0*
