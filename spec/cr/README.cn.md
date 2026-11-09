[English Version](./README.md) | 中文版

# NPS Change Requests（CR）

**Change Request（CR）** 是 NPS 协议族在 **1.0 之前**阶段所使用的轻量级
设计文档，用于在落地代码之前（或之中）记录一次规范或实现变更的目的、
动机和形态。

## CR vs RFC

| 维度 | CR（本目录） | RFC（[../rfcs/](../rfcs/)） |
|---|---|---|
| 阶段 | 1.0 之前（alpha / beta） | 1.0 之后稳定期 |
| 准入门槛 | 无 —— 实现可直接推进，无需外部接受 | 必须 —— 在 `dev` PR 上 Accepted 后才能开始实现 |
| 评审者 | 作者 +（可选）一位独立评审者 | Shepherd + 社区讨论窗口 |
| 编号 | `NPS-CR-NNNN` | `NPS-RFC-NNNN` |
| 生命周期 | Draft → Implemented（或 Withdrawn） | Draft → Proposed → Accepted → Activated →（Superseded） |
| 状态保留 | 实现之后保留为历史档案 | 永久记录；通过新 RFC 被取代 |
| 范围 | 单次连贯变更，常跨越 spec + 实现 + 测试 | 同上 |

引入 CR 的原因：协议族还在 0.x、没有生产部署需要保护时，完整 RFC 流程
（指派 shepherd、正式讨论窗口、合入时正式 Accepted 仪式）带来的摩擦超过
其收益。一旦协议族进入 v1.0，所有影响规范的变更**必须**改用
[../rfcs/](../rfcs/) 下的 RFC 流程，并遵循该目录文档中记录的过程。

## CR 文档结构

一份 CR 文档至少包含：

1. **摘要** —— 一段说明变更内容。
2. **动机** —— 为什么现在需要这次变更。
3. **规范改动** —— 给出 `spec/` 文件的确切文本或 diff。
4. **SDK 改动** —— 按语言列出影响。
5. **合规改动** —— 新增或修改的测试项。
6. **迁移影响** —— 内部和外部用户。
7. **范围之外** —— 明确不在本 CR 内的事项，防止 scope creep。
8. **接受标准** —— 具体的完成清单。
9. **CHANGELOG 条目** —— 建议文案。
10. **开放问题** —— 作者希望评审者投票的事项。

结构和正式 RFC 足够接近，必要时可以原样升级为 RFC（例如某条 CR 在 0.x
末尾还没实现，而此时 v1.0 已经发布）。

## 索引

| CR | 标题 | 目标版本 | 状态 |
|---|---|---|---|
| [NPS-CR-0017](./NPS-CR-0017-schema-batches.md) | 有界schema批次及差量恢复 | v1.0.0-alpha.20 | Draft |
| [NPS-CR-0016](./NPS-CR-0016-data-results.md) | 版本绑定的数据结果资源 | v1.0.0-alpha.20 | Draft |
| [NPS-CR-0015](./NPS-CR-0015-governed-challenge.cn.md) | 受治理操作 challenge | v1.0.0-alpha.20 | Draft |
| [NPS-CR-0014](./NPS-CR-0014-production-runtime.cn.md) | 有界生产运行时 | v1.0.0-alpha.20 | Draft |
| [NPS-CR-0013](./NPS-CR-0013-invocation-context.cn.md) | 认证调用上下文 | v1.0.0-alpha.20 | Draft |
| [NPS-CR-0012](./NPS-CR-0012-feature-negotiation.cn.md) | Native 握手与 HTTP 准入的 feature revision 协商 | v1.0.0-alpha.20 | Draft |
| [NPS-CR-0001](./NPS-CR-0001-anchor-bridge-split.md) | 把 Gateway Node 拆为 Anchor Node + Bridge Node | v1.0-alpha.3 | Implemented（2026-04-26）|
| [NPS-CR-0002](./NPS-CR-0002-anchor-topology-queries.md) | Anchor Node 标准拓扑查询类型（`topology.snapshot` / `topology.stream`） | v1.0-alpha.4 | Implemented（2026-04-27）|
| [NPS-CR-0003](./NPS-CR-0003-orchestrator-group-session-nids.md) | 编排器 group NID 与短期 session NID | v1.0-alpha.6 | Implemented（2026-05-28）|
| [NPS-CR-0004](./NPS-CR-0004-pen-wirein.md) | 写入 IANA 已分配的 PEN 65715（替换临时 `1.3.6.1.4.1.99999`） | v1.0-alpha.6 | Implemented（2026-05-08）|
| [NPS-CR-0005](./NPS-CR-0005-nip-ca-ra-model.md) | NIP CA RA 注册授权模型（三层准入） | v1.0-alpha.7 | Implemented |
| [NPS-CR-0006](./NPS-CR-0006-subscribe-frame.md) | SubscribeFrame（NWP §13）正式规范 | v1.0.0-alpha.11 | Implemented（2026-05-28）|
| [NPS-CR-0007](./NPS-CR-0007-nop-l3-runtime-integration.md) | NOP ↔ Node Profile L3 runtime 集成 | v1.0.0-alpha.13 | Implemented（2026-06-13）|
| [NPS-CR-0008](./NPS-CR-0008-tier3-binary-vector.md) | Tier-3 BinaryVector v1 编码 | v1.0-alpha.14 | Implemented（2026-06-26）|
| [NPS-CR-0009](./NPS-CR-0009-multi-anchor-ha.md) | 多 Anchor 高可用（`anchor_failover` / `anchor_quorum_lost`、`cluster_epoch` 栅栏、NDP 解析规则） | v1.0.0-alpha.17 | Implemented |
| [NPS-CR-0010](./NPS-CR-0010-bridge-bidirectional.md) | Bridge Node 是双向的 —— 修正"仅出向"的自相矛盾，新增 `bridge_inbound_protocols` 方向声明，拆分 §16 合规 profile，把 `compat/*-ingress` 合并进 Bridge 包 | v1.0.0-alpha.17 | Implemented |
| [NPS-CR-0011](./NPS-CR-0011-stateful-llm-context.cn.md) | 有状态 LLM context 与增量 completion —— owner-bound 不透明 context、CAS 版本、原子提交、生命周期 action、发现与可测复用 | v1.0.0-alpha.18 | Implemented |

## 起草新 CR 的方法

最新草案：[NPS-CR-0012：Feature Revision 协商](./NPS-CR-0012-feature-negotiation.cn.md)
—— 目标 v1.0.0-alpha.20；Draft；讨论 NPS-Dev #124。

1. 从上面的索引选下一个可用 `NNNN` 编号。
2. 照已有 CR（比如 CR-0001）的结构起草 —— 不存在正式模板文件，结构
   有意保持灵活。
3. 写英文 (`NPS-CR-NNNN-<slug>.md`) 即可。CR 不强制要求中文双份 ——
   它们是 1.0 之前的非正式规划文档；真正落地变更的 `spec/*.md` 会按
   项目惯例提供英中双份。
4. 在上方索引表加一行。
5. 切 `feat/cr-NNNN-<slug>` 分支开始实现。
