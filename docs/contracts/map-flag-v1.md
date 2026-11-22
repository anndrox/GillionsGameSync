# Website map flags

Testing candidate 0.0.114.0. In-game TEST acceptance is pending the Site side
being available and the PP arranging a flag in another zone.

The device permission key is `mapFlags`, labelled "Map flags from the site",
with scope `server:game-sync:receive:map-flag:v1`. The plugin setting
`EnableMapFlagRequests` starts On for new and upgraded Testing configurations.
A saved local Off blocks delivery. A fresh Site permission must also permit
delivery. A missing permission does not authorise a flag.

## Poll

The existing WebsiteLinksEnabled item-link lane polls
`/api/game-sync/item-links/poll`. Its ordinary cadence is five seconds,
with the existing Hunt focus scheduling and failure backoff.

When map flags are enabled and the character is ready, the body includes
`map_flag` in `requestTypes` and `map_flag_v1` in
`nativeRequests.capabilities`. For map flags alone the body is

```json
{
  "capability": "native_item_link",
  "pluginVersion": "0.0.114.0",
  "requestTypes": ["map_flag"],
  "nativeRequests": {
    "contract": "native-requests-v1",
    "contractVersion": 1,
    "capabilities": ["map_flag_v1"]
  }
}
```

Item and Party Finder permissions independently add `item` and
`party_finder` to the type list. Enabled Party Finder delivery also adds
`native_party_finder_link_v1` to the capability list. Disabling map flags
retains the existing item/PF body.

## Reply and consume

The reply envelope has `ok: true` and `request`. It may also carry the
existing `nativeRequests` acknowledgement, with contract, contractVersion,
acceptedClientProduct and capabilities, including `map_flag_v1`.

The request has exactly these fields.

| Field | Accepted value |
|---|---|
| requestType | `map_flag` |
| requestId | Non-empty UUID in D format |
| claimToken | 16 through 100 ASCII letters, digits, underscores or hyphens |
| expiresAt | UTC timestamp ending Z, later than issuer time, at most 90 seconds ahead |
| territoryId, mapId | Positive uint IDs |
| mapX, mapY | Finite numbers in 0 through 100 |
| label | 1 through 120 characters under Hunt's plain-text rules |
| source | `vendor`, `quest`, `card_match` or `fate` |

Unknown, duplicate, missing or mistyped fields are rejected. Hunt and map flags
share one thread-safe FIFO of 128 attempted IDs. Attempted claims survive
local setting changes and expire from memory through FIFO eviction or a
session reset. A failed or lost consume response cannot cause a retry.

Before consuming, the framework thread checks the Map and TerritoryType
sheets. Both rows must exist, SizeFactor must be positive, and the Map's
TerritoryType must equal territoryId. The consume POST to
`/api/game-sync/item-links/consume` has exactly requestType, requestId and
claimToken. Presentation requires HTTP 200 with `{"ok":true,"consumed":true}`.

The framework thread rechecks the session, consent, expiry, logged-in state,
both BetweenAreas flags and map rows before opening the flag through
`IGameGui.OpenMapWithMapLink`. A successful open prints
"Gillions: flagged <label> on your map."
