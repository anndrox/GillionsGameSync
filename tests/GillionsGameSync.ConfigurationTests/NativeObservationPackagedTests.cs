using System.Collections;
using System.Diagnostics;
using System.Reflection;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;

// Actual packaged configuration type and the real Dalamud serializer; no game.
internal static class NativeObservationPackagedTests {
    // The per-character saddlebag store survives Save/load, and configurations
    // written before it existed load with an empty store, not a failure.
    internal static void Run(Assembly plugin, object configurations, MethodInfo load, IDalamudPluginInterface savedViaPlugin, string product) {
        var checks = 0;
        void Check(bool value, string why) { checks++; if (!value) throw new Exception("Native observation packaged: " + why); }
        var configurationType = plugin.GetType("GillionsGameSync.PluginConfiguration", true)!;
        var ownedType = plugin.GetType("GillionsGameSync.OwnedCharacterState", true)!;
        var observationType = plugin.GetType("GillionsGameSync.InventoryContainerObservation", true)!;
        var itemType = plugin.GetType("GillionsGameSync.InventoryContainerItem", true)!;
        var legacy = JsonConvert.DeserializeObject("{\"OwnedCharacters\":{\"g:1\":{\"Generation\":\"g\",\"CharacterContentId\":\"1\"}}}", configurationType)!;
        var legacyOwned = ((IDictionary)configurationType.GetProperty("OwnedCharacters")!.GetValue(legacy)!)["g:1"]!;
        var legacyStore = (IDictionary?)ownedType.GetProperty("InventoryContainers")!.GetValue(legacyOwned);
        Check(legacyStore is not null && legacyStore.Count == 0, "older configuration loads with an empty saddlebag store");

        var configuration = Activator.CreateInstance(configurationType)!;
        var owned = Activator.CreateInstance(ownedType)!;
        ownedType.GetProperty("Generation")!.SetValue(owned, "g"); ownedType.GetProperty("CharacterContentId")!.SetValue(owned, "1");
        var item = Activator.CreateInstance(itemType)!;
        itemType.GetProperty("ItemId")!.SetValue(item, 12345u); itemType.GetProperty("Quantity")!.SetValue(item, 7); itemType.GetProperty("HqQuantity")!.SetValue(item, 1);
        var observation = Activator.CreateInstance(observationType)!;
        var observedAt = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);
        observationType.GetProperty("ObservedAtUtc")!.SetValue(observation, observedAt);
        ((IList)observationType.GetProperty("Items")!.GetValue(observation)!).Add(item);
        ((IDictionary)ownedType.GetProperty("InventoryContainers")!.GetValue(owned)!)["SaddleBag1"] = observation;
        ((IDictionary)configurationType.GetProperty("OwnedCharacters")!.GetValue(configuration)!)["g:1"] = owned;
        configurationType.GetMethod("Save")!.Invoke(configuration, [savedViaPlugin]);
        var restarted = load.Invoke(configurations, [product])!;
        var restartedOwned = ((IDictionary)configurationType.GetProperty("OwnedCharacters")!.GetValue(restarted)!)["g:1"]!;
        var restartedStore = (IDictionary)ownedType.GetProperty("InventoryContainers")!.GetValue(restartedOwned)!;
        var restartedObservation = restartedStore["SaddleBag1"]!;
        var restartedItem = ((IList)observationType.GetProperty("Items")!.GetValue(restartedObservation)!)[0]!;
        Check((DateTime)observationType.GetProperty("ObservedAtUtc")!.GetValue(restartedObservation)! == observedAt
            && (uint)itemType.GetProperty("ItemId")!.GetValue(restartedItem)! == 12345u && (int)itemType.GetProperty("Quantity")!.GetValue(restartedItem)! == 7
            && (int)itemType.GetProperty("HqQuantity")!.GetValue(restartedItem)! == 1, "actual serializer preserves remembered saddlebag contents across restart");
        Console.WriteLine($"Actual saddlebag store Save/load/legacy PASS: {checks}; {product}; synthetic, no native collection.");
    }

    // Measurement only (not a CI assertion): the real Dalamud serializer cost of
    // one full configuration save at a given size, as the plugin saves on the
    // framework thread.
    internal static void MeasureSave(Assembly plugin, string directory, int targetBytes) {
        var configurationType = plugin.GetType("GillionsGameSync.PluginConfiguration", true)!;
        var ownedType = plugin.GetType("GillionsGameSync.OwnedCharacterState", true)!;
        var eventType = plugin.GetType("GillionsGameSync.GilLedgerEvent", true)!;
        var serialize = typeof(PluginConfigurations).GetMethod("SerializeConfig", BindingFlags.Static | BindingFlags.NonPublic)!;
        var configuration = Activator.CreateInstance(configurationType)!;
        var owned = Activator.CreateInstance(ownedType)!;
        ((IDictionary)configurationType.GetProperty("OwnedCharacters")!.GetValue(configuration)!)["g:1"] = owned;
        var events = (IList)ownedType.GetProperty("PendingGilLedgerEvents")!.GetValue(owned)!;
        var start = new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc);
        while (((string)serialize.Invoke(null, [configuration])!).Length < targetBytes)
            for (var i = 0; i < 200; i++) events.Add(Activator.CreateInstance(eventType, [Guid.NewGuid().ToString("N"), start.AddSeconds(events.Count), 1000L + events.Count,
                "vendor_sale", "confirmed", 5333, 3, "1234567890", "Synthetic", 736u, new[] { 1, 2, 3 }, "Synthetic", "World"])!);
        var path = Path.Combine(directory, "measure.json");
        var times = new List<double>();
        for (var run = 0; run < 25; run++) {
            var watch = Stopwatch.StartNew();
            File.WriteAllText(path, (string)serialize.Invoke(null, [configuration])!);
            watch.Stop();
            if (run >= 5) times.Add(watch.Elapsed.TotalMilliseconds);
        }
        times.Sort();
        Console.WriteLine($"Configuration save of {new FileInfo(path).Length:N0} bytes: median {times[times.Count / 2]:F1} ms, p95 {times[(int)(times.Count * 0.95)]:F1} ms, max {times[^1]:F1} ms (serialize + write, after warm-up; synthetic content).");
    }
}
