# Testing 0.0.94 — inventory burst coalescing (published)

Status: **published** as [`v0.0.94-testing`](https://github.com/anndrox/GillionsGameSync-Testing/releases/tag/v0.0.94-testing),
from source `f1866ecefbdc56b87f2aca29b567b9c95996a57b` (main CI run 40131087581 passed).

| Item | Value |
|---|---|
| ZIP | sha256 `35ca31926f542d4e29c2a67d3643801033b043b39e1d2f534ef5905aac9134da` (two package runs gave the same ZIP; the second run reused the compiled DLL, so this shows packaging determinism, not a clean recompile) |
| DLL | `GillionsGameSyncTest.dll` 0.0.94.0, sha256 `be1ae689544c69427f779d5bdad3a10faa7933440128ded8bd2893f67c7eb6bb` |
| Feed | canonical feed sha256 `348f36f54cdec35e23c1b0f8d2c33f9a3d02f51de141eef6908cc6611457554f`, distribution commit `e594d4f` |
| Upgrade | real-serializer upgrade from the published 0.0.93.0 DLL passed on this exact DLL |

The public assets and the live feed were verified anonymously. Stable (1.0.31.2)
and the earlier Testing releases are unchanged.

## Why

Measured on the owner's TEST client during heavy activity, inventory uploaded 80
times in 70 minutes:
- a median of 6.6 s apart;
- each a full upload of about 41 KB;
- each a real change, because inventory uploads were already event-driven.

The owner approved grouping them, provided the website's inventory view stays
within about 10 seconds of the game.

## What changes

`InventoryUploadSchedule`, in `ObservationMaintenance.cs`:
- An inventory change still waits the 1.5 s settle (250 ms after retainer
  listings change).
- Uploads are at least **10 s apart**.
- No change waits more than about **10 s** from the first unsent change, plus any
  upload already in progress.
- A dispatched upload refused by a loading screen or unloaded inventory is
  retried after 2 s, and a failed one after 10 s, so the change isn't left for
  the next inventory event.
- A wall clock moving backwards doesn't stall uploads.
- A session reset drops any pending schedule.

| Activity | Before | After |
|---|---|---|
| One isolated change | Upload after 1.5 s | Same |
| A change every 2 s for a minute | About 30 uploads | About 7 |
| Measured owner session (replay of the upload times) | 80 uploads | About 47 (41% fewer) |

Unchanged:
- **Sync now:** still uploads immediately.
- **Automatic rotation:** still checks inventory every 4.5 minutes and uploads
  only on change.
- **Withholding:** inventory is still withheld when its sources aren't loaded
  (never sent empty).

## Physical evidence

Covered by synthetic timing tests. Not physically tested in FFXIV.
