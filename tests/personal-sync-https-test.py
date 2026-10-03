"""Explicit shared-TEST API fixture acceptance; no direct DB/source mutations.

Uses the existing protected synthetic TEST member through normal login/pairing.
Credentials/cookies/codes/device tokens remain exclusively in remote memory.
Fixture reads are deliberately synthetic, NEVER live gameplay evidence.
"""
import argparse
import base64
import json
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument("--site-source", type=Path, required=True)
parser.add_argument("--run-test", action="store_true", required=True)
args = parser.parse_args()
fixtures = {r: json.loads((args.site_source / "apps/web/src/data" / f).read_text(encoding="utf-8")) for r, f in [
    ("hunt_bills", "test-hunt-bills-v1.json"), ("submarine_personal", "test-submarine-personal-v1.json")
]}
native_sub = Path(__file__).resolve().parents[1] / 'artifacts/verification/submarine-policy/submarine-personal-v1.json'
# Actual C# private-export fixture, not a hand-written approximation.
fixtures['submarine_personal'] = json.loads(native_sub.read_text(encoding='utf-8'))
encoded = base64.b64encode(json.dumps(fixtures).encode()).decode()
remote = r'''
import base64,copy,http.cookiejar,json,re,shlex,ssl,urllib.request,urllib.parse,urllib.error,uuid,datetime
from pathlib import Path
fixtures=json.loads(base64.b64decode("FIXTURES"))
origin="https://test.gillions.app"
secrets={}
for line in Path('/srv/ffxiv-gillions-test/secrets/test.env').read_text().splitlines():
 if line and not line.startswith('#') and '=' in line:
  k,v=line.split('=',1)
  if k in ('TEST_MEMBER_USERNAME','TEST_MEMBER_PASSWORD'): secrets[k]=shlex.split(v)[0]
assert len(secrets)==2,"protected TEST identity unavailable"
jar=http.cookiejar.CookieJar()
class NoRedirect(urllib.request.HTTPRedirectHandler):
 def redirect_request(self,*a,**k): return None
opener=urllib.request.build_opener(urllib.request.HTTPSHandler(context=ssl.create_default_context()),urllib.request.HTTPCookieProcessor(jar),NoRedirect())
def req(path,body=None,headers=None):
 h={'User-Agent':'GillionsGameSyncTest/0.0.74','Origin':origin,**(headers or {})}
 data=body if isinstance(body,bytes) or body is None else urllib.parse.urlencode(body).encode()
 if body is not None: h.setdefault('Content-Type','application/x-www-form-urlencoded')
 try:
  response=opener.open(urllib.request.Request(origin+path,data=data,headers=h),timeout=30)
 except urllib.error.HTTPError as e: response=e
 return response.code,response.read(512*1024).decode(),response.headers
def cookie(name): return next((c.value for c in jar if c.name==name),'')
def form(path,body): return req(path,{**body,'csrf_token':cookie('ffxiv_gillions_csrf')})
checks=0
def check(value,label):
 global checks
 assert value,label
 checks+=1;print('PASS '+label,flush=True)
status,html,_=req('/login')
csrf=re.search(r'name="preauth_csrf" value="([^"]+)"',html)
check(status==200 and csrf is not None,'trusted TLS/login CSRF')
status,_,_=req('/login',{'preauth_csrf':csrf[1],'identifier':secrets['TEST_MEMBER_USERNAME'],'password':secrets['TEST_MEMBER_PASSWORD'],'next':'/gillions-sync'})
check(status in (302,303) and bool(cookie('ffxiv_gillions_session')),'authenticated synthetic TEST member')
check(any(c.name=='ffxiv_gillions_session' and c.secure for c in jar),'secure session cookie')
status,html,_=req('/gillions-sync')
# Use an existing explicitly synthetic profile only; never create/link real IDs.
options=re.findall(r'<option value="(\d+)"[^>]*>([^<]*)</option>',html)
fixture=next((int(v) for v,label in options if 'Fixture Adventurer' in label),None)
check(status==200 and fixture is not None,'existing fixture profile selected without account mutation')
created=[]
def enroll(permissions):
 fields={'character':str(fixture),'redirect_to':'/gillions-sync','client_product':'GillionsGameSyncTest','testing_acknowledgment':'1',**{r:'1' for r in permissions}}
 status,_,_=form('/gillions-sync/pairing',fields)
 code=urllib.parse.unquote(cookie('ffxiv_gillions_game_sync_pairing'))
 check(status in (302,303) and bool(code),'explicit fixture pairing via member form')
 status,text,_=req('/api/game-sync/enroll',json.dumps({'version':'0.0.74'}).encode(),{'Content-Type':'application/json','Authorization':'Bearer '+code})
 device=json.loads(text);check(status in (200,201) and device.get('ok'),'fixture enrollment via standard API')
 created.append(device);return device
def sync(device,resource,payload,nonce):
 return req('/api/game-sync/sync',json.dumps({'resourceType':resource,'nonce':nonce,'payload':payload}).encode(),{'Content-Type':'application/json','X-Gillions-Personal-Contract':'personal-observations-v1',**({'Authorization':'Bearer '+device['token']} if device else {})})
try:
 device=enroll(fixtures)
 unpermitted=enroll([])
 presence={'schemaVersion':1,'contractVersion':1,'character':{'contentId':str(fixture),'name':'Fixture Adventurer','world':'Cactuar'},
  'observedAtUtc':datetime.datetime.now(datetime.timezone.utc).isoformat().replace('+00:00','Z'),
  'clientVersion':'0.0.74','clientProduct':'GillionsGameSyncTest','clientChannel':'testing',
  'capabilities':['retainer.observations.v1','retainer.results.v1','retainer.results.exact-ack.v1','retainer.presence.v1'],
  'autoRetainer':{'installed':False,'loaded':False,'apiReady':False,'suppressed':None,'multiModeEnabled':None,'characterEnabled':None,
   'retainerPlannerEnabled':False,'retainerPlannerReadiness':[],'plannerOptIn':False,'version':None,'maximumPlanExecutions':None,'supportedCompletionActions':[],'capabilities':[]},'appliedPlans':[]}
 status,text,_=req('/api/game-sync/presence',json.dumps(presence).encode(),{'Content-Type':'application/json','Authorization':'Bearer '+device['token'],'X-Gillions-Personal-Contract':'personal-observations-v1'})
 ack=json.loads(text).get('personalObservations',{})
 check(status==200 and ack.get('contractVersion')==1 and ack.get('endpoint')==origin+'/api/game-sync/sync','active exact personal presence contract/HTTPS endpoint')
 check({r['resourceType'] for r in ack.get('resources',[])}==set(fixtures),'both exact private resource permissions acknowledged')
 now=datetime.datetime.now(datetime.timezone.utc)
 for resource,payload in fixtures.items():
  for row in payload['bills' if resource=='hunt_bills' else 'slots']:
   row['observedAtUtc']=now.isoformat(timespec='milliseconds').replace('+00:00','Z')
   row['gameVersion']='2026.09.15.0000.0000';row['collectorVersion']='0.0.74.0'
   if resource=='hunt_bills': row['observationId']=uuid.uuid4().hex
   else:
    row['expectedReturnAtUtc']=(now+datetime.timedelta(days=1)).isoformat(timespec='milliseconds').replace('+00:00','Z')
    row['voyageState']='expected-in-flight'
  nonce=uuid.uuid4().hex
  status,text,_=sync(device,resource,payload,nonce);receipt=json.loads(text)
  check(status in (200,201) and receipt.get('ok') and bool(receipt.get('snapshotId')),resource+' HTTPS authenticated schema/receipt')
  status,text,_=sync(device,resource,payload,nonce)
  check(status in (200,201) and json.loads(text)==receipt,resource+' identical nonce/body same immutable receipt')
  changed=copy.deepcopy(payload)
  row=changed['bills' if resource=='hunt_bills' else 'slots'][0]
  if resource=='hunt_bills': row['observationId']=uuid.uuid4().hex
  else: row['currentExperience']+=1
  status,text,_=sync(device,resource,changed,nonce)
  check(status==409 and json.loads(text).get('code')=='NONCE_CONFLICT',resource+' nonce conflict fail closed')
  status,_,_=sync(None,resource,payload,uuid.uuid4().hex);check(status==401,resource+' anonymous rejection')
  status,_,_=sync(unpermitted,resource,payload,uuid.uuid4().hex);check(status==403,resource+' unpermitted device rejection')
  older=copy.deepcopy(payload)
  for row in older['bills' if resource=='hunt_bills' else 'slots']: row['observedAtUtc']=(now-datetime.timedelta(minutes=1)).isoformat(timespec='milliseconds').replace('+00:00','Z')
  status,text,_=sync(device,resource,older,uuid.uuid4().hex)
  check(status==200 and json.loads(text).get('unchanged'),resource+' older observation does not replace latest')
  status,page,h=req(('/hunts' if resource=='hunt_bills' else '/submarines')+'?character='+str(fixture))
  check(status==200 and 'no-store' in h.get('Cache-Control',''),resource+' private persisted page read')
  check('Synthetic preview' not in page,'fixture stored state selected without demo flag (NOT live evidence)')
 status,_,_=req('/hunts?character=999999999')
 check(status==403,'unowned character read rejection')
 for d in created:
  status,_,_=form('/gillions-sync/'+d['device_id']+'/revoke',{'redirect_to':'/gillions-sync'})
  check(status in (302,303),'fixture device revoked via normal member API')
  status,_,_=sync(d,'hunt_bills',fixtures['hunt_bills'],uuid.uuid4().hex)
  check(status==401,'revoked device rejection')
 print('HTTPS fixture checks passed: '+str(checks)+'; NOT live game/FC validation.',flush=True)
finally:
 for d in created:
  form('/gillions-sync/'+d['device_id']+'/revoke',{'redirect_to':'/gillions-sync'})
'''.replace("FIXTURES",encoded)
result=subprocess.run(['ssh','gillions-codex','python3','-'],input=remote,text=True,capture_output=True,timeout=180)
print(result.stdout)
if result.returncode:
    # No exception locals, raw response/page/configuration or credential output.
    print('HTTPS fixture acceptance failed; stage is the last PASS above. No secrets printed.')
    raise SystemExit(result.returncode)
