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
        backoff.Refused(t0, "explicit:g:True");
        Check(backoff.Waiting(t0.AddSeconds(15)) && backoff.Waiting(t0.AddMinutes(4).AddSeconds(59)), "no retry on the next presence acknowledgements");
        Check(!backoff.Waiting(t0.AddMinutes(5)), "retries after five minutes");
        // 15-second presence cadence over an hour with every upload refused: 12 attempts instead of 240.
        var attempts = 0; var b = new TravelRefusalBackoff();
        for (var s = 0; s < 3600; s += 15) { var now = t0.AddSeconds(s); if (!b.Waiting(now)) { attempts++; b.Refused(now, "explicit:g:True"); } }
        Check(attempts == 12, "an hour of refusals makes 12 attempts (" + attempts + ")");
        backoff.Refused(t0, "explicit:g:True"); backoff.Reset();
        Check(!backoff.Waiting(t0.AddSeconds(1)), "a character, login or permission change retries at once");
        // A failed presence lapses the lease (known decision unchanged); only a real decision change clears the wait.
        var lease = new TravelRefusalBackoff();
        lease.Refused(t0, "explicit:g:True");
        lease.DecisionChanged(null); lease.DecisionChanged("explicit:g:True");
        Check(lease.Waiting(t0.AddSeconds(40)), "a lease lapse and recovery with the same decision keeps the wait");
        lease.DecisionChanged("explicit:g2:True");
        Check(!lease.Waiting(t0.AddSeconds(41)), "a new travel decision on Gillions retries at once");
        Check(TravelRefusalBackoff.Counts(400) && TravelRefusalBackoff.Counts(403) && TravelRefusalBackoff.Counts(409)
            && !TravelRefusalBackoff.Counts(408) && !TravelRefusalBackoff.Counts(425) && !TravelRefusalBackoff.Counts(307) && !TravelRefusalBackoff.Counts(308),
            "timeouts and redirects keep the short retry");
        var clock = new TravelRefusalBackoff();
        clock.Refused(t0.AddHours(1), null);
        Check(!clock.Waiting(t0), "a wall clock moved backwards does not extend the wait");
        Console.WriteLine($"Travel refusal back-off (5 minutes, reset on session/permission change) PASS: {checks}");
    }
}
