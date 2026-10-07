using System.Reflection;
using System.Text.Json.Nodes;

internal static class PermissionPackagedTests {
    internal static void Run(Assembly assembly) {
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        var type=assembly.GetType("GillionsGameSync.PermissionAuthority",true)!;
        var instance=Activator.CreateInstance(type,true)!;
        object? Call(string name,params object?[] values)=>type.GetMethod(name,flags)!.Invoke(instance,values);
        int count=0;
        void Check(bool value,string name) { count++;if(!value)throw new Exception("Packaged permission: "+name); }
        const string device="00000000-0000-4000-8000-000000000001";
        var issued=new DateTime(2026,10,7,12,0,1,DateTimeKind.Utc);
        var keys=(string[])type.GetField("Keys",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
        JsonObject Fixture(bool explicitDecision,bool enabled) {
            var decisions=new JsonObject();
            foreach(var key in keys) decisions[key]=new JsonObject {
                ["scope"]=key=="marketContribution"?"account":"device",["state"]=explicitDecision?"explicit":"legacy",
                ["enabled"]=explicitDecision?JsonValue.Create(enabled):null,
                ["generation"]=explicitDecision?"00000000-0000-4000-8000-000000000002":null,
                ["decidedAt"]=explicitDecision?issued.AddSeconds(-1).ToString("O"):null};
            return new JsonObject {["ok"]=true,["permissionAuthority"]=new JsonObject {
                ["revision"]=1,["deviceBinding"]=new JsonObject {["deviceId"]=device,["pairedAt"]=issued.AddDays(-1).ToString("O"),["characterId"]=100},
                ["issuedAt"]=issued.ToString("O"),["expiresAt"]=issued.AddSeconds(30).ToString("O"),["permissions"]=decisions}};
        }
        Call("Bind","synthetic-enrollment:content",device);
        Check((bool)Call("Apply",Fixture(false,false).ToJsonString(),issued,0L,null)!,"legacy authority accepted");
        foreach(var key in keys) {
            Check(!(bool)Call("Allows",key,false,0L)!,key+" historical OFF preserved");
            Check((bool)Call("Allows",key,true,0L)!,key+" historical ON preserved");
        }
        Check((bool)Call("Apply",Fixture(true,true).ToJsonString(),issued,0L,null)!,"explicit authority accepted");
        foreach(var key in keys)Check((bool)Call("Allows",key,false,0L)!,key+" fresh explicit ON supersedes local OFF");
        var mixed=Fixture(true,true);
        mixed["permissionAuthority"]!["permissions"]!["itemLinks"]!["enabled"]=false;
        mixed["permissionAuthority"]!["permissions"]!["automaticHuntMaps"]!["enabled"]=false;
        Check((bool)Call("Apply",mixed.ToJsonString(),issued,0L,null)!,"mixed authority accepted");
        Check(!(bool)Call("Allows","itemLinks",true,0L)!&&(bool)Call("PartyFinderLinks",false,false,0L)!,"PF receiving independent of item OFF");
        Check(!(bool)Call("Allows","automaticHuntMaps",true,0L)!&&(bool)Call("HuntReceiving",false,0L)!,"manual Hunt eligible while automatic OFF");
        foreach(var key in keys)Check(!(bool)Call("Allows",key,true,30000L)!,key+" exact lease expiry");
        Call("Invalidate");Check(!(bool)Call("HuntReceiving",true,0L)!,"missing/revoked lease closes presentation");
        var configuration=assembly.GetType("GillionsGameSync.PluginConfiguration",true)!;
        Check(!configuration.GetProperties().Any(p=>p.Name.Contains("PermissionAuthority")||p.Name.Contains("PermissionGeneration")),"authority/generations are not configuration");
        Console.WriteLine($"Exact packaged permission authority PASS: {count} checks; synthetic, no game/HTTP invocation.");
    }
}
