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

var unpairedStartupHydration = new PairedClientHydrationState();
unpairedStartupHydration.PluginStarted(hasDeviceCredential: false);
Assert(!unpairedStartupHydration.CharacterSyncPending && !unpairedStartupHydration.PresencePending,
    "an unpaired startup must not schedule character hydration or presence");

var postPairHydration = new PairedClientHydrationState();
postPairHydration.PairingSucceeded();
Assert(postPairHydration.CharacterSyncPending && postPairHydration.PresencePending,
    "successful pairing must schedule immediate character hydration and presence");
Assert(!postPairHydration.TryBeginCharacterSync(syncInFlight: true) && postPairHydration.CharacterSyncPending,
    "post-pair hydration must remain queued while another sync is active");
Assert(postPairHydration.TryBeginCharacterSync(syncInFlight: false)
    && !postPairHydration.CharacterSyncPending,
    "the first available framework update must claim the queued character hydration exactly once");
Assert(!postPairHydration.TryBeginCharacterSync(syncInFlight: false),
    "post-pair character hydration must not become a second recurring sync loop");
Assert(postPairHydration.TryBeginPresence(presenceInFlight: false) && !postPairHydration.PresencePending,
    "successful pairing must claim its immediate presence exactly once");
Assert(!postPairHydration.TryBeginPresence(presenceInFlight: false),
    "post-pair presence must not become a second recurring heartbeat loop");

var pairedStartupHydration = new PairedClientHydrationState();
pairedStartupHydration.PluginStarted(hasDeviceCredential: true);
Assert(pairedStartupHydration.CharacterSyncPending && pairedStartupHydration.PresencePending,
    "an already-paired startup or reload must schedule immediate character hydration and presence");
Assert(pairedStartupHydration.TryBeginCharacterSync(syncInFlight: false)
    && pairedStartupHydration.TryBeginPresence(presenceInFlight: false),
    "an already-paired startup must allow each one-time action to begin");
Assert(!pairedStartupHydration.TryBeginCharacterSync(syncInFlight: false)
    && !pairedStartupHydration.TryBeginPresence(presenceInFlight: false),
    "an already-paired startup must not repeat either action");
Assert(PairedClientHydrationState.CharacterResource == "character",
    "post-pair hydration must upload only the authoritative character identity resource");
Console.WriteLine("paired-client character hydration tests passed");

Assert(ProgressionSnapshotPolicy.NormalizeAlliedSocietyRank(0x87) == 7, "the rank-increased-today flag must not inflate allied-society rank");
Assert(ProgressionSnapshotPolicy.NormalizeSharedFateMaximumRank(0, 0) == 3
    && ProgressionSnapshotPolicy.NormalizeSharedFateMaximumRank(1, 0) == 3
    && ProgressionSnapshotPolicy.NormalizeSharedFateMaximumRank(2, 0) == 4,
    "native zero Shared FATE maximum-rank sentinels must use the established per-tab game caps");
Assert(ProgressionSnapshotPolicy.NormalizeSharedFateMaximumRank(0, byte.MaxValue) == 3
    && ProgressionSnapshotPolicy.NormalizeSharedFateMaximumRank(1, byte.MaxValue) == 3
    && ProgressionSnapshotPolicy.NormalizeSharedFateMaximumRank(2, byte.MaxValue) == 4,
    "native out-of-contract Shared FATE maximum-rank sentinels must use the established per-tab game caps");
Assert(ProgressionSnapshotPolicy.NormalizeSharedFateMaximumRank(0, 75) == 3
    && ProgressionSnapshotPolicy.NormalizeSharedFateMaximumRank(1, 33) == 3
    && ProgressionSnapshotPolicy.NormalizeSharedFateMaximumRank(2, 5) == 4,
    "native Shared FATE bytes must not override the fixed game cap for a known tab");
Assert(ProgressionSnapshotPolicy.NormalizeSharedFateMaximumRank(3, 5) == 5,
    "a plausible native Shared FATE maximum rank remains authoritative for an unknown future tab");
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
Assert(!RetainerVentureSnapshotPolicy.MergeRoster(ventureState, null, ventureNow)
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
Assert(!RetainerVentureSnapshotPolicy.MergeRoster(ventureState,
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
var cachedObserved = ventureNow.AddMinutes(1);
ventureState.Retainers[0].Stats = new() {
    ItemLevel = 650, Observation = new("partial", cachedObserved, cachedObserved, [], "autoretainer_cached", false),
};
ventureState.Retainers[0].Venture.Assignment = ventureState.Retainers[0].Venture.Assignment! with {
    BeginAt = new(ventureNow.AddMinutes(-55), cachedObserved, "autoretainer_cached"),
};
Assert(RetainerVentureSnapshotPolicy.RetireCachedStats(ventureState), "legacy cached stats must be retired");
Assert(ventureState.Retainers[0].Stats.ItemLevel == 650
    && ventureState.Retainers[0].Stats.Observation is { Status: "unavailable", Provenance: "retained_historical", RetainedData: true }
    && ventureState.Retainers[0].Stats.Observation.LastObservedAtUtc == cachedObserved
    && ventureState.Retainers[0].Venture.Assignment?.BeginAt is { Provenance: "retained_historical" },
    "cached values must survive without a fresh observation timestamp or native provenance");
Assert(!RetainerVentureSnapshotPolicy.RetireCachedStats(ventureState), "retirement must be idempotent");
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
var delayedAckState = new RetainerVentureLocalState { PendingResultEvents = [revisedEvidence!] };
RetainerVentureSnapshotPolicy.AcknowledgeResults(delayedAckState, acceptedIds, new Dictionary<string, string> { [ventureResult.EventId] = ventureResult.PayloadFingerprint });
Assert(delayedAckState.PendingResultEvents.Count == 1,
    "F02 delayed ACK for the original fingerprint must preserve revised evidence");
RetainerVentureSnapshotPolicy.AcknowledgeResults(persistedState!, acceptedIds, new Dictionary<string, string> { [revisedEvidence!.EventId] = revisedEvidence.PayloadFingerprint });
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
Assert(RetainerPresenceResponsePolicy.TryParse(presenceResponse, RetainerClientPolicy.Testing, out var uploadSupported) && uploadSupported,
    "a compatible testing presence response must enable only the scoped upload path");
Assert(!RetainerPresenceResponsePolicy.TryParse(presenceResponse, RetainerClientPolicy.Stable, out _),
    "an older server response must not enable stable Retainer traffic without explicit product acceptance");
var stablePresenceResponse = JsonSerializer.Serialize(new { ok = true, schemaVersion = 1, serverTimeUtc = ventureNow,
    acceptedClientProduct = RetainerClientPolicy.Stable.ProductName,
    acceptedContractVersion = RetainerClientPolicy.ContractVersion,
    recommendedHeartbeatSeconds = 30, onlineWindowSeconds = 90, maximumBackoffSeconds = 300,
    featureCompatibility = new { observations = "supported", results = "supported", planner = "supported" } });
Assert(RetainerPresenceResponsePolicy.TryParse(stablePresenceResponse, RetainerClientPolicy.Stable, out var stableUploadSupported)
    && stableUploadSupported,
    "stable Retainer behavior must require an exact server product and contract acknowledgement");
Assert(!RetainerPresenceResponsePolicy.TryParse(stablePresenceResponse.Replace(RetainerClientPolicy.Stable.ProductName, RetainerClientPolicy.Testing.ProductName, StringComparison.Ordinal),
    RetainerClientPolicy.Stable, out _), "a response for the testing product must not activate the stable product");
Assert(RetainerClientPolicy.Stable.ProductName != RetainerClientPolicy.Testing.ProductName
    && RetainerClientPolicy.Stable.Channel == "stable" && RetainerClientPolicy.Testing.Channel == "testing",
    "stable and testing products must retain distinct identities while sharing contract v1");
Assert(RetainerCapabilities.Client.SequenceEqual(new[] {
    "retainer.observations.v1", "retainer.results.v1", "retainer.results.exact-ack.v1", "retainer.presence.v1"
}), "only read-only Retainer capabilities may be advertised");
foreach (var client in new[] { RetainerClientPolicy.Stable, RetainerClientPolicy.Testing }) {
    var nativePresence = RetainerPresencePolicy.CreateNative(new("111", "Fixture", "Test"), ventureNow, client, "0.0.0");
    using var wire = JsonDocument.Parse(JsonSerializer.Serialize(nativePresence));
    var auto = wire.RootElement.GetProperty("autoRetainer");
    Assert(!auto.GetProperty("installed").GetBoolean() && !auto.GetProperty("loaded").GetBoolean()
        && !auto.GetProperty("apiReady").GetBoolean() && !auto.GetProperty("plannerOptIn").GetBoolean()
        && auto.GetProperty("version").ValueKind == JsonValueKind.Null
        && auto.GetProperty("retainerPlannerReadiness").GetArrayLength() == 0
        && auto.GetProperty("capabilities").GetArrayLength() == 0
        && wire.RootElement.GetProperty("appliedPlans").GetArrayLength() == 0,
        "native presence must preserve the inert v1 envelope without planner claims");
}
Assert(RetainerPresenceResponsePolicy.TryParse(stablePresenceResponse.Replace("\"planner\":\"supported\"", "\"planner\":\"server_disabled\""),
    RetainerClientPolicy.Stable, out var retiredUpload) && retiredUpload, "retired planner must not disable ordinary Retainer uploads");
Assert(!RetainerPresenceResponsePolicy.TryParse("{}", RetainerClientPolicy.Stable, out var malformedUpload) && !malformedUpload,
    "malformed presence must not retain an upload grant");
var ordinaryScopes = new[] { "inventory", "collectibles" };
Assert(RetainerClientPolicy.BuildSyncScopes(ordinaryScopes, false).SequenceEqual(ordinaryScopes),
    "an older server without Retainer support must preserve ordinary Game Sync scopes unchanged");
Assert(RetainerClientPolicy.BuildSyncScopes(ordinaryScopes, true).SequenceEqual(new[] { "inventory", "collectibles", "retainer_ventures" }),
    "Retainer observation must activate only after the server accepts the client");
Console.WriteLine("retainer observation, retirement and exact retry tests passed");

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
await AuditRegressionTests.RunAsync();
