# Releasing

GitHub is the source-history authority. The Gillions static plugin service remains the distribution authority for Dalamud manifests and immutable ZIP artifacts.

## Local package

Create a candidate without publishing:

```powershell
./scripts/package.ps1 -Channel testing -Version 0.0.59
./scripts/package.ps1 -Channel stable -Version 1.0.26
```

The script validates the version and public origin, builds Release, creates a deterministic three-file ZIP, writes a feed-compatible manifest, and reports the ZIP SHA-256. Identical source and dependencies produce identical packaged file contents and archive metadata. Output stays below ignored `artifacts/`.

## Publication requirements

- Review and commit the exact source first.
- Run `./scripts/verify.ps1` with zero warnings and errors.
- Inspect the ZIP contents and scan the source and artifact for private paths or secrets.
- Publish testing first for behavior that needs in-game acceptance.
- Use the Gillions static-release publisher; do not rebuild the website for a plugin-only release.
- Verify the manifest, ZIP, icon, checksum, rollback artifact, and service health.
- Tag only the source commit that corresponds to the published artifact. Do not replace an artifact under an existing version.

Publishing credentials and server configuration are intentionally not stored in this repository.

Testing `0.0.59` requires a production web runtime with `RETAINER_TESTING_UPLOAD_ENABLED=true`. That switch enables only an authenticated, currently present `GillionsGameSyncTest` client declaring the complete v1 observation/result/exact-ack/presence capability set. It does not enable stable clients, add the resource to legacy scopes, or enable plan delivery or AutoRetainer writes. Disabling the switch and recreating web immediately closes only this testing path.

Testing `0.0.59` requires the independent `RETAINER_PLAN_DELIVERY_ENABLED=true` runtime gate and the full plan-delivery/application/completion capability set. Disable that gate and recreate only web to stop all Gillions-to-AutoRetainer writes without disabling normal Game Sync or Retainer observations. Never publish this build to the stable manifest.

## Prepared stable Retainer rollout

Stable `1.0.26` is a release candidate only. Before publication, Site Operations must add separately disabled `RETAINER_STABLE_UPLOAD_ENABLED` and `RETAINER_STABLE_PLAN_DELIVERY_ENABLED` gates. A stable presence response may advertise support only after validating the authenticated `GillionsGameSync` product, contract v1, and required capabilities, and must echo:

- `acceptedClientProduct: "GillionsGameSync"`;
- `acceptedContractVersion: 1`.

Absent, malformed, testing-product, or mismatched-contract acknowledgements keep stable Retainer uploads and plan polling disabled. The stable planner gate must remain independent from stable observation intake. Disable the planner gate to stop new AutoRetainer writes; disable the stable intake gate to stop stable Retainer uploads. Neither rollback requires disabling ordinary Game Sync.

Recommended order: deploy the echo and disabled gates; validate the stable candidate against a non-production enrollment; publish the immutable stable artifact with both gates still disabled; enable stable observation for a bounded cohort; then enable stable planner delivery for an explicitly opted-in cohort. Stable publication and production enablement remain separate decisions.
