# Gillions Game Sync

Gillions Game Sync is the open-source Dalamud plugin for [Gillions](https://gillions.app). This source targets `1.0.30`, with read-only character and Retainer synchronization, safer pairing and offline records, and a simpler settings window. The [stable manifest](data/GillionsGameSync.json) on `main` and [GitHub Releases](https://github.com/anndrox/GillionsGameSync/releases) identify the publicly available build; a task branch or local package is not a published update.

Automatic sync is one global control for the supported categories: inventory, currencies, achievements, collectibles, character progress, quest journal, reputation, Shared FATEs and glamour plates. Retainer observations and venture results use a separate server compatibility acknowledgement. There is no per-category chooser. Unavailable game data is preserved or omitted instead of being reported as an intentional deletion.

Collectibles include authoritative Master Recipe Book and Regional Folklore tome unlocks. The plugin reads tome ownership from native unlock state and sends stable tome item IDs; it does not infer ownership from inventory or unrelated collections.

Version `1.0.30` removes venture planning, AutoRetainer discovery and IPC, plan delivery, and apply/restore controls. Native Retainer observations, inventory, listings and venture results remain. Older local plans, backups, queues and maps stay preserved but inactive when their account ownership cannot be proved. This update does not modify plans already installed in AutoRetainer or add a history recovery/export feature.

## Install and update

Add the stable custom repository URL to Dalamud:

`https://raw.githubusercontent.com/anndrox/GillionsGameSync/main/data/GillionsGameSync.json`

Keep that URL when updating through Dalamud's plugin installer. Existing users must **pair once after updating to `1.0.30`** because older credentials did not record a verified issuing origin. Do not delete the configuration. Confirm the pairing destination shown in the plugin, enter a fresh one-time code from Gillions, and choose **Pair this device**. Website history is unchanged; older local records with unverified ownership will not be assigned to the newly paired account or uploaded automatically.

The testing feed is separate and should be installed only when a Gillions test is requested:

`https://gillions.app/plugins/GillionsGameSyncTesting.json`

Updating this plugin does not cancel external AutoRetainer plans or guarantee cancellation of callbacks already queued by an older loaded plugin. Use AutoRetainer's own controls when you need that automation to stop. See the [release notes](docs/releases/v1.0.30.md).

## Pair and sync

- Open the plugin's Dalamud configuration window, or use `/gillionssync pair` to open its pairing controls.
- `/gillionssync` opens the window and requests a manual sync. **Sync now** also collects the supported data currently available to the logged-in character.
- **Automatic sync** checks one ordinary category every 30 seconds. Inventory changes, queued Gil records and changed Retainer observations use their own deadlines. The two-second Gil fallback and 750 ms dirty delay are unchanged.
- Pairing and startup with a valid pairing perform one character sync and presence request even when Automatic sync is off. Recurring collection remains off in that case.
- **Allow website 'Link in game' requests** is a separate control, on by default. While paired and logged in, it polls for authenticated requests and prints a requested native item link after the server consumes its claim. It does not automate gameplay.

Connection details, update history, data status and diagnostics are collapsed by default. The pairing destination and actionable account/storage warnings remain visible. Editing the HTTPS server address changes the destination for the next pairing; an existing device credential stays bound to its original origin. Disconnecting clears that credential while preserving local history.

## Offline records

New pending evidence is owned by a pairing generation and authoritative character content ID. Acknowledgements retire only the exact versions sent. Switching characters, pairing again, opting out of a request mode, or unloading the plugin cancels the affected work and prevents stale replies from changing a new session.

Pending data across all new pairing generations and characters shares a limit of **10,000 records or 8 MiB of serialized data**, whichever is reached first. At capacity the plugin keeps admitted records, pauses new event recording and shows a coverage-gap warning. Native Gil observations keep their existing cadence. After acknowledged uploads free space, recording starts from a fresh balance baseline; missed events are not invented or recovered.

Re-pairing does not free storage or authorize uploading another generation's records. Inactive or uncertain records may continue to occupy the limit. Legacy unowned history remains outside this new budget, and the budget does not promise a maximum total configuration-file size. Synthetic fixtures measure storage accounting; they do not establish hours of player coverage. See [Privacy](docs/privacy.md).

## Build and verify

Requirements are Windows, .NET 10 SDK and the Dalamud dependencies used by `Dalamud.NET.Sdk`.

```powershell
./scripts/verify.ps1
./scripts/package.ps1 -Channel stable -Version 1.0.30
./scripts/package.ps1 -Channel testing -Version 0.0.0
```

Verification includes synthetic policy tests, source contracts, both product builds and actual Dalamud configuration load/Save/reload fixtures. It does not run a game session. Packages stay under ignored `artifacts/`; building does not publish or install them.

## Source and releases

`main` is the public stable source line. The reviewed [`data/GillionsGameSync.json`](data/GillionsGameSync.json) is the stable Dalamud manifest. Stable ZIPs are immutable [GitHub Release assets](https://github.com/anndrox/GillionsGameSync/releases), checksums live under [`data/releases`](data/releases), and the icon lives under [`assets`](assets). Candidate preparation, independent review, publication and a user's installed version are separate states. Testing retains its own product identity and publication path.

See [Testing](docs/testing.md), [Releasing](docs/releasing.md), [Privacy](docs/privacy.md) and [Contributing](CONTRIBUTING.md).

## License

Gillions Game Sync uses the [MIT License](LICENSE).
