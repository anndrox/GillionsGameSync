# Gillions Game Sync plugin

This is the personal, read-only Dalamud plugin source for account-linked Gillions sync. It pairs with Gillions, receives a one-time device credential, and synchronizes all supported categories through outbound HTTPS to the owning account.

Collection is native and read-only. It does not open a retainer, request a listing, use packet capture, or require another plugin. Player bags, armoury, currency, crystals, and saddlebags are collected only while loaded. Retainer state is collected only after its live container is loaded and is never inferred as empty.

The optional “Link in game” capability uses only the paired device credential to poll for a short-lived account-scoped item request. The plugin verifies the ID against the local Lumina Item sheet, consumes the one-time server claim, and prints a native FFXIV item link through Dalamud chat. It does not inspect inventory or automate game controls for this capability. Servers or older plugin builds without the capability safely retain the website fallback.

Commands:

- `/gillionssync pair` pairs the plugin after the user enters a one-time code in its settings.
- `/gillionssync` manually collects and synchronizes the enabled scopes.

While logged in, automatic sync is enabled by default. During the current
trial it checks the local selected data every 5 seconds but only uploads an individual category when its
contents changed. This keeps retainer listing state fresh without repeatedly
sending identical inventories. It can be disabled in the plugin window.

The plugin has no game-command, packet, UI automation, market-provider, Square Enix credential, or inbound-network behavior. The collector adapters are deliberately isolated so they can be completed and patch-tested against the exact installed Dalamud API without touching pairing or transport. Allagan Tools export remains an explicit fallback only.

Build with the current .NET SDK and the installed Dalamud API target. The plugin is currently built with .NET 10 and Dalamud API 15. Do not distribute a build until each collector has been tested with an empty character, a populated inventory, retainers, and a patch-current client.

## Dalamud experimental repository

The public repository URL is `https://gillions.app/plugins/GillionsGameSync.json`; testing uses `https://gillions.app/plugins/GillionsGameSyncTesting.json`. Release builds receive the public origin through `GillionsPublicBaseUrl`, so non-public builds can use an explicit alternate origin without changing runtime code. Builds using the new public origin migrate only configurations still set to the former Gillions default; user-entered custom or self-hosted URLs are preserved. The stable baseline is `1.0.0.0`; it is published with an immutable versioned ZIP and SHA-256 checksum through that feed. Its window has a normal close control and can be reopened from Dalamud's plugin configuration. Local configuration changes are marshaled back to Dalamud's framework thread after network sync, avoiding a main-thread error while preserving the completed upload. Retainer-market snapshots now include names from the local retainer manager when they are available, while retaining an ID fallback. It reports the actual Gillions response when pairing or syncing, refuses to upload an empty inventory snapshot, identifies Armoire, Glamour Dresser, and placed apartment/private-estate/Free Company furnishings using Allagan-resolved containers so sellable-stock tools can exclude them, includes owned retainer bags in crafting inventory, and excludes items actively listed on a Retainer Market from craftability. Each listing retains its retainer-market slot, so two listings of the same item and quality remain distinct. When the optional Achievements scope is authorized, it reads completed IDs only after the in-game Achievement list has loaded. The optional Character Progress scope stores job levels, the active job's equipped gear as its last-known setup, unlocked crafting recipes, and caught fishing-log entries. Change detection uses canonical fingerprints, so unordered provider data cannot cause an upload by itself. People who add the repository once receive later updates without changing their repository setting.
