#if GILLIONS_TEST_BUILD || GILLIONS_PUBLIC_BUILD || GILLIONS_TRAVEL_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace GillionsGameSync;

// Framework-owned, one volatile preparation. Never configuration, diagnostics,
// an offline journal, or a record with a generated payload-bearing ToString.
internal sealed class TravelPrepared : IDisposable {
    internal TravelPrepared(string binding, TravelObservation observation, DateTime now) {
        Binding = binding; ObservedAtUtc = observation.ObservedAtUtc;
        Payload = JsonSerializer.Serialize(observation, PersonalObservationCompatibility.Json);
        Nonce = Guid.NewGuid().ToString("N");
        Lifetime.CancelAfter(ObservedAtUtc.AddSeconds(TravelPolicy.TtlSeconds) - now);
    }
    internal string Binding { get; }
    internal string Payload { get; }
    internal string Nonce { get; }
    internal DateTime ObservedAtUtc { get; }
    internal CancellationTokenSource Lifetime { get; } = new();
    internal bool Finished { get; set; }
    internal bool Fresh(DateTime now) => now >= ObservedAtUtc && now < ObservedAtUtc.AddSeconds(TravelPolicy.TtlSeconds);
    public void Dispose() { Lifetime.Cancel(); Lifetime.Dispose(); }
}

internal static class TravelSyncPolicy {
    internal const string Origin = "https://test.gillions.app";
    internal const string Endpoint = Origin + "/api/game-sync/sync";
    internal const string Resource = "travel_context";
    internal const string Contract = "travel-context-v1";
    internal const string Capability = "travel_context_v1";
    internal const string Permission = "server:game-sync:personal:travel-context:v1";
    internal const int MaximumEnvelopeBytes = 69632;
    internal static bool Admit(bool testing, bool supported, bool consent, bool paired, string origin, bool acknowledged) =>
        testing && supported && consent && paired && NativeProduct.TransportOrigin(origin) && acknowledged;
    // Presence contains this entry ONLY after Site verifies the independent
    // device grant, active account, owned character and enrolled Testing version.
    internal static bool Compatible(string json, string origin = Origin) {
        if (!NativeProduct.TransportOrigin(origin)) return false;
        try {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            var root = doc.RootElement; var ack = root.GetProperty("personalObservations");
            if (root.GetProperty("ok").ValueKind != JsonValueKind.True
                || root.GetProperty("acceptedClientProduct").GetString() != NativeProduct.Name
                || ack.GetProperty("contractVersion").GetInt32() != 1 || ack.GetProperty("endpoint").GetString() != origin + "/api/game-sync/sync") return false;
            var entries = ack.GetProperty("resources").EnumerateArray().Where(r => r.GetProperty("resourceType").GetString() == Resource).ToArray();
            if (entries.Length != 1) return false;
            var r = entries[0];
            return r.GetProperty("schemaVersion").GetInt32() == 1
                && r.GetProperty("collectorSchema").GetString() == Contract
                && r.GetProperty("transportContract").GetString() == Contract
                && r.GetProperty("capability").GetString() == Capability
                && r.GetProperty("maxPayloadBytes").GetInt32() == TravelPolicy.MaximumBytes
                && r.GetProperty("maxEnvelopeBytes").GetInt32() == MaximumEnvelopeBytes
                && r.GetProperty("maxDepth").GetInt32() == TravelPolicy.MaximumDepth
                && r.GetProperty("maxDestinations").GetInt32() == TravelPolicy.MaximumDestinations
                && r.GetProperty("ttlSeconds").GetInt32() == TravelPolicy.TtlSeconds
                && r.GetProperty("resourceHeader").GetString() == "x-gillions-personal-resource"
                && r.GetProperty("capabilityHeader").GetString() == "x-gillions-personal-capability";
        } catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { return false; }
    }
    internal static bool Receipt(int status, string json) {
        if (status is not (200 or 201) || Encoding.UTF8.GetByteCount(json) > 4096) return false;
        try {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4 });
            var r = doc.RootElement;
            return r.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "ok", "receivedAt", "snapshotId", "unchanged" })
                && r.GetProperty("ok").ValueKind == JsonValueKind.True
                && Guid.TryParseExact(r.GetProperty("snapshotId").GetString(), "D", out var id) && id != Guid.Empty
                && r.GetProperty("unchanged").ValueKind == (status == 200 ? JsonValueKind.True : JsonValueKind.False)
                && r.GetProperty("receivedAt").GetString() is { } text && text.EndsWith('Z')
                && DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var time) && PersonalObservationCompatibility.Utc(time);
        } catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { return false; }
    }
    internal static bool Terminal(int status) => status is >= 300 and < 500 && status != 429;
    internal static int RetrySeconds(int status, int failures, double? retryAfter) {
        var floor = status is 429 or 503 ? 60 : 15;
        var delay = Math.Max(floor, Math.Min(120, 15 * Math.Pow(2, Math.Clamp(failures - 1, 0, 3))));
        return (int)Math.Clamp(Math.Ceiling(Math.Max(delay, retryAfter is >= 0 and <= 86400 ? retryAfter.Value : 0)), 15, 86400);
    }
}

internal sealed class TravelSyncState {
    internal TravelPrepared? Pending { get; private set; }
    internal DateTime NextAttemptUtc { get; private set; }
    private int failures;
    internal void Clear() { Pending?.Dispose(); Pending = null; } // never reset the send/backoff budget
    internal bool Maintain(string binding, bool admitted, DateTime now) {
        if (!admitted || Pending is { } p && (p.Binding != binding || !p.Fresh(now))) Clear();
        return admitted && now >= NextAttemptUtc;
    }
    internal TravelPrepared? Prepare(string binding, TravelObservation? observation, DateTime now) {
        if (string.IsNullOrEmpty(binding) || !TravelPolicy.Fresh(observation, now) || now < NextAttemptUtc) return null;
        var old = Pending;
        if (old is not null && old.Binding == binding && old.ObservedAtUtc >= observation!.ObservedAtUtc)
            return old.ObservedAtUtc == observation.ObservedAtUtc && !old.Finished ? old : null;
        Clear(); Pending = new(binding, observation!, now); return Pending;
    }
    internal void Dispatched(DateTime now) => NextAttemptUtc = now.AddSeconds(TravelPolicy.CadenceSeconds);
    internal void Complete(TravelPrepared prepared, bool receipt, int status, double? retryAfter, DateTime now) {
        if (!ReferenceEquals(Pending, prepared) || !prepared.Fresh(now)) { if (ReferenceEquals(Pending, prepared)) Clear(); return; }
        if (receipt || TravelSyncPolicy.Terminal(status)) { prepared.Finished = true; failures = 0; }
        else { failures++; NextAttemptUtc = now.AddSeconds(TravelSyncPolicy.RetrySeconds(status, failures, retryAfter)); }
    }
}
#endif
