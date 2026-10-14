# NWP Bridge Target Schema

`LabAcacia.NPS.NWP.Bridge` 接收一个 `ActionFrame`，其中 `params` 携带
`bridge_target` 对象。Bridge Node 会把该 frame 分发到指定外部协议，并返回
`CapsFrame`。

## 通用字段

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `protocol` | string | 是 | `http`、`grpc`、`mcp`、`a2a`，或自定义 dispatcher protocol。 |
| `endpoint` | string | 是 | 绝对 `http://` 或 `https://` URI。gRPC 场景下 path 是完整 service method，例如 `/pkg.Service/Method`。 |
| `headers` | object | 否 | 转发到外部调用的字符串 header。 |
| `reject_private` | boolean | 否 | 默认 `true`；拒绝 private、link-local、loopback host。 |
| `allowed_prefixes` | string 或 string[] | 否 | 可选 allowlist；endpoint 必须以其中之一开头。 |
| `allow_http` | boolean | 否 | 默认 `true`。设为 `false` 时强制 HTTPS。 |

## HTTP

`HttpBridgeDispatcher` 转发一次 HTTP 请求。

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `method` | string | `POST` | HTTP method。 |
| `content_type` | string | `application/json` | 请求 body content type。 |
| `body` | JSON | 无 | 请求 body；未设置时使用 `params.body`。 |

响应 row 包含 `status_code`、`success`、`content_type`、`headers`，以及
JSON payload 的 `body` 或普通文本的 `body_text`。

## MCP JSON-RPC

`McpBridgeDispatcher` 向 MCP HTTP server 发送 JSON-RPC 2.0。

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `rpc_method` / `method` | string | `tools/call` | JSON-RPC method。 |
| `id` | JSON | 自动生成 | JSON-RPC id。 |
| `rpc_params` / `params` | JSON | `{}` | JSON-RPC params。 |

响应 row 包含 `jsonrpc_response`，并在存在时复制顶层 `result` 或 `error`。

## A2A JSON-RPC

`A2aBridgeDispatcher` 向 A2A endpoint 发送 JSON-RPC 2.0。

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `rpc_method` / `method` | string | `tasks/send` | JSON-RPC method。 |
| `id` | JSON | 自动生成 | JSON-RPC id。 |
| `rpc_params` / `params` | JSON | `{}` | JSON-RPC params。 |

## gRPC JSON

`GrpcBridgeDispatcher` 使用 JSON gRPC codec 发送一次 unary gRPC 请求：
`content-type: application/grpc+json`。

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `grpc_message` / `message` / `body` | JSON | frame `params` 或 `{}` | 按 gRPC length-prefix 编码的一条 JSON message。 |

响应 row 包含 `grpc_status`、`grpc_message`、response headers、trailers 和
`messages[]`。JSON message 会被解析；非 JSON message 以 Base64 string 返回。

## 示例

```json
{
  "action_id": "bridge.dispatch",
  "params": {
    "bridge_target": {
      "protocol": "mcp",
      "endpoint": "https://mcp.example.test/rpc",
      "allowed_prefixes": ["https://mcp.example.test/"],
      "rpc_method": "tools/call"
    },
    "params": {
      "name": "orders.lookup",
      "arguments": { "order_id": "42" }
    }
  }
}
```
