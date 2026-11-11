using System;
using System.Diagnostics;
using System.Reflection;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace GillionsGameSync;

internal static partial class NativeCompatibility {
    // Assemblies are constant for this plugin load. The game build is checked
    // on each call; no file inspection or native access happens on the hot path.
    private static readonly NativeBuild LoadedAssemblies = new("",
        VersionOf(typeof(IFramework).Assembly), RevisionOf(typeof(IFramework).Assembly),
        VersionOf(typeof(InventoryManager).Assembly), RevisionOf(typeof(InventoryManager).Assembly),
        LibraryVersion(typeof(Lumina.GameData).Assembly), LibraryVersion(typeof(Lumina.Excel.Sheets.Fate).Assembly));

    internal static bool SupportsRuntime(NativeFamily family, string? game) =>
        game is not null && SupportsExact(family, LoadedAssemblies with { GameBuild = game });

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
