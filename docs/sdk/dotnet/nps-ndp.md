[English Version](./nps-ndp.en.md) | 中文版

# NPS.NDP API 参考

`NPS.NDP` 提供节点发现功能：AnnounceFrame 广播、nwp:// 地址解析、GraphFrame 图谱同步，以及公告签名验证。

---

## DI 注册

```csharp
builder.Services.AddNdp();
// 注册：INdpRegistry（singleton InMemoryNdpRegistry）、NdpAnnounceValidator（singleton）
```

---

## NDP 帧类型

命名空间：`NPS.NDP.Frames`

### AnnounceFrame

节点/Agent 存在性广播帧：

```csharp
public sealed record AnnounceFrame : IFrame
{
    public required string                   Nid          { get; init; }
    public          string?                  NodeType     { get; init; } // "memory"|"action"|"complex"
    public required IReadOnlyList<NdpAddress> Addresses   { get; init; }
    public required IReadOnlyList<string>    Capabilities { get; init; }
    public required uint                     Ttl          { get; init; } // 秒；0 = 立即过期
    public required string                   Timestamp    { get; init; } // ISO 8601 UTC
    public required string                   Signature    { get; init; } // Ed25519 over canonical JSON
}
```

### NdpAddress

物理地址条目：

```csharp
public sealed record NdpAddress
{
    public required string Host     { get; init; } // hostname 或 IP
    public required int    Port     { get; init; }
    public required string Protocol { get; init; } // "https" | "nps-native"
}
```

**示例：**

```csharp
var announce = new AnnounceFrame
{
    Nid      = "urn:nps:node:api.example.com:products",
    NodeType = "memory",
    Addresses =
    [
        new NdpAddress { Host = "api.example.com", Port = 443,   Protocol = "https" },
        new NdpAddress { Host = "api.example.com", Port = 17433, Protocol = "nps-native" },
    ],
    Capabilities = ["nwp:query", "nwp:stream"],
    Ttl          = 300,
    Timestamp    = DateTime.UtcNow.ToString("O"),
    Signature    = NipSigner.Sign(caPrivateKey, announcePayload),
};
```

---

### ResolveFrame

解析 nwp:// 地址到物理端点（请求/响应复用同一帧类型）：

```csharp
public sealed record ResolveFrame : IFrame
{
    public required string              Target        { get; init; } // nwp://host/path
    public          string?             RequesterNid  { get; init; }
    public          NdpResolveResult?   Resolved      { get; init; } // 响应时填充
}

public sealed record NdpResolveResult
{
    public required string  Host             { get; init; }
    public required int     Port             { get; init; }
    public          string? CertFingerprint  { get; init; }
    public required uint    Ttl              { get; init; }
}
```

**示例：**

```csharp
// 请求
var req = new ResolveFrame
{
    Target       = "nwp://api.example.com/products",
    RequesterNid = "urn:nps:agent:ca.example.com:my-agent",
};

// 使用注册表解析
NdpResolveResult? result = registry.Resolve("nwp://api.example.com/products");
```

---

### GraphFrame

拓扑快照帧：

```csharp
public sealed record GraphFrame : IFrame
{
    public required string                      GraphId  { get; init; }
    public required IReadOnlyList<NdpGraphNode> Nodes    { get; init; } // 最多 256
    public required IReadOnlyList<NdpGraphEdge> Edges    { get; init; } // 最多 1024
    public required uint                        Ttl      { get; init; }
    public          JsonElement?                Metadata { get; init; }
}

public sealed record NdpGraphNode
{
    public required string                Nid           { get; init; }
    public          string?               ClusterAnchor { get; init; }
    public          IReadOnlyList<string>? NodeRoles     { get; init; }
}

public sealed record NdpGraphEdge
{
    public required string FromNid   { get; init; }
    public required string ToNid     { get; init; }
    public          uint?  LatencyMs { get; init; }
    public          string? Protocol { get; init; }
}
```

---

## INdpRegistry

命名空间：`NPS.NDP.Registry`

```csharp
public interface INdpRegistry
{
    // 注册或刷新节点公告（TTL=0 时立即淘汰）
    void Announce(AnnounceFrame frame)

    // 解析 nwp:// 目标 URL 到物理端点
    NdpResolveResult? Resolve(string target)

    // 返回所有当前存活的公告
    IReadOnlyList<AnnounceFrame> GetAll()

    // 返回指定 NID 的公告（已过期则返回 null）
    AnnounceFrame? GetByNid(string nid)
}
```

---

## InMemoryNdpRegistry

命名空间：`NPS.NDP.Registry`

线程安全的内存注册表，使用 `Lock` 同步：

```csharp
public sealed class InMemoryNdpRegistry : INdpRegistry
{
    // 可替换时钟（用于单元测试）
    public Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;

    void Announce(AnnounceFrame frame)        // TTL=0 → 立即淘汰；否则存入或刷新
    NdpResolveResult? Resolve(string target)  // 匹配所有存活 NID，找第一个覆盖 target 的
    IReadOnlyList<AnnounceFrame> GetAll()     // 返回快照（已过期项惰性清除）
    AnnounceFrame? GetByNid(string nid)

    // 判断 nid 是否覆盖指定 target URL（权威段 + 路径前缀匹配）
    static bool NwpTargetMatchesNid(string nid, string target)
}
```

### NwpTargetMatchesNid 说明

`nid` 格式：`urn:nps:node:{authority}:{path-segments}`

匹配规则：提取 nid 中的权威段（第 4 个冒号之后）与 `nwp://` URL 的 authority + path 进行前缀匹配：

```csharp
// nid = "urn:nps:node:api.example.com:products"
// 覆盖 nwp://api.example.com/products 及其子路径
InMemoryNdpRegistry.NwpTargetMatchesNid(
    "urn:nps:node:api.example.com:products",
    "nwp://api.example.com/products/123") // → true

InMemoryNdpRegistry.NwpTargetMatchesNid(
    "urn:nps:node:api.example.com:products",
    "nwp://api.example.com/orders")       // → false
```

---

## NdpAnnounceValidator

命名空间：`NPS.NDP.Validation`

验证 AnnounceFrame 的 Ed25519 签名及 NID 一致性：

```csharp
public sealed class NdpAnnounceValidator
{
    // 注册 NID 对应的公钥（ed25519:{base64url} 格式）
    void RegisterPublicKey(string nid, string encodedPubKey)

    // 移除已注册的公钥
    void RemovePublicKey(string nid)

    // 返回所有已注册公钥的只读视图
    IReadOnlyDictionary<string, string> KnownPublicKeys { get; }

    // 验证 AnnounceFrame（签名 + NID 一致性）
    NdpAnnounceResult Validate(AnnounceFrame frame)
}
```

### NdpAnnounceResult

```csharp
public sealed record NdpAnnounceResult
{
    public bool    IsValid   { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message   { get; init; }

    public static NdpAnnounceResult Ok()
    public static NdpAnnounceResult Fail(string errorCode, string message)
}
```

**示例：**

```csharp
// 节点启动时注册公钥
validator.RegisterPublicKey(
    "urn:nps:node:api.example.com:products",
    NipSigner.EncodePublicKey(nodePubKey));

// 收到公告时验证
var result = validator.Validate(announceFrame);
if (!result.IsValid)
{
    logger.LogWarning("Invalid announce: {Code} {Msg}", result.ErrorCode, result.Message);
    return;
}

// 验证通过后写入注册表
registry.Announce(announceFrame);
```

---

## FrameRegistry 扩展

命名空间：`NPS.NDP.Registry`

```csharp
// 将 NDP 帧类型注册到 FrameRegistryBuilder
builder.AddNdp()
// 注册：AnnounceFrame(0x30)、ResolveFrame(0x31)、GraphFrame(0x32)
```

---

## NDP 错误码

命名空间：`NPS.NDP`

```csharp
NdpErrorCodes.ResolveNotFound          // "NDP-RESOLVE-NOT-FOUND"
NdpErrorCodes.ResolveAmbiguous         // "NDP-RESOLVE-AMBIGUOUS"
NdpErrorCodes.ResolveTimeout           // "NDP-RESOLVE-TIMEOUT"
NdpErrorCodes.ResolveStale             // "NDP-RESOLVE-STALE"
NdpErrorCodes.AnnounceSignatureInvalid // "NDP-ANNOUNCE-SIGNATURE-INVALID"
NdpErrorCodes.AnnounceNidMismatch      // "NDP-ANNOUNCE-NID-MISMATCH"
NdpErrorCodes.AnnounceProfileViolation // "NDP-ANNOUNCE-PROFILE-VIOLATION"
NdpErrorCodes.GraphSeqGap              // "NDP-GRAPH-SEQ-GAP"
NdpErrorCodes.GraphTooLarge            // "NDP-GRAPH-TOO-LARGE"
NdpErrorCodes.GraphInvalid             // "NDP-GRAPH-INVALID"
NdpErrorCodes.FederationLoop           // "NDP-FEDERATION-LOOP"
NdpErrorCodes.RegistryUnavailable      // "NDP-REGISTRY-UNAVAILABLE"
```

---

## 完整使用示例

```csharp
// 1. 节点启动 — 广播存在
var registry  = serviceProvider.GetRequiredService<INdpRegistry>();
var validator = serviceProvider.GetRequiredService<NdpAnnounceValidator>();

// 注册本节点公钥
validator.RegisterPublicKey(myNid, NipSigner.EncodePublicKey(myPubKey));

// 构造并发布公告
var announce = new AnnounceFrame
{
    Nid          = myNid,
    NodeType     = "memory",
    Addresses    = [new NdpAddress { Host = "api.example.com", Port = 443, Protocol = "https" }],
    Capabilities = ["nwp:query"],
    Ttl          = 300,
    Timestamp    = DateTime.UtcNow.ToString("O"),
    Signature    = NipSigner.Sign(myPrivateKey, payload),
};

var r = validator.Validate(announce);
if (r.IsValid) registry.Announce(announce);

// 2. Agent — 解析目标节点
var resolved = registry.Resolve("nwp://api.example.com/products");
if (resolved is null) throw new Exception("Node not found");

var endpoint = $"{resolved.Host}:{resolved.Port}";
```

---

## 相关文档

- [节点类型概念](../../concepts/node-types.md)
- [NPS.NOP API 参考](./nps-nop.md)
- [NDP 规范](../../../spec/NPS-4-NDP.md)
