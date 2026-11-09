[English](./data-workflow-evidence.md) | 中文版

# alpha.20 数据处理证据

关联：D20-01 #165、Epic #163；D20-01基准/证据AC状态：COMPLETE。
堆叠集成、安全、SDK及发布门禁另行保持。
[方法和精确标准答案](../../spec/conformance/alpha20/data_workflow_methodology.json)
在资格验证前冻结。本页区分本地准备与另行执行的公共运行时/模型观察；实际执行见下文
对应日期的记录，准备工具本身仍不发起这些调用。

## 无凭据、无第三方依赖的本地运行

```bash
python3 tools/benchmarks/data-workflow-evidence.py --rows 100 1000 100000
python3 -m unittest discover -s tools/benchmarks/tests -p 'test_data_workflow_evidence.py' -v
```

工具不调用网络或供应商。A/B 表达同一五列合成数据，B 另包含一份 schema，
两者都保留全部记录。gzip 的 mtime 固定为 0；精确聚合及异常组与独立冻结 oracle 比较，
使用整数交叉乘法判定，不用展示舍入改变结果。

2026-11-02 在开发主机 Python 3.14.4 对十万行 fixture 的本地观测：

| 表达 | UTF-8 字节 | gzip 表达字节 |
| --- | ---: | ---: |
| A：对象 JSON | 9,758,001 | 297,133 |
| B：schema 加行数组 | 4,858,133 | 265,832 |

数据 SHA-256：`934291a490f2df764354ecfc3ca292e4a0573e35fcf7c74f462f19b8773e12dd`。
这是表达/压缩测量，不是完整 HTTP/NCP 网络字节、模型 token、上下文可容纳性、
公共 route 执行或供应商费用。不同 Python/zlib 可能改变 gzip 输出，比较时须记录环境；
一次准备耗时也不是预注册的 30 次传输性能实验。

## 缺失证据保持缺失

C（普通 HTTP 运行时）和 D（NPS 运行时）在无 capture 时为 NOT_EXECUTED。
wire bytes 和 provider usage 为 null，不能填零或用字节估算代替。
A/B 的供应商 usage 和上下文可容纳性也未知。工具不输出 token 节省百分比，
也不会把 fixture 比例称为 NCP 的额外价值。

```bash
python3 tools/benchmarks/data-workflow-evidence.py --rows 100 --require-runtime-captures
```

缺 capture 时输出 IN_PROGRESS，退出码为 2，不会静默通过，也不能用 mock 替代。
旧 CGN/context 数值保留为历史估算/模拟；发布阈值在明确门禁决策前继续保留。

## 导入采集证据

```bash
python3 tools/benchmarks/data-workflow-evidence.py --captures /absolute/path/run-captures.json --require-runtime-captures
```

文件为 C/D capture 数组，包含 arm、rows、fixture_sha256、与 oracle 完全匹配的
标准化 answer、明确 cold/warm、精确 model_visible_utf8、实际 wire_bytes、
30 个 latency_ms，以及 source.revision/binding/boundary/request_id/environment。
provider_usage 可缺失；有值时须明确 provider/model_id、正有限 spend_cap、
usage_mapping，计数为非负整数，未知类别保持 null。

Release HTTP runner 还记录每工作流 TLS 字节/压缩/RSS、构建/出站策略和全进程峰值。
[机器可读结构 schema](http-runtime-capture.schema.json) 允许保留 RSS 失败观察。
`data-workflow-runtime.py` 和 `data-workflow-changes.py` 另行检查来源/答案、完整矩阵、
成对文本和真实 TRX 通过情况；符合 schema 本身不证明真实性或合格。

同规模、同缓存 C/D 必须使用相同模型可见文本、环境、测量边界与供应商/模型/计量映射。
文本超过 32 KiB、答案或数据不同、重复 arm/cache、次数不足、布尔/负 token 数
或缺失来源都会拒绝，避免把 prompt/边界变化归给 NCP。

通过验证的输入仍是“外部提供的 capture”，不是工具现场认证的观测。
它不能证明文件真实性、TLS 记账或运行时权限执行，不能单靠文件满足六 SDK、
真实供应商或发布验收。公共 API/native/HTTP/安全测试仍须提供自身证据。
不推导供应商费率或费用。报告只输出文本 hash/长度，不输出原始模型文本或凭据；
生产 capture、API key、敏感 prompt 或客户数据不得进入 Git。

## 声明与证据映射及当前边界

另有显式启用的 [DeepSeek 执行脚本](../../tools/benchmarks/data-workflow-deepseek.py)，
在用户批准的合计 10 美元上限内记录真实 A/B 调用。[2026-11-02 实测](deepseek-ab-observation-2026-11-02.cn.md)
保留错误答案、上下文拒绝、真实 usage 和保守费用预留。原准备工具仍不调用模型，
不改变任何传输证据的适用范围。

| 声明或问题 | 对应证据与边界 |
| --- | --- |
| 对象 JSON 与 schema/rows 谁更小 | 上述表达/gzip 字节，不推导 tokenizer、计费或完整网络收益。 |
| A/B 真实模型任务 | [A/B 记录](deepseek-ab-observation-2026-11-02.cn.md)：20 次入场回复精确评分失败，10 次大输入拒绝，没有截断。 |
| 运行时精确计算与有界证据 | [冻结 oracle 执行](result-oracle-observation-2026-11-02.cn.md)、[39 项语义执行](result-invariant-observation-2026-11-02.cn.md)，明确合成身份限制和独立真实 mTLS 覆盖。 |
| 普通 HTTP 与 NPS 双跳运行时 | [a4d80af 实测](runtime-http-observation-2026-11-02.cn.md)：720 次测量/24 配置，此 NPS 拓扑延迟/字节更差；不能当成后来代码的测量。 |
| C/D 真实模型可见 token | [C/D 记录](deepseek-cd-observation-2026-11-02.cn.md)：30 次精确复现、C/D usage 相同，答案已经由运行时计算。不代表 D20-05 实时工具推理或 NCP token 收益。 |
| schema/更新行为与开销 | [1860c19 实测](schema-update-http-observation-2026-11-02.cn.md)：18/18 条件、144 配置、4320 次测量、487.16MiB 峰值满足声明预算，前两轮失败仍保留。测受控切换后的新快照；未采集 CPU 时间，不宣称切换/并发更新/delta 开销。 |
| 实际进程CPU消耗 | [3b624e4实测](process-cpu-observation-2026-11-02.cn.md)：另一次完整144配置/4320工作流，每项30个真实CPU样本、峰值477.59MiB；D的CPU中位有71/72组更高，例外保留。共宿主进程边界，不作协议专属归因。 |
| 原生 NCP、Arrow/Flight/Protobuf 效率 | 此 HTTP 比较明确排除；见[范围决策](../adr/2026-11-02-ncp-measurement-scope.md)。D20-04 在前置条件满足后完成自身验收。 |
| 供应商总费用 | 共享账本保存已知 usage 和未知 usage 的保守预留，不是账单；未知项阻止完整实付费用比。 |
| 整套就绪 | 仍须 D20-08、安全和发布门禁；结果引用夹具不证明六 SDK 或生产 PKI 合格。 |

模型轮次须有明确供应商/模型/预算；不能把 C/D 成功费用除以错误 A/B 的费用，称为等价
正确任务节省。历史 CGN/固定阈值仍由门禁台账协调。负面 HTTP 观察触发上述范围决策，
不事后调整验收阈值。

## D20-01 AC完成证据

| 验收项 | 已观察验证 |
| --- | --- |
| 表达/运行时/供应商/估算分开 | b42d079修正维护入口声明；本映射和JSON schema分开本地字节/CGN、实际TLS/RSS/CPU及供应商记录，不由字节推导计费。 |
| 四路径、同任务、完整上下文失败 | 原A/B、C/D保留各规模、模型/温度/输出配置及5次重复；A/B错误和上下文拒绝不截断成成功，C/D历史有界复现明确不代表实时工具推理。 |
| 可复现负载与精确答案 | 冻结种子/oracle及schema/更新预注册；1860c19、3b624e4各实际执行18来源条件/144配置/4320测量+432预热，独立生成器、整数答案/引用、旧快照及真实TRX全部通过。 |
| 完整可比指标/预算 | 冷/热前端、identity/gzip响应使用相同TLS流边界，保留30项延迟/字节/RSS及最新CPU；原120秒/512MiB/32KiB门槛通过，CPU含共宿主/GC/采样开销，历史缺失不倒填。 |
| 负面结果与无预定收益 | 两轮schema/更新失败及更早内存失败保留；最新72组墙钟/字节中位均普通HTTP更优，CPU有71组，例外保留。NCP范围ADR记录决策，HTTP声明明确排除原生/Arrow/Flight/Protobuf优越性。 |
| 完整供应商记账及限制 | 共享10美元账本保留全部尝试/探测和可用usage；成功/探测峰值费率上界0.1017384美元，加未知usage预留3.20，共保守计入3.3017384，不是账单或等价正确任务费用节省比。运行时/CPU实验无付费调用。 |
| 可审查证据与保留门禁 | 中英文方法/观察、原始记录、分布/schema已提交；39项离线guard、冻结39设计用例/12 alpha门禁/57旧任务及来源CI通过。旧固定阈值、原alpha/beta/安全/资格义务均保留。 |

完成的是测量包，不是D20-02/03、D20-04传输、D20-05实时Agent、六SDK或#134发布。
依赖/registry安全检查仍失败，没有合并或豁免；按权威issue依赖继续已授权Epic，
不把本证据检查点当作发布就绪。
