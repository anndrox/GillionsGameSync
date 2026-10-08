"""Read-only shared TEST module cross-check; synthetic, NOT authenticated game proof.

Uses established ssh/docker tooling, no credentials, DB writes, new services or
Site source changes. HTTPS anonymous rejection is exercised from this workstation.
"""
import json
from pathlib import Path
import subprocess
import urllib.request
import urllib.error

root = Path(__file__).resolve().parents[1]
fixture = root / 'artifacts/verification/travel/travel-context-v1.json'
proof = fixture.with_name('site-protocol-proof.json')
expected = '93007035e6cce8636e10a356dcda4fb07c3dfd1c'
labels = subprocess.run(['ssh', 'gillions-codex', "docker inspect --format '{{json .Config.Labels}}' ffxiv-gillions-test-web-1"], capture_output=True, text=True, check=True).stdout
identity = json.loads(labels)['org.opencontainers.image.revision']
assert identity == expected, 'Shared TEST changed: rediscover exact compatibility before validation.'
script = r'''
import assert from 'node:assert/strict';
import {validateTravelContext,authorizeTravelHeaders,TRAVEL_PERMISSION,TRAVEL_CAPABILITY} from './src/features/game-sync/travel-context.mjs';
import {createTravelContextStore} from './src/features/game-sync/travel-context-store.mjs';
import {createPersonalIntakeRoutes} from './src/features/game-sync/personal-intake-routes.mjs';
import {authorizePersonalDevice} from './src/features/game-sync/personal-observation-store.mjs';
import {travelCatalogAdmission} from '../shared/src/travel-reference.mjs';
let input='';for await(const chunk of process.stdin)input+=chunk;const payload=JSON.parse(input);
const epoch=Date.parse(payload.observedAtUtc);let clock=epoch+100,queries=0;
const device={device_id:'synthetic-native77',user_id:1,external_character_id:123,enabled:true,access_status:'active',auth_enabled:true,authorized_client_product:'GillionsGameSyncTest',version:'0.0.77.0',personal_observation_permissions:[TRAVEL_PERMISSION]};
const pool={query:async(sql,args)=>{assert.match(sql,/^SELECT/);queries++;const read=sql.includes('SELECT d.version');const owner=read?args[0]===1&&args[1]===123:args[1]===1&&args[2]===123;return {rows:owner?[{device_id:device.device_id,version:device.version}]:[]};}};
const store=createTravelContextStore({pool,reference:travelCatalogAdmission,now:()=>clock,schedule:()=>({}),cancel:()=>{}});
const routes=createPersonalIntakeRoutes({travelStore:store,store:{},environment:'test',httpsOrigin:'https://test.gillions.app'});
const ack=routes.acknowledgment(device,'personal-observations-v1',TRAVEL_CAPABILITY);
assert.equal(ack.resources.length,1);assert.equal(ack.resources[0].transportContract,'travel-context-v1');
assert.equal(routes.acknowledgment({...device,personal_observation_permissions:[]},'personal-observations-v1',TRAVEL_CAPABILITY),null);
assert.equal(routes.acknowledgment(device,'personal-observations-v1','wrong').resources.length,0);
const headers={'user-agent':'GillionsGameSyncTest/0.0.77.0','x-gillions-personal-resource':'travel_context','x-gillions-personal-capability':TRAVEL_CAPABILITY};
authorizeTravelHeaders({headers},device);validateTravelContext(payload,{now:clock,reference:travelCatalogAdmission});
for(const [d,status] of [[{...device,revoked_at:'revoked'},401],[{...device,personal_observation_permissions:[]},403],[{...device,authorized_client_product:'GillionsGameSync'},403]])assert.throws(()=>authorizePersonalDevice(d,'travel_context'),e=>e.statusCode===status);
assert.throws(()=>authorizeTravelHeaders({headers},{...device,version:'0.0.75.0'}),e=>e.statusCode===409);
const first=await store.accept(device,{nonce:'synthetic-first',payload});const retry=await store.accept(device,{nonce:'synthetic-first',payload});
assert.equal(first.unchanged,false);assert.equal(retry.unchanged,true);assert.equal(first.snapshotId,retry.snapshotId);assert.equal(first.receivedAt,retry.receivedAt);
await assert.rejects(store.accept(device,{nonce:'synthetic-first',payload:{...payload,mapX:11.1}}),e=>e.statusCode===409);
await assert.rejects(store.accept(device,{nonce:'synthetic-older',payload:{...payload,observedAtUtc:new Date(epoch-1).toISOString()}}),e=>e.statusCode===409);
assert.equal((await store.read(1,123)).state,'fresh');assert.equal((await store.read(2,123)).context,null);
clock=epoch+45000;assert.equal((await store.read(1,123)).context,null);
await assert.rejects(store.accept(device,{nonce:'synthetic-expired',payload}),e=>e.statusCode===409);
clock=epoch+46000;const newer={...payload,observedAtUtc:new Date(clock).toISOString()};await store.accept(device,{nonce:'synthetic-newer',payload:newer});store.revoke(device.device_id);assert.equal((await store.read(1,123)).context,null);store.close();
console.log(JSON.stringify({ok:true,synthetic:true,authenticatedHttp:false,databaseWrites:0,siteSource:'93007035e6cce8636e10a356dcda4fb07c3dfd1c',ack:{ok:true,acceptedClientProduct:'GillionsGameSyncTest',personalObservations:ack},first,retry,checks:'payload/ack/grant/version/receipts/conflict/older/isolation/expiry/revoke PASS',readOnlyQueriesSimulated:queries}));
'''
command = 'docker exec -i -w /app/apps/web ffxiv-gillions-test-web-1 node --input-type=module -e ' + __import__('shlex').quote(script)
result = subprocess.run(['ssh', 'gillions-codex', command], input=fixture.read_text(encoding='utf-8'), capture_output=True, text=True)
if result.returncode:
    raise RuntimeError('Shared TEST synthetic module validation failed; no payload/error body printed.')
evidence = json.loads(result.stdout)
class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        raise RuntimeError('Redirect rejected.')
request = urllib.request.Request('https://test.gillions.app/api/game-sync/sync', data=b'{}', headers={
    'Content-Type':'application/json', 'User-Agent':'GillionsGameSyncTest/0.0.77.0',
    'X-Gillions-Personal-Contract':'travel-context-v1', 'X-Gillions-Personal-Resource':'travel_context',
    'X-Gillions-Personal-Capability':'travel_context_v1'})
try:
    urllib.request.build_opener(NoRedirect()).open(request, timeout=10)
    raise AssertionError('Anonymous request accepted.')
except urllib.error.HTTPError as error:
    assert error.code == 401
evidence['actualHttps'] = {'normalTls': True, 'redirects': 'rejected', 'anonymousStatus': 401}
# Generated sanitized test evidence, never a real location/request-body archive.
proof.write_text(json.dumps(evidence, indent=2)+'\n', encoding='utf-8')
subprocess.run(['dotnet','run','--project',str(root/'tests/GillionsGameSync.TravelTests'),'-c','Release','--','--site-protocol',str(proof)],check=True)
print('Shared TEST exact Native/Site synthetic protocol + Native ack/receipt checks PASS; normal HTTPS anonymous401. No authenticated live FFXIV claim.')
