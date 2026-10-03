# Privacy

### Testing 0.0.71–0.0.72 private daily / weekly facts

This separate OFF-by-default experiment retains private personal counters,
flags and activity/next-boundary times locally. Own ContentId is transient;
a domain-separated SHA256 partition remains local, never exported. At most
16 characters / 24 latest groups each / 384 KiB; overflow preserves records,
warns and refuses oversized admissions. No unlimited history or silent eviction.
Manual PRIVATE copy excludes names, raw/local-hashed identities, account/FC,
credentials, pairing secrets, logs, equipment and Dashboard configuration.
There is no new upload, public sharing, credential or scope. Disable stops new
reads/copy and preserves retention; copies already shared cannot be retracted.
Configuration contains unrelated credentials and must never be shared.
Unsupported retained members stay inert and are excluded from export. Source
ownership/reset freshness remains unverified and explicitly disclosed; no automatic
dated checklist completion is authorized. 0.0.72 adds loaded Doman native donation
totals under the same private opt-in and bounds. Finite Custom Delivery rejection
reasons are session-local, with no identifiers, private counters or exception text.
See [schema/retention](contracts/dashboard-facts-v1.md).

## Testing 0.0.69 personal observation experiments

Hunt local retention defaults OFF independently of pairing/ordinary sync.
It stores at most 16 own-character hashed partitions, 22 latest positive bill
observations per partition, below 256 KiB in existing Testing configuration.
Only the logged-in player's Content ID is read for local scoping; raw ID is not
retained or exported. Hashing is not anonymization. No other player's ID,
sighting/radar, chat, spawn scan or Hunt upload is introduced.

Submarine snapshots now also hold a private hashed workshop scope and optional
positive unlocked/explored sector evidence. They still use the same existing
retained store, not another database/file. Unavailable state never erases history.
Hunts sample naturally loaded caches at most once every five seconds on the
existing framework loop, without requiring a bill window. Reads require loaded
own-player state, no zoning and loaded Key Items containing a matching bill item.
That corroborates the bill category, not cached-order ownership or freshness.
Submarines remain interface-event-only, throttled to at most once/second. Both
readers are patch-gated and require explicit local opt-in; neither requests data
from the game server or automatically opens an interface.
Global Automatic sync does not govern these
separate local tests; disable their local controls to stop reads.

**PRIVATE export is not community contribution.** It requires explicit prepare
and copy and contains gameplay/activity facts. Submarine private export includes
names, slot, registration, hashed workshop scope and route/return/progression.
Hunt private export excludes character name/ID/hash, but contains assigned-target
cache/counter observations and times. It is scoped to the active character;
submarine export requires a workshop verified in the current territory/session.
Opt-out/session changes clear prepared copies. An already copied clipboard or
shared file cannot be recalled. Copy only if permitted, to a trusted recipient.
Never share the credential-bearing full configuration. Aggregate diagnostics
contain no names, IDs, routes, counters, credentials or raw exception text.

The existing sanitized voyage-results export excludes all newly added private
fields and remains separately consented. Neither private nor community export
uploads. No new credential, receiver, public FC telemetry or enrollment expansion
exists. Future private sync requires explicit consent, Testing/authenticated
admission, account isolation and compatibility review; see the
[exact draft and limitations](contracts/personal-observations-v1.md).

Gillions Game Sync is opt-in and account-linked. Automatic sync is a global switch for its nine supported ordinary categories, not a per-category selection. Manual sync requests one collection of available supported data. Paired startup and successful pairing perform one character sync and presence request even when Automatic sync is off. Website item links have a separate switch, enabled by default, and require a valid pairing and logged-in character.

## Connection and ownership

Pairing sends the plugin version and a one-time pairing code to the HTTPS origin displayed in the pairing form. It no longer collects or submits a machine name or a machine/user-domain hash. The returned device credential is bound locally to that origin, server-issued device ID and a fresh pairing generation. Editing the next-pair server address does not transfer an existing bearer credential to another origin. HTTPS self-hosted origins, including explicit ports, remain supported; credentials, query strings and non-root paths are not accepted in server addresses.

Older credentials lack that binding and must be paired again once. Their original configuration fields, unscoped queues/maps and opaque planner history remain preserved but inactive. New data uses separate pairing-generation and character-content-ID partitions. A new pairing does not infer ownership of former records or delete website history. Internally valid pairings created by this version can resume after reload without another pairing.

The current bearer, origin binding and private gameplay history are stored in ordinary Dalamud plugin configuration. **Do not share that file in support bundles.** Disconnect clears the active credential; it preserves local history. Logout, character changes, pairing changes and disposal clear transient context and invalidate requests. Automatic and item-link opt-outs invalidate their respective request modes. A request already accepted by the server cannot be undone by client cancellation; stale local completions leave pending evidence preserved.

## Optional Party Finder contribution

Party Finder listens only to Dalamud's public listing event; it never actively queries the game. Stable builds retain their separate off-by-default direct contribution to [xivpf.com](https://xivpf.com), independent of Gillions pairing. Testing `0.0.65` instead sends to the fixed `https://gillions.app/api/game-sync/party-finder/contribute` receiver. It requires a logged-in paired Testing device, explicit site-side contribution permission and a new local opt-in. Old third-party opt-in does not authorize this different recipient. Automatic/manual character sync is independent. Older testing `0.0.64` remains loopback-only.

The payload contains public listing IDs, owner ID lower bits, encoded names/descriptions, worlds, duty/settings, jobs and slots. Listing owners need not be contributors. Testing authenticates with the existing paired credential only to Gillions HTTPS; neither credentials nor reporter IDs are placed in the payload or forwarded to xivpf. Gillions derives a private reporter from authentication. It maintains a bounded runtime-only current cache, not history/durable delivery; restart clears it. No Square Enix credentials, private chat, diagnostics or unrelated local data are included. Testing queues at most 1,000 identities plus an in-flight batch of at most 100. Opt-out, logout, character changes, re-pair and disposal cancel/clear old-session testing observations. Accepted server effects cannot be recalled by cancellation.

Testing requests contain at most 100 listings / 256 KiB. Native events share conservative 100-ms observation buckets; timestamps and countdowns are never regenerated on retry. Expired/stale observations are dropped. Batching waits ten seconds after the newest listing, honors server Retry-After/backoff with jitter and never starts more than six requests in a rolling minute per load; server limits are shared across an account's devices. Permission failures persist a non-secret blocked paired-generation marker until explicit correction/fresh enrollment. Logout, character changes, local off/on and reload do not remove that stop; no credentials, listings or response bodies are added to persisted stop state. The new local setting persists, but queues and rate counters do not. Redirects/404 instead stop the current load until deployment is corrected and the plugin reloaded; re-pairing does not repair an endpoint. Redirects are never followed. Only exact matching v1 acknowledgements count as acceptance of the current cache—not upstream delivery or proof of game-data truth.

## Data and temporary observations

### Testing 0.0.67 passive market candidate

The owner-selected plugin setting defaults ON, including older Testing configs
with no saved choice. This differs from the other experimental opt-ins. Disable
"Contribute observed market data to Gillions" to prevent new market sends; unsent
transient work clears and active requests are cancelled where possible. Ordinary
sync/PF and Dalamud's own uploader/preference are unaffected. The website does not
change this choice; server compatibility/authentication is availability, not consent.

OFF and denied-enrollment state persist through supported 0.0.67 handling, not
unsupported downgrades. A 0.0.66 save can discard both fields; a later 0.0.67 load
then defaults ON and loses that stop. Preserve configuration, do not downgrade,
and re-check/disable contribution before browsing after any older-binary save.

The market payload excludes player/character/account/Content IDs, buyer/retainer/
artisan names and identities. It includes public market listing IDs (not reporter
IDs), item/world, HQ/quantity/unit price and available retainer city; recent sales
include raw sale price, quantity, HQ, mannequin flag and source purchase UTC time.
No listing creation/review time or complete-listing count is exposed by the
supported interface; none is fabricated. World is guarded current-world context,
not a field supplied by the response. Every observation remains partial.

Authentication uses the existing origin-bound bearer solely in Authorization;
the dedicated contribution transport neither accepts nor replays cookies, and
market intake must not create cookie authentication/tracking.
existing presence still contains its established character/device relationship.
The presence body is unchanged, with an optional non-identifying capability
header. User-Agent gives the existing product/version. TLS/network service and
existing authentication can associate transport with its device/account/IP, so
the transport is **not anonymous**. The required shared market dataset must not
persist reporter/account/device linkage; Site must implement and verify that
separation before admitting contributions. Authentication does not attest game truth.

No market payload or receipt is persisted in plugin configuration. Only the ON/OFF
choice and non-secret denied-enrollment generation are saved. At most 64 pending
packets plus one in flight and 64 short-lived dedup fingerprints live in memory.
They expire after two minutes and clear on session/world changes, opt-out or
disposal. Drops are acceptable cache contributions, not financial/durable custody.
Body cap 32 KiB, response cap 8 KiB, 15-second deadline, at most three attempts
per observation and six starts/minute per load. No Universalis path is added.
See [exact fields, limitations and Site requirements](contracts/market-observations-v1.md).

### Testing-only submarine local retention

Testing `0.0.66` adds a separate off-by-default local-retention control and a second
off-by-default community-preparation control. Reads occur only on naturally opened
workshop/voyage interface events, not polling or game-server requests. No voyage
actions, third-party IPC or other players' account identifiers are collected.
Names, local slots/registration and private hashed workshop scope are saved locally
with numeric builds/routes/results and observation/version evidence in the existing
Testing configuration. Raw house identity is transient, not retained. That
configuration already contains a credential: never share it as a support bundle.

Community opt-in allows only future completed results to enter an explicitly
prepared/copied sanitized dataset. There is **no upload endpoint or automatic
sharing**. Export excludes names, local hashes, house/FC, slot/registration,
reporter/account identifiers, credentials and raw logs. Routes/builds/rewards/times
can still reveal FC activity; share only information you are allowed to contribute.
Current opt-out disables export and clears prepared data, but preserves local
history and cannot retract a copy already shared externally. Only aggregate
diagnostics are offered for support. No new credentials or pairing scopes exist.

Retained component reserves fewer than 4 MiB for at most 400 voyage anchors/results
and 32 snapshots. Overflow preserves all admitted history, warns and refuses new
identities; existing anchors can still receive results. No pruning/eviction,
automatic ACK, contribution deletion or server history is invented. Unsupported
retained versions are preserved and fail closed. Producing build/departure and
sector attribution remain explicitly unavailable when not verified; unlinked or
conflicting results are excluded from countable exports. See the
[local schema and limitations](contracts/submarine-voyages-v1.md).

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

## Combined testing candidate: Beastmaster

The testing-only Beastmaster view requires explicit local sampling opt-in after
each plugin load. Closing its window stops sampling and clears results. Pet names
and ownership stay in memory; character identity is used only internally to
invalidate stale results. No Beastmaster data is saved, logged or uploaded.
An explicit copy action places the displayed pet results on the local clipboard.
Ordinary paired Game Sync behavior remains governed by the controls above.
