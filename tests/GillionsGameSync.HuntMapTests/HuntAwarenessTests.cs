using System.Text.Json;
using System.Text.Json.Nodes;
using GillionsGameSync;

internal static class HuntAwarenessTests {
    internal static void Run(Action<bool, string> check, DateTime now, HuntMapRequest fate, HuntMapRequest bRank,
        HuntMapRequest nextCandidate, JsonObject nextResponse) {
        var state = new HuntDeferState();
        check(state.Plan(fate, HuntAvailability.NotSeen, now) is null, "Unconsumed target cannot defer");
        state.Consumed(fate);
        check(state.Plan(bRank, HuntAvailability.NotSeen, now) is null, "Only the current consumed target can defer");
        check(state.Plan(fate, HuntAvailability.Unknown, now) is null, "Unknown is not negative evidence");
        var action = state.Plan(fate, HuntAvailability.NotSeen, now)!;
        CheckBody(check, action, fate, "defer", "FATE_NOT_UP");
        var empty = Progression(now.AddMinutes(60));
        check(HuntMapPolicy.TryDeferPoll(empty.ToJsonString(), now, action, out var none, out var end)
            && none is null && end == now.AddMinutes(30), "FATE cap and null next command");
        check(!HuntMapPolicy.TryPoll(empty.ToJsonString(), now, HuntMapPolicy.CapabilityV2, out _), "Normal poll rejects mutation progression");
        state.Sent(action, now);
        state.Acknowledge(action, end);
        check(state.Current is null && state.Plan(fate, HuntAvailability.Up, now.AddMinutes(1)) is null,
            "Accepted defer retires the old claim, even when all targets are waiting");

        // Fixtures use the same target row with distinct command identities, as a newer Site claim would.
        state.Consumed(bRank);
        check(state.Plan(bRank, HuntAvailability.Up, now.AddSeconds(59)) is null, "One shared 60-second limit for defer and clear");
        var clear = state.Plan(bRank, HuntAvailability.Up, now.AddMinutes(1))!;
        CheckBody(check, clear, bRank, "clear-defer", null);
        const string cleared = "{\"ok\":true,\"request\":null,\"progression\":{\"deferCleared\":true}}";
        check(HuntMapPolicy.TryDeferPoll(cleared, now, clear, out none, out end) && none is null && end is null,
            "Valid clear with null request");
        check(!HuntMapPolicy.TryDeferPoll(cleared, now, action, out _, out _), "A clear receipt cannot acknowledge a defer");
        state.Sent(clear, now.AddMinutes(1));
        state.Acknowledge(clear, null);
        state.Consumed(nextCandidate);
        check(state.Plan(nextCandidate, HuntAvailability.NotSeen, now.AddSeconds(119)) is null, "Throttle survives clear and candidate changes");
        check(state.Plan(nextCandidate, HuntAvailability.Up, now.AddMinutes(2)) is null, "Clear removes the local defer");
        var markDefer = state.Plan(nextCandidate, HuntAvailability.NotSeen, now.AddMinutes(2))!;
        CheckBody(check, markDefer, nextCandidate, "defer", "B_RANK_NOT_SEEN");
        var progression = Progression(now.AddMinutes(10));
        nextResponse["progression"] = progression["progression"]!.DeepClone();
        check(HuntMapPolicy.TryDeferPoll(nextResponse.ToJsonString(), now, markDefer, out var next, out end)
            && next is not null && end == now.AddMinutes(5), "B-rank cap and existing V2 next claim");
        state.Acknowledge(markDefer, end);
        state.Consumed(bRank);
        check(state.Plan(bRank, HuntAvailability.Up, now.AddMinutes(5)) is null, "Expired defer does not send a clear");
        state.Reset();
        check(state.Plan(bRank, HuntAvailability.NotSeen, now) is null, "Reset invalidates the current command");

        var exact = new object();
        var sonar = new object();
        check(BRankSighting.Resolve(true, exact, sonar, true) == (HuntAvailability.Up, exact), "Object-table coordinates take priority");
        check(BRankSighting.Resolve(false, null, sonar, true) == (HuntAvailability.Up, sonar), "Sonar coordinates can supply the exact flag");
        check(BRankSighting.Resolve(false, null, (object?)null, false) == (HuntAvailability.Unknown, null), "Outside load range keeps the spawn-area flag");
        check(BRankSighting.Resolve(false, null, (object?)null, true) == (HuntAvailability.NotSeen, null), "A missing mark near its spawn can defer");

        var revision = new string('d', 64);
        TrackedFates? Tracked(string running, TrackedFates? old = null) {
            using var document = JsonDocument.Parse($$"""
                {"schemaVersion":1,"revision":"{{revision}}","items":[{"fateId":366,"name":"He's Got Legs","territoryId":140,"mapId":20,"zoneName":"Western Thanalan"}],"runningOnYourWorld":[{{running}}]}
                """);
            return TrackedFates.Parse(document.RootElement, old);
        }
        var tracked = Tracked("366")!;
        check(tracked.Items.Count == 1 && tracked.Running.SetEquals([366u]), "Exact personal tracked list and running ids");
        check(Tracked("1", tracked) is null, "Reject a running id outside the tracked list");
        check(Tracked("", tracked)!.Running.Count == 0, "Refresh running ids even when the catalogue revision is unchanged");

        var occurrences = new FateAlertOccurrences();
        check(!occurrences.Observe(366, true, 0, false), "Alerts off suppresses the notification");
        check(occurrences.Observe(366, true, 0, true), "First preparation, site sighting or Sonar relay alerts");
        check(!occurrences.Observe(366, true, 10, true), "FATE running confirms the same start without repeating");
        check(!occurrences.Observe(366, true, 0, true), "Additional sources do not repeat the alert");
        check(occurrences.Observe(366, true, 20, true), "Distinct local start alerts even if a site snapshot overlaps");
        check(!occurrences.Observe(366, false, 0, true) && occurrences.Observe(366, true, 0, true), "An ended FATE can start again");
        occurrences.Retain(Tracked(""));
        check(!occurrences.Observe(366, true, 0, true), "Refreshing the tracked list preserves occurrence deduplication");

        check(SonarChatRelay.IsSender("Sonar") && !SonarChatRelay.IsSender("Sonar Player"), "Only the exact Sonar sender");
        check(SonarChatRelay.Matches("<Maduin> Rank B: Bloody Mary", "Maduin", "Bloody Mary", true), "Matching Sonar B-rank relay");
        check(SonarChatRelay.Matches("<Maduin> FATE: He's Got Legs", "Maduin", "He's Got Legs", false), "Matching Sonar FATE relay");
        check(!SonarChatRelay.Matches("<Maduin> Rank B: Bloody Mary killed", "Maduin", "Bloody Mary", true)
            && !SonarChatRelay.Matches("<Marilith> Rank B: Bloody Mary", "Maduin", "Bloody Mary", true)
            && !SonarChatRelay.Matches("<Maduin> Rank B: Bloody Marys", "Maduin", "Bloody Mary", true)
            && !SonarChatRelay.Matches("New relay format", "Maduin", "Bloody Mary", true), "Ignore malformed, wrong world, other target and kill messages");
    }

    private static JsonObject Progression(DateTime until) => new() {
        ["ok"] = true, ["request"] = null,
        ["progression"] = new JsonObject { ["deferred"] = true, ["until"] = until.ToString("O") },
    };

    private static void CheckBody(Action<bool, string> check, HuntDeferAction action, HuntMapRequest claim, string name, string? reason) {
        var body = JsonSerializer.SerializeToElement(action.Body());
        string[] keys = ["capability", "action", "requestId", "claimToken", "revision", "candidateId"];
        check(HuntMapPolicy.Exact(body, reason is null ? keys : [.. keys, "reason"]), "Defer wire keys exclude target, location, sighting and Sonar data");
        check(body.GetProperty("capability").GetString() == "native_hunt_map_v2" && body.GetProperty("action").GetString() == name
            && body.GetProperty("requestId").GetString() == claim.RequestId && body.GetProperty("claimToken").GetString() == claim.ClaimToken
            && body.GetProperty("revision").GetString() == claim.Revision && body.GetProperty("candidateId").GetString() == claim.CandidateId
            && (reason is null || body.GetProperty("reason").GetString() == reason), "Mutation uses the exact latest consumed claim");
    }
}
