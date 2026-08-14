using System;
using System.Collections.Generic;
using System.Linq;

namespace GillionsGameSync;

internal sealed record ArmoireCatalogEntry(uint CabinetId, uint ItemId);

internal static class ArmoireSnapshotPolicy {
    public static uint[] BuildOwnedItemIds(
        IEnumerable<ArmoireCatalogEntry> catalog,
        Func<uint, bool> isStored) {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(isStored);

        return catalog
            .Where(entry => entry.ItemId > 0 && isStored(entry.CabinetId))
            .Select(entry => entry.ItemId)
            .Distinct()
            .Order()
            .ToArray();
    }
}
