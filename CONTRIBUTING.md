# Contributing

Bug reports and focused pull requests are welcome.

Before opening a pull request:

1. Keep collection read-only and avoid gameplay automation.
2. Use authoritative Dalamud or game-client state; do not infer ownership from unrelated IDs or collection types.
3. Preserve compatibility with installed older builds and optional payload fields.
4. Do not add credentials, pairing codes, player data, private endpoints, machine paths, build output, or diagnostic archives.
5. Run `./scripts/verify.ps1` and describe any in-game validation that remains necessary.

Public plugin changes are complete only after the reviewed version, stable manifest, immutable GitHub Release artifact and affected public documentation reach the canonical distribution. A task-branch push alone is not a user update. Follow [Releasing](docs/releasing.md), preserve required independent review gates, and distinguish prepared, published and installed versions.

Protocol or payload changes must document their compatibility behavior. Features that rely on another plugin must remain optional and must not become a dependency for core synchronization.
