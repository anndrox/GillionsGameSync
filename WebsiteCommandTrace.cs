#if GILLIONS_TEST_BUILD || GILLIONS_PUBLIC_BUILD || GILLIONS_HUNT_MAP_TESTS
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
    private double consumeDispatchMs, consumeHeadersMs, consumeAckMs;
    internal void PollDispatched() { dispatchedUtc = DateTime.UtcNow; dispatchMs = Elapsed(); }
    internal void Claimed() => claimMs = Elapsed();
    internal void ConsumeStarted() => consumeStartMs = Elapsed();
    internal void ConsumeFinished() => consumeEndMs = Elapsed();
    internal void ConsumeDispatched() => consumeDispatchMs = Elapsed();
    internal void ConsumeHeadersReceived() => consumeHeadersMs = Elapsed();
    internal void ConsumeAcknowledged() => consumeAckMs = Elapsed();
    internal void MapStarted() => mapStartMs = Elapsed();
    internal void MapFinished() => mapEndMs = Elapsed();
    private double Elapsed() => Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    internal string PollSummary() => string.Create(CultureInfo.InvariantCulture,
        $"attempt UTC {StartedUtc:yyyy-MM-ddTHH:mm:ss.fffZ}; dispatched={(dispatchedUtc != default)}; elapsed {Elapsed():F2} ms");
    internal string Describe() => string.Create(CultureInfo.InvariantCulture,
        $"poll UTC {dispatchedUtc:yyyy-MM-ddTHH:mm:ss.fffZ}; pre-dispatch {dispatchMs:F2} ms; poll/claim {claimMs-dispatchMs:F2} ms; claim-to-consume-callback {consumeStartMs-claimMs:F2} ms; consume callback (catalog/framework/HTTP) {consumeEndMs-consumeStartMs:F2} ms; claim-to-consume-dispatch {consumeDispatchMs-claimMs:F2} ms; consume HTTP headers {consumeHeadersMs-consumeDispatchMs:F2} ms; consume headers-to-ack {consumeAckMs-consumeHeadersMs:F2} ms; consume-ack-to-map {mapStartMs-consumeAckMs:F2} ms; claim-to-map {mapStartMs-claimMs:F2} ms; map call {mapEndMs-mapStartMs:F2} ms");
}
#endif
