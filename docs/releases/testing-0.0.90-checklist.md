# Testing 0.0.90 owner checklist (about 20 minutes)

Details for each step: [testing-0.0.90.md](testing-0.0.90.md). Nothing here
publishes anything or changes a feed.

**Before you start.**
- Launch through XIVLauncher on the normal (release) Dalamud channel.
- You need the candidate `GillionsGameSyncTest.dll` (version 0.0.90.0).

| # | Do | Expect | Result |
|---|---|---|---|
| 1 | `/xlplugins`: **disable** (don't uninstall) Gillions Game Sync and Gillions Game Sync Testing | Both disabled | ☐ |
| 2 | `/xlsettings` → Experimental → Dev Plugin Locations: add the candidate folder, save. `/xlplugins` → Dev Tools: enable it | One Game Sync copy running | ☐ |
| 3 | `/gillionssynctest` → Settings → Advanced | `0.0.90.0 · Channel: Testing`, `Game: 2026.10.16.0000.0000` | ☐ |
| 4 | Advanced → Testing diagnostics → Hunt diagnostics → *Copy aggregate Hunt diagnostics* | `SDK: 7.57.0.9414`, not "Unsupported game/SDK build" | ☐ |
| 5 | Settings → Hunts → *Show Hunt Progress*; stand where a held bill's targets are | Counts match your Hunt log | ☐ |
| 6 | Teleport once | Panel recovers; nothing marked complete that isn't | ☐ |
| 7 | (Workshop access) `/gillionssubs` → enable local retention → open the voyage list | Submarines listed with matching rank and return time | ☐ / n/a |
| 8 | Leave the workshop | History kept, not emptied | ☐ / n/a |
| 9 | `/gillionsfates` | Not "Unsupported source/game matrix". If paired, the Site reason (e.g. `FATE_SOURCE_UNSUPPORTED`) = **Site-dependent**, not a failure | ☐ |
| 10 | Travel: leave Hunt routing location consent **off** | No location sharing appears | ☐ |
| 11 | Play 10–15 min; `/xlstats` | No new hitches; note Game Sync's time | ☐ |
| 12 | Disable the dev copy; re-enable your usual copy | Back to normal | ☐ |

**Send back.**
- The ☐ results.
- The copied Hunt diagnostics text, plus the submarine one if tested.
- The FATE "Remote contribution" line.
- A cropped Hunt Progress screenshot, with no character name.

**Never send.** Configuration files, pairing codes, PRIVATE JSON or logs.

**Outcome.**
- Rows 3–6 and 11 pass → Hunts accepted.
- Rows 7–8 pass → submarines accepted.
- FATE and Travel stay Site-dependent until Gillions admits the new game version.
