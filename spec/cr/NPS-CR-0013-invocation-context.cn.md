[English](./NPS-CR-0013-invocation-context.md) | 中文版

# NPS-CR-0013：认证调用上下文

- 状态：Draft
- 目标：v1.0.0-alpha.20
- 日期：2026-09-09
- 作者：Codex
- 讨论：[#125](https://github.com/labacacia/NPS-Dev/issues/125)
- 追踪：EPIC-007、ADR-004 D20-02、A20-R005..R008

## 1. 摘要

NIP §7.8 / NWP §9.7 的规范后续明确原生载体、逐请求精确特性对、完整语义正文、
UTF-8 数组顺序及确定性标识/尺寸上限，取代下文历史提案中未明确的载体/正文细节。
实现仍未完成。

区分已验证准入权限和调用方关联信息，定义认证网关之间的短时签名投影。
本文为 NWP/NIP 新 profile 提案；不改变已发布版本或 IdentFrame 编码。

## 2. 动机

BKE 可选签名配置和 BKL 本地 owner 回退需要入口验证，不能单凭源码判定远程漏洞。
Header 名称不能证明权限来源；产品 IAM 决策继续由产品负责。

## 3. 规范提案

### 3.1 内存对象

分离调用方事实（request_id、traceparent、期望 deadline、idempotency_key、有界 hints）
和准入权限（subject NID、验证后的 assurance、capabilities、部署 scope、issuer、
session/transport 来源）。调用方只能缩短服务端期限。幂等 key 不是身份。
Native 权限来自 NIP session 准入，不来自 Hello.agent_id。本地开发权限必须由服务端
显式部署模式启用，远程请求不能选择。不能把请求反序列化为已准入权限。

### 3.2 编码与字段

保留 feature ID `nps.invocation_context` revision 1；完成向量和发布门槛前为 experimental。
两跳均要求支持。单个 `X-NPS-Invocation-Context` header 是 UTF-8 JSON
`{"payload":{...},"signature":"..."}` 的无填充 base64url。

拒绝重复 header/key、非规范 base64url、未知字段、无效 Unicode、非 I-JSON 数值；
解码后最多 16 KiB 或更小本地上限。payload 仅包含以下字段：

| 字段 | 类型和约束 |
|---|---|
| profile | 固定 nps.invocation_context |
| revision | 整数 1 |
| issuer | 准入网关 NID，最多 512 UTF-8 字节 |
| key_id | 配置 key 标识，1..128 ASCII 字节 |
| audience | 精确逻辑接收服务 ID，1..512 UTF-8 字节 |
| subject | 原始主体 NID，最多 512 UTF-8 字节 |
| assurance | anonymous / attested / verified，受接收方许可约束 |
| capabilities | 排序去重，最多 64 项，每项最多 128 UTF-8 字节 |
| scope | 部署注册的 opaque scope ID，排序去重，最多 64 项，每项最多 256 UTF-8 字节 |
| issued_at / expires_at | Unix 秒整数 |
| projection_id | 16 个密码学随机字节的 32 位小写 hex |
| request_digest | sha256: 加 64 位小写 hex |

scope 不是策略表达式；通配符不自动授予权限。字符串精确比较，不暗中大小写折叠或
Unicode normalization。签名输入为 UTF-8 `NPS-CONTEXT-PROJECTION/1`、一个零字节、
RFC 8785/JCS payload。签名是 64 字节 Ed25519 的无填充 base64url。
不允许从输入选择算法或嵌入 key；只使用配置中的 (issuer,key_id)。不改变 IdentFrame。

### 3.3 请求摘要

SHA-256 输入为 UTF-8 `NPS-INVOKE-REQUEST/1`、一个零字节、以下精确对象的 JCS：
protocol、operation、target、idempotency_key、required_features、body。
protocol/operation 使用注册标识；target 为解析后的精确逻辑服务/资源；
idempotency_key 为字符串或 null；required_features 按 ID 排序且无重复。

JSON body 为 `{"encoding":"json","value":<I-JSON>}`；
二进制为 `{"encoding":"bytes","content_type":"<注册的规范媒体类型>","sha256":"sha256:<hex>"}`。
未注册/歧义媒体类型拒绝。影响操作语义的 query 参数必须纳入 value；排除 trace 和
投影本身。注册的操作 schema 必须包含影响权限/副作用的全部输入，不得暗用 header。
完整 JSON 示例见英文 §3.3。签发方与接收方必须解析出相同操作、目标、body。
网关改变任何语义字段或 required_features 时必须重新认证并签发下一跳投影。

### 3.4 时间、对端与重放

时间整数范围为 0..9007199254740991；0 < expires_at-issued_at <= 60 秒。
最大时钟容差 5 秒；issued_at > now+5 或 now >= expires_at+5 时拒绝。
本地可收紧。通过独立认证的 TLS/mTLS 或等价传输，将对端绑定到配置的 issuer 集合；
签名本身不足以准入。公网投影输入先移除，调用方不能选择可信 issuer 集合。

派发前按尺寸/schema、channel issuer、配置 key、签名、audience/subject 绑定、时间、
request digest 和本地准入策略验证。接收方委托上限约束 scope/capabilities。
subject 是可信 issuer 声明的原始主体，不需要等于紧邻网关身份。

重放存储对 (issuer,projection_id) 原子 insert-if-absent，至少保留到 expires_at+5。
同 audience 的副本共享且进程重启后仍有效；不可用时安全拒绝。无效请求不能消耗 ID；
并发有效重复最多准入一个。丢失响应后重试使用新 projection_id 和相同操作幂等身份。
重放检测不代替持久化操作去重，插入 ID 不证明业务已执行。

key 轮换通过配置重叠窗口，按最大有效期加容差约束。撤销立即生效，不接受“撤销前签发”
作为豁免。日志只记有界标识/错误，不记录 header、签名或敏感 scope。

### 3.5 错误提案

| 错误 | 状态 | HTTP |
|---|---|---|
| NIP-CONTEXT-INVALID | NPS-CLIENT-BAD-FRAME | 400 |
| NIP-CONTEXT-UNTRUSTED | NPS-AUTH-FORBIDDEN | 403 |
| NIP-CONTEXT-BINDING-MISMATCH | NPS-AUTH-FORBIDDEN | 403 |
| NIP-CONTEXT-EXPIRED | NPS-AUTH-FORBIDDEN | 403 |
| NIP-CONTEXT-REPLAY | NPS-CLIENT-CONFLICT | 409 |
| NIP-CONTEXT-VERIFIER-UNAVAILABLE | NPS-SERVER-UNAVAILABLE | 503 |

错误签名/未知 key 为 untrusted；未来时间或非法生命周期为 invalid。
返回有界诊断。这些错误尚待 registry 落地。

## 4. SDK 改动

.NET/Python/TypeScript/Go/Java/Rust 实现严格解析、JCS/摘要/签名、不可变准入对象、
issuer channel 绑定和具有原子语义的持久化重放存储接口。IAM 和 transport auth 注入
明确策略。必须在 typed deserialization 丢弃未知字段前完成验证。

## 5. 合规场景

CTX-001：六 SDK 的 JCS/摘要/签名字节一致。
CTX-002：错误签名、issuer key、对端 issuer、audience 拒绝。
CTX-003：body/target/operation/features/幂等 key 变化拒绝。
CTX-004：缺 key/replay store 拒绝。
CTX-005：跨副本并发重复仅准入一次。
CTX-006：进程重启后保留期内仍拒绝重放。
CTX-007：时间边界、超长生命周期、未来时间。
CTX-008：重复 JSON/header、歧义 base64url、未知字段、超限拒绝。
CTX-009：移除公网伪造投影；Hello.agent_id 不授予权限。
CTX-010：网关变化重新投影且验证原始 subject。
CTX-011：签名 scope 不能扩大委托上限。
CTX-012：新投影配合相同操作 key 命中持久化去重。
CTX-013：撤销 key 使未到期投影失效。
CTX-014：远程请求不能选择本地权限模式。

## 6. 迁移

BKE/BKL/Ivy/Passport/Root 先证明入口认证与部署模式，再映射产品权限。
保留已有 SDK issuance wrapper。只有端到端实施强制验证后才声明支持。

## 7. 范围之外

产品租户词汇、审批 UI、分布式业务事务、通用 bearer token、重写证书签名、
未经验证的部署漏洞结论。

## 8. 完成标准

- [ ] 双语规范、错误/版本更新（P20-105/107）。
- [ ] CTX-001..014 字节/安全向量（P20-108）。
- [ ] 六 SDK 正负向一致性及持久化重放测试。
- [ ] 网关入口、副本、重启验证与日志脱敏。

## 9. CHANGELOG 提案

增加请求绑定的认证调用投影，具有严格规范签名、可信对端/issuer 绑定和持久化重放准入。

## 10. 后续

Feature 生命周期提升等待共享向量；scope 词汇由部署负责。
起草完成不表示实现或资格验证完成。
