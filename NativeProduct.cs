using System;

namespace GillionsGameSync;

// Identity is selected by compilation, never by a server-provided version or
// a local configuration switch. Policy-only fixtures retain the Testing identity.
internal static class NativeProduct {
    internal static bool RequestOrigin(string? origin) => TransportOrigin(origin);

    internal static bool CanDispatch(string origin, Uri? destination) =>
        RequestOrigin(origin) && destination is { IsAbsoluteUri: true, UserInfo.Length: 0, Fragment.Length: 0 }
        && destination.GetLeftPart(UriPartial.Authority) == origin;
#if GILLIONS_PUBLIC_BUILD
    internal const string Name = "GillionsGameSync";
#else
    internal const string Name = "GillionsGameSyncTest";
#endif
    // Transports use only these exact origins. The captured pairing,
    // never this allow-list, selects the request destination and credential.
    internal static bool TransportOrigin(string? origin) => origin == "https://test.gillions.app"
#if GILLIONS_PUBLIC_BUILD
        || origin == "https://gillions.app"
#endif
        ;
}
