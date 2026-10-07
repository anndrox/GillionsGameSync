# Hunt bill item coverage

Testing83 adds a private, latest-only observation of whether each supported Hunt
bill Key Item is held. It does not identify the currently owned order or a new
acquisition. Historical positive progress remains separate and unchanged.
This is the exact Native producer proposal for Site Operations; current Site
does not yet admit this version. Publishing Native does not activate Site intake.

## States and coverage domain

The domain is the authenticated paired character plus
`domainKind:currently-held-item-backed-bills` and exact native `billTypeId`.
Every supported type has an explicit state; omitted domains prove nothing.

| State | Meaning | Permitted routing effect |
| --- | --- | --- |
| absent_confirmed | Mapped item absent from a fresh complete Key Items snapshot | Temporarily exclude historical selections in that exact character/type domain |
| present_unresolved | Mapped item present, owned order/acquisition unsupported | No individual-order supersession; invalidate previous absence authority |
| unavailable | Complete/fresh/initialized source not established | No supersession; invalidate previous absence authority |

Absence does not mean completion, reset, expiry, order replacement or new cycle.
Never rewrite a retained counter or permanently tombstone a monster. Reappearance
must not inherit an absent verdict or reassign historical maximum kills to a new
acquisition. Typed LogMessage4411/final-counter admission remains independent.

## Complete snapshot requirements

The existing three-second Hunt observation schedule runs on the framework thread;
manual Sync now retains its existing one-second admission floor. No new game
request, interface action, object/mob scan or location read is introduced.

Before native access require the exact supported catalog/SDK pair:
game `2026.09.15.0000.0000`, FFXIVClientStructs `7.56.2.9136`.
Require logged-in, loaded PlayerState with nonzero own ContentId and no zoning.
Bind that character before and after the read. Login/logout, character change,
paired-generation change and territory/zoning transitions invalidate RAM coverage.

Use InventoryManager's KeyItems container (2004), not cached Hunt flags. Require
IsLoaded, non-null Items, reported size1..256, correct Type and typed GetSize
agreement. Copy every reported slot twice on the same framework turn; headers,
pointer and slot facts must agree. Each slot must have its expected index and
container and no symbolic representation. ItemId/quantity both zero or both
positive remain the ordinary valid shapes. A zero-ID slot with positive residual
quantity is accepted as empty only with a separate same-read per-slot native
proof: IsEmpty=true, GetBaseItemId=0 and GetQuantity matching the raw quantity,
each repeated consistently while the slot's raw fields remain unchanged. Require
non-null typed virtual table/getters; at most8 such exceptions per complete
snapshot. No aggregate diagnostic or missing getter establishes this proof.
Negative quantity, positive-ID/zero-quantity, inconsistent getters, symbolic or
misidentified slots still fail closed. The proof is ephemeral and never part of
the payload. Every positive item must resolve in EventItem. Reject a read whose
preparation exceeds100ms. Require all22 unique catalog type/item mappings and
valid native classification/range, resolving each mapped EventItem. Partial,
malformed, changed or unsupported data cannot produce an absence fact.

Snapshot UTC and monotonic age are bounded to15s, including at dispatch. Time is
freshness only, never a Hunt reset/cycle oracle. A sample observed in the future
or after a clock rollback is not current. UI observations are timestamped and
expire; unavailable source produces explicit unavailable states when the current
character/catalog remain safely bound. When no character binding is possible,
send nothing; omission does not authorize anything.

## Catalog mapping

Mapping comes generically from installed Lumina MobHuntOrderType.EventItem,
OrderStart/OrderAmount and EventItem; these static definitions are not ownership.

| billTypeId | Key Item | Order range | Native type |
| --- | --- | --- | --- |
| 0 | 2001361 | 1–50 | 1 |
| 1 | 2001700 | 68–77 | 1 |
| 2 | 2001701 | 78–87 | 1 |
| 3 | 2001702 | 88–105 | 1 |
| 4 | 2001362 | 51–67 | 2 |
| 5 | 2001703 | 106–117 | 2 |
| 6 | 2002113 | 118–127 | 1 |
| 7 | 2002114 | 128–137 | 1 |
| 8 | 2002115 | 138–155 | 1 |
| 9 | 2002116 | 156–167 | 2 |
| 10 | 2002628 | 168–177 | 1 |
| 11 | 2002629 | 178–187 | 1 |
| 12 | 2002630 | 188–205 | 1 |
| 13 | 2002631 | 206–217 | 2 |
| 14 | 2003090 | 218–227 | 1 |
| 15 | 2003091 | 228–237 | 1 |
| 16 | 2003092 | 238–255 | 1 |
| 17 | 2003093 | 256–267 | 2 |
| 18 | 2003509 | 268–277 | 1 |
| 19 | 2003510 | 278–287 | 1 |
| 20 | 2003511 | 288–304 | 1 |
| 21 | 2003512 | 305–316 | 2 |

Native1 is daily,2 weekly. Coverage identity remains billTypeId, not a label,
target, order ID or acquisition generation. Existing metadata describes retained
orders as unverified; coverage cannot upgrade that claim.

## Exact transport admission

Use the existing private HTTPS sync endpoint, device Bearer authentication,
Testing enrollment, active account and paired-character ownership gate, and
existing private Hunt permission. No new endpoint/auth system/permission grants.
Local retention and SyncPersonalHunts must both be ON; OFF cancels future sends
without erasing history or disabling ordinary sync, Market, PF or travel.

Presence retains `X-Gillions-Personal-Contract:personal-observations-v1` and the
unchanged travel header. Native additionally advertises
`X-Gillions-Hunt-Item-Coverage:hunt_bills_v2`. Site must explicitly return exactly
one hunt_bills resource entry under personalObservations:

```json
{
  "ok": true,
  "acceptedClientProduct": "GillionsGameSyncTest",
  "personalObservations": {
    "contractVersion": 1,
    "endpoint": "https://test.gillions.app/api/game-sync/sync",
    "resources": [{"resourceType":"hunt_bills","schemaVersion":2,
      "collectorSchema":"hunt-bills-v2","capability":"hunt_bills_v2",
      "maxPayloadBytes":65536}]
  }
}
```

Other enabled resources may have their own entries. Do not return simultaneous
v1/v2 entries for hunt_bills: ambiguity fails closed. Legacy hunt_bills_v1
acknowledgment keeps the unchanged positive-only v1 sender active, with no new
fields. Unknown/missing acknowledgment cannot activate coverage. Preserve V1/V2
map/focus capability admission separately; no focus/travel permission grants.

## Exact payload

Envelope remains `{resourceType:"hunt_bills",nonce,payload}`. Payload is existing
[personal Hunt export](personal-observations-v1.md), changing only top-level
schemaVersion to2, collectorSchema to hunt-bills-v2, permitting bills length0..22,
and adding billItemCoverage. An empty historical bills array never erases history.
The legacy uploadState string remains producer-format metadata, not status.

billItemCoverage contains exactly:

- collectorSchema: `hunt-bill-items-v1`
- domainKind: `currently-held-item-backed-bills`
- observationId: random32-hex observation identity, not acquisition/session token
- observedAtUtc: UTC snapshot time
- gameVersion, collectorVersion, sdkVersion: actual source versions
- source: `complete-loaded-key-items-snapshot` (source family; state may be unavailable)
- maximumAgeSeconds:15
- orderOwnership and acquisitionIdentity: `unsupported`
- domains: all22 entries with billTypeId, keyItemId and state

The [sanitized example](../examples/hunt-bills-v2.json) is invented fixture data,
not a real character observation. No account/character ID, partition hash,
session ID, location, credentials, widget/config/checklist IDs are in coverage.
Envelope/authentication owns identity. Payload cap remains64KiB.

## Retry and retention

One RAM-only prepared Hunt v2 body/hash/nonce uses the established receipt model
and shared single-flight private lane. Same-body retry keeps nonce; changed
semantics or invalidated local epoch creates a new nonce, never edits a sent body.
Do not persist coverage or prepared coverage in config. Restart/OFF/session
transition clears it. Positive history remains durably retained and is included
in the next fresh payload, so retiring a stale current assertion loses no history.

Preparation lives at most10s; dispatch also checks the original observation's15s
UTC/monotonic freshness, live state equality, epoch and auth/permission admission.
No offline negative journal or expired replay. Same nonce/different body remains
an error; established UUID snapshotId/receivedAt/unchanged receipt is required.
Testing84 schedules an ACKed assertion's refresh at6s from its original observation
time, with UTC and monotonic agreement. It uses only a newer already-available
character-bound RAM observation and may bypass the routine5s personal tick, never
single-flight, backoff or permission gates. An unchanged observation is not
reissued with a new nonce. Hunt reads remain3s; no command-poll coupling or renewed
timestamp on old evidence. The existing10s preparation/15s observation checks
still apply. Failure uses existing60..900s backoff, not faster gameplay observation.
Unchanged terminal input stays stopped in RAM until semantics/session changes.
Classified retry/backoff is committed for the still-authorized request even when
its sample expires or changes during response delivery; only ACK acceptance
requires the same current sample. OFF/session cancellation suppresses old callbacks.
The server must not extend observation freshness based on retries or receipt time.

## Site validation and supersession rule

Site must add TEST-only schema2 admission before Native sends it. Validate full
type/item mapping, all22 explicit states, supported source versions, UTC time,
15s maximum observation age, and existing private auth/nonce/receipt scope.
Monotonic newest-observation admission and equal-time semantic conflicts remain
fail closed. Merge historical positives under existing rules; never use empty
bills or omission to delete. Preserve v1 producer/read compatibility.

For character C/domain D, only a fresh latest absent_confirmed may exclude
historical records in D from CURRENT routing. Preserve their counters/provenance.
Newer present_unresolved/unavailable revokes absence authority; missing/expired
coverage is UNKNOWN and cannot authorize exclusion. A disconnected/disabled
device's old absence cannot become permanent. Select eligible current receiver
scope; never reuse a revoked/wrong-character session's assertion.

Do not supersede by individual order, monster, unrelated type, reset clock,
location/travel/focus, kill-count disappearance or omitted payload. No public feed.
Site source/schema/database changes are not part of this Native objective.

## Validation scope

Managed A-I tests cover absent/present/partial, transitions, permission OFF,
unchanged history, independent types, unresolved order and removal. Additional
tests cover malformed slots/catalog, freshness, epoch, nonce and version admission.
Source/SDK/build checks and exact-package tests do not constitute live inventory
proof. Live proof must confirm a complete bound Key Items read and at least one
state against the owner's ordinary inventory; no hunt completion is required.

Site wording for unchanged Party Finder attribution: **Data Provided by** label
followed by a clickable **xivpf.com** button linking to `https://xivpf.com`.
