using System;

namespace GillionsGameSync;

internal static class PublicUrlConfiguration {
    public const string LegacyPublicBaseUrl = "https://gillions.lanlab.one";
    internal const string StableDestination = "https://gillions.app";

    // Only a new installed Stable version applies this migration.
    internal static string DestinationLabel(string? value) {
        if (SyncOrigin.TryNormalize(value, out var origin)) return origin;
        if (string.IsNullOrWhiteSpace(value)) return "an empty address";
        // Legacy values can contain an HTTP scheme or path. Show the host,
        // never URL user information, paths, queries or fragments.
        return Uri.TryCreate(value, UriKind.Absolute, out var address) && address.Host.Length > 0
            ? address.IdnHost : "an invalid address";
    }

    internal static string PairingFailure(Exception error, string? attemptedOrigin) {
        if (!SyncOrigin.TryNormalize(attemptedOrigin, out var origin))
            return "Enter a pairing code and a supported HTTPS pairing destination.";
        var host = new Uri(origin).IdnHost;
        return error switch {
            GillionsHttpException { Status: 401 } =>
                $"The pairing code is invalid, expired, or already used on {host}. Create a fresh one-time code there and try again.",
            GillionsHttpException { Code: "ACCOUNT_DISABLED" } => $"Your account on {host} is disabled.",
            GillionsHttpException { Code: "TRIAL_EXPIRED" } => $"Your trial on {host} has ended.",
            _ => $"Pairing did not complete on {host}. Check the connection, then create a fresh one-time code there and try again.",
        };
    }

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
