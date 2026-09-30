using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
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
#if GILLIONS_TEST_BUILD
            return XivpfEndpointPolicy.RequireBuildSafe(endpoint, true);
#else
            return XivpfEndpointPolicy.RequireBuildSafe(endpoint, false);
#endif
        }
    }
}

internal sealed class DalamudPartyFinderContributionSource : IPartyFinderContributionSource {
    private readonly IPartyFinderGui partyFinderGui;
    private readonly IPluginLog log;
    private readonly Func<bool> enabled;
    private bool disposed;

    internal DalamudPartyFinderContributionSource(IPartyFinderGui partyFinderGui, IPluginLog log, Func<bool> enabled) {
        this.partyFinderGui = partyFinderGui;
        this.log = log;
        this.enabled = enabled;
        partyFinderGui.ReceiveListing += OnListing;
    }

    public event Action<PartyFinderContributionListing>? ListingReceived;

    private void OnListing(IPartyFinderListing listing, IPartyFinderListingEventArgs args) {
        if (disposed || !enabled()) return;
        try {
            var snapshot = new PartyFinderListingSnapshot(
                (uint)listing.Id,
                (uint)listing.ContentId,
                listing.Name.Encode(),
                listing.Description.Encode(),
                (ushort)listing.World.Value.RowId,
                (ushort)listing.HomeWorld.Value.RowId,
                (ushort)listing.CurrentWorld.Value.RowId,
                (uint)listing.Category,
                listing.RawDuty,
                (byte)listing.DutyType,
                listing.BeginnersWelcome,
                listing.SecondsRemaining,
                listing.MinimumItemLevel,
                listing.Parties,
                listing.SlotsAvailable,
                (uint)listing.LastPatchHotfixTimestamp,
                (uint)listing.Objective,
                (uint)listing.Conditions,
                (uint)listing.DutyFinderSettings,
                (uint)listing.LootRules,
                (uint)listing.SearchArea,
                listing.Slots.Select(slot => slot.Accepting.Aggregate(0u, (aggregate, flag) => aggregate | (uint)flag)).ToArray(),
                listing.RawJobsPresent.ToArray());
            ListingReceived?.Invoke(new PartyFinderContributionListing(snapshot));
        } catch (Exception error) {
            log.Warning(error, "Unable to prepare a public Party Finder listing for xivpf.");
        }
    }

    public void Dispose() {
        if (disposed) return;
        disposed = true;
        partyFinderGui.ReceiveListing -= OnListing;
    }
}

internal sealed class DalamudPartyFinderContributionLog(IPluginLog log) : IPartyFinderContributionLog {
    public void Uploaded(int count) => log.Information("Contributed {Count} public Party Finder listings directly to xivpf.", count);
    public void Failed(Exception error) => log.Warning(error, "Direct xivpf Party Finder contribution failed; a bounded retry remains queued.");
}

internal static class PartyFinderContributorFactory {
    internal static IPartyFinderContributor Create(IPartyFinderGui partyFinderGui, HttpClient http, IPluginLog log, Func<bool> enabled) {
        DalamudPartyFinderContributionSource? source = null;
        try {
            var endpoint = XivpfEndpoints.ContributionUrl;
            var version = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "unknown";
            source = new DalamudPartyFinderContributionSource(partyFinderGui, log, enabled);
            return new PartyFinderContributor(source, http, new DalamudPartyFinderContributionLog(log), enabled, endpoint,
                $"GillionsGameSync/{version}");
        } catch (Exception error) {
            source?.Dispose();
            log.Error(error, "Party Finder contribution is unavailable; ordinary Gillions Game Sync remains active.");
            return new DisabledPartyFinderContributor();
        }
    }
}
