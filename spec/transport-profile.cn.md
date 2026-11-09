[English Version](./transport-profile.md) | 中文版

# NPS 传输绑定规范（Transport Profile）

> 历史草案，不是当前合规目标。请参阅[传输规则对齐](./transport-reconciliation.cn.md)
> 中的冲突台账和现行 NCP 依据。下文原稿仅作历史保留。

**标题**: NPS Transport Profile  
**版本**: 0.1  
**状态**: Draft  
**日期**: 2026-05-10  
**依赖**: NPS-1-NCP v0.7

---

## 1. 概述

NPS 在协议层是**传输无关**的：NCP 帧（见 [NPS-1-NCP.md](NPS-1-NCP.cn.md)）是一段自包含的字节序列，其语义不依赖具体载体。本规范统一规定：合规实现 **必须** 识别哪些传输绑定、每种绑定如何在线路上承载 NCP 帧，以及当多种绑定共用同一监听端口时服务端如何**自动检测**。

本文中的关键词 "MUST"、"MUST NOT"、"REQUIRED"、"SHOULD"、"SHOULD NOT"、"MAY" 按 RFC 2119 解释。

本规范是 NCP §2.2（传输双模）与 §2.6.1（连接前导）的补充，不替代它们。如本文与 NPS-1-NCP 冲突，以 NPS-1-NCP 为准。

---

## 2. 传输绑定

合规服务端 **必须** 支持 **HTTP/1.1** 与 **TCP 原生** 两种绑定。本节其它绑定为可选；一旦实现，必须遵守对应规则。

### 2.1 HTTP/1.1

- 方法：`POST`
- 路径：`/nps`（服务端 MAY 暴露协议特定路径如 `/nwp/...`；`/nps` 是传输探测的标准回退路径）
- 请求 `Content-Type` 头：客户端 MUST 发送 `application/x-nps-frame`；服务端 MUST 将该值视为正文为 NCP 帧的权威证据。
- 响应 `Content-Type` 头：原生 NCP 帧响应使用 `application/x-nps-frame`。
- HTTP body 携带恰好一个序列化 NCP 帧；当使用 `Transfer-Encoding: chunked` 时，body 为按发送顺序拼接的多个 NCP 帧。
- 错误 MAY 在 HTTP body 中以 `ErrorFrame`（0xFE）返回；同时 SHOULD 在 HTTP 状态行中按 §5 反映。

### 2.2 HTTP/2

- §2.1 中所有 HTTP/1.1 规则均适用。
- 一条 HTTP/2 连接 MAY 多路复用多个请求流；每个流承载一个逻辑 NCP 请求/响应对。
- Server Push MUST NOT 用于推送客户端未请求的 NCP 帧。
- 当 ALPN 协商 `h2` 成功时，实现 SHOULD 优先使用 HTTP/2 而非 HTTP/1.1。

### 2.3 WebSocket

- 通过 HTTP/1.1 Upgrade（`Upgrade: websocket`）在 `/nps`（或实现自定义路径）上建立连接。
- 升级完成后，NCP 帧 MUST 作为 **二进制** WebSocket 数据帧交换；MUST NOT 使用文本帧承载 NCP 帧。
- 一个 WebSocket 数据帧承载一个 NCP 帧；WebSocket 层的 continuation 分片只能在 WebSocket 边界发生，MUST NOT 把一个 NCP 帧拆到两条 NPS 层消息里。

### 2.4 TCP 原生

- 载体：单条 TCP 连接（无 HTTP 包装）。
- TCP 握手完成后，客户端 MUST 立即发送 [NPS-1-NCP §2.6.1](NPS-1-NCP.cn.md#261-连接前导native-mode) 规定的 **8 字节连接前导**：`b"NPS/1.0\n"`（hex `4E 50 53 2F 31 2E 30 0A`）。
- 未收到前导的服务端 MUST 按 §2.6.1 关闭连接；本规范不削弱该要求。
- 前导之后，连接为 NCP 帧的双向连续流（见 [NPS-1-NCP §3](NPS-1-NCP.cn.md)）。

### 2.5 QUIC

- 载体：通过 ALPN（见 §3）协商的 QUIC 连接。
- 拓扑：**每会话一流（stream-per-session）** —— 每个 NPS 逻辑会话占用恰好一条双向 QUIC stream。长会话 MAY 在该 stream 上保持整个生命周期；一次性请求/响应 MAY 新开一条 stream，完成后关闭。
- 客户端 MUST 在每条 stream 起始处发送 NPS-1-NCP §2.6.1 中规定的 8 字节前导，先于该 stream 上的第一个 NCP 帧。
- MAY 使用 0-RTT 数据；若使用，前导计入 0-RTT 载荷。

---

## 3. TLS

### 3.1 ALPN Token

ALPN token `nps/1` 标识 NPS Transport Profile 1.x。客户端在与 NPS 端点协商 TLS 时 SHOULD 提供 `nps/1`。当监听端口专用于 NPS 时，服务端 SHOULD 接受 `nps/1` 并相对 `http/1.1` / `h2` 等通用 token 优先选择它。

`nps/1` 将注册进 IANA TLS ALPN Protocol IDs 注册表。

### 3.2 各部署 Profile 的 TLS 版本要求

| 部署 Profile | TLS 要求 |
|-------------|----------|
| 公共联邦（跨组织、面向互联网） | **MUST** 使用 TLS 1.2 或更高；推荐 TLS 1.3 |
| 组织私域（单组织或单信任域内） | **SHOULD** 使用 TLS 1.2 或更高 |
| 本地开发（loopback、单机） | **MAY** 使用 TLS；允许明文 |

公共联邦 Profile 下，实现 MUST 拒绝 SSLv3、TLS 1.0 与 TLS 1.1。

---

## 4. 检测优先级阶梯

当服务端在同一监听器上暴露多种绑定时（例如 HTTPS 端口同时承载 WebSocket Upgrade，或客户端误路由发来原始 TCP 前导），服务端 MUST 按下列**有序**阶梯自动检测绑定。**首个**产生确定匹配的信号胜出；后续信号 MUST NOT 覆盖前面的匹配。

| 优先级 | 信号 | 选定绑定 |
|--------|------|---------|
| 1 | TLS ALPN 选中 `nps/1` | NPS 原生（TCP / HTTP/2 / QUIC，取决于 TLS 层载体） |
| 2 | HTTP 请求 `Content-Type: application/x-nps-frame`（或在 `/nps` 上的 `Upgrade: websocket`） | HTTP / WebSocket 绑定（§2.1–§2.3） |
| 3 | 流的前 8 字节匹配 NCP 原生前导 `b"NPS/1.0\n"` | TCP 原生（§2.4） |
| 4 | 连接到达 NPS 默认端口 `7433` | 服务端 MAY 假定为 TCP 原生并读取前导；不匹配则按 NPS-1-NCP §2.6.1 关闭连接 |

若以上均未命中，服务端 MUST 将连接视为非 NPS 流量并关闭，且 MUST NOT 在线路上写出 NPS 帧。

> **关于默认端口。** 本规范保留 **7433** 作为传输探测用途的默认端口。NPS 套件的默认运行端口仍是 **17433**（NPS-1-NCP §2.3）；7433 适用于希望单独运行探测专用监听器的环境。

---

## 5. ErrorFrame → HTTP 状态码映射

在 HTTP / WebSocket 绑定下，服务端发送 `ErrorFrame`（NCP 0xFE）时 SHOULD 同时把失败反映在 HTTP 状态码上。下表为已列出错误码的标准映射；未列出的错误码，实现 SHOULD 参照 [status-codes.cn.md](status-codes.cn.md) 选择最接近的状态码。

| ErrorFrame 错误码 | HTTP 状态码 |
|-------------------|-------------|
| `NCP-ANCHOR-ID-MISMATCH` | 409 Conflict |
| `NCP-ANCHOR-STALE` | 409 Conflict |
| `NCP-ANCHOR-NOT-FOUND` | 404 Not Found |
| `NCP-FRAME-TOO-LARGE` / `NCP-FRAME-PAYLOAD-TOO-LARGE` | 413 Content Too Large |
| `NCP-FRAME-UNKNOWN-TYPE` | 400 Bad Request |
| `NCP-FRAME-FLAGS-INVALID` | 400 Bad Request |
| `NCP-VERSION-INCOMPATIBLE` | 426 Upgrade Required |
| `NCP-ENCODING-UNSUPPORTED` | 415 Unsupported Media Type |
| `NCP-PREAMBLE-INVALID` | （无 HTTP 响应 —— 连接静默关闭） |
| `NOP-COMPENSATION-FAILED` | 422 Unprocessable Content |
| `NWP-RESERVED-TYPE-UNSUPPORTED` | 501 Not Implemented |
| （其它协议级错误） | 400 Bad Request |

服务端故障（未捕获异常、内部错误）MUST 返回 HTTP 500 且不附带 `ErrorFrame` 主体，除非服务端确信对端已完成 NPS 握手。

---

## 变更记录

| 版本 | 日期 | 说明 |
|------|------|------|
| 0.1 | 2026-05-10 | 初始 Draft。定义 HTTP/1.1、HTTP/2、WebSocket、TCP 原生、QUIC 绑定；ALPN token `nps/1`；检测优先级阶梯；ErrorFrame → HTTP 状态码映射。Issue #31。 |
