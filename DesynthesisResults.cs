#if GILLIONS_TEST_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GillionsGameSync;

internal static class DesynthesisResults {
    internal const string Permission = "desynthesisResults";
    internal const string Resource = "desynthesis_results";
    internal const string Capability = "desynthesis_results_v1";
    internal const string Schema = "desynthesis-results-v1";
    internal const string Label = "Desynthesis results";
    internal const string Description = "Your desynthesis results are added to totals on item pages. Only totals are shown, never who got what, and only once enough results are in. You can turn this off at any time.";
    internal const int Capacity = 500;
    internal const int BatchSize = 250;

    internal static bool Compatible(string json, string origin) {
        if (!NativeProduct.TransportOrigin(origin)) return false;
        try {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            var root = doc.RootElement;
            if (root.GetProperty("ok").ValueKind != JsonValueKind.True
                || root.GetProperty("acceptedClientProduct").GetString() != NativeProduct.Name) return false;
            var ack = root.GetProperty("personalObservations");
            if (ack.GetProperty("contractVersion").GetInt32() != 1
                || ack.GetProperty("endpoint").GetString() != origin + "/api/game-sync/sync") return false;
            var entries = ack.GetProperty("resources").EnumerateArray()
                .Where(row => row.GetProperty("resourceType").GetString() == Resource).ToArray();
            return entries.Length == 1 && entries[0].GetProperty("schemaVersion").GetInt32() == 1
                && entries[0].GetProperty("collectorSchema").GetString() == Schema
                && entries[0].GetProperty("capability").GetString() == Capability
                && entries[0].GetProperty("maxPayloadBytes").GetInt32() == PersonalSyncPolicy.MaximumPayloadBytes;
        } catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) {
            return false;
        }
    }

    internal static DesynthesisObservation? Create(uint source, int? job, DateTime observedAtUtc, ReadOnlySpan<DesynthesisOutput> outputs) {
        if (source == 0 || job is < 8 or > 15 || observedAtUtc.Kind != DateTimeKind.Utc
            || observedAtUtc > DateTime.UtcNow.AddMinutes(2) || outputs.Length is < 1 or > 3) return null;
        foreach (var output in outputs)
            if (output.ItemId == 0 || output.ItemId == source || output.Quantity is < 1 or > 999) return null;
        var copy = outputs.ToArray();
        if (copy.Select(output => output.ItemId).Distinct().Count() != copy.Length) return null;
        return new(Guid.NewGuid().ToString("N"), source, job, observedAtUtc, copy);
    }

    internal static byte[] Encode(DesynthesisObservation[] observations) =>
        JsonSerializer.SerializeToUtf8Bytes(new { schema = Schema, observations });
}

internal readonly record struct DesynthesisOutput(
    [property: JsonPropertyName("itemId")] uint ItemId,
    [property: JsonPropertyName("quantity")] int Quantity);

internal sealed record DesynthesisObservation(
    [property: JsonPropertyName("eventId")] string EventId,
    [property: JsonPropertyName("sourceItemId")] uint SourceItemId,
    [property: JsonPropertyName("desynthJobId")] int? DesynthJobId,
    [property: JsonPropertyName("observedAtUtc")] DateTime ObservedAtUtc,
    [property: JsonPropertyName("outputs")] DesynthesisOutput[] Outputs);

internal sealed class DesynthesisBatch(DesynthesisObservation[] observations) {
    internal string Nonce { get; } = Guid.NewGuid().ToString("N");
    internal byte[] Payload { get; } = DesynthesisResults.Encode(observations);
    internal string LastEventId { get; } = observations[^1].EventId;
}

// The queue and its currently prepared prefix have one framework-thread owner.
// Overflow retires the prepared prefix because it would contain an evicted row.
internal sealed class DesynthesisQueue {
    private readonly Queue<DesynthesisObservation> pending = new();
    private DesynthesisBatch? batch;
    internal bool Enabled { get; private set; }

    internal void SetEnabled(bool enabled) {
        Enabled = enabled;
        if (enabled) return;
        pending.Clear();
        batch = null;
    }

    internal bool Capture(uint source, int? job, DateTime now, ReadOnlySpan<DesynthesisOutput> outputs) {
        if (!Enabled || DesynthesisResults.Create(source, job, now, outputs) is not { } result) return false;
        if (pending.Count == DesynthesisResults.Capacity) {
            // The retained queue always owns exactly the latest 500 results.
            pending.Dequeue();
            batch = null;
        }
        pending.Enqueue(result);
        return true;
    }

    internal DesynthesisBatch? Prepare() => !Enabled || pending.Count == 0 ? null
        : batch ??= new(pending.Take(DesynthesisResults.BatchSize).ToArray());

    internal void Acknowledge(DesynthesisBatch accepted) {
        if (!ReferenceEquals(batch, accepted)) return;
        while (pending.Dequeue().EventId != accepted.LastEventId) { }
        batch = null;
    }
}
#endif
