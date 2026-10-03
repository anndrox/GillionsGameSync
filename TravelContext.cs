#if GILLIONS_TEST_BUILD || GILLIONS_TRAVEL_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace GillionsGameSync;

// Deliberately not a configuration/persistence type. Only the current session's
// latest observation lives in RAM; nothing is appended to personal sync history.
internal sealed record TravelDestination(uint AetheryteId, uint TerritoryId,
    string Attunement, string Usability, uint? ObservedListGil, uint? ActualCostGil,
    bool? Home, bool? Free, bool? Favored);
internal sealed record TravelObservation(uint TerritoryId, uint MapId, double MapX, double MapY,
    DateTime ObservedAtUtc, string GameBuild, string NativeVersion, string CollectorVersion,
    string TeleportSupport, TravelDestination[] Destinations) {
    public int SchemaVersion => 1;
    public string CollectorSchema => "travel-context-v1";
    public string LocationSource => "dalamud-local-player-maputil";
    public string TeleportSource => "naturally-visible-teleport-owned-cache";
    public string CostSupport => "UNSUPPORTED_FINAL_CHARGE";
    public string UploadState => "local-only-no-server-contract";
}
internal static class TravelPolicy {
    internal const int MaximumDestinations = 256;
    internal const int MaximumBytes = 65536;
    internal const int MaximumDepth = 8;
    internal const int TtlSeconds = 45;
    internal const int CadenceSeconds = 15;
    internal static double Round(double coordinate) => Math.Round(coordinate, 1, MidpointRounding.AwayFromZero);
    internal static bool Coordinate(double value) => double.IsFinite(value) && value is > 0 and <= 100 && Round(value) == value;
    internal static bool Valid(TravelObservation? value) => value is not null
        && value.TerritoryId is > 0 and <= 65535 && value.MapId is > 0 and <= 65535
        && Coordinate(value.MapX) && Coordinate(value.MapY)
        && PersonalObservationCompatibility.Utc(value.ObservedAtUtc)
        && PersonalObservationCompatibility.Supports(value.GameBuild,value.NativeVersion)
        && PersonalObservationCompatibility.Metadata(value.CollectorVersion)
        && value.TeleportSupport is "OBSERVED_PARTIAL" or "UNAVAILABLE"
        && value.Destinations is not null && value.Destinations.Length <= MaximumDestinations
        && (value.TeleportSupport == "OBSERVED_PARTIAL" ? value.Destinations.Length > 0 : value.Destinations.Length == 0)
        && value.Destinations.All(d => d is not null && d.AetheryteId is > 0 and <= 65535 && d.TerritoryId is > 0 and <= 65535
            && d.Attunement == "OBSERVED_IN_PERSONAL_LIST" && d.Usability == "UNKNOWN"
            && d.ActualCostGil is null && d.ObservedListGil is <= 99999)
        && value.Destinations.Select(d => d.AetheryteId).Distinct().Count() == value.Destinations.Length
        && JsonSerializer.SerializeToUtf8Bytes(value, PersonalObservationCompatibility.Json).Length <= MaximumBytes;
    internal static bool Fresh(TravelObservation? value, DateTime now) => Valid(value)
        && PersonalObservationCompatibility.Utc(now) && value!.ObservedAtUtc <= now
        && now - value.ObservedAtUtc < TimeSpan.FromSeconds(TtlSeconds);
}
internal sealed class TravelContextState {
    private ulong owner;
    private TravelObservation? latest;
    private DateTime nextReadUtc;
    internal bool Enabled { get; private set; }
    internal void SetEnabled(bool enabled) { if (enabled == Enabled) return; Clear(); Enabled = enabled; }
    internal void Clear() { owner = 0; latest = null; nextReadUtc = DateTime.MinValue; }
    internal void Invalidate() { latest = null; } // keep cadence bounded across rapid events
    internal bool Begin(DateTime now) {
        if (!Enabled || !PersonalObservationCompatibility.Utc(now) || now < nextReadUtc) return false;
        nextReadUtc = now.AddSeconds(TravelPolicy.CadenceSeconds); return true;
    }
    internal bool Observe(ulong character, TravelObservation value, DateTime now) {
        if (!Enabled || character == 0 || !TravelPolicy.Fresh(value,now)) return false;
        if (owner != character) { owner = character; latest = null; }
        if (latest is not null && value.ObservedAtUtc <= latest.ObservedAtUtc) return false;
        latest = value; return true;
    }
    internal TravelObservation? Current(ulong character, DateTime now) {
        if (!Enabled || character == 0 || owner != character || !TravelPolicy.Fresh(latest,now)) { latest = null; return null; }
        return latest;
    }
    // No server-approved travel capability exists. Even enabled local consent
    // cannot authorize a guessed contract or a network send.
    internal static bool TransportActivated => false;
}
#endif
