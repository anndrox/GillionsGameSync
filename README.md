# Gillions Game Sync

Gillions Game Sync is the open-source Dalamud plugin for [Gillions](https://gillions.app). Published stable `1.0.29` lets a player pair one FFXIV character with their Gillions account and synchronize the data categories they explicitly enable. The unreleased source candidate removes venture planning and AutoRetainer integration; published packages remain unchanged until a separately approved release.

The complete Collectibles snapshot includes authoritative Master Recipe Book and Regional Folklore tome unlocks. Folklore ownership is read from the game client's unlock state and sent as stable tome item IDs; it is never inferred from inventory, gathering logs, mounts, or other collections.

The current source candidate keeps native Retainer observations, venture results, inventory, listings and ordinary synchronization. It no longer discovers or invokes AutoRetainer, polls for venture plans, or applies/restores plans. Existing local backups and ownership records survive as inert configuration data; no recovery UI or export is added. Previously cached item-level, Gathering, Perception and venture-start information remains historical. Already-installed external plans may continue under AutoRetainer's own settings.

Retainer uploads still require server acceptance of the client product and contract. The plugin does not capture packets, accept inbound network connections, read Square Enix credentials, or depend on another plugin for core synchronization. Historical stable `1.0.29` behavior is recorded in the changelog.

## Install

Add the stable custom repository URL to Dalamud:

`https://raw.githubusercontent.com/anndrox/GillionsGameSync/main/data/GillionsGameSync.json`

The testing feed is intentionally separate and should be installed only when a Gillions test is requested:

`https://gillions.app/plugins/GillionsGameSyncTesting.json`

## Pair and sync

- `/gillionssync pair` pairs the plugin after you enter the one-time code shown by Gillions.
- `/gillionssync` opens the plugin and can run a manual sync.
- Automatic sync checks one scheduled category every 30 seconds. Inventory uses a short event-driven debounce, and captured Gil Ledger events are queued promptly.

Only enabled categories are sent. Data that is not authoritatively loaded is preserved or omitted rather than reported as empty.

## Build and verify

Requirements:

- Windows
- .NET 10 SDK
- Access to the Dalamud NuGet packages used by `Dalamud.NET.Sdk`

Run the complete local verification:

```powershell
./scripts/verify.ps1
```

Create a local package without publishing it:

```powershell
./scripts/package.ps1 -Channel stable -Version 1.0.29
./scripts/package.ps1 -Channel testing -Version 0.0.62
```

Packages are written below `artifacts/`, which is ignored by Git. Stable publication uses an immutable GitHub Release asset. The separately identified testing plugin remains on the controlled Gillions testing feed.

## Source and releases

`main` is the public stable source line. Experimental and release-candidate work is developed and validated separately before promotion. The reviewed [`data/GillionsGameSync.json`](data/GillionsGameSync.json) on `main` is the canonical stable Dalamud repository manifest. Stable ZIPs are immutable [GitHub Release assets](https://github.com/anndrox/GillionsGameSync/releases), their SHA-256 records live under [`data/releases`](data/releases), and the public icon lives under [`assets`](assets). A stable release updates the manifest in the same reviewed source history and validates the tag, download, checksum, and package before promotion. This removal candidate does not update the published manifest, version, tags or release assets. Retained observation uploads continue to require the applicable server product/channel and account/device acceptance.

See [Privacy](docs/privacy.md), [Testing](docs/testing.md), [Releasing](docs/releasing.md), and [Contributing](CONTRIBUTING.md).

## License

Gillions Game Sync is licensed under the [MIT License](LICENSE).
