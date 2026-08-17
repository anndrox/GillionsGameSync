# Changelog

## Unreleased

- Send unlocked Tomes of Regional Folklore as stable, deduplicated item IDs in the existing complete Collectibles snapshot for Mining, Botany, and Fishing tracking on Gillions.

## 1.0.27 - 2026-08-15

- Fixed prior-plan restoration after a deliberate conflict review so the exact newly acknowledged AutoRetainer state can be used as the retry baseline.
- Preserved fail-closed behavior if AutoRetainer changes again before restoration, plus the original backup, immediate read-back, device ownership, and execution-limit safeguards.

## 1.0.26 - 2026-08-15

- Promoted the live-validated Retainer observation, result acknowledgement, presence, and bounded AutoRetainer plan-delivery implementation to stable.
- Added explicit stable product and contract acceptance: stable Retainer uploads and plan polling remain unavailable unless Gillions acknowledges `GillionsGameSync` contract v1 and the applicable feature set.
- Kept AutoRetainer plan control off by default after upgrading from stable `1.0.25`; observation remains independent and ordinary Game Sync continues when Retainer support or AutoRetainer is unavailable.
- Preserved latest-only delivery, the 24-execution limit, exact acknowledgements, device ownership, compare-and-set conflict refusal/retry, immediate read-back, and complete prior-plan restoration.
- Made local release ZIP creation reproducible so identical candidate contents produce the same SHA-256.

## 0.0.59 - 2026-08-15 (testing)

- Allowed a new deliberate Sync to compare-and-set from the exact AutoRetainer plan hash previously reported in a bounded `local_plan_changed` acknowledgement.
- Preserved fail-closed behavior when AutoRetainer changes again, plus the existing testing-only opt-in, device ownership, 24-execution limit, backup, read-back, and restoration safeguards. Stable `1.0.25` remains unchanged.

## 0.0.58 - 2026-08-14 (testing)

- Corrected AutoRetainer additional-data IPC calls with a data-only mirror of AutoRetainer's complete writable record, avoiding plugin-owned constructor execution across Dalamud load contexts.
- Preserved the existing testing-only opt-in, bounded-plan, backup, compare-and-set, read-back, and restoration safeguards. Stable `1.0.25` remains unchanged.

## 0.0.57 - 2026-08-14 (testing)

- Superseded testing build; its direct runtime-type binding exposed AutoRetainer's load-context conversion incompatibility before any plan was delivered.

## 0.0.56 - 2026-08-14 (testing)

- Added explicit testing-only AutoRetainer planner opt-in and authenticated latest-only Gillions plan polling.
- Added device-pinned ownership, a complete original embedded-plan and `PlanCompleteBehavior` backup, and compare-and-set protection against outside plan changes.
- Added same-framework-update read, bounded write, immediate read-back verification, exact application acknowledgement, and safe original-plan restoration.
- Enforced a maximum of 24 executions and limited Gillions completion behavior to Assign Quick Venture or Do Nothing. Stable `1.0.25` remains read-only and unchanged.

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
