"""Amend only one failed offline-verifier invocation; preserve original full-run evidence."""
from pathlib import Path
import hashlib, importlib.util, json, os, subprocess, sys
ROOT=Path('/home/olynn/ai-workspaces/labacacia-nps/worktrees/nps-alpha20-data-workflow')
BASE=Path('/home/olynn/ai-workspaces/labacacia-nps/.artifacts/alpha20/experimental-local-ci-20261103-01')
OUT=Path('/home/olynn/ai-workspaces/labacacia-nps/.artifacts/alpha20/experimental-local-ci-offline-retry-20261103-02')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def git(*a):return subprocess.check_output(['git','-C',str(ROOT),*a],text=True).strip()
def need(v,m):
 if not v:raise ValueError(m)
need(not OUT.exists(),'fresh retry output required')
need(not git('status','--porcelain'),'fixed source must be clean')
revision=git('rev-parse','HEAD'); prior=json.loads((BASE/'local-ci.json').read_text());old=prior['source_revision']
need(prior['passed'] is False and prior['source_unchanged'] is True,'preserve real failed original')
failed=[r for r in prior['stages']if not r['passed']];need(len(failed)==1 and failed[0]['stage']=='sealed-measurements','only offline stage may be replaced')
need(len(prior['runtime_configurations'])==7 and all(r['result']=='passed'for r in prior['runtime_configurations']),'seven configs must actually pass')
for row in prior['stages']:
 if 'log'in row:need(sha(BASE/row['log'])==row['sha256'],'original stage log drift')
name='tools/ci/local-alpha20.py';before=subprocess.check_output(['git','-C',str(ROOT),'show',old+':'+name]);after=(ROOT/name).read_bytes()
wrong=b'"--verify-packet", "docs/benchmarks/runs/ncp-transfer-42a0b05"';right=b'"--verify-packet", "docs/benchmarks/runs/2026-11-02-ncp-transfer-qualified.json"'
need(before.count(wrong)==1 and before.replace(wrong,right)==after,'exact single argument fix required')
need(git('diff','--name-only',old,revision).splitlines()==[name],'SDK/test/fixture/audit/control inputs must be unchanged')
need(sha(ROOT/'docs/qualification/alpha20-experimental-matrix.json')==prior['matrix_sha256'],'matrix differs')
spec=importlib.util.spec_from_file_location('localci',ROOT/name);ci=importlib.util.module_from_spec(spec);spec.loader.exec_module(ci)
source=ci.committed_source(ROOT,revision)
OUT.mkdir(mode=0o700)
python=str(ROOT/'demos/data-workflow/.demo-output/venv/bin/python')
commands=[[python,'tools/benchmarks/ncp-transfer-runtime.py','--verify-packet','docs/benchmarks/runs/2026-11-02-ncp-transfer-qualified.json'],[python,'demos/data-workflow/summarize.py','--packet','demos/data-workflow/evidence/live-tool-agent-bounded-v2-126c5df'],[python,'tools/benchmarks/nop-result-handoff.py','--packet','docs/benchmarks/runs/nop-handoff-kernel-7c4d890-attempt-03']]
log=OUT/'sealed-measurements.log';codes=[]
with log.open('xb')as stream:
 for command in commands:
  stream.write((json.dumps({'command':command,'cwd':'.','verification_source_revision':revision})+'\n').encode());stream.flush()
  code=subprocess.run(command,cwd=ROOT,env=ci.clean_environment(os.environ),stdout=stream,stderr=subprocess.STDOUT).returncode;codes.append(code)
need(codes==[0,0,0],'all three actual offline verifiers must pass')
need(not git('status','--porcelain')and git('rev-parse','HEAD')==revision and ci.source_still_matches(ROOT,source['files']),'fixed source changed')
patch=subprocess.check_output(['git','-C',str(ROOT),'diff',old,revision,'--',name]);(OUT/'exact-driver-fix.patch').write_bytes(patch)
report=json.loads(json.dumps(prior));report['kind']='amended/composed local qualification receipt; not full-suite execution at fixed HEAD'
report['source_revision']=old;report['verification_fix_revision']=revision;report['sdk_tests_audits_rerun_at_fix_revision']=False
report['original_failed_run']={'source_revision':old,'report':'attempt-01/local-ci.json','sha256':sha(BASE/'local-ci.json'),'passed':False,'failed_stage_sha256':failed[0]['sha256']}
report['verified_input_equivalence']={'changed_paths':[name],'change':'exact single offline NCP manifest argument','exact_patch_sha256':hashlib.sha256(patch).hexdigest(),'matrix_unchanged':True,'sdk_test_fixture_audit_inputs_unchanged':True,'fixed_source_objects_verified':len(source['files'])}
for row in report['stages']:
 if row['stage']=='sealed-measurements':row.update(passed=True,exit_codes=codes,log='offline-retry/sealed-measurements.log',sha256=sha(log),verification_source_revision=revision,supersedes_failed_log_sha256=failed[0]['sha256']);row.pop('elapsed_seconds',None)
 elif 'log'in row:row['log']='attempt-01/'+row['log']
report['passed']=all(r['passed']for r in report['stages'])and len(report['runtime_configurations'])==7
report['new_model_calls']=0;report['publication']=False;report['release_ready']=False
(OUT/'amended-local-ci.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({'passed':report['passed'],'full_run_source':old,'offline_fix_source':revision,'replacement_exit_codes':codes,'model_calls':0,'full_suites_repeated':False}))
