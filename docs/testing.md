# Testing

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

The separate XIVPF testing endpoint remains loopback-only. The Gillions address
does not change it. Leave contribution disabled until the server-hosted test
receiver and its exact client-facing URL are verified and a reviewed successor
targets that receiver. Beastmaster results remain local-only and unuploaded.

This document describes the `1.0.30` source and its synthetic verification. The stable feed and GitHub Release identify the published package. Testing builds retain their separate product identity and publication path.

Run `./scripts/verify.ps1`. It executes the linked-production policy suite, source integration contracts, packaging fixtures, stable/testing builds and actual Dalamud configuration round-trips. Fixtures stay under ignored `artifacts/verification`; they do not use a game session, installed user configuration, server or database.

The Party Finder fixture executes exact `UploadableListing` serialization, ten-second newest-listing batching, identity deduplication, the rolling six-attempt ceiling, failed-request backoff, the queued 1,000-identity bound, opt-out cancellation/clearing, completion races with disable/disposal, redirect rejection, endpoint policy and event-source disposal. Actual built-product configuration tests verify that absent settings remain off, explicit opt-in persists, the stable-compatible assembly resolves `https://xivpf.com/contribute/multiple`, and the testing assembly resolves only the default loopback endpoint. Synthetic HTTP handlers never contact xivpf.com.

An end-to-end contribution check is separate: run the official Remote Party Finder server with its MongoDB dependency on loopback, build the testing product, and submit a fixture to its local `/contribute/multiple` route. Record the accepted server response and database result. Never substitute the production endpoint when the official local runtime is unavailable.

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
