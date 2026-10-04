# Testing Hunt map V2 and ephemeral Active Hunt focus

Reconciled against deployed shared TEST Site
`70bc0d7a45ba9e54d44647df72b47f735fe9042a`, schema0025, and its
`docs/contracts/game-sync-hunt-map-v2.md`. Existing V1 contract remains unchanged.
Rediscovered deployed successor `36327f9cf624ca44d535a30e71fade2853762e74`
at schema0025 during latency validation. Only Hunt references/tests/documentation
advanced; command/focus modules and wire contracts are unchanged. Running-module
V1/V2/Next/focus/consume fixtures pass against that exact successor.

Only `https://test.gillions.app`, Testing identity, existing owned paired session,
separate default-OFF map consent and `server:game-sync:receive:hunt-map:v1` grant.
No new endpoint, account policy, TLS bypass, location consent or native collection.

Normal POST `/api/game-sync/item-links/poll` body contains only
`capability:native_hunt_map_v2`. Exact acknowledgment is `ok,request,progression`;
normal polling and browser Next require `progression:null`. Native does not send
device-side Next. V2 request adds exactly five fields to the unchanged15-field V1:
`contractVersion:2`, `candidateKind`, `walkExpiresAt`, `candidateSetTruncated`,
`recommendedAetheryte:null|{aetheryteId,name}`. Ordinary/FATE uses one anchor/index0;
discrete B-rank supports bounded indices/counts1..128. FATE activity stays UNKNOWN.
Reference locations are never sightings. Installed map/territory/aetheryte rows,
coordinate/text/type/count/claim expiry validation fail closed.

Walk timestamp is metadata, not cursor expiry/authority. Native never resets,
advances or wraps the Site cursor. Intentional browser `POST /hunts action=next`
queues a fresh normal command; focus/travel/time/partial progress do not authorize
automatic re-presentation. Site owns target/candidate/revision and watermark.

Consume uses existing `/api/game-sync/item-links/consume`, exact returned
requestId/claimToken/revision and capability for the parsed version. Only exact200
`{ok:true,consumed:true}` may present once.409/replay/stale/malformed/network
uncertainty never presents. Existing128-entry RAM attempted-request reservation,
session cancellation and body10s/whole-turn15s limits remain. Testing0.0.82
preparation gives Hunt and item/PF independent single-flight polls; requests
within each lane stay serialized. Maximum two simultaneous command polls.
Explicit unsupported V2 or exact empty legacy response backs off to unchanged V1
for five minutes; a V1 claim is never interpreted as a V2 command.

Presence adds `X-Gillions-Hunt-Focus: active_hunt_focus_v1` only for eligible
private Hunt sync. Its body is unchanged. Exact additive
`huntFocus:{contract:active_hunt_focus_v1,focused:boolean,expiresAt:UTC|null}`
is RAM-only, permission-gated and hard-expired locally at <=30s. Active lease
refreshes existing presence at12s, supported inactive probes at20s; failures keep
existing backoff. Focus skips personal in-flight work and prioritizes the existing
command polls at1s instead of5s, without faster Hunt/travel reads. Each lane
reserves one deadline before dispatch (no outer/inner deadline quantization).
Focus may shorten a routine wait, never existing30/60/300s failure backoff.
Missing/failed/false/OFF/session change stops priority, not ordinary Game Sync.

This cadence is preparation, not evidence of live latency acceptance. Initial
focus discovery still depends on unchanged presence; an unavailable network or
retry backoff cannot promise interactive delivery. Existing40-entry local
diagnostics add numeric command-stage timings and bounded failure summaries;
no upload, new telemetry store, tokens, account IDs or coordinates are added.
An eight-character request-ID prefix supports local correlation; existing
Dalamud logs keep their existing platform retention, separately from the40-entry
UI list. See V1 diagnostics disclosure; no full request IDs or claim tokens.
Consume acknowledgment precedes map invocation by design. A map API returning
true is invocation evidence, not a measurement of pixels becoming visible.

Testing82 evaluates response expiries on a request-scoped issuer clock ONLY
when Date comes from the normal authenticated, non-redirected exact TEST HTTPS
response. RFC Date's upper second bound plus the entire header round-trip gives
a conservative remaining lifetime; monotonic elapsed includes body/framework
work. The original raw wire expiry is unchanged. Claim90s/focus30s/whole-turn15s
limits are not widened. Focus translates remaining duration into RAM-only local
expiry and additionally expires monotonically (PC rollback cannot extend it).
Duplicate/malformed Date, >30s clock discrepancy or invalid/excessive RTT fails
closed. Missing Date preserves the previous strict local-clock policy for
compatibility; it does not add a TTL tolerance or insecure fallback. Presence
clock failure clears focus priority without breaking ordinary sync. The same
Testing response-clock handling covers existing item/PF expiries on exact TEST;
Stable and other origins retain their existing strict local-clock policy.

Site attribution recommendation remains **Data provided by xivpf.com**, with the
clickable xivpf.com link. No contribution contract or technical identifier change.
