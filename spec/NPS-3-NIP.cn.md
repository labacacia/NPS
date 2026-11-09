[English Version](./NPS-3-NIP.md) | 中文版

# NPS-3: Neural Identity Protocol (NIP)

**Spec Number**: NPS-3
**Status**: Proposed
**Version**: 0.16
**Date**: 2026-09-09
**Port**: 17433（默认，共用）/ 17435（可选独立）
**Authors**: Ori Lynn / INNO LOTUS PTY LTD
**Depends-On**: NPS-1 (NCP v0.13)

---

## 1. Terminology

本文档中的关键字 "MUST"、"MUST NOT"、"SHOULD"、"MAY" 按照 RFC 2119 解释。

---

## 2. 协议概述

NIP 为每个 AI Agent、NWP 节点和人类操作员颁发可验证的神经身份（NID），携带能力声明和权限 scope，支持信任链传递和实时吊销。NIP 是 NPS 安全模型的核心。

### 2.1 参与方

| 角色 | 描述 |
|------|------|
| Root CA | 根证书颁发机构，离线保存 |
| Org CA | 组织中间 CA，每组织一个 |
| Agent | 持有 NID 证书的 AI Agent |
| Node | 持有 NID 证书的 NWP 节点 |
| Operator | 人类管理员，持有 Operator 证书 |
| Verifier | 验证 NID 的一方（通常是 Node）|

### 2.2 CA 层级结构

```
Root CA（离线保存，极少使用）
    │
    └── Org Intermediate CA（每组织一个，有效期 1 年）
            ├── Agent Certificate    （有效期 30 天，支持自动续期）
            ├── Node Certificate     （有效期 90 天）
            └── Operator Certificate （有效期 1 年，绑定 MFA）
```

---

## 3. NID 格式

```abnf
nid         = "urn:nps:" entity-type ":" issuer-domain ":" identifier
entity-type = "agent" / "node" / "org"
issuer-domain = <RFC 1034 domain>
identifier  = 1*(ALPHA / DIGIT / "-" / "_" / ".")
```

**示例**

```
urn:nps:agent:ca.innolotus.com:550e8400-e29b-41d4    ← AI Agent
urn:nps:node:api.myapp.com:products                   ← NWP 节点
urn:nps:org:mycompany.com                              ← 组织 CA
```

### 3.1 保留标识符前缀（NPS-CR-0003）

`entity-type = agent` 的 NID 上保留两类标识符前缀，用于在编排器 / 会话 lineage 模型中标记结构化角色。两者均遵循已有的 identifier ABNF（`1*(ALPHA / DIGIT / "-" / "_" / ".")`）。

| 前缀 | 角色 | 示例 |
|------|------|------|
| `group-` | 编排器组 NID —— 由 CA 代表该组所颁发的一组会话 NID 的信任锚。生命周期较长（默认 365 天）且可吊销。| `urn:nps:agent:ca.example.com:group-7f3c9e1a-b2d8-4c6f-9a01` |
| `session-` | 在某个组下颁发的短寿命会话 NID。`session-` 之后的部分 MUST 为 `{unix-timestamp}-{random}` 形式，其中 `{random}` 至少 8 位十六进制。默认有效期 1 小时，最长 24 小时。| `urn:nps:agent:ca.example.com:session-1714672800-f3a92c0b` |

不带这两类前缀的标识符仍按普通 agent NID 处理。接收方 MUST NOT 仅凭前缀拒绝某个 NID；前缀仅作信息提示，权威角色由签名后的 `IdentFrame.lineage.role` 字段承载（§5.1.3）。协议层的 `nop:orchestrate` 能力（NPS-5）继续表达"作为编排器"的角色，与本前缀约定相互独立。

---

## 4. 签名算法

| 算法 | 用途 | 密钥长度 |
|------|------|---------|
| **Ed25519**（主算法）| Agent 高频验签 | 32 字节私钥 / 32 字节公钥 |
| **ECDSA P-256**（备用）| 兼容性场景 | 32 字节私钥 / 64 字节公钥 |

公钥编码格式：`{algorithm}:{base64url(DER)}`，例如：`ed25519:MCowBQYDK2VwAyEA...`

---

## 5. 帧类型

### 5.1 IdentFrame (0x20)

Agent 身份声明与证书携带。每次建立连接时作为握手帧发送。

**字段定义**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `frame` | uint8 | 必填 | 固定值 `0x20` |
| `nid` | string | 必填 | Agent NID |
| `pub_key` | string | 必填 | 公钥，格式：`{alg}:{base64url}` |
| `capabilities` | array | 必填 | Agent 持有的能力列表 |
| `scope` | object | 必填 | 访问范围声明 |
| `issued_by` | string | 必填 | 颁发者 NID（Org CA）|
| `issued_at` | string | 必填 | 颁发时间（ISO 8601 UTC）|
| `expires_at` | string | 必填 | 过期时间（ISO 8601 UTC）|
| `serial` | string | 必填 | 证书序列号（Org CA 全局唯一，十六进制）|
| `signature` | string | 必填 | CA 对本帧签名，格式：`{alg}:{base64url}` |
| `cert_format` | string（枚举）| 必填 | 证书编码格式。取值 `"x509-der"` / `"raw-pubkey"` 之一。不参与签名的规范化 JSON（见 §5.1.3 规范化说明）。|
| `cert_chain` | array of string | `cert_format = "x509-der"` 时必填 | DER 编码的证书链，base64url 编码，leaf 在前。当 `cert_format = "raw-pubkey"` 时，`cert_chain` MUST 省略（不出现在序列化帧中，也不进入 canonical form）。不参与签名的规范化 JSON（见 §5.1.3 规范化说明）。|
| `metadata` | object | 可选 | Agent 元数据，见下 |
| `assurance_level` | string | 可选 | 取值 `"anonymous"` / `"attested"` / `"verified"` 之一（见 §5.1.1）。当 NID 证书携带 `id-nid-assurance-level` 扩展时本字段为 REQUIRED。接收方 MUST 把缺失字段视作 `"anonymous"`（向后兼容 v1.0-alpha.2 发布者）。两端都存在时 MUST 保持一致——**Phase gate**：Phase 1–2（当前）强制可选（SHOULD 检查，MAY 强制执行）；Phase 3 flag day（见 NPS-RFC-0003 §8.1）起强制变为 MUST，违规返回 `NIP-ASSURANCE-MISMATCH`。（NPS-RFC-0003）|
| `lineage` | object | 可选 | 签名 lineage 元数据。当本 NID 为编排器组（`role = "group"`）或短寿命会话（`role = "session"`）时存在。详见 §5.1.3。（NPS-CR-0003）|
| `ocsp_staple` | string | 可选 | Agent 在发送时装订（staple）的 base64url 编码 DER OCSP 响应（NIP v0.9 §5.1.4）。接收方 SHOULD 验证 staple 签名；staple 过期返回 `NIP-OCSP-STAPLE-EXPIRED`。缺省时接收方 MAY 按 NWM 中声明的 `ocsp_url` 发起在线 OCSP 查询。|
| `node_roles` | array[string] | 可选 | 自声明的节点角色标签，例如 `["memory", "orchestrator"]`（NIP v0.10）。词汇与 NDP `AnnounceFrame.node_roles` 相同。Phase 1–2：自声明，仅作参考。Phase 3 flag day 起：MUST 与 `id-nps-node-roles` X.509 扩展（`1.3.6.1.4.1.65715.2.2`）一致；不一致返回 `NIP-CERT-NODE-ROLES-MISMATCH`。|

**metadata 字段（可选）**

| 字段 | 类型 | 描述 |
|------|------|------|
| `model_family` | string | Agent 使用的模型族标识，如 `"openai/gpt-4o"`、`"anthropic/claude-4"` |
| `tokenizer` | string | Agent 使用的 tokenizer 标识，如 `"cl100k_base"` |
| `runtime` | string | Agent 运行时标识，如 `"langchain/0.2"`、`"autogen/0.4"` |

metadata 字段不参与签名计算，Agent 可在运行时动态设置。Node 使用 metadata 中的 tokenizer 信息实现 CGN 自动匹配（见 [token-budget.cn.md](token-budget.cn.md)）。

**未签名 `metadata` 的信任边界（normative —— closes issue #39）**

由于 `metadata` 不参与签名，其中每一个值都属于 **Agent 自报、未经验证**的输入。Node MUST NOT 将 `IdentFrame.metadata` 的任何字段（包括但不限于 `model_family`、`tokenizer`、`runtime`）作为以下用途的输入：

1. **计费或结算** —— 实际向租户收取的 CGN，或计入付费配额的数量；
2. **配额提升** —— 授予更大的 token 预算、更高并发数，或任何高于默认的限速档；
3. **声誉评分** —— 任何会影响后续路由或定价的持久化信任信号；
4. **安全或授权决策** —— 准入能力门控端点、策略豁免，或保证等级（assurance level）核验。

上述用途下，Node MUST 改用一个**经验证（verified）**的信号 —— 携带于已签名的 IdentFrame 主体、X.509 NID 证书（NPS-RFC-0002 扩展）或 CA 背书的旁路通道之中 —— 或 Node 自身的**观测（observed）**测量值。把未签名的 `metadata` 当作权威信号属于不合规行为。

**Tokenizer 三层信任模型**

为了把上述信任边界落实到 token 计费链路，NPS 定义三种相互独立的 tokenizer 信号。实现 MUST 在代码路径与日志中保持三者可区分。

| 等级 | 来源 | 信任 | 允许的用途 |
|------|------|------|------------|
| `declared_tokenizer` | Agent 通过 `IdentFrame.metadata.tokenizer` 或 `X-NWP-Tokenizer` 请求头自带。**未签名。** | 仅 Agent 单方声明。 | **仅作估算提示**：用于预算预估、缓存键消歧、遥测。MUST NOT 驱动计费、配额提升、声誉或安全。 |
| `verified_tokenizer` | 由 CA 或平台对 tokenizer 标识与 NID 之间绑定关系的背书 —— 例如 X.509 NID 证书扩展、NPS-AaaS-Profile 下 AaaS 运营者的签字声明，或部署各方约定的其他签名通道。 | 可加密验证。 | 对策略、计费、配额档位与保证级访问权威。结算级流程 MUST 以本层为门槛。 |
| `observed_tokenizer_profile` | Node 侧的统计或行为推断（如字节长度分布、响应形态指纹、采样回 tokenize）。 | Node 内部测量。 | 滥用检测、漂移告警、异常评分及 Node 自身声誉引擎的输入。MUST NOT 作为已验证的声明对外输出。 |

当多层信号同时可用时，对上述边界所约束的任何决策，Node MUST 采用最高信任层级；较低层级 MAY 并存以供诊断比对。

**capabilities 标准值**

| 能力 | 描述 |
|------|------|
| `nwp:query` | 可查询 Memory Node |
| `nwp:action` | 可调用 Action Node |
| `nwp:stream` | 可接收 StreamFrame 响应 |
| `ncp:stream` | 可发起 NCP 流式传输 |
| `nop:delegate` | 可委托子任务给其他 Agent |
| `nop:orchestrate` | 可作为 Orchestrator 发起 TaskFrame |
| `topology:read` | 可通过保留查询类型 `topology.snapshot` / `topology.stream` 读取 Anchor Node 拓扑数据（NPS-2 §12）；Phase 1–2 的 Anchor Node MUST 按 NPS-2 §12.4 要求此能力。Phase 1–2 中该能力为自声明并签名；CA 背书的角色绑定推迟到 Phase 3（RFC-0002 修订）。 |
| `llm:complete` | 可调用带 LLM/Thinking Profile 节点上的标准 NWP `llm.complete` action（NPS-2 §4.2a、§7.5）|
| `llm:context` | 可按 NWP §7.6 操作 owner-bound 有状态 LLM context。Create/append/fork/reset 还要求 `llm:complete`；status/release 只要求该 capability 加 owner 授权。仅持有 context ID 不产生任何授权。 |
| `llm:stream` | 可接收流式 LLM completion 响应（`LlmCompleteActionRequest.stream = true`）|
| `llm:tool_call` | 可在 LLM completion 流程中提交工具定义并接收 tool-call 请求 |
| `llm:embed` | 节点声明 embedding 支持时，可调用标准 embedding action |
| `llm:rerank` | 节点声明 rerank 支持时，可调用标准 rerank action |

**scope 字段**

```json
{
  "nodes":            ["nwp://api.myapp.com/*"],
  "actions":          ["orders:read", "orders:create"],
  "max_token_budget": 50000
}
```

**签名计算**

签名对象为 IdentFrame 去掉 `signature` 字段后的规范化 JSON（字段字母序，无空白）。

**完整示例**

```json
{
  "frame": "0x20",
  "nid": "urn:nps:agent:ca.innolotus.com:550e8400-e29b-41d4",
  "pub_key": "ed25519:MCowBQYDK2VwAyEA...",
  "capabilities": ["nwp:query", "nwp:action", "ncp:stream"],
  "scope": {
    "nodes":            ["nwp://api.myapp.com/*"],
    "actions":          ["orders:read", "orders:create"],
    "max_token_budget": 50000
  },
  "issued_by":  "urn:nps:org:mycompany.com",
  "issued_at":  "2026-04-10T00:00:00Z",
  "expires_at": "2026-05-10T00:00:00Z",
  "serial":     "0x0A3F9C",
  "signature":  "ed25519:3045022100..."
}
```

---

### 5.1.1 保证等级（NPS-RFC-0003）

NPS 为 Agent 身份定义三个**保证等级**，参考 NIST SP 800-63 IAL 与 CA/B Forum DV/OV/EV 设计。等级通过 `IdentFrame.assurance_level` 传输，并作为 Node 决策（`NWM.min_assurance_level`）的权威来源；当使用 X.509 NID 证书（NPS-RFC-0002）时，证书 MUST 同样携带 `id-nid-assurance-level` 扩展，且两者 MUST 一致——**Phase gate**：Phase 1–2（当前）强制可选；Phase 3 flag day 起强制（见 NPS-RFC-0003 §8.1）。

| 等级 | 枚举值 | CA 最低要求 | 典型用途 |
|------|--------|-------------|---------|
| L0 | `"anonymous"` | 自签，或 CA 签发但无身份绑定 | 业余 Agent、开发/测试、免费只读端点 |
| L1 | `"attested"` | NID 由 RFC-0002 兼容 CA 签发；CA 验证 NID 私钥持有（如 ACME `agent-01` 挑战）；联系邮箱或域名经过验证。`id-nid-assurance-level` X.509 扩展使用 IANA 已分配 OID `1.3.6.1.4.1.65715.2.1`，配套 `id-nps-node-roles` 扩展使用 `1.3.6.1.4.1.65715.2.2`（PEN 65715 于 2026-05-08 分配给 LabAcacia，见 NPS-CR-0004 与 NPS-RFC-0002 §10 OQ-2）。| 大多数生产 Agent；默认限速级别 |
| L2 | `"verified"` | L1 要求 **加** CA 绑定运营者法律身份（org-NID 用工商注册，托管 Agent 用 AaaS 运营者签字声明）| 受监管集成、付费高级、可签合约的编排 |

默认为 `"anonymous"` —— RFC-0003 之前的 NID 以及任何缺该字段的 NID 都按 L0 处理。Node 通过 NWM 中 `min_assurance_level` 声明严格要求（NPS-2 §4.1、§4.3）。

枚举**有序**：`anonymous < attested < verified`。请求等级低于 Node 要求时 MUST 返回 `NWP-AUTH-ASSURANCE-TOO-LOW`（`NPS-AUTH-FORBIDDEN`）。

**向前兼容**：收到不在本枚举中的 `assurance_level` 值时，实现 MUST 将其视为协议错误并返回 `NIP-ASSURANCE-UNKNOWN`（`NPS-CLIENT-BAD-FRAME`）。实现 MUST NOT 静默降级为 `anonymous` —— 静默降级会为未来规范新增的更高保证等级留下安全漏洞。

完整设计动机（含与 NPS-RFC-0002 协调的 X.509 critical extension 翻转计划）参见 [NPS-RFC-0003](rfcs/NPS-RFC-0003-agent-identity-assurance-levels.cn.md)。

---

### 5.1.2 声誉日志条目（NPS-RFC-0004）

> 与保证等级（§5.1.1）配套。`assurance_level` 回答*"这是谁？"*，声誉日志条目回答*"这个 NID 行为如何？"* —— 以 Certificate-Transparency 风格的 append-only 日志发布，让任何方（Node、审计、下游运营方）都能在没有预先关系的情况下拿到关于同一 NID 的同一份证据。

声誉条目是签名 JSON 对象，由 issuer（典型为 AaaS Anchor Node、审计、CA）发布，针对一个 `subject_nid` 记录一次观察到的行为事件。条目由**日志运营方**汇总成 append-only 历史；下面的 wire 格式是条目本身，与具体日志运营方的 HTTP 接口无关（运营接口见 [NPS-RFC-0004 §4.3](rfcs/NPS-RFC-0004-nid-reputation-log.cn.md)）。

**字段定义**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `v` | uint8 | 必填 | Schema 版本。本文档定义 `1`。|
| `log_id` | string (NID) | 必填 | append（或将要 append）此条目的日志运营方 NID。自发布时 MAY 与 `issuer_nid` 相等。|
| `seq` | uint64 | 必填 | 每个 `log_id` 内单调递增的序号。由日志运营方在 append 时设置。|
| `timestamp` | string (RFC 3339 UTC) | 必填 | 日志运营方的提交时间。|
| `subject_nid` | string (NID) | 必填 | 本条目*关于*哪个 NID。|
| `incident` | string (枚举) | 必填 | §5.1.2.1 中的取值之一，或前向兼容的未知值（接收方 MUST 保留未知值）。|
| `severity` | string (枚举) | 必填 | `info` / `minor` / `moderate` / `major` / `critical` 之一。|
| `window` | object | 可选 | `{ start: ISO 8601, end: ISO 8601 }`，描述观察窗口。|
| `observation` | object | 可选 | 自由格式的、机器可读的、按事件类型而定的细节（如限速违规可写 `{requests: 45000, threshold: 300}`）。|
| `evidence_ref` | string (URL) | 可选 | 更丰富证据（日志、转录）的 URL。|
| `evidence_sha256` | string (hex) | 可选 | 证据 blob 的 SHA-256；让查询方可以检测对日志外证据的篡改。|
| `issuer_nid` | string (NID) | 必填 | 做出断言的一方的 NID。MAY 等于 `log_id`。|
| `signature` | string | 必填 | `{alg}:{base64url}` Ed25519 签名，由 `issuer_nid` 私钥对**移除 `signature` 字段后**经 RFC 8785（JCS）规范化的条目计算。日志运营方校验该签名后，再用自己的 `log_id` 私钥重签整个条目以承诺序号（双签名模型）。|

#### 5.1.2.1 事件类型词汇表（初始集）

| 取值 | 含义 |
|------|------|
| `cert-revoked` | CA 吊销了 `subject_nid` 的证书。|
| `rate-limit-violation` | 持续违反公布的限速。|
| `tos-violation` | 违反 AaaS Anchor Node 公布的服务条款。|
| `scraping-pattern` | 行为匹配爬虫启发式规则。|
| `payment-default` | CGN 或法币的已承诺交易违约。|
| `contract-dispute` | 异步 NOP 任务上未解决的合同违约。|
| `impersonation-claim` | 第三方主张 `subject_nid` 在冒充自己。|
| `positive-attestation` | 显式正面信号（如独立审计通过）。|

接收方 MUST 把未知 `incident` 取值视作不透明透传（前向兼容）；运营侧过滤器 MAY 忽略它们。详见 [NPS-RFC-0004 §4.2](rfcs/NPS-RFC-0004-nid-reputation-log.cn.md)。

**错误**

| 错误码 | 触发条件 |
|--------|---------|
| `NIP-CONTEXT-INVALID` | `NPS-CLIENT-BAD-FRAME` | 投影格式、边界或生命周期无效 |
| `NIP-CONTEXT-UNTRUSTED` | `NPS-AUTH-FORBIDDEN` | 不可信 issuer/key 或无效签名 |
| `NIP-CONTEXT-BINDING-MISMATCH` | `NPS-AUTH-FORBIDDEN` | 投影请求、主体、audience 或策略不匹配 |
| `NIP-CONTEXT-EXPIRED` | `NPS-AUTH-FORBIDDEN` | 投影有效窗口已过期 |
| `NIP-CONTEXT-REPLAY` | `NPS-CLIENT-CONFLICT` | 有效投影已被准入 |
| `NIP-CONTEXT-VERIFIER-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | 验证或持久化重放基础设施不可用 |
| `NIP-REPUTATION-ENTRY-INVALID`（`NPS-CLIENT-BAD-FRAME`）| 条目签名校验失败或规范化形式不合法。|
| `NIP-REPUTATION-LOG-UNREACHABLE`（`NPS-DOWNSTREAM-UNAVAILABLE`）| 准入评估时无法到达 Node `reputation_policy` 引用的某个日志运营方。|
| `NWP-AUTH-REPUTATION-BLOCKED`（`NPS-AUTH-FORBIDDEN`）| 声誉策略命中了对发起方 `subject_nid` 的 `reject_on` 规则。（定义在 NPS-2 §6 —— Anchor / Memory / Action / Complex / Bridge Node 选择启用。）|

> NPS-RFC-0004 Phase 1 规范化条目 wire 格式与上方签名规则；Merkle 树 / Signed Tree Head / inclusion-proof 接口（RFC 9162 风格）以及 NDP 发现接口在 Phase 2 落地。完整 phasing 见 RFC。

---

### 5.1.3 Lineage（NPS-CR-0003）

`IdentFrame.lineage` 承载签名后的父链元数据，将短寿命会话 NID 链接回其编排器组 NID，并记录组的所有者 / 授权来源。信任链如下：

```
人类所有者  →  Operator key（§2.1）  →  编排器组 NID  →  短寿命会话 NID
```

`lineage` 是 IdentFrame 规范化 JSON 的**签名**部分（与被排除的 `metadata` 不同）。修改任一子字段都会使 CA 签名失效——这正是链路可端到端**验证**的关键：节点在准入会话 NID 时可证明 `lineage.parent_nid` 是 CA 在颁发时权威断言的，而非链路上后塞的。

**字段定义**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `role` | enum（`"group"` / `"session"`）| `lineage` 存在时必填 | 本 NID 的 lineage 角色。|
| `parent_nid` | string（NID）| `role = "session"` 时必填 | 直接父 NID。1 级链（group → session）下等于 `group_nid`。|
| `group_nid` | string（NID）| `role = "session"` 时必填 | 链路根上的组 NID。1 级链下与 `parent_nid` 相等；为未来更深层链路保留。|
| `session_id` | string | `role = "session"` 时必填 | 本次执行的稳定 id；与 NID 标识符中的 `session-...` 段相符（§3.1）。回显出来允许消费方无需解析 URN 即可索引。|
| `purpose` | string（≤256 UTF-8 字节）| 可选 | 本次执行用途的人类可读自由文本标签，如 `"order-classification-job"`。|
| `owner_user_id` | string | 可选 | 该组代表的人类所有者稳定标识（如内部 user UUID）。当 `role = "group"` 且部署知道所有者时 SHOULD 填写。|
| `owner_key_id` | string | 可选 | 授权创建该组的所有者密钥 `kid` 提示（Operator key、OIDC `sub`、硬件令牌 id）。|

**组 NID 示例**

```json
"lineage": {
  "role":          "group",
  "owner_user_id": "user-7f3c9e1a",
  "owner_key_id":  "op-kid-2026-04"
}
```

**会话 NID 示例**

```json
"lineage": {
  "role":          "session",
  "parent_nid":    "urn:nps:agent:ca.example.com:group-7f3c9e1a-b2d8-4c6f-9a01",
  "group_nid":     "urn:nps:agent:ca.example.com:group-7f3c9e1a-b2d8-4c6f-9a01",
  "session_id":    "session-1714672800-f3a92c0b",
  "purpose":       "data-extraction-job-42",
  "owner_user_id": "user-7f3c9e1a",
  "owner_key_id":  "op-kid-2026-04"
}
```

**向后兼容**：CR-0003 之前的发布者不发送 `lineage`；CR-0003 之前的严格规范化验证器（排序 + 排除 `signature` / `metadata` / `cert_format` / `cert_chain`）若其 DTO 未承载该字段会丢弃未知字段，对 CR-0003 帧的签名校验**会失败**。新发送方在感知 CR-0003 时产出 CR-0003 帧；旧验证方 MUST 升级以验证它们。无 `lineage` 的普通 agent NID 仍按位兼容。

**错误码**

| 错误码 | 触发条件 |
|--------|---------|
| `NIP-CA-GROUP-REVOKED`（`NPS-AUTH-FORBIDDEN`）| 不能在已吊销的组下颁发会话。|
| `NIP-CA-PARENT-NOT-FOUND`（`NPS-CLIENT-NOT-FOUND`）| 会话颁发请求引用的 `parent_nid` / 组 NID 不存在。|
| `NIP-CA-PARENT-NOT-GROUP`（`NPS-CLIENT-BAD-PARAM`）| 引用的父 NID 存在但未注册为 `lineage.role = "group"`。|
| `NIP-CA-SESSION-VALIDITY-INVALID`（`NPS-CLIENT-BAD-PARAM`）| 会话有效期低于 60 秒或高于 CA 配置的最大值。|
| `NIP-CA-JWS-INVALID`（`NPS-AUTH-UNAUTHENTICATED`）| 会话颁发请求上的 group-JWS 在签名、头、或形态校验上失败。|
| `NIP-CA-JWS-EXPIRED`（`NPS-AUTH-UNAUTHENTICATED`）| group-JWS `iat` 超出 CA 时钟偏移窗口（默认 ±5 分钟）。|
| `NIP-CERT-PARENT-REVOKED`（`NPS-AUTH-UNAUTHENTICATED`）| 会话 NID 的父 / 组 NID 已被吊销或过期（链路检查，§7 步骤 3a）。|

完整动机、JWS 形态与迁移影响见 [NPS-CR-0003](cr/NPS-CR-0003-orchestrator-group-session-nids.md)。

---

### 5.1.4 OCSP Stapling（NIP v0.9）

Agent MAY 把预先取得的 OCSP 响应附加到 IdentFrame 的 `ocsp_staple` 字段，使接收方 Node 无需实时 OCSP 往返即可验证证书吊销状态。

**验证规则（接收方）**

1. 把 `ocsp_staple` 从 base64url 解码，得到 DER 编码的 `OCSPResponse`。
2. 用颁发 CA 的证书（取自 `cert_chain` 或 Node 本地信任库）验证该 OCSP 响应的签名。
3. 检查 `thisUpdate` 与 `nextUpdate`：若当前时间晚于 `nextUpdate`，返回 `NIP-OCSP-STAPLE-EXPIRED`。
4. 在 `SingleResponse` 中检查由 `serial` 标识的那张证书的 `certStatus`。若为 `revoked`，返回 `NIP-CERT-REVOKED`。
5. 通过全部检查的 staple 覆盖该 serial 的任何已缓存吊销状态；Node SHOULD NOT 再发起实时 OCSP 请求。

当 `ocsp_staple` 缺省时，Node MAY 按 `NWM.ocsp_url`（§4.1）声明的 URL 发起在线 OCSP 查询。该查询为可选项，但对 `"verified"` 保证等级的端点 RECOMMENDED。

**X.509 OID 注册表（NIP v0.9 —— PEN 65715 arc）**

| OID | 名称 | ASN.1 类型 | 描述 |
|-----|------|-----------|------|
| `1.3.6.1.4.1.65715.2.1` | `id-nid-assurance-level` | UTF8String | Agent 保证等级（`anonymous` / `attested` / `verified`）。CA MUST 在颁发时填充。|
| `1.3.6.1.4.1.65715.2.2` | `id-nps-node-roles` | SEQUENCE OF UTF8String | `AnnounceFrame.node_roles` / `IdentFrame.node_roles` 携带的角色标签。CA SHOULD 从注册请求中填充。Phase 3 起强制。|
| `1.3.6.1.4.1.65715.2.3` | `id-nps-capabilities` | SEQUENCE OF UTF8String | 能力字符串（词汇与 `IdentFrame.capabilities` 相同）。CA MAY 填充，作为未签名 `capabilities` 字段的 CA 背书替代。Phase 3。|

---

### 5.2 TrustFrame (0x21)

跨 CA 信任链传递与能力授权。TrustFrame 让某一 CA（**grantor**，授权方）授权另一 CA（**grantee**，受权方）颁发的 IdentFrame 中携带的 `capabilities` 被已信任 grantor 的 Node 接受 —— 而无需将 grantee 加入每个 Node 的 `trusted_issuers` 列表。授权按能力子集与一组 `nwp://` URL 模式作 scope 限制；通过 grantor 的签名做到端到端可验证。开源 SDK 承载完整 wire frame，并 MAY 提供针对显式 pin 住的 grantor anchor 的基础验证；托管多 CA federation、trust-anchor discovery、吊销 feed 与商业 trust-chain policy 属于 NPS Cloud 范畴。

**字段定义**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `frame` | uint8 | 必填 | 固定为 `0x21`。|
| `grantor_nid` | string（NID）| 必填 | 授权方 CA 的 NID。MUST 出现在验证方 Node 的 `trusted_issuers` 中，TrustFrame 才会生效。|
| `grantee_ca` | string（NID）| 必填 | 受权方 CA 的 NID。`issued_by` 等于 `grantee_ca` 的 IdentFrame 在该授权下被接受。|
| `trust_scope` | array of string | 必填 | 本次授权覆盖的能力字符串。MUST 是 §5.1 中标准 `capabilities` 枚举的子集（`nwp:query` / `nwp:action` / `nwp:stream` / `ncp:stream` / `nop:delegate` / `nop:orchestrate` / `topology:read` / `llm:complete` / `llm:context` / `llm:stream` / `llm:tool_call` / `llm:embed` / `llm:rerank`）。受权方 MUST NOT 颁发携带该集合外能力的下游 IdentFrame。|
| `nodes` | array of string | 必填 | 本次信任适用的 `nwp://` URL 模式。`*` 匹配单段路径；`**` 匹配多段路径。空数组表示不覆盖任何 Node（授权事实上失效）。|
| `issued_at` | string（ISO 8601 UTC）| 必填 | 颁发时间。|
| `expires_at` | string（ISO 8601 UTC）| 必填 | 过期时间。超过该时刻后帧 MUST 被拒绝并返回 `NIP-TRUST-FRAME-EXPIRED`。|
| `serial` | string（hex）| 必填 | 16 位 0 填充的十六进制序列号，用于通过 RevokeFrame（§5.3）做吊销跟踪。|
| `signer_nid` | string（NID）| 必填 | 对该帧签名的私钥所属 NID。MUST 等于 `grantor_nid` 自身，或位于 `grantor_nid` 域下、并被本地策略识别为 operator / delegated signer 的授权 `agent` NID。|
| `signature` | string | 必填 | `{alg}:{base64url}` —— `ed25519:`（Ed25519）或 `ecdsa-p256:`（ECDSA P-256）。|

**签名计算**

签名对象为 TrustFrame 去掉 `signature` 字段后的规范化 JSON（字段字母序，无空白）—— 规范化规则与 §5.1 IdentFrame 相同。支持的算法：Ed25519（`ed25519:`）与 ECDSA P-256（`ecdsa-p256:`）。

**错误**

| 错误码 | 触发条件 |
|--------|---------|
| `NIP-TRUST-FRAME-INVALID`（`NPS-CLIENT-BAD-FRAME`）| TrustFrame 格式不合法（必填字段缺失、签名验证失败或规范化形式错误）。|
| `NIP-TRUST-FRAME-EXPIRED`（`NPS-AUTH-UNAUTHENTICATED`）| 当前时间已超过 `expires_at`。|
| `NIP-TRUST-FRAME-GRANTOR-REVOKED`（`NPS-AUTH-UNAUTHENTICATED`）| `grantor_nid` 自身的 CA 证书在验证时已被吊销或过期。|
| `NIP-TRUST-FRAME-SCOPE-EXCEEDS-GRANTOR`（`NPS-AUTH-FORBIDDEN`）| `trust_scope` 包含 grantor 自身不持有的能力（不可扩大 scope 原则，§10.3）。|
| `NIP-TRUST-FRAME-NODES-PATTERN-INVALID`（`NPS-CLIENT-BAD-FRAME`）| `nodes` 数组某项不是语法合法的 `nwp://` URL 模式，或通配符出现在不允许的位置。|

**完整示例**

```json
{
  "frame":       "0x21",
  "grantor_nid": "urn:nps:org:org-a.com",
  "grantee_ca":  "urn:nps:org:org-b.com",
  "trust_scope": ["nwp:query", "nwp:action"],
  "nodes":       ["nwp://api.org-a.com/public/**"],
  "issued_at":   "2026-05-11T00:00:00Z",
  "expires_at":  "2026-12-31T00:00:00Z",
  "serial":      "00000000000A3F9C",
  "signer_nid":  "urn:nps:org:org-a.com",
  "signature":   "ed25519:3045022100..."
}
```

---

### 5.3 RevokeFrame (0x22)

吊销一个 NID、该 NID 名下所有证书，或通过 serial 指定的某张证书。RevokeFrame 由颁发该 NID 的 CA（或该 CA 域下已授权的 delegated agent）签发，通过 NIP push 通道推送给订阅的 Node；接收方据此更新本地证书 / OCSP 缓存，并拒绝后续使用被吊销目标进行认证的请求。在协议层 RevokeFrame 为 **fire-and-forget**（成功时无响应帧）；接收方 MUST 仅在帧本身格式错误或未授权时回发 `ErrorFrame`。

**字段定义**

| 字段 | 类型 | 必填 | 描述 |
|------|------|------|------|
| `frame` | uint8 | 必填 | 固定值 `0x22`。|
| `target_nid` | string（NID）| 必填 | 被吊销的 NID。MAY 是 Agent NID、Node NID、组 NID 或 Org NID。|
| `serial` | string（hex）| 可选 | `target_nid` 名下要吊销的具体证书序列号。若存在，MUST 与 `target_nid` 当前已颁发证书的 serial 一致；若缺省，则 `target_nid` 名下所有当前已颁发证书均被吊销（等价于吊销该 NID）。|
| `reason` | string（枚举）| 必填 | 取下方 reason 枚举表中的某个值。|
| `revoked_at` | string（ISO 8601 UTC）| 必填 | 吊销生效的墙钟时刻。接收方 SHOULD 拒绝任何 IdentFrame `issued_at` 早于或等于 `revoked_at` 的请求。|
| `parent_nid` | string（NID）| 条件 | 当 `reason = "parent_revoked"` 时为 REQUIRED，用于标记触发本次级联的组 NID（NPS-CR-0003）；其他 reason 取值时 MUST 省略。|
| `signer_nid` | string（NID）| 必填 | 对本帧签名的 CA 或 delegated agent NID。MUST 是以下之一：`target_nid` 的颁发 CA、该 CA 域下持有 `nip:revoke` 能力的已授权 `agent` NID，或（级联场景）颁发 `parent_nid` 的 CA。|
| `signature` | string | 必填 | `{alg}:{base64url}`，由 `signer_nid` 的私钥签名。支持算法：`ed25519:`（Ed25519）或 `ecdsa-p256:`（ECDSA P-256）。|

**`reason` 枚举**

| 取值 | 含义 |
|------|------|
| `key_compromise` | Agent / Node 私钥已知或疑似被泄露，紧急程度最高。|
| `ca_compromise` | 颁发 CA 的签名密钥被泄露；该 CA 名下所有证书隐含被吊销，对每个受影响子 NID MAY 分别签发 RevokeFrame。|
| `affiliation_changed` | Agent 已离开颁发组织；即便个别证书尚未过期，其全部能力一并吊销。|
| `superseded` | 已被一张同 NID 的重新颁发证书替代（例如需要使旧 serial 失效的常规轮换）。`serial` 字段 SHOULD 设置，用以标记被替代的那张证书。|
| `cessation_of_operation` | Agent / Node 已退役，不会再出示凭据。|
| `parent_revoked` |（NPS-CR-0003）由 CA 在级联吊销中对会话 NID 签发的 RevokeFrame。`parent_nid` 字段 MUST 设为该组 NID。用以区分"因祖先而被强制失效"与"因自身原因被吊销"。|

**向前兼容**：接收方遇到不在上表中的 `reason` 取值 MUST 将其视作 `key_compromise`（最严格解释）。接收方 MAY 额外记录该未知值用于诊断，并 MAY 通过旁路通道向发布 CA 回送 `NIP-REVOKE-FRAME-REASON-UNKNOWN`，但 MUST NOT 把未知 reason 静默降级为更宽松的处理。

**签名计算**

签名对象为 RevokeFrame 去掉 `signature` 字段后的规范化 JSON（字段按字母序排序、无空白）—— 与 §5.1 IdentFrame 的规范化规则相同（RFC 8785 JCS）。

**错误**

| 错误码 | 触发条件 |
|--------|---------|
| `NIP-REVOKE-FRAME-INVALID`（`NPS-CLIENT-BAD-FRAME`）| RevokeFrame 不合法（缺必填字段、签名校验失败或规范化形式错误）。|
| `NIP-REVOKE-FRAME-UNAUTHORIZED-ISSUER`（`NPS-AUTH-FORBIDDEN`）| `signer_nid` 无权吊销 `target_nid`（既不是 `target_nid` 的颁发 CA，也不是该 CA 域下持有 `nip:revoke` 的已授权 delegated `agent`；级联场景下也不是 `parent_nid` 的颁发 CA）。|
| `NIP-REVOKE-FRAME-SERIAL-MISMATCH`（`NPS-CLIENT-BAD-PARAM`）| `serial` 存在但与 `target_nid` 当前已颁发的任何证书 serial 都不匹配。|
| `NIP-REVOKE-FRAME-REASON-UNKNOWN`（`NPS-CLIENT-BAD-FRAME`）| `reason` 取值不在定义的枚举中。接收方安全起见按 `key_compromise` 处理（见上方"向前兼容"），并通过旁路回报本错误码以便发布 CA 纠正。|

**完整示例**

```json
{
  "frame":      "0x22",
  "target_nid": "urn:nps:agent:ca.innolotus.com:550e8400-e29b-41d4",
  "serial":     "0x0A3F9C",
  "reason":     "key_compromise",
  "revoked_at": "2026-04-10T12:00:00Z",
  "signer_nid": "urn:nps:org:ca.innolotus.com",
  "signature":  "ed25519:3045022100..."
}
```

**级联示例（`parent_revoked`）**

```json
{
  "frame":      "0x22",
  "target_nid": "urn:nps:agent:ca.example.com:session-1714672800-f3a92c0b",
  "reason":     "parent_revoked",
  "revoked_at": "2026-04-10T12:00:00Z",
  "parent_nid": "urn:nps:agent:ca.example.com:group-7f3c9e1a-b2d8-4c6f-9a01",
  "signer_nid": "urn:nps:org:ca.example.com",
  "signature":  "ed25519:..."
}
```

`parent_revoked`（NPS-CR-0003）由 CA 在级联吊销中对会话 NID 的 `RevokeFrame` 自行赋值——此时该会话所在的组 NID 已被吊销。它将"因祖先而被强制失效"的会话与"因自身原因被吊销"的会话区分开来。

---

## 6. 证书生命周期

```
注册          自动续期          主动吊销        CA 轮换
  │               │                 │              │
  ↓               ↓                 ↓              ↓
CA 颁发      到期前 7 天         RevokeFrame    新旧 CA 并行
IdentFrame   Agent 自动触发      立即生效         30 天
有效期 30 天  CA 颁发新证书      节点拒绝所有    平滑过渡
             新旧并行 1 小时      该 NID 请求
```

### 6.1 短时 / 可续期证书 profile（边缘 mTLS）

在 `nps-ingress`（L2）网关终结原生模式双向 TLS 的边缘部署（NPS-RFC-0006 §6.3），可以借助
**短时证书**在不做逐请求 OCSP 往返的前提下把吊销暴露窗口降到最小。本 profile 是对标准有效期
档位（§2.1）的增量补充，由 CA 在颁发时选定。

| 属性 | 标准 Node 证书 | **短时边缘证书** |
|------|----------------|------------------|
| 有效期 | 90 天 | **1–24 小时**（默认 6 小时）|
| 续期触发 | 到期前 7 天 | **续期窗口 = 剩余有效期的 25%** |
| 续期重叠 | 新旧并行 1 小时 | 新旧并行至旧证书的 `expires_at` |
| 吊销依赖 | OCSP / CRL | **有效期 ≤ OCSP 缓存 TTL** —— 短有效期本身即主要吊销手段，OCSP 只作纵深防御 |
| 与 OCSP staple 的交互 | 可选 | 网关 SHOULD 装订（`IdentFrame.ocsp_staple`，NIP v0.9 §8.2）；staple 响应若比证书剩余有效期还旧，MUST 刷新（`NIP-OCSP-STAPLE-EXPIRED`）|

规则：

1. 短时边缘证书的有效期 MUST 落在 `[1h, 24h]` 区间内。请求颁发超出该区间的证书返回
   `NIP-CA-SESSION-VALIDITY-INVALID`（复用会话 NID 路径的错误码，§5）。
2. CA SHOULD 支持**在线续期**（ACME `agent-01`，NPS-RFC-0002），使边缘节点无需运营方介入即可
   在续期窗口到来前自行续期。
3. 依赖方（例如 `nps-ingress`）MUST 以 `NIP-CERT-EXPIRED` 拒绝 `expires_at ≤ now` 的短时证书；
   由于有效期本身很短，当证书有效期 ≤ 依赖方配置的 OCSP 缓存 TTL 时，依赖方 MAY 跳过逐请求
   OCSP 查询。
4. 短时边缘证书参与 TLS 1.3 会话恢复（NPS-RFC-0006 §6.4）：恢复票据 MUST NOT 比签发它的那张
   证书活得更久。

### 6.2 alpha.19 续期互操作 Profile（NIP v0.15）

共享 case 位于
[`renewal_revocation_vectors.json`](conformance/nip/renewal_revocation_vectors.json)。

1. **NIP-P19-01 —— 续期窗口。** 标准证书在 `not_after` 前七天开放续期；短时边缘
   证书在原有效期剩余 25% 时开放。边界包含在窗口内。更早请求以
   `NIP-CA-RENEWAL-TOO-EARLY` 返回，且不改变状态。
2. **NIP-P19-02 —— 身份连续性。** 续期使用当前未过期证书认证，并保留 NID、subject、
   lineage、assurance level、capabilities 与 scope；除非 Operator 授权的独立 mutation
   已经收窄这些字段。续期 MUST NOT 扩大权限。
3. **NIP-P19-03 —— 原子颁发。** 成功续期先提交新的唯一 serial 与审计记录再返回。
   旧证书在自身 expiry 或 revoke 前仍独立有效。
4. **NIP-P19-04 —— 幂等。** 续期请求携带 idempotency key。相同 key 与 canonical
   request 重复时返回同一 serial；不同内容复用该 key 时为 `NIP-CA-SERIAL-DUPLICATE`，
   且不得另发证书。
5. **NIP-P19-05 —— resumption bound。** 绑定旧证书的 TLS session ticket 与缓存准入
   不得晚于旧证书过期，也不得仅因颁发 replacement 而延长。

---

## 7. 验证流程

```
Node 收到 IdentFrame
  │
  ├─ 1. 检查 expires_at > now（过期→ NIP-CERT-EXPIRED）
  ├─ 2. 检查 issued_by 在 NWM trusted_issuers 中（不信任→ NIP-CERT-UNTRUSTED-ISSUER）
  ├─ 3. 用 issued_by CA 公钥验证 signature（失败→ NIP-CERT-SIGNATURE-INVALID）
  ├─ 3a.（NPS-CR-0003）若存在 lineage.parent_nid，则对父 NID 发起 OCSP 查询
  │      （已吊销或过期→ NIP-CERT-PARENT-REVOKED）
  ├─ 4. OCSP 查询（若 NWM 配置了 ocsp_url）或检查本地 CRL（已吊销→ NIP-CERT-REVOKED）
  ├─ 5. 检查 capabilities 包含节点要求的能力（不足→ NIP-CERT-CAPABILITY-MISSING）
  └─ 6. 检查 scope.nodes 覆盖目标节点路径（不覆盖→ NWP-AUTH-NID-SCOPE-VIOLATION）
        全部通过 → 请求授权
```

只要 `lineage.parent_nid` 存在，步骤 **3a**（链路检查）即为强制项，与会话 NID 自身是否仍在有效期内无关。它与 §5.3 中由 CA 侧执行的级联吊销（`parent_revoked` 原因）共同构成纵深防御：即便 CA 漏写级联记录，验证方也能在此步拦截；而正确执行级联的 CA 仍会把这些条目发布到 CRL，让简单消费者也能感知。

**TrustFrame 链说明（§5.2）**：当 IdentFrame 的 `issued_by` 并不直接出现在 Node 的 `trusted_issuers` 中时，步骤 3 仍可通过 TrustFrame 链通过。完成步骤 3（CA 签名校验）后，若 IdentFrame 由 `grantee_ca` 颁发，Node SHOULD 验证存在一份未过期且有效的 TrustFrame —— 由 Node `trusted_issuers` 中的 `grantor_nid` 颁发，且覆盖请求所需的能力与目标 Node 路径。TrustFrame 自身 MUST 通过 §5.2 错误表中列出的全部检查 —— 任一失败应直接以对应的 `NIP-TRUST-FRAME-*` 错误码短路准入，而非回退到 `NIP-CERT-UNTRUSTED-ISSUER`。

**入站 RevokeFrame 处理（§5.3）**：RevokeFrame 通过 NIP push 通道异步到达，不属于上方按 IdentFrame 维度的准入流程。Node 收到 RevokeFrame 后 MUST：

```
Node 收到 RevokeFrame（push 通道）
  │
  ├─ R1. 解析帧并对规范化 JSON 验证 signer_nid 签名
  │      （失败 → 回发 ErrorFrame NIP-REVOKE-FRAME-INVALID；不应用本次吊销）
  ├─ R2. 校验 signer_nid 是否有权吊销 target_nid —— 见 §5.3 signer_nid 规则
  │      （失败 → 回发 ErrorFrame NIP-REVOKE-FRAME-UNAUTHORIZED-ISSUER；不应用本次吊销）
  ├─ R3. 若 `serial` 存在，校验其与 target_nid 名下已知已颁发证书匹配
  │      （不匹配 → 回发 ErrorFrame NIP-REVOKE-FRAME-SERIAL-MISMATCH；不应用本次吊销）
  ├─ R4. 若 `reason` 不在定义枚举中，按 `key_compromise` 处理（最严格）
  │      并向发布方回送 ErrorFrame NIP-REVOKE-FRAME-REASON-UNKNOWN
  ├─ R5. 对 target_nid 在本地证书 / OCSP 缓存中执行 poison
  │      （若 `serial` 存在则限定到该证书，否则覆盖 target_nid 名下所有证书）
  └─ R6. 不发送任何成功响应 —— RevokeFrame 在协议层为 fire-and-forget。
```

R5 生效后，任何针对 `target_nid`（或被限定的 `serial`）的后续 IdentFrame 将在上方步骤 4 失败，错误码为 `NIP-CERT-REVOKED`。

---

### 7.5 Phase-3 强制校验模式(NIP v0.11)

Phase-3 flag day(`v1.0.0-beta.1`,NPS-RFC-0003 §8.1)将强制的 CA 证明校验,统一收敛到接收端
的一个校验策略开关 **`phase3_enforcement`** 上,部署方 MAY 在 flag day 之前提前开启——
使 flag day 成为一次配置默认值的切换,而不是代码变更。

- **Phase 1–2(当前)** —— `phase3_enforcement` 默认 `false`。接收端 SHOULD 执行下列检查,
  MAY 拒绝;开关为 `false` 时检查失败仅作告警(日志/指标),MUST NOT 单独据此拒绝 IdentFrame。
- **`phase3_enforcement = true`**(现在可选开启;flag day 后为默认)—— 接收端 MUST 对
  X.509 NID 证书(NPS-RFC-0002)强制以下检查。每项检查**仅当对应证书扩展存在时**适用,
  因此不带证明扩展的自声明(Phase 1–2)NID 不受影响:

  | 检查 | 要求 | 错误码 |
  |------|------|--------|
  | Assurance | `IdentFrame.assurance_level` 等于 `id-nid-assurance-level` 扩展 | `NIP-ASSURANCE-MISMATCH` |
  | Node roles | `IdentFrame.node_roles` 的每一项均出现在 `id-nps-node-roles`(`65715.2.2`)中——不得声明未证明的角色 | `NIP-CERT-NODE-ROLES-MISMATCH` |
  | Capabilities | `IdentFrame.capabilities` 的每一项均出现在 `id-nps-capabilities`(`65715.2.3`)中——不得声明未证明的能力 | `NIP-CERT-CAPABILITIES-EXCEEDED` |
  | OCSP staple | `IdentFrame.ocsp_staple` 存在且未过期(`nextUpdate` 在未来) | `NIP-OCSP-STAPLE-EXPIRED` |

- **Flag day(`v1.0.0-beta.1`)** —— `phase3_enforcement` 默认变为 `true`;
  `public-federated` 部署(NDP §7)MUST NOT 将其关闭。`local-dev` / `org-private` MAY 保持 `false`。

角色与能力检查为**子集**检查(frame 声明不得超过 CA 证明范围);少声明是允许的。这在不改变
wire 格式的前提下,收口了 Phase 1–2 遗留的自声明缺口(§5.1、NPS-2 §12.4)。

### 7.6 可移植 CA 与验证 Profile（NIP v0.13）

本 profile 使 §7 验证方与开源 CA 生命周期在各 SDK 之间保持确定性。语言无关的
[`revocation_policy_vectors.json`](conformance/nip/revocation_policy_vectors.json)
与 [`signed_crl_vectors.json`](conformance/nip/signed_crl_vectors.json)
用例为规范性要求。

**验证顺序**

完整验证方 MUST 按以下顺序执行适用的检查，并在第一个失败处停止：

1. 有效期；
2. 直接受信任的颁发者，或一条有效的 pin 住 grantor 的 TrustFrame 路径；
3. 帧签名、可选的 X.509 链，以及 Phase-3 检查；
4. 存在 lineage 时的父 / 组状态；
5. 实时吊销策略；
6. 所需能力；
7. 目标 Node scope。

对外结果沿用既有的 §7 步骤编号：实时吊销仍为步骤 4，能力为步骤 5，scope 为步骤 6。

**吊销模式**

接收方 MUST 暴露带两个取值的 `revocation_mode` 策略：

- `if_configured` 是 alpha 期的兼容默认值。未配置任何吊销源时，记录一条诊断信息后继续验证。
- `required` 在未配置任何吊销源时以 `NIP-OCSP-UNAVAILABLE` 拒绝。`public-federated` 部署 MUST 使用该模式。

已配置的吊销源按以下固定顺序查询：

1. 已验证的本地 CRL / 缓存；
2. 实时吊销回调；
3. 等价于 `INipCaStore` 的按 serial 查询；
4. CA 声明的 OCSP 端点。

结果为已吊销时立即以 `NIP-CERT-REVOKED` 停止。已配置吊销源出现异常、传输失败、非成功 HTTP
响应或响应格式错误时，MUST fail closed 并返回 `NIP-OCSP-UNAVAILABLE`。既有的 `ocsp_fail_open`
兼容逃生口仅适用于 OCSP 可用性失败；它 MUST 默认为 `false`，且 MUST NOT 在 `public-federated`
部署中开启。一份已配置且当前有效、但不含该 serial 的本地 CRL，即可在无需在线查询的情况下满足
`required` 模式。

**可移植的开源 CA 生命周期**

- Register MUST 拒绝重复 NID 与重复 serial。
- Renew MUST 追加新 serial，同时保留先前记录以供审计。
- Revoke MUST 更新当前存活记录，并使其对实时验证与下一次 CRL 响应可见。
- CA store MUST 支持按 NID 与 serial 查询、已吊销枚举、父级枚举与全量枚举。管理侧的全量枚举通过
  `GET /v1/certificates` 暴露，需 Operator 授权。
- `/.well-known/nps-ca.endpoints.ocsp` MUST 指向一个真实提供服务的端点。CA MUST 省略自身不提供的端点。

`GET /v1/crl` 返回一份带签名的 JSON 制品：

```json
{
  "issued_by": "urn:nps:org:ca.example.com",
  "issued_at": "2026-07-29T01:00:00Z",
  "entries": [
    {
      "nid": "urn:nps:agent:ca.example.com:agent-001",
      "serial": "0x0001",
      "revoked_at": "2026-07-29T00:00:00Z",
      "reason": "key_compromise"
    }
  ],
  "signature": "ed25519:..."
}
```

签名覆盖去掉 `signature` 并对 `issued_by`、`issued_at`、`entries` 内容递归排序后形成的 RFC 8785
规范化 JSON 对象。客户端 MUST 先用 `/.well-known/nps-ca` 提供的 CA 公钥验证该签名，才能把这些
条目当作本地吊销源使用。条目 MUST 按 `(revoked_at, serial, nid)` 升序输出，使相同的 CA 状态与
`issued_at` 产出确定性的制品内容。

本 profile 新增一个管理类 HTTP 端点，不新增任何帧字段或错误码。

### 7.7 alpha.19 吊销 freshness 与 advisory Profile（NIP v0.15）

6. **NIP-P19-06 —— freshness。** CRL、cache entry、staple 或 OCSP result 只有在
   `this_update <= now < next_update` 时才为 current；到达边界即 stale，不能满足
   `revocation_mode = required`。
7. **NIP-P19-07 —— stale fallback。** 固定 §7.6 source 顺序中，若后续已配置 source
   能回答，则跳过 stale source。若没有后续 source 产生 current 且 definitive 的结果，
   以 `NIP-REVOCATION-STATE-STALE` fail closed。
8. **NIP-P19-08 —— unknown 与 timeout。** definitive OCSP `unknown` 结果以
   `NIP-OCSP-UNKNOWN` fail closed。timeout、transport failure、畸形数据与非成功 HTTP
   仍映射 `NIP-OCSP-UNAVAILABLE`。`ocsp_fail_open` 不得把 `revoked`、`unknown`、
   stale 或签名非法数据转换为成功。
9. **NIP-P19-09 —— cache replacement。** current result 只有通过签名和 interval
   校验后才能替换同 issuer/serial 的旧结果；`this_update` 更早的响应不得回滚 cache。
10. **NIP-P19-10 —— Phase-3 advisory。** advisory evaluation 返回稳定排序的
    Phase-3 拒绝检查列表，每项包含 error code 与 field。它 MUST NOT 改变
    `phase3_enforcement`、修改 trust/revocation state，或拒绝当前 Phase 1–2 请求。

---

### 7.8 认证调用投影（NIP v0.16）

以下要求定义 experimental 的 `nps.invocation_context` revision 1，
不改变 IdentFrame 签名或证书格式。

#### 7.8.1 内存对象

分离调用方事实（request_id、traceparent、期望 deadline、idempotency_key、有界 hints）
和准入权限（subject NID、验证后的 assurance、capabilities、部署 scope、issuer、
session/transport 来源）。调用方只能缩短服务端期限。幂等 key 不是身份。
Native 权限来自 NIP session 准入，不来自 Hello.agent_id。本地开发权限必须由服务端
显式部署模式启用，远程请求不能选择。不能把请求反序列化为已准入权限。

#### 7.8.2 编码与字段

保留 feature ID `nps.invocation_context` revision 1；完成向量和发布门槛前为 experimental。
两跳均要求支持。单个 `X-NPS-Invocation-Context` header 是 UTF-8 JSON
`{"payload":{...},"signature":"..."}` 的无填充 base64url。

拒绝重复 header/key、非规范 base64url、未知字段、无效 Unicode、非 I-JSON 数值；
解码后最多 16 KiB 或更小本地上限。payload 仅包含以下字段：

| 字段 | 类型和约束 |
|---|---|
| profile | 固定 nps.invocation_context |
| revision | 整数 1 |
| issuer | 准入网关 NID，1..512 UTF-8 字节 |
| key_id | 配置 key 标识，匹配 `[A-Za-z0-9._:-]{1,128}` |
| audience | 精确逻辑接收服务 ID，1..512 UTF-8 字节 |
| subject | 原始主体 NID，1..512 UTF-8 字节 |
| assurance | anonymous / attested / verified，受接收方许可约束 |
| capabilities | 排序去重，最多 64 项，每项 1..128 UTF-8 字节 |
| scope | 部署注册的 opaque scope ID，排序去重，最多 64 项，每项 1..256 UTF-8 字节 |
| issued_at / expires_at | Unix 秒整数 |
| projection_id | 16 个密码学随机字节的 32 位小写 hex |
| request_digest | sha256: 加 64 位小写 hex |

scope 不是策略表达式；通配符不自动授予权限。字符串精确比较，不暗中大小写折叠或
Unicode normalization。签名输入为 UTF-8 `NPS-CONTEXT-PROJECTION/1`、一个零字节、
RFC 8785/JCS payload。签名是 64 字节 Ed25519 的无填充 base64url。
不允许从输入选择算法或嵌入 key；只使用配置中的 (issuer,key_id)。不改变 IdentFrame。

#### 7.8.3 请求摘要

SHA-256 输入为 UTF-8 `NPS-INVOKE-REQUEST/1`、一个零字节、以下精确对象的 JCS：
protocol、operation、target、idempotency_key、required_features、body。
protocol/operation 使用注册标识；target 为解析后的精确逻辑服务/资源；
idempotency_key 为字符串或 null；required_features 按 ID 排序且无重复。

JSON body 为 `{"encoding":"json","value":<I-JSON>}`；
二进制为 `{"encoding":"bytes","content_type":"<注册的规范媒体类型>","sha256":"sha256:<hex>"}`。
未注册/歧义媒体类型拒绝。影响操作语义的 query 参数必须纳入 value；排除 trace 和
投影本身。注册的操作 schema 必须包含影响权限/副作用的全部输入，不得暗用 header。
完整 JSON 示例见英文 §7.8.3。签发方与接收方必须解析出相同操作、目标、body。
网关改变任何语义字段或 required_features 时必须重新认证并签发下一跳投影。

#### 7.8.4 时间、对端与重放

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

#### 7.8.5 确定性边界与载体绑定

JSON revision 和时间戳字段必须使用十进制整数数字语法，不含小数或指数，
并遵守前述取值范围。

Capabilities 和 scope 必须按无符号 UTF-8 字节顺序排序，精确拒绝重复；
此数组排序不同于 JCS 对象 key 排序。Issuer 和 subject 必须满足既有 NIP 的
NID 语法。Protocol 是 1..32 字节的 ASCII 注册标识，operation 为 1..256
UTF-8 字节，target 为 1..512 UTF-8 字节；非 null 幂等 key 为 1..128 UTF-8
字节，且须满足其操作 schema。必需特性对遵循 NCP §2.6.3 类型，按 ID 排序、
唯一且最多 32 项。完整规范化请求摘要对象不得超过 1048576 字节；
现有更小请求/帧上限仍适用。

NWP §9.7 定义 HTTP/原生载体及精确逻辑请求构造。两者使用相同 envelope 和
签名算法。投影不是 bearer 凭据，缺少独立认证通道的 issuer 绑定时不得接受。
公网或不可信入站投影载体必须先移除，网关再从本地已准入权限生成新投影。

验证器对 schema/编码/尺寸/非法生命周期/未来签发返回 NIP-CONTEXT-INVALID；
签名/key/issuer 错误返回 NIP-CONTEXT-UNTRUSTED；
audience/subject/请求绑定或委托策略不匹配返回 NIP-CONTEXT-BINDING-MISMATCH；
过期返回 NIP-CONTEXT-EXPIRED；有效投影重复返回 NIP-CONTEXT-REPLAY；
验证或重放基础设施不可用返回 NIP-CONTEXT-VERIFIER-UNAVAILABLE。
这些结果都禁止业务派发。错误详情必须有界，不得暴露 envelope 或敏感 scope。

---

## 8. NIP CA Server OSS API

| 方法 | 路径 | 认证 | 描述 |
|------|------|------|------|
| POST | `/v1/agents/register` | Operator Cert | 注册 Agent，返回 NID + IdentFrame |
| POST | `/v1/agents/{nid}/renew` | Agent Cert | 续期证书（到期前 7 天可续）|
| POST | `/v1/agents/{nid}/revoke` | Operator Cert | 吊销 NID |
| GET | `/v1/agents/{nid}/verify` | 无 | 验证 NID 有效性（OCSP 查询）|
| POST | `/v1/nodes/register` | Operator Cert | 注册 NWP 节点，颁发 Node Certificate |
| POST | `/v1/orchestrators/groups/register` | Operator Cert | （NPS-CR-0003）注册编排器组，返回带 `lineage.role = "group"` 的 IdentFrame |
| POST | `/v1/orchestrators/groups/{group_nid}/sessions/issue` | Group JWS 或 Operator Cert | （NPS-CR-0003）在该组下颁发短寿命会话 NID |
| POST | `/v1/orchestrators/groups/{group_nid}/revoke` | Operator Cert | （NPS-CR-0003）吊销该组，并级联吊销其下所有仍存活的会话 |
| GET | `/v1/orchestrators/groups/{group_nid}/sessions` | Operator Cert | （NPS-CR-0003）列出该组已颁发的会话（审计用途）|
| GET | `/v1/ca/cert` | 无 | CA 公钥证书 |
| GET | `/v1/certificates` | Operator Cert | 枚举已颁发的证书记录，供审计使用 |
| GET | `/v1/crl` | 无 | 证书吊销列表 |
| GET | `/.well-known/nps-ca` | 无 | CA 发现端点 |

**`/.well-known/nps-ca` 响应**

```json
{
  "nps_ca": "0.1",
  "issuer": "urn:nps:org:ca.mycompany.com",
  "display_name": "MyCompany NPS CA",
  "public_key": "ed25519:MCowBQYDK2VwAyEA...",
  "algorithms": ["ed25519", "ecdsa-p256"],
  "endpoints": {
    "register": "https://ca.mycompany.com/v1/agents/register",
    "verify":   "https://ca.mycompany.com/v1/agents/{nid}/verify",
    "node_verify": "https://ca.mycompany.com/v1/nodes/{nid}/verify",
    "crl":      "https://ca.mycompany.com/v1/crl"
  },
  "capabilities": ["agent", "node", "operator", "orchestrator-group"],
  "max_cert_validity_days": 30
}
```

### 8.1 Registration Authority（RA）端点（NPS-CR-0005，Implemented）

NPS-CR-0005 引入三档 Registration Authority（RA）模型，**在上文 Operator 凭证的
`POST /v1/agents/register` 流程之外**，再提供若干由策略驱动的 Agent 准入路径。默认档位仍为
`operator_only` —— 新档位通过 `NipCaOptions.EnrollmentTier` 显式开启，并新增以下端点：

| 方法 | 路径 | 认证 | 描述 |
|------|------|------|------|
| POST | `/v1/enrollment/tokens` | Operator Cert | （NPS-CR-0005，Tier 2）签发一次性、按 NID 限定 scope 的 bootstrap token。|
| GET  | `/v1/enrollment/pending` | Operator Cert | （NPS-CR-0005，Tier 3）列出等待管理员裁决的注册请求。|
| POST | `/v1/enrollment/pending/{id}/approve` | Operator Cert | （NPS-CR-0005，Tier 3）批准一条待决请求并颁发 IdentFrame。|
| POST | `/v1/enrollment/pending/{id}/reject` | Operator Cert | （NPS-CR-0005，Tier 3）以运营方给出的理由拒绝一条待决请求。|

三个档位（allowlist / bootstrap token / pending queue）、错误码（`NIP-RA-TOKEN-INVALID`、
`NIP-RA-TOKEN-EXPIRED`、`NIP-RA-NID-NOT-ALLOWED`、`NIP-RA-PENDING-REJECTED`）以及
`NipCaOptions.EnrollmentTier` 选择器，由 **[NPS-CR-0005](./cr/NPS-CR-0005-nip-ca-ra-model.md)**
规范性定义；请求 / 响应结构与验证流程已由六个 SDK 的 CA surface 实现。本节是 API 索引，
不是未来工作占位符。

---

## 9. 错误码

| 错误码 | NPS 状态码 | 描述 |
|--------|-----------|------|
| `NIP-CERT-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | 证书已过期 |
| `NIP-CERT-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | 证书已吊销 |
| `NIP-CERT-SIGNATURE-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | 证书签名验证失败 |
| `NIP-CERT-UNTRUSTED-ISSUER` | `NPS-AUTH-UNAUTHENTICATED` | 颁发者不在信任列表 |
| `NIP-CERT-CAPABILITY-MISSING` | `NPS-AUTH-FORBIDDEN` | 证书缺少所需能力 |
| `NIP-CERT-SCOPE-VIOLATION` | `NPS-AUTH-FORBIDDEN` | 证书 scope 不覆盖目标路径 |
| `NIP-CERT-CAPABILITIES-EXCEEDED` | `NPS-AUTH-FORBIDDEN` | `IdentFrame.capabilities` 声明了 CA 背书的 `id-nps-capabilities` 扩展中不存在的能力；Phase-3 强制校验（NIP v0.12 §7.5）|
| `NIP-CA-NID-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | NID 不存在 |
| `NIP-CA-NID-ALREADY-EXISTS` | `NPS-CLIENT-CONFLICT` | NID 已存在（重复注册）|
| `NIP-CA-SERIAL-DUPLICATE` | `NPS-CLIENT-CONFLICT` | 证书序列号已存在 |
| `NIP-CA-RENEWAL-TOO-EARLY` | `NPS-CLIENT-BAD-PARAM` | 尚未到续期窗口 |
| `NIP-CA-SCOPE-EXPANSION-DENIED` | `NPS-AUTH-FORBIDDEN` | 请求 scope 超出父级 scope |
| `NIP-OCSP-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | OCSP 服务暂不可用 |
| `NIP-OCSP-STAPLE-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | `IdentFrame.ocsp_staple` 的 `nextUpdate` 已过期 —— staple 已陈旧，Agent 必须刷新后重发（NIP v0.9 §5.1.4）|
| `NIP-OCSP-UNKNOWN` | `NPS-AUTH-UNAUTHENTICATED` | 合法 OCSP response 无法确认请求的 issuer/serial 状态，因此 fail closed |
| `NIP-REVOCATION-STATE-STALE` | `NPS-SERVER-UNAVAILABLE` | 没有已配置吊销源产出 current result；stale state 不能满足 required mode |
| `NIP-CERT-NODE-ROLES-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | `IdentFrame.node_roles` 与 `id-nps-node-roles` X.509 扩展不一致；Phase 3 强制校验（NIP v0.10）|
| `NIP-TRUST-FRAME-INVALID` | `NPS-CLIENT-BAD-FRAME` | TrustFrame 签名或格式不合法 —— 见 §5.2 |
| `NIP-TRUST-FRAME-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | TrustFrame `expires_at` 已过期 —— 见 §5.2 |
| `NIP-TRUST-FRAME-GRANTOR-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | TrustFrame `grantor_nid` 自身的 CA 证书已被吊销或过期 —— 见 §5.2 |
| `NIP-TRUST-FRAME-SCOPE-EXCEEDS-GRANTOR` | `NPS-AUTH-FORBIDDEN` | TrustFrame `trust_scope` 包含 grantor 自身不持有的能力（不可扩大 scope 原则，§10.3）—— 见 §5.2 |
| `NIP-TRUST-FRAME-NODES-PATTERN-INVALID` | `NPS-CLIENT-BAD-FRAME` | TrustFrame `nodes` 数组某项不是合法的 `nwp://` URL 模式 —— 见 §5.2 |
| `NIP-REVOKE-FRAME-INVALID` | `NPS-CLIENT-BAD-FRAME` | RevokeFrame 不合法（缺必填字段、签名校验失败或规范化形式错误）—— 见 §5.3 |
| `NIP-REVOKE-FRAME-UNAUTHORIZED-ISSUER` | `NPS-AUTH-FORBIDDEN` | RevokeFrame `signer_nid` 无权吊销 `target_nid` —— 见 §5.3 |
| `NIP-REVOKE-FRAME-SERIAL-MISMATCH` | `NPS-CLIENT-BAD-PARAM` | RevokeFrame `serial` 存在但与 `target_nid` 当前已颁发证书的 serial 都不匹配 —— 见 §5.3 |
| `NIP-REVOKE-FRAME-REASON-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | RevokeFrame `reason` 取值不在定义的枚举中；接收方按 `key_compromise` 处理 —— 见 §5.3 |
| `NIP-ASSURANCE-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | `IdentFrame.assurance_level` 与证书扩展 `id-nid-assurance-level` 不一致（防 downgrade 攻击）—— 见 §5.1.1 |
| `NIP-ASSURANCE-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | `assurance_level` 取值不在定义枚举（`anonymous` / `attested` / `verified`）之中 —— 见 §5.1.1 |
| `NIP-REPUTATION-ENTRY-INVALID` | `NPS-CLIENT-BAD-FRAME` | 声誉日志条目签名校验失败或规范化形式不合法 —— 见 §5.1.2（NPS-RFC-0004）|
| `NIP-REPUTATION-LOG-UNREACHABLE` | `NPS-DOWNSTREAM-UNAVAILABLE` | 准入评估时无法到达 Node `reputation_policy` 引用的某个日志运营方 —— 见 §5.1.2（NPS-RFC-0004）|
| `NIP-CA-GROUP-REVOKED` | `NPS-AUTH-FORBIDDEN` | 不能在已吊销的组 NID 下颁发会话 —— 见 §5.1.3（NPS-CR-0003）|
| `NIP-CA-PARENT-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | 会话颁发请求引用的 `parent_nid` / 组 NID 不存在 —— 见 §5.1.3（NPS-CR-0003）|
| `NIP-CA-PARENT-NOT-GROUP` | `NPS-CLIENT-BAD-PARAM` | 引用的父 NID 存在，但未注册为 `lineage.role = "group"` —— 见 §5.1.3（NPS-CR-0003）|
| `NIP-CA-SESSION-VALIDITY-INVALID` | `NPS-CLIENT-BAD-PARAM` | 请求的会话有效期低于 60 秒或超过 CA 配置的上限 —— 见 §5.1.3（NPS-CR-0003）|
| `NIP-CA-JWS-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | 会话颁发请求上的 Group-JWS 授权未通过签名、头部或结构校验 —— 见 §5.1.3（NPS-CR-0003）|
| `NIP-CA-JWS-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | Group-JWS 的 `iat` 落在 CA 时钟偏差窗口外（默认 ±5 分钟）—— 见 §5.1.3（NPS-CR-0003）|
| `NIP-CERT-PARENT-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | 会话 NID 的父 / 组 NID 已被吊销或过期（链路检查，§7 步骤 3a）—— 见 §5.1.3（NPS-CR-0003）|

HTTP 模式下的状态码映射见 [status-codes.cn.md](status-codes.cn.md)。

---

## 10. 安全考量

### 10.1 密钥存储
CA 私钥 MUST 存储于 HSM 或加密密钥文件（AES-256-GCM）。OSS 实现使用加密文件并预留 HSM 接口。

### 10.2 时序攻击防御
OCSP 响应时间 SHOULD 归一化（固定延迟至 200ms），防止通过响应时间推断证书状态。

### 10.3 Scope 不可扩大原则
委托链中任何环节的 scope MUST NOT 超过其父级 scope。CA MUST 在颁发证书时强制校验。

---

## 11. 变更历史

| 版本 | 日期 | 变更 |
|------|------|------|
| 0.16 | 2026-09-09 | Alpha.20 experimental 认证调用投影：有界 JCS/Ed25519 envelope、完整请求绑定、可信通道 issuer、委托上限及持久化原子重放准入。 |
| 0.15 | 2026-08-31 | alpha.19 续期/吊销加固：包含边界的标准/短时续期窗口、身份连续性、原子且幂等的颁发、旧证书 ticket bound、显式吊销 freshness 与 stale fallback、OCSP unknown fail closed、防回滚 cache replacement、确定性且不强制执行的 Phase-3 advisory 输出，以及共享向量。将 §8.1 CR-0005 RA 状态从 stub 订正为 Implemented；Depends-On 推进至 NCP 0.12。|
| 0.14 | 2026-08-12 | 新增标准 `llm:context` capability，用于 NWP §7.6 有状态 LLM context 生命周期，并允许 TrustFrame `trust_scope` 覆盖它。Completion mutation 还要求 `llm:complete`；owner 授权的 status/release 只需 `llm:context`。Context ID 只是 locator，不是 bearer authorization。不新增 frame 字段或 NIP 错误码。 |
| 0.13 | 2026-07-29 | 新增 §7.6 **可移植 CA 与验证 Profile**：确定性的验证顺序与吊销源顺序、`if_configured` 与 fail-closed 的 `required` 两种吊销模式、带签名的确定性 CRL 语义、CA store 全量枚举，以及需鉴权的 `GET /v1/certificates`。新增共享的吊销策略与签名 CRL 测试向量。无新增 frame 字段或错误码。|
| 0.12 | 2026-07-23 | 新增 §7.5 **Phase-3 强制模式**：接收侧 `phase3_enforcement` 验证策略开关，把 Phase-1–2 可选的 CA 声明校验（assurance / node_roles / capabilities / OCSP-staple）在 `v1.0.0-beta.1` flag day 之前提前变为硬性 MUST —— flag day 因此只是默认值切换而非代码变更。角色/能力检查为子集检查（不得声称未经 CA 见证的内容）；各检查仅在对应证书扩展存在时生效（自声明 NID 不受影响）。新增错误码 `NIP-CERT-CAPABILITIES-EXCEEDED`（`NPS-AUTH-FORBIDDEN`）。增量、向后兼容。（由 edge 线 0.11 重编号 —— 已发布的 alpha.16 线独立地把 0.11 用于下面的 LLM capability 字符串。）|
| 0.11 | 2026-07-04 | 新增标准 LLM capability 字符串（`llm:complete`、`llm:stream`、`llm:tool_call`、`llm:embed`、`llm:rerank`），用于 NWP LLM/Thinking Profile 的发现与授权。TrustFrame `trust_scope` 可覆盖这些能力。无新增 frame 字段或错误码。 |
| 0.10 | 2026-06-12 | 新增 §6.1 **边缘 mTLS 短时/可续期证书 profile**：1–24 小时有效期（默认 6 小时），用于 `nps-ingress`（L2）终结的原生模式 mTLS（NPS-RFC-0006 §6.3）；续期窗口 = 剩余有效期 25%；ACME `agent-01` 在线自续期；有效期 ≤ OCSP 缓存 TTL，以短有效期作为主要吊销手段；OCSP staple 刷新交互（`NIP-OCSP-STAPLE-EXPIRED`）；恢复票据受证书有效期约束（NPS-RFC-0006 §6.4）。复用现有错误码（`NIP-CA-SESSION-VALIDITY-INVALID`、`NIP-CERT-EXPIRED`）；无新错误码。 |
| 0.10 | 2026-06-03 | `IdentFrame.node_roles`（`string[]`，可选）—— 自声明角色标签，词汇与 `NDP.AnnounceFrame.node_roles` 相同；Phase 3 起对 `id-nps-node-roles` X.509 扩展（OID 65715.2.2）做门控校验；新增错误码 `NIP-CERT-NODE-ROLES-MISMATCH`。|
| 0.9 | 2026-05-31 | `IdentFrame.ocsp_staple`（base64url DER OCSPResponse）：§5.1.4 装订规则（验签、`nextUpdate` 检查、`NIP-OCSP-STAPLE-EXPIRED`）；X.509 OID 表：`id-nid-assurance-level` 65715.2.1、`id-nps-node-roles` 65715.2.2、`id-nps-capabilities` 65715.2.3（PEN 65715 —— NPS-CR-0004）。|
| 0.8 | 2026-05-11 | 扩展 §5.2 TrustFrame：补全字段定义表（10 个字段，新增必填的 `issued_at` / `serial` / `signer_nid` 用于吊销与审计追溯）、签名规范化规则（与 §5.1 IdentFrame 一致）、错误码子表与完整示例。§7 新增 TrustFrame 验证说明（IdentFrame 的 `issued_by` 是 `grantee_ca` 时，存在 `trusted_issuers` 中 `grantor_nid` 颁发、覆盖该请求的有效 TrustFrame 即可准入）。§9 新增 4 个错误码：`NIP-TRUST-FRAME-EXPIRED`、`NIP-TRUST-FRAME-GRANTOR-REVOKED`、`NIP-TRUST-FRAME-SCOPE-EXCEEDS-GRANTOR`、`NIP-TRUST-FRAME-NODES-PATTERN-INVALID`。|
| 0.7 | 2026-05-07 | **NPS-CR-0003**：编排器组 NID 与短寿命会话 NID。新增 §3.1，在 `entity-type = agent` 上保留 `group-` / `session-` 标识符前缀。新增带签名的 `IdentFrame.lineage` 对象，含 `role` / `parent_nid` / `group_nid` / `session_id` / `purpose` / `owner_user_id` / `owner_key_id`（§5.1.3）。新增 RevokeFrame 原因 `parent_revoked`（§5.3）。验证流程新增链路检查步骤 **3a**（§7）。CA Server 新增 4 个端点 `/v1/orchestrators/groups/...`（§8）。新增 7 个错误码（§9）：`NIP-CA-GROUP-REVOKED`、`NIP-CA-PARENT-NOT-FOUND`、`NIP-CA-PARENT-NOT-GROUP`、`NIP-CA-SESSION-VALIDITY-INVALID`、`NIP-CA-JWS-INVALID`、`NIP-CA-JWS-EXPIRED`、`NIP-CERT-PARENT-REVOKED`。普通单 NID 注册流程完全向后兼容；编排器场景为可选启用。|
| 0.6 | 2026-05-01 | 在标准 `capabilities` 注册表中新增 `topology:read`（§5.1 能力表）。Anchor Node 在 Phase 1–2 需以此能力门控拓扑读操作（`topology.snapshot` / `topology.stream`），对应 NPS-2 §12.4 最低授权绑定（M6）。Phase 1–2 为自声明并密钥签名；CA 认证角色绑定（`id-nps-node-roles` 证书扩展）推迟到 Phase 3，待 RFC-0002 稳定后落地。 |
| 0.5 | 2026-04-26 | 新增 §5.1.2 声誉日志条目 —— Certificate-Transparency 风格的 append-only NID 行为记录 wire 格式。Phase 1 落地条目结构（12 字段，含基于 JCS 规范化的双签名要求）、初始 8 项 `incident` 枚举（`cert-revoked` / `rate-limit-violation` / `tos-violation` / `scraping-pattern` / `payment-default` / `contract-dispute` / `impersonation-claim` / `positive-attestation`）、5 级 `severity` 枚举。新增错误码 `NIP-REPUTATION-ENTRY-INVALID` 与 `NIP-REPUTATION-LOG-UNREACHABLE`。Merkle / STH / inclusion-proof 接口推迟到 Phase 2，详见 [NPS-RFC-0004](rfcs/NPS-RFC-0004-nid-reputation-log.cn.md) §8.1。|
| 0.4 | 2026-04-25 | 新增 §5.1.1 保证等级（`anonymous` / `attested` / `verified`）；IdentFrame 增加可选 `assurance_level` 字段，缺失向后兼容默认 `anonymous`；新增错误码 `NIP-ASSURANCE-MISMATCH`、`NIP-ASSURANCE-UNKNOWN`。详见 [NPS-RFC-0003](rfcs/NPS-RFC-0003-agent-identity-assurance-levels.cn.md)。`Depends-On` NCP 版本订正为 `v0.6`（NPS-RFC-0001）。 |
| 0.2 | 2026-04-12 | 统一端口 17433；IdentFrame 增加 metadata 字段（tokenizer 自动匹配）；错误码改用 NPS 状态码映射；完善错误码列表 |
| 0.1 | 2026-04-10 | 初始规范：NID 格式、IdentFrame/TrustFrame/RevokeFrame、CA Server API、验证流程 |

---

*归属：LabAcacia / INNO LOTUS PTY LTD · Apache 2.0*
