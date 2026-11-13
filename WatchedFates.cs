#if GILLIONS_TEST_BUILD || GILLIONS_HUNT_MAP_TESTS
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;

namespace GillionsGameSync;

internal sealed record WatchedFateOccurrence(string Id, TrackedFate Fate, string World, string DataCentre,
    DateTime? EndsAt, DateTime ExpiresAt);

internal sealed record WatchedFateSnapshot(HashSet<uint> Ids, WatchedFateOccurrence[] Active) {
    internal const int MaximumBytes = 16 * 1024;

    internal static WatchedFateSnapshot? Parse(string json) {
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) return null;
        try {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            if (!HuntMapPolicy.Exact(root, "schemaVersion", "revision", "watchedFateIds", "active")
                || root.GetProperty("schemaVersion").GetInt32() != 1
                || !HuntMapPolicy.Hex(root.GetProperty("revision").GetString(), 64)) return null;
            var ids = root.GetProperty("watchedFateIds");
            var active = root.GetProperty("active");
            if (ids.GetArrayLength() > 64 || active.GetArrayLength() > 32) return null;
            var watched = new HashSet<uint>();
            foreach (var value in ids.EnumerateArray())
                if (!watched.Add(value.GetUInt16()) || value.GetUInt16() == 0) return null;

            var occurrences = new List<WatchedFateOccurrence>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in active.EnumerateArray()) {
                if (!HuntMapPolicy.Exact(row, "occurrenceId", "fateId", "name", "territoryId", "mapId", "zoneName",
                    "world", "dataCentre", "state", "endsAtUtc", "expiresAtUtc", "location")) return null;
                var id = row.GetProperty("occurrenceId").GetString();
                var fate = row.GetProperty("fateId").GetUInt16();
                var name = row.GetProperty("name").GetString();
                var territory = row.GetProperty("territoryId").GetUInt32();
                var map = row.GetProperty("mapId");
                var mapId = map.ValueKind == JsonValueKind.Null ? 0 : map.GetUInt32();
                var zone = row.GetProperty("zoneName").GetString();
                var world = row.GetProperty("world").GetString();
                var centre = row.GetProperty("dataCentre").GetString();
                if (!HuntMapPolicy.Hex(id, 64) || !seen.Add(id!) || !watched.Contains(fate) || territory == 0
                    || !HuntMapPolicy.Text(name, 256) || (zone is not null && !HuntMapPolicy.Text(zone, 160))
                    || !HuntMapPolicy.Text(world, 80) || !HuntMapPolicy.Text(centre, 80)
                    || row.GetProperty("state").GetString() is not ("preparing" or "running" or "ending")) return null;
                var ends = row.GetProperty("endsAtUtc");
                var end = ends.ValueKind == JsonValueKind.Null ? (DateTime?)null : Utc(ends);
                var expires = Utc(row.GetProperty("expiresAtUtc"));
                var location = row.GetProperty("location");
                FateMapPoint? point = null;
                if (location.ValueKind != JsonValueKind.Null) {
                    if (mapId == 0 || !HuntMapPolicy.Exact(location, "mapX", "mapY")) return null;
                    var x = location.GetProperty("mapX").GetSingle();
                    var y = location.GetProperty("mapY").GetSingle();
                    if (!(x is >= 1 and <= 42 && y is >= 1 and <= 42)) return null;
                    point = new(x, y);
                }
                occurrences.Add(new(id!, new(fate, name!, territory, mapId, zone ?? "", point), world!, centre!, end, expires));
            }
            return new(watched, occurrences.ToArray());
        } catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or OverflowException) {
            return null;
        }
    }

    private static DateTime Utc(JsonElement value) {
        if (value.GetString() is not { } text || !text.EndsWith('Z')) throw new FormatException();
        return value.GetDateTime();
    }
}

internal sealed record WatchedFateReply(HttpStatusCode Status, WatchedFateSnapshot? Snapshot = null,
    string? ETag = null, WebsiteResponseClock? Clock = null, double? RetryAfter = null);

internal sealed class WatchedFatePoll {
    private sealed record Cache(WatchedFateSnapshot Snapshot, string? ETag, WebsiteResponseClock Clock, long Until);
    private volatile Cache? cache;
    private long next;
    private int failures;
    private bool received;
    internal bool LegacyFallback { get; private set; }
    internal bool Blocked { get; private set; }

    internal bool Due(long now) => !Blocked && now >= next;
    private Cache? Fresh(long now) => cache is { } value && now < value.Until ? value : null;
    internal string? ETag(long now) => Fresh(now)?.ETag;
    internal bool IsWatched(uint id, long now) => Fresh(now)?.Snapshot.Ids.Contains(id) == true;
    internal IEnumerable<WatchedFateOccurrence> Active(long now) {
        if (Fresh(now) is not { } value) yield break;
        var utc = value.Clock.UtcNow;
        foreach (var occurrence in value.Snapshot.Active)
            if (occurrence.ExpiresAt > utc && occurrence.ExpiresAt <= utc.AddSeconds(35)) yield return occurrence;
    }

    internal void Complete(WatchedFateReply reply, long now, int jitterMs) {
        if (reply.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) {
            ClearSession();
            Blocked = true;
            return;
        }
        if (reply.Status == HttpStatusCode.OK && reply.Snapshot is { } snapshot && reply.Clock is { } clock) {
            cache = new(snapshot, reply.ETag, clock, now + 75_000);
        } else if (reply.Status == HttpStatusCode.NotModified && cache is { ETag: not null } prior
            && reply.ETag == prior.ETag) {
            // Revalidation renews the watch list, not the sighting's expiry or its issuer clock.
            cache = prior with { Until = now + 75_000 };
        } else {
            if (!received && reply.Status is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed) LegacyFallback = true;
            int seconds = Math.Min(300, 60 << Math.Min(failures, 3));
            failures = Math.Min(failures + 1, 4);
            if (reply.RetryAfter is { } delay && double.IsFinite(delay))
                seconds = Math.Max(seconds, (int)Math.Clamp(Math.Ceiling(delay), 0, int.MaxValue));
            next = now + seconds * 1000L;
            return;
        }
        received = true;
        LegacyFallback = false;
        failures = 0;
        next = now + 30_000 + Math.Clamp(jitterMs, -2000, 2000);
    }

    internal void ClearSession() {
        cache = null;
        LegacyFallback = false;
        received = false;
        failures = 0;
        next = 0;
    }

    internal void NewPairing() {
        ClearSession();
        Blocked = false;
    }
}
#endif
