using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace GillionsGameSync;

internal static class XivpfEndpointPolicy {
    internal static readonly Uri ProductionEndpoint = new("https://xivpf.com/contribute/multiple");

    internal static Uri RequireSafe(Uri endpoint, bool requireLoopback) {
        if (!endpoint.IsAbsoluteUri || !string.IsNullOrEmpty(endpoint.UserInfo))
            throw new InvalidOperationException("xivpf contribution endpoint must be an absolute URL without credentials.");
        var loopback = endpoint.IsLoopback;
        if (requireLoopback && !loopback)
            throw new InvalidOperationException("Testing builds require a loopback xivpf contribution endpoint.");
        if (endpoint.Scheme != Uri.UriSchemeHttps && !(loopback && endpoint.Scheme == Uri.UriSchemeHttp))
            throw new InvalidOperationException("xivpf contribution must use HTTPS or loopback HTTP.");
        return endpoint;
    }

    internal static Uri RequireBuildSafe(Uri endpoint, bool testingBuild) {
        endpoint = RequireSafe(endpoint, testingBuild);
        if (!testingBuild && !endpoint.IsLoopback && endpoint != ProductionEndpoint)
            throw new InvalidOperationException("Stable-compatible builds may contribute only to the official xivpf endpoint or a loopback test server.");
        return endpoint;
    }
}

internal sealed record PartyFinderListingSnapshot(
    uint Id,
    uint ContentIdLower,
    byte[] Name,
    byte[] Description,
    ushort CreatedWorld,
    ushort HomeWorld,
    ushort CurrentWorld,
    uint Category,
    ushort Duty,
    byte DutyType,
    bool BeginnersWelcome,
    ushort SecondsRemaining,
    ushort MinItemLevel,
    byte NumParties,
    byte SlotsAvailable,
    uint LastServerRestart,
    uint Objective,
    uint Conditions,
    uint DutyFinderSettings,
    uint LootRules,
    uint SearchArea,
    IReadOnlyList<uint> SlotAccepting,
    IReadOnlyList<byte> JobsPresent);

internal readonly record struct PartyFinderListingIdentity(uint LastServerRestart, ushort CreatedWorld, uint Id);

internal sealed class PartyFinderContributionListing {
    [JsonPropertyName("id")] public uint Id { get; }
    [JsonPropertyName("content_id_lower")] public uint ContentIdLower { get; }
    [JsonPropertyName("name")] public byte[] Name { get; }
    [JsonPropertyName("description")] public byte[] Description { get; }
    [JsonPropertyName("created_world")] public ushort CreatedWorld { get; }
    [JsonPropertyName("home_world")] public ushort HomeWorld { get; }
    [JsonPropertyName("current_world")] public ushort CurrentWorld { get; }
    [JsonPropertyName("category")] public uint Category { get; }
    [JsonPropertyName("duty")] public ushort Duty { get; }
    [JsonPropertyName("duty_type")] public byte DutyType { get; }
    [JsonPropertyName("beginners_welcome")] public bool BeginnersWelcome { get; }
    [JsonPropertyName("seconds_remaining")] public ushort SecondsRemaining { get; }
    [JsonPropertyName("min_item_level")] public ushort MinItemLevel { get; }
    [JsonPropertyName("num_parties")] public byte NumParties { get; }
    [JsonPropertyName("slots_available")] public byte SlotsAvailable { get; }
    [JsonPropertyName("last_server_restart")] public uint LastServerRestart { get; }
    [JsonPropertyName("objective")] public uint Objective { get; }
    [JsonPropertyName("conditions")] public uint Conditions { get; }
    [JsonPropertyName("duty_finder_settings")] public uint DutyFinderSettings { get; }
    [JsonPropertyName("loot_rules")] public uint LootRules { get; }
    [JsonPropertyName("search_area")] public uint SearchArea { get; }
    [JsonPropertyName("slots")] public IReadOnlyList<PartyFinderContributionSlot> Slots { get; }
    [JsonPropertyName("jobs_present")] public IReadOnlyList<byte> JobsPresent { get; }

    internal PartyFinderListingIdentity Identity => new(LastServerRestart, CreatedWorld, Id);

    internal PartyFinderContributionListing(PartyFinderListingSnapshot listing) {
        Id = listing.Id;
        ContentIdLower = listing.ContentIdLower;
        Name = listing.Name;
        Description = listing.Description;
        CreatedWorld = listing.CreatedWorld;
        HomeWorld = listing.HomeWorld;
        CurrentWorld = listing.CurrentWorld;
        Category = listing.Category;
        Duty = listing.Duty;
        DutyType = listing.DutyType;
        BeginnersWelcome = listing.BeginnersWelcome;
        SecondsRemaining = listing.SecondsRemaining;
        MinItemLevel = listing.MinItemLevel;
        NumParties = listing.NumParties;
        SlotsAvailable = listing.SlotsAvailable;
        LastServerRestart = listing.LastServerRestart;
        Objective = listing.Objective;
        Conditions = listing.Conditions;
        DutyFinderSettings = listing.DutyFinderSettings;
        LootRules = listing.LootRules;
        SearchArea = listing.SearchArea;
        Slots = listing.SlotAccepting.Select(value => new PartyFinderContributionSlot(value)).ToArray();
        JobsPresent = listing.JobsPresent.ToArray();
    }
}

internal sealed class PartyFinderContributionSlot {
    [JsonPropertyName("accepting")] public uint Accepting { get; }
    internal PartyFinderContributionSlot(uint accepting) => Accepting = accepting;
}

internal interface IPartyFinderContributionSource : IDisposable {
    event Action<PartyFinderContributionListing>? ListingReceived;
}

internal interface IPartyFinderContributionLog {
    void Uploaded(int count);
    void Failed(Exception error);
}

internal interface IPartyFinderContributor : IDisposable {
    void SetEnabled(bool value);
    void Tick(DateTime now);
}

internal sealed class DisabledPartyFinderContributor : IPartyFinderContributor {
    public void SetEnabled(bool value) { }
    public void Tick(DateTime now) { }
    public void Dispose() { }
}

internal sealed class PartyFinderContributor : IPartyFinderContributor {
    internal static readonly TimeSpan UploadDelay = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);
    internal const int MaximumRequestsPerMinute = 6;
    internal const int MaximumPendingListings = 1000;

    private sealed record PendingListing(PartyFinderContributionListing Listing, long Sequence);

    private readonly object gate = new();
    private readonly IPartyFinderContributionSource source;
    private readonly HttpClient http;
    private readonly IPartyFinderContributionLog log;
    private readonly Func<bool> enabled;
    private readonly Func<DateTime> utcNow;
    private readonly Uri endpoint;
    private readonly string userAgent;
    private readonly TimeSpan uploadDelay;
    private readonly Dictionary<PartyFinderListingIdentity, PendingListing> pending = [];
    private readonly Queue<DateTime> requestAttempts = new();
    private DateTime nextUploadUtc = DateTime.MaxValue;
    private CancellationTokenSource? activeRequestCancellation;
    private Task activeUpload = Task.CompletedTask;
    private bool enabledState;
    private bool uploadInFlight;
    private bool disposed;
    private int enabledGeneration;
    private long sequence;

    internal PartyFinderContributor(
        IPartyFinderContributionSource source,
        HttpClient http,
        IPartyFinderContributionLog log,
        Func<bool> enabled,
        Uri endpoint,
        string userAgent,
        Func<DateTime>? utcNow = null,
        TimeSpan? uploadDelay = null) {
        this.source = source;
        this.http = http;
        this.log = log;
        this.enabled = enabled;
        this.endpoint = XivpfEndpointPolicy.RequireSafe(endpoint, false);
        this.userAgent = userAgent;
        this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        this.uploadDelay = uploadDelay ?? UploadDelay;
        enabledState = enabled();
        source.ListingReceived += OnListing;
    }

    internal int PendingCount { get { lock (gate) return pending.Count; } }

    internal Task WhenIdleAsync() { lock (gate) return activeUpload; }

    public void SetEnabled(bool value) {
        CancellationTokenSource? cancel = null;
        lock (gate) {
            if (disposed || enabledState == value) return;
            enabledState = value;
            enabledGeneration++;
            if (!value) {
                pending.Clear();
                nextUploadUtc = DateTime.MaxValue;
                cancel = activeRequestCancellation;
            }
        }
        cancel?.Cancel();
    }

    private void OnListing(PartyFinderContributionListing listing) {
        SetEnabled(enabled());
        lock (gate) {
            if (disposed || !enabledState) return;
            EnqueueLatestNoLock(listing);
            nextUploadUtc = utcNow().Add(uploadDelay);
        }
    }

    public void Tick(DateTime now) {
        SetEnabled(enabled());
        lock (gate) {
            if (disposed || !enabledState || uploadInFlight || pending.Count == 0 || now < nextUploadUtc) return;
            PruneRequestAttemptsNoLock(now);
            if (requestAttempts.Count >= MaximumRequestsPerMinute) {
                nextUploadUtc = requestAttempts.Peek().AddMinutes(1);
                return;
            }
            var batch = pending.Values.OrderBy(value => value.Sequence).Select(value => value.Listing).ToList();
            pending.Clear();
            requestAttempts.Enqueue(now);
            uploadInFlight = true;
            var generation = enabledGeneration;
            var cancellation = new CancellationTokenSource();
            activeRequestCancellation = cancellation;
            activeUpload = UploadAsync(batch, generation, cancellation);
        }
    }

    private async Task UploadAsync(List<PartyFinderContributionListing> batch, int generation, CancellationTokenSource cancellation) {
        try {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) {
                Content = new StringContent(JsonSerializer.Serialize(batch), Encoding.UTF8, "application/json"),
            };
            request.Headers.UserAgent.ParseAdd(userAgent);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"xivpf returned HTTP {(int)response.StatusCode}.");
            log.Uploaded(batch.Count);
        } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
            // Opt-out and disposal intentionally discard the unsent batch.
        } catch (Exception error) {
            var reportFailure = false;
            lock (gate) {
                if (!disposed && enabledState && generation == enabledGeneration) {
                    foreach (var listing in batch) EnqueueRetryNoLock(listing);
                    var retryAt = utcNow().Add(RetryDelay);
                    if (nextUploadUtc < retryAt) nextUploadUtc = retryAt;
                    reportFailure = true;
                }
            }
            if (reportFailure) log.Failed(error);
        } finally {
            lock (gate) {
                if (ReferenceEquals(activeRequestCancellation, cancellation)) activeRequestCancellation = null;
                uploadInFlight = false;
            }
            cancellation.Dispose();
        }
    }

    private void EnqueueLatestNoLock(PartyFinderContributionListing listing) {
        if (!pending.ContainsKey(listing.Identity) && pending.Count >= MaximumPendingListings) {
            var oldest = pending.MinBy(pair => pair.Value.Sequence).Key;
            pending.Remove(oldest);
        }
        pending[listing.Identity] = new(listing, ++sequence);
    }

    private void EnqueueRetryNoLock(PartyFinderContributionListing listing) {
        if (pending.ContainsKey(listing.Identity) || pending.Count >= MaximumPendingListings) return;
        pending[listing.Identity] = new(listing, ++sequence);
    }

    private void PruneRequestAttemptsNoLock(DateTime now) {
        var cutoff = now.AddMinutes(-1);
        while (requestAttempts.Count > 0 && requestAttempts.Peek() <= cutoff) requestAttempts.Dequeue();
    }

    public void Dispose() {
        CancellationTokenSource? cancel;
        lock (gate) {
            if (disposed) return;
            disposed = true;
            enabledState = false;
            enabledGeneration++;
            pending.Clear();
            nextUploadUtc = DateTime.MaxValue;
            cancel = activeRequestCancellation;
        }
        source.ListingReceived -= OnListing;
        source.Dispose();
        cancel?.Cancel();
    }
}
