using System.Reflection;
using Lumina.Excel.Sheets;
using System.Text.Json;
using GillionsGameSync;

// Offline SDK/catalog capability inventory only; no live pointers or requests.
if (args.Length == 1 && args[0] == "--dashboard-sdk") {
    var assembly = typeof(FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState).Assembly;
    Console.WriteLine(assembly.GetName());
    foreach (var name in new[] { "PlayerState", "UIState", "InstanceContent", "ContentsNote", "InventoryManager", "SatisfactionSupplyManager", "AgentSatisfactionSupply", "AgentReconstructionBox", "FashionCheckManager", "AgentAozContentBriefing" }) {
        var type = assembly.GetTypes().Single(t => t.Name == name);
        Console.WriteLine(type.FullName);
        foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m is FieldInfo or PropertyInfo or MethodInfo && !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"))
            .Where(m => !m.Name.Contains("MemberFunction") && !m.Name.Contains("VirtualTable"))) Console.WriteLine(member);
    }
    foreach (var name in new[] { "ContentRoulette", "ContentsNote", "SatisfactionNpc", "Tomestones", "TomestonesItem", "AozContent", "FashionCheckWeeklyTheme", "WeeklyBingoOrderData" }) {
        var type = typeof(MobHuntOrder).Assembly.GetTypes().FirstOrDefault(t => t.Name == name && t.Namespace == "Lumina.Excel.Sheets");
        Console.WriteLine("Sheet " + name + ": " + (type is null ? "unavailable" : string.Join(", ", type.GetProperties().Select(p => p.Name + "=" + p.PropertyType.Name))));
    }
    return;
}
if (args.Length == 2 && args[0] == "--dashboard-catalog") {
    using var game = new Lumina.GameData(args[1]);
    Console.WriteLine("Game version: " + game.Repositories["ffxiv"].Version);
    var sheet = game.GetExcelSheet<Lumina.Excel.Sheets.ContentRoulette>()!;
    foreach (var row in sheet.Where(r => r.Name.ExtractText().Length > 0))
        Console.WriteLine(string.Join(", ", typeof(Lumina.Excel.Sheets.ContentRoulette).GetProperties().Where(p => new[] { "RowId", "Name", "IsInDutyFinder", "CompletionArrayIndex" }.Contains(p.Name)).Select(p => p.Name + "=" + p.GetValue(row))));
    foreach (var row in game.GetExcelSheet<Tomestones>()!)
        Console.WriteLine("Tomestone " + string.Join(", ", typeof(Tomestones).GetProperties().Select(p => p.Name + "=" + p.GetValue(row))));
    Console.WriteLine("Challenge IDs: " + string.Join(",", game.GetExcelSheet<Lumina.Excel.Sheets.ContentsNote>()!.Select(r => r.RowId)));
    foreach (var row in game.GetExcelSheet<SatisfactionNpc>()!.Where(r => r.RowId > 0))
        Console.WriteLine($"Delivery {row.RowId}, NPC {row.Npc.RowId}, DeliveriesPerWeek {row.DeliveriesPerWeek}");
    foreach (var row in game.GetExcelSheet<TomestonesItem>()!.Where(r => r.Item.RowId > 0))
        Console.WriteLine($"TomestonesItem {row.RowId}, item {row.Item.RowId}, tomestones {row.Tomestones.RowId}, inventory slot {row.CurrencyInventorySlot}");
    Console.WriteLine("Supported Challenge rows: " + game.GetExcelSheet<Lumina.Excel.Sheets.ContentsNote>()!.Count(r => r.RowId is >= 1 and <= 104 && r.RequiredAmount > 0));
    return;
}

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
var displayTarget = Bill().Targets[0];
var displayBefore = JsonSerializer.Serialize(displayTarget);
int nameReads = 0;
var display = new HuntTargetPresentation(id => { nameReads++; return id == 100 ? "étoile 魔物" : null; });
Check(display.TargetLine(displayTarget) == "  étoile 魔物 — 1/3 kills (target ID 1; NPC name ID 100)", "Localized target/counter/IDs missing.");
Check(display.TargetLine(displayTarget with { ObservedKills = 3 }).Contains("3/3 kills") && nameReads == 1, "Label cache froze counters or reread static catalog.");
Check(displayBefore == JsonSerializer.Serialize(displayTarget), "Presentation changed retained target.");
foreach (string? value in new string?[] { null, "", " \r\n\t ", "\0" }) {
    var missing = new HuntTargetPresentation(_ => value);
    Check(missing.TargetLine(displayTarget).Contains("Target name unavailable — 1/3 kills (target ID 1; NPC name ID 100)"), "Missing name guessed or lost numeric fallback.");
}
Check(new HuntTargetPresentation(_ => throw new InvalidOperationException()).TargetLine(displayTarget).Contains("Target name unavailable"), "Catalog failure broke presentation.");
Check(new HuntTargetPresentation(_ => "\n test\t\0 ").TargetLine(displayTarget).StartsWith("  test —"), "Catalog control text escaped target row.");
Check(new HuntTargetPresentation(_ => new string('x', 1000)).TargetLine(displayTarget).StartsWith("  " + new string('x', 160) + " —"), "Display label bound exceeded.");
int boundedReads = 0;
var boundedNames = new HuntTargetPresentation(_ => { boundedReads++; return null; });
for (uint id = 1; id <= HuntTargetPresentation.MaximumNames; id++) boundedNames.TargetLine(displayTarget with { NpcNameId = id });
for (uint id = 1; id <= HuntTargetPresentation.MaximumNames; id++) boundedNames.TargetLine(displayTarget with { NpcNameId = id });
Check(boundedReads == HuntTargetPresentation.MaximumNames, "Unavailable-name cache rescanned known IDs.");
boundedNames.TargetLine(displayTarget with { NpcNameId = 9999 });
boundedNames.TargetLine(displayTarget with { NpcNameId = 9999 });
Check(boundedReads == HuntTargetPresentation.MaximumNames + 2, "Display cache grew beyond bound.");
Check(boundedNames.TargetLine(displayTarget).Contains("target ID 1") && boundedReads == HuntTargetPresentation.MaximumNames + 2, "Overflow evicted prior static label.");
var cadence = new HuntObservationSchedule();
Check(!cadence.TryBegin(now, false), "Disabled cadence admitted.");
Check(cadence.TryBegin(now, true), "First loaded-cache check not immediate.");
for (int ms = 0; ms < 5000; ms += 25)
    Check(!cadence.TryBegin(now.AddMilliseconds(ms), true), "Per-frame/native read cadence exceeded.");
Check(cadence.TryBegin(now.AddSeconds(5), true), "Five-second refresh missing.");
Check(!cadence.TryBegin(now.AddSeconds(10), false), "Opt-out did not stop due read.");
Check(!cadence.TryBegin(now.AddSeconds(-1), true), "Backwards clock triggered repeated reads.");
cadence.Reset(); Check(cadence.TryBegin(now, true), "Enable/session reset failed.");
Check(!new HuntObservationSchedule().TryBegin(DateTime.SpecifyKind(now, DateTimeKind.Local), true), "Non-UTC cadence admitted.");
Check(HuntObservationAdmission.CanUseBillCache(0, 1, true, 2000001, true), "Matching loaded bill corroboration rejected.");
foreach (var bad in new[] {
    (index: (byte)0, flags: 1, loaded: false, item: 2000001u, present: true),
    (index: (byte)0, flags: 1, loaded: true, item: 2000001u, present: false),
    (index: (byte)0, flags: 0, loaded: true, item: 2000001u, present: true),
    (index: (byte)0, flags: -1, loaded: true, item: 2000001u, present: true),
    (index: (byte)22, flags: 1, loaded: true, item: 2000001u, present: true),
    (index: (byte)0, flags: 1, loaded: true, item: 0u, present: true)
}) Check(!HuntObservationAdmission.CanUseBillCache(bad.index, bad.flags, bad.loaded, bad.item, bad.present), "Unavailable/foreign/absent bill admitted.");
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
    Bill() with { SourceEvidence = "verified-current-acceptance" },
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
Check(policy.Observe(key, [Bill() with { SourceEvidence = HuntObservationAdmission.KeyItemEvidence, ObservedAtUtc = now.AddDays(2) }]), "Corroborated source did not update.");
var evidenceRestart = JsonSerializer.Deserialize<HuntBillRetention>(JsonSerializer.Serialize(store))!;
using (var evidenceJson = JsonDocument.Parse(new HuntBillRetentionPolicy(evidenceRestart).PreparePrivateExport(key))) {
    Check(evidenceJson.RootElement.GetProperty("bills")[0].GetProperty("sourceEvidence").GetString() == HuntObservationAdmission.KeyItemEvidence, "Source evidence lost on restart/export.");
    Check(evidenceJson.RootElement.GetProperty("characterAssociation").GetString()!.Contains("unverified"), "Corroboration incorrectly claims cache ownership.");
    Check(evidenceJson.RootElement.GetProperty("bills")[1].GetProperty("sourceEvidence").ValueKind == JsonValueKind.Null, "Old UI-only observation gained fabricated key-item evidence.");
}
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
