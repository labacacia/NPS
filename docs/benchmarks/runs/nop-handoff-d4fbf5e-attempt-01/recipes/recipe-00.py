#!/usr/bin/env python3
"""Preregistered direct NOP reference qualification; no model/credentials/ledger calls.

Execute only from clean committed sources after building the declared test assembly.
Preserve every failed/partial attempt. Packet verification needs only standard Python.
"""
from __future__ import annotations
import argparse
import hashlib
import importlib.util
import json
import math
import os
from pathlib import Path
import subprocess
import sys
import threading
import time

ROOT = Path(__file__).resolve().parents[2]
METHOD = ROOT / 'docs/benchmarks/nop-result-handoff-method.json'
ASSEMBLY = ROOT / 'impl/dotnet/tests/NPS.Tests/bin/Release/net10.0/NPS.Tests.dll'
SOURCE_FILES = [
    'tools/benchmarks/nop-result-handoff.py', 'docs/benchmarks/nop-result-handoff-method.json',
    'demos/data-workflow/resource_profile.py',
    'impl/dotnet/tests/NPS.Tests/Nwp/NopHandoffCapture.cs',
    'impl/dotnet/tests/NPS.Tests/Nwp/NopResultHandoffTests.cs',
    'impl/dotnet/tests/NPS.Tests/Ncp/SchemaBatchCaptureProgram.cs',
    'impl/dotnet/tests/NPS.Tests/Nwp/ResultOracleWorkflowTests.cs',
    'impl/dotnet/tests/NPS.Tests/Nwp/ResultInvocationTlsTests.cs',
    'impl/dotnet/tests/NPS.Tests/Nwp/ResultInvocationGatewayTests.cs',
    'impl/dotnet/tests/NPS.Tests/Nwp/ResultRuntimeComparisonTransport.cs',
    'spec/conformance/alpha20/data_workflow_methodology.json',
    'impl/dotnet/src/NPS.NOP/Models/TaskState.cs',
    'impl/dotnet/src/NPS.NOP/Orchestration/NopInputMapper.cs',
    'impl/dotnet/src/NPS.NOP/Orchestration/NopOrchestratorOptions.cs',
    'impl/dotnet/src/NPS.NOP/Validation/DagValidator.cs',
    'impl/dotnet/src/NPS.NOP/Runtime/NopRuntimeBinding.cs',
    'impl/dotnet/src/NPS.NOP/Runtime/NopRuntimeExecutor.cs',
    'impl/dotnet/src/NPS.NOP/Runtime/NopTaskIdentity.cs',
    'impl/dotnet/src/NPS.NOP/Orchestration/NopOrchestrator.cs',
    'impl/dotnet/src/NPS.NOP/Frames/NopFrames.cs',
    'impl/dotnet/src/NPS.NOP/Orchestration/NopTaskResult.cs',
    'impl/dotnet/samples/NPS.Samples.NopDag/Orchestration/SqliteNopRuntimeJournal.cs',
    'impl/dotnet/samples/NPS.Samples.NopDag/NPS.Samples.NopDag.csproj',
]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def write(path, value):
    with path.open('x') as output:
        json.dump(value, output, indent=2, allow_nan=False)
        output.write('\n')


def conditions(method):
    if (method['positive_rows'] != [100,1000,100000] or method['positive_repeats'] != 3
        or method['fault_rows'] != 100 or method['fault_repeats'] != 1
        or method['faults'] != ['missing-grant','revoked-grant','hidden-field','revoked-worker','expired-input','lost-query-response','create-audit-loss']):
        raise ValueError('Original declared cases changed.')
    result = [(f'handoff-{rows}-pass-r{repeat}', rows, None)
        for rows in method['positive_rows'] for repeat in range(method['positive_repeats'])]
    result += [(f"handoff-{method['fault_rows']}-{fault}-r0", method['fault_rows'], fault) for fault in method['faults']]
    if len(result) != 16 or len(result) != method['condition_count']:
        raise ValueError('Preregistered condition matrix changed.')
    return result


def execute(directory):
    if subprocess.check_output(['git','status','--porcelain'], cwd=ROOT, text=True).strip():
        raise ValueError('Commit/preregister source before execution; never measure a dirty tree.')
    if not ASSEMBLY.is_file():
        raise ValueError('Build the declared test assembly first.')
    method = json.loads(METHOD.read_text())
    spec = importlib.util.spec_from_file_location('handoff_resource_profile', ROOT / SOURCE_FILES[2])
    resource = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(resource)
    profile = resource.install()
    profile['rss_scope'] = method['resource_scope']
    revision = subprocess.check_output(['git','rev-parse','HEAD'], cwd=ROOT, text=True).strip()
    directory.mkdir(parents=True, exist_ok=False)
    recipes = directory / 'recipes'; recipes.mkdir()
    sources = []
    for index, source in enumerate(SOURCE_FILES):
        data = (ROOT / source).read_bytes()
        name = f'recipe-{index:02d}' + Path(source).suffix
        (recipes / name).write_bytes(data)
        sources.append(dict(path=source, archive_path='recipes/' + name, sha256=sha(data)))
    assemblies = {name:sha((ASSEMBLY.parent/name).read_bytes()) for name in
        ('NPS.Tests.dll','NPS.NOP.dll','NPS.NWP.dll','NPS.NIP.dll','NPS.NIP.Storage.Sqlite.dll','NPS.Core.dll',
         'NPS.Samples.NopDag.dll','NPS.Tests.deps.json')}
    dependencies = json.loads((ASSEMBLY.parent/'NPS.Tests.deps.json').read_text())
    with (directory/'runtime-dependencies.json').open('xb') as output:
        output.write((ASSEMBLY.parent/'NPS.Tests.deps.json').read_bytes())
    registration = dict(dependency_names=sorted(dependencies['libraries']),source_revision=revision, method=method, resource_profile=profile, source_files=sources,
        test_assembly_sha256=assemblies['NPS.Tests.dll'], assemblies=assemblies,
        dotnet_sdk=subprocess.check_output(['dotnet','--version'], text=True).strip(),
        python=sys.version, controller_pid=os.getpid())
    write(directory/'preregistration.json', registration)
    failed = []
    for label, rows, fault in conditions(method):
        capture = directory / label; capture.mkdir()
        env = dict(os.environ, NPS_NOP_HANDOFF_CAPTURE_DIR=str(capture.resolve()))
        gate = resource.ResourceGate()
        done = threading.Event()
        def sample():
            while not done.wait(resource.INTERVAL):
                try:
                    gate.observe()
                except RuntimeError:
                    return
        watcher = threading.Thread(target=sample, daemon=True)
        watcher.start()
        process = None
        error = None
        fixture_affinity = None
        try:
            gate.require()
            if any(sha((ASSEMBLY.parent/name).read_bytes()) != digest for name,digest in assemblies.items()):
                raise RuntimeError('Measured assembly/dependency drift before condition.')
            with (capture/'execution.log').open('wb') as output:
                process = subprocess.Popen(['dotnet',str(ASSEMBLY),'--nop-handoff',label], cwd=ROOT,
                    env=env,stdout=output,stderr=subprocess.STDOUT)
                fixture_affinity = sorted(os.sched_getaffinity(process.pid))
                while process.poll() is None:
                    remaining = gate.require()
                    try:
                        process.wait(timeout=min(.05,remaining))
                    except subprocess.TimeoutExpired:
                        pass
                if process.returncode:
                    raise RuntimeError('Direct asserted fixture returned a failure.')
                gate.require()
                if any(sha((ASSEMBLY.parent/name).read_bytes()) != digest for name,digest in assemblies.items()):
                    raise RuntimeError('Measured assembly/dependency drift after condition.')
        except Exception as failure:
            error = type(failure).__name__
            if process is not None and process.poll() is None:
                process.kill(); process.wait(timeout=5)
        finally:
            done.set(); watcher.join(timeout=1)
            record = gate.report()
            record['specification'] = dict(record['specification'],rss_scope=method['resource_scope'])
            write(capture/'resource.json',dict(case_id=label,rows=rows,fault=fault,process_id=None if process is None else process.pid,
                guard=record,fixture_cpu_affinity=fixture_affinity,error_type=error,exit_code=None if process is None else process.returncode))
        if error or not record['passed']:
            failed.append(label)
        print(label + (' PASS' if not error and record['passed'] else ' FAILED (preserved)'),flush=True)
    write(directory/'completion.json',dict(complete=True,conditions=16,failed=failed))
    # Closed scientific packet contains typed logs and synthetic observations, never
    # ephemeral certificates/keys/configuration/SQLite or any provider credentials.
    write(directory/'index.json',dict(kind='closed direct asserted NOP reference packet',files={
        str(path.relative_to(directory)):sha(path.read_bytes()) for path in sorted(directory.rglob('*')) if path.is_file()}))
    report = verify(directory)
    print(json.dumps(report,indent=2))


def verify(directory):
    index = json.loads((directory/'index.json').read_text())
    listed = index['files']
    actual = {str(p.relative_to(directory)) for p in directory.rglob('*') if p.is_file() and str(p.relative_to(directory)) != 'index.json'}
    if actual != set(listed):
        raise ValueError('Missing or unexpected closed packet files.')
    for name, digest in listed.items():
        path = directory/name
        if path.resolve().parent != directory.resolve() and directory.resolve() not in path.resolve().parents:
            raise ValueError('Packet path escaped its root.')
        if path.is_symlink() or sha(path.read_bytes()) != digest:
            raise ValueError('Closed packet file hash changed.')
    registration = json.loads((directory/'preregistration.json').read_text())
    method = registration['method']
    dependencies = json.loads((directory/'runtime-dependencies.json').read_text())
    if sha((directory/'runtime-dependencies.json').read_bytes()) != registration['assemblies']['NPS.Tests.deps.json'] or sorted(dependencies['libraries']) != registration['dependency_names']:
        raise ValueError('Actual dependency capture differs.')
    if 'SQLitePCLRaw.bundle_e_sqlite3/3.0.5' not in registration['dependency_names']:
        raise ValueError('Declared maintained SQLite bundle missing.')
    if (method['processor_count'],method['gc_available_memory_bytes'],method['rss_limit_bytes'],method['max_case_seconds']) != (4,335544320,536870912,120):
        raise ValueError('Original resource limits changed.')
    if {source['path'] for source in registration['source_files']} != set(SOURCE_FILES):
        raise ValueError('Measured recipe set missing.')
    source_map = {source['path']:source['archive_path'] for source in registration['source_files']}
    if json.loads((directory/source_map['docs/benchmarks/nop-result-handoff-method.json']).read_text()) != method:
        raise ValueError('Registered method differs from its exact measured recipe.')
    oracle = json.loads((directory/source_map['spec/conformance/alpha20/data_workflow_methodology.json']).read_text())['reference_answers']
    expected_spec = dict(processor_count=4,gc_heap_hard_limit='0x14000000',rss_limit_bytes=536870912,
        max_workflow_seconds=120,rss_sample_interval_seconds=.01,rss_scope=method['resource_scope'])
    profile = registration['resource_profile']
    affinity = profile.get('cpu_affinity',[])
    if (len(affinity)!=4 or len(set(affinity))!=4 or any(type(cpu) is not int or cpu<0 for cpu in affinity)
        or profile != dict(expected_spec,cpu_affinity=affinity)
        or method['rss_sample_interval_seconds'] != .01):
        raise ValueError('Actual frozen launch profile/CPU affinity differs.')
    for source in registration['source_files']:
        if source['archive_path'] not in listed or listed[source['archive_path']] != source['sha256']:
            raise ValueError('Measured source recipe changed.')
    completion = json.loads((directory/'completion.json').read_text())
    if completion != dict(complete=True,conditions=16,failed=[]):
        raise ValueError('Qualification contains failed/incomplete conditions; retain them as failures.')
    reports = []
    seen_tasks = set()
    if type(registration['controller_pid']) is not int or registration['controller_pid']<=0:
        raise ValueError('Actual controller identity missing.')
    for label, rows, fault in conditions(method):
        capture = directory / label
        raw_data = (capture/'raw-case.json').read_bytes()
        raw = json.loads(raw_data)
        receipt = json.loads((capture/'execution-receipt.json').read_text())
        metrics = json.loads((capture/'resource.json').read_text())
        guard = metrics['guard']
        if ((metrics['case_id'],metrics['rows'],metrics['fault'],metrics['process_id']) !=
                (label,rows,fault,receipt['process_id']) or metrics['fixture_cpu_affinity'] != affinity
                or guard['specification'] != expected_spec):
            raise ValueError('Resource record condition/process/profile binding differs.')
        if (raw['rows'],raw['fault'],receipt['case_id'],receipt['rows'],receipt['fault']) != (rows,fault,label,rows,fault):
            raise ValueError('Mixed handoff conditions.')
        if receipt['test_assembly_sha256'] != registration['test_assembly_sha256'] or receipt['raw_record_sha256'] != sha(raw_data):
            raise ValueError('Execution receipt/source hash differs.')
        if any(type(receipt[key]) is not int or receipt[key]<=0 for key in ('processor_count','gc_available_memory_bytes','process_id')):
            raise ValueError('Backend integer control/process evidence invalid.')
        if receipt['passed'] is not True:
            raise ValueError('Direct assertions did not pass.')
        if (receipt['processor_count'],receipt['gc_available_memory_bytes'],receipt['execution_mode'],receipt['passed']) != (4,335544320,'direct_asserted_fixture',True):
            raise ValueError('Actual backend controls/execution missing.')
        if (metrics['exit_code'] != 0 or metrics['error_type'] is not None or guard['passed'] is not True or guard['violations']
            or type(guard['poll_count']) is not int or guard['poll_count'] <= 0
            or registration['controller_pid'] not in guard['process_ids'] or receipt['process_id'] not in guard['process_ids']):
            raise ValueError('Actual controller/fixture process evidence incomplete.')
        for key in ('sampled_process_tree_peak_rss_bytes','live_process_high_water_sum_guard_bytes'):
            if type(guard[key]) is not int or not 0 < guard[key] <= 536870912:
                raise ValueError('Process-tree memory guard failed.')
        if type(guard['elapsed_seconds']) not in (int,float) or not math.isfinite(guard['elapsed_seconds']) or not 0 < guard['elapsed_seconds'] < 120:
            raise ValueError('Case deadline failed.')
        for key in ('wall_ms','process_cpu_ms','managed_allocated_bytes'):
            if type(receipt[key]) not in (int,float) or not math.isfinite(receipt[key]) or receipt[key]<0:
                raise ValueError('Actual process cost counter missing/invalid.')
        observation = raw['observation']; result = observation['Result']
        if result['TaskId'] in seen_tasks: raise ValueError('Independent case reused an earlier task result.')
        seen_tasks.add(result['TaskId'])
        certainty = 'indeterminate' if fault in ('lost-query-response','create-audit-loss') else 'known_terminal'
        if result['execution_outcome'] != certainty or result['FinalState'] != (4 if fault is None else 5):
            # TaskState wire enum is checked below against the measured recipe's defined enum.
            raise ValueError('Truthful parent outcome differs from the asserted scenario.')
        if observation['ModelVisibleBytes'] != 0 or type(observation['ModelVisibleBytes']) is not int:
            raise ValueError('Deterministic plan must not claim model usage.')
        if fault is None:
            if observation['Dispatches'] != 4 or observation['Released'] != 2 or raw['source_captures'] != 2:
                raise ValueError('Reference DAG/cleanup did not execute exactly.')
            node_results = result['NodeResults']
            if set(node_results) != {'orders','refunds','analysis','report'}:
                raise ValueError('Required proven DAG outputs missing.')
            for name in ('orders','refunds'):
                descriptor = node_results[name]
                actual = node_results['analysis']['counts' if name == 'orders' else 'sums']
                if actual['descriptor'] != descriptor:
                    raise ValueError('Aggregate lost its source/snapshot/schema provenance.')
                seen = set()
                for group in actual['groups']:
                    key = (group['reason'],group['period'])
                    if key in seen: raise ValueError('Duplicate exact group.')
                    seen.add(key)
                    expected = oracle[str(rows)]['groups'][group['reason']][group['period']]
                    if group['count'] != expected['orders' if name == 'orders' else 'refunds']:
                        raise ValueError('Frozen exact group count differs.')
                    if name == 'refunds' and group['sum'] != expected['refund_amount_minor']:
                        raise ValueError('Frozen exact amount differs.')
                required = {(reason,period) for reason,periods in oracle[str(rows)]['groups'].items()
                    for period,values in periods.items() if isinstance(values,dict) and values.get('orders' if name == 'orders' else 'refunds',0)>0}
                if seen != required: raise ValueError('Frozen oracle group missing.')
            if node_results['report']['provenance'] != node_results['refunds'] or len(node_results['report']['evidence']) != 5:
                raise ValueError('Report lost bounded row provenance.')
            if (node_results['analysis']['refunds'] != node_results['refunds']
                or node_results['report']['groups'] != node_results['analysis']['sums']['groups']):
                raise ValueError('Report changed verified groups or upstream descriptor.')
            for number, row in enumerate(node_results['report']['evidence']):
                if row['id'] != f'order:{number:06d}' or set(row) != {'id','reason','period','amount_minor'}:
                    raise ValueError('Report must contain the five distinct actually read refund IDs/fields.')
                if (not 0<=number<rows or row['reason'] != ['delivery','quality','duplicate','other'][number%4]
                    or row['period'] != ('baseline' if number%100<50 else 'current')
                    or row['amount_minor'] != 1000+number%100*37):
                    raise ValueError('Actually inspected source row differs.')
            if not 0 < observation['DelegateBytes'] <= 16384 or not 0 < observation['AlignBytes'] <= 32768:
                raise ValueError('Reference payload exceeded its bound.')
        elif result['NodeResults']:
            raise ValueError('Failed/unknown input was consumed downstream.')
        expected_counts = {
            None:(4,2,7,2,0,None),
            'missing-grant':(3,2,4,2,0,'NWP-RESULT-NOT-FOUND'),
            'revoked-grant':(3,2,4,2,0,'NWP-RESULT-NOT-FOUND'),
            'hidden-field':(3,2,5,2,0,'NWP-RESULT-NOT-FOUND'),
            'revoked-worker':(3,2,4,2,0,'NIP-CONTEXT-UNTRUSTED'),
            'expired-input':(3,2,2,0,0,'NWP-RESULT-NOT-FOUND'),
            'lost-query-response':(1,1,2,1,0,'NOP-EXECUTION-INDETERMINATE'),
            'create-audit-loss':(1,1,1,0,1,'NOP-EXECUTION-INDETERMINATE'),
        }[fault]
        if (observation['Dispatches'],raw['source_captures'],raw['resource_handler_dispatches'],observation['Released'],
            observation['UnresolvedCreates'],result['ErrorCode']) != expected_counts:
            raise ValueError('Scenario-specific dispatch/effect/cleanup/error proof differs.')
        if len(raw['unresolved_create_keys']) != observation['UnresolvedCreates']:
            raise ValueError('Unknown allocation recovery identities were lost.')
        if fault == 'create-audit-loss':
            pending = raw['unresolved_create_keys'][0]
            import uuid
            if uuid.UUID(pending['idempotency_key']).version != 4 or pending['query'] is not None or pending['ttl_seconds']!=300:
                raise ValueError('Original unknown-create key/parameters missing.')
        if any(type(observation[k]) is not int or observation[k] <= 0 for k in ('PublicTlsBytes','GatewayReceiverTlsBytes')):
            raise ValueError('Actual referenced network cost missing.')
        reports.append(dict(case_id=label,rows=rows,fault=fault,**{k:observation[k] for k in
            ('Dispatches','DelegateBytes','AlignBytes','PublicTlsBytes','GatewayReceiverTlsBytes','Released','UnresolvedCreates')},
            peak_rss_bytes=guard['sampled_process_tree_peak_rss_bytes'],high_water_guard_bytes=guard['live_process_high_water_sum_guard_bytes'],
            wall_ms=receipt['wall_ms'],cpu_ms=receipt['process_cpu_ms'],allocated_bytes=receipt['managed_allocated_bytes']))
    return dict(source_revision=registration['source_revision'],conditions=len(reports),passed=True,
        max_rss_bytes=max(r['peak_rss_bytes'] for r in reports),max_high_water_guard_bytes=max(r['high_water_guard_bytes'] for r in reports),cases=reports)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument('--execute',type=Path,help='Fresh absolute output directory; actual local fixture, no models')
    group.add_argument('--packet',type=Path,help='Verify existing closed packet; no SDK, model, original ledger or network')
    args = parser.parse_args()
    if args.execute:
        if not args.execute.is_absolute(): parser.error('Execution output must be absolute.')
        execute(args.execute)
    else:
        print(json.dumps(verify(args.packet),indent=2))

if __name__ == '__main__':
    main()
