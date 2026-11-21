"""Channel isolation/source correspondence; no network or player state."""
from pathlib import Path
import re
import subprocess

r=Path(__file__).resolve().parents[1]
public='43a86941f8f8e60c583c961b1502345df76c7e64'
# PublicHealth and the Hunt Progress state have separate behavioral tests.
# Keep the surrounding presentation rules under the historical source comparison.
def outside_status_and_hunt_state(text):
    for declaration in ('internal sealed record PublicHealth(', 'internal sealed class HuntProgressState {'):
        head, rest = text.split(declaration, 1)
        text = head + rest.split('\n}\n', 1)[1]
    return text
experience=(r/'PublicExperience.cs').read_bytes().replace(b'\r\n',b'\n').decode('utf-8')
accepted=subprocess.check_output(['git','show',public+':PublicExperience.cs'],cwd=r).replace(b'\r\n',b'\n').decode('utf-8')
# The existing bill NPC identifier is carried only in the local display model.
npc_name_id = '    internal uint NpcNameId { get; init; }\n'
assert experience.count(npc_name_id) == 1
experience = experience.replace(npc_name_id, '')
# Owner-approved with the per-list Collections send (Partner, 2026-11-17): the window also shows the Sync now results
# that name sections ("not ready", "paused"), in the exact shape SyncSectionReport.Message builds. Only that one switch arm
# differs from the accepted file outside the PublicHealth record.
sync_result_arm="        _ when SyncSectionReport.IsMessage(value) => value,\n"
assert experience.count(sync_result_arm)==1
experience=experience.replace(sync_result_arm,'')
assert 'SyncSectionReport' not in outside_status_and_hunt_state(experience)
# Disconnect may report either confirmed Site revocation or a local-only clear.
for line in [
    '    internal const string Disconnected = "Disconnected. Saved history remains on this PC.";\n',
    '    internal const string DisconnectedLocally = "Disconnected on this PC. Gillions could not confirm revocation. Disconnect this device in Gillions settings.";\n',
]:
    assert experience.count(line) == 1
    experience = experience.replace(line, '')
experience = experience.replace('or Disconnected or DisconnectedLocally', 'or "Disconnected. Saved history remains on this PC."')
assert outside_status_and_hunt_state(experience)==outside_status_and_hunt_state(accepted)
hunt=(r/'HuntBillLocalView.cs').read_text(encoding='utf-8').replace('    internal bool Reading => ReadEnabled;\n','')
hunt=hunt[:hunt.index('    internal HuntBillItemSnapshot? CurrentCoverage')] + hunt[hunt.index('    internal DateTime? CoverageObservedAtUtc'):]
# The framework guard logs failed background reads after the existing cleanup.
hunt_fault = '''        } catch (Exception) {
            itemCoverage.Clear();
            Publish("Hunt read/save failed; history preserved. New data may be memory-only. No live correctness claim.");
            if (!force) throw;
        }'''
assert hunt.count(hunt_fault) == 1
hunt = hunt.replace(hunt_fault, '        } catch (Exception) { itemCoverage.Clear(); Publish("Hunt read/save failed; history preserved. New data may be memory-only. No live correctness claim."); }')
# The Hunts tab projects the same admitted observations without changing how they are read.
# Reverse its presentation-only additions before comparing the rest of the collector.
hunt=hunt[:hunt.index('    internal HuntTabBills TabBills')] + '    internal void ClearProgress() => Progress = HuntProgressSnapshot.Unavailable;\n' + hunt[hunt.index('    private bool ReadEnabled'):]

hunt=hunt.replace('private void ClearLiveBaseline() { ClearProgress();',
    'private void ClearLiveBaseline() { Progress = HuntProgressSnapshot.Unavailable;')
hunt=hunt.replace('itemCoverage.Clear(); ClearProgress();', 'itemCoverage.Clear(); Progress = HuntProgressSnapshot.Unavailable;')
hunt=hunt.replace('        ClearProgress();\n        LastDiagnostic', '        Progress = HuntProgressSnapshot.Unavailable;\n        LastDiagnostic')
accepted_hunt=subprocess.check_output(['git','show',public+':HuntBillLocalView.cs'],cwd=r).decode().replace('\r\n','\n')
for old_status in ['Private TEST sync is controlled separately in the main window.', 'Private TEST sync status is in the main window.']:
    accepted_hunt=accepted_hunt.replace(old_status,'Private sync requires separate permission.')
progress_end='\n#if GILLIONS_TEST_BUILD\n    private void Draw()'
def without_tab_projection(text):
    head,rest=text.split('    private void PublishProgress(',1)
    projection,tail=rest.split(progress_end,1)
    start,rows=projection.split('        var rows = new List<HuntProgressRow>();',1)
    territory_lookup=rows[rows.index('        foreach (var bill in observations)'):rows.index('            if (targetTerritory == 0) complete = false;')]
    return head+start+territory_lookup+progress_end+tail
assert without_tab_projection(hunt).replace('NativeCompatibility.SupportsRuntime(NativeFamily.Hunt, GameVersion())',
                    'PersonalObservationCompatibility.Supports(GameVersion(), typeof(MobHunt).Assembly.GetName().Version?.ToString())')==without_tab_projection(accepted_hunt)
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
assert 'configuration.PairingRequired, configuration.DeviceId, configuration.DeviceToken, NativeProduct.RequestOrigin)' in plugin
assert '    private readonly HttpClient http = PartyFinderHttp.CreateClient();' in plugin
assert 'private readonly HttpClient http = new' not in plugin
for name in ['Request(string path','SnapshotRequest(string path']:
    body=plugin.split(name,1)[1].split('private ',1)[0]
    assert body.index('NativeProduct.RequestOrigin(permit.Origin)') < body.index('new HttpRequestMessage')
assert (r/'PartyFinderContributionCore.cs').read_text().find('AllowAutoRedirect = false')>=0
# Every device-authenticated send uses the same destination check just before dispatch.
for name in ['Plugin.cs', 'PluginAppearance.cs', 'PluginDesynthesis.cs', 'PluginHuntSplit.cs', 'PluginWatchedFates.cs', 'DeviceRevocation.cs']:
    source=(r/name).read_text(encoding='utf-8')
    sends=list(re.finditer(r'\.SendAsync\(request,',source))
    last_send=0
    for send in sends:
        preceding=source[max(last_send,send.start()-1400):send.start()]
        assert ('RequireDispatch(request, permit)' in preceding
            or 'NativeProduct.CanDispatch(permit.Origin, request.RequestUri)' in preceding
            or 'NativeProduct.CanDispatch(origin, request.RequestUri)' in preceding
            or '#else\n            pending = http' in preceding),name
        last_send=send.end()
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
print('Testing distribution source PASS: accepted UI and Hunt boundaries; approved Hunt qualification and status copy; pairing/permit/build/redirect fences; historical Testing feed unchanged; Stable feed is a Stable-only immutable release entry.')
