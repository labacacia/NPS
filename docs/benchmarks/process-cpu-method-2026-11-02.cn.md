[English](process-cpu-method-2026-11-02.md) | 中文

# 进程CPU观察扩展预注册

日期：2026-11-02。D20-01 #165 / Epic #163。先提交方法和计数代码，再运行。
1860c19矩阵已通过其声明的正确性/RSS/时间/证据门槛，但未采集CPU时间；配置核数和
线程数不是CPU消耗，历史记录继续保持此项未知。

重跑同样完整144配置，每项测量30次、预热3次。来源变更、算子、C/D顺序、连接/压缩
状态、4CPU亲和性、托管320MiB，以及RSS512MiB/120秒/32768字节门槛均保持。
不增加操作、供应商调用、性能阈值或强制GC。

每个工作流在既有计时开始前读取当前测试进程
[`Process.TotalProcessorTime`](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.totalprocessortime?view=net-10.0)；工作流与
冷前端关闭完成后刷新计数，记录差值毫秒。保留30个CPU差值及中位/最近秩p95/最小/
最大，与相同30次延迟/TLS/RSS观察对应；预热CPU样本同其他预热指标一起丢弃。
CPU采样包含计数器查询开销，不扣除；既有墙钟计时边界不改，不用墙钟推算CPU。

计数覆盖共宿主前端、网关/服务端、密码学/SQLite和GC线程的用户态/内核态CPU，
不含其他进程或外部模型供应商；异步/后台工作可能跨采样窗口。并行CPU时间可能超过
墙钟，操作系统计数精度可能使短工作流出现0；观察到的0不等于缺失。这里只比较部署/
路径，不代表原生NCP专属CPU或通用生产利用率。

144项观察各保存精确边界字符串与30个有限非负值。结构schema为旧记录保留可选CPU
字段，当前实验driver则强制要求。语义guard拒绝缺失/次数不足/布尔/负数/非有限值及
篡改归因；摘要对历史缺失值保持null，绝不补0。仍须18项真实TRX、独立来源/oracle/
引用及其余未改门槛全部通过，才可称CPU实验合格。

仍用现有driver，选择新的绝对输出目录：

```sh
python3 tools/benchmarks/data-workflow-changes.py --execute --output <new-absolute-observation-directory>
```

新提交、来源/方法/schema hash、原始失败或样本与分布独立保留，不覆盖或重命名以前
成功/失败记录。共享供应商账本及10美元预算不变。

实际[3b624e4观察](process-cpu-observation-2026-11-02.cn.md)已通过18项、144配置及原有
门槛，CPU样本齐全；历史CPU仍未知，原失败资源实验仍为失败。
