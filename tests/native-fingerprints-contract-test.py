"""A95 native fingerprints: reviewed file sets, every native reader assigned, LF-normalized, Testing-only report; no network."""
from pathlib import Path
import hashlib
import re

r = Path(__file__).resolve().parents[1]
sets = [line.split() for line in (r / 'native-fingerprints.txt').read_text(encoding='utf-8').splitlines()
        if line.strip() and not line.lstrip().startswith('#')]
names = [s[0] for s in sets]
assert names.count('core') == 1 and len(set(names)) == len(names), 'one core set and unique family names'
families = {s[0]: s[1:] for s in sets if s[0] != 'core'}
core = next(s[1:] for s in sets if s[0] == 'core')
# The Site accepts only these family keys (SITE_DESIGN.md); at most 8.
assert set(families) <= {'hunt', 'travel', 'fate', 'submarine', 'appearance', 'desynthesis'} and len(families) <= 8
assert {'NativeCompatibility.cs', 'NativeCompatibility.Runtime.cs', 'packages.lock.json'} <= set(core), 'pins and the family table are in every family'
listed = set(core).union(*map(set, families.values()))
for name in listed:
    assert (r / name).is_file(), 'listed fingerprint file is missing: ' + name

# Every top-level source with ClientStructs or unsafe code is assigned to a set, or
# explicitly outside: ordinary sync, local-only families without upload, and Plugin.cs
# (its only native read is the non-Dalamud-build ContentId path; Testing uses IPlayerState).
outside = {'Plugin.cs', 'DirectGameSnapshotCollector.cs', 'BeastmasterLocalView.cs', 'DashboardLocalView.cs'}
for path in sorted(r.glob('*.cs')):
    text = path.read_text(encoding='utf-8')
    if 'FFXIVClientStructs' in text or re.search(r'\bunsafe\b', text):
        assert path.name in listed or path.name in outside, 'new native reader needs a fingerprint set: ' + path.name


def fingerprint(family):
    text = 'family:' + family + '\n'
    for name in sorted(set(core + families[family])):
        data = (r / name).read_bytes().replace(b'\r\n', b'\n')
        text += name + '\n' + hashlib.sha256(data).hexdigest() + '\n'
    return hashlib.sha256(text.encode('utf-8')).hexdigest()


# Same result for CRLF and LF checkouts; the build's generated table must agree.
expected = {family: fingerprint(family) for family in families}
for generated in r.glob('obj/*/NativeFingerprints.g.cs'):
    found = dict(re.findall(r'\["([a-z]+)"\] = "([0-9a-f]{64})"', generated.read_text(encoding='utf-8')))
    assert found == expected, 'generated fingerprints differ from the reviewed file sets: ' + str(generated)

plugin = (r / 'Plugin.cs').read_text(encoding='utf-8').replace('\r\n', '\n')
presence = plugin.split('private void SendCurrentRetainerPresence', 1)[1].split('private void OnFrameworkUpdate', 1)[0]
assert '#if GILLIONS_TEST_BUILD\n' in presence and 'NativeFingerprints = NativeFingerprints.ByFamily' in presence.split('#if GILLIONS_TEST_BUILD\n', 1)[1].split('#endif', 1)[0], \
    'fingerprints are reported by the Testing build only'
document = (r / 'RetainerPresence.cs').read_text(encoding='utf-8')
assert 'JsonPropertyName("nativeFingerprints"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)' in document, \
    'top-level and omitted when absent (never inside capabilities)'
print('Native fingerprint sets, assignment, normalization and Testing-only report passed:', ', '.join(f'{k} {v[:12]}' for k, v in expected.items()))
