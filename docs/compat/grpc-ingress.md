[English Version](./grpc-ingress.en.md) | 中文版

# gRPC Ingress 详解

> **历史指南。** 独立 ingress 仓库已经归档。当前 bridge 实现权威为
> [NPS-SDK-DotNet](https://github.com/labacacia/NPS-SDK-DotNet) 中的
> `LabAcacia.NPS.NWP.Bridge`；新部署应以其当前 API 与安全说明为准。

> `LabAcacia.GrpcIngress` v1.0.0-alpha.16 — 把一个或多个 **NWP 节点** 暴露成
> 一个 **gRPC 服务**。proto 包 `labacacia.grpc_bridge.v1`，服务名 `NwpBridge`，
> 4 个 unary RPC。

本文补充 [`compat/grpc-ingress/README.md`](https://github.com/labacacia/NPS-gRPC-Ingress/blob/main/README.md) 未覆盖的细节：
为什么是"bytes 透传"而非强类型 proto、错误映射的语义边界、多语言 client 用法、
如何在本服务之上叠一层强类型 proto。

- 代码仓：`compat/grpc-ingress/`（monorepo）/ `labacacia/NPS-gRPC-Ingress`（独立）
- 面向读者：要从已有 gRPC / protobuf 生态调 NWP 的开发人员，或打算把 NPS
  服务通过 service mesh 发布的团队。

---

## 1. 为什么要 gRPC 桥

2026-04-20 评审的一条意见：

> "为什么不锚在 json 上呢，就算未来打算往流发展也可以直接集成 grpc/protobuf。"

合理。gRPC 有：

- 强类型生成（~12 种语言）
- HTTP/2 多路复用 + flow control
- 业界成熟的工具链（Envoy、service mesh、observability）
- 双向流、server streaming

NPS 原生 NCP / NWP **不应该**抛弃——它解决的是 Agent 语义问题，不是 RPC
工程问题。但**桥接**到 gRPC 让调用方可以用熟悉的工具链，就是良好分工。

### 1.1 桥的设计分歧点

做 gRPC 桥有两条路：

| 策略              | 做法                                                           | 代价                                                    |
| ----------------- | -------------------------------------------------------------- | ------------------------------------------------------- |
| 强类型 proto      | 每个 NWP Node 一份 `.proto`，每个 action 一条 RPC              | NWP 是**运行时 schema**（AnchorFrame）—— 编译期就不知道结构 |
| 通用 bytes 透传   | 统一 4 个 RPC；payload 是 JSON 编码的 NWP 帧体，承载在 `bytes` | 失去了编译期类型，client 自己解 JSON                    |

**v0.1 选 bytes 透传**。理由：

1. NWP 的核心价值是 Node 声明 + Agent 消费 AnchorFrame。把它**冻死**在
   proto 的编译期类型里，就失去了 NPS 在动态性上的优势。
2. 需要强类型的 client，可以**在本服务之上**生成自己的 `.proto`——
   把 NWP node 的 AnchorFrame 翻译成 message 定义，layer 到 `Invoke`
   的 `params_json` / response `body_json` 上。参见 §11。
3. 保持 4 个 RPC 的简洁，让桥本身小到几乎没有维护成本。

---

## 2. 架构

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

一次调用：

1. Client 发起 unary RPC，body 里 `UpstreamContext.upstream = "orders"`
2. `NwpBridgeService` 按 name 查 upstream dict（失败 → `NOT_FOUND`）
3. 转发到 `<BaseUrl>/<path>`，带上 `X-NWP-Agent` / `Idempotency-Key` / `traceparent`
4. 收到 response 后按 RPC 不同策略处理：
   - `GetManifest` / `ListActions`：非 2xx → 抛 `RpcException`
   - `Invoke` / `Query`：**所有**状态都作为数据返回 `http_status` 字段

---

## 3. 配置表面

### 3.1 `GrpcIngressOptions`

| 字段              | 默认                  | 含义                                                     |
| ----------------- | --------------------- | -------------------------------------------------------- |
| `Upstreams`       | *（必填，≥1）*         | 上游列表                                                  |
| `MaxPayloadBytes` | `4 * 1024 * 1024`（4 MiB） | `Invoke.params_json` / `Query.query_json` 的最大 bytes 长度 |

### 3.2 `NwpUpstream`

| 字段         | 必填 | 含义                                    |
| ------------ | ---- | --------------------------------------- |
| `Name`       | 是   | 唯一；`UpstreamContext.upstream` 按此查找 |
| `BaseUrl`    | 是   | NWP node 根 URL                         |
| `AgentNid`   | 否   | **默认** `X-NWP-Agent`；若 client 在 `UpstreamContext.agent_nid` 里提供，用 client 的 |
| `AuthHeader` | 否   | 静态 `Authorization` header             |

**per-request agent_nid 覆盖**：这是 gRPC 桥和 MCP / A2A 桥的重要差别。
gRPC 桥把 `UpstreamContext.agent_nid` **透传**到 `X-NWP-Agent`，让 gRPC
调用方暴露自己的 NID。MCP / A2A 桥以桥自身身份调上游。

### 3.3 DI + 路由

```csharp
builder.Services.AddGrpcIngress(o =>
{
    o.Upstreams = new[] { /* ... */ };
});

var app = builder.Build();
app.MapGrpcIngress();   // MapGrpcService<NwpBridgeService>() 的别名
app.Run();
```

Kestrel **必须支持 HTTP/2**。`.NET 10` 默认开启；若在自定义 endpoint
配置里关闭，请显式允许：

```csharp
builder.WebHost.ConfigureKestrel(o =>
{
    o.ListenAnyIP(5001, l => l.Protocols = HttpProtocols.Http2);
});
```

---

## 4. 协议映射

### 4.1 `NwpBridge` 服务

`proto` 包 `labacacia.grpc_bridge.v1`，C# namespace `LabAcacia.GrpcIngress.Generated`：

```proto
service NwpBridge {
  rpc GetManifest(ManifestRequest) returns (ManifestResponse);
  rpc Invoke(InvokeRequest)        returns (InvokeResponse);
  rpc Query(QueryRequest)          returns (QueryResponse);
  rpc ListActions(ActionsRequest)  returns (ActionsResponse);
}
```

### 4.2 `UpstreamContext`

所有 4 个 RPC 的请求都携带 `UpstreamContext`：

| 字段              | proto 类型 | 映射                                                         |
| ----------------- | ---------- | ------------------------------------------------------------ |
| `upstream`        | string     | 必填；查 dict 找 `NwpUpstream`                                |
| `agent_nid`       | string     | `X-NWP-Agent`；空则回落到 `NwpUpstream.AgentNid`              |
| `idempotency_key` | string     | `Idempotency-Key`（仅 `Invoke` 用）                          |
| `traceparent`     | string     | W3C `traceparent`；空则由 Kestrel / upstream HTTP 生成       |

### 4.3 `GetManifest`

```
rpc GetManifest(ManifestRequest) returns (ManifestResponse);

message ManifestResponse {
  bytes  nwm_json  = 1;   // /.nwm 整段 JSON
  string node_type = 2;   // memory | action | complex | gateway
}
```

- `GET <BaseUrl>/.nwm`
- 非 2xx → `RpcException`（见 §5）
- 2xx → `nwm_json` 原样 + 从 body 解析 `node_type` 作为便利字段

### 4.4 `Invoke`

```
rpc Invoke(InvokeRequest) returns (InvokeResponse);

message InvokeRequest {
  UpstreamContext ctx         = 1;
  string          action_id   = 2;
  bytes           params_json = 3;   // 动态 params 对象，JSON 编码
}

message InvokeResponse {
  int32  http_status = 1;            // 200 / 202 / 4xx / 5xx
  bytes  body_json   = 2;            // CapsFrame / task ack / ErrorFrame
  string task_id     = 3;            // 仅 202 时从 body 提取
}
```

- 校验：
  - `action_id` 非空 → 否则 `INVALID_ARGUMENT`
  - `params_json.Length <= MaxPayloadBytes` → 否则 `RESOURCE_EXHAUSTED`
  - `params_json` 必须是合法 JSON → 否则 `INVALID_ARGUMENT`
- 桥组帧：`{"action_id":"<id>","params":<raw JSON>}` 送到 `POST /invoke`
- **所有状态作为数据返回**；见 §5.3

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

- 原样 POST 到 `/query`
- 不做任何 JSON 校验（让上游决定 400）
- 同 `Invoke`，非 2xx 作为数据返回

### 4.6 `ListActions`

```
message ActionsResponse {
  bytes actions_json = 1;   // /actions 整段 JSON
}
```

- `GET <BaseUrl>/actions`
- 非 2xx → `RpcException`

---

## 5. 错误映射 — 关键设计

这里有两类 RPC，错误语义**故意**不同。

### 5.1 发现类 RPC：`GetManifest` / `ListActions`

非 2xx **抛 `RpcException`**，按表映射：

| Upstream HTTP | gRPC `StatusCode`     |
| ------------- | --------------------- |
| 400 / 422     | `INVALID_ARGUMENT`    |
| 401 / 403     | `PERMISSION_DENIED`   |
| 404           | `NOT_FOUND`           |
| 408           | `DEADLINE_EXCEEDED`   |
| 409           | `ABORTED`             |
| 429           | `RESOURCE_EXHAUSTED`  |
| 5xx           | `UNAVAILABLE`         |
| 其他          | `UNKNOWN`             |

`RpcException.Status.Detail` 包含 `<op> upstream returned <code>: <body前 256 字节>`。

**为什么抛？** 发现类调用没有合理的**中性表示**——
manifest 读不到就是调用者不该继续的前置条件。

### 5.2 调用类 RPC：`Invoke` / `Query`

非 2xx **作为数据**返回：

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

**为什么不抛？** 调用方需要区分：

- **业务拒绝**（422 "order quantity invalid"，429 "rate limited"，409 "conflict"）
  —— 这是信息，不是错误；客户端 UI 要展示给用户
- **传输故障**（上游挂了，5xx；网络 broken pipe）
  —— 这是真错误

把 4xx/5xx 一律抛成 gRPC `RpcException`，客户端被迫 `try/catch` 所有业务分支，
代码难写而且语义混淆。把状态**作为数据**返回，客户端按 `http_status` 自己
判断。

### 5.3 仍然会抛的 gRPC 状态

- `INVALID_ARGUMENT`：`ctx.upstream` 空；`action_id` 空；`params_json` 非法 JSON
- `NOT_FOUND`：`ctx.upstream` 不在配置中
- `RESOURCE_EXHAUSTED`：超过 `MaxPayloadBytes`

这些是**调用方错**（非网络错），故作为 RpcException 抛。

### 5.4 网络层真错

`HttpClient` 抛出的 `HttpRequestException` 等**不**被 `MapUpstreamFailure`
处理。它们会冒泡到 gRPC 框架，变成 `UNKNOWN`。生产可以在 `NwpUpstreamClient`
外套一层 `try/catch` 把网络错映射成 `UNAVAILABLE`——未来 v0.2 会内置。

---

## 6. Header 透传规则

| Header            | 来源优先级                                       | 说明                       |
| ----------------- | ------------------------------------------------ | -------------------------- |
| `X-NWP-Agent`     | `ctx.agent_nid` > `NwpUpstream.AgentNid` > 无    | 空字符串视为无              |
| `Idempotency-Key` | `ctx.idempotency_key` > 无                        | 仅 `/invoke`                |
| `traceparent`     | `ctx.traceparent` > 无                            | 桥不生成；建议客户端生成    |
| `Authorization`   | `NwpUpstream.AuthHeader` > 无                    | **不从 gRPC client 透传**  |
| `Content-Type`    | `application/nwp-frame`（POST 时自动加）         | 固定                        |

**关键**：`Authorization` **不**从 gRPC client 透传到上游。gRPC 调用侧的
auth（通常是 mTLS 或 interceptor token）和上游 NWP 的 auth 是两回事；
桥的 `NwpUpstream.AuthHeader` 是**服务到服务**的凭证。如果需要把 end-user
token 透到上游，用 gRPC metadata + 自定义 interceptor 在 service 里读
`context.RequestHeaders` 然后覆盖（暂未内置）。

---

## 7. 多语言 client 用法

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

注意：`grpcurl` 需要 `bytes` 字段使用 base64 编码。

---

## 8. 在 bytes 透传之上叠强类型 proto

调用方想要编译期类型？推荐做法：

1. 从 NWP Node 的 `/.schema?action_id=orders.create` 拉 AnchorFrame
2. 根据字段定义翻译成 `.proto` message：

```proto
// orders.proto — 第三方生成
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

3. 写 thin client wrapper：

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

保留了桥的透明性，同时给 client 强类型。

---

## 9. 部署

### 9.1 TLS

gRPC 生态**强制 HTTP/2**，多数客户端**要求 TLS**（用 HTTP/2 cleartext `h2c`
要显式配置）。Kestrel 生产部署：

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

### 9.2 认证

服务端：gRPC interceptor 或 ASP.NET Core 的 `RequireAuthorization()`。

```csharp
app.MapGrpcIngress().RequireAuthorization("grpc-clients");
```

客户端：通过 CallCredentials / ChannelCredentials 发 JWT 或 mTLS；
桥不参与。

### 9.3 多副本

桥是**无状态的**（连 task map 都没有，因为异步任务状态靠上游自己的
`system.task.status` 维护）。水平扩展平凡。

### 9.4 service mesh / Envoy

gRPC + HTTP/2 is first-class 在 Envoy / Linkerd / Istio。把桥 pod 标注
`appProtocol: grpc`（k8s service）即可走 L7 负载均衡。Service mesh 里
注意**不要**让 mesh 把 `Authorization` 改写（避免和桥的 `AuthHeader` 冲突）。

### 9.5 超时

gRPC 有 **deadline** 概念。客户端设 deadline，`ServerCallContext.CancellationToken`
会在 deadline 到时 cancel 上游 `HttpClient` 调用。两层超时建议：

| 层             | 典型值      |
| -------------- | ----------- |
| gRPC deadline  | 30 s        |
| HttpClient timeout | 25 s    |

让 HTTP 比 gRPC 早一点超时，gRPC 有机会返 `DEADLINE_EXCEEDED`。

---

## 10. 观测

### 10.1 ASP.NET Core 自带

- Kestrel 请求日志（打开 `Logging.LogLevel.Microsoft.AspNetCore = Information`）
- gRPC 的 `ServerCallContext` 里能拿 peer、method 等上下文

### 10.2 OpenTelemetry

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddGrpcClientInstrumentation()
        .AddHttpClientInstrumentation())   // ↑ 上游 HttpClient
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation());
```

这自动打：

- gRPC server 入口 span（每个 RPC 一个）
- 上游 HTTP 出口 span（每个 `/.nwm` / `/invoke` 等一个）
- 按 method 的延迟直方图

### 10.3 traceparent 传递

client 若在 `UpstreamContext.traceparent` 里传 W3C traceparent，桥直接
转发到上游 HTTP header——跨协议 trace 链不断。若 client 没传，
OpenTelemetry HttpClient instrumentation 会自己生成。

---

## 11. 测试

```bash
dotnet test compat/grpc-ingress/tests/LabAcacia.GrpcIngress.Tests/
```

15 个测试覆盖：

- 4 个 RPC 各 happy path
- `Invoke` 的 422 业务失败 → 数据返回而不抛
- `GetManifest` 的 404 → 抛 `NOT_FOUND`
- `ListActions` 的 500 → 抛 `UNAVAILABLE`
- `params_json` 非法 JSON → 抛 `INVALID_ARGUMENT`
- `ctx.upstream` 空 → 抛 `INVALID_ARGUMENT`
- `ctx.upstream` 未注册 → 抛 `NOT_FOUND`
- 超过 `MaxPayloadBytes` → 抛 `RESOURCE_EXHAUSTED`
- `agent_nid` / `idempotency_key` / `traceparent` 透传到上游 header
- 异步 202 → `task_id` 从 body 抽取

测试用自定义 `TestServerCallContext`（避免 `Grpc.Core.Testing` 重依赖）。

---

## 12. 限制

| 类别                        | 状态            | 说明                                                       |
| --------------------------- | --------------- | ---------------------------------------------------------- |
| Server-streaming / bidi     | 未支持          | v0.2 计划加 `InvokeStream`（对应 AlignStream）             |
| Reflection / Descriptor API | 不内置          | 用 `.proto` 生成代码；或自己注册 `services.AddGrpcReflection()` |
| 全局 interceptor            | 不内置          | 调用方自己注入（Kestrel standard path）                    |
| 强类型 proto                | 不内置          | 调用方按 §8 自行叠加                                       |
| `Authorization` 透传        | 不透传          | 用 `NwpUpstream.AuthHeader` 静态注入                       |
| Health check                | 不内置          | 建议配 `Grpc.HealthCheck` package                          |

---

## 13. 常见问题

**Q: 为什么不生成 per-NWP-node 的 `.proto`？**
A: 参见 §1.1。简版：编译期 schema 与 NWP 运行时 schema 冲突。

**Q: bytes 透传会不会效率低？**
A: 比强类型 marshalling 只多一次 `JSON.Parse`。相比 HTTP/1.1 + JSON 的
NWP 原始调用，gRPC 的 HTTP/2 帧复用 + header 压缩（HPACK）收益大得多。
如果极端在乎体积，可以把 NWP 上游切到 MsgPack tier（NWP 支持 `Accept: application/vnd.nps.msgpack`），桥同样透传。

**Q: 一个 NwpBridge 服务能接多个 upstream 吗？**
A: 可以。`Upstreams` 是 list，`UpstreamContext.upstream` 按 name 选。

**Q: gRPC client 怎么知道 upstream 有哪些 action？**
A: 先调 `ListActions`，拿到 `actions_json`（NWP 原始 `/actions` 响应），
在 client 侧解析。配合 `GetManifest` 拿到 `node_type`。

**Q: 能代理 NWP 原生帧（非 HTTP 模式）吗？**
A: 当前不能。桥内部用 `HttpClient` 走 NWP HTTP 模式。原生 NCP 帧直接走
gRPC bytes 携带是可行设计（v0.3+ 考虑），但需要桥理解 NCP 帧头。

**Q: `task_id` 什么时候有值？**
A: 仅当 `http_status == 202` **且** body 是合法 JSON 且顶层有 `task_id`
字段。其它情况空字符串。

---

## 14. 参考

- NWP 规范：[`spec/NPS-2-NWP.md`](../../spec/NPS-2-NWP.md) v0.5
- proto 定义：[`compat/grpc-ingress/src/LabAcacia.GrpcIngress/Protos/nwp_ingress.proto`](https://github.com/labacacia/NPS-gRPC-Ingress/blob/main/src/LabAcacia.GrpcIngress/Protos/nwp_ingress.proto)
- gRPC .NET 文档：https://learn.microsoft.com/aspnet/core/grpc/
- CHANGELOG：[`compat/grpc-ingress/CHANGELOG.md`](https://github.com/labacacia/NPS-gRPC-Ingress/blob/main/CHANGELOG.md)
- 独立仓：[labacacia/NPS-gRPC-Ingress](https://github.com/labacacia/NPS-gRPC-Ingress)
