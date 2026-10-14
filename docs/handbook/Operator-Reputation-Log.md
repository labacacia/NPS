# Operator: NID Reputation Log

> **Audience:** Operators (running an nps-ledger instance) + AaaS operators (consuming a log)
> **Status:** ✅ Reviewed for v1.0.0-alpha.19
> **Source-of-truth precedence:** `spec/` documents in [`labacacia/NPS`](https://github.com/labacacia/NPS/tree/main/spec) win over this page if they disagree.

The NPS reputation log is Certificate Transparency for AI agents — an append-only, signed, Merkle-tree-backed log of NID behavioral incidents. Any party (AaaS gateway, CA, auditor) can publish signed observations about a NID, and any node can query the log before admitting an agent. Multiple independent log operators are expected; nodes choose which logs to trust.

**Spec**: `spec/rfcs/NPS-RFC-0004-nid-reputation-log.md` (Status: Accepted; Phase 3 landed in alpha.5)
**Reference implementation**: `nps-ledger` (private; `labacacia/NPS-Ledger`)

---

## What the reputation log provides

| Capability | Available since |
|------------|----------------|
| Submit and query signed entries via HTTP API | Phase 1 (alpha.3) |
| Merkle tree, Signed Tree Head (STH), inclusion proofs | Phase 2 (alpha.4) |
| STH Gossip Protocol, fork detection | Phase 3 (alpha.5) |
| `ReputationLogClient` SDK helper (all six SDKs) | alpha.7 |
| Federation push (`POST /v1/log/federation/push`), NDP §9 loop detection | alpha.11 |

The CT analogy holds precisely:
- **Log operator** = CT log operator (appends entries, signs the tree)
- **Issuer** = CA or AaaS gateway publishing an incident against a NID
- **Auditor** = any node querying the log before admitting an agent
- **STH gossip** = cross-log consistency verification (analogous to RFC 9162 §8.1.4)

---

## Log entry shape (RFC-0004 §4.1)

Each entry is a signed JSON object with the following fields:

| Field | Required | Description |
|-------|----------|-------------|
| `v` | yes | Schema version; always `1` |
| `log_id` | yes | NID of the log operator appending this entry |
| `seq` | yes | Monotonically-increasing per-log sequence number |
| `timestamp` | yes | RFC 3339 UTC; log operator's clock |
| `subject_nid` | yes | NID this entry is about |
| `incident` | yes | Incident type enum (see below) |
| `severity` | yes | `info` / `minor` / `moderate` / `major` / `critical` |
| `window` | no | Time window the observation covers (`start`, `end`) |
| `observation` | no | Free-form machine-readable incident detail |
| `evidence_ref` | no | URL to richer evidence blob |
| `evidence_sha256` | no | SHA-256 of the evidence blob for tamper detection |
| `issuer_nid` | yes | NID of the party asserting the incident (MAY equal `log_id`) |
| `signature` | yes | Ed25519 signature by `issuer_nid`'s private key over the JCS-canonical entry (signature field excluded) |

**Signing canonicalization**: JCS (RFC 8785) applied to the entry object with the `signature` field omitted. The log operator verifies the issuer's signature before appending, then re-signs the full entry to commit sequence number and timestamp. This dual-signature model means the operator cannot silently attribute entries to third parties.

---

## Severity ladder

| Value | Meaning |
|-------|---------|
| `info` | Informational; no enforcement implied |
| `minor` | Low-severity; context for policy evaluation |
| `moderate` | Meaningful violation; SHOULD trigger monitoring |
| `major` | Serious violation; SHOULD trigger rate limiting or admission downgrade |
| `critical` | Severe; SHOULD trigger rejection |

The recommended minimum rejection policy (AaaS L2-09) rejects on `cert-revoked >=minor` and on `rate-limit-violation` / `tos-violation` of `major` or higher within the last 30 days.

---

## Incident vocabulary

| Value | Meaning |
|-------|---------|
| `cert-revoked` | CA revoked the NID's certificate |
| `rate-limit-violation` | Sustained violation of published rate limits |
| `tos-violation` | Violated an AaaS gateway's published terms |
| `scraping-pattern` | Behavior matched scraping heuristics |
| `payment-default` | CGN / fiat payment default on committed transaction |
| `contract-dispute` | Contractual breach on an async NOP task, unresolved |
| `impersonation-claim` | A third party claims the subject NID is impersonating them |
| `positive-attestation` | Explicit positive signal (e.g., audit passed) |

Unknown values MUST be preserved by log operators and returned to queriers (forward compatibility).

---

## Phase 1 — HTTP API (submit and query)

Every log operator exposes:

```
POST /v1/log/entries
    Body: ReputationLogEntry JSON
    Returns: 201 + stored entry (server-assigned seq + timestamp)

GET  /v1/log/entries?nid=<subject_nid>&since=<seq>
    Returns: array of entries about subject_nid with seq > since
```

Both `nid` and `since` are optional; omitting them returns all entries.

---

## Phase 2 — Merkle integrity (alpha.4+)

```
GET /v1/log/sth
    Returns: SignedTreeHead
    {
      "log_id": "...",
      "tree_size": 42817,
      "timestamp": "2026-04-21T14:30:00Z",
      "sha256_root_hash": "hex-of-merkle-root",
      "signature": "ed25519:<base64url>"
    }

GET /v1/log/proof?seq=<n>
    Returns: InclusionProof
    {
      "seq": 42,
      "leaf_index": 41,
      "tree_size": 1000,
      "leaf_hash": "hex",
      "audit_path": ["hex", "hex", ...]
    }
```

Merkle structure mirrors RFC 9162 (Certificate Transparency v2): leaves are canonical entries; internal nodes are SHA-256 hashes; the STH commits to the current root and is signed by the `log_id` key. Inclusion proofs let any party verify that entry `seq` is in the current tree without downloading the full log.

---

## Phase 3 — STH Gossip Protocol (alpha.5+)

The gossip protocol enables cross-log consistency verification and fork detection.

### Gossip endpoint

Each log operator exposes:

```
GET /v1/log/gossip/sth
```

Response:

```json
{
  "own_sth": {
    "tree_size": 42817,
    "timestamp": "2026-05-01T10:00:00Z",
    "sha256_root_hash": "hex-of-merkle-root",
    "log_id": "nid:ed25519:<log-operator-pubkey>",
    "signature": "base64url(...)"
  },
  "peer_sths": [
    {
      "log_id": "nid:ed25519:<peer-pubkey>",
      "received_at": "2026-05-01T09:59:30Z",
      "sth": { "..." }
    }
  ]
}
```

`peer_sths` holds the most recent validated STH received from each configured peer. Clients can cross-check peer state without contacting each peer directly.

### Gossip push cycle

Log operators configured with a `peers` list run a background gossip cycle (default every 30 seconds):

1. **Fetch** `GET /v1/log/gossip/sth` from each peer.
2. **Verify** the peer's `own_sth.signature` against the peer's `log_id` public key.
3. **Monotonicity check**: peer's new `tree_size` MUST be >= last accepted `tree_size` for that `log_id`.
4. **Consistency proof** (SHOULD): when `tree_size` increases, fetch an RFC 9162 consistency proof from the peer and verify it before accepting the new STH.
5. **Cache** the accepted peer STH; serve it from `/v1/log/gossip/sth`.

### Fork detection

A `tree_size` regression (step 3 fails) is evidence of a fork attempt. The operator MUST:
- Emit `NIP-REPUTATION-GOSSIP-FORK` to local audit.
- Cease accepting STH updates from that peer until manually reviewed.

`NIP-REPUTATION-GOSSIP-SIG-INVALID` is raised when step 2 fails (peer STH signature invalid).

---

## Federation push (alpha.11)

In addition to the pull-style STH gossip above, `nps-ledger` (alpha.11+) supports a
**push-style federation** endpoint for forwarding batches of reputation entries between
federated registries:

```
POST /v1/log/federation/push
    Body: { entries: [ ReputationLogEntry, ... ] }
    Header: X-NPS-Forwarded-By: <comma-separated NID chain>
    Returns: 200 + per-entry accept/reject status
```

Forwarding follows the **NDP §9** federation-forwarding model. Each hop appends its NID to
the `X-NPS-Forwarded-By` header; an operator that sees its own NID already in the chain
drops the batch (loop detection). The maximum forwarding depth is **3 hops**; exceeding it
raises `NDP-FEDERATION-LOOP`. This lets `public-federated` registries propagate incidents
across a federation without re-injecting them in a cycle.

---

## ReputationLogClient SDK helper (alpha.7)

All six SDKs (Python / TypeScript / Go / Java / Rust / .NET) ship a **`ReputationLogClient`**
(RFC-0004 Phase 2, added in alpha.7) that wraps the log HTTP API: submitting and querying
entries, fetching and verifying the `SignedTreeHead`, fetching an `InclusionProof`, and
verifying it via the RFC 9162 Merkle fold. It validates the operator's dual Ed25519
signatures for you, so AaaS gateways can consult a log on agent admission without
reimplementing the Merkle/STH plumbing.

---

## `reputation_policy` in NWM (RFC-0004 §4.4)

Nodes that wish to enforce reputation checks declare a `reputation_policy` in their NWM:

```yaml
reputation_policy:
  required_logs: ["log:labacacia-primary"]
  reject_on:
    - { incident: "cert-revoked", severity: ">=minor" }
    - { incident: "rate-limit-violation", severity: ">=major", within_days: 30 }
    - { incident: "tos-violation", severity: ">=major", within_days: 30 }
    - { incident: "scraping-pattern", severity: ">=major", within_days: 30 }
```

The recommended default for L2 AaaS deployments is `{"require_log": "labacacia/nps-ledger:latest"}` with the reject rules above. Nodes omitting the field simply skip log consultation — the reputation system is advisory.

For hot paths, nodes SHOULD cache log query results with a short TTL (default 60 s) and refresh asynchronously. A hard `reject_on: cert-revoked` SHOULD be checked synchronously but can be satisfied by OCSP-stapling-like pre-fetch.

---

## Operating nps-ledger

`nps-ledger` is the private reference log operator that ships with NPS Cloud. Key operating parameters:

| Variable | Default | Purpose |
|----------|---------|---------|
| `NPSLEDGER_PORT` | `17440` | TCP port to bind |
| `NPSLEDGER_DATA_DIR` | *(current dir)* | Directory for the operator keypair and default SQLite file |
| `NPSLEDGER_SQLITE_PATH` | *(in-memory)* | SQLite database path; omit for ephemeral dev/test |
| `NPSLEDGER_LOG_ID` | *(derived from keypair)* | Override `log_id` in STH responses |
| `NPSLEDGER_PEERS` | *(empty)* | Comma-separated `host:port` list of gossip peers |
| `NPSLEDGER_GOSSIP_INTERVAL_S` | `30` | Gossip push-pull cycle interval (seconds; min 10, max 3600) |

On first boot, `nps-ledger` generates an operator Ed25519 keypair at `${NPSLEDGER_DATA_DIR}/operator.ed25519.pkcs8` (mode `0600`). The `log_id` is derived from the keypair fingerprint unless overridden.

**To join a gossip federation**, set `NPSLEDGER_PEERS` to a comma-separated list of peer endpoints.

No `nps-ledger` container image is published to any registry, so build one from the
repository first and run your local tag:

```bash
# Build once from the labacacia/NPS-Ledger checkout
git checkout v1.0.0-alpha.19
docker build -t labacacia/nps-ledger:1.0.0-alpha.19 .

# Then run with the gossip peers configured
docker run \
  -e NPSLEDGER_PEERS=log2.example.com:17440,log3.example.com:17440 \
  -e NPSLEDGER_GOSSIP_INTERVAL_S=30 \
  labacacia/nps-ledger:1.0.0-alpha.19
```

Peer operators must reciprocally add your endpoint to their `NPSLEDGER_PEERS` list. STH gossip is bidirectional.

---

## See also

- [Protocol NIP](./Protocol-NIP.md) — NIP spec, including NID identity and reputation entry shapes (§5.1.2)
- [Daemon nps-ledger](./Daemon-NPS-Ledger.md) — detailed nps-ledger daemon reference
- [Operator AaaS Profile](./Operator-AaaS-Profile.md) — L2-09 reputation_policy requirement and recommended default

---

*Last reviewed at suite version: v1.0.0-alpha.19*
