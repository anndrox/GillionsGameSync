#if GILLIONS_TEST_BUILD || GILLIONS_APPEARANCE_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace GillionsGameSync;

internal sealed record ConsentCategory(string Key, string Label, string State, string? Generation, DateTimeOffset? DecidedAt) {
    internal string DisplayState => State switch { "on" => "On", "off" => "Off", _ => "Not set" };
}

internal sealed record ConsentStatus(long CharacterId, DateTimeOffset IssuedAt, string SettingsUrl, IReadOnlyList<ConsentCategory> Categories) {
    private static readonly string[] Keys = [
        "marketContribution", "partyFinderContribution", "fateContribution", "personalHunts",
        "huntRoutingLocation", "automaticHuntMaps", "personalSubmarines", "itemLinks",
        "partyFinderLinks", "appearance",
    ];

    internal static ConsentStatus? Read(JsonElement root, string origin) {
        try {
            if (!root.TryGetProperty("consentStatus", out var status) || status.GetProperty("version").GetInt32() != 1
                || status.GetProperty("characterId").GetInt64() <= 0
                || !Timestamp(status.GetProperty("issuedAt"), out var issuedAt)) return null;
            var url = status.GetProperty("settingsUrl").GetString();
            if (url != origin + "/gillions-sync") return null;
            var rows = status.GetProperty("categories");
            if (rows.GetArrayLength() != Keys.Length) return null;

            var categories = new ConsentCategory[Keys.Length];
            for (int i = 0; i < categories.Length; i++) {
                var row = rows[i];
                var key = row.GetProperty("key").GetString();
                var label = row.GetProperty("label").GetString();
                var state = row.GetProperty("state").GetString();
                var generation = row.TryGetProperty("generation", out var id) ? id.GetString() : null;
                if (key != Keys[i] || string.IsNullOrWhiteSpace(label)
                    || state is not ("on" or "off" or "not-set") || row.GetProperty("changeOn").GetString() != "site"
                    || generation is not null && !Guid.TryParseExact(generation, "D", out _)) return null;
                DateTimeOffset? decisionTime = null;
                if (row.TryGetProperty("decidedAt", out var decidedAt) && decidedAt.ValueKind != JsonValueKind.Null) {
                    if (!Timestamp(decidedAt, out var parsed)) return null;
                    decisionTime = parsed;
                }
                categories[i] = new(key, label, state, generation, decisionTime);
            }
            return new(status.GetProperty("characterId").GetInt64(), issuedAt, url, Array.AsReadOnly(categories));
        } catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException) {
            return null;
        }
    }

    private static bool Timestamp(JsonElement value, out DateTimeOffset timestamp) =>
        DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp)
        && timestamp.Offset == TimeSpan.Zero;
}
#endif
