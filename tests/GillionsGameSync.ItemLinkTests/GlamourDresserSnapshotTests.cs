using System.Text.Json;
using GillionsGameSync;

// Testing 0.0.107 glamour dresser contents. Ids are public catalog examples, never a real character.
internal static class GlamourDresserSnapshotTests {
    internal static void Run() {
        var checks = 0;
        void Check(bool value, string why) { checks++; if (!value) throw new Exception("Glamour dresser: " + why); }
        var sets = new HashSet<uint> { 41002 };
        var slots = new List<GlamourDresserSlot> {
            new(0, 0, 0, 0, null), new(1, 1_005_000, 3, 0, 0x1F), new(2, 6000, 1, 2, null), new(3, 41002, 0, 0, 0x07), new(4, 41002, 0, 0, null),
        };
        var read = GlamourDresserSnapshotPolicy.Build(slots, sets);
        Check(read.Observed && read.Items.Length == 4, "empty slots skipped, duplicates kept per slot");
        Check(read.Items[0] is { ItemId: 5000, Hq: true, Stain0Id: 3, Slot: 1, SetPieceMask: null }, "HQ offset removed and flagged; a mask on a non-set item is dropped");
        Check(read.Items[1] is { ItemId: 6000, Hq: false, Stain0Id: 1, Stain1Id: 2 }, "both stains kept");
        Check(read.Items[2].SetPieceMask == 0x07 && read.Items[3].SetPieceMask is null, "set mask only where the cache agreed for that slot");
        Check(read.Items.All(i => i.Location == "GlamourDresser" && i.Source == "native_loaded_glamour_dresser"), "location and source label");
        Check(!GlamourDresserSnapshotPolicy.Build([new(0, 0, 0, 0, null), new(1, 0, 0, 0, null)], sets).Observed, "an all-empty loaded dresser is not evidence");
        Check(!GlamourDresserSnapshotPolicy.Build([], sets).Observed, "no slots is unobserved");

        var basePayload = new { character = new { contentId = "1" }, items = new object[0], armoireItems = new object[0], armoireObserved = false };
        var sent = JsonDocument.Parse(JsonSerializer.Serialize(GlamourDresserSnapshotPolicy.With(basePayload, read))).RootElement;
        Check(sent.GetProperty("glamourDresserObserved").GetBoolean() && sent.GetProperty("glamourDresserItems").GetArrayLength() == 4
            && sent.TryGetProperty("armoireObserved", out _), "keys sit beside the armoire");
        var first = sent.GetProperty("glamourDresserItems")[0];
        Check(first.GetProperty("itemId").GetUInt32() == 5000 && first.GetProperty("hq").GetBoolean() && !first.TryGetProperty("setPieceMask", out _),
            "camelCase names; no null mask written");
        Check(sent.GetProperty("glamourDresserItems")[2].GetProperty("setPieceMask").GetUInt16() == 7, "set mask written for set items");
        var unobserved = JsonDocument.Parse(JsonSerializer.Serialize(GlamourDresserSnapshotPolicy.With(basePayload, GlamourDresserRead.Unobserved))).RootElement;
        Check(!unobserved.GetProperty("glamourDresserObserved").GetBoolean() && unobserved.GetProperty("glamourDresserItems").GetArrayLength() == 0,
            "unobserved sends [] with observed false, as the armoire does");
        Console.WriteLine($"Glamour dresser snapshot PASS: {checks}");
    }
}
