# Testing private transport v1

Canonical plugin transport behavior, Testing successor 0.0.74. Local collector
fields remain defined by [personal observations](personal-observations-v1.md).
The exact Site contract is `docs/contracts/game-sync-personal-observations-v1.md`
at Site configuration source `9018a32b3101a50f8e4219b4f632f73f8934ec69`, baked
application `e6b30b65fdc9b83b4304a939f2a0305654fa1215`, TEST schema 0019. The legacy
proposed `uploadState=local-only-no-server-contract` is deliberately retained:
Site explicitly requires it as producer-format metadata, not transport status.

## Consent, origin and identity

Only `https://test.gillions.app/api/game-sync/sync`. No redirects, certificate
bypass, direct IP, production endpoint or HTTP fallback. Existing paired-device
Bearer authentication and `{resourceType,nonce,payload}` envelope; identity comes
from authenticated Site account/device/linked character, never added to payload.
Do not automatically change the server of an existing pairing. Re-pair at TEST
through its normal management page, select Testing and explicitly grant each
private resource. Old pairing permissions never silently expand.

`SyncPersonalHunts` and `SyncPersonalSubmarines` default OFF, independently of
local retention, Automatic sync, Market and public Party Finder contribution.
New pairing resets these local upload switches. Retention must also be ON.
OFF cancels that feature's queued dispatch/in-flight work where possible, stops
future sends, and preserves local/pending state. It cannot retract a request
already accepted by Site. No automatic public/community voyage sharing.

Presence at TEST adds `X-Gillions-Personal-Contract: personal-observations-v1`.
Exact acknowledgment requires `acceptedClientProduct=GillionsGameSyncTest`,
personalObservations contractVersion 1, the exact HTTPS endpoint and a unique
resource entry with schemaVersion 1, exact collectorSchema/capability and 65536
maxPayloadBytes. Absence/incompatibility suppresses private sends only. Personal
submissions carry the same contract header and recheck permission/origin before
dispatch; Site rechecks durable device/account/character permission each time.

| Resource | Collector schema | Server permission | Capability |
| --- | --- | --- | --- |
| hunt_bills | hunt-bills-v1 | server:game-sync:personal:hunt-bills:v1 | hunt_bills_v1 |
| submarine_personal | submarine-personal-v1 | server:game-sync:personal:submarine-personal:v1 | submarine_personal_v1 |

Hunts export only the active own-character hashed partition. Missing observations
do not send an empty replacement. Submarines transport only a verified, positive,
current-character workshop batch in this session; old unassociated shared local
workshop snapshots are not reassigned. After reload/logout/territory transition,
wait for another naturally opened workshop interface. Existing voyage history,
producing/result-time build distinction and community consent/export remain.
Numeric route/sector arrays use integer JSON arrays (not byte[] base64); retained
configuration byte arrays and schema are unchanged.

## Prepared state and failures

Existing Dalamud configuration stores private `PersonalSync` schema 1: at most
16 owner/resource entries and 1,152 KiB serialized UTF-8. OwnerKey is a local
domain-derived hash of paired generation and own-character partition, never
transmitted. Each entry contains resource, immutable nonce/body/hash and ACK/
blocked status, no credential. One entry per owner/resource; acknowledged entries
can be replaced by a changed successor. Unacknowledged entries never lose their
nonce/body to a newer observation. Capacity/unknown format/corruption fail closed
without eviction or erasure; other features continue. This is a bounded prepared
latest-state set, not an unbounded offline history journal.

Store before dispatch. Same immutable snapshot retries after timeout/reload with
the same nonce and payload. A valid success receipt requires ok true, nonzero UUID
snapshotId, receivedAt and boolean unchanged. ACK stops repeated sends until local
observation changes. Network/invalid receipt/429/503 use 60–900 second exponential
backoff, one private request in flight. 400/401/403/404/409/413/415 stop the affected
prepared snapshot. Correct permission/contract/input and use a corrected forward
candidate/fresh pairing as appropriate; do not retry unchanged terminal input.
Pending records for previous pairing generations remain inactive/preserved.

Testing79: Hunt successor comparison ignores only bill observationId/observedAtUtc
refreshes. Exact prepared payload hash/nonce remains immutable, including after
restart. Counts, order, provenance and every other field still participate. Thus
unchanged periodic reads/provenance-time refreshes do not send network snapshots.
Retained semantic changes immediately bypass the routine five-second preparation
cadence, never failure backoff. After a receipt, the next framework turn can drain
a changed successor or the other enabled resource. A whole private HTTP/receipt
flight is bounded to30s; timeout retains the same nonce/body with60–900s backoff.
Sync now requests a fresh eligible Hunt read and prompts independently enabled,
compatible personal resources, even when ordinary sync is already in flight.
Submarine state still requires naturally observed current-session workshop data;
no manual action opens/requests an interface or reassigns historical workshops.
OFF, exact TEST origin, pairing/character scope and capability checks remain.
Aggregate stages distinguish raw counter/gate change, retained semantic change,
new preparation and accepted receipt; no payload/IDs/authentication are logged.

Site performs nonce conflict and monotonic observation admission: old observations
cannot replace newer, equal-time semantic conflict fails closed, omission never
deletes. Receipt identity is device/character/resource/nonce. Submarine positive
sector history union remains account/character/workshop/registration scoped.

Private names/scope/progress/activity are personal data. Never share configuration,
tokens/codes or raw private exports as ordinary diagnostics. Main-window diagnostics
record only resource, HTTP result and valid-receipt status. Local views still show
collector limitations; the main window is the separate transport status authority.

## Dashboard readiness and bounded Site handoff

Current Site has no activated Dashboard resource. Its `dashboard-facts.md` audit
has fixtures/read models only and prohibits automatic task completion. No Dashboard
HTTP transport is introduced. Hunts/Submarines do not depend on this missing lane.

Site Operations: use [dashboard-facts-v1](dashboard-facts-v1.md), `DashboardFacts.cs`
and `docs/examples/dashboard-facts-v1.json` as the existing Native model, not a new
endpoint. Before wiring, provide the exact resourceType, schemaVersion,
collectorSchema, persisted Testing permission, capability/presence acknowledgment,
payload limits, validation and authenticated account/paired-character scope using
the existing sync/nonce/receipt model. Specify monotonic per-system/scope updates,
omission-as-unknown, stale/unavailable/unsupported handling and retained/reset
ownership evidence. Agree retention bounds and supported producer versions.

Preserve typed boolean/progress/allowance/journal/time facts and nullable values:
global Custom Deliveries used/12/shared remaining; client residual capacity is not
deliverability; ambiguous agent RemainingAllowances stays unsupported; Doman
accepting is not weekly completion; Wondrous Tails expiry is not weekly reset;
reference calendar/timestamp alone does not prove cache ownership/current period.
The local cap is 16 characters, 24 latest system/scope groups each, 384 KiB; proposed
export cap 64 KiB. Site owns matching/manual overrides/tasks/widgets/layout/recurrence;
do not send that configuration to Native. Transport acceptance does not itself
authorize automatic checklist completion. Return an exact activated TEST contract
and fixtures; no Stable or production activation is requested.
