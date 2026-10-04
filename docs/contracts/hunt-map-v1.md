# Testing automatic Hunt map guidance v1

Canonical current Native contract, matched to deployed Site source
`1be7595aec3f9fa2b407b5dbdeec8b8e36e3eb3e`, clean TEST schema `0021`.
Site authority: `docs/contracts/game-sync-hunt-map-v1.md` in Gillions/FFXIV-Gillions.
No Site source or schema change is part of this Native implementation.

## Consent / identity

Testing only, exact HTTPS origin `https://test.gillions.app`. New local
`AutomaticallyShowHuntMap` defaults false, including upgrades. Site separately
owns default-OFF `ui_preferences.huntMapGuidance[characterId]`, enabled/deviceId/
mode/expansion, and grant `server:game-sync:receive:hunt-map:v1` to its selected
account-owned Testing device. None implies other consent or location sharing.
Existing paired-device bearer, bound character/session and Testing User-Agent
`GillionsGameSyncTest/0.0.78.0` remain authoritative; no payload account IDs.

## Exact existing poll / consume

One existing five-second request loop, serialized flight owner. Hunt admission is
independent of item/PF/ordinary-sync controls. It uses redirect/cookie-free normal
TLS HTTP, never direct IP, production or insecure fallback.

`POST /api/game-sync/item-links/poll`: ONLY `{"capability":"native_hunt_map_v1"}`.
Exact acknowledgment: `{"ok":true,"request":null}` or a claimed `hunt_map` request.
No PF-style nativeRequests acknowledgment descriptor is invented. Capability
availability is 20 seconds, advertisement is not a grant. Polling never enqueues.

Exact request fields: requestType, requestId (UUID), claimToken (opaque),
huntTargetId, huntTargetName, availability, territoryId, mapId, mapX, mapY,
candidateId (24 lowercase hex), revision (64 lowercase hex), candidateIndex (0),
candidateCount, expiresAt (UTC, at most 90 seconds). Availability has classification
(ALWAYS_AVAILABLE/FATE_REQUIRED/CONDITIONAL/UNKNOWN), fateId, fateName, activity
(UNKNOWN only). Extra/missing/duplicate fields, malformed text/coordinates,
unsupported types, bad expiry and invalid installed Map/Territory/target/FATE IDs
fail closed. Response reading is capped at4096 UTF-8 bytes, depth bounded.

`POST /api/game-sync/item-links/consume`: ONLY requestType=`hunt_map`,
capability=`native_hunt_map_v1`, requestId, claimToken, revision.
Only HTTP200 and exact `{"ok":true,"consumed":true}` authorizes presentation.
409/stale/expired/revoked/unauthorized/redirect/network failure: no map action,
discard coordinates, no consume retry. Ordinary sync/local observations survive.
The whole Hunt poll/consume turn has a15-second linked deadline, each streamed
body a10-second deadline, and known claim expiry can only shorten the consume
lifetime. Stalled200 headers/bodies cannot indefinitely hold the shared request
loop or retain expired claims. Failure releases its flight owner for item/PF.

## Revision and presentation

Site owns the target/reference join, candidate ordering and revision. Consume
atomically rechecks current revision, ownership, preference/device/grant and
capability under the same lock as accepted Hunt advancement. After B commits,
old unconsumed A is rejected, including before B is polled. A consumed before B
commits was valid at that instant; Native presents immediately on framework thread
after response, rechecking session/consent/expiry. It does not schedule delayed maps.

Server retains one revision watermark per account/character and never auto-replays
equivalent observations, page refreshes, repeated polls or unchanged travel updates.
Native retains at most128 attempted request IDs in RAM; no claim/coordinates or
request history persisted/logged. Testing80 records only public target/candidate/
revision and presentation outcome after a new one-time consume in bounded local
diagnostics, never request/claim token or private position. Reserve before consume; a lost response sacrifices delivery
rather than replaying an uncertain side effect. New manual Show request ID may
intentionally re-present the same revision; Native cannot invent an automatic/manual
flag absent from v1. A revision hash is opaque and never sorted locally.

Supported `IGameGui.OpenMapWithMapLink(MapLinkPayload)` uses the float human-readable
XY constructor. No unsafe callbacks/clicks, game-server requests, teleport, movement,
targeting or combat. Logout/re-pair/unload/OFF cancels the request token; server
revocation/capability loss/current-revision change prevents consume. Local zoning
also fails closed. Map failure requires Site manual retry, not automatic reopening.

FATE_REQUIRED prints its named FATE requirement and unknown activity. Every map
is explicitly a Hunt area/reference, never a sighting. Conditional/unknown labels
stay honest. Site selects candidate0 and count/identity; alternative cycling is
NOT authorized in v1. Bounded Site follow-up: define a current-revision action that
selects another already-authorized candidate through these same endpoints before
Native can offer Next possible location. No arbitrary-coordinate extension.

Preserved77 travel-context-v1: separate consent/grant, 15-second native read/send
admission, RAM/latest-only, 45-second expiry, cached quotes not final teleport cost.
All other prior capabilities and Stable remain independent.

Validation distinguishes focused fixtures / running Site modules with simulated
SQL / actual read-only schema and normal TLS checks from owner live game proof.
Real automatic map advancement remains for the owner; no fixtures prove gameplay.
