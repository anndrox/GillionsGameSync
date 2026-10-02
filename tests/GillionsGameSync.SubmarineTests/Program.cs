using System.Text.Json;
using GillionsGameSync;

var now = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
var unix = (uint)new DateTimeOffset(now).ToUnixTimeSeconds();
var build = new SubmarineBuild(40, new(3, 4, 1, 2), new(50, 60, 70, 80, 90, 1, 2, 3, 4, 5, 73));
var key = SubmarineVoyageRetentionPolicy.SubmarineKey(987654321, 0, unix - 86400);
SubmarineSnapshot Snapshot(uint returnAt, byte[]? route = null, SubmarineBuild? parts = null) => new(key, 0, "LOCAL-NAME-ONLY",
    unix - 86400, parts ?? build, 100, 200, returnAt, route ?? [1, 2], null, now, "synthetic-game", "0.0.66.0");
SubmarineResult Result(uint quantity = 2) => new("per-sector", [
    new(1, 100, 3, true, false, false, [new(1000, quantity, true)]),
    new(2, 200, null, false, true, true, [new(1001, 4, false)])
], [new(1000, quantity, null), new(1001, 4, null)], 300, []);
SubmarineResult VoyageOnly() => new("voyage-only", [], [new(1000, 2, null), new(1001, 4, null)], null, ["hq-experience-unlocks-unavailable"]);
int checks = 0;
void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
SubmarineVoyageRetention Enabled(bool community = false) => new() { LocalRetentionEnabled = true, CommunityContributionEnabled = community };

var off = new SubmarineVoyageRetention();
var offPolicy = new SubmarineVoyageRetentionPolicy(off);
Check(!offPolicy.ObserveSnapshot(Snapshot(unix + 100)) && off.Voyages.Count == 0, "Local collection is not off by default.");
Check(!offPolicy.ObserveResult(key, build, Result(), now, "synthetic", "synthetic"), "Off result admission changed history.");
bool refused = false; try { offPolicy.PrepareExport(); } catch (InvalidOperationException) { refused = true; }
Check(refused, "Export bypassed community consent.");
Check(offPolicy.WaitingStatus.StartsWith("Local retention off"), "Disabled status contradicts consent.");
off.LocalRetentionEnabled = true;
Check(offPolicy.WaitingStatus.StartsWith("Local retention on; awaiting verified"), "Enabled status does not await loaded evidence.");
off.LocalRetentionEnabled = false;

var planning = Enabled(); var planningPolicy = new SubmarineVoyageRetentionPolicy(planning);
int planningSaves = 0;
void PlanningEvent(byte[] plan, DateTime observed) {
    if (planningPolicy.ObserveSnapshots([Snapshot(unix + 100) with { PlannedRoute = plan, ObservedAtUtc = observed }])) planningSaves++;
}
PlanningEvent([3, 4], now);
var firstPlanningState = JsonSerializer.Serialize(planning);
PlanningEvent([3, 4], now.AddSeconds(1));
Check(planningSaves == 1 && JsonSerializer.Serialize(planning) == firstPlanningState,
    "Identical normalized planning refresh mutated retained state or requested persistence.");
PlanningEvent([4, 3], now.AddSeconds(2));
Check(planningSaves == 2 && planning.Current.Single().Snapshot.PlannedRoute!.SequenceEqual(new byte[] { 4, 3 }),
    "Actual plan change did not produce exactly one semantic save.");
Check(planning.Voyages.Count == 1 && planning.Voyages[0].OrderedRoute.SequenceEqual(new byte[] { 1, 2 }),
    "Planning event changed producing route or duplicated voyage.");

var store = Enabled(true); var policy = new SubmarineVoyageRetentionPolicy(store);
Check(!policy.ObserveSnapshot(Snapshot(unix + 100) with { RegisteredAtUnix = 0 }), "Unloaded registration admitted.");
Check(!policy.ObserveSnapshot(Snapshot(unix + 100) with { Build = build with { Parts = new(0, 4, 1, 2) } }), "Partial parts admitted.");
Check(!policy.ObserveSnapshot(Snapshot(unix + 100, [1, 1])), "Duplicate sector route admitted.");
Check(!policy.ObserveSnapshot(Snapshot(unix + 100, [0, 1])), "Unloaded route admitted.");
Check(store.Voyages.Count == 0, "Unavailable data fabricated an empty voyage.");
Check(policy.ObserveSnapshot(Snapshot(unix + 100)), "Current voyage anchor not admitted.");
Check(store.Voyages.Count == 1 && store.Voyages[0].Result is null && store.Voyages[0].VoyageBuild == build,
    "Expected return was treated as completion, or in-flight build lost.");
Check(!policy.ObserveSnapshot(Snapshot(unix + 100) with { ObservedAtUtc = now.AddSeconds(5) }), "Repeated unchanged snapshot requested a save.");
Check(policy.ObserveSnapshot(Snapshot(unix + 100) with { PlannedRoute = [3, 4] }), "Plan not retained distinctly.");
Check(store.Voyages.Count == 1 && store.Voyages[0].OrderedRoute.SequenceEqual(new byte[] { 1, 2 }), "Planned route replaced dispatched/current route.");

var returned = now.AddSeconds(101); var laterBuild = build with { Rank = 41, Parts = new(7, 8, 5, 6) };
Check(policy.ObserveSnapshot(Snapshot(0, []) with { Build = laterBuild, ObservedAtUtc = returned }), "Idle/result snapshot not retained.");
Check(policy.ObserveResult(key, laterBuild, Result(), returned, "synthetic-result-game", "0.0.66.0"), "Completed result not admitted after return timestamp cleared.");
Check(store.Voyages.Count == 1 && store.Voyages[0].LinkedToVoyage && store.Voyages[0].VoyageBuild == build
    && store.Voyages[0].ResultTimeBuild == laterBuild, "Later build silently substituted for voyage build.");
Check(!policy.ObserveResult(key, laterBuild, Result(), returned.AddSeconds(1), "synthetic", "synthetic"), "Duplicate result counted twice.");
Check(store.Voyages[0].Result?.Sectors[0].Rewards[0].Hq == true && store.Voyages[0].Result?.Sectors[1].AdditionalSubmarineUnlocked == true,
    "Per-sector HQ/unlock evidence lost.");
var output = policy.PrepareExport(); using var doc = JsonDocument.Parse(output);
Check(doc.RootElement.GetProperty("voyages").GetArrayLength() == 1, "Consented completed voyage missing from export.");
Check(doc.RootElement.GetProperty("voyages")[0].GetProperty("departureAtUtc").ValueKind == JsonValueKind.Null, "Departure fabricated.");
Check(!output.Contains("LOCAL-NAME-ONLY") && !output.Contains(key) && !output.Contains("987654321")
    && !output.Contains("registeredAt") && !output.Contains("localSubmarine") && !output.Contains("token", StringComparison.OrdinalIgnoreCase), "Export contains private identity or credentials.");
Check(doc.RootElement.GetProperty("voyages")[0].GetProperty("resultGameVersion").GetString() == "synthetic-result-game", "Result version provenance lost.");
var exportId = doc.RootElement.GetProperty("voyages")[0].GetProperty("observationId").GetString();
var restart = JsonSerializer.Deserialize<SubmarineVoyageRetention>(JsonSerializer.Serialize(store))!;
var restartedPolicy = new SubmarineVoyageRetentionPolicy(restart);
Check(!restartedPolicy.ObserveResult(key, laterBuild, Result(), returned.AddSeconds(2), "synthetic", "synthetic") && restart.Voyages.Count == 1,
    "Persistence/restart lost deduplication.");
Check(JsonDocument.Parse(restartedPolicy.PrepareExport()).RootElement.GetProperty("voyages")[0].GetProperty("observationId").GetString() == exportId,
    "Repeated export regenerated public record identity.");
restart.LocalRetentionEnabled = false; restart.CommunityContributionEnabled = false;
Check(!restartedPolicy.ObserveSnapshot(Snapshot(unix + 999)) && restart.Voyages.Count == 1, "Opt-out erased/changed history.");
refused = false; try { restartedPolicy.PrepareExport(); } catch (InvalidOperationException) { refused = true; }
Check(refused, "Opt-out allowed export.");

var successive = Enabled(); var successivePolicy = new SubmarineVoyageRetentionPolicy(successive);
successivePolicy.ObserveSnapshot(Snapshot(unix + 10));
successivePolicy.ObserveResult(key, build, Result(), now.AddSeconds(11), "synthetic", "synthetic");
successivePolicy.ObserveSnapshot(Snapshot(unix + 1000, [3, 4]) with { ObservedAtUtc = now.AddSeconds(12) });
Check(successive.Voyages.Count == 2 && successive.Voyages[0].Result is not null && successive.Voyages[1].Result is null,
    "Successive voyage overwrote completed history.");
successivePolicy.ObserveSnapshot(Snapshot(unix + 1000, [4, 3]) with { ObservedAtUtc = now.AddSeconds(13) });
Check(successive.Voyages.Count == 2 && successive.Voyages[1].ConflictingObservation
    && successive.Voyages[1].OrderedRoute.SequenceEqual(new byte[] { 3, 4 }), "Identity collision/changed route overwrote route or counted twice.");

var late = Enabled(true); var latePolicy = new SubmarineVoyageRetentionPolicy(late);
latePolicy.ObserveSnapshot(Snapshot(unix - 1));
latePolicy.ObserveResult(key, laterBuild, VoyageOnly(), now, "synthetic", "synthetic");
Check(late.Voyages[0].VoyageBuild is null && late.Voyages[0].Result!.RewardAssociation == "voyage-only"
    && late.Voyages[0].Result!.VoyageRewards.All(r => r.Hq is null), "Late build or voyage-level HQ/sector evidence fabricated.");
latePolicy.ObserveResult(key, laterBuild, Result(), now.AddSeconds(1), "synthetic", "synthetic");
Check(late.Voyages.Count == 1 && late.Voyages[0].Result!.RewardAssociation == "per-sector", "Identical aggregate result could not gain verified sector attribution.");
latePolicy.ObserveResult(key, laterBuild, Result(3), now.AddSeconds(2), "synthetic", "synthetic");
Check(late.Voyages.Count == 1 && late.Voyages[0].ConflictingObservation && late.Voyages[0].Result!.Sectors[0].Rewards[0].Quantity == 2,
    "Conflicting result replaced original.");
Check(JsonDocument.Parse(latePolicy.PrepareExport()).RootElement.GetProperty("voyages").GetArrayLength() == 0, "Conflicting history exported as countable.");

var orphan = Enabled(true); var orphanPolicy = new SubmarineVoyageRetentionPolicy(orphan);
orphanPolicy.ObserveSnapshot(Snapshot(0));
orphanPolicy.ObserveResult(key, laterBuild, Result(), now, "synthetic", "synthetic");
Check(orphan.Voyages.Count == 1 && !orphan.Voyages[0].LinkedToVoyage && orphan.Voyages[0].VoyageBuild is null, "Unlinked result fabricated an anchor/build.");
Check(!orphanPolicy.ObserveResult(key, laterBuild, Result(), now.AddSeconds(1), "synthetic", "synthetic"), "Unlinked duplicate grew history.");
Check(JsonDocument.Parse(orphanPolicy.PrepareExport()).RootElement.GetProperty("voyages").GetArrayLength() == 0, "Unlinked results counted as voyages in export.");

var consent = Enabled(); var consentPolicy = new SubmarineVoyageRetentionPolicy(consent);
consentPolicy.ObserveSnapshot(Snapshot(unix - 1)); consentPolicy.ObserveResult(key, build, Result(), now, "synthetic", "synthetic");
consent.CommunityContributionEnabled = true;
Check(JsonDocument.Parse(consentPolicy.PrepareExport()).RootElement.GetProperty("voyages").GetArrayLength() == 0, "Opt-in retroactively promoted old observations.");

var capacity = Enabled(true); var capacityPolicy = new SubmarineVoyageRetentionPolicy(capacity);
for (uint i = 0; i < SubmarineVoyageRetentionPolicy.MaximumRecords; i++) {
    Check(capacityPolicy.ObserveSnapshot(Snapshot(unix + 100 + i)), "Anchor reservation failed below limit.");
}
var before = JsonSerializer.Serialize(capacity.Voyages);
Check(capacityPolicy.ObserveSnapshot(Snapshot(unix + 10000)) && capacity.CapacityReached, "Overflow was not reported durably.");
Check(capacity.Voyages.Count == 400 && JsonSerializer.Serialize(capacity.Voyages) == before, "Overflow discarded admitted history.");
Check(capacityPolicy.ObserveResult(key, build, Result(), now.AddSeconds(500), "synthetic", "synthetic") && capacity.Voyages.Count == 400,
    "Full retention denied reserved completion space or added another voyage.");
Check(SubmarineVoyageRetentionPolicy.Size(capacity) < SubmarineVoyageRetentionPolicy.MaximumRetainedBytes, "Storage bound exceeded.");
Check(!capacityPolicy.ObserveSnapshot(Snapshot(unix + 10001)), "Repeated overflow caused repeated durable changes.");

var unsupported = Enabled(); unsupported.SchemaVersion = 99;
Check(!new SubmarineVoyageRetentionPolicy(unsupported).ObserveSnapshot(Snapshot(unix + 1)) && unsupported.SchemaVersion == 99,
    "Unsupported retained format rewritten.");
var oversized = Enabled(); oversized.Voyages = Enumerable.Range(0, 401).Select(_ => new RetainedSubmarineVoyage()).ToList();
Check(!new SubmarineVoyageRetentionPolicy(oversized).ObserveSnapshot(Snapshot(unix + 1)) && oversized.Voyages.Count == 401,
    "Oversized old retention silently pruned.");
var currentLimit = Enabled(); var currentLimitPolicy = new SubmarineVoyageRetentionPolicy(currentLimit);
for (uint i = 0; i < 32; i++) currentLimitPolicy.ObserveSnapshot(Snapshot(0) with { LocalSubmarineKey = SubmarineVoyageRetentionPolicy.SubmarineKey(i + 1, 0, unix - 1) });
currentLimitPolicy.ObserveSnapshot(Snapshot(0));
Check(currentLimit.Current.Count == 32 && currentLimit.CapacityReached, "Current-cache growth unbounded or silently evicted.");
var malformed = JsonSerializer.Deserialize<SubmarineVoyageRetention>(JsonSerializer.Serialize(store))!;
malformed.Voyages[0].Result = malformed.Voyages[0].Result! with { Limitations = ["untrusted free text"] };
Check(!new SubmarineVoyageRetentionPolicy(malformed).Supported, "Malformed retained free text admitted to export.");
foreach (bool full in new[] { false, true }) {
    var warningStore = Enabled(); warningStore.CapacityReached = full;
    if (!full) warningStore.SchemaVersion = 99;
    var warningPolicy = new SubmarineVoyageRetentionPolicy(warningStore);
    string warning = full ? "Retention limit reached" : "Unsupported/oversized retained format";
    Check(warningPolicy.WaitingStatus.Contains(warning), "Initial state hid a retained warning.");
    warningStore.CommunityContributionEnabled = true;
    Check(warningPolicy.WaitingStatus.Contains(warning), "Community opt-in hid a retained warning.");
    warningStore.LocalRetentionEnabled = false;
    Check(warningPolicy.WaitingStatus.StartsWith("Local retention off") && warningPolicy.WaitingStatus.Contains(warning),
        "Local opt-out hid a retained warning or implied collection remains on.");
    warningStore.CommunityContributionEnabled = false;
    Check(warningPolicy.WaitingStatus.Contains(warning), "Community opt-out hid a retained warning.");
}
foreach (var invalidTime in new[] { DateTime.SpecifyKind(now, DateTimeKind.Local), DateTime.SpecifyKind(now, DateTimeKind.Unspecified), default(DateTime), DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc) }) {
    foreach (bool resultTime in new[] { false, true }) {
        var malformedTime = JsonSerializer.Deserialize<SubmarineVoyageRetention>(JsonSerializer.Serialize(store))!;
        if (resultTime) malformedTime.Voyages[0].ResultsObservedAtUtc = invalidTime;
        else malformedTime.Voyages[0].FirstObservedAtUtc = invalidTime;
        var preserved = JsonSerializer.Serialize(malformedTime);
        var malformedTimePolicy = new SubmarineVoyageRetentionPolicy(malformedTime);
        Check(!malformedTimePolicy.Supported, "Malformed/local/unspecified retained UTC field admitted.");
        refused = false; try { malformedTimePolicy.PrepareExport(); } catch (InvalidOperationException) { refused = true; }
        Check(refused && JsonSerializer.Serialize(malformedTime) == preserved, "Invalid UTC field exported or silently repaired.");
    }
}

// Complete loaded relationship validation, not just field syntax.
SubmarineVoyageRetention CloneValid() => JsonSerializer.Deserialize<SubmarineVoyageRetention>(JsonSerializer.Serialize(store))!;
var corruptions = new Action<SubmarineVoyageRetention>[] {
    s => s.Current[0].AnchoredVoyageKey = "malformed",
    s => s.Current[0].AnchoredVoyageKey = new string('f', 64),
    s => {
        var foreign = JsonSerializer.Deserialize<RetainedSubmarineVoyage>(JsonSerializer.Serialize(s.Voyages[0]))!;
        foreign.LocalSubmarineKey = new string('b', 64);
        foreign.LocalVoyageKey = SubmarineVoyageRetentionPolicy.Hash($"{foreign.LocalSubmarineKey}:{foreign.ExpectedReturnUnix}");
        foreign.ObservationId = Guid.NewGuid().ToString("N");
        s.Voyages.Add(foreign); s.Current[0].AnchoredVoyageKey = foreign.LocalVoyageKey;
    },
    s => { s.Voyages[0].LocalVoyageKey = new string('c', 64); s.Current[0].AnchoredVoyageKey = s.Voyages[0].LocalVoyageKey; },
    s => s.Voyages[0].Result = s.Voyages[0].Result! with { Sectors = [s.Voyages[0].Result!.Sectors[0] with { SectorId = 3 }, s.Voyages[0].Result!.Sectors[1]] },
    s => s.Voyages[0].Result = s.Voyages[0].Result! with { VoyageRewards = [new(1000, 999, null), new(1001, 4, null)] },
    s => s.Voyages[0].Result = s.Voyages[0].Result! with { TotalExperience = 301 },
    s => s.Voyages[0].Result = s.Voyages[0].Result! with { Sectors = [null!] },
    s => s.Voyages[0].Result = s.Voyages[0].Result! with { VoyageRewards = null! },
    s => s.Voyages[0].BuildEvidence = "unavailable-not-observed-in-flight",
    s => s.Voyages[0].ResultsObservedAtUtc = now.AddSeconds(-1),
    s => s.Voyages[0].ResultsObservedAtUtc = now.AddSeconds(1),
};
foreach (var corrupt in corruptions) {
    var invalid = CloneValid(); corrupt(invalid);
    var bytes = JsonSerializer.Serialize(invalid);
    var invalidPolicy = new SubmarineVoyageRetentionPolicy(invalid);
    Check(!invalidPolicy.Supported, "Inconsistent retained relation admitted on reload.");
    refused = false; try { invalidPolicy.PrepareExport(); } catch (InvalidOperationException) { refused = true; }
    Check(refused && JsonSerializer.Serialize(invalid) == bytes, "Inconsistent retained relation exported or repaired.");
    Check(!invalidPolicy.ObserveSnapshot(Snapshot(unix + 10000))
        && !invalidPolicy.ObserveResult(key, build, Result(), returned, "synthetic", "synthetic")
        && JsonSerializer.Serialize(invalid) == bytes, "Malformed history changed by collection.");
}
var activeMutation = CloneValid(); var activePolicy = new SubmarineVoyageRetentionPolicy(activeMutation);
var badUnlinked = JsonSerializer.Deserialize<SubmarineVoyageRetention>(JsonSerializer.Serialize(orphan))!;
badUnlinked.Voyages[0].LocalVoyageKey = new string('e', 64);
Check(!new SubmarineVoyageRetentionPolicy(badUnlinked).Supported, "Invalid unlinked fingerprint identity admitted.");
Check(activePolicy.Supported, "Valid relation baseline rejected.");
activeMutation.Current[0].AnchoredVoyageKey = new string('f', 64);
var mutatedBytes = JsonSerializer.Serialize(activeMutation);
Check(!activePolicy.ObserveResult(key, build, Result(), returned, "synthetic", "synthetic")
    && JsonSerializer.Serialize(activeMutation) == mutatedBytes, "Cached validation bypassed association guard.");
var exportMutation = CloneValid(); var exportPolicy = new SubmarineVoyageRetentionPolicy(exportMutation);
Check(exportPolicy.Supported, "Valid export baseline rejected.");
exportMutation.Voyages[0].Result = exportMutation.Voyages[0].Result! with { TotalExperience = 999 };
refused = false; try { exportPolicy.PrepareExport(); } catch (InvalidOperationException) { refused = true; }
Check(refused, "Cached validation allowed mutated history export.");
var badAdmission = Enabled(true); var badAdmissionPolicy = new SubmarineVoyageRetentionPolicy(badAdmission);
badAdmissionPolicy.ObserveSnapshot(Snapshot(unix - 1));
var admittedBefore = JsonSerializer.Serialize(badAdmission);
Check(!badAdmissionPolicy.ObserveResult(key, build, Result() with { VoyageRewards = [new(1000, 3, null)] }, now, "synthetic", "synthetic")
    && JsonSerializer.Serialize(badAdmission) == admittedBefore, "Bad aggregate admitted through direct result path.");

var workshopKey = SubmarineVoyageRetentionPolicy.Hash("synthetic-workshop");
var personal = Enabled(); var personalPolicy = new SubmarineVoyageRetentionPolicy(personal);
var personalSnapshot = Snapshot(unix + 100) with { LocalWorkshopKey = workshopKey, UnlockedSectorIds = [1, 2], ExploredSectorIds = [1] };
Check(personalPolicy.ObserveSnapshot(personalSnapshot), "Private scoped snapshot not admitted.");
Check(personalPolicy.ObserveSnapshot(personalSnapshot with { Slot = 1, LocalSubmarineKey = SubmarineVoyageRetentionPolicy.Hash("synthetic-slot-1"), Name = "Synthetic second" }), "Second submarine not admitted.");
Check(!personalPolicy.ObserveSnapshot(personalSnapshot with { UnlockedSectorIds = [0] }), "Unknown/zero sector evidence admitted.");
Check(!personalPolicy.ObserveSnapshot(personalSnapshot with { UnlockedSectorIds = [] }), "Empty sectors passed for unavailable.");
Check(!personalPolicy.ObserveSnapshot(personalSnapshot with { UnlockedSectorIds = [1, 1] }), "Duplicate unlock flags admitted.");
Check(!personalPolicy.ObserveSnapshot(personalSnapshot with { Build = build with { Stats = null! } }), "Partial stats admitted.");
Check(!personalPolicy.ObserveSnapshot(personalSnapshot with { Build = build with { Stats = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0) } }), "Unpopulated stats admitted.");
Check(!personalPolicy.ObserveSnapshot(personalSnapshot with { CurrentRoute = null! }), "Unloaded route admitted.");
Check(!personalPolicy.ObserveSnapshot(personalSnapshot with { LocalWorkshopKey = null }), "Unscoped progression admitted.");
Check(!personalPolicy.ObserveSnapshot(personalSnapshot with { ObservedAtUtc = now.AddSeconds(-1), Build = laterBuild }), "Older build replaced current.");
Check(personalPolicy.ObserveSnapshot(personalSnapshot with { UnlockedSectorIds = null, ExploredSectorIds = null, ObservedAtUtc = now.AddMinutes(2) }), "Bounded freshness refresh missing.");
Check(personal.Current.Single(c => c.Snapshot.Slot == 0).Snapshot.UnlockedSectorIds!.SequenceEqual(new byte[] { 1, 2 }), "Unavailable flags erased positive evidence.");
var privateJson = personalPolicy.PreparePersonalExport(workshopKey);
using (var privateDoc = JsonDocument.Parse(privateJson)) {
    Check(privateDoc.RootElement.GetProperty("slots").GetArrayLength() == 2, "Multiple submarine slots missing.");
    var slot = privateDoc.RootElement.GetProperty("slots")[0];
    Check(slot.GetProperty("voyageState").GetString() == "expected-return-due-not-observed-completion", "Expected return claimed actual completion.");
    Check(slot.GetProperty("build").GetProperty("parts").GetProperty("hull").GetInt32() == 3, "Component ID changed.");
    Check(slot.GetProperty("departureAtUtc").ValueKind == JsonValueKind.Null, "Departure fabricated.");
    Check(slot.GetProperty("sectorCompleteness").GetString() == "positive-observations-only", "Partial flags claimed complete unlocks.");
}
Check(!privateJson.Contains(key) && !privateJson.Contains("987654321"), "Private export contains raw house/submarine key.");
var personalRestart = JsonSerializer.Deserialize<SubmarineVoyageRetention>(JsonSerializer.Serialize(personal))!;
Check(new SubmarineVoyageRetentionPolicy(personalRestart).PreparePersonalExport(workshopKey) == privateJson, "Private state did not survive restart.");
Check(personalPolicy.ObserveSnapshot(personalSnapshot with { CurrentRoute = [], ExpectedReturnUnix = 0, ObservedAtUtc = now.AddMinutes(3) }), "Unavailable voyage snapshot rejected.");
using (var unknownDoc = JsonDocument.Parse(personalPolicy.PreparePersonalExport(workshopKey))) {
    var slot = unknownDoc.RootElement.GetProperty("slots")[0];
    Check(slot.GetProperty("voyageState").GetString() == "unavailable" && slot.GetProperty("orderedSectorIds").ValueKind == JsonValueKind.Null, "Cleared fields claimed idle/empty route.");
}
personal.LocalRetentionEnabled = false;
refused = false; try { personalPolicy.PreparePersonalExport(workshopKey); } catch (InvalidOperationException) { refused = true; }
Check(refused && personal.Current.Count == 2, "Opt-out allowed private export or erased history.");
Check(!PersonalObservationCompatibility.Supports("unavailable", PersonalObservationCompatibility.NativeVersion), "Unsupported patch admitted.");

if (args.Length >= 2 && args[0] == "--fixture") {
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
    File.WriteAllText(args[1], JsonSerializer.Serialize(store));
    File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!, "submarine-personal-v1.json"), privateJson);
    File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!, "personal-retained-fixture.json"), JsonSerializer.Serialize(personalRestart));
    if (args.Length == 4 && args[2] == "--export-fixture") {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[3]))!);
        File.WriteAllText(args[3], policy.PrepareExport());
    }
}
Console.WriteLine($"Submarine managed retention/identity/consent/export fixtures passed: {checks}; no native game reads or live success claim.");
