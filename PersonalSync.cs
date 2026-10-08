#if GILLIONS_TEST_BUILD || GILLIONS_PUBLIC_BUILD || GILLIONS_PERSONAL_STATE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

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
internal enum PersonalResponseDisposition { Canceled, Acknowledged, Blocked, Retry }
internal static class PersonalSyncPolicy {
    internal static readonly string[] Resources = ["hunt_bills", "submarine_personal"];
    internal const string Origin = "https://test.gillions.app";
    internal const string Endpoint = Origin + "/api/game-sync/sync";
    internal const string Contract = "personal-observations-v1";
    internal const string HuntCoverageCapability = "hunt_bills_v2";
    internal const string HuntCoverageHeader = "X-Gillions-Hunt-Item-Coverage";
    internal const int MaximumPayloadBytes = 65536;
    internal const int MaximumPrepared = 16;
    internal const int MaximumStateBytes = 1152 * 1024;
    internal static string Owner(string generation, string characterKey) => Hash(generation + ":" + characterKey);
    internal static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    // Payload/nonce custody still uses the EXACT hash. Only admission of an ACKed
    // Hunt successor ignores routine observation identity/time refreshes.
    internal static bool SameObservation(string resource, string first, string second) {
        if (first == second) return true;
        if (resource != "hunt_bills") return false;
        JsonNode? Semantic(string payload) {
            var node = JsonNode.Parse(payload);
            if (node?["bills"] is JsonArray bills)
                foreach (var bill in bills.OfType<JsonObject>()) {
                    bill.Remove("observationId"); bill.Remove("observedAtUtc");
                }
            return node;
        }
        try { return JsonNode.DeepEquals(Semantic(first), Semantic(second)); }
        catch (JsonException) { return false; }
    }
    internal static string? Schema(string resource) => resource switch {
        "hunt_bills" => "hunt-bills-v1", "submarine_personal" => "submarine-personal-v1", _ => null
    };
    internal static string? Capability(string resource) => resource switch {
        "hunt_bills" => "hunt_bills_v1", "submarine_personal" => "submarine_personal_v1", _ => null
    };
    internal static bool Compatible(string json, string resource, string origin = Origin) {
        if (!NativeProduct.TransportOrigin(origin)) return false;
        try {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            var root = doc.RootElement;
            if (root.GetProperty("ok").ValueKind != JsonValueKind.True
                || root.GetProperty("acceptedClientProduct").GetString() != NativeProduct.Name) return false;
            var ack = root.GetProperty("personalObservations");
            if (ack.GetProperty("contractVersion").GetInt32() != 1 || ack.GetProperty("endpoint").GetString() != origin + "/api/game-sync/sync") return false;
            var matches = ack.GetProperty("resources").EnumerateArray().Where(r => r.GetProperty("resourceType").GetString() == resource).ToArray();
            return Schema(resource) is not null && matches.Length == 1
                && matches[0].GetProperty("schemaVersion").GetInt32() == 1
                && matches[0].GetProperty("collectorSchema").GetString() == Schema(resource)
                && matches[0].GetProperty("capability").GetString() == Capability(resource)
                && matches[0].GetProperty("maxPayloadBytes").GetInt32() == MaximumPayloadBytes;
        } catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { return false; }
    }
    internal static bool HuntCoverageCompatible(string json, string origin = Origin) {
        if (!NativeProduct.TransportOrigin(origin)) return false;
        try {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            var r = doc.RootElement;
            var a = r.GetProperty("personalObservations");
            var entries = a.GetProperty("resources").EnumerateArray().Where(e => e.GetProperty("resourceType").GetString() == "hunt_bills").ToArray();
            return r.GetProperty("ok").ValueKind == JsonValueKind.True && r.GetProperty("acceptedClientProduct").GetString() == NativeProduct.Name
                && a.GetProperty("contractVersion").GetInt32() == 1 && a.GetProperty("endpoint").GetString() == origin + "/api/game-sync/sync"
                && entries.Length == 1 && entries[0].GetProperty("schemaVersion").GetInt32() == 2
                && entries[0].GetProperty("collectorSchema").GetString() == "hunt-bills-v2"
                && entries[0].GetProperty("capability").GetString() == HuntCoverageCapability
                && entries[0].GetProperty("maxPayloadBytes").GetInt32() == MaximumPayloadBytes;
        } catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { return false; }
    }
    internal static bool HuntCoverageFresh(string payload, DateTime now) {
        try {
            using var doc = JsonDocument.Parse(payload);
            var time = doc.RootElement.GetProperty("billItemCoverage").GetProperty("observedAtUtc").GetDateTime();
            return PersonalObservationCompatibility.Utc(now) && PersonalObservationCompatibility.Utc(time)
                && now >= time && now - time <= TimeSpan.FromSeconds(HuntBillItemCoverage.MaximumAgeSeconds);
        } catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { return false; }
    }
    internal static bool HuntCoveragePayloadValid(string payload) {
        try {
            if (Encoding.UTF8.GetByteCount(payload) > MaximumPayloadBytes) return false;
            using var doc = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 16 });
            var r = doc.RootElement; var c = r.GetProperty("billItemCoverage");
            var domains = c.GetProperty("domains").EnumerateArray().ToArray();
            return r.GetProperty("schemaVersion").GetInt32() == 2 && r.GetProperty("collectorSchema").GetString() == "hunt-bills-v2"
                && r.GetProperty("uploadState").GetString() == "local-only-no-server-contract" && r.GetProperty("bills").GetArrayLength() <= 22
                && c.GetProperty("collectorSchema").GetString() == "hunt-bill-items-v1" && c.GetProperty("domainKind").GetString() == HuntBillItemCoverage.DomainKind
                && c.GetProperty("maximumAgeSeconds").GetInt32() == HuntBillItemCoverage.MaximumAgeSeconds
                && c.GetProperty("orderOwnership").GetString() == "unsupported" && c.GetProperty("acquisitionIdentity").GetString() == "unsupported"
                && Guid.TryParseExact(c.GetProperty("observationId").GetString(), "N", out _)
                && PersonalObservationCompatibility.Utc(c.GetProperty("observedAtUtc").GetDateTime())
                && new[] { "gameVersion", "collectorVersion", "sdkVersion" }.All(k => PersonalObservationCompatibility.Metadata(c.GetProperty(k).GetString()))
                && domains.Length == 22 && domains.Select(d => d.GetProperty("billTypeId").GetInt32()).OrderBy(i => i).SequenceEqual(Enumerable.Range(0,22))
                && domains.Select(d => d.GetProperty("keyItemId").GetUInt32()).Distinct().Count() == 22
                && domains.All(d => d.GetProperty("keyItemId").GetUInt32() > 0 && d.GetProperty("state").GetString() is "absent_confirmed" or "present_unresolved" or "unavailable");
        } catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { return false; }
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
    internal static int Withdraw(PersonalSyncState state, string owner, string resource) =>
        !Valid(state) ? 0 : state.Prepared.RemoveAll(p => p.OwnerKey == owner && p.Resource == resource);
    internal static PersonalPreparedSnapshot? Prepare(PersonalSyncState state, string owner, string resource, string payload) {
        if (!Valid(state) || !PersonalObservationCompatibility.Key(owner) || !PayloadValid(resource, payload)) return null;
        var prior = state.Prepared.SingleOrDefault(p => p.OwnerKey == owner && p.Resource == resource);
        if (prior is not null && (!prior.Acknowledged || SameObservation(resource, prior.Payload, payload))) return prior;
        if (prior is null && state.Prepared.Count >= MaximumPrepared) return null;
        var next = new PersonalPreparedSnapshot(owner, resource, Guid.NewGuid().ToString("N"), payload, Hash(payload));
        var candidate = new PersonalSyncState { Prepared = state.Prepared.Where(p => p != prior).Append(next).ToList() };
        if (!Valid(candidate)) return null;
        state.Prepared = candidate.Prepared;
        return next;
    }
    internal static bool CanSend(bool sync, bool retain, string origin, bool accepted, PersonalPreparedSnapshot? prepared) =>
        sync && retain && NativeProduct.TransportOrigin(origin) && accepted && prepared is { Acknowledged: false, Blocked: false };
    internal static bool Due(DateTime now, DateTime next, DateTime retry, bool inFlight, bool prompt, bool enabled) =>
        enabled && !inFlight && now >= retry && (prompt || now >= next);
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
    internal static bool PersistBeforeSend(Action save) {
        try { save(); return true; } catch (Exception) { return false; }
    }
    internal static bool TerminalStatus(int status) => status is 400 or 401 or 403 or 404 or 409 or 413 or 415;
    // HTTP deadline ends I/O, not ownership of an already classified response.
    // A delayed framework commit still records its receipt/terminal/backoff unless
    // the actual session or feature has ended. No linked HTTP token gate here.
    internal static PersonalResponseDisposition Disposition(bool current, bool sessionCanceled,
        bool featureCanceled, bool enabled, bool receipt, bool terminal) =>
        !current || sessionCanceled || featureCanceled || !enabled ? PersonalResponseDisposition.Canceled
        : receipt ? PersonalResponseDisposition.Acknowledged
        : terminal ? PersonalResponseDisposition.Blocked : PersonalResponseDisposition.Retry;
    internal static async Task<bool> ReadReceiptAsync(int status, Func<Task<string>> read) =>
        status is >= 200 and <= 299 && Receipt(await read());
}
#endif
