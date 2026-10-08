"""Read-only running TEST module cross-check, simulated SQL; NOT real game proof.

No Site writes, fixture accounts, credentials in evidence, or DB mutations.
"""
import json
import subprocess
import shlex
import urllib.request
import urllib.error
from pathlib import Path

root=Path(__file__).resolve().parents[1]
expected='1be7595aec3f9fa2b407b5dbdeec8b8e36e3eb3e'
labels=json.loads(subprocess.run(['ssh','gillions-codex',"docker inspect --format '{{json .Config.Labels}}' ffxiv-gillions-test-web-1"],check=True,capture_output=True,text=True).stdout)
assert labels['org.opencontainers.image.revision']==expected,'Rediscover changed TEST before validation.'
script=r'''
import assert from 'node:assert/strict';
import pg from 'pg';
import {createHuntNativeRequests} from './src/features/game-sync/hunt-native-requests.mjs';
import {HUNT_MAP_CAPABILITY,HUNT_MAP_PERMISSION,huntGuidanceState} from './src/hunt-guidance-model.mjs';
const actual=new pg.Pool({connectionString:process.env.DATABASE_URL});
let identity;try{identity=(await actual.query('SELECT current_database() AS database, max(version) AS head FROM application_control.schema_changes')).rows[0];}finally{await actual.end();}
assert.equal(identity.database,'ffxiv_gillions_test');assert.equal(identity.head,'0021');
let clock=Date.parse('2026-10-04T02:00:00Z');Date.now=()=>clock;
const id='00000000-0000-4000-8000-000000000001';
const device={device_id:id,user_id:1,external_character_id:99000001,authorized_client_product:'GillionsGameSyncTest'};
const pref={enabled:true,deviceId:id,mode:'original'};
let observations=[{schemaVersion:1,billTypeId:18,orderId:270,targets:[{targetId:695,requiredKills:1,observedKills:0,completed:false},{targetId:688,requiredKills:1,observedKills:0,completed:false}]}];
// Actual trusted Site routing builds the payload; SQL statements are simulated.
let current=huntGuidanceState(observations,{preference:pref,now:clock});assert.ok(current.revision);
let claimed=false,consumed=false,eligible=true;
const payload=()=>{const s=current,c=s.candidates[0],a=s.route.availability;return {huntTargetId:s.route.target.targetId,huntTargetName:s.route.reference.name.slice(0,120),availability:{classification:a.classification,fateId:a.fateId??null,fateName:a.fateName??null,activity:'UNKNOWN'},territoryId:s.territoryId,mapId:s.mapId,mapX:c.mapX,mapY:c.mapY,candidateId:c.candidateId,revision:s.revision,candidateIndex:0,candidateCount:s.candidates.length};};
const client={release(){},async query(sql,args){
 if(sql==='BEGIN'||sql==='COMMIT'||sql==='ROLLBACK'||sql.includes('pg_advisory_xact_lock'))return {rows:[],rowCount:0};
 if(sql.includes('SELECT a.ui_preferences'))return {rows:[{ui_preferences:{huntMapGuidance:{99000001:pref}}}],rowCount:1};
 if(sql.includes('SELECT observation_json'))return {rows:observations.map(x=>({observation_json:x})),rowCount:observations.length};
 if(sql.includes('SELECT device_id FROM game_sync_devices'))return {rows:eligible?[{device_id:id}]:[],rowCount:eligible?1:0};
 if(sql.includes('SET hunt_map_available_at=NOW()'))return {rows:[],rowCount:1};
 if(sql.includes('SET claimed_at=NOW()')){if(claimed||consumed||args[3]!==current.revision)return {rows:[],rowCount:0};claimed=true;return {rowCount:1,rows:[{request_id:id,claim_token:'T'.repeat(43),expires_at:'2026-10-04T02:01:30.000Z',hunt_payload:payload()}]};}
 if(sql.includes('target_revision IS DISTINCT FROM'))return {rows:[],rowCount:0};
 if(sql.includes('SET consumed_at=NOW()')&&sql.includes('request_id=$1')){const ok=claimed&&!consumed&&args[4]===current.revision; if(ok)consumed=true;return {rows:ok?[{request_id:id}]:[],rowCount:ok?1:0};}
 throw Error('Unexpected simulated query');
}};
const native=createHuntNativeRequests({pool:{connect:async()=>client},environment:'test'});
const request=await native.poll(device,{capability:HUNT_MAP_CAPABILITY},true);assert.equal(request.requestType,'hunt_map');
assert.equal(await native.poll(device,{capability:HUNT_MAP_CAPABILITY},true),null);
const body={requestType:'hunt_map',capability:HUNT_MAP_CAPABILITY,requestId:request.requestId,claimToken:request.claimToken,revision:request.revision};
assert.equal(await native.consume(device,{...body,revision:'b'.repeat(64)},true),false);
assert.equal(await native.consume(device,{...body,capability:'native_item_link'},true),false);
assert.equal(await native.consume(device,{...body,mapX:1},true),false);
assert.equal(await native.consume({...device,authorized_client_product:'GillionsGameSync'},body,true),false);
assert.equal(await native.consume(device,body,false),false);
eligible=false;assert.equal(await native.consume(device,body,true),false);eligible=true;
assert.equal(await native.consume(device,body,true),true);assert.equal(await native.consume(device,body,true),false);
// Actual Site checks current revision AFTER advancement, even before B is polled.
observations[0].targets[0]={...observations[0].targets[0],observedKills:1,completed:true};
current=huntGuidanceState(observations,{preference:pref,now:clock});assert.notEqual(current.revision,body.revision);
assert.equal(await native.consume(device,body,true),false);
claimed=false;consumed=false;const b=await native.poll(device,{capability:HUNT_MAP_CAPABILITY},true);assert.notEqual(b.revision,request.revision);
assert.equal(await native.consume(device,body,true),false);
assert.equal(await native.consume(device,{...body,revision:b.revision},true),true);
await assert.rejects(native.poll(device,{capability:HUNT_MAP_CAPABILITY,pluginVersion:'0.0.78.0'},true),e=>e.statusCode===400);
await assert.rejects(native.poll(device,{capability:HUNT_MAP_CAPABILITY},false),e=>e.statusCode===403);
assert.equal(HUNT_MAP_PERMISSION,'server:game-sync:receive:hunt-map:v1');
// Sanitize the synthetic claim token as well, despite not being a real credential.
request.claimToken='S'.repeat(43);
console.log(JSON.stringify({ok:true,siteSource:'1be7595aec3f9fa2b407b5dbdeec8b8e36e3eb3e',identity,simulatedSql:true,databaseWrites:0,authenticatedHttp:false,poll:{ok:true,request},consume:{ok:true,consumed:true},checks:'Site routing/poll/one-time claim/consume/strict fields/product/capability/permission/A-to-B stale race PASS'}));
'''
command='docker exec -i -w /app/apps/web ffxiv-gillions-test-web-1 node --input-type=module -e '+shlex.quote(script)
run=subprocess.run(['ssh','gillions-codex',command],check=False,capture_output=True,text=True)
if run.returncode: raise RuntimeError('Read-only TEST module cross-check failed: '+run.stderr[-1400:])
proof=json.loads(run.stdout)
class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self,*args,**kwargs): raise RuntimeError('Redirect rejected')
req=urllib.request.Request('https://test.gillions.app/api/game-sync/item-links/poll',data=b'{"capability":"native_hunt_map_v1"}',headers={'Content-Type':'application/json','User-Agent':'GillionsGameSyncTest/0.0.78.0'})
try: urllib.request.build_opener(NoRedirect()).open(req,timeout=15); raise AssertionError('Anonymous request accepted')
except urllib.error.HTTPError as e: assert e.code==401; proof['trustedTlsAnonymousStatus']=e.code
destination=root/'artifacts/verification/hunt-map/site-protocol-proof.json'
destination.parent.mkdir(parents=True,exist_ok=True)
destination.write_text(json.dumps(proof,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in proof.items() if k not in ('poll','consume')}))
