# Copyright 2026 INNO LOTUS PTY LTD
# SPDX-License-Identifier: Apache-2.0
"""Frozen local resource controls for the actual Agent and all its descendants.

No model calls, registry changes or global shell configuration. OS affinity and
.NET environment limits affect this process and its subsequently launched children.
RSS/VmHWM guard observations remain separate from the managed heap hard limit.
"""
from __future__ import annotations

import os
from pathlib import Path
import time
import threading

PROCESSORS = 4
HEAP_LIMIT = '0x14000000' # 320 MiB, the original benchmark control.
RSS_LIMIT = 512 * 1024 * 1024
WORKFLOW_SECONDS = 120
INTERVAL = .01
SPECIFICATION = dict(processor_count=PROCESSORS, gc_heap_hard_limit=HEAP_LIMIT,
    rss_limit_bytes=RSS_LIMIT, max_workflow_seconds=WORKFLOW_SECONDS,
    rss_sample_interval_seconds=INTERVAL,
    rss_scope='actual Agent process plus all live descendants; concurrent RSS and conservative live VmHWM sum')


def install() -> dict:
    available = sorted(os.sched_getaffinity(0))
    if len(available) < PROCESSORS:
        raise RuntimeError('The frozen four-CPU profile is unavailable; do not silently change it.')
    selected = available[:PROCESSORS]
    os.sched_setaffinity(0, selected)
    os.environ['DOTNET_PROCESSOR_COUNT'] = str(PROCESSORS)
    os.environ['DOTNET_GCHeapHardLimit'] = HEAP_LIMIT
    os.environ.pop('DOTNET_GCHeapHardLimitPercent', None)
    return dict(**SPECIFICATION, cpu_affinity=sorted(os.sched_getaffinity(0)))


def _terminal_or_gone(pid: int) -> bool:
    """Confirm an exiting descendant, never interpret missing live counters as zero."""
    for attempt in range(2):
        try:
            status = Path(f'/proc/{pid}/status').read_text()
        except (FileNotFoundError, ProcessLookupError):
            return True
        state = next((line.split(':', 1)[1].strip().split()[0]
            for line in status.splitlines() if line.startswith('State:')), None)
        if state in {'Z', 'X', 'x'}:
            return True
        if attempt == 0:
            time.sleep(.001) # Bounded kernel exit transition; a live missing counter still fails.
    return False


def actual_tree_usage(root: int) -> tuple[int, int, float, list[int]]:
    pending, seen = [root], set()
    rss = high_water = 0
    cpu = 0.0
    while pending:
        pid = pending.pop()
        if pid in seen:
            continue
        seen.add(pid)
        try:
            status = Path(f'/proc/{pid}/status').read_text()
            values = dict(line.split(':', 1) for line in status.splitlines() if ':' in line)
            if values.get('State', '').lstrip().startswith('Z'):
                continue
            # Missing counters on a live process fail closed rather than becoming zero/"passed".
            rss += int(values['VmRSS'].split()[0]) * 1024
            high_water += int(values['VmHWM'].split()[0]) * 1024
            stat = Path(f'/proc/{pid}/stat').read_text().rsplit(')', 1)[1].split()
            cpu += sum(int(value) for value in stat[11:13]) / os.sysconf('SC_CLK_TCK')
            children = Path(f'/proc/{pid}/task/{pid}/children').read_text()
            pending.extend(int(value) for value in children.split())
        except (FileNotFoundError, ProcessLookupError):
            if pid == root:
                raise
            continue # Kernel confirms the descendant disappeared during this sample.
        except (KeyError, ValueError, IndexError):
            # exit_mm can remove VmRSS before status/stat reads finish. Only a
            # second kernel observation of terminal/disappeared state permits skipping;
            # a live/unreadable process still fails closed, including the root itself.
            if pid != root and _terminal_or_gone(pid):
                continue
            raise
    if root not in seen or rss <= 0 or high_water <= 0:
        raise RuntimeError('Actual process-tree resource counters unavailable.')
    return rss, high_water, cpu, sorted(seen)


class ResourceGate:
    """A sampled guard with a finite wall deadline. Violations stay recorded and never reset."""
    def __init__(self, root: int | None = None, *, clock=time.monotonic, usage=actual_tree_usage):
        self.root = root if root is not None else os.getpid()
        self.clock, self.usage = clock, usage
        self.started = clock()
        self.peak = self.high_water = self.polls = 0
        self.processes: set[int] = set()
        self.violations: set[str] = set()
        self.counter_error_types: set[str] = set()
        self._lock = threading.Lock()

    def observe(self) -> None:
        with self._lock:
            self._observe_locked()

    def _observe_locked(self) -> None:
        try:
            rss, high_water, _, pids = self.usage(self.root)
        except Exception as error:
            self.counter_error_types.add(type(error).__name__)
            self.violations.add('resource-counters-unavailable')
            raise RuntimeError('Actual resource counters unavailable; qualification failed closed.') from None
        self.peak, self.high_water = max(self.peak, rss), max(self.high_water, high_water)
        self.polls += 1
        self.processes.update(pids)
        if rss > RSS_LIMIT or high_water > RSS_LIMIT:
            self.violations.add('512MiB-actual-process-tree-RSS-or-live-VmHWM')
        if self.clock() - self.started >= WORKFLOW_SECONDS:
            self.violations.add('120s-workflow-deadline')

    def require(self) -> float:
        self.observe()
        if self.violations:
            raise RuntimeError('Frozen workflow resource guard failed; no subsequent paid request is allowed.')
        return WORKFLOW_SECONDS - (self.clock() - self.started)

    def report(self) -> dict:
        return dict(specification=SPECIFICATION, sampled_process_tree_peak_rss_bytes=self.peak,
            live_process_high_water_sum_guard_bytes=self.high_water, poll_count=self.polls,
            process_ids=sorted(self.processes), elapsed_seconds=self.clock() - self.started,
            violations=sorted(self.violations), counter_error_types=sorted(self.counter_error_types), passed=self.polls > 0 and not self.violations)
