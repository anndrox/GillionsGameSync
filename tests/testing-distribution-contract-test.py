"""Channel isolation/source correspondence; no network or player state."""
from pathlib import Path
import subprocess

r=Path(__file__).resolve().parents[1]
public='43a86941f8f8e60c583c961b1502345df76c7e64'
# The owner-directed status correction (2026-10-24 regression audit) replaces only
# the PublicHealth status record; every other presentation rule stays byte-frozen.
def outside_status(text):
    head,rest=text.split('internal sealed record PublicHealth(',1)
    return head+rest.split('\n}\n',1)[1]
experience=(r/'PublicExperience.cs').read_bytes().replace(b'\r\n',b'\n').decode('utf-8')
accepted=subprocess.check_output(['git','show',public+':PublicExperience.cs'],cwd=r).replace(b'\r\n',b'\n').decode('utf-8')
assert outside_status(experience)==outside_status(accepted)
hunt=(r/'HuntBillLocalView.cs').read_text(encoding='utf-8').replace('    internal bool Reading => ReadEnabled;\n','')
assert hunt.replace('NativeCompatibility.Supports(NativeFamily.Hunt, ','PersonalObservationCompatibility.Supports(')==subprocess.check_output(['git','show',public+':HuntBillLocalView.cs'],cwd=r).decode().replace('\r\n','\n')
windows=(r/'PublicGameSyncUi.cs').read_text(encoding='utf-8')
normalized=windows.replace('        string idSuffix=testing ? "Test" : "";\n','').replace('+idSuffix','')
# Owner-reported duplicate windows after Reconnect -> Finish (2026-10-24): only FinishPairing may differ.
normalized=normalized.replace('    // A reconnect started from Settings returns there; only first-run onboarding\n'
    '    // opens the main window, so finishing never stacks a second Gillions window.\n'
    '    internal void FinishPairing() { pairing.IsOpen=false; if(!settings.IsOpen) main.IsOpen=true; }',
    '    internal void FinishPairing() { pairing.IsOpen=false; main.IsOpen=true; }')
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
# The historical Testing feed stays frozen. The Stable feed advances only with an
# owner-authorized Stable release: it must stay a Stable-only entry pointing at its
# own immutable GitHub release asset with a recorded checksum.
name='data/GillionsGameSyncTesting.json'
assert (r/name).read_bytes().replace(b'\r\n',b'\n')==subprocess.check_output(['git','show',public+':'+name],cwd=r).replace(b'\r\n',b'\n')
import json
stable=json.loads((r/'data/GillionsGameSync.json').read_text(encoding='utf-8'))
assert len(stable)==1 and stable[0]['InternalName']=='GillionsGameSync' and stable[0]['RepoUrl']=='https://github.com/anndrox/GillionsGameSync'
version=stable[0]['AssemblyVersion']
import re
project=(r/'GillionsGameSync.csproj').read_text(encoding='utf-8')
assert re.search(r'<Version>(\d+\.\d+\.\d+\.\d+)</Version>',project).group(1)==version,'Stable feed version must equal the Public project version'
asset=f'https://github.com/anndrox/GillionsGameSync/releases/download/v{version}/GillionsGameSync-{version}.zip'
assert all(stable[0][field]==asset for field in ['DownloadLink','DownloadLinkInstall','DownloadLinkUpdate','DownloadLinkTesting'])
# Same key set and fixed identity values as the published 1.0.31.2 Stable entry (bbbafcc).
assert sorted(stable[0])==['AcceptsFeedback','ApplicableVersion','AssemblyVersion','Author','CanUnloadAsync','DalamudApiLevel','Description',
    'DownloadCount','DownloadLink','DownloadLinkInstall','DownloadLinkTesting','DownloadLinkUpdate','IconUrl','InternalName','LastUpdate',
    'LoadPriority','LoadRequiredState','LoadSync','Name','Punchline','RepoUrl','Tags']
assert {k:stable[0][k] for k in ['Author','Name','Description','Punchline','IconUrl','DalamudApiLevel','Tags','ApplicableVersion']}=={
    'Author':'Gillions','Name':'Gillions Game Sync',
    'Description':'Opt-in character synchronization for Gillions with read-only Retainer observations. Never automates gameplay or sends Square Enix credentials.',
    'Punchline':'Opt-in account sync for your Gillions profile.',
    'IconUrl':'https://raw.githubusercontent.com/anndrox/GillionsGameSync/main/assets/GillionsGameSync-icon-v4.png',
    'DalamudApiLevel':15,'Tags':['inventory','collection','utility'],'ApplicableVersion':'any'}
assert re.fullmatch(r'[0-9a-f]{64}  '+re.escape(f'GillionsGameSync-{version}.zip'),(r/f'data/releases/v{version}.sha256').read_text().strip())
print('Testing distribution source PASS: accepted UI unchanged; only inspected Hunt source qualification differs; pairing/permit/build/redirect fences; historical Testing feed unchanged; Stable feed is a Stable-only immutable release entry.')
