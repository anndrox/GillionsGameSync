# Testing

Run the complete local verification from the repository root:

```powershell
./scripts/verify.ps1
```

This runs the focused C# fixtures, source-level barding and performance contracts, and both stable-compatible and testing-compatible Release builds. Warnings are treated as failures by the focused test project; release work requires zero build warnings and zero build errors.

Client-facing collection changes also require proportionate in-game validation against the current Dalamud and FFXIV patch. Test unavailable and partially loaded state as well as the positive path. Never use production player data as a fixture.

Testing builds use a separate Dalamud identity and configuration from stable. Passing local and testing-feed checks does not by itself authorize stable publication.
