using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal static class GapBaselineTests {
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run(Assembly assembly, Func<object, object> reload, string fixturePath) {
        var pluginType = assembly.GetType("GillionsGameSync.Plugin", true)!;
        var configType = assembly.GetType("GillionsGameSync.PluginConfiguration", true)!;
        var ownedType = assembly.GetType("GillionsGameSync.OwnedCharacterState", true)!;
        var ledgerType = assembly.GetType("GillionsGameSync.GilLedgerEvent", true)!;
        var balanceType = assembly.GetType("GillionsGameSync.RetainerBalanceRead", true)!;
        var accounting = assembly.GetType("GillionsGameSync.DurableEvidenceBudget", true)!.GetMethod("SerializeAccountingDocument")!;
        object Get(object value, string name) => value.GetType().GetProperty(name)!.GetValue(value)!;
        void Set(object value, string name, object? item) => value.GetType().GetProperty(name)!.SetValue(value, item);
        object NewLedger(int id, long amount) => Activator.CreateInstance(ledgerType, new object?[] {
            id.ToString("D32"), DateTime.UtcNow, amount, "retainer_gil_receipt", "inferred",
            null, null, null, null, null, Array.Empty<int>(), "Fixture", "Test",
        })!;
        byte[] Evidence(object config) => (byte[])accounting.Invoke(null, [Get(Get(config, "OwnedCharacters"), "Values")])!;
        object NewPlugin(object config, object state) {
            // No constructor, subscriptions, HTTP, native readers or game services run.
            // Only managed production recovery/observation/correlation methods are invoked.
            var plugin = RuntimeHelpers.GetUninitializedObject(pluginType);
            foreach (var name in new[] { "evidenceBudget", "savePolicy", "recentRetainerWithdrawals", "recentGilLedgerLogs",
                "recentGilLedgerChat", "emittedRetainerChatEvidence", "diagnosticsLock", "diagnostics" }) {
                var field = pluginType.GetField(name, PrivateInstance)!;
                field.SetValue(plugin, Activator.CreateInstance(field.FieldType));
            }
            pluginType.GetField("configuration", PrivateInstance)!.SetValue(plugin, config);
            pluginType.GetField("activeOwnedState", PrivateInstance)!.SetValue(plugin, state);
            pluginType.GetField("log", PrivateInstance)!.SetValue(plugin, DispatchProxy.Create<IPluginLog, GapDiagnosticProxy>());
            return plugin;
        }
        void Invoke(object plugin, string name, params object?[] arguments) =>
            pluginType.GetMethod(name, PrivateInstance)!.Invoke(plugin, arguments);
        IList Pending(object state, string name) => (IList)Get(state, name);
        IDictionary Balances(object state) => (IDictionary)Get(state, "RetainerGilBalances");
        object Balance(long gil) => Activator.CreateInstance(balanceType, ["100", "Fixture retainer", "Fixture town", gil])!;

        var config = Activator.CreateInstance(configType)!;
        var states = (IDictionary)Get(config, "OwnedCharacters");
        foreach (var (key, content, generation) in new[] { ("first:111", "111", "first"), ("first:222", "222", "first"), ("second:111", "111", "second") }) {
            var state = Activator.CreateInstance(ownedType)!;
            Set(state, "CharacterContentId", content); Set(state, "Generation", generation);
            Set(Get(state, "RetainerState"), "CharacterContentId", content);
            Balances(state)["100"] = 1000L;
            Balances(state)["200"] = 2000L;
            Pending(state, "PendingGilLedgerEvents").Add(NewLedger(states.Count * 10 + 1, 123));
            Pending(state, "PendingRetainerGilReceipts").Add(NewLedger(states.Count * 10 + 2, 321));
            Pending(state, "PendingRetainerGilDeposits").Add(NewLedger(states.Count * 10 + 3, -111));
            var saleType = assembly.GetType("GillionsGameSync.PendingRetainerSale", true)!;
            Pending(state, "PendingRetainerSales").Add(Activator.CreateInstance(saleType,
                [(states.Count * 10 + 4).ToString("D32"), DateTime.UtcNow, 77L, 500, 1, "100", "Fixture retainer"])!);
            var resultType = assembly.GetType("GillionsGameSync.RetainerVentureResultEvent", true)!;
            var itemType = assembly.GetType("GillionsGameSync.RetainerVentureResultItem", true)!;
            Pending(Get(state, "RetainerState"), "PendingResultEvents").Add(Activator.CreateInstance(resultType,
                ["1", (states.Count * 10 + 5).ToString("D32"), "fixture-fingerprint", "partial", "100", 1U,
                    DateTime.UtcNow, DateTime.UtcNow, null, Array.CreateInstance(itemType, 0), "native_current"])!);
            states.Add(key, state);
        }
        Pending(config, "PendingGilLedgerEvents").Add(NewLedger(999, 17));
        var gap = Get(config, "CoverageGap");
        Set(gap, "Paused", true); Set(gap, "StartedAtUtc", DateTime.UtcNow.AddSeconds(-2));
        var preserved = Evidence(config);
        config = reload(config); // Restart during a gap must retain both uncertainty and the paused state.
        Assert(preserved.SequenceEqual(Evidence(config)), "Restart during a gap must preserve exact pending evidence.");
        states = (IDictionary)Get(config, "OwnedCharacters");
        var active = states["second:111"]!; // Simulate a newer pairing partition while older evidence remains inactive.
        var plugin = NewPlugin(config, active);
        Invoke(plugin, "AfterAcknowledgedDrain", 0);
        Assert((bool)Get(Get(config, "CoverageGap"), "Paused"), "An ordinary ACK cannot resume a coverage gap.");
        Assert(Balances(active).Count == 2, "No recovery has happened before an actual acknowledged drain.");
        Invoke(plugin, "AfterAcknowledgedDrain", 1);
        Assert(!(bool)Get(Get(config, "CoverageGap"), "Paused"), "Acknowledged room resumes recording.");
        Assert(preserved.SequenceEqual(Evidence(config)), "Recovery must not delete or rewrite pending evidence.");

        foreach (DictionaryEntry entry in states) {
            Assert((long)Balances(entry.Value!)["100"]! == 1000L && (long)Balances(entry.Value!)["200"]! == 2000L,
                "Recovery retains historical balances rather than erasing them.");
            Assert(((IEnumerable)Get(entry.Value!, "RetainerGilBaselinesNeedingRefresh")).Cast<string>().Order().SequenceEqual(new[] { "100", "200" }),
                "Recovery marks loaded and unloaded retainers in every affected partition.");
        }
        config = reload(config); // Restart after recovery but before any retainer is observed.
        Assert(preserved.SequenceEqual(Evidence(config)), "Recovery markers must not alter admitted evidence during reload.");
        states = (IDictionary)Get(config, "OwnedCharacters"); active = states["second:111"]!;
        plugin = NewPlugin(config, active);

        // The first stable read includes a 500-Gil withdrawal wholly inside the gap.
        Invoke(plugin, "ObserveStableRetainerBalance", Balance(500));
        Invoke(plugin, "QueueRetainerGilReceipt", NewLedger(100, 500));
        var queued = Pending(active, "PendingRetainerGilReceipts");
        Assert(queued.Cast<object>().Any(receipt => (string)Get(receipt, "EventId") == 100.ToString("D32")
                && receipt.GetType().GetProperty("RetainerId")!.GetValue(receipt) is null),
            "The first post-gap balance must not attribute a later equal-value receipt to a gap-only withdrawal.");
        Assert((long)Balances(active)["100"]! == 500L, "The first fresh read establishes the new owned baseline.");

        // A subsequent actually observed change remains useful for normal correlation.
        Invoke(plugin, "ObserveStableRetainerBalance", Balance(450));
        Invoke(plugin, "QueueRetainerGilReceipt", NewLedger(101, 50));
        Assert(Pending(active, "PendingGilLedgerEvents").Cast<object>().Any(receipt =>
                (string)Get(receipt, "EventId") == 101.ToString("D32") && (string)Get(receipt, "RetainerId") == "100"),
            "A later valid observed withdrawal still attributes its matching receipt.");
        Assert(queued.Cast<object>().Any(receipt => (string)Get(receipt, "EventId") == 100.ToString("D32")),
            "The earlier uncertain receipt stays pending after an unrelated valid correlation.");

        // Global backpressure also affects other characters and inactive pairing generations.
        foreach (DictionaryEntry entry in states) {
            if (ReferenceEquals(entry.Value, active)) continue;
            Assert((long)Balances(entry.Value!)["100"]! == 1000L,
                "An unobserved partition retains its historical value.");
            Assert(((IEnumerable)Get(entry.Value!, "RetainerGilBaselinesNeedingRefresh")).Cast<string>().Contains("100"),
                "Every affected owner partition still requires a fresh post-gap baseline.");
        }
        Assert(((IEnumerable)Get(active, "RetainerGilBaselinesNeedingRefresh")).Cast<string>().SequenceEqual(new[] { "200" }),
            "Observing one retainer must not validate another unloaded retainer's historical baseline.");
        Pending(active, "PendingRetainerGilDeposits").Add(NewLedger(104, -500));
        Invoke(plugin, "ObserveStableRetainerBalance", Activator.CreateInstance(balanceType, ["200", "Other fixture", "Fixture town", 2500L]));
        Assert(Pending(active, "PendingRetainerGilDeposits").Cast<object>().Any(deposit => (string)Get(deposit, "EventId") == 104.ToString("D32")),
            "A gap-only increase must not confirm an equal-value deposit on the first fresh observation.");
        Pending(active, "PendingRetainerGilDeposits").Add(NewLedger(105, -25));
        Invoke(plugin, "ObserveStableRetainerBalance", Activator.CreateInstance(balanceType, ["200", "Other fixture", "Fixture town", 2525L]));
        Assert(Pending(active, "PendingGilLedgerEvents").Cast<object>().Any(deposit =>
                (string)Get(deposit, "EventId") == 105.ToString("D32") && (string)Get(deposit, "Kind") == "retainer_gil_deposit"),
            "A later genuinely observed increase still confirms its matching deposit.");
        Invoke(plugin, "QueueRetainerGilReceipt", NewLedger(103, 500));
        Assert(queued.Cast<object>().Any(receipt => (string)Get(receipt, "EventId") == 103.ToString("D32")
                && receipt.GetType().GetProperty("RetainerId")!.GetValue(receipt) is null),
            "A retainer unavailable at recovery also needs its own baseline-only first read.");
        var beforeReload = Evidence(config);
        config = reload(config);
        Assert(beforeReload.SequenceEqual(Evidence(config)), "Post-recovery reload must preserve all pending evidence exactly.");
        Assert(Pending(config, "PendingGilLedgerEvents").Count == 1,
            "Recovery and reload do not touch inert D01 legacy evidence.");
        var restored = ((IDictionary)Get(config, "OwnedCharacters"))["first:222"]!;
        var restoredPlugin = NewPlugin(config, restored);
        Invoke(restoredPlugin, "ObserveStableRetainerBalance", Balance(500));
        Invoke(restoredPlugin, "QueueRetainerGilReceipt", NewLedger(102, 500));
        Assert(Pending(restored, "PendingRetainerGilReceipts").Cast<object>().Any(receipt =>
                (string)Get(receipt, "EventId") == 102.ToString("D32") && receipt.GetType().GetProperty("RetainerId")!.GetValue(receipt) is null),
            "A partition revisited after restart cannot reuse its pre-gap baseline.");
        File.WriteAllText(Path.Combine(fixturePath, "gap-baseline-recovery.json"), new JObject {
            ["product"] = assembly.GetName().Name, ["status"] = "PASS", ["nativeGameExecuted"] = false,
            ["pendingBytesBeforeRecovery"] = preserved.Length,
            ["checks"] = new JArray("restart while paused", "re-pair partition preservation", "ordinary ACK cannot resume",
                "actual drain resets all affected baselines", "gap-only delta remains unassigned", "subsequent observed delta correlates",
                "all five pending queues preserved", "gap-only increase cannot confirm deposit", "later observed increase confirms deposit",
                "reload after recovery before observation", "unloaded retainer baseline", "restart and other character baseline", "D01 inert legacy queue preserved"),
        }.ToString(Formatting.Indented));
        Console.WriteLine($"Actual production recovery/first-retainer-observation/receipt regression passed: {assembly.GetName().Name}; no native reads.");
    }

    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

public class GapDiagnosticProxy : DispatchProxy {
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
        if (targetMethod?.Name == "Information") return null;
        throw new InvalidOperationException("Recovery fixture attempted an unexpected game service.");
    }
}
