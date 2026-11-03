import assert from 'node:assert/strict';
import { readFileSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
const entries = ['', '/core', '/ncp', '/nwp', '/nip', '/ndp', '/nop', '/daemon', '/conformance', '/alpha19', '/alpha20', '/alpha20/sqlite'];
for (const entry of entries) await import('@labacacia/nps-sdk' + entry);
const { parseFeatureOffer, parseRequiredFeatureHeader } = await import('@labacacia/nps-sdk/alpha20');
const corpus = JSON.parse(readFileSync(process.argv[2], 'utf8'));
for (const vector of corpus.cases) {
  const parse = () => vector.kind === 'native-offer' ? parseFeatureOffer(vector.raw) : parseRequiredFeatureHeader(vector.headers);
  if (['NCP-FEATURE-NEGOTIATION-INVALID', 'NWP-FEATURE-REQUIREMENTS-INVALID'].includes(vector.expected.error))
    assert.throws(parse, new RegExp(vector.expected.error));
  else if (vector.kind === 'native-offer') {
    const raw = JSON.parse(vector.raw), parsed = parse();
    assert.deepEqual(Object.entries(parsed.features), (raw.feature_offer ?? []).map(value => [value.id, value.revisions]));
    assert.deepEqual(parsed.required, raw.required_features ?? []);
  } else assert.deepEqual(parse(), vector.expected.required);
}
const { SqliteProjectionReplayStore } = await import('@labacacia/nps-sdk/alpha20/sqlite');
const directory = mkdtempSync(join(tmpdir(), 'nps-packed-replay-'));
try {
  const location = join(directory, 'replay.db'), options = { audience: 'fixture.receiver', capacity: 4, clockSeconds: () => 100 };
  const input = { audience: options.audience, issuer: 'fixture.issuer', projectionId: 'ab'.repeat(16), acceptBefore: 110, retainUntil: 115 };
  let store = SqliteProjectionReplayStore.open(location, options);
  assert.equal(await store.claim(input), 'claimed'); store.close();
  store = SqliteProjectionReplayStore.open(location, options);
  assert.equal(await store.claim(input), 'duplicate'); store.close();
} finally { rmSync(directory, { recursive: true, force: true }); }
console.log(JSON.stringify({ public_entrypoints: entries.length, parser_cases: corpus.cases.length, sqlite_reopen_duplicate: true, source_revision: 'a9e4f05eeb85c3d5e37a85e28975aa1a23a464b7', publication: false, passed: true }));
