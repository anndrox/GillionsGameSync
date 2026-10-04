# Testing personal observations v1 — local draft and bounded Site handoff

Current transport successor: [Testing personal transport](testing-personal-transport-v1.md).
The sections below describe the original producing local v1 format and proposal;
Testing 0.0.74 adds independently consented shared-TEST transport without changing
these payload fields or granting public sharing. Dashboard remains local-only.

Applies to Testing 0.0.69 (retains 0.0.68 observations). This is **not an activated HTTP contract**. Both
collectors retain locally in the existing Dalamud Testing configuration; explicit
PRIVATE exports prepare the proposed payloads. No new endpoint, uploader, public
FC dataset, credential or pairing scope is implemented. Ordinary sync, market v1
and Party Finder contracts remain unchanged.

## Source and patch boundary

The inspected local game catalog is `2026.09.15.0000.0000`; the installed
FFXIVClientStructs assembly is `7.56.2.9136`, Dalamud API 15. Both experiments
check that exact pair **before native access**. Other pairs preserve retention
and stop reads. Matching versions, compilation and static catalogs do not prove
live memory layout, cache ownership or freshness. No supported Hunt Bill getter
exists in the installed Dalamud service API; the collector uses its existing
FFXIVClientStructs `MobHunt` definition, not bespoke offsets or packet hooks.

Hunts read naturally loaded `MobHunt` caches every three seconds on
the existing framework loop, independently of pairing, ordinary sync and whether
a bill window is open. Admission requires loaded current PlayerState, no zoning,
a loaded/bounded Key Items container, and a positive obtained flag with a matching
MobHuntOrderType.EventItem actually present (positive quantity). This corroborates
the bill type, not the cached order's character ownership or current freshness.
No coherent positive evidence means unavailable/preserved, never no bills.
Testing79 additionally captures a narrowly bounded final-counter transition after
bill-item/flag removal: a bill positively corroborated in this character's current
session within six seconds, unchanged obtained order/item/catalog target identity,
previously incomplete, and ALL explicitly read counters exactly equal requirements.
No absent=>complete inference, counter clamping, available-board substitution,
extra completion flag, hook or request. Stale/partial/malformed/order-changed reads
are rejected. Session baselines are RAM-only, cleared on logout/character change,
zoning and local opt-out; restart history alone cannot establish this admission.
The nullable existing sourceEvidence is null when current Key Item corroboration
is absent; prior positive baseline does not become current acceptance proof.
Sync now can force one eligible read before its normal deadline, with a one-second
manual admission floor. Only ordinary scheduled reads use the three-second cadence.
Submarines retain their existing workshop events;
positive unlock/exploration getters run only when the selected planning agent
owns the visible interface in the loaded current workshop. Both require local
opt-in and framework-thread execution; submarine reads remain at most once per second. Static
catalogs are cached. No UI is opened, no mob/object table is scanned, no callback,
packet hook, game/web request, gameplay action or third-party runtime is added.
Hunt memory observation is explicitly bounded, not a per-frame native scan.

Definitions: [MobHunt](https://github.com/aers/FFXIVClientStructs/blob/6a562a1ef86b9acb22bda0726e28b0e23bb27552/FFXIVClientStructs/FFXIV/Client/Game/UI/MobHunt.cs),
[HousingManager](https://github.com/aers/FFXIVClientStructs/blob/6a562a1ef86b9acb22bda0726e28b0e23bb27552/FFXIVClientStructs/FFXIV/Client/Game/HousingManager.cs),
[Lumina sheet schemas](https://github.com/xivdev/EXDSchema/tree/f3cacf3bb7bcea2e1dcd2ce1204f8be22a6c9922).
These are technical references; compiled installed SDK/catalog evidence is also
required. Lumina provides static identities/requirements, not player ownership,
counter freshness, bill reset times, spawn sightings or FC permissions.

HuntBuddy [inspected reader](https://github.com/SheepGoMeh/HuntBuddy/blob/1de61e25b99b6473f4d2c75b4023e588016f0aa4/HuntBuddy/Plugin.cs)
is a technical reference for cache availability, not a dependency or copied code
(no repository license detected). Its per-frame native flag checks/background
native reads are not adopted. The 0.0.68 `MobHunt` subscription was insufficient:
other implementations use case-sensitive `Mobhunt` and numbered expansion windows
([inspected Wayfarer names](https://github.com/will-corrigan/Wayfarer/blob/91e09b573ec8af79d430208485019bb8e48e3bb7/Wayfarer/Modules/Hunting/MarkBillButtons.cs)).
The successor removes the UI-event dependency rather than adding more hooks.

## Hunt retained/private payload

Authoritative definitions: `HuntBills.cs`; [invented example](../examples/hunt-bills-v1.json).
Root fields are exactly `schemaVersion:1`, `collectorSchema:"hunt-bills-v1"`,
`uploadState:"local-only-no-server-contract"`,
`source:"naturally-loaded-mob-hunt-client-cache"`,
`completeness:"positive-observations-only"`,
`characterAssociation:"active-character-context-cache-ownership-unverified"`,
`resetAtUtc:null`, `resetApplicability:"unavailable"`, and `bills` (1–22).

| Fields | Source / semantics | Freshness / absence | Privacy / retention |
| --- | --- | --- | --- |
| observationId | Random 32-hex identity for admitted observation; repeated export/reload keeps it | Changed state or >=60-second refresh admits a new observation; not bill-day identity | Private retry/idempotency identity; retained |
| billTypeId | Native mark index 0–21, matching MobHuntOrderType | Only positive obtained flags; missing indices say nothing about absence | Personal cache snapshot |
| category, tier | Type sheet 1=daily/2=weekly; inspected index mapping below | Unsupported type/tier rejected | Personal snapshot; static category reference |
| orderId, eventItemId | Native obtained order row getter, validated against type OrderStart/OrderAmount; type EventItem row ID | Unknown/out-of-range order rejects that bill; never substitute available board order | Matching loaded bill item corroborates type only; cached-order ownership remains unverified |
| observedAtUtc, gameVersion, collectorVersion | Framework observation UTC, Lumina base repository version, actual assembly version | UTC only; not server receipt, reset or fresh network response | Private activity/provenance, retained |
| acceptance | Always `obtained-flag-observed-not-current-acceptance-proof` | Cache does not clear completed marks; no independent cache-owner/generation flag | No active-assignment claim |
| sourceEvidence | Additive nullable observation provenance: `loaded-key-item-and-obtained-flag-cache-unverified` for currently corroborated reads | null for older UI-only or bounded same-session final-counter observations without current Key Item evidence; never retrospectively claim current key-item evidence | Private snapshot; does not prove current cached order or ownership |
| targets[].targetIndex | Order subrow index, 0–4 | Whole bill rejected for partial/invalid targets | Snapshot |
| targetId, npcNameId | MobHuntOrder.Target -> MobHuntTarget -> BNpcName, known rows required | Unknown IDs rejected | Numeric reference join keys; no mob sighting |
| mapId, placeNameId, fateId | MobHuntTarget sheet reference IDs, verbatim | 0 is the sheet's absent reference sentinel, not a coordinate or empty personal state | Static references; no live positions |
| requiredKills, targetType, rank | Order subrow requirements/classification | RequiredKills >0; values are reference requirements, not progression proof | Personal target snapshot |
| observedKills, completed | Native CurrentKills for matching subrow; completed means counter == requirement | Negative/above-required rejects bill; derived completion is cache observation, not current bill acceptance | Private progress, retained |
| resetAtUtc, resetApplicability | Unsupported, explicitly null/unavailable | No inferred daily/weekly reset or midnight-generated bill | No reset deletion |

Inspected category indices: ARR daily 0/weekly 4; HW daily 1–3/weekly 5;
SB daily 6–8/weekly 9; ShB 10–12/13; EW 14–16/17; DT 18–20/21.
Daily tiers follow those three indices; ARR daily and each weekly use tier 1.
Expansion label can be resolved from versioned reference catalogs, not a new
gameplay scan. No supported current-sighting feed or B-rank coordinates are added.

Local `HuntBillRetention` v1 holds default-OFF LocalRetentionEnabled, up to 16
hashed own-character partitions / 22 latest positive bill observations each,
below 256 KiB, and CapacityReached. Raw own Content ID is used transiently for
SHA-256 scoping, not stored/exported; a hash is not anonymization. Unknown format
or oversized/malformed state is preserved and collection/export fail closed.
Zero observed bills, cleared flags, unknown IDs, logout and missing UI never
clear prior bills. A changed order replaces that category's latest positive
snapshot; no historical bill-counting or reset inference. Older timestamps cannot
overwrite newer observations. Semantically unchanged events save at most once
per minute. At capacity, keep all admitted partitions; reject a new character and
show a sticky warning, while bounded existing-character updates remain possible.
These snapshots are not an unsent contribution journal; there are no server ACKs.

PRIVATE export requires retention ON and the matching active character partition.
It excludes character/account/name/hash, device/authentication, chat and diagnostics.
The active loaded character and matching bill item are context, **not independent proof the
global cache belongs to that character**. Live transition validation remains
required. Site must not present these provisional records as a complete current
assignment, no bills, or a verified next-target recommendation.

## Submarine retained/private payload

Uses the existing [voyage retention](submarine-voyages-v1.md), not another file or
database. Existing completed-result history, dedup IDs, community consent,
producing/result-time build separation and sanitized community export remain.
Optional additive snapshot fields: LocalWorkshopKey, UnlockedSectorIds,
ExploredSectorIds. Older snapshots retain nulls and remain valid. Snapshot reserve
is now 4096 bytes; 400*8192 + 32*4096 + bounded metadata stays below 4 MiB.
Unsupported downgrades can drop added fields; preserve configuration and use
forward fixes, not automatic rollback.

PRIVATE root: `schemaVersion:1`, `collectorSchema:"submarine-personal-v1"`,
`uploadState:"local-only-no-server-contract"`, `workshopScope` (64 lowercase hex),
`scopeEvidence:"observed-workshop-not-fc-membership-proof"`,
`completeness:"positive-observations-only"`, `slots` (1–4).
[Invented example](../examples/submarine-personal-v1.json).

| Fields | Source / semantics | Freshness / absence | Privacy / retention |
| --- | --- | --- | --- |
| workshopScope | SHA-256 of `workshop:` + current house ID; raw ID never saved/exported | Loaded current workshop only; not FC ID, membership, ownership or authorization proof | Sensitive stable scope, PRIVATE only |
| slot, name, registeredAtUnix | Verified native slot/name/registration | Replacement registration distinct from voyage departure; name local UTF-8 validated | Private FC activity/detail |
| build.rank, build.parts | Native rank + Hull/Stern/Bow/Bridge SubmarinePart row IDs, catalog validated | Observed current build, NOT producing voyage build; unloaded rejected | Private snapshot |
| build.stats | Native five base/bonus stats and log speed | Kept as observed; totals may be base+bonus, do not add log speed again | Private snapshot |
| currentExperience, nextRankExperience | Native observed EXP fields | Client-cache values, not fabricated level-up timing | Private progression |
| observedAtUtc, gameVersion, collectorVersion | Snapshot observation UTC and actual versions | Last admitted read; same semantics refresh no more than once/minute | Activity/provenance |
| expectedReturnAtUtc | Positive native ReturnTime -> UTC | Null when cleared/unavailable; NOT actual completion | Private scheduling |
| departureAtUtc | Always null | RegisterTime is not departure; no duration subtraction | Unsupported |
| voyageState | `expected-in-flight`, `expected-return-due-not-observed-completion`, or `unavailable` based on return vs observation time | Does not advance solely because the wall clock changes; zero is not idle/no voyage | Observation-time inference, explicit |
| orderedSectorIds | CurrentExplorationPoints; 1–5 ordered known unique sectors | Null for unavailable/cleared route, not empty active voyage | Private current route |
| plannedSectorIds | SelectedPoints only when agent owns current addon | Null when unavailable; [] means that verified planning agent selected no points, not an empty voyage | Separate UI plan snapshot |
| unlockedSectorIds, exploredSectorIds | Existing read-only SDK getters during visible selected planning interface, positive known sector IDs only | Null for no positive evidence; never []/all locked. Retained positive union survives missing flags | Private progression, NOT complete unlock set |
| sectorCompleteness, sectorFreshness | `unavailable` or `positive-observations-only`; always `retained-positive-history-not-current-lock-state` freshness | Positive history can predate current build snapshot; not a current lock-set assertion | No eligibility/planner guarantee |

Slots not observed remain unknown, never empty. Latest retained observation per
workshop/slot is exported; old registrations remain retained and voyage history
is not overwritten. Export requires current-session verified workshop scope;
logout/territory change, unsupported build or failed reads clear prepared private
data/scope. Personal JSON includes names/scope/registration; it is **not** the
existing sanitized community result export. Both are explicit manual copies;
community opt-in is neither required nor granted by private export.

## Exact bounded Site Operations handoff (proposal, not activated contract)

1. Implement TEST-only private admission for proposed resource types `hunt_bills`
   and `submarine_personal` through existing authenticated
   `POST /api/game-sync/sync` envelope `{resourceType,nonce,payload}`. Payloads
   above/fixtures are the exact local draft; agree activation with Native Collector
   before wiring. No anonymous receiver, public FC aggregation or production path.
2. Keep existing device authentication, Testing product/enrollment, account access
   and origin-bound HTTPS. Require explicit per-feature personal-sync permission
   and versioned compatibility acknowledgement; absence must suppress sends,
   not affect ordinary sync. Do not auto-expand existing Stable devices/scopes.
   Current plugin sends **no new resources/capability header**; transport wiring
   is the next joint step after compatible TEST intake is ready.
3. Derive account/device/paired-character ownership from authentication, never
   accept reporter/account IDs from payloads. Hunt future capture/queue/export must
   use the same verified active character/generation; do not send retained other
   character partitions. Isolate workshop scopes under the reporting account;
   hash is not FC permission proof or cross-account dedup authority.
4. Strict schema/collector version, UTC/provenance, enum/ID/array/count/byte
   validation. Hunt cap 64 KiB, 22 unique types / 5 unique target indices each;
   submarine cap 64 KiB, <=4 unique slots, <=5 route sectors, positive sector
   arrays <=255 unique IDs, valid bounded name/parts/rank/stats. Reject unknown
   versions/shape, excess/depth, malformed or cross-scope requests. Cross-check
   IDs through game-version catalogs; don't convert absent/null to zero.
5. Existing receipt shape: `{ok:true,snapshotId,receivedAt,unchanged}`; nonce is
   immutable per prepared snapshot/retry, scoped to authenticated account/device/
   character/resource. Duplicate same nonce/body -> same logical receipt;
   nonce/body mismatch -> explicit conflict. Newer semantic snapshots update
   latest state; older source times never replace newer. Keep unavailable/partial
   observations from deleting prior trusted data. Persist provisional observations
   separately from any verified-current UI claims and retain freshness labels.
6. Private Web/API reads must require account ownership, prevent cross-FC/account
   disclosure and opt-out respected. Hunts can replace *synthetic observed cache
   targets/counters* with clearly provisional real observations, not automatically
   complete/current assignments or reset promises. Submarines can replace observed
   name/rank/components/stats/route/expected return/positive progression, but not
   missing slots, complete unlocks, voyage departure, permissions or planner truth.
7. Keep static sectors/component/reward/route-distance models and multiple B-rank
   possible positions in versioned shared reference catalogs with provenance.
   Possible positions aren't sightings. No hunt radar, new market demand or planner
   architecture. Completed submarine community results remain a separate future
   consent/security contract; don't feed private payloads into that dataset.
8. Provide a trusted HTTPS TEST origin (current HTTP IP:3301 is rejected), disposable
   account/fixtures, negative auth/version/privacy/idempotency tests and capability
   acknowledgement for joint validation. No TLS relaxation, production pairing
   switch or credential relay. Native Collector then wires/testing-publishes a
   bounded successor, after review and live cache/character/workshop validation.

Site source audited: `4daedf6bfb4c40c252785b8c96ab24b3250a9f95`.
Its RESOURCE_SCOPES does not include either proposed resource; previews declare
personal state unsupported. No E/F/G end-to-end acceptance/persistence/read claim
can be made. This objective modifies neither Site nor a server.
