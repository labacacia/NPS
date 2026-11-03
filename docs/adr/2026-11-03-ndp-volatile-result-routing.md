# D20-07: opt-in volatile result route admission

Status: accepted experimental reference decision; [D20-07 AC evidence](../plans/2026-11-03-ndp-reference-ac-evidence.md) COMPLETE. Clean source `02443a3` passes CI41591866781 all ten jobs. Continue D20-08; wider production/durability/security gates remain unwaived.

An announcement signature proves possession, not permission to claim a cluster or a
successor endpoint. The selected .NET reference component therefore resolves CURRENT
host identity/ownership policy independently on admission and each use. The policy
binds the publisher key, cluster, anchor role, positive epoch, exact HTTPS endpoint,
certificate pin, revision and expiry before sequence/epoch fences can advance.
The existing legacy registry APIs and daemon defaults are unchanged.

Keep at most 256 publisher tombstones without eviction. Reject sequence/digest
conflicts and lower epochs. Select the unique highest recorded owner before
capability filtering. Missing, expired, draining, unauthorized or incapable highest
owners cannot cause fallback to an older owner. Equal-epoch conflict remains denied
until a higher authorized epoch, including after expiry. This is deliberately stricter
than choosing between expired conflicting records.

Signed timestamp+TTL, receipt+TTL, current identity expiry and a monotonic receipt
lease bound freshness. Unsigned last_seen can only shorten expiry; unsigned draining
can only add a veto at the same signed sequence. A higher signed sequence is needed
to clear that veto. Exact retransmission never renews the lease.

A selection binds registry generation, admitted record, publisher, endpoint, epoch,
sequence and current identity policy. Keep this binding with every retained result;
validate immediately before each operation. Changed admission requires a new client,
fresh authenticated NWM feature-revision discovery and current receiver NIP admission.
Coarse announcement capabilities never authorize an operation. The final receiver
remains authoritative across the unavoidable discovery/dispatch race.

Results remain process-local and issuer pinned. A successor or an empty restarted
store cannot reconstruct an old result. Report unavailable state and retain any
uncertain original mutation key/query/TTL for explicit owner recovery; do not create
a replacement result or replay mutations automatically. Data migration, durable
registry recovery/federation, remote/native NOP transport and six-SDK parity are not
qualified by this reference increment. Cold registry restart invalidates selections
and requires current host ownership policy; it cannot recover forgotten history.

Verification: bounded signed-announcement fault tests plus two distinct actual HTTPS
result nodes using the existing public ResultHttpClient, two-leg mTLS, CURRENT NIP,
NWM and result policies. Static endpoint use remains independently testable.
