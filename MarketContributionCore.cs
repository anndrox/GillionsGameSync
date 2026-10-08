#if GILLIONS_TEST_BUILD || GILLIONS_PUBLIC_BUILD || GILLIONS_MARKET_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GillionsGameSync;

internal sealed record MarketListing(string ListingId, bool Hq, uint Quantity, uint PricePerUnit, int? RetainerCityId);
internal sealed record MarketSale(bool Hq, uint Quantity, uint SalePrice, DateTime SoldAtUtc, bool OnMannequin);
internal sealed record MarketObservation(string ObservationId, ushort WorldId, uint ItemId, string Kind,
    DateTime ClientObservedAtUtc, MarketListing[] Listings, MarketSale[] Sales) {
    internal static MarketObservation ListingsPacket(ushort world, uint item, MarketListing[] rows, DateTime at)
        => new(Guid.NewGuid().ToString("N"), world, item, "listings", at, rows, []);
    internal static MarketObservation HistoryPacket(ushort world, uint item, MarketSale[] rows, DateTime at)
        => new(Guid.NewGuid().ToString("N"), world, item, "history", at, [], rows);
    internal bool Valid => Guid.TryParseExact(ObservationId, "N", out _) && WorldId > 0 && ItemId is > 0 and < 1000000
        && ClientObservedAtUtc.Kind == DateTimeKind.Utc && ClientObservedAtUtc >= DateTime.UnixEpoch
        && ClientObservedAtUtc <= DateTime.MaxValue.AddMinutes(-5)
        && Listings is not null && Sales is not null
        && (Kind switch {
            "listings" => Listings.Length is >= 1 and <= 10 && Sales.Length == 0
            && Listings.All(l => l is not null && ulong.TryParse(l.ListingId, out var id) && id > 0
                && l.ListingId == id.ToString(System.Globalization.CultureInfo.InvariantCulture)
                && l.Quantity > 0 && l.PricePerUnit > 0 && (l.RetainerCityId is null or >= 1 and <= 8))
            && Listings.Select(l => l.ListingId).Distinct().Count() == Listings.Length,
            "history" => Sales.Length is >= 1 and <= 20 && Listings.Length == 0
                && Sales.All(s => s is not null && s.Quantity > 0 && s.SalePrice > 0 && s.SoldAtUtc.Kind == DateTimeKind.Utc
                    && s.SoldAtUtc >= DateTime.UnixEpoch && s.SoldAtUtc <= ClientObservedAtUtc.AddMinutes(5)),
            _ => false
        });
    internal byte[] Serialize() => JsonSerializer.SerializeToUtf8Bytes(new {
        schemaVersion = 1, source = "gillions-game-sync", clientProduct = NativeProduct.Name,
        ObservationId, WorldId, ItemId, Kind, ClientObservedAtUtc,
        worldEvidence = "current-world-context", completeness = "partial", sourceSnapshotAtUtc = (DateTime?)null,
        listings = Kind == "listings" ? Listings : null, sales = Kind == "history" ? Sales : null
    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    internal string ContentFingerprint() => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
        WorldId, ItemId, Kind, Listings, Sales
    })));
}

// Existing pairing supplies the credential-bearing closure. No credential or
// character identity is added to the market payload or memory-only queue.
internal sealed class MarketContributionSession(string key, string authorizationKey, bool blocked, ushort worldId,
    CancellationToken cancellation, Func<Task> deny, Func<byte[], CancellationToken, Task<HttpResponseMessage>> send) {
    internal string Key { get; } = key;
    internal string AuthorizationKey { get; } = authorizationKey;
    internal bool Blocked { get; } = blocked;
    internal ushort WorldId { get; } = worldId;
    internal CancellationToken Cancellation { get; } = cancellation;
    internal Task Deny() => deny();
    internal Task<HttpResponseMessage> Send(byte[] body, CancellationToken token) => send(body, token);
}

internal sealed class MarketContributor : IDisposable {
    internal const string Path = "/api/game-sync/market-observations";
    internal const int MaximumPending = 64;
    internal const int MaximumBodyBytes = 32768;
    private sealed record Pending(MarketObservation Observation, string Fingerprint, int Failures, DateTime Due);
    private readonly object gate = new();
    private readonly Func<DateTime> utcNow;
    private readonly Action<string> report;
    private readonly Queue<DateTime> attempts = new();
    private readonly List<Pending> pending = [];
    private readonly Dictionary<string, DateTime> recent = [];
    private MarketContributionSession? session;
    private CancellationTokenSource? cancellation;
    private Task active = Task.CompletedTask;
    private bool enabled = true, disposed, inFlight, endpointStopped;
    private string? deniedAuthorization;
    private int epoch;
    private string status = "On; awaiting compatible authenticated Gillions market intake. No market queries or upload before compatibility acceptance.";
    internal MarketContributor(Action<string> report, Func<DateTime>? utcNow = null) {
        this.report = report; this.utcNow = utcNow ?? (() => DateTime.UtcNow);
    }
    internal bool Enabled { get { lock (gate) return enabled; } }
    internal string Status { get { lock (gate) return status; } }
    internal int PendingCount { get { lock (gate) return pending.Count; } }
    internal Task WhenIdleAsync() { lock (gate) return active; }
    internal void SetEnabled(bool value) {
        CancellationTokenSource? stop;
        lock (gate) {
            if (enabled == value || disposed) return;
            enabled = value; epoch++; pending.Clear(); recent.Clear(); stop = cancellation;
            if (value) UpdateReadinessStatus(true);
            else status = "Off; unsent market observations cleared. Ordinary sync unchanged.";
        }
        Cancel(stop);
    }
    internal void RefreshSession(MarketContributionSession? next) {
        CancellationTokenSource? stop = null;
        lock (gate) {
            if (disposed) return;
            var wasReady = Ready;
            if (next?.Cancellation.IsCancellationRequested == true) next = null;
            var changed = next?.Key != session?.Key;
            if (changed) {
                epoch++; pending.Clear(); recent.Clear(); stop = cancellation;
            }
            session = next;
            if (next?.Blocked == true) deniedAuthorization = next.AuthorizationKey;
            UpdateReadinessStatus(!wasReady || changed);
        }
        Cancel(stop);
    }
    // Called under gate. Refresh only transition status, not later upload outcomes.
    private void UpdateReadinessStatus(bool transitioned) {
        if (!enabled) status = "Off; ordinary sync unchanged.";
        else if (endpointStopped) status = "Market endpoint unavailable/redirected; stopped for this load. Site must establish the contract.";
        else if (session is null || session.Cancellation.IsCancellationRequested)
            status = "On; waiting for compatible paired intake/current-world context. No market uploads.";
        else if (deniedAuthorization == session.AuthorizationKey)
            status = "Market authorization denied; check the account and save an explicit contribution decision on Gillions. No re-pairing required for permission changes.";
        else if (transitioned)
            status = "On; compatible authenticated Gillions intake ready. Naturally received partial market observations may be uploaded.";
    }
    internal bool Observe(MarketObservation observation) {
        lock (gate) {
            var now = utcNow();
            if (!Ready || !observation.Valid || observation.WorldId != session!.WorldId || !Fresh(observation, now)) return false;
            // Only bounded managed rows were copied at receipt. Hashing and JSON
            // serialization are done by the worker, not on the native callback.
            if (pending.Count >= MaximumPending) { status = "Market queue full; new transient observation dropped. Gameplay and ordinary sync unchanged."; return false; }
            pending.Add(new(observation, "", 0, now.AddSeconds(2)));
            return true;
        }
    }
    private bool Ready => !disposed && enabled && !endpointStopped && session is not null
        && !session.Cancellation.IsCancellationRequested && deniedAuthorization != session.AuthorizationKey;
    private static bool Fresh(MarketObservation row, DateTime now) => row.ClientObservedAtUtc <= now.AddSeconds(5)
        && row.ClientObservedAtUtc >= now.AddMinutes(-2);
    internal void Tick(DateTime now) {
        lock (gate) {
            if (!Ready || inFlight) return;
            pending.RemoveAll(p => !Fresh(p.Observation, now));
            if (pending.Count == 0 || now < pending[0].Due) return;
            while (attempts.TryPeek(out var at) && at <= now.AddMinutes(-1)) attempts.Dequeue();
            if (attempts.Count >= 6) return;
            var captured = session!; var capturedEpoch = epoch;
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(captured.Cancellation);
            var cancel = cancellation;
            inFlight = true;
            active = Task.Run(() => Upload(captured, capturedEpoch, cancel));
        }
    }
    private bool Current(MarketContributionSession captured, int capturedEpoch) => Ready && epoch == capturedEpoch && session?.Key == captured.Key;
    private async Task Upload(MarketContributionSession captured, int capturedEpoch, CancellationTokenSource cancel) {
        Pending? row = null;
        bool retry = false; double delay = 10; string outcome = "";
        try {
            lock (gate) {
                if (!Current(captured, capturedEpoch) || pending.Count == 0) return;
                row = pending[0]; pending.RemoveAt(0);
            }
            row = row with { Fingerprint = row.Observation.ContentFingerprint() };
            lock (gate) {
                foreach (var k in recent.Where(p => p.Value < utcNow().AddMinutes(-2)).Select(p => p.Key).ToArray()) recent.Remove(k);
                if (row.Failures == 0 && recent.ContainsKey(row.Fingerprint)) return;
            }
            var body = row.Observation.Serialize();
            if (body.Length > MaximumBodyBytes) { outcome = "Market payload too large; observation dropped."; return; }
            lock (gate) {
                if (!Current(captured, capturedEpoch) || !Fresh(row.Observation, utcNow())) return;
                attempts.Enqueue(utcNow());
            }
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            using var response = await captured.Send(body, deadline.Token);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) {
                lock (gate) {
                    if (session is null || session.AuthorizationKey == captured.AuthorizationKey) deniedAuthorization = captured.AuthorizationKey;
                    if (session?.AuthorizationKey == captured.AuthorizationKey) pending.Clear();
                }
                await captured.Deny(); // Persist enrollment stop before reading any response.
                outcome = "Market authorization denied; stopped for this pairing. Ordinary sync remains separate.";
                return;
            }
            if (response.StatusCode == HttpStatusCode.OK) {
                ValidateReceipt(await ReadBounded(response.Content, deadline.Token), row.Observation);
                lock (gate) if (Current(captured, capturedEpoch)) {
                    if (recent.Count >= MaximumPending) recent.Remove(recent.MinBy(p => p.Value).Key);
                    recent[row.Fingerprint] = utcNow();
                }
                outcome = "Gillions accepted a partial market observation; not a complete market snapshot.";
            } else if ((int)response.StatusCode is >= 300 and < 400 || response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed) {
                lock (gate) if (session is null || session.AuthorizationKey == captured.AuthorizationKey) { endpointStopped = true; pending.Clear(); }
                outcome = "Market endpoint unavailable/redirected; stopped for this load. No alternate receiver.";
            } else {
                retry = response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout;
                delay = Math.Max(delay, response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 0);
                if (response.Headers.RetryAfter?.Date is { } at) delay = Math.Max(delay, (at.UtcDateTime - utcNow()).TotalSeconds);
                outcome = retry ? "Market intake temporarily unavailable; bounded transient retry." : "Market observation rejected; not retrying unchanged invalid data.";
            }
        } catch (OperationCanceledException) when (cancel.IsCancellationRequested) {
            // Opt-out/logout/re-pair/world changes and disposal drop transient work.
        } catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException or JsonException
            or InvalidDataException or InvalidOperationException or KeyNotFoundException or FormatException) {
            retry = true; outcome = "Market transport/receipt unavailable; bounded retry, no gameplay interruption.";
        } finally {
            lock (gate) {
                if (Current(captured, capturedEpoch)) {
                    if (retry && row is not null && row.Failures < 2 && Fresh(row.Observation, utcNow()) && pending.Count < MaximumPending
                        && delay < 120) pending.Insert(0, row with { Failures = row.Failures + 1,
                            Due = utcNow().AddSeconds(Math.Max(delay, 10 * (1 << row.Failures))) });
                    if (outcome.Length > 0) status = outcome;
                }
                inFlight = false;
                if (ReferenceEquals(cancellation, cancel)) cancellation = null;
            }
            if (outcome.Length > 0) report(outcome); // Fixed summaries only; no response bodies/identity/credentials.
            cancel.Dispose();
        }
    }
    private static async Task<byte[]> ReadBounded(HttpContent content, CancellationToken token) {
        using var stream = await content.ReadAsStreamAsync(token); using var output = new MemoryStream();
        var buffer = new byte[1024]; int count;
        while ((count = await stream.ReadAsync(buffer, token)) > 0) {
            if (output.Length + count > 8192) throw new InvalidDataException("Receipt too large.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
    internal static bool Compatible(JsonElement presence) {
        try {
            var ack = presence.GetProperty("marketContribution");
            return ack.GetProperty("contractVersion").GetInt32() == 1 && ack.GetProperty("enabled").ValueKind == JsonValueKind.True
                && ack.GetProperty("acceptedClientProduct").GetString() == NativeProduct.Name;
        } catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException) { return false; }
    }
    internal static bool Compatible(string presence) {
        try { using var document = JsonDocument.Parse(presence); return Compatible(document.RootElement); }
        catch (JsonException) { return false; }
    }
    private static void ValidateReceipt(byte[] bytes, MarketObservation row) {
        using var doc = JsonDocument.Parse(bytes); var r = doc.RootElement;
        if (r.GetProperty("ok").ValueKind != JsonValueKind.True || r.GetProperty("schemaVersion").GetInt32() != 1
            || r.GetProperty("observationId").GetString() != row.ObservationId || r.GetProperty("worldId").GetUInt16() != row.WorldId
            || r.GetProperty("itemId").GetUInt32() != row.ItemId || r.GetProperty("kind").GetString() != row.Kind
            || r.GetProperty("status").GetString() is not ("accepted" or "duplicate" or "older_observation")
            || !DateTimeOffset.TryParseExact(r.GetProperty("receivedAtUtc").GetString(), "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var at)
            || at.Offset != TimeSpan.Zero)
            throw new InvalidDataException("Receipt mismatch.");
    }
    private static void Cancel(CancellationTokenSource? value) {
        try { value?.Cancel(); } catch (Exception e) when (e is ObjectDisposedException or AggregateException) { }
    }
    public void Dispose() {
        CancellationTokenSource? stop;
        lock (gate) {
            if (disposed) return; disposed = true; epoch++; pending.Clear(); recent.Clear(); session = null; stop = cancellation;
        }
        Cancel(stop);
    }
}
#endif
