[English Version](./nid-identity.en.md) | 中文版

# NID 身份体系

NPS 在协议层内置 Agent 身份机制，取代依赖 HTTP Header 或应用层 Token 的传统方案。

---

## NID 格式

**NID（Neural Identity Descriptor）** 是 NPS 中节点和 Agent 的唯一身份标识：

```
urn:nps:{entity-type}:{ca-host}:{id}
```

| 字段 | 说明 | 示例 |
|------|------|------|
| `entity-type` | 实体类型 | `agent`、`node`、`org` |
| `ca-host` | 颁发该 NID 的 CA 域名 | `ca.example.com` |
| `id` | 实体唯一标识符；组织 CA NID 省略该段 | `gpt-agent-01` |

**示例：**

```
urn:nps:agent:ca.example.com:gpt-agent-01    # Agent NID
urn:nps:node:api.example.com:products         # Node NID
urn:nps:org:ca.example.com                    # 组织 CA NID（无 id 段）
```

---

## 证书结构

每个 NID 由 NIP CA 颁发一个 `IdentFrame`，包含：

| 字段 | 说明 |
|------|------|
| `nid` | 唯一身份标识符 |
| `pub_key` | Ed25519 公钥（`ed25519:{base64url}` 格式）|
| `capabilities` | 能力声明列表（如 `nwp:query`）|
| `scope` | 允许访问的节点路径集合（JSON）|
| `issued_by` | 颁发 CA 的 NID |
| `issued_at` / `expires_at` | 颁发/过期时间（ISO 8601 UTC）|
| `serial` | 证书序列号（全局唯一）|
| `signature` | CA 对载荷规范 JSON 的 Ed25519 签名 |

---

## 签名机制

所有 NIP 帧（IdentFrame、TrustFrame、RevokeFrame、AnnounceFrame）均使用 **Ed25519** 签名：

1. 将载荷序列化为 **规范 JSON**（RFC 8785 JCS，属性按字母序，无空白符）
2. 对规范 JSON 的 UTF-8 字节进行 Ed25519 签名
3. 签名以 Base64URL 编码存入 `signature` 字段

密钥存储：CA 私钥使用 **PBKDF2-SHA256（60 万次迭代）+ AES-256-GCM** 加密保存到文件，由 `NipKeyManager` 管理。

---

## CA 颁发流程

```
Agent/Node                CA Server (NipCaService)
    │                           │
    │──── POST /v1/agents/register ─────▶│
    │   { identifier,            │
    │     pubKey, capabilities,  │
    │     scopeJson }            │
    │                           │── 生成 NID
    │                           │── 校验公钥格式
    │                           │── 签发 IdentFrame
    │                           │── 存入 NipCertRecord
    │◀──── IdentFrame ──────────│
    │                           │
```

---

## 节点端六步验证

节点收到携带 IdentFrame 的请求时，`NipIdentVerifier` 按如下顺序验证：

| 步骤 | 检查内容 | 失败原因 |
|------|----------|---------|
| 1 | `expires_at > now` | 证书已过期 |
| 2 | `issued_by` 在本节点信任列表中 | 不受信任的 CA |
| 3 | Ed25519 签名验证 | 签名无效（可能被篡改）|
| 4 | 本地 CRL + OCSP 查询 | 证书已被吊销 |
| 5 | `capabilities` 包含节点要求的能力 | 缺少必要能力 |
| 6 | `scope` 覆盖当前请求路径 | Scope 不足（越权）|

任意步骤失败即拒绝请求，返回对应错误码。

---

## Scope 格式

`scope` 为 JSON 对象，`paths` 字段是允许访问的路径数组，支持通配符：

```json
{
  "paths": [
    "/products/*",
    "/orders/**",
    "/admin/reports"
  ]
}
```

| 模式 | 匹配范围 |
|------|---------|
| `/products/123` | 精确匹配 |
| `/products/*` | 单段通配（不含子路径）|
| `/orders/**` | 多段通配（含所有子路径）|

---

## 证书续期与吊销

### 续期

证书只能在**续期窗口期**（到期前 `RenewalWindowDays` 天）内续期，窗口期外续期调用返回 `NIP-CA-RENEWAL-TOO-EARLY`。

```
POST /v1/agents/urn%3Anps%3Aagent%3Aca.example.com%3Agpt-agent-01/renew
→ 新的 IdentFrame（更新 issued_at / expires_at / signature）
```

### 吊销

立即生效，写入 CA 数据库，返回 `RevokeFrame`，并通过 CRL / 在线校验发布：

```
POST /v1/agents/{nid}/revoke
{ "reason": "key_compromise" }
→ RevokeFrame
```

常用 `reason` 值：`key_compromise`、`affiliation_changed`、`superseded`、`cessation_of_operation`。

---

## 在线状态查询

节点端验证第 4 步可调用 CA 的 OCSP 风格 verify 端点实时检查吊销状态：

```
GET /v1/agents/{nid}/verify
→ { "valid": true } | { "valid": false, "error": "NIP-CERT-REVOKED" }
```

verify 响应默认最短 200ms（`NormalizeOcspResponseTime = true`），防止时序侧信道攻击。

---

## 相关文档

- [NPS.NIP API 参考](../sdk/dotnet/nps-nip.md)
- [NIP 规范](../../spec/NPS-3-NIP.md)
