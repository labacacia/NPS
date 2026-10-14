# NWP Bridge Target Schema

`LabAcacia.NPS.NWP.Bridge` accepts an `ActionFrame` whose `params` contains a
`bridge_target` object. The Bridge Node dispatches the frame to the selected
external protocol and returns a `CapsFrame`.

## Common fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `protocol` | string | yes | One of `http`, `grpc`, `mcp`, `a2a`, or a custom dispatcher protocol. |
| `endpoint` | string | yes | Absolute `http://` or `https://` URI. For gRPC, the path is the full service method, e.g. `/pkg.Service/Method`. |
| `headers` | object | no | String headers forwarded to the external call. |
| `reject_private` | boolean | no | Defaults to `true`; rejects private, link-local, and loopback hosts. |
| `allowed_prefixes` | string or string[] | no | Optional allowlist. The endpoint must start with one of these prefixes. |
| `allow_http` | boolean | no | Defaults to `true`. Set `false` to require HTTPS. |

## HTTP

`HttpBridgeDispatcher` forwards one HTTP request.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `method` | string | `POST` | HTTP method. |
| `content_type` | string | `application/json` | Request body content type. |
| `body` | JSON | absent | Request body. If omitted, `params.body` is used. |

The response row contains `status_code`, `success`, `content_type`, `headers`,
and either `body` for JSON payloads or `body_text`.

## MCP JSON-RPC

`McpBridgeDispatcher` posts JSON-RPC 2.0 to MCP HTTP servers.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `rpc_method` / `method` | string | `tools/call` | JSON-RPC method. |
| `id` | JSON | generated | JSON-RPC id. |
| `rpc_params` / `params` | JSON | `{}` | JSON-RPC params. |

The response row contains `jsonrpc_response`, and copies top-level `result` or
`error` when present.

## A2A JSON-RPC

`A2aBridgeDispatcher` posts JSON-RPC 2.0 to A2A endpoints.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `rpc_method` / `method` | string | `tasks/send` | JSON-RPC method. |
| `id` | JSON | generated | JSON-RPC id. |
| `rpc_params` / `params` | JSON | `{}` | JSON-RPC params. |

## gRPC JSON

`GrpcBridgeDispatcher` sends one unary gRPC request using the JSON gRPC codec:
`content-type: application/grpc+json`.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `grpc_message` / `message` / `body` | JSON | frame `params` or `{}` | Message payload encoded as one gRPC length-prefixed JSON message. |

The response row contains `grpc_status`, `grpc_message`, response headers,
trailers, and `messages[]`. JSON messages are parsed; non-JSON messages are
returned as Base64 strings.

## Example

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
