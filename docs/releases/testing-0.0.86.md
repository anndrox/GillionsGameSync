# Testing 0.0.86 authenticated FATE observations

Published October 5, 2026 Eastern / October 6 UTC after exact-package reviews and
real FFXIV-to-shared-TEST acceptance. The same reviewed bytes were published
without rebuilding. Keep the existing rolling Testing URL:

https://github.com/anndrox/GillionsGameSync/releases/download/v0.0.64-testing/GillionsGameSyncTesting.json

Disable the diagnostic dev-plugin copy before enabling installed Testing86;
never load both. Preserve configuration and pairing. No reset, re-pairing or new
repository entry is needed. An initial GitHub CDN cache hit retained85; both a
cache-revalidated request and a later ordinary GET resolved the exact reviewed86.

## Contract and sender

Use `/gillionsfates` to start the temporary Advanced/Testing measurement session.
It starts OFF, stops on reload and retains bounded RAM only. Site's independent
`fate_public_observations` revision1 policy is OFF by default and must be explicitly
ON for the current authenticated Testing device. The local session grants no
sharing permission; there is no duplicate permanent local FATE contribution toggle.

Shared TEST Site `64d0d8ee8e105d2cad828606849e89d284e89ab0`, schema0027, admits the
actual86 device/source tuple through exact revision3 discovery. Contract and
collector schema are `fate-live-observations-v1`, wire1, positive_only. Endpoint:
POST https://test.gillions.app/api/game-sync/fates/contribute. Existing paired
Bearer authentication, trusted TLS, exact HTTPS origin and no redirects/fallback
are preserved. Contributor custody is private; public content is de-identified,
not anonymous transport.

Accepted85 collection remains unchanged at five-second reads. Discovery has a
ten-second floor and contribution a five-second floor; unchanged positive evidence
renews at ten seconds. Semantic changes prepare new UUIDs. Batches contain1..64
rows, at most128KiB. Preparation plus one immutable in-flight/retry body stays
RAM-only and bounded. Grants and unsent batches expire30seconds; no offline journal.

Retries keep UUID/body/original time; changed payload needs a new UUID. Exact receipts
bind identity/counts. HTTP disposition and Retry-After survive malformed/stalled
error bodies and delayed callbacks. Interrupted200/201 streams retry immutable
bytes; complete malformed success receipts fail closed. Bounded backoff honors
Retry-After. Policy/session/context/revoke invalidation cancels FATE only; renewed
admission requires fresh observations. No new gameplay reads or private collection.

See the [exact capability](../contracts/fate-live-observations-v1.md) for the full
source matrix, schema, occurrence, receipt and privacy model.

## Real game acceptance

The owner loaded the exact reviewed86 DLL. Seven real FATEs arrived from Cactuar
(CurrentWorld79), Central Shroud (territory148), public instance1:
127,131,136,137,140,209,604. Six were Running;136 Preparing retained unknown timing
and provisional identity. Spirithold Run140 had31% progress; bonusfalse.

Native acknowledged seven accepted rows with a59.99ms Stopwatch request. The exact
3146-byte sent-body SHA256 matched the actual server receipt:
`9bcf8025bb9b3317ede862d7ffd37549e3a4a0814f3bd2a681ccc3b2959ec403`.
Its six envelope keys and allowed source/public event fields contained no character,
account, device, ContentID, HomeWorld, player position, party/FC, inventory, quests,
private Hunt/travel or credentials. FATE geometry is event geometry, not player
position. No private binding identifiers or credentials are retained in public notes.

Successful renewals arrived roughly10seconds apart. Read-only storage counted
seven slots after many renewals, not duplicate occurrences. Lethe on My Mind131
changed0->16% on unchanged world79/territory148/instance1/start1791249402; the API
and actual detail page showed fresh16% progress, correct scope and derived timing.

Spirithold Run naturally ceased positive observation. Its actual page became stale,
preserved31% and explicitly said elapsed timing was not observed completion. After
120seconds it rendered No recent Gillions observations; read-only persistence
confirmed cleanup. It never became inactive/completed because of absence. No fake
clock, database mutation, requested FATE completion or zone change was used.

While sender renewals were active, the owner confirmed one Megamaguey Show in FFXIV
was prompt (around1–2seconds), without a hitch or unexplained repeat. This is bounded
owner-live sanity evidence, not a numerical command-latency benchmark.

## Performance and validation

Twenty samples: read median 0.048 ms, p95 2.336 ms, maximum 2.479 ms; preparation
median 0.091 ms, p95 2.552 ms, maximum 14.726 ms. Combined median 0.14305 ms,
maximum 17.2046 ms; final 12 samples combined median 0.1247 ms, maximum 0.2307 ms.
Startup cost remains included. These are
read/copy/preparation stages and current-thread counters, not frame/process totals.
Native and server clocks differed about2.1seconds; cross-clock subtraction is not
a request-latency measurement.

Maintained verification passed8411 counted managed assertions:603 FATE,799 Hunt-map,
542 Submarine,1516 personal,1792 Dashboard,2551 travel,224 Market,116 PF and268
orchestration, plus ordinary/Beastmaster/config/source/SDK/manifest/performance checks.
Both validation builds had zero warnings/errors;12 actual serializer fixtures per
product and12 Stable FATE-exclusion checks passed. Exact final DLL validation passed
403 FATE checks plus inherited HuntV2/focus/clocks/consume/config/resource/privacy.

Independent Security/Privacy and Compatibility accepted the exact source/tree/package
with no remaining findings. No artifact rebuild followed reviews or live acceptance.
Publication-only metadata validation is separate from these tests and live proof.

## Published identities

Starting published85 source: `39f7a71a9d1d7d4f17068aff50c9d516aa9884fd`.
Starting task record: `334579fe96d66386db44b7e1a3524e4d25849e4c`.
Artifact source: `f16884294cb45e23a233fc6cfadce2fb6c0b03df`.
Tree: `67a9caff8542e68699cbeec0e77c082a782ac0ee`.
Branch: `codex/game-sync/beastmaster-party-finder-testing`.
Annotated tag: `v0.0.86-testing`, `a0e5b304f58edaa19ab770637111d7cc56351a4f`.
Release: [Testing86](https://github.com/anndrox/GillionsGameSync/releases/tag/v0.0.86-testing).

- ZIP: `624f0b9c3ccaf2bc9ba2816fd2ae01270f0c6b36a416017ec227112ecd980760`
- DLL: `561112e5760d2f9213bc763ec745589ab12090a127f85d322a0b00bcd9660fb5`
- Manifest: `5df4c24c45ee290a361ec878759678e40e5c2f39bbdb7f4212d4d087eedfba51`
- Embedded manifest: `3e3c8bb6b3d2546d67a28ee622d8b295b5b55361d9d1a38b4326cd245e039506`

Anonymous immutable JSON/ZIP/contained DLL/embedded manifest match reviewed bytes.
Only mutable rolling64 JSON advanced;34 prior releases/57 immutable assets,
including85, are unchanged. The [publication record](../../data/releases/testing-0.0.86.json)
binds asset IDs, checks and limits. Later publication metadata does not change artifact source.

## Site handoff and boundaries

Real live FATE crowdsourcing is proven on shared TEST. Tracked FATEs, toast/tray/
Notification Center alerts and optional sound remain separate Site work consuming
this occurrence/freshness model. Preserve provisional/unknown/ambiguous/expiry
semantics; missing observations and estimated timers never establish completion.

For PF attribution use the same clickable wording: **Data provided by xivpf.com**.
No technical PF/contribution contract change is required.

Hunt80 completion,82 commands,84 coverage margin, V1/V2 maps, PF/item links,
Beastmaster, Market, Submarines, Dashboard/Custom Deliveries, travel privacy,
configuration/retention and ordinary sync remain preserved. Stable1.0.30, both main
branches, production, Site source/schema, Market architecture and Wardrobe are
untouched. No Native gameplay writes, new credentials, TLS bypass, default-ON
sharing, alerts, Tracked FATEs or static Site UI redesign.

REAL LIVE FATE CROWDSOURCING PROVEN ON SHARED TEST — READY FOR TRACKED FATES AND ALERTS
