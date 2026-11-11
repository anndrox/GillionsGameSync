# Testing 0.0.99.0

Game Sync Settings shows the sharing choices for your character. Use **Change
on gillions.app** to make a change. Appearance reads both dyes from the gear
your character is showing. If you share your location for Hunt routing,
Gillions also receives your current world.

Hunt routing gets a small coverage update every 10 seconds. A bill update is
sent when your progress changes, with a full bill check every 10 minutes.
Game Sync saves settings in the background, spreads larger reads across game
updates, and waits before retrying an upload Gillions has paused.

## Owner checks on TEST

Use one enabled copy of Testing and the same character, frame cap, and plugin
settings as the 0.0.98.0 baseline. Compare Framework and Draw separately in
`/xlstats` during five minutes in town, five minutes of Hunt or FATE play, and
a short retainer bell session. Check login, zone changes, Sync now, and a Hunt
kill. Report aggregate times and request sizes.

- In Settings, compare every sharing row with Gillions. Check a new pairing
  shows Party Finder off. Toggle appearance on Gillions and confirm the plugin
  stops sending the look as soon as it receives Off.
- Check both dyes on the head, body, hands and feet, and no dyes on the legs,
  weapons or accessories. Compare the underlying item with the shown glamour.
- Turn on routing location and check a world visit follows the current world.
  Turn routing off and check that the location report clears.
- Check Hunt Progress in a zone with no targets and open a Hunt map from
  Gillions. The map message describes the usual area or a possible location.
- Use the [0.0.98 checklist](testing-0.0.98.md) for the other native families.
  The November runtime keeps the same family qualifications.

The TEST site must accept the draft 3 travel field and Testing build before the
owner check. The package and the Testing feed advance after the separate release
go-ahead.
