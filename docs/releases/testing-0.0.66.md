# Gillions Game Sync Testing 0.0.66

Manual Dalamud custom repository URL (available after publication):

`https://github.com/anndrox/GillionsGameSync/releases/download/v0.0.66-testing/GillionsGameSyncTesting.json`

This successor retains authenticated Gillions Party Finder, local Beastmaster and
previous performance corrections. It adds **Testing-only local submarine retention**
and manual sanitized community-export preparation. No server deployment, Stable
publication, third-party plugin dependency, voyage control or upload endpoint.

## In-game checks — not yet validated by fixtures

1. Replace the previous manual Testing JSON, refresh Dalamud and confirm
   `Gillions Game Sync Testing 0.0.66.0`. Preserve configuration. Do not restart
   the game or delete data merely for this test; use normal plugin update controls.
2. Open `/gillionssubs` or **Submarine voyage retention** in settings. Confirm
   local retention and community preparation are off. Ordinary HTTPS sync/PF must
   retain their existing independent pairing and consent behavior.
3. Enable local retention. Enter the FC workshop and use the voyage menus
   manually. Compare name/rank, four part row IDs, base/bonus stats, ordered current
   route and expected return with the game. Select a different planned route and
   confirm it does not replace current voyage history.
4. While a voyage is in flight, open the menu normally so its expected-return
   anchor/build can be retained. Later view its actual results normally. Check
   producing sector, item ID/quantity/HQ, experience and unlock flags. Opening
   before data loads must show unavailable, not empty. No interfaces/actions
   should happen automatically.
5. View the same result repeatedly, then update/reload the plugin through normal
   controls. Retained result count must not grow for duplicates or disappear.
   Test a successive voyage and a different workshop/character without mixing
   scoped data. A later build must remain separate from the frozen in-flight build.
6. Test a result first seen without an anchor. It must be retained as unlinked,
   not counted/exported as a verified voyage. A late-only producing build is
   unavailable, not silently reconstructed. Unverified selected pointers fail closed.
7. Community preparation is a **second opt-in** and does not upload. Enable only
   if allowed to share FC voyage routes/builds/rewards/times; observe a new result,
   prepare the sanitized JSON and inspect it before copying. No name, FC/house,
   account/reporter identifiers, credential, local hash or raw log should appear.
   Earlier results observed before consent remain ineligible. Disable consent:
   export is unavailable, prepared data clears, retained history stays.
8. Check interface-read/save timing and gameplay responsiveness with the window
   open and closed. There must be no recurring submarine sampling or hidden requests.
   Bounds are tested synthetically, not by filling a player's real configuration.

Open the submarine window and **Copy aggregate diagnostics** after one test.
Send that text with version, game/Dalamud version, interface actions, which fields
were wrong/missing and whether a hitch occurred. Do not send the full configuration:
it contains the existing credential and private history. Names are local UI only,
not aggregate diagnostics. Do not substitute a full voyage export for diagnostics
unless you deliberately intend to share the sanitized community dataset.

## Retention and availability

At most 400 anchors/results and 32 current snapshots; reserved serialized component
storage below 4 MiB. At capacity, preserve admitted history, warn and stop admitting
new identities; existing anchors retain completion space. No silent deletion or
unsent-record eviction. Export is not an ACK and does not free space.
Older Testing binaries can drop the unknown retained field when saving. Do not
downgrade as automatic recovery: disable this feature/plugin, preserve configuration
and use a forward correction. Stable's separate configuration is unchanged.

Departure is unavailable. Producing build is recorded only when observed in flight,
otherwise null. Sector rewards require coherent native sector and displayed
aggregate evidence; voyage-only fallback has no inferred sector/HQ/experience.
Result event timing, pointer membership, live correctness and performance still
need in-game evidence. [Schema, examples and Site requirements](../contracts/submarine-voyages-v1.md).

Managed checks, actual SDK compilation and configuration Save/reload are validation
evidence, not successful live collection. Existing Party Finder deployment is
separate from this local-only addition. No submarine production upload is claimed.
