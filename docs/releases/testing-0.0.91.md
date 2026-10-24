# Testing 0.0.91 — regression audit and accurate status

Status: **release candidate.** Its publication state is recorded separately once
the Testing release and feed are verified.

This release was qualified without owner-run in-game testing. Every change is
source-qualified and covered by synthetic, real-serializer and packaged tests.
Nothing here was physically tested in FFXIV.

## What changes

### Status rows say what is actually wrong

The main window and **Settings → Advanced** now distinguish these states:

| Label | Meaning |
|---|---|
| Game version not supported yet | Game Sync has not qualified this game/SDK build for the feature. No native reads happen. |
| Update required | Gillions refused this client version. |
| Not available on Gillions | Gillions has not admitted or acknowledged the feature for this client, or refused it. |
| Off on Gillions | The feature is switched off in your Gillions settings. |
| Off | The feature is switched off (local or legacy setting). |
| Waiting for supported context | Logged out, loading, outside a supported area, or no data observed yet. |
| Temporarily unavailable | No fresh permission from Gillions right now, for example while it is unreachable. |
| Ready | Supported, allowed and working. |

Fixed in particular:
- **Party Finder** always showed Ready when paired. It now shows the contribution
  state, which is Off by default.
- **Market** showed "Temporarily unavailable" for being off, for Gillions not
  acknowledging Market, and for real outages.
- **FATEs** showed "Update required" when Gillions had not admitted the current
  FATE source. It now shows "Not available on Gillions".
- **Hunts** show "Game version not supported yet", not "Update required", on an
  unqualified game. When Hunt Progress and private Hunts are both off, they show Off.
- **Update Game Sync** in the Testing build opens the Testing releases.

### Connection and website commands

- Only a request that reached, or failed to reach, Gillions can mark it
  unavailable. Local collection, validation and pairing-input failures can't.
- Authorization and client-version refusals show as "Authorization expired or
  revoked" or "Update required".
- The item-link poll includes Party Finder native requests only when PF links
  are allowed, so the Site's stricter PF admission can't refuse item links.
- A Stable presence response without explicit product acceptance keeps its
  permission lease; only Retainer uploads stay off. This changes Stable source
  only and reaches Stable users only in a future Stable release.

### Reliability

- An acknowledged upload that only refreshes a hash or timestamp no longer
  forces a full configuration save. Retired evidence still saves immediately.
- A failed configuration save is retried after 5 s, doubling up to 60 s,
  instead of on every frame. A Site denial and the unload flush still save at once.
- A repeating background fault is logged once a minute.
- A Gil flush refused by a loading screen retries after 2 s instead of every
  frame. A paired-client hydration that failed to reach Gillions is retried
  after 60 s.
- Retainer gear, inventory coverage and retainer gil are attributed only to a
  stably active retainer. Retainer gil is ignored until its container is loaded.
- Private Hunt and submarine uploads alternate which goes first, so a failing
  Hunt upload can't starve submarines.
- FATE capability discovery no longer repeats every 10 s while you are outside
  a supported area.
- Beastmaster diagnostics (Testing only) stop native reads on runtimes they were
  never qualified on (September only, like Dashboard).

Also included: the native observation integrity work merged after 0.0.90 (see
[native observation availability](../native-observation-availability.md)).

## Unchanged

- Consent and every default, including Hunt-routing location (default Off).
- Site permission leases, payload shapes, endpoints and cadences.
- The frozen transports: FATE, personal, travel, Market and PF links.
- Native compatibility for Hunt, Submarine, Travel and FATE.

## What Gillions still decides

Verified with Site Engineering against deployed TEST and Production:
- **FATE:** Gillions admits only the September FATE source. FATEs show
  "Not available on Gillions" on 2026.10.16 until it admits the current one.
- **Hunt coverage** for game 2026.10.16 is admitted on TEST only in Site main,
  not in the deployed build.
- **Private Hunt bills and submarines:** Gillions' reference catalog is pinned
  to 2026.09.15, so it refuses them on 2026.10.16.
- **Production** (Stable 1.0.31.2) had a Site proxy-trust misconfiguration. That
  blocked item links, Hunt maps and the Market acknowledgement. Site Engineering
  owns and is correcting it; a Testing release cannot fix it.

## Physical evidence

None for this version. These behaviours depend on the game and still need
checks in play:
- the retainer-switch timings;
- the inventory, saddlebag and currency availability checks in
  [native observation availability](../native-observation-availability.md).

Unsupported native features stay disabled rather than being assumed to work.
