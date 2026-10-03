#if GILLIONS_TEST_BUILD || GILLIONS_DASHBOARD_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GillionsGameSync;

// Finite observed facts, not Dashboard tasks, configuration, or a generic event bus.
public sealed record DashboardValue(uint Id, uint RelatedId = 0, int? Progress = null,
    int? Limit = null, int? Remaining = null, bool? Completed = null, bool? Available = null) {
    [Newtonsoft.Json.JsonExtensionData]
    public Dictionary<string, Newtonsoft.Json.Linq.JToken>? UnsupportedMembers { get; set; }
}
public sealed record DashboardObservation(string System, uint ScopeId, DateTime ObservedAtUtc,
    DateTime? NextAtUtc, DashboardValue[] Values, string GameVersion, string NativeVersion, string CollectorVersion) {
    [Newtonsoft.Json.JsonExtensionData]
    public Dictionary<string, Newtonsoft.Json.Linq.JToken>? UnsupportedMembers { get; set; }
}
public sealed class DashboardCharacter {
    public string LocalCharacterKey { get; set; } = "";
    public List<DashboardObservation> Observations { get; set; } = [];
    [Newtonsoft.Json.JsonExtensionData]
    public Dictionary<string, Newtonsoft.Json.Linq.JToken>? UnsupportedMembers { get; set; }
}
public sealed class DashboardRetention {
    public int SchemaVersion { get; set; } = 1;
    public bool LocalRetentionEnabled { get; set; }
    public bool CapacityReached { get; set; }
    public List<DashboardCharacter> Characters { get; set; } = [];
    [Newtonsoft.Json.JsonExtensionData]
    public Dictionary<string, Newtonsoft.Json.Linq.JToken>? UnsupportedMembers { get; set; }
}

internal static class DashboardSources {
    // The global allowance fact and selected-client details have independent
    // bounds. An unavailable/malformed client must not suppress a valid global
    // fact, and a malformed global count must not poison valid client details.
    internal static DashboardObservation[] AdmitCustomDeliveries(DashboardObservation global, DashboardObservation? client) =>
        new[] { global.System == "custom-deliveries-global" ? global : null,
            client?.System == "custom-deliveries-client" ? client : null }
        .Where(r => r is not null && DashboardRetentionPolicy.Valid(r)).Select(r => r!).ToArray();
    internal static (string Source, string Cadence)? Definition(string system) => system switch {
        "roulette-reward" => ("InstanceContent.IsRouletteComplete/natural-ContentsFinder", "daily"),
        "custom-deliveries-global" => ("SatisfactionSupplyManager/natural-SatisfactionSupply", "weekly"),
        "custom-deliveries-client" => ("AgentSatisfactionSupply.NpcData/natural-SatisfactionSupply", "weekly-and-rank-progression"),
        "challenge-log" => ("ContentsNote.Loaded/natural-ContentsNote", "weekly"),
        "weekly-tomestones" => ("InventoryManager/loaded-Currency", "weekly"),
        "wondrous-tails" => ("PlayerState/held-journal", "journal-expiration"),
        "leve-allowance" => ("QuestManager/natural-ContentsInfo", "allowance-regeneration"),
        "society-allowance" => ("QuestManager/natural-ContentsInfo", "daily"),
        "map-availability" => ("UIState.cached-NextMapAllowanceTimestamp/natural-ContentsInfo", "next-availability"),
        "squadron-mission" => ("PlayerState/natural-ContentsInfo", "expected-completion"),
        "squadron-training" => ("PlayerState/natural-ContentsInfo", "expected-completion"),
        "frontline-weekly" => ("PvPProfile.IsLoaded/weekly-counters", "weekly"),
        "rival-wings-weekly" => ("PvPProfile.IsLoaded/weekly-counters", "weekly"),
        "doman-enclave-weekly" => ("DomanEnclaveManager.IsLoaded/weekly-donation-state", "weekly"),
        _ => null
    };
    internal static bool Counter(DashboardValue v, int maximum) => v.Progress is >= 0 && v.Limit is > 0
        && v.Limit <= maximum && v.Progress <= v.Limit && v.Remaining == v.Limit - v.Progress
        && v.Completed is null && v.Available is null && v.RelatedId == 0;
    internal static bool ValueValid(string system, DashboardValue v) => system switch {
        "roulette-reward" => v.Id is > 0 and <= 255 && v.Completed.HasValue && v.Available is null
            && v.Progress is null && v.Limit is null && v.Remaining is null && v.RelatedId == 0,
        "custom-deliveries-global" => v.Id == 0 && Counter(v, 12) && v.Limit == 12,
        "custom-deliveries-client" => v.Id switch {
            0 => Counter(v, 6), // selected client's used/remaining deliveries
            1 => (v.Progress is null && v.Limit is null || v.Progress is >= 0 && v.Limit is > 0 and <= 65535 && v.Progress <= v.Limit)
                && v.Remaining is null && v.Completed is null && v.Available is null && v.RelatedId == 0,
            2 => v.Progress is >= 1 and <= 5 && v.Limit == 5 && v.Remaining is null
                && v.Completed is null && v.Available is null && v.RelatedId == 0,
            _ => false
        },
        "challenge-log" => v.Id is >= 1 and <= 104 && v.Completed.HasValue && v.Available is null
            && v.Progress is null && v.Limit is null && v.Remaining is null && v.RelatedId == 0,
        "weekly-tomestones" => v.Id > 0 && Counter(v, 10000),
        "doman-enclave-weekly" => v.Id == 0 && v.Available.HasValue && Counter(v with { Available = null }, 65535),
        "frontline-weekly" or "rival-wings-weekly" => v.Id < (system == "frontline-weekly" ? 4 : 2)
            && v.Progress is >= 0 and <= 10000 && v.Limit is null && v.Remaining is null
            && v.Completed is null && v.Available is null && v.RelatedId == 0,
        "wondrous-tails" => v.Id switch {
            0 or 1 => v.RelatedId == 0 && v.Progress is >= 0 and <= 9 && v.Limit == 9
                && v.Remaining is null && v.Completed is null && v.Available == true,
            >= 2 and <= 17 => v.RelatedId is > 0 and <= 255 && v.Progress is >= 0 and <= 2
                && v.Completed == (v.Progress > 0) && v.Limit is null && v.Remaining is null && v.Available is null,
            _ => false
        },
        "leve-allowance" or "society-allowance" => v.Id == 0 && v.Progress is null && v.Remaining is >= 0
            && v.Limit == (system == "leve-allowance" ? 100 : 12) && v.Remaining <= v.Limit
            && v.Completed is null && v.Available is null && v.RelatedId == 0,
        "map-availability" or "squadron-mission" or "squadron-training" => v.Id == 0 && v.RelatedId == 0
            && v.Progress is null && v.Limit is null && v.Remaining is null && v.Completed is null && v.Available is null,
        _ => false
    };
}

internal enum DashboardDeliveryReadStatus {
    Awaiting, Reading, AgentUnavailable, ManagerUnavailable, InterfaceClosed,
    NpcInvalid, NpcUninitialized, AddonNotUpdated, ManagerInitializing, ManagerUninitialized,
    ResetUnavailable, ClientCatalogMismatch, ClientAllowanceMismatch, ClientRankMismatch,
    ClientCounterMismatch, ObservedBoth, ObservedGlobal, ObservedClient, InvalidFacts, ReadFailed
}

// Session-local, finite rejection reasons only: no NPC/player identifiers,
// private values, exception text, history or configuration persistence.
internal sealed class DashboardDeliveryDiagnostics {
    internal DateTime? LastAttemptUtc { get; private set; }
    internal DashboardDeliveryReadStatus Status { get; private set; }
    internal void Reset() { LastAttemptUtc = null; Status = DashboardDeliveryReadStatus.Awaiting; }
    internal void Record(DateTime now, DashboardDeliveryReadStatus status) { LastAttemptUtc = now; Status = status; }
    internal string Text => $"Custom Delivery read: {Status}; last attempt UTC: {LastAttemptUtc?.ToString("u") ?? "not attempted"}. Read status only; retained rows and freshness are separate.";
}

internal sealed class DashboardSchedule {
    internal const int GroupCount = 8;
    private DateTime next;
    private int roundRobin;
    private bool prioritize = true;
    private readonly HashSet<int> pending = [];
    internal void Notice(int group) { if (group is >= 0 and < GroupCount) pending.Add(group); }
    internal void Reset() { next = default; roundRobin = 0; prioritize = true; pending.Clear(); }
    internal int? Begin(DateTime now, bool enabled) {
        if (!enabled || !PersonalObservationCompatibility.Utc(now) || now < next) return null;
        next = now.AddSeconds(5);
        // Alternate event priority with round-robin so UI events cannot starve
        // cached sources. At most one bounded group per five seconds.
        bool eventTurn = prioritize; prioritize = !prioritize;
        if (eventTurn && pending.Count > 0) { var priority = pending.Min(); pending.Remove(priority); return priority; }
        var group = roundRobin; roundRobin = (roundRobin + 1) % GroupCount; pending.Remove(group);
        return group;
    }
}

internal sealed class DashboardRetentionPolicy(DashboardRetention store) {
    internal const int MaximumCharacters = 16, MaximumGroups = 24, MaximumBytes = 384 * 1024;
    internal static string CharacterKey(ulong id) => Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes(FormattableString.Invariant($"gillions-dashboard-local-v1:{id}"))));
    internal static string Identity(DashboardObservation row) => $"{row.System}:{row.ScopeId}";
    internal static bool Equivalent(DashboardObservation a, DashboardObservation b) =>
        JsonSerializer.Serialize(a with { ObservedAtUtc = b.ObservedAtUtc }) == JsonSerializer.Serialize(b);
    internal static bool Valid(DashboardObservation? row) {
        if (row is null || row.UnsupportedMembers is { Count: > 0 } || DashboardSources.Definition(row.System) is null
            || !PersonalObservationCompatibility.Utc(row.ObservedAtUtc)
            || !PersonalObservationCompatibility.Metadata(row.GameVersion) || !PersonalObservationCompatibility.Metadata(row.NativeVersion)
            || !PersonalObservationCompatibility.Metadata(row.CollectorVersion)
            || row.Values is not { Length: >= 1 and <= 104 } || row.Values.Any(v => v is null || v.UnsupportedMembers is { Count: > 0 })
            || row.Values.Select(v => v.Id).Distinct().Count() != row.Values.Length
            || !row.Values.All(v => DashboardSources.ValueValid(row.System, v))) return false;
        if (row.System == "custom-deliveries-client" ? row.ScopeId is < 1 or > 12 : row.ScopeId != 0) return false;
        int count = row.System switch { "custom-deliveries-client" => 3, "wondrous-tails" => 18,
            "frontline-weekly" => 4, "rival-wings-weekly" => 2,
            "roulette-reward" or "challenge-log" => row.Values.Length, _ => 1 };
        if (row.Values.Length != count) return false;
        if (row.System == "custom-deliveries-client") {
            if (!row.Values.Select(v => v.Id).Order().SequenceEqual(new uint[] { 0, 1, 2 })) return false;
            if (row.Values.Single(v => v.Id == 1).Progress is null && row.Values.Single(v => v.Id == 2).Progress != 5) return false;
        }
        if (row.System == "wondrous-tails" && !row.Values.Select(v => v.Id).Order().SequenceEqual(Enumerable.Range(0, 18).Select(v => (uint)v))) return false;
        if (row.System is "frontline-weekly" or "rival-wings-weekly") {
            if (!row.Values.Select(v => v.Id).Order().SequenceEqual(Enumerable.Range(0, count).Select(v => (uint)v))) return false;
            int total = row.Values.Single(v => v.Id == 0).Progress!.Value;
            if (row.Values.Where(v => v.Id > 0).Sum(v => v.Progress!.Value) > total) return false;
        }
        bool requiresTime = row.System is "custom-deliveries-global" or "custom-deliveries-client" or "wondrous-tails"
            or "leve-allowance" or "map-availability" or "squadron-mission" or "squadron-training";
        if (requiresTime && row.NextAtUtc is null) return false;
        if (row.System is "roulette-reward" or "weekly-tomestones" or "society-allowance" or "frontline-weekly" or "rival-wings-weekly" or "doman-enclave-weekly" && row.NextAtUtc is not null) return false;
        return row.NextAtUtc is null || PersonalObservationCompatibility.Utc(row.NextAtUtc.Value)
            && row.NextAtUtc > row.ObservedAtUtc && row.NextAtUtc - row.ObservedAtUtc <= TimeSpan.FromDays(15);
    }
    internal bool Supported {
        get {
            try { return store.SchemaVersion == 1 && store.UnsupportedMembers is not { Count: > 0 }
                && store.Characters is not null && store.Characters.Count <= MaximumCharacters
                && store.Characters.All(c => c is not null && c.UnsupportedMembers is not { Count: > 0 } && PersonalObservationCompatibility.Key(c.LocalCharacterKey)
                    && c.Observations is not null && c.Observations.Count <= MaximumGroups && c.Observations.All(Valid)
                    && c.Observations.Select(Identity).Distinct().Count() == c.Observations.Count)
                && store.Characters.Select(c => c.LocalCharacterKey).Distinct().Count() == store.Characters.Count
                && JsonSerializer.SerializeToUtf8Bytes(store).Length <= MaximumBytes;
            } catch (Exception) { return false; }
        }
    }
    internal bool Observe(string key, DashboardObservation[] observations) {
        if (!store.LocalRetentionEnabled || !Supported || !PersonalObservationCompatibility.Key(key)
            || observations.Length is 0 or > MaximumGroups || !observations.All(Valid)
            || observations.Select(Identity).Distinct().Count() != observations.Length) return false;
        var priorCharacter = store.Characters.SingleOrDefault(c => c.LocalCharacterKey == key);
        var candidate = new DashboardCharacter { LocalCharacterKey = key, Observations = priorCharacter?.Observations.ToList() ?? [] };
        bool changed = false;
        foreach (var row in observations) {
            var old = candidate.Observations.SingleOrDefault(o => Identity(o) == Identity(row));
            if (old is not null && row.ObservedAtUtc <= old.ObservedAtUtc) continue;
            if (old is not null && Equivalent(old, row)
                && row.ObservedAtUtc - old.ObservedAtUtc < TimeSpan.FromMinutes(1)) continue;
            if (old is not null) candidate.Observations.Remove(old);
            candidate.Observations.Add(row); changed = true;
        }
        if (!changed) return false;
        var candidates = store.Characters.Where(c => c != priorCharacter).Append(candidate).ToList();
        // Test the actual retained envelope, not only its inner array.
        var envelope = new DashboardRetention { LocalRetentionEnabled = true, CapacityReached = store.CapacityReached, Characters = candidates };
        if (candidates.Count > MaximumCharacters || candidate.Observations.Count > MaximumGroups
            || JsonSerializer.SerializeToUtf8Bytes(envelope).Length > MaximumBytes - 64) {
            bool first = !store.CapacityReached; store.CapacityReached = true; return first;
        }
        store.Characters = candidates; return true;
    }
    internal DashboardObservation[] Rows(string key) => Supported
        ? store.Characters.SingleOrDefault(c => c.LocalCharacterKey == key)?.Observations.ToArray() ?? [] : [];
    internal static string Freshness(DashboardObservation row, DateTime now, bool currentSessionObserved) =>
        !currentSessionObserved || !PersonalObservationCompatibility.Utc(now) || now < row.ObservedAtUtc
        || now - row.ObservedAtUtc > TimeSpan.FromMinutes(2) || row.NextAtUtc is { } next && now >= next
            ? "STALE" : "OBSERVED";
    internal string Export(string key, DateTime now, IReadOnlySet<string> currentSession) {
        if (!store.LocalRetentionEnabled || !Supported || !PersonalObservationCompatibility.Key(key)
            || !PersonalObservationCompatibility.Utc(now)) throw new InvalidOperationException("Off/unavailable/unsupported.");
        var rows = Rows(key); if (rows.Length == 0) throw new InvalidOperationException("No observations.");
        return JsonSerializer.Serialize(new {
            schemaVersion = 1, collectorSchema = "dashboard-facts-v1", uploadState = "local-only-no-server-contract",
            privacy = "private-personal-activity", generatedAtUtc = now,
            characterAssociation = "active-character-context-native-cache-ownership-unverified",
            automaticChecklistCompletion = "not-authorized-by-this-experimental-export",
            observations = rows.OrderBy(Identity).Select(r => new {
                r.System, r.ScopeId, r.ObservedAtUtc, r.NextAtUtc, r.GameVersion, r.NativeVersion, r.CollectorVersion,
                source = DashboardSources.Definition(r.System)!.Value.Source,
                cadence = DashboardSources.Definition(r.System)!.Value.Cadence,
                freshness = Freshness(r, now, currentSession.Contains(Identity(r))),
                resetApplicability = r.NextAtUtc is null ? "unavailable" : "native-next-boundary-only-not-server-response-proof",
                values = r.Values.Select(v => new { v.Id, v.RelatedId, v.Progress, v.Limit, v.Remaining, v.Completed, v.Available })
            })
        }, PersonalObservationCompatibility.Json);
    }
}
#endif
