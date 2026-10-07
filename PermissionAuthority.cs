using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GillionsGameSync;

// Authenticated presence only. RAM custody: no credentials, config migration or
// relationship between the Site external ID and the game's Content ID.
internal sealed record PermissionEnrollment(Guid DeviceId, DateTime PairedAt, long CharacterId);
internal sealed record PermissionDecision(string State, bool? Enabled, string? Generation, DateTime? DecidedAt);
internal sealed class PermissionAuthority {
    internal const string Origin = "https://test.gillions.app";
    internal static readonly string[] Keys = ["marketContribution", "partyFinderContribution", "personalHunts",
        "personalSubmarines", "huntRoutingLocation", "itemLinks", "partyFinderLinks", "automaticHuntMaps"];
    private readonly Dictionary<string, PermissionDecision> decisions = new(StringComparer.Ordinal);
    private readonly HashSet<string> explicitSeen = new(StringComparer.Ordinal);
    private string session = "", device = "";
    private DateTime latestIssued = DateTime.MinValue;
    private long expiresClock;
    private bool valid;
    internal PermissionEnrollment? Enrollment { get; private set; }
    internal void Bind(string binding, string deviceId) {
        if (session == binding && device == deviceId) return;
        session = binding; device = deviceId; Enrollment = null; latestIssued = DateTime.MinValue;
        decisions.Clear(); explicitSeen.Clear(); Invalidate();
    }
    internal void Invalidate() { valid = false; expiresClock = 0; }
    internal bool Fresh(long clock) => valid && clock < expiresClock;
    internal bool Explicit(string key, long clock) => Fresh(clock) && decisions.TryGetValue(key, out var p) && p.State == "explicit";
    internal bool Allows(string key, bool historical, long clock) => Fresh(clock) && decisions.TryGetValue(key, out var p)
        && (p.State == "explicit" ? p.Enabled == true : p.State == "legacy" && !explicitSeen.Contains(key) && historical);
    internal string Identity(string key, long clock) => !Fresh(clock) || !decisions.TryGetValue(key, out var p) ? "unavailable"
        : p.State == "explicit" ? "explicit:" + p.Generation + ":" + p.Enabled : p.State;
    internal bool PartyFinderLinks(bool historicalItem, bool historicalPf, long clock) => Allows("partyFinderLinks", historicalItem && historicalPf, clock);
    internal bool HuntReceiving(bool historicalAutomatic, long clock) => Explicit("automaticHuntMaps", clock)
        || Allows("automaticHuntMaps", historicalAutomatic, clock);
    private static bool Shape(JsonElement e, params string[] keys) => e.ValueKind == JsonValueKind.Object
        && e.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(keys.Order());
    private static bool Utc(string? text, out DateTime time) => DateTime.TryParseExact(text,
        ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"], CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out time);
    // A first successful presence has already validated name/world/Content ID at
    // Site. Pin its authenticated enrollment tuple; later tuples cannot replace it.
    internal bool Apply(string json, DateTime issuerUpperUtc, long clock, PermissionEnrollment? expected = null) {
        try {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            var root = document.RootElement;
            if (root.GetProperty("ok").ValueKind != JsonValueKind.True || !root.TryGetProperty("permissionAuthority", out var a)
                || !Shape(a, "revision", "deviceBinding", "issuedAt", "expiresAt", "permissions")
                || a.GetProperty("revision").GetInt32() != 1 || session.Length == 0
                || !Utc(a.GetProperty("issuedAt").GetString(), out var issued)
                || !Utc(a.GetProperty("expiresAt").GetString(), out var expires)
                || expires - issued != TimeSpan.FromSeconds(30) || issued > issuerUpperUtc.AddSeconds(5)
                || issuerUpperUtc.Kind != DateTimeKind.Utc) return Reject();
            var b = a.GetProperty("deviceBinding");
            if (!Shape(b, "deviceId", "pairedAt", "characterId")
                || !Guid.TryParseExact(b.GetProperty("deviceId").GetString(), "D", out var id)
                || !Guid.TryParse(device, out var current) || id != current || id == Guid.Empty
                || !Utc(b.GetProperty("pairedAt").GetString(), out var paired) || paired > issued || paired < DateTime.UnixEpoch
                || !b.GetProperty("characterId").TryGetInt64(out var character) || character <= 0 || character > 9007199254740991L) return Reject();
            var enrollment = new PermissionEnrollment(id, paired, character);
            if ((expected ?? Enrollment) is { } pinned && pinned != enrollment) return Reject();
            // Ignore old responses, rather than letting them revoke a newer lease.
            if (issued < latestIssued) return false;
            if (expires <= issuerUpperUtc) return Reject();
            var permissions = a.GetProperty("permissions");
            if (!Shape(permissions, Keys)) return Reject();
            var next = new Dictionary<string, PermissionDecision>(StringComparer.Ordinal);
            foreach (var key in Keys) {
                var p = permissions.GetProperty(key);
                next.Add(key, ParseDecision(key, p, issued));
            }
            Enrollment = enrollment; latestIssued = issued;
            decisions.Clear(); foreach (var (key, decision) in next) {
                decisions.Add(key, decision); if (decision.State == "explicit") explicitSeen.Add(key);
            }
            expiresClock = checked(clock + (long)Math.Min(30000, (expires - issuerUpperUtc).TotalMilliseconds)); valid = true;
            return true;
        } catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or KeyNotFoundException or OverflowException or ArgumentException) { return Reject(); }
    }
    private static PermissionDecision ParseDecision(string key, JsonElement p, DateTime issued) {
        try {
            if (Shape(p, "scope", "state", "enabled", "generation", "decidedAt")
                    && p.GetProperty("scope").GetString() == (key == "marketContribution" ? "account" : "device")) {
                    var state = p.GetProperty("state").GetString();
                    var enabled = p.GetProperty("enabled"); var generation = p.GetProperty("generation"); var at = p.GetProperty("decidedAt");
                    if (state == "legacy" && enabled.ValueKind == JsonValueKind.Null && generation.ValueKind == JsonValueKind.Null && at.ValueKind == JsonValueKind.Null)
                        return new("legacy", null, null, null);
                    else if (state == "explicit" && enabled.ValueKind is JsonValueKind.True or JsonValueKind.False
                        && Guid.TryParseExact(generation.GetString(), "D", out var g) && g != Guid.Empty
                        && Utc(at.GetString(), out var decided) && decided >= DateTime.UnixEpoch && decided <= issued)
                        return new("explicit", enabled.GetBoolean(), g.ToString("D"), decided);
                }
        } catch (Exception e) when (e is InvalidOperationException or FormatException or OverflowException or ArgumentException) { }
        return new("invalid", false, null, null);
    }
    private bool Reject() { Invalidate(); return false; }
}

internal static class PermissionObservationFilter {
    // Filter the transport copy only; private retained history remains unchanged.
    internal static string? FreshPersonal(string resource, string? payload, DateTime floor) {
        if (payload is null) return null;
        try {
            var root = JsonNode.Parse(payload)?.AsObject();
            var key = resource == "hunt_bills" ? "bills" : "slots";
            if (root?[key] is not JsonArray rows) return null;
            var fresh = rows.Where(row => row?["observedAtUtc"]?.GetValue<DateTime>() >= floor).Select(row => row!.DeepClone()).ToArray();
            if (root["billItemCoverage"] is { } coverage) {
                if (coverage["observedAtUtc"]?.GetValue<DateTime>() is not { } at || at < floor) return null;
            } else if (fresh.Length == 0) return null;
            root[key] = new JsonArray(fresh); return root.ToJsonString();
        } catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException) { return null; }
    }
}
