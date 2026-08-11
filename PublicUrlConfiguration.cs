using System;

namespace GillionsGameSync;

internal static class PublicUrlConfiguration {
    public const string LegacyPublicBaseUrl = "https://gillions.lanlab.one";

    public static bool TryUseCompiledDefault(string? currentValue, string? compiledDefault, out string serverUrl) {
        var target = (compiledDefault ?? "").Trim().TrimEnd('/');
        var current = (currentValue ?? "").Trim().TrimEnd('/');
        serverUrl = current;
        if (string.IsNullOrWhiteSpace(target)) return false;
        if (!string.IsNullOrWhiteSpace(current)
            && !string.Equals(current, LegacyPublicBaseUrl, StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(current, target, StringComparison.OrdinalIgnoreCase)) return false;
        serverUrl = target;
        return true;
    }
}
