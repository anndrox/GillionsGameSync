using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GillionsGameSync;

internal enum SyncSectionOutcome { Sent, Unchanged, Skipped }

// One Collections list left out of a section, with the game sheet or native state that wasn't ready and its first
// missing row. Key and Source are fixed identifiers; nothing here comes from the character or the payload.
internal sealed record SyncSectionList(string Key, string Source, uint? Row);

// How one section of one sync ended. Reason is a fixed code (see SyncSectionReport); HttpStatus and Code are kept only
// for a section Gillions refused. LastSentUtc is the last time this session sent the section.
internal sealed record SyncSectionStatus(string Resource, SyncSectionOutcome Outcome, string Reason, IReadOnlyList<SyncSectionList> Lists,
    int? HttpStatus, string Code, DateTime ObservedUtc, DateTime? LastSentUtc);

// A section Gillions refused (Waiting is false) or that waits out the retry delay after a refusal (Waiting is true).
internal sealed record SyncSectionPark(int? HttpStatus, string Code, bool Waiting);

// Every per-section diagnostic (the /xllog line, the Advanced tab, the support summary, the Sync now message and the
// status Gillions may receive) is built here from fixed names, fixed reason codes, game sheet names, row numbers and
// HTTP statuses. Character names, worlds, content ids, tokens and payloads never reach these inputs.
internal static class SyncSectionReport {
    internal const string NotLoaded = "not_loaded";
    internal const string AchievementsWindow = "achievements_window";
    internal const string ChangedDuringRead = "changed_during_read";
    internal const string ListsNotReady = "lists_not_ready";
    internal const string SiteRefused = "site_refused";
    internal const string SiteRetryWait = "site_retry_wait";
    internal const string StatusContract = "sync_status_v1";
    internal const string StatusPath = "/api/game-sync/sync-status";

    private static readonly (string Resource, string Name)[] Sections = [
        ("inventory", "Inventory"), ("currencies", "Currencies"), ("achievements", "Achievements"), ("collectibles", "Collections"),
        ("character", "Character"), ("quest_journal", "Quest journal"), ("reputation", "Allied societies"), ("shared_fates", "Shared FATEs"),
        ("glamour_plates", "Glamour plates"), ("gil_ledger", "Gil history"), ("retainer_ventures", "Retainer ventures"),
    ];

    internal static IEnumerable<string> Resources => Sections.Select(section => section.Resource);
    internal static IEnumerable<string> Names => Sections.Select(section => section.Name);

    internal static string Name(string resource) {
        foreach (var section in Sections) if (section.Resource == resource) return section.Name;
        return "Another section";
    }

    // A game sheet, native state or site code name: letters, digits and underscores only, else nothing.
    internal static string SafeToken(string? value) =>
        value is { Length: > 0 and <= 48 } && Regex.IsMatch(value, "^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.CultureInvariant) ? value : "";

    // sections: the resources this sync covered, in order. sent: resources Gillions accepted. withheld: the collectors'
    // "<resource>[.<list>]:<availability>[:<source>:<row>]" entries. parked: resources Gillions refused or that wait out a
    // refusal. A covered section that was neither sent, withheld nor parked had nothing new to send.
    internal static IReadOnlyList<SyncSectionStatus> Build(IReadOnlyList<string> sections, IReadOnlySet<string> sent, IReadOnlyList<string> withheld,
        IReadOnlyDictionary<string, SyncSectionPark> parked, DateTime now, IReadOnlyDictionary<string, DateTime> lastSent) {
        var order = sections.Concat(parked.Keys.Where(resource => !sections.Contains(resource))).Distinct(StringComparer.Ordinal);
        var result = new List<SyncSectionStatus>();
        foreach (var resource in order) {
            DateTime? last = sent.Contains(resource) ? now : lastSent.TryGetValue(resource, out var previous) ? previous : null;
            var lists = new List<SyncSectionList>();
            string? whole = null;
            foreach (var entry in withheld) {
                var parts = entry.Split(':');
                if (parts[0] == resource) whole ??= parts.Length > 1 ? parts[1] : nameof(NativeAvailability.NotLoaded);
                else if (parts[0].StartsWith(resource + ".", StringComparison.Ordinal)) {
                    var key = SafeToken(parts[0][(resource.Length + 1)..]);
                    uint? row = parts.Length > 3 && uint.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
                    if (key.Length > 0 && lists.All(list => list.Key != key)) lists.Add(new(key, parts.Length > 2 ? SafeToken(parts[2]) : "", row));
                }
            }
            if (parked.TryGetValue(resource, out var park))
                result.Add(new(resource, SyncSectionOutcome.Skipped, park.Waiting ? SiteRetryWait : SiteRefused, lists,
                    park.HttpStatus is >= 100 and <= 599 ? park.HttpStatus : null, SafeToken(park.Code), now, last));
            else if (whole is not null)
                result.Add(new(resource, SyncSectionOutcome.Skipped,
                    whole == nameof(NativeAvailability.StaleContext) ? ChangedDuringRead
                    : lists.Count > 0 ? ListsNotReady
                    : resource == "achievements" ? AchievementsWindow : NotLoaded, lists, null, "", now, last));
            else
                result.Add(new(resource, sent.Contains(resource) ? SyncSectionOutcome.Sent : SyncSectionOutcome.Unchanged,
                    lists.Count > 0 ? ListsNotReady : "", lists, null, "", now, last));
        }
        return result;
    }

    private static string Detail(SyncSectionList list) =>
        list.Source.Length == 0 ? "not ready"
        : list.Row is { } row ? $"{list.Source} row {row.ToString(CultureInfo.InvariantCulture)} missing"
        : $"{list.Source} not loaded";

    private static string Lists(SyncSectionStatus status) =>
        string.Join(", ", status.Lists.Select(list => $"{CollectibleSnapshotPolicy.Label(list.Key)} ({Detail(list)})"));

    private static string Labels(SyncSectionStatus status) => string.Join(", ", status.Lists.Select(list => CollectibleSnapshotPolicy.Label(list.Key)));

    internal static string ReasonText(SyncSectionStatus status) => status.Reason switch {
        NotLoaded => "not loaded in game yet",
        AchievementsWindow => "the Achievements window hasn't been opened since login",
        ChangedDuringRead => "the area or character changed during the read",
        ListsNotReady when status.Outcome == SyncSectionOutcome.Skipped => "not ready: " + Lists(status),
        ListsNotReady => "without " + Lists(status),
        SiteRefused => "Gillions refused it (" + (status.HttpStatus is { } http ? "HTTP " + http.ToString(CultureInfo.InvariantCulture) : "no status")
            + (status.Code.Length > 0 ? ", " + status.Code : "") + ")",
        SiteRetryWait => "waiting to retry after Gillions refused it",
        _ => "",
    };

    private static string OutcomeWord(SyncSectionOutcome outcome) => outcome switch {
        SyncSectionOutcome.Sent => "sent", SyncSectionOutcome.Unchanged => "unchanged", _ => "skipped",
    };

    // The /xllog line, e.g. "Gillions Game Sync: Collections sent without portrait conditions (BannerCondition row 123 missing)."
    internal static string LogLine(SyncSectionStatus status) {
        var reason = ReasonText(status);
        var joiner = status.Outcome == SyncSectionOutcome.Skipped ? ", " : " ";
        return $"Gillions Game Sync: {Name(status.Resource)} {OutcomeWord(status.Outcome)}{(reason.Length > 0 ? joiner + reason : "")}.";
    }

    // Background syncs log a section only when its outcome or reason differs from the last line logged for it; a manual
    // sync logs every section.
    internal static bool ShouldLog(bool manual, SyncSectionStatus? previous, SyncSectionStatus current) =>
        manual || previous is null || LogLine(previous) != LogLine(current);

    private static string Time(DateTime utc, DateTime nowUtc) {
        var local = utc.ToLocalTime();
        return local.Date == nowUtc.ToLocalTime().Date
            ? local.ToString("HH:mm", CultureInfo.InvariantCulture)
            : local.ToString("d MMM HH:mm", CultureInfo.InvariantCulture);
    }

    // The Advanced tab and support summary line, e.g. "Achievements: skipped 05:12 · the Achievements window hasn't been
    // opened since login · last sent 4 Nov 21:03".
    internal static string StatusLine(SyncSectionStatus status, DateTime nowUtc) {
        var parts = new List<string> { $"{Name(status.Resource)}: {OutcomeWord(status.Outcome)} {Time(status.ObservedUtc, nowUtc)}" };
        var reason = ReasonText(status);
        if (reason.Length > 0) parts.Add(reason);
        if (status.Outcome != SyncSectionOutcome.Sent)
            parts.Add(status.LastSentUtc is { } last ? "last sent " + Time(last, nowUtc) : "not sent this session");
        return string.Join(" · ", parts);
    }

    // The Sync now result. "Sync completed." only when every section was sent or unchanged; otherwise it names what was
    // paused, what wasn't ready and which Collections lists were left out.
    internal static string Message(IReadOnlyList<SyncSectionStatus> statuses, bool anySent) {
        var paused = statuses.Where(status => status.Reason is SiteRefused or SiteRetryWait).Select(status => Name(status.Resource)).ToArray();
        var notReady = statuses.Where(status => status.Outcome == SyncSectionOutcome.Skipped && status.Reason is not (SiteRefused or SiteRetryWait))
            .Select(status => status.Lists.Count > 0 ? $"{Name(status.Resource)} (not ready: {Labels(status)})" : Name(status.Resource)).ToArray();
        var partial = statuses.Where(status => status.Outcome != SyncSectionOutcome.Skipped && status.Lists.Count > 0).ToArray();
        if (paused.Length == 0 && notReady.Length == 0 && partial.Length == 0)
            return anySent ? "Sync completed." : "Your supported data is already current.";
        var sentences = new List<string>();
        if (paused.Length > 0) sentences.Add($"Gillions paused updates for {string.Join(", ", paused)}. Game Sync will retry.");
        if (notReady.Length > 0) sentences.Add($"Not ready yet, kept unchanged on Gillions: {string.Join(", ", notReady)}.");
        foreach (var status in partial) sentences.Add($"{Name(status.Resource)} were sent without {Labels(status)} (not ready yet).");
        if (notReady.Length > 0 || partial.Length > 0) sentences.Add("Try again in a moment.");
        return string.Join(" ", sentences);
    }

    // The exact shape Message produces, so the Game Sync window can show it and still drop any other text.
    private static readonly Regex MessageShape = BuildMessageShape();

    private static Regex BuildMessageShape() {
        static string Any(IEnumerable<string> values) => "(?:" + string.Join("|", values.Select(Regex.Escape)) + ")";
        static string List(string item) => item + "(?:, " + item + ")*";
        var labels = List(Any(CollectibleSnapshotPolicy.LabelValues));
        var name = Any(Names);
        var notReadyItem = "(?:" + name + "(?: \\(not ready: " + labels + "\\))?)";
        var paused = "Gillions paused updates for " + List(name) + "\\. Game Sync will retry\\.";
        var notReady = "Not ready yet, kept unchanged on Gillions: " + List(notReadyItem) + "\\.";
        var partial = name + " were sent without " + labels + " \\(not ready yet\\)\\.";
        return new Regex("^(?:" + paused + "|(?:" + paused + " )?(?:(?:" + notReady + ")(?: " + partial + ")*|" + partial + "(?: " + partial + ")*) Try again in a moment\\.)$",
            RegexOptions.CultureInvariant);
    }

    internal static bool IsMessage(string value) => MessageShape.IsMatch(value);

    // The support summary block: one status line per section from the last sync.
    internal static string Summary(IReadOnlyList<SyncSectionStatus> statuses, DateTime nowUtc) =>
        statuses.Count == 0 ? "Sections: no sync this session"
        : "Sections:\n" + string.Join("\n", statuses.Select(status => "- " + StatusLine(status, nowUtc)));

    // The status Gillions may receive after a sync, only when it advertises sync_status_v1. It carries the same fixed
    // codes; the character identity is the snapshot's own, so Gillions files it under the right character.
    internal static object SiteStatus(object identity, IReadOnlyList<SyncSectionStatus> statuses) => new {
        character = identity,
        schemaVersion = 1,
        sections = statuses.Select(status => new {
            section = status.Resource,
            outcome = OutcomeWord(status.Outcome),
            reason = status.Reason,
            lists = status.Lists.Select(list => new { key = list.Key, source = list.Source, row = list.Row }).ToArray(),
            httpStatus = status.HttpStatus,
            code = status.Code,
        }).ToArray(),
    };

    // True only when the presence response names sync_status_v1; older Sites never receive the status.
    internal static bool SiteSupportsStatus(string presenceJson) {
        try {
            using var document = JsonDocument.Parse(presenceJson);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("syncStatusContract", out var contract)
                && contract.ValueKind == JsonValueKind.String && contract.GetString() == StatusContract;
        } catch (JsonException) {
            return false;
        }
    }
}
