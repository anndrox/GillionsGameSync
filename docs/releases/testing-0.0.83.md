# Testing 0.0.83 Hunt bill item coverage

Status: prepared successor; publication and live inventory validation pending.
Starting published82 source42cf0b11f1c499261bf56dec11d851d08dfe6d70;
starting task HEADd723d93bb9a323a4780f388da094220247c17289.

Testing83 adds safe absence-only Hunt bill item coverage, not authoritative
current-order/cycle ownership. See the [exact Site handoff and full mapping](../contracts/hunt-bill-items-v1.md)
and [invented schema example](../examples/hunt-bills-v2.json).

The loaded full Key Items snapshot can report absent_confirmed for each exact
bill type. Item presence is present_unresolved; incomplete/uninitialized/malformed
source is unavailable. Historical counters/provenance never change because an
item disappears. All22 supported domains have explicit states.

Coverage and its prepared nonce/body live only in RAM. Existing private Hunt
consent is required; focus/travel do not grant it. The existing authenticated
TEST endpoint sends schema2 only after exact hunt_bills_v2 acknowledgment. Site
currently supports v1, so old positive-only sends remain unchanged and coverage
transport is gated. Site must implement the documented admission/freshness/latest
state rules; no Site changes occur in this Native release.

No gameplay writes, UI opening, extra request channel, public private-state feed,
new history, increased native collection cadence or Stable publication. Preserve
80 positive final completion,82 responsiveness, map V1/V2, PF/item links, Market,
Submarine, Dashboard facts, Custom Deliveries, ordinary sync and travel privacy.

## Validation

Managed fixtures cover A-I, full slot enumeration/consistency, malformed catalogs,
UTC/monotonic expiry, local epochs, permission OFF, immutable retries, terminal
input and exact capability admission. Maintained source/configuration/SDK/build
and regression tests are separate from native runtime proof. Exact-DLL tests use
managed fixtures only and must not be presented as live inventory validation.

## One bounded live inventory check

After exact-package reviews pass, load the reviewed DLL with installed82 disabled;
never run both. Open `/gillionshunts` with local retention ON. No bill window,
quest completion, configuration reset or re-pairing is required. Confirm the
timestamped complete Key Items result and one displayed bill-type item state
against ordinary Key Items. A screenshot of these lines is sufficient; do not
share configuration, credentials, raw memory or whole private JSON exports.

If complete coverage cannot be established, finite loaded/stable/slot/symbolic/
item consistency diagnostics identify the gate; do not weaken validation merely
to produce absence. Publication/live-proof state and hashes are recorded separately.

## Live completeness failure

The initial owner run showed loaded, stable and initialized source with all124
slots and no identity or symbolic mismatch, but one invalid item/quantity shape.
All22 domains correctly remained unavailable. A subsequent diagnostic run
identified a zero-ID slot with positive residual quantity; repeated native
getters reported empty=true, base item ID0 and unchanged matching quantity.
This identifies the failed assumption, not successful complete coverage.

The diagnostic successor distinguishes zero-ID/nonzero-quantity,
positive-ID/zero-quantity and negative-quantity counts. At most8 structurally
bound, non-symbolic invalid slots receive read-only IsEmpty, GetBaseItemId and
GetQuantity probes, twice for consistency, only on the existing Hunt cadence.
Null virtual tables/functions are not called. The UI and copied aggregate
diagnostics expose finite counts only, never slot indices, item IDs or quantities
from those probes. Aggregate probe counts never authorize coverage and are not
exported, logged as new history, persisted or uploaded.

The correction accepts only that zero-ID/positive residual-quantity case with
a separate same-read native-confirmed empty proof. Missing/inconsistent getters,
negative quantity or any other malformed slot remains unavailable. All other
completeness, freshness, character and history gates are unchanged; the payload
shape is unchanged. Corrected fixtures are not live coverage proof.

The [pinned typed source](https://github.com/aers/FFXIVClientStructs/blob/6a562a1ef86b9acb22bda0726e28b0e23bb27552/FFXIVClientStructs/FFXIV/Client/Game/InventoryItem.cs)
defines those getters; the owner diagnostics establish the observed empty-slot
case. The corrected complete-coverage path still needs a bounded runtime check.

## Site routing rule

Only a fresh latest absent_confirmed in the authenticated exact character/type
domain may exclude historical records from current routing. Never change their
positive progress. present_unresolved/unavailable/omission cannot supersede
anything; reappearance and expiry invalidate previous absence authority. No
permanent monster tombstone, individual-order replacement or reset inference.

Party Finder attribution recommendation remains **Data provided by xivpf.com**,
with its clickable source link.
