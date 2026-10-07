using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GillionsGameSync;

// Pure presentation state. No gameplay reads, transport, credentials or history.
internal sealed record PublicHealth(string Connection, string Character, string Hunts, string PartyFinder,
    string Fates, string Market, DateTime? LastSync, string Channel, string Version, string GameVersion) {
    private static string SafeVersion(string v) => Regex.IsMatch(v,@"^\d{1,8}(?:\.\d{1,8}){2,4}$") ? v : "unavailable";
    private static string SafeStatus(string s) => s is "Connected" or "Not connected" or "Connecting" or "Authorization expired or revoked"
        or "Account unavailable" or "Ready" or "Waiting for supported context" or "Temporarily unavailable" or "Gillions unavailable" or "Update required" ? s : "Temporarily unavailable";
    internal string SupportSummary() => $"Gillions Game Sync {SafeVersion(Version)}\nChannel: {(Channel is "Testing" or "Public" ? Channel : "unavailable")}\nConnection: {SafeStatus(Connection)}\nGame: {SafeVersion(GameVersion)}\nHunts: {SafeStatus(Hunts)}\nParty Finder: {SafeStatus(PartyFinder)}\nFATEs: {SafeStatus(Fates)}\nMarket: {SafeStatus(Market)}\nLast successful sync: {LastSync?.ToString("O") ?? "Not yet"}";
    internal static string ConnectionState(bool paired, bool pairing, string code) => pairing ? "Connecting"
        : code is "DEVICE_INVALID" or "DEVICE_REVOKED" or "TOKEN_INVALID" ? "Authorization expired or revoked"
        : code is "UNSUPPORTED_CLIENT" or "VERSION_UNSUPPORTED" or "CLIENT_VERSION_UNSUPPORTED" ? "Update required"
        : code is "ACCOUNT_DISABLED" or "TRIAL_EXPIRED" ? "Account unavailable"
        : !paired ? "Not connected" : "Connected";
}

internal static class PublicConnectionPresentation {
    internal static string PairingOrigin(string value) => SyncOrigin.TryNormalize(value,out var origin) ? origin : "";
    internal static string PairingUrl(string value) => PairingOrigin(value) is { Length: > 0 } origin ? origin+"/gillions-sync#pairing" : "";
    internal static string ActionMessage(string value, bool pairing = false) => value switch {
        "Connected. Gillions will load your selected character." or "Sync completed." or "Your supported data is already current."
            or "Disconnected. Saved history remains on this PC." or "Your Gillions trial has ended." or "This Gillions account is disabled."
            or "The connection or sync settings changed. Try again when ready." => value,
        "Gillions could not complete the request. Pending records were kept; please try again." => pairing
            ? "Pairing did not complete. Check the destination and connection, then create a fresh one-time code there and try again."
            : value,
        _ => ""
    };
}

internal sealed record HuntProgressRow(string Key, string Name, int Current, int Required) {
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
    private readonly Dictionary<string, HuntProgressRow> prior = new();
    private readonly Dictionary<string, (HuntProgressRow Row, long Until)> completed = new();
    private long allCompleteUntil;
    internal long InteractionRevision { get; private set; }
    internal bool Visible => visible;
    internal HuntProgressDisplay Display { get; private set; } = new("", "Hunt progress unavailable", []);
    internal void Close() { InteractionRevision++; visible = false; suppressed = true; manuallyOpened = false; }
    internal void Open() { InteractionRevision++; suppressed = false; manuallyOpened = true; visible = true; }
    internal void Invalidate(bool endTerritorySession = false) {
        prior.Clear(); completed.Clear(); allCompleteUntil = 0; visible = false;
        Display = new("", "Hunt progress unavailable", []);
        if (endTerritorySession) { InteractionRevision++; territory = 0; suppressed = false; manuallyOpened = false; }
    }
    internal void Update(bool show, uint currentTerritory, HuntProgressSnapshot sample, long clock) {
        if (territory != currentTerritory || enabled != show) {
            InteractionRevision++;
            territory = currentTerritory; enabled = show; suppressed = false; visible = false;
            manuallyOpened = false; prior.Clear(); completed.Clear(); allCompleteUntil = 0;
        }
        if (!show) { visible = false; return; }
        bool fresh = currentTerritory != 0 && sample.Territory == currentTerritory && clock < sample.ExpiresAt;
        if (!fresh || sample.Availability.Length != 0) {
            // Never leave stale numbers displayed as current during reconciliation.
            prior.Clear(); completed.Clear(); allCompleteUntil = 0;
            Display = new(sample.Territory == currentTerritory ? sample.Area : "", sample == HuntProgressSnapshot.Unavailable ? "Hunt progress unavailable"
                : fresh ? sample.Availability : "Hunt progress updating…", []);
            return;
        }
        var rows = sample.Rows;
        foreach (var row in rows) {
            if (row.Complete && prior.TryGetValue(row.Key, out var before) && !before.Complete)
                completed[row.Key] = (row, clock + 2500);
        }
        foreach (var key in completed.Where(p => clock >= p.Value.Until || !rows.Any(r => r.Key == p.Key && r.Complete)).Select(p => p.Key).ToArray()) completed.Remove(key);
        var incomplete = rows.Where(r => !r.Complete).ToArray();
        if (incomplete.Length > 0 && !suppressed) visible = true;
        if (incomplete.Length == 0 && sample.CoverageComplete && prior.Values.Any(r => !r.Complete))
            allCompleteUntil = clock + 2500;
        // Empty coverage at initial load is not a target-completion event.
        bool allComplete = incomplete.Length == 0 && sample.CoverageComplete && clock < allCompleteUntil;
        Display = new(sample.Area, allComplete ? "All current Hunt targets here are complete."
            : incomplete.Length == 0 && !sample.CoverageComplete ? "Hunt progress updating…" : "",
            incomplete.Concat(completed.Values.Select(p => p.Row)).ToArray());
        if (incomplete.Length == 0 && completed.Count == 0 && !allComplete && !manuallyOpened) visible = false;
        prior.Clear(); foreach (var row in rows) prior[row.Key] = row;
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
