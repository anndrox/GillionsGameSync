# Testing 0.0.71 — daily / weekly personal facts

Status: **published Testing prerelease** on October 3, 2026 at 01:13:44 UTC.
The owner accepted Hunt testing, removing the active-session publication hold.
The existing `v0.0.64-testing/GillionsGameSyncTesting.json` rolling URL now points
to 0.0.71.0; testers keep their existing repository entry. No Stable/server effects.

Adds `/gillionsfacts` / **Private daily / weekly facts**, a separate default-OFF
read-only experiment. No pairing permission change or new upload; no Dashboard
configuration. Existing Hunts, Submarines, Beastmaster, Party Finder, Market and
ordinary sync remain independent. Source may observe UI/cache data but cannot
yet prove current server reset/ownership: no live-success claim.

See [capability matrix](../dashboard-capability-audit.md) and
[exact private model / Site handoff](../contracts/dashboard-facts-v1.md).

## Reviewed candidate evidence

Reviewed source `3c6f680dcbddf02586a570473d58a511cb51f3f9`; starting source
`d4d33572a1edcc3386d678ebf35abb8b84644892`, existing
`codex/game-sync/beastmaster-party-finder-testing` branch. No new worktree.
Independent Security & Privacy assessment accepted the complete new private
collection/retention/export/integration/disclosure surface, no findings; reviewer
independently passed 746 managed and source-contract checks. Unchanged accepted
features retain acceptance, not a new live validation claim.

Maintained verify passed: 2,064 numbered managed checks (746 new facts, 287 Hunt,
539 submarine, 224 market, 268 prior regression) plus PF/BST/ordinary/source/
performance/package/configuration checks. Actual installed SDK builds: zero
warnings/errors. Actual Dalamud serializer cases passed for both products and
the exact packaged successor; new facts default OFF and Stable excludes them.
Synthetic retained-store validation averaged 0.65 ms / 20 iterations; this is
not in-game frame-time evidence. Docs local targets and diff whitespace passed.

Published ZIP `GillionsGameSyncTesting-0.0.71.zip` SHA256
`b8a25bc62221c4d4b5432f022c91b4478032fc819208d9197d87e0f9e20f2559`.
ZIP and manifest preserved outside the worktree in the owner's Evidence area;
the immutable reviewed package was published without rebuilding.
[Machine-readable release evidence](../../data/releases/testing-0.0.71.json).
Publication does not prove live native collection or Site intake.
The full matrix, retained/export model, sanitized fixture, validation and bounded
Site handoff are committed with it. No new HTTP contract or production effect.

The fixed URL was anonymously verified at 01:16:59 UTC: manifest SHA256
`fb7a5e08ac58b63393d425e8addca906c703bbc9ee3ccf23f0703c47fbdc3637`,
all four download links, downloaded ZIP, embedded product/API/version and DLL
checksum matched the reviewed package. Initial GitHub predecessor caching cleared
without changing the owner's URL or re-uploading. The historical 0.0.64 ZIP and
notes were preserved; Stable 1.0.30 asset digest/date remains unchanged.
Full maintained verification passed again after release metadata changes, with
the same 2,064 numbered managed checks and zero SDK build warnings/errors.

## Live checklist

Keep the same testing repository URL; do not delete config or downgrade.
Update when convenient. Hunt testing is accepted by the owner, not additional
independently observed runtime evidence. New facts and submarine capture remain
live-unverified; Site owns future intake and page integration.

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
