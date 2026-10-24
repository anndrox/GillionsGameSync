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
    public void Reset() { Due = DateTime.MaxValue; pendingSince = DateTime.MaxValue; lastDispatch = DateTime.MinValue; }
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
