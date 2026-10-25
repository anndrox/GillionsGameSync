"""Presence local preferences mirror the plugin's legacy permission inputs; no network."""
from pathlib import Path
import re

r = Path(__file__).resolve().parents[1]
plugin = '\n'.join((r / name).read_text(encoding='utf-8').replace('\r\n', '\n') for name in ['Plugin.cs', 'PluginPermissions.cs'])
local = plugin.split('private PresenceLocalPreferences? LocalPreferences() {', 1)[1].split('#else', 1)[0]
# Only Gillions permission origins receive them, on the one presence factory.
assert 'SitePermissionOrigin ? LocalPreferences() : null,' in plugin
assert plugin.count('RetainerPresencePolicy.CreateNative(') == 1
# Each named field reports its own key's legacy input (named arguments keep the order honest).
field = {'marketContribution': 'MarketContribution', 'partyFinderContribution': 'PartyFinderContribution',
         'personalHunts': 'PersonalHunts', 'personalSubmarines': 'PersonalSubmarines', 'huntRoutingLocation': 'HuntRoutingLocation',
         'itemLinks': 'ItemLinks', 'partyFinderLinks': 'PartyFinderLinks', 'automaticHuntMaps': 'AutomaticHuntMaps'}
lines = {m.group(1): m.group(2) for m in re.finditer(r'(\w+): Local\("(\w+)", ', local)}
assert {v: k for k, v in lines.items()} == {k: v for k, v in field.items()}, lines
def argument(name): return local.split(name + ': Local(', 1)[1].split('),\n', 1)[0].split(');', 1)[0]
for key in ['marketContribution', 'partyFinderContribution', 'personalHunts', 'personalSubmarines', 'huntRoutingLocation', 'itemLinks']:
    found = re.findall(r'PermissionEnabled\("' + key + r'", ([^)]+)\)', plugin)
    assert found, 'no PermissionEnabled fallback found for ' + key
    for expression in found:
        assert expression.strip() in argument(field[key]), (key, expression)
assert 'configuration.HuntBills.LocalRetentionEnabled' in argument('PersonalHunts')
assert 'configuration.SubmarineVoyages.LocalRetentionEnabled' in argument('PersonalSubmarines')
assert 'configuration.EnableItemLinkRequests && configuration.EnablePartyFinderLinkRequests' in argument('PartyFinderLinks')
assert 'configuration.AutomaticallyShowHuntMap' in argument('AutomaticHuntMaps')
assert 'historical && !permissionAuthority.ExplicitSeen(key)' in local
print('Presence local preferences PASS: Gillions origins only, one presence factory, each key reports its own legacy input.')
