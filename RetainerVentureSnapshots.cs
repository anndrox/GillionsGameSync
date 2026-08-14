using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GillionsGameSync;

public static class RetainerObservationVocabulary {
    public const string Complete = "complete";
    public const string Partial = "partial";
    public const string Unavailable = "unavailable";
    public const string AuthoritativeEmpty = "authoritative_empty";
    public const string NativeCurrent = "native_current";
    public const string LoadedOnly = "loaded_only";
    public const string AutoRetainerCached = "autoretainer_cached";
    public const string Derived = "derived";
    public const string RetainedHistorical = "retained_historical";
}

public sealed record RetainerObservationCoverage(
    [property: JsonPropertyName("scopeId")] string ScopeId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("lastObservedAtUtc")] DateTime? LastObservedAtUtc,
    [property: JsonPropertyName("lastChangedAtUtc")] DateTime? LastChangedAtUtc,
    [property: JsonPropertyName("provenance")] string Provenance,
    [property: JsonPropertyName("retainedData")] bool RetainedData);

public sealed record RetainerObservationEvidence(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("lastObservedAtUtc")] DateTime? LastObservedAtUtc,
    [property: JsonPropertyName("lastChangedAtUtc")] DateTime? LastChangedAtUtc,
    [property: JsonPropertyName("coverage")] RetainerObservationCoverage[] Coverage,
    [property: JsonPropertyName("provenance")] string Provenance,
    [property: JsonPropertyName("retainedData")] bool RetainedData) {
    public static RetainerObservationEvidence Unknown(string provenance) =>
        new(RetainerObservationVocabulary.Unavailable, null, null, [], provenance, false);
}

public sealed class RetainerVentureLocalState {
    public string CharacterContentId { get; set; } = "";
    public RetainerObservationEvidence RosterObservation { get; set; } = RetainerObservationEvidence.Unknown(RetainerObservationVocabulary.NativeCurrent);
    public List<RetainerVentureProfile> Retainers { get; set; } = [];
    public List<RetainerInventorySourceObservation> InventorySources { get; set; } = [];
    public List<RetainerVentureResultEvent> PendingResultEvents { get; set; } = [];
}

public sealed class RetainerVentureProfile {
    [JsonPropertyName("retainerId")]
    public string RetainerId { get; set; } = "";
    [JsonPropertyName("profileObservation")]
    public RetainerObservationEvidence ProfileObservation { get; set; } = RetainerObservationEvidence.Unknown(RetainerObservationVocabulary.NativeCurrent);
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("classJobId")]
    public uint? ClassJobId { get; set; }
    [JsonPropertyName("level")]
    public int? Level { get; set; }
    [JsonPropertyName("equipment")]
    public RetainerEquipmentObservation Equipment { get; set; } = new();
    [JsonPropertyName("stats")]
    public RetainerStatsObservation Stats { get; set; } = new();
    [JsonPropertyName("gil")]
    public List<RetainerGilObservation> Gil { get; set; } = [];
    [JsonPropertyName("venture")]
    public RetainerVentureObservation Venture { get; set; } = new();
}

public sealed class RetainerEquipmentObservation {
    [JsonPropertyName("observation")]
    public RetainerObservationEvidence Observation { get; set; } = RetainerObservationEvidence.Unknown(RetainerObservationVocabulary.LoadedOnly);
    [JsonPropertyName("items")]
    public List<RetainerVentureGearItem>? Items { get; set; }
}

public sealed class RetainerStatsObservation {
    [JsonPropertyName("observation")]
    public RetainerObservationEvidence Observation { get; set; } = RetainerObservationEvidence.Unknown(RetainerObservationVocabulary.AutoRetainerCached);
    [JsonPropertyName("itemLevel")]
    public int? ItemLevel { get; set; }
    [JsonPropertyName("gathering")]
    public int? Gathering { get; set; }
    [JsonPropertyName("perception")]
    public int? Perception { get; set; }
}

public sealed record RetainerGilObservation(
    [property: JsonPropertyName("context")] string Context,
    [property: JsonPropertyName("observation")] RetainerObservationEvidence Observation,
    [property: JsonPropertyName("value")] long? Value);

public sealed class RetainerVentureObservation {
    [JsonPropertyName("observation")]
    public RetainerObservationEvidence Observation { get; set; } = RetainerObservationEvidence.Unknown(RetainerObservationVocabulary.NativeCurrent);
    [JsonPropertyName("assignment")]
    public RetainerVentureAssignment? Assignment { get; set; }
}

public sealed record RetainerVentureBeginTimestamp(
    [property: JsonPropertyName("value")] DateTime Value,
    [property: JsonPropertyName("observedAtUtc")] DateTime ObservedAtUtc,
    [property: JsonPropertyName("provenance")] string Provenance);

public sealed record RetainerVentureAssignment(
    [property: JsonPropertyName("ventureId")] uint VentureId,
    [property: JsonPropertyName("beginAt")] RetainerVentureBeginTimestamp? BeginAt,
    [property: JsonPropertyName("completeAtUtc")] DateTime CompleteAtUtc);

public sealed record RetainerVentureGearItem(
    [property: JsonPropertyName("slotIndex")] int SlotIndex,
    [property: JsonPropertyName("itemId")] uint ItemId,
    [property: JsonPropertyName("isHq")] bool IsHq);

public sealed record RetainerVentureRosterEntry(string RetainerId, string Name, byte ClassJobId, byte Level, ushort VentureId, uint VentureCompleteUnix, uint Gil);
public sealed record RetainerVentureRosterRead(DateTime ObservedAtUtc, bool Complete, RetainerVentureRosterEntry[] Retainers);
public sealed record RetainerVentureGearRead(string RetainerId, DateTime ObservedAtUtc, RetainerVentureGearItem[] EquippedItems);
public sealed record AutoRetainerStatsRead(string RetainerId, DateTime ObservedAtUtc, int? ItemLevel, int? Gathering, int? Perception, DateTime? VentureStartedAtUtc);
public sealed record RetainerInventoryContainerRead(string ContainerId, bool Loaded, int UsedSlots, int MaximumSlots);
public sealed record RetainerInventorySourceRead(string Source, string? RetainerId, DateTime ObservedAtUtc, RetainerInventoryContainerRead[] Containers);

public sealed record RetainerInventoryCountObservation(
    [property: JsonPropertyName("value")] int Value,
    [property: JsonPropertyName("observedAtUtc")] DateTime ObservedAtUtc,
    [property: JsonPropertyName("provenance")] string Provenance,
    [property: JsonPropertyName("definitionVersion")] string DefinitionVersion);

public sealed record RetainerInventoryContainerObservation(
    [property: JsonPropertyName("containerId")] string ContainerId,
    [property: JsonPropertyName("observation")] RetainerObservationEvidence Observation);

public sealed class RetainerInventorySourceObservation {
    [JsonPropertyName("source")]
    public string Source { get; set; } = "";
    [JsonPropertyName("retainerId")]
    public string? RetainerId { get; set; }
    [JsonPropertyName("observation")]
    public RetainerObservationEvidence Observation { get; set; } = RetainerObservationEvidence.Unknown(RetainerObservationVocabulary.LoadedOnly);
    [JsonPropertyName("containers")]
    public List<RetainerInventoryContainerObservation> Containers { get; set; } = [];
    [JsonPropertyName("usedSlots")]
    public RetainerInventoryCountObservation? UsedSlots { get; set; }
    [JsonPropertyName("maximumSlots")]
    public RetainerInventoryCountObservation? MaximumSlots { get; set; }
}

public sealed record RetainerVentureResultItem(
    [property: JsonPropertyName("itemId")] uint ItemId,
    [property: JsonPropertyName("quantity")] uint Quantity);
public sealed record RetainerVentureResultRead(string RetainerId, uint VentureId, uint VentureCompleteUnix, DateTime ObservedAtUtc, uint AwardedExperience, RetainerVentureResultItem[] Items);
public sealed record RetainerVentureResultEvent(
    [property: JsonPropertyName("eventIdVersion")] string EventIdVersion,
    [property: JsonPropertyName("eventId")] string EventId,
    [property: JsonPropertyName("payloadFingerprint")] string PayloadFingerprint,
    [property: JsonPropertyName("payloadStatus")] string PayloadStatus,
    [property: JsonPropertyName("retainerId")] string RetainerId,
    [property: JsonPropertyName("ventureId")] uint VentureId,
    [property: JsonPropertyName("ventureCompleteAtUtc")] DateTime VentureCompleteAtUtc,
    [property: JsonPropertyName("observedAtUtc")] DateTime ObservedAtUtc,
    [property: JsonPropertyName("awardedExperience")] uint? AwardedExperience,
    [property: JsonPropertyName("items")] RetainerVentureResultItem[] Items,
    [property: JsonPropertyName("evidenceType")] string EvidenceType);

public sealed record RetainerVenturePayload(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("character")] object Character,
    [property: JsonPropertyName("rosterObservation")] RetainerObservationEvidence RosterObservation,
    [property: JsonPropertyName("retainers")] RetainerVentureProfile[] Retainers,
    [property: JsonPropertyName("inventorySources")] RetainerInventorySourceObservation[] InventorySources,
    [property: JsonPropertyName("resultEvents")] RetainerVentureResultEvent[] ResultEvents);

public static class RetainerVentureSnapshotPolicy {
    public const int MaxRetainers = 10;
    public const int MaxPendingResultEvents = 50;
    public const string InventorySlotDefinitionVersion = "native-container-slots-v1";

    public static RetainerVentureLocalState GetCharacterState(IDictionary<string, RetainerVentureLocalState> states, ulong contentId) {
        if (contentId == 0) throw new ArgumentOutOfRangeException(nameof(contentId));
        var key = contentId.ToString(CultureInfo.InvariantCulture);
        if (!states.TryGetValue(key, out var state)) {
            state = new RetainerVentureLocalState { CharacterContentId = key };
            states[key] = state;
        }
        state.CharacterContentId = key;
        state.Retainers ??= [];
        state.InventorySources ??= [];
        state.PendingResultEvents ??= [];
        return state;
    }

    public static bool MergeRoster(RetainerVentureLocalState state, RetainerVentureRosterRead? read, DateTime nowUtc) {
        if (read is null || !read.Complete) return MarkRosterUnavailable(state, nowUtc);
        var entries = read.Retainers.Where(entry => IsValidRetainerId(entry.RetainerId))
            .GroupBy(entry => entry.RetainerId, StringComparer.Ordinal).Select(group => group.First())
            .OrderBy(entry => entry.RetainerId, StringComparer.Ordinal).Take(MaxRetainers).ToArray();
        var priorById = state.Retainers.ToDictionary(entry => entry.RetainerId, StringComparer.Ordinal);
        var next = new List<RetainerVentureProfile>(entries.Length);
        foreach (var entry in entries) {
            priorById.TryGetValue(entry.RetainerId, out var prior);
            var classJobId = NormalizeClassJob(entry.ClassJobId);
            var level = NormalizeLevel(entry.Level);
            var profileChanged = prior is null || prior.Name != entry.Name || prior.ClassJobId != classJobId || prior.Level != level;
            var profileChangedAt = profileChanged ? read.ObservedAtUtc : prior!.ProfileObservation.LastChangedAtUtc ?? read.ObservedAtUtc;
            var venture = BuildVenture(entry.VentureId, entry.VentureCompleteUnix);
            RetainerVentureObservation ventureObservation;
            if (entry.VentureId == 0) {
                var changed = prior?.Venture.Assignment is not null || prior?.Venture.Observation.Status != RetainerObservationVocabulary.AuthoritativeEmpty;
                ventureObservation = new RetainerVentureObservation {
                    Observation = Evidence(RetainerObservationVocabulary.AuthoritativeEmpty, read.ObservedAtUtc,
                        changed ? read.ObservedAtUtc : prior?.Venture.Observation.LastChangedAtUtc ?? read.ObservedAtUtc,
                        RetainerObservationVocabulary.NativeCurrent),
                    Assignment = null,
                };
            } else if (venture is not null) {
                var changed = prior?.Venture.Assignment is null || !EquivalentVenture(prior.Venture.Assignment, venture);
                ventureObservation = new RetainerVentureObservation {
                    Observation = Evidence(RetainerObservationVocabulary.Complete, read.ObservedAtUtc,
                        changed ? read.ObservedAtUtc : prior?.Venture.Observation.LastChangedAtUtc ?? read.ObservedAtUtc,
                        RetainerObservationVocabulary.NativeCurrent),
                    Assignment = venture with { BeginAt = prior?.Venture.Assignment?.VentureId == venture.VentureId ? prior.Venture.Assignment.BeginAt : null },
                };
            } else if (prior?.Venture.Assignment is not null) {
                ventureObservation = new RetainerVentureObservation {
                    Observation = Evidence(RetainerObservationVocabulary.Unavailable, read.ObservedAtUtc,
                        prior.Venture.Observation.LastChangedAtUtc ?? read.ObservedAtUtc,
                        RetainerObservationVocabulary.RetainedHistorical, retained: true),
                    Assignment = prior.Venture.Assignment,
                };
            } else {
                ventureObservation = new RetainerVentureObservation {
                    Observation = Evidence(RetainerObservationVocabulary.Unavailable, read.ObservedAtUtc, read.ObservedAtUtc, RetainerObservationVocabulary.NativeCurrent),
                    Assignment = null,
                };
            }
            var gilPrior = prior?.Gil.FirstOrDefault(entry => entry.Context == "native_roster_current");
            var gilChanged = gilPrior?.Value != entry.Gil;
            var gil = new RetainerGilObservation("native_roster_current",
                Evidence(RetainerObservationVocabulary.Complete, read.ObservedAtUtc,
                    gilChanged ? read.ObservedAtUtc : gilPrior?.Observation.LastChangedAtUtc ?? read.ObservedAtUtc,
                    RetainerObservationVocabulary.NativeCurrent), entry.Gil);
            next.Add(new RetainerVentureProfile {
                RetainerId = entry.RetainerId,
                ProfileObservation = Evidence(RetainerObservationVocabulary.Complete, read.ObservedAtUtc, profileChangedAt, RetainerObservationVocabulary.NativeCurrent),
                Name = string.IsNullOrWhiteSpace(entry.Name) ? null : entry.Name,
                ClassJobId = classJobId,
                Level = level,
                Equipment = prior?.Equipment ?? new(),
                Stats = prior?.Stats ?? new(),
                Gil = [gil],
                Venture = ventureObservation,
            });
        }
        var rosterIdsChanged = !state.Retainers.Select(entry => entry.RetainerId).Order().SequenceEqual(next.Select(entry => entry.RetainerId).Order(), StringComparer.Ordinal);
        state.RosterObservation = Evidence(entries.Length == 0 ? RetainerObservationVocabulary.AuthoritativeEmpty : RetainerObservationVocabulary.Complete,
            read.ObservedAtUtc, rosterIdsChanged || state.RosterObservation.LastChangedAtUtc is null ? read.ObservedAtUtc : state.RosterObservation.LastChangedAtUtc.Value,
            RetainerObservationVocabulary.NativeCurrent);
        state.Retainers = next;
        return true;
    }

    public static bool MarkRosterUnavailable(RetainerVentureLocalState state, DateTime observedAtUtc) {
        if (state.Retainers.Count == 0) {
            state.RosterObservation = Evidence(RetainerObservationVocabulary.Unavailable, observedAtUtc,
                state.RosterObservation.LastChangedAtUtc ?? observedAtUtc, RetainerObservationVocabulary.NativeCurrent);
        } else {
            state.RosterObservation = Evidence(RetainerObservationVocabulary.Unavailable, observedAtUtc,
                state.RosterObservation.LastChangedAtUtc ?? observedAtUtc, RetainerObservationVocabulary.RetainedHistorical, retained: true);
        }
        return true;
    }

    public static bool MergeGear(RetainerVentureLocalState state, RetainerVentureGearRead? read) {
        if (read is null || !IsValidRetainerId(read.RetainerId)) return false;
        var profile = state.Retainers.FirstOrDefault(entry => entry.RetainerId == read.RetainerId);
        if (profile is null) return false;
        var items = read.EquippedItems.Where(item => item.SlotIndex >= 0 && item.ItemId > 0)
            .GroupBy(item => item.SlotIndex).Select(group => group.First())
            .OrderBy(item => item.SlotIndex).ThenBy(item => item.ItemId).ToList();
        var changed = profile.Equipment.Items is null || !profile.Equipment.Items.SequenceEqual(items);
        profile.Equipment = new RetainerEquipmentObservation {
            Observation = Evidence(items.Count == 0 ? RetainerObservationVocabulary.AuthoritativeEmpty : RetainerObservationVocabulary.Complete,
                read.ObservedAtUtc, changed ? read.ObservedAtUtc : profile.Equipment.Observation.LastChangedAtUtc ?? read.ObservedAtUtc,
                RetainerObservationVocabulary.LoadedOnly),
            Items = items,
        };
        return true;
    }

    public static bool MergeAutoRetainerStats(RetainerVentureLocalState state, IEnumerable<AutoRetainerStatsRead>? reads, DateTime observedAtUtc) {
        var byId = (reads ?? []).Where(read => IsValidRetainerId(read.RetainerId)).ToDictionary(read => read.RetainerId, StringComparer.Ordinal);
        var touched = false;
        foreach (var profile in state.Retainers) {
            if (byId.TryGetValue(profile.RetainerId, out var read) && (read.ItemLevel is not null || read.Gathering is not null || read.Perception is not null)) {
                var changed = profile.Stats.ItemLevel != read.ItemLevel || profile.Stats.Gathering != read.Gathering || profile.Stats.Perception != read.Perception;
                var populated = new[] { read.ItemLevel, read.Gathering, read.Perception }.Count(value => value is not null);
                profile.Stats = new RetainerStatsObservation {
                    Observation = Evidence(populated == 3 ? RetainerObservationVocabulary.Complete : RetainerObservationVocabulary.Partial,
                        read.ObservedAtUtc, changed ? read.ObservedAtUtc : profile.Stats.Observation.LastChangedAtUtc ?? read.ObservedAtUtc,
                        RetainerObservationVocabulary.AutoRetainerCached),
                    ItemLevel = read.ItemLevel,
                    Gathering = read.Gathering,
                    Perception = read.Perception,
                };
                if (read.VentureStartedAtUtc is DateTime started && profile.Venture.Assignment is not null) {
                    profile.Venture.Assignment = profile.Venture.Assignment with {
                        BeginAt = new RetainerVentureBeginTimestamp(started, read.ObservedAtUtc, RetainerObservationVocabulary.AutoRetainerCached),
                    };
                }
            } else if (profile.Stats.ItemLevel is not null || profile.Stats.Gathering is not null || profile.Stats.Perception is not null) {
                profile.Stats.Observation = Evidence(RetainerObservationVocabulary.Unavailable, observedAtUtc,
                    profile.Stats.Observation.LastChangedAtUtc ?? observedAtUtc, RetainerObservationVocabulary.RetainedHistorical, retained: true);
            } else {
                profile.Stats.Observation = Evidence(RetainerObservationVocabulary.Unavailable, observedAtUtc,
                    profile.Stats.Observation.LastChangedAtUtc ?? observedAtUtc, RetainerObservationVocabulary.AutoRetainerCached);
            }
            touched = true;
        }
        return touched;
    }

    public static bool MergeInventorySources(RetainerVentureLocalState state, IEnumerable<RetainerInventorySourceRead> reads) {
        var next = new List<RetainerInventorySourceObservation>();
        foreach (var read in reads) {
            var prior = state.InventorySources.FirstOrDefault(entry => entry.Source == read.Source && entry.RetainerId == read.RetainerId);
            var loaded = read.Containers.Where(container => container.Loaded).ToArray();
            var status = loaded.Length == 0 ? RetainerObservationVocabulary.Unavailable
                : loaded.Length == read.Containers.Length ? RetainerObservationVocabulary.Complete : RetainerObservationVocabulary.Partial;
            var containers = read.Containers.Select(container => new RetainerInventoryContainerObservation(container.ContainerId,
                Evidence(container.Loaded ? RetainerObservationVocabulary.Complete : RetainerObservationVocabulary.Unavailable,
                    read.ObservedAtUtc, container.Loaded ? read.ObservedAtUtc : prior?.Observation.LastChangedAtUtc ?? read.ObservedAtUtc,
                    RetainerObservationVocabulary.LoadedOnly))).ToList();
            var changed = prior is null || !EquivalentInventory(prior, read);
            next.Add(new RetainerInventorySourceObservation {
                Source = read.Source,
                RetainerId = read.RetainerId,
                Observation = Evidence(status, read.ObservedAtUtc,
                    changed ? read.ObservedAtUtc : prior?.Observation.LastChangedAtUtc ?? read.ObservedAtUtc,
                    RetainerObservationVocabulary.LoadedOnly),
                Containers = containers,
                UsedSlots = status == RetainerObservationVocabulary.Complete
                    ? new(loaded.Sum(container => container.UsedSlots), read.ObservedAtUtc, RetainerObservationVocabulary.LoadedOnly, InventorySlotDefinitionVersion) : null,
                MaximumSlots = status == RetainerObservationVocabulary.Complete
                    ? new(loaded.Sum(container => container.MaximumSlots), read.ObservedAtUtc, RetainerObservationVocabulary.LoadedOnly, InventorySlotDefinitionVersion) : null,
            });
        }
        state.InventorySources = next;
        return true;
    }

    public static RetainerVentureResultEvent? CreateResultEvent(RetainerVentureLocalState state, RetainerVentureResultRead? read) {
        if (read is null || !IsValidRetainerId(state.CharacterContentId) || !IsValidRetainerId(read.RetainerId) || read.VentureId == 0) return null;
        var completedAt = TryReadUnixTime(read.VentureCompleteUnix);
        if (completedAt is null) return null;
        var items = read.Items.Where(item => item.ItemId > 0 && item.Quantity > 0)
            .OrderBy(item => item.ItemId).ThenBy(item => item.Quantity).Take(2).ToArray();
        if (items.Length == 0) return null;
        uint? awardedExperience = read.AwardedExperience == 0 ? null : read.AwardedExperience;
        var completedText = CanonicalUtc(completedAt.Value);
        var identity = JsonSerializer.Serialize(new object[] { "retainer-result-v1", state.CharacterContentId, read.RetainerId, read.VentureId, completedText });
        var fingerprint = JsonSerializer.Serialize(new { awardedExperience, items = items.Select(item => new { itemId = item.ItemId, quantity = item.Quantity }).ToArray() });
        return new RetainerVentureResultEvent("retainer-result-v1", Hash(identity), Hash(fingerprint), awardedExperience is not null ? "complete" : "partial",
            read.RetainerId, read.VentureId, completedAt.Value, read.ObservedAtUtc, awardedExperience, items, "native_retainer_task_result");
    }

    public static bool AddPendingResult(RetainerVentureLocalState state, RetainerVentureResultEvent? result) {
        if (result is null) return false;
        var existing = state.PendingResultEvents.FindIndex(entry => entry.EventId == result.EventId);
        if (existing >= 0) {
            if (state.PendingResultEvents[existing].PayloadFingerprint == result.PayloadFingerprint) return false;
            state.PendingResultEvents[existing] = result;
        } else state.PendingResultEvents.Add(result);
        state.PendingResultEvents = state.PendingResultEvents.OrderBy(entry => entry.ObservedAtUtc)
            .ThenBy(entry => entry.EventId, StringComparer.Ordinal).TakeLast(MaxPendingResultEvents).ToList();
        return true;
    }

    public static RetainerVenturePayload BuildPayload(RetainerVentureLocalState state, object character) => new(1, character,
        state.RosterObservation,
        state.Retainers.OrderBy(entry => entry.RetainerId, StringComparer.Ordinal).Select(CloneProfile).ToArray(),
        state.InventorySources.OrderBy(entry => entry.Source, StringComparer.Ordinal).ThenBy(entry => entry.RetainerId, StringComparer.Ordinal).ToArray(),
        state.PendingResultEvents.OrderBy(entry => entry.ObservedAtUtc).ThenBy(entry => entry.EventId, StringComparer.Ordinal).ToArray());

    public static void AcknowledgeResults(RetainerVentureLocalState state, IEnumerable<string> eventIds) {
        var acknowledged = eventIds.ToHashSet(StringComparer.Ordinal);
        state.PendingResultEvents.RemoveAll(entry => acknowledged.Contains(entry.EventId));
    }

    public static RetainerVentureAssignment? BuildVenture(ushort ventureId, uint completeUnix) {
        if (ventureId == 0) return null;
        var completeAt = TryReadUnixTime(completeUnix);
        return completeAt is null ? null : new RetainerVentureAssignment(ventureId, null, completeAt.Value);
    }

    public static uint ResolveResultCompletionUnix(RetainerVentureLocalState state, string retainerId, uint ventureId, uint nativeCompletionUnix) {
        if (TryReadUnixTime(nativeCompletionUnix) is not null) return nativeCompletionUnix;
        var prior = state.Retainers.FirstOrDefault(entry => entry.RetainerId == retainerId);
        if (prior?.Venture.Assignment?.VentureId != ventureId) return 0;
        var unix = new DateTimeOffset(prior.Venture.Assignment.CompleteAtUtc.ToUniversalTime()).ToUnixTimeSeconds();
        return unix is > 0 and <= uint.MaxValue && TryReadUnixTime((uint)unix) is not null ? (uint)unix : 0;
    }

    public static uint? NormalizeClassJob(byte classJobId) => classJobId == 0 ? null : classJobId;
    public static int? NormalizeLevel(byte level) => level == 0 ? null : level;

    private static RetainerObservationEvidence Evidence(string status, DateTime observed, DateTime changed, string provenance, bool retained = false) =>
        new(status, observed.ToUniversalTime(), changed.ToUniversalTime(), [], provenance, retained);

    private static bool IsValidRetainerId(string value) => ulong.TryParse(value, out var parsed) && parsed > 0;
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string CanonicalUtc(DateTime value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    private static DateTime? TryReadUnixTime(uint value) {
        if (value == 0) return null;
        try { var result = DateTimeOffset.FromUnixTimeSeconds(value).UtcDateTime; return result.Year is >= 2010 and <= 2200 ? result : null; }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static RetainerVentureProfile CloneProfile(RetainerVentureProfile source) => new() {
        RetainerId = source.RetainerId, ProfileObservation = source.ProfileObservation, Name = source.Name,
        ClassJobId = source.ClassJobId, Level = source.Level,
        Equipment = new() { Observation = source.Equipment.Observation, Items = source.Equipment.Items?.OrderBy(item => item.SlotIndex).ToList() },
        Stats = new() { Observation = source.Stats.Observation, ItemLevel = source.Stats.ItemLevel, Gathering = source.Stats.Gathering, Perception = source.Stats.Perception },
        Gil = source.Gil.ToList(), Venture = new() { Observation = source.Venture.Observation, Assignment = source.Venture.Assignment },
    };

    private static bool EquivalentVenture(RetainerVentureAssignment left, RetainerVentureAssignment right) =>
        left.VentureId == right.VentureId && left.CompleteAtUtc.ToUniversalTime() == right.CompleteAtUtc.ToUniversalTime();

    private static bool EquivalentInventory(RetainerInventorySourceObservation prior, RetainerInventorySourceRead read) =>
        prior.Containers.Select(entry => entry.ContainerId).Order().SequenceEqual(read.Containers.Select(entry => entry.ContainerId).Order(), StringComparer.Ordinal)
        && prior.UsedSlots?.Value == read.Containers.Where(entry => entry.Loaded).Sum(entry => entry.UsedSlots)
        && prior.MaximumSlots?.Value == read.Containers.Where(entry => entry.Loaded).Sum(entry => entry.MaximumSlots);
}

public static class RetainerAcknowledgementPolicy {
    private static readonly System.Text.RegularExpressions.Regex HashPattern = new("^[0-9a-f]{64}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    public static bool TryParseExact(string json, IEnumerable<string> sentEventIds, out string[] acceptedEventIds) {
        acceptedEventIds = [];
        try {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True
                || !root.TryGetProperty("resourceType", out var resource) || resource.GetString() != "retainer_ventures"
                || !root.TryGetProperty("schemaVersion", out var schema) || schema.GetInt32() != 1
                || !root.TryGetProperty("snapshotAccepted", out var snapshot) || snapshot.ValueKind != JsonValueKind.True
                || !root.TryGetProperty("serverTimeUtc", out var serverTime) || !DateTimeOffset.TryParse(serverTime.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _)
                || !root.TryGetProperty("acceptedEventIds", out var accepted) || accepted.ValueKind != JsonValueKind.Array) return false;
            var sent = sentEventIds.ToHashSet(StringComparer.Ordinal);
            var parsed = accepted.EnumerateArray().Select(entry => entry.ValueKind == JsonValueKind.String ? entry.GetString() ?? "" : "").ToArray();
            if (parsed.Length > MaxAcknowledgedEvents || parsed.Distinct(StringComparer.Ordinal).Count() != parsed.Length
                || parsed.Any(id => !HashPattern.IsMatch(id) || !sent.Contains(id))) return false;
            acceptedEventIds = parsed;
            return true;
        } catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException) { return false; }
    }

    private const int MaxAcknowledgedEvents = 50;
}

public static class RetainerPresencePolicy {
    public const int NormalIntervalSeconds = 30;
    public const int OnlineWindowSeconds = 90;
    public const int MaximumBackoffSeconds = 300;

    public static TimeSpan NextSuccessDelay(int jitterSeconds) => TimeSpan.FromSeconds(NormalIntervalSeconds + Math.Clamp(jitterSeconds, -5, 5));
    public static TimeSpan NextFailureDelay(int consecutiveFailures, int jitterSeconds) {
        var exponent = Math.Clamp(consecutiveFailures, 1, 6);
        var seconds = Math.Min(MaximumBackoffSeconds, 10 * (1 << (exponent - 1)));
        return TimeSpan.FromSeconds(Math.Clamp(seconds + Math.Clamp(jitterSeconds, -5, 5), 5, MaximumBackoffSeconds));
    }
}
