# 五层协议

> [English](protocols.md) | 中文版

NPS 拆成五层，每个关注点有唯一归属。依赖关系：

```
NCP ← NWP ← NIP ← NDP
NCP + NWP + NIP ← NOP
```

五个协议共用默认端口 **17433**，通过 1 字节帧类型字段路由。

---

## NCP — Neural Communication Protocol

**角色**：Wire format 与 framing。最接近的类比是 **HTTP/2 帧**。

NCP 定义 4 字节（或 8 字节 EXT）帧头、1 字节 flags、两个编码 tier（Tier-1 JSON 和 Tier-2 MsgPack），以及五种核心帧。

| 帧 | 编码 | 用途 |
|----|------|------|
| `AnchorFrame`  | 0x01 | 发布内容寻址的 Schema |
| `DiffFrame`    | 0x02 | 演化 Anchor — Schema 迁移 |
| `StreamFrame`  | 0x03 | 流式响应的一个 chunk |
| `CapsFrame`    | 0x04 | 节点能力 / 响应信封 |
| `ErrorFrame`   | 0xFE | 全协议族共用的统一错误帧 |

**关键设计决策：**

- 帧大小：默认 64 KB（EXT=0）；扩展 4 GB（EXT=1），用于大 payload
- 编码：生产环境默认 MsgPack（比 JSON 小 ~60%）；JSON 用于调试和互通
- `AnchorFrame` TTL 默认 3600 秒

完整规范：[NPS-1 NCP](https://github.com/labacacia/NPS/blob/main/spec/NPS-1-NCP.cn.md)。

---

## NWP — Neural Web Protocol

**角色**：请求 / 响应语义。类比是 **HTTP**。

NWP 定义 Agent 如何向节点发起查询或动作，并通过 `anchor_ref` 标明本次交互绑定的 Schema。

| 帧 | 编码 | 用途 |
|----|------|------|
| `QueryFrame`  | 0x10 | 对 Memory Node 的分页 / 过滤查询 |
| `ActionFrame` | 0x11 | 对节点发起同步或异步动作 |

**四种节点：**

- **Memory Node** — 拥有 Anchor 并对外提供查询（商品目录、知识库）
- **Action Node** — 暴露可调用动作（发邮件、下单）
- **Anchor Node** — 无状态路由到 NOP DAG（NPS-CR-0001 由 Gateway Node 重命名）和 **Bridge Node** — NPS↔非-NPS 协议翻译
- **Agent Node** — 同时说 NWP + NOP，通常是 LLM 驱动的执行体

完整规范：[NPS-2 NWP](https://github.com/labacacia/NPS/blob/main/spec/NPS-2-NWP.cn.md)。

---

## NIP — Neural Identity Protocol

**角色**：加密身份。类比是 **TLS + PKI**。

每个节点、每个 Agent 都有一对 Ed25519 密钥和一个 **NID** — URN 形如 `urn:nps:node:{authority}:{name}`。身份不是外挂；`IdentFrame` 是一等公民的帧。

| 帧 | 编码 | 用途 |
|----|------|------|
| `IdentFrame`  | 0x20 | 节点身份声明 |
| `TrustFrame`  | 0x21 | 委托 / 信任断言（带 scope + expiry）|
| `RevokeFrame` | 0x22 | 吊销 NID |

**为什么 Ed25519**：性能优先。Agent 高频验签 — RSA-2048 每一帧会烧掉毫秒。

**密钥存储**：参考实现使用 AES-256-GCM + PBKDF2-SHA256（600,000 次迭代）持久化密钥。独立的 **NIP CA Server** 提供六种语言（C# / Python / TypeScript / Java / Rust / Go）。

完整规范：[NPS-3 NIP](https://github.com/labacacia/NPS/blob/main/spec/NPS-3-NIP.cn.md)。

---

## NDP — Neural Discovery Protocol

**角色**：节点发现。类比是 **DNS**。

Agent 需要把 `nwp://api.example.com/products` 解析为具体的 (host, port, protocol) 三元组。NDP 是这个解析过程，且不依赖中心注册表。

| 帧 | 编码 | 用途 |
|----|------|------|
| `AnnounceFrame` | 0x30 | 发布节点的可达地址 + 能力 + TTL |
| `ResolveFrame`  | 0x31 | 查询或应答"此 NID 的地址是？" |
| `GraphFrame`    | 0x32 | 解析器之间的拓扑同步 |

**Announce 必须签名** — 解析器用已知公钥校验签名后才接受记录。TTL = 0 表示"正在下线，请驱逐"。

完整规范：[NPS-4 NDP](https://github.com/labacacia/NPS/blob/main/spec/NPS-4-NDP.cn.md)。

---

## NOP — Neural Orchestration Protocol

**角色**：多 Agent 任务编排。类比是 **SMTP + 消息队列 + 工作流引擎** 三合一。

NOP 接受一个 DAG — 节点执行动作，边传输输出 — 并提供委托、fan-in barrier、流式进度、签名委托链。

| 帧 | 编码 | 用途 |
|----|------|------|
| `TaskFrame`        | 0x40 | 提交一个 DAG 去执行 |
| `DelegateFrame`    | 0x41 | 编排器向 Agent 发出的节点级调用 |
| `SyncFrame`        | 0x42 | Fan-in barrier — 等 K-of-N 上游完成 |
| `AlignStreamFrame` | 0x43 | 子任务的流式进度 / 部分结果 |

**硬性限制（NPS-5 §8.2）：**

- 每个 DAG 最多 32 节点
- 委托链最多 3 层
- 超时上限 3,600,000 ms（1 小时）
- `callback_url` 由编排器做 SSRF 校验

完整规范：[NPS-5 NOP](https://github.com/labacacia/NPS/blob/main/spec/NPS-5-NOP.cn.md)。

---

## 帧命名空间（速查）

| 范围 | 协议 | 帧 |
|------|------|----|
| `0x01–0x0F` | NCP | Anchor、Diff、Stream、Caps（Hello 预留） |
| `0x10–0x1F` | NWP | Query、Action |
| `0x20–0x2F` | NIP | Ident、Trust、Revoke |
| `0x30–0x3F` | NDP | Announce、Resolve、Graph |
| `0x40–0x4F` | NOP | Task、Delegate、Sync、AlignStream |
| `0xFE`      | System | ErrorFrame — 全协议族共用 |

机器可读注册表：[frame-registry.yaml](https://github.com/labacacia/NPS/blob/main/spec/frame-registry.yaml)。

---

## 相关文档

- [错误码](https://github.com/labacacia/NPS/blob/main/spec/error-codes.cn.md) — `{PROTOCOL}-{CATEGORY}-{DETAIL}` 命名空间
- [状态码](https://github.com/labacacia/NPS/blob/main/spec/status-codes.cn.md) — NPS 原生状态码 + HTTP 映射
- [Token Budget](https://github.com/labacacia/NPS/blob/main/spec/token-budget.cn.md) — CGN 计量规范
- [AaaS Profile](https://github.com/labacacia/NPS/blob/main/spec/services/NPS-AaaS-Profile.cn.md) — Agent-as-a-Service 合规级别（L1 / L2 / L3）

---

📖 教程、参考资料和运维指南，请访问 [NPS handbook](https://github.com/labacacia/NPS/blob/main/docs/handbook/Home.md)。
