#!/usr/bin/env python3
# Copyright 2026 INNO LOTUS PTY LTD
# SPDX-License-Identifier: Apache-2.0
"""Replay recorded operation selections against actual SDK/mTLS under frozen resource controls.

No model, credentials, provider calls or spend ledger. This qualifies local resource
behavior only; original model answers/failures are preserved and never rescored.
"""
from __future__ import annotations

import argparse
import asyncio
import hashlib
import json
import os
from pathlib import Path
import subprocess
import time
import uuid

from resource_profile import ResourceGate, install, INTERVAL
from workflow import WorkflowTools, client, transport

DEMO = Path(__file__).resolve().parent


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False).encode()


def write(path, value):
    path.write_bytes(canonical(value) + b'\n')


async def replay_case(configuration, original, output):
    gate, active = ResourceGate(), True
    async def sample():
        while active:
            gate.observe()
            await asyncio.sleep(INTERVAL)
    sampler = asyncio.create_task(sample())
    tools = None
    case = dict(case_id=original['case_id'], original_model_status=original['status'],
        kind='actual SDK/mTLS recorded-operation replay; zero model calls; not a new model answer')
    try:
        async with transport(configuration) as http:
            tools = WorkflowTools(client(configuration, http, original['case_id'].rsplit(':', 1)[1]))
            for step in original['tool_trace']:
                gate.require()
                actual = await tools.call(step['operation'], step['arguments'])
                if step['operation'] in ('result_create','result_read','result_aggregate'):
                    descriptor = actual['descriptor']
                    previous = step['output']['descriptor']
                    for name in ('schema_ref','snapshot_id','row_count','issuer_nid'):
                        if descriptor[name] != previous[name]:
                            raise ValueError('Recorded source/schema/identity fence changed during resource replay.')
                if step['operation'] == 'result_aggregate' and canonical(actual['groups']) != canonical(step['output']['groups']):
                    raise ValueError('Actual exact aggregate differs from recorded source.')
                if step['operation'] == 'result_read' and canonical(actual['rows']) != canonical(step['output']['rows']):
                    raise ValueError('Actual inspected rows differ from recorded source.')
            case['cleanup'] = await tools.cleanup()
            if case['cleanup']['failed'] or case['cleanup']['unknown_creates']:
                raise ValueError('Recorded resource replay cleanup failed.')
            gate.require()
            case.update(runtime_replay_passed=True, executed_operations=len(tools.trace), inspected_rows=tools.rows,
                inspected_output_bytes=tools.bytes)
    except Exception as error:
        case.update(runtime_replay_passed=False, failure_type=type(error).__name__,
            http_status=getattr(error,'http_status',None), error_code=getattr(error,'error_code',None),
            outcome_unknown=getattr(error,'outcome_unknown',None))
        raise
    finally:
        active = False
        await sampler
        gate.observe()
        case['resource_guard'] = gate.report()
        write(output / (original['case_id'].replace(':','_') + '.json'), case)
    if not case['resource_guard']['passed']:
        raise RuntimeError('Frozen resource guard failed.')
    return case


async def execute(packet, output, profile):
    cases = [json.loads(path.read_text()) for path in packet.glob('d05*.json')]
    if len(cases) != 80:
        raise ValueError('The original complete80case packet is required.')
    assembly = DEMO / 'server/bin/Release/net10.0/NPS.Demo.DataWorkflow.dll'
    results = []
    for rows in (100,100000):
        directory = output / f'server-{rows}'
        with (output / f'server-{rows}.log').open('wb') as log:
            process = subprocess.Popen(['dotnet',str(assembly),'--output',str(directory),'--rows',str(rows)],stdout=log,stderr=log)
            try:
                connection = directory / 'connection.json'
                deadline = time.monotonic() + 30
                while not connection.exists():
                    if process.poll() is not None or time.monotonic() > deadline:
                        raise RuntimeError('Actual bounded server startup failed.')
                    await asyncio.sleep(.05)
                configuration = json.loads(connection.read_text())
                async with transport(configuration) as owner:
                    adapter = client(configuration,owner,'nps')
                    old = await adapter.create('orders',uuid.uuid4(),ttl_seconds=900)
                    old_amount = await adapter.aggregate(old,[],sum_field='amount_minor')
                    for revision in (1,2):
                        if revision == 2:
                            async with transport(configuration,operator=True) as operator:
                                response = await operator.post(configuration['operator_url']+'/advance-source',content=b'{}')
                                assert response.status_code == 200
                            assert await adapter.aggregate(old,[],sum_field='amount_minor') == old_amount
                        selected = [case for case in cases if case['rows']==rows and case['revision']==revision]
                        selected.sort(key=lambda case: (int(case['case_id'].split(':')[3]),
                            case['cache_requested'],0 if case['case_id'].endswith(':ordinary') else 1))
                        for original in selected:
                            result = await replay_case(configuration,original,output)
                            results.append(result)
                            print(original['case_id']+' local replay/resource PASS',flush=True)
                    await adapter.release(old)
            finally:
                process.terminate()
                try: process.wait(timeout=10)
                except subprocess.TimeoutExpired: process.kill();process.wait()
    write(output / 'summary.json',dict(kind='bounded actual SDK/mTLS recorded-operation replay; zero paid/model calls',
        resource_profile=profile,source_packet_index_sha256=hashlib.sha256((packet/'index.json').read_bytes()).hexdigest(),
        original_model_successes=sum(c['independent_oracle_match'] for c in cases),original_model_failures_preserved=2,
        runtime_replays=len(results),all_resource_guards_passed=all(r['resource_guard']['passed'] for r in results),
        actual_process_tree_peak_rss_bytes=max(r['resource_guard']['sampled_process_tree_peak_rss_bytes'] for r in results),
        live_high_water_sum_guard_bytes=max(r['resource_guard']['live_process_high_water_sum_guard_bytes'] for r in results),
        case_files=[r['case_id'].replace(':','_')+'.json' for r in results]))


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--packet',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    arguments=parser.parse_args()
    output=arguments.output.resolve()
    if not arguments.output.is_absolute() or not output.is_relative_to(DEMO/'.demo-output') or output.exists():
        parser.error('A fresh absolute output directory under this demo .demo-output is required.')
    output.mkdir(parents=True,mode=0o700)
    profile=install()
    write(output/'profile.json',profile)
    source=output/'source';source.mkdir()
    import shutil
    for name in ('resource-replay.py','resource_profile.py','workflow.py','server/OrderSource.cs','server/DemoHost.cs'):
        target=source/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(DEMO/name,target)
    write(output/'source.json',dict(files=[dict(path=str(p.relative_to(source)),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
        for p in sorted(source.rglob('*')) if p.is_file()],
        assembly_sha256=hashlib.sha256((DEMO/'server/bin/Release/net10.0/NPS.Demo.DataWorkflow.dll').read_bytes()).hexdigest(),
        paid_calls=0))
    asyncio.run(execute(arguments.packet,output,profile))


if __name__=='__main__': main()
