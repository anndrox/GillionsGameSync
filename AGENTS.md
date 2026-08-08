# Native Collector instructions

Scope: Gillions Game Sync plugin source.

Native Collector is the primary specialist for collection, local state, payloads, transport/retry/privacy, enrollment/sync behavior, and backwards compatibility. Read `docs/components/native-collector.md` and `docs/contracts/game-sync-api.md` when relevant.

This specialization is not a permission boundary. When a requested collector feature reasonably requires related server-handler, normalization, query, test, configuration, or small migration changes, make those changes as part of the same implementation when safe. Do not require an RFC or Site Operations handoff solely because another component is involved.

Backwards compatibility with installed Gillions Game Sync builds is a high-priority technical constraint. Treat breaking protocol or authentication behavior with additional care and focused compatibility testing.

Run the appropriate build/tests and verify affected server behavior when applicable. Use testing/stable release procedures proportionally to the requested task. Publication may proceed when it is part of delivering the requested result; do not create a separate approval loop unless the task reaches a root-level explicit-approval boundary.

Update `docs/changes/native-collector.md` after notable changes and material contract documentation when payload or compatibility behavior changes. Never record credentials, pairing codes, or raw player data.

Material plugin UI changes follow the repository bulk UX review workflow.
