[English Version](./nps-core.en.md) | 中文版

# NPS.Core API 参考

`NPS.Core` 是协议栈的基础包，提供帧接口定义、全帧命名空间、编解码器、Schema 锚定缓存和异常体系。

---

## DI 注册

```csharp
builder.Services.AddNpsCore(opt =>
{
    opt.DefaultTier               = EncodingTier.MsgPack;
    opt.AnchorTtlSeconds          = 3600;
    opt.MaxFramePayload           = 65535;
    opt.AllowPlaintext            = false;
    opt.EnableExtendedFrameHeader = false;
});
```

注册后，DI 容器中可获取：`FrameRegistry`（singleton）、`Tier1JsonCodec`（singleton）、`Tier2MsgPackCodec`（singleton）、`NpsFrameCodec`（singleton）、`AnchorFrameCache`（scoped）。

---

## NpsCoreOptions

命名空间：`NPS.Core.Extensions`

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `DefaultTier` | `EncodingTier` | `MsgPack` | 默认编码 Tier |
| `AnchorTtlSeconds` | `int` | `3600` | AnchorFrame 缓存 TTL（秒） |
| `AllowPlaintext` | `bool` | `false` | 是否允许明文连接（生产环境必须为 false）|
| `MaxFramePayload` | `uint` | `65535` | 单帧最大载荷字节数（64 KiB）|
| `EnableExtendedFrameHeader` | `bool` | `false` | 允许超过 64 KiB 的载荷并使用 8 字节扩展帧头 |

---

## IFrame

命名空间：`NPS.Core.Frames`

所有帧类型必须实现的接口：

```csharp
public interface IFrame
{
    FrameType    FrameType     { get; }
    EncodingTier PreferredTier { get; }
}
```

---

## FrameType（枚举）

命名空间：`NPS.Core.Frames`

帧类型码，底层类型 `byte`：

```csharp
// NCP (0x01–0x0F)
Anchor      = 0x01   // Schema 锚定
Diff        = 0x02   // 增量补丁
Stream      = 0x03   // 流式分块
Caps        = 0x04   // 完整响应胶囊
Hello       = 0x06   // 原生模式握手

// NWP (0x10–0x1F)
Query       = 0x10   // 结构化查询
Action      = 0x11   // 操作调用

// NIP (0x20–0x2F)
Ident       = 0x20   // 身份声明
Trust       = 0x21   // 跨 CA 信任链
Revoke      = 0x22   // 证书吊销

// NDP (0x30–0x3F)
Announce    = 0x30   // 节点公告
Resolve     = 0x31   // 地址解析
Graph       = 0x32   // 节点图谱同步

// NOP (0x40–0x4F)
Task        = 0x40   // 任务定义
Delegate    = 0x41   // 子任务委托
Sync        = 0x42   // 同步屏障
AlignStream = 0x43   // 有向任务流

// System (0xF0–0xFF)
Error       = 0xFE   // 统一错误帧
```

---

## EncodingTier / FrameFlags（枚举）

命名空间：`NPS.Core.Frames`

```csharp
public enum EncodingTier : byte
{
    Json    = 0x00,   // Tier-1：UTF-8 JSON
    MsgPack = 0x01,   // Tier-2：MessagePack 二进制
}

[Flags]
public enum FrameFlags : byte
{
    None        = 0x00,
    Tier2MsgPack = 0x01,  // bit 0
    Final       = 0x04,   // bit 2：流最后一帧；非流帧必须置 1
    Encrypted   = 0x08,   // bit 3：载荷已加密
    Ext         = 0x80,   // bit 7：8 字节扩展帧头
}
```

---

## FrameHeader

命名空间：`NPS.Core.Frames`

```csharp
public record struct FrameHeader(FrameType FrameType, FrameFlags Flags, uint PayloadLength)
```

### 常量

| 常量 | 值 | 说明 |
|------|----|------|
| `DefaultSize` | `4` | 标准帧头字节数 |
| `ExtendedSize` | `8` | 扩展帧头字节数 |
| `DefaultMaxPayload` | `65535` | 标准模式最大载荷（64 KiB - 1）|
| `ExtendedMaxPayload` | `4294967295` | 扩展模式最大载荷（4 GiB - 1）|

### 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `IsExtended` | `bool` | EXT 标志（bit 7）是否置 1 |
| `HeaderSize` | `int` | 本实例帧头字节数（4 或 8）|
| `EncodingTier` | `EncodingTier` | 从 Flags 低 2 位提取的编码 Tier |
| `IsFinal` | `bool` | FINAL 标志（bit 2）是否置 1 |
| `IsEncrypted` | `bool` | ENC 标志（bit 3）是否置 1 |

### 方法

```csharp
// 从字节缓冲区解析帧头（自动检测 4/8 字节模式）
static FrameHeader Parse(ReadOnlySpan<byte> buffer)

// 将帧头写入字节缓冲区
void WriteTo(Span<byte> buffer)
```

---

## NCP 帧类型

命名空间：`NPS.Core.Frames.Ncp`

### AnchorFrame

Schema 锚定帧。首次建立 Schema 引用，供后续帧通过 `anchor_id` 引用。

```csharp
public sealed record AnchorFrame : IFrame
{
    public required string      AnchorId { get; init; } // sha256:{64 hex 字符}
    public required FrameSchema Schema   { get; init; }
    public          uint        Ttl      { get; init; } = 3600; // 秒；0 = 禁用缓存
}
```

**示例：**

```csharp
var schema = new FrameSchema
{
    Fields =
    [
        new SchemaField("id",    "uint64",  Semantic: "entity.id"),
        new SchemaField("name",  "string",  Semantic: "entity.label"),
        new SchemaField("price", "decimal", Semantic: "commerce.price.usd"),
    ]
};

var frame = new AnchorFrame
{
    AnchorId = AnchorIdComputer.Compute(schema),
    Schema   = schema,
    Ttl      = 3600,
};

byte[] wire = codec.Encode(frame);
```

---

### DiffFrame

增量补丁帧。仅传输变更字段，大幅降低高频更新场景的 token 消耗。

```csharp
public sealed record DiffFrame : IFrame
{
    public required string                     AnchorRef   { get; init; }
    public required ulong                      BaseSeq     { get; init; }
    public          string                     PatchFormat { get; init; } = "json_patch";
    public          IReadOnlyList<JsonPatchOperation>? Patch { get; init; }
    public          byte[]?                    BinaryBitset { get; init; }
    public          string?                    EntityId     { get; init; }
}
```

`JsonPatchOperation` 对应 RFC 6902，`Op` 取值：`add`、`remove`、`replace`、`move`、`copy`、`test`。

`PatchFormat` 取值（见 `NcpPatchFormat`）：

| 值 | 说明 |
|----|------|
| `"json_patch"` | RFC 6902 JSON Patch，兼容所有 Tier |
| `"binary_bitset"` | 紧凑二进制格式，**仅限** Tier-2（MsgPack）使用 |

**示例：**

```csharp
var diff = new DiffFrame
{
    AnchorRef = anchorId,
    BaseSeq   = 42,
    Patch     =
    [
        new JsonPatchOperation("replace", "/price", Value: JsonSerializer.SerializeToElement(99.99m)),
    ],
};
```

---

### StreamFrame

流式分块帧。支持大结果集的分页流式传输，具备反压（Back-pressure）机制。

```csharp
public sealed record StreamFrame : IFrame
{
    public required string                          StreamId   { get; init; }
    public required uint                            Seq        { get; init; }
    public required bool                            IsLast     { get; init; }
    public          string?                         AnchorRef  { get; init; }
    public required IReadOnlyList<JsonElement>      Data       { get; init; }
    public          uint?                           WindowSize { get; init; }
    public          string?                         ErrorCode  { get; init; }
}
```

**流协议：**
- `Seq` 从 0 开始严格单调递增
- `IsLast = true` 时表示流结束；若同时 `ErrorCode != null` 则流异常终止
- `WindowSize` 用于反压控制：消费方处理完一批后向发送方通知可接收的新窗口大小

---

### CapsFrame

完整响应胶囊。包含按 Schema 描述的完整数据行，支持游标分页。

```csharp
public sealed record CapsFrame : IFrame
{
    public required string                              AnchorRef    { get; init; }
    public required uint                                Count        { get; init; }
    public required IReadOnlyList<JsonElement>          Data         { get; init; }
    public          string?                             NextCursor   { get; init; }
    public          uint?                               TokenEst     { get; init; }
    public          bool?                               Cached       { get; init; }
    public          string?                             TokenizerUsed { get; init; }
    public          AnchorFrame?                        InlineAnchor  { get; init; }
}
```

`InlineAnchor`：当 Schema 已更新时，节点应内联最新 AnchorFrame，避免客户端再发一次请求。

---

### HelloFrame

原生模式握手帧。客户端发出，声明支持的 NPS 版本、编码格式和协议。

```csharp
public sealed record HelloFrame : IFrame
{
    public required string               NpsVersion          { get; init; } // "0.4"
    public          string?              MinVersion          { get; init; }
    public required IReadOnlyList<string> SupportedEncodings { get; init; }
    public required IReadOnlyList<string> SupportedProtocols { get; init; }
    public          string?              AgentId             { get; init; }
    public          uint                 MaxFramePayload     { get; init; } = 65535;
    public          bool                 ExtSupport          { get; init; } = false;
    public          uint                 MaxConcurrentStreams { get; init; } = 32;
    public          IReadOnlyList<string>? E2EEncAlgorithms  { get; init; }
}
```

---

### ErrorFrame

统一错误帧，所有协议层共用。

```csharp
public sealed record ErrorFrame : IFrame
{
    public required string       Status  { get; init; } // NPS 状态码
    public required string       Error   { get; init; } // 协议层错误码
    public          string?      Message { get; init; }
    public          JsonElement? Details { get; init; }
}
```

---

## FrameSchema / SchemaField

命名空间：`NPS.Core.Frames`

```csharp
public record FrameSchema
{
    public required IReadOnlyList<SchemaField> Fields { get; init; }
}

// 位置记录（Positional Record）
public record SchemaField(
    string  Name,
    string  Type,
    string? Semantic = null,
    bool    Nullable = false
);
```

---

## NpsFrameCodec

命名空间：`NPS.Core.Codecs`

主编解码器，负责帧头读写和 Tier 路由：

```csharp
public class NpsFrameCodec
{
    // 构造（通常由 DI 注入）
    public NpsFrameCodec(
        Tier1JsonCodec          json,
        Tier2MsgPackCodec       msgpack,
        Tier3BinaryVectorCodec  binaryVector,
        FrameRegistry           registry,
        uint                    maxPayload = 65535)

    // 编码：序列化帧并写入帧头；overrideTier 用于已协商策略、诊断和测试，
    // 不表示业务流程中可以任意切换 Tier。
    public byte[] Encode(IFrame frame, EncodingTier? overrideTier = null)

    // 解码：从完整 wire 消息（帧头 + 载荷）还原强类型帧
    public IFrame Decode(ReadOnlySpan<byte> wire)

    // 仅解析帧头，不反序列化载荷
    public static FrameHeader PeekHeader(ReadOnlySpan<byte> wire)
}
```

**示例：**

```csharp
// 使用 DI 注入
public class MyService(NpsFrameCodec codec)
{
    public byte[] EncodeQuery(QueryFrame q) => codec.Encode(q);

    public IFrame ParseIncoming(byte[] wire) => codec.Decode(wire);

    // 强制使用 JSON（调试场景）
    public byte[] DebugEncode(IFrame f) => codec.Encode(f, EncodingTier.Json);
}
```

---

## Tier1JsonCodec / Tier2MsgPackCodec

命名空间：`NPS.Core.Codecs`

底层 Tier 编解码器，实现 `IFrameCodec`：

```csharp
public interface IFrameCodec
{
    byte[] Encode(IFrame frame);
    IFrame Decode(FrameType type, ReadOnlySpan<byte> payload, FrameRegistry registry);
}
```

通常无需直接使用——通过 `NpsFrameCodec` 访问即可。

---

## FrameRegistry / FrameRegistryBuilder

命名空间：`NPS.Core.Registry`

帧类型到 CLR 类型的映射注册表：

```csharp
// 构建（链式调用）
var registry = new FrameRegistryBuilder()
    .AddNcp()   // 注册所有 NCP 核心帧
    .AddNwp()   // 注册 NWP 帧（QueryFrame、ActionFrame）
    .AddNip()   // 注册 NIP 帧（IdentFrame、TrustFrame、RevokeFrame）
    .AddNdp()   // 注册 NDP 帧（AnnounceFrame、ResolveFrame、GraphFrame）
    .Build();

// 查询
Type clrType = registry.Resolve(FrameType.Anchor); // → typeof(AnchorFrame)
```

---

## AnchorFrameCache

命名空间：`NPS.Core.Caching`

线程安全的 Schema 内存缓存（DI 生命周期：Scoped）：

```csharp
// 存储并返回 anchor_id（若 Schema 冲突则抛出 NpsAnchorPoisonException）
string anchorId = cache.Set(anchorFrame);

// 尝试获取
if (cache.TryGet(anchorId, out AnchorFrame? found))
{
    // 使用 found.Schema
}

// 强制获取（未找到则抛出 NpsAnchorNotFoundException）
AnchorFrame frame = cache.GetRequired(anchorId);
```

---

## AnchorIdComputer

命名空间：`NPS.Core.Anchoring`

```csharp
// 计算 anchor_id（sha256:{hex}）
string id = AnchorIdComputer.Compute(schema);

// 调试用：返回用于计算的规范 JSON 字符串
string json = AnchorIdComputer.CanonicalJson(schema);
```

---

## 异常体系

命名空间：`NPS.Core.Exceptions`

```
NpsException
├── NpsFrameException           — 帧解析或结构非法
├── NpsCodecException           — 编解码层失败
├── NpsAnchorNotFoundException  — anchor_id 未在缓存中找到
├── NpsAnchorPoisonException    — 相同 anchor_id 对应冲突 Schema
├── NpsAnchorStaleException     — anchor_id 已知但 Schema 已更新
├── NpsStreamException          — 流帧序号错误或流非法终止
└── NpsVersionIncompatibleException — 客户端 min_version 高于服务端支持版本
```

所有异常均含 `NpsStatusCode`（NPS 状态码）和 `ProtocolErrorCode`（协议层错误码）属性。

**示例：**

```csharp
try
{
    var frame = codec.Decode(wire);
}
catch (NpsAnchorNotFoundException ex)
{
    // ex.AnchorId     — 缺失的 anchor_id
    // ex.NpsStatusCode — "NPS-CLIENT-NOT-FOUND"
    // ex.ProtocolErrorCode — "NCP-ANCHOR-NOT-FOUND"
    logger.LogWarning("Schema anchor missing: {Id}", ex.AnchorId);
}
catch (NpsFrameException ex)
{
    logger.LogError("Malformed frame: {Msg}", ex.Message);
}
```

---

## 状态码与错误码常量

### NpsStatusCodes

命名空间：`NPS.Core`

```csharp
NpsStatusCodes.Ok                        // "NPS-OK"
NpsStatusCodes.OkAccepted                // "NPS-OK-ACCEPTED"
NpsStatusCodes.ClientBadFrame            // "NPS-CLIENT-BAD-FRAME"
NpsStatusCodes.ClientNotFound            // "NPS-CLIENT-NOT-FOUND"
NpsStatusCodes.AuthUnauthenticated       // "NPS-AUTH-UNAUTHENTICATED"
NpsStatusCodes.AuthForbidden             // "NPS-AUTH-FORBIDDEN"
NpsStatusCodes.LimitBudget               // "NPS-LIMIT-BUDGET"
NpsStatusCodes.ServerInternal            // "NPS-SERVER-INTERNAL"
// … 完整列表见 spec/status-codes.md
```

### NcpErrorCodes

命名空间：`NPS.Core`

```csharp
NcpErrorCodes.AnchorNotFound             // "NCP-ANCHOR-NOT-FOUND"
NcpErrorCodes.AnchorSchemaInvalid        // "NCP-ANCHOR-SCHEMA-INVALID"
NcpErrorCodes.AnchorStale               // "NCP-ANCHOR-STALE"
NcpErrorCodes.StreamSeqGap              // "NCP-STREAM-SEQ-GAP"
NcpErrorCodes.EncodingUnsupported       // "NCP-ENCODING-UNSUPPORTED"
NcpErrorCodes.VersionIncompatible       // "NCP-VERSION-INCOMPATIBLE"
// … 完整列表见 spec/error-codes.md
```

---

## 相关文档

- [帧模型概念](../../concepts/frame-model.md)
- [NPS.NWP API 参考](./nps-nwp.md)
- [NCP 规范](../../../spec/NPS-1-NCP.md)
