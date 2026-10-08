# Gillions Game Sync Testing 0.0.63

Published as a testing-only GitHub prerelease replacing testing 0.0.62 and using
the same `GillionsGameSyncTest` identity. Add the GitHub-hosted
repository JSON manually in Dalamud:

`https://github.com/anndrox/GillionsGameSync/releases/download/v0.0.63-testing/GillionsGameSyncTesting.json`

## What changes

- Adds a local Beastmaster ownership view to Game Sync Testing. Open its settings
  and choose **Beastmaster local test**, or run `/gillionsbst`. Sampling starts off
  each time the plugin loads; enable it explicitly. Closing the window stops it.
- Shows loaded pet names and owned/unowned status. Unloaded or unverified data
  stays unknown; logout and character changes clear prior results. Beastmaster
  observations are not saved, logged or uploaded. The website page is deferred.
- Adds the independent, off-by-default Party Finder contribution setting. The
  testing product accepts only a loopback XIVPF endpoint and never follows
  redirects, so it cannot post the testing payload to production. Run the
  official Remote Party Finder service locally before enabling contribution.
- Includes current Game Sync corrections since testing 0.0.62: origin-bound
  pairing, character-owned records, preserved inactive legacy history, exact
  acknowledgements and bounded pending storage. Ordinary character and Retainer
  observations remain available under their existing controls and server gates.
- Removes AutoRetainer discovery/control and venture planning. This does not
  cancel plans already installed in AutoRetainer; use its own controls for those.

## Upgrade

Add the manual GitHub repository URL above, then install or update
**Gillions Game Sync Testing** through Dalamud's installer.
Do not install this as the stable plugin or run a development copy alongside it.
Disable/remove the earlier standalone Beastmaster diagnostic to avoid duplicate
`/gillionsbst` handlers.

**Preserve your configuration and pair once after updating from 0.0.62.** Verify
the displayed destination before pairing. Old local history remains preserved
and inactive when its ownership cannot be proved; website history is unchanged.
A valid pairing created by the new format can survive later plugin reloads.

Automatic sync remains one global switch. Pairing and paired startup perform one
character sync and presence request even if recurring Automatic sync is off.
Website item links have a separate switch. Retainer uploads require server
compatibility acceptance. No pairing is needed just to view Beastmaster locally.

Pending records share a 10,000-record / 8 MiB budget. Admitted records are kept;
new event recording pauses with a gap warning at capacity. Records from earlier
owned pairing generations still count; legacy unowned history is preserved outside
that budget. Re-pairing does not clear old records, and missed events are not recovered.

## Verification and recovery

The earlier standalone Beastmaster diagnostic passed an owner bestiary comparison.
Release-candidate live checks and independent reviews are tracked separately;
that prior result is not live acceptance of this package. The SDK pet-name schema
is experimental and confined to the testing build.

If this candidate misbehaves, first disable Beastmaster sampling or disable the
plugin. Preserve configuration. Do not automatically downgrade to 0.0.62: older
planner-capable code and configuration saving require a specific compatibility
review. A forward correction is preferred. A feed withdrawal only prevents new
updates; it does not undo packages already installed by users.

The existing `gillions.app` feed, stable package, server behavior and website
remain unchanged. Installed-user acceptance is still required before either
feature is integrated to `main`.
