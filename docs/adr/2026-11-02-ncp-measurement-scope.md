# ADR: NCP scope after paired HTTP observations

- Status: Accepted experimental scope constraint; native NCP qualification remains D20-04.
- Date: 2026-11-02.
- Trace: D20-01 #165, D20-04 #168, Epic #163.
- Authority: user-authorized dependency-ordered implementation and the D20-00 frozen
  methodology's negative-result decision rule; pre-1.0 author-reviewed change process.

## Evidence and context

The [recorded Release HTTP experiment](../benchmarks/runtime-http-observation-2026-11-02.md)
at a4d80af observed the NPS two-hop gateway prototype slower and larger than ordinary
HTTPS using the same source/store/arithmetic operators. It passed exact answers,
bounded evidence and the declared deployment RSS ceiling. It was not a native NCP
bulk-transfer experiment. Additional gateway authentication, fresh projections,
per-operation discovery and downstream connections are part of that measured topology.

The subsequent [1860c19 schema/update run](../benchmarks/schema-update-http-observation-2026-11-02.md)
passes all 144 configurations/4320 measured workflows at 487.16MiB peak in the same
declared deployment, after retaining two negative attempts and reducing shared-store
capture allocations. All 72 paired conditions still show higher median latency and
TLS-stream bytes for this NPS topology. That strengthens the scoped negative HTTP
finding; it remains separate from native NCP or protocol-specific memory attribution.
The [3b624e4 CPU run](../benchmarks/process-cpu-observation-2026-11-02.md) repeats all 144
configurations with actual process CPU. D has higher CPU median in 71/72 pairs; the
one lower median and its broad distribution remain visible. All paired wall/byte
medians are higher on D. These are co-host/topology observations, not a native codec cause.

[Actual bounded-evidence model reproduction](../benchmarks/deepseek-cd-observation-2026-11-02.md)
observed identical C/D usage and exact replies: moving computation out of context is
already possible over ordinary HTTP. The model reproduced runtime-computed JSON;
this does not establish a live tool Agent or an NCP billing advantage. Earlier A/B
wrong answers/context failures remain failures rather than an equivalent correct-cost
denominator. A wire representation ratio cannot establish provider token savings.

## Decision

Retain NCP's existing schema-bound batch/stream/delta responsibility, with no automatic
model-token or provider-cost claim. D20-04 will qualify its declared existing semantics,
backpressure, missing-anchor/base errors and explicit resynchronization. Qualify the
reference behavior and measure any additional native transport value before broad
SDK porting under D20-08. Do not attribute gateway overhead or C's external-compute
benefit to native NCP. No new frame or compression format follows from these observations.

This HTTP workstream explicitly excludes native NCP superiority and Arrow/Flight/
Protobuf superiority comparisons. Any future comparative native efficiency claim
needs an appropriate complete-framing baseline and equivalently correct work under
its preregistered conditions. Schema/update HTTP measurements cover fresh snapshots
after a controlled change; they do not qualify delta propagation or live streams.

Product value assessed in later packages can include interoperable result identity,
bounded disclosure, execution certainty and recovery, even if transport overhead
remains. Performance claims will identify their route/deployment/revision and observed
distributions. A deployment GC budget is not protocol-specific memory savings.

## Alternatives and consequences

Optimizing the current gateway can be evaluated independently after measuring its
cost, but cannot make the recorded old revision faster retroactively. Expanding native
ports before a qualified reference would multiply uncertain behavior across SDKs.
Keeping transport/value claims bounded lets the existing D20-04 qualification proceed
when D20-01 AC are complete, without claiming a predetermined win.

No gate, frozen vector, historical 30%/50% obligation, security check, suite publication
decision or beta requirement is removed or lowered. D20-01's benchmark/evidence AC
are complete after the full schema/update/CPU observations and claim reconciliation. This decision
does not change repository roles, release boundaries or protocol versions.
