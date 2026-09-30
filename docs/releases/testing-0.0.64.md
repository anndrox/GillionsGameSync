# Gillions Game Sync Testing 0.0.64

This testing successor combines the local Beastmaster ownership view, the
Party Finder/XIVPF contributor and the performance improvements on
`codex/game-sync/beastmaster-party-finder-testing`.

Manual Dalamud custom repository URL:

`https://github.com/anndrox/GillionsGameSync/releases/download/v0.0.64-testing/GillionsGameSyncTesting.json`

## Server-hosted testing limitation

This published package predates the owner's server-hosted testing direction.
Its editable Gillions connection field accepts HTTPS only, so it cannot pair
with the existing HTTP LAN test gateway. A testing-only successor is prepared
in source, but plaintext test credential risk acceptance or an HTTPS endpoint
is required before publication. Do not use production pairing to work around
that limitation.

The Gillions connection field does not change the separate XIVPF contribution
endpoint. This package remains loopback-only. Leave contribution disabled for
server-hosted testing until Site Operations supplies an isolated receiver on the
owner-selected test server and a plugin successor targets its verified URL.
Do not create a gaming-PC service or send testing traffic to production.

## Install

1. Open Dalamud settings (`/xlsettings`), Experimental, Custom Plugin Repositories.
   Replace the earlier manual `0.0.63` entry with the URL above and save.
2. In the plugin installer (`/xlplugins`), refresh/update and confirm
   **Gillions Game Sync Testing**, version **0.0.64.0**.
3. Keep the configuration. An existing valid `0.0.63` pairing can remain paired;
   users upgrading from older unbound builds follow the displayed pairing prompt.
4. Disable duplicate development/standalone Beastmaster copies. For a useful
   performance comparison, temporarily disable stable Game Sync while running
   this testing copy, preserving its configuration.

## What changes

- Visible settings-state publication runs at four updates per second during
  ordinary operation, with immediate eligibility after settings/session changes.
- Party Finder batch sorting, JSON serialization and HTTP startup run on a worker.
- Static Armoire catalog projections are reused. Live ownership is still reread.
- Empty pending-record queues skip unnecessary JSON header serialization.
- Local diagnostics include collection duration on the framework thread.

Collection/polling intervals and the existing ownership, acknowledgement and
cancellation boundaries remain unchanged. Beastmaster sampling remains opt-in,
local-only and unsaved. Testing XIVPF traffic remains loopback-only. Hunt-bill
collection and website-to-game map requests are not implemented in this package.

## In-game checklist

1. **Load and settings:** confirm `0.0.64.0`, retained pairing/settings and normal
   load. Open/close settings and change Automatic sync or website item-link
   permission; controls and connection status should respond normally.
2. **Normal play:** spend two minutes moving around with settings closed, then
   repeat with Data status and Diagnostics expanded. Record FPS/frame times and
   any periodic hitch, keeping zone, graphics settings and background plugins
   comparable. Note whether a hitch coincides with a collection diagnostic.
3. **Automatic/manual sync:** observe ordinary automatic sync, then click
   **Sync now**. Verify supported data reaches the paired character on the site.
   Check for repeated unchanged uploads or noticeable stalls during collection.
4. **Inventory and Armoire:** move an item and confirm inventory sync. If
   practical, store/remove an eligible Armoire item and confirm ownership updates;
   cached definitions must not freeze live ownership. Repeat an inventory sync.
5. **Retainers:** view a venture result, accept it, and inspect listings/inventory.
   Confirm the usual observations/results arrive once and there is no new hitch.
   Retainer uploads still depend on the server's existing compatibility acceptance.
6. **Website item link:** with its permission enabled, use the site's Link in game
   button. Expect one native link in chat. Disable permission and confirm requests
   do not print while it is disabled. This release does not send map flags.
7. **Beastmaster:** open `/gillionsbst` and enable local sampling. Manually open
   the bestiary; compare owned/unowned entries, names and total. After a manual
   capture, check the result updates. Closing the diagnostic window must stop
   sampling. If current-character freshness is unverified, leave sampling on,
   log out/in and manually open the bestiary again.
8. **Character changes/reload:** prior Beastmaster results must clear on logout
   or switching character. Require fresh loading for the next character and
   compare again. Reloading the plugin leaves Beastmaster sampling off and
   preserves existing configuration.
9. **Party Finder:** with the official Remote Party Finder service running
   locally at `http://127.0.0.1:8000`, enable contribution and browse/refresh
   Party Finder. Expect batching after about ten seconds without a serialization
   hitch. Confirm local acceptance; disabling contribution stops further work.
   Without that local service, leave contribution off or report the expected
   connection failure separately: this testing package cannot contribute to
   production xivpf.com. Inspect Dalamud's plugin log for contribution success
   or failure messages; those messages are separate from the copied sync report.

## Copy diagnostics back

Testing sync diagnostics record automatically. Open **Gillions Game Sync Testing**
settings, expand **Diagnostics**, and choose **Clear diagnostics** before one test.
Reproduce the problem, return promptly, then choose **Copy diagnostic report**
and paste the clipboard text into the chat. Only the latest 40 entries are kept.
Repeat separately for another issue so the relevant entries are not displaced.

For Beastmaster, choose **Copy current diagnostic text** in `/gillionsbst` and
paste that report too. It includes the loading state, counts and pet results.
For a Party Finder issue, also copy the relevant Gillions contribution error
lines from Dalamud's log viewer, reviewing them before sharing.

Include the action that triggered the issue, approximate local time/time zone,
plugin/game/Dalamud versions, whether Automatic sync and optional sampling or
contribution were enabled, and FPS/frame times before/during the issue. State
whether it repeats and whether settings were open. Do not share plugin
configuration, device credentials or pairing codes. Review reports for gameplay
details you do not want to share; share only the relevant lines from the broader
Dalamud log rather than a full log dump.

## Validation and limits

The candidate uses the same testing product identity as `0.0.63`. Both product
builds, policy/contract fixtures and actual Dalamud configuration preservation
must pass before publication. Synthetic tests demonstrate bounded view updates
and nonblocking Party Finder HTTP startup; they do not establish live-game
frame-time improvement. Installed-client results remain the next evidence.

If a problem occurs, disable the optional feature or testing plugin and preserve
configuration. Report diagnostics for a forward correction. Stable distribution,
main integration, the existing Gillions-hosted feed and server deployment are
outside this testing release.
