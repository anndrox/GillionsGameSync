#if GILLIONS_TEST_BUILD || GILLIONS_PUBLIC_BUILD
using System;
using System.Globalization;
using System.Linq;
using Dalamud.Game.Network.Structures;
using Dalamud.Plugin.Services;

namespace GillionsGameSync;

// Public receive events only. No game requests, searches, hooks, packet parsing,
// third-party IPC or interaction with Dalamud's own upload preference.
internal sealed class MarketContributionSource : IDisposable {
    private readonly IMarketBoard market;
    private readonly MarketContributor contributor;
    private readonly Func<MarketContributionSession?> capture;
    private readonly Func<DateTime> utcNow;
    private bool disposed;
    internal MarketContributionSource(IMarketBoard market, MarketContributor contributor, Func<MarketContributionSession?> capture, Func<DateTime>? utcNow = null) {
        this.market = market; this.contributor = contributor; this.capture = capture;
        this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        market.OfferingsReceived += Listings; market.HistoryReceived += History;
    }
    private void Listings(IMarketBoardCurrentOfferings packet) {
        if (disposed || !contributor.Enabled) return;
        try {
            var at = utcNow();
            var session = capture(); contributor.RefreshSession(session);
            if (session is null || packet.ItemListings.Count is < 1 or > 10) return;
            var item = packet.ItemListings[0].ItemId;
            if (packet.ItemListings.Any(l => l.ItemId != item)) return;
            var rows = packet.ItemListings.Select(l => new MarketListing(l.ListingId.ToString(CultureInfo.InvariantCulture),
                l.IsHq, l.ItemQuantity, l.PricePerUnit, l.RetainerCityId is >= 1 and <= 8 ? l.RetainerCityId : null)).ToArray();
            contributor.Observe(MarketObservation.ListingsPacket(session.WorldId, item, rows, at));
        } catch (Exception) { /* Malformed partial event cannot affect market-board use. No raw logs. */ }
    }
    private void History(IMarketBoardHistory packet) {
        if (disposed || !contributor.Enabled) return;
        try {
            var at = utcNow();
            var session = capture(); contributor.RefreshSession(session);
            if (session is null || packet.HistoryListings.Count is < 1 or > 20) return;
            var rows = packet.HistoryListings.Select(l => new MarketSale(l.IsHq, l.Quantity, l.SalePrice, l.PurchaseTime, l.OnMannequin)).ToArray();
            contributor.Observe(MarketObservation.HistoryPacket(session.WorldId, packet.ItemId, rows, at));
        } catch (Exception) { /* No identity, raw data or exceptions are logged. */ }
    }
    public void Dispose() {
        if (disposed) return;
        disposed = true; market.OfferingsReceived -= Listings; market.HistoryReceived -= History;
    }
}
#endif
