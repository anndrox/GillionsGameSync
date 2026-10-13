using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Configuration;
using Newtonsoft.Json;

// Disposable actual Dalamud serializer/files only. Never instantiates Plugin.
internal static class ChannelIsolationPackagedTests {
    internal static void Run(Assembly assembly,string directory) {
        if(assembly.GetName().Name!="GillionsGameSyncTest") return;
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var type=assembly.GetType("GillionsGameSync.PluginConfiguration",true)!;
        var storage=new PluginConfigurations(Path.Combine(directory,"channel-isolation"));
        Directory.CreateDirectory(Path.Combine(directory,"channel-isolation"));
        string stable=storage.GetConfigFile("GillionsGameSync").FullName;
        string testing=storage.GetConfigFile("GillionsGameSyncTest").FullName;
        int checks=0;
        void Check(bool value,string why){checks++;if(!value)throw new InvalidOperationException(why);}
        Check(stable!=testing,"channel filenames must be distinct");
        const string stableContent="{\"DeviceToken\":\"SYNTHETIC_PUBLIC_CREDENTIAL_0000000000\",\"marker\":17}";
        File.WriteAllText(stable,stableContent);
        var load=typeof(PluginConfigurations).GetMethod("LoadForType")!.MakeGenericMethod(type);
        var fresh=load.Invoke(storage,["GillionsGameSyncTest"]) ?? Activator.CreateInstance(type)!;
        Check(string.IsNullOrEmpty((string?)type.GetProperty("DeviceToken")!.GetValue(fresh)),"fresh Testing cannot inherit Public credential");
        Check(type.GetProperty("ActiveSession")!.GetValue(fresh) is null,"fresh Testing cannot inherit Public pairing");
        var serialize=typeof(PluginConfigurations).GetMethod("SerializeConfig",BindingFlags.Static|BindingFlags.NonPublic)!;
        File.WriteAllText(testing,(string)serialize.Invoke(null,[fresh])!);
        Check(File.ReadAllText(stable)==stableContent,"Testing serialization must not overwrite Public");
        const string device="00000000-0000-4000-8000-000000000001",token="SYNTHETIC_TESTING_CREDENTIAL_0000000000";
        type.GetProperty("DeviceId")!.SetValue(fresh,device);type.GetProperty("DeviceToken")!.SetValue(fresh,token);
        var sessionType=assembly.GetType("GillionsGameSync.PairedSession",true)!;
        type.GetProperty("ActiveSession")!.SetValue(fresh,sessionType.GetMethod("Create")!.Invoke(null,["https://gillions.app",device,token]));
        string retained=JsonConvert.SerializeObject(fresh);
        var plugin=RuntimeHelpers.GetUninitializedObject(assembly.GetType("GillionsGameSync.Plugin",true)!);
        plugin.GetType().GetField("configuration",flags)!.SetValue(plugin,fresh);
        Check(!(bool)plugin.GetType().GetProperty("HasPairedSession",flags)!.GetValue(plugin)!,"historical Testing production session must be inactive");
        Check(JsonConvert.SerializeObject(fresh)==retained,"inactivating unsupported origin must not mutate its credential/history");
        type.GetProperty("ActiveSession")!.SetValue(fresh,sessionType.GetMethod("Create")!.Invoke(null,["https://test.gillions.app",device,token]));
        Check((bool)plugin.GetType().GetProperty("HasPairedSession",flags)!.GetValue(plugin)!,"separate verified TEST session remains usable");
        File.Delete(testing); // disposable Testing uninstall; no owner files
        Check(File.ReadAllText(stable)==stableContent,"Testing removal must preserve Public file");
        Console.WriteLine($"Exact channel isolation PASS: {checks}; actual disposable Dalamud config, no player files/network/game.");
    }
}
