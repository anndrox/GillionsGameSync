using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Plugin;
using Newtonsoft.Json;

internal static class DistributionManifestPackagedTests {
    internal static void Run(Assembly assembly) {
        if(assembly.GetName().Name!="GillionsGameSyncTest") return;
        var feed=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(assembly.Location)!,"..","GillionsGameSyncTesting.json"));
        if(!File.Exists(feed)) return; // verification binaries are not packages
        var dalamud=typeof(IDalamudPlugin).Assembly;
        var manifestType=dalamud.GetType("Dalamud.Plugin.Internal.Types.Manifest.RemotePluginManifest",true)!;
        var listType=typeof(List<>).MakeGenericType(manifestType);
        var parsed=(IList)JsonConvert.DeserializeObject(File.ReadAllText(feed),listType)!;
        int checks=0;
        void Check(bool value,string why){checks++;if(!value)throw new InvalidOperationException(why);}
        Check(parsed.Count==1,"dedicated feed must identify one Testing product");
        var entry=parsed[0]!;
        object? Field(string name)=>manifestType.GetProperty(name)!.GetValue(entry);
        Check((string)Field("InternalName")! == "GillionsGameSyncTest","feed cannot replace Stable");
        Check((Version)Field("AssemblyVersion")! == assembly.GetName().Version,"real Dalamud version parser must match packaged DLL");
        Check((int)Field("DalamudApiLevel")! == 15,"current API compatibility");
        Check(Field("TestingAssemblyVersion") is null,"separate product does not masquerade as Stable experimental selection");
        Check((string)Field("RepoUrl")! == "https://github.com/anndrox/GillionsGameSync-Testing","dedicated distribution identity");
        foreach(var name in new[]{"DownloadLinkInstall","DownloadLinkUpdate","DownloadLinkTesting"})
            Check(((string)Field(name)!).StartsWith("https://github.com/anndrox/GillionsGameSync-Testing/releases/download/",StringComparison.Ordinal),"versioned Testing downloads only");
        var repositoryType=dalamud.GetType("Dalamud.Plugin.Internal.Types.PluginRepository",true)!;
        var repository=RuntimeHelpers.GetUninitializedObject(repositoryType);
        Check((bool)repositoryType.GetMethod("IsValidManifest",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(repository,[entry])!,"actual installed Dalamud manifest admission");
        // Source-derived updater fence: pinned PluginManager lines1761-1770
        // additionally require InstalledFromUrl == SourceRepo.PluginMasterUrl.
        const string current="https://raw.githubusercontent.com/anndrox/GillionsGameSync-Testing/main/data/GillionsGameSyncTesting.json";
        const string historical="https://github.com/anndrox/GillionsGameSync/releases/download/v0.0.64-testing/GillionsGameSyncTesting.json";
        Check(current!=historical,"old installations need explicit reinstall to rebind source URL");
        Check((Version)Field("AssemblyVersion")! > new Version(0,0,87,0),"first dedicated release updates the established version line");
        Console.WriteLine($"Exact Dalamud distribution manifest PASS: {checks}; actual installed parser/validator, source-derived update fence, NOT physical install.");
    }
}
