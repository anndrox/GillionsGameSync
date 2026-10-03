using System.Diagnostics;
using System.Text.Json;
using GillionsGameSync;

int checks=0;
void Check(bool ok,string name) { checks++; if (!ok) throw new Exception(name); }
var now = new DateTime(2026,10,3,18,0,0,DateTimeKind.Utc);
TravelDestination Destination(uint id=2) => new(id,132,"OBSERVED_IN_PERSONAL_LIST","UNKNOWN",120,null,false,false,true);
TravelObservation Row() => new(132,2,10.1,20.2,now,PersonalObservationCompatibility.GameBuild,
    PersonalObservationCompatibility.NativeVersion,"0.0.77.0","OBSERVED_PARTIAL",[Destination()]);
var state = new TravelContextState();
Check(!state.Enabled && !state.Begin(now) && !state.Observe(1,Row(),now) && state.Current(1,now)==null,"default OFF");
Check(!TravelSyncPolicy.Admit(true,true,false,true,TravelSyncPolicy.Origin,true),"transport OFF without distinct consent");
state.SetEnabled(true);
Check(state.Begin(now) && !state.Begin(now.AddSeconds(14)) && state.Begin(now.AddSeconds(15)),"bounded fallback");
foreach(var lifecycle in new[] { "logout/login","OFF/ON","map/territory" }) {
    var gate=new TravelContextState(); gate.SetEnabled(true);
    Check(gate.Begin(now),"initial lifecycle read");
    for(int i=0;i<15;i++) {
        if(lifecycle=="OFF/ON") { gate.SetEnabled(false); gate.SetEnabled(true); }
        else if(lifecycle=="logout/login") { gate.Clear(); gate.Invalidate(); }
        else gate.Invalidate();
        Check(!gate.Begin(now.AddSeconds(i)),"lifecycle bypassed15s: "+lifecycle);
    }
    Check(gate.Begin(now.AddSeconds(15)),"lifecycle suppressed due successor");
}
Check(state.Observe(1,Row(),now) && state.Current(1,now)!=null,"own logged-in location");
Check(!state.Observe(0,Row(),now) && !state.Observe(1,Row(),now),"unowned/duplicate timestamp");
Check(!state.Observe(1,Row() with { ObservedAtUtc=now.AddSeconds(-1) },now),"older rejected");
Check(!state.Observe(1,Row() with { ObservedAtUtc=now.AddSeconds(1) },now),"future rejected");
Check(state.Current(2,now)==null,"other character cannot read");
Check(state.Observe(2,Row(),now) && state.Current(1,now)==null,"switch drops previous partition");
foreach (var invalid in new[] {
    Row() with { TerritoryId=0 },Row() with { TerritoryId=uint.MaxValue },Row() with { MapId=0 },Row() with { MapId=uint.MaxValue },
    Row() with { MapX=0,MapY=0 },Row() with { MapX=double.NaN },Row() with { MapY=double.PositiveInfinity },
    Row() with { MapX=-1 },Row() with { MapX=100.1 },Row() with { MapX=1.123 },
    Row() with { GameBuild="next-patch" },Row() with { NativeVersion="next-sdk" },Row() with { CollectorVersion="secret\r\n" },
    Row() with { ObservedAtUtc=default },Row() with { ObservedAtUtc=DateTime.SpecifyKind(now,DateTimeKind.Local) },
    Row() with { Destinations=null! },Row() with { Destinations=[null!] },Row() with { Destinations=[] },
    Row() with { Destinations=[Destination(),Destination()] },Row() with { TeleportSupport="AVAILABLE" },
    Row() with { TeleportSupport="UNAVAILABLE" },Row() with { Destinations=Enumerable.Range(1,257).Select(i=>Destination((uint)i)).ToArray() }
}) Check(!TravelPolicy.Valid(invalid) && !state.Observe(1,invalid,now),"malformed/source unsupported");
foreach(var invalid in new[] {
    Destination(0),Destination(uint.MaxValue),Destination() with { TerritoryId=0 },Destination() with { TerritoryId=uint.MaxValue },
    Destination() with { Attunement="static-existence" },Destination() with { Usability="AVAILABLE" },
    Destination() with { ActualCostGil=120 },Destination() with { ObservedListGil=null },Destination() with { ObservedListGil=100000 }
}) Check(!TravelPolicy.Valid(Row() with { Destinations=[invalid] }),"bad destination/fabricated usability/final cost");
Check(TravelPolicy.Valid(Row() with { TeleportSupport="UNAVAILABLE",Destinations=[] }),"unavailable teleports preserve location");
Check(TravelPolicy.Valid(Row() with { Destinations=[Destination() with { ObservedListGil=0,Free=true,Home=true,Favored=false }] }),"observed free/home/favored flags with zero quote, NOT final charge");
Check(TravelPolicy.Valid(Row() with { Destinations=[Destination() with { Home=null,Free=null,Favored=null }] }),"unknown relationships");
Check(TravelPolicy.Fresh(Row(),now.AddSeconds(44)) && !TravelPolicy.Fresh(Row(),now.AddSeconds(45)),"TTL exact");
for(int i=0;i<1000;i++) {
    var time=now.AddSeconds(i);
    Check(state.Observe(1,Row() with { ObservedAtUtc=time,MapX=TravelPolicy.Round(10+(i%10)*0.1) },time),"latest admitted");
    Check(state.Current(1,time)?.ObservedAtUtc==time,"latest only, no list/history");
}
state.SetEnabled(false);
Check(state.Current(1,now.AddSeconds(1000))==null && !state.Begin(now.AddSeconds(1000)),"disable clears/stops");
state.SetEnabled(true); state.Observe(1,Row(),now); state.Invalidate();
Check(state.Current(1,now)==null,"map/territory invalidation");
state.Observe(1,Row(),now); state.Clear();
Check(state.Current(1,now)==null,"logout/reload empty ephemeral context");
var json=JsonSerializer.Serialize(Row(),PersonalObservationCompatibility.Json);
using(var doc=JsonDocument.Parse(json,new JsonDocumentOptions { MaxDepth=TravelPolicy.MaximumDepth })) {
    Check(doc.RootElement.GetProperty("destinations")[0].GetProperty("actualCostGil").ValueKind == JsonValueKind.Null,"no fabricated charge");
    Check(doc.RootElement.GetProperty("uploadState").GetString()=="local-only-no-server-contract","export not activation");
}
foreach(var forbidden in new[] { "contentId","accountId","worldId","worldX","worldZ","workshopScope","recommendedTarget","bestAetheryte","deviceToken" })
    Check(!json.Contains(forbidden,StringComparison.OrdinalIgnoreCase),"private/minimal fact shape");
for(int count=1;count<=256;count++) {
    var row=Row() with { Destinations=Enumerable.Range(1,count).Select(i=>Destination((uint)i)).ToArray() };
    int bytes=JsonSerializer.SerializeToUtf8Bytes(row,PersonalObservationCompatibility.Json).Length;
    Check(TravelPolicy.Valid(row)==(bytes<=TravelPolicy.MaximumBytes),"size bound at count "+count);
}
// Measure the real managed validation/latest-only model, not a live framework read.
var benchmark=Row() with { Destinations=Enumerable.Range(1,128).Select(i=>Destination((uint)i)).ToArray() };
for(int i=0;i<20;i++) TravelPolicy.Valid(benchmark);
long allocated=GC.GetAllocatedBytesForCurrentThread();var start=Stopwatch.GetTimestamp();
for(int i=0;i<500;i++) if(!TravelPolicy.Valid(benchmark)) throw new Exception("benchmark admission");
Console.WriteLine($"Travel policy benchmark128: {Stopwatch.GetElapsedTime(start).TotalMilliseconds/500:F3} ms/check; {(GC.GetAllocatedBytesForCurrentThread()-allocated)/500} bytes/check. Fixture, not live native timing.");
if(args.Length==2 && args[0]=="--fixture") { var path=Path.GetFullPath(args[1]); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path,json); }
if(args.Length==2 && args[0]=="--site-protocol") {
    using var site=JsonDocument.Parse(File.ReadAllText(args[1]));
    Check(TravelSyncPolicy.Compatible(site.RootElement.GetProperty("ack").GetRawText()),"exact deployed Site ack incompatible");
    Check(TravelSyncPolicy.Receipt(201,site.RootElement.GetProperty("first").GetRawText()),"exact deployed Site new receipt incompatible");
    Check(TravelSyncPolicy.Receipt(200,site.RootElement.GetProperty("retry").GetRawText()),"exact deployed Site retry receipt incompatible");
}
checks += TravelTransportTests.Run();
Console.WriteLine($"Travel checks PASS: {checks}; bounded Testing transport fixtures; no real FFXIV validation claimed.");
