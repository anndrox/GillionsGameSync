using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using GillionsGameSync;
var dir=Path.GetFullPath(args[0]);
var input=JsonNode.Parse(File.ReadAllText(Path.Combine(dir,"hunt-input.json")))!;
var ids=input["items"]!.Deserialize<uint[]>()!;
var targets=input["targets"]!.Deserialize<HuntBillTarget[]>(new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
var current=JsonNode.Parse(File.ReadAllText(Path.Combine(dir,"support-contracts.json")))!["production"]!.DeepClone();
var legacy=JsonNode.Parse("""{"revision":"hunt-reference:legacy-20260915","nativeBuilds":[{"gameVersion":"2026.09.15.0000.0000","sdkVersion":"7.56.2.9136"}],"referenceBuilds":["2026.09.15.0000.0000"]}""")!;
const string generation="22222222-2222-4222-8222-222222222222";
var results=new List<object>();int assertions=0,passed=0,failed=0;
void Check(bool value,string message){assertions++;if(!value)throw new Exception(message);}
async Task Case(string name,Func<Task> run){try{await run();results.Add(new{name,result="PASS"});passed++;}catch(Exception e){results.Add(new{name,result="FAIL",error=e.Message});failed++;}}
string Presence(JsonNode? support,bool enabled=true,string gen=generation,Action<JsonObject>? mutate=null) {
    var now=DateTime.UtcNow;
    var permissions=new JsonObject();
    foreach(var key in new[]{"marketContribution","partyFinderContribution","personalHunts","personalSubmarines","huntRoutingLocation","itemLinks","partyFinderLinks","automaticHuntMaps"})
        permissions[key]=new JsonObject{["scope"]=key=="marketContribution"?"account":"device",["state"]="explicit",["enabled"]=key=="personalHunts"&&enabled,["generation"]=gen,["decidedAt"]=now.AddMinutes(-1)};
    var resource=new JsonObject{["resourceType"]="hunt_bills",["schemaVersion"]=2,["collectorSchema"]="hunt-bills-v2",["capability"]="hunt_bills_v2",["maxPayloadBytes"]=65536};
    if(support is not null)resource["support"]=support.DeepClone();
    var root=new JsonObject{["ok"]=true,["schemaVersion"]=1,["recommendedHeartbeatSeconds"]=15,["onlineWindowSeconds"]=60,["maximumBackoffSeconds"]=900,
        ["featureCompatibility"]=new JsonObject{["observations"]="supported",["results"]="supported"},["acceptedClientProduct"]="GillionsGameSync",["acceptedContractVersion"]=1,
        ["personalObservations"]=new JsonObject{["contractVersion"]=1,["endpoint"]="https://gillions.app/api/game-sync/sync",["resources"]=new JsonArray(resource)},
        ["permissionAuthority"]=new JsonObject{["revision"]=1,["deviceBinding"]=new JsonObject{["deviceId"]="11111111-1111-4111-8111-111111111111",["pairedAt"]=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc),["characterId"]=100},["issuedAt"]=now,["expiresAt"]=now.AddSeconds(30),["permissions"]=permissions}};
    mutate?.Invoke(root);return root.ToJsonString();
}
HuntBillItemCoverage coverage=new();
string Sample(Plugin plugin,int counter=0) {
    var now=DateTime.UtcNow;var mono=Stopwatch.GetTimestamp();var key=plugin.Character;
    var history=new HuntBillRetention{LocalRetentionEnabled=false};
    var policy=new HuntBillRetentionPolicy(history){SiteRetentionAuthorized=()=>plugin.Enabled};
    var rows=targets.Select(t=>t with{ObservedKills=counter}).ToArray();
    Check(policy.Observe(key,[new((byte)input["billTypeId"]!.GetValue<int>(),"daily",1,input["orderId"]!.GetValue<uint>(),ids[18],rows,now,NativeCompatibility.OctoberHotfix.GameBuild,"1.0.31.7"){SourceEvidence=HuntObservationAdmission.KeyItemEvidence}]),"actual retention refused permitted fixture");
    coverage=new();coverage.SetCatalog(ids.Select((id,i)=>new HuntBillItemDomain((byte)i,id,1,1,1000)).ToArray());
    HuntKeyItemSlot[] slots=[new(0,2004,false,ids[18],1),new(1,2004,false,0,0)];
    coverage.Observe(key,key,true,true,2004,slots.Length,slots,true,now,mono,NativeCompatibility.OctoberHotfix.GameBuild,"1.0.31.7",NativeCompatibility.OctoberHotfix.ClientStructsVersion);
    plugin.SetLive(coverage,policy);
    var payload=coverage.Payload(policy,key,now,mono)!;
    Check(PersonalSyncPolicy.HuntCoveragePayloadValid(payload),"actual serializer invalid");return payload;
}
async Task<PersonalPreparedSnapshot> Rejected(Plugin p,JsonNode? support=null) {
    await p.Presence(Presence(support));var body=Sample(p);var prep=p.Prepare(body)!;
    Check(prep is not null,"missing first preparation");await p.Send(prep!);
    Check(p.Handler.Calls==1&&prep!.Blocked,"actual HTTP400 sender did not terminally block");return prep!;
}
await Case("published HTTP400 classification and fresh timestamps remain blocked",async()=>{
    using var p=new Plugin();var old=await Rejected(p);
    var body=Sample(p);Check(HuntBillItemCoverage.SameStates(old.Payload,body),"fixture semantics changed");
    Check(!p.Current(old)&&ReferenceEquals(old,p.Prepare(body)),"unchanged blocked input escaped");
});
await Case("ordinary healthy same-authority presence renewals do not retry unchanged legacy support",async()=>{
    using var p=new Plugin();var old=await Rejected(p,legacy);
    for(int i=0;i<5;i++){await p.Presence(Presence(legacy));Check(ReferenceEquals(old,p.Prepare(Sample(p))),"unchanged contract cleared terminal stop");}
    Check(p.Handler.Calls==1,"renewal dispatched");
});
await Case("changed genuine authenticated support permits fresh immutable preparation and actual synthetic receipt",async()=>{
    using var p=new Plugin();var old=await Rejected(p);
    await p.Presence(Presence(current));var next=p.Prepare(Sample(p))!;
    Check(next is not null&&!ReferenceEquals(old,next)&&!next.Blocked&&next.Nonce!=old.Nonce,"changed supported contract did not recover published terminal preparation");
    Check(p.Current(next!),"recovery not dispatch-current");p.Handler.Status=201;await p.Send(next!);
    Check(next!.Acknowledged&&p.Handler.Calls==2,"actual sender/receipt did not acknowledge recovery");
    Check(old.Blocked&&HuntBillItemCoverage.SameStates(old.Payload,next.Payload),"old input edited/relabelled");
});
await Case("legacy-to-current exact build support recovers once and resists contract alternation",async()=>{
    using var p=new Plugin();var old=await Rejected(p,legacy);
    await p.Presence(Presence(current));var next=p.Prepare(Sample(p))!;
    Check(!ReferenceEquals(next,old)&&!next.Blocked,"known legacy-to-current contract did not recover");
    await p.Send(next);Check(next.Blocked&&p.Handler.Calls==2,"second actual400 not preserved");
    for(int i=0;i<4;i++){
        var alternative=current.DeepClone();alternative["revision"]="hunt-reference:"+new string(i%2==0?'a':'b',64);
        await p.Presence(Presence(alternative));Check(ReferenceEquals(next,p.Prepare(Sample(p))),"changed/alternating revisions created repeated unchanged-invalid send");
    }
});
await Case("already supported unchanged contract never grants a retry",async()=>{
    using var p=new Plugin();var old=await Rejected(p,current);await p.Presence(Presence(current));
    Check(ReferenceEquals(old,p.Prepare(Sample(p))),"same supported contract retried");
});
foreach(var status in new[]{401,403,404,409,413,415})await Case("HTTP"+status+" terminal stop cannot recover from support metadata",async()=>{
    using var p=new Plugin();p.Handler.Status=status;var old=await Rejected(p,legacy);await p.Presence(Presence(current));
    Check(ReferenceEquals(old,p.Prepare(Sample(p))),"non400 terminal stop reopened");
});
await Case("late old terminal response cannot block recovered preparation",async()=>{
    using var p=new Plugin();var old=await Rejected(p,legacy);await p.Presence(Presence(current));var next=p.Prepare(Sample(p))!;
    Check(!ReferenceEquals(old,next),"support recovery required before late-response check");
    p.Block(old);Check(!next.Blocked&&p.Current(next),"late old response poisoned new preparation");
});
var invalids=new Dictionary<string,Func<JsonNode?>>{
    ["missing support"]=()=>null,["null support"]=()=>JsonValue.Create((string?)null),
    ["revision only"]=()=>JsonNode.Parse("""{"revision":"hunt-reference:new"}"""),
    ["wrong native pair"]=()=>{var n=current.DeepClone();n["nativeBuilds"]![1]!["sdkVersion"]="7.57.0.9999";return n;},
    ["missing coverage reference"]=()=>{var n=current.DeepClone();n["referenceBuilds"]=new JsonArray("2026.09.15.0000.0000");return n;},
    ["unknown support field"]=()=>{var n=current.DeepClone();n["extra"]=true;return n;},
    ["duplicate native pair"]=()=>{var n=current.DeepClone();n["nativeBuilds"]!.AsArray().Add(n["nativeBuilds"]![0]!.DeepClone());return n;},
    ["unknown revision shape"]=()=>{var n=current.DeepClone();n["revision"]="unrelated";return n;},
};
foreach(var (name,make) in invalids)await Case(name+" cannot recover",async()=>{
    using var p=new Plugin();var old=await Rejected(p,legacy);await p.Presence(Presence(make()));
    Check(ReferenceEquals(old,p.Prepare(Sample(p))),"unsupported/malformed metadata cleared terminal stop");
});
await Case("support list/member ordering cannot manufacture changed contract",async()=>{
    using var p=new Plugin();var old=await Rejected(p,current);var n=current.DeepClone();
    n["nativeBuilds"]=new JsonArray(n["nativeBuilds"]!.AsArray().Reverse().Select(b=>b!.DeepClone()).ToArray());
    n["referenceBuilds"]=new JsonArray(n["referenceBuilds"]!.AsArray().Reverse().Select(b=>b!.DeepClone()).ToArray());
    await p.Presence(Presence(n));Check(ReferenceEquals(old,p.Prepare(Sample(p))),"array order manufactured recovery");
});
await Case("bill reference not advertised cannot use recovery",async()=>{
    using var p=new Plugin();var old=await Rejected(p,legacy);await p.Presence(Presence(current));var body=JsonNode.Parse(Sample(p))!;
    body["bills"]![0]!["gameVersion"]="2026.12.01.0000.0000";
    // Establish this distinct unsupported semantic input under legacy first.
    using var q=new Plugin();await q.Presence(Presence(legacy));var qbody=JsonNode.Parse(Sample(q))!;qbody["bills"]![0]!["gameVersion"]="2026.12.01.0000.0000";
    var blocked=q.Prepare(qbody.ToJsonString())!;q.Block(blocked);await q.Presence(Presence(current));
    Check(ReferenceEquals(blocked,q.Prepare(qbody.ToJsonString())),"unadvertised retained reference recovered");
});
await Case("explicit Off refuses preparation and queued sender despite changed support",async()=>{
    using var p=new Plugin();var old=await Rejected(p,legacy);await p.Presence(Presence(current,false));
    Check(!p.Enabled&&p.Prepare(old.Payload) is null,"Off authority bypassed");await p.Send(old);Check(p.Handler.Calls==1,"Off dispatched old queue");
});
await Case("revoked/invalidated authority refuses queued sender",async()=>{
    using var p=new Plugin();var old=await Rejected(p,legacy);p.Revoke();
    Check(!p.Enabled&&p.Prepare(old.Payload) is null,"revoked authority bypassed");await p.Send(old);Check(p.Handler.Calls==1,"revoked dispatched");
});
await Case("wrong authenticated response origin invalidates permission and never dispatches",async()=>{
    using var p=new Plugin();var old=await Rejected(p,legacy);await p.Presence(Presence(current),"https://example.invalid");
    Check(!p.Enabled&&p.Prepare(old.Payload) is null,"redirected origin granted recovery");await p.Send(old);Check(p.Handler.Calls==1,"wrong-origin dispatched");
});
await Case("permission decision transition already recovers published sender independently of support",async()=>{
    using var p=new Plugin();var old=await Rejected(p,legacy);await p.Presence(Presence(legacy,true,"33333333-3333-4333-8333-333333333333"));
    var next=p.Prepare(Sample(p))!;Check(next is not null&&!ReferenceEquals(old,next)&&!next.Blocked,"genuine new authority did not clear old cache");
});
await Case("failed presence then restored permission already clears published cache",async()=>{
    using var p=new Plugin();var old=await Rejected(p,legacy);await p.Presence("{}");Check(!p.Enabled,"bad presence retained grant");
    await p.Presence(Presence(current));var next=p.Prepare(Sample(p))!;
    Check(next is not null&&!ReferenceEquals(old,next)&&!next.Blocked,"restored authority not refreshed");
});
await Case("character isolation rejects old source and old permit",async()=>{
    using var p=new Plugin();var old=await Rejected(p,legacy);p.ChangeCharacter(456);Check(!p.Current(old),"old character preparation current");
    await p.Send(old);Check(p.Handler.Calls==1,"old character queued body dispatched");await p.Presence(Presence(current));
    var next=p.Prepare(Sample(p))!;Check(next.OwnerKey!=old.OwnerKey&&next.Nonce!=old.Nonce,"character identity/nonce reused");
});
await Case("session isolation rejects old source and old permit",async()=>{
    using var p=new Plugin();var old=await Rejected(p,legacy);p.ChangeSession();Check(!p.Current(old),"old session current");
    await p.Send(old);Check(p.Handler.Calls==1,"old session queued body dispatched");await p.Presence(Presence(current));
    var next=p.Prepare(Sample(p))!;Check(next.OwnerKey!=old.OwnerKey&&next.Nonce!=old.Nonce,"session identity/nonce reused");
});
await Case("Off during response cancels terminal commit",async()=>{
    using var p=new Plugin();await p.Presence(Presence(legacy));var prepared=p.Prepare(Sample(p))!;
    p.Handler.BeforeResponse=p.Revoke;await p.Send(prepared);Check(!prepared.Blocked&&!prepared.Acknowledged,"revoked in-flight response committed");
});
await Case("canceled worker never dispatches",async()=>{
    using var p=new Plugin();await p.Presence(Presence(current));var prepared=p.Prepare(Sample(p))!;await p.Send(prepared,true);
    Check(p.Handler.Calls==0&&!prepared.Blocked,"canceled queue dispatched/committed");
});
await Case("changed semantics have existing ordinary new preparation path",async()=>{
    using var p=new Plugin();var old=await Rejected(p,legacy);var next=p.Prepare(Sample(p,1))!;
    Check(!ReferenceEquals(old,next)&&!next.Blocked&&next.Nonce!=old.Nonce,"real progress cannot replace terminal input");
});
await Case("ignored older authority preserves newer lease and legacy support, then accepted support recovers once",async()=>{
    using var p=new Plugin();var old=await Rejected(p,legacy);
    var accepted=p.LatestIssued;var authority=p.AuthorityState;var support=p.SupportState;
    Check(!p.RecoverySpent,"budget spent before stale response");
    await p.Presence(Presence(current,mutate:r=>{
        r["permissionAuthority"]!["issuedAt"]=accepted.AddSeconds(-2);
        r["permissionAuthority"]!["expiresAt"]=accepted.AddSeconds(28);
    }));
    Check(p.Enabled&&p.AuthorityState==authority,"ignored response revoked/replaced newer On lease");
    Check(ReferenceEquals(old,p.Prepare(Sample(p)))&&!p.RecoverySpent,"ignored older authority response reopened terminal input or consumed budget");
    Check(p.SupportState==support,"ignored older authority response replaced current legacy support");
    await p.Presence(Presence(current));var next=p.Prepare(Sample(p))!;
    Check(!ReferenceEquals(old,next)&&!next.Blocked&&p.RecoverySpent,"accepted later support did not permit bounded recovery");
    p.Handler.Status=201;await p.Send(next);Check(next.Acknowledged&&p.Handler.Calls==2,"later genuine recovery did not receive fixture receipt");
});
await Case("stale support after accepted current support neither replaces it nor manufactures a later change",async()=>{
    using var p=new Plugin();var old=await Rejected(p,current);
    var accepted=p.LatestIssued;var authority=p.AuthorityState;var support=p.SupportState;
    await p.Presence(Presence(legacy,mutate:r=>{
        r["permissionAuthority"]!["issuedAt"]=accepted.AddSeconds(-2);
        r["permissionAuthority"]!["expiresAt"]=accepted.AddSeconds(28);
    }));
    Check(p.Enabled&&p.AuthorityState==authority&&p.SupportState==support,"stale legacy response overwrote accepted current authority/support");
    Check(!p.RecoverySpent&&ReferenceEquals(old,p.Prepare(Sample(p))),"stale response spent recovery budget");
    await p.Presence(Presence(current));Check(ReferenceEquals(old,p.Prepare(Sample(p)))&&!p.RecoverySpent,"same accepted current support manufactured recovery");
});
await Case("stale revisions and late old response preserve second terminal stop and spent budget",async()=>{
    using var p=new Plugin();var old=await Rejected(p,legacy);await p.Presence(Presence(current));
    var next=p.Prepare(Sample(p))!;await p.Send(next);Check(next.Blocked&&p.RecoverySpent,"second terminal stop missing");
    var accepted=p.LatestIssued;var authority=p.AuthorityState;var support=p.SupportState;
    for(int i=0;i<3;i++){
        var alt=current.DeepClone();alt["revision"]="hunt-reference:"+new string(i%2==0?'a':'b',64);
        await p.Presence(Presence(alt,mutate:r=>{
            r["permissionAuthority"]!["issuedAt"]=accepted.AddSeconds(-2);
            r["permissionAuthority"]!["expiresAt"]=accepted.AddSeconds(28);
        }));
        Check(p.AuthorityState==authority&&p.SupportState==support,"stale alternating support replaced accepted state");
        p.Block(old);Check(ReferenceEquals(next,p.Prepare(Sample(p)))&&p.RecoverySpent,"late old response/stale revision changed terminal budget");
    }
    var later=current.DeepClone();later["revision"]="hunt-reference:"+new string('c',64);
    await p.Presence(Presence(later));Check(ReferenceEquals(next,p.Prepare(Sample(p)))&&p.Handler.Calls==2,"accepted alternating revision granted second recovery");
});
Console.WriteLine(JsonSerializer.Serialize(new {fixtureOnly=true,sourceVariant="maintained-source",passed,failed,assertions,results},new JsonSerializerOptions{WriteIndented=true}));
return failed==0?0:1;
