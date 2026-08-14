# Changelog

## 0.0.55 - 2026-08-14 (testing)

- Added character-partitioned Retainer roster, profile, gear, venture, inventory-coverage, and optional read-only AutoRetainer cache observations using the approved v1 evidence vocabulary.
- Added testing-only presence and capability negotiation with jittered heartbeats, bounded retry backoff, and character lifecycle invalidation.
- Added durable venture-result identity with separate reward fingerprints and strict exact-event acknowledgement parsing; malformed or unrelated successful responses preserve every pending event.
- Removed the testing runtime paths that could apply or restore AutoRetainer plans. This build performs no AutoRetainer writes and requires the server's testing-only capability gate before uploading Retainer data.

## 1.0.25 - 2026-08-14

- Added bounded, read-only local observation of retainer identity, venture assignment, readiness, loaded gear, and transient result evidence.
- Added adaptive observation around AutoRetainer's short result window using only Dalamud's loaded-plugin notification. Stable does not call AutoRetainer IPC or automate ventures.
- Kept the unsupported `retainer_ventures` upload resource production-gated; this release does not send it to Gillions.

## 1.0.24 - 2026-08-13

- Added authoritative Armoire ownership collection with unloaded-state preservation.
- Sanitized embedded source paths in distributed diagnostics.

Earlier release history is retained in Git. The Gillions stable and testing feeds remain authoritative for distributed versions and artifacts.
