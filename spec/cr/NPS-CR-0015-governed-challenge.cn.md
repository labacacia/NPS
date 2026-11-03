[English](./NPS-CR-0015-governed-challenge.md) | 中文版

# NPS-CR-0015：受治理操作 Challenge

- 状态：Draft
- 目标：v1.0.0-alpha.20
- 日期：2026-09-09
- 作者：Codex
- 讨论：[#127](https://github.com/labacacia/NPS-Dev/issues/127)
- 追踪：EPIC-007、ADR-004 D20-03、A20-R009/R013/R014
- 依赖：CR-0013 上下文绑定、CR-0014 结果语义

## 1. 摘要

规范集成：NWP §7.7 和固定 challenge Anchor 明确稳定 authority 命名空间、
精确保留期限、管理动作限制及 typed Caps 结果，取代下文未明确的记录/响应细节。
运行时、并发和恢复资格验证仍待完成。

可选 NWP profile 让逻辑 mutation 等待产品审批，再通过持久化操作去重准入。
Opaque handle 标识服务端状态，不是权限。保留 nps.governed_challenge revision 1，
experimental；不增加帧类型，不承诺通用 exactly-once。

## 2. 动机

Passport 的 hash、expiry、consumed/denied 状态提供设计输入，但单个状态不能证明外部
副作用只发生一次。客户端需统一 pending/terminal 语义，产品保留审批方法/UI/策略/存储。

## 3. 规范提案

### 3.1 初始操作和绑定

要求认证主体、逻辑 audience/target、action ID 和 1..128 UTF-8 字节非空幂等 key。
使用现有 ActionFrame。先验证参数和授权，缺 key 则不创建状态或副作用。

绑定 authority、subject、action、audience、冻结的已接受参数、操作 key、expiry、
选定 feature revisions。摘要遵循 CR-0013，即使 native 无 projection header 也一样。
Handle/proof 不纳入原始业务摘要，冻结参数不可变。

持久操作 key 是 (authority,subject,audience,action,idempotency_key)。
同 key 同摘要返回原 pending/terminal 操作；同 key 不同摘要冲突，不新建 challenge。
每次调用（包括取结果）重新授权。返回 handle 前，操作记录和 challenge 原子持久化。

### 3.2 响应

标准 Caps.data 承载已注册 Anchor schema 的 profile record，不能用任意 JSON 替代 Caps。
HTTP pending 可返回 202；native 使用相同 record：

| 字段 | 规则 |
|---|---|
| challenge_id | 32 随机字节的无填充 base64url |
| state | pending / approved / executing / completed / denied / expired / superseded / indeterminate |
| action_id | 原操作 ID |
| audience | 原逻辑目标 |
| request_digest | sha256: 加 64 小写 hex |
| expires_at | Unix 秒整数，限制执行准入 |
| methods | 最多 8 个唯一产品方法 ID，每项最多 64 ASCII 字节 |
| operation_id | opaque 服务端 ID，最多 128 字节 |
| result | 仅 completed 时携带原 action typed result |
| error | 有界操作/profile 错误，不含审批凭据 |

主体/authority 绑定存服务端，不泄漏私有策略。默认 TTL 300 秒，上限 900，服务端时钟
权威。终态后结果/去重至少保留 86400 秒并公布实际窗口；窗口外不自动重试。

### 3.3 协议动作

注册 nwp.challenge.status、nwp.challenge.resume、nwp.challenge.cancel，
使用现有 action discovery、ActionFrame、Caps。Params 仅含 challenge_id，
服务端读取冻结原操作。不传 OTP、审批 token、密码。要求原 subject/audience 和产品许可。

status 无副作用；resume 仅 approved 时，在重查当前 grant/revocation/scope、摘要、
expiry 后原子 claim approved -> executing。重复 resume 返回同一状态/结果，不重复执行。
cancel 原子地将 pending/approved -> denied；executing 后只报告现状，不声称回滚。
未知或错误 subject handle 返回同样的 not-found，知道 handle 不足以读取/审批/执行。

产品认证审批流程负责 pending -> approved/denied，不能改参数/绑定。
Superseded 结束旧 challenge；不同参数用新逻辑操作/key。
省略本 profile 不能绕过产品对受治理操作的审批要求。

### 3.4 状态和执行所有权

合法转移：pending -> approved/denied/expired/superseded；
approved -> executing/denied/expired/superseded；executing -> completed/indeterminate。
Completed 可保存已知成功或失败。审批本身不执行；expiry 限制 claim，不回滚 executing。
终态不可变，只有可信 reconciliation 可依据持久证据将 indeterminate -> completed，
不能自动重复 mutation。

Claim 与操作记录原子提交。副作用同库则与结果一并事务提交；外部系统则使用相同操作
身份的幂等 sink，否则崩溃后维持 indeterminate。一次 claim 不证明一次外部 effect。
并发、重启和 Anchor failover 共享操作存储/fencing；状态不可用则安全拒绝。

### 3.5 错误和限制

| 错误提案 | 状态 | HTTP |
|---|---|---|
| NWP-CHALLENGE-INVALID | NPS-CLIENT-BAD-FRAME | 400 |
| NWP-CHALLENGE-NOT-FOUND | NPS-CLIENT-NOT-FOUND | 404 |
| NWP-CHALLENGE-BINDING-CONFLICT | NPS-CLIENT-CONFLICT | 409 |
| NWP-CHALLENGE-NOT-APPROVED | NPS-CLIENT-CONFLICT | 409 |
| NWP-CHALLENGE-EXPIRED | NPS-CLIENT-CONFLICT | 409 |
| NWP-CHALLENGE-STATE-UNAVAILABLE | NPS-SERVER-UNAVAILABLE | 503 |

默认每 subject 最多 16、全局 4096 个 pending/approved。容量拒绝先于状态/副作用分配。
Status/resume/cancel 使用节点 rate policy。日志不得包含审批 proof、投影或业务参数。

## 4. SDK 改动

六 SDK 提供 typed state/result、status/resume/cancel、indeterminate。
自动 polling 受期限/TTL/rate hint 限制，不自动 approve。
产品 approval/persistence 注入，不复制 Passport UI。

## 5. 合规场景

CHL-001：同 key 同摘要返回同 handle。
CHL-002：同 key 不同参数无副作用冲突。
CHL-003：错误 subject/audience/authority 不能使用 handle。
CHL-004：并发 resume 只有一个持久 claim。
CHL-005：丢响应后取回持久结果。
CHL-006：外部效果不确定的崩溃不重执行。
CHL-007：claim 前过期拒绝，claim 后过期不表示回滚。
CHL-008：resume 前撤销/范围变化拒绝。
CHL-009：cancel/resume 竞争通过原子状态处理。
CHL-010：approval 不改冻结参数，handle 不授予权限。
CHL-011：共享存储/fence 防重启/failover 重复 claim。
CHL-012：存储不可用/配额满在 effect 前失败。
CHL-013：不支持 profile 的调用者不能绕过审批。
CHL-014：结果保留窗口外不自动重放。
CHL-015：状态/错误/日志不泄漏其他主体或审批秘密。
CHL-016：标准 Caps/schema 可被六 SDK 解码。

## 6. 迁移

Passport 先证明并发/恢复，再映射。保留产品 UI/通知。旧状态由明确 adapter 转换，
不能因为名字相同就宣称合规。

## 7. 范围之外

跨节点通用签名审批回执、UI/MFA 协议、事务实现、全部操作强制审批、通用 exactly-once。

## 8. 完成标准

- [ ] 双语 action/schema/error/feature registry。
- [ ] CHL-001..016 可执行向量与并发/崩溃测试。
- [ ] 六 SDK 和 Passport 参考流程。
- [ ] 持久保留、replica failover、reconciliation 证据。

## 9. CHANGELOG 提案

新增治理操作 handle 和状态/resume 语义，持久去重并显式报告不确定结果。

## 10. 后续

P20-108 冻结 Anchor schema/hash 和向量。产品必须证明事务或幂等 sink 策略，
才能宣称单次执行。
