using System.Text.Json;
using GillionsGameSync;

internal static class SyncSectionReportTests {
    internal static void Run() {
        var checks = 0;
        void Check(bool value, string why) { checks++; if (!value) throw new Exception("Sync section report: " + why); }
        var now = new DateTime(2026, 11, 17, 5, 12, 0, DateTimeKind.Utc);
        var earlier = new DateTime(2026, 11, 4, 21, 3, 0, DateTimeKind.Utc);
        var none = new Dictionary<string, DateTime>();
        var noParks = new Dictionary<string, SyncSectionPark>();

        // Every skip reason, one section each, plus sent and unchanged.
        string[] sections = ["inventory", "currencies", "achievements", "character", "quest_journal", "reputation", "shared_fates", "glamour_plates", "collectibles", "retainer_ventures"];
        var parks = new Dictionary<string, SyncSectionPark> {
            ["quest_journal"] = new(400, "INVALID_PAYLOAD", Waiting: false),
            ["gil_ledger"] = new(null, "", Waiting: true),
        };
        var report = SyncSectionReport.Build(sections, new HashSet<string> { "currencies" },
            ["inventory:NotLoaded", "achievements:NotLoaded", "character:StaleContext", "glamour_plates:NotLoaded"], parks, now,
            new Dictionary<string, DateTime> { ["inventory"] = earlier });
        var byResource = report.ToDictionary(status => status.Resource);
        Check(report.Select(status => status.Resource).SequenceEqual([.. sections, "gil_ledger"]), "sections keep sync order; a parked extra section comes last");
        (string Resource, SyncSectionOutcome Outcome, string Reason, string Line)[] expected = [
            ("inventory", SyncSectionOutcome.Skipped, SyncSectionReport.NotLoaded, "Gillions Game Sync: Inventory skipped, not loaded in game yet."),
            ("currencies", SyncSectionOutcome.Sent, "", "Gillions Game Sync: Currencies sent."),
            ("achievements", SyncSectionOutcome.Skipped, SyncSectionReport.AchievementsWindow, "Gillions Game Sync: Achievements skipped, the Achievements window hasn't been opened since login."),
            ("character", SyncSectionOutcome.Skipped, SyncSectionReport.ChangedDuringRead, "Gillions Game Sync: Character skipped, the area or character changed during the read."),
            ("quest_journal", SyncSectionOutcome.Skipped, SyncSectionReport.SiteRefused, "Gillions Game Sync: Quest journal skipped, Gillions refused it (HTTP 400, INVALID_PAYLOAD)."),
            ("reputation", SyncSectionOutcome.Unchanged, "", "Gillions Game Sync: Allied societies unchanged."),
            ("shared_fates", SyncSectionOutcome.Unchanged, "", "Gillions Game Sync: Shared FATEs unchanged."),
            ("glamour_plates", SyncSectionOutcome.Skipped, SyncSectionReport.NotOpened, "Gillions Game Sync: Glamour plates skipped, not loaded in game right now, Gillions keeps the last copy."),
            ("collectibles", SyncSectionOutcome.Unchanged, "", "Gillions Game Sync: Collections unchanged."),
            ("retainer_ventures", SyncSectionOutcome.Unchanged, "", "Gillions Game Sync: Retainer ventures unchanged."),
            ("gil_ledger", SyncSectionOutcome.Skipped, SyncSectionReport.SiteRetryWait, "Gillions Game Sync: Gil history skipped, waiting to retry after Gillions refused it."),
        ];
        foreach (var (resource, outcome, reason, line) in expected) {
            var status = byResource[resource];
            Check(status.Outcome == outcome && status.Reason == reason, $"{resource}: {outcome} / {reason}");
            Check(SyncSectionReport.LogLine(status) == line, $"{resource}: log line ({SyncSectionReport.LogLine(status)})");
        }
        Check(byResource["currencies"].LastSentUtc == now && byResource["inventory"].LastSentUtc == earlier && byResource["achievements"].LastSentUtc is null,
            "last sent is now for a sent section, kept for a skipped one, empty when never sent");
        var message = SyncSectionReport.Message(report, anySent: true);
        Check(message == "Gillions paused updates for Quest journal, Gil history. Game Sync will retry. Not ready yet, kept unchanged on Gillions: Inventory, Character. Try again in a moment. "
            + SyncSectionReport.AchievementsStep, "Sync now names paused and skipped sections and the Achievements step, never Sync completed. (" + message + ")");
        Check(PublicConnectionPresentation.ActionMessage(message) == message, "the window shows the combined message");

        // "Sync completed." only when every section was sent or unchanged.
        var clean = SyncSectionReport.Build(["inventory", "currencies"], new HashSet<string> { "inventory" }, [], noParks, now, none);
        Check(SyncSectionReport.Message(clean, anySent: true) == "Sync completed.", "all sent or unchanged: Sync completed.");
        Check(SyncSectionReport.Message(SyncSectionReport.Build(["inventory"], new HashSet<string>(), [], noParks, now, none), anySent: false) == "Your supported data is already current.",
            "nothing new: already current");
        var oneSkipped = SyncSectionReport.Build(["inventory", "achievements"], new HashSet<string> { "inventory" }, ["achievements:NotLoaded"], noParks, now, none);
        Check(SyncSectionReport.Message(oneSkipped, anySent: true) == SyncSectionReport.AchievementsStep,
            "achievements not loaded: the step that loads them, no Sync completed. and no Try again");
        var loadedLater = SyncSectionReport.Build(["inventory", "currencies"], new HashSet<string> { "inventory" }, ["currencies:NotLoaded"], noParks, now, none);
        Check(SyncSectionReport.Message(loadedLater, anySent: true) == "Not ready yet, kept unchanged on Gillions: Currencies. Try again in a moment.",
            "a section the game loads on its own: no Sync completed., try again");
        // Reviewer 1 (c0c3448): a section that loads only with its window open is reported, but an ordinary sync still completes.
        foreach (var resource in new[] { "glamour_plates", "shared_fates", "quest_journal", "reputation" }) {
            var ordinary = SyncSectionReport.Build(["inventory", resource], new HashSet<string> { "inventory" }, [resource + ":NotLoaded"], noParks, now, none);
            var skipped = ordinary.Single(status => status.Resource == resource);
            Check(skipped.Outcome == SyncSectionOutcome.Skipped && skipped.Reason == SyncSectionReport.NotOpened, resource + ": reported as not opened");
            Check(SyncSectionReport.Message(ordinary, anySent: true) == "Sync completed.", resource + " not loaded plus an accepted send: Sync completed.");
            Check(SyncSectionReport.Message(SyncSectionReport.Build([resource], new HashSet<string>(), [resource + ":NotLoaded"], noParks, now, none), anySent: false) == "Your supported data is already current.",
                resource + " alone: already current");
            Check(SyncSectionReport.StatusLine(skipped, now).Contains(" · not loaded in game right now, Gillions keeps the last copy", StringComparison.Ordinal), resource + ": the Advanced tab still says so");
        }
        var pausedOnly = SyncSectionReport.Build(["inventory"], new HashSet<string>(), [], new Dictionary<string, SyncSectionPark> { ["inventory"] = new(409, "STALE", false) }, now, none);
        var pausedMessage = SyncSectionReport.Message(pausedOnly, anySent: false);
        Check(pausedMessage == "Gillions paused updates for Inventory. Game Sync will retry." && PublicConnectionPresentation.ActionMessage(pausedMessage) == pausedMessage, "paused only");
        var partial = SyncSectionReport.Build(["achievements", "collectibles"], new HashSet<string> { "collectibles" },
            ["achievements:NotLoaded", "collectibles.portraitConditions:NotLoaded:BannerCondition:123", "collectibles.folkloreBookIds:NotLoaded:PlayerState:"], noParks, now, none);
        var partialMessage = SyncSectionReport.Message(partial, anySent: true);
        Check(partialMessage == "Collections were sent without portrait conditions, folklore tomes (not ready yet). Try again in a moment. " + SyncSectionReport.AchievementsStep
            && PublicConnectionPresentation.ActionMessage(partialMessage) == partialMessage, "left-out lists and the Achievements step together (" + partialMessage + ")");
        foreach (var shown in new[] { SyncSectionReport.AchievementsStep, "Gillions paused updates for Inventory. Game Sync will retry. " + SyncSectionReport.AchievementsStep,
            "Gillions paused updates for Inventory. Game Sync will retry. Not ready yet, kept unchanged on Gillions: Currencies, Collections (not ready: minions). Collections were sent without portrait conditions (not ready yet). Try again in a moment. " + SyncSectionReport.AchievementsStep })
            Check(PublicConnectionPresentation.ActionMessage(shown) == shown, "the window shows: " + shown);
        foreach (var refused in new[] { "", " " + SyncSectionReport.AchievementsStep, SyncSectionReport.AchievementsStep + " synthetic-bearer", "Try again in a moment. synthetic-token" })
            Check(PublicConnectionPresentation.ActionMessage(refused).Length == 0, "the window refuses: [" + refused + "]");
        Check(SyncSectionReport.LogLine(partial[1]) == "Gillions Game Sync: Collections sent without portrait conditions (BannerCondition row 123 missing), folklore tomes (PlayerState not loaded).",
            "the log names each left-out list with its sheet and row");
        var unchangedPartial = SyncSectionReport.Build(["collectibles"], new HashSet<string>(), ["collectibles.portraitConditions:NotLoaded:BannerCondition:123"], noParks, now, none).Single();
        Check(unchangedPartial.Outcome == SyncSectionOutcome.Unchanged && SyncSectionReport.LogLine(unchangedPartial) == "Gillions Game Sync: Collections unchanged without portrait conditions (BannerCondition row 123 missing).",
            "an unchanged send still names the list left out");

        // The window shows only these shapes.
        Check(PublicConnectionPresentation.ActionMessage("Sync completed.") == "Sync completed." && PublicConnectionPresentation.ActionMessage("Your supported data is already current.") == "Your supported data is already current.",
            "the existing results are still shown");
        foreach (var marker in new[] { "synthetic-bearer", "account-id-123", "https://example.invalid/", "collectibles:NotLoaded", "Minions 12", "Fixture Adventurer" }) {
            Check(PublicConnectionPresentation.ActionMessage($"Not ready yet, kept unchanged on Gillions: {marker}. Try again in a moment.").Length == 0, "not-ready shape refuses: " + marker);
            Check(PublicConnectionPresentation.ActionMessage($"Collections were sent without {marker} (not ready yet). Try again in a moment.").Length == 0, "left-out shape refuses: " + marker);
            Check(PublicConnectionPresentation.ActionMessage($"Gillions paused updates for {marker}. Game Sync will retry.").Length == 0, "paused shape refuses: " + marker);
        }

        // Background syncs log a section only when its line changes; manual syncs log every section.
        var first = clean[0];
        Check(SyncSectionReport.ShouldLog(manual: false, previous: null, first), "first background status is logged");
        Check(!SyncSectionReport.ShouldLog(manual: false, previous: first, first with { ObservedUtc = now.AddMinutes(1) }), "a repeated background line is not logged again");
        Check(SyncSectionReport.ShouldLog(manual: false, previous: first, byResource["inventory"]), "a changed background line is logged");
        Check(SyncSectionReport.ShouldLog(manual: true, previous: first, first), "a manual sync logs every section");

        // Advanced tab lines and the support summary.
        var achievementsLine = SyncSectionReport.StatusLine(byResource["achievements"], now);
        Check(achievementsLine.StartsWith("Achievements: skipped ", StringComparison.Ordinal)
            && achievementsLine.EndsWith(" · the Achievements window hasn't been opened since login · not sent this session", StringComparison.Ordinal), "skipped line: reason and never sent (" + achievementsLine + ")");
        var inventoryLine = SyncSectionReport.StatusLine(byResource["inventory"], now);
        Check(inventoryLine.Contains(" · not loaded in game yet · last sent ", StringComparison.Ordinal), "skipped line keeps the last sent time (" + inventoryLine + ")");
        Check(!SyncSectionReport.StatusLine(byResource["currencies"], now).Contains("last sent", StringComparison.Ordinal), "a sent line needs no last-sent time");
        Check(report.All(status => !SyncSectionReport.StatusLine(status, now).Contains('—') && !SyncSectionReport.LogLine(status).Contains('—')), "no em dashes");
        var lines = report.Select(status => SyncSectionReport.StatusLine(status, now)).ToArray();
        var health = new PublicHealth("Connected", "Fixture Adventurer · Fixture World", "Ready", "Off", "Off", "Off", now, "Public", "1.0.31.11", "2026.11.03.0000.0000", lines);
        var summary = health.SupportSummary();
        Check(summary.Contains("\nSections:\n- Inventory: skipped ", StringComparison.Ordinal) && summary.Split('\n').Count(line => line.StartsWith("- ", StringComparison.Ordinal)) == report.Count,
            "the support summary lists every section");
        Check(new PublicHealth("Connected", "c", "Ready", "Off", "Off", "Off", null, "Public", "1.0.31.11", "2026.11.03.0000.0000").SupportSummary().EndsWith("\nSections: no sync this session", StringComparison.Ordinal),
            "the support summary says when no sync ran this session");

        // The status Gillions may receive, and only when it asks.
        var site = JsonSerializer.SerializeToElement(SyncSectionReport.SiteStatus(new { name = "Fixture Adventurer", world = "Fixture World" }, partial));
        Check(site.GetProperty("schemaVersion").GetInt32() == 1 && site.GetProperty("sections").GetArrayLength() == 2, "site status: schema and sections");
        var collections = site.GetProperty("sections")[1];
        Check(collections.GetProperty("section").GetString() == "collectibles" && collections.GetProperty("outcome").GetString() == "sent"
            && collections.GetProperty("reason").GetString() == SyncSectionReport.ListsNotReady, "site status: section, outcome and reason code");
        Check(collections.GetProperty("lists")[0].GetProperty("key").GetString() == "portraitConditions" && collections.GetProperty("lists")[0].GetProperty("source").GetString() == "BannerCondition"
            && collections.GetProperty("lists")[0].GetProperty("row").GetUInt32() == 123 && collections.GetProperty("lists")[1].GetProperty("row").ValueKind == JsonValueKind.Null,
            "site status: each left-out list with its sheet and row");
        Check(site.EnumerateObject().Select(p => p.Name).SequenceEqual(["character", "schemaVersion", "sections"]), "site status carries no payload");
        Check(SyncSectionReport.SiteSupportsStatus("{\"ok\":true,\"syncStatusContract\":\"sync_status_v1\"}"), "a Site that names sync_status_v1 receives the status");
        Check(!SyncSectionReport.SiteSupportsStatus("{\"ok\":true}") && !SyncSectionReport.SiteSupportsStatus("{\"syncStatusContract\":\"sync_status_v2\"}")
            && !SyncSectionReport.SiteSupportsStatus("not json") && !SyncSectionReport.SiteSupportsStatus("[]"), "older or other Sites never receive it");

        // Codes and sheet names are plain identifiers or nothing.
        Check(SyncSectionReport.SafeToken("INVALID_PAYLOAD") == "INVALID_PAYLOAD" && SyncSectionReport.SafeToken("BannerCondition") == "BannerCondition", "plain identifiers pass");
        Check(SyncSectionReport.SafeToken("a b") == "" && SyncSectionReport.SafeToken("token=abc") == "" && SyncSectionReport.SafeToken(new string('a', 49)) == "" && SyncSectionReport.SafeToken(null) == "",
            "anything else is dropped");
        var leaky = SyncSectionReport.Build(["inventory"], new HashSet<string>(), [], new Dictionary<string, SyncSectionPark> { ["inventory"] = new(400, "Bearer synthetic-token", false) }, now, none).Single();
        Check(SyncSectionReport.LogLine(leaky) == "Gillions Game Sync: Inventory skipped, Gillions refused it (HTTP 400).", "a refusal code that isn't an identifier is left out");
        var odd = SyncSectionReport.Build(["collectibles"], new HashSet<string> { "collectibles" }, ["collectibles.Fixture Adventurer:NotLoaded:Fixture World:12"], noParks, now, none).Single();
        Check(odd.Lists.Count == 0, "a withheld entry that isn't made of identifiers names nothing");

        // Privacy: a full flow from a capture that carries a character never puts the character in a log line, the
        // Advanced tab, the support summary or the Sync now message.
        const string name = "Fixture Adventurer", world = "Fixture World";
        const ulong contentId = 9876543210123456;
        var capture = CollectibleSnapshotPolicy.Capture(() => contentId, new { name, world }, [
            new("minions", () => new long[] { 424242 }), new("portraitConditions", () => throw new CollectibleSourceUnavailableException("BannerCondition", 77)),
        ]);
        Check(capture.Payload is not null, "privacy fixture sends the ready list");
        var flow = SyncSectionReport.Build(["collectibles", "achievements"], new HashSet<string> { "collectibles" }, [.. capture.Withheld, "achievements:NotLoaded"],
            new Dictionary<string, SyncSectionPark> { ["achievements"] = new(403, "CHARACTER_NOT_LINKED", false) }, now, none);
        var texts = flow.Select(SyncSectionReport.LogLine).Concat(flow.Select(status => SyncSectionReport.StatusLine(status, now)))
            .Append(SyncSectionReport.Summary(flow, now)).Append(SyncSectionReport.Message(flow, anySent: true))
            .Append(new PublicHealth("Connected", name + " · " + world, "Ready", "Off", "Off", "Off", now, "Public", "1.0.31.11", "2026.11.03.0000.0000",
                flow.Select(status => SyncSectionReport.StatusLine(status, now)).ToArray()).SupportSummary()).ToArray();
        foreach (var text in texts) {
            Check(!text.Contains(name, StringComparison.OrdinalIgnoreCase) && !text.Contains(world, StringComparison.OrdinalIgnoreCase), "no character name or world: " + text);
            Check(!text.Contains(contentId.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal), "no content id: " + text);
            Check(!text.Contains("424242", StringComparison.Ordinal), "no payload values: " + text);
            Check(!text.Contains("Bearer", StringComparison.OrdinalIgnoreCase) && !text.Contains("token", StringComparison.OrdinalIgnoreCase), "no tokens: " + text);
        }
        var siteFlow = JsonSerializer.Serialize(SyncSectionReport.SiteStatus(new { name, world }, flow));
        Check(!siteFlow.Contains(contentId.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal) && !siteFlow.Contains("424242", StringComparison.Ordinal),
            "the site status carries no content id and no payload values");
        Console.WriteLine($"Sync section report PASS: {checks}; synthetic statuses, not live game or HTTP proof.");
    }
}
