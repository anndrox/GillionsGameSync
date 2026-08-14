using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace GillionsGameSync;

public sealed class RetainerVentureLocalState {
    public bool RosterComplete { get; set; }
    public DateTime? RosterObservedAtUtc { get; set; }
    public List<RetainerVentureProfile> Retainers { get; set; } = [];
    public List<RetainerVentureResultEvent> PendingResultEvents { get; set; } = [];
}

public sealed class RetainerVentureProfile {
    [JsonPropertyName("retainerId")]
    public string RetainerId { get; set; } = "";
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("classJobId")]
    public uint? ClassJobId { get; set; }
    [JsonPropertyName("level")]
    public int? Level { get; set; }
    [JsonPropertyName("profileObservedAtUtc")]
    public DateTime ProfileObservedAtUtc { get; set; }
    [JsonPropertyName("gearObserved")]
    public bool GearObserved { get; set; }
    [JsonPropertyName("gearObservedAtUtc")]
    public DateTime? GearObservedAtUtc { get; set; }
    [JsonPropertyName("equippedItems")]
    public List<RetainerVentureGearItem>? EquippedItems { get; set; }
    [JsonPropertyName("ventureObserved")]
    public bool VentureObserved { get; set; }
    [JsonPropertyName("ventureObservedAtUtc")]
    public DateTime? VentureObservedAtUtc { get; set; }
    [JsonPropertyName("venture")]
    public RetainerVentureAssignment? Venture { get; set; }
}

public sealed record RetainerVentureGearItem(
    [property: JsonPropertyName("slotIndex")] int SlotIndex,
    [property: JsonPropertyName("itemId")] uint ItemId,
    [property: JsonPropertyName("isHq")] bool IsHq);
public sealed record RetainerVentureAssignment(
    [property: JsonPropertyName("ventureId")] uint VentureId,
    [property: JsonPropertyName("completeAtUtc")] DateTime CompleteAtUtc,
    [property: JsonPropertyName("state")] string State);
public sealed record RetainerVentureRosterEntry(string RetainerId, string Name, byte ClassJobId, byte Level, ushort VentureId, uint VentureCompleteUnix);
public sealed record RetainerVentureRosterRead(DateTime ObservedAtUtc, bool Complete, RetainerVentureRosterEntry[] Retainers);
public sealed record RetainerVentureGearRead(string RetainerId, DateTime ObservedAtUtc, RetainerVentureGearItem[] EquippedItems);
public sealed record RetainerVentureResultItem(
    [property: JsonPropertyName("itemId")] uint ItemId,
    [property: JsonPropertyName("quantity")] uint Quantity);
public sealed record RetainerVentureResultRead(string RetainerId, uint VentureId, uint VentureCompleteUnix, DateTime ObservedAtUtc, uint RetainerExperience, RetainerVentureResultItem[] Items);
public sealed record RetainerVentureResultEvent(
    [property: JsonPropertyName("eventId")] string EventId,
    [property: JsonPropertyName("retainerId")] string RetainerId,
    [property: JsonPropertyName("ventureId")] uint VentureId,
    [property: JsonPropertyName("ventureCompleteAtUtc")] DateTime VentureCompleteAtUtc,
    [property: JsonPropertyName("observedAtUtc")] DateTime ObservedAtUtc,
    [property: JsonPropertyName("retainerExperience")] uint? RetainerExperience,
    [property: JsonPropertyName("items")] RetainerVentureResultItem[] Items,
    [property: JsonPropertyName("evidenceType")] string EvidenceType);

public sealed record RetainerVenturePayload(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("rosterComplete")] bool RosterComplete,
    [property: JsonPropertyName("rosterObservedAtUtc")] DateTime? RosterObservedAtUtc,
    [property: JsonPropertyName("character")] object Character,
    [property: JsonPropertyName("retainers")] RetainerVentureProfile[] Retainers,
    [property: JsonPropertyName("resultEvents")] RetainerVentureResultEvent[] ResultEvents);

public static class RetainerVentureSnapshotPolicy {
    public const int MaxRetainers = 10;
    public const int MaxPendingResultEvents = 50;

    public static bool MergeRoster(RetainerVentureLocalState state, RetainerVentureRosterRead? read, DateTime nowUtc) {
        if (read is null || !read.Complete) return false;
        var entries = read.Retainers
            .Where(entry => IsValidRetainerId(entry.RetainerId))
            .GroupBy(entry => entry.RetainerId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(entry => entry.RetainerId, StringComparer.Ordinal)
            .Take(MaxRetainers)
            .ToArray();
        var previous = state.Retainers.ToDictionary(entry => entry.RetainerId, StringComparer.Ordinal);
        var next = new List<RetainerVentureProfile>(entries.Length);
        foreach (var entry in entries) {
            previous.TryGetValue(entry.RetainerId, out var prior);
            var venture = BuildVenture(entry.VentureId, entry.VentureCompleteUnix, nowUtc);
            // A non-zero task with no valid completion timestamp is unavailable,
            // not idle. Keep the last positively observed venture in that case.
            var ventureObserved = entry.VentureId == 0 || venture is not null;
            var profileChanged = prior is null
                || prior.Name != entry.Name
                || prior.ClassJobId != NormalizeClassJob(entry.ClassJobId)
                || prior.Level != NormalizeLevel(entry.Level);
            var ventureChanged = prior is null
                || (ventureObserved && (!prior.VentureObserved || !Equals(prior.Venture, venture)));
            next.Add(new RetainerVentureProfile {
                RetainerId = entry.RetainerId,
                Name = entry.Name,
                ClassJobId = NormalizeClassJob(entry.ClassJobId),
                Level = NormalizeLevel(entry.Level),
                ProfileObservedAtUtc = profileChanged ? read.ObservedAtUtc : prior!.ProfileObservedAtUtc,
                GearObserved = prior?.GearObserved ?? false,
                GearObservedAtUtc = prior?.GearObservedAtUtc,
                EquippedItems = prior?.EquippedItems,
                VentureObserved = ventureObserved || (prior?.VentureObserved ?? false),
                VentureObservedAtUtc = ventureObserved
                    ? (ventureChanged ? read.ObservedAtUtc : prior?.VentureObservedAtUtc ?? read.ObservedAtUtc)
                    : prior?.VentureObservedAtUtc,
                Venture = ventureObserved ? venture : prior?.Venture,
            });
        }
        var changed = !EquivalentProfiles(state.Retainers, next) || !state.RosterComplete;
        state.RosterComplete = true;
        if (changed) state.RosterObservedAtUtc = read.ObservedAtUtc;
        state.Retainers = next;
        return changed;
    }

    public static bool MergeGear(RetainerVentureLocalState state, RetainerVentureGearRead? read) {
        if (read is null || !IsValidRetainerId(read.RetainerId)) return false;
        var profile = state.Retainers.FirstOrDefault(entry => entry.RetainerId == read.RetainerId);
        if (profile is null) return false;
        var items = read.EquippedItems
            .Where(item => item.SlotIndex >= 0 && item.ItemId > 0)
            .GroupBy(item => item.SlotIndex)
            .Select(group => group.First())
            .OrderBy(item => item.SlotIndex)
            .ThenBy(item => item.ItemId)
            .ToList();
        if (profile.GearObserved && profile.EquippedItems is not null && profile.EquippedItems.SequenceEqual(items)) return false;
        profile.GearObserved = true;
        profile.GearObservedAtUtc = read.ObservedAtUtc;
        profile.EquippedItems = items;
        return true;
    }

    public static RetainerVentureResultEvent? CreateResultEvent(RetainerVentureResultRead? read) {
        if (read is null || !IsValidRetainerId(read.RetainerId) || read.VentureId == 0) return null;
        var completedAt = TryReadUnixTime(read.VentureCompleteUnix);
        if (completedAt is null) return null;
        var items = read.Items
            .Where(item => item.ItemId > 0 && item.Quantity > 0)
            .OrderBy(item => item.ItemId)
            .ThenBy(item => item.Quantity)
            .Take(2)
            .ToArray();
        if (items.Length == 0) return null;
        var canonical = $"{read.RetainerId}|{read.VentureId}|{read.VentureCompleteUnix}|{read.RetainerExperience}|{string.Join(';', items.Select(item => $"{item.ItemId}:{item.Quantity}"))}";
        var eventId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return new RetainerVentureResultEvent(eventId, read.RetainerId, read.VentureId, completedAt.Value, read.ObservedAtUtc,
            read.RetainerExperience == 0 ? null : read.RetainerExperience, items, "native_retainer_task_result");
    }

    public static bool AddPendingResult(RetainerVentureLocalState state, RetainerVentureResultEvent? result) {
        if (result is null || state.PendingResultEvents.Any(entry => entry.EventId == result.EventId)) return false;
        state.PendingResultEvents.Add(result);
        state.PendingResultEvents = state.PendingResultEvents
            .OrderBy(entry => entry.ObservedAtUtc)
            .ThenBy(entry => entry.EventId, StringComparer.Ordinal)
            .TakeLast(MaxPendingResultEvents)
            .ToList();
        return true;
    }

    public static RetainerVenturePayload BuildPayload(RetainerVentureLocalState state, object character) => new(
        1,
        state.RosterComplete,
        state.RosterObservedAtUtc,
        character,
        state.Retainers.OrderBy(entry => entry.RetainerId, StringComparer.Ordinal).Select(CloneProfile).ToArray(),
        state.PendingResultEvents.OrderBy(entry => entry.ObservedAtUtc).ThenBy(entry => entry.EventId, StringComparer.Ordinal).ToArray());

    public static void AcknowledgeResults(RetainerVentureLocalState state, IEnumerable<string> eventIds) {
        var acknowledged = eventIds.ToHashSet(StringComparer.Ordinal);
        state.PendingResultEvents.RemoveAll(entry => acknowledged.Contains(entry.EventId));
    }

    public static RetainerVentureAssignment? BuildVenture(ushort ventureId, uint completeUnix, DateTime nowUtc) {
        if (ventureId == 0) return null;
        var completeAt = TryReadUnixTime(completeUnix);
        if (completeAt is null) return null;
        return new RetainerVentureAssignment(ventureId, completeAt.Value, completeAt.Value <= nowUtc ? "ready" : "in_progress");
    }

    public static uint ResolveResultCompletionUnix(RetainerVentureLocalState state, string retainerId, uint ventureId, uint nativeCompletionUnix) {
        if (TryReadUnixTime(nativeCompletionUnix) is not null) return nativeCompletionUnix;
        var prior = state.Retainers.FirstOrDefault(entry => entry.RetainerId == retainerId);
        if (prior?.VentureObserved != true || prior.Venture?.VentureId != ventureId) return 0;
        var completeAtUtc = prior.Venture.CompleteAtUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(prior.Venture.CompleteAtUtc, DateTimeKind.Utc)
            : prior.Venture.CompleteAtUtc.ToUniversalTime();
        var unix = new DateTimeOffset(completeAtUtc).ToUnixTimeSeconds();
        return unix is > 0 and <= uint.MaxValue && TryReadUnixTime((uint)unix) is not null ? (uint)unix : 0;
    }

    public static uint? NormalizeClassJob(byte classJobId) => classJobId == 0 ? null : classJobId;
    public static int? NormalizeLevel(byte level) => level == 0 ? null : level;

    private static bool IsValidRetainerId(string value) => ulong.TryParse(value, out var parsed) && parsed > 0;

    private static DateTime? TryReadUnixTime(uint value) {
        if (value == 0) return null;
        try {
            var result = DateTimeOffset.FromUnixTimeSeconds(value).UtcDateTime;
            return result.Year is >= 2010 and <= 2200 ? result : null;
        } catch (ArgumentOutOfRangeException) {
            return null;
        }
    }

    private static RetainerVentureProfile CloneProfile(RetainerVentureProfile source) => new() {
        RetainerId = source.RetainerId,
        Name = source.Name,
        ClassJobId = source.ClassJobId,
        Level = source.Level,
        ProfileObservedAtUtc = source.ProfileObservedAtUtc,
        GearObserved = source.GearObserved,
        GearObservedAtUtc = source.GearObservedAtUtc,
        EquippedItems = source.EquippedItems?.OrderBy(item => item.SlotIndex).ThenBy(item => item.ItemId).ToList(),
        VentureObserved = source.VentureObserved,
        VentureObservedAtUtc = source.VentureObservedAtUtc,
        Venture = source.Venture,
    };

    private static bool EquivalentProfiles(IEnumerable<RetainerVentureProfile> left, IEnumerable<RetainerVentureProfile> right) {
        static string Fingerprint(RetainerVentureProfile profile) => string.Join('|',
            profile.RetainerId, profile.Name, profile.ClassJobId, profile.Level, profile.ProfileObservedAtUtc.ToUniversalTime().Ticks,
            profile.GearObserved, profile.GearObservedAtUtc?.ToUniversalTime().Ticks,
            profile.EquippedItems is null ? "unknown" : string.Join(',', profile.EquippedItems.Select(item => $"{item.SlotIndex}:{item.ItemId}:{item.IsHq}")),
            profile.VentureObserved, profile.VentureObservedAtUtc?.ToUniversalTime().Ticks,
            profile.Venture is null ? "idle" : $"{profile.Venture.VentureId}:{profile.Venture.CompleteAtUtc.ToUniversalTime().Ticks}:{profile.Venture.State}");
        return left.OrderBy(entry => entry.RetainerId, StringComparer.Ordinal).Select(Fingerprint)
            .SequenceEqual(right.OrderBy(entry => entry.RetainerId, StringComparer.Ordinal).Select(Fingerprint), StringComparer.Ordinal);
    }
}
