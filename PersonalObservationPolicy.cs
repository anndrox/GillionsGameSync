using System;
using System.Linq;
using System.Text.Json;

namespace GillionsGameSync;

// Exact installed catalog/SDK pair inspected for this Testing candidate. This
// admits experiments, not a claim of live correctness. New patches fail closed.
internal static class PersonalObservationCompatibility {
    internal const string GameBuild = "2026.09.15.0000.0000";
    internal const string NativeVersion = "7.56.2.9136";
    internal static bool Supports(string game, string? native) => game == GameBuild && native == NativeVersion;
    internal static bool Utc(DateTime value) => value.Kind == DateTimeKind.Utc && value >= DateTime.UnixEpoch;
    internal static bool Key(string? value) => value is { Length: 64 } && value.All(c => c is >= 'a' and <= 'f' or >= '0' and <= '9');
    internal static bool Metadata(string? value) => value is { Length: >= 1 and <= 80 }
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');
    internal static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
}

// Hunt qualification is deliberately separate from the broader personal gate:
// it cannot enable unqualified Submarine, Dashboard or travel memory readers.
// Maintained MobHunt/inventory layouts and signatures were compared at the exact
// published ClientStructs revisions; the relevant sources are byte-identical.
internal static class HuntObservationCompatibility {
    internal const string OctoberGameBuild = "2026.10.06.0000.0000";
    internal const string OctoberNativeVersion = "7.57.0.9378";
    internal static bool Supports(string game, string? native) =>
        PersonalObservationCompatibility.Supports(game, native)
        || game == OctoberGameBuild && native == OctoberNativeVersion;
}
