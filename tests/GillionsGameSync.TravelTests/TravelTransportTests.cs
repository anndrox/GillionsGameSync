using System.Text.Json;
using GillionsGameSync;

internal static class TravelTransportTests {
    internal static int Run() {
        int checks=0;
        void Check(bool ok,string reason) { checks++; if(!ok) throw new Exception(reason); }
        var now=DateTime.UtcNow;
        TravelObservation Row(DateTime? at=null) => new(100,200,10.1,20.2,at??now,
            PersonalObservationCompatibility.GameBuild,PersonalObservationCompatibility.NativeVersion,"0.0.77.0","OBSERVED_PARTIAL",
            [new(1,100,"OBSERVED_IN_PERSONAL_LIST","UNKNOWN",100,null,null,null,null)]);
        var descriptor=new Dictionary<string,object> { ["resourceType"]="travel_context",["schemaVersion"]=1,
            ["collectorSchema"]="travel-context-v1",["transportContract"]="travel-context-v1",["capability"]="travel_context_v1",
            ["maxPayloadBytes"]=65536,["maxEnvelopeBytes"]=69632,["maxDepth"]=8,["maxDestinations"]=256,["ttlSeconds"]=45,
            ["resourceHeader"]="x-gillions-personal-resource",["capabilityHeader"]="x-gillions-personal-capability" };
        string Ack(object[]? resources=null,string endpoint=TravelSyncPolicy.Endpoint,string product="GillionsGameSyncTest") =>
            JsonSerializer.Serialize(new { ok=true,acceptedClientProduct=product,personalObservations=new { contractVersion=1,endpoint,resources=resources??[descriptor] } });
        Check(TravelSyncPolicy.Compatible(Ack()),"exact Site grant-gated ack rejected");
        Check(!TravelSyncPolicy.Compatible(Ack([])),"general permission cannot grant travel");
        Check(!TravelSyncPolicy.Compatible(Ack([descriptor,descriptor])),"duplicate resource accepted");
        Check(!TravelSyncPolicy.Compatible(Ack(product:"GillionsGameSync")),"Stable ack admitted");
        foreach(var origin in new[]{"http://test.gillions.app","https://gillions.app","https://10.10.2.13","https://test.gillions.app/","https://test.gillions.app.evil"}) {
            Check(!TravelSyncPolicy.Compatible(Ack(endpoint:origin+"/api/game-sync/sync")),"wrong ack endpoint");
            Check(!TravelSyncPolicy.Admit(true,true,true,true,origin,true),"wrong origin admitted");
        }
        foreach(var key in descriptor.Keys.ToArray()) {
            var changed=new Dictionary<string,object>(descriptor); changed.Remove(key);
            Check(!TravelSyncPolicy.Compatible(Ack([changed])),"missing ack field: "+key);
            changed[key]="wrong";
            Check(!TravelSyncPolicy.Compatible(Ack([changed])),"wrong ack field: "+key);
        }
        foreach(var json in new[]{"{}","null","[]","not JSON","{\"ok\":true,\"personalObservations\":null}"})
            Check(!TravelSyncPolicy.Compatible(json),"malformed ack accepted");
        for(int mask=0;mask<32;mask++) Check(TravelSyncPolicy.Admit((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0,TravelSyncPolicy.Origin,(mask&16)!=0)==(mask==31),"admission conjunction");
        string Receipt(bool unchanged=false) => JsonSerializer.Serialize(new { ok=true,snapshotId=Guid.NewGuid().ToString("D"),receivedAt=now.ToString("O"),unchanged });
        Check(TravelSyncPolicy.Receipt(201,Receipt()),"new receipt rejected");
        Check(TravelSyncPolicy.Receipt(200,Receipt(true)),"same logical receipt rejected");
        Check(!TravelSyncPolicy.Receipt(200,Receipt())&&!TravelSyncPolicy.Receipt(201,Receipt(true)),"status/receipt mismatch");
        foreach(int status in new[]{0,202,204,301,307,400,401,403,409,429,503}) Check(!TravelSyncPolicy.Receipt(status,Receipt()),"non-contract success");
        foreach(var malformed in new[]{"null","[]","{}","broken",Receipt().Replace("true","false"),Receipt().Replace("snapshotId","wrong"),Receipt().Replace("Z","+00:00"),Receipt()[..^1]+",\"coords\":[1,2]}",new string('x',4097)})
            Check(!TravelSyncPolicy.Receipt(201,malformed),"malformed receipt accepted");
        var sync=new TravelSyncState();
        Check(sync.Prepare("session1",Row(now.AddSeconds(-45)),now)==null,"expired prepare");
        var first=sync.Prepare("session1",Row(),now)!;
        Check(first is not null&&Guid.TryParseExact(first.Nonce,"N",out _),"bounded nonce preparation");
        Check(ReferenceEquals(sync.Prepare("session1",Row() with { MapX=30.1 },now),first),"changed body under same observation regenerates nonce");
        sync.Dispatched(now);
        Check(!sync.Maintain("session1",true,now.AddSeconds(14)),"send faster than15s");
        sync.Complete(first!,false,0,null,now);
        var retry=sync.Prepare("session1",Row(),now.AddSeconds(15));
        Check(ReferenceEquals(first,retry)&&retry!.Nonce==first!.Nonce&&retry.Payload==first.Payload,"retry body/nonce drift");
        sync.Dispatched(now.AddSeconds(15)); sync.Complete(first!,true,201,null,now.AddSeconds(15));
        Check(sync.Prepare("session1",Row(),now.AddSeconds(30))==null,"receipt re-send");
        var next=sync.Prepare("session1",Row(now.AddSeconds(30)),now.AddSeconds(30))!;
        Check(next.Nonce!=first!.Nonce&&next.ObservedAtUtc==now.AddSeconds(30),"new observation reused nonce");
        Check(sync.Prepare("session1",Row(now.AddSeconds(29)),now.AddSeconds(30))==null&&ReferenceEquals(sync.Pending,next),"older observation replaced newer pending context");
        var token=next.Lifetime.Token;
        sync.Maintain("session1",true,now.AddSeconds(75));
        Check(sync.Pending==null&&token.IsCancellationRequested,"expiry extends old location or does not cancel");
        foreach(var transition in new[]{"OFF","logout","switch","re-pair","revoked","unload","build","ack","origin","map"}) {
            var s=new TravelSyncState(); var p=s.Prepare("old",Row(),now)!; var cancellation=p.Lifetime.Token;
            if(transition is "switch" or "re-pair") s.Maintain("new",true,now);
            else if(transition is "map" or "unload") s.Clear();
            else s.Maintain("old",false,now);
            Check(s.Pending==null&&cancellation.IsCancellationRequested,"lifecycle cancellation: "+transition);
            s.Complete(p,true,201,null,now);
            Check(s.Pending==null,"late response resurrected state: "+transition);
        }
        foreach(int status in new[]{0,429,503}) {
            var s=new TravelSyncState();var p=s.Prepare("old",Row(),now)!;s.Dispatched(now);
            s.Complete(p,false,status,status==0?null:60,now);
            Check(!s.Maintain("old",true,now.AddSeconds(14)),"retry too fast");
            if(status!=0) { Check(s.NextAttemptUtc==now.AddSeconds(60),"Retry-After60 not honored");s.Maintain("old",true,now.AddSeconds(45));Check(s.Pending==null,"stale retry survived backoff"); }
            s.Clear(); Check(!s.Maintain("new",true,now.AddSeconds(14)),"lifecycle bypassed send budget");
        }
        foreach(var seconds in new double?[]{null,-1,0,10,60,120,900,86400,double.NaN,double.PositiveInfinity}) {
            Check(TravelSyncPolicy.RetrySeconds(429,1,seconds)>=60,"429floor");
            Check(TravelSyncPolicy.RetrySeconds(503,1,seconds)>=60,"503floor");
            Check(TravelSyncPolicy.RetrySeconds(0,1,seconds)>=15,"networkfloor");
        }
        foreach(int status in new[]{301,302,307,308,400,401,403,404,409,413,415}) Check(TravelSyncPolicy.Terminal(status),"redirect/rejection not terminal");
        Check(!TravelSyncPolicy.Terminal(429)&&!TravelSyncPolicy.Terminal(503),"rate/unavailable not retryable");
        using(var doc=JsonDocument.Parse(next.Payload)) {
            var r=doc.RootElement;
            Check(r.EnumerateObject().Count()==16,"extra payload field");
            var d=r.GetProperty("destinations")[0];
            Check(d.EnumerateObject().Count()==9&&d.GetProperty("actualCostGil").ValueKind==JsonValueKind.Null,"extra destination/final charge");
            foreach(var key in new[]{"home","free","favored"}) Check(d.GetProperty(key).ValueKind==JsonValueKind.Null,"null flag lost");
            Check(r.GetProperty("collectorVersion").GetString()=="0.0.77.0"&&r.GetProperty("mapX").GetDouble()==10.1,"metadata/precision drift");
        }
        Check(typeof(TravelPrepared).GetMethod("ToString")!.DeclaringType==typeof(object),"payload-bearing generated ToString");
        return checks;
    }
}
