"""Linux kernel RSS accounting for a trusted, frozen controller/fixture pair.

This is not a generic descendant-tree monitor. The fixture's reviewed code must
not spawn subprocesses. All-thread scans reject every observed scope expansion.
"""
from __future__ import annotations
import os
from pathlib import Path
import resource
import signal
import sys
import time

LIMIT = 512 * 1024 * 1024
SECONDS = 120
INTERVAL = .01
ACCOUNTING = 'linux-controller-mm-and-exact-wait4-peak-v1'


def positive_integer(value):
    return type(value) is int and value > 0


def all_thread_children(pid, *, may_have_exited=False):
    try:
        threads = list(Path(f'/proc/{pid}/task').iterdir())
    except (FileNotFoundError, ProcessLookupError):
        if may_have_exited:
            return set()
        raise
    if not threads and not may_have_exited:
        raise RuntimeError('Live root thread scope unavailable.')
    children = set()
    for thread in threads:
        try:
            children.update(int(value) for value in (thread/'children').read_text().split())
        except (FileNotFoundError, ProcessLookupError):
            continue # This individual thread exited; never infer process memory from it.
    return children


def procfs_memory(pid):
    fields = dict(line.split(':',1) for line in Path(f'/proc/{pid}/status').read_text().splitlines() if ':' in line)
    return tuple(int(fields[name].split()[0])*1024 for name in ('VmRSS','VmHWM'))


def wait_exact_child(pid,budget):
    try:
        return os.wait4(pid,os.WNOHANG)
    except ChildProcessError:
        budget.violations.add('exclusive-wait4-ownership-lost')
        raise


def signal_owned_fixture(budget):
    # An ECHILD PID is no longer known to be ours and may have been reused.
    if 'exclusive-wait4-ownership-lost' in budget.violations or budget.reaped:
        return False
    try:
        os.kill(budget.fixture,signal.SIGKILL)
    except ProcessLookupError:
        pass
    return True


class KernelPairBudget:
    def __init__(self, fixture_pid, *, started=None, clock=time.monotonic):
        if sys.platform != 'linux' or not hasattr(os,'wait4'):
            raise RuntimeError('This kernel-accounting method requires Linux wait4.')
        if not positive_integer(fixture_pid) or fixture_pid == os.getpid():
            raise ValueError('Exactly one distinct direct fixture process is required.')
        self.root, self.fixture = os.getpid(), fixture_pid
        self.clock = clock
        self.started = clock() if started is None else started
        self.violations = set()
        self.diagnostic_errors = set()
        self.controller_kib = self.fixture_kib = 0
        self.controller_lifetime_kib = 0
        self.observed_root_hwm = self.observed_fixture_hwm = self.sampled_peak = 0
        self.polls = self.scope_checks = 0
        self.observed_pids = {self.root, self.fixture}
        self.waited_pid = self.wait_status = self.wait_returncode = None
        self.reaped = False

    def _controller_peak(self):
        try:
            _,hwm=procfs_memory(self.root)
            if not positive_integer(hwm) or hwm%1024:
                raise ValueError('Current-image controller HWM invalid.')
            self.controller_kib=max(self.controller_kib,hwm//1024)
        except Exception as error:
            self.violations.add('controller-current-image-HWM-unavailable')
            self.diagnostic_errors.add('controller-required:' + type(error).__name__)
        try:
            # SELF usage survives execve and may contain a much larger launcher
            # image. Preserve it visibly, but never subtract it from live HWM.
            inherited=resource.getrusage(resource.RUSAGE_SELF).ru_maxrss
            if not positive_integer(inherited): raise ValueError()
            self.controller_lifetime_kib=max(self.controller_lifetime_kib,inherited)
        except Exception as error:
            self.diagnostic_errors.add('inherited-self-diagnostic:' + type(error).__name__)

    def observe(self, *, fixture_terminal=False):
        self._controller_peak()
        try:
            direct = all_thread_children(self.root)
            nested = all_thread_children(self.fixture,may_have_exited=True)
            extra = (direct - {self.fixture}) | nested
            self.observed_pids.update(direct | nested)
            self.scope_checks += 1
            if extra:
                self.violations.add('unexpected-process-scope')
        except Exception as error:
            self.violations.add('process-scope-unavailable')
            self.diagnostic_errors.add('scope:' + type(error).__name__)
        values = {}
        for role,pid in (('controller',self.root),('fixture',self.fixture)):
            if role == 'fixture' and fixture_terminal:
                continue
            try:
                rss,hwm = procfs_memory(pid)
                if not positive_integer(rss) or not positive_integer(hwm):
                    raise ValueError('Nonpositive diagnostic counter.')
                values[role] = rss
                if role == 'controller':
                    self.observed_root_hwm = max(self.observed_root_hwm,hwm)
                else:
                    self.observed_fixture_hwm = max(self.observed_fixture_hwm,hwm)
            except Exception as error:
                # Primary proof is full-lifetime kernel accounting, not a replacement
                # zero value. Every incomplete procfs diagnostic remains visible.
                self.diagnostic_errors.add(role + ':' + type(error).__name__)
                if role=='controller': self.violations.add('controller-current-image-HWM-unavailable')
        if len(values) == 2:
            self.sampled_peak = max(self.sampled_peak,sum(values.values()))
        self.polls += 1
        if self.sampled_peak > LIMIT or self._bound() > LIMIT:
            self.violations.add('512MiB-conservative-fixed-pair-RSS')
        if self.clock()-self.started >= SECONDS:
            self.violations.add('120s-original-case-deadline')

    def _bound(self):
        return max(self.controller_kib*1024,self.observed_root_hwm) + max(self.fixture_kib*1024,self.observed_fixture_hwm)

    def remaining(self):
        return max(0.0,SECONDS-(self.clock()-self.started))

    def finish(self, waited_pid, status, usage):
        self.waited_pid = waited_pid
        self.wait_status = status
        if waited_pid != self.fixture or type(status) is not int or usage is None:
            self.violations.add('exact-child-kernel-accounting-incomplete')
            return
        peak = usage.ru_maxrss
        if not positive_integer(peak):
            self.violations.add('kernel-fixture-peak-unavailable')
            return
        self.fixture_kib = peak
        self.wait_returncode = os.waitstatus_to_exitcode(status)
        self.reaped = True
        self.observe(fixture_terminal=True)
        if self._bound() < self.sampled_peak:
            self.violations.add('kernel-pair-bound-below-observed-RSS')

    def report(self):
        self._controller_peak()
        if not self.reaped:
            self.violations.add('exact-child-kernel-accounting-incomplete')
        if self._bound() > LIMIT:
            self.violations.add('512MiB-conservative-fixed-pair-RSS')
        elapsed = self.clock()-self.started
        if elapsed >= SECONDS:
            self.violations.add('120s-original-case-deadline')
        return dict(accounting=ACCOUNTING,input_units='Linux KiB',root_pid=self.root,fixture_pid=self.fixture,
            waited_pid=self.waited_pid,wait_status=self.wait_status,wait_returncode=self.wait_returncode,
            wait4_complete=self.reaped,controller_peak_source='proc-self-status-VmHWM-current-program-image',
            controller_image_scope='from controller program entry; no exec or clear_refs reset during capture',
            controller_rusage_lifetime_peak_kib=self.controller_lifetime_kib,controller_peak_rss_kib=self.controller_kib,fixture_peak_rss_kib=self.fixture_kib,
            kernel_peak_sum_bytes=(self.controller_kib+self.fixture_kib)*1024,
            controller_baseline_subtracted=False,observed_controller_hwm_bytes=self.observed_root_hwm,
            observed_fixture_hwm_bytes=self.observed_fixture_hwm,conservative_pair_peak_bound_bytes=self._bound(),
            sampled_pair_peak_rss_bytes=self.sampled_peak,procfs_diagnostic_complete=not self.diagnostic_errors,
            diagnostic_errors=sorted(self.diagnostic_errors),scope_checks=self.scope_checks,poll_count=self.polls,
            process_ids=sorted(self.observed_pids),elapsed_seconds=elapsed,violations=sorted(self.violations),
            passed=self.reaped and self.polls>0 and self.scope_checks>0 and not self.violations)
