# Site scope minimums v1

`site-scope-minimums-v1.json` lists, for one game build, the lowest published Game
Sync version on each channel that can serve each Site-controlled scope. Game Sync
owns this file. Site reads it so that its own pages can show a notice such as
"Private Hunt bills need 1.0.31.6".

## Format

- `schemaVersion`: always `1`.
- `gameVersion`: the exact `ffxiv` game data version the table applies to.
- `scopes`: one entry per Site permission key (`PermissionAuthority.Keys`), plus
  `fate`. Each entry has a `stable` value (product `GillionsGameSync`) and a
  `testing` value (product `GillionsGameSyncTest`).
- Each value is an exact four-part dotted version, compared numerically, or
  `null` when no published version on that channel serves the scope for this game
  build.
- Unknown scopes are ignored. A missing scope means no version notice.

## Meaning and limits

- A minimum is a **client** fact only. It does not show that the Site admits the
  version, the game build or the source data for that scope. It also does not
  show that the member has consented. Those are separate Site gates.
  - Example: travel (`huntRoutingLocation`) is served by Stable 1.0.31.6, but it
    stays unavailable until the Site admits 2026.10.16 travel.
  - Example: FATE also needs the Site and Scribe to admit the 2026.10.16 FATE
    source.
- **Game-build scopes** (`personalHunts`, `personalSubmarines`,
  `huntRoutingLocation`, `fate`) read native game state. They are served only
  where `NativeCompatibility.cs` qualifies the native family for this
  `gameVersion`:
  - families: Hunt, Submarine, Travel and Fate;
  - first Testing build with that qualification: 0.0.90;
  - first Stable build: 1.0.31.6.
  - Stable 1.0.31.2 supports only 2026.09.15.
- **Other scopes** do not depend on the game build. Their floor is the oldest
  version in the current release lines that understands all eight permission
  keys:
  - Stable: 1.0.31.2.
  - Testing: 0.0.88, the oldest release in the current Testing distribution
    repository.
- Known older-version defect: before Testing 0.0.91 and Stable 1.0.31.6, turning
  on Party Finder links could also block item links. The floor stays at 1.0.31.2
  because item links still work there with Party Finder links off.

## Consuming it

Site pins a Game Sync commit SHA and this file's SHA-256 at each Site release. It
vendors the file into the Site build and never fetches it at runtime. Game Sync
updates the file, through review and CI, whenever:
- a release changes a minimum;
- a native family is qualified for a new game build.

`tests/site-scope-minimums-contract-test.py` (run by `scripts/verify.ps1`) checks
the shape and the scope keys. It requires every game-build scope to be `null`
unless its native family is qualified for `gameVersion`. It also requires every
version to be no newer than the current project versions.
