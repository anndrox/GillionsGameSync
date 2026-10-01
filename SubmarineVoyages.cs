#if GILLIONS_TEST_BUILD || GILLIONS_SUBMARINE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GillionsGameSync;

public sealed record SubmarineParts(ushort Hull, ushort Stern, ushort Bow, ushort Bridge);
public sealed record SubmarineStats(ushort SurveillanceBase, ushort RetrievalBase, ushort SpeedBase,
    ushort RangeBase, ushort FavorBase, ushort SurveillanceBonus, ushort RetrievalBonus,
    ushort SpeedBonus, ushort RangeBonus, ushort FavorBonus, ushort LogSpeed);
public sealed record SubmarineBuild(byte Rank, SubmarineParts Parts, SubmarineStats Stats);
public sealed record SubmarineReward(uint ItemId, uint Quantity, bool? Hq);
public sealed record SubmarineSectorResult(byte SectorId, uint Experience, byte? UnlockedSectorId,
    bool FirstExploration, bool AdditionalSubmarineUnlocked, bool DoubleDip, SubmarineReward[] Rewards);
public sealed record SubmarineResult(string RewardAssociation, SubmarineSectorResult[] Sectors,
    SubmarineReward[] VoyageRewards, uint? TotalExperience, string[] Limitations);
public sealed record SubmarineSnapshot(string LocalSubmarineKey, byte Slot, string Name, uint RegisteredAtUnix,
    SubmarineBuild Build, uint CurrentExperience, uint NextRankExperience, uint ExpectedReturnUnix,
    byte[] CurrentRoute, byte[]? PlannedRoute, DateTime ObservedAtUtc, string GameVersion, string CollectorVersion);

public sealed class SubmarineCurrentObservation {
    public SubmarineSnapshot Snapshot { get; set; } = null!;
    public string? AnchoredVoyageKey { get; set; }
}
public sealed class RetainedSubmarineVoyage {
    public string LocalVoyageKey { get; set; } = "";
    public string ObservationId { get; set; } = Guid.NewGuid().ToString("N");
    public string LocalSubmarineKey { get; set; } = "";
    public uint? ExpectedReturnUnix { get; set; }
    public byte[] OrderedRoute { get; set; } = [];
    public SubmarineBuild? VoyageBuild { get; set; }
    public string BuildEvidence { get; set; } = "unavailable-not-observed-in-flight";
    public SubmarineBuild? ResultTimeBuild { get; set; }
    public DateTime FirstObservedAtUtc { get; set; }
    public DateTime? ResultsObservedAtUtc { get; set; }
    public string GameVersion { get; set; } = "unavailable";
    public string CollectorVersion { get; set; } = "unavailable";
    public string? ResultGameVersion { get; set; }
    public string? ResultCollectorVersion { get; set; }
    public SubmarineResult? Result { get; set; }
    public bool LinkedToVoyage { get; set; }
    public bool ConflictingObservation { get; set; }
    public bool ContributionConsentAtResult { get; set; }
}
public sealed class SubmarineVoyageRetention {
    public int SchemaVersion { get; set; } = 1;
    public bool LocalRetentionEnabled { get; set; }
    public bool CommunityContributionEnabled { get; set; }
    public List<SubmarineCurrentObservation> Current { get; set; } = [];
    public List<RetainedSubmarineVoyage> Voyages { get; set; } = [];
    public bool CapacityReached { get; set; }
}

// Pure managed admission policy. No network, third-party plugin, native action,
// credential, account identifier or inferred departure time exists here.
internal sealed class SubmarineVoyageRetentionPolicy(SubmarineVoyageRetention store) {
    internal const int MaximumRecords = 400;
    internal const int MaximumCurrentSnapshots = 32;
    internal const int MaximumRecordBytes = 8192;
    internal const int MaximumSnapshotBytes = 2048;
    internal const int MaximumRetainedBytes = 4 * 1024 * 1024;
    internal const string CollectorSchema = "submarine-observation-v1";
    private bool? supported;
    internal string Status { get; private set; } = "Local retention off; no submarine reads or uploads.";
    internal string WaitingStatus => store.LocalRetentionEnabled
        ? "Local retention on; awaiting verified data from workshop interfaces you open normally. History preserved."
        : "Local retention off; no submarine reads or uploads. History preserved.";
    internal bool Supported => supported ??= ValidateLoaded();
    private bool ValidateLoaded() {
        try {
            return store.SchemaVersion == 1 && store.Current is not null && store.Voyages is not null
                && store.Current.Count <= MaximumCurrentSnapshots && store.Voyages.Count <= MaximumRecords
                && store.Current.All(row => row?.Snapshot is not null && SnapshotValid(row.Snapshot) && Size(row) <= MaximumSnapshotBytes)
                && store.Current.Select(row => row.Snapshot.LocalSubmarineKey).Distinct().Count() == store.Current.Count
                && store.Voyages.All(row => row is not null && LocalKey(row.LocalVoyageKey) && LocalKey(row.LocalSubmarineKey)
                    && Guid.TryParseExact(row.ObservationId, "N", out _) && row.OrderedRoute is not null
                    && UtcObservation(row.FirstObservedAtUtc)
                    && (row.ResultsObservedAtUtc is null || UtcObservation(row.ResultsObservedAtUtc.Value))
                    && (!row.LinkedToVoyage || row.ExpectedReturnUnix > 0 && Route(row.OrderedRoute))
                    && (row.BuildEvidence is "unavailable-not-observed-in-flight" or "observed-while-in-flight-not-dispatch-proof")
                    && Metadata(row.GameVersion) && Metadata(row.CollectorVersion)
                    && (row.VoyageBuild is null || BuildValid(row.VoyageBuild))
                    && (row.Result is null || ResultValid(row.Result) && row.ResultsObservedAtUtc is not null
                        && row.ResultTimeBuild is not null && BuildValid(row.ResultTimeBuild)
                        && Metadata(row.ResultGameVersion!) && Metadata(row.ResultCollectorVersion!))
                    && Size(row) <= MaximumRecordBytes)
                && store.Voyages.Select(row => row.LocalVoyageKey).Distinct().Count() == store.Voyages.Count
                && store.Voyages.Select(row => row.ObservationId).Distinct().Count() == store.Voyages.Count;
        } catch (Exception) { return false; } // Preserve malformed retained data; never repair/prune implicitly.
    }
    // Reservation bounds guarantee admitted anchors have room to become results.
    // 400 * 8192 + 32 * 2048 + bounded metadata remains below 4 MiB.
    internal static int Size<T>(T row) => JsonSerializer.SerializeToUtf8Bytes(row).Length;
    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    internal static string SubmarineKey(ulong privateHouseId, byte slot, uint registration)
        => Hash(FormattableString.Invariant($"{privateHouseId}:{slot}:{registration}"));
    private static string Fingerprint<T>(T value) => Hash(JsonSerializer.Serialize(value));
    private static bool Route(byte[] route) => route.Length is >= 1 and <= 5 && route.All(id => id > 0) && route.Distinct().Count() == route.Length;
    private static bool LocalKey(string value) => value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool Metadata(string value) => value.Length is >= 1 and <= 80 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');
    private static bool UtcObservation(DateTime value) => value.Kind == DateTimeKind.Utc && value >= DateTime.UnixEpoch;
    private static bool BuildValid(SubmarineBuild b) => b.Rank > 0 && b.Parts is { Hull: > 0, Stern: > 0, Bow: > 0, Bridge: > 0 } && b.Stats is not null;
    private static bool SnapshotValid(SubmarineSnapshot s) => LocalKey(s.LocalSubmarineKey) && s.Slot < 4
        && s.Name.Length <= 20 && s.RegisteredAtUnix > 0 && s.Build.Rank > 0
        && s.Build.Parts is { Hull: > 0, Stern: > 0, Bow: > 0, Bridge: > 0 }
        && s.CurrentRoute.Length <= 5 && (s.CurrentRoute.Length == 0 || Route(s.CurrentRoute))
        && (s.PlannedRoute is null || s.PlannedRoute.Length == 0 || Route(s.PlannedRoute))
        && Metadata(s.GameVersion) && Metadata(s.CollectorVersion) && UtcObservation(s.ObservedAtUtc);
    private bool RejectCapacity() {
        Status = "Retention full or record too large: existing history preserved; new observations not admitted. No automatic deletion or upload.";
        var changed = !store.CapacityReached; store.CapacityReached = true; return changed;
    }
    // One already-normalized snapshot per slot/event. The caller saves once only
    // when this batch or its result observation actually changes retained data.
    internal bool ObserveSnapshots(IEnumerable<SubmarineSnapshot> snapshots) {
        bool changed = false;
        foreach (var snapshot in snapshots) changed |= ObserveSnapshot(snapshot);
        return changed;
    }
    internal bool ObserveSnapshot(SubmarineSnapshot snapshot) {
        if (!store.LocalRetentionEnabled) return false;
        if (!Supported) { Status = "Retained format unsupported/oversized; preserved unchanged. Collection paused."; return false; }
        if (!SnapshotValid(snapshot)) { Status = "Partial workshop data; no valid or empty voyage inferred."; return false; }
        var current = store.Current.SingleOrDefault(row => row.Snapshot.LocalSubmarineKey == snapshot.LocalSubmarineKey);
        var key = snapshot.ExpectedReturnUnix > 0 && Route(snapshot.CurrentRoute)
            ? Hash($"{snapshot.LocalSubmarineKey}:{snapshot.ExpectedReturnUnix}") : null;
        var voyage = key is null ? null : store.Voyages.SingleOrDefault(row => row.LocalVoyageKey == key);
        if (current is null && store.Current.Count >= MaximumCurrentSnapshots || key is not null && voyage is null && store.Voyages.Count >= MaximumRecords)
            return RejectCapacity();
        var next = new SubmarineCurrentObservation { Snapshot = snapshot, AnchoredVoyageKey = key ?? current?.AnchoredVoyageKey };
        if (Size(next) > MaximumSnapshotBytes) return RejectCapacity();
        bool changed = current is null || Fingerprint(current.Snapshot with { ObservedAtUtc = DateTime.MinValue })
            != Fingerprint(snapshot with { ObservedAtUtc = DateTime.MinValue }) || current.AnchoredVoyageKey != next.AnchoredVoyageKey;
        if (changed) {
            if (current is not null) store.Current.Remove(current);
            store.Current.Add(next);
        }
        if (key is not null && voyage is null) {
            bool inFlight = snapshot.ExpectedReturnUnix > new DateTimeOffset(snapshot.ObservedAtUtc).ToUnixTimeSeconds();
            store.Voyages.Add(new() {
                LocalVoyageKey = key, LocalSubmarineKey = snapshot.LocalSubmarineKey,
                ExpectedReturnUnix = snapshot.ExpectedReturnUnix, OrderedRoute = snapshot.CurrentRoute.ToArray(),
                VoyageBuild = inFlight ? snapshot.Build : null,
                BuildEvidence = inFlight ? "observed-while-in-flight-not-dispatch-proof" : "unavailable-not-observed-in-flight",
                FirstObservedAtUtc = snapshot.ObservedAtUtc, GameVersion = snapshot.GameVersion,
                CollectorVersion = snapshot.CollectorVersion, LinkedToVoyage = true
            }); changed = true;
        } else if (voyage is not null && (!voyage.OrderedRoute.SequenceEqual(snapshot.CurrentRoute)
            || voyage.VoyageBuild is not null && snapshot.ExpectedReturnUnix > new DateTimeOffset(snapshot.ObservedAtUtc).ToUnixTimeSeconds()
                && voyage.VoyageBuild != snapshot.Build) && !voyage.ConflictingObservation) {
            voyage.ConflictingObservation = true; changed = true;
        }
        Status = store.CapacityReached ? "Retention limit reached; existing admitted voyages can still receive results. New history is paused."
            : "Loaded snapshot retained. Expected return is not observed completion; departure time unavailable.";
        return changed;
    }
    private static bool ResultValid(SubmarineResult r) => r.RewardAssociation is "per-sector" or "voyage-only"
        && r.Sectors.Length <= 5 && r.VoyageRewards.Length <= 10 && r.Limitations.Length <= 8
        && r.Limitations.All(value => value is "sector-data-unavailable-or-inconsistent" or "sector-reward-unavailable-or-inconsistent"
            or "sector-attribution-unavailable" or "sector-aggregate-mismatch" or "hq-experience-unlocks-unavailable"
            or "total-experience-unavailable-route-completeness-unverified")
        && r.Sectors.All(s => s.SectorId > 0 && s.Rewards.Length <= 2 && s.Rewards.All(reward => RewardValid(reward) && reward.Hq.HasValue))
        && r.Sectors.Select(s => s.SectorId).Distinct().Count() == r.Sectors.Length
        && r.VoyageRewards.All(RewardValid)
        && (r.RewardAssociation != "voyage-only" || r.Sectors.Length == 0 && r.TotalExperience is null)
        && (r.RewardAssociation != "per-sector" || r.Sectors.Length > 0);
    private static bool RewardValid(SubmarineReward r) => r.ItemId > 0 && r.Quantity > 0;
    private static string RewardTotals(SubmarineResult result) => JsonSerializer.Serialize((result.RewardAssociation == "per-sector"
        ? result.Sectors.SelectMany(s => s.Rewards) : result.VoyageRewards)
        .GroupBy(r => r.ItemId).OrderBy(g => g.Key).Select(g => new { id = g.Key, quantity = g.Sum(r => (long)r.Quantity) }));
    internal bool ObserveResult(string localSubmarineKey, SubmarineBuild resultTimeBuild, SubmarineResult result,
        DateTime now, string gameVersion, string collectorVersion) {
        if (!store.LocalRetentionEnabled) return false;
        if (!Supported || !ResultValid(result) || !LocalKey(localSubmarineKey) || !BuildValid(resultTimeBuild)
            || !Metadata(gameVersion) || !Metadata(collectorVersion) || !UtcObservation(now)) {
            Status = "Partial/unsupported results; retained history preserved."; return false;
        }
        var current = store.Current.SingleOrDefault(row => row.Snapshot.LocalSubmarineKey == localSubmarineKey);
        var anchor = current?.AnchoredVoyageKey is { } anchorKey
            ? store.Voyages.SingleOrDefault(row => row.LocalVoyageKey == anchorKey) : null;
        bool linked = anchor is { LinkedToVoyage: true, ConflictingObservation: false, ExpectedReturnUnix: not null }
            && anchor.ExpectedReturnUnix <= new DateTimeOffset(now).ToUnixTimeSeconds() + 30
            && (result.RewardAssociation == "voyage-only" || result.Sectors.All(s => anchor.OrderedRoute.Contains(s.SectorId)));
        // Without an anchor, preserve a non-countable observation. A fingerprint
        // cannot distinguish identical successive voyages; never pretend otherwise.
        var key = linked ? anchor!.LocalVoyageKey : Hash($"unlinked:{localSubmarineKey}:{Fingerprint(result)}");
        var existing = store.Voyages.SingleOrDefault(row => row.LocalVoyageKey == key);
        if (existing?.Result is { } previous) {
            if (Fingerprint(previous) == Fingerprint(result)) { Status = "Duplicate result ignored; voyage not counted again."; return false; }
            // Only stronger sector attribution of identical aggregate rewards may
            // enrich an existing voyage-level result. No later build substitution.
            if (!(linked && previous.RewardAssociation == "voyage-only" && result.RewardAssociation == "per-sector"
                && RewardTotals(previous) == RewardTotals(result))) {
                Status = "Conflicting result retained as a warning; original history not overwritten or counted twice.";
                var change = !existing.ConflictingObservation; existing.ConflictingObservation = true; return change;
            }
        }
        if (existing is null && store.Voyages.Count >= MaximumRecords) return RejectCapacity();
        var candidate = existing is null ? new RetainedSubmarineVoyage {
            LocalVoyageKey = key, LocalSubmarineKey = localSubmarineKey, LinkedToVoyage = false,
            FirstObservedAtUtc = now, GameVersion = gameVersion, CollectorVersion = collectorVersion
        } : JsonSerializer.Deserialize<RetainedSubmarineVoyage>(JsonSerializer.Serialize(existing))!;
        candidate.Result = result;
        candidate.ResultsObservedAtUtc ??= now;
        candidate.ResultTimeBuild ??= resultTimeBuild;
        candidate.ResultGameVersion ??= gameVersion;
        candidate.ResultCollectorVersion ??= collectorVersion;
        if (existing?.Result is null) candidate.ContributionConsentAtResult = store.CommunityContributionEnabled;
        if (Size(candidate) > MaximumRecordBytes) return RejectCapacity();
        if (existing is not null) store.Voyages.Remove(existing);
        store.Voyages.Add(candidate);
        Status = linked ? "Completed result retained locally; no upload. Voyage-time build is preserved or explicitly unavailable."
            : "Result retained without a verified voyage anchor; excluded from countable contribution exports.";
        return true;
    }
    internal string PrepareExport() {
        if (!Supported || !store.CommunityContributionEnabled) throw new InvalidOperationException("Community preparation is off or retained format unsupported.");
        var eligible = store.Voyages.Where(v => v.LinkedToVoyage && !v.ConflictingObservation
            && v.Result is not null && v.ContributionConsentAtResult).Select(v => new {
                observationId = v.ObservationId, schemaVersion = 1, collectorSchema = CollectorSchema,
                gameVersion = v.GameVersion, collectorVersion = v.CollectorVersion,
                resultGameVersion = v.ResultGameVersion, resultCollectorVersion = v.ResultCollectorVersion,
                orderedSectorIds = v.OrderedRoute, expectedReturnAtUtc = DateTimeOffset.FromUnixTimeSeconds(v.ExpectedReturnUnix!.Value).UtcDateTime,
                departureAtUtc = (DateTime?)null, firstObservedAtUtc = v.FirstObservedAtUtc,
                resultsObservedAtUtc = v.ResultsObservedAtUtc, voyageBuild = v.VoyageBuild,
                buildEvidence = v.BuildEvidence, resultTimeBuild = v.ResultTimeBuild, result = v.Result
            }).ToArray();
        // Deliberate allowlist: no private key, house/FC, name, slot, registration,
        // reporter, account, credential, free text or diagnostic enters this data.
        return JsonSerializer.Serialize(new { schemaVersion = 1, collectorSchema = CollectorSchema,
            uploadState = "local-only-no-server-contract", voyages = eligible },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
    }
}
#endif
