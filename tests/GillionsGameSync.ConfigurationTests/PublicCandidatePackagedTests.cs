using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Newtonsoft.Json;

internal static class PublicCandidatePackagedTests {
    internal static void Run(Assembly a) {
        int count=0;
        void Check(bool ok,string message){count++;if(!ok)throw new Exception("Public candidate: "+message);}
        const BindingFlags instance=BindingFlags.NonPublic|BindingFlags.Instance,statics=BindingFlags.NonPublic|BindingFlags.Static;
        bool testing=a.GetName().Name=="GillionsGameSyncTest";
        var plugin=a.GetType("GillionsGameSync.Plugin",true)!;
        var configType=a.GetType("GillionsGameSync.PluginConfiguration",true)!;
        Check((string)a.GetType("GillionsGameSync.NativeProduct",true)!.GetField("Name",statics)!.GetRawConstantValue()! == a.GetName().Name,"compile-selected product");
        Check((string)plugin.GetField("CommandName",statics)!.GetRawConstantValue()! == (testing?"/gillionssynctest":"/gillionssync"),"channel command isolation");
        foreach(var name in new[]{"FateLocalView","SubmarineLocalView","TravelContextLocalView","HuntBillLocalView"}) {
            var type=a.GetType("GillionsGameSync."+name,true)!;
            Check((type.GetMethod("Draw",instance)!=null)==testing,"diagnostic renderer isolation: "+name);
            Check((type.GetMethod("Show",instance)!=null)==testing,"diagnostic opener isolation: "+name);
        }
        Check((a.GetType("GillionsGameSync.FateLocalView",true)!.GetMethod("Diagnostic",instance)!=null)==testing,"private FATE export isolation");
        foreach(var name in new[]{"BeastmasterLocalView","DashboardLocalView","DashboardRetention"})Check((a.GetType("GillionsGameSync."+name)!=null)==testing,"research exclusion: "+name);
        // No default/new origin impersonation: each contract parser must accept
        // this binary's product and reject the other channel's acknowledgment.
        var product=a.GetName().Name!;var other=testing?"GillionsGameSync":"GillionsGameSyncTest";
        var market=a.GetType("GillionsGameSync.MarketContributor",true)!.GetMethod("Compatible",statics,[typeof(string)])!;
        string Market(string p)=>new JsonObject{["ok"]=true,["marketContribution"]=new JsonObject{["contractVersion"]=1,["serviceAvailable"]=true,["enabled"]=true,["acceptedClientProduct"]=p}}.ToJsonString();
        Check((bool)market.Invoke(null,[Market(product)])!,"matching Market product");
        Check(!(bool)market.Invoke(null,[Market(other)])!,"other Market product denied");
        var config=Activator.CreateInstance(configType)!;
        const string device="00000000-0000-4000-8000-000000000001",token="SYNTHETIC_DEVICE_CREDENTIAL_0000000000";
        var session=a.GetType("GillionsGameSync.PairedSession",true)!.GetMethod("Create")!.Invoke(null,["https://test.gillions.app",device,token]);
        configType.GetProperty("ActiveSession")!.SetValue(config,session);configType.GetProperty("DeviceId")!.SetValue(config,device);configType.GetProperty("DeviceToken")!.SetValue(config,token);
        var p=RuntimeHelpers.GetUninitializedObject(plugin);
        plugin.GetField("configuration",instance)!.SetValue(p,config);
        plugin.GetField("activeOwnedState",instance)!.SetValue(p,Activator.CreateInstance(a.GetType("GillionsGameSync.OwnedCharacterState",true)!));
        var authorityType=a.GetType("GillionsGameSync.PermissionAuthority",true)!;
        var authority=Activator.CreateInstance(authorityType,true)!;plugin.GetField("permissionAuthority",instance)!.SetValue(p,authority);
        authorityType.GetMethod("Bind",instance)!.Invoke(authority,["fixture:character",device]);
        var now=DateTime.UtcNow;var clock=Environment.TickCount64;
        JsonObject Authority(string state,bool enabled) {
            var decisions=new JsonObject();
            foreach(var key in (string[])authorityType.GetField("Keys",statics)!.GetValue(null)!)decisions[key]=new JsonObject{["scope"]=key=="marketContribution"?"account":"device",["state"]=state,["enabled"]=state=="explicit"?JsonValue.Create(enabled):null,["generation"]=state=="explicit"?"00000000-0000-4000-8000-000000000002":null,["decidedAt"]=state=="explicit"?now.AddSeconds(-1).ToString("O"):null};
            return new JsonObject{["ok"]=true,["permissionAuthority"]=new JsonObject{["revision"]=1,["deviceBinding"]=new JsonObject{["deviceId"]=device,["pairedAt"]=now.AddDays(-1).ToString("O"),["characterId"]=100},["issuedAt"]=now.ToString("O"),["expiresAt"]=now.AddSeconds(30).ToString("O"),["permissions"]=decisions}};
        }
        bool Personal(string resource)=>(bool)plugin.GetMethod("PersonalEnabled",instance)!.Invoke(p,[resource])!;
        string before=JsonConvert.SerializeObject(config);
        Check((bool)authorityType.GetMethod("Apply",instance)!.Invoke(authority,[Authority("legacy",false).ToJsonString(),now,clock,null])!,"legacy fixture accepted");
        Check(!Personal("hunt_bills")&&!Personal("submarine_personal"),"historical OFF retained");
        Check((bool)authorityType.GetMethod("Apply",instance)!.Invoke(authority,[Authority("explicit",true).ToJsonString(),now,clock,null])!,"explicit fixture accepted");
        Check(Personal("hunt_bills")&&Personal("submarine_personal"),"explicit consent is not blocked by an inaccessible local Testing-only toggle");
        Check(JsonConvert.SerializeObject(config)==before,"Site authority never rewrites historical configuration");
        authorityType.GetMethod("Invalidate",instance)!.Invoke(authority,[]);
        Check(!Personal("hunt_bills")&&!Personal("submarine_personal"),"invalid lease closes both personal resources");
        foreach(var name in new[]{"HuntBillRetentionPolicy","SubmarineVoyageRetentionPolicy"}) {
            var policyType=a.GetType("GillionsGameSync."+name,true)!;var store=Activator.CreateInstance(policyType.GetConstructors().Single().GetParameters()[0].ParameterType)!;
            var policy=Activator.CreateInstance(policyType,[store])!;
            var gate=policyType.GetProperty("RetentionEnabled",instance)!;
            Check(!(bool)gate.GetValue(policy)!,"retention defaults OFF");
            bool allowed=true; policyType.GetProperty("SiteRetentionAuthorized",instance)!.SetValue(policy,(Func<bool>)(()=>allowed));
            Check((bool)gate.GetValue(policy)!,"explicit retention authority works");allowed=false;
            Check(!(bool)gate.GetValue(policy)!,"lease callback fails closed immediately");
        }
        Console.WriteLine($"Exact public capability/product/diagnostic/permission checks PASS: {count}; {product}; synthetic only.");
    }
}
