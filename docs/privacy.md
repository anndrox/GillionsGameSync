# Privacy

Gillions Game Sync is opt-in and account-linked. It sends only the synchronization categories enabled by the player to the configured Gillions HTTPS origin.

The plugin does not send Square Enix credentials, chat text, unrelated account data, arbitrary local files, or diagnostic recordings automatically. It has no inbound listener and performs no packet capture or gameplay automation.

Pairing uses a one-time code to obtain a device credential. That credential is stored in Dalamud's plugin configuration and must never be committed, logged, or shared. Diagnostic recording is disabled by default, remains local, is bounded, and requires the user to copy it manually for support.

Some game state is available only while its interface or container is authoritatively loaded. In those cases the plugin preserves prior positive state or omits the field rather than treating unavailable state as an intentional deletion.
