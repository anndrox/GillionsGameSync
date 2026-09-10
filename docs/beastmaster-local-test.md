# Beastmaster local diagnostic

Temporary, owner-run read-only test. No pairing, upload, configuration persistence,
page, or automatic gameplay. This is not a stable collector or release.

Build from this task branch with the installed Dalamud API 15 SDK:

```powershell
dotnet build GillionsGameSync.csproj -c Release -warnaserror -p:GillionsTestBuild=true -p:GillionsBeastmasterDiagnostic=true -p:Version=0.0.2 -p:OutputPath=artifacts/beastmaster-local/
```

The DLL and adjacent JSON are in `artifacts/beastmaster-local/`.
The distinct identity is `GillionsBeastmasterDiagnostic`; its only plugin entry
point is the diagnostic. The ordinary sync plugin is not started.

## Load and check

1. Open Dalamud settings (`/xlsettings`), Experimental, enable Developer Mode.
2. Under Dev Plugin Locations, select the built `GillionsBeastmasterDiagnostic.dll`,
   then save. Enable **Gillions Beastmaster Local Diagnostic** in the plugin installer.
3. Run `/gillionsbst`; enable local sampling. No pairing is needed.
4. Check the loading state, then open the bestiary manually. If the list was
   already received before sampling started, ownership remains unknown. Keep the
   diagnostic open and sampling enabled, log out/in, then open the bestiary.
5. Compare names, owned/unowned entries, and total with the bestiary. After a manual
   capture, check that the appropriate entry and total change. Repeat logout/login
   and, if available, a character switch: the old character's list must disappear.
6. Copy the diagnostic text if useful. This explicit clipboard action includes pet
   results, not account or character identifiers. Report the action taken and
   whether the game and diagnostic agree. Close the window to stop sampling.
7. Disable the diagnostic and remove its Dev Plugin Location when finished.

## Evidence and limits

The installed SDK's typed `XBMManager` supplies loading state and `IsPetUnlocked`.
The installed experimental `XBMPet.Pet` reference supplies pet names. The SDK marks
that catalog experimental; its warning is suppressed only inside this diagnostic.
No offsets, packet hooks, load requests or native writes are introduced.

Unknown/not-loaded is never represented as an empty collection. Fresh loading
must be observed for the current character; a received list at startup is not
sufficient. Native-count/catalog mismatch also remains unknown. A missed loading
transition may therefore leave the diagnostic unknown; report that observation.
The loading transition is a conservative test guard, not proof that the game's
native state is correctly associated with the character.

Verification: diagnostic warning-as-error build and `scripts/verify.ps1` passed,
including freshness fixtures for startup, loading, character switch, logout,
reset and unexpected state. Initial compilation identified the absent stable name
field; installed metadata resolved the experimental pet reference. Stable and
ordinary testing builds remain separate. In-game loading, bestiary comparison,
capture updates and session transitions are **not yet validated**.

Source references:
- [Native manager](https://github.com/aers/FFXIVClientStructs/blob/f0741b7604bdd537b62467c07ad82fdf584e0abb/FFXIVClientStructs/FFXIV/Client/Game/XBMManager.cs)
- [Dalamud developer DLL locations](https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Interface/Internal/Windows/Settings/Widgets/DevPluginsSettingsEntry.cs)