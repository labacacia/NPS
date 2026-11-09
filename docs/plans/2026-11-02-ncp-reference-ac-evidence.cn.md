[English](2026-11-02-ncp-reference-ac-evidence.md) | 中文

# D20-04 参考实现逐项 AC 证据

D20-04 #168 / Epic #163。状态：**COMPLETE，仅限下述实验性 .NET 参考范围**。
D20-00/D20-01 前置均 COMPLETE。这是 AC 判断，stacked 集成、六 SDK 资格与发布仍独立。

| AC | 结果 | 已执行证据 |
| --- | --- | --- |
| AC1 实际表达、schema 与协商修订 | COMPLETE | 原对象行 codec 实测保留字段名；显式 opt-in schema_batches=1 使用规范 JSON/MessagePack 元组、保序物化 schema/JCS SHA256、严格有界原始类型。106 固定 wire case 与10组独立 Unicode/JCS schema 直接执行。registry0.3为受限增量 successor，原12个 result predecessor 保留。 |
| AC2 anchor/schema/base/序号与恢复 | COMPLETE | 缺失/过期/错误 hash、重复/缺口/乱序、重放、错误 epoch/base、到期、类型/null/原子 Diff、终止状态均通过。实际 baseline/update 保持旧快照不变；部分 EOF 后经重新授权、新完整 baseline/session 恢复。schema 变更绑定新 schema/epoch，不自动降级，不声称增量效率。 |
| AC3 实际有界传输与宿主来源权限 | COMPLETE | 22 个独立 pins mTLS/公开 publisher-reader 用例覆盖两编码、100/1000/100000行、精确 ID/总和、来源/消费拒绝、预取消、等待 credit 时取消/撤权、零窗口/暂停恢复/耗尽/伪造/重放/缺失 credit、超时、超大来源行、EOF/恢复。010781b 的212个组合回归通过。回调绑定真实 peer/source，权限由可信宿主拥有。 |
| AC4 等价压缩/替代格式与资源 | COMPLETE | 方法及修正在实测42a0b05之前登记。72条件各30采样+3预热全部通过独立来源/schema/逐行、双向TLS以下字节、宿主CPU/分配及完整实际进程树RSS检查。峰值/上界320.12109375MiB，低于512MiB，4CPU/320MiB托管/120s策略不变。24个原生对同编码gzip配对在全部4指标中位数上均更高；小数据/变更负结果及失败轮次完整保留。 |
| AC5 双语契约与基线兼容 | COMPLETE | 双语profile、选中后的规范编码、协商恢复、实测文档齐全；未选中codec及默认关闭offer不变。result freeze、106-case manifest 字节不变。实测源码 feature/version/contract、完整.NET1597/1597及CI41542003619通过；更广兼容cell仍属D20-08。 |
| AC6 广泛移植前的真实结论与决策 | COMPLETE | ADR保留实验性参考语义，不宣传优于压缩HTTP、通用内存/增量/token收益。原始文件、收据、来源索引及全部分布已保存；独立校验拒绝混用、缺失、伪造或预算失败证据。默认offer、旧节省/安全/registry gate及发布边界不变。 |

## 可审查源码与复现

- #188/aef0d0c receiver；#189/ceda186 精确 mTLS/session 选择；#190/92f22aa 自有紧凑行；
  #191/a200679 registry successor；#192/864069e 规范 codec/可移植 case；#193/936c29a 严格 JCS；
  #194/010781b 实际有界 publisher/reader 与22流用例。
- #195/a868c02 初始采集；#196/fd4aa11 内存/配置修正；#197/775d2cf 共享不可变来源；
  #198/42a0b05 直接执行同一断言及流式 HTTP。
- 精确实测修订：`42a0b05865d4206b612cc55f2e80f2505cb6ea67`。
  测试程序集SHA256：`04045b765914280fad9c70f63b3d8db1a5ac23dfb2a80e7e3b6adf7dfc969b3a`。
- [实测及边界](../benchmarks/ncp-transfer-observation-2026-11-02.cn.md)、
  [方法](../benchmarks/ncp-transfer-method-2026-11-02.cn.md)、
  [完整证据包](../benchmarks/runs/2026-11-02-ncp-transfer-qualified.json)、
  [72行中位数/p95](../benchmarks/runs/2026-11-02-ncp-transfer-summary.csv)、
  [范围决策](../adr/2026-11-02-ncp-qualified-reference-scope.md)。

```sh
python3 tools/benchmarks/ncp-transfer-runtime.py --verify-packet docs/benchmarks/runs/2026-11-02-ncp-transfer-qualified.json
python3 -m unittest discover -s tools/benchmarks/tests -p 'test_ncp_transfer*.py'
python3 tools/conformance/check-schema-batch-vectors.py
python3 tools/conformance/check-data-workflow-contract.py
```

## 剩余套件义务

预配 pins/当前回调只证明一个.NET来源/消费者参考路径，不代表通用PKI/NIP delegation或任意生产权限。
只读NWP Action为夹具服务自有；原生result操作、恢复会话peer证明、NWP result subscription、实时schema/
delta效率及全部SDK公开API cell未在此获资格。资源实测只覆盖声明的5/6字段数据，不覆盖所有允许schema。
累计分配、逻辑元组字节和RSS不同。

D20-05..08、旧P20仍独立。依赖advisory及registry凭据清单仍失败、未豁免；不合并、不投影、不发布、不宣称
beta或父Epic完成。本工作未调用付费模型，也未改变共享预算账本。

下一步：agent进入已授权D20-05 #169，构建独立.NET/Python有界result/tool示例，按既有共享费用账本与普通HTTP
对照验证来源引用、drill-down和真实工具行为。本记录提交、canonical tracker对齐后，继续实现下一子任务无需重复授权。
