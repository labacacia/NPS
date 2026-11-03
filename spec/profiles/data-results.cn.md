[English](./data-results.md) | 中文版

# NWP 数据结果资源 — revision 1

- 状态：候选契约，experimental，默认关闭。
- 日期：2026-11-02；目标：alpha.20。
- 关联：[CR-0016](../cr/NPS-CR-0016-data-results.md)、[D20-00 #164](https://github.com/labacacia/NPS-Dev/issues/164)、#129。
- 精确机器契约：[data_result_contract.json](../conformance/alpha20/data_result_contract.json)。
- 本文冻结供实现使用的语义，不声明 SDK 或运行时已经可用。

## 1. 边界和准入

**DR-01.** 复用 ActionFrame/Params、NWM ActionSpec、CapsFrame 和既有 native/HTTP
binding，不新增帧、节点类型、endpoint 路径、身份算法或默认能力。
可选 feature 为 `nps.data_results` revision 1，experimental，两端必须本地启用。
以下所有操作必须精确协商该 revision，持有 `nwp:action` 和适用 node/action scope，
并经过资源策略。

Native 遵循 NCP 协商；HTTP 遵循 NWP §9.6 的全后端准入保证和精确成功 echo。
不支持/撤回的能力在零 handler 调用下拒绝；本 profile 操作缺少必需 feature 也须拒绝，
不能作为普通 action 放行。无关操作保留 alpha.19 行为，注册或发布不等于启用。
使用身份 projection 时，另要求 `nps.invocation_context=1` 及完整绑定。
直接通过 NIP 验证的调用者不强制使用 projection。

## 2. 结果与快照

**DR-02.** 结果是授权、有限、不可变的快照，绑定服务 issuer 和 principal。
描述符包含 `result_ref`、`issuer_nid`、`snapshot_id`、`schema_ref`、
`row_count`、`created_at`、`expires_at`、`retention="process"`。
时间为 UTC Unix 整数秒；随机不透明引用至少 128 bit 熵，不能充当凭据。
描述符是 CapsFrame 内的 profile payload，不能替代帧 envelope，
不能携带全量 rows、凭据或 bearer grant。描述符和记录数本身也需要授权。

快照标识绑定实际记录、稳定排序、源版本及 schema；schema hash 只标识结构。
Create 必须原子捕获一致的授权快照，否则失败；不支持一致捕获的源不得声称支持。
普通 NWP 分页不能证明已捕获一致快照。schema Anchor 复用既有 JCS 规则。

## 3. 操作

| Action ID | 必需参数 | 返回 |
| --- | --- | --- |
| `result.create` | 已公布 source ID 与既有 query/filter/fields/order 子集 | 仅描述符 |
| `result.read` | result_ref、snapshot_id、schema_ref、有界 limit；可选 fields/cursor | 描述符、有界有序行和 next cursor |
| `result.aggregate` | result_ref、snapshot_id、schema_ref、既有 aggregate/group_by | 有界聚合及来源描述 |
| `result.release` | result_ref | owner 授权下的幂等释放回执 |

**DR-03.** NWM 仅声明实际支持的 source 和计算子集。未知/不支持查询在访问源之前拒绝，
不得近似执行。不执行调用者提供的 URL、SQL 或脚本。Create 默认零预览。
Read 的 limit 为 1..1000，每页压缩前 canonical JSON UTF-8 不超过 65536 字节；
单行过大须明确失败，不能截断记录。schema/来源可单独读取，不强迫全量进入上下文。
聚合仅访问授权行和可见字段；分组、计数也受保护。金额采用整数最小单位或声明的精确
decimal，展示舍入不得改变 oracle 计数。未来派生结果保留来源、不晚于最早源过期、
不扩大权限。本 revision 不引入新查询语言。

## 4. 策略、委托与模型出口

**DR-04.** 身份来自已验证 native session 或 HTTP verifier，不信任 X-NWP-Agent、
模型声明、unsigned metadata 或复制来的 handle。租户/资源来自可信服务策略。
每个操作，包括元数据、重放回执与聚合，重新检查当前权限。

**DR-05.** 结果服务的资源策略负责授权，NIP 提供已验证主体、capability 和受限委托依据。
TrustFrame 是 CA 间信任，不是逐结果授权。Worker 除自己的已验证身份及 node/action scope，
还须有服务端保存、绑定接收者的资源 grant。操作/字段与 grantor 和源的实时策略取交集，
行条件只能增加限制，本 revision 不允许资源 grant 递归转授。
撤销 owner/源权限后，阻止既有/派生结果的后续读取，并在下一授权边界终止 stream；
已交付的缓冲数据不可撤回。源策略变化不能静默改写快照；不再兼容时拒绝读取。
策略/verifier 不可用须 fail closed。聚合/元数据错误不能泄露跨租户存在性。

**DR-06.** 向外部模型检查数据除 read 权限外，还须明确服务端出口决策；
模型请求不能替代此决策。默认 runtime-only、零预览。Bridge 不能丢失或伪造身份/租户，
使用 projection 必须另行协商并验证。审计保留主体、操作、结果和有界标识，
不记录全量数据、prompt、凭据或签名材料。

## 5. 保留、容量和重试

**DR-07.** 参考 profile 仅承诺进程内保留。TTL 默认 300 秒、最大 900 秒，
`now >= expires_at` 即过期，时钟回退不得延长剩余 TTL。
issuer 重启、未经证明的节点接替或释放后，结果不可用；不暗示持久化或跨节点恢复。
重新查询须显式产生新快照，不得静默重放。持久恢复需单独通过资格验证。

每 owner 最多 16 个 live 结果；每结果最多 100,000 行/32 MiB，总保留 payload 128 MiB。
行、描述符、cursor、去重状态须纳入记账或另有有界 overhead 额度。
公布前原子检查 owner/global 上限，容量不足拒绝，不提前逐出仍在承诺期的结果。
可清理过期状态；请求可降低而不能提高上限。

**DR-08.** Create 的幂等键和逻辑 digest 绑定 caller/issuer/action。
同进程、过期前相同键和内容返回原有授权描述符，内容不同返回冲突。
Release 在同一窗口按 owner 幂等。过期/重启不意味着允许重放外部变更；
本 revision 操作不执行外部业务写入。超时/取消可能已捕获并保留结果，应报告不确定
或通过同一键恢复，不能谎称未发生分配。

## 6. Cursor、增量与恢复

**DR-09.** Cursor 绑定 issuer、result、snapshot、schema、principal、policy version、
offset，验证绑定后才返回行。源更新不能把 cursor 切到 live/新快照；
更改字段/filter 使 cursor 失效，不能默默跳行/重复。Create 固定顺序，以稳定记录 ID
打破排序平局。错误 snapshot/schema/base 在授权后返回冲突并要求显式 rebase。
不支持的 delta 编码须拒绝，快照不是 live 订阅；NCP stream/delta 资格验证仍属 D20-04。

## 7. 错误与兼容

复用既有 ErrorFrame/NPS status，参数错误使用 `NWP-ACTION-PARAMS-INVALID`，
feature 错误沿用既有代码。以下六个 NWP-RESULT 错误在双语错误/状态表注册；
fixture 标签到精确 wire code 的映射见 data_result_contract.json。

| Fixture label | Wire error code | NPS status / HTTP |
| --- | --- | --- |
| INVALID_PARAMS | NWP-ACTION-PARAMS-INVALID | NPS-CLIENT-UNPROCESSABLE / 422 |
| UNAUTHENTICATED | NWP-RESULT-IDENTITY-REQUIRED | NPS-AUTH-UNAUTHENTICATED / 401 |
| RESULT_UNAVAILABLE | NWP-RESULT-NOT-FOUND | NPS-CLIENT-NOT-FOUND / 404 |
| SNAPSHOT_MISMATCH | NWP-RESULT-SNAPSHOT-MISMATCH | NPS-CLIENT-CONFLICT / 409 |
| CAPACITY | NWP-RESULT-CAPACITY | NPS-LIMIT-RESOURCE / 429 |
| POLICY_UNAVAILABLE | NWP-RESULT-POLICY-UNAVAILABLE | NPS-SERVER-UNAVAILABLE / 503 |
| KEY_CONFLICT | NWP-RESULT-REPLAY-CONFLICT | NPS-CLIENT-CONFLICT / 409 |
| FEATURE_UNSUPPORTED | NWP-FEATURE-REQUIRED-UNSUPPORTED | NPS-CLIENT-CONFLICT / 409 |

错误不包含记录值、其他租户占用或凭据，仅授权后允许返回快照不匹配详情。

**DR-10.** 实际 operation/binding 实现并验证前不得公告支持。
零派发声明必须有真实 client/server route 测试；契约 oracle 仅证明设计一致，
不能冒充运行时测试。新必需能力不支持即拒绝；可选、未选路径保留基线，
不能把引用解释成完整记录。

## 8. 验证与晋级

[共享场景](../conformance/alpha20/data_result_vectors.json)覆盖准入、身份/scope、
元数据、行列、过期、源/grant 撤销、委托、cursor/快照、进程丢失、容量和重放。
D20-02/03/04/08 必须在公共 API 及声明的 binding 执行同一不变量。
冻结的 JSON/UTF-8 和 oracle 只证明设计一致，不替代六 SDK、bridge、供应商或 failover。

[门禁台账](../conformance/alpha20/data_workflow_gates.json)保留 12 alpha、12 独立 beta 门禁、
57 个既有 P20 任务，不豁免任何一项。
[预注册方法](../conformance/alpha20/data_workflow_methodology.json)先固定任务与评分；
模型/供应商/预算未配置时阻止真实供应商运行，不能捏造测量。
