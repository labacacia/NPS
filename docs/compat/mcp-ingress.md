[English Version](./mcp-ingress.en.md) | 中文版

# MCP Ingress 详解

> **历史指南。** 独立 ingress 仓库已经归档。当前 bridge 实现权威为
> [NPS-SDK-DotNet](https://github.com/labacacia/NPS-SDK-DotNet) 中的
> `LabAcacia.NPS.NWP.Bridge`；新部署应以其当前 API 与安全说明为准。

> `LabAcacia.McpIngress` v1.0.0-alpha.16 — 把一个或多个 **NWP 节点** 暴露成一个
> [**Model Context Protocol (MCP)**](https://modelcontextprotocol.io) 服务端。
> 适配规范：MCP `2024-11-05`（JSON-RPC 2.0 over HTTP POST）。

本文补充 [`compat/mcp-ingress/README.md`](https://github.com/labacacia/NPS-MCP-Ingress/blob/main/README.md) 未覆盖的细节：
协议映射的语义、headers 透传、错误边界、部署关注点、实战接入示例。

- 代码仓：`compat/mcp-ingress/`（monorepo）/ `labacacia/NPS-MCP-Ingress`（独立）
- 面向读者：要把已有的 MCP 客户端（Claude Desktop、IDE Agent、Anthropic SDK）
  接到 NPS 生态的运维/开发人员。

---

## 1. 为什么需要桥

MCP 是 Anthropic 推出的**工具/资源调用协议**，生态内已有大量客户端。
NPS 是**协议族**，Node 自描述（`/.nwm` + `/.schema` + `/actions`）、
运行时 Schema（AnchorFrame）。两者的语义是可以对齐的，但 wire format 不同：

| 方面       | MCP 2024-11-05                       | NPS NWP v0.13                                |
| ---------- | ------------------------------------ | ------------------------------------------- |
| 传输       | JSON-RPC 2.0 over HTTP POST           | HTTP 模式 + 原生帧模式                       |
| 工具声明   | `tools/list` 返回 inputSchema JSON   | `/actions` + AnchorFrame（运行时）           |
| 资源声明   | `resources/list` / `resources/read`   | Memory Node `/query`                        |
| 传输凭证   | 由客户端层自行处理                    | `X-NWP-Agent` + NIP 证书                     |

桥的责任是**让 MCP 客户端无需改动**即可读取 NWP Memory Node、调用 Action Node。

---

## 2. 架构

```
┌──────────────────┐   JSON-RPC 2.0   ┌────────────────────┐   HTTP/JSON   ┌─────────────────┐
│  MCP Client      │  ───────────→   │  LabAcacia         │  ──────────→ │  NWP Memory /   │
│  (Claude Desktop,│                  │  .McpIngress        │              │  Action Node    │
│   IDE agents, …) │  ←───────────   │  (ASP.NET Core)    │  ←────────── │                 │
└──────────────────┘                  └────────────────────┘               └─────────────────┘
                                        ▲
                                        │ 配置
                                        │
                                  NwpUpstream[]
```

每个 `NwpUpstream` 映射成：

- 一个 MCP **资源 URI 命名空间**：`nwp://<name>/`
- 若 `node_type == "memory"` → 进入 `resources/list` 响应
- 若 `node_type ∈ {"action","complex"}` → 每个 NWP action 成为一个 MCP **tool**

`NwpUpstreamClient` 持有一个 `HttpClient`（由 `IHttpClientFactory` 管理，
命名 `"mcp-bridge:{name}"`），每个上游各一个。

---

## 3. 配置表面

### 3.1 `McpIngressOptions`

| 字段                | 默认值                | 含义                                                         |
| ------------------- | --------------------- | ------------------------------------------------------------ |
| `ServerName`        | `LabAcacia.McpIngress` | `initialize` 返回的 `serverInfo.name`                        |
| `ServerVersion`     | `1.0.0-alpha.16`       | `initialize` 返回的 `serverInfo.version`                     |
| `Upstreams`         | *（必填，≥1 条）*      | 上游 NWP 节点列表                                             |
| `ResourceReadLimit` | `100`                 | `resources/read` 时传给 Memory Node `/query` 的 `limit` 字段 |

### 3.2 `NwpUpstream`

| 字段         | 必填   | 含义                                                                   |
| ------------ | ------ | ---------------------------------------------------------------------- |
| `Name`       | 是     | 唯一；进入 `nwp://<name>/` URI 和 `<name>__<action>` tool name         |
| `BaseUrl`    | 是     | NWP 节点根 URL（无尾斜线）；`/.nwm` `/actions` `/query` `/invoke` 挂此  |
| `AgentNid`   | 否     | 发到上游的 `X-NWP-Agent` header                                        |
| `AuthHeader` | 否     | 原样作为 `Authorization` header                                        |

**Name 唯一性守卫**：`AddMcpIngress` 启动时会对 `Upstreams` 做去重检查，
重复直接抛 `InvalidOperationException`。同样若列表为空也抛异常——
没有任何上游的桥没有意义。

### 3.3 DI + 路由

```csharp
builder.Services.AddMcpIngress(o => { /* options */ });
app.MapMcpIngress("/mcp"); // 默认路径 /mcp
```

`MapMcpIngress` 返回 `IEndpointConventionBuilder`，可继续叠加：

```csharp
app.MapMcpIngress("/mcp")
   .RequireAuthorization("mcp-clients")
   .WithMetadata(new CorsPolicyMetadata(/*...*/));
```

---

## 4. 协议映射（逐方法）

### 4.1 `initialize`

**请求**

```json
{ "jsonrpc":"2.0", "id":1, "method":"initialize",
  "params":{ "protocolVersion":"2024-11-05", "capabilities":{}, "clientInfo":{...} } }
```

**响应**（`McpInitializeResult`）

```json
{ "jsonrpc":"2.0", "id":1,
  "result":{
    "server_info": { "name":"...", "version":"..." },
    "capabilities": { "resources":{}, "tools":{} }
  }
}
```

桥**不与上游通信**；仅回送配置。`capabilities` 固定只包含 `resources` + `tools`，
**不含** `prompts`、`sampling`、`roots` 等——v0.1 不实现这些。

### 4.2 `resources/list`

**每调用一次**就把所有配置的上游**全扫一遍**：`GET /.nwm` → 过滤
`node_type == "memory"`。

**返回**：每个满足的上游一条资源。

```json
{ "resources":[
    { "uri":"nwp://products/",
      "name":"<display_name or 'products'>",
      "description":"NWP Memory Node 'products' — use resources/read to query.",
      "mime_type":"application/json" }
] }
```

**性能提示**：如果 upstreams 很多（≥20），每次 list 请求都会串行触发
N 次 `/.nwm` 调用。生产环境可以：

- 用 HttpClient 级别的短 TTL 响应缓存（例如 60 秒）
- 或在 `NwpUpstreamClient` 外再包一层，把 `/.nwm` 缓起来
  —— 目前桥不内置 NWM 缓存，是**有意的**（避免跟节点方的
  AnchorFrame TTL 策略打架）

### 4.3 `resources/read`

**请求参数**

```json
{ "uri":"nwp://products/" }
```

URI 必须是 `nwp://` scheme；host 段即 `Upstream.Name`。

**映射**：`POST <BaseUrl>/query`，body 为

```json
{ "limit": <ResourceReadLimit, 默认100> }
```

请求 content-type 设为 `application/nwp-frame`（NWP 约定）。

**响应**：原样把上游 JSON body 回传给 MCP 客户端作为**单个 text content**：

```json
{ "contents":[ {
    "uri":"nwp://products/",
    "mime_type":"application/json",
    "text":"<upstream body 原样>"
} ] }
```

注意：MCP client 拿到的是 **字符串**，不是结构化 JSON —— 这是 MCP 对
content 的约定，客户端自行再解。

### 4.4 `tools/list`

对每个 `node_type ∈ {"action","complex"}` 的上游：

1. `GET /.nwm` 取 node_type
2. `GET /actions` 取 action 清单

每个 action 生成一个 MCP tool：

| 字段          | 值                                                              |
| ------------- | --------------------------------------------------------------- |
| `name`        | `<upstream>__<action_id_dot_to_underscore>`                     |
| `description` | 若 action 声明了 `description` 就用，否则 `null`                |
| `inputSchema` | `{ "type":"object", "additionalProperties": true }`（见下）    |

**为什么不透出 AnchorFrame 作为 inputSchema？**
AnchorFrame 是 NWP 的**运行时** schema，有 `token_budget` / `nid_bindings` /
MsgPack tier 等 MCP 客户端无法理解的字段。v0.1 选择让 MCP 客户端"自由填参"，
**由 NWP 节点校验**并返回 ErrorFrame —— 语义一致，且避免 schema 翻译失真。
未来 `v0.2` 计划在保守子集上生成 JSON Schema（仅映射可无损的字段）。

### 4.5 `tools/call`

**请求**

```json
{ "name":"orders__orders_create", "arguments": { "sku":"ABC-123", "qty":1 } }
```

**解析**：以首次出现的 `__` 分割，左边是 upstream 名，右边把 `_` 还原成 `.`
得到 `action_id`。

**映射**：`POST <BaseUrl>/invoke` with

```json
{ "action_id":"orders.create", "params": { "sku":"ABC-123", "qty":1 } }
```

**响应原样回送**，并设置 `isError = !resp.IsSuccessStatusCode`：

```json
{ "content":[ { "type":"text", "text":"<raw upstream body>" } ],
  "is_error": false }
```

**重要**：MCP 的 `is_error` 只对 **HTTP 非 2xx** 标记 true。业务层 2xx 成功
但 `status != "completed"` 的 CapsFrame 仍然 `is_error=false`——客户端要
自己读 JSON 判断。

### 4.6 `ping`

无上游交互；桥直接回 `result: {}`。用于 keepalive。

---

## 5. Header 透传

上游每次请求附带：

| Header          | 来源                                | 可选 |
| --------------- | ----------------------------------- | ---- |
| `X-NWP-Agent`   | `NwpUpstream.AgentNid`              | 是   |
| `Authorization` | `NwpUpstream.AuthHeader`（原样）    | 是   |
| `Content-Type`  | `application/nwp-frame`（仅 POST） | 恒定 |

MCP **不透传**客户端自己的 `X-NWP-Agent`——桥是服务端，对上游而言它**就是**
Agent。如果需要转发最终 MCP caller 的身份，要求客户端在 `arguments` 里显式
放 `actor_nid` 字段并由 Action Node 读取。

---

## 6. 错误映射

### 6.1 JSON-RPC 标准错误

| Code     | 触发条件                                                |
| -------- | ------------------------------------------------------- |
| `-32700` | HTTP body 不是合法 JSON                                  |
| `-32600` | 合法 JSON，但不是 JSON-RPC 2.0 请求（缺 `method`）      |
| `-32601` | 方法名不是桥认识的 6 种之一                             |
| `-32602` | 参数校验失败（如 `resources/read` 缺 `uri`）            |
| `-32603` | 任何没被分类的异常（已打 `ILogger.LogError`）           |

### 6.2 桥特有错误

| Code     | 含义                                                               |
| -------- | ------------------------------------------------------------------ |
| `-32000` | 上游返回非 2xx（用于 `resources/read`；`tools/call` 不用此，见下） |
| `-32001` | `resources/read` 的 URI host 不是配置的 upstream 名                |
| `-32002` | `tools/call` 的工具名 upstream 前缀不匹配                          |

### 6.3 `tools/call` 与其他 RPC 的错误语义差别

- `resources/read` 上游失败 → **抛** `-32000` JSON-RPC 错误
- `tools/call` 上游失败 → **返回** `is_error=true` 的 result，**不抛错**

差别的原因：MCP 的 tool contract 规定工具执行失败**应当**通过 `is_error=true`
回传给模型侧去"看到"并自我纠正；而资源读取失败是**基础设施错误**，模型无法
处理，应当作为 RPC 错误让客户端层显式处理。

### 6.4 Notification（无 `id`）

按 JSON-RPC 2.0 规范，桥对 notification 请求回 HTTP `204 No Content`，
**不产生响应体**。方法不认识也不会回错（规范要求）。

---

## 7. 异步任务

MCP `2024-11-05` 的 tool call 是**同步**语义——客户端期望一次 POST 拿到结果。
但 NWP Action Node 支持 `async=true` 的动作，返回 `202 Accepted + task_id`。

**v0.1 的处理**：桥把 202 的 body 原样透传，客户端收到 `is_error=false` 但
content 里是 task 的 ack。客户端**自行负责轮询** —— 把 `system.task.status`
当作另一个 tool 去调用。

示例流程：

1. `tools/call` name=`orders__orders_bulk_import` → 收到 task_id
2. 客户端循环 `tools/call` name=`orders__system_task_status` with `{"task_id":"..."}`
3. 业务完成后读结果（通常从 Memory Node `resources/read`）

**不满意**？`v0.2` 计划引入 MCP **progress notifications**（规范 2025 版
新增），让桥把 NWP 异步进度主动推回 MCP 客户端。但 2024-11-05 客户端无法
直接消费 progress，兼容性是阻塞项。

---

## 8. 实战接入

### 8.1 Claude Desktop

Claude Desktop 的 `config.json`（macOS: `~/Library/Application Support/Claude/`）：

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

或使用 HTTP server transport（Claude Desktop 0.8+）：

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
    # tools 里包含 products__*, orders__* 等
    result = await mcp.call_tool("orders__orders_create",
                                  arguments={"sku":"ABC-123","qty":1})
    print(result.content[0].text)
```

### 8.3 自研 JSON-RPC 客户端

直接 POST 到 `/mcp`：

```bash
curl -X POST https://bridge.example.com/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

---

## 9. 部署

### 9.1 TLS 与认证

桥**本身不做**终端 TLS 或客户端认证。推荐：

- 外层放 NGINX / Envoy / Azure Front Door 做 TLS 终结 + mTLS 或 API Key
- 或在 Kestrel 上配置 HTTPS 证书 + 用 `RequireAuthorization("mcp-clients")`

上游方向的凭证通过 `NwpUpstream.AuthHeader` 注入——**静态值**，
不支持 per-request 覆盖。动态凭证场景请在外层包装 `HttpMessageHandler`
做 token 刷新。

### 9.2 多实例

桥是**无状态的**（除短暂的 HTTP 连接池）——可以任意水平扩展。
`resources/list` 对每个 upstream 做 `/.nwm` 调用，副本数 × upstreams 数会
放大读取压力；上游方建议开 Last-Modified / ETag 缓存。

### 9.3 观测

桥用 `ILogger<McpIngress>` 输出：

- `LogError` on unhandled exceptions（方法名 + 堆栈）
- 不打 info 级别日志 —— ASP.NET Core request pipeline 已有

若要加 metrics，可以挂 `IHttpClientFactory` 的 DelegatingHandler，
用 Meter API 计 HTTP 上游延迟 / 状态码分布。

### 9.4 资源消耗

每个 JSON-RPC call 一次 POST，请求体通常 < 1 KiB，响应体视
`ResourceReadLimit` 而定（默认 100 条，按业务形态从 10 KiB 到几 MiB）。
`HttpClient` 默认超时 100 秒——生产环境显式设短（30 秒）并配合
`NwpUpstream.BaseUrl` 的超时策略。

---

## 10. 测试

```bash
dotnet test compat/mcp-ingress/tests/LabAcacia.McpIngress.Tests/
```

测试用 `HttpMessageHandler` stub 打假上游，**不涉及网络**。

自测覆盖：
- initialize
- resources/list：memory / non-memory / 混合
- resources/read：URI 合法性 / 上游失败 / 成功透传
- tools/list：action + complex 节点
- tools/call：同步 2xx / 同步 4xx（is_error=true）/ 非法 name
- notification（无 id）返回 204

---

## 11. 限制（v0.1）

| 类别             | 状态              | 说明                                                    |
| ---------------- | ----------------- | ------------------------------------------------------- |
| Streaming        | 未支持            | MCP 2025 新增的 progress notifications 计划 v0.2       |
| Prompts          | 未支持            | 桥不翻译 NWP 到 MCP prompt                             |
| Sampling         | 永不支持          | NPS 不提供模型采样                                      |
| Resource 订阅    | 未支持            | `resources/subscribe`                                  |
| Anchor Node      | 未支持            | `node_type: "anchor"` 不在 tools/list 中出现           |
| 多租户隔离       | 未支持            | 所有 MCP client 共享同一套 upstreams 配置              |
| NWM 缓存         | 不内置            | 由上游节点的 HTTP cache 策略决定                       |
| 对称加密         | 不处理            | NIP 证书 / NCP 握手在上游 NWP 层自己管                 |

---

## 12. 常见问题

**Q: tool name 里的 `__` 会不会和 action_id 自身的 `_` 冲突？**
A: 以**第一次出现**的 `__` 分割 upstream 名。action_id 内部的 `_`
来自规范（动作 id 允许 `[a-z0-9._-]`），桥把 `.` → `_` 单向映射。只要
upstream 名本身不含 `__` 就安全。建议 upstream 名用短 kebab-case。

**Q: 我能一个 MCP 客户端同时接多个桥吗？**
A: 可以。Claude Desktop 的 `mcpServers` 是 map，每个条目一个桥。tool
名的 upstream 前缀保证不冲突。

**Q: 上游返回 200 但 body 是 ErrorFrame 怎么办？**
A: `tools/call` 仍然 `is_error=false`（因为 HTTP 状态是 200），但 text
content 里是 ErrorFrame JSON。客户端负责解析 `{"frame_type":"error",...}`。
未来 v0.2 会扫 frame_type 并翻译。

**Q: 怎么让 MCP 客户端把 NID 发到 NWP 节点？**
A: 目前无法。桥以自己的 `AgentNid` 调上游。如果业务需要区分 MCP caller
的真实身份，让上游在 `arguments` 里声明 `actor_nid`，Action Node 实现端
自己读。

---

## 13. 参考

- NWP 规范：[`spec/NPS-2-NWP.md`](../../spec/NPS-2-NWP.md) v0.5
- MCP 规范：https://spec.modelcontextprotocol.io/2024-11-05/
- CHANGELOG：[`compat/mcp-ingress/CHANGELOG.md`](https://github.com/labacacia/NPS-MCP-Ingress/blob/main/CHANGELOG.md)
- 独立仓：[labacacia/NPS-MCP-Ingress](https://github.com/labacacia/NPS-MCP-Ingress)
