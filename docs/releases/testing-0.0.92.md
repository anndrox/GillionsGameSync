# Testing 0.0.92 — Gillions refusals named, character notices (published)

Status: **published** as [`v0.0.92-testing`](https://github.com/anndrox/GillionsGameSync-Testing/releases/tag/v0.0.92-testing),
from source `ac5920e6b72ec7d016fe2680bd54f890485176da` (main CI run 40091875774 passed).

| Item | Value |
|---|---|
| ZIP | sha256 `039e4bc8c3fdeab8f542bfc1a7234c76b1ef37d8ab8f79cc1a4a0786bcfd70f7` (two builds byte-identical) |
| DLL | `GillionsGameSyncTest.dll` 0.0.92.0, sha256 `cf5e1e3e0742340fda6a8ebe164f59c65e914f0a7821e3761c1405083b743085` |
| Feed | canonical feed sha256 `425c1276277a4c3f3cde4798f25c5ae2e7b144853ef07c3596ecf972bf289de8`, distribution commit `fd05b7e` |
| Upgrade | real-serializer upgrade from the published 0.0.91.0 DLL passed on this exact DLL |

The public assets and the live feed were verified anonymously. Stable (1.0.31.2),
its feed and the earlier Testing releases are unchanged.

## Why

The owner ran Testing 0.0.91 against Gillions TEST. It reported "Gillions
unavailable" while TEST was up and refusing requests. Site Engineering confirmed
two causes from the TEST database:
- **Uploads:** `/sync` returned 400 because the character being played (a newly
  played alt) was not linked to the TEST account. Adding it in Preferences fixed
  the uploads.
- **Presence:** returned 403 `character_mismatch`. The device was paired while a
  different character was active, and presence is bound to the paired character.
  Re-pairing while playing the character fixes it.

Neither was an outage, and the old label hid the fix from the player.

## What changes

| Gillions answer | Connection shows | In-game notice |
|---|---|---|
| character not linked (`CHARACTER_NOT_LINKED`, `character_not_found`, or the deployed exact message) | Character not linked on Gillions | Once per character and session: add it in Preferences, then Sync now |
| `character_mismatch` | Paired to a different character (with Reconnect) | Once per character and session: pair again while playing it |
| product identity mismatch (`CLIENT_PRODUCT_MISMATCH` or the deployed exact message) | Reconnect required (with Reconnect) | — |
| any other 4xx except 408/409/429 | Refused by Gillions | — |
| no response, 408, 409 (a single resource's state conflict), 429, 5xx | Gillions unavailable (retried) | — |

- Site text is used only to classify a refusal. It is never displayed or logged;
  local diagnostics record only the bounded refusal code.
- A successful upload no longer clears a presence-only refusal, and a successful
  presence no longer clears "character not linked". Character refusals are
  cleared on a character change or login, and "not linked" wins over "paired to
  a different character" for the same character, because re-pairing cannot fix it.
- Hydration is not retried after a refusal, because it would repeat.
- **Windows:** finishing a reconnect started from Settings returns to Settings. Only
  first-run onboarding opens the main window, so Reconnect → Finish no longer
  stacks windows.

Unchanged: everything in 0.0.91. That includes the frozen transports, consent,
payloads and native compatibility.

## Physical evidence

The owner reproduced the original behaviour on 0.0.91. The 0.0.92 corrections are
source-qualified and covered by synthetic tests; they were not physically tested
in FFXIV.
