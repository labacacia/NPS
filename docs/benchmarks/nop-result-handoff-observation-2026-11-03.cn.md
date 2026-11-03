[English](nop-result-handoff-observation-2026-11-03.md) | 中文

# NOP 授权结果交接观察 — 2026-11-03

kernel-v2全部16组通过：100/1000/100000行各三次成功流程，加七种预期故障。成功DAG核对冻结oracle精确分组、
五条实际读取退款行及两次所有者释放。固定参考最大保守合计275349504字节，即262.59375MiB，低于原512MiB。
这是参考语义/资源验收，不是普遍可靠、效率或NPS节省结论。

来源7c4d89006afa141c8576c8940a3b2cf4890983f6（[PR209](https://github.com/labacacia/NPS-Dev/pull/209)）在捕获前提交。
[封闭包](runs/nop-handoff-kernel-7c4d890-attempt-03/index.json)绑定24份脚本、冻结oracle、工具链/程序集/依赖、
原始观察、CPU/堆/affinity/PID、内核记录及成本。CI41576423537十项通过，含.NET1638、demo94、工具14项。

四节点为Alice的orders/refunds -> Bob的analysis -> Alice的report。不同真实mTLS/当前NIP身份保护两段数据请求；
Bob只获精确快照/字段/aggregate授权，读行/释放被拒绝；Alice读五行、撤销授权并释放已知结果。角色派发在本地，
不验收远端NOP。[方法](nop-result-handoff-method.cn.md)保持实际4核、335544320GC字节、512MiB、120秒及10ms。
[记账ADR](../adr/2026-11-03-nop-reference-memory-accounting.md)要求完整当前控制器映像VmHWM与精确fixture生命周期
wait4峰值，取更高实测HWM保守合计；不扣基线、不exec/clear_refs、不强制GC/重置池。继承SELF历史保留诊断。

冻结代码不创建子进程，全线程扫描只见固定对；这是可信拓扑前提，不证明任意短命fork。16组均保留退出时fixture
procfs KeyError，诊断不完整；v2独立完整根/内核/范围证据通过，不重分类旧v1失败。

| 行数 | Delegate JSON字节 | Align JSON字节 | 公共TLS字节 | 网关/接收端TLS字节 | 墙钟ms | fixture CPUms | 托管分配字节 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 100 | 4636 | 5388 | 36427 | 83884 | 1676.0594 | 1579.885 | 16631824 |
| 1000 | 4659 | 5422 | 36451 | 83907 | 1681.3120 | 1603.411 | 18653928 |
| 100000 | 4705 | 5490 | 36494 | 83956 | 3489.6045 | 3676.724 | 238259640 |

每规模三次新进程中位数，含配置/启动/操作/清理，不作为暖服务或性能比较。本地JSON不等于远端NOP流量；真实TLS
含双向HTTP/TLS、拒绝与清理，不含TCP/IP包头。模型可见0表示未调用模型，不推算token/费用/节省。
[逐组CSV](runs/2026-11-03-nop-handoff-kernel-cases.csv)保留全部。

派发/捕获/handler/释放/未决次数：缺授权与撤销授权均3/2/4/2/0；隐藏金额3/2/5/2/0；撤销工作者3/2/4/2/0；
过期3/2/2/0/0；查询终态丢失1/1/2/1/0；真实分配后审计503为1/1/1/0/1。前五组为known_terminal失败，后两组
为indeterminate/NOP-EXECUTION-INDETERMINATE；均不执行报告。未知mutation不重试/补偿，保留原UUID/query/TTL由
所有者单独决定；清理不承诺回滚，不把未决改成成功。同任务复投零派发且保留首结果。回归另覆盖调用者/摘要冲突、
稳定重试键/原期限、坏终态、忽略取消/迟到效果、取消竞态、重启、账本/表丢失、CAS、容量与过期。

[首轮](runs/nop-handoff-d4fbf5e-attempt-01/index.json)及[第二轮](runs/nop-handoff-ce2e483-attempt-02/index.json)
v1继续资源失败，各16组功能通过、一次退出计数缺失；诊断/脚本不可变。

~~~bash
python3 tools/benchmarks/nop-result-handoff.py --packet docs/benchmarks/runs/nop-handoff-kernel-7c4d890-attempt-03
~~~

标准Python验证哈希、控制/PID/内核公式、完整矩阵、oracle/行来源、精确故障及成本，无SDK、密钥、原账本或网络，
不重跑实验。D20-06[参考AC](../plans/2026-11-03-nop-reference-ac-evidence.cn.md)为COMPLETE；默认功能、不支持的
callback/补偿/K-of-N/condition/preflight/原生/远端/完整生产运行时/六SDK路径不变。快照/授权进程内且固定发行者；
任务SQLite不复制数据。采纳者/安全/凭据/P20/beta/发布门槛仍在，#134发布独立；未合并/投影/发布。

D06无供应商调用或费用。共享DeepSeek保守累计仍USD4.3990379含历史USD3.20未知，原USD10剩USD5.6009621；上界非账单。
下一步D20-07发现/归属，再D20-08集成。
