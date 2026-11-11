# Gillions Game Sync

Gillions Game Sync is a Dalamud plugin for FINAL FANTASY XIV. Pair your character with [Gillions](https://gillions.app) to see your gear, collections and progress on the website. The plugin reads game data without changing it.

## What it shares

Sharing through Game Sync is optional. You choose what to share in Game Sync settings on [gillions.app](https://gillions.app/gillions-sync), and you can turn sharing off at any time. Some categories need a compatible game version or only update after you open the relevant window in game.

**On when you pair (you can turn these off):**

- Inventory, gear and currencies
- Retainers and ventures
- Glamour plates and dye colours
- Achievements, collectibles, quests and character progress, including reputation and Shared FATE progress
- Gil ledger
- Hunt progress
- Submarine voyages
- Market listings and recent sales you see

Hunt progress, submarine voyages and market updates each have their own switch in Game Sync settings. The rest stop when you disconnect your device.

**Off until you turn them on:**

- Travel and location
- Live FATEs
- Character appearance (Testing only)
- **Party Finder (optional):** Party Finder listings you see in game, including other players' public listings, are sent to Gillions, which passes them on to xivpf.com.

Testing also has a Beastmaster view that stays on your device and does not upload data.

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

## Support

Visit [Gillions Help](https://gillions.app/help) for setup and troubleshooting.

## For developers

See the [developer docs](docs/README.md) for building, testing, compatibility and releases. [MIT licence](LICENSE).
