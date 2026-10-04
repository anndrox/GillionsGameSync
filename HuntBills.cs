#if GILLIONS_TEST_BUILD || GILLIONS_PERSONAL_STATE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GillionsGameSync;

public sealed record HuntBillTarget(byte TargetIndex, uint TargetId, uint NpcNameId, uint MapId,
    uint PlaceNameId, uint FateId, byte RequiredKills, int ObservedKills, byte TargetType, byte Rank);
public sealed record HuntBillObservation(byte BillTypeId, string Category, byte Tier, uint OrderId,
    uint EventItemId, HuntBillTarget[] Targets, DateTime ObservedAtUtc, string GameVersion, string CollectorVersion) {
    public string ObservationId { get; init; } = Guid.NewGuid().ToString("N");
    public string? SourceEvidence { get; init; }
}

// Static catalog labels only; never part of retained observations or exports.
internal sealed class HuntTargetPresentation(Func<uint, string?> resolveName) {
    internal const int MaximumNames = 512;
    private readonly Dictionary<uint, string> names = new();
    internal string TargetLine(HuntBillTarget target) {
        if (!names.TryGetValue(target.NpcNameId, out var name)) {
            string? value;
            try { value = resolveName(target.NpcNameId); } catch (Exception) { value = null; }
            name = value is null ? "" : new string(value.Where(c => !char.IsControl(c)).Take(160).ToArray()).Trim();
            if (name.Length == 0) name = "Target name unavailable";
            if (names.Count < MaximumNames) names[target.NpcNameId] = name;
        }
        return $"  {name} — {target.ObservedKills}/{target.RequiredKills} kills (target ID {target.TargetId}; NPC name ID {target.NpcNameId})";
    }
}

// Managed admission/cadence only. No native pointers, requests or ownership inference.
internal sealed class HuntObservationSchedule {
    private DateTime nextReadUtc;
    private DateTime lastReadUtc;
    internal bool TryBegin(DateTime now, bool enabled, bool force = false) {
        if (!enabled || !PersonalObservationCompatibility.Utc(now)
            || (force ? now - lastReadUtc < TimeSpan.FromSeconds(1) : now < nextReadUtc)) return false;
        lastReadUtc = now; nextReadUtc = now.AddSeconds(3);
        return true;
    }
    internal void Reset() { nextReadUtc = default; lastReadUtc = default; }
}
// Only bridges the final transition of a bill positively corroborated in this
// session. No durable baseline, absent=>complete inference or cache-owner claim.
internal sealed class HuntSessionProgress {
    private string character = "";
    private readonly Dictionary<byte, HuntBillObservation> positive = new();
    internal void Reset() { character = ""; positive.Clear(); }
    internal void Bind(string key) { if (character != key) { Reset(); character = key; } }
    internal void Record(HuntBillObservation observation) {
        if (HuntBillRetentionPolicy.BillValid(observation)
            && observation.SourceEvidence == HuntObservationAdmission.KeyItemEvidence)
            positive[observation.BillTypeId] = observation;
    }
    internal bool MayReadFinal(byte index, DateTime now) => positive.TryGetValue(index, out var prior)
        && now >= prior.ObservedAtUtc && now - prior.ObservedAtUtc <= TimeSpan.FromSeconds(6)
        && prior.Targets.Any(t => t.ObservedKills < t.RequiredKills);
    internal bool CanObserveFinal(HuntBillObservation observation) =>
        HuntBillRetentionPolicy.BillValid(observation)
        && positive.TryGetValue(observation.BillTypeId, out var prior)
        && observation.OrderId == prior.OrderId && observation.EventItemId == prior.EventItemId
        && observation.ObservedAtUtc >= prior.ObservedAtUtc
        && observation.ObservedAtUtc - prior.ObservedAtUtc <= TimeSpan.FromSeconds(6)
        && prior.Targets.Any(t => t.ObservedKills < t.RequiredKills)
        && observation.Targets.Length == prior.Targets.Length
        && observation.Targets.All(t => t.ObservedKills == t.RequiredKills
            && prior.Targets.Any(p => p with { ObservedKills = t.ObservedKills } == t && p.ObservedKills <= t.ObservedKills));
}
internal static class HuntObservationAdmission {
    internal const string KeyItemEvidence = "loaded-key-item-and-obtained-flag-cache-unverified";
    internal static bool CanUseBillCache(byte index, int flags, bool keyItemsLoaded, uint expectedItem, bool present) =>
        index < 22 && (flags & ~((1 << 22) - 1)) == 0 && (flags & (1 << index)) != 0
        && keyItemsLoaded && expectedItem > 0 && present;
}
public sealed class RetainedHuntCharacter {
    public string LocalCharacterKey { get; set; } = "";
    public List<HuntBillObservation> Bills { get; set; } = [];
}
public sealed class HuntBillRetention {
    public int SchemaVersion { get; set; } = 1;
    public bool LocalRetentionEnabled { get; set; }
    public List<RetainedHuntCharacter> Characters { get; set; } = [];
    public bool CapacityReached { get; set; }
}
internal sealed class HuntBillRetentionPolicy(HuntBillRetention store) {
    internal bool LastSemanticChange { get; private set; }
    internal const int MaximumCharacters = 16;
    internal const int MaximumBytes = 256 * 1024;
    internal static string CharacterKey(ulong ownContentId) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(FormattableString.Invariant($"hunt-local:{ownContentId}")))).ToLowerInvariant();
    internal static bool BillValid(HuntBillObservation? bill) => bill is not null && bill.BillTypeId < 22
        && bill.Category is "daily" or "weekly" && bill.Tier is >= 1 and <= 3 && bill.OrderId > 0
        && bill.EventItemId > 0 && Guid.TryParseExact(bill.ObservationId, "N", out _)
        && (bill.SourceEvidence is null || bill.SourceEvidence == HuntObservationAdmission.KeyItemEvidence)
        && PersonalObservationCompatibility.Utc(bill.ObservedAtUtc)
        && PersonalObservationCompatibility.Metadata(bill.GameVersion) && PersonalObservationCompatibility.Metadata(bill.CollectorVersion)
        && bill.Targets is { Length: >= 1 and <= 5 }
        && bill.Targets.All(t => t is not null)
        && bill.Targets.Select(t => t.TargetIndex).Distinct().Count() == bill.Targets.Length
        && bill.Targets.All(t => t is not null && t.TargetIndex < 5 && t.TargetId > 0 && t.NpcNameId > 0
            && t.RequiredKills > 0 && t.ObservedKills >= 0 && t.ObservedKills <= t.RequiredKills);
    internal bool Supported {
        get {
            try {
                return store.SchemaVersion == 1 && store.Characters is not null && store.Characters.Count <= MaximumCharacters
                    && store.Characters.All(c => c is not null && PersonalObservationCompatibility.Key(c.LocalCharacterKey)
                        && c.Bills is not null && c.Bills.Count <= 22 && c.Bills.All(BillValid)
                        && c.Bills.Select(b => b.BillTypeId).Distinct().Count() == c.Bills.Count)
                    && store.Characters.Select(c => c.LocalCharacterKey).Distinct().Count() == store.Characters.Count
                    && JsonSerializer.SerializeToUtf8Bytes(store).Length <= MaximumBytes;
            } catch (Exception) { return false; }
        }
    }
    internal string Status => !Supported ? "Unsupported/oversized Hunt retention preserved unchanged; collection/export paused."
        : store.CapacityReached ? "Hunt retention full; prior observations preserved. New character observations paused."
        : store.LocalRetentionEnabled ? "Awaiting bounded loaded-cache observation; bill windows are not required. Retained state is not proof of current acceptance."
        : "Hunt retention off; prior state preserved. No Hunt uploads.";
    // Missing/cleared obtained flags do NOT delete bills. Complete cache ownership
    // and reset/absence cannot be established from these native fields alone.
    internal bool Observe(string characterKey, HuntBillObservation[] observations) {
        LastSemanticChange = false;
        if (!store.LocalRetentionEnabled || !Supported || !PersonalObservationCompatibility.Key(characterKey)
            || observations.Length is 0 or > 22 || !observations.All(BillValid)
            || observations.Select(b => b.BillTypeId).Distinct().Count() != observations.Length) return false;
        var current = store.Characters.SingleOrDefault(c => c.LocalCharacterKey == characterKey);
        var candidate = new RetainedHuntCharacter { LocalCharacterKey = characterKey, Bills = current?.Bills.ToList() ?? [] };
        bool changed = false;
        bool semantic = false;
        foreach (var observation in observations) {
            var prior = candidate.Bills.SingleOrDefault(b => b.BillTypeId == observation.BillTypeId);
            if (prior is not null && observation.ObservedAtUtc < prior.ObservedAtUtc) continue;
            var comparison = prior is null ? null : prior with { ObservedAtUtc = observation.ObservedAtUtc, ObservationId = observation.ObservationId };
            bool same = comparison is not null && JsonSerializer.Serialize(comparison) == JsonSerializer.Serialize(observation);
            // Refresh provenance at most once/minute; semantic changes save promptly.
            if (same
                && observation.ObservedAtUtc - prior!.ObservedAtUtc < TimeSpan.FromMinutes(1)) continue;
            semantic |= !same;
            if (prior is not null) candidate.Bills.Remove(prior);
            candidate.Bills.Add(observation); changed = true;
        }
        if (!changed) return false;
        var candidates = store.Characters.Where(c => c != current).Append(candidate).ToList();
        if (candidates.Count > MaximumCharacters || JsonSerializer.SerializeToUtf8Bytes(candidates).Length + 1024 > MaximumBytes) {
            bool signal = !store.CapacityReached; store.CapacityReached = true; return signal;
        }
        store.Characters = candidates;
        LastSemanticChange = semantic;
        return true;
    }
    internal string PreparePrivateExport(string characterKey) {
        if (!Supported || !store.LocalRetentionEnabled || !PersonalObservationCompatibility.Key(characterKey))
            throw new InvalidOperationException("Retention off or unsupported.");
        var current = store.Characters.SingleOrDefault(c => c.LocalCharacterKey == characterKey);
        if (current is null || current.Bills.Count == 0) throw new InvalidOperationException("No retained Hunt observations for this character.");
        return JsonSerializer.Serialize(new {
            schemaVersion = 1, collectorSchema = "hunt-bills-v1", uploadState = "local-only-no-server-contract",
            source = "naturally-loaded-mob-hunt-client-cache", completeness = "positive-observations-only",
            characterAssociation = "active-character-context-cache-ownership-unverified",
            resetAtUtc = (DateTime?)null, resetApplicability = "unavailable", bills = current.Bills.OrderBy(b => b.BillTypeId).Select(b => new {
                b.ObservationId, b.BillTypeId, b.Category, b.Tier, b.OrderId, b.EventItemId, b.ObservedAtUtc, b.GameVersion, b.CollectorVersion, b.SourceEvidence,
                acceptance = "obtained-flag-observed-not-current-acceptance-proof", targets = b.Targets.Select(t => new {
                    t.TargetIndex, t.TargetId, t.NpcNameId, t.MapId, t.PlaceNameId, t.FateId, t.RequiredKills, t.ObservedKills,
                    completed = t.ObservedKills == t.RequiredKills, t.TargetType, t.Rank
                })
            })
        }, PersonalObservationCompatibility.Json);
    }
}
#endif
