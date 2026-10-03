#if GILLIONS_TEST_BUILD
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace GillionsGameSync;

// Existing SDK reads only. No object-table scan, game requests, native writes,
// per-frame native collection, network transport, or third-party plugin dependency.
internal sealed class HuntBillLocalView : IDisposable {
    private readonly IDalamudPluginInterface ui;
    private readonly ICommandManager commands;
    private readonly IFramework framework;
    private readonly IClientState client;
    private readonly IDataManager data;
    private readonly ICondition conditions;
    private readonly HuntBillRetention store;
    private readonly HuntBillRetentionPolicy policy;
    private readonly HuntTargetPresentation presentation;
    private readonly System.Action persist;
    private readonly string collectorVersion = typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "unavailable";
    private Dictionary<uint, MobHuntOrderType>? types;
    private readonly Dictionary<uint, HuntBillTarget[]> targets = new();
    private readonly HuntObservationSchedule schedule = new();
    private int attempts;
    private DateTime? lastAttemptUtc;
    private bool visible, disposed;
    private string export = "";
    private sealed record View(bool Enabled, string Status, string[] Rows, double Milliseconds,
        int Attempts = 0, DateTime? LastAttemptUtc = null);
    private volatile View view = new(false, "Hunt retention off. Private TEST sync is controlled separately in the main window.", [], 0);

    internal HuntBillLocalView(IDalamudPluginInterface ui, ICommandManager commands, IFramework framework,
        IClientState client, IDataManager data, ICondition conditions, HuntBillRetention store, System.Action persist) {
        this.ui = ui; this.commands = commands; this.framework = framework; this.client = client;
        this.data = data; this.conditions = conditions; this.store = store; this.persist = persist;
        policy = new(store);
        presentation = new(id => {
            var sheet = data.GetExcelSheet<BNpcName>();
            return id > 0 && sheet.HasRow(id) ? sheet.GetRow(id).Singular.ExtractText() : null;
        });
        client.Logout += OnLogout;
        commands.AddHandler("/gillionshunts", new CommandInfo((_, _) => Show()) { HelpMessage = "Testing read-only Hunt Bill local observations (no uploads)." });
        ui.UiBuilder.Draw += Draw;
        Publish(policy.Status);
    }
    internal void Show() => visible = true;
    private string GameVersion() => data.GameData.Repositories.TryGetValue("ffxiv", out var repo) ? repo.Version : "unavailable";
    private unsafe string CurrentCharacterKey() {
        if (!PersonalObservationCompatibility.Supports(GameVersion(), typeof(MobHunt).Assembly.GetName().Version?.ToString())) return "";
        var player = PlayerState.Instance();
        return client.IsLoggedIn && player != null && player->IsLoaded && player->ContentId != 0
            ? HuntBillRetentionPolicy.CharacterKey(player->ContentId) : "";
    }
    private void OnLogout(int _, int __) { export = ""; schedule.Reset(); Publish("Logged out; retained Hunt state is historical, not current. Private sync paused."); }
    private void Publish(string status, string characterKey = "", double milliseconds = 0) {
        var rows = policy.Supported ? store.Characters.SingleOrDefault(c => c.LocalCharacterKey == characterKey)?.Bills
            .OrderBy(b => b.BillTypeId).SelectMany(b => new[] {
                $"Bill {b.BillTypeId} ({b.Category}, tier {b.Tier}) | order {b.OrderId}",
                $"Retained observation {b.ObservedAtUtc:u}; current acceptance/cache ownership unverified"
            }.Concat(b.Targets.OrderBy(t => t.TargetIndex).Select(presentation.TargetLine))).ToArray() ?? [] : [];
        view = new(store.LocalRetentionEnabled, status, rows, milliseconds, attempts, lastAttemptUtc);
    }
    private HuntBillTarget[]? CatalogTargets(uint orderId, MobHuntOrderType type) {
        if (targets.TryGetValue(orderId, out var cached)) return cached;
        if (orderId < type.OrderStart.RowId || orderId >= type.OrderStart.RowId + type.OrderAmount) return null;
        var orders = data.GetSubrowExcelSheet<MobHuntOrder>();
        if (!orders.HasRow(orderId)) return null;
        var rows = orders.GetRow(orderId).ToArray();
        if (rows.Length is < 1 or > 5 || rows.Select(r => r.SubrowId).Distinct().Count() != rows.Length) return null;
        var catalog = data.GetExcelSheet<MobHuntTarget>();
        var result = new List<HuntBillTarget>(5);
        foreach (var row in rows.OrderBy(r => r.SubrowId)) {
            if (row.SubrowId >= 5 || row.NeededKills == 0 || !catalog.HasRow(row.Target.RowId)) return null;
            var target = catalog.GetRow(row.Target.RowId);
            if (target.RowId == 0 || target.Name.RowId == 0 || !data.GetExcelSheet<BNpcName>().HasRow(target.Name.RowId)) return null;
            result.Add(new((byte)row.SubrowId, target.RowId, target.Name.RowId, target.Map.RowId,
                target.PlaceName.RowId, target.FATE.RowId, row.NeededKills, 0, row.Type, row.Rank));
        }
        // At most the observed 22 bill categories' finite catalog groups; cache
        // unknown IDs nowhere and do not scan unrelated sheets on every event.
        if (targets.Count < 512) targets[orderId] = result.ToArray();
        return result.ToArray();
    }
    // Called by the plugin's existing framework update. The due check precedes
    // all native access; no new subscription, requests or interface dependency.
    internal unsafe void Tick(DateTime now) {
        if (disposed || !schedule.TryBegin(now, store.LocalRetentionEnabled)) return;
        if (!framework.IsInFrameworkUpdateThread) { Publish("Hunt read requires framework thread; no native access."); return; }
        attempts++; lastAttemptUtc = now;
        if (!PersonalObservationCompatibility.Supports(GameVersion(), typeof(MobHunt).Assembly.GetName().Version?.ToString())) {
            Publish("Unsupported game/SDK build; no Hunt native read. Retained state preserved."); return;
        }
        long started = Stopwatch.GetTimestamp();
        try {
            if (!policy.Supported) { Publish(policy.Status); return; }
            var characterKey = CurrentCharacterKey();
            if (characterKey.Length == 0 || conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51]) {
                export = ""; Publish("Hunt player unavailable or transitioning; prior observations preserved, not empty."); return;
            }
            var manager = InventoryManager.Instance();
            var keyItems = manager == null ? null : manager->GetInventoryContainer(InventoryType.KeyItems);
            if (keyItems == null || !keyItems->IsLoaded || keyItems->Items == null || keyItems->Size is < 1 or > 256) {
                export = ""; Publish("Key Items unavailable/not loaded; no Hunt cache admitted. Prior state preserved.", characterKey); return;
            }
            var hunt = MobHunt.Instance();
            if (hunt == null || (hunt->ObtainedFlags & ~((1 << 22) - 1)) != 0) {
                Publish("Hunt native source unavailable/incompatible; prior state preserved.", characterKey); return;
            }
            types ??= data.GetExcelSheet<MobHuntOrderType>().Where(t => t.RowId < 22).ToDictionary(t => t.RowId);
            var presentItems = new HashSet<uint>();
            for (int slot = 0; slot < keyItems->Size; slot++) {
                var item = keyItems->Items[slot];
                if (item.Quantity > 0 && item.ItemId > 0) presentItems.Add(item.ItemId);
            }
            var observations = new List<HuntBillObservation>(22);
            int partial = 0;
            for (byte index = 0; index < 22; index++) {
                if ((hunt->ObtainedFlags & (1 << index)) == 0) continue; // Not proof of absent/completed/reset bill.
                if (!types.TryGetValue(index, out var type) || type.Type is not (1 or 2) || type.EventItem.RowId == 0) { partial++; continue; }
                if (!HuntObservationAdmission.CanUseBillCache(index, hunt->ObtainedFlags, true,
                    type.EventItem.RowId, presentItems.Contains(type.EventItem.RowId))) { partial++; continue; }
                int obtained = hunt->GetObtainedHuntOrderRowId(index); // Existing read-only row-ID getter, no request.
                var catalog = obtained > 0 ? CatalogTargets((uint)obtained, type) : null;
                if (catalog is null) { partial++; continue; }
                var read = catalog.Select(t => t with { ObservedKills = hunt->CurrentKills[index].Counts[t.TargetIndex] }).ToArray();
                bool weekly = type.Type == 2;
                byte tier = weekly || index == 0 ? (byte)1 : (byte)((index <= 3 ? index - 1 : (index - 6) % 4) + 1);
                var observation = new HuntBillObservation(index, weekly ? "weekly" : "daily", tier,
                    (uint)obtained, type.EventItem.RowId, read, now, GameVersion(), collectorVersion) {
                    SourceEvidence = HuntObservationAdmission.KeyItemEvidence
                };
                if (HuntBillRetentionPolicy.BillValid(observation)) observations.Add(observation); else partial++;
            }
            if (policy.Observe(characterKey, observations.ToArray())) { export = ""; persist(); }
            var status = observations.Count == 0 ? $"No corroborated bills observed; {partial} partial/unmatched. NOT proof of no bills. Prior state preserved."
                : $"Observed {observations.Count} bill caches with matching loaded Key Items; {partial} partial. Bill windows not required. Current order/cache ownership/reset unverified. Private TEST sync status is in the main window.";
            if (store.CapacityReached) status += " Retention capacity reached: new character observations paused; existing history preserved.";
            Publish(status, characterKey, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        } catch (Exception) { Publish("Hunt read/save failed; history preserved. New data may be memory-only. No live correctness claim."); }
    }
    private void Draw() {
        if (!visible || disposed) return;
        var state = view;
        ImGui.SetNextWindowSize(new Vector2(780, 500), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("My Hunt Bills local test###GillionsHuntBills", ref visible)) {
            ImGui.TextWrapped("Testing, read-only, local-first. Every five seconds, observe naturally loaded bill caches corroborated by loaded Key Items. No bill window required. No radar, mob scans, UI opening or game requests. Missing data never clears retained bills. Private TEST uploads require separate permission; see the main window for sync controls/status.");
            bool enabled = state.Enabled;
            if (ImGui.Checkbox("Retain naturally loaded Hunt Bill observations locally", ref enabled)) {
                export = "";
                _ = framework.RunOnFrameworkThread(() => { if (disposed) return; store.LocalRetentionEnabled = enabled; schedule.Reset(); persist(); Publish(policy.Status); });
            }
            ImGui.TextWrapped(state.Status);
            ImGui.TextWrapped($"Last cache read/save: {state.Milliseconds:F2} ms. Up to 16 characters / 22 bill categories each / 256 KiB. Latest positive observation per bill; no reset, absence or cache-owner proof.");
            ImGui.TextWrapped("Private export contains your targets, counters and activity times. No credential, account/character ID or name. Copy only to a trusted diagnostic recipient; this is not community contribution or a server upload.");
            if (enabled && ImGui.Button("Prepare PRIVATE Hunt JSON (no upload)")) {
                _ = framework.RunOnFrameworkThread(() => {
                    if (disposed) return;
                    try { export = policy.PreparePrivateExport(CurrentCharacterKey()); }
                    catch (Exception) { export = ""; Publish("Private export unavailable: no retained observation for active character, retention off or unsupported format."); }
                });
            }
            if (enabled && export.Length > 0 && ImGui.Button("Copy PRIVATE Hunt JSON")) ImGui.SetClipboardText(export);
            if (ImGui.Button("Copy aggregate Hunt diagnostics")) ImGui.SetClipboardText($"Gillions Game Sync Testing {collectorVersion}\nGame: {GameVersion()}; SDK: {typeof(MobHunt).Assembly.GetName().Version}\nHunts local: {state.Enabled}\n{state.Status}\nAttempts: {state.Attempts}; last attempt UTC: {state.LastAttemptUtc:u}; cadence: 5 seconds\nRead/save: {state.Milliseconds:F2} ms\nLocal collection diagnostics only; private TEST sync controls/status are in the main window. No live correctness claim.");
            ImGui.PushTextWrapPos(0);
            foreach (var row in state.Rows) ImGui.TextUnformatted(row);
            ImGui.PopTextWrapPos();
        }
        ImGui.End();
    }
    public void Dispose() {
        if (disposed) return; disposed = true; export = "";
        client.Logout -= OnLogout; ui.UiBuilder.Draw -= Draw; commands.RemoveHandler("/gillionshunts");
    }
}
#endif
