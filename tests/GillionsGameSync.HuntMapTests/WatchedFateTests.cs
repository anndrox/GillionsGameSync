using System.Net;
using GillionsGameSync;

internal static class WatchedFateTests {
    internal static void Run(Action<bool, string> check, DateTime now) {
        string revision = new('a', 64), id = new('b', 64);
        var valid = $$"""
            {"schemaVersion":1,"revision":"{{revision}}","watchedFateIds":[490],"active":[{
            "occurrenceId":"{{id}}","fateId":490,"name":"The Bigger They Are","territoryId":155,
            "mapId":53,"zoneName":"Coerthas Central Highlands","world":"Balmung","dataCentre":"Crystal",
            "state":"running","endsAtUtc":"{{now.AddMinutes(2):O}}","expiresAtUtc":"{{now.AddSeconds(20):O}}",
            "location":{"mapX":13.48,"mapY":26.28} }]}
            """;
        var snapshot = WatchedFateSnapshot.Parse(valid);
        check(snapshot is { Active.Length: 1 } && snapshot.Active[0].Fate.Location is not null, "PR #722 occurrence shape");
        var nullable = valid.Replace("\"mapId\":53", "\"mapId\":null")
            .Replace($"\"endsAtUtc\":\"{now.AddMinutes(2):O}\"", "\"endsAtUtc\":null")
            .Replace("{\"mapX\":13.48,\"mapY\":26.28}", "null");
        var row = WatchedFateSnapshot.Parse(nullable)?.Active[0];
        check(row is { EndsAt: null, Fate.Location: null, Fate.MapId: 0 }, "Unknown timer and point stay nullable");
        check(WatchedFateSnapshot.Parse(valid.Replace("[490]", "[491]")) is null, "Reject unwatched occurrence");
        check(WatchedFateSnapshot.Parse(valid.Replace("13.48", "99")) is null, "Reject map point outside the map");
        check(WatchedFateSnapshot.Parse(valid + new string(' ', 16384)) is null, "Reject oversized response");
        check(WatchedFateSnapshot.Parse(valid.Replace("[490]", "[" + string.Join(",", Enumerable.Range(490, 65)) + "]")) is null, "Reject more than 64 watches");
        var poll = new WatchedFatePoll();
        var clock = WebsiteResponseClock.Capture(null, now, TimeSpan.Zero);
        poll.Complete(new(HttpStatusCode.OK, snapshot, "\"tag\"", clock), 0, 1250);
        check(!poll.Due(31249) && poll.Due(31250), "30 second poll with jitter");
        check(poll.IsWatched(490, 10) && poll.Active(10).Single().Id == id, "Active watch feeds alert and marker");
        poll.Complete(new(HttpStatusCode.NotModified, ETag: "\"tag\""), 31000, 0);
        check(poll.ETag(100000) == "\"tag\"" && poll.IsWatched(490, 100000), "304 renews watch list and validator");
        check(!poll.IsWatched(490, 106000) && poll.ETag(106000) is null, "Expired watch cache cannot request a 304");
        poll.Complete(new(HttpStatusCode.Unauthorized), 110000, 0);
        poll.ClearSession();
        check(poll.Blocked && !poll.Due(long.MaxValue) && !poll.IsWatched(490, 110000), "Revocation survives character reset");
        poll.NewPairing();
        check(poll.Due(0), "New pairing can poll");
        poll.Complete(new(HttpStatusCode.NotFound), 0, 0);
        check(poll.LegacyFallback && !poll.Due(59999) && poll.Due(60000), "Legacy fallback only for absent route with 60 second retry");
        poll.Complete(new(HttpStatusCode.ServiceUnavailable), 60000, 0);
        check(!poll.Due(179999) && poll.Due(180000), "Second failure doubles retry");
        poll.Complete(new(HttpStatusCode.ServiceUnavailable, RetryAfter: 250), 180000, 0);
        check(!poll.Due(429999) && poll.Due(430000), "Retry-After extends delay");
        poll.Complete(new(HttpStatusCode.ServiceUnavailable), 430000, 0);
        check(!poll.Due(729999) && poll.Due(730000), "Exponential delay caps at five minutes");
        poll.Complete(new(HttpStatusCode.OK, snapshot, "\"tag\"", WebsiteResponseClock.Capture(null, now.AddMinutes(1), TimeSpan.Zero)), 730000, 0);
        check(!poll.LegacyFallback && !poll.Active(730000).Any(), "Successful endpoint supersedes presence and ignores expired rows");
        poll.Complete(new(HttpStatusCode.NotFound), 760000, 0);
        check(!poll.LegacyFallback, "Do not resume legacy delivery after the endpoint was accepted");

        var alerts = new FateAlertOccurrences();
        alerts.RememberLegacy(490, "Balmung", now);
        check(!alerts.ObserveSite(id, 490, "Balmung", now.AddSeconds(5)), "Legacy-to-Site handoff suppresses duplicate alert");
        check(!alerts.ObserveSite(id, 490, "Balmung", now.AddSeconds(10)), "Same ID cannot alert twice");
        check(alerts.ObserveSite(revision, 490, "Famfrit", now.AddSeconds(10)), "Different occurrence can alert");
        alerts.ClearLegacy();
        check(!alerts.ObserveSite(revision, 490, "Famfrit", now.AddSeconds(15)), "World changes keep Site dedupe");
        alerts.Clear();
        check(alerts.ObserveSite(revision, 490, "Famfrit", now.AddSeconds(20)), "Session reset clears dedupe");
    }
}
