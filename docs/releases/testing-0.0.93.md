# Testing 0.0.93 — quieter Retainer uploads

Status: **release candidate.** Its publication state is recorded separately once
the Testing release and feed are verified.

## Why

On TEST, the owner's 0.0.92 client uploaded `retainer_ventures` about every 30
seconds: 258 uploads in about 2.5 hours, each about 40 KB, each stored as a new
batch (HTTP 201), with no retainer change.

Every roster read refreshes per-retainer `lastObservedAtUtc` and similar
observation times. So the payload hash never matched the last upload, and every
automatic cadence re-sent it.

## What changes

| Situation | Upload |
|---|---|
| Content changed (roster, venture, gear, gil, inventory coverage) | Immediately, at the next 30-second check or the 250 ms change trigger |
| Venture results pending acknowledgement | Always |
| Sync now | Always |
| Nothing changed | Only a freshness refresh, at most every 5 minutes |
| Plugin restart or character change | One refresh |

- **Content hash:** the client compares a content hash of the payload with only the
  observation-time fields removed (`lastObservedAtUtc`, `observedAtUtc`).
  `lastChangedAtUtc` moves only with a value change, so it stays in the hash.
- **Payload unchanged:** the payload shape itself is unchanged.
- **Persistence:** the content hash is stored alongside the existing per-character
  payload hashes, and the last upload time is kept in memory.

An unchanged hour now uploads 12 times instead of 120.

## Site agreement

Site Engineering (Site source e39ea10c) confirmed:
- Site has no time-based staleness rule for this data, and displayed ages are
  informational.
- Connected/Offline and plan readiness come from presence, not from this upload.
- The scope upserts are guarded by monotonic timestamps.

No Site change is needed.

## Physical evidence

Covered by synthetic tests on the real payload builder. Not physically tested in
FFXIV.
