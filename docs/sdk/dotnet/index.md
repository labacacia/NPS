[English Version](./index.en.md) | 中文版

# NPS .NET SDK — 快速开始

## 环境要求

| 组件 | 最低版本 |
|------|---------|
| .NET | 10.0 |
| ASP.NET Core | 10.0（`NPS.NWP`、`NPS.NIP` 需要）|

---

## 包结构

| NuGet 包 | 说明 | 依赖 |
|---------|------|------|
| `LabAcacia.NPS.Core` | 帧编解码、注册表、Anchor 缓存 | — |
| `LabAcacia.NPS.NWP` | NWP 帧类型、NWM 清单、Memory / Action / Complex / Anchor / Bridge Node 中间件 | NPS.Core |
| `LabAcacia.NPS.NWP.Anchor` | Anchor Node：无状态 AaaS 入口；`AnchorNodeClient` 支持 `topology.snapshot` / `topology.stream` | NPS.Core, NPS.NWP |
| `LabAcacia.NPS.NWP.Bridge` | Bridge Node：出站 NPS → 外部协议 dispatcher，以及入站 MCP/A2A server bridge | NPS.Core, NPS.NWP |
| `LabAcacia.NPS.NIP` | CA 库 + 节点端身份验证器 + X.509 / ACME `agent-01`（RFC-0002 原型）| NPS.Core |
| `LabAcacia.NPS.NDP` | 节点发现、注册表、公告验证 | NPS.Core, NPS.NIP |
| `LabAcacia.NPS.NOP` | DAG 编排引擎 | NPS.Core |
| `LabAcacia.NPS.Daemon.Observability` | Daemon 共享 metrics、health、logging 辅助能力 | NPS.Core |

所有 SDK 包均以 `1.0.0-alpha.18` 版本发布到 NuGet。

Bridge Node 调用 schema：[NWP Bridge Target Schema](./bridge-target.md)。

---

## DI 注册总览

以下片段展示完整协议栈的注册顺序（按依赖顺序）：

```csharp
// Program.cs
using NPS.Core.Extensions;
using NPS.NWP.Extensions;
using NPS.NIP.Extensions;
using NPS.NDP.Extensions;
using NPS.NOP.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 1. NPS.Core（必须最先注册）
builder.Services.AddNpsCore(opt =>
{
    opt.DefaultTier           = EncodingTier.MsgPack; // 生产环境默认
    opt.AnchorTtlSeconds      = 3600;
    opt.MaxFramePayload       = 65535;
    opt.EnableExtendedFrameHeader = false;
});

// 2. NPS.NWP — Memory Node 中间件
builder.Services.AddNwp(opt =>
{
    opt.DefaultTokenBudget = 0;     // 0 = 不限制
    opt.MaxDepth           = 5;
    opt.DefaultLimit       = 20;
});

// 3. NPS.NIP — 仅节点端验证器（不含 CA）
builder.Services.AddNipVerifier(opt =>
{
    opt.TrustedIssuers = new Dictionary<string, string>
    {
        ["urn:nps:org:ca.example.com"] = "https://ca.example.com"
    };
    opt.OcspUrl = "https://ca.example.com/v1/agents/{nid}/verify";
});

// 4. NPS.NDP — 节点注册表 + 公告验证
builder.Services.AddNdp();

// 5. NPS.NOP — 编排引擎
builder.Services.AddNopOrchestrator(opt =>
{
    opt.MaxConcurrentNodes       = Environment.ProcessorCount * 2;
    opt.EnableCallback           = true;
    opt.CallbackRetryBaseDelayMs = 1000;
});

var app = builder.Build();
app.Run();
```

---

## 单独使用 NPS.Core（无 DI）

```csharp
using NPS.Core.Codecs;
using NPS.Core.Registry;
using NPS.Core.Frames.Ncp;

// 手动构建编解码器
var registry = new FrameRegistryBuilder()
    .AddNcp()
    .Build();

var json    = new Tier1JsonCodec();
var msgpack = new Tier2MsgPackCodec();
var codec   = new NpsFrameCodec(json, msgpack, registry);

// 编码
var anchor = new AnchorFrame
{
    AnchorId = AnchorIdComputer.Compute(schema),
    Schema   = schema,
    Ttl      = 3600,
};
byte[] wire = codec.Encode(anchor); // 自动写入 4-byte 帧头

// 解码
IFrame frame = codec.Decode(wire);
```

---

## 搭建 NIP CA Server

> 完整搭建指南见 [`tools/nip-ca-server/`](https://github.com/labacacia/NIP-CA-Server)。

```csharp
// 注册 CA 服务（含密钥生成）
builder.Services.AddNipCa(opt =>
{
    opt.CaNid          = "urn:nps:org:ca.example.com";
    opt.DisplayName    = "Example NPS CA";
    opt.KeyFilePath    = "/secrets/ca.key";
    opt.KeyPassphrase  = Environment.GetEnvironmentVariable("CA_PASSPHRASE")!;
    opt.BaseUrl        = "https://ca.example.com";
    opt.ConnectionString = Environment.GetEnvironmentVariable("DB_CONN")!;
    opt.AgentCertValidityDays = 30;
    opt.NodeCertValidityDays  = 90;
}, generateKeyIfMissing: true);

// 映射 CA HTTP 路由
var app = builder.Build();
app.MapNipCa();
app.Run();
```

---

## 各包 API 参考

| 包 | 文档 |
|----|------|
| `NPS.Core` | [nps-core.md](./nps-core.md) |
| `NPS.NWP` | [nps-nwp.md](./nps-nwp.md) |
| `NPS.NIP` | [nps-nip.md](./nps-nip.md) |
| `NPS.NDP` | [nps-ndp.md](./nps-ndp.md) |
| `NPS.NOP` | [nps-nop.md](./nps-nop.md) |

---

## 概念文档

| 文档 | 说明 |
|------|------|
| [帧模型](../../concepts/frame-model.md) | 帧结构、编码 Tier、帧头格式、Schema 锚定 |
| [NID 身份体系](../../concepts/nid-identity.md) | NID 格式、CA 颁发流程、身份验证步骤 |
| [DAG 编排](../../concepts/dag-orchestration.md) | NOP DAG 模型、委托链、K-of-N 同步 |
| [节点类型](../../concepts/node-types.md) | Memory / Action / Complex / Anchor / Bridge 节点 |
