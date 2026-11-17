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

        // The collector's full list set: all ready, then each source failing on its own, with the sheet and row it names.
        character = 1;
        var now = new DateTime(2026, 11, 17, 5, 12, 0, DateTimeKind.Utc);
        var noParks = new Dictionary<string, SyncSectionPark>();
        var noLastSent = new Dictionary<string, DateTime>();
        CollectibleCapture Full(string? failing) => CollectibleSnapshotPolicy.Capture(() => character, identity,
            Keys.Select((key, index) => new CollectiblePart(key, () => key == failing ? throw new CollectibleSourceUnavailableException("BannerCondition", (uint)(index + 100)) : new long[] { index + 1 })).ToArray());
        IReadOnlyList<SyncSectionStatus> Report(CollectibleCapture capture) => SyncSectionReport.Build(["collectibles"],
            capture.Payload is null ? new HashSet<string>() : new HashSet<string> { "collectibles" }, capture.Withheld, noParks, now, noLastSent);
        var allReady = Full(null);
        var allJson = Json(allReady);
        Check(allReady.Withheld.Length == 0, "all ready: nothing withheld");
        Check(allJson.EnumerateObject().Select(p => p.Name).SequenceEqual(["character", "complete", .. Keys]),
            "all ready: every list is sent, in the collector's earlier payload order (same hash as before)");
        Check(Keys.Select((key, index) => allJson.GetProperty(key)[0].GetInt64() == index + 1).All(x => x), "all ready: each list carries its own values");
        var allReport = Report(allReady);
        Check(allReport.Single().Outcome == SyncSectionOutcome.Sent && allReport.Single().Lists.Count == 0, "all ready: Collections sent with nothing left out");
        Check(SyncSectionReport.Message(allReport, anySent: true) == "Sync completed.", "all ready: Sync completed.");
        Check(SyncSectionReport.LogLine(allReport.Single()) == "Gillions Game Sync: Collections sent.", "all ready: the log line says sent");
        foreach (var (failing, index) in Keys.Select((key, index) => (key, index))) {
            var capture = Full(failing);
            var label = CollectibleSnapshotPolicy.Label(failing);
            var row = index + 100;
            Check(label != failing || failing is "minions" or "mounts" or "bardings" or "emotes", $"{failing}: has a player-facing name");
            var status = Report(capture).Single();
            var message = SyncSectionReport.Message([status], anySent: capture.Payload is not null);
            var line = SyncSectionReport.LogLine(status);
            Check(status.Lists.Single() == new SyncSectionList(failing, "BannerCondition", (uint)row), $"{failing}: the report names the list, the sheet and the row");
            Check(PublicConnectionPresentation.ActionMessage(message) == message, $"{failing}: the Sync now window shows the message ({message})");
            if (CollectibleSnapshotPolicy.ClearedWhenOmitted.Contains(failing)) {
                Check(capture.Payload is null, $"{failing}: a list the Site clears when left out still withholds the whole send");
                Check(capture.Withheld.SequenceEqual([$"collectibles.{failing}:NotLoaded:BannerCondition:{row}", "collectibles:NotLoaded"]), $"{failing}: the whole send and its cause are named");
                Check(status.Outcome == SyncSectionOutcome.Skipped && status.Reason == SyncSectionReport.ListsNotReady, $"{failing}: Collections skipped, lists not ready");
                Check(message == $"Not ready yet, kept unchanged on Gillions: Collections (not ready: {label}). Try again in a moment.", $"{failing}: Sync now names the list that stopped Collections ({message})");
                Check(line == $"Gillions Game Sync: Collections skipped, not ready: {label} (BannerCondition row {row} missing).", $"{failing}: the log names the list, sheet and row ({line})");
            } else {
                Check(capture.Payload is not null, $"{failing}: the other lists are still sent");
                var json = Json(capture);
                Check(!json.TryGetProperty(failing, out _), $"{failing}: the list that isn't ready is left out, never sent empty");
                Check(json.EnumerateObject().Select(p => p.Name).SequenceEqual(["character", "complete", .. Keys.Where(key => key != failing)]), $"{failing}: every other list is sent");
                Check(json.GetProperty("complete").GetBoolean(), $"{failing}: the sent snapshot stays complete for the lists it carries");
                Check(capture.Withheld.SequenceEqual([$"collectibles.{failing}:NotLoaded:BannerCondition:{row}"]), $"{failing}: only that list is named as withheld");
                Check(status.Outcome == SyncSectionOutcome.Sent && status.Reason == SyncSectionReport.ListsNotReady, $"{failing}: Collections sent, with that list left out");
                Check(message == $"Collections were sent without {label} (not ready yet). Try again in a moment.", $"{failing}: Sync now names the list left out ({message})");
                Check(line == $"Gillions Game Sync: Collections sent without {label} (BannerCondition row {row} missing).", $"{failing}: the log names the list, sheet and row ({line})");
            }
        }
        Check(CollectibleSnapshotPolicy.ClearedWhenOmitted.SetEquals(["minions", "mounts", "orchestrions", "fashions"]),
            "the whole-send lists are exactly the ones the Site deletes when left out");
        var none = CollectibleSnapshotPolicy.Capture(() => character, identity, Keys.Where(key => !CollectibleSnapshotPolicy.ClearedWhenOmitted.Contains(key))
            .Select(key => new CollectiblePart(key, () => throw new CollectibleSourceUnavailableException())).ToArray());
        Check(none.Payload is null && none.Withheld[^1] == "collectibles:NotLoaded", "no list ready: nothing is sent");
        var folklore = CollectibleSnapshotPolicy.Capture(() => character, identity, [new("cards", () => new long[] { 1 }), new("folkloreBookIds", () => throw new CollectibleSourceUnavailableException("PlayerState"))]);
        Check(SyncSectionReport.LogLine(Report(folklore).Single()) == "Gillions Game Sync: Collections sent without folklore tomes (PlayerState not loaded).",
            "a native state that isn't loaded is named without a row");
        Check(new CollectibleSourceUnavailableException("Bad name; drop table", 1).SourceName == "", "a source name that isn't a plain identifier is dropped");
        Console.WriteLine($"Collectible readiness PASS: {checks}; managed fault/lifecycle fixtures, not physical FFXIV proof.");
    }

    private static IEnumerable<long> Interrupted() {
        yield return 344;
        throw new InvalidOperationException("fixture interrupted sheet enumeration");
    }
}
