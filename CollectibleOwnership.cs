#if GILLIONS_TEST_BUILD || GILLIONS_COLLECTIBLE_TESTS
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GillionsGameSync;

// Testing-only gallery ownership keys added to the complete collectibles snapshot.
// Site contract (Site 3, galleries #780/#781): facewearGlassesIds = Glasses row ids,
// hairstyleUnlockLinks = CharaMakeCustomize.UnlockLink values, outfitSetIds =
// MirageStoreSetItem row ids. Ownership is read anew for every sync, never cached.
internal static class CollectibleOwnership {
    internal const uint HighQualityOffset = 1_000_000;
    // The Site refuses a key past this; omitting it means unknown, never Missing.
    internal const int MaximumEntries = 4096;
    internal const string SharingLine = "Collections: mounts, minions, facewear, hairstyles, outfits and more sync with your character. Outfits update after you open a glamour dresser.";

    internal static long[] Distinct(IEnumerable<long> ids) => ids.Where(id => id > 0).Distinct().Order().ToArray();

    // A dresser slot holds an item id, plus 1,000,000 for high quality. Only ids that
    // are Mirage Store set rows count as an outfit; loose pieces never do.
    internal static long[] OutfitSetIds(IEnumerable<uint> setRowIds, IEnumerable<uint> dresserItemIds, IEnumerable<uint> armoireItemIds) {
        var sets = setRowIds.Where(id => id > 0).ToHashSet();
        return Distinct(dresserItemIds.Concat(armoireItemIds)
            .Select(id => id >= HighQualityOffset ? id - HighQualityOffset : id)
            .Where(sets.Contains).Select(id => (long)id));
    }

    // Outfits stay absent (not empty) when the dresser was not loaded this session.
    internal static JsonObject With(object payload, long[] facewear, long[] hairstyleUnlockLinks, long[]? outfitSetIds) {
        var root = JsonSerializer.SerializeToNode(payload)!.AsObject();
        // The legacy "facewear" key is another id space on the Site; never send under it.
        void Add(string key, long[]? ids) { if (ids is not null && ids.Length <= MaximumEntries) root[key] = JsonSerializer.SerializeToNode(ids); }
        Add("facewearGlassesIds", facewear);
        Add("hairstyleUnlockLinks", hairstyleUnlockLinks);
        Add("outfitSetIds", outfitSetIds);
        return root;
    }
}
#endif
