# Testing 0.0.74 — private personal TEST synchronization

Testing only. Hunt Bills and personal submarine observations can use the existing
authenticated secure shared TEST sync contract, with independent default-OFF
upload permissions. Stable and existing ordinary Game Sync are unchanged.

Keep the existing rolling repository URL:

`https://github.com/anndrox/GillionsGameSync/releases/download/v0.0.64-testing/GillionsGameSyncTesting.json`

## Consent and installed test

Refresh/update Dalamud to **Gillions Game Sync Testing 0.0.74.0**. Preserve configuration;
do not downgrade or reset it. Existing main-site pairings are not silently moved.
To test personal transport, select `https://test.gillions.app` in Connection details
and pair through that site's normal Testing-device workflow. Explicitly grant
the site's private Hunt/Submarine permissions, then separately enable the desired
private sync switch in plugin settings. Local retention must also be enabled.
Pairing codes/tokens/configuration must never be shared as diagnostics.

For the first live Hunt test, use your existing bills: enable **Sync private Hunt
Bills to secure TEST** and inspect your private TEST Hunts page. No new hunt,
full bill set, logout, or bill-window opening is required when coherent caches
are already loaded. Copy only the aggregate Hunt and main-window sync diagnostics
if needed. A valid receipt proves intake, not verified acceptance/reset ownership.
Interface closure/unavailable data must not replace retained observations with zero.
Reload preserves prepared state; ordinary sync and other feature permissions remain
independent. OFF prevents future resource dispatch and preserves local state; it
cannot retract an already accepted request.

## Exact behavior

- `hunt_bills`: schema 1 / `hunt-bills-v1`, authenticated paired-character scope.
  Positive cached daily/weekly bills, targets and counters are provisional. No
  missing-data-as-empty, verified reset/acceptance, live sighting or B-rank position.
- `submarine_personal`: schema 1 / `submarine-personal-v1`, private scoped workshop
  state from the active character's naturally loaded current observation batch.
  After reload or a character/territory transition, a naturally opened workshop
  is needed; old unassociated workshop snapshots are not reassigned.
  Registered time is not departure; expected return is not observed completion;
  missing slots are unknown; positive sector history is not the complete unlock set.
  Routes/sector arrays export as numeric JSON arrays, not byte-array base64.
- Existing global Custom Delivery allowances remain authoritative. Per-client
  remaining is residual capacity only. Ambiguous agent RemainingAllowances remains
  unsupported; mismatch preserves retained state. Dashboard facts remain local.
- Normal TLS, exact TEST hostname, no redirects/direct-IP/HTTP retry/certificate bypass.
  Presence must acknowledge the exact product, contract, resource and capability.
- Prepared nonce/body is durably saved before each eligible dispatch/retry. Save
  failure suppresses sends. At most 16 owner/resource entries and 1,152 KiB; no unsent
  eviction. Same nonce/body retries safely. Terminal HTTP errors stop unchanged
  input even if the error body is empty/HTML/malformed. Network errors use bounded
  backoff. Identity stays in authentication, not personal payloads.

See [exact transport/consent/retention contract and Dashboard handoff](../contracts/testing-personal-transport-v1.md).

## Validation and limitations

Maintained `scripts/verify.ps1` PASS: **3,217** numbered checks (personal 391,
submarine 542, Dashboard 1,792, market 224, prior 268), plus Party Finder, configuration,
ownership, manifests, source contracts and performance checks. Both product builds
have zero warnings/errors. Exact packaged Testing DLL passes 12 actual Dalamud
serializer/configuration fixtures, separate from 12/channel verification fixtures.
Hunt/Submarine opt-outs preserve ordinary sync, Market preference and retained state.
Beastmaster, Custom Deliveries and previous performance additions remain intact.

Shared TEST HTTPS **36 checks PASS**, using sanitized Site Hunt and actual Native
submarine-export fixtures through real authenticated HTTP routes. Enrollment,
presence, schema, receipt retry/conflict, older-observation preservation, persistence,
permission/anonymous/revoked rejection, unowned-character read rejection and private
Hunt/Submarine page responses passed. Stored fixture observations render without
the synthetic-demo banner; these are still fixture data, NOT real game observations.
Only our temporary fixture devices were revoked; retained observations preserved.

Current Site source/configuration is `9018a32b3101a50f8e4219b4f632f73f8934ec69`,
baked TEST application `e6b30b65fdc9b83b4304a939f2a0305654fa1215`, TEST schema 0019.
Normal trusted TLS/hostname validation and the authenticated HTTPS origin gate pass.
Infrastructure review is not reopened. No Site source/schema/deployment was changed.

Independent Security/Privacy and producer-consumer review accepted source
`052acbd1f30b20de6c4d36675ce92a90d8ba0980`; both identified blockers corrected and
re-reviewed with no remaining findings. Source/fixture evidence is not live success.
Synthetic Dashboard validation averaged 0.87 ms in the final suite; no new native
polling/game requests were added and no live frame-time measurement is claimed.

**READY FOR OWNER END-TO-END HUNT TEST.** Real Hunt HTTPS receipt-to-private-page
validation, normal state-change refresh and reload/interface-closure transport
checks remain outstanding. Prior owner-confirmed local Hunt behavior is preserved.
**LIVE FC VALIDATION OUTSTANDING**; eligible FC access is not a publication blocker.
Dashboard has fixtures/read models but no exact activated intake contract; the
linked bounded Site handoff specifies required resource/schema/permissions,
validation, scope, typed fact and receipt semantics. Hunt/Submarine transport is
independent of that missing contract. No Dashboard schema is invented.

## Exact candidate

Source/tag target: `052acbd1f30b20de6c4d36675ce92a90d8ba0980`.
Branch: `codex/game-sync/beastmaster-party-finder-testing`.
Starting published Testing73 source: `735bd17e77f959c103f59a465f047961c247465d`;
starting publication-record HEAD: `d69ce4327c43de1fe01d74dff7ef8c7c5441afe9`.
Release/tag: `v0.0.74-testing`.

SHA256:

```text
ZIP      4f94ccd8033bf73f226082601c10d61c44d4b24d141c87b062095e3a5be82462
DLL      473ef2b902ac4749359461cb0cd3b9497e85a78d917a654fed2dfe61494af24d
Manifest 665cc9a28f277ce3aa0d31e5339a7f4664ed389ace432fddb8ac948b2153fe33
```

The previous preparation was rebuilt because review corrections changed code;
the final exact ZIP/DLL/product/API15/version/hash/configuration checks passed.
Published as a prerelease on 2026-10-03 at 13:51:22 UTC. The exact unchanged rolling
repository URL anonymously resolved to 0.0.74.0 at 13:54:58 UTC; all four ZIP links,
downloaded ZIP hash and embedded DLL/version/product/API15 match this candidate.
Initial predecessor cache cleared without re-upload or changing the URL.
Publication state and public chain checks are recorded in
[durable release evidence](../../data/releases/testing-0.0.74.json).
No Stable/main/production/Site source/Market architecture/Wardrobe/gameplay writes
or TLS bypass. Prior immutable Testing artifacts remain unchanged.
