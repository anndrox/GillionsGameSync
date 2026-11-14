using System.Text.Json;
using GillionsGameSync;

// Testing 0.0.106 gallery ownership keys. Ids are public catalog examples, never a real character.
internal static class CollectibleOwnershipTests {
    internal static void Run() {
        var checks = 0;
        void Check(bool value, string why) { checks++; if (!value) throw new Exception("Collectible ownership: " + why); }
        Check(CollectibleOwnership.Distinct([7, 3, 7, 0, -1, 3]).SequenceEqual(new long[] { 3, 7 }), "unlock links are positive, distinct and ordered");
        uint[] sets = [41001, 41002, 41003];
        var outfits = CollectibleOwnership.OutfitSetIds(sets, [0, 41002, 5000, 1_041_001, 41002], [41003, 6000]);
        Check(outfits.SequenceEqual(new long[] { 41001, 41002, 41003 }), "dresser HQ offset removed, armoire set items count, duplicates once");
        Check(CollectibleOwnership.OutfitSetIds(sets, [5000, 1_005_000], []).Length == 0, "loose pieces are never outfits");
        Check(CollectibleOwnership.OutfitSetIds([], [41001], []).Length == 0, "no set catalog, no outfits");

        var basePayload = new { character = new { contentId = "1" }, complete = true, fashions = new long[] { 4 } };
        var loaded = JsonDocument.Parse(JsonSerializer.Serialize(CollectibleOwnership.With(basePayload, [12, 13], [101], [41002]))).RootElement;
        Check(loaded.GetProperty("complete").GetBoolean() && loaded.GetProperty("fashions")[0].GetInt64() == 4, "existing collectibles unchanged");
        Check(loaded.GetProperty("facewearGlassesIds").GetArrayLength() == 2 && !loaded.TryGetProperty("facewear", out _) && loaded.GetProperty("hairstyleUnlockLinks")[0].GetInt64() == 101
            && loaded.GetProperty("outfitSetIds")[0].GetInt64() == 41002, "gallery keys use the Site names");
        var unloaded = JsonDocument.Parse(JsonSerializer.Serialize(CollectibleOwnership.With(basePayload, [], [], null))).RootElement;
        Check(!unloaded.TryGetProperty("outfitSetIds", out _), "an unloaded dresser omits outfits instead of sending empty");
        Check(unloaded.GetProperty("facewearGlassesIds").GetArrayLength() == 0 && unloaded.GetProperty("hairstyleUnlockLinks").GetArrayLength() == 0,
            "read unlock lists stay explicit, even when empty");
        var tooMany = Enumerable.Range(1, CollectibleOwnership.MaximumEntries + 1).Select(i => (long)i).ToArray();
        var capped = JsonDocument.Parse(JsonSerializer.Serialize(CollectibleOwnership.With(basePayload, tooMany, [101], [41002]))).RootElement;
        Check(!capped.TryGetProperty("facewearGlassesIds", out _) && capped.GetProperty("hairstyleUnlockLinks").GetArrayLength() == 1,
            "a list past the Site cap is omitted, never cut");
        Check(CollectibleOwnership.SharingLine.Length <= 160 && !CollectibleOwnership.SharingLine.Contains('\n'), "settings line stays compact");
        Console.WriteLine($"Collectible ownership PASS: {checks}");
    }
}
