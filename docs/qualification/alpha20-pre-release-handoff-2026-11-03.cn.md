[English](alpha20-pre-release-handoff-2026-11-03.md) | 中文

# D20-08 实验交接至 #134

用户2026-11-03接受[有边界收口决定](../adr/2026-11-03-alpha20-experimental-closeout.cn.md)
并选择本地CI，原范围选择阻挡已解除。七个冻结配置及当前本地安全/源码/包检查
已通过，D08在实验范围COMPLETE，见[AC证据](../plans/2026-11-03-alpha20-experimental-ac-evidence.cn.md)，本交接不宣称ready-to-tag。[机器交接](2026-11-03-alpha20-pre-release-handoff.json)、
[原ID处置](2026-11-03-alpha20-gate-reconciliation.json)、[实验矩阵](alpha20-experimental-matrix.json)。

当前必需证据覆盖.NET/Python结果HTTP、.NET批次双编码、本地NOP及两种易失NDP
节点转移。六SDK辅助函数/编解码/回归/包门槛单独记录。真实隐私/授权/外发/取消/
恢复负例、依赖安全、冻结夹具和源码/包可复现性仍必需。原576项生产单元仍未执行，
完整生产/采用方义务在129..133保留原ID。156/157/158通用客户端未验收，#156失败
回执继续保留，实验范围不掩盖失败。

[本地runner](../../tools/ci/README.cn.md)执行检查，验证Actions自动触发已关闭且不
调用Actions。GitHub secrets库存和Cargo认证不是本地CI前置条件。再次递归检查
用户提供的个人凭据文件，未识别到Cargo/crates.io字段/标记，标准Cargo token
环境变量为空。不记录或上传秘密，不为本地打包索要发布密钥；Rust本地包验证
无需token且已通过。

[保留的绑定源码包结果](runs/sdk-isolation-a9e4f05/results.json)仍属历史真实生产/
消费者证据，本地门槛验证日志、检查哈希及当前载荷源码完全一致，不冒称重算已
删除归档的字节。另在本地生成当前源码wheel/四NuGet示例包。新回执必须记录实际
源码及每个冻结参数变体，旧远端CI仅作为历史。实际注册表权限/命名空间、签名、
真实发布Git的SourceLink、最终20版本/公开同步和无未解释漂移仍是134前置要求。

公开版本仍为19，本地开发产物明确未发布。上次只读比较有60项spec/292项SDK差异，
旧数字不能冒充当前已干净。本地CI不reset或投影发布仓库；真实候选回执只在实际
已提交版本/源码/上游和必需发布检查一致后捕获，不改称19回执为20。发布/beta单独。

已授权实验D00..08范围内无剩余工作。下一独立轨道：134解决实际发布前置条件并
取得既有明确发布决定；完整生产/beta继续开放，本交接不授权发布或tag。
