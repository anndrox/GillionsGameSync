"""Presence local preferences mirror the plugin's legacy permission inputs; no network."""
from pathlib import Path
import re

r = Path(__file__).resolve().parents[1]
plugin = '\n'.join((r / name).read_text(encoding='utf-8').replace('\r\n', '\n') for name in ['Plugin.cs', 'PluginPermissions.cs'])
local = plugin.split('private PresenceLocalPreferences? LocalPreferences() =>', 1)[1].split('#else', 1)[0]
# Only Gillions permission origins receive them, on the one presence factory.
assert 'SitePermissionOrigin ? LocalPreferences() : null,' in plugin
assert plugin.count('RetainerPresencePolicy.CreateNative(') == 1
# Every local input a "legacy" permission decision falls back to is reported.
for key in ['marketContribution', 'partyFinderContribution', 'personalHunts', 'personalSubmarines', 'huntRoutingLocation', 'itemLinks']:
    for expression in re.findall(r'PermissionEnabled\("' + key + r'", ([^)]+)\)', plugin):
        assert expression.strip() in local, (key, expression)
for expression in ['configuration.HuntBills.LocalRetentionEnabled', 'configuration.SubmarineVoyages.LocalRetentionEnabled',
                   'configuration.EnablePartyFinderLinkRequests', 'configuration.AutomaticallyShowHuntMap']:
    assert expression in local, expression
print('Presence local preferences PASS: Gillions origins only, one presence factory, every legacy permission input reported.')
