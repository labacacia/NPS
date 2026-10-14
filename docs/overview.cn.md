# 总览

> [English](overview.md) | 中文版

## NPS 是什么？

**Neural Protocol Suite** 是一族由五个相互协作的子协议组成的栈，用来替换 HTTP/REST 在 AI 原生负载中的位置：

| 层 | 名称 | 类比 |
|----|------|------|
| NCP   | Neural Communication Protocol | Wire format / framing（类比 HTTP/2 帧） |
| NWP   | Neural Web Protocol           | Request / response（类比 HTTP 语义） |
| NIP   | Neural Identity Protocol      | Agent 的 TLS / PKI |
| NDP   | Neural Discovery Protocol     | Agent 的 DNS |
| NOP   | Neural Orchestration Protocol | SMTP + MQ — 多 Agent DAG |

五个协议共用默认端口 **17433**，并通过 1 字节的帧类型字段路由。同一条连接可以同时承载任意协议的帧。

---

## 为什么不继续用 HTTP？

HTTP 和 REST 假设**有一个人类在读响应**。对 AI Agent，这个假设在四处失效：

### 1. Schema 重复

REST 端点每次响应都隐式地重新描述一遍自己的 Schema（字段名、类型提示、嵌套）。一个 Agent 查询 100 个商品，相同的字段名会在线缆上重复 100 次。

NPS 的 `AnchorFrame` **只发布一次** Schema。Agent 通过 SHA-256 内容寻址 id（`sha256:abc…`）引用它。服务器可以通过 `DiffFrame` 失效或演进它。

### 2. 身份只是附加品

HTTP 的身份是一堆补丁：Cookie、OAuth、API key、mTLS。在 Agent A 委托 Agent B 的多 Agent 工作流中，**"A 是不是真的代表用户说话"** 没有标准答案。

NPS 把 `IdentFrame` 和 `TrustFrame` 做成一等公民的 Ed25519 身份。每一帧都可签名；`NipCa` 以规范化的证书生命周期签发和吊销身份。

### 3. 语义靠散文传递

REST API 发布 OpenAPI Schema，描述的是**类型**，不是**含义**。Agent 得从上下文推断 `price` 是"美元零售价"。

NPS 的 Schema 字段携带 `semantic` 注解 — 例如 `commerce.price.usd` — 来自共享本体。Agent 不需要做散文解析就能对齐意图。

### 4. 只能一来一回

REST 是请求-响应。流式是 2009 年补丁（SSE / chunked transfer）。多 Agent 编排完全外包给了框架（Temporal、Airflow、LangGraph）。

NPS 把流（`StreamFrame`、`AlignStreamFrame`）和编排（`TaskFrame`、`DelegateFrame`、`SyncFrame`）做成**协议级原语**。

---

## NPS 面向谁？

| 你在构建… | NPS 为你做到… |
|-----------|--------------|
| 面向 AI 购物者的知识库 / API | 一次发布 Anchor → 每次查询 Token 减少 80% |
| 多 Agent 编排器 | 使用带签名委托链的 `TaskFrame` DAG |
| Agent 市场 | 加密身份 + 发现，不用自建 DNS |
| 受合规约束的服务 | AaaS Profile L1 / L2 / L3 定义了"生产就绪" |
| 消费 Agent 产出数据的工具 | Schema 绑定的响应，不再靠 LLM 解析 JSON |

NPS **不是** LLM API（OpenAI、Anthropic）的替代。它是这些 LLM 驱动的 Agent 彼此通话、访问数据源、调用彼此工具时走的**网络**。

---

## NPS 不是什么

- 不是新的基础模型架构
- 不是 LangChain 风格的 SDK（NPS 是 **wire**，SDK 编码它）
- 不是 MCP（MCP 是单 LLM 的工具协议；NPS 是多 Agent 的网络）
- 默认不与 A2A 兼容 — `a2a-bridge` 适配器在路线图里

---

## 下一步

- [协议族](protocols.cn.md) — 五层协议详解
- [SDK](sdks.cn.md) — 选一门语言
- [路线图](roadmap.cn.md) — 已发布 / 待发布

---

📖 教程、参考资料和运维指南，请访问 [NPS handbook](https://github.com/labacacia/NPS/blob/main/docs/handbook/Home.md)。
