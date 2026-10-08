# Submarine observations v1 — local Testing draft

Scope: introduced in Testing `0.0.66`; continued in `0.0.68`, local retention and manual sanitized export only.
This is **not an HTTP contract**, receiver, automatic uploader or server deployment.
Site Operations owns future admission, storage, comparisons, page and notifications.

Testing 0.0.68 adds an exact game/SDK gate, <=1/second event-read ceiling,
optional private workshop/positive-sector snapshot fields, and a separate PRIVATE
personal export. See [personal-state draft](personal-observations-v1.md); those
private fields never enter this sanitized community dataset.

## Read-only source and availability

The collector uses the installed Dalamud API 15 / FFXIVClientStructs definitions,
not copied offsets, packet hooks or another plugin. It observes `PostSetup`,
`PostRefresh` and `PostRequestedUpdate` for `SelectString`, `SubmarineExploration`
and `AirShipExplorationResult`. Reads require local opt-in, the framework thread,
a logged-in/loaded player, a visible interface, a loaded **current** workshop,
valid workshop identity, and coherent native submarine fields/catalog IDs.
No per-frame or timer polling, interface opening, game callbacks, dispatch,
recall, repair, redeployment or server requests are issued.

This is observation of loaded client cache, not proof of a fresh network reply.
The SDK has no independent submarine-cache generation flag here. Actual installed
game behavior, result-interface event timing and workshop/character changes still
require live acceptance. Missing/partial data never means empty or clears history.

| Field | Evidence and limitation |
| --- | --- |
| Identity/name/rank | Local workshop slot, registration timestamp, native bounded UTF-8 name and rank. No player or FC account identifiers are read. Name stays out of diagnostics/sanitized community export; explicit PRIVATE export is separately described. |
| Hull/stern/bow/bridge | Native `SubmarinePart` row IDs; catalog-validated. These are part row IDs, not item IDs. |
| Stats | Native surveillance, retrieval, speed, range and favor base/bonus fields, plus log speed. Stored as observed, not recalculated. |
| Current route | Ordered nonzero `CurrentExplorationPoints`, at most five distinct catalog sectors. Zero/invalid data is unavailable, not an empty voyage. |
| Planned route | Separate `SelectedPoints` while the planning agent owns the visible addon; never replaces a voyage's route. |
| Expected return | Native `ReturnTime`, only an expectation/anchor; it can clear when results are opened. |
| Departure | **Unavailable**. `RegisterTime` is submarine registration, not departure. Export uses null. No duration subtraction or guessed departure. |
| Voyage-time build | First build/rank/stats observed while expected return is still future. Evidence is `observed-while-in-flight-not-dispatch-proof`. Never substitute a later build. Late-only observation leaves it null. |
| Result-time build | Separate observed build/rank/stats, which may differ after return/rank-up. Not implicitly the producing build. |
| Sector results | Only with active submarine result agent owning the visible addon, populated result data, and a selected pointer matching a verified slot in the loaded workshop. An unverified selected copy pointer is not dereferenced; results remain unavailable. |
| Rewards | Native gathered `Point`, primary/additional item IDs, quantities and HQ flags. Validate sectors/items and compare aggregate item totals with the result interface before claiming sector attribution. No localized log parsing. |
| Voyage-only fallback | When the result interface's bounded item list is valid but sector attribution is inconsistent/unavailable, retain that list with `rewardAssociation=voyage-only`; HQ, experience and unlock evidence are unavailable. Never distribute aggregate rewards among sectors. |
| Experience/unlocks | Per-sector observed experience, unlocked sector, first-exploration, additional-submarine-unlocked and double-dip flags where gathered entries are valid. Total experience stays null unless route completeness is established. Zero unlocked sector becomes null, not a fabricated unlock. |
| Provenance | First observation/result observation UTC; game repository version, plugin collector version and `submarine-observation-v1`. Initial/result versions are separate. |

Lumina supplies local catalogs for parts, sectors, ranks and items. It does not
provide the FC's dynamic voyage results or prove ownership/freshness.

Technical definitions: [WorkshopTerritory](https://github.com/aers/FFXIVClientStructs/blob/fed8bc9977ff51e2762867cab8eb6a0b4aa58746/FFXIVClientStructs/FFXIV/Client/Game/Housing/WorkshopTerritory.cs),
[submarine planning](https://github.com/aers/FFXIVClientStructs/blob/fed8bc9977ff51e2762867cab8eb6a0b4aa58746/FFXIVClientStructs/FFXIV/Client/UI/Agent/AgentSubmersibleExploration.cs),
[result interface](https://github.com/aers/FFXIVClientStructs/blob/fed8bc9977ff51e2762867cab8eb6a0b4aa58746/FFXIVClientStructs/FFXIV/Client/UI/Agent/AgentExplorationResult.cs).
Submarine Tracker's MIT-licensed implementation was a technical reference for the
registration/cleared-return distinction, not copied runtime code or a dependency.
Its packet hook, account/FC identifiers, database and third-party integration are
not used. No AutoRetainer or Submarine Tracker installation is required.

## Retained schema and identity

Authoritative managed definitions: [SubmarineVoyages.cs](../../SubmarineVoyages.cs).
The existing Testing configuration has an additive `SubmarineVoyages` property;
Stable does not contain this property, collector or policy. No old data migration
or deletion is performed. [Synthetic retained example](../examples/submarine-retained-v1.json)
contains invented values, not a real FC/player observation.

Older Testing binaries do not understand this added configuration field and can
drop it when saving. Automatic downgrade is not a safe history-preserving rollback.
Disable this feature/plugin, preserve configuration and use a forward correction;
do not feed the new retained file to an older Testing binary as a recovery step.

Root `SubmarineVoyageRetention`:

- `SchemaVersion`: integer 1. Unsupported/malformed/oversized retained formats are
  preserved unchanged and collection/export fail closed. First/result observation
  times must be UTC and no earlier than the Unix epoch; invalid loaded timestamps
  are not exported or silently repaired.
- `LocalRetentionEnabled`, `CommunityContributionEnabled`: separate booleans,
  both false by default. Neither depends on ordinary Automatic sync or pairing.
- `Current`: at most 32 local snapshots, including name/slot, private scoped key,
  registration timestamp, build/progression, loaded current and optional planned
  routes, expected return, observation/version metadata and last voyage anchor.
- `Voyages`: at most 400 anchors or completed/unlinked result records, with stable
  random `ObservationId`, private dedup keys, route/return, separate voyage/result
  builds, provenance, result, linkage/conflict and observation-time consent flags.
- `CapacityReached`: durable visible warning, not permission to delete history.

Private submarine key is SHA-256 of workshop house identity + slot + registration
timestamp. Raw house identity is transient and never retained/exported. Hashing
is local scoping, **not anonymization for publication**; the hash, name, slot and
registration are excluded from sanitized community exports, not the new explicit PRIVATE personal export.

Anchored voyage key is SHA-256(private submarine key + expected return timestamp).
Route is deliberately not part of the identity: a changed route for the same
anchor is a conflict, not another completed voyage. Successive return timestamps
make distinct voyages and preserve older results. Renaming a submarine does not
change its identity; registration distinguishes replacement in the same slot.

Results attach only to the last retained anchor for the same submarine, when
expected return is due (30-second clock tolerance) and valid sector IDs belong
to its route. Exact repeats are ignored, including after restart. Conflicting
routes/in-flight builds/results flag the existing record and preserve the original;
conflicted records cannot enter countable exports. Only identical aggregate
voyage-only results may gain stronger verified sector attribution; this does not
replace observation-time consent, first result time or the original build.

Reload and community export validate deterministic linked-voyage identity,
current-anchor existence/ownership, build evidence, result chronology, route
membership, aggregate reward totals and total experience coherence. Dangling,
foreign or inconsistent retained relationships fail closed without repair or
history deletion. Result association separately rechecks anchor ownership;
successful initial validation is not permission to trust a later mutated link.

Planning metadata is normalized before the single snapshot admission per slot
and interface event. Identical refreshes within 60 seconds do not mutate retained
timestamps or save. A later event may refresh observation UTC at most once/minute.
An actual semantic change requests one save for
the event. Enabling retention reports awaiting verified workshop data, not a
previous off/loaded status. No verified slots means unavailable, never empty.
Unsupported-format and capacity warnings remain visible after initialization or
either consent toggle, including while local retention is off.

If no defensible anchor exists, retain an **unlinked** result fingerprint scoped
to that submarine. Identical unlinked results collapse; two truly identical
successive voyages without an anchor cannot be distinguished. Such observations
are not claimed as countable voyages and are excluded from contribution exports.
No session time or guessed departure is fabricated to manufacture uniqueness.

## Bounds and overflow

Reserve at most 8192 serialized UTF-8 bytes per voyage record and 4096 per current
snapshot. With 400 records, 32 snapshots and bounded metadata, this retained
component stays below 4 MiB; it is separate from ordinary sync's existing pending
budget and is **not a bound on the whole credential-bearing configuration**.

At capacity, preserve every admitted record, show a durable warning and reject
new identities without eviction. Existing anchors can still receive bounded
completed results because their space was reserved. Export does not acknowledge,
delete or free records. There is no automatic pruning, unsent-record discard,
server ACK or invented archive/reset workflow. A future reviewed retention/archive
decision is needed to continue beyond the limit. A save failure is surfaced; a
fixture does not prove filesystem durability on a player's machine.

## Sanitized export draft

[Synthetic export example](../examples/submarine-export-v1.json) is valid JSON
with invented values. Root fields are exactly `schemaVersion:1`,
`collectorSchema:"submarine-observation-v1"`,
`uploadState:"local-only-no-server-contract"` and `voyages` array (0–400).

Each eligible voyage includes only:

- random stable `observationId` (32 hex digits), schema/collector identifiers;
- first/result game and collector versions, first/results UTC observation times;
- ordered `SubmarineExploration` sector IDs, expected return UTC, null departure;
- optional producing-time `voyageBuild`, explicit `buildEvidence`, separate
  `resultTimeBuild`: rank, four part row IDs and the observed base/bonus/log stats;
- `result`: `rewardAssociation` (`per-sector` or `voyage-only`), `sectors`,
  `voyageRewards`, nullable `totalExperience`, and allowlisted limitation codes.
- Sector entries: sector ID, experience, nullable unlocked sector ID, observed
  first/additional-submarine/double-dip booleans and at most two rewards. Each
  reward has positive item ID/quantity and HQ boolean; voyage-only HQ is null.

Only linked, nonconflicting completed results **observed while community opt-in
was enabled** are eligible. Later opt-in does not promote earlier results.
Preparing then copying the export is explicit; current opt-out prevents export
and clears any prepared copy in the plugin. Opt-out does not erase saved history
or undo something the player has already copied/shared externally.

No submarine name, house/FC identity/hash, local slot/registration, account,
reporter, credential, player IDs, logs or raw game strings are included. Routes,
builds, rewards and timestamps can still reveal FC activity; contribute only
information you are permitted to share. Random record IDs deduplicate repeated
exports from this retained record, **not the same FC voyage reported by another
player**. Cross-contributor collision/quality policy needs explicit Site design;
do not infer global uniqueness, truth or FC ownership from this draft.

## Bounded Site requirements

Do not implement an endpoint from this document alone. Before plugin uploads,
Site must define an authenticated, explicitly consented Gillions contract and
review it with Native Collector. Specify supported schema/version admission,
server-derived private reporter, limits, exact receipts, immutable retry identity,
update/conflict semantics, contribution opt-out/revocation, rejected/partial data,
cross-contributor dedup/quality policy, retention/history and public disclosure.
Allow missing producing build/departure, partial or voyage-level reward evidence
without inventing sector attribution. Map numeric catalogs by game version.
Notifications/page comparisons remain separate server work. No credential or
anonymous receiver is added here; ordinary HTTPS/PF/Retainer contracts are unchanged.
