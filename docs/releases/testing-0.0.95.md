# Testing 0.0.95 — travel refusal back-off

Status: **release candidate.** Its publication state is recorded separately once
the Testing release and feed are verified.

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
