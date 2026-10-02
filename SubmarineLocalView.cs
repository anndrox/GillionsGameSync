#if GILLIONS_TEST_BUILD
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using Action = System.Action;

namespace GillionsGameSync;

// Event-only reads of interfaces opened by the player. No framework polling,
// packet hooks, callbacks to the game, third-party IPC, or network transport.
internal sealed class SubmarineLocalView : IDisposable {
    private static readonly string[] Addons = ["SelectString", "SubmarineExploration", "AirShipExplorationResult"];
    private static readonly AddonEvent[] Events = [AddonEvent.PostSetup, AddonEvent.PostRefresh, AddonEvent.PostRequestedUpdate];
    private readonly IDalamudPluginInterface ui;
    private readonly ICommandManager commands;
    private readonly IFramework framework;
    private readonly IClientState client;
    private readonly IDataManager data;
    private readonly IAddonLifecycle lifecycle;
    private readonly SubmarineVoyageRetention store;
    private readonly SubmarineVoyageRetentionPolicy policy;
    private readonly Action persist;
    private readonly string collectorVersion = typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "unavailable";
    private HashSet<uint>? partIds;
    private HashSet<uint>? sectorIds;
    private HashSet<uint>? rankIds;
    private bool visible;
    private bool disposed;
    private string export = "";
    private string personalExport = "";
    private string activeWorkshopKey = "";
    private DateTime nextReadUtc;
    private sealed record View(bool Local, bool Community, string Status, string[] Rows, int Records, int Results, double Milliseconds);
    private volatile View view = new(false, false, "Local retention off. Open workshop interfaces manually after enabling.", [], 0, 0, 0);

    internal SubmarineLocalView(IDalamudPluginInterface ui, ICommandManager commands, IFramework framework,
        IClientState client, IDataManager data, IAddonLifecycle lifecycle, SubmarineVoyageRetention store, Action persist) {
        this.ui = ui; this.commands = commands; this.framework = framework; this.client = client; this.data = data;
        this.lifecycle = lifecycle; this.store = store; this.persist = persist; policy = new(store);
        foreach (var kind in Events) lifecycle.RegisterListener(kind, Addons, OnAddon);
        client.Logout += OnLogout;
        client.TerritoryChanged += OnTerritoryChanged;
        commands.AddHandler("/gillionssubs", new CommandInfo((_, _) => Show()) { HelpMessage = "Open testing submarine local retention and sanitized export controls (no uploads)." });
        ui.UiBuilder.Draw += Draw;
        Publish(policy.WaitingStatus);
    }
    internal void Show() => visible = true;
    private void OnLogout(int _, int __) { activeWorkshopKey = ""; personalExport = ""; export = ""; Publish("Logged out; retained observations are historical, not current workshop state."); }
    private void OnTerritoryChanged(uint _) { activeWorkshopKey = ""; personalExport = ""; export = ""; Publish("Territory changed; prior observations retained. Await workshop interface evidence."); }
    private void Publish(string? status = null, double milliseconds = 0) {
        var rows = policy.Supported ? store.Current?.Where(row => row?.Snapshot is not null).Take(32).Select(row => {
            var s = row.Snapshot;
            return $"Slot {s.Slot + 1}: {s.Name} | rank {s.Build.Rank} | current sectors: {string.Join(',', s.CurrentRoute)}"
                + $" | expected return: {(s.ExpectedReturnUnix > 0 ? DateTimeOffset.FromUnixTimeSeconds(s.ExpectedReturnUnix).ToString("u") : "unavailable (not proof of idle)")}"
                + $" | retained {s.ObservedAtUtc:u}; unlock/explored evidence: {s.UnlockedSectorIds?.Length.ToString() ?? "unknown"}/{s.ExploredSectorIds?.Length.ToString() ?? "unknown"}";
        }).ToArray() ?? [] : [];
        view = new(store.LocalRetentionEnabled, store.CommunityContributionEnabled, status ?? policy.Status, rows,
            store.Voyages?.Count ?? 0, store.Voyages?.Count(row => row?.Result is not null) ?? 0, milliseconds);
    }
    private string GameVersion() => data.GameData.Repositories.TryGetValue("ffxiv", out var repo) && repo.Version.Length <= 80
        ? repo.Version : "unavailable";
    private unsafe SubmarineSnapshot? Snapshot(WorkshopTerritory* workshop, HousingWorkshopSubmersibleSubData* sub,
        byte slot, DateTime now, byte[]? planned = null) {
        if (sub == null || sub->Parent != &workshop->Submersible || sub->RegisterTime == 0
            || sub->RegisterTime > new DateTimeOffset(now).ToUnixTimeSeconds() + 30 || sub->RankId == 0) return null;
        partIds ??= data.GetExcelSheet<SubmarinePart>().Where(row => row.RowId > 0).Select(row => row.RowId).ToHashSet();
        sectorIds ??= data.GetExcelSheet<SubmarineExploration>().Where(row => row.RowId > 0 && !row.StartingPoint).Select(row => row.RowId).ToHashSet();
        rankIds ??= data.GetExcelSheet<SubmarineRank>().Where(row => row.RowId > 0).Select(row => row.RowId).ToHashSet();
        if (!rankIds.Contains(sub->RankId) || !new uint[] { sub->HullId, sub->SternId, sub->BowId, sub->BridgeId }.All(partIds.Contains)) return null;
        var bytes = sub->Name;
        var end = bytes.IndexOf((byte)0);
        if (end <= 0) return null;
        var name = new UTF8Encoding(false, true).GetString(bytes[..end]);
        if (name.Length > 20) return null;
        var route = ReadRoute(sub->CurrentExplorationPoints);
        if (route is null || planned is not null && !planned.All(id => sectorIds.Contains(id))) return null;
        if (sub->ReturnTime > new DateTimeOffset(now).ToUnixTimeSeconds() + 40 * 86400L) return null;
        return new(SubmarineVoyageRetentionPolicy.SubmarineKey(workshop->HouseId.Id, slot, sub->RegisterTime), slot, name,
            sub->RegisterTime, new(sub->RankId, new(sub->HullId, sub->SternId, sub->BowId, sub->BridgeId),
                new(sub->SurveillanceBase, sub->RetrievalBase, sub->SpeedBase, sub->RangeBase, sub->FavorBase,
                    sub->SurveillanceBonus, sub->RetrievalBonus, sub->SpeedBonus, sub->RangeBonus, sub->FavorBonus, sub->LogSpeed)),
            sub->CurrentExp, sub->NextLevelExp, sub->ReturnTime, route, planned, now, GameVersion(), collectorVersion) {
                LocalWorkshopKey = SubmarineVoyageRetentionPolicy.Hash(FormattableString.Invariant($"workshop:{workshop->HouseId.Id}"))
            };
    }
    private byte[]? ReadRoute(ReadOnlySpan<byte> native) {
        var route = new List<byte>(5);
        bool ended = false;
        foreach (var id in native) {
            if (id == 0) { ended = true; continue; }
            if (ended || sectorIds?.Contains(id) != true || route.Contains(id)) return null;
            route.Add(id);
        }
        return route.ToArray();
    }
    private unsafe void OnAddon(AddonEvent kind, AddonArgs args) {
        if (disposed || !store.LocalRetentionEnabled || !framework.IsInFrameworkUpdateThread) return;
        // Gate before ALL native pointers/signature calls, including PlayerState.
        if (!PersonalObservationCompatibility.Supports(GameVersion(), typeof(HousingManager).Assembly.GetName().Version?.ToString())) {
            activeWorkshopKey = ""; personalExport = "";
            Publish("Unsupported game/SDK build; no submarine native read. Retained history preserved."); return;
        }
        if (DateTime.UtcNow < nextReadUtc) return;
        nextReadUtc = DateTime.UtcNow.AddSeconds(1);
        var started = Stopwatch.GetTimestamp();
        bool changed = false;
        string? readStatus = null;
        try {
            if (!policy.Supported) { activeWorkshopKey = ""; personalExport = ""; Publish("Unsupported/oversized retained format; preserved unchanged. Collection paused."); return; }
            var player = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState.Instance();
            var housing = HousingManager.Instance();
            if (!client.IsLoggedIn || player == null || !player->IsLoaded || args.Addon.IsNull || housing == null
                || housing->WorkshopTerritory == null || housing->CurrentTerritory != (HousingTerritory*)housing->WorkshopTerritory
                || !housing->WorkshopTerritory->IsLoaded() || !args.Addon.IsVisible) {
                activeWorkshopKey = ""; personalExport = "";
                Publish("Workshop/interface not freshly available; history preserved."); return;
            }
            var workshop = housing->WorkshopTerritory;
            if (workshop->HouseId.Id is 0 or ulong.MaxValue) { activeWorkshopKey = ""; personalExport = ""; Publish("Workshop identity unavailable; history preserved."); return; }
            var now = DateTime.UtcNow;
            var selected = workshop->Submersible.DataPointers[4].Value;
            int selectedSlot = -1;
            for (byte slot = 0; slot < 4; slot++) {
                fixed (HousingWorkshopSubmersibleSubData* sub = &workshop->Submersible.Data[slot]) {
                    if (selected == sub) selectedSlot = slot;
                }
            }
            // Never dereference an unverified selected/copy pointer. Only a
            // pointer into this loaded workshop's four native slots is admitted.
            byte[]? planned = null;
            byte[]? unlocked = null, explored = null;
            if (selectedSlot >= 0) {
                sectorIds ??= data.GetExcelSheet<SubmarineExploration>().Where(row => row.RowId > 0 && !row.StartingPoint).Select(row => row.RowId).ToHashSet();
                var planning = AgentSubmersibleExploration.Instance();
                if (planning != null && planning->IsAgentActive() && planning->AddonId == args.Addon.Id
                    && planning->SelectedPointsCount <= 5) {
                    planned = ReadRoute(planning->SelectedPoints[..planning->SelectedPointsCount]);
                    // Existing SDK getters inspect loaded flags. Only positive
                    // evidence is retained; all-false never means locked.
                    var eligible = sectorIds.Where(id => id <= byte.MaxValue).Order().Select(id => (byte)id).ToArray();
                    var positiveUnlocked = eligible.Where(id => HousingManager.IsSubmarineExplorationUnlocked(id)).ToArray();
                    var positiveExplored = eligible.Where(id => HousingManager.IsSubmarineExplorationExplored(id)).ToArray();
                    unlocked = positiveUnlocked.Length > 0 ? positiveUnlocked : null;
                    explored = positiveExplored.Length > 0 ? positiveExplored : null;
                }
            }
            var snapshots = new List<SubmarineSnapshot>(4);
            SubmarineSnapshot? selectedSnapshot = null;
            for (byte slot = 0; slot < 4; slot++) {
                fixed (HousingWorkshopSubmersibleSubData* sub = &workshop->Submersible.Data[slot]) {
                    var snapshot = Snapshot(workshop, sub, slot, now, slot == selectedSlot ? planned : null);
                    if (snapshot is null) continue; // Unloaded/unused slot is not an empty voyage.
                    if (slot == selectedSlot) snapshot = snapshot with { UnlockedSectorIds = unlocked, ExploredSectorIds = explored };
                    snapshots.Add(snapshot);
                    if (slot == selectedSlot) selectedSnapshot = snapshot;
                }
            }
            changed |= policy.ObserveSnapshots(snapshots);
            activeWorkshopKey = snapshots.FirstOrDefault()?.LocalWorkshopKey ?? "";
            if (snapshots.Count == 0) readStatus = "No verified loaded submarine slots; history preserved. Local retention remains on.";
            if (selectedSnapshot is not null) {
                var results = AgentSubmersibleExplorationResult.Instance();
                if (results != null && results->IsAgentActive() && results->AddonId == args.Addon.Id && results->Data != null) {
                    var result = Results(selected, results->Data, selectedSnapshot.CurrentRoute);
                    if (result is not null) changed |= policy.ObserveResult(selectedSnapshot.LocalSubmarineKey,
                        selectedSnapshot.Build, result, now, selectedSnapshot.GameVersion, collectorVersion);
                    else readStatus = "Result interface partially loaded or inconsistent; no completion inferred.";
                }
            } else {
                var results = AgentSubmersibleExplorationResult.Instance();
                if (results != null && results->IsAgentActive() && results->AddonId == args.Addon.Id)
                    readStatus = "Selected submarine is not a verified loaded workshop slot; results unavailable. History preserved.";
            }
            if (changed) { export = ""; personalExport = ""; persist(); }
            Publish(readStatus, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        } catch (Exception) {
            activeWorkshopKey = ""; personalExport = "";
            // No raw native/remote text, names, IDs, or exception data in diagnostics.
            Publish("Submarine read/save failed; existing history not erased. New data may be memory-only until a successful save. Report game/Dalamud version and interface actions only.");
        }
    }
    private unsafe SubmarineResult? Results(HousingWorkshopSubmersibleSubData* sub, ExplorationResultData* result, byte[] route) {
        if (result->ItemReturnListCount > 10) return null;
        var aggregate = new List<SubmarineReward>(10);
        for (int i = 0; i < result->ItemReturnListCount; i++) {
            var r = result->ItemReturn[i];
            if (r.ItemId == 0 || r.Quantity == 0 || !data.GetExcelSheet<Item>().HasRow(r.ItemId)) return null;
            aggregate.Add(new(r.ItemId, r.Quantity, null)); // This structure has no verified HQ field.
        }
        var sectors = new List<SubmarineSectorResult>(5);
        foreach (var row in sub->GatheredData) {
            if (row.Point == 0) continue;
            if (sectorIds?.Contains(row.Point) != true || row.UnlockedPoint != 0 && sectorIds?.Contains(row.UnlockedPoint) != true
                || sectors.Any(s => s.SectorId == row.Point)) return VoyageOnly(aggregate, "sector-data-unavailable-or-inconsistent");
            var rewards = new List<SubmarineReward>(2);
            foreach (var reward in new[] { new SubmarineReward(row.ItemIdPrimary, row.ItemCountPrimary, row.ItemHQPrimary),
                new SubmarineReward(row.ItemIdAdditional, row.ItemCountAdditional, row.ItemHQAdditional) }) {
                if (reward.ItemId == 0 && reward.Quantity == 0) continue;
                if (reward.ItemId == 0 || reward.Quantity == 0 || !data.GetExcelSheet<Item>().HasRow(reward.ItemId))
                    return VoyageOnly(aggregate, "sector-reward-unavailable-or-inconsistent");
                rewards.Add(reward);
            }
            sectors.Add(new(row.Point, row.ExpGained, row.UnlockedPoint > 0 ? row.UnlockedPoint : null,
                row.FirstExploration, row.AdditionalSubmarineUnlocked, row.DoubleDip, rewards.ToArray()));
        }
        if (sectors.Count == 0) return aggregate.Count > 0 ? VoyageOnly(aggregate, "sector-attribution-unavailable") : null;
        if (aggregate.Count == 0 && sectors.Any(s => s.Rewards.Length > 0)) return null; // Wait for populated result interface.
        var totals = sectors.SelectMany(s => s.Rewards).GroupBy(r => r.ItemId).ToDictionary(g => g.Key, g => g.Sum(r => (long)r.Quantity));
        var displayed = aggregate.GroupBy(r => r.ItemId).ToDictionary(g => g.Key, g => g.Sum(r => (long)r.Quantity));
        if (totals.Count != displayed.Count || totals.Any(pair => !displayed.TryGetValue(pair.Key, out var quantity) || quantity != pair.Value))
            return VoyageOnly(aggregate, "sector-aggregate-mismatch");
        bool full = route.Length > 0 && sectors.Count == route.Length && sectors.All(s => route.Contains(s.SectorId));
        var experience = sectors.Sum(s => (long)s.Experience);
        return new("per-sector", sectors.ToArray(), aggregate.ToArray(), full && experience <= uint.MaxValue ? (uint)experience : null,
            full ? [] : ["total-experience-unavailable-route-completeness-unverified"]);
    }
    private static SubmarineResult? VoyageOnly(List<SubmarineReward> rewards, string reason) => rewards.Count > 0
        ? new("voyage-only", [], rewards.ToArray(), null, [reason, "hq-experience-unlocks-unavailable"]) : null;
    private void Change(Action action) {
        export = ""; personalExport = "";
        _ = framework.RunOnFrameworkThread(() => { if (disposed) return; action(); export = ""; personalExport = ""; persist(); Publish(policy.WaitingStatus); });
    }
    private void Draw() {
        if (!visible || disposed) return;
        var state = view;
        ImGui.SetNextWindowSize(new Vector2(780, 540), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Submarine voyage retention (Testing)###GillionsSubmarineRetention", ref visible)) {
            ImGui.TextWrapped("Read-only, local-first. Use FC workshop menus normally. No automatic interface opening, dispatch/recall/repair, game-server requests, third-party plugin or upload endpoint.");
            bool local = state.Local;
            if (ImGui.Checkbox("Retain naturally loaded submarine observations locally", ref local)) Change(() => store.LocalRetentionEnabled = local);
            bool community = state.Community;
            if (ImGui.Checkbox("Opt in to preparing future completed results for community contribution", ref community)) Change(() => store.CommunityContributionEnabled = community);
            ImGui.TextWrapped("Community opt-in does not upload. Only results observed while opted in can enter an explicitly copied sanitized export. Names, house/FC, account/reporter identifiers and credentials are excluded. A future authenticated contract and review are required before any upload.");
            ImGui.TextWrapped(state.Status);
            ImGui.TextWrapped("PRIVATE workshop export is separate from sanitized community results. It contains submarine names, slot/registration, a hashed workshop scope and activity times. A hash is not anonymization. Export only if permitted; no public sharing or upload occurs. Missing unlock flags remain unknown, not locked; complete unlock eligibility is unproven.");
            if (local && ImGui.Button("Prepare PRIVATE current workshop JSON (no upload)")) {
                _ = framework.RunOnFrameworkThread(() => {
                    if (disposed) return;
                    try { personalExport = policy.PreparePersonalExport(activeWorkshopKey); }
                    catch (Exception) { personalExport = ""; Publish("Private workshop export unavailable: open a verified workshop interface while retention is on."); }
                });
            }
            if (local && personalExport.Length > 0 && ImGui.Button("Copy PRIVATE workshop JSON")) ImGui.SetClipboardText(personalExport);
            ImGui.TextWrapped($"Retained anchors/results: {state.Records} / 400; with observed results: {state.Results}. At most 32 current snapshots; reserved storage below 4 MiB. Full retention pauses new history and preserves existing records; nothing is silently deleted. Last interface read/save: {state.Milliseconds:F2} ms.");
            if (community && ImGui.Button("Prepare sanitized contribution JSON (no upload)")) {
                _ = framework.RunOnFrameworkThread(() => {
                    if (disposed) return;
                    try { export = policy.PrepareExport(); Publish("Sanitized export prepared; inspect it before copying. No upload occurred."); }
                    catch (Exception) { export = ""; Publish("Export unavailable: consent off or retained format unsupported."); }
                });
            }
            if (community && export.Length > 0) {
                ImGui.SameLine();
                if (ImGui.Button("Copy sanitized JSON")) ImGui.SetClipboardText(export);
                ImGui.TextWrapped(export.Length < 20000 ? export : "Export prepared (large). Copy only if you intend to share this sanitized voyage dataset.");
            }
            if (ImGui.Button("Copy aggregate diagnostics")) ImGui.SetClipboardText($"Gillions Game Sync Testing {collectorVersion}\nSubmarine retention\nLocal: {state.Local}; community preparation: {state.Community}\n{state.Status}\nRetained: {state.Records}; results: {state.Results}; interface read/save: {state.Milliseconds:F2} ms\nNo upload endpoint. No live correctness claim.");
            ImGui.Separator();
            foreach (var row in state.Rows) ImGui.TextUnformatted(row); // Names stay out of diagnostics/sanitized community export; PRIVATE copy is explicit.
        }
        ImGui.End();
    }
    public void Dispose() {
        if (disposed) return;
        disposed = true; export = ""; personalExport = "";
        foreach (var kind in Events) lifecycle.UnregisterListener(kind, Addons, OnAddon);
        client.Logout -= OnLogout; client.TerritoryChanged -= OnTerritoryChanged;
        ui.UiBuilder.Draw -= Draw; commands.RemoveHandler("/gillionssubs");
    }
}
#endif
