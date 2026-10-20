# Native compatibility

`NativeCompatibility.cs` is the one place that decides on which exact client
runtimes the patch-sensitive collector families (Hunt, Submarine, Travel, FATE and
Dashboard) may read native game state. Each family lists the exact runtimes it was
qualified on. A runtime is the game data version Dalamud reports for `ffxiv`, plus
the Dalamud release and the FFXIVClientStructs assembly that release ships. For
those families, anything not listed fails closed before any of their native reads,
and qualifying one family never admits another.

The ordinary snapshot collectors (`DirectGameSnapshotCollector.cs`) are outside this
table. Their reads are guarded by per-source loaded/ready checks, and they rely on
Dalamud shipping ClientStructs rebuilt for each patch.

The table is native safety only. Player consent, Site authority, Site source
admission and capability negotiation stay where they were and are still required.

## Current table

| Family | Native layouts read | Qualified runtimes |
|---|---|---|
| Hunt (Hunt Progress, Hunt bills, Hunt item coverage) | `MobHunt`, `InventoryManager`/`InventoryContainer`/`InventoryItem` (Key Items), `PlayerState` | 2026.09.15 / 15.0.3.6; 2026.10.06 / 15.0.3.7 |
| Submarine | `HousingManager`, `WorkshopTerritory` and submersible data, `AgentSubmersibleExploration(Result)`, `AgentInterface`, `PlayerState` | 2026.09.15 / 15.0.3.6 |
| Travel (Hunt routing location) | `AgentTeleport`, `Telepo`/`TeleportInfo`, `UIState` aetherytes, `AtkUnitBase`, `AgentInterface` | 2026.09.15 / 15.0.3.6 |
| FATE | Dalamud `IFateTable` (maintained `FateContext`) | 2026.09.15 / 15.0.3.6; 2026.10.06 / 15.0.3.7 |
| Dashboard (Testing-only, local) | many UI/agent/content structs | 2026.09.15 / 15.0.3.6 |

These are exactly the admissions that existed before the table. Introducing it
changed no behaviour.

| Runtime | Dalamud | ClientStructs |
|---|---|---|
| September | 15.0.3.6 `b666d821a47306fb447c60155b5d99377f91a5ee` | 7.56.2.9136 `313161e448e335adddd928f8a0212b2c328b5659` |
| October | 15.0.3.7 `c9dacb095340ebe6513f13af6c38b13517ae70e4` | 7.57.0.9378 `78975d8306c2766250ee4dca1925ef81f5dfd4f7` |

Both ship Lumina 7.7.0 and Lumina.Excel 7.5.1.

## Layout fingerprints

`tests/GillionsGameSync.ConfigurationTests` has two evidence modes:
- `--native-layout-report <dalamud directory>` prints fingerprints;
- `--native-layout-check <dalamud directory>` fails unless they match the recorded values.

For exactly the members each collector reads, the fingerprint hashes:
- struct sizes, and field offsets, types and fixed-size-array attributes;
- the enum constants the plugin compiles in (e.g. `InventoryType.KeyItems = 2004`);
- property and method signatures;
- member-function and static-address signature strings;
- agent identities and `Inherits` relations.

It does not hash accessor bodies, because IL metadata tokens differ between
unrelated builds. Qualification therefore also needs a byte-identical (or reviewed
additive-only) ClientStructs source comparison for the files a family reads.

Recorded values, measured on the official 15.0.3.6 and 15.0.3.7 archives:

| Family | 15.0.3.6 | 15.0.3.7 |
|---|---|---|
| Hunt | `2312d897d65b4177` | `2312d897d65b4177` |
| Submarine | `8c4e37e328c571a9` | `8c4e37e328c571a9` (not admitted) |
| Travel | `22c25911f7cc88bf` | `22c25911f7cc88bf` (not admitted) |
| FATE | `6923c1e09b662322` | `6923c1e09b662322` |

On every run, CI inspects the pinned runtime inside the packaged tests and re-proves
the earlier archive with `--native-layout-check`.

These are reference evidence, not physical proof. Game data or behaviour can differ
in ways ClientStructs does not describe.

## Site dependencies (outside this repository)

The Site validates its own exact tuples. As of its 2026-10-20 source, it accepts
only the September tuple (`2026.09.15.0000.0000` / `7.56.2.9136`) for:

- **FATE source admission:** `fate-observations.mjs`. It also requires the exact Dalamud revision and plugin collector version.
- **Hunt item coverage:** `hunt-bill-coverage.mjs`.
- **Travel context:** `travel-context.mjs`.
- **Hunt v1 and submarine personal observations:** these are checked against Site reference catalogs by game version.

The Site also admits Public versions individually. So a client qualification alone
restores local Hunt Progress and local retention. Uploads need separate Site
admission of the runtime and plugin version; until then the Site rejects them and
the client backs off.

## Patch-day procedure

1. **Identify the runtime players will run.**
   - Dalamud release `version` at the newest `goatcorp/dalamud-distrib`
     "Promoting for … from stg" commit.
   - The `FFXIVClientStructs.dll` version and `+revision` inside that commit's
     root `latest.zip`.
   - The game build from ClientStructs `ida/data.yml` (and `game/ffxivgame.ver`
     on a patched client).
2. **Compare sources.**
   ```bash
   gh api repos/aers/FFXIVClientStructs/compare/<last-qualified>...<new> --jq '.files[].filename'
   ```
   Check the result against the files each family reads. Review any change to
   them before going further.
3. **Compare binaries.** Extract the new archive and run the layout report:
   ```bash
   dotnet run --project tests/GillionsGameSync.ConfigurationTests -c Release -- --native-layout-report <dir>
   ```
   An unchanged family fingerprint plus a byte-identical (or reviewed
   additive-only) source diff qualifies that family at source level. A changed
   fingerprint means reading the diff and the collector code first.
4. **Update the table and CI together.**
   - Add the runtime to `NativeCompatibility.cs` for the qualified families only.
   - Add the measured values to `NativeCompatibilityPackagedTests.Expected`.
   - Pin `.github/workflows/verify.yml` to the promotion commit's root `latest.zip`
     and its SHA-256.
   - Keep earlier listed runtimes in the workflow's layout recheck step.
5. **Build a Testing candidate** and run the owner in-game procedure from its
   release notes on the release Dalamud channel. A new runtime stays *physical
   client pending* until that passes.
6. **Tell Site Operations** the exact tuple and collector version for each
   resource the Site validates. Release Stable only after the Testing check and
   the Site admission.
