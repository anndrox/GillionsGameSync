# Dashboard facts v1 — private local proposal, no server contract

Status: Testing 0.0.71 prepared experiment. This is **not** an accepted HTTP
resource or production contract. No endpoint, capability, anonymous receiver or
automatic upload is added. Site owns eventual authorization/intake and Dashboard
configuration. Existing HTTPS pairing and ordinary sync are unchanged.

## Native admission

Exact game `2026.09.15.0000.0000` and FFXIVClientStructs `7.56.2.9136` before
all signatures/pointers, including player identity. Framework thread, enabled
retention, logged-in own PlayerState loaded with nonzero ContentId, and no zoning
are required. Own ContentId is transient only, domain-separated SHA256 partition
key local only; it is never exported. UI-gated sources must be visibly naturally
open; roulette and Custom Delivery also require active owning agent AddonId.
Custom Delivery needs agent validity/init/update and manager initialized/not
loading, unique ENpcResident-to-SatisfactionNpc match and coherent limits.
Challenge Log requires native Loaded state. Currency inventory and PvP profile
require their explicit loaded flags. Held Wondrous Tails requires all 16 catalog
objectives with valid statuses and coherent future expiration.

Cadence is one source group / five seconds on the existing Plugin scheduler,
alternating queued managed UI notices and fair round-robin. Seven groups: roulette,
deliveries, challenges, currency, journal, timers, PvP. With no notices every
source gets a turn within 35 seconds; sustained notices cannot starve periodic
sources (at most 70 seconds between round-robin turns). No native reads occur in
UI draw or event notice handlers. Static roulette/challenge/current currency
catalogs are bounded/cached. Settings display contains frozen managed strings;
private copied diagnostics include no per-system activity values or identity.

## Finite retained model

`DashboardRetention` version 1, `localRetentionEnabled=false`, `capacityReached`,
and character partitions. Each has `localCharacterKey` and latest observations.
Each observation has:

- `system`: finite allowlist below; no arbitrary user task/widget IDs.
- `scopeId`: 1–12 SatisfactionNpc ID for selected client; 0 for all other systems.
- `observedAtUtc`: actual UTC cache read time, not reward completion time.
- `nextAtUtc`: verified positive future native boundary or null, never computed
  from local midnight/public reset schedules. Meaning differs by system below.
- `gameVersion`, `nativeVersion`, `collectorVersion`: producing versions retained
  with the facts, not substituted with versions from a later plugin restart.
- `values`: 1–104 distinct finite entries; fields `id`, `relatedId`, nullable
  `progress`, `limit`, `remaining`, `completed`, `available`.

Zero is meaningful only in an admitted loaded source. Null means unknown/not
represented, not false/zero. `completed` never means a Site task is complete.
`available` is deliberately not populated for roulette or allowance eligibility.

| System | Values and meaning | Native next boundary |
| --- | --- | --- |
| roulette-reward | `id=ContentRoulette.RowId`; completed native reward flag; eligibility null; all numeric fields null | Unknown; cadence daily reference only |
| custom-deliveries-global | id0 used `progress`, limit12, remaining `12-used`; no completion/eligibility | Manager's weekly reset |
| custom-deliveries-client | scope SatisfactionNpc; id0 weekly used/limit/remaining; id1 current-rank satisfaction/max (not lifetime total); id2 rank/max5 | Manager's weekly reset; does NOT reset permanent rank |
| challenge-log | ContentsNote ID1–104 present in catalog with positive requirement; completion only, numeric progress unknown; Site joins static category/requirement | Cached next challenge reset if coherent; otherwise null |
| weekly-tomestones | id current capped Item.RowId; earned `progress`, native cap `limit`, remaining subtraction; never balance | Unknown |
| wondrous-tails | id0 placed stickers/limit9, id1 Second Chance/limit9, available true proves only positive held journal context; ids2–17 are slots0–15, relatedId WeeklyBingoOrderData, progress native status0/1/2 and completed=1 or2 | Journal expiration, NOT weekly reset/turn-in timestamp |
| leve-allowance | id0 remaining/limit100; progress/completion null | Native next regeneration |
| society-allowance | id0 remaining shared allowance/limit12; no per-society quest completion | Unknown |
| map-availability | id0, all value fields null | Native next availability only; past timestamp not proof of available/harvested |
| squadron-mission / squadron-training | id0, all value fields null | Native expected finish; not claim, success, daily allowance or completed-result proof |
| frontline-weekly | ids0 matches,1 first,2 second,3 third; progress only; sum placements <= matches | Unknown |
| rival-wings-weekly | ids0 matches,1 wins; progress only; wins <= matches | Unknown |

Custom counters are coherent integers within caps; current-rank satisfaction is
0..max, max1..65535; rank1..5. If a max-rank client has native satisfaction max0,
both satisfaction fields are unknown/null, not a fabricated zero progress bar;
weekly allowance and rank remain independently observable. Invalid client detail
does not suppress a separately valid global allowance observation.
Tomestone cap1..10000 and earned0..cap, native cap
must equal the uniquely selected capped catalog item. PvP counters0..10000 (a
defensive sanity bound, not a weekly goal). Journal order IDs1..255 must exist in
installed catalog, statuses0..2; all 18 value IDs required. All next timestamps
UTC, strictly later than observation, at most 15 days; runtime uses tighter bounds
(weekly reset <=8 days; leve <=1 day; map/squadron <=2 days). Invalid groups fail
closed. Unsupported Doman, duty lockout, Fashion, Carnivale, Faux and other system
names cannot be admitted. Missing sources never emit an empty replacement.

## Identity, updates, retention, compatibility

Key: local hashed own character + `system:scopeId`. Whole-system snapshot is the
unit of admission, not a daily event history. A newer valid snapshot replaces
that same identity; successive journals replace the prior journal, selected
clients update only their own scope. Duplicate/backdated/equal-time conflicting
observations do not replace retained state. Equivalent reads refresh persisted
observation time at most once/minute; semantic changes save promptly.

At most **16 characters, 24 latest groups per character, 384 KiB serialized
retention envelope**. Admission checks actual candidate envelope atomically,
reserving 64 bytes for overflow reporting. Capacity preserves prior records,
reports CAPACITY and refuses any admission/replacement that would exceed bounds.
Existing identities can update if they fit. No silent pruning/eviction. This is
not an unsent-contribution queue and has no server ACK/delete behavior. No upload
exists; local opt-out stops reads and private export, preserves records, and
does not retract an already copied clipboard payload.

Unknown schema, malformed bounds, duplicate keys or unsupported future members
disable collection/export, preserve parsed retained data, and never normalize
to an empty supported store. Newtonsoft extension data preserves unknown members
at envelope/character/observation/value levels through this candidate's normal
save/load. It grants no permission to export those members. A pre-0.0.71 binary
does not know this new property and cannot promise preservation on downgrade;
do not downgrade/delete configuration. No Hunt/submarine data transformation or
ordinary configuration converter behavior is changed.

## Freshness and reset limits

All observations persist after UI closes, logout, character changes, plugin
restart, feature disablement or incompatible patch. Current-session evidence is
in memory only and clears on login/logout/territory/key changes. An unavailable
source never removes retained facts. Display/export marks a retained row STALE
when no matching session observation exists, age exceeds two minutes, clock is
before observation, or the next native boundary is reached. An elapsed boundary
does not fabricate a reset/new zero. A new coherent loaded response may replace
with actual zero, but response generation/reset ownership still needs live proof.
OBSERVED means cache read, not complete/eligible/current-week verified.

## Private export and Site Operations handoff

`dashboard-facts-v1` export has schemaVersion1, local-only-no-server-contract,
private-personal-activity, generated UTC, explicit unverified character/cache
association and **automaticChecklistCompletion=not-authorized-by-this-experimental-export**.
Rows add finite `source`, `cadence`, computed `freshness`, and
`resetApplicability=unavailable` or native-next-boundary-only-not-server-response-proof.
No ContentId, partition hash, character/account/FC name, pairing secret,
credential, raw log/chat, theme solution, unrelated equipment or Dashboard ID.
Copy requires enabled supported retention and fresh active character context,
rechecked on framework thread at copy time. An unsupported patch refuses export.
[All-system example](../examples/dashboard-facts-v1.json) is generated from
synthetic fixtures (`synthetic-game`, `synthetic-sdk`), never player data.

Bounded handoff to Site Operations:

1. Reuse existing supported achievement/collection/character/normal-quest/
   reputation/Shared-FATE/currency-balance snapshots and compatible Retainer
   venture observations. Hunt/submarine remain their existing separate local
   proposals; do not create duplicate collectors.
2. Decide/accept an authenticated Testing device, selected-character scoped
   private facts intake contract. The name above is a schema proposal, NOT a new
   resource sent by the plugin. No anonymous/public contribution. New intake must
   ignore omission as unknown, validate finite types/native versions/bounds,
   reject arbitrary task configuration, deduplicate by system/scope/character and
   update monotonically without replacing current data with older observations.
3. Agree source ownership/freshness/reset-window evidence and compatibility
   acknowledgment with Native Collector before adding a transport. Site cannot
   authorize from client claims alone; require existing server-controlled Testing
   enrollment and applicable user consent. No new plugin endpoint in this task.
4. Display/progress now: bounded counters, allowances, rank, journal statuses and
   expected times with explicit unknown/stale/currentness limitations. No automatic
   dated checklist completion from this provisional export. After live ownership
   and matching reset-window proof: roulette reward, Challenge Log flags, selected
   Custom Delivery counts and journal task statuses are candidate factual inputs,
   not direct task updates. Rank/balance/timestamps alone remain display-only.
5. Doman weekly donations, general duty/loot lockouts, Fashion reward/participation,
   Carnivale weekly targets, Faux, lottery and unsupported systems remain manual.
   See the [full audit](../dashboard-capability-audit.md). Public static schedules
   do not make a stale private observation fresh.
6. Implement Site's personal layout/tasks/recurrence/manual completion separately.
   No Site repository/deployment/schema changes were performed by this objective.
