[English](2026-11-03-data-workflow-reference-ac-evidence.md) | 中文

# D20-05 参考实现验收证据

关联 D20-05 #169 / Epic #163。状态：**实验参考实现及测量 AC 为 COMPLETE**。
D20-00..04 已完成各自记录的前置范围。集成、所有 SDK/绑定、生产 PKI、安全/凭据/P20 与发布资格仍需单独验收。

| AC | 实际证据 | 结果 |
| --- | --- | --- |
| 数据源及精确分析 | 冻结 oracle；真实100/1000/100000行检查；模型100/100000行、基线/更新；受控两条路径各40/40精确答案及五条实际读取引用 | COMPLETE |
| 公开 SDK 与公平工具 | 安装 source109ea55 的公开 wheel/四个NuGet；真实双段mTLS/currentNIP/接收证明/SQLite防重放；普通HTTP共享同一provider/源/策略/存储及外部计算能力 | COMPLETE 参考范围 |
| 四类对照及准确结论 | D20-01全量JSON/schema+rows失败及有界普通/NPS证据保留；v1/v2完整模型工具矩阵分别保留；ADR接受语义，不宣称NPS独有token/费用/延迟优势 | COMPLETE |
| usage/cache/更新/失败 | v2全部80组/528次付费请求含40次预热；实际输入/输出/cache；预热请求哈希一致；旧快照不变、新边界有效；无自动重试 | COMPLETE |
| 资源包络 | 预登记126c5df完整v2：实际4核/托管320MiB/实际进程树512MiB/120秒；80组均通过；RSS451653632/HWM456880128字节，最长21.899014秒；v1及预检失败保留 | COMPLETE 测量范围 |
| 独立交付与复核 | 独立安装副本真实三规模mTLS检查通过；86项通过/1项私有源码比较按预期跳过；主副本87项通过；两个封闭包仅需标准Python，无SDK/密钥/原账本/网络 | COMPLETE |
| 故障审查及权限 | 测量封存后修复普通创建未知结果键丢失及跨namespace/历史预留结算；13项故障回归通过；认证/边界/清理/过期仍校验；引用本身不赋权 | COMPLETE |
| 预算及来源 | 同一个USD10原账本，1118次尝试保留；保守累计USD4.3990379含历史USD3.20未知预留，剩余USD5.6009621；v2全部12份脚本/包/程序集/进程/资源哈希封闭 | COMPLETE 记账 |

测量来源 `126c5df2ca069309c231b2ef6b5e58778f503b2a`
（[PR204](https://github.com/labacacia/NPS-Dev/pull/204)）在付费调用前提交。
后续故障修复不更改测量脚本、不重复付费请求。SDK 来源109ea55e11609645aa8524b36e0c33f55e5733fa。
CI200..204已通过；本次CI在关闭子项前记录于实现PR。

复核[受控观察](../benchmarks/data-workflow-agent-controlled-observation-2026-11-03.cn.md)、
[范围决策](../adr/2026-11-03-data-workflow-agent-scope.md)、
[实现记录](2026-11-02-data-workflow-demo-implementation.md)及
[封闭索引](../../demos/data-workflow/evidence/live-tool-agent-bounded-v2-126c5df/index.json)。
[README](../../demos/data-workflow/README.cn.md)提供两个证据包复核命令，无模型请求且不修改原记账。
不再执行任一完整矩阵；费用为保守上界，非账单。

下一步：D20-06 #170结果授权交接/执行结果，再推进D20-07/08。默认功能、冻结契约、独立仓库生命周期及发布边界不变。
NU1902、凭据/安全/P20义务不豁免；#134发布授权独立。未合并、未同步发布仓库、未发布。
