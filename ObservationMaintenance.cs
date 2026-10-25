using System;
using System.Collections.Generic;
using System.Linq;

namespace GillionsGameSync;

public sealed class ObservationSavePolicy {
    // A freshness-only save rewrites the whole configuration on the framework
    // thread (measured ~19 ms median for a 1.5 MB config), so timestamp-only
    // refreshes persist rarely. Durable changes still save on the next tick.
    public static readonly TimeSpan FreshnessInterval = TimeSpan.FromMinutes(5);
    private DateTime nextFreshnessSave, retryAfter;
    private bool durable;
    private bool freshness;
    private int failures;
    public void RequestDurable() => durable = true;
    public void Refresh() => freshness = true;
    public bool ShouldSave(DateTime now, bool ignoreBackoff = false) => (ignoreBackoff || now >= retryAfter) && (durable || freshness && now >= nextFreshnessSave);
    public void Saved(DateTime now) { durable = false; freshness = false; failures = 0; retryAfter = DateTime.MinValue; nextFreshnessSave = now.Add(FreshnessInterval); }
    // A failed write keeps its pending changes but is not re-serialized every
    // frame. Returns true for the first failure of a run, so it is logged once.
    public bool Failed(DateTime now) { retryAfter = now.AddSeconds(Math.Min(60, 5 << Math.Min(failures, 4))); return failures++ == 0; }
}

// Inventory changes arrive in bursts while gathering or crafting. Upload at
// most once per window, and never later than one window after the first unsent
// change, so the website stays within about ten seconds of the game.
public sealed class InventoryUploadSchedule {
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(10);
    private DateTime lastDispatch = DateTime.MinValue, pendingSince = DateTime.MaxValue;
    public DateTime Due { get; private set; } = DateTime.MaxValue;
    public void Changed(DateTime now, TimeSpan settle) {
        // A wall clock moved backwards must not hold uploads back.
        if (lastDispatch > now) lastDispatch = DateTime.MinValue;
        if (pendingSince != DateTime.MaxValue && pendingSince > now) pendingSince = now;
        if (pendingSince == DateTime.MaxValue) pendingSince = now;
        var latest = pendingSince.Add(Window);
        var target = now.Add(settle) < latest ? now.Add(settle) : latest;
        var earliest = lastDispatch == DateTime.MinValue ? DateTime.MinValue : lastDispatch.Add(Window);
        Due = target > earliest ? target : earliest;
    }
    public bool TryDispatch(DateTime now) {
        if (now < Due) return false;
        Due = DateTime.MaxValue; pendingSince = DateTime.MaxValue; lastDispatch = now;
        return true;
    }
    // A dispatched upload that could not run (loading screen, not loaded, failure)
    // keeps its change pending instead of waiting for the next inventory event.
    public void Requeue(DateTime now, TimeSpan delay) {
        if (pendingSince == DateTime.MaxValue || pendingSince > now) pendingSince = now;
        var retry = now.Add(delay);
        if (Due == DateTime.MaxValue || retry < Due) Due = retry;
    }
    public void Reset() { Due = DateTime.MaxValue; pendingSince = DateTime.MaxValue; lastDispatch = DateTime.MinValue; }
}

// A refused travel upload (an unadmitted native build, a missing grant) would
// otherwise be retried on every 15 s presence acknowledgement. Wait five
// minutes; a session change, or a real change of the travel decision, clears
// the wait. A lease lapse (failed presence) is not a decision change.
public sealed class TravelRefusalBackoff {
    public static readonly TimeSpan Delay = TimeSpan.FromMinutes(5);
    private DateTime until = DateTime.MinValue;
    private string? decision;
    // Timeouts and redirects are transient, not refusals.
    public static bool Counts(int status) => status is not (307 or 308 or 408 or 425);
    public void Refused(DateTime now, string? knownDecision) { until = now.Add(Delay); decision = knownDecision; }
    public void DecisionChanged(string? knownDecision) { if (knownDecision is not null && knownDecision != decision) Reset(); }
    public bool Waiting(DateTime now) {
        if (until > now.Add(Delay)) until = now; // a wall clock moved backwards never extends the wait
        return now < until;
    }
    public void Reset() { until = DateTime.MinValue; decision = null; }
}

// An unlinked character refuses every ordinary upload until it is linked, so
// the 10 s inventory retry and the 30 s resource rotation would repeat the
// refusal about 8 times a minute. Wait five minutes; a character change, login
// or an accepted presence/sync (which proves the link) clears the wait. Local
// observation continues, and a manual Sync now always runs.
public sealed class CharacterRefusalBackoff {
    public static readonly TimeSpan Delay = TimeSpan.FromMinutes(5);
    private DateTime until = DateTime.MinValue;
    // Only "not linked" blocks every resource; "paired to another character"
    // concerns presence and Retainer uploads, not the ordinary snapshots.
    public static bool Counts(string code) => code == SiteRefusalPolicy.CharacterNotLinked;
    public void Refused(DateTime now) => until = now.Add(Delay);
    public bool Waiting(DateTime now) {
        if (until > now.Add(Delay)) until = now; // a wall clock moved backwards never extends the wait
        return now < until;
    }
    public void Reset() => until = DateTime.MinValue;
}

public static class TransientEvidencePolicy {
    public const int MaximumEntries = 256;
    public static void Prune<T>(List<T> entries, DateTime now, TimeSpan horizon, Func<T, DateTime> observed) {
        entries.RemoveAll(entry => observed(entry) < now.Subtract(horizon));
        if (entries.Count > MaximumEntries) entries.RemoveRange(0, entries.Count - MaximumEntries);
    }
    public static void Prune<TKey, TValue>(Dictionary<TKey, TValue> entries, DateTime now, TimeSpan horizon, Func<TValue, DateTime> observed) where TKey : notnull {
        foreach (var key in entries.Where(entry => observed(entry.Value) < now.Subtract(horizon)).Select(entry => entry.Key).ToArray()) entries.Remove(key);
        foreach (var key in entries.OrderBy(entry => observed(entry.Value)).Take(Math.Max(0, entries.Count - MaximumEntries)).Select(entry => entry.Key).ToArray()) entries.Remove(key);
    }
}

public sealed class RetainerResultViewCache {
    private string? character;
    private RetainerVentureResultRead? last;
    public bool Changed(string contentId, RetainerVentureResultRead? read) {
        if (read is null) { Clear(); return false; }
        if (character == contentId && last is not null && last.RetainerId == read.RetainerId && last.VentureId == read.VentureId
            && last.VentureCompleteUnix == read.VentureCompleteUnix && last.AwardedExperience == read.AwardedExperience
            && last.Items.SequenceEqual(read.Items)) return false;
        character = contentId;
        last = read with { Items = read.Items.ToArray() };
        return true;
    }
    public void Clear() { character = null; last = null; }
}
