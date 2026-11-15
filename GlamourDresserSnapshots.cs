#if GILLIONS_TEST_BUILD || GILLIONS_POLICY_TESTS
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace GillionsGameSync;

// One glamour dresser slot as read: the raw id carries +1,000,000 for high quality.
internal readonly record struct GlamourDresserSlot(int Slot, uint RawItemId, byte Stain0Id, byte Stain1Id, ushort? SetPieceMask);

internal sealed record GlamourDresserItem(
    [property: JsonPropertyName("itemId")] uint ItemId,
    [property: JsonPropertyName("hq")] bool Hq,
    [property: JsonPropertyName("stain0Id")] byte Stain0Id,
    [property: JsonPropertyName("stain1Id")] byte Stain1Id,
    [property: JsonPropertyName("slot")] int Slot,
    [property: JsonPropertyName("location")] string Location,
    [property: JsonPropertyName("source")] string Source,
    // Outfit (Mirage Store set) slots only: the cache's per-piece bits, unverified bit order.
    [property: JsonPropertyName("setPieceMask"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ushort? SetPieceMask);

internal sealed record GlamourDresserRead(bool Observed, GlamourDresserItem[] Items) {
    internal static readonly GlamourDresserRead Unobserved = new(false, []);
}

internal static class GlamourDresserSnapshotPolicy {
    internal const uint HighQualityOffset = 1_000_000;
    internal const int Slots = 800;
    internal const string Location = "GlamourDresser";
    internal const string Source = "native_loaded_glamour_dresser";

    // An all-empty loaded read is treated as unobserved: MirageManager can stay
    // allocated with nothing loaded, so emptiness is never evidence.
    internal static GlamourDresserRead Build(IReadOnlyList<GlamourDresserSlot> slots, IReadOnlySet<uint> setRowIds) {
        if (slots.Count == 0 || slots.Count > Slots) return GlamourDresserRead.Unobserved;
        var items = slots.Where(s => s.RawItemId != 0 && s.Slot >= 0 && s.Slot < Slots).Select(s => {
            var hq = s.RawItemId >= HighQualityOffset;
            var itemId = hq ? s.RawItemId - HighQualityOffset : s.RawItemId;
            return new GlamourDresserItem(itemId, hq, s.Stain0Id, s.Stain1Id, s.Slot, Location, Source,
                !hq && setRowIds.Contains(itemId) ? s.SetPieceMask : null);
        }).Where(i => i.ItemId > 0).OrderBy(i => i.Slot).ToArray();
        return items.Length == 0 ? GlamourDresserRead.Unobserved : new(true, items);
    }

    // Beside armoireItems/armoireObserved: [] and false when unobserved, so the Site keeps its last list.
    internal static JsonObject With(object payload, GlamourDresserRead read) {
        var root = JsonSerializer.SerializeToNode(payload)!.AsObject();
        root["glamourDresserItems"] = JsonSerializer.SerializeToNode(read.Observed ? read.Items : []);
        root["glamourDresserObserved"] = read.Observed;
        return root;
    }
}
#endif
