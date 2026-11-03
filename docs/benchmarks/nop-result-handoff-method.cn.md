[English](nop-result-handoff-method.md) | 中文

# NOP 结果交接参考验收方法

关联 D20-06 #170 / Epic #163。本方法验收有界参考工作流，不比较效率，不测供应商费用。首次捕获前提交完整执行器、
fixture、工具及[固定方法](nop-result-handoff-method.json)；测量期间不改源码、不重叠本地构建/测试。
失败及不完整尝试分别保留，不覆盖。

四节点为 orders/refunds -> analysis -> report。两个宿主绑定角色使用不同真实 mTLS/当前 NIP 身份调用数据网关/
接收端。Alice 拥有快照，仅给 Bob 精确快照/字段/aggregate 权限；报告读取五行，Alice 撤销授权并释放已知结果。
角色执行派发在本地，不验收远端 NOP/AlignStream 传输。创建后的审计503真实触发客户端 OutcomeUnknown；
原创建键/参数保留未决，由所有者单独决定恢复。清理不自动重放该 mutation，也不声称回滚。

共16组：100/1000/100000行各三次成功流程，另在100行运行七种故障各一次：缺少授权、撤销授权、字段不足、
撤销工作者、输入过期、查询终态丢失、实际分配后审计响应失败。冻结的独立 D20-01 oracle 核对精确计数/金额；
失败/未知输入不能进入报告。每组复投同任务，核对无新增派发且父结果不可改。另有执行器故障套件覆盖重启、
数据库/表丢失、取消/迟到结果、容量/保留与畸形帧。

每组启动新的直接断言 .NET fixture 进程，不经过 VSTest 父进程。控制器固定实际四核 affinity、
DOTNET_PROCESSOR_COUNT4、托管堆320MiB、实际控制器及全部存活子进程512MiB RSS/保守VmHWM合计、全组120秒。
10ms采样，失败保持；证据绑定子进程实际 OS affinity、后端 CPU/GC 字节及 PID。不强制GC、不重置池。
新进程是明确的功能验收条件，不作为暖服务性能数据。

分别记录：本地实际序列化的 Delegate/AlignStream JSON 字节（不是远端NOP线缆字节）；公共客户端/网关和网关/
接收端两段 TCP 流字节（含HTTP/TLS、拒绝及清理调用，不含TCP/IP包头）；fixture墙钟/CPU/分配；整个实际进程树
资源检查；源码/程序集/依赖哈希。确定性计划没有模型请求，模型可见字节记0，不推算token、缓存、模型延迟、账单
或节省比例。逐故障核对派发/分配/handler/释放/未决次数、错误及确定性，不据成功宣称普遍可靠或生产就绪。

在干净的主开发仓库先提交/预登记，再执行：

```bash
dotnet build impl/dotnet/tests/NPS.Tests/NPS.Tests.csproj -c Release --no-restore
python3 tools/benchmarks/nop-result-handoff.py --execute /absolute/fresh/owned/capture-directory
python3 tools/benchmarks/nop-result-handoff.py --packet /absolute/fresh/owned/capture-directory
```

输出必须为全新绝对目录，放在跟踪源码外。工具检查源码干净及程序集/依赖漂移，捕获实际进程，保留类型日志与失败
记录后封闭证据。仅需标准Python即可复核，无SDK、供应商密钥、私有原账本或网络。封闭包包含原测量脚本、冻结
oracle、实际依赖清单、原始观察、执行回执、资源记录和哈希索引；不导出临时私钥、凭据、服务配置或SQLite文件。
哈希是完整性/复现记录，不是密码学签署证明。

16组及各故障精确断言全部通过后，本参考资源AC才为COMPLETE。失败尝试及原因始终保留。原生/远端/六SDK/完整
生产运行时、安全/凭据/P20与发布资格单独验收。共享DeepSeek记账不变，不需要新模型调用。

## 首轮保留记录及 procfs 修正

来源d4fbf5e的16组功能断言全部通过，但一次100行子进程退出附近的资源计数读取失败，因此整轮仍为失败，未获资源
资格。[完整封闭包](runs/nop-handoff-d4fbf5e-attempt-01/index.json)保留全部16组、后端/程序集/资源/成本记录及
失败检查。最大保守高水位274300928字节虽低于512MiB，不能抵消计数缺失。PR207正常CI41571306303十项均通过。

500个短命子进程的独立诊断复现ESRCH及退出时VmRSS消失，立即重查仍有竞态。修正最多等待1ms再核验内核退出
状态，只接受确实终态/消失的子进程；根进程、权限或仍存活却缺计数的情况继续失败。七项回归覆盖这些区别，demo
94项通过。[诊断记录](runs/nop-counter-diagnosis-2026-11-03/attempt-01.json)包含不充分的立即重查方案，均不冒充
资格成绩。必须先提交新来源再完整重跑16组，不单独替换失败组，不降低上限。

只读复核已保留失败矩阵的完整性/功能：

```bash
python3 tools/benchmarks/nop-result-handoff.py --packet docs/benchmarks/runs/nop-handoff-d4fbf5e-attempt-01 --allow-resource-failures
```

命令明确返回 `passed:false` 及失败资源组，默认复核仍拒绝失败矩阵。无模型请求、账本变化或资源豁免。
D20-06仍为IN_PROGRESS，等待受控资源验收。

## 方法 v2：完整 fixture 生命周期记账

ce2e483的第二轮完整v1矩阵也通过16组功能断言，但一次1000行退出计数仍缺失。
[第二轮封闭失败包](runs/nop-handoff-ce2e483-attempt-02/index.json)继续保留失败。PR208正常CI41573317003九项通过，
含.NET1638和demo94项；fixture-oracle正确发现索引要求的类型日志因Git忽略规则未进入提交。下一增量只显式加入
逐条核对的单行完成日志，不放松封闭校验。

当前[固定方法](nop-result-handoff-method.json)为独立预登记的kernel-v2，NPS矩阵尚未执行。
[记账ADR](../adr/2026-11-03-nop-reference-memory-accounting.md)明确可信双进程/控制器当前程序映像边界、精确wait4
单一回收者、控制器必需VmHWM和fixture完整生命周期峰值、保守合计、可见的子进程procfs诊断缺口，以及不变的资源
门槛。不泛化任意子进程树。原始SELF使用量保留为诊断；控制器不重置、不扣除当前映像基线。

冻结fixture不创建子进程；观察到范围扩张或范围证据不可读均失败。14项标准Python检查覆盖真实子进程生命周期、
缺失/错误PID、零峰值、错误单位、范围扩张/不可读、原期限、根HWM缺失、子进程诊断缺口和继承的启动历史。
这些不是完整16组NPS矩阵的替代成绩。

默认复核继续拒绝两轮旧失败；显式--allow-resource-failures分别返回16组功能通过、passed:false。
新来源或记账边界不重分类旧成绩。
