#if GILLIONS_TEST_BUILD || GILLIONS_HUNT_MAP_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GillionsGameSync;

// RAM-only claim; deliberately no generated ToString containing its token.
internal sealed class HuntMapRequest {
    internal required string RequestId { get; init; }
    internal required string ClaimToken { get; init; }
    internal required string Revision { get; init; }
    internal required string CandidateId { get; init; }
    internal uint TargetId { get; init; }
    internal required string TargetName { get; init; }
    internal uint TerritoryId { get; init; }
    internal uint MapId { get; init; }
    internal float MapX { get; init; }
    internal float MapY { get; init; }
    internal int CandidateIndex { get; init; }
    internal int CandidateCount { get; init; }
    internal DateTime ExpiresAtUtc { get; init; }
    internal required string Classification { get; init; }
    internal uint? FateId { get; init; }
    internal string? FateName { get; init; }
    internal string Guidance => $"{TargetName} - Hunt area/reference location (not a sighting). " + (Classification switch {
        "FATE_REQUIRED" => $"FATE required: {FateName}; activity not currently known.",
        "CONDITIONAL" => "Conditional availability; activity not currently known.",
        "UNKNOWN" => "Availability unknown.",
        _ => "Ordinary reference; live availability unknown.",
    }) + $" Site location {CandidateIndex + 1} of {CandidateCount}; alternative cycling is not supported in v1.";
}

internal static class HuntMapPolicy {
    internal const string Origin = "https://test.gillions.app";
    internal const string Capability = "native_hunt_map_v1";
    internal const string Permission = "server:game-sync:receive:hunt-map:v1";
    internal const string RequestType = "hunt_map";
    internal const int MaximumResponseBytes = 4096;
    internal static bool Admit(bool consent, bool paired, string origin, bool current) => consent && paired && current && origin == Origin;
    private static bool Exact(JsonElement value, params string[] names) {
        if (value.ValueKind != JsonValueKind.Object) return false;
        var keys = new HashSet<string>(names, StringComparer.Ordinal);
        foreach (var p in value.EnumerateObject()) if (!keys.Remove(p.Name)) return false;
        return keys.Count == 0;
    }
    private static bool Hex(string? value, int length) => value is not null && value.Length == length
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool Text(string? value, int limit) => !string.IsNullOrWhiteSpace(value) && value.Length <= limit
        && value == value.Trim() && !value.Any(c => char.IsControl(c)
            || char.GetUnicodeCategory(c) is UnicodeCategory.Format or UnicodeCategory.Surrogate or UnicodeCategory.PrivateUse);
    internal static bool Valid(HuntMapRequest r, DateTime now) => now.Kind == DateTimeKind.Utc
        && Guid.TryParseExact(r.RequestId, "D", out var id) && id != Guid.Empty
        && r.ClaimToken.Length is >= 16 and <= 100 && r.ClaimToken.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
        && Hex(r.Revision, 64) && Hex(r.CandidateId, 24) && r.TargetId > 0 && Text(r.TargetName, 120)
        && r.TerritoryId > 0 && r.MapId > 0 && float.IsFinite(r.MapX) && float.IsFinite(r.MapY)
        && r.MapX is >= 0 and <= 100 && r.MapY is >= 0 and <= 100 && r.CandidateIndex == 0 && r.CandidateCount > 0
        && r.ExpiresAtUtc.Kind == DateTimeKind.Utc && r.ExpiresAtUtc > now && r.ExpiresAtUtc - now <= TimeSpan.FromSeconds(90)
        && r.Classification is "ALWAYS_AVAILABLE" or "FATE_REQUIRED" or "CONDITIONAL" or "UNKNOWN"
        && (r.FateId is null ? r.FateName is null && r.Classification != "FATE_REQUIRED" : r.FateId > 0 && Text(r.FateName, 160))
        && (r.Classification != "ALWAYS_AVAILABLE" || r.FateId is null);
    internal static HuntMapRequest? Parse(string json, DateTime now) {
        if (Encoding.UTF8.GetByteCount(json) > MaximumResponseBytes) return null;
        try {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 5 });
            var root = doc.RootElement;
            // Site's actual v1 acknowledgment is just ok + request, NOT PF's nativeRequests descriptor.
            if (!Exact(root, "ok", "request") || root.GetProperty("ok").ValueKind != JsonValueKind.True) return null;
            var r = root.GetProperty("request");
            if (!Exact(r, "requestType", "requestId", "claimToken", "huntTargetId", "huntTargetName", "availability",
                "territoryId", "mapId", "mapX", "mapY", "candidateId", "revision", "candidateIndex", "candidateCount", "expiresAt")
                || r.GetProperty("requestType").GetString() != RequestType) return null;
            var a = r.GetProperty("availability");
            if (!Exact(a, "classification", "fateId", "fateName", "activity") || a.GetProperty("activity").GetString() != "UNKNOWN") return null;
            var date = r.GetProperty("expiresAt").GetString();
            if (date is null || !date.EndsWith('Z') || !DateTime.TryParseExact(date,
                ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"], CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var expiry)) return null;
            var result = new HuntMapRequest {
                RequestId = r.GetProperty("requestId").GetString() ?? "", ClaimToken = r.GetProperty("claimToken").GetString() ?? "",
                Revision = r.GetProperty("revision").GetString() ?? "", CandidateId = r.GetProperty("candidateId").GetString() ?? "",
                TargetId = r.GetProperty("huntTargetId").GetUInt32(), TargetName = r.GetProperty("huntTargetName").GetString() ?? "",
                TerritoryId = r.GetProperty("territoryId").GetUInt32(), MapId = r.GetProperty("mapId").GetUInt32(),
                MapX = r.GetProperty("mapX").GetSingle(), MapY = r.GetProperty("mapY").GetSingle(),
                CandidateIndex = r.GetProperty("candidateIndex").GetInt32(), CandidateCount = r.GetProperty("candidateCount").GetInt32(),
                ExpiresAtUtc = expiry, Classification = a.GetProperty("classification").GetString() ?? "",
                FateId = a.GetProperty("fateId").ValueKind == JsonValueKind.Null ? null : a.GetProperty("fateId").GetUInt32(),
                FateName = a.GetProperty("fateName").GetString(),
            };
            return Valid(result, now) ? result : null;
        } catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException) { return null; }
    }
    internal static bool Consumed(string json) {
        if (Encoding.UTF8.GetByteCount(json) > MaximumResponseBytes) return false;
        try {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 2 });
            return Exact(doc.RootElement, "ok", "consumed") && doc.RootElement.GetProperty("ok").ValueKind == JsonValueKind.True
                && doc.RootElement.GetProperty("consumed").ValueKind == JsonValueKind.True;
        } catch (JsonException) { return false; }
    }
}

internal static class HuntMapTransport {
    internal const int RequestTimeoutSeconds = 15;
    internal const int BodyTimeoutSeconds = 10;
    internal static CancellationTokenSource Deadline(CancellationToken session, CancellationToken feature, DateTime? expiry = null) {
        var source = CancellationTokenSource.CreateLinkedTokenSource(session, feature);
        var remaining = expiry is null ? TimeSpan.FromSeconds(RequestTimeoutSeconds) : expiry.Value - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero) source.Cancel();
        else source.CancelAfter(remaining < TimeSpan.FromSeconds(RequestTimeoutSeconds) ? remaining : TimeSpan.FromSeconds(RequestTimeoutSeconds));
        return source;
    }
    internal static async Task<string> ReadAsync(HttpContent content, CancellationToken cancellation) {
        // ResponseHeadersRead ends HttpClient.Timeout at headers. Bound actual
        // body reads independently, including chunked/slow/stalled responses.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(BodyTimeoutSeconds));
        var token = deadline.Token;
        if (content.Headers.ContentLength > HuntMapPolicy.MaximumResponseBytes) throw new InvalidOperationException("Hunt response too large.");
        using var stream = await content.ReadAsStreamAsync(token);
        var bytes = new byte[HuntMapPolicy.MaximumResponseBytes + 1];
        var length = 0;
        while (length < bytes.Length) {
            var read = await stream.ReadAsync(bytes.AsMemory(length), token);
            if (read == 0) break;
            length += read;
        }
        if (length > HuntMapPolicy.MaximumResponseBytes) throw new InvalidOperationException("Hunt response too large.");
        return new UTF8Encoding(false, true).GetString(bytes, 0, length);
    }
}

internal sealed class HuntMapProcessor {
    private readonly HashSet<string> attempted = new(StringComparer.Ordinal);
    private readonly Queue<string> order = new();
    internal async Task<string> ProcessAsync(HuntMapRequest? request, Func<DateTime> now, Func<Task<bool>> permitted,
        Func<HuntMapRequest, Task<bool>> consume, Func<HuntMapRequest, Task<bool>> present) {
        if (request is null || !HuntMapPolicy.Valid(request, now())) return "No valid current Hunt request";
        if (!await permitted()) return "Hunt map disabled or stale session";
        if (!attempted.Add(request.RequestId)) return "Hunt request already attempted";
        order.Enqueue(request.RequestId);
        while (order.Count > 128) attempted.Remove(order.Dequeue());
        // Revision is opaque, NOT sortable. Site atomically checks it at consume.
        // Same revision + new authorized request ID is the manual Show fallback.
        // Never retry a lost consume response. Single polling owner serializes presentations.
        if (!await consume(request)) return "Hunt request rejected; no map opened";
        if (!HuntMapPolicy.Valid(request, now()) || !await permitted()) return "Hunt request cancelled after consume";
        return await present(request) ? "Current Hunt reference map shown" : "Map unavailable; use Site Show in FFXIV to retry";
    }
}
#endif
