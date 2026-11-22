#if GILLIONS_TEST_BUILD || GILLIONS_HUNT_MAP_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace GillionsGameSync;

// Claims stay in RAM. Do not generate a ToString that contains the token.
internal sealed class MapFlagRequest {
    internal required string RequestId { get; init; }
    internal required string ClaimToken { get; init; }
    internal DateTime ExpiresAtUtc { get; init; }
    internal uint TerritoryId { get; init; }
    internal uint MapId { get; init; }
    internal float MapX { get; init; }
    internal float MapY { get; init; }
    internal required string Label { get; init; }
    internal required string Source { get; init; }
}

internal static class MapFlagPolicy {
    internal const string PermissionKey = "mapFlags";
    internal const string Permission = "server:game-sync:receive:map-flag:v1";
    internal const string Label = "Map flags from the site";
    internal const string RequestType = "map_flag";
    internal const string Capability = "map_flag_v1";
    internal static bool Valid(MapFlagRequest request, DateTime now) =>
        now.Kind == DateTimeKind.Utc
        && Guid.TryParseExact(request.RequestId, "D", out var id) && id != Guid.Empty
        && request.ClaimToken.Length is >= 16 and <= 100
        && request.ClaimToken.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
        && request.ExpiresAtUtc.Kind == DateTimeKind.Utc && request.ExpiresAtUtc > now
        && request.ExpiresAtUtc - now <= TimeSpan.FromSeconds(90)
        && request.TerritoryId > 0 && request.MapId > 0
        && float.IsFinite(request.MapX) && float.IsFinite(request.MapY)
        && request.MapX is >= 0 and <= 100 && request.MapY is >= 0 and <= 100
        && HuntMapPolicy.Text(request.Label, 120)
        && request.Source is "vendor" or "quest" or "card_match" or "fate";

    internal static MapFlagRequest? Parse(JsonElement root, DateTime now) {
        try {
            if (!HuntMapPolicy.Exact(root, "ok", "request") && !HuntMapPolicy.Exact(root, "ok", "request", "nativeRequests"))
                return null;
            if (root.GetProperty("ok").ValueKind != JsonValueKind.True) return null;
            if (root.TryGetProperty("nativeRequests", out var ack)
                && (!HuntMapPolicy.Exact(ack, "contract", "contractVersion", "acceptedClientProduct", "capabilities")
                    || ack.GetProperty("contract").GetString() != "native-requests-v1"
                    || ack.GetProperty("contractVersion").GetInt32() != 1
                    || ack.GetProperty("acceptedClientProduct").GetString() != NativeProduct.Name
                    || !ack.GetProperty("capabilities").EnumerateArray().Any(c => c.GetString() == Capability))) return null;
            var row = root.GetProperty("request");
            if (!HuntMapPolicy.Exact(row, "requestType", "requestId", "claimToken", "expiresAt",
                "territoryId", "mapId", "mapX", "mapY", "label", "source")
                || row.GetProperty("requestType").GetString() != RequestType
                || !HuntMapPolicy.Utc(row.GetProperty("expiresAt").GetString(), out var expiry)) return null;
            var x = row.GetProperty("mapX").GetDouble();
            var y = row.GetProperty("mapY").GetDouble();
            if (!double.IsFinite(x) || !double.IsFinite(y) || x is < 0 or > 100 || y is < 0 or > 100) return null;
            var request = new MapFlagRequest {
                RequestId = row.GetProperty("requestId").GetString() ?? "",
                ClaimToken = row.GetProperty("claimToken").GetString() ?? "",
                ExpiresAtUtc = expiry,
                TerritoryId = row.GetProperty("territoryId").GetUInt32(),
                MapId = row.GetProperty("mapId").GetUInt32(),
                MapX = (float)x,
                MapY = (float)y,
                Label = row.GetProperty("label").GetString() ?? "",
                Source = row.GetProperty("source").GetString() ?? "",
            };
            return Valid(request, now) ? request : null;
        } catch (Exception error) when (error is JsonException or InvalidOperationException
            or KeyNotFoundException or FormatException or OverflowException) { return null; }
    }

    internal static bool CatalogValid(uint territoryId, bool territoryExists, bool mapExists, int sizeFactor, uint mapTerritoryId) =>
        territoryExists && mapExists && sizeFactor > 0 && mapTerritoryId == territoryId;

    internal static bool Ready(bool loggedIn, bool betweenAreas, bool betweenAreas51) =>
        loggedIn && !betweenAreas && !betweenAreas51;

    internal static object PollBody(string version, bool itemLinks, bool partyFinder, bool mapFlags) {
        var body = new Dictionary<string, object> { ["capability"] = "native_item_link", ["pluginVersion"] = version };
        var capabilities = new List<string>();
        if (partyFinder) capabilities.Add("native_party_finder_link_v1");
        if (mapFlags) {
            capabilities.Add(Capability);
            var types = new List<string>();
            if (itemLinks) types.Add("item");
            if (partyFinder) types.Add("party_finder");
            types.Add(RequestType);
            body["requestTypes"] = types;
        }
        if (capabilities.Count > 0)
            body["nativeRequests"] = new { contract = "native-requests-v1", contractVersion = 1, capabilities };
        return body;
    }

    internal static object ConsumeBody(MapFlagRequest request) =>
        new { requestType = RequestType, requestId = request.RequestId, claimToken = request.ClaimToken };
}

internal static class MapFlagProcessor {
    internal static async Task<string> ProcessAsync(MapFlagRequest? request, HuntMapProcessor attempts, Func<DateTime> now,
        Func<Task<bool>> permitted, Func<MapFlagRequest, Task<bool>> consume, Func<MapFlagRequest, Task<bool>> present) {
        if (request is null || !MapFlagPolicy.Valid(request, now())) return "Invalid or expired map flag";
        if (!await permitted()) return "Map flags disabled or character unavailable";
        if (!attempts.TryBegin(request.RequestId)) return "Map flag already attempted";
        if (!await consume(request)) return "Map flag rejected";
        if (!MapFlagPolicy.Valid(request, now()) || !await permitted()) return "Map flag cancelled after consume";
        return await present(request) ? "Map flag shown" : "Map unavailable";
    }
}
#endif
