# NPS alpha.19 历史债清单与基线冻结

English version: [alpha19-debt-inventory.md](alpha19-debt-inventory.md)

**状态**：P19-0 基线
**创建日期**：2026-08-31
**发布目标**：`v1.0.0-alpha.19`
**跟踪**：[NPS-Dev#91](https://github.com/labacacia/NPS-Dev/issues/91) · [ChangeControl EPIC-004](https://github.com/innolotus/ChangeControl/issues/4)

## 1. 目的

alpha.20 预计引入较大规模的新设计。因此 alpha.19 定义为现有／alpha.20
之前 NPS 契约的清债与基线冻结版本：在新设计开始前，关闭协议、六 SDK、daemon、
可执行合规、文档真实性以及发布分发债务。

P19-0 冻结的是清单。P19-0 完成**不代表**表内债务已经实现，而表示每个候选项都有
稳定 ID、证据、责任方、处置、下游范围与客观关闭关口。

## 2. 债务判定与处置

满足至少一项即属于 alpha.19 债务：

1. alpha.18 或更早已完成里程碑曾承诺，但仍未完成；
2. 当前规范写有 `MUST`，参考 runtime 却没有可执行行为；
3. Implemented／capability／status 声明缺少 runtime 或负路径证明；
4. 六个受支持 SDK 在当前契约行为上不一致；
5. daemon 暴露当前契约，却缺少必要 runtime、持久化、准入或认证；
6. canonical／分发源码、fixture、版本或当前文档存在无法解释的漂移。

允许的处置：

| 处置 | 含义 | 关闭条件 |
|---|---|---|
| **实现** | 当前行为缺失或不完整 | 代码／规范／文档与可执行验收证据 |
| **纠正声明** | 行为已存在，但当前状态／文档／矩阵不实 | 当前文档／状态与证据链接完成对账 |
| **验证后关闭** | 行为看似存在，但 parity／覆盖未获证明 | 跨 runtime 或分发验证证据 |
| **证明属未来** | 该项从未属于 alpha.20 前契约 | 规范／路线图证据、owner 与未来目标，且没有冲突的当前声明 |

仅把未解决的当前行为改名为“未来”不算关闭。

## 3. 协议与共享契约清单

| ID | 优先级 | 债务与来源证据 | 处置／owner | 关闭证据 | 下游 |
|---|---|---|---|---|---|
| **P19-P01** | P0 | NCP 0.12 加固从 alpha.18 移除：runtime keepalive timer、确定性 timeout 关连、QUIC migration/0-RTT/流控/背压，以及 idle/dead-peer/oversized-Nop 向量。见 [`spec/NPS-Roadmap.cn.md`](../spec/NPS-Roadmap.cn.md)。 | 实现 · NCP + 六 SDK owner | EN/CN NCP 0.12 delta；六 SDK 执行共享 fixture；确定性 close reason 与 timer 测试 | NPS、六 SDK 仓、ingress/daemons |
| **P19-P02** | P0 | NDP 0.13 持久化／恢复从 alpha.18 移除：sequence/epoch fence 跨重启、分区、stale、同 epoch split、loop 与恢复。 | 实现 · NDP + 六 SDK owner | EN/CN NDP 0.13 delta；持久存储契约；六 SDK 通过共享重启/分区/fence fixture | NPS、六 SDK 仓、nps-registry |
| **P19-P03** | P0 | NOP 0.10 的有界 replay/eviction、TTL、`weighted_first_k`、`merge_all` 和丢包/乱序/重复/超时行为从 alpha.18 移除。 | 实现 · NOP + 六 SDK owner | EN/CN NOP 0.10 delta 与故障 fixture 在六 runtime 一致执行 | NPS、六 SDK 仓、nps-runner |
| **P19-P04** | P0 | NWP 可续订 subscription 强制执行与可移植 stability/SLA/billing metadata 原计划进 alpha.18，但与此无关的 0.21 bump 没有交付它们。 | 实现 · NWP + 六 SDK owner | 规范 delta；取消/续订/故障测试；六 SDK metadata round-trip 与当前文档 | NPS、六 SDK 仓、Node profile |
| **P19-P05** | P0 | NIP 短寿证书续期互操作、OCSP/CRL timeout/stale/unknown fail-closed 与 Phase-3 拒绝 advisory 原计划交付，但与此无关的 0.14 bump 没有覆盖。 | 实现 · NIP/CA + 六 SDK owner | 续期互操作与吊销故障套件；确定性 advisory 输出；EN/CN 对齐 | NPS、六 SDK 仓、NIP-CA-Server、ingress |
| **P19-P06** | P0 | `frame-registry.yaml` 0.15 及依赖的版本／overview／错误／状态面随 P19-P01..P03 顺延。 | 实现 · 共享规范 owner | Registry 0.15、version matrix、overview、errors/statuses、生成常量检查与 CN 镜像一致 | 全部必需发布列车仓库 |

## 4. SDK 与实现 parity 清单

| ID | 优先级 | 债务与来源证据 | 处置／owner | 关闭证据 | 下游 |
|---|---|---|---|---|---|
| **P19-S01** | P0 | alpha.19 铁律要求 .NET、Python、TypeScript、Go、Java、Rust 行为一致；DTO/catalog 存在不能证明 timer、持久化、过期、replay、取消、准入或恢复。 | 实现／验证 · 六 SDK owner | 一张以 P19-P01..P05 为键的行为矩阵；每个必需 cell 链接可执行测试和 package surface | 六 standalone SDK 仓 |
| **P19-S02** | P1 | NPS handbook 记录 Go 没有导出的 runtime `Version` 常量并称为 known gap，而 Go 分发已有 `VERSION` 文件。 | 实现或纠正声明 · Go owner | `core.Version`（或明确的 package metadata API）及测试；NPS handbook 完成对账 | NPS-SDK-Go、NPS-Wiki |
| **P19-S03** | P1 | 历史路线图称所有 SDK 都缺 NDP DNS TXT 解析，但当前 .NET/Python/TS/Go/Java/Rust 源码与测试已有 lookup/parse/fallback。 | 纠正声明并验证 · NDP/docs owner | 记录六 SDK DNS TXT 测试清单；历史行标为已被取代；不再存在当前 gap 声明 | NPS、Wiki、六 SDK 文档 |

## 5. Daemon 清单

| ID | 优先级 | 债务与来源证据 | 处置／owner | 关闭证据 | 下游 |
|---|---|---|---|---|---|
| **P19-D01** | P0 | `nps-ingress` 源码已有 TLS 1.3、ALPN `nps/1.0`、mTLS、inline session-NID 绑定与代理，但 [`tools/daemons/nps-ingress/README.cn.md`](https://github.com/labacacia/NPS-Dev/blob/main/tools/daemons/nps-ingress/README.cn.md)、health `todo` 和架构文本仍把部分行为写为 skeleton／future。 | 纠正声明 · ingress/docs owner | 源码/health/README/architecture/changelog 一致；当前文档引用 inline binding 与 half-close 测试 | NPS-Daemons、NPS-Wiki |
| **P19-D02** | P0 | ingress 尚未完成适用的 `TC-N2-Tls-*`、`TC-N2-*`、`TC-N2-HA-*` 证据，以及当前契约的 rate limit/auth/CGN/reputation/Anchor 准入。 | 实现 · ingress + conformance owner | 可执行 family manifest；准入负路径；不得 advertise 未支持 capability | NPS-Daemons、Node L2 认证 |
| **P19-D03** | P1 | `nps-runner` 已实现 portable OCI SpawnSpec 与周期性 lease renewal，但历史路线图／状态仍列为剩余工作。 | 纠正声明并验证 · runner/docs owner | 当前文档引用 OCI/renewal 测试；移除或标明历史的过期 remaining-work 声明 | NPS-Daemons、NPS-Wiki |
| **P19-D04** | P0 | Runner lease/dedup 是进程内状态，但当前措辞声称 exactly-one-runner；deployment L3 认证、崩溃/重启/reclaim、lease-loss cancel 与 terminal ownership 需要共享／持久证明。 | 实现 · runner owner | 多 runner 持久 store 测试；crash/reclaim 与 lease-loss 故障测试；L3 manifest | NPS-Daemons、NOP runtime 文档 |
| **P19-D05** | P0 | [`tools/daemons/npsd/README.cn.md`](https://github.com/labacacia/NPS-Dev/blob/main/tools/daemons/npsd/README.cn.md) 与 changelog 把 native NCP transport 标为未完成，而 Node profile 存在当前 NCP 要求。 | 实现 · npsd/NCP owner | native preamble/handshake/close 行为和适用 Node 测试通过；文档／状态一致 | NPS-Daemons、ingress integration |
| **P19-D06** | P0 | `npsd` inbox 仍在内存中，而 `TC-N1-NWP-02` 要求重启后保持 inbox；resident push 仍记录为 future。 | 实现 · npsd/NWP owner | 持久队列 migration/restart/TTL/priority/ack 测试、resident delivery 处置和 L1 manifest | NPS-Daemons、runner |
| **P19-D07** | P1 | `npsd` 文档仍把 NDP AnnounceFrame 发射与 sub-NID renewal 标为未完成。 | 实现 · npsd/NDP/NIP owner | 签名 announce/liveness 测试与 renewal/revocation/restart 测试；health/docs 更新 | NPS-Daemons、nps-registry |

## 6. 合规清单

| ID | 优先级 | 债务与来源证据 | 处置／owner | 关闭证据 | 下游 |
|---|---|---|---|---|---|
| **P19-C01** | P0 | [`NPS-Node-L1.cn.md`](../spec/services/conformance/NPS-Node-L1.cn.md) 实际定义 20 个 case 标题（旧计数正文误写为 21），且参考套件表仍写 .NET 计划中、Python/TS TODO。SDK conformance catalog 不等于可执行认证。 | 实现 · Node conformance + runtime owner | 每个 advertised implementation 有可运行 L1 harness/manifest；全部 mandatory case pass，optional `na` 合法 | Daemons、SDK/release 文档 |
| **P19-C02** | P0 | [`NPS-Node-L2.cn.md`](../spec/services/conformance/NPS-Node-L2.cn.md) 定义 topology、TLS、bridge、HA family，但仍写 Python/TS 可执行套件 TODO，ingress 也缺完整证据。 | 实现 · Node conformance + daemon/SDK owner | 可执行 family manifest；强制 all-or-none family 规则；适用 runtime 全部通过 | Daemons、六 SDK |
| **P19-C03** | P0 | [`NPS-Node-Profile.cn.md`](../spec/services/NPS-Node-Profile.cn.md) 仍把详细 L2 ID 与 L2 suite 标为 TODO；L2-01..L2-07 只有 follow-up 跟踪。 | 实现当前规范 case 或证明属未来 · profile owner | 每个 L2 requirement 有稳定 ID、规范分类、case mapping 和实现处置；不得存在无支持的 L2 声明 | Node profile 与认证模板 |
| **P19-C04** | P1 | L3 case 已存在、runner 也声明 Layer-3 行为，但 deployment-specific certification 仍单独且无记录。 | 实现适用当前契约或收窄声明 · runner/profile owner | 明确 L3 claim 边界，并为已声明 family 生成可执行 manifest | NPS-Daemons、NPS handbook |

## 7. 规范与文档真实性清单

| ID | 优先级 | 债务与来源证据 | 处置／owner | 关闭证据 | 下游 |
|---|---|---|---|---|---|
| **P19-T01** | P0 | NPS-CR-0005 已是 `Implemented`，六 SDK 有 RA policy/service/tests，但 NIP §8.1 仍把 RA endpoint 标为 `stub`，并称 CR 到 Implemented 才补 body。 | 纠正规范并验证 · NIP owner | EN/CN NIP §8.1 与已实现契约一致；CR checklist/status 与六 SDK/CA 证据对账 | NPS、六 SDK、NIP-CA-Server |
| **P19-T02** | P0 | 已 Accepted/Active 的 RFC coverage matrix 仍有 `_TBD_`/`pending`，尤其 RFC-0001/0002/0003/0004，但后续实现已经存在。 | 纠正声明并验证 · RFC owner | EN/CN matrix 与源码／测试一致；真实缺失 cell 转成实现项，不再保留过期 pending | NPS 与 Wiki |
| **P19-T03** | P1 | 历史路线图仍有已被取代的“所有 SDK 未实现”和 daemon skeleton 语句，当前状态标签不足。 | 纠正声明 · roadmap owner | 保留历史事实，但被取代的行明确链接关闭版本／证据 | Release roadmap 与 Wiki |
| **P19-T04** | P0 | Daemon root/readme/architecture/health 状态跨越 skeleton 与当前实现；`nps-registry`/`nps-ledger` 描述也落后于已交付 SQLite/Merkle/gossip。 | 纠正声明并验证 · daemon/docs owner | 形成一份由 health/tests 生成或机械校验的当前 capability matrix；EN/CN 与 NPS handbook 一致 | NPS-Daemons、ledger、Wiki |
| **P19-T05** | P0 | 仓库要求 EN/CN 同批变更；alpha.19 会跨多个面增加规范与运维内容。 | 验证后关闭 · documentation owner | 结构／语义 parity 检查通过；不存在未翻译 normative delta | NPS 与 Wiki |
| **P19-T06** | P1 | 根 `CLAUDE.md`、README/version 表和源码布局说明含有过期版本或 phase 措辞。 | 纠正声明 · repository docs owner | 当前入口文档与 `version-matrix.yaml`、真实源码和发布拓扑一致；一致性脚本通过 | Contributor onboarding |

## 8. 发布与分发清单

| ID | 优先级 | 债务与来源证据 | 处置／owner | 关闭证据 | 下游 |
|---|---|---|---|---|---|
| **P19-R01** | P0 | NPS-Dev 是 source of truth；Release、SDK 和 daemon 仓必须按删除语义及明确 distribution-only 例外物化。 | 验证后关闭 · release owner | 物化后生成 Dev→Release/SDK/daemon 零未解释漂移报告 | 全部必需列车仓库 |
| **P19-R02** | P0 | 每个 standalone 必须携带它实际执行的 fixture；只有 catalog 或只有 source-tree fixture 不能证明 package。 | 实现／验证 · release + SDK owner | 每个 standalone 在 clean checkout package test 中执行 vendored fixture | 六 SDK 仓 |
| **P19-R03** | P0 | 发布族铁律是含 `nps-conformance` 的八个 Rust crate 与十一个 NuGet 包，另含 PyPI/npm/Maven/Go 及 image/release artifact。 | 验证后关闭 · release owner | 机器可读 artifact inventory、package dry-run、最低工具链测试与依赖／安全扫描 | Registry 与 release page |
| **P19-R04** | P0 | Registry preflight 必须证明 publish 权限而非匿名读取；`version.yaml` 最后 bump。 | 验证后关闭 · release owner | 权限检查有记录、secret 不入库、版本 oracle 一致、pre-release review 全绿 | 全部 registry |
| **P19-R05** | P0 | 发布不可逆，实现完成不等于获得发布授权。 | 强制关口 · release owner/user | pre-release review 后记录明确批准；tag/package/image/release 验证入日志 | GitHub、Gitee、registry |

## 9. 已证明未来／排除清单

除非出现与当前／alpha.20 前契约相冲突的新证据，下列项目不属于 alpha.19 债务。

| ID | 排除范围 | 证据／边界 | 重新进入规则 |
|---|---|---|---|
| **P19-X01** | NIP Phase-3 flag day | 当前路线图明确目标为 beta.1 | 仅可通过显式 Epic／设计修订进入 |
| **P19-X02** | 多区域 NPS Cloud CA、生产 HSM、cross-CA trust | 路线图 Phase 3 / 2027 Q1+ | 单独 Cloud 里程碑／RFC |
| **P19-X03** | NPS 1.0 freeze 与标准化工作 | Beta/Phase 4 路线图 | 单独稳定化／标准化计划 |
| **P19-X04** | C++ 与 PHP 晋级 | alpha.13 起显式降级为 placeholder SDK | 有源码＋测试并显式决定进入受支持 SDK 集 |
| **P19-X05** | NPS Studio 与 NPS-NWP-Manager 完成 | 有意保留的 planned/stub 产品仓 | 单独产品 Epic／里程碑 |
| **P19-X06** | alpha.20 新协议／产品设计 | 产品决策：alpha.19 只清债 | alpha.20 设计记录 |
| **P19-X07** | 已退役 compat-ingress 的 v0.2 feature TODO | alpha.18 起 compat ingress 退出必需列车，由 NWP Bridge 取代 | 只有证明存在受支持当前 consumer 才重新进入 |
| **P19-X08** | 实现期间 tag/package/image 发布 | 必须单独批准 | P19 pre-release review 成功并获得明确批准 |

## 10. 仓库责任图

| 工作流 | Canonical owner | 物化／受影响仓库 |
|---|---|---|
| 规范、共享 fixture、六实现 | `labacacia/NPS-Dev` | NPS 与六 SDK 仓 |
| 公共 daemon bundle | `labacacia/NPS-Dev/tools/daemons` | `labacacia/NPS-Daemons` |
| CA 分发 | `labacacia/NPS-Dev/tools/nip-ca-server` | `labacacia/NIP-CA-Server` |
| Ledger/Cloud CA 分发 | NPS-Dev daemon source | 按需同步 `labacacia/NPS-Ledger`、`labacacia/NPS-Cloud-CA` |
| 公共发布文档 | NPS-Dev/NPS | `labacacia/NPS-Wiki` |
| 跨仓治理 | ChangeControl EPIC-004 | 产品 issue/PR 链接该 Epic |

## 11. 产品 Issue 映射

| 跟踪 Issue | 清单范围 |
|---|---|
| [#91](https://github.com/labacacia/NPS-Dev/issues/91) | P19-0 主清单与基线冻结 |
| [#92](https://github.com/labacacia/NPS-Dev/issues/92) | NCP：P19-P01、P19-P06（NCP 部分）、P19-S01 |
| [#93](https://github.com/labacacia/NPS-Dev/issues/93) | NDP：P19-P02、P19-S01、P19-S03 |
| [#94](https://github.com/labacacia/NPS-Dev/issues/94) | NOP：P19-P03、P19-S01 |
| [#95](https://github.com/labacacia/NPS-Dev/issues/95) | NWP/NIP：P19-P04、P19-P05、P19-S01、P19-T01 |
| [#96](https://github.com/labacacia/NPS-Dev/issues/96) | Daemon：P19-D01–P19-D07、P19-C04、P19-T04 |
| [#97](https://github.com/labacacia/NPS-Dev/issues/97) | 合规：P19-S01、P19-C01–P19-C04、P19-T02、P19-T05 |
| [#98](https://github.com/labacacia/NPS-Dev/issues/98) | 发布／文档：P19-S02、P19-T02–P19-T06、P19-R01–P19-R05 |

同一 ID 在同时需要实现关闭证据与独立合规／文档关闭证据时可以进入多个 Issue。
#91 负责冻结清单的增改，分域 Issue 负责交付关闭证据。

## 12. P19-0 完成关口

P19-0 在以下条件全部满足时完成：

- 每一行都有稳定 ID、证据、owner、处置、关闭证据与下游范围；
- 每个实现项都进入 alpha.19 工作图与产品 issue 跟踪；
- 每个“证明属未来”项都有不冲突的边界和重新进入规则；
- EN/CN 清单结构与含义一致；
- alpha.19 路线图链接本清单，并把 P19-0 放在规范／runtime 实现之前；
- 后续发现追加新 ID，不得静默改写既有 ID 的含义。

仅凭本文件，任何 alpha.19 实现项都不能被判定为完成。
