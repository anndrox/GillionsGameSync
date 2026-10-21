# Testing 0.0.90 — 7.57 hotfix compatibility (unpublished candidate)

Status: **Hunt Progress physically accepted** on game `2026.10.16.0000.0000` /
Dalamud 15.0.3.9 / ClientStructs 7.57.0.9414 (owner test, below). Submarine reads are
**source qualified, not physically tested** (no workshop access). FATE client
identity was observed; FATE collection is **pending Site admission**. Travel was
**not exercised** (consent stayed OFF). Nothing is published. The
short owner checklist is [testing-0.0.90-checklist.md](testing-0.0.90-checklist.md).
Site requirements are in [the Site admission handoff](../contracts/hotfix-2026.10.16-site-admission.md).

## What changes

- A single per-collector compatibility table (`NativeCompatibility.cs`, merged in
  anndrox/GillionsGameSync#7) replaces the scattered exact build checks. See
  [native compatibility](../native-compatibility.md).
- The current release runtime is admitted for the Hunt, Submarine and Travel
  native reads and the client side of the FATE source match:
  - game `2026.10.16.0000.0000`;
  - Dalamud 15.0.3.9 (`30509c3b4fd00edd9a3d65b7bbf16433d84be03b`);
  - FFXIVClientStructs 7.57.0.9414 (`dd57986b8287e7c6ed9c27198f8c76cb04a81dfe`);
  - Lumina 7.7.0, Excel 7.5.1.
- Dashboard facts stay September-only. Dalamud 15.0.3.8 (ClientStructs 7.57.0.9386)
  is not admitted. Every other runtime keeps failing closed.
- Collectible readiness from PR #6 is included unchanged.

Consent, Site permission leases, capability negotiation, payload shapes, cadences
and retention are unchanged. No new collection, upload resource or gameplay writes.

## Owner acceptance record

The owner ran the local Testing 0.0.90.0 candidate in the game. The prepared build's
`GillionsGameSyncTest.dll` sha256 is
`03716a48128ea122e782fd78bbbddc56579582f28f1af7141a392c13a8e230ea`, from compiled
source `24e4548`.

**What the plugin reported:**
- game `2026.10.16.0000.0000`;
- ClientStructs `7.57.0.9414`;
- five positive Hunt bill cache snapshots;
- zero partial or unmatched positive bill snapshots;
- raw and accessor target counters in agreement;
- no unsupported game/SDK status for Hunts.

**What the owner confirmed:**
- Hunt bills matched the in-game Hunt log.
- Individual target kill counts matched the in-game Hunt log.
- Hunt Progress stayed accurate after teleporting or changing zones.
- The plugin was used during normal gameplay, including inventory and retainer
  interaction, with no performance problem reported.

| Family | Result on this runtime |
|---|---|
| Hunt (local Hunt Progress and Hunt reads) | **Physically accepted** |
| Submarine | Source qualified; **not physically tested** (no workshop access). Neither accepted nor failed. |
| FATE | Client source identity observed with no native-version mismatch. Diagnostic status: `Site policy unavailable; session/context changed.` No FATE observations were measured and no contribution occurred. **Collection pending Site admission** and a later runtime check. |
| Travel | **Not exercised**: Hunt-routing location consent stayed OFF. Consent and Site permission gates are unchanged. |

**Not tested:**
- private Site uploads (Hunt bills v1/v2, submarines);
- Site acceptance of any upload.

## What this test can and cannot prove

| Area | Proven by this procedure | Needs the Site first |
|---|---|---|
| Hunt Progress, local Hunt reads and retention | Yes | — |
| Submarine local reads and retention | Yes | — |
| Hunt / submarine private uploads | No | Hotfix references, and admission of collector version `0.0.90.0` |
| FATE observations | Only that the client offers the right source and reads nothing until granted | FATE source admission |
| Travel (Hunt routing location) | Only that nothing is read without consent | Travel tuple admission, plus your account's routing consent |
| Site reference data (Hunt targets, submarine parts/sectors, FATE references) | No | A reviewed 2026.10.16 catalog on the Site, via Scribe or a reviewed no-change statement |

A Site rejection of a correct local observation is a **Site dependency**, not a
plugin failure. The FATE diagnostics show the Site's own reason code, so the two
can be told apart.

## Candidate build

- **Identity:** `Gillions Game Sync Testing`, InternalName `GillionsGameSyncTest`,
  version `0.0.90.0`, built from the PR #8 head commit.
- **Product scope:** TEST-only HTTPS (`https://test.gillions.app`); it never contacts
  production. It shares the existing Testing configuration (same InternalName),
  so an existing TEST pairing is reused and no new pairing is needed. Unpaired
  local checks also work.
- **Build (the existing approved method):** from a checkout of the PR #8 head,
  with Dalamud's dev hooks or `DALAMUD_HOME` pointing at Dalamud 15.0.3.9:

  ```powershell
  ./scripts/package.ps1 -Channel testing -Version 0.0.90 -PublishedAt 1792540800
  ```

  The DLL is then in `artifacts/package/testing/0.0.90/build/GillionsGameSyncTest.dll`.
  The completion report of the preparing session records the exact path, commit
  and SHA-256 of a prebuilt copy.

Nothing in this procedure changes a feed, a custom repository URL or the
published Testing 0.0.88 install. Every plugin change below is an action you
take in Dalamud's own settings.

## Detailed procedure

### 1. Runtime

1. Launch the game through XIVLauncher as usual, so Dalamud updates itself.
2. Use the **release** Dalamud channel. In Dalamud settings, under the
   staging/beta setting, no beta or staging key should be active.
3. After launch, `%APPDATA%\XIVLauncher\addon\Hooks\15.0.3.9` should exist.
   The in-plugin check in step 3.2 is authoritative.

### 2. Install the candidate (your explicit action)

1. `/xlplugins` → Installed: **disable** "Gillions Game Sync" (Public) and
   "Gillions Game Sync Testing", if installed. Disable, don't uninstall, so their
   configuration stays.
2. `/xlsettings` → Experimental → Dev Plugin Locations: add the folder containing
   the candidate `GillionsGameSyncTest.dll`, enable it and save.
3. `/xlplugins` → Dev Tools: enable the development copy of
   "Gillions Game Sync Testing". Never enable two copies of Game Sync at once.

### 3. Identity and compatibility

1. `/gillionssynctest` → Settings → Advanced.
   - Expect `Game Sync 0.0.90.0 · Channel: Testing` and `Game: 2026.10.16.0000.0000`.
2. Advanced → Testing diagnostics → **Hunt diagnostics** → *Copy aggregate Hunt
   diagnostics*.
   - The copied text must show `Game: 2026.10.16.0000.0000; SDK: 7.57.0.9414`.
   - Its status must **not** say `Unsupported game/SDK build`.
   - This text has no character names or IDs and is safe to share.
3. If you are paired to TEST, Advanced → feature health **Hunts** must not show
   `Update required`.

If the SDK shows any other version, stop. That runtime is not qualified, and every
gated collector must report unsupported and read nothing. That is correct
behaviour, so record it as such.

### 4. Hunts (local, no Site needed)

1. Settings → Hunts → enable **Show Hunt Progress**. Off is the default.
2. With at least one Hunt bill held, go to an area with its targets. Rows and
   counts should match your in-game Hunt log.
3. Teleport or change zone once. The panel recovers after the loading screen
   without inventing completion.
4. With no matching bill, the panel must say that no rows is not proof of no bills.
5. Optional: in `/gillionshunts`, *Retain naturally loaded Hunt Bill observations
   locally* (default off) keeps history on this PC only.

### 5. Submarines (only with workshop access)

1. `/gillionssubs`. Note that local retention and community preparation are off
   unless you changed them before.
2. Enable *Retain naturally loaded submarine observations locally*.
3. In the Company Workshop, open the submarine voyage list normally.
   - The window rows list your loaded submarines, with rank and return times
     matching the game.
   - The status must not say `Unsupported game/SDK build`.
4. Leave the workshop. History is preserved, not emptied.
5. *Copy aggregate diagnostics* excludes names and is safe to share. Leave
   community preparation **off**.

### 6. FATE (Site-dependent)

1. `/gillionsfates`. The status must not say `Unsupported source/game matrix`.
2. Expected results:
   - **Unpaired:** no reads. Nothing is collected until Gillions grants the source.
   - **Paired to TEST, before the Site admits the source:** *Remote contribution*
     shows a Site reason such as `FATE_SOURCE_UNSUPPORTED`, and the Hunts-style
     health shows FATEs `Update required` or `Temporarily unavailable`. The only
     traffic is the existing capability discovery request, which offers the
     client's source identity. Record this as **Site-dependent**.
   - **After Site admission:** in a normal public zone with a naturally occurring
     FATE, rows show progress for the FATE's own position. No player coordinates
     are recorded.
3. Do not copy *PRIVATE FATE diagnostics* unless asked. It reveals your world and
   location.

### 7. Travel (Site-dependent; not exercised in this pass)

Hunt routing location consent lives in your Gillions account, is off by default,
and has no in-game switch. Leave it off for this test.

Confirm that nothing in Game Sync suggests location sharing. A full travel test
waits until the Site admits the hotfix travel tuple.

### 8. Ordinary behaviour and performance

1. Play normally for 10–15 minutes: inventory changes, a retainer bell if
   convenient, and a zone change.
2. `/xlstats` (Dalamud plugin statistics): note Game Sync's framework-update
   time. Frame hitches should be no worse than with the published build.
3. Market, Party Finder and item-link settings are unchanged. Nothing new is
   shared.

### 9. Restore

1. Disable the development copy.
2. Remove its Dev Plugin Location if you like.
3. Re-enable whichever Game Sync copy you normally use.

Published Public 1.0.31.2 will still show Hunts `Update required` on this game
version. That is expected until a successor is published.

## Evidence to report

Report pass, fail or Site-dependent for each step, plus:
- the copied aggregate Hunt diagnostics;
- the copied aggregate submarine diagnostics (if you tested submarines);
- the FATE *Remote contribution* line text;
- cropped screenshots of Hunt Progress next to the in-game Hunt log, with your
  character name cropped out;
- the `/xlstats` number.

Never share configuration files, pairing codes, PRIVATE JSON exports, or logs that
contain your character name.

## Acceptance rule

- Steps 3, 4 and 8 passing accepts the **Hunt** family on this runtime. This has
  happened (see the owner acceptance record).
- Step 5 needs workshop access. The owner has none, so submarines remain
  source-qualified. That alone does not block a Testing release.
- FATE and Travel stay **Site-dependent** until the Site admits the tuple.
- PR #8 may then be merged (source integration). Publication remains a separate
  owner decision.
