using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
#if !GILLIONS_POLICY_TESTS
using Dalamud.Plugin;
#endif

namespace GillionsGameSync;

public sealed record GillionsVenturePlanStep(
    [property: JsonPropertyName("ventureId")] uint VentureId,
    [property: JsonPropertyName("repetitions")] int Repetitions);

internal sealed record GillionsVenturePlanSpec(
    string RetainerId,
    string RetainerName,
    GillionsVenturePlanStep[] Steps,
    string CompletionBehavior);

public sealed record AutoRetainerVenturePlanBackup(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("steps")] GillionsVenturePlanStep[] Steps,
    [property: JsonPropertyName("planCompleteBehavior")] string PlanCompleteBehavior,
    [property: JsonPropertyName("linkedVenturePlan")] string LinkedVenturePlan,
    [property: JsonPropertyName("venturePlanIndex")] uint VenturePlanIndex,
    [property: JsonPropertyName("enablePlanner")] bool EnablePlanner);

public sealed record AutoRetainerPlanOwnershipState(
    string OwnerDeviceId,
    string RetainerId,
    string RevisionId,
    int ProjectionGeneration,
    string DeliveryId,
    string AppliedHash,
    string PriorPlanBackupHash,
    AutoRetainerVenturePlanBackup PriorPlanBackup,
    int RevisionNumber = 0,
    bool RestoreApplied = false);

internal enum AutoRetainerPlanApplyResult {
    Applied,
    Restored,
    Idempotent,
    AutoRetainerUnavailable,
    PlannerDisabled,
    InvalidPlan,
    CasMismatch,
    ReadBackMismatch,
    IpcRejected,
}

internal enum AutoRetainerOwnedPlanDecision {
    Apply,
    Idempotent,
    Conflict,
}

internal sealed record AutoRetainerPlanMutationOutcome(
    AutoRetainerPlanApplyResult Result,
    string? ObservedBeforeHash = null,
    string? AppliedHash = null,
    string? ReadBackHash = null,
    string? PriorPlanBackupHash = null,
    AutoRetainerVenturePlanBackup? PriorPlanBackup = null);

internal static class VenturePlannerCapabilityPolicy {
    public const int MaximumPendingExecutions = 24;

    public static bool IsAvailable(bool enabled, bool autoRetainerLoaded, bool apiReady, bool paired, bool rosterComplete) =>
        enabled && autoRetainerLoaded && apiReady && paired && rosterComplete;

    public static bool IsValid(GillionsVenturePlanSpec? plan) => plan is {
        RetainerId.Length: > 0,
        RetainerName.Length: > 0,
        Steps.Length: > 0 and <= MaximumPendingExecutions,
    } && ulong.TryParse(plan.RetainerId, out var retainerId) && retainerId > 0
        && plan.RetainerName.Length <= 64
        && plan.CompletionBehavior is "assign_quick_venture" or "do_nothing"
        && plan.Steps.All(step => step.VentureId > 0 && step.Repetitions is > 0 and <= MaximumPendingExecutions)
        && plan.Steps.Sum(step => step.Repetitions) <= MaximumPendingExecutions;

    public static string BuildManagedPlanName(string retainerName) => $"Gillions Venture ({retainerName.Trim()})";
}

internal static class AutoRetainerOwnedPlanPolicy {
    public static AutoRetainerOwnedPlanDecision Decide(
        string? expectedAppliedHash,
        string ownershipAppliedHash,
        string observedBeforeHash,
        bool matchesManagedPlan) {
        if (matchesManagedPlan && (
            string.Equals(observedBeforeHash, ownershipAppliedHash, StringComparison.Ordinal)
            || string.Equals(expectedAppliedHash, ownershipAppliedHash, StringComparison.Ordinal)
            || string.Equals(expectedAppliedHash, observedBeforeHash, StringComparison.Ordinal)))
            return AutoRetainerOwnedPlanDecision.Idempotent;
        return !string.IsNullOrWhiteSpace(expectedAppliedHash)
            && string.Equals(observedBeforeHash, expectedAppliedHash, StringComparison.Ordinal)
                ? AutoRetainerOwnedPlanDecision.Apply
                : AutoRetainerOwnedPlanDecision.Conflict;
    }
}

#if !GILLIONS_POLICY_TESTS
internal sealed class AutoRetainerVenturePlanWriter(IDalamudPluginInterface pluginInterface) {
    private readonly AutoRetainerIpc autoRetainerIpc = new(pluginInterface);

    public bool IsReady() {
        try {
            pluginInterface.GetIpcSubscriber<object>("AutoRetainer.Init").InvokeAction();
            return true;
        } catch {
            return false;
        }
    }

    public AutoRetainerPlanMutationOutcome Apply(
        ulong contentId,
        GillionsVenturePlanSpec plan,
        string? expectedAppliedHash,
        AutoRetainerPlanOwnershipState? ownership) {
        if (contentId == 0 || !VenturePlannerCapabilityPolicy.IsValid(plan)) return new(AutoRetainerPlanApplyResult.InvalidPlan);
        if (!IsReady()) return new(AutoRetainerPlanApplyResult.AutoRetainerUnavailable);
        try {
            // Every mutable AutoRetainer object is fetched, changed, written,
            // and read back synchronously during this one framework update.
            var data = ReadFresh(contentId, plan.RetainerName);
            if (data is null) return new(AutoRetainerPlanApplyResult.IpcRejected);
            var before = AutoRetainerVenturePlanMutation.Capture(data);
            var beforeHash = AutoRetainerVenturePlanMutation.Hash(before);
            var priorBackup = ownership?.PriorPlanBackup ?? before;
            var priorBackupHash = ownership?.PriorPlanBackupHash ?? beforeHash;
            if (ownership is null && !before.EnablePlanner)
                return new(AutoRetainerPlanApplyResult.PlannerDisabled, beforeHash, PriorPlanBackupHash: priorBackupHash, PriorPlanBackup: priorBackup);
            if (ownership is not null) {
                var decision = AutoRetainerOwnedPlanPolicy.Decide(
                    expectedAppliedHash,
                    ownership.AppliedHash,
                    beforeHash,
                    AutoRetainerVenturePlanMutation.MatchesManaged(before, plan));
                if (decision == AutoRetainerOwnedPlanDecision.Conflict)
                    return new(AutoRetainerPlanApplyResult.CasMismatch, beforeHash, PriorPlanBackupHash: priorBackupHash, PriorPlanBackup: priorBackup);
                if (decision == AutoRetainerOwnedPlanDecision.Idempotent)
                    return new(AutoRetainerPlanApplyResult.Idempotent, beforeHash, beforeHash, beforeHash, priorBackupHash, priorBackup);
            } else if (expectedAppliedHash is not null) {
                return new(AutoRetainerPlanApplyResult.CasMismatch, beforeHash, PriorPlanBackupHash: priorBackupHash, PriorPlanBackup: priorBackup);
            }

            AutoRetainerVenturePlanMutation.Apply(data, plan);
            Write(contentId, plan.RetainerName, data);
            var readBack = ReadFresh(contentId, plan.RetainerName);
            if (readBack is null) return new(AutoRetainerPlanApplyResult.ReadBackMismatch, beforeHash, PriorPlanBackupHash: priorBackupHash, PriorPlanBackup: priorBackup);
            var readBackState = AutoRetainerVenturePlanMutation.Capture(readBack);
            var readBackHash = AutoRetainerVenturePlanMutation.Hash(readBackState);
            if (!AutoRetainerVenturePlanMutation.MatchesManaged(readBackState, plan)) {
                TryRollback(contentId, plan.RetainerName, before);
                return new(AutoRetainerPlanApplyResult.ReadBackMismatch, beforeHash, readBackHash, readBackHash, priorBackupHash, priorBackup);
            }
            return new(AutoRetainerPlanApplyResult.Applied, beforeHash, readBackHash, readBackHash, priorBackupHash, priorBackup);
        } catch {
            return new(AutoRetainerPlanApplyResult.IpcRejected);
        }
    }

    public AutoRetainerPlanMutationOutcome Restore(
        ulong contentId,
        string retainerName,
        string expectedAppliedHash,
        AutoRetainerPlanOwnershipState ownership) {
        if (contentId == 0 || string.IsNullOrWhiteSpace(retainerName)) return new(AutoRetainerPlanApplyResult.InvalidPlan);
        if (!IsReady()) return new(AutoRetainerPlanApplyResult.AutoRetainerUnavailable);
        try {
            var data = ReadFresh(contentId, retainerName);
            if (data is null) return new(AutoRetainerPlanApplyResult.IpcRejected);
            var before = AutoRetainerVenturePlanMutation.Capture(data);
            var beforeHash = AutoRetainerVenturePlanMutation.Hash(before);
            if (string.Equals(beforeHash, ownership.PriorPlanBackupHash, StringComparison.Ordinal))
                return new(AutoRetainerPlanApplyResult.Idempotent, beforeHash, beforeHash, beforeHash, ownership.PriorPlanBackupHash, ownership.PriorPlanBackup);
            if (!string.Equals(expectedAppliedHash, ownership.AppliedHash, StringComparison.Ordinal)
                || !string.Equals(beforeHash, ownership.AppliedHash, StringComparison.Ordinal))
                return new(AutoRetainerPlanApplyResult.CasMismatch, beforeHash, PriorPlanBackupHash: ownership.PriorPlanBackupHash, PriorPlanBackup: ownership.PriorPlanBackup);

            AutoRetainerVenturePlanMutation.Restore(data, ownership.PriorPlanBackup);
            Write(contentId, retainerName, data);
            var readBack = ReadFresh(contentId, retainerName);
            if (readBack is null) return new(AutoRetainerPlanApplyResult.ReadBackMismatch, beforeHash, PriorPlanBackupHash: ownership.PriorPlanBackupHash, PriorPlanBackup: ownership.PriorPlanBackup);
            var readBackState = AutoRetainerVenturePlanMutation.Capture(readBack);
            var readBackHash = AutoRetainerVenturePlanMutation.Hash(readBackState);
            if (!string.Equals(readBackHash, ownership.PriorPlanBackupHash, StringComparison.Ordinal)) {
                TryRollback(contentId, retainerName, before);
                return new(AutoRetainerPlanApplyResult.ReadBackMismatch, beforeHash, readBackHash, readBackHash, ownership.PriorPlanBackupHash, ownership.PriorPlanBackup);
            }
            return new(AutoRetainerPlanApplyResult.Restored, beforeHash, readBackHash, readBackHash, ownership.PriorPlanBackupHash, ownership.PriorPlanBackup);
        } catch {
            return new(AutoRetainerPlanApplyResult.IpcRejected);
        }
    }

    private object? ReadFresh(ulong contentId, string retainerName) =>
        autoRetainerIpc.ReadAdditionalRetainerData(contentId, retainerName);

    private void Write(ulong contentId, string retainerName, object data) =>
        autoRetainerIpc.WriteAdditionalRetainerData(contentId, retainerName, data);

    private void TryRollback(ulong contentId, string retainerName, AutoRetainerVenturePlanBackup state) {
        try {
            var fresh = ReadFresh(contentId, retainerName);
            if (fresh is null) return;
            AutoRetainerVenturePlanMutation.Restore(fresh, state);
            Write(contentId, retainerName, fresh);
            _ = ReadFresh(contentId, retainerName);
        } catch { }
    }
}
#endif

internal static class AutoRetainerVenturePlanMutation {
    private static readonly JsonSerializerOptions CanonicalJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static AutoRetainerVenturePlanBackup Capture(object data) {
        var planObject = ReadMember(data, "VenturePlan") ?? throw new InvalidOperationException("AutoRetainer did not expose a venture plan.");
        var list = ReadMember(planObject, "List") as IList ?? throw new InvalidOperationException("AutoRetainer did not expose the venture plan list.");
        var steps = list.Cast<object>().Select(entry => new GillionsVenturePlanStep(
            Convert.ToUInt32(ReadMember(entry, "ID") ?? 0u),
            Convert.ToInt32(ReadMember(entry, "Num") ?? 0))).ToArray();
        return new AutoRetainerVenturePlanBackup(
            Convert.ToString(ReadMember(planObject, "Name")) ?? "",
            steps,
            NormalizeCompletionBehavior(ReadMember(planObject, "PlanCompleteBehavior")),
            Convert.ToString(ReadMember(data, "LinkedVenturePlan")) ?? "",
            Convert.ToUInt32(ReadMember(data, "VenturePlanIndex") ?? 0u),
            Convert.ToBoolean(ReadMember(data, "EnablePlanner") ?? false));
    }

    public static string Hash(AutoRetainerVenturePlanBackup state) {
        var canonical = new object[] {
            "autoretainer-plan-v1",
            state.Name,
            state.Steps.Select(step => new object[] { step.VentureId, step.Repetitions }).ToArray(),
            state.PlanCompleteBehavior,
            state.LinkedVenturePlan,
            state.VenturePlanIndex,
            state.EnablePlanner,
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical, CanonicalJson)))).ToLowerInvariant();
    }

    public static bool MatchesManaged(AutoRetainerVenturePlanBackup state, GillionsVenturePlanSpec plan) =>
        string.Equals(state.Name, VenturePlannerCapabilityPolicy.BuildManagedPlanName(plan.RetainerName), StringComparison.Ordinal)
        && state.Steps.SequenceEqual(plan.Steps)
        && string.Equals(state.PlanCompleteBehavior, plan.CompletionBehavior, StringComparison.Ordinal)
        && state.LinkedVenturePlan.Length == 0
        && state.VenturePlanIndex == 0
        && state.EnablePlanner;

    public static void Apply(object data, GillionsVenturePlanSpec plan) {
        if (!VenturePlannerCapabilityPolicy.IsValid(plan)) throw new ArgumentException("The Gillions venture plan is invalid.", nameof(plan));
        var planObject = ReadMember(data, "VenturePlan") ?? throw new InvalidOperationException("AutoRetainer did not expose a venture plan.");
        WriteMember(planObject, "Name", VenturePlannerCapabilityPolicy.BuildManagedPlanName(plan.RetainerName));
        ReplaceSteps(planObject, plan.Steps);
        WriteCompletionBehavior(planObject, plan.CompletionBehavior);
        WriteMember(data, "LinkedVenturePlan", "");
        WriteMember(data, "VenturePlanIndex", 0u);
        WriteMember(data, "EnablePlanner", true);
    }

    public static void Restore(object data, AutoRetainerVenturePlanBackup backup) {
        var planObject = ReadMember(data, "VenturePlan") ?? throw new InvalidOperationException("AutoRetainer did not expose a venture plan.");
        WriteMember(planObject, "Name", backup.Name);
        ReplaceSteps(planObject, backup.Steps);
        WriteCompletionBehavior(planObject, backup.PlanCompleteBehavior);
        WriteMember(data, "LinkedVenturePlan", backup.LinkedVenturePlan);
        WriteMember(data, "VenturePlanIndex", backup.VenturePlanIndex);
        WriteMember(data, "EnablePlanner", backup.EnablePlanner);
    }

    private static string NormalizeCompletionBehavior(object? value) => Convert.ToString(value) switch {
        "Restart_plan" => "restart_plan",
        "Assign_Quick_Venture" => "assign_quick_venture",
        "Do_nothing" => "do_nothing",
        "Repeat_last_venture" => "repeat_last_venture",
        _ => throw new InvalidOperationException("AutoRetainer exposed an unknown plan completion behavior."),
    };

    private static void WriteCompletionBehavior(object planObject, string behavior) {
        var member = planObject.GetType().GetField("PlanCompleteBehavior", BindingFlags.Instance | BindingFlags.Public) as MemberInfo
            ?? planObject.GetType().GetProperty("PlanCompleteBehavior", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMemberException(planObject.GetType().FullName, "PlanCompleteBehavior");
        var targetType = member is FieldInfo field ? field.FieldType : ((PropertyInfo)member).PropertyType;
        var apiName = behavior switch {
            "restart_plan" => "Restart_plan",
            "assign_quick_venture" => "Assign_Quick_Venture",
            "do_nothing" => "Do_nothing",
            "repeat_last_venture" => "Repeat_last_venture",
            _ => throw new InvalidOperationException("The requested completion behavior is unsupported."),
        };
        WriteMember(planObject, "PlanCompleteBehavior", Enum.Parse(targetType, apiName, ignoreCase: false));
    }

    private static void ReplaceSteps(object planObject, GillionsVenturePlanStep[] steps) {
        var list = ReadMember(planObject, "List") as IList ?? throw new InvalidOperationException("AutoRetainer did not expose the venture plan list.");
        var elementType = list.GetType().GetGenericArguments().SingleOrDefault()
            ?? throw new InvalidOperationException("AutoRetainer venture plan entries are unavailable.");
        list.Clear();
        foreach (var step in steps) {
            var entry = Activator.CreateInstance(elementType) ?? throw new InvalidOperationException("AutoRetainer venture plan entry could not be created.");
            WriteMember(entry, "ID", step.VentureId);
            WriteMember(entry, "Num", step.Repetitions);
            list.Add(entry);
        }
    }

    private static object? ReadMember(object target, string name) {
        var type = target.GetType();
        return type.GetField(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(target)
            ?? type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(target);
    }

    private static void WriteMember(object target, string name, object value) {
        var type = target.GetType();
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
        if (field is not null) {
            field.SetValue(target, ConvertValue(value, field.FieldType));
            return;
        }
        var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        if (property?.CanWrite == true) {
            property.SetValue(target, ConvertValue(value, property.PropertyType));
            return;
        }
        throw new MissingMemberException(type.FullName, name);
    }

    private static object ConvertValue(object value, Type targetType) {
        if (targetType.IsInstanceOfType(value)) return value;
        if (targetType.IsEnum) return value is string text ? Enum.Parse(targetType, text, false) : Enum.ToObject(targetType, value);
        return Convert.ChangeType(value, targetType);
    }
}
