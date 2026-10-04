"""Running TEST modules, simulated SQL only; no authenticated HTTP/game claim."""
import json, subprocess, shlex, urllib.request, urllib.error
from pathlib import Path
root=Path(__file__).resolve().parents[1]
expected='36327f9cf624ca44d535a30e71fade2853762e74'
labels=json.loads(subprocess.run(['ssh','gillions-codex',"docker inspect --format '{{json .Config.Labels}}' ffxiv-gillions-test-web-1"],check=True,capture_output=True,text=True).stdout)
assert labels['org.opencontainers.image.revision']==expected,'Rediscover changed TEST before validation.'
script=r'''
import assert from 'node:assert/strict';import pg from 'pg';
import {createHuntNativeRequests} from './src/features/game-sync/hunt-native-requests.mjs';
import {huntTargetKey} from './src/hunt-product-model.mjs';
const actual=new pg.Pool({connectionString:process.env.DATABASE_URL});let identity;
try{const c=await actual.connect();try{await c.query('BEGIN READ ONLY');identity=(await c.query('SELECT current_database() AS database,max(version) AS head FROM application_control.schema_changes')).rows[0];await c.query('ROLLBACK');}finally{c.release();}}finally{await actual.end();}
assert.equal(identity.database,'ffxiv_gillions_test');assert.equal(identity.head,'0025');
let clock=Date.parse('2026-10-04T02:00:00Z');const RealDate=Date;
globalThis.Date=class extends RealDate{constructor(...args){super(...(args.length?args:[clock]));}static now(){return clock;}};
const id='00000000-0000-4000-8000-000000000001',device={device_id:id,user_id:1,external_character_id:99000001,authorized_client_product:'GillionsGameSyncTest'};
let pref={enabled:true,deviceId:id,mode:'original'},capability='native_hunt_map_v1',row=null,route={done_targets:[]},eligible=true;
let observations=[{schemaVersion:1,billTypeId:18,orderId:270,targets:[{targetId:695,requiredKills:2,observedKills:0,completed:false},{targetId:688,requiredKills:1,observedKills:0,completed:false}]}];
// Mutation-shaped queries below reach only this in-memory pool, never PostgreSQL.
const client={release(){},async query(sql,p=[]){
 if(['BEGIN','COMMIT','ROLLBACK'].includes(sql)||sql.includes('pg_advisory_xact_lock'))return {rows:[],rowCount:0};
 if(sql.includes('SELECT a.ui_preferences'))return {rows:[{ui_preferences:{huntMapGuidance:{99000001:pref}}}],rowCount:1};
 if(sql.includes('SELECT observation_json'))return {rows:observations.map(x=>({observation_json:x})),rowCount:observations.length};
 if(sql.includes('SELECT * FROM user_hunt_route_state'))return {rows:[route],rowCount:1};
 if(sql.includes('UPDATE app_users')){pref=JSON.parse(p[2]);return {rows:[],rowCount:1};}
 if(sql.includes('SELECT hunt_map_capability'))return {rows:[{hunt_map_capability:capability}],rowCount:1};
 if(sql.includes('SELECT device_id,version'))return {rows:[],rowCount:0};
 if(sql.includes('SELECT d.device_id'))return {rows:eligible?[{device_id:id}]:[],rowCount:eligible?1:0};
 if(sql.includes('SELECT device_id FROM game_sync_devices')){const ok=eligible&&(!p[5]||p[5]===capability);return {rows:ok?[{device_id:id}]:[],rowCount:ok?1:0};}
 if(sql.includes('SET hunt_map_available_at=NOW()')){capability=p[2];return {rows:[{device_id:id}],rowCount:1};}
 if(sql.includes('SELECT * FROM game_sync_item_link_requests'))return {rows:row?[row]:[],rowCount:row?1:0};
 if(sql.includes('DELETE FROM game_sync_item_link_requests')){row=null;return {rows:[],rowCount:1};}
 if(sql.includes('INSERT INTO game_sync_item_link_requests')){row={request_id:p[0],user_id:p[1],external_character_id:p[2],claim_token:p[3],claim_hash:p[4],expires_at:new Date(clock+90000).toISOString(),created_at:new Date(clock).toISOString(),device_id:p[6],hunt_payload:JSON.parse(p[7]),target_revision:p[8],hunt_walk:p[9]?JSON.parse(p[9]):null,claimed_at:null,consumed_at:null};return {rows:[],rowCount:1};}
 if(sql.includes('INSERT INTO user_hunt_route_state')){route={...route,automatic_target:p[2],automatic_device:p[3],automatic_revision:p[4]};return {rows:[],rowCount:1};}
 if(sql.includes('SET claimed_at=NOW()')){const ok=row&&!row.claimed_at&&!row.consumed_at&&row.target_revision===p[3]&&Boolean(row.hunt_walk)===p[4]&&Date.parse(row.expires_at)>clock;if(ok)row.claimed_at=new Date(clock).toISOString();return {rows:ok?[row]:[],rowCount:ok?1:0};}
 if(sql.includes('SET consumed_at=NOW()')&&sql.includes('request_id=$1')){const ok=row&&row.request_id===p[0]&&row.target_revision===p[4]&&row.claim_hash===p[5]&&row.claimed_at&&!row.consumed_at&&Date.parse(row.expires_at)>clock;if(ok)row.consumed_at=new Date(clock).toISOString();return {rows:ok?[{request_id:row.request_id}]:[],rowCount:ok?1:0};}
 if(sql.includes('SET hunt_walk=$2')){row.hunt_walk=JSON.parse(p[1]);return {rows:[],rowCount:1};}
 if(sql.includes('target_revision=repeat')){if(row){row.consumed_at??=new Date(clock).toISOString();row.target_revision='0'.repeat(64);row.hunt_walk=null;}return {rows:[],rowCount:1};}
 throw Error('Unexpected simulated query');
}};
const native=createHuntNativeRequests({pool:{connect:async()=>client,query:(...a)=>client.query(...a)},environment:'test'});
const consume=r=>({requestType:'hunt_map',capability:r.contractVersion===2?'native_hunt_map_v2':'native_hunt_map_v1',requestId:r.requestId,claimToken:r.claimToken,revision:r.revision});
let s=await native.read(1,99000001);await native.edit(1,99000001,{action:'show',revision:s.revision});
const v1=await native.poll(device,{capability:'native_hunt_map_v1'},true);assert.equal(Object.keys(v1).length,15);assert.equal(await native.consume(device,consume(v1),true),true);assert.equal(await native.consume(device,consume(v1),true),false);
await native.poll(device,{capability:'native_hunt_map_v2'},true);clock+=6000;s=await native.read(1,99000001);await native.edit(1,99000001,{action:'show',revision:s.revision});
const ordinary=await native.poll(device,{capability},true);assert.equal(ordinary.progression,null);assert.equal(Object.keys(ordinary.request).length,20);assert.equal(ordinary.request.candidateKind,'ORDINARY_AREA');assert.equal(ordinary.request.candidateCount,1);
assert.equal(await native.consume(device,{...consume(ordinary.request),revision:'b'.repeat(64)},true),false);assert.equal(await native.consume(device,consume(ordinary.request),false),false);
eligible=false;assert.equal(await native.consume(device,consume(ordinary.request),true),false);eligible=true;
assert.equal(await native.consume(device,consume(ordinary.request),true),true);assert.equal(await native.consume(device,consume(ordinary.request),true),false);
await native.afterTravelAccepted(device);const watermark=JSON.stringify(route);observations[0].targets[0].observedKills=1;await native.afterTravelAccepted(device);assert.equal(JSON.stringify(route),watermark);assert.equal((await native.poll(device,{capability},true)).request,null);
await native.focus.renew(1,99000001);const focused=await native.focus.read(device);assert(focused.focused);clock+=31000;assert.equal((await native.focus.read(device)).focused,false);await native.focus.renew(1,99000001);assert.equal((await native.poll(device,{capability},true)).request,null);
observations=[{schemaVersion:1,billTypeId:21,orderId:312,targets:[{targetId:802,requiredKills:1,observedKills:0,completed:false}]}];await native.afterTravelAccepted(device);
const first=await native.poll(device,{capability},true);assert.equal(first.request.candidateKind,'DISCRETE_SPAWN_LOCATIONS');assert.equal(await native.consume(device,consume(first.request),true),true);const before=JSON.stringify(route);
clock+=6000;s=await native.read(1,99000001);const form={action:'next',target:huntTargetKey(s.route.bill,s.route.target),revision:first.request.revision,candidate_id:first.request.candidateId};
const advanced=await native.edit(1,99000001,form);assert.equal(advanced.progression,'advanced');assert.equal(JSON.stringify(route),before);await assert.rejects(native.edit(1,99000001,form),e=>e.statusCode===409);
const next=await native.poll(device,{capability},true);assert.equal(next.progression,null);assert.equal(next.request.candidateIndex,1);assert.notEqual(next.request.requestId,first.request.requestId);
assert.equal(await native.consume(device,consume(first.request),true),false);assert.equal(await native.consume(device,consume(next.request),true),true);assert.equal(await native.consume(device,consume(next.request),true),false);
row.hunt_walk.index=row.hunt_walk.candidateIds.length-1;row.hunt_payload.candidateIndex=row.hunt_walk.index;row.hunt_payload.candidateId=row.hunt_walk.candidateIds.at(-1);clock+=6000;s=await native.read(1,99000001);
const exhausted=await native.edit(1,99000001,{action:'next',target:huntTargetKey(s.route.bill,s.route.target),revision:s.revision,candidate_id:row.hunt_payload.candidateId});assert.equal(exhausted.progression,'exhausted');assert.equal(JSON.stringify(route),before);
native.focus.close();const clean=r=>({...r,claimToken:'S'.repeat(43)});
console.log(JSON.stringify({ok:true,siteSource:'36327f9cf624ca44d535a30e71fade2853762e74',identity,simulatedSql:true,databaseWrites:0,authenticatedHttp:false,poll:{ok:true,request:clean(v1)},pollV2:{ok:true,...ordinary,request:clean(ordinary.request)},pollNext:{ok:true,...next,request:clean(next.request)},focus:{ok:true,huntFocus:focused},consume:{ok:true,consumed:true},now:'2026-10-04T02:00:00Z',nextNow:'2026-10-04T02:00:43Z',checks:'Running Site V1/V2/consume/replay/stale/grants/partial/focus-expiry/browser Next/no-wrap/watermark PASS'}));
'''
run=subprocess.run(['ssh','gillions-codex','docker exec -i -w /app/apps/web ffxiv-gillions-test-web-1 node --input-type=module -e '+shlex.quote(script)],capture_output=True,text=True)
if run.returncode:raise RuntimeError('Read-only contract check failed: '+run.stderr[-1800:])
proof=json.loads(run.stdout)
class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self,*args,**kwargs):raise RuntimeError('Redirect rejected')
for capability in ('native_hunt_map_v1','native_hunt_map_v2'):
    request=urllib.request.Request('https://test.gillions.app/api/game-sync/item-links/poll',data=json.dumps({'capability':capability}).encode(),headers={'Content-Type':'application/json','User-Agent':'GillionsGameSyncTest/0.0.81.0'})
    try:urllib.request.build_opener(NoRedirect()).open(request,timeout=15);raise AssertionError('Anonymous accepted')
    except urllib.error.HTTPError as error:assert error.code==401
proof['trustedTlsAnonymousStatus']=401
destination=root/'artifacts/verification/hunt-map/site-protocol-proof.json';destination.parent.mkdir(parents=True,exist_ok=True)
destination.write_text(json.dumps(proof,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in proof.items() if k not in ('poll','pollV2','pollNext','focus','consume')}))
