# SDK Common Patterns

**Status:** ✅ Reviewed for v1.0.0-alpha.19 (released 2026-09-06)

> **Audience:** Developers building Agents or Nodes with any NPS SDK.
> **Source-of-truth precedence:** `spec/` documents win over this page if they disagree.

This page is a cookbook of cross-cutting patterns. Every section applies to all six SDK languages (.NET, Python, TypeScript, Java, Rust, Go) unless a language-specific note appears. Code snippets use pseudo-code; adapt to your SDK's surface.

---

## Table of contents

1. [Retries](#retries)
2. [Streaming queries](#streaming-queries)
3. [Subscriptions](#subscriptions)
4. [Capability negotiation](#capability-negotiation)
5. [Token budget (CGN)](#token-budget-cgn)
6. [Version pinning](#version-pinning)
7. [Encoding tier](#encoding-tier)
8. [Serving local actions to external MCP / A2A clients (inbound Bridge)](#serving-local-actions-to-external-mcp--a2a-clients-inbound-bridge)

---

## Retries

### Which errors are retryable

Only retry **server-side transient errors** on **idempotent** actions:

| Error code | NPS status | Retryable? | Condition |
|-----------|-----------|-----------|-----------|
| `NPS-SERVER-INTERNAL` | `NPS-SERVER-INTERNAL` | Yes | Idempotent action only |
| `NPS-SERVER-THROTTLE` | `NPS-LIMIT-RATE` (`NWP-RATE-LIMIT-EXCEEDED`) | Yes | After waiting for `X-NWP-Rate-Reset` |
| `NPS-SERVER-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | Yes | With backoff; idempotent actions only |
| Any `NPS-CLIENT-*` | — | No | Client error; fix the request |
| `NWP-BUDGET-EXCEEDED` | `NPS-LIMIT-BUDGET` | No | Increase budget or reduce scope |
| `NIP-CERT-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | No | Re-enroll with the CA first |
| `NIP-CERT-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | No | Renew certificate first |
| `NWP-AUTH-ASSURANCE-TOO-LOW` | `NPS-AUTH-FORBIDDEN` | No | Upgrade assurance level |

An action is idempotent when its `ActionSpec.idempotent` flag in the NWM is `true`. Never assume idempotency — always read the manifest.

### Recommended retry policy

```
max_attempts = 3
base_delay_ms = 200
multiplier = 2.0   // exponential backoff

for attempt in 1..max_attempts:
    response = call(action_frame)

    if response.error in TRANSIENT_ERRORS and action_spec.idempotent:
        if attempt < max_attempts:
            sleep(base_delay_ms * multiplier^(attempt - 1))
            continue

    break  // success or non-retryable
```

Use `ActionFrame.idempotency_key` (UUID v4) to let the server deduplicate replayed requests. The same key is valid for 24 hours on the server side. Set it on the first attempt and keep it across retries.

### What NOT to retry

- Any error in the `NPS-CLIENT-*` family — retrying will not change the outcome.
- `NWP-BUDGET-EXCEEDED` — the request itself exceeds budget; reduce scope.
- Revoked or expired NID — certificate renewal must happen before the next request.
- `NWP-ACTION-IDEMPOTENCY-CONFLICT` — another request with the same key is already in progress; wait for it rather than sending again.

---

## Streaming queries

A streaming query is a `QueryFrame` with `stream: true` (or using the `/stream` sub-path). The node returns a sequence of `StreamFrame (0x03)` batches rather than a single `CapsFrame`.

### Reading the stream

```
stream_id = new_uuid()
query_frame = {
    "frame": "0x10",
    "anchor_ref": schema_ref,
    "stream": true,
    "filter": { ... },
    "limit": 100,        // records per batch
    "request_id": stream_id
}

send(query_frame)

while true:
    frame = receive()
    if frame.type != StreamFrame:
        break  // error or unexpected

    process(frame.data)

    if frame.is_last == true:  // FINAL flag
        break
```

**The `is_last` flag is the termination signal.** The spec calls it `is_last = true` on the terminal frame (§6.6). Do not rely on connection close as the only termination signal; always check `is_last`.

The first frame (`seq = 0`) carries metadata: `estimated_total` (total matching records, -1 = unknown) and `request_id` echo. Subsequent frames carry data batches.

### Early cancellation

To stop the stream before `is_last`:

```
cancel_frame = {
    "frame": "0x12",
    "action": "unsubscribe",
    "stream_id": stream_id   // matches QueryFrame.request_id
}
send(cancel_frame)
```

This reuses `SubscribeFrame` as the protocol-wide stream cancellation signal. Nodes route it by `stream_id` regardless of whether the stream originated from a query or a subscription.

### Timeout

Set a **per-stream timeout** (from first frame received to last), not a per-chunk timeout. A single batch may be slow to arrive if the node is querying a large dataset; a per-chunk timeout would produce spurious cancellations.

```
stream_deadline = now() + stream_timeout_ms

for each frame:
    if now() > stream_deadline:
        send(cancel_frame)
        raise TimeoutError
    process(frame)
```

---

## Subscriptions

A subscription (`SubscribeFrame` with `action = "subscribe"`) establishes a long-lived push channel for incremental changes. NWP v0.13 (CR-0006, accepted 2026-05-28) gives `SubscribeFrame` a formal spec (§13): a `subscription_id` (UUID v4), a QueryFrame-compatible filter, `heartbeat_interval_ms`, `max_events`, and an opaque `cursor` for lossless resume. The `topology:subscribe` capability is required (MUST, §12.4) for topology subscriptions.

### Establishing a subscription

```
subscribe_frame = {
    "frame": "0x12",
    "action": "subscribe",
    "subscription_id": new_uuid(),          // UUID v4
    "anchor_ref": schema_ref,
    "filter": { "status": { "$eq": "active" } },
    "heartbeat_interval_ms": 30000,
    "max_events": 0                          // 0 = unbounded
}
send(subscribe_frame)

// Wait for acknowledgement CapsFrame
ack = receive_caps_frame(anchor_ref = "nps:system:subscribe:ack")
assert ack.data[0].status == "subscribed"
cursor = ack.data[0].cursor

// Enter push loop
while connected:
    diff = receive_diff_frame()
    apply_diff(diff.patch)
    cursor = diff.cursor
```

### Reconnection and lossless resume

If the connection drops, reconnect with the last opaque `cursor` to resume without loss:

```
subscribe_frame = {
    ...
    "action": "subscribe",
    "subscription_id": subscription_id,
    "cursor": cursor                        // opaque resume token (CR-0006)
}
```

If the `cursor` is outside the node's buffer window (typically 10 minutes or 10,000 events), the node returns `NWP-SUBSCRIBE-SEQ-TOO-OLD`. In that case:

1. Issue a fresh full QueryFrame (no streaming) to re-sync state.
2. Re-subscribe without a `cursor`.

The node emits a `heartbeat_interval_ms` keepalive on the channel; if no event or heartbeat arrives within ~3× that interval, treat the channel as dead and resume from the last `cursor`.

### Cancellation

```
cancel_frame = {
    "frame": "0x12",
    "action": "unsubscribe",
    "subscription_id": subscription_id
}
send(cancel_frame)
```

The node stops pushing DiffFrames and frees server-side state. Always unsubscribe explicitly rather than just closing the connection.

---

## Capability negotiation

Capabilities flow in two directions:

- **Agent → Node**: declared in `IdentFrame.capabilities`; the Node checks these on every request.
- **Node → Agent**: declared in `NWM.capabilities`; the Agent reads these before issuing requests.

### Advertising capabilities (Agent side)

Include every capability your Agent holds in the IdentFrame you send at connection time. The IdentFrame is signed by the CA, so the declaration is integrity-protected.

```json
"capabilities": ["nwp:query", "nwp:action", "nwp:stream", "topology:read"]
```

Do not include capabilities you do not actually support — the Node may use them to route or prioritize traffic, and a false declaration creates trust debt.

### Checking node capabilities before issuing requests (Agent side)

Read the NWM before the first request and cache it. From NWP v0.14, every `GET /.nwm` carries an `X-NWM-Version` response header equal to the manifest's monotonic `manifest_version` (uint32, with `manifest_updated_at` as an ISO 8601 timestamp). Cache that value and send `If-None-Match: <manifest_version>` to get a cheap `304 Not Modified` when nothing changed:

```
nwm = fetch_nwm("nwp://api.example.com/orders/.nwm")   // record X-NWM-Version
// later: GET /.nwm with `If-None-Match: <cached manifest_version>` → 304 if unchanged

if not nwm.capabilities.stream_query:
    // Fall back to paginated single queries
    use_pagination_mode()

if nwm.capabilities.vector_search:
    // Use semantic search path
    query_frame.vector_search = { ... }
```

### Runtime gate check (Node side)

In HTTP mode, the request carries an `X-NWP-Capabilities` header derived from the caller's IdentFrame. Check it in middleware before serving any capability-gated route:

```
// Check for topology:read on topology.* endpoints
required_cap = "topology:read"
caller_caps  = parse_header(request.headers["X-NWP-Capabilities"])

if required_cap not in caller_caps:
    return error("NWP-TOPOLOGY-UNAUTHORIZED", "NPS-AUTH-FORBIDDEN")
```

---

## Token budget (CGN)

NPS uses **Cognon (CGN)** as a model-agnostic token-accounting unit. Nodes consume CGN against your declared budget and report actual usage in response headers.

> **CGN-Estimate vs CGN-Billing (token-budget v0.5+).** CGN is split into two profiles. **CGN-Estimate** covers budgets/quota/telemetry — sampling and byte-size fallback are permitted (±5 % drift), no signing. **CGN-Billing** covers commercial settlement — it requires the `verified_tokenizer` tier (NIP §5.1), NID-signed metering records, no sampling/fallback, and audit-log integration. Profile is carried by the §4.2 headers `X-NWP-Tokens-Profile`, `X-NWP-Billing-Record`, and `X-NWP-Billing-Tokenizer-Tier`; if these are silent the response defaults to CGN-Estimate.

### Setting a budget on requests

HTTP mode:
```
X-NWP-Budget: 2000
X-NWP-Tokenizer: cl100k_base
```

Native mode (QueryFrame field):
```json
"token_budget": 2000,
"tokenizer": "cl100k_base"
```

Declare your tokenizer when known — it improves accuracy of the node's estimates.

### Reading remaining budget from responses

Every response in HTTP mode carries:

```
X-NWP-Tokens: 380          ← CGN consumed by this response
X-NWP-Tokens-Native: 365   ← native tokens (when tokenizer is known)
X-NWP-Tokenizer-Used: cl100k_base
```

Track cumulative consumption across requests to stay within your session budget:

```
session_budget = 50000
used_cgn = 0

for each request:
    response = send(request)
    used_cgn += parse_int(response.headers["X-NWP-Tokens"])

    if session_budget - used_cgn < min_remaining:
        pause_and_report("approaching budget limit")
```

### Streaming budget

For streaming queries (`stream: true`), `X-NWP-Budget` applies **per batch**, not to the total stream. The node trims or stops the current batch if it would exceed budget. Check `X-NWP-Tokens` after each StreamFrame to track cumulative consumption:

```
total_cgn = 0
for each stream_frame:
    total_cgn += parse_int(stream_frame.headers["X-NWP-Tokens"])
    if total_cgn > session_limit:
        send(cancel_frame)
        break
```

For `topology.stream` and other long-running push subscriptions, budget enforcement is **agent-side only** — the node does not apply `X-NWP-Budget` to push events. You are responsible for tracking cumulative CGN and unsubscribing when the limit is reached.

### cgn_est in ActionSpec

The `cgn_est` field in an ActionSpec is an **estimate**, not a hard limit. It tells you approximately how many CGN a typical call to that action will consume. Use it for pre-flight planning, not for enforcement:

```
action = nwm.actions["analysis.run"]
if action.cgn_est > remaining_budget:
    log_warn("Insufficient budget estimate for this action")
    // ... decide whether to proceed
```

If the actual response exceeds `X-NWP-Budget`, the node will either trim the response or return `NWP-BUDGET-EXCEEDED`. In neither case will you receive silently truncated structured data.

---

## Version pinning

### Always pin to the suite version

NPS is a protocol suite; all components release together under a single suite version (`1.0.0-alpha.19`). Pin to this suite version, not to per-package/per-SDK versions.

**Correct:**
```
# requirements.txt (Python)
# PyPI normalises the pre-release segment, so the suite tag alpha.19 is written 1.0.0a19 here
nps-lib==1.0.0a19
```

```xml
<!-- .csproj (.NET) -->
<PackageReference Include="LabAcacia.NPS.Core" Version="1.0.0-alpha.19" />
```

**Incorrect:** pinning each NPS package to a different version (e.g., `LabAcacia.NPS.Core` at alpha.5 while `LabAcacia.NPS.NWP` is at alpha.4) creates cross-package incompatibilities that are hard to diagnose.

> **No alpha sub-versions since alpha.6.** Releases now advance `alpha.N → alpha.N+1` (e.g. the current `1.0.0-alpha.19`); there is no `alpha.5.x`-style hotfix sequence going forward. The shims below cover field renames that landed during the alpha.5.x line and are needed only when interoperating with old (pre-alpha.6) peers.

### Legacy field-name shims (pre-alpha.6 peers)

The `estimated_npt` → `cgn_est` rename landed in alpha.5.2. When talking to peers that predate it, write client code that checks both fields with a fallback:

```
// Pseudo-code — tolerates pre-alpha.5.2 servers that still send estimated_npt
function read_cgn_estimate(action_spec):
    return action_spec.cgn_est ?? action_spec.estimated_npt ?? null
```

Once all peers are on alpha.6+, drop the `estimated_npt` fallback. The current field name is `cgn_est`.

Similarly, `node_kind` was renamed to `node_roles` in NDP/NIP. `node_kind` was an accepted alias **through alpha.5 only**; from alpha.6 clients MUST send `node_roles` (`topology.filter.node_roles`). When reading from a pre-alpha.6 peer:

```
// Read node roles from NDP AnnounceFrame / NIP IdentFrame; accept the legacy alias
function read_node_roles(frame):
    return frame.node_roles ?? frame.node_kind ?? []
```

These shims are transitional. Remove them after confirming all peers are on alpha.6+.

---

## Encoding tier

NPS supports three encoding tiers:

| Tier | Identifier | Wire format | Use case |
|------|-----------|-------------|----------|
| Tier-1 | `json` | Plain JSON | Development, debugging, interop testing |
| Tier-2 | `msgpack` | MessagePack binary | Production — ~60% smaller payloads |
| Tier-3 | `binary_vector.v1` | MessagePack metadata + raw `float32` segments | Vector-heavy frames (`QueryFrame.vector_search.vector`); negotiated, opt-in (NCP v0.9) |

### Always use MsgPack in production

The `~60%` size reduction comes from MsgPack's binary encoding of field names and values. For high-frequency Agent loops, this meaningfully reduces both bandwidth and latency.

Set the encoding in the NWM `preferred_format` field:

```json
"preferred_format": "msgpack",
"wire_formats": ["ncp-capsule", "msgpack", "json"]
```

In HTTP mode, the client announces encoding preference via:

```
X-NWP-Encoding: msgpack
```

If a Node does not support MsgPack (it has `msgpack` absent from `wire_formats`), fall back to JSON automatically. Never hard-fail on encoding negotiation.

### For debugging and interop testing

Switch to JSON to read raw wire data without a MsgPack parser:

```
X-NWP-Encoding: json
```

JSON is also the safe fallback for exploratory calls to third-party nodes whose encoding support you have not yet verified.

### Tier-3 BinaryVector for vector search (NCP v0.9)

Tier-3 BinaryVector (`binary_vector.v1`) is an opt-in encoding for vector-heavy frames — its only standard binding is `QueryFrame.vector_search.vector`. It carries dense embeddings as raw little-endian `float32` segments after a MessagePack metadata block, which is far more compact than encoding each float as a structured Tier-1/Tier-2 value.

It is **negotiated, never assumed**. Only emit Tier-3 when both peers advertised `binary_vector.v1` in their capabilities; a receiver that did not negotiate it rejects the frame with `NCP-ENCODING-UNSUPPORTED`.

```
# Advertise binary_vector.v1 in caps, then only switch the vector-search
# QueryFrame to Tier-3 when the peer also advertised it.
if "binary_vector.v1" in peer_caps and "binary_vector.v1" in my_caps:
    wire = codec.encode(query_frame, tier=BINARY_VECTOR)   # 0b10
else:
    wire = codec.encode(query_frame)                       # Tier-2 MsgPack
```

Treat malformed Tier-3 payloads as **client** errors, not server faults: the `NCP-BINARY-VECTOR-MALFORMED` / `-DIM-MISMATCH` / `-INDEX-INVALID` / `-DTYPE-UNSUPPORTED` / `-TRUNCATED` codes all map to `NPS-CLIENT-BAD-FRAME` (fix the request — do not retry). The reserved tier `0b11` returns `NCP-FRAME-FLAGS-INVALID`.

---

## Serving local actions to external MCP / A2A clients (inbound Bridge)

The inbound NWP Bridge server lets external MCP / A2A clients invoke your **local** NPS actions (the inverse of the outbound Bridge Node, which translates NPS frames out to non-NPS targets). It is **secure-by-default** — wire every gate before any external request reaches a local action:

- require a valid `X-NWP-Agent` NID plus a configured verifier hook (reject unauthenticated callers);
- expose only an explicit **action allowlist**;
- bound the request body (default 1 MB → HTTP 413 on overflow);
- enforce a dispatch timeout (default 30 s → HTTP 504);
- return **sanitized** client errors — never leak internal exception detail to the external caller.

```
# Pseudo-code — inbound bridge request handling
on inbound_request(req):
    nid = verify_x_nwp_agent(req.headers["X-NWP-Agent"])   # else 401
    if req.action not in ALLOWED_ACTIONS:                   # allowlist
        return sanitized_error("NPS-CLIENT-FORBIDDEN")
    if req.body_len > MAX_REQUEST_BODY_BYTES:               # default 1 MB
        return http(413)
    with timeout(DISPATCH_TIMEOUT_MS):                      # default 30 s → 504
        return dispatch_local_action(req.action, req.params)
```

The exact registration API is language-specific; the .NET reference exposes `AddBridgeServer` / `UseBridgeServer` with `McpServerBridge` / `A2aServerBridge` adapters (see [SDK DotNet](./SDK-DotNet.md)).

---

## See also

- [Reference: Cognon Budget](./Reference-Cognon-Budget.md) — full CGN spec including exchange-rate table and tokenizer resolution chain

---

*Last reviewed at suite version: v1.0.0-alpha.19*
