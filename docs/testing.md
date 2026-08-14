# Testing

Run the complete local verification from the repository root:

```powershell
./scripts/verify.ps1
```

This runs the focused C# fixtures, source-level barding and performance contracts, and both stable-compatible and testing-compatible Release builds. Warnings are treated as failures by the focused test project; release work requires zero build warnings and zero build errors.

Client-facing collection changes also require proportionate in-game validation against the current Dalamud and FFXIV patch. Test unavailable and partially loaded state as well as the positive path. Never use production player data as a fixture.

Testing builds use a separate Dalamud identity and configuration from stable. Passing local and testing-feed checks does not by itself authorize stable publication.

Testing `0.0.55` adds the Retainer observation contract behind server capability negotiation. It sends testing presence at an approximately 30-second jittered cadence, retains character-partitioned result evidence across retries/restarts, and uploads `retainer_ventures` only when the server advertises both observation and exact-result support. AutoRetainer access is read-only; plan application, restoration, delivery polling, and completion behavior are absent from the runtime path.

Retainer acceptance must cover login/logout and character switching, unavailable/partial/authoritative-empty evidence, loaded and unloaded gear/containers, duplicate and revised result evidence, malformed/partial/wrong-resource acknowledgements, presence expiry/backoff, and server-gate disablement. A bare `2xx` must never remove a pending result.
