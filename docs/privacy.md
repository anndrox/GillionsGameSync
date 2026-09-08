# Privacy

Gillions Game Sync is opt-in and account-linked. It sends only the synchronization categories enabled by the player to the configured Gillions HTTPS origin.

The plugin does not send Square Enix credentials, chat text, unrelated account data, arbitrary local files, or diagnostic recordings automatically. It has no inbound listener and performs no packet capture or gameplay automation.

The unreleased source candidate removes all AutoRetainer discovery, read/write IPC, plan polling and apply/restore paths. Retained native observations and venture-result uploads use the existing account, product and capability boundaries. A neutral legacy presence object remains for older-server compatibility; it does not inspect installed plugins or report planner readiness.

Existing plan backups and ownership records remain inert in the local configuration, including unknown legacy fields. They are not interpreted as commands, included in presence, exported or automatically uploaded. The candidate adds no recovery feature. Existing AutoRetainer-derived stats and start times are historical and receive no new observation timestamps from retirement. This source change does not retroactively alter the published stable `1.0.29` or testing packages described in the changelog.

Pairing uses a one-time code to obtain a device credential. That credential is stored in Dalamud's plugin configuration and must never be committed, logged, or shared. Diagnostic recording is disabled by default, remains local, is bounded, and requires the user to copy it manually for support.

Some game state is available only while its interface or container is authoritatively loaded. In those cases the plugin preserves prior positive state or omits the field rather than treating unavailable state as an intentional deletion.
