[English](result-oracle-observation-2026-11-02.md) | 中文

# 通过认证结果操作验证冻结退款数据集

日期：2026-11-02。范围：D20-02/D20-03 reference 工作流，确定性运行时。
依据：[冻结方法](../../spec/conformance/alpha20/data_workflow_methodology.json)。

`FrozenRefundWorkflowMatchesExactOracleThroughSupportedPublicClient` 对冻结的
100、1000、100000 行数据，依次经过受支持 `ResultHttpClient`、实际公开 mTLS、
新网关投影和独立校验的接收端。来源为按记录 ID 稳定排序的不可变种子数据，schema
anchor 由现有 `AnchorFrameCache` 计算。fixture 只注册 `filter: {refunded: true}`
这一来源查询子集，不注册生产来源，也不扩展通用查询语言。

工作流分别捕获所有订单和退款订单，对前者按原因/时期取得精确 COUNT，对后者取得
COUNT/Int64 SUM，再读一页有界退款明细。全部整数及每种原因前五个当期退款 ID 都须
与冻结 oracle 相等。异常判定使用精确有理数，三个规模都识别出 delivery，展示舍入
不会改变结果。

100000 行的断言值为：

| 原因 | 基期订单数 / 退款数 / 退款金额 | 当期订单数 / 退款数 / 退款金额 |
| --- | --- | --- |
| delivery | 13000 / 3000 / 3444000 | 12000 / 7000 / 23576000 |
| quality | 13000 / 3000 / 3555000 | 12000 / 0 / 0 |
| duplicate | 12000 / 2000 / 2296000 | 13000 / 0 / 0 |
| other | 12000 / 2000 / 2370000 | 13000 / 0 / 0 |

金额均为整数最小货币单位。delivery 的证据 ID 为 `order:000052`、`order:000056`、
`order:000060`、`order:000064`、`order:000068`。从实际聚合响应、最多五行和 result/
snapshot/record 引用组装的模型边界证据不超过 32768 UTF-8 字节，完整来源不发给模型。
两次捕获具有不同 snapshot、相同 schema。把一个 snapshot 替换进另一个结果的读取，
会得到规范 HTTP 409 snapshot-mismatch 错误。最终显式释放两个结果，来源捕获恰为两次。

执行：

```sh
dotnet test impl/dotnet/tests/NPS.Tests/NPS.Tests.csproj --filter FullyQualifiedName~FrozenRefundWorkflowMatchesExactOracle
```

这三个正确性执行不等于 30 次传输实验或五次真实模型实验。没有发起模型调用，也没有
据此宣称 token/费用节省、时延/RSS 分布、C/D 传输收益、原生/通用数值或六 SDK 合格。
证据仅说明：真实有界 reference 路径可以在外部模型出站前精确算出答案。
原有安全/发布门槛和共享 API 预算均保留。
