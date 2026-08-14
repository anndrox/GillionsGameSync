# Testing

Run the complete local verification from the repository root:

```powershell
./scripts/verify.ps1
```

This runs the focused C# fixtures, source-level barding and performance contracts, and both stable-compatible and testing-compatible Release builds. Warnings are treated as failures by the focused test project; release work requires zero build warnings and zero build errors.

Client-facing collection changes also require proportionate in-game validation against the current Dalamud and FFXIV patch. Test unavailable and partially loaded state as well as the positive path. Never use production player data as a fixture.

Testing builds use a separate Dalamud identity and configuration from stable. Passing local and testing-feed checks does not by itself authorize stable publication.

Testing `0.0.56` preserves the `0.0.55` Retainer observation contract and adds the first controlled AutoRetainer plan write path. It remains inert until the user explicitly enables Gillions planner access, AutoRetainer is loaded/API-ready with its planner enabled, the testing server advertises plan support, and an intentionally synchronized latest revision is delivered to the paired owning device.

Every delivery is limited to 24 fixed executions. The client resolves the current character and stable retainer identity immediately before access, validates venture IDs against the current Lumina sheet, fetches a fresh AutoRetainer object, applies either Assign Quick Venture or Do Nothing completion behavior, writes and reads back within one framework update, and acknowledges only the verified result. The first write preserves the complete prior embedded plan, linkage, enabled state, and `PlanCompleteBehavior`. Later writes and restoration use compare-and-set hashes; outside changes are reported without overwrite. The client never expands or repeats a Gillions queue locally.

Retainer acceptance must cover login/logout and character switching, unavailable/partial/authoritative-empty evidence, loaded and unloaded gear/containers, duplicate and revised result evidence, malformed/partial/wrong-resource acknowledgements, presence expiry/backoff, and server-gate disablement. A bare `2xx` must never remove a pending result.

Plan-delivery acceptance additionally covers planner opt-out, stable/older-client exclusion, latest-only delivery, lease retry/expiry, multiple-device ownership, the 24/25 boundary, invalid venture rejection, outside-change CAS conflict, immediate read-back, exact acknowledgement, original-plan restoration, and confirmation that unrelated Retainers and characters remain unchanged.
