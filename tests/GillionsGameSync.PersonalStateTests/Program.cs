using System.Reflection;
using Lumina.Excel.Sheets;
using System.Text.Json;
using GillionsGameSync;

// Explicit offline catalog verification; never a game/server request.
if (args.Length == 2 && args[0] == "--catalog") {
    foreach (var type in new[] { typeof(MobHuntOrder), typeof(MobHuntOrderType), typeof(MobHuntTarget) })
        Console.WriteLine(type.Name + ": " + string.Join(", ", type.GetProperties().Select(p => p.Name + "=" + p.PropertyType.Name)));
    Console.WriteLine("Native assembly: " + typeof(FFXIVClientStructs.FFXIV.Client.Game.UI.MobHunt).Assembly.GetName());
    using var game = new Lumina.GameData(args[1]);
    Console.WriteLine("Catalog game version: " + game.Repositories["ffxiv"].Version);
    var types = game.GetExcelSheet<MobHuntOrderType>()!;
    foreach (var row in types.Take(24))
        Console.WriteLine(string.Join(", ", typeof(MobHuntOrderType).GetProperties().Select(p => p.Name + "=" + p.GetValue(row))));
    var orders = game.GetSubrowExcelSheet<MobHuntOrder>()!;
    foreach (var group in orders.Take(3)) foreach (var row in group)
        Console.WriteLine(string.Join(", ", typeof(MobHuntOrder).GetProperties().Select(p => p.Name + "=" + p.GetValue(row))));
    return;
}
var now = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);
var key = HuntBillRetentionPolicy.CharacterKey(123456789);
HuntBillObservation Bill(byte type = 0, int kills = 1, uint order = 1) => new(type, type == 4 ? "weekly" : "daily", 1, order, 2000001,
    [new(0, 1, 100, 200, 300, 0, 3, kills, 1, 1)], now, "synthetic-game", "0.0.68.0");
int checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
bool Refused(Func<string> action) { try { action(); return false; } catch (InvalidOperationException) { return true; } }
Check(PersonalObservationCompatibility.Supports("2026.09.15.0000.0000", "7.56.2.9136"), "Known candidate pair disabled.");
foreach (var pair in new[] { ("unknown", "7.56.2.9136"), ("2026.09.15.0000.0000", "7.56.3.0"), ("2026.10.01.0000.0000", "7.56.2.9136") })
    Check(!PersonalObservationCompatibility.Supports(pair.Item1, pair.Item2), "Unsupported patch/SDK admitted.");
var store = new HuntBillRetention(); var policy = new HuntBillRetentionPolicy(store);
Check(!store.LocalRetentionEnabled && !policy.Observe(key, [Bill()]) && store.Characters.Count == 0, "Default opt-in bypass.");
Check(Refused(() => policy.PreparePrivateExport(key)), "Off export bypass.");
store.LocalRetentionEnabled = true;
Check(!policy.Observe(key, []) && store.Characters.Count == 0, "Unavailable source fabricated empty state.");
foreach (var bad in new[] {
    Bill() with { Targets = [] }, Bill() with { Targets = null! }, Bill() with { Targets = [null!] },
    Bill() with { Targets = [new(0, 0, 100, 200, 300, 0, 3, 1, 1, 1)] },
    Bill() with { Targets = [new(0, 1, 0, 200, 300, 0, 3, 1, 1, 1)] },
    Bill() with { Targets = [new(0, 1, 100, 200, 300, 0, 3, -1, 1, 1)] }, Bill(kills: 4),
    Bill() with { BillTypeId = 22 }, Bill() with { Category = "radar" }, Bill() with { Tier = 0 },
    Bill() with { OrderId = 0 }, Bill() with { ObservedAtUtc = default },
    Bill() with { ObservedAtUtc = DateTime.SpecifyKind(now, DateTimeKind.Local) }
}) Check(!policy.Observe(key, [bad]) && store.Characters.Count == 0, "Partial/unknown/malformed admitted.");
Check(policy.Observe(key, [Bill(), Bill(4)]), "Supported daily/weekly positive observations rejected.");
var saved = JsonSerializer.Serialize(store);
Check(!policy.Observe(key, []) && JsonSerializer.Serialize(store) == saved, "Unavailable source erased retained observations.");
Check(!policy.Observe(key, [Bill() with { ObservedAtUtc = now.AddSeconds(1) }]), "Unchanged event saved too often.");
Check(policy.Observe(key, [Bill(kills: 3) with { ObservedAtUtc = now.AddSeconds(2) }]), "Completed target did not update.");
using (var json = JsonDocument.Parse(policy.PreparePrivateExport(key))) {
    Check(json.RootElement.GetProperty("bills")[0].GetProperty("targets")[0].GetProperty("completed").GetBoolean(), "Completion missing.");
    Check(json.RootElement.GetProperty("resetAtUtc").ValueKind == JsonValueKind.Null, "Reset fabricated.");
}
Check(policy.Observe(key, [Bill(kills: 0, order: 2) with { ObservedAtUtc = now.AddDays(1) }]), "New order did not replace latest positive state.");
Check(store.Characters.Single().Bills.Single(b => b.BillTypeId == 0).OrderId == 2, "Previous day counters substituted.");
Check(!policy.Observe(key, [Bill(kills: 3) with { ObservedAtUtc = now.AddMinutes(-1) }]), "Older observation replaced current state.");
var restarted = JsonSerializer.Deserialize<HuntBillRetention>(JsonSerializer.Serialize(store))!;
var restartPolicy = new HuntBillRetentionPolicy(restarted);
Check(restartPolicy.Supported && restartPolicy.PreparePrivateExport(key) == policy.PreparePrivateExport(key), "Restart lost identity/state.");
Check(Refused(() => policy.PreparePrivateExport(HuntBillRetentionPolicy.CharacterKey(999))), "Different character exposed retained bills.");
var exported = policy.PreparePrivateExport(key);
Check(!exported.Contains(key) && !exported.Contains("123456789") && !exported.Contains("token", StringComparison.OrdinalIgnoreCase), "Private credential/ID export.");
store.LocalRetentionEnabled = false;
Check(!policy.Observe(key, [Bill()]) && Refused(() => policy.PreparePrivateExport(key)), "Opt-out permits collection/export.");
Check(store.Characters.Single().Bills.Count == 2, "Opt-out erased state.");
var malformed = JsonSerializer.Deserialize<HuntBillRetention>(JsonSerializer.Serialize(restarted))!;
malformed.SchemaVersion = 99; var malformedBytes = JsonSerializer.Serialize(malformed);
Check(!new HuntBillRetentionPolicy(malformed).Supported && JsonSerializer.Serialize(malformed) == malformedBytes, "Unknown retained schema altered.");
var full = new HuntBillRetention { LocalRetentionEnabled = true }; var fullPolicy = new HuntBillRetentionPolicy(full);
for (ulong n = 1; n <= 16; n++) Check(fullPolicy.Observe(HuntBillRetentionPolicy.CharacterKey(n), [Bill()]), "Capacity refused too early.");
Check(fullPolicy.Observe(key, [Bill()]) && full.CapacityReached && full.Characters.Count == 16, "Overflow not signaled/preserved.");
Check(!fullPolicy.Observe(key, [Bill()]), "Repeated overflow save storm.");
Check(fullPolicy.Observe(HuntBillRetentionPolicy.CharacterKey(1), [Bill(kills: 3)]), "Capacity denied existing-character update.");
Check(JsonSerializer.SerializeToUtf8Bytes(full).Length < HuntBillRetentionPolicy.MaximumBytes, "Hunt storage exceeded bound.");
if (args.Length == 2 && args[0] == "--fixture") {
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
    File.WriteAllText(args[1], restartPolicy.PreparePrivateExport(key));
}
Console.WriteLine($"Hunt/private-state/patch/retention fixtures passed: {checks}; synthetic only, no live-game claim.");
