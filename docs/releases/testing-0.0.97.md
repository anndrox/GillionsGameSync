# Testing 0.0.97: game version and local settings in the check-in

Status: **published** as [`v0.0.97-testing`](https://github.com/anndrox/GillionsGameSync-Testing/releases/tag/v0.0.97-testing),
from source `7c58cd393fcbe015551caf07155301d9efbfe239` (main CI run
40603432086 passed). Independent review of candidate PR #30 was PASS.

Owner authorization in the Game Sync chat on 2026-10-27: "Yes. Publish Testing
0.0.97 to the Testing channel through the same verified procedure: Part C of
the brief, verifying the four assets anonymously before touching the feed."

| Item | Value |
| --- | --- |
| ZIP | SHA256 `f3c243ce3c3bfc603a88fd8050e543f4340ab6cfea9587fee8f61ef1a3cbf40d`; two clean detached builds were byte-identical |
| DLL | `GillionsGameSyncTest.dll` 0.0.97.0, SHA256 `4ce3582f31d197eb6dded39a15feb143350242efe0274483cc5d26d7f826fc63` |
| Feed | SHA256 `02468eb1f74516ab815d4d34582c7eda6878b9b9538c25f6eeab2a01b334fabb`; distribution commit `8ff32ac7fc8c307bbd350475fdfe368b4b305634` |
| Embedded manifest | SHA256 `99072ac93d533d8532a9f74fb99b5719af23c52caf1b325c6fc68fe83433e711` |
| Order | All four public release assets downloaded anonymously and matched staging before the feed commit |
| Live verification | Raw feed hash and the ZIP served by DownloadLinkUpdate matched the staged bytes |
| Upgrade | Real-serializer write/check/plain sequence from the published 0.0.96.0 DLL passed on the exact 0.0.97.0 DLL |

## What a tester gets

The regular check-in includes the game version and the plugin's eight local
settings so Gillions can show them. For a setting you have never chosen on
the website, Gillions can show the plugin's value. The game version also lets
the website show the appropriate game-build notice.

The eight settings cover Market contribution, Party Finder contribution,
personal Hunt bills, personal submarines, Hunt routing location, item links,
Party Finder links and automatic Hunt maps. Their check-in fields are
`marketContribution`, `partyFinderContribution`, `personalHunts`,
`personalSubmarines`, `huntRoutingLocation`, `itemLinks`, `partyFinderLinks`
and `automaticHuntMaps`, inside `localPreferences`; the game version is
`gameVersion`.

Updating needs no re-pairing or new permission and adds no new data category
beyond those settings. The reported values do not replace a website choice
or grant consent. No setting or default changes, and no new capability is
advertised. Nothing extra is read from the game for the settings report.

## Scope

This release packages the already merged check-in change from PR #29.
These fields are sent only to Gillions origins; the Testing product retains
its existing https://test.gillions.app origin and separate identity. It also
retains the character-not-linked and travel back-off already published in
Testing 0.0.96.

## Verification

Validation passed `scripts/verify.ps1` with qualified Dalamud
15.0.3.9, including the local-preference source/serialization tests, both
product builds with warnings as errors and actual configuration serializers.
The upgrade modes name the exact published predecessor
0.0.96.0 and candidate 0.0.97.0.

Independent review passed before merging the candidate. The upgrade run used
the anonymously downloaded published 0.0.96 DLL: two identity checks, ten
upgrade checks and the new DLL's actual configuration serializer checks
(12 fixtures) passed. No re-pairing or consent reset was required by those
synthetic fixtures. No in-game or live Site acceptance is claimed.
