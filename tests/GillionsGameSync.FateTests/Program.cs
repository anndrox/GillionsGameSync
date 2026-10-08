using System.Text;
using System.Text.Json;
using GillionsGameSync;

int checks=0;
void Check(bool ok,string why) { checks++; if(!ok) throw new Exception(why); }
var now=new DateTime(2026,10,5,12,0,0,DateTimeKind.Utc);
var source=new FateSource("dalamud-fate-table","0.0.85.0","2026.09.15.0000.0000",15,"15.0.3.6",
    "b666d821a47306fb447c60155b5d99377f91a5ee","7.56.2.9136","313161e448e335adddd928f8a0212b2c328b5659",
    "7.7.0","7.5.1","2026.09.15.0000.0000");
var context=new FateContext(123,21,134,2); // Synthetic local identity NEVER serialized.
FateObservation Row(DateTime? time=null) => new(1000,21,134,new("public_instance",2),new("running",4),time??now,
    35,true,15,20,new(120,7,-250),75,new((int)new DateTimeOffset(now).ToUnixTimeSeconds()-180,900));
Check(FatePolicy.Compatible(source),"exact researched matrix");
foreach(var bad in new[] {
    source with {GameVersion="next"},source with {ReferenceGameVersion="next"},source with {DalamudApiLevel=16},
    source with {DalamudVersion="15.0.3.7"},source with {DalamudRevision="next"},source with {ClientStructsVersion="next"},
    source with {ClientStructsRevision="next"},source with {LuminaVersion="next"},source with {ExcelVersion="next"},
    source with {Family="untrusted"},source with {CollectorVersion="unavailable"}
}) Check(!FatePolicy.Compatible(bad),"patch/source fence");
for(int raw=0;raw<256;raw++) Check((FatePolicy.State((byte)raw)!=null)==new[]{3,4,5,7,8}.Contains(raw),"enum closed");
Check(FatePolicy.Valid(Row(),now),"bounded positive row");
foreach(var bad in new[] {
    Row() with {FateId=0},Row() with {WorldId=0},Row() with {TerritoryId=0},
    Row() with {Instance=new("unknown",0)},Row() with {Instance=new("noninstanced",2)},Row() with {Instance=new("public_instance",0)},
    Row() with {State=new("ended",4)},Row() with {ProgressPercent=101},Row() with {Level=1,MaxLevel=255},
    Row() with {Level=20,MaxLevel=15},Row() with {Level=null,MaxLevel=20},Row() with {Level=0},
    Row() with {PositionWorld=new(float.NaN,0,0)},Row() with {PositionWorld=new(0,float.PositiveInfinity,0)},
    Row() with {RadiusWorld=0},Row() with {RadiusWorld=float.NaN},Row() with {Timing=new(0,900)},
    Row() with {Timing=new(1,0)},Row() with {ObservedAt=now.AddSeconds(-31)},
    Row() with {ObservedAt=now.AddSeconds(6)},Row() with {ObservedAt=DateTime.SpecifyKind(now,DateTimeKind.Local)}
}) Check(!FatePolicy.Valid(bad,now),"invalid row fails closed");
Check(FatePolicy.Valid(Row() with {Instance=new("noninstanced",0)},now),"settled noninstance");
Check(FatePolicy.Valid(Row() with {ProgressPercent=null,Bonus=null,Level=null,MaxLevel=null,Timing=null,PositionWorld=null,RadiusWorld=null},now),"unknown is null, not fabricated");
Check(FatePolicy.Timing(0,900,now)==null && FatePolicy.Timing((int)new DateTimeOffset(now).ToUnixTimeSeconds()+6,900,now)==null,"unstarted/future timer");
Check(FatePolicy.Valid(Row() with {ProgressPercent=100},now) && Row().State.Kind=="running","100 never terminal");
Check(FatePolicy.Occurrence(Row() with {Timing=null},"epoch")==null,"provisional no durable identity");
var identity=FatePolicy.Occurrence(Row(),"epoch");
foreach(var changed in new[] {Row() with {WorldId=22},Row() with {Instance=new("public_instance",3)},
    Row() with {TerritoryId=135},Row() with {Timing=Row().Timing! with {StartTimeEpoch=Row().Timing!.StartTimeEpoch+1}}})
    Check(FatePolicy.Occurrence(changed,"epoch")!=identity,"distinct occurrence contexts");
Check(FatePolicy.Prepare(1,source,[],now)==null,"no empty replacement");
Check(FatePolicy.Prepare(1,source,Enumerable.Repeat(Row(),65).ToArray(),now)==null,"64 bound");
var state=new FateEpochState();
state.Invalidate(); var settledEpoch=state.Epoch;
Check(state.Observe(context,context,source,[Row()],now),"first positive");
Check(state.Epoch==settledEpoch,"first admitted context binds the already-settled epoch without invalidating it again");
var batch=state.Prepared!;
Check(batch is not null && batch.Body.Length<128*1024,"bounded exact batch");
using(var d=JsonDocument.Parse(batch!.Body)) {
    Check(d.RootElement.EnumerateObject().Select(p=>p.Name).Order().SequenceEqual(
        new[]{"schemaVersion","collectorSchema","coverage","source","batchId","observations"}.Order()),"exact envelope keys");
    Check(d.RootElement.GetProperty("observations")[0].GetProperty("observedAt").GetString()!.EndsWith('Z'),"ISO UTC");
}
string body=Encoding.UTF8.GetString(batch!.Body.Span);
var copy=batch.CopyBody(); copy[0]=0;
Check(batch.Body.Span[0]!=(byte)0,"request copies cannot mutate a prepared retry");
foreach(var excluded in new[]{"characterName","contentId","accountId","deviceId","homeWorld","playerPosition","inventory","quests","party","fc","token","receivedAt","timeRemaining"})
    Check(!body.Contains('"'+excluded+'"',StringComparison.OrdinalIgnoreCase),"privacy/static exclusion "+excluded);
Check(state.Observe(context,context,source,[Row(now.AddSeconds(5))],now.AddSeconds(5)) && !state.LastChanged
    && state.Prepared==batch && state.Prepared!.BatchId==batch.BatchId,"unchanged within renewal, exact retry ID/body");
Check(state.Observe(context,context,source,[Row(now.AddSeconds(10))],now.AddSeconds(10)) && state.Prepared!.BatchId!=batch.BatchId,"bounded renewal new ID");
foreach(var changed in new[] {Row(now.AddSeconds(15)) with {ProgressPercent=40},Row(now.AddSeconds(15)) with {Bonus=false},
    Row(now.AddSeconds(15)) with {State=new("ending",5)}}) {
    Check(state.Observe(context,context,source,[changed],now.AddSeconds(15)) && state.LastChanged,"semantic successor");
}
Check(state.Observe(context,context,source,[Row(now.AddSeconds(20)) with {State=new("ended",7)}],now.AddSeconds(20)) && state.Current.Length==1,"direct bound terminal");
Check(state.Observe(context,context,source,[],now.AddSeconds(25)) && state.Prepared==null && state.Current.Length==0,"disappearance cancels unsent, no negative");
state.Invalidate();
Check(state.Observe(context,context,source,[Row() with {State=new("failed",8)}],now) && state.Current.Length==0,"terminal cannot create occurrence after reload");
foreach(var other in new[] {context with {LocalCharacter=456},context with {World=22},context with {Territory=135},context with {Instance=3}}) {
    state.Observe(context,context,source,[Row()],now);
    Check(!state.Observe(context,other,source,[Row()],now) && state.Prepared==null && state.Current.Length==0,"during-read transition discard");
}
Check(!state.Observe(context with {LocalCharacter=0},context with {LocalCharacter=0},source,[Row()],now),"unbound local context");
state.Observe(context,context,source,[Row()],now); state.Expire(now.AddSeconds(31));
Check(state.Prepared==null,"RAM prepared expiry");
state.Invalidate(); state.Observe(context,context,source,[Row()],now);
var grant=new FateAdmission("session","https://test.gillions.app","GillionsGameSyncTest",FatePolicy.Capability,1,
    FatePolicy.Capability,source,FatePolicy.AccountPolicy,1,true,false,now.AddMinutes(1),true,true);
var prepared=state.Prepared!;
Check(FateTransportPolicy.CanSend(grant,source,"session",true,true,prepared,state.Epoch,now),"normalized independent grant");
Check(!FateTransportPolicy.CanSend(null,source,"session",true,true,prepared,state.Epoch,now),"no real Site discovery means no sends");
foreach(var bad in new[] {grant with {Origin="http://test.gillions.app"},grant with {Origin="https://gillions.app"},
    grant with {Origin="https://10.10.2.1"},grant with {SessionGeneration="other"},grant with {Product="GillionsGameSync"},
    grant with {Capability="ordinary-sync"},grant with {SchemaVersion=2},grant with {CollectorSchema="unknown"},
    grant with {PolicyKey="market"},grant with {PolicyRevision=2},grant with {Granted=false},grant with {Revoked=true},
    grant with {ExpiresAt=now},grant with {ActiveAccount=false},grant with {EnabledDevice=false},
    grant with {AdmittedSource=source with {CollectorVersion="0.0.84.0"}}
}) Check(!FateTransportPolicy.CanSend(bad,source,"session",true,true,prepared,state.Epoch,now),"independent policy/source/product/origin gate");
Check(!FateTransportPolicy.CanSend(grant,source,"session",false,true,prepared,state.Epoch,now),"pairing required");
Check(!FateTransportPolicy.CanSend(grant,source,"session",true,false,prepared,state.Epoch,now),"context required");
Check(!FateTransportPolicy.CanSend(grant,source,"session",true,true,prepared,state.Epoch+1,now),"old epoch forbidden");
using(var request=FateTransportPolicy.Request(grant,source,"session",true,true,prepared,state.Epoch,now,"synthetic-token")) {
    Check(request?.RequestUri?.AbsoluteUri==FatePolicy.Endpoint && request.Headers.Authorization?.Scheme=="Bearer","exact HTTPS paired request factory");
    Check(request!.Content!.ReadAsByteArrayAsync().Result.SequenceEqual(prepared.Body.ToArray()),"exact prepared bytes, no retry restamp");
}
state.CancelUnsent(); Check(state.Prepared==null,"policy OFF cancels RAM batch, no persistent config touched");
string Receipt(int accepted,int duplicate,string rejected="[]") => $"{{\"ok\":true,\"schemaVersion\":1,\"batchId\":\"{prepared.BatchId}\",\"receivedAt\":\"{now:O}\",\"acceptedCount\":{accepted},\"duplicateCount\":{duplicate},\"rejected\":{rejected},\"retryAfterSeconds\":5}}";
Check(FateTransportPolicy.Receipt(Receipt(1,0),prepared,1,now),"exact receipt");
Check(FateTransportPolicy.Receipt(Receipt(0,1),prepared,1,now),"duplicate receipt");
Check(FateTransportPolicy.Receipt(Receipt(0,0,"[{\"index\":0,\"code\":\"FATE_DEFINITION_UNSUPPORTED\"}]"),prepared,1,now),"explicit rejected row");
Check(!FateTransportPolicy.Receipt("{\"ok\":true}",prepared,1,now),"generic 200 is not receipt or authorization");
Check(!FateTransportPolicy.Receipt(Receipt(1,1),prepared,1,now),"count integrity");
Check(!FateTransportPolicy.Receipt(Receipt(0,0,"[{\"index\":1,\"code\":\"bad\"}]"),prepared,1,now),"reject index range");
Check(!FateTransportPolicy.Receipt(Receipt(1,0).Replace(prepared.BatchId.ToString(),Guid.NewGuid().ToString()),prepared,1,now),"receipt batch binding");
foreach(int status in new[]{401,403,404,422}) Check(FateTransportPolicy.Classify(status,false)==FateSendDisposition.Suspended,"suspend only FATE");
foreach(int status in new[]{429,503,502}) Check(FateTransportPolicy.Classify(status,false)==FateSendDisposition.Retry,"transient");
foreach(int status in new[]{400,409,413,415,200}) Check(FateTransportPolicy.Classify(status,false)==FateSendDisposition.Invalid,"no blind correction retry");
Check(FateTransportPolicy.RetrySeconds(0)==5 && FateTransportPolicy.RetrySeconds(1,90)==90 && FateTransportPolicy.RetrySeconds(100)==640,"bounded backoff");
Check(FateTransportPolicy.RetrySeconds(0,1800)==1800 && FateTransportPolicy.RetrySeconds(0,5.1)==6,"honor long/fractional server delay without early retry");
Check(FateTransportPolicy.RetrySeconds(0,double.NaN)==5 && FateTransportPolicy.RetrySeconds(0,double.PositiveInfinity)==5,"malformed retry header cannot create spin");
foreach(uint instance in new uint[]{0,1,2,3}) {
    state.Invalidate();
    var c=context with {Instance=instance};
    var r=Row() with {Instance=new(instance==0 ? "noninstanced":"public_instance",instance),State=new("preparing",3),Timing=null};
    Check(state.Observe(c,c,source,[r],now) && state.Current.Length==1 && FatePolicy.Occurrence(r,"epoch")==null,"preparing without timing is provisional");
    var running=r with {State=new("running",4),Timing=Row().Timing,ObservedAt=now.AddSeconds(5)};
    Check(state.Observe(c,c,source,[running],now.AddSeconds(5)) && state.LastChanged,"preparing to timed running");
    var failed=running with {State=new("failed",8),ObservedAt=now.AddSeconds(10)};
    Check(state.Observe(c,c,source,[failed],now.AddSeconds(10)) && state.Current.Single().State.Raw==8,"positive failed state only for established timed occurrence");
    state.Invalidate();
    Check(state.Observe(c,c,source,[failed],now.AddSeconds(10)) && state.Current.Length==0,"logout/instance invalidation prevents terminal carryover");
}
state.Invalidate();
state.Observe(context,context,source,[Row()],now);
var newStart=Row(now.AddSeconds(5)) with {Timing=Row().Timing! with {StartTimeEpoch=Row().Timing!.StartTimeEpoch+1},ProgressPercent=0};
Check(state.Observe(context,context,source,[newStart],now.AddSeconds(5)) && state.Current.Single().ProgressPercent==0,"new occurrence cannot inherit progress");
var costs=new FateMeasurements(); for(int i=0;i<300;i++) costs.Add(new(i,100,i/2.0,20,i%10,1,i%2==0,"fixture"));
Check(costs.Count==240 && costs.Snapshot()[0].ReadMilliseconds==60,"bounded diagnostic ring");
Check(FateMeasurements.Summary(costs.Snapshot()).Contains("p95"),"median/p95/max diagnostics");
// Reproduce the native reader's separately scheduled settlement check. No game
// getters are called; the source contract separately checks this caller pattern.
var lifecycle=new FateEpochState(); FateContext? settled=null; long admittedEpoch=0;
int settlePasses=0, readPasses=0;
for(int tick=0;tick<8;tick++) {
    var time=now.AddSeconds(tick*5);
    if(settled!=context || admittedEpoch!=lifecycle.Epoch) {
        lifecycle.Invalidate(); settled=context; admittedEpoch=lifecycle.Epoch; settlePasses++; continue;
    }
    var positive=Row(time) with {State= tick==7 ? new("ended",7) : new("running",4)};
    Check(lifecycle.Observe(context,context,source,[positive],time),"consecutive settled scheduled read");
    Check(lifecycle.Epoch==admittedEpoch && lifecycle.Current.Length==1,"ordinary reads preserve settled epoch and established terminal support");
    readPasses++;
}
Check(settlePasses==1 && readPasses==7,"one settlement pass, then every five-second read; no alternating re-settlement");
var previousEpoch=lifecycle.Epoch;
lifecycle.Observe(context with {World=22},context with {World=22},source,[Row(now.AddSeconds(40)) with {WorldId=22}],now.AddSeconds(40));
Check(lifecycle.Epoch>previousEpoch && lifecycle.Current.Single().WorldId==22,"real context changes still invalidate the prior epoch");
SenderTests.Run(Check,source,now);
if(args.Length==2 && args[0]=="--fixture") {
    var path=Path.GetFullPath(args[1]); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllBytes(path,prepared.CopyBody());
}
Console.WriteLine($"FATE fixtures passed: {checks}. Synthetic, not live collection/performance evidence.");
