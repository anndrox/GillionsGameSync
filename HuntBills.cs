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
    internal const int MaximumCharacters = 16;
    internal const int MaximumBytes = 256 * 1024;
    internal static string CharacterKey(ulong ownContentId) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(FormattableString.Invariant($"hunt-local:{ownContentId}")))).ToLowerInvariant();
    internal static bool BillValid(HuntBillObservation? bill) => bill is not null && bill.BillTypeId < 22
        && bill.Category is "daily" or "weekly" && bill.Tier is >= 1 and <= 3 && bill.OrderId > 0
        && bill.EventItemId > 0 && Guid.TryParseExact(bill.ObservationId, "N", out _)
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
        : store.LocalRetentionEnabled ? "Awaiting Hunt Bill interface observations; retained state is not proof of current acceptance."
        : "Hunt retention off; prior state preserved. No Hunt uploads.";
    // Missing/cleared obtained flags do NOT delete bills. Complete cache ownership
    // and reset/absence cannot be established from these native fields alone.
    internal bool Observe(string characterKey, HuntBillObservation[] observations) {
        if (!store.LocalRetentionEnabled || !Supported || !PersonalObservationCompatibility.Key(characterKey)
            || observations.Length is 0 or > 22 || !observations.All(BillValid)
            || observations.Select(b => b.BillTypeId).Distinct().Count() != observations.Length) return false;
        var current = store.Characters.SingleOrDefault(c => c.LocalCharacterKey == characterKey);
        var candidate = new RetainedHuntCharacter { LocalCharacterKey = characterKey, Bills = current?.Bills.ToList() ?? [] };
        bool changed = false;
        foreach (var observation in observations) {
            var prior = candidate.Bills.SingleOrDefault(b => b.BillTypeId == observation.BillTypeId);
            if (prior is not null && observation.ObservedAtUtc < prior.ObservedAtUtc) continue;
            var comparison = prior is null ? null : prior with { ObservedAtUtc = observation.ObservedAtUtc, ObservationId = observation.ObservationId };
            // Refresh provenance at most once/minute; semantic changes save promptly.
            if (comparison is not null && JsonSerializer.Serialize(comparison) == JsonSerializer.Serialize(observation)
                && observation.ObservedAtUtc - prior!.ObservedAtUtc < TimeSpan.FromMinutes(1)) continue;
            if (prior is not null) candidate.Bills.Remove(prior);
            candidate.Bills.Add(observation); changed = true;
        }
        if (!changed) return false;
        var candidates = store.Characters.Where(c => c != current).Append(candidate).ToList();
        if (candidates.Count > MaximumCharacters || JsonSerializer.SerializeToUtf8Bytes(candidates).Length + 1024 > MaximumBytes) {
            bool signal = !store.CapacityReached; store.CapacityReached = true; return signal;
        }
        store.Characters = candidates;
        return true;
    }
    internal string PreparePrivateExport(string characterKey) {
        if (!Supported || !store.LocalRetentionEnabled || !PersonalObservationCompatibility.Key(characterKey))
            throw new InvalidOperationException("Retention off or unsupported.");
        var current = store.Characters.SingleOrDefault(c => c.LocalCharacterKey == characterKey);
        if (current is null || current.Bills.Count == 0) throw new InvalidOperationException("No retained Hunt observations for this character.");
        return JsonSerializer.Serialize(new {
            schemaVersion = 1, collectorSchema = "hunt-bills-v1", uploadState = "local-only-no-server-contract",
            source = "naturally-visible-mob-hunt-client-cache", completeness = "positive-observations-only",
            characterAssociation = "active-character-at-interface-cache-ownership-unverified",
            resetAtUtc = (DateTime?)null, resetApplicability = "unavailable", bills = current.Bills.OrderBy(b => b.BillTypeId).Select(b => new {
                b.ObservationId, b.BillTypeId, b.Category, b.Tier, b.OrderId, b.EventItemId, b.ObservedAtUtc, b.GameVersion, b.CollectorVersion,
                acceptance = "obtained-flag-observed-not-current-acceptance-proof", targets = b.Targets.Select(t => new {
                    t.TargetIndex, t.TargetId, t.NpcNameId, t.MapId, t.PlaceNameId, t.FateId, t.RequiredKills, t.ObservedKills,
                    completed = t.ObservedKills == t.RequiredKills, t.TargetType, t.Rank
                })
            })
        }, PersonalObservationCompatibility.Json);
    }
}
#endif
