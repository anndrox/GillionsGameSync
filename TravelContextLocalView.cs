#if GILLIONS_TEST_BUILD
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

namespace GillionsGameSync;

// Read only LocalPlayer (no enumeration/radar), public map/state getters and a
// corroborated naturally visible native Teleport cache. Never refresh that cache.
internal sealed class TravelContextLocalView : IDisposable {
    private readonly IDalamudPluginInterface ui;
    private readonly IClientState client;
    private readonly IObjectTable objects;
    private readonly IPlayerState player;
    private readonly IDataManager data;
    private readonly IGameGui gameGui;
    private readonly ICondition conditions;
    private readonly TravelContextState state = new();
    internal event System.Action? Invalidated;
    // Cheap lifecycle admission; no position/cache reads or JSON per frame.
    internal bool Available(ulong character) => !disposed && state.Enabled && client.IsLoggedIn && player.IsLoaded
        && character != 0 && player.ContentId == character && !client.IsPvP && !client.IsGPosing
        && !conditions[ConditionFlag.BetweenAreas] && !conditions[ConditionFlag.BetweenAreas51]
        && PersonalObservationCompatibility.Supports(GameVersion(), nativeVersion);
    internal TravelObservation? Current(ulong character, DateTime now) => Available(character) ? state.Current(character,now) : null;
    internal void ClearSession() { state.Clear(); Reset(); }
    private bool visible, disposed;
    private sealed record View(string Status, string[] Rows, double Milliseconds, long Bytes, DateTime? ObservedAtUtc = null);
    private volatile View view = new("Location consent OFF; no travel reads or uploads.", [], 0, 0);
    private readonly string version = typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "unavailable";
    private readonly string nativeVersion = typeof(Telepo).Assembly.GetName().Version?.ToString() ?? "unavailable";
    internal TravelContextLocalView(IDalamudPluginInterface ui, IClientState client, IObjectTable objects,
        IPlayerState player, IDataManager data, IGameGui gameGui, ICondition conditions) {
        this.ui=ui; this.client=client; this.objects=objects; this.player=player;
        this.data=data; this.gameGui=gameGui; this.conditions=conditions;
        ui.UiBuilder.Draw += Draw; client.Login += Reset; client.Logout += Logout;
        client.TerritoryChanged += Changed; client.MapIdChanged += Changed;
    }
    internal void Show() => visible = true;
    internal void SetEnabled(bool enabled) {
        if (state.Enabled == enabled) return;
        state.SetEnabled(enabled);
        Invalidated?.Invoke();
        view = new(enabled ? "Awaiting current session; transport requires the independent shared TEST grant."
            : "Location consent OFF; volatile location cleared. No uploads.", [], 0, 0);
    }
    private void Reset() { state.Invalidate(); Invalidated?.Invoke(); view = new(state.Enabled
        ? "Session/map changed: no current travel observation. No upload."
        : "Location consent OFF; no travel reads or uploads.", [], 0, 0); }
    private void Logout(int _, int __) { state.Clear(); Reset(); }
    private void Changed(uint _) => Reset();
    private string GameVersion() => data.GameData.Repositories.TryGetValue("ffxiv", out var repository) ? repository.Version : "unavailable";
    private unsafe TravelDestination[] ReadDestinations() {
        // Public IAetheryteList refreshes native state on access; do NOT use it.
        var addon = (AtkUnitBase*)gameGui.GetAddonByName("Teleport", 1).Address;
        var agent = AgentTeleport.Instance(); var telepo = Telepo.Instance();
        if (addon == null || !addon->IsVisible || agent == null || telepo == null
            || !agent->IsAgentActive() || agent->AddonId != addon->Id
            || agent->AetheryteList != &telepo->TeleportList) return [];
        var count = telepo->TeleportList.Count;
        if (count is < 1 or > TravelPolicy.MaximumDestinations || agent->AetheryteCount != count) return [];
        var catalog = data.GetExcelSheet<Aetheryte>(); // cached sheet, bounded ID lookup only
        var unlocks = UIState.Instance();
        if (unlocks == null) return [];
        var home = player.HomeAetheryte.RowId; var free = player.FreeAetheryte.RowId;
        var favorites = player.FavoriteAetherytes;
        if (favorites.Count > 4) return [];
        var result = new List<TravelDestination>((int)count);
        var ids = new HashSet<uint>();
        for (int i=0; i<count; i++) {
            var value = telepo->TeleportList[i];
            // Do not export private housing destinations, estate IDs, wards,
            // plots, shared-house relationships or apartment details.
            if (value.IsSharedHouse || value.IsApartment || value.SubIndex != 0 || value.Ward != 0 || value.Plot != 0) continue;
            var row = catalog.GetRowOrDefault(value.AetheryteId);
            if (row is null || !row.Value.IsAetheryte || row.Value.Territory.RowId != value.TerritoryId
                || value.AetheryteId >= unlocks->UnlockedAetherytes.Length * 8
                || !unlocks->IsAetheryteUnlocked(value.AetheryteId)
                || value.GilCost > 99999 || !ids.Add(value.AetheryteId)) return [];
            bool favored = false;
            for (int j=0;j<favorites.Count;j++) if (favorites[j].RowId == value.AetheryteId) favored = true;
            if (value.IsFavourite != favored) return []; // disagreeing caches fail closed
            result.Add(new(value.AetheryteId,value.TerritoryId,"OBSERVED_IN_PERSONAL_LIST","UNKNOWN",
                value.GilCost,null,home == 0 ? null : home == value.AetheryteId,
                value.IsFreeAetheryte || free == value.AetheryteId, favored));
        }
        return result.ToArray();
    }
    internal void Tick(DateTime now) {
        if (view.ObservedAtUtc is { } observed && now >= observed.AddSeconds(TravelPolicy.TtlSeconds)) Unavailable("Current travel observation expired; volatile facts cleared.");
        if (view.ObservedAtUtc is not null && !Available(player.ContentId)) Unavailable("Current player/build unavailable; volatile facts cleared.");
        if (disposed || !state.Begin(now)) return; // no position/native reads each frame
        var start = Stopwatch.GetTimestamp(); var allocated = GC.GetAllocatedBytesForCurrentThread();
        try {
            var native = typeof(Telepo).Assembly.GetName().Version?.ToString() ?? "unavailable";
            if (!PersonalObservationCompatibility.Supports(GameVersion(),native)) { Unavailable("Unsupported game/SDK: no travel reads."); return; }
            if (!client.IsLoggedIn || !player.IsLoaded || player.ContentId == 0 || client.IsPvP || client.IsGPosing
                || conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51]) { Unavailable("Player/location unavailable; no current context."); return; }
            var character = player.ContentId; var territory = client.TerritoryType; var mapId = client.MapId;
            var local = objects.LocalPlayer;
            var map = data.GetExcelSheet<Lumina.Excel.Sheets.Map>().GetRowOrDefault(mapId);
            if (local == null || map is null || territory == 0 || map.Value.TerritoryType.RowId != territory
                || map.Value.SizeFactor == 0) { Unavailable("Map/player correspondence unavailable; no coordinates."); return; }
            var position = local.Position;
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Z)) { Unavailable("Invalid position; no coordinates."); return; }
            var coordinates = MapUtil.WorldToMap(new Vector2(position.X,position.Z),map.Value);
            var destinations = ReadDestinations();
            var observation = new TravelObservation(territory,mapId,TravelPolicy.Round(coordinates.X),TravelPolicy.Round(coordinates.Y),
                now,GameVersion(),native,version,destinations.Length == 0 ? "UNAVAILABLE" : "OBSERVED_PARTIAL",destinations);
            if (player.ContentId != character || client.MapId != mapId || client.TerritoryType != territory
                || !state.Observe(character,observation,now)) { Unavailable("Invalid/changing context; fail closed."); return; }
            var rows = new List<string> { $"Territory {territory}; map {mapId}; position {observation.MapX:F1}, {observation.MapY:F1}",
                $"Observed UTC {now:yyyy-MM-dd HH:mm:ss}Z; expires after {TravelPolicy.TtlSeconds}s; {destinations.Length} public destinations observed (not a complete unlock list).",
                "Actual final charged cost UNSUPPORTED; cached list Gil is diagnostic only. Current action usability UNKNOWN." };
            for (int i=0;i<Math.Min(5,destinations.Length);i++) {
                var d=destinations[i];
                var label=data.GetExcelSheet<Aetheryte>().GetRowOrDefault(d.AetheryteId)?.PlaceName.Value.Name.ExtractText() ?? "Unknown destination";
                // Public catalog text is diagnostic only: bounded, no extra
                // payload field, sheet scan, private house label or UI scraping.
                label=label.Replace('\r',' ').Replace('\n',' ');
                if (label.Length>80) label=label[..80];
                rows.Add($"{label} (Aetheryte {d.AetheryteId}): list quote {d.ObservedListGil} Gil; Home {d.Home}; Free {d.Free}; Favored {d.Favored}");
            }
            view = new("Private current context observed locally. HTTPS TEST transport is separately gated by the paired travel grant.",rows.ToArray(),
                Stopwatch.GetElapsedTime(start).TotalMilliseconds,GC.GetAllocatedBytesForCurrentThread()-allocated,now);
        } catch (Exception) { Unavailable("Travel source unavailable; no current context. Unrelated sync preserved."); }
    }
    private void Unavailable(string message) { state.Invalidate(); Invalidated?.Invoke(); view = new(message,[],0,0); }
    private void Draw() {
        if (!visible || disposed) return;
        if (ImGui.Begin("Hunt routing context — private local test",ref visible)) {
            var current=view;
            ImGui.TextWrapped("Separate OFF-by-default location permission. Current map-space position rounded to 0.1; RAM only, no movement history. Read at most once/15 seconds. Upload only to HTTPS shared TEST with its independent travel grant. Naturally open Teleport to inspect cached quotes; never opened by Gillions.");
            ImGui.TextWrapped(current.Status);
            var age = current.ObservedAtUtc is { } observed ? (DateTime.UtcNow-observed).TotalSeconds : double.PositiveInfinity;
            if (age is >= 0 and < TravelPolicy.TtlSeconds) {
                foreach (var row in current.Rows) ImGui.TextWrapped(row);
                ImGui.TextWrapped($"Observation age: {age:F0}s.");
            } else ImGui.TextWrapped("Current context UNAVAILABLE/EXPIRED. No coordinates or prices implied.");
            ImGui.TextWrapped($"Last read/view: {current.Milliseconds:F2} ms; {current.Bytes} allocated bytes (includes bounded validation/view). Local observation only; live correctness not yet established.");
        }
        ImGui.End();
    }
    public void Dispose() {
        disposed=true; state.Clear(); Invalidated?.Invoke(); view=new("Disposed",[],0,0);
        ui.UiBuilder.Draw -= Draw; client.Login -= Reset; client.Logout -= Logout;
        client.TerritoryChanged -= Changed; client.MapIdChanged -= Changed;
    }
}
#endif
