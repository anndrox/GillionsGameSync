using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Dalamud.Plugin.Services;
using Dalamud.Game.Network.Structures;
using GillionsGameSync;

int checks = 0;
void Check(bool value, string why) { checks++; if (!value) throw new Exception(why); }
var now = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
var world = (ushort)74;
MarketObservation Observation() => MarketObservation.ListingsPacket(world, 5333, [new("12345678901234567890", true, 7, 1500, 1)], now);
HttpResponseMessage Receipt(byte[] body, string status = "accepted") {
    var row = JsonSerializer.Deserialize<JsonElement>(body);
    return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new {
        ok = true, schemaVersion = 1, observationId = row.GetProperty("observationId").GetString(),
        worldId = row.GetProperty("worldId").GetUInt16(), itemId = row.GetProperty("itemId").GetUInt32(),
        kind = row.GetProperty("kind").GetString(), status, receivedAtUtc = "2026-10-01T12:00:03.000Z"
    })) };
}
MarketContributionSession Session(Func<byte[], CancellationToken, Task<HttpResponseMessage>> send,
    string key = "synthetic-epoch", CancellationToken token = default, Func<Task>? deny = null, bool blocked = false)
    => new(key, "synthetic-pairing", blocked, world, token, deny ?? (() => Task.CompletedTask), send);
var sent = new List<byte[]>();
Task<HttpResponseMessage> Accept(byte[] body, CancellationToken token) { token.ThrowIfCancellationRequested(); sent.Add(body); return Task.FromResult(Receipt(body)); }
var contributor = new MarketContributor(_ => {}, () => now);
Check(contributor.Enabled, "Gillions setting must default on.");
Check(!contributor.Observe(Observation()), "Unpaired/incompatible capture admitted.");
Check(!MarketContributor.Compatible("{}") && !MarketContributor.Compatible("not JSON"), "Missing/malformed capability accepted.");
Check(MarketContributor.Compatible("{\"marketContribution\":{\"contractVersion\":1,\"enabled\":true,\"acceptedClientProduct\":\"GillionsGameSyncTest\"}}"), "Exact capability rejected.");
Check(!MarketContributor.Compatible("{\"marketContribution\":{\"contractVersion\":2,\"enabled\":true,\"acceptedClientProduct\":\"GillionsGameSyncTest\"}}"), "Unknown contract accepted.");
Check(!MarketContributor.Compatible("{\"marketContribution\":{\"contractVersion\":1,\"enabled\":true,\"acceptedClientProduct\":\"GillionsGameSync\"}}"), "Stable permission accepted.");
contributor.RefreshSession(Session(Accept));
Check(!contributor.Observe(Observation() with { WorldId = 75 }), "Cross-world observation admitted.");
foreach (var bad in new[] {
    Observation() with { ItemId = 0 }, Observation() with { ItemId = 1000000 },
    Observation() with { Listings = [] }, Observation() with { Kind = "complete" },
    Observation() with { ClientObservedAtUtc = DateTime.MaxValue },
    Observation() with { ClientObservedAtUtc = DateTime.SpecifyKind(now, DateTimeKind.Unspecified) },
    Observation() with { Listings = [new("0", true, 1, 1, null)] },
    Observation() with { Listings = [new("01", true, 1, 1, null)] },
    Observation() with { Listings = [new("1", true, 0, 1, null)] },
    Observation() with { Listings = [new("1", true, 1, 0, null)] },
    Observation() with { Listings = [new("1", true, 1, 1, null), new("1", true, 1, 1, null)] }
}) Check(!contributor.Observe(bad), "Malformed/empty observation admitted.");
Check(contributor.Observe(Observation()), "Sanitized listing observation rejected.");
now = now.AddSeconds(3); contributor.Tick(now); await contributor.WhenIdleAsync();
Check(sent.Count == 1, "Observed listing was not sent.");
var payload = JsonSerializer.Deserialize<JsonElement>(sent[0]);
Check(payload.GetProperty("completeness").GetString() == "partial" && payload.GetProperty("worldEvidence").GetString() == "current-world-context", "Partial/context limits omitted.");
Check(payload.GetProperty("sourceSnapshotAtUtc").ValueKind == JsonValueKind.Null && !payload.TryGetProperty("receivedAtUtc", out _), "Snapshot/receiver timestamp fabricated.");
Check(payload.GetProperty("listings")[0].GetProperty("listingId").GetString() == "12345678901234567890", "Listing ID lost precision.");
Check(contributor.Observe(Observation()), "Repeated valid packet cannot be safely processed.");
now = now.AddSeconds(3); contributor.Tick(now); await contributor.WhenIdleAsync();
Check(sent.Count == 1, "Identical recent packet was uploaded twice unnecessarily.");
contributor.SetEnabled(false);
Check(!contributor.Observe(Observation()) && contributor.PendingCount == 0, "Opt-out admitted new data.");
contributor.Tick(now); await contributor.WhenIdleAsync(); Check(sent.Count == 1, "Opt-out sent market data.");
contributor.SetEnabled(true);

// Actual public Dalamud interface adapter fixtures. Identity getters throw if read.
var source = new FakeMarketBoard();
using var adapter = new MarketContributionSource(source, contributor, () => Session(Accept), () => now);
source.Listings(new Packet([new Listing()]));
now = now.AddSeconds(3); contributor.Tick(now); await contributor.WhenIdleAsync();
Check(sent.Count == 2 && JsonSerializer.Deserialize<JsonElement>(sent[^1]).GetProperty("listings")[0].GetProperty("pricePerUnit").GetUInt32() == 700, "Public event projection failed.");
source.History(new HistoryPacket());
now = now.AddSeconds(3); contributor.Tick(now); await contributor.WhenIdleAsync();
var sales = JsonSerializer.Deserialize<JsonElement>(sent[^1]);
Check(sent.Count == 3 && sales.GetProperty("sales")[0].GetProperty("soldAtUtc").GetDateTime().Kind == DateTimeKind.Utc, "Source sale timestamp lost.");
Check(!System.Text.Encoding.UTF8.GetString(sent[^1]).Contains("buyer", StringComparison.OrdinalIgnoreCase), "History contains buyer identity.");
source.Listings(new Packet([])); Check(contributor.PendingCount == 0, "Empty packet fabricated zero listings.");
source.Listings(new Packet([new Listing(), new Listing(5334)])); Check(contributor.PendingCount == 0, "Mixed item packet admitted.");
contributor.SetEnabled(false); source.History(new HistoryPacket()); Check(contributor.PendingCount == 0, "Disabled native event admitted.");
adapter.Dispose(); Check(source.ListingSubscriptions == 0 && source.HistorySubscriptions == 0, "Event handlers leaked on disposal.");

// Bounded network/server retries retain original observation ID/time/body.
now = new(2026, 10, 1, 13, 0, 0, DateTimeKind.Utc);
int calls = 0; var retryBodies = new List<byte[]>();
using var retry = new MarketContributor(_ => {}, () => now);
retry.RefreshSession(Session((body, _) => { calls++; retryBodies.Add(body); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)); }));
retry.Observe(Observation());
for (int i = 0; i < 5; i++) { now = now.AddSeconds(30); retry.Tick(now); await retry.WhenIdleAsync(); }
Check(calls == 3 && retry.PendingCount == 0, "Retries not bounded to three attempts.");
Check(retryBodies.All(b => b.SequenceEqual(retryBodies[0])), "Retry changed observation ID/timestamp/body.");
using var throttled = new MarketContributor(_ => {}, () => now);
int rateCalls = 0;
throttled.RefreshSession(Session((_, _) => { rateCalls++; var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests); r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(90)); return Task.FromResult(r); }));
throttled.Observe(Observation()); now = now.AddSeconds(3); throttled.Tick(now); await throttled.WhenIdleAsync();
now = now.AddSeconds(30); throttled.Tick(now); await throttled.WhenIdleAsync(); Check(rateCalls == 1, "Retry-After ignored.");

using var denied = new MarketContributor(_ => {}, () => now);
int deniedCalls = 0, denials = 0;
var denialSession = Session((_, _) => { deniedCalls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)); }, deny: () => { denials++; return Task.CompletedTask; });
denied.RefreshSession(denialSession); denied.Observe(Observation()); now = now.AddSeconds(3); denied.Tick(now); await denied.WhenIdleAsync();
denied.RefreshSession(denialSession); denied.Observe(Observation()); now = now.AddSeconds(30); denied.Tick(now); await denied.WhenIdleAsync();
Check(deniedCalls == 1 && denials == 1, "Auth denial did not stop enrollment or request durable stop.");
using var reloadDenied = new MarketContributor(_ => {}, () => now);
reloadDenied.RefreshSession(Session(Accept, blocked: true)); Check(!reloadDenied.Observe(Observation()), "Persisted enrollment denial ignored.");

using var absent = new MarketContributor(_ => {}, () => now);
int absentCalls = 0;
absent.RefreshSession(Session((_, _) => { absentCalls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)); }));
absent.Observe(Observation()); now = now.AddSeconds(3); absent.Tick(now); await absent.WhenIdleAsync();
absent.Observe(Observation()); now = now.AddSeconds(10); absent.Tick(now); await absent.WhenIdleAsync(); Check(absentCalls == 1, "Missing endpoint retried uncontrollably.");

using var bounded = new MarketContributor(_ => {}, () => now); bounded.RefreshSession(Session(Accept));
for (int i = 0; i < 64; i++) Check(bounded.Observe(Observation()), "Queue full before bound.");
Check(!bounded.Observe(Observation()) && bounded.PendingCount == 64, "Queue growth unbounded.");
now = now.AddMinutes(3); bounded.Tick(now); Check(bounded.PendingCount == 0, "Stale observations retained indefinitely.");
using var network = new MarketContributor(_ => {}, () => now);
int networkCalls = 0;
network.RefreshSession(Session((_, _) => { networkCalls++; throw new HttpRequestException("SYNTHETIC-PRIVATE-ERROR"); }));
network.Observe(Observation());
for (int i = 0; i < 4; i++) { now = now.AddSeconds(25); network.Tick(now); await network.WhenIdleAsync(); }
Check(networkCalls == 3 && network.PendingCount == 0 && !network.Status.Contains("SYNTHETIC"), "Network failure not bounded/sanitized.");
using var badReceipt = new MarketContributor(_ => {}, () => now);
int receiptCalls = 0;
badReceipt.RefreshSession(Session((_, _) => { receiptCalls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"ok\":true}") }); }));
badReceipt.Observe(Observation()); now = now.AddSeconds(3); badReceipt.Tick(now); await badReceipt.WhenIdleAsync();
Check(receiptCalls == 1 && badReceipt.PendingCount == 1 && !badReceipt.Status.Contains("accepted"), "Malformed receipt falsely acknowledged.");

using var cancelled = new MarketContributor(_ => {}, () => now);
var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
bool cancellationObserved = false;
cancelled.RefreshSession(Session(async (_, token) => {
    started.SetResult();
    try { await Task.Delay(Timeout.Infinite, token); } catch (OperationCanceledException) { cancellationObserved = true; throw; }
    throw new Exception("Unreachable");
}));
cancelled.Observe(Observation()); now = now.AddSeconds(3); cancelled.Tick(now);
await started.Task.WaitAsync(TimeSpan.FromSeconds(5)); cancelled.SetEnabled(false);
await cancelled.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
Check(cancellationObserved && cancelled.PendingCount == 0, "Opt-out failed to cancel in-flight work.");
using var switched = new MarketContributor(_ => {}, () => now);
switched.RefreshSession(Session(Accept)); switched.Observe(Observation());
switched.RefreshSession(Session(Accept, key: "new-world-epoch"));
Check(switched.PendingCount == 0, "World/enrollment/session change carried old queue.");
using var rateBound = new MarketContributor(_ => {}, () => now);
int limitedCalls = 0;
rateBound.RefreshSession(Session((body, _) => { limitedCalls++; return Task.FromResult(Receipt(body)); }));
for (int i = 0; i < 7; i++) {
    rateBound.Observe(Observation() with { Listings = [new((i + 1).ToString(), false, 1, (uint)(i + 1), null)] });
    now = now.AddSeconds(3); rateBound.Tick(now); await rateBound.WhenIdleAsync();
}
Check(limitedCalls == 6, "More than six market HTTP attempts per minute.");
using var worker = new MarketContributor(_ => {}, () => now);
using var workerEntered = new ManualResetEventSlim(); using var workerRelease = new ManualResetEventSlim();
var callerThread = Environment.CurrentManagedThreadId; int dispatchThread = callerThread;
worker.RefreshSession(Session((body, _) => {
    dispatchThread = Environment.CurrentManagedThreadId; workerEntered.Set();
    if (!workerRelease.Wait(TimeSpan.FromSeconds(5))) throw new Exception("Tick blocked synchronous worker startup.");
    return Task.FromResult(Receipt(body));
}));
worker.Observe(Observation()); now = now.AddSeconds(3); worker.Tick(now);
Check(workerEntered.Wait(TimeSpan.FromSeconds(5)) && dispatchThread != callerThread, "HTTP startup ran on event/framework caller.");
workerRelease.Set(); await worker.WhenIdleAsync();
using var staleDenial = new MarketContributor(_ => {}, () => now);
var oldStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var oldReply = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
staleDenial.RefreshSession(new("old-runtime", "old-enrollment", false, world, default, () => Task.CompletedTask,
    (_, _) => { oldStarted.SetResult(); return oldReply.Task; }));
staleDenial.Observe(Observation()); now = now.AddSeconds(3); staleDenial.Tick(now);
await oldStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
staleDenial.RefreshSession(new("new-runtime", "new-enrollment", true, world, default, () => Task.CompletedTask, Accept));
oldReply.SetResult(new(HttpStatusCode.Forbidden)); await staleDenial.WhenIdleAsync();
Check(!staleDenial.Observe(Observation()), "Old late denial cleared a newer enrollment's stop.");
contributor.Dispose();
Console.WriteLine($"Market passive public-event/allowlist/partial/consent/bounded transport fixtures passed: {checks}; no native game reads or live traffic.");

sealed class FakeMarketBoard : IMarketBoard {
    private IMarketBoard.OfferingsReceivedDelegate? listings;
    private IMarketBoard.HistoryReceivedDelegate? history;
    public int ListingSubscriptions { get; private set; }
    public int HistorySubscriptions { get; private set; }
    public event IMarketBoard.OfferingsReceivedDelegate OfferingsReceived { add { listings += value; ListingSubscriptions++; } remove { listings -= value; ListingSubscriptions--; } }
    public event IMarketBoard.HistoryReceivedDelegate HistoryReceived { add { history += value; HistorySubscriptions++; } remove { history -= value; HistorySubscriptions--; } }
    public event IMarketBoard.ItemPurchasedDelegate ItemPurchased { add => throw new Exception("Not a supported observation source"); remove => throw new Exception(); }
    public event IMarketBoard.PurchaseRequestedDelegate PurchaseRequested { add => throw new Exception("Do not track purchases"); remove => throw new Exception(); }
    public event IMarketBoard.TaxRatesReceivedDelegate TaxRatesReceived { add => throw new Exception("Do not request taxes"); remove => throw new Exception(); }
    public void Listings(IMarketBoardCurrentOfferings packet) => listings?.Invoke(packet);
    public void History(IMarketBoardHistory packet) => history?.Invoke(packet);
}
sealed record Packet(IReadOnlyList<IMarketBoardItemListing> ItemListings) : IMarketBoardCurrentOfferings { public int RequestId => throw new Exception("No request identity needed"); }
sealed class Listing(uint item = 5333) : IMarketBoardItemListing {
    public uint ItemId => item; public bool IsHq => false; public uint ItemQuantity => 3; public uint PricePerUnit => 700;
    public ulong ListingId => 111; public int RetainerCityId => 0;
    public ulong RetainerId => throw new Exception("Identity read"); public string RetainerName => throw new Exception("Identity read");
    public ulong ArtisanId => throw new Exception("Identity read");
    public IReadOnlyList<IItemMateria> Materia => throw new Exception(); public int MateriaCount => throw new Exception();
    public bool OnMannequin => throw new Exception(); public int Stain1Id => throw new Exception(); public int Stain2Id => throw new Exception(); public uint TotalTax => throw new Exception();
}
sealed class HistoryPacket : IMarketBoardHistory {
    public uint ItemId => 5333;
    public IReadOnlyList<IMarketBoardHistoryListing> HistoryListings => [new Sale()];
}
sealed class Sale : IMarketBoardHistoryListing {
    public string BuyerName => throw new Exception("Identity read"); public bool IsHq => false; public bool OnMannequin => false;
    public DateTime PurchaseTime => new(2026, 10, 1, 11, 0, 0, DateTimeKind.Utc); public uint Quantity => 2; public uint SalePrice => 600;
}
