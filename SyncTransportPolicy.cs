using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace GillionsGameSync;

// Control responses are bounded even when the peer omits Content-Length.
public static class SyncResponsePolicy {
    public const int MaximumBytes = 64 * 1024;
    public const int MaximumDepth = 16;
    public static readonly JsonSerializerOptions Options = new() { MaxDepth = MaximumDepth };
    public static async Task<string> ReadAsync(HttpContent content, CancellationToken cancellation = default) {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        cancellation = deadline.Token;
        if (content.Headers.ContentLength > MaximumBytes) throw new InvalidOperationException("Gillions response exceeded the size limit.");
        using var input = await content.ReadAsStreamAsync(cancellation);
        using var output = new MemoryStream();
        var buffer = new byte[4096];
        while (true) {
            var count = await input.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, MaximumBytes + 1 - output.Length)), cancellation);
            if (count == 0) break;
            output.Write(buffer, 0, count);
            if (output.Length > MaximumBytes) throw new InvalidOperationException("Gillions response exceeded the size limit.");
        }
        var text = Encoding.UTF8.GetString(output.ToArray());
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = MaximumDepth });
        return text;
    }

    public static bool IsSnapshotReceipt(string json, string resource, bool allowEmptySnapshot = false) {
        try {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = MaximumDepth });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True
                || !root.TryGetProperty("unchanged", out var unchanged) || unchanged.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
            // An empty glamour snapshot can retain no prior snapshot at all.
            if (resource == "glamour_plates" && allowEmptySnapshot && unchanged.ValueKind == JsonValueKind.True
                && !root.TryGetProperty("snapshotId", out _)) return true;
            if (!root.TryGetProperty("snapshotId", out var id) || !PositiveId(id)
                || !root.TryGetProperty("receivedAt", out var received) || received.ValueKind != JsonValueKind.String) return false;
            return DateTimeOffset.TryParse(received.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _);
        } catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException) { return false; }
    }
    public static async Task EnsureSuccessfulAsync(HttpResponseMessage response, CancellationToken cancellation = default) {
        if (response.IsSuccessStatusCode) return;
        var code = "";
        try {
            using var document = JsonDocument.Parse(await SyncResponsePolicy.ReadAsync(response.Content, cancellation));
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.String)
                code = value.GetString() ?? "";
        } catch (Exception error) when (error is JsonException or InvalidOperationException) { }
        if (code == "TRIAL_EXPIRED") throw new GillionsSyncRejectedException(code, "Your Gillions trial has ended.");
        if (code == "ACCOUNT_DISABLED") throw new GillionsSyncRejectedException(code, "This Gillions account is disabled.");
        throw new InvalidOperationException($"Gillions rejected the request (HTTP {(int)response.StatusCode}).");
    }

    private static bool PositiveId(JsonElement id) => id.ValueKind switch {
        JsonValueKind.Number => id.TryGetInt64(out var value) && value > 0,
        JsonValueKind.String => long.TryParse(id.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0,
        _ => false,
    };
}

public sealed record GilLedgerEvent(string EventId, DateTime OccurredAtUtc, long GilDelta, string Kind, string Confidence, int? ItemId, int? ItemQuantity, string? RetainerId, string? RetainerName, uint? LogMessageId, int[] LogIntegerParameters, string? CharacterName = null, string? CharacterWorld = null);
public sealed record PendingRetainerSale(string SaleId, DateTime OccurredAtUtc, long Amount, int? ItemId, int ItemQuantity, string? RetainerId, string? RetainerName);

public static class GilLedgerPolicy {
    private static readonly Regex Id = new("^[A-Za-z0-9_-]{12,160}$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> Kinds = new(StringComparer.Ordinal) {
        "unclassified", "player_trade", "retainer_sale", "retainer_gil_receipt", "retainer_gil_deposit", "retainer_gil_withdrawal",
        "retainer_balance_increase", "retainer_balance_decrease", "vendor_purchase", "vendor_sale", "repair", "teleport", "market_purchase",
        "quest_or_leve_reward", "duty_roulette_reward", "dungeon_reward", "mob_or_chest_drop", "occult_crescent_treasure_cache_reward",
        "occult_crescent_bronze_treasure_cache_reward", "occult_crescent_gold_treasure_cache_reward"
    };
    public static bool IsValid(GilLedgerEvent entry) => Id.IsMatch(entry.EventId ?? "")
        && entry.OccurredAtUtc != default && entry.GilDelta is >= -999999999 and <= 999999999 and not 0
        && Kinds.Contains(entry.Kind) && entry.Confidence is "inferred" or "confirmed"
        && (entry.ItemId is null || entry.ItemId > 0 && entry.ItemId != 1000000)
        && (entry.ItemQuantity is null || entry.ItemQuantity is > 0 and <= 9999)
        && entry.LogIntegerParameters is { Length: <= 8 };
    public static byte[] PreparePayload(string name, string world, string sessionId, GilLedgerEvent[] entries) {
        if (!Id.IsMatch(sessionId) || entries.Length is < 1 or > 200 || entries.Any(entry => !IsValid(entry))
            || entries.Select(entry => entry.EventId).Distinct(StringComparer.Ordinal).Count() != entries.Length)
            throw new InvalidOperationException("Pending Gil data failed validation and was preserved.");
        return JsonSerializer.SerializeToUtf8Bytes(new {
            character = new { name, world }, sessionId,
            events = entries.Select(entry => new { eventId = entry.EventId, occurredAt = entry.OccurredAtUtc, gilDelta = entry.GilDelta,
                kind = entry.Kind, confidence = entry.Confidence, itemId = entry.ItemId, itemQuantity = entry.ItemQuantity,
                retainerId = entry.RetainerId, retainerName = entry.RetainerName, logMessageId = entry.LogMessageId,
                logIntegerParameters = entry.LogIntegerParameters }).ToArray()
        }, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
    }
    public static int Acknowledge(List<GilLedgerEvent> pending, GilLedgerEvent[] sent) => pending.RemoveAll(current => sent.Any(entry =>
        entry.EventId == current.EventId && entry with { LogIntegerParameters = current.LogIntegerParameters } == current
        && entry.LogIntegerParameters.SequenceEqual(current.LogIntegerParameters)));

    public static List<PendingRetainerSale> Confirm(List<PendingRetainerSale> pending, long amount, string? retainerId) {
        if (amount <= 0 || string.IsNullOrWhiteSpace(retainerId)) return [];
        var candidates = pending.Where(sale => sale.Amount > 0 && (string.IsNullOrWhiteSpace(retainerId)
            || StringComparer.Ordinal.Equals(sale.RetainerId, retainerId))).ToArray();
        var exact = candidates.Where(sale => sale.Amount == amount).ToArray();
        if (exact.Length > 1) return []; // equal-value observations do not establish which sale was paid
        if (exact.Length == 0 && candidates.Sum(sale => (decimal)sale.Amount) == amount) exact = candidates;
        var ids = exact.Select(sale => sale.SaleId).ToHashSet(StringComparer.Ordinal);
        if (ids.Count != exact.Length || pending.Any(sale => ids.Contains(sale.SaleId) && !exact.Contains(sale))) return [];
        pending.RemoveAll(sale => ids.Contains(sale.SaleId));
        return exact.ToList();
    }
}

public sealed class GillionsSyncRejectedException : InvalidOperationException {
    public string Code { get; }
    public GillionsSyncRejectedException(string code, string message) : base(message) => Code = code;
}

public static class SyncErrorPolicy {
    public static string Message(Exception error) => error switch {
        GillionsSyncRejectedException rejection when rejection.Code == "TRIAL_EXPIRED" => "Your Gillions trial has ended.",
        GillionsSyncRejectedException rejection when rejection.Code == "ACCOUNT_DISABLED" => "This Gillions account is disabled.",
        OperationCanceledException => "The connection or sync settings changed. Try again when ready.",
        _ => "Gillions could not complete the request. Pending records were kept; please try again.",
    };

}
