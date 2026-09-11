# Privacy

Gillions Game Sync is opt-in and account-linked. Automatic sync is a global switch for its nine supported ordinary categories, not a per-category selection. Manual sync requests one collection of available supported data. Paired startup and successful pairing perform one character sync and presence request even when Automatic sync is off. Website item links have a separate switch, enabled by default, and require a valid pairing and logged-in character.

## Connection and ownership

Pairing sends the plugin version and a one-time pairing code to the HTTPS origin displayed in the pairing form. It no longer collects or submits a machine name or a machine/user-domain hash. The returned device credential is bound locally to that origin, server-issued device ID and a fresh pairing generation. Editing the next-pair server address does not transfer an existing bearer credential to another origin. HTTPS self-hosted origins, including explicit ports, remain supported; credentials, query strings and non-root paths are not accepted in server addresses.

Older credentials lack that binding and must be paired again once. Their original configuration fields, unscoped queues/maps and opaque planner history remain preserved but inactive. New data uses separate pairing-generation and character-content-ID partitions. A new pairing does not infer ownership of former records or delete website history. Internally valid pairings created by this version can resume after reload without another pairing.

The current bearer, origin binding and private gameplay history are stored in ordinary Dalamud plugin configuration. **Do not share that file in support bundles.** Disconnect clears the active credential; it preserves local history. Logout, character changes, pairing changes and disposal clear transient context and invalidate requests. Automatic and item-link opt-outs invalidate their respective request modes. A request already accepted by the server cannot be undone by client cancellation; stale local completions leave pending evidence preserved.

## Data and temporary observations

Supported synchronization includes character name/world, character and Retainer identifiers, inventory/items, Gil and currencies, progression/unlocks, loaded Retainer listings and venture evidence. Presence includes character identity and client product/channel/version for compatibility checks. Ledger records can include numeric game-event IDs and up to eight integer arguments. The plugin does not send Square Enix credentials, player chat text, arbitrary local files, diagnostic recordings automatically, or unrelated accounts' retained records. It has no inbound listener, packet capture or gameplay controls.

While recurring collection is authorized, callbacks inspect only relevant system log IDs and original system-sale channels. Player-channel text is rejected before extraction. Selected sale text is used transiently to derive structured facts; raw text is not retained or uploaded. Unsupported locale or parameter evidence stays unclassified or inferred while normal native balance observation remains available.

Log/chat/withdrawal matching opportunities expire after five seconds; item attribution expires after ten seconds. Each transient buffer is capped at 256 entries and maintenance runs independently of new matching input. Character, pairing and lifecycle changes clear the context, including cached listings. Item-link claims also use bounded local replay tracking and server consumption before printing.

## Pending storage and gaps

Newly managed pending Gil events, receipts, deposits, sales and venture results share a limit of 10,000 records or 8 MiB across all pairing generations and characters. Accounting is the UTF-8 size of a flat JSON array containing each record with its generation, character and queue label. It includes ownership metadata and same-ID replacements. It is not a limit on the entire configuration file, nonpending snapshots, log files or preserved legacy history.

Admitted evidence is not silently evicted. If a new admission or replacement exceeds the limit, existing records remain and a bounded coverage-gap state is saved. New event recording pauses while native Gil observations keep their two-second fallback and 750 ms dirty handling. Recording resumes from a fresh baseline after acknowledged drainage; missed transactions cannot be reconstructed. Previous pairing generations and uncertain records can keep occupying storage. Re-pairing does not reset the budget or grant authority to upload those records. Unowned legacy history remains outside the new budget and can already be large.

Routine freshness-only saves are coalesced with other changes and occur no more often than once per 30 seconds. Semantic changes and admitted durable evidence request prompt framework persistence. A crash before the next save can replay acknowledged data; exact identifiers and version checks favor preserving evidence over deleting unsent updates.

## Retired integration and diagnostics

Version `1.0.30` removes AutoRetainer discovery, IPC, plan polling and apply/restore paths. Neutral legacy presence fields preserve old-server parsing without inspecting other plugins or advertising planner readiness. Existing plan backups and ownership records remain inert, including unknown fields and scalar timestamp strings. Prior AutoRetainer-derived stats/start times remain historical and are not projected into a new owned session. No automatic restore, purge or history recovery/export is added. Older installed plugins and external plans retain their own behavior until separately changed.

Stable diagnostic recording is off by default and can be started for ten minutes. Testing builds record diagnostics automatically. The in-memory display holds at most 40 lines; entries also use normal local Dalamud logging, whose retention is separate. Reports can contain gameplay details such as balances, Retainer names and structured item facts. Review copied reports before sharing. Remote error bodies and arbitrary server error messages are not echoed into diagnostics or the window; recognized errors use local text. Control responses are limited to 64 KiB and JSON depth 16 before receipt processing.

## Unreleased testing candidate: Beastmaster

The testing-only Beastmaster view requires explicit local sampling opt-in after
each plugin load. Closing its window stops sampling and clears results. Pet names
and ownership stay in memory; character identity is used only internally to
invalidate stale results. No Beastmaster data is saved, logged or uploaded.
An explicit copy action places the displayed pet results on the local clipboard.
Ordinary paired Game Sync behavior remains governed by the controls above.
