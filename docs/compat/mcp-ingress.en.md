English | [中文版](./mcp-ingress.md)

# MCP Ingress — Detailed Guide

> **Historical guide.** The standalone ingress repository is archived. Current
> bridge implementation authority is `LabAcacia.NPS.NWP.Bridge` in
> [NPS-SDK-DotNet](https://github.com/labacacia/NPS-SDK-DotNet); use its current
> API and security guidance for new deployments.

> `LabAcacia.McpIngress` v1.0.0-alpha.16 — exposes one or more **NWP nodes** as a
> single [**Model Context Protocol (MCP)**](https://modelcontextprotocol.io) server.
> Target spec: MCP `2024-11-05` (JSON-RPC 2.0 over HTTP POST).

This doc supplements [`compat/mcp-ingress/README.md`](https://github.com/labacacia/NPS-MCP-Ingress/blob/main/README.md)
with details the README doesn't cover: protocol-mapping semantics, header
forwarding, error boundaries, deployment concerns, and real-world integration
examples.

- Source: `compat/mcp-ingress/` (monorepo) / `labacacia/NPS-MCP-Ingress` (standalone)
- Audience: operators and developers wiring existing MCP clients (Claude Desktop,
  IDE agents, the Anthropic SDK) into NPS.

---

## 1. Why bridge at all?

MCP is Anthropic's **tool / resource invocation protocol** with an established
client ecosystem. NPS is a **protocol suite** with self-describing nodes
(`/.nwm` + `/.schema` + `/actions`) and runtime schemas (AnchorFrame). The
semantics align; the wire formats do not:

| Aspect            | MCP 2024-11-05                         | NPS NWP v0.13                                 |
| ----------------- | -------------------------------------- | -------------------------------------------- |
| Transport         | JSON-RPC 2.0 over HTTP POST             | HTTP mode + native frame mode                 |
| Tool declaration  | `tools/list` returns JSON inputSchema   | `/actions` + runtime AnchorFrame              |
| Resource model    | `resources/list` / `resources/read`     | Memory Node `/query`                         |
| Transport auth    | Client-layer concern                    | `X-NWP-Agent` + NIP certificates              |

The bridge's job is to let **unmodified** MCP clients read NWP Memory Nodes and
invoke Action Nodes.

---

## 2. Architecture

```
┌──────────────────┐   JSON-RPC 2.0   ┌────────────────────┐   HTTP/JSON   ┌─────────────────┐
│  MCP Client      │  ───────────→   │  LabAcacia         │  ──────────→ │  NWP Memory /   │
│  (Claude Desktop,│                  │  .McpIngress        │              │  Action Node    │
│   IDE agents, …) │  ←───────────   │  (ASP.NET Core)    │  ←────────── │                 │
└──────────────────┘                  └────────────────────┘               └─────────────────┘
                                        ▲
                                        │ config
                                        │
                                  NwpUpstream[]
```

Each `NwpUpstream` maps to:

- one MCP **resource-URI namespace**: `nwp://<name>/`
- if `node_type == "memory"` → appears in `resources/list`
- if `node_type ∈ {"action","complex"}` → each NWP action becomes an MCP **tool**

`NwpUpstreamClient` wraps one `HttpClient` per upstream, managed by
`IHttpClientFactory` under the name `"mcp-bridge:{name}"`.

---

## 3. Configuration surface

### 3.1 `McpIngressOptions`

| Property            | Default                | Purpose                                                           |
| ------------------- | ---------------------- | ----------------------------------------------------------------- |
| `ServerName`        | `LabAcacia.McpIngress`  | `serverInfo.name` returned by `initialize`                        |
| `ServerVersion`     | `1.0.0-alpha.16`        | `serverInfo.version` returned by `initialize`                     |
| `Upstreams`         | *(required, ≥1)*       | Upstream NWP nodes                                                |
| `ResourceReadLimit` | `100`                  | `limit` passed to Memory Node `/query` on `resources/read`        |

### 3.2 `NwpUpstream`

| Property     | Required | Purpose                                                                  |
| ------------ | -------- | ------------------------------------------------------------------------ |
| `Name`       | yes      | Unique; becomes the `nwp://<name>/` host segment and tool-name prefix    |
| `BaseUrl`    | yes      | NWP node root (no trailing slash); hosts `/.nwm` `/actions` `/query` `/invoke` |
| `AgentNid`   | no       | Sent to upstream as `X-NWP-Agent`                                         |
| `AuthHeader` | no       | Sent verbatim as `Authorization`                                          |

**Name uniqueness guard**: `AddMcpIngress` rejects duplicates at startup with
`InvalidOperationException`. An empty upstream list is also rejected — a
bridge without upstreams is a no-op.

### 3.3 DI + routing

```csharp
builder.Services.AddMcpIngress(o => { /* options */ });
app.MapMcpIngress("/mcp"); // default path is /mcp
```

`MapMcpIngress` returns `IEndpointConventionBuilder` so you can chain:

```csharp
app.MapMcpIngress("/mcp")
   .RequireAuthorization("mcp-clients")
   .WithMetadata(new CorsPolicyMetadata(/*...*/));
```

---

## 4. Protocol mapping (method-by-method)

### 4.1 `initialize`

**Request**

```json
{ "jsonrpc":"2.0", "id":1, "method":"initialize",
  "params":{ "protocolVersion":"2024-11-05", "capabilities":{}, "clientInfo":{...} } }
```

**Response** (`McpInitializeResult`)

```json
{ "jsonrpc":"2.0", "id":1,
  "result":{
    "server_info": { "name":"...", "version":"..." },
    "capabilities": { "resources":{}, "tools":{} }
  }
}
```

The bridge does **no upstream I/O** here. `capabilities` is fixed to
`resources` + `tools`; `prompts`, `sampling`, and `roots` are **not** exposed
in v0.1.

### 4.2 `resources/list`

**Each call** fans out to every configured upstream: `GET /.nwm` →
filter `node_type == "memory"`.

**Response**: one resource entry per matching upstream.

```json
{ "resources":[
    { "uri":"nwp://products/",
      "name":"<display_name or 'products'>",
      "description":"NWP Memory Node 'products' — use resources/read to query.",
      "mime_type":"application/json" }
] }
```

**Performance note**: with many upstreams (≥20), every list call fires N
serial `/.nwm` requests. In production:

- Add a short-TTL HTTP response cache (e.g. 60 s) at the `HttpClient` layer
- Or wrap `NwpUpstreamClient` to memoize `/.nwm`

The bridge **deliberately** doesn't bundle NWM caching — it would collide with
whatever AnchorFrame-TTL policy the node operator chose.

### 4.3 `resources/read`

**Params**

```json
{ "uri":"nwp://products/" }
```

URI must be `nwp://`; the host segment identifies the `Upstream.Name`.

**Mapping**: `POST <BaseUrl>/query` with

```json
{ "limit": <ResourceReadLimit, default 100> }
```

Content-Type is `application/nwp-frame` per NWP convention.

**Response**: the upstream JSON body is returned verbatim as **one text content
block**:

```json
{ "contents":[ {
    "uri":"nwp://products/",
    "mime_type":"application/json",
    "text":"<upstream body verbatim>"
} ] }
```

The MCP client receives a **string**, not a structured object — this matches
MCP's content contract. The client re-parses.

### 4.4 `tools/list`

For every upstream with `node_type ∈ {"action","complex"}`:

1. `GET /.nwm` to confirm node_type
2. `GET /actions` to enumerate actions

Each action produces one tool entry:

| Field         | Value                                                            |
| ------------- | ---------------------------------------------------------------- |
| `name`        | `<upstream>__<action_id_dot_to_underscore>`                      |
| `description` | Action's `description` if present, otherwise `null`              |
| `inputSchema` | `{ "type":"object", "additionalProperties": true }`             |

**Why not surface AnchorFrame as inputSchema?**
AnchorFrame is NWP's **runtime** schema with fields (`token_budget`,
`nid_bindings`, MsgPack tier hints) the MCP client can't consume. v0.1 lets the
MCP client send freeform args and **relies on the NWP node for validation**
(returning an ErrorFrame on invalid input) — consistent semantics, no lossy
schema translation. v0.2 plans JSON-Schema emission over a conservative subset.

### 4.5 `tools/call`

**Request**

```json
{ "name":"orders__orders_create", "arguments": { "sku":"ABC-123", "qty":1 } }
```

**Parse**: split at the **first** `__`. Left side is the upstream name; right
side has `_` rewritten back to `.` to recover `action_id`.

**Mapping**: `POST <BaseUrl>/invoke` with

```json
{ "action_id":"orders.create", "params": { "sku":"ABC-123", "qty":1 } }
```

**Response** is forwarded verbatim with `isError = !resp.IsSuccessStatusCode`:

```json
{ "content":[ { "type":"text", "text":"<raw upstream body>" } ],
  "is_error": false }
```

**Important**: `is_error` flips only on **HTTP non-2xx**. A 2xx CapsFrame with
`status != "completed"` still leaves `is_error=false` — the client is
responsible for looking at the JSON.

### 4.6 `ping`

No upstream I/O. Returns `result: {}`. Keepalive only.

---

## 5. Header forwarding

Every upstream request carries:

| Header          | Source                                  | Optional |
| --------------- | --------------------------------------- | -------- |
| `X-NWP-Agent`   | `NwpUpstream.AgentNid`                  | yes      |
| `Authorization` | `NwpUpstream.AuthHeader` (verbatim)     | yes      |
| `Content-Type`  | `application/nwp-frame` (POST only)     | fixed    |

The bridge does **not** forward a caller-supplied `X-NWP-Agent` — from the
upstream's point of view, the bridge **is** the agent. If your Action Node
needs the MCP caller's real NID, have the client pass an `actor_nid` field
inside `arguments` and let your Action Node handler read it.

---

## 6. Error mapping

### 6.1 JSON-RPC standard errors

| Code     | Trigger                                                     |
| -------- | ----------------------------------------------------------- |
| `-32700` | HTTP body is not valid JSON                                  |
| `-32600` | Valid JSON but not a JSON-RPC 2.0 request (missing `method`) |
| `-32601` | Method name is not one of the six the bridge implements     |
| `-32602` | Param validation (e.g. `resources/read` missing `uri`)      |
| `-32603` | Any unclassified exception (logged via `ILogger.LogError`)  |

### 6.2 Bridge-specific errors

| Code     | Meaning                                                                    |
| -------- | -------------------------------------------------------------------------- |
| `-32000` | Upstream returned non-2xx (used for `resources/read`; see below for tools) |
| `-32001` | `resources/read` URI host does not match any configured upstream           |
| `-32002` | `tools/call` upstream prefix does not match any configured upstream        |

### 6.3 `tools/call` vs other RPCs

- `resources/read` upstream failure → **throws** `-32000` JSON-RPC error
- `tools/call` upstream failure → **returns** result with `is_error=true`,
  **no throw**

Why the split? MCP's tool contract expects tool failures to surface through
`is_error=true` so the model can observe and self-correct. Resource-read
failures are **infrastructure** errors the model can't fix, so they're raised
as explicit RPC errors for the client layer to handle.

### 6.4 Notifications (no `id`)

Per JSON-RPC 2.0, notifications receive HTTP `204 No Content` with no body.
An unknown method does **not** produce an error either (spec-required).

---

## 7. Async task handling

MCP `2024-11-05` tool calls are **synchronous** — clients expect a result in
one POST. But NWP Action Nodes may expose `async=true` actions that return
`202 Accepted + task_id`.

**v0.1 behavior**: the bridge forwards the 202 body verbatim; the client sees
`is_error=false` with the task ack as text content. The client is **responsible
for polling** — treat `system.task.status` as another tool.

Sample flow:

1. `tools/call` name=`orders__orders_bulk_import` → receive task_id
2. Client loops `tools/call` name=`orders__system_task_status` with `{"task_id":"..."}`
3. On completion, read the result (typically via Memory Node `resources/read`)

**Unhappy with that?** v0.2 plans MCP **progress notifications** (added in the
2025 spec), letting the bridge push async progress back to MCP clients. But
2024-11-05 clients can't consume progress, so compatibility is the blocker.

---

## 8. Integration examples

### 8.1 Claude Desktop

`~/Library/Application Support/Claude/claude_desktop_config.json` (macOS):

```json
{
  "mcpServers": {
    "nps-ingress": {
      "command": "dotnet",
      "args": ["run","--project","/path/to/your/bridge/host.csproj"]
    }
  }
}
```

Or using HTTP server transport (Claude Desktop 0.8+):

```json
{
  "mcpServers": {
    "nps-ingress": {
      "url": "https://bridge.example.com/mcp"
    }
  }
}
```

### 8.2 Anthropic SDK (Python)

```python
from anthropic import Anthropic
from anthropic.mcp import StreamableHttpClient

client = Anthropic()
async with StreamableHttpClient("https://bridge.example.com/mcp") as mcp:
    tools = await mcp.list_tools()
    # tools contains products__*, orders__*, ...
    result = await mcp.call_tool("orders__orders_create",
                                  arguments={"sku":"ABC-123","qty":1})
    print(result.content[0].text)
```

### 8.3 Raw JSON-RPC client

```bash
curl -X POST https://bridge.example.com/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

---

## 9. Deployment

### 9.1 TLS & auth

The bridge does **not** terminate TLS or authenticate callers. Recommended:

- Front it with NGINX / Envoy / Azure Front Door for TLS termination + mTLS
  or API-key auth
- Or configure Kestrel HTTPS + `RequireAuthorization("mcp-clients")`

Upstream credentials are static (`NwpUpstream.AuthHeader`) — no per-request
override. For dynamic tokens, wrap a `DelegatingHandler` around the inner
`HttpClient` for token refresh.

### 9.2 Scaling

The bridge is **stateless** (apart from short-lived HTTP connection pools),
so horizontal scaling is trivial. `resources/list` fans out to each upstream
on every call — `replicas × upstreams` amplifies read pressure; let the
upstream operator cache `/.nwm` at their edge.

### 9.3 Observability

The bridge logs via `ILogger<McpIngress>`:

- `LogError` on unhandled exceptions (method name + stack)
- No info-level logging — ASP.NET Core request pipeline already covers that

For metrics, hook a `DelegatingHandler` into `IHttpClientFactory` and meter
upstream latency + status-code distribution via the Meter API.

### 9.4 Resource budget

Each JSON-RPC call is one POST; request bodies are usually <1 KiB, responses
sized by `ResourceReadLimit` (default 100 rows — roughly 10 KiB–few MiB
depending on the domain). The default `HttpClient` timeout is 100 s — set it
explicitly (e.g. 30 s) in production, and pair with an `NwpUpstream.BaseUrl`
timeout policy.

---

## 10. Testing

```bash
dotnet test compat/mcp-ingress/tests/LabAcacia.McpIngress.Tests/
```

Tests run against stub `HttpMessageHandler`s — no network.

Covered:
- initialize
- resources/list: memory / non-memory / mixed
- resources/read: URI validation / upstream failure / happy path
- tools/list: action + complex nodes
- tools/call: sync 2xx / sync 4xx (is_error=true) / malformed name
- Notifications (no id) → 204

---

## 11. Limitations (v0.1)

| Area             | Status             | Notes                                                   |
| ---------------- | ------------------ | ------------------------------------------------------- |
| Streaming        | Not supported      | MCP 2025 progress notifications planned for v0.2        |
| Prompts          | Not supported      | Bridge doesn't translate NWP into MCP prompts           |
| Sampling         | Not planned        | NPS doesn't expose model sampling                       |
| Resource sub.    | Not supported      | `resources/subscribe`                                   |
| Anchor Node      | Not supported      | `node_type: "anchor"` does not appear in `tools/list`   |
| Multi-tenancy    | Not supported      | All MCP clients share one upstreams config              |
| NWM cache        | Not bundled        | Defer to upstream HTTP cache policy                     |
| Transport crypto | Pass-through only  | NIP certs / NCP handshakes are the upstream's concern    |

---

## 12. FAQ

**Q: Can `__` in the tool name collide with an action_id containing `_`?**
A: Split on the **first** `__`. action_id internals may contain `_` (the NWP
spec allows `[a-z0-9._-]` in action ids); the bridge unconditionally rewrites
`.` → `_` in one direction. As long as your upstream names don't contain `__`
you're fine. Use short kebab-case upstream names.

**Q: Can one MCP client talk to multiple bridges?**
A: Yes. Claude Desktop's `mcpServers` is a map — one entry per bridge. Tool
name prefixes keep them non-overlapping.

**Q: What if upstream returns 200 with an ErrorFrame body?**
A: `tools/call` reports `is_error=false` (because HTTP is 200) but the text
content is the ErrorFrame JSON. The client parses `{"frame_type":"error",...}`.
v0.2 will inspect `frame_type` and translate.

**Q: How do I pass the real MCP caller's NID to the upstream?**
A: You can't today. The bridge talks to upstreams as itself. If your Action
Node needs the original caller's identity, have clients declare `actor_nid` in
`arguments` and read it server-side.

---

## 13. References

- NWP spec: [`spec/NPS-2-NWP.md`](../../spec/NPS-2-NWP.md) v0.5
- MCP spec: https://spec.modelcontextprotocol.io/2024-11-05/
- CHANGELOG: [`compat/mcp-ingress/CHANGELOG.md`](https://github.com/labacacia/NPS-MCP-Ingress/blob/main/CHANGELOG.md)
- Standalone repo: [labacacia/NPS-MCP-Ingress](https://github.com/labacacia/NPS-MCP-Ingress)
