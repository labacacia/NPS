# Example: NWP Graph Walk

**Status:** ✅ Reviewed for v1.0.0-alpha.19 release

**Repo:** `labacacia/NPS-Examples`, directory: `nwp-graph-walk/` (source in NPS-Dev `demos/nwp-graph-walk/`)

This demo demonstrates NWP Complex Node graph traversal — how an agent issues one request and receives aggregated results from a multi-hop graph of nodes, with server-enforced depth control and cycle detection.

---

## Purpose

If you are evaluating NPS as a foundation for Agent-native knowledge graphs, you need to answer three practical questions before adopting it:

1. **Does one query really hit the whole graph?** This demo proves the typed-ref fanout end-to-end with a Memory Node + a Complex Node.
2. **Can I bound blast radius?** Graph walks are trivially DoS-able without a depth cap and a cycle detector. This demo exercises both safety gates.
3. **What does an Agent actually see?** The response is one deterministic JSON shape, not a multiplexed stream of fragments — which means it can be cached by `anchor_ref`.

---

## What Complex Node Behavior Means

In NWP (NPS-2 §11), a **Complex Node** is a node that can recursively delegate sub-queries to other NWP nodes and aggregate the results into a single response. It does this by declaring `graph.refs` in its Neural Web Manifest (NWM) — each `ref` is a labeled edge to another NWP node URL.

When an Agent issues `POST /query` with `X-NWP-Depth: N`, the Complex Node:

1. Answers its own query locally (depth `N`).
2. For every declared `graph.refs[<rel>]`, issues a child `POST /query` with `X-NWP-Depth: N-1`, passing traversal context sufficient for cycle detection (mechanism is implementation-defined).
3. Inlines each child's CapsFrame under `graph[<rel>].data` in its response.

Two hard safety gates apply at every hop:

- **`max_depth` in the NWM `graph` block (NWP §11):** A node's NWM may cap the maximum depth it is willing to serve. A request exceeding this is rejected *before* any child call with `NWP-DEPTH-EXCEEDED` (HTTP 400 / NPS-CLIENT-BAD-PARAM). The check is pre-fanout — a malicious Agent cannot amplify load by requesting depth 100.
- **Cycle detection (NWP §11):** The spec requires nodes to detect circular references and emit `NWP-GRAPH-CYCLE` (HTTP 422 / NPS-CLIENT-UNPROCESSABLE). The detection mechanism is implementation-defined. The parent surfaces the error under `graph[].error` without failing its own response — the traversal continues and the useful data is preserved.

---

---

## Not to Be Confused With: NDP GraphFrame / Topology Subscriptions

This demo is about **NWP Complex Node graph traversal** — an Agent issuing one `POST /query` and having a Complex Node recursively fan out over its declared `graph.refs` (NWP §11). That is a query-time, request-scoped traversal of a node's own sub-node references.

It is a separate mechanism from the **NDP topology graph**, which two other alpha.13 features cover:

- **NDP `GraphFrame` (0x32)** is now the §5 **topology-snapshot** format (NDP v0.8): `graph_id`, `nodes` (each with `nid` / `cluster_anchor` / `node_roles`), `edges` (`from_nid` / `to_nid` / `latency_ms` / `protocol`), `ttl`, and `metadata`, bounded at **max 256 nodes / 1024 edges** (`NDP-GRAPH-TOO-LARGE`, `NDP-GRAPH-INVALID`). It describes the registry's view of the node graph, not an Agent's per-query fanout.
- **Topology subscriptions** use NWP **`SubscribeFrame` (0x12)** with `type = "topology.stream"` (NWP §12–13, CR-0006): a client-generated `subscription_id` (UUID v4) plus an opaque `cursor` for lossless resume, gated by the `topology:subscribe` capability. Topology consumers also track `manifest_version` and the `X-NWM-Version` response header on `GET /.nwm` (NWP v0.14).

Neither GraphFrame nor SubscribeFrame is exercised by this demo; the demo stays entirely within the NWP Complex Node `graph.refs` request path described above.

---

## Demo Topology

Five in-process loopback nodes:

```
                ┌──────────────────┐
                │ customers :17451 │  (Memory Node)
                └────────▲─────────┘
                         │  graph.refs[customer]
                ┌────────┴─────────┐
                │   orders :17450  │  (Complex Node, max_depth=2)
                └────────┬─────────┘
                         │  graph.refs[product]
                ┌────────▼─────────┐
                │  products :17452 │  (Memory Node)
                └──────────────────┘

 ┌──────────────────┐        ┌──────────────────┐
 │  hub-a :17460    │ ◄────► │  hub-b :17461    │   (cycle pair)
 │  graph.refs[peer]│        │  graph.refs[peer]│
 └──────────────────┘        └──────────────────┘
```

---

## How to Run

```bash
git clone https://github.com/labacacia/nps.git
cd nps
dotnet run --project demos/nwp-graph-walk
```

Requires .NET 10 SDK. All five nodes and the client run inside one process; nothing binds beyond `127.0.0.1`.

---

## Scenes and Expected Output

### Scene A — depth=0, no fanout

```
POST http://127.0.0.1:17450/query   X-NWP-Depth: 0
→ HTTP 200 OK
{
  "anchor_ref": "sha256:8a68d6e3…",
  "count": 3,
  "data": [ {"id":1001, "customer_id":501, "product_id":301, …}, … ]
}
```

No `graph` key. A Complex Node at depth 0 behaves exactly like a Memory Node — it answers only its own data.

### Scene B — depth=1, one hop

```
POST http://127.0.0.1:17450/query   X-NWP-Depth: 1
→ HTTP 200 OK
{
  "anchor_ref": "sha256:8a68d6e3…",
  "count": 3,
  "data": [ … ],
  "graph": [
    { "rel": "customer",
      "node": "http://127.0.0.1:17451",
      "data": { "anchor_ref": "sha256:ec903485…", "count": 2, "data": [ … ] } },
    { "rel": "product",
      "node": "http://127.0.0.1:17452",
      "data": { "anchor_ref": "sha256:a734e8fa…", "count": 3, "data": [ … ] } }
  ]
}
```

One round trip from the Agent, two child calls (`customers` + `products`) issued by the Complex Node, two distinct anchor refs returned. The Agent caches each child node independently.

**Key learning — how `cgn_est` accumulates across graph hops:** The `cgn_est` field (Cognon estimate, renamed from `estimated_npt` in alpha.5.2) in the top-level CapsFrame represents the token budget consumed by the entire response, including the inlined child frames. Each child CapsFrame also carries its own `cgn_est`. An Agent tracking token budget across a graph traversal should sum the `cgn_est` values from each distinct CapsFrame it receives — the top-level `cgn_est` does not automatically aggregate the children's values.

### Scene C — depth=9, rejected before fanout

```
POST http://127.0.0.1:17450/query   X-NWP-Depth: 9
→ HTTP 400 Bad Request
{
  "frame_type": 254,
  "status": "NPS-CLIENT-BAD-PARAM",
  "error": "NWP-DEPTH-EXCEEDED",
  "message": "X-NWP-Depth 9 exceeds node max_depth 2."
}
```


Frame type 254 = ErrorFrame (0xFE). The `status` shown above (`NPS-CLIENT-BAD-PARAM`) reflects the alpha.13 status-code mapping for `NWP-DEPTH-EXCEEDED`. The check happens before any child call is made. The `orders` node's NWM declares `graph.max_depth: 2`; the request for depth 9 is rejected immediately. There is no amplification risk.

### Scene D — mutual reference, cycle caught

Starting at `hub-a` with depth=2. `hub-a` references `hub-b` (`graph.refs[peer]`), and `hub-b` references `hub-a` back:

```
POST http://127.0.0.1:17460/query   X-NWP-Depth: 2
→ HTTP 200 OK
{
  "anchor_ref": "sha256:779ec85b…",
  "count": 1, "data": [ {"id":1, "label":"hub-a local row"} ],
  "graph": [
    { "rel": "peer", "node": "http://127.0.0.1:17461",
      "data": {
        "count": 1, "data": [ {"id":2, "label":"hub-b local row"} ],
        "graph": [
          { "rel": "peer", "node": "http://127.0.0.1:17460",
            "error": {
              "code": "NWP-NODE-UNAVAILABLE",
              "message": "child 'peer' returned 422: {…,\"error\":\"NWP-GRAPH-CYCLE\",…graph cycle detected at 'urn:nps:node:demo.local:hub-a'.}"
            } }
        ]
      } }
  ]
}
```

The overall traversal **succeeds** (HTTP 200 with both hubs' rows). The cycle is surfaced as a scoped error under `graph[].error`. An Agent can use the useful data from both hubs and still see exactly where the loop closed.

---

## Layout

```
demos/nwp-graph-walk/
├── Program.cs                 # 5 loopback nodes + 4 scenes (A/B/C/D)
├── NodeHosts.cs               # WebApplication factories (Memory / Complex)
├── InMemoryProviders.cs       # StaticMemoryNodeProvider + StaticComplexNodeProvider
├── DemoData.cs                # schemas + fixed rows (orders/customers/products/hubs)
└── NPS.Demo.GraphWalk.csproj  # .NET 10 Web SDK
```

---

## Demo-Only Configuration

Two `ComplexNodeOptions` are relaxed so the traversal can execute on loopback:

| Option | Demo value | Production default | Reason |
|--------|-----------|-------------------|--------|
| `RejectPrivateChildUrls` | `false` | `true` | Loopback / RFC1918 is blocked in production to prevent SSRF. |
| `AllowHttpChildUrls` | `true` | `false` | NPS-2 §13.2 mandates `https://` for child fetches. |

The `AllowedChildUrlPrefixes` allowlist (`["http://127.0.0.1:"]`) is still enforced — the demo does not disable the allowlist, only the scheme check.

---

## Snapshot Refresh

The captured output above was recorded on 2026-04-21. Output snapshots must be refreshed at each suite version change. The `cgn_est` field rename (from `token_est` in older protocol versions) in alpha.5.2 breaks older snapshots — regenerate them with `dotnet run --project demos/nwp-graph-walk` after any NWP protocol change.

---

## Limitations

- **No NIP.** `X-NWP-Agent` carries a bare URN; no certificate chain, no capability check, no rate limit.
- **Depth-5 ceiling not separately demonstrated.** NPS-2 §11 caps overall depth at 5 regardless of `graph_max_depth`; this is covered by unit tests, not this demo.
- **In-process topology.** All five nodes share one process and one `HttpClientFactory`. Production Complex Nodes talk to independent nodes over HTTPS with real certificates.

---

## Related Pages

- [Protocol NWP](./Protocol-NWP.md) — full NWP specification reference
- [SDK Common Patterns](./SDK-Common-Patterns.md) — how to build queries with depth control from any SDK

---

*Last reviewed at suite version: v1.0.0-alpha.19 release*
