[English Version](./frame-model.en.md) | 中文版

# 帧模型

NPS 所有协议层的通信单元统称为**帧（Frame）**。每个帧由一个固定格式的**帧头**和一段按编码 Tier 序列化的**载荷**组成。

---

## 帧命名空间

所有帧类型通过一个单字节的类型码（`FrameType`）标识，由 `spec/frame-registry.yaml` 统一管理：

| 范围 | 协议 | 类型码 | 帧名称 |
|------|------|--------|--------|
| 0x01–0x0F | NCP | `0x01` | AnchorFrame — Schema 锚定 |
| | | `0x02` | DiffFrame — 增量补丁 |
| | | `0x03` | StreamFrame — 流式分块 |
| | | `0x04` | CapsFrame — 完整响应胶囊 |
| | | `0x06` | HelloFrame — 原生模式握手 |
| 0x10–0x1F | NWP | `0x10` | QueryFrame — 结构化查询 |
| | | `0x11` | ActionFrame — 操作调用 |
| 0x20–0x2F | NIP | `0x20` | IdentFrame — 身份声明 |
| | | `0x21` | TrustFrame — 跨 CA 信任链 |
| | | `0x22` | RevokeFrame — 证书吊销 |
| 0x30–0x3F | NDP | `0x30` | AnnounceFrame — 节点公告 |
| | | `0x31` | ResolveFrame — 地址解析 |
| | | `0x32` | GraphFrame — 节点图谱同步 |
| 0x40–0x4F | NOP | `0x40` | TaskFrame — 任务定义 |
| | | `0x41` | DelegateFrame — 子任务委托 |
| | | `0x42` | SyncFrame — 同步屏障 |
| | | `0x43` | AlignStreamFrame — 有向任务流 |
| 0xF0–0xFF | System | `0xFE` | ErrorFrame — 统一错误帧 |

---

## 编码 Tier

每个帧通过帧头的低 2 位指定编码格式（`EncodingTier`）：

| Tier | 标识符 | 帧头低 2 位 | 说明 |
|------|--------|------------|------|
| Tier-1 | `Json` | `0x00` | UTF-8 JSON，人类可读，适用于开发调试和兼容模式 |
| Tier-2 | `MsgPack` | `0x01` | MessagePack 二进制，体积约为 JSON 的 40%，生产环境默认 |
| Tier-3 | `BinaryVector` v1 | `0x02` | 可选的向量密集型 Payload 编码；需协商 `binary_vector.v1`；MessagePack 元数据 + little-endian float32 向量段 |

每个帧类型有一个首选 Tier（`IFrame.PreferredTier`）。`overrideTier` 是会话策略、测试和诊断用的策略入口；应用代码不应在同一业务流程中随意切换 Tier。

---

## 帧头格式

### 标准模式（4 字节，默认）

```
 0       1       2       3
 ┌───────┬───────┬───────────────┐
 │ Type  │ Flags │ PayloadLength │
 │ 1 B   │ 1 B   │ 2 B (BE)      │
 └───────┴───────┴───────────────┘
```

- **Type**：`FrameType` 枚举值（1 字节）
- **Flags**：标志字节（1 字节），见下方说明
- **PayloadLength**：载荷长度，大端字节序（最大 65 535 字节 / 64 KiB）

### 扩展模式（8 字节，EXT=1）

当载荷超过 64 KiB（且 `EnableExtendedFrameHeader = true`）时，帧头自动升级为 8 字节：

```
 0       1       2       3       4       5       6       7
 ┌───────┬───────┬───────────────────────────────────────┐
 │ Type  │ Flags │ PayloadLength (4 B, BE)                │
 │ 1 B   │ 1 B   │ 最大 4 294 967 295 字节 / 4 GiB        │
 └───────┴───────┴───────────────────────────────────────┘
```

`Flags` 字节的 bit 7（`EXT`）置 1 表示扩展模式。

---

## 帧标志位（Flags）

| 位 | 名称 | 值 | 说明 |
|----|------|----|------|
| 1–0 | Encoding Tier | `0x00`/`0x01` | 低 2 位，编码 Tier |
| 2 | FINAL | `0x04` | 置 1 表示流的最后一帧；所有非流帧必须置 1 |
| 3 | ENC | `0x08` | 置 1 表示载荷已加密；生产环境必须置 1 |
| 7 | EXT | `0x80` | 置 1 表示使用 8 字节扩展帧头 |

---

## Schema 锚定机制

NPS 的核心优化之一是避免重复传输 Schema。流程如下：

1. **首次**：服务端发送 `AnchorFrame`，携带完整 `FrameSchema`，并附带 `anchor_id`（Schema 的 SHA-256 摘要）。
2. **后续**：响应帧（如 `CapsFrame`）仅携带 `anchor_id` 引用，不重复传输 Schema。
3. **客户端缓存**：`AnchorFrameCache` 在内存中缓存 Schema，TTL 默认 3600 秒。
4. **冲突检测**：若相同 `anchor_id` 对应不同 Schema，抛出 `NpsAnchorPoisonException`。

### anchor_id 计算规则

```
anchor_id = "sha256:" + SHA256( RFC8785_JCS_Canonical_JSON(schema) )
```

Schema 字段按名称排序后序列化，确保顺序无关性（NPS-1 §4.1）。

### FrameSchema 字段类型

| 类型字符串 | 说明 |
|-----------|------|
| `uint64` | 无符号 64 位整数 |
| `int32` | 有符号 32 位整数 |
| `string` | UTF-8 字符串 |
| `decimal` | 十进制数（精确） |
| `bool` | 布尔值 |
| `timestamp` | ISO 8601 UTC 时间戳 |
| `bytes` | 字节序列（Base64 编码传输） |

语义注解（`Semantic`）采用点分格式，供 AI 模型理解语义，如 `entity.id`、`commerce.price.usd`。

---

## 错误帧

所有协议层共用同一个错误帧类型 `ErrorFrame (0xFE)`：

```json
{
  "status": "NPS-CLIENT-NOT-FOUND",
  "error":  "NCP-ANCHOR-NOT-FOUND",
  "message": "anchor_id sha256:a3f9... not found in cache"
}
```

- `status`：NPS 状态码，见 `spec/status-codes.md`
- `error`：协议层错误码，见 `spec/error-codes.md`

---

## 相关文档

- [NCP 规范](../../spec/NPS-1-NCP.md)
- [帧注册表](../../spec/frame-registry.yaml)
- [错误码命名空间](../../spec/error-codes.md)
- [NPS.Core SDK 参考](../sdk/dotnet/nps-core.md)
