#if GILLIONS_TEST_BUILD || GILLIONS_PERSONAL_STATE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GillionsGameSync;

// No credentials or raw character/account IDs. Private pending payloads use the
// existing configuration custody; never export this state as diagnostics.
public sealed class PersonalSyncState {
    public int SchemaVersion { get; set; } = 1;
    public List<PersonalPreparedSnapshot> Prepared { get; set; } = [];
}
public sealed record PersonalPreparedSnapshot(string OwnerKey, string Resource, string Nonce,
    string Payload, string PayloadHash) {
    public bool Acknowledged { get; set; }
    public bool Blocked { get; set; }
}
internal static class PersonalSyncPolicy {
    internal static readonly string[] Resources = ["hunt_bills", "submarine_personal"];
    internal const string Origin = "https://test.gillions.app";
    internal const string Endpoint = Origin + "/api/game-sync/sync";
    internal const string Contract = "personal-observations-v1";
    internal const int MaximumPayloadBytes = 65536;
    internal const int MaximumPrepared = 16;
    internal const int MaximumStateBytes = 1152 * 1024;
    internal static string Owner(string generation, string characterKey) => Hash(generation + ":" + characterKey);
    internal static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    internal static string? Schema(string resource) => resource switch {
        "hunt_bills" => "hunt-bills-v1", "submarine_personal" => "submarine-personal-v1", _ => null
    };
    internal static string? Capability(string resource) => resource switch {
        "hunt_bills" => "hunt_bills_v1", "submarine_personal" => "submarine_personal_v1", _ => null
    };
    internal static bool Compatible(string json, string resource) {
        try {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            var root = doc.RootElement;
            if (root.GetProperty("ok").ValueKind != JsonValueKind.True
                || root.GetProperty("acceptedClientProduct").GetString() != "GillionsGameSyncTest") return false;
            var ack = root.GetProperty("personalObservations");
            if (ack.GetProperty("contractVersion").GetInt32() != 1 || ack.GetProperty("endpoint").GetString() != Endpoint) return false;
            var matches = ack.GetProperty("resources").EnumerateArray().Where(r => r.GetProperty("resourceType").GetString() == resource).ToArray();
            return Schema(resource) is not null && matches.Length == 1
                && matches[0].GetProperty("schemaVersion").GetInt32() == 1
                && matches[0].GetProperty("collectorSchema").GetString() == Schema(resource)
                && matches[0].GetProperty("capability").GetString() == Capability(resource)
                && matches[0].GetProperty("maxPayloadBytes").GetInt32() == MaximumPayloadBytes;
        } catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { return false; }
    }
    internal static bool PayloadValid(string resource, string payload) {
        try {
            if (Encoding.UTF8.GetByteCount(payload) > MaximumPayloadBytes || Schema(resource) is null) return false;
            using var doc = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 16 });
            var root = doc.RootElement;
            return root.GetProperty("schemaVersion").GetInt32() == 1
                && root.GetProperty("collectorSchema").GetString() == Schema(resource)
                && root.GetProperty(resource == "hunt_bills" ? "bills" : "slots").GetArrayLength() is > 0
                && root.GetProperty("uploadState").GetString() == "local-only-no-server-contract";
        } catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { return false; }
    }
    internal static bool Valid(PersonalSyncState state) {
        try {
            return state.SchemaVersion == 1 && state.Prepared is not null && state.Prepared.Count <= MaximumPrepared
                && state.Prepared.All(p => p is not null && PersonalObservationCompatibility.Key(p.OwnerKey)
                    && Guid.TryParseExact(p.Nonce, "N", out _) && PayloadValid(p.Resource, p.Payload) && p.PayloadHash == Hash(p.Payload))
                && state.Prepared.Select(p => p.OwnerKey + ":" + p.Resource).Distinct().Count() == state.Prepared.Count
                && JsonSerializer.SerializeToUtf8Bytes(state).Length <= MaximumStateBytes;
        } catch (Exception) { return false; }
    }
    // One latest prepared snapshot per owner/resource. Never overwrite unacked
    // content or nonce. ACKed entries permit a successor; capacity never evicts.
    internal static PersonalPreparedSnapshot? Prepare(PersonalSyncState state, string owner, string resource, string payload) {
        if (!Valid(state) || !PersonalObservationCompatibility.Key(owner) || !PayloadValid(resource, payload)) return null;
        var prior = state.Prepared.SingleOrDefault(p => p.OwnerKey == owner && p.Resource == resource);
        if (prior is not null && (!prior.Acknowledged || prior.PayloadHash == Hash(payload))) return prior;
        if (prior is null && state.Prepared.Count >= MaximumPrepared) return null;
        var next = new PersonalPreparedSnapshot(owner, resource, Guid.NewGuid().ToString("N"), payload, Hash(payload));
        var candidate = new PersonalSyncState { Prepared = state.Prepared.Where(p => p != prior).Append(next).ToList() };
        if (!Valid(candidate)) return null;
        state.Prepared = candidate.Prepared;
        return next;
    }
    internal static bool CanSend(bool sync, bool retain, string origin, bool accepted, PersonalPreparedSnapshot? prepared) =>
        sync && retain && origin == Origin && accepted && prepared is { Acknowledged: false, Blocked: false };
    internal static bool Receipt(string json) {
        try {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            var root = doc.RootElement;
            return root.GetProperty("ok").ValueKind == JsonValueKind.True
                && Guid.TryParse(root.GetProperty("snapshotId").GetString(), out var id) && id != Guid.Empty
                && root.GetProperty("unchanged").ValueKind is JsonValueKind.True or JsonValueKind.False
                && DateTimeOffset.TryParse(root.GetProperty("receivedAt").GetString(), System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var received) && received >= DateTimeOffset.UnixEpoch;
        } catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { return false; }
    }
    internal static int RetrySeconds(int failures) => (int)Math.Min(900, 60 * Math.Pow(2, Math.Clamp(failures, 0, 4)));
}
#endif
