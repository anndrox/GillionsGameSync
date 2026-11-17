using System.Text.Json;
using GillionsGameSync;

internal static class CollectibleReadinessTests {
    // The collector's Collections lists, in its payload order.
    private static readonly string[] Keys = ["cards", "minions", "mounts", "bardings", "emotes", "orchestrions", "fashions", "blueMageSpells",
        "sightseeingLogIds", "aetherCurrentIds", "portraitBackgrounds", "portraitConditions", "portraitDecorations", "portraitFacials",
        "portraitFrames", "portraitPoses", "masterRecipeBookIds", "folkloreBookIds"];

    internal static void Run() {
        var checks = 0;
        void Check(bool value, string why) { checks++; if (!value) throw new Exception("Collectible readiness: " + why); }
        var source = new object();
        var cache = new ObservedSheetRows<long>();
        var calls = 0;
        long[]? Read(IEnumerable<long>? rows) => cache.Get(source, () => { calls++; return rows; });
        Check(Read(null) is null && Read(null) is null && calls == 2, "unavailable source must not be cached as empty");
        Check(Read([])!.Length == 0 && Read(null) is null, "empty definitions cannot poison later readiness");
        // Native BannerCondition identities from Site93's pinned game/reference audit.
        // These are public catalog examples, never real-character ownership.
        long[] conditions = [344, 429, 433, 696, 938];
        Check(Read(conditions)!.SequenceEqual(conditions), "later ready sheet repairs initial unavailability");
        var priorCalls = calls;
        Check(Read(null)!.SequenceEqual(conditions) && calls == priorCalls, "successful static definitions are reused");
        source = new object();
        Check(Read(null) is null, "new data source cannot borrow old definitions");
        try { Read(Interrupted()); throw new Exception("interrupted enumeration accepted"); }
        catch (InvalidOperationException) { checks++; }
        Check(Read(conditions)!.SequenceEqual(conditions), "interrupted materialization must not cache partial rows");

        ulong character = 1;
        var payloadReads = 0;
        var owned = conditions.ToHashSet();
        var identity = new { name = "Fixture", world = "Fixture" };
        CollectiblePart[] Parts() => [
            new("portraitConditions", () => { payloadReads++; return conditions.Where(owned.Contains).ToArray(); }),
            new("portraitFrames", () => new long[] { 96 }), new("mounts", () => new long[] { 7 }),
        ];
        CollectibleCapture Capture() => CollectibleSnapshotPolicy.Capture(() => character, identity, Parts());
        JsonElement Json(CollectibleCapture capture) => JsonSerializer.SerializeToElement(capture.Payload);
        character = 0;
        var loggedOut = Capture();
        Check(loggedOut.Payload is null && payloadReads == 0, "startup before loaded character must not query unlocks");
        Check(loggedOut.Withheld.SequenceEqual(["collectibles:NotLoaded"]), "a capture before the character loads is named as not ready");
        character = 1;
        var firstCapture = Capture();
        var first = Json(firstCapture);
        Check(firstCapture.Withheld.Length == 0, "a fully ready capture withholds nothing");
        Check(first.GetProperty("complete").GetBoolean(), "successful full capture retains existing completeness");
        Check(first.GetProperty("character").GetProperty("name").GetString() == "Fixture", "the capture carries the character identity");
        Check(first.GetProperty("portraitConditions").EnumerateArray().Select(x => x.GetInt64()).SequenceEqual(conditions), "owned conditions remain exact");
        Check(first.GetProperty("portraitFrames")[0].GetInt64() == 96 && !conditions.Contains(96), "portrait elements stay in their own namespace");
        Check(first.GetProperty("mounts")[0].GetInt64() == 7, "unrelated valid category is unchanged");
        var before = JsonSerializer.Serialize(first);
        try {
            CollectibleSnapshotPolicy.Capture(() => character, identity, [new("portraitFrames", () => throw new InvalidOperationException("fixture interrupted native read"))]);
            throw new Exception("unexpected native failure swallowed");
        } catch (InvalidOperationException) { checks++; }
        Check(JsonSerializer.Serialize(first) == before, "a failed follow-up cannot mutate copied accepted evidence");
        Check(Capture().Payload is not null, "next ordinary successful observation remains possible");
        owned.Clear();
        var empty = Json(Capture());
        Check(empty.GetProperty("complete").GetBoolean() && empty.GetProperty("portraitConditions").GetArrayLength() == 0,
            "fully read catalog with zero owned conditions is authoritative complete-empty");
        Check(JsonSerializer.Serialize(empty) == JsonSerializer.Serialize(Capture().Payload), "same observation retains deterministic payload for existing hash/retry handling");
        var read = 0;
        var switched = CollectibleSnapshotPolicy.Capture(() => ++read == 1 ? 1UL : 2UL, identity, Parts());
        Check(switched.Payload is null && switched.Withheld.SequenceEqual(["collectibles:StaleContext"]), "character switch during capture cannot publish foreign evidence");
        read = 0;
        Check(CollectibleSnapshotPolicy.Capture(() => ++read == 1 ? 1UL : 0UL, identity, Parts()).Payload is null, "logout during capture cannot publish evidence");
        read = 0;
        Check(CollectibleSnapshotPolicy.Capture(() => ++read == 1 ? 1UL : 2UL, identity, [new("portraitConditions", () => throw new CollectibleSourceUnavailableException()), new("cards", () => new long[] { 1 })]).Withheld
            .SequenceEqual(["collectibles:StaleContext"]), "a character switch outranks a list that wasn't ready");
        character = 2;
        owned.Add(938);
        Check(Json(Capture()).GetProperty("portraitConditions").EnumerateArray().Select(x => x.GetInt64()).SequenceEqual([938L]),
            "new character gets current unlocks, never prior character ownership");
        character = 0;
        Check(Capture().Payload is null, "logout withholds resource");
        character = 2;
        owned.Add(344);
        Check(Json(Capture()).GetProperty("portraitConditions").EnumerateArray().Select(x => x.GetInt64()).SequenceEqual([344L, 938L]),
            "reconnect and multiple observations reread current ownership");

        // The collector's full list set: all ready, then each source failing on its own.
        character = 1;
        CollectibleCapture Full(string? failing) => CollectibleSnapshotPolicy.Capture(() => character, identity,
            Keys.Select((key, index) => new CollectiblePart(key, () => key == failing ? throw new CollectibleSourceUnavailableException() : new long[] { index + 1 })).ToArray());
        var allReady = Full(null);
        var allJson = Json(allReady);
        Check(allReady.Withheld.Length == 0, "all ready: nothing withheld");
        Check(allJson.EnumerateObject().Select(p => p.Name).SequenceEqual(["character", "complete", .. Keys]),
            "all ready: every list is sent, in the collector's earlier payload order (same hash as before)");
        Check(Keys.Select((key, index) => allJson.GetProperty(key)[0].GetInt64() == index + 1).All(x => x), "all ready: each list carries its own values");
        Check(CollectibleSnapshotPolicy.NotReadyText([]) == "", "all ready: no not-ready text");
        foreach (var failing in Keys) {
            var capture = Full(failing);
            var label = CollectibleSnapshotPolicy.Label(failing);
            Check(label != failing || failing is "minions" or "mounts" or "bardings" or "emotes", $"{failing}: has a player-facing name");
            var text = CollectibleSnapshotPolicy.NotReadyText(capture.Withheld.Select(entry => entry.Split(':')[0]).Distinct().ToArray());
            if (CollectibleSnapshotPolicy.ClearedWhenOmitted.Contains(failing)) {
                Check(capture.Payload is null, $"{failing}: a list the Site clears when left out still withholds the whole send");
                Check(capture.Withheld.SequenceEqual([$"collectibles.{failing}:NotLoaded", "collectibles:NotLoaded"]), $"{failing}: the whole send and its cause are named");
                Check(text == $"Not ready yet, kept unchanged on Gillions: collectibles (not ready: {label}).", $"{failing}: Sync now names the list that stopped Collections ({text})");
            } else {
                Check(capture.Payload is not null, $"{failing}: the other lists are still sent");
                var json = Json(capture);
                Check(!json.TryGetProperty(failing, out _), $"{failing}: the list that isn't ready is left out, never sent empty");
                Check(json.EnumerateObject().Select(p => p.Name).SequenceEqual(["character", "complete", .. Keys.Where(key => key != failing)]), $"{failing}: every other list is sent");
                Check(json.GetProperty("complete").GetBoolean(), $"{failing}: the sent snapshot stays complete for the lists it carries");
                Check(capture.Withheld.SequenceEqual([$"collectibles.{failing}:NotLoaded"]), $"{failing}: only that list is named as withheld");
                Check(text == $"Collections were sent without {label} (not ready yet).", $"{failing}: Sync now names the list left out ({text})");
            }
        }
        Check(CollectibleSnapshotPolicy.ClearedWhenOmitted.SetEquals(["minions", "mounts", "orchestrions", "fashions"]),
            "the whole-send lists are exactly the ones the Site deletes when left out");
        var none = CollectibleSnapshotPolicy.Capture(() => character, identity, Keys.Where(key => !CollectibleSnapshotPolicy.ClearedWhenOmitted.Contains(key))
            .Select(key => new CollectiblePart(key, () => throw new CollectibleSourceUnavailableException())).ToArray());
        Check(none.Payload is null && none.Withheld[^1] == "collectibles:NotLoaded", "no list ready: nothing is sent");
        var two = CollectibleSnapshotPolicy.Capture(() => character, identity, [new("portraitConditions", () => throw new CollectibleSourceUnavailableException()),
            new("cards", () => new long[] { 1 }), new("folkloreBookIds", () => throw new CollectibleSourceUnavailableException())]);
        Check(CollectibleSnapshotPolicy.NotReadyText(["achievements", .. two.Withheld.Select(entry => entry.Split(':')[0])])
            == "Not ready yet, kept unchanged on Gillions: achievements. Collections were sent without portrait conditions, folklore tomes (not ready yet).",
            "two lists left out and another resource are named together");
        Check(CollectibleSnapshotPolicy.NotReadyText(["achievements", "inventory"]) == "Not ready yet, kept unchanged on Gillions: achievements, inventory.",
            "other resources keep the existing wording");
        Console.WriteLine($"Collectible readiness PASS: {checks}; managed fault/lifecycle fixtures, not physical FFXIV proof.");
    }

    private static IEnumerable<long> Interrupted() {
        yield return 344;
        throw new InvalidOperationException("fixture interrupted sheet enumeration");
    }
}
