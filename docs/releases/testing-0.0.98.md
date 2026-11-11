# Testing 0.0.98: November FFXIV patch compatibility

This experimental build uses the dedicated Gillions Game Sync Testing feed and
the separate `GillionsGameSyncTest` identity. The six source-qualified native
families are Hunt, Travel, FATE, Submarine, Beastmaster and Appearance.
Dashboard remains paused on this patch; retained facts are preserved. See the
[qualification evidence](https://github.com/anndrox/GillionsGameSync/blob/codex/game-sync/patch-2026-11-10/docs/native-compatibility.md#november-2026-patch-qualification).

Runtime: FFXIV `2026.11.03.0000.0000`, ClientStructs
`7.57.0.9480+80cb4a7b7e11a6702e2eb8839afd301a918c638e`, and release-channel
Dalamud `15.0.4.0+12e318c1936e449e5883faa61214eb2ced478349`.
Source qualification does not establish live behavior. Connected checks require
an existing TEST pairing and applicable consent and Site admission. If a
capability is unavailable, report it as held; do not try to force a grant.

## Owner in-game checklist

1. Use the release channel of Dalamud. Confirm the game and Dalamud versions
   above and Testing `0.0.98.0` in the installer. Disable Public and any dev
   copies before enabling this single Testing copy. Keep existing configuration.
2. **Hunt:** Open Hunt Progress with a known active bill. Compare its target and
   kill count to the in-game bill, then check after ordinary progress. Enter and
   exit a duty to confirm the pause and resume. A missing bill is not a kill or
   completion. If TEST accepts private Hunt, check that retained history survives.
3. **Travel and FATE:** Travel starts with location consent off. If TEST admits
   the capability and you opt in, manually open Teleport and compare the public
   destinations and approximate map location. Do not teleport for this check.
   Close it, revoke consent and confirm current context clears. In a public
   overworld zone use `/gillionsfates` and compare a visible FATE's name, timer
   and progress when TEST grants collection. A withheld capability is a hold.
4. **Submarine:** With access to a workshop and local retention enabled for the
   check, use the normal voyage and result windows. Compare route, submarine
   state and already-visible results; check that closing the UI does not create
   empty observations. Do not dispatch a voyage for this check.
5. **Beastmaster:** Open `/gillionsbst`. Verify sampling starts off. Enable it
   and manually open the bestiary after a fresh login; compare known owned and
   unowned pets and the total. Close the diagnostic or switch characters and
   check that old results clear. Unknown is not zero. This check stays local.
6. **Appearance:** Leave it off unless using a private TEST pairing with the
   applicable grant. With explicit per-character opt-in, compare a known look:
   base item, glamour, both dyes, both rings, a known empty slot and ordinary
   customization. Check withholding during zone transitions, logout, GPose and
   transformation. Opt out and confirm pending updates stop and reconnect
   does not restore sharing. If TEST has not granted access, leave it off.
7. **Dashboard:** If you already use local Dashboard retention, open
   `/gillionsfacts` and confirm the unsupported runtime notice, no new facts or
   export, and retained history preserved. Do not enable retention just to test
   the fence.
8. Report pass, hold or failure for each family with the three runtime versions
   and a short reproduction. Keep account and character identifiers, pairing
   details and raw private exports out of the report. If anything crashes or
   returns wrong data, disable Testing and report the last action.
