English | [中文版](./node-types.md)

# Node Types

NPS defines five neural node types, each with a distinct responsibility. A single deployment MAY carry more than one role at the same time (e.g. a Memory Node that is also an Anchor Node); multi-role declaration travels in the NDP `Announce` frame's `node_roles` field. The original `Gateway Node` type was retired in v1.0-alpha.3 and split into Anchor Node + Bridge Node by [NPS-CR-0001](../../spec/cr/NPS-CR-0001-anchor-bridge-split.md).

---

## Memory Node

**Analogy:** Database + Search Engine

**Role:** Data storage and retrieval. AI Agents query Memory Nodes via `QueryFrame`; the node handles filtering, pagination, and sorting, returning results in schema-anchored format.

**Key Features:**
- Pre-declares schema via `AnchorFrame` to avoid repeated transmission
- Supports the NWP Filter DSL (`$eq`, `$gt`, `$in`, `$between`, `$contains`, etc.)
- Cursor-based pagination (`next_cursor`)
- Optional vector similarity search (`VectorSearch` capability)
- Streaming responses (`/stream` endpoint + `StreamFrame`)
- CGN consumption metering (`X-NWP-Tokens` response header)

**Implemented data sources (.NET):** SQL Server, PostgreSQL; custom sources implement `IMemoryNodeProvider`.

**NWM declaration:**

```json
{
  "node_type": "memory",
  "capabilities": { "query": true, "stream": true, "vector_search": false }
}
```

---

## Action Node

**Analogy:** REST API + Function Call

**Role:** Executes operations and service invocations. AI Agents call node-exposed operations via `ActionFrame` (`action_id`); the node runs business logic and returns the result.

**Key Features:**
- Operations are identified as `"{domain}.{verb}"` (e.g. `orders.create`)
- Idempotency key support (`IdempotencyKey`): same key within 24 hours returns the original result
- Asynchronous operations (`Async = true`) return `task_id + poll_url`
- Custom per-request timeout (`TimeoutMs`, max 300 s)

**NWM declaration:**

```json
{
  "node_type": "action",
  "capabilities": { "query": false }
}
```

---

## Complex Node

**Analogy:** BFF (Backend for Frontend) + Graph Database

**Role:** A combined data-and-processing node. Delegates requests to child nodes through the node graph traversal mechanism (`X-NWP-Depth`), enabling multi-source aggregation or multi-step processing.

**Key Features:**
- `X-NWP-Depth` controls max traversal depth (default 1, max 5)
- Declares child node references in `graph.refs` within the NWM
- Can support both Query and Action capabilities simultaneously
- Can inline child AnchorFrames to reduce round-trips

**NWM declaration:**

```json
{
  "node_type": "complex",
  "capabilities": { "query": true },
  "graph": {
    "refs": [
      { "rel": "products", "node": "nwp://api.example.com/products" },
      { "rel": "orders",   "node": "nwp://api.example.com/orders" }
    ],
    "max_depth": 2
  }
}
```

---

## Anchor Node

**Analogy:** Cluster control plane + NOP entry point

**Role:** Cluster control plane and external entry point. Accepts inbound NWP `Action` / `Query` frames addressed to the cluster, dispatches them to member nodes (typically by converting to a NOP `TaskFrame` and delegating to a local NOP orchestrator), aggregates outbound responses into a single response stream, and optionally maintains a registry of member nodes.

**Key Features:**
- Stateless per request; MAY maintain a long-lived member-node registry
- Member nodes register via NDP `Announce` with `cluster_anchor` referencing the Anchor Node's NID
- Anchor Nodes that maintain a registry MUST expose it via reserved query types `topology.snapshot` and `topology.stream` (NPS-CR-0002)
- Key distinction from Complex Node: Complex Nodes own business data; Anchor Nodes only route to NOP and aggregate
- Replaces the cluster-entry / NOP-routing role formerly carried by `Gateway Node`

**NWM declaration:**

```json
{
  "node_type": "anchor",
  "capabilities": { "query": false }
}
```

---

## Bridge Node

**Analogy:** NPS ↔ external protocol translator

**Role:** Bridges NPS frames and non-NPS protocols (HTTP/HTTPS, gRPC, MCP, A2A). Outbound adapters translate NPS `ActionFrame` calls into external protocol calls; inbound server adapters expose local NPS actions as external protocol servers. A single Bridge Node MAY translate to multiple distinct external protocols; deployments MAY operate dedicated Bridge Nodes per protocol for isolation.

**Key Features:**
- Outbound: accepts NWP frames carrying a `bridge_target` parameter identifying the external protocol and endpoint
- Outbound: produces requests in the target protocol's format and translates responses back into NWP frames
- Inbound: exposes local NPS actions as MCP tools and A2A skills, translating external client calls into `ActionFrame` dispatch
- Direction note: `bridge_protocols` declare **NPS → external** capability; `bridge_inbound_protocols` declare **external → NPS** capability through `McpInboundServer` / `A2aInboundServer` / `GrpcInboundService`. The legacy `compat/*-ingress` packages remain standalone compatibility edges.
- Introduced by [NPS-CR-0001](../../spec/cr/NPS-CR-0001-anchor-bridge-split.md) and made bidirectional by NPS-CR-0010; the current SDK surface uses shared Bridge DTOs, the §16.3 error map, inbound protocol servers, and host handlers over `INwpBackend` / `NwpBackend`.

**NWM declaration:**

```json
{
  "node_type": "bridge",
  "capabilities": { "query": false }
}
```

---

## Comparison Summary

| Dimension | Memory Node | Action Node | Complex Node | Anchor Node | Bridge Node |
|-----------|-------------|-------------|--------------|-------------|-------------|
| Primary frame type | QueryFrame | ActionFrame | QueryFrame/ActionFrame | ActionFrame/TaskFrame | ActionFrame |
| Data ownership | Yes | No | Optional | No | No |
| Child node graph | No | No | Yes | Member registry (optional) | No |
| Statefulness | Stateful | Stateful | Stateful | **Stateless** (registry optional) | **Stateless** |
| Horizontal scaling | DB-bound | Business-bound | Child-bound | **Native** | **Native** |
| Primary protocols | NWP | NWP | NWP + NDP | NWP + NOP + NDP | NWP ↔ external (HTTP/gRPC/MCP/A2A) |

---

## Related Documentation

- [NPS.NWP API Reference](../sdk/dotnet/nps-nwp.en.md)
- [DAG Orchestration Concepts](./dag-orchestration.en.md)
- [NWP Specification](../../spec/NPS-2-NWP.md)
- [AaaS Compliance Profile](../../spec/services/NPS-AaaS-Profile.md)
