using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GillionsGameSync;

public sealed record RetainerAppliedPlanDocument(
    [property: JsonPropertyName("retainerId")] string RetainerId,
    [property: JsonPropertyName("revisionId")] string RevisionId,
    [property: JsonPropertyName("revisionNumber")] int RevisionNumber,
    [property: JsonPropertyName("projectionGeneration")] int ProjectionGeneration,
    [property: JsonPropertyName("deliveryId")] string DeliveryId,
    [property: JsonPropertyName("appliedHash")] string AppliedHash,
    [property: JsonPropertyName("priorPlanBackupHash")] string PriorPlanBackupHash,
    [property: JsonPropertyName("backupIncludesCompletionBehavior")] bool BackupIncludesCompletionBehavior);

internal sealed record RetainerPlanDeliveryDocument(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("operation")] string Operation,
    [property: JsonPropertyName("deliveryId")] string DeliveryId,
    [property: JsonPropertyName("leaseToken")] string LeaseToken,
    [property: JsonPropertyName("planId")] string? PlanId,
    [property: JsonPropertyName("revisionId")] string? RevisionId,
    [property: JsonPropertyName("revisionNumber")] int? RevisionNumber,
    [property: JsonPropertyName("revisionHash")] string? RevisionHash,
    [property: JsonPropertyName("projectionGeneration")] int? ProjectionGeneration,
    [property: JsonPropertyName("retainerId")] string RetainerId,
    [property: JsonPropertyName("retainerName")] string RetainerName,
    [property: JsonPropertyName("expectedAppliedHash")] string? ExpectedAppliedHash,
    [property: JsonPropertyName("completionBehavior")] string? CompletionBehavior,
    [property: JsonPropertyName("steps")] GillionsVenturePlanStep[]? Steps,
    [property: JsonPropertyName("priorPlanBackupHash")] string? PriorPlanBackupHash,
    [property: JsonPropertyName("priorPlanBackup")] AutoRetainerVenturePlanBackup? PriorPlanBackup,
    [property: JsonPropertyName("requiredCapabilities")] string[] RequiredCapabilities,
    [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc,
    [property: JsonPropertyName("expiresAtUtc")] DateTime ExpiresAtUtc);

internal sealed record RetainerPlanPollResponse(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("serverTimeUtc")] DateTime ServerTimeUtc,
    [property: JsonPropertyName("pollAfterSeconds")] int PollAfterSeconds,
    [property: JsonPropertyName("deliveries")] RetainerPlanDeliveryDocument[] Deliveries);

internal sealed record RetainerPlanAcknowledgementDocument(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("operation")] string Operation,
    [property: JsonPropertyName("deliveryId")] string DeliveryId,
    [property: JsonPropertyName("leaseToken")] string LeaseToken,
    [property: JsonPropertyName("revisionId")] string? RevisionId,
    [property: JsonPropertyName("projectionGeneration")] int? ProjectionGeneration,
    [property: JsonPropertyName("retainerId")] string RetainerId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("clientVersion")] string ClientVersion,
    [property: JsonPropertyName("capabilities")] string[] Capabilities,
    [property: JsonPropertyName("appliedAtUtc")] DateTime? AppliedAtUtc,
    [property: JsonPropertyName("observedBeforeHash")] string? ObservedBeforeHash,
    [property: JsonPropertyName("appliedHash")] string? AppliedHash,
    [property: JsonPropertyName("readBackHash")] string? ReadBackHash,
    [property: JsonPropertyName("priorPlanBackupHash")] string? PriorPlanBackupHash,
    [property: JsonPropertyName("priorPlanBackup")] AutoRetainerVenturePlanBackup? PriorPlanBackup,
    [property: JsonPropertyName("backupIncludesCompletionBehavior")] bool BackupIncludesCompletionBehavior);

internal static class RetainerPlanDeliveryPolicy {
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static bool TryParse(string json, DateTime nowUtc, out RetainerPlanPollResponse? response) {
        response = null;
        try {
            var parsed = JsonSerializer.Deserialize<RetainerPlanPollResponse>(json, JsonOptions);
            if (parsed is not { Ok: true, SchemaVersion: 1 }
                || parsed.PollAfterSeconds is < 15 or > 300
                || parsed.Deliveries is null || parsed.Deliveries.Length > 10
                || parsed.Deliveries.Any(delivery => !IsValid(delivery, nowUtc))
                || parsed.Deliveries.GroupBy(delivery => delivery.RetainerId, StringComparer.Ordinal).Any(group => group.Count() > 1)) return false;
            response = parsed;
            return true;
        } catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException) {
            return false;
        }
    }

    public static bool IsValid(RetainerPlanDeliveryDocument delivery, DateTime nowUtc) {
        if (delivery.SchemaVersion != 1
            || !Guid.TryParse(delivery.DeliveryId, out _)
            || delivery.LeaseToken.Length < 32
            || !ulong.TryParse(delivery.RetainerId, out var retainerId) || retainerId == 0
            || delivery.RetainerName.Length is < 1 or > 64
            || delivery.CreatedAtUtc.ToUniversalTime() > nowUtc.AddMinutes(1)
            || delivery.ExpiresAtUtc.ToUniversalTime() <= delivery.CreatedAtUtc.ToUniversalTime()
            || delivery.ExpiresAtUtc.ToUniversalTime() <= nowUtc
            || delivery.RequiredCapabilities is null
                || !delivery.RequiredCapabilities.All(capability => RetainerCapabilities.Client.Contains(capability))) return false;
        if (delivery.Operation == "apply_projection") {
            if (!Guid.TryParse(delivery.PlanId, out _)
                || !Guid.TryParse(delivery.RevisionId, out _)
                || delivery.RevisionNumber is null or < 1
                || delivery.ProjectionGeneration is null or < 1
                || delivery.RevisionHash?.Length != 64
                || delivery.ExpectedAppliedHash is not null && delivery.ExpectedAppliedHash.Length != 64
                || delivery.CompletionBehavior is not ("restart_plan" or "assign_quick_venture" or "do_nothing")
                || delivery.Steps is not { Length: > 0 and <= VenturePlannerCapabilityPolicy.MaximumPendingExecutions }) return false;
            var plan = new GillionsVenturePlanSpec(delivery.RetainerId, delivery.RetainerName, delivery.Steps, delivery.CompletionBehavior);
            return VenturePlannerCapabilityPolicy.IsValid(plan);
        }
        return delivery.Operation == "restore_prior"
            && delivery.ExpectedAppliedHash?.Length == 64
            && delivery.PriorPlanBackupHash?.Length == 64
            && delivery.PriorPlanBackup is not null;
    }

    public static RetainerAppliedPlanDocument[] AppliedPlansForCharacter(
        IReadOnlyDictionary<string, AutoRetainerPlanOwnershipState> states,
        ulong contentId) => states
        .Where(entry => entry.Key.StartsWith($"{contentId}:", StringComparison.Ordinal) && !entry.Value.RestoreApplied)
        .Select(entry => new RetainerAppliedPlanDocument(
            entry.Value.RetainerId,
            entry.Value.RevisionId,
            entry.Value.RevisionNumber,
            entry.Value.ProjectionGeneration,
            entry.Value.DeliveryId,
            entry.Value.AppliedHash,
            entry.Value.PriorPlanBackupHash,
            true))
        .Take(10)
        .ToArray();

    public static string OwnershipKey(ulong contentId, string retainerId) => $"{contentId}:{retainerId}";

    public static bool IsCurrentCharacter(ulong requestedContentId, ulong currentContentId) =>
        requestedContentId != 0 && requestedContentId == currentContentId;

    public static bool IsOwnedByDevice(AutoRetainerPlanOwnershipState? ownership, string deviceId) =>
        ownership is null || (!string.IsNullOrWhiteSpace(deviceId)
            && string.Equals(ownership.OwnerDeviceId, deviceId, StringComparison.Ordinal));

    public static RetainerVentureProfile? ResolveRetainer(
        IEnumerable<RetainerVentureProfile> retainers,
        string retainerId) {
        var matches = retainers.Where(entry => string.Equals(entry.RetainerId, retainerId, StringComparison.Ordinal)).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    public static bool IsLatestDelivery(
        AutoRetainerPlanOwnershipState? ownership,
        RetainerPlanDeliveryDocument delivery) {
        if (delivery.Operation != "apply_projection" || delivery.RevisionNumber is null || delivery.ProjectionGeneration is null) return false;
        if (ownership is null) return true;
        // Testing builds published before revision-number persistence still retain
        // the immutable revision ID and CAS hashes. Accept one valid delivery so
        // the state can be upgraded without stranding an already-owned plan.
        if (ownership.RevisionNumber == 0) return true;
        if (string.Equals(ownership.RevisionId, delivery.RevisionId, StringComparison.Ordinal))
            return delivery.RevisionNumber.Value == ownership.RevisionNumber
                && delivery.ProjectionGeneration.Value >= ownership.ProjectionGeneration;
        return delivery.RevisionNumber.Value > ownership.RevisionNumber;
    }
}
