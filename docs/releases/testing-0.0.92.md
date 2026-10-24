# Testing 0.0.92 — Gillions refusals named, character notices

Status: **release candidate.** Its publication state is recorded separately once
the Testing release and feed are verified.

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
| any other 4xx except 408/429 | Refused by Gillions | — |
| no response, 408, 429, 5xx | Gillions unavailable (retried) | — |

- Site text is used only to classify a refusal. It is never displayed or logged;
  local diagnostics record only the bounded refusal code.
- A successful upload no longer clears a presence-only refusal, and a successful
  presence no longer clears "character not linked".
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
