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
testing_group = re.search(r"<PropertyGroup Condition=\"'\$\(GillionsTestBuild\)' == 'true'\">(.*?)</PropertyGroup>", project, re.S).group(1)
stable_now = re.search(r'<Version>([\d.]+)</Version>', project.replace(testing_group, '')).group(1)
testing_now = re.search(r'<Version>([\d.]+)</Version>', testing_group).group(1)
assert stable_now.startswith('1.') and testing_now.startswith('0.0.'), (stable_now, testing_now)
assert qualified, 'no native family parsed from NativeCompatibility.cs'
def parts(v): return tuple(int(x) for x in (v + '.0' * (3 - v.count('.'))).split('.'))
for scope, entry in table['scopes'].items():
    assert sorted(entry) == ['stable', 'testing'], scope
    for channel, current in (('stable', stable_now), ('testing', testing_now)):
        value = entry[channel]
        if scope in family:
            # Null exactly when the family is not qualified for this build.
            assert (value is None) == (family[scope] not in qualified), f'{scope} {channel}: {family[scope]} qualification vs {value}'
        if value is None: continue
        assert re.fullmatch(r'\d+\.\d+\.\d+\.\d+', value), (scope, channel, value)
        assert value.startswith('1.' if channel == 'stable' else '0.0.'), (scope, channel, value)
        assert parts(value) <= parts(current), f'{scope} {channel} {value} is newer than the project version {current}'
print(f'Site scope minimums PASS: {len(table["scopes"])} scopes for {game}; native scopes match qualified families {sorted(qualified)}.')
