using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace GillionsGameSync;

public sealed partial class Plugin {
    private readonly PermissionAuthority permissionAuthority = new();
    private readonly Dictionary<string, string> permissionTransitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> permissionFreshFrom = new(StringComparer.Ordinal);
    private bool SitePermissionOrigin => configuration.ActiveSession?.Origin == PermissionAuthority.Origin;
#if GILLIONS_TEST_BUILD
    private bool nativeVersionReported;
#endif
    private DateTime nextPermissionMaintenanceUtc;
    private bool PermissionEnabled(string key, bool historical) => !SitePermissionOrigin ? historical
        : HasPairedSession && activeOwnedState is not null && permissionAuthority.Allows(key, historical, Environment.TickCount64);
    private bool ExplicitPermission(string key) => SitePermissionOrigin && permissionAuthority.Explicit(key, Environment.TickCount64);
    private bool ItemLinksEnabled => PermissionEnabled("itemLinks", configuration.EnableItemLinkRequests);
#if GILLIONS_TEST_BUILD
    private bool PartyFinderLinksEnabled => SitePermissionOrigin
        ? HasPairedSession && activeOwnedState is not null && permissionAuthority.PartyFinderLinks(configuration.EnableItemLinkRequests,
            configuration.EnablePartyFinderLinkRequests, Environment.TickCount64)
        : configuration.EnableItemLinkRequests && configuration.EnablePartyFinderLinkRequests;
#endif
    private bool WebsiteLinksEnabled => ItemLinksEnabled
#if GILLIONS_TEST_BUILD
        || PartyFinderLinksEnabled
#endif
        ;
#if GILLIONS_TEST_BUILD
    // Site already classifies/filters automatic vs explicit commands and validates
    // focus at poll/consume. The unchanged wire shape has no invented mode field.
    private bool HuntReceivingEnabled => SitePermissionOrigin && HasPairedSession && activeOwnedState is not null
        && permissionAuthority.HuntReceiving(configuration.AutomaticallyShowHuntMap, Environment.TickCount64);
    private bool MarketEnabled => PermissionEnabled("marketContribution", configuration.ContributeObservedMarketData);
    private bool TravelEnabled => PermissionEnabled("huntRoutingLocation", configuration.ShareHuntRoutingLocation);
    private static string PersonalPermission(string resource) => resource == "hunt_bills" ? "personalHunts" : "personalSubmarines";
    private DateTime FreshFrom(string key) => permissionFreshFrom.GetValueOrDefault(key, DateTime.MaxValue);
    private string PermissionIdentity(string key) => permissionAuthority.Identity(key, Environment.TickCount64);
    // Existing durable denial field stores only a bounded fingerprint for an
    // explicit decision, never the Site generation. Legacy uses its unchanged
    // enrollment key so old authorization stops remain effective after upgrade.
    private string ContributionAuthority(string key) => ExplicitPermission(key)
        ? PersonalSyncPolicy.Hash(configuration.ActiveSession!.Generation + ":" + PermissionIdentity(key))
        : configuration.ActiveSession!.Generation;
#endif
    private void ApplyPermissions(string json, SyncRequestPermit permit, HttpResponseMessage response, TimeSpan roundTrip) {
        if (permit.Origin != PermissionAuthority.Origin) return;
        permissionAuthority.Bind(permit.Session!.Generation + ":" + permit.ContentId, permit.Session.DeviceId);
        // Only the exact authenticated, non-redirected origin supplies issuer time.
        var now = DateTime.UtcNow;
        if (response.RequestMessage?.RequestUri?.GetLeftPart(UriPartial.Authority) != PermissionAuthority.Origin
            || roundTrip < TimeSpan.Zero || roundTrip > TimeSpan.FromSeconds(30)) { permissionAuthority.Invalidate(); return; }
        if (response.Headers.Date is { } date) {
            if ((date.UtcDateTime - now).Duration() > TimeSpan.FromSeconds(30)) { permissionAuthority.Invalidate(); return; }
            now = date.UtcDateTime.AddSeconds(1).Add(roundTrip);
        }
        permissionAuthority.Apply(json, now, Environment.TickCount64);
#if GILLIONS_TEST_BUILD
        nativeVersionReported = true;
#endif
        ReconcilePermissions(force: true);
    }
    private void ReconcilePermissions(bool force = false) {
        var now = DateTime.UtcNow;
        if (!force && now < nextPermissionMaintenanceUtc) return;
        nextPermissionMaintenanceUtc = now.AddMilliseconds(250);
        var clock = Environment.TickCount64;
        foreach (var key in PermissionAuthority.Keys) {
            var identity = SitePermissionOrigin ? permissionAuthority.Identity(key, clock) : "historical";
            if (key == "itemLinks") identity += ":" + ItemLinksEnabled;
#if GILLIONS_TEST_BUILD
            identity += ":" + (key switch {
                "marketContribution" => MarketEnabled,
                "partyFinderContribution" => PartyFinderContributionEnabled,
                "personalHunts" => PersonalEnabled("hunt_bills"),
                "personalSubmarines" => PersonalEnabled("submarine_personal"),
                "huntRoutingLocation" => TravelEnabled,
                "partyFinderLinks" => PartyFinderLinksEnabled,
                "automaticHuntMaps" => HuntReceivingEnabled,
                _ => false,
            });
#endif
            if (permissionTransitions.GetValueOrDefault(key) == identity) continue;
            permissionTransitions[key] = identity; permissionFreshFrom[key] = DateTime.UtcNow;
#if GILLIONS_TEST_BUILD
            if (key == "marketContribution") { marketContributor.SetEnabled(false); marketContributor.RefreshSession(null); }
            if (key == "partyFinderContribution") partyFinderContributor.SetEnabled(false);
            if (key == "huntRoutingLocation") { ClearTravelPending(); travelLocal.ClearSession(); }
            if (key is "personalHunts" or "personalSubmarines") {
                var resource = key == "personalHunts" ? "hunt_bills" : "submarine_personal";
                if (personalCancellation.Remove(resource, out var previous)) { previous.Cancel(); previous.Dispose(); }
                if (key == "personalHunts") huntCoverageSync.Clear();
                if (HasPairedSession && activeOwnedState is not null) {
                    var owner = PersonalSyncPolicy.Owner(configuration.ActiveSession!.Generation,
                        HuntBillRetentionPolicy.CharacterKey(activeRetainerCharacterContentId));
                    // Retained history is not erased. Obsolete prepared contribution
                    // copies/nonces are withdrawn so OFF -> ON cannot replay them.
                    if (configuration.PersonalSync.Prepared.RemoveAll(p => p.OwnerKey == owner && p.Resource == resource) > 0)
                        RequestConfigurationSave();
                }
                nextPersonalUtc = DateTime.MinValue;
            }
#endif
        }
#if GILLIONS_TEST_BUILD
        marketContributor.SetEnabled(MarketEnabled);
        travelLocal.SetEnabled(TravelEnabled);
#endif
        partyFinderContributor.SetEnabled(PartyFinderContributionEnabled);
    }
}
