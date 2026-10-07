"""Testing-only travel custody/read/send boundaries, not live correctness proof."""
from pathlib import Path
import re
root=Path(__file__).resolve().parents[1]
source=(root/'TravelContextLocalView.cs').read_text(encoding='utf-8')
model=(root/'TravelContext.cs').read_text(encoding='utf-8')
plugin=(root/'Plugin.cs').read_text(encoding='utf-8')
assert source.startswith('#if GILLIONS_TEST_BUILD')
assert model.startswith('#if GILLIONS_TEST_BUILD || GILLIONS_TRAVEL_TESTS')
assert not re.search(r'SendAsync|HttpClient|UpdateAetheryteList\(|GetTeleportCost\(|Teleport\(|TeleportTo|FireCallback|ReceiveEvent|GetIpc|\.Refresh\(',source)
assert 'objects.LocalPlayer' in source and 'foreach (var' not in source.split('internal void Tick',1)[1].split('private void Unavailable',1)[0]
assert 'IAetheryteList' in source and not re.search(r'private .*IAetheryteList',source)
assert 'state.Begin(now)' in source and source.index('!PersonalObservationCompatibility.Supports') < source.index('var character = player.ContentId')
assert 'client.MapIdChanged += Changed' in source and 'client.MapIdChanged -= Changed' in source
assert 'agent->AetheryteList != &telepo->TeleportList' in source and 'agent->AddonId != addon->Id' in source
assert 'agent->AetheryteCount != count' in source and 'IsAetheryteUnlocked' in source
assert 'value.IsSharedHouse || value.IsApartment' in source and 'value.Ward != 0 || value.Plot != 0' in source
assert 'map.Value.TerritoryType.RowId != territory' in source and 'MapUtil.WorldToMap' in source
assert 'data.GetExcelSheet' not in source.split('private void Draw()',1)[1]
assert 'TravelPolicy.Round(coordinates.X)' in source and 'Actual final charged cost UNSUPPORTED' in source
transport=(root/'TravelSync.cs').read_text(encoding='utf-8')
assert transport.startswith('#if GILLIONS_TEST_BUILD || GILLIONS_TRAVEL_TESTS')
assert 'TravelObservation? latest;' in model and 'List<TravelObservation>' not in model
clear=model.split('internal void Clear()',1)[1].split('internal void Invalidate',1)[0]
assert 'nextReadUtc' not in clear
assert 'private void Logout' in source and 'state.Clear(); Reset();' in source
assert 'Math.Min(5,destinations.Length)' in source and 'label.Length>80' in source
assert 'PlaceName.Value.Name.ExtractText()' in source
assert 'public bool ShareHuntRoutingLocation { get; set; }' in plugin
assert 'travelLocal.Tick(now)' in plugin and 'travelLocal.Dispose()' in plugin
assert 'travelLocal.SetEnabled(TravelEnabled)' in (root/'PluginPermissions.cs').read_text()
ui=(root/'PluginPublicUi.cs').read_text(encoding='utf-8')
assert 'configuration.ShareHuntRoutingLocation=' not in ui
assert 'travelLocal.Invalidated += ClearTravelPending' in plugin
assert 'travelAccepted = false; travelBinding = ""; ClearTravelPending(); travelLocal?.ClearSession();' in plugin
assert 'personalHttp.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token)' in plugin
send=plugin.split('private async Task SendTravelAsync',1)[1].split('private bool PersonalEnabled',1)[0]
assert 'RequirePermit(permit)' in send and '!TravelAdmitted()' in send and '!prepared.Fresh(DateTime.UtcNow)' in send
assert 'X-Gillions-Personal-Resource' in send and 'X-Gillions-Personal-Capability' in send
assert 'status is 200 or 201 &&' in send and 'EnsureSuccessfulResponse' not in send
assert 'RequestConfigurationSave' not in send and 'prepared.Payload' not in send.split('RecordDiagnostic(',1)[1]
assert 'Save' not in transport and 'List<TravelPrepared>' not in transport and 'CancelAfter' in transport
assert 'AllowAutoRedirect = false' in (root/'PartyFinderContributionCore.cs').read_text(encoding='utf-8')
config=plugin.split('public sealed class PluginConfiguration',1)[1]
assert not re.search(r'public .*Travel(Observation|Context|Destination)',config)
assert 'travel_context' not in (root/'PersonalSync.cs').read_text(encoding='utf-8')
assert re.search(r'Resources = \["hunt_bills", "submarine_personal"\]',(root/'PersonalSync.cs').read_text(encoding='utf-8'))
assert not re.search(r'WriteAllText|SavePluginConfig|\bFile\.|\blogger\b|\blog\.',source+model+transport)
print('Travel source boundaries PASS: separate gated HTTPS transport; no persisted travel/history/game requests/radar/private housing; other resources unchanged.')
