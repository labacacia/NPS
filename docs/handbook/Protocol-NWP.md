# Protocol: NWP — Neural Web Protocol

**Status:** ✅ Released alpha.19 reference

**Released spec**: `spec/NPS-2-NWP.md` v0.22 · **Port**: 17433 (shared) / 17434 (optional dedicated)

> **Alpha.19 release: NWP 0.22 Proposed.** It closes
> NWM normalization, renewable subscription lease/SLA/billing metadata and
> related failure paths across six SDKs. See [Alpha.19 Current Status](./Alpha19-Current-Status.md).

NWP is the HTTP-equivalent for Agent-to-Node interaction in NPS. Where HTTP defines how browsers and servers exchange web pages, NWP defines how AI Agents query data, invoke actions, and subscribe to changes on Neural Nodes — with responses that are directly machine-understandable, requiring no semantic parsing layer. NWP runs on top of [Protocol NCP](./Protocol-NCP.md) the same way HTTP semantics run on top of TCP.

Related: [Protocol NCP](./Protocol-NCP.md) | [Protocol NIP](./Protocol-NIP.md) | [Protocol NDP](./Protocol-NDP.md) | [Reference: Cognon Budget](./Reference-Cognon-Budget.md)

---

## Node Types

| Type | Role | Typical data sources |
|------|------|---------------------|
| **Memory Node** | Data storage and retrieval, no compute logic | RDS, NoSQL, file systems, vector databases |
| **Action Node** | Executes operations, returns results or side effects | Functions, external APIs, message queues |
| **Complex Node** | Mixed data and operations, with sub-node references | All of the above plus sub-node graph |
| **Anchor Node** | Cluster control plane and external entry point — routes inbound frames to member nodes via NOP; optionally maintains member topology | AaaS platforms, multi-agent service gateways |
| **Bridge Node** | Translates in either declared direction between NPS frames and non-NPS protocols (HTTP/HTTPS, gRPC, MCP, A2A) | Legacy REST APIs, gRPC services, Model Context Protocol servers |

**Anchor Node** and **Bridge Node** were introduced by NPS-CR-0001, replacing the retired `Gateway Node` type. Anchor Node inherits the cluster-entry and NOP-routing role; Bridge Node is a new type responsible for NPS-to-external-protocol translation. The legacy wire value `"gateway"` is rejected with `NWP-MANIFEST-NODE-TYPE-REMOVED`.

A node MAY carry multiple roles simultaneously (e.g., `["anchor", "memory"]`). The full role set is declared in the NDP `AnnounceFrame.node_roles` field (discovery layer). The NWM `node_type` field (single string) declares which role this particular `/.nwm` endpoint is serving; it MUST be one of the values in `node_roles`.

### Bridge Node — outbound and inbound profiles

For outbound translation, a Bridge Node accepts NWP frames carrying a `bridge_target` object that identifies the external protocol and endpoint. The standard fields (spec §2.1) are:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `protocol` | string | Required | One of `"http"` / `"grpc"` / `"mcp"` / `"a2a"` |
| `endpoint` | string (URL) | Required | Upstream endpoint to dial |
| `headers` | object (string→string) | Optional | Extra HTTP headers passed to the upstream |

Unknown `bridge_target` fields MUST be ignored (opaque pass-through, forward compatibility). A Bridge Node validates `protocol` against its advertised set (NDP `bridge_protocols`); a missing `bridge_target` or unsupported protocol is rejected with `NWP-ACTION-PARAMS-INVALID`. Bridge Nodes are stateless per request and MUST NOT participate in cluster topology (`topology.*` → `NWP-RESERVED-TYPE-UNSUPPORTED`).

NWP v0.19 (CR-0010) also standardizes inbound adapters. A node advertises inbound protocols separately through NDP `bridge_inbound_protocols`; inbound MCP/A2A requests are authenticated, normalized into NWP ActionFrames, and dispatched through the same action surface. A protocol is never assumed bidirectional merely because it appears in `bridge_protocols`.

---

## Neural Web Manifest (NWM)

Every node MUST expose a machine-readable manifest at `/.nwm` with `Content-Type: application/nwp-manifest+json`. The NWM is the single source of truth for what a node can do, what it requires from callers, and where its endpoints live.

### Key NWM Fields

| Field | Type | Description |
|-------|------|-------------|
| `nwp` | string | NWP version, currently `"0.4"` |
| `node_id` | string | Node NID, format `urn:nps:node:{host}:{path}` |
| `node_type` | string | Operative role at this endpoint: `"memory"` / `"action"` / `"complex"` / `"anchor"` / `"bridge"` |
| `manifest_version` | uint32 | Monotonically incrementing manifest version counter (starts at 1, +1 on every structural change). Servers MUST return `X-NWM-Version: {manifest_version}` on every `GET /.nwm`. (NWP v0.14 — changed from opaque ETag string to uint32 counter) |
| `manifest_updated_at` | string | ISO 8601 timestamp of the last manifest change, e.g. `"2026-06-03T12:00:00Z"`. SHOULD be set whenever `manifest_version` is incremented. (NWP v0.14) |
| `trust_anchors` | array | NIDs of CA nodes the Anchor accepts as IdentFrame issuers (e.g. `["urn:nps:agent:ca.example.com:root"]`). Consumers SHOULD use this to pre-validate their issuer before connecting; absent = accept any CA trusted by the NIP chain. (NWP v0.13) |
| `wire_formats` | array | Supported encodings: `["ncp-capsule", "msgpack", "json"]` |
| `schema_anchors` | object | Pre-declared schemas as `{name: anchor_id}` — Agents SHOULD preload all on first connection |
| `capabilities` | object | Boolean flags: `query`, `stream_query`, `aggregate`, `subscribe`, `vector_search`, `token_budget_hint`, `ext_frame`, `e2e_enc`, `inline_anchor` |
| `auth` | object | Authentication requirements: `required`, `identity_type` (`"nip-cert"` / `"bearer"` / `"none"`), `trusted_issuers`, `required_capabilities` |
| `min_assurance_level` | string | Node-level minimum: `"anonymous"` (default) / `"attested"` / `"verified"`. Requests below this level are rejected with `NWP-AUTH-ASSURANCE-TOO-LOW`. (NPS-RFC-0003) |
| `reputation_policy` | object | Phase 2 field (NWP v0.8+): defines `reject_on` rules against reputation log entries. Produces `NWP-AUTH-REPUTATION-BLOCKED`. (NPS-RFC-0004) |
| `actions` | object | `{action_id: ActionSpec}` registry — required for Action and Complex nodes |
| `endpoints` | object | URLs for each sub-path (`query`, `stream`, `invoke`, `subscribe`, `actions`, `schema`) |
| `tokenizer_support` | array | Tokenizers the node can use for CGN estimation |

The NWM may be conditionally requested using `If-None-Match: {manifest_version}` (HTTP mode, integer string e.g. `If-None-Match: 7`). If unchanged, the server returns `304 Not Modified`. Servers MUST emit `X-NWM-Version: {manifest_version}` on every `GET /.nwm` response so agents can detect staleness without a full re-fetch; `manifest_updated_at` gives a human-readable timestamp of the last structural change. (NWP v0.14)

### Actions: ActionSpec

Each entry in the `actions` map is an `ActionSpec` describing a callable operation:

| Field | Description |
|-------|-------------|
| `description` | Human-readable description |
| `params_anchor` | Schema `anchor_id` for input parameters |
| `result_anchor` | Schema `anchor_id` for the result |
| `async` | Whether async execution is supported |
| `idempotent` | Whether safe to retry |
| `timeout_ms_default` / `timeout_ms_max` | Timeout bounds in milliseconds |
| `required_capability` | NIP capability required (e.g. `"nwp:invoke"`) |
| `min_assurance_level` | **Per-action override**: `"anonymous"` / `"attested"` / `"verified"`. Takes precedence over the top-level NWM value for requests targeting this action. (NPS-RFC-0003) |

---

## LLM / Thinking Profile (`profiles.llm`)

NWP v0.16–v0.21 make model serving a first-class NWM concept. A model-serving
Action or Complex Node advertises a standard **`profiles.llm`** block in its NWM
(§4.2a): model descriptors, context-window and streaming/tool support, privacy
hints, and the reasoning-disclosure policy. "Thinking Node" is a product-facing
alias, **not** a new `node_type` — declare `action` (model actions only) or
`complex` (also owns memory / tools / routing).

- Coarse discovery and authorization use the NIP `llm:*` capability strings
  (`llm:complete`, `llm:stream`, `llm:tool_call`, `llm:embed`, `llm:rerank`;
  NIP v0.11) carried in `IdentFrame.capabilities` / NDP announce.
- The **`llm.complete`** ActionFrame contract (§7.5) pins the typed
  request/response DTO shape, `stop_reason` enum, tool-call field names,
  sync / async / streaming response semantics, and the ErrorFrame-vs-payload
  error boundary, with snake_case keys across JSON and MessagePack.

### Stateful context and delta completion (NWP v0.21, CR-0011)

Profile version `0.2` adds an optional `context` descriptor and six lifecycle operations: `create`, `append`, `fork`, `reset`, `status`, and `release`. Context identifiers are opaque, owner-bound locators, never bearer credentials. Mutations use compare-and-swap `base_version`, require an idempotency key, and commit only on terminal success; cancellation, timeout, revocation, or stream failure aborts the reservation.

The existing `llm.complete` ActionFrame remains the request carrier. Unary results use the typed action response; `stream=true` returns StreamFrames and only the terminal chunk may carry the committed context receipt. Stateful requests never silently fall back to stateless prompt replay. Usage separates logical, reused, evaluated, and wire input so model-input savings can be measured independently from MessagePack/NCP byte savings.

> **Candidate implementation boundary:** the alpha.18 SDK source implements stateful unary and asynchronous completion with process-local stores. Stateful streaming, durable stores, the strict-native Ivy migration, and the evaluated-token benchmark remain CR-0011 acceptance work. Current reference servers reject stateful `stream=true` without fallback.

## HTTP Binding Rejection Codes (§9.5, alpha.16)

NWP v0.17 registers canonical error codes for HTTP-overlay transport
rejections that previously surfaced as ad-hoc HTTP errors:
`NWP-HTTP-ORIGIN-FORBIDDEN`, `NWP-HTTP-CONTENT-TYPE-UNSUPPORTED`,
`NWP-HTTP-ACCEPT-UNSATISFIABLE`, `NWP-HTTP-REQUEST-ID-MISMATCH`,
`NWP-HTTP-FRAME-BODY-MALFORMED`, plus `NWP-CAPABILITY-ADVERTISED-UNIMPLEMENTED`
for advertised-but-unimplemented capability rollout windows. See
[Reference: Error Codes](./Reference-Error-Codes.md).

## QueryFrame (0x10)

Used for structured data queries on Memory Nodes. Returns a `CapsFrame` (single response) or a `StreamFrame` sequence (streaming mode).

### Key Fields

| Field | Description |
|-------|-------------|
| `anchor_ref` | Schema `anchor_id` to use for this query |
| `type` | Reserved query type identifier (see Reserved Query Types section); absent = normal per-anchor query |
| `auto_anchor` | If true and anchor is stale, node attaches updated schema inline in response. Default: true |
| `stream` | If true, triggers streaming mode; response is a `StreamFrame` sequence |
| `filter` | Filter conditions using `$eq`, `$ne`, `$lt`, `$lte`, `$gt`, `$gte`, `$in`, `$nin`, `$contains`, `$between`, `$exists`, `$regex`, `$and`, `$or`, `$not` |
| `fields` | Field projection list; omit to return all fields |
| `limit` | Max records (default 20, max 1000) |
| `cursor` | Pagination cursor from previous response `next_cursor` |
| `order` | Sort rules: `[{field, dir: "ASC"/"DESC"}]` |
| `vector_search` | Vector similarity search: `{field, vector, top_k, threshold, metric}` |
| `token_budget` | CGN budget limit (native mode equivalent of `X-NWP-Budget`) |
| `depth` | Node graph traversal depth (default 1, max 5) |
| `aggregate` | Aggregation operations: `{operations: [{func, field, alias}], group_by, having}` |
| `request_id` | UUID v4 for tracing; echoed in response |
| `cgn_est` | (formerly `estimated_npt` until alpha.5.2) Estimated Cognon cost per event in streaming scenarios |

### Sync vs Async Behavior

QueryFrame is synchronous by default: the node responds immediately with a `CapsFrame`. When `stream: true` is set, the node pushes a sequence of `StreamFrame` chunks, with the first chunk carrying `estimated_total` and `request_id` metadata. To cancel an in-flight streaming query, send an `ErrorFrame` referencing the QueryFrame's `request_id`, or disconnect; nodes route the cancellation by `request_id` and MUST NOT require a SubscribeFrame-shaped cancel message for streaming queries.

---

## ActionFrame (0x11)

Used for operation invocation on Action Nodes and Complex Nodes.

### Key Fields

| Field | Description |
|-------|-------------|
| `action_id` | Operation identifier, format `{domain}.{verb}` |
| `params` | Input parameters; validated against `ActionSpec.params_anchor` |
| `idempotency_key` | UUID v4; valid for 24 hours; safe to repeat on retry |
| `timeout_ms` | Timeout in milliseconds (default 5000, max 300000) |
| `async` | If true, execute asynchronously; response returns `task_id` + `poll_url` |
| `callback_url` | `https://` URL for async task completion notification |
| `priority` | `"low"` / `"normal"` / `"high"` |
| `cgn_est` | Estimated Cognon cost (carried through async task tracking, replaces `estimated_npt` from alpha.4) |

### Async Task Flow

When `async: true`, the node immediately returns a `CapsFrame` carrying `{task_id, status: "pending", poll_url, estimated_ms}`. The task progresses through `PENDING → RUNNING → COMPLETED / FAILED / CANCELLED`. Poll via `system.task.status` with `{task_id}`, or receive completion notification via `callback_url`.

All nodes supporting async actions MUST implement `system.task.status` and `system.task.cancel` as reserved `action_id` values.

---

## Streaming: StreamFrame and Subscriptions

### Streaming Query (stream: true on QueryFrame)

The node pushes `StreamFrame (0x03)` chunks with increasing `seq` values. The final chunk has `is_last=true` (FINAL flag set). To cancel, the Agent sends an `ErrorFrame` referencing the QueryFrame's `request_id`, or disconnects.

### SubscribeFrame (0x12) — Change Subscriptions

Used to establish continuous change subscriptions on Memory and Anchor Nodes. The node pushes incremental updates as `DiffFrame (0x02)` events. The full wire shape is formalised in **spec §13** (CR-0006, Accepted 2026-05-28) and is the authoritative NWP v0.13 form.

**Request fields (§13.1):**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `subscription_id` | string | Required | Client-generated UUID v4; correlates events and cancels the subscription |
| `type` | string | Optional | Reserved subscribe type per §12 (e.g. `"topology.stream"`); absent = per-anchor subscription |
| `anchor_ref` | string | Conditionally Required | anchor_id of the subscribed data; omitted when a reserved `type` defines its own target |
| `filter` | object | Optional | Same filter syntax as QueryFrame `filter`; absent = all events match |
| `heartbeat_interval_ms` | uint32 | Optional | If set, server MUST emit a heartbeat DiffFrame (empty payload, `event_type = "heartbeat"`) at this interval; default 0 (no heartbeat) |
| `max_events` | uint32 | Optional | Server closes the subscription after delivering this many events; 0 = unlimited |
| `cursor` | string | Optional | Opaque server-issued resume position; expired cursor → `NWP-SUBSCRIBE-SEQ-TOO-OLD` |

> **Retired field names (pre-v0.13):** earlier alpha drafts used `action`, `stream_id`, `heartbeat_interval`, and `resume_from_seq`. These are retired for NWP v0.13 and MUST NOT be emitted by conformant alpha.11+ producers; consumers MAY accept them only as a pre-alpha.11 compatibility fallback, normalizing internally to the §13 fields above.

**Pushed DiffFrame event envelope (§13.2):** each event carries `subscription_id`, a monotonically increasing `seq` (uint64), `event_type` (`"create"` / `"update"` / `"delete"` / `"heartbeat"` / `"error"`; reserved subscribe types MAY add more), optional `timestamp`, optional `payload`, and optional `cgn_est` (uint32 — estimated CGN cost of this push event's payload, for Agent-side cumulative-budget accounting; absent means no per-event estimate). Cursors are opaque — clients MUST NOT parse them; on a `seq` gap, re-subscribe with the latest server-issued `cursor`.

**Lifecycle:** client sends SubscribeFrame → server replies CapsFrame (`subscription_id` echoed, `status = "open"`) → server streams DiffFrame events → client cancels by closing the transport (server MAY also close at `max_events`). On error the server MUST send a terminal ErrorFrame with the appropriate `NWP-SUBSCRIBE-*` code.

---

## Reserved Query Types (§12)

The `type` field on `QueryFrame` and `SubscribeFrame` opts a request into a reserved query type with specification-defined semantics. Implementations that do not recognise a reserved `type` value MUST reject the request with `NWP-RESERVED-TYPE-UNSUPPORTED` (HTTP 501) — added in alpha.5 to distinguish "unknown reserved operation" from "action not found."

### topology.snapshot (QueryFrame)

One-shot retrieval of an Anchor Node's cluster topology. Added in alpha.4 via NPS-CR-0002. Mandatory for all Anchor Nodes at NPS-AaaS Profile L2 and above.

**Request:** `QueryFrame` with `type: "topology.snapshot"` and a nested `topology` object:
- `topology.scope`: `"cluster"` (Anchor's own cluster) or `"member"` (with `topology.target_nid`)
- `topology.include`: subset of `["members", "capabilities", "tags", "metrics"]`. Default: `["members"]`
- `topology.depth`: sub-Anchor recursion depth (default 1; depth >= 2 is OPTIONAL at L2)

**Response:** `CapsFrame` with `anchor_ref: "nps:system:topology:snapshot"`, single-element `data` array containing `{version, anchor_nid, cluster_size, members[], truncated}`. The `version` field is a monotonically increasing integer that correlates the snapshot with subsequent `topology.stream` events.

### topology.stream (SubscribeFrame)

Continuous topology change feed. Mandatory at Profile L2 and above.

**Request:** `SubscribeFrame` with `type: "topology.stream"`, a required `subscription_id` (UUID v4), an optional opaque `cursor`, and a nested `topology` object:
- `topology.scope`: `"cluster"` (default)
- `topology.filter`: `{tags_any, tags_all, node_roles}` — reduces event volume; unsupported keys → `NWP-TOPOLOGY-FILTER-UNSUPPORTED`
- `topology.since_version`: topology-specific bootstrap hint for clients that have a snapshot `version` but no opaque `cursor` yet. For v0.13, the opaque `cursor` is the canonical resume mechanism and takes precedence over `topology.since_version` when both are present. If the version is outside the retention window the Anchor MUST emit a `resync_required` event and the client MUST issue a fresh `topology.snapshot`.

**Events** are pushed as `DiffFrame (0x02)` with `event_type` from an extended enum:

| event_type | Trigger |
|------------|---------|
| `member_joined` | New NDP `AnnounceFrame` naming this Anchor as `cluster_anchor` |
| `member_left` | Member left or exceeded NDP liveness TTL |
| `member_updated` | Existing member metadata changed (field-level diff in `changes`) |
| `anchor_state` | Anchor Node internal state change (e.g. version counter rebase after restart) |
| `resync_required` | Subscriber's `topology.since_version` is no longer replayable; client MUST issue a fresh `topology.snapshot` |

The `seq` on each event is the post-event topology version.

### topology:read / topology:subscribe Capability Gate (alpha.5)

Anchor Nodes MUST require the requesting NID to declare `topology:read` in `IdentFrame.capabilities` before serving any `topology.*` request. Absent capability produces `NWP-TOPOLOGY-UNAUTHORIZED`. This is enforced by `AnchorNodeMiddleware` in the .NET reference implementation. The capability is self-declared and key-signed at Phase 1–2; CA-attested role binding is deferred to Phase 3.

The two surfaces are gated separately (spec §12.4):
- `topology.snapshot` (single-shot pull): requires `topology:read`.
- `topology.stream` (long-lived subscription): requires `topology:read` **AND** `topology:subscribe`. Enforcement of `topology:subscribe` was promoted from SHOULD to **MUST** in NWP v0.13 (CR-0006); Anchor Nodes that cannot enforce it MUST document the non-enforcement explicitly in the NWM `stability` metadata.

If the Anchor revokes a subscriber's capability mid-stream, it MUST emit a terminal `NWP-TOPOLOGY-UNAUTHORIZED` event (DiffFrame `event_type = "error"`) and then close the stream rather than silently dropping the subscriber.

---

## HTTP Headers (HTTP Mode)

### Request Headers

| Header | Description |
|--------|-------------|
| `X-NWP-Agent` | Agent NID (required when `auth.required=true`) |
| `X-NWP-Budget` | CGN budget limit (uint32) |
| `X-NWP-Tokenizer` | Tokenizer the Agent is using |
| `X-NWP-Depth` | Graph traversal depth (default 1, max 5) |
| `X-NWP-Encoding` | Request encoding tier: `json` / `msgpack` |
| `X-NWP-Request-ID` | UUID v4 for tracing; echoed in response |
| `X-NWP-Capabilities` | Agent capability list (used by `AnchorNodeMiddleware` for capability gate, alpha.5) |
| `If-None-Match` | NWM conditional request; value is `manifest_version` (uint32). Unchanged manifest → `304 Not Modified` (NWP v0.14) |
| `Content-Type` | MUST be `application/nwp-frame` |

### Response Headers

| Header | Description |
|--------|-------------|
| `X-NWM-Version` | `manifest_version` (uint32); MUST be sent on every `GET /.nwm` response (NWP v0.14) |
| `X-NWP-Schema` | `anchor_id` used in the response |
| `X-NWP-Tokens` | Actual CGN consumed |
| `X-NWP-Tokenizer-Used` | Tokenizer actually applied |
| `X-NWP-Cached` | `"true"` indicates cache hit |
| `X-NWP-Node-Type` | Node type |
| `X-NWP-Rate-Limit` / `X-NWP-Rate-Remaining` / `X-NWP-Rate-Reset` | Rate limit state |

---

## min_assurance_level (NPS-RFC-0003)

Nodes and individual actions may require a minimum assurance level from the calling Agent. The three levels are `anonymous` (default, L0), `attested` (L1), and `verified` (L2). The check is ordered: `anonymous < attested < verified`.

- **Node-level:** declared as `min_assurance_level` in the top-level NWM. All requests to this node must meet the threshold.
- **Per-action override:** declared as `min_assurance_level` on an individual `ActionSpec`. When present, takes precedence over the node-level value for requests targeting that action.

Requests presenting a level below the threshold are rejected with `NWP-AUTH-ASSURANCE-TOO-LOW` (`NPS-AUTH-FORBIDDEN`). The response SHOULD include a `hint` pointing to a CA enrolment URL.

---

## NWP-RESERVED-TYPE-UNSUPPORTED (alpha.5)

When a `QueryFrame` or `SubscribeFrame` carries a `type` field that the node does not recognise as a supported reserved type, the node MUST return `NWP-RESERVED-TYPE-UNSUPPORTED` with HTTP status 501. This is intentionally distinct from `NWP-ACTION-NOT-FOUND` — the unknown operand is `type`, not `action_id`. This allows callers to distinguish "this node does not implement topology queries at all" from "this action_id does not exist on this node."

---

## Error Codes (Selected)

| Error Code | NPS Status | Description |
|------------|------------|-------------|
| `NWP-AUTH-ASSURANCE-TOO-LOW` | `NPS-AUTH-FORBIDDEN` | Agent's assurance level below `min_assurance_level` |
| `NWP-AUTH-REPUTATION-BLOCKED` | `NPS-AUTH-FORBIDDEN` | Reputation policy matched a `reject_on` rule (NPS-RFC-0004, Phase 2) |
| `NWP-MANIFEST-NODE-TYPE-REMOVED` | `NPS-CLIENT-BAD-FRAME` | NWM `node_type` contains retired `"gateway"` (NPS-CR-0001) |
| `NWP-RESERVED-TYPE-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | Unrecognised reserved `type` value (HTTP 501) |
| `NWP-TOPOLOGY-UNAUTHORIZED` | `NPS-AUTH-FORBIDDEN` | Caller lacks `topology:read` capability |
| `NWP-TOPOLOGY-FILTER-UNSUPPORTED` | `NPS-CLIENT-BAD-PARAM` | `topology.filter` contains unrecognised key |
| `NWP-ACTION-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | `action_id` does not exist on this node |
| `NWP-ACTION-PARAMS-INVALID` | `NPS-CLIENT-UNPROCESSABLE` | Parameter schema validation failed |
| `NWP-BUDGET-EXCEEDED` | `NPS-LIMIT-BUDGET` | Response would exceed the CGN token budget |
| `NWP-RATE-LIMIT-EXCEEDED` | `NPS-LIMIT-RATE` | Per-Agent rate limit exceeded |
| `NWP-QUERY-REGEX-UNSAFE` | `NPS-CLIENT-BAD-PARAM` | `$regex` pattern rejected (ReDoS risk or too long) |
| `NWP-SUBSCRIBE-SEQ-TOO-OLD` | `NPS-CLIENT-CONFLICT` | `cursor` outside the node's retention window; full re-query or reserved-type resync required |

---

> Last reviewed at suite version: v1.0.0-alpha.19
>
> Alpha.19 source status reconciled on 2026-09-05.
