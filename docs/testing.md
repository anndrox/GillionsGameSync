# Testing

This document describes the `1.0.30` source and its synthetic verification. The stable feed and GitHub Release identify the published package. Testing builds retain their separate product identity and publication path.

Run `./scripts/verify.ps1`. It executes the linked-production policy suite, source integration contracts, packaging fixtures, stable/testing builds and actual Dalamud configuration round-trips. Fixtures stay under ignored `artifacts/verification`; they do not use a game session, installed user configuration, server or database.

## Corrected boundaries

The managed suite exercises canonical HTTPS origins and bound credentials; A-B-A character changes; re-pair, automatic/item-link opt-out and disposal permits; exact sent/current Retainer and Gil versions; delayed/subset/foreign acknowledgements; preservation of unrelated and equal-value ambiguous sales; unchanged/unavailable roster semantics; a 60-second freshness-save clock; native result-view tuple deduplication; and transient expiry without new input.

Storage fixtures cover 10,000 records across characters, capacity plus one, exact 8 MiB serialized accounting, a same-ID replacement that exceeds the byte limit, acknowledged drainage, re-pairing and reload. The accounting document includes owner and queue metadata. One ordinary synthetic Gil row occupies 404 bytes; 10,000 such rows across two fixture characters occupy 4,030,001 bytes. These examples establish accounting consistency, not typical player accumulation, FPS or hours of offline coverage.

Response fixtures cover missing/dishonest Content-Length, a streamed byte cap, depth 17 rejection, malformed success receipts, a valid empty-glamour no-prior receipt, synthetic secret echoes and cancellation. Gil wire tests require all entries to pass the existing server's validation, at most 200 events, and omission of absent numeric fields so they cannot become rejected zero values under the server's normalizer. Retainer wire batches remain at most 50 without truncating the durable queue.

Source contracts connect the tested policies to real plugin dispatch, framework-owned completion, legacy quarantine, cancellation, original system-message gating, UI command routing and coalesced saves. They retain the two-second Gil fallback, 750 ms dirty handling, separate Retainer deadline and ordinary category rotation. The updated plugin advertises observation/result/ACK/presence capabilities only. Stable Retainer uploads still require exact accepted product/contract identity; an old server's planner status cannot grant executable behavior.

## Actual configuration and UI-state fixtures

The configuration suite loads each built product's real configuration type through `PluginConfigurations.LoadForType`, invokes its normal Save wrapper through a storage-only proxy, and uses Dalamud's serializer. Eleven legacy fixtures per product preserve populated and malformed opaque planner values, unknown fields, scalar timestamp offsets/precision, missing fields and repeated saves. Reader tests preserve ordinary typed dates/strings, root metadata behavior and nested lookalike handling.

Additional actual-assembly fixtures save/reload valid origin bindings, two owned character queues, an independent legacy queue and a paused coverage gap. A new pairing must create an empty current partition while retaining and counting previous owned records. The exact owned accounting document is compared before and after the real serializer. Static ledger-classification fixtures retain valid structured sales and keep malformed item/quantity/amount evidence inferred.

Eight deterministic UI model states are exported per product, including initial pairing, legacy re-pairing, connected, automatic-off, logged-out, storage-paused, resumed-gap and combined warnings. Technical details and diagnostic rows are constructed only when their sections are expanded; their data is projected on the framework thread and rendered through immutable view snapshots.

These are source/model fixtures, not rendered ImGui frames or live-game acceptance. They do not establish game-version compatibility, actual channel behavior in every locale, visual layout in an installed client, or cancellation of callbacks queued by an older plugin. Required independent UX, Security and Compatibility reviews remain separate gates. Local verification and packaging do not publish or install a plugin.
