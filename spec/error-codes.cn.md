[English Version](./error-codes.md) | 中文版

# NPS 统一错误码命名空间

**Version**: 1.12
**Date**: 2026-11-02

错误码格式：`{PROTOCOL}-{CATEGORY}-{DETAIL}`

NPS 采用两级错误体系：
1. **协议错误码**（本文档）：具体错误标识，前缀标识所属协议
2. **NPS 状态码**：传输层状态分类与 HTTP 映射，见 [status-codes.cn.md](status-codes.cn.md)

---

## NCP 错误码

| 错误码 | NPS 状态码 | 描述 |
|--------|-----------|------|
| `NCP-FEATURE-NEGOTIATION-INVALID` | `NPS-CLIENT-BAD-FRAME` | 特性协商元数据或选择无效 |
| `NCP-FEATURE-REQUIRED-UNSUPPORTED` | `NPS-CLIENT-CONFLICT` | 必需原生特性没有合格共同 revision |
| `NCP-ANCHOR-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | anchor_ref 引用的 Schema 不存在，Agent 须重新获取 AnchorFrame |
| `NCP-ANCHOR-SCHEMA-INVALID` | `NPS-CLIENT-BAD-FRAME` | AnchorFrame 中 Schema 格式不合法 |
| `NCP-ANCHOR-ID-MISMATCH` | `NPS-CLIENT-CONFLICT` | 相同 anchor_id 收到不同 Schema（锚点污染攻击防御）|
| `NCP-FRAME-UNKNOWN-TYPE` | `NPS-CLIENT-BAD-FRAME` | 未知帧类型码 |
| `NCP-FRAME-PAYLOAD-TOO-LARGE` | `NPS-LIMIT-PAYLOAD` | Payload 超过协商的 max_frame_payload |
| `NCP-FRAME-FLAGS-INVALID` | `NPS-CLIENT-BAD-FRAME` | Flags 字段中保留位非零 |
| `NCP-BINARY-VECTOR-MALFORMED` | `NPS-CLIENT-BAD-FRAME` | Tier-3 BinaryVector Payload 格式不合法 |
| `NCP-BINARY-VECTOR-DIM-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | BinaryVector marker 的维度与向量段不匹配 |
| `NCP-BINARY-VECTOR-INDEX-INVALID` | `NPS-CLIENT-BAD-FRAME` | BinaryVector marker 引用了不存在的向量段 |
| `NCP-BINARY-VECTOR-DTYPE-UNSUPPORTED` | `NPS-CLIENT-BAD-FRAME` | BinaryVector marker 使用了不支持的 dtype |
| `NCP-BINARY-VECTOR-TRUNCATED` | `NPS-CLIENT-BAD-FRAME` | BinaryVector 向量段被截断 |
| `NCP-STREAM-SEQ-GAP` | `NPS-STREAM-SEQ-GAP` | StreamFrame 序号不连续 |
| `NCP-STREAM-NOT-FOUND` | `NPS-STREAM-NOT-FOUND` | stream_id 引用的流不存在 |
| `NCP-STREAM-LIMIT-EXCEEDED` | `NPS-STREAM-LIMIT` | 超出单连接最大并发流数 |
| `NCP-ENCODING-UNSUPPORTED` | `NPS-SERVER-ENCODING-UNSUPPORTED` | 请求的编码 Tier 不被支持 |
| `NCP-ANCHOR-STALE` | `NPS-CLIENT-CONFLICT` | anchor_ref 存在但 Schema 已更新；响应通过 CapsFrame.inline_anchor 携带最新 AnchorFrame |
| `NCP-DIFF-FORMAT-UNSUPPORTED` | `NPS-CLIENT-BAD-FRAME` | DiffFrame 使用了接收方不支持的 patch_format（如 binary_bitset）|
| `NCP-VERSION-INCOMPATIBLE` | `NPS-PROTO-VERSION-INCOMPATIBLE` | 客户端 min_version 高于 Server 支持的最高版本（握手失败）|
| `NCP-STREAM-WINDOW-OVERFLOW` | `NPS-STREAM-LIMIT` | 发送方在应用层流量控制窗口耗尽后继续发送 StreamFrame |
| `NCP-ENC-NOT-NEGOTIATED` | `NPS-CLIENT-BAD-FRAME` | 收到 ENC=1 帧，但会话未协商 E2E 加密算法（HelloFrame 中未声明）|
| `NCP-ENC-AUTH-FAILED` | `NPS-CLIENT-BAD-FRAME` | E2E 加密 Auth Tag 验证失败，帧可能被篡改 |
| `NCP-PREAMBLE-INVALID` | `NPS-PROTO-PREAMBLE-INVALID` | 原生模式连接首 8 字节非常量前导 `b"NPS/1.0\n"`；服务端静默关闭连接，不返回 ErrorFrame（NPS-RFC-0001） |
| `NCP-NID-MISMATCH` | `NPS-AUTH-UNAUTHENTICATED` | 原生模式 mTLS 客户端证书 NID 与会话 `IdentFrame` NID 不一致，或恢复会话的证书 NID 与票据绑定 NID 不一致（NPS-RFC-0006 §6.3–§6.4） |
| `NCP-KEEPALIVE-TIMEOUT` | `NPS-SERVER-TIMEOUT` | 在 3 × `ping_interval_ms` 内未收到任何帧（含 NopFrame）；连接将被关闭（NCP v0.8 §7.6） |
| `NCP-EARLY-DATA-REJECTED` | `NPS-PROTO-VERSION-INCOMPATIBLE` | NPS 应用数据出现在 QUIC/TLS 0-RTT early data 中，并在握手确认前被拒绝（NCP v0.12 §7.7）|

---

## NWP 错误码

| 错误码 | NPS 状态码 | 描述 |
|--------|-----------|------|
| `NWP-RESULT-IDENTITY-REQUIRED` | `NPS-AUTH-UNAUTHENTICATED` | 结果操作必须有已验证主体 |
| `NWP-RESULT-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | 结果不存在、未授权、过期、释放或不可用；统一披露 |
| `NWP-RESULT-SNAPSHOT-MISMATCH` | `NPS-CLIENT-CONFLICT` | 授权快照/schema/cursor 绑定不匹配 |
| `NWP-RESULT-CAPACITY` | `NPS-LIMIT-RESOURCE` | 有界结果存储无法保留请求结果 |
| `NWP-RESULT-POLICY-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | 必需策略/验证服务不可用；fail closed |
| `NWP-RESULT-REPLAY-CONFLICT` | `NPS-CLIENT-CONFLICT` | owner 绑定键被用于不同冻结请求内容 |
| `NWP-CHALLENGE-INVALID` | `NPS-CLIENT-BAD-FRAME` | Challenge 输入无效 |
| `NWP-CHALLENGE-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | 未知或无权访问的 challenge handle |
| `NWP-CHALLENGE-BINDING-CONFLICT` | `NPS-CLIENT-CONFLICT` | 同操作 key 对应不同摘要 |
| `NWP-CHALLENGE-NOT-APPROVED` | `NPS-CLIENT-CONFLICT` | Challenge 未批准执行 |
| `NWP-CHALLENGE-EXPIRED` | `NPS-CLIENT-CONFLICT` | Challenge 在执行 claim 前过期 |
| `NWP-CHALLENGE-STATE-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | 持久 challenge 状态不可用 |
| `NWP-FEATURE-REQUIREMENTS-INVALID` | `NPS-CLIENT-BAD-FRAME` | HTTP 必需特性头无效或重复 |
| `NWP-FEATURE-REQUIRED-UNSUPPORTED` | `NPS-CLIENT-CONFLICT` | 派发前无法满足精确必需特性 |
| `NWP-AUTH-NID-SCOPE-VIOLATION` | `NPS-AUTH-FORBIDDEN` | Agent scope 不覆盖目标节点路径 |
| `NWP-AUTH-NID-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | NID 证书已过期 |
| `NWP-AUTH-NID-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | NID 已被吊销 |
| `NWP-AUTH-NID-UNTRUSTED-ISSUER` | `NPS-AUTH-UNAUTHENTICATED` | NID 颁发者不在 trusted_issuers 中 |
| `NWP-AUTH-NID-CAPABILITY-MISSING` | `NPS-AUTH-FORBIDDEN` | Agent 缺少节点要求的能力（如 nwp:query）|
| `NWP-AUTH-ASSURANCE-TOO-LOW` | `NPS-AUTH-FORBIDDEN` | Agent 保证等级低于节点 `min_assurance_level`（NPS-2 §4.1）或 ActionSpec 的 per-action 覆盖（§4.6）；响应 SHOULD 在 `hint` 字段附 CA 注册 URL（NPS-RFC-0003）|
| `NWP-AUTH-REPUTATION-BLOCKED` | `NPS-AUTH-FORBIDDEN` | 接收 Node 的声誉策略命中了对发起方 `subject_nid` 的 `reject_on` 规则；响应 SHOULD 携带匹配的 `incident` + `severity` + 日志条目 `seq` 便于追溯（NPS-RFC-0004）|
| `NWP-REPUTATION-THROTTLED` | `NPS-CLIENT-RATE-LIMITED` | 声誉策略的 `throttle_on` 规则命中；响应包含 `Retry-After: 60`（NPS-RFC-0005） |
| `NWP-REPUTATION-REJECTED` | `NPS-AUTH-FORBIDDEN` | 声誉策略的 `reject_on` 规则命中；响应体包含 `matched_incident` 与 `matched_severity`（NPS-RFC-0005） |
| `NWP-REPUTATION-BANNED` | `NPS-AUTH-FORBIDDEN` | 声誉策略的 `ban_on` 规则或有效 ban cache 条目命中；响应 SHOULD 包含 `X-NWP-Ban-Expires`（NPS-RFC-0005） |
| `NWP-QUERY-FILTER-INVALID` | `NPS-CLIENT-BAD-PARAM` | Filter 语法不合法或嵌套超过 8 层 |
| `NWP-QUERY-FIELD-UNKNOWN` | `NPS-CLIENT-BAD-PARAM` | fields 中引用了不存在的字段 |
| `NWP-QUERY-CURSOR-INVALID` | `NPS-CLIENT-BAD-PARAM` | cursor 值无法解码或已过期 |
| `NWP-QUERY-REGEX-UNSAFE` | `NPS-CLIENT-BAD-PARAM` | `$regex` 模式被拒绝（ReDoS 风险、超长或嵌套量词）|
| `NWP-QUERY-VECTOR-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | 节点不支持向量搜索 |
| `NWP-QUERY-AGGREGATE-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | 节点不支持聚合查询（capabilities.aggregate=false）|
| `NWP-QUERY-AGGREGATE-INVALID` | `NPS-CLIENT-BAD-PARAM` | aggregate 结构不合法（未知 func、alias 重复、缺少必填字段等）|
| `NWP-QUERY-STREAM-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | 节点不支持流式查询（capabilities.stream_query=false）|
| `NWP-ACTION-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | action_id 不存在于节点注册表 |
| `NWP-ACTION-PARAMS-INVALID` | `NPS-CLIENT-UNPROCESSABLE` | 操作参数 Schema 校验失败 |
| `NWP-ACTION-IDEMPOTENCY-CONFLICT` | `NPS-CLIENT-CONFLICT` | 相同 idempotency_key 的请求正在进行中 |
| `NWP-LLM-CONTEXT-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | 有状态 LLM context 未知/已 release，或 owner-scoped idempotency lookup 超出保留窗口（NWP §7.6）|
| `NWP-LLM-CONTEXT-EXPIRED` | `NPS-CLIENT-GONE` | Context 存在 idle-expiry tombstone；调用方必须创建或 reset 其他 context（NWP §7.6）|
| `NWP-LLM-CONTEXT-VERSION-CONFLICT` | `NPS-CLIENT-CONFLICT` | `base_version` 陈旧，或其他 mutation 持有 context reservation；hint SHOULD 携带当前版本（NWP §7.6）|
| `NWP-LLM-CONTEXT-BINDING-MISMATCH` | `NPS-CLIENT-CONFLICT` | 解析后的 model、system message、tool definition 或 runtime compatibility revision 与已提交 binding 不一致（NWP §7.6）|
| `NWP-LLM-CONTEXT-FORBIDDEN` | `NPS-AUTH-FORBIDDEN` | 认证 caller 不是 context owner，或缺少所需 scope/capability（NWP §7.6）|
| `NWP-LLM-CONTEXT-LIMIT-EXCEEDED` | `NPS-LIMIT-RESOURCE` | 达到每 principal 活跃 context 上限；hint SHOULD 携带广告上限（NWP §7.6）|
| `NWP-LLM-CONTEXT-OPERATION-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | 节点支持有状态 context，但未广告/实现请求的生命周期 operation（NWP §7.6）|
| `NWP-TASK-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | task_id 引用的异步任务不存在 |
| `NWP-TASK-ALREADY-CANCELLED` | `NPS-CLIENT-CONFLICT` | 任务已被取消，无法继续操作 |
| `NWP-TASK-ALREADY-COMPLETED` | `NPS-CLIENT-CONFLICT` | 任务已完成，无法取消 |
| `NWP-TASK-ALREADY-FAILED` | `NPS-CLIENT-CONFLICT` | 任务已失败，无法取消 |
| `NWP-SUBSCRIBE-STREAM-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | unsubscribe 引用的 stream_id 不存在 |
| `NWP-SUBSCRIBE-LIMIT-EXCEEDED` | `NPS-LIMIT-EXCEEDED` | 超出节点允许的最大并发订阅数 |
| `NWP-SUBSCRIBE-FILTER-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | 节点不支持带 filter 的订阅 |
| `NWP-SUBSCRIBE-INTERRUPTED` | `NPS-SERVER-UNAVAILABLE` | 订阅流因底层数据源中断而终止 |
| `NWP-SUBSCRIBE-SEQ-TOO-OLD` | `NPS-CLIENT-CONFLICT` | resume_from_seq 超出节点缓冲范围（推荐缓冲 10 分钟或 10,000 条）；Agent 须全量重查后重新订阅 |
| `NWP-SUBSCRIBE-LEASE-INVALID` | `NPS-CLIENT-BAD-PARAM` | 可续订订阅 operation 或 lease/policy bounds 非法（NWP v0.22 §13.4）|
| `NWP-SUBSCRIBE-LEASE-EXPIRED` | `NPS-CLIENT-GONE` | 订阅 lease 已到 deadline，不能 renew 或复活（NWP v0.22 §13.4）|
| `NWP-BUDGET-EXCEEDED` | `NPS-LIMIT-BUDGET` | 响应将超过 X-NWP-Budget 限制 |
| `NWP-CGN-LIMIT-EXCEEDED` | `NPS-CLIENT-REQUEST-TOO-LARGE` | 响应会超过有效 CGN 预算 `min(cgn_limit, X-NWP-Budget)`，且无法继续裁剪；响应 SHOULD 包含 `effective_budget` 与 `estimated_cgn`（token-budget.md §7.4） |
| `NWP-DEPTH-EXCEEDED` | `NPS-CLIENT-BAD-PARAM` | X-NWP-Depth 超过节点允许的 max_depth |
| `NWP-GRAPH-CYCLE` | `NPS-CLIENT-UNPROCESSABLE` | 节点图谱中存在循环引用 |
| `NWP-NODE-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | 底层数据源暂不可用 |
| `NWP-MANIFEST-VERSION-UNSUPPORTED` | `NPS-CLIENT-BAD-PARAM` | Agent NPS 版本低于节点要求的 min_agent_version |
| `NWP-MANIFEST-NODE-TYPE-REMOVED` | `NPS-CLIENT-BAD-FRAME` | NWM `node_type` 包含已移除的遗留值 `"gateway"`（NPS-CR-0001）；请改用 `"anchor"` 或 `"bridge"`。响应 SHOULD 携带指向 NPS-CR-0001 的 `hint` 字段。|
| `NWP-MANIFEST-NODE-TYPE-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | NWM `node_type` 包含无法识别的值（非已知遗留移除值——遗留移除情况请用 `NWP-MANIFEST-NODE-TYPE-REMOVED`）。|
| `NWP-RATE-LIMIT-EXCEEDED` | `NPS-LIMIT-RATE` | 超出频率限制，X-NWP-Rate-Reset 头包含重置时间 |
| `NWP-RESERVED-TYPE-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | `QueryFrame` 或 `SubscribeFrame` 的 `type` 字段包含不可识别的 reserved-type 标识符；当前节点未实现所请求的保留操作（NWP §12）。与 `NWP-ACTION-NOT-FOUND` 不同——当 `type` 字段是未知操作数（而非 `action_id`）时使用此码。|
| `NWP-HTTP-ORIGIN-FORBIDDEN` | `NPS-AUTH-FORBIDDEN` | HTTP overlay origin policy 在接收 NWP frame 之前拒绝调用方（NWP §9.5）|
| `NWP-HTTP-CONTENT-TYPE-UNSUPPORTED` | `NPS-CLIENT-BAD-FRAME` | HTTP overlay 请求 `Content-Type` 不是受支持的 NWP frame media type（NWP §9.5）|
| `NWP-HTTP-ACCEPT-UNSATISFIABLE` | `NPS-CLIENT-BAD-PARAM` | HTTP overlay 请求 `Accept` 无法由任何受支持的 response media type 满足（NWP §9.5）|
| `NWP-HTTP-REQUEST-ID-MISMATCH` | `NPS-CLIENT-BAD-PARAM` | 响应 `X-NWP-Request-ID` 未回传请求 ID（NWP §9.5）|
| `NWP-HTTP-FRAME-BODY-MALFORMED` | `NPS-CLIENT-BAD-FRAME` | HTTP body 无法解析为受支持的 NWP frame envelope（NWP §9.5）|
| `NWP-HTTP-BODY-TOO-LARGE` | `NPS-LIMIT-PAYLOAD` | HTTP 请求 body 超出可移植 Node 服务端配置的上限（NWP §9.5 / §16.5.1）|
| `NWP-CAPABILITY-ADVERTISED-UNIMPLEMENTED` | `NPS-SERVER-UNSUPPORTED` | NWM 声明了当前节点无法服务的 capability/profile；不同于 `NWP-QUERY-VECTOR-UNSUPPORTED` 等如实声明不支持的专用错误码（NWP §9.5）|
| `NWP-TOPOLOGY-UNAUTHORIZED` | `NPS-AUTH-FORBIDDEN` | 调用方无权读取该 Anchor 的拓扑信息（NPS-2 §12）；授权策略由实现方按 §12.4 自定义（NPS-CR-0002）|
| `NWP-ANCHOR-NOT-LEADER` | `NPS-CLIENT-CONFLICT` | 拓扑写入被发送到 standby 或只读降级的 Anchor；只有当前活跃的集群 owner 接受写入（NPS-CR-0009）|
| `NWP-ANCHOR-EPOCH-FENCED` | `NPS-CLIENT-CONFLICT` | 入站帧携带的 `cluster_epoch` 高于接收 Anchor 自身的值；接收方是已被取代的 leader，被栅栏隔离（NPS-CR-0009）|
| `NWP-TOPOLOGY-UNSUPPORTED-SCOPE` | `NPS-CLIENT-BAD-PARAM` | `topology.scope` 的值该 Anchor Node 未实现（NPS-CR-0002）|
| `NWP-TOPOLOGY-DEPTH-UNSUPPORTED` | `NPS-CLIENT-BAD-PARAM` | 请求的 `topology.depth` 超出该 Anchor Node 配置的最大值（NPS-CR-0002）|
| `NWP-TOPOLOGY-FILTER-UNSUPPORTED` | `NPS-CLIENT-BAD-PARAM` | `topology.filter` 包含未识别的键或不支持的运算符（NPS-CR-0002）|
| `NWP-BRIDGE-DIRECTION-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | 请求指向了本 Bridge Node 未声明的「协议 × 方向」组合 —— 入向请求命中了不在 `bridge_inbound_protocols` 中的协议，或出向 `bridge_target.protocol` 不在 `bridge_protocols` 中（NPS-4 §3.1）。响应 SHOULD 在 `hint` 中携带两个已声明的数组。与 `NWP-ACTION-PARAMS-INVALID` 的区别：该协议/方向本身格式合法，只是本节点不提供服务，而非请求畸形。（NPS-CR-0010）|
| `NWP-BRIDGE-TARGET-INVALID` | `NPS-CLIENT-UNPROCESSABLE` | **出向。** 调用未携带合法的 `bridge_target` 对象，或其 schema 校验失败（缺 `protocol` / `endpoint`）。*由 NPS-CR-0010 正式登记；.NET Bridge 自 alpha.7 起一直在发这个码，却从未登记在本表中。* |
| `NWP-BRIDGE-PROTOCOL-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | **出向。** `bridge_target.protocol` 格式合法，但本 Bridge Node 没有为它注册 dispatcher。与 `NWP-BRIDGE-DIRECTION-UNSUPPORTED` 的区别：后者针对**已声明**的协议/方向集合，本码针对 dispatcher 注册表。*由 NPS-CR-0010 登记。* |
| `NWP-BRIDGE-ENDPOINT-INVALID` | `NPS-CLIENT-UNPROCESSABLE` | **出向。** `bridge_target.endpoint` 不是合法 URL，或被 Bridge 的 SSRF 策略拒绝（NWP §15.2）。*由 NPS-CR-0010 登记。* |
| `NWP-BRIDGE-UPSTREAM-FAILED` | `NPS-DOWNSTREAM-UNAVAILABLE` | **出向。** 对外调用在传输层失败、超时，或返回了 Bridge 无法翻译回 NWP 帧的响应。注意区别：若上游返回的是一个能被**成功翻译**的错误状态，则按 NWP §16.3 映射，不用本码。*由 NPS-CR-0010 登记。* |
| `NWP-BRIDGE-SERVER-TOOL-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | **入向。** 外部客户端点名了本 Bridge 并未暴露的 tool / action / resource。*由 NPS-CR-0010 登记。* |
| `NWP-BRIDGE-SERVER-DISPATCHER-MISSING` | `NPS-SERVER-INTERNAL` | **入向。** Bridge 启动时未配置其所代理的 NPS 节点后端 —— 这是部署故障，不是客户端错误。*由 NPS-CR-0010 登记；.NET Bridge 此前在这里发的是根本不存在的状态码 `NPS-SERVER-NOT-IMPLEMENTED`。* |
| `NWP-BRIDGE-SERVER-DISPATCH-FAILED` | `NPS-SERVER-INTERNAL` | **入向。** 向所代理的 NPS 节点分发时发生非预期失败。*由 NPS-CR-0010 登记。* |

---

## NIP 错误码

| 错误码 | NPS 状态码 | 描述 |
|--------|-----------|------|
| `NIP-CONTEXT-INVALID` | `NPS-CLIENT-BAD-FRAME` | 投影格式、边界或生命周期无效 |
| `NIP-CONTEXT-UNTRUSTED` | `NPS-AUTH-FORBIDDEN` | 不可信 issuer/key 或无效签名 |
| `NIP-CONTEXT-BINDING-MISMATCH` | `NPS-AUTH-FORBIDDEN` | 投影请求、主体、audience 或策略不匹配 |
| `NIP-CONTEXT-EXPIRED` | `NPS-AUTH-FORBIDDEN` | 投影有效窗口已过期 |
| `NIP-CONTEXT-REPLAY` | `NPS-CLIENT-CONFLICT` | 有效投影已被准入 |
| `NIP-CONTEXT-VERIFIER-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | 验证或持久化重放基础设施不可用 |
| `NIP-CERT-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | 证书已过期（expires_at < now）|
| `NIP-CERT-NODE-ROLES-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | `IdentFrame.node_roles` 与 `id-nps-node-roles` X.509 扩展不一致；Phase 3 强制（NIP v0.10） |
| `NIP-CERT-CAPABILITIES-EXCEEDED` | `NPS-AUTH-FORBIDDEN` | `IdentFrame.capabilities` 声称了 CA 见证扩展 `id-nps-capabilities` 中不存在的能力；Phase-3 强制（NIP v0.12）|
| `NIP-OCSP-STAPLE-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | `IdentFrame.ocsp_staple` 的 `nextUpdate` 已过期；Agent 必须刷新后重发（NIP v0.9 §5.1.4） |
| `NIP-CERT-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | 证书已被吊销（在 CRL 或 OCSP 中）|
| `NIP-CERT-SIGNATURE-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | 证书签名验证失败 |
| `NIP-CERT-UNTRUSTED-ISSUER` | `NPS-AUTH-UNAUTHENTICATED` | 颁发者不在 trusted_issuers 列表中 |
| `NIP-CERT-CAPABILITY-MISSING` | `NPS-AUTH-FORBIDDEN` | 证书缺少节点要求的能力 |
| `NIP-CERT-SCOPE-VIOLATION` | `NPS-AUTH-FORBIDDEN` | 证书 scope 不覆盖目标路径或操作 |
| `NIP-CA-NID-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | NID 不存在于 CA 数据库 |
| `NIP-CA-NID-ALREADY-EXISTS` | `NPS-CLIENT-CONFLICT` | NID 已存在（重复注册）|
| `NIP-CA-SERIAL-DUPLICATE` | `NPS-CLIENT-CONFLICT` | 证书序列号已存在 |
| `NIP-CA-RENEWAL-TOO-EARLY` | `NPS-CLIENT-BAD-PARAM` | 距到期超过 7 天，尚未到续期窗口 |
| `NIP-CA-SCOPE-EXPANSION-DENIED` | `NPS-AUTH-FORBIDDEN` | 请求的 scope 超出父级 scope（委托链违规）|
| `NIP-CA-GROUP-REVOKED` | `NPS-AUTH-FORBIDDEN` | 不能在已吊销的 group NID 下签发 session（NPS-3 §5.1.3，NPS-CR-0003） |
| `NIP-CA-PARENT-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | 签发 session 时指定的 parent/group NID 不存在（NPS-CR-0003） |
| `NIP-CA-PARENT-NOT-GROUP` | `NPS-CLIENT-BAD-PARAM` | 签发 session 时指定的 parent NID 不是 orchestrator group（NPS-CR-0003） |
| `NIP-CA-SESSION-VALIDITY-INVALID` | `NPS-CLIENT-BAD-PARAM` | 请求的 session 有效期超出允许范围或超过 parent group 的剩余有效期（NPS-CR-0003） |
| `NIP-CA-JWS-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | group-JWS session 自签发请求的签名、protected header 或 payload 无效（NPS-CR-0003） |
| `NIP-CA-JWS-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | group-JWS session 自签发请求已超过允许的时间窗口（NPS-CR-0003） |
| `NIP-OCSP-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | OCSP 服务暂时不可用 |
| `NIP-OCSP-UNKNOWN` | `NPS-AUTH-UNAUTHENTICATED` | 合法 OCSP response 无法确认请求的 issuer/serial 状态，因此 fail closed（NIP v0.15 §7.7）|
| `NIP-REVOCATION-STATE-STALE` | `NPS-SERVER-UNAVAILABLE` | 没有已配置吊销源产出 current result；stale state 不能满足 required mode（NIP v0.15 §7.7）|
| `NIP-TRUST-FRAME-INVALID` | `NPS-CLIENT-BAD-FRAME` | TrustFrame 签名或格式不合法 —— 见 NPS-3 §5.2 |
| `NIP-TRUST-FRAME-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | TrustFrame `expires_at` 已过期 —— 见 NPS-3 §5.2 |
| `NIP-TRUST-FRAME-GRANTOR-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | TrustFrame `grantor_nid` 自身的 CA 证书已被吊销或过期 —— 见 NPS-3 §5.2 |
| `NIP-TRUST-FRAME-SCOPE-EXCEEDS-GRANTOR` | `NPS-AUTH-FORBIDDEN` | TrustFrame `trust_scope` 包含 grantor 自身不持有的能力（不可扩大 scope 原则）—— 见 NPS-3 §5.2 |
| `NIP-TRUST-FRAME-NODES-PATTERN-INVALID` | `NPS-CLIENT-BAD-FRAME` | TrustFrame `nodes` 数组某项不是合法的 `nwp://` URL 模式（如通配符位置错误）—— 见 NPS-3 §5.2 |
| `NIP-REVOKE-FRAME-INVALID` | `NPS-CLIENT-BAD-FRAME` | RevokeFrame 不合法（缺必填字段、签名校验失败或规范化形式错误）—— 见 NPS-3 §5.3 |
| `NIP-REVOKE-FRAME-UNAUTHORIZED-ISSUER` | `NPS-AUTH-FORBIDDEN` | RevokeFrame `signer_nid` 无权吊销 `target_nid` —— 见 NPS-3 §5.3 |
| `NIP-REVOKE-FRAME-SERIAL-MISMATCH` | `NPS-CLIENT-BAD-PARAM` | RevokeFrame `serial` 存在但与 `target_nid` 当前已颁发证书的 serial 都不匹配 —— 见 NPS-3 §5.3 |
| `NIP-REVOKE-FRAME-REASON-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | RevokeFrame `reason` 取值不在定义的枚举中；接收方按 `key_compromise` 处理（最严格）—— 见 NPS-3 §5.3 |
| `NIP-ASSURANCE-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | `IdentFrame.assurance_level` 与证书扩展 `id-nid-assurance-level` 不一致（防 downgrade 攻击）—— 见 NPS-3 §5.1.1（NPS-RFC-0003）|
| `NIP-ASSURANCE-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | `assurance_level` 取值不在定义枚举（`anonymous` / `attested` / `verified`）—— 见 NPS-3 §5.1.1（NPS-RFC-0003）|
| `NIP-REPUTATION-ENTRY-INVALID` | `NPS-CLIENT-BAD-FRAME` | 声誉日志条目签名校验失败或规范化（RFC 8785 JCS）形式不合法 —— 见 NPS-3 §5.1.2（NPS-RFC-0004）|
| `NIP-REPUTATION-LOG-UNREACHABLE` | `NPS-DOWNSTREAM-UNAVAILABLE` | 准入评估时无法到达 Node `reputation_policy` 引用的某个日志运营方 —— 见 NPS-3 §5.1.2（NPS-RFC-0004）|
| `NIP-REPUTATION-GOSSIP-FORK` | `NPS-SERVER-INTERNAL` | 跨节点 STH 一致性检查失败；可能检测到 Merkle 树分叉 —— 见 NPS-RFC-0004 §4.5 |
| `NIP-REPUTATION-GOSSIP-SIG-INVALID` | `NPS-CLIENT-BAD-FRAME` | gossip 交换中对端 STH 签名验证失败 —— 见 NPS-RFC-0004 §4.5 |
| `NIP-CERT-FORMAT-INVALID` | `NPS-CLIENT-BAD-FRAME` | `IdentFrame.cert_chain` 不是 DER 编码 X.509，或 ASN.1 解析失败 —— 见 NPS-RFC-0002 §4.3 |
| `NIP-CERT-EKU-MISSING` | `NPS-CLIENT-BAD-FRAME` | leaf 证书缺少必需的 NPS EKU（`agent-identity` 或 `node-identity`）或未标 critical —— 见 NPS-RFC-0002 §4.1 / §4.3 |
| `NIP-CERT-SUBJECT-NID-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | X.509 leaf 证书 subject CN / SAN URI 与 `IdentFrame.nid` 字段不一致 —— 见 NPS-RFC-0002 §4.3 |
| `NIP-ACME-CHALLENGE-FAILED` | `NPS-CLIENT-BAD-FRAME` | ACME `agent-01` challenge 校验失败（token 不匹配、签名验证失败或 replay）—— 见 NPS-RFC-0002 §4.4 |
| `NIP-CERT-PARENT-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | session NID 的 parent/group NID 已吊销或过期（链校验，NPS-3 §7 step 3a；NPS-CR-0003） |

---

## NDP 错误码

| 错误码 | NPS 状态码 | 描述 |
|--------|-----------|------|
| `NDP-RESOLVE-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | nwp:// 地址无法解析到物理端点 |
| `NDP-RESOLVE-AMBIGUOUS` | `NPS-CLIENT-CONFLICT` | 解析结果存在冲突（多个不一致的注册）|
| `NDP-RESOLVE-TIMEOUT` | `NPS-SERVER-TIMEOUT` | 解析请求超时 |
| `NDP-RESOLVE-STALE` | `NPS-CLIENT-NOT-FOUND` | 被解析条目的新鲜度截止 `(last_seen ?? timestamp) + ttl` 已过期；Registry MUST NOT 返回该失效注册（NDP v0.9 §3.2.1）|
| `NDP-CLUSTER-SPLIT` | `NPS-CLIENT-CONFLICT` | 同一 `cluster_anchor` 集群存在两个宣告相同 `cluster_epoch` 的活跃 Anchor（脑裂）；Registry 拒绝任意裁决（NPS-CR-0009）|
| `NDP-ANNOUNCE-STALE` | `NPS-CLIENT-NOT-FOUND` | 超过 `heartbeat_interval_ms × 3` 未收到重新公告，AnnounceFrame 心跳已过期（NDP v0.9）|
| `NDP-ANNOUNCE-SIGNATURE-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | AnnounceFrame 签名验证失败 |
| `NDP-ANNOUNCE-NID-MISMATCH` | `NPS-CLIENT-BAD-FRAME` | AnnounceFrame 中 NID 与签名证书不一致 |
| `NDP-ANNOUNCE-ROLE-REMOVED` | `NPS-CLIENT-BAD-FRAME` | AnnounceFrame `node_roles` 包含已移除的遗留值 `"gateway"`（NPS-CR-0001）；请改用 `"anchor"` 或 `"bridge"`。响应 SHOULD 携带指向 NPS-CR-0001 的 `hint` 字段。|
| `NDP-ANNOUNCE-ROLE-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | AnnounceFrame `node_roles` 包含无法识别的值（非已知遗留移除值——遗留移除情况请用 `NDP-ANNOUNCE-ROLE-REMOVED`）。|
| `NDP-ANNOUNCE-CONFLICT` | `NPS-CLIENT-CONFLICT` | 两个 AnnounceFrame 共享相同 `nid` 与 `graph_seq` 但所覆盖内容不同（注册表投毒企图；见 NPS-4 §7.4）|
| `NDP-ANNOUNCE-PROFILE-VIOLATION` | `NPS-AUTH-FORBIDDEN` | AnnounceFrame 违反当前 Registry 安全 profile 约束，且没有更具体的 NDP 错误码可用（例如 org-private / public-federated profile 下的地址策略）|
| `NDP-GRAPH-SEQ-ROLLBACK` | `NPS-CLIENT-BAD-FRAME` | AnnounceFrame 的 `graph_seq` 小于该 NID 此前已接受的最高值（回滚企图；见 NPS-4 §7.5）|
| `NDP-GRAPH-SEQ-GAP` | `NPS-STREAM-SEQ-GAP` | GraphFrame 序号不连续 |
| `NDP-GRAPH-INVALID` | `NPS-CLIENT-BAD-FRAME` | GraphFrame 边引用了不在 `nodes` 列表中的 NID，或存在自环边 |
| `NDP-GRAPH-TOO-LARGE` | `NPS-LIMIT-PAYLOAD` | GraphFrame 超过拓扑快照规模限制（`nodes` > 256 或 `edges` > 1024）|
| `NDP-FEDERATION-LOOP` | `NPS-CLIENT-CONFLICT` | 联邦转发通过 `ndp-forwarded-by` hop 列表检测到环路 |
| `NDP-ISSUER-NOT-ALLOWED` | `NPS-AUTH-FORBIDDEN` | AnnounceFrame 的签发者（签名 CA）不在当前注册表 profile 的签发者白名单中（见 NPS-4 §7.3）|
| `NDP-CA-ATTEST-REQUIRED` | `NPS-AUTH-UNAUTHENTICATED` | 当前注册表 profile 要求 CA 背书的 NID，但 AnnounceFrame 证书链未锚定到配置的信任根（见 NPS-4 §7.3）|
| `NDP-REGISTRY-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | NDP Registry 暂时不可用 |
| `NDP-STATE-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | 要求持久化的 Registry 无法持久读取或提交恢复状态（NDP v0.13 §7.8）|
| `NDP-STATE-CORRUPT` | `NPS-SERVER-INTERNAL` | 持久化 Registry state 未通过 schema、checksum 或单调 fence 校验（NDP v0.13 §7.8）|

---

## NOP 错误码

| 错误码 | NPS 状态码 | 描述 |
|--------|-----------|------|
| `NOP-EXECUTION-INDETERMINATE` | `NPS-SERVER-INTERNAL` | 执行结果不确定，不可自动重试 |
| `NOP-TASK-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | task_id 不存在 |
| `NOP-TASK-TIMEOUT` | `NPS-SERVER-TIMEOUT` | 整体任务执行超时 |
| `NOP-TASK-DAG-INVALID` | `NPS-CLIENT-BAD-FRAME` | DAG 格式不合法（缺少起点/终点节点、字段错误等）|
| `NOP-TASK-DAG-CYCLE` | `NPS-CLIENT-BAD-FRAME` | DAG 中存在环路 |
| `NOP-TASK-DAG-TOO-LARGE` | `NPS-CLIENT-BAD-FRAME` | DAG 节点数超过上限（默认 32）|
| `NOP-TASK-ALREADY-COMPLETED` | `NPS-CLIENT-CONFLICT` | 任务已完成，无法重复提交 |
| `NOP-TASK-CANCELLED` | `NPS-CLIENT-CONFLICT` | 任务已被取消 |
| `NOP-DELEGATE-SCOPE-VIOLATION` | `NPS-AUTH-FORBIDDEN` | delegated_scope 超出父 Agent scope |
| `NOP-DELEGATE-REJECTED` | `NPS-CLIENT-UNPROCESSABLE` | 目标 Agent 拒绝接受委托（能力不足或超负载），响应含 retry_after_ms |
| `NOP-DELEGATE-CHAIN-TOO-DEEP` | `NPS-CLIENT-BAD-PARAM` | 委托链深度超过上限（默认 3 层）|
| `NOP-DELEGATE-TIMEOUT` | `NPS-SERVER-TIMEOUT` | 子任务在 deadline_at 前未完成 |
| `NOP-SYNC-TIMEOUT` | `NPS-SERVER-TIMEOUT` | SyncFrame 等待依赖任务完成超时 |
| `NOP-SYNC-DEPENDENCY-FAILED` | `NPS-CLIENT-UNPROCESSABLE` | 等待的依赖子任务已失败（且失败数超出 K-of-N 容忍范围）|
| `NOP-STREAM-SEQ-GAP` | `NPS-STREAM-SEQ-GAP` | AlignStream 序号不连续 |
| `NOP-STREAM-NID-MISMATCH` | `NPS-AUTH-UNAUTHENTICATED` | AlignStream sender_nid 与连接身份不一致 |
| `NOP-RESOURCE-INSUFFICIENT` | `NPS-SERVER-UNAVAILABLE` | 预检（preflight）发现一个或多个 Worker Agent 资源不足（CGN 不够或缺少能力）|
| `NOP-CONDITION-EVAL-ERROR` | `NPS-CLIENT-BAD-PARAM` | DAG 节点 condition 表达式求值失败（语法错误或引用了不存在的字段）|
| `NOP-INPUT-MAPPING-ERROR` | `NPS-CLIENT-UNPROCESSABLE` | input_mapping JSONPath 无法解析或目标字段不存在 |
| `NOP-COMPENSATION-FAILED` | `NPS-CLIENT-UNPROCESSABLE` | 终态——saga 回滚过程中节点的 `compensate_action` 返回错误 |
| `NOP-COMPENSATION-NOT-SUPPORTED` | `NPS-CLIENT-UNPROCESSABLE` | 终态——存在需要补偿的前驱缺少 `compensate_action` 且 `compensation_policy="strict"` |
| `NOP-CLAIM-CONFLICT` | `NPS-CLIENT-CONFLICT` | TaskFrame 已被存活的 `nps-runner` 租约占用（NPS-CR-0007 §4.2）|
| `NOP-SPAWN-SPEC-INVALID` | `NPS-CLIENT-BAD-PARAM` | `spawn_spec_ref` 无法解析或未通过 SpawnSpec 模式校验（NPS-CR-0007 §5）|
| `NOP-RUNTIME-IDLE-TIMEOUT` | `NPS-SERVER-TIMEOUT` | L3 Worker 空闲时间超过上限（NPS-CR-0007 §6）|
| `NOP-RUNTIME-MAX-RUNTIME` | `NPS-SERVER-TIMEOUT` | L3 Worker 总运行时间超过上限（NPS-CR-0007 §6）|
| `NOP-CALLBACK-HMAC-MISSING` | `NPS-AUTH-UNAUTHENTICATED` | 设置了 `callback_secret` 但缺少 `X-NPS-Signature` header（NOP v0.6）|
| `NOP-CALLBACK-INVALID` | `NPS-CLIENT-BAD-PARAM` | callback URL 未通过 scheme、user-info、DNS、公网地址或重定向校验（NOP v0.9）|
| `NOP-CALLBACK-HMAC-INVALID` | `NPS-AUTH-UNAUTHENTICATED` | callback HMAC 格式错误，或与精确原始 body 不匹配（NOP v0.9）|
| `NOP-TASK-RESULT-EXPIRED` | `NPS-CLIENT-NOT-FOUND` | 任务结果超过 `result_ttl_seconds` 后不再保留（NOP v0.7）|
| `NOP-REPLAY-CONFLICT` | `NPS-CLIENT-CONFLICT` | 同一 `(caller_nid, task_id)` 被不同的 TaskFrame 不可变内容复用（NOP v0.10 §3.5.6）|
| `NOP-REPLAY-LIMIT` | `NPS-LIMIT-RESOURCE` | replay ledger 已满，且没有可安全移除的过期 tombstone 或 terminal record（NOP v0.10 §3.5.6）|
| `NOP-AGGREGATION-INVALID` | `NPS-CLIENT-BAD-PARAM` | 聚合输入违反所选 strategy，例如 `weighted_first_k` 缺少有限数值 score（NOP v0.10 §3.5.6）|
| `NOP-STREAM-NAK-UNRESOLVABLE` | `NPS-STREAM-SEQ-GAP` | NAK 请求的帧已从发送方缓冲区淘汰（NOP v0.7）|

---

*归属：LabAcacia / INNO LOTUS PTY LTD · Apache 2.0*
