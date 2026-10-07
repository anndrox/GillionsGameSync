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
