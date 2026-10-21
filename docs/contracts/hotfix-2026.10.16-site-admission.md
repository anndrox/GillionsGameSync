# Site admission handoff: 7.57 hotfix runtime (Testing 0.0.90 / Public 1.0.31.6)

**For:** Site Engineering.

**Client source:** anndrox/GillionsGameSync#8 (draft, held for physical client
acceptance), on top of the merged compatibility table from #7.

The client changes **no payload shape, endpoint, header or permission**. It
reports its real runtime. It never rewrites versions to an older tuple, and it
doesn't widen anything to get past a rejection. When a request is rejected, it
stops or backs off and keeps its local state.

## Exact runtime tuple

| Field | Value |
|---|---|
| Game (`ffxiv` game data) | `2026.10.16.0000.0000` |
| Dalamud | `15.0.3.9`, revision `30509c3b4fd00edd9a3d65b7bbf16433d84be03b`, API level 15 |
| FFXIVClientStructs (`clientStructsVersion` / `sdkVersion` / `nativeVersion`) | `7.57.0.9414`, revision `dd57986b8287e7c6ed9c27198f8c76cb04a81dfe` |
| Lumina / Lumina.Excel | `7.7.0` / `7.5.1` |
| Collector versions | Testing `0.0.90.0` (product `GillionsGameSyncTest`, TEST only); Public `1.0.31.6` (product `GillionsGameSync`) |

## Affected Site checks

These were observed read-only in Site source `064f3aa1` (2026-10-20). Deployed
state is not verified.

| Client request | Payload fields the Site validates | Site source | Current Site value |
|---|---|---|---|
| `POST /api/game-sync/fates/capability`, then `POST /api/game-sync/fates/contribute` | the whole `acceptedSource` (`family`, `collectorVersion`, `gameVersion`, `dalamudApiLevel`, `dalamudVersion`, `dalamudRevision`, `clientStructsVersion`, `clientStructsRevision`, `luminaVersion`, `excelVersion`, `referenceGameVersion`); `referenceCompatibilityEpoch` `fate-reference:2026.10.16.0000.0000` | `fate-observations.mjs` `FATE_SOURCE(S)` | September tuple (15.0.3.6 / 7.56.2.9136); collector `0.0.86.0`, `0.0.87.0` and Public test versions |
| `POST /api/game-sync/sync` `hunt_bills`, item coverage v2 | `billItemCoverage.gameVersion`, `.sdkVersion`, `.collectorVersion` | `hunt-bill-coverage.mjs` | `2026.09.15.0000.0000` / `7.56.2.9136` |
| `POST /api/game-sync/sync` `hunt_bills` v1 | per-bill `gameVersion` → `reference('hunt-target', …, gameVersion)` | `personal-observations.mjs` with store references | references keyed by game version |
| `POST /api/game-sync/sync` `submarine_personal` | per-slot `gameVersion` → `reference('part'/'sector', …, gameVersion)` | `personal-observations.mjs` | references keyed by game version |
| `POST /api/game-sync/sync` `travel_context` | `gameBuild`, `nativeVersion` | `travel-context.mjs` `TRAVEL_GAME_BUILD` / `TRAVEL_NATIVE_VERSION` | `2026.09.15.0000.0000` / `7.56.2.9136` |
| Public optional features (personal, FATE, market, PF) | client product + version | `client-admission.mjs` | TEST: `1.0.31.1`, `1.0.31.3`; production: `1.0.31.2` |

## Expected client behaviour until admitted

- **FATE:** the capability discovery request is sent with the tuple above. A
  `FATE_SOURCE_UNSUPPORTED` reply keeps the client from reading FATEs or
  contributing.
- **Hunt coverage and travel:** a rejection marks the prepared input blocked, or
  backs off, without touching local history.
- **Hunt Progress and local retention:** these work without the Site.

## Status reported by Site Engineering

- **Forgejo `Gillions/FFXIV-Gillions` PR #98** turns the gates into exact lists
  and pins the rejections above in tests. It changes no admission.
- **PR #99 (draft HOLD)** adds only:
  - the exact pair `2026.10.16.0000.0000` / `7.57.0.9414` for travel and Hunt coverage;
  - TEST Public `1.0.31.6`. Production stays `1.0.31.2`.
- **FATE, Hunt v1 and submarines** additionally need a reviewed 2026.10.16
  reference/territory set and personal catalog, via Scribe or a reviewed
  statement that the rows are unchanged from 2026.09.15. Game Sync has not
  diffed those rows. The client only validates catalog shape at runtime, and
  that check fails closed.
- **Client acceptance:** Hunt Progress is physically accepted on this tuple.
  Submarines are not physically tested. FATE and travel were not exercised.

## Requested

The Site should admit, through its own separate review:
- the tuple above for the FATE source;
- Hunt coverage;
- travel;
- hotfix reference catalogs for Hunt and submarines;
- collector `0.0.90.0` on TEST;
- later, `1.0.31.6` where Public admission is intended.

Its permission, consent and reference-data checks stay as they are.
