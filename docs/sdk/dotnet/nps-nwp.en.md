English | [中文版](./nps-nwp.md)

# NPS.NWP API Reference

`NPS.NWP` provides the Memory Node middleware (ASP.NET Core), NWP frame types, Neural Web Manifest model, HTTP header constants, CGN metering utilities, and SQL query builders.

---

## DI Registration

```csharp
// Prerequisite: AddNpsCore() must be called first
builder.Services.AddNwp(opt =>
{
    opt.DefaultTokenBudget = 0;   // 0 = no limit
    opt.MaxDepth           = 5;   // X-NWP-Depth cap
    opt.DefaultLimit       = 20;  // default page size
});
```

---

## NwpOptions

Namespace: `NPS.NWP.Extensions`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Port` | `int` | `17433` | NWP port (unified port, NPS-2 §3) |
| `DefaultTokenBudget` | `uint` | `0` | Default token budget (0 = unlimited) |
| `MaxDepth` | `uint` | `5` | Maximum node graph traversal depth |
| `DefaultLimit` | `uint` | `20` | Default QueryFrame.Limit (max 1000) |

---

## NWP Frame Types

Namespace: `NPS.NWP.Frames`

### QueryFrame

Structured query frame — sent to Memory Nodes to retrieve data.

```csharp
public sealed record QueryFrame : IFrame
{
    public string?                          AnchorRef    { get; init; }
    public JsonElement?                     Filter       { get; init; }
    public IReadOnlyList<string>?           Fields       { get; init; }
    public uint                             Limit        { get; init; } = 20;
    public string?                          Cursor       { get; init; }
    public IReadOnlyList<QueryOrderClause>? Order        { get; init; }
    public VectorSearchOptions?             VectorSearch { get; init; }
}
```

**Example:**

```csharp
// Query products with price > 100, sorted by price descending, first 50
var query = new QueryFrame
{
    AnchorRef = anchorId,
    Filter    = JsonSerializer.SerializeToElement(new
    {
        price = new { _gt = 100 }
    }),
    Order = [new QueryOrderClause("price", "DESC")],
    Limit = 50,
};
```

#### Filter DSL

Filters use a JSON predicate format (NPS-2 §5.2):

| Operator | Type | Example |
|----------|------|---------|
| `$eq` | Equals | `{ "status": { "$eq": "active" } }` |
| `$ne` | Not equals | `{ "type": { "$ne": "draft" } }` |
| `$lt` / `$lte` | Less than / less than or equal | `{ "price": { "$lt": 100 } }` |
| `$gt` / `$gte` | Greater than / greater than or equal | `{ "age": { "$gte": 18 } }` |
| `$in` | In set | `{ "tag": { "$in": ["a","b"] } }` |
| `$nin` | Not in set | `{ "tag": { "$nin": ["x"] } }` |
| `$contains` | String contains | `{ "name": { "$contains": "foo" } }` |
| `$between` | Range | `{ "price": { "$between": [10, 50] } }` |
| `$and` / `$or` | Logical composition | `{ "$and": [{ "a": 1 }, { "b": 2 }] }` |

---

### QueryOrderClause

```csharp
public record QueryOrderClause(string Field, string Dir); // Dir: "ASC" | "DESC"
```

---

### VectorSearchOptions

Vector similarity search parameters (requires the `vector_search` node capability):

```csharp
public sealed record VectorSearchOptions
{
    public required string  Field     { get; init; }        // vector field name
    public required float[] Vector    { get; init; }        // query vector
    public          uint    TopK      { get; init; } = 10;  // nearest neighbor count
    public          double? Threshold { get; init; }        // min similarity (0.0–1.0)
    public          string  Metric    { get; init; } = "cosine"; // "cosine"|"euclidean"|"dot"
}
```

---

### ActionFrame

Operation invocation frame — sent to Action or Complex Nodes.

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

**Example:**

```csharp
// Synchronous call
var action = new ActionFrame
{
    ActionId       = "orders.create",
    Params         = JsonSerializer.SerializeToElement(new { userId = "u123", total = 99.9 }),
    IdempotencyKey = Guid.NewGuid().ToString("D"),
};

// Asynchronous call (node returns task_id + poll_url)
var asyncAction = new ActionFrame
{
    ActionId = "reports.generate",
    Params   = JsonSerializer.SerializeToElement(new { from = "2026-01-01" }),
    Async    = true,
};
```

---

## Memory Node Middleware

### IMemoryNodeProvider

Namespace: `NPS.NWP.MemoryNode`

Implement this interface to back a node with a custom data source:

```csharp
public interface IMemoryNodeProvider
{
    Task<MemoryNodeQueryResult> QueryAsync(
        QueryFrame frame, MemoryNodeSchema schema,
        MemoryNodeOptions options, CancellationToken ct = default);

    IAsyncEnumerable<IReadOnlyList<IReadOnlyDictionary<string, object?>>> StreamAsync(
        QueryFrame frame, MemoryNodeSchema schema,
        MemoryNodeOptions options, CancellationToken ct = default);

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
    public required string          NodeId          { get; set; } // urn:nps:node:{host}:{path}
    public          string?         DisplayName     { get; set; }
    public required MemoryNodeSchema Schema         { get; set; }
    public          uint            DefaultLimit    { get; set; } = 20;
    public          uint            MaxLimit        { get; set; } = 1000;
    public          bool            RequireAuth     { get; set; } = false;
    public          uint            DefaultTokenBudget { get; set; } = 0;
    public required string          PathPrefix      { get; set; } // "/products"
}
```

### MemoryNodeSchema

```csharp
public class MemoryNodeSchema
{
    public required string                       TableName  { get; init; }
    public required string                       PrimaryKey { get; init; }
    public required IReadOnlyList<MemoryNodeField> Fields   { get; init; }

    public MemoryNodeField? GetField(string name)
    public bool             HasField(string name)
}

public class MemoryNodeField
{
    public required string  Name        { get; init; }
    public required string  Type        { get; init; } // "string"|"number"|"boolean"|"datetime"|"object"
    public          string? Description { get; init; }
    public          bool    Nullable    { get; init; } = true;
    public          string? ColumnName  { get; init; } // DB column name (defaults to Name)
    public          string  ResolvedColumnName { get; }
}
```

### Registration and Middleware Mounting

```csharp
// Use the built-in SQL Server provider
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

// Mount middleware (handles /.nwm / /.schema / /query / /stream)
var app = builder.Build();
app.UseMemoryNode<SqlServerMemoryNodeProvider>(opt => { /* same opts */ });
app.Run();
```

### Custom Provider

```csharp
public class MongoMemoryNodeProvider : IMemoryNodeProvider
{
    public async Task<MemoryNodeQueryResult> QueryAsync(
        QueryFrame frame, MemoryNodeSchema schema,
        MemoryNodeOptions options, CancellationToken ct)
    {
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

## Built-In SQL Providers

Namespace: `NPS.NWP.MemoryNode.Providers`

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

Both use Dapper for lightweight parameterized queries and support `QueryAsync`, `StreamAsync`, and `CountAsync`.

---

## MemoryNodeMiddleware

Namespace: `NPS.NWP.MemoryNode`

ASP.NET Core middleware — after mounting, automatically handles these sub-paths:

| Path | Method | Function |
|------|--------|----------|
| `{prefix}/.nwm` | `GET` | Return Neural Web Manifest (JSON) |
| `{prefix}/.schema` | `GET` | Return current AnchorFrame schema |
| `{prefix}/query` | `POST` | Handle QueryFrame, return CapsFrame |
| `{prefix}/stream` | `POST` | Handle QueryFrame, stream StreamFrames |

---

## Neural Web Manifest (NWM)

Namespace: `NPS.NWP.Nwm`

`NeuralWebManifest` describes a node's capabilities, endpoints, and auth requirements — used by AI Agents for auto-discovery:

```csharp
public record NeuralWebManifest
{
    public required string  Nwp         { get; init; } = "0.2";
    public required string  NodeId      { get; init; }
    public required string  NodeType    { get; init; } // "memory"|"action"|"complex"
    public          string? DisplayName { get; init; }
    public required IReadOnlyList<string>              WireFormats      { get; init; }
    public required string                             PreferredFormat  { get; init; }
    public          IReadOnlyDictionary<string,string>? SchemaAnchors   { get; init; }
    public required NodeCapabilities                   Capabilities     { get; init; }
    public          IReadOnlyList<string>?             DataSources      { get; init; }
    public required NodeAuth                           Auth             { get; init; }
    public required NodeEndpoints                      Endpoints        { get; init; }
    public          IReadOnlyList<string>?             TokenizerSupport { get; init; }
    public          NodeGraph?                         Graph            { get; init; }
}
```

### NodeCapabilities

```csharp
public record NodeCapabilities
{
    public bool Query           { get; init; } // Supports QueryFrame (0x10)
    public bool Stream          { get; init; } // Supports StreamFrame responses
    public bool Subscribe       { get; init; } // Supports DiffFrame push subscriptions
    public bool VectorSearch    { get; init; } // Supports vector similarity search
    public bool TokenBudgetHint { get; init; } // Supports X-NWP-Budget response trimming
    public bool ExtFrame        { get; init; } // Supports 8-byte extended frame header
}
```

### NodeAuth

```csharp
public record NodeAuth
{
    public required bool                   Required             { get; init; }
    public          string?                IdentityType         { get; init; } // "nip-cert"|"bearer"|"none"
    public          IReadOnlyList<string>? TrustedIssuers       { get; init; }
    public          IReadOnlyList<string>? RequiredCapabilities { get; init; }
    public          string?                ScopeCheck           { get; init; } // "prefix"|"exact"
    public          string?                OcspUrl              { get; init; }
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

## HTTP Header Constants

Namespace: `NPS.NWP.Http`

### Request Headers (NwpHttpHeaders)

| Constant | Header Name | Description |
|----------|-------------|-------------|
| `Agent` | `X-NWP-Agent` | Agent NID |
| `Budget` | `X-NWP-Budget` | Token budget ceiling (uint32 CGN) |
| `Depth` | `X-NWP-Depth` | Node graph traversal depth (default 1, max 5) |
| `Encoding` | `X-NWP-Encoding` | Requested encoding: `"json"` or `"msgpack"` |
| `Tokenizer` | `X-NWP-Tokenizer` | Agent's tokenizer identifier |

### Response Headers (NwpHttpHeaders)

| Constant | Header Name | Description |
|----------|-------------|-------------|
| `Schema` | `X-NWP-Schema` | anchor_id of the response schema |
| `Tokens` | `X-NWP-Tokens` | Actual CGN consumption for the response |
| `TokensNative` | `X-NWP-Tokens-Native` | Native token consumption |
| `TokenizerUsed` | `X-NWP-Tokenizer-Used` | Tokenizer actually used |
| `Cached` | `X-NWP-Cached` | `"true"` when served from server-side cache |
| `NodeType` | `X-NWP-Node-Type` | Node type: `"memory"` / `"action"` / `"complex"` |

### MIME Types

```csharp
NwpHttpHeaders.MimeFrame    = "application/nwp-frame"
NwpHttpHeaders.MimeCapsule  = "application/nwp-capsule"
NwpHttpHeaders.MimeManifest = "application/nwp-manifest+json"
```

---

## NWP Error Codes

Namespace: `NPS.NWP.Http`

```csharp
NwpErrorCodes.AuthNidScopeViolation   // "NWP-AUTH-NID-SCOPE-VIOLATION"
NwpErrorCodes.AuthNidExpired          // "NWP-AUTH-NID-EXPIRED"
NwpErrorCodes.QueryFilterInvalid      // "NWP-QUERY-FILTER-INVALID"
NwpErrorCodes.QueryFieldUnknown       // "NWP-QUERY-FIELD-UNKNOWN"
NwpErrorCodes.ActionNotFound          // "NWP-ACTION-NOT-FOUND"
NwpErrorCodes.ActionParamsInvalid     // "NWP-ACTION-PARAMS-INVALID"
NwpErrorCodes.BudgetExceeded          // "NWP-LIMIT-BUDGET-EXCEEDED"
NwpErrorCodes.DepthExceeded           // "NWP-LIMIT-DEPTH-EXCEEDED"
// Full list: spec/error-codes.md
```

---

## CGN Metering

Namespace: `NPS.NWP.MemoryNode`

`CognMeter` provides standardized Cognon (CGN) metering:

```csharp
// Raw UTF-8 bytes: ceil(byteCount / 4)
uint cost = CognMeter.Measure(utf8Bytes);

// String
uint cost = CognMeter.Measure("hello world");

// Serialize to JSON, then measure
uint cost = CognMeter.MeasureJson(myObject);

// Row collection
uint cost = CognMeter.MeasureRows(rows);
```

---

## SQL Query Builders

Namespace: `NPS.NWP.MemoryNode.Query`

### NwpFilterTranslator

Translates the NWP Filter DSL to a parameterized SQL WHERE clause:

```csharp
var translator = new NwpFilterTranslator(schema, DatabaseDialect.SqlServer);
var parameters = new DynamicParameters();
string where = translator.Translate(frame.Filter, parameters);
// "WHERE [Price] > @p0 AND [Status] = @p1"
```

### SqlQueryBuilder

Builds complete SELECT queries (field projection, filter, ordering, cursor pagination):

```csharp
var builder = new SqlQueryBuilder(schema, DatabaseDialect.PostgreSql);

// Build SELECT query
(string sql, DynamicParameters p) = builder.Build(frame, options);

// Build COUNT(*) query
(string countSql, DynamicParameters cp) = builder.BuildCount(frame);

// Cursor encode/decode
string cursor = SqlQueryBuilder.EncodeCursor(1000L);
long   offset = SqlQueryBuilder.DecodeCursor(cursor);
```

```csharp
public enum DatabaseDialect { SqlServer, PostgreSql }
```

---

## Related Documentation

- [Node Types Concepts](../../concepts/node-types.en.md)
- [NPS.NIP API Reference](./nps-nip.en.md)
- [NWP Specification](../../../spec/NPS-2-NWP.md)
