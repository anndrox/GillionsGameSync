using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Dalamud.Plugin.Services;

// Actual installed managed ABI/API inspection. Never invoke native getters,
// constructors, hooks or HTTP. This cannot establish physical game correctness.
internal static class OctoberCompatibilityPackagedTests {
    internal static void Run(Assembly plugin,string libraries) {
        var checks=0;
        void Check(bool value,string why){checks++;if(!value)throw new Exception("October compatibility: "+why);}
        var native=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(libraries,"FFXIVClientStructs.dll"));
        Check(native.GetName().Version!.ToString()=="7.57.0.9378","inspected current native version");
        Check(typeof(IFramework).Assembly.GetName().Version!.ToString()=="15.0.3.7","inspected current Dalamud version");
        Type N(string name)=>native.GetType("FFXIVClientStructs.FFXIV.Client.Game."+name,true)!;
        void Offset(Type t,string f,int at)=>Check(t.GetField(f,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!
            .GetCustomAttribute<FieldOffsetAttribute>()?.Value==at,t.Name+"."+f+" offset");
        var hunt=N("UI.MobHunt");Check(hunt.StructLayoutAttribute!.Size==0x1f0,"MobHunt size");
        Offset(hunt,"_availableMarkId",0x08);Offset(hunt,"_obtainedMarkId",0x1e);Offset(hunt,"_currentKills",0x34);Offset(hunt,"ObtainedFlags",0x1ec);
        Check(hunt.GetMethod("GetKillCount")!.ReturnType==typeof(int)
            && hunt.GetMethod("GetKillCount")!.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(byte),typeof(byte)}),"kill accessor ABI");
        foreach(var name in new[]{"GetObtainedHuntOrderRowId","GetAvailableHuntOrderRowId"})
            Check(hunt.GetMethod(name)!.ReturnType==typeof(int)&&hunt.GetMethod(name)!.GetParameters().Single().ParameterType==typeof(byte),"order accessor ABI");
        var item=N("InventoryItem");Check(item.StructLayoutAttribute!.Size==0x48,"InventoryItem size");
        Offset(item,"Container",0x08);Offset(item,"Slot",0x0c);Offset(item,"IsSymbolic",0x0e);Offset(item,"ItemId",0x10);Offset(item,"Quantity",0x14);
        Check(item.GetMethod("IsEmpty")!.ReturnType==typeof(bool)&&item.GetMethod("GetBaseItemId")!.ReturnType==typeof(uint)
            &&item.GetMethod("GetQuantity")!.ReturnType==typeof(uint),"read-only key-item virtual accessor ABI");
        var fate=N("Fate.FateContext");Check(fate.StructLayoutAttribute!.Size==0x29d0,"FateContext size");
        foreach(var pair in new[]{("FateId",0x18),("StartTimeEpoch",0x20),("Duration",0x28),("State",0x3ac),
            ("Progress",0x3b7),("IsBonus",0x3c1),("Level",0x7f3),("MaxLevel",0x7f4),("Location",0x850),("Radius",0x864)})Offset(fate,pair.Item1,pair.Item2);
        var api=typeof(IFateTable).Assembly;
        var wrapped=api.GetType("Dalamud.Game.ClientState.Fates.IFate",true)!;
        foreach(var pair in new[]{("FateId",typeof(ushort)),("Progress",typeof(byte)),("HasBonus",typeof(bool)),("Level",typeof(byte)),
            ("MaxLevel",typeof(byte)),("StartTimeEpoch",typeof(int)),("Duration",typeof(short)),("Position",typeof(System.Numerics.Vector3)),("Radius",typeof(float))})
            Check(wrapped.GetProperty(pair.Item1)!.PropertyType==pair.Item2,"maintained IFate property "+pair.Item1);
        Check(typeof(IClientState).GetProperty("Instance")!.PropertyType==typeof(uint),"public instance ordinal");
        foreach(var name in new[]{"Login","Logout","ZoneInit","TerritoryChanged","InstanceChanged","MapIdChanged"})Check(typeof(IClientState).GetEvent(name)!=null,"existing lifecycle event "+name);
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var helper=plugin.GetType("GillionsGameSync.HuntObservationCompatibility",true)!.GetMethod("Supports",flags)!;
        Check((bool)helper.Invoke(null,new object[]{"2026.10.06.0000.0000","7.57.0.9378"})!,"packaged October Hunt gate");
        Check(!(bool)helper.Invoke(null,new object[]{"2026.10.06.0000.0000","7.57.0.9379"})!,"packaged future SDK fence");
        Check(!(bool)helper.Invoke(null,new object[]{"2026.10.07.0000.0000","7.57.0.9378"})!,"packaged future game fence");
        var other=plugin.GetType("GillionsGameSync.PersonalObservationCompatibility",true)!.GetMethod("Supports",flags)!;
        Check(!(bool)other.Invoke(null,new object[]{"2026.10.06.0000.0000","7.57.0.9378"})!,"unrelated memory readers not enabled");
        var source=plugin.GetType("GillionsGameSync.FateSource",true)!;
        object Source(string game="2026.10.06.0000.0000",string reference="2026.10.06.0000.0000",string revision="c9dacb095340ebe6513f13af6c38b13517ae70e4")=>
            Activator.CreateInstance(source,new object[]{"dalamud-fate-table",plugin.GetName().Version!.ToString(4),game,15,"15.0.3.7",revision,
                "7.57.0.9378","78975d8306c2766250ee4dca1925ef81f5dfd4f7","7.7.0","7.5.1",reference})!;
        var compatible=plugin.GetType("GillionsGameSync.FatePolicy",true)!.GetMethod("Compatible",flags)!;
        Check((bool)compatible.Invoke(null,new[]{Source()})!,"packaged October FATE matrix");
        Check(!(bool)compatible.Invoke(null,new[]{Source(reference:"2026.09.15.0000.0000")})!,"actual reference build cannot be spoofed");
        Check(!(bool)compatible.Invoke(null,new[]{Source(revision:"unknown")})!,"exact revision fence");
        Check(!(bool)compatible.Invoke(null,new[]{Source(game:"2026.10.07.0000.0000",reference:"2026.10.07.0000.0000")})!,"future FATE game fence");
        Console.WriteLine($"Exact October managed ABI/API/source gates PASS: {checks}; no native getter, game or HTTP invocation; physical acceptance pending.");
    }
}
