using System.Text.Json;
using GillionsGameSync;

internal static class CollectibleReadinessTests {
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
        object Payload() {
            payloadReads++;
            return new { complete = true, portraitConditions = conditions.Where(owned.Contains).ToArray(),
                portraitFrames = new long[] { 96 }, mounts = new long[] { 7 } };
        }
        object? Capture() => CollectibleSnapshotPolicy.Capture(() => character, Payload);
        JsonElement Json(object payload) => JsonSerializer.SerializeToElement(payload);
        character = 0;
        Check(Capture() is null && payloadReads == 0, "startup before loaded character must not query unlocks");
        character = 1;
        var first = Json(Capture()!);
        Check(first.GetProperty("complete").GetBoolean(), "successful full capture retains existing completeness");
        Check(first.GetProperty("portraitConditions").EnumerateArray().Select(x => x.GetInt64()).SequenceEqual(conditions), "owned conditions remain exact");
        Check(first.GetProperty("portraitFrames")[0].GetInt64() == 96 && !conditions.Contains(96), "portrait elements stay in their own namespace");
        Check(first.GetProperty("mounts")[0].GetInt64() == 7, "unrelated valid category is unchanged");
        var before = JsonSerializer.Serialize(first);
        Check(CollectibleSnapshotPolicy.Capture(() => character, () => throw new CollectibleSourceUnavailableException()) is null,
            "missing source withholds entire resource, not a complete partial payload");
        Check(JsonSerializer.Serialize(first) == before, "unavailable follow-up cannot mutate copied accepted evidence");
        try {
            CollectibleSnapshotPolicy.Capture(() => character, () => throw new InvalidOperationException("fixture interrupted native read"));
            throw new Exception("unexpected native failure swallowed");
        } catch (InvalidOperationException) { checks++; }
        Check(Capture() is not null, "next ordinary successful observation remains possible");
        owned.Clear();
        var empty = Json(Capture()!);
        Check(empty.GetProperty("complete").GetBoolean() && empty.GetProperty("portraitConditions").GetArrayLength() == 0,
            "fully read catalog with zero owned conditions is authoritative complete-empty");
        Check(JsonSerializer.Serialize(empty) == JsonSerializer.Serialize(Capture()), "same observation retains deterministic payload for existing hash/retry handling");
        var read = 0;
        Check(CollectibleSnapshotPolicy.Capture(() => ++read == 1 ? 1UL : 2UL, Payload) is null, "character switch during capture cannot publish foreign evidence");
        read = 0;
        Check(CollectibleSnapshotPolicy.Capture(() => ++read == 1 ? 1UL : 0UL, Payload) is null, "logout during capture cannot publish evidence");
        character = 2;
        owned.Add(938);
        Check(Json(Capture()!).GetProperty("portraitConditions").EnumerateArray().Select(x => x.GetInt64()).SequenceEqual([938L]),
            "new character gets current unlocks, never prior character ownership");
        character = 0;
        Check(Capture() is null, "logout withholds resource");
        character = 2;
        owned.Add(344);
        Check(Json(Capture()!).GetProperty("portraitConditions").EnumerateArray().Select(x => x.GetInt64()).SequenceEqual([344L, 938L]),
            "reconnect and multiple observations reread current ownership");
        Console.WriteLine($"Collectible readiness PASS: {checks}; managed fault/lifecycle fixtures, not physical FFXIV proof.");
    }

    private static IEnumerable<long> Interrupted() {
        yield return 344;
        throw new InvalidOperationException("fixture interrupted sheet enumeration");
    }
}
