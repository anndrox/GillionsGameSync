using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class FatePackagedTests {
    internal static void Run(Assembly assembly, bool testing, string fixtureDirectory) {
        int checks=0;
        void Check(bool ok,string message) { checks++; if (!ok) throw new Exception(message); }
        string[] names=["FateLocalView","FatePolicy","FatePrepared","FateEpochState","FateTransportPolicy","FateAdmission","FateMeasurements","FateDiscovery","FateGrant","FateSenderState"];
        foreach(var name in names) Check((assembly.GetType("GillionsGameSync."+name)!=null)==testing,"FATE Testing/Stable type boundary: "+name);
        var plugin=assembly.GetType("GillionsGameSync.Plugin",true)!;
        Check(plugin.GetConstructors().Single().GetParameters().Any(p=>p.ParameterType==typeof(Dalamud.Plugin.Services.IFateTable))==testing,"FATE service injection must be Testing only.");
        var config=assembly.GetType("GillionsGameSync.PluginConfiguration",true)!;
        Check(!config.GetProperties().Any(p=>p.Name.Contains("Fate",StringComparison.OrdinalIgnoreCase)),"FATE session must not add a permanent configuration permission/history.");
        if (!testing) { Console.WriteLine($"Exact packaged FATE Stable exclusion PASS: {checks} checks."); return; }
        Type T(string name) => assembly.GetType("GillionsGameSync."+name,true)!;
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var policy=T("FatePolicy");
        object Invoke(string name,params object?[] args) => policy.GetMethod(name,flags)!.Invoke(null,args)!;
        var now=new DateTime(2026,10,5,12,0,0,DateTimeKind.Utc);
        var source=Activator.CreateInstance(T("FateSource"),["dalamud-fate-table",assembly.GetName().Version!.ToString(4),"2026.09.15.0000.0000",15,"15.0.3.6",
            "b666d821a47306fb447c60155b5d99377f91a5ee","7.56.2.9136","313161e448e335adddd928f8a0212b2c328b5659","7.7.0","7.5.1","2026.09.15.0000.0000"])!;
        Check((bool)Invoke("Compatible",source),"Exact packaged supported source matrix.");
        Check(!(bool)Invoke("Compatible",(object?)null),"Missing source must fail closed.");
        for(int raw=0;raw<256;raw++) Check((Invoke("State",(byte)raw)!=null)==new[]{3,4,5,7,8}.Contains(raw),"Packaged enum fence.");
        var instance=Activator.CreateInstance(T("FateInstance"),["public_instance",(uint)2])!;
        var row=Activator.CreateInstance(T("FateObservation"),[(ushort)1000,(uint)21,(uint)134,instance,Invoke("State",(byte)4),now,
            (byte)35,true,(byte)15,(byte)20,null,null,null])!;
        var rows=Array.CreateInstance(T("FateObservation"),1); rows.SetValue(row,0);
        var prepared=Invoke("Prepare",(long)1,source,rows,now);
        var epochType=T("FateEpochState"); const BindingFlags instanceFlags=BindingFlags.Instance|BindingFlags.NonPublic;
        var lifecycle=Activator.CreateInstance(epochType,true)!;
        epochType.GetMethod("Invalidate",instanceFlags)!.Invoke(lifecycle,[]);
        var epoch=epochType.GetProperty("Epoch",instanceFlags)!;
        long settledEpoch=(long)epoch.GetValue(lifecycle)!;
        var context=Activator.CreateInstance(T("FateContext"),[(ulong)123,(uint)21,(uint)134,(uint)2])!;
        for(int tick=0;tick<4;tick++) {
            Check((bool)epochType.GetMethod("Observe",instanceFlags)!.Invoke(lifecycle,[context,context,source,rows,now.AddSeconds(tick*5)])!,"Packaged consecutive settled reads.");
            Check((long)epoch.GetValue(lifecycle)! == settledEpoch,"Packaged first-context binding must not force recurring re-settlement.");
        }
        var localType=T("FateLocalView");
        var local=System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(localType);
        localType.GetField("state",instanceFlags)!.SetValue(local,lifecycle);
        localType.GetField("measurements",instanceFlags)!.SetValue(local,Activator.CreateInstance(T("FateMeasurements"),true));
        var automatic=localType.GetMethod("SetAuthorized",instanceFlags)!;
        automatic.Invoke(local,[true]);
        Check((bool)localType.GetProperty("Measuring",instanceFlags)!.GetValue(local)!,"Automatic Site grant starts collection without diagnostic command.");
        Check(!(bool)localType.GetProperty("ContextReady",instanceFlags)!.GetValue(local)!,"Authorization cannot invent supported settled context.");
        automatic.Invoke(local,[false]);
        Check(!(bool)localType.GetProperty("Measuring",instanceFlags)!.GetValue(local)!,"Policy loss stops collection.");
        Check(epochType.GetProperty("Prepared",instanceFlags)!.GetValue(lifecycle) is null,"Policy loss discards unsent observations.");
        Check(((Array)epochType.GetProperty("Current",instanceFlags)!.GetValue(lifecycle)!).Length==0,"Policy loss clears volatile rows.");
        Check((long)epoch.GetValue(lifecycle)! == settledEpoch,"Policy stop must not create discovery/re-settlement feedback loop.");
        automatic.Invoke(local,[true]);
        Check(epochType.GetProperty("Prepared",instanceFlags)!.GetValue(lifecycle) is null,"OFF to ON requires fresh observations, not pre-consent replay.");
        var body=(ReadOnlyMemory<byte>)T("FatePrepared").GetProperty("Body",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(prepared)!;
        using(var json=JsonDocument.Parse(body)) {
            Check(json.RootElement.EnumerateObject().Select(p=>p.Name).Order().SequenceEqual(new[]{"schemaVersion","collectorSchema","coverage","source","batchId","observations"}.Order()),"Packaged exact envelope keys.");
            Check(json.RootElement.GetProperty("source").GetProperty("collectorVersion").GetString()==assembly.GetName().Version!.ToString(4),"Fixture source must identify actual packaged version.");
            Check(json.RootElement.GetProperty("observations")[0].GetProperty("observedAt").GetString()!.EndsWith('Z'),"Packaged UTC ISO-Z.");
            foreach(var excluded in new[]{"contentId","characterName","accountId","deviceId","homeWorld","playerPosition","inventory","quests","party","fc","token","receivedAt","timeRemaining"}) {
                Check(!System.Text.Encoding.UTF8.GetString(body.Span).Contains('"'+excluded+'"',StringComparison.OrdinalIgnoreCase),"Packaged private field exclusion: "+excluded);
            }
        }
        var admission=T("FateTransportPolicy").GetMethod("CanSend",flags)!;
        Check(!(bool)admission.Invoke(null,[null,source,"synthetic",true,true,prepared,(long)1,now])!,"Exact packaged no-admission gate must deny sends.");
        var request=T("FateTransportPolicy").GetMethod("Request",flags)!;
        Check(request.Invoke(null,[null,source,"synthetic",true,true,prepared,(long)1,now,"synthetic-token"]) is null,"No HTTP request may be created without exact Site policy.");
        Check((string)policy.GetField("Endpoint",flags)!.GetRawConstantValue()! == "https://test.gillions.app/api/game-sync/fates/contribute","Exact TEST endpoint only.");
        var copy=(byte[])T("FatePrepared").GetMethod("CopyBody",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(prepared,[])!;
        copy[0]=0; Check(body.Span[0]!=(byte)0,"Packaged immutable retry body.");
        var sourceJson=JsonSerializer.SerializeToElement(source,source.GetType(),new JsonSerializerOptions {PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
        var wire=JsonSerializer.Serialize(new {ok=true,fateContribution=new {
            capability="fate-live-observations-v1",schemaVersion=1,collectorSchema="fate-live-observations-v1",coverage="positive_only",
            endpoint="/api/game-sync/fates/contribute",maxPayloadBytes=131072,maxObservations=64,acceptedClientProduct="GillionsGameSyncTest",
            acceptedSource=sourceJson,referenceCompatibilityEpoch="fate-reference:2026.09.15.0000.0000",authorized=true,reason=(string?)null,
            deviceBinding=new {deviceId="aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",pairedAt=now.AddDays(-1)},
            policy=new {name="fate_public_observations",revision=1,enabled=true,generation="bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"},
            issuedAt=now,expiresAt=now.AddSeconds(30),retryAfterSeconds=5}});
        var parse=T("FateDiscovery").GetMethod("Parse",flags)!;
        var grant=parse.Invoke(null,[wire,"synthetic","aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",source,now]);
        Check(grant is not null,"Packaged exact discovery parser admits supported grant.");
        var offWire=JsonNode.Parse(wire)!;
        offWire["fateContribution"]!["authorized"]=false;
        offWire["fateContribution"]!["reason"]="FATE_POLICY_REQUIRED";
        offWire["fateContribution"]!["policy"]!["enabled"]=false;
        var offGrant=parse.Invoke(null,[offWire.ToJsonString(),"synthetic","aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",source,now]);
        Check(offGrant is not null,"Packaged real OFF fixture accepted");
        var offType=offGrant!.GetType();
        var fateStatus=assembly.GetType("GillionsGameSync.PublicHealth",true)!.GetMethod("FateState",flags)!;
        Check((string)fateStatus.Invoke(null,[true,offType.GetProperty("Reason")!.GetValue(offGrant),false,
            offType.GetProperty("PolicyEnabled")!.GetValue(offGrant)])! == "Off on Gillions","parsed real OFF fixture renders honest Site status");
        Check(parse.Invoke(null,[wire.Replace("\"ok\":true","\"ok\":true,\"ok\":true"),"synthetic","aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",source,now]) is null,"Packaged duplicate key denies admission.");
        Check(parse.Invoke(null,[wire,"synthetic","aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",source,now.AddSeconds(30)]) is null,"Packaged expired grant denies admission.");
        var senderType=T("FateSenderState");
        using var sender=(IDisposable)Activator.CreateInstance(senderType,true)!;
        object? Call(string method,params object?[] arguments)=>senderType.GetMethod(method,instanceFlags)!.Invoke(sender,arguments);
        Call("Bind","binding",(long)1); Call("Install",grant,now,(long)0);
        Check((bool)Call("Authorized",now,(long)0)!,"Packaged policyON authorizes only sender.");
        Check(Call("Take",prepared,source,"synthetic",now,(long)0) is null,"Packaged pre-consent observation never sent.");
        Call("Install",null,now,(long)1);
        Check(!(bool)Call("Authorized",now,(long)1)!,"Packaged unavailable discovery closes sender.");
        Check(Call("Discover",(long)0) is true && Call("Discover",(long)10000) is false,"Packaged discovery single-flight.");
        Call("DiscoveryFinished"); Check(Call("Discover",(long)9999) is false,"Packaged discovery10s admission.");
        Call("Install",grant,now,(long)0); Call("Maintain",now.AddSeconds(-1),(long)30000);
        Check(!(bool)Call("Authorized",now.AddSeconds(-1),(long)30000)!,"Packaged monotonic grant expiry.");
        var responsePolicy=T("FateTransportPolicy").GetMethod("Response",flags)!;
        foreach(int status in new[]{401,403,404,422,429,503,502,400,409,413,415,200,201})
            foreach(bool failed in new[]{false,true}) {
                var result=responsePolicy.Invoke(null,[status,false,failed])!.ToString();
                var expected=status is 401 or 403 or 404 or 422 ? "Suspended"
                    : status is 429 or >=500 || status is 200 or 201 && failed ? "Retry" : "Invalid";
                Check(result==expected,"Packaged HTTP disposition survives empty/malformed/stalled body: "+status);
            }
        var transportFailure=T("FateTransportPolicy").GetMethod("TransportFailure",flags)!;
        foreach(var error in new Exception[]{new HttpIOException(HttpRequestError.ResponseEnded),new IOException("stream ended"),
            new HttpRequestException(),new OperationCanceledException(),new JsonException(),new InvalidOperationException()}) {
            var failed=(bool)transportFailure.Invoke(null,[error])!;
            Check(failed==(error is IOException or HttpRequestException or OperationCanceledException),"Packaged stream failure classification "+error.GetType().Name);
            foreach(int status in new[]{200,201,401,403,422,429,503}) {
                var expected=status is 401 or 403 or 422 ? "Suspended"
                    : status is 429 or 503 || failed ? "Retry" : "Invalid";
                Check(responsePolicy.Invoke(null,[status,false,failed])!.ToString()==expected,"Packaged truncated versus malformed complete response "+status);
            }
        }
        var read=T("SyncResponsePolicy").GetMethod("ReadAsync",BindingFlags.Public|BindingFlags.Static)!;
        using(var content=new StreamContent(new TruncatedResponseStream())) {
            Exception? observed=null;
            try { ((Task<string>)read.Invoke(null,[content,CancellationToken.None])!).GetAwaiter().GetResult(); }
            catch(Exception error) { observed=error; }
            Check(observed is HttpIOException,"Packaged bounded reader exposes premature response termination.");
            foreach(int status in new[]{200,201})
                Check(responsePolicy.Invoke(null,[status,false,(bool)transportFailure.Invoke(null,[observed])!])!.ToString()=="Retry","Packaged actual truncated success retries immutable batch.");
        }
        var canCommit=T("FateTransportPolicy").GetMethod("CanCommit",flags)!;
        using var transportDeadline=new CancellationTokenSource(); using var feature=new CancellationTokenSource(); transportDeadline.Cancel();
        Check((bool)canCommit.Invoke(null,[true,true,feature.Token])!,"Packaged delayed disposition survives transport deadline.");
        feature.Cancel(); Check(!(bool)canCommit.Invoke(null,[true,true,feature.Token])!,"Packaged actual cancellation suppresses old disposition.");
        var renewed=JsonNode.Parse(wire)!;
        renewed["fateContribution"]!["issuedAt"]=JsonSerializer.SerializeToNode(now.AddSeconds(20));
        renewed["fateContribution"]!["expiresAt"]=JsonSerializer.SerializeToNode(now.AddSeconds(50));
        var renewedGrant=parse.Invoke(null,[renewed.ToJsonString(),"synthetic","aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",source,now.AddSeconds(20)]);
        Check(renewedGrant is not null,"Packaged renewed exact30s grant.");
        var freshRow=Activator.CreateInstance(T("FateObservation"),[(ushort)1000,(uint)21,(uint)134,instance,Invoke("State",(byte)4),now.AddSeconds(5),
            (byte)35,true,(byte)15,(byte)20,null,null,null])!;
        var freshRows=Array.CreateInstance(T("FateObservation"),1); freshRows.SetValue(freshRow,0);
        var agedBatch=Invoke("Prepare",(long)1,source,freshRows,now.AddSeconds(5));
        var newerRow=Activator.CreateInstance(T("FateObservation"),[(ushort)1000,(uint)21,(uint)134,instance,Invoke("State",(byte)4),now.AddSeconds(37),
            (byte)40,true,(byte)15,(byte)20,null,null,null])!;
        var newerRows=Array.CreateInstance(T("FateObservation"),1); newerRows.SetValue(newerRow,0);
        var newerBatch=Invoke("Prepare",(long)1,source,newerRows,now.AddSeconds(37));
        foreach(var outcome in new[]{"Retry","Suspended","Invalid","Acknowledged"}) {
            using var late=(IDisposable)Activator.CreateInstance(senderType,true)!;
            object? Late(string method,params object?[] arguments)=>senderType.GetMethod(method,instanceFlags)!.Invoke(late,arguments);
            Late("Bind","late",(long)1); Late("Install",grant,now,(long)0); Late("Install",renewedGrant,now.AddSeconds(20),(long)20000);
            Check(Late("Take",agedBatch,source,"synthetic",now.AddSeconds(28),(long)28000) is not null,"Packaged age23 dispatch.");
            Late("Maintain",now.AddSeconds(36),(long)36000);
            Late("Complete",agedBatch,Enum.Parse(T("FateSendDisposition"),outcome),now.AddSeconds(36),(long)36000,1,(double?)30);
            Late("UploadFinished");
            if(outcome=="Retry") Check(Late("Take",newerBatch,source,"synthetic",now.AddSeconds(37),(long)37000) is null
                && senderType.GetProperty("Status",instanceFlags)!.GetValue(late)!.ToString()!.Contains("bounded retry"),"Packaged expired in-flight response preserves rate backoff.");
            else if(outcome=="Acknowledged") Check(senderType.GetProperty("LastAcknowledged",instanceFlags)!.GetValue(late) is not null,"Packaged expired in-flight logical receipt commits.");
            else Check(!(bool)Late("Authorized",now.AddSeconds(37),(long)37000)!,"Packaged expired in-flight suspension/correction commits.");
        }
        using var backoff=(IDisposable)Activator.CreateInstance(senderType,true)!;
        object? Backoff(string method,params object?[] arguments)=>senderType.GetMethod(method,instanceFlags)!.Invoke(backoff,arguments);
        Backoff("Bind","backoff",(long)1);
        long attempt=0;
        foreach(long next in new long[]{10000,20000,40000,80000}) {
            Check((bool)Backoff("Discover",attempt)!,"Packaged discovery at exponential deadline."); Backoff("DiscoveryFinished"); Backoff("DiscoveryFailed",attempt,null);
            Check(!(bool)Backoff("Discover",next-1)!,"Packaged failed discovery retains exponential count."); attempt=next;
        }
        Directory.CreateDirectory(fixtureDirectory);
        File.WriteAllBytes(Path.Combine(fixtureDirectory,"packaged-fate-live-observations-v1.json"),body.ToArray());
        Console.WriteLine($"Exact packaged FATE schema/privacy/no-admission/config boundaries PASS: {checks} checks. Synthetic; no native getter or live HTTP call.");
    }
    private sealed class TruncatedResponseStream : Stream {
        public override bool CanRead=>true;
        public override bool CanSeek=>false;
        public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();
        public override long Position { get=>throw new NotSupportedException(); set=>throw new NotSupportedException(); }
        public override int Read(byte[] buffer,int offset,int count)=>throw new HttpIOException(HttpRequestError.ResponseEnded);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)=>
            ValueTask.FromException<int>(new HttpIOException(HttpRequestError.ResponseEnded));
        public override void Flush()=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
        public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }
}
