using GillionsGameSync;

// An unlinked character's ordinary uploads wait five minutes after a refusal
// instead of retrying every 10 s; a character change or proven link clears it.
internal static class CharacterRefusalBackoffTests {
    internal static void Run() {
        var checks = 0;
        void Check(bool ok, string why) { checks++; if (!ok) throw new Exception("Character refusal back-off: " + why); }
        var t0 = new DateTime(2026, 10, 25, 12, 28, 42, DateTimeKind.Utc);
        Check(CharacterRefusalBackoff.Counts(SiteRefusalPolicy.CharacterNotLinked), "not linked holds ordinary uploads back");
        Check(!CharacterRefusalBackoff.Counts(SiteRefusalPolicy.CharacterMismatch) && !CharacterRefusalBackoff.Counts(SiteRefusalPolicy.Rejected)
            && !CharacterRefusalBackoff.Counts(SiteRefusalPolicy.ProductMismatch) && !CharacterRefusalBackoff.Counts("DEVICE_INVALID"),
            "other refusals keep their own handling");
        var backoff = new CharacterRefusalBackoff();
        Check(!backoff.Waiting(t0), "nothing refused, nothing held back");
        backoff.Refused(t0);
        Check(backoff.Waiting(t0.AddSeconds(10)) && backoff.Waiting(t0.AddMinutes(4).AddSeconds(59)), "no 10-second inventory retry");
        Check(!backoff.Waiting(t0.AddMinutes(5)), "retries after five minutes");
        // Observed 2026-10-25: 141 refused uploads in about 17 minutes. With the wait, one per five minutes.
        var attempts = 0; var b = new CharacterRefusalBackoff();
        for (var s = 0; s < 3600; s += 10) { var now = t0.AddSeconds(s); if (!b.Waiting(now)) { attempts++; b.Refused(now); } }
        Check(attempts == 12, "an hour of refusals makes 12 attempts (" + attempts + ")");
        backoff.Refused(t0); backoff.Reset();
        Check(!backoff.Waiting(t0.AddSeconds(1)), "a character change, login or accepted presence retries at once");
        var clock = new CharacterRefusalBackoff();
        clock.Refused(t0.AddHours(1));
        Check(!clock.Waiting(t0), "a wall clock moved backwards does not extend the wait");
        Console.WriteLine($"Character refusal back-off (5 minutes, reset on character change or proven link) PASS: {checks}");
    }
}
