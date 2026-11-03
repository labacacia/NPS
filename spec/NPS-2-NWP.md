English | [中文版](./NPS-2-NWP.cn.md)

# NPS-2: Neural Web Protocol (NWP)

**Spec Number**: NPS-2
**Status**: Proposed
**Version**: 0.23
**Date**: 2026-09-09
**Port**: 17433 (default, shared) / 17434 (optional dedicated)
**Authors**: Ori Lynn / INNO LOTUS PTY LTD
**Depends-On**: NPS-1 (NCP v0.13), NPS-3 (NIP v0.16), NPS-4 (NDP v0.14)

> This document is the NWP detailed specification. For a suite overview see [NPS-0-Overview.md](NPS-0-Overview.md).

---

## 1. Terminology

Keywords "MUST", "MUST NOT", "REQUIRED", "SHOULD", "MAY" in this document are interpreted per RFC 2119.

---

## 2. Protocol Overview

NWP defines the AI-native request/response semantics for interacting with Neural Nodes. Agents use `nwp://` addresses to query data, invoke operations, subscribe to changes, route through cluster anchors, and bridge to external protocols across the Memory / Action / Complex / Anchor / Bridge roles. Responses are directly machine-understandable, requiring no semantic parsing layer.

NWP is a semantic protocol layer, not a Memory-Node-only REST API. It can be carried over native NCP sessions or over the HTTP overlay mode defined in §2.2; REST is only an analogy for familiar request/response semantics or an external protocol target of Bridge Nodes.

LLM-serving is expressed as an NWM **LLM/Thinking Profile** (§4.2a) on ordinary Action or Complex Nodes, not as a sixth base `node_type`. This keeps `node_type` tied to protocol responsibility while still giving model-serving nodes a standard discovery shape.

### 2.1 Node Types

| Type | Responsibility | Typical Data Sources |
|------|---------------|---------------------|
| **Memory Node** | Data storage and retrieval, no compute logic | RDS, NoSQL, file systems, vector databases |
| **Action Node** | Executes operations, returns results or side effects | Functions, external APIs, message queues, Webhooks |
| **Complex Node** | Mixed data and operations, with sub-node references | All of the above + sub-node references |
| **Anchor Node** | Cluster control plane and external entry point — routes inbound NWP `Action`/`Query` frames to member nodes via NOP, optionally maintains member-node topology | AaaS platforms, multi-agent service gateways, sub-cluster routers |
| **Bridge Node** | Translates between NPS frames and non-NPS protocols (HTTP/HTTPS, gRPC, MCP, A2A) | Calls to legacy REST APIs, gRPC services, Model Context Protocol servers, Agent-to-Agent endpoints |

A node MAY simultaneously carry more than one role (for example, a single deployment can be both a Memory Node and an Anchor Node when role separation is unnecessary). Multi-role declaration travels in the NDP `Announce` frame's `node_roles` field (NPS-4 §3.1).

> **Thinking Node** is a product-facing alias, not a wire-level node type. A node that serves LLM completions SHOULD declare `node_type: "action"` when it only performs model actions, or `node_type: "complex"` when it also owns memory, tools, routing, or graph behavior. It advertises the standard `profiles.llm` NWM block (§4.2a) and the appropriate NIP capabilities (`llm:*`, NPS-3 §5.1).

> **Anchor Node** and **Bridge Node** were introduced together by [NPS-CR-0001](cr/NPS-CR-0001-anchor-bridge-split.md), replacing the original `Gateway Node` type:
> - **Anchor Node** inherits the cluster-entry / NOP-routing role that Gateway Node was carrying. It is stateless per request but MAY maintain a long-lived registry of member nodes.
> - **Bridge Node** is a new type whose role is **NPS ↔ non-NPS protocol translation, in both directions**. It is stateless per request and does not participate in cluster topology. Direction is declared per protocol in NDP `Announce` (`bridge_protocols` for outbound, `bridge_inbound_protocols` for inbound). ([NPS-CR-0010](cr/NPS-CR-0010-bridge-bidirectional.md) settled this: alpha.3–alpha.15 shipped a normative "outbound-only" narrowing that existed solely to keep the name `Bridge` distinct from the then-separate `compat/*-ingress` packages; those packages are now absorbed into the Bridge package, and the restriction is lifted.)
> - The original `Gateway Node` term is retired; the wire value `"gateway"` is removed and parsers MUST reject it with a clear error referencing CR-0001.

#### Removed types

> **Gateway Node** (removed in v1.0-alpha.3) — Split into **Anchor Node** (cluster entry / NOP routing) and **Bridge Node** (NPS↔non-NPS protocol translation). See [NPS-CR-0001](cr/NPS-CR-0001-anchor-bridge-split.md) for full rationale and migration notes. Implementations MUST reject the legacy `node_type: "gateway"` with `NWP-MANIFEST-NODE-TYPE-REMOVED` and the legacy `node_roles: ["gateway"]` (or legacy `node_kind: "gateway"`) with `NDP-ANNOUNCE-ROLE-REMOVED`; response SHOULD include a `hint` pointing to NPS-CR-0001.

#### Node Role Resolution

Two fields participate in node-role declaration at different protocol layers. They are intentionally distinct in name and semantics:

| Field | Protocol | Layer | Cardinality | Authoritative for |
|-------|----------|-------|-------------|-------------------|
| `node_roles` | NDP `Announce` (NPS-4 §3.1) | Discovery | Array — all roles the node carries | Full role set; used for discovery filtering and cluster membership |
| `node_type` | NWP NWM (§4.1) | Service | String — single operative role | Which role this particular `/.nwm` endpoint is serving |

**Constraint**: `node_type` MUST be one of the values declared in the node's `node_roles`. Validators SHOULD verify this against cached NDP `Announce` data when available.

Multi-role nodes (e.g., `node_roles: ["anchor", "memory"]`) MAY expose separate `/.nwm` endpoints on different paths or ports, each advertising a different `node_type`; or may choose a single dominant `node_type` matching the primary role served at that endpoint. In either case the constraint holds: `node_type ∈ node_roles`.

#### Anchor Node — detailed semantics

An Anchor Node MUST:

1. Accept inbound NWP `Action` and `Query` frames addressed to the cluster (i.e. to the Anchor's NID rather than to a specific member NID).
2. Dispatch frames to appropriate member nodes based on their declared capabilities and current load. The reference dispatch path converts an `ActionFrame` into a NOP `TaskFrame` and delegates to a local NOP orchestrator (see [NPS-AaaS-Profile §2](services/NPS-AaaS-Profile.md)).
3. Aggregate outbound responses from member nodes into a single response stream toward the originating caller.
4. Optionally maintain a registry of member nodes within its cluster (their NIDs, declared capabilities, and `activation_mode` per [NPS-Node Profile §6](services/NPS-Node-Profile.md)). Member nodes register on cluster join via NDP `Announce` frames carrying `cluster_anchor` referencing the Anchor Node's NID; deregistration follows standard NDP offline semantics.

A cluster MUST have at least one Anchor Node. High-availability deployments MAY operate multiple Anchor Nodes for the same cluster; the consensus protocol between Anchor Nodes is implementation-defined and is deferred to NPS-AaaS Profile L3.

Anchor Nodes that maintain a member registry MUST expose it via the reserved query types `topology.snapshot` and `topology.stream` (§12), per [NPS-CR-0002](cr/NPS-CR-0002-anchor-topology-queries.md). Both are mandatory at NPS-AaaS Profile L2 and above.

#### Bridge Node — detailed semantics

A Bridge Node translates in **both directions** between NPS frames and non-NPS protocols. It MUST implement at least one direction, and MUST declare which protocols it serves in which direction via NDP `Announce` (NPS-4 §3.1): `bridge_protocols` for outbound, `bridge_inbound_protocols` for inbound. A Bridge Node MUST NOT be assumed to serve a direction it did not declare. (NPS-CR-0010)

**Outbound — NPS → external protocol.** A Bridge Node serving outbound MUST:

1. Accept inbound NWP frames carrying a `bridge_target` parameter that identifies the external protocol and endpoint. The canonical `bridge_target` wire shape is `{ "protocol", "endpoint", "extras"? }`: `protocol` (string, required — one of `"http"`, `"grpc"`, `"mcp"`, `"a2a"`); `endpoint` (string URL, required); `extras` (object, optional — per-protocol knobs such as HTTP `method`, `headers`, MCP `tool`, or gRPC call metadata). HTTP headers MUST travel inside `bridge_target.extras.headers`, not as a top-level `bridge_target.headers` field. Third-party adapters MAY add fields inside `extras`; unknown top-level fields and unknown `extras` members MUST be ignored by consumers.
2. Produce outbound requests in the target protocol's format.
3. Translate target-protocol responses back into NWP frames (typically `CapsFrame`).

**Inbound — external protocol → NPS.** A Bridge Node serving inbound MUST:

4. Expose a server endpoint for each protocol listed in `bridge_inbound_protocols`, speaking that protocol's native wire format.
5. Translate a foreign-protocol request into NWP frames addressed to the NPS nodes it fronts — Memory Node `Query` for reads, Action / Complex Node `Invoke` for calls — and translate the NWP response back into the foreign protocol's response format.
6. NOT require the foreign client to possess any NPS addressing, NID, or frame knowledge. The foreign client MUST be able to treat the Bridge as a native server of its own protocol.
7. Map NWP / NPS error codes onto the foreign protocol's error space using the normative mapping for that protocol (§16.3).

For **MCP inbound**, a conformant Bridge Node MUST serve `initialize`, `ping`, `tools/list`, `tools/call`, `resources/list`, and `resources/read`. Memory Nodes are projected as MCP resources; Action / Complex Nodes are projected as MCP tools.

A deployment MAY run inbound translation as a plain hosting library in front of NPS nodes, with no NID and no `Announce`. Such a deployment is **not** a Bridge Node — it is the Bridge library, and the MUSTs above do not bind it. Only a deployment that announces `node_roles: ["bridge"]` is a Bridge Node. (NPS-CR-0010 §3.3)

Bridge Nodes are stateless per request and do not participate in cluster topology, in **both** directions. A single Bridge Node MAY serve several protocols and both directions; deployments MAY operate dedicated Bridge Nodes per protocol or per direction for isolation.

Standard external protocols expected to be supported by reference Bridge Node implementations:

- HTTP / HTTPS (REST and streaming)
- gRPC (unary and streaming)
- MCP (Model Context Protocol)
- A2A (Agent-to-Agent protocol)

Additional protocol adapters MAY be registered through future CRs. The set of supported protocols is declared in NDP `Announce.bridge_protocols` / `Announce.bridge_inbound_protocols` (NPS-4 §3.1).

### 2.2 Overlay Mode

Attach a NWP interface to an existing HTTP service. The server distinguishes visitors by request headers:

```
Request contains X-NWP-Agent or HelloFrame  →  return application/nwp-*
Regular browser request (no above markers)  →  return text/html (normal website)
```

In Overlay mode, NWP uses HTTP transport with frames serialized in the HTTP body. See [NPS-1-NCP.md §2.2](NPS-1-NCP.md#22-transport-modes).

---

## 3. Node Address Specification

### 3.1 nwp:// URL Syntax (ABNF)

```abnf
nwp-url     = "nwp://" host [":" port] "/" node-path ["/" sub-path]
host        = <RFC 3986 host>
port        = 1*DIGIT               ; default 17433
node-path   = segment *("/" segment)
sub-path    = "query" / "stream" / "invoke" / "subscribe" / "actions"
            / ".schema" / ".nwm"
segment     = 1*(ALPHA / DIGIT / "-" / "_")
```

### 3.2 Sub-Path Conventions

| Sub-Path | Method | Node Types | Description |
|----------|--------|-----------|-------------|
| `/query` | POST | Memory | Single structured query (returns CapsFrame) |
| `/stream` | POST | Memory | Streaming query (returns StreamFrame sequence) |
| `/invoke` | POST | Action / Complex | Operation invocation endpoint |
| `/subscribe` | POST | Memory | Change subscription endpoint (HTTP mode, SSE) |
| `/actions` | GET | Action / Complex | List callable operations (returns NWM actions subset JSON) |
| `/.schema` | GET | All | Schema definition (returns AnchorFrame JSON) |
| `/.nwm` | GET | All | Full node manifest (returns NWM JSON) |

---

## 4. Neural Web Manifest (NWM)

Every node MUST expose a machine-readable manifest at `/.nwm`, MIME type: `application/nwp-manifest+json`.

### 4.1 Complete Field Definitions

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `nwp` | string | Required | NWP version, currently `"0.4"` |
| `node_id` | string | Required | Node NID, format: `urn:nps:node:{host}:{path}` |
| `node_type` | string | Required | `"memory"` / `"action"` / `"complex"` / `"anchor"` / `"bridge"`. The legacy value `"gateway"` was removed in v1.0-alpha.3 (see §2.1 *Removed types* and [NPS-CR-0001](cr/NPS-CR-0001-anchor-bridge-split.md)); parsers MUST reject it with `NWP-MANIFEST-NODE-TYPE-REMOVED`. Any other unrecognized value MUST be rejected with `NWP-MANIFEST-NODE-TYPE-UNKNOWN`. This field declares the **operative role** this NWP service endpoint is serving — it MUST be one of the values in the node's NDP `Announce.node_roles` (NPS-4 §3.1). For multi-role nodes, `node_type` selects which role is active at this endpoint; see §2.1 *Node Role Resolution* for the full constraint. |
| `display_name` | string | Optional | Human-readable node name |
| `manifest_version` | uint32 | Optional | Monotonically incrementing manifest version counter (starts at 1, incremented on every structural change). Servers MUST include `X-NWM-Version: <manifest_version>` on every `GET /.nwm` response. Agents SHOULD send `If-None-Match: <manifest_version>` on subsequent requests; unchanged manifests return `304 Not Modified`. (NWP v0.14) |
| `manifest_updated_at` | string | Optional | ISO 8601 timestamp of the last manifest change, e.g. `"2026-06-03T12:00:00Z"`. SHOULD be set whenever `manifest_version` is incremented. (NWP v0.14) |
| `min_agent_version` | string | Optional | Minimum NPS version the Agent must support, format `"major.minor"`; Agents below this version MUST be rejected with `NWP-MANIFEST-VERSION-UNSUPPORTED` |
| `min_assurance_level` | string | Optional | One of `"anonymous"` (default) / `"attested"` / `"verified"` (see [NIP §5.1.1](NPS-3-NIP.md#511-assurance-levels-nps-rfc-0003)). Requests presenting a level lower than this MUST be rejected with `NWP-AUTH-ASSURANCE-TOO-LOW` (`NPS-AUTH-FORBIDDEN`); response SHOULD include a `hint` pointing to a CA enrolment URL. Default `"anonymous"` preserves backward compatibility with v1.0-alpha.2 nodes. Per-action override is permitted via the `min_assurance_level` field on an individual `ActionSpec` (§4.6). (NPS-RFC-0003) |
| `wire_formats` | array | Required | Supported encoding formats: `["ncp-capsule", "msgpack", "json"]` |
| `preferred_format` | string | Required | Preferred format |
| `schema_anchors` | object | Optional | Pre-declared schema anchors, `{name: anchor_id}` |
| `capabilities` | object | Required | Node capability declarations, see §4.2 |
| `data_sources` | array | Optional | Underlying data source identifier list |
| `auth` | object | Required | Authentication requirements, see §4.3 |
| `rate_limits` | object | Optional | Rate limit declarations, see §4.4 |
| `actions` | object | Conditionally Required | MUST be populated for Action/Complex nodes; operation registry, see §4.6 |
| `endpoints` | object | Required | URLs for each functional endpoint |
| `graph` | object | Optional | Sub-node references (Complex Node only), see §11 |
| `tokenizer_support` | array | Optional | List of tokenizers supported by the node (see [token-budget.md](token-budget.md)) |
| `stability` | string | Optional | Lifecycle stage: `"experimental"` / `"stable"` / `"deprecated"`. Marketplace / NeuronHub discovery clients use this to filter or warn on non-stable services. Default: `"stable"` (backward-compatible — pre-0.11 manifests are treated as stable). Per-action override permitted via ActionSpec.stability (§4.6). |
| `sla` | object | Optional | SLO commitments for the node, see §4.7. Advisory only; the protocol does not enforce these. Per-action override permitted via ActionSpec.sla (§4.6). |
| `billing` | object | Optional | Commercial metadata for the node (metering profile + price hint), see §4.8. Advisory only; the protocol does not collect or settle charges. Per-action override permitted via ActionSpec.billing (§4.6). |
| `subscription_policy` | object | Optional | Renewable-subscription limits: `default_lease_seconds`, `max_lease_seconds`, and `renew_before_seconds`; see §13.4. Absence means the v0.21 unleased compatibility behavior. |
| `trust_anchors` | array of strings | Optional | NIDs of CA nodes the Anchor accepts as IdentFrame issuers (e.g. `["urn:nps:agent:ca.example.com:root"]`). Consumers SHOULD use this to pre-validate their issuer before connecting. When absent, the node accepts any CA trusted by the NIP verification chain. |
| `profiles` | object | Optional | Structured protocol profiles layered on top of the base node role, see §4.2a. Unknown profile keys MUST be ignored by consumers. |

### 4.2 capabilities Field

| Capability Key | Type | Description |
|---------------|------|-------------|
| `query` | bool | Supports QueryFrame (single query) |
| `stream_query` | bool | Supports streaming queries (StreamFrame response) |
| `aggregate` | bool | Supports aggregation queries (QueryFrame.aggregate) |
| `subscribe` | bool | Supports change subscriptions (DiffFrame push) |
| `subscribe_filter` | bool | Supports subscriptions with filter conditions |
| `vector_search` | bool | Supports vector similarity search |
| `token_budget_hint` | bool | Supports trimming responses based on CGN budget |
| `ext_frame` | bool | Supports extended frame header (large frame mode) |
| `e2e_enc` | bool | Supports NCP E2E encryption (ENC=1, see NPS-1-NCP §7.4) |
| `inline_anchor` | bool | Supports returning updated AnchorFrame inline in responses |

### 4.2a profiles Field

`profiles` is an optional object for structured capability profiles that refine a node's base role. Profiles do **not** introduce new `node_type` values. A consumer that does not understand a profile key MUST ignore that key and continue using the base NWP role, action registry, and capability flags.

#### LLM / Thinking Profile (`profiles.llm`)

A node with `profiles.llm` is an **LLM-capable Action or Complex Node**. Product documentation MAY call it a "Thinking Node", but the canonical NWM `node_type` remains `"action"` or `"complex"`:

- Use `"action"` when the endpoint only runs model actions and returns results.
- Use `"complex"` when the endpoint also owns memory, tool orchestration, graph traversal, session state, or other non-trivial composition.
- The node SHOULD advertise `llm:complete` in its NDP/NIP `capabilities` and MUST implement the `llm.complete` ActionFrame contract (§7.5) when `profiles.llm.actions` contains `"llm.complete"`.
- If `supports_stream = true`, the node SHOULD also advertise `llm:stream`; if `supports_tools = true`, it SHOULD advertise `llm:tool_call`.
- A node that advertises `context.supported = true` MUST also advertise `llm:context`, list the implemented lifecycle actions, and implement §7.6 without silent stateless fallback.

**`profiles.llm` fields**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `profile_version` | string | Optional | LLM profile schema version. `"0.1"` is the stateless profile; `"0.2"` adds the optional stateful-context descriptor below. |
| `actions` | array[string] | Optional | Standard LLM action ids implemented by this node. Default: `["llm.complete"]` |
| `provider` | string | Optional | Provider/runtime family, e.g. `"willow"`, `"ollama"`, `"openai-compatible"`. Advisory only |
| `default_model` | string | Optional | Model id used when a request omits provider-specific routing hints |
| `models` | array | Optional | Model descriptors, see below |
| `supports_stream` | bool | Optional | Whether `llm.complete` accepts `stream=true` and returns a StreamFrame sequence |
| `supports_tools` | bool | Optional | Whether tool definitions and tool-call responses are supported |
| `supports_json_mode` | bool | Optional | Whether structured JSON/object completions are supported |
| `supports_embeddings` | bool | Optional | Whether embedding actions such as `llm.embed` are supported |
| `supports_rerank` | bool | Optional | Whether rerank actions such as `llm.rerank` are supported |
| `reasoning_visibility` | string | Optional | `"none"` / `"summary"` / `"trace"`. `trace` exposes provider reasoning artifacts and is deployment-sensitive |
| `privacy` | object | Optional | Operator privacy policy hints, see below |
| `context` | object | Optional | Stateful LLM context support and operational limits; requires `profile_version = "0.2"` and §7.6 |

**Model descriptor**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | Required | Model id accepted by `LlmCompleteActionRequest.model` |
| `display_name` | string | Optional | Human-readable model name |
| `modalities` | array[string] | Optional | Supported modalities, e.g. `["text"]` or `["text","image"]` |
| `context_window` | uint32 | Optional | Maximum input context window in native model tokens |
| `max_output_tokens` | uint32 | Optional | Maximum output tokens permitted by this node |
| `tokenizer` | string | Optional | Tokenizer id used for estimates and CGN hints |
| `cgn_profile` | string | Optional | CGN conversion profile id from `cgn-profiles.yaml`, when known |

**Privacy descriptor**

| Field | Type | Description |
|-------|------|-------------|
| `retention` | string | Prompt/response retention policy, e.g. `"none"` / `"session"` / `"30d"` |
| `training` | bool | Whether prompt/response content may be used for model training |
| `region` | string | Optional processing or storage region hint |

**Stateful context descriptor (`profiles.llm.context`)**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `supported` | bool | Required | Whether this node implements the §7.6 contract. `false` MUST NOT be inferred as partial support. |
| `operations` | array[string] | Required when supported | Implemented operations from `create`, `append`, `fork`, `reset`, `release`. |
| `persistence` | string | Required when supported | `connection`, `process`, or `durable`, with the lifecycle guarantees in §7.6.5. |
| `max_contexts_per_principal` | uint32 | Required when supported | Maximum live contexts owned by one authenticated principal/security scope. |
| `max_ttl_seconds` | uint32 | Required when supported | Maximum accepted idle TTL. |
| `tombstone_seconds` | uint32 | Required when supported | Minimum released/expired tombstone visibility promised by the node. |

**Example**

```json
{
  "node_type": "action",
  "capabilities": { "query": false, "stream_query": false, "token_budget_hint": true },
  "actions": {
    "llm.complete": {
      "description": "Complete a chat conversation",
      "params_anchor": "nps:system:llm.complete:request",
      "result_anchor": "nps:system:llm.complete:response",
      "async": true,
      "required_capability": "llm:complete"
    },
    "llm.context.status": {
      "description": "Inspect an LLM context or recover an idempotent create outcome",
      "params_anchor": "nps:system:llm.context.status:request",
      "result_anchor": "nps:system:llm.context.status:response",
      "async": false,
      "required_capability": "llm:context"
    },
    "llm.context.release": {
      "description": "Release an LLM context",
      "params_anchor": "nps:system:llm.context.release:request",
      "result_anchor": "nps:system:llm.context.release:response",
      "async": false,
      "required_capability": "llm:context"
    }
  },
  "profiles": {
    "llm": {
      "profile_version": "0.2",
      "provider": "willow",
      "default_model": "willow-small",
      "actions": ["llm.complete", "llm.context.status", "llm.context.release"],
      "supports_stream": true,
      "supports_tools": true,
      "reasoning_visibility": "summary",
      "context": {
        "supported": true,
        "operations": ["create", "append", "fork", "reset", "release"],
        "persistence": "process",
        "max_contexts_per_principal": 32,
        "max_ttl_seconds": 3600,
        "tombstone_seconds": 86400
      },
      "models": [
        {
          "id": "willow-small",
          "modalities": ["text"],
          "context_window": 128000,
          "max_output_tokens": 8192,
          "tokenizer": "cl100k_base",
          "cgn_profile": "oa.reasoning"
        }
      ],
      "privacy": { "retention": "none", "training": false, "region": "us" }
    }
  }
}
```

### 4.3 auth Field

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `required` | bool | Required | Whether authentication is required |
| `identity_type` | string | Conditionally Required | `"nip-cert"` / `"bearer"` / `"none"` |
| `trusted_issuers` | array | Conditionally Required | List of trusted CA URLs (required when identity_type is nip-cert) |
| `required_capabilities` | array | Optional | Capabilities the Agent MUST hold, e.g. `["nwp:query"]` |
| `scope_check` | string | Optional | Scope validation mode: `"prefix"` (default) / `"exact"` |
| `ocsp_url` | string | Optional | OCSP validation endpoint |

### 4.4 rate_limits Field

| Field | Type | Description |
|-------|------|-------------|
| `requests_per_minute` | uint32 | Max requests per Agent per minute |
| `requests_per_day` | uint32 | Max requests per Agent per day |
| `max_concurrent_streams` | uint32 | Max concurrent streams per Agent |
| `max_subscriptions` | uint32 | Max concurrent subscriptions per Agent |

### 4.4a sla Field

Advisory SLO commitments. Clients (especially marketplace / NeuronHub aggregators) display or filter on these values; the protocol does not police them. All sub-fields are Optional.

| Field | Type | Description |
|-------|------|-------------|
| `p95_latency_ms` | uint32 | Self-declared 95th-percentile end-to-end latency in milliseconds, measured at the node's `/.nwm` reference endpoint |
| `availability` | string | Self-declared availability target as a decimal-fraction string, e.g. `"0.999"` for "three nines". Format: `0\.[0-9]+`; clients SHOULD interpret missing as "best effort". |
| `sla_tier` | string | Optional named tier for marketplace listing: `"best-effort"` / `"standard"` / `"premium"`. Free-form strings outside this enum are reserved for future extension and SHOULD be ignored by current clients. |

### 4.4b billing Field

Advisory commercial metadata. The protocol does not authorize, meter, or settle charges; this field exists so marketplace listings, agent autoscalers, and budget gates can make informed decisions before invoking. All sub-fields are Optional.

| Field | Type | Description |
|-------|------|-------------|
| `metering_profile` | string | Identifier of the metering model: `"free"` / `"metered"` / `"flat-rate"`. Free-form values outside this enum are reserved and SHOULD be treated as `"metered"` by conservative clients. |
| `billing_unit` | string | Unit string when `metering_profile = "metered"`, e.g. `"per-token"` / `"per-request"` / `"per-cgn"` / `"per-second"`. |
| `price_hint` | string | Indicative price in ISO-4217 currency-prefixed decimal form, e.g. `"USD 0.0002"` per `billing_unit`. Hint only — the operator's external contract is authoritative. |
| `currency` | string | ISO-4217 currency code (e.g. `"USD"`, `"EUR"`, `"CNY"`). Optional convenience field; `price_hint` already encodes the currency prefix. |

### 4.4c Alpha.19 portable metadata normalization (NWP v0.22)

The requirement IDs below are exercised by
[`alpha19_hardening_vectors.json`](conformance/nwp/alpha19_hardening_vectors.json):

1. **NWP-P19-01 — stability.** Producers MUST emit only `experimental`,
   `stable`, or `deprecated`. Absence normalizes to `stable`. Consumers preserve
   an unknown raw value but normalize it to `experimental` for filtering and
   MUST NOT rank it as stable.
2. **NWP-P19-02 — SLA.** `p95_latency_ms`, when present, is a positive uint32;
   `availability` is a decimal fraction in `(0, 1]`; and the three standard
   `sla_tier` values retain their defined ordering. An unknown tier is preserved
   but is unranked. Invalid advisory sub-fields are ignored with a diagnostic;
   they do not invalidate unrelated manifest fields.
3. **NWP-P19-03 — billing.** `free` forbids `billing_unit`, `price_hint`, and
   `currency`; `metered` requires a non-empty `billing_unit`; a `price_hint`
   MUST match `^[A-Z]{3} [0-9]+(\\.[0-9]+)?$`, and a separate `currency`, when
   present, MUST equal its prefix. Unknown metering profiles normalize to
   `metered` without inventing a price.
4. **NWP-P19-04 — action overrides.** Action-level metadata overrides only
   fields it supplies. Missing fields fall through to the normalized top-level
   value. An invalid override sub-field is ignored and falls through rather
   than erasing a valid top-level value.

### 4.5 Complete NWM Example

```json
{
  "nwp": "0.4",
  "node_id": "urn:nps:node:api.example.com:orders",
  "node_type": "complex",
  "display_name": "Order Management Node",
  "manifest_version": 7,
  "manifest_updated_at": "2026-06-03T00:00:00Z",
  "min_agent_version": "0.3",
  "wire_formats": ["ncp-capsule", "msgpack", "json"],
  "preferred_format": "ncp-capsule",
  "schema_anchors": {
    "order":   "sha256:a3f9b2c1...",
    "product": "sha256:b2c1d3e4..."
  },
  "capabilities": {
    "query": true,
    "stream_query": true,
    "aggregate": true,
    "subscribe": true,
    "subscribe_filter": true,
    "vector_search": false,
    "token_budget_hint": true,
    "ext_frame": false,
    "e2e_enc": false,
    "inline_anchor": true
  },
  "data_sources": ["rds:orders_db"],
  "auth": {
    "required": true,
    "identity_type": "nip-cert",
    "trusted_issuers": ["https://ca.mycompany.com"],
    "required_capabilities": ["nwp:query", "nwp:invoke"],
    "scope_check": "prefix"
  },
  "rate_limits": {
    "requests_per_minute": 300,
    "requests_per_day": 50000,
    "max_concurrent_streams": 10,
    "max_subscriptions": 5
  },
  "stability": "stable",
  "sla": {
    "p95_latency_ms": 250,
    "availability": "0.999",
    "sla_tier": "standard"
  },
  "billing": {
    "metering_profile": "metered",
    "billing_unit": "per-request",
    "price_hint": "USD 0.0002",
    "currency": "USD"
  },
  "actions": {
    "orders.create": {
      "description": "Create a new order",
      "params_anchor": "sha256:create_params...",
      "result_anchor": "sha256:order...",
      "async": true,
      "idempotent": true,
      "timeout_ms_default": 10000,
      "timeout_ms_max": 60000,
      "required_capability": "nwp:invoke",
      "stability": "stable",
      "sla": { "p95_latency_ms": 800, "sla_tier": "premium" },
      "billing": { "metering_profile": "metered", "billing_unit": "per-request", "price_hint": "USD 0.001" }
    },
    "orders.cancel": {
      "description": "Cancel an existing order",
      "params_anchor": "sha256:cancel_params...",
      "result_anchor": "sha256:cancel_result...",
      "async": false,
      "idempotent": true,
      "timeout_ms_default": 5000,
      "timeout_ms_max": 10000,
      "required_capability": "nwp:invoke",
      "stability": "experimental"
    }
  },
  "endpoints": {
    "query":     "nwp://api.example.com/orders/query",
    "stream":    "nwp://api.example.com/orders/stream",
    "invoke":    "nwp://api.example.com/orders/invoke",
    "subscribe": "nwp://api.example.com/orders/subscribe",
    "actions":   "nwp://api.example.com/orders/actions",
    "schema":    "nwp://api.example.com/orders/.schema"
  },
  "tokenizer_support": ["cl100k_base", "claude"]
}
```

**NWM Conditional Requests**

Agents SHOULD cache the NWM and use `manifest_version` for conditional requests: in HTTP mode via the `If-None-Match: <manifest_version>` header (integer string, e.g. `If-None-Match: 7`); if the manifest is unchanged the server returns `304 Not Modified`. Servers MUST include `X-NWM-Version: <manifest_version>` on every `GET /.nwm` response so agents can detect staleness without a full re-fetch. `manifest_updated_at` provides a human-readable timestamp of the last structural change.

### 4.6 NWM Action Registry

The `actions` field is an `{action_id: ActionSpec}` dictionary. Action/Complex/Anchor Nodes MUST declare all callable operations here.

**ActionSpec Field Definitions**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `description` | string | Optional | Human-readable description of the operation |
| `params_anchor` | string | Optional | anchor_id of the parameter Schema (Agent uses this to validate ActionFrame.params) |
| `result_anchor` | string | Optional | anchor_id of the result Schema (CapsFrame uses this anchor_ref on success) |
| `async` | bool | Required | Whether async execution is supported (if true, ActionFrame may set async=true) |
| `idempotent` | bool | Optional | Whether the operation is idempotent (Agents may safely retry if true) |
| `timeout_ms_default` | uint32 | Optional | Default timeout in milliseconds |
| `timeout_ms_max` | uint32 | Optional | Maximum allowed timeout in milliseconds |
| `required_capability` | string | Optional | NIP capability required to invoke this operation, e.g. `"nwp:invoke"` |
| `min_assurance_level` | string | Optional | Per-action assurance-level override: `"anonymous"` / `"attested"` / `"verified"`. When present, takes precedence over the top-level NWM `min_assurance_level` for requests targeting this action. Requests presenting a lower level MUST be rejected with `NWP-AUTH-ASSURANCE-TOO-LOW`. (NPS-RFC-0003) |
| `stability` | string | Optional | Per-action lifecycle stage override (`"experimental"` / `"stable"` / `"deprecated"`). When present, takes precedence over the top-level NWM `stability` for this action. Marketplace clients SHOULD surface deprecated actions even when the node-level stability is `"stable"`. |
| `sla` | object | Optional | Per-action SLO override; same shape as the top-level `sla` field (§4.4a). When present, fields supplied here override the matching top-level fields for this action only; unsupplied sub-fields fall through to the top-level. |
| `billing` | object | Optional | Per-action commercial-metadata override; same shape as the top-level `billing` field (§4.4b). Field-level fallback semantics match `sla`. |

**`/actions` Endpoint**

An Agent sends `GET /actions`; the node returns the full NWM `actions` field as JSON (for dynamic discovery without downloading the entire NWM):

```json
{
  "node_id": "urn:nps:node:api.example.com:orders",
  "actions": {
    "orders.create": { ... },
    "orders.cancel": { ... }
  }
}
```

---

## 5. Schema Retrieval Flow

Agents retrieve a Node's schema via the following flow (AnchorFrames are published by Nodes; Agents are read-only consumers):

```
Agent                              Node
  │                                  │
  │── GET /.nwm ─────────────────→   │  1. Read manifest, get schema_anchors
  │←── NWM JSON ──────────────────   │     { "order": "sha256:a3f9..." }
  │                                  │
  │── GET /.schema ──────────────→   │  2. Fetch complete AnchorFrame (on demand)
  │←── AnchorFrame JSON ──────────   │     Agent caches locally
  │                                  │
  │── QueryFrame(anchor_ref) ────→   │  3. Query carries only anchor_ref
  │←── CapsFrame(anchor_ref) ─────   │
```

Agents SHOULD preload AnchorFrames for all `schema_anchors` declared in the NWM on first connection, reducing latency for subsequent requests.

---

## 6. QueryFrame (0x10)

Used for structured data queries on Memory Nodes.

### 6.1 Field Definitions

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `frame` | uint8 | Required | Fixed value `0x10` |
| `type` | string | Optional | Reserved query type identifier per §12. When set, type-specific fields apply and `anchor_ref` semantics are defined by the type. Absent: per-anchor query behavior below |
| `anchor_ref` | string | Conditionally Required | Schema anchor_id; may be omitted for aggregation queries or when `type` selects a reserved type that does not require it |
| `auto_anchor` | bool | Optional | If true and the anchor is stale, the Node automatically attaches the latest AnchorFrame in the response. Default: true |
| `stream` | bool | Optional | If true, triggers streaming query mode (see §6.6); response is a StreamFrame sequence rather than a CapsFrame |
| `aggregate` | object | Optional | Aggregation operation (see §6.7); when set, `anchor_ref` may be omitted |
| `filter` | object | Optional | Filter conditions, see §6.2 |
| `fields` | array | Optional | List of fields to return; omit to return all fields |
| `limit` | uint32 | Optional | Maximum records to return, default 20, max 1000; for streaming queries, max records per frame |
| `cursor` | string | Optional | Pagination cursor from the previous response's `next_cursor` |
| `order` | array | Optional | Sort rules, see §6.3 |
| `vector_search` | object | Optional | Vector similarity search, see §6.4 |
| `token_budget` | uint32 | Optional | CGN budget limit (native mode equivalent of `X-NWP-Budget`) |
| `tokenizer` | string | Optional | Tokenizer identifier in use (native mode equivalent of `X-NWP-Tokenizer`) |
| `depth` | uint8 | Optional | Node graph traversal depth, default 1, max 5 (native mode equivalent of `X-NWP-Depth`) |
| `request_id` | string | Optional | UUID v4 for request tracing; echoed back by the node in response and logs |

### 6.2 Filter Syntax

| Operator | Meaning | Example |
|----------|---------|---------|
| `$eq` | Equal | `{ "status": { "$eq": "active" } }` |
| `$ne` | Not equal | `{ "status": { "$ne": "deleted" } }` |
| `$lt` | Less than | `{ "price": { "$lt": 500 } }` |
| `$lte` | Less than or equal | `{ "price": { "$lte": 500 } }` |
| `$gt` | Greater than | `{ "stock": { "$gt": 0 } }` |
| `$gte` | Greater than or equal | `{ "rating": { "$gte": 4.0 } }` |
| `$in` | In list | `{ "category": { "$in": ["phone", "tablet"] } }` |
| `$nin` | Not in list | `{ "tag": { "$nin": ["discontinued"] } }` |
| `$contains` | String contains (case-sensitive) | `{ "name": { "$contains": "Pro" } }` |
| `$between` | Range (inclusive on both ends) | `{ "price": { "$between": [100, 500] } }` |
| `$exists` | Field exists check | `{ "thumbnail": { "$exists": true } }` |
| `$regex` | Regex match (UTF-8) | `{ "sku": { "$regex": "^PROD-[0-9]{4}$" } }` |
| `$and` | Logical AND | `{ "$and": [ {...}, {...} ] }` |
| `$or` | Logical OR | `{ "$or": [ {...}, {...} ] }` |
| `$not` | Logical NOT | `{ "$not": { "status": { "$eq": "deleted" } } }` |

**`$regex` Security Constraints**: Pattern length ≤ 256 characters; nested quantifiers (e.g. `(a+)+`) are prohibited; nodes MUST perform ReDoS detection and return `NWP-QUERY-REGEX-UNSAFE` on violation.

Filter nesting depth MUST be ≤ 8 levels.

### 6.3 Sort Rules

```json
{ "order": [{ "field": "price", "dir": "ASC" }, { "field": "name", "dir": "ASC" }] }
```

### 6.4 Vector Search

```json
{
  "vector_search": {
    "field": "embedding",
    "vector": [0.12, -0.34, 0.56],
    "top_k": 10,
    "threshold": 0.85,
    "metric": "cosine"
  }
}
```

Supported `metric` values: `cosine` (default), `euclidean`, `dot_product`. Nodes declare support via `capabilities.vector_search=true`; unsupported nodes return `NWP-QUERY-VECTOR-UNSUPPORTED`.

### 6.5 Single Query Complete Example

```json
{
  "frame": "0x10",
  "anchor_ref": "sha256:a3f9b2c1...",
  "auto_anchor": true,
  "filter": {
    "$and": [
      { "category": { "$eq": "electronics" } },
      { "price": { "$lt": 500 } },
      { "stock": { "$gt": 0 } }
    ]
  },
  "fields": ["id", "name", "price", "stock"],
  "limit": 20,
  "order": [{ "field": "price", "dir": "ASC" }],
  "token_budget": 800,
  "tokenizer": "cl100k_base",
  "request_id": "550e8400-e29b-41d4-a716-446655440001"
}
```

### 6.6 Streaming Query Protocol

When QueryFrame contains `stream: true` (or uses the `/stream` sub-path), the node responds with a **StreamFrame (0x03) sequence** rather than a single CapsFrame. Requires `capabilities.stream_query=true`.

**Streaming Query Flow**

```
Agent                              Node
  │                                  │
  │── QueryFrame(stream:true) ────→  │
  │                                  │  Query in batches, limit records per batch
  │  ←── StreamFrame(seq=0) ───────  │  First frame, contains anchor_ref and estimated_total
  │  ←── StreamFrame(seq=1) ───────  │  Subsequent frames, data is the next batch of records
  │       ...                        │
  │  ←── StreamFrame(is_last=true) ─ │  Final frame, is_last=true, data may be empty
```

**First Frame Additional Fields (StreamFrame Extension)**

For streaming queries, the first frame (seq=0) SHOULD include metadata:

| Field | Type | Description |
|-------|------|-------------|
| `estimated_total` | uint64 | Estimated total records matching the filter; -1 means unknown |
| `request_id` | string | Echo of the QueryFrame's request_id |

**Pagination vs. Streaming**

- Streaming queries do not use `cursor`; records are pushed continuously per `order` until `limit × frames` or full push completes
- To terminate early, the Agent sends an ErrorFrame referencing the QueryFrame's `request_id`, or disconnects. Nodes route the cancellation by `request_id` and MUST NOT require a SubscribeFrame-shaped cancel message for streaming queries.
- The node MUST stop pushing after the connection is closed

### 6.7 Aggregation Queries

When QueryFrame contains an `aggregate` field, the node returns aggregated results rather than raw records. Requires `capabilities.aggregate=true`.

**aggregate Field Definitions**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `operations` | array | Required | List of aggregation operations, see below |
| `group_by` | array | Optional | Grouping field list, e.g. `["category", "status"]` |
| `having` | object | Optional | Post-grouping filter (same syntax as filter, but field names are aliases) |

**operation Element**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `func` | string | Required | `COUNT` / `SUM` / `AVG` / `MIN` / `MAX` / `COUNT_DISTINCT` |
| `field` | string | Conditionally Required | Field to aggregate (`COUNT` may omit, meaning row count) |
| `alias` | string | Required | Result field name |

**Aggregation Query Example**

```json
{
  "frame": "0x10",
  "filter": { "status": { "$eq": "active" } },
  "aggregate": {
    "operations": [
      { "func": "COUNT", "alias": "total" },
      { "func": "SUM",   "field": "price",  "alias": "revenue" },
      { "func": "AVG",   "field": "rating", "alias": "avg_rating" }
    ],
    "group_by": ["category"],
    "having": { "total": { "$gt": 10 } }
  },
  "order": [{ "field": "revenue", "dir": "DESC" }],
  "request_id": "550e8400-e29b-41d4-a716-446655440002"
}
```

**Aggregation Response (CapsFrame)**

Aggregation responses do not use a business schema; `anchor_ref` is fixed as `"nps:system:aggregate:result"`:

```json
{
  "frame": "0x04",
  "anchor_ref": "nps:system:aggregate:result",
  "count": 3,
  "data": [
    { "category": "electronics", "total": 142, "revenue": 89450.00, "avg_rating": 4.3 },
    { "category": "clothing",    "total": 87,  "revenue": 12300.00, "avg_rating": 4.1 },
    { "category": "books",       "total": 56,  "revenue": 3200.00,  "avg_rating": 4.6 }
  ]
}
```

---

## 7. ActionFrame (0x11)

Used for operation invocation on Action Nodes and Complex Nodes.

### 7.1 Field Definitions

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `frame` | uint8 | Required | Fixed value `0x11` |
| `action_id` | string | Required | Operation identifier, format: `{domain}.{verb}`; system-reserved operations see §7.3 |
| `params` | object | Optional | Operation parameters; schema defined by NWM `actions.{action_id}.params_anchor` |
| `idempotency_key` | string | Optional | Idempotency key (UUID v4), valid for 24 hours |
| `timeout_ms` | uint32 | Optional | Timeout in milliseconds, default 5000, max 300000 |
| `async` | bool | Optional | If true, execute asynchronously; response returns `task_id` |
| `callback_url` | string | Optional | Callback URL for async task completion (`https://` only) |
| `priority` | string | Optional | Task priority: `"low"` / `"normal"` (default) / `"high"` |
| `request_id` | string | Optional | UUID v4 for request tracing (echoed in response and task status) |

### 7.2 Async Task State Machine

```
PENDING → RUNNING → COMPLETED
                  ↘ FAILED
                  ↘ CANCELLED
```

For async execution, the initial response (CapsFrame):

```json
{
  "task_id": "uuid-v4",
  "status": "pending",
  "poll_url": "nwp://api.example.com/orders/actions/status/uuid-v4",
  "estimated_ms": 3000,
  "request_id": "550e8400-..."
}
```

### 7.3 System-Reserved Operations

All nodes supporting async Actions MUST implement:

| action_id | Description | Required Params | Response |
|-----------|-------------|-----------------|----------|
| `system.task.status` | Poll task status | `{ "task_id": "uuid" }` | Task status object (see below) |
| `system.task.cancel` | Cancel a task | `{ "task_id": "uuid" }` | `{ "cancelled": true }` or error |

**`system.task.status` Response**

```json
{
  "task_id": "uuid-v4",
  "status": "running",
  "progress": 0.42,
  "created_at": "2026-04-14T10:00:00Z",
  "updated_at": "2026-04-14T10:00:05Z",
  "request_id": "550e8400-...",
  "result": null,
  "error": null
}
```

### 7.4 Complete Example

```json
{
  "frame": "0x11",
  "action_id": "orders.create",
  "params": { "product_id": 1001, "quantity": 2 },
  "idempotency_key": "550e8400-e29b-41d4-a716-446655440000",
  "timeout_ms": 10000,
  "async": true,
  "callback_url": "https://agent.myapp.com/callbacks/nwp",
  "priority": "normal",
  "request_id": "550e8400-e29b-41d4-a716-446655440003"
}
```

### 7.5 Standard LLM Completion Action (`llm.complete`)

NWP standardizes the `llm.complete` action so SDKs and agent runtimes do not
need private ActionFrame payload codecs for ordinary model completions.
Nodes that serve this action SHOULD also advertise the LLM/Thinking Profile in
NWM `profiles.llm` (§4.2a) so clients can discover models, streaming/tool
support, privacy hints, and reasoning-disclosure policy without invoking a
model request.

**Request binding**

- The wire frame is `ActionFrame` with `action_id = "llm.complete"`.
- `ActionFrame.params` MUST contain a `LlmCompleteActionRequest` object.
- `params.kind` MUST be `"llm.complete"` when present. Producers SHOULD emit it
  for self-description and compatibility with pre-contract clients; consumers
  MAY accept an absent `kind` only when `action_id` is already `"llm.complete"`.
- `ActionFrame.async = true` requests asynchronous execution. `params.stream =
  true` requests an immediate `StreamFrame` response. The two flags MUST NOT be
  combined; servers SHOULD reject the combination with `NWP-ACTION-PARAMS-INVALID`.

**`LlmCompleteActionRequest` fields**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `kind` | string | Optional | Self-description. Canonical value: `"llm.complete"` |
| `model` | string | Required | Model identifier as understood by the receiving LLM node |
| `max_tokens` | uint32 | Optional | Maximum generated tokens |
| `stream` | bool | Optional | When true, response is a `StreamFrame` sequence instead of a synchronous `CapsFrame` |
| `messages` | array | Required | Ordered conversation messages |
| `tools` | array | Optional | Tool definitions available to the model |
| `context` | `LlmContextRequestDto` | Optional | Stateful context operation defined in §7.6. Absent preserves stateless full-history semantics. |

**`LlmMessageDto` fields**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `role` | string | Required | `"system"` / `"user"` / `"assistant"` / `"tool"` |
| `content` | string | Optional | Message text or serialized tool result |
| `tool_call_id` | string | Optional | Tool-call id referenced by a `"tool"` message |
| `tool_name` | string | Optional | Tool name referenced by a `"tool"` message |
| `tool_calls` | array | Optional | Tool calls emitted by an assistant message |

**Tool fields**

`LlmToolCallDto` uses `{ "call_id", "tool_name", "arguments_json" }`.
`arguments_json` is a JSON string so providers can preserve the exact argument
object received from or sent to an LLM backend. `LlmToolDefinitionDto` uses
`{ "name", "description", "parameters" }`, where each `ToolParameterDto` uses
`{ "name", "type", "description", "required" }`. Standard `type` values are
`"string"`, `"number"`, `"boolean"`, `"object"`, and `"array"`.

**Success response semantics**

| Request mode | Successful response |
|--------------|---------------------|
| `async=false`, `stream=false` | `CapsFrame` with `anchor_ref = "nps:system:llm.complete:response"`, `request_id` copied from the ActionFrame when present, and `data[0]` containing `LlmCompleteActionResponse` |
| `async=true`, `stream=false` | `AsyncActionResponse` acknowledgment. `system.task.status.result` contains `LlmCompleteActionResponse` when completed |
| `stream=true` | `StreamFrame` sequence with `anchor_ref = "nps:system:llm.complete:stream"` on the first chunk; `data[]` contains `LlmCompleteStreamChunkDto` items |

Fire-and-forget is not a separate `llm.complete` mode. Clients that do not care
about the result MAY submit an async request and ignore the poll URL, but the
server still follows the async task contract.

**`LlmCompleteActionResponse` fields**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `stop_reason` | enum | Required | `"end_turn"` / `"tool_use"` / `"tool_calls"` / `"max_tokens"` / `"length"` / `"error"` |
| `content` | string | Optional | Final generated text for non-tool completions |
| `tool_calls` | array | Optional | Tool calls requested by the model |
| `error` | string | Optional | Model/provider-level completion error; see error rule below |
| `usage` | object | Optional | Actual model/provider usage for this invocation; see `LlmUsageDto` below |
| `context` | `LlmContextReceiptDto` | Optional | Committed stateful-context receipt; MUST be absent for stateless requests. |

For streaming responses, `LlmCompleteStreamChunkDto.content_delta` carries the
new text for that chunk. The final chunk SHOULD set `stop_reason`; abnormal
stream termination SHOULD use a terminal `ErrorFrame` or `StreamFrame.error_code`.
The terminal chunk MAY carry `usage`. A successful stateful stream MUST carry
its context receipt only on the terminal chunk. Non-terminal chunks MUST omit
the receipt and SHOULD omit usage.

**`LlmUsageDto` fields**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `input_tokens` | uint32 | Optional | Total logical model-input tokens, including any reused prefix |
| `output_tokens` | uint32 | Optional | Tokens generated by the model for this completion |
| `cache_hit` | bool | Optional | Whether the model runtime reused a prefix/KV-cache entry |
| `reused_tokens` | uint32 | Optional | Input tokens reused from prefix/KV cache without new evaluation |
| `evaluated_tokens` | uint32 | Optional | Input tokens newly evaluated by the model for this invocation |
| `wire_input_bytes` | uint64 | Optional | Complete serialized ActionFrame payload bytes measured at the NWP decoder boundary, after NCP decryption and excluding the NCP header/TLS/response bytes |

Every usage field is optional because providers expose different accounting
levels. Values in `usage` MUST be actual runtime/provider observations, not
estimates. `CapsFrame.token_est` remains a CGN estimate and is not a substitute
for `usage`. `CapsFrame.cached` means the complete NWP response came from the
server-side response cache; it is distinct from `usage.cache_hit`. When all
three input-accounting fields are known, producers SHOULD satisfy
`reused_tokens + evaluated_tokens = input_tokens`.
`cache_hit = true` is valid only when `reused_tokens > 0`. `wire_input_bytes`
MUST be measured from the accepted wire payload, not obtained by re-serializing
a DTO. Providers MUST omit token fields they cannot observe rather than derive
them from prompt length.

**Error rule**

Protocol, validation, authorization, timeout, and provider dispatch failures
SHOULD be returned as `ErrorFrame` using the normal NWP error mapping. The
`LlmCompleteActionResponse.error` field is reserved for model-level failures
that are themselves a successful action result, for example a provider returning
a structured "model refused/error" completion instead of raising a transport or
server error.

**Field naming and encoding**

Canonical JSON field names are snake_case. SDK producers MUST emit snake_case.
SDK consumers SHOULD accept PascalCase/case-insensitive property names as a
compatibility fallback. MessagePack payload maps MUST use the same canonical
snake_case keys as JSON.

### 7.6 Stateful LLM Context and Delta Completion

This section implements NPS-CR-0011. It is an opt-in extension of §7.5, not a
new frame family. A request without `LlmCompleteActionRequest.context` remains
stateless and sends a complete ordered message list. A request with `context`
MUST execute the requested state transition exactly or fail with an ErrorFrame;
the server MUST NOT silently rebuild a stateless prompt or report a false cache
hit.

#### 7.6.1 Request and receipt DTOs

A stateful `llm.complete` ActionFrame MUST carry an `idempotency_key`. Its
`messages` field means the complete initial/replacement transcript for `create`
and `reset`, and only the ordered delta after `base_version` for `append` and
`fork`. An empty message array is allowed only for `fork`.

**`LlmContextRequestDto`**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `operation` | enum | Required | `create`, `append`, `fork`, or `reset`. |
| `context_id` | string | Conditional | Required for all operations except `create`; forbidden for `create`. |
| `base_version` | uint64 | Conditional | Required for `append`, `fork`, and `reset`; MUST equal the committed version. |
| `ttl_seconds` | uint32 | Optional | Requested idle TTL; the server MAY clamp it to its advertised maximum. |

`ttl_seconds` MUST be greater than zero when present. If omitted, `create` uses
the node default; `append` and `reset` preserve the context's current effective
TTL; and `fork` inherits the source context's remaining TTL. A supplied value or
an inherited/default value MAY be clamped to `max_ttl_seconds`; the effective
expiry MUST be returned in the receipt when TTL is bounded.

Context IDs are case-sensitive, unpadded base64url strings generated from at
least 128 bits of cryptographically secure randomness. Producers MUST emit
22–128 ASCII characters from `[A-Za-z0-9_-]`. An ID MUST NOT encode a NID,
tenant, model, database key, or other sensitive metadata. It is a locator, not
an authorization credential.

**`LlmContextReceiptDto`**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `context_id` | string | Required | Opaque context identifier. |
| `version` | uint64 | Required | Committed version after the operation. Create/fork start at 1; append/reset/release increment exactly once. |
| `operation` | enum | Required | `create`, `append`, `fork`, `reset`, or `release`. |
| `state` | enum | Required | `active` for completion mutations; `released` for release. |
| `expires_at` | RFC 3339 timestamp | Optional | Effective idle expiry when bounded. |
| `parent_context_id` | string | Required for fork | Source context. |
| `parent_version` | uint64 | Required for fork | Immutable source version used by the fork. |

Unary success carries the receipt on `LlmCompleteActionResponse.context`.
Async acknowledgment does not commit context state; the receipt appears only in
the completed `system.task.status.result`. Streaming success carries the receipt
only on the terminal chunk. Stateless responses and non-terminal chunks MUST
omit it.

#### 7.6.2 Lifecycle actions

| Action | Request DTO | Success response |
|--------|-------------|------------------|
| `llm.context.status` | `LlmContextStatusRequestDto` with exactly one of `context_id` or `idempotency_key` | CapsFrame `anchor_ref = "nps:system:llm.context.status:response"`, `data[0] = LlmContextStatusDto` |
| `llm.context.release` | `LlmContextReleaseRequestDto` with `context_id` and `base_version`; ActionFrame MUST carry `idempotency_key` | CapsFrame `anchor_ref = "nps:system:llm.context.release:response"`, `data[0] = LlmContextReceiptDto` |

`LlmContextStatusDto` contains required `state` (`busy`, `active`, `released`,
`expired`, or `failed`) plus optional `context_id`, `version`, `expires_at`,
`request_id`, and `error_code`. Active/released/expired states MUST carry an ID
and version. An in-flight create reports `busy` but MUST omit its not-yet-
committed ID/version. A failed create reports `failed`, omits ID/version, and
carries its terminal error code. Status is observational and MUST NOT refresh
TTL.

Release increments vN to a released tombstone vN+1. It is replay-idempotent for
the same owner and ActionFrame key. Mutations against a released context return
`NWP-LLM-CONTEXT-NOT-FOUND`; the owner MAY still inspect its tombstone with
status during the advertised retention window.

#### 7.6.3 Binding and authorization

Create/reset binds the context to the resolved model ID, ordered system
messages, canonical tool definitions, and the provider/runtime compatibility
revision required for prefix reuse. Append/fork MUST preserve this binding.
Their deltas MUST NOT contain system-role messages; omitted `tools` reuses the
bound definitions, while present `tools` MUST canonically equal them. The
required `model` field MUST resolve to the bound model. A mismatch returns
`NWP-LLM-CONTEXT-BINDING-MISMATCH`.

Stateful `llm.complete` mutations require both `llm:complete` and
`llm:context`; streaming and tools additionally require `llm:stream` and
`llm:tool_call`, respectively. Status/release require `llm:context` plus owner
authorization but do not require the caller to retain model-invocation rights.
Stateful coordinator APIs MUST pass the complete required-capability set to the
deployment-owned NIP authorizer at every admission and commit check. A
coordinator with no authorizer installed MUST fail closed with
`NWP-LLM-CONTEXT-FORBIDDEN`; it MUST NOT treat an absent hook as authorization.
The owner is the authenticated NID plus the node's authenticated tenant/workspace
security scope. That scope comes from admitted identity and deployment policy,
never a client-controlled context field. Every operation MUST re-run normal NIP
expiry, revocation, assurance, scope, and capability checks. Long-running work
MUST repeat revocation/authorization checks before commit. An authenticated
non-owner receives `NWP-LLM-CONTEXT-FORBIDDEN`.

#### 7.6.4 Atomic state transitions

Create commits v1. Append/reset compare-and-swap `base_version` and commit
vN+1. Fork atomically snapshots the parent at admission, creates a child v1,
and never mutates the parent. A later parent mutation does not invalidate an
admitted fork snapshot. At most one mutation reservation may exist per context;
a stale or concurrent loser receives `NWP-LLM-CONTEXT-VERSION-CONFLICT` with
the current version in the ErrorFrame hint.

A completion mutation commits only after a successful terminal result whose
stop reason is not `error`. The commit includes both the request delta and the
terminal assistant text/tool calls. A structured refusal represented by an
ordinary `end_turn` MAY commit. Validation/auth/provider failure, timeout,
cancellation, `stop_reason = "error"`, or abnormal stream termination MUST
leave the prior transcript/version unchanged and release the reservation.

The server MUST atomically commit to the store promised by its NWM persistence
level before exposing the terminal receipt. A valid reservation prevents idle
expiry while work is running. Successful create/append/fork/reset starts or
refreshes TTL; status and failed/cancelled mutations do not. If the prior TTL
elapsed during work and the reservation aborts, the context transitions directly
to expired. Automatic expiry records the last committed version without
incrementing it.

#### 7.6.5 Reconnect, restart, and idempotency

The NWM persistence values mean:

- `connection`: survives requests only on the creating NCP connection;
- `process`: survives reconnect only when routed to the same process, not a
  process restart or another instance;
- `durable`: survives process restart under the same logical node NID and
  endpoint identity.

Connection/process contexts are node-instance scoped and clients SHOULD pin
them to the creating endpoint. Cross-instance migration/distributed context
stores are not defined here. State loss returns not-found/expired and MUST NOT
create a replacement.

The existing 24-hour ActionFrame idempotency window also retains the owner-
scoped key-to-outcome record, even when context TTL is shorter. A duplicate
while the first request runs returns `NWP-ACTION-IDEMPOTENCY-CONFLICT` and MUST
NOT join a live stream. Completed unary/async requests use the cached-result
rule. Completed streaming replay uses a new StreamFrame sequence with logically
identical ordered text, tool calls, stop reason, usage, and terminal receipt;
it MUST NOT regenerate or recommit. `llm.context.status` by idempotency key
resolves a lost create response. After 24 hours the key lookup MAY return
not-found.

#### 7.6.6 Expiry, limits, and errors

Released/expired tombstones SHOULD remain visible to their owner for at least
`tombstone_seconds`. Mutations against an expiry tombstone return
`NWP-LLM-CONTEXT-EXPIRED`; after tombstone removal they return not-found.

| Error | NPS status | Meaning |
|-------|------------|---------|
| `NWP-CHALLENGE-INVALID` | `NPS-CLIENT-BAD-FRAME` | Invalid challenge input |
| `NWP-CHALLENGE-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | Unknown or unauthorized challenge handle |
| `NWP-CHALLENGE-BINDING-CONFLICT` | `NPS-CLIENT-CONFLICT` | Same operation key with a different digest |
| `NWP-CHALLENGE-NOT-APPROVED` | `NPS-CLIENT-CONFLICT` | Challenge is not approved for execution |
| `NWP-CHALLENGE-EXPIRED` | `NPS-CLIENT-CONFLICT` | Challenge expired before execution claim |
| `NWP-CHALLENGE-STATE-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | Durable challenge state unavailable |
| `NWP-FEATURE-REQUIREMENTS-INVALID` | `NPS-CLIENT-BAD-FRAME` | Invalid or repeated required-feature HTTP header |
| `NWP-FEATURE-REQUIRED-UNSUPPORTED` | `NPS-CLIENT-CONFLICT` | Required exact feature unavailable before dispatch |
| `NWP-LLM-CONTEXT-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | Unknown/released context or expired idempotency lookup. |
| `NWP-LLM-CONTEXT-EXPIRED` | `NPS-CLIENT-GONE` | Idle-expiry tombstone is still present. |
| `NWP-LLM-CONTEXT-VERSION-CONFLICT` | `NPS-CLIENT-CONFLICT` | Stale version or concurrent mutation reservation. |
| `NWP-LLM-CONTEXT-BINDING-MISMATCH` | `NPS-CLIENT-CONFLICT` | Model/system/tools/runtime binding differs. |
| `NWP-LLM-CONTEXT-FORBIDDEN` | `NPS-AUTH-FORBIDDEN` | Caller is not owner or lacks scope/capability. |
| `NWP-LLM-CONTEXT-LIMIT-EXCEEDED` | `NPS-LIMIT-RESOURCE` | Per-principal live-context limit reached. |
| `NWP-LLM-CONTEXT-OPERATION-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | Node supports context but not this operation. |

Malformed field combinations use `NWP-ACTION-PARAMS-INVALID`. All failures in
this section use ErrorFrame or terminal stream error, never
`LlmCompleteActionResponse.error`.

#### 7.6.7 Conformance and benchmark claim

All six SDKs MUST execute `conformance/nwp/llm_context_vectors.json`. A benchmark
claiming stateful savings MUST compare the same multi-turn role/tool semantics
in stateless and strict-native stateful modes. On the second turn, stateful mode
MUST send only the delta and demonstrate both lower `wire_input_bytes` and lower
observed `evaluated_tokens`. Protocol fallback MUST be disabled.

---

### 7.7 Governed action challenge (NWP v0.23)

The following rules define experimental `nps.governed_challenge` revision 1,
requiring `nps.production_runtime` revision 1 and additionally context revision 1
when a projection is carried. These profiles do not replace product approval policy
or allocate new frames.

#### 7.7.1 Binding and initial action

A governed operation requires authenticated subject, logical audience/target, action ID and a
nonempty idempotency key (1..128 UTF-8 bytes). Existing ActionFrame carries the operation.
An opted-in server validates parameters and authorization before creating state. Missing key
rejects before creating challenge or effect.

Bind issuer authority, subject, action, audience, frozen accepted parameters, operation key, expiry
and selected feature revisions. The operation digest uses NIP §7.8 / NWP §9.7 logical request digest rules,
even when the context is native and no projection header exists. Challenge metadata and proof
handle are excluded from the original operation digest; original business parameters are immutable.

Durable operation key is (authority,subject,audience,action,idempotency_key). Same key and same
digest returns the existing pending/terminal operation. Same key and different digest conflicts
without creating a second challenge. Authorization is rechecked on every call, including results.
Store the initial operation and challenge atomically before returning a handle.

#### 7.7.2 Challenge response

The standard Caps response carries a profile-defined JSON record in data with its registered
Anchor schema; it is not arbitrary JSON substituted for a Caps payload. HTTP may use 202 for pending;
native uses the same Caps record. Fields:

| Field | Type / rule |
|---|---|
| challenge_id | opaque unpadded base64url of 32 random bytes |
| state | pending / approved / executing / completed / denied / expired / superseded / indeterminate |
| action_id | original registered action |
| audience | original logical target |
| request_digest | sha256: plus 64 lowercase hex digits |
| expires_at | integer Unix seconds; before execution admission |
| methods | unique product method identifiers, at most 8; each at most 64 ASCII bytes |
| operation_id | opaque server operation identifier, at most 128 bytes |
| result | only for completed; original action's complete typed Caps value |
| error | bounded original/action error or profile error; never approval credentials |

Subject/authority binding remains server-side; do not expose private policy/account details.
TTL defaults to 300 seconds, maximum 900, server clock authoritative. Persist result/deduplication
records for at least 86400 seconds after terminal transition and publish the exact retention period.
A client MUST NOT automatically retry after the advertised retention window.

#### 7.7.3 Protocol actions

Register nwp.challenge.status, nwp.challenge.resume and nwp.challenge.cancel using existing NWP
action discovery, ActionFrame and Caps responses. Params contain only challenge_id; original frozen
operation is loaded from state. No reusable OTP, approval token or user password travels in these
actions. Each action requires the same authenticated subject/audience and product permission.

status returns current state without effects. resume requires approved state, rechecks current
grant/revocation/scope, original digest and expiry, then atomically claims approved -> executing.
Repeated resume returns the same operation state/result; it never creates another execution.
cancel transitions pending/approved -> denied atomically. Cancel after executing reports current
state and cannot claim rollback. Unknown or wrong-subject handles return the same bounded not-found
error. Handle knowledge does not let another subject view, approve, resume or cancel.

Human/step-up systems update pending -> approved or denied via product-owned authenticated flows.
They cannot modify frozen parameters or rebind the operation. Supersession ends the old challenge;
a new logical operation/key is required for different parameters. Direct execute bypass is not
allowed merely because a client omitted this profile on a policy-governed action.

#### 7.7.4 State transitions and execution ownership

Allowed transitions:

- pending -> approved / denied / expired / superseded;
- approved -> executing / denied / expired / superseded;
- executing -> completed / indeterminate.

Completed includes a durable terminal action result or known terminal action failure. A pending
approval does not itself start execution. Expiry applies before executing; it does not undo an
already executing side effect. Terminal state is immutable except an explicit trusted reconciliation
process may resolve indeterminate -> completed with durable execution evidence. Reconciliation
never automatically repeats the mutation.

Claim ownership and durable operation record must commit atomically. If the effect shares the same
transactional store, commit effect and result together. Otherwise use an idempotent external sink
with the same durable operation identity, or retain indeterminate after an ambiguous crash.
At-most-one claim is not proof of at-most-one external effect. Concurrent callers, restart and
Anchor failover share the operation store/fencing scope; unavailable state fails closed.

#### 7.7.5 Errors and limits

| Error | Status | HTTP |
|---|---|---|
| NWP-CHALLENGE-INVALID | NPS-CLIENT-BAD-FRAME | 400 |
| NWP-CHALLENGE-NOT-FOUND | NPS-CLIENT-NOT-FOUND | 404 |
| NWP-CHALLENGE-BINDING-CONFLICT | NPS-CLIENT-CONFLICT | 409 |
| NWP-CHALLENGE-NOT-APPROVED | NPS-CLIENT-CONFLICT | 409 |
| NWP-CHALLENGE-EXPIRED | NPS-CLIENT-CONFLICT | 409 |
| NWP-CHALLENGE-STATE-UNAVAILABLE | NPS-SERVER-UNAVAILABLE | 503 |

Bound active pending challenges per subject and global storage; defaults 16 per subject and 4096
total pending/approved challenges. Capacity rejection occurs before mutation/state allocation.
Rate-limit status/resume/cancel with existing node policy. Logs contain bounded operation/state
identifiers, not approval proofs, projection headers or business parameters.

#### 7.7.6 Schema, retention and response boundaries

Here authority is a receiver-configured logical operation-owner namespace, stable
across its replicas and gateway routes; it is not the immediate projection issuer/key.
Changing ingress gateways MUST NOT create a second deduplication namespace.
Only profile-generated challenge metadata is excluded from the original digest;
business params named challenge_id or similar remain part of the signed body.

Management actions MUST reject callback_url and async=true. Their timeout/priority
only govern the management request and MUST NOT replace frozen operation fields;
their idempotency key cannot replace the stored original operation key.
Execution claim MUST reject when server now >= expires_at. Initial expiry is strictly
after creation and at most 900 seconds later.

The existing ActionFrame UUID-v4 idempotency-key rule still applies; the 1..128-byte
limit does not relax it. NWM advertises the durable retention through
`profiles.governed_challenge = {"revision":1,"retention_seconds":86400}`.
Retention is uint32, at least 86400; changing it changes manifest version.
Finite local storage MUST reject new operations rather than evict records before
their promised retention ends.

Fixed Anchors are defined in [challenge-schemas.json](conformance/alpha20/challenge-schemas.json).
The three management actions have params containing only challenge_id. Responses are
standard Caps with count=1 and a complete challenge record in data[0]. Unused result
and error are explicitly null. Non-null result is the original operation's standard
Caps object, not an arbitrary replacement envelope. A known completed success requires
result and null error; a known completed failure requires error and null result.
All other states require null result. Existing frame/response limits still apply.

The record additionally carries `retained_until` (nullable uint64 Unix seconds):
terminal records MUST set it to at least terminal commit time plus the promised
retention; other states set null. Trusted reconciliation MUST NOT shorten a promised
deadline. All time values are at most 9007199254740991. Method IDs match
`[a-z][a-z0-9_.:-]{0,63}`; operation_id is 1..128 UTF-8 bytes.
Action/audience/digest follow §9.7 and NIP §7.8 bounds.

Initial governed invocation only creates/returns pending or approved state; it does
not execute the original mutation. Status returns the current record. Resume in
pending/denied/superseded returns NOT-APPROVED; expired returns EXPIRED;
executing/completed/indeterminate returns the same state/result. These rejections
use the corresponding NWP-CHALLENGE-* errors above. Unknown/wrong-subject remains
uniform NOT-FOUND. HTTP pending/approved/executing records use 202; other records use
200; errors use the error table. Outcome certainty comes from state/result/error,
never merely an HTTP success status.

---

## 8. SubscribeFrame Overview (0x12)

Used to establish change subscriptions on Memory and Anchor Nodes. The authoritative v0.13 wire shape is defined in §13 (CR-0006): `subscription_id`, `filter`, `heartbeat_interval_ms`, `max_events`, and opaque `cursor`.

Earlier alpha drafts used `action`, `stream_id`, `heartbeat_interval`, and `resume_from_seq`. Those names are retired for NWP v0.13 and MUST NOT be emitted by conformant alpha.11+ producers. Consumers MAY accept them only as a pre-alpha.11 compatibility fallback, but MUST normalize internally to the §13 fields.

### 8.1 Subscription in HTTP Mode (SSE)

```
POST /nwp/orders/subscribe HTTP/1.1
Content-Type: application/nwp-frame

[SubscribeFrame bytes]

HTTP/1.1 200 OK
Content-Type: text/event-stream

data: [DiffFrame bytes, base64]

data: [DiffFrame bytes, base64]
```

---

## 9. HTTP Headers (HTTP Mode)

### 9.1 Request Headers

| Header | Required | Description |
|--------|----------|-------------|
| `X-NWP-Agent` | Conditionally Required | Agent NID (required when auth.required=true) |
| `Authorization` | Conditionally Required | HTTP bearer credential when `auth.identity_type = "bearer"`. Not used in native mode; native authentication is session-bound via NCP/NIP IdentFrame. |
| `X-NWP-Budget` | Optional | CGN budget limit (uint32) |
| `X-NWP-Tokenizer` | Optional | Tokenizer identifier used by the Agent |
| `X-NWP-Depth` | Optional | Node graph traversal depth, default 1, max 5 |
| `X-NWP-Encoding` | Optional | Request encoding tier: `json`/`msgpack`, default `msgpack` |
| `X-NWP-Request-ID` | Optional | UUID v4 request tracing ID; echoed back by the node in the response header |
| `If-None-Match` | Optional | NWM conditional request; value is `manifest_version` |
| `Content-Type` | Required | `application/nwp-frame` |

### 9.2 Response Headers

| Header | Description |
|--------|-------------|
| `X-NWP-Schema` | anchor_id used in the response |
| `X-NWP-Tokens` | Actual CGN consumed |
| `X-NWP-Tokens-Native` | Native token consumption |
| `X-NWP-Tokenizer-Used` | Tokenizer actually used |
| `X-NWP-Cached` | `true` indicates a cache hit |
| `X-NWP-Node-Type` | Node type |
| `X-NWP-Request-ID` | Echo of the requester's `X-NWP-Request-ID` (node MAY auto-generate if not provided) |
| `X-NWP-Rate-Limit` | Max requests per minute |
| `X-NWP-Rate-Remaining` | Remaining requests this minute |
| `X-NWP-Rate-Reset` | Rate limit window reset time (Unix timestamp) |
| `Content-Type` | `application/nwp-capsule` (normal response) / `application/nwp-error+json` (error response) |

### 9.3 Native Mode Field Mapping

| HTTP Header | QueryFrame Field | ActionFrame Field |
|-------------|-----------------|-------------------|
| `X-NWP-Agent` | — (declared as HelloFrame `agent_id` hint; authenticated identity is the session-bound NIP IdentFrame) | Same |
| `X-NWP-Budget` | `token_budget` | — |
| `X-NWP-Tokenizer` | `tokenizer` | — |
| `X-NWP-Depth` | `depth` | — |
| `X-NWP-Request-ID` | `request_id` | `request_id` |

### 9.4 HTTP Mode Error Response Format

In HTTP mode, error responses use the following format, `Content-Type: application/nwp-error+json`:

```json
{
  "status": "NPS-CLIENT-NOT-FOUND",
  "error": "NWP-ACTION-NOT-FOUND",
  "message": "Action 'orders.ship' is not registered on this node",
  "details": { "action_id": "orders.ship" },
  "request_id": "550e8400-e29b-41d4-a716-446655440003"
}
```

The HTTP status code is determined by the NPS status code mapping; see [status-codes.md](status-codes.md).

**Field Descriptions**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `status` | string | Required | NPS status code |
| `error` | string | Required | Protocol-level error code (e.g. `NWP-ACTION-NOT-FOUND`) |
| `message` | string | Optional | Human-readable description |
| `details` | object | Optional | Structured additional error information |
| `request_id` | string | Optional | Echo of the `X-NWP-Request-ID` from the request |

### 9.5 HTTP Binding Rejection Codes

HTTP overlay implementations MUST use canonical NWP error codes for transport-binding preconditions before the request body is admitted as a NWP frame. These errors are NWP-owned because they determine whether an NWP `QueryFrame`, `ActionFrame`, or `SubscribeFrame` can be recovered from the HTTP exchange; they are not implementation-local adapter errors.

| Rejection | Trigger | Error Code | NPS Status Code |
|-----------|---------|------------|-----------------|
| Origin disallowed | Browser-facing HTTP binding rejects the request origin by CORS or equivalent origin policy | `NWP-HTTP-ORIGIN-FORBIDDEN` | `NPS-AUTH-FORBIDDEN` |
| Content-Type unsupported | Request body media type is not a supported NWP frame media type | `NWP-HTTP-CONTENT-TYPE-UNSUPPORTED` | `NPS-CLIENT-BAD-FRAME` |
| Accept unsatisfiable | `Accept` refuses every response media type this node can emit | `NWP-HTTP-ACCEPT-UNSATISFIABLE` | `NPS-CLIENT-BAD-PARAM` |
| Request-id echo mismatch | Client observes that the response `X-NWP-Request-ID` does not match the request header | `NWP-HTTP-REQUEST-ID-MISMATCH` | `NPS-CLIENT-BAD-PARAM` |
| Unparseable frame body | HTTP body cannot be parsed as any supported NWP frame envelope or NCP-carried NWP frame | `NWP-HTTP-FRAME-BODY-MALFORMED` | `NPS-CLIENT-BAD-FRAME` |
| Body too large | Request body exceeds the server's advertised or configured NWP body limit | `NWP-HTTP-BODY-TOO-LARGE` | `NPS-LIMIT-PAYLOAD` |
| Advertised but unimplemented | NWM advertises a capability or profile that this node accepts in discovery but cannot currently serve | `NWP-CAPABILITY-ADVERTISED-UNIMPLEMENTED` | `NPS-SERVER-UNSUPPORTED` |

`NWP-CAPABILITY-ADVERTISED-UNIMPLEMENTED` is distinct from capability-specific unsupported codes such as `NWP-QUERY-VECTOR-UNSUPPORTED`: use the specific code when the manifest truthfully declares the feature unsupported, and use `NWP-CAPABILITY-ADVERTISED-UNIMPLEMENTED` only for rollout windows, disabled backends, or inconsistent discovery state where the feature was advertised but cannot be served.

---

### 9.6 Feature-dependent HTTP admission (NWP v0.23)

Feature IDs, revision descriptors and lifecycle policy follow NCP §2.6.3.
Every required dependency in feature-registry.json must also be explicitly listed
in the request's exact pair set; a missing/incompatible dependency fails with
NWP-FEATURE-REQUIRED-UNSUPPORTED before dispatch.
The following requirements are opt-in, but malformed feature headers MUST be
rejected by a profile-aware endpoint even on a base operation.

**NWP-FTR-01 — advertisement.** NWM MAY expose
`profiles.feature_negotiation = {"version":1,"feature_offer":[...]}`.
Its raw JSON profile value MUST NOT exceed 16384 UTF-8 bytes; existing manifest
limits also apply. The manifest version MUST change when the offer changes.
The advertisement covers the service endpoint; narrower action support MUST be
revalidated before side effects. Discovery is not admission or authorization.

**NWP-FTR-02 — exact header.** Feature-dependent requests MUST carry one ASCII
`X-NPS-Required-Features` header: comma-separated `id=revision` pairs, sorted by
ascending ID, without whitespace, duplicates or leading zeroes; at most 32 pairs
and 4096 bytes. Repeated header instances MUST be rejected before framework joining.
All listed revisions are exact and required for that operation. Invalid syntax
maps to `NWP-FEATURE-REQUIREMENTS-INVALID` / HTTP 400.

**NWP-FTR-03 — dispatch gate.** Before invoking a handler, the endpoint MUST check
current enabled revisions, dependencies, operation support and authenticated
admission policy. Unsupported/withdrawn support fails with
`NWP-FEATURE-REQUIRED-UNSUPPORTED` / HTTP 409 and zero handler invocations.
Feature claims MUST NOT expand admitted authority. Gateways revalidate each leg;
native and HTTP selections are independent. Context projection request digests
MUST bind the exact required feature set.

**NWP-FTR-04 — route guarantee.** Before sending a feature-dependent operation,
clients MUST obtain affirmative version-1 advertisement on the authenticated route
serving that operation. That route MUST enforce admission for every possible backend,
or reject before forwarding; mixed old/new routes MUST NOT advertise until this is
guaranteed. Clients MUST fail locally if the guarantee is unavailable, and MUST NOT
redirect/fall back to an unverified route. An old server ignoring the header is not
a safe fallback; post-response detection is too late to prevent side effects.

**NWP-FTR-05 — response.** Successful responses MUST echo the exact accepted pairs
in `X-NPS-Selected-Features`; failed responses MUST NOT assert selection success.
A missing/malformed/mismatched success echo is a client protocol error with uncertain
operation outcome, never permission to retry a mutation. Calls not using the profile
omit the required header and retain base behavior.

---

### 9.7 Invocation-context carrier and request binding

**NWP-CTX-01 — native carrier.** QueryFrame, ActionFrame and SubscribeFrame MAY
carry `invocation_context`, an unpadded base64url string containing the NIP §7.8
JSON envelope, plus `required_features`, an array of exact
`{"id":"nps.invocation_context","revision":1}`-shaped pairs. These request pairs
are distinct from Hello's required-ID array. They MUST be unique, sorted by ID and
at most 32; every exact pair MUST already belong to the admitted session selection.
A projection requires the context revision-1 pair. Feature-aware receivers MUST
reject unnegotiated requirements before dispatch; clients MUST NOT send them to
legacy sessions. No other frame receives a context carrier through this binding.

**NWP-CTX-02 — HTTP carrier.** Use the single `X-NPS-Invocation-Context` header
and exact `X-NPS-Required-Features` set of §9.6, including context revision 1.
The native-only `invocation_context` and `required_features` body fields MUST NOT
also appear on a projected HTTP request; multiple carriers are invalid. A gateway
translates carriers explicitly, not by preserving both. The encoded context string
is at most 21846 ASCII bytes, and the decoded envelope at most 16384 bytes;
non-canonical base64url and duplicate instances are invalid.

**NWP-CTX-03 — logical request.** For this binding, digest `protocol` is `nwp`;
ActionFrame uses its exact `action_id` as `operation`, QueryFrame uses
`nwp.query`, and SubscribeFrame uses `nwp.subscribe`. `target` is the exact
logical service/resource ID established by the authenticated route, checked against
receiver configuration, never copied from an unverified caller authority header.
`idempotency_key` is the actual request field or null, preserving its schema rules.
`required_features` is exactly the request pair set from the applicable carrier.

**NWP-CTX-04 — complete semantic body.** For JSON-compatible requests,
`body.value` is the parsed NWP request object with `frame`, `request_id`,
`invocation_context` and `required_features` removed. For ActionFrame only,
also remove `action_id` and `idempotency_key`, which are bound in the outer object.
All remaining fields are retained, including params, filters, query type,
callback_url, priority, async, timeout and subscription options when present.
Preserve field presence; do not insert defaults or normalize values before digest
verification. HTTP path/query/header semantics must first be mapped into this logical
request using the registered binding. Headers not represented in that binding MUST
NOT affect authority or side effects. Non-I-JSON values require a registered byte-body
profile; otherwise reject rather than invent a cross-language conversion.
Transport trace correlation and the projection are excluded, not business inputs.
The verified I-JSON value MUST be the value delivered to the handler, not a second
higher-precision parse of original numeric lexemes. Numeric semantics are finite
IEEE-754 binary64; integers outside ±9007199254740991 or schema requirements for
higher-precision decimals must use declared string/byte representations. A verifier
MUST NOT sign a rounded value while the handler executes a different precise value.
Excluded correlation fields MUST NOT drive authorization, operation selection,
business deduplication or business decisions; protocol correlation echo is still allowed.

**NWP-CTX-05 — authority and dispatch.** NIP §7.8 verification and atomic replay
admission follow pure operation-schema/binding validation. Invalid operation inputs
MUST NOT consume a projection ID; that validation cannot produce business effects.
Verification and replay
admission MUST finish before any operation handler or operation-driven callback,
external probe or business side effect. Verification/replay-store operations themselves
are admission work, not operation dispatch. Public/untrusted projection carriers are stripped at the ingress boundary;
fresh projection uses locally admitted authority. The immediate gateway identity and
original subject remain distinct. A caller cannot choose trusted issuer policy,
local-dev mode, capabilities or scope by filling fields. Projection/feature support
MUST NOT be advertised until enforcement exists on all serving routes. Re-projection
after a changed hop/request does not authorize retry of an uncertain mutation.

---

## 10. Complete Request/Response Example

**HTTP Mode Query Request**

```
POST /nwp/orders/query HTTP/1.1
Host: api.example.com:17433
X-NWP-Agent: urn:nps:agent:ca.innolotus.com:550e8400
X-NWP-Budget: 1200
X-NWP-Tokenizer: cl100k_base
X-NWP-Request-ID: 550e8400-e29b-41d4-a716-446655440001
Content-Type: application/nwp-frame

[QueryFrame: { anchor_ref, filter, fields, limit, auto_anchor }]
```

**Success Response**

```
HTTP/1.1 200 OK
X-NWP-Schema: sha256:a3f9...
X-NWP-Tokens: 380
X-NWP-Request-ID: 550e8400-e29b-41d4-a716-446655440001
X-NWP-Rate-Limit: 300
X-NWP-Rate-Remaining: 248
Content-Type: application/nwp-capsule

[CapsFrame]
```

**Error Response**

```
HTTP/1.1 404 Not Found
X-NWP-Request-ID: 550e8400-e29b-41d4-a716-446655440001
Content-Type: application/nwp-error+json

{ "status": "NPS-CLIENT-NOT-FOUND", "error": "NWP-QUERY-FIELD-UNKNOWN", ... }
```

---

## 11. Complex Node — Node Graph

Complex Nodes declare sub-node references in the NWM:

```json
{
  "graph": {
    "refs": [
      { "rel": "user",    "node": "nwp://api.myapp.com/users" },
      { "rel": "payment", "node": "nwp://pay.myapp.com/transactions" }
    ],
    "max_depth": 2
  }
}
```

Agents control traversal depth via the `X-NWP-Depth` header (HTTP mode) or the QueryFrame `depth` field (native mode). Nodes MUST detect circular references (return `NWP-GRAPH-CYCLE`) and maintain a sub-node URL whitelist (SSRF prevention).

---

## 12. Reserved Query Types

The `type` field on `QueryFrame` (§6.1) and `SubscribeFrame` (§13.1) opts a request into a **reserved query type** with specification-defined semantics. Identifiers in the `topology.*` namespace are reserved for cluster topology operations on Anchor Nodes; all reserved namespaces below are **mandatory** for the node roles indicated.

| Namespace | Owning role | Mandatory at | Operations |
|-----------|-------------|--------------|------------|
| `topology.*` | Anchor Node (§2.1) | NPS-AaaS Profile L2 ([services/NPS-AaaS-Profile.md §4.3](services/NPS-AaaS-Profile.md)) | `topology.snapshot` (§12.1), `topology.stream` (§12.2) |

When `type` is absent, default per-anchor query/subscribe semantics apply (§6, §8). When `type` is set, the per-type fields documented below apply and any conflicting standard fields (e.g. `anchor_ref`, top-level `filter`) are ignored unless the per-type schema explicitly carries them.

Implementations that do not recognize a reserved `type` value MUST reject the request with `NWP-RESERVED-TYPE-UNSUPPORTED` (§13) so the caller can distinguish "unknown reserved operation" from "action_id not found" (`NWP-ACTION-NOT-FOUND`) and fall back or fail explicitly.

### 12.1 `topology.snapshot`

Single-shot retrieval of an Anchor Node's cluster topology.

| Property | Value |
|----------|-------|
| Frame | QueryFrame (0x10) with `type = "topology.snapshot"` |
| Required of | All Anchor Nodes (mandatory at NPS-AaaS Profile L2 and above) |
| Idempotent | Yes |
| Caching | Responses MAY be cached client-side; the `version` field correlates the snapshot with subsequent `topology.stream` events |

**Request fields** (top-level on QueryFrame, supplementing §6.1):

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `type` | string | Required | Constant `"topology.snapshot"` |
| `topology` | object | Required | Container for topology-specific parameters per below |
| `topology.scope` | string | Required | `"cluster"` for the Anchor's own cluster; `"member"` for a single member (requires `topology.target_nid`) |
| `topology.include` | array of strings | Optional | Subset of `["members", "capabilities", "tags", "metrics"]`. Default `["members"]`. The `capabilities` and `metrics` schemas are implementation-defined and MAY be empty |
| `topology.depth` | uint8 | Optional | For sub-Anchor members, controls recursion. `1` (default) lists sub-Anchors as references only; `2+` recurses. Anchor Nodes MAY cap depth and set `truncated: true` when exceeded. Sub-Anchor recursion at depth ≥ 2 is **OPTIONAL** at L2; clients SHOULD recurse manually by issuing one snapshot per sub-Anchor |
| `topology.target_nid` | string | Conditionally Required | Required when `topology.scope = "member"`; the NID of the member to introspect |

**Response**: `CapsFrame (0x04)` with `anchor_ref = "nps:system:topology:snapshot"` and a single-element `data` array carrying the snapshot payload below.

```json
{
  "frame": "0x04",
  "anchor_ref": "nps:system:topology:snapshot",
  "count": 1,
  "data": [{
    "version": 142,
    "anchor_nid": "urn:nps:node:labacacia:host-abc123",
    "cluster_size": 23,
    "members": [
      {
        "nid": "urn:nps:agent:labacacia:host-abc123-sess-aaa",
        "node_roles": ["memory"],
        "activation_mode": "ephemeral",
        "tags": ["dev", "library"],
        "joined_at": "2026-04-15T10:23:00Z",
        "last_seen": "2026-04-26T14:55:00Z"
      },
      {
        "nid": "urn:nps:node:labacacia:host-def456",
        "node_roles": ["anchor"],
        "activation_mode": "resident",
        "child_anchor": true,
        "member_count": 7,
        "tags": ["sub-cluster", "training"]
      }
    ],
    "truncated": false
  }]
}
```

**Snapshot payload fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `version` | uint64 | Required | Monotonically increasing topology version. Resets only on Anchor restart / rebase (§12.3) |
| `anchor_nid` | string | Required | NID of the responding Anchor Node |
| `cluster_size` | uint32 | Required | Total direct members, regardless of `depth` truncation |
| `members` | array of member objects | Required | Per the member object schema below |
| `truncated` | bool | Optional | True iff `topology.depth` cap was hit; otherwise omitted or false |

**Member object schema**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `nid` | string | Required | Member NID |
| `node_roles` | array of strings | Required | NDP `node_roles` — the full role set declared by this member (NPS-4 §3.1) |
| `activation_mode` | string | Required | NDP `activation_mode` — one of `ephemeral` / `resident` / `hybrid` (NPS-4) |
| `child_anchor` | bool | Optional | True if this member is itself an Anchor Node of a sub-cluster; implies `member_count` |
| `member_count` | uint32 | Conditionally Required | Required when `child_anchor = true`; count of the sub-Anchor's direct members |
| `tags` | array of strings | Optional | NDP-declared tags |
| `joined_at` | string | Optional | RFC 3339 timestamp; first observed |
| `last_seen` | string | Optional | RFC 3339 timestamp; most recent NDP `Announce` |
| `capabilities` | object | Optional | Returned only if requested via `topology.include`; schema implementation-defined |
| `metrics` | object | Optional | Returned only if requested via `topology.include`; schema implementation-defined |

### 12.2 `topology.stream`

Continuous topology change feed for an Anchor Node's cluster.

| Property | Value |
|----------|-------|
| Frame | SubscribeFrame (0x12) with `type = "topology.stream"` |
| Required of | All Anchor Nodes (mandatory at NPS-AaaS Profile L2 and above) |
| Cancelable | Yes — by closing the subscription transport or receiving/sending a terminal ErrorFrame |

**Request fields** (top-level on SubscribeFrame, supplementing §13.1):

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `type` | string | Required | Constant `"topology.stream"` |
| `subscription_id` | string | Required | Client-generated UUID v4 used to correlate the topology stream and pushed DiffFrames |
| `cursor` | string | Optional | Opaque server-issued resume cursor from a previous subscription. When present, `cursor` takes precedence over `topology.since_version` |
| `topology` | object | Required | Container for topology-specific parameters per below |
| `topology.scope` | string | Required | `"cluster"` (default for Anchor's own cluster); future scopes reserved |
| `topology.filter` | object | Optional | Reduces event volume. Supported keys: `tags_any` (array, match-any), `tags_all` (array, match-all), `node_roles` (array — filter by role, matches members whose `node_roles` intersects the given values). Anchor Nodes MUST reject unsupported filter keys with `NWP-TOPOLOGY-FILTER-UNSUPPORTED` |
| `topology.since_version` | uint64 | Optional | Topology-specific initial resume hint for clients that have a snapshot `version` but do not yet have an opaque `cursor`. Anchor Node MUST replay missed events when feasible; if the version is outside the retention window, MUST emit a `resync_required` event and the client MUST issue a fresh `topology.snapshot` |

For `type = "topology.stream"`, `cursor` is the canonical v0.13 resume mechanism. `topology.since_version` is accepted only as a topology-specific bootstrap hint when no cursor is available; when both fields are present, `cursor` takes precedence.

**Events** are pushed as `DiffFrame (0x02)` with the §13.2 subscription event envelope. For `topology.stream` subscriptions, `subscription_id` identifies the stream, `event_type` carries one of the topology event types below (extending the default `"create" / "update" / "delete"` enum), `seq` is the post-event topology version (§12.3), and `payload` carries the event-specific data.

| `event_type` | Trigger | `payload` shape |
|--------------|---------|-----------------|
| `member_joined` | NDP `Announce` from a node naming this Anchor as `cluster_anchor` | Full member object (§12.1) |
| `member_left` | Member explicitly leaves OR exceeds NDP liveness TTL | `{ "nid": "urn:nps:..." }` |
| `member_updated` | Existing member's metadata changes (tags, activation_mode, capabilities, etc.) | `{ "nid": "urn:nps:...", "changes": { "<field>": <new value>, ... } }` — field-level diff only; reassembly is the client's responsibility |
| `anchor_state` | Anchor cluster status change relevant to subscribers. Payload carries a discriminator `field` selecting one of the sub-types below | `{ "field": "<sub-type>", "details": { ... } }` |
| `resync_required` | Subscriber MUST tear down its local view and issue a fresh `topology.snapshot` followed by a new `topology.stream` subscription. Triggers: (a) `topology.since_version` is no longer replayable; (b) any `anchor_state` sub-type that invalidates the prior version counter (e.g. `version_rebased`); (c) server-side state loss requiring re-subscription | `{ "reason": "<version_too_old | anchor_rebased | server_state_lost>" }`. This event MAY omit `seq` and the subscriber MUST issue a fresh `topology.snapshot` |

**`anchor_state` sub-types** (selected by the payload `field` discriminator):

| `field` | Phase | Trigger | `details` shape |
|---------|-------|---------|-----------------|
| `version_rebased` | Phase 1–2 | Anchor restarted and reset its monotonic `version` counter (§12.3). Subscribers MUST treat as equivalent to `resync_required` | `{ "previous_version": <uint64>, "new_version": <uint64> }` |
| `anchor_failover` | Finalised (NPS-CR-0009) | Active Anchor handed cluster ownership to a peer (multi-Anchor HA, AaaS L3). Emitted at each ownership transfer; the fenced prior leader sends a terminal `anchor_failover` then closes its streams | `{ "successor_nid": "urn:nps:...", "cluster_epoch": <uint64>, "reason": "planned" \| "active_lost" }` |
| `anchor_quorum_lost` | Finalised (NPS-CR-0009) | Anchor cluster cannot maintain the ownership quorum; cluster is read-only (degraded). Anchors reject topology writes with `NWP-ANCHOR-NOT-LEADER` and mark `health: "degraded"` (NDP §3.2) | `{ "quorum_size": <uint32>, "available": <uint32> }` |

**Cluster ownership fence (NPS-CR-0009).** In a multi-Anchor cluster at most one Anchor is the active owner at any instant, identified by a monotonically increasing `cluster_epoch` (uint64, starts at 1). Every `topology.snapshot` / `topology.stream` response and every topology-mutating write carries the current `cluster_epoch`. A standby (or read-only-degraded) Anchor MUST reject topology writes with `NWP-ANCHOR-NOT-LEADER` (→ `NPS-CLIENT-CONFLICT`); an active Anchor MUST reject any inbound frame bearing a **higher** `cluster_epoch` with `NWP-ANCHOR-EPOCH-FENCED` (fencing a superseded leader). Single-Anchor clusters keep `cluster_epoch = 1` and never emit `anchor_failover` / `anchor_quorum_lost`. See [NPS-CR-0009](cr/NPS-CR-0009-multi-anchor-ha.md).

Implementations MUST treat unknown `anchor_state.field` values as forward-compatible and ignore them rather than tearing down the subscription, so future Phase 3 sub-types can be introduced without a wire break.

Standard SubscribeFrame heartbeats (§13.2) operate unchanged. Cancellation is transport-level for v0.13: either side MAY close the subscription stream after emitting a terminal ErrorFrame when an error reason is available.

### 12.3 Versioning and Consistency Model

**Guaranteed**:

- A `topology.snapshot` returned at `version: V` reflects the cluster state after exactly `V` topology mutations.
- A `topology.stream` event with `seq: V` reflects the cluster state after exactly `V` mutations.
- A snapshot at `V` combined with all subsequent stream events `V+1, V+2, …` yields a consistent live view.

**Not guaranteed**:

- Real-time delivery latency — events MAY batch.
- Event ordering across multiple Anchor Nodes — each Anchor maintains its own `version` counter.
- Total ordering with non-topology events (Action / Query traffic on member nodes).

**Restart and rebase**: An Anchor Node MAY rebase its `version` counter on restart. When rebasing, the Anchor MUST emit an `anchor_state` event with `field: "version_rebased"` to all active subscribers; subscribers MUST treat this as equivalent to `resync_required` and issue a fresh `topology.snapshot`.

### 12.4 Out of Scope

- **Capability and metric schema standardization**: §12.1's `capabilities` and `metrics` field schemas are implementation-defined; standardization is deferred to a follow-up CR once enough implementations exist.
- **Cross-cluster federation queries**: Querying topology across multiple Anchor Nodes is an NPS-AaaS Profile L3 / NPS Cloud concern. This section is single-Anchor only.
- **Authorization model — minimum binding (Phase 1–2)**: Anchor Nodes MUST enforce the following minimum before serving any `topology.*` request:

  1. **Capability gate (per surface)**: Phase 1–2 distinguishes two authorization surfaces:
     - `topology.snapshot` (single-shot pull, §12.1): the requesting NID MUST declare `topology:read` in `IdentFrame.capabilities` (NPS-3 §5.1); absent capability MUST produce `NWP-TOPOLOGY-UNAUTHORIZED`.
     - `topology.stream` (long-lived subscription, §12.2): the requester MUST declare `topology:read` AND `topology:subscribe` in `IdentFrame.capabilities`. Phase 2 Anchor Nodes MUST enforce the `topology:subscribe` capability (treating its absence as an authorization failure) so subscription privilege is separable from snapshot read; Anchor Nodes that do not yet enforce `topology:subscribe` MUST at minimum enforce `topology:read`. Nodes that cannot enforce `topology:subscribe` MUST document non-enforcement explicitly in the NWM `stability` metadata.

     The IdentFrame is signed by the requester's private key, so the claim is integrity-protected but self-declared — it is not CA-attested at Phase 1–2.
  2. **NDP role cross-check (defense-in-depth)**: The Anchor SHOULD additionally verify that the requester's last received `AnnounceFrame` (within TTL) declares `node_roles` containing `"anchor"`. A mismatch SHOULD produce `NWP-TOPOLOGY-UNAUTHORIZED` with a `hint`. An absent `AnnounceFrame` MUST NOT block a requester that has passed the capability gate.
  3. **Mid-stream rejection (subscriptions only)**: For an established `topology.stream` subscription, if the Anchor revokes the requester's capability set (e.g. RevokeFrame received from the CA, NID expiry, or scope narrowing) the server MUST emit a terminal `NWP-TOPOLOGY-UNAUTHORIZED` event on the stream and then close the stream. The event carries the standard DiffFrame envelope with `event_type = "error"` and a payload of `{ "code": "NWP-TOPOLOGY-UNAUTHORIZED", "reason": "<revoked | expired | scope_narrowed>" }`. Anchor Nodes MUST NOT silently drop subscribers — a clean rejection event is required so clients can distinguish authorization loss from transport-level disconnects.
  4. **Reputation interaction (NPS-RFC-0004 `reputation_policy`)**: When the receiving Anchor declares a `reputation_policy` (NWM Phase 2 field, see [NPS-RFC-0004 §4.4](rfcs/NPS-RFC-0004-nid-reputation-log.md)) and the requesting NID's reputation score drops below a configured threshold while a `topology.stream` subscription is active, the Anchor SHOULD emit a terminal event with `payload.code = "NWP-AUTH-REPUTATION-BLOCKED"` carrying the matching `incident`, `severity`, and ledger entry `seq` (per error code §14), then close the stream. For initial-handshake reputation rejection (request time), the standard synchronous `NWP-AUTH-REPUTATION-BLOCKED` error code applies and the subscription is never opened. Anchors without a `reputation_policy` declared have no obligation to evaluate reputation.
  5. **Phase 3 [RFC-0002 stable]**: Anchors SHOULD additionally verify a CA-attested `id-nps-node-roles` cert extension (to be defined in a follow-up RFC-0002 amendment) to close the self-declaration gap and bind the role claim to a CA-issued certificate.

  Fine-grained per-cluster namespace or ACL policies remain implementation-defined and are tracked for a follow-up CR.
- **Browser transport (WebSocket)**: Whether `npsd` exposes a WebSocket endpoint for browser clients is tracked separately. Topology query semantics here are transport-independent.

---

## 13. SubscribeFrame (0x12) — Formal Specification

SubscribeFrame opens a server-push subscription on a Memory or Anchor Node. The server streams matching events as DiffFrame messages until the subscription is closed.

### 13.1 Request fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `frame` | uint8 | Required | Fixed value `0x12` |
| `subscription_id` | string | Required | Client-generated UUID v4; used to correlate events and cancel the subscription |
| `operation` | string | Optional | `open` (default), `renew`, or `close`. `renew` and `close` address an existing `subscription_id` and MUST omit target/filter fields. |
| `type` | string | Optional | Reserved subscribe type identifier per §12. When set, type-specific fields apply and `anchor_ref` semantics are defined by the type. Absent: per-anchor subscribe behavior below |
| `anchor_ref` | string | Conditionally Required | anchor_id of the subscribed data. Required for default per-anchor subscriptions; omitted when a reserved `type` defines its own target semantics (for example `topology.stream`) |
| `filter` | object | Optional | Same filter syntax as QueryFrame `filter` (§6); if absent, all events match |
| `heartbeat_interval_ms` | uint32 | Optional | If set, server MUST emit a heartbeat DiffFrame (empty payload, `event_type = "heartbeat"`) at this interval; default 0 (no heartbeat) |
| `max_events` | uint32 | Optional | Server closes the subscription after delivering this many events; 0 = unlimited |
| `cursor` | string | Optional | Resume from a prior position; if the cursor is expired the server MUST return `NWP-SUBSCRIBE-SEQ-TOO-OLD` |
| `lease_seconds` | uint32 | Optional | Requested lease for `open` or `renew`. Valid only when `subscription_policy` is advertised; the server clamps it to `max_lease_seconds`. Zero is invalid. |

### 13.2 Lifecycle

1. Client sends SubscribeFrame → server responds with CapsFrame (`subscription_id` echoed, `status = "open"`)
2. Server streams DiffFrame events; each carries the `subscription_id` in an EXT header or equivalent transport metadata
3. Client cancels by closing the subscription transport; server MAY also close when `max_events` is reached
4. On server-side error, server MUST send a terminal ErrorFrame with the appropriate `NWP-SUBSCRIBE-*` code before closing

Subscription-pushed DiffFrames add the following event-envelope fields to the standard DiffFrame fields:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `subscription_id` | string | Required | Associated subscription ID |
| `seq` | uint64 | Required except terminal `resync_required` | Monotonically increasing event sequence number within the subscription; reserved subscribe types MAY bind this to a domain-specific version, such as topology version (§12.3) |
| `event_type` | string | Required | `"create"` / `"update"` / `"delete"` / `"heartbeat"` / `"error"` for default subscriptions. Reserved subscribe types (§12) MAY define additional values |
| `timestamp` | string | Optional | Time of change (ISO 8601) |
| `payload` | object | Optional | Event-specific payload. Heartbeats use an empty payload |
| `cgn_est` | uint32 | Optional | Estimated CGN token cost of this push event's payload. Nodes SHOULD populate this field on each pushed DiffFrame so subscribers can perform Agent-side cumulative-budget accounting per [token-budget.md §7.2](token-budget.md). Absent means the node does not provide a per-event estimate; Agents MAY estimate locally via UTF-8/4 fallback |

**Cursor semantics**

- Cursor values are opaque server-generated strings. Clients MUST NOT parse, compare, or synthesize them.
- When the Agent detects a `seq` gap, it SHOULD re-subscribe with the latest server-issued `cursor` it has received.
- Nodes SHOULD retain recent cursor positions (recommended: 10 minutes or 10,000 events, whichever comes first).
- If `cursor` is outside the retention window, the node MUST return `NWP-SUBSCRIBE-SEQ-TOO-OLD` or emit a reserved-type-specific terminal resync event (for example `topology.stream` `resync_required`).

### 13.3 Error codes

The following error codes (defined in §14) apply to SubscribeFrame operations:

- `NWP-SUBSCRIBE-STREAM-NOT-FOUND` — subscription_id unknown or already closed
- `NWP-SUBSCRIBE-LIMIT-EXCEEDED` — server's concurrent subscription limit reached
- `NWP-SUBSCRIBE-FILTER-UNSUPPORTED` — filter expression not supported by this node
- `NWP-SUBSCRIBE-INTERRUPTED` — server-side interruption
- `NWP-SUBSCRIBE-SEQ-TOO-OLD` — cursor position is no longer available

### 13.4 Alpha.19 renewable subscription profile (NWP v0.22)

5. **NWP-P19-05 — capability truth.** A node supports lease operations only
   when it advertises `subscription_policy`. Its default and maximum MUST be
   positive, and `renew_before_seconds` MUST be less than the maximum.
6. **NWP-P19-06 — open.** `operation` absent means `open`. The server applies
   the requested lease or `default_lease_seconds`, clamps it to the maximum, and
   returns `lease_seconds` plus RFC 3339 `expires_at` in the opening CapsFrame.
7. **NWP-P19-07 — binding.** Renew and close are authorized against the same
   authenticated principal, target, and reserved `type` captured at open time.
   A different principal receives the existing authorization error and learns
   no subscription state.
8. **NWP-P19-08 — renew.** A valid renew accepted before expiry replaces the
   deadline with `accepted_at + effective_lease_seconds` and returns the new
   deadline. It never rewinds a cursor or resets event `seq`.
9. **NWP-P19-09 — expiry.** At `now >= expires_at`, the server atomically closes
   the subscription, emits at most one terminal event carrying
   `NWP-SUBSCRIBE-LEASE-EXPIRED` when the transport is writable, and releases
   resources. Renew after expiry returns the same error and cannot resurrect it.
10. **NWP-P19-10 — invalid requests.** Zero leases, invalid policy bounds,
    target/filter fields on renew/close, or an unknown operation return
    `NWP-SUBSCRIBE-LEASE-INVALID` without mutating the subscription.

---

## 14. Error Codes

| Error Code | NPS Status Code | Description |
|-----------|----------------|-------------|
| `NWP-AUTH-NID-SCOPE-VIOLATION` | `NPS-AUTH-FORBIDDEN` | Agent scope does not cover the target node |
| `NWP-AUTH-NID-EXPIRED` | `NPS-AUTH-UNAUTHENTICATED` | NID certificate has expired |
| `NWP-AUTH-NID-REVOKED` | `NPS-AUTH-UNAUTHENTICATED` | NID has been revoked |
| `NWP-AUTH-NID-UNTRUSTED-ISSUER` | `NPS-AUTH-UNAUTHENTICATED` | NID issuer not in trusted_issuers |
| `NWP-AUTH-NID-CAPABILITY-MISSING` | `NPS-AUTH-FORBIDDEN` | Agent lacks a capability required by the node |
| `NWP-AUTH-ASSURANCE-TOO-LOW` | `NPS-AUTH-FORBIDDEN` | Agent's `assurance_level` is below the node's `min_assurance_level` (or the per-action override). Response SHOULD include a `hint` pointing to a CA enrolment URL. (NPS-RFC-0003) |
| `NWP-AUTH-REPUTATION-BLOCKED` | `NPS-AUTH-FORBIDDEN` | The receiving Node's `reputation_policy` (Phase 2 NWM field — see [NPS-RFC-0004 §4.4](rfcs/NPS-RFC-0004-nid-reputation-log.md)) matched a `reject_on` rule against the requesting `subject_nid`. Response SHOULD include the matching `incident` + `severity` + log entry `seq` for traceability. The field shape that produces this error lands at NWP v0.13 (Phase 2); the error code itself is reserved at NWP v0.13 (Phase 1) so SDKs can begin recognising it without round-tripping through a future spec bump. (NPS-RFC-0004) |
| `NWP-QUERY-FILTER-INVALID` | `NPS-CLIENT-BAD-PARAM` | Filter syntax is invalid or nesting is too deep |
| `NWP-QUERY-FIELD-UNKNOWN` | `NPS-CLIENT-BAD-PARAM` | fields references a non-existent field |
| `NWP-QUERY-CURSOR-INVALID` | `NPS-CLIENT-BAD-PARAM` | cursor value cannot be decoded or has expired |
| `NWP-QUERY-REGEX-UNSAFE` | `NPS-CLIENT-BAD-PARAM` | `$regex` pattern rejected (ReDoS risk or too long) |
| `NWP-QUERY-VECTOR-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | Node does not support vector search |
| `NWP-QUERY-AGGREGATE-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | Node does not support aggregation queries |
| `NWP-QUERY-AGGREGATE-INVALID` | `NPS-CLIENT-BAD-PARAM` | aggregate structure is invalid (unknown func, duplicate alias, etc.) |
| `NWP-QUERY-STREAM-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | Node does not support streaming queries |
| `NWP-ACTION-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | action_id does not exist |
| `NWP-ACTION-PARAMS-INVALID` | `NPS-CLIENT-UNPROCESSABLE` | Operation parameter schema validation failed |
| `NWP-ACTION-IDEMPOTENCY-CONFLICT` | `NPS-CLIENT-CONFLICT` | A request with the same idempotency_key is already in progress |
| `NWP-LLM-CONTEXT-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | Stateful LLM context is unknown/released, or idempotency lookup expired (§7.6) |
| `NWP-LLM-CONTEXT-EXPIRED` | `NPS-CLIENT-GONE` | Stateful LLM context has an idle-expiry tombstone (§7.6) |
| `NWP-LLM-CONTEXT-VERSION-CONFLICT` | `NPS-CLIENT-CONFLICT` | `base_version` is stale or a concurrent mutation owns the reservation (§7.6) |
| `NWP-LLM-CONTEXT-BINDING-MISMATCH` | `NPS-CLIENT-CONFLICT` | Model/system/tools/runtime binding differs from the committed context (§7.6) |
| `NWP-LLM-CONTEXT-FORBIDDEN` | `NPS-AUTH-FORBIDDEN` | Caller is not the context owner or lacks scope/capability (§7.6) |
| `NWP-LLM-CONTEXT-LIMIT-EXCEEDED` | `NPS-LIMIT-RESOURCE` | Per-principal live-context limit reached (§7.6) |
| `NWP-LLM-CONTEXT-OPERATION-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | Context is supported but the requested lifecycle operation is not (§7.6) |
| `NWP-TASK-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | task_id does not exist |
| `NWP-TASK-ALREADY-CANCELLED` | `NPS-CLIENT-CONFLICT` | Task has already been cancelled |
| `NWP-TASK-ALREADY-COMPLETED` | `NPS-CLIENT-CONFLICT` | Task has already completed; cannot cancel |
| `NWP-TASK-ALREADY-FAILED` | `NPS-CLIENT-CONFLICT` | Task has already failed; cannot cancel |
| `NWP-SUBSCRIBE-STREAM-NOT-FOUND` | `NPS-CLIENT-NOT-FOUND` | `subscription_id` referenced by a subscription operation does not exist or is already closed |
| `NWP-SUBSCRIBE-LIMIT-EXCEEDED` | `NPS-LIMIT-EXCEEDED` | Exceeded maximum concurrent subscriptions |
| `NWP-SUBSCRIBE-FILTER-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | Node does not support filtered subscriptions |
| `NWP-SUBSCRIBE-INTERRUPTED` | `NPS-SERVER-UNAVAILABLE` | Subscription stream terminated due to underlying data source interruption |
| `NWP-SUBSCRIBE-SEQ-TOO-OLD` | `NPS-CLIENT-CONFLICT` | `cursor` is outside the node's retention window; full re-query or reserved-type resync required |
| `NWP-SUBSCRIBE-LEASE-INVALID` | `NPS-CLIENT-BAD-PARAM` | Renewable-subscription operation or lease/policy bounds are invalid |
| `NWP-SUBSCRIBE-LEASE-EXPIRED` | `NPS-CLIENT-GONE` | Subscription lease reached its deadline and cannot be renewed or resurrected |
| `NWP-BUDGET-EXCEEDED` | `NPS-LIMIT-BUDGET` | Response would exceed the token budget |
| `NWP-DEPTH-EXCEEDED` | `NPS-CLIENT-BAD-PARAM` | depth exceeds the node's allowed max_depth |
| `NWP-GRAPH-CYCLE` | `NPS-CLIENT-UNPROCESSABLE` | Node graph contains a circular reference |
| `NWP-NODE-UNAVAILABLE` | `NPS-SERVER-UNAVAILABLE` | Underlying data source temporarily unavailable |
| `NWP-MANIFEST-VERSION-UNSUPPORTED` | `NPS-CLIENT-BAD-PARAM` | Agent NPS version is below the node's min_agent_version |
| `NWP-MANIFEST-NODE-TYPE-REMOVED` | `NPS-CLIENT-BAD-FRAME` | NWM `node_type` contains the removed legacy value `"gateway"` (NPS-CR-0001); response SHOULD include a `hint` pointing to NPS-CR-0001 |
| `NWP-MANIFEST-NODE-TYPE-UNKNOWN` | `NPS-CLIENT-BAD-FRAME` | NWM `node_type` contains an unrecognized value (see `NWP-MANIFEST-NODE-TYPE-REMOVED` for the `"gateway"` case) |
| `NWP-RATE-LIMIT-EXCEEDED` | `NPS-LIMIT-RATE` | Rate limit exceeded |
| `NWP-RESERVED-TYPE-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | `QueryFrame` or `SubscribeFrame` `type` is an unrecognized reserved-type identifier (§12). Distinct from `NWP-ACTION-NOT-FOUND` — the unknown operand is `type`, not `action_id`. |
| `NWP-HTTP-ORIGIN-FORBIDDEN` | `NPS-AUTH-FORBIDDEN` | HTTP overlay origin policy rejected the caller (§9.5) |
| `NWP-HTTP-CONTENT-TYPE-UNSUPPORTED` | `NPS-CLIENT-BAD-FRAME` | HTTP overlay request `Content-Type` is not a supported NWP frame media type (§9.5) |
| `NWP-HTTP-ACCEPT-UNSATISFIABLE` | `NPS-CLIENT-BAD-PARAM` | HTTP overlay request `Accept` cannot be satisfied by any supported response media type (§9.5) |
| `NWP-HTTP-REQUEST-ID-MISMATCH` | `NPS-CLIENT-BAD-PARAM` | Response `X-NWP-Request-ID` does not echo the request ID (§9.5) |
| `NWP-HTTP-FRAME-BODY-MALFORMED` | `NPS-CLIENT-BAD-FRAME` | HTTP body cannot be parsed as a supported NWP frame envelope (§9.5) |
| `NWP-HTTP-BODY-TOO-LARGE` | `NPS-LIMIT-PAYLOAD` | HTTP request body exceeds the server's NWP body limit (§9.5, §16.5.1) |
| `NWP-CAPABILITY-ADVERTISED-UNIMPLEMENTED` | `NPS-SERVER-UNSUPPORTED` | NWM advertises a capability/profile that the node currently cannot serve (§9.5) |
| `NWP-TOPOLOGY-UNAUTHORIZED` | `NPS-AUTH-FORBIDDEN` | Caller lacks permission to read this Anchor's topology (§12). Authorization policy is implementation-defined per §12.4 |
| `NWP-TOPOLOGY-UNSUPPORTED-SCOPE` | `NPS-CLIENT-BAD-PARAM` | `topology.scope` value is not implemented by this Anchor |
| `NWP-TOPOLOGY-DEPTH-UNSUPPORTED` | `NPS-CLIENT-BAD-PARAM` | Requested `topology.depth` exceeds this Anchor's maximum |
| `NWP-TOPOLOGY-FILTER-UNSUPPORTED` | `NPS-CLIENT-BAD-PARAM` | `topology.filter` contains an unrecognized key |
| `NWP-ANCHOR-NOT-LEADER` | `NPS-CLIENT-CONFLICT` | A topology-mutating write reached a standby (or read-only-degraded) Anchor; only the active owner of the current `cluster_epoch` may accept writes (§12.2, NPS-CR-0009) |
| `NWP-ANCHOR-EPOCH-FENCED` | `NPS-CLIENT-CONFLICT` | An inbound frame carries a `cluster_epoch` higher than the receiving Anchor's own; the receiver is a superseded leader and fences itself (§12.2, NPS-CR-0009) |
| `NWP-BRIDGE-DIRECTION-UNSUPPORTED` | `NPS-SERVER-UNSUPPORTED` | The request arrived on a direction/protocol this Bridge Node does not declare — see §16.2; the response SHOULD carry both `bridge_protocols` and `bridge_inbound_protocols` in `hint` (NPS-CR-0010) |

---

## 15. Security Considerations

### 15.1 Scope Enforcement
Nodes MUST validate the Agent NID's scope on every request. Requests exceeding scope MUST return `NWP-AUTH-NID-SCOPE-VIOLATION` and MUST NOT return any data.

### 15.2 SSRF Protection
When Complex Nodes resolve sub-node references, they MUST maintain an allowlist of permitted node URL prefixes and MUST NOT access internal network addresses (RFC 1918).

### 15.3 Token Budget Enforcement
When the budget is exceeded, nodes SHOULD prefer trimming response content (field reduction → summary → record truncation); if trimming is not possible, they MUST return `NWP-BUDGET-EXCEEDED` and MUST NOT silently truncate data. See [token-budget.md §4.3](token-budget.md).

### 15.4 Rate Limiting
Nodes SHOULD enforce per-Agent NID rate limiting. On limit exceeded, return `NWP-RATE-LIMIT-EXCEEDED` with an `X-NWP-Rate-Reset` header. Unauthenticated requests SHOULD be limited by IP.

### 15.5 Filter Injection Protection
- Field names MUST contain only letters/digits/underscores/dots, length ≤ 128 characters
- `$regex` patterns MUST undergo ReDoS detection; filter nesting depth ≤ 8
- Nodes MUST use parameterized queries; string concatenation is prohibited

### 15.6 callback_url Abuse Prevention
- ActionFrame `callback_url` MUST use the `https://` scheme
- Nodes SHOULD perform SSRF checks on callback URLs (internal network addresses prohibited)
- Nodes SHOULD apply exponential backoff retries for failed callback deliveries (max 3 attempts), then abandon and mark the task as `COMPLETED` rather than retrying indefinitely

### 15.7 Topology Read-back
Anchor Nodes implementing §12 MUST treat `topology.snapshot` and `topology.stream` as authenticated surfaces. The minimum authorization binding is defined in §12.4: at Phase 1–2, the requesting NID MUST declare `topology:read` in `IdentFrame.capabilities` (primary gate); NDP `node_roles` cross-check is a defense-in-depth SHOULD. Unauthorized callers MUST receive `NWP-TOPOLOGY-UNAUTHORIZED` rather than silent empty responses to prevent oracle attacks against cluster membership.

---

## 16. Bridge Node Conformance

The Bridge Node type and the `bridge_target` object schema were introduced by NPS-CR-0001 (§2.1)
and standardized at NWP v0.13. [NPS-CR-0010](cr/NPS-CR-0010-bridge-bidirectional.md) settled Bridge
Node as **bidirectional** and split this section into two independent conformance profiles. This
section formalises both, provides the normative error mapping shared by them, and provides canonical
`bridge_target` round-trip test vectors so that all SDK Bridge implementations agree on the wire shape.

### 16.1 Conformance profiles

A Bridge Node MUST claim at least one of the two profiles below and MUST declare it on the wire
(§2.1, NPS-4 §3.1). Claiming neither is not a conformant Bridge Node. A Bridge Node MAY claim both.
An implementation that claims only **Outbound** — the only profile that existed through alpha.15 —
remains fully conformant with no change.

#### 16.1.1 Outbound profile (NPS → external)

Claimed by declaring a non-empty `bridge_protocols`. A conformant outbound Bridge Node MUST:

1. Advertise `node_type: "bridge"` in its NWM (§4.1) and the supported external protocols via
   NDP `bridge_protocols` (NPS-4 §3.1).
2. Accept inbound NWP frames carrying a `bridge_target` object and reject a frame missing it, or
   one that fails `bridge_target` schema validation, with `NWP-BRIDGE-TARGET-INVALID`.
3. Validate `bridge_target.protocol` against its advertised set; a protocol with no registered
   dispatcher MUST return `NWP-BRIDGE-PROTOCOL-UNSUPPORTED` (not a silent fallthrough).
4. Treat unknown `bridge_target` fields as opaque pass-through and MUST NOT fail on them
   (forward compatibility).
5. Be **stateless per request** and MUST NOT participate in cluster topology (`topology.*` MUST
   return `NWP-RESERVED-TYPE-UNSUPPORTED` on a pure Bridge Node).

A conformant outbound Bridge Node SHOULD:

1. Apply SSRF protection to `bridge_target.endpoint` (NPS-2 §15.2) before dialing the upstream.
2. Propagate `bridge_target.extras.headers` to the upstream HTTP request verbatim, minus
   hop-by-hop headers.

#### 16.1.2 Inbound profile (external → NPS)

Claimed by declaring a non-empty `bridge_inbound_protocols`. A conformant inbound Bridge Node MUST:

1. Advertise `node_type: "bridge"` in its NWM (§4.1) and the served protocols via NDP
   `bridge_inbound_protocols` (NPS-4 §3.1).
2. Expose a server endpoint for each declared protocol, speaking that protocol's native wire format,
   and require **no** NPS addressing, NID, or frame knowledge of the foreign client.
3. Project the NPS nodes it fronts onto the foreign protocol's object model: Memory Nodes onto the
   protocol's read surface, Action / Complex Nodes onto its call surface. For **MCP** this means
   serving `initialize`, `ping`, `tools/list`, `tools/call`, `resources/list`, and `resources/read` —
   an inbound MCP Bridge that omits `resources/*` is **not** conformant.
4. Map NWP / NPS error codes onto the foreign protocol's error space per §16.3, deterministically.
5. Reject a request for a protocol it did not declare in `bridge_inbound_protocols` with
   `NWP-BRIDGE-DIRECTION-UNSUPPORTED`; the response SHOULD carry both declared arrays in `hint`.
6. Be **stateless per request** and MUST NOT participate in cluster topology.

A conformant inbound Bridge Node SHOULD:

1. Apply the fronted node's own authorization (§7) to the translated NWP frame rather than granting
   the foreign client ambient authority.
2. Preserve NPS status-code semantics through the mapping — a mapping that collapses distinct NPS
   status classes onto one foreign error is a conformance failure, not a quality-of-implementation
   matter.

### 16.2 Direction declaration

`bridge_protocols` and `bridge_inbound_protocols` are independent sets over the same value domain
(`"http"`, `"grpc"`, `"mcp"`, `"a2a"`). A protocol MAY appear in both (bridged in both directions).
A node declaring `node_roles: ["bridge"]` MUST have at least one of the two non-empty. Receivers MUST
treat an absent `bridge_inbound_protocols` as `[]` — which is exactly a pre-alpha.16, outbound-only
Bridge Node. (NPS-CR-0010 §3.2)

### 16.3 Error mapping (normative)

Both directions translate NPS status codes into (or out of) a foreign protocol's error space. Through
alpha.15 this mapping was implemented twice per protocol — once in the outbound dispatcher, once in
the then-separate `compat/*-ingress` package — and the two copies drifted. The mapping is therefore
normative, and a single implementation of it MUST serve both directions.

**MCP (JSON-RPC 2.0).** NPS status → JSON-RPC error code:

| NPS status | JSON-RPC code | Notes |
|---|---|---|
| `NPS-CLIENT-BAD-FRAME` | `-32600` (Invalid Request) | |
| `NPS-CLIENT-BAD-PARAM`, `NPS-CLIENT-UNPROCESSABLE` | `-32602` (Invalid params) | |
| `NPS-CLIENT-NOT-FOUND` | `-32601` (Method not found) for an unknown tool in `tools/call`; `-32602` for an unknown URI in `resources/read` | The distinction matters: an unknown *tool* is a missing method to an MCP client, an unknown *resource* is a bad argument |
| `NPS-CLIENT-GONE` | `-32602` | |
| `NPS-CLIENT-CONFLICT` | `-32004` (implementation-defined) | |
| `NPS-AUTH-UNAUTHENTICATED` | `-32001` (implementation-defined) | MUST be a JSON-RPC error, never a successful result carrying an error payload |
| `NPS-AUTH-FORBIDDEN` | `-32003` (implementation-defined) | MUST NOT be collapsed onto `-32001` |
| `NPS-LIMIT-RATE`, `NPS-LIMIT-BUDGET`, `NPS-LIMIT-PAYLOAD`, `NPS-LIMIT-RESOURCE` | `-32005` (implementation-defined) | |
| `NPS-SERVER-UNSUPPORTED` | `-32601` (Method not found) | Includes `NWP-BRIDGE-DIRECTION-UNSUPPORTED` |
| `NPS-SERVER-INTERNAL`, `NPS-SERVER-UNAVAILABLE`, `NPS-SERVER-TIMEOUT`, `NPS-DOWNSTREAM-UNAVAILABLE` | `-32603` (Internal error) | Upstream node failure |
| parse failure before dispatch | `-32700` (Parse error) | |

The application-defined code `-32002` is **reserved and MUST NOT be emitted**. Through alpha.15 the
.NET Bridge used it for "tool not found"; NPS-CR-0010 maps an unknown tool to `-32601`
(Method not found) instead — which is what an MCP client already understands — and leaves `-32002`
reserved rather than reassigning it, so a client pinned to the old behaviour cannot silently misread
some other error as a missing tool.

The reverse direction (JSON-RPC error → NPS status) is the inverse of this table; where the inverse is
not injective, an implementation MUST choose the **most specific** NPS status, never a generic
`NPS-SERVER-INTERNAL`.

**gRPC.** NPS status → gRPC status code:

| NPS status | gRPC status |
|---|---|
| `NPS-CLIENT-BAD-FRAME`, `NPS-CLIENT-BAD-PARAM`, `NPS-CLIENT-UNPROCESSABLE` | `INVALID_ARGUMENT` |
| `NPS-CLIENT-NOT-FOUND`, `NPS-CLIENT-GONE` | `NOT_FOUND` |
| `NPS-CLIENT-CONFLICT` | `ABORTED` |
| `NPS-AUTH-UNAUTHENTICATED` | `UNAUTHENTICATED` |
| `NPS-AUTH-FORBIDDEN` | `PERMISSION_DENIED` |
| `NPS-LIMIT-RATE`, `NPS-LIMIT-BUDGET`, `NPS-LIMIT-PAYLOAD`, `NPS-LIMIT-RESOURCE` | `RESOURCE_EXHAUSTED` |
| `NPS-SERVER-UNSUPPORTED` | `UNIMPLEMENTED` |
| `NPS-SERVER-INTERNAL` | `INTERNAL` |
| `NPS-SERVER-UNAVAILABLE`, `NPS-DOWNSTREAM-UNAVAILABLE` | `UNAVAILABLE` |
| `NPS-SERVER-TIMEOUT` | `DEADLINE_EXCEEDED` |

**A2A.** NPS status → A2A task state: client-class errors terminate the task as `failed` with the NPS
code preserved verbatim in the failure detail; server-class errors terminate as `failed` and are
retryable. An A2A Bridge MUST NOT silently downgrade an error to a `completed` task.

An implementation MUST NOT collapse distinct NPS status classes onto a single foreign error code
(§16.1.2 SHOULD-2 makes this observable); doing so is a conformance failure.

### 16.4 `bridge_target` test vectors

The canonical wire shape is `{ "protocol", "endpoint", "extras"? }` (the SDK in-memory form;
`headers` and other per-protocol knobs travel inside `extras`). All six SDKs MUST round-trip
these vectors identically (`from_dict(to_dict(x)) == x`):

```json
{ "protocol": "http", "endpoint": "https://api.example.com/v1/orders" }
```

```json
{
  "protocol": "http",
  "endpoint": "https://api.example.com/v1/orders",
  "extras": { "method": "POST", "headers": { "X-Tenant": "acme" } }
}
```

```json
{ "protocol": "grpc", "endpoint": "grpc.example.com:443/orders.OrderService/Create" }
```

```json
{ "protocol": "mcp", "endpoint": "https://mcp.example.com/sse", "extras": { "tool": "create_order" } }
```

Vector rules:
- `protocol` and `endpoint` are required and MUST be preserved verbatim.
- `extras` MUST be omitted from the serialized form when empty/absent (not emitted as `null`).
- A `BridgeNodeDescriptor` serialises `supported_protocols` as a **sorted** array for stable
  output across SDKs.

### 16.5 Portable Node and Bridge server profile

NWP v0.20 defines a transport-independent decision profile for SDK-hosted servers. It does not add
a frame type. Implementations MAY expose framework-specific middleware, but their admission,
dispatch, cancellation, and error decisions MUST match the shared vectors in
`spec/conformance/nwp/`.

#### 16.5.1 Node admission and dispatch

Memory, Action, and Complex Node servers claiming the portable profile MUST:

1. Serve `/.nwm` with `application/nwp-manifest+json` and require `GET`; a method rejection MUST
   return HTTP 405 with `Allow: GET`. Memory and Complex Nodes MUST dispatch `QueryFrame`; Action
   and Complex Nodes MUST dispatch `ActionFrame`.
2. Require `POST` for `/query` and `/invoke`. A method rejection occurs before frame admission,
   returns HTTP 405, and MUST include `Allow: POST`.
3. Accept `application/nwp-frame`. During the alpha.17 compatibility window they MUST also accept
   legacy request media type `application/x-nps-frame`, but MUST emit only the canonical
   `application/nwp-capsule`, `application/nwp-error+json`, and
   `application/nwp-manifest+json` response media types. The legacy alias is deprecated and is
   removed from the required profile in alpha.18.
4. Enforce a finite, configurable HTTP body limit before decoding. An oversized body MUST return
   HTTP 413 with `NPS-LIMIT-PAYLOAD` / `NWP-HTTP-BODY-TOO-LARGE`.
5. Return canonical §9.5 errors for unsupported `Content-Type`, unsatisfied `Accept`, and malformed
   bodies. HTTP error bodies MUST use `application/nwp-error+json`.
6. Preserve the request correlation identifier end to end: `X-NWP-Request-ID` in HTTP mode and the
   frame `request_id` in native mode. Responses that have a correlation field MUST echo it.
7. Propagate caller cancellation into decode and provider/handler work. If cancellation is observed
   before a response is committed, the server MUST abort without synthesizing an ErrorFrame or HTTP
   error response.
8. Record one terminal telemetry outcome from `success`, `rejected`, `cancelled`, or `timeout`; the
   correlation identifier SHOULD be attached when the telemetry system permits it.

In native mode, unsupported decoded frame types MUST produce an `ErrorFrame` with
`NPS-CLIENT-BAD-FRAME` / `NWP-NATIVE-FRAME-UNSUPPORTED`. A provider failure after successful
admission remains `NWP-NATIVE-DISPATCH-FAILED` unless a more specific protocol error is available.

The normative cases are `portable_node_server_vectors.json`.

#### 16.5.2 Bridge preflight and lifecycle

An outbound Bridge server claiming the portable profile MUST perform the following checks before
dialing an upstream:

1. Validate the `bridge_target` shape, then resolve `protocol` in the registered dispatcher set.
   A missing protocol is `NPS-CLIENT-UNPROCESSABLE` / `NWP-BRIDGE-TARGET-INVALID`; a well-formed but
   unregistered protocol is `NPS-SERVER-UNSUPPORTED` /
   `NWP-BRIDGE-PROTOCOL-UNSUPPORTED`.
2. Validate an absolute endpoint scheme and apply configured HTTPS, prefix-allowlist, and
   private/loopback address policy. Rejection is `NPS-CLIENT-UNPROCESSABLE` /
   `NWP-BRIDGE-ENDPOINT-INVALID`. SDK policy evaluation MUST be deterministic and MUST NOT require
   DNS for literal-address inputs; hosts resolved at dial time remain subject to rebinding-safe
   address checks.
3. Apply a finite deadline across preflight, connection, response headers, body translation, and
   response emission. An exhausted deadline is HTTP 504 with `NPS-SERVER-TIMEOUT` /
   `NWP-BRIDGE-UPSTREAM-FAILED`; cancellation takes precedence over timeout and produces no
   synthesized response.
4. Preserve correlation identity into the external request where the target protocol has a
   correlation or metadata mechanism, and preserve synchronous versus asynchronous Action task
   mode. An admitted async action reports `NPS-OK-ACCEPTED`.
5. Emit one terminal telemetry outcome under the same rule as §16.5.1.

The normative cases are `bridge_lifecycle_vectors.json`. All six reference SDKs MUST execute both
v0.20 vector sets in CI. Equivalent decisions, status/error pairs, response media types, and
correlation behavior are required across HTTP and native hosts.

---

## 17. Changelog

| Version | Date | Changes |
|---------|------|---------|
| 0.23 | 2026-09-09 | Alpha.20 feature-dependent HTTP admission: NWM advertisement, exact required header, all-backend route guarantee, zero-dispatch rejection and success echo; base behavior preserved when unused. Also defines context carriers/full request binding and governed challenge states, fixed Anchors, management actions and durable retention. |
| 0.22 | 2026-08-31 | Alpha.19 NWM/subscription hardening: deterministic stability/SLA/billing normalization and action fallback; advertised renewable subscription policy; open/renew/close operations with principal binding, clamped leases, explicit deadlines, non-resurrectable expiry, and shared conformance vectors. Adds two subscription errors. Depends-On advances to NCP 0.12, NIP 0.15, and NDP 0.13. |
| 0.21 | 2026-08-12 | **NPS-CR-0011 stateful LLM context/delta completion**: NWM LLM profile 0.2 advertises explicit context operations/persistence/limits; `llm.complete` gains optional owner-bound opaque context requests and terminal receipts; adds status/release lifecycle actions, CAS versions, binding checks, atomic cancellation/stream commit, restart/idempotency semantics, measured `wire_input_bytes`, seven deterministic errors, and shared conformance vectors. Stateless completion remains compatible and stateful requests never silently fall back. Depends-On NIP advanced to v0.14 for `llm:context`. No new frame type. |
| 0.20 | 2026-07-29 | Added §16.5 portable Node/Bridge server profile and shared cross-language vectors. Standardized HTTP/native admission, role dispatch, canonical/legacy MIME handling for the alpha.17 compatibility window, finite body limits, cancellation, correlation propagation, Bridge dispatcher/SSRF/deadline preflight, async task mode, and terminal telemetry outcomes. Added `NWP-HTTP-BODY-TOO-LARGE` → `NPS-LIMIT-PAYLOAD`; no new frame type. Depends-On advanced to NCP v0.11 and NIP v0.13. |
| 0.19 | 2026-07-23 | **NPS-CR-0010 Bridge Node is bidirectional**: resolved the spec's own contradiction — the §2.1 taxonomy, the "Removed types" note, and NPS-CR-0001 all defined Bridge Node as NPS↔non-NPS translation, while the §2.1 callout and the normative MUST list narrowed it to NPS→external only. The narrowing existed solely to keep the name `Bridge` distinct from the then-separate `compat/*-ingress` packages; those are now absorbed into the Bridge package and the restriction is lifted. Bridge Node semantics restructured into **Outbound** (unchanged) + **Inbound** (new) MUST lists; MCP inbound MUST serve `resources/*` as well as `tools/*`. §16 split into two independent conformance profiles (§16.1.1 outbound / §16.1.2 inbound), a normative direction declaration (§16.2), and normative per-protocol error-mapping tables (§16.3) that a single implementation MUST serve in both directions. Role-vs-library boundary made explicit: only a deployment announcing `node_roles: ["bridge"]` is a Bridge Node. `Depends-On` NDP bumped to v0.11 (defines `bridge_inbound_protocols`). One new error code `NWP-BRIDGE-DIRECTION-UNSUPPORTED`. Additive and backward-compatible: an outbound-only Bridge Node remains conformant unchanged. (Renumbered from the edge-line 0.16 — the released alpha.16 line had independently used 0.15–0.17 for the LLM profile series below.) |
| 0.18 | 2026-07-23 | **NPS-CR-0009 multi-Anchor HA**: finalised the two §12.2 `anchor_state` sub-types `anchor_failover` (`successor_nid` / `cluster_epoch` / `reason`) and `anchor_quorum_lost` (`quorum_size` / `available`), removing the Phase-3-placeholder “MUST NOT emit” restriction. New `cluster_epoch` (uint64) ownership fence on topology responses/writes; standby writes rejected with `NWP-ANCHOR-NOT-LEADER`, superseded leaders fenced with `NWP-ANCHOR-EPOCH-FENCED`. Additive and Phase-gated: single-Anchor clusters keep `cluster_epoch = 1` and are unaffected. `Depends-On` NDP bumped to v0.10 (defines `cluster_epoch` on AnnounceFrame + highest-epoch resolution). Two new error codes. |
| 0.17 | 2026-07-05 | Added §9.5 HTTP Binding Rejection Codes and six canonical NWP error codes for HTTP overlay preconditions and advertised-but-unimplemented capability rollout windows: `NWP-HTTP-ORIGIN-FORBIDDEN`, `NWP-HTTP-CONTENT-TYPE-UNSUPPORTED`, `NWP-HTTP-ACCEPT-UNSATISFIABLE`, `NWP-HTTP-REQUEST-ID-MISMATCH`, `NWP-HTTP-FRAME-BODY-MALFORMED`, and `NWP-CAPABILITY-ADVERTISED-UNIMPLEMENTED`. Shared `error-codes.md` bumped to v1.6. |
| 0.16 | 2026-07-04 | Adds §4.2a NWM `profiles` and the standard LLM/Thinking Profile (`profiles.llm`) for model-serving Action/Complex Nodes. Clarifies that "Thinking Node" is a product-facing alias, not a new `node_type`; coarse discovery uses NIP/NDP `llm:*` capabilities while detailed model, streaming, tool, privacy, and reasoning-disclosure metadata lives in NWM. No new frame type or error code. Depends-On NIP bumped to v0.11 for the `llm:*` capability registry. |
| 0.15 | 2026-07-04 | New §7.5 standardizes the `llm.complete` ActionFrame contract: typed request/response DTO shape, stop_reason enum, tool call field names, sync/async/streaming response semantics, ErrorFrame-vs-payload-error rule, and snake_case JSON/MessagePack key policy. No new frame type or error code. |
| 0.14 | 2026-06-12 | New §16 **Bridge Node Conformance**: formal MUST/SHOULD requirements (advertise `node_type: "bridge"`, validate `bridge_target.protocol`, opaque unknown-field pass-through, stateless / no-topology) and canonical `bridge_target` round-trip test vectors (http / grpc / mcp, with `extras`) all six SDKs must round-trip identically. §16 Changelog renumbered to §17. No new error codes; no `Depends-On` change. |
| 0.14 | 2026-06-03 | NWM `manifest_version` type changed from opaque string (ETag) to uint32 monotonic counter (starts at 1, incremented on every structural change). New NWM field `manifest_updated_at` (ISO 8601, optional) records the last-change timestamp. Servers MUST return `X-NWM-Version: <manifest_version>` on every `GET /.nwm` response; agents use `If-None-Match: <uint32>` for conditional requests. No new error codes; `304 Not Modified` reused for cache hits. |
| 0.13 | 2026-05-28 | §13 SubscribeFrame (0x12) formal specification (closes CR-0006): field table (subscription_id, filter, heartbeat_interval_ms, max_events, cursor), lifecycle (open→active→heartbeat→close), error code reference. §12.4 `topology:subscribe` enforcement promoted SHOULD → MUST; nodes that cannot enforce MUST document non-enforcement in NWM stability metadata. NWM gains optional `trust_anchors` field (array of CA NID URNs). BridgeNode `bridge_target` object schema standardized (protocol + endpoint + extras carrier). |
| 0.12 | 2026-05-11 | NPS-CR-0002 Phase 2 spec gaps closed. §8.2 DiffFrame extension table gains optional `cgn_est` field (uint32) for per-event CGN reporting on push streams per [token-budget.md §7.2](token-budget.md); columns reformatted to include Required. §12.2 `topology.stream` events table: `anchor_state` row gains explicit sub-type discriminator schema (`version_rebased` defined for Phase 1–2; `anchor_failover` and `anchor_quorum_lost` reserved as Phase 3 placeholder slots — implementations MUST NOT emit Phase 3 sub-types pre-stable and MUST ignore unknown sub-types for forward compatibility); `resync_required` trigger and `reason` enum broadened (`version_too_old` / `anchor_rebased` / `server_state_lost`). §12.4 Phase 1–2 authorization model expanded: (a) capability gate split per surface — `topology.snapshot` requires `topology:read`; `topology.stream` requires `topology:read` AND SHOULD additionally require `topology:subscribe` in Phase 2 (MUST in Phase 3); (b) new mid-stream rejection rule — server MUST emit terminal `NWP-TOPOLOGY-UNAUTHORIZED` event then close the stream on capability revocation; (c) new reputation interaction — for active subscriptions, Anchors with a declared `reputation_policy` SHOULD emit terminal `NWP-AUTH-REPUTATION-BLOCKED` and close the stream when the subscriber's reputation drops below threshold. No new error codes; existing `NWP-TOPOLOGY-UNAUTHORIZED` and `NWP-AUTH-REPUTATION-BLOCKED` reused. No `Depends-On` change. See issue #41. |
| 0.11 | 2026-05-10 | NWM gains optional top-level `stability` (`experimental`/`stable`/`deprecated`), `sla` (object: `p95_latency_ms`, `availability`, `sla_tier`), and `billing` (object: `metering_profile`, `billing_unit`, `price_hint`, `currency`) fields (§4.1, §4.4a, §4.4b). ActionSpec (§4.6) gains matching per-action `stability` / `sla` / `billing` overrides with field-level fallback to the top-level values. All fields are advisory (no protocol-level enforcement) and backward-compatible — pre-0.11 manifests are treated as `stability="stable"` with no SLO/billing metadata. Enables marketplace / NeuronHub clients to filter, warn, or rank services by lifecycle stage and commercial profile per AaaS-Profile discovery requirements. No new error codes; no `Depends-On` change. See issue #36. |
| 0.10 | 2026-05-01 | §12.4 authorization model replaced "implementation-defined" with a Phase 1–2 minimum binding: Anchor Nodes MUST require `topology:read` in `IdentFrame.capabilities` (capability gate, self-declared but signed); SHOULD cross-check NDP `node_roles` contains `"anchor"` as defense-in-depth; Phase 3 [RFC-0002 stable] adds CA-attested `id-nps-node-roles` cert extension. §14.7 updated to reference §12.4 defined minimum instead of the previous hedging "SHOULD restrict" language. `Depends-On` NIP bumped to v0.6 (defines `topology:read` capability). |
| 0.9 | 2026-05-01 | **Breaking rename (pre-1.0)**: Topology member object field `node_kind` renamed to `node_roles` (§12.1); topology stream filter key `node_kind` renamed to `node_roles` (§12.2). §2.1 updated: `node_kind` reference to `node_roles`. New §2.1 **Node Role Resolution** section: `node_roles` (NDP, discovery-layer, array) and `node_type` (NWM, service-layer, string) are distinct fields — `node_type` MUST be one of the values in `node_roles`; validators SHOULD verify against cached NDP data. §4.1 `node_type` description updated with the cross-protocol constraint and pointer to §2.1. §14.7 `node_kind` reference updated to `node_roles`. Depends-On NDP bumped to v0.6. Fixes M1 naming-disambiguation issue. |
| 0.8 | 2026-04-27 | New §12 **Reserved Query Types** introducing the `topology.*` namespace mandatory at NPS-AaaS Profile L2: `topology.snapshot` (QueryFrame, `type="topology.snapshot"`) and `topology.stream` (SubscribeFrame, `type="topology.stream"`). Both QueryFrame §6.1 and SubscribeFrame §8.1 gain an optional top-level `type` field for opting into reserved types. DiffFrame §8.2 `event_type` enum extended via reserved subscribe types — `topology.stream` adds `member_joined` / `member_left` / `member_updated` / `anchor_state` / `resync_required`. Five new error codes: `NWP-TOPOLOGY-UNAUTHORIZED`, `NWP-TOPOLOGY-UNSUPPORTED-SCOPE`, `NWP-TOPOLOGY-DEPTH-UNSUPPORTED`, `NWP-TOPOLOGY-FILTER-UNSUPPORTED` (table §13). New §14.7 Topology Read-back security section. Existing §12 Error Codes / §13 Security / §14 Changelog renumbered to §13 / §14 / §15 to accommodate the new section. See [NPS-CR-0002](cr/NPS-CR-0002-anchor-topology-queries.md). |
| 0.7 | 2026-04-26 | **Breaking.** §2.1 Node Types: `Gateway Node` removed; replaced by **Anchor Node** (cluster control plane + NOP routing — inherits the existing role) and **Bridge Node** (NPS↔non-NPS protocol translation — new). NWM `node_type` enum updated; legacy `"gateway"` MUST be rejected. Anchor Node detailed semantics (§2.1 inline) cover member dispatch + optional registry; Bridge Node semantics cover HTTP/gRPC/MCP/A2A target adapters. Depends-On upgraded to NDP v0.8 for the `node_kind` array form + `cluster_anchor` + `bridge_protocols` fields. See [NPS-CR-0001](cr/NPS-CR-0001-anchor-bridge-split.md). |
| 0.6 | 2026-04-25 | NWM gains optional top-level `min_assurance_level` field (`anonymous` / `attested` / `verified`), with `min_assurance_level` per-action override on individual ActionSpecs (§4.6). New error code `NWP-AUTH-ASSURANCE-TOO-LOW` (`NPS-AUTH-FORBIDDEN`). `Depends-On` upgraded to NCP v0.7 (NPS-RFC-0001) and NIP v0.9 (NPS-RFC-0003). See [NPS-RFC-0003](rfcs/NPS-RFC-0003-agent-identity-assurance-levels.md). |
| 0.4 | 2026-04-14 | §3.2 added `/actions` sub-path; §4.1 NWM added `actions` field; §4.2 capabilities added stream_query and aggregate; §4.6 NWM Action Registry (ActionSpec, params_anchor/result_anchor/async/idempotent); QueryFrame §6.1 added `stream`, `aggregate`, `request_id`; §6.6 Streaming Query Protocol (StreamFrame sequence, estimated_total, early termination); §6.7 Aggregation queries (COUNT/SUM/AVG/MIN/MAX/COUNT_DISTINCT, group_by, having); ActionFrame §7.1 added `request_id`; SubscribeFrame §8.1 added `resume_from_seq`; §8.2 DiffFrame extension fields (monotonic seq, event_type, timestamp) and reconnection semantics; §9.1/9.2 added X-NWP-Request-ID; §9.4 HTTP mode error response format (application/nwp-error+json); §10 updated complete examples (including error response); §13.6 callback_url abuse prevention security section; 5 new error codes (AGGREGATE-UNSUPPORTED/-INVALID, STREAM-UNSUPPORTED, SUBSCRIBE-SEQ-TOO-OLD, task cancel series) |
| 0.3 | 2026-04-14 | SubscribeFrame (0x12); auto_anchor; Filter $not/$exists/$regex; ActionFrame callback_url/priority; system.task.*; NWM min_agent_version/rate_limits; §14.4/14.5 security sections |
| 0.2 | 2026-04-12 | Unified port 17433; AnchorFrame owned by Node; CGN metering; NPS status code mapping |
| 0.1 | 2026-04-10 | Initial specification |

---

*Attribution: LabAcacia / INNO LOTUS PTY LTD · Apache 2.0*
