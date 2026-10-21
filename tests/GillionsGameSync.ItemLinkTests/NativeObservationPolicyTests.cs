using System.Text.Json;
using GillionsGameSync;

// Synthetic native reads only: these exercise availability, carry-forward,
// identity isolation and wire shape, not physical FFXIV collection.
internal static class NativeObservationPolicyTests {
    internal static void Run() {
        var checks = 0;
        void Check(bool value, string why) { checks++; if (!value) throw new Exception("Native observation: " + why); }
        var now = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);
        InventoryRow Row(uint item, int quantity, string location) => new(item, quantity, 0, 0, location, InventoryObservationPolicy.NativeSource);
        List<ContainerRead> Reads(bool saddlebagLoaded, InventoryRow[]? saddlebag = null, bool inventoryLoaded = true, string? unloaded = null) {
            var reads = new List<ContainerRead>();
            foreach (var location in new[] { "Inventory1", "Inventory2", "Inventory3", "Inventory4" })
                reads.Add(new(location, inventoryLoaded, location == "Inventory1" ? [Row(5333, 3, location)] : []));
            reads.Add(new("EquippedItems", true, [new(1, 1, 0, 1, "EquippedItems", InventoryObservationPolicy.NativeSource)]));
            reads.Add(new("Crystals", true, [Row(2, 900, "Crystals")]));
            foreach (var location in InventoryObservationPolicy.RequiredLocations.Where(location => location.StartsWith("Armory", StringComparison.Ordinal)))
                reads.Add(new(location, true, location == "ArmoryRings" ? [Row(4000, 1, location)] : []));
            if (unloaded is not null) reads = reads.Select(read => read.Location == unloaded ? read with { Loaded = false, Items = [] } : read).ToList();
            reads.Add(new("SaddleBag1", saddlebagLoaded, saddlebagLoaded ? saddlebag ?? [] : []));
            reads.Add(new("PremiumSaddleBag1", false, []));
            return reads;
        }

        // Unavailable is never an empty inventory.
        var store = new Dictionary<string, InventoryContainerObservation>(StringComparer.Ordinal);
        Check(InventoryObservationPolicy.Assemble(Reads(false, inventoryLoaded: false), store, now) is null, "unloaded main inventory withholds the whole resource");
        foreach (var resident in new[] { "EquippedItems", "Crystals", "ArmoryRings", "ArmorySoulCrystal" })
            Check(InventoryObservationPolicy.Assemble(Reads(false, unloaded: resident), store, now) is null, "unloaded resident " + resident + " withholds instead of deleting its items");
        var empty = new List<ContainerRead>(InventoryObservationPolicy.RequiredLocations.Select(location => new ContainerRead(location, true, [])));
        Check(InventoryObservationPolicy.Assemble(empty, store, now) is null, "an entirely empty character is not a credible read");

        // A never-opened saddlebag is reported unobserved, not empty, and not invented.
        var first = InventoryObservationPolicy.Assemble(Reads(false), store, now)!;
        Check(first.UnobservedLocations.SequenceEqual(["SaddleBag1", "PremiumSaddleBag1"]) && first.CarriedLocations.Length == 0, "never-observed saddlebags are unobserved");
        Check(!first.Items.Any(row => row.Location.StartsWith("SaddleBag", StringComparison.Ordinal)) && !first.StoreChanged && store.Count == 0, "nothing is invented for an unobserved container");

        // A loaded saddlebag is remembered; a later unloaded read carries it.
        var opened = InventoryObservationPolicy.Assemble(Reads(true, [Row(12345, 7, "SaddleBag1")]), store, now)!;
        Check(opened.ObservedLocations.Contains("SaddleBag1") && opened.StoreChanged && store["SaddleBag1"].Items.Single().ItemId == 12345, "loaded saddlebag is observed and remembered");
        var later = InventoryObservationPolicy.Assemble(Reads(false), store, now.AddMinutes(5))!;
        var carried = later.Items.Single(row => row.Location == "SaddleBag1");
        Check(later.CarriedLocations.SequenceEqual(["SaddleBag1"]) && carried.ItemId == 12345 && carried.Quantity == 7 && carried.Source == InventoryObservationPolicy.CarriedSource,
            "unopened saddlebag carries its last loaded contents instead of deleting them");
        Check(!later.StoreChanged && store["SaddleBag1"].ObservedAtUtc == now, "carrying never refreshes the observation time");
        Check(InventoryObservationPolicy.Assemble(Reads(true, [Row(12345, 7, "SaddleBag1")]), store, now.AddMinutes(9))!.StoreChanged == false, "unchanged loaded contents do not rewrite the store");

        // A genuinely emptied saddlebag is an observation and replaces the memory.
        var emptied = InventoryObservationPolicy.Assemble(Reads(true, []), store, now.AddMinutes(10))!;
        Check(emptied.StoreChanged && store["SaddleBag1"].Items.Count == 0 && !emptied.Items.Any(row => row.Location == "SaddleBag1"), "a loaded empty saddlebag is a real empty observation");
        var afterEmpty = InventoryObservationPolicy.Assemble(Reads(false), store, now.AddMinutes(11))!;
        Check(afterEmpty.CarriedLocations.SequenceEqual(["SaddleBag1"]) && !afterEmpty.Items.Any(row => row.Location == "SaddleBag1"), "carried empty contents stay empty");

        // Character isolation: each owned character has its own store.
        var other = new Dictionary<string, InventoryContainerObservation>(StringComparer.Ordinal);
        var otherRead = InventoryObservationPolicy.Assemble(Reads(false), other, now)!;
        Check(otherRead.UnobservedLocations.Contains("SaddleBag1") && other.Count == 0, "another character never borrows saddlebag memory");

        // Bounded memory: an implausible read is never remembered or truncated.
        var big = Enumerable.Range(1, InventoryObservationPolicy.MaximumCarriedItems + 20).Select(i => Row((uint)i, 1, "SaddleBag1")).ToArray();
        var oversized = InventoryObservationPolicy.Assemble(Reads(true, big), store, now.AddMinutes(12))!;
        Check(!oversized.StoreChanged && store["SaddleBag1"].Items.Count == 0, "an oversized read leaves the prior memory unchanged");
        store["SaddleBag1"].Items.Add(null!);
        Check(InventoryObservationPolicy.Assemble(Reads(false), store, now.AddMinutes(13))!.UnobservedLocations.Contains("SaddleBag1"), "a corrupted memory is not carried");
        store.Remove("SaddleBag1");

        // Wire shape is unchanged from the previous anonymous rows.
        var legacy = JsonSerializer.Serialize(new { itemId = 5333u, quantity = 3, hqQuantity = 0, equippedQuantity = 0, location = "Inventory1", source = "native_client_state" });
        Check(JsonSerializer.Serialize(Row(5333, 3, "Inventory1")) == legacy, "inventory row JSON matches the established payload shape");
        Check(JsonSerializer.Serialize(new CurrencyRow(1, 100, "native_client_state")) == JsonSerializer.Serialize(new { itemId = 1u, quantity = 100, source = "native_client_state" }), "currency row JSON matches the established payload shape");

        // Currency and gil: zero is not unreadable.
        Check(CurrencyObservationPolicy.Rows(false, []) is null && CurrencyObservationPolicy.Rows(false, [new(1, 5, "x")]) is null, "unloaded currency is no observation");
        Check(CurrencyObservationPolicy.Rows(true, [])!.Length == 0, "loaded currency with no balances is a genuine empty list");
        Check(CurrencyObservationPolicy.Gil(false, 0) is null && CurrencyObservationPolicy.Gil(true, 0) == 0 && CurrencyObservationPolicy.Gil(true, 1234) == 1234, "an unreadable gil balance is null; a real zero is zero");

        // Retainer pages stay resident: only a stably active retainer owns them.
        var retainer = new RetainerContextStability();
        Check(!retainer.IsStable("100", now), "no observation means no retainer context");
        retainer.Observe("100", now);
        Check(!retainer.IsStable("100", now.AddMilliseconds(500)), "a just-opened retainer is not yet attributable");
        Check(retainer.IsStable("100", now + RetainerContextStability.Window), "the same retainer becomes attributable after the window");
        Check(!retainer.IsStable("200", now + RetainerContextStability.Window), "another retainer never inherits the context");
        retainer.Observe("200", now.AddSeconds(2));
        Check(!retainer.IsStable("200", now.AddSeconds(2.5)) && !retainer.IsStable("100", now.AddSeconds(5)), "switching retainers restarts the window");
        retainer.Observe(null, now.AddSeconds(6));
        Check(!retainer.IsStable("200", now.AddSeconds(9)), "leaving the bell clears the context");
        retainer.Observe("0", now.AddSeconds(10));
        Check(!retainer.IsStable("0", now.AddSeconds(20)), "a zero retainer id is never a context");
        retainer.Observe("300", now.AddSeconds(30)); retainer.Clear();
        Check(!retainer.IsStable("300", now.AddSeconds(40)), "session reset clears the context");

        // Paired-client hydration stays pending when it could not run, with backoff.
        var hydration = new PairedClientHydrationState();
        hydration.PairingSucceeded();
        Check(hydration.TryBeginCharacterSync(false, now) && !hydration.CharacterSyncPending, "hydration starts once");
        hydration.RetryCharacterSync(now.AddSeconds(5));
        Check(hydration.CharacterSyncPending && !hydration.TryBeginCharacterSync(false, now.AddSeconds(1)), "a not-ready hydration does not retry every frame");
        Check(hydration.TryBeginCharacterSync(false, now.AddSeconds(5)) && !hydration.CharacterSyncPending, "a not-ready hydration is retried after the backoff");

        Console.WriteLine($"Native observation availability/carry/isolation/wire/retainer-context PASS: {checks}; synthetic reads, not physical FFXIV collection.");
    }
}
