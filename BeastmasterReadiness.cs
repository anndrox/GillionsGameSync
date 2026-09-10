#if GILLIONS_BEASTMASTER_DIAGNOSTIC || GILLIONS_POLICY_TESTS
namespace GillionsGameSync;

// A loaded native list has no owner identifier. Admit ownership only after an
// unloaded/requested state has been observed for this same logged-in character.
internal sealed class BeastmasterReadiness {
    private ulong character;
    private bool sawLoading;
    public void Reset() { character = 0; sawLoading = false; }
    public bool Observe(ulong currentCharacter, bool loading, bool received) {
        if (currentCharacter == 0) { Reset(); return false; }
        if (character != currentCharacter) { Reset(); character = currentCharacter; }
        if (loading) sawLoading = true;
        else if (!received) sawLoading = false;
        return received && sawLoading;
    }
}
#endif