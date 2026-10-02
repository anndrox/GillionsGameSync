# Gillions Game Sync Testing 0.0.68 — local Hunts / private submarines

Testing only, separate `GillionsGameSyncTest` product, Dalamud API 15.
No Stable, Site, server, infrastructure, market runtime or Wardrobe changes.
Published 0.0.66/0.0.67 remain unchanged. See [exact draft and Site handoff](../contracts/personal-observations-v1.md).
Source/hash/publication evidence: [Testing 0.0.68 release record](../../data/releases/testing-0.0.68.json).
Preparation, public availability and live-game success are distinct.

Published manual Dalamud repository:

`https://github.com/anndrox/GillionsGameSync/releases/download/v0.0.68-testing/GillionsGameSyncTesting.json`

Replace earlier manual Testing entry, not Stable. Preserve configuration; do not
downgrade. Disable Stable while comparing ordinary sync to avoid duplicates;
this release does not automatically change it.

## Flags

- Hunt local retention: OFF default, independent of Automatic sync/pairing.
- Submarine local retention: OFF default; prior saved choice survives.
- Community voyage preparation: separate OFF default; no uploads. PRIVATE copies
  require explicit prepare/copy and never grant community consent.
- Beastmaster: unchanged local sampling OFF after load.
- Party Finder: unchanged site permission and separate local opt-in, OFF default.
- Market: unchanged ON default, saved OFF/denial and authenticated compatibility
  gate; disable before browsing if you do not intend contribution.
- Ordinary Automatic sync and item-link defaults/HTTPS binding: unchanged.

Hunt/submarine native reads require game `2026.09.15.0000.0000` plus installed
FFXIVClientStructs `7.56.2.9136`; other pairs stop reads/preserve history.
Matching this inspected pair is not live compatibility proof. Pairing is not
needed for these local tests. Plaintext TEST IP pairing remains rejected.

## One live checklist

1. Confirm Testing `0.0.68.0`; start with new retention OFF. Ordinary settings
   still open. Market OFF prevents new Gillions market sends while ordinary sync
   remains independent. Use only an already-authorized HTTPS pairing; this task
   does not authorize production test writes.
2. `/gillionshunts`, enable local retention, naturally open an accepted daily or
   weekly Hunt Bill. Compare category/order/each target ID and counters. Missing
   data must say unknown/preserved, never no bills. No initial logout/login needed.
3. If feasible, defeat one target normally and reopen its bill; compare increment/
   completion. Accepting a changed bill should replace that category's latest
   positive state. A real reset requires a later observation; no reset inference.
   Optional second-character check must not display/export the first's partition.
   Cached ownership/current acceptance remain explicitly unverified.
4. FC-access tester only: `/gillionssubs`, enable local retention; enter workshop,
   open Voyage Control Panel normally and inspect submarine/build. Compare name,
   rank, four parts, base/bonus stats, EXP, ordered route and expected return.
   Naturally view route planning for positive unlocked/explored evidence, not a
   complete unlock set. Do not dispatch/recall/repair/claim solely for testing.
   Normally returned results can test existing sector/voyage reward retention.
5. Close UIs/leave workshop/reload plugin normally. Prior observations/results
   persist labelled retained; unloaded slots, zero return and missing flags never
   become empty/idle/all locked. Reopen UI to refresh. Repeated results must not
   count another voyage; later builds never substitute the producing build.
6. Disable local retention: no corresponding reads/private export; history stays.
   Community export remains independent and sanitized (no private names/scope).
   Unsupported version must stop reads rather than clear data.
7. Recheck PF using its existing allowed HTTPS setup, Beastmaster vs bestiary,
   and ordinary inventory/currency sync. Watch read/save milliseconds/menu hitches.
   No Hunt/submarine uploader or new Universalis path exists.

## Diagnostics

Use local windows' **Copy aggregate ... diagnostics**. Include plugin/game/
Dalamud/SDK versions, interface actions, daily/weekly or FC access availability,
expected vs observed behavior and milliseconds. Aggregate copies exclude names,
IDs, counters, routes, secrets and raw exceptions. Redact ordinary sync reports.
Never send full config, pairing code, bearer, raw memory or secret screenshots.

PRIVATE JSON is optional only when required for diagnosis and you may share it
with a trusted recipient. It contains activity; submarine copies include names/
scope. Prefer aggregates first. Clipboard copying isn't uploading or community
consent. No live game session was available during preparation.

## Capability audit

Starting source `9821c92df8f51c9fef913a9e8294fd6ed3b4469c`, branch
`codex/game-sync/beastmaster-party-finder-testing`. Authoritative clone main:
`35d51ee3e362756a0d8e0962d3c3e3b009a72541` (Forgejo); public main:
`ec3fcdb1dca5642f24aefc25e66e7877b4de9a46`. Preserve both; only task branch
and successor Testing tag/assets are targets. Existing worktree reused.

| Layer | Hunts: predecessor / successor | Submarines: predecessor / successor |
| --- | --- | --- |
| A collector | MISSING / IMPLEMENTED BUT UNPROVEN natively | IMPLEMENTED BUT UNPROVEN / extended provisional personal state |
| B builds | MISSING / IMPLEMENTED + PROVEN SDK build | IMPLEMENTED + PROVEN SDK builds |
| C enabled Testing | MISSING / IMPLEMENTED + PROVEN opt-in, OFF default | IMPLEMENTED + PROVEN opt-in, OFF default |
| D payload | MISSING / PARTIAL: versioned local draft | PARTIAL: local community + new PRIVATE draft, no activated HTTP |
| E server accepts | MISSING | MISSING |
| F server persists | MISSING; plugin local persistence proven separately | MISSING; plugin local persistence proven separately |
| G personal Web/API | MISSING; synthetic/reference preview exists | MISSING; synthetic/reference preview exists |
| H published | MISSING in 0.0.67 / IMPLEMENTED + PROVEN package presence in 0.0.68 | IMPLEMENTED + PROVEN local reader in 0.0.67 / private support package presence in 0.0.68 |
| I live validation | IMPLEMENTED BUT UNPROVEN | IMPLEMENTED BUT UNPROVEN; requires eligible FC tester |

Departure/verified bill reset timing are UNSUPPORTED BY CURRENT GAME STATE
evidence available to these readers. Static/build/fixture evidence cannot prove
cache ownership, complete unlock readiness, FC permission, live frame costs or
filesystem durability on a player's machine. Tests and final hash accompany the
release evidence. Integrated end state: **BLOCKED — SITE/SERVER HANDOFF REQUIRED**;
local experiments ready for the checklist, not synchronized preview replacement.
