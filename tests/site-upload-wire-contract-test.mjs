// Replay synthetic request bytes against a local checkout of the deployed Site.
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import {Readable} from 'node:stream';
import {pathToFileURL} from 'node:url';
import {runInNewContext} from 'node:vm';

const [site,baseline,testing,stable] = process.argv.slice(2);
if (!site || !baseline || !testing || !stable)
  throw new Error('Expected Site checkout and baseline, Testing and Stable fixture directories.');

process.env.GILLIONS_ENVIRONMENT = 'production';
process.env.GILLIONS_PERSONAL_SYNC_HTTPS_ORIGIN = 'https://gillions.app';
const importSite = file => import(pathToFileURL(resolve(site,file)));
const features = 'apps/web/src/features/game-sync/';
const {validateHuntBillCoverage} = await importSite(features+'hunt-bill-coverage.mjs');
const {validatePersonalObservation} = await importSite(features+'personal-observations.mjs');
const {validateTravelContext,authorizeTravelHeaders,TRAVEL_CAPABILITY} = await importSite(features+'travel-context.mjs');
const {validateFateEnvelope} = await importSite(features+'fate-observations.mjs');
const {travelCatalogAdmission:reference} = await importSite('apps/shared/src/travel-reference.mjs');

// Load the standalone raw body reader without starting the Site or its database.
const server = await readFile(resolve(site,'apps/web/src/server.mjs'),'utf8');
const readerSource = server.slice(server.indexOf('async function readJsonBody('), server.indexOf('function bearerToken('));
assert.match(readerSource,/^async function readJsonBody/);
const readJsonBody = runInNewContext(readerSource+'\nreadJsonBody',{Buffer,TextDecoder});
const readBytes = (dir,file) => readFile(resolve(dir,file+'-wire.json'));
const parse = async (dir,file) => {
  const bytes = await readBytes(dir,file);
  const {raw} = await readJsonBody(Readable.from([bytes]),file==='fate'?131072:69632,file==='fate');
  return JSON.parse(raw); // Site validators expect ordinary objects from their own realm.
};

const now = Date.parse('2026-11-20T02:25:00.123Z');
const oldTravel = (await parse(baseline,'travel')).payload;
assert.throws(()=>validateTravelContext(oldTravel,{now,reference}),
  e=>e.code==='INVALID_TRAVEL_CONTEXT'&&e.message==='Future/invalid travel observations are not current.');
assert.doesNotThrow(()=>validateTravelContext(oldTravel,{now:now+1,reference}));
const oldHunt = (await parse(baseline,'hunt')).payload;
assert.doesNotThrow(()=>validatePersonalObservation('hunt_bills',oldHunt,{now:now+14999}));
assert.throws(()=>validateHuntBillCoverage(oldHunt.billItemCoverage,{now:now+15000}),
  e=>e.code==='INVALID_HUNT_COVERAGE'&&e.message==='Hunt coverage is not current.');
const wrongMapping = structuredClone(oldHunt.billItemCoverage);
[wrongMapping.domains[0].keyItemId,wrongMapping.domains[1].keyItemId]=
  [wrongMapping.domains[1].keyItemId,wrongMapping.domains[0].keyItemId];
assert.throws(()=>validateHuntBillCoverage(wrongMapping,{now}),
  e=>e.code==='INVALID_HUNT_COVERAGE'&&e.message==='Invalid Hunt type/item/state mapping.');
const baselineFate = await parse(baseline,'fate');
assert.doesNotThrow(()=>validateFateEnvelope(baselineFate));

for (const [directory,version] of [[testing,'0.0.111.0'],[stable,'1.0.31.14']]) {
  const travel = (await parse(directory,'travel')).payload;
  assert.equal(travel.collectorVersion,version);
  assert.doesNotThrow(()=>validateTravelContext(travel,{now,reference}));
  const hunt = (await parse(directory,'hunt')).payload;
  assert.equal(hunt.billItemCoverage.collectorVersion,version);
  const fate = await parse(directory,'fate');
  assert.equal(fate.source.collectorVersion,version);
  // Separate shape from exact-version admission using an admitted fixture identity.
  hunt.billItemCoverage.collectorVersion=oldHunt.billItemCoverage.collectorVersion;
  assert.doesNotThrow(()=>validatePersonalObservation('hunt_bills',hunt,{now}));
  assert.throws(()=>validateFateEnvelope(fate),e=>e.code==='FATE_SOURCE_UNSUPPORTED');
  const compatibleFate = {...fate,source:{...fate.source,collectorVersion:baselineFate.source.collectorVersion}};
  assert.doesNotThrow(()=>validateFateEnvelope(compatibleFate));
  for (const file of ['travel','fate']) {
    const bytes = await readBytes(directory,file);
    await assert.rejects(readJsonBody(Readable.from([bytes.subarray(0,-1)]),131072,file==='fate'), {name:'SyntaxError'});
  }
}

const unadmittedStable = {
  authorized_client_product:'GillionsGameSync',
  version:'1.0.31.14',
};
assert.throws(()=>authorizeTravelHeaders({headers:{'user-agent':'GillionsGameSync/1.0.31.14',
  'x-gillions-personal-resource':'travel_context','x-gillions-personal-capability':TRAVEL_CAPABILITY}},unadmittedStable),
  e=>e.code==='TRAVEL_CLIENT_UNSUPPORTED');
console.log('Site wire replay PASS. The baseline Travel and Hunt edge cases are reproduced. JSON parses when complete, and exact new-release admission remains required.');
