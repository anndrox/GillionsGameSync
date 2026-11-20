using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

internal static class WireContractPackagedTests {
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static async Task RunAsync(Assembly assembly, string directory, bool baseline = false) {
        var version = assembly.GetName().Version!.ToString(4);
        var game = "2026.11.03.0000.0000";
        var sdk = "7.57.0.9480";
        var time = new DateTime(2026, 11, 20, 2, 25, 0, DateTimeKind.Utc).AddMilliseconds(123);
        var observed = time.AddTicks(4567);
        Type T(string name) => assembly.GetType("GillionsGameSync." + name, true)!;
        object New(string name, params object?[] args) =>
            Activator.CreateInstance(T(name), BindingFlags.Public | Instance, null, args, null)!;
        object Invoke(string type, string method, params object?[] args) =>
            T(type).GetMethod(method, Static)!.Invoke(null, args)!;

        var plugin = RuntimeHelpers.GetUninitializedObject(T("Plugin"));
        var snapshotRequest = T("Plugin").GetMethod("SnapshotRequest", Instance)!;
        using var lifetime = (IDisposable)New("SyncRequestLifetime");
        var mode = Enum.Parse(T("SyncRequestMode"), "Manual");
        var permit = T("SyncRequestLifetime").GetMethod("Capture")!.Invoke(lifetime,
            [mode, 1UL, null, "https://test.gillions.app", "SYNTHETIC_CREDENTIAL_FOR_WIRE_FIXTURES"])!;
        const string nonce = "synthetic-wire-contract";
        HttpRequestMessage Request(string resource, string payload) =>
            (HttpRequestMessage)snapshotRequest.Invoke(plugin, ["/api/game-sync/sync", permit, resource, nonce, Encoding.UTF8.GetBytes(payload)])!;

        Directory.CreateDirectory(directory);
        var destinations = Array.CreateInstance(T("TravelDestination"), 1);
        destinations.SetValue(New("TravelDestination", 52U, 134U, "OBSERVED_IN_PERSONAL_LIST", "UNKNOWN", 200U, null, null, null, null), 0);
        var observation = New("TravelObservation", 134U, 15U, 22.1, 21.2, observed, game, sdk, version, "OBSERVED_PARTIAL", destinations);
        if (assembly.GetName().Name == "GillionsGameSyncTest")
            T("TravelObservation").GetProperty("CurrentWorldId")!.SetValue(observation, 21U);
        using var travel = (IDisposable)New("TravelPrepared", "synthetic", observation, observed);
        var travelPayload = (string)T("TravelPrepared").GetProperty("Payload", Instance)!.GetValue(travel)!;
        using (var request = Request("travel_context", travelPayload)) {
            var bytes = await request.Content!.ReadAsByteArrayAsync();
            using var envelope = JsonDocument.Parse(bytes);
            var sent = envelope.RootElement.GetProperty("payload").GetProperty("observedAtUtc").GetDateTime();
            Check(sent == (baseline ? observed : time), "Travel wire timestamp precision.");
            await File.WriteAllBytesAsync(Path.Combine(directory, "travel-wire.json"), bytes);
        }

        uint[] itemIds = [2001361,2001700,2001701,2001702,2001362,2001703,2002113,2002114,2002115,2002116,
            2002628,2002629,2002630,2002631,2003090,2003091,2003092,2003093,2003509,2003510,2003511,2003512];
        var domains = Array.CreateInstance(T("HuntBillItemState"), itemIds.Length);
        for (var i = 0; i < itemIds.Length; i++)
            domains.SetValue(New("HuntBillItemState", (byte)i, itemIds[i], "absent_confirmed"), i);
        var coverage = New("HuntBillItemSnapshot", Guid.NewGuid().ToString("N"), time, game, version, sdk, domains);
        var history = Invoke("HuntBillRetentionPolicy", "SerializePrivateBills", Array.CreateInstance(T("HuntBillObservation"), 0));
        var huntPayload = (string)Invoke("HuntBillItemCoverage", "WithCoverage", history, coverage, false);
        using (var request = Request("hunt_bills", huntPayload)) {
            var bytes = await request.Content!.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(Path.Combine(directory, "hunt-wire.json"), bytes);
        }

        var source = New("FateSource", "dalamud-fate-table", version, game, 15, "15.0.4.1",
            "d0e93484f99fdb7d58b3a2f22152cf108f3486ff", sdk, "80cb4a7b7e11a6702e2eb8839afd301a918c638e",
            "7.7.0", "7.5.1", game);
        var rows = Array.CreateInstance(T("FateObservation"), 1);
        rows.SetValue(New("FateObservation", (ushort)1000, 21U, 134U, New("FateInstance", "noninstanced", 0U),
            New("FateStateValue", "running", (byte)4), time, null, null, null, null, null, null, null), 0);
        var fate = Invoke("FatePolicy", "Prepare", 1L, source, rows, time);
        var fateBytes = (byte[])T("FatePrepared").GetMethod("CopyBody", Instance)!.Invoke(fate, [])!;
        using (var json = JsonDocument.Parse(fateBytes))
            Check(json.RootElement.GetProperty("observations").GetArrayLength() == 1, "FATE body is complete UTF-8 JSON.");
        await File.WriteAllBytesAsync(Path.Combine(directory, "fate-wire.json"), fateBytes);

        try {
            using var malformed = Request("travel_context", "{\"missingValue\":");
            Check(baseline, "Malformed raw payload reached an outgoing HTTP request.");
        } catch (TargetInvocationException error) when (error.InnerException is JsonException or ArgumentException) {
            Check(!baseline, "Baseline fixture expected the historical unchecked raw writer.");
        }
        Console.WriteLine($"Packaged Hunt, Travel, and FATE wire fixtures PASS: {assembly.GetName().Name} {version}.");
    }

    private static void Check(bool condition, string message) {
        if (!condition) throw new InvalidOperationException(message);
    }
}
