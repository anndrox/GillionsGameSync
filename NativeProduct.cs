namespace GillionsGameSync;

// Identity is selected by compilation, never by a server-provided version or
// a local configuration switch. Policy-only fixtures retain the Testing identity.
internal static class NativeProduct {
#if GILLIONS_PUBLIC_BUILD
    internal const string Name = "GillionsGameSync";
#else
    internal const string Name = "GillionsGameSyncTest";
#endif
}
