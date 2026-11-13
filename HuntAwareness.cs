#if GILLIONS_TEST_BUILD || GILLIONS_PUBLIC_BUILD || GILLIONS_HUNT_MAP_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GillionsGameSync;

internal readonly record struct FateMapPoint(float MapX, float MapY);
internal sealed record TrackedFate(uint FateId, string Name, uint TerritoryId, uint MapId, string ZoneName, FateMapPoint? Location);

internal sealed record TrackedFates(string Revision, Dictionary<uint, TrackedFate> Items, HashSet<uint> Running) {
    internal static TrackedFates? Parse(JsonElement field, TrackedFates? previous) {
        if (!HuntMapPolicy.Exact(field, "schemaVersion", "revision", "items", "runningOnYourWorld")
            || Encoding.UTF8.GetByteCount(field.GetRawText()) > 8192
            || field.GetProperty("schemaVersion").GetInt32() != 1) return null;
        var revision = field.GetProperty("revision").GetString();
        if (!HuntMapPolicy.Hex(revision, 64)) return null;
        var items = field.GetProperty("items");
        var running = field.GetProperty("runningOnYourWorld");
        if (items.GetArrayLength() > 64 || running.GetArrayLength() > 64) return null;
        var unchanged = previous is not null && previous.Revision == revision;
        var catalog = unchanged ? previous!.Items : new Dictionary<uint, TrackedFate>();
        if (!unchanged) foreach (var item in items.EnumerateArray()) {
            string[] fields = ["fateId", "name", "territoryId", "mapId", "zoneName"];
            if (item.ValueKind != JsonValueKind.Object) return null;
            var hasLocation = item.TryGetProperty("location", out var location);
            if (!HuntMapPolicy.Exact(item, hasLocation ? [.. fields, "location"] : fields)) return null;
            var id = item.GetProperty("fateId").GetUInt32();
            var territory = item.GetProperty("territoryId").GetUInt32();
            var map = item.GetProperty("mapId").GetUInt32();
            var name = item.GetProperty("name").GetString();
            var zone = item.GetProperty("zoneName").GetString();
            FateMapPoint? point = null;
            if (hasLocation && location.ValueKind != JsonValueKind.Null) {
                if (!HuntMapPolicy.Exact(location, "mapX", "mapY")) return null;
                var x = location.GetProperty("mapX").GetSingle();
                var y = location.GetProperty("mapY").GetSingle();
                if (!(x is >= 0 and <= 100 && y is >= 0 and <= 100)) return null;
                point = new(x, y);
            }
            if (id == 0 || territory == 0 || map == 0 || !HuntMapPolicy.Text(name, 120) || !HuntMapPolicy.Text(zone, 80)
                || !catalog.TryAdd(id, new(id, name!, territory, map, zone!, point))) return null;
        }
        var active = new HashSet<uint>();
        foreach (var value in running.EnumerateArray()) {
            var id = value.GetUInt32();
            if (!catalog.ContainsKey(id) || !active.Add(id)) return null;
        }
        return new(revision!, catalog, active);
    }
}

internal sealed class FateAlertOccurrences {
    private sealed record Occurrence(int Start, bool Alerted);
    private readonly Dictionary<uint, Occurrence> active = new();

    internal bool Observe(uint id, bool isActive, int start, bool enabled) {
        if (!isActive) { active.Remove(id); return false; }
        if (!active.TryGetValue(id, out var occurrence) || (start > 0 && occurrence.Start > 0 && start != occurrence.Start))
            occurrence = new(start, false);
        var alert = enabled && !occurrence.Alerted;
        active[id] = new(start > 0 ? start : occurrence.Start, occurrence.Alerted || alert);
        return alert;
    }

    internal void Retain(TrackedFates? tracked) {
        foreach (var id in active.Keys.Where(id => tracked?.Items.ContainsKey(id) != true).ToArray()) active.Remove(id);
    }

    internal void Clear() => active.Clear();
}

internal sealed class HuntDeferAction(HuntMapRequest command, string? reason) {
    internal HuntMapRequest Command { get; } = command;
    internal string? Reason { get; } = reason;
    internal string Name => Reason is null ? "clear-defer" : "defer";
    internal int CapMinutes => Reason == HuntDeferState.FateNotUp ? 30 : 5;

    // Omitting the optional until uses the site's cap and avoids client clock skew.
    internal object Body() {
        var body = new Dictionary<string, string> {
            ["capability"] = HuntMapPolicy.CapabilityV2, ["action"] = Name, ["requestId"] = Command.RequestId,
            ["claimToken"] = Command.ClaimToken, ["revision"] = Command.Revision, ["candidateId"] = Command.CandidateId,
        };
        if (Reason is not null) body.Add("reason", Reason);
        return body;
    }

    internal bool TryProgression(JsonElement value, DateTime now, out DateTime? until) {
        until = null;
        if (Reason is null)
            return HuntMapPolicy.Exact(value, "deferCleared") && value.GetProperty("deferCleared").ValueKind == JsonValueKind.True;
        if (!HuntMapPolicy.Exact(value, "deferred", "until") || value.GetProperty("deferred").ValueKind != JsonValueKind.True
            || !HuntMapPolicy.Utc(value.GetProperty("until").GetString(), out var end)) return false;
        // Consume the site's clamped deadline. Bound local memory even when clocks disagree.
        until = end <= now.AddMinutes(CapMinutes) ? end : now.AddMinutes(CapMinutes);
        return true;
    }
}

internal enum HuntAvailability { Unknown, Up, NotSeen }

internal static class BRankSighting {
    internal static (HuntAvailability Availability, T? Link) Resolve<T>(bool targetLoaded, T? objectLink, T? sonarLink,
        bool nearSpawn, bool inFlight = false) where T : class {
        var link = objectLink ?? sonarLink;
        var availability = targetLoaded || link is not null ? HuntAvailability.Up
            : nearSpawn && !inFlight ? HuntAvailability.NotSeen : HuntAvailability.Unknown;
        return (availability, link);
    }
}

internal sealed class HuntDeferState {
    internal const string FateNotUp = "FATE_NOT_UP";
    internal const string BRankNotSeen = "B_RANK_NOT_SEEN";
    internal sealed record WaitingTarget(uint TargetId, string Name, uint? FateId, uint TerritoryId, uint MapId,
        uint World, DateTime Until, bool SeenUp = false);

    private readonly Dictionary<uint, WaitingTarget> deferred = new();
    private readonly Dictionary<uint, DateTime> throttled = new();
    internal HuntMapRequest? Current { get; private set; }
    internal bool CanDefer { get; private set; }

    internal static string? Reason(HuntMapRequest command) => command.CandidateKind switch {
        "FATE_REQUIRED" => FateNotUp,
        "DISCRETE_SPAWN_LOCATIONS" => BRankNotSeen,
        _ => null,
    };

    internal void Consumed(HuntMapRequest command) {
        Current = command;
        CanDefer = true;
        deferred.Remove(command.TargetId); // This target has already returned to the route.
    }

    internal void RetirePresentation() => CanDefer = false;

    internal void ForgetCurrent() {
        Current = null;
        RetirePresentation();
    }

    internal WaitingTarget[] Waiting(uint world, DateTime now) {
        PruneWaiting(now);
        return deferred.Values.Where(target => target.World == world && !target.SeenUp).ToArray();
    }

    internal IEnumerable<WaitingTarget> DeferredBRanks(uint world, DateTime now) =>
        deferred.Values.Where(target => target.World == world && target.FateId is null && target.Until > now);

    private void PruneWaiting(DateTime now) {
        foreach (var target in deferred.Values.Where(target => target.Until <= now).ToArray()) deferred.Remove(target.TargetId);
    }

    internal void SawUp(WaitingTarget target) {
        if (deferred.TryGetValue(target.TargetId, out var waiting) && ReferenceEquals(target, waiting))
            deferred[target.TargetId] = waiting with { SeenUp = true };
    }

    internal HuntDeferAction? Plan(HuntMapRequest command, HuntAvailability availability, uint world, DateTime now) {
        if (!ReferenceEquals(command, Current)) return null;
        foreach (var id in throttled.Keys.Where(id => throttled[id] <= now).ToArray()) throttled.Remove(id);
        PruneWaiting(now);
        if (throttled.ContainsKey(command.TargetId) || throttled.Count >= 32) return null;
        if (deferred.Values.Any(target => target.World == world && target.SeenUp)) return new(command, null);
        if (!CanDefer || availability != HuntAvailability.NotSeen || Reason(command) is not { } reason) return null;
        if (reason == BRankNotSeen && command.CandidateIndex != command.CandidateCount - 1) return null;
        return new(command, reason);
    }

    internal void Sent(HuntDeferAction action, DateTime now) => throttled[action.Command.TargetId] = now.AddSeconds(60);

    internal void Acknowledge(HuntDeferAction action, uint world, DateTime? until, bool seenUp = false) {
        if (!ReferenceEquals(action.Command, Current)) return;
        var command = action.Command;
        if (until is { } end && deferred.Count < 32)
            deferred[command.TargetId] = new(command.TargetId, command.FateName ?? command.TargetName, command.FateId,
                command.TerritoryId, command.MapId, world, end, seenUp);
        else if (action.Reason is null) deferred.Clear();
        // A null next request leaves the latest consumed identity available for clear-defer.
        RetirePresentation();
    }

    internal void Reset() {
        ForgetCurrent(); deferred.Clear(); throttled.Clear();
    }
}

internal static class SonarChatRelay {
    internal static bool IsSender(string sender) => sender == "Sonar";

    internal static bool Matches(string text, string world, string name, bool bRank) {
        if (text.Length is 0 or > 512 || string.IsNullOrEmpty(world) || string.IsNullOrEmpty(name)
            || !text.Contains($"<{world}>", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(text, @"\b(killed|dead|defeated|ended|failed|completed|despawned|cooldown|no longer)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return false;
        // These are local relay markers, independent of the colour and icon payloads.
        var kind = bRank ? @"\bRank B\b" : @"\bFATE\b";
        return Regex.IsMatch(text, kind, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            && Regex.IsMatch(text, $@"(?<![\p{{L}}\p{{N}}'’-]){Regex.Escape(name)}(?![\p{{L}}\p{{N}}'’-])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    internal static int Instance(string text) {
        for (var instance = 1; instance <= 9; instance++)
            if (text.Contains((char)('\uE0B0' + instance))) return instance;
        return 0;
    }
}
#endif
