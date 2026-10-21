using System;

namespace GillionsGameSync;

internal sealed class PairedClientHydrationState {
    public const string CharacterResource = "character";

    public bool CharacterSyncPending { get; private set; }
    public bool PresencePending { get; private set; }

    public void PluginStarted(bool hasDeviceCredential) {
        if (hasDeviceCredential) Schedule();
    }

    public void PairingSucceeded() => Schedule();

    private DateTime characterSyncNotBeforeUtc = DateTime.MinValue;

    public bool TryBeginCharacterSync(bool syncInFlight) => TryBeginCharacterSync(syncInFlight, DateTime.MaxValue);
    public bool TryBeginCharacterSync(bool syncInFlight, DateTime nowUtc) {
        if (!CharacterSyncPending || syncInFlight || nowUtc < characterSyncNotBeforeUtc) return false;
        CharacterSyncPending = false;
        return true;
    }

    // The hydration character sync could not run (not ready, loading screen,
    // character withheld): keep it pending, without retrying every frame.
    public void RetryCharacterSync(DateTime notBeforeUtc) {
        CharacterSyncPending = true;
        characterSyncNotBeforeUtc = notBeforeUtc;
    }

    public bool TryBeginPresence(bool presenceInFlight) {
        if (!PresencePending || presenceInFlight) return false;
        PresencePending = false;
        return true;
    }

    private void Schedule() {
        CharacterSyncPending = true;
        PresencePending = true;
    }
}
