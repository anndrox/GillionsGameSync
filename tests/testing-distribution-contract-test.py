"""Channel isolation/source correspondence; no network or player state."""
from pathlib import Path
import subprocess

r=Path(__file__).resolve().parents[1]
public='43a86941f8f8e60c583c961b1502345df76c7e64'
for name in ['PublicExperience.cs','HuntBillLocalView.cs']:
    assert (r/name).read_bytes().replace(b'\r\n',b'\n')==subprocess.check_output(['git','show',public+':'+name],cwd=r).replace(b'\r\n',b'\n'),name
windows=(r/'PublicGameSyncUi.cs').read_text(encoding='utf-8')
normalized=windows.replace('        string idSuffix=testing ? "Test" : "";\n','').replace('+idSuffix','')
assert normalized==subprocess.check_output(['git','show',public+':PublicGameSyncUi.cs'],cwd=r).decode().replace('\r\n','\n')
ui=(r/'PluginPublicUi.cs').read_text(encoding='utf-8')
for phrase in ['Reconnect Game Sync','You already have a saved pairing.','shorter windows scroll']:
    assert phrase in ui
plugin=(r/'Plugin.cs').read_text(encoding='utf-8')
pair=plugin.split('private async Task PairAsync()',1)[1].split('private ',1)[0]
assert pair.index('!NativeProduct.RequestOrigin(origin)') < pair.index('configuration.PairingRequired = true;')
permit=plugin.split('private bool PermitIsCurrent(',1)[1].split('private ',1)[0]
assert 'NativeProduct.RequestOrigin(permit.Origin)' in permit
assert '&& NativeProduct.RequestOrigin(configuration.ActiveSession?.Origin)' in plugin
assert '#if GILLIONS_TEST_BUILD\n    private readonly HttpClient http = PartyFinderHttp.CreateClient();' in plugin
for name in ['Request(string path','SnapshotRequest(string path']:
    body=plugin.split(name,1)[1].split('private ',1)[0]
    assert body.index('NativeProduct.RequestOrigin(permit.Origin)') < body.index('new HttpRequestMessage')
assert (r/'PartyFinderContributionCore.cs').read_text().find('AllowAutoRedirect = false')>=0
pack=(r/'scripts/package.ps1').read_text()
assert "if ($Channel -eq 'testing') { $allowedRepositories += '/anndrox/GillionsGameSync-Testing' }" in pack
assert "$PublicBaseUrl -cne 'https://test.gillions.app'" in pack
for name in ['data/GillionsGameSync.json','data/GillionsGameSyncTesting.json']:
    assert (r/name).read_bytes().replace(b'\r\n',b'\n')==subprocess.check_output(['git','show',public+':'+name],cwd=r).replace(b'\r\n',b'\n')
print('Testing distribution source PASS: shared corrected components identical; pairing/permit/build/redirect fences; Stable and historical Testing feeds unchanged.')
