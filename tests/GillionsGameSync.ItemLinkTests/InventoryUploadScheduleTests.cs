using GillionsGameSync;

// Synthetic event timing: inventory bursts while gathering are coalesced to at
// most one upload per 10 s, and no change waits longer than 10 s to upload.
internal static class InventoryUploadScheduleTests {
    internal static void Run() {
        var checks = 0;
        void Check(bool ok, string why) { checks++; if (!ok) throw new Exception("Inventory upload schedule: " + why); }
        var settle = TimeSpan.FromMilliseconds(1500);
        var t0 = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

        var single = new InventoryUploadSchedule();
        Check(!single.TryDispatch(t0), "nothing pending uploads nothing");
        single.Changed(t0, settle);
        Check(single.Due == t0 + settle && !single.TryDispatch(t0.AddSeconds(1)) && single.TryDispatch(t0 + settle), "an isolated change still uploads after the 1.5 s settle");
        Check(!single.TryDispatch(t0.AddSeconds(5)), "one change, one upload");

        // Gathering: a change every 2 s for 60 s, checked on 100 ms framework ticks.
        var burst = new InventoryUploadSchedule();
        var uploads = new List<DateTime>(); var firstUnsent = DateTime.MaxValue; var worstDelay = TimeSpan.Zero;
        for (var ms = 0; ms <= 75000; ms += 100) {
            var now = t0.AddMilliseconds(ms);
            if (ms <= 60000 && ms % 2000 == 0) { burst.Changed(now, settle); if (firstUnsent == DateTime.MaxValue) firstUnsent = now; }
            if (burst.TryDispatch(now)) {
                uploads.Add(now);
                if (now - firstUnsent > worstDelay) worstDelay = now - firstUnsent;
                firstUnsent = DateTime.MaxValue;
            }
        }
        Check(uploads.Zip(uploads.Skip(1)).All(pair => pair.Second - pair.First >= InventoryUploadSchedule.Window), "at most one upload per 10 s window");
        Check(worstDelay <= InventoryUploadSchedule.Window + TimeSpan.FromMilliseconds(100), "no change waits longer than the window (" + worstDelay + ")");
        Check(uploads.Count is >= 6 and <= 8 && uploads[^1] >= t0.AddSeconds(60), "31 changes in 60 s become about 7 uploads, the last one covering the final change (" + uploads.Count + ")");

        // The old pure debounce never uploaded until the burst stopped.
        var legacyDue = DateTime.MaxValue; var legacyFirst = DateTime.MaxValue;
        for (var ms = 0; ms <= 60000; ms += 100) {
            var now = t0.AddMilliseconds(ms);
            if (ms % 2000 == 0) legacyDue = now + settle;
            if (now >= legacyDue) { legacyFirst = now; break; }
        }
        Check(legacyFirst == t0.AddMilliseconds(1500), "fixture matches the previous behaviour: each 2 s change uploaded after its own 1.5 s settle");

        // After a quiet period, a new change is not delayed by the old window.
        var quiet = new InventoryUploadSchedule();
        quiet.Changed(t0, settle); quiet.TryDispatch(t0 + settle);
        quiet.Changed(t0.AddSeconds(30), settle);
        Check(quiet.Due == t0.AddSeconds(30) + settle, "a change long after the last upload uploads after the normal settle");
        quiet.Reset();
        Check(quiet.Due == DateTime.MaxValue && !quiet.TryDispatch(t0.AddMinutes(5)), "a session reset drops pending uploads");
        Console.WriteLine($"Inventory upload coalescing (10 s window, 10 s worst delay) PASS: {checks}");
    }
}
