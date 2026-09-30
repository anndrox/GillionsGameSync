using System;
using System.Collections.Generic;
using System.Linq;

namespace GillionsGameSync;

public sealed class ObservationSavePolicy {
    public static readonly TimeSpan FreshnessInterval = TimeSpan.FromSeconds(30);
    private DateTime nextFreshnessSave;
    private bool durable;
    private bool freshness;
    public void RequestDurable() => durable = true;
    public void Refresh() => freshness = true;
    public bool ShouldSave(DateTime now) => durable || freshness && now >= nextFreshnessSave;
    public void Saved(DateTime now) { durable = false; freshness = false; nextFreshnessSave = now.Add(FreshnessInterval); }
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
