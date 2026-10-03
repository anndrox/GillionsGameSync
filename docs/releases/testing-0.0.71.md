# Testing 0.0.71 — prepared daily / weekly personal facts

Status: **candidate held, not published**. Published Testing 0.0.70 and the owner's
existing `v0.0.64-testing/GillionsGameSyncTesting.json` rolling URL remain unchanged.
Do not interrupt an active Hunt validation session. No Stable/server effects.

Adds `/gillionsfacts` / **Private daily / weekly facts**, a separate default-OFF
read-only experiment. No pairing permission change or new upload; no Dashboard
configuration. Existing Hunts, Submarines, Beastmaster, Party Finder, Market and
ordinary sync remain independent. Source may observe UI/cache data but cannot
yet prove current server reset/ownership: no live-success claim.

See [capability matrix](../dashboard-capability-audit.md) and
[exact private model / Site handoff](../contracts/dashboard-facts-v1.md).

## Live checklist after publication is appropriate

Keep the same testing repository URL; do not delete config or downgrade. No need
to restart the current Hunt session to carry out this preparation work.

1. Before enabling, note facts attempts stay zero while ordinary sync, Hunt local
   observations and existing contribution controls operate as before.
2. Enable only **Retain private daily / weekly observations locally**. Open Duty
   Finder naturally and leave it visible through a source turn (normally within
   35 seconds, at most 70 under sustained UI notices). Compare all unlocked and
   locked roulettes. Eligibility must remain UNKNOWN, not fabricated completed.
   Compare reward flags before/after a normal completion. Do not automate queueing.
3. Open Custom Deliveries naturally: compare global used/12/remaining and selected
   client count/cap, current-rank satisfaction and rank. At a max-rank client,
   verify the native satisfaction max remains coherent; unavailable data must
   preserve prior state, not fabricate zero. Compare next native reset.
4. Open Challenge Log naturally: compare completed flags across categories.
   Numeric challenge progress must remain UNKNOWN. Closing it preserves facts;
   after two minutes without a read they become STALE.
5. Compare limited tomestone **earned this week**, native cap and remaining, not
   wallet balance. Other currencies must not become weekly progress. Compare
   weekly PvP counters after the game naturally loads the PvP profile.
6. With a held Wondrous Tails journal, compare stickers, Second Chance, all 16
   order IDs/statuses and expiration. Missing/expired journal must not claim
   turned-in or delete history; claimed sticker is separate from task completion.
7. Open Timers naturally: compare leve/society remaining and any available map,
   squadron mission/training next timestamps. Past/zero timestamps remain unknown,
   not proof of finished/available/claimed. Doman/Fashion/Carnivale are NOT falsely
   shown complete: those unsupported personal facts remain manual-only.
8. Disable: attempts stop and private copy is unavailable, records retained.
   Re-enable, reload plugin when convenient, log out/change characters: no cross-
   character export or false freshness; retained observations start STALE.
9. At actual reset, old facts become STALE/UNKNOWN; never automatic zero. Only a
   new admitted observation changes values. This requires actual in-game evidence.
10. Check frame time / displayed read-save timing while opening UI and after
    enabling/reloading. Regression: existing Hunt target names/counters/drop/
    reacquire/off-on, Beastmaster freshness, PF controls/recipient and ordinary
    sync with market OFF. Do not force submarine interaction without access.

For support, use **Copy aggregate facts diagnostics**, then paste version,
game/SDK, enabled status, source status, attempt count and read/save time. State
which interface was naturally open, what it showed, and elapsed time. No config,
pairing code, device credential, player log or private JSON required. Private
facts export contains activity/counters/times; share only intentionally with a
trusted recipient. Screenshots can contain identities; crop/redact them.
