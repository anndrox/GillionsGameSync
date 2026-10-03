# Private Submarine production beta — readiness and Site handoff

Owner decision, 2026-10-03: lack of owner FC access is **not a launch blocker**.
Eligible production opt-in users can establish real-client coverage after safe
beta launch. It is not authorization to invent production intake or bypass gates.
This assessment changes no collector, Stable binary, Site source or production.

## Current readiness

| Surface | Current state |
| --- | --- |
| Native collector | Testing local collector and authenticated private TEST transport exist. Exact game `2026.09.15.0000.0000` / SDK `7.56.2.9136` gate precedes native reads; unsupported/unavailable preserves retained state. Read-only naturally loaded workshop events, no automation. Live FC validation still outstanding, not an owner-access blocker. |
| Stable | Published 1.0.30 contains no Submarine collector/configuration/transport. Existing direct xivpf and ordinary sync remain independent. Stable promotion requires a bounded Submarine-only source change, not promotion of all Testing experiments. |
| Production Site contract | **Missing** in deployed application revision `25fa03aca0322b887b5e7971a89bd700cf0d9f63`, verified image digest `sha256:485cd87c1ba76b70ff41a070a76f46b8ef83520c64be32466bae08ca78ca71ba`. Deployed routes contain no personal intake. Anonymous sync 401 is ordinary auth, not evidence of personal support. Newer Site private intake explicitly permits only test/disposable and Testing devices. |
| Persistence | TEST schema 0019 has private latest state and exact receipts; HTTPS fixture checks prove those TEST paths only. Production installation/read/retention acceptance is not verified; do not assume 0019 deployed. No migration is run here. |
| Privacy | Existing producer and TEST account/paired-character/workshop isolation are explicit. Hash is sensitive scope, not FC ownership. Current TEST review acceptance cannot stand in for a changed Stable/production review. |
| Kill switch | No production Submarine-specific intake/use switch in the current Site implementation. TEST environment gating is not the required per-resource production kill switch. |
| Opt-in | Testing local retention/community preparation/private sync are separate default-OFF settings. Stable consent does not exist yet. Old users/devices must remain OFF, no automatic scope expansion. |
| Site page | Newer TEST private page/read model/reference planner exists; production beta page, readiness and private stale labels are not accepted/deployed. |
| Reference/planner | Versioned parts/rank/sectors/unlocks/distances/timing and known reward relationships exist in the newer Site source. They are independent reference evidence, not personal ownership or empirical reward probabilities. |

Production source directory access was unavailable; read-only deployed Docker
image identity and targeted packaged routes supplied runtime evidence instead.
No secrets/configuration dump, production mutation or infrastructure review.

## Private / community / telemetry separation

Private personal `submarine_personal` schema 1 / `submarine-personal-v1` contains
name, workshop scope, registration, rank/parts/stats/EXP, ordered/planned routes,
expected return and positive unlock/explore history. Registration is NOT departure,
expected return NOT completion, missing slot NOT empty, positive history NOT a
complete unlock set, and workshopScope NOT permission/FC membership. Keep account
and character identity server-authenticated, not payload supplied. Current-session
verified workshop batch only; after logout/reload/territory change wait for natural
workshop observation, never reassign old shared workshop snapshots to another owner.

Existing separate community preparation was audited in `SubmarineVoyages.cs` and
[its retained/export contract](submarine-voyages-v1.md). It is **local manual export,
not a production uploader**: separate default-OFF observation-time consent; only
linked nonconflicting completed results observed while opted in; no retrospective
consent promotion. Random stable observation ID, producing vs result-time build,
verified per-sector vs voyage-only rewards/HQ/EXP/unlock limitations. No name,
house/FC/workshop hash/slot/registration/account/reporter/credentials in that export.
Route/build/time can still reveal FC activity. Cross-contributor dedup is unproven;
same FC voyage reported by multiple players is not globally deduplicated by random
ID. Community stays disabled/deferred until its own admission/privacy/quality
contract; private sync must never populate a public outcomes table.

Local history bounds: 400 voyage records, 32 snapshots, <=4 MiB; no eviction of
unsent/admitted results, overflow warning and reject new identities. Private
prepared transport is separately bounded to 16 owner/resource entries / 1,152 KiB,
durable immutable nonce/body before sends, no unbounded journal. Opt-out preserves
history/pending state and stops future resource sends, not ordinary/PF/Market/Hunt.

Minimal existing Native diagnostics disclose resource/result/receipt and bounded
collector timing/reason, not private payload. Production telemetry is not yet wired.
Site should aggregate only success/failure reason, compatible game/SDK/plugin/
collector versions, admission result, bucketed timing and non-sensitive availability
counts using established operational tooling. No new analytics platform, raw payload,
submarine/character name, FC identity, workshop scope, routes, progression, credentials
or memory dumps. No individual tester identity in aggregate reports. Do not turn
account/device identity used for authorization into a telemetry dimension.

Reference source provenance already identifies the installed Lumina catalog,
pinned community timing/unlocks and candidate reward observations. Sample counts
and known reward relationships do **not** justify probabilities or precise EV.
First beta needs no loot prediction. Market values need freshness and “reference
gross value”; no profit claim without modeled operating costs. Keep modeled travel
time, observed expected return and empirical outcome data distinct.

## One exact bounded Site Operations handoff

1. Verify current production source/image/schema and promote only the compatible
   private Submarine utility surfaces intentionally. Agree an exact production
   activation of existing `POST /api/game-sync/sync`, header
   `X-Gillions-Personal-Contract: personal-observations-v1`, envelope
   `{resourceType:"submarine_personal",nonce,payload}`; no new receiver/auth/public
   FC feed. Current TEST contract is a baseline, **not** production authorization
   or already compatible Stable contract. Keep schemaVersion=1,
   collectorSchema=`submarine-personal-v1`, payload cap 65,536 bytes / envelope
   69,632 bytes unless a reviewed, agreed correction genuinely requires versioning.
2. Provide exact Stable product/channel/version/game/SDK admission, HTTPS production
   origin and presence acknowledgment: accepted product, personal contractVersion 1,
   exact endpoint, resource/schema/collector/capability/max bytes. Reuse persisted
   permission `server:game-sync:personal:submarine-personal:v1` and capability
   `submarine_personal_v1`; explicit default-OFF pairing/account consent, old devices
   unchanged. Per-send current device/account/paired-character ownership rechecked.
   Absence/incompatibility suppresses this resource only. Do not enable Hunt,
   Dashboard, community outcomes or other Testing resources on production.
3. Implement the required **Submarine-specific server kill switch** for admission
   AND personal-data-driven use. Withhold capability and reject new writes when
   OFF; planners/notifications must not treat retained state as current/live input.
   Preserve dated private latest state/receipts; private read may show historical
   data with disabled/stale reason. Fast operator disable without plugin update;
   re-enable cannot silently widen device permissions or native compatibility.
   Ordinary/Hunt/Market/PF resources remain independently operational.
4. Prepare/review the additive production schema path from its verified current
   head; never reset, edit applied migrations or run schema repair in runtime.
   Reuse private latest + receipt storage and existing archive/minimization/removal
   policy. Preserve per-account/paired-character/workshop isolation, non-destructive
   opt-out and monotonic updates/positive-sector union for the same registration.
   Omission/unavailable never clears. Reject equal-time conflicting data. Exact
   same nonce/body → original `{ok:true,snapshotId,receivedAt,unchanged}` receipt;
   changed body →409. TEST baseline: 32 latest submarine keys per account/character,
   4,096 receipts/account, 14-day opportunistic receipt prune and 30 requests/minute
   account admission. Confirm appropriate production limits explicitly, no silent
   unsent discard/unbounded retained-data expansion.
5. Private `/submarines` and associated account-owned character reads: no public
   payloads, no cross-account workshop dedup/FC authority inference, private/no-store.
   Show Beta/Experimental, Connected, Waiting for workshop observation, Last observed,
   Unsupported after game update, Stale observation, No synchronized Submarine data
   yet. Kill-switch/unsupported/missing slot cannot mean no submarines. Reference
   planner remains useful without personal sync; use positive history provisionally,
   no eligibility/loot guarantee. No synthetic banner on real selected state and
   no synthetic fallback pretending to be a player's state.
6. Agree privacy-safe operational counters above, existing logging/retention and
   access controls. Do not log bodies/errors containing private field values. Keep
   community results disabled/deferred and probability/EV unknown. No production
   community contribution endpoint is required for this beta.
7. Return exact reviewed production contract/source/schema/artifact/kill-switch
   operation and fixtures. Run disposable/TEST schema, authenticated receipt,
   isolation, opt-out, unsupported builds, omission, freshness, retry, retention and
   kill-switch tests; include beta page evidence and independent Security/Privacy
   review with no blockers. Native then implements the minimal Stable opt-in/
   collector/transport promotion, exact compatible build gate and ordinary-feature
   regressions, packages/reviews an immutable Stable candidate. Production activation
   follows the agreed guarded order, never an assumed server endpoint.

## Release order and acceptance

Site compatible private production preparation/review → Native exact-contract
Stable preparation/review → guarded server deployment with Submarine intake/use
OFF → verify deployed identity/privacy/schema/page → publish compatible Stable beta
→ enable only the approved resource for explicitly opted-in users → gather bounded
aggregate eligible-client evidence. Owner FC gameplay is never required. This is
a release sequence for the two owning lanes, not a deployment executed here.

Existing Testing validation: 542 Submarine managed checks, 391 personal transport/
Hunt checks, source boundaries, actual configuration preservation, and prior 36
shared-TEST HTTPS fixture checks covering receipts/persistence/private pages/auth.
No real workshop result capture is claimed. Beta users will establish event timing,
current-workshop coherence across transitions, field coverage/result attribution,
patch compatibility, admission failures and real frame-time ranges. Unknowns remain
visible; pointer/signature success alone is not semantic compatibility.

**BLOCKED — PRODUCTION SITE/SERVER HANDOFF REQUIRED.**
The blocker is missing accepted production support/kill switch/Stable promotion,
not the owner's lack of FC access. No Stable or production publication is made.
