using GillionsGameSync;
using System.Text.Json;
using System.Text.Json.Nodes;

var now = new DateTime(2026, 10, 4, 2, 0, 0, DateTimeKind.Utc);
var count = 0;
void Check(bool value, string name) { count++; if (!value) throw new Exception(name); }
JsonObject Fixture(string revision = "a", string? id = null) => new() {
    ["ok"] = true, ["request"] = new JsonObject {
        ["requestType"] = "hunt_map", ["requestId"] = id ?? Guid.NewGuid().ToString("D"), ["claimToken"] = new string('T', 43),
        ["huntTargetId"] = 4, ["huntTargetName"] = "Daddy Longlegs", ["territoryId"] = 140, ["mapId"] = 20,
        ["mapX"] = 14.41, ["mapY"] = 6.84, ["candidateId"] = new string('c', 24), ["revision"] = new string(revision[0], 64),
        ["candidateIndex"] = 0, ["candidateCount"] = 1, ["expiresAt"] = now.AddSeconds(90).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
        ["availability"] = new JsonObject { ["classification"] = "FATE_REQUIRED", ["fateId"] = 366, ["fateName"] = "He's Got Legs", ["activity"] = "UNKNOWN" },
    },
};
HuntMapRequest? Parse(JsonObject x) => HuntMapPolicy.Parse(x.ToJsonString(), now);
var aJson = Fixture(); var a = Parse(aJson)!;
Check(a != null && a.MapX == 14.41f && a.MapY == 6.84f && a.CandidateIndex == 0, "exact Site payload XY/order");
Check(a!.Guidance.Contains("FATE required: He's Got Legs") && a.Guidance.Contains("not currently known")
    && a.Guidance.Contains("not a sighting") && !a.Guidance.Contains("FATE active"), "honest FATE reference");
Check(typeof(HuntMapRequest).GetMethod("ToString")!.DeclaringType == typeof(object), "no token ToString");
foreach (var field in aJson["request"]!.AsObject().Select(x => x.Key).ToArray()) {
    var x = Fixture(); x["request"]!.AsObject().Remove(field); Check(Parse(x) == null, "missing " + field);
    x = Fixture(); x["request"]![field] = null; Check(Parse(x) == null, "null " + field);
}
foreach (var field in new[] { "classification", "fateId", "fateName", "activity" }) {
    var x = Fixture(); x["request"]!["availability"]!.AsObject().Remove(field); Check(Parse(x) == null, "availability missing " + field);
}
foreach (var (field, value) in new (string, JsonNode?)[] {
    ("requestType", "item"), ("requestType", "party_finder"), ("requestType", "teleport"), ("capability", "wrong"),
    ("requestId", "not UUID"), ("requestId", Guid.Empty.ToString("D")), ("claimToken", "bad\nclaim"),
    ("claimToken", ""), ("claimToken", new string('x',101)), ("revision", "wrong"), ("revision", new string('A',64)),
    ("candidateId", new string('a',23)), ("candidateIndex", 1), ("candidateIndex", -1), ("candidateCount", 0),
    ("huntTargetId", 0), ("mapId", 0), ("territoryId", 0), ("mapX", -0.1), ("mapY", 100.1),
    ("mapX", "12.5"), ("huntTargetName", "\u0002remote payload"), ("huntTargetName", new string('x',121)),
    ("expiresAt", now.ToString("yyyy-MM-ddTHH:mm:ssZ")), ("expiresAt", now.AddSeconds(91).ToString("yyyy-MM-ddTHH:mm:ssZ")),
    ("expiresAt", now.AddSeconds(90).ToString("o").Replace("Z","+00:00")), ("userId", 1), ("characterId", 1),
}) { var x = Fixture(); x["request"]![field] = value?.DeepClone(); Check(Parse(x) == null, "reject " + field); }
foreach (var (field, value) in new (string, JsonNode?)[] { ("activity","ACTIVE"), ("classification","bogus"),
    ("fateId",0), ("fateName",null), ("fateName",new string('x',161)), ("extra",true) }) {
    var x = Fixture(); x["request"]!["availability"]![field] = value?.DeepClone(); Check(Parse(x) == null, "reject availability " + field);
}
foreach (var extra in new[] { "nativeRequests", "acceptedClientProduct", "capability", "schemaVersion" }) {
    var x = Fixture(); x[extra] = "GillionsGameSync"; Check(Parse(x) == null, "not invented ack " + extra);
}
foreach(var json in new[] {"{", "[]", "{\"ok\":true,\"request\":null}", "{\"ok\":false,\"request\":null}",
    aJson.ToJsonString().Replace("\"ok\":true", "\"ok\":true,\"ok\":true"), new string('x',4097) })
    Check(HuntMapPolicy.Parse(json,now)==null,"malformed/null/duplicate/oversized");
foreach (var cls in new[] { "ALWAYS_AVAILABLE", "CONDITIONAL", "UNKNOWN" }) {
    var x=Fixture(); x["request"]!["availability"]!["classification"]=cls; x["request"]!["availability"]!["fateId"]=null; x["request"]!["availability"]!["fateName"]=null;
    var r=Parse(x); Check(r!=null && !r.Guidance.Contains("FATE active"),"classification " + cls);
}
Check(HuntMapPolicy.Consumed("{\"ok\":true,\"consumed\":true}"),"exact consume ack");
foreach(var json in new[] {"{}","{\"ok\":true,\"consumed\":false}","{\"ok\":false,\"consumed\":true}",
    "{\"ok\":true,\"consumed\":true,\"requestId\":\"extra\"}","{\"ok\":true,\"ok\":true,\"consumed\":true}","null"})
    Check(!HuntMapPolicy.Consumed(json),"bad consume ack");
for(int mask=0;mask<8;mask++) foreach(var origin in new[] { HuntMapPolicy.Origin,"http://test.gillions.app","https://10.10.2.1","https://gillions.app","https://test.gillions.app:443","https://test.gillions.app.attacker.invalid" })
    Check(HuntMapPolicy.Admit((mask&1)!=0,(mask&2)!=0,origin,(mask&4)!=0)==(mask==7&&origin==HuntMapPolicy.Origin),"independent consent/pair/session/origin");

// Server authority simulation: opaque revisions are not Native-derived or sortable.
var processor=new HuntMapProcessor(); var current=a.Revision; var allowed=true; var opens=new List<string>(); var consumes=0;
Task<bool> Permit()=>Task.FromResult(allowed);
Task<bool> Consume(HuntMapRequest r) { consumes++; return Task.FromResult(r.Revision==current); }
Task<bool> Present(HuntMapRequest r) { opens.Add(r.Revision); return Task.FromResult(true); }
Check((await processor.ProcessAsync(a,()=>now,Permit,Consume,Present)).Contains("shown")&&opens.Count==1,"A first");
Check((await processor.ProcessAsync(a,()=>now,Permit,Consume,Present)).Contains("already attempted")&&opens.Count==1&&consumes==1,"repeat A");
// Same revision, new claim is Site-authorized manual retry. Auto duplicates are
// prevented by the server's durable revision watermark, not by guessing a mode.
var manual=Parse(Fixture())!;
Check((await processor.ProcessAsync(manual,()=>now,Permit,Consume,Present)).Contains("shown")&&opens.Count==2,"manual same revision");
var b=Parse(Fixture("b"))!; current=b.Revision;
var stale=Parse(Fixture())!;
Check((await processor.ProcessAsync(stale,()=>now,Permit,Consume,Present)).Contains("rejected")&&opens.Count==2,"A invalid after authoritative B before B poll");
Check((await processor.ProcessAsync(b,()=>now,Permit,Consume,Present)).Contains("shown")&&opens.Count==3,"B exactly once");
Check((await processor.ProcessAsync(Parse(Fixture())!,()=>now,Permit,Consume,Present)).Contains("rejected")&&opens.Count==3,"late A after B");
Check((await processor.ProcessAsync(b,()=>now,Permit,Consume,Present)).Contains("already attempted")&&opens.Count==3,"repeated B");
// Native-controlled partial progress cannot create a request or replay an old
// one. New Site request IDs (including explicit manual Show) still work.
var progressProcessor = new HuntMapProcessor(); int progressOpens = 0, progressConsumes = 0;
Task<bool> ProgressConsume(HuntMapRequest r) { progressConsumes++; return Task.FromResult(true); }
Task<bool> ProgressPresent(HuntMapRequest r) { progressOpens++; return Task.FromResult(true); }
Check((await progressProcessor.ProcessAsync(a,()=>now,Permit,ProgressConsume,ProgressPresent)).Contains("shown"), "Initial current target not shown once.");
for (int kills = 0; kills <= 1; kills++)
    Check((await progressProcessor.ProcessAsync(a,()=>now,Permit,ProgressConsume,ProgressPresent)).Contains("already attempted")
        && progressOpens == 1 && progressConsumes == 1, "Partial0/2->1/2 replayed consumed request.");
Check((await progressProcessor.ProcessAsync(Parse(Fixture())!,()=>now,Permit,ProgressConsume,ProgressPresent)).Contains("shown") && progressOpens == 2,
    "Explicit manual same-target Show incorrectly deduped.");
Check((await progressProcessor.ProcessAsync(b,()=>now,Permit,ProgressConsume,ProgressPresent)).Contains("shown") && progressOpens == 3,
    "Next authoritative target blocked.");
var changed=Fixture("c"); changed["request"]!["candidateId"]=new string('d',24); changed["request"]!["candidateCount"]=80;
var c=Parse(changed)!;current=c.Revision;
Check(c.CandidateCount==80&&(await processor.ProcessAsync(c,()=>now,Permit,Consume,Present)).Contains("shown"),"new candidate revision");
foreach(var lifecycle in new[]{"logout","re-pair","revocation","unload","OFF","capability loss"}) {
    allowed=false;var before=opens.Count;var n=consumes;
    Check(!(await processor.ProcessAsync(Parse(Fixture("c"))!,()=>now,Permit,Consume,Present)).Contains("shown")&&opens.Count==before&&consumes==n,lifecycle);
}
allowed=true;
var beforeCancelled=opens.Count;
Check((await processor.ProcessAsync(Parse(Fixture("c"))!,()=>now,Permit,r=>{allowed=false;return Task.FromResult(true);},Present)).Contains("cancelled")&&opens.Count==beforeCancelled,"OFF after consume");
allowed=true;
Check((await processor.ProcessAsync(Parse(Fixture("c"))!,()=>now,Permit,r=>{now=now.AddSeconds(90);return Task.FromResult(true);},Present)).Contains("cancelled")&&opens.Count==beforeCancelled,"expired after consume");
// A lost response burns the attempt even if the remote consume committed.
now=now.AddSeconds(-90); var lost=Parse(Fixture("c"))!;
try { await processor.ProcessAsync(lost,()=>now,Permit,r=>throw new HttpRequestException(),Present); Check(false,"lost response must throw"); } catch(HttpRequestException) { Check(true,"lost response"); }
Check((await processor.ProcessAsync(lost,()=>now,Permit,Consume,Present)).Contains("already attempted"),"no uncertain replay");
for(int i=0;i<300;i++) await processor.ProcessAsync(Parse(Fixture("c"))!,()=>now,Permit,Consume,Present);
Check(((System.Collections.ICollection)typeof(HuntMapProcessor).GetField("order",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(processor)!).Count==128,"bounded RAM watermark");
// Additive V2 uses only the Site-selected candidate; ordinary/FATE one anchor,
// B-rank intentional browser Next as a new normal request (progression:null).
JsonObject V2(string kind = "ORDINARY_AREA", int index = 0, int total = 1, string revision = "a") {
    var x = Fixture(revision); x["progression"] = null; var r = x["request"]!;
    r["contractVersion"] = 2; r["candidateKind"] = kind;
    r["walkExpiresAt"] = now.AddSeconds(-600).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    r["candidateSetTruncated"] = false; r["recommendedAetheryte"] = new JsonObject { ["aetheryteId"] = 3, ["name"] = "Bentbranch Meadows" };
    r["candidateIndex"] = index; r["candidateCount"] = total;
    if (kind != "FATE_REQUIRED") { r["availability"]!["classification"] = "ALWAYS_AVAILABLE"; r["availability"]!["fateId"] = null; r["availability"]!["fateName"] = null; }
    return x;
}
HuntMapRequest? Parse2(JsonObject x) => HuntMapPolicy.TryPoll(x.ToJsonString(), now, HuntMapPolicy.CapabilityV2, out var r) ? r : null;
var ordinary2 = Parse2(V2())!;
Check(ordinary2.ContractVersion == 2 && ordinary2.Capability == HuntMapPolicy.CapabilityV2 && ordinary2.CandidateCount == 1, "V2 ordinary exact single anchor");
Check(ordinary2.Guidance.Contains("primary reference anchor") && !ordinary2.Guidance.Contains("alternative cycling"), "V2 honest ordinary guidance");
Check(Parse2(V2("FATE_REQUIRED"))!.Guidance.Contains("activity not currently known"), "V2 FATE reference no fake activity");
var brank2 = Parse2(V2("DISCRETE_SPAWN_LOCATIONS", 3, 8))!;
Check(brank2.CandidateIndex == 3 && brank2.Guidance.Contains("Possible location 4 of 8"), "Site B-rank specific candidate");
Check(brank2.WalkExpiresAtUtc < now && HuntMapPolicy.Valid(brank2, now), "expired historical walk metadata does not erase valid cursor/request");
Check(HuntMapPolicy.Parse(V2().ToJsonString(), now) is null, "V1 never accepts richer V2");
Check(Parse2(Fixture()) is null, "V2 never reconstructs missing V1 fields");
Check(HuntMapPolicy.TryPoll("{\"ok\":true,\"request\":null,\"progression\":null}",now,HuntMapPolicy.CapabilityV2,out var none2) && none2 is null, "new Site valid empty V2 negotiation");
Check(!HuntMapPolicy.TryPoll(V2().ToJsonString(),now,"arbitrary_capability",out _), "unsupported capability");
foreach (var field in V2()["request"]!.AsObject().Select(p => p.Key)) {
    var x = V2(); x["request"]!.AsObject().Remove(field); Check(Parse2(x) is null,"missing V2 " + field);
}
foreach (var (field,value) in new (string,JsonNode?)[] {
    ("contractVersion",1),("contractVersion",3),("contractVersion","2"),("candidateKind","LIVE_SIGHTING"),
    ("candidateKind",null),("walkExpiresAt",null),("walkExpiresAt","2026-10-04T02:00:00+00:00"),
    ("candidateSetTruncated","false"),("candidateSetTruncated",null),("candidateIndex",-1),("candidateIndex",128),
    ("candidateCount",0),("candidateCount",129),("candidateCount",2),("candidateIndex",1),("accountId",1)
}) { var x=V2(); x["request"]![field]=value?.DeepClone(); Check(Parse2(x) is null,"malformed V2 " + field); }
foreach(var kind in new[]{"ORDINARY_AREA","FATE_REQUIRED"}) {
    Check(Parse2(V2(kind,1,2)) is null,"no multiple ordinary/FATE areas");
}
for(int total=1;total<=128;total*=2) {
    Check(Parse2(V2("DISCRETE_SPAWN_LOCATIONS",total-1,total)) is not null,"bounded last B-rank candidate " + total);
    Check(Parse2(V2("DISCRETE_SPAWN_LOCATIONS",total,total)) is null,"B-rank out of bounds/no wrap " + total);
}
foreach(var (field,value) in new (string,JsonNode?)[]{("aetheryteId",0),("aetheryteId","3"),("name",new string('x',121)),("name","bad\nname"),("extra",1)}) {
    var x=V2();x["request"]!["recommendedAetheryte"]![field]=value?.DeepClone();Check(Parse2(x) is null,"V2 recommended " + field);
}
var noRecommendation=V2();noRecommendation["request"]!["recommendedAetheryte"]=null;
Check(Parse2(noRecommendation) is not null,"optional recommendation null");
foreach(var progression in new[]{"advanced","exhausted","bogus"}) {
    var x=V2();x["progression"]=progression;Check(Parse2(x) is null,"normal Native poll cannot silently invoke device Next " + progression);
}
var negotiation=new HuntMapNegotiation();
Check(negotiation.Capability(now)==HuntMapPolicy.CapabilityV2,"probe additive V2");
Check(negotiation.LegacyEmpty("{\"ok\":true,\"request\":null}",now),"old Site exact empty fallback");
Check(!negotiation.LegacyEmpty(Fixture().ToJsonString(),now),"never reinterpret a claimed V1 response during V2 probe");
Check(!negotiation.LegacyEmpty("{\"ok\":true,\"request\":null,\"progression\":null}",now),"V2 empty not legacy");
negotiation.Unsupported(now);
Check(negotiation.Capability(now.AddSeconds(299))==HuntMapPolicy.Capability,"bounded old-Site V1 compatibility");
Check(negotiation.Capability(now.AddMinutes(5))==HuntMapPolicy.CapabilityV2,"bounded rediscovery after Site upgrade");
negotiation.Reset(); Check(negotiation.Capability(now)==HuntMapPolicy.CapabilityV2,"new session negotiation reset");

string Focus(bool active,DateTime? expiry=null)=>new JsonObject { ["ok"]=true,["huntFocus"]=new JsonObject {
    ["contract"]=HuntFocusState.Contract,["focused"]=active,["expiresAt"]=expiry?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
}}.ToJsonString();
var focus=new HuntFocusState(); var lease=Focus(true,now.AddSeconds(30));
Check(focus.Apply(lease,now,true)&&focus.Active(now,true),"eligible explicit focus accepted");
Check(!focus.Active(now,false),"focus never grants missing separate permission");
Check(!focus.Active(now.AddSeconds(30),true),"hard TTL expiration without network response");
Check(focus.Apply(Focus(false),now,true)&&focus.Supported&&!focus.Active(now,true),"false lease ordinary behavior");
Check(!focus.Apply(lease,now,false)&&!focus.Supported,"OFF cannot admit lease");
foreach(var invalid in new[]{"{}","null","{",new string('x',131073),Focus(true),Focus(false,now.AddSeconds(30)),
    Focus(true,now),Focus(true,now.AddSeconds(-1)),Focus(true,now.AddSeconds(31)),
    lease.Replace("active_hunt_focus_v1","untrusted"),lease.Replace("\"focused\":true","\"focused\":\"true\""),
    lease.Replace("\"contract\":","\"extra\":true,\"contract\":"),lease.Replace("\"ok\":true","\"ok\":false"),
    lease.Replace("\"huntFocus\":", "\"huntFocus\":null,\"huntFocus\":")}) {
    Check(!focus.Apply(invalid,now,true)&&!focus.Active(now,true),"malformed/missing/expired lease fails closed");
}
Check(focus.Apply(lease,now,true),"focus restart eligible");
for(int mask=0;mask<8;mask++) Check(focus.Active(now,mask==7)==(mask==7),"focus requires independently combined local/pair/server permission " + mask);
var v2Processor=new HuntMapProcessor();var v2Opened=0;var v2Consumed=0;
Task<bool> V2Permit()=>Task.FromResult(true);Task<bool> V2Consume(HuntMapRequest r){v2Consumed++;return Task.FromResult(true);}
Task<bool> V2Present(HuntMapRequest r){v2Opened++;return Task.FromResult(true);}
Check((await v2Processor.ProcessAsync(ordinary2,()=>now,V2Permit,V2Consume,V2Present)).Contains("shown"),"V2 A first once");
foreach(var change in new[]{"12-second focus renewal","partial0/2->1/2","same-target travel","same-target rerank","transient reconnect","focus expiry/resume"}) {
    focus.Clear();focus.Apply(lease,now,true);
    Check((await v2Processor.ProcessAsync(ordinary2,()=>now,V2Permit,V2Consume,V2Present)).Contains("already attempted")&&v2Opened==1&&v2Consumed==1,"no replay " + change);
}
Check((await v2Processor.ProcessAsync(Parse2(V2())!,()=>now,V2Permit,V2Consume,V2Present)).Contains("shown")&&v2Opened==2,"explicit Site manual Show new ID not over-deduped");
Check((await v2Processor.ProcessAsync(Parse2(V2(revision:"b"))!,()=>now,V2Permit,V2Consume,V2Present)).Contains("shown")&&v2Opened==3,"A->B opens B once");
Check((await v2Processor.ProcessAsync(brank2,()=>now,V2Permit,V2Consume,V2Present)).Contains("shown"),"B-rank first selected candidate");
var next2=Parse2(V2("DISCRETE_SPAWN_LOCATIONS",4,8))!;
Check((await v2Processor.ProcessAsync(next2,()=>now,V2Permit,V2Consume,V2Present)).Contains("shown")&&v2Opened==5,"browser Next distinct normal command presents next candidate");
Check((await v2Processor.ProcessAsync(next2,()=>now,V2Permit,V2Consume,V2Present)).Contains("already attempted")&&v2Opened==5,"browser Next duplicate no replay");
Check((await v2Processor.ProcessAsync(Parse2(V2())!,()=>now,V2Permit,r=>Task.FromResult(false),V2Present)).Contains("rejected")&&v2Opened==5,"409 false receipt never presented");
// Actual linked transport and stream reader, not HttpClient's header-only
// timeout. One real10s body deadline, plus short known-expiry/lifecycle tests.
var flight = false;
var elapsed = System.Diagnostics.Stopwatch.StartNew();
try {
    flight=true;
    using var stalledPoll=new StreamContent(new StalledStream());
    await HuntMapTransport.ReadAsync(stalledPoll,CancellationToken.None);
    Check(false,"stalled poll must timeout");
} catch(OperationCanceledException) {
    Check(elapsed.Elapsed>=TimeSpan.FromSeconds(9)&&elapsed.Elapsed<TimeSpan.FromSeconds(14),"finite stalled poll body deadline");
} finally { flight=false; }
Check(!flight&&opens.Count==304,"stalled poll releases flight/no map");
var stalledClaim=Parse(Fixture("c"))!;var stalledConsumes=0;var stalledOpens=0;var stalledProcessor=new HuntMapProcessor();
try {
    flight=true;
    await stalledProcessor.ProcessAsync(stalledClaim,()=>now,Permit,async r=>{
        stalledConsumes++;
        using var deadline=HuntMapTransport.Deadline(CancellationToken.None,CancellationToken.None,DateTime.UtcNow.AddMilliseconds(100));
        using var content=new StreamContent(new StalledStream());
        await HuntMapTransport.ReadAsync(content,deadline.Token);return true;
    },r=>{stalledOpens++;return Task.FromResult(true);});
    Check(false,"stalled consume must cancel at claim expiry");
} catch(OperationCanceledException) { Check(true,"stalled consume expiry"); } finally { flight=false; }
Check(!flight&&stalledConsumes==1&&stalledOpens==0,"stalled consume releases flight/no map");
Check((await stalledProcessor.ProcessAsync(stalledClaim,()=>now,Permit,Consume,Present)).Contains("already attempted")&&stalledConsumes==1,"no uncertain stalled consume retry");
using(var cancel=new CancellationTokenSource()) {
    using var deadline=HuntMapTransport.Deadline(cancel.Token,CancellationToken.None);cancel.Cancel();
    Check(deadline.IsCancellationRequested,"session cancel deadline");
}
using(var cancel=new CancellationTokenSource()) {
    using var deadline=HuntMapTransport.Deadline(CancellationToken.None,cancel.Token);cancel.Cancel();
    Check(deadline.IsCancellationRequested,"consent cancel deadline");
}
using(var deadline=HuntMapTransport.Deadline(CancellationToken.None,CancellationToken.None,DateTime.UtcNow.AddMilliseconds(-1))) Check(deadline.IsCancellationRequested,"past claim cancels immediately");
using(var deadline=HuntMapTransport.Deadline(CancellationToken.None,CancellationToken.None)) {
    var requestElapsed=System.Diagnostics.Stopwatch.StartNew();
    try { await Task.Delay(Timeout.InfiniteTimeSpan,deadline.Token); Check(false,"whole turn deadline"); }
    catch(OperationCanceledException) { Check(requestElapsed.Elapsed>=TimeSpan.FromSeconds(14)&&requestElapsed.Elapsed<TimeSpan.FromSeconds(20),"15s whole turn deadline"); }
}
using(var oversize=new ByteArrayContent(new byte[4097])) {
    try { await HuntMapTransport.ReadAsync(oversize,CancellationToken.None);Check(false,"oversize"); } catch(InvalidOperationException) { Check(true,"bounded response length"); }
}
using(var good=new StringContent("{\"ok\":true,\"consumed\":true}")) Check(HuntMapPolicy.Consumed(await HuntMapTransport.ReadAsync(good,CancellationToken.None)),"normal streamed consume");
if(args.Length==2&&args[0]=="--site-protocol") {
    var proof=JsonNode.Parse(File.ReadAllText(args[1]))!.AsObject();
    var siteNow=DateTime.Parse(proof["now"]!.GetValue<string>()).ToUniversalTime();
    Check(HuntMapPolicy.Parse(proof["poll"]!.ToJsonString(),siteNow)!=null,"running Site generated V1 payload");
    Check(HuntMapPolicy.TryPoll(proof["pollV2"]!.ToJsonString(),siteNow.AddSeconds(6),HuntMapPolicy.CapabilityV2,out var siteOrdinary)&&siteOrdinary?.CandidateKind=="ORDINARY_AREA","running Site generated V2 ordinary anchor");
    Check(HuntMapPolicy.TryPoll(proof["pollNext"]!.ToJsonString(),DateTime.Parse(proof["nextNow"]!.GetValue<string>()).ToUniversalTime(),HuntMapPolicy.CapabilityV2,out var siteNext)&&siteNext?.CandidateIndex==1,"running Site browser Next normal V2 command");
    var siteFocus=new HuntFocusState();
    Check(siteFocus.Apply(proof["focus"]!.ToJsonString(),siteNow.AddSeconds(6),true)&&siteFocus.Active(siteNow.AddSeconds(6),true),"running Site exact focus lease");
    Check(HuntMapPolicy.Consumed(proof["consume"]!.ToJsonString()),"actual Site consume ack");
}
Console.WriteLine($"Hunt map focused fixtures passed: {count}; no live game proof.");

sealed class StalledStream : Stream {
    public override bool CanRead=>true;
    public override bool CanSeek=>false;
    public override bool CanWrite=>false;
    public override long Length=>throw new NotSupportedException();
    public override long Position { get=>0;set=>throw new NotSupportedException(); }
    public override int Read(byte[] b,int offset,int count)=>throw new NotSupportedException();
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default) { await Task.Delay(Timeout.InfiniteTimeSpan,token);return 0; }
    public override void Flush()=>throw new NotSupportedException();
    public override long Seek(long o,SeekOrigin origin)=>throw new NotSupportedException();
    public override void SetLength(long l)=>throw new NotSupportedException();
    public override void Write(byte[] b,int offset,int count)=>throw new NotSupportedException();
}
