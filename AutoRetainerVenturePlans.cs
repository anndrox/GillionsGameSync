using System;
using System.Collections;
using System.Linq;
using System.Reflection;
#if !GILLIONS_POLICY_TESTS
using Dalamud.Plugin;
#endif

namespace GillionsGameSync;

public sealed record GillionsVenturePlanStep(uint VentureId, int Repetitions);

internal sealed record GillionsVenturePlanSpec(
    string RetainerId,
    string RetainerName,
    GillionsVenturePlanStep[] Steps);

public sealed record AutoRetainerVenturePlanBackup(
    string Name,
    GillionsVenturePlanStep[] Steps,
    string LinkedVenturePlan,
    uint VenturePlanIndex,
    bool EnablePlanner);

internal enum AutoRetainerPlanApplyResult {
    Applied,
    Disabled,
    AutoRetainerUnavailable,
    InvalidPlan,
    IpcRejected,
}

internal static class VenturePlannerCapabilityPolicy {
    public static bool IsAvailable(bool enabled, bool autoRetainerLoaded, bool apiReady, bool paired, bool rosterComplete) =>
        enabled && autoRetainerLoaded && apiReady && paired && rosterComplete;

    public static bool IsValid(GillionsVenturePlanSpec? plan) => plan is {
        RetainerId.Length: > 0,
        RetainerName.Length: > 0,
        Steps.Length: > 0 and <= 50,
    } && ulong.TryParse(plan.RetainerId, out var retainerId) && retainerId > 0
        && plan.RetainerName.Length <= 64
        && plan.Steps.All(step => step.VentureId > 0 && step.Repetitions is > 0 and <= 999);

    public static string BuildManagedPlanName(string retainerName) => $"Gillions Venture ({retainerName.Trim()})";
}

#if !GILLIONS_POLICY_TESTS
internal sealed class AutoRetainerVenturePlanWriter(IDalamudPluginInterface pluginInterface) {
    public bool IsReady() {
        try {
            pluginInterface.GetIpcSubscriber<object>("AutoRetainer.Init").InvokeAction();
            return true;
        } catch {
            return false;
        }
    }

    public AutoRetainerPlanApplyResult Apply(ulong contentId, GillionsVenturePlanSpec plan, out AutoRetainerVenturePlanBackup? backup) {
        backup = null;
        if (contentId == 0 || !VenturePlannerCapabilityPolicy.IsValid(plan)) return AutoRetainerPlanApplyResult.InvalidPlan;
        if (!IsReady()) return AutoRetainerPlanApplyResult.AutoRetainerUnavailable;
        try {
            // AutoRetainer requires its mutable configuration object to be read,
            // changed, and written back during the same framework update. Keep
            // the provider's runtime object rather than persisting or cloning it.
            var data = pluginInterface
                .GetIpcSubscriber<ulong, string, object>("AutoRetainer.GetAdditionalRetainerData")
                .InvokeFunc(contentId, plan.RetainerName);
            if (data is null) return AutoRetainerPlanApplyResult.IpcRejected;
            backup = AutoRetainerVenturePlanMutation.Capture(data);
            AutoRetainerVenturePlanMutation.Apply(data, plan);
            pluginInterface
                .GetIpcSubscriber<ulong, string, object, object>("AutoRetainer.WriteAdditionalRetainerData")
                .InvokeAction(contentId, plan.RetainerName, data);
            return AutoRetainerPlanApplyResult.Applied;
        } catch {
            return AutoRetainerPlanApplyResult.IpcRejected;
        }
    }

    public AutoRetainerPlanApplyResult Restore(ulong contentId, string retainerName, AutoRetainerVenturePlanBackup backup) {
        if (contentId == 0 || string.IsNullOrWhiteSpace(retainerName)) return AutoRetainerPlanApplyResult.InvalidPlan;
        if (!IsReady()) return AutoRetainerPlanApplyResult.AutoRetainerUnavailable;
        try {
            var data = pluginInterface
                .GetIpcSubscriber<ulong, string, object>("AutoRetainer.GetAdditionalRetainerData")
                .InvokeFunc(contentId, retainerName);
            if (data is null) return AutoRetainerPlanApplyResult.IpcRejected;
            AutoRetainerVenturePlanMutation.Restore(data, backup);
            pluginInterface
                .GetIpcSubscriber<ulong, string, object, object>("AutoRetainer.WriteAdditionalRetainerData")
                .InvokeAction(contentId, retainerName, data);
            return AutoRetainerPlanApplyResult.Applied;
        } catch {
            return AutoRetainerPlanApplyResult.IpcRejected;
        }
    }
}
#endif

internal static class AutoRetainerVenturePlanMutation {
    public static AutoRetainerVenturePlanBackup Capture(object data) {
        var planObject = ReadMember(data, "VenturePlan") ?? throw new InvalidOperationException("AutoRetainer did not expose a venture plan.");
        var list = ReadMember(planObject, "List") as IList ?? throw new InvalidOperationException("AutoRetainer did not expose the venture plan list.");
        var steps = list.Cast<object>().Select(entry => new GillionsVenturePlanStep(
            Convert.ToUInt32(ReadMember(entry, "ID") ?? 0u),
            Convert.ToInt32(ReadMember(entry, "Num") ?? 0))).ToArray();
        return new AutoRetainerVenturePlanBackup(
            Convert.ToString(ReadMember(planObject, "Name")) ?? "",
            steps,
            Convert.ToString(ReadMember(data, "LinkedVenturePlan")) ?? "",
            Convert.ToUInt32(ReadMember(data, "VenturePlanIndex") ?? 0u),
            Convert.ToBoolean(ReadMember(data, "EnablePlanner") ?? false));
    }

    public static void Apply(object data, GillionsVenturePlanSpec plan) {
        if (!VenturePlannerCapabilityPolicy.IsValid(plan)) throw new ArgumentException("The Gillions venture plan is invalid.", nameof(plan));
        var planObject = ReadMember(data, "VenturePlan") ?? throw new InvalidOperationException("AutoRetainer did not expose a venture plan.");
        WriteMember(planObject, "Name", VenturePlannerCapabilityPolicy.BuildManagedPlanName(plan.RetainerName));
        ReplaceSteps(planObject, plan.Steps);

        // A Gillions-managed plan is embedded directly on this retainer. It
        // does not alter AutoRetainer's global saved-plan list.
        WriteMember(data, "LinkedVenturePlan", "");
        WriteMember(data, "VenturePlanIndex", 0u);
        WriteMember(data, "EnablePlanner", true);
    }

    public static void Restore(object data, AutoRetainerVenturePlanBackup backup) {
        var planObject = ReadMember(data, "VenturePlan") ?? throw new InvalidOperationException("AutoRetainer did not expose a venture plan.");
        WriteMember(planObject, "Name", backup.Name);
        ReplaceSteps(planObject, backup.Steps);
        WriteMember(data, "LinkedVenturePlan", backup.LinkedVenturePlan);
        WriteMember(data, "VenturePlanIndex", backup.VenturePlanIndex);
        WriteMember(data, "EnablePlanner", backup.EnablePlanner);
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

    private static object ConvertValue(object value, Type targetType) => targetType.IsInstanceOfType(value)
        ? value
        : Convert.ChangeType(value, targetType);
}
