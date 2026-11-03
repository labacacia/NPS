[English](2026-11-03-nop-reference-ac-evidence.md) | 中文

# D20-06 参考验收证据

关联D20-06 #170 / Epic #163。状态：**实验性有界.NET参考AC为COMPLETE**；D20-05参考前置已完成。
不关闭套件集成，不宣称完整production_runtime，不授权发布。

| AC | 实际证据 | 状态 |
| --- | --- | --- |
| 授权查询/分析/报告 | orders/refunds -> analysis -> report四节点；Alice/Bob不同真实mTLS/当前NIP保护双段HTTPS；精确快照/字段/aggregate授权；Bob读行/释放被拒；所有者读五行及清理 | COMPLETE参考 |
| 结果输入/来源/归属 | 100/1000/100000各三次对独立冻结oracle精确计数/金额、实际行ID/来源；定位符不赋权，宿主映射工作者/动作并管理授权/清理；本地派发边界明确，无bulk/model上下文 | COMPLETE |
| 稳定身份/重试/期限 | 调用者/任务原子唯一键+不可变摘要，派发前持久UUID；运行中重复Running不冒终态，终态重复不派发/不改结果；仅not_dispatched可在原键及节点/任务期限内重试 | COMPLETE参考 |
| 未知/部分失败/取消/迟到 | 七种真实故障；终态丢失与分配后审计503均一派发后停下，不报告/重试/补偿，保留原未决键；回归含忽略取消、50次完成/取消竞态、坏终态及迟到不可改 | COMPLETE |
| 持久性/重启/容量 | SQLite单主机独占、显式初始化/重开、缺库/表失败、调用者/所有权/摘要隔离、终态CAS、重启未决不重放、256永久墓碑不驱逐、TTL隐藏；不承诺外部效果恰好一次或复制数据 | COMPLETE本地参考 |
| 资源/引用成本 | 预登记7c4d890完整16组kernel-v2通过实际4核/托管320MiB/512MiB/120秒；可信固定控制器当前映像+fixture生命周期对，最大275349504字节；双段TLS与本地JSON分开；模型0表示未调用；两轮v1失败保留 | COMPLETE测量范围 |
| 来源/复现/默认兼容 | 24脚本/oracle/依赖/程序集/后端/PID/原始数据/成本封闭；标准Python复核；EN/CN方法/API/观察/恢复；默认功能/旧路径不变，contractless空成员兼容通过；冻结/功能/规范版本不变 | COMPLETE参考 |
| 审查/验证 | 完整.NET1637后补旧兼容定向41项；CI20941576423537.NET1638及全部十项通过；demo94/工具14项；独立高风险审查问题修复回归，失败证据仍失败 | COMPLETE |

实现栈#206/105be801、#207/d4fbf5e、#208/ce2e483、#209/7c4d890；实际测量7c4d89006afa141c8576c8940a3b2cf4890983f6。
复核[观察](../benchmarks/nop-result-handoff-observation-2026-11-03.cn.md)、
[封闭索引](../benchmarks/runs/nop-handoff-kernel-7c4d890-attempt-03/index.json)、
[记账决策](../adr/2026-11-03-nop-reference-memory-accounting.md)及[实现记录](2026-11-03-nop-result-handoff-implementation.md)。
16组退出procfs缺口继续为诊断；独立完整wait4、根HWM及范围证据通过。可信不派生子进程是前提，不泛化任意进程树。

本路径callback/preflight/condition/K-of-N/补偿在派发前拒绝；远端NOP/原生/任意工作者树/共享副本/结果迁移/
完整production_runtime/六SDK及采纳者仍未验收。nps-runner/旧P20/安全/凭据/beta/发布义务不豁免；
持久任务历史不重建进程内快照/授权。无新模型调用；原USD10账本保守USD4.3990379含历史USD3.20未知，剩USD5.6009621，
非账单。未合并/投影/发布。下一步D20-07 #171发现/新鲜度/归属，再D20-08；#134发布独立。
