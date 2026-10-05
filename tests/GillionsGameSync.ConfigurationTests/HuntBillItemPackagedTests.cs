using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

internal static class HuntBillItemPackagedTests {
    internal static void Run(Assembly a, bool testing) {
        var coverage = a.GetType("GillionsGameSync.HuntBillItemCoverage");
        var sync = a.GetType("GillionsGameSync.HuntBillItemSync");
        if (!testing) { Require(coverage is null && sync is null,"Stable gained absence coverage"); return; }
        Require(coverage is not null && sync is not null,"Testing missing coverage");
        var domain=a.GetType("GillionsGameSync.HuntBillItemDomain",true)!;
        var slot=a.GetType("GillionsGameSync.HuntKeyItemSlot",true)!;
        var domains=Array.CreateInstance(domain,22);
        uint[] ids=[2001361,2001700,2001701,2001702,2001362,2001703,2002113,2002114,2002115,2002116,2002628,2002629,2002630,2002631,2003090,2003091,2003092,2003093,2003509,2003510,2003511,2003512];
        for(int i=0;i<22;i++) domains.SetValue(Activator.CreateInstance(domain,[(byte)i,ids[i],(byte)1,1u,10u]),i);
        var slots=Array.CreateInstance(slot,2);
        slots.SetValue(Activator.CreateInstance(slot,[0,2004,false,2003509u,1,false]),0);
        slots.SetValue(Activator.CreateInstance(slot,[1,2004,false,0u,0,false]),1);
        var current=Activator.CreateInstance(coverage!,true)!;
        const BindingFlags instance=BindingFlags.Instance|BindingFlags.NonPublic;
        coverage!.GetMethod("SetCatalog",instance)!.Invoke(current,[domains]);
        var key=new string('a',64); var other=new string('b',64); var now=DateTime.UtcNow; var mono=Stopwatch.GetTimestamp();
        void Observe(string after,bool loaded) => coverage.GetMethod("Observe",instance)!.Invoke(current,[key,after,true,loaded,2004,2,slots,true,now,mono,"2026.09.15.0000.0000","0.0.83.0","7.56.2.9136",null]);
        object? Current(string owner) => coverage.GetMethod("Current",instance)!.Invoke(current,[owner,now,mono]);
        Observe(key,true);
        var snapshot=Current(key)!;
        var states=((Array)snapshot.GetType().GetProperty("Domains")!.GetValue(snapshot)!).Cast<object>().ToArray();
        Require(states.Length==22,"Exact DLL dropped domains");
        string State(object d)=>(string)d.GetType().GetProperty("State")!.GetValue(d)!;
        Require(State(states[18])=="present_unresolved" && states.Where((_,i)=>i!=18).All(d=>State(d)=="absent_confirmed"),"Exact DLL absence/presence overclaimed");
        slots.SetValue(Activator.CreateInstance(slot,[1,2004,false,0u,1,false]),1); Observe(key,true);
        Require(((Array)Current(key)!.GetType().GetProperty("Domains")!.GetValue(Current(key))!).Cast<object>().All(d=>State(d)=="unavailable"),"Exact DLL trusted residual quantity without per-slot native proof");
        slots.SetValue(Activator.CreateInstance(slot,[1,2004,false,0u,1,true]),1); Observe(key,true);
        Require(((Array)Current(key)!.GetType().GetProperty("Domains")!.GetValue(Current(key))!).Cast<object>().Count(d=>State(d)=="present_unresolved")==1,"Exact DLL rejected native-confirmed empty slot or lost held bill");
        slots.SetValue(Activator.CreateInstance(slot,[1,2004,false,0u,0,false]),1);
        Require(Current(other) is null,"Exact DLL cross-character leak");
        Observe(key,false);
        snapshot=Current(key)!;
        Require(((Array)snapshot.GetType().GetProperty("Domains")!.GetValue(snapshot)!).Cast<object>().All(d=>State(d)=="unavailable"),"Exact DLL unloaded absence");
        Observe(other,true); Require(Current(key) is null,"Exact DLL transition retained current absence");
        var config=a.GetType("GillionsGameSync.PluginConfiguration",true)!;
        Require(!config.GetProperties().Any(p=>p.PropertyType==coverage || p.PropertyType==sync || p.PropertyType.Name.StartsWith("HuntBillItem",StringComparison.Ordinal)),"Hunt item coverage became durable config");
        var policy=a.GetType("GillionsGameSync.PersonalSyncPolicy",true)!;
        var ack=JsonSerializer.Serialize(new{ok=true,acceptedClientProduct="GillionsGameSyncTest",personalObservations=new{contractVersion=1,endpoint="https://test.gillions.app/api/game-sync/sync",resources=new[]{new{resourceType="hunt_bills",schemaVersion=2,collectorSchema="hunt-bills-v2",capability="hunt_bills_v2",maxPayloadBytes=65536}}}});
        var compatible=policy.GetMethod("HuntCoverageCompatible",BindingFlags.Static|BindingFlags.NonPublic)!;
        Require((bool)compatible.Invoke(null,[ack])!,"Exact DLL compatible v2 gate failed");
        Require(!(bool)compatible.Invoke(null,[ack.Replace("hunt_bills_v2","hunt_bills_v1")])!,"Exact DLL silently accepts v1 for absence");
        var disposition=a.GetType("GillionsGameSync.PersonalResponseDisposition",true)!;
        var needsCurrent=sync!.GetMethod("NeedsCurrentSample",BindingFlags.Static|BindingFlags.NonPublic)!;
        foreach(var name in new[]{"Retry","Blocked","Canceled","Acknowledged"})
            Require((bool)needsCurrent.Invoke(null,[Enum.Parse(disposition,name)])! == (name=="Acknowledged"),"Exact DLL expiry suppresses response classification");
        Console.WriteLine("Exact packaged absence-only source/completeness/character/unavailable/version/RAM boundaries PASS; pure managed calls, no live memory or HTTP.");
    }
    private static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
}
