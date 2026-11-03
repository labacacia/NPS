[English](2026-11-03-ndp-reference-ac-evidence.md) | 中文

# D20-07 参考验收证据

追踪：#171 / Epic #163。状态：**COMPLETE，仅限有界实验参考 AC**。[干净提交 CI41591866781](https://github.com/labacacia/NPS-Dev/actions/runs/41591866781) **10** 项全部 PASS，含六 SDK 回归、实际包安装 mTLS demo、oracle、CA/容器和 NativeAOT。本地全量 **1666/1666 PASS**，含 **28** 项新增用例；[绑定源码的执行摘要和首次失败](../qualification/2026-11-03-ndp-reference-tests.json)。实现：[#211](https://github.com/labacacia/NPS-Dev/pull/211)，`02443a3a4e6e096309b9c562de8ec1bc658b84b9`。
范围：显式选择的 .NET 易失路由准入及两个实际 HTTPS 结果节点，不代表生产运行时、
持久 daemon、原生传输或全部 SDK 已通过。

| AC | 实现及可观察验证 |
|---|---|
| 端点/能力发现 | NdpResultDiscovery 先独立解析当前宿主 NIP/公钥/集群/角色/epoch/端点授权，再验证有界 canonical 签名。能力仅作提示。 |
| 新鲜度/排空/回滚/脑裂 | NdpResultDiscoveryTests 覆盖签名/单调时钟失效、时钟回拨、未签名心跳不续租、draining 黏性、序号/摘要/epoch 栅栏、容量、同 epoch 冲突、不回退旧所有者、策略变更和冷启动 generation。 |
| 不支持的所需功能 | 先成功缓存正向 NWM，再以更高 manifest version 撤回支持；新增 POST/handler/capture 均为零。随后撤销当前 NIP，新增 receiver/provider/capture 为零。 |
| 切换重新准入 | 双节点分别验证不同 NID 及同 NID/epoch 更换端点。实际不同证书、客户端、NWM、receiver audience/projection/replay/store。旧绑定在 B POST 前拒绝；新 B 客户端实际使用选择的 TLS 指纹并验证当前 NIP/NWM。 |
| 所有权/状态不可用 | 同 NID 的空接替存储返回 NOT-FOUND，capture 为零；不同发行节点描述符在本地拒绝且未派发。已释放本地结果也保持 NOT-FOUND，不重建。这不是崩溃恢复或持久迁移证明。 |
| 显式恢复/不重放修改 | 发现组件不派发/重放操作。旧结果保留原准入绑定，不能静默绑定到 B；只有显式新查询才创建 B 状态。D05/D06 原始未知创建键/查询/TTL 规则及未知结果不被改变。 |
| 静态路径 | 既有双段认证公共 gateway 流程可独立运行；切换后静态配置的 A 客户端仍可在 A 接收端策略允许时读取/释放 A 的保留状态。 |
| 双语/范围真实性 | Discovery README、docs/samples/ndp-result-routing、NDP §7.9 参考说明及 ADR。默认行为、wire 版本和 registry daemon 不变。 |

夹具身份扩展默认保持旧 NID。最小的 SDK 支撑修复将本地无效描述符标记为“已知未派发”，
无效接收响应仍为未知；不同发行者切换案例执行验证。独立高风险审查覆盖身份、栅栏、
解析、传输及证据；已加强重复字段拒绝、每次操作前校验和缓存支持撤回测试。

首次本地全量失败：86 项宿主 inotify 配额错误，以及既有原生握手断言 539ms 超过
500ms。保留失败，不改阈值。重跑仅对本次命令设置 DOTNET_USE_POLLING_FILE_WATCHER=1
及 DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false，避免配置监听占用。这里仅作功能
验证，不提出资源、延迟、token 或费用结论。

无付费模型调用，原 USD10 账本不变。安全依赖、registry 凭据、旧 P20/beta/兼容/采用方
及发布门槛均未豁免。不合并、不投影、不发布；#134 独立。下一步：D07 证据通过后由
代理继续 D20-08 #172 的集成/支持/安全/候选审计。
