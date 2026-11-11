# Gillions Game Sync

Gillions Game Sync is a Dalamud plugin for FINAL FANTASY XIV. Pair your character with [Gillions](https://gillions.app) to see your gear, collections and progress on the website. The plugin reads game data without changing it.

## What it shares

You choose each sharing category in [Game Sync settings on gillions.app](https://gillions.app/gillions-sync). Supported data includes inventory, gear and currencies; retainer items and ventures; glamour plates and dye colours; achievements, collectibles and quest progress; and Gil changes for the ledger. Some data is available only after you open the relevant window in game.

## Install

In Dalamud, go to **Settings > Experimental > Custom Plugin Repositories**. Add the URL for your channel and save. Then open the plugin installer and install the matching plugin.

### Stable

Install **Gillions Game Sync**. In game, open `/gillionssync` and follow the pairing steps on [gillions.app](https://gillions.app/gillions-sync).

```text
https://raw.githubusercontent.com/anndrox/GillionsGameSync/main/data/GillionsGameSync.json
```

### Testing (early builds)

Use Stable for everyday play. For early builds, install **Gillions Game Sync Testing** from the separate Testing feed. Read the [Testing instructions](https://github.com/anndrox/GillionsGameSync-Testing#readme) before switching. Enable only one copy at a time.

```text
https://raw.githubusercontent.com/anndrox/GillionsGameSync-Testing/main/data/GillionsGameSyncTesting.json
```

<!-- Legacy Testing feed (frozen, kept here for the existing manifest contract): https://github.com/anndrox/GillionsGameSync/releases/download/v0.0.64-testing/GillionsGameSyncTesting.json -->

## Screenshots

Screenshots of the plugin window and sharing settings will go here.

## Support

Visit [Gillions Help](https://gillions.app/help) for setup and troubleshooting.

## For developers

See the [developer docs](docs/README.md) for building, testing, compatibility and releases. [MIT licence](LICENSE).
