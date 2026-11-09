[English Version](./nps-nip.en.md) | 中文版

# NPS.NIP API 参考

`NPS.NIP` 提供两个独立的子系统：
- **CA 服务（`NipCaService`）**：证书颁发机构，负责 NID 注册、证书颁发/续期/吊销。
- **节点端验证器（`NipIdentVerifier`）**：节点侧的 IdentFrame 六步校验。

---

## DI 注册

### 仅验证器（不含 CA）

适用于普通节点，只需验证传入的 IdentFrame：

```csharp
builder.Services.AddNipVerifier(opt =>
{
    opt.TrustedIssuers = new Dictionary<string, string>
    {
        ["urn:nps:org:ca.example.com"] = "https://ca.example.com"
    };
    opt.OcspUrl            = "https://ca.example.com/v1/agents/{nid}/verify";
    opt.LocalRevokedSerials = null; // 或提供本地吊销集合
});
```

### CA 服务（完整）

适用于搭建 NIP CA Server：

```csharp
builder.Services.AddNipCa(opt =>
{
    opt.CaNid                = "urn:nps:org:ca.example.com";
    opt.DisplayName          = "Example NPS CA";
    opt.KeyFilePath          = "/secrets/ca.key";
    opt.KeyPassphrase        = Environment.GetEnvironmentVariable("CA_PASSPHRASE")!;
    opt.BaseUrl              = "https://ca.example.com";
    opt.ConnectionString     = Environment.GetEnvironmentVariable("DB_CONN")!;
    opt.AgentCertValidityDays = 30;
    opt.NodeCertValidityDays  = 90;
    opt.RenewalWindowDays     = 7;
}, generateKeyIfMissing: true);

// 映射 CA HTTP 路由
app.MapNipCa();
```

---

## NIP 帧类型

命名空间：`NPS.NIP.Frames`

### IdentFrame

Agent 身份声明帧，携带 CA 颁发的证书信息：

```csharp
public sealed record IdentFrame : IFrame
{
    public required string                  Nid          { get; init; } // urn:nps:agent:{ca}:{id}
    public required string                  PubKey       { get; init; } // ed25519:{base64url}
    public required IReadOnlyList<string>   Capabilities { get; init; }
    public required JsonElement             Scope        { get; init; }
    public required string                  IssuedBy     { get; init; }
    public required string                  IssuedAt     { get; init; } // ISO 8601 UTC
    public required string                  ExpiresAt    { get; init; }
    public required string                  Serial       { get; init; }
    public required string                  Signature    { get; init; } // Ed25519 over canonical JSON
    public          IdentMetadata?          Metadata     { get; init; }
}

public sealed record IdentMetadata
{
    public string? ModelFamily { get; init; } // e.g. "claude"
    public string? Tokenizer   { get; init; } // e.g. "cl100k_base"
    public string? Runtime     { get; init; } // e.g. "dotnet-10"
}
```

---

### TrustFrame

跨 CA 信任链委托帧：

```csharp
public sealed record TrustFrame : IFrame
{
    public required string                  GrantorNid  { get; init; }
    public required string                  GranteeCa   { get; init; }
    public required IReadOnlyList<string>   TrustScope  { get; init; }
    public required IReadOnlyList<string>   Nodes       { get; init; }
    public required string                  IssuedAt    { get; init; }
    public required string                  ExpiresAt   { get; init; }
    public required string                  Serial      { get; init; }
    public required string                  SignerNid   { get; init; }
    public required string                  Signature   { get; init; }
}
```

---

### RevokeFrame

证书吊销帧：

```csharp
public sealed record RevokeFrame : IFrame
{
    public required string TargetNid { get; init; }
    public          string? Serial    { get; init; }
    public required string Reason    { get; init; }
    public required string RevokedAt { get; init; }
    public          string? ParentNid { get; init; }
    public required string SignerNid { get; init; }
    public required string Signature { get; init; }
}
```

---

## NipIdentVerifier（节点端验证器）

命名空间：`NPS.NIP.Verification`

### 六步验证流程

| 步骤 | 内容 | 失败错误码 |
|------|------|-----------|
| 1 | 证书有效期：`ExpiresAt > now` | `NIP-CERT-EXPIRED` |
| 2 | 颁发者信任：`IssuedBy` 在 `TrustedIssuers` 中 | `NIP-CERT-UNTRUSTED-ISSUER` |
| 3 | 签名验证：Ed25519 验签 | `NIP-CERT-SIGNATURE-INVALID` |
| 4 | 本地 CRL + OCSP 查询 | `NIP-CERT-REVOKED` / `NIP-OCSP-UNAVAILABLE` |
| 5 | 能力检查：NID 必须声明所有 `RequiredCapabilities` | `NIP-CERT-CAPABILITY-MISSING` |
| 6 | Scope 校验：`TargetNodePath` 被 NID Scope 覆盖 | `NIP-CERT-SCOPE-VIOLATION` |

```csharp
public sealed class NipIdentVerifier
{
    public NipIdentVerifier(
        NipVerifierOptions     opts,
        IHttpClientFactory?    httpFactory = null,
        ILogger<NipIdentVerifier>? logger = null)

    // 验证 IdentFrame，返回验证结果
    public Task<NipIdentVerifyResult> VerifyAsync(
        IdentFrame          frame,
        NipVerifyContext?   context = null,
        CancellationToken   ct = default)

    // 工具方法：检查 NWP 路径是否与 scope 模式匹配（支持 * 通配）
    public static bool NwpPathMatches(string pattern, string path)
}
```

### NipVerifierOptions

```csharp
public sealed class NipVerifierOptions
{
    // CA NID -> CA 基础 URL 映射（用于在线 verify / OCSP 风格查询）
    public required Dictionary<string, string> TrustedIssuers     { get; set; }
    public          string?                    OcspUrl            { get; set; }
    public          IReadOnlySet<string>?      LocalRevokedSerials { get; set; }
}
```

### NipVerifyContext

每次请求的上下文（步骤 5–6 依赖）：

```csharp
public sealed record NipVerifyContext
{
    public IReadOnlyList<string>? RequiredCapabilities { get; init; }
    public string?                TargetNodePath       { get; init; } // 目标节点路径
    public DateTime?              AsOf                 { get; init; } // 校验时间点（默认 now）
}
```

### NipIdentVerifyResult

```csharp
public sealed record NipIdentVerifyResult
{
    public bool    IsValid     { get; init; }
    public string? ErrorCode   { get; init; }
    public string? Message     { get; init; }
    public int     FailedStep  { get; init; } // 0 = 成功，1-6 = 失败步骤

    public static NipIdentVerifyResult Ok()
    public static NipIdentVerifyResult Fail(int step, string errorCode, string message)
}
```

**示例：**

```csharp
// 在 ASP.NET Core 中间件中验证传入请求
public class NipAuthMiddleware(RequestDelegate next, NipIdentVerifier verifier)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var agentHeader = ctx.Request.Headers[NwpHttpHeaders.Agent].FirstOrDefault();
        if (agentHeader is null) { ctx.Response.StatusCode = 401; return; }

        // 从请求头或请求体中解析 IdentFrame
        var frame = ParseIdentFrame(agentHeader);

        var result = await verifier.VerifyAsync(frame, new NipVerifyContext
        {
            RequiredCapabilities = ["nwp:query"],
            TargetNodePath       = ctx.Request.Path,
        });

        if (!result.IsValid)
        {
            ctx.Response.StatusCode = 403;
            await ctx.Response.WriteAsJsonAsync(new { error = result.ErrorCode });
            return;
        }

        await next(ctx);
    }
}
```

**`NwpPathMatches` 用法：**

```csharp
// 支持 * 通配符（单段）和 ** 通配符（多段）
NipIdentVerifier.NwpPathMatches("/products/*",  "/products/123")   // true
NipIdentVerifier.NwpPathMatches("/admin/**",    "/admin/a/b/c")    // true
NipIdentVerifier.NwpPathMatches("/products",    "/orders")         // false
```

---

## NipCaService（CA 业务逻辑）

命名空间：`NPS.NIP.Ca`

```csharp
public sealed class NipCaService
{
    public NipCaService(NipCaOptions opts, INipCaStore store, NipKeyManager keys)

    // 注册新 Agent/Node 并颁发 IdentFrame
    public Task<IdentFrame> RegisterAsync(
        string                  entityType,   // "agent" | "node"
        string                  identifier,
        string                  pubKey,       // ed25519:{base64url}
        IReadOnlyList<string>   capabilities,
        string                  scopeJson,
        string?                 metadataJson = null,
        CancellationToken       ct = default)

    // 续期证书（仅在续期窗口期内有效）
    public Task<IdentFrame> RenewAsync(string nid, CancellationToken ct = default)

    // 立即吊销证书
    public Task<RevokeFrame> RevokeAsync(string nid, string reason, CancellationToken ct = default)

    // 校验 NID 是否有效（返回 NipVerifyResult）
    public Task<NipVerifyResult> VerifyAsync(string nid, CancellationToken ct = default)

    // 返回当前 CRL（所有已吊销证书记录）
    public Task<IReadOnlyList<NipCertRecord>> GetCrlAsync(CancellationToken ct = default)

    // 返回 CA 公钥（ed25519:{base64url} 格式）
    public string GetCaPublicKey()

    // 构建 NID 字符串
    public string BuildNid(string entityType, string identifier)
}
```

### NipVerifyResult

```csharp
public sealed class NipVerifyResult
{
    public bool           Valid     { get; init; }
    public string?        ErrorCode { get; init; }
    public string?        Message   { get; init; }
    public NipCertRecord? Record    { get; init; }

    public static NipVerifyResult Ok(NipCertRecord record)
    public static NipVerifyResult Fail(string errorCode, string message)
}
```

**示例：**

```csharp
// 颁发 Agent 证书
var identFrame = await caService.RegisterAsync(
    entityType:   "agent",
    identifier:   "gpt-agent-01",
    pubKey:       NipSigner.EncodePublicKey(agentPubKey),
    capabilities: ["nwp:query", "nwp:action"],
    scopeJson:    """{"paths": ["/products/*", "/orders/*"]}""");

// 吊销
var revokeFrame = await caService.RevokeAsync(
    "urn:nps:agent:ca.example.com:gpt-agent-01",
    reason: "key_compromise");
```

---

## NipCaOptions

命名空间：`NPS.NIP.Ca`

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `CaNid` | `string` | 必填 | CA NID 标识符 |
| `DisplayName` | `string?` | `null` | 人类可读名称 |
| `KeyFilePath` | `string` | 必填 | CA 私钥文件路径（AES-256-GCM 加密）|
| `KeyPassphrase` | `string` | 必填 | 密钥文件解密口令 |
| `AgentCertValidityDays` | `int` | `30` | Agent 证书有效期（天）|
| `NodeCertValidityDays` | `int` | `90` | Node 证书有效期（天）|
| `RenewalWindowDays` | `int` | `7` | 证书可续期时间窗口（到期前 N 天）|
| `BaseUrl` | `string` | 必填 | CA 服务器基础 URL |
| `RoutePrefix` | `string` | `""` | HTTP 路由前缀 |
| `ConnectionString` | `string` | 必填 | PostgreSQL 连接字符串 |
| `NormalizeOcspResponseTime` | `bool` | `true` | OCSP 响应最短 200ms（防时序攻击）|
| `Algorithms` | `IReadOnlyList<string>` | `["ed25519"]` | 支持的签名算法 |

---

## NipCertRecord

命名空间：`NPS.NIP.Ca`

CA 数据库中的证书记录：

```csharp
public sealed class NipCertRecord
{
    public required string    Nid          { get; init; }
    public required string    EntityType   { get; init; }
    public required string    Serial       { get; init; }
    public required string    PubKey       { get; init; }
    public required string[]  Capabilities { get; init; }
    public required string    ScopeJson    { get; init; }
    public required string    IssuedBy     { get; init; }
    public required DateTime  IssuedAt     { get; init; }
    public required DateTime  ExpiresAt    { get; init; }
    public          DateTime? RevokedAt    { get; init; }
    public          string?   RevokeReason { get; init; }
    public          string?   MetadataJson { get; init; }
}
```

---

## INipCaStore

命名空间：`NPS.NIP.Ca`

持久化抽象接口，内置实现为 `PostgreSqlNipCaStore`：

```csharp
public interface INipCaStore
{
    Task SaveAsync(NipCertRecord record, CancellationToken ct = default)
    Task<NipCertRecord?> GetByNidAsync(string nid, CancellationToken ct = default)
    Task<NipCertRecord?> GetBySerialAsync(string serial, CancellationToken ct = default)
    Task<bool> RevokeAsync(string nid, string reason, DateTime revokedAt, CancellationToken ct = default)
    Task<string> NextSerialAsync(CancellationToken ct = default)
    Task<IReadOnlyList<NipCertRecord>> GetRevokedAsync(CancellationToken ct = default)
}
```

---

## 密码学工具

### NipSigner

命名空间：`NPS.NIP.Crypto`

Ed25519 签名与验证：

```csharp
public static class NipSigner
{
    // 对载荷规范 JSON 进行 Ed25519 签名，返回 base64url 编码的签名
    static string Sign(Key caPrivateKey, object payload)

    // 验证签名
    static bool Verify(PublicKey caPubKey, object payload, string signature)

    // 公钥编解码（ed25519:{base64url}）
    static string     EncodePublicKey(PublicKey pubKey)
    static PublicKey? DecodePublicKey(string encoded)

    // 规范 JSON（属性按字母序，无空白）
    static string CanonicalJson(object obj)

    // Base64URL 编解码
    static string Base64Url(byte[] data)
    static byte[] FromBase64Url(string s)
}
```

### NipKeyManager

命名空间：`NPS.NIP.Crypto`

CA 密钥对管理（PBKDF2 + AES-256-GCM 加密存储）：

```csharp
public sealed class NipKeyManager : IDisposable
{
    // 常量
    const int NonceSize    = 12;       // AES-GCM nonce
    const int TagSize      = 16;       // AES-GCM tag
    const int Pbkdf2Iters  = 600_000;  // PBKDF2-SHA256 迭代次数
    const int RawKeyBytes  = 32;       // Ed25519 原始密钥长度

    // 属性
    PublicKey PublicKey  { get; }
    Key       PrivateKey { get; }
    bool      IsLoaded   { get; }

    // 生成新密钥对并加密保存到文件
    void Generate(string keyFilePath, string passphrase)

    // 从加密文件加载密钥对
    void Load(string keyFilePath, string passphrase)

    void Dispose()
}
```

---

## NIP 错误码

命名空间：`NPS.NIP.Ca`

```csharp
NipErrorCodes.CertExpired      // "NIP-CERT-EXPIRED"
NipErrorCodes.CertRevoked      // "NIP-CERT-REVOKED"
NipErrorCodes.CertSigInvalid   // "NIP-CERT-SIGNATURE-INVALID"
NipErrorCodes.CertUntrusted    // "NIP-CERT-UNTRUSTED-ISSUER"
NipErrorCodes.CertCapMissing   // "NIP-CERT-CAPABILITY-MISSING"
NipErrorCodes.CertScope        // "NIP-CERT-SCOPE-VIOLATION"
NipErrorCodes.NidNotFound      // "NIP-CA-NID-NOT-FOUND"
NipErrorCodes.NidAlreadyExists // "NIP-CA-NID-ALREADY-EXISTS"
NipErrorCodes.SerialDuplicate  // "NIP-CA-SERIAL-DUPLICATE"
NipErrorCodes.RenewalTooEarly  // "NIP-CA-RENEWAL-TOO-EARLY"
NipErrorCodes.ScopeExpansion   // "NIP-CA-SCOPE-EXPANSION-DENIED"
NipErrorCodes.OcspUnavailable  // "NIP-OCSP-UNAVAILABLE"
NipErrorCodes.TrustInvalid     // "NIP-TRUST-FRAME-INVALID"
```

---

## CA HTTP 路由

`MapNipCa()` 自动挂载以下端点：

| 路径 | 方法 | 功能 |
|------|------|------|
| `/.well-known/nps-ca` | `GET` | CA discovery 文档 |
| `/v1/ca/cert` | `GET` | 返回 CA 公钥 |
| `/v1/crl` | `GET` | 获取带签名的吊销列表 |
| `/v1/agents/register` | `POST` | 注册 Agent 并颁发 IdentFrame |
| `/v1/nodes/register` | `POST` | 注册节点并颁发 IdentFrame |
| `/v1/agents/{nid}/renew` | `POST` | Agent 证书续期 |
| `/v1/nodes/{nid}/renew` | `POST` | 节点证书续期 |
| `/v1/agents/{nid}/revoke` | `POST` | 吊销 Agent 证书 |
| `/v1/nodes/{nid}/revoke` | `POST` | 吊销节点证书 |
| `/v1/agents/{nid}/verify` | `GET` | Agent 的 OCSP 风格在线状态校验 |
| `/v1/nodes/{nid}/verify` | `GET` | 节点的 OCSP 风格在线状态校验 |
| `/v1/agents/register-x509` | `POST` | 注册 Agent 并颁发 X.509-backed IdentFrame |
| `/v1/nodes/register-x509` | `POST` | 注册节点并颁发 X.509-backed IdentFrame |
| `/v1/orchestrators/groups/register` | `POST` | 注册编排器组 NID |
| `/v1/orchestrators/groups/{group_nid}/sessions/issue` | `POST` | 在组下签发短寿命 session NID |
| `/v1/orchestrators/groups/{group_nid}/revoke` | `POST` | 吊销组并级联吊销 live sessions |
| `/v1/orchestrators/groups/{group_nid}/sessions` | `GET` | 列出组下 sessions |

---

## 相关文档

- [NID 身份体系概念](../../concepts/nid-identity.md)
- [NPS.NDP API 参考](./nps-ndp.md)
- [NIP 规范](../../../spec/NPS-3-NIP.md)
