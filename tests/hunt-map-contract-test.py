"""Testing map actions are bounded presentation, not gameplay automation."""
from pathlib import Path
root=Path(__file__).resolve().parents[1]
p=(root/'Plugin.cs').read_text(encoding='utf-8')
m=(root/'HuntMapRequests.cs').read_text(encoding='utf-8')
assert m.startswith('#if GILLIONS_TEST_BUILD || GILLIONS_HUNT_MAP_TESTS')
assert 'public bool AutomaticallyShowHuntMap { get; set; }' in p
loop=p.split('private async Task PollWebsiteRequestsAsync()',1)[1].split('private bool HuntMapPermitted',1)[0]
assert 'itemLinkPollInFlight = true' in loop and 'finally' in loop
assert 'PollHuntMapRequestAsync' in loop and 'PollItemLinkRequestsAsync' in loop
assert 'EnableItemLinkRequests && HasPairedSession' in loop
hunt=p.split('private bool HuntMapPermitted',1)[1].split('private bool PartyFinderLinksPermitted',1)[0]
assert 'EnableItemLinkRequests' not in hunt and 'AutomaticSync' not in hunt and 'ShareHuntRoutingLocation' not in hunt
assert 'CapturePermit(SyncRequestMode.Personal)' in hunt and 'CreateLinkedTokenSource' in hunt
assert 'HuntMapPolicy.Admit' in hunt and 'PermitIsCurrent(permit)' in hunt
assert 'personalHttp.SendAsync' in hunt and 'HttpStatusCode.OK' in hunt and 'HuntMapPolicy.Consumed' in hunt
assert 'nextItemLinkPollUtc' not in hunt and 'nextHuntMapPollUtc' in hunt # Hunt denial cannot throttle unrelated item/PF traffic.
assert 'new { capability = HuntMapPolicy.Capability }' in hunt
assert 'GetExcelSheet<Lumina.Excel.Sheets.Map>' in hunt and 'TerritoryType.RowId == r.TerritoryId' in hunt
assert 'OpenMapWithMapLink(new MapLinkPayload(r.TerritoryId, r.MapId, r.MapX, r.MapY))' in hunt
for forbidden in ['FireCallback','Teleport(', 'TeleportTo','TargetManager','SendAction','unsafe','TrustAll','ServerCertificateCustomValidationCallback']:
    assert forbidden not in hunt+m,forbidden
assert 'ClearHuntMapRequests();' in p.split('private void ResetSessionContext()',1)[1].split('private void RefreshSessionContext()',1)[0]
assert 'observedHuntMapGuidance != configuration.AutomaticallyShowHuntMap' in p
assert 'huntMapCancellation.Cancel(); huntMapCancellation.Dispose();' in p.split('public void Dispose()',1)[1]
assert 'RequestConfigurationSave' not in hunt and 'claimToken' not in hunt.split('log.Debug',1)[1].split('private async Task',1)[0]
assert 'Revision is opaque' in m and 'new authorized request ID' in m
print('Hunt map Testing/source/consent/transport/lifecycle/API boundaries PASS')
