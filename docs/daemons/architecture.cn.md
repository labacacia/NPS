[English Version](./architecture.md) | 中文版

# NPS Daemon 架构

> NPS 在生产环境的参考部署拓扑。六个常驻服务分布在三层；每一层独立水平扩展，
> 各层有不同的信任边界和故障边界。
>
> 状态 —— alpha.18 源码 / alpha.19 债务收口边界：`npsd` 已在配置的
> loopback 端口复用 HTTP 与 local-dev 原生 NCP，具备有界 preamble/Hello
> 协商和 Anchor ACK/缓存。其 per-NID inbox 已使用持久 SQLite，并具备跨重启
> 的 TTL、优先级和 ack 语义。此 ephemeral L1 profile 明确拒绝 resident/hybrid
> push，且仍不宣称完整 Node L1。
> `nps-ingress` 已实现 native TLS 1.3 / ALPN
> `nps/1.0` / mTLS / session-NID 绑定的 transport 边界。适用的
> `TC-N2-Tls-01..04` family 已可执行；更宽的 admission 路线图项已明确处置，
> 不作为 transport capability 声明。`nps-runner` 已实现 inbox 调度、portable
> OCI SpawnSpec 解析/执行、有界 worker 生命周期，以及带跨进程 claim、重启/回收、
> 终态去重与旧属主围栏证据的持久化 SQLite 租约。完整 TaskFrame DAG/Saga L3
> 认证仍不作声明；逐 case manifest 记录其准确边界。其他 daemon 的收口工作单独跟踪。

---

## 三层

```
┌──────────────────────────────────────────────────────────────────────┐
│ 第三层 —— 信任锚点      （NPS Cloud，2027 Q1+）                       │
│ ┌──────────────────┐                  ┌──────────────────┐           │
│ │ ⑤ nps-cloud-ca   │                  │ ⑥ nps-ledger     │           │
│ │ X.509 签发       │                  │ K-of-N 审计日志   │           │
│ │ CRL / OCSP       │                  │ 防篡改证据         │           │
│ └────────┬─────────┘                  └────────┬─────────┘           │
└──────────┼─────────────────────────────────────┼─────────────────────┘
           ▼                                     ▼
┌──────────────────────────────────────────────────────────────────────┐
│ 第二层 —— 接入网关      （按入口种类，按需部署）                       │
│ ┌──────────────────┐                  ┌──────────────────┐           │
│ │ ③ nps-ingress    │                  │ ④ nps-registry   │           │
│ │ Internet 入站    │                  │ 跨机 NDP         │           │
│ │ NPS-over-TLS     │                  │ resolve+graph    │           │
│ │ TLS + NID 绑定  │                  │ （L2 阶段）      │           │
│ └────────┬─────────┘                  └────────┬─────────┘           │
└──────────┼─────────────────────────────────────┼─────────────────────┘
           ▼                                     ▼
┌──────────────────────────────────────────────────────────────────────┐
│ 第一层 —— 本机基础设施  （每台机器各一个）                            │
│ ┌──────────────────┐                  ┌──────────────────┐           │
│ │ ① npsd           │ ◀── inbox ────  │ ② nps-runner     │           │
│ │ NCP wire +       │                  │ FaaS 调度         │           │
│ │ root keypair +   │ ── spawn-spec ▶ │ ephemeral worker │           │
│ │ session reg +    │                  │ 生命周期管理（L3）│           │
│ │ inbox            │                  │                  │           │
│ │ port :17433      │                  │                  │           │
│ └──────────────────┘                  └──────────────────┘           │
└──────────────────────────────────────────────────────────────────────┘
```

## 六个 daemon

### 第一层 —— 本机基础设施（每台机器各一个）

#### ① `npsd` —— 协议接入与状态宿主

- **监听**：默认 `127.0.0.1:17433`，在统一端口无损复用 HTTP 控制流量与 local-dev 原生 NCP。
- **已实现原生边界**：有界 [NPS-RFC-0001](https://github.com/labacacia/NPS/blob/main/spec/rfcs/NPS-RFC-0001-ncp-connection-preamble.cn.md) preamble 与 Hello/Caps 协商、协商后的编码/payload 约束、canonical AnchorFrame ACK/缓存，以及确定性的静默/错误关闭。公网 TLS 1.3/mTLS 终止属于 `nps-ingress`。
- **已实现状态边界**：root Ed25519 密钥对管理、为本机 agent 签发 sub-NID，以及用于 `ephemeral` HTTP pull + 显式 ack 的持久 per-NID SQLite inbox。未投递帧、绝对 TTL、优先级顺序和 ack 均跨 daemon 重启保留。
- **明确限制**：不宣称完整 Node L1。全部 20 个 L1 用例、当前证据状态、可运行门禁和剩余缺口见 [`NPS-NODE-L1-MANIFEST.json`](https://github.com/labacacia/NPS-Dev/blob/main/tools/daemons/npsd/conformance/NPS-NODE-L1-MANIFEST.json)。resident/hybrid push 在 L1 可选，本 profile 明确拒绝；BYO-key agent 不会自动广播，因为 npsd 不持有其私钥，也不得冒充 publisher 签名。
- **为什么必须常驻**：身份和 inbox 不能跟着 session 走 —— 持久状态需要宿主进程。本机所有 NPS 客户端（MCP shim、resident agent、worker、ingress shim）都通过它接入。
- **为什么不能合并**：协议层是业务无关的，所有上层共享；合并到任何业务进程都会污染信任域。
- **参考合规**：目标是 `NPS-Node Profile L1`（见 [`spec/services/NPS-Node-Profile.cn.md`](https://github.com/labacacia/NPS/blob/main/spec/services/NPS-Node-Profile.cn.md) 与 [`spec/services/conformance/NPS-Node-L1.cn.md`](https://github.com/labacacia/NPS/blob/main/spec/services/conformance/NPS-Node-L1.cn.md)）。

#### ② `nps-runner` —— 任务调度器 / FaaS runtime *(L3 阶段)*

- **已实现的 runtime 边界**：向本机 `npsd` 自注册并轮询 inbox；校验 inline
  或引用形式的 portable OCI SpawnSpec；执行 OCI 容器（保留旧版直接子进程兼容
  路径）；限制并发、空闲与最大运行时间；捕获日志并报告完成结果。
- **已实现的 ownership 行为**：在多个协调 runner 进程共享的持久化 SQLite
  文件中原子 claim 并周期续租。每次启动生成新的 instance identity 以围栏旧
  进程；终态记录跨重启保留；租约丢失时取消 worker，且不写终态、不 ack
  inbox、不发送通知。
- **明确限制**：当前 inbox 契约执行单个 SpawnSpec，而不是多节点 TaskFrame，
  因此不声明完整 Node L3 认证。已验证/部分验证/未执行的逐 case 边界见
  [`NPS-NODE-L3-MANIFEST.json`](https://github.com/labacacia/NPS-Dev/blob/main/tools/daemons/nps-runner/conformance/NPS-NODE-L3-MANIFEST.json)，
  更完整能力边界见
  [`NPS-RUNNER-CAPABILITIES.json`](https://github.com/labacacia/NPS-Dev/blob/main/tools/daemons/nps-runner/conformance/NPS-RUNNER-CAPABILITIES.json)。
- **为什么必须常驻**：消息随时到达；调度器必须在那才能 spawn。worker 是临时的，但调度器不是。
- **为什么与 `npsd` 分离**：资源画像差异巨大 —— `npsd` 静态、内存可预测；`nps-runner` 在 spawn 高峰时 burst；故障隔离很关键（worker crash 不该把 NCP 层带下去）；信任边界不同（`nps-runner` 跑用户提供的 Agent SDK 代码，协议层不该有这个权限面）。

### 第二层 —— 接入网关（按入口种类，按需部署）

#### ③ `nps-ingress` —— Internet 入站网关

- **已实现的 transport 边界**：以 TLS 1.3 + ALPN `nps/1.0` 终止 native NCP；默认开启的 mTLS 把 NIP 客户端证书验证到配置信任锚；Caps 后的 `IdentFrame` NID 在转发前绑定到证书 NID；握手时间/frame 大小和后端准入前 framing 均有硬边界；通过的字节流代理到本机 NCP 后端，并在客户端 half-close 后排空响应。`TC-N2-Tls-01..04` 已通过真实 socket 执行。
- **边界处置**：拓扑、Bridge、HA 与 Registry family 适用于其他 IUT 角色。限速、NeuronHub 用户鉴权、CGN 扣款、声誉、Anchor 中间件及广义 DDoS 控制不作为 transport capability 宣传；其产品/AaaS/可选组合/部署归属记录在 [`NPS-INGRESS-ADMISSION-DISPOSITION.json`](https://github.com/labacacia/NPS-Dev/blob/main/tools/daemons/nps-ingress/conformance/NPS-INGRESS-ADMISSION-DISPOSITION.json)。
- **为什么必须常驻**：对外暴露的端口必须始终在听。
- **为什么与 `npsd` 分离**：`npsd` 默认只 bind 127.0.0.1；网关 bind 公网。攻击面、安全策略、运维边界完全不同。`npsd` 失败影响本机所有 NPS 流量；网关失败只影响外部入站 —— 二者必须能独立重启。
- **说明**：这是**进程级**名字；规范层"集群控制平面"角色现在叫 **Anchor Node**（NWP），见 [NPS-CR-0001](https://github.com/labacacia/NPS/blob/main/spec/cr/NPS-CR-0001-anchor-bridge-split.md)。`nps-ingress` 进程 MAY 承载 Anchor Node 中间件，但不是必须。

#### ④ `nps-registry` —— Discovery 注册中心 *(L2 阶段，可选托管)*

- **跨机 NDP** 发现服务的中心节点。响应 `Resolve` / `Graph` 查询；汇总多机注册信息。
- **每机 `npsd` 只知道本机 session**；跨机查询走 registry。
- **为什么必须常驻**：发现请求随机到达；中心节点不能 cold start。
- **为什么独立于 `npsd`**：`npsd` 一机一个，registry 一网一个（或一组带 HA）。L1 不需要；L2 出现。NeuronHub 自己跑一个供其用户用；LabAcacia 跑公益版给开源生态。

### 第三层 —— 信任锚点 *(NPS Cloud 阶段，2027 Q1+)*

#### ⑤ `nps-cloud-ca` —— 证书签发服务

- **NIP 信任根**，签发跨组织的 NID 证书，提供 CRL / OCSP。LabAcacia 自己跑开源 CA；商业用户也可以跑私有 CA。
- **为什么必须常驻**：撤销查询必须实时响应；签发请求随机到达。
- **为什么独立于 `nps-registry`**：CA 是信任的*来源*；Registry 是信息的*索引*。CA 失败影响新 NID 接入和撤销响应；Registry 失败只影响发现。信任级别、运维流程、审计要求都不同。CA 私钥 MUST 做物理或 HSM 级保护；registry 没这要求。

#### ⑥ `nps-ledger` —— 审计与合规日志收集器

- **append-only 收集点**，K-of-N 审计共识所需 —— NeuronHub 的 CGN 结算正确性最终靠这一层提供证据。实现 [NPS-RFC-0004](https://github.com/labacacia/NPS/blob/main/spec/rfcs/NPS-RFC-0004-nid-reputation-log.cn.md) 定义的 Certificate-Transparency 风格日志：提交、查询、signed tree head、inclusion proof。
- **为什么必须常驻**：审计日志是连续 append 流；接收端不能 down，否则有 gap。
- **为什么独立**：审计的隔离要求最强 —— `nps-ledger` MUST NOT 信任被审计方的进程，所以必须跑在独立 trust domain。即便 `npsd` 被攻陷，`nps-ledger` 还能提供独立证据。

---

## 命名与二进制布局

| Daemon | 文件夹 | 项目 | 二进制 | NuGet 包 |
|--------|--------|---------|--------|----------------|
| ① | `tools/daemons/npsd/` | `Npsd.csproj` | `npsd` | `LabAcacia.NPS.Daemon.Npsd` |
| ② | `tools/daemons/nps-runner/` | `NpsRunner.csproj` | `nps-runner` | `LabAcacia.NPS.Daemon.Runner` |
| ③ | `tools/daemons/nps-ingress/` | `NpsIngress.csproj` | `nps-ingress` | `LabAcacia.NPS.Daemon.Ingress` |
| ④ | `tools/daemons/nps-registry/` | `NpsRegistry.csproj` | `nps-registry` | `LabAcacia.NPS.Daemon.Registry` |
| ⑤ | `tools/daemons/nps-cloud-ca/` | `NpsCloudCa.csproj` | `nps-cloud-ca` | `LabAcacia.NPS.Daemon.CloudCa` |
| ⑥ | `tools/daemons/nps-ledger/` | `NpsLedger.csproj` | `nps-ledger` | `LabAcacia.NPS.Daemon.Ledger` |

## 历史 alpha-to-beta 阶段规划

下表保留 alpha.4 时的规划快照，不是当前 capability matrix。当前
`nps-ingress` 与 `nps-runner` 的行为和缺口以上文及 daemon README 为准。

| Daemon | alpha.3 | alpha.4（本次） | alpha.11+ | beta / 1.0 |
|--------|---------|----------------|----------|------------|
| `npsd` | L1 最小集：`127.0.0.1:17433` 监听、root keypair 生成、`/.nwm`、基础 `/health`、本机 NDP 上发 AnnounceFrame | L1+：sub-NID 签发、per-NID inbox 队列、NCP 原生模式前导 runtime（NPS-RFC-0001）| 推送到 resident agent | 完整 L1 + L2 合规 |
| `nps-runner` | 骨架 + Generic-Host worker；记录 inbox-watch 契约 | Inbox poller 接入 `npsd` | Spawn 生命周期、隔离 | L3 合规 |
| `nps-ingress` | 骨架 + `0.0.0.0:443` HTTP 监听（TLS 关）；记录 NeuronHub 集成点 | Anchor Node 中间件接入（NPS-CR-0001 wiring）| 声誉查询（NPS-RFC-0004）| DDoS、CGN 扣款 |
| `nps-registry` | 骨架 + HTTP 监听；占位 `Resolve`/`Graph` 端点返回 `NDP-REGISTRY-UNAVAILABLE` | **真实 SQLite 注册中心**：Announce / Resolve / Graph 全部可用；TTL lazy expiry；单调 graph seq；文件或内存通过 env 选择 | HA cluster 模式 | Federation |
| `nps-cloud-ca` | 骨架；实际签发交给 `tools/nip-ca-server*`（6 个多语言 OSS CA）| NPS-RFC-0002 X.509 + ACME 接入（骨架；完整签发在 nip-ca-server）| HSM 集成 | 跨 CA 信任 |
| `nps-ledger` | 骨架 + 内存日志，遵循 NPS-RFC-0004 Phase 1 条目结构 | **Phase 2**：SQLite 持久化、RFC 9162 Merkle 树、operator 签名 STH、`/v1/log/proof` 包含证明端点 | STH gossip、公共镜像 | 公共日志认证 |

---

*归属：LabAcacia / INNO LOTUS PTY LTD · Apache 2.0*
