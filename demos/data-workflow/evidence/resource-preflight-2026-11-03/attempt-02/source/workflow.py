# Copyright 2026 INNO LOTUS PTY LTD
# SPDX-License-Identifier: Apache-2.0
"""Host-owned bounded workflow adapters. Importing this module never contacts a model."""
from __future__ import annotations

import dataclasses
import json
import ssl
import uuid
from typing import Any
from urllib.parse import urlsplit

import httpx
from nps_sdk.nwp import ResultAggregate, ResultDescriptor, ResultHttpClient, ResultHttpError, ResultPage

FIELDS = ('id', 'period', 'reason', 'refunded', 'amount_minor')
REASONS = ('delivery', 'quality', 'duplicate', 'other')


def wire(value: Any) -> Any:
    if dataclasses.is_dataclass(value):
        return dataclasses.asdict(value)
    return value


def encoded(value: Any) -> bytes:
    return json.dumps(wire(value), ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode()


def transport(configuration: dict, *, operator: bool = False) -> httpx.AsyncClient:
    """Operator-selected local trust; no proxy inheritance, redirects or disabled certificate validation."""
    for name in ('base_url', 'ordinary_url', 'operator_url'):
        address = urlsplit(configuration[name])
        if address.scheme != 'https' or address.hostname not in ('127.0.0.1', 'localhost') or address.username or address.password:
            raise ValueError('This synthetic demo accepts only pinned loopback HTTPS configuration.')
    context = ssl.create_default_context(cafile=configuration['ca_file'])
    prefix = 'operator_' if operator else ''
    context.load_cert_chain(configuration[prefix + 'certificate_file'], configuration[prefix + 'key_file'])
    return httpx.AsyncClient(verify=context, trust_env=False, follow_redirects=False, timeout=120)


class OrdinaryClient:
    """Named JSON endpoints with the same source, provider, principal and result store as NPS.

    This demo adapter is not an SDK/protocol implementation. Its server uses the
    public SDK provider as a common operator engine. No automatic retry/pagination.
    """
    def __init__(self, configuration: dict, http: httpx.AsyncClient):
        self.base = configuration['ordinary_url']
        self.issuer = configuration['issuer_nid']
        self.http = http

    async def invoke(self, operation: str, parameters: dict, key: str | None = None) -> dict:
        body: dict[str, Any] = dict(params=parameters)
        if key is not None:
            body['idempotency_key'] = key
        async with self.http.stream('POST', self.base + '/' + operation, json=body) as response:
            raw = bytearray()
            async for chunk in response.aiter_bytes():
                if len(raw) + len(chunk) > 65536:
                    raise ResultHttpError('Bounded ordinary response exceeded.', outcome_unknown=True)
                raw.extend(chunk)
            if response.status_code != 200:
                try:
                    error = json.loads(raw)
                except ValueError:
                    error = {}
                raise ResultHttpError('Ordinary operation denied.', outcome_unknown=response.status_code >= 500,
                    http_status=response.status_code, error_code=error.get('error'))
            if response.headers.get('content-type', '').split(';')[0] != 'application/json':
                raise ValueError('Invalid ordinary response type.')
            value = json.loads(raw)
            if type(value) is not dict:
                raise ValueError('Invalid ordinary response.')
            return value

    def descriptor(self, value: dict) -> ResultDescriptor:
        descriptor = ResultDescriptor(**value)
        if descriptor.issuer_nid != self.issuer or descriptor.retention != 'process':
            raise ValueError('Unexpected ordinary issuer/retention.')
        return descriptor

    async def create(self, source_id: str, idempotency_key: uuid.UUID, *, query: dict | None = None,
                     ttl_seconds: int | None = None) -> ResultDescriptor:
        params = dict(source_id=source_id, **(query or {}))
        if ttl_seconds is not None:
            params['ttl_seconds'] = ttl_seconds
        return self.descriptor(await self.invoke('create', params, str(idempotency_key)))

    @staticmethod
    def binding(descriptor: ResultDescriptor) -> dict:
        return dict(result_ref=descriptor.result_ref, snapshot_id=descriptor.snapshot_id, schema_ref=descriptor.schema_ref)

    async def read(self, descriptor: ResultDescriptor, limit: int, *, fields=None, cursor=None) -> ResultPage:
        params = dict(**self.binding(descriptor), limit=limit)
        if fields is not None:
            params['fields'] = list(fields)
        if cursor is not None:
            params['cursor'] = cursor
        value = await self.invoke('read', params)
        actual = self.descriptor(value['descriptor'])
        if actual != descriptor or type(value['rows']) is not list or len(value['rows']) > limit:
            raise ValueError('Ordinary snapshot/page mismatch.')
        return ResultPage(actual, tuple(value['rows']), value['next_cursor'])

    async def aggregate(self, descriptor: ResultDescriptor, group_by, *, sum_field=None) -> ResultAggregate:
        operations = [dict(func='COUNT', alias='count')]
        if sum_field is not None:
            operations.append(dict(func='SUM', field=sum_field, alias='sum'))
        value = await self.invoke('aggregate', dict(**self.binding(descriptor),
            aggregate=dict(group_by=list(group_by), operations=operations)))
        actual = self.descriptor(value['descriptor'])
        if actual != descriptor or len(value['groups']) > 1000:
            raise ValueError('Ordinary aggregation mismatch.')
        return ResultAggregate(actual, tuple(value['groups']))

    async def release(self, descriptor: ResultDescriptor) -> bool:
        value = await self.invoke('release', dict(result_ref=descriptor.result_ref))
        if value != dict(result_ref=descriptor.result_ref, released=True):
            raise ValueError('Invalid release receipt.')
        return True


def client(configuration: dict, http: httpx.AsyncClient, arm: str):
    if arm == 'nps':
        return ResultHttpClient(configuration['base_url'], configuration['issuer_nid'], http_client=http)
    if arm == 'ordinary':
        return OrdinaryClient(configuration, http)
    raise ValueError('Only registered ordinary/NPS bounded tool arms are supported.')


def rates(orders: ResultAggregate, refunds: ResultAggregate) -> dict:
    """Exact integer-derived anomaly decision; no model arithmetic or hidden source rows."""
    counts = {(g['reason'], g['period']): g['count'] for g in orders.groups}
    totals = {(g['reason'], g['period']): (g['count'], g['sum']) for g in refunds.groups}
    groups, anomalies = {}, []
    for reason in REASONS:
        groups[reason] = {}
        for period in ('baseline', 'current'):
            count, amount = totals.get((reason, period), (0, 0))
            groups[reason][period] = dict(orders=counts.get((reason, period), 0), refunds=count, refund_amount_minor=amount)
        b, c = groups[reason]['baseline'], groups[reason]['current']
        if min(b['orders'], c['orders']) >= 10 and c['refunds'] >= 3 and (
            10 * (c['refunds'] * b['orders'] - b['refunds'] * c['orders']) >= c['orders'] * b['orders']):
            anomalies.append(reason)
    return dict(groups=groups, anomaly_reasons=anomalies)


class WorkflowTools:
    """Only host-admitted descriptors can be used. Model input never selects network, authority or a logical replay key."""
    def __init__(self, adapter, *, maximum_bytes: int = 32768, maximum_rows: int = 20,
                 maximum_calls: int = 16, maximum_results: int = 4):
        self.adapter = adapter
        self.maximum_bytes, self.maximum_rows = maximum_bytes, maximum_rows
        self.maximum_calls, self.maximum_results = maximum_calls, maximum_results
        self.bytes = self.rows = self.calls = 0
        self.results: dict[str, ResultDescriptor] = {}
        self.released: set[str] = set()
        self.queries: dict[str, dict] = {}
        self.aggregates: dict[str, ResultAggregate] = {}
        self.inspected: list[dict] = []
        self.trace: list[dict] = []
        self.unknown_creates: list[dict] = []
        self.orders: ResultAggregate | None = None
        self.refunds: ResultAggregate | None = None

    @staticmethod
    def _keys(arguments: dict, allowed: set[str], required: set[str]):
        if type(arguments) is not dict or set(arguments) - allowed or not required <= set(arguments):
            raise ValueError('Unregistered tool arguments.')

    def _descriptor(self, handle: Any) -> ResultDescriptor:
        if type(handle) is not str or handle not in self.results or handle in self.released:
            raise ValueError('Tool requires an admitted live handle.')
        return self.results[handle]

    async def call(self, name: str, arguments: dict) -> dict:
        if self.calls >= self.maximum_calls or self.bytes >= self.maximum_bytes:
            raise ValueError('Cumulative tool budget exhausted.')
        if name not in ('result_create', 'result_read', 'result_aggregate', 'result_release'):
            raise ValueError('Unregistered tool.')
        self.calls += 1
        if name == 'result_create':
            self._keys(arguments, {'filter'}, set())
            if len(self.results) >= self.maximum_results:
                raise ValueError('Result allocation budget exhausted.')
            query = arguments.get('filter', {})
            if type(query) is not dict or set(query) - {'period', 'reason', 'refunded'}:
                raise ValueError('Unsupported source filter.')
            for key, value in query.items():
                if not (key == 'refunded' and value is True or key == 'period' and type(value) is str
                        and value in ('baseline', 'current') or key == 'reason' and type(value) is str and value in REASONS):
                    raise ValueError('Unsupported source filter.')
            logical_key = uuid.uuid4()
            try:
                descriptor = await self.adapter.create('orders', logical_key, query={'filter': query} if query else None)
            except ResultHttpError as error:
                if error.outcome_unknown:
                    self.unknown_creates.append(dict(idempotency_key=str(logical_key), query=query))
                raise
            handle = 'r' + str(len(self.results) + 1)
            self.results[handle] = descriptor
            self.queries[handle] = dict(query)
            output = dict(handle=handle, descriptor=wire(descriptor))
        elif name == 'result_read':
            self._keys(arguments, {'handle', 'limit'}, {'handle', 'limit'})
            descriptor = self._descriptor(arguments['handle'])
            limit = arguments['limit']
            if type(limit) is not int or not 1 <= limit <= 5 or self.rows + limit > self.maximum_rows:
                raise ValueError('Inspected row budget exceeded.')
            page = await self.adapter.read(descriptor, limit, fields=FIELDS)
            self.rows += len(page.rows)
            citations = [dict(result_ref=descriptor.result_ref, snapshot_id=descriptor.snapshot_id,
                schema_ref=descriptor.schema_ref, **row) for row in page.rows]
            self.inspected.extend(citations)
            output = dict(handle=arguments['handle'], **wire(page), citations=citations)
        elif name == 'result_aggregate':
            self._keys(arguments, {'handle', 'group_by', 'sum_amount'}, {'handle', 'group_by'})
            descriptor = self._descriptor(arguments['handle'])
            fields = arguments['group_by']
            if (type(fields) is not list or len(fields) > 3 or any(type(f) is not str or f not in ('period', 'reason', 'refunded') for f in fields)
                or len(set(fields)) != len(fields) or type(arguments.get('sum_amount', False)) is not bool):
                raise ValueError('Unsupported aggregate.')
            summed = arguments.get('sum_amount', False)
            aggregate = await self.adapter.aggregate(descriptor, fields, sum_field='amount_minor' if summed else None)
            self.aggregates[arguments['handle']] = aggregate
            output = dict(handle=arguments['handle'], **wire(aggregate))
            query = self.queries[arguments['handle']]
            if set(fields) == {'period', 'reason'}:
                if query == {} and not summed:
                    self.orders = aggregate
                elif query == {'refunded': True} and summed:
                    self.refunds = aggregate
            if self.orders is not None and self.refunds is not None:
                output['exact_derived_answer'] = rates(self.orders, self.refunds)
        else:
            self._keys(arguments, {'handle'}, {'handle'})
            descriptor = self._descriptor(arguments['handle'])
            await self.adapter.release(descriptor)
            output = dict(handle=arguments['handle'], released=True)
            self.released.add(arguments['handle'])
        size = len(encoded(output))
        self.bytes += size
        self.trace.append(dict(operation=name, arguments=arguments, output_bytes=size, output=output))
        if self.bytes > self.maximum_bytes:
            # Do not disclose this output to the model; record the real completed operation.
            raise ValueError('Cumulative inspected-output budget exceeded; operation completed.')
        return output

    async def cleanup(self) -> dict:
        released, failed = [], []
        for handle, descriptor in self.results.items():
            if handle in self.released:
                continue
            try:
                await self.adapter.release(descriptor)
                self.released.add(handle)
                released.append(handle)
            except (ResultHttpError, httpx.HTTPError, ValueError):
                failed.append(handle)
        return dict(released=released, failed=failed, unknown_creates=self.unknown_creates,
            retention='process/TTL; cancellation or a failed cleanup does not prove rollback')

    def validate_answer(self, answer: dict) -> None:
        """Faithfulness requires actual aggregate results and complete, actually inspected row citations."""
        if self.orders is None or self.refunds is None:
            raise ValueError('Final answer lacks executed aggregate evidence.')
        self._keys(answer, {'groups', 'anomaly_reasons', 'citations'}, {'groups', 'anomaly_reasons', 'citations'})
        exact = rates(self.orders, self.refunds)
        if (json.dumps(answer['groups'], sort_keys=True, allow_nan=False) != json.dumps(exact['groups'], sort_keys=True)
                or answer['anomaly_reasons'] != exact['anomaly_reasons']):
            raise ValueError('Final answer differs from executed exact aggregates.')
        citations = answer['citations']
        if (type(citations) is not list or len(citations) != 5 or any(c not in self.inspected for c in citations)
            or len({c['id'] for c in citations}) != 5):
            raise ValueError('Final answer citations were not five distinct inspected rows.')
        if any(c['period'] != 'current' or c['reason'] not in exact['anomaly_reasons'] or c['refunded'] is not True for c in citations):
            raise ValueError('Citations do not support the reported current refund anomaly.')
