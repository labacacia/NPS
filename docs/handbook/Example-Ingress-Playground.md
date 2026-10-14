# Example: Ingress Playground

**Status:** ✅ Reviewed for v1.0.0-alpha.19 release

**Repo:** `labacacia/NPS-Examples`, directory: `ingress-playground/` (source in NPS-Dev `demos/ingress-playground/`)

This demo shows three ingress protocols — MCP, A2A, and gRPC — each translating external requests into NPS frames and forwarding them to the same upstream NWP Action Node. One process, one business function, three simultaneous protocol facades.

---

## What This Example Shows

The same logical NWP action — `greetings.hello(name)` — is reached simultaneously through three different compatibility adapters:

```
                  ┌──────────────────────────────┐
                  │  NWP Action Node  :17481     │
                  │  greetings.hello(name)       │
                  └──────────────▲───────────────┘
                                 │ POST /invoke  (ActionFrame)
       ┌───────────────────────────┼────────────────────────────┐
       │                           │                            │
┌──────┴──────────┐        ┌───────┴──────────┐         ┌───────┴──────────┐
│ MCP Ingress     │        │ A2A Ingress      │         │ gRPC Ingress     │
│   :17482/mcp    │        │   :17483/a2a     │         │   :17484 h2c     │
│ tools/call      │        │ tasks/send       │         │ Invoke RPC       │
└─────────────────┘        └──────────────────┘         └──────────────────┘
       ▲                           ▲                            ▲
       └────────────────┬──────────┘                            │
                        │                                       │
                ┌───────┴────────────────────────┐             │
                │   single .NET client           │─────────────┘
                │   (Program.cs)                 │
                └────────────────────────────────┘
```

The three adapters are shape-translators only — they rewrite the envelope and forward unchanged NPS `ActionFrame` payloads. The same upstream `IActionNodeProvider` implementation handles every call; only the outer wire format differs.

---

## Naming Note: Ingress vs Bridge

The three NuGet packages are named `LabAcacia.McpIngress`, `LabAcacia.A2aIngress`, and `LabAcacia.GrpcIngress` — not `*Bridge`. This naming was established by [CR-0001](./CR-Process.md) in alpha.3:

- **Ingress** (external → NPS): these packages translate inbound MCP / A2A / gRPC requests into NPS frames. Direction: external protocol → NWP.
- **Bridge Node** (NPS → external): a NWP node type that translates *outbound* NPS frames to external protocols. Direction: NWP → external protocol.

The repository names were similarly renamed: `NPS-mcp-ingress`, `NPS-a2a-ingress`, `NPS-grpc-ingress`. The captured demo output in the README still shows `bridge-playground` in `anchor_ref` URLs because it was recorded before the rename; new runs show `ingress-playground`.

---

## What Each Adapter Does

| Adapter | What it exposes | How the NPS payload is carried |
|---------|-----------------|-------------------------------|
| `LabAcacia.McpIngress` | MCP 2024-11-05 server: `tools/list`, `tools/call` | CapsFrame serialized into `content[{type:"text", text:"..."}]` |
| `LabAcacia.A2aIngress` | Google A2A v0.2 server: `tasks/send`, `tasks/get` | CapsFrame inlined as `artifacts[].parts[{type:"data", data:…}]` |
| `LabAcacia.GrpcIngress` | gRPC service `NwpIngress.Invoke` (h2c in demo) | CapsFrame serialized to JSON → passed as `bytes body_json` in `InvokeResponse` |

Because the adapters only rewrite the envelope, the same `ActionFrame` reaches the upstream node every time. The same `CapsFrame` (byte-identical except for a `via` tag the provider writes to identify the channel) comes back from all three.

---

## How to Run

```bash
git clone https://github.com/labacacia/nps.git
cd nps
dotnet run --project demos/ingress-playground
```

Requires .NET 10 SDK. Four Kestrel hosts (the upstream Action Node + 3 ingress adapters) and the client all run in one process; nothing binds beyond `127.0.0.1`.

---

## Round-Trip Walk-Through

### MCP Request (Scene A)

The client sends `tools/call` to `:17482/mcp` with tool name `greetings__greetings_hello`:

```
POST http://127.0.0.1:17482/mcp
```

`LabAcacia.McpIngress` translates this into a `POST /invoke` `ActionFrame` and forwards it to the upstream at `:17481`. The upstream processes `greetings.hello("Ada")` and returns a `CapsFrame`. The MCP Ingress wraps that CapsFrame into the MCP `content[{type:"text", text:"..."}]` structure:

```json
{
  "jsonrpc": "2.0", "id": 2,
  "result": {
    "content": [
      { "type": "text",
        "text": "{\"frame_type\":4,\"preferred_tier\":1,\"anchor_ref\":\"nps://demo/ingress-playground/anchors/greeting/v1\",\"count\":1,\"data\":[{\"greeting\":\"Hello, Ada!\",\"via\":\"MCP\",\"upstream_node\":\"NWP Action Node — ingress-playground upstream\"}],\"cgn_est\":0}"
      }
    ],
    "isError": false
  }
}
```

The inner payload is a standard NPS CapsFrame (`frame_type: 4`).

### A2A Request (Scene B)

The client sends `tasks/send` to `:17483/a2a` with `skillId: "greetings.hello"`. The A2A Ingress translates this to an `ActionFrame` and forwards it upstream. The CapsFrame comes back wrapped as an A2A `artifacts[].parts[{type:"data", data:…}]`:

```json
{
  "jsonrpc": "2.0", "id": 1,
  "result": {
    "id": "22f0b799-...", "status": { "state": "completed", … },
    "artifacts": [
      { "name": "greetings.hello",
        "parts": [
          { "type": "data",
            "data": {
              "frame_type": 4,
              "anchor_ref": "nps://demo/ingress-playground/anchors/greeting/v1",
              "count": 1,
              "data": [ { "greeting": "Hello, Ada!", "via": "A2A", … } ],
              "cgn_est": 0
            }
          }
        ]
      }
    ]
  }
}
```

### gRPC Request (Scene C)

The client calls `NwpIngress.Invoke` over h2c (plaintext HTTP/2) to `:17484`. The gRPC Ingress does byte-level passthrough: `InvokeResponse.body_json` carries the serialized CapsFrame unchanged:

```json
{
  "frame_type": 4,
  "anchor_ref": "nps://demo/ingress-playground/anchors/greeting/v1",
  "count": 1,
  "data": [ { "greeting": "Hello, Ada!", "via": "gRPC", … } ],
  "cgn_est": 0
}
```

### What to Observe

The three `via` values (`MCP` / `A2A` / `gRPC`) are the **only** material difference between the three responses. The `anchor_ref` is identical across all three — which means an Agent could cache the response once and reuse it regardless of which ingress delivered it.

---

## Expected Successful Output

When all three calls succeed, the console shows:

```
Scene A — MCP  → HTTP 200 OK  (content text wrapping CapsFrame)
Scene B — A2A  → HTTP 200 OK  (artifacts data wrapping CapsFrame)
Scene C — gRPC → gRPC OK      (body_json CapsFrame passthrough)
All three channels verified.
```

---

## Snapshot Refresh

The captured output in the demo README was recorded on 2026-04-21. Output snapshots must be refreshed at each suite version change. The `cgn_est` field rename (from `token_est` in older runs) in alpha.5.2 breaks older snapshots — regenerate them with `dotnet run --project demos/ingress-playground` after any protocol change that touches CapsFrame fields.

---

## Demo-Only Configuration

The demo makes two deliberate relaxations for local running:

- **Plaintext h2c for gRPC.** The gRPC ingress listens on `http://` so the demo runs without a certificate. The `System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport` AppContext switch is set only in this demo. Production gRPC MUST use TLS.
- **No NIP.** Bridge calls carry no `X-NWP-Agent` header, no certificate chain, no capability check. The upstream accepts anonymous calls because `ActionNodeOptions.RequireAuth` defaults to `false`.

---

## Layout

```
demos/ingress-playground/
├── Program.cs                        # 4 hosts + 3 client scenes (A/B/C)
├── HostBuilders.cs                   # Kestrel factories: upstream + 3 ingress adapters
├── GreetingsProvider.cs              # IActionNodeProvider impl for greetings.hello
├── Protos/nwp_bridge_client.proto    # Local copy of the ingress proto (client-only)
└── NPS.Demo.IngressPlayground.csproj
```

---

## Limitations

- **Single action only.** MCP `resources/*` mapping (Memory Nodes), A2A `artifacts[]` with multiple parts, and the gRPC `Query` RPC are not shown. See `compat/{mcp,a2a,grpc}-ingress/` tests for the full surface.
- **In-process hosts.** All four Kestrel hosts run in one process. Production bridges are independently deployed.
- **Synchronous only.** `greetings.hello` is `Async=false`; the 202 path (A2A submitted→working→completed polling, gRPC async) is not exercised.

---

## Related Pages

- [SDK Building a Bridge Node](./SDK-Building-a-Bridge-Node.md) — how to implement an NPS→external Bridge Node (the inverse direction)

---

*Last reviewed at suite version: v1.0.0-alpha.19 release*
