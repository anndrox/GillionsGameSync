#if GILLIONS_TEST_BUILD
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using Action = System.Action;

namespace GillionsGameSync;

// No game requests, hooks, callbacks, network transport, task definitions, or
// per-frame native scans. UI events enqueue managed notices only; existing
// Plugin framework cadence reads at most one bounded source group / 5 seconds.
internal sealed class DashboardLocalView : IDisposable {
    private static readonly string[] Addons = ["ContentsFinder", "SatisfactionSupply", "ContentsNote", "ContentsInfo", "WeeklyBingo"];
    private static readonly AddonEvent[] Events = [AddonEvent.PostSetup, AddonEvent.PostRefresh, AddonEvent.PostRequestedUpdate];
    private readonly IDalamudPluginInterface ui;
    private readonly ICommandManager commands;
    private readonly IFramework framework;
    private readonly IClientState client;
    private readonly IDataManager data;
    private readonly IGameGui gameGui;
    private readonly ICondition conditions;
    private readonly IAddonLifecycle lifecycle;
    private readonly DashboardRetention store;
    private readonly DashboardRetentionPolicy policy;
    private readonly Action persist;
    private readonly DashboardSchedule schedule = new();
    private readonly DashboardDeliveryDiagnostics deliveryDiagnostics = new();
    private readonly HashSet<string> observedThisSession = [];
    private readonly Dictionary<uint, string> rouletteNames = new();
    private uint[]? roulettes, challenges;
    private uint limitedItem;
    private int limitedCap;
    private string activeKey = "";
    private readonly string version = typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "unavailable";
    private bool visible, disposed;
    private int attempts;
    private sealed record View(bool Enabled, string Status, string[] Rows, double Milliseconds, int Attempts, string DeliveryStatus);
    private volatile View view = new(false, "Dashboard fact retention OFF; no upload contract.", [], 0, 0, "Custom Delivery read: not attempted.");
    internal DashboardLocalView(IDalamudPluginInterface ui, ICommandManager commands, IFramework framework,
        IClientState client, IDataManager data, IGameGui gameGui, ICondition conditions, IAddonLifecycle lifecycle,
        DashboardRetention store, Action persist) {
        this.ui = ui; this.commands = commands; this.framework = framework; this.client = client; this.data = data;
        this.gameGui = gameGui; this.conditions = conditions; this.lifecycle = lifecycle; this.store = store;
        this.persist = persist; policy = new(store);
        foreach (var kind in Events) lifecycle.RegisterListener(kind, Addons, OnAddon);
        client.Logout += OnLogout; client.Login += OnLogin; client.TerritoryChanged += OnTerritoryChanged;
        commands.AddHandler("/gillionsfacts", new CommandInfo((_, _) => Show()) { HelpMessage = "Testing private daily/weekly native facts (read-only, no uploads)." });
        ui.UiBuilder.Draw += Draw;
    }
    internal void Show() => visible = true;
    private string GameVersion() => data.GameData.Repositories.TryGetValue("ffxiv", out var repo) ? repo.Version : "unavailable";
    private bool Compatible() => PersonalObservationCompatibility.Supports(GameVersion(), typeof(PlayerState).Assembly.GetName().Version?.ToString());
    private unsafe string CurrentKey() {
        if (!Compatible() || !client.IsLoggedIn || conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51]) return "";
        var player = PlayerState.Instance();
        return player != null && player->IsLoaded && player->ContentId != 0 ? DashboardRetentionPolicy.CharacterKey(player->ContentId) : "";
    }
    private void ResetSession(string status) { ResetEvidence(); schedule.Reset(); Publish(status); }
    private void OnLogout(int _, int __) => ResetSession("Logged out: retained facts STALE, not proof of today's/this week's activity.");
    private void OnLogin() => ResetSession("New session: awaiting naturally loaded facts; prior state STALE.");
    private void OnTerritoryChanged(uint _) => ResetSession("Territory changed: awaiting fresh native evidence; prior state preserved.");
    private void OnAddon(AddonEvent _, AddonArgs args) {
        if (disposed || !store.LocalRetentionEnabled || !framework.IsInFrameworkUpdateThread) return;
        int group = args.AddonName switch { "ContentsFinder" => 0, "SatisfactionSupply" => 1, "ContentsNote" => 2,
            "ContentsInfo" => 5, "WeeklyBingo" => 4, _ => -1 };
        schedule.Notice(group); // managed notice, never opens/refreshes the interface
    }
    private unsafe bool Visible(string name, uint ownerId = 0) {
        var addon = (AtkUnitBase*)gameGui.GetAddonByName(name, 1).Address;
        return addon != null && addon->IsVisible && (ownerId == 0 || addon->Id == ownerId);
    }
    private DashboardObservation Observation(string system, DateTime now, DashboardValue[] values,
        DateTime? next = null, uint scope = 0) => new(system, scope, now, next, values, GameVersion(),
            typeof(PlayerState).Assembly.GetName().Version?.ToString() ?? "unavailable", version);
    private static DateTime? Next(long timestamp, DateTime now, int maxDays = 15) {
        if (timestamp <= 0) return null;
        var seconds = new DateTimeOffset(now).ToUnixTimeSeconds();
        return timestamp > seconds && timestamp - seconds <= maxDays * 86400L ? DateTime.UnixEpoch.AddSeconds(timestamp) : null;
    }
    private unsafe DashboardObservation[] Read(int group, DateTime now) {
        var player = PlayerState.Instance(); // Tick already admitted exact build + own loaded player
        if (player == null || !player->IsLoaded) return [];
        switch (group) {
            case 0: {
                var agent = AgentContentsFinder.Instance();
                if (agent == null || !agent->IsAgentActive() || !Visible("ContentsFinder", agent->AddonId)) return [];
                roulettes ??= data.GetExcelSheet<Lumina.Excel.Sheets.ContentRoulette>()
                    .Where(r => r.IsInDutyFinder && r.CompletionArrayIndex >= 0 && r.RowId is > 0 and <= 255)
                    .Select(r => { rouletteNames[r.RowId] = r.Name.ExtractText(); return r.RowId; }).ToArray();
                if (roulettes.Length is < 1 or > 32) return [];
                var instance = FFXIVClientStructs.FFXIV.Client.Game.UI.InstanceContent.Instance();
                if (instance == null) return [];
                // Getter takes sheet RowId, NOT CompletionArrayIndex. No direct raw
                // array access (installed sheet includes index 10/Normal Raids).
                return [Observation("roulette-reward", now, roulettes.Select(id => new DashboardValue(id,
                    Completed: instance->IsRouletteComplete((byte)id))).ToArray())];
            }
            case 1: {
                deliveryDiagnostics.Record(now, DashboardDeliveryReadStatus.Reading);
                DashboardObservation[] Reject(DashboardDeliveryReadStatus status, DashboardObservation? global = null) {
                    deliveryDiagnostics.Record(now, status);
                    return global is null ? [] : DashboardSources.AdmitCustomDeliveries(global, null);
                }
                var agent = AgentSatisfactionSupply.Instance(); var manager = SatisfactionSupplyManager.Instance();
                if (agent == null) return Reject(DashboardDeliveryReadStatus.AgentUnavailable);
                if (manager == null) return Reject(DashboardDeliveryReadStatus.ManagerUnavailable);
                if (!agent->IsAgentActive() || !Visible("SatisfactionSupply", agent->AddonId)) return Reject(DashboardDeliveryReadStatus.InterfaceClosed);
                if (!agent->NpcInfo.Valid) return Reject(DashboardDeliveryReadStatus.NpcInvalid);
                if (!agent->NpcInfo.Initialized) return Reject(DashboardDeliveryReadStatus.NpcUninitialized);
                if (!agent->NpcInfo.AddonUpdated) return Reject(DashboardDeliveryReadStatus.AddonNotUpdated);
                if (manager->CurrentNpcInitInProgress) return Reject(DashboardDeliveryReadStatus.ManagerInitializing);
                if (!manager->CurrentNpcInitDone) return Reject(DashboardDeliveryReadStatus.ManagerUninitialized);
                var next = Next(new DateTimeOffset(manager->GetResetDateTime()).ToUnixTimeSeconds(), now, 8);
                if (next is null) return Reject(DashboardDeliveryReadStatus.ResetUnavailable);
                int used = manager->GetUsedAllowances();
                var global = Observation("custom-deliveries-global", now, [new(0, Progress: used, Limit: 12, Remaining: 12 - used)], next);
                var npc = agent->NpcData;
                var clients = data.GetExcelSheet<SatisfactionNpc>();
                // Match ENpcResident identity; do not guess CurrentNpc index convention.
                var matches = clients.Where(r => r.RowId is >= 1 and <= 12 && r.Npc.RowId == npc.NpcId).ToArray();
                if (matches.Length != 1) return Reject(DashboardDeliveryReadStatus.ClientCatalogMismatch, global);
                if (npc.MaxAllowances != matches[0].DeliveriesPerWeek) return Reject(DashboardDeliveryReadStatus.ClientAllowanceMismatch, global);
                if (npc.RankMax != 5) return Reject(DashboardDeliveryReadStatus.ClientRankMismatch, global);
                if (npc.RemainingAllowances != npc.MaxAllowances - npc.UsedAllowances) return Reject(DashboardDeliveryReadStatus.ClientCounterMismatch, global);
                var selected = Observation("custom-deliveries-client", now, [
                        new(0, Progress: npc.UsedAllowances, Limit: npc.MaxAllowances, Remaining: npc.RemainingAllowances),
                        npc.RankCur == 5 && npc.SatisfactionMax == 0 ? new(1)
                            : new(1, Progress: npc.SatisfactionCur, Limit: npc.SatisfactionMax),
                        new(2, Progress: npc.RankCur, Limit: npc.RankMax)
                    ], next, matches[0].RowId);
                // A selected client's unavailable rank-progress detail must not
                // erase or suppress a separately valid global allowance fact.
                var rows = DashboardSources.AdmitCustomDeliveries(global, selected);
                deliveryDiagnostics.Record(now, rows.Length == 2 ? DashboardDeliveryReadStatus.ObservedBoth
                    : rows.Length == 0 ? DashboardDeliveryReadStatus.InvalidFacts
                    : rows[0].System == "custom-deliveries-global" ? DashboardDeliveryReadStatus.ObservedGlobal : DashboardDeliveryReadStatus.ObservedClient);
                return rows;
            }
            case 2: {
                var note = FFXIVClientStructs.FFXIV.Client.Game.UI.ContentsNote.Instance();
                if (!Visible("ContentsNote") || note == null || note->State != FFXIVClientStructs.FFXIV.Client.Game.UI.ContentsNote.ContentsNoteState.Loaded) return [];
                challenges ??= data.GetExcelSheet<Lumina.Excel.Sheets.ContentsNote>()
                    .Where(r => r.RowId is >= 1 and <= 104 && r.RequiredAmount > 0).Select(r => r.RowId).ToArray();
                var state = UIState.Instance();
                var next = state == null ? null : Next(state->NextChallengeLogResetTimestamp, now, 8);
                // Completion only: DisplayStatuses has no verified progress semantics.
                return challenges.Length == 0 ? [] : [Observation("challenge-log", now,
                    challenges.Select(id => new DashboardValue(id, Completed: note->IsContentNoteComplete((int)id))).ToArray(), next)];
            }
            case 3: {
                var inventory = InventoryManager.Instance();
                var currency = inventory == null ? null : inventory->GetInventoryContainer(InventoryType.Currency);
                if (currency == null || !currency->IsLoaded || currency->Items == null || currency->Size is < 1 or > 256) return [];
                if (limitedItem == 0) {
                    var items = data.GetExcelSheet<TomestonesItem>().Where(r => r.Item.RowId > 0 && r.Tomestones.IsValid
                        && r.Tomestones.Value.WeeklyLimit > 0).ToArray();
                    if (items.Length != 1) return [];
                    limitedItem = items[0].Item.RowId; limitedCap = items[0].Tomestones.Value.WeeklyLimit;
                }
                int cap = InventoryManager.GetLimitedTomestoneWeeklyLimit();
                if (cap != limitedCap) return []; // catalog/native identity mismatch fails closed
                int earned = inventory->GetWeeklyAcquiredTomestoneCount();
                return [Observation("weekly-tomestones", now, [new(limitedItem, Progress: earned, Limit: cap, Remaining: cap - earned)])];
            }
            case 4: {
                if (!player->HasWeeklyBingoJournal) return []; // no journal flag is NOT proof of turn-in
                var next = Next(player->GetWeeklyBingoExpireUnixTimestamp(), now);
                if (next is null) return []; // expired/unloaded journal preserves last trusted observation
                var catalog = data.GetExcelSheet<WeeklyBingoOrderData>();
                var values = new List<DashboardValue>(18) {
                    new(0, Progress: player->WeeklyBingoNumPlacedStickers, Limit: 9, Available: true),
                    new(1, Progress: (int)player->WeeklyBingoNumSecondChancePoints, Limit: 9, Available: true)
                };
                for (int index = 0; index < 16; index++) {
                    uint order = player->WeeklyBingoOrderData[index];
                    if (order == 0 || !catalog.HasRow(order)) return [];
                    int status = (int)player->GetWeeklyBingoTaskStatus(index);
                    values.Add(new((uint)index + 2, order, Progress: status, Completed: status is 1 or 2));
                }
                return [Observation("wondrous-tails", now, values.ToArray(), next)];
            }
            case 5: {
                // Agent type is ContentsTimer, but its actual UI addon is
                // ContentsInfo. Require the active agent's matching visible UI.
                var timer = AgentContentsTimer.Instance();
                if (timer == null || !timer->IsAgentActive() || !Visible("ContentsInfo", timer->AddonId)) return [];
                var quest = QuestManager.Instance(); var state = UIState.Instance();
                if (quest == null || state == null) return [];
                var rows = new List<DashboardObservation>(5);
                var leveNext = Next(QuestManager.GetNextLeveAllowancesUnixTimestamp(), now, 1);
                if (leveNext is not null) rows.Add(Observation("leve-allowance", now,
                    [new(0, Limit: 100, Remaining: quest->NumLeveAllowances)], leveNext));
                uint society = quest->GetBeastTribeAllowance();
                if (society <= 12) rows.Add(Observation("society-allowance", now, [new(0, Limit: 12, Remaining: (int)society)]));
                var map = Next(state->NextMapAllowanceTimestamp, now, 2);
                if (map is not null) rows.Add(Observation("map-availability", now, [new(0)], map));
                var mission = Next(player->SquadronMissionCompletionTimestamp, now, 2);
                if (mission is not null) rows.Add(Observation("squadron-mission", now, [new(0)], mission));
                var training = Next(player->SquadronTrainingCompletionTimestamp, now, 2);
                if (training is not null) rows.Add(Observation("squadron-training", now, [new(0)], training));
                return rows.Where(DashboardRetentionPolicy.Valid).ToArray();
            }
            case 6: {
                var profile = PvPProfile.Instance();
                if (profile == null || !profile->IsLoaded || profile->RivalWingsWeeklyMatches > 10000 || profile->RivalWingsWeeklyMatchesWon > 10000) return [];
                return [Observation("frontline-weekly", now, [
                    new(0, Progress: profile->FrontlineWeeklyMatches), new(1, Progress: profile->FrontlineWeeklyFirstPlace),
                    new(2, Progress: profile->FrontlineWeeklySecondPlace), new(3, Progress: profile->FrontlineWeeklyThirdPlace)
                ]), Observation("rival-wings-weekly", now, [
                    new(0, Progress: (int)profile->RivalWingsWeeklyMatches), new(1, Progress: (int)profile->RivalWingsWeeklyMatchesWon)
                ])];
            }
            case 7: {
                // Naturally populated typed manager; no donation/window action or
                // game request. Loaded cache is still not reset/ownership proof.
                var doma = DomanEnclaveManager.Instance();
                if (doma == null || !doma->IsLoaded) return [];
                var state = doma->State;
                if (state.Allowance == 0 || state.Donated > state.Allowance) return [];
                return [Observation("doman-enclave-weekly", now,
                    [new(0, Progress: state.Donated, Limit: state.Allowance, Remaining: state.Allowance - state.Donated,
                        Available: state.IsAcceptingDonations)])];
            }
            default: return [];
        }
    }
    internal void Tick(DateTime now) {
        if (disposed || !framework.IsInFrameworkUpdateThread) return;
        int? group = schedule.Begin(now, store.LocalRetentionEnabled); if (group is null) return;
        attempts++;
        if (!Compatible()) { ResetEvidence(); Publish("UNSUPPORTED game/SDK: native reads/export stopped; retained facts preserved."); return; }
        if (!policy.Supported) { ResetEvidence(); Publish("UNSUPPORTED retained schema/bounds: collection/export stopped; data preserved."); return; }
        long started = Stopwatch.GetTimestamp();
        try {
            var key = CurrentKey();
            if (key.Length == 0) { ResetEvidence(); Publish("UNAVAILABLE player/session: retained facts preserved, not zero."); return; }
            if (key != activeKey) { ResetEvidence(); activeKey = key; }
            var observations = Read(group.Value, now);
            // Atomic whole-group admission; invalid/missing source never produces empty replacement.
            bool valid = observations.Length > 0 && observations.All(DashboardRetentionPolicy.Valid);
            if (valid) {
                if (policy.Observe(key, observations)) persist();
                var retained = policy.Rows(key);
                foreach (var row in observations) {
                    var identity = DashboardRetentionPolicy.Identity(row);
                    observedThisSession.Remove(identity);
                    if (retained.Any(old => DashboardRetentionPolicy.Equivalent(old, row))) observedThisSession.Add(identity);
                }
            }
            Publish(!valid ? $"Source group {group} UNAVAILABLE/UNKNOWN/invalid; prior state preserved, not zero."
                : $"Observed {observations.Length} bounded fact groups; cache ownership/reset response unverified. No automatic checklist completion or uploads.",
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        } catch (Exception) { ResetEvidence();
            if (group == 1) deliveryDiagnostics.Record(now, DashboardDeliveryReadStatus.ReadFailed);
            Publish("Read/save failed; retained facts preserved, new data may be memory-only. No live correctness claim."); }
    }
    private void ResetEvidence() { activeKey = ""; observedThisSession.Clear(); deliveryDiagnostics.Reset(); }
    private void Publish(string status, double milliseconds = 0) {
        var now = DateTime.UtcNow;
        var rows = policy.Rows(activeKey).OrderBy(DashboardRetentionPolicy.Identity).SelectMany(o => new[] {
            $"{o.System} / scope {o.ScopeId}: {DashboardRetentionPolicy.Freshness(o, now, observedThisSession.Contains(DashboardRetentionPolicy.Identity(o)))} at {o.ObservedAtUtc:u}; next: {o.NextAtUtc?.ToString("u") ?? "UNKNOWN"}"
        }.Concat(o.Values.Select(v => $"  {(o.System == "roulette-reward" && rouletteNames.TryGetValue(v.Id, out var name) ? name : "Reference " + v.Id)} / related {v.RelatedId}: progress {v.Progress?.ToString() ?? "UNKNOWN"}, limit {v.Limit?.ToString() ?? "UNKNOWN"}, remaining {v.Remaining?.ToString() ?? "UNKNOWN"}, completed {v.Completed?.ToString() ?? "UNKNOWN"}, eligibility {v.Available?.ToString() ?? "UNKNOWN"}"))).ToArray();
        if (store.CapacityReached) status += " CAPACITY: new records paused; nothing evicted.";
        view = new(store.LocalRetentionEnabled, status, rows, milliseconds, attempts, deliveryDiagnostics.Text);
    }
    private void Draw() {
        if (!visible || disposed) return;
        var state = view;
        ImGui.SetNextWindowSize(new Vector2(850, 520), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Private daily / weekly facts###GillionsDashboardFacts", ref visible)) {
            ImGui.TextWrapped("Testing, OFF by default. Read-only native facts, not Dashboard checklist/configuration. No new uploads. One bounded source group per five seconds; naturally opened interfaces only where needed. Native caches may be stale or have unverified ownership/reset response; this experiment does NOT prove checklist completion.");
            bool enabled = state.Enabled;
            if (ImGui.Checkbox("Retain private daily / weekly observations locally", ref enabled))
                _ = framework.RunOnFrameworkThread(() => { if (disposed) return; store.LocalRetentionEnabled = enabled;
                    ResetSession(enabled ? "Enabled: awaiting observations; retained state STALE." : "Disabled: no new reads/export; prior state preserved."); persist(); });
            ImGui.TextWrapped(state.Status);
            ImGui.TextWrapped(state.DeliveryStatus);
            ImGui.TextWrapped($"Read/save: {state.Milliseconds:F2} ms; attempts {state.Attempts}. At most 16 characters / 24 latest groups each / 384 KiB; no history eviction. Data and activity times are PRIVATE. Configuration includes unrelated credentials: never share it.");
            if (state.Enabled && ImGui.Button("Copy PRIVATE facts JSON (no upload)"))
                _ = framework.RunOnFrameworkThread(() => {
                    if (disposed) return;
                    try {
                        var key = CurrentKey();
                        if (key.Length == 0 || key != activeKey) throw new InvalidOperationException();
                        ImGui.SetClipboardText(policy.Export(key, DateTime.UtcNow, observedThisSession));
                    } catch (Exception) { Publish("Private export refused: off, unsupported or active character unavailable. Retention preserved."); }
                });
            if (ImGui.Button("Copy aggregate facts diagnostics")) ImGui.SetClipboardText($"Gillions Testing {version}\nGame {GameVersion()}; SDK {typeof(PlayerState).Assembly.GetName().Version}\nFacts local: {state.Enabled}\n{state.Status}\n{state.DeliveryStatus}\nAttempts {state.Attempts}; cadence 5 seconds, one group\nRead/save {state.Milliseconds:F2} ms\nNo upload; no live correctness claim.");
            ImGui.PushTextWrapPos(0); foreach (var row in state.Rows) ImGui.TextUnformatted(row); ImGui.PopTextWrapPos();
        }
        ImGui.End();
    }
    public void Dispose() {
        if (disposed) return; disposed = true; ResetEvidence();
        foreach (var kind in Events) lifecycle.UnregisterListener(kind, Addons, OnAddon);
        client.Logout -= OnLogout; client.Login -= OnLogin; client.TerritoryChanged -= OnTerritoryChanged;
        ui.UiBuilder.Draw -= Draw; commands.RemoveHandler("/gillionsfacts");
    }
}
#endif
