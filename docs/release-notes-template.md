# Release titles and notes

Use these titles for future GitHub releases:

- Stable: `Gillions Game Sync <version> (Stable)`
- Testing: `Gillions Game Sync <version> (Testing)`

Use the player-facing release version, for example `1.0.31.9` or `0.0.98`. Keep Testing releases marked as prereleases. This format applies to the title and notes only.

Copy the template below into the GitHub release description. Lead with what players will notice, and state what was tested in game. For Stable, also include the recorded ZIP SHA-256.

```markdown
## What's new

- <What players will notice and why it helps.>

## Updating

- <Say which channel this is for and whether players need to do anything after updating in Dalamud.>
- <Say whether sharing choices or pairing change. For Testing, remind players to enable only one copy.>

## Compatibility and known issues

- Game: <supported game version or patch>
- Dalamud: <supported version or API level>
- Tested in game: <what was tested, or "No in-game testing for this build.">
- Known issues: <anything unavailable, including features that still need Gillions support>
```
