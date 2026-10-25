# Testing 0.0.96: character-not-linked back-off and clearer notice (published)

Status: **published** as [`v0.0.96-testing`](https://github.com/anndrox/GillionsGameSync-Testing/releases/tag/v0.0.96-testing),
from source `2828741544134a3dee407a49b23316bb9036c36a` (main CI run 40231164640 passed).
Owner authorization, given in the Game Sync chat on 2026-10-25: "publish Testing
0.0.96 (character-not-linked back-off and named chat notice) to the Testing
channel through the same verified procedure."

| Item | Value |
|---|---|
| ZIP | sha256 `08c61f23ca3b032b8314c6554761b6a2a81f99f6e33a160fb760808f85f23664` (two clean builds in separate checkouts, byte-identical) |
| DLL | `GillionsGameSyncTest.dll` 0.0.96.0, sha256 `6dd426b135a5788d118322b842acf9923b5ff0fc264522b2283b987aa68ba79e` |
| Feed | canonical feed sha256 `a040851ceb1d4a9541ef3035c2f236a5d2f532af1e9fadea0a8d1b82683deb88`, distribution commit `1b94bd0fdfa8d5807bb6725523da69321abd7235` |
| Order | release assets verified anonymously before the feed commit |
| Upgrade | real-serializer upgrade from the published 0.0.95.0 DLL passed on this exact DLL |

## Why

On 2026-10-25, Stable 1.0.31.6 on Production was played on a character that was
not linked to the account. Every request was refused with HTTP 400
`CHARACTER_NOT_LINKED`: 8 presence attempts and 141 ordinary uploads in about 17
minutes. The inventory upload was retried every 10 seconds. Site confirmed the
server behaved correctly: on the single-pairing path it refuses before storing
anything.

## What changes

PR #25 (`CharacterRefusalBackoff` in `ObservationMaintenance.cs`, wired in
`Plugin.cs`):
- After a `CHARACTER_NOT_LINKED` refusal, background ordinary uploads wait
  **5 minutes**. That covers inventory, the resource rotation, Retainer uploads
  and the Gil flush. An hour of refusals now makes at most 12 attempts. A
  refusal of any sync, including Sync now, starts the wait for the background
  uploads. Hunt, submarine, travel and Market uploads have their own schedules
  and are not part of this gate.
- **Clears the wait:** a character change, logging in or out, or an accepted
  upload.
- **Does not clear it:** an accepted presence, which proves only the paired
  character, not the account link.
- **Keeps running:** local capture, so nothing is lost; changes upload once the
  character is linked. Sync now, and the one-time hydration sync, are not held
  back. Presence keeps its own back-off.
- **Unchanged:** other refusals keep their existing handling. That includes
  "paired to a different character", which concerns presence and Retainer
  uploads.

The one-time chat notice (local chat only) now names the character and world,
falling back to "This character" before the character is known. It says to link
this character on Gillions or play a linked one.

## Unchanged

- No setting, consent default or data category changes.
- No frozen transport file changed (`tests/public-candidate-contract-test.py`).
- Testing still pairs only with https://test.gillions.app.

## Verification

- `CharacterRefusalBackoffTests`: 8 checks.
- `tests/character-refusal-contract-test.py` (run by `verify.ps1`) checks the
  wiring. Every capture happens before the gate and every upload after it,
  presence never touches the wait, and only a refusal on the current permit arms
  it. A mutation that makes presence reset the wait fails it.
- Full `scripts/verify.ps1` passes with warnings as errors (Dalamud 15.0.3.9).
- Independent review: one blocker (presence cleared the wait), fixed and
  re-reviewed clean.

## Physical evidence

Covered by synthetic timing and wiring tests. Not physically tested in FFXIV.
