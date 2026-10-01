# Gillions Game Sync Testing 0.0.67 — prepared market candidate

Status: source/package candidate, not a published release or installed-game proof.
It continues the existing 0.0.66 branch ancestry and preserves Party Finder,
Beastmaster, performance additions and read-only submarine retention. Stable,
published 0.0.66 immutable assets and server deployments are unchanged.

Future manual JSON **only after publication is confirmed**:

`https://github.com/anndrox/GillionsGameSync/releases/download/v0.0.67-testing/GillionsGameSyncTesting.json`

Until then keep the published 0.0.66 JSON. Do not downgrade/delete configuration,
replace existing immutable assets, install a competing plugin identity or enable
production traffic as validation. The [contract and bounded Site handoff](../contracts/market-observations-v1.md)
defines the required authenticated receiver; current Site has no such endpoint.

## Changes and consent

"Contribute observed market data to Gillions" is **ON by default**, including
older Testing configurations without this choice. OFF persists, stops new market
sends and clears/cancels transient work without affecting ordinary sync/PF.
No website needs to be open. No market searches, crawler, Universalis upload or
Dalamud preference access. Existing HTTPS pairing is authentication, not anonymous
transport; market payloads exclude player/buyer/retainer/account identities.

Public receive events copy only partial listings/recent sales. World is guarded
current-world context, not a response field. Empty/unavailable data cannot mean
zero listings. Listing creation/review time and source snapshot time are unavailable;
source purchase times remain distinct from client observation/server receipt.

## Focused / isolated acceptance checklist

1. Candidate must show 0.0.67.0 and the existing Testing identity. Preserve config
   and previous PF/Beastmaster/submarine choices/history. Confirm new market choice
   ON; disable/save/reload and confirm OFF. Ordinary manual sync/PF still works.
2. Before a compatible isolated server acknowledges v1, naturally view market
   data. Status must remain waiting; no market endpoint requests occur. Ordinary
   existing presence/sync is unchanged, with only the optional capability header.
3. Use only an approved disposable HTTPS origin with the exact existing pairing
   contract and v1 acknowledgment/intake. Do not use production contribution traffic
   or bypass TLS/authentication. Site separately owns environment implementation.
4. Naturally view an item. Compare numeric listing IDs/prices/quantities/HQ/city;
   mark listing packet partial, not the whole market. View history and compare
   actual sold times/raw sale prices. Check world visit/current versus home world.
5. Verify receiver timestamps are server-generated and retries keep original client
   times/IDs. Repeat packets safely, including same-price rows; no fabricated empty
   market or duplicate historical trades. Switch world/character/re-pair during a
   delayed upload; old work must cancel/drop and never change the new session.
6. Turn market contribution OFF during an upload and continue normal market-board
   use. New sends stop, queued work clears, no gameplay action or hitch occurs.
   Dalamud's own uploader/preference remains untouched.
7. Exercise synthetic 400/401/403/404/redirect/429/5xx, malformed/oversized receipts,
   timeout and unavailable network. No gameplay/UI disruption; bounded retries and
   persistent enrollment stop, not credential logging. Use fixtures, not real bad data.
8. Recheck ordinary sync, PF opt-in/permission, local Beastmaster and submarine
   retention. Compare frame time under identical ordinary browsing/settings conditions.

Diagnostics report fixed aggregate summaries only. Use the existing diagnostics
copy control with version/actions/status and any hitch; never share full plugin
configuration, bearer, pairing code or raw buyer/retainer names. Managed public-
interface/configuration tests do not establish live event threads, packet/world
freshness, FPS, server ingestion or cache ordering correctness.
