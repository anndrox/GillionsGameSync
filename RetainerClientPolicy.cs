using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GillionsGameSync;

public sealed record RetainerClientProfile(
    string ProductName,
    string Channel,
    bool RequiresExplicitServerProductAcceptance);

public static class RetainerClientPolicy {
    public const int ContractVersion = 1;
    public const string ResourceType = "retainer_ventures";

    public static readonly RetainerClientProfile Stable = new(
        "GillionsGameSync",
        "stable",
        true);

    public static readonly RetainerClientProfile Testing = new(
        "GillionsGameSyncTest",
        "testing",
        false);

    public static string[] BuildSyncScopes(IEnumerable<string> ordinaryScopes, bool serverAccepted) {
        var scopes = ordinaryScopes.ToArray();
        return serverAccepted ? scopes.Append(ResourceType).ToArray() : scopes;
    }

}

// Every roster read refreshes observation times, so the whole payload changed
// every 30 seconds without any retainer change. Upload on a content change or
// pending venture results; otherwise refresh freshness every five minutes.
public static class RetainerUploadPolicy {
    public static readonly TimeSpan FreshnessInterval = TimeSpan.FromMinutes(5);
    public const string SemanticHashKey = RetainerClientPolicy.ResourceType + ":semantic";
    // Evidence times only. Some evidence restamps "lastChangedAtUtc" on every read
    // (loaded inventory containers, an unreadable venture), and a real change always
    // also changes a value, status or count, so all three times are excluded; the
    // five-minute refresh carries current times. Arrays are compared order-free:
    // every element carries its own identity (slot, container, context, event id).
    private static readonly HashSet<string> TimeOnly = new(StringComparer.Ordinal) { "lastObservedAtUtc", "observedAtUtc", "lastChangedAtUtc" };

    public static string SemanticHash(JsonElement payload) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(payload))));

    public static bool ShouldUpload(bool force, int pendingResults, string semanticHash, string? lastSemanticHash,
        DateTime lastUploadUtc, DateTime nowUtc) =>
        force || pendingResults > 0 || semanticHash != lastSemanticHash || nowUtc - lastUploadUtc >= FreshnessInterval;

    private static string Canonical(JsonElement element) => element.ValueKind switch {
        JsonValueKind.Object => "{" + string.Join(",", element.EnumerateObject().Where(property => !TimeOnly.Contains(property.Name))
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => JsonSerializer.Serialize(property.Name) + ":" + Canonical(property.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", element.EnumerateArray().Select(Canonical).OrderBy(value => value, StringComparer.Ordinal)) + "]",
        JsonValueKind.String => JsonSerializer.Serialize(element.GetString()),
        _ => element.GetRawText(),
    };
}
