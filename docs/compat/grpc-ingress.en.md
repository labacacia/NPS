English | [中文版](./grpc-ingress.md)

# gRPC Ingress Deep Dive

> **Historical guide.** The standalone ingress repository is archived. Current
> bridge implementation authority is `LabAcacia.NPS.NWP.Bridge` in
> [NPS-SDK-DotNet](https://github.com/labacacia/NPS-SDK-DotNet); use its current
> API and security guidance for new deployments.

> `LabAcacia.GrpcIngress` v1.0.0-alpha.16 — exposes one or more **NWP nodes**
> as a single **gRPC service**. Proto package `labacacia.grpc_bridge.v1`,
> service name `NwpBridge`, 4 unary RPCs.

This document fills in what [`compat/grpc-ingress/README.md`](https://github.com/labacacia/NPS-gRPC-Ingress/blob/main/README.md)
does not cover: why "bytes passthrough" instead of strong-typed proto,
the semantic boundary of error mapping, multi-language client usage, and
how to layer strong-typed proto on top of this service.

- Code: `compat/grpc-ingress/` (monorepo) / `labacacia/NPS-gRPC-Ingress` (standalone)
- Audience: developers calling NWP from an existing gRPC / protobuf
  ecosystem, or teams planning to publish NPS services via a service mesh.

---

## 1. Why a gRPC ingress

One piece of 2026-04-20 review feedback:

> "Why not anchor on JSON? Even if you want to go streaming later, you
> could just integrate gRPC/protobuf."

Fair. gRPC offers:

- Strong-typed code generation (~12 languages)
- HTTP/2 multiplexing + flow control
- Mature industry tooling (Envoy, service mesh, observability)
- Bidirectional / server streaming

NPS native NCP / NWP **should not** be abandoned — it solves the Agent
semantics problem, not the RPC engineering problem. But **bridging** to
gRPC lets callers use familiar tooling. Good separation of concerns.

### 1.1 The design fork

There are two ways to build a gRPC ingress:

| Strategy              | Approach                                                               | Cost                                                          |
| --------------------- | ---------------------------------------------------------------------- | ------------------------------------------------------------- |
| Strong-typed proto    | One `.proto` per NWP Node, one RPC per action                          | NWP is a **runtime schema** (AnchorFrame) — compile-time unknown |
| Generic bytes passthrough | Unified 4 RPCs; payload is JSON-encoded NWP frame body carried in `bytes` | Loses compile-time typing; client parses JSON                |

**v0.1 picks bytes passthrough**. Reasons:

1. NWP's core value is Node-declared + Agent-consumed AnchorFrame.
   Freezing it into proto compile-time types throws away NPS's dynamism advantage.
2. Clients that want strong typing can **layer on top** by generating their
   own `.proto` — translate the Node's AnchorFrame into message definitions
   stacked on `Invoke`'s `params_json` / response `body_json`. See §8.
3. Keeping to 4 RPCs makes the bridge itself small and nearly maintenance-free.

---

## 2. Architecture

```
┌──────────────────┐   gRPC over HTTP/2   ┌────────────────────┐   HTTP/JSON   ┌─────────────────┐
│  gRPC Client     │  ──────────────────→ │  LabAcacia         │  ──────────→ │  NWP Memory /   │
│  (C#, Go, Java,  │                       │  .GrpcIngress       │              │  Action /        │
│   Python, Rust…) │  ←──────────────────  │  (ASP.NET Core +   │  ←────────── │  Complex /       │
│                  │                       │   Kestrel HTTP/2)  │              │  Anchor Node    │
└──────────────────┘                       └────────────────────┘               └─────────────────┘
                                             ▲
                                             │ config
                                             │
                                       NwpUpstream[]
```

One call:

1. Client invokes a unary RPC with `UpstreamContext.upstream = "orders"` in the body.
2. `NwpBridgeService` looks up the upstream dict by name (miss → `NOT_FOUND`).
3. Forwards to `<BaseUrl>/<path>` with `X-NWP-Agent` / `Idempotency-Key` / `traceparent`.
4. Response handling differs per RPC:
   - `GetManifest` / `ListActions`: non-2xx → throws `RpcException`
   - `Invoke` / `Query`: **every** status returned as data in `http_status`

---

## 3. Configuration surface

### 3.1 `GrpcIngressOptions`

| Field             | Default                    | Meaning                                                     |
| ----------------- | -------------------------- | ----------------------------------------------------------- |
| `Upstreams`       | *(required, ≥1)*            | Upstream list                                                |
| `MaxPayloadBytes` | `4 * 1024 * 1024` (4 MiB)   | Max bytes length of `Invoke.params_json` / `Query.query_json` |

### 3.2 `NwpUpstream`

| Field        | Required | Meaning                                                                   |
| ------------ | -------- | ------------------------------------------------------------------------- |
| `Name`       | Yes      | Unique; `UpstreamContext.upstream` looked up by this                       |
| `BaseUrl`    | Yes      | NWP node root URL                                                          |
| `AgentNid`   | No       | **Default** `X-NWP-Agent`; overridden if client supplies `UpstreamContext.agent_nid` |
| `AuthHeader` | No       | Static `Authorization` header                                             |

**Per-request agent_nid override**: this is a key difference between the
gRPC ingress and the MCP / A2A ingresss. The gRPC ingress **forwards**
`UpstreamContext.agent_nid` to `X-NWP-Agent`, letting the gRPC caller
expose its own NID. The MCP / A2A ingresss call upstream with the
bridge's own identity.

### 3.3 DI + routing

```csharp
builder.Services.AddGrpcIngress(o =>
{
    o.Upstreams = new[] { /* ... */ };
});

var app = builder.Build();
app.MapGrpcIngress();   // alias for MapGrpcService<NwpBridgeService>()
app.Run();
```

Kestrel **must support HTTP/2**. `.NET 10` enables it by default. If a
custom endpoint config disables it, opt back in explicitly:

```csharp
builder.WebHost.ConfigureKestrel(o =>
{
    o.ListenAnyIP(5001, l => l.Protocols = HttpProtocols.Http2);
});
```

---

## 4. Protocol mapping

### 4.1 `NwpBridge` service

`proto` package `labacacia.grpc_bridge.v1`, C# namespace `LabAcacia.GrpcIngress.Generated`:

```proto
service NwpBridge {
  rpc GetManifest(ManifestRequest) returns (ManifestResponse);
  rpc Invoke(InvokeRequest)        returns (InvokeResponse);
  rpc Query(QueryRequest)          returns (QueryResponse);
  rpc ListActions(ActionsRequest)  returns (ActionsResponse);
}
```

### 4.2 `UpstreamContext`

All 4 RPC requests carry `UpstreamContext`:

| Field             | proto type | Mapping                                                         |
| ----------------- | ---------- | --------------------------------------------------------------- |
| `upstream`        | string     | Required; keys the upstream dict                                 |
| `agent_nid`       | string     | `X-NWP-Agent`; empty falls back to `NwpUpstream.AgentNid`       |
| `idempotency_key` | string     | `Idempotency-Key` (used only by `Invoke`)                       |
| `traceparent`     | string     | W3C `traceparent`; if empty, generated by Kestrel / upstream HTTP |

### 4.3 `GetManifest`

```
rpc GetManifest(ManifestRequest) returns (ManifestResponse);

message ManifestResponse {
  bytes  nwm_json  = 1;   // full /.nwm JSON
  string node_type = 2;   // memory | action | complex | gateway
}
```

- `GET <BaseUrl>/.nwm`
- Non-2xx → `RpcException` (see §5)
- 2xx → `nwm_json` as-is + `node_type` parsed from body as a convenience field

### 4.4 `Invoke`

```
rpc Invoke(InvokeRequest) returns (InvokeResponse);

message InvokeRequest {
  UpstreamContext ctx         = 1;
  string          action_id   = 2;
  bytes           params_json = 3;   // dynamic params object, JSON-encoded
}

message InvokeResponse {
  int32  http_status = 1;            // 200 / 202 / 4xx / 5xx
  bytes  body_json   = 2;            // CapsFrame / task ack / ErrorFrame
  string task_id     = 3;            // extracted from body only on 202
}
```

- Validation:
  - `action_id` non-empty, else `INVALID_ARGUMENT`
  - `params_json.Length <= MaxPayloadBytes`, else `RESOURCE_EXHAUSTED`
  - `params_json` must be valid JSON, else `INVALID_ARGUMENT`
- Bridge frames it as `{"action_id":"<id>","params":<raw JSON>}` and `POST /invoke`
- **Every status returned as data**; see §5.3

### 4.5 `Query`

```
message QueryRequest {
  UpstreamContext ctx        = 1;
  bytes           query_json = 2;   // QueryFrame body, e.g. {"anchor_ref":..,"where":..,"limit":..}
}

message QueryResponse {
  int32 http_status = 1;
  bytes body_json   = 2;
}
```

- Forwarded verbatim to `POST /query`
- No JSON validation (let upstream decide 400)
- Same as `Invoke`, non-2xx returned as data

### 4.6 `ListActions`

```
message ActionsResponse {
  bytes actions_json = 1;   // full /actions JSON
}
```

- `GET <BaseUrl>/actions`
- Non-2xx → `RpcException`

---

## 5. Error mapping — key design

Two RPC categories, **intentionally** different error semantics.

### 5.1 Discovery RPCs: `GetManifest` / `ListActions`

Non-2xx **throws `RpcException`**, mapped per table:

| Upstream HTTP | gRPC `StatusCode`     |
| ------------- | --------------------- |
| 400 / 422     | `INVALID_ARGUMENT`    |
| 401 / 403     | `PERMISSION_DENIED`   |
| 404           | `NOT_FOUND`           |
| 408           | `DEADLINE_EXCEEDED`   |
| 409           | `ABORTED`             |
| 429           | `RESOURCE_EXHAUSTED`  |
| 5xx           | `UNAVAILABLE`         |
| other         | `UNKNOWN`             |

`RpcException.Status.Detail` contains `<op> upstream returned <code>: <first 256 bytes of body>`.

**Why throw?** Discovery calls have no reasonable **neutral representation**
— if the manifest cannot be read, it's a precondition failure and the
caller shouldn't proceed.

### 5.2 Call RPCs: `Invoke` / `Query`

Non-2xx is **returned as data**:

```csharp
// 200 success
InvokeResponse { http_status: 200, body_json: "<CapsFrame>", task_id: "" }

// 202 async accepted
InvokeResponse { http_status: 202, body_json: "<{task_id:...}>", task_id: "task-abc" }

// 422 business rejection
InvokeResponse { http_status: 422, body_json: "<ErrorFrame JSON>", task_id: "" }

// 502 upstream down
InvokeResponse { http_status: 502, body_json: "<server error body>", task_id: "" }
```

**Why not throw?** Callers need to distinguish:

- **Business rejection** (422 "order quantity invalid", 429 "rate limited",
  409 "conflict") — this is information, not an error; client UI surfaces it to users
- **Transport failure** (upstream dead, 5xx; network broken pipe) — this is a real error

If every 4xx/5xx threw as gRPC `RpcException`, clients would be forced to
`try/catch` every business branch — painful to write and semantically
muddled. Returning status **as data** lets the client branch on
`http_status` itself.

### 5.3 gRPC statuses still thrown

- `INVALID_ARGUMENT`: empty `ctx.upstream`; empty `action_id`; invalid JSON in `params_json`
- `NOT_FOUND`: `ctx.upstream` not in config
- `RESOURCE_EXHAUSTED`: exceeded `MaxPayloadBytes`

These are **caller bugs** (not network errors), hence thrown as RpcException.

### 5.4 Network-level real errors

`HttpClient`'s `HttpRequestException` and friends are **not** handled by
`MapUpstreamFailure`. They bubble up through the gRPC framework and
become `UNKNOWN`. Production can wrap `NwpUpstreamClient` in a
`try/catch` to map network errors to `UNAVAILABLE` — v0.2 will have this
built in.

---

## 6. Header passthrough rules

| Header            | Source priority                                       | Notes                       |
| ----------------- | ----------------------------------------------------- | --------------------------- |
| `X-NWP-Agent`     | `ctx.agent_nid` > `NwpUpstream.AgentNid` > none        | Empty string treated as none |
| `Idempotency-Key` | `ctx.idempotency_key` > none                           | `/invoke` only               |
| `traceparent`     | `ctx.traceparent` > none                               | Bridge does not generate; client should |
| `Authorization`   | `NwpUpstream.AuthHeader` > none                       | **Not forwarded from gRPC client** |
| `Content-Type`    | `application/nwp-frame` (auto-set on POST)            | Fixed                        |

**Key**: `Authorization` is **not** forwarded from the gRPC client to
upstream. Auth on the gRPC call side (usually mTLS or an interceptor
token) and upstream NWP's auth are separate concerns; the bridge's
`NwpUpstream.AuthHeader` is a **service-to-service** credential. To pass
an end-user token upstream, use gRPC metadata + a custom interceptor in
the service that reads `context.RequestHeaders` and overrides (not built in yet).

---

## 7. Multi-language client usage

### 7.1 C#

```csharp
using Grpc.Net.Client;
using LabAcacia.GrpcIngress.Generated;

using var channel = GrpcChannel.ForAddress("https://bridge.example.com:5001");
var client = new NwpBridge.NwpBridgeClient(channel);

var resp = await client.InvokeAsync(new InvokeRequest
{
    Ctx = new UpstreamContext
    {
        Upstream       = "orders",
        AgentNid       = "nid:ed25519:...",
        IdempotencyKey = Guid.NewGuid().ToString(),
    },
    ActionId   = "orders.create",
    ParamsJson = Google.Protobuf.ByteString.CopyFromUtf8("""{"sku":"ABC","qty":1}"""),
});

Console.WriteLine($"HTTP {resp.HttpStatus}, body={resp.BodyJson.ToStringUtf8()}");
```

### 7.2 Go

```go
import (
    "context"
    pb "github.com/labacacia/nps-grpc-ingress/gen/v1"
    "google.golang.org/grpc"
)

conn, _ := grpc.Dial("bridge.example.com:5001", grpc.WithTransportCredentials(...))
client := pb.NewNwpBridgeClient(conn)

resp, err := client.Invoke(context.Background(), &pb.InvokeRequest{
    Ctx: &pb.UpstreamContext{Upstream: "orders", AgentNid: "nid:ed25519:..."},
    ActionId:   "orders.create",
    ParamsJson: []byte(`{"sku":"ABC","qty":1}`),
})

fmt.Printf("HTTP %d, body=%s\n", resp.HttpStatus, resp.BodyJson)
```

### 7.3 Python

```python
import json
import grpc
from nwp_bridge_v1 import nwp_bridge_pb2, nwp_bridge_pb2_grpc

channel = grpc.secure_channel("bridge.example.com:5001", grpc.ssl_channel_credentials())
stub    = nwp_bridge_pb2_grpc.NwpBridgeStub(channel)

resp = stub.Invoke(nwp_bridge_pb2.InvokeRequest(
    ctx=nwp_bridge_pb2.UpstreamContext(upstream="orders", agent_nid="nid:ed25519:..."),
    action_id="orders.create",
    params_json=json.dumps({"sku":"ABC","qty":1}).encode()
))

print(resp.http_status, resp.body_json.decode())
```

### 7.4 Java

```java
ManagedChannel channel = ManagedChannelBuilder
    .forAddress("bridge.example.com", 5001).useTransportSecurity().build();
NwpBridgeBlockingStub stub = NwpBridgeGrpc.newBlockingStub(channel);

InvokeResponse resp = stub.invoke(InvokeRequest.newBuilder()
    .setCtx(UpstreamContext.newBuilder()
        .setUpstream("orders").setAgentNid("nid:ed25519:...").build())
    .setActionId("orders.create")
    .setParamsJson(ByteString.copyFromUtf8("{\"sku\":\"ABC\",\"qty\":1}"))
    .build());

System.out.printf("HTTP %d, body=%s%n", resp.getHttpStatus(), resp.getBodyJson().toStringUtf8());
```

### 7.5 `grpcurl`

```bash
grpcurl -proto ./nwp_ingress.proto \
  -d '{
    "ctx": {"upstream":"orders","agent_nid":"nid:ed25519:..."},
    "action_id": "orders.create",
    "params_json": "eyJza3UiOiJBQkMiLCJxdHkiOjF9"
  }' \
  bridge.example.com:5001 \
  labacacia.grpc_bridge.v1.NwpBridge/Invoke
```

Note: `grpcurl` requires `bytes` fields to be base64-encoded.

---

## 8. Layering strong-typed proto on top of bytes passthrough

Callers who want compile-time types — recommended approach:

1. Pull the AnchorFrame from the NWP Node's `/.schema?action_id=orders.create`
2. Translate field definitions into a `.proto` message:

```proto
// orders.proto — third-party generated
package my.company.orders.v1;

message CreateOrderRequest {
  string sku = 1;
  int32  qty = 2;
}

message CreateOrderResponse {
  string order_id = 1;
  string status   = 2;
}
```

3. Write a thin client wrapper:

```csharp
class OrdersClient(NwpBridge.NwpBridgeClient inner)
{
    public async Task<CreateOrderResponse> CreateAsync(CreateOrderRequest req)
    {
        var paramsJson = JsonFormatter.Default.Format(req); // protobuf → JSON
        var resp = await inner.InvokeAsync(new InvokeRequest {
            Ctx        = new() { Upstream = "orders" },
            ActionId   = "orders.create",
            ParamsJson = ByteString.CopyFromUtf8(paramsJson),
        });
        if (resp.HttpStatus != 200)
            throw new BusinessException(resp.HttpStatus, resp.BodyJson.ToStringUtf8());
        return JsonParser.Default.Parse<CreateOrderResponse>(resp.BodyJson.ToStringUtf8());
    }
}
```

Bridge transparency preserved; client gets strong types.

---

## 9. Deployment

### 9.1 TLS

The gRPC ecosystem **mandates HTTP/2**, and most clients **require TLS**
(HTTP/2 cleartext `h2c` must be explicitly configured). Production Kestrel:

```csharp
builder.WebHost.ConfigureKestrel(o =>
{
    o.ListenAnyIP(5001, l =>
    {
        l.Protocols = HttpProtocols.Http2;
        l.UseHttps("/etc/certs/fullchain.pfx", "<pw>");
    });
});
```

### 9.2 Authentication

Server: gRPC interceptor or ASP.NET Core's `RequireAuthorization()`.

```csharp
app.MapGrpcIngress().RequireAuthorization("grpc-clients");
```

Client: issue JWT or mTLS via CallCredentials / ChannelCredentials;
bridge stays out of it.

### 9.3 Multiple replicas

The bridge is **stateless** (not even a task map — async task state is
tracked upstream via `system.task.status`). Horizontal scaling is trivial.

### 9.4 Service mesh / Envoy

gRPC + HTTP/2 is first-class in Envoy / Linkerd / Istio. Annotate the
bridge pod `appProtocol: grpc` (k8s service) for L7 load balancing.
In the mesh, **do not** let the mesh rewrite `Authorization` (avoid
clashing with the bridge's `AuthHeader`).

### 9.5 Timeouts

gRPC has a **deadline** concept. When the client sets a deadline,
`ServerCallContext.CancellationToken` cancels the upstream `HttpClient`
call on expiry. Recommended two-tier timeouts:

| Layer                 | Typical     |
| --------------------- | ----------- |
| gRPC deadline         | 30 s        |
| HttpClient timeout    | 25 s        |

Let HTTP time out slightly before gRPC so gRPC has a chance to return
`DEADLINE_EXCEEDED`.

---

## 10. Observability

### 10.1 Built-in ASP.NET Core

- Kestrel request logs (set `Logging.LogLevel.Microsoft.AspNetCore = Information`)
- gRPC's `ServerCallContext` exposes peer, method, and other context

### 10.2 OpenTelemetry

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddGrpcClientInstrumentation()
        .AddHttpClientInstrumentation())   // ↑ upstream HttpClient
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation());
```

This auto-emits:

- gRPC server entry spans (one per RPC)
- Upstream HTTP egress spans (one per `/.nwm` / `/invoke` etc.)
- Latency histograms per method

### 10.3 traceparent propagation

If the client passes W3C traceparent in `UpstreamContext.traceparent`,
the bridge forwards it to the upstream HTTP header — the cross-protocol
trace chain stays intact. If the client didn't, OpenTelemetry HttpClient
instrumentation generates one.

---

## 11. Testing

```bash
dotnet test compat/grpc-ingress/tests/LabAcacia.GrpcIngress.Tests/
```

15 tests cover:

- Happy paths for all 4 RPCs
- `Invoke` 422 business failure → returned as data, not thrown
- `GetManifest` 404 → throws `NOT_FOUND`
- `ListActions` 500 → throws `UNAVAILABLE`
- `params_json` invalid JSON → throws `INVALID_ARGUMENT`
- Empty `ctx.upstream` → throws `INVALID_ARGUMENT`
- Unregistered `ctx.upstream` → throws `NOT_FOUND`
- Exceeding `MaxPayloadBytes` → throws `RESOURCE_EXHAUSTED`
- `agent_nid` / `idempotency_key` / `traceparent` propagated to upstream header
- Async 202 → `task_id` extracted from body

Tests use a custom `TestServerCallContext` (avoiding the heavy
`Grpc.Core.Testing` dependency).

---

## 12. Limitations

| Category                   | Status          | Notes                                                         |
| -------------------------- | --------------- | ------------------------------------------------------------- |
| Server-streaming / bidi    | Not supported   | v0.2 plans `InvokeStream` (matching AlignStream)              |
| Reflection / Descriptor API | Not built-in   | Generate from `.proto`; or register `services.AddGrpcReflection()` |
| Global interceptor         | Not built-in    | Caller injects their own (Kestrel standard path)              |
| Strong-typed proto         | Not built-in    | Layer per §8                                                  |
| `Authorization` forwarding | Not forwarded   | Use `NwpUpstream.AuthHeader` static injection                 |
| Health check               | Not built-in    | Recommend the `Grpc.HealthCheck` package                      |

---

## 13. FAQ

**Q: Why not generate a `.proto` per NWP node?**
A: See §1.1. Short version: compile-time schema clashes with NWP runtime schema.

**Q: Is bytes passthrough inefficient?**
A: Only one extra `JSON.Parse` vs strong-typed marshalling. Compared to
HTTP/1.1 + JSON for a raw NWP call, the gains from gRPC's HTTP/2 frame
multiplexing + header compression (HPACK) far outweigh the parse cost.
Extreme size sensitivity? Switch the NWP upstream to the MsgPack tier
(NWP supports `Accept: application/vnd.nps.msgpack`); the bridge
forwards transparently.

**Q: Can a single NwpBridge service attach multiple upstreams?**
A: Yes. `Upstreams` is a list; `UpstreamContext.upstream` selects by name.

**Q: How does a gRPC client know what actions an upstream has?**
A: Call `ListActions` first to get `actions_json` (the NWP raw
`/actions` response), and parse client-side. Pair with `GetManifest`
to get `node_type`.

**Q: Can the bridge proxy native NWP frames (non-HTTP mode)?**
A: Not yet. The bridge internally uses `HttpClient` with NWP HTTP mode.
Carrying native NCP frames directly through gRPC bytes is a viable
design (v0.3+ consideration) but requires the bridge to understand NCP
frame headers.

**Q: When does `task_id` have a value?**
A: Only when `http_status == 202` **and** the body is valid JSON with a
top-level `task_id` field. Empty string otherwise.

---

## 14. References

- NWP spec: [`spec/NPS-2-NWP.md`](../../spec/NPS-2-NWP.md) v0.5
- proto definition: [`compat/grpc-ingress/src/LabAcacia.GrpcIngress/Protos/nwp_ingress.proto`](https://github.com/labacacia/NPS-gRPC-Ingress/blob/main/src/LabAcacia.GrpcIngress/Protos/nwp_ingress.proto)
- gRPC .NET docs: https://learn.microsoft.com/aspnet/core/grpc/
- CHANGELOG: [`compat/grpc-ingress/CHANGELOG.md`](https://github.com/labacacia/NPS-gRPC-Ingress/blob/main/CHANGELOG.md)
- Standalone repo: [labacacia/NPS-gRPC-Ingress](https://github.com/labacacia/NPS-gRPC-Ingress)
