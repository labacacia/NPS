[English](./NPS-CR-0012-feature-negotiation.md) | 中文版

# NPS-CR-0012：Feature Revision 协商

- **状态**：Draft
- **目标**：v1.0.0-alpha.20
- **日期**：2026-09-09
- **作者**：Codex（Epic 设计落地）
- **讨论**：[NPS-Dev #124](https://github.com/labacacia/NPS-Dev/issues/124)
**Epic**：[EPIC-007](https://github.com/innolotus/ChangeControl/issues/7)

## 1. 摘要

规范集成：NCP §2.6.3 / NWP §9.6 明确字节上限：原生特性握手的完整 payload
（含基础字段）不超过 16 KiB；HTTP 发现限制 profile 原始 JSON 值。
此规则取代下文历史提案中有歧义的独立块表述。完整 wire 向量仍待完成。

在 Hello (0x06)、握手 Caps (0x04) 和 NWM 中增加有界的 feature revision
声明与选择，不分配新帧类型。必需支持必须在派发前得到明确确认。
本文是具体规范提案，不修改已发布规范，也不表示 alpha.20 已实现。

## 2. 动机

包版本不能证明某项可选行为存在。旧节点可能忽略字段/header，产品适配器也可能
仅实现握手。因此依赖新特性的调用不能静默降级为旧操作。
追踪 ADR-004 D20-01、A20-R001..R004。

## 3. 规范改动提案

### 3.1 数据类型与限制

JSON 字段使用 snake_case。Feature ID 是最多 96 字节的 ASCII，匹配
`^[a-z][a-z0-9]*([.][a-z][a-z0-9_-]*)+$`。`nps.` 保留给公开 NPS profile，
示例使用 `example.`。每个 ID 有 1..16 个按 revision 升序排列的描述符；
revision 是 1..65535 的 uint16，不能是布尔、小数或零。
每个 revision 的 lifecycle 必须是 experimental、stable、deprecated 之一；
数字大小不自动表示语义兼容。

- `feature_offer`：最多 32 项，形如
  `{"id":"example.echo","revisions":[{"revision":1,"lifecycle":"stable"}]}`。
- `required_features`：最多 32 个已出现在 offer 中的 ID。
- `selected_features`：最多 32 项，形如 `{"id":"example.echo","revision":1}`。

一次交互最多 32 个 ID。重复 ID/revision/JSON key、未知对象成员、错误类型、
不支持的协商版本均无效。协商块 UTF-8 JSON 不超过 16 KiB，同时受帧/请求上限
约束，取较小值。缺失可选数组视为空。

experimental revision 必须双方本地显式启用；接收方不能根据对端声称的 lifecycle
放宽本地策略。deprecated 也须本地策略允许。stable 语义不能不兼容地改用。
各 revision 的完整语义和跨特性约束由 feature registry 定义。

### 3.2 Native 模式

Hello 可增加 `feature_negotiation: 1`、`feature_offer`、`required_features`。
有 offer/required 却无版本字段属于无效。

服务端校验 offer，将客户端 revision 与本地已启用集合取交集，每个共有 ID 选择
最高的双方合格 revision。本地策略先过滤资格，再选择。必需 ID 必须同时出现在
offer 和结果中；未知必需 ID 或空交集在握手阶段拒绝。未知可选 ID 忽略。
选定组合不满足跨特性约束时直接拒绝，不静默改变确定性选择算法。

握手 Caps 返回 `feature_negotiation: 1` 和 `selected_features`，无选择也返回
显式空数组。客户端拒绝重复、未提出、不符合本地策略的选择或缺失必需项。
这些字段仅用于握手，不能通过业务 Caps 重新协商活动连接。

旧 Caps 无确认时，客户端视选择集合为空。有必需项则关闭且不派发；无必需项可
继续原有基础行为。扩展握手被拒绝后，仅在本地策略允许且不携带必需特性或业务请求
时，才可建立新的基础连接。不得重放已派发操作。

选择绑定当前已准入 session。重连/故障转移必须重新协商与身份准入。
Feature 不授予身份、能力、scope 或 assurance。应用 API 必须在写入请求前检查
所需 revision。活动特性不能静默撤销；应停止准入、drain/关闭并重新协商。

例如 offer 提出 revision [1,2]，服务端支持 [1,3]，只能选择 1。
完整 Hello/Caps JSON 示例见英文版 §3.2。

### 3.3 HTTP 模式

NWM 增加可选
`profiles.feature_negotiation = {"version":1,"feature_offer":[...]}`。
Offer 变化时递增现有 manifest version。声明覆盖服务入口；更窄的 action 支持
必须在产生副作用前由 handler 再校验。发现结果不能替代准入。

使用某特性语义的请求携带单个 ASCII header `X-NPS-Required-Features`：
按 ID 升序的逗号分隔 `id=revision`；禁止空白、重复、前导零及重复 header。
最多 32 对、4096 字节。即使发现时是可选项，一旦操作使用其语义，该请求就必须
要求这个精确 revision。网关必须拒绝歧义的重复 header。

入口在 handler 执行前检查语法、当前启用 revision、跨特性依赖、具体操作支持和
认证准入策略。不支持或已撤销的特性在派发前拒绝。成功响应通过
`X-NPS-Selected-Features` 回显接受集合；失败请求不声明选择成功。
成功响应缺失/错误回显属于客户端协议错误，操作结果不确定，不允许据此重试 mutation。

客户端发送依赖特性的操作前，必须经服务该操作的认证路由取得明确的 version-1 声明。
声明该 profile 的路由必须保证所有可能后端都执行规则，或由入口先拒绝不合格请求；
混合新旧后端不能在缺乏入口保证时声明支持。入口只向兼容目标派发。
禁止重定向到未确认路由或回退旧入口。无法确认路由保证时，本地失败且不发送操作。
旧节点忽略 header 不是安全回退，事后检查回显也无法阻止已发生副作用。

未使用 profile 的调用省略 header，保持基础行为。理解 profile 的服务端对基础调用
中的错误 feature header 仍须拒绝。网关每一跳分别验证，不能把支持声明当授权。
P20-102 的上下文投影请求摘要须绑定这些 feature requirements。

### 3.4 错误提案

以下需加入 error-codes.md，并非已有发布错误：

| 错误 | 状态 | HTTP | 场景 |
|---|---|---|---|
| NCP-FEATURE-NEGOTIATION-INVALID | NPS-CLIENT-BAD-FRAME | 400 | 畸形/超限 offer、版本、重复或无效选择 |
| NCP-FEATURE-REQUIRED-UNSUPPORTED | NPS-CLIENT-CONFLICT | 409 | 必需 native 支持缺失或无合格交集 |
| NWP-FEATURE-REQUIREMENTS-INVALID | NPS-CLIENT-BAD-FRAME | 400 | HTTP 必需项 header 无效 |
| NWP-FEATURE-REQUIRED-UNSUPPORTED | NPS-CLIENT-CONFLICT | 409 | 当前操作不支持精确 revision |

旧节点不能发出新错误时客户端提供同名本地错误。详情只含有界 ID/revision，
不含凭据或业务正文。无法解析到协商块时，已有传输/帧尺寸错误可优先返回。

## 4. SDK 改动

.NET、Python、TypeScript、Go、Java、Rust 均实现相同的类型、确定性选择、
握手校验、HTTP 准入及本地派发检查。Python 特别禁止 bool 作为整数，
TypeScript 校验整数和 uint16；所有语言都在重连时清空选择状态。
保持未使用特性时的旧行为，不能把忽略字段当作支持。

P20-105/107/108/109 负责版本、双语规范、向量和兼容矩阵；
P20-201..208 负责实现与一致性证明。

## 5. 合规场景

| ID | 场景 | 结果 |
|---|---|---|
| FTR-001 | [1,2] 与 [1,3] | 选择 1 |
| FTR-002 | 未知可选 ID | 省略并保持基础行为 |
| FTR-003 | 必需 ID 无交集 | 派发前拒绝 |
| FTR-004 | 旧 Caps 无确认 | 有必需项关闭，否则仅基础行为 |
| FTR-005 | 选择未提出 revision | 业务写入前拒绝 |
| FTR-006 | 重复、布尔、小数、零、溢出 | 无效 |
| FTR-007 | experimental 缺任一方本地启用 | 不合格 |
| FTR-008 | HTTP 发现后撤销支持 | handler 前拒绝 |
| FTR-009 | 旧 HTTP 目标或不安全混合路由 | 客户端/入口派发前阻止 |
| FTR-010 | 派发后成功响应无回显 | 协议错误，不自动重试 mutation |
| FTR-011 | 重连、故障转移、不同网关跳 | 重新选择及准入 |
| FTR-012 | 超限、重复 header、前导零 | 无效 |
| FTR-013 | alpha.19↔alpha.20 基础调用 | 保持现有行为 |
| FTR-014 | 非法特性组合 | 派发前拒绝 |
| FTR-015 | 伪造支持或 stable 标记 | 不扩大权限/策略 |
| FTR-016 | 必需 ID 未出现在 offer | 无效 |

这些是规范场景，尚非可执行覆盖；P20-108 生成共享 wire fixtures 与派发计数断言。

## 6. 迁移影响

新增可选字段仍需证明旧 decoder 兼容。区分基础回退与依赖新特性的调用。
HTTP 运维必须验证入口及后端路由后才可声明强制执行。包升级不自动启用特性。
无 P20-105 破坏性证据时，保留旧默认和最低兼容版本。

## 7. 范围之外

特性自身的 runtime/context/challenge 行为、beta 发布、新帧类型、NIP 身份格式修改、
将协商视为密码学身份认证。

## 8. 完成标准

- [ ] 双语 NCP/NWP 规范、错误及版本落地（P20-105/107）。
- [ ] Registry 审查真实 ID/revision 和约束。
- [ ] FTR-001..016 共享可执行向量冻结（P20-108）。
- [ ] 六 SDK 与兼容矩阵通过（P20-201..208/109）。
- [ ] 证明 HTTP 混合后端强制执行与 native 派发前拒绝。

P20-101 负责起草 CR；实现门槛满足前保持 Draft。

## 9. CHANGELOG 提案

增加 native 握手与 HTTP 准入的显式 feature revision 协商，有界元数据、
必需支持安全拒绝以及旧基础模式兼容。

## 10. 后续实现决策

当前 wire 提案可供具体评审。真实 feature ID 由各 profile CR 分配；
完整编码向量与发布版本冻结留在 P20-105/108。按仓库 alpha CR 流程，
不需要额外的外部 RFC 接受仪式。
