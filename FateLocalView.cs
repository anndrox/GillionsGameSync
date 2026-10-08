#if GILLIONS_TEST_BUILD || GILLIONS_PUBLIC_BUILD
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using TerritoryUse = FFXIVClientStructs.FFXIV.Client.Enums.TerritoryIntendedUse;
using Lumina.Excel.Sheets;

namespace GillionsGameSync;

// Maintained IFateTable/IFate only. No unsafe code, actor enumeration, native
// requests, game writes, UI scraping or raw addresses. Diagnostics are RAM-only
// collection is controlled solely by fresh authenticated Site policy discovery.
internal sealed class FateLocalView : IDisposable {
    private readonly IDalamudPluginInterface ui;
    private readonly ICommandManager commands;
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly IDataManager data;
    private readonly ICondition conditions;
    private readonly IFateTable table;
    private readonly IFramework framework;
    private readonly FateEpochState state=new();
    private FateMeasurements measurements=new();
    private readonly FateSource? source;
    private bool measuring, disposed;
#if GILLIONS_TEST_BUILD
    private bool visible;
#endif
    private long nextRead;
    private FateContext? settled;
    private long settleEpoch;
    private sealed record View(string Status, FateContext? Context, FateObservation[] Rows, FateCost[] Costs, int TableRows);
    private volatile View view=new("Local FATE diagnostic session stopped; Site admission unavailable. No sends.",null,[],[],0);
    private string summary="No measured samples.";
    internal FateSource? ObservedSource => source;
    internal FatePrepared? Prepared => state.Prepared;
    internal long Epoch => state.Epoch;
    internal bool Measuring => measuring && !disposed;
    internal void SetAuthorized(bool authorized) {
        if (measuring == authorized || disposed) return;
        measuring = authorized;
        if (!authorized) { state.ForgetObservations(); view=new("Waiting for supported context or Gillions authorization.",null,[],measurements.Snapshot(),0); }
    }
    // Managed settlement state only: sender maintenance never invokes Context()
    // or adds player/location/game-memory reads between five-second passes.
    internal bool ContextReady => Measuring && settled is not null && settleEpoch==state.Epoch;
    internal event System.Action? Invalidated;
    internal string RemoteStatus { get; set; }="Site policy unavailable; no contributions.";
    internal void CancelUnsent()=>state.CancelUnsent();
    internal void Acknowledge(Guid batch) { if(state.Prepared?.BatchId==batch) state.CancelUnsent(); }

    internal FateLocalView(IDalamudPluginInterface ui, ICommandManager commands, IClientState client,
        IPlayerState player, IDataManager data, ICondition conditions, IFateTable table, IFramework framework) {
        this.ui=ui; this.commands=commands; this.client=client; this.player=player;
        this.data=data; this.conditions=conditions; this.table=table; this.framework=framework;
        try { source=Source(); } catch (Exception) { source=null; }
        client.Login+=Invalidate; client.Logout+=Logout; client.ZoneInit+=Zone;
        client.TerritoryChanged+=Changed; client.InstanceChanged+=Changed; client.MapIdChanged+=Changed;
#if GILLIONS_TEST_BUILD
        commands.AddHandler("/gillionsfates",new CommandInfo((_,_)=>Show()) { HelpMessage="Testing FATE diagnostics only; collection starts automatically when Gillions permits it." });
        ui.UiBuilder.Draw+=Draw;
#endif
    }
    private string GameVersion() => data.GameData.Repositories.TryGetValue("ffxiv",out var repo) ? repo.Version : "unavailable";
    private static string FileVersion(Assembly a) => FileVersionInfo.GetVersionInfo(a.Location).FileVersion ?? "unavailable";
    private static string Revision(Assembly a) {
        var v=FileVersionInfo.GetVersionInfo(a.Location).ProductVersion;
        return v is not null && v.Contains('+') ? v[(v.LastIndexOf('+')+1)..] : "unavailable";
    }
    private FateSource Source() => new("dalamud-fate-table",typeof(Plugin).Assembly.GetName().Version?.ToString(4) ?? "unavailable",
        GameVersion(),ui.Manifest.DalamudApiLevel,FileVersion(typeof(IFateTable).Assembly),Revision(typeof(IFateTable).Assembly),
        FileVersion(typeof(FFXIVClientStructs.FFXIV.Client.Game.Fate.FateManager).Assembly),
        Revision(typeof(FFXIVClientStructs.FFXIV.Client.Game.Fate.FateManager).Assembly),
        FileVersion(typeof(Lumina.GameData).Assembly).TrimEndVersionZero(),
        FileVersion(typeof(Fate).Assembly).TrimEndVersionZero(),GameVersion());
#if GILLIONS_TEST_BUILD
    internal void Show() => visible=true;
#endif
    private void Invalidate() {
        state.Invalidate(); settled=null; settleEpoch=state.Epoch;
        Invalidated?.Invoke();
        // Lifecycle cannot bypass five-second read admission. ZoneInit happens
        // before ClientState assigns the new territory/instance.
        view=new("Context invalidated; unsent epoch cleared. Missing FATEs are UNKNOWN. No sends.",null,[],measurements.Snapshot(),0);
    }
    internal void ClearAuthorization() => Invalidate();
    private void Logout(int _,int __)=>Invalidate();
    private void Changed(uint _)=>Invalidate();
    private void Zone(ZoneInitEventArgs _)=>Invalidate();
    private FateContext? Context() {
        if (!client.IsLoggedIn || !player.IsLoaded || client.IsPvP || client.IsGPosing
            || conditions[ConditionFlag.BetweenAreas] || conditions[ConditionFlag.BetweenAreas51]
            || conditions[ConditionFlag.LoggingOut] || conditions[ConditionFlag.ReadyingVisitOtherWorld]
            || conditions[ConditionFlag.WaitingToVisitOtherWorld] || conditions[ConditionFlag.BoundByDuty]
            || conditions[ConditionFlag.BoundByDuty56] || conditions[ConditionFlag.BoundByDuty95]) return null;
        var character=player.ContentId; var world=player.CurrentWorld; var territory=client.TerritoryType;
        var row=data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territory);
        if (character==0 || !world.IsValid || !world.Value.IsPublic || territory==0 || row is null
            || row.Value.TerritoryIntendedUse.RowId!=(uint)TerritoryUse.Overworld
            || row.Value.ContentFinderCondition.RowId!=0) return null;
        return new(character,world.RowId,territory,client.Instance);
    }
    internal void Tick(DateTime now) {
        if (disposed || !measuring) return;
        long monotonic=Stopwatch.GetTimestamp();
        if (monotonic<nextRead) return; // no FATE/position/player reads per frame
        nextRead=monotonic+(long)(Stopwatch.Frequency*FatePolicy.ReadSeconds);
        double readMs=0, prepareMs=0; long readBytes=0, prepareBytes=0;
        int count=0; string outcome="unavailable"; bool preparing=false;
        var start=Stopwatch.GetTimestamp(); var allocated=GC.GetAllocatedBytesForCurrentThread();
        FateContext? before=null; FateObservation[] rows=[];
        try {
            if (!FatePolicy.Compatible(source) || GameVersion()!=source!.GameVersion) {
                Invalidate(); outcome="Unsupported source/game matrix; no FATE reads."; return;
            }
            before=Context();
            if (before is null) { Invalidate(); outcome="Unavailable/unsupported public context. No negative inference."; return; }
            // Two independently scheduled stable context checks, rather than a
            // guessed settle timeout or reading inside a zoning event.
            if (settled!=before || settleEpoch!=state.Epoch) {
                state.Invalidate(); settled=before; settleEpoch=state.Epoch;
                outcome="Awaiting a second settled context check; no rows admitted."; return;
            }
            long epoch=state.Epoch;
            count=table.Length;
            if (count is <0 or >FatePolicy.MaximumRows) {
                Invalidate(); outcome="Table count outside bounded support; no negative inference."; return;
            }
            var copied=new List<FateObservation>(count);
            for (int i=0;i<count;i++) {
                var f=table[i];
                if (f is null || !table.IsValid(f)) continue;
                // Do not store IFate or an address. Each value is copied during
                // this bounded framework pass and never dereferenced afterward.
                ushort id=f.FateId; var territory=f.TerritoryType; var definition=f.GameData;
                var value=FatePolicy.State((byte)f.State);
                if (id==0 || value is null || !territory.IsValid || territory.RowId!=before.Territory
                    || !definition.IsValid || definition.RowId!=id || definition.Value.EurekaFate!=0) continue;
                byte progress=f.Progress, level=f.Level, max=f.MaxLevel;
                var position=f.Position; float radius=f.Radius;
                var timer=FatePolicy.Timing(f.StartTimeEpoch,f.Duration,now);
                bool levels=level>0 && max>=level && !(level==1 && max==255);
                copied.Add(new(id,before.World,before.Territory,
                    new(before.Instance==0 ? "noninstanced" : "public_instance",before.Instance),value,now,
                    progress<=100 ? progress : null,f.HasBonus,levels ? level : null,levels ? max : null,
                    float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z)
                        ? new(position.X,position.Y,position.Z) : null,
                    float.IsFinite(radius) && radius>0 ? radius : null,timer));
            }
            var after=Context();
            if (after!=before || after is null || epoch!=state.Epoch || count!=table.Length) {
                Invalidate(); outcome="Context/table changed during read; epoch discarded."; return;
            }
            rows=copied.ToArray();
            readMs=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            readBytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
            start=Stopwatch.GetTimestamp(); allocated=GC.GetAllocatedBytesForCurrentThread();
            preparing=true;
            if (!state.Observe(before,after,source!,rows,now)) {
                outcome="Row/source/context validation failed closed."; return;
            }
            state.Expire(now);
            prepareMs=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            prepareBytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
            outcome=count==0 ? "Empty/unavailable table; no negative observation or replacement batch."
                : $"Copied {rows.Length}; admitted {state.Current.Length} positive observations; no global coverage claim.";
        } catch (Exception e) {
            Invalidate(); outcome=$"Read failed closed ({e.GetType().Name}); no negative inference.";
        } finally {
            if (preparing) { prepareMs=Stopwatch.GetElapsedTime(start).TotalMilliseconds; prepareBytes=GC.GetAllocatedBytesForCurrentThread()-allocated; }
            else { readMs=Stopwatch.GetElapsedTime(start).TotalMilliseconds; readBytes=GC.GetAllocatedBytesForCurrentThread()-allocated; }
            var cost=new FateCost(readMs,readBytes,prepareMs,prepareBytes,count,state.Current.Length,state.LastChanged,outcome);
            measurements.Add(cost);
            view=new(outcome,before,state.Current.ToArray(),measurements.Snapshot(),count);
            summary=FateMeasurements.Summary(view.Costs); // once per bounded read, never every UI frame
        }
    }
#if GILLIONS_TEST_BUILD
    internal string Diagnostic() => "PRIVATE FATE Testing diagnostics (manual copy, no diagnostic upload)\nWorld, territory and observation times reveal your presence. Share only with a trusted diagnostic recipient; never share configuration or credentials.\n"+view.Status+"\n"
        +$"Cadence {FatePolicy.ReadSeconds}s; RAM-only, maximum 240 samples. Source: {JsonSerializer.Serialize(source,FatePolicy.Json)}\n"
        +(view.Context is { } c ? $"World {c.World}; territory {c.Territory}; public ordinal {c.Instance}.\n" : "Context unavailable.\n")
        +summary+"\n"
        +$"Remote: {RemoteStatus}\n"
        +"Rows: "+JsonSerializer.Serialize(view.Rows,FatePolicy.Json)+"\n"
        +"Samples: "+JsonSerializer.Serialize(view.Costs,FatePolicy.Json);
    private void ResetMeasurements() { measurements=new(); summary="No measured samples."; }
    private void Draw() {
        if (!visible || disposed) return;
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(760,480),ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Advanced Testing FATE diagnostics",ref visible)) { ImGui.End(); return; }
        ImGui.TextWrapped("Read-only public overworld observations start automatically with a fresh Gillions policy grant and supported context. No player coordinates, persistent history or gameplay writes. Missing state is UNKNOWN. Diagnostics cannot start or authorize collection.");
        ImGui.TextWrapped("PRIVATE diagnostics: world, territory and observation times reveal your presence. Copy only for a trusted diagnostic recipient. Never share configuration or credentials.");
        if (ImGui.Button("Reset measurement counters")) _=framework.RunOnFrameworkThread(()=> { if (!disposed) ResetMeasurements(); });
        ImGui.TextWrapped(view.Status);
        ImGui.TextWrapped("Remote contribution: "+RemoteStatus);
        ImGui.TextWrapped(summary);
        if (ImGui.Button("Copy PRIVATE FATE diagnostics (local, no diagnostic upload)")) ImGui.SetClipboardText(Diagnostic());
        foreach(var row in view.Rows) ImGui.TextWrapped($"FATE {row.FateId}: {row.State.Kind}, progress {row.ProgressPercent?.ToString() ?? "UNKNOWN"}%, bonus {row.Bonus?.ToString() ?? "UNKNOWN"}; world {row.WorldId}, territory {row.TerritoryId}, {row.Instance.Kind} {row.Instance.Number}; observed {row.ObservedAt:O}; start {row.Timing?.StartTimeEpoch.ToString() ?? "UNKNOWN"}.");
        ImGui.End();
    }
#endif
    public void Dispose() {
        disposed=true; measuring=false; state.Invalidate();
        client.Login-=Invalidate; client.Logout-=Logout; client.ZoneInit-=Zone;
        client.TerritoryChanged-=Changed; client.InstanceChanged-=Changed; client.MapIdChanged-=Changed;
#if GILLIONS_TEST_BUILD
        ui.UiBuilder.Draw-=Draw; commands.RemoveHandler("/gillionsfates");
#endif
    }
}
internal static class FateVersionFormat {
    internal static string TrimEndVersionZero(this string s) => s.EndsWith(".0",StringComparison.Ordinal) ? s[..^2] : s;
}
#endif
