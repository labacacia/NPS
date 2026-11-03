[English Version](./CHANGELOG.md) | 中文版

# 变更日志

**NPS（Neural Protocol Suite）** 项目的所有重要变更记录于此。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

在 NPS 达到 v1.0 稳定版之前，套件内所有仓库 —— 规范、各 SDK（.NET / Python / TypeScript / Java / Rust / Go）、CA Server、兼容 Bridge —— 同步使用同一个预发布版本号。

## [1.0.0-alpha.20] — 发布候选，尚未发布

- 有边界实验：结果资源/授权、重复schema批次、本地任务交接和易失节点转移；各语言支持范围不同，未承诺六SDK完整运行时一致性。
- 保留与等价HTTPgzip及普通工具的真实测量；不宣称固定token、费用或速度收益。
- 候选使用统一版本、本地CI与独立包验证；完整生产能力、beta和实际发布保持后续门槛。

## [1.0.0-alpha.19] —— 2026-09-05

### 修复

- 将已被取代的 roadmap 声明约束为有日期的历史快照，并链接其关闭版本／当前
  证据。alpha.6 DNS TXT、alpha.7 reputation client、alpha.5/alpha.17 daemon
  骨架／剩余工作和 provisional PEN 记录继续保留历史，但不再表现为当前 gap。
- 依据当前源码对账 CR/RFC lifecycle、六 SDK coverage matrix 与 proposal 阶段
  问题备注。RFC-0001/0002/0003 现为 Active，RFC-0006 英中均为 Accepted，
  CR-0011 索引与正文均为 Implemented，并以可执行 ledger 防止 `_TBD_`/
  `pending` coverage 回归。
- 将当前合同 AaaS L2-01..L2-07 恢复到 Node L2 suite，不再作为未来工作整体
  延期。v0.7 suite 现含 38 个 case；六语言 SDK catalog 与 validator 都强制
  L2-01..05，并仅在携带有理由的例外 message 时允许 L2-06/07 记 `na`。
- 将六语言 SDK conformance manifest emitter 全部对齐 Node L2 v0.6 与 31-case
  catalog。Validator 现在会拒绝旧 profile 版本、错误 summary、partial-family
  `na`，以及非法的 single-/multi-Anchor HA 适用性组合。
- 让仓库中每个对外声明的实现证据 artifact 都可运行且完整覆盖自身 scope：npsd
  穷举全部 20 个 L1 case，ingress 记录完整四项 TLS family，runner 穷举全部十项
  L3 case，并以机器可读 registry 防止 profile claim 丢失对应 manifest。
- 将 Node L1 套件计数修正为实际的 20 个 case 标题，使 Node L2 runtime catalog
  与 manifest 版本覆盖 v0.6 的全部 31 个 case，并新增机器可读证据清单，明确区分
  可执行、部分覆盖、仅 catalog 以及参考 IUT 不适用状态，不据此宣称认证。
- 将 `nps-runner` 当前能力声明与可执行行为对齐：portable OCI SpawnSpec 执行和
  周期性进程内租约续期已实现；持久化/共享租约、重启/回收保证及完整 L3 认证
  仍未完成，当前文档不再暗示这些能力已经具备。
- 用持久化 SQLite 协调替换 `nps-runner` 的进程内 lease：现已执行跨进程原子
  claim、process-instance fencing、过期租约回收、跨重启 terminal dedup、
  lease-loss worker cancellation 与 terminal-before-ack；case manifest 在
  TaskFrame DAG/Saga 部署用例执行前继续拒绝完整 L3 认证。
- 修复 `nps-runner` 生产依赖注入和 monorepo/standalone 两条 Docker build；
  CI 现在执行 daemon 自有 runner test suite。

## [1.0.0-alpha.18] —— 2026-08-15

### 新增

- 新增官方 NWP LLM usage telemetry（`input_tokens`、`output_tokens`、prefix/KV
  cache 命中、复用 token 与新计算 token）及 unary `CapsFrame.request_id` 关联。
  六语言 native NWP server helper 统一回显 request ID；并明确
  `CapsFrame.cached` 与模型 prefix/KV cache 复用不是同一语义（NPS-Dev#88）。
- 新增候选 NPS-CR-0011 / NWP 0.21 有状态 LLM context contract：owner-bound
  不透明 context ID、create/append/fork/reset/status/release、CAS 版本、原子
  unary/async 原子取消语义、NWM 0.2 发现、NIP 0.14 `llm:context` 授权、确定性错误，
  以及共享生命周期/replay/restart/accounting 向量（NPS-Dev#90）。Stateless
  completion 保持兼容，stateful 请求绝不静默 fallback。六语言参考 server
  均实现 stateful NDJSON streaming：仅在成功终帧原子提交，异常结束会中止，
  已完成序列可在新的 `stream_id` 下进行幂等重放。
- 新增用于有界活跃对象上限的 `NPS-LIMIT-RESOURCE`，以及在 decoder 边界测量
  请求的 `LlmUsageDto.wire_input_bytes`。
- 新增六语言 stateful LLM 黑盒集成覆盖：验证重连与响应丢失恢复、并发
  append/CAS 仅一个请求胜出，以及 server 重启后 process-local context
  明确丢失。
- 新增基于官方 MessagePack `ActionFrame` decoder 的 CR-0011 strict-native
  第二轮基准。确定性门禁在禁用 fallback 时验证 delta-only role/tool 语义等价，
  并分别报告更低的 decoder `wire_input_bytes` 与 runtime `evaluated_tokens`。

### 修复

- 六语言有状态 LLM Action coordinator 在未配置部署侧 authorizer 时改为 fail
  closed，并在 admission/commit 向 authorizer 传入精确 capability 集合
  （`llm:complete` + `llm:context`，使用 stream/tool 时再追加相应 capability）。
- 六语言的 19 条 CR-0011 共享合规向量改为 fixture-driven：测试会实际执行并
  校验每条 vector 的 `input`、`pre_state` 与 `expected` contract，不再只把
  fixture 当作 ID dispatch 清单。
- 六语言 Action Server 的 stateful LLM streaming 语义完成对齐：
  `stream=true` 不得与 async ack 组合；context receipt 只出现在成功终帧；
  失败或未完整结束的流会中止且不进入缓存；成功重试会在 server 新生成的
  stream identifier 下重放完全相同的序列。
- 修复 .NET NativeAOT MessagePack resolver 对可空 `UInt64` 的缺失支持，恢复
  NDP `AnnounceFrame.cluster_epoch` 的 Tier-2 round-trip；同时覆盖 Graph latency
  的 null 与有值两种可选 primitive 情况（NPS-Dev#89）。
- TypeScript runtime `VERSION` 改为从 `package.json` 派生，避免包版本更新后
  runtime banner 仍停留在旧版本。
- Go SDK 支持下限降至 Go 1.23，并固定到仍兼容该下限的最新
  `golang.org/x/crypto` 系列；release gate 会覆盖最低工具链。
- Java MessagePack 升至 0.9.11、Jackson 升至 2.18.9、BouncyCastle 升至
  1.84，清除 alpha.18 OSV 门报告的全部 runtime dependency advisory。

### 发布工程

- 修正 alpha.18 同步发布手册：兼容 ingress 不再属于强制列车；Rust 发布包含
  `nps-conformance` 在内的 8 个 crate；standalone 物化删除旧源码并 vendoring
  conformance fixture；Maven 增加 Python `zipfile` 回退；npm 与 crates.io
  preflight 必须证明发布权限。
- 新增 NativeAOT codec publish-and-run smoke 门，并把 NuGet workflow/family
  checker 对齐到 alpha.18 的 11 包 SDK 家族。

## [1.0.0-alpha.17] —— 2026-08-02

### 新增

- 将 .NET 参考实现的服务端与编排能力移植到 Python、TypeScript、Java、Rust 和 Go：NCP 原生传输、NWP Action/Complex/Memory Node 与双向 bridge、NIP CA 与完整校验、NOP 编排、daemon observability，以及 telemetry。
- 为官方 .NET 包族新增 NativeAOT-safe frame codec 与发布验证。
- 为 NCP 0.11 原生服务握手、NWP 0.20 Node/Bridge serving、NIP 0.13
  CA/吊销、NDP 0.12 registry 准入和 NOP 0.9 编排/runtime 决策新增可移植、
  语言无关 Profile 与共享一致性向量；六个 SDK 执行同一套 fixture。
- 完成 CR-0007 共享实现门，并为 `nps-runner` 加入 portable OCI
  SpawnSpec/引用解析、租约续期、生命周期强制和 daemon 自有 runtime 向量。

### 修复

- 升级存在漏洞的 Rust transport / PKI 依赖、TypeScript 测试工具链，并将 Go 最低工具链提升到 1.26.5；各语言依赖审计目前无可处理漏洞。
- 为 source-of-truth checker 增加窄范围的历史 retired identifier allowlist，避免协议历史记录造成虚假发布阻塞。
- 在 publish overlay 中保留 runner/ingress 独立测试访问权限，恢复 Ledger 独立分发版的 SQLite 依赖，并从 daemon 分发中移除存在漏洞的 SQLite runtime bundle。
- 让 `check-version-sync.py` 正确执行 `expected: skip`，使冻结或退役仓库能从后续 release train 中干净排除。
- 修正可移植 NWP 方法拒绝：`/.nwm` 现在返回 `Allow: GET`，并补上缺失的
  六语言共享向量。
- 六个 SDK 现在一致拒绝畸形的 NDP `graph_seq`、`ttl` 与 Bridge 协议声明，
  且拒绝时不推进 Registry sequence fence。
- 加固 Runner 远程 SpawnSpec 拉取的私网 SSRF、重定向绕过与 DNS rebinding
  防护；租约丢失后不再写入陈旧终态或 ack inbox。
- 在 `dpkg-deb` 校验前规范化 `DEBIAN/` 控制目录权限，使 daemon 的 Debian
  打包不再受调用方 `umask` 影响。

### 发布工程

- 准备协议规范、六种 SDK 分发仓、daemon overlay 与 package metadata 的同步 alpha.17 候选；未执行 tag 或发布。
- 准备 MCP、A2A、gRPC compatibility ingress 的最后一个 deprecated alpha.17
  构建；消费者应迁移到 `LabAcacia.NPS.NWP.Bridge`，alpha.18 起这些包退出
  必须同步的发布列车。

## [1.0.0-alpha.16] —— 2026-07-23

### 修复

- 由于 alpha.15 包已存在于公开 registry，本次以 alpha.16 重新发布整套版本。
- 修复 nps-daemons 分发仓测试隔离：nps-ingress 与 nps-runner 不再把测试源码编入应用项目，同时通过显式测试程序集可见性保留 internal 覆盖。

## [1.0.0-alpha.15] —— 2026-06-28

### 新增

- **NWP LLM/Thinking Profile**：NWM 新增 `profiles.llm`，用于模型服务型 Action/Complex Node；.NET 参考实现补齐模型描述、stream/tool 支持、隐私提示与 reasoning 暴露策略的官方 DTO/helper。NIP v0.11 注册标准 `llm:*` capability 字符串，用于发现与授权。
- **NWP HTTP binding 错误码注册表**：NWP v0.17 / `error-codes.md` v1.6 新增 HTTP overlay 传输拒绝（Origin、Content-Type、Accept、request-id 回传不匹配、frame body 无法解析）以及“已声明但未实现”capability rollout 窗口的规范化错误码，关闭 NPS-Dev#84。
- 在 SDK 源树与分发仓中实现 NCP Tier-3 BinaryVector policy，并补齐 malformed-frame / client-error conformance 覆盖。
- 新增入站 NWP Bridge server adapters：MCP 与 A2A server-side Bridge adapter，包含 ASP.NET hosting、JSON-RPC framing helper、tool / AgentCard metadata 与本地 action dispatch。

### Alpha 破坏性 wire 修正

- **NIP TrustFrame/RevokeFrame 签名 payload 对齐**：签名 payload 现在包含当前 NPS-3 字段（`issued_at`、`serial`、`signer_nid`、`target_nid`），并使用当前吊销状态 / 错误命名（`NIP-CERT-REVOKED`）。旧 alpha.14 形状签出的 frame 升级后不再通过验证。
- **NDP Announce 签名规范体对齐**：各 SDK 现在签同一个 canonical Announce body（排除 `signature` / `health` / `last_seen` / `frame`，省略 null optional，`heartbeat_interval_ms` 缺省时才按 `60000` 默认值参与签名，显式 `0` 作为 disabled 原样签入）。依赖旧 per-SDK 差异形态的 signed Announce 可能无法跨 SDK 验证。
- **NDP graph / revoke guard 强制执行**：SDK 现在一致强制 GraphFrame 节点/边界限与 NIP revoke parent 规则；过去可能漏过的 malformed frame 会按文档化协议错误拒绝。

### 加固

- 加固入站 NWP Bridge server dispatch：默认要求调用方验证，增加 request body 上限、dispatch timeout，并对客户端错误脱敏。
- 加固 native-mode parser：malformed Tier-3 BinaryVector frame 返回文档化 client error，而不是 server-internal failure；Go native serving 不再修改共享 negotiated tier 状态。

### 发布工程

- 将 Dev 单一事实源同步到 Release/spec、独立 SDK 源树、ingress/tool 分发仓、README banner、包 manifest、lockfile 与当前 alpha.15 版本元数据。

## [1.0.0-alpha.14] —— 2026-06-26

### .NET SDK

- **NuGet 包族发布加固**：alpha.14 原子打包完整公开 .NET 包族（`NPS.Core`、`NWP`、`NWP.Anchor`、`NWP.Bridge`、`NIP`、NIP SQLite/PostgreSQL 存储、`NDP`、`NOP`、`Daemon.Observability`、`NPS.Conformance` 以及 MCP/A2A/gRPC ingress 包），产出 `.snupkg` 符号包，并在推送 Nexus / NuGet.org 前校验 package + symbol 集合完整性。
- **`NPS.NWP.Bridge` concrete adapters**：Bridge Node 不再只是类型包。新增 `IBridgeDispatcher`、`BridgeDispatcherRegistry`、不绑定宿主的 `BridgeNode`、ASP.NET `AddBridgeNode` / `UseBridgeNode`，并内置 HTTP/HTTPS、gRPC JSON unary、MCP JSON-RPC、A2A JSON-RPC dispatcher。出站 Bridge 调用默认带 endpoint allowlist 与 private/loopback SSRF 防护。
- **Bridge target schema 文档**：新增 HTTP、gRPC JSON unary、MCP JSON-RPC、A2A JSON-RPC 的规范化 `bridge_target` schema。
- **Core codec DX**：`NpsFrameCodec` 新增默认 factory 与实例 `Peek(...)` helper，保留静态 `PeekHeader(...)`。
- **Daemon observability 解耦**：health/readiness 渲染抽到 transport-neutral `HealthProbeRenderer`，ASP.NET endpoint helper 复用同一 renderer。
- **NIP TrustFrame 开源边界**：文档明确开源 pinned-grantor 基础验证与 NPS Cloud 托管 federation 的边界，并新增开放消费者可用的 `TrustFrameValidator`。
- **Banyan 集成安全修复**：`NipIdentVerifier` 可接入 live revocation callback 或 `INipCaStore`；OCSP transport error 默认 fail-closed；`NcpServer` 支持 pre-preamble authenticated stream hook 以承载 TLS/mTLS；native-mode Hello 读取增加 timeout 与 payload size 上限。
- **NIP CA 吊销工件**：`/v1/crl` 现在返回 `issued_at` 与 CA detached signature；`/.well-known/nps-ca` 不再广告未映射的 `/ocsp` URL；`INipCaStore` 新增 `ListAsync()`，并提供内置 `InMemoryNipCaStore`。
- **类型化远程 NIP CA client**：`NipCaClient` 覆盖 CA discovery、CRL 获取、Ed25519 注册/续期/吊销/验证，以及 RFC-0002 X.509 注册流程。
- **Native-mode NWP serving**：`NwpNativeNodeServer` 让 Memory / Action Node 可以直接通过 `NcpSession` 或 native NCP stream 服务 `QueryFrame` / `ActionFrame`，不用宿主手写 frame loop。
- **Conformance harness 包**：新增 `LabAcacia.NPS.Conformance`，提供 Node L1/L2 case catalog、run manifest 与 CI 自认证 validation helper。
- **本地 dev stack**：新增 Docker Compose dev stack，用于 loopback `npsd`、开发 NIP CA 与 compat ingress smoke test。

### 维护

- 针对 alpha 阶段 XML 文档缺口集中 warning policy；对 `SQLitePCLRaw.lib.e_sqlite3` 增加显式 NuGet audit suppression，直到上游发布修复包。
- 更新 .NET SDK quickstart、包表与发布流程文档。

---

## [1.0.0-alpha.13] —— 2026-06-13

> **取代已撤回的 `1.0.0-alpha.12`。** alpha.12 NuGet 包在 release review
> 完成前带着易受攻击的 `MessagePack 3.0.300`（NU1903）与 native handshake
> 缺陷提前发布，现已弃用；alpha.13 保留同一功能集并补齐安全/握手修复。

### SDK —— 六种官方语言功能对等

- 将 Anchor Node server、CGN meter、Bridge types 与 reputation-policy evaluator
  从 .NET 移植到 Python / TypeScript / Go / Java / Rust，使五种非 .NET SDK
  从 client-only 晋级 client + server；补齐 `NWP-CGN-LIMIT-EXCEEDED` 与三个
  `NWP-REPUTATION-*` 错误码。

### 规范

- **NCP v0.8 / NPS-RFC-0006**：Draft → Proposed；统一 ALPN `nps/1.0`、TLS
  包裹、mTLS 与 `NCP-NID-MISMATCH` session-NID 绑定。
- **NWP v0.14**：新增 Bridge Node §16 MUST/SHOULD 与 `bridge_target` 向量。
- **NIP v0.10**：新增 1–24h 可续期 edge mTLS 证书 profile。
- **NDP v0.9**：新增 `health` / `last_seen`；过期解析返回 `NDP-RESOLVE-STALE`。
- **NOP v0.7 / NPS-CR-0007**：task claim lease、`NOP-CLAIM-CONFLICT`、
  `NOP-RUNTIME-IDLE-TIMEOUT`、`NOP-RUNTIME-MAX-RUNTIME`、
  `NOP-SPAWN-SPEC-INVALID` 与 `TC-N3-*`。

### Daemon（L2/L3）

- `nps-runner` 接入 NPS-CR-0007 lease/dedup；`nps-ingress` 接入
  NPS-RFC-0006 TLS 1.3/mTLS 与 `NCP-NID-MISMATCH`，分别通过 8/5 项单测。

---

## [1.0.0-alpha.11] —— 2026-05-28

### 规范

- **NCP v0.7**：`max_concurrent_streams`、`NCP-STREAM-LIMIT-EXCEEDED`、QUIC
  stream mapping、`NCP-REKEY-REQUIRED`。
- **NWP v0.13 / CR-0006**：SubscribeFrame、`trust_anchors` 与 `bridge_target`。
- **NIP v0.9**：`ocsp_staple`、PEN 65715.2.2/65715.2.3 与
  `NIP-OCSP-STAPLE-EXPIRED`。
- **NDP v0.8**：GraphFrame、`NDP-GRAPH-INVALID`、`NDP-GRAPH-TOO-LARGE`、
  federation 与 `NDP-FEDERATION-LOOP`。
- **NOP v0.6**：ACK/NAK、`NOP-STREAM-NAK`、`weighted_first_k`、`merge_all`、
  cross-cluster delegate、HMAC 与 `NOP-CALLBACK-HMAC-MISSING`。
- **CR-0006** 已 Accepted；**RFC-0006** 为 Draft。

### SDK

- 六个 SDK 同步交付上述 NOP、NIP、NDP、NWP 类型、wire 语义与 NWM 发布面。

### .NET SDK

- `AnchorNodeOptions.TrustAnchors`、CR-0006 `SubscribeFrame` rewrite、
  `IdentFrame.OcspStaple`、`IdNpsCapabilities`、AlignStream Ack/Nak、
  `TargetClusterAnchor`、GraphFrame types 与 `WeightedFirstK` / `MergeAll`。

### Daemon

- `nps-ledger` 增加 federation push 与 `X-NPS-Forwarded-By` 三跳检测；
  nps-probe v0.2、nps-orchestrator alpha.11、NPS-NWP-Manager v0.1 同步落地。

---

## [1.0.0-alpha.10] —— 2026-05-28

### 规范

- **NPS-CR-0005** 定义 allowlist、bootstrap-token 与 pending-queue 三级 RA，
  并增加 `db/003_ra_model.sql`。

### SDK

- 六 SDK 同步 NOP saga compensation、NDP security profile、
  `IdentReputationPolicyHint` 与 `IdentMetadata`。

### .NET SDK

- `NPS.NIP` 实现 RA 三级 enrollment、store 接口与管理端点。

---

## [1.0.0-alpha.9] —— 2026-05-28

### SDK

- 六 SDK 补齐 RFC-0004 `ReputationLogClient`、RFC 9162 inclusion proof、
  `AnchorNodeClient` 测试与 RFC-0003 assurance level/metadata。

---

## [1.0.0-alpha.8] —— 2026-05-28

### 规范

- NPS-RFC-0005 与 NPS-RFC-0002 从 Draft 晋级 Accepted。

### .NET SDK

- `ReputationPolicyEvaluator`、`NWP-REPUTATION-THROTTLED`、
  `NWP-REPUTATION-REJECTED`、`NWP-REPUTATION-BANNED`、
  `X-NWP-Reputation-Status`、`X-NWP-Ban-Expires`、`cgn_limit` 与早期
  SubscribeFrame（0x12）进入 .NET SDK。

---

## [1.0.0-alpha.7] —— 2026-05-18

### .NET SDK

- **`NPS.NWP.Bridge` —— Bridge Node HTTP/HTTPS 调度器**（关闭 issue #66，NPS-CR-0001 §3.2 Phase 1）：完整 HTTP/HTTPS 协议适配器发布 —— `IBridgeDispatcher` / `BridgeDispatcherRegistry` 抽象、带 SSRF 防护的 `HttpBridgeDispatcher`（RFC-1918 拒绝，`[GeneratedRegex]`）、`BridgeNodeMiddleware`（ASP.NET Core —— 路由 `GET /.nwm` 与 `POST /invoke`，结果封装为 CapsFrame）、`BridgeServiceExtensions`（`AddBridgeNode` / `UseBridgeNode`）。gRPC / MCP / A2A 适配器按 Phase 1 范围推迟到后续 CR。CR-0001 状态更新为"已实现"。

### TypeScript SDK

- **`MemoryNodeServer` —— 框架无关 HTTP 处理器**（关闭 issue #60）：`IMemoryNodeProvider` 接口 + `MemoryNodeServer` 类，服务 `GET /.nwm`、`GET /.schema`、`POST /query`、`POST /stream`；SHA-256 anchor_id 计算；token 预算裁剪；鉴权门控；Node.js `http.IncomingMessage` 适配器。14 个测试覆盖全部端点、鉴权、预算裁剪及错误场景。

### Go SDK

- **`nwp.MemoryNodeServer` —— `http.Handler` Memory Node**（关闭 issue #60）：TypeScript `MemoryNodeServer` 的 Go 等价实现。`IMemoryNodeProvider` + `StreamingProvider` 接口；`ServeHTTP` 路由；`go vet` 通过。

### 演示

- **`cross-sdk-interop` —— 扩展至每客户端 4 个测试场景**（关闭 issue #63）：四语言客户端（dotnet / python / node / go）在原有 `GET /.schema` + `POST /query` 基础上新增 `GET /.nwm` 发现 + `POST /stream` NDJSON 汇总测试。跨语言 wire 格式等价性已验证。

### 日常维护

- NPS-Dev 及所有镜像仓库的 SDK、compat README（EN + CN）补充 badge 块。38 个 README 文件的协议版本 badge（`NCP v0.6`、`NWP v0.12`、`NIP v0.8`、`NDP v0.7`、`NOP v0.5`）现已全部一致。
- `README.md` 目录树中的规范版本注释更新至当前版本（`NWP v0.12`、`NIP v0.8`、`NDP v0.7`）。
- `impl/typescript/src/index.ts` 中的 `VERSION` 从过期的 `alpha.2` 修正为 `alpha.7`。

---

## [1.0.0-alpha.6] —— 2026-05-14

### 规范

- **NPS-CR-0004 — IANA PEN 65715 wire-in**：IANA 已于 2026-05-08 向 *Neuro Protocol Suites Committee* 分配 Private Enterprise Number **65715**，替换此前未注册的临时 arc `1.3.6.1.4.1.99999`。所有 NPS X.509 OID 现锚定于已分配 arc `1.3.6.1.4.1.65715`：`id-nps-eku-agent` = `1.3.6.1.4.1.65715.1.1`，`id-nps-eku-node` = `1.3.6.1.4.1.65715.1.2`，`id-nid-assurance-level` = `1.3.6.1.4.1.65715.2.1`，`id-nps-node-roles` = `1.3.6.1.4.1.65715.2.2`。**NPS-RFC-0002** 晋级 Draft → Proposed（移除 EXPERIMENTAL 横幅；OQ-2 生产使用门槛关闭）。NIP 版本号升至 **0.8**。Roadmap R08 PEN 分配阻塞已关闭。**破坏性变更**（不保留双 OID 过渡期）：在 `1.3.6.1.4.1.99999.*` 下签发的证书 MUST 撤销并重签后方可主张合规。六个 NPS-Dev `impl/` SDK 已在 65715（已核验）；六个 `NPS-sdk-*` 镜像仓与四个冻结的 `nip-ca-server/example/` 端口在本次发布完成 wire-in。详见 `spec/cr/NPS-CR-0004-pen-wirein.md` 与 `pen-impact.md`（审计清单，§9 执行顺序）。

- **发布纪律 —— alpha 没有小版本**：`docs/release-process.cn.md` 明确 `X.Y.Z-alpha.N → X.Y.Z-alpha.N+1` 是 alpha 唯一合法推进路径。`1.0.0-alpha.5.1` 或 `1.0.0-alpha.5.2` 这类活跃标签无效；alpha.5 之后仍需补入的内容并入 `1.0.0-alpha.6`。

- **NPS-CR-0002 Phase 2 规范完成（NWP v0.12）**：§8.2 为 `DiffFrame` 新增 `cgn_est` 字段，用于按事件估算 Cognon；§12.2 注册 `anchor_state` 与 `resync_required` 事件类型；§12.4 扩展授权模型，引入 `topology:subscribe` 能力、流中拒绝（mid-stream rejection）语义以及与声誉系统的交互说明；`spec/frame-registry.yaml` 中 0x02（DiffFrame）和 0x12（SubscribeFrame）的描述同步更新以反映新字段。

- **NPS-3-NIP §5.2 TrustFrame — 完整规范**：新增十字段表格（含 `serial`、`signer_nid`）；签名采用 Ed25519/JCS 规范化规则；新增五个错误码（`NIP-TRUST-FRAME-INVALID`、`NIP-TRUST-FRAME-EXPIRED`、`NIP-TRUST-FRAME-GRANTOR-REVOKED`、`NIP-TRUST-FRAME-SCOPE-EXCEEDS-GRANTOR`、`NIP-TRUST-FRAME-NODES-PATTERN-INVALID`）；§7 验证流程更新以接入 TrustFrame 校验。CN 镜像（`NPS-3-NIP.cn.md`）同步更新。

- **NPS-3-NIP §5.3 RevokeFrame — 完整规范**：新增字段表（含条件字段 `parent_nid`）；reason 枚举表附明确的向后兼容性策略；签名规则；四个新错误码；§7 入站推送验证步骤插入。CN 镜像（`NPS-3-NIP.cn.md`）同步更新。

- **NPS-3-NIP §5.1 IdentFrame — `cert_chain` + `cert_format`**：IdentFrame 字段表新增 `cert_chain`（DER 证书链，以 base64url 编码数组形式承载）和 `cert_format`（`x509-der` | `raw-pubkey`）。

- **NPS-CR-0003 — Draft → Accepted 推进（2026-05-11）**：Orchestrator 组 / 短时会话 NID CR 已于 2026-05-11 由 Draft 推进至 Accepted。.NET 参考实现已在本次发布中落地；非 .NET SDK 端口仍延后。

- **NPS-CR-0005 — NIP CA RA 模型（Draft）**：起草新 CR，为 NIP CA Server 的 Registration-Authority 定义三级登记授权模型：**allowlist**、**bootstrap-token**、**approval-queue**。填补此前 CR 对非 Orchestrator 类登记策略未作规范的空白。

- **`spec/frame-registry.yaml` — 六个帧 draft → proposed 晋级**：NWP `0x10`（QueryFrame）、`0x11`（ActionFrame）、`0x12`（SubscribeFrame）以及 NIP `0x20`（IdentFrame）、`0x21`（TrustFrame）、`0x22`（RevokeFrame）在 alpha.6 字段表冻结后，由 `status: draft` 一并晋级为 `status: proposed`。

- **合规测试向量**：NWP 套件新增 **49 个向量** —— `action_frame`（+13），并新增 `subscribe_frame` 与 `query_frame` 聚合路径覆盖。NIP 套件新增 **TrustFrame 11 个**、**RevokeFrame 12 个**，并扩展 `IdentFrame` 的 lineage 与 `assurance-level` 覆盖。

### .NET SDK

- **`NPS.NWP.Anchor` —— NPS-CR-0002 Phase 2 alpha.6 边界**：`topology.stream` push/notify 继续由 `AnchorNodeMiddleware` + `IAnchorTopologyService` 提供，alpha.5 的 `topology.filter.node_kind` 兼容窗口已关闭。客户端必须发送 `topology.filter.node_roles`；旧别名返回 HTTP 400 / `NWP-TOPOLOGY-FILTER-UNSUPPORTED`。

- **`NPS.NIP` —— group/session NID 签发**：新增 group register、session issue、
  audit listing 与级联 revoke；链校验返回 `NIP-CERT-PARENT-REVOKED`。
  Group-JWS 校验明确返回 `NIP-CA-GROUP-REVOKED`、`NIP-CA-PARENT-NOT-FOUND`、
  `NIP-CA-PARENT-NOT-GROUP`、`NIP-CA-SESSION-VALIDITY-INVALID`、
  `NIP-CA-JWS-INVALID` 与 `NIP-CA-JWS-EXPIRED`。

### NIP CA Server

- `/v1/orchestrators/groups/...` 注册、吊销、session 签发与审计端点上线；
  `/.well-known/nps-ca` 发布 `orchestrator-group` capability。

### 测试

- `NPS.Tests.Nip.OrchestratorGroupSessionTests` 增加 16 项 xUnit case，覆盖
  NPS-CR-0003 的五项验收场景及有效期、scope、JWS、审计边界。

### 实现

- **`npsd` + `nip-ca-server` — 运维端点基线**：两个 daemon 现均暴露 `/healthz`（存活）、`/readyz`（就绪）与 `/metrics`（Prometheus 暴露格式）端点。`SIGTERM` 触发优雅关停，含 30 秒 drain 窗口。JSON 结构化日志由新引入的 `NPS.Daemon.Observability` 共享库提供；启动时通过 `NPS_LOG_LEVEL` 环境变量控制日志详细程度。

### Ops

- **`deploy/docker-compose/`**：新增 `docker-compose.yml` 与 `.env.example`，支持一条命令启动完整的 NPS daemon 栈。

- **`deploy/systemd/`**：新增 `npsd` 与 `nip-ca-server` 的 systemd unit 文件，并附 `install.sh` 安装脚本，自动安装 unit 并重新加载 `systemd`。

- **`Makefile`**：新增顶层目标 `up`、`down` 与 `install-systemd`，封装上述 docker-compose 与 systemd 流程。

---

## [1.0.0-alpha.5] —— 2026-05-01

### 规范

- **NPS-RFC-0004 Phase 3 — STH Gossip 协议**：新增 §4.5 STH Gossip Protocol。每个日志运营方 MUST 暴露 `GET /v1/log/gossip/sth`，返回 `own_sth` 和缓存的 `peer_sths`。后台 gossip 推送周期（通过 `NPSLEDGER_PEERS` + `NPSLEDGER_GOSSIP_INTERVAL_S` 配置）：拉取 → 验证 Ed25519 签名 → 单调性检查（`tree_size` MUST 不递减）→ 缓存。分叉检测：`tree_size` 回退时记录 `NIP-REPUTATION-GOSSIP-FORK` 并停止接受该对端的 STH。两个新错误码：`NIP-REPUTATION-GOSSIP-FORK`（`NPS-SERVER-INTERNAL`）和 `NIP-REPUTATION-GOSSIP-SIG-INVALID`（`NPS-CLIENT-BAD-FRAME`）已加入 `spec/error-codes.md`。§4.5 重新编号：原 §4.5 错误码 → §4.7；原 §4.7 向后兼容性 → §4.8。OQ-1（gossip 协议选型）已决议采用轻量 NPS 原生变体。Phase 3 阶段行标记已完成。

- **AaaS-Profile v0.5 → v0.6 — L2-09 默认 `reputation_policy`**：§4.3 Level 2 表格新增 L2-09（SHOULD）：配置 `reputation_policy` 并查询 RFC-0004 日志运营方；推荐最小策略：`fail_open: true`，拒绝 `cert-revoked ≥minor` 及 30 天内 `scraping-pattern ≥major`。

- **`spec/status-codes.md` / `spec/status-codes.cn.md` — `NPS-SERVER-UNSUPPORTED`**：新增状态码（HTTP 501），用于服务端未实现的操作（如 `QueryFrame`/`SubscribeFrame` 收到未实现的保留 `type` 值）。协议错误映射表新增 `NWP-RESERVED-TYPE-UNSUPPORTED → NPS-SERVER-UNSUPPORTED`。

- **`spec/error-codes.md` — `NWP-RESERVED-TYPE-UNSUPPORTED`**：新增错误码（`NPS-SERVER-UNSUPPORTED` → HTTP 501），用于 `QueryFrame`/`SubscribeFrame` 的 `type` 值在保留命名空间内但节点未实现时。含与 `NWP-ACTION-NOT-FOUND`（未知 `action_id`）的明确区分说明。

- **`spec/token-budget.md` §7.2 — 每事件 `cgn_est` 字段（SHOULD）**：推送流 SHOULD 在每个 `TopologyEventEnvelope`（及类似 envelope 类型）上携带 `cgn_est` 字段，值为事件负载的 UTF-8/4 字节估算。允许 Agent 在不自行计数字节的情况下追踪 token 消耗。

- **wire 字段重命名 —— `AnchorActionSpec.estimated_npt → cgn_est`**（破坏性）：与 CGN 命名规范对齐，同时与 `TopologyEventEnvelope.cgn_est` 保持一致。**Wire 破坏性变更** —— 固定旧字段名的客户端升级后将看到该字段为 null。不保留别名。关联 [labacacia/NPS-Dev#17](https://github.com/labacacia/NPS-Dev/issues/17)。

### .NET SDK

- **`NPS.NWP.Anchor` — `NWP-RESERVED-TYPE-UNSUPPORTED` 执行**：`AnchorNodeMiddleware` 在 `/anchor/query` 或 `/anchor/subscribe` 收到未实现的保留 `type` 时，现在返回 HTTP 501 / `NPS-SERVER-UNSUPPORTED` / `NWP-RESERVED-TYPE-UNSUPPORTED`。之前错误地返回 404 / `NWP-ACTION-NOT-FOUND`。

- **`NPS.NWP.Anchor` — `topology:read` 能力门控**：新增 `AnchorNodeOptions.RequireTopologyCapability`（默认 `false`）。启用后，`/query` 和 `/subscribe` 均检查 `X-NWP-Capabilities` 请求头是否包含 `"topology:read"`（大小写不敏感，逗号分隔）；缺失时返回 HTTP 403 / `NPS-AUTH-FORBIDDEN` / `NWP-TOPOLOGY-UNAUTHORIZED`。新增 `NwpHttpHeaders.Capabilities = "X-NWP-Capabilities"` 常量。

- **`NPS.NWP.Anchor` — `TopologyEventEnvelope` 上的 `cgn_est`**：`TopologyEventEnvelope` 新增可空字段 `cgn_est: uint?`，每次推送事件时以 `Math.Max(1, UTF8.GetByteCount(payload) / 4)` 填充。

- **`NPS.NIP` — `AssuranceLevels.FromWireOrAnonymous("")` 修复**：空字符串 `""` 现在返回 `Anonymous`（与 `null` 一致）。Python、TypeScript 和 Java SDK 收到同样修复。

- **`NPS.NIP` — `SqliteNipCaStore`**：新增基于 SQLite（`Microsoft.Data.Sqlite`）的 `INipCaStore` 实现，适用于无 PostgreSQL 依赖的单二进制 / 嵌入式 CA 部署场景。Capabilities 以 JSON 字符串形式存储；序列号通过原子 `nip_serial` 表生成。工厂方法：`await SqliteNipCaStore.OpenAsync(connectionString)`。关联 [labacacia/NPS-Dev#19](https://github.com/labacacia/NPS-Dev/issues/19)。

- **`NPS.NIP` — 可插拔 `INipCaStore` 注入**：`NipServiceExtensions` 新增两个重载：`AddNipCa(configure, INipCaStore store)` 接受任意 store 实现；`AddNipCaWithSqlite(configure, sqliteConnectionString)` 是便捷包装，在启动时同步打开并迁移 SQLite 数据库。原有 `AddNipCa(configure)` 保留 PostgreSQL 行为，但新增对 `ConnectionString` 非空的校验，并在不满足时输出清晰的错误提示。`NipCaOptions.ConnectionString` 从 `required string` 改为 `string?`，使用自定义 store 的调用方无需再设置该字段。关联 [labacacia/NPS-Dev#18](https://github.com/labacacia/NPS-Dev/issues/18)。

### Daemons

- **`daemons/nps-ledger/` — Phase 3：STH Gossip**：新增 `GossipState` 单例，持有对端配置（`NPSLEDGER_PEERS` JSON 数组 `{log_id, endpoint, pub_key?}`）并缓存每个对端最近验证的 `SignedTreeHead`。新增 `GossipService`（`BackgroundService`），每 tick 运行 gossip 推送周期（`NPSLEDGER_GOSSIP_INTERVAL_S`，默认 30 s）：拉取 `GET {peer}/v1/log/gossip/sth`、验证 Ed25519 签名（配置了 `pub_key` 时；否则发出警告跳过，仅限开发环境）、执行单调性检查、接受后缓存。新端点 `GET /v1/log/gossip/sth` 返回 `{own_sth, peer_sths}`。`/health` 更新为 `version: "1.0.0-alpha.5"`、`phase: 3`、`gossip_peers`、`gossip_interval_s`。新增 `NPSLEDGER_PEERS` 和 `NPSLEDGER_GOSSIP_INTERVAL_S` 环境变量。csproj version → `1.0.0-alpha.5`。

### SDK 修复

- **Python `nps_sdk` — `AssuranceLevel.from_wire("")` 返回 `ANONYMOUS`**：`if wire is None:` 改为 `if not wire:`，`None` 和 `""` 均返回 `ANONYMOUS` 而非抛出 `ValueError`。

- **TypeScript `@labacacia/nps-sdk` — `AssuranceLevel.fromWire("")` 返回 `Anonymous`**：`if (wire == null)` 改为 `if (!wire)`。

- **Java `nps-sdk` — `AssuranceLevel.fromWire("")` 返回 `ANONYMOUS`**：`if (wire == null)` 改为 `if (wire == null || wire.isEmpty())`。

### 测试

- 测试数量：**629 → 656**（全部通过）。
  - `GossipStateTests.cs` — 13 个新测试：区间 clamp、空状态不变量、`AcceptPeerSth` 存储与更新、`CurrentPeerSths` 多对端、`ReceivedAt` 时间戳、`FromEnvironment` 解析（无对端 / 有效 JSON / 含 pub_key / 畸形 JSON 兜底）、NipSigner 经缓存 STH 的签名轮回。
  - `AnchorTopologyTests.cs` — `TopologyQuery_UnknownReservedType_Returns501WithCorrectCode`（重命名 + 断言从 404→501）；新增 `TopologySnapshot_MissingCapability_Returns403`。
  - `AssuranceLevelTests.cs` — `FromWireOrAnonymous_NullOrEmpty_ReturnsAnonymous`（替换原合并测试）；`FromWireOrAnonymous_UnknownNonEmpty_Throws`（新增，验证 spec m6 执行）。

### 修复

- **m8 — RFC-0002 §4.4 引用了不存在/错误的 NIP 章节**：`spec/rfcs/NPS-RFC-0002-x509-acme-nid-certs.md` §4.4 写道 `Servers MUST implement timing-safe comparison per spec/NPS-3-NIP.md §10.2`。NIP §10.2 实际存在，但涵盖的是 OCSP 响应时间规范化——另一种时序问题，与挑战 token 比较无关。修复：将该交叉引用替换为内联自包含要求（`服务端 MUST 使用常数时间比较验证签名后的挑战 token，防止时序预言攻击`），并附注说明 §10.2 不适用于此处。

- **m7 — NWP §6.6 使用 SubscribeFrame 取消流式查询但无设计说明**：取消指令用 `SubscribeFrame(action="unsubscribe")` 停止流式查询——订阅语义帧被用于查询状态机——但没有任何设计说明。修复：在 §6.6 新增 **注** 解释这是协议层面统一的流取消信号（有意复用），节点通过 `stream_id` 路由，与流源自 QueryFrame 还是 SubscribeFrame 无关。

- **m6 — NIP §5.1.1 `assurance_level` 枚举缺少未来未知值的向前兼容规则**：规范定义了 `anonymous / attested / verified`，但对收到枚举外值时应如何处理只字未提——引发歧义：是静默降级为 `anonymous` 还是拒绝。修复：§5.1.1 新增 **向前兼容** 段落：未知值 MUST 触发 `NIP-ASSURANCE-UNKNOWN`（NPS-CLIENT-BAD-FRAME）；明确禁止静默降级为 `anonymous`，以防止为未来更高保证等级留下安全漏洞。

- **m5 — CERTIFIED.md 自声明模板对 RFC 8785 规范化描述不充分**：`NPS-NODE-L1-CERTIFIED.md` 和 `NPS-NODE-L2-CERTIFIED.md` 均写"RFC 8785 canonicalized"但未指明关键要求（UTF-8 编码、键排序、IEEE 754 数字格式、Unicode 转义规则），跨语言实现对同一逻辑 manifest 可能产生不同 SHA-256 摘要。修复：两个模板的 Attestation 节均新增 **RFC 8785 JCS 要求** 提示块，列举四项必须满足的属性并建议使用 JCS 兼容库。

- **m4 — `spec/token-budget.md` v0.2 没有持续推送操作的预算语义**：规范定义了请求/响应的 `X-NWP-Budget`，但未涉及流式查询（`QueryFrame stream:true`）和 SubscribeFrame 推送流（`topology.stream`）。修复：`token-budget.md` v0.2 → v0.3；新增 §7「流式与订阅预算策略」，定义：§7.1 流式查询——预算按批次执行，节点 MUST 裁剪或终止超预算批次；§7.2 推送流——节点不执行 `X-NWP-Budget`（由 Agent 侧控制），每个事件 SHOULD 上报 `X-NWP-Tokens`；设计原因说明服务端强制与实时交付目标不兼容。

- **m3 — NOP §3.1.1 "3 层"委托链上限定义模糊（角色数 vs. 跳数）**：文本写"最大委托链深度：3 层（Orchestrator → Worker → Sub-Worker）"但未说明"层"是计实体还是计跳数。`NOP-DELEGATE-CHAIN-TOO-DEEP` 的描述也只写"默认 3 层"，无进一步解释。修复：§3.1.1 条目重写，明确说明每个实体算一层（Orchestrator=1、Worker=2、Sub-Worker=3；第 4 个实体触发错误），并增加等价的跳数说明（≤ 2 次 DelegateFrame 跳转）。错误码表描述同步更新。

- **m2 — NDP §5 DNS TXT `v=nps1` 与 NCP preamble `NPS/1.0` 版本字符串风格不一致且无说明**：同一协议版本在相邻规范中出现两种不同的版本字符串格式，没有交叉引用或设计说明，造成表面不一致。修复：在 NDP §5 新增注释，说明 `v=nps1` 遵循 DNS TXT 键值惯例，`NPS/1.0\n` 是传输层的 RFC 风格标识符；二者服务于不同协议层，编码形式的差异是有意为之。

- **m1 — NCP §3.1 扩展帧头 ASCII 图表底边有误且缺少 `Reserved` 字段描述**：8 字节扩展帧头的 ASCII 艺术图底边为 `┘──────────────┘`（缺少 `┴` 连接符），应为 `┴──────────────┘`，导致 Reserved 格看起来与主框分离。字段描述列表也遗漏了 `Reserved` 条目，读者无法直观确认 8 字节总长。修复：将底边修正为 `┴──────────────┘`；增加 `**Reserved**：2 字节（Byte 6–7，仅扩展头存在）；发送方 MUST 置 0，接收方 MUST 忽略。扩展头共 8 字节：1 + 1 + 4 + 2。` 条目。

- **`spec/rfcs/NPS-RFC-0003` — critical 扩展翻转加 phase gate**：RFC-0003 §4.2 原先对证书扩展校验和 MISMATCH 关闭写的是无条件 MUST，而修订历史注明 critical-extension flip "尚未生效"——构成降级攻击窗口。修复：§4.2 字段表和 prose 按阶段拆分：Phase 1–2（当前）为 SHOULD 检查，强制可选；Phase 3（flag day，见 §8.1）为 MUST 强制。§8.1 Phase 3 行新增明确的 flag day 定义（在 NPS-Dev GitHub Discussions 提前 ≥ 21 日历天公告）。`id-nid-assurance-level` critical 扩展升级段标注"（Phase 3 — 尚未生效）"。同步在 NPS-3-NIP §5.1.1 IdentFrame 字段描述和 §5.1.1 prose 加 phase gate。

- **`spec/rfcs/NPS-RFC-0002` — 临时 OID 显式封锁**：RFC header 和正文顶部新增 `WARNING: EXPERIMENTAL` 警告块；Status 行改为 `Draft — EXPERIMENTAL`；§10 OQ-2 从"申请中"强化为一组 MUST NOT 约束列表。`spec/NPS-3-NIP.md` §5.1.1 `"attested"` 行新增合规封锁说明：临时 OID `1.3.6.1.4.1.99999.1` 在 IANA PEN 分配前不满足"RFC-0002 兼容"的合规或生产要求。

- **`spec/NPS-2-NWP.md` — 幽灵字段 `auth.min_assurance_level` 修复**：NWP §4.1 散文承诺了通过 ActionSpec 上的 `auth.min_assurance_level` 字段做 per-action 保证级别覆盖（§4.6），但 §4.3 auth 字段表和 §4.6 ActionSpec 字段表均未定义该字段。修复：在 §4.6 ActionSpec 字段表中直接新增 `min_assurance_level: string（可选）`（平级字段，与 `required_capability` 保持一致）；同步修正 §4.1 散文和 §15 v0.6 changelog 行，去掉错误的 `auth.` 前缀。（NPS-RFC-0003）

- **`spec/error-codes.md` v0.9 → v1.0**：四个 NWP 拓扑错误码（`NWP-TOPOLOGY-UNAUTHORIZED`、`NWP-TOPOLOGY-UNSUPPORTED-SCOPE`、`NWP-TOPOLOGY-DEPTH-UNSUPPORTED`、`NWP-TOPOLOGY-FILTER-UNSUPPORTED`）已在 NPS-2 NWP §13 和 NPS-CR-0002 §3.5 中定义并引用，但 alpha.4 规范同步时漏写入 error-codes 注册表。本次补入注册，措辞与 NWP §13 定义保持一致。

- **M2 — `"gateway"` 遗留值拒绝缺少错误码**：NPS-CR-0001 要求 MUST 拒绝 `node_roles`（NDP）和 `node_type`（NWP NWM）中已移除的 `"gateway"` wire 值，但未指定具体错误码——各 SDK 可随意使用任何码，客户端无法区分"已知遗留值"与"完全无效值"。修复：`spec/error-codes.md` v1.0 → v1.1，新增四个错误码：`NDP-ANNOUNCE-ROLE-REMOVED`、`NDP-ANNOUNCE-ROLE-UNKNOWN`（NDP），`NWP-MANIFEST-NODE-TYPE-REMOVED`、`NWP-MANIFEST-NODE-TYPE-UNKNOWN`（NWP）。`-REMOVED` 码用于 `"gateway"` 专属情况（响应 SHOULD 携带 hint 指向 CR-0001），`-UNKNOWN` 为其他未知值的通用兜底。NPS-4-NDP §3.1 / §6、NPS-2-NWP §2.1 / §4.1 / §13 及 NPS-CR-0001 §4 均更新引用新错误码。

- **M8 — AaaS-Profile L2 / Node-Profile L2 跨 Profile 契约隐式**：`NPS-AaaS-Profile.md` L2-08 要求 Anchor Node 实现 `topology.snapshot` / `topology.stream`，但未说明 *宿主 daemon* 需满足什么条件；`NPS-Node-Profile.md` §8 对 Node L1 只写了 SHOULD，构成认证漏洞——Anchor 可以声称 AaaS L2 但运行在不合规的宿主上。修复：（1）`NPS-AaaS-Profile.md` v0.4 → v0.5：§4.3 L2-08 描述扩展——声明 L2-08 的实现 MUST 满足宿主 Anchor 的 Node-Profile L1；维护活跃成员注册表的 Anchor SHOULD 同时满足 Node-Profile L2。Depends-On 升级：NWP v0.8 → v0.10、NIP v0.4 → v0.6、NDP v0.5 → v0.6。（2）`NPS-Node-Profile.md` v0.1 → v0.2：§4 NWP 条目扩展——L2 Anchor Node MUST 实现 `topology.snapshot` / `topology.stream`（NPS-2 §12），新增 AaaS L2-08 交叉引用；§8 关系段落：Anchor 宿主 Node-Profile L1 由 SHOULD 升为 MUST；声明 AaaS L2-08 的 Anchor MUST 同时满足 Node-Profile L2。Depends-On 升级：NCP v0.5 → v0.6，新增 NPS-2（NWP v0.10），NIP v0.3 → v0.6，NDP v0.4 → v0.6。

- **M7 — L2 合规套件零负路径测试用例**：`spec/services/conformance/NPS-Node-L2.md` §3 的 7 个 TC 全是 happy path——静默接受 `depth=999`、忽略未知 filter key、向未鉴权调用方返回拓扑数据的实现也能通过 L2 认证。"每个 MUST 子句都有一个失败路径 TC"的不变量未被满足。修复：`NPS-Node-L2.md` v0.1 → v0.2 新增 5 个负路径 TC（TC-N2-AnchorTopo-04 至 -08）：无 `topology:read` 的未鉴权访问 → `NWP-TOPOLOGY-UNAUTHORIZED`；depth 超过 Anchor 上限 → `NWP-TOPOLOGY-DEPTH-UNSUPPORTED`；不识别的 scope → `NWP-TOPOLOGY-UNSUPPORTED-SCOPE`；不识别的 filter key → `NWP-TOPOLOGY-FILTER-UNSUPPORTED`；不识别的 reserved type → `NWP-RESERVED-TYPE-UNSUPPORTED`。TC 总数 7 → 12；认证通过阈值同步更新。同步修正 TC-N2-AnchorTopo-01 和 -03 中 `node_kind` → `node_roles`（M1 一致性）。

- **M6 — 拓扑读回鉴权为"实现自定"且无身份↔角色绑定**：NWP §12.4 把授权策略完全推给实现方，§14.7 只写了 SHOULD restrict to NIDs with `node_roles: "anchor"`——但 `node_roles` 只存在于 NDP `AnnounceFrame` 和 NWM，IdentFrame 里没有。连接时出示的身份凭证和"我是 anchor"之间没有任何可绑定的机制，导致高敏感的拓扑数据（完整集群成员表 + 容量指标）没有可测试的访问门控。修复：（1）NWP §12.4 "Authorization model" 条目替换为三级最小绑定——Phase 1–2：Anchor Nodes MUST 要求 `IdentFrame.capabilities` 包含 `topology:read`（主要 gate，key-signed 但自声明）；SHOULD 额外校验 NDP `node_roles` 含 `"anchor"` 作为深度防御；Phase 3 [RFC-0002 stable]：SHOULD 额外验证 CA 背书的 `id-nps-node-roles` cert extension。（2）§14.7 改为引用 §12.4 定义的最小绑定，去掉原有 "until standardized" 措辞。（3）`spec/NPS-3-NIP.md` v0.5 → v0.6：标准 `capabilities` 注册表新增 `topology:read`。（4）NWP `Depends-On` NIP 升到 v0.6。规范版本：NWP v0.9 → v0.10，NIP v0.5 → v0.6。

- **M5 — `spec/frame-registry.yaml` alpha.4 帧变更未同步**：alpha.4（NPS-CR-0002）给 `QueryFrame`（0x10）、`SubscribeFrame`（0x12）、`DiffFrame`（0x02）分别加了可选字段或扩展了枚举，但 `frame-registry.yaml` 停在 v0.9 未动。注册表是 CI 和代码生成使用的机器可读事实源，不更新意味着帧接口的变化在版本 diff 和 git blame 中完全隐形。修复：注册表 v0.9 → v0.10；`QueryFrame` 和 `SubscribeFrame` 的 `protocol_version` 从 `0.5` 升到 `0.8`（NWP v0.8 新增可选 `type` 字段用于保留查询命名空间）；`DiffFrame` 的 `protocol_version` 从 `0.5` 升到 `0.6`（NCP 当前版本 v0.6；NPS-CR-0002 NWP §8.2 扩展 `event_type` 枚举，新增 `member_joined` / `member_left` / `member_updated`）。三个条目的 description 均已补入新字段说明及来源 CR。

- **M4 — `NWP-ACTION-NOT-FOUND` 在 §12 保留查询拒绝中被重载**：NWP §12 规定对 `QueryFrame` 上无法识别的保留 `type` 值用 `NWP-ACTION-NOT-FOUND` 拒绝，但该码在 §7.1 已定义为"`action_id` 未注册"。客户端收到 `NWP-ACTION-NOT-FOUND` 时无法区分"action_id 写错"与"对端不支持这个保留操作"——灰色降级路径写不了。修复：`spec/error-codes.md` v1.1 → v1.2 新增 `NWP-RESERVED-TYPE-UNSUPPORTED`（`NPS-SERVER-UNSUPPORTED`）；NWP §12 拒绝规则改用新码；§13 错误表同步更新。error-codes.md 和 §13 描述中均添加与 `NWP-ACTION-NOT-FOUND` 的明确区分说明。

- **M3 — RFC-0004 §4.3/§4.4 Phase 2 功能误标为 Phase 1**：RFC-0004 §4.3 在一个代码块中列出全部四个 HTTP endpoint（含 `/sth`、`/proof`），暗示它们属于 Phase 1；§4.4 以现在时描述 NWM `reputation_policy` 和 NDP `/.nid/reputation`，也未标注延后。但按 §8.1 分阶段表和附录 A，Merkle 树 + STH + 包含证明 + `/.nid/reputation` + `reputation_policy` 均属 Phase 2（目标 v1.0-alpha.5）。修复：§4.3 拆分为 `4.3.1 Phase 1 —— 提交与查询（当前）` 和 `4.3.2 [Phase 2] —— Merkle 完整性证明（延后）`，并添加明确的延后说明块；§4.4 顶部新增 `[Phase 2 —— 已延后]` 说明块；§7 Merkle/STH 安全段标注 `[Phase 2]`；§8.3 测试项 2、3 标注 `[Phase 2]`。CN/EN 版本同步修改。

- **M1 — `node_kind` / `node_type` 命名消歧**：`node_kind`（NDP `Announce`）与 `node_type`（NWP NWM）名字相近但语义分叉——`node_kind` 是发现层多值角色列表，`node_type` 是单运营角色——且无跨协议约束文档，构成验证歧义。修复：（1）`NPS-4-NDP.md` v0.5 → v0.6：`node_kind` 更名为 `node_roles`（仅数组；解析器 MUST 在 alpha.5 前把 `node_kind` 当别名接受）；（2）`NPS-2-NWP.md` v0.8 → v0.9：新增 §2.1 *节点角色解析* 说明两字段设计；`node_type` 描述更新——MUST 为 `node_roles` 中的值之一；拓扑成员表和 `topology.filter` 键更名为 `node_roles`；§14.7 引用更新；（3）`spec/frame-registry.yaml` AnnounceFrame 条目更新；（4）`spec/cr/NPS-CR-0001` §3.4 补入历史迁移说明。

---

## [1.0.0-alpha.4] — 2026-04-30

### 规范（Spec）

- **NPS-CR-0002 —— Implemented（pre-1.0 fast-track）**：Anchor Node 上保留查询类型 `topology.snapshot` 与 `topology.stream`，在 NPS-AaaS-Profile L2 强制要求。NWP 新增顶级 §12 同时定义两者：`topology.snapshot`（QueryFrame，`type="topology.snapshot"`）返回集群当前成员列表，附带单调 `version` 计数；`topology.stream`（SubscribeFrame，`type="topology.stream"`）以 DiffFrame 推送 `member_joined` / `member_left` / `member_updated` / `anchor_state` / `resync_required` 事件，其 `seq` 即事件后的拓扑版本。QueryFrame §6.1 与 SubscribeFrame §8.1 各新增可选顶层 `type` 字段，用于选入保留类型；DiffFrame §8.2 `event_type` 枚举在保留订阅类型下扩展到拓扑事件取值。新增 4 条错误码：`NWP-TOPOLOGY-UNAUTHORIZED`、`NWP-TOPOLOGY-UNSUPPORTED-SCOPE`、`NWP-TOPOLOGY-DEPTH-UNSUPPORTED`、`NWP-TOPOLOGY-FILTER-UNSUPPORTED`。新增 §14.7 拓扑读取安全节。原 NWP §12 错误码 / §13 安全 / §14 变更历史顺延为 §13 / §14 / §15。AaaS-Profile §4.3 新增 L2-08，强制维护成员注册表的 Anchor Node 实现两种查询类型；§2 引言段中"留给 v1.0-alpha.4"的占位措辞替换为具体 L2 强制声明。新增合规套件 `spec/services/conformance/NPS-Node-L2.md` v0.1 覆盖 L2-08，含 7 个 `TC-N2-AnchorTopo-*` / `TC-N2-AnchorStream-*` 用例；配套 `NPS-NODE-L2-CERTIFIED.md` 自声明模板。L2-01 至 L2-07 其余要求由后续 CR 补齐。

- **NPS-RFC-0002 —— Prototype 落地（状态仍为 Draft）**：X.509 + ACME NID 证书的 .NET 参考原型。`IdentFrame` 新增可选字段 `cert_format`（默认 `"v1-proprietary"` | `"v2-x509"`）与 `cert_chain`（base64url DER）—— 非破坏性；v1 verifier 忽略额外字段、继续工作（RFC §8.1 Phase 1 双信任）。`NPS.NIP.X509` 提供 `NipX509Builder`（按 RFC 8410 签发 leaf + 自签 root，critical NPS EKU、SAN URI = NID、`nid-assurance-level` 自定义扩展用 ASN.1 ENUMERATED 编码）与 `NipX509Verifier`（chain 验证 + EKU 必现 + subject/SAN 与 NID 匹配 + assurance level 绑定）。`Ed25519X509SignatureGenerator` 通过 NSec 把 Ed25519 接到 .NET `CertificateRequest`。`NipCaService.RegisterX509Async` 与既有 v1 路径并行签发 v2；`NipIdentVerifier` Step 3b 在配置 X.509 trust roots 时叠加 chain 校验。`NPS.NIP.Acme` 提供 ACME 客户端 + 进程内服务端，实现 NPS-RFC-0002 §4.4 的新 challenge 类型 `agent-01`：客户端用 NID 私钥签 challenge token，服务端用 account JWK 验证，finalize 接受 CSR 并签发 X.509 叶证书。**Pebble（RFC 8555 参考服务端）interop 推迟到后续** —— pebble 不实现非标准 `agent-01`，直接 interop 只能验证 HTTP-01 合规；`tools/pebble/setup.sh` 已经放好二进制下载脚本。新增 4 条错误码（`NIP-CERT-FORMAT-INVALID`、`NIP-CERT-EKU-MISSING`、`NIP-CERT-SUBJECT-NID-MISMATCH`、`NIP-ACME-CHALLENGE-FAILED`）写入 `spec/error-codes.md` v0.8 → v0.9。**RFC §9 实测数据已回填**：v1 IdentFrame 459 B；v2 IdentFrame 1512 B（+229%，超 §8.4 原 1200 B 上限 26% —— 上限放宽到 1600 B）；v1 验签 597.8 µs / v2 验签 1698.5 µs（2.84× v1，在 5× regression ceiling 内 —— 50 µs 绝对目标因宿主硬件依赖太强、共享租户 CI 上不可移植而删除）。RFC §10 OQ-1 由数据闭环：X.509 + ACME 一起做（prototype 7 工作日 vs 仅 X.509 ~5 天 —— 拆开会要两轮跨语言移植）。临时 OID arc `1.3.6.1.4.1.99999` 占位待 IANA PEN（OQ-2）。基准项目 `NPS.Benchmarks.NipCert` 产出 `docs/benchmarks/nip-cert-prototype.md`。RFC 仍为 Draft，待 shepherd 评审 + 跨语言 SDK 移植 + IANA PEN 后推到 Proposed/Accepted。

- **版本号变更**：
  - NPS-2 NWP：`v0.7` → `v0.8`（CR-0002 §12 保留查询类型 + 拓扑错误码 + DiffFrame `event_type` 扩展）。
  - AaaS-Profile：`v0.3` → `v0.4`（CR-0002 L2-08 + §2 占位替换；`Depends-On` NWP 升级到 v0.8）。
  - `spec/error-codes.md`：`v0.8` → `v0.9`（RFC-0002 prototype —— 4 条 NIP-CERT-* / NIP-ACME-* 新错误码）。

### .NET SDK

- **`NPS.NWP.Anchor`**：新增客户端接口，命名空间 `NPS.NWP.Anchor.Client` —— `AnchorNodeClient` 暴露 `GetSnapshotAsync(...)`（typed Query）与 `SubscribeAsync(...)`（typed `IAsyncEnumerable<TopologyEvent>`）。Record：`TopologySnapshot`、`MemberInfo`、`MemberChanges`、`TopologyFilter`。`TopologyEvent` 层次：`MemberJoined` / `MemberLeft` / `MemberUpdated` / `AnchorState` / `ResyncRequired`。`AnchorNodeMiddleware` 扩展，把 `/query` 与 `/subscribe` 路由到新 DI 接口 `IAnchorTopologyService`；既有 `/invoke` 路由不变。

### Daemons

- **`daemons/nps-registry/` —— SQLite 真实注册落地**：alpha.3 把这个 daemon 作为 phase-1 骨架发出，Announce / Resolve / Graph 端点都是返回空 payload 的 stub。alpha.4 换成 `SqliteNdpRegistry` —— 一个 SQLite 持久化的 `INdpRegistry` 实现：announcement 表带 `expires_at` 索引方便 lazy purge，`graph_meta` 单例行保存 per-cluster 单调 graph 序号（每次 Announce / eviction 自增）。每次读（`GetAll` / `Resolve`）走一次 lazy expiry，因此不需要后台定时器。`CreateInMemory()` 工厂用 `mode=memory&cache=shared` 私有连接串 + 保活连接 —— 既是 daemon 在 `NPSREGISTRY_SQLITE_PATH` 未设置时的零配置兜底，也是测试夹具入口。新增 env `NPSREGISTRY_SQLITE_PATH` 选定文件路径；`/health` 暴露新字段 `storage`（`"in-memory"` 或配置的文件路径）。新增 xUnit 测试 10 个在 `impl/dotnet/tests/NPS.Tests/Daemons/Registry/SqliteNdpRegistryTests.cs` —— 与 `InMemoryNdpRegistryTests` 保持 parity（announce / refresh / TTL 驱逐 / lazy purge / resolve / 未知 target 返 null），加上 SQLite 特异用例（graph seq 单调性、跨重开持久化）。新增包依赖：`Microsoft.Data.Sqlite 10.0.0`。csproj 版本号升 `1.0.0-alpha.4`。补齐 alpha.3 中"SQLite-backed real registration lands alpha.4"的承诺。L2 federation（跨机 gossip）仍按 `docs/daemons/architecture.md` 节奏排队到 alpha.5+。

- **`daemons/nps-ledger/` —— Phase 2：SQLite + Merkle + STH + 包含证明**：alpha.3 把这个 daemon 作为 Phase 1 内存日志发出，只承载 `ReputationLogEntry` 的形状，`/v1/log/sth` 返回占位 `{seq, timestamp, merkle_root: null}`。alpha.4 把 NPS-RFC-0004 §4.3 承诺的 Phase 2 全部端面落地。新增 `LedgerStore`（SQLite）以单调 `seq` 追加（事务级 `seq_counter` 行 + tx 内分配），盖上服务端 `timestamp`，并把 RFC 9162 §2 的 `LeafHash = SHA256(0x00 || canonical_body)` 与 body 一起持久化，免去每次 STH / 证明都重新 canonicalize 的开销。新增 `MerkleTree` 提供 RFC 9162 二叉树实现：`Mth`（Merkle Tree Hash）按"最大不超过区间长度的 2 的幂"分裂，`InclusionProof` 按 leaf-to-root 顺序返回 audit path 并在每层用标准左右兄弟判定。新增 `OperatorIdentity` 镜像 `npsd` 的 `RootIdentity` —— Ed25519 keypair 以 PKCS#8 写到 `${NPSLEDGER_DATA_DIR}/operator.ed25519.pkcs8`，POSIX 权限 `0600`，公钥编码 `ed25519:{base64url}`，`log_id` 在没有 `NPSLEDGER_LOG_ID` 覆盖时取 `urn:nps:log:operator-{8字节指纹hex}`；同一 key 用于签 STH。新增 `SignedTreeHead` / `InclusionProof` record 定义 wire JSON 形状。`Program.cs` 重写：`/v1/log/entries` 改走 `LedgerStore`；`/v1/log/sth` 返回真实签名后的 STH（operator 私钥按既有 `NipSigner` canonical-with-signature-excluded 管线签）；新增 `GET /v1/log/proof?seq=N` 返回 audit path + leaf hash + tree size 供离线包含验证；`/health` 新暴露 `phase: 2`、`storage`、`log_id`、`operator_pub_key` 字段。新增 env：`NPSLEDGER_DATA_DIR`、`NPSLEDGER_SQLITE_PATH`、`NPSLEDGER_LOG_ID`。跨 log replay 防御：`log_id` 也在签名覆盖范围内，所以 log "A" 的 STH 不会被当作 log "B" 通过验证，即使签名 key 一样。新增 xUnit 测试 20 个在 `impl/dotnet/tests/NPS.Tests/Daemons/Ledger/`：7 个 `MerkleTreeTests`（RFC 9162 已知向量：空 / 1 / 2 / 4 / 5 叶；n=7 时按 RFC 9162 §2.1.3.2 算法验证 inclusion proof verifies-at-every-index；单叶 path 为空；越界抛错）、9 个 `LedgerStoreTests`（seq 从 1 起单调 / server 改写 timestamp / count 跟踪 / Query subject + since 过滤 / leaf hash 与 `SHA256(0x00 || canonical_body)` 一致 / `GetLeafHashAndIndex` 返回正确 0-based index / 未知 seq 返 null / 文件持久化跨重开保留计数并继续编号）、4 个 `SignedTreeHeadTests`（签名 roundtrip 通过；篡改 `sha256_root_hash` / `tree_size` / `log_id` 各自破坏验签）。测试总数：609 → 629，全绿。新包依赖：`Microsoft.Data.Sqlite 10.0.0`。csproj 版本号升 `1.0.0-alpha.4`。补齐 alpha.3 中"persistence + Merkle tree + STH + inclusion proofs land in alpha.4"的承诺。STH 跨 operator gossip（NPS-RFC-0004 §6 federation）以及完整 issuer 签名验证（需要 nps-registry 解 NID）继续排队到 alpha.5+。

- **`daemons/npsd/`**：新增 `TopologyRegistry` 服务摄入 NDP `Announce` 帧（`cluster_anchor` = npsd 自身 NID），维护以 NID 为键的内存成员表、单调的 per-Anchor `version` 计数器，以及可配的环形事件缓冲（默认保留 256 个事件）供 `topology.stream` 重播。实现 `IAnchorTopologyService`，让标准 `AnchorNodeMiddleware` 的 `/query` / `/subscribe` 路由能直接暴露拓扑操作而无需逐部署粘合代码。重启策略：把 `version` rebase 到当前快照成员数，并向活跃订阅推送 `anchor_state.version_rebased`。

### Java SDK

- **NPS-RFC-0001 Phase 2 —— Java preamble helper**：新建 final class `com.labacacia.nps.ncp.NcpPreamble`，暴露规范常量（`LITERAL`、`LENGTH`、`READ_TIMEOUT_MS`、`CLOSE_DEADLINE_MS`）、`getBytes()`（防御性拷贝）、`matches`、`tryValidate(byte[], String[] reason)`、`validate`（不通过时抛 `NcpPreambleInvalidException`，携带 `errorCode` + `statusCode`）以及 `write(OutputStream)`。reason 字符串与 .NET 参考完全一致，方便跨 SDK 遥测对齐；遇到 future-major（`NPS/2.x`）preamble 与任意垃圾区分提示。新增测试类 `NcpPreambleTest` —— 14 个用例全绿。无新增依赖。
- **NPS-RFC-0002 —— Java 移植（跨 SDK 端口波首棒）**：`impl/java/` 与 .NET 参考原型对齐。新建包 `com.labacacia.nps.nip.x509`：`NipX509Builder`（用 BouncyCastle `X509v3CertificateBuilder` 签发 leaf + 自签 root，critical NPS EKU、SAN URI = NID、`nid-assurance-level` 自定义扩展用 ASN.1 ENUMERATED 编码，临时 OID `1.3.6.1.4.1.99999.2.1`）、`NipX509Verifier`（chain 验证 + EKU 必现且 critical 检查 + subject CN/SAN URI 匹配 NID + assurance level 绑定 + chain 必须锚到可信 root）、`Ed25519PublicKeys` helper（raw 32-byte ↔ JCA `PublicKey` ↔ BC `SubjectPublicKeyInfo` 互转）、`NpsX509Oids` 常量。证书签名复用原生 JCA Ed25519，通过 `JcaContentSignerBuilder("Ed25519")` —— 不切 provider。新建包 `com.labacacia.nps.nip.acme`：`AcmeMessages`（DTO records：Directory / Account / Order / Identifier / Authorization / Challenge / FinalizePayload / ProblemDetail，带 Jackson 注解）、`AcmeJws`（RFC 7515 flattened JWS 签名，`alg: "EdDSA"` 来自 RFC 8037 + RFC 7638 thumbprint）、`AcmeClient`（基于 Java 11+ `HttpClient`，跑通 newNonce → newAccount → newOrder → POST-as-GET authz → respond `agent-01` → finalize 带 PKCS#10 CSR → 拉取 PEM 链）、`AcmeServer`（进程内 JDK `com.sun.net.httpserver.HttpServer`，供测试用，状态机完整：nonces / account JWKs / orders / authorizations / challenges / 已签发证书）。`IdentFrame` 加入可选 `cert_format` + `cert_chain` 字段（非破坏性 —— 既有 4 参构造函数保留）。新增 `NipIdentVerifier` + `NipVerifierOptions` 实现 RFC §8.1 Phase 1 双信任：v1 Ed25519 sig 检查（始终）+ Step 2 最小 assurance level（配置时）+ Step 3b X.509 chain 校验（仅当 `trustedX509Roots` 非空且 `cert_format == "v2-x509"` 时启用）。新增 `AssuranceLevel` 枚举（RFC-0003 —— 补齐 Java SDK 之前 deferred 的 parse-only 路径）与 `NipErrorCodes` 常量类（含全部现有 NIP 错误码及 4 条 RFC-0002 新增码）。新增 7 个测试在 `src/test/java/com/labacacia/nps/nip/`：`NipX509Tests`（5 用例 —— 完整 happy path、EKU 被剥离、subject NID 不一致、v1-only verifier 通过忽略 chain 接受 v2 frame、v2 verifier 在 trust roots 不匹配时拒绝）、`AcmeAgent01Tests`（2 用例 —— ACME agent-01 完整 round-trip + 发出的 PEM 链通过 `NipX509Verifier` 校验、被篡改的 agent_signature 触发 `NIP-ACME-CHALLENGE-FAILED`）。Java SDK 测试总数：91 → 98，全绿。新依赖：`org.bouncycastle:bcprov-jdk18on:1.79` + `bcpkix-jdk18on:1.79`（仅用 X.509 builder API —— JDK 不开放公开的证书构建接口）。Build version 升 `1.0.0-alpha.4`。`IdentFrame.unsignedDict()` 故意不包含 `cert_format` / `cert_chain` 在 v1 签名 payload 中（CA Ed25519 签名仍然只覆盖 `nid` / `pub_key` / `metadata` / `assurance_level`）。

### Python SDK

- **NPS-RFC-0001 Phase 2 —— Python preamble helper**：新建模块 `nps_sdk.ncp.preamble`，暴露规范常量（`LITERAL`、`LENGTH`、`BYTES`、`READ_TIMEOUT`、`CLOSE_DEADLINE`、`ERROR_CODE`、`STATUS_CODE`）、`matches`、`try_validate(buf) -> (ok, reason)`、`validate`（不通过时抛 `NcpPreambleInvalidError`，携带 `error_code` + `status_code`）以及同步 / 异步 `write` / `write_async` 帮助函数。reason 字符串与 .NET 参考完全一致，方便跨 SDK 遥测对齐。从 `nps_sdk.ncp` 重新导出，并连同 `NcpPreambleInvalidError` 一起暴露。新增测试文件 `tests/test_ncp_preamble.py` —— 13 个用例全绿。无新增依赖。
- **NPS-RFC-0002 —— Python 移植（跨 SDK 端口波第二棒）**：`impl/python/` 与 .NET / Java 参考对齐。新建模块 `nps_sdk.nip.x509`：`NipX509Builder`（用 `cryptography.x509.CertificateBuilder` + 原生 `pyca/cryptography` Ed25519，按 RFC 8410 用 `algorithm=None` 签名）、`NipX509Verifier`（chain 解码 + EKU 必现且 critical 检查 + subject CN / SAN URI 匹配 + assurance level 绑定 + chain 必须锚到可信 root）、`NpsX509Oids` 常量。新建模块 `nps_sdk.nip.acme`：`messages`（dataclass DTO 带 `to_dict()`/`from_dict()` —— Directory / Account / Order / Identifier / Authorization / Challenge / FinalizePayload / ProblemDetail / ChallengeRespondPayload）、`jws`（RFC 7515 flattened JWS，`alg: "EdDSA"` 来自 RFC 8037 + RFC 7638 thumbprint）、`AcmeClient`（基于 httpx `AsyncClient`，跑通 newNonce → newAccount → newOrder → POST-as-GET authz → respond `agent-01` → finalize 带 PKCS#10 CSR → 拉取 PEM 链）、`AcmeServer`（stdlib `http.server.ThreadingHTTPServer` 在 daemon thread 中运行 —— 状态机完整：nonces / account JWKs / orders / authorizations / challenges / 已签发证书）。`IdentFrame` 加入可选 `assurance_level`（RFC-0003）+ `cert_format` + `cert_chain` 字段（非破坏性）；`unsigned_dict()` 故意把 `cert_format` / `cert_chain` 排除在 v1 签名 payload 之外（CA Ed25519 签名仍然只覆盖既有的 `nid` / `pub_key` / `capabilities` / `scope` / `issued_by` / `issued_at` / `expires_at` / `serial` / `metadata` / `assurance_level`）。新增 `NipIdentVerifier` + `NipVerifierOptions` 实现 RFC §8.1 Phase 1 双信任：v1 Ed25519 sig 检查（始终）+ Step 2 最小 assurance level（配置时）+ Step 3b X.509 chain 校验（仅当 `trusted_x509_roots` 非空且 `cert_format == "v2-x509"` 时启用）。新增 `AssuranceLevel` 枚举（RFC-0003 —— 补齐 Python SDK 之前 deferred 的 parse-only 路径）与 `error_codes` 模块（含全部 NIP 错误码及 4 条 RFC-0002 新码）。新增 7 个测试在 `tests/`：`test_nip_x509.py`（5 用例 —— happy path、EKU 被剥离、subject NID 不一致、v1-only verifier 通过忽略 chain 接受 v2、v2 verifier 在 trust roots 不匹配时拒绝）、`test_nip_acme_agent01.py`（2 用例 —— ACME agent-01 完整 round-trip + 发出的 PEM 链通过 `NipX509Verifier` 校验、被篡改的 agent_signature 触发 `NIP-ACME-CHALLENGE-FAILED`）。Python SDK 测试总数：191 → 198，全绿。**无新增包依赖** —— `cryptography` / `httpx` / `msgpack` 已在 `pyproject.toml`；进程内 ACME server 用 stdlib `http.server.ThreadingHTTPServer`，未引入异步服务器框架。Build version 升 `1.0.0-alpha.4`。

### TypeScript SDK

- **NPS-RFC-0001 Phase 2 —— TypeScript preamble helper**：新建模块 `src/ncp/preamble.ts`，暴露规范常量（`PREAMBLE_LITERAL`、`PREAMBLE_LENGTH`、`PREAMBLE_BYTES`、`PREAMBLE_READ_TIMEOUT_MS`、`PREAMBLE_CLOSE_DEADLINE_MS`、`PREAMBLE_ERROR_CODE`、`PREAMBLE_STATUS_CODE`）、`preambleMatches`、`tryValidatePreamble`、`validatePreamble`（不通过时抛 `NcpPreambleInvalidError`，携带 `errorCode` + `statusCode`）以及 `writePreamble`。reason 字符串与 .NET 参考完全一致，方便跨 SDK 遥测对齐。从 `src/ncp/index.ts` 重新导出；`NCP_PREAMBLE_INVALID` 加入 `NCP_ERROR_CODES`。新增测试文件 `tests/ncp/preamble.test.ts` —— 13 个用例全绿。无新增依赖。
- **NPS-RFC-0002 —— TypeScript 移植（跨 SDK 端口波第三棒）**：`impl/typescript/` 与 .NET / Java / Python 参考对齐。新建模块 `nip/x509`：`issueLeaf` / `issueRoot`（基于 `@peculiar/x509` `X509CertificateGenerator` + 原生 Web Crypto Ed25519 —— Node 18+ 的 `globalThis.crypto.subtle` 已原生支持 `{name: "Ed25519"}`）、`verify`（chain 解码 + EKU 必现且 critical 检查 + subject CN / SAN URI 匹配 + assurance-level 绑定（自定义 ASN.1 ENUMERATED `nid-assurance-level` 扩展，临时 OID `1.3.6.1.4.1.99999.2.1`）+ chain 必须锚到可信 root）、`oids.ts` 常量。新建模块 `nip/acme`：`messages.ts`（plain interface DTO —— Directory / Account / Order / Identifier / Authorization / Challenge / FinalizePayload / ProblemDetail / ChallengeRespondPayload / NewAccountPayload / NewOrderPayload）、`wire.ts` 常量（`agent-01`、`application/jose+json`、RFC 8555 status 枚举）、`jws.ts`（RFC 7515 flattened JWS，`alg: "EdDSA"` 来自 RFC 8037 + RFC 7638 thumbprint，基于 `@noble/ed25519` + `@noble/hashes/sha256`）、`client.ts`（基于 Node 内建 `fetch`，跑通 newNonce → newAccount → newOrder → POST-as-GET authz → respond `agent-01` → finalize 带 PKCS#10 CSR（`@peculiar/x509` `Pkcs10CertificateRequestGenerator`）→ 拉取 PEM 链）、`server.ts`（进程内 `node:http.createServer`，供测试用 —— 状态机完整：nonces / account JWKs / orders / authorizations / challenges / 已签发证书）。`IdentFrame` 加入可选 `assuranceLevel`（RFC-0003）+ `certFormat` + `certChain` 构造选项（非破坏性 —— 既有 4 参构造函数保留）；`unsignedDict()` 故意把 `cert_format` / `cert_chain` 排除在 v1 签名 payload 之外（CA Ed25519 签名仍然只覆盖 `nid` / `pub_key` / `metadata` / `assurance_level`）。新增 `NipIdentVerifier` + `NipVerifierOptions` 实现 RFC §8.1 Phase 1 双信任：v1 Ed25519 sig 检查（始终）+ Step 2 最小 assurance level（配置时）+ Step 3b X.509 chain 校验（仅当 `trustedX509Roots` 非空且 `certFormat === "v2-x509"` 时启用）。新增 `AssuranceLevel` 类（RFC-0003 —— 补齐 TypeScript SDK 之前 deferred 的 parse-only 路径）与 `error-codes.ts` 模块（含全部 NIP 错误码及 4 条 RFC-0002 新码）。`src/nip/index.ts` 现重新导出 `assurance-level` / `cert-format` / `error-codes` / `verifier`，并以 namespace 形式重新导出 `x509` / `acme`，让 `@labacacia/nps-sdk` 消费方看到完整接口。新增 7 个测试在 `tests/`：`nip-x509.test.ts`（5 用例 —— happy path、EKU 被剥离、subject NID 不一致、v1-only verifier 通过忽略 chain 接受 v2、v2 verifier 在 trust roots 不匹配时拒绝）、`nip-acme-agent01.test.ts`（2 用例 —— ACME agent-01 完整 round-trip + 发出的 PEM 链通过 X.509 verifier 校验、被篡改的 agent_signature 触发 `NIP-ACME-CHALLENGE-FAILED`）。共享 `tests/_rfc0002-keys.ts` 把 noble 的 32 字节裸 Ed25519 私/公钥按 RFC 8410 固定 PKCS8/SPKI 前缀拼出 DER，再 importKey 进 Web Crypto 形成 `CryptoKeyPair`，让 SDK 两半在同一用例内共用密钥。TypeScript SDK 测试总数：264 → 271，全绿。新依赖：`@peculiar/x509@^1.12.0`（X.509 builder API —— Node Web Crypto 能签/验 Ed25519，但不能构建证书）。包版本号升 `1.0.0-alpha.4`。

### Go SDK

- **NPS-RFC-0001 Phase 2 —— Go preamble helper**：新建文件 `ncp/preamble.go`，暴露规范常量（`PreambleLiteral`、`PreambleLength`、`PreambleReadTimeoutSecs`、`PreambleCloseDeadlineMs`、`PreambleErrorCode`、`PreambleStatusCode`）、`PreambleMatches`、`ValidatePreamble`（不通过时返回 `*ErrPreambleInvalid`，携带结构化 `Reason`）以及 `WritePreamble(io.Writer)`。reason 字符串与 .NET 参考完全一致，方便跨 SDK 遥测对齐。新增测试文件 `ncp/preamble_test.go` —— 10 个用例全绿。无新增 module 依赖。
- **NPS-RFC-0002 —— Go 移植（跨 SDK 端口波第四棒）**：`impl/go/` 与 .NET / Java / Python / TypeScript 参考对齐。新建包 `nip/x509`：`IssueLeaf` / `IssueRoot`（基于 stdlib `crypto/x509.CreateCertificate` + 原生 Ed25519 —— Go 的 stdlib 自 1.13 起就直接支持 Ed25519 X.509 签发，零第三方依赖）、`Verify`（chain 解码 + EKU 必现且 critical 检查 + subject CN / SAN URI 匹配 + assurance-level 绑定（自定义 ASN.1 ENUMERATED `nid-assurance-level` 扩展，临时 OID `1.3.6.1.4.1.99999.2.1`）+ chain 必须锚到可信 root，通过 stdlib `Certificate.CheckSignatureFrom`）、`oids.go` 常量。新建包 `nip/acme`：`messages.go`（带 `json` tag 的 struct DTO —— Directory / Account / Order / Identifier / Authorization / Challenge / FinalizePayload / ProblemDetail / ChallengeRespondPayload）、`wire.go` 常量（`agent-01`、`application/jose+json`、RFC 8555 status 枚举）、`jws.go`（RFC 7515 flattened JWS，`alg: "EdDSA"` 来自 RFC 8037 + RFC 7638 thumbprint，全部基于 stdlib `crypto/ed25519`）、`client.go`（基于 stdlib `net/http`，跑通 newNonce → newAccount → newOrder → POST-as-GET authz → respond `agent-01` → finalize 带 PKCS#10 CSR → 拉取 PEM 链）、`server.go`（进程内 `net/http.Server`，供测试用 —— 状态机完整：nonces / account JWKs / orders / authorizations / challenges / 已签发证书）。`IdentFrame` 加入可选 `AssuranceLevel`（RFC-0003）+ `CertFormat` + `CertChain` 字段（非破坏性）；`UnsignedDict()` 故意把 `cert_format` / `cert_chain` 排除在 v1 签名 payload 之外。新增 `NipIdentVerifier` + `VerifierOptions` 实现 RFC §8.1 Phase 1 双信任：v1 Ed25519 sig 检查（始终）+ Step 2 最小 assurance level（配置时）+ Step 3b X.509 chain 校验（仅当 `TrustedX509Roots` 非空且 `CertFormat == "v2-x509"` 时启用）。为避免 `nip → nip/x509 → nip` 循环导入，`NipIdentVerifier` 接受一个注入的 `X509ChainVerifier` 函数 adapter —— 调用方把它指向 `nip/x509.Verify`（测试中只需 3 行）。新增 `AssuranceLevel` 结构（含 `MeetsOrExceeds`、`AssuranceFromWire`、`AssuranceFromRank`）与 `error_codes.go` 模块（含全部 NIP 错误码及 4 条 RFC-0002 新码）。新增 7 个测试在 `nip/`：`nip_x509_test.go`（5 用例 —— happy path、EKU 被剥离、subject NID 不一致、v1-only verifier 通过忽略 chain 接受 v2、v2 verifier 在 trust roots 不匹配时拒绝）、`nip_acme_agent01_test.go`（2 用例 —— ACME agent-01 完整 round-trip + 发出的 PEM 链通过 X.509 verifier 校验、被篡改的 agent_signature 触发 `NIP-ACME-CHALLENGE-FAILED`）。Go SDK 测试总数：79 → 86，全绿。**无新增 module 依赖** —— Go stdlib 的 `crypto/x509` 端到端覆盖证书签发与验证（对比 TS 需要 `@peculiar/x509`、Java 需要 BouncyCastle）。

### Rust SDK

- **NPS-RFC-0001 Phase 2 —— Rust preamble helper**：新建模块 `nps_ncp::preamble`，暴露规范常量（`LITERAL`、`BYTES`、`LENGTH`、`READ_TIMEOUT_SECS`、`CLOSE_DEADLINE_MS`、`ERROR_CODE`、`STATUS_CODE`）、`matches(&[u8])`、`validate(&[u8]) -> NpsResult<()>`（不通过时返回 `NpsError::Frame(reason)`）以及 `write(&mut impl Write)`。reason 字符串与 .NET 参考完全一致，方便跨 SDK 遥测对齐。新增 integration 测试文件 `nps-ncp/tests/preamble_tests.rs` —— 10 个用例全绿。无新增 crate 依赖。
- **NPS-RFC-0002 —— Rust 移植（跨 SDK 端口波第五棒，收官）**：`impl/rust/nps-nip/` 与 .NET / Java / Python / TypeScript / Go 参考对齐。新建模块 `nps_nip::x509`（含 `oids`、`builder`、`verifier`）：`issue_leaf` / `issue_root` 基于 `rcgen 0.13`，启用 `x509-parser` feature 让 `SubjectPublicKeyInfo::from_der` 可用 —— 这是让我们能用 *外部* Ed25519 公钥（agent 的）签发证书的关键，rcgen 的标准 API 要求拿到 subject 的私钥，这条路绕不过去。`verify` 用 `x509-parser` 解析链，再通过 `ed25519-dalek` 直接验签：chain 解码 + EKU 必现且 critical + subject CN / SAN URI 匹配 + assurance-level 绑定（自定义 ASN.1 ENUMERATED `nid-assurance-level` 扩展，临时 OID `1.3.6.1.4.1.99999.2.1`）+ chain 必须锚到可信 root。critical EKU 用手写的 `CustomExtension` 而不是 rcgen 的 `extended_key_usages`，因为 rcgen 这个字段不暴露 `Other(...)` OID 的 critical 位；`oids.rs` 自带一个小的 DER OID 编码器（无新增依赖）。新建模块 `nps_nip::acme`（含 `wire`、`messages`、`jws`、`client`、`server`）：`jws.rs`（RFC 7515 flattened JWS，`alg: "EdDSA"` 来自 RFC 8037 + RFC 7638 thumbprint，基于 `ed25519-dalek` + `sha2`）、`client.rs`（基于 `reqwest::Client` 异步，跑通 newNonce → newAccount → newOrder → POST-as-GET authz → respond `agent-01` → finalize 带 PKCS#10 CSR（用 `rcgen::CertificateParams::serialize_request` 生成）→ 拉取 PEM 链）、`server.rs`（在后台线程跑 `tiny_http::Server`，供测试用 —— 状态机完整：nonces / account JWKs / orders / authorizations / challenges / 已签发证书）。`IdentFrame` 加入可选 `assurance_level`（RFC-0003）+ `cert_format` + `cert_chain` 字段（非破坏性 —— 既有测试中的 struct literal 已显式补上新字段）；`unsigned_dict()` 故意把 `cert_format` / `cert_chain` 排除在 v1 签名 payload 之外。新增 `NipIdentVerifier` + `NipVerifierOptions` 实现 RFC §8.1 Phase 1 双信任：v1 Ed25519 sig 检查（始终）+ Step 2 最小 assurance level（配置时）+ Step 3b X.509 chain 校验（仅当 `trusted_x509_roots_der` 非空且 `cert_format == "v2-x509"` 时启用）。两个模块同 crate，所以 Rust 端不需要 Go 端用的"函数 adapter 打破循环"的写法。新增 `AssuranceLevel` 结构 + `error_codes` 模块（含全部 NIP 错误码及 4 条 RFC-0002 新码）。新增 7 个测试在 `nps-nip/tests/`：`nip_x509_tests.rs`（5 用例 —— happy path、EKU 被剥离、subject NID 不一致、v1-only verifier 通过忽略 chain 接受 v2、v2 verifier 在 trust roots 不匹配时拒绝）、`nip_acme_agent01_tests.rs`（2 用例 —— ACME agent-01 完整 round-trip + 发出的 PEM 链通过 X.509 verifier 校验、被篡改的 agent_signature 触发 `NIP-ACME-CHALLENGE-FAILED`）。Rust SDK 测试总数：92 → 99，全绿。新增 crate 依赖：`rcgen 0.13`（X.509 builder，feature `pem`+`crypto`+`ring`+`x509-parser`）、`x509-parser 0.16`（cert 解析 + chain 验证）、`tiny_http 0.12`（测试用进程内 HTTP server）、`time 0.3`（rcgen 的日期类型）。**附带修了一个预存的版本 pin bug**：`Cargo.toml` 的 `nps-* = "=1.0.0-alpha.2"` 与各 crate 声明的 `1.0.0-alpha.3` 不一致（dev 上 workspace 之前根本没法 build）；现已统一升至 `1.0.0-alpha.4`。

### Tools

- **`tools/nip-ca-server/example/python/` —— RFC-0002 同步（从 alpha.2 解冻）**：新增端点 `POST /v2/agents/register` 与 `POST /v2/nodes/register`，签发同时含 v1 Ed25519 签名 AND 2 段 X.509 链（leaf + 自签 root）的双信任 v2 IdentFrame。自签 X.509 root 在首次启动时生成于 `${NIP_CA_ROOT_CERT_FILE:/data/ca.root.der}`（5 年有效期），之后复用。`ca.issue_cert_x509(...)` 是新函数，把 `cryptography.x509.CertificateBuilder` 配 NPS EKU + assurance-level 扩展，并把 `cert_format: "v2-x509"` + `cert_chain` 附到现有 v1 frame；X.509 签发代码内联到 `ca.py`（不依赖 SDK），保持参考端口自包含（与 Java 端口的 composite-build trade-off 相对：Java *依赖* SDK，Python *不依赖*，让 Docker context 更简洁）。`/.well-known/nps-ca` 公布 `cert_formats: ["v1-proprietary", "v2-x509"]` 与新增 `register_v2` 端点 URL，schema 版本号升 `nps_ca: "0.2"`。FastAPI 上的 ACME server 端点暂缓（与 .NET 跨语言对齐 —— `tools/nip-ca-server/` 的 C# 参考目前也只暴露 v1 端点；SDK 内的 `AcmeServer` 仍是 ACME 的 canonical 参考实现）。版本号升 `1.0.0-alpha.4`。

- **`tools/nip-ca-server/example/rust/` —— RFC-0002 同步（从 alpha.2 解冻）**：新增端点 `POST /v2/agents/register` 与 `POST /v2/nodes/register`，签发同时含 v1 Ed25519 签名 AND 2 段 X.509 链（leaf + 自签 root）的双信任 v2 IdentFrame。自签 X.509 root 在每次启动时基于持久化的 CA 私钥重新派生，写到 `${NIP_CA_ROOT_CERT_FILE:/data/ca.root.der}`（5 年有效期）—— 注意：Rust 参考端口不从存盘 DER 反序列化，而是每次启动都用同一把 CA 私钥重新自签，因为 `rcgen::Certificate` 不能从原始 DER 重建；客户端应把 root 视为可重新派生的工件。`ca::issue_cert_x509(...)` 包装 `rcgen::CertificateParams::signed_by` 配 NPS EKU + assurance-level 扩展，并把 `cert_format: "v2-x509"` + `cert_chain` 附到现有 v1 frame；X.509 签发代码内联到 `ca.rs`（不依赖 SDK），保持参考端口自包含（与 Python / Go / TS 取舍一致 —— 只有 Java 端口把 SDK 当 runtime dep）。`/.well-known/nps-ca` 公布 `cert_formats: ["v1-proprietary", "v2-x509"]` 与新增 `register_v2` 端点 URL，schema 版本号升 `nps_ca: "0.2"`。axum 上的 ACME server 端点暂缓（与 .NET 跨语言对齐；SDK 内的 `nps_nip::acme::AcmeServer` 仍是 ACME 的 canonical 参考，可单独嵌入生产部署）。新增 crate 依赖：`rcgen 0.13`、`time 0.3`。版本号升 `1.0.0-alpha.4`。

- **`tools/nip-ca-server/example/java/` —— RFC-0002 同步（从 alpha.2 解冻）**：Java 参考 CA Server 现在通过 Gradle composite build（`includeBuild('../../../../impl/java')`）依赖 SDK；BouncyCastle 由 SDK 传递引入。新增端点 `POST /v2/agents/register` 与 `POST /v2/nodes/register`，签发同时含 v1 Ed25519 签名 AND 2 段 X.509 链（leaf + 自签 root，由同一 CA Ed25519 密钥签）的双信任 v2 IdentFrame。自签 X.509 root 在首次启动时生成于 `${nip.ca.root-cert-file:/data/ca.root.der}`（5 年有效期），之后复用。`CaService.issueCertX509(...)` 是新方法，包装 `NipX509Builder.issueLeaf` 并把 `cert_format: "v2-x509"` + `cert_chain` 附到现有 v1 frame 上。`/.well-known/nps-ca` 现在公布 `cert_formats: ["v1-proprietary", "v2-x509"]` 与新增的 `register_v2` 端点 URL。Spring app 上的 ACME server 端点暂缓（与 .NET 跨语言对齐 —— `tools/nip-ca-server/` 的 C# 参考目前也只暴露 v1 端点；SDK 内的 `AcmeServer` 仍是 ACME 的 canonical 参考实现）。版本号升 `1.0.0-alpha.4`。`CaController.java` 末尾的死代码 `class Duration` shim 已移除（之前掩盖了 `java.time.Duration`，CaService 引入 java.time.Duration 后必须清理）。

- **`tools/nip-ca-server/example/ts/` —— RFC-0002 同步（从 alpha.2 解冻）**：新增端点 `POST /v2/agents/register` 与 `POST /v2/nodes/register`，签发同时含 v1 Ed25519 签名 AND 2 段 X.509 链（leaf + 自签 root）的双信任 v2 IdentFrame。自签 X.509 root 在首次启动时生成于 `${NIP_CA_ROOT_CERT_FILE:/data/ca.root.der}`（5 年有效期），之后复用。`ca.issueCertX509(...)` 是新函数 —— 把 `node:crypto` 的 `KeyObject` 通过 RFC 8410 固定 PKCS8/SPKI Ed25519 前缀桥接到 `@peculiar/x509` 的 Web Crypto API，包装 `X509CertificateGenerator` 配 NPS EKU + ASN.1 ENUMERATED `nid-assurance-level` 扩展，并把 `cert_format: "v2-x509"` + `cert_chain` 附到现有 v1 frame 上。X.509 签发代码内联到 `ca.ts`（不依赖 SDK），保持参考端口自包含（与 Python 端口的取舍一致 —— Java *依赖* SDK 通过 Gradle composite build；Python 与 TypeScript *不依赖*，让 Docker context 更简洁）。`/.well-known/nps-ca` 公布 `cert_formats: ["v1-proprietary", "v2-x509"]` 与新增 `register_v2` 端点 URL，schema 版本号升 `nps_ca: "0.2"`。Fastify 上的 ACME server 端点暂缓（与 .NET 跨语言对齐 —— `tools/nip-ca-server/` 的 C# 参考目前也只暴露 v1 端点；SDK 内的 `AcmeServer` 仍是 ACME 的 canonical 参考实现）。新依赖：`@peculiar/x509@^1.12.0`。版本号升 `1.0.0-alpha.4`。

- **`tools/nip-ca-server/example/go/` —— RFC-0002 同步（从 alpha.2 解冻）**：新增端点 `POST /v2/agents/register` 与 `POST /v2/nodes/register`，签发同时含 v1 Ed25519 签名 AND 2 段 X.509 链（leaf + 自签 root）的双信任 v2 IdentFrame。自签 X.509 root 在首次启动时生成于 `${NIP_CA_ROOT_CERT_FILE:/data/ca.root.der}`（5 年有效期），之后复用。`ca.IssueCertX509(...)` 是新函数，包装 stdlib `crypto/x509.CreateCertificate` 配 NPS EKU + assurance-level 扩展，并把 `cert_format: "v2-x509"` + `cert_chain` 附到现有 v1 frame 上；X.509 签发代码内联到 `ca/ca.go`（不依赖 SDK，Go 参考端口一直是自包含的，且 Go stdlib 已覆盖 Ed25519 X.509 —— 无需新增 module 依赖）。`/.well-known/nps-ca` 公布 `cert_formats: ["v1-proprietary", "v2-x509"]` 与新增 `register_v2` 端点 URL，schema 版本号升 `nps_ca: "0.2"`。`net/http` mux 上的 ACME server 端点暂缓（与 .NET 跨语言对齐；SDK 内的 `nip/acme.Server` 仍是 ACME 的 canonical 参考实现，可单独嵌入生产部署）。无新增 module 依赖。

---

## [1.0.0-alpha.3] — 未发布（dev）

### 规范（Spec）

- **新增 — `spec/services/NPS-Node-Profile.md` v0.1 Draft**：节点侧合规规范（L1 Basic / L2 Interactive / L3 Autonomous），与服务侧 AaaS Profile 正交。引入 `activation_mode` 模型（`ephemeral` / `resident` / `hybrid`）作为三级的承载主轴。L1 附带 21 条详细 req ID（`N1-NCP-*` 到 `N1-OBS-*`）；L2/L3 仅列 headline additions，详细 ID 在 NPS-Roadmap Phase 2/3 跟踪。
- **新增 — `spec/services/conformance/NPS-Node-L1.md` v0.1**：语言无关的 L1 合规测试套件 —— 21 个测试用例（`TC-N1-*`）、paired-peer 方法论、结果 manifest schema。参考套件位置：`impl/dotnet/tests/NPS.Daemon.Conformance.Tests/`。历史勘误：alpha.19 审计确认实际有 20 个 `TC-N1-*` 标题；原始“21 个”正文计数属于编辑错误。
- **新增 — `spec/services/conformance/NPS-NODE-L1-CERTIFIED.md`**：实现方 L1 合规自声明模板。包含实现身份、所用 peer、运行环境、逐用例结果 checklist、结果 manifest、Ed25519 声明块。
- **NPS-RFC-0001 —— Accepted（1.0 之前快速通道）**：原生模式 NCP 连接现在 MUST 以 8 字节 ASCII 常量前导 `b"NPS/1.0\n"` 起头。新增 `spec/NPS-1-NCP.md` §2.6.1。HTTP 模式不受影响。前导不匹配时服务端 500 ms 内静默关闭（不发 `ErrorFrame`，避免向扫描器泄露帧细节）。短读 10 秒超时。新增错误码 `NCP-PREAMBLE-INVALID` 与状态码 `NPS-PROTO-PREAMBLE-INVALID`（同时引入新的 `PROTO` 状态分类，覆盖既有的 `NPS-PROTO-VERSION-INCOMPATIBLE`）。在 `frame-registry.yaml` 新增 `reservations:` 段，把帧类型字节 `0x4E`（ASCII `N`）保留 —— MUST NOT 分配给任何 NCP 帧。
- **NPS-RFC-0003 —— Accepted（1.0 之前快速通道）**：Agent 身份三级保证等级（`anonymous` / `attested` / `verified`），用于反爬与信任分流。新增 NPS-3 §5.1.1；`IdentFrame` 增加可选 `assurance_level` 字段（默认 `anonymous`，向后兼容）。NWM 增加可选顶层 `min_assurance_level` 与 ActionSpec 上的 per-action 覆盖。新增错误码 `NIP-ASSURANCE-MISMATCH`、`NIP-ASSURANCE-UNKNOWN`、`NWP-AUTH-ASSURANCE-TOO-LOW`。X.509 critical extension 翻转（§4.2）需等 NPS-RFC-0002 在 alpha.4 落地后再启用 —— 这里 Phase 1 仅 parse；现有 verifier 默认行为不变。
- **NPS-CR-0001 —— Implemented（1.0 之前快速通道）**：把原 `Gateway Node`（NWP 单一节点类型）拆为 **Anchor Node**（集群控制平面 + NOP 路由 —— 继承既有角色，重命名）与 **Bridge Node**（NPS↔非-NPS 协议翻译 —— 全新，如 HTTP/gRPC/MCP/A2A 目标）。Wire 值 `node_type: "gateway"` 移除，解析器 MUST 拒绝。NWP §2.1 重写并新增 Anchor + Bridge 完整子节；AaaS-Profile §2 把 `Gateway Node` 重命名为 `Anchor Node`，新增 §2A `Bridge Node`；NPS-Node Profile §8 引用同步。NDP `Announce` 新增三个 additive 字段以支持多角色声明：`node_kind`（string 或数组 —— 取值 `memory` / `action` / `complex` / `anchor` / `bridge`）、`cluster_anchor`（NID —— 给非-Anchor 集群成员）、`bridge_protocols`（数组 —— 给 `bridge` 节点；标准值 `http` / `grpc` / `mcp` / `a2a`）。为避免与新节点类型 **Bridge Node** 命名冲突，历史上承载相反方向（外部协议 → NPS）的 `compat/{mcp,a2a,grpc}-bridge` 包重命名为 `compat/{mcp,a2a,grpc}-ingress`；同名 sibling GitHub/Gitee 仓库 `NPS-{mcp,a2a,grpc}-bridge` 在 alpha.3 release sync 阶段统一改名为 `*-ingress`（步骤见 `tools/cr-0001-rename-instructions.md` —— 平台侧改名 MUST 由人工在 GitHub/Gitee 上执行）。
- **NPS-RFC-0004 —— Accepted（1.0 之前快速通道）**：Certificate-Transparency 风格的 NID 行为事件 append-only 日志。NPS-3 §5.1.2 定义条目 wire 格式 —— 12 字段签名 JSON、双签名（issuer + 日志运营方）覆盖 RFC 8785（JCS）规范化形式 —— 加上 8 项 `incident` 枚举（`cert-revoked` / `rate-limit-violation` / `tos-violation` / `scraping-pattern` / `payment-default` / `contract-dispute` / `impersonation-claim` / `positive-attestation`）和 5 级 `severity` 枚举（`info` / `minor` / `moderate` / `major` / `critical`）。新增三个错误码：`NIP-REPUTATION-ENTRY-INVALID`（`NPS-CLIENT-BAD-FRAME`）、`NIP-REPUTATION-LOG-UNREACHABLE`（映射到全新的 `NPS-DOWNSTREAM-UNAVAILABLE` 状态）、`NWP-AUTH-REPUTATION-BLOCKED`（`NPS-AUTH-FORBIDDEN` —— 在 NWP v0.7 预留，即便策略字段形态在 Phase 2 / NWP v0.8 才落地）。Phase 1 仅落地条目结构 + 参考类型；Merkle 树、STH、inclusion proof、NDP `/.nid/reputation` 发现、NWM `reputation_policy` 解析按 RFC §8.1 推迟到 Phase 2（v1.0-alpha.4）。
- **版本升级**：
  - NPS-1 NCP：`v0.5` → `v0.6`（NPS-RFC-0001）。
  - NPS-2 NWP：`v0.5` → `v0.7`（NPS-RFC-0003 NWM 加字段 + NPS-CR-0001 §2.1 节点类型重写 + Anchor/Bridge 子节 + NPS-RFC-0004 错误码 `NWP-AUTH-REPUTATION-BLOCKED` 预留；`Depends-On` 升级为 NCP v0.6 + NIP v0.5 + NDP v0.5）。
  - NPS-3 NIP：`v0.3` → `v0.5`（NPS-RFC-0003 §5.1.1 保证等级 + NPS-RFC-0004 §5.1.2 声誉日志条目）。
  - NPS-4 NDP：`v0.4` → `v0.5`（NPS-CR-0001 AnnounceFrame `node_kind` 数组形式 + `cluster_anchor` + `bridge_protocols`；`Depends-On` 升级为 NCP v0.6 + NIP v0.4）。
  - AaaS-Profile：`v0.2` → `v0.3`（NPS-CR-0001 §2 重写；新增 §2A Bridge Node；Depends-On 全面刷新）。
  - NPS-4 NDP：`v0.3` → `v0.4` —— AnnounceFrame (0x30) 新增三个 additive 字段（`activation_mode`、`activation_endpoint`、`spawn_spec_ref`），新增 §3.1.1 激活语义小节。新增字段全部向后兼容：接收方 MUST 把缺失的 `activation_mode` 按 `ephemeral` 处理，因此 `v1.0-alpha.2` 发布者依然可被正确解析。`Depends-On` NCP 版本订正为 `v0.5`。
  - `spec/error-codes.md`：`v0.5` → `v0.8`（RFC-0001 + RFC-0003 + RFC-0004 行 —— 共五个新错误码）。
  - `spec/status-codes.md`：`v0.2` → `v0.4`（新增 `PROTO` 分类 + RFC-0001 行；RFC-0003 映射；新增 `NPS-DOWNSTREAM-UNAVAILABLE` 状态码 + RFC-0004 映射）。
  - `frame-registry.yaml`：`v0.6` → `v0.9` —— NDP 帧 `protocol_version` 更新到 `0.5`（CR-0001 字段）；AnnounceFrame description 扩写包含 `node_kind` / `cluster_anchor` / `bridge_protocols` 三元组；新增顶层 `reservations:` 段，保留 `0x4E`（NPS-RFC-0001）；`IdentFrame (0x20)` `protocol_version` `0.3` → `0.4`，description 扩写包含 `assurance_level`（NPS-RFC-0003）。

### Daemon

- **新增 —— `daemons/` 顶层目录**，承载六个常驻服务二进制，构成 NPS 在生产环境的参考部署拓扑。架构总览见 [`docs/daemons/architecture.cn.md`](docs/daemons/architecture.cn.md)。每个 daemon 配套 kebab-case 二进制名、多阶段 `Dockerfile`、双语 README。
  - **第一层（本机基础设施）：**
    - `daemons/npsd/` —— `npsd`（`Npsd.csproj`、包 `LabAcacia.NPS.Daemon.Npsd`）。监听 `127.0.0.1:17433`。L1 最小集：root Ed25519 keypair 生成（PKCS#8、文件权限 `0600`，满足 NPS-Node-L1 `TC-N1-NIP-01`）、`/.nwm` 自身 manifest、`/health`。可通过 `NPSD_PORT` / `NPSD_HOST` / `NPSD_DATA_DIR` 配置。NCP 原生模式前导 runtime、inbox 持久化、sub-NID 签发、AnnounceFrame 上发推迟到 alpha.4。
    - `daemons/nps-runner/` —— `nps-runner`（`NpsRunner.csproj`、包 `LabAcacia.NPS.Daemon.Runner`）。Phase 1 骨架：Generic Host 脚手架 + 30 秒 heartbeat。Inbox 监视 + `spawn_spec_ref` 解析 + worker 生命周期在 L3 阶段（alpha.5+）落地。
  - **第二层（接入网关）：**
    - `daemons/nps-ingress/` —— `nps-ingress`（`NpsIngress.csproj`、包 `LabAcacia.NPS.Daemon.Ingress`）。Phase 1 骨架：公网 HTTP 监听 `:8080` + `/health`，端点本身记录后续里程碑。TLS termination、限速、NeuronHub 鉴权、CGN 扣款、NPS-RFC-0004 声誉查询、NPS-CR-0001 Anchor Node 中间件接入在 alpha.4 → alpha.5 落地。
    - `daemons/nps-registry/` —— `nps-registry`（`NpsRegistry.csproj`、包 `LabAcacia.NPS.Daemon.Registry`）。Phase 1 骨架：HTTP 监听 NDP 可选独立端口 `17436`；`Resolve`/`Graph`/`Announce` URL 已就位但返回 `NDP-REGISTRY-UNAVAILABLE`（HTTP 503），让消费方提前集成 + graceful fallback。SQLite 后端真实注册在 alpha.4 落地。
  - **第三层（信任锚点 —— NPS Cloud，2027 Q1+）：**
    - `daemons/nps-cloud-ca/` —— `nps-cloud-ca`（`NpsCloudCa.csproj`、包 `LabAcacia.NPS.Daemon.CloudCa`）。Phase 1 deferral 骨架，NIP 可选独立端口 `17435`；`/v1/issue`、`/v1/revoke`、`/v1/crl`、`/v1/ocsp` 返回 `NIP-CA-NOT-READY`（HTTP 503）+ 指针指向 alpha.2 已上线的六个多语言 `tools/nip-ca-server*` OSS CA。本 daemon 自己的 X.509 + ACME 流水线随 NPS-RFC-0002 在 alpha.4 落地。
    - `daemons/nps-ledger/` —— `nps-ledger`（`NpsLedger.csproj`、包 `LabAcacia.NPS.Daemon.Ledger`）。Phase 1：内存日志，遵循 NPS-RFC-0004 `ReputationLogEntry` 结构；`POST /v1/log/entries`、`GET /v1/log/entries?nid=&since=`、`GET /v1/log/sth`。持久化 + Merkle 树 + operator 签名 STH + inclusion proof 在 alpha.4 落地。
- **`impl/dotnet/NPS.sln`**：六个 daemon csproj 全部加入；单次 sln `dotnet build` 覆盖库 + bridges + ingresses + samples + benchmarks + tests + daemons。

### 实现（Impl）

- **.NET —— `impl/dotnet/src/NPS.Core/Ncp/NcpPreamble.cs`（新）**：NPS-RFC-0001 Phase 1。参考 helper：暴露常量前导字节 + 异步写入 / 校验操作，给（即将到来的）原生传输层用。alpha.3 本身不带原生传输；helper 留给将来的 `NPS.Native` 工作或第三方传输实现者直接使用。xUnit 覆盖：来回往返、不匹配、未来主版本字节、状态码映射。
- **.NET —— `impl/dotnet/src/NPS.NIP/AssuranceLevel.cs`（新）** + `IdentFrame.AssuranceLevel`、`NipVerifyContext.MinAssuranceLevel`、`NeuralWebManifest.MinAssuranceLevel`、`NipErrorCodes.{AssuranceMismatch, AssuranceUnknown}`、`NwpErrorCodes.AuthAssuranceTooLow`：NPS-RFC-0003 Phase 1。强类型 `AssuranceLevel` 枚举，带有序比较 + JSON converter（线上字符串 `"anonymous"` / `"attested"` / `"verified"`）。IdentFrame 与 NWM 端到端 round-trip 该字段；verifier 透传到 per-request context 但**尚未强制** —— Phase 1 仅 parse，默认行为不变。NPS.NIP 与 NPS.NWP 包升级为 `1.0.0-alpha.3`。
- **NPS Daemon 参考实现（L1 合规）**：仍推迟到后续里程碑；届时将放在 `impl/dotnet/src/NPS.Daemon/`，合规测试套件放在 `impl/dotnet/tests/NPS.Daemon.Conformance.Tests/`。
- **其余 5 个 SDK（Python / TypeScript / Java / Rust / Go）**：NPS-RFC-0001 Phase 2 —— 各 SDK 的前导 helper —— 按 RFC §8.1 排到 v1.0-alpha.4。

### 工具

- **NIP CA Server 拆出独立发布仓** ——
  [`labacacia/nip-ca-server`](https://github.com/labacacia/nip-ca-server)
  ([Gitee 镜像](https://gitee.com/labacacia/nip-ca-server))。截至 v1.0.0-alpha.2，
  该服务一直只作为 monorepo 子目录发布；从 v1.0.0-alpha.3 起拥有独立的
  公开仓库、独立的 CHANGELOG、独立的 SemVer tag。Monorepo 仍是源
  （`tools/nip-ca-server/`），通过 `tools/release/sync-nip-ca-server.sh`
  物化到发布仓。发布仓自带 `LICENSE` + `NOTICE`，依赖
  nuget.org 上的 `LabAcacia.NPS.NIP` v1.0.0-alpha.3，脱离 monorepo
  也能构建。
- **5 个非 .NET 实现保留为 `tools/nip-ca-server/example/`** ——
  Python / TypeScript / Java / Rust / Go。仍可作为 NPS-3 §8 接口的
  各语言阅读样例，但冻结在 v1.0.0-alpha.2（无 Docker 镜像、无 SemVer tag、
  不进 CI、不推荐生产使用）。如要接手见 `tools/nip-ca-server/example/README.cn.md`。
  这些目录也跟随发布仓的 `example/` 一起对外发出。
- **新增 `tools/release/`** —— `sync-nip-ca-server.sh` 脚本从 monorepo
  生成发布仓工作树（rsync + publish-overlay 覆盖），提交、打 tag、推送
  GitHub。后续从 monorepo 拆出更多独立发布仓时，本脚本是模板。
  完整流程：[`docs/release-process.cn.md`](docs/release-process.cn.md)。
- **NPS Daemons 拆出发布仓** ——
  [`labacacia/nps-daemons`](https://github.com/labacacia/nps-daemons)
  ([Gitee 镜像](https://gitee.com/labacacia/nps-daemons)) 把 4 个 OSS
  daemon（`npsd`、`nps-runner`、`nps-ingress`、`nps-registry`）打成
  bundle —— NPS 参考拓扑的 Layer-1 + Layer-2。每个子目录自包含（独立
  README、Dockerfile、依赖 nuget.org 上 `LabAcacia.NPS.*` 包的 csproj），
  仓库根的 `docker-compose.yml` 一键拉起 4 个。2 个 Layer-3 信任锚
  daemon（`nps-cloud-ca`、`nps-ledger`）以**私有**仓发到 `innolotus`
  组织 ——
  [`innolotus/nps-cloud-ca`](https://github.com/innolotus/nps-cloud-ca)、
  [`innolotus/nps-ledger`](https://github.com/innolotus/nps-ledger)，
  跟 NPS Cloud GA（2027 Q1+）一起公开。`tools/daemons/<daemon>/`
  下每个 daemon 都有自己的 `publish-overlay/`（PackageReference csproj、
  自包含 Dockerfile、nuget.config），`tools/daemons/bundle-overlay/`
  持有 bundle 级文件（顶层 README/CHANGELOG/docker-compose）。3 个
  新 sync 脚本（`sync-nps-daemons.sh`、`sync-nps-cloud-ca.sh`、
  `sync-nps-ledger.sh`）按 `sync-nip-ca-server.sh` 模板。公开 bundle
  注册到 `tools/mirror-to-gitee/sync-all.sh`；私有仓不镜像。

### 兼容性

- alpha.3 中 NDP 改动严格 additive，与 alpha.2 完全向后兼容 —— `activation_mode` 缺失定义为 `ephemeral`；接收方 MUST NOT 因缺字段而拒绝 AnnounceFrame。
- NCP 连接前导（NPS-RFC-0001）**对早于 alpha.3 的原生模式客户端是 breaking change**。HTTP 模式不受影响。原生模式按 `spec/NPS-Roadmap.md` 仍处于 Phase 2+，没有 GA 部署依赖；现在改省去 1.0 GA 之后的废弃周期。alpha.3 落地的 .NET helper 是纯库（暂无传输层），所以没有线上部署会被打破。
- 现有不感知 L1 的工具链无改动即可互通。

---

## [1.0.0-alpha.2] — 2026-04-19

### 规范（Spec）

- **状态升级**：11 份规范文档全部从 `Draft` → `Proposed`，公开 RFC 窗口开启。
- **版本升级**：
  - NPS-0 Overview：`v0.2` → `v0.3`
  - NPS-1 NCP：`v0.4` → `v0.5`
  - NPS-2 NWP：`v0.4` → `v0.5`
  - NPS-3 NIP：`v0.2` → `v0.3`
  - NPS-4 NDP：`v0.2` → `v0.3`
  - NPS-5 NOP：`v0.3` → `v0.4`
  - NPS-Roadmap：`v0.2` → `v0.3`
  - `frame-registry.yaml`：`v0.4` → `v0.5`（所有 `protocol_version` 字段同步更新）
  - `error-codes.md`：`v0.4` → `v0.5`
  - `status-codes.md`：`v0.1` → `v0.2`
  - `token-budget.md`：`v0.1` → `v0.2`
  - `services/NPS-AaaS-Profile.md`：`v0.1` → `v0.2`
- **双语统一**：所有规范文档遵循全项目命名约定 —— `X.md` 为英文主版本，顶部附 `English | [中文版](./X.cn.md)` 语言切换器；`X.cn.md` 为中文副版本。旧的 `X.en.md` 后缀文件全部移除。
- `Depends-On` 交叉引用已同步新版本号。

### 实现（.NET / Python / TypeScript / Java / Rust / Go）

六个参考 SDK 全部同步至 `1.0.0-alpha.2`。在项目达到 `1.0` 稳定版之前，所有 SDK 统一使用相同预发布标签，无论单个仓库变更量多少。

#### .NET —— `impl/dotnet/` —— `1.0.0-alpha.2`

- `NPS.Core` README 显式列出全套 NCP 帧（AnchorFrame / DiffFrame / StreamFrame / CapsFrame / HelloFrame / ErrorFrame）。
- `NPS.NWP` README 列出四种节点类型（Memory / Action / Complex / Gateway）。
- 状态更新：495 测试全绿，包含新增的 wire-size 基准、Gateway Node 中间件、A2A Bridge。

#### Python —— `impl/python/` —— `1.0.0-alpha.2`

- 版本 `0.2.0` → `1.0.0-alpha.2`；除版本对齐外无功能变更。
- 162 测试，97% 覆盖率。

#### TypeScript —— `impl/typescript/` —— `1.0.0-alpha.2`

- **Fixed —— `NpsFrameCodec is not a constructor`**：`src/core/index.ts` 现显式从 `./codec.js`、`./registry.js`、`./cache.js` 重新导出随包提供的 OOP API（`NpsFrameCodec`、`Tier1JsonCodec`、`Tier2MsgPackCodec`、`FrameRegistry`、`AnchorFrameCache`）。`./codecs/` 下的并行函数式 API 仍可通过直接路径导入，但不再自动导出（与类 API 在 `FrameType` / `EncodingTier` / `FrameHeader` 上冲突）。
- **Fixed —— 缺失的 Ed25519 运行时依赖**：`@noble/ed25519` 和 `@noble/hashes` 现已声明为运行时依赖。此前 `src/nip/identity.ts` 和 `src/ndp/validator.ts` 已 import 但 `package.json` 未声明，导致 npm 消费者安装失败。
- **Added —— HelloFrame（NCP 0x06）**：`src/ncp/frames.ts` 新增 `HelloFrame` 类（snake_case toDict/fromDict），在 `ncp/registry.ts` 中注册；`FrameType.HELLO = 0x06` 加入枚举（介于 ALIGN 与 NWP 区段之间）。
- **Added —— subpath exports**：`package.json` 的 `exports` 映射新增 `./nwp`、`./nip`、`./ndp`、`./nop`（此前仅声明了 `.`、`./core`、`./ncp`，但 README 已示例使用全部六个）。
- Node 引擎要求升级到 `>=22.0.0`，与实际技术栈对齐。
- 264 测试全绿（此前为 158 通过 + 5 个文件失败 + 1 个测试失败）。

#### Java —— `impl/java/` —— `1.0.0-alpha.2`

- 版本 `0.1.0` → `1.0.0-alpha.2`；除版本对齐外无功能变更。
- 87 测试全绿。

#### Rust —— `impl/rust/` —— `1.0.0-alpha.2`

- 版本 `0.1.0` → `1.0.0-alpha.2`；除版本对齐外无功能变更。
- 6 个 workspace crate 共 88 测试全绿。

#### Go —— `impl/go/` —— `1.0.0-alpha.2`

- 版本 `0.1.0` → `1.0.0-alpha.2`；除版本对齐外无功能变更。
- 75 测试全绿。

### 工具

#### NIP CA Server —— 六语言新实现

- `tools/nip-ca-server/` —— ASP.NET Core 10 + SQLite（`1.0.0-alpha.2`）
- `tools/nip-ca-server-python/` —— FastAPI + SQLite（`1.0.0-alpha.2`）
- `tools/nip-ca-server-ts/` —— Fastify + better-sqlite3（`1.0.0-alpha.2`）
- `tools/nip-ca-server-java/` —— Spring Boot 3.4 + SQLite（`1.0.0-alpha.2`）
- `tools/nip-ca-server-rust/` —— Axum + SQLite（`1.0.0-alpha.2`）
- `tools/nip-ca-server-go/` —— stdlib `net/http` + SQLite（`1.0.0-alpha.2`）

六个实现共用同一套 REST API（NPS-3 §8）和 Docker Compose 入口。

### 兼容 Bridge

- `compat/mcp-ingress/` —— `LabAcacia.McpIngress` 将 NWP Memory / Action / Complex Node 暴露为 MCP 2024-11-05 服务端（15 测试）。版本 `1.0.0-alpha.2`。
- `compat/a2a-ingress/` —— `LabAcacia.A2aIngress` 将 Action / Complex / Gateway Node 暴露为 Google A2A v0.2 服务端；`GET /.well-known/agent.json`、JSON-RPC 2.0 `tasks/send` / `tasks/get` / `tasks/cancel`（18 测试）。版本 `1.0.0-alpha.2`。

### 文档

- 根 `README` 翻转为全项目约定：`README.md` 为英文主、`README.cn.md` 为中文副。旧的 `README.en.md` 后缀文件删除。
- 所有 SDK 和 CA Server README 新增中文副本（`README.cn.md`），两份文件顶部都带语言切换器。
- CLAUDE.md 的规范索引表、仓库结构树、版本引用已同步新版本号。
- `docs/sdk/dotnet/index.en.md` 指向根 README 路线图章节的链接已修复（`README.en.md` → `README.md`）。

---

## [1.0.0-alpha.1] — 2026-04-10

首个公开 alpha。完整说明见 [Release-v1.0.0-alpha.1](https://github.com/LabAcacia/nps/releases/tag/v1.0.0-alpha.1)。

---

[1.0.0-alpha.7]: https://github.com/LabAcacia/nps/releases/tag/v1.0.0-alpha.7
[1.0.0-alpha.6]: https://github.com/LabAcacia/nps/releases/tag/v1.0.0-alpha.6
[1.0.0-alpha.2]: https://github.com/LabAcacia/nps/releases/tag/v1.0.0-alpha.2
[1.0.0-alpha.1]: https://github.com/LabAcacia/nps/releases/tag/v1.0.0-alpha.1
