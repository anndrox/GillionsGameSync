using System.Diagnostics;
using System.Text.Json;
using GillionsGameSync;

var now = new DateTime(2026, 10, 3, 1, 0, 0, DateTimeKind.Utc);
var key = DashboardRetentionPolicy.CharacterKey(123456789);
int checks = 0;
void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
bool Refused(Func<string> action) { try { action(); return false; } catch (InvalidOperationException) { return true; } }
DashboardObservation Row(string system, DashboardValue[] values, DateTime? next = null, uint scope = 0) =>
    new(system, scope, now, next, values, "synthetic-game", "synthetic-sdk", "0.0.72.0");
var future = now.AddDays(2);
DashboardValue[] Journal() => new DashboardValue[] { new(0, Progress: 2, Limit: 9, Available: true), new(1, Progress: 0, Limit: 9, Available: true) }
    .Concat(Enumerable.Range(0, 16).Select(i => new DashboardValue((uint)i + 2, (uint)i + 1, Progress: i % 3, Completed: i % 3 > 0))).ToArray();
var rows = new[] {
    Row("roulette-reward", [new(1, Completed: false), new(17, Completed: true)]),
    Row("custom-deliveries-global", [new(0, Progress: 6, Limit: 12, Remaining: 6)], future),
    Row("custom-deliveries-client", [new(0, Progress: 6, Limit: 6, Remaining: 0), new(1, Progress: 50, Limit: 100), new(2, Progress: 3, Limit: 5)], future, 1),
    Row("challenge-log", [new(1, Completed: false), new(103, Completed: true)], future),
    Row("weekly-tomestones", [new(47, Progress: 100, Limit: 900, Remaining: 800)]),
    Row("wondrous-tails", Journal(), future),
    Row("leve-allowance", [new(0, Limit: 100, Remaining: 0)], now.AddHours(4)),
    Row("society-allowance", [new(0, Limit: 12, Remaining: 12)]),
    Row("map-availability", [new(0)], now.AddHours(10)),
    Row("squadron-mission", [new(0)], now.AddHours(18)),
    Row("squadron-training", [new(0)], now.AddHours(1)),
    Row("frontline-weekly", [new(0, Progress: 6), new(1, Progress: 1), new(2, Progress: 2), new(3, Progress: 3)]),
    Row("rival-wings-weekly", [new(0, Progress: 3), new(1, Progress: 2)]),
    Row("doman-enclave-weekly", [new(0, Progress: 10000, Limit: 20000, Remaining: 10000, Available: true)])
};
foreach (var row in rows) {
    var store = new DashboardRetention(); var policy = new DashboardRetentionPolicy(store);
    Check(DashboardRetentionPolicy.Valid(row), row.System + " positive invalid");
    Check(!store.LocalRetentionEnabled && !policy.Observe(key, [row]) && Refused(() => policy.Export(key, now, new HashSet<string>())), "OFF default bypass");
    store.LocalRetentionEnabled = true;
    Check(policy.Observe(key, [row]), "positive admission");
    string before = JsonSerializer.Serialize(store);
    Check(!policy.Observe(key, []) && before == JsonSerializer.Serialize(store), "unavailable erased state");
    Check(!policy.Observe(key, [row]) && before == JsonSerializer.Serialize(store), "duplicate counted again");
    Check(!policy.Observe(key, [row with { ObservedAtUtc = now.AddSeconds(-1) }]), "backwards observation replaced state");
    Check(!policy.Observe(key, [row with { ObservedAtUtc = now.AddSeconds(5) }]), "unchanged save storm");
    Check(policy.Observe(key, [row with { ObservedAtUtc = now.AddMinutes(1) }]), "bounded freshness refresh missing");
    foreach (var bad in new[] { row with { Values = [] }, row with { Values = null! }, row with { Values = [null!] },
        row with { Values = row.Values.Append(row.Values[0]).ToArray() }, row with { ScopeId = uint.MaxValue },
        row with { System = "manual-task" }, row with { GameVersion = "secret\r\n" }, row with { NativeVersion = null! }, row with { CollectorVersion = new string('a', 81) },
        row with { ObservedAtUtc = default }, row with { ObservedAtUtc = DateTime.SpecifyKind(now, DateTimeKind.Local) },
        row with { NextAtUtc = now }, row with { NextAtUtc = now.AddDays(16) },
        row with { NextAtUtc = DateTime.SpecifyKind(future, DateTimeKind.Local) }
    }) Check(!DashboardRetentionPolicy.Valid(bad) && !policy.Observe(key, [bad]), row.System + " malformed admitted");
    Check(DashboardRetentionPolicy.Freshness(row, now, true) == "OBSERVED", "current observation missing");
    Check(DashboardRetentionPolicy.Freshness(row, now, false) == "STALE", "restart/character/session data became fresh");
    Check(DashboardRetentionPolicy.Freshness(row, now.AddMinutes(3), true) == "STALE", "unavailable source stayed fresh");
    Check(DashboardRetentionPolicy.Freshness(row, now.AddSeconds(-1), true) == "STALE", "future observation fresh");
    if (row.NextAtUtc is { } boundary) Check(DashboardRetentionPolicy.Freshness(row, boundary, true) == "STALE", "reset became incomplete rather than stale");
    var restarted = JsonSerializer.Deserialize<DashboardRetention>(JsonSerializer.Serialize(store))!;
    var reload = new DashboardRetentionPolicy(restarted);
    Check(reload.Supported && reload.Rows(key).Length == 1, "persistence loss");
    string export = reload.Export(key, now.AddMinutes(1), new HashSet<string>());
    Check(export.Contains("STALE") && export.Contains("unverified") && export.Contains("not-authorized"), "reload claimed verification");
    Check(!export.Contains(key) && !export.Contains("123456789") && !export.Contains("token", StringComparison.OrdinalIgnoreCase), "private identity/credential exported");
    Check(Refused(() => reload.Export(DashboardRetentionPolicy.CharacterKey(999), now, new HashSet<string>())), "other character exported");
    restarted.LocalRetentionEnabled = false;
    Check(!reload.Observe(key, [row]) && Refused(() => reload.Export(key, now, new HashSet<string>())) && reload.Rows(key).Length == 1, "opt-out erased/collected/exported");
    foreach (int schema in new[] { 0, 2, 999 }) {
        restarted.SchemaVersion = schema; var retained = JsonSerializer.Serialize(restarted);
        Check(!reload.Supported && !reload.Observe(key, [row]) && retained == JsonSerializer.Serialize(restarted), "upgrade/downgrade altered unknown schema");
    }
}
Check(rows[0].Values[0].Available is null && !rows[0].Values[0].Completed!.Value, "incomplete mistaken for eligible");
Check(DashboardRetentionPolicy.Valid(rows[2] with { Values = [new(0, Progress: 6, Limit: 6, Remaining: 0), new(1), new(2, Progress: 5, Limit: 5)] }), "max-rank unavailable satisfaction blocked allowance");
Check(!DashboardRetentionPolicy.Valid(rows[2] with { Values = [new(0, Progress: 6, Limit: 6, Remaining: 0), new(1), new(2, Progress: 4, Limit: 5)] }), "unknown lower-rank progress admitted");
var exhausted = rows[1] with { Values = [new(0, Progress: 12, Limit: 12, Remaining: 0)] };
Check(DashboardSources.AdmitCustomDeliveries(exhausted, rows[2]).Length == 2, "exhausted allowance facts missing");
Check(DashboardSources.AdmitCustomDeliveries(exhausted, null).SequenceEqual(new[] { exhausted }), "missing client suppressed global");
foreach (var badClient in new[] {
    rows[2] with { ScopeId = 0 }, rows[2] with { ScopeId = 13 },
    rows[2] with { Values = [new(0, Progress: 6, Limit: 0, Remaining: 0), new(1), new(2, Progress: 5, Limit: 5)] },
    rows[2] with { Values = [new(0, Progress: 6, Limit: 6, Remaining: 1), new(1), new(2, Progress: 5, Limit: 5)] },
    rows[2] with { Values = [new(0, Progress: 6, Limit: 6, Remaining: 0), new(1), new(2, Progress: 4, Limit: 5)] },
    rows[2] with { Values = [new(0, Progress: 6, Limit: 6, Remaining: 0), new(1, Progress: 100, Limit: 50), new(2, Progress: 3, Limit: 5)] }
}) Check(DashboardSources.AdmitCustomDeliveries(exhausted, badClient).SequenceEqual(new[] { exhausted }), "malformed client suppressed/fabricated global");
foreach (int used in new[] { -1, 13, int.MaxValue }) {
    var badGlobal = exhausted with { Values = [new(0, Progress: used, Limit: 12, Remaining: 12 - used)] };
    Check(DashboardSources.AdmitCustomDeliveries(badGlobal, rows[2]).SequenceEqual(new[] { rows[2] }), "malformed global poisoned valid client");
    Check(DashboardSources.AdmitCustomDeliveries(badGlobal, null).Length == 0, "unavailable pair fabricated observation");
}
Check(DashboardSources.AdmitCustomDeliveries(rows[0], rows[0]).Length == 0, "delivery admission accepted unrelated system");
var deliveryStore = new DashboardRetention { LocalRetentionEnabled = true }; var deliveryPolicy = new DashboardRetentionPolicy(deliveryStore);
Check(deliveryPolicy.Observe(key, [rows[2]]), "initial client retention failed");
Check(deliveryPolicy.Observe(key, DashboardSources.AdmitCustomDeliveries(exhausted, null))
    && deliveryPolicy.Rows(key).Length == 2 && deliveryPolicy.Rows(key).Contains(rows[2]), "global-only read erased retained client");
var deliveryStatus = new DashboardDeliveryDiagnostics();
Check(deliveryStatus.Status == DashboardDeliveryReadStatus.Awaiting && deliveryStatus.LastAttemptUtc is null, "diagnostic claims read at startup");
foreach (var reason in Enum.GetValues<DashboardDeliveryReadStatus>()) {
    deliveryStatus.Record(now, reason);
    Check(deliveryStatus.Status == reason && deliveryStatus.LastAttemptUtc == now
        && deliveryStatus.Text.Contains(reason.ToString()) && !deliveryStatus.Text.Contains(key), "finite delivery reason missing or private identity exposed");
}
deliveryStatus.Reset();
Check(deliveryStatus.LastAttemptUtc is null && deliveryStatus.Status == DashboardDeliveryReadStatus.Awaiting
    && !deliveryStatus.Text.Contains(now.ToString("u")), "delivery diagnostic crosses session/opt-out boundary");
foreach (var bad in new[] { new DashboardValue(0, Completed: true), new(256, Completed: false), new(1, Completed: true, Available: false), new(1, Progress: 0), new(1) })
    Check(!DashboardSources.ValueValid("roulette-reward", bad), "roulette eligibility/unavailable fabricated");
foreach (var system in new[] { "doman-enclave", "raid-weekly-clear", "raid-loot", "fashion-report", "masked-carnivale", "faux-hollows", "manual-task", "dashboard-layout" })
    Check(!DashboardRetentionPolicy.Valid(Row(system, [new(0, Progress: 0)])), "unsupported semantic system admitted");
foreach (var system in new[] { "custom-deliveries-global", "weekly-tomestones" }) {
    Check(DashboardSources.ValueValid(system, new(system == "weekly-tomestones" ? 47u : 0u, Progress: system == "weekly-tomestones" ? 900 : 12,
        Limit: system == "weekly-tomestones" ? 900 : 12, Remaining: 0)), "true zero allowance rejected");
    foreach (var bad in new[] { new DashboardValue(0, Progress: -1, Limit: 12, Remaining: 13), new(0, Progress: 0, Limit: 12, Remaining: 0),
        new(0, Progress: 13, Limit: 12, Remaining: -1), new(0, Progress: 0, Limit: 0, Remaining: 0), new(0, Progress: 0, Limit: 12, Remaining: 12, Completed: true) })
        Check(!DashboardSources.ValueValid(system, bad), "counter malformed/fake completion admitted");
}
foreach (var bad in new[] { Journal().Where(v => v.Id != 3).ToArray(), Journal().Select(v => v.Id == 3 ? v with { Progress = 3 } : v).ToArray(),
    Journal().Select(v => v.Id == 3 ? v with { RelatedId = 0 } : v).ToArray(), Journal().Select(v => v.Id == 0 ? v with { Progress = 10 } : v).ToArray() })
    Check(!DashboardRetentionPolicy.Valid(Row("wondrous-tails", bad, future)), "partial/unknown/invalid journal admitted");
var transitionStore = new DashboardRetention { LocalRetentionEnabled = true }; var transition = new DashboardRetentionPolicy(transitionStore);
Check(transition.Observe(key, [rows[1]]), "initial week failed");
var resetWeek = rows[1] with { ObservedAtUtc = future.AddSeconds(1), NextAtUtc = future.AddDays(7), Values = [new(0, Progress: 0, Limit: 12, Remaining: 12)] };
Check(transition.Observe(key, [resetWeek]) && transition.Rows(key).Single().Values.Single().Progress == 0, "coherent source reload/reset update refused");
Check(transition.Observe(key, [rows[5]]), "initial journal failed");
var journalSuccessor = rows[5] with { ObservedAtUtc = now.AddHours(1), Values = Journal().Select(v => v.Id > 1 ? v with { RelatedId = v.RelatedId + 1 } : v).ToArray() };
Check(transition.Observe(key, [journalSuccessor]) && transition.Rows(key).Count(r => r.System == "wondrous-tails") == 1, "successive journal duplicated");
var full = new DashboardRetention { LocalRetentionEnabled = true }; var fullPolicy = new DashboardRetentionPolicy(full);
for (ulong id = 1; id <= 16; id++) Check(fullPolicy.Observe(DashboardRetentionPolicy.CharacterKey(id), rows), "capacity refused early");
Check(fullPolicy.Observe(key, rows) && full.CapacityReached && full.Characters.Count == 16, "overflow not signaled/preserved");
Check(!fullPolicy.Observe(key, rows), "overflow save storm");
Check(fullPolicy.Observe(DashboardRetentionPolicy.CharacterKey(1), [rows[0] with { ObservedAtUtc = now.AddSeconds(2), Values = [new(1, Completed: true)] }]), "full store update refused");
Check(JsonSerializer.SerializeToUtf8Bytes(full).Length <= DashboardRetentionPolicy.MaximumBytes, "retention bytes exceeded");
var corrupt = JsonSerializer.Deserialize<DashboardRetention>(JsonSerializer.Serialize(full))!;
corrupt.Characters[0].Observations.Add(corrupt.Characters[0].Observations[0]);
Check(!new DashboardRetentionPolicy(corrupt).Supported, "duplicate persisted groups accepted");
corrupt = JsonSerializer.Deserialize<DashboardRetention>(JsonSerializer.Serialize(full))!;
corrupt.Characters.Add(corrupt.Characters[0]);
Check(!new DashboardRetentionPolicy(corrupt).Supported, "duplicate persisted characters accepted");
foreach (var level in new[] { "root", "character", "observation", "value" }) {
    var futureJson = Newtonsoft.Json.Linq.JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(full));
    var target = level switch {
        "root" => futureJson,
        "character" => (Newtonsoft.Json.Linq.JObject)futureJson["Characters"]![0]!,
        "observation" => (Newtonsoft.Json.Linq.JObject)futureJson["Characters"]![0]!["Observations"]![0]!,
        _ => (Newtonsoft.Json.Linq.JObject)futureJson["Characters"]![0]!["Observations"]![0]!["Values"]![0]!
    };
    target["FutureField"] = new Newtonsoft.Json.Linq.JObject { ["preserve"] = "inert-only" };
    var futureStore = Newtonsoft.Json.JsonConvert.DeserializeObject<DashboardRetention>(futureJson.ToString())!;
    var futurePolicy = new DashboardRetentionPolicy(futureStore);
    Check(!futurePolicy.Supported && !futurePolicy.Observe(key, rows)
        && Refused(() => futurePolicy.Export(DashboardRetentionPolicy.CharacterKey(1), now, new HashSet<string>())), "future members admitted/exported");
    Check(Newtonsoft.Json.JsonConvert.SerializeObject(futureStore).Contains("FutureField"), "future retention data stripped");
}
var largeRows = rows.Where(r => r.System != "roulette-reward" && r.System != "challenge-log" && r.System != "custom-deliveries-client" && r.System != "rival-wings-weekly")
    .Concat(new[] { Row("roulette-reward", Enumerable.Range(1,104).Select(i => new DashboardValue((uint)i, Completed: true)).ToArray()),
        Row("challenge-log", Enumerable.Range(1,104).Select(i => new DashboardValue((uint)i, Completed: true)).ToArray(), future) })
    .Concat(Enumerable.Range(1,12).Select(i => rows[2] with { ScopeId = (uint)i })).ToArray();
var byteLimited = new DashboardRetention { LocalRetentionEnabled = true }; var bytePolicy = new DashboardRetentionPolicy(byteLimited);
Check(largeRows.Length == DashboardRetentionPolicy.MaximumGroups && largeRows.All(DashboardRetentionPolicy.Valid), "byte fixture exceeds group bounds before byte admission");
for (ulong id = 1; id <= 16; id++) bytePolicy.Observe(DashboardRetentionPolicy.CharacterKey(id), largeRows);
Check(byteLimited.CapacityReached && byteLimited.Characters.Count < 16 && JsonSerializer.SerializeToUtf8Bytes(byteLimited).Length < DashboardRetentionPolicy.MaximumBytes,
    "byte capacity did not refuse atomically");
Check(!PersonalObservationCompatibility.Supports("future-game", PersonalObservationCompatibility.NativeVersion)
    && !PersonalObservationCompatibility.Supports(PersonalObservationCompatibility.GameBuild, "future-sdk"), "patch gate failed");
var schedule = new DashboardSchedule();
Check(schedule.Begin(now, false) is null, "OFF cadence admitted");
Check(schedule.Begin(now, true) == 0, "initial schedule");
for (int ms = 0; ms < 5000; ms += 25) Check(schedule.Begin(now.AddMilliseconds(ms), true) is null, "per-frame native work admitted");
Check(schedule.Begin(now.AddSeconds(5), true) == 1, "cadence lost");
Check(schedule.Begin(now.AddSeconds(10), false) is null, "OFF reads admitted");
schedule.Reset(); var visited = new HashSet<int>();
for (int i = 0; i < 24; i++) { schedule.Notice(2); visited.Add(schedule.Begin(now.AddSeconds(i * 5), true)!.Value); }
Check(visited.Count == DashboardSchedule.GroupCount, "event priority starved normal sources");
Check(!DashboardRetentionPolicy.Valid(rows.Single(r => r.System == "rival-wings-weekly") with { Values = [new(0, Progress: 1), new(1, Progress: 2)] }), "weekly wins exceeded matches");
Check(!DashboardRetentionPolicy.Valid(rows.Single(r => r.System == "frontline-weekly") with { Values = [new(0, Progress: 1), new(1, Progress: 1), new(2, Progress: 1), new(3, Progress: 0)] }), "weekly places exceeded matches");
var doman = rows.Single(r => r.System == "doman-enclave-weekly");
foreach (var values in new[] {
    new DashboardValue[] { new(0, Progress: 0, Limit: 20000, Remaining: 20000, Available: true) },
    new DashboardValue[] { new(0, Progress: 20000, Limit: 20000, Remaining: 0, Available: false) },
    new DashboardValue[] { new(0, Progress: 65535, Limit: 65535, Remaining: 0, Available: true) }
}) Check(DashboardRetentionPolicy.Valid(doman with { Values = values }), "loaded Doman zero/exhausted/bound value rejected");
foreach (var value in new DashboardValue[] {
    new(0, Progress: 0, Limit: 0, Remaining: 0, Available: true), new(0, Progress: 20001, Limit: 20000, Remaining: -1, Available: true),
    new(0, Progress: 10000, Limit: 20000, Remaining: 0, Available: true), new(0, Progress: 0, Limit: 65536, Remaining: 65536, Available: true),
    new(0, Progress: 0, Limit: 20000, Remaining: 20000), new(0, Progress: 20000, Limit: 20000, Remaining: 0, Available: true, Completed: true)
}) Check(!DashboardRetentionPolicy.Valid(doman with { Values = [value] }), "Doman malformed/unloaded/fabricated completion admitted");
Check(!DashboardRetentionPolicy.Valid(doman with { NextAtUtc = future }), "Doman invented reset admitted");
var oldFacts = new DashboardRetention { LocalRetentionEnabled = true }; var oldPolicy = new DashboardRetentionPolicy(oldFacts);
Check(oldPolicy.Observe(key, rows.Where(r => r.System != "doman-enclave-weekly").ToArray()) && oldPolicy.Supported, "pre-Doman records incompatible");
Check(oldPolicy.Observe(key, [doman]) && oldPolicy.Rows(key).Length == rows.Length, "Doman addition erased older facts");
Check(schedule.Begin(DateTime.SpecifyKind(now.AddDays(1), DateTimeKind.Local), true) is null, "local clock admitted");
var timer = Stopwatch.StartNew(); for (int i = 0; i < 20; i++) Check(fullPolicy.Supported, "bounded validation failed"); timer.Stop();
Console.WriteLine($"Dashboard managed fixtures passed: {checks}; bounded store validation average {timer.Elapsed.TotalMilliseconds / 20:F2} ms; synthetic only, NOT live collection success.");
if (args.Length == 2 && args[0] == "--fixture") {
    var sample = new DashboardRetention { LocalRetentionEnabled = true }; var samplePolicy = new DashboardRetentionPolicy(sample);
    samplePolicy.Observe(key, rows);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
    File.WriteAllText(args[1], samplePolicy.Export(key, now, rows.Select(DashboardRetentionPolicy.Identity).ToHashSet()));
}
