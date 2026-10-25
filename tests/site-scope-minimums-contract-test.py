"""Site scope minimums: shape, keys and native qualification; no network."""
from pathlib import Path
import json, re

r = Path(__file__).resolve().parents[1]
table = json.loads((r / 'docs/contracts/site-scope-minimums-v1.json').read_text(encoding='utf-8'))
assert sorted(table) == ['gameVersion', 'schemaVersion', 'scopes'] and table['schemaVersion'] == 1
game = table['gameVersion']
assert re.fullmatch(r'\d{4}\.\d{2}\.\d{2}\.\d{4}\.\d{4}', game)

authority = (r / 'PermissionAuthority.cs').read_text(encoding='utf-8')
keys = re.findall(r'"(\w+)"', authority.split('internal static readonly string[] Keys = [', 1)[1].split('];', 1)[0])
assert len(keys) == 8 and sorted(table['scopes']) == sorted(keys + ['fate']), sorted(table['scopes'])

# Which native families qualify this game build (NativeCompatibility.cs is the authority).
native = (r / 'NativeCompatibility.cs').read_text(encoding='utf-8')
builds = dict(re.findall(r'NativeBuild (\w+) = new\("([\d.]+)"', native))
qualified = set()
for families, names in re.findall(r'((?:NativeFamily\.\w+(?: or )?)+) => \[([\w, ]*)\]', native):
    if any(builds.get(name.strip()) == game for name in names.split(',') if name.strip()):
        qualified.update(re.findall(r'NativeFamily\.(\w+)', families))
family = {'personalHunts': 'Hunt', 'personalSubmarines': 'Submarine', 'huntRoutingLocation': 'Travel', 'fate': 'Fate'}

project = (r / 'GillionsGameSync.csproj').read_text(encoding='utf-8')
stable_now, testing_now = re.findall(r'<Version>([\d.]+)</Version>', project)[:2]
def parts(v): return tuple(int(x) for x in (v + '.0' * (3 - v.count('.'))).split('.'))
for scope, entry in table['scopes'].items():
    assert sorted(entry) == ['stable', 'testing'], scope
    for channel, current in (('stable', stable_now), ('testing', testing_now)):
        value = entry[channel]
        if scope in family and family[scope] not in qualified:
            assert value is None, f'{scope} {channel}: {family[scope]} is not qualified for {game}'
        if value is None: continue
        assert re.fullmatch(r'\d+\.\d+\.\d+\.\d+', value), (scope, channel, value)
        assert parts(value) <= parts(current), f'{scope} {channel} {value} is newer than the project version {current}'
print(f'Site scope minimums PASS: {len(table["scopes"])} scopes for {game}; native scopes match qualified families {sorted(qualified)}.')
