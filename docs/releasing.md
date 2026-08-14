# Releasing

GitHub is the source-history authority. The Gillions static plugin service remains the distribution authority for Dalamud manifests and immutable ZIP artifacts.

## Local package

Create a candidate without publishing:

```powershell
./scripts/package.ps1 -Channel testing -Version 0.0.54
./scripts/package.ps1 -Channel stable -Version 1.0.25
```

The script validates the version and public origin, builds Release, creates the three-file ZIP, writes a feed-compatible manifest, and reports the ZIP SHA-256. Output stays below ignored `artifacts/`.

## Publication requirements

- Review and commit the exact source first.
- Run `./scripts/verify.ps1` with zero warnings and errors.
- Inspect the ZIP contents and scan the source and artifact for private paths or secrets.
- Publish testing first for behavior that needs in-game acceptance.
- Use the Gillions static-release publisher; do not rebuild the website for a plugin-only release.
- Verify the manifest, ZIP, icon, checksum, rollback artifact, and service health.
- Tag only the source commit that corresponds to the published artifact. Do not replace an artifact under an existing version.

Publishing credentials and server configuration are intentionally not stored in this repository.
