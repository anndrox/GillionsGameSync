#if GILLIONS_TEST_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;

namespace GillionsGameSync;

public sealed partial class Plugin {
    private const string HuntDeferHeader = "x-gillions-hunt-defer-capability";
    private const string TrackedFatesHeader = "x-gillions-tracked-fates-capability";
    private readonly HuntDeferState huntDefer = new();
    private readonly FateAlertOccurrences fateAlerts = new();
    private readonly IFateTable huntFateTable;
    private readonly IPlayerState huntPlayerState;
    private bool huntDeferSupported;
    private TrackedFates? trackedFates;
    private uint trackedFateWorld, awarenessWorld;
    private int ignoredSonarLines;
    private long nextAwarenessRead;
    private WebsiteResponseClock? huntAwarenessClock;
    private uint currentHuntWorld;
    private bool exactHuntFlag;
    private (DateTime Until, int Instance, MapLinkPayload Link)? huntSonar;
    private readonly Dictionary<uint, DateTime> sonarFates = new();
    private readonly Dictionary<uint, LocalFate> localFates = new();
    private (uint World, uint Territory, uint Map, uint Instance)? sampledFateZone;
    private (uint World, uint Territory, uint Map, uint Instance)? settledAwarenessZone;
    private HuntAvailability currentHuntAvailability;
    private DateTime? currentBRankSeenAt;

    private sealed record LocalFate(uint Territory, uint Map, uint Instance, int Start, DateTime Until, bool Running, MapLinkPayload? Link);

    private void ApplyHuntAwareness(string json, uint world) {
        using var response = JsonDocument.Parse(json);
        var root = response.RootElement;
        huntDeferSupported = root.TryGetProperty("huntDefer", out var defer) && HuntMapPolicy.Exact(defer, "schemaVersion")
            && defer.GetProperty("schemaVersion").ValueKind == JsonValueKind.Number
            && defer.GetProperty("schemaVersion").TryGetInt32(out var schema) && schema == 1;
        if (!huntDeferSupported) ForgetAwareHunt();

        TrackedFates? next = null;
        try {
            if (root.TryGetProperty("trackedFates", out var field)) next = TrackedFates.Parse(field, trackedFates);
        } catch (Exception e) when (e is InvalidOperationException or FormatException or OverflowException) {
            // An invalid optional field cannot interfere with ordinary presence.
        }
        trackedFates = next;
        trackedFateWorld = world;
        if (next is not null) fateAlerts.Retain(next);
        foreach (var id in sonarFates.Keys.Where(id => next?.Items.ContainsKey(id) != true).ToArray()) sonarFates.Remove(id);
        foreach (var id in localFates.Keys.Where(id => next?.Items.ContainsKey(id) != true).ToArray()) localFates.Remove(id);
    }

    private void ForgetAwareHunt() {
        huntDefer.ForgetCurrent();
        RetireAwareHuntFlag();
    }

    private void RetireAwareHuntFlag() {
        huntDefer.RetirePresentation();
        huntAwarenessClock = null;
        currentBRankSeenAt = null;
        huntSonar = null;
        currentHuntAvailability = HuntAvailability.Unknown;
    }

    private void ResetHuntAwareness() {
        SuspendHuntAwareness();
        huntDefer.Reset();
        awarenessWorld = 0;
        sonarFates.Clear(); localFates.Clear(); fateAlerts.Clear();
        sampledFateZone = settledAwarenessZone = null;
    }

    private void SuspendHuntAwareness() {
        huntDeferSupported = false;
        ForgetAwareHunt();
        trackedFates = null;
        trackedFateWorld = 0;
    }

    private uint AwarenessWorld() => huntPlayerState.IsLoaded ? huntPlayerState.CurrentWorld.RowId : 0;

    private bool HuntAwarenessReady() => !disposed && HasPairedSession && activeOwnedState is not null
        && clientState.IsLoggedIn && objects.LocalPlayer is not null
        && !marketConditions[ConditionFlag.BetweenAreas] && !marketConditions[ConditionFlag.BetweenAreas51]
        && !marketConditions[ConditionFlag.LoggingOut] && !marketConditions[ConditionFlag.ReadyingVisitOtherWorld]
        && !marketConditions[ConditionFlag.WaitingToVisitOtherWorld];

    private HuntMapRequest? LatestAwareHunt(uint world) => huntDeferSupported && HuntReceivingEnabled && world != 0
        && currentHuntWorld == world ? huntDefer.Current : null;

    private HuntMapRequest? CurrentAwareHunt(uint world) => LatestAwareHunt(world) is { } command && huntDefer.CanDefer
        && HuntDeferState.Reason(command) is not null && huntAwarenessClock?.Unexpired(command.ExpiresAtUtc) == true ? command : null;

    private HuntDeferAction? PlanHuntDefer(bool recheckObjectTable = false) {
        if (!HuntAwarenessReady()) return null;
        var world = AwarenessWorld();
        if (LatestAwareHunt(world) is not { } command) return null;
        var availability = CurrentAwareHunt(world) is null ? HuntAvailability.Unknown : currentHuntAvailability;
        var now = DateTime.UtcNow;
        if (HasCurrentHuntRelay(command, now)) availability = HuntAvailability.Up;
        else if (sampledFateZone != (currentHuntWorld, (uint)clientState.TerritoryType, clientState.MapId, clientState.Instance)
            && HuntDeferState.Reason(command) == HuntDeferState.FateNotUp) availability = HuntAvailability.Unknown;
        var action = huntDefer.Plan(command, availability, world, now);
        if (action?.Reason == HuntDeferState.BRankNotSeen && recheckObjectTable)
            return huntDefer.Plan(command, ObserveBRank(command).Availability, world, now);
        return action;
    }

    private bool OpenExactHuntFlag(HuntMapRequest command, MapLinkPayload link) {
        var opened = huntMapGui.OpenMapWithMapLink(link);
        if (opened) chatGui.Print($"{HuntMonsterName.Display(command.TargetName)}: sighting marked on your map.", "Gillions");
        return opened;
    }

    private MapLinkPayload? BeginAwareHunt(HuntMapRequest command, WebsiteResponseClock clock) {
        ForgetAwareHunt();
        if (!huntDeferSupported || command.ContractVersion != 2 || !HuntAwarenessReady()) return null;
        huntDefer.Consumed(command);
        currentHuntWorld = AwarenessWorld();
        huntAwarenessClock = clock;
        exactHuntFlag = false;
        if (HuntDeferState.Reason(command) != HuntDeferState.BRankNotSeen) return null;
        var mark = ObserveBRank(command);
        if (mark.Availability == HuntAvailability.Up) {
            currentHuntAvailability = mark.Availability;
            currentBRankSeenAt = BRankSightingTime(mark.Link, DateTime.UtcNow);
        }
        exactHuntFlag = mark.Link is not null;
        return mark.Link;
    }

    private (HuntAvailability Availability, MapLinkPayload? Link) ObserveBRank(HuntMapRequest command) {
        var relay = HasCurrentHuntRelay(command, DateTime.UtcNow) ? huntSonar!.Value.Link : null;
        if (clientState.TerritoryType != command.TerritoryId || clientState.MapId != command.MapId)
            return BRankSighting.Resolve(false, null, relay, false);
        if (ValidatedAwarenessMap(command.TerritoryId, command.MapId) is not { } map || objects.LocalPlayer is not { } player)
            return (HuntAvailability.Unknown, null);
        var nameId = HuntNpcNameId(command.TargetId);
        if (nameId == 0) return (HuntAvailability.Unknown, null);
        foreach (var actor in objects) {
            if (actor is not IBattleNpc npc || npc.NameId != nameId || npc.CurrentHp == 0) continue;
            return BRankSighting.Resolve(true, PositionLink(command.TerritoryId, map, npc.Position), relay, false);
        }
        if (relay is not null) return (HuntAvailability.Up, relay);
        // The spawn carries map XY only. Flight cannot establish that the spawn is within load range.
        var playerMap = MapUtil.WorldToMap(new Vector2(player.Position.X, player.Position.Z), map);
        var radiusOnMap = MapUtil.ConvertWorldCoordXZToMapCoord(40, map.SizeFactor, 0)
            - MapUtil.ConvertWorldCoordXZToMapCoord(0, map.SizeFactor, 0);
        var nearby = Vector2.DistanceSquared(playerMap, new(command.MapX, command.MapY)) <= radiusOnMap * radiusOnMap;
        return BRankSighting.Resolve(false, null, relay, nearby, inFlight: marketConditions[ConditionFlag.InFlight]);
    }

    private DateTime BRankSightingTime(MapLinkPayload? link, DateTime now) =>
        huntSonar is { } sighting && ReferenceEquals(link, sighting.Link) ? sighting.Until.AddSeconds(-60) : now;

    private bool HasCurrentHuntRelay(HuntMapRequest command, DateTime now) => configuration.UseSonarChatMessages
        && huntSonar is { } relay && relay.Until > now
        && (clientState.TerritoryType != command.TerritoryId || relay.Instance == clientState.Instance);

    private uint HuntNpcNameId(uint targetId) => dataManager.GetExcelSheet<MobHuntTarget>().GetRowOrDefault(targetId)?.Name.RowId ?? 0;

    private void ObserveWaitingBRanks(IEnumerable<HuntDeferState.WaitingTarget> waiting) {
        var ranks = waiting.Where(target => target.FateId is null)
            .Select(target => (Target: target, NameId: HuntNpcNameId(target.TargetId))).Where(rank => rank.NameId != 0)
            .ToLookup(rank => rank.NameId, rank => rank.Target);
        if (ranks.Count == 0) return;
        foreach (var actor in objects) {
            if (actor is not IBattleNpc npc || npc.CurrentHp == 0) continue;
            foreach (var target in ranks[npc.NameId]) huntDefer.SawUp(target);
        }
    }

    private void TickHuntAwareness() {
        if (!huntDeferSupported && trackedFates is null) return;
        if (!permissionAuthority.Fresh(Environment.TickCount64)) { SuspendHuntAwareness(); return; }
        if (!HuntAwarenessReady()) { sampledFateZone = settledAwarenessZone = null; return; }
        var stamp = Environment.TickCount64;
        if (stamp < nextAwarenessRead) return;
        nextAwarenessRead = stamp + 3000;
        var world = AwarenessWorld();
        if (world == 0) return;
        if (awarenessWorld != world) {
            awarenessWorld = world;
            localFates.Clear(); sonarFates.Clear(); fateAlerts.Clear();
        }
        var now = DateTime.UtcNow;
        var command = CurrentAwareHunt(world);
        var zone = (World: world, Territory: (uint)clientState.TerritoryType, Map: clientState.MapId, Instance: clientState.Instance);
        var settled = settledAwarenessZone == zone;
        settledAwarenessZone = zone;
        var huntFateId = command?.FateId;
        var huntFate = huntFateId is not null && command!.TerritoryId == zone.Territory && command.MapId == zone.Map;
        var waiting = huntDeferSupported && HuntReceivingEnabled
            ? huntDefer.Waiting(world, now).Where(target => target.TerritoryId == zone.Territory && target.MapId == zone.Map).ToArray() : [];
        Dictionary<uint, LocalFate>? local = null;
        if (settled && !inDuty && !clientState.IsPvP && !clientState.IsGPosing) {
            var watchedFates = waiting.Where(target => target.FateId.HasValue).Select(target => target.FateId!.Value).ToHashSet();
            if (huntFate) watchedFates.Add(huntFateId!.Value);
            if (configuration.TrackedFateAlerts && trackedFates is { } tracked) {
                foreach (var fate in tracked.Items.Values)
                    if (fate.TerritoryId == zone.Territory && fate.MapId == zone.Map) watchedFates.Add(fate.FateId);
            }
            if (watchedFates.Count > 0) local = ReadAwarenessFates(zone.Territory, zone.Map, zone.Instance, watchedFates, now);
            if (waiting.Length > 0) ObserveWaitingBRanks(waiting);
        }
        sampledFateZone = local is null ? null : zone;
        if (local is not null) {
            foreach (var target in waiting)
                if (target.FateId is { } id && local.TryGetValue(id, out var running) && running.Running) huntDefer.SawUp(target);
            foreach (var id in localFates.Keys.Where(id => localFates[id].Territory == zone.Territory && localFates[id].Map == zone.Map
                && localFates[id].Instance == zone.Instance && !local.ContainsKey(id)).ToArray()) localFates.Remove(id);
            foreach (var (id, fate) in local) if (trackedFates?.Items.ContainsKey(id) == true) localFates[id] = fate;
        }
        if (command is not null) {
            if (HuntDeferState.Reason(command) == HuntDeferState.BRankNotSeen) {
                var mark = ObserveBRank(command);
                currentHuntAvailability = settled || mark.Availability == HuntAvailability.Up ? mark.Availability : HuntAvailability.Unknown;
                if (mark.Availability == HuntAvailability.Up) currentBRankSeenAt = BRankSightingTime(mark.Link, now);
                if (mark.Link is not null && huntAwarenessClock is { } clock)
                    RememberHuntTabMap(command, mark.Link, clock);
                if (!exactHuntFlag && !huntMapPollInFlight && mark.Link is not null) {
                    exactHuntFlag = true;
                    OpenExactHuntFlag(command, mark.Link);
                }
            } else {
                currentHuntAvailability = !huntFate || local is null ? HuntAvailability.Unknown
                    : local.TryGetValue(huntFateId!.Value, out var f) && f.Running ? HuntAvailability.Up : HuntAvailability.NotSeen;
            }
        }
        TickTrackedFateAlerts(now, world, settled);
    }

    private Dictionary<uint, LocalFate>? ReadAwarenessFates(uint territory, uint mapId, uint instance, HashSet<uint> watchedFates, DateTime now) {
        if (!FatePolicy.Compatible(fateLocal.ObservedSource) || huntFateTable.Length is < 0 or > FatePolicy.MaximumRows
            || ValidatedAwarenessMap(territory, mapId) is not { } map) return null;
        var result = new Dictionary<uint, LocalFate>();
        var count = huntFateTable.Length;
        for (var index = 0; index < count; index++) {
            var fate = huntFateTable[index];
            if (fate is null || !huntFateTable.IsValid(fate)) continue;
            var id = fate.FateId;
            if (!watchedFates.Contains(id) || fate.TerritoryType.RowId != territory) continue;
            var state = FatePolicy.State((byte)fate.State);
            if (state?.Kind is not ("running" or "preparing")) continue;
            var running = state.Kind == "running";
            var until = now.AddSeconds(Math.Clamp(fate.TimeRemaining, 30, 3600));
            result[id] = new(territory, mapId, instance, running ? fate.StartTimeEpoch : 0, until, running, PositionLink(territory, map, fate.Position));
        }
        return count == huntFateTable.Length ? result : null;
    }

    private void TickTrackedFateAlerts(DateTime now, uint world, bool settled) {
        if (trackedFates is not { } tracked) return;
        foreach (var item in tracked.Items.Values) {
            var inZone = clientState.TerritoryType == item.TerritoryId && clientState.MapId == item.MapId;
            if (inZone && !settled) continue; // Let the first in-zone table read supply the link.
            var local = localFates.TryGetValue(item.FateId, out var seen) && seen.Until > now ? seen : null;
            var sonar = configuration.UseSonarChatMessages && sonarFates.TryGetValue(item.FateId, out var relayUntil) && relayUntil > now;
            var remote = trackedFateWorld == world && tracked.Running.Contains(item.FateId);
            if (!fateAlerts.Observe(item.FateId, local is not null || sonar || remote, local?.Start ?? 0, configuration.TrackedFateAlerts)) continue;
            PrintTrackedFateAlert(item, inZone && local?.Instance == clientState.Instance ? local.Link : null);
        }
    }

    private void PrintTrackedFateAlert(TrackedFate fate, MapLinkPayload? link) {
        var message = new SeStringBuilder().AddText($"[Gillions] Your tracked FATE {fate.Name} started in {fate.ZoneName}");
        if (link is not null) message.AddText(" ").Append(SeString.CreateMapLink(fate.TerritoryId, fate.MapId, link.RawX, link.RawY));
        else if (fate.Location is { } point && ValidatedAwarenessMap(fate.TerritoryId, fate.MapId) is not null)
            message.AddText(" ").Append(SeString.CreateMapLink(fate.TerritoryId, fate.MapId, point.MapX, point.MapY));
        chatGui.Print(new XivChatEntry { Type = AlertChatType(configuration.TrackedFateChatTab), Message = message.Build(), Silent = true });
        if (configuration.TrackedFateAlertSound) UIGlobals.PlayChatSoundEffect(1);
    }

    private Map? ValidatedAwarenessMap(uint territory, uint mapId) {
        var map = dataManager.GetExcelSheet<Map>().GetRowOrDefault(mapId);
        return map is { SizeFactor: > 0 } value && value.TerritoryType.RowId == territory ? value : null;
    }

    private static MapLinkPayload? PositionLink(uint territory, Map map, Vector3 position) {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Z) || Math.Abs(position.X) > 4096 || Math.Abs(position.Z) > 4096) return null;
        return new(territory, map.RowId, (int)Math.Round(position.X * 1000), (int)Math.Round(position.Z * 1000));
    }

    private void ReadSonarChat(IHandleableChatMessage message) {
        if (!configuration.UseSonarChatMessages || (!huntDeferSupported && trackedFates is null)
            || !SonarChatRelay.IsSender(message.OriginalSender.ExtractText()) || !HuntAwarenessReady()) return;
        try {
            if (TryReadSonarChat(message)) return;
        } catch (Exception e) when (e is InvalidOperationException or FormatException or ArgumentException or IOException) { }
        if (ignoredSonarLines < int.MaxValue) ignoredSonarLines++;
    }

    private bool TryReadSonarChat(IHandleableChatMessage message) {
        if (message.OriginalMessage.ByteLength > 2048) return false;
        var links = SeString.Parse(message.OriginalMessage.Data.Span).Payloads.OfType<MapLinkPayload>().Take(2).ToArray();
        if (links.Length != 1) return false;
        var link = links[0];
        var territory = link.TerritoryType.RowId;
        var map = link.Map.RowId;
        if (ValidatedAwarenessMap(territory, map) is null || Math.Abs((long)link.RawX) > 4096000 || Math.Abs((long)link.RawY) > 4096000) return false;
        var worldId = AwarenessWorld();
        if (worldId == 0 || awarenessWorld != worldId) return false;
        var world = huntPlayerState.CurrentWorld.Value.Name.ExtractText();
        var content = message.OriginalMessage.ExtractText();
        var instance = SonarChatRelay.Instance(content);
        var now = DateTime.UtcNow;
        var command = CurrentAwareHunt(worldId);
        var bRank = command is not null && HuntDeferState.Reason(command) == HuntDeferState.BRankNotSeen;
        var matched = command is not null && command.TerritoryId == territory && command.MapId == map
            && SonarChatRelay.Matches(content, world, bRank ? HuntMonsterName.Display(command.TargetName) : command.FateName ?? "", bRank);
        if (matched) huntSonar = (now.AddSeconds(60), instance, link);
        if (huntDeferSupported && HuntReceivingEnabled && (territory != clientState.TerritoryType || instance == clientState.Instance)) {
            foreach (var target in huntDefer.Waiting(worldId, now)) {
                if (target.TerritoryId != territory || target.MapId != map
                    || !SonarChatRelay.Matches(content, world, target.Name, target.FateId is null)) continue;
                huntDefer.SawUp(target);
                matched = true;
            }
        }
        if (!configuration.TrackedFateAlerts || trackedFates is not { } tracked) return matched;
        var matches = tracked.Items.Values.Where(f => f.TerritoryId == territory && f.MapId == map
            && SonarChatRelay.Matches(content, world, f.Name, false)).Take(2).ToArray();
        if (matches.Length != 1) return matched;
        var id = matches[0].FateId;
        if (!sonarFates.TryGetValue(id, out var prior) || prior <= now) sonarFates[id] = now.AddMinutes(30);
        return true;
    }

    private static XivChatType AlertChatType(int tab) => tab switch {
        1 => XivChatType.SystemMessage,
        2 => XivChatType.Notice,
        _ => XivChatType.Echo,
    };

    private void DrawHuntAwarenessSettings() {
        var alerts = configuration.TrackedFateAlerts;
        var sound = configuration.TrackedFateAlertSound;
        var tab = Math.Clamp(configuration.TrackedFateChatTab, 0, 2);
        var sonar = configuration.UseSonarChatMessages;
        if (ImGui.Checkbox("Tracked FATE alerts", ref alerts)) QueueUiAction(() => { configuration.TrackedFateAlerts = alerts; RequestConfigurationSave(); });
        ImGui.BeginDisabled(!alerts);
        if (ImGui.Checkbox("Alert sound", ref sound)) QueueUiAction(() => { configuration.TrackedFateAlertSound = sound; RequestConfigurationSave(); });
        if (ImGui.Combo("Chat tab", ref tab, "Echo\0System messages\0Notices\0"))
            QueueUiAction(() => { configuration.TrackedFateChatTab = tab; RequestConfigurationSave(); });
        ImGui.EndDisabled();
        if (ImGui.Checkbox("Use Sonar's chat messages", ref sonar)) QueueUiAction(() => {
            configuration.UseSonarChatMessages = sonar;
            if (!sonar) { huntSonar = null; sonarFates.Clear(); currentHuntAvailability = HuntAvailability.Unknown; }
            RequestConfigurationSave();
        });
    }
}
#endif
