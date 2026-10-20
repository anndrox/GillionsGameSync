# Testing 0.0.90 — 7.57 hotfix compatibility (unpublished candidate)

Status: **source qualified, physical client pending**. Nothing is published.

## What changes

- A single per-collector compatibility table (`NativeCompatibility.cs`) replaces
  the exact build checks that were scattered across the Hunt, submarine, travel,
  Dashboard and FATE collectors. See [native compatibility](../native-compatibility.md).
- Hunt, submarine and travel native reads, and the client side of the FATE source
  match, are qualified for the current release runtime:
  game `2026.10.16.0000.0000`, Dalamud 15.0.3.9
  (`30509c3b4fd00edd9a3d65b7bbf16433d84be03b`), FFXIVClientStructs 7.57.0.9414
  (`dd57986b8287e7c6ed9c27198f8c76cb04a81dfe`), Lumina 7.7.0, Excel 7.5.1.
- Dashboard facts stay September-only, because they were not requalified.
- Dalamud 15.0.3.8 (ClientStructs 7.57.0.9386) is not admitted.
- Collectible readiness from PR #6 is included unchanged.

Consent, Site permission leases, capability negotiation, payload shapes, cadences
and retention are unchanged. Hunt-routing location still defaults OFF. No new
collection, no new upload resource and no gameplay writes.

## What still depends on the Site

As of its 2026-10-20 source, the Site validates only the September tuple for:
- FATE source admission (it also needs the exact Dalamud revision and collector version `0.0.90.0`);
- Hunt item coverage;
- travel context.

It also checks Hunt and submarine personal observations against game-version
reference catalogs. Until Site Operations admits the hotfix tuple, those uploads
are expected to be rejected, and the client backs off without changing local state.

## Owner physical acceptance procedure

Use only a Testing build produced from the exact candidate commit.

1. **Prepare.**
   - Launch through XIVLauncher so Dalamud updates.
   - In `/xldev` → About, confirm Dalamud **15.0.3.9**.
   - Confirm the game reports **2026.10.16**.
   - If either differs, stop and record what you see. Other runtimes must show
     "unsupported" and read nothing.
2. **Install.**
   - Disable Public and any dev copy.
   - Install Testing 0.0.90.0 from the Testing feed, or as a dev plugin when
     testing an unpublished build.
   - Never enable two copies at once.
3. **Hunts.**
   - With at least one Hunt bill held, open Hunt Progress. Bill names and counts
     should appear and should match the in-game Hunt log.
   - With no bills held, it must say that no rows is not proof of no bills.
   - Teleport once and confirm the status recovers after the loading screen.
4. **Submarines** (only if you have workshop access):
   - Enable private submarine retention.
   - Open the voyage list in the workshop.
   - The Testing diagnostics should list the loaded submarines, with names, ranks
     and return times matching the game.
   - Leave the workshop and confirm history is preserved, not emptied.
5. **Travel** (optional, since its upload is Site-held):
   - Turn on Hunt routing location consent.
   - Open the Teleport window. The Testing travel diagnostic should show your
     rounded map position and a partial destination list.
   - Turn consent OFF and confirm the volatile context clears.
6. **FATE.**
   - Until the Site admits the hotfix source, the status shows "Update required"
     (Site reason `FATE_SOURCE_UNSUPPORTED`) or "Temporarily unavailable".
   - The only FATE traffic before that admission is the existing capability
     discovery request, which offers the client's source identity. No FATE table
     reads or contributions happen until the Site grants the source.
7. **Ordinary sync and performance.**
   - Inventory, character and retainer sync behave as before.
   - Watch `/xldev` plugin statistics for Game Sync frame time during normal play.
8. **Unsupported fallback.**
   - If Dalamud updates again before the test, confirm Hunt Progress reports
     "Update required" and no Hunt read occurs.
9. **Report.**
   - Report only safe summaries, cropped screenshots or bounded failure lines.
   - Never share configuration files or credentials.

Passing steps 1–4 and 7 is the physical evidence required to move the Hunt and
Submarine families from *physical client pending* to accepted for this runtime.
Steps 5 and 6 additionally need Site admission.
