[English Version](./index.en.md) | 中文版

# NPS Compatibility Bridges

NPS 提供三座桥，把 NWP 节点暴露给已有生态。三座桥都是 **ASP.NET Core 库** +
同构代码路径（`IHttpClientFactory` 转发到上游 NWP 节点），但在**协议语义**和
**错误语义**上做了不同取舍。

| Bridge                         | 对外协议                          | 上游节点                                   | 版本           | 关系          |
| ------------------------------ | --------------------------------- | ------------------------------------------ | -------------- | ------------- |
| [MCP Ingress](./mcp-ingress.md)   | MCP 2024-11-05（JSON-RPC 2.0/HTTP） | Memory / Action / Complex                  | v1.0.0-alpha.16 | 1 : N         |
| [A2A Ingress](./a2a-ingress.md)   | Google A2A v0.2（JSON-RPC 2.0/HTTP） | Action / Complex / Anchor                  | v1.0.0-alpha.16 | 1 : 1         |
| [gRPC Ingress](./grpc-ingress.md) | gRPC/HTTP2（4 个 unary RPC）         | Memory / Action / Complex / Anchor         | v1.0.0-alpha.16 | 1 : N         |

---

## 如何选？

### 选 **MCP Ingress**——当你要面向 LLM 客户端

- 客户端是 Claude Desktop / Cursor / Continue 等 MCP host
- 需要把若干 NWP Memory Node 的 schema 暴露为 `resources`
- 需要把若干 NWP Action Node 的动作暴露为 `tools`
- 场景：让一个 AI 助手直接读你的数据 + 调你的 API

### 选 **A2A Ingress**——当你要让 Agent 互相调

- 对端是 Google A2A-compatible Agent
- 一个 NWP 节点 = 一个 AgentCard
- 需要异步任务生命周期（`tasks/get` 轮询）
- 场景：Agent-to-Agent 编排、跨组织 Agent 协作

### 选 **gRPC Ingress**——当你要接工程基础设施

- 调用方在已有 gRPC / protobuf 生态里（service mesh、Envoy、既有 microservice）
- 需要 HTTP/2 多路复用、deadline、流式（v0.2+）
- 愿意用 JSON-encoded bytes 透传（而非强类型 proto；见文档 §1.1）
- 场景：把 NPS 服务纳入 k8s + Istio 的企业部署

---

## 共同设计

三座桥共享以下设计：

1. **上游协议**：都经 NWP HTTP 模式（`application/nwp-frame`）访问 `/.nwm` /
   `/invoke` / `/query` / `/actions`
2. **身份**：服务到服务用 `NwpUpstream.AuthHeader`，上游 NWP 的 Agent 身份
   通过 `X-NWP-Agent` 头传递（gRPC 桥允许 client 覆盖，MCP / A2A 桥固定）
3. **幂等**：`/invoke` 透传 `Idempotency-Key`
4. **追踪**：透传 W3C `traceparent`
5. **无状态**：A2A 桥的 in-memory 任务映射除外；其余桥均为纯转发层

---

## 共同不做的

- **不做 NWP 原生帧代理**：桥**始终**走 NWP HTTP 模式；原生 NCP 帧携带是 v0.3+ 方向
- **不做 schema 翻译**：NWP 是运行时 schema，不翻译成桥协议的编译期类型
  （gRPC 桥的关键决策，见 [`grpc-bridge.md §1.1`](./grpc-ingress.md#11-桥的设计分歧点)）
- **不做 auth 代理**：end-user token **不**从桥的客户端侧透传到上游；需要时用
  interceptor 自行注入

---

## 版本对齐

| 组件             | 版本 | 兼容的 NWP 规范                                             |
| ---------------- | ---- | ----------------------------------------------------------- |
| MCP Ingress       | v1.0.0-alpha.16 | [NWP v0.13](../../spec/NPS-2-NWP.cn.md) `/.nwm` + `/invoke` + `/query` |
| A2A Ingress       | v1.0.0-alpha.16 | [NWP v0.13](../../spec/NPS-2-NWP.cn.md) Action/Complex/Anchor `§3.2` + `§7` 异步任务 |
| gRPC Ingress      | v1.0.0-alpha.16 | [NWP v0.13](../../spec/NPS-2-NWP.cn.md) `/.nwm` + `/invoke` + `/query` + `/actions` |

---

## 独立仓

三座桥同时以**独立 repo** 发布，方便 NuGet 消费：

- [labacacia/NPS-MCP-Ingress](https://github.com/labacacia/NPS-MCP-Ingress)
- [labacacia/NPS-A2A-Ingress](https://github.com/labacacia/NPS-A2A-Ingress)
- [labacacia/NPS-gRPC-Ingress](https://github.com/labacacia/NPS-gRPC-Ingress)

monorepo 下的副本（`compat/<bridge>/`）是**参考实现**，可提 PR。独立 repo
是**发布目标**，issue 和 release 都在那里。

---

## 详细文档

- [MCP Ingress 详解](./mcp-ingress.md)
- [A2A Ingress 详解](./a2a-ingress.md)
- [gRPC Ingress 详解](./grpc-ingress.md)

---

## 参考

- NWP 规范：[`spec/NPS-2-NWP.cn.md`](../../spec/NPS-2-NWP.cn.md) v0.5
- AaaS 合规规范：[`spec/services/NPS-AaaS-Profile.cn.md`](../../spec/services/NPS-AaaS-Profile.cn.md)
- Roadmap：[`spec/NPS-Roadmap.cn.md`](../../spec/NPS-Roadmap.cn.md)
