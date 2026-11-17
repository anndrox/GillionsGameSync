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

internal sealed class CollectibleSourceUnavailableException : Exception;

// One Collections list and the read that produces it. Key is the payload property name.
internal sealed record CollectiblePart(string Key, Func<object> Read);

// Payload is null when nothing is sent. Withheld holds "<resource>:<availability>" entries: "collectibles" for the whole
// send, "collectibles.<key>" for one list left out of a send (or the list that stopped it).
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
        foreach (var part in parts) {
            try {
                payload[part.Key] = part.Read();
            } catch (CollectibleSourceUnavailableException) {
                notReady.Add(part.Key);
            }
        }
        if (readCharacter() != character) return new(null, [Resource + ":" + NativeAvailability.StaleContext]);
        var withheld = notReady.Select(key => PartResource(key) + ":" + NativeAvailability.NotLoaded).ToList();
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

    // The Sync now text for resources that weren't ready (resource names before ':', in order, once each). A list left
    // out of a sent Collections snapshot is named as such; a whole resource keeps the existing "kept unchanged" wording,
    // with the Collections lists that stopped it in brackets.
    internal static string NotReadyText(IReadOnlyList<string> notReady) {
        var prefix = Resource + ".";
        var parts = notReady.Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).Select(name => Label(name[prefix.Length..])).ToArray();
        var wholeCollections = notReady.Contains(Resource, StringComparer.Ordinal);
        var whole = notReady.Where(name => !name.StartsWith(prefix, StringComparison.Ordinal))
            .Select(name => name == Resource && parts.Length > 0 ? $"{Resource} (not ready: {string.Join(", ", parts)})" : name).ToArray();
        var sentences = new List<string>();
        if (whole.Length > 0) sentences.Add($"Not ready yet, kept unchanged on Gillions: {string.Join(", ", whole)}.");
        if (parts.Length > 0 && !wholeCollections) sentences.Add($"Collections were sent without {string.Join(", ", parts)} (not ready yet).");
        return string.Join(" ", sentences);
    }
}
