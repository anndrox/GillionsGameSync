using System.Reflection;
using Dalamud.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Separate processes load predecessor and candidate with the same real product
// name. Only synthetic credentials/state are used; no plugin constructor runs.
internal static class StableUpgradeTests {
    internal static void Run(Assembly assembly, string directory, string mode) {
        Directory.CreateDirectory(directory);
        var type=assembly.GetType("GillionsGameSync.PluginConfiguration",true)!;
        var serialize=typeof(PluginConfigurations).GetMethod("SerializeConfig",BindingFlags.Static|BindingFlags.NonPublic)!;
        var storage=new PluginConfigurations(directory);
        var load=typeof(PluginConfigurations).GetMethod("LoadForType")!.MakeGenericMethod(type);
        string path=storage.GetConfigFile("GillionsGameSync").FullName;
        int checks=0;
        void Check(bool ok,string message) { checks++; if(!ok)throw new Exception("Stable upgrade: "+message); }
        string Save(object value)=>(string)serialize.Invoke(null,[value])!;
        JObject Parse(string text) { using var reader=new JsonTextReader(new StringReader(text)){DateParseHandling=DateParseHandling.None};return JObject.Load(reader); }
        if(mode=="--upgrade-write") {
            Check(assembly.GetName().Name=="GillionsGameSync" && assembly.GetName().Version!.ToString(4)=="1.0.30.0","real immutable predecessor required");
            var value=Activator.CreateInstance(type)!;
            foreach(var name in new[]{"AutomaticSync","EnableItemLinkRequests","EnableAutoRetainerVenturePlans"})type.GetProperty(name)!.SetValue(value,false);
            Check(type.GetProperty("EnableGillionsPartyFinderContributions") is null,"predecessor has no Gillions PF consent to migrate");
            type.GetProperty("LastReadChangelogVersion")!.SetValue(value,"1.0.30");
            type.GetProperty("AutoRetainerVenturePlanBackups")!.SetValue(value,JObject.Parse("{\"synthetic\":{\"unknownLegacyField\":17}}"));
            const string token="SYNTHETIC_DEVICE_CREDENTIAL_0000000000";
            const string device="00000000-0000-4000-8000-000000000001";
            type.GetProperty("DeviceToken")!.SetValue(value,token); type.GetProperty("DeviceId")!.SetValue(value,device);
            var session=assembly.GetType("GillionsGameSync.PairedSession",true)!.GetMethod("Create")!.Invoke(null,["https://gillions.app",device,token]);
            type.GetProperty("ActiveSession")!.SetValue(value,session);
            type.GetProperty("ServerUrl")!.SetValue(value,"https://gillions.app");
            var predecessor=Save(value);
            File.WriteAllText(Path.Combine(directory,"predecessor.json"),predecessor);
            File.WriteAllText(path,predecessor);
            Console.WriteLine($"Real Stable30 serializer fixture PASS: {checks} identity checks; synthetic data only.");return;
        }
        string original=File.ReadAllText(Path.Combine(directory,"predecessor.json"));
        if(mode=="--rollback-check") {
            Check(assembly.GetName().Version!.ToString(4)=="1.0.30.0","rollback uses exact predecessor");
            var upgraded=Parse(File.ReadAllText(Path.Combine(directory,"upgraded.json")));
            File.WriteAllText(path,original); // disposable recovery with preserved pre-upgrade backup
            var restored=load.Invoke(storage,["GillionsGameSync"])!;
            Check(JToken.DeepEquals(Parse(Save(restored)),Parse(original)),"backup rollback must exactly preserve old config");
            // Older serializer need not preserve unknown new fields: do not use
            // its save output as a lossless downgrade of candidate configuration.
            Check(upgraded.Property("HuntBills") is not null,"candidate retained additive schema evidence");
            Console.WriteLine($"Real Stable30 backup rollback PASS: {checks} checks; additive-field downgrade is not lossless.");return;
        }
        Check(assembly.GetName().Name=="GillionsGameSync" && assembly.GetName().Version!.ToString(4)=="1.0.31.0","exact public candidate required");
        File.WriteAllText(path,original);
        var loaded=load.Invoke(storage,["GillionsGameSync"])!;
        string saved=Save(loaded); var before=Parse(original);var after=Parse(saved);
        foreach(var property in before.Properties()) Check(JToken.DeepEquals(property.Value,after[property.Name]),"predecessor field changed: "+property.Name);
        var sessionValue=type.GetProperty("ActiveSession")!.GetValue(loaded)!;
        Check((bool)sessionValue.GetType().GetMethod("IsValid")!.Invoke(sessionValue,[type.GetProperty("DeviceId")!.GetValue(loaded),type.GetProperty("DeviceToken")!.GetValue(loaded)])!,"pairing remains valid without re-pair");
        Check(!(bool)type.GetProperty("PairingRequired")!.GetValue(loaded)!,"no forced pairing");
        foreach(var name in new[]{"ShowHuntProgress","LockHuntProgressPosition","SyncPersonalHunts","SyncPersonalSubmarines","ShareHuntRoutingLocation","AutomaticallyShowHuntMap","EnablePartyFinderLinkRequests"})Check(!(bool)type.GetProperty(name)!.GetValue(loaded)!,"new legacy permission defaults OFF: "+name);
        Check(!(bool)type.GetProperty("EnableGillionsPartyFinderContributions")!.GetValue(loaded)!,"upgrade must not invent Gillions PF consent");
        File.WriteAllText(path,saved); var restarted=load.Invoke(storage,["GillionsGameSync"])!;
        Check(JToken.DeepEquals(after,Parse(Save(restarted))),"restart preserves all candidate config");
        File.WriteAllText(Path.Combine(directory,"upgraded.json"),saved);
        File.WriteAllText(Path.Combine(directory,"upgrade-result.json"),new JObject{["product"]="GillionsGameSync",["predecessor"]="1.0.30.0",["candidate"]="1.0.31.0",["checks"]=checks,["result"]="PASS",["syntheticOnly"]=true,["pairingPreserved"]=true,["geometry"]="Dalamud-managed, no config migration or geometry write"}.ToString());
        Console.WriteLine($"Real Stable30 -> Public31 upgrade PASS: {checks} checks; actual Dalamud serializer; no live game.");
    }
}
