[English](./NPS-CR-0014-production-runtime.md) | 中文版

# NPS-CR-0014：有界生产运行时

- 状态：Draft
- 目标：v1.0.0-alpha.20
- 日期：2026-09-09
- 作者：Codex
- 讨论：[#126](https://github.com/labacacia/NPS-Dev/issues/126)
- 追踪：EPIC-007、ADR-004 D20-04、A20-R009..R012

## 1. 摘要

规范集成：NCP §7.8、NDP §7.9、NOP §3.6 定义运行时职责、重新发现/准入及
显式执行确定性。Profile 仍为 experimental，SDK 和跨产品资格验证尚未完成。

在 NCP/NIP/NDP/NOP 基础上定义派发结果、重试、队列和 session 生命周期。
保留 nps.production_runtime revision 1，资格验证前为 experimental。
不替代已有 wire 定义。

## 2. 动机

Ivy 自建关联/队列/取消循环，Kernel 自建握手与重连。SDK 先提供对应公共能力再移除
产品实现。连接失败可重试不等于业务执行可以重放。

## 3. 规范提案

### 3.1 公共职责

六 SDK 以各自语言类型提供 SessionFactory（连接/协商/准入）、SessionPool（完整安全
key 和有界等待）、InvocationDispatcher（先注册关联再写入、单次本地终态）、
RetryDecision（依据派发确定性/操作契约/期限的纯决策）和 DrainController。
准入权限不能任意修改。保留底层 stream API，但绕过托管 dispatcher 时必须明确不具有
本 profile 保证。

### 3.2 状态与结果

Session：connecting -> negotiating -> admitting -> active -> draining -> closed。
建立失败释放资源；closed 不能回池。重连新建 session 并重新 NIP 准入。

Invocation：queued -> writing -> awaiting -> completed；
另有 rejected-before-dispatch、cancelled-before-dispatch、expired-before-dispatch、
indeterminate 终态。首次写入尝试中/之后的传输失败默认不确定，除非认证服务端明确证明
未派发；本地/TCP 已写入不能证明业务执行。有效关联远程终态才形成已知结果。
相同重复终态忽略，冲突终态关闭 session，不能再次调用应用完成回调。

Session 内禁止复用关联 ID。终态 tombstone 保留到 session 关闭；达到容量则 drain/
重连，不能驱逐后复用。未知关联 ID 关闭。取消后的迟到终态只能补诊断证据，不能改写
已返回的本地结果。

### 3.3 资源上限

默认连接/握手/准入各 5 秒，请求总期限 30 秒（含排队、写入、等待、重试），drain
30 秒。每完整 pool key 最多 8 个 session，全局 64 个、待 acquire 最多 256。
每 session 最多 32 个排队帧或 1 MiB payload，任一先到即限制；tombstone 默认 4096。
可配置其他有限正值，但不能超过协商的 payload/concurrency 上限。所有等待服从剩余
期限。心跳/idle 仍用 NCP §7.6/7.7，不新增矛盾协商。队列满在派发前拒绝。
流消费缓慢时使用现有 flow control；超协商窗口的 peer 使 session 失败。

Pool key 包含 endpoint 身份、subject、credential generation、assurance/scope 策略
指纹、features、encoding/security profile。不同租户/委托范围不能因 host/port 相同
而复用。撤销/轮换使相应池失效；全局限制同时约束 key 数。

### 3.4 重试

写入前确认未派发可在期限和预算内重试；服务端明确未派发的临时拒绝还需操作允许。
未知结果的只读操作仅在声明 replay-safe 且符合 freshness/cost 语义时重试。
未知 mutation 没有持久化去重证明则绝不自动重试；有证明时必须相同 principal/key/
digest、在保留期和许可目标范围内。已知远程终态不自动重放。

默认总共 2 次尝试。第 retry_index 次重试延迟均匀取
0..min(1000 ms,100 ms * 2^(retry_index-1))；注入时钟/随机源以测试。
延迟和服务端 hint 都消耗原总期限，超过剩余预算则终止。
重连成功不能证明前次未执行。跨 owner 重试必须共享持久化去重范围，否则 indeterminate。

### 3.5 取消与 drain

首次写入前取消阻止派发；之后只停止本地等待，并传播已协商的操作/NOP 取消机制，
不发明新 frame。无远程回滚/终态证据则结果仍不确定，不承诺副作用撤销。
Drain 拒绝新 acquire/操作，有界等待并请求受支持取消，最后关闭，
未决 mutation 报 indeterminate。取消、超时、close 竞争仍只交付一次本地完成。

### 3.6 故障转移与观测

断线重新解析 active Anchor，执行单调 cluster_epoch fencing，再协商和身份准入。
清理旧节点 route/capability 缓存；旧 owner 或 resumption ticket 不构成授权。
记录连接、协商、排队、尝试、重试、重连、取消、drain、failover 的计数/耗时、
结果确定性和有界关联 ID。禁止凭据/投影/body 日志，以及 subject/scope/key 的高基数标签。

### 3.7 错误

queue-full、deadline-before-dispatch、cancelled-before-dispatch、indeterminate、
correlation-conflict、session-closed 为 typed API 结果，不自动分配新 wire 错误。
保留原协议错误；操作确需新 wire 错误时另做 registry 变更。

## 4. SDK 改动

.NET/Python/TypeScript/Go/Java/Rust 实现相同职责、有界资源与结果确定性，
复用帧/身份原语。LLM 映射留在产品；Kernel 持久日志保留兼容 reader。

## 5. 合规场景

RUN-001：队列满不写入。
RUN-002：mutation 写入/响应丢失返回不确定，不盲重试。
RUN-003：同 key/digest 仅在持久保留证明下重试。
RUN-004：不同 principal/scope/credential generation 不共用 session。
RUN-005：取消/超时/响应竞争只完成一次。
RUN-006：重复终态忽略；冲突/未知关联关闭。
RUN-007：tombstone 满触发 drain，不不安全驱逐。
RUN-008：建立失败释放全部资源。
RUN-009：jitter/hint 消耗原期限和尝试预算。
RUN-010：慢消费者下维持流/窗口/字节限制。
RUN-011：drain 拒新工作，如实报告未决 mutation。
RUN-012：旧 Anchor/新 epoch 要求重新解析准入。
RUN-013：无共享去重不能跨 owner 重放未知 mutation。
RUN-014：日志/指标有界且无凭据/投影。
RUN-015：未协商 profile 保留基础行为。

## 6. 迁移

先发布公共运行时和向量，再逐步收敛 Ivy/BKE/Kernel。
保留底层路径的例外/回滚范围列入 P20-409。默认期限不是部署延迟保证。

## 7. 范围之外

产品事务、共识算法、通用 exactly-once、新心跳/取消帧、对所有异常自动重试。

## 8. 完成标准

- [ ] 双语规范、兼容/版本审查。
- [ ] RUN-001..015 确定性向量、虚拟时钟和派发计数。
- [ ] 六 SDK 及有界内存/队列证明。
- [ ] 跨产品取消、重连、drain、failover 验证。

## 9. CHANGELOG 提案

新增有界托管运行时，具有派发确定性、安全重试和重连/故障转移后重新准入。

## 10. 后续

规范落地时关联操作取消和持久去重契约；缺失时保守失败，不推断支持。
