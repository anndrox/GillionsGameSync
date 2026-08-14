# Changelog

## 1.0.25 - 2026-08-14

- Added bounded, read-only local observation of retainer identity, venture assignment, readiness, loaded gear, and transient result evidence.
- Added adaptive observation around AutoRetainer's short result window using only Dalamud's loaded-plugin notification. Stable does not call AutoRetainer IPC or automate ventures.
- Kept the unsupported `retainer_ventures` upload resource production-gated; this release does not send it to Gillions.

## 1.0.24 - 2026-08-13

- Added authoritative Armoire ownership collection with unloaded-state preservation.
- Sanitized embedded source paths in distributed diagnostics.

Earlier release history is retained in Git. The Gillions stable and testing feeds remain authoritative for distributed versions and artifacts.
