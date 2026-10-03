# Private travel context v1 — Native producer and bounded Site handoff

Status: **Testing77 transport to accepted shared TEST travel-context-v1**.
Testing76 remains the immutable local-only predecessor. Activation changes no
Hunt, submarine, Dashboard, PF or market contract or collection semantics.

## Capability audit and evidence limits

Installed API15 / FFXIVClientStructs `7.56.2.9136`, game
`2026.09.15.0000.0000`; any different pair stops all new travel reads.
Actual installed metadata, typed compilation and public utility fixtures are
validation evidence, not successful live game observations.

| Fact | Source / supported meaning |
| --- | --- |
| Territory / map | Public `IClientState.TerritoryType`, `MapId`; validate current Map's TerritoryType and nonzero scale. |
| Position | Only `IObjectTable.LocalPlayer.Position`; public pure `MapUtil.WorldToMap(Vector2, Map)` with current map, rounded to 0.1 map units. World coordinates are transient calculation inputs, never retained/exported. |
| Attuned public destinations | Naturally visible `Teleport` addon and active AgentTeleport with matching AddonId, its list pointer equal to the owned `Telepo.TeleportList`, matching positive bounded count. Each ordinary public Aetheryte must match catalog territory and current typed UIState unlock flag within the SDK-declared bit array. Only positive observed entries, NOT complete unlock state. |
| Cached list Gil | Typed `TeleportInfo.GilCost`, bounded 0–99,999. This is an observed cached list quote, NOT proven final charge or proven freshly recalculated price. No cost formula invoked or reconstructed. |
| Home / favored / free | Loaded public IPlayerState HomeAetheryte, FavoriteAetherytes, FreeAetheryte and typed list favored/free flags. Favored disagreement rejects the entire list. Home zero is unknown. Free means the public configured destination or loaded free flag, not proof every platform benefit/ticket is accounted for. |
| Action usability | UNKNOWN. Attunement/list presence is not a guarantee teleport can execute under combat/duty/restrictions. No action execution/query is introduced. |
| Other price modifiers | UNSUPPORTED final-charge interpretation: tickets, fee reductions/other benefits and Return behavior are not independently modeled. |
| Freshness | Login/logout/map/territory events invalidate visible facts; fallback reads at most every15 seconds; every observation expires after45 seconds. Per-list cache refresh generation is NOT proven. |

**Teleport cost verdict: PARTIAL — bounded game list Gil quotes can be observed,
but final current charged Gil and price-cache refresh generation are not proven.**
`actualCostGil` is always null; Site MUST NOT use `observedListGil` for an exact
Cheapest Teleport claim. No owner price/action is needed for publication. One
ordinary visible Teleport comparison later supplies live evidence, not automatic
support for all modifiers.

Public IAetheryteList was deliberately NOT injected/enumerated: its accessor
calls native UpdateAetheryteList, and ordinary enumeration repeatedly accesses
Length/indexer. Avoid rebuilding state just to collect it. No guessed offsets,
hidden requests, UI text/OCR, hooks, actions, callbacks or UI opening.
Primary reference sources, checked against installed metadata:
[Dalamud list](https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Game/ClientState/Aetherytes/AetheryteList.cs),
[MapUtil](https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Utility/MapUtil.cs),
[IPlayerState](https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Plugin/Services/IPlayerState.cs),
[typed Telepo](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/UI/Telepo.cs),
[AgentTeleport](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/Agent/AgentTeleport.cs).
Upstream is reference only; installed SDK owns candidate compatibility.

## Implemented Native behavior

Separate `ShareHuntRoutingLocation` setting: **OFF by default**, including older
configs where Hunt/ordinary/PF permissions are ON. User label:
“Share my current location for Hunt route recommendations”. It clearly says
**HTTPS shared TEST only, independently granted**. Turning OFF cancels/clears volatile
travel state and stops reads; Hunt/submarine local retention and sync, ordinary,
PF/item links, Dashboard, Beastmaster and market choices stay independent.

TravelContextState holds just one in-memory observation and a transient own-player
association. No coordinates/cost payload/configuration partition survives restart.
No pending offline upload journal, diagnostic copy/export button, coordinates in
logs, ContentID/name/account/world/DC/estate details in the model. Public housing
entries are excluded (house/apartment/ward/plot/subindex). Other character/session
observations are invalidated; stale/older/equal-time replacement is rejected.
Unavailable clears ONLY volatile travel state, never retained Hunt or other data.

Every framework callback checks cheap lifecycle/expiry admission when enabled. At most
one travel read per15 seconds; event invalidation cannot bypass that cadence.
No full sheet enumeration: cached Lumina sheets, bounded row lookups, one public
player position. Draw uses a bounded immutable view and hides expired facts;
at most five destination quote samples with bounded public names and IDs.
Logout/login and OFF/ON clear facts but retain the current plugin-load admission
deadline, so they cannot bypass the15-second limit. Diagnostics report total read/validation/
view ms and framework-thread allocations. Offline policy timing is separately
identified; actual native read timing awaits owner test.

## Accepted Site readiness (independently verified before editing)

Shared TEST checkout and baked web revision observed October3:
`93007035e6cce8636e10a356dcda4fb07c3dfd1c`, image
`sha256:82dba0e1aa50a98182e049dab28cc7a8b0b8f337e7e36807fa8658f03c06128a`.
Site accepts travel alongside Hunt/submarine; schema0020. Travel custody is a
single-process expiring RAM cache, NOT durable personal-latest/receipt tables.
Hunt/submarine PersonalSyncPolicy remains unchanged; travel preparation is RAM-only.

## Exact accepted transport — Site owns intake and private presentation

Reuse existing authenticated `POST /api/game-sync/sync` envelope
`{resourceType, nonce, payload}` and paired-device account/linked-character
ownership; no new receiver/auth, no identity IDs inside payload. Testing-only
exact trusted HTTPS origin `https://test.gillions.app`, no redirects/IP/HTTP/TLS
bypass. Existing pairing, active account, character, version and resource checks
must run before body handling and on every request, not merely presence.

Accepted resource:

- resourceType `travel_context`
- schemaVersion `1`, collectorSchema `travel-context-v1`
- permission `server:game-sync:personal:travel-context:v1`
- capability `travel_context_v1`
- separate persisted server opt-in and separate default-OFF Native consent;
  existing Hunt/submarine grants MUST NOT silently acquire this permission
- max payload65,536 UTF-8 bytes; envelope69,632; depth8; at most256 unique public
  destinations (the byte bound may admit fewer); strict exact fields below

Payload (sanitized **synthetic** example, not a real character observation):

```json
{
  "territoryId": 100, "mapId": 200, "mapX": 10.1, "mapY": 20.2,
  "observedAtUtc": "2026-10-03T18:00:00Z",
  "gameBuild": "2026.09.15.0000.0000", "nativeVersion": "7.56.2.9136",
  "collectorVersion": "0.0.76.0", "teleportSupport": "OBSERVED_PARTIAL",
  "destinations": [{
    "aetheryteId": 1, "territoryId": 100,
    "attunement": "OBSERVED_IN_PERSONAL_LIST", "usability": "UNKNOWN",
    "observedListGil": 120, "actualCostGil": null,
    "home": false, "free": false, "favored": true
  }],
  "schemaVersion": 1, "collectorSchema": "travel-context-v1",
  "locationSource": "dalamud-local-player-maputil",
  "teleportSource": "naturally-visible-teleport-owned-cache",
  "costSupport": "UNSUPPORTED_FINAL_CHARGE",
  "uploadState": "local-only-no-server-contract"
}
```

Exact model lives in TravelContext.cs; preserve camelCase fields and nulls.
IDs positive<=65,535 plus actual catalog existence/Map-territory agreement on
Site; coordinates finite (0,100], one decimal. No 0,0 placeholder. UTC Z >=epoch;
no future timestamp accepted as current. Collector metadata1–80 safe ASCII chars,
exact native/game supported pair. Reject extra keys/duplicate destinations and
contradictory shape. `UNAVAILABLE` teleportSupport requires empty destinations;
`OBSERVED_PARTIAL` requires positive rows. Empty destinations says no trusted
list observed, NOT no unlocked teleports. `actualCostGil` MUST be null in v1.
Missing location means do not send, never fabricate coordinates. Home/free/favored
null means unknown, not false. `observedListGil` is required for admitted observed
rows; zero is a quote only, never an unknown sentinel. Missing destination Map
joins to Site-owned Aetheryte reference data, not an inferred plugin position.

**Lifetime/custody:** authenticated account+linked-character latest context only.
Short TTL45 seconds from observation time, not receipt time; reject expired/future
updates and older/equal-time conflicts. Do not refresh observation freshness on
retry/read. Never append location observations or join to an activity log. No
raw payload/coordinate logging, historical receipt body, archive/backups of a
movement trail, public response, third-party forwarding or aggregate analytics.
Site chooses appropriate short-lived storage consistent with these invariants;
ordinary durable personal observation tables/14-day payload receipts are NOT
automatically suitable. Read responses private/no-store and account-owned only.

**Retry/receipts:** same nonce+same canonical payload -> same logical
receipt; different content under nonce ->409/fail closed. Existing success shape
`{ok:true,snapshotId:<UUID>,receivedAt:<UTC>,unchanged:<boolean>}`;201 new/200
unchanged. Retain only bounded nonce+hash+receipt metadata through the short TTL,
not historical location bodies. Same key tied to device+character+resource;
unauthorized cross-account/character requests fail before body processing.
Bound at one latest context and one current retry receipt per owner/character,
no unbounded journal. After expiry a stale nonce must not resurrect old location.
Native keeps one immutable prepared nonce/payload for<=45s in
RAM, replace expired pending state with a new nonce+fresh observation, and use
bounded >=15s retry/backoff (Retry-After on429/503). Permission OFF/logout/switch/
wrong origin/unsupported build cancels and clears ONLY travel pending/latest.

Presence retains `X-Gillions-Personal-Contract: personal-observations-v1` and adds
`X-Gillions-Personal-Capability: travel_context_v1`. Only the exact grant-gated
`personalObservations.resources` travel entry admits transmission. It must match
schema, collector, transportContract, capability, endpoint, byte/depth/destination
limits, ttlSeconds and header names. Existing personal grants alone cannot admit it.
Both enrolled and request Testing versions must be four-part0.0.76.0 or compatible
0.0 successor; ordinary presence updates the stored installed version normally.
Submission uses `X-Gillions-Personal-Contract: travel-context-v1`, resource
`X-Gillions-Personal-Resource: travel_context`, and the capability header above.
No redirects, alternate origin, direct IP, HTTP or TLS bypass. Valid receipts are
201/unchanged:false or200/unchanged:true with UUID and UTC time; receipt time never
extends freshness. Error bodies are not read/logged. Retries >=15s with bounded
exponential backoff;429/503 honor Retry-After and at least60s. A backoff outliving
the observation discards its pending body; a genuinely fresh observation later
gets a new nonce. Clearing facts does not reset the per-load send/backoff budget.
Consent OFF/logout/session switch/re-pair/unpair/unload/map change/incompatible
build/wrong origin/rejected capability/expiry cancel the affected volatile state.
No configuration or offline queue entry is created. Logs contain only a concise
acceptance message; neither complete payloads nor coordinates are logged.
`uploadState: local-only-no-server-contract` is retained historical producer
metadata required by Site v1, NOT an assertion that Testing77 transport is dormant.

## Exact product direction for Site's Hunt routing lane

Combine private accepted Hunt target identity/counters/derived completion with
fresh private territory/map/roundedposition and positive attuned public entries;
Site owns map/zone/Aetheryte positions and possible Hunt spawn positions. Native
returns no bestAetheryte/recommendedTarget/ranking/widget/checklist settings.

Normal Hunts is compact, **current target and large readable map first**.
Remove **State** and **Reference** columns from prime presentation; compact
status/Help/details can retain provisional/freshness/provenance. Automatically
select the appropriate incomplete target, show its map, and when a real accepted
Hunt observation reaches required kills advance/recalculate/switch map without
a manual Next click. Do not infer completion from missing bills or stale caches.

Modes conceptually: Cheapest Teleport (owner-preferred default ONLY once final
actual prices are trustworthy), Shortest Travel, Original Bill Order. This v1
supports current-zone/reference-travel/order fallback; cached diagnostic quotes
are not exact cheapest support. Site may evaluate multiple **Possible locations**,
never “Current mob location”; no radar/sighting claim.

Preserve preceding Site handoffs, including exact PF attribution:
**Data provided by xivpf.com**, with clickable source link where appropriate.

## One concise owner validation after updating

Enable the distinct routing-location checkbox and open “Private Hunt routing
context”. Compare zone/map and rounded map coordinates, naturally open Teleport
and compare up to five destination IDs/quote Gil/flags after one15-second check.
Move or make one normal zone transition, confirm old context clears/new position
appears, then turn OFF and confirm facts clear while ordinary/Hunt sync continues.
No Hunt kill, repeated teleports, extra delivery, configuration reset or credential
sharing needed. No upload can occur in76. In77, also confirm the independent Site
device travel grant, fresh private Site routing context, one replacement after
moving/zoning, and expiry within45s after turning OFF. No kill/teleport execution
is required. Synthetic/transport tests are not real FFXIV collection evidence.

End state after77 publication: **READY FOR OWNER LIVE TRAVEL TRANSPORT VALIDATION**.
