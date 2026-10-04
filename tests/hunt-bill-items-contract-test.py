"""Read-only structural boundaries for absence coverage, alongside managed/SDK tests."""
from pathlib import Path
import json
import re

root = Path(__file__).resolve().parents[1]
model = (root / 'HuntBillItemCoverage.cs').read_text()
native = (root / 'HuntBillLocalView.cs').read_text()
plugin = (root / 'Plugin.cs').read_text()
contract = (root / 'PersonalSync.cs').read_text()
assert model.startswith('#if GILLIONS_TEST_BUILD || GILLIONS_PERSONAL_STATE_TESTS')
assert all(s in model for s in ('absent_confirmed', 'present_unresolved', 'unavailable'))
assert 'slots.Length == size' in model and '!s.Symbolic' in model and 's.Slot == i' in model
assert 'before != after' in model and 'Stopwatch.GetElapsedTime' in model and 'Epoch' in model
assert 'domains = snapshot.Domains' in model and 'orderOwnership = "unsupported"' in model
assert not re.search(r'File\.|Save\(|HttpClient|ContentId|ObjectTable|Update\s*\+=', model)
assert 'CopySlots()' in native and 'firstSlots.SequenceEqual(secondSlots)' in native
assert 'keyItems->GetSize() == size' in native and 'EventItem>().HasRow(s.ItemId)' in native
assert 'itemCoverage.Observe(characterKey, CurrentCharacterKey()' in native
assert 'TimeSpan.FromMilliseconds(100)' in native
assert 'huntCoverageSync.Clear(); huntLocal?.ClearCoverage();' in plugin
assert 'huntCoverageAccepted' in plugin and 'PersonalSyncPolicy.HuntCoverageCompatible' in plugin
assert 'coverage && !CoveragePreparedCurrent(prepared, permit)' in plugin
assert 'var coverageToken = coverageCancellation.Token;' in plugin
assert 'SendPersonalAsync(coveragePermit, current, coverageToken, coverage: true)' in plugin
assert 'SendPersonalAsync(coveragePermit, current, coverageCancellation.Token' not in plugin
assert 'HuntBillItemSync.NeedsCurrentSample(disposition) && !CoveragePreparedCurrent' in plugin
assert 'if (!coverage) RequestConfigurationSave();' in plugin
assert 'huntCoverageSync' not in plugin.split('public sealed class PluginConfiguration',1)[1]
assert 'itemCoverage' not in plugin.split('public sealed class PluginConfiguration',1)[1]
assert 'SyncPersonalHunts && configuration.HuntBills.LocalRetentionEnabled' in plugin
assert 'HuntCoverageHeader = "X-Gillions-Hunt-Item-Coverage"' in contract
assert 'entries.Length == 1' in contract and '"hunt-bills-v2"' in contract
example = json.loads((root / 'docs/examples/hunt-bills-v2.json').read_text())
c = example['billItemCoverage']
assert example['schemaVersion'] == 2 and c['orderOwnership'] == c['acquisitionIdentity'] == 'unsupported'
assert len(c['domains']) == 22 and {d['billTypeId'] for d in c['domains']} == set(range(22))
assert len({d['keyItemId'] for d in c['domains']}) == 22
assert all(set(d) == {'billTypeId','keyItemId','state'} for d in c['domains'])
assert c['domains'][18]['keyItemId'] == 2003509
assert not any(k in c for k in ('characterId','accountId','ownerKey','sessionId','orderId','location'))
print('Absence-only Testing/RAM/complete-slot/character/consent/version/dispatch/schema source boundaries PASS.')
