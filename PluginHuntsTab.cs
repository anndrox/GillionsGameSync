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
        if (view.Location.Length > 0 && view.Location != LocationUploadPresentation.Sent) Label(view.Location);
        long tick = Environment.TickCount64;
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(.28f, .41f, .55f, 1));
        DrawBillTargets(view, fates, tick);
        DrawBRankMarks(view.Marks);
        DrawFatesHere(fates, tick);
        ImGui.PopStyleColor();
    }

    private void DrawBillTargets(HuntsTabView view, HuntsFates fates, long tick) {
        var bills = view.Bills;
        if (tick >= bills.ExpiresAt || bills.EmptyConfirmed || bills.Rows.Length == 0) return;
        DrawHuntTabHeading("Hunt bill targets");
        foreach (var group in bills.Rows.GroupBy(row => (row.Weekly, row.BillId, row.OrderId, row.Tier))
            .OrderBy(group => group.Key.Weekly).ThenBy(group => group.Key.BillId)) {
            ImGui.PushID(group.Key.BillId);
            Label(group.Key.Weekly ? "Weekly bill" : $"Daily bill · Tier {group.Key.Tier}");
            if (BeginHuntRows("Bill targets", true)) {
                foreach (var row in group) {
                    ImGui.PushID(row.Target.Key);
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    bool fateLinked = !string.IsNullOrWhiteSpace(row.FateName);
                    var fate = fateLinked ? Array.Find(fates.Rows, candidate => candidate.Id == row.FateId) : null;
                    if (fateLinked) {
                        DrawHuntBadge("FATE", true);
                        ImGui.SameLine();
                    }
                    DrawHuntName(row.Target.Name, row.Zone);
                    ImGui.TableNextColumn();
                    ImGui.ProgressBar((float)row.Target.Current / row.Target.Required, new Vector2(-1, 0),
                        $"{row.Target.Current}/{row.Target.Required} kills");
                    ImGui.TableSetColumnIndex(3);
                    var point = view.Point?.Matches(row.TargetId, row.MapId) == true ? view.Point : null;
                    if (point is null && fate is { Position: not null }) DrawFateMapButton(fates, fate);
                    else DrawHuntTabMapButton(point);
                    if (fate is not null) {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Indent(ImGui.GetFontSize());
                        DrawHuntName(row.FateName!, fates.Zone, FateLevel(fate));
                        ImGui.Unindent(ImGui.GetFontSize());
                        ImGui.TableNextColumn();
                        DrawFateProgress(fate, "FATE ");
                        ImGui.TableNextColumn();
                        DrawHuntCellText(fate.TimeLeft(tick));
                    }
                    ImGui.PopID();
                }
                ImGui.EndTable();
            }
            ImGui.PopID();
        }
    }

    private void DrawBRankMarks(HuntTabMark[] marks) {
        if (marks.Length == 0) return;
        DrawHuntTabHeading("B-rank marks");
        if (!BeginHuntRows("B-rank rows", false)) return;
        foreach (var mark in marks) {
            ImGui.PushID((int)mark.TargetId);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            DrawHuntBadge("B", false);
            ImGui.SameLine();
            DrawHuntName(mark.Name, mark.Zone);
            ImGui.TableNextColumn();
            DrawHuntCellText(mark.SeenAt is { } seen ? LastSeenLabel(seen, DateTime.UtcNow) : "Last seen unknown");
            ImGui.TableNextColumn();
            DrawHuntTabMapButton(mark.Point);
            ImGui.PopID();
        }
        ImGui.EndTable();
    }

    private void DrawFatesHere(HuntsFates snapshot, long tick) {
        if (snapshot.Rows.Length == 0) return;
        DrawHuntTabHeading("FATEs here");
        if (!snapshot.Complete && ImGui.IsItemHovered()) ImGui.SetTooltip("Some FATE details are unavailable.");
        if (!BeginHuntRows("Local FATE rows", true)) return;
        for (int index = 0; index < snapshot.Rows.Length; index++) {
            var row = snapshot.Rows[index];
            ImGui.PushID(index);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            DrawHuntName(row.Name, snapshot.Zone, FateLevel(row));
            ImGui.TableNextColumn();
            DrawFateProgress(row, "");
            ImGui.TableNextColumn();
            DrawHuntCellText(row.TimeLeft(tick));
            ImGui.TableNextColumn();
            DrawFateMapButton(snapshot, row);
            ImGui.PopID();
        }
        ImGui.EndTable();
    }

    private static bool BeginHuntRows(string id, bool progress) {
        if (!ImGui.BeginTable(id, progress ? 4 : 3, ImGuiTableFlags.SizingFixedFit)) return false;
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
        if (progress) ImGui.TableSetupColumn("Progress", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 5);
        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * (progress ? 6 : 10));
        ImGui.TableSetupColumn("Map", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
        return true;
    }

    private static string FateLevel(HuntsFateRow row) => row.Level is { } level ? $"Level {level}" : "Level unknown";

    private static void DrawHuntName(string name, string zone, string? level = null) {
        DrawHuntCellText(name);
        if (!ImGui.IsItemHovered()) return;
        ImGui.BeginTooltip();
        ImGui.TextUnformatted(name);
        ImGui.TextUnformatted(zone);
        if (level is not null) ImGui.TextUnformatted(level);
        ImGui.EndTooltip();
    }

    private static void DrawHuntCellText(string text) {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(text);
    }

    private static void DrawFateProgress(HuntsFateRow row, string prefix) {
        if (row.State == "preparing") ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * .6f);
        if (row.Progress is { } progress)
            ImGui.ProgressBar(progress / 100f, new Vector2(-1, 0), $"{prefix}{progress}%");
        else {
            DrawHuntCellText("Unknown");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Progress unavailable");
        }
        if (row.State == "preparing") ImGui.PopStyleVar();
    }

    private static bool DrawFlagButton(bool disabled) {
        float size = ImGui.GetFrameHeight();
        ImGui.BeginDisabled(disabled);
        bool clicked = ImGui.Button("##Flag on map", new Vector2(size));
        var start = ImGui.GetItemRectMin() + new Vector2(size * .31f, size * .27f);
        var ink = ImGui.GetWindowDrawList();
        uint colour = ImGui.GetColorU32(ImGuiCol.Text);
        ink.AddLine(start, start + new Vector2(0, size * .49f), colour, Math.Max(1, size * .065f));
        ink.AddTriangleFilled(start, start + new Vector2(size * .45f, size * .13f),
            start + new Vector2(0, size * .27f), colour);
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("Flag on map");
        return clicked;
    }

    private void DrawFateMapButton(HuntsFates snapshot, HuntsFateRow row) {
        if (DrawFlagButton(row.Position is null) && row.Position is { } position)
            QueueUiAction(() => {
                if (!fateLocal.CanFlag(snapshot) || !HuntsFateRow.ValidPosition(position)) return;
                var territory = snapshot.Context!.Territory;
                var map = dataManager.GetExcelSheet<Map>().GetRowOrDefault(snapshot.Map);
                if (map is not { SizeFactor: > 0 } || map.Value.TerritoryType.RowId != territory) return;
                huntMapGui.OpenMapWithMapLink(new MapLinkPayload(territory, snapshot.Map,
                    (int)Math.Round(position.X * 1000), (int)Math.Round(position.Z * 1000)));
            });
    }

    private static string LastSeenLabel(DateTime seen, DateTime now) {
        var age = now - seen;
        if (age.TotalMinutes < 1) return "Last seen <1 min ago";
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
        if (DrawFlagButton(point is null || !point.Clock.Unexpired(point.ExpiresAt) || point.RelayUntil <= DateTime.UtcNow)
            && point is not null)
            QueueUiAction(() => {
                if (ReferenceEquals(point, CurrentHuntTabPoint())) huntMapGui.OpenMapWithMapLink(point.Link);
            });
    }
}
