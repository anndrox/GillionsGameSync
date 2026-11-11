using System;
using System.Diagnostics;
using System.Reflection;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace GillionsGameSync;

internal static partial class NativeCompatibility {
    // The assembly tuple is constant for the plugin load.
    private static readonly NativeBuild LoadedAssemblies = new("",
        VersionOf(typeof(IFramework).Assembly), RevisionOf(typeof(IFramework).Assembly),
        VersionOf(typeof(InventoryManager).Assembly), RevisionOf(typeof(InventoryManager).Assembly),
        LibraryVersion(typeof(Lumina.GameData).Assembly), LibraryVersion(typeof(Lumina.Excel.Sheets.Fate).Assembly));

    private sealed record RuntimeCapabilities(string Game, int Families);
    private static RuntimeCapabilities? runtimeCapabilities;

    internal static bool SupportsRuntime(NativeFamily family, string? game) {
        if (game is null || !Enum.IsDefined(family)) return false;
        var cached = System.Threading.Volatile.Read(ref runtimeCapabilities);
        if (cached?.Game != game) {
            var runtime = LoadedAssemblies with { GameBuild = game };
            var families = 0;
            foreach (var candidate in Enum.GetValues<NativeFamily>())
                if (SupportsExact(candidate, runtime)) families |= 1 << (int)candidate;
            cached = new(game, families);
            System.Threading.Volatile.Write(ref runtimeCapabilities, cached);
        }
        return (cached.Families & (1 << (int)family)) != 0;
    }

    private static string VersionOf(Assembly assembly) => assembly.GetName().Version?.ToString() ?? "";
    private static string RevisionOf(Assembly assembly) {
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var separator = version?.LastIndexOf('+') ?? -1;
        return separator >= 0 ? version![(separator + 1)..] : "";
    }
    private static string LibraryVersion(Assembly assembly) {
        if (string.IsNullOrEmpty(assembly.Location)) return "";
        var info = FileVersionInfo.GetVersionInfo(assembly.Location);
        return FormattableString.Invariant($"{info.FileMajorPart}.{info.FileMinorPart}.{info.FileBuildPart}");
    }
}
