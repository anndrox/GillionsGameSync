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
    HuntProgressRow Target, string Zone, uint TargetId, uint MapId);
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

    private void DrawHuntsTab(HuntsTabView view) {
        ImGui.Spacing();
        if (view.Location.Length > 0) Label(view.Location);
        bool wide = ImGui.GetContentRegionAvail().X >= ImGui.GetFontSize() * 30;
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
                    Label(row.Target.Name);
                    Label(row.Zone);
                    if (table) ImGui.TableNextColumn();
                    float barWidth = table ? -1 : Math.Max(ImGui.GetFontSize() * 3, ImGui.GetContentRegionAvail().X
                        - ImGui.CalcTextSize("Flag on map").X - 2 * ImGui.GetStyle().FramePadding.X - ImGui.GetStyle().ItemSpacing.X);
                    ImGui.ProgressBar((float)row.Target.Current / row.Target.Required, new Vector2(barWidth, 0),
                        $"{row.Target.Current}/{row.Target.Required}");
                    if (table) ImGui.TableNextColumn();
                    else ImGui.SameLine();
                    DrawHuntTabMapButton(view.Point?.Matches(row.TargetId, row.MapId) == true ? view.Point : null);
                    ImGui.PopID();
                }
                if (table) ImGui.EndTable();
                ImGui.PopID();
            }

        ImGui.PopStyleColor();
        DrawHuntTabHeading("B-rank marks");
        if (view.Marks.Length == 0) {
            Label("No B-rank marks to show.");
            return;
        }
        bool markTable = wide && ImGui.BeginTable("B-rank rows", 3,
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
            DrawBRankBadge();
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

    private static void DrawBRankBadge() {
        var start = ImGui.GetCursorScreenPos();
        float size = ImGui.GetTextLineHeight();
        var ink = ImGui.GetWindowDrawList();
        ink.AddRectFilled(start, start + new Vector2(size), ImGui.GetColorU32(new Vector4(.13f, .24f, .35f, 1)), size / 2);
        ink.AddText(start + new Vector2((size - ImGui.CalcTextSize("B").X) / 2, 0),
            ImGui.GetColorU32(new Vector4(.62f, .82f, 1, 1)), "B");
        ImGui.Dummy(new Vector2(size));
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
