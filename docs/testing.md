# Testing

## Fixed Testing update URL

Keep `https://github.com/anndrox/GillionsGameSync/releases/download/v0.0.64-testing/GillionsGameSyncTesting.json`
in Dalamud. This JSON is the rolling Testing channel pointer, not a version pin.
The published feed currently advertises 0.0.69; authorized 0.0.70 publication
advances the same JSON to the tested target-name correction. Refresh
the plugin list and update normally; keep the installed configuration. Future
authorized Testing publications must advance this same JSON, not ask testers to
change repository URLs. [Publication procedure](releasing.md#fixed-testing-update-feed).

## Testing 0.0.68 — local Hunt Bills and private submarines

0.0.70 adds readable localized target names, separate wrapped target rows and
kill counters. Use [0.0.70 live checks](releases/testing-0.0.70.md). This is
display-only: numeric retention/export and five-second admission are unchanged.
Fixtures cover Unicode, missing/throwing/empty names, text bounds, counters,
static cache reuse/overflow and unchanged retained data. No live rendering proof.

For the Hunt event-path correction, use [0.0.69 retest instructions](releases/testing-0.0.69.md).
The corrected reader does not require opening a bill when matching loaded Key
Items/cache data are present. 0.0.68 collection instructions below are historical.

Use [0.0.68 live checks](releases/testing-0.0.68.md) and the
[private-state draft/Site handoff](contracts/personal-observations-v1.md).
Both local-retention choices default OFF. No Hunt/submarine uploads exist;
Site preview data cannot become synchronized personal state until the bounded
intake handoff is implemented and joint tests pass. Published 0.0.67 is the
immutable predecessor (its original preparation notes below are historical).

The new experiments require game `2026.09.15.0000.0000` and installed
FFXIVClientStructs assembly `7.56.2.9136`. Other pairs stop native collection and
preserve history. This is an inspected catalog/SDK pair, not live correctness.
Run the full verification plus focused personal-state fixtures; actual Dalamud
Save/load tests are separate from real interface/access/performance validation.

## Passive market candidate — Testing 0.0.67

Same-lineage successor to 0.0.66, published October 1, 2026 (no live proof). Use the
[candidate checklist](releases/testing-0.0.67.md) and [intake contract/Site handoff](contracts/market-observations-v1.md).
Public-interface adapter fixtures use identity getters that throw if accessed.
Managed fixtures test sanitation, partial/empty/malformed data, source timestamps,
duplicate packets, bounded queues/rate/retries, Retry-After, auth/endpoint stops,
cancelled in-flight requests, session changes and compatibility fail-closed behavior.
Actual Dalamud Save/load verifies default ON for new/older config, persistent OFF,
ordinary-sync isolation and denied-enrollment persistence. Stable excludes all
new runtime/setting types. No live market traffic is used by these fixtures.

## Submarine local retention — Testing 0.0.66

Use [Testing66 in-game checks](releases/testing-0.0.66.md) and the
[local retained/export draft](contracts/submarine-voyages-v1.md). The collector is
Testing-only, event-driven, read-only and off by default. Community preparation is
an independent opt-in/manual copy, not an upload. No server or third-party plugin
is required. Fixtures cover partial data, identity/duplicates, successive voyages,
route/build conflicts, sector/voyage reward limitations, restart persistence,
reservation/overflow, consent, sanitation and Stable exclusion. Actual config tests
use Dalamud Save/load; none establishes live native collection or frame times.

## Main-site HTTPS testing

The owner selected `https://gillions.app` for ordinary testing-client sync.
The published `0.0.64` package already supports that HTTPS origin. Use a normal
single-use pairing code from the main site. Open `/gillionssynctest pair`, set
the server address under **Connection details**, enter the code and choose
**Pair this device**. Confirm **Connected to https://gillions.app** before sync.
Editing the address alone does not retarget an existing bound session.

These are real main-site data writes, not an isolated test-database run. Disable
the stable copy while comparing/testing ordinary sync to avoid duplicate
collectors; preserve both configurations. No credentials or existing records
are transferred between the stable and testing product identities.

The previously prepared private-HTTP pairing exception was never published and
has been withdrawn. Both products retain HTTPS-only pairing.

Testing `0.0.65` uses the fixed authenticated Gillions HTTPS Party Finder intake.
It requires a new pairing created with Testing selected, real-data acknowledgement
and separate site-side public Party Finder permission, plus a new local opt-in.
Old `0.0.64` contribution settings do not enable this recipient. Leave contribution
off until Site confirms production activation. The editable server field cannot
retarget Party Finder or send its credential to another origin. Beastmaster remains
local-only. See [0.0.65 testing checks](releases/testing-0.0.65.md).

This document describes the `1.0.30` source and its synthetic verification. The stable feed and GitHub Release identify the published package. Testing builds retain their separate product identity and publication path.

Run `./scripts/verify.ps1`. It executes the linked-production policy suite, source integration contracts, packaging fixtures, stable/testing builds and actual Dalamud configuration round-trips. Fixtures stay under ignored `artifacts/verification`; they do not use a game session, installed user configuration, server or database.

Party Finder fixtures cover legacy direct-xivpf behavior and the testing Gillions v1 wrapper: bounded batches/bodies, exact acknowledgements, immutable retry timestamps, stale drops, permission stops, retry delays, request ceilings and session/opt-out cancellation. Actual built-product configuration tests verify separate consent and fixed product endpoints. Synthetic handlers never contact live services.

End-to-end validation is separate: pair the testing successor through the owner-authorized HTTPS site, enable both site permission and local consent, and manually browse Party Finder. Compare exact server acknowledgements with current read results and expiration. Do not make testers host a local service, relax TLS or send credentials to xivpf.com. The legacy loopback integration harness is synthetic/disposable tooling, not the installed testing product's route.

## Corrected boundaries

The managed suite exercises canonical HTTPS origins and bound credentials; A-B-A character changes; re-pair, automatic/item-link opt-out and disposal permits; exact sent/current Retainer and Gil versions; delayed/subset/foreign acknowledgements; preservation of unrelated and equal-value ambiguous sales; unchanged/unavailable roster semantics; a 60-second freshness-save clock; native result-view tuple deduplication; and transient expiry without new input.

Storage fixtures cover 10,000 records across characters, capacity plus one, exact 8 MiB serialized accounting, a same-ID replacement that exceeds the byte limit, acknowledged drainage, re-pairing and reload. The accounting document includes owner and queue metadata. One ordinary synthetic Gil row occupies 404 bytes; 10,000 such rows across two fixture characters occupy 4,030,001 bytes. These examples establish accounting consistency, not typical player accumulation, FPS or hours of offline coverage.

Response fixtures cover missing/dishonest Content-Length, a streamed byte cap, depth 17 rejection, malformed success receipts, a valid empty-glamour no-prior receipt, synthetic secret echoes and cancellation. Gil wire tests require all entries to pass the existing server's validation, at most 200 events, and omission of absent numeric fields so they cannot become rejected zero values under the server's normalizer. Retainer wire batches remain at most 50 without truncating the durable queue.

Source contracts connect the tested policies to real plugin dispatch, framework-owned completion, legacy quarantine, cancellation, original system-message gating, UI command routing and coalesced saves. They retain the two-second Gil fallback, 750 ms dirty handling, separate Retainer deadline and ordinary category rotation. The updated plugin advertises observation/result/ACK/presence capabilities only. Stable Retainer uploads still require exact accepted product/contract identity; an old server's planner status cannot grant executable behavior.

## Actual configuration and UI-state fixtures

The configuration suite loads each built product's real configuration type through `PluginConfigurations.LoadForType`, invokes its normal Save wrapper through a storage-only proxy, and uses Dalamud's serializer. Eleven legacy fixtures per product preserve populated and malformed opaque planner values, unknown fields, scalar timestamp offsets/precision, missing fields and repeated saves. Reader tests preserve ordinary typed dates/strings, root metadata behavior and nested lookalike handling.

Additional actual-assembly fixtures save/reload valid origin bindings, two owned character queues, an independent legacy queue and a paused coverage gap. A new pairing must create an empty current partition while retaining and counting previous owned records. The exact owned accounting document is compared before and after the real serializer. Static ledger-classification fixtures retain valid structured sales and keep malformed item/quantity/amount evidence inferred.

Recovery fixtures invoke the actual built plugin's managed acknowledged-drain, stable-retainer-observation and receipt/deposit methods without its constructor or native reads. They cover reload during a gap, reload after recovery before observation, inactive ownership partitions and unavailable retainers. Historical balances and all five pending queues survive; the first post-gap observation cannot attribute a receipt or deposit from the missed interval, while later observed changes still correlate. Persisted per-retainer invalidation prevents a restart or character switch from reviving a pre-gap baseline.

Eight deterministic UI model states are exported per product, including initial pairing, legacy re-pairing, connected, automatic-off, logged-out, storage-paused, resumed-gap and combined warnings. Technical details and diagnostic rows are constructed only when their sections are expanded; their data is projected on the framework thread and rendered through immutable view snapshots.

These are source/model fixtures, not rendered ImGui frames or live-game acceptance. They do not establish game-version compatibility, actual channel behavior in every locale, visual layout in an installed client, or cancellation of callbacks queued by an older plugin. Required independent UX, Security and Compatibility reviews remain separate gates. Local verification and packaging do not publish or install a plugin.

## Performance validation

The performance candidate follows the combined Beastmaster/Party Finder source. It adds no polling loop and preserves item-link polling, ordinary category rotation, the Gil fallback/change delay, transient Retainer result capture, and Beastmaster sampling intervals.

Synthetic fixtures exercise 7,200 visible-window frames over 60 seconds at 120 FPS: the refresh policy admits 240 publications instead of one per frame. Closed windows admit none; reopening, session resets and queued UI actions can refresh immediately. This verifies cadence, not rendered frame-time improvement. Empty-owner storage fixtures verify exact accounting remains the two-byte `[]` document without serializing unused queue headers. Existing exact-capacity, overflow, acknowledgement and reload checks still apply.

The Party Finder fixture deliberately blocks synchronous HTTP startup on its worker and verifies that the Tick caller returns before it is released. The full suite retains exact payloads, deduplication, retries, the rolling request ceiling and cancellation/disposal races. Synthetic handlers do not contact the production contribution service.

For in-game validation, compare the installed baseline and candidate under matching conditions: normal play with settings closed; settings with Data status/Diagnostics expanded; inventory changes; Retainer result views; Beastmaster sampling; and receiving a full Party Finder listing batch. Record frame times as well as FPS. While diagnostics are active, `Native collection [...]` records elapsed framework-thread capture time (native reads plus copied snapshot/queue state). It excludes background JSON preparation and network response time. Stable diagnostic recording remains opt-in; testing recording follows the existing testing behavior.

Full character, quest and collectibles scans remain on the framework thread because their unlock reads access game state. Their actual cost has not been measured in a live game here. Use the per-category timing evidence before changing their cadence or splitting capture across frames. These source changes are included in the `0.0.64` successor, with an [in-game checklist and diagnostic-copy instructions](releases/testing-0.0.64.md); the immutable `0.0.63` package remains unchanged.
