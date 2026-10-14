# SDK Tutorial: Building a Bridge Node

**Status:** ✅ Reviewed for v1.0.0-alpha.19

> **Audience:** Developers implementing NPS↔non-NPS protocol translation (MCP, A2A, gRPC, HTTP).
> **Source-of-truth precedence:** `spec/` documents win over this page if they disagree.

A **Bridge Node** translates NPS frames into requests on non-NPS protocols and translates the responses back. It was introduced alongside the Anchor Node in v1.0-alpha.3 by [NPS-CR-0001](../../spec/cr/NPS-CR-0001-anchor-bridge-split.md), which split the now-retired "Gateway Node" type into two distinct roles with clearly separate concerns.

---

## Table of contents

1. [What a Bridge Node is (and is not)](#what-a-bridge-node-is-and-is-not)
2. [Direction and the compat/\*-ingress packages](#direction-and-the-compatx-ingress-packages)
3. [Step 1 — Declare the NWM manifest](#step-1--declare-the-nwm-manifest)
4. [Step 2 — Declare in the NDP AnnounceFrame](#step-2--declare-in-the-ndp-announceframe)
5. [Step 3 — Handle inbound ActionFrame with bridge_target](#step-3--handle-inbound-actionframe-with-bridge_target)
6. [Step 4 — Translate errors into the NPS namespace](#step-4--translate-errors-into-the-nps-namespace)
7. [Inbound NWP Bridge server adapters (external → local NPS actions)](#inbound-nwp-bridge-server-adapters-external--local-nps-actions)
8. [Rejecting legacy gateway wire values](#rejecting-legacy-gateway-wire-values)
9. [Reference implementations](#reference-implementations)

---

## What a Bridge Node is (and is not)

| It IS | It is NOT |
|-------|-----------|
| A translator: NPS frames → external protocol requests | A proxy or cache |
| Stateless per request | A state store or session manager |
| The outbound edge of an NPS cluster for non-NPS systems | An Anchor Node (Anchor routes inbound NPS → NPS; Bridge routes NPS → external) |
| Declared with `node_roles: ["bridge"]` | A replacement for the retired Gateway Node |

A Bridge Node does not participate in cluster topology and does not maintain a member registry. It simply accepts an ActionFrame carrying a `bridge_target` parameter, makes an outbound call in the target protocol's format, and returns the result as a CapsFrame.

A single Bridge Node MAY support multiple external protocols simultaneously. Deployments MAY also run dedicated Bridge Nodes per protocol for isolation and independent scaling.

---

## Direction and the compat/\*-ingress packages

Traffic direction is the key distinction in the NPS ecosystem:

```
NPS cluster ──[Bridge Node]──→ external system   (NPS → external)
external system ──[Ingress adapter]──→ NPS        (external → NPS)
```

The `compat/mcp-ingress`, `compat/a2a-ingress`, and `compat/grpc-ingress` packages carry the **inverse** direction: they accept incoming traffic from external systems and translate it into NPS frames. These were originally named `compat/*-bridge` before CR-0001 renamed them to free the "Bridge" word for the outbound role.

When you are building a **Bridge Node** you are implementing the outbound path. If you want to receive MCP/A2A/gRPC calls from the outside world and feed them into an NPS cluster, use the ingress adapters instead.

---

## Step 1 — Declare the NWM manifest

The NWM for a Bridge Node uses `node_type: "bridge"` and lists the supported external protocols in `bridge_protocols`:

```json
{
  "nwp": "0.14",
  "node_id": "urn:nps:node:api.example.com:mcp-bridge",
  "node_type": "bridge",
  "display_name": "Example MCP Bridge",
  "manifest_version": 1,
  "manifest_updated_at": "2026-06-13T00:00:00Z",
  "wire_formats": ["ncp-capsule", "msgpack", "json"],
  "preferred_format": "msgpack",
  "capabilities": {
    "query": false,
    "stream_query": false,
    "subscribe": false,
    "token_budget_hint": true
  },
  "auth": {
    "required": true,
    "identity_type": "nip-cert",
    "trusted_issuers": ["https://ca.example.com"],
    "required_capabilities": ["nwp:action"]
  },
  "actions": {
    "mcp.call": {
      "description": "Forward an NPS action to an MCP server",
      "async": false,
      "idempotent": false,
      "timeout_ms_default": 30000,
      "required_capability": "nwp:action"
    }
  },
  "endpoints": {
    "invoke": "nwp://api.example.com/mcp-bridge/invoke"
  }
}
```

The `bridge_protocols` field is carried in the NDP `AnnounceFrame`, not in the NWM itself (see Step 2). The NWM declares the node role; NDP carries the protocol list.

---

## Step 2 — Declare in the NDP AnnounceFrame

The AnnounceFrame (NDP 0x30) carries the authoritative role declaration for discovery:

```json
{
  "frame": "0x30",
  "nid": "urn:nps:node:api.example.com:mcp-bridge",
  "node_roles": ["bridge"],
  "bridge_protocols": ["mcp", "a2a"],
  "activation_mode": "ephemeral"
}
```

**`bridge_protocols` standard values (NPS-CR-0001 §3.2 / AaaS Profile §2A.3):**

| Value | External protocol |
|-------|------------------|
| `"http"` | HTTP / HTTPS (REST and streaming) |
| `"grpc"` | gRPC (unary and streaming) |
| `"mcp"` | Model Context Protocol |
| `"a2a"` | Agent-to-Agent (Google A2A v0.2) |

Additional protocol values MAY be registered through future CRs. The list is open-ended to allow third-party adapters without requiring a spec change.

**`bridge_target` schema:** As of **NWP v0.13 (CR-0006)** the `bridge_target` object that callers pass inside an ActionFrame is **standardized** with three fields:

| Field | Type | Description |
|-------|------|-------------|
| `protocol` | string | External protocol selector — one of the `bridge_protocols` standard values (`"http"` / `"grpc"` / `"mcp"` / `"a2a"`). |
| `endpoint` | string | Target endpoint URI in the external protocol's address space (e.g. `"https://mcp.example.com/tools/search"`). |
| `headers` | object | Optional map of protocol-level headers/metadata to forward (e.g. HTTP headers, gRPC metadata). |

```json
"bridge_target": {
  "protocol": "mcp",
  "endpoint": "mcp.example.com/tools/search",
  "headers": { "x-tenant": "acme" }
}
```

Earlier releases (prior to NWP v0.13) treated this shape as implementation-defined per CR-0001 §3.2; the standardized schema above is now the canonical form. Any per-protocol extensions beyond these three fields SHOULD still be documented in your node's NWM description or ActionSpec `params_anchor`.

**`node_roles` MUST match `node_type`:** The NWP constraint is that `node_type` in the NWM MUST be one of the values declared in `node_roles`. For a pure Bridge Node: `node_type = "bridge"` and `node_roles` must include `"bridge"`. A multi-role node (e.g., `"bridge"` + `"action"`) must include both in `node_roles`.

---

## Step 3 — Handle inbound ActionFrame with bridge_target

The ActionFrame arriving at a Bridge Node MUST carry a `bridge_target` object (standardized schema, see Step 2) inside `params`. The Bridge Node's translation loop:

```
Caller           Bridge Node                        External System
  │                   │                                   │
  │── ActionFrame ──→ │                                   │
  │   params: {       │                                   │
  │     bridge_target │                                   │
  │     ...payload... │                                   │
  │   }               │                                   │
  │                   │── 1. Extract bridge_target        │
  │                   │── 2. Build external request ────→ │
  │                   │      (HTTP/gRPC/MCP/A2A format)   │
  │                   │   ←── External response ───────── │
  │                   │── 3. Translate response           │
  │ ←── CapsFrame ─── │      to CapsFrame                 │
```

**Implementation sketch (protocol-neutral pseudo-code):**

```
function handle_action_frame(action_frame, nwm):
    // Validate NID and scope as usual
    verify_ident_frame(action_frame.caller_ident)

    // Dispatch based on action_id
    action_spec = nwm.actions[action_frame.action_id]
    bridge_target = action_frame.params["bridge_target"]

    // Resolve the external protocol handler
    handler = get_handler(bridge_target.protocol)  // "mcp", "http", etc.

    // Call the external system
    try:
        external_response = handler.call(bridge_target, action_frame.params)
        return caps_frame(data = external_response, cgn_est = measure_cgn(external_response))
    except ExternalError as e:
        return error_frame(translate_error(e))   // see Step 4
```

**Authentication relay (optional):**

Bridge Nodes MAY forward NIP credentials to the external system where the target protocol has an equivalent concept (e.g., HTTP `Authorization` header mapped from the NID). Where no mapping exists, use vendor-side credentials configured per Bridge instance. Never forward the raw private key.

**Observability:**

Annotate OpenTelemetry spans with `bridge.target_protocol` and `bridge.target_endpoint` so end-to-end traces visually cross the NPS/external boundary.

---

## Step 4 — Translate errors into the NPS namespace

Non-NPS systems return errors in their own formats. A Bridge Node MUST translate every outbound error into a valid NPS ErrorFrame (`0xFE`) before returning it to the caller.

**General translation table:**

| External error class | NPS error code | NPS status |
|---------------------|---------------|------------|
| 400 Bad Request / invalid params | `NWP-ACTION-PARAMS-INVALID` | `NPS-CLIENT-UNPROCESSABLE` |
| 401 / 403 Unauthorized | `NWP-AUTH-NID-CAPABILITY-MISSING` | `NPS-AUTH-FORBIDDEN` |
| 404 Not Found | `NWP-ACTION-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` |
| 429 Too Many Requests | `NWP-RATE-LIMIT-EXCEEDED` | `NPS-LIMIT-RATE` |
| 500 Internal Error | `NPS-SERVER-INTERNAL` | `NPS-SERVER-INTERNAL` |
| 503 / service down | `NWP-NODE-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` |
| Connection timeout | `NPS-SERVER-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` |

**ErrorFrame (`0xFE`) wire shape:**

```json
{
  "frame": "0xFE",
  "status": "NPS-SERVER-UNAVAILABLE",
  "error": "NWP-NODE-UNAVAILABLE",
  "message": "MCP server at mcp.example.com did not respond within 30 s",
  "details": {
    "bridge_protocol": "mcp",
    "bridge_target": "mcp.example.com/tools/search",
    "upstream_status": 503
  },
  "request_id": "550e8400-..."
}
```

Include `bridge_protocol` and a sanitized reference to the external target in `details` — it helps callers diagnose which downstream system failed without leaking internal endpoint secrets.

**Protocol-specific notes:**

- **MCP:** Tool-call errors return in the `isError: true` tool result. Map to `NWP-ACTION-PARAMS-INVALID` (semantic error in the tool call) or `NPS-SERVER-INTERNAL` (unexpected tool-side crash).
- **gRPC status codes:** `UNAVAILABLE` → `NPS-SERVER-UNAVAILABLE`; `UNIMPLEMENTED` → `NPS-SERVER-UNSUPPORTED`; `PERMISSION_DENIED` → `NPS-AUTH-FORBIDDEN`.
- **A2A:** Task failure states map to `NPS-SERVER-INTERNAL`; authentication failures to `NPS-AUTH-UNAUTHENTICATED`.

---

## Inbound NWP Bridge server adapters (external → local NPS actions)

Everything above describes the **outbound** Bridge Node: NPS frames in, an external-protocol call out. As of **alpha.14** the SDK family also ships **inbound NWP Bridge server adapters** that run the *other* direction at the action layer — they let an external **MCP** or **A2A** client invoke **local NPS actions**, with the adapter handling the protocol translation and NPS dispatch in-process.

```
external MCP / A2A client ──[Bridge server adapter]──→ local NPS actions
```

This is distinct from both:

- the **outbound `BridgeNode`** dispatchers (NPS → external, Steps 1–4 above), and
- the **`compat/*-ingress`** adapters (external → NPS, full node front-end). The Bridge server adapters are a lighter-weight, action-level surface you mount into an existing service to expose selected local actions to MCP / A2A callers.

### Adapters and wiring

The reference implementation exposes the adapters as `McpServerBridge` / `A2aServerBridge`, mounted into an ASP.NET Core host via `AddBridgeServer` / `UseBridgeServer`. (Other SDKs expose the equivalent capability; check the source for exact names per language.)

### Secure-by-default

The inbound Bridge server is hardened by default — a misconfigured deployment fails closed rather than open:

| Control | Behavior |
|---------|----------|
| **Caller identity** | Requires a valid `X-NWP-Agent` NID header plus a **configured verifier hook**. With no verifier configured the adapter refuses to dispatch. |
| **Action allowlist** | Only actions you explicitly allowlist are reachable; everything else is rejected. External callers cannot reach un-listed local actions. |
| **Bounded request bodies** | Request bodies above `MaxRequestBodyBytes` (default 1 MB) are rejected with HTTP **413**. |
| **Dispatch timeout** | A local dispatch exceeding `DispatchTimeoutMs` (default 30 s) is aborted and returns HTTP **504**. |
| **Sanitized errors** | Internal failures are translated to sanitized client errors — no internal endpoint, stack, or secret detail leaks to the external caller. |

When mapping the external error back to the caller, follow the same sanitisation principle as the outbound path (Step 4): surface a stable NPS-namespaced error and a sanitized reference, never raw internal detail.

---

## Rejecting legacy gateway wire values

Your implementation MUST reject the retired `"gateway"` role in both places it can appear:

| Incoming wire field | Legacy value | Correct response |
|--------------------|-------------|-----------------|
| NWP NWM `node_type` | `"gateway"` | `NWP-MANIFEST-NODE-TYPE-REMOVED` |
| NDP AnnounceFrame `node_roles` | `["gateway"]` | `NDP-ANNOUNCE-ROLE-REMOVED` |

Both responses SHOULD include a `hint` field referencing NPS-CR-0001 and indicating that callers should migrate to `"anchor"` or `"bridge"` depending on the role they intended.

Do NOT silently accept `"gateway"` or remap it to `"anchor"`. Explicit rejection ensures operators discover the breaking change rather than silently operating in an undefined state.

---

## Reference implementations

Three open-source reference implementations are available:

| Repository | External protocol | Direction |
|-----------|------------------|-----------|
| `labacacia/NPS-MCP-Ingress` | Model Context Protocol | external → NPS (ingress; NOT a Bridge Node but a useful reference for MCP wire format) |
| `labacacia/NPS-A2A-Ingress` | Google Agent-to-Agent | external → NPS (ingress) |
| `labacacia/NPS-gRPC-Ingress` | gRPC | external → NPS (ingress) |

These ingress packages carry the inverse direction from a Bridge Node, but their protocol-translation logic (MCP tool schema mapping, A2A task state machine, gRPC proto-to-NPS frame mapping) is the most complete reference for each protocol's quirks. Study the translation layer and adapt it for the outbound path.

A dedicated outbound (NPS → external) MCP bridge remains a roadmap item. The `McpIngress` / `A2aIngress` / `GrpcIngress` packages above were last published at **v1.0.0-alpha.16** (a prepared alpha.17 deprecation release was never published) and left the suite release train at alpha.18 — their maintained replacement is the inbound surface of `LabAcacia.NPS.NWP.Bridge`. The archived ingress sources remain the most complete reference translation layers for outbound bridge work.

---

## See also

- [Protocol NDP](./Protocol-NDP.md) — AnnounceFrame schema, `node_roles`, `bridge_protocols`
- [SDK Building an Anchor Node](./SDK-Building-an-Anchor-Node.md) — the sibling routing role

---

*Last reviewed at suite version: v1.0.0-alpha.19*
