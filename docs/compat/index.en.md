English | [中文版](./index.md)

# NPS Compatibility Bridges

NPS ships three bridges that expose NWP nodes to existing ecosystems.
All three are **ASP.NET Core libraries** with an isomorphic code path
(`IHttpClientFactory` forwarding to upstream NWP nodes), but they make
different trade-offs on **protocol semantics** and **error semantics**.

| Bridge                           | Outward protocol                     | Upstream nodes                              | Version        | Relationship |
| -------------------------------- | ------------------------------------ | ------------------------------------------- | -------------- | ------------ |
| [MCP Ingress](./mcp-ingress.en.md)  | MCP 2024-11-05 (JSON-RPC 2.0/HTTP)    | Memory / Action / Complex                   | v1.0.0-alpha.16 | 1 : N        |
| [A2A Ingress](./a2a-ingress.en.md)  | Google A2A v0.2 (JSON-RPC 2.0/HTTP)   | Action / Complex / Anchor                   | v1.0.0-alpha.16 | 1 : 1        |
| [gRPC Ingress](./grpc-ingress.en.md) | gRPC/HTTP2 (4 unary RPCs)            | Memory / Action / Complex / Anchor          | v1.0.0-alpha.16 | 1 : N        |

---

## Which one to pick

### Pick **MCP Ingress** when your target is an LLM client

- Client is Claude Desktop / Cursor / Continue or another MCP host
- You want to expose multiple NWP Memory Node schemas as `resources`
- You want to expose multiple NWP Action Node actions as `tools`
- Scenario: let an AI assistant directly read your data + call your API

### Pick **A2A Ingress** when agents call each other

- Peer is a Google A2A-compatible Agent
- One NWP node = one AgentCard
- Need async task lifecycle (`tasks/get` polling)
- Scenario: Agent-to-Agent orchestration, cross-org agent collaboration

### Pick **gRPC Ingress** when plugging into engineering infrastructure

- Caller lives in an existing gRPC / protobuf ecosystem (service mesh, Envoy, existing microservices)
- Need HTTP/2 multiplexing, deadlines, streaming (v0.2+)
- OK with JSON-encoded bytes passthrough (vs. strong-typed proto; see doc §1.1)
- Scenario: fold NPS services into an enterprise k8s + Istio deployment

---

## Shared design

All three bridges share:

1. **Upstream protocol**: all access `/.nwm` / `/invoke` / `/query` /
   `/actions` via NWP HTTP mode (`application/nwp-frame`)
2. **Identity**: service-to-service uses `NwpUpstream.AuthHeader`; upstream
   NWP's Agent identity travels in `X-NWP-Agent` (gRPC ingress lets the
   client override; MCP / A2A ingresss hold it fixed)
3. **Idempotency**: `/invoke` forwards `Idempotency-Key`
4. **Tracing**: W3C `traceparent` forwarded
5. **Statelessness**: except the A2A ingress's in-memory task map, the
   bridges are pure forwarding layers

---

## Shared non-goals

- **No native NWP frame proxying**: bridges **always** use NWP HTTP
  mode; carrying native NCP frames is a v0.3+ direction
- **No schema translation**: NWP is a runtime schema and is **not**
  translated to the bridge protocol's compile-time types (key
  gRPC-bridge decision — see [`grpc-bridge.en.md §1.1`](./grpc-ingress.en.md#11-the-design-fork))
- **No auth proxying**: end-user tokens are **not** forwarded from the
  bridge's client side to upstream; inject via interceptors if needed

---

## Version alignment

| Component        | Version        | Compatible NWP spec                                                   |
| ---------------- | -------------- | --------------------------------------------------------------------- |
| MCP Ingress       | v1.0.0-alpha.16 | [NWP v0.13](../../spec/NPS-2-NWP.md) `/.nwm` + `/invoke` + `/query`    |
| A2A Ingress       | v1.0.0-alpha.16 | [NWP v0.13](../../spec/NPS-2-NWP.md) Action/Complex/Anchor `§3.2` + `§7` async task lifecycle |
| gRPC Ingress      | v1.0.0-alpha.16 | [NWP v0.13](../../spec/NPS-2-NWP.md) `/.nwm` + `/invoke` + `/query` + `/actions` |

---

## Standalone repos

All three bridges are also published as **standalone repos** for NuGet
consumption:

- [labacacia/NPS-MCP-Ingress](https://github.com/labacacia/NPS-MCP-Ingress)
- [labacacia/NPS-A2A-Ingress](https://github.com/labacacia/NPS-A2A-Ingress)
- [labacacia/NPS-gRPC-Ingress](https://github.com/labacacia/NPS-gRPC-Ingress)

Copies under the monorepo (`compat/<bridge>/`) are the **reference
implementation** and accept PRs. The standalone repo is the **release
target**; issues and releases live there.

---

## Detailed docs

- [MCP Ingress deep dive](./mcp-ingress.en.md)
- [A2A Ingress deep dive](./a2a-ingress.en.md)
- [gRPC Ingress deep dive](./grpc-ingress.en.md)

---

## References

- NWP spec: [`spec/NPS-2-NWP.md`](../../spec/NPS-2-NWP.md) v0.5
- AaaS compliance profile: [`spec/services/NPS-AaaS-Profile.md`](../../spec/services/NPS-AaaS-Profile.md)
- Roadmap: [`spec/NPS-Roadmap.md`](../../spec/NPS-Roadmap.md)
