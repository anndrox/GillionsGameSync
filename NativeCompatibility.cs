using System.Linq;

namespace GillionsGameSync;

// One exact runtime: the "ffxiv" game data version Dalamud reports, the Dalamud
// release, and the FFXIVClientStructs/Lumina assemblies that release ships.
internal sealed record NativeBuild(string GameBuild, string DalamudVersion, string DalamudRevision,
    string ClientStructsVersion, string ClientStructsRevision, string LuminaVersion, string ExcelVersion);

// Each collector family reads different native layouts and is qualified on its
// own. Each entry requires a pinned source and shipped layout review, including
// comparison to the previous qualification where one exists (evidence and the
// patch-day procedure: docs/native-compatibility.md). Anything unlisted
// fails closed before any native read; qualifying one family never admits
// another. Site authority and player consent remain separate, later gates.
internal enum NativeFamily { Hunt, Submarine, Travel, Fate, Dashboard, Beastmaster, Appearance, Desynthesis }

internal static partial class NativeCompatibility {
    internal static readonly NativeBuild September = new("2026.09.15.0000.0000", "15.0.3.6",
        "b666d821a47306fb447c60155b5d99377f91a5ee", "7.56.2.9136", "313161e448e335adddd928f8a0212b2c328b5659", "7.7.0", "7.5.1");
    internal static readonly NativeBuild October = new("2026.10.06.0000.0000", "15.0.3.7",
        "c9dacb095340ebe6513f13af6c38b13517ae70e4", "7.57.0.9378", "78975d8306c2766250ee4dca1925ef81f5dfd4f7", "7.7.0", "7.5.1");
    // 7.57 hotfix1 with the Dalamud release promoted on 2026-10-20.
    internal static readonly NativeBuild OctoberHotfix = new("2026.10.16.0000.0000", "15.0.3.9",
        "30509c3b4fd00edd9a3d65b7bbf16433d84be03b", "7.57.0.9414", "dd57986b8287e7c6ed9c27198f8c76cb04a81dfe", "7.7.0", "7.5.1");

    // Release-channel archive promoted on 2026-11-10.
    internal static readonly NativeBuild November = new("2026.11.03.0000.0000", "15.0.4.0",
        "12e318c1936e449e5883faa61214eb2ced478349", "7.57.0.9480", "80cb4a7b7e11a6702e2eb8839afd301a918c638e", "7.7.0", "7.5.1");

    internal static NativeBuild[] Qualified(NativeFamily family) => family switch {
        NativeFamily.Hunt or NativeFamily.Fate or NativeFamily.Appearance => [September, October, OctoberHotfix, November],
        NativeFamily.Submarine or NativeFamily.Travel => [September, OctoberHotfix, November],
        NativeFamily.Beastmaster => [September, November],
        NativeFamily.Dashboard => [September],
        NativeFamily.Desynthesis => [November],
        _ => [],
    };

    internal static bool SupportsExact(NativeFamily family, NativeBuild runtime) =>
        Qualified(family).Contains(runtime);

    // Snapshot policies only: native readers must use SupportsRuntime or an
    // exact source identity check before touching game state.
    internal static bool Supports(NativeFamily family, string? game, string? clientStructs) =>
        Qualified(family).Any(build => build.GameBuild == game && build.ClientStructsVersion == clientStructs);
}
