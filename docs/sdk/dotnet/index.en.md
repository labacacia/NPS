English | [中文版](./index.md)

# NPS .NET SDK — Getting Started

## Requirements

| Component | Minimum Version |
|-----------|----------------|
| .NET | 10.0 |
| ASP.NET Core | 10.0 (required by `NPS.NWP`, `NPS.NIP`) |

---

## Package Layout

| NuGet Package | Description | Depends On |
|---------------|-------------|------------|
| `LabAcacia.NPS.Core` | Frame codec, registry, Anchor cache | — |
| `LabAcacia.NPS.NWP` | NWP frame types, NWM manifest, Memory/Action/Complex/Anchor/Bridge Node middleware | NPS.Core |
| `LabAcacia.NPS.NWP.Anchor` | Anchor Node: stateless AaaS entry point; `AnchorNodeClient` for `topology.snapshot` / `topology.stream` | NPS.Core, NPS.NWP |
| `LabAcacia.NPS.NWP.Bridge` | Bridge Node: outbound NPS → external dispatchers plus inbound MCP/A2A server bridges | NPS.Core, NPS.NWP |
| `LabAcacia.NPS.NIP` | CA library + node-side identity verifier + X.509 / ACME `agent-01` (RFC-0002 prototype) | NPS.Core |
| `LabAcacia.NPS.NDP` | Node discovery, registry, announce validation | NPS.Core, NPS.NIP |
| `LabAcacia.NPS.NOP` | DAG orchestration engine | NPS.Core |
| `LabAcacia.NPS.Daemon.Observability` | Shared daemon metrics, health, and logging helpers | NPS.Core |

All SDK packages ship on NuGet at version `1.0.0-alpha.18`.

Bridge Node invocation schema: [NWP Bridge Target Schema](./bridge-target.en.md).

---

## DI Registration Overview

The following snippet shows the full protocol stack registration in dependency order:

```csharp
// Program.cs
using NPS.Core.Extensions;
using NPS.NWP.Extensions;
using NPS.NIP.Extensions;
using NPS.NDP.Extensions;
using NPS.NOP.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 1. NPS.Core (must be registered first)
builder.Services.AddNpsCore(opt =>
{
    opt.DefaultTier               = EncodingTier.MsgPack; // production default
    opt.AnchorTtlSeconds          = 3600;
    opt.MaxFramePayload           = 65535;
    opt.EnableExtendedFrameHeader = false;
});

// 2. NPS.NWP — Memory Node middleware
builder.Services.AddNwp(opt =>
{
    opt.DefaultTokenBudget = 0;     // 0 = no limit
    opt.MaxDepth           = 5;
    opt.DefaultLimit       = 20;
});

// 3. NPS.NIP — node-side verifier only (no CA)
builder.Services.AddNipVerifier(opt =>
{
    opt.TrustedIssuers = new Dictionary<string, string>
    {
        ["urn:nps:org:ca.example.com"] = "https://ca.example.com"
    };
    opt.OcspUrl = "https://ca.example.com/v1/agents/{nid}/verify";
});

// 4. NPS.NDP — node registry + announce validation
builder.Services.AddNdp();

// 5. NPS.NOP — orchestration engine
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

## Using NPS.Core Without DI

```csharp
using NPS.Core.Codecs;
using NPS.Core.Registry;
using NPS.Core.Frames.Ncp;

// Build codec manually
var registry = new FrameRegistryBuilder()
    .AddNcp()
    .Build();

var json    = new Tier1JsonCodec();
var msgpack = new Tier2MsgPackCodec();
var codec   = new NpsFrameCodec(json, msgpack, registry);

// Encode
var anchor = new AnchorFrame
{
    AnchorId = AnchorIdComputer.Compute(schema),
    Schema   = schema,
    Ttl      = 3600,
};
byte[] wire = codec.Encode(anchor); // writes 4-byte header automatically

// Decode
IFrame frame = codec.Decode(wire);
```

---

## Setting Up a NIP CA Server

> See [`tools/nip-ca-server/`](https://github.com/labacacia/NIP-CA-Server) for a complete deployment guide.

```csharp
// Register CA services (with key generation)
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

// Map CA HTTP routes
var app = builder.Build();
app.MapNipCa();
app.Run();
```

---

## Per-Package API Reference

| Package | Documentation |
|---------|---------------|
| `NPS.Core` | [nps-core.en.md](./nps-core.en.md) |
| `NPS.NWP` | [nps-nwp.en.md](./nps-nwp.en.md) |
| `NPS.NIP` | [nps-nip.en.md](./nps-nip.en.md) |
| `NPS.NDP` | [nps-ndp.en.md](./nps-ndp.en.md) |
| `NPS.NOP` | [nps-nop.en.md](./nps-nop.en.md) |

---

## Concept Documentation

| Document | Description |
|----------|-------------|
| [Frame Model](../../concepts/frame-model.en.md) | Frame structure, encoding tiers, header format, schema anchoring |
| [NID Identity System](../../concepts/nid-identity.en.md) | NID format, CA issuance flow, verification steps |
| [DAG Orchestration](../../concepts/dag-orchestration.en.md) | NOP DAG model, delegation chain, K-of-N sync |
| [Node Types](../../concepts/node-types.en.md) | Memory / Action / Complex / Anchor / Bridge nodes |
