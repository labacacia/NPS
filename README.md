English | [中文](README.cn.md)

# NPS — Neural Protocol Suite

**1.0.0-alpha.20 release candidate — unpublished.**

NPS provides NWP request/result resources, NIP identity and authorization, NCP
transport, NOP orchestration and NDP discovery. This candidate closes a bounded
experiment in data workflows. It does not qualify full production or beta.

- .NET and Python ResultHttpClient use the .NET HTTP reference with bounded,
  authorized results and real mTLS evidence at100/1000/100000 rows.
- .NET native repeated-schema batches have JSON and MessagePack reference tests.
- Local four-worker NOP handoff and two volatile NDP transfer configurations have
  exact fault/recovery evidence. Remote production orchestration is not qualified.
- Six SDK helper/parser/regression surfaces are separate from these runtime paths.
  Other result/native paths and the general Python NwpClient remain unqualified.

Read the [support ledger](docs/qualification/alpha20-support-2026-11-03.md),
[accepted scope](docs/adr/2026-11-03-alpha20-experimental-closeout.md),
[experimental evidence](docs/plans/2026-11-03-alpha20-experimental-ac-evidence.md)
and [specifications](spec/NPS-0-Overview.md) before choosing a path.

Equivalent HTTPgzip and ordinary-tool comparisons did not establish a universal
NPS token, fee, latency or transport advantage; the measured negative results stay
visible. Historical production/beta obligations are not counted as passed.

Candidate branches and local archives are review inputs. No installation command
here implies that alpha.20 already exists in a public registry. Actual releases
need the source-bound candidate/preflight, registry authority and publication
record tracked by [#134](https://github.com/labacacia/NPS-Dev/issues/134).

Source is authored in NPS-Dev and projected through its release ownership tooling.
The release projections are NPS, six SDKs, NPS-Daemons and NIP-CA-Server.
Model-Runtime, Console and Paper retain independent lifecycles.
