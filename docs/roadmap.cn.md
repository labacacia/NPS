[English](roadmap.md) | 中文

# 路线图

最新已发布套件为 **1.0.0-alpha.20**，发布于 2026-11-10。本页分别记录已交付成果、待完成工程工作和待定发布事项。

## 已交付里程碑

| 里程碑 | 成果 |
| --- | --- |
| 协议基础 | 五个版本化协议，共用帧、错误与状态注册表 |
| SDK 生态 | .NET、Python、TypeScript、Java、Rust、Go 六语言包均发布 alpha.20 |
| 参考服务 | 四个公开 daemon 及单独部署的 NIP CA，容器镜像通过本地构建 |
| alpha.20 数据工作流实验 | 有界结果资源与授权、重复 schema 批次、本地任务交接与易失节点转移 |
| alpha.20 发布 | 注册表产物、GitHub/Gitee Release 与套件版本清单均已核验 |

已交付范围见 [alpha.20 Release](https://github.com/labacacia/NPS/releases/tag/v1.0.0-alpha.20)、[SDK 指南](sdks.cn.md)、[支持台账](qualification/alpha20-support-2026-11-03.cn.md)和[实验验收证据](plans/2026-11-03-alpha20-experimental-ac-evidence.cn.md)。

## 工程后续

| 方向 | 待完成工作 |
| --- | --- |
| 通用 NWP HTTP 客户端 | Python 的规范 MIME 协商与 capsule 解码、.NET 公共 HTTP 路径以及协议错误详情传递 |
| 六 SDK 数据工作流 | 对参考配置以外的操作、传输绑定和语言组合完成资格验证 |
| 生产运行时 | 完成生产级协商、调用上下文、有界运行时与受治理操作契约，并验证实现 |
| 编排与发现 | 验证远程任务交接、持久化发现/状态恢复及多节点故障行为 |
| 采用方集成 | 明确各产品负责人，并执行要求的集成与迁移资格验证 |

[已接受的范围决定](adr/2026-11-03-alpha20-experimental-closeout.cn.md)和[原门槛对账](qualification/2026-11-03-alpha20-gate-reconciliation.json)保留了未完成的生产义务。推进顺序从规范契约、SDK 和服务实现，进入产品采用与资格验证。

## beta 决策

beta 准备需要针对已发布 alpha.20 逐项复核门槛，再明确选择 beta.1、后续 alpha 或延长观察期。beta 的发布日期尚未确定。

适用门槛的复核属于实验交付后的后续工作，发布版本号本身不能代表生产矩阵已经完成。

## 设计探索

结果资源契约将持久化存储、多主机迁移和递归委托留给后续设计。schema 批次在结果流和订阅中的采用，也需要单独评估策略、绑定和价值。

这些边界见 [CR-0016](../spec/cr/NPS-CR-0016-data-results.md)和 [CR-0017](../spec/cr/NPS-CR-0017-schema-batches.md)。后续取舍继续使用已有 HTTP gzip 与普通工具对照测量作为依据。

## 项目边界

NPS-Dev 负责主开发与发布工具，NPS、六 SDK、NPS-Daemons 和 NIP-CA-Server 是套件发布投影。Model-Runtime、Console、Paper 具有独立生命周期，见[仓库拓扑决定](adr/2026-10-14-repository-topology.md)。

[项目首页](../README.cn.md) · [SDK](sdks.cn.md) · [发布历史](../CHANGELOG.cn.md)
