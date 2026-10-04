#if GILLIONS_TEST_BUILD || GILLIONS_HUNT_MAP_TESTS
using System;
using System.Diagnostics;
using System.Globalization;

namespace GillionsGameSync;

// One command's numeric durations, session RAM only. No token, payload, identity
// or location custody; completed output uses the existing capped diagnostics.
internal sealed class WebsiteCommandTrace {
    private readonly long started = Stopwatch.GetTimestamp();
    internal DateTime StartedUtc { get; } = DateTime.UtcNow;
    private DateTime dispatchedUtc;
    private double dispatchMs;
    private double claimMs, consumeStartMs, consumeEndMs, mapStartMs, mapEndMs;
    internal void PollDispatched() { dispatchedUtc = DateTime.UtcNow; dispatchMs = Elapsed(); }
    internal void Claimed() => claimMs = Elapsed();
    internal void ConsumeStarted() => consumeStartMs = Elapsed();
    internal void ConsumeFinished() => consumeEndMs = Elapsed();
    internal void MapStarted() => mapStartMs = Elapsed();
    internal void MapFinished() => mapEndMs = Elapsed();
    private double Elapsed() => Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    internal string PollSummary() => string.Create(CultureInfo.InvariantCulture,
        $"attempt UTC {StartedUtc:yyyy-MM-ddTHH:mm:ss.fffZ}; dispatched={(dispatchedUtc != default)}; elapsed {Elapsed():F2} ms");
    internal string Describe() => string.Create(CultureInfo.InvariantCulture,
        $"poll UTC {dispatchedUtc:yyyy-MM-ddTHH:mm:ss.fffZ}; pre-dispatch {dispatchMs:F2} ms; poll/claim {claimMs-dispatchMs:F2} ms; claim-to-consume-dispatch {consumeStartMs-claimMs:F2} ms; consume {consumeEndMs-consumeStartMs:F2} ms; consume-to-map {mapStartMs-consumeEndMs:F2} ms; claim-to-map {mapStartMs-claimMs:F2} ms; map call {mapEndMs-mapStartMs:F2} ms");
}
#endif
