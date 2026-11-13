using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Lumina.Excel.Sheets;

namespace GillionsGameSync;

// Display-only rows from the existing admitted bill and Hunt map observations.
internal sealed record HuntTabBillRow(byte BillId, uint OrderId, bool Weekly, byte Tier,
    HuntProgressRow Target, string Zone, uint TargetId, uint MapId, uint FateId, string? FateName);
internal sealed record HuntTabBills(HuntTabBillRow[] Rows, bool EmptyConfirmed, long ExpiresAt) {
    internal static readonly HuntTabBills Unknown = new([], false, 0);
}

internal sealed record HuntTabMapPoint(uint TargetId, uint MapId, uint World, uint Territory, uint PlayerMap, uint Instance,
    MapLinkPayload Link, WebsiteResponseClock Clock, DateTime ExpiresAt) {
    internal DateTime? RelayUntil { get; init; }
    internal bool AppliesTo(uint world, uint territory, uint map, uint instance) =>
        World != 0 && World == world && Territory == territory && PlayerMap == map && Instance == instance
        && Clock.Unexpired(ExpiresAt);
    internal bool Matches(uint targetId, uint mapId) => TargetId == targetId && MapId == mapId;
}

internal sealed record HuntTabMark(uint TargetId, string Name, string Zone, DateTime? SeenAt, HuntTabMapPoint? Point);
internal sealed record HuntsTabView(string Location, HuntTabBills Bills, HuntTabMark[] Marks, HuntTabMapPoint? Point) {
    internal static readonly HuntsTabView Empty = new("", HuntTabBills.Unknown, [], null);
}

public sealed partial class Plugin {
    private volatile HuntsTabView huntTab = HuntsTabView.Empty;
    private HuntTabMapPoint? huntTabMap;

    private void RememberHuntTabMap(HuntMapRequest request, MapLinkPayload link, WebsiteResponseClock clock) {
#if GILLIONS_TEST_BUILD
        // A relay link is still subject to the source toggle at presentation and click.
        DateTime? relayUntil = huntSonar is { } relay && ReferenceEquals(link, relay.Link) ? relay.Until : null;
#else
        DateTime? relayUntil = null;
#endif
        if (huntTabMap is { } previous && previous.Matches(request.TargetId, request.MapId)
            && previous.Clock == clock && previous.RelayUntil == relayUntil
            && previous.Link.RawX == link.RawX && previous.Link.RawY == link.RawY
            && previous.AppliesTo(playerState.CurrentWorld.RowId, clientState.TerritoryType, clientState.MapId, clientState.Instance)) return;
        huntTabMap = new(request.TargetId, request.MapId, playerState.CurrentWorld.RowId,
            clientState.TerritoryType, clientState.MapId, clientState.Instance, link, clock, request.ExpiresAtUtc) {
            RelayUntil = relayUntil
        };
    }

    private HuntTabMapPoint? CurrentHuntTabPoint() {
        if (!HuntReceivingEnabled || !clientState.IsLoggedIn || !playerState.IsLoaded
            || marketConditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]
            || marketConditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51]) return null;
        var point = huntTabMap;
#if GILLIONS_TEST_BUILD
        if (point?.RelayUntil is { } until && (!configuration.UseSonarChatMessages || until <= DateTime.UtcNow)) return null;
#endif
        return point?.AppliesTo(playerState.CurrentWorld.RowId, clientState.TerritoryType,
            clientState.MapId, clientState.Instance) == true ? point : null;
    }

    private HuntsTabView ReadHuntsTab(DateTime now) {
        var point = CurrentHuntTabPoint();
        var marks = new List<HuntTabMark>();
#if GILLIONS_TEST_BUILD
        if (huntDeferSupported && HuntReceivingEnabled && HuntAwarenessReady()) {
            var world = AwarenessWorld();
            var command = CurrentAwareHunt(world);
            var active = command is not null && HuntDeferState.Reason(command) == HuntDeferState.BRankNotSeen ? command : null;
            string Zone(uint territory) =>
                dataManager.GetExcelSheet<TerritoryType>().GetRowOrDefault(territory)?.PlaceName.Value.Name.ExtractText() is { Length: > 0 } name
                    ? name : "Zone unknown";
            if (active is not null)
                marks.Add(new(active.TargetId, HuntMonsterName.Display(active.TargetName), Zone(active.TerritoryId),
                    currentBRankSeenAt, point?.Matches(active.TargetId, active.MapId) == true ? point : null));
            foreach (var mark in huntDefer.DeferredBRanks(world, now)) {
                if (mark.TargetId == active?.TargetId) continue;
                marks.Add(new(mark.TargetId, HuntMonsterName.Display(mark.Name), Zone(mark.TerritoryId), null,
                    point?.Matches(mark.TargetId, mark.MapId) == true ? point : null));
            }
        }
#endif
        return new(travelStatus, huntLocal.TabBills, marks.ToArray(), point);
    }

    private void DrawHuntsTab(HuntsTabView view, HuntsFates fates) {
        ImGui.Spacing();
        if (view.Location.Length > 0) Label(view.Location);
        bool wide = ImGui.GetContentRegionAvail().X >= ImGui.GetFontSize() * 30;
        long tick = Environment.TickCount64;
        DrawHuntTabHeading("Hunt bill targets");
        var bills = view.Bills;
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(.28f, .41f, .55f, 1));
        if (Environment.TickCount64 >= bills.ExpiresAt || (!bills.EmptyConfirmed && bills.Rows.Length == 0))
            Label("Hunt bill state is unknown.");
        else if (bills.EmptyConfirmed)
            Label("No hunt bills open");

        else
            foreach (var group in bills.Rows.GroupBy(row => (row.Weekly, row.BillId, row.OrderId, row.Tier))
                .OrderBy(group => group.Key.Weekly).ThenBy(group => group.Key.BillId)) {
                ImGui.PushID(group.Key.BillId);
                Label(group.Key.Weekly ? "Weekly bill" : $"Daily bill · Tier {group.Key.Tier}");
                bool table = wide && ImGui.BeginTable("Bill targets", 3,
                    ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.BordersInnerH);
                if (table) {
                    ImGui.TableSetupColumn("Target", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Progress", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 7);
                    ImGui.TableSetupColumn("Map", ImGuiTableColumnFlags.WidthFixed);
                }
                foreach (var row in group) {
                    ImGui.PushID(row.Target.Key);
                    if (table) { ImGui.TableNextRow(); ImGui.TableNextColumn(); }
                    else ImGui.Separator();
                    bool fateLinked = !string.IsNullOrWhiteSpace(row.FateName);
                    var fate = fateLinked ? Array.Find(fates.Rows, candidate => candidate.Id == row.FateId) : null;
                    if (fateLinked) {
                        DrawHuntBadge("FATE", true);
                        ImGui.SameLine();
                    }
                    Label(row.Target.Name);
                    Label(row.Zone);
                    if (table) ImGui.TableNextColumn();
                    float barWidth = table ? -1 : Math.Max(ImGui.GetFontSize() * 3, ImGui.GetContentRegionAvail().X
                        - ImGui.CalcTextSize("Flag on map").X - 2 * ImGui.GetStyle().FramePadding.X - ImGui.GetStyle().ItemSpacing.X);
                    ImGui.ProgressBar((float)row.Target.Current / row.Target.Required, new Vector2(barWidth, 0),
                        $"{row.Target.Current}/{row.Target.Required} kills");
                    if (table) ImGui.TableNextColumn();
                    else ImGui.SameLine();
                    var point = view.Point?.Matches(row.TargetId, row.MapId) == true ? view.Point : null;
                    if (point is null && fate is { Position: not null }) DrawFateMapButton(fates, fate);
                    else DrawHuntTabMapButton(point);
                    if (fateLinked) {
                        if (table) { ImGui.TableNextRow(); ImGui.TableNextColumn(); }
                        DrawBillFate(row.FateName!, fate, fates.Complete, table, tick);
                    }
                    ImGui.PopID();
                }
                if (table) ImGui.EndTable();
                ImGui.PopID();
            }

        DrawHuntTabHeading("B-rank marks");
        if (view.Marks.Length == 0) Label("No B-rank marks to show.");
        bool markTable = view.Marks.Length > 0 && wide && ImGui.BeginTable("B-rank rows", 3,
            ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.BordersInnerH);
        if (markTable) {
            ImGui.TableSetupColumn("Mark", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Last seen", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 9);
            ImGui.TableSetupColumn("Map", ImGuiTableColumnFlags.WidthFixed);
        }
        foreach (var mark in view.Marks) {
            ImGui.PushID((int)mark.TargetId);
            if (markTable) { ImGui.TableNextRow(); ImGui.TableNextColumn(); }
            else ImGui.Separator();
            DrawHuntBadge("B", false);
            ImGui.SameLine();
            ImGui.BeginGroup();
            Label(mark.Name);
            Label(mark.Zone);
            ImGui.EndGroup();
            if (markTable) ImGui.TableNextColumn();
            Label(mark.SeenAt is { } seen ? LastSeenLabel(seen, DateTime.UtcNow) : "Last seen unknown");
            if (markTable) ImGui.TableNextColumn();
            DrawHuntTabMapButton(mark.Point);
            ImGui.PopID();
        }
        if (markTable) ImGui.EndTable();
        DrawFatesHere(fates, wide, tick);
        ImGui.PopStyleColor();
    }

    private static void DrawBillFate(string name, HuntsFateRow? row, bool complete, bool table, long tick) {
        Label(name);
        if (row is null) {
            Label(complete ? "Not active here" : "FATE status unavailable");
            if (table) ImGui.TableNextColumn();
            Label("Progress unavailable");
            if (table) ImGui.TableNextColumn();
            Label("Time unavailable");
            return;
        }
        if (table) ImGui.TableNextColumn();
        DrawFateProgress(row, "FATE ");
        if (table) ImGui.TableNextColumn();
        Label(row.TimeLeft(tick));
    }

    private void DrawFatesHere(HuntsFates snapshot, bool wide, long tick) {
        DrawHuntTabHeading("FATEs here");
        if (snapshot.Rows.Length == 0) {
            Label(snapshot.Complete ? "No FATEs active in this area" : "FATEs unavailable in this area");
            return;
        }
        bool table = wide && ImGui.BeginTable("Local FATE rows", 4,
            ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.BordersInnerH);
        if (table) {
            ImGui.TableSetupColumn("FATE", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Progress", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 6);
            ImGui.TableSetupColumn("Time and state", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 6);
            ImGui.TableSetupColumn("Map", ImGuiTableColumnFlags.WidthFixed);
        }
        for (int index = 0; index < snapshot.Rows.Length; index++) {
            var row = snapshot.Rows[index];
            ImGui.PushID(index);
            if (table) { ImGui.TableNextRow(); ImGui.TableNextColumn(); }
            else ImGui.Separator();
            Label(row.Name);
            Label(row.Level is { } level ? $"Level {level}" : "Level unknown");
            if (table) ImGui.TableNextColumn();
            string time = row.TimeLeft(tick);
            string state = row.State switch { "preparing" => "Preparing", "running" => "Running", _ => "Ending" };
            var style = ImGui.GetStyle();
            float remaining = ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Flag on map").X
                - 2 * style.FramePadding.X - 2 * style.ItemSpacing.X
                - Math.Max(ImGui.CalcTextSize(time).X, ImGui.CalcTextSize(state).X);
            bool inline = !table && remaining >= ImGui.GetFontSize() * 9;
            DrawFateProgress(row, "", inline ? remaining : -1);
            if (table) ImGui.TableNextColumn();
            else if (inline) ImGui.SameLine();
            ImGui.BeginGroup();
            Label(time);
            Label(state);
            ImGui.EndGroup();
            if (table) ImGui.TableNextColumn();
            else if (inline) ImGui.SameLine();
            DrawFateMapButton(snapshot, row);
            ImGui.PopID();
        }
        if (table) ImGui.EndTable();
        if (!snapshot.Complete) Label("Some FATE details are unavailable.");
    }

    private static void DrawFateProgress(HuntsFateRow row, string prefix, float width = -1) {
        if (row.Progress is { } progress)
            ImGui.ProgressBar(progress / 100f, new Vector2(width, 0), $"{prefix}{progress}%");
        else Label("Progress unavailable");
    }

    private void DrawFateMapButton(HuntsFates snapshot, HuntsFateRow row) {
        ImGui.BeginDisabled(row.Position is null);
        if (ImGui.Button("Flag on map") && row.Position is { } position)
            QueueUiAction(() => {
                if (!fateLocal.CanFlag(snapshot) || !HuntsFateRow.ValidPosition(position)) return;
                var territory = snapshot.Context!.Territory;
                var map = dataManager.GetExcelSheet<Map>().GetRowOrDefault(snapshot.Map);
                if (map is not { SizeFactor: > 0 } || map.Value.TerritoryType.RowId != territory) return;
                huntMapGui.OpenMapWithMapLink(new MapLinkPayload(territory, snapshot.Map,
                    (int)Math.Round(position.X * 1000), (int)Math.Round(position.Z * 1000)));
            });
        ImGui.EndDisabled();
    }

    private static string LastSeenLabel(DateTime seen, DateTime now) {
        var age = now - seen;
        if (age.TotalMinutes < 1) return "Last seen less than a minute ago";
        if (age.TotalHours < 1) return $"Last seen {(int)age.TotalMinutes} min ago";
        return $"Last seen {seen.ToLocalTime():t}";
    }

    private static void DrawHuntTabHeading(string text) {
        ImGui.Spacing();
        var start = ImGui.GetCursorScreenPos();
        var padding = ImGui.GetStyle().FramePadding;
        var size = new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetTextLineHeight() + 2 * padding.Y);
        var ink = ImGui.GetWindowDrawList();
        ink.AddRectFilled(start, start + size, ImGui.GetColorU32(new Vector4(.14f, .18f, .23f, 1)));
        ink.AddText(start + padding, ImGui.GetColorU32(ImGuiCol.Text), text);
        ImGui.Dummy(size);
    }

    private static void DrawHuntBadge(string text, bool fate) {
        var start = ImGui.GetCursorScreenPos();
        float height = ImGui.GetTextLineHeight();
        float padding = height / 4;
        float iconWidth = fate ? height * .65f : 0;
        var size = new Vector2(ImGui.CalcTextSize(text).X + 2 * padding + iconWidth, height + 2);
        var ink = ImGui.GetWindowDrawList();
        uint colour = ImGui.GetColorU32(fate ? new Vector4(.88f, .78f, 1, 1) : new Vector4(.62f, .82f, 1, 1));
        ink.AddRectFilled(start, start + size, ImGui.GetColorU32(new Vector4(.15f, .17f, .23f, 1)), 3);
        ink.AddRect(start, start + size, colour, 3);
        if (fate) {
            var center = start + new Vector2(padding + height / 4, size.Y / 2);
            var x = new Vector2(height / 5, 0);
            var y = new Vector2(0, height / 5);
            ink.AddQuadFilled(center - x, center - y, center + x, center + y, colour);
        }
        ink.AddText(start + new Vector2(padding + iconWidth, 1), colour, text);
        ImGui.Dummy(size);
    }

    private void DrawHuntTabMapButton(HuntTabMapPoint? point) {
        ImGui.BeginDisabled(point is null || !point.Clock.Unexpired(point.ExpiresAt) || point.RelayUntil <= DateTime.UtcNow);
        if (ImGui.Button("Flag on map") && point is not null)
            QueueUiAction(() => {
                if (ReferenceEquals(point, CurrentHuntTabPoint())) huntMapGui.OpenMapWithMapLink(point.Link);
            });
        ImGui.EndDisabled();
    }
}
