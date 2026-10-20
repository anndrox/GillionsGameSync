using System;
using System.Linq;
using System.Text.Json;

namespace GillionsGameSync;

// Shared private-observation validation. Native build admission lives only in
// NativeCompatibility, per collector family.
internal static class PersonalObservationCompatibility {
    internal static bool Utc(DateTime value) => value.Kind == DateTimeKind.Utc && value >= DateTime.UnixEpoch;
    internal static bool Key(string? value) => value is { Length: 64 } && value.All(c => c is >= 'a' and <= 'f' or >= '0' and <= '9');
    internal static bool Metadata(string? value) => value is { Length: >= 1 and <= 80 }
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');
    internal static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
}
