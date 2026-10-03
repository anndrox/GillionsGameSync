# Testing Party Finder audit and exact Site handoff

Native successor: 0.0.75. This defines the prepared **plugin-side** request contract,
not an activated Site enqueue/claim service. No Site changes are made here.

## Current implemented pipeline, 2026-10-03

| Layer | Classification | Evidence / limitation |
| --- | --- | --- |
| Natural FFXIV observations | IMPLEMENTED BUT UNPROVEN end-to-end | `IPartyFinderGui.ReceiveListing`, no active queries; adapter copies the existing v1 listing fields. No new real-client session was observed in this audit. |
| Published 0.0.74 Testing target | WRONG TARGET | Build metadata and session binding select production; a TEST pairing cannot contribute there. |
| 0.0.75 prepared Testing target | IMPLEMENTED + PROVEN in policy/SDK tests | Dispatch derives the URL from the immutable authenticated session, restricted to exact `https://test.gillions.app`; no production, HTTP, direct-IP or redirect fallback. Runtime delivery remains unproven. |
| TEST authenticated intake | IMPLEMENTED + PROVEN for reachability/auth, not live insertion | Existing POST `/api/game-sync/party-finder/contribute`; anonymous 401, authorized empty batch 400, no listing inserted. |
| Current listing state | PARTIAL | Expiring in-memory cache, 6,000 entries maximum; restart clears it. Authenticated GET `/api/game-sync/party-finder/listings` returned zero entries. This is not durable history or evidence of a live contributor. |
| TEST PF page | WRONG SOURCE / PARTIAL | Page service reads its separate reference-proxy/xivpf provider, not the contribution cache. A successful contributor receipt does not prove page visibility. |
| PF website native request | MISSING on Site | Prepared Native supports the contract below; old Site polling remains item-only and has no matching PF acknowledgment. Fail closed/dormant. |

**Shared TEST is not verified to receive live Testing plugin PF observations today.**
No fixture can close that gap. The audited cache is empty and published 0.0.74 is
wrong-target for TEST. These facts do not prove nobody ever contributed through
another client. The authenticated page is reachable, not integrated with this cache.

Audited TEST checkout `8c135f6113fc6383a7e21c8b94de4f770b1afcc7`, baked application
`d70dbbf649bbee4b373dbb8cf4fb4312dc7c88ed`. Personal/PF source since the prior
`9018a32b3101a50f8e4219b4f632f73f8934ec69` audit is unchanged. Deployment can advance;
Site must reverify its active identity before implementing this handoff.

## Existing contribution contract remains unchanged

Keep Site `docs/contracts/game-sync-party-finder-v1.md`: schema 1, natural public
listings, authenticated Testing enrollment and persisted
`server:game-sync:party-finder:contribute:v1` permission. Local PF opt-in is separate
and OFF by default. Preserve the existing 1,000 pending / 100 batch / 256 KiB body /
six requests per rolling minute limits and exact acknowledgment. Immutable retry
observation timestamps, expiration, durable denial stop and opt-out cancellation
remain. A zero-time observation can remove an existing server listing; it must not
produce an actionable link. Reporter identity stays in authentication, not payload.
Stable continues its independent, opt-in direct xivpf contribution path unchanged.

## Installed native capability and authoritative fields

Installed API15 directly confirms public
`SeString.CreatePartyFinderLink(uint listingId, string recruiterName, bool isCrossWorld)`.
It constructs a native hyperlink, not a supported command to open a listing.
`IPartyFinderGui` exposes no public listing Open/Join/Apply. Native delivers the
link using `IChatGui.Print` on the framework thread. **NATIVE CHAT LINK REQUIRES
FINAL IN-GAME CLICK.** Never claim the website opens/joins the party automatically.

Required fields already survive the contribution model; no v1 payload extension:

- `id`: positive uint; reject a natural source ulong above uint.MaxValue before
  conversion in Testing. Never truncate into another actionable listing.
- `name`: `IPartyFinderListing.Name` is the host/recruiter, not a listing title.
  It is currently preserved as base64 game-text bytes. Decode strict UTF-8 to a
  plain canonical recruiter name; reject SeString/control payloads, invalid UTF-8,
  leading/trailing whitespace, format/private-use/surrogate characters, over
  64 UTF-16 units or 128 UTF-8 bytes. Do not substitute a title or browser text.
- `search_area`: installed SDK flags DataCenter=1, World=8. `flags & 9` must be
  exactly 1 (crossWorld=true) or 8 (false); 0/both/unknown-width are unsupported.
  Other privacy/recruitment bits do not establish world scope. Do not use the
  Dalamud listing indexer: its zero/None behavior matches all flags.
- `created_world:last_server_restart:id`: canonical current listing key.
  `home_world`/`current_world` are not substitutes for cross-world scope.
- `observedAt`, receipt expiry and `seconds_remaining`: retain immutable source
  freshness and computed cache expiry; needed for server enqueue/consume validation.

Actual installed SDK fixtures verify signature, enum values and both native
PartyFinderPayload flag cases with uint boundary IDs and SeString encode/decode.
The factory compiles against the installed DLL. It requires live Dalamud services,
so offline payload tests do **not** prove actual in-game presentation or clicking.
Primary references: [listing definition](https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Game/Gui/PartyFinder/Types/PartyFinderListing.cs),
[service](https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Plugin/Services/IPartyFinderGui.cs),
[SeString helper](https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Game/Text/SeStringHandling/SeString.cs).

## One bounded Site Operations handoff — return, do not execute

1. **TEST PF feed integration:** retain the existing authenticated contribution
   endpoint; do not add an uploader. Bind the TEST page/alert matching to its
   authoritative current cache, or provide a deliberate supported adapter without
   silently mixing fixture/provider state. Label native vs xivpf provenance
   honestly. Do not claim durable history. Prove a naturally received listing's
   intake acknowledgment, key/freshness and real page visibility when available.
2. **Enqueue:** extend the existing authenticated website native-request path,
   e.g. POST `/api/game-sync/party-finder-links` accepting ONLY `{listingKey}`.
   This is the proposed Site enqueue route, not a route implemented by Native.
   Reuse existing session/CSRF/account access/rate limits and the item request
   queue, claim and consume machinery; no second plugin poll channel. Select an
   account-owned, nonrevoked, actively connected Testing device advertising the
   capability below with explicit PF-action consent. Site derives all PF fields
   from the CURRENT authoritative cache record, never browser-provided ID/name/
   world/serialized payload. Scope request and claim to that account/device.
3. **Freshness:** compute `expiresAt=min(enqueueNow+60s, listingObservedAt+5m,
   authoritativeListingExpiresAt)`. Reject expired/deleted/zero-time listings at
   enqueue, claim and consume. Max positive listing lifetime supported by Native
   is one hour from observation. Observation may not be over 30s in the future.
   Native rechecks times immediately before presentation; Site must also recheck
   that the key still represents the authoritative listing at consume. Replaced
   content/restarted server keys invalidate the action, never retarget a claim.
4. **Poll compatibility:** reuse POST `/api/game-sync/item-links/poll`, its existing
   `capability:"native_item_link"`, pluginVersion and five-second cadence. Testing
   additionally sends `nativeRequests:{contract:"native-requests-v1",contractVersion:1,
   capabilities:["native_party_finder_link_v1"]}` only when locally permitted on
   secure TEST. Otherwise capabilities is []. Old item-only clients and legacy
   item request/consume responses must remain unchanged. No acknowledgment means
   no PF action. Site returns exact acknowledgment plus at most one claimed request:

   ```json
   {
     "ok": true,
     "nativeRequests": {
       "contract": "native-requests-v1", "contractVersion": 1,
       "acceptedClientProduct": "GillionsGameSyncTest",
       "capabilities": ["native_party_finder_link_v1"]
     },
     "request": {
       "requestType": "party_finder",
       "requestId": "1f5f2617-8d91-4bb2-bbda-b884e0117f9c",
       "claimToken": "synthetic_fixture_claim_1234567890",
       "listingKey": "57:1700000000:101", "listingId": 101,
       "recruiterName": "Fixture Recruiter", "crossWorld": true,
       "listingObservedAt": "2026-10-03T12:00:00Z",
       "listingExpiresAt": "2026-10-03T12:02:00Z",
       "expiresAt": "2026-10-03T12:01:00Z"
     }
   }
   ```

   Synthetic shape only; these times are not a current actionable request.
   The ten request fields are exact; extras/duplicates/missing/wrong types fail
   closed. UUID D-format nonzero request ID; canonical positive ushort world /
   uint restart / positive uint listing key. Claim token 16–500 ASCII alnum/-/_;
   random and unguessable in real Site implementation. All three timestamps UTC Z.
5. **Consume:** reuse POST `/api/game-sync/item-links/consume` with
   `{requestId,claimToken,requestType:"party_finder",listingKey,contract:"native-requests-v1"}`.
   After account/device/claim/expiry/current-listing validation and a one-time
   atomic consume, return exactly associated acknowledgment
   `{ok:true,consumed:true,contract:"native-requests-v1",requestType:"party_finder",requestId}`.
   Old item `{ok:true,consumed:true}` alone is not PF authorization. The replay
   must NOT reauthorize presentation; claim tokens must never move to another
   device/type/key. Native reserves request ID before awaiting, remembers at most
   128 attempts, consumes before printing and checks session/permission/expiry
   again on the framework thread. Lost consume response sacrifices delivery,
   never retries a side effect. Server consumed-state/short expiry prevents replay
   after reload or bounded local-ID eviction. No persisted command journal.
6. **Consent/migration:** existing Testing master is now “Allow website 'Open in
   FFXIV' requests”; its item behavior/default is unchanged. New child “Include
   Party Finder native links (final in-game click)” defaults OFF for existing and
   new users. Both gates are necessary; old item consent never expands. Logout,
   re-pair, device/session change, plugin unload or either switch OFF invalidate
   pending actions. No Hunt/Submarine/Market/Automatic-sync coupling. PF contribution
   consent is separate from receiving a website link. Do not grant action classes
   merely because a client advertises them.
7. **Alert UX:** Notification Center record → toast → **Open in FFXIV** → bounded
   enqueue. “Queued” is not “opened.” “Sent to FFXIV” means consumed chat-link
   delivery, with **Click the native link in game to open the listing**. Without
   client delivery acknowledgment, server consume proves only claim consumption;
   do not claim confirmed rendering. Return “Listing expired”, “No compatible
   Game Sync connected” or “Request failed” honestly; retry means a new intentional
   enqueue after freshness validation, never replay an old consumed request.
   Use exact source attribution **Data provided by xivpf.com**, linked to
   `https://xivpf.com`, wherever that provider supplies the data. Do not imply
   Gillions originates/owns xivpf data or mislabel native-only observations.

`SeString.CreatePartyFinderSearchConditionsLink(string message)` is a supported
generic notification hyperlink, not an API to encode/recreate Gillions filters.
No search fallback is implemented; later optional research must establish useful
native behavior before claiming alert-condition restore.

## Validation / completion boundary

Focused policy and installed SDK evidence are offline/synthetic. The shared TEST
audit used an existing synthetic account and an ephemeral normally enrolled device,
then revoked only that device. It submitted no positive PF data. No real PF upload,
page integration or website-to-game action is proven. Site/page/request completion
is the remaining bounded dependency; Native can publish the safe, dormant Testing
capability independently. Stable, production, Site source, personal collectors and
Market architecture remain unchanged.

**READY FOR SITE PARTY FINDER / OPEN-IN-FFXIV HANDOFF.**
