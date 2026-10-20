# Native compatibility

`NativeCompatibility.cs` is the one place that decides on which exact client
runtimes the patch-sensitive collector families (Hunt, Submarine, Travel, FATE and
Dashboard) may read native game state. Each family lists the exact runtimes it was
qualified on. A runtime is the game data version Dalamud reports
for `ffxiv`, plus the Dalamud release and the FFXIVClientStructs assembly that
release ships. For those families, anything not listed fails closed before
any of their native reads, and qualifying one family never admits another.

The ordinary snapshot collectors (`DirectGameSnapshotCollector.cs`) are outside this
table. Their reads are guarded by per-source loaded/ready checks, and they rely on
Dalamud shipping ClientStructs rebuilt for each patch.

The table is native safety only. Player consent, Site authority, Site source
admission and capability negotiation stay where they were and are still required.

## Current table

| Family | Native layouts read | Qualified runtimes |
|---|---|---|
| Hunt (Hunt Progress, Hunt bills, Hunt item coverage) | `MobHunt`, `InventoryManager`/`InventoryContainer`/`InventoryItem` (Key Items), `PlayerState` | 2026.09.15 / 15.0.3.6; 2026.10.06 / 15.0.3.7; **2026.10.16 / 15.0.3.9** |
| Submarine | `HousingManager`, `WorkshopTerritory` and submersible data, `AgentSubmersibleExploration(Result)`, `AgentInterface`, `PlayerState` | 2026.09.15 / 15.0.3.6; **2026.10.16 / 15.0.3.9** |
| Travel (Hunt routing location) | `AgentTeleport`, `Telepo`/`TeleportInfo`, `UIState` aetherytes, `AtkUnitBase`, `AgentInterface` | 2026.09.15 / 15.0.3.6; **2026.10.16 / 15.0.3.9** |
| FATE | Dalamud `IFateTable` (maintained `FateContext`) | 2026.09.15 / 15.0.3.6; 2026.10.06 / 15.0.3.7; **2026.10.16 / 15.0.3.9** |
| Dashboard (Testing-only, local) | many UI/agent/content structs | 2026.09.15 / 15.0.3.6 only (not requalified) |

Exact identities:

| Runtime | Dalamud | ClientStructs |
|---|---|---|
| September | 15.0.3.6 `b666d821a47306fb447c60155b5d99377f91a5ee` | 7.56.2.9136 `313161e448e335adddd928f8a0212b2c328b5659` |
| October | 15.0.3.7 `c9dacb095340ebe6513f13af6c38b13517ae70e4` | 7.57.0.9378 `78975d8306c2766250ee4dca1925ef81f5dfd4f7` |
| October hotfix | 15.0.3.9 `30509c3b4fd00edd9a3d65b7bbf16433d84be03b` | 7.57.0.9414 `dd57986b8287e7c6ed9c27198f8c76cb04a81dfe` |

All three ship Lumina 7.7.0 and Lumina.Excel 7.5.1. Dalamud 15.0.3.8
(`f7d06f36`, ClientStructs 7.57.0.9386 `d6397a76`) predated the hotfix game
data. Its layouts match too, but it is not listed, because 15.0.3.9 replaced it
on the release channel within hours of the hotfix.

## Evidence for the 2026.10.16 qualification

1. **Maintained sources.** Every ClientStructs file the Hunt, Submarine, Travel
   and FATE families read is byte-identical between `78975d83` (15.0.3.7),
   `d6397a76` (15.0.3.8) and `dd57986b` (15.0.3.9). The files compared were
   MobHunt, InventoryManager, InventoryContainer, InventoryItem, PlayerState,
   HousingManager, HousingTerritory, WorkshopTerritory,
   AgentSubmersibleExploration, AgentInterface, AgentTeleport, Telepo, UIState,
   AtkUnitBase, FateManager and FateContext (the struct Dalamud's `IFateTable`
   wraps; also unchanged since September). Among the C# sources, the maintainers'
   "7.57 hotfix1" signature pass changed only `PacketDispatcher`.
2. **September → current changes for Submarine and Travel.** All of them are
   additive or cosmetic:
   - `HousingManager.RequestWardInfo` and `PlayerState.GetCurrentClassJobContentMaxLevel` were added;
   - `AgentTeleport.ContextMenuAetheryteIndex` was documented at `0x58` (the plugin reads `0x5C`/`0x60`/`0x68`);
   - two `AtkUnitBase` parameters were renamed;
   - `Telepo` doc comments were removed.
3. **Shipped binaries.** `tests/GillionsGameSync.ConfigurationTests` has a
   `--native-layout-report <dalamud directory>` mode. For exactly the members each
   collector reads, it hashes:
   - struct sizes, and field offsets, types and fixed-size-array attributes;
   - the enum constants the plugin compiles in (e.g. `InventoryType.KeyItems = 2004`);
   - property and method signatures;
   - member-function and static-address signature strings;
   - agent identities and `Inherits` relations.

   It does not hash accessor bodies, because IL metadata tokens differ between
   unrelated builds. The byte-identical source comparison in item 1 covers those.

   Against the official 15.0.3.6, 15.0.3.7, 15.0.3.8 and 15.0.3.9 release archives,
   every family's fingerprint was identical, with no missing member:
   - Hunt `2312d897d65b4177`
   - Submarine `8c4e37e328c571a9`
   - Travel `22c25911f7cc88bf`
   - FATE `6923c1e09b662322`

   On every run, CI inspects the pinned runtime and also re-proves the recorded
   values against the 15.0.3.6 and 15.0.3.7 archives with `--native-layout-check`.
4. **Dalamud 15.0.3.7 → 15.0.3.9.** The service changes don't affect the APIs
   used. `AddonArgs.Addon` became an init property with the same getter, and
   there were no `IFateTable`, `IPlayerState`, `IClientState` or `IGameInventory`
   changes.

This is reference evidence. It is not physical proof: game data or behaviour can
still differ in ways ClientStructs doesn't describe. Each newly listed runtime
stays **physical client pending** until the owner procedure in the release notes
passes.

## Site dependencies (outside this repository)

Exact hotfix tuple, endpoints and fields for Site Engineering: [hotfix Site admission handoff](contracts/hotfix-2026.10.16-site-admission.md).

The Site validates its own exact tuples. As of its 2026-10-20 source, it still
accepts only the September tuple (`2026.09.15.0000.0000` / `7.56.2.9136`) for:

- **FATE source admission:** `fate-observations.mjs`. It also requires the exact Dalamud revision and plugin collector version.
- **Hunt item coverage:** `hunt-bill-coverage.mjs`.
- **Travel context:** `travel-context.mjs`.
- **Hunt v1 and submarine personal observations:** these are checked against Site reference catalogs by game version.

The Site also admits Public versions individually. So on the hotfix, local Hunt
Progress and local retention can work from this client alone. Uploads of the
affected resources need separate Site admission of the new runtime and plugin
version; until then the Site rejects them and the client backs off.

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
   Check the result against the files in the table above. Review any change to
   them before going further.
3. **Compare binaries.** Extract the new archive and run the layout report:
   ```bash
   dotnet run --project tests/GillionsGameSync.ConfigurationTests -c Release -- --native-layout-report <dir>
   ```
   An unchanged family fingerprint plus a byte-identical
   (or reviewed additive-only) source diff qualifies that family at source level. A changed fingerprint means reading the diff and the
   collector code before anything else.
4. **Update the table and CI together.**
   - Add the runtime to `NativeCompatibility.cs` for the qualified families only.
   - Add the measured values to `NativeCompatibilityPackagedTests.Expected`.
   - Add the archive to the workflow's layout recheck step when an earlier runtime stays listed.
   - Pin `.github/workflows/verify.yml` to the promotion commit's root
     `latest.zip` and its SHA-256.
5. **Build a Testing candidate** and run the owner in-game procedure from its
   release notes on the release Dalamud channel.
6. **Tell Site Operations** the exact tuple and collector version for each
   resource the Site validates. Release Stable only after the Testing check and
   the Site admission.
