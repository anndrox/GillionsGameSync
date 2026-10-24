# Testing 0.0.93 — quieter Retainer uploads (published)

Status: **published** as [`v0.0.93-testing`](https://github.com/anndrox/GillionsGameSync-Testing/releases/tag/v0.0.93-testing),
from source `032544a132aa2bc20fa3cc1f0fab17bb3e3d40ab` (main CI run 40126674446 passed).

| Item | Value |
|---|---|
| ZIP | sha256 `693b29555e1698c72ee128578eaeaf3e529c66ffe0e283da81b5f416aca019e8` (two builds byte-identical) |
| DLL | `GillionsGameSyncTest.dll` 0.0.93.0, sha256 `92706175a76ed3686393a3bab63beecb5efa76d7b8c22ede013c4aba6c940f14` |
| Feed | canonical feed sha256 `7ac76fd4e6c90969678aa645a49f99ff052b0831bf885f78462b1340838c7448`, distribution commit `276b802` |
| Upgrade | real-serializer upgrade from the published 0.0.92.0 DLL passed on this exact DLL |

The public assets and the live feed were verified anonymously. Stable (1.0.31.2)
and the earlier Testing releases are unchanged.

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
  evidence-time fields removed (`lastObservedAtUtc`, `observedAtUtc`,
  `lastChangedAtUtc`). Loaded inventory containers restamp `lastChangedAtUtc` on
  every read, and a real change always also changes a value, status or count. The
  5-minute refresh carries current times.
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
