# Testing 0.0.94 — inventory burst coalescing

Status: **release candidate.** Its publication state is recorded separately once
the Testing release and feed are verified.

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
