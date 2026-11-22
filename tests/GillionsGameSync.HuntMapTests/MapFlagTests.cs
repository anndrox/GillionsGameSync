using GillionsGameSync;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class MapFlagTests {
    internal static async Task Run(Action<bool, string> check, DateTime now) {
        JsonObject Fixture(string? id = null) => new() {
            ["ok"] = true,
            ["request"] = new JsonObject {
                ["requestType"] = "map_flag", ["requestId"] = id ?? Guid.NewGuid().ToString("D"),
                ["claimToken"] = "synthetic_claim_token_123", ["expiresAt"] = now.AddSeconds(90).ToString("O"),
                ["territoryId"] = 140, ["mapId"] = 20, ["mapX"] = 14.41, ["mapY"] = 6.84,
                ["label"] = "A place in another zone", ["source"] = "vendor",
            },
        };
        MapFlagRequest? Parse(JsonObject value) => MapFlagPolicy.Parse(JsonSerializer.SerializeToElement(value), now);
        var fixture = Fixture();
        var flag = Parse(fixture)!;
        check(flag is { MapId: 20, MapX: 14.41f, MapY: 6.84f }, "Map flag accepts human map coordinates");
        check(typeof(MapFlagRequest).GetMethod("ToString")!.DeclaringType == typeof(object), "Map flag claim is absent from ToString");
        foreach (var name in fixture["request"]!.AsObject().Select(p => p.Key)) {
            var missing = Fixture(); missing["request"]!.AsObject().Remove(name);
            check(Parse(missing) is null, "Map flag rejects missing " + name);
        }
        foreach (var name in fixture["request"]!.AsObject().Select(p => p.Key)) {
            var wrong = Fixture(); wrong["request"]![name] = new JsonArray();
            check(Parse(wrong) is null, "Map flag rejects wrong type " + name);
        }
        foreach (var name in new[] { "huntTargetId", "revision", "url", "extra" }) {
            var extra = Fixture(); extra["request"]![name] = 1;
            check(Parse(extra) is null, "Map flag rejects additional " + name);
        }
        foreach (var source in new[] { "vendor", "quest", "card_match", "fate" }) {
            var value = Fixture(); value["request"]!["source"] = source;
            check(Parse(value) is not null, "Map flag source " + source);
        }
        foreach (var source in new[] { "hunt", "Vendor", "card", "", "quest " }) {
            var value = Fixture(); value["request"]!["source"] = source;
            check(Parse(value) is null, "Map flag rejects unsupported source " + source);
        }
        foreach (var label in new[] { "", " ", " leading", "trailing ", "bad\nlabel", "\u202eformat", "\ue000icon", new string('x', 121) }) {
            var value = Fixture(); value["request"]!["label"] = label;
            check(Parse(value) is null, "Map flag uses Hunt plain-text rules");
        }
        var limit = Fixture(); limit["request"]!["label"] = new string('x', 120);
        check(Parse(limit) is not null, "Map flag permits 120 characters");
        foreach (var field in new[] { "mapX", "mapY" }) {
            foreach (var coordinate in new[] { -0.01, 100.01, 100.00000001, -1e-100, double.MaxValue }) {
                var value = Fixture(); value["request"]![field] = coordinate;
                check(Parse(value) is null, "Map flag rejects invalid coordinate");
            }
            foreach (var coordinate in new[] { 0, 100 }) {
                var value = Fixture(); value["request"]![field] = coordinate;
                check(Parse(value) is not null, "Map flag accepts coordinate boundary");
            }
        }
        foreach (var invalid in new Action<JsonObject>[] {
            r => r["requestType"] = "hunt_map", r => r["territoryId"] = 0, r => r["mapId"] = -1,
            r => r["requestId"] = Guid.Empty.ToString("D"), r => r["claimToken"] = "too_short",
            r => r["claimToken"] = new string('a', 101), r => r["claimToken"] = "invalid token with spaces",
            r => r["expiresAt"] = now.ToString("O"), r => r["expiresAt"] = now.AddSeconds(90.001).ToString("O"),
            r => r["expiresAt"] = now.AddSeconds(30).ToString("O").Replace("Z", "+00:00"),
        }) {
            var value = Fixture(); invalid(value["request"]!.AsObject());
            check(Parse(value) is null, "Map flag rejects invalid identity, expiry or coordinates");
        }
        var duplicateJson = fixture.ToJsonString().Replace("\"mapId\":20", "\"mapId\":20,\"mapId\":20");
        using (var duplicate = JsonDocument.Parse(duplicateJson))
            check(MapFlagPolicy.Parse(duplicate.RootElement, now) is null, "Map flag rejects duplicate fields");
        check(MapFlagPolicy.Parse(JsonSerializer.SerializeToElement(new { ok = true, request = (object?)null }), now) is null,
            "Empty response has no map action");
        check(MapFlagPolicy.Ready(true, false, false)
            && !MapFlagPolicy.Ready(false, false, false)
            && !MapFlagPolicy.Ready(true, true, false)
            && !MapFlagPolicy.Ready(true, false, true), "Map flag waits for login and both zone-transition flags");
        check(MapFlagPolicy.CatalogValid(140, true, true, 100, 140), "Map and territory agree");
        check(!MapFlagPolicy.CatalogValid(140, false, true, 100, 140)
            && !MapFlagPolicy.CatalogValid(140, true, false, 100, 140)
            && !MapFlagPolicy.CatalogValid(140, true, true, 0, 140)
            && !MapFlagPolicy.CatalogValid(140, true, true, 100, 141), "Catalog mismatch prevents consume");

        foreach (var pf in new[] { false, true }) {
            var poll = JsonSerializer.SerializeToElement(MapFlagPolicy.PollBody("0.0.114.0", true, pf, true));
            check(poll.GetProperty("capability").GetString() == "native_item_link"
                && poll.GetProperty("requestTypes").EnumerateArray().Any(x => x.GetString() == "map_flag")
                && poll.GetProperty("nativeRequests").GetProperty("capabilities").EnumerateArray().Any(x => x.GetString() == "map_flag_v1"),
                "Map flag advertises on the existing item-link poll with or without PF");
        }
        var off = JsonSerializer.SerializeToElement(MapFlagPolicy.PollBody("0.0.114.0", true, false, false));
        check(off.EnumerateObject().Count() == 2 && !off.TryGetProperty("nativeRequests", out _),
            "Map flags Off retains the existing item-only poll");
        var mapOnly = JsonSerializer.SerializeToElement(MapFlagPolicy.PollBody("0.0.114.0", false, false, true));
        check(mapOnly.GetProperty("requestTypes").EnumerateArray().Select(x => x.GetString()).SequenceEqual(["map_flag"]),
            "Map flags work independently of item and PF permission");
        var consume = JsonSerializer.SerializeToElement(MapFlagPolicy.ConsumeBody(flag));
        check(consume.EnumerateObject().Select(x => x.Name).Order().SequenceEqual(new[] { "requestType", "requestId", "claimToken" }.Order()),
            "Consume contains exactly requestType, requestId and claimToken");
        var sequence = new List<string>();
        var attempts = new HuntMapProcessor();
        bool permitted = true, catalog = true, acknowledged = true, opened = true;
        int consumes = 0, maps = 0, lines = 0;
        DateTime current = now;
        Task<bool> Allowed() => Task.FromResult(permitted);
        Task<bool> Consume(MapFlagRequest request) {
            sequence.Add("catalog");
            if (!catalog) return Task.FromResult(false);
            consumes++; sequence.Add("consume");
            return Task.FromResult(acknowledged);
        }
        Task<bool> Present(MapFlagRequest request) {
            maps++; sequence.Add("map");
            if (!opened) return Task.FromResult(false);
            lines++; sequence.Add("chat");
            return Task.FromResult(true);
        }
        Task<string> Run(MapFlagRequest request) =>
            MapFlagProcessor.ProcessAsync(request, attempts, () => current, Allowed, Consume, Present);
        check(await Run(flag) == "Map flag shown" && sequence.SequenceEqual(["catalog", "consume", "map", "chat"]),
            "Another-zone map validates catalog then consumes before one map and chat");
        check((await Run(flag)).Contains("already attempted") && consumes == 1 && maps == 1 && lines == 1,
            "Repeated map flag has no side effect");
        var shared = Parse(Fixture())!;
        check(attempts.TryBegin(shared.RequestId) && (await Run(shared)).Contains("already attempted"),
            "Hunt and map flags share the same ID memory");
        catalog = false;
        check(await Run(Parse(Fixture())!) == "Map flag rejected" && consumes == 1 && maps == 1,
            "Missing catalog prevents network consume and map");
        catalog = true; acknowledged = false;
        var rejected = Parse(Fixture())!;
        check(await Run(rejected) == "Map flag rejected" && maps == 1, "Rejected consume opens no map");
        acknowledged = true;
        check((await Run(rejected)).Contains("already attempted"), "Rejected consume is tried once");
        var uncertain = Parse(Fixture())!;
        try {
            await MapFlagProcessor.ProcessAsync(uncertain, attempts, () => now, Allowed,
                _ => throw new IOException("Lost response"), Present);
            check(false, "Lost consume must propagate");
        } catch (IOException) { }
        check((await Run(uncertain)).Contains("already attempted"), "Lost response never replays a consume");
        var inFlight = new TaskCompletionSource<bool>();
        var concurrent = Parse(Fixture())!;
        var first = MapFlagProcessor.ProcessAsync(concurrent, attempts, () => current, Allowed, _ => inFlight.Task, Present);
        check((await Run(concurrent)).Contains("already attempted"), "Concurrent duplicate reserved before await");
        permitted = false; inFlight.SetResult(true);
        check(await first == "Map flag cancelled after consume" && maps == 1, "Off during consume prevents presentation");
        permitted = true;
        var expiry = Parse(Fixture())!;
        check(await MapFlagProcessor.ProcessAsync(expiry, attempts, () => current, Allowed, _ => {
            current = now.AddSeconds(91); return Task.FromResult(true);
        }, Present) == "Map flag cancelled after consume" && maps == 1, "Expired consume cannot present");
        current = now; opened = false;
        check(await Run(Parse(Fixture())!) == "Map unavailable" && lines == 1, "Failed OpenMap produces no success chat");
        for (var i = 0; i < 130; i++) attempts.TryBegin(Guid.NewGuid().ToString("D"));
        check(attempts.TryBegin(flag.RequestId), "Map and Hunt attempts share bounded 128-ID eviction");
        check(!attempts.TryBegin(flag.RequestId), "The same request ID is already reserved");
        check(attempts.TryBegin(Guid.NewGuid().ToString("D")), "A different request ID is newly reserved");
        Console.WriteLine("Map flag contract and delivery fixtures passed.");
    }
}
