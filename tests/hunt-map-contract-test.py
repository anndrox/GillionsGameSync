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
assert 'CapturePermit(SyncRequestMode.Personal)' in hunt and 'HuntMapTransport.Deadline(permit.Cancellation, context.Token)' in hunt
assert 'HuntMapTransport.Deadline(cancellation, CancellationToken.None, r.ExpiresAtUtc)' in hunt
assert 'HuntMapTransport.ReadAsync(response.Content, cancellation)' in hunt
assert 'BodyTimeoutSeconds = 10' in m and 'RequestTimeoutSeconds = 15' in m and 'deadline.CancelAfter' in m
assert 'stream.ReadAsync(bytes.AsMemory(length), token)' in m
assert 'HuntMapPolicy.Admit' in hunt and 'PermitIsCurrent(permit)' in hunt
assert 'personalHttp.SendAsync' in hunt and 'HttpStatusCode.OK' in hunt and 'HuntMapPolicy.Consumed' in hunt
assert 'nextItemLinkPollUtc' not in hunt and 'nextHuntMapPollUtc' in hunt # Hunt denial cannot throttle unrelated item/PF traffic.
assert 'new { capability = context.Capability }' in hunt
assert 'capability = r.Capability' in hunt and 'HuntMapPolicy.TryPoll(json' in hunt
assert 'huntMapNegotiation.LegacyEmpty' in hunt and 'huntMapNegotiation.Unsupported' in hunt
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

focus=(root/'HuntFocus.cs').read_text(encoding='utf-8')
assert focus.startswith('#if GILLIONS_TEST_BUILD || GILLIONS_HUNT_MAP_TESTS')
assert 'time - now > TimeSpan.FromSeconds(30)' in focus and 'expiresAtUtc > now' in focus
assert 'separatelyPermitted && Supported' in focus and 'HuntMapPolicy.Exact(focus' in focus
assert 'JsonDocument' in focus and 'keys.Add(field.Name)' in focus
assert 'RequestConfigurationSave' not in focus and 'Configuration' not in focus and 'MapX' not in focus
assert 'request.Headers.Add(HuntFocusState.Header, HuntFocusState.Contract)' in p
assert 'HuntFocusEligible() && personalAccepted.Contains("hunt_bills")' in p
assert 'PersonalEnabled("hunt_bills") && HasPairedSession && activeOwnedState is not null' in p
assert 'nextHuntFocusPresenceUtc = nextRetainerPresenceUtc' in p
assert 'if (presenceInFlight || personalInFlight || now < nextHuntFocusPresenceUtc)' in p
tick=p.split('private void TickHuntFocus',1)[1].split('private void ClearTravelPending',1)[0]
assert 'SendCurrentRetainerPresence' in tick and 'Capture' not in tick and 'travelLocal' not in tick
assert 'huntFocus.Clear(); nextHuntFocusPresenceUtc = DateTime.MinValue;' in p
assert 'huntFocusActiveDiagnostic ? "active (ephemeral)"' in p # Draw reads managed status only, no native pointers.
assert 'HuntMapTransport.Deadline(permit.Cancellation, CancellationToken.None)' in p
assert 'SyncResponsePolicy.ReadAsync(response.Content, responseCancellation)' in p
print('Hunt V2/focus source/priority/independent permission/ephemeral privacy/compatibility boundaries PASS')
