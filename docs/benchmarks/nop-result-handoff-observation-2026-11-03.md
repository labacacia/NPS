English | [中文](nop-result-handoff-observation-2026-11-03.cn.md)

# NOP authorized result-handoff observation — 2026-11-03

All16 kernel-v2 conditions pass: nine positive workflows at100/1000/100000rows and
seven expected failure scenarios. Positive DAGs return exact frozen oracle groups,
five actually read refund rows and two owner releases. The fixed reference's largest
conservative bound is275349504bytes (262.59375MiB), below512MiB. This qualifies
reference semantics/resources, not universal reliability, efficiency or NPS savings.

Measured source7c4d89006afa141c8576c8940a3b2cf4890983f6,
[PR209](https://github.com/labacacia/NPS-Dev/pull/209), was committed before capture.
The [closed packet](runs/nop-handoff-kernel-7c4d890-attempt-03/index.json) binds24
recipes, frozen oracle, exact toolchains/test/SDK hashes, dependency manifest, raw
observations, CPU/heap/affinity/PIDs, kernel records and costs. CI41576423537 all10jobs
pass, including.NET1638,94demo guards and14tool checks.

Four nodes: Alice's orders/refunds -> Bob's analysis -> Alice's report. Distinct real
mTLS/current NIP identities protect both gateway/receiver resource hops. Bob receives
exact snapshot/field/aggregate grants and is denied row reads/release; Alice reads
five rows, revokes grants and releases known results. Worker-role dispatch is LOCAL,
not remote NOP/AlignStream transport qualification.

The [preregistered method](nop-result-handoff-method.md) preserves actual4CPU,
335544320GCbytes,512MiB,120s and10ms. The [accounting ADR](../adr/2026-11-03-nop-reference-memory-accounting.md)
requires controller full-current-program-image VmHWM plus exact fixture full-lifetime
wait4 RSS peak, augmented by higher observed HWM. No baseline subtraction, exec/
clear_refs, forcedGC or pool reset occurs. Raw inherited SELF history stays diagnostic.

Reviewed frozen code never spawns subprocesses; all-thread scans see only the pair.
This trusted topology precondition is not proof against arbitrary transient forks.
All16cases retain a fixture procfs KeyError at exit: those diagnostics are incomplete.
Complete root/kernel/scope evidence independently passes under v2; neither old v1
resource failure is reclassified.

| Rows | Delegate JSON bytes | Align JSON bytes | Public TLS bytes | Gateway/receiver TLS bytes | Wall ms | Fixture CPU ms | Managed allocated bytes |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 100 | 4636 | 5388 | 36427 | 83884 | 1676.0594 | 1579.885 | 16631824 |
| 1000 | 4659 | 5422 | 36451 | 83907 | 1681.3120 | 1603.411 | 18653928 |
| 100000 | 4705 | 5490 | 36494 | 83956 | 3489.6045 | 3676.724 | 238259640 |

Medians of three fresh direct-fixture runs, including provisioning/startup/operations/
cleanup; not warm-service or comparative performance data. Local JSON is not remote
NOP wire traffic. Actual TLS counters include both directions, HTTP/TLS, denials and
cleanup but exclude TCP/IP packet overhead. Model-visible0 means no model requests,
not estimated tokens/fees/savings. [All cases](runs/2026-11-03-nop-handoff-kernel-cases.csv).

Counts below: node dispatch / source capture / resource handler / owner release /
unresolved create. No failed scenario executes a report.

| Fault | Counts | Parent certainty/error |
| --- | --- | --- |
| Missing grant | 3/2/4/2/0 | known_terminal, NWP-RESULT-NOT-FOUND |
| Revoked grant | 3/2/4/2/0 | known_terminal, NWP-RESULT-NOT-FOUND |
| Hidden amount field | 3/2/5/2/0 | known_terminal, NWP-RESULT-NOT-FOUND |
| Revoked worker NIP | 3/2/4/2/0 | known_terminal, NIP-CONTEXT-UNTRUSTED |
| Expired input | 3/2/2/0/0 | known_terminal, uniform NWP-RESULT-NOT-FOUND |
| Lost query terminal | 1/1/2/1/0 | indeterminate, NOP-EXECUTION-INDETERMINATE |
| Actual post-create audit503 | 1/1/1/0/1 | indeterminate, NOP-EXECUTION-INDETERMINATE |

Unknown mutations are not retried/compensated. The audit503 allocation retains its
original UUID/query/TTL for a separate owner decision. Cleanup never implies rollback
or turns an unknown parent into success. Duplicates dispatch nothing and retain the
first result. Runtime regressions separately cover caller/digest conflicts, stable
retry identity/original deadlines, bad/missing evidence, ignored cancellation/late
effects, finish/cancel races, restart, lost journal/schema, CAS, capacity and expiry.

Both complete v1 matrices remain resource failures:
[first](runs/nop-handoff-d4fbf5e-attempt-01/index.json),
[second](runs/nop-handoff-ce2e483-attempt-02/index.json). Each has16functional passes
and one exit-counter failure; all diagnostics/recipes remain immutable.

~~~bash
python3 tools/benchmarks/nop-result-handoff.py --packet docs/benchmarks/runs/nop-handoff-kernel-7c4d890-attempt-03
~~~

Standard Python checks all closed hashes, actual controls/PIDs/kernel formula,
fixed matrix, oracle/row provenance, exact failures and costs without SDK, credentials,
original ledger or network. It reruns neither model calls nor experiments.

D20-06 is COMPLETE for [experimental reference AC](../plans/2026-11-03-nop-reference-ac-evidence.md).
Legacy/default offers and unsupported callbacks/compensation/K-of-N/conditions/
preflight/native/remote/fullproduction-runtime/six-SDK paths remain unchanged.
Snapshots/grants stay process-local/issuer-pinned; task SQLite does not replicate
them. General adopter/security/registry/P20/beta/release gates remain;#134publishing
separate. No merge/projection/publication.

D06 adds no provider calls/charges. Shared DeepSeek conservative accounting remains
USD4.3990379 including historicalUSD3.20unknown, remainingUSD5.6009621 under original
USD10; upper bounds,not invoices. Next:D20-07 discovery/ownership,thenD20-08 integration.
