English | [中文版](./transport-reconciliation.cn.md)

# Transport reconciliation — alpha.20

- Status: Clarification of existing NCP v0.12 requirements; implementation audit pending
- Date: 2026-09-09
- Tracking: [P20-106 / #128](https://github.com/labacacia/NPS-Dev/issues/128)

## Authority

The [current NCP specification](./NPS-1-NCP.md) is the wire-format authority.
RFC-0006 remains the historical accepted transport decision, with §6 as the TLS
binding referenced by NCP §7.5. Its conflicting older prose is not an alternative
wire format. The May 2026 transport-profile draft is historical, not a conformance
target. This reconciliation does not change protocol versions or allocate frames.

## Conflict ledger

| ID | Historical text | Current interpretation / source |
|---|---|---|
| TRN-01 | Draft requires every server to offer HTTP and TCP | Declare and test the deployed binding. NCP §2.2 defines modes, not mandatory dual deployment. |
| TRN-02 | Draft uses generic /nps and application/x-nps-frame | NWP HTTP uses its specified endpoints and application/nwp-frame (NCP §2.2). No generic binary/chunked tunnel is standardized by that draft. |
| TRN-03 | Draft uses port 7433 and ALPN nps/1 | Default port 17433 (NCP §2.3); native ALPN nps/1.0 (NCP §7.5). |
| TRN-04 | RFC §2.2 has a five-byte TCP header; §3.3 removes QUIC lengths | Both use the NCP §3 four-byte default or eight-byte EXT header. Type and flags are separate bytes. Carrier reads may split or coalesce frames; parse declared lengths, never read boundaries. |
| TRN-05 | Draft creates a new session/preamble on every QUIC stream | Preserve RFC §3.2 connection-level handshake on client bidirectional stream 0 and channel-per-bidirectional-stream mapping; new channel streams do not repeat a session handshake. |
| TRN-06 | Draft permits early data | NCP-P19-06 prohibits all NPS preambles and frames in TLS/QUIC 0-RTT. Resumption still requires a confirmed handshake, fresh preamble and Hello. |
| TRN-07 | RFC §8 allows cleartext preamble before TLS | RFC §6.2 and NCP §7.5 require TLS wrapping before any NPS bytes outside explicit local-dev. No STARTTLS or plaintext sniff-and-upgrade. |
| TRN-08 | RFC §4.1 invents EXT rekey:true | EXT selects header length, not a metadata map. NCP §7.4 defines NCP-REKEY-REQUIRED and ECDH/HKDF rotation; do not emit the invented field. |
| TRN-09 | RFC §5 uses uint16 and maximum 65535 | NCP Hello/Caps define uint32, default 32, negotiated minimum. Local resource ceilings may be lower without narrowing the wire type. |
| TRN-10 | RFC §3.5 requires Hello.transport | Current NCP Hello schema does not require this field. Carrier selection precedes Hello; do not reject a conforming Hello because it omits transport. |
| TRN-11 | RFC §2.3/§7 invent Caps.status=closing and fail every outstanding call | No new closing Caps shape is defined. Drain locally and close the carrier; transport loss does not prove non-execution. Runtime retry/indeterminate semantics are separately tracked in #126. |
| TRN-12 | RFC §7 requires an ErrorFrame for every malformed connection | NCP §2.6 admission rules require silent close for invalid/partial preamble or invalid Hello. Never send NPS errors before a safe authenticated framing context exists. |
| TRN-13 | Draft makes WebSocket and generic HTTP/2 tunnels canonical | These draft bindings are not qualification evidence. A separately specified adapter must declare its contract; implementing a tunnel alone does not establish canonical NWP HTTP conformance. |

## Implementation follow-up

P20-201..208 and P20-301..306 must audit all six SDKs and applicable daemons against
TRN-01..13. In particular, check split/coalesced TCP and QUIC reads, both header
widths, post-TLS preamble ordering, no 0-RTT, stream-0-only handshake, omitted
Hello.transport, uint32 negotiation and rejection before admission. A document
correction is not a passing runtime test.

P20-108 owns executable shared vectors; #129 owns normative integration/version
freeze. Any newly discovered ambiguity requiring new wire semantics must be
resolved through a pre-1.0 CR, not inferred from a transport library API.

