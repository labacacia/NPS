#!/usr/bin/env python3
# Copyright 2026 INNO LOTUS PTY LTD
# SPDX-License-Identifier: Apache-2.0
"""Opt-in real DeepSeek tool-Agent matrix; default invocation makes no paid requests."""
from __future__ import annotations

import argparse
import asyncio
import dataclasses
import hashlib
import json
import os
from pathlib import Path
import subprocess
import time
import uuid

import httpx

from nps_sdk.nwp import ResultHttpError

from agent import SYSTEM, TOOLS, execute
from provider import DeepSeekProvider, SharedLedger, StopExperiment, canonical, sha
from workflow import client, transport
from resource_profile import ResourceGate, install, INTERVAL, SPECIFICATION, actual_tree_usage

DEMO = Path(__file__).resolve().parent


def atomic_record(path, value):
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_bytes(canonical(value) + b'\n')
    temporary.replace(path)


def conditions(method):
    prefix = method.get('case_prefix', 'd05-tool-v1')
    return [dict(rows=rows, revision=revision, repeat=repeat, cache=cache, arm=arm,
        case_id=f'{prefix}:{rows}:{revision}:{repeat}:{cache}:{arm}')
        for rows in method['row_counts'] for revision in (1, 2) for repeat in range(method['repeats'])
        for cache in ('cold-requested', 'warm-requested') for arm in ('ordinary', 'nps')]


def verify_method():
    method = json.loads((DEMO / 'live-method.json').read_text())
    if method['system_sha256'] != hashlib.sha256(SYSTEM.encode()).hexdigest() or method['tools_sha256'] != sha(TOOLS):
        raise StopExperiment('Prompt or tools differ from the preregistered method.')
    if method['workflow_count'] != len(conditions(method)):
        raise StopExperiment('Live matrix cardinality changed.')
    paths = ('agent.py','provider.py','workflow.py','live-run.py','live-method.json','oracle.json','resource_profile.py',
        'server/NPS.Demo.DataWorkflow.csproj','server/Program.cs','server/DemoAuthority.cs','server/DemoHost.cs','server/OrderSource.cs')
    source = [dict(path=name,archive_path=f'recipe-{index:02d}'+Path(name).suffix,
        sha256=hashlib.sha256((DEMO/name).read_bytes()).hexdigest()) for index,name in enumerate(paths)]
    if method.get('resource_profile') != SPECIFICATION:
        raise StopExperiment('Controlled method differs from frozen resource configuration.')
    return dict(method=method, source_files=source)



def exact_oracle(rows, revision):
    """Frozen baseline answers plus independently counted deterministic update increments; never sent to the model."""
    reference = json.loads((DEMO / 'oracle.json').read_text())['reference_answers'][str(rows)]
    groups = {reason: {period: dict(value[period]) for period in ('baseline', 'current')}
        for reason, value in reference['groups'].items()}
    if revision == 2:
        reasons = ('delivery', 'quality', 'duplicate', 'other')
        for index in range(rows // 100):
            period, reason = ('baseline' if index % 100 < 50 else 'current'), reasons[index % 4]
            refunded = index % 100 < 10 or period == 'current' and reason == 'delivery' and index % 100 < 80
            if refunded:
                groups[reason][period]['refund_amount_minor'] += 100
    return dict(groups=groups, anomaly_reasons=reference['anomaly_reasons'],
        expected_citation_ids=reference['groups']['delivery']['sample_current_refund_ids'])


async def run_case(provider, configuration, condition, output, *, resource_enforced=True):
    if resource_enforced and (len(os.sched_getaffinity(0)) != 4
            or os.environ.get('DOTNET_PROCESSOR_COUNT') != '4'
            or os.environ.get('DOTNET_GCHeapHardLimit') != '0x14000000'):
        raise StopExperiment('The frozen four-CPU/managed-heap controls must be installed before live execution.')
    path = output / (condition['case_id'].replace(':', '_') + '.json')
    if path.exists():
        raise StopExperiment('Existing workflow capture must not be replayed/overwritten.')
    wire = dict(request_body_bytes=0, response_body_bytes=0, requests=0)
    class CountingStream(httpx.AsyncByteStream):
        def __init__(self, stream): self.stream = stream
        async def __aiter__(self):
            async for chunk in self.stream:
                wire['response_body_bytes'] += len(chunk)
                yield chunk
        async def aclose(self): await self.stream.aclose()
    async def request_hook(request):
        wire['requests'] += 1
        wire['request_body_bytes'] += len(request.content)
    async def response_hook(response):
        response.stream = CountingStream(response.stream)
    gate, active = ResourceGate(), True
    before_cpu = actual_tree_usage(os.getpid())[2]
    async def sample():
        while active:
            gate.observe()
            await asyncio.sleep(INTERVAL)
    sampler = asyncio.create_task(sample())
    previous_guard = getattr(provider,'guard',None)
    provider.guard = gate.require if resource_enforced else previous_guard
    started = time.monotonic()
    result = None
    def record(summary):
        atomic_record(path, dict(**summary, local_http=wire, oracle_expected=exact_oracle(condition['rows'], condition['revision'])))
    try:
        async with transport(configuration) as http:
            http.event_hooks = {'request': [request_hook], 'response': [response_hook]}
            result = await execute(provider, client(configuration, http, condition['arm']),
                case_id=condition['case_id'], rows=condition['rows'], revision=condition['revision'],
                nonce=uuid.uuid4().hex, cache=condition['cache'], record=record)
            expected = exact_oracle(condition['rows'], condition['revision'])
            answer = result['answer']
            oracle_match = result['exact_and_faithful'] and (
                canonical(answer['groups']) == canonical(expected['groups'])
                and answer['anomaly_reasons'] == expected['anomaly_reasons']
                and [row['id'] for row in answer['citations']] == expected['expected_citation_ids'])
            result['independent_oracle_match'] = oracle_match
            if not oracle_match:
                result['status'] = 'INCORRECT_OR_INVALID_ANSWER'
            record(result)
    finally:
        active = False
        await sampler
        gate.observe()
        provider.guard = previous_guard
        state = json.loads(path.read_text()) if path.exists() else dict(case_id=condition['case_id'], status='INTERRUPTED')
        state.update(local_http=wire, local_metrics=dict(
            elapsed_ms=round((time.monotonic() - started) * 1000, 3),
            sampled_agent_plus_server_rss_bytes=gate.peak, rss_poll_count=gate.polls,
            agent_plus_server_cpu_seconds=actual_tree_usage(os.getpid())[2] - before_cpu,
            scope='actual Agent and all live descendants; sampled RSS and conservative live VmHWM guard; excludes provider compute/TLS/header bytes'),
            resource_guard=dict(enabled=resource_enforced, **gate.report()))
        atomic_record(path, state)
    if resource_enforced and not state['resource_guard']['passed']:
        raise StopExperiment('Frozen workflow resource gate failed; capture retained.')
    print(json.dumps(dict(case_id=condition['case_id'], status=state['status'],
        model_requests=len(state.get('turns', [])), tool_operations=state.get('executed_tool_operations'),
        inspected_rows=state.get('inspected_rows'), inspected_output_bytes=state.get('inspected_output_bytes'))), flush=True)
    return state


async def run_matrix(provider, method, output, provenance):
    assembly = DEMO / 'server/bin/Release/net10.0/NPS.Demo.DataWorkflow.dll'
    conditions_list = conditions(method)
    results = []
    for rows in method['row_counts']:
        directory = output / f'server-{rows}'
        with (output / f'server-{rows}.log').open('wb') as log:
            process = subprocess.Popen(['dotnet', str(assembly), '--output', str(directory), '--rows', str(rows)], stdout=log, stderr=log)
            try:
                connection = directory / 'connection.json'
                deadline = time.monotonic() + 30
                while not connection.is_file():
                    if process.poll() is not None or time.monotonic() > deadline:
                        raise StopExperiment('Local server startup failed; no model call for this source.')
                    await asyncio.sleep(.05)
                configuration = json.loads(connection.read_text())
                if (configuration.get('server_processor_count') != 4
                        or not 0 < configuration.get('server_gc_available_memory_bytes',0) <= 320*1024*1024):
                    raise StopExperiment('Actual server CPU/managed-heap configuration differs from the controlled profile.')
                configuration['_server_pid'] = process.pid
                atomic_record(output / f'server-controls-{rows}.json', dict(server_process_id=process.pid,
                    processor_count=configuration['server_processor_count'],
                    gc_available_memory_bytes=configuration['server_gc_available_memory_bytes'],
                    server_gc=configuration['server_gc_is_server_gc']))
                old = None
                async with transport(configuration) as owner:
                    adapter = client(configuration, owner, 'nps')
                    old = await adapter.create('orders', uuid.uuid4(), ttl_seconds=900)
                    old_totals = await adapter.aggregate(old, [], sum_field='amount_minor')
                for revision in (1, 2):
                    if revision == 2:
                        async with transport(configuration, operator=True) as operator:
                            response = await operator.post(configuration['operator_url'] + '/advance-source', content=b'{}')
                            if response.status_code != 200 or response.json() != {'source_revision': 2}:
                                raise StopExperiment('Operator update failed.')
                        async with transport(configuration) as owner:
                            adapter = client(configuration, owner, 'nps')
                            # The old result may expire naturally during slow provider runs. Expiry is not silent mutation.
                            try:
                                after = await adapter.aggregate(old, [], sum_field='amount_minor')
                                unchanged = after == old_totals
                                old_state = 'retained-unchanged' if unchanged else 'MUTATED'
                            except ResultHttpError as error:
                                expired = error.http_status == 404 and time.time() >= old.expires_at
                                unchanged, old_state = expired, 'expired-unavailable' if expired else 'FAILED'
                            if not unchanged:
                                raise StopExperiment('Pre-update snapshot fence failed.')
                            new = await adapter.create('orders', uuid.uuid4())
                            new_totals = await adapter.aggregate(new, [], sum_field='amount_minor')
                            if new.schema_ref == old.schema_ref or new.snapshot_id == old.snapshot_id:
                                raise StopExperiment('Source update did not change snapshot/schema.')
                            if new_totals.groups[0]['sum'] - old_totals.groups[0]['sum'] != rows:
                                raise StopExperiment('Source update amount differs from preregistered one-percent change.')
                            atomic_record(output / f'update-{rows}.json', dict(old_descriptor=dataclasses.asdict(old),
                                old_state=old_state, new_descriptor=dataclasses.asdict(new),
                                old_totals=dataclasses.asdict(old_totals), new_totals=dataclasses.asdict(new_totals)))
                            await adapter.release(new)
                    for condition in conditions_list:
                        if condition['rows'] != rows or condition['revision'] != revision:
                            continue
                        result = await run_case(provider, configuration, condition, output)
                        results.append(dict(case_id=condition['case_id'], status=result['status'],
                            independent_oracle_match=result.get('independent_oracle_match', False)))
                        atomic_record(output / 'progress.json', dict(expected_workflows=method['workflow_count'], completed=results))
                if old is not None:
                    async with transport(configuration) as owner:
                        try:
                            await client(configuration, owner, 'nps').release(old)
                            atomic_record(output / f'control-cleanup-{rows}.json', dict(released=True, outcome_unknown=False))
                        except ResultHttpError as error:
                            atomic_record(output / f'control-cleanup-{rows}.json', dict(released=False,
                                outcome_unknown=error.outcome_unknown, http_status=error.http_status,
                                expired_confirmed=error.http_status == 404 and time.time() >= old.expires_at))
            finally:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill(); process.wait()
    atomic_record(output / 'summary.json', dict(kind='actual model-selected bounded tools',
        expected_workflows=method['workflow_count'], completed=results, sdk_provenance=provenance))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--execute', action='store_true', help='Explicitly enable previously authorized paid model calls.')
    parser.add_argument('--credentials', type=Path, help='Local JSON store containing DEEPSEEK_TEST_KEY; contents never printed.')
    parser.add_argument('--ledger', type=Path, help='Existing approved shared ledger; never reset or replace with a new budget.')
    parser.add_argument('--packages', type=Path, help='Source-bound package directory with source.json.')
    parser.add_argument('--output', type=Path, help='Fresh absolute directory under this demo .demo-output.')
    arguments = parser.parse_args()
    registration = verify_method()
    if not arguments.execute:
        print(json.dumps(dict(paid_calls=False, registration=registration, conditions=conditions(registration['method'])), indent=2))
        return
    if None in (arguments.credentials, arguments.ledger, arguments.packages, arguments.output):
        parser.error('Explicit credentials, existing shared ledger, SDK package capture and fresh output are required.')
    output = arguments.output.resolve()
    if not arguments.output.is_absolute() or not output.is_relative_to(DEMO / '.demo-output') or output.exists():
        parser.error('Use a fresh absolute output directory inside this demo .demo-output; captures are never overwritten.')
    provenance = json.loads((arguments.packages / 'source.json').read_text())
    for filename, digest in provenance['artifacts'].items():
        if hashlib.sha256((arguments.packages / filename).read_bytes()).hexdigest() != digest:
            raise StopExperiment('SDK package capture changed.')
    import importlib.metadata
    if importlib.metadata.version('nps-lib') != provenance['python_version']:
        raise StopExperiment('Installed Python SDK differs from captured source package.')
    dependencies = json.loads((DEMO / 'server/bin/Release/net10.0/NPS.Demo.DataWorkflow.deps.json').read_text())['libraries']
    expected = sorted('LabAcacia.' + name + '/' + provenance['nuget_version']
        for name in ('NPS.Core', 'NPS.NWP', 'NPS.NIP', 'NPS.NIP.Storage.Sqlite'))
    if sorted(name for name in dependencies if name.startswith('LabAcacia.NPS.')) != expected:
        raise StopExperiment('Built server SDK differs from captured package source.')
    # Scope/method binding is persisted before reading the secret or reserving the first paid call.
    profile = install()
    ledger = SharedLedger(arguments.ledger, registration, namespace=registration['method']['ledger_namespace'])
    api_key = os.environ.get('DEEPSEEK_API_KEY') or json.loads(arguments.credentials.read_text())['DEEPSEEK_TEST_KEY']
    provider = DeepSeekProvider(api_key, ledger)
    output.mkdir(parents=True, mode=0o700)
    atomic_record(output / 'preregistration.json', dict(**registration, sdk_provenance=provenance, resource_profile=profile, agent_process_id=os.getpid(),
        assembly_sha256=hashlib.sha256((DEMO / 'server/bin/Release/net10.0/NPS.Demo.DataWorkflow.dll').read_bytes()).hexdigest()))
    asyncio.run(run_matrix(provider, registration['method'], output, provenance))


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        # Do not expose credential/network exception details or raw provider bodies.
        raise SystemExit(f'Live matrix stopped ({type(error).__name__}); inspect captured progress and the unchanged shared ledger history.') from None
