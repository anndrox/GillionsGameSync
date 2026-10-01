# Passive market observations v1 — Testing client / Site handoff

Status: plugin candidate 0.0.67. The current server has no compatible intake.
This specifies the exact required contract, not a deployed endpoint, anonymous
receiver or authority to mutate another lane's source/runtime. Site Operations
owns implementation, storage/cache behavior and any deployment. Native Collector
owns this client. Stopped Market/Worker branches and crawler redesign are outside scope.

## Source and availability

Installed API 15 reflection/compilation verifies `IMarketBoard.OfferingsReceived`
and `HistoryReceived`, `IMarketBoardCurrentOfferings.ItemListings` and
`IMarketBoardHistory.ItemId/HistoryListings`. The collector subscribes only to
these public receive events. It never opens interfaces, searches, queries game
servers, reads Dalamud settings, proxies or uploads to Universalis.

Primary references: [public service](https://github.com/goatcorp/Dalamud/blob/b666d821a47306fb447c60155b5d99377f91a5ee/Dalamud/Plugin/Services/IMarketBoard.cs),
[listing interface](https://github.com/goatcorp/Dalamud/blob/b666d821a47306fb447c60155b5d99377f91a5ee/Dalamud/Game/Marketboard/Network/Structures/IMarketBoardCurrentOfferings.cs),
[history interface](https://github.com/goatcorp/Dalamud/blob/b666d821a47306fb447c60155b5d99377f91a5ee/Dalamud/Game/Marketboard/Network/Structures/IMarketBoardHistory.cs),
[UTC sale decoding](https://github.com/goatcorp/Dalamud/blob/b666d821a47306fb447c60155b5d99377f91a5ee/Dalamud/Game/Marketboard/Network/Structures/MarketBoardHistory.cs).
Current upstream paths are technical references; installed API 15 signatures and
actual SDK builds are the candidate's compatibility evidence. No source hooks copied.

Listings: one packet contains 1–10 supported rows, not a complete item market.
The public API does not expose expected packet count/batch completeness or an
empty item's identity. Zero rows are omitted, never "zero listings". Mixed item
packets or invalid rows are rejected atomically. Do not cast internal classes or
parse raw packets to recover unsupported review timestamps/batch internals.

History: 1–20 raw recent sales, not all sales or a complete time interval. Source
purchase times are UTC from the supported `PurchaseTime`; no listing update time
or snapshot source time is invented. `SalePrice` is retained as the raw API field,
not multiplied into a guessed transaction total. Identical same-price/time/
quantity sales can be ambiguous; they are observed rows, not proven unique trades.

World: current player's `CurrentWorld.RowId` at the framework-thread receive
callback, not HomeWorld and not an event world field. Logged-out, unknown-world,
between-area and non-framework callbacks are dropped. The captured session/world
is checked again immediately before dispatch. Session/world changes drop the
queue. This is context evidence, not proof of packet-origin world or freshness;
late replies across transitions and live event-thread behavior need game testing.

## Setting, pairing and compatibility

`ContributeObservedMarketData` defaults **true**, including old Testing config
without the property. Saved false remains false across reload. The checkbox is
authoritative; website/Dalamud choices do not modify it. OFF stops new sends,
clears transient work and cancels active work where possible; a server-accepted
request cannot be recalled. Ordinary sync, PF and other local features are independent.

Reuse existing paired HTTPS origin, bearer and request lifetime. No new identity,
credential, session framework or pairing scope. The ordinary presence body remains
unchanged. Testing adds `X-Gillions-Market-Contract: 1` to that existing request.
Only a successful, current, authenticated presence response with this exact
optional object permits market dispatch for the same enrollment generation:

```json
{"marketContribution":{"contractVersion":1,"enabled":true,"acceptedClientProduct":"GillionsGameSyncTest"}}
```

Missing/false/wrong product/version or malformed evidence means no market upload;
it does not fail ordinary sync or change the plugin checkbox. Compatibility is
memory-only for this enrollment/load and is replaced by subsequent valid presence
responses. The server still authenticates/authorizes every contribution. It must
derive allowed Testing product from the existing enrolled device, not User-Agent.

## Exact proposed HTTP contract

`POST /api/game-sync/market-observations` on the credential's issuing HTTPS origin.
Use the existing Bearer Authorization and product/version User-Agent. No redirect
following, alternate receiver, anonymous fallback or cross-origin credential transfer.
Content-Type `application/json`, maximum 32768 request bytes; strict version 1.

Listing example (synthetic, partial):

```json
{
  "schemaVersion":1,"source":"gillions-game-sync","clientProduct":"GillionsGameSyncTest",
  "observationId":"0123456789abcdef0123456789abcdef",
  "worldId":74,"itemId":5333,"kind":"listings",
  "clientObservedAtUtc":"2026-10-01T12:00:00Z",
  "worldEvidence":"current-world-context","completeness":"partial","sourceSnapshotAtUtc":null,
  "listings":[{"listingId":"12345678901234567890","hq":true,"quantity":7,"pricePerUnit":1500,"retainerCityId":1}],
  "sales":null
}
```

History has `kind:"history"`, `listings:null`, and `sales` containing 1–20 rows:

```json
{"hq":false,"quantity":2,"salePrice":600,"soldAtUtc":"2026-10-01T11:00:00Z","onMannequin":false}
```

Exact allowlist: no player/character/account/Content ID, buyer/retainer/artisan
identity or name, request ID, credential, logs, machine identity or private strings.
Listing ID is a positive canonical decimal UInt64 **string** for market-row
deduplication, not a reporter ID; avoid JavaScript numeric precision loss. Item
is positive base catalog ID below 1000000; world/catalog validity is server-checked.
Quantities/prices are positive UInt32; HQ boolean. Optional city is 1–8 or null
when unsupported/unknown. `sourceSnapshotAtUtc` is always null. Client observation
UTC is actual event receipt, immutable on retries; server receipt UTC is assigned
by Site, not trusted from a client. Sold UTC is the genuine source sale timestamp,
not observation time. Versioned API/catalog interpretation must remain explicit.

HTTP 200 exact receipt, maximum 8192 bytes:

```json
{"ok":true,"schemaVersion":1,"observationId":"0123456789abcdef0123456789abcdef",
 "worldId":74,"itemId":5333,"kind":"listings","status":"accepted",
 "receivedAtUtc":"2026-10-01T12:00:03.000Z"}
```

`status` is `accepted`, `duplicate` or `older_observation`; all IDs/kind must match
and receiver time must be `yyyy-MM-ddTHH:mm:ss.fffZ`. Receipt is acceptance of an
observation, never proof of whole-market coverage or game truth. No raw error text
is consumed/logged. Errors use existing protocol conventions: 400 malformed,
401 invalid device, 403 denied Testing/account, 409 conflicting observation ID,
413 oversized, 415 unsupported media, 429 rate-limited, 503 temporarily unavailable.
429/5xx retries are bounded; use HTTP Retry-After seconds/date. 401/403 persist a
non-secret stop for that enrollment; fresh pairing is required after correction.
Redirect/404/405 stop the load without fallback; fix Site before reload.

## Deduplication, partial data and bounded failures

Client uses random observation IDs stable for a transient retry, never regenerates
receipt time or body. Identical successful content is suppressed for two minutes.
Site must separately deduplicate repeats across IDs/reporters; listing IDs are
market-object identities, sale rows have no stable trade ID. Preserve multiplicity
within observed arrays and do not count repeated history responses as new trades.
Same observation ID with changed body is a conflict, not an update.

Site must validate all rows atomically, bound client clock skew (suggested five
minutes), assign trusted received time, and prevent older valid observations from
overwriting newer world/item/kind or listing-row evidence. An earlier delayed
packet is not made newer by its later receipt. Partial listing packets may add/
update observed rows but cannot delete unobserved rows or replace a complete cache
with an empty/partial snapshot. History preserves genuine sold time independently.
Client clocks/source snapshots are not authoritative proof; do not revive a crawler.

At most 64 pending packets plus one in flight; two-minute TTL; at most 64 recent
dedup fingerprints. Full queue rejects a new observation with fixed summary;
stale/failed transient contributions may be discarded. No journal, exactly-once
custody, disk payload queue or guarantee of delivery. Work is memory-only and
clears on opt-out, logout/session/world changes or disposal. Serialize/hash/start
HTTP on a worker; context/upload maintenance is at most four times/second, no
market sampling/game query. Six HTTP starts/minute/load, 15-second total deadline,
three attempts/observation, bounded backoff and Retry-After. Gameplay never waits
for an upload; ordinary sync/PF remains separate.

## Privacy / precise Site handoff

The payload is de-identified, but authenticated transport is **not anonymous**:
existing bearer enrollment maps to a device/account, presence already includes
ordinary character identity, User-Agent includes product/version, and ordinary
network/authentication infrastructure can see an IP. None is added to the market
dataset merely for attribution. Client persists only setting and denied generation.

Site must retain shared market facts/provenance `source=gillions-game-sync` and
version evidence without new reporter/account/device/name linkage in market data.
Existing private auth/rate-limit state may use established identity; do not store
tokens/raw headers or introduce reporter tracking as a market feature. Keep normal
security logging under its established policy and document any actual retention.
This client cannot attest that users ran an unmodified plugin; enforce authorized
entry-point, bounds/quality and rate limits rather than claiming cryptographic proof.

Needed bounded server outcome: implement the optional presence acknowledgment and
authenticated v1 route with pre-body access checks, exact receipts, limits,
dedup/older/partial semantics and de-identified persistence; validate with synthetic
isolated requests including wrong product, revoked device, malformed/mixed scope,
duplicate/conflicting IDs, old observations, listing multiplicity, history duplicates
and unavailable storage. Preserve ordinary/PF/Retainer contracts and disabled-client
compatibility. No production migration/deployment is authorized by this document.
