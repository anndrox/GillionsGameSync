# Testing

Run the complete local verification from the repository root:

```powershell
./scripts/verify.ps1
```

This runs the focused C# fixtures, source-level barding and performance contracts, and both stable-compatible and testing-compatible Release builds. Warnings are treated as failures by the focused test project; release work requires zero build warnings and zero build errors.

Verification also validates the committed stable Dalamud repository manifest, including its required field types, stable identity, versioned HTTPS download URLs, repository URL, bounded size, anonymous URL shape, and malformed/missing-field rejection fixtures. Game Sync does not fetch this file at runtime; Dalamud consumes it before installing or updating the plugin, so client-side retry and last-known-good cache behavior do not apply inside the plugin.

Client-facing collection changes also require proportionate in-game validation against the current Dalamud and FFXIV patch. Test unavailable and partially loaded state as well as the positive path. Never use production player data as a fixture.

Testing builds use a separate Dalamud identity and configuration from stable. Passing local and testing-feed checks does not by itself authorize stable publication.

After successful pairing or re-pairing, the client schedules one immediate `character` resource sync so the selected character appears without a manual Sync action. This one-time hydration does not enable recurring automatic sync, alter the normal 30-second scoped rotation, or perform gameplay/provider actions. Normal Retainer presence is also rescheduled immediately and remains subject to the existing paired automatic-sync safety gate.

Stable `1.0.27` uses the same Retainer contract implementation as testing while retaining the `GillionsGameSync` product identity. An older or testing-only server response cannot activate stable Retainer traffic: stable requires the server to acknowledge the exact stable product and contract v1 before accepting observation/result support or planner support. Ordinary resources continue syncing when that acknowledgement is absent or malformed.

Upgrading from stable `1.0.25` leaves AutoRetainer plan access disabled. Test the explicit opt-in independently from read-only Retainer observation, including AutoRetainer absent, unloaded, outdated, API-unready, and planner-disabled states. Multi Mode remains diagnostic and is not a prerequisite.

Testing `0.0.61` preserves the Retainer observation contract and controlled AutoRetainer plan write path, including the live-validated conflict-review restoration retry. It remains inert until the user explicitly enables Gillions planner access, AutoRetainer is loaded/API-ready with its planner enabled, the testing server advertises plan support, and an intentionally synchronized latest revision is delivered to the paired owning device. AutoRetainer Multi Mode is separate and is not required for Gillions plan delivery.

The unpublished `0.0.63` testing candidate accepts up to 500 fixed executions and advertises that limit with `retainer.autoretainer.plan-limit-500.v1`. It accepts `restart_plan` only when the server requires `retainer.autoretainer.restart-completion.v1`; AutoRetainer then applies its native Restart Plan behavior after the final valid entry. The client does not implement a second local repeat loop.

The client resolves the current character and stable retainer identity immediately before access, validates venture IDs against the current Lumina sheet, fetches a fresh AutoRetainer object for that exact retainer, and refuses the write if that retainer's planner is disabled or unavailable. Presence reports `retainerPlannerEnabled: null` and publishes timestamped entries in `retainerPlannerReadiness`; this prevents readiness from one retainer being borrowed for another. Servers must require `retainer.autoretainer.per-retainer-readiness.v1` and treat missing, duplicate, unavailable, or stale entries as not ready.

Assign Quick Venture, Restart Plan, and Do Nothing are written through AutoRetainer's existing completion enum and read back within the same framework update. Success is acknowledged only when the exact managed plan, ordered steps, completion behavior, linkage, index, and enabled state match. The first write preserves the complete prior embedded plan, linkage, enabled state, and `PlanCompleteBehavior`. Later writes and restoration use compare-and-set hashes; outside changes are reported without overwrite. After such a conflict, a new deliberate Sync may use only the exact observed hash acknowledged to Gillions as its next compare-and-set baseline. A further local change fails closed again.

Retainer acceptance must cover login/logout and character switching, unavailable/partial/authoritative-empty evidence, loaded and unloaded gear/containers, duplicate and revised result evidence, malformed/partial/wrong-resource acknowledgements, presence expiry/backoff, and server-gate disablement. A bare `2xx` must never remove a pending result.

Plan-delivery acceptance additionally covers planner opt-out, stable/older-client exclusion, latest-only delivery, lease retry/expiry, multiple-device ownership, the 24/25 and 500/501 boundaries, Restart Plan round-trip, mixed per-retainer readiness, invalid venture rejection, Quick Venture apply/read-back mismatch, outside-change CAS conflict, immediate read-back, exact acknowledgement, original-plan restoration, and confirmation that unrelated Retainers and characters remain unchanged.

## Unpublished Venture compatibility contract

This candidate remains contract version `1`; the extension is additive and capability-gated. Application / Backend must not infer support from the plugin version string alone.

- `maximumPlanExecutions` is `500`. Both the number of ordered `steps` and the sum of their positive `repetitions` must be at most 500.
- `supportedCompletionActions` is `restart_plan`, `assign_quick_venture`, and `do_nothing`.
- A delivery using more than 24 executions must require `retainer.autoretainer.plan-limit-500.v1`.
- A `restart_plan` delivery must require `retainer.autoretainer.restart-completion.v1`. It selects AutoRetainer's native `Restart_plan` completion mode; Gillions does not run a local repeat worker.
- Every plan delivery must require `retainer.autoretainer.per-retainer-readiness.v1` and match one current `retainerPlannerReadiness` entry by exact `retainerId`. The entry must have `status: "ready"`, `plannerEnabled: true`, and an `observedAtUtc` within the server's accepted online window. Missing, duplicate, disabled, unavailable, or stale entries are not ready. The legacy `retainerPlannerEnabled` aggregate is deliberately `null`.
- An `assign_quick_venture` delivery must require `retainer.autoretainer.quick-completion-readback.v1` in addition to the existing Quick Venture capability. `applied` means the exact target retainer was resolved, its planner was enabled, the write succeeded, and immediate read-back matched the requested ordered steps and `Assign_Quick_Venture` completion mode. `autoretainer_not_ready`, `retainer_not_found`, `ipc_rejected`, and `read_back_mismatch` remain non-success outcomes.
- Clients that omit any required capability must not receive the associated delivery. They may continue using the older 24-execution Assign Quick Venture or Do Nothing contract when the server deliberately sends only the older capability set.

Stable release-candidate acceptance also covers exact `acceptedClientProduct` and `acceptedContractVersion` matching, testing/stable identity separation, malformed and foreign-product responses, and confirmation that no Retainer resource is added to ordinary sync scopes until the server accepts the stable client.
