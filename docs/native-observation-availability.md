# Native observation availability

The Site replaces several ordinary-sync resources wholesale. Measured against
Site source `a89db308`:
- `inventory` deletes every stored item not present in the upload;
- `currencies` deletes all balances and inserts the upload;
- `character` replaces jobs, gear, recipes and fish.

An upload built from a native source that was **not ready** would therefore
delete valid data. Game Sync never sends unavailable data as an empty or zero
result. It **withholds** the resource, and the Site keeps its last accepted value.

## States

`NativeAvailability` in `NativeObservationPolicy.cs`:

| State | Meaning | Upload |
|---|---|---|
| `Observed` | The native source is loaded for the current character | Sent; an empty or zero value is genuine |
| `NotLoaded` | The container, state or catalog is not loaded yet | Withheld |
| `StaleContext` | The local player, name or world is not available (loading screen, logout) | Withheld |

Withheld resources are listed in diagnostics. After a manual sync the main window
says which resources were not ready and were kept unchanged on Gillions. A local
precondition — a sync already running, no character, a loading screen — is never
reported as a connection failure.

## Per resource

### inventory

- **Withheld unless every resident container is loaded:**
  - `Inventory1`–`4`;
  - `EquippedItems`;
  - `Crystals`;
  - the thirteen `Armory*` containers.

  An unloaded resident container would otherwise let the Site delete its items.
- **Saddlebags** (`SaddleBag1/2`, `PremiumSaddleBag1/2`) load only after the player opens them in a session.
  - A loaded read is remembered per owned character (pairing generation + content id, at most 64 rows per container).
  - While a saddlebag is unloaded, its last loaded contents are carried with `source: "native_client_state_last_observed"`, instead of letting the Site delete them.
  - A saddlebag that was loaded and empty is a real empty observation.
  - A saddlebag never seen for this character is left out, as before.
- **Additive fields** (older Sites ignore them):
  - `observedLocations`
  - `carriedLocations`
  - `unobservedLocations`
  - `retainerBagRetainerIds`
- Item rows keep the established shape: `itemId`, `quantity`, `hqQuantity`, `equippedQuantity`, `location`, `source`.

### currencies

- Withheld while the `Currency` container is not loaded.
- A loaded container with no positive balance sends `items: []`, a genuine "all zero".

### gil (ledger)

- `ReadGil` is null until the `Currency` container is loaded.
- A 0 read before loading would otherwise become the baseline, and the real balance a phantom income event.

### retainer bags and listings

- The game keeps retainer pages and the selling list resident after the player leaves a bell or switches retainers.
- They are attributed only to the active retainer, after it has stayed active for 1.5 s (`RetainerContextStability`).
- Otherwise they are not observed, and the Site keeps the prior per-retainer data.
- `retainerBagRetainerIds` names retainers whose pages were read, including empty ones.

### character

- Withheld unless all of these are loaded:
  - PlayerState;
  - the equipped container;
  - the ClassJob, Recipe and FishParameter catalogs.

### armoire

- An unavailable Cabinet catalog is retried on the next read, not cached as empty.

## Unchanged

- Hunt, submarine, FATE and travel collectors.
- The per-collector native compatibility table.
- Consent, Site permission leases and capability negotiation.
- Upload endpoints.
- Collectible readiness.

## Known limits

- **Guard reach:** the identity and loading-screen guard applies to every ordinary
  sync resource, including `shared_fates`, `retainer_ventures` and gil-ledger
  uploads. They are retried on the next cadence.
- **Hydration retry:** a paired-client hydration that cannot run stays pending, with
  a 5-second backoff.
- **Never-seen saddlebags:** a saddlebag this client has never seen loaded for the
  current pairing (e.g. after re-pairing or on a new PC) is still left out. The
  current Site then deletes those items until the player opens the saddlebag.
  `unobservedLocations` names them, so the Site can stop doing that.

- **Stale carried contents:** carried saddlebag contents are this client's last loaded read. If the same character changes its saddlebags on another PC, this client keeps sending its older contents until it next opens them.
- **Emptied retainers:** a retainer emptied completely can't be cleared on the Site from `retainerBags` alone, because the Site replaces only retainers that have item rows. `retainerBagRetainerIds` lets the Site handle that later.

## Saving

Timestamp-only freshness saves now happen at most every 5 minutes, instead of
every 30 seconds. Durable changes still save on the next tick.

A full configuration save measured 19 ms median and 29 ms p95 at 1.5 MB, and
10 ms / 17 ms at 0.8 MB. That is Dalamud's serializer plus the file write, on
the development PC, with synthetic content. In-game frame cost is not measured.

## Sync Now

A manual multi-resource sync collects one resource per framework tick
(`IFramework.RunOnTick`), re-checking the permit each tick. It no longer walks
every native source in one frame. Automatic sync already collected one resource
per 30 s.

## Physical checks still required

These are owner checks in the game. Fixture tests cannot prove them.

1. **Saddlebag and inventory.** Log in, then Sync now without opening the saddlebag.
   - The saddlebag-only items on Gillions are not deleted. Before any first opening
     they are simply not updated.
   - Open the saddlebag, move one item, and Sync now. The change appears.
   - Relog without opening the saddlebag and Sync now. The saddlebag items remain.
2. **Currency and gil.** Sync now right after login.
   - Currencies on Gillions are never blanked.
   - No full-balance gil "income" appears in the ledger after login.
3. **Retainers.** At a bell, open retainer A, then retainer B, waiting a few seconds
   on each.
   - Each retainer's bags and listings on Gillions match that retainer.
   - Nothing from A appears under B after switching.
4. **Loading screen.** Press Sync now during a teleport.
   - The message asks you to wait for the loading screen.
   - The connection status stays connected.
5. **Repeated Sync now.** Use `/gillionssync sync` twice quickly. The second one
   says a sync is already in progress, not "Gillions unavailable".
6. **Smoothness.** During ordinary play with the plugin enabled, watch `/xlstats` for
   Game Sync framework time. Report periodic hitches.
