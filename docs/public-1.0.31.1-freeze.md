# Public 1.0.31.1 identity-only successor

Product: `GillionsGameSync`; assembly/package identity: `1.0.31.1`.
Base: corrected `80f9134479db1c3f543417f2c310f82f168be88b`.
Status: unpublished; exact Site admission still required. No main/feed/tag changes.

Both Public `1.0.31.0` sources (`5a6ca690` and corrected `80f9134`) are invalidated
for new public capability admission: the existing wire identity cannot distinguish
them. Historical frozen evidence remains preserved; neither may be published or
admitted for newly promoted capabilities. The [previous freeze](public-1.0.31-freeze.md)
records inherited behavior, not current admission authority.

Only product version and directly derived assembly/manifest/FATE collector metadata
change. Runtime source, configuration, permissions, endpoints, polling, frozen UI,
PF-link compiled-product acknowledgement and Market exact TEST-origin restriction
remain unchanged. Packaging accepts a fourth component without appending `.0`;
existing three-component Public/Testing versions retain their normalization.

The FATE tuple changes only `collectorVersion` to `1.0.31.1`. The accepted game,
Dalamud, ClientStructs, Lumina and Excel tuple must be independently verified
unchanged before building. Stop on unexpected dependency changes.

Run maintained verification, real Stable30 synthetic upgrade/restart/backup
rollback fixtures, exact-package checks, automatic FATE/permission/PF/Market
regressions and controlled compiled UI comparison. UI may differ only in actual
version text. Obtain bounded independent Security/Privacy and Compatibility
judgment, build the final candidate once, and freeze exact bytes externally.
No repeat of the physical Testing87 campaign is required for this identity change.

Site handoff must request exact `GillionsGameSync / 1.0.31.1` admission and exact
FATE tuple, never a `>=` family rule or SHA constant pretending to be remote proof.
Reject all `1.0.31.0` for new capabilities; preserve Testing86/87 and historical
Stable30 ordinary behavior. This objective does not modify Site or production.

See [candidate version immutability](releasing.md#public-candidate-identity-immutability).
