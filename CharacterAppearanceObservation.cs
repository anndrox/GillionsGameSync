using System;
using System.Text.Json.Serialization;

namespace GillionsGameSync;

// Transport evidence only: playable-profile/package policy belongs to the consumer.
// No generated record ToString: customization is private, not diagnostics.
public sealed class CharacterAppearanceObservation {
    [JsonPropertyName("schemaVersion")] public int SchemaVersion => 1;
    [JsonPropertyName("source")] public string Source => "local-player-customize-player-state";
    [JsonPropertyName("customizationLayout")] public string CustomizationLayout => "ffxiv-customize-26-v1";
    private CharacterAppearanceObservation() { }
    [JsonPropertyName("state")] public string State { get; private init; } = "unavailable";
    [JsonPropertyName("observedAt")] public string ObservedAt { get; private init; } = "";
    [JsonPropertyName("gameBuild")] public string? GameBuild { get; private init; }
    [JsonPropertyName("reason")] public string? Reason { get; private init; }
    [JsonPropertyName("customizationHex")] public string? CustomizationHex { get; private init; }
    [JsonPropertyName("race")] public byte? Race { get; private init; }
    [JsonPropertyName("tribe")] public byte? Tribe { get; private init; }
    [JsonPropertyName("modelSex")] public byte? ModelSex { get; private init; }

    public static CharacterAppearanceObservation Capture(ReadOnlySpan<byte> bytes, string? gameBuild,
        DateTime observedAt, bool loadedOwnedPlayer, byte race, byte tribe, byte sex, bool transformed) {
        var reason = !loadedOwnedPlayer ? "PLAYER_UNAVAILABLE"
            : !ValidBuild(gameBuild) ? "BUILD_UNAVAILABLE"
            : transformed ? "TRANSFORMED_PLAYER"
            : bytes.Length != 26 ? "CUSTOMIZATION_UNAVAILABLE"
            : bytes[0] != race || bytes[4] != tribe || bytes[1] != sex ? "IDENTITY_MISMATCH"
            : race is < 1 or > 8 || tribe is < 1 or > 16 || sex > 1 || bytes[2] != 1 ? "NON_PLAYABLE_IDENTITY"
            : null;
        return new CharacterAppearanceObservation {
            State = reason is null ? "complete" : "unavailable",
            ObservedAt = observedAt.ToUniversalTime().ToString("O"),
            GameBuild = ValidBuild(gameBuild) ? gameBuild : null,
            Reason = reason,
            CustomizationHex = reason is null ? Convert.ToHexString(bytes).ToLowerInvariant() : null,
            Race = reason is null ? race : null, Tribe = reason is null ? tribe : null, ModelSex = reason is null ? sex : null,
        };
    }

    private static bool ValidBuild(string? value) => value is { Length: > 0 and <= 80 }
        && System.Text.RegularExpressions.Regex.IsMatch(value, @"\A[0-9]+(?:\.[0-9]+){2,6}\z");
}
