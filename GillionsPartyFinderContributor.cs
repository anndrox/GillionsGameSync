using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GillionsGameSync;

// Holds an opaque session epoch and a credential-bearing dispatch closure, not
// a printable credential record. Created and validated by Plugin on its framework.
internal sealed class GillionsPartyFinderSession(string key, string authorizationKey, bool authorizationBlocked,
    CancellationToken cancellation, Func<Task> recordAuthorizationDenial,
    Func<byte[], CancellationToken, Task<HttpResponseMessage>> send) {
    internal string Key { get; } = key;
    internal string AuthorizationKey { get; } = authorizationKey;
    internal bool AuthorizationBlocked { get; } = authorizationBlocked;
    internal CancellationToken Cancellation { get; } = cancellation;
    internal Task RecordAuthorizationDenial() => recordAuthorizationDenial();
    internal Task<HttpResponseMessage> Send(byte[] body, CancellationToken token) => send(body, token);
}

internal sealed class GillionsPartyFinderContributor : IPartyFinderContributor {
    internal const string ApprovedTestingOrigin = "https://test.gillions.app";
    internal const string ContributionPath = "/api/game-sync/party-finder/contribute";
    // Build metadata is a compatibility guard, not the dispatch authority.
    internal static readonly Uri Endpoint = new(ApprovedTestingOrigin + ContributionPath);
    internal static Uri SessionEndpoint(string origin) {
        if (origin != ApprovedTestingOrigin) throw new InvalidOperationException("Party Finder Testing requires the approved secure TEST pairing.");
        return new Uri(origin + ContributionPath);
    }
    internal const int MaximumBodyBytes = 262144;
    private sealed record Observation(PartyFinderContributionListing Listing, DateTime At, long Sequence);
    private readonly object gate = new();
    private readonly IPartyFinderContributionSource source;
    private readonly Func<GillionsPartyFinderSession?> captureSession;
    private readonly Func<bool> enabled;
    private readonly Func<DateTime> utcNow;
    private readonly Action<string> report;
    private readonly Func<int> jitter;
    private readonly Dictionary<PartyFinderListingIdentity, Observation> pending = [];
    private readonly Queue<DateTime> attempts = new();
    private GillionsPartyFinderSession? session;
    private CancellationTokenSource? activeCancellation;
    private Task active = Task.CompletedTask;
    private DateTime due = DateTime.MaxValue;
    private string? blockedAuthorization;
    private bool endpointStopped;
    private string status = "Off. Manage this Testing device's public Party Finder contribution on Gillions.";
    private bool enabledState;
    private bool disposed;
    private bool inFlight;
    private long sequence;
    private int generation;
    private int batchLimit = 100;
    private int failures;

    internal GillionsPartyFinderContributor(IPartyFinderContributionSource source,
        Func<GillionsPartyFinderSession?> captureSession, Func<bool> enabled, Action<string> report,
        Func<DateTime>? utcNow = null, Func<int>? jitter = null) {
        this.source = source; this.captureSession = captureSession; this.enabled = enabled; this.report = report;
        this.utcNow = utcNow ?? (() => DateTime.UtcNow); this.jitter = jitter ?? (() => Random.Shared.Next(1, 4));
        enabledState = enabled(); source.ListingReceived += OnListing;
    }
    internal string Status { get { lock (gate) return status; } }
    internal int PendingCount { get { lock (gate) return pending.Count; } }
    internal Task WhenIdleAsync() { lock (gate) return active; }

    public void SetEnabled(bool value) {
        CancellationTokenSource? cancel = null;
        lock (gate) {
            if (disposed || enabledState == value) return;
            enabledState = value; generation++;
            if (!value) { pending.Clear(); due = DateTime.MaxValue; cancel = activeCancellation; status = "Off; unsent listings cleared."; }
        }
        PartyFinderContributor.CancelSafely(cancel);
    }
    private void RefreshSession() {
        SetEnabled(enabled());
        var current = captureSession(); // Only invoked from framework/native event callbacks.
        CancellationTokenSource? cancel = null;
        lock (gate) {
            if (disposed) return;
            if (current?.Key != session?.Key || current?.Cancellation.IsCancellationRequested == true) {
                pending.Clear(); due = DateTime.MaxValue; generation++; cancel = activeCancellation;
                session = current?.Cancellation.IsCancellationRequested == true ? null : current;
                failures = 0; batchLimit = 100;
            }
            // Refresh the persistent authorization flag even without a runtime-key change.
            if (session is not null && current?.Key == session.Key) session = current;
            if (session?.AuthorizationBlocked == true) blockedAuthorization = session.AuthorizationKey;
            if (enabledState && session is null) status = "Waiting for a logged-in, paired Testing client on the approved secure TEST origin.";
            if (session is not null && blockedAuthorization == session.AuthorizationKey)
                status = "Authorization stopped. Check the account and save an explicit contribution decision on Gillions; no re-pairing required for permission changes.";
            if (endpointStopped) status = "Party Finder endpoint unavailable or redirected; contribution stopped for this load. Site must correct deployment before reload.";
        }
        PartyFinderContributor.CancelSafely(cancel);
    }
    private void OnListing(PartyFinderContributionListing listing) {
        RefreshSession();
        lock (gate) {
            if (disposed || !enabledState || session is null || blockedAuthorization == session.AuthorizationKey || endpointStopped || !Valid(listing)) return;
            var now = utcNow();
            // A native listing burst shares a conservative 100-ms observation
            // bucket. Never regenerate this timestamp at dispatch or retry.
            var at = new DateTime(now.Ticks - now.Ticks % (TimeSpan.TicksPerMillisecond * 100), DateTimeKind.Utc);
            if (pending.Count >= 1000 && !pending.ContainsKey(listing.Identity)) pending.Remove(pending.MinBy(pair => pair.Value.Sequence).Key);
            pending[listing.Identity] = new(listing, at, ++sequence);
            var delayed = now.AddSeconds(10);
            if (due == DateTime.MaxValue || due < delayed) due = delayed;
            status = $"Queued public listings: {pending.Count}.";
        }
    }
    private static bool Valid(PartyFinderContributionListing l) => l.Id > 0 && l.CreatedWorld > 0 && l.HomeWorld > 0
        && l.Name.Length <= 128 && l.Description.Length <= 1024 && l.Category <= 65535
        && l.SecondsRemaining <= 3600 && l.NumParties is >= 1 and <= 6 && l.SlotsAvailable <= 48
        && l.Slots.Count <= 48 && l.JobsPresent.Count <= 48;
    private static bool Fresh(Observation row, DateTime now) => row.At >= now.AddMinutes(-5)
        && (row.Listing.SecondsRemaining == 0 || row.At.AddSeconds(row.Listing.SecondsRemaining) > now);

    public void Tick(DateTime now) {
        RefreshSession();
        lock (gate) {
            if (disposed || !enabledState || session is null || blockedAuthorization == session.AuthorizationKey || endpointStopped || inFlight) return;
            foreach (var row in pending.Where(pair => !Fresh(pair.Value, now)).ToArray()) pending.Remove(row.Key);
            if (pending.Count == 0 || now < due) return;
            while (attempts.TryPeek(out var attempt) && attempt <= now.AddMinutes(-1)) attempts.Dequeue();
            if (attempts.Count >= 6) { due = attempts.Peek().AddMinutes(1); return; }
            inFlight = true;
            var capturedSession = session;
            var capturedGeneration = generation;
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(session.Cancellation);
            activeCancellation = cancellation;
            // All ordering, body sizing, serialization and HTTP startup stay off
            // the game thread. Only immutable managed observations leave it.
            active = Task.Run(() => Upload(capturedSession, capturedGeneration, cancellation));
        }
    }
    private async Task Upload(GillionsPartyFinderSession captured, int epoch, CancellationTokenSource cancellation) {
        Observation[] batch = [];
        bool retry = false, stop = false;
        double wait = 10;
        string outcome = "";
        try {
            lock (gate) {
                if (!Current(captured, epoch)) return;
                var first = pending.Values.OrderBy(row => row.Sequence).FirstOrDefault();
                if (first is null) return;
                batch = pending.Values.Where(row => row.At == first.At && Fresh(row, utcNow())).OrderBy(row => row.Sequence).Take(batchLimit).ToArray();
                foreach (var row in batch) pending.Remove(row.Listing.Identity);
            }
            if (batch.Length == 0) return;
            byte[] body;
            while (true) {
                body = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1,
                    observedAtUtc = batch[0].At.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                    listings = batch.Select(row => row.Listing).ToArray() });
                if (body.Length <= MaximumBodyBytes) break;
                var keep = Math.Max(1, batch.Length / 2);
                lock (gate) { if (Current(captured, epoch)) foreach (var row in batch.Skip(keep)) Requeue(row); }
                batch = batch.Take(keep).ToArray();
            }
            cancellation.Token.ThrowIfCancellationRequested();
            lock (gate) {
                if (!Current(captured, epoch) || batch.Any(row => !Fresh(row, utcNow()))) return;
                attempts.Enqueue(utcNow());
            }
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(30)); // Includes response-body reads, not only headers.
            using var response = await captured.Send(body, deadline.Token);
            // Record authorization denial immediately, before response reads or any
            // runtime-current test. Logout/opt-out must not erase a received denial.
            var denied = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
            if (denied) {
                stop = true;
                lock (gate) {
                    if (session is null || session.AuthorizationKey == captured.AuthorizationKey) blockedAuthorization = captured.AuthorizationKey;
                    if (session?.AuthorizationKey == captured.AuthorizationKey) pending.Clear();
                }
                await captured.RecordAuthorizationDenial();
            }
            if (response.StatusCode == HttpStatusCode.OK) {
                var bytes = await ReadBounded(response.Content, deadline.Token);
                ValidateAcknowledgement(bytes, batch);
                lock (gate) if (Current(captured, epoch)) failures = 0;
                outcome = $"Gillions Party Finder accepted {batch.Length} observations (current cache only; not xivpf delivery).";
            } else {
                retry = response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable;
                stop = denied || (int)response.StatusCode is >= 300 and < 400 || response.StatusCode == HttpStatusCode.NotFound;
                if (stop && !denied) lock (gate) { endpointStopped = true; pending.Clear(); }
                if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge && batch.Length > 1) {
                    lock (gate) batchLimit = Math.Max(1, batch.Length / 2);
                    retry = true;
                }
                byte[] bytes;
                try { bytes = await ReadBounded(response.Content, deadline.Token); }
                catch (Exception error) when (error is IOException or InvalidDataException or HttpRequestException
                    || error is OperationCanceledException && !cancellation.IsCancellationRequested) { bytes = []; }
                var code = ErrorCode(bytes);
                wait = Math.Max(Backoff(), RetryAfter(response, bytes));
                outcome = $"Gillions Party Finder: HTTP {(int)response.StatusCode}; {code}; "
                    + (denied ? "authorization stopped; check the account and contribution permission on Gillions."
                        : stop ? "endpoint failure; stopped for this load until Site corrects deployment and the plugin is reloaded."
                        : retry ? "bounded retry queued." : "batch discarded; not retrying unchanged invalid data.");
            }
        } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
            // Opt-out, logout, re-pair and disposal discard old-session work.
        } catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException) {
            retry = !stop; wait = Backoff(); outcome = stop
                ? "Gillions Party Finder authorization denied; contribution stopped. Persistent stop could not be confirmed; check local configuration storage before reload."
                : "Gillions Party Finder transport failed; bounded retry queued.";
        } catch (Exception error) when (error is JsonException or InvalidDataException or FormatException or InvalidOperationException or KeyNotFoundException) {
            retry = true; wait = Backoff(); outcome = "Gillions Party Finder acknowledgement invalid; no acceptance claimed; bounded retry queued.";
        } finally {
            bool publish = false;
            lock (gate) {
                if (Current(captured, epoch)) {
                    if (stop) pending.Clear();
                    else if (retry) foreach (var row in batch) Requeue(row);
                    var now = utcNow();
                    var delay = Math.Max(10, wait) + (retry ? Math.Clamp(jitter(), 1, 3) : 0);
                    due = delay >= (DateTime.MaxValue - now).TotalSeconds ? DateTime.MaxValue : now.AddSeconds(delay);
                    if (outcome.Length > 0) { status = outcome; publish = true; }
                }
                inFlight = false;
                if (ReferenceEquals(activeCancellation, cancellation)) activeCancellation = null;
            }
            if (publish) report(outcome); // Never names, listing keys, body, token or server error text.
            cancellation.Dispose();
        }
    }
    private bool Current(GillionsPartyFinderSession captured, int epoch) => !disposed && enabledState
        && generation == epoch && session?.Key == captured.Key && !captured.Cancellation.IsCancellationRequested;
    private void Requeue(Observation row) {
        if (Fresh(row, utcNow()) && !pending.ContainsKey(row.Listing.Identity) && pending.Count < 1000) pending[row.Listing.Identity] = row;
    }
    private double Backoff() => Math.Min(60, 10 * Math.Pow(2, Math.Min(3, failures++)));
    private static async Task<byte[]> ReadBounded(HttpContent content, CancellationToken token) {
        using var stream = await content.ReadAsStreamAsync(token);
        using var output = new MemoryStream();
        var buffer = new byte[4096];
        int count;
        while ((count = await stream.ReadAsync(buffer, token)) > 0) {
            if (output.Length + count > 32768) throw new InvalidDataException("Response exceeds bounded acknowledgement size.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
    private static void ValidateAcknowledgement(byte[] bytes, Observation[] batch) {
        using var doc = JsonDocument.Parse(bytes);
        var root = doc.RootElement;
        if (root.GetProperty("ok").ValueKind != JsonValueKind.True || root.GetProperty("schemaVersion").GetInt32() != 1
            || !DateTimeOffset.TryParseExact(root.GetProperty("receivedAtUtc").GetString(), "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _)
            || root.GetProperty("currentCount").GetInt32() is < 0 or > 6000) throw new InvalidDataException("Invalid acknowledgement envelope.");
        var keys = batch.Select(row => Key(row.Listing)).ToHashSet(StringComparer.Ordinal);
        foreach (var entry in root.GetProperty("accepted").EnumerateArray()) {
            if (!keys.Remove(entry.GetProperty("listingKey").GetString() ?? "")
                || entry.GetProperty("status").GetString() is not ("inserted" or "updated" or "unchanged" or "older_observation" or "expired"))
                throw new InvalidDataException("Acknowledgement does not match the submitted batch.");
        }
        if (keys.Count > 0) throw new InvalidDataException("Incomplete acknowledgement.");
    }
    internal static string Key(PartyFinderContributionListing l) => FormattableString.Invariant($"{l.CreatedWorld}:{l.LastServerRestart}:{l.Id}");
    private static string ErrorCode(byte[] bytes) {
        try {
            using var doc = JsonDocument.Parse(bytes);
            var code = doc.RootElement.GetProperty("code").GetString();
            return code is "INVALID_JSON" or "INVALID_PARTY_FINDER_REQUEST" or "DEVICE_INVALID" or "ACCOUNT_UNAVAILABLE"
                or "TESTING_PRODUCT_REQUIRED" or "PARTY_FINDER_PERMISSION_REQUIRED" or "METHOD_NOT_ALLOWED" or "PAYLOAD_TOO_LARGE"
                or "UNSUPPORTED_MEDIA_TYPE" or "RATE_LIMITED" or "INTAKE_BUSY" or "INTAKE_UNAVAILABLE" ? code : "UNEXPECTED_RESPONSE";
        } catch { return "UNEXPECTED_RESPONSE"; }
    }
    private static double RetryAfter(HttpResponseMessage response, byte[] bytes) {
        var seconds = response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 0;
        if (response.Headers.RetryAfter?.Date is { } date) seconds = Math.Max(seconds, (date - DateTimeOffset.UtcNow).TotalSeconds);
        try {
            using var doc = JsonDocument.Parse(bytes);
            if (doc.RootElement.TryGetProperty("retryAfterSeconds", out var value) && value.TryGetInt32(out var supplied)) seconds = Math.Max(seconds, supplied);
        } catch (JsonException) { }
        return Math.Max(0, seconds);
    }
    public void Dispose() {
        CancellationTokenSource? cancel;
        lock (gate) {
            if (disposed) return;
            disposed = true; generation++; pending.Clear(); session = null; cancel = activeCancellation;
        }
        source.ListingReceived -= OnListing; source.Dispose(); PartyFinderContributor.CancelSafely(cancel);
    }
}
