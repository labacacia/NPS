English | [中文版](./a2a-ingress.md)

# A2A Ingress — Detailed Guide

> **Historical guide.** The standalone ingress repository is archived. Current
> bridge implementation authority is `LabAcacia.NPS.NWP.Bridge` in
> [NPS-SDK-DotNet](https://github.com/labacacia/NPS-SDK-DotNet); use its current
> API and security guidance for new deployments.

> `LabAcacia.A2aIngress` v1.0.0-alpha.16 — exposes a **single NWP Action / Complex /
> Anchor Node** as a [**Google Agent-to-Agent (A2A) v0.2**](https://github.com/google/A2A)-compliant
> agent. JSON-RPC 2.0 over HTTP POST, plus `GET /.well-known/agent.json`.

Supplements [`compat/a2a-ingress/README.md`](https://github.com/labacacia/NPS-A2A-Ingress/blob/main/README.md)
with AgentCard composition rules, skill-selection algorithm, async task
lifecycle mapping, multi-replica task-state storage, and A2A ↔ NWP state
semantics.

- Source: `compat/a2a-ingress/` (monorepo) / `labacacia/NPS-A2A-Ingress` (standalone)
- Audience: operators wiring existing A2A-ecosystem consumers (Google ADK,
  A2A-speaking peer agents) into NPS nodes.

---

## 1. Why bridge at all?

A2A assumes **"one agent, one AgentCard"** — an agent exposes capabilities as
a skill list. NPS NWP nodes are already that shape: one Action Node has
several actions, each with declarative metadata. The mapping is:

| A2A concept         | NPS mapping                                                         |
| ------------------- | ------------------------------------------------------------------- |
| AgentCard           | `/.nwm` + `/actions` merged                                         |
| Skill               | One NWP `action_id`                                                 |
| Task                | One `/invoke` call (sync or async)                                  |
| Task state machine  | NWP `system.task.status` (`pending/running/completed/failed/cancelled`) |
| Task artifact       | CapsFrame `data` body or async `result`                             |

**Key difference from the MCP ingress**: one AgentCard binds to **one upstream**
(1:1); MCP servers can expose **multiple upstreams** (1:N). That's not a
choice — A2A AgentCard represents an agent identity, so stuffing multiple
NWP nodes into one card breaks identity semantics.

---

## 2. Architecture

```
┌──────────────────┐  GET /.well-known/agent.json  ┌────────────────────┐  GET /.nwm + /actions  ┌──────────────────┐
│  A2A peer        │ ────────────────────────────→ │  LabAcacia         │ ────────────────────→  │  NWP Action /    │
│  (Google ADK,    │                                │  .A2aIngress        │                         │  Complex /       │
│   other agents)  │  JSON-RPC 2.0 POST /a2a        │  (ASP.NET Core)    │  POST /invoke            │  Anchor Node     │
│                  │ ←────────────────────────────  │                    │ ←────────────────────── │                  │
└──────────────────┘                                └────────────────────┘                         └──────────────────┘
                                                      │
                                                      │ in-memory:
                                                      │   Dict<a2a_task_id, UpstreamTaskBinding>
                                                      ▼
```

- Single upstream, single AgentCard
- In-memory `{a2a_task_id → (action_id, upstream_task_id)}` for `tasks/get` / `tasks/cancel`
- State transitions happen **on read** (lazy), not pushed

---

## 3. Configuration surface

### 3.1 `A2aIngressOptions`

| Property                | Default                                        | Purpose                                                            |
| ----------------------- | ---------------------------------------------- | ------------------------------------------------------------------ |
| `AgentName`             | `NPS A2A Ingress`                               | AgentCard `name`                                                   |
| `AgentDescription`      | `null` (falls back to upstream `display_name`) | AgentCard `description`                                            |
| `AgentVersion`          | `0.1.0`                                        | AgentCard `version` (**agent version**, not bridge package version) |
| `PublicUrl`             | `null` (auto-derived from request)             | AgentCard `url` — the JSON-RPC endpoint clients dispatch to         |
| `ProviderOrganization`  | `LabAcacia / INNO LOTUS PTY LTD`               | AgentCard `provider.organization`                                   |
| `ProviderUrl`           | `https://github.com/labacacia/nps`             | AgentCard `provider.url`                                            |
| `DocumentationUrl`      | `null`                                         | AgentCard `documentationUrl`                                        |
| `AuthSchemes`           | `[]`                                           | AgentCard `authentication.schemes`                                  |
| `Upstream`              | *(required)*                                   | The single upstream NWP node                                        |

### 3.2 `A2aUpstream`

| Property     | Required | Purpose                                 |
| ------------ | -------- | --------------------------------------- |
| `BaseUrl`    | yes      | Upstream root URL                       |
| `AgentNid`   | no       | `X-NWP-Agent` header                    |
| `AuthHeader` | no       | `Authorization` header (verbatim)       |

### 3.3 DI + routing

```csharp
builder.Services.AddRouting();
builder.Services.AddA2aIngress(o => { /* options */ });

app.UseRouting();
app.UseEndpoints(e => e.MapA2aIngress());
// Registers two endpoints:
//   GET  /.well-known/agent.json     → AgentCard
//   POST /a2a                        → JSON-RPC
```

`MapA2aIngress()` accepts a custom JSON-RPC path:

```csharp
app.MapA2aIngress(jsonRpcPath: "/rpc/a2a");
```

But `/.well-known/agent.json` is **spec-mandated** and cannot be changed.

---

## 4. AgentCard composition

### 4.1 Data sources

Every `GET /.well-known/agent.json`:

1. `GET <upstream>/.nwm` — reads `display_name` (used as description fallback)
2. `GET <upstream>/actions` — enumerates actions, one skill each

**Not cached** — the upstream is hit live on every request. A2A clients expect
AgentCards to be relatively stable, but live lookup guarantees NWP schema
updates surface immediately. Add a 60 s cache-control at the edge in production.

### 4.2 Composition logic

```
A2aAgentCard:
  name              = options.AgentName
  description       = options.AgentDescription ?? nwm.display_name
  url               = options.PublicUrl ?? <derived from incoming request>
  version           = options.AgentVersion
  provider          = { organization: ProviderOrganization, url: ProviderUrl }
  documentationUrl  = options.DocumentationUrl
  capabilities      = { streaming: false, pushNotifications: false,
                        stateTransitionHistory: false }
  authentication    = options.AuthSchemes.Count > 0 ? { schemes } : null
  skills[]          = actions.map(a => {
                        id:          a.id,
                        name:        a.id,
                        description: a.description,
                        tags:        a.async === true ? ["async"] : null
                      })
```

### 4.3 Why PublicUrl matters

`AgentCard.url` is the address the A2A client will POST `tasks/send` to. Two
modes:

- **Explicit PublicUrl**: `o.PublicUrl = new Uri("https://public.example/a2a")`
- **Implicit**: bridge assembles `Scheme + Host + PathBase + /a2a` from the
  incoming request

You **must** set PublicUrl explicitly in production — behind a reverse proxy,
the incoming request typically has an internal hostname.

---

## 5. Skill selection (the heart of `tasks/send`)

An A2A task has no "target skill" field — A2A assumes the agent figures out
intent itself. The bridge must pick a NWP action **deterministically**.
Lookup order:

1. `params.metadata.skillId` (+ optional `params.metadata.params`)
2. `message.metadata.skillId` (+ optional `message.metadata.params`)
3. First `type:"data"` part in `message.parts`, keyed under `skillId` /
   `skill_id` / `action_id`

Both casing variants are accepted — the A2A ecosystem uses both camelCase
and snake_case.

If none of the three yield a skill id → JSON-RPC `-32602` with a guidance
message.

### 5.1 Argument source

| Source                         | Args from                                                    |
| ------------------------------ | ------------------------------------------------------------ |
| metadata-carried skillId       | `metadata.params` (empty object if absent)                   |
| data-part-carried skillId      | Prefer `data.params`; otherwise the data part minus skill-id key |

### 5.2 Examples

**Option A — metadata explicit**

```json
{ "jsonrpc":"2.0","id":1,"method":"tasks/send","params":{
    "id":"task-001",
    "message":{ "role":"user", "parts":[ {"type":"text","text":"Create order"} ] },
    "metadata":{ "skillId":"orders.create", "params":{"sku":"ABC","qty":1} }
} }
```

**Option B — data part**

```json
{ "jsonrpc":"2.0","id":1,"method":"tasks/send","params":{
    "id":"task-002",
    "message":{ "role":"user", "parts":[
      { "type":"data", "data":{ "skillId":"orders.create", "sku":"ABC", "qty":1 } }
    ] }
} }
```

Both are valid; **Option A is preferred** — explicit intent, easier auditing
and quota tracking.

---

## 6. Task lifecycle mapping

### 6.1 Sync (upstream 200)

- NWP `/invoke` returns a CapsFrame
- Bridge returns `A2aTask{ status: completed, artifacts: [ {name: action_id, parts: [{type: data, data: <body>}]} ] }`

### 6.2 Async (upstream 202)

- NWP returns `{ task_id, status: "pending", poll_url? }`
- Bridge records `{ a2a_task_id, upstream_task_id }` in the in-memory map
- Bridge returns `A2aTask{ status: submitted }` (no artifacts)

### 6.3 Polling (`tasks/get`)

- Look up the in-memory map; miss → `-32001 TaskNotFound`
- Hit → `POST /invoke { action_id: "system.task.status", params: {task_id: <upstream_id>} }`
- Upstream returns a CapsFrame with payload `{status, result?}`

### 6.4 State translation

| NWP (`system.task.status`)        | A2A `status.state`          | Notes                                          |
| --------------------------------- | --------------------------- | ---------------------------------------------- |
| `pending`                         | `submitted`                 | Bridge just recorded it                        |
| `running`                         | `working`                   | Execution underway                             |
| `completed`                       | `completed`                 | Artifact returned                              |
| `failed`                          | `failed`                    |                                                |
| `cancelled` / `canceled`          | `canceled`                  | NWP uses British spelling, A2A American; both accepted |
| other / missing                   | `unknown`                   | Doesn't throw — lets the client decide         |

### 6.5 Cancel (`tasks/cancel`)

- Look up the map; miss → `-32001`
- Hit → `POST /invoke { action_id: "system.task.cancel", params: {task_id} }`
- Upstream 2xx → return `A2aTask{ status: canceled }`
- Upstream non-2xx → `-32002 TaskNotCancelable` JSON-RPC error

### 6.6 Task-map persistence

**v0.1 uses an in-memory `ConcurrentDictionary`**. Consequences:

- Restart loses async task bindings → `tasks/get` reports `-32001` for ids
  from before the restart
- In multi-replica deployments a task pins to one replica → need sticky
  sessions or shared storage

**v0.2 plan**: an `ITaskBindingStore` abstraction with an in-memory default
and a Redis adapter. Workarounds for now: pin replicas via LB header-affinity
on `a2a_task_id`, or have the upstream maintain its own task→node index.

---

## 7. Error mapping

### 7.1 Standard JSON-RPC errors

| Code     | Trigger                                           |
| -------- | ------------------------------------------------- |
| `-32700` | Body is not JSON                                   |
| `-32600` | Not a valid JSON-RPC 2.0 request                   |
| `-32601` | Unknown method                                     |
| `-32602` | Missing / invalid params (including skill lookup failure) |
| `-32603` | Any other exception                                |

### 7.2 A2A application errors

| Code     | Meaning                                                   |
| -------- | --------------------------------------------------------- |
| `-32001` | `TaskNotFound` — the bridge has no record of that task    |
| `-32002` | `TaskNotCancelable` — upstream refused to cancel          |
| `-32004` | `UnsupportedOperation` — e.g. `tasks/sendSubscribe`       |
| `-32010` | `UpstreamError` — upstream status poll failed             |

### 7.3 **Key design**: upstream business failures are **not** JSON-RPC errors

When `tasks/send` gets a 4xx/5xx from the upstream, it **does not** raise a
JSON-RPC error; it returns:

```json
{ "jsonrpc":"2.0","id":1,"result":{
    "id":"task-001",
    "status":{ "state":"failed",
               "message":{ "role":"agent", "parts":[ {"type":"text","text":"Upstream NWP returned HTTP 422: ..."} ] } }
} }
```

A2A clients can handle failures **uniformly** through task state — no
exception path. JSON-RPC errors are reserved for real **protocol-level**
faults (bad params, bad method, bridge bugs).

### 7.4 Notifications

No `id` → HTTP `204 No Content`. Unknown method returns no error either
(per spec).

---

## 8. Integration examples

### 8.1 Google ADK (Python)

```python
from google_adk.a2a import A2aClient

client = A2aClient("https://bridge.example.com")
card = await client.fetch_agent_card()
print("Skills:", [s.id for s in card.skills])

task = await client.send_task(
    task_id="task-001",
    message={"role":"user","parts":[{"type":"text","text":"Create order"}]},
    metadata={"skillId":"orders.create","params":{"sku":"ABC","qty":1}}
)

if task.status.state == "submitted":
    while True:
        task = await client.get_task(task.id)
        if task.status.state in ("completed","failed","canceled"):
            break
        await asyncio.sleep(2)

print("Result artifact:", task.artifacts[0].parts[0].data)
```

### 8.2 Hand-rolled JSON-RPC client

```bash
# Fetch AgentCard
curl https://bridge.example.com/.well-known/agent.json

# Send task
curl -X POST https://bridge.example.com/a2a \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc":"2.0","id":1,"method":"tasks/send",
    "params":{
      "id":"task-001",
      "message":{"role":"user","parts":[{"type":"data","data":{
        "skillId":"orders.create","sku":"ABC","qty":1
      }}]}
    }
  }'
```

### 8.3 Outbound A2A (reverse direction)

This bridge is **passive-only** (server role). To initiate tasks from an NPS
agent toward other A2A agents, either use the NWP `http_upstream` gateway
pattern or call Google ADK's client SDK directly.

---

## 9. Deployment

### 9.1 PublicUrl & reverse proxies

Production essential:

```csharp
o.PublicUrl = new Uri("https://bridge.public.example.com/a2a");
```

Behind NGINX / Envoy, ensure `X-Forwarded-Proto` / `X-Forwarded-Host` are
properly consumed (`app.UseForwardedHeaders()`) — otherwise the implicit
fallback derives the wrong URL.

### 9.2 Auth

The bridge does **no** client authentication. The A2A spec's `authentication`
field is **advertisement only** — "I need bearer tokens" is something the
AgentCard says, not something the bridge enforces. Do real auth in front of
it: NGINX + OAuth2 proxy, Kestrel + JWT bearer, or an API gateway.

Upstream credentials (`A2aUpstream.AuthHeader`) are **static**.

### 9.3 Multi-replica

See §6.6 on task-map persistence. If you don't need async tasks, scale freely.
If you do, stay single-replica for now or use sticky sessions.

### 9.4 Observability

- `ILogger<A2aIngress>` logs dispatch failures and upstream status-poll errors
- Suggestion: `OpenTelemetry.Instrumentation.Http` for upstream `/invoke`
  latency / status-code / volume metrics
- Task-count metrics aren't exposed directly yet (v0.2 TODO). For now,
  subclass `A2aIngress` and wrap `DispatchAsync` if you need counters.

### 9.5 Timeouts

`tasks/send` timeouts are the A2A client's concern; the bridge's `HttpClient`
default is 100 s. Best practice: model long-running work as NWP async
(`async=true`) so 202 returns quickly.

---

## 10. Testing

```bash
dotnet test compat/a2a-ingress/tests/LabAcacia.A2aIngress.Tests/
```

Coverage:

- AgentCard composition (incl. fallback description)
- `tasks/send` × three skill sources × sync / async / failure
- `tasks/get` state translation for all 5 states
- `tasks/cancel` success + upstream refusal
- `tasks/sendSubscribe` / push → `-32004`
- `tasks/get` for unknown id → `-32001`
- Notifications (no id) → 204
- URL fallback / PublicUrl override

No network.

---

## 11. Limitations

| Area              | Status          | Notes                                                         |
| ----------------- | --------------- | ------------------------------------------------------------- |
| Server streaming  | Not supported   | `tasks/sendSubscribe` → `-32004`; v0.2 TODO                    |
| Push notifications | Not supported  | Same                                                          |
| State transition history | Not supported | Clients only see current state                            |
| Task-map persistence | In-memory only | Restart / multi-replica restrictions (§6.6)                |
| Multi-upstream    | Never supported | 1:1 by protocol semantics                                     |
| Authentication    | Advertise only  | Real auth sits in front of the bridge                         |

---

## 12. FAQ

**Q: Does the A2A peer see any NWP schema detail?**
A: No AnchorFrame / NID-binding specifics leak. AgentCard exposes only skill
id + description + async tag. Peer agents dispatch by skill id; business
params travel in `metadata.params` or a data part.

**Q: What if the upstream is an Anchor Node (NOP orchestrator entry)?**
A: Fine — Anchor Node's `/actions` is structurally identical to an Action
Node's. As long as the Anchor's actions are `/invoke`-able (sync or async),
the bridge will proxy them.

**Q: Why isn't NWP `anchor_ref` exposed to A2A clients?**
A: A2A's Task/Artifact model has no "schema ref" concept. Stuffing it into
`artifact.metadata` would force peer agents to deal with NPS-specific fields,
breaking the bridge's transparency. If the downstream agent is NPS-aware,
have it speak NWP directly.

**Q: Can one bridge process serve N upstreams?**
A: No — one process, one upstream. Run multiple bridge processes on
different ports / path prefixes. This is intentional: A2A AgentCard is
agent identity, not a multiplexer.

**Q: Who generates task ids?**
A: The A2A **client** does. The bridge doesn't generate or validate the
format — as long as it's recorded in the in-memory map, `tasks/get` can
route back to the upstream. UUIDv4 is a safe default.

---

## 13. References

- NWP spec: [`spec/NPS-2-NWP.md`](../../spec/NPS-2-NWP.md) (Action Node §7,
  async §7.2, `system.task.*` §7.3)
- A2A spec: https://github.com/google/A2A/blob/main/specification/
- CHANGELOG: [`compat/a2a-ingress/CHANGELOG.md`](https://github.com/labacacia/NPS-A2A-Ingress/blob/main/CHANGELOG.md)
- Standalone repo: [labacacia/NPS-A2A-Ingress](https://github.com/labacacia/NPS-A2A-Ingress)
