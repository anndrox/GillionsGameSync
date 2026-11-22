#if GILLIONS_TEST_BUILD
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Lumina.Excel.Sheets;

namespace GillionsGameSync;

public sealed partial class Plugin {
    private bool MapFlagsEnabled => configuration.EnableMapFlagRequests && SitePermissionOrigin
        && HasPairedSession && activeOwnedState is not null
        && permissionAuthority.Allows(MapFlagPolicy.PermissionKey, true, Environment.TickCount64);

    private bool MapFlagPermitted(SyncRequestPermit permit) => PermitIsCurrent(permit) && MapFlagsEnabled
        && MapFlagPolicy.Ready(clientState.IsLoggedIn,
            marketConditions[ConditionFlag.BetweenAreas], marketConditions[ConditionFlag.BetweenAreas51]);

    private bool MapFlagCatalogValid(MapFlagRequest request) {
        var map = dataManager.GetExcelSheet<Map>().GetRowOrDefault(request.MapId);
        return MapFlagPolicy.CatalogValid(request.TerritoryId,
            dataManager.GetExcelSheet<TerritoryType>().HasRow(request.TerritoryId),
            map is not null, map?.SizeFactor ?? 0, map?.TerritoryType.RowId ?? 0);
    }

    private async Task ProcessMapFlagAsync(SyncRequestPermit permit, System.Text.Json.JsonElement root, WebsiteResponseClock clock) {
        var request = MapFlagPolicy.Parse(root, clock.UtcNow);
        var attempts = await framework.RunOnFrameworkThread(() => { RequirePermit(permit); return huntMapProcessor; });
        var result = await MapFlagProcessor.ProcessAsync(request, attempts, () => clock.UtcNow,
            () => framework.RunOnFrameworkThread(() => MapFlagPermitted(permit)),
            r => ConsumeMapFlagAsync(permit, r, clock),
            r => framework.RunOnFrameworkThread(() => {
                if (!MapFlagPermitted(permit) || !MapFlagPolicy.Valid(r, clock.UtcNow) || !MapFlagCatalogValid(r)) return false;
                if (!huntMapGui.OpenMapWithMapLink(new MapLinkPayload(r.TerritoryId, r.MapId, r.MapX, r.MapY))) return false;
                chatGui.Print($"Gillions: flagged {r.Label} on your map.");
                return true;
            }));
        await CommitAsync(permit, _ => RecordDiagnostic($"Map flag: {result}."));
    }

    private async Task<bool> ConsumeMapFlagAsync(SyncRequestPermit permit, MapFlagRequest flag, WebsiteResponseClock clock) {
        if (!await framework.RunOnFrameworkThread(() => MapFlagPermitted(permit)
            && MapFlagPolicy.Valid(flag, clock.UtcNow) && MapFlagCatalogValid(flag))) return false;
        using var deadline = HuntMapTransport.Deadline(permit.Cancellation, CancellationToken.None, flag.ExpiresAtUtc, clock.UtcNow);
        using var request = Request("/api/game-sync/item-links/consume", permit, MapFlagPolicy.ConsumeBody(flag));
        Task<HttpResponseMessage>? pending = null;
        await framework.RunOnFrameworkThread(() => {
            RequireDispatch(request, permit);
            if (!MapFlagPermitted(permit) || !MapFlagPolicy.Valid(flag, clock.UtcNow)) throw new OperationCanceledException();
            pending = personalHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        });
        using var response = await pending!;
        return response.StatusCode == HttpStatusCode.OK
            && HuntMapPolicy.Consumed(await HuntMapTransport.ReadAsync(response.Content, deadline.Token));
    }
}
#endif
