using System.Reflection;
using System.Text.Json;

internal static class FatePackagedTests {
    internal static void Run(Assembly assembly, bool testing, string fixtureDirectory) {
        int checks=0;
        void Check(bool ok,string message) { checks++; if (!ok) throw new Exception(message); }
        string[] names=["FateLocalView","FatePolicy","FatePrepared","FateEpochState","FateTransportPolicy","FateAdmission","FateMeasurements"];
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
        Directory.CreateDirectory(fixtureDirectory);
        File.WriteAllBytes(Path.Combine(fixtureDirectory,"packaged-fate-live-observations-v1.json"),body.ToArray());
        Console.WriteLine($"Exact packaged FATE schema/privacy/no-admission/config boundaries PASS: {checks} checks. Synthetic; no native getter or live HTTP call.");
    }
}
