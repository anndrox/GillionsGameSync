# Gillions Game Sync instructions

Scope: the standalone Gillions Game Sync Dalamud plugin repository.

Preserve read-only collection, player opt-in, account isolation, privacy, and compatibility with installed older builds. Use authoritative Dalamud or game-client state and never infer ownership from unrelated identifiers. Optional integrations must complement the plugin and must not become dependencies for core synchronization.

Do not commit credentials, pairing codes, player data, diagnostics, private endpoints, local machine paths, build outputs, or release artifacts. Keep `PathMap` enabled so distributed diagnostics use `/_/GillionsGameSync/...` paths.

Run `./scripts/verify.ps1` after material source changes. A release candidate requires zero warnings and zero errors plus proportionate patch-current in-game validation. Packaging does not authorize publication; the Gillions static feed is updated through its separate controlled publisher.

Update the README, privacy/testing/releasing guidance, and changelog when their documented behavior changes.
