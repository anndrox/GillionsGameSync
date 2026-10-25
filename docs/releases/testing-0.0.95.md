# Testing 0.0.95 — travel refusal back-off (published)

Status: **published** as [`v0.0.95-testing`](https://github.com/anndrox/GillionsGameSync-Testing/releases/tag/v0.0.95-testing),
from source `2630eaa0da547ba3612be3b78a870bba819cbafc` (main CI run 40177524649 passed).

| Item | Value |
|---|---|
| ZIP | sha256 `17e4b2eb2ded779a9d7a48c75f2c7ff52b869f5df504e68a6e577fa65201e9ee` (single clean build) |
| DLL | `GillionsGameSyncTest.dll` 0.0.95.0, sha256 `86811ce6455c38cd07e11755728a16691fc06ec5059f65a776984fa41e2fa652` |
| Feed | canonical feed sha256 `0e5095313ea5f636228d50d59dfc37677fc495bfd9b03a998405a78635618d9a` |
| Upgrade | real-serializer upgrade from the published 0.0.94.0 DLL passed on this exact DLL |

## Why

Site Engineering raised it during the Stable 1.0.31.6 admission review. Once a
client version is admitted, Gillions offers travel even when it can't yet accept
uploads for the client's game build. Each travel upload is then refused (400),
and the refusals count against the player's shared private-upload budget of 30 a
minute.

The client ended each attempt on a refusal, but retried after the next 15-second
presence acknowledgement. That was up to 4 refused uploads a minute while travel
was on.

## What changes

`TravelRefusalBackoff`, in `ObservationMaintenance.cs`, used by the travel
scheduler in `Plugin.cs`:
- After a refused (terminal) travel upload, Game Sync waits **5 minutes** before
  the next travel attempt.
- An hour of refusals now makes 12 attempts instead of 240.
- A character change, login, or a real change of the travel decision on Gillions
  clears the wait, so a fix takes effect at once. A brief presence failure does
  not clear it.
- Timeouts and redirects (408, 425, 307, 308) keep the usual short retry.
- A wall clock moving backwards never extends the wait.

Unchanged:
- Travel is off by default.
- The travel transport (`TravelSync.cs`) is frozen and untouched.
- Hunt coverage was already quiet: a refused coverage snapshot is re-sent only
  when the Hunt item states, the zone or the session change.

## Physical evidence

Covered by synthetic timing tests. Not physically tested in FFXIV.
