"""Public presentation/lifecycle boundaries; not live UI or consent proof."""
from pathlib import Path
import re
root=Path(__file__).resolve().parents[1]
ui=(root/'PluginPublicUi.cs').read_text(encoding='utf-8')
windows=(root/'PublicGameSyncUi.cs').read_text(encoding='utf-8')
plugin=(root/'Plugin.cs').read_text(encoding='utf-8')
hunt=(root/'HuntBillLocalView.cs').read_text(encoding='utf-8')
main=ui.split('private void DrawMainPublic()',1)[1].split('private void DrawPairingPublic()',1)[0]
draw=ui.split('private void DrawHuntProgress()',1)[1]
assert 'BeginTab' not in main
assert all('BeginTabItem("'+tab+'")' in ui for tab in ['General','Hunts','Connection','Advanced'])
assert all(s in windows for s in ['WindowSystem','ImGuiCond.FirstUseEver','NoFocusOnAppearing','NoNavFocus','NoNavInputs','RespectCloseHotkey=false'])
assert 'DisableFadeInFadeOut=true' in windows and 'override void OnClose' not in windows
assert 'hunts.Flags &= ~ImGuiWindowFlags.AlwaysAutoResize' in windows
assert 'hunts.Size=new Vector2(300,180)' in windows and 'MinimumSize=new Vector2(220,100)' in windows
assert 'Flags=ImGuiWindowFlags.AlwaysAutoResize' in windows # other compact surfaces unchanged
assert 'NoSavedSettings' not in windows and 'NoResize' not in windows and 'NoScrollbar' not in windows
assert 'hunts.Flags = locked() ? hunts.Flags | ImGuiWindowFlags.NoMove' in windows
assert 'huntWindow.Observe(requestedOpen,hunts.IsOpen' in windows
assert not re.search(r'SetNextWindowPos|SetWindowFocus|SetNextWindowFocus|SetKeyboardFocus',windows+ui)
assert not re.search(r'MapLink|OpenMap|SyncAsync|GetExcelSheet|PlayerState|ContentId|ReadLocal|Request\(',draw)
assert 'configuration.ShowHuntProgress=show' in ui and 'configuration.LockHuntProgressPosition=locked' in ui
assert not re.search(r'configuration\.(Contribute|Enable.*Contribution|SyncPersonal|ShareHuntRouting|AutomaticallyShowHuntMap)\w*\s*=',ui)
assert 'PrivacyUrl' in ui and '+"/gillions-sync"' in ui
assert 'publicUi.ShowSettings()' in plugin and 'publicUi.ShowPairing()' in plugin
assert 'PublicConnectionPresentation.PairingUrl(uiServerAddress)' in ui and 'Next pairing destination:' in ui
assert 'Use default pairing destination' in ui and 'DrawActionFeedback(pairing:true)' in ui
assert 'PublicConnectionPresentation.PairingOrigin(uiServerAddress); uiPairingCode' in plugin
fate_view=(root/'FateLocalView.cs').read_text()
assert 'PRIVATE FATE Testing diagnostics' in fate_view and 'reveal your presence' in fate_view
assert 'Copy PRIVATE FATE diagnostics' in fate_view
assert 'private void DrawDiagnostics' not in plugin
assert 'uiState.Model.Warning' in main
progress=hunt.split('private void PublishProgress(',1)[1].split('#if GILLIONS_TEST_BUILD\n    private void Draw()',1)[0]
assert 'store.Characters' not in progress and 'policy.Prepare' not in progress
assert 'bool complete = false' in progress # item coverage does not prove current order/reset
assert 'target.MapId > 0' in progress and 'target.PlaceNameId > 0' in progress
assert 'ReadEnabled => policy.RetentionEnabled || ProgressRequested' in hunt
assert 'huntLocal.ProgressRequested = configuration.ShowHuntProgress' in plugin
assert 'CoveragePayload(string key, DateTime now) => policy.RetentionEnabled' in hunt
assert plugin.count('fateLocal.Tick(now)')==1 and plugin.count('TickFateSender(now)')==1
permission=(root/'PluginPermissions.cs').read_text()
apply=permission.split('private void ApplyPermissions(',1)[1].split('private void ReconcilePermissions(',1)[0]
assert apply.index('ReconcilePermissions(force: true)') < apply.index('permissionAuthority.Apply(')
assert apply.count('ReconcilePermissions(force: true)') == 2
assert 'PersonalSyncPolicy.Withdraw(configuration.PersonalSync, owner, resource)' in permission
assert 'RecordContributionDenial("partyFinderContribution", enrollmentGeneration, authorizationGeneration,' in plugin
assert 'RecordContributionDenial("marketContribution", generation, authorization,' in plugin
assert 'FATE_POLICY_OFF' not in ui and 'PublicHealth.FateState(' in ui
assert 'ImGui.TextUnformatted("Data provided by"); ImGui.SameLine();' in ui
assert 'if(ImGui.SmallButton("xivpf.com")) Util.OpenLink("https://xivpf.com")' in ui
assert 'ImGui.Button("Data provided by xivpf.com")' not in ui
assert 'commands.AddHandler("/gillionsfates",new CommandInfo((_,_)=>Show())' in (root/'FateLocalView.cs').read_text()
print('Public UI/source boundaries PASS: local presentation, one existing reader/lifecycle, safe support, no hidden policy expansion or game writes.')
