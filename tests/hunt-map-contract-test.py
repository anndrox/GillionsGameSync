"""Testing map actions are bounded presentation, not gameplay automation."""
from pathlib import Path
root=Path(__file__).resolve().parents[1]
p=(root/'Plugin.cs').read_text(encoding='utf-8')
m=(root/'HuntMapRequests.cs').read_text(encoding='utf-8')
assert m.startswith('#if GILLIONS_TEST_BUILD || GILLIONS_HUNT_MAP_TESTS')
assert 'public bool AutomaticallyShowHuntMap { get; set; }' in p
loop=p.split('private void PollWebsiteRequests(DateTime now)',1)[1].split('private bool HuntMapPermitted',1)[0]
assert '!huntMapPollInFlight && huntMapPoll.TryBegin(now, focused)' in loop
assert '!itemLinkPollInFlight' in loop and 'websiteItemPoll.TryBegin(now, focused)' in loop
assert 'await' not in loop # unrelated item/PF cannot serialize Hunt behind it
assert 'PollHuntMapRequestAsync' in loop and 'PollItemLinkRequestsAsync' in loop
assert 'EnableItemLinkRequests && HasPairedSession' in loop
hunt=p.split('private bool HuntMapPermitted',1)[1].split('private bool PartyFinderLinksPermitted',1)[0]
assert 'EnableItemLinkRequests' not in hunt and 'AutomaticSync' not in hunt and 'ShareHuntRoutingLocation' not in hunt
assert 'CapturePermit(SyncRequestMode.Personal)' in hunt and 'HuntMapTransport.Deadline(permit.Cancellation, context.Token)' in hunt
assert 'HuntMapTransport.Deadline(cancellation, CancellationToken.None, r.ExpiresAtUtc, commandClock.UtcNow)' in hunt
assert 'HuntMapTransport.ReadAsync(response.Content, cancellation)' in hunt
assert 'BodyTimeoutSeconds = 10' in m and 'RequestTimeoutSeconds = 15' in m and 'deadline.CancelAfter' in m
assert 'stream.ReadAsync(bytes.AsMemory(length), token)' in m
assert 'HuntMapPolicy.Admit' in hunt and 'PermitIsCurrent(permit)' in hunt
assert 'personalHttp.SendAsync' in hunt and 'HttpStatusCode.OK' in hunt and 'HuntMapPolicy.Consumed' in hunt
assert 'websiteItemPoll' not in hunt and 'huntMapPoll.Backoff' in hunt # independent lane denial
assert 'huntMapPoll.TryBegin' not in hunt and 'AddSeconds(HuntFocusActive' not in hunt # no second deadline
assert 'huntMapPollInFlight = true' in hunt and 'finally' in hunt and 'huntMapPollInFlight = false' in hunt
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
public_ui=(root/'PluginPublicUi.cs').read_text(encoding='utf-8')
assert 'private void DrawHuntProgress()' in public_ui and 'Native' not in public_ui.split('private void DrawHuntProgress()',1)[1]
assert 'huntFocusActiveDiagnostic = HuntFocusActive(now)' in p # Existing managed focus signal remains, not a public privacy mirror.
assert 'HuntMapTransport.Deadline(permit.Cancellation, CancellationToken.None)' in p
assert 'SyncResponsePolicy.ReadAsync(response.Content, responseCancellation)' in p
print('Hunt V2/focus source/priority/independent permission/ephemeral privacy/compatibility boundaries PASS')

clock=(root/'WebsiteCommandPollClock.cs').read_text(encoding='utf-8')
trace=(root/'WebsiteCommandTrace.cs').read_text(encoding='utf-8')
assert 'focused ? 1 : 5' in clock and 'focused && !retry' in clock
assert 'Stopwatch.GetElapsedTime' in trace and 'CultureInfo.InvariantCulture' in trace
assert 'PollDispatched' in hunt and 'Hunt command timing' in hunt
for forbidden in ['Capture', 'RequestConfigurationSave', 'HttpClient', 'File.', 'claimToken']:
    assert forbidden not in clock+trace,forbidden
assert 'if (diagnostics.Count > 40)' in p # unchanged retention cap
print('Command-only independent deadline/finite focus/backoff/numeric local timing boundaries PASS')
issuer=(root/'WebsiteResponseClock.cs').read_text(encoding='utf-8')
assert 'TimeSpan.FromSeconds(30)' in issuer and 'AddSeconds(1).Add(headerRoundTrip)' in issuer
assert 'Stopwatch.GetElapsedTime' in issuer and 'ServerCertificate' not in issuer
assert 'response.RequestMessage?.RequestUri?.GetLeftPart(UriPartial.Authority) == HuntMapPolicy.Origin' in p
assert 'HuntMapPolicy.TryPoll(json, commandClock.UtcNow' in hunt
assert 'HuntMapPolicy.Valid(r, commandClock.UtcNow)' in hunt
assert 'Stopwatch.GetElapsedTime(observed) < remaining' in focus
assert 'ClearHuntMapRequests(); websiteItemPoll.Reset()' not in p
assert 'if (!commandClock.Unexpired(request.ExpiresAtUtc)) return false;' in p
assert 'request => ConsumeItemLinkRequestAsync(permit, request\n#if GILLIONS_TEST_BUILD\n                    , commandClock' in p
print('Exact-origin bounded issuer timeline/parser/consume/presentation/monotonic focus boundaries PASS')
