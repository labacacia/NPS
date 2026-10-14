[English Version](./a2a-ingress.en.md) | 中文版

# A2A Ingress 详解

> **历史指南。** 独立 ingress 仓库已经归档。当前 bridge 实现权威为
> [NPS-SDK-DotNet](https://github.com/labacacia/NPS-SDK-DotNet) 中的
> `LabAcacia.NPS.NWP.Bridge`；新部署应以其当前 API 与安全说明为准。

> `LabAcacia.A2aIngress` v1.0.0-alpha.16 — 把**单个 NWP Action / Complex /
> Anchor Node** 暴露成一个符合 [**Google Agent-to-Agent (A2A) v0.2**](https://github.com/google/A2A)
> 规范的 Agent。JSON-RPC 2.0 over HTTP POST，外加 `GET /.well-known/agent.json`。

本文补充 [`compat/a2a-ingress/README.md`](https://github.com/labacacia/NPS-A2A-Ingress/blob/main/README.md) 未覆盖的细节：
AgentCard 合成规则、skill 选择算法、异步任务生命周期映射、多副本部署下的 task
状态存储、A2A ↔ NWP 状态语义差异。

- 代码仓：`compat/a2a-ingress/`（monorepo）/ `labacacia/NPS-A2A-Ingress`（独立）
- 面向读者：想把已有的 A2A 生态（Google ADK、A2A-speaking peer agents）接到
  NPS 节点的运维/开发人员。

---

## 1. 为什么需要桥

A2A 的设计前提是 **"一个 agent 一个 AgentCard"**——对外以 skill list 暴露能力。
NPS 的 NWP 节点天然就是这种粒度：一个 Action Node 有若干 action，每个 action
都有声明式元数据。桥的工作本质是：

| A2A 概念            | NPS 映射                                                          |
| ------------------- | ----------------------------------------------------------------- |
| AgentCard           | `/.nwm` + `/actions` 合并                                         |
| Skill               | 一个 NWP `action_id`                                              |
| Task                | `/invoke` 的一次调用（同步或异步）                                |
| Task State Machine  | NWP `system.task.status` 的 `pending/running/completed/failed/cancelled` |
| Task Artifact       | CapsFrame 的 `data` 体或异步任务的 `result`                       |

**与 MCP Ingress 的重大差别**：A2A 一个 AgentCard 对应**一个上游**（一对一）；
MCP 一个 server 可以挂**多个上游**（一对多）。这是协议语义决定的：A2A
AgentCard 是 agent 身份；把多个 NWP 节点塞一个 card 会破坏 agent identity
的语义。

---

## 2. 架构

```
┌──────────────────┐  GET /.well-known/agent.json  ┌────────────────────┐  GET /.nwm + /actions  ┌──────────────────┐
│  A2A peer        │ ────────────────────────────→ │  LabAcacia         │ ────────────────────→  │  NWP Action /    │
│  (Google ADK,    │                                │  .A2aIngress        │                         │  Complex /       │
│   other agents)  │  JSON-RPC 2.0 POST /a2a        │  (ASP.NET Core)    │  POST /invoke            │  Anchor Node     │
│                  │ ←────────────────────────────  │                    │ ←────────────────────── │                  │
└──────────────────┘                                └────────────────────┘                         └──────────────────┘
                                                      │
                                                      │ 内存中：
                                                      │   Dict<a2a_task_id, UpstreamTaskBinding>
                                                      ▼
```

- 单上游，单 AgentCard
- 内存中保存 `{a2a_task_id → (action_id, upstream_task_id)}` 以支持 `tasks/get` / `tasks/cancel`
- 状态转换发生在**读取时**（lazy），而不是推送

---

## 3. 配置表面

### 3.1 `A2aIngressOptions`

| 字段                    | 默认                                          | 含义                                                              |
| ----------------------- | --------------------------------------------- | ----------------------------------------------------------------- |
| `AgentName`             | `NPS A2A Ingress`                              | AgentCard `name`                                                  |
| `AgentDescription`      | `null`（回落到上游 `display_name`）           | AgentCard `description`                                           |
| `AgentVersion`          | `0.1.0`                                       | AgentCard `version`（**对外 agent 版本**，不是桥自身 nuget 版本） |
| `PublicUrl`             | `null`（自动从请求 URL 派生）                 | AgentCard `url` 字段 — A2A client 投递 task 的端点                |
| `ProviderOrganization`  | `LabAcacia / INNO LOTUS PTY LTD`              | AgentCard `provider.organization`                                 |
| `ProviderUrl`           | `https://github.com/labacacia/nps`            | AgentCard `provider.url`                                          |
| `DocumentationUrl`      | `null`                                        | AgentCard `documentationUrl`                                      |
| `AuthSchemes`           | `[]`                                          | AgentCard `authentication.schemes`                                |
| `Upstream`              | *（必填）*                                     | 单个上游 NWP 节点                                                  |

### 3.2 `A2aUpstream`

| 字段         | 必填 | 含义                                       |
| ------------ | ---- | ------------------------------------------ |
| `BaseUrl`    | 是   | 上游根 URL                                 |
| `AgentNid`   | 否   | `X-NWP-Agent` header                       |
| `AuthHeader` | 否   | `Authorization` header（原样）             |

### 3.3 DI + 路由

```csharp
builder.Services.AddRouting();
builder.Services.AddA2aIngress(o => { /* options */ });

app.UseRouting();
app.UseEndpoints(e => e.MapA2aIngress());
// 注册两个端点：
//   GET  /.well-known/agent.json     → AgentCard
//   POST /a2a                        → JSON-RPC
```

`MapA2aIngress()` 可以重写 JSON-RPC 路径：

```csharp
app.MapA2aIngress(jsonRpcPath: "/rpc/a2a");
```

但 `/.well-known/agent.json` 是**规范规定的**路径，不可改。

---

## 4. AgentCard 合成规则

### 4.1 数据源

每次 `GET /.well-known/agent.json` 调用：

1. `GET <upstream>/.nwm` —— 取 `display_name`（用作 description fallback）
2. `GET <upstream>/actions` —— 枚举 `actions`，每条成一个 skill

**不缓存**——每次请求都实时打上游。A2A 客户端预期 AgentCard 相对稳定，
但实时查询保证 NWP schema 更新后立即反映。生产环境可在前端加 60 秒的
HTTP cache-control。

### 4.2 合成逻辑

```
A2aAgentCard:
  name              = options.AgentName
  description       = options.AgentDescription ?? nwm.display_name
  url               = options.PublicUrl ?? <从当前 request 派生>
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

### 4.3 URL 解析的重要性

`AgentCard.url` 是 A2A client 接下来 POST `tasks/send` 的地址。
两种模式：

- **显式 PublicUrl**：配置 `o.PublicUrl = new Uri("https://public.example/a2a")`
- **隐式**：桥从入站 request 的 `Scheme + Host + PathBase + /a2a` 拼接

生产**必须**显式配置 PublicUrl，否则 reverse proxy 后面拿到的 request 可能
是内网地址。

---

## 5. Skill 选择算法（`tasks/send` 的核心）

A2A task 没有"目标 skill"字段——A2A 设计里 agent 自己理解意图。
但桥需要**确定性地**挑出一个 NWP action。查找顺序：

1. `params.metadata.skillId`（+ 可选 `params.metadata.params`）
2. `message.metadata.skillId`（+ 可选 `message.metadata.params`）
3. `message.parts` 中第一个 `type:"data"` part 的对象里，按键名
   `skillId` / `skill_id` / `action_id` 查找

键名变体原因：A2A 生态 camelCase 与 snake_case 都有在用；都兼容。

若三处都拿不到 → JSON-RPC 错误 `-32602` with 指引消息。

### 5.1 参数来源

| 来源                       | 参数在哪                                |
| -------------------------- | --------------------------------------- |
| metadata 携带 skillId      | `metadata.params`（如果没有就空对象）   |
| data part 携带 skillId     | 优先 `data.params`；否则删去 skill-id 键后其余字段 |

### 5.2 举例

**方案 A — metadata 显式**

```json
{ "jsonrpc":"2.0","id":1,"method":"tasks/send","params":{
    "id":"task-001",
    "message":{ "role":"user", "parts":[ {"type":"text","text":"Create order"} ] },
    "metadata":{ "skillId":"orders.create", "params":{"sku":"ABC","qty":1} }
} }
```

**方案 B — data part**

```json
{ "jsonrpc":"2.0","id":1,"method":"tasks/send","params":{
    "id":"task-002",
    "message":{ "role":"user", "parts":[
      { "type":"data", "data":{ "skillId":"orders.create", "sku":"ABC", "qty":1 } }
    ] }
} }
```

两种方案在桥里都合法；**方案 A 更推荐**（意图显式，便于审计 / 配额）。

---

## 6. Task 生命周期映射

### 6.1 同步（上游 200）

- NWP `/invoke` 返回 CapsFrame
- 桥回送 `A2aTask{ status: completed, artifacts: [ {name: action_id, parts: [{type: data, data: <body>}]} ] }`

### 6.2 异步（上游 202）

- NWP 返回 `{ task_id, status: "pending", poll_url? }`
- 桥在内存 map 写入 `{ a2a_task_id: <A2A id>, upstream_task_id: <从 body 解> }`
- 回送 `A2aTask{ status: submitted }`（不带 artifacts）

### 6.3 轮询（`tasks/get`）

- 查内存 map；查不到 → `-32001 TaskNotFound`
- 命中 → `POST /invoke { action_id: "system.task.status", params: {task_id: <upstream_id>} }`
- 上游返回 CapsFrame with payload `{status, result?}`

### 6.4 状态机翻译

| NWP (`system.task.status`)        | A2A `status.state`          | 备注                                   |
| --------------------------------- | --------------------------- | -------------------------------------- |
| `pending`                         | `submitted`                 | 桥侧刚记录                             |
| `running`                         | `working`                   | 正在执行                               |
| `completed`                       | `completed`                 | 返回 artifact                          |
| `failed`                          | `failed`                    |                                        |
| `cancelled` / `canceled`          | `canceled`                  | NWP 用英式，A2A 用美式；两种写法都认  |
| 其他 / 不存在                     | `unknown`                   | 不抛错，留给客户端判断                 |

### 6.5 取消（`tasks/cancel`）

- 查 map；没找到 → `-32001`
- 找到 → `POST /invoke { action_id: "system.task.cancel", params: {task_id} }`
- 上游 2xx → 返回 `A2aTask{ status: canceled }`
- 上游非 2xx → `-32002 TaskNotCancelable` JSON-RPC 错误

### 6.6 Task map 的持久化

**v0.1 桥用 in-memory ConcurrentDictionary**。意味着：

- 重启会丢失异步 task 绑定 → `tasks/get` 对重启前的 id 报 `-32001`
- 多副本部署 task 会 pin 在某一副本上 → 需要 sticky session 或共享存储

**v0.2 计划**：抽象 `ITaskBindingStore`，默认内存实现 + Redis 适配器。
当前 workaround：若部署多副本，在 LB 用 `a2a_task_id` 做 header-based
session affinity，或让 upstream 自行维护 task→node 的反查索引。

---

## 7. Error 映射

### 7.1 标准 JSON-RPC 错误

| Code     | 触发                                             |
| -------- | ------------------------------------------------ |
| `-32700` | body 非 JSON                                     |
| `-32600` | 不是合法 JSON-RPC 2.0 请求                       |
| `-32601` | 方法名不认识                                     |
| `-32602` | 参数缺失 / 非法（含 skill 查找失败）             |
| `-32603` | 其他异常                                         |

### 7.2 A2A 应用级错误

| Code     | 含义                                               |
| -------- | -------------------------------------------------- |
| `-32001` | `TaskNotFound` — 桥的 map 没有这个 a2a_task_id     |
| `-32002` | `TaskNotCancelable` — 上游拒绝取消                 |
| `-32004` | `UnsupportedOperation` — `tasks/sendSubscribe` 等 |
| `-32010` | `UpstreamError` — 轮询上游 status 时上游失败       |

### 7.3 **关键设计**：上游业务失败 **不** 报 JSON-RPC 错误

`tasks/send` 拿到上游 4xx/5xx 时 **不抛 JSON-RPC 错误**，而是返回：

```json
{ "jsonrpc":"2.0","id":1,"result":{
    "id":"task-001",
    "status":{ "state":"failed",
               "message":{ "role":"agent", "parts":[ {"type":"text","text":"Upstream NWP returned HTTP 422: ..."} ] } }
} }
```

这样 A2A 客户端可以**统一**按 task state 处理，不需要 exception handling
路径。JSON-RPC 错误只保留给真正的**协议级**故障（参数错、方法错、桥自身 bug）。

### 7.4 Notification

无 `id` → HTTP `204 No Content`。方法名错也不回错。

---

## 8. 实战接入

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

### 8.2 手写 JSON-RPC 客户端

```bash
# 拉 AgentCard
curl https://bridge.example.com/.well-known/agent.json

# 发送 task
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

### 8.3 作为 A2A peer 发起 task（反向）

本桥只提供**被动**端（server 角色）。如果你要从 NPS agent 主动发 task 到
其它 A2A agent，用 NWP 的 `http_upstream` gateway 模式或直接用 Google ADK
的客户端 SDK。

---

## 9. 部署

### 9.1 PublicUrl & reverse proxy

生产必配：

```csharp
o.PublicUrl = new Uri("https://bridge.public.example.com/a2a");
```

若通过 NGINX / Envoy，务必让 `X-Forwarded-Proto` / `X-Forwarded-Host` 正确
转入 Kestrel（`app.UseForwardedHeaders()`），否则 fallback 派生会出错。

### 9.2 认证

桥**不做**客户端认证。A2A 规范的 `authentication` 字段是**描述性的**——
AgentCard 告诉 peer "我需要 bearer"，但桥本身不验证。把鉴权放在外层：
NGINX + OAuth2 proxy、Kestrel + JWT bearer、或放在 API gateway。

上游方向的认证通过 `A2aUpstream.AuthHeader` 注入，**静态**。

### 9.3 多副本

见 §6.6 的 task map 持久化问题。不需要异步 task 的部署可以直接多副本；
需要异步的暂时保持单副本，或外套 sticky session。

### 9.4 观测

- `ILogger<A2aIngress>` 记录 dispatch 失败 / 上游 status 查询失败
- 建议：用 `OpenTelemetry.Instrumentation.Http` 监控上游 `/invoke` 的
  延迟、状态码、请求量
- 如果要暴露 A2A 任务数 metric，可以 subclass `A2aIngress` 并重写
  dispatcher，当前未提供 Counter 注入点（v0.2 TODO）

### 9.5 超时

A2A client 的 `tasks/send` 超时由客户端 SDK 管；桥的 `HttpClient` 默认
100 秒。把长任务都走异步路径（`async=true` on NWP action）是最佳实践。

---

## 10. 测试

```bash
dotnet test compat/a2a-ingress/tests/LabAcacia.A2aIngress.Tests/
```

覆盖：

- AgentCard 合成（含 fallback description）
- `tasks/send` 三种 skill 来源 × 同步 / 异步 / 失败
- `tasks/get` 状态翻译全 5 态
- `tasks/cancel` 成功 + 上游拒绝
- `tasks/sendSubscribe` / push → `-32004`
- `tasks/get` 未知 id → `-32001`
- notification (no id) → 204
- URL 回退 / PublicUrl 覆盖

无网络。

---

## 11. 限制

| 类别              | 状态            | 说明                                                       |
| ----------------- | --------------- | ---------------------------------------------------------- |
| Server-streaming  | 未支持          | `tasks/sendSubscribe` → `-32004`；v0.2 TODO                |
| Push notifications | 未支持         | 同上                                                       |
| State transition history | 未支持    | A2A client 只能拿 current state                            |
| Task map 持久化   | 仅内存          | 重启 / 多副本场景受限（见 §6.6）                            |
| 多上游            | 永不支持        | 协议语义上一 AgentCard 一 agent                            |
| Authentication    | 只发 schemes 声明 | 实际鉴权在外层                                            |

---

## 12. 常见问题

**Q: A2A peer 调我的 bridge，能看到上游 NWP 的 schema 吗？**
A: 看不到 AnchorFrame / NID binding 这些 NPS 特色字段。AgentCard 只暴露
skill id + 描述 + async tag。Peer agent 按 skill id 投递，业务参数通过
`metadata.params` 或 data part 传。

**Q: 上游是 Anchor Node（NOP 编排入口）怎么办？**
A: 没问题。Anchor Node 的 `/actions` 和 Action Node 结构一致，桥无需
特殊处理。只要 Anchor 声明的 action 可被 `/invoke`（同步或异步），桥就
能代理。

**Q: 为什么不把 NWP `anchor_ref` 暴露给 A2A client？**
A: A2A 的 Task/Artifact 模型里没有"schema ref"的概念。硬塞进
`artifact.metadata` 会让 peer agent 被迫处理 NPS 特色字段，违背桥的
透明性目标。如果下游 agent 是 NPS-aware 的，让它直接走 NWP 原生协议。

**Q: 能做"一个 bridge 进程 N 个 upstream"吗？**
A: 不行，每个进程绑一个 upstream。如果要暴露多个 NPS agent，起多个
bridge 进程，用不同 port / path prefix。这是刻意设计——
A2A AgentCard 是 agent 身份，不该塞多个。

**Q: Task id 由谁生成？**
A: 由 A2A **client** 生成。桥不生成也不校验格式——只要在内存 map 里
做了记录，`tasks/get` 就能回找上游 task。建议 client 用 UUID v4。

---

## 13. 参考

- NWP 规范：[`spec/NPS-2-NWP.md`](../../spec/NPS-2-NWP.md)（Action Node §7，
  异步 §7.2，`system.task.*` §7.3）
- A2A 规范：https://github.com/google/A2A/blob/main/specification/
- CHANGELOG：[`compat/a2a-ingress/CHANGELOG.md`](https://github.com/labacacia/NPS-A2A-Ingress/blob/main/CHANGELOG.md)
- 独立仓：[labacacia/NPS-A2A-Ingress](https://github.com/labacacia/NPS-A2A-Ingress)
