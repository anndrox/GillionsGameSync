"""Bounded presentation-only diff audit; not a transport acceptance suite."""
from pathlib import Path
import subprocess

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
    lines = lambda text: [line.strip() for line in text.splitlines() if marker in line]
    assert lines(ui) == lines(old), marker
for path in ['PublicGameSyncUi.cs', 'PublicExperience.cs',
             'Plugin.cs', 'PluginPermissions.cs', 'HuntFocus.cs', 'HuntMapRequests.cs',
             'GillionsGameSync.csproj']:
    assert (root / path).read_text(encoding='utf-8') == previous(path), path
assert 'DrawBranding(56)' in ui and 'DrawBranding(32)' in ui and 'DrawBranding(20)' in ui
assert 'Connected to Gillions' in ui and 'DrawFeatureHealth(h)' in ui
assert 'band.Y,band.Y' in ui and 'GillionsGameSync.Branding.png' in ui
print('Presentation diff audit PASS: actions, destinations, lifecycle, config, window identity and Hunt rendering unchanged.')
