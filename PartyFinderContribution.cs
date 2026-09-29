using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Dalamud.Game.Gui.PartyFinder.Types;
using Dalamud.Plugin.Services;

namespace GillionsGameSync;

internal static class XivpfEndpoints {
    internal static Uri ContributionUrl {
        get {
            var configured = typeof(Plugin).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => string.Equals(attribute.Key, "XivpfContributionUrl", StringComparison.Ordinal))
                ?.Value;
            if (!Uri.TryCreate(configured, UriKind.Absolute, out var endpoint))
                throw new InvalidOperationException("XivpfContributionUrl build metadata is missing or invalid.");
            var loopback = endpoint.IsLoopback && endpoint.Scheme == Uri.UriSchemeHttp;
            if (endpoint.Scheme != Uri.UriSchemeHttps && !loopback)
                throw new InvalidOperationException("xivpf contribution must use HTTPS or loopback HTTP.");
            return endpoint;
        }
    }
}

internal sealed class PartyFinderContributor : IDisposable {
    internal static readonly TimeSpan UploadDelay = TimeSpan.FromSeconds(10);
    private readonly IPartyFinderGui partyFinderGui;
    private readonly HttpClient http;
    private readonly IPluginLog log;
    private readonly Func<bool> enabled;
    private readonly Uri endpoint;
    private readonly ConcurrentDictionary<int, ConcurrentQueue<PartyFinderContributionListing>> batches = new();
    private DateTime nextUploadUtc = DateTime.MaxValue;
    private bool uploadInFlight;
    private bool disposed;

    internal PartyFinderContributor(IPartyFinderGui partyFinderGui, HttpClient http, IPluginLog log, Func<bool> enabled, Uri endpoint) {
        this.partyFinderGui = partyFinderGui;
        this.http = http;
        this.log = log;
        this.enabled = enabled;
        this.endpoint = endpoint;
        partyFinderGui.ReceiveListing += OnListing;
    }

    private void OnListing(IPartyFinderListing listing, IPartyFinderListingEventArgs args) {
        if (disposed || !enabled()) return;
        try {
            batches.GetOrAdd(args.BatchNumber, _ => new()).Enqueue(new PartyFinderContributionListing(listing));
            // A page can arrive in more than one callback. Wait until ten seconds
            // after the newest listing so one direct request carries the burst.
            nextUploadUtc = DateTime.UtcNow.Add(UploadDelay);
        } catch (Exception error) {
            log.Warning(error, "Unable to prepare a public Party Finder listing for xivpf.");
        }
    }

    internal void Tick(DateTime now) {
        if (disposed) return;
        if (!enabled()) {
            batches.Clear();
            nextUploadUtc = DateTime.MaxValue;
            return;
        }
        if (uploadInFlight || now < nextUploadUtc || batches.IsEmpty) return;
        nextUploadUtc = now.Add(UploadDelay);
        var pending = DrainPending();
        if (pending.Count == 0) return;
        uploadInFlight = true;
        _ = UploadAsync(pending);
    }

    private List<PartyFinderContributionListing> DrainPending() {
        var pending = new List<PartyFinderContributionListing>();
        foreach (var (batchNumber, queue) in batches.ToArray()) {
            if (!batches.TryRemove(batchNumber, out _)) continue;
            while (queue.TryDequeue(out var listing)) pending.Add(listing);
        }
        return pending
            .GroupBy(listing => (listing.LastServerRestart, listing.CreatedWorld, listing.Id))
            .Select(group => group.Last())
            .ToList();
    }

    private async Task UploadAsync(List<PartyFinderContributionListing> pending) {
        try {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) {
                Content = new StringContent(JsonSerializer.Serialize(pending), Encoding.UTF8, "application/json"),
            };
            request.Headers.UserAgent.ParseAdd($"GillionsGameSync/{typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "unknown"}");
            using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"xivpf returned HTTP {(int)response.StatusCode}.");
            log.Information("Contributed {Count} public Party Finder listings directly to xivpf.", pending.Count);
        } catch (Exception error) {
            // Preserve this bounded batch for the next ten-second attempt. New
            // listings coalesce with it, and Tick still permits at most six
            // requests per minute.
            var retry = batches.GetOrAdd(-1, _ => new());
            foreach (var listing in pending) retry.Enqueue(listing);
            log.Warning(error, "Direct xivpf Party Finder contribution failed; the bounded batch remains queued.");
        } finally {
            uploadInFlight = false;
        }
    }

    public void Dispose() {
        if (disposed) return;
        disposed = true;
        partyFinderGui.ReceiveListing -= OnListing;
        batches.Clear();
    }
}

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
    [JsonPropertyName("slots")] public List<PartyFinderContributionSlot> Slots { get; }
    [JsonPropertyName("jobs_present")] public List<byte> JobsPresent { get; }

    internal PartyFinderContributionListing(IPartyFinderListing listing) {
        Id = (uint)listing.Id;
        ContentIdLower = (uint)listing.ContentId;
        Name = listing.Name.Encode();
        Description = listing.Description.Encode();
        CreatedWorld = (ushort)listing.World.Value.RowId;
        HomeWorld = (ushort)listing.HomeWorld.Value.RowId;
        CurrentWorld = (ushort)listing.CurrentWorld.Value.RowId;
        Category = (uint)listing.Category;
        Duty = listing.RawDuty;
        DutyType = (byte)listing.DutyType;
        BeginnersWelcome = listing.BeginnersWelcome;
        SecondsRemaining = listing.SecondsRemaining;
        MinItemLevel = listing.MinimumItemLevel;
        NumParties = listing.Parties;
        SlotsAvailable = listing.SlotsAvailable;
        LastServerRestart = (uint)listing.LastPatchHotfixTimestamp;
        Objective = (uint)listing.Objective;
        Conditions = (uint)listing.Conditions;
        DutyFinderSettings = (uint)listing.DutyFinderSettings;
        LootRules = (uint)listing.LootRules;
        SearchArea = (uint)listing.SearchArea;
        Slots = listing.Slots.Select(slot => new PartyFinderContributionSlot(slot)).ToList();
        JobsPresent = listing.RawJobsPresent.ToList();
    }
}

internal sealed class PartyFinderContributionSlot {
    [JsonPropertyName("accepting")] public uint Accepting { get; }
    internal PartyFinderContributionSlot(PartyFinderSlot slot) {
        Accepting = slot.Accepting.Aggregate(0u, (aggregate, flag) => aggregate | (uint)flag);
    }
}
