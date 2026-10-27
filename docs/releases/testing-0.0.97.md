# Testing 0.0.97: game version and local settings in the check-in

Status: **candidate, not published**. Testing 0.0.96 remains the published
release. Publication requires independent review, deterministic builds,
upgrade verification and the owner's explicit approval.

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

This candidate packages the already merged check-in change from PR #29.
These fields are sent only to Gillions origins; the Testing product retains
its existing https://test.gillions.app origin and separate identity. It also
retains the character-not-linked and travel back-off already published in
Testing 0.0.96.

## Verification

Candidate validation passed `scripts/verify.ps1` with qualified Dalamud
15.0.3.9, including the local-preference source/serialization tests, both
product builds with warnings as errors and actual configuration serializers.
The upgrade modes name the exact published predecessor
0.0.96.0 and candidate 0.0.97.0.

Independent review and the exact published-DLL upgrade run are separate
gates before staging and publication. No in-game or live Site acceptance is
claimed by preparing this candidate.
