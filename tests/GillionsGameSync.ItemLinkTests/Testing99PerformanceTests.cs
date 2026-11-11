using System.Net;
using System.Net.Http.Headers;
using GillionsGameSync;

internal static class Testing99PerformanceTests {
    internal static async Task Run() {
        int checks = 0;
        void Check(bool condition, string context) { checks++; if (!condition) throw new Exception(context); }

        var writer = new OrderedConfigurationWriter();
        var enter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var actions = new List<string>();
        var first = writer.Enqueue(() => {
            enter.SetResult();
            release.Task.GetAwaiter().GetResult();
            actions.Add("save pending evidence");
        });
        await enter.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var denial = writer.Enqueue(() => actions.Add("save denial"));
        Check(!first.IsCompleted && !denial.IsCompleted, "worker does not block tick or reorder denial");
        release.SetResult();
        await first;
        await denial;
        actions.Add("send after durable checkpoint");
        Check(actions.SequenceEqual(new[] { "save pending evidence", "save denial", "send after durable checkpoint" }), "durable ordering");
        var failed = writer.Enqueue(() => throw new IOException());
        var recovered = writer.Enqueue(() => actions.Add("save recovered state"));
        try { await failed; throw new Exception("failed checkpoint reported success"); }
        catch (IOException) { }
        await recovered;
        Check(actions[^1] == "save recovered state", "failed checkpoint leaves worker usable");

        const string synthetic = "fixture_only_12345678901234567890";
        const string device = "00000000-0000-4000-8000-000000000001";
        var session = PairedSession.Create("https://test.gillions.app", device, synthetic);
        var cache = new PairingValidationCache();
        var validations = 0;
        bool Allowed(string? origin) { validations++; return origin == "https://test.gillions.app"; }
        for (int frame = 0; frame < 600; frame++)
            Check(cache.IsBound(session, false, device, synthetic, Allowed), "cached bound session");
        Check(validations == 1, "one validation for warm pairing");
        Check(!cache.IsBound(session, true, device, synthetic, Allowed), "revocation invalidates cache");
        Check(cache.IsBound(session with { Generation = Guid.NewGuid().ToString("N") }, false, device, synthetic, Allowed)
            && validations == 2, "new enrollment validates again");

        var budget = new NativeRowBudget();
        var reads = 0;
        budget.NextTick();
        try {
            budget.Read("catalog", 600, i => { reads++; return (true, i); });
            throw new Exception("scan completed in one tick");
        } catch (NativeScanPendingException) { }
        Check(reads == NativeRowBudget.RowsPerTick, "bounded row count");
        budget.NextTick();
        try { budget.Read("catalog", 600, i => { reads++; return (true, i); }); }
        catch (NativeScanPendingException) { }
        budget.NextTick();
        Check(budget.Read("catalog", 600, i => { reads++; return (true, i); }).SequenceEqual(Enumerable.Range(0, 600))
            && reads == 600, "publishes only full replacement and visits each row once");
        try {
            budget.RequireShape("catalog", 601);
            throw new Exception("source change accepted");
        } catch (NativeScanChangedException) { }

        var view = new RetainerResultViewCache();
        var native = new RetainerResultValues("1", 2, 3, 4, 5, 6, 0, 0);
        Check(view.Changed("character", native) && !view.Changed("character", native), "identical native rewards skip materialization");
        Check(view.Changed("character", native with { FirstQuantity = 7 }), "changed reward");
        view.Clear();
        Check(view.Changed("character", native), "view reopen");
        var items = Enumerable.Range(0, 300).ToDictionary(i => i, _ => DateTime.UnixEpoch);
        TransientEvidencePolicy.Prune(items, DateTime.UnixEpoch, TimeSpan.FromSeconds(10), stamp => stamp);
        Check(items.Count == TransientEvidencePolicy.MaximumEntries, "retainer evidence cap");

        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) {
            Content = new StringContent("""{"ok":false,"code":"RATE_LIMITED","error":"Wait","retryable":true}""")
        };
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(60));
        var error = await SyncResponsePolicy.ReadFailureAsync(response, default);
        Check(error.Status == 429 && error.Code == "RATE_LIMITED" && error.RetryAfter == TimeSpan.FromSeconds(60), "preserve response status, code and delay");
        var retry = new ResourceRetry();
        retry.Failed(100, "same", error);
        Check(!retry.Ready(60_099, "changed") && retry.Ready(60_100, "same"), "server minimum survives a changed payload");
        retry.Failed(60_100, "same", new GillionsHttpException("UNSUPPORTED_VERSION", "Refused", 409, null));
        Check(!retry.Ready(360_099, "same") && retry.Ready(60_101, "changed")
            && retry.Ready(360_100, "same"), "unchanged refusal parks until a slow probe");
        retry.Clear();
        retry.Failed(400_000, "same", new IOException());
        Check(retry.Remaining(400_000, "same").TotalMilliseconds is >= 5000 and < 6250, "bounded transient jitter");
        Console.WriteLine($"Testing performance fixtures: {checks} passed.");
    }
}
