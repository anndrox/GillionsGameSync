using GillionsGameSync;

// A refused travel upload waits five minutes instead of retrying on every
// 15-second presence acknowledgement; a session or permission change clears it.
internal static class TravelRefusalBackoffTests {
    internal static void Run() {
        var checks = 0;
        void Check(bool ok, string why) { checks++; if (!ok) throw new Exception("Travel refusal back-off: " + why); }
        var t0 = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
        var backoff = new TravelRefusalBackoff();
        Check(!backoff.Waiting(t0), "nothing refused, nothing held back");
        backoff.Refused(t0);
        Check(backoff.Waiting(t0.AddSeconds(15)) && backoff.Waiting(t0.AddMinutes(4).AddSeconds(59)), "no retry on the next presence acknowledgements");
        Check(!backoff.Waiting(t0.AddMinutes(5)), "retries after five minutes");
        // 15-second presence cadence over an hour with every upload refused: 12 attempts instead of 240.
        var attempts = 0; var b = new TravelRefusalBackoff();
        for (var s = 0; s < 3600; s += 15) { var now = t0.AddSeconds(s); if (!b.Waiting(now)) { attempts++; b.Refused(now); } }
        Check(attempts == 12, "an hour of refusals makes 12 attempts (" + attempts + ")");
        backoff.Refused(t0); backoff.Reset();
        Check(!backoff.Waiting(t0.AddSeconds(1)), "a character, login or permission change retries at once");
        var clock = new TravelRefusalBackoff();
        clock.Refused(t0.AddHours(1));
        Check(!clock.Waiting(t0), "a wall clock moved backwards does not extend the wait");
        Console.WriteLine($"Travel refusal back-off (5 minutes, reset on session/permission change) PASS: {checks}");
    }
}
