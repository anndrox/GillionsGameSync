# Beastmaster local test in Game Sync Testing

This task-branch candidate incorporates the proven read-only Beastmaster reader
into **Gillions Game Sync Testing**. It is off by default on every plugin load.
The stable plugin excludes the reader. Source push is not a plugin/feed release.

## Build and load

```powershell
./scripts/package.ps1 -Channel testing -Version 0.0.63 -PublishedAt 1789085847
```

The DLL and adjacent JSON are in `artifacts/package/testing/0.0.63/build/`.
The identity is the existing `GillionsGameSyncTest`, version `0.0.63.0`.

1. Disable the earlier **Gillions Beastmaster Local Diagnostic** and remove its
   Dev Plugin Location to avoid two handlers for `/gillionsbst`.
2. If an existing **Gillions Game Sync Testing** is enabled, disable it first.
   Keep its configuration; do not run two copies of that same plugin identity.
3. In Dalamud settings (`/xlsettings`), Experimental, enable Developer Mode.
   Under Dev Plugin Locations, select this candidate's `GillionsGameSyncTest.dll`,
   save, then enable the development copy of **Gillions Game Sync Testing**.
4. Open its settings and choose **Beastmaster local test**, or run `/gillionsbst`.
   The window initially says sampling is off; enable it explicitly.

No pairing is needed for the Beastmaster view. Existing Game Sync features retain
normal behavior and may synchronize ordinary data if this testing identity was
already paired. Beastmaster results never enter those uploads or saved records.

## Owner-run checks

- Confirm no Beastmaster sampling occurs until enabled. Reloading the plugin or
  closing its Beastmaster window must leave sampling off.
- Observe loading before/after manually opening the bestiary. If the native list
  was already received, leave the window open with sampling on, log out/in, and
  open the bestiary again. Unknown is not an empty collection.
- Compare pet names, owned/unowned entries and total against the bestiary.
- After a manual capture, confirm the appropriate entry and count update.
- Log out and switch to a different character. The prior list must disappear;
  admit the new character's results only after fresh loading and compare again.
- If useful, copy the current diagnostic text and report the action and agreement.
  The explicit clipboard action includes pet results and version, not character
  or account identifiers. Do not share normal plugin configuration or credentials.

Disable the development copy and remove its Dev Plugin Location when finished;
then re-enable the installed testing copy if desired. No feed was updated.

## Evidence and remaining uncertainty

The prior standalone reader passed an owner-run fresh-load and bestiary ownership
comparison. That acceptance belongs to diagnostic commit `cdbd87a`; it is supporting
evidence, not live acceptance of this integrated candidate. Capture changes and a
different character remain untested in-game. The candidate's test results are
reported with its exact commit/build identity.

The installed SDK supplies typed `XBMManager` loading state and `IsPetUnlocked`.
Its experimental `XBMPet.Pet` reference supplies names. That SDK evaluation opt-in
is confined to the testing-only view. No offsets, packet hooks, load requests or
native writes are introduced. A missing loading transition, unavailable catalog,
read failure or native-count/catalog disagreement leaves ownership unknown.

The view is owned/disposed by the existing testing plugin. Sampling, freshness and
results stay in memory. Required verification includes freshness fixtures, actual
stable/testing assembly isolation and the existing repository checks. The stable
build has neither the local view nor its readiness policy. There are no upload,
server, page, persisted-contract or production changes in this candidate.

[Official developer DLL-location controls](https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Interface/Internal/Windows/Settings/Widgets/DevPluginsSettingsEntry.cs)