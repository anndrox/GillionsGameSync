#if GILLIONS_TEST_BUILD
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Game.Chat;
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
    private readonly IChatGui chat;
    private readonly HuntBillRetention store;
    private readonly HuntBillRetentionPolicy policy;
    private readonly HuntTargetPresentation presentation;
    private readonly System.Action persist;
    private readonly string collectorVersion = typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "unavailable";
    private Dictionary<uint, MobHuntOrderType>? types;
    private readonly Dictionary<uint, HuntBillTarget[]> targets = new();
    private readonly HuntObservationSchedule schedule = new();
    private readonly HuntSessionProgress session = new();
    private readonly HuntBillItemCoverage itemCoverage = new();
    internal long CoverageRevision => itemCoverage.Revision;
    internal long CoverageEpoch => itemCoverage.Epoch;
    internal void ClearCoverage() => itemCoverage.Clear();
    internal string? CoveragePayload(string key, DateTime now) => store.LocalRetentionEnabled
        ? itemCoverage.Payload(policy, key, now, Stopwatch.GetTimestamp()) : null;
    private string rawFingerprint = "";
    private string lastProgressDiagnostic = "Structured Hunt progress not observed in this session.";
    internal long SemanticRevision { get; private set; }
    internal long RawRevision { get; private set; }
    internal string LastDiagnostic { get; private set; } = "Hunt observation not attempted.";
    private int attempts;
    private DateTime? lastAttemptUtc;
    private bool visible, disposed;
    private string export = "";
    private sealed record View(bool Enabled, string Status, string[] Rows, double Milliseconds,
        int Attempts = 0, DateTime? LastAttemptUtc = null, string CoverageStatus = "Hunt item coverage UNAVAILABLE.",
        DateTime? CoverageObservedAtUtc = null, string[]? CoverageRows = null);
    private volatile View view = new(false, "Hunt retention off. Private TEST sync is controlled separately in the main window.", [], 0);

    internal HuntBillLocalView(IDalamudPluginInterface ui, ICommandManager commands, IFramework framework,
        IClientState client, IDataManager data, ICondition conditions, IChatGui chat, HuntBillRetention store, System.Action persist) {
        this.ui = ui; this.commands = commands; this.framework = framework; this.client = client;
        this.data = data; this.conditions = conditions; this.store = store; this.persist = persist;
        this.chat = chat;
        policy = new(store);
        presentation = new(id => {
            var sheet = data.GetExcelSheet<BNpcName>();
            return id > 0 && sheet.HasRow(id) ? sheet.GetRow(id).Singular.ExtractText() : null;
        });
        client.Logout += OnLogout;
        client.TerritoryChanged += OnTerritoryChanged;
        conditions.ConditionChange += OnConditionChange;
        chat.LogMessage += OnProgressMessage;
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
    private void OnLogout(int _, int __) { ClearLiveBaseline(); Publish("Logged out; retained Hunt state is historical, not current. Private sync paused."); }
    private void ClearLiveBaseline() { export = ""; schedule.Reset(); session.Reset(); itemCoverage.Clear(); rawFingerprint = ""; lastProgressDiagnostic = "Structured Hunt progress cleared on session/transition."; }
    private void OnProgressMessage(ILogMessage message) {
        // The event wrapper is borrowed. Copy only three typed numeric values
        // synchronously; never retain it, strings, entity data or chat content.
        if (disposed || !store.LocalRetentionEnabled || !framework.IsInFrameworkUpdateThread
            || message.LogMessageId != HuntProgressMessage.LogId) return;
        if (!PersonalObservationCompatibility.Supports(GameVersion(), typeof(MobHunt).Assembly.GetName().Version?.ToString())
            || conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51]) return;
        var key = CurrentCharacterKey();
        if (key.Length == 0) return;
        session.Bind(key);
        if (message.ParameterCount != 4 || !message.TryGetIntParameter(1, out var npc) || npc <= 0
            || !message.TryGetIntParameter(2, out var count) || !message.TryGetIntParameter(3, out var required)) {
            lastProgressDiagnostic = "Hunt log4411 parameter shape unsupported; no progress inferred."; return;
        }
        var proof = new HuntProgressMessage((uint)npc, count, required, DateTime.UtcNow);
        bool bound = session.Queue(proof);
        lastProgressDiagnostic = $"Hunt log4411 at {proof.ObservedAtUtc:u}: NPC name ID {npc}; count {count}/{required}; {(bound ? "uniquely bound to recent session bill; awaiting fresh order/gate check" : "rejected: invalid/ambiguous/unassociated session proof")}.";
    }
    private void OnTerritoryChanged(uint _) { ClearLiveBaseline(); Publish("Territory changed; prior Hunt observations preserved. Await fresh corroboration."); }
    private void OnConditionChange(ConditionFlag flag, bool value) {
        if (value && flag is ConditionFlag.BetweenAreas or ConditionFlag.BetweenAreas51) ClearLiveBaseline();
    }
    private void Publish(string status, string characterKey = "", double milliseconds = 0) {
        var rows = policy.Supported ? store.Characters.SingleOrDefault(c => c.LocalCharacterKey == characterKey)?.Bills
            .OrderBy(b => b.BillTypeId).SelectMany(b => new[] {
                $"Bill {b.BillTypeId} ({b.Category}, tier {b.Tier}) | order {b.OrderId}",
                $"Retained observation {b.ObservedAtUtc:u}; current acceptance/cache ownership unverified"
            }.Concat(b.Targets.OrderBy(t => t.TargetIndex).Select(presentation.TargetLine))).ToArray() ?? [] : [];
        var coverage = itemCoverage.Current(characterKey, DateTime.UtcNow, Stopwatch.GetTimestamp());
        view = new(store.LocalRetentionEnabled, status, rows, milliseconds, attempts, lastAttemptUtc, itemCoverage.Status,
            coverage?.ObservedAtUtc, coverage?.Domains.Select(d => $"Bill item {d.BillTypeId} / key item {d.KeyItemId}: {d.State}").ToArray());
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
    // all native access; no new polling subscription, requests or interface dependency.
    // Supported lifecycle subscriptions only invalidate the RAM admission baseline.
    internal unsafe void Tick(DateTime now, bool force = false) {
        if (!store.LocalRetentionEnabled) { session.Reset(); itemCoverage.Clear(); }
        if (force) LastDiagnostic = "Hunt read not due/eligible (one-second manual admission bound); retained state preserved.";
        if (disposed || !schedule.TryBegin(now, store.LocalRetentionEnabled, force)) return;
        LastDiagnostic = "Hunt fresh source unavailable; retained state preserved, no completion inferred.";
        if (!framework.IsInFrameworkUpdateThread) { itemCoverage.Clear(); Publish("Hunt read requires framework thread; no native access."); return; }
        attempts++; lastAttemptUtc = now;
        if (!PersonalObservationCompatibility.Supports(GameVersion(), typeof(MobHunt).Assembly.GetName().Version?.ToString())) {
            itemCoverage.Clear();
            Publish("Unsupported game/SDK build; no Hunt native read. Retained state preserved."); return;
        }
        long started = Stopwatch.GetTimestamp();
        try {
            if (!policy.Supported) { itemCoverage.Clear(); Publish(policy.Status); return; }
            var characterKey = CurrentCharacterKey();
            if (characterKey.Length == 0 || conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51]) {
                session.Reset(); itemCoverage.Clear(); rawFingerprint = "";
                export = ""; Publish("Hunt player unavailable or transitioning; prior observations preserved, not empty."); return;
            }
            session.Bind(characterKey);
            types ??= data.GetExcelSheet<MobHuntOrderType>().Where(t => t.RowId < 22).ToDictionary(t => t.RowId);
            var domains = types.Values.Select(t => new HuntBillItemDomain((byte)t.RowId, t.EventItem.RowId, t.Type, t.OrderStart.RowId, t.OrderAmount)).ToArray();
            itemCoverage.SetCatalog(domains.All(d => data.GetExcelSheet<EventItem>().HasRow(d.KeyItemId)) ? domains : []);
            var manager = InventoryManager.Instance();
            var keyItems = manager == null ? null : manager->GetInventoryContainer(InventoryType.KeyItems);
            if (keyItems == null || !keyItems->IsLoaded || keyItems->Items == null || keyItems->Size is < 1 or > 256) {
                itemCoverage.Observe(characterKey, CurrentCharacterKey(), true, false, 0, 0, [], false, now, Stopwatch.GetTimestamp(), GameVersion(), collectorVersion, typeof(MobHunt).Assembly.GetName().Version!.ToString());
                export = ""; Publish("Key Items unavailable/not loaded; no Hunt cache admitted. Prior state preserved.", characterKey); return;
            }
            // Two complete copies, same pointer/header and current character.
            // Validate every slot before a negative fact; symbolic items fail closed.
            var pointer = keyItems->Items; var size = keyItems->Size; var container = keyItems->Type;
            HuntKeyItemSlot[] CopySlots() {
                var result = new HuntKeyItemSlot[size];
                for (int i = 0; i < size; i++) { var s = pointer[i]; result[i] = new(s.Slot, (int)s.Container, s.IsSymbolic, s.ItemId, s.Quantity); }
                return result;
            }
            var firstSlots = CopySlots();
            var secondSlots = keyItems->Items == pointer && keyItems->Size == size && keyItems->Type == container && keyItems->IsLoaded ? CopySlots() : [];
            bool stable = keyItems->Items == pointer && keyItems->Size == size && keyItems->Type == container && keyItems->IsLoaded
                && firstSlots.SequenceEqual(secondSlots) && keyItems->GetSize() == size
                && firstSlots.All(s => s.ItemId == 0 || data.GetExcelSheet<EventItem>().HasRow(s.ItemId))
                && Stopwatch.GetElapsedTime(started) <= TimeSpan.FromMilliseconds(100);
            itemCoverage.Observe(characterKey, CurrentCharacterKey(), !conditions[ConditionFlag.BetweenAreas] && !conditions[ConditionFlag.BetweenAreas51], true,
                (int)container, size, firstSlots, stable, now, Stopwatch.GetTimestamp(), GameVersion(), collectorVersion, typeof(MobHunt).Assembly.GetName().Version!.ToString());
            var hunt = MobHunt.Instance();
            if (hunt == null || (hunt->ObtainedFlags & ~((1 << 22) - 1)) != 0) {
                Publish("Hunt native source unavailable/incompatible; prior state preserved.", characterKey); return;
            }
            var presentItems = new HashSet<uint>();
            for (int slot = 0; slot < keyItems->Size; slot++) {
                var item = keyItems->Items[slot];
                if (item.Quantity > 0 && item.ItemId > 0) presentItems.Add(item.ItemId);
            }
            var observations = new List<HuntBillObservation>(22);
            var raw = new List<string>(22);
            var details = new List<string>(132);
            int partial = 0;
            for (byte index = 0; index < 22; index++) {
                if (!types.TryGetValue(index, out var type) || type.Type is not (1 or 2) || type.EventItem.RowId == 0) { partial++; continue; }
                bool corroborated = HuntObservationAdmission.CanUseBillCache(index, hunt->ObtainedFlags, true,
                    type.EventItem.RowId, presentItems.Contains(type.EventItem.RowId));
                if (!corroborated && !session.MayReadFinal(index, now)) continue;
                int obtained = hunt->GetObtainedHuntOrderRowId(index); // Existing read-only row-ID getter, no request.
                int available = hunt->GetAvailableHuntOrderRowId(index); // Diagnostic identity only; never substitute for obtained.
                var catalog = obtained > 0 ? CatalogTargets((uint)obtained, type) : null;
                if (catalog is null) { partial++; continue; }
                var counters = catalog.Select(t => new HuntCounterReading(hunt->CurrentKills[index].Counts[t.TargetIndex],
                    hunt->GetKillCount(index, t.TargetIndex))).ToArray();
                var read = catalog.Select((t, n) => t with { ObservedKills = counters[n].Accessor }).ToArray();
                raw.Add($"{index}:{obtained}:{available}:{corroborated}:{string.Join(',', counters.Select(c => $"{c.Raw}/{c.Accessor}"))}");
                details.Add($"Bill {index}: obtained order {obtained}; available order {available}; flag={((hunt->ObtainedFlags & (1 << index)) != 0)}; item={presentItems.Contains(type.EventItem.RowId)}; current corroboration={corroborated}.");
                var retained = store.Characters.SingleOrDefault(c => c.LocalCharacterKey == characterKey)?.Bills.SingleOrDefault(b => b.BillTypeId == index);
                for (int n = 0; n < catalog.Length; n++) {
                    var t = catalog[n];
                    var previous = retained?.OrderId == (uint)obtained ? retained.Targets.SingleOrDefault(p => p.TargetIndex == t.TargetIndex)?.ObservedKills : null;
                    details.Add($"  Target index {t.TargetIndex}, ID {t.TargetId}, NPC {t.NpcNameId}: required={t.RequiredKills}; raw={counters[n].Raw}; accessor={counters[n].Accessor}; retained-before={previous?.ToString() ?? "UNKNOWN"}.");
                }
                if (counters.Where((c, n) => !c.Valid(catalog[n].RequiredKills)).Any()) { partial++; details.Add("  Counter disagreement/out-of-range: bill rejected, no clamping or source preference."); continue; }
                bool weekly = type.Type == 2;
                byte tier = weekly || index == 0 ? (byte)1 : (byte)((index <= 3 ? index - 1 : (index - 6) % 4) + 1);
                var observation = new HuntBillObservation(index, weekly ? "weekly" : "daily", tier,
                    (uint)obtained, type.EventItem.RowId, read, now, GameVersion(), collectorVersion) {
                    SourceEvidence = corroborated ? HuntObservationAdmission.KeyItemEvidence : null
                };
                var nativeObservation = observation;
                observation = session.Apply(observation, out bool messageApplied);
                if (messageApplied) details.Add("  Exact typed log4411 progress applied after fresh same-order check; not disappearance inference.");
                if (HuntBillRetentionPolicy.BillValid(observation) && (corroborated || session.CanObserveFinal(observation))) {
                    observations.Add(observation);
                    if (corroborated) session.Record(nativeObservation);
                } else partial++;
            }
            if (policy.Observe(characterKey, observations.ToArray())) { export = ""; persist(); }
            if (policy.LastSemanticChange) SemanticRevision++;
            foreach (var observation in observations) {
                var saved = store.Characters.SingleOrDefault(c => c.LocalCharacterKey == characterKey)?.Bills.SingleOrDefault(b => b.BillTypeId == observation.BillTypeId);
                if (saved is not null) details.Add($"Retained after: bill {saved.BillTypeId}, order {saved.OrderId}; "
                    + string.Join(", ", saved.Targets.Select(t => $"target index {t.TargetIndex}={t.ObservedKills}/{t.RequiredKills}")) + ".");
            }
            var fingerprint = string.Join('|', raw);
            if (fingerprint != rawFingerprint) RawRevision++;
            LastDiagnostic = $"Hunt supported raw/accessor counter/gate state {(fingerprint != rawFingerprint ? "changed" : "unchanged")}; retained semantics {(policy.LastSemanticChange ? "changed" : "unchanged")}; semantic revision={SemanticRevision}; admitted bills={observations.Count}; rejected/unavailable={partial}. Missing state is UNKNOWN.\n{lastProgressDiagnostic}\n" + string.Join('\n', details.Take(176));
            rawFingerprint = fingerprint;
            var status = observations.Count == 0 ? $"No corroborated bills observed; {partial} partial/unmatched. NOT proof of no bills. Prior state preserved."
                : $"Observed {observations.Count} positive bill cache snapshots; {partial} partial/unmatched. Admission uses current Key Item corroboration or a bounded same-session final-counter transition. Bill windows not required. Current order/cache ownership/reset unverified. Private TEST sync status is in the main window.";
            if (store.CapacityReached) status += " Retention capacity reached: new character observations paused; existing history preserved.";
            Publish(status, characterKey, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        } catch (Exception) { itemCoverage.Clear(); Publish("Hunt read/save failed; history preserved. New data may be memory-only. No live correctness claim."); }
    }
    private void Draw() {
        if (!visible || disposed) return;
        var state = view;
        ImGui.SetNextWindowSize(new Vector2(780, 500), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("My Hunt Bills local test###GillionsHuntBills", ref visible)) {
            ImGui.TextWrapped("Testing, read-only, local-first. Every three seconds, observe naturally loaded bill caches corroborated by loaded Key Items. A same-session final-counter transition can be retained within six seconds of corroboration; disappearance alone is UNKNOWN. No bill window, radar, mob scans, UI opening or game requests. Missing data never clears retained bills. Private TEST uploads require separate permission; see the main window.");
            bool enabled = state.Enabled;
            if (ImGui.Checkbox("Retain naturally loaded Hunt Bill observations locally", ref enabled)) {
                export = "";
                _ = framework.RunOnFrameworkThread(() => { if (disposed) return; store.LocalRetentionEnabled = enabled; schedule.Reset(); session.Reset(); persist(); Publish(policy.Status); });
            }
            ImGui.TextWrapped(state.Status);
            bool coverageFresh = state.CoverageObservedAtUtc is DateTime observed && DateTime.UtcNow >= observed
                && DateTime.UtcNow - observed <= TimeSpan.FromSeconds(HuntBillItemCoverage.MaximumAgeSeconds);
            ImGui.TextWrapped(coverageFresh ? state.CoverageStatus + $" Observed {state.CoverageObservedAtUtc:u}; current character/session context."
                : "Hunt item coverage UNAVAILABLE/expired; retained progress unchanged.");
            if (coverageFresh && state.CoverageRows is not null) foreach (var row in state.CoverageRows) ImGui.TextUnformatted(row);
            ImGui.TextWrapped($"Last cache read/save: {state.Milliseconds:F2} ms. Up to 16 characters / 22 bill categories each / 256 KiB. Latest positive observation per bill; no reset, absence or cache-owner proof.");
            ImGui.TextWrapped("Private export contains your targets, counters and activity times. No credential, account/character ID or name. Copy only to a trusted diagnostic recipient; this is not community contribution or a server upload.");
            if (enabled && ImGui.Button("Prepare PRIVATE Hunt JSON (no upload)")) {
                _ = framework.RunOnFrameworkThread(() => {
                    if (disposed) return;
                    try { export = CoveragePayload(CurrentCharacterKey(), DateTime.UtcNow) ?? policy.PreparePrivateExport(CurrentCharacterKey()); }
                    catch (Exception) { export = ""; Publish("Private export unavailable: no retained observation for active character, retention off or unsupported format."); }
                });
            }
            if (enabled && export.Length > 0 && ImGui.Button("Copy PRIVATE Hunt JSON")) ImGui.SetClipboardText(export);
            if (ImGui.Button("Copy aggregate Hunt diagnostics")) ImGui.SetClipboardText($"Gillions Game Sync Testing {collectorVersion}\nGame: {GameVersion()}; SDK: {typeof(MobHunt).Assembly.GetName().Version}\nHunts local: {state.Enabled}\n{state.Status}\n{LastDiagnostic}\nAttempts: {state.Attempts}; last attempt UTC: {state.LastAttemptUtc:u}; cadence: 3 seconds\nRead/save: {state.Milliseconds:F2} ms\nLocal collection diagnostics only; private TEST sync controls/status are in the main window. No live correctness claim.");
            ImGui.PushTextWrapPos(0);
            foreach (var row in state.Rows) ImGui.TextUnformatted(row);
            ImGui.PopTextWrapPos();
        }
        ImGui.End();
    }
    public void Dispose() {
        if (disposed) return; disposed = true; export = "";
        client.Logout -= OnLogout; ui.UiBuilder.Draw -= Draw; commands.RemoveHandler("/gillionshunts");
        client.TerritoryChanged -= OnTerritoryChanged; conditions.ConditionChange -= OnConditionChange;
        chat.LogMessage -= OnProgressMessage;
    }
}
#endif
