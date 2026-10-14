[English Version](./node-types.en.md) | 中文版

# 节点类型

NPS 定义五种神经节点类型，各自承担不同的职责。一个部署 MAY 同时承担多个角色（例如同时是 Memory Node 和 Anchor Node）；多角色声明通过 NDP `Announce` 帧的 `node_roles` 字段下发。原 `Gateway Node` 类型已在 v1.0-alpha.3 由 [NPS-CR-0001](../../spec/cr/NPS-CR-0001-anchor-bridge-split.md) 拆分为 Anchor Node + Bridge Node 两类，原名称已退役。

---

## Memory Node（记忆节点）

**类比：** 数据库 + 搜索引擎

**职责：** 数据存储与检索。AI Agent 通过 `QueryFrame` 访问结构化数据，节点负责过滤、分页、排序，并以 Schema 锚定的格式返回结果。

**核心特性：**
- 通过 `AnchorFrame` 预先声明 Schema，避免重复传输
- 支持 NWP Filter DSL（`$eq`、`$gt`、`$in`、`$between`、`$contains` 等）
- 支持游标分页（`next_cursor`）
- 可选向量相似度搜索（`VectorSearch` 能力）
- 支持流式响应（`/stream` 端点 + `StreamFrame`）
- CGN 消耗计量（`X-NWP-Tokens` 响应头）

**已实现数据源（.NET）：** SQL Server、PostgreSQL；自定义数据源实现 `IMemoryNodeProvider`。

**NWM 声明：**

```json
{
  "node_type": "memory",
  "capabilities": { "query": true, "stream": true, "vector_search": false }
}
```

---

## Action Node（行动节点）

**类比：** REST API + 函数调用

**职责：** 执行操作和服务调用。AI Agent 通过 `ActionFrame` 调用节点暴露的操作（`action_id`），节点执行业务逻辑并返回结果。

**核心特性：**
- 操作以 `"{domain}.{verb}"` 格式标识（如 `orders.create`）
- 支持幂等键（`IdempotencyKey`），24 小时内相同键返回原始结果
- 支持异步操作（`Async = true`），返回 `task_id + poll_url`
- 自定义超时（`TimeoutMs`，最大 300 秒）

**NWM 声明：**

```json
{
  "node_type": "action",
  "capabilities": { "query": false }
}
```

---

## Complex Node（复合节点）

**类比：** BFF（Backend For Frontend）+ 图数据库

**职责：** 数据与处理的综合节点。通过节点图谱遍历（`X-NWP-Depth`）将请求委托给子节点，实现多数据源聚合或多步骤处理。

**核心特性：**
- `X-NWP-Depth` 控制最大遍历深度（默认 1，最大 5）
- 在 NWM 的 `graph.refs` 中声明子节点引用
- 可同时支持 Query 和 Action 能力
- 可内联子节点的 AnchorFrame（减少往返）

**NWM 声明：**

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

## Anchor Node（锚点节点）

**类比：** 集群控制面 + NOP 入口

**职责：** 集群控制面与对外入口。接受发往集群的 NWP `Action` / `Query` 帧，将其分派到成员节点（参考做法：转换为 NOP `TaskFrame` 并交给本地 NOP 编排器），把成员节点的响应聚合成一条返回流，并可维护成员节点注册表。

**核心特性：**
- 单次请求无状态；MAY 维护长期成员节点注册表
- 成员节点通过 NDP `Announce` 帧上的 `cluster_anchor` 字段（指向 Anchor Node 的 NID）入册
- 维护注册表的 Anchor Node MUST 通过保留查询类型 `topology.snapshot` / `topology.stream` 暴露（见 NPS-CR-0002）
- 与 Complex Node 的本质区别：Complex Node 有自身业务数据，Anchor Node 只做集群路由 + 聚合
- 取代原 `Gateway Node` 承担的"集群入口 / NOP 路由"角色

**NWM 声明：**

```json
{
  "node_type": "anchor",
  "capabilities": { "query": false }
}
```

---

## Bridge Node（桥接节点）

**类比：** NPS ↔ 外部协议翻译器

**职责：** 在 NPS 帧与非 NPS 协议（HTTP/HTTPS、gRPC、MCP、A2A）之间做桥接。出站适配器把 NPS `ActionFrame` 调用翻译成外部协议调用；入站 server adapter 把本地 NPS action 暴露成外部协议服务。一个 Bridge Node 可以翻译到多种外部协议，部署时也 MAY 按协议拆分独立的 Bridge Node 以做隔离。

**核心特性：**
- 出站：接收带 `bridge_target` 参数（标识外部协议与端点）的 NWP 帧
- 出站：以目标协议格式生成请求，并把目标协议的响应翻译回 NWP 帧
- 入站：把本地 NPS action 暴露成 MCP tool 与 A2A skill，并把外部客户端调用翻译成 `ActionFrame` dispatch
- 方向说明：`bridge_protocols` 声明 **NPS → 外部**能力；`bridge_inbound_protocols` 声明通过 `McpInboundServer` / `A2aInboundServer` / `GrpcInboundService` 提供的 **外部 → NPS**能力。旧的 `compat/*-ingress` 包仍作为独立兼容边界保留。
- 由 [NPS-CR-0001](../../spec/cr/NPS-CR-0001-anchor-bridge-split.md) 引入，并由 NPS-CR-0010 扩展为双向 Bridge；当前 SDK surface 使用共享 Bridge DTO、§16.3 error map、入站协议 server，以及基于 `INwpBackend` / `NwpBackend` 的 host handler。

**NWM 声明：**

```json
{
  "node_type": "bridge",
  "capabilities": { "query": false }
}
```

---

## 对比汇总

| 维度 | Memory Node | Action Node | Complex Node | Anchor Node | Bridge Node |
|------|-------------|-------------|--------------|-------------|-------------|
| 主要帧类型 | QueryFrame | ActionFrame | QueryFrame/ActionFrame | ActionFrame/TaskFrame | ActionFrame |
| 数据所有权 | 有 | 无 | 可选 | 无 | 无 |
| 子节点图谱 | 无 | 无 | 有 | 成员注册表（可选） | 无 |
| 状态 | 有状态 | 有状态 | 有状态 | **无状态**（注册表可选） | **无状态** |
| 可水平扩展 | 受数据库限制 | 受业务限制 | 受子节点限制 | **天然** | **天然** |
| 主要协议 | NWP | NWP | NWP + NDP | NWP + NOP + NDP | NWP ↔ 外部（HTTP/gRPC/MCP/A2A） |

---

## 相关文档

- [NPS.NWP API 参考](../sdk/dotnet/nps-nwp.md)
- [DAG 编排概念](./dag-orchestration.md)
- [NWP 规范](../../spec/NPS-2-NWP.md)
- [AaaS 合规性规范](../../spec/services/NPS-AaaS-Profile.md)
