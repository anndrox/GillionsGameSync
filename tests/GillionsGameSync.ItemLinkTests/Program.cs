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
Assert(ProgressionSnapshotPolicy.NormalizeSharedFateMaximumRank(0, 0) == 3
    && ProgressionSnapshotPolicy.NormalizeSharedFateMaximumRank(1, 0) == 3
    && ProgressionSnapshotPolicy.NormalizeSharedFateMaximumRank(2, 0) == 4,
    "native zero Shared FATE maximum-rank sentinels must use the established per-tab game caps");
Assert(ProgressionSnapshotPolicy.NormalizeSharedFateMaximumRank(2, 5) == 5,
    "a future nonzero native Shared FATE maximum rank must remain authoritative");
Assert(ProgressionSnapshotPolicy.NormalizeSharedFateMaximumRank(3, 0) == 0,
    "an unknown tab must not invent a Shared FATE maximum rank");
var completeTabs = Enumerable.Range(0, 3).Select(tabIndex => new SharedFateTabProgress((byte)tabIndex,
    Enumerable.Range(0, 6).Select(zoneIndex => new SharedFateZoneProgress((uint)(1000 + tabIndex * 10 + zoneIndex), 2, 3, 20, 60)).ToArray())).ToArray();
Assert(ProgressionSnapshotPolicy.IsCompleteSharedFateSnapshot(completeTabs), "three valid six-zone Shared FATE tabs must be complete");
var canonicalTabs = ProgressionSnapshotPolicy.BuildCompleteSharedFateSnapshot(completeTabs.Select(tab => (IReadOnlyCollection<SharedFateZoneProgress>)tab.Zones));
Assert(canonicalTabs is not null && canonicalTabs.Select(tab => tab.TabIndex).SequenceEqual(new byte[] { 0, 1, 2 }),
    "the native fixed-array display order must become the zero-based server tab contract");
Assert(!ProgressionSnapshotPolicy.IsCompleteSharedFateSnapshot(completeTabs.Take(2)), "a partial Shared FATE tab set must not be uploaded as complete");
Assert(ProgressionSnapshotPolicy.BuildCompleteSharedFateSnapshot(completeTabs.Take(2).Select(tab => (IReadOnlyCollection<SharedFateZoneProgress>)tab.Zones)) is null,
    "canonicalization must not turn a partial native tab set into a complete snapshot");
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
var characterStates = new Dictionary<string, RetainerVentureLocalState>(StringComparer.Ordinal);
var ventureState = RetainerVentureSnapshotPolicy.GetCharacterState(characterStates, 111);
var otherCharacterState = RetainerVentureSnapshotPolicy.GetCharacterState(characterStates, 222);
Assert(ventureState != otherCharacterState && ventureState.CharacterContentId == "111" && otherCharacterState.CharacterContentId == "222",
    "retainer state must be partitioned by character content ID");
Assert(RetainerVentureSnapshotPolicy.MergeRoster(ventureState, null, ventureNow)
    && ventureState.RosterObservation.Status == "unavailable", "an unavailable roster must be explicit and non-authoritative");
Assert(RetainerVentureSnapshotPolicy.MergeRoster(ventureState,
    new RetainerVentureRosterRead(ventureNow, true, [
        new("200", "Ready", 0, 0, 22, readyCompleteUnix, 2000),
        new("100", "Active", 18, 90, 11, activeCompleteUnix, 1000),
    ]), ventureNow), "a complete native roster must be accepted");
Assert(ventureState.Retainers.Select(entry => entry.RetainerId).SequenceEqual(["100", "200"]), "retainers must be stable and sorted");
Assert(ventureState.Retainers[0].ClassJobId == 18 && ventureState.Retainers[0].Level == 90, "known class/job and level values must be preserved");
Assert(ventureState.Retainers[1].ClassJobId is null && ventureState.Retainers[1].Level is null, "zero class/job and level values must remain unknown");
Assert(ventureState.Retainers[0].Venture.Assignment?.VentureId == 11, "an active venture must retain its stable task ID");
Assert(ventureState.Retainers[0].Gil.Single().Value == 1000, "native roster gil must carry current provenance");
var unchangedChangedAt = ventureState.RosterObservation.LastChangedAtUtc;
Assert(RetainerVentureSnapshotPolicy.MergeRoster(ventureState,
    new RetainerVentureRosterRead(ventureNow.AddSeconds(5), true, [
        new("200", "Ready", 0, 0, 22, readyCompleteUnix, 2000),
        new("100", "Active", 18, 90, 11, activeCompleteUnix, 1000),
    ]), ventureNow.AddSeconds(5)), "an unchanged roster must refresh observation evidence");
Assert(ventureState.RosterObservation.LastObservedAtUtc == ventureNow.AddSeconds(5)
    && ventureState.RosterObservation.LastChangedAtUtc == unchangedChangedAt,
    "unchanged data must refresh lastObservedAtUtc without changing lastChangedAtUtc");
Assert(ventureState.Retainers.All(entry => entry.Equipment.Observation.Status == "unavailable" && entry.Equipment.Items is null),
    "gear must remain explicitly unavailable before the active retainer inventory loads");
Assert(RetainerVentureSnapshotPolicy.MergeGear(ventureState,
    new RetainerVentureGearRead("100", ventureNow.AddMinutes(1), [new(5, 400, true), new(1, 300, false)])), "loaded native retainer gear must be accepted");
Assert(ventureState.Retainers[0].Equipment.Observation.Status == "complete"
    && ventureState.Retainers[0].Equipment.Items!.Select(item => item.SlotIndex).SequenceEqual([1, 5]),
    "observed gear must be sorted and marked complete for that loaded retainer");
Assert(!RetainerVentureSnapshotPolicy.MergeGear(ventureState, null), "an unloaded gear container must preserve the last observation");
Assert(RetainerVentureSnapshotPolicy.MergeAutoRetainerStats(ventureState,
    [new("100", ventureNow.AddMinutes(1), 650, null, null, ventureNow.AddMinutes(-55))], ventureNow.AddMinutes(1)),
    "AutoRetainer-cached stats must be accepted with explicit provenance");
Assert(ventureState.Retainers[0].Stats.ItemLevel == 650
    && ventureState.Retainers[0].Stats.Observation.Provenance == "autoretainer_cached"
    && ventureState.Retainers[0].Venture.Assignment?.BeginAt?.Provenance == "autoretainer_cached",
    "cached item level and venture start must not be represented as native-current data");
RetainerVentureSnapshotPolicy.MergeAutoRetainerStats(ventureState, [], ventureNow.AddMinutes(2));
Assert(ventureState.Retainers[0].Stats.ItemLevel == 650
    && ventureState.Retainers[0].Stats.Observation.Provenance == "retained_historical"
    && ventureState.Retainers[0].Stats.Observation.RetainedData,
    "unavailable cached stats must retain prior values as historical evidence");
Assert(RetainerVentureSnapshotPolicy.MergeInventorySources(ventureState, [
    new("character_inventory", null, ventureNow, [new("Inventory1", true, 2, 35), new("Inventory2", false, 0, 0)]),
    new("retainer_inventory", "100", ventureNow, [new("RetainerPage1", true, 1, 25)]),
    new("retainer_inventory", "200", ventureNow, [new("RetainerPage1", false, 0, 0)]),
]), "inventory source coverage must be stored");
Assert(ventureState.InventorySources.Single(entry => entry.Source == "character_inventory").Observation.Status == "partial"
    && ventureState.InventorySources.Single(entry => entry.RetainerId == "100").Observation.Status == "complete"
    && ventureState.InventorySources.Single(entry => entry.RetainerId == "200").Observation.Status == "unavailable",
    "inventory coverage must distinguish partial, complete, and unavailable sources");
Assert(RetainerVentureSnapshotPolicy.MergeRoster(ventureState,
    new RetainerVentureRosterRead(ventureNow.AddMinutes(2), true, [
        new("200", "Ready", 0, 0, 0, 0, 2000),
        new("100", "Active", 18, 90, 33, readyCompleteUnix, 1000),
    ]), ventureNow.AddMinutes(2)), "a changed assignment must update the venture observation");
Assert(ventureState.Retainers[0].Venture.Assignment?.VentureId == 33, "replacement venture state must be represented");
Assert(ventureState.Retainers[1].Venture.Observation.Status == "authoritative_empty" && ventureState.Retainers[1].Venture.Assignment is null,
    "a positive zero-task roster observation must represent authoritative idle");
Assert(RetainerVentureSnapshotPolicy.ResolveResultCompletionUnix(ventureState, "100", 33, 0) == readyCompleteUnix,
    "a matching last-known positive assignment must recover completion evidence after the active native field clears");
Assert(RetainerVentureSnapshotPolicy.ResolveResultCompletionUnix(ventureState, "100", 33, activeCompleteUnix) == activeCompleteUnix,
    "valid native completion evidence must take precedence over local fallback state");
Assert(RetainerVentureSnapshotPolicy.ResolveResultCompletionUnix(ventureState, "100", 999, 0) == 0,
    "a prior completion timestamp must not be reused for a different venture");

var resultRead = new RetainerVentureResultRead("100", 33, readyCompleteUnix, ventureNow.AddMinutes(3), 1234,
    [new(500, 2), new(400, 1)]);
var ventureResult = RetainerVentureSnapshotPolicy.CreateResultEvent(ventureState, resultRead);
Assert(ventureResult is not null && ventureResult.Items.Select(item => item.ItemId).SequenceEqual(new uint[] { 400, 500 }), "a structured result must preserve and sort both native reward items");
var revisedEvidence = RetainerVentureSnapshotPolicy.CreateResultEvent(ventureState, resultRead with { AwardedExperience = 2222, Items = [new(600, 1)] });
Assert(ventureResult!.EventId == revisedEvidence!.EventId && ventureResult.PayloadFingerprint != revisedEvidence.PayloadFingerprint,
    "reward contents and XP must change fingerprint evidence without changing durable event identity");
Assert(RetainerVentureSnapshotPolicy.AddPendingResult(ventureState, ventureResult), "a new result must enter the bounded retry queue");
Assert(!RetainerVentureSnapshotPolicy.AddPendingResult(ventureState, ventureResult), "a duplicate result must not replay into the queue");
Assert(RetainerVentureSnapshotPolicy.AddPendingResult(ventureState, revisedEvidence)
    && ventureState.PendingResultEvents.Single().PayloadFingerprint == revisedEvidence.PayloadFingerprint,
    "new evidence for the same event must replace the pending payload without duplicating logical identity");
Assert(RetainerVentureSnapshotPolicy.CreateResultEvent(ventureState, resultRead with { VentureCompleteUnix = 0 }) is null,
    "a result without authoritative completion evidence must be rejected");

var persistedState = JsonSerializer.Deserialize<RetainerVentureLocalState>(JsonSerializer.Serialize(ventureState));
Assert(persistedState is not null && persistedState.PendingResultEvents.Single().EventId == ventureResult.EventId, "pending result evidence must survive a plugin restart");
var venturePayloadJson = JsonSerializer.Serialize(RetainerVentureSnapshotPolicy.BuildPayload(persistedState!, new { contentId = "111", name = "Fixture", world = "Test" }));
Assert(venturePayloadJson.Contains("\"rosterObservation\"", StringComparison.Ordinal)
    && venturePayloadJson.Contains("\"resultEvents\"", StringComparison.Ordinal)
    && venturePayloadJson.Contains("\"inventorySources\"", StringComparison.Ordinal), "the payload must match the v1 evidence contract");
var acknowledgement = JsonSerializer.Serialize(new { ok = true, resourceType = "retainer_ventures", schemaVersion = 1, snapshotAccepted = true,
    acceptedEventIds = new[] { ventureResult.EventId }, serverTimeUtc = ventureNow });
Assert(RetainerAcknowledgementPolicy.TryParseExact(acknowledgement, [ventureResult.EventId], out var acceptedIds)
    && acceptedIds.SequenceEqual([ventureResult.EventId]), "an exact acknowledgement must accept only the requested event ID");
Assert(RetainerAcknowledgementPolicy.TryParseExact(acknowledgement.Replace(ventureResult.EventId, new string('a', 64)), [ventureResult.EventId], out _) == false,
    "an acknowledgement containing an unrelated ID must delete nothing");
Assert(!RetainerAcknowledgementPolicy.TryParseExact("{}", [ventureResult.EventId], out _), "a malformed 2xx body must delete nothing");
Assert(!RetainerAcknowledgementPolicy.TryParseExact(acknowledgement.Replace("retainer_ventures", "inventory"), [ventureResult.EventId], out _),
    "a wrong-resource acknowledgement must delete nothing");
var explicitEmpty = JsonSerializer.Serialize(new { ok = true, resourceType = "retainer_ventures", schemaVersion = 1, snapshotAccepted = true,
    acceptedEventIds = Array.Empty<string>(), serverTimeUtc = ventureNow });
Assert(RetainerAcknowledgementPolicy.TryParseExact(explicitEmpty, [ventureResult.EventId], out var noAccepted) && noAccepted.Length == 0,
    "an empty acknowledgement must preserve every pending event");
RetainerVentureSnapshotPolicy.AcknowledgeResults(persistedState!, acceptedIds);
Assert(persistedState!.PendingResultEvents.Count == 0, "only an exactly acknowledged result may leave the retry queue");
var olderVentureState = JsonSerializer.Deserialize<RetainerVentureLocalState>("{}");
Assert(olderVentureState is not null && olderVentureState.RosterObservation.Status == "unavailable"
    && olderVentureState.Retainers.Count == 0 && olderVentureState.PendingResultEvents.Count == 0,
    "older configuration payloads without venture state must remain compatible");
var emptyState = RetainerVentureSnapshotPolicy.GetCharacterState(characterStates, 333);
RetainerVentureSnapshotPolicy.MergeRoster(emptyState, new(ventureNow, true, []), ventureNow);
Assert(emptyState.RosterObservation.Status == "authoritative_empty" && emptyState.Retainers.Count == 0,
    "a positively observed empty roster must be authoritative empty");
Assert(RetainerPresencePolicy.NextSuccessDelay(5) == TimeSpan.FromSeconds(35)
    && RetainerPresencePolicy.NextFailureDelay(20, 5) == TimeSpan.FromSeconds(300),
    "presence cadence must use bounded jitter and capped exponential backoff");
var presenceResponse = JsonSerializer.Serialize(new { ok = true, schemaVersion = 1, serverTimeUtc = ventureNow,
    recommendedHeartbeatSeconds = 30, onlineWindowSeconds = 90, maximumBackoffSeconds = 300,
    featureCompatibility = new { observations = "supported", results = "supported", planner = "server_disabled" } });
Assert(RetainerPresenceResponsePolicy.TryParse(presenceResponse, RetainerClientPolicy.Testing, out var uploadSupported, out var plannerSupported) && uploadSupported && !plannerSupported,
    "a compatible testing presence response must enable only the scoped upload path");
Assert(!RetainerPresenceResponsePolicy.TryParse(presenceResponse, RetainerClientPolicy.Stable, out _, out _),
    "an older server response must not enable stable Retainer traffic without explicit product acceptance");
var stablePresenceResponse = JsonSerializer.Serialize(new { ok = true, schemaVersion = 1, serverTimeUtc = ventureNow,
    acceptedClientProduct = RetainerClientPolicy.Stable.ProductName,
    acceptedContractVersion = RetainerClientPolicy.ContractVersion,
    recommendedHeartbeatSeconds = 30, onlineWindowSeconds = 90, maximumBackoffSeconds = 300,
    featureCompatibility = new { observations = "supported", results = "supported", planner = "supported" } });
Assert(RetainerPresenceResponsePolicy.TryParse(stablePresenceResponse, RetainerClientPolicy.Stable, out var stableUploadSupported, out var stablePlannerSupported)
    && stableUploadSupported && stablePlannerSupported,
    "stable Retainer behavior must require an exact server product and contract acknowledgement");
Assert(!RetainerPresenceResponsePolicy.TryParse(stablePresenceResponse.Replace(RetainerClientPolicy.Stable.ProductName, RetainerClientPolicy.Testing.ProductName, StringComparison.Ordinal),
    RetainerClientPolicy.Stable, out _, out _), "a response for the testing product must not activate the stable product");
Assert(RetainerClientPolicy.Stable.ProductName != RetainerClientPolicy.Testing.ProductName
    && RetainerClientPolicy.Stable.Channel == "stable" && RetainerClientPolicy.Testing.Channel == "testing",
    "stable and testing products must retain distinct identities while sharing contract v1");
var ordinaryScopes = new[] { "inventory", "collectibles" };
Assert(RetainerClientPolicy.BuildSyncScopes(ordinaryScopes, false).SequenceEqual(ordinaryScopes),
    "an older server without Retainer support must preserve ordinary Game Sync scopes unchanged");
Assert(RetainerClientPolicy.BuildSyncScopes(ordinaryScopes, true).SequenceEqual(new[] { "inventory", "collectibles", "retainer_ventures" }),
    "Retainer observation must activate only after the server accepts the client");
Assert(!RetainerClientPolicy.ShouldPollPlans(false, true, true, true, true), "server plan acceptance is mandatory");
Assert(!RetainerClientPolicy.ShouldPollPlans(true, false, true, true, true), "planner opt-in must default to disabled");
Assert(!RetainerClientPolicy.ShouldPollPlans(true, true, false, true, true), "AutoRetainer absence must disable plan polling");
Assert(!RetainerClientPolicy.ShouldPollPlans(true, true, true, false, true), "an unavailable AutoRetainer API must disable plan polling");
Assert(RetainerClientPolicy.ShouldPollPlans(true, true, true, true, true), "Multi Mode must not be a prerequisite for an otherwise eligible plan poll");
Console.WriteLine("retainer venture snapshot and retry tests passed");

var managedPlan = new GillionsVenturePlanSpec("100", "Fixture-retainer", [new(245, 3), new(112, 1)], "do_nothing");
Assert(VenturePlannerCapabilityPolicy.IsValid(managedPlan), "a bounded Gillions venture plan must be valid");
Assert(!VenturePlannerCapabilityPolicy.IsValid(managedPlan with { RetainerId = "invalid" }), "a malformed retainer identity must be rejected");
Assert(!VenturePlannerCapabilityPolicy.IsValid(managedPlan with { Steps = [new(0, 1)] }), "a zero venture ID must be rejected");
Assert(VenturePlannerCapabilityPolicy.IsValid(managedPlan with { Steps = [new(245, 24)] }), "exactly 24 pending executions must be accepted");
Assert(!VenturePlannerCapabilityPolicy.IsValid(managedPlan with { Steps = [new(245, 24), new(112, 1)] }), "a 25th pending execution must be rejected client-side");
Assert(!VenturePlannerCapabilityPolicy.IsValid(managedPlan with { CompletionBehavior = "repeat_last_venture" }), "unbounded completion behavior must be rejected");
Assert(!VenturePlannerCapabilityPolicy.IsAvailable(false, true, true, true, true), "venture planning must remain explicitly opted out by default");
Assert(!VenturePlannerCapabilityPolicy.IsAvailable(true, false, true, true, true), "AutoRetainer absence must disable venture planning");
Assert(VenturePlannerCapabilityPolicy.IsAvailable(true, true, true, true, true), "the complete opted-in capability must be available");
var ownedHash = new string('a', 64);
var conflictHash = new string('b', 64);
var changedAgainHash = new string('c', 64);
Assert(AutoRetainerOwnedPlanPolicy.Decide(ownedHash, ownedHash, ownedHash, false) == AutoRetainerOwnedPlanDecision.Apply,
    "a normal later revision must compare-and-set from the last owned hash");
Assert(AutoRetainerOwnedPlanPolicy.Decide(conflictHash, ownedHash, conflictHash, false) == AutoRetainerOwnedPlanDecision.Apply,
    "a deliberate retry may compare-and-set from the exact server-delivered conflict observation");
Assert(AutoRetainerOwnedPlanPolicy.Decide(conflictHash, ownedHash, changedAgainHash, false) == AutoRetainerOwnedPlanDecision.Conflict,
    "another local edit after the conflict observation must fail closed");
Assert(AutoRetainerOwnedPlanPolicy.Decide(ownedHash, changedAgainHash, changedAgainHash, true) == AutoRetainerOwnedPlanDecision.Idempotent,
    "an already applied managed plan must remain replay-safe");
Assert(AutoRetainerOwnedPlanPolicy.CanRestore(ownedHash, ownedHash),
    "an unchanged Gillions-managed plan must remain eligible for exact restoration");
Assert(AutoRetainerOwnedPlanPolicy.CanRestore(conflictHash, conflictHash),
    "a deliberate restoration retry must accept the exact externally changed state acknowledged to Gillions");
Assert(!AutoRetainerOwnedPlanPolicy.CanRestore(conflictHash, changedAgainHash),
    "restoration must fail closed if AutoRetainer changes again after the acknowledged retry baseline");
var fakeAdditionalData = new FakeAdditionalRetainerData();
fakeAdditionalData.VenturePlan.List.Add(new FakePlannedVenture { ID = 999, Num = 9 });
var originalPlan = AutoRetainerVenturePlanMutation.Capture(fakeAdditionalData);
AutoRetainerVenturePlanMutation.Apply(fakeAdditionalData, managedPlan);
Assert(fakeAdditionalData.EnablePlanner && fakeAdditionalData.LinkedVenturePlan == "" && fakeAdditionalData.VenturePlanIndex == 0, "the embedded planner must be enabled without linking a global plan");
Assert(fakeAdditionalData.VenturePlan.Name == "Gillions Venture (Fixture-retainer)", "the managed plan must use the Gillions retainer-specific name");
Assert(fakeAdditionalData.VenturePlan.List.Select(entry => (entry.ID, entry.Num)).SequenceEqual(new[] { (245u, 3), (112u, 1) }), "the managed plan must replace only the embedded venture sequence");
Assert(fakeAdditionalData.VenturePlan.PlanCompleteBehavior == FakePlanCompleteBehavior.Do_nothing, "the bounded Do Nothing completion behavior must be applied explicitly");
Assert(fakeAdditionalData.Deposit, "unrelated AutoRetainer settings must be preserved");
AutoRetainerVenturePlanMutation.Restore(fakeAdditionalData, originalPlan);
Assert(!fakeAdditionalData.EnablePlanner && fakeAdditionalData.LinkedVenturePlan == "saved-plan" && fakeAdditionalData.VenturePlanIndex == 7, "the prior planner linkage and enabled state must be restorable");
Assert(fakeAdditionalData.VenturePlan.Name == "Existing plan" && fakeAdditionalData.VenturePlan.List.Single().ID == 999, "the prior embedded plan must be restorable");
Assert(fakeAdditionalData.VenturePlan.PlanCompleteBehavior == FakePlanCompleteBehavior.Restart_plan, "the original unbounded completion behavior must be preserved only in the restorable backup");
Assert(AutoRetainerVenturePlanMutation.Hash(AutoRetainerVenturePlanMutation.Capture(fakeAdditionalData)) == AutoRetainerVenturePlanMutation.Hash(originalPlan), "restoration read-back must match the exact pre-write plan hash");
Assert(AutoRetainerVenturePlanMutation.Hash(originalPlan) == "3bc8171dd8512db86fe7a8b75ffa5bd8053921e3b64c45e12b6ccd780a261ab4", "client and server must share the canonical prior-plan backup hash");
Console.WriteLine("AutoRetainer venture-plan policy tests passed");

var deliveryJson = JsonSerializer.Serialize(new {
    ok = true,
    schemaVersion = 1,
    serverTimeUtc = ventureNow,
    pollAfterSeconds = 15,
    deliveries = new[] { new {
        schemaVersion = 1,
        operation = "apply_projection",
        deliveryId = Guid.NewGuid().ToString(),
        leaseToken = new string('a', 43),
        planId = Guid.NewGuid().ToString(),
        revisionId = Guid.NewGuid().ToString(),
        revisionNumber = 1,
        revisionHash = new string('b', 64),
        projectionGeneration = 1,
        retainerId = "100",
        retainerName = "Fixture-retainer",
        expectedAppliedHash = (string?)null,
        completionBehavior = "assign_quick_venture",
        steps = new[] { new { ventureId = 245u, repetitions = 24 } },
        priorPlanBackupHash = (string?)null,
        priorPlanBackup = (object?)null,
        requiredCapabilities = RetainerCapabilities.Client,
        createdAtUtc = ventureNow,
        expiresAtUtc = ventureNow.AddMinutes(1),
    } }
});
Assert(RetainerPlanDeliveryPolicy.TryParse(deliveryJson, ventureNow, out var parsedDelivery)
    && parsedDelivery?.Deliveries.Single().CompletionBehavior == "assign_quick_venture",
    "a current, bounded, fully capable testing delivery must parse");
var oversizedDeliveryJson = deliveryJson.Replace("\"repetitions\":24", "\"repetitions\":25", StringComparison.Ordinal);
Assert(!RetainerPlanDeliveryPolicy.TryParse(oversizedDeliveryJson, ventureNow, out _), "a 25-execution delivery must be rejected before AutoRetainer access");
Assert(!RetainerPlanDeliveryPolicy.TryParse(deliveryJson, ventureNow.AddMinutes(2), out _), "an expired delivery lease must be rejected before AutoRetainer access");
Assert(!RetainerPlanDeliveryPolicy.TryParse(deliveryJson.Replace("\"expectedAppliedHash\":null", "\"expectedAppliedHash\":\"bad\"", StringComparison.Ordinal), ventureNow, out _),
    "a mismatched compare-and-set hash shape must be rejected before AutoRetainer access");
var duplicateRetainerResponse = parsedDelivery! with {
    Deliveries = [
        parsedDelivery.Deliveries.Single(),
        parsedDelivery.Deliveries.Single() with { DeliveryId = Guid.NewGuid().ToString(), RevisionId = Guid.NewGuid().ToString() },
    ],
};
Assert(!RetainerPlanDeliveryPolicy.TryParse(JsonSerializer.Serialize(duplicateRetainerResponse), ventureNow, out _),
    "multiple revisions for one Retainer in a poll must be rejected rather than applied sequentially");
var ownershipFixtures = new Dictionary<string, AutoRetainerPlanOwnershipState>(StringComparer.Ordinal) {
    [RetainerPlanDeliveryPolicy.OwnershipKey(111, "100")] = new("device", "100", Guid.NewGuid().ToString(), 1, Guid.NewGuid().ToString(), new string('c', 64), AutoRetainerVenturePlanMutation.Hash(originalPlan), originalPlan),
    [RetainerPlanDeliveryPolicy.OwnershipKey(222, "100")] = new("device", "100", Guid.NewGuid().ToString(), 1, Guid.NewGuid().ToString(), new string('d', 64), AutoRetainerVenturePlanMutation.Hash(originalPlan), originalPlan),
};
Assert(RetainerPlanDeliveryPolicy.AppliedPlansForCharacter(ownershipFixtures, 111).Single().RetainerId == "100",
    "presence must report only the active character's locally applied plan state");
Assert(RetainerPlanDeliveryPolicy.IsCurrentCharacter(111, 111) && !RetainerPlanDeliveryPolicy.IsCurrentCharacter(111, 222),
    "a delivery captured for another active character must be rejected");
Assert(RetainerPlanDeliveryPolicy.ResolveRetainer(ventureState.Retainers, "100")?.Name == "Active"
    && RetainerPlanDeliveryPolicy.ResolveRetainer(ventureState.Retainers, "999") is null,
    "a delivery for a foreign Retainer ID must be rejected instead of falling back to a name or list position");
Assert(RetainerPlanDeliveryPolicy.IsOwnedByDevice(null, "device")
    && RetainerPlanDeliveryPolicy.IsOwnedByDevice(ownershipFixtures[RetainerPlanDeliveryPolicy.OwnershipKey(111, "100")], "device")
    && !RetainerPlanDeliveryPolicy.IsOwnedByDevice(ownershipFixtures[RetainerPlanDeliveryPolicy.OwnershipKey(111, "100")], "foreign-device"),
    "existing Gillions plan ownership must remain pinned to its original device");
var trackedRevision = ownershipFixtures[RetainerPlanDeliveryPolicy.OwnershipKey(111, "100")] with {
    RevisionId = Guid.NewGuid().ToString(),
    RevisionNumber = 2,
    ProjectionGeneration = 1,
};
var deliveryFixture = parsedDelivery.Deliveries.Single();
Assert(RetainerPlanDeliveryPolicy.IsLatestDelivery(trackedRevision with { RevisionNumber = 0 }, deliveryFixture),
    "ownership persisted by an older client without a revision number must accept one valid delivery to upgrade its state");
Assert(!RetainerPlanDeliveryPolicy.IsLatestDelivery(trackedRevision, deliveryFixture with { RevisionNumber = 1 }),
    "an older immutable revision must be rejected even if it arrives with a valid lease");
Assert(RetainerPlanDeliveryPolicy.IsLatestDelivery(trackedRevision, deliveryFixture with { RevisionNumber = 3 }),
    "a newer deliberately requested immutable revision may advance the owned plan");
Assert(RetainerPlanDeliveryPolicy.IsLatestDelivery(trackedRevision, deliveryFixture with {
    RevisionId = trackedRevision.RevisionId,
    RevisionNumber = trackedRevision.RevisionNumber,
    ProjectionGeneration = trackedRevision.ProjectionGeneration + 1,
}), "a newer server projection of the current revision may advance without becoming a different revision");
Console.WriteLine("Retainer plan delivery contract tests passed");

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
    public FakePlanCompleteBehavior PlanCompleteBehavior = FakePlanCompleteBehavior.Restart_plan;
}

public enum FakePlanCompleteBehavior { Restart_plan, Assign_Quick_Venture, Do_nothing, Repeat_last_venture }

public sealed class FakePlannedVenture {
    public uint ID;
    public int Num = 1;
}
