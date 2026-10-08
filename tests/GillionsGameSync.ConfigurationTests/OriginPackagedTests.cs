using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using JsonConvert=Newtonsoft.Json.JsonConvert;

// Actual compiled policies/request builders; synthetic credentials, no HTTP or
// plugin constructor, no access to the installed player's configuration.
internal static class OriginPackagedTests {
    internal static void Run(Assembly a) {
        const BindingFlags statics=BindingFlags.NonPublic|BindingFlags.Static, instance=BindingFlags.NonPublic|BindingFlags.Instance;
        Type T(string name)=>a.GetType("GillionsGameSync."+name,true)!;
        object? Call(string type,string method,params object?[] args)=>T(type).GetMethod(method,statics)!.Invoke(null,args);
        int checks=0;
        void Check(bool ok,string why){checks++;if(!ok)throw new Exception("Packaged origin: "+why);}
        bool testing=a.GetName().Name=="GillionsGameSyncTest";
        const string test="https://test.gillions.app", prod="https://gillions.app";
        string[] origins=[test,prod,"http://test.gillions.app","http://gillions.app","https://sub.gillions.app",
            "https://test.gillions.app.evil.example","https://gillions.app.evil.example","https://other.example",
            "https://gillions.app:444","https://test.gillions.app:444","https://gillions.app:443",
            "https://gillions.app/","https://gillions.app/path","https://gillions.app?x=1","https://user@gillions.app",
            "HTTPS://GILLIONS.APP",""];
        var prepared=Activator.CreateInstance(T("PersonalPreparedSnapshot"),["owner","hunt_bills","nonce","{}","hash"])!;
        foreach(var origin in origins) {
            bool allowed=origin==test || !testing && origin==prod;
            Check((bool)Call("NativeProduct","TransportOrigin",origin)! == allowed,"exact allow-list "+origin);
            Check((bool)Call("HuntMapPolicy","Admit",true,true,origin,true)! == allowed,"Hunt admission "+origin);
            Check(!(bool)Call("HuntMapPolicy","Admit",false,true,origin,true)!,"origin never grants Hunt consent");
            Check((bool)Call("TravelSyncPolicy","Admit",true,true,true,true,origin,true)! == allowed,"travel admission "+origin);
            Check(!(bool)Call("TravelSyncPolicy","Admit",true,true,false,true,origin,true)!,"origin never grants travel consent");
            Check((bool)Call("PersonalSyncPolicy","CanSend",true,true,origin,true,prepared)! == allowed,"personal admission "+origin);
            Check(!(bool)Call("PersonalSyncPolicy","CanSend",false,true,origin,true,prepared)!,"origin never grants personal consent");
            try {
                var endpoint=(Uri)Call("GillionsPartyFinderContributor","SessionEndpoint",origin)!;
                Check(allowed && endpoint.AbsoluteUri==origin+"/api/game-sync/party-finder/contribute","PF paired destination");
            } catch(TargetInvocationException e) when(e.InnerException is InvalidOperationException) {Check(!allowed,"PF rejects wrong origin");}
        }
        foreach(var origin in new[]{test,prod}) {
            bool allowed=origin==test || !testing;
            string other=origin==test?prod:test;
            string Ack(string endpoint,int schema=1)=>JsonSerializer.Serialize(new{ok=true,acceptedClientProduct=a.GetName().Name,
                personalObservations=new{contractVersion=1,endpoint=endpoint+"/api/game-sync/sync",resources=new[]{new{
                    resourceType="hunt_bills",schemaVersion=schema,collectorSchema="hunt-bills-v"+schema,capability="hunt_bills_v"+schema,maxPayloadBytes=65536}}}});
            Check((bool)Call("PersonalSyncPolicy","Compatible",Ack(origin),"hunt_bills",origin)! == allowed,"personal ACK binds to paired origin");
            Check(!(bool)Call("PersonalSyncPolicy","Compatible",Ack(other),"hunt_bills",origin)!,"personal ACK cannot cross environments");
            Check((bool)Call("PersonalSyncPolicy","HuntCoverageCompatible",Ack(origin,2),origin)! == allowed,"Hunt V2 ACK binds to paired origin");
            Check(!(bool)Call("PersonalSyncPolicy","HuntCoverageCompatible",Ack(other,2),origin)!,"Hunt V2 ACK cannot cross environments");
            object Constant(string type,string name)=>T(type).GetField(name,statics)!.GetRawConstantValue()!;
            string Travel(string endpoint)=>JsonSerializer.Serialize(new{ok=true,acceptedClientProduct=a.GetName().Name,
                personalObservations=new{contractVersion=1,endpoint=endpoint+"/api/game-sync/sync",resources=new[]{new{
                    resourceType="travel_context",schemaVersion=1,collectorSchema="travel-context-v1",transportContract="travel-context-v1",capability="travel_context_v1",
                    maxPayloadBytes=Constant("TravelPolicy","MaximumBytes"),maxEnvelopeBytes=69632,maxDepth=Constant("TravelPolicy","MaximumDepth"),
                    maxDestinations=Constant("TravelPolicy","MaximumDestinations"),ttlSeconds=Constant("TravelPolicy","TtlSeconds"),
                    resourceHeader="x-gillions-personal-resource",capabilityHeader="x-gillions-personal-capability"}}}});
            Check((bool)Call("TravelSyncPolicy","Compatible",Travel(origin),origin)! == allowed,"travel ACK binds to paired origin");
            Check(!(bool)Call("TravelSyncPolicy","Compatible",Travel(other),origin)!,"travel ACK cannot cross environments");
            const string device="00000000-0000-4000-8000-000000000001",token="SYNTHETIC_DEVICE_CREDENTIAL_0000000000";
            var session=T("PairedSession").GetMethod("Create")!.Invoke(null,[origin,device,token])!;
            var config=Activator.CreateInstance(T("PluginConfiguration"))!;
            foreach(var (key,value) in new (string,object)[]{("ActiveSession",session),("ServerUrl",origin),("DeviceId",device),("DeviceToken",token)})
                config.GetType().GetProperty(key)!.SetValue(config,value);
            string before=JsonConvert.SerializeObject(config);
            var restored=JsonConvert.DeserializeObject(before,config.GetType())!;
            Check(JsonConvert.SerializeObject(restored)==before,"synthetic config round-trip unchanged");
            Check((string)config.GetType().GetProperty("ServerUrl")!.GetValue(restored)! == origin,"server selection preserved");
            var restoredSession=config.GetType().GetProperty("ActiveSession")!.GetValue(restored)!;
            Check((bool)T("PairedSession").GetMethod("IsValid")!.Invoke(restoredSession,[device,token])!,"existing pairing survives");
            using var lifetime=(IDisposable)Activator.CreateInstance(T("SyncRequestLifetime"))!;
            var mode=Enum.Parse(T("SyncRequestMode"),"Manual");
            var permit=lifetime.GetType().GetMethod("Capture")!.Invoke(lifetime,[mode,1UL,session,origin,token])!;
            var plugin=RuntimeHelpers.GetUninitializedObject(T("Plugin"));
            foreach(var path in new[]{"/api/game-sync/presence","/api/game-sync/item-links/poll","/api/game-sync/item-links/consume"}) {
                using var request=(HttpRequestMessage)T("Plugin").GetMethod("Request",instance)!.Invoke(plugin,[path,permit,new{synthetic=true}])!;
                Check(request.RequestUri!.AbsoluteUri==origin+path && request.Headers.Authorization?.Parameter==token,"request keeps paired origin/token");
            }
            using var snapshot=(HttpRequestMessage)T("Plugin").GetMethod("SnapshotRequest",instance)!.Invoke(plugin,["/api/game-sync/sync",permit,"hunt_bills","nonce","{}"u8.ToArray()])!;
            Check(snapshot.RequestUri!.AbsoluteUri==origin+"/api/game-sync/sync" && snapshot.Headers.Authorization?.Parameter==token,"personal request keeps pairing");
            foreach(var responseOrigin in new[]{origin,other,"http://gillions.app","https://other.example"}) {
                using var response=new HttpResponseMessage(System.Net.HttpStatusCode.OK){RequestMessage=new HttpRequestMessage(HttpMethod.Post,responseOrigin+"/api/game-sync/presence")};
                response.Headers.Date=DateTimeOffset.UtcNow;
                var clock=T("Plugin").GetMethod("CommandResponseClock",statics)!.Invoke(null,[response,permit,TimeSpan.FromMilliseconds(1)])!;
                Check((bool)T("WebsiteResponseClock").GetProperty("IssuerTime",instance)!.GetValue(clock)! == (allowed && responseOrigin==origin),"issuer time requires same paired approved origin");
            }
            var otherSession=T("PairedSession").GetMethod("Create")!.Invoke(null,[other,device,token])!;
            Check(!(bool)lifetime.GetType().GetMethod("Accepts")!.Invoke(lifetime,[permit,1UL,otherSession,token,true,true])!,"captured permit invalid after environment/pairing change");
        }
        Console.WriteLine($"Exact packaged origin/environment/ACK/config/request isolation PASS: {checks}; synthetic, no HTTP/game/player config.");
    }
}
