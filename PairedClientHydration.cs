namespace GillionsGameSync;

internal sealed class PairedClientHydrationState {
    public const string CharacterResource = "character";

    public bool CharacterSyncPending { get; private set; }
    public bool PresencePending { get; private set; }

    public void PluginStarted(bool hasDeviceCredential) {
        if (hasDeviceCredential) Schedule();
    }

    public void PairingSucceeded() => Schedule();

    public bool TryBeginCharacterSync(bool syncInFlight) {
        if (!CharacterSyncPending || syncInFlight) return false;
        CharacterSyncPending = false;
        return true;
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
