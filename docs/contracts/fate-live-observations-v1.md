# Testing live FATE observations

Testing candidate 0.0.85 adds a read-only, RAM-only local diagnostic collector.
Remote contributions remain disabled. Site contract revision 2 reserves intake
but does not activate an endpoint, provider or consent policy. Publication requires
real-client positive observation and cost acceptance, not fixture success.

## Maintained sources and supported context

The collector uses Dalamud `IFateTable`/`IFate`, `IClientState`, `IPlayerState` and
`ICondition`, with Lumina typed World, TerritoryType and Fate references. It copies
primitives on the framework thread; it never retains IFate wrappers or addresses,
reads player coordinates, parses localized text, enumerates actors or requests
game state. Existing plugins are research references, not runtime dependencies.

Gillions-specific logic supplies the gaps not covered by the maintained APIs:
settled-context validation, positive-only admission, bounded occurrence support,
contract serialization, source fencing and local cost measurements. The reference
APIs do not implement Gillions policy, batching or receipts.

Only public World rows and ordinary Overworld territories with no Content Finder
condition are supported. PvP, duties, group pose, zoning, logout and world-visit
transitions fail closed. Eureka, Bozja and special exploratory modes remain outside
scope. CurrentWorld, territory and typed public instance ordinal must agree before
and after enumeration; two separately scheduled stable checks precede the first
read. Zero ordinal means noninstanced only in that settled supported context.
Login/logout, ZoneInit, territory, map, instance and pairing/session invalidation
discard unsent evidence and occurrence support.

The initial exact local source fence is:

| Source | Accepted version or revision |
| --- | --- |
| Game and referenceGameVersion | 2026.09.15.0000.0000 |
| Dalamud API | 15 |
| Dalamud | 15.0.3.6, b666d821a47306fb447c60155b5d99377f91a5ee |
| FFXIVClientStructs | 7.56.2.9136, 313161e448e335adddd928f8a0212b2c328b5659 |
| Lumina | 7.7.0 |
| Lumina.Excel | 7.5.1 |

Metadata comes from the actual installed assemblies and game repository. Unsupported
or unavailable metadata disables FATE reads without disabling other Game Sync.
This local fence does not grant future server admission.

## Fields and positive evidence

Required row keys are fateId, worldId, territoryId, instance, state and observedAt.
FateId is a nonzero UInt16 matching a valid compatible definition. World and
territory are positive typed row IDs. Instance is `{kind:"public_instance",
number:<positive UInt32>}` or `{kind:"noninstanced",number:0}`. State pairs are
preparing/3, running/4, ending/5, ended/7 and failed/8; unknown raw values reject
the row. Observation time is collector UTC ISO-Z, never receipt time.

Nullable optional keys are progressPercent (0..100), bonus (event Boolean),
level/maxLevel (positive ordered pair, excluding 1/255), positionWorld (finite
event x/y/z), radiusWorld (finite positive event radius) and timing. Timing pairs
a positive plausible Int32 startTimeEpoch with durationSeconds 1..32767; starts
more than five seconds ahead or 32767 seconds behind the collector clock are
unusable. Invalid optional detail is null, not fabricated. Geometry describes
the event, not the player; progress/bonus do not establish player credit/rewards.

Missing rows or empty/unavailable tables never imply inactive, removed, completed
or failed. No empty replacement or negative tombstone is prepared. Progress 100,
estimated end and disappearance are not completion evidence. TimeRemaining is not
submitted. Ended/failed rows require previously observed support for the same valid
timed occurrence in this epoch; at most 128 support entries expire after 120 seconds.
New occurrences never inherit progress or terminal state. No-start preparing or
running rows remain provisional with no durable occurrence identity.

Site owns the occurrence discriminator: referenceCompatibilityEpoch + worldId +
territoryId + instance.kind/number + fateId + startTimeEpoch. It is not a guaranteed
global spawn UUID. Site must keep worlds, ordinals and starts separate, reject
older overwrites, preserve known fields against null and classify tied conflicts
as ambiguous. Native supplies no static names, reference maps or public reporter
identity; Site owns compatible catalog enrichment and field merging.

## RAM limits and diagnostic session

Open `/gillionsfates` or Advanced FATE diagnostics under Testing diagnostics, then
start the temporary local measurement session. It is stopped by default, is not
persisted across reload and grants no remote consent. Stop discards observations.
No permanent configuration checkbox or observation history is added.

Reads are independently admitted every five monotonic seconds, not on the command,
Hunt or travel schedule. Context changes and stop/start cannot bypass this cadence.
At most 64 table entries are inspected; a larger table fails this bounded collector
closed rather than claiming complete coverage. Semantic changes prepare a new
batch; unchanged positive data renews after ten seconds. One replaceable prepared
batch has a fresh UUID and immutable body, expires beyond 30 seconds, and is cleared
on context invalidation. Retries keep the same body/time/ID. A fresh read, not a
retry, may renew observation time.

The 240-sample RAM measurement ring records read/copy and preparation durations,
current-thread allocations, table/accepted counts and semantic change status.
Diagnostics report median/p95/max and distinguish these stages from whole-frame
performance. Copying is manual; public world/territory/time can reveal presence.
Share only with a trusted diagnostic recipient. No automatic diagnostic upload.

## Exact envelope and dormant transport

Reserved endpoint: `POST https://test.gillions.app/api/game-sync/fates/contribute`.
Capability and collectorSchema: `fate-live-observations-v1`; schemaVersion: 1;
coverage: `positive_only`. Envelope keys are exactly schemaVersion,
collectorSchema, coverage, source, batchId and observations. Batch limits are
1..64 rows and 128 KiB uncompressed application/json.

Source keys are exactly family (`dalamud-fate-table`), collectorVersion,
gameVersion, dalamudApiLevel, dalamudVersion, dalamudRevision,
clientStructsVersion, clientStructsRevision, luminaVersion, excelVersion and
referenceGameVersion. They describe the actual producer, not authenticated truth,
and must not appear as a public reporter fingerprint.

Excluded payload fields include account/device/character/content IDs or names,
HomeWorld, player coordinates, participation, quests, inventory, party/FC,
credentials, actor/pointer values and private Hunt/travel state. Local character
identity exists only as an epoch invalidation key and is not serialized. Future
paired Bearer transport identifies the contributor privately; de-identification
is not anonymity.

The internal admission helper requires the current pairing generation, current
epoch, active account, enabled Testing device, exact TEST HTTPS origin, exact
schema/capability/source tuple and independent `fate_public_observations` revision
1 grant with valid expiry and no revocation. Missing or mismatched gates deny
request creation. Ordinary sync, PF, Market or Site login cannot grant consent.
Disabling FATE sends must cancel its RAM batch without changing other preferences.

**Activation dependency:** Site revision 2 does not define the exact authenticated
capability/policy discovery response. Native has no live grant parser, policy
installation or FATE HTTP sender. Those remain unwired until the exact discovery,
revocation/session binding and admission contract is agreed. The request/receipt
helpers are tested preparation only; a newly reachable endpoint alone cannot
activate this build. No contribution requests are made while waiting.

Future sender requirements are at least five seconds between attempted requests,
bounded backoff, no redirects, normal hostname/TLS verification and the existing
paired Bearer channel. Account budget is shared 12/minute, burst 2; Site global cap
is 600/minute. Same batch ID/body is duplicate-safe; changed body needs a new UUID.
Never renew observedAt on retry. Re-observe expired data instead of replaying it.

Success receipt keys are exactly ok=true, schemaVersion=1, batchId, receivedAt,
acceptedCount, duplicateCount, rejected[{index,code}] and retryAfterSeconds=5.
Validate batch binding, time, counts and unique bounded rejection indices. Generic
200 is not an acknowledgement or admission. 401/403/404/422 suspend FATE only;
400/409/413/415 require correction; 429/503 and other transient 5xx use exponential
backoff (5..640 seconds) respecting a longer valid Retry-After. Queued data still
expires at 30 seconds. No production, plaintext or direct-IP fallback is allowed.

Site clock admission is at most five seconds future/thirty seconds old. Fresh
through observedAt+30 seconds, stale until +120, then no recent observation.
Estimated timer expiry is not a terminal event. Live acceptance must establish
actual cost, cadence margin and command responsiveness before publication.

## Site activation handoff

Site must implement the exact discovery response and explicit independent policy,
source/reference allowlist, authenticated Testing-only intake, private contributor
custody, budgets, bounded receipts, conflicting observation handling, expiry and
de-identified read model. Unknown source, field, mode, world, instance or policy
fails closed. Public responses exclude producer fingerprints and private metadata.
Hunt/travel cannot be joined to the public observation stream.

Native then needs a bounded follow-up wiring the agreed discovery/policy lifecycle
and sender to these helpers, with joint auth/revocation/retry tests. No Site
activation, public alert, Watch/Tracked FATE control or static FATE UI change is
part of this candidate. Exact accepted source/package identities and measured
runtime results accompany the release handoff after owner runtime acceptance.
