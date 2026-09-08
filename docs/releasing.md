# Releasing

GitHub is the source-history, stable Dalamud manifest, icon, tag, and immutable stable ZIP authority. Gillions infrastructure is not part of the stable distribution chain.

The stable custom-repository URL is:

`https://raw.githubusercontent.com/anndrox/GillionsGameSync/main/data/GillionsGameSync.json`

The branch-backed URL is intentional: `main` is the reviewed stable source line, and the manifest advances with each stable release without asking users to replace their repository URL. Each stable ZIP is an immutable GitHub Release asset named `GillionsGameSync-X.Y.Z.zip` under tag `vX.Y.Z`; its SHA-256 is recorded under `data/releases/`.

## Completion requirement

Changes to the public plugin must reach its canonical public GitHub distribution and update all affected documentation. A task-branch push, local package or source review alone does not deliver an installed update. Keep the version, changelog, README/update instructions, privacy/testing guidance, manifest, checksum and release notes consistent with the reviewed behavior. Record prepared, reviewed, published and installed identities separately.

An owner-requested public release proceeds through the required independent reviews and normal publication steps without a second generic publication question. New authority boundaries still require their specific decisions. Public plugin release authority does not grant private server integration, deployment, database changes, live game actions or testing-feed publication.

## Local package

Create a candidate without publishing:

```powershell
./scripts/package.ps1 -Channel testing -Version 0.0.0
./scripts/package.ps1 -Channel stable -Version 1.0.30
```

Testing `0.0.0` is a local validation identity, not a testing release. The package script validates inputs, builds Release, creates a deterministic three-file ZIP, writes a feed-compatible manifest, and reports the ZIP SHA-256. Stable manifests use GitHub Release and raw-content URLs; testing manifests retain the separate Gillions testing origin. Identical source and dependencies produce identical packaged file contents and archive metadata. Output stays below ignored `artifacts/`. Keep `PathMap` enabled.

## Publication requirements

1. Reconcile current public `main`, tags, releases and feed before selecting an unused version. Preserve immutable existing tags/assets and concurrent changes.
2. Prepare the exact candidate on an isolated release branch. Update the project version, documentation and release notes. Run `./scripts/prepare-stable-github-release.ps1 -Version X.Y.Z`; review its stable manifest, immutable URLs, artifact and checksum record.
3. Run `./scripts/verify.ps1` with zero warnings and errors. Inspect the ZIP and scan source/artifacts for private paths or secrets. Bind the exact source, package, feed, checksum, release notes and publication commands for required independent review. Synthetic checks do not establish installed-game compatibility or visual acceptance.
4. After the release gates pass within explicit publication authority, integrate the reviewed source, manifest, icon changes (when any), checksum and documentation into GitHub `main`. Reconcile a changed main before execution; do not force-push over other work.
5. Run `./scripts/publish-stable-github-release.ps1 -Version X.Y.Z`. It requires a clean integrated commit, preserves any existing immutable tag/asset, creates a missing tag/release, reproduces the recorded package and verifies the anonymous public chain. The manifest becomes public before the asset in this established sequence; complete publication promptly and report a failed step rather than claiming the release is available.
6. Apply the reviewed user-facing release notes to the GitHub Release using `gh release edit vX.Y.Z --repo anndrox/GillionsGameSync --notes-file <reviewed-notes-file>`. Include the recorded package SHA-256 in that release body.
7. Confirm the tag and `main` source, release body, raw manifest, immutable download, checksum and embedded Dalamud identity/version. Report verified public availability separately from any user's installed version.

Never replace an artifact under an existing version or publish a testing artifact as stable. `DownloadLinkTesting` in the stable entry intentionally equals the stable release URL. The entry does not expose `TestingAssemblyVersion` or `TestingDalamudApiLevel`, so Dalamud cannot select that field as a testing build. Gillions testing uses the distinct `GillionsGameSyncTest` identity and testing feed; it is not published through stable GitHub Releases.

Publishing credentials and server configuration are intentionally not stored in this repository.

## Retainer compatibility and cutover

Version `1.0.30` removes executable planning and AutoRetainer discovery/read/write/apply/restore paths. Stable Retainer observations still require the authenticated product, contract and capability acknowledgement, including `acceptedClientProduct: "GillionsGameSync"` and `acceptedContractVersion: 1`. Missing or mismatched acceptance keeps Retainer uploads disabled while ordinary sync remains available. Neutral legacy presence fields preserve older-server parsing without advertising executable planner capability.

Public plugin publication does not prove or require an assumed server deployment. Review the exact client/server contract and any actual ordering dependency before release. Testing and stable observation admission remain separate server decisions. No planner flag or server response can restore an executable path in `1.0.30`.

Older `1.0.29` and historical testing builds included planner capabilities; their historical behavior remains in the changelog. A new public ZIP does not update every installed client immediately. Already installed external plans and callbacks queued by older loaded clients may continue; neither plugin publication nor disabling new server delivery proves their cancellation. Users manage external automation through its own controls. Preserve legacy local data as inert records; do not purge configurations, introduce automatic restoration, or roll back to planner-capable code as an unreviewed recovery step.

Server maintenance admission, old-client handling, live-game validation and rollback decisions retain their independent review and execution requirements. Keep release verification limited to the public plugin chain unless an additional concrete operation is authorized.
