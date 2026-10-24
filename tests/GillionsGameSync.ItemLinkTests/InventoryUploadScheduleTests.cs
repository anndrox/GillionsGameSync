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

        // A fast burst (every 500 ms) never settles, so only the 10 s cap uploads it.
        var fast = new InventoryUploadSchedule(); var fastUploads = new List<DateTime>();
        var fastFirstUnsent = DateTime.MaxValue; var fastWorst = TimeSpan.Zero;
        for (var ms = 0; ms <= 40000; ms += 100) {
            var now = t0.AddMilliseconds(ms);
            if (ms <= 30000 && ms % 500 == 0) { fast.Changed(now, settle); if (fastFirstUnsent == DateTime.MaxValue) fastFirstUnsent = now; }
            if (fast.TryDispatch(now)) {
                fastUploads.Add(now);
                if (now - fastFirstUnsent > fastWorst) fastWorst = now - fastFirstUnsent;
                fastFirstUnsent = DateTime.MaxValue;
            }
        }
        var fastTrace = string.Join(",", fastUploads.Select(u => (u - t0).TotalSeconds));
        Check(fastUploads[0] == t0.AddSeconds(10), "a change that never settles still uploads at the 10 s cap (" + fastTrace + ")");
        Check(fastWorst == InventoryUploadSchedule.Window && fastUploads.Zip(fastUploads.Skip(1)).All(pair => pair.Second - pair.First >= InventoryUploadSchedule.Window),
            "the cap counts from the first unsent change; uploads stay at least 10 s apart (" + fastTrace + ")");
        Check(fastUploads.Count == 3 && fastUploads[^1] <= t0.AddSeconds(40), "61 changes in 30 s become 3 uploads, the last covering the final change (" + fastTrace + ")");

        // A retainer-listing change (250 ms settle) after an inventory change does not wait the longer settle.
        var listing = new InventoryUploadSchedule();
        listing.Changed(t0, settle); listing.Changed(t0.AddMilliseconds(100), TimeSpan.FromMilliseconds(250));
        Check(listing.Due == t0.AddMilliseconds(350), "the latest change's settle applies");

        // A dispatched upload that could not run is retried rather than waiting for the next event.
        var requeued = new InventoryUploadSchedule();
        requeued.Changed(t0, settle); Check(requeued.TryDispatch(t0 + settle), "dispatched");
        requeued.Requeue(t0.AddSeconds(2), TimeSpan.FromSeconds(2));
        Check(!requeued.TryDispatch(t0.AddSeconds(3)) && requeued.TryDispatch(t0.AddSeconds(4)), "a loading-screen refusal retries after 2 s");
        requeued.Requeue(t0.AddSeconds(5), TimeSpan.FromSeconds(10));
        requeued.Changed(t0.AddSeconds(6), settle);
        Check(requeued.Due <= t0.AddSeconds(15), "a failure retry is not pushed later by a new change");

        // A wall clock moved backwards does not stall uploads.
        var clock = new InventoryUploadSchedule();
        clock.Changed(t0.AddHours(1), settle); clock.TryDispatch(t0.AddHours(1) + settle);
        clock.Changed(t0, settle);
        Check(clock.Due == t0 + settle, "after the clock jumps back an hour, a change uploads after the normal settle");

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
