using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GillionsGameSync;

// Pure presentation state. No gameplay reads, transport, credentials or history.
internal sealed record PublicHealth(string Connection, string Character, string Hunts, string PartyFinder,
    string Fates, string Market, DateTime? LastSync, string Channel, string Version, string GameVersion, IReadOnlyList<string>? Sections = null) {
    private static string SafeVersion(string v) => Regex.IsMatch(v,@"^\d{1,8}(?:\.\d{1,8}){2,4}$") ? v : "unavailable";
    private static string SafeStatus(string s) => s is "Connected" or "Not connected" or "Connecting" or "Authorization expired or revoked"
        or "Account unavailable" or "Ready" or "Off on Gillions" or "Waiting for supported context" or "Temporarily unavailable" or "Gillions unavailable" or "Update required"
        or "Off" or "Not available on Gillions" or "Game version not supported yet" or CharacterNotLinked or OtherCharacter or ReconnectRequired or Refused ? s : "Temporarily unavailable";
    internal const string CharacterNotLinked = "Character not linked on Gillions";
    internal const string OtherCharacter = "Paired to a different character";
    internal const string ReconnectRequired = "Reconnect required";
    internal const string Refused = "Refused by Gillions";
    internal string SupportSummary() => $"Gillions Game Sync {SafeVersion(Version)}\nChannel: {(Channel is "Testing" or "Public" ? Channel : "unavailable")}\nConnection: {SafeStatus(Connection)}\nGame: {SafeVersion(GameVersion)}\nHunts: {SafeStatus(Hunts)}\nParty Finder: {SafeStatus(PartyFinder)}\nFATEs: {SafeStatus(Fates)}\nMarket: {SafeStatus(Market)}\nLast successful sync: {LastSync?.ToString("O") ?? "Not yet"}\n{(Sections is { Count: > 0 } ? "Sections:\n" + string.Join("\n", Sections.Select(line => "- " + line)) : "Sections: no sync this session")}";
    internal static string ConnectionState(bool paired, bool pairing, string code) => pairing ? "Connecting"
        : code is "DEVICE_INVALID" or "DEVICE_REVOKED" or "TOKEN_INVALID" ? "Authorization expired or revoked"
        : code is "UNSUPPORTED_CLIENT" or "VERSION_UNSUPPORTED" or "CLIENT_VERSION_UNSUPPORTED" ? "Update required"
        : code is "ACCOUNT_DISABLED" or "TRIAL_EXPIRED" ? "Account unavailable"
        : !paired ? "Not connected"
        : code == SiteRefusalPolicy.CharacterNotLinked ? CharacterNotLinked
        : code == SiteRefusalPolicy.CharacterMismatch ? OtherCharacter
        : code == SiteRefusalPolicy.ProductMismatch ? ReconnectRequired
        : code == SiteRefusalPolicy.Rejected ? Refused : "Connected";
    // An unqualified local game/SDK is not a client update; a Site that has not
    // admitted this source is not a local version mismatch.
    internal const string UnsupportedGame = "Game version not supported yet";
    internal static string FateState(bool compatible, bool bound, string? reason, bool? granted, bool? policyEnabled, bool authorized, bool contextReady) =>
        !compatible ? UnsupportedGame
        : !bound ? "Waiting for supported context"
        : reason == "FATE_SCHEMA_UNSUPPORTED" ? "Update required"
        : reason is "FATE_SOURCE_UNSUPPORTED" or "TESTING_PRODUCT_REQUIRED" ? "Not available on Gillions"
        : reason == "ACCOUNT_UNAVAILABLE" ? "Account unavailable"
        : reason is "DEVICE_INVALID" or "DEVICE_REVOKED" or "TOKEN_INVALID" ? "Authorization expired or revoked"
        : reason == "FATE_POLICY_REQUIRED" && policyEnabled == false ? "Off on Gillions"
        : granted == true && authorized ? (contextReady ? "Ready" : "Waiting for supported context") : "Temporarily unavailable";
    // Market and Party Finder contribution. A decision to stay Off is never an error.
    // decision: the Site permission state ("explicit", "legacy" or "invalid").
    internal static string ContributionState(bool siteOrigin, bool loggedIn, bool leaseFresh, string decision, bool allowed,
        bool accepted, bool stopped) =>
        !siteOrigin ? "Not available on Gillions"
        : !loggedIn ? "Waiting for supported context"
        : !leaseFresh ? "Temporarily unavailable"
        : decision == "invalid" ? "Not available on Gillions"
        : !allowed ? (decision == "explicit" ? "Off on Gillions" : "Off")
        : stopped || !accepted ? "Not available on Gillions" : "Ready";
    internal static string HuntState(bool qualified, bool reading, bool fresh) =>
        !qualified ? UnsupportedGame : !reading ? "Off" : fresh ? "Ready" : "Waiting for supported context";
}

internal static class PublicConnectionPresentation {
    internal const string Disconnected = "Disconnected. Saved history remains on this PC.";
    internal const string DisconnectedLocally = "Disconnected on this PC. Gillions could not confirm revocation. Disconnect this device in Gillions settings.";
    internal static string PairingOrigin(string value) => SyncOrigin.TryNormalize(value,out var origin) ? origin : "";
    internal static string PairingUrl(string value) => PairingOrigin(value) is { Length: > 0 } origin ? origin+"/gillions-sync#pairing" : "";
    internal static string ActionMessage(string value, bool pairing = false) => value switch {
        "Connected. Gillions will load your selected character." or "Sync completed." or "Your supported data is already current."
            or Disconnected or DisconnectedLocally or "Your Gillions trial has ended." or "This Gillions account is disabled."
            or "The connection or sync settings changed. Try again when ready." => value,
        "Gillions could not complete the request. Pending records were kept; please try again." => pairing
            ? "Pairing did not complete. Check the destination and connection, then create a fresh one-time code there and try again."
            : value,
        _ when SyncSectionReport.IsMessage(value) => value,
        _ => ""
    };
}

internal sealed record HuntProgressRow(string Key, string Name, int Current, int Required) {
    internal uint NpcNameId { get; init; }
    internal int Remaining => Math.Max(0, Required - Current);
    internal bool Complete => Current == Required;
}
internal sealed record HuntProgressSnapshot(uint Territory, string Area, string Availability,
    bool CoverageComplete, HuntProgressRow[] Rows, long ExpiresAt) {
    internal static readonly HuntProgressSnapshot Unavailable = new(0, "", "Hunt progress unavailable", false, [], 0);
}
internal sealed record HuntProgressDisplay(string Area, string Status, HuntProgressRow[] Rows);

// One session-local display state. Territory entry, not progress/reconnect, resets
// manual-close suppression. Completion holds are monotonic and never persisted.
internal sealed class HuntProgressState {
    private uint territory;
    private bool enabled, suppressed, visible, manuallyOpened;
    private Dictionary<string, (HuntProgressRow Row, long LastCountChange)> prior = new();
    private readonly Dictionary<string, long> completed = new();
    private HuntProgressRow[] priorRows = [], orderedRows = [];
    private long countRevision, allCompleteUntil;
    private uint priorTargetNpcNameId;
    private string? activeMarkKey;
    internal long InteractionRevision { get; private set; }
    internal bool Visible => visible;
    internal HuntProgressDisplay Display { get; private set; } = new("", "Hunt progress unavailable", []);
    internal void Close() { InteractionRevision++; visible = false; suppressed = true; manuallyOpened = false; }
    internal void Open() { InteractionRevision++; suppressed = false; manuallyOpened = true; visible = true; }
    internal void Invalidate(bool endTerritorySession = false) {
        ClearRows();
        visible = false;
        Display = new("", "Hunt progress unavailable", []);
        if (endTerritorySession) { InteractionRevision++; territory = 0; suppressed = false; manuallyOpened = false; }
    }
    private void ClearRows() {
        prior.Clear(); completed.Clear();
        priorRows = []; orderedRows = [];
        countRevision = 0; allCompleteUntil = 0;
        priorTargetNpcNameId = 0; activeMarkKey = null;
    }

    internal void Update(bool show, uint currentTerritory, HuntProgressSnapshot sample, long clock,
        uint targetNpcNameId = 0) {
        if (territory != currentTerritory || enabled != show) {
            InteractionRevision++;
            territory = currentTerritory; enabled = show; suppressed = false; visible = false;
            manuallyOpened = false;
            ClearRows();
        }
        if (!show) { visible = false; return; }
        bool fresh = currentTerritory != 0 && sample.Territory == currentTerritory && clock < sample.ExpiresAt;
        if (!fresh || sample.Availability.Length != 0) {
            // Never leave stale numbers displayed as current during reconciliation.
            ClearRows();
            Display = new(sample.Territory == currentTerritory ? sample.Area : "", sample == HuntProgressSnapshot.Unavailable ? "Hunt progress unavailable"
                : fresh ? sample.Availability : "Hunt progress updating…", []);
            return;
        }
        var rows = sample.Rows;
        bool rowsChanged = !ReferenceEquals(rows, priorRows) && !rows.AsSpan().SequenceEqual(priorRows);
        bool hasIncomplete = rows.Any(row => !row.Complete);
        if (rowsChanged) {
            if (!hasIncomplete && sample.CoverageComplete && priorRows.Any(row => !row.Complete))
                allCompleteUntil = clock + 2500;
            TrackCountChanges(rows, clock);
        }
        priorRows = rows;

        if (rowsChanged || targetNpcNameId != priorTargetNpcNameId) {
            priorTargetNpcNameId = targetNpcNameId;
            var nextActive = SelectActiveMark(rows, targetNpcNameId);
            if (rowsChanged || nextActive != activeMarkKey) {
                activeMarkKey = nextActive;
                // LINQ's stable ordering keeps every other mark in bill order.
                orderedRows = rows.OrderBy(row => row.Complete).ThenBy(row => row.Key != activeMarkKey).ToArray();
            }
        }

        int heldBefore = completed.Count;
        if (heldBefore > 0) {
            foreach (var key in completed.Where(pair => clock >= pair.Value
                || !prior.TryGetValue(pair.Key, out var mark) || !mark.Row.Complete).Select(pair => pair.Key).ToArray())
                completed.Remove(key);
        }

        if (hasIncomplete && !suppressed) visible = true;
        // Keep finished rows at the bottom while hunting. Once all are finished,
        // keep the existing brief completion hold and auto-hide behavior.
        var displayRows = orderedRows;
        if (!hasIncomplete) {
            if (completed.Count == 0) displayRows = [];
            else if (rowsChanged || completed.Count != heldBefore)
                displayRows = orderedRows.Where(row => completed.ContainsKey(row.Key)).ToArray();
            else displayRows = Display.Rows;
        }
        bool allComplete = !hasIncomplete && sample.CoverageComplete && clock < allCompleteUntil;
        Display = new(sample.Area, allComplete ? "All current Hunt targets here are complete."
            : rows.Length == 0 ? "None of your Hunt targets are in this zone."
            : !hasIncomplete && !sample.CoverageComplete ? "The Hunt targets you can see here are complete." : "", displayRows);
        if (!hasIncomplete && completed.Count == 0 && !allComplete && !manuallyOpened) visible = false;
    }

    private void TrackCountChanges(HuntProgressRow[] rows, long clock) {
        // All count changes in one snapshot share a revision: their exact order is unknown.
        countRevision++;
        var next = new Dictionary<string, (HuntProgressRow Row, long LastCountChange)>(rows.Length);
        foreach (var row in rows) {
            long lastCountChange = 0;
            if (prior.TryGetValue(row.Key, out var before)) {
                lastCountChange = before.Row.Current == row.Current ? before.LastCountChange : countRevision;
                if (row.Complete && !before.Row.Complete) completed[row.Key] = clock + 2500;
            }
            next[row.Key] = (row, lastCountChange);
        }
        prior = next;
    }

    private string? SelectActiveMark(HuntProgressRow[] rows, uint targetNpcNameId) {
        string? recent = null;
        long latestChange = 0;
        foreach (var row in rows) {
            if (row.Complete) continue;
            if (targetNpcNameId != 0 && row.NpcNameId == targetNpcNameId) return row.Key;
            long lastCountChange = prior[row.Key].LastCountChange;
            if (lastCountChange > latestChange) {
                latestChange = lastCountChange;
                recent = row.Key;
            }
        }
        return recent;
    }
}

// WindowSystem reports OnClose one frame later for both programmatic and user
// closures. Observe its post-Draw IsOpen transition instead, and latch a user
// dismissal until the queued model interaction is applied (or manually reopened).
internal sealed class HuntProgressWindowGate {
    private long? dismissedRevision;
    internal bool Prepare(bool visible, long revision) => visible && dismissedRevision != revision;
    internal bool Observe(bool requestedOpen, bool actualOpen, long revision) {
        if (!requestedOpen || actualOpen) return false;
        dismissedRevision = revision;
        return true;
    }
}
