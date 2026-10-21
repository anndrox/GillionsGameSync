using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace GillionsGameSync;

// Native availability of one observation at capture time. Anything but
// Observed is withheld from upload, so the Site keeps its last accepted value
// instead of replacing it with an empty or zero result.
internal enum NativeAvailability { Observed, NotLoaded, StaleContext }

// One player inventory row. Property order and names are the established wire
// shape of the inventory payload's "items" rows.
internal sealed record InventoryRow(
    [property: JsonPropertyName("itemId")] uint ItemId,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("hqQuantity")] int HqQuantity,
    [property: JsonPropertyName("equippedQuantity")] int EquippedQuantity,
    [property: JsonPropertyName("location")] string Location,
    [property: JsonPropertyName("source")] string Source);

// One currency balance row; the established "currencies" payload item shape.
internal sealed record CurrencyRow(
    [property: JsonPropertyName("itemId")] uint ItemId,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("source")] string Source);

internal static class CurrencyObservationPolicy {
    // An unloaded Currency container is no observation (null). A loaded one
    // with no positive balance is a genuine empty list: every balance is zero.
    internal static CurrencyRow[]? Rows(bool loaded, IEnumerable<CurrencyRow> positiveRows) => loaded ? positiveRows.ToArray() : null;
    // GetGil reads the same container and returns 0 before it loads.
    internal static long? Gil(bool loaded, long value) => loaded ? value : null;
}

// One native container read: Loaded is the container's own loaded flag.
internal sealed record ContainerRead(string Location, bool Loaded, InventoryRow[] Items);

// Last loaded contents of an on-demand container, retained per owned character.
public sealed class InventoryContainerObservation {
    public DateTime ObservedAtUtc { get; set; }
    public List<InventoryContainerItem> Items { get; set; } = [];
}
public sealed class InventoryContainerItem {
    public uint ItemId { get; set; }
    public int Quantity { get; set; }
    public int HqQuantity { get; set; }
}

internal sealed record InventoryAssembly(InventoryRow[] Items, string[] ObservedLocations, string[] CarriedLocations,
    string[] UnobservedLocations, bool StoreChanged);

internal static class InventoryObservationPolicy {
    internal const string NativeSource = "native_client_state";
    internal const string CarriedSource = "native_client_state_last_observed";
    internal const int MaximumCarriedItems = 64;
    // Resident whenever the character is loaded. If any is not loaded the read
    // is not a usable inventory, so the whole resource is withheld: the Site
    // would otherwise delete everything those containers hold.
    internal static readonly string[] RequiredLocations = ["Inventory1", "Inventory2", "Inventory3", "Inventory4", "EquippedItems", "Crystals",
        "ArmoryMainHand", "ArmoryOffHand", "ArmoryHead", "ArmoryBody", "ArmoryHands", "ArmoryLegs", "ArmoryFeets",
        "ArmoryEar", "ArmoryNeck", "ArmoryWrist", "ArmoryRings", "ArmorySoulCrystal"];
    // Loaded only after the player opens them in a session. The Site replaces
    // the whole item list, so the last loaded contents are carried instead of
    // letting an unopened saddlebag delete items.
    internal static readonly string[] CarriedLocations = ["SaddleBag1", "SaddleBag2", "PremiumSaddleBag1", "PremiumSaddleBag2"];

    internal static InventoryAssembly? Assemble(IReadOnlyList<ContainerRead> reads, IDictionary<string, InventoryContainerObservation> carried, DateTime nowUtc) {
        var byLocation = reads.GroupBy(read => read.Location, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
        foreach (var location in RequiredLocations)
            if (!byLocation.TryGetValue(location, out var required) || !required.Loaded) return null;
        var items = new List<InventoryRow>();
        var observed = new List<string>();
        var carriedUsed = new List<string>();
        var unobserved = new List<string>();
        var storeChanged = false;
        foreach (var read in reads) {
            if (read.Loaded) {
                observed.Add(read.Location);
                items.AddRange(read.Items);
                if (CarriedLocations.Contains(read.Location, StringComparer.Ordinal)) storeChanged |= Remember(carried, read, nowUtc);
                continue;
            }
            if (CarriedLocations.Contains(read.Location, StringComparer.Ordinal) && carried.TryGetValue(read.Location, out var prior) && prior?.Items is { } priorItems
                && priorItems.Count <= MaximumCarriedItems && priorItems.All(item => item is not null)) {
                carriedUsed.Add(read.Location);
                items.AddRange(priorItems.Select(item => new InventoryRow(item.ItemId, item.Quantity, item.HqQuantity, 0, read.Location, CarriedSource)));
            } else unobserved.Add(read.Location);
        }
        // An entirely empty character is not a credible native read.
        if (items.Count == 0) return null;
        return new(items.ToArray(), observed.ToArray(), carriedUsed.ToArray(), unobserved.ToArray(), storeChanged);
    }

    private static bool Remember(IDictionary<string, InventoryContainerObservation> carried, ContainerRead read, DateTime nowUtc) {
        // A read larger than any real saddlebag is not remembered (never truncated).
        if (read.Items.Length > MaximumCarriedItems) return false;
        var next = read.Items.Select(row => new InventoryContainerItem { ItemId = row.ItemId, Quantity = row.Quantity, HqQuantity = row.HqQuantity }).ToList();
        if (carried.TryGetValue(read.Location, out var prior) && prior?.Items is { } priorItems && priorItems.Count == next.Count
            && priorItems.Zip(next).All(pair => pair.First is not null && pair.First.ItemId == pair.Second.ItemId && pair.First.Quantity == pair.Second.Quantity && pair.First.HqQuantity == pair.Second.HqQuantity))
            return false;
        carried[read.Location] = new InventoryContainerObservation { ObservedAtUtc = nowUtc, Items = next };
        return true;
    }
}

// Retainer pages and the selling list stay resident after the player leaves a
// bell or switches retainers. Attribute them only to the active retainer, and
// only once that same retainer has stayed active for the stability window.
internal sealed class RetainerContextStability {
    internal static readonly TimeSpan Window = TimeSpan.FromMilliseconds(1500);
    private string retainerId = "";
    private DateTime since = DateTime.MaxValue;

    internal void Observe(string? activeRetainerId, DateTime nowUtc) {
        var current = string.IsNullOrEmpty(activeRetainerId) || activeRetainerId == "0" ? "" : activeRetainerId;
        if (current == retainerId) return;
        retainerId = current;
        since = current.Length == 0 ? DateTime.MaxValue : nowUtc;
    }

    internal bool IsStable(string? activeRetainerId, DateTime nowUtc) =>
        !string.IsNullOrEmpty(activeRetainerId) && activeRetainerId == retainerId && since != DateTime.MaxValue && nowUtc - since >= Window;

    internal void Clear() { retainerId = ""; since = DateTime.MaxValue; }
}
