using System;
using System.Collections.Generic;
using System.Linq;

namespace GillionsGameSync;

// Cache definitions only after a successful, fully materialized sheet read.
// A missing source is retried on the next ordinary observation, never cached.
internal sealed class ObservedSheetRows<T> {
    private object? source;
    private T[]? rows;

    internal T[]? Get(object currentSource, Func<IEnumerable<T>?> load) {
        if (ReferenceEquals(source, currentSource)) return rows;
        var available = load();
        if (available is null) return null;
        var complete = available.ToArray();
        if (complete.Length == 0) return complete;
        source = currentSource;
        rows = complete;
        return complete;
    }
}

// SourceName is the game sheet or native state that wasn't ready (a type name such as "BannerCondition" or
// "PlayerState"), and Row the first row that was missing, when there is one. Neither carries character data.
internal sealed class CollectibleSourceUnavailableException(string sourceName = "", uint? row = null) : Exception {
    internal string SourceName { get; } = SyncSectionReport.SafeToken(sourceName);
    internal uint? Row { get; } = row;
}

// One Collections list and the read that produces it. Key is the payload property name.
internal sealed record CollectiblePart(string Key, Func<object> Read);

// Payload is null when nothing is sent. Withheld holds "<resource>:<availability>" entries: "collectibles" for the whole
// send, "collectibles.<key>:NotLoaded:<source>:<row>" for one list left out of a send (or the list that stopped it), with
// the sheet or state that wasn't ready and its first missing row (empty when there is none).
internal sealed record CollectibleCapture(Dictionary<string, object>? Payload, string[] Withheld);

internal static class CollectibleSnapshotPolicy {
    internal const string Resource = "collectibles";

    // The Site deletes its stored rows for these lists when a complete snapshot leaves them out, so one of these not
    // being ready still withholds the whole send. Every other list is kept, or shown as not synced, when left out:
    // cards, bardings, portrait conditions (frames), emotes, recipe books and folklore have explicit presence guards,
    // and the snapshot-only lists are read only when the latest snapshot carries them as an array.
    internal static readonly IReadOnlySet<string> ClearedWhenOmitted = new HashSet<string>(StringComparer.Ordinal) { "minions", "mounts", "orchestrions", "fashions" };

    internal static string PartResource(string key) => Resource + "." + key;

    // Each list is read on its own. A list whose source isn't ready (a portrait condition row, the folklore books, an
    // empty catalog) is left out and named, and the rest is sent. Character unlock results are never cached, including
    // across reconnects, and a character change or logout during the read still withholds everything.
    internal static CollectibleCapture Capture(Func<ulong> readCharacter, object identity, IReadOnlyList<CollectiblePart> parts) {
        var character = readCharacter();
        if (character == 0) return new(null, [Resource + ":" + NativeAvailability.NotLoaded]);
        var payload = new Dictionary<string, object>(StringComparer.Ordinal) { ["character"] = identity, ["complete"] = true };
        var notReady = new List<string>();
        var withheld = new List<string>();
        foreach (var part in parts) {
            try {
                payload[part.Key] = part.Read();
            } catch (CollectibleSourceUnavailableException unavailable) {
                notReady.Add(part.Key);
                withheld.Add($"{PartResource(part.Key)}:{NativeAvailability.NotLoaded}:{unavailable.SourceName}:{unavailable.Row?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? ""}");
            }
        }
        if (readCharacter() != character) return new(null, [Resource + ":" + NativeAvailability.StaleContext]);
        if (notReady.Count == parts.Count || notReady.Any(ClearedWhenOmitted.Contains)) {
            withheld.Add(Resource + ":" + NativeAvailability.NotLoaded);
            return new(null, [.. withheld]);
        }
        return new(payload, [.. withheld]);
    }

    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal) {
        ["cards"] = "Triple Triad cards", ["minions"] = "minions", ["mounts"] = "mounts", ["bardings"] = "bardings", ["emotes"] = "emotes",
        ["orchestrions"] = "orchestrion rolls", ["fashions"] = "fashion accessories", ["blueMageSpells"] = "Blue Mage spells",
        ["sightseeingLogIds"] = "sightseeing log", ["aetherCurrentIds"] = "aether currents", ["portraitBackgrounds"] = "portrait backgrounds",
        ["portraitConditions"] = "portrait conditions", ["portraitDecorations"] = "portrait decorations", ["portraitFacials"] = "portrait expressions",
        ["portraitFrames"] = "portrait frames", ["portraitPoses"] = "portrait poses", ["masterRecipeBookIds"] = "master recipe books",
        ["folkloreBookIds"] = "folklore tomes",
    };

    internal static string Label(string key) => Labels.TryGetValue(key, out var label) ? label : key;

    internal static IEnumerable<string> LabelValues => Labels.Values;
}
