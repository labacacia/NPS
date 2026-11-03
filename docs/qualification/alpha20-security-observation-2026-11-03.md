English | [中文](alpha20-security-observation-2026-11-03.cn.md)

# D20-08 dependency correction evidence

Status: IN_PROGRESS pending clean-commit CI; all listed local dependency audits have passed. This is
one integration increment, not an alpha.20 release or full security qualification.
Baseline: D07 source `2023e0b`; CI41592588893 passed all ten normal jobs, while Security
Gates41592357004 failed npm, NuGet, Go, Maven and RustSec. Registry authority was skipped;
PyPI and workflow/history checks passed. Normal CI success did not waive these failures.

| Area | Correction and boundary |
|---|---|
| .NET | MessagePack 3.1.9; SQLitePCLRaw bundle 3.0.5 across SDK and daemon source/publish overlays. Existing frozen codecs and APIs remain unchanged. |
| TypeScript | Vitest 4.1.12 with patched mocker/Vite/source-map graph; CA example Fastify 5.12.6 and fast-uri fixes. Both SDK and example dependency graphs are audited, including development dependencies. |
| Java | Jackson 2.18.11 and aligned BC 1.86. Boot 4.0.9/Spring 7.0.10 for the CA example; explicit patched Jackson 2 signing/storage alongside Boot's Jackson 3 web stack. Java 21 remains; Gradle 8.14.3 has the official distribution checksum pinned. |
| Rust | Refresh locked Quinn 0.11.19, rustls 0.23.46, webpki 0.103.16, roots 1.0.11 and Tokio 1.54.1; CA example Tokio follows the patched graph. Existing downstream lockfiles need their own updates. |
| Go | CI/security and the CA builder use Go 1.26.10. The Go 1.23 language floor is preserved; it is not a claim that an old Go runtime is patched. Reachability results are reported separately from required-module advisories. |

Upstream evidence: [MessagePack advisory](https://github.com/MessagePack-CSharp/MessagePack-CSharp/security/advisories/GHSA-qhrr-8q5h-9q3h),
[Vitest advisory](https://github.com/vitest-dev/vitest/security/advisories/GHSA-qxg6-89mg-rqv8),
[Spring MVC advisory](https://github.com/advisories/GHSA-pc63-qcmh-9cmg),
[Boot migration guide](https://github.com/spring-projects/spring-boot/wiki/Spring-Boot-4.0-Migration-Guide),
[Go x509 record](https://pkg.go.dev/vuln/GO-2026-6809),
[Rustls](https://rustsec.org/advisories/RUSTSEC-2026-0349.html),
[webpki](https://rustsec.org/advisories/RUSTSEC-2026-0382.html),
[roots](https://rustsec.org/advisories/RUSTSEC-2026-0381.html),
[Tokio](https://rustsec.org/advisories/RUSTSEC-2026-0386.html).
Spring 6.2's recorded findings have no public patched version in that line; forcing
Spring 7 into Boot 3's graph was not used as a shortcut.

Executed local verification so far: .NET1666/1666 and runner46/46, TypeScript1324/1324,
Java744/744, Rust765/765, Go SDK tests; six resolved NuGet target graphs have zero reported
advisories/warnings, both Maven runtime graphs have zero OSV findings, both npm graphs
have zero reported advisories. Go SDK govulncheck reports zero reachable vulnerabilities
and zero vulnerable imported packages, while retaining 20 advisories in required modules
whose vulnerable code is not called. These are scanner-specific results, not a blanket
absence-of-vulnerabilities claim. Rust SDK/CA audits report zero vulnerabilities and warnings. CA-Go also reports zero reachable/imported-package findings, with 22 required-module advisories retained. Clean-commit remote gates remain pending.

The Java CA's actual HTTP test checks unauthorized registration before storage, nested
Unicode/integer signature compatibility using an independent JDK verifier, duplicate
registration, lookup and HTTP v2 X.509 chain issuance/leaf/root/public-key checks. A built
Boot jar separately starts with its embedded schema and passes health/discovery/401.
This exposed two pre-existing startup defects: an unclosed SQLite WAL result set and
filesystem-only classpath schema loading. Both were corrected and independently reviewed.
The initial missing-Jackson2 compilation and subsequent SQLite startup failure remain
recorded; fixes were verified, not bypassed. A further actual check found old key envelopes were mode000: fix POSIX0600/fallback ordering and verify restart of the same key/SQLite state with public-key/serial continuity. No production TLS/PKI claim follows.
The pre-existing permissive blank-operator-key behavior is explicitly outside the tested
configured-key admission boundary.

Local npm9's resolver failed with edgesOut; a task-local npm11.11.0 generated the lockfile
and installed the declared graph successfully. There was no global npm replacement or
advisory suppression. Rust/Go build tools were downloaded to the external task artifact
directory and verified against official SHA-256 metadata, without changing shell profiles.
NuGet reports now print only package/advisory metadata before the unchanged zero-finding
gate, making failures diagnosable. No scanner ignores, thresholds or default feature offers
were added. No paid provider requests, release projections, tags or publication occurred.

Next: finish the remaining audits/CI, publish exact source/evidence receipts, and continue
D20-08 support/compatibility/old-gate/drift/package handoff work. Registry credential
inventory, external owner/adopter decisions, other runtime bindings, beta and publication
requirements remain unresolved until their own evidence/authority closes them.

[Validated local source/test/audit receipt](2026-11-03-alpha20-security-tests.json).

Clean source535d0f7 dependency Security Gates41597419804 passed all seven scanners;
registry authority was skipped on the pull-request event. First normal CI41597571436
passed nine jobs but the final Java packaged probe failed because the runner lacked
rg; SDK/CA tests and bootJar had already passed. The corrected Python standard-library
selector passes an actual directory-based jar probe and rejects empty/ambiguous
directories before creating state. [Preserved failure and correction receipt](2026-11-03-java-ca-portable-probe.json).
Corrected-source CI remains pending. Manual registry preflight separately fails its
missing-credential inventory; this is not waived by the seven scanner passes.

Corrected source `92bf98e` passed all 10 normal CI jobs ([41599401714](https://github.com/labacacia/NPS-Dev/actions/runs/41599401714)) and 7 scanner jobs ([41599410395](https://github.com/labacacia/NPS-Dev/actions/runs/41599410395)). Registry authority was skipped on the PR; the prior manual inventory failure remains unresolved. These runs do not prove publishing authority.
