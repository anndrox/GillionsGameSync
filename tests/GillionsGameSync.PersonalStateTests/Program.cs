using System.Reflection;
using Lumina.Excel.Sheets;
using System.Text.Json;
using GillionsGameSync;

// Offline disassembly of the installed executable, never process memory. Uses
// Dalamud's existing Iced dependency; does not resolve/invoke runtime functions.
if (args.Length == 2 && args[0] == "--hunt-binary") {
    var bytes = File.ReadAllBytes(args[1]);
    Console.WriteLine("EXE SHA256: " + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
    foreach (var pattern in new[] { Convert.FromHexString("80FA167319") }) {
        var offsets = Enumerable.Range(0, bytes.Length - pattern.Length).Where(i => bytes.AsSpan(i, pattern.Length).SequenceEqual(pattern)).ToArray();
        Console.WriteLine($"Pattern {Convert.ToHexString(pattern)} matches {offsets.Length}");
        foreach (var offset in offsets) {
            var start = offset;
            var decoder = Iced.Intel.Decoder.Create(64, new Iced.Intel.ByteArrayCodeReader(bytes[start..Math.Min(bytes.Length, offset + 33)]));
            decoder.IP = (ulong)start;
            var formatter = new Iced.Intel.IntelFormatter(); var output = new Iced.Intel.StringOutput();
            while (decoder.IP < (ulong)Math.Min(bytes.Length, offset + 33)) { decoder.Decode(out var instruction); output.Reset(); formatter.Format(instruction, output); Console.WriteLine($"{instruction.IP:X}: {output}"); }
        }
    }
    return;
}

// Static game catalog inspection only; no runtime pointers, chat or server access.
if (args.Length == 2 && args[0] == "--hunt-logs") {
    using var game = new Lumina.GameData(args[1]);
    Console.WriteLine("Game version: " + game.Repositories["ffxiv"].Version);
    foreach (var row in game.GetExcelSheet<LogMessage>()!.Where(r => r.Text.ExtractText().Contains("slain", StringComparison.OrdinalIgnoreCase)))
        Console.WriteLine($"Log {row.RowId}: {row.Text.ToMacroString()}");
    return;
}

// Offline SDK/catalog capability inventory only; no live pointers or requests.
if (args.Length == 1 && args[0] == "--dashboard-sdk") {
    var assembly = typeof(FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState).Assembly;
    Console.WriteLine(assembly.GetName());
    foreach (var name in new[] { "PlayerState", "UIState", "InstanceContent", "ContentsNote", "InventoryManager", "SatisfactionSupplyManager", "AgentSatisfactionSupply", "AgentContentsTimer", "DomanEnclaveManager", "AgentReconstructionBox", "FashionCheckManager", "AgentAozContentBriefing" }) {
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
if (args.Length == 1 && args[0] == "--hunt-sdk") {
    var type = typeof(FFXIVClientStructs.FFXIV.Client.Game.UI.MobHunt);
    Console.WriteLine(type.Assembly.GetName());
    foreach (var m in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)) Console.WriteLine(m);
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
for (int ms = 0; ms < 3000; ms += 25)
    Check(!cadence.TryBegin(now.AddMilliseconds(ms), true), "Per-frame/native read cadence exceeded.");
Check(cadence.TryBegin(now.AddSeconds(3), true), "Three-second refresh missing.");
Check(!cadence.TryBegin(now.AddSeconds(3.5), true, force: true), "Manual click flood bypassed one-second native bound.");
Check(cadence.TryBegin(now.AddSeconds(4), true, force: true), "Sync now did not force eligible fresh read before periodic deadline.");
Check(!cadence.TryBegin(now.AddSeconds(5), false, force: true), "Manual refresh bypassed local retention OFF.");
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
var progressionStore = new HuntBillRetention { LocalRetentionEnabled = true };
var progression = new HuntBillRetentionPolicy(progressionStore);
var session = new HuntSessionProgress(); session.Bind(key);
HuntBillObservation Progress(int kills, int seconds) => Bill(kills: kills) with {
    ObservedAtUtc = now.AddSeconds(seconds), SourceEvidence = HuntObservationAdmission.KeyItemEvidence,
    Targets = [new(0, 1, 100, 200, 300, 0, 2, kills, 1, 1), new(1, 2, 101, 200, 300, 0, 1, 1, 1, 1)]
};
for (int kills = 0; kills <= 2; kills++) {
    var observation = Progress(kills, kills * 3);
    Check(progression.Observe(key, [observation]) && progression.LastSemanticChange, "0->1->2 retained semantic progression missing.");
    session.Record(observation);
    using var exportJson = JsonDocument.Parse(progression.PreparePrivateExport(key));
    Check(exportJson.RootElement.GetProperty("bills")[0].GetProperty("targets")[0].GetProperty("completed").GetBoolean() == (kills == 2), "2/2 exact completion edge missing.");
    Check(exportJson.RootElement.GetProperty("bills")[0].GetProperty("targets")[1].GetProperty("completed").GetBoolean(), "Multi-target 1/1 completion lost.");
}
Check(!progression.Observe(key, [Progress(2, 9)]) && !progression.LastSemanticChange, "No-change creates semantic dispatch.");
Check(progression.Observe(key, [Progress(2, 70)]) && !progression.LastSemanticChange, "Provenance refresh mistaken for semantic change.");
session.Reset(); session.Bind(key); session.Record(Progress(1, 3));
var final = Progress(2, 6) with { SourceEvidence = null };
Check(session.MayReadFinal(0, now.AddSeconds(6)) && session.CanObserveFinal(final), "Explicit same-order final counters lost when key item/flag disappears.");
Check(!session.CanObserveFinal(Progress(1, 6) with { SourceEvidence = null }), "Disappearance alone fabricated final kill.");
Check(!session.CanObserveFinal(final with { OrderId = 2 }), "Different order inherited completion.");
Check(!session.CanObserveFinal(final with { ObservedAtUtc = now.AddSeconds(10) }), "Stale session counter baseline admitted.");
Check(!session.CanObserveFinal(final with { Targets = final.Targets.Take(1).ToArray() }), "Partial bill fabricated completion.");
Check(!session.CanObserveFinal(final with { Targets = [final.Targets[0] with { ObservedKills = 3 }, final.Targets[1]] }), "Out-of-range counter clamped into completion.");
session.Bind(HuntBillRetentionPolicy.CharacterKey(999));
Check(!session.CanObserveFinal(final), "Character transition inherited live bill baseline.");
session.Bind(key);
Check(!session.CanObserveFinal(final), "Reload/history alone established final native evidence.");
session.Record(Progress(1, 3)); session.Reset();
Check(!session.CanObserveFinal(final), "Opt-out/logout preserved completion admission baseline.");
// Both supported native reads are compared and fail closed on disagreement.
var accessor = typeof(FFXIVClientStructs.FFXIV.Client.Game.UI.MobHunt).GetMethod("GetKillCount", [typeof(byte), typeof(byte)]);
Check(accessor is not null && accessor.ReturnType == typeof(int), "Installed supported accessor signature unavailable.");
foreach (var required in new[] { 1, 2, 3 }) foreach (var count in Enumerable.Range(0, required + 1))
    Check(new HuntCounterReading(count, count).Valid(required), "Raw/accessor agreement rejected.");
foreach (var pair in new[] { (0, 1), (1, 0), (1, 2), (-1, -1), (3, 3) })
    Check(!new HuntCounterReading(pair.Item1, pair.Item2).Valid(2), "Disagreement/invalid native counter preferred or clamped.");
HuntProgressMessage Message(int count, int required = 2, uint npc = 100, int seconds = 4) => new(npc, count, required, now.AddSeconds(seconds));
Check(HuntProgressMessage.LogId == 4411, "Structured progress message identity changed.");
foreach (var message in new[] { Message(-1), Message(3), Message(1, 0), Message(1, 256), Message(1, npc: 0), Message(1) with { ObservedAtUtc = DateTime.SpecifyKind(now, DateTimeKind.Local) } })
    Check(!message.Valid, "Malformed structured progress admitted.");
foreach (var required in new[] { 1, 2, 3 }) {
    var eventSession = new HuntSessionProgress(); eventSession.Bind(key);
    var eventStore = new HuntBillRetention { LocalRetentionEnabled = true }; var eventRetention = new HuntBillRetentionPolicy(eventStore);
    HuntBillObservation Native(int count, int seconds) => Progress(count, seconds) with {
        Targets = [Progress(0, 0).Targets[0] with { RequiredKills = (byte)required, ObservedKills = count }, Progress(0, 0).Targets[1]]
    };
    var initial = Native(0, 0); eventSession.Record(initial); Check(eventRetention.Observe(key, [initial]), "Initial event bill missing.");
    for (int count = 1; count <= required; count++) {
        var rawBill = Native(count - 1, count * 3);
        eventSession.Record(rawBill);
        Check(eventSession.Queue(Message(count, required, seconds: count * 3 + 1)), "Exact recent event did not bind.");
        var readBill = rawBill with { ObservedAtUtc = now.AddSeconds(count * 3 + 2) };
        var corrected = eventSession.Apply(readBill, out var applied);
        Check(applied && corrected.Targets[0].ObservedKills == count, "Typed count failed to correct stale raw count.");
        Check(eventRetention.Observe(key, [corrected]) && eventRetention.LastSemanticChange, "Corrected count not persisted promptly.");
        Check(!eventSession.Apply(readBill, out _).Equals(corrected), "Borrowed event proof replayed.");
        Check(!eventRetention.Observe(key, [readBill with { ObservedAtUtc = readBill.ObservedAtUtc.AddSeconds(1) }]) && !eventRetention.LastSemanticChange,
            "Stale same-order native counter undid positive event progress.");
    }
    using var completedJson = JsonDocument.Parse(eventRetention.PreparePrivateExport(key));
    Check(completedJson.RootElement.GetProperty("bills")[0].GetProperty("targets")[0].GetProperty("completed").GetBoolean(), "1/1,2/2,3/3 final event missing.");
    var reloaded = JsonSerializer.Deserialize<HuntBillRetention>(JsonSerializer.Serialize(eventStore))!;
    Check(!new HuntBillRetentionPolicy(reloaded).Observe(key, [Native(required - 1, 60)]), "Restart stale count erased retained positive completion.");
    Check(eventRetention.Observe(key, [Native(0, 61) with { OrderId = 2 }]), "New obtained order incorrectly inherits old progress.");
}
session.Reset(); session.Bind(key); session.Record(Progress(1, 3));
Check(session.Queue(Message(2)), "Bound final event not queued.");
var eventFinal = session.Apply(Progress(1, 6) with { SourceEvidence = null }, out var finalEventApplied);
Check(finalEventApplied && session.CanObserveFinal(eventFinal), "Explicit bound event final lost during bill-item transition.");
foreach (var broken in new[] { final with { OrderId = 2 }, final with { EventItemId = 2000002 }, final with { Targets = [final.Targets[0] with { TargetId = 99 }, final.Targets[1]] }, final with { ObservedAtUtc = now.AddSeconds(11) }, Progress(0, 6) }) {
    session.Reset(); session.Bind(key); session.Record(Progress(1, 3)); Check(session.Queue(Message(2)), "Negative proof setup failed.");
    Check(session.Apply(broken, out var applied) == broken && !applied, "Mismatched/stale/regressive source inherited log proof.");
}
session.Reset(); session.Bind(key); session.Record(Progress(1, 3)); session.Record(Progress(1, 3) with { BillTypeId = 1 });
Check(!session.Queue(Message(2)), "Ambiguous NPC/requirement bound to arbitrary bill.");
session.Reset(); session.Bind(key); session.Record(Progress(1, 3)); Check(session.Queue(Message(2)), "Character reset setup failed.");
session.Bind(HuntBillRetentionPolicy.CharacterKey(999)); Check(!session.Apply(final, out var foreignApplied).Equals(final) || !foreignApplied, "Cross-character event survived.");
session.Reset(); session.Bind(key); Check(!session.Queue(Message(2)), "History/reload alone authorized event association.");
session.Record(Progress(1, 3)); Check(!session.Queue(Message(2, seconds: 10)), "Stale bill baseline associated new event.");
Check(!session.Queue(Message(2, seconds: 2)), "Event predates baseline.");
session.Reset(); session.Bind(key); session.Record(Progress(0, 0) with { Targets = [Progress(0, 0).Targets[0], Progress(0, 0).Targets[1] with { ObservedKills = 0 }] });
Check(session.Queue(Message(1, seconds: 1)) && session.Queue(Message(1, required: 1, npc: 101, seconds: 2)), "Two target events in one interval collided.");
var multiple = session.Apply(Progress(0, 3) with { Targets = [Progress(0, 0).Targets[0], Progress(0, 0).Targets[1] with { ObservedKills = 0 }] }, out var multiApplied);
Check(multiApplied && multiple.Targets.All(t => t.ObservedKills == 1), "Bounded multi-target interval lost positive event.");
var weekly = Progress(0, 0) with { BillTypeId = 4, Category = "weekly", Targets = [Progress(0, 0).Targets[0] with { RequiredKills = 1, ObservedKills = 0 }] };
session.Reset(); session.Bind(key); session.Record(weekly); Check(session.Queue(Message(1, required: 1, seconds: 1)), "Weekly event unassociated.");
Check(session.Apply(weekly with { ObservedAtUtc = now.AddSeconds(3) }, out var weeklyApplied).Targets[0].ObservedKills == 1 && weeklyApplied, "Weekly/B-rank completion lost.");
// Complete producing->retention->export->immutable preparation->receipt->successor path.
var wireStore = new HuntBillRetention { LocalRetentionEnabled = true };
var wireRetention = new HuntBillRetentionPolicy(wireStore);
var wireState = new PersonalSyncState();
var wireOwner = PersonalSyncPolicy.Owner("synthetic-session", key);
string previousNonce = "";
for (int kills = 0; kills <= 2; kills++) {
    Check(wireRetention.Observe(key, [Progress(kills, kills * 3)]) && wireRetention.LastSemanticChange, "Pipeline lost semantic progress.");
    Check(PersonalSyncPolicy.Due(now, now.AddSeconds(5), DateTime.MinValue, false, wireRetention.LastSemanticChange, true), "Pipeline waited a second periodic phase.");
    var payload = wireRetention.PreparePrivateExport(key);
    var prepared = PersonalSyncPolicy.Prepare(wireState, wireOwner, "hunt_bills", payload)!;
    Check(prepared.Nonce != previousNonce && PersonalSyncPolicy.CanSend(true,true,PersonalSyncPolicy.Origin,true,prepared), "Changed final kill did not prepare/send successor.");
    Check(!PersonalSyncPolicy.CanSend(false,true,PersonalSyncPolicy.Origin,true,prepared), "Pipeline sent disabled personal resource.");
    Check(PersonalSyncPolicy.Receipt(JsonSerializer.Serialize(new { ok=true, snapshotId=Guid.NewGuid(), receivedAt=now, unchanged=false })), "Pipeline receipt rejected.");
    prepared.Acknowledged = true; previousNonce = prepared.Nonce;
    Check(!PersonalSyncPolicy.CanSend(true,true,PersonalSyncPolicy.Origin,true,prepared), "Acknowledged pipeline resends without change.");
}
wireRetention.Observe(key, [Progress(2, 70)]);
Check(PersonalSyncPolicy.Prepare(wireState, wireOwner, "hunt_bills", wireRetention.PreparePrivateExport(key))!.Nonce == previousNonce, "Same semantic final payload creates endless successor.");
var completeBeforeMissing = wireRetention.PreparePrivateExport(key);
Check(!wireRetention.Observe(key, []) && wireRetention.PreparePrivateExport(key) == completeBeforeMissing, "Interface closure/unavailable replaced complete state.");
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
PersonalSyncTests.Run(Check);
HuntBillItemCoverageTests.Run(Check);
Console.WriteLine($"Hunt/private-state/patch/retention/transport fixtures passed: {checks}; synthetic only, no live-game claim.");
