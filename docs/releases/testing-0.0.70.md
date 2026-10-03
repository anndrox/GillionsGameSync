# Testing 0.0.70 — readable Hunt targets

Successor to 0.0.69. The owner demonstrated the drop/reacquire Key Items gate:
dropped bills yielded no corroborated observation with retained state preserved;
reacquired bills yielded two matching caches and zero partial reads. That is
bounded runtime evidence for admission, not current order/counter correctness.
The owner then reported that numeric target IDs were not usable for comparison.

The Hunt panel now shows one wrapped line per retained target: localized monster
name from BNpcName.Singular, observed/required kills and numeric target/NPC-name
IDs. Missing/failed names explicitly say "Target name unavailable"; no name is
guessed. Labels use the current client's game catalog language. Static labels
are resolved during view publication, not on every rendered frame, cached to
512 entries (including missing labels); excess labels resolve without growth or
eviction. Text controls are removed and labels bounded to 160 characters.

This is display-only. Names are not added to retention, PRIVATE exports or
aggregate diagnostics. No schema/contract, native source/admission, five-second
cadence, consent, retention, uploader, credential or server change. Party Finder,
Beastmaster, submarines, market contribution and ordinary sync are preserved.
Existing independent privacy acceptance carries forward for unchanged surfaces;
static NPC labels introduce no material changed privacy/trust/custody risk.
Published predecessors, Stable/main and Site/server remain untouched.

Keep the existing Dalamud repository URL:

`https://github.com/anndrox/GillionsGameSync/releases/download/v0.0.64-testing/GillionsGameSyncTesting.json`

## Owner checks

1. Refresh/update normally and confirm 0.0.70.0. Do not change the repository URL,
   delete configuration, log out or re-pair just to obtain Hunt observations.
2. Enable `/gillionshunts` retention with accepted bills held; wait ten seconds.
   Each target should show a monster name and its kill count on a separate line.
3. Compare names, requirements and counts with actual bill details. Kill one
   required target; wait 5–10 seconds without reopening a bill and check the
   matching named row increments. Current cache ownership/freshness is unverified.
4. Disable retention; attempts stop. Re-enable; collection resumes. Retained
   observations are historical, not proof of current acceptance.
5. Send a Hunt-panel screenshot and aggregate diagnostics, plus action/time and
   any hitch. No config, credentials or private export needed for this check.

Build/fixture tests do not prove installed game name rendering or target/counter
correctness. No Hunt/submarine upload exists; the existing bounded Site intake
handoff is still required before joint server testing.

## Publication evidence

Published October 2, 2026, 20:26:57 New York time (October 3, 00:26:57 UTC),
Testing source `f3378b7909882a0f3415cfd25c779c01ec76d603`, tag `v0.0.70-testing`.
Full verification passed: 287 Hunt, 539 submarine, 268 prior regression and 224
market checks, PF/Beastmaster/performance and actual build/serializer checks.
The final packaged Testing DLL also passed the actual configuration suite.

ZIP SHA-256: `d3329ad04e3787b07067d71d712648424bdeca3c9ca4d17807df4095ce5fb8bd`.
The existing v0.0.64 JSON URL anonymously serves 0.0.70.0 with successor ZIP
links; embedded identity and artifact hashes verified. See
[durable release evidence](../../data/releases/testing-0.0.70.json) and
[fixed-feed evidence](../../data/releases/testing-update-feed.json).
