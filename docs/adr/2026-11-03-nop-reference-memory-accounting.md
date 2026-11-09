# ADR: Fixed-reference NOP memory accounting

- Status: Accepted experimental measurement method; D20-06 #170 / Epic #163.
- Date: 2026-11-03.
- Scope: trusted frozen controller program plus one direct .NET fixture process.
- Predecessor: [actual workflow scope](2026-11-03-data-workflow-agent-scope.md).

Two complete v1 matrices pass all16functional assertions but each loses one procfs
memory observation around fixture exit. Both remain resource failures. The1ms
terminal-confirmation correction passes short-child diagnostics but does not remove
the .NET exit race. Do not publish a lucky replacement case, extend delays until a
pass, or lower the original resource requirement.

Use a new preregistered method. Require the controller's full current-program-image
VmHWM and the exact fixture PID's complete-lifetime wait4 ru_maxrss, including shutdown.
Augment each with any higher valid live HWM and add the two maxima conservatively.
The sum bounds simultaneous reported RSS for this fixed pair. Original4CPU/320MiB
managed/512MiB RSS/120s/10ms controls remain. No controller baseline, forcedGC, pool
or high-water reset is subtracted. No exec/clear_refs reset is allowed after controller
entry. Read the root HWM again after child reaping and at case completion.

Linux retains getrusage usage across execve; a launch diagnostic observed an inherited
SELF maximum around2.8GiB while the Python image's VmHWM was28MiB. Preserve that raw
SELF lifetime value separately; it is not the current executable image's measured
memory. This is an explicit temporal boundary, not numerical baseline subtraction.
[Linux usage](https://man7.org/linux/man-pages/man2/getrusage.2.html),
[current process fields](https://man7.org/linux/man-pages/man5/proc_pid_status.5.html),
[exact-child wait4](https://man7.org/linux/man-pages/man2/wait4.2.html).

Exactly one wait4 owner reaps the known fixture PID. Do not use Popen.poll/wait/
communicate/context-manager teardown or its kill/terminate/send_signal methods while
unreaped: signaling methods internally poll. Signal only the owned unreaped PID with
os.kill, then reap through wait4 and set Popen.returncode immediately. Wrong/missing
PID, units, peaks, status, incomplete reaping, root HWM/scope failure, exceeded budget
or original deadline fails qualification. Failed cleanup is bounded and never renews
the qualifying deadline.

The source-reviewed fixture never spawns subprocesses; worker roles, Kestrel nodes and
data operations run in-process. Scan children across every thread and reject observed
scope expansion or unavailable scope evidence. Polling cannot prove absence of a
short-lived fork. This method qualifies this trusted fixed pair only; an unconditional
arbitrary-tree assertion would need stronger enforcement/accounting such as cgroups.
No generalized daemon/adopter resource gate receives a pass.

Keep10ms concurrent RSS observations as diagnostics. Child procfs gaps remain visible
and incomplete; they cannot replace missing wait4 accounting. Root HWM and process
scope remain mandatory. Require the conservative bound to cover all observed concurrent
RSS. The independent verifier checks formulas, exact PID/status/units, unchanged limits,
scope/affinity, frozen oracle/provenance and all16specific outcomes.

Both earlier matrices, diagnostic candidates, failed guards and exact scientific
recipes remain immutable. The v2 source must be committed before its complete matrix.
Default offers, protocol/repository boundaries, all-SDK/native/remote qualification,
security/registry/P20 gates and publishing remain unchanged. D20-06 stays IN_PROGRESS
until executed v2 evidence and remaining AC pass.

## Executed decision

Committed7c4d890 kernel-v2 completes all16conditions with complete exact-PID accounting,
mandatory root HWM/current image, actual4CPU/335544320GCbytes and original limits.
Largest conservative bound275349504bytes; every child exit procfs diagnostic gap is
retained as incomplete, not converted into a procfs pass. Both v1matrices remain
failed and immutable. [Observation](../benchmarks/nop-result-handoff-observation-2026-11-03.md)
and [reference AC](../plans/2026-11-03-nop-reference-ac-evidence.md) record the decision.

Accept D20-06 experimental reference AC and continue authorized D20-07/08. The trusted
pair/current-image scope is not enlarged; generic/daemon/adopter resource obligations,
remote/native/fullruntime/six-SDK support and security/registry/P20/publishing remain
separate. No provider calls, default offer, merge, release projection or publication.
