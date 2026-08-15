using System;
using System.Collections.Generic;
using System.Linq;

namespace GillionsGameSync;

public sealed record RetainerClientProfile(
    string ProductName,
    string Channel,
    bool RequiresExplicitServerProductAcceptance);

public static class RetainerClientPolicy {
    public const int ContractVersion = 1;
    public const string ResourceType = "retainer_ventures";

    public static readonly RetainerClientProfile Stable = new(
        "GillionsGameSync",
        "stable",
        true);

    public static readonly RetainerClientProfile Testing = new(
        "GillionsGameSyncTest",
        "testing",
        false);

    public static string[] BuildSyncScopes(IEnumerable<string> ordinaryScopes, bool serverAccepted) {
        var scopes = ordinaryScopes.ToArray();
        return serverAccepted ? scopes.Append(ResourceType).ToArray() : scopes;
    }

    public static bool ShouldPollPlans(
        bool serverAccepted,
        bool explicitOptIn,
        bool autoRetainerLoaded,
        bool autoRetainerApiReady,
        bool paired) => serverAccepted
            && explicitOptIn
            && autoRetainerLoaded
            && autoRetainerApiReady
            && paired;
}
