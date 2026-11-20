"""Promoted release boundaries and unchanged runtime, not live acceptance.

The owner-authorized live sizing/status correction intentionally supersedes
historical pixel equality. public-ui-contract-test.py and compiled renders cover
its UI; the original recommendation ledger, branding and transport stay frozen.
"""
from pathlib import Path
import subprocess
r=Path(__file__).resolve().parents[1]
base='414c1db3c4d921f9d667ad1afc976c217332456a'
def old(p):return subprocess.check_output(['git','show',base+':'+p],cwd=r).decode().replace('\r\n','\n')
assert (r/'docs/public-ui-polish.md').read_text(encoding='utf-8')==old('docs/public-ui-polish.md')
runtime_base='54492975c7b6ff2f90f3745bcf6bcde12365fbee'
cs=(r/'GillionsGameSync.csproj').read_text()
assert '<Version>1.0.31.13</Version>' in cs and 'GILLIONS_PUBLIC_BUILD' in cs
assert 'https://xivpf.com/contribute/multiple' not in cs
for p in ['FateTransport.cs','FateDiscovery.cs','PersonalSync.cs','TravelSync.cs','MarketContributionCore.cs','PartyFinderLinkRequests.cs']:
 text=(r/p).read_text()
 previous=subprocess.check_output(['git','show',runtime_base+':'+p],cwd=r).decode().replace('\r\n','\n')
 # These three wire fixes have focused behavior tests. Keep the rest frozen.
 contract_changes={
  'FateTransport.cs': ('    };\n', '    internal static int RetrySeconds', '    internal static FateSendDisposition Response'),
  'PersonalSync.cs': ('    internal static bool HuntCoverageFresh', '    internal static bool PayloadValid', None),
  'TravelSync.cs': ('        Binding = binding; ObservedAtUtc = observation.ObservedAtUtc;\n', '        Nonce = ', None),
 }
 if p in contract_changes:
  start,end,old_start=contract_changes[p]
  if p=='FateTransport.cs':
   start='    // The body is immutable UTF-8'
  def outside_change(source, marker):
   left, rest=source.split(marker,1)
   return left+end+rest.split(end,1)[1]
  assert outside_change(text,start)==outside_change(previous,old_start or start),p
 else:
  assert text==previous,p
 assert 'NativeProduct.Name' in text,p
 assert '"GillionsGameSyncTest"' not in text,p
for p in ['BeastmasterLocalView.cs','DashboardLocalView.cs']:
 assert (r/p).read_text().startswith('#if GILLIONS_TEST_BUILD\n'),p
for p in ['FateLocalView.cs','TravelContextLocalView.cs','SubmarineLocalView.cs']:
 text=(r/p).read_text()
 assert '#if GILLIONS_TEST_BUILD\n    private void Draw' in text or '#if GILLIONS_TEST_BUILD\n    internal string Diagnostic' in text or '#if GILLIONS_TEST_BUILD\n    private void Change' in text,p
assert subprocess.check_output(['git','show',base+':assets/GillionsGameSync-icon-v4.png'],cwd=r)==(r/'assets/GillionsGameSync-icon-v4.png').read_bytes()
print('Public source checks PASS: historical ledger/icon and accepted transport unchanged; explicit channel identity; diagnostic renderers excluded. UI corrections validated separately, not live acceptance.')
