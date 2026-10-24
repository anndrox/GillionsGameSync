using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using Dalamud.Plugin.Services;

// Actual installed managed ABI/API inspection. Never invoke native getters,
// constructors, hooks or HTTP. This cannot establish physical game correctness.
internal static class NativeCompatibilityPackagedTests {
    // The exact ClientStructs members each family reads. A fingerprint covers
    // declared struct sizes, field offsets/types/attributes, compiled enum
    // values, property and method signatures, native signature/address
    // attributes and agent identities. It does not hash accessor bodies (IL
    // metadata tokens differ between unrelated builds), so qualification also
    // requires the byte-identical source comparison in docs/native-compatibility.md.
    private static readonly (string Family, (string Type, string[] Members)[] Uses)[] Families = [
        ("Hunt", [
            ("FFXIV.Client.Game.UI.MobHunt", ["Instance", "ObtainedFlags", "CurrentKills", "GetKillCount", "GetObtainedHuntOrderRowId", "GetAvailableHuntOrderRowId"]),
            ("FFXIV.Client.Game.InventoryManager", ["Instance", "GetInventoryContainer"]),
            ("FFXIV.Client.Game.InventoryContainer", ["Items", "Size", "Type", "IsLoaded", "GetSize"]),
            ("FFXIV.Client.Game.InventoryItem", ["VirtualTable", "Container", "Slot", "IsSymbolic", "ItemId", "Quantity", "IsEmpty", "GetBaseItemId", "GetQuantity"]),
            ("FFXIV.Client.Game.InventoryType", ["KeyItems"]),
            ("FFXIV.Client.Game.UI.PlayerState", ["Instance", "IsLoaded", "ContentId"]),
        ]),
        ("Submarine", [
            ("FFXIV.Client.Game.HousingManager", ["Instance", "WorkshopTerritory", "CurrentTerritory", "IsSubmarineExplorationUnlocked", "IsSubmarineExplorationExplored"]),
            ("FFXIV.Client.Game.WorkshopTerritory", ["Submersible", "HouseId", "IsLoaded"]),
            ("FFXIV.Client.Game.HousingWorkshopSubmersibleData", ["Data", "DataPointers"]),
            ("FFXIV.Client.Game.HousingWorkshopSubmersibleSubData", ["Parent", "RankId", "RegisterTime", "ReturnTime", "CurrentExp", "NextLevelExp",
                "HullId", "SternId", "BowId", "BridgeId", "Name", "CurrentExplorationPoints", "SurveillanceBase", "RetrievalBase", "SpeedBase", "RangeBase",
                "FavorBase", "SurveillanceBonus", "RetrievalBonus", "SpeedBonus", "RangeBonus", "FavorBonus", "LogSpeed", "GatheredData"]),
            ("FFXIV.Client.Game.HousingWorkshopSubmarineGathered", ["Point", "UnlockedPoint", "ItemIdPrimary", "ItemCountPrimary", "ItemHQPrimary",
                "ItemIdAdditional", "ItemCountAdditional", "ItemHQAdditional", "ExpGained", "FirstExploration", "AdditionalSubmarineUnlocked", "DoubleDip"]),
            ("FFXIV.Client.UI.Agent.AgentSubmersibleExploration", ["Instance", "SelectedPoints", "SelectedPointsCount"]),
            ("FFXIV.Client.UI.Agent.AgentSubmersibleExplorationResult", ["Instance", "Data"]),
            ("FFXIV.Client.UI.Agent.ExplorationResultData", ["ItemReturnListCount", "ItemReturn"]),
            ("FFXIV.Client.UI.Agent.ExplorationResultDataItemReturn", ["ItemId", "Quantity"]),
            ("FFXIV.Client.UI.Agent.AgentInterface", ["IsAgentActive", "AddonId"]),
            ("FFXIV.Client.Game.UI.PlayerState", ["Instance", "IsLoaded", "ContentId"]),
        ]),
        ("Travel", [
            ("FFXIV.Client.UI.Agent.AgentTeleport", ["Instance", "AetheryteList", "AetheryteCount"]),
            ("FFXIV.Client.Game.UI.Telepo", ["Instance", "TeleportList"]),
            ("FFXIV.Client.Game.UI.TeleportInfo", ["AetheryteId", "TerritoryId", "GilCost", "IsFavourite", "IsFreeAetheryte", "IsSharedHouse", "IsApartment", "SubIndex", "Ward", "Plot"]),
            ("FFXIV.Client.Game.UI.UIState", ["Instance", "UnlockedAetherytes", "IsAetheryteUnlocked"]),
            ("FFXIV.Component.GUI.AtkUnitBase", ["IsVisible", "Id"]),
            ("FFXIV.Client.UI.Agent.AgentInterface", ["IsAgentActive", "AddonId"]),
        ]),
        ("Fate", [
            ("FFXIV.Client.Game.Fate.FateContext", ["FateId", "StartTimeEpoch", "Duration", "State", "Progress", "IsBonus", "Level", "MaxLevel", "Location", "Radius"]),
        ]),
    ];
    // Measured with --native-layout-report against the official Dalamud
    // 15.0.3.6/15.0.3.7/15.0.3.9 release archives (15.0.3.8 also matched but is
    // not qualified). Equality across a family's qualified versions is the
    // reference evidence for its admission; CI rechecks every recorded archive
    // with --native-layout-check.
    private const string HuntLayout = "2312d897d65b4177", SubmarineLayout = "8c4e37e328c571a9",
        TravelLayout = "22c25911f7cc88bf", FateLayout = "6923c1e09b662322";
    private static readonly Dictionary<(string Family, string ClientStructs), string> Expected = new() {
        [("Hunt", "7.56.2.9136")] = HuntLayout, [("Hunt", "7.57.0.9378")] = HuntLayout, [("Hunt", "7.57.0.9414")] = HuntLayout,
        [("Submarine", "7.56.2.9136")] = SubmarineLayout, [("Submarine", "7.57.0.9414")] = SubmarineLayout,
        [("Travel", "7.56.2.9136")] = TravelLayout, [("Travel", "7.57.0.9414")] = TravelLayout,
        [("Fate", "7.56.2.9136")] = FateLayout, [("Fate", "7.57.0.9378")] = FateLayout, [("Fate", "7.57.0.9414")] = FateLayout,
    };

    internal static void Run(Assembly plugin,string libraries) {
        var checks=0;
        void Check(bool value,string why){checks++;if(!value)throw new Exception("Native compatibility: "+why);}
        var native=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(libraries,"FFXIVClientStructs.dll"));
        var installed=native.GetName().Version!.ToString();
        var dalamud=typeof(IFramework).Assembly.GetName().Version!.ToString();
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var table=plugin.GetType("GillionsGameSync.NativeCompatibility",true)!;
        var familyType=plugin.GetType("GillionsGameSync.NativeFamily",true)!;
        var qualified=table.GetMethod("Qualified",flags)!;
        var supports=table.GetMethod("Supports",flags)!;
        object Family(string name)=>Enum.Parse(familyType,name);
        string P(object build,string name)=>(string)build.GetType().GetProperty(name)!.GetValue(build)!;
        object[] Builds(string family)=>((Array)qualified.Invoke(null,[Family(family)])!).Cast<object>().ToArray();
        bool Supports(string family,string game,string sdk)=>(bool)supports.Invoke(null,[Family(family),game,sdk])!;
        // The pinned runtime must be a reviewed, exactly matching table entry.
        var runtime=Enum.GetNames(familyType).SelectMany(Builds).FirstOrDefault(b=>P(b,"ClientStructsVersion")==installed);
        Check(runtime is not null,"uninspected ClientStructs "+installed+" requires qualification (docs/native-compatibility.md)");
        Check(P(runtime!,"DalamudVersion")==dalamud,"table Dalamud "+P(runtime!,"DalamudVersion")+" differs from inspected "+dalamud);
        Check(Revision(native)==P(runtime!,"ClientStructsRevision"),"inspected ClientStructs revision differs from the table");
        Check(Revision(typeof(IFramework).Assembly)==P(runtime!,"DalamudRevision"),"inspected Dalamud revision differs from the table");
        var fingerprints=Fingerprints(native);
        foreach(var (family,_) in Families) {
            if(!Builds(family).Any(b=>P(b,"ClientStructsVersion")==installed)) continue;
            Check(Expected.TryGetValue((family,installed),out var expected),family+" has no recorded layout for "+installed);
            Check(fingerprints[family].Hash==expected,family+" layout "+fingerprints[family].Hash+" differs from qualified "+expected+":\n"+string.Join("\n",fingerprints[family].Lines));
            Check(!fingerprints[family].Lines.Any(l=>l.Contains("MISSING",StringComparison.Ordinal)),family+" member missing");
        }
        Check(!Builds("Dashboard").Any(b=>P(b,"ClientStructsVersion")!="7.56.2.9136"),"Dashboard readers were never requalified after September");
        Check(!Builds("Beastmaster").Any(b=>P(b,"ClientStructsVersion")!="7.56.2.9136"),"Beastmaster readers were never requalified after September");

        // Explicit, human-readable offsets kept from the October qualification.
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

        // Exact fences: each family follows only its own list.
        var game=P(runtime!,"GameBuild");
        var next=installed[..(installed.LastIndexOf('.')+1)]+(int.Parse(installed[(installed.LastIndexOf('.')+1)..])+1);
        foreach(var family in Enum.GetNames(familyType)) {
            var listed=Builds(family).Any(b=>P(b,"GameBuild")==game&&P(b,"ClientStructsVersion")==installed);
            Check(Supports(family,game,installed)==listed,"packaged "+family+" gate differs from its own qualification list");
            Check(!Supports(family,game,next),"packaged future SDK fence: "+family);
            Check(!Supports(family,"2099.01.01.0000.0000",installed),"packaged future game fence: "+family);
        }
        var source=plugin.GetType("GillionsGameSync.FateSource",true)!;
        object Source(string? g=null,string? reference=null,string? revision=null)=>
            Activator.CreateInstance(source,new object[]{"dalamud-fate-table",plugin.GetName().Version!.ToString(4),g??game,15,dalamud,
                revision??P(runtime!,"DalamudRevision"),installed,P(runtime!,"ClientStructsRevision"),"7.7.0","7.5.1",reference??game})!;
        var compatible=plugin.GetType("GillionsGameSync.FatePolicy",true)!.GetMethod("Compatible",flags)!;
        Check((bool)compatible.Invoke(null,new[]{Source()})! == Builds("Fate").Any(b=>P(b,"ClientStructsVersion")==installed),"packaged FATE matrix for the pinned runtime");
        Check(!(bool)compatible.Invoke(null,new[]{Source(reference:game=="2026.09.15.0000.0000"?"2026.10.06.0000.0000":"2026.09.15.0000.0000")})!,"actual reference build cannot be spoofed");
        Check(!(bool)compatible.Invoke(null,new[]{Source(revision:"unknown")})!,"exact revision fence");
        Check(!(bool)compatible.Invoke(null,new[]{Source(g:"2099.01.01.0000.0000",reference:"2099.01.01.0000.0000")})!,"future FATE game fence");
        Console.WriteLine($"Native compatibility for game {game} / Dalamud {dalamud} / ClientStructs {installed} PASS: {checks}; managed metadata only, no native getter, game or HTTP invocation; physical acceptance pending.");
    }

    // Evidence modes: print each family's fingerprint for any Dalamud directory,
    // or (check) fail unless every recorded family matches for that runtime.
    internal static void Report(string libraries, bool check = false) {
        // An isolated context, so the inspected directory never resolves to the
        // ClientStructs copy this test was compiled against.
        var context=new AssemblyLoadContext("native-layout-report");
        context.Resolving += (_, name) => {
            var path = Path.Combine(libraries, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
        var native=context.LoadFromAssemblyPath(Path.Combine(libraries,"FFXIVClientStructs.dll"));
        Console.WriteLine($"ClientStructs {native.GetName().Version} {Revision(native)}");
        var version=native.GetName().Version!.ToString();
        var compared=0;
        foreach(var (family,(hash,lines)) in Fingerprints(native)) {
            if(check) {
                if(!Expected.TryGetValue((family,version),out var expected)) continue;
                compared++;
                if(hash!=expected) throw new Exception($"Native layout: {family} {hash} differs from recorded {expected} for ClientStructs {version}:\n"+string.Join("\n",lines));
                Console.WriteLine($"{family} {hash} matches recorded layout");
                continue;
            }
            Console.WriteLine($"{family} {hash}");
            foreach(var line in lines) Console.WriteLine("  "+line);
        }
        if(check && compared==0) throw new Exception("Native layout: no recorded layout for ClientStructs "+version);
    }

    private static string Revision(Assembly assembly) {
        var version=assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        return version.Contains('+') ? version[(version.LastIndexOf('+')+1)..] : "unavailable";
    }

    private static Dictionary<string,(string Hash,string[] Lines)> Fingerprints(Assembly native) {
        var result=new Dictionary<string,(string,string[])>();
        foreach(var (family,uses) in Families) {
            var lines=new List<string>();
            foreach(var (typeName,members) in uses) {
                var type=native.GetType("FFXIVClientStructs."+typeName);
                if(type is null) { lines.Add(typeName+" MISSING type"); continue; }
                lines.Add($"{typeName} size {type.StructLayoutAttribute?.Size}");
                // Agent/addon identities select which native object Instance() returns.
                foreach(var attribute in type.CustomAttributes.Where(a=>a.AttributeType.Name is "AgentAttribute" or "AddonAttribute" or "InheritsAttribute`1" or "InheritsAttribute")
                    .Select(Attribute).Order(StringComparer.Ordinal))
                    lines.Add($"{typeName} attribute {attribute}");
                foreach(var member in members) lines.AddRange(Describe(type,member).Select(l=>typeName+" "+l));
            }
            lines.Sort(StringComparer.Ordinal);
            var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",lines))))[..16].ToLowerInvariant();
            result[family]=(hash,lines.ToArray());
        }
        return result;
    }

    private static IEnumerable<string> Describe(Type type,string member) {
        const BindingFlags all=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;
        var found=false;
        // Generated accessors and inherited members (e.g. AgentInterface) are
        // resolved on the declaring chain, never by unrelated name matches.
        for(var current=type;current is not null && current!=typeof(object) && current!=typeof(ValueType);current=current.BaseType) {
            if(current.GetField(member,all) is { } field) { found=true; yield return Field(field); }
            if(current.GetProperty(member,all) is { } property) {
                found=true; yield return $"P {member} {Format(property.PropertyType)}";
                var backing="_"+char.ToLowerInvariant(member[0])+member[1..];
                if(current.GetField(backing,all) is { } storage) yield return Field(storage);
            }
            foreach(var method in current.GetMethods(all).Where(m=>m.Name==member).OrderBy(m=>m.ToString(),StringComparer.Ordinal)) {
                found=true;
                var signature=string.Join(",",method.GetParameters().Select(p=>Format(p.ParameterType)));
                var attributes=string.Join(";",method.CustomAttributes.Where(a=>a.AttributeType.Name is "MemberFunctionAttribute" or "VirtualFunctionAttribute" or "StaticAddressAttribute")
                    .Select(Attribute).Order(StringComparer.Ordinal));
                yield return $"M {(method.IsStatic?"static ":"")}{member}({signature}) {Format(method.ReturnType)} {attributes}";
            }
            if(found) yield break;
        }
        yield return member+" MISSING";
    }

    private static string Field(FieldInfo field) =>
        $"F {field.Name} @{field.GetCustomAttribute<FieldOffsetAttribute>()?.Value.ToString() ?? "-"} {Format(field.FieldType)}"
        + (field.IsLiteral ? " = "+Convert.ToString(field.GetRawConstantValue(),System.Globalization.CultureInfo.InvariantCulture) : "")
        + string.Concat(field.CustomAttributes.Where(a=>a.AttributeType.Name is "FixedBufferAttribute" or "FixedSizeArrayAttribute" or "MarshalAsAttribute")
            .Select(a=>" "+Attribute(a)).Order(StringComparer.Ordinal));

    // Expands array and named arguments, so different contents never print alike.
    private static string Attribute(CustomAttributeData attribute) {
        static string Value(CustomAttributeTypedArgument argument) => argument.Value is IEnumerable<CustomAttributeTypedArgument> items
            ? "["+string.Join(",",items.Select(Value))+"]"
            : argument.Value is Type type ? Format(type) : Convert.ToString(argument.Value,System.Globalization.CultureInfo.InvariantCulture) ?? "null";
        var generic=attribute.AttributeType.IsGenericType ? "<"+string.Join(",",attribute.AttributeType.GetGenericArguments().Select(Format))+">" : "";
        return attribute.AttributeType.Name+generic+"("+string.Join(",",attribute.ConstructorArguments.Select(Value)
            .Concat(attribute.NamedArguments.Select(n=>n.MemberName+"="+Value(n.TypedValue))))+")";
    }

    // Stable across builds: never includes assembly-qualified version strings.
    private static string Format(Type type) {
        if(type.IsPointer) return Format(type.GetElementType()!)+"*";
        if(type.IsByRef) return Format(type.GetElementType()!)+"&";
        if(type.IsArray) return Format(type.GetElementType()!)+"[]";
        if(type.IsGenericType) {
            var definition=type.GetGenericTypeDefinition();
            return (definition.FullName ?? definition.Name)+"["+string.Join(",",type.GetGenericArguments().Select(Format))+"]";
        }
        return type.FullName ?? type.Name;
    }
}
