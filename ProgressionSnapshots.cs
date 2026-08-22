using System;
using System.Collections.Generic;
using System.Linq;

namespace GillionsGameSync;

internal readonly record struct AlliedSocietyProgress(uint BeastTribeId, byte Rank, ushort Reputation);
internal readonly record struct SharedFateZoneProgress(uint TerritoryTypeId, byte Rank, byte MaxRank, ushort CompletedFates, ushort NeededFates);
internal sealed record SharedFateTabProgress(byte TabIndex, SharedFateZoneProgress[] Zones);

internal static class ProgressionSnapshotPolicy {
    public const int SharedFateTabCount = 3;
    public const int SharedFateZonesPerTab = 6;
    public const byte SharedFateMaximumAcceptedRank = 127;
    private static readonly byte[] SharedFateMaximumRanksByTab = [3, 3, 4];

    public static byte NormalizeAlliedSocietyRank(byte rank) => (byte)(rank & 0x7F);

    public static byte NormalizeSharedFateMaximumRank(int tabIndex, byte nativeMaximumRank) =>
        nativeMaximumRank is > 0 and <= SharedFateMaximumAcceptedRank
            ? nativeMaximumRank
            : tabIndex >= 0 && tabIndex < SharedFateMaximumRanksByTab.Length
                ? SharedFateMaximumRanksByTab[tabIndex]
                : (byte)0;

    public static bool IsCompleteSharedFateTab(byte tabIndex, IReadOnlyCollection<SharedFateZoneProgress> zones) =>
        tabIndex < SharedFateTabCount
        && zones.Count == SharedFateZonesPerTab
        && zones.Select(zone => zone.TerritoryTypeId).Distinct().Count() == SharedFateZonesPerTab
        && zones.All(zone => zone.TerritoryTypeId > 0 && zone.MaxRank > 0 && zone.Rank <= zone.MaxRank
            && ((zone.NeededFates > 0 && zone.CompletedFates <= zone.NeededFates)
                || (zone.Rank == zone.MaxRank && zone.NeededFates == 0)));

    public static SharedFateTabProgress[]? BuildCompleteSharedFateSnapshot(
        IEnumerable<IReadOnlyCollection<SharedFateZoneProgress>> nativeTabsInDisplayOrder) {
        var tabs = nativeTabsInDisplayOrder
            .Select((zones, index) => new SharedFateTabProgress((byte)index, zones.ToArray()))
            .ToArray();
        return IsCompleteSharedFateSnapshot(tabs) ? tabs : null;
    }

    public static bool IsCompleteSharedFateSnapshot(IEnumerable<SharedFateTabProgress> tabs) {
        var tabList = tabs.ToArray();
        return tabList.Length == SharedFateTabCount
            && tabList.Select(tab => tab.TabIndex).Distinct().OrderBy(index => index).SequenceEqual(new byte[] { 0, 1, 2 })
            && tabList.All(tab => IsCompleteSharedFateTab(tab.TabIndex, tab.Zones))
            && tabList.SelectMany(tab => tab.Zones).Select(zone => zone.TerritoryTypeId).Distinct().Count() == SharedFateTabCount * SharedFateZonesPerTab;
    }
}
