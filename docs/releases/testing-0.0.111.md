# Gillions Game Sync 0.0.111 (Testing)

## What's new

- Hunt coverage checks the exact Site item mapping and leaves time for Site to validate each sample. Travel sends its observation time at Site's millisecond precision.
- Uploads can recover when Site reports a temporary failure reading a complete JSON body. FATE retries keep the same batch and expire with the original observation.
- Game Sync sends only to its paired Gillions origin. Disconnect asks Site to revoke this device and explains when it could only clear the local pairing.

## Updating

- Existing device credentials migrate to Windows per-user protection. Use only one Game Sync channel at a time.
- If you roll back after migration, create a fresh pairing on the channel you roll back to.

## Compatibility and known issues

- Game build `2026.11.03.0000.0000`, Dalamud `15.0.4.1` (API 15).
- In-game acceptance and Site admission for this version are pending.
