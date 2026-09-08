using System;

namespace GillionsGameSync;

public sealed record PluginWindowModel(string Connection, string Status, string? Warning, bool CanSync) {
    public static PluginWindowModel Create(bool paired, bool needsPairing, bool loggedIn, bool automatic, bool busy, bool storagePaused, bool hadGap, string blockedCode) {
        var warnings = new System.Collections.Generic.List<string>();
        if (blockedCode == "TRIAL_EXPIRED") warnings.Add("Your Gillions trial has ended. Check your account on the website.");
        if (blockedCode == "ACCOUNT_DISABLED") warnings.Add("This Gillions account is disabled. Check your account on the website.");
        if (storagePaused) warnings.Add("Offline storage is full. Saved records are safe, but new event recording is paused. Recording resumes when acknowledged uploads make room; missed events cannot be recovered. Older pairings still count toward the limit.");
        if (needsPairing) warnings.Add("Pair this device to continue. Older local records with unverified ownership stay preserved and inactive. Your website history is unchanged.");
        if (hadGap && !storagePaused) warnings.Add("Recording resumed after an offline-storage gap. Events missed during that gap cannot be recovered.");
        var warning = warnings.Count == 0 ? null : string.Join("\n\n", warnings);
        var status = !paired ? "Enter a one-time pairing code from Gillions." : !loggedIn ? "Log into a character to sync."
            : busy ? "Syncingâ€¦" : automatic ? "Automatic sync is on." : "Automatic sync is off. You can still sync manually.";
        return new(paired ? "Connected" : "Not connected", status, warning, paired && loggedIn && !busy);
    }
}

public sealed record PluginUiSnapshot(PluginWindowModel Model, bool Paired, bool Pairing, bool Automatic, bool ItemLinks,
    string Origin, DateTime? LastSync, string Message, string ReadChangelogVersion, bool RetainerSupported,
    string Availability, EvidenceBudgetUsage? Budget, bool Recording, DateTime RecordingUntil, string[] Diagnostics) {
    public static readonly PluginUiSnapshot Empty = new(PluginWindowModel.Create(false, false, false, true, false, false, false, ""),
        false, false, true, true, "", null, "", "", false, "", null, false, default, []);
}
