using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Dalamud.Game.Text.SeStringHandling;

namespace GillionsGameSync;

internal sealed record ItemLinkPollResponse(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("request")] ItemLinkRequest? Request);

internal sealed record ItemLinkRequest(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("itemId")] long ItemId,
    [property: JsonPropertyName("expiresAt")] DateTime ExpiresAtUtc,
    [property: JsonPropertyName("claimToken")] string ClaimToken);

internal enum ItemLinkDeliveryResult {
    Delivered,
    InvalidRequest,
    Expired,
    InvalidItem,
    AlreadyDelivered,
    ConsumeRejected,
}

internal static class ItemLinkPollPolicy {
    public static bool ShouldPoll(bool enabled, bool isLoggedIn, string? deviceToken, bool pollInFlight, DateTime nowUtc, DateTime nextPollUtc) =>
        enabled && isLoggedIn && !string.IsNullOrWhiteSpace(deviceToken) && !pollInFlight && nowUtc >= nextPollUtc;
}

internal static class NativeItemLinkFactory {
    public static bool IsValidItemId(long itemId) => itemId > 0 && itemId <= uint.MaxValue;

    public static SeString Create(long itemId, string displayName) {
        if (!IsValidItemId(itemId)) throw new ArgumentOutOfRangeException(nameof(itemId));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("An authoritative item name is required.", nameof(displayName));
        return SeString.CreateItemLink((uint)itemId, false, displayName.Trim());
    }
}

internal sealed class ItemLinkRequestProcessor {
    private const int MaximumRememberedRequests = 128;
    private static readonly TimeSpan MaximumRequestLifetime = TimeSpan.FromMinutes(5);
    private readonly HashSet<string> deliveredRequestIds = new(StringComparer.Ordinal);
    private readonly Queue<string> deliveredRequestOrder = new();
    private readonly object deliveryLock = new();

    public async Task<ItemLinkDeliveryResult> ProcessAsync(
        ItemLinkRequest? request,
        DateTime nowUtc,
        Func<long, Task<string?>> resolveItemNameAsync,
        Func<ItemLinkRequest, Task<bool>> consumeAsync,
        Func<SeString, Task> printAsync) {
        if (request is null
            || string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Length > 200
            || string.IsNullOrWhiteSpace(request.ClaimToken) || request.ClaimToken.Length > 500
            || !NativeItemLinkFactory.IsValidItemId(request.ItemId)) return ItemLinkDeliveryResult.InvalidRequest;
        if (request.ExpiresAtUtc <= nowUtc || request.ExpiresAtUtc > nowUtc.Add(MaximumRequestLifetime)) return ItemLinkDeliveryResult.Expired;
        lock (deliveryLock) if (deliveredRequestIds.Contains(request.RequestId)) return ItemLinkDeliveryResult.AlreadyDelivered;

        var itemName = await resolveItemNameAsync(request.ItemId);
        if (string.IsNullOrWhiteSpace(itemName)) return ItemLinkDeliveryResult.InvalidItem;
        var link = NativeItemLinkFactory.Create(request.ItemId, itemName);

        // The server consumes the account-scoped claim before the chat side
        // effect. A transport retry therefore cannot print the same request
        // twice, even if the acknowledgement response is lost.
        if (!await consumeAsync(request)) return ItemLinkDeliveryResult.ConsumeRejected;
        lock (deliveryLock) {
            if (!deliveredRequestIds.Add(request.RequestId)) return ItemLinkDeliveryResult.AlreadyDelivered;
            deliveredRequestOrder.Enqueue(request.RequestId);
            while (deliveredRequestOrder.Count > MaximumRememberedRequests) deliveredRequestIds.Remove(deliveredRequestOrder.Dequeue());
        }
        await printAsync(link);
        return ItemLinkDeliveryResult.Delivered;
    }
}
