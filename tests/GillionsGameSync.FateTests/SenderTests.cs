using System.Text.Json;
using System.Text.Json.Nodes;
using GillionsGameSync;

internal static class SenderTests {
    internal static void Run(Action<bool,string> check,FateSource source,DateTime now) {
        const string device="aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", policy="bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
        var root=JsonNode.Parse(JsonSerializer.Serialize(new {ok=true,fateContribution=new {
            capability=FatePolicy.Capability,schemaVersion=1,collectorSchema=FatePolicy.Capability,coverage="positive_only",
            endpoint="/api/game-sync/fates/contribute",maxPayloadBytes=131072,maxObservations=64,
            acceptedClientProduct="GillionsGameSyncTest",acceptedSource=source,referenceCompatibilityEpoch="fate-reference:"+source.ReferenceGameVersion,
            authorized=true,reason=(string?)null,deviceBinding=new {deviceId=device,pairedAt=now.AddDays(-1)},
            policy=new {name=FatePolicy.AccountPolicy,revision=1,enabled=true,generation=policy},issuedAt=now,expiresAt=now.AddSeconds(30),retryAfterSeconds=5
        }},FatePolicy.Json))!;
        string Json()=>root.ToJsonString();
        FateGrant? Parse(JsonNode node)=>FateDiscovery.Parse(node.ToJsonString(),"session",device,source,now);
        var grant=Parse(root)!;
        check(grant is {Admission.Granted:true},"exact revision3 grant");
        foreach(var field in root["fateContribution"]!.AsObject().ToArray()) {
            var missing=root.DeepClone(); missing["fateContribution"]!.AsObject().Remove(field.Key);
            check(Parse(missing)==null,"grant mandatory field "+field.Key);
            var bad=root.DeepClone(); bad["fateContribution"]![field.Key]=JsonValue.Create("wrong");
            check(Parse(bad)==null,"grant malformed field "+field.Key);
        }
        foreach(var path in new[]{"acceptedSource","deviceBinding","policy"}) {
            var value=root["fateContribution"]![path]!.AsObject();
            foreach(var field in value.ToArray()) {
                var missing=root.DeepClone(); missing["fateContribution"]![path]!.AsObject().Remove(field.Key);
                check(Parse(missing)==null,"nested mandatory "+path+"."+field.Key);
            }
            var extra=root.DeepClone(); extra["fateContribution"]![path]!["private"]=1;
            check(Parse(extra)==null,"unknown nested property "+path);
        }
        check(FateDiscovery.Parse(Json().Replace("\"ok\":true","\"ok\":true,\"ok\":true"),"session",device,source,now)==null,"duplicate discovery root key");
        check(FateDiscovery.Parse(Json().Replace("\"revision\":1","\"revision\":1,\"revision\":1"),"session",device,source,now)==null,"duplicate policy key");
        check(FateDiscovery.Parse(Json(),"session",Guid.NewGuid().ToString(),source,now)==null,"other device binding");
        check(FateDiscovery.Parse(Json(),"session",device,source with {CollectorVersion="0.0.86.0"},now)==null,"new actual source cannot impersonate85");
        check(FateDiscovery.Parse(Json(),"session",device,source,now.AddSeconds(30))==null,"expired grant");
        check(FateDiscovery.Parse(Json(),"session",device,source,now.AddSeconds(-6))==null,"future issuance beyond skew");
        var off=root.DeepClone(); off["fateContribution"]!["authorized"]=false; off["fateContribution"]!["reason"]="FATE_POLICY_REQUIRED";
        off["fateContribution"]!["policy"]!["enabled"]=false; off["fateContribution"]!["policy"]!["generation"]=null;
        var denied=Parse(off)!; check(denied is {Admission.Granted:false,PolicyEnabled:false},"default-off exact discovery is valid denial");
        var unsupported=off.DeepClone(); unsupported["fateContribution"]!["reason"]="FATE_SOURCE_UNSUPPORTED";
        check(FateDiscovery.Parse(unsupported.ToJsonString(),"session",device,source with {CollectorVersion="0.0.86.0"},now)
            is {Admission.Granted:false},"old Site matrix explicitly fences new collector");
        FateObservation Row(int second,int progress=1)=>new(1000,21,134,new("noninstanced",0),new("running",4),now.AddSeconds(second),
            (byte)progress,false,15,20,null,null,new((int)new DateTimeOffset(now).ToUnixTimeSeconds()-10,900));
        FatePrepared Batch(int second,int progress=1)=>FatePolicy.Prepare(1,source,[Row(second,progress)],now.AddSeconds(second))!;
        var local=new FateEpochState(); var context=new FateContext(123,21,134,0);
        using var state=new FateSenderState(); state.Bind("session:epoch",1);
        check(state.Discover(0) && !state.Discover(10000),"discovery single-flight"); state.DiscoveryFinished();
        check(!state.Discover(9999),"discovery10s floor");
        state.Install(grant,now,0);
        check(state.Take(Batch(0),source,"session",now,0)==null,"no pre-consent or same-clock replay");
        var first=Batch(5); var sent=state.Take(first,source,"session",now.AddSeconds(5),5000)!;
        check(sent==first && !state.Cancellation.IsCancellationRequested,"fresh admitted send");
        check(state.Take(Batch(6),source,"session",now.AddSeconds(6),6000)==null,"upload single-flight");
        state.Complete(first,FateSendDisposition.Retry,now.AddSeconds(6),6000); state.UploadFinished();
        check(state.Take(Batch(10,2),source,"session",now.AddSeconds(10),10000)==null,"network backoff floor");
        check(state.Take(Batch(11,2),source,"session",now.AddSeconds(11),11000)==first,"retry immutable ID/body not latest successor");
        state.Complete(first,FateSendDisposition.Acknowledged,now.AddSeconds(11),11000,1); state.UploadFinished();
        check(state.LastAccepted==1 && state.LastAcknowledged==now.AddSeconds(11),"bounded receipt delivery state");
        check(state.Take(first,source,"session",now.AddSeconds(16),16000)==null,"ACKed batch not repeated under new identity");
        var renewed=Batch(16); check(state.Take(renewed,source,"session",now.AddSeconds(16),16000)==renewed,"fresh renewal send");
        var cancellation=state.Cancellation;
        state.Install(denied,now.AddSeconds(17),17000);
        check(cancellation.IsCancellationRequested && !state.Authorized(now.AddSeconds(17),17000),"policyOFF cancels in-flight and unsent");
        state.UploadFinished(); state.Install(grant with {Admission=grant.Admission with {ExpiresAt=now.AddSeconds(50)},IssuedAt=now.AddSeconds(20)},now.AddSeconds(20),20000);
        check(state.Take(renewed,source,"session",now.AddSeconds(21),21000)==null,"OFF->ON no pre-consent replay");
        var after=Batch(25); check(state.Take(after,source,"session",now.AddSeconds(25),25000)==after,"OFF->ON fresh read");
        state.Complete(after,FateSendDisposition.Suspended,now.AddSeconds(25),25000); state.UploadFinished();
        check(!state.Authorized(now.AddSeconds(26),26000),"FATE-only denial suspends");
        state.Install(grant with {Admission=grant.Admission with {ExpiresAt=now.AddSeconds(60)},IssuedAt=now.AddSeconds(30)},now.AddSeconds(30),30000);
        check(state.Take(after,source,"session",now.AddSeconds(31),31000)==null,"reacquired denial discards old snapshot");
        check(state.Take(Batch(35),source,"session",now.AddSeconds(35),35000)!=null,"new discovery resumes fresh observations");
        state.UploadFinished(); cancellation=state.Cancellation; state.Bind("new-session",2);
        check(cancellation.IsCancellationRequested && state.Grant==null && state.Take(Batch(36),source,"session",now.AddSeconds(36),36000)==null,"session/context cancels old batch");
        state.Install(grant,now,40000); cancellation=state.Cancellation; state.Maintain(now.AddSeconds(-1),70000);
        check(state.Grant==null && cancellation.IsCancellationRequested,"monotonic grant expiry despite clock rollback");
        local.Observe(context,context,source,[Row(0)],now); var original=local.Prepared;
        local.CancelUnsent(); local.Observe(context,context,source,[Row(5)],now.AddSeconds(5));
        check(local.Prepared==null,"ACK does not force unchanged5s uploads");
        local.Observe(context,context,source,[Row(10)],now.AddSeconds(10));
        check(local.Prepared is not null && local.Prepared.BatchId!=original!.BatchId,"normal10s positive renewal");
        for(int status=0;status<4;status++) {
            using var deniedState=new FateSenderState(); deniedState.Bind("s",1); deniedState.Install(denied,now,0);
            check(deniedState.Take(Batch(5),source,"session",now.AddSeconds(5),5000)==null,"OFF cannot send regardless ordinary/PF/Market toggles");
        }
        check(FateDiscovery.Error("{\"ok\":false,\"code\":\"FATE_RATE_LIMITED\",\"retryAfterSeconds\":60}",out var code,out var retry)
            && code=="FATE_RATE_LIMITED" && retry==60,"exact retry error");
        foreach(var bad in new[]{"{}","{\"ok\":false,\"code\":\"private-echo\"}","{\"ok\":false,\"code\":\"FATE_RATE_LIMITED\",\"retryAfterSeconds\":0}",
            "{\"ok\":false,\"code\":\"FATE_RATE_LIMITED\",\"code\":\"FATE_RATE_LIMITED\"}"})
            check(!FateDiscovery.Error(bad,out _,out _),"unknown/malformed errors cannot bypass gates");
        using var correction=new FateSenderState(); correction.Bind("binding",1); correction.Install(grant,now,0);
        var badBatch=Batch(5); correction.Take(badBatch,source,"session",now.AddSeconds(5),5000);
        correction.Complete(badBatch,FateSendDisposition.Invalid,now.AddSeconds(5),5000); correction.UploadFinished();
        correction.Install(grant,now.AddSeconds(10),10000);
        check(!correction.Authorized(now.AddSeconds(11),11000)
            && correction.Take(Batch(15),source,"session",now.AddSeconds(15),15000)==null,"400/409/413/415/malformed receipt requires correction, no blind replacement after discovery");
        // A completed network response can wait behind the framework until the
        // transport timeout fires. Only actual session/context/feature authority
        // decides whether its classified disposition may commit.
        using var timeout=new CancellationTokenSource(); using var feature=new CancellationTokenSource();
        timeout.Cancel();
        check(FateTransportPolicy.CanCommit(true,true,feature.Token),"delayed framework disposition survives HTTP timeout");
        state.Bind("delayed",1); state.Install(grant,now,0);
        if(FateTransportPolicy.CanCommit(true,true,feature.Token)) state.Install(denied,now.AddSeconds(11),11000);
        check(!state.Authorized(now.AddSeconds(11),11000),"classified OFF commits after HTTP deadline and cancels grant");
        feature.Cancel(); check(!FateTransportPolicy.CanCommit(true,true,feature.Token),"actual feature cancellation suppresses stale disposition");
        check(!FateTransportPolicy.CanCommit(false,true,CancellationToken.None) && !FateTransportPolicy.CanCommit(true,false,CancellationToken.None),"changed session/context suppresses stale disposition");
        foreach(int status in new[]{401,403,404,422,429,503,502,400,409,413,415,200,201}) {
            foreach(var failed in new[]{false,true}) {
                var expected=status is 401 or 403 or 404 or 422 ? FateSendDisposition.Suspended
                    : status is 429 or >=500 || status is 200 or 201 && failed ? FateSendDisposition.Retry : FateSendDisposition.Invalid;
                check(FateTransportPolicy.Response(status,false,failed)==expected,"empty/malformed/stalled body preserves HTTP disposition "+status);
            }
        }
        foreach(var error in new Exception[]{new HttpIOException(HttpRequestError.ResponseEnded),new IOException("stream ended"),
            new HttpRequestException(),new OperationCanceledException(),new JsonException(),new InvalidOperationException()}) {
            var failed=FateTransportPolicy.TransportFailure(error);
            check(failed==(error is IOException or HttpRequestException or OperationCanceledException),"response-stream failure classification "+error.GetType().Name);
            foreach(int status in new[]{200,201,401,403,422,429,503}) {
                var expected=status is 401 or 403 or 422 ? FateSendDisposition.Suspended
                    : status is 429 or 503 || failed ? FateSendDisposition.Retry : FateSendDisposition.Invalid;
                check(FateTransportPolicy.Response(status,false,failed)==expected,"truncated versus malformed complete response "+status);
            }
        }
        check(FateTransportPolicy.Response(null,false)==FateSendDisposition.Retry,"network failure before headers retry same body");
        foreach(var outcome in new[]{FateSendDisposition.Retry,FateSendDisposition.Suspended,FateSendDisposition.Invalid,FateSendDisposition.Acknowledged}) {
            using var late=new FateSenderState(); late.Bind("late",1); late.Install(grant,now,0);
            late.Install(grant with {Admission=grant.Admission with {ExpiresAt=now.AddSeconds(50)},IssuedAt=now.AddSeconds(20)},now.AddSeconds(20),20000);
            var aged=Batch(5); check(late.Take(aged,source,"session",now.AddSeconds(28),28000)==aged,"dispatch age23 under renewed grant");
            late.Maintain(now.AddSeconds(36),36000); // age31: no longer a retryable payload
            late.Complete(aged,outcome,now.AddSeconds(36),36000,1,30); late.UploadFinished();
            if(outcome==FateSendDisposition.Retry)
                check(late.Take(Batch(37),source,"session",now.AddSeconds(37),37000)==null,"in-flight expiry cannot erase503/Retry-After30");
            else if(outcome is FateSendDisposition.Suspended or FateSendDisposition.Invalid)
                check(!late.Authorized(now.AddSeconds(37),37000),"in-flight expiry cannot erase suspension/correction");
            else check(late.LastAcknowledged==now.AddSeconds(36),"in-flight expiry preserves valid logical receipt");
        }
        using var backoff=new FateSenderState(); backoff.Bind("failures",1);
        long attempt=0;
        foreach(long next in new long[]{10000,20000,40000,80000}) {
            check(backoff.Discover(attempt),"discovery attempt at growing backoff"); backoff.DiscoveryFinished(); backoff.DiscoveryFailed(attempt);
            check(!backoff.Discover(next-1),"discovery failure exponential floor survives Install(null)"); attempt=next;
        }
        backoff.Install(grant,now,attempt); // a validated denial also counts as successful discovery
    }
}
