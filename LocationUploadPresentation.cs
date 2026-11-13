using System;

namespace GillionsGameSync;

internal static class LocationUploadPresentation {
    internal const string Sent = "Location: sent";
    internal const string BuildRejected = "Location: not accepted by the site (game version)";
    internal const string Off = "Location: sharing off";

    internal static string Label(bool? sharing, string outcome) =>
        sharing == true ? outcome : sharing == false ? Off : "";

    internal static string Refusal(Exception error) =>
        error is GillionsHttpException { TravelBuildRejected: true } ? BuildRejected : "";
}
