#if GILLIONS_TEST_BUILD || GILLIONS_HUNT_MAP_TESTS
using System;
using System.Diagnostics;

namespace GillionsGameSync;

// Request-scoped only: issuer UTC plus monotonic elapsed time. The caller may
// use Date only from its authenticated, non-redirected exact TEST HTTPS reply.
// Date has second precision; use its upper bound plus the entire header RTT so
// remaining lifetime is conservative, never extended by transit or PC skew.
internal sealed class WebsiteResponseClock {
    private readonly DateTime reference;
    private readonly long received = Stopwatch.GetTimestamp();
    internal bool IssuerTime { get; }
    private WebsiteResponseClock(DateTime value, bool issuer) { reference = value; IssuerTime = issuer; }
    internal DateTime UtcNow => reference.Add(Stopwatch.GetElapsedTime(received));
    internal bool Unexpired(DateTime expiresAtUtc) => UtcNow < expiresAtUtc;
    internal static WebsiteResponseClock Capture(DateTimeOffset? date, DateTime localNow, TimeSpan headerRoundTrip) {
        if (localNow.Kind != DateTimeKind.Utc) throw new InvalidOperationException("UTC reference required.");
        // Missing Date preserves the old strict local-clock policy for V1;
        // it is not a TTL tolerance or an insecure transport fallback.
        if (date is null) return new(localNow, false);
        if ((date.Value.UtcDateTime-localNow).Duration()>TimeSpan.FromSeconds(30)
            || headerRoundTrip<TimeSpan.Zero || headerRoundTrip>TimeSpan.FromSeconds(30))
            throw new InvalidOperationException("Issuer clock outside bounded response window.");
        return new(date.Value.UtcDateTime.AddSeconds(1).Add(headerRoundTrip), true);
    }
}
#endif
