[English](ncp-transfer-observation-2026-11-02.md) | 中文

# 原生 schema-batch 与流式 HTTP 实测

2026-11-02；D20-04 #168，Epic #163。实测源码
`42a0b05865d4206b612cc55f2e80f2505cb6ea67`，Release/Linux x64/.NET 10.0.14。

与相同编码的 HTTP gzip 基线相比，原生 .NET 参考实现的延迟、进程 CPU、分配量、TLS 流字节
中位数在 **24 个配对条件中全部更高**，包含小数据和 schema 变化。完整 72 条件均通过来源、
schema、逐行和资源检查：2,160 次正式工作流、216 次丢弃预热，每组 30 次采样。
完整实际进程树的观测峰值与存活进程高水位之和上界均为 **320.12109375 MiB**，低于原定
512 MiB。没有模型/API 调用；模型 usage、模型可见字节仍为未知。

## 完整传输对照

[预登记方法及各轮修正](ncp-transfer-method-2026-11-02.cn.md)覆盖 6 种实际 mTLS 路径、
100/1,000/100,000 行、冷/热连接、原始/schema 变更任务。每行 ID、原始值、金额总和及 schema
均与独立 D20-01 oracle 一致。变更任务传两份完整快照，检查旧快照不变、新 schema 与新 epoch；
这不测量增量节省。

十万行、热连接、原始 schema：

| 路径 | 延迟中位数 / p95 ms | CPU 中位数 / p95 ms | 分配量中位数 MiB | TLS 流字节中位数 MiB |
| --- | ---: | ---: | ---: | ---: |
| HTTP JSON identity | 223.62 / 544.88 | 286.76 / 370.73 | 30.22 | 9.3672 |
| HTTP JSON gzip | 274.07 / 567.18 | 292.60 / 529.42 | 30.05 | 0.4002 |
| HTTP MessagePack identity | 124.55 / 169.18 | 157.46 / 242.75 | 24.18 | 7.0735 |
| HTTP MessagePack gzip | 107.41 / 123.04 | 151.41 / 224.41 | 24.09 | 0.3639 |
| 原生 NCP JSON 元组 | 540.65 / 586.55 | 745.30 / 823.83 | 187.31 | 4.6617 |
| 原生 NCP MessagePack 元组 | 594.39 / 622.00 | 849.48 / 929.67 | 239.14 | 3.3394 |

在这个单一条件中，相对同编码、未压缩 HTTP 对象行，元组传输字节分别减少 50.23% 和 52.79%。
但普通 gzip 在本数据上消除了更多重复结构，不能据此宣传优于压缩基线或节省模型计费。
MessagePack 对照是既有库的普通具名 map；未评测 Arrow/Flight/Protobuf。

小数据与完整 schema 重同步示例，均为热连接：

| 行数 / 任务 | HTTP JSON gzip / 原生 JSON 中位数 ms | HTTP MessagePack gzip / 原生 MessagePack 中位数 ms |
| --- | ---: | ---: |
| 100 / 原始 | 1.37 / 2.56 | 1.18 / 3.08 |
| 1,000 / 原始 | 6.07 / 22.92 | 4.47 / 27.02 |
| 100,000 / schema 变更 | 545.49 / 1076.09 | 260.46 / 1206.04 |

没有删掉条件或首轮采样。[完整 72 条件 CSV](runs/2026-11-02-ncp-transfer-summary.csv)、
[证据包及源码索引](runs/2026-11-02-ncp-transfer-qualified.json)和其链接的原始文件/执行收据
保留全部分布、字节及分配量。

## 边界与复现

计数覆盖同一个直接执行进程中的客户端/服务端，包括 TLS、请求/响应编码、原生投影和严格校验、
压缩、GC、客户端冷连接关闭。并行 CPU 可高于墙钟时间。累计托管分配、保留元组字节、RSS 是
不同量，不能当作某个协议的内存归因。TLS 之下双向计数包含 HTTP chunk framing、原生握手/
Anchor/Stream/credit 及 TLS record，不含 TCP/IP 包。热连接在计时外完整读取一次，再丢弃三次
预热；冷连接包括连接/准入。证书/服务/来源准备在工作流计时外，但仍计入进程树存活期间峰值。
不强制 GC、不重置池。

原生使用公开 session/publisher/reader 与当前宿主 peer/source 回调；HTTP 使用具名 DTO 解码器及
标准流式序列化器。结果属于实现/部署观察，无法隔离 codec 原因。预配 pins、完整新 mTLS 握手
不证明通用 NIP/PKI、恢复会话或 NWP result-stream 资格；未发明原生 gzip tier。

```sh
python3 tools/benchmarks/ncp-transfer-runtime.py --verify-packet docs/benchmarks/runs/2026-11-02-ncp-transfer-qualified.json
python3 -m unittest discover -s tools/benchmarks/tests -p 'test_ncp_transfer*.py'
# 检出精确实测源码并 restore .NET 10 依赖后，使用全新绝对输出目录
python3 tools/benchmarks/ncp-transfer-runtime.py /absolute/path/new-capture
python3 tools/benchmarks/ncp-transfer-runtime.py /absolute/path/new-capture --verify
```

直接入口执行同一组断言；正常 VSTest 在实测源码仍通过 1,597/1,597。完成收据绑定唯一声明条件、
实际程序集及精确原始文件哈希，不伪装 TRX 或通用 SDK 资格。完整实际进程树仍全部计入。

## 保留失败与决策

| 源码 | 完成条件 / 通过 / 失败 | 中断 / 未运行 | 观测失败 |
| --- | --- | --- | --- |
| a868c02 | 66 / 49 / 17 | 1 / 5 | 15 HTTP OOM；1 个原因未证实 HTTP 500；1 个原生变更 RSS 失败 |
| fd4aa11 | 57 / 53 / 4 | 1 / 14 | 2 个 OOM，2 个 RSS 失败 |
| 775d2cf | 65 / 59 / 6 | 1 / 6 | 6 个完成语义采样后的 RSS 失败 |

[首轮](runs/2026-11-02-ncp-transfer-first-attempt.json)、
[第二轮](runs/2026-11-02-ncp-transfer-second-attempt.json)、
[第三轮](runs/2026-11-02-ncp-transfer-third-attempt.json)保留完成/部分采样和中断/未运行状态。
每轮来源、缓冲、TCP、宿主修正均先记录再执行。宿主配置不同，旧 RSS 失败不重分类，跨轮差异
不归因于协议内存收益。未提交诊断属于探索，不混入本次合格采样。

[范围决策](../adr/2026-11-02-ncp-qualified-reference-scope.md)保留实验性有界参考实现，用于 schema/
epoch/base/credit/恢复语义，不宣传优于压缩传输或节省模型 token。D20-05 转入公开有界 result/tool
工作流；SDK 广泛资格仍在 D20-08。默认 feature offer、旧 30%/50% 义务、安全/registry gate、发布决策
均不变。[D20-04 逐项 AC 证据](../plans/2026-11-02-ncp-reference-ac-evidence.cn.md)。
