# Releasing

GitHub is the source-history, stable Dalamud manifest, icon, tag, and immutable stable ZIP authority. Gillions infrastructure is not part of the stable distribution chain.

The stable custom-repository URL is:

`https://raw.githubusercontent.com/anndrox/GillionsGameSync/main/data/GillionsGameSync.json`

The branch-backed URL is intentional: `main` is the reviewed stable source line, and the manifest must advance with each stable release without asking users to replace their repository URL. Each stable ZIP is an immutable GitHub Release asset named `GillionsGameSync-X.Y.Z.zip` under tag `vX.Y.Z`; its SHA-256 is recorded under `data/releases/`.

## Local package

Create a candidate without publishing:

```powershell
./scripts/package.ps1 -Channel testing -Version 0.0.61
./scripts/package.ps1 -Channel stable -Version 1.0.29
```

The package script validates its inputs, builds Release, creates a deterministic three-file ZIP, writes a feed-compatible manifest, and reports the ZIP SHA-256. Stable manifests use GitHub Release and raw-content URLs; testing manifests retain the separate Gillions testing origin. Identical source and dependencies produce identical packaged file contents and archive metadata. Output stays below ignored `artifacts/`.

## Publication requirements

- Review and commit the exact source first. Publish testing through its separate Gillions feed when in-game acceptance is required.
- Run `./scripts/prepare-stable-github-release.ps1 -Version X.Y.Z`; review the generated stable manifest, immutable GitHub URLs, artifact, and checksum record.
- Run `./scripts/verify.ps1` with zero warnings and errors. Inspect the ZIP and scan source/artifacts for private paths or secrets.
- Commit and integrate the exact source, manifest, icon changes (when any), and checksum record into GitHub `main`.
- Run `./scripts/publish-stable-github-release.ps1 -Version X.Y.Z`. It requires a clean integrated commit, preserves any existing immutable tag/asset, creates a missing tag/release, and verifies the anonymous public chain.
- Confirm the raw manifest resolves the plugin entry to the expected GitHub Release ZIP and that the downloaded SHA-256 and embedded Dalamud package match.
- Never replace an artifact under an existing version or publish a testing artifact as stable.

`DownloadLinkTesting` in the stable entry intentionally equals the stable release URL. The entry does not expose `TestingAssemblyVersion` or `TestingDalamudApiLevel`, so Dalamud cannot select that field as a testing build. Gillions testing uses the distinct `GillionsGameSyncTest` identity and testing feed; it is not published through stable GitHub Releases.

Publishing credentials and server configuration are intentionally not stored in this repository.

Testing `0.0.61` requires a production web runtime with `RETAINER_TESTING_UPLOAD_ENABLED=true`. That switch enables only an authenticated, currently present `GillionsGameSyncTest` client declaring the complete v1 observation/result/exact-ack/presence capability set. It does not enable stable clients, add the resource to legacy scopes, or enable plan delivery or AutoRetainer writes. Disabling the switch and recreating web immediately closes only this testing path.

Testing `0.0.61` requires the independent `RETAINER_PLAN_DELIVERY_ENABLED=true` runtime gate and the full plan-delivery/application/completion capability set. Disable that gate and recreate only web to stop all Gillions-to-AutoRetainer writes without disabling normal Game Sync or Retainer observations. Never publish this build to the stable manifest.

## Stable Retainer rollout

Stable `1.0.29` is published. Production uses separate `RETAINER_STABLE_UPLOAD_ENABLED` and `RETAINER_STABLE_PLAN_DELIVERY_ENABLED` gates. A stable presence response may advertise support only after validating the authenticated `GillionsGameSync` product, contract v1, and required capabilities, and must echo:

- `acceptedClientProduct: "GillionsGameSync"`;
- `acceptedContractVersion: 1`.

Absent, malformed, testing-product, or mismatched-contract acknowledgements keep stable Retainer uploads and plan polling disabled. The stable planner gate must remain independent from stable observation intake. Disable the planner gate to stop new AutoRetainer writes; disable the stable intake gate to stop stable Retainer uploads. Neither rollback requires disabling ordinary Game Sync.

Production enabled stable observation and plan delivery only after the exact source tree passed local and GitHub verification, the immutable artifact was reproduced, and the server acceptance probe passed. Planner delivery remains separately kill-switchable and still requires explicit player opt-in plus the complete capability, presence, AutoRetainer readiness, ownership, compare-and-set, read-back, backup, and execution-limit gates.
