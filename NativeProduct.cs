namespace GillionsGameSync;

// Identity is selected by compilation, never by a server-provided version or
// a local configuration switch. Policy-only fixtures retain the Testing identity.
internal static class NativeProduct {
#if GILLIONS_PUBLIC_BUILD
    internal const string Name = "GillionsGameSync";
#else
    internal const string Name = "GillionsGameSyncTest";
#endif
    // Optional transports use only these exact origins. The captured pairing,
    // never this allow-list, selects the request destination and credential.
    internal static bool TransportOrigin(string? origin) => origin == "https://test.gillions.app"
#if GILLIONS_PUBLIC_BUILD
        || origin == "https://gillions.app"
#endif
        ;
}
