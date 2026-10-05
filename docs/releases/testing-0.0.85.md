# Testing 0.0.85 local FATE observations

Status: published and anonymously verified October 5, 2026. The unchanged rolling
Testing URL resolves 0.0.85.0. All 33 prior releases and 55 immutable assets remain
unchanged; only the explicitly mutable rolling64 JSON advanced. The exact accepted
artifact was published without rebuilding.

Testing85 supplies a temporary Advanced/Testing read-only local FATE session,
not active contribution. Use /gillionsfates and explicitly start measurement.
The session starts OFF, stops on reload, keeps bounded RAM only and grants no
sharing permission. There is no persistent FATE toggle or durable FATE history.

## Sources and collector behavior

Maintained Dalamud IFateTable/IFate and typed client/player/condition APIs supply
facts; compatible Lumina World/TerritoryType/Fate catalogs supply validation.
Custom Gillions logic covers settled-context admission, positive-only semantics,
RAM limits, version fencing, serialization and diagnostic measurements. No new
low-level parser, pointer retention, actor scans, UI scraping or game requests.

Reads occur independently every five monotonic seconds. Two scheduled stable
context checks precede first admission. CurrentWorld, compatible ordinary public
Overworld territory and typed instance must agree before and after enumeration.
Duties, PvP, special exploratory modes, group pose, zoning and world-visit/logout
transitions fail closed. Context/account/session changes discard unsent epochs.

The first context now binds its already-settled epoch without invalidating it
again. The original diagnostic's alternating settlement defect was reproduced
and corrected; the added exact-DLL assertion fails against the preserved old
candidate and passes against the published DLL.

Required direct scalars are FATE/world/territory IDs, instance, recognized typed
state and collector UTC time. Optional progress/bonus, ordered levels, event
geometry/radius and valid start/duration retain their limitations. Preparing may
have unknown timing. Missing data remains unknown: no empty replacement, negative
coverage, disappearance/progress100/timer completion or player credit inference.

At most 64 table entries/rows, one immutable prepared batch of 128 KiB and 240
RAM cost samples are supported. Semantic changes prepare a new UUID/body;
unchanged positive evidence renews after ten seconds. Unsent data expires after
30 seconds; retries never refresh time. Same-epoch timed-occurrence support is
bounded to 128 entries/120 seconds. No-start evidence remains provisional and
separate occurrences cannot inherit progress or terminal state.

See the [exact Native capability](../contracts/fate-live-observations-v1.md) for
all field, occurrence, source, consent, receipt and error semantics.

## Real runtime acceptance

The corrected client run contains one settlement check followed by 14 consecutive
positive reads, each with three rows. Direct Preparing with unknown timing and
Running with valid timing, progress/bonus, ordered levels and event geometry are
observed. There is no repeated settling.

| Population | Read median / p95 / max ms | Preparation median / p95 / max ms | Combined median / p95 / max ms |
| --- | --- | --- | --- |
| All 15 samples | 0.0176 / 1.7968 / 1.7968 | 0.0755 / 10.3777 / 10.3777 | 0.1313 / 11.5156 / 11.5156 |
| Final 12 positive samples | 0.01585 / 0.1164 / 0.1164 | 0.0748 / 0.1317 / 0.1317 | 0.1112 / 0.1919 / 0.1919 |

P95 uses nearest rank; small populations make p95 equal maximum. The first positive
sample costs 11.5156 ms combined with 335824 preparation bytes. Startup cost remains
included. Cold JIT/JSON initialization is a hypothesis, not proven attribution.
Final12 read allocations are 1152 bytes/sample; preparation median 4776/max 6816.

These measure read/copy and preparation stages/current-thread allocations, not
whole-frame behavior. Individual sample timestamps are not retained, so exact
inter-read wall gaps cannot be reconstructed. No active upload/Site freshness
margin is claimed. Corrected world/instance/logout/terminal transitions are
fixture-validated, not all individually observed live.

While FATE measurement was running, the owner confirmed prompt PF chat-link
delivery without a noticeable hitch or duplicate. The concurrent Native log
also contains valid Hunt receipts, accepted travel context and ordinary Retainer
uploads. This is bounded observational responsiveness acceptance, not numeric
click-to-delivery latency; Hunt observation-to-dispatch timings are not command
timings. Other unchanged capabilities retain maintained regression validation.

## Validation and reviews

The maintained suite passes 8223 counted managed assertions: 415 FATE, 799 Hunt-map,
542 Submarine, 1516 personal, 1792 Dashboard, 2551 travel, 224 Market, 116 PF and 268
orchestration. Additional ordinary/Beastmaster/source/SDK/manifest/performance
checks pass. Stable and Testing builds have zero warnings/errors, with 12 actual
configuration load/save/reload fixtures per product and nine Stable FATE-exclusion
checks. Exact packaged-DLL validation includes 295 FATE assertions plus inherited
Hunt V2/focus/clocks/consume/issuer/consent/resource/configuration checks.

Security/Privacy and Compatibility accepted source39f/treecb5 and exact package,
closing runtime conditions after the corrected trace and owner responsiveness
confirmation. No outstanding findings remain. Publication-only docs/feed/test-pin
updates do not change the accepted artifact; post-publication verification uses
separate validation versions, not a released85 rebuild.

## Published identities

Starting published version: 0.0.84.0, source0a2c50605135812464768842690af03ae575d94b.
Starting task HEAD: a19b655046e45b2bd2bfb71b74a1c03026ce6348.
Artifact source: 39f7a71a9d1d7d4f17068aff50c9d516aa9884fd.
Tree: cb5cfca651cadf09fa4b416a27af0d08eea955d6.
Branch: codex/game-sync/beastmaster-party-finder-testing.
Tag: v0.0.85-testing, annotated4dad847ac9b5ea08e14e8b0c680a5d0b3bb7f6e3.
Release: [Testing85](https://github.com/anndrox/GillionsGameSync/releases/tag/v0.0.85-testing).

- ZIP SHA256: 8e46200dc9f7cb41293d3aa13481d6b1273546e3689176b4525be05863c5c8bc
- DLL SHA256: 428b819c2fcc3c9492461203db3a0ee888fa4f0b1dac08afa792429cf84daa4c
- Manifest SHA256: 4114c60957cfcfcbaa6cd58abfe29cf11eec9b63806e3e7867c1525a40b491ee
- Embedded manifest SHA256: d96ed9dfa18393b4f257dc3e2f5495cfd9e506226a37c6c46b940e20447646c9

The [publication record](../../data/releases/testing-0.0.85.json) binds release IDs,
checks and limits. The immutable and rolling public manifest/ZIP/contained DLL/
embedded identity match exact accepted bytes; all four download links point to85.

Keep the existing rolling Testing URL:
https://github.com/anndrox/GillionsGameSync/releases/download/v0.0.64-testing/GillionsGameSyncTesting.json

Disable the diagnostic dev-plugin entry before enabling installed Testing85;
never run both. Keep configuration. No reset, re-pairing or extra gameplay needed.

## Exact Site activation handoff

Site contract baseline is ab21c3bf2a904ed8edd07f2591e8035e6a910694, schema0026,
revision2/wire1. No Site changes or real FATE contributions were performed.
Reserved TEST endpoint is POST https://test.gillions.app/api/game-sync/fates/contribute.
Capability and collectorSchema are fate-live-observations-v1; schemaVersion1,
coverage positive_only; independent policy fate_public_observations revision1.

The [Native contract](../contracts/fate-live-observations-v1.md) defines the exact
six-key envelope, eleven-key source tuple, fields, occurrence discriminator,
freshness/merge requirements, bounds, receipts/retry errors and privacy exclusions.
Site must explicitly admit actual collector0.0.85.0 and the exact source matrix:
game/reference2026.09.15.0000.0000; API15; Dalamud15.0.3.6 revision
b666d821a47306fb447c60155b5d99377f91a5ee; ClientStructs7.56.2.9136 revision
313161e448e335adddd928f8a0212b2c328b5659; Lumina7.7.0/Excel7.5.1.

**Required dependency:** Site must define the exact authenticated discovery response:
which existing authenticated response carries schema/capability/source/reference
admission, strict field keys/types, explicit independent consent, expiry/revocation,
current Testing device/account and pairing-generation binding, and how updates
are detected without expanding private gameplay reads. No default-ON grant is
authorized. Missing, malformed, expired or revoked admission fails FATE closed.

The published85 has no live grant parser, grant installation or HTTP sender.
After agreement, a bounded Native follow-up must wire discovery/policy lifecycle
and sender, then jointly validate auth/revocation/context/retry/freshness. Merely
deploying an endpoint cannot activate85. Helpers are preparation, not live wire proof.

Site's separately authorized activation must enforce exact source/reference/mode
validation; paired Bearer Testing account/device admission; private contributor
custody; positive-only conflicting observation merge and freshness; 1attempt/5s/
device, shared12/minute/account burst2/global600/minute; same-ID/body receipts and
different-body409; bounded receipt/log retention/deletion; de-identified read model.
Public responses must exclude producer fingerprints/private identifiers and must
not join Hunt/travel data. No-start evidence cannot create durable alert/history
identity; absence and estimated end cannot establish completion.

For unrelated PF consumer consistency, preserve the agreed clickable wording:
**Data provided by xivpf.com**. Technical PF/contribution contracts remain unchanged.

Stable/main/production/Site/schema/database/Market architecture/Wardrobe remain
untouched. Hunt80 final completion,84 coverage margin,82 command responsiveness,
V1/V2 maps, PF/item links, Beastmaster, Market, Submarines, Dashboard/Custom Deliveries,
travel privacy and ordinary sync are preserved. No gameplay writes, TLS bypass,
public alert, Watch/Tracked FATE or static Site UI change.
