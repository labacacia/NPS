[English Version](./nps-nwp.en.md) | 中文版

# NPS.NWP API 参考

`NPS.NWP` 提供 Memory Node 中间件（ASP.NET Core）、NWP 帧类型、Neural Web Manifest 模型、HTTP 头常量、CGN 计量工具以及 SQL 查询构建器。

---

## DI 注册

```csharp
// 前提：已调用 AddNpsCore()
builder.Services.AddNwp(opt =>
{
    opt.DefaultTokenBudget = 0;   // 0 = 不限制
    opt.MaxDepth           = 5;   // X-NWP-Depth 上限
    opt.DefaultLimit       = 20;  // 默认分页大小
});
```

---

## NwpOptions

命名空间：`NPS.NWP.Extensions`

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Port` | `int` | `17433` | NWP 端口（统一端口，NPS-2 §3）|
| `DefaultTokenBudget` | `uint` | `0` | 缺省 Token 预算（0 = 不限制）|
| `MaxDepth` | `uint` | `5` | 节点图谱最大遍历深度 |
| `DefaultLimit` | `uint` | `20` | QueryFrame.Limit 缺省值（最大 1000）|

---

## NWP 帧类型

命名空间：`NPS.NWP.Frames`

### QueryFrame

结构化查询帧，向 Memory Node 发起查询请求。

```csharp
public sealed record QueryFrame : IFrame
{
    public string?                     AnchorRef    { get; init; }
    public JsonElement?                Filter       { get; init; }
    public IReadOnlyList<string>?      Fields       { get; init; }
    public uint                        Limit        { get; init; } = 20;
    public string?                     Cursor       { get; init; }
    public IReadOnlyList<QueryOrderClause>? Order   { get; init; }
    public VectorSearchOptions?        VectorSearch { get; init; }
}
```

**示例：**

```csharp
// 查询价格 > 100 的产品，按价格降序，返回前 50 条
var query = new QueryFrame
{
    AnchorRef = anchorId,
    Filter    = JsonSerializer.SerializeToElement(new
    {
        price = new { _gt = 100 }
    }),
    Order  = [new QueryOrderClause("price", "DESC")],
    Limit  = 50,
};
```

#### Filter DSL

Filter 使用 JSON 谓词格式（NPS-2 §5.2）：

| 操作符 | 类型 | 示例 |
|--------|------|------|
| `$eq` | 等于 | `{ "status": { "$eq": "active" } }` |
| `$ne` | 不等于 | `{ "type": { "$ne": "draft" } }` |
| `$lt` / `$lte` | 小于 / 小于等于 | `{ "price": { "$lt": 100 } }` |
| `$gt` / `$gte` | 大于 / 大于等于 | `{ "age": { "$gte": 18 } }` |
| `$in` | 包含于集合 | `{ "tag": { "$in": ["a","b"] } }` |
| `$nin` | 不包含于集合 | `{ "tag": { "$nin": ["x"] } }` |
| `$contains` | 字符串包含 | `{ "name": { "$contains": "foo" } }` |
| `$between` | 区间 | `{ "price": { "$between": [10, 50] } }` |
| `$and` / `$or` | 逻辑组合 | `{ "$and": [{ "a": 1 }, { "b": 2 }] }` |

---

### QueryOrderClause

```csharp
public record QueryOrderClause(string Field, string Dir); // Dir: "ASC" | "DESC"
```

---

### VectorSearchOptions

向量相似度搜索参数（需节点支持 `vector_search` 能力）：

```csharp
public sealed record VectorSearchOptions
{
    public required string  Field     { get; init; }        // 向量字段名
    public required float[] Vector    { get; init; }        // 查询向量
    public          uint    TopK      { get; init; } = 10;  // 最近邻数量
    public          double? Threshold { get; init; }        // 最小相似度 (0.0–1.0)
    public          string  Metric    { get; init; } = "cosine"; // "cosine"|"euclidean"|"dot"
}
```

---

### ActionFrame

操作调用帧，向 Action 或 Complex Node 发送操作请求。

```csharp
public sealed record ActionFrame : IFrame
{
    public required string       ActionId       { get; init; } // "{domain}.{verb}"
    public          JsonElement? Params         { get; init; }
    public          string?      IdempotencyKey { get; init; } // UUID v4
    public          uint         TimeoutMs      { get; init; } = 5000;
    public          bool         Async          { get; init; }
}
```

**示例：**

```csharp
// 同步调用
var action = new ActionFrame
{
    ActionId       = "orders.create",
    Params         = JsonSerializer.SerializeToElement(new { userId = "u123", total = 99.9 }),
    IdempotencyKey = Guid.NewGuid().ToString("D"),
};

// 异步调用（节点返回 task_id + poll_url）
var asyncAction = new ActionFrame
{
    ActionId = "reports.generate",
    Params   = JsonSerializer.SerializeToElement(new { from = "2026-01-01" }),
    Async    = true,
};
```

---

## Memory Node 中间件

### IMemoryNodeProvider

命名空间：`NPS.NWP.MemoryNode`

实现此接口以对接自定义数据源：

```csharp
public interface IMemoryNodeProvider
{
    // 分页查询
    Task<MemoryNodeQueryResult> QueryAsync(
        QueryFrame frame, MemoryNodeSchema schema,
        MemoryNodeOptions options, CancellationToken ct = default);

    // 流式读取（异步序列，每页为一组行）
    IAsyncEnumerable<IReadOnlyList<IReadOnlyDictionary<string, object?>>> StreamAsync(
        QueryFrame frame, MemoryNodeSchema schema,
        MemoryNodeOptions options, CancellationToken ct = default);

    // 计数查询
    Task<long> CountAsync(
        QueryFrame frame, MemoryNodeSchema schema,
        CancellationToken ct = default);
}
```

### MemoryNodeQueryResult

```csharp
public class MemoryNodeQueryResult
{
    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; }
    public          string?                                              NextCursor { get; init; }
    public          int                                                  Count => Rows.Count;
}
```

### MemoryNodeOptions

```csharp
public class MemoryNodeOptions
{
    public required string         NodeId          { get; set; } // urn:nps:node:{host}:{path}
    public          string?        DisplayName     { get; set; }
    public required MemoryNodeSchema Schema        { get; set; }
    public          uint           DefaultLimit    { get; set; } = 20;
    public          uint           MaxLimit        { get; set; } = 1000;
    public          bool           RequireAuth     { get; set; } = false;
    public          uint           DefaultTokenBudget { get; set; } = 0;
    public required string         PathPrefix      { get; set; } // "/products"
}
```

### MemoryNodeSchema

```csharp
public class MemoryNodeSchema
{
    public required string                      TableName  { get; init; }
    public required string                      PrimaryKey { get; init; }
    public required IReadOnlyList<MemoryNodeField> Fields  { get; init; }

    public MemoryNodeField? GetField(string name)
    public bool             HasField(string name)
}

public class MemoryNodeField
{
    public required string  Name         { get; init; }
    public required string  Type         { get; init; } // "string"|"number"|"boolean"|"datetime"|"object"
    public          string? Description  { get; init; }
    public          bool    Nullable     { get; init; } = true;
    public          string? ColumnName   { get; init; } // 数据库列名（默认与 Name 相同）
    public          string  ResolvedColumnName { get; }
}
```

### 注册与中间件挂载

```csharp
// 使用内置 SQL Server Provider
builder.Services.AddMemoryNode<SqlServerMemoryNodeProvider>(opt =>
{
    opt.NodeId      = "urn:nps:node:api.example.com:products";
    opt.DisplayName = "Products Memory Node";
    opt.PathPrefix  = "/products";
    opt.Schema      = new MemoryNodeSchema
    {
        TableName  = "Products",
        PrimaryKey = "Id",
        Fields     =
        [
            new MemoryNodeField { Name = "Id",    Type = "number" },
            new MemoryNodeField { Name = "Name",  Type = "string" },
            new MemoryNodeField { Name = "Price", Type = "number" },
        ],
    };
});

// 挂载中间件（MapNwp 路由 /.nwm / /.schema / /query / /stream）
var app = builder.Build();
app.UseMemoryNode<SqlServerMemoryNodeProvider>(opt => { /* same opts */ });
app.Run();
```

### 自定义 Provider

```csharp
public class MongoMemoryNodeProvider : IMemoryNodeProvider
{
    public async Task<MemoryNodeQueryResult> QueryAsync(
        QueryFrame frame, MemoryNodeSchema schema,
        MemoryNodeOptions options, CancellationToken ct)
    {
        // 自定义查询逻辑
        var rows = await FetchFromMongo(frame.Filter, frame.Limit, ct);
        return new MemoryNodeQueryResult { Rows = rows };
    }

    public async IAsyncEnumerable<IReadOnlyList<IReadOnlyDictionary<string, object?>>> StreamAsync(
        QueryFrame frame, MemoryNodeSchema schema,
        MemoryNodeOptions options, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var page in StreamFromMongo(frame.Filter, ct))
            yield return page;
    }

    public async Task<long> CountAsync(QueryFrame frame, MemoryNodeSchema schema, CancellationToken ct)
        => await CountFromMongo(frame.Filter, ct);
}
```

---

## 内置 SQL Provider

命名空间：`NPS.NWP.MemoryNode.Providers`

### SqlServerMemoryNodeProvider

```csharp
public class SqlServerMemoryNodeProvider : IMemoryNodeProvider
{
    public SqlServerMemoryNodeProvider(
        string connectionString,
        ILogger<SqlServerMemoryNodeProvider> logger)
}
```

### PostgreSqlMemoryNodeProvider

```csharp
public class PostgreSqlMemoryNodeProvider : IMemoryNodeProvider
{
    public PostgreSqlMemoryNodeProvider(
        string connectionString,
        ILogger<PostgreSqlMemoryNodeProvider> logger)
}
```

两者均基于 Dapper 实现动态参数化查询，支持 `QueryAsync`、`StreamAsync`、`CountAsync`。

---

## MemoryNodeMiddleware

命名空间：`NPS.NWP.MemoryNode`

ASP.NET Core 中间件，挂载后自动处理以下子路径：

| 路径 | 方法 | 功能 |
|------|------|------|
| `{prefix}/.nwm` | `GET` | 返回 Neural Web Manifest（JSON）|
| `{prefix}/.schema` | `GET` | 返回当前 AnchorFrame Schema |
| `{prefix}/query` | `POST` | 处理 QueryFrame，返回 CapsFrame |
| `{prefix}/stream` | `POST` | 处理 QueryFrame，流式返回 StreamFrame |

---

## Neural Web Manifest（NWM）

命名空间：`NPS.NWP.Nwm`

`NeuralWebManifest` 描述节点的能力、端点和认证要求，供 AI Agent 自动发现：

```csharp
public record NeuralWebManifest
{
    public required string  Nwp         { get; init; } = "0.2";
    public required string  NodeId      { get; init; }
    public required string  NodeType    { get; init; } // "memory"|"action"|"complex"
    public          string? DisplayName { get; init; }
    public required IReadOnlyList<string>              WireFormats       { get; init; }
    public required string                             PreferredFormat   { get; init; }
    public          IReadOnlyDictionary<string,string>? SchemaAnchors    { get; init; }
    public required NodeCapabilities                   Capabilities      { get; init; }
    public          IReadOnlyList<string>?             DataSources       { get; init; }
    public required NodeAuth                           Auth              { get; init; }
    public required NodeEndpoints                      Endpoints         { get; init; }
    public          IReadOnlyList<string>?             TokenizerSupport  { get; init; }
    public          NodeGraph?                         Graph             { get; init; }
}
```

### NodeCapabilities

```csharp
public record NodeCapabilities
{
    public bool Query           { get; init; } // 支持 QueryFrame (0x10)
    public bool Stream          { get; init; } // 支持 StreamFrame 响应
    public bool Subscribe       { get; init; } // 支持 DiffFrame 推送订阅
    public bool VectorSearch    { get; init; } // 支持向量相似度搜索
    public bool TokenBudgetHint { get; init; } // 支持 X-NWP-Budget 响应裁剪
    public bool ExtFrame        { get; init; } // 支持 8 字节扩展帧头
}
```

### NodeAuth

```csharp
public record NodeAuth
{
    public required bool                   Required           { get; init; }
    public          string?                IdentityType       { get; init; } // "nip-cert"|"bearer"|"none"
    public          IReadOnlyList<string>? TrustedIssuers     { get; init; }
    public          IReadOnlyList<string>? RequiredCapabilities { get; init; }
    public          string?                ScopeCheck         { get; init; } // "prefix"|"exact"
    public          string?                OcspUrl            { get; init; }
}
```

### NodeEndpoints / NodeGraph

```csharp
public record NodeEndpoints
{
    public string? Query  { get; init; } // "nwp://.../query"
    public string? Stream { get; init; } // "nwp://.../stream"
    public string? Invoke { get; init; } // "nwp://.../invoke"
    public string? Schema { get; init; } // "nwp://.../.schema"
}

public record NodeGraph
{
    public required IReadOnlyList<NodeGraphRef> Refs     { get; init; }
    public          uint                        MaxDepth { get; init; } = 2;
}

public record NodeGraphRef(string Rel, string Node);
```

---

## HTTP 头常量

命名空间：`NPS.NWP.Http`

### 请求头（NwpHttpHeaders）

| 常量 | Header 名 | 说明 |
|------|-----------|------|
| `Agent` | `X-NWP-Agent` | Agent NID |
| `Budget` | `X-NWP-Budget` | Token 预算上限（uint32 CGN）|
| `Depth` | `X-NWP-Depth` | 节点图谱遍历深度（默认 1，最大 5）|
| `Encoding` | `X-NWP-Encoding` | 请求编码格式：`"json"` 或 `"msgpack"` |
| `Tokenizer` | `X-NWP-Tokenizer` | Agent 的 Tokenizer 标识符 |

### 响应头（NwpHttpHeaders）

| 常量 | Header 名 | 说明 |
|------|-----------|------|
| `Schema` | `X-NWP-Schema` | 响应使用的 anchor_id |
| `Tokens` | `X-NWP-Tokens` | 响应的实际 CGN 消耗 |
| `TokensNative` | `X-NWP-Tokens-Native` | 原生 token 消耗 |
| `TokenizerUsed` | `X-NWP-Tokenizer-Used` | 实际使用的 Tokenizer |
| `Cached` | `X-NWP-Cached` | `"true"` 表示命中服务端缓存 |
| `NodeType` | `X-NWP-Node-Type` | 节点类型：`"memory"`/`"action"`/`"complex"` |

### MIME 类型

```csharp
NwpHttpHeaders.MimeFrame    = "application/nwp-frame"
NwpHttpHeaders.MimeCapsule  = "application/nwp-capsule"
NwpHttpHeaders.MimeManifest = "application/nwp-manifest+json"
```

---

## NWP 错误码

命名空间：`NPS.NWP.Http`

```csharp
NwpErrorCodes.AuthNidScopeViolation      // "NWP-AUTH-NID-SCOPE-VIOLATION"
NwpErrorCodes.AuthNidExpired             // "NWP-AUTH-NID-EXPIRED"
NwpErrorCodes.QueryFilterInvalid         // "NWP-QUERY-FILTER-INVALID"
NwpErrorCodes.QueryFieldUnknown          // "NWP-QUERY-FIELD-UNKNOWN"
NwpErrorCodes.ActionNotFound             // "NWP-ACTION-NOT-FOUND"
NwpErrorCodes.ActionParamsInvalid        // "NWP-ACTION-PARAMS-INVALID"
NwpErrorCodes.BudgetExceeded            // "NWP-LIMIT-BUDGET-EXCEEDED"
NwpErrorCodes.DepthExceeded             // "NWP-LIMIT-DEPTH-EXCEEDED"
// … 完整列表见 spec/error-codes.md
```

---

## CGN 计量

命名空间：`NPS.NWP.MemoryNode`

`CognMeter` 提供统一的 CGN（Cognon）计量：

```csharp
// 原始 UTF-8 字节：ceil(byteCount / 4)
uint cost = CognMeter.Measure(utf8Bytes);

// 字符串
uint cost = CognMeter.Measure("hello world");

// 序列化为 JSON 后计量
uint cost = CognMeter.MeasureJson(myObject);

// 行集合
uint cost = CognMeter.MeasureRows(rows);
```

---

## SQL 查询构建器

命名空间：`NPS.NWP.MemoryNode.Query`

### NwpFilterTranslator

将 NWP Filter DSL 翻译为参数化 SQL WHERE 子句：

```csharp
var translator = new NwpFilterTranslator(schema, DatabaseDialect.SqlServer);
var parameters = new DynamicParameters();
string where = translator.Translate(frame.Filter, parameters);
// "WHERE [Price] > @p0 AND [Status] = @p1"
```

### SqlQueryBuilder

构建完整 SELECT 查询（含字段投影、过滤、排序、游标分页）：

```csharp
var builder = new SqlQueryBuilder(schema, DatabaseDialect.PostgreSql);

// 构建 SELECT 查询
(string sql, DynamicParameters p) = builder.Build(frame, options);

// 构建 COUNT(*) 查询
(string countSql, DynamicParameters cp) = builder.BuildCount(frame);

// 游标编解码
string cursor = SqlQueryBuilder.EncodeCursor(1000L);
long   offset = SqlQueryBuilder.DecodeCursor(cursor);
```

```csharp
public enum DatabaseDialect { SqlServer, PostgreSql }
```

---

## 相关文档

- [节点类型概念](../../concepts/node-types.md)
- [NPS.NIP API 参考](./nps-nip.md)
- [NWP 规范](../../../spec/NPS-2-NWP.md)
