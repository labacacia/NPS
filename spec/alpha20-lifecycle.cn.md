[English](./alpha20-lifecycle.md) | 中文版

# Alpha.20 兼容与 beta 生命周期矩阵

- 状态：有边界实验参考配置已验收，完整生产/beta矩阵排后续
- 日期：2026-09-09
- 跟踪：P20-109、A20-R001/R003、[#129](https://github.com/labacacia/NPS-Dev/issues/129)
- 机器可读用例：[compatibility-matrix.json](./conformance/alpha20/compatibility-matrix.json)

## 版本边界

现行唯一版本依据仍为 [version-matrix.yaml](./version-matrix.yaml)。
下表候选契约目标已由规范集成变更写入唯一版本依据，不表示包已可用或运行时已验证。

| 契约 | alpha.19 | alpha.20 候选契约 |
|---|---|---|
| NCP | 0.12 | 0.13 |
| NWP | 0.22 | 0.23 |
| NIP | 0.15 | 0.16 |
| NDP | 0.13 | 0.14 |
| NOP | 0.10 | 0.11 |
| 帧注册表 | 0.15 | 0.15（无新帧类型或标志位分配） |

套件或包版本不证明特性支持。除非记录了破坏性需求及迁移决策，否则保持现有兼容下限。
NDP §7.9 明确运行时发现/故障转移证据，NOP §3.6 明确结果确定性及重试/取消绑定，
不是空版本升级。特性注册表版本为 0.3，错误/状态映射为 1.12/0.9。
评审及 CI 集成仍是 P20-105/107 的完成门槛。

当前适用范围：用户2026-11-03接受[有边界实验收口](../docs/adr/2026-11-03-alpha20-experimental-closeout.cn.md)。
下方576项定义作为延期的完整生产矩阵保留，不改为通过，也不作为本次实验的全SDK
运行时声明。当前验收为单独冻结七参考配置、六辅助契约/包门槛及必需的参考路径
安全/来源/本地CI。

## 双向行为

每行覆盖 .NET、Python、TypeScript、Go、Java、Rust 的全部 36 个有序客户端/服务端
组合，分别运行于原生和规范 HTTP 路径：共 576 个必测单元。
不可用绑定记录为 unsupported，不得记为通过或静默跳过。

| 用例 | 客户端 → 服务端 | 前提 | 预期行为 |
|---|---|---|---|
| CMP-01 | alpha.19 → alpha.19 | 未变更的基线请求 | 保留现有行为 |
| CMP-02 | alpha.19 → alpha.20 | 不含新特性字段 | 保留现有行为，不扩大权限 |
| CMP-03 | alpha.20 → alpha.19 | 新特性可选且存在安全旧路径 | 省略新语义，保留基线行为 |
| CMP-04 | alpha.20 → alpha.19 | 必需新特性 | 客户端在派发前拒绝，处理器调用为零 |
| CMP-05 | alpha.20 → alpha.20 | 已启用且精确相同的 revision | 选择并执行协商 revision |
| CMP-06 | alpha.20 → alpha.20 | 必需 revision 不匹配 | 派发前拒绝，处理器调用为零 |
| CMP-07 | alpha.20 → alpha.20 | 本地未启用 experimental revision | 不选择；若为必需则拒绝 |
| CMP-08 | alpha.20 → alpha.20 | 调用方伪造上下文或特性支持 | 不扩大权限，拒绝不可信投影 |

原生降级遵循 CR-0012：旧对端不必理解可选 offer 元数据。
HTTP 的已认证发现响应本身不够：发送必需特性请求之前，路由必须保证每个后端都执行
准入检查。含旧后端的混合部署不满足此前提。响应头不能追溯阻止副作用。
降级请求不得携带旧处理器可能误用的新上下文或 challenge 语义。

## 特性生命周期

| 表面 | alpha.20 生命周期 | Beta 承诺 |
|---|---|---|
| 未改变的既有协议行为 | 基线兼容目标 | 仅在 beta.1 清单列为 supported 时纳入 |
| 特性协商元数据 version 1 | CR-0012 提案，待验证 | 不因包版本自动晋级 |
| nps.invocation_context revision 1 | Experimental，显式启用 | 有证据并显式晋级前排除 |
| nps.production_runtime revision 1 | Experimental，显式启用 | 有证据并显式晋级前排除 |
| nps.governed_challenge revision 1 | Experimental，显式启用 | 有证据并显式晋级前排除 |
| nps.data_results revision 1 | [实验结果资源](profiles/data-results.cn.md)，运行时资格验证待完成 | 有证据并显式晋级前排除 |

受支持的 beta 表面，包括线字段、特性 ID/revision、错误语义和公共 SDK API，
仅做兼容新增，不移除、不不兼容地重用、不静默扩大权限。弃用不意味着 beta 期间移除。
Experimental revision 仍要求双方显式启用；远端 stable 标签不能覆盖本地策略。
语义破坏必须使用新 revision 并记录迁移决策，不能重解释同一 revision。
安全紧急变更必须公开 advisory 及遏制/迁移路径，不允许未记录的例外。

## 资格证据

每个已执行单元记录用例 ID、绑定、双方 SDK 与精确版本、fixture 哈希、环境、时间、
实际结果、处理器调用数和证据引用。CMP-04/06 必须证明零派发，而不只是客户端异常。
CMP-08 除拒绝外还须证明已准入 subject/scope/capabilities 未改变。
矩阵不预填任何测试结果。P20-501 负责执行；P20-508 的问题处置不能把
unsupported 或失败单元改记为通过。

Beta 仍以全部 BETA-G01..12、21 天干净间隔、剩余风险报告和单独发布决策为条件。
内部强制采用本身不是 beta 资格证据。本文不授权发布。

D20-08 当前证据见[支持清单](../docs/qualification/2026-11-03-alpha20-support-ledger.json)及[保留门槛核对](../docs/qualification/2026-11-03-alpha20-gate-reconciliation.json)。576个必需运行时单元仍未执行，解析器、编解码和回归测试不填充这些单元。当前清单仅补充原始12份冻结资产，不改写历史记录。
