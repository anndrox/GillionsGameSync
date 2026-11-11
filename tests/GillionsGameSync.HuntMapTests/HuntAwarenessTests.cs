using System.Text.Json;
using System.Text.Json.Nodes;
using GillionsGameSync;

internal static class HuntAwarenessTests {
    internal static void Run(Action<bool, string> check, DateTime now, HuntMapRequest fate, HuntMapRequest firstOfThree,
        HuntMapRequest bRank, HuntMapRequest nextCandidate, HuntMapRequest ordinary, JsonObject nextResponse) {
        const uint world = 90;
        var state = new HuntDeferState();
        check(state.Plan(fate, HuntAvailability.NotSeen, world, now) is null, "Unconsumed target cannot defer");
        state.Consumed(fate);
        check(state.Plan(bRank, HuntAvailability.NotSeen, world, now) is null, "Only the current consumed target can defer");
        check(state.Plan(fate, HuntAvailability.Unknown, world, now) is null, "Unknown is not negative evidence");
        var action = state.Plan(fate, HuntAvailability.NotSeen, world, now)!;
        CheckBody(check, action, fate, "defer", "FATE_NOT_UP");
        var empty = Progression(now.AddMinutes(60));
        check(HuntMapPolicy.TryDeferPoll(empty.ToJsonString(), now, action, out var none, out var end)
            && none is null && end == now.AddMinutes(30), "FATE cap and null next command");
        check(!HuntMapPolicy.TryPoll(empty.ToJsonString(), now, HuntMapPolicy.CapabilityV2, out _), "Normal poll rejects mutation progression");
        state.Sent(action, now);
        state.Acknowledge(action, world, end);
        check(ReferenceEquals(state.Current, fate) && !state.CanDefer && state.Plan(fate, HuntAvailability.NotSeen, world, now.AddMinutes(1)) is null,
            "All waiting retires presentation and defer but preserves the latest consumed identity");
        check(state.Waiting(world + 1, now).Length == 0 && state.Waiting(world, now).Single().FateId == fate.FateId,
            "Deferred observations are scoped to the world where the target was deferred");

        state.Consumed(firstOfThree);
        check(state.Plan(firstOfThree, HuntAvailability.NotSeen, world, now.AddSeconds(5)) is null,
            "B rank at candidate index 0 of 3 sends no defer");
        state.Consumed(bRank);
        var inFlight = BRankSighting.Resolve(false, null, (object?)null, nearSpawn: true, inFlight: true);
        check(inFlight.Availability == HuntAvailability.Unknown && state.Plan(bRank, inFlight.Availability, world, now.AddSeconds(5)) is null,
            "Flying above the last B-rank spawn sends no defer even at the same map XY");
        var markDefer = state.Plan(bRank, HuntAvailability.NotSeen, world, now.AddSeconds(5))!;
        check(markDefer is { Reason: "B_RANK_NOT_SEEN" }, "B rank at candidate index 2 of 3 can defer");
        CheckBody(check, markDefer, bRank, "defer", "B_RANK_NOT_SEEN");
        var progression = Progression(now.AddMinutes(10));
        nextResponse["progression"] = progression["progression"]!.DeepClone();
        check(HuntMapPolicy.TryDeferPoll(nextResponse.ToJsonString(), now, markDefer, out var next, out var markEnd)
            && next is not null && markEnd == now.AddMinutes(5), "B-rank cap and existing V2 next claim");
        state.Sent(markDefer, now.AddSeconds(5));
        state.Acknowledge(markDefer, world, markEnd);
        var waiting = state.Waiting(world, now);
        check(waiting.Length == 2 && waiting.Any(target => target.FateId is null), "Watch both deferred targets locally");
        state.SawUp(waiting.Single(target => target.FateId == fate.FateId));
        check(state.Plan(bRank, HuntAvailability.Unknown, world, now.AddSeconds(64)) is null, "One 60-second limit on the authorizing command target");
        check(state.Plan(bRank, HuntAvailability.Unknown, world, now.AddSeconds(65)) is { Reason: null },
            "Clear can use the last consumed claim when no new target is delivered");
        state.Consumed(ordinary);
        check(state.Plan(fate, HuntAvailability.Unknown, world, now.AddSeconds(65)) is null, "Clear cannot reuse the observed FATE's obsolete claim");
        check(state.Plan(ordinary, HuntAvailability.Unknown, world + 1, now.AddSeconds(65)) is null, "Other-world sightings cannot clear waiting targets");
        var clear = state.Plan(ordinary, HuntAvailability.Unknown, world, now.AddSeconds(65))!;
        CheckBody(check, clear, ordinary, "clear-defer", null);
        const string cleared = "{\"ok\":true,\"request\":null,\"progression\":{\"deferCleared\":true}}";
        check(HuntMapPolicy.TryDeferPoll(cleared, now, clear, out none, out end) && none is null && end is null,
            "Valid clear with null request");
        check(!HuntMapPolicy.TryDeferPoll(cleared, now, action, out _, out _), "A clear receipt cannot acknowledge a defer");
        state.Sent(clear, now.AddSeconds(65));
        state.Acknowledge(clear, world, null);
        check(state.Waiting(world, now).Length == 0 && state.Plan(ordinary, HuntAvailability.Unknown, world, now.AddSeconds(66)) is null,
            "Clear removes every deferred target and pending sighting");
        state.Consumed(nextCandidate);
        markDefer = state.Plan(nextCandidate, HuntAvailability.NotSeen, world, now.AddMinutes(2))!;
        state.Acknowledge(markDefer, world, markEnd);
        state.SawUp(state.Waiting(world, now.AddMinutes(2)).Single());
        state.Consumed(ordinary);
        check(state.Plan(ordinary, HuntAvailability.Unknown, world, now.AddSeconds(124)) is null, "Clear throttle survives another candidate and a fresh claim");
        check(state.Plan(ordinary, HuntAvailability.Unknown, world, now.AddSeconds(125))?.Command == ordinary, "A deferred B-rank sighting also clears using the latest claim");
        check(state.Plan(ordinary, HuntAvailability.Unknown, world, now.AddMinutes(5)) is null, "Expired defer does not send a clear");
        state.Reset();
        check(state.Plan(bRank, HuntAvailability.NotSeen, world, now) is null, "Reset invalidates the current command");

        var exact = new object();
        var sonar = new object();
        check(BRankSighting.Resolve(true, exact, sonar, true, inFlight: true) == (HuntAvailability.Up, exact),
            "Object-table coordinates take priority, including in flight");
        check(BRankSighting.Resolve(false, null, sonar, true) == (HuntAvailability.Up, sonar), "Sonar coordinates can supply the exact flag");
        check(BRankSighting.Resolve(false, null, (object?)null, false) == (HuntAvailability.Unknown, null), "Outside load range keeps the spawn-area flag");
        check(BRankSighting.Resolve(false, null, (object?)null, true) == (HuntAvailability.NotSeen, null), "A missing mark near its spawn can defer");

        var revision = new string('d', 64);
        TrackedFates? Tracked(string running, TrackedFates? old = null, string location = "{\"mapX\":13.48,\"mapY\":26.28}") {
            using var document = JsonDocument.Parse($$"""
                {"schemaVersion":1,"revision":"{{revision}}","items":[{"fateId":366,"name":"He's Got Legs","territoryId":140,"mapId":20,"zoneName":"Western Thanalan","location":{{location}}}],"runningOnYourWorld":[{{running}}]}
                """);
            return TrackedFates.Parse(document.RootElement, old);
        }
        var tracked = Tracked("366")!;
        check(tracked.Items.Count == 1 && tracked.Running.SetEquals([366u]), "Exact personal tracked list and running ids");
        check(tracked.Items[366].Location == new FateMapPoint(13.48f, 26.28f), "Catalog position uses the Site's map coordinates");
        check(Tracked("", location: "null")!.Items[366].Location is null, "Null catalog position leaves the zone name");
        check(Tracked("", location: "{\"mapX\":101,\"mapY\":3}") is null
            && Tracked("", location: "{\"mapX\":15,\"mapY\":3,\"extra\":true}") is null, "Malformed catalog positions fail closed");
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
