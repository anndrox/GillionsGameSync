# Gillions Game Sync governance

Inherit `C:\~Gillions\AGENTS.md`.

Repository scope: standalone Gillions Game Sync Dalamud plugin source.

Canonical branch: `main`

Canonical remote: `origin` -> private Forgejo

Reference remote: `upstream` -> GitHub

Do not push to GitHub without explicit authorization. Packaging or building does not authorize public publication; publishing the plugin or static feed requires explicit authorization.

Preserve read-only, player-opt-in collection, account isolation, privacy, capability negotiation, and compatibility with installed older builds. Use authoritative Dalamud/game state and never infer ownership from unrelated identifiers. Optional integrations must not become core synchronization dependencies.

Do not commit credentials, pairing codes, player data, diagnostics, private endpoints, local paths, build outputs, or release artifacts. Keep `PathMap` enabled. Server-contract work may inspect the relevant intake code in `C:\~Gillions\FFXIV-Gillions`, but cross-repository changes must be recorded and committed in their own repository task branch.

Run `./scripts/verify.ps1` after material source changes. Update README, privacy/testing/releasing guidance, and changelog when documented behavior changes.
