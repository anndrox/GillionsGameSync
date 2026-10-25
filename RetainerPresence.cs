using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GillionsGameSync;

public static class RetainerCapabilities {
    public static readonly string[] Client = [
        "retainer.observations.v1",
        "retainer.results.v1",
        "retainer.results.exact-ack.v1",
        "retainer.presence.v1",
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
    [property: JsonPropertyName("retainerPlannerReadiness")] object[] RetainerPlannerReadiness,
    [property: JsonPropertyName("plannerOptIn")] bool PlannerOptIn,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("maximumPlanExecutions")] int? MaximumPlanExecutions,
    [property: JsonPropertyName("supportedCompletionActions")] string[] SupportedCompletionActions,
    [property: JsonPropertyName("capabilities")] string[] Capabilities);

// The plugin's own switch value for each Site permission key, as it applies while
// the member has never chosen on the website (Site "legacy"). Site displays these
// only in that state; they are never a website decision, consent or grant.
// Owner-approved 2026-10-25. Nothing here is read from the game.
public sealed record PresenceLocalPreferences(
    [property: JsonPropertyName("marketContribution")] bool MarketContribution,
    [property: JsonPropertyName("partyFinderContribution")] bool PartyFinderContribution,
    [property: JsonPropertyName("personalHunts")] bool PersonalHunts,
    [property: JsonPropertyName("personalSubmarines")] bool PersonalSubmarines,
    [property: JsonPropertyName("huntRoutingLocation")] bool HuntRoutingLocation,
    [property: JsonPropertyName("itemLinks")] bool ItemLinks,
    [property: JsonPropertyName("partyFinderLinks")] bool PartyFinderLinks,
    [property: JsonPropertyName("automaticHuntMaps")] bool AutomaticHuntMaps);

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
    [property: JsonPropertyName("appliedPlans")] object[] AppliedPlans,
    // Optional, ignored by Sites that predate them (Site PR #137); omitted when unknown.
    [property: JsonPropertyName("localPreferences"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PresenceLocalPreferences? LocalPreferences = null,
    [property: JsonPropertyName("gameVersion"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? GameVersion = null);

public static class RetainerPresenceResponsePolicy {
    public static bool TryParse(
        string json,
        RetainerClientProfile client,
        out bool uploadSupported) {
        uploadSupported = false;
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
                || observations.ValueKind != JsonValueKind.String || results.ValueKind != JsonValueKind.String) return false;
            var hasAcceptedProduct = root.TryGetProperty("acceptedClientProduct", out var acceptedProduct)
                && acceptedProduct.ValueKind == JsonValueKind.String;
            var acceptedContractVersion = 0;
            var hasAcceptedContract = root.TryGetProperty("acceptedContractVersion", out var acceptedContract)
                && acceptedContract.ValueKind == JsonValueKind.Number
                && acceptedContract.TryGetInt32(out acceptedContractVersion);
            if ((hasAcceptedProduct && !string.Equals(acceptedProduct.GetString(), client.ProductName, StringComparison.Ordinal))
                || (hasAcceptedContract && acceptedContractVersion != RetainerClientPolicy.ContractVersion)) return false;
            // Missing explicit acceptance withholds Retainer uploads only. The
            // presence (and its separately device-bound permission lease) stays valid.
            uploadSupported = (!client.RequiresExplicitServerProductAcceptance || hasAcceptedProduct && hasAcceptedContract)
                && observations.GetString() == "supported" && results.GetString() == "supported";
            return true;
        } catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException) { return false; }
    }
}
