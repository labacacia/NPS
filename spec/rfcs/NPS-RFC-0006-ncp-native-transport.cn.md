English | [中文版](./NPS-RFC-0006-ncp-native-transport.cn.md)

<!--
Copyright 2026 INNO LOTUS PTY LTD
Developed under LabAcacia Open Source Initiative
Licensed under the Apache License, Version 2.0
-->

# NPS-RFC-0006：NCP 原生模式传输绑定

> 2026-09-09 澄清：保留历史 Accepted 状态。冲突的旧成帧、early-data、rekey
> 和准入文本不得覆盖现行 NCP。实现前请阅读[传输规则对齐](../transport-reconciliation.cn.md)
> 的 TRN-01..13。本说明不代表 SDK 已通过资格验证。

**RFC 编号**: NPS-RFC-0006
**状态**: Accepted
**版本**: 0.2
**日期**: 2026-06-12
**作者**: Ori Lynn / INNO LOTUS PTY LTD
**依赖**: NPS-1 (NCP) v0.7, NPS-3 (NIP) v0.9
**Issue**: GitHub NPS-Dev#45

---

## 摘要

NCP 定义了两种传输模式：HTTP 模式（NCP 帧承载于 HTTP 报文体）和原生模式（直接 TCP 或 QUIC 连接）。NCP v0.6 规定了连接前导字节（`b"NPS/1.0\n"`，NPS-RFC-0001）及 HelloFrame/CapsFrame 握手，但 QUIC 流映射、重新加密密钥（rekeying）和合规要求均为实现自定。

本 RFC 正式规定了 TCP 和 QUIC 两种传输下的原生模式绑定，使六种参考 SDK 能够实现可互操作的原生模式支持。

---

## 1. 术语

本文中的关键词 "MUST"（必须）、"MUST NOT"（不得）、"SHOULD"（应）、"SHOULD NOT"（不应）、"MAY"（可以）依照 RFC 2119 解读。

---

## 2. TCP 传输绑定

### 2.1 连接建立

1. 客户端向服务端 NCP 端口（默认 17433）发起 TCP 连接。
2. 客户端 MUST 在任何其他数据之前发送 8 字节连接前导字节 `b"NPS/1.0\n"`（NPS-RFC-0001）。
3. 客户端 MUST 在前导字节发送后 5 秒内发送 HelloFrame（`0x06`）。
4. 服务端 MUST 在收到 HelloFrame 后 5 秒内回应 CapsFrame（`0x04`），否则发送 ErrorFrame 并关闭连接。

### 2.2 TCP 帧封装格式

NCP 帧在 TCP 上使用 5 字节长度前缀封装：

```
[4 字节: 载荷长度，大端序 uint32]
[1 字节: 帧类型字节（标志位 + 类型）]
[N 字节: 帧载荷]
```

帧总大小 MUST NOT 超过协商的 `max_frame_payload`。超出限制时，接收方 MUST 返回 `NCP-FRAME-TOO-LARGE`，并 MAY 关闭连接。

### 2.3 连接关闭

- 正常关闭：发送方在关闭 TCP socket 前发送 `status = "closing"` 的 CapsFrame。
- 异常关闭：TCP FIN 或 RST；对端 SHOULD 将所有进行中的请求视为失败，错误码 `NCP-NODE-UNAVAILABLE`。

---

## 3. QUIC 传输绑定

### 3.1 ALPN

NCP over QUIC 使用 ALPN 标识符 **`nps/1.0`**（套件级 NPS 原生模式 ALPN token，见 §6.1）。此值取代 v0.1 草案中临时使用的 `ncp/1`。IANA 注册待后续 RFC；实现 MUST 使用 `nps/1.0`。

### 3.2 流映射

- 每个 NCP 逻辑信道映射到**一条双向 QUIC 流**。
- HelloFrame MUST 在流 0（客户端发起的第一条双向流）上发送。
- 服务端 CapsFrame MUST 在同一流 0 上回复。
- 后续帧使用新的双向流（流 4、8、12……，按 QUIC 客户端发起双向流编号规则）。
- 单向 QUIC 流保留供未来使用，当前实现 MUST 忽略。

### 3.3 QUIC 帧封装格式

QUIC 上的帧无需 NCP 层长度前缀（QUIC 本身提供流级别帧边界）：

```
[1 字节: 帧类型字节（标志位 + 类型）]
[N 字节: 帧载荷]
```

### 3.4 连接关闭

正常关闭使用应用错误码 `0x00` 的 `CONNECTION_CLOSE` 帧；协议违规使用错误码 `0x01`。

### 3.5 QUIC 上的 HelloFrame

QUIC 上的 HelloFrame MUST 将 `transport` 字段设置为 `"quic"`。
不支持 QUIC 的服务端若意外检测到 QUIC 上已协商 ALPN `nps/1.0`，
MUST 返回带 `NCP-VERSION-INCOMPATIBLE` 的 ErrorFrame。

---

## 4. 重新加密密钥协议

当 E2E 加密处于活跃状态（NCP §7.4）时，实现 MUST 在以下条件满足后重新协商密钥（任一先到）：

- 连接上（任一方向）已发送 **2^32 帧**，或
- 距上次密钥协商已过 **24 小时** 墙钟时间

实现 SHOULD 在 2^32 - 65536 帧时提前触发重新协商。

### 4.1 带内重新密钥信号

发起方发送带有 EXT 头字段 `rekey: true` 的帧，接收方 MUST 完成当前帧交换后执行密钥派生，再发送下一帧。

### 4.2 重新密钥失败

若对端在超过 2^32 帧后仍未重新协商，接收方 MUST 发送 `NCP-REKEY-REQUIRED` 并 MUST 关闭连接。

---

## 5. `max_concurrent_streams` 协商

HelloFrame 携带 `max_concurrent_streams`（uint16）；服务端在 CapsFrame 中回显其上限；有效限制为 `min(客户端上限, 服务端上限)`，双向生效。超出时返回 `NCP-STREAM-LIMIT-EXCEEDED`。

默认值：32。最小值：1。最大值：65535。

---

## 6. TLS 绑定与双向认证

本节规范化原生模式 NCP 的传输安全层，并解决此前关于 ALPN 与 TLS 封装的开放问题（OQ-1、OQ-3）。

### 6.1 ALPN token

两种原生模式传输均通告套件级 ALPN token **`nps/1.0`**：

- **TCP**：连接经 TLS 封装（§6.2）并协商 ALPN `nps/1.0`。
- **QUIC**：在 QUIC/TLS 1.3 握手中协商 ALPN `nps/1.0`（§3.1）。

不识别 `nps/1.0` 的服务端 MUST 以 TLS alert `no_application_protocol`（120）终止握手，而非回落到默认协议。

### 6.2 TLS 封装（解决 OQ-3）

原生模式 over TCP MUST 使用 **TLS 封装**传输：先建立 TLS 1.3，RFC-0001 前导（`b"NPS/1.0\n"`）及所有 NCP 帧均在 TLS 会话**内部**传输。原生模式**不允许** STARTTLS 式带内升级（会使前导与 HelloFrame 处于明文）。`local-dev` profile（NPS-4 §7.2）仅可在回环测试中无 TLS 运行。QUIC 内建 TLS 1.3，天然满足本要求。

### 6.3 与 NIP 证书的双向认证（mTLS）

当上层 profile 要求认证时，原生模式 MUST 使用**双向 TLS**：

- 服务端出示 NIP 颁发的证书（NPS-3、RFC-0002 X.509 NID profile），其 NID 与连接通告的节点身份一致。
- 客户端在 TLS 握手中出示其 NIP 证书。服务端 MUST 校验证书链至配置的信任锚（`trust_anchors`，NWP v0.13 §4.1）并将证书 NID 绑定到 NCP 会话。
- 握手后的 `IdentFrame` 仍是能力、scope、保证等级的权威来源；mTLS 证书 NID 与 `IdentFrame` NID MUST 一致，否则服务端 MUST 以 `NCP-NID-MISMATCH` 关闭。

mTLS 是 `nps-ingress`（L2）终结的传输层准入门；参见 NPS-Node Profile L2 要求。

### 6.4 会话恢复

为降低重复短连接的冷启动延迟，服务端 MAY 签发 **TLS 1.3 会话恢复票据**（`NewSessionTicket`）。恢复的会话：

- MUST 重新出示客户端证书（票据不豁免 mTLS）；恢复会话证书 NID 与票据绑定 NID 不同时 MUST 以 `NCP-NID-MISMATCH` 拒绝。
- 仍 MUST 发送 RFC-0001 前导与新的 HelloFrame；恢复仅缩短 TLS 握手，不缩短 NCP 握手。
- SHOULD 受证书剩余有效期与服务端配置的票据生存期约束（建议 ≤ 24 小时，与 §4 重新密钥窗口对齐）。

---

## 7. 合规要求

原生模式合规实现 MUST：

1. 支持 TCP 传输绑定（§2）
2. 在 HelloFrame 之前发送 RFC-0001 前导字节
3. 协商 `max_concurrent_streams`
4. 遵守协商的帧大小限制
5. 在 E2E 加密激活时实现 rekeying（§4）
6. 在任何协议违规关闭前发送 ErrorFrame
7. 在所有非 `local-dev` profile 下使用带 ALPN `nps/1.0` 的 TLS 封装传输（§6.1–§6.2）
8. 与 NIP 证书执行双向 TLS 并将证书 NID 绑定到 NCP 会话，证书/`IdentFrame` NID 不一致时以 `NCP-NID-MISMATCH` 拒绝（§6.3）

原生模式合规实现 SHOULD：

1. 支持 QUIC 传输绑定（§3）
2. 实现正常关闭（CapsFrame `status="closing"`）
3. 为重复短连接提供 TLS 1.3 会话恢复票据（§6.4）

---

## 8. 安全考虑

生产环境中的原生模式连接 MUST 使用 TLS 1.3 或 QUIC 内置加密（`local-dev` 安全配置文件除外，参见 NPS-4 §7.2）。NCP 帧层的 E2E 加密（§7.4）是纵深防御机制，不能替代传输层 TLS。

---

## 9. 问题处置

| ID | 问题 | 状态 |
|----|------|------|
| OQ-1 | IANA 注册 ALPN `nps/1.0` | 已解决（值已选定，§6.1）；IANA 注册在 IETF Internet-Draft 后续 RFC 中跟踪 |
| OQ-2 | QUIC 版本：仅 QUIC v1 (RFC 9000)，还是同时支持 v2？ | 当前契约已决：仅 QUIC v1；QUIC v2 需未来 RFC 修订 |
| OQ-3 | STARTTLS 与 TLS 封装：是否强制其中之一？ | 已解决（§6.2 —— 原生模式强制 TLS 封装；禁止 STARTTLS） |

---

## 10. 变更历史

| 版本 | 日期 | 变更 |
|------|------|------|
| 0.2 状态对账 | 2026-09-05 | 英中均保持 Accepted，并将当前 QUIC 契约固定为 v1；QUIC v2 需要未来修订。|
| 0.2 接受 | 2026-07-05 | Accepted；实现激活仍由独立门禁控制。|
| 0.2 | 2026-06-12 | 草案 → **提议**。新增 §6 TLS 绑定与双向认证：套件级 ALPN `nps/1.0`（取代临时值 `ncp/1`）、原生模式 over TCP 强制 TLS 封装（解决 OQ-3）、与 NIP 证书的 mTLS + session-NID 绑定（`NCP-NID-MISMATCH`）、TLS 1.3 会话恢复票据。§7 合规新增 TLS/mTLS MUST。OQ-1/OQ-3 已解决。§7–§10 重编号。 |
| 0.1 | 2026-05-28 | 初稿 |

---

*归属：LabAcacia / INNO LOTUS PTY LTD · Apache 2.0*
