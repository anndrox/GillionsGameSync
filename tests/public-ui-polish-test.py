"""Bounded presentation-only diff audit; not a transport acceptance suite."""
from pathlib import Path
import subprocess
import re

root = Path(__file__).resolve().parents[1]
base = '8ebbab6988751abe17420705f8186d9ba88a9811'
def previous(path):
    return subprocess.check_output(['git', 'show', f'{base}:{path}'], cwd=root).decode()
ui = (root / 'PluginPublicUi.cs').read_text(encoding='utf-8')
old = previous('PluginPublicUi.cs')
assert ui.split('private void DrawBranding', 1)[0] == old.split('private void DrawBranding', 1)[0]
assert ui.split('private void DrawHuntProgress()', 1)[1] == old.split('private void DrawHuntProgress()', 1)[1]
# The existing action statements and authoritative destinations are unchanged.
for marker in ['QueueUiAction', 'RequestConfigurationSave', 'SyncAsync()',
               'Util.OpenLink', 'publicUi.Show', 'publicUi.FinishPairing',
               'configuration.ActiveSession', 'PairingUrl(uiServerAddress)',
               'nextOrigin+', 'Link("Open privacy', 'ImGui.Checkbox',
               'ImGui.SetClipboardText', 'DrawPairingControls(uiState)']:
    # Explicit owner-approved label/widget polish; action bodies stay identical.
    lines = lambda text: [line.strip().replace('"Disconnect Game Sync"','"Disconnect"').replace('ImGui.SmallButton("xivpf.com")','ImGui.Button("xivpf.com")') for line in text.splitlines() if marker in line]
    assert lines(ui) == lines(old), marker
for path in ['PublicGameSyncUi.cs', 'PublicExperience.cs',
             'Plugin.cs', 'PluginPermissions.cs', 'HuntFocus.cs', 'HuntMapRequests.cs',
             'GillionsGameSync.csproj']:
    assert (root / path).read_text(encoding='utf-8') == previous(path), path
assert 'DrawPairingBranding()' in ui and 'DrawBranding(32)' in ui and 'DrawBranding(20)' in ui
assert 'Connected to Gillions' in ui and 'DrawFeatureHealth(h,sharedUnavailable)' in ui
assert 'band.Y,band.Y' in ui and 'GillionsGameSync.Branding.png' in ui
assert 'You’ll continue on ' in ui and ' to connect this device.' in ui
assert 'h.Hunts=="Gillions unavailable" && h.PartyFinder=="Gillions unavailable"' in ui
assert 'h.Fates=="Gillions unavailable" && h.Market=="Gillions unavailable"' in ui
assert 'DrawFeatureHealth(publicHealth)' in ui # Advanced keeps the underlying statuses.
final_base='da099bb215d066c72c332beb166c13ec824d41f1'
prior_ui=subprocess.check_output(['git','show',f'{final_base}:PluginPublicUi.cs'],cwd=root).decode()
def section(text,start,end):
    return text.split(start,1)[1].split(end,1)[0]
assert section(ui,'private void DrawBranding','private static void DrawConnection') == section(prior_ui,'private void DrawBranding','private static void DrawConnection')
assert section(ui,'private void DrawPublicSettings','if(ImGui.BeginTabItem("Connection"))') == section(prior_ui,'private void DrawPublicSettings','if(ImGui.BeginTabItem("Connection"))')
new_main=section(ui,'private void DrawMainPublic','private void DrawPairingPublic')
old_main=section(prior_ui,'private void DrawMainPublic','private void DrawPairingPublic')
new_main=re.sub(r'        bool sharedUnavailable=.*?        DrawFeatureHealth\(h,sharedUnavailable\);', '        DrawFeatureHealth(h);',new_main,flags=re.S)
assert new_main==old_main # The only Main delta is the shared-outage display case.
ledger=(root/'docs/public-ui-polish.md').read_text(encoding='utf-8')
assert all(column in ledger for column in ['Current implementation','Disposition','Timing','Presentation vs functional','Reason / trigger','Source review'])
old_ledger=subprocess.check_output(['git','show',f'{final_base}:docs/public-ui-polish.md'],cwd=root).decode()
recommendations=lambda text: {line.split('|')[1].strip() for line in text.splitlines() if line.startswith('| ') and not line.startswith('| ---') and not line.startswith('| Recommendation')}
assert recommendations(old_ledger)<=recommendations(ledger) # No recovered criticism disappears.
print('Presentation diff audit PASS: actions, destinations, lifecycle, config, window identity and Hunt rendering unchanged.')
