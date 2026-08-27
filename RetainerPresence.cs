using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
#if !GILLIONS_POLICY_TESTS
using Dalamud.Plugin;
#endif

namespace GillionsGameSync;

public static class RetainerCapabilities {
    public static readonly string[] Client = [
        "retainer.observations.v1",
        "retainer.results.v1",
        "retainer.results.exact-ack.v1",
        "retainer.presence.v1",
        "retainer.plan-delivery.v1",
        "retainer.plan-ack.v1",
        "retainer.autoretainer.plan-apply.v1",
        "retainer.autoretainer.plan-limit-500.v1",
        "retainer.autoretainer.restart-completion.v1",
        "retainer.autoretainer.per-retainer-readiness.v1",
        "retainer.autoretainer.quick-completion.v1",
        "retainer.autoretainer.quick-completion-readback.v1",
        "retainer.autoretainer.do-nothing-completion.v1",
    ];
}

public sealed record RetainerPresenceCharacter(
    [property: JsonPropertyName("contentId")] string ContentId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("world")] string World);

public sealed record AutoRetainerPresenceDocument(
    [property: JsonPropertyName("installed")] bool Installed,
    [property: JsonPropertyName("loaded")] bool Loaded,
    [property: JsonPropertyName("apiReady")] bool ApiReady,
    [property: JsonPropertyName("suppressed")] bool? Suppressed,
    [property: JsonPropertyName("multiModeEnabled")] bool? MultiModeEnabled,
    [property: JsonPropertyName("characterEnabled")] bool? CharacterEnabled,
    [property: JsonPropertyName("retainerPlannerEnabled")] bool? RetainerPlannerEnabled,
    [property: JsonPropertyName("retainerPlannerReadiness")] RetainerPlannerReadinessDocument[] RetainerPlannerReadiness,
    [property: JsonPropertyName("plannerOptIn")] bool PlannerOptIn,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("maximumPlanExecutions")] int? MaximumPlanExecutions,
    [property: JsonPropertyName("supportedCompletionActions")] string[] SupportedCompletionActions,
    [property: JsonPropertyName("capabilities")] string[] Capabilities);

public sealed record RetainerPlannerReadinessDocument(
    [property: JsonPropertyName("retainerId")] string RetainerId,
    [property: JsonPropertyName("retainerName")] string? RetainerName,
    [property: JsonPropertyName("plannerEnabled")] bool? PlannerEnabled,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("observedAtUtc")] DateTime ObservedAtUtc);

internal enum RetainerPlannerReadinessResult {
    Ready,
    Disabled,
    Unavailable,
    Stale,
    Missing,
    Ambiguous,
}

internal static class RetainerPlannerReadinessPolicy {
    public static RetainerPlannerReadinessDocument Observation(
        string retainerId,
        string? retainerName,
        bool? plannerEnabled,
        DateTime observedAtUtc) => new(
            retainerId,
            retainerName,
            plannerEnabled,
            plannerEnabled switch { true => "ready", false => "disabled", null => "unavailable" },
            observedAtUtc);

    public static RetainerPlannerReadinessResult Evaluate(
        IEnumerable<RetainerPlannerReadinessDocument> observations,
        string retainerId,
        DateTime nowUtc,
        TimeSpan maximumAge) {
        if (string.IsNullOrWhiteSpace(retainerId)) return RetainerPlannerReadinessResult.Missing;
        var matches = observations.Where(entry => string.Equals(entry.RetainerId, retainerId, StringComparison.Ordinal)).Take(2).ToArray();
        if (matches.Length == 0) return RetainerPlannerReadinessResult.Missing;
        if (matches.Length > 1) return RetainerPlannerReadinessResult.Ambiguous;
        var match = matches[0];
        if (match.ObservedAtUtc.ToUniversalTime() < nowUtc.ToUniversalTime().Subtract(maximumAge)) return RetainerPlannerReadinessResult.Stale;
        return match.Status switch {
            "ready" when match.PlannerEnabled == true => RetainerPlannerReadinessResult.Ready,
            "disabled" when match.PlannerEnabled == false => RetainerPlannerReadinessResult.Disabled,
            _ => RetainerPlannerReadinessResult.Unavailable,
        };
    }
}

public sealed record RetainerPresenceDocument(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("character")] RetainerPresenceCharacter Character,
    [property: JsonPropertyName("observedAtUtc")] DateTime ObservedAtUtc,
    [property: JsonPropertyName("clientProduct")] string ClientProduct,
    [property: JsonPropertyName("clientChannel")] string ClientChannel,
    [property: JsonPropertyName("clientVersion")] string ClientVersion,
    [property: JsonPropertyName("contractVersion")] int ContractVersion,
    [property: JsonPropertyName("capabilities")] string[] Capabilities,
    [property: JsonPropertyName("autoRetainer")] AutoRetainerPresenceDocument AutoRetainer,
    [property: JsonPropertyName("appliedPlans")] RetainerAppliedPlanDocument[] AppliedPlans);

public sealed record AutoRetainerObservationProbe(
    AutoRetainerPresenceDocument Presence,
    AutoRetainerStatsRead[] Stats);

public static class RetainerPresenceResponsePolicy {
    public static bool TryParse(
        string json,
        RetainerClientProfile client,
        out bool uploadSupported,
        out bool plannerSupported) {
        uploadSupported = false;
        plannerSupported = false;
        try {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True
                || !root.TryGetProperty("schemaVersion", out var schema) || schema.GetInt32() != 1
                || !root.TryGetProperty("recommendedHeartbeatSeconds", out var heartbeat) || heartbeat.GetInt32() is < 5 or > 300
                || !root.TryGetProperty("onlineWindowSeconds", out var online) || online.GetInt32() < heartbeat.GetInt32()
                || !root.TryGetProperty("maximumBackoffSeconds", out var backoff) || backoff.GetInt32() is < 30 or > 900
                || !root.TryGetProperty("featureCompatibility", out var compatibility) || compatibility.ValueKind != JsonValueKind.Object
                || !compatibility.TryGetProperty("observations", out var observations)
                || !compatibility.TryGetProperty("results", out var results)
                || !compatibility.TryGetProperty("planner", out var planner)) return false;
            var hasAcceptedProduct = root.TryGetProperty("acceptedClientProduct", out var acceptedProduct)
                && acceptedProduct.ValueKind == JsonValueKind.String;
            var acceptedContractVersion = 0;
            var hasAcceptedContract = root.TryGetProperty("acceptedContractVersion", out var acceptedContract)
                && acceptedContract.ValueKind == JsonValueKind.Number
                && acceptedContract.TryGetInt32(out acceptedContractVersion);
            if ((hasAcceptedProduct && !string.Equals(acceptedProduct.GetString(), client.ProductName, StringComparison.Ordinal))
                || (hasAcceptedContract && acceptedContractVersion != RetainerClientPolicy.ContractVersion)
                || (client.RequiresExplicitServerProductAcceptance && (!hasAcceptedProduct || !hasAcceptedContract))) return false;
            uploadSupported = observations.GetString() == "supported" && results.GetString() == "supported";
            plannerSupported = planner.GetString() == "supported";
            return true;
        } catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException) { return false; }
    }
}

#if !GILLIONS_POLICY_TESTS
internal sealed class AutoRetainerObservationReader(IDalamudPluginInterface pluginInterface) {
    private readonly AutoRetainerIpc autoRetainerIpc = new(pluginInterface);

    public AutoRetainerObservationProbe Read(ulong contentId, IEnumerable<RetainerVentureProfile> retainers, bool plannerOptIn, DateTime observedAtUtc) {
        var plugin = pluginInterface.InstalledPlugins.FirstOrDefault(entry =>
            string.Equals(entry.InternalName, "AutoRetainer", StringComparison.OrdinalIgnoreCase));
        var installed = plugin is not null;
        var loaded = plugin?.IsLoaded == true;
        var version = plugin is null ? null : Convert.ToString(ReadMember(plugin, "Version"));
        if (!loaded) return new(new(installed, false, false, null, null, null, null, [], plannerOptIn, version, null, [], []), []);

        var apiReady = InvokeReady();
        if (!apiReady) return new(new(true, true, false, null, null, null, null, [], plannerOptIn, version, null, [], []), []);
        var suppressed = TryInvokeValue<bool>("AutoRetainer.GetSuppressed");
        var multiModeEnabled = TryInvokeValue<bool>("AutoRetainer.GetMultiModeEnabled");
        var enabledRetainers = TryInvokeReference<Dictionary<ulong, HashSet<string>>>("AutoRetainer.PluginState.GetEnabledRetainers")
            ?? TryInvokeReference<Dictionary<ulong, HashSet<string>>>("AutoRetainer.GetConfig.SelectedRetainers");
        bool? characterEnabled = enabledRetainers is null ? null : enabledRetainers.TryGetValue(contentId, out var selected) && selected.Count > 0;
        var offline = TryInvoke<ulong, object>("AutoRetainer.GetOfflineCharacterData", contentId);
        var offlineRetainers = ReadMember(offline, "RetainerData") as IEnumerable;
        var stats = new List<AutoRetainerStatsRead>();
        var plannerReadiness = new List<RetainerPlannerReadinessDocument>();
        foreach (var retainer in retainers) {
            var additional = TryReadAdditionalRetainerData(contentId, retainer.Name ?? "");
            var plannerEnabled = additional is null ? null : ReadBoolean(additional, "EnablePlanner");
            plannerReadiness.Add(RetainerPlannerReadinessPolicy.Observation(
                retainer.RetainerId,
                retainer.Name,
                plannerEnabled,
                observedAtUtc));
            if (additional is null) continue;
            var itemLevel = ReadNonNegative(additional, "Ilvl");
            var gathering = ReadNonNegative(additional, "Gathering");
            var perception = ReadNonNegative(additional, "Perception");
            DateTime? startedAtUtc = null;
            if (offlineRetainers is not null) foreach (var entry in offlineRetainers) {
                var id = Convert.ToString(ReadMember(entry, "RetainerID"));
                if (id != retainer.RetainerId) continue;
                var seconds = ReadInt64(entry, "VentureBeginsAt");
                if (seconds is > 0) {
                    try { startedAtUtc = DateTimeOffset.FromUnixTimeSeconds(seconds.Value).UtcDateTime; } catch (ArgumentOutOfRangeException) { }
                }
                break;
            }
            stats.Add(new(retainer.RetainerId, observedAtUtc, itemLevel, gathering, perception, startedAtUtc));
        }
        var completionActions = new[] { "restart_plan", "assign_quick_venture", "do_nothing" };
        var capabilities = new[] {
            "retainer.autoretainer.plan-apply.v1",
            "retainer.autoretainer.plan-limit-500.v1",
            "retainer.autoretainer.restart-completion.v1",
            "retainer.autoretainer.per-retainer-readiness.v1",
            "retainer.autoretainer.quick-completion.v1",
            "retainer.autoretainer.quick-completion-readback.v1",
            "retainer.autoretainer.do-nothing-completion.v1",
        };
        // The legacy aggregate readiness field is intentionally null. A true
        // value could be borrowed from a different retainer by an older server.
        return new(new(true, true, true, suppressed, multiModeEnabled, characterEnabled, null,
            plannerReadiness.ToArray(), plannerOptIn, version, VenturePlannerCapabilityPolicy.MaximumPendingExecutions,
            completionActions, capabilities), stats.ToArray());
    }

    private bool InvokeReady() {
        try { pluginInterface.GetIpcSubscriber<object>("AutoRetainer.Init").InvokeAction(); return true; }
        catch { return false; }
    }

    private object? TryReadAdditionalRetainerData(ulong contentId, string retainerName) {
        try { return autoRetainerIpc.ReadAdditionalRetainerData(contentId, retainerName); }
        catch { return null; }
    }

    private T? TryInvokeValue<T>(string name) where T : struct {
        try { return pluginInterface.GetIpcSubscriber<T>(name).InvokeFunc(); } catch { return null; }
    }

    private T? TryInvokeReference<T>(string name) where T : class {
        try { return pluginInterface.GetIpcSubscriber<T>(name).InvokeFunc(); } catch { return null; }
    }

    private TOutput? TryInvoke<TInput, TOutput>(string name, TInput input) where TOutput : class {
        try { return pluginInterface.GetIpcSubscriber<TInput, TOutput>(name).InvokeFunc(input); } catch { return null; }
    }

    private TOutput? TryInvoke<TInput1, TInput2, TOutput>(string name, TInput1 input1, TInput2 input2) where TOutput : class {
        try { return pluginInterface.GetIpcSubscriber<TInput1, TInput2, TOutput>(name).InvokeFunc(input1, input2); } catch { return null; }
    }

    private static object? ReadMember(object? target, string name) {
        if (target is null) return null;
        var type = target.GetType();
        return type.GetField(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(target)
            ?? type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(target);
    }

    private static int? ReadNonNegative(object target, string name) {
        try { var value = Convert.ToInt32(ReadMember(target, name) ?? -1); return value >= 0 ? value : null; }
        catch { return null; }
    }

    private static long? ReadInt64(object target, string name) {
        try { return Convert.ToInt64(ReadMember(target, name) ?? -1L); } catch { return null; }
    }

    private static bool? ReadBoolean(object target, string name) {
        try { return Convert.ToBoolean(ReadMember(target, name)); } catch { return null; }
    }
}
#endif
