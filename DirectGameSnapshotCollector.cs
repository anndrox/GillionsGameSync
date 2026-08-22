using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace GillionsGameSync;

// Read-only collectors are kept separate from pairing and transport. They only
// inspect client state already resident in the game and never automate UI,
// capture packets, or depend on another plugin.
public static class DirectGameSnapshotCollector {
    // Capture is local-only. It runs while the player naturally opens a
    // retainer and lets one later manual sync submit every retainer observed in
    // the current session together.
    public static bool CaptureLoadedRetainerListings() => NativeInventoryCollector.CaptureLoadedRetainerListings();

    public static bool HasActiveRetainer() => NativeInventoryCollector.HasActiveRetainer();

    internal static RetainerContext? FindLoadedRetainerItem(uint itemId) => NativeInventoryCollector.FindLoadedRetainerItem(itemId);
    internal static RetainerBalanceRead? ReadActiveRetainerGil() => NativeInventoryCollector.ReadActiveRetainerGil();

    internal static bool CaptureRetainerVentureResultObservation(RetainerVentureLocalState state, out string resultProbeStatus) {
        var now = DateTime.UtcNow;
        // The game can clear the active retainer's completion timestamp while
        // constructing the result view. Capture reward evidence against the
        // prior positive assignment before the fresh roster replaces it.
        return RetainerVentureSnapshotPolicy.AddPendingResult(state,
            RetainerVentureSnapshotPolicy.CreateResultEvent(state, RetainerVentureNativeCollector.ReadVisibleResult(now, state, out resultProbeStatus)));
    }

    internal static bool CaptureRetainerVentureRosterAndGear(RetainerVentureLocalState state) {
        var now = DateTime.UtcNow;
        var changed = RetainerVentureSnapshotPolicy.MergeGear(state, RetainerVentureNativeCollector.ReadLoadedActiveRetainerGear(now));
        changed |= RetainerVentureSnapshotPolicy.MergeRoster(state, RetainerVentureNativeCollector.ReadCompleteRoster(now), now);
        return changed;
    }

    internal static bool CaptureRetainerInventoryCoverage(RetainerVentureLocalState state) =>
        RetainerVentureSnapshotPolicy.MergeInventorySources(state, NativeInventoryCollector.ReadRetainerInventoryCoverage(state.Retainers));

    public static IEnumerable<GameSnapshot> Collect(IDalamudPluginInterface pluginInterface, IClientState clientState, IObjectTable objects, IDataManager dataManager, IUnlockState unlockState, IReadOnlyDictionary<string, long>? retainerGilBalances, RetainerVentureLocalState? retainerVentureState, IEnumerable<string> scopes) {
        var selected = new HashSet<string>(scopes ?? [], StringComparer.Ordinal);
        var identity = new { name = objects.LocalPlayer?.Name.TextValue ?? "", world = objects.LocalPlayer?.HomeWorld.Value.Name.ToString() ?? "" };
        if (selected.Contains("inventory")) {
            var inventory = NativeInventoryCollector.ReadAllContainers(dataManager);
            if (inventory.Items.Length == 0) throw new InvalidOperationException("No player inventory is currently loaded. Log into the selected character, open Inventory once, then try sync again.");
            var retainerGil = (retainerGilBalances ?? new Dictionary<string, long>()).Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && entry.Value >= 0).Select(entry => new { retainerId = entry.Key, gil = entry.Value }).ToArray();
            yield return new GameSnapshot("inventory", new { character = identity, items = inventory.Items, armoireItems = inventory.ArmoireItems, armoireObserved = inventory.ArmoireObserved, retainerListings = inventory.RetainerListings, retainerListingsObserved = inventory.RetainerListingsObserved, retainerListingRetainerIds = inventory.RetainerListingRetainerIds, retainerBags = inventory.RetainerBags, retainerBagsObserved = inventory.RetainerBagsObserved, retainerGil });
        }
        if (selected.Contains("currencies")) yield return new GameSnapshot("currencies", new { character = identity, items = CurrencyCollector.Read() });
        if (selected.Contains("achievements")) {
            var achievements = AchievementCollector.ReadUnlockedIds(dataManager, unlockState);
            if (achievements.Loaded) yield return new GameSnapshot("achievements", new { character = identity, ids = achievements.Ids, complete = true });
        }
        if (selected.Contains("character")) {
            var progress = CharacterProgressCollector.Read(dataManager, unlockState);
            yield return new GameSnapshot("character", new { character = identity, currentJobId = progress.CurrentJobId, jobs = progress.Jobs, equippedItems = progress.EquippedItems, craftingRecipeIds = progress.CraftingRecipeIds, gatheringLogIds = progress.GatheringLogIds });
        }
        if (selected.Contains("quest_journal")) {
            var quests = QuestJournalCollector.Read(dataManager, unlockState);
            if (quests != null) yield return new GameSnapshot("quest_journal", new {
                character = identity,
                schemaVersion = 1,
                ready = true,
                complete = true,
                coverage = new {
                    oneTimeNormalQuests = true,
                    repeatableExcluded = true,
                    levequestsExcluded = true,
                    tribalAndDailyExcluded = true,
                    questManagerIdMapping = true,
                },
                completedQuestIds = quests.CompletedQuestIds,
                contentHash = quests.ContentHash,
                eligibleCount = quests.EligibleCount,
                verifiedCount = quests.VerifiedCount,
                excludedByIdRangeCount = quests.ExcludedByIdRangeCount,
            });
        }
        if (selected.Contains("reputation")) {
            var reputation = ReputationCollector.Read();
            if (reputation != null) yield return new GameSnapshot("reputation", new {
                character = identity,
                schemaVersion = 1,
                complete = true,
                alliedSocieties = reputation.Entries.Select(entry => new {
                    beastTribeId = entry.BeastTribeId,
                    rank = entry.Rank,
                    reputation = entry.Reputation,
                }).ToArray(),
            });
        }
        if (selected.Contains("shared_fates")) {
            var sharedFates = SharedFateCollector.ReadComplete();
            if (sharedFates != null) yield return new GameSnapshot("shared_fates", new {
                character = identity,
                schemaVersion = 1,
                complete = true,
                tabs = sharedFates.Tabs.Select(tab => new {
                    tabIndex = tab.TabIndex,
                    zones = tab.Zones.Select(zone => new {
                        territoryTypeId = zone.TerritoryTypeId,
                        rank = zone.Rank,
                        maxRank = zone.MaxRank,
                        completedFates = zone.CompletedFates,
                        neededFates = zone.NeededFates,
                    }).ToArray(),
                }).ToArray(),
            });
        }
        if (selected.Contains("collectibles")) yield return new GameSnapshot("collectibles", new {
            character = identity, complete = true,
            cards = CollectibleCollector.ReadCards(dataManager, unlockState), minions = CollectibleCollector.ReadMinions(dataManager, unlockState), mounts = CollectibleCollector.ReadMounts(dataManager, unlockState), bardings = CollectibleCollector.ReadBardings(dataManager, unlockState), emotes = CollectibleCollector.ReadEmotes(dataManager, unlockState),
            orchestrions = CollectibleCollector.ReadOrchestrions(dataManager, unlockState), fashions = CollectibleCollector.ReadFashions(dataManager, unlockState), blueMageSpells = CollectibleCollector.ReadBlueMageSpells(dataManager, unlockState), sightseeingLogIds = CollectibleCollector.ReadSightseeingLog(dataManager, unlockState), aetherCurrentIds = CollectibleCollector.ReadAetherCurrents(dataManager, unlockState),
            portraitBackgrounds = CollectibleCollector.ReadPortraitBackgrounds(dataManager, unlockState), portraitConditions = CollectibleCollector.ReadPortraitConditions(dataManager, unlockState), portraitDecorations = CollectibleCollector.ReadPortraitDecorations(dataManager, unlockState), portraitFacials = CollectibleCollector.ReadPortraitFacials(dataManager, unlockState), portraitFrames = CollectibleCollector.ReadPortraitFrames(dataManager, unlockState), portraitPoses = CollectibleCollector.ReadPortraitPoses(dataManager, unlockState),
            masterRecipeBookIds = CollectibleCollector.ReadMasterRecipeBooks(dataManager, unlockState), folkloreBookIds = CollectibleCollector.ReadFolkloreBookIds(dataManager)
        });
        if (selected.Contains("glamour_plates")) {
            var plates = GlamourPlateCollector.Read();
            // The manager is unavailable outside some normal client states.
            // Absence is not proof that every plate was cleared, so retain the
            // last successful server snapshot until a loaded manager is read.
            if (plates != null) yield return new GameSnapshot("glamour_plates", new { character = identity, complete = true, plates });
        }
        if (selected.Contains("retainer_ventures") && retainerVentureState is not null && !string.IsNullOrWhiteSpace(retainerVentureState.CharacterContentId)) {
            var retainerIdentity = new { contentId = retainerVentureState.CharacterContentId, name = identity.name, world = identity.world };
            var payload = RetainerVentureSnapshotPolicy.BuildPayload(retainerVentureState, retainerIdentity);
            yield return new GameSnapshot("retainer_ventures", payload, payload.ResultEvents.Select(entry => entry.EventId).ToArray());
        }
    }
}

internal static class RetainerVentureNativeCollector {
    public static unsafe RetainerVentureRosterRead? ReadCompleteRoster(DateTime observedAtUtc) {
        try {
            var manager = RetainerManager.Instance();
            if (manager == null || !manager->IsReady) return null;
            var retainers = new List<RetainerVentureRosterEntry>(RetainerVentureSnapshotPolicy.MaxRetainers);
            for (var index = 0; index < RetainerVentureSnapshotPolicy.MaxRetainers; index++) {
                var retainer = manager->Retainers[index];
                if (retainer.RetainerId == 0) continue;
                retainers.Add(new RetainerVentureRosterEntry(
                    retainer.RetainerId.ToString(),
                    retainer.NameString ?? "",
                    retainer.ClassJob,
                    retainer.Level,
                    retainer.VentureId,
                    retainer.VentureComplete,
                    retainer.Gil));
            }
            return new RetainerVentureRosterRead(observedAtUtc, true, retainers.ToArray());
        } catch {
            return null;
        }
    }

    public static unsafe RetainerVentureGearRead? ReadLoadedActiveRetainerGear(DateTime observedAtUtc) {
        try {
            var inventory = InventoryManager.Instance();
            var retainers = RetainerManager.Instance();
            var active = retainers == null ? null : retainers->GetActiveRetainer();
            if (inventory == null || active == null || active->RetainerId == 0) return null;
            var container = inventory->GetInventoryContainer(InventoryType.RetainerEquippedItems);
            if (container == null || !container->IsLoaded || container->Items == null || container->Size < 0) return null;
            var equipped = new List<RetainerVentureGearItem>();
            for (var index = 0; index < container->Size; index++) {
                var item = container->Items[index];
                if (item.ItemId == 0 || item.IsSymbolic) continue;
                equipped.Add(new RetainerVentureGearItem(index, item.ItemId,
                    (item.Flags & InventoryItem.ItemFlags.HighQuality) != 0));
            }
            return new RetainerVentureGearRead(active->RetainerId.ToString(), observedAtUtc, equipped.ToArray());
        } catch {
            return null;
        }
    }

    public static unsafe RetainerVentureResultRead? ReadVisibleResult(DateTime observedAtUtc, RetainerVentureLocalState state, out string probeStatus) {
        try {
            var agent = AgentRetainerTask.Instance();
            if (agent == null || !agent->IsAgentActive()) {
                probeStatus = "inactive";
                return null;
            }
            if (agent->IsLoading) {
                probeStatus = "loading";
                return null;
            }
            if (agent->DisplayType != 3) {
                probeStatus = "not_completed_view";
                return null;
            }
            var manager = RetainerManager.Instance();
            var active = manager == null ? null : manager->GetActiveRetainer();
            if (active == null || active->RetainerId == 0) {
                probeStatus = "completed_view_missing_active_retainer";
                return null;
            }
            var ventureId = (uint)agent->RetainerData.RewardRetainerTaskId;
            if (ventureId == 0) {
                probeStatus = "completed_view_missing_venture_id";
                return null;
            }
            var retainerId = active->RetainerId.ToString();
            var nativeCompletionUnix = active->VentureId == ventureId ? active->VentureComplete : 0;
            var completionUnix = RetainerVentureSnapshotPolicy.ResolveResultCompletionUnix(state, retainerId, ventureId, nativeCompletionUnix);
            if (completionUnix == 0) {
                probeStatus = "completed_view_missing_completion_evidence";
                return null;
            }
            var items = new List<RetainerVentureResultItem>(2);
            for (var index = 0; index < 2; index++) {
                var itemId = agent->RetainerData.RewardItemIds[index];
                var quantity = agent->RetainerData.RewardItemCount[index];
                if (itemId > 0 && quantity > 0) items.Add(new RetainerVentureResultItem(itemId, quantity));
            }
            if (items.Count == 0) {
                probeStatus = "completed_view_missing_reward_items";
                return null;
            }
            probeStatus = nativeCompletionUnix == completionUnix ? "captured_native_completion" : "captured_prior_completion";
            return new RetainerVentureResultRead(retainerId, ventureId, completionUnix,
                observedAtUtc, agent->RetainerData.RewardXP, items.ToArray());
        } catch {
            probeStatus = "read_error";
            return null;
        }
    }
}

internal static class ReputationCollector {
    public static unsafe ReputationRead? Read() {
        try {
            var manager = QuestManager.Instance();
            if (manager == null) return null;
            var entries = new List<AlliedSocietyProgress>();
            var reputation = manager->BeastReputation;
            for (var index = 0; index < reputation.Length; index++) {
                var rank = ProgressionSnapshotPolicy.NormalizeAlliedSocietyRank(reputation[index].Rank);
                var value = reputation[index].Value;
                if (rank == 0 && value == 0) continue;
                entries.Add(new AlliedSocietyProgress((uint)index + 1, rank, value));
            }
            return new ReputationRead(entries.ToArray());
        } catch {
            return null;
        }
    }
}

internal static class SharedFateCollector {
    public static string LastAttemptDiagnostic { get; private set; } = "Shared FATE collector has not run.";

    public static unsafe SharedFateRead? ReadComplete() {
        try {
            var agent = AgentFateProgress.Instance();
            if (agent == null) {
                LastAttemptDiagnostic = "Shared FATE omitted: native agent unavailable.";
                return null;
            }
            var nativeTabs = new List<IReadOnlyCollection<SharedFateZoneProgress>>();
            var tabDiagnostics = new List<string>();
            var position = 0;
            foreach (ref var tab in agent->Tabs) {
                var zoneEntries = new List<SharedFateZoneProgress>();
                var zeroTerritories = 0;
                var zeroMaximumRanks = 0;
                var outOfRangeMaximumRanks = 0;
                var nonCanonicalMaximumRanks = 0;
                var ranksAboveMaximum = 0;
                var unavailableRequirements = 0;
                var progressAboveRequirement = 0;
                foreach (ref var zone in tab.Zones) {
                    if (zone.TerritoryTypeId == 0) {
                        zeroTerritories++;
                        continue;
                    }
                    var maximumRank = ProgressionSnapshotPolicy.NormalizeSharedFateMaximumRank(position, zone.MaxRank);
                    zoneEntries.Add(new SharedFateZoneProgress(zone.TerritoryTypeId, zone.CurrentRank, maximumRank, zone.FateProgress, zone.NeededFates));
                    if (zone.MaxRank == 0) zeroMaximumRanks++;
                    if (zone.MaxRank > ProgressionSnapshotPolicy.SharedFateMaximumAcceptedRank) outOfRangeMaximumRanks++;
                    if (zone.MaxRank != maximumRank) nonCanonicalMaximumRanks++;
                    if (zone.CurrentRank > maximumRank) ranksAboveMaximum++;
                    if (zone.NeededFates == 0 && zone.CurrentRank != maximumRank) unavailableRequirements++;
                    if (zone.NeededFates > 0 && zone.FateProgress > zone.NeededFates) progressAboveRequirement++;
                }
                nativeTabs.Add(zoneEntries);
                tabDiagnostics.Add($"position={position},nativeIndex={tab.TabIndex},populated={zoneEntries.Count},zeroTerritory={zeroTerritories},zeroMaxRank={zeroMaximumRanks},outOfRangeMaxRank={outOfRangeMaximumRanks},nonCanonicalMaxRank={nonCanonicalMaximumRanks},rankAboveMax={ranksAboveMaximum},neededUnavailable={unavailableRequirements},progressAboveNeeded={progressAboveRequirement}");
                position++;
            }
            // FateProgressTab.TabIndex is a UI-facing native value. The API
            // contract is zero-based, while the fixed array already supplies
            // the authoritative display order for its three tabs.
            var tabs = ProgressionSnapshotPolicy.BuildCompleteSharedFateSnapshot(nativeTabs);
            LastAttemptDiagnostic = tabs is null
                ? $"Shared FATE omitted: incomplete native state ({string.Join("; ", tabDiagnostics)})."
                : $"Shared FATE complete: tabs={tabs.Length},zones={tabs.Sum(tab => tab.Zones.Length)} ({string.Join("; ", tabDiagnostics)}).";
            return tabs is null ? null : new SharedFateRead(tabs);
        } catch (Exception error) {
            // The Shared FATE agent is server-loaded UI state. Never turn an
            // unopened or partially loaded window into a complete empty result.
            LastAttemptDiagnostic = $"Shared FATE omitted: native read failed ({error.GetType().Name}).";
            return null;
        }
    }
}

internal sealed record ReputationRead(AlliedSocietyProgress[] Entries);
internal sealed record SharedFateRead(SharedFateTabProgress[] Tabs);

internal static class QuestJournalCollector {
    private static readonly object CatalogLock = new();
    private static IDataManager? catalogSource;
    private static Quest[]? eligibleCatalog;

    public static unsafe QuestJournalRead? Read(IDataManager dataManager, IUnlockState unlockState) {
        try {
            var manager = QuestManager.Instance();
            if (manager == null) return null;
            var rows = GetEligibleCatalog(dataManager);
            if (rows == null || rows.Length == 0) return null;
            var eligible = rows.Length;
            var verified = 0;
            var excludedByIdRange = 0;
            var completed = new List<long>();
            foreach (var row in rows) {
                verified++;
                // IUnlockState's Quest overload uses Dalamud's QuestManager-backed
                // completion mapping. Do not cast the Lumina RowId to ushort: modern
                // quest IDs such as 66295 are mapped to the native completion bit
                // index by the service.
                if (unlockState.IsQuestCompleted(row)) completed.Add(row.RowId);
            }
            completed.Sort();
            var ids = completed.ToArray();
            var hashInput = string.Join(",", ids);
            var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput)))[..16];
            return new QuestJournalRead(eligible, verified, excludedByIdRange, ids, contentHash);
        } catch {
            // Never turn a missing/invalid native or catalog state into an empty
            // completion set. The caller omits the resource instead.
            return null;
        }
    }

    private static Quest[]? GetEligibleCatalog(IDataManager dataManager) {
        lock (CatalogLock) {
            if (ReferenceEquals(catalogSource, dataManager) && eligibleCatalog is not null) return eligibleCatalog;
            var sheet = dataManager.GetExcelSheet<Quest>(null, "Quest");
            if (sheet == null) return null;
            var rows = sheet.Where(IsEligibleOneTimeNormalQuest).ToArray();
            if (rows.Length == 0) return null;
            catalogSource = dataManager;
            eligibleCatalog = rows;
            return rows;
        }
    }

    private static bool IsEligibleOneTimeNormalQuest(Quest row) {
        if (row.RowId == 0) return false;
        if (row.IsRepeatable || row.RepeatIntervalType != 0) return false;
        if (row.QuestRepeatFlag.RowId != 0) return false;
        // Exclude all BeastTribe-linked rows conservatively. This prevents daily,
        // repeatable, and tribal-specific records from entering the one-time set.
        if (row.BeastTribe.RowId != 0) return false;
        return true;
    }
}

internal sealed record QuestJournalRead(int EligibleCount, int VerifiedCount, int ExcludedByIdRangeCount, long[] CompletedQuestIds, string ContentHash);

internal static class GlamourPlateCollector {
    // Plates are read-only appearance templates. This never applies, edits, or
    // restores a glamour; site-side saves are independent permanent references.
    public static unsafe object[]? Read() {
        var manager = MirageManager.Instance();
        if (manager == null) return null;
        var result = new List<object>();
        for (var plateIndex = 0; plateIndex < 20; plateIndex++) {
            var plate = manager->GlamourPlates[plateIndex];
            var slots = new List<object>();
            for (var slotIndex = 0; slotIndex < 12; slotIndex++) {
                var itemId = plate.ItemIds[slotIndex];
                if (itemId == 0) continue;
                slots.Add(new { slot = slotIndex, itemId, stain0Id = plate.Stain0Ids[slotIndex], stain1Id = plate.Stain1Ids[slotIndex] });
            }
            if (slots.Count > 0) result.Add(new { plate = plateIndex + 1, slots = slots.ToArray() });
        }
        // MirageManager can remain allocated after the dresser has unloaded
        // its plate data. An empty array in that state is not evidence that
        // the player deliberately cleared every plate. Omit the resource so
        // Gillions retains the last positively loaded snapshot instead.
        return result.Count > 0 ? result.ToArray() : null;
    }
}

internal static class NativeInventoryCollector {
    private static readonly object RetainerListingCacheLock = new();
    private static readonly Dictionary<string, RetainerListingRead> RetainerListingCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<uint, (RetainerContext Context, DateTime ObservedAtUtc)> RecentRetainerItems = new();
    // These are player-owned containers resident in normal client state. Retainer
    // bags/listings and specialized storage are intentionally not inferred.
    private static readonly InventoryType[] PlayerContainers = [
        InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4,
        InventoryType.EquippedItems, InventoryType.Crystals,
        InventoryType.ArmoryMainHand, InventoryType.ArmoryOffHand, InventoryType.ArmoryHead,
        InventoryType.ArmoryBody, InventoryType.ArmoryHands, InventoryType.ArmoryLegs,
        InventoryType.ArmoryFeets, InventoryType.ArmoryEar, InventoryType.ArmoryNeck,
        InventoryType.ArmoryWrist, InventoryType.ArmoryRings, InventoryType.ArmorySoulCrystal,
        InventoryType.SaddleBag1, InventoryType.SaddleBag2, InventoryType.PremiumSaddleBag1,
        InventoryType.PremiumSaddleBag2,
    ];

    public static string GetAvailabilityStatus() => "Native Gillions collector is active: it reads loaded player bags, armoury, currencies, crystals, and saddlebags directly from the client. It does not use Allagan Tools, packet capture, or another download.";

    public static unsafe long? ReadGil() {
        var manager = InventoryManager.Instance();
        return manager == null ? null : manager->GetGil();
    }

    public static unsafe RetainerBalanceRead? ReadActiveRetainerGil() {
        var manager = InventoryManager.Instance();
        var retainerManager = RetainerManager.Instance();
        var activeRetainer = retainerManager == null ? null : retainerManager->GetActiveRetainer();
        if (manager == null || activeRetainer == null || activeRetainer->RetainerId == 0) return null;
        return new RetainerBalanceRead(activeRetainer->RetainerId.ToString(), activeRetainer->NameString ?? "", activeRetainer->Town.ToString(), manager->GetRetainerGil());
    }

    public static unsafe bool HasActiveRetainer() {
        var retainerManager = RetainerManager.Instance();
        var activeRetainer = retainerManager == null ? null : retainerManager->GetActiveRetainer();
        return activeRetainer is not null && activeRetainer->RetainerId != 0;
    }

    public static unsafe RetainerContext? FindLoadedRetainerItem(uint itemId) {
        var manager = InventoryManager.Instance();
        var retainerManager = RetainerManager.Instance();
        var activeRetainer = retainerManager == null ? null : retainerManager->GetActiveRetainer();
        if (manager == null || activeRetainer == null || activeRetainer->RetainerId == 0) return null;
        foreach (var name in new[] { "RetainerPage1", "RetainerPage2", "RetainerPage3", "RetainerPage4", "RetainerPage5", "RetainerPage6", "RetainerPage7", "RetainerCrystals" }) {
            if (!Enum.TryParse<InventoryType>(name, out var containerType)) continue;
            var container = manager->GetInventoryContainer(containerType);
            if (container == null || !container->IsLoaded || container->Items == null || container->Size <= 0) continue;
            for (var index = 0; index < container->Size; index++) {
                var item = container->Items[index];
                if (item.ItemId == itemId && item.Quantity > 0)
                    return new RetainerContext(activeRetainer->RetainerId.ToString(), activeRetainer->NameString ?? "");
            }
        }
        lock (RetainerListingCacheLock) {
            if (RecentRetainerItems.TryGetValue(itemId, out var cached) && cached.ObservedAtUtc >= DateTime.UtcNow.AddSeconds(-10)) return cached.Context;
            RecentRetainerItems.Remove(itemId);
        }
        return null;
    }

    private static unsafe void CacheLoadedRetainerItems(InventoryManager* manager) {
        var retainerManager = RetainerManager.Instance();
        var activeRetainer = retainerManager == null ? null : retainerManager->GetActiveRetainer();
        if (activeRetainer == null || activeRetainer->RetainerId == 0) return;
        var context = new RetainerContext(activeRetainer->RetainerId.ToString(), activeRetainer->NameString ?? "");
        foreach (var name in new[] { "RetainerPage1", "RetainerPage2", "RetainerPage3", "RetainerPage4", "RetainerPage5", "RetainerPage6", "RetainerPage7", "RetainerCrystals" }) {
            if (!Enum.TryParse<InventoryType>(name, out var containerType)) continue;
            var container = manager->GetInventoryContainer(containerType);
            if (container == null || !container->IsLoaded || container->Items == null || container->Size <= 0) continue;
            for (var index = 0; index < container->Size; index++) {
                var item = container->Items[index];
                if (item.ItemId == 0 || item.Quantity <= 0 || item.IsSymbolic) continue;
                lock (RetainerListingCacheLock) RecentRetainerItems[item.ItemId] = (context, DateTime.UtcNow);
            }
        }
    }

    public static bool CaptureLoadedRetainerListings() {
        unsafe {
            // Do not walk retained inventory containers while normal gameplay
            // is active. The manager can keep stale page data resident after
            // leaving a bell; only a currently active retainer is valid data.
            if (!HasActiveRetainer()) return false;
            var manager = InventoryManager.Instance();
            if (manager == null) return false;
            CacheLoadedRetainerItems(manager);
            var read = ReadActiveRetainerListings(manager);
            if (!read.Observed || read.RetainerIds.Length != 1) return false;
            lock (RetainerListingCacheLock) {
                var retainerId = read.RetainerIds[0];
                if (RetainerListingCache.TryGetValue(retainerId, out var prior)
                    && prior.RetainerIds.SequenceEqual(read.RetainerIds, StringComparer.Ordinal)
                    && prior.Items.SequenceEqual(read.Items)) return false;
                RetainerListingCache[retainerId] = read;
                return true;
            }
        }
    }

    public static unsafe InventoryRead ReadAllContainers(IDataManager dataManager) {
        var manager = InventoryManager.Instance();
        if (manager == null) return new InventoryRead([], [], false, [], [], false, [], false);
        var items = new List<object>();
        foreach (var containerType in PlayerContainers) {
            var container = manager->GetInventoryContainer(containerType);
            if (container == null || !container->IsLoaded || container->Items == null || container->Size <= 0) continue;
            for (var index = 0; index < container->Size; index++) {
                var item = container->Items[index];
                if (item.ItemId == 0 || item.Quantity <= 0 || item.IsSymbolic) continue;
                var highQuality = (item.Flags & InventoryItem.ItemFlags.HighQuality) != 0;
                items.Add(new {
                    itemId = item.ItemId,
                    quantity = highQuality ? 0 : item.Quantity,
                    hqQuantity = highQuality ? item.Quantity : 0,
                    equippedQuantity = containerType == InventoryType.EquippedItems ? item.Quantity : 0,
                    location = containerType.ToString(),
                    source = "native_client_state",
                });
            }
        }
        var retainerBags = ReadLoadedRetainerBags(manager);
        var armoire = ArmoireCollector.Read(dataManager);
        CaptureLoadedRetainerListings();
        RetainerListingRead retainerListings;
        lock (RetainerListingCacheLock) {
            var cached = RetainerListingCache.Values.ToArray();
            retainerListings = new RetainerListingRead(cached.SelectMany((entry) => entry.Items).ToArray(), cached.Length > 0, cached.SelectMany((entry) => entry.RetainerIds).Distinct(StringComparer.Ordinal).ToArray());
        }
        return new InventoryRead(items.ToArray(), retainerListings.Items, retainerListings.Observed, retainerListings.RetainerIds, retainerBags.Items, retainerBags.Observed, armoire.Items, armoire.Observed);
    }

    public static unsafe object[] ReadEquippedItems() {
        var manager = InventoryManager.Instance();
        if (manager == null) return [];
        var container = manager->GetInventoryContainer(InventoryType.EquippedItems);
        if (container == null || !container->IsLoaded || container->Items == null || container->Size <= 0) return [];
        return Enumerable.Range(0, container->Size).Select(index => {
            var item = container->Items[index];
            return (object)new { slot = $"Equipped {index + 1}", itemId = item.ItemId, quantity = item.Quantity, isHq = (item.Flags & InventoryItem.ItemFlags.HighQuality) != 0 };
        }).Where(item => ((dynamic)item).itemId > 0 && ((dynamic)item).quantity > 0).ToArray();
    }

    // Retainer containers only exist after that retainer has been opened in this
    // game session. Enum parsing keeps this patch-tolerant across client names;
    // absent/unloaded containers are deliberately not reported as empty.
    private static unsafe RetainerBagRead ReadLoadedRetainerBags(InventoryManager* manager) {
        var retainerManager = RetainerManager.Instance();
        var retainerId = retainerManager == null ? 0UL : retainerManager->LastSelectedRetainerId;
        // A page container can remain cached after leaving a bell. Only claim a
        // complete retainer observation while the client identifies the active
        // retainer for this session.
        if (retainerId == 0) return new RetainerBagRead([], false);
        var result = new List<object>();
        var observed = false;
        foreach (var name in new[] { "RetainerPage1", "RetainerPage2", "RetainerPage3", "RetainerPage4", "RetainerPage5", "RetainerPage6", "RetainerPage7", "RetainerCrystals" }) {
            if (!Enum.TryParse<InventoryType>(name, out var containerType)) continue;
            var container = manager->GetInventoryContainer(containerType);
            if (container == null || !container->IsLoaded || container->Items == null || container->Size <= 0) continue;
            observed = true;
            for (var index = 0; index < container->Size; index++) {
                var item = container->Items[index];
                if (item.ItemId == 0 || item.Quantity <= 0 || item.IsSymbolic) continue;
                var highQuality = (item.Flags & InventoryItem.ItemFlags.HighQuality) != 0;
                result.Add(new { retainerId = retainerId.ToString(), itemId = item.ItemId, quantity = highQuality ? 0 : item.Quantity, hqQuantity = highQuality ? item.Quantity : 0, location = containerType.ToString(), slot = index, source = "native_loaded_retainer_state" });
            }
        }
        return new RetainerBagRead(result.ToArray(), observed);
    }

    // RetainerMarket is the native container backing the active retainer's
    // selling-list window. Asking prices are held separately by the inventory
    // manager and are read slot-for-slot; no network or UI action is performed.
    private static unsafe RetainerListingRead ReadActiveRetainerListings(InventoryManager* manager) {
        var retainerManager = RetainerManager.Instance();
        if (retainerManager == null) return new RetainerListingRead([], false, []);
        var activeRetainer = retainerManager->GetActiveRetainer();
        if (activeRetainer == null || activeRetainer->RetainerId == 0) return new RetainerListingRead([], false, []);
        var container = manager->GetInventoryContainer(InventoryType.RetainerMarket);
        if (container == null || !container->IsLoaded || container->Items == null || container->Size <= 0) return new RetainerListingRead([], false, []);

        var result = new List<RetainerListingItem>();
        var retainerId = activeRetainer->RetainerId;
        var retainerName = activeRetainer->NameString ?? "";
        for (var index = 0; index < container->Size; index++) {
            var listing = container->Items[index];
            if (listing.ItemId == 0 || listing.Quantity <= 0 || listing.IsSymbolic) continue;
            var unitPrice = manager->GetRetainerMarketPrice(listing.Slot);
            if (unitPrice == 0) continue;
            result.Add(new RetainerListingItem(
                retainerId.ToString(),
                retainerName,
                listing.ItemId,
                listing.Quantity,
                unitPrice,
                (listing.Flags & InventoryItem.ItemFlags.HighQuality) != 0,
                listing.Slot,
                "native_loaded_retainer_listing_state"));
        }
        return new RetainerListingRead(result.ToArray(), true, [retainerId.ToString()]);
    }

    public static unsafe object[] ReadCurrencyItems() {
        var manager = InventoryManager.Instance();
        if (manager == null) return [];
        var container = manager->GetInventoryContainer(InventoryType.Currency);
        if (container == null || !container->IsLoaded || container->Items == null || container->Size <= 0) return [];
        var result = new List<object>();
        for (var index = 0; index < container->Size; index++) {
            var item = container->Items[index];
            if (item.ItemId == 0 || item.Quantity <= 0 || item.IsSymbolic) continue;
            result.Add(new { itemId = item.ItemId, quantity = item.Quantity, source = "native_client_state" });
        }
        return result.ToArray();
    }

    public static unsafe RetainerInventorySourceRead[] ReadRetainerInventoryCoverage(IEnumerable<RetainerVentureProfile> retainers) {
        var now = DateTime.UtcNow;
        var manager = InventoryManager.Instance();
        var result = new List<RetainerInventorySourceRead>();
        result.Add(ReadCoverageSource(manager, "character_inventory", null, now,
            InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4));
        result.Add(ReadCoverageSource(manager, "chocobo_saddlebag", null, now,
            InventoryType.SaddleBag1, InventoryType.SaddleBag2));
        result.Add(ReadCoverageSource(manager, "premium_saddlebag", null, now,
            InventoryType.PremiumSaddleBag1, InventoryType.PremiumSaddleBag2));
        result.Add(ReadCoverageSource(manager, "armoury_chest", null, now,
            InventoryType.EquippedItems, InventoryType.ArmoryMainHand, InventoryType.ArmoryOffHand, InventoryType.ArmoryHead,
            InventoryType.ArmoryBody, InventoryType.ArmoryHands, InventoryType.ArmoryLegs, InventoryType.ArmoryFeets,
            InventoryType.ArmoryEar, InventoryType.ArmoryNeck, InventoryType.ArmoryWrist, InventoryType.ArmoryRings, InventoryType.ArmorySoulCrystal));
        result.Add(ReadCoverageSource(manager, "crystals", null, now, InventoryType.Crystals));
        result.Add(ReadCoverageSource(manager, "currency_inventory", null, now, InventoryType.Currency));

        var retainerManager = RetainerManager.Instance();
        var activeRetainer = retainerManager == null ? null : retainerManager->GetActiveRetainer();
        var activeRetainerId = activeRetainer == null || activeRetainer->RetainerId == 0 ? null : activeRetainer->RetainerId.ToString();
        var retainerContainerNames = new[] { "RetainerPage1", "RetainerPage2", "RetainerPage3", "RetainerPage4", "RetainerPage5", "RetainerPage6", "RetainerPage7", "RetainerCrystals" };
        var retainerTypes = retainerContainerNames.Where(name => Enum.TryParse<InventoryType>(name, out _)).Select(name => Enum.Parse<InventoryType>(name)).ToArray();
        foreach (var retainer in retainers.OrderBy(entry => entry.RetainerId, StringComparer.Ordinal)) {
            result.Add(ReadCoverageSource(activeRetainerId == retainer.RetainerId ? manager : null, "retainer_inventory", retainer.RetainerId, now, retainerTypes));
        }
        return result.ToArray();
    }

    private static unsafe RetainerInventorySourceRead ReadCoverageSource(InventoryManager* manager, string source, string? retainerId, DateTime observedAtUtc, params InventoryType[] types) {
        var containers = types.Select(type => {
            var container = manager == null ? null : manager->GetInventoryContainer(type);
            var loaded = container != null && container->IsLoaded && container->Items != null && container->Size >= 0;
            var used = 0;
            if (loaded) for (var index = 0; index < container->Size; index++) {
                var item = container->Items[index];
                if (item.ItemId > 0 && item.Quantity > 0 && !item.IsSymbolic) used++;
            }
            return new RetainerInventoryContainerRead(type.ToString(), loaded, used, loaded ? container->Size : 0);
        }).ToArray();
        return new RetainerInventorySourceRead(source, retainerId, observedAtUtc, containers);
    }
}

internal sealed record InventoryRead(object[] Items, object[] RetainerListings, bool RetainerListingsObserved, string[] RetainerListingRetainerIds, object[] RetainerBags, bool RetainerBagsObserved, object[] ArmoireItems, bool ArmoireObserved);
internal sealed record RetainerBagRead(object[] Items, bool Observed);
internal sealed record RetainerContext(string RetainerId, string RetainerName);
internal sealed record RetainerBalanceRead(string RetainerId, string RetainerName, string Town, long Gil);
internal sealed record RetainerListingRead(RetainerListingItem[] Items, bool Observed, string[] RetainerIds);
internal sealed record RetainerListingItem(
    [property: JsonPropertyName("retainerId")] string RetainerId,
    [property: JsonPropertyName("retainerName")] string RetainerName,
    [property: JsonPropertyName("itemId")] uint ItemId,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("unitPrice")] ulong UnitPrice,
    [property: JsonPropertyName("isHq")] bool IsHq,
    [property: JsonPropertyName("slot")] short Slot,
    [property: JsonPropertyName("source")] string Source);
internal static class CurrencyCollector { public static object[] Read() => NativeInventoryCollector.ReadCurrencyItems(); }

internal static class ArmoireCollector {
    public static unsafe ArmoireRead Read(IDataManager dataManager) {
        try {
            var uiState = UIState.Instance();
            if (uiState == null || !uiState->Cabinet.IsCabinetLoaded()) return new ArmoireRead(false, []);
            var cabinet = &uiState->Cabinet;
            var catalog = SheetRowCache<Lumina.Excel.Sheets.Cabinet>.Get(dataManager)
                .Select(row => new ArmoireCatalogEntry(row.RowId, row.Item.RowId))
                .ToArray();
            if (catalog.Length == 0) return new ArmoireRead(false, []);
            var itemIds = ArmoireSnapshotPolicy.BuildOwnedItemIds(catalog, cabinet->IsItemInCabinet);
            return new ArmoireRead(true, itemIds.Select(itemId => (object)new {
                itemId,
                armoireQuantity = 1,
                location = "Armoire",
                source = "native_loaded_armoire_state",
            }).ToArray());
        } catch {
            return new ArmoireRead(false, []);
        }
    }
}
internal sealed record ArmoireRead(bool Observed, object[] Items);

internal static class AchievementCollector {
    public static AchievementRead ReadUnlockedIds(IDataManager dataManager, IUnlockState unlockState) {
        if (!unlockState.IsAchievementListLoaded) return new AchievementRead(false, []);
        try {
            var rows = SheetRowCache<Lumina.Excel.Sheets.Achievement>.Get(dataManager);
            return rows.Length == 0 ? new AchievementRead(false, []) : new AchievementRead(true, rows.Where(unlockState.IsAchievementComplete).Select(row => (long)row.RowId).ToArray());
        } catch { return new AchievementRead(false, []); }
    }
}
internal sealed record AchievementRead(bool Loaded, long[] Ids);

internal static class CharacterProgressCollector {
    public static unsafe CharacterProgressRead Read(IDataManager dataManager, IUnlockState unlockState) {
        var player = PlayerState.Instance();
        var jobs = new List<object>();
        if (player != null) {
            var classJobs = SheetRowCache<ClassJob>.Get(dataManager);
            jobs = classJobs.Select(row => new { id = (int)row.RowId, name = row.Name.ToString(), abbreviation = row.Abbreviation.ToString(), level = (int)player->GetClassJobLevel((int)row.RowId, false) }).Where(row => row.level > 0).Cast<object>().ToList();
        }
        var recipes = SheetRowCache<Recipe>.Get(dataManager);
        var crafting = recipes.Where(unlockState.IsRecipeUnlocked).Select(row => (long)row.RowId).ToArray();
        var fish = SheetRowCache<FishParameter>.Get(dataManager);
        var gathering = player == null ? [] : fish.Where(row => row.IsInLog && player->IsFishCaught((uint)row.RowId)).Select(row => (long)row.RowId).ToArray();
        return new CharacterProgressRead(player == null ? 0 : (uint)player->CurrentClassJobId, jobs.ToArray(), NativeInventoryCollector.ReadEquippedItems(), crafting, gathering);
    }
}
internal sealed record CharacterProgressRead(uint CurrentJobId, object[] Jobs, object[] EquippedItems, long[] CraftingRecipeIds, long[] GatheringLogIds);

internal static class CollectibleCollector {
    public static long[] ReadCards(IDataManager dataManager, IUnlockState unlockState) {
        return Read<TripleTriadCard>(dataManager, unlockState.IsTripleTriadCardUnlocked);
    }
    public static long[] ReadMinions(IDataManager dataManager, IUnlockState unlockState) {
        return Read<Companion>(dataManager, unlockState.IsCompanionUnlocked);
    }
    public static long[] ReadMounts(IDataManager dataManager, IUnlockState unlockState) {
        return Read<Mount>(dataManager, unlockState.IsMountUnlocked);
    }
    // BuddyEquip is the authoritative game-data sheet for the player's chocobo
    // companion equipment. IUnlockState reads its client-owned unlock state;
    // this deliberately does not infer bardings from mounts or item ownership.
    public static long[] ReadBardings(IDataManager dataManager, IUnlockState unlockState) => Read<BuddyEquip>(dataManager, unlockState.IsBuddyEquipUnlocked);
    public static long[] ReadEmotes(IDataManager dataManager, IUnlockState unlockState) {
        return Read<Emote>(dataManager, unlockState.IsEmoteUnlocked);
    }
    private static long[] Read<T>(IDataManager dataManager, Func<T,bool> unlocked) where T : struct, Lumina.Excel.IExcelRow<T> => SheetRowCache<T>.Get(dataManager).Where(unlocked).Select(row => (long)row.RowId).ToArray();
    public static long[] ReadOrchestrions(IDataManager dataManager, IUnlockState unlockState) => Read<Orchestrion>(dataManager, unlockState.IsOrchestrionUnlocked);
    public static long[] ReadFashions(IDataManager dataManager, IUnlockState unlockState) => Read<Ornament>(dataManager, unlockState.IsOrnamentUnlocked);
    public static long[] ReadBlueMageSpells(IDataManager dataManager, IUnlockState unlockState) => Read<AozAction>(dataManager, unlockState.IsAozActionUnlocked);
    public static long[] ReadSightseeingLog(IDataManager dataManager, IUnlockState unlockState) => Read<Adventure>(dataManager, unlockState.IsAdventureComplete);
    public static long[] ReadAetherCurrents(IDataManager dataManager, IUnlockState unlockState) => Read<AetherCurrent>(dataManager, unlockState.IsAetherCurrentUnlocked);
    public static long[] ReadPortraitBackgrounds(IDataManager dataManager, IUnlockState unlockState) => Read<BannerBg>(dataManager, unlockState.IsBannerBgUnlocked);
    public static long[] ReadPortraitConditions(IDataManager dataManager, IUnlockState unlockState) => Read<BannerCondition>(dataManager, unlockState.IsBannerConditionUnlocked);
    public static long[] ReadPortraitDecorations(IDataManager dataManager, IUnlockState unlockState) => Read<BannerDecoration>(dataManager, unlockState.IsBannerDecorationUnlocked);
    public static long[] ReadPortraitFacials(IDataManager dataManager, IUnlockState unlockState) => Read<BannerFacial>(dataManager, unlockState.IsBannerFacialUnlocked);
    public static long[] ReadPortraitFrames(IDataManager dataManager, IUnlockState unlockState) => Read<BannerFrame>(dataManager, unlockState.IsBannerFrameUnlocked);
    public static long[] ReadPortraitPoses(IDataManager dataManager, IUnlockState unlockState) => Read<BannerTimeline>(dataManager, unlockState.IsBannerTimelineUnlocked);
    public static long[] ReadMasterRecipeBooks(IDataManager dataManager, IUnlockState unlockState) => Read<SecretRecipeBook>(dataManager, unlockState.IsSecretRecipeBookUnlocked);

    // Folklore ownership is read from the authoritative unlock state keyed by
    // GatheringSubCategory, then transmitted as stable tome Item row IDs. A
    // single tome may unlock more than one subcategory, so canonical item IDs
    // are deduplicated before the complete Collectibles snapshot is sent.
    public static unsafe long[] ReadFolkloreBookIds(IDataManager dataManager) {
        var player = PlayerState.Instance();
        var books = SheetRowCache<GatheringSubCategory>.Get(dataManager);
        if (player == null) return [];
        return books
            .Where(row => row.RowId > 0 && row.Item.RowId > 0 && player->IsFolkloreBookUnlocked((uint)row.RowId))
            .Select(row => (long)row.Item.RowId)
            .Distinct()
            .Order()
            .ToArray();
    }
}

internal static class SheetRowCache<T> where T : struct, Lumina.Excel.IExcelRow<T> {
    private static readonly object CacheLock = new();
    private static IDataManager? source;
    private static T[] rows = [];

    public static T[] Get(IDataManager dataManager) {
        lock (CacheLock) {
            if (ReferenceEquals(source, dataManager)) return rows;
            rows = dataManager.GetExcelSheet<T>()?.Where(row => row.RowId > 0).ToArray() ?? [];
            source = dataManager;
            return rows;
        }
    }
}
