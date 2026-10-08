"""Frozen UI equivalence and promoted release gates, not live acceptance."""
from pathlib import Path
import subprocess
r=Path(__file__).resolve().parents[1]
base='414c1db3c4d921f9d667ad1afc976c217332456a'
def old(p):return subprocess.check_output(['git','show',base+':'+p],cwd=r).decode().replace('\r\n','\n')
ui=(r/'PluginPublicUi.cs').read_text(encoding='utf-8')
assert ui.replace('#if GILLIONS_TEST_BUILD || GILLIONS_PUBLIC_BUILD\n        if (HasPairedSession)', '#if GILLIONS_TEST_BUILD\n        if (HasPairedSession)')==old('PluginPublicUi.cs')
for p in ['PublicExperience.cs','PublicGameSyncUi.cs','docs/public-ui-polish.md']:
 assert (r/p).read_text(encoding='utf-8')==old(p),p
cs=(r/'GillionsGameSync.csproj').read_text()
assert '<Version>1.0.31.1</Version>' in cs and 'GILLIONS_PUBLIC_BUILD' in cs
assert 'https://xivpf.com/contribute/multiple' not in cs
for p in ['FateTransport.cs','FateDiscovery.cs','PersonalSync.cs','TravelSync.cs','MarketContributionCore.cs','PartyFinderLinkRequests.cs']:
 text=(r/p).read_text()
 assert 'NativeProduct.Name' in text,p
 assert '"GillionsGameSyncTest"' not in text,p
for p in ['BeastmasterLocalView.cs','DashboardLocalView.cs']:
 assert (r/p).read_text().startswith('#if GILLIONS_TEST_BUILD\n'),p
for p in ['FateLocalView.cs','TravelContextLocalView.cs','SubmarineLocalView.cs']:
 text=(r/p).read_text()
 assert '#if GILLIONS_TEST_BUILD\n    private void Draw' in text or '#if GILLIONS_TEST_BUILD\n    internal string Diagnostic' in text or '#if GILLIONS_TEST_BUILD\n    private void Change' in text,p
assert subprocess.check_output(['git','show',base+':assets/GillionsGameSync-icon-v4.png'],cwd=r)==(r/'assets/GillionsGameSync-icon-v4.png').read_bytes()
print('Public freeze/source checks PASS: frozen UI/ledger/icon identical; explicit channel identity; shared runtime; diagnostic renderers excluded.')
