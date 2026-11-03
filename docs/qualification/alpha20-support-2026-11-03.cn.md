[English](alpha20-support-2026-11-03.md) | 中文

# alpha.20 当前支持范围与保留门槛

D20-08在已接受的有边界实验范围COMPLETE。已发布套件和规范包版本仍为 alpha.19；绑定源码的 alpha.20
开发包仅用于本地验证。导入 SDK 不会自动启用运行时 profile。[机器支持清单](2026-11-03-alpha20-support-ledger.json)
和[门槛核对](2026-11-03-alpha20-gate-reconciliation.json)补充原始12份冻结资产，
历史文件保持不变。

| 能力 | 已执行证据 | 边界 |
|---|---|---|
| 特性与错误辅助函数 | 六 SDK 的共享解析与错误映射测试 | 不代表完整协商、准入或传输/profile 验收 |
| 上下文字节与准入 | .NET/TypeScript 接收方函数；Python/Go 字节测试 | 仍需独立通道/策略及路由集成；Java/Rust 上下文一致性未验收 |
| 持久重放防护 | .NET/TypeScript 单机 SQLite 参考实现 | 不含多机共享存储、回滚证明或业务幂等 |
| 结果 HTTP | .NET/Python 公开客户端到 .NET 参考服务端 | 原生结果绑定及其他 SDK 结果路径不支持 |
| schema 批次 | .NET 显式启用的原生参考路径及成对测量 | 其他 SDK 不支持；已测传输劣于匹配的 HTTPgzip，不宣称普遍节省 |
| NOP 结果交接 | 四个本地 .NET worker 角色及故障/重启证据 | 不含远程或全 SDK 生产运行时 profile |
| NDP 结果路由 | 双节点 .NET 易失准入及每次更新的宿主授权检查 | 不含持久发现或完整生产运行时验收 |

[独立 SDK/包证据](runs/sdk-isolation-a9e4f05/README.md)记录 .NET SDK1538项测试、
Python1513项/92.13%覆盖率、TypeScript1324项、Go包测试、Java744项和Rust765项。
既有独立边界排除的128项 .NET daemon 测试另由完整开发仓库 CI 覆盖，不计为独立
SDK通过。npm/Python/NuGet/Java/Go 包消费者证据与源码测试分别记录。Rust 全候选
门槛及修正后的 CI 以当前包内记录为准，不推定通过。

用户2026-11-03接受[有边界实验收口](../adr/2026-11-03-alpha20-experimental-closeout.cn.md)
并指示本地CI。旧576项完整生产矩阵仍未执行，延期在133。原12alpha/57P20义务的
完整完成标记保持false，逐项明确当前适配/延期/发布处置。当前运行时验收使用
冻结七个配置，不用编解码数量代替。普遍30%CGN/50%线路实验阈值调整为精确正确性、
资源界限和公平实测，历史阈值不标成达成。Beta/采用方/完整生产仍在后续或单独
范围。156/157/158仍开放，仅单独的ResultHttpClient路径已局部验收。

[本地CI证据](runs/experimental-closeout-0c4bfdf/README.cn.md)已通过当前安全隐私/来源/回归/包
门槛，D08在该范围完成，不要求GitHub Actions或远端secrets。远端扫描正负结果保留为历史，不作为
当前执行依据。凭据文件复查仍未识别Cargo token，不阻止本地Rust验证。实际发布
权限、Maven签名、SourceLink、最终20版本/公开投影一致及发布留134。本地封包明确区分原完整执行和仅离线参数修正，不宣称ready-tag。
