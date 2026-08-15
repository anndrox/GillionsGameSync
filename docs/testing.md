# Testing

Run the complete local verification from the repository root:

```powershell
./scripts/verify.ps1
```

This runs the focused C# fixtures, source-level barding and performance contracts, and both stable-compatible and testing-compatible Release builds. Warnings are treated as failures by the focused test project; release work requires zero build warnings and zero build errors.

Client-facing collection changes also require proportionate in-game validation against the current Dalamud and FFXIV patch. Test unavailable and partially loaded state as well as the positive path. Never use production player data as a fixture.

Testing builds use a separate Dalamud identity and configuration from stable. Passing local and testing-feed checks does not by itself authorize stable publication.

Stable `1.0.26` uses the same Retainer contract implementation as testing while retaining the `GillionsGameSync` product identity. An older or testing-only server response cannot activate stable Retainer traffic: stable requires the server to acknowledge the exact stable product and contract v1 before accepting observation/result support or planner support. Ordinary resources continue syncing when that acknowledgement is absent or malformed.

Upgrading from stable `1.0.25` leaves AutoRetainer plan access disabled. Test the explicit opt-in independently from read-only Retainer observation, including AutoRetainer absent, unloaded, outdated, API-unready, and planner-disabled states. Multi Mode remains diagnostic and is not a prerequisite.

Testing `0.0.60` preserves the Retainer observation contract and controlled AutoRetainer plan write path. It also sends the explicit testing product and channel identity required by the production contract. It remains inert until the user explicitly enables Gillions planner access, AutoRetainer is loaded/API-ready with its planner enabled, the testing server advertises plan support, and an intentionally synchronized latest revision is delivered to the paired owning device. AutoRetainer Multi Mode is separate and is not required for Gillions plan delivery.

Every delivery is limited to 24 fixed executions. The client resolves the current character and stable retainer identity immediately before access, validates venture IDs against the current Lumina sheet, fetches a fresh AutoRetainer object, applies either Assign Quick Venture or Do Nothing completion behavior, writes and reads back within one framework update, and acknowledges only the verified result. The first write preserves the complete prior embedded plan, linkage, enabled state, and `PlanCompleteBehavior`. Later writes and restoration use compare-and-set hashes; outside changes are reported without overwrite. After such a conflict, a new deliberate Sync may use only the exact observed hash acknowledged to Gillions as its next compare-and-set baseline. A further local change fails closed again. The client never expands or repeats a Gillions queue locally.

Retainer acceptance must cover login/logout and character switching, unavailable/partial/authoritative-empty evidence, loaded and unloaded gear/containers, duplicate and revised result evidence, malformed/partial/wrong-resource acknowledgements, presence expiry/backoff, and server-gate disablement. A bare `2xx` must never remove a pending result.

Plan-delivery acceptance additionally covers planner opt-out, stable/older-client exclusion, latest-only delivery, lease retry/expiry, multiple-device ownership, the 24/25 boundary, invalid venture rejection, outside-change CAS conflict, immediate read-back, exact acknowledgement, original-plan restoration, and confirmation that unrelated Retainers and characters remain unchanged.

Stable release-candidate acceptance also covers exact `acceptedClientProduct` and `acceptedContractVersion` matching, testing/stable identity separation, malformed and foreign-product responses, and confirmation that no Retainer resource is added to ordinary sync scopes until the server accepts the stable client.
