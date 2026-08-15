# Privacy

Gillions Game Sync is opt-in and account-linked. It sends only the synchronization categories enabled by the player to the configured Gillions HTTPS origin.

The plugin does not send Square Enix credentials, chat text, unrelated account data, arbitrary local files, or diagnostic recordings automatically. It has no inbound listener and performs no packet capture or gameplay automation.

Published stable `1.0.26` and testing `0.0.59` can modify only the opted-in character's AutoRetainer embedded Venture Plan through AutoRetainer's public IPC after an authenticated Gillions request. Stable planner access remains disabled by default and cannot activate until Gillions explicitly accepts the stable product and contract. The original plan and completion behavior are retained locally and on Gillions for verified restoration. A deliberate conflict retry is accepted only when current AutoRetainer state exactly matches the hash previously acknowledged to Gillions; another local change blocks the write. No AutoRetainer configuration file is read or written directly, and unrelated AutoRetainer settings are excluded from the backup and mutation.

Pairing uses a one-time code to obtain a device credential. That credential is stored in Dalamud's plugin configuration and must never be committed, logged, or shared. Diagnostic recording is disabled by default, remains local, is bounded, and requires the user to copy it manually for support.

Some game state is available only while its interface or container is authoritatively loaded. In those cases the plugin preserves prior positive state or omits the field rather than treating unavailable state as an intentional deletion.
