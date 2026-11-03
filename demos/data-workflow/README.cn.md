[English](README.md) | 中文

# 有界订单/退款工具工作流

状态：D20-05 实验参考实现及测量验收为 COMPLETE。受控矩阵 80 个工作流全部通过精确答案、真实引用和资源检查；
实际进程树保守峰值 435.71484375 MiB，低于原 512 MiB 上限。
[受控观察及限制](../../docs/benchmarks/data-workflow-agent-controlled-observation-2026-11-03.cn.md)。
首轮两次模型失败及 558 MiB 资源失败全部保留。生产、全 SDK 和发布资格仍需单独验收。

支持 100、1,000、100,000 条合成订单，通过创建不可变结果、有界读取、COUNT/SUM 聚合和释放四种操作完成分析。
普通 HTTP 命名 JSON 端点与 NPS 使用同一个公开 SDK 操作引擎、数据源、身份、资源策略和进程内结果存储；NPS
另外执行肯定式发现和独立的网关/接收端认证链路。普通 HTTP 同样支持外部计算，不被限制为全量原始数据输入。

公开 Python 客户端使用 mTLS，服务端把证书映射到当前 NIP Ident，经合成 CA/撤销存储核验。网关剥离外来投影
头，为请求签发新投影，使用独立 TLS 身份连接接收端；接收端验证签名、当前身份/资源策略和 SQLite 持久防重放。
独立操作者证书仅允许一次源更新，绝不是模型第五种工具。这是临时、固定信任的本地测试配置，**不是生产 PKI
入网、签发或轮换实现**。

使用声明的公开 SDK 包；alpha.20 开发快照尚未发布，已发布的 alpha.19 不含这些新 API。采用 Codebase per-demo
契约 `9d4010702e14213a8749aee3a7c0af0ceaa81478`，增量加入现有平铺 demos，不重生成根目录或改变仓库边界。

## 安装

需要 .NET 10 SDK、Python 3.11+ 和 NuGet/PyPI 依赖访问。以下命令均在本 demo 目录执行，不全局安装包。

```bash
python3 -m venv .demo-output/venv
.demo-output/venv/bin/python -m pip install --upgrade pip
```

系统缺少 ensurepip 时，可安装系统 venv 支持，或先用 `python3 -m venv --without-pip` 创建，再通过已有托管 pip 的
`--python <venv-python>` 安装至该环境；不要关闭 TLS 验证。

仅在规范开发仓库 NPS-Dev 内，从已提交且干净的 SDK 源码构建未发布包：

```bash
.demo-output/venv/bin/python build-packages.py
```

输出 `.demo-output/packages/<source-prefix>/source.json`、四个 NuGet 包、符号和 Python wheel，记录源码、版本及
文件哈希；不发布、不改规范版本、不调用模型。后续构建使用带修订标识的开发版本，避免包缓存串用。SDK 尚未
还原依赖时，先用其配置的源还原 SDK solution。独立分发直接附上已构建的包目录，运行端不依赖私有源码路径。

将输出目录（或分发附带的 `packages`）设为 `PACKAGE_DIR`：

```bash
PACKAGE_DIR=.demo-output/packages/<source-prefix>
.demo-output/venv/bin/python -m pip install "$PACKAGE_DIR"/nps_lib-*.whl
SDK_VERSION=$(.demo-output/venv/bin/python -c 'import json,sys; print(json.load(open(sys.argv[1]))["nuget_version"])' "$PACKAGE_DIR/source.json")
dotnet restore server/NPS.Demo.DataWorkflow.csproj --source "$PACKAGE_DIR" --source https://api.nuget.org/v3/index.json -p:NpsDemoSdkVersion="$SDK_VERSION"
dotnet build server/NPS.Demo.DataWorkflow.csproj -c Release --no-restore -p:NpsDemoSdkVersion="$SDK_VERSION"
.demo-output/venv/bin/python check.py --packages "$PACKAGE_DIR"
```

预期三个 PASS 和新的 `.demo-output/check-*/receipt.json`。检查会启动真实 HTTPS/mTLS 服务，以冻结的 D20-01
答案验证两条路径、五条实际读出的订单证据、创建重放、快照/schema 冲突、释放、当前授权及一次 1% 数据/schema
更新。旧快照及同键重放保持原值，新结果拥有新快照和 schema。负向检查包含普通用户越权更新、操作者越权调用
公开结果路径、直接绕过网关和重复 JSON 键；失败以非零退出。`--rows 100` 可缩短诊断。

收据记录 SDK 来源、程序集和 demo 源文件哈希及合成数据结果，不调用模型。额外工具守卫测试：

```bash
.demo-output/venv/bin/python -m pip install pytest pytest-asyncio
.demo-output/venv/bin/python -m pytest tests -q -o asyncio_default_fixture_loop_scope=function
```

手工运行时选 `.demo-output` 内一个全新绝对路径：

```bash
dotnet server/bin/Release/net10.0/NPS.Demo.DataWorkflow.dll --output "$PWD/.demo-output/manual-01" --rows 100000
```

仅输出连接配置路径；客户端使用其中 owner CA/证书/密钥。普通 HTTP 可向 `ordinary/create` POST
`{"params":{"source_id":"orders"},"idempotency_key":"<UUID-v4>"}`；NPS 使用已安装的 Python
`ResultHttpClient`。Ctrl-C 停止；证书一小时有效，仅用于该运行实例，重启不迁移结果。

## 范围、限制和清理

源为 `orders`，可选过滤仅支持 `refunded:true`、`period:baseline|current`、
`reason:delivery|quality|duplicate|other`。源查询 fields/order 扩展、URL、SQL、脚本在捕获前拒绝；读取可投影字段。
TTL 1–900 秒，默认 300，结果仅进程内保留。操作者更新给前 1% 行的金额增加 100 最小货币单位并加入 nullable
`note`，改变数据版本而不改变资源策略版本。

`WorkflowTools` 限制最多四次结果分配、16 次调用、每次读五行、累计读 20 行、累计输出 32 KiB；只接受实际返回
的 descriptor，用已执行的聚合做精确整数异常判断，拒绝伪造引用。释放失败和未知分配结果会保留供清理/显式同键
恢复，不隐式重试或换键分配。[模型 Agent](LIVE.cn.md) 已实现并以离线供应商 fixture 加真实 mTLS 工具验证，真实矩阵已完成，全部失败样本保留。

证书/私钥、SQLite、日志、环境、包及结果仅存于 Git 忽略的 `.demo-output/`。Unix 运行目录仅所有者可访问，私钥
0600。先停止自身服务，再仅删除选中的生成目录。检查显式释放已知结果；失败/取消不代表回滚。共享模型费用
账本绝不属于 demo 清理范围，不删除、不重置。不需要客户数据或供应商密钥。

现有 MessagePack 3.1.7 的 NU1902 漏洞告警仍未豁免。本地参考 demo 不代表所有 SDK、生产证书生命周期、公开
部署、原生结果绑定或所有传输已合格。不预设模型费用收益；D20-04 测得原生传输劣于匹配的 HTTP gzip。

[实现与剩余验收项](../../docs/plans/2026-11-02-data-workflow-demo-implementation.md)。

[受控封闭证据包](evidence/live-tool-agent-bounded-v2-126c5df/index.json)附实际测量脚本、全部模型响应/工具轨迹及脱敏账本。
无需密钥、SDK、私有原账本或网络即可分别复核两轮完整矩阵：

```bash
python3 summarize.py --packet evidence/live-tool-agent-bounded-v2-126c5df
python3 summarize.py --packet evidence/live-tool-agent-afafebd
```

检查文件/脚本哈希、付费观察、usage、费用、答案、实际读取引用及更新边界；受控包另检查原 CPU/堆/RSS/期限。
同一个 USD10 原账本保留全部 1,118 次尝试及历史 USD3.20 未知预留；保守累计 USD4.3990379，剩余 USD5.6009621，非账单。
不再执行任一已完成矩阵。当前故障路径修复在测量证据封存后完成，详情见
[范围决策](../../docs/adr/2026-11-03-data-workflow-agent-scope.md)。
