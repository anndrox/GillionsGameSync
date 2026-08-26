namespace GillionsGameSync;

internal sealed class PostPairHydrationState {
    public const string CharacterResource = "character";

    public bool CharacterSyncPending { get; private set; }

    public void PairingSucceeded() => CharacterSyncPending = true;

    public bool TryBeginCharacterSync(bool syncInFlight) {
        if (!CharacterSyncPending || syncInFlight) return false;
        CharacterSyncPending = false;
        return true;
    }
}
