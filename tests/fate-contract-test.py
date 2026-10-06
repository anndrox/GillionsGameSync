"""Read-only FATE source boundaries; no live-game or performance claim."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
native = (root / 'FateLocalView.cs').read_text(encoding='utf-8')
model = (root / 'FateObservations.cs').read_text(encoding='utf-8')
transport = (root / 'FateTransport.cs').read_text(encoding='utf-8')
plugin = (root / 'Plugin.cs').read_text(encoding='utf-8')
assert native.startswith('#if GILLIONS_TEST_BUILD')
assert model.startswith('#if GILLIONS_TEST_BUILD || GILLIONS_FATE_TESTS')
assert transport.startswith('#if GILLIONS_TEST_BUILD || GILLIONS_FATE_TESTS')
code = re.sub(r'//[^\n]*', '', native)
assert not re.search(r'unsafe|->|\.Address|HomeWorld|LocalPlayer|ObjectTable|TimeRemaining|ExtractText|GetIpc|SendAsync|HttpClient|FireCallback|ReceiveEvent|WriteAll|SavePluginConfig', code)
assert 'private readonly IFateTable table;' in native
assert not re.search(r'private .*\bIFate\b', native)
assert 'player.CurrentWorld' in native and 'world.Value.IsPublic' in native
assert 'TerritoryUse.Overworld' in native and 'ContentFinderCondition.RowId!=0' in native
for flag in ['BetweenAreas','BetweenAreas51','LoggingOut','ReadyingVisitOtherWorld','WaitingToVisitOtherWorld','BoundByDuty','BoundByDuty56','BoundByDuty95']:
    assert f'ConditionFlag.{flag}' in native
for event, handler in [('Login','Invalidate'),('Logout','Logout'),('ZoneInit','Zone'),('TerritoryChanged','Changed'),('InstanceChanged','Changed'),('MapIdChanged','Changed')]:
    assert f'client.{event}+={handler}' in native and f'client.{event}-={handler}' in native
tick = native.split('internal void Tick',1)[1].split('internal string Diagnostic',1)[0]
assert tick.index('monotonic<nextRead') < tick.index('before=Context()') < tick.index('count=table.Length')
assert tick.index('!FatePolicy.Compatible(source)') < tick.index('count=table.Length')
assert 'settled!=before || settleEpoch!=state.Epoch' in tick
assert 'var after=Context()' in tick and 'epoch!=state.Epoch || count!=table.Length' in tick
assert 'count is <0 or >FatePolicy.MaximumRows' in tick and 'i<count' in tick
assert 'definition.Value.EurekaFate!=0' in tick
assert 'nextRead=' not in native.split('private void Invalidate()',1)[1].split('internal void ClearAuthorization',1)[0]
assert 'nextRead=' not in native.split('private void Start()',1)[1].split('private void Draw()',1)[0]
assert 'measuring=true' not in native.split('internal FateLocalView',1)[1].split('private string GameVersion',1)[0]
assert 'fateLocal.Tick(now)' in plugin and 'fateLocal.Dispose()' in plugin and 'fateLocal?.ClearAuthorization()' in plugin
config = plugin.split('public sealed class PluginConfiguration',1)[1]
assert not re.search(r'public .*Fate(Observation|Prepared|Admission)|public bool .*Fate',config)
assert not re.search(r'SendAsync|new HttpClient',native)
assert not re.search(r'new FateAdmission|FateTransportPolicy.Request',native)
sender = (root / 'FateSenderState.cs').read_text(encoding='utf-8')
discovery = (root / 'FateDiscovery.cs').read_text(encoding='utf-8')
assert 'TickFateSender(now)' in plugin and 'fateSender.Dispose()' in plugin
assert 'SyncRequestMode.Manual' in plugin.split('private void TickFateSender',1)[1].split('private MarketContributionSession',1)[0]
assert 'personalHttp.SendAsync' in plugin.split('private async Task<HttpResponseMessage> DispatchFateAsync',1)[1].split('private async Task DiscoverFateAsync',1)[0]
assert 'clock+10000' in sender and 'clock+5000' in sender
assert 'batch.OldestObservation<=consentAfter' in sender
assert not re.search(r'WriteAll|SavePluginConfig|File\.|IPlayerState|IObjectTable|IFateTable',sender+discovery)
assert 'body=(byte[])bytes.Clone()' in model and 'internal byte[] CopyBody()' in model
assert 'timedOccurrences.Count < 128' in model and 'costs.Count == 240' in model
assert not re.search(r'WriteAll|SavePluginConfig|File\.',model+transport)
print('FATE source boundaries PASS: typed public API; independent bounded reads; lifecycle invalidation; RAM-only sender; exact Site policy; no Stable/config expansion.')
