using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using GillionsGameSync;
using System.Text.Json;

static void Assert(bool condition, string message) {
    if (!condition) throw new InvalidOperationException(message);
}

static ItemLinkRequest Request(string id = "request-1", long itemId = 4555, DateTime? expires = null, string claim = "claim-1") =>
    new(id, itemId, expires ?? DateTime.UtcNow.AddMinutes(1), claim);

const string currentPublicOrigin = "https://gillions.app";
Assert(PublicUrlConfiguration.TryUseCompiledDefault("", currentPublicOrigin, out var initialOrigin)
    && initialOrigin == currentPublicOrigin, "a new configuration must use the compiled public origin");
Assert(PublicUrlConfiguration.TryUseCompiledDefault(PublicUrlConfiguration.LegacyPublicBaseUrl + "/", currentPublicOrigin + "/", out var migratedOrigin)
    && migratedOrigin == currentPublicOrigin, "the legacy default must migrate to the compiled public origin");
Assert(!PublicUrlConfiguration.TryUseCompiledDefault("https://self-hosted.example", currentPublicOrigin, out var customOrigin)
    && customOrigin == "https://self-hosted.example", "a custom server URL must be preserved");
Assert(!PublicUrlConfiguration.TryUseCompiledDefault(currentPublicOrigin, currentPublicOrigin, out var unchangedOrigin)
    && unchangedOrigin == currentPublicOrigin, "the current compiled origin must not rewrite configuration");
Console.WriteLine("public URL configuration tests passed");

Assert(ProgressionSnapshotPolicy.NormalizeAlliedSocietyRank(0x87) == 7, "the rank-increased-today flag must not inflate allied-society rank");
var completeTabs = Enumerable.Range(0, 3).Select(tabIndex => new SharedFateTabProgress((byte)tabIndex,
    Enumerable.Range(0, 6).Select(zoneIndex => new SharedFateZoneProgress((uint)(1000 + tabIndex * 10 + zoneIndex), 2, 3, 20, 60)).ToArray())).ToArray();
Assert(ProgressionSnapshotPolicy.IsCompleteSharedFateSnapshot(completeTabs), "three valid six-zone Shared FATE tabs must be complete");
Assert(!ProgressionSnapshotPolicy.IsCompleteSharedFateSnapshot(completeTabs.Take(2)), "a partial Shared FATE tab set must not be uploaded as complete");
var duplicateZoneTabs = completeTabs.Select(tab => new SharedFateTabProgress(tab.TabIndex, tab.Zones.ToArray())).ToArray();
duplicateZoneTabs[2].Zones[5] = duplicateZoneTabs[2].Zones[4];
Assert(!ProgressionSnapshotPolicy.IsCompleteSharedFateSnapshot(duplicateZoneTabs), "duplicate Shared FATE territories must be rejected");
Console.WriteLine("reputation and Shared FATE completeness tests passed");

var armoireCatalog = new[] {
    new ArmoireCatalogEntry(0, 2897),
    new ArmoireCatalogEntry(1, 2888),
    new ArmoireCatalogEntry(2, 2897),
    new ArmoireCatalogEntry(3, 0),
};
var armoireItems = ArmoireSnapshotPolicy.BuildOwnedItemIds(armoireCatalog, cabinetId => cabinetId is 0 or 2 or 3);
Assert(armoireItems.SequenceEqual(new uint[] { 2897 }), "unlocked Cabinet rows, including row zero, must emit their authoritative Item IDs once");
Assert(!armoireItems.Contains(2888u), "locked Cabinet rows must not be emitted");
Assert(!armoireItems.Contains(0u), "malformed Cabinet item mappings must not be emitted");
Console.WriteLine("armoire snapshot tests passed");

var ventureNow = new DateTime(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);
var activeCompleteUnix = (uint)new DateTimeOffset(ventureNow.AddHours(1)).ToUnixTimeSeconds();
var readyCompleteUnix = (uint)new DateTimeOffset(ventureNow.AddMinutes(-5)).ToUnixTimeSeconds();
var ventureState = new RetainerVentureLocalState();
Assert(!RetainerVentureSnapshotPolicy.MergeRoster(ventureState, null, ventureNow), "an unavailable roster must preserve prior state");
Assert(!RetainerVentureSnapshotPolicy.MergeRoster(ventureState,
    new RetainerVentureRosterRead(ventureNow, false, []), ventureNow), "a partial roster must not become authoritative");
Assert(RetainerVentureSnapshotPolicy.MergeRoster(ventureState,
    new RetainerVentureRosterRead(ventureNow, true, [
        new("200", "Ready", 0, 0, 22, readyCompleteUnix),
        new("100", "Active", 18, 90, 11, activeCompleteUnix),
    ]), ventureNow), "a complete native roster must be accepted");
Assert(ventureState.Retainers.Select(entry => entry.RetainerId).SequenceEqual(["100", "200"]), "retainers must be stable and sorted");
Assert(ventureState.Retainers[0].ClassJobId == 18 && ventureState.Retainers[0].Level == 90, "known class/job and level values must be preserved");
Assert(ventureState.Retainers[1].ClassJobId is null && ventureState.Retainers[1].Level is null, "zero class/job and level values must remain unknown");
Assert(ventureState.Retainers[0].Venture?.State == "in_progress", "a future completion time must be in progress");
Assert(ventureState.Retainers[1].Venture?.State == "ready", "a past completion time must be ready");
var unchangedObservedAt = ventureState.RosterObservedAtUtc;
Assert(!RetainerVentureSnapshotPolicy.MergeRoster(ventureState,
    new RetainerVentureRosterRead(ventureNow.AddSeconds(5), true, [
        new("200", "Ready", 0, 0, 22, readyCompleteUnix),
        new("100", "Active", 18, 90, 11, activeCompleteUnix),
    ]), ventureNow.AddSeconds(5)), "an unchanged roster must not churn timestamps or hashes");
Assert(ventureState.RosterObservedAtUtc == unchangedObservedAt, "unchanged roster observations must retain their prior timestamp");
Assert(ventureState.Retainers.All(entry => !entry.GearObserved && entry.EquippedItems is null), "gear must remain explicitly unknown before the active retainer inventory loads");
Assert(RetainerVentureSnapshotPolicy.MergeGear(ventureState,
    new RetainerVentureGearRead("100", ventureNow.AddMinutes(1), [new(5, 400, true), new(1, 300, false)])), "loaded native retainer gear must be accepted");
Assert(ventureState.Retainers[0].GearObserved && ventureState.Retainers[0].EquippedItems!.Select(item => item.SlotIndex).SequenceEqual([1, 5]), "observed gear must be sorted and marked authoritative for that retainer");
Assert(!RetainerVentureSnapshotPolicy.MergeGear(ventureState, null), "an unloaded gear container must preserve the last observation");
Assert(RetainerVentureSnapshotPolicy.MergeRoster(ventureState,
    new RetainerVentureRosterRead(ventureNow.AddMinutes(2), true, [
        new("200", "Ready", 0, 0, 0, 0),
        new("100", "Active", 18, 90, 33, readyCompleteUnix),
    ]), ventureNow.AddMinutes(2)), "a changed assignment must update the venture observation");
Assert(ventureState.Retainers[0].Venture?.VentureId == 33 && ventureState.Retainers[0].Venture?.State == "ready", "replacement ready venture state must be represented");
Assert(ventureState.Retainers[1].VentureObserved && ventureState.Retainers[1].Venture is null, "a positive zero-task roster observation must represent idle");
Assert(RetainerVentureSnapshotPolicy.ResolveResultCompletionUnix(ventureState, "100", 33, 0) == readyCompleteUnix,
    "a matching last-known positive assignment must recover completion evidence after the active native field clears");
Assert(RetainerVentureSnapshotPolicy.ResolveResultCompletionUnix(ventureState, "100", 33, activeCompleteUnix) == activeCompleteUnix,
    "valid native completion evidence must take precedence over local fallback state");
Assert(RetainerVentureSnapshotPolicy.ResolveResultCompletionUnix(ventureState, "100", 999, 0) == 0,
    "a prior completion timestamp must not be reused for a different venture");

var resultRead = new RetainerVentureResultRead("100", 33, readyCompleteUnix, ventureNow.AddMinutes(3), 1234,
    [new(500, 2), new(400, 1)]);
var ventureResult = RetainerVentureSnapshotPolicy.CreateResultEvent(resultRead);
Assert(ventureResult is not null && ventureResult.Items.Select(item => item.ItemId).SequenceEqual(new uint[] { 400, 500 }), "a structured result must preserve and sort both native reward items");
Assert(ventureResult!.EventId == RetainerVentureSnapshotPolicy.CreateResultEvent(resultRead)!.EventId, "result event IDs must be deterministic");
Assert(RetainerVentureSnapshotPolicy.AddPendingResult(ventureState, ventureResult), "a new result must enter the bounded retry queue");
Assert(!RetainerVentureSnapshotPolicy.AddPendingResult(ventureState, ventureResult), "a duplicate result must not replay into the queue");
Assert(RetainerVentureSnapshotPolicy.CreateResultEvent(resultRead with { VentureCompleteUnix = 0 }) is null, "a result without authoritative completion evidence must be rejected");

var persistedState = JsonSerializer.Deserialize<RetainerVentureLocalState>(JsonSerializer.Serialize(ventureState));
Assert(persistedState is not null && persistedState.PendingResultEvents.Single().EventId == ventureResult.EventId, "pending result evidence must survive a plugin restart");
var venturePayloadJson = JsonSerializer.Serialize(RetainerVentureSnapshotPolicy.BuildPayload(persistedState!, new { name = "Fixture", world = "Test" }));
Assert(venturePayloadJson.Contains("\"rosterComplete\":true", StringComparison.Ordinal)
    && venturePayloadJson.Contains("\"resultEvents\"", StringComparison.Ordinal)
    && !venturePayloadJson.Contains("RosterComplete", StringComparison.Ordinal), "the proposed payload must be deterministic and camel-case compatible");
RetainerVentureSnapshotPolicy.AcknowledgeResults(persistedState!, [ventureResult.EventId]);
Assert(persistedState!.PendingResultEvents.Count == 0, "acknowledged result evidence must leave the retry queue");
var olderVentureState = JsonSerializer.Deserialize<RetainerVentureLocalState>("{}");
Assert(olderVentureState is not null && !olderVentureState.RosterComplete && olderVentureState.Retainers.Count == 0 && olderVentureState.PendingResultEvents.Count == 0,
    "older configuration payloads without venture state must remain compatible");
Console.WriteLine("retainer venture snapshot and retry tests passed");

var managedPlan = new GillionsVenturePlanSpec("100", "Fixture-retainer", [new(245, 3), new(112, 1)]);
Assert(VenturePlannerCapabilityPolicy.IsValid(managedPlan), "a bounded Gillions venture plan must be valid");
Assert(!VenturePlannerCapabilityPolicy.IsValid(managedPlan with { RetainerId = "invalid" }), "a malformed retainer identity must be rejected");
Assert(!VenturePlannerCapabilityPolicy.IsValid(managedPlan with { Steps = [new(0, 1)] }), "a zero venture ID must be rejected");
Assert(!VenturePlannerCapabilityPolicy.IsAvailable(false, true, true, true, true), "venture planning must remain explicitly opted out by default");
Assert(!VenturePlannerCapabilityPolicy.IsAvailable(true, false, true, true, true), "AutoRetainer absence must disable venture planning");
Assert(VenturePlannerCapabilityPolicy.IsAvailable(true, true, true, true, true), "the complete opted-in capability must be available");
var fakeAdditionalData = new FakeAdditionalRetainerData();
fakeAdditionalData.VenturePlan.List.Add(new FakePlannedVenture { ID = 999, Num = 9 });
var originalPlan = AutoRetainerVenturePlanMutation.Capture(fakeAdditionalData);
AutoRetainerVenturePlanMutation.Apply(fakeAdditionalData, managedPlan);
Assert(fakeAdditionalData.EnablePlanner && fakeAdditionalData.LinkedVenturePlan == "" && fakeAdditionalData.VenturePlanIndex == 0, "the embedded planner must be enabled without linking a global plan");
Assert(fakeAdditionalData.VenturePlan.Name == "Gillions Venture (Fixture-retainer)", "the managed plan must use the Gillions retainer-specific name");
Assert(fakeAdditionalData.VenturePlan.List.Select(entry => (entry.ID, entry.Num)).SequenceEqual(new[] { (245u, 3), (112u, 1) }), "the managed plan must replace only the embedded venture sequence");
Assert(fakeAdditionalData.Deposit, "unrelated AutoRetainer settings must be preserved");
AutoRetainerVenturePlanMutation.Restore(fakeAdditionalData, originalPlan);
Assert(!fakeAdditionalData.EnablePlanner && fakeAdditionalData.LinkedVenturePlan == "saved-plan" && fakeAdditionalData.VenturePlanIndex == 7, "the prior planner linkage and enabled state must be restorable");
Assert(fakeAdditionalData.VenturePlan.Name == "Existing plan" && fakeAdditionalData.VenturePlan.List.Single().ID == 999, "the prior embedded plan must be restorable");
Console.WriteLine("AutoRetainer venture-plan policy tests passed");

Assert(!NativeItemLinkFactory.IsValidItemId(0), "zero item ID must be rejected");
Assert(!NativeItemLinkFactory.IsValidItemId(-1), "negative item ID must be rejected");
Assert(!NativeItemLinkFactory.IsValidItemId((long)uint.MaxValue + 1), "out-of-range item ID must be rejected");
Assert(NativeItemLinkFactory.IsValidItemId(4555), "positive uint item ID must pass range validation");

Console.WriteLine("item-id validation passed");
var nativeLink = NativeItemLinkFactory.Create(4555, "Ether");
Assert(nativeLink.Payloads.OfType<ItemPayload>().Single().ItemId == 4555, "native link must contain the requested ItemPayload");
Assert(nativeLink.TextValue.Contains("Ether", StringComparison.Ordinal), "native link must display the authoritative item name");
Console.WriteLine("native-link construction passed");

var now = DateTime.UtcNow;
Assert(ItemLinkPollPolicy.ShouldPoll(true, true, "device-token", false, now, now), "a linked, logged-in idle plugin should poll");
Assert(!ItemLinkPollPolicy.ShouldPoll(false, true, "device-token", false, now, now), "a disabled handler must not poll");
Assert(!ItemLinkPollPolicy.ShouldPoll(true, false, "device-token", false, now, now), "an offline plugin must not poll");
Assert(!ItemLinkPollPolicy.ShouldPoll(true, true, "", false, now, now), "an unlinked plugin must not poll");
Assert(!ItemLinkPollPolicy.ShouldPoll(true, true, "device-token", true, now, now), "an in-flight poll must not overlap");

var processor = new ItemLinkRequestProcessor();
var sequence = new List<string>();
var printed = 0;
var success = await processor.ProcessAsync(
    Request(expires: now.AddMinutes(1)), now,
    _ => Task.FromResult<string?>("Ether"),
    _ => { sequence.Add("consume"); return Task.FromResult(true); },
    _ => { sequence.Add("print"); printed++; return Task.CompletedTask; });
Assert(success == ItemLinkDeliveryResult.Delivered && printed == 1, "a valid claimed request must print once");
Assert(sequence.SequenceEqual(["consume", "print"]), "the server claim must be consumed before printing");

var replay = await processor.ProcessAsync(
    Request(expires: now.AddMinutes(1)), now,
    _ => Task.FromResult<string?>("Ether"),
    _ => Task.FromResult(true),
    _ => { printed++; return Task.CompletedTask; });
Assert(replay == ItemLinkDeliveryResult.AlreadyDelivered && printed == 1, "a consumed request must not replay");

var expiredProcessor = new ItemLinkRequestProcessor();
var expiredTouchedTransport = false;
var expired = await expiredProcessor.ProcessAsync(
    Request(expires: now.AddSeconds(-1)), now,
    _ => { expiredTouchedTransport = true; return Task.FromResult<string?>("Ether"); },
    _ => { expiredTouchedTransport = true; return Task.FromResult(true); },
    _ => { expiredTouchedTransport = true; return Task.CompletedTask; });
Assert(expired == ItemLinkDeliveryResult.Expired && !expiredTouchedTransport, "expired requests must be rejected before item or transport work");

var invalid = await new ItemLinkRequestProcessor().ProcessAsync(
    Request(itemId: 0, expires: now.AddMinutes(1)), now,
    _ => Task.FromResult<string?>("Invalid"),
    _ => Task.FromResult(true),
    _ => Task.CompletedTask);
Assert(invalid == ItemLinkDeliveryResult.InvalidRequest, "invalid item IDs must be rejected");

var missingCatalogItem = await new ItemLinkRequestProcessor().ProcessAsync(
    Request("missing-item", 999999, now.AddMinutes(1)), now,
    _ => Task.FromResult<string?>(null),
    _ => Task.FromResult(true),
    _ => Task.CompletedTask);
Assert(missingCatalogItem == ItemLinkDeliveryResult.InvalidItem, "IDs absent from the Lumina catalog must be rejected");

var isolatedPrinted = false;
var accountIsolation = await new ItemLinkRequestProcessor().ProcessAsync(
    Request("wrong-account", 4555, now.AddMinutes(1), "unauthorized-claim"), now,
    _ => Task.FromResult<string?>("Ether"),
    _ => Task.FromResult(false),
    _ => { isolatedPrinted = true; return Task.CompletedTask; });
Assert(accountIsolation == ItemLinkDeliveryResult.ConsumeRejected && !isolatedPrinted, "an unauthorized account claim must never print");

Console.WriteLine("Gillions item-link protocol tests passed.");

public sealed class FakeAdditionalRetainerData {
    public bool Deposit = true;
    public FakeVenturePlan VenturePlan = new();
    public string LinkedVenturePlan = "saved-plan";
    public uint VenturePlanIndex = 7;
    public bool EnablePlanner;
}

public sealed class FakeVenturePlan {
    public string Name = "Existing plan";
    public List<FakePlannedVenture> List = [];
}

public sealed class FakePlannedVenture {
    public uint ID;
    public int Num = 1;
}
