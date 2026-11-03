# Copyright 2026 INNO LOTUS PTY LTD
# SPDX-License-Identifier: Apache-2.0
"""Explicit paid-call adapter with append-only accounting in the existing approved ledger.

No requests occur on import. Credentials/error bodies/hidden reasoning are never
persisted. Atomic updates preserve every existing attempt and unknown reservation.
"""
from __future__ import annotations

import datetime
from decimal import Decimal
import fcntl
import hashlib
import json
import os
import re
from pathlib import Path
import tempfile
import time
import urllib.error
import urllib.request

MODEL = 'deepseek-flash'
CAP = Decimal('10')
RESERVATION = Decimal('0.32')
PROBE = Decimal('0.0000072')
# Preserve the exact original A/B ledger prompt binding; the Agent method has its own extension digest.
ORIGINAL_PROMPT = '985a5de13c7e92966c95364bfbd7b1e399de0b52d74ec5201d2b48f2afecd3be'


def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':'), allow_nan=False).encode()


def sha(value):
    return hashlib.sha256(canonical(value)).hexdigest()


class StopExperiment(RuntimeError):
    """A budget/accounting/provider invariant failed; no automatic retry is allowed."""


class SharedLedger:
    def __init__(self, path: Path, method: dict, *, namespace='D20-05-tool-agent'):
        if type(namespace) is not str or re.fullmatch(r'D20-[0-9]{2}-[a-z0-9-]{1,70}', namespace) is None:
            raise StopExperiment('An explicit bounded experiment namespace is required.')
        self.path, self.method, self.namespace = path, method, namespace
        if not path.is_file():
            raise StopExperiment('An existing approved shared ledger is required; creating/resetting one is forbidden.')
        self.update(lambda state: self._register(state))

    def _register(self, state):
        extensions = state.setdefault('extensions', {})
        existing = extensions.get(self.namespace)
        if existing is not None:
            if existing['method_sha256'] != sha(self.method):
                raise StopExperiment('Registered live-Agent method changed.')
        else:
            extensions[self.namespace] = dict(method_sha256=sha(self.method), registered_at=now(),
                prior_attempts_count=len(state['attempts']), prior_attempts_sha256=sha(state['attempts']),
                kind='actual model-selected bounded tools; every priming/turn request accounted', method=self.method)

    def update(self, operation):
        with self.path.with_suffix(self.path.suffix + '.lock').open('a') as lock:
            fcntl.flock(lock, fcntl.LOCK_EX)
            state = json.loads(self.path.read_text())
            if (state.get('budget_usd') != str(CAP) or state.get('model') != MODEL
                    or state.get('prior_probe_cost_bound_usd') != str(PROBE)
                    or state.get('prompt_sha256') != ORIGINAL_PROMPT):
                raise StopExperiment('Original shared ledger identity/configuration changed.')
            attempts = state.get('attempts')
            if type(attempts) is not list or len({item['key'] for item in attempts}) != len(attempts):
                raise StopExperiment('Ledger attempts are missing or duplicated.')
            bound = PROBE + sum((Decimal(item['charged_bound_usd']) for item in attempts), Decimal(0))
            if bound != Decimal(state['accounted_cost_bound_usd']) or bound > CAP:
                raise StopExperiment('Existing shared accounting is inconsistent.')
            for registration in state.get('extensions', {}).values():
                if not isinstance(registration, dict) or 'prior_attempts_sha256' not in registration:
                    continue
                count = registration['prior_attempts_count']
                if (type(count) is not int or not 0 <= count <= len(attempts)
                        or sha(attempts[:count]) != registration['prior_attempts_sha256']):
                    raise StopExperiment('A previous shared attempt/reservation changed.')
            result = operation(state)
            state['accounted_cost_bound_usd'] = str(PROBE + sum(
                (Decimal(item['charged_bound_usd']) for item in attempts), Decimal(0)))
            with tempfile.NamedTemporaryFile('w', dir=self.path.parent, delete=False) as output:
                json.dump(state, output, indent=2, allow_nan=False)
                output.write('\n'); output.flush(); os.fsync(output.fileno())
                temporary = output.name
            os.replace(temporary, self.path) # Atomic persistence of the same ledger; not a new run or reset.
            return result

    def reserve(self, key: str, metadata: dict):
        if set(metadata) & {'key','started_at','status','charged_bound_usd','completed_at','provider_usage'}:
            raise StopExperiment('Request metadata cannot override shared accounting fields.')
        if metadata.get('method', self.namespace) != self.namespace:
            raise StopExperiment('Attempt namespace differs from its registered experiment.')
        metadata = dict(metadata, method=self.namespace)
        def operation(state):
            if any(item['key'] == key for item in state['attempts']):
                raise StopExperiment('This paid attempt already exists; do not automatically retry it.')
            accounted = Decimal(state['accounted_cost_bound_usd'])
            if accounted + RESERVATION > CAP:
                raise StopExperiment('The approved shared budget cannot reserve another request.')
            state['attempts'].append(dict(key=key, started_at=now(), status='OUTCOME_UNKNOWN',
                charged_bound_usd=str(RESERVATION), **metadata))
        self.update(operation)

    def settle(self, key: str, observation: dict):
        def operation(state):
            item = next(attempt for attempt in state['attempts'] if attempt['key'] == key)
            if item['status'] != 'OUTCOME_UNKNOWN' or 'completed_at' in item:
                raise StopExperiment('A paid attempt cannot be settled twice.')
            item.update(observation, completed_at=now())
            usage = observation.get('provider_usage')
            if type(usage) is dict:
                prompt, completion = usage.get('prompt_tokens'), usage.get('completion_tokens')
                if type(prompt) is not int or type(completion) is not int or min(prompt, completion) < 0:
                    raise StopExperiment('Provider token counters are invalid; keep the full reservation.')
                if prompt > 1048576 or completion > 2048:
                    raise StopExperiment('Provider usage exceeded the reservation assumptions.')
                bound = (Decimal(prompt) * Decimal('.30') + Decimal(completion) * Decimal('1.20')) / 1000000
                item['charged_bound_usd'] = item['usage_cost_upper_bound_usd'] = str(bound)
        self.update(operation)


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, file, code, message, headers, new_url):
        return None


class DeepSeekProvider:
    def __init__(self, api_key: str, ledger: SharedLedger, *, guard=None):
        if not isinstance(api_key, str) or not api_key.strip():
            raise StopExperiment('A local configured DeepSeek credential is required.')
        self._key, self.ledger, self.guard = api_key, ledger, guard

    def request(self, key: str, payload: dict, metadata: dict) -> dict:
        raw_request = canonical(payload)
        if (payload.get('model') != MODEL or payload.get('max_tokens') != 2048
                or payload.get('thinking') != {'type': 'disabled'} or payload.get('stream') is not False
                or len(raw_request) > 131072):
            raise StopExperiment('Request differs from the preregistered bounded model configuration.')
        timeout = 120
        if self.guard is not None:
            try:
                remaining = self.guard()
                if remaining is not None:
                    if type(remaining) not in (int, float) or not 0 < remaining <= 120:
                        raise RuntimeError('Invalid remaining workflow deadline.')
                    timeout = remaining
            except Exception:
                raise StopExperiment('Resource guard failed before paid reservation/network use.') from None
        self.ledger.reserve(key, dict(method=self.ledger.namespace, request_json_bytes=len(raw_request),
            request_sha256=hashlib.sha256(raw_request).hexdigest(), **metadata))
        request = urllib.request.Request('https://api.deepseek.com/chat/completions', data=raw_request,
            headers={'Authorization': 'Bearer ' + self._key, 'Content-Type': 'application/json'})
        started = time.monotonic()
        try:
            opener = urllib.request.build_opener(NoRedirect(), urllib.request.ProxyHandler({}))
            with opener.open(request, timeout=timeout) as response:
                raw = response.read(2 * 1024 * 1024 + 1)
                if len(raw) > 2 * 1024 * 1024:
                    raise StopExperiment('Provider response exceeds capture bound.')
                body = json.loads(raw)
                message = body['choices'][0]['message']
                # Whitelist visible assistant fields. Never capture hidden reasoning, credentials or raw error bodies.
                visible = {name: message[name] for name in ('role', 'content', 'tool_calls') if name in message}
                observation = dict(status='OBSERVED', http_status=response.status, returned_model=body.get('model'),
                    response_id=body.get('id'), finish_reason=body['choices'][0].get('finish_reason'),
                    provider_usage=body.get('usage'), assistant_message=visible,
                    provider_response_json_bytes=len(raw), response_sha256=hashlib.sha256(raw).hexdigest())
        except urllib.error.HTTPError as error:
            observation = dict(status='HTTP_ERROR', http_status=error.code, provider_usage=None)
            error.close()
        except Exception as error:
            observation = dict(status='OUTCOME_UNKNOWN', failure_type=type(error).__name__, provider_usage=None)
        observation['elapsed_ms'] = round((time.monotonic() - started) * 1000, 3)
        self.ledger.settle(key, observation)
        if observation.get('returned_model', MODEL) != MODEL:
            raise StopExperiment('Provider returned a different model; comparison stopped.')
        if observation['status'] != 'OBSERVED':
            raise StopExperiment('Provider attempt failed/unknown; reservation retained and no retry performed.')
        if self.guard is not None:
            try:
                self.guard()
            except Exception:
                raise StopExperiment('Resource guard failed after observed request; its actual usage is settled.') from None
        return observation
