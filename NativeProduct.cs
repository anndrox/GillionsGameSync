namespace GillionsGameSync;

// Identity is selected by compilation, never by a server-provided version or
// a local configuration switch. Policy-only fixtures retain the Testing identity.
internal static class NativeProduct {
    // The public Testing distribution may retain historical configuration, but
    // never dispatches ordinary hydration, pairing or commands to production.
    internal static bool RequestOrigin(string? origin) =>
#if GILLIONS_TEST_BUILD
        TransportOrigin(origin);
#else
        true; // Public retains its established ordinary SyncOrigin/permit gates.
#endif
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
