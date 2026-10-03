# Testing 0.0.73 — selected Custom Delivery counter diagnosis

Status: owner-approved Testing publication of the exact validated prepared
package, without rebuilding. Versioned72 is preserved; the unchanged owner-selected
rolling repository URL advances to73. Stable/main/Site/server are untouched.
No new upload, game request, UI opening, hook, gameplay action or polling cadence.

## Diagnosis

FACT: owner live72 evidence reached `ClientCounterMismatch`. In that build this
means `NpcData.RemainingAllowances != NpcData.MaxAllowances - NpcData.UsedAllowances`.
All preceding active visible owning-addon, native validity/init/update, manager
init, future-reset, unique ENpcResident catalog match, cap and rank-max gates
passed. Global native allowance facts were admitted independently. Live
roulette, weekly tomestone and journal facts were also observed. Measured read/
save durations are individual captured reads, not every group's frame-time bound.

UNKNOWN: the numeric tuple was intentionally absent from aggregate diagnostics.
The evidence therefore does not prove the exact agent-remaining interpretation
(global, effective, stale or layout issue), or rule out a hidden update race.
Do not assert any of those as the observed cause. A wrong `CurrentNpc` index was
not being used by72: client scope came from a unique ENpcResident catalog match.

The demonstrated defect is an unsupported equality assumption used as the
client admission gate. The installed
[SDK manager definition](https://github.com/aers/FFXIVClientStructs/blob/6a562a1ef86b9acb22bda0726e28b0e23bb27552/FFXIVClientStructs/FFXIV/Client/Game/SatisfactionSupplyManager.cs)
names the agent field but does not establish that equality. DailyDuty/Accountant
corroborate global allowances only. They did not prove the original client model.

Independent primary source references map SatisfactionNpc RowId minus one to the
manager's UsedAllowances, SatisfactionRanks and Satisfaction arrays:
[Umbra](https://github.com/una-xiv/umbra/blob/cbd00524952aa1c0386a47126f05721191f80033/Umbra.Game/src/CustomDeliveries/CustomDeliveriesRepository.cs)
and [HaselDebug](https://github.com/Haselnussbomber/HaselDebug/blob/91b4e56b626d0347edf22f3825b187ac07bc5996/HaselDebug/Tabs/SatisfactionSupplyTab.cs).
These are technical references, not a dependency, copied code, or authority to
adopt their automation, broad scans or freshness assumptions.

## Bounded correction and capability state

- Preserve global getter/reset/admission exactly as72.
- Preserve the active visible UI, native validity/init/update/reset, unique
  ENpcResident catalog identity, native/catalog cap and rank-max checks.
- Bounds-check the matched RowId-minus-one entry in all three manager arrays.
- Admit client usage only when manager usage is within client cap and matches
  the active agent, and manager rank/satisfaction also match the agent.
- Retain client remaining as client cap minus corroborated usage, NOT effective
  ability to deliver. In a synthetic example, global used12 can coexist with another client's
  used0/cap6/residual6, with eligibility unknown. Never infer all clients used6.
- Do not read the ambiguous agent RemainingAllowances field. It is unsupported.
  No minimum, clamp, swapped index, zero fabrication or initialization bypass.
- Invalid/partial/index/corroboration failure keeps client detail unavailable for
  that read and preserves its retained observation; valid global still works.

This is reference/SDK/fixture-backed conditional client capability, not live73
success. Existing retained71/72 facts keep producing metadata and remain stale
until independently observed. The v1 JSON shape, 16-character/24-group/384-KiB
bounds, default-OFF private consent, 8-group fair five-second cadence, private
export and ordinary HTTPS pairing/sync remain unchanged.

No additional delivery is needed. A successor build is required to run the
corrected collector; the owner has accepted and authorized this publication.
No new live state is requested to invent a semantic proof for the rejected field.

## Validation

Maintained `scripts/verify.ps1` PASS: 3,110 numbered managed checks, including
1,792 Dashboard cases (all 12 client IDs, mismatched/out-of-range usage, rank/
satisfaction disagreement, global exhaustion with unspent client capacity,
retained-state preservation and independent global/client admission). Hunt287,
submarine539, market224 and prior268 regressions also pass. Party Finder,
Beastmaster, roulette/tomestone/journal retention, ordinary sync, configuration,
source privacy, consent and performance contracts pass. Actual SDK Stable-compatible
and Testing builds have zero warnings/errors; actual serializer fixtures pass.
These are offline/SDK validation, not live73 correctness or frame-time evidence.

Primary assessed the bounded source correction and complete directly affected
native/admission/export/persistence/diagnostic surface. It adds no private fields,
identity, credential, upload, trust or retention scope. The previously reviewed
privacy boundaries remain unchanged; no new independent approval is claimed.
The market-observations-v1 contract is unchanged.

Prepared package source: `735bd17e77f959c103f59a465f047961c247465d`.
ZIP SHA256: `91c3fd8da2322b10f3e0858db25ecd97df854bf3d4a5dfc9b569f8e6c727a563`.
Manifest SHA256: `2062725468b69db98c0afc61d4229850f5d5ddb1ac0d6339e10dd7399fba99c2`.
DLL SHA256: `fd2a3ee69401a61782a21136c48fe2c0b936c24c2790bce897f25b11be64cd58`.
Exact packaged DLL actual configuration tests and product/API15/73/hash/private-path
checks PASS. The exact package is now published at `v0.0.73-testing`; no rebuild.
Detailed durable evidence: [Testing73](../../data/releases/testing-0.0.73.json).

## Owner-approved publication

Published the exact prepared package, without rebuilding, on 2026-10-03 at
07:00:12 UTC under tag `v0.0.73-testing`. Owner acceptance does not relabel this
as live73 validation. Exact existing packaged DLL configuration tests were rerun
with `--no-build` immediately before publication; all passed. Tag points to the
validated source above; publication documentation does not alter that package.

The unchanged owner-selected repository URL
`https://github.com/anndrox/GillionsGameSync/releases/download/v0.0.64-testing/GillionsGameSyncTesting.json`
anonymously returned `0.0.73.0` at 07:01:57 UTC. Its manifest hash, all four links,
downloaded ZIP hash, embedded product/API15/version and DLL hash match the exact
prepared artifact. Initial predecessor cache cleared without re-upload or a new
URL. Stable release/main and Site/server are untouched; versioned72 and historical
Testing artifacts remain preserved. No additional delivery/Ameliance action is
required for this publication.
