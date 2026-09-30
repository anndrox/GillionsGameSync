using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GillionsGameSync;

internal static class Program {
    private static readonly DateTime Epoch = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    private static async Task Main(string[] args) {
        TestExactPayloadSerialization();
        TestEndpointPolicy();
        TestDedicatedClientRejectsRedirects();
        await TestOffByDefaultAndNoCaptureWhileDisabled();
        await TestBatchingDeduplicationAndDirectRequest();
        await TestUploadStartupDoesNotBlockTick();
        await TestRedirectResponseIsOneFailedAttempt();
        await TestFailedRequestRetryDelay();
        await TestRollingRequestCeiling();
        TestBoundedPendingData();
        await TestDisableCancelsAndClearsImmediately();
        await TestCompletionRacingDisableIsSafe();
        await TestCompletionRacingDisposeIsSafe();
        TestDisposedCancellationIsSafe();
        TestDisposalUnsubscribes();
        await GillionsPartyFinderTests.Run();
        if (args is ["--integration", var endpoint]) await TestLocalIntegration(new Uri(endpoint));
        Console.WriteLine("Party Finder contribution behavior verification passed.");
    }

    private static void TestExactPayloadSerialization() {
        var listing = Listing(17, contentId: 0xAABBCCDD, marker: 9);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new[] { listing }));
        var root = document.RootElement[0];
        var expectedNames = new HashSet<string> {
            "id", "content_id_lower", "name", "description", "created_world", "home_world", "current_world",
            "category", "duty", "duty_type", "beginners_welcome", "seconds_remaining", "min_item_level",
            "num_parties", "slots_available", "last_server_restart", "objective", "conditions",
            "duty_finder_settings", "loot_rules", "search_area", "slots", "jobs_present",
        };
        Assert(root.EnumerateObject().Select(property => property.Name).ToHashSet().SetEquals(expectedNames),
            "Serialized listing fields differ from the pinned UploadableListing contract.");
        Assert(root.GetProperty("id").GetUInt32() == 17 && root.GetProperty("content_id_lower").GetUInt32() == 0xAABBCCDD,
            "Listing identity mapping changed.");
        Assert(root.GetProperty("name").GetString() == Convert.ToBase64String(new byte[] { 0x41, 9 })
            && root.GetProperty("description").GetString() == Convert.ToBase64String(new byte[] { 0x42, 9 }),
            "Byte-oriented name and description encoding changed.");
        Assert(root.GetProperty("created_world").GetUInt16() == 21
            && root.GetProperty("home_world").GetUInt16() == 22
            && root.GetProperty("current_world").GetUInt16() == 23
            && root.GetProperty("category").GetUInt32() == 24
            && root.GetProperty("duty").GetUInt16() == 25
            && root.GetProperty("duty_type").GetByte() == 26
            && root.GetProperty("beginners_welcome").GetBoolean()
            && root.GetProperty("seconds_remaining").GetUInt16() == 27
            && root.GetProperty("min_item_level").GetUInt16() == 28
            && root.GetProperty("num_parties").GetByte() == 2
            && root.GetProperty("slots_available").GetByte() == 3
            && root.GetProperty("last_server_restart").GetUInt32() == 29
            && root.GetProperty("objective").GetUInt32() == 30
            && root.GetProperty("conditions").GetUInt32() == 31
            && root.GetProperty("duty_finder_settings").GetUInt32() == 32
            && root.GetProperty("loot_rules").GetUInt32() == 33
            && root.GetProperty("search_area").GetUInt32() == 34,
            "Numeric UploadableListing semantics changed.");
        Assert(root.GetProperty("slots").EnumerateArray().Select(slot => slot.GetProperty("accepting").GetUInt32()).SequenceEqual(new uint[] { 3, 12 })
            && root.GetProperty("jobs_present").EnumerateArray().Select(job => job.GetByte()).SequenceEqual(new byte[] { 7, 8 }),
            "Slot masks or present-job bytes changed.");
    }

    private static void TestEndpointPolicy() {
        Assert(XivpfEndpointPolicy.RequireSafe(new("https://xivpf.com/contribute/multiple"), false).Scheme == "https",
            "Production HTTPS endpoint was rejected.");
        Assert(XivpfEndpointPolicy.RequireSafe(new("http://127.0.0.1:8000/contribute/multiple"), true).IsLoopback,
            "Loopback test endpoint was rejected.");
        AssertThrows(() => XivpfEndpointPolicy.RequireSafe(new("http://xivpf.com/contribute/multiple"), false),
            "Non-loopback HTTP must be rejected.");
        AssertThrows(() => XivpfEndpointPolicy.RequireSafe(new("https://xivpf.com/contribute/multiple"), true),
            "Testing builds must reject the production endpoint.");
        AssertThrows(() => XivpfEndpointPolicy.RequireSafe(new("ftp://127.0.0.1/contribute/multiple"), false),
            "Non-HTTP protocols must be rejected.");
        AssertThrows(() => XivpfEndpointPolicy.RequireSafe(new("https://user:secret@xivpf.com/contribute/multiple"), false),
            "Endpoint credentials must be rejected.");
        AssertThrows(() => XivpfEndpointPolicy.RequireBuildSafe(new("https://example.com/contribute/multiple"), false),
            "Stable-compatible builds must reject non-official remote HTTPS endpoints.");
        Assert(XivpfEndpointPolicy.RequireBuildSafe(new("http://localhost:8000/contribute/multiple"), false).IsLoopback,
            "Stable-compatible local validation must retain loopback HTTP support.");
    }

    private static void TestDedicatedClientRejectsRedirects() {
        using var handler = PartyFinderHttp.CreateHandler();
        Assert(!handler.AllowAutoRedirect, "The dedicated contribution client must never follow endpoint redirects.");
    }

    private static async Task TestOffByDefaultAndNoCaptureWhileDisabled() {
        var enabled = false;
        var clock = new ManualClock(Epoch);
        var source = new FakeSource();
        var handler = new RecordingHandler();
        using var contributor = Contributor(source, handler, () => enabled, clock);
        source.Emit(Listing(1));
        contributor.Tick(clock.Now.AddHours(1));
        await contributor.WhenIdleAsync();
        Assert(contributor.PendingCount == 0 && handler.Requests.Count == 0,
            "Disabled contribution captured or submitted a listing.");
    }

    private static async Task TestBatchingDeduplicationAndDirectRequest() {
        var enabled = true;
        var clock = new ManualClock(Epoch);
        var source = new FakeSource();
        var handler = new RecordingHandler();
        using var contributor = Contributor(source, handler, () => enabled, clock);
        source.Emit(Listing(1, marker: 1));
        clock.Now = Epoch.AddSeconds(5);
        source.Emit(Listing(1, marker: 9));
        source.Emit(Listing(2, marker: 2));
        contributor.Tick(Epoch.AddSeconds(14));
        Assert(handler.Requests.Count == 0, "Batch uploaded before ten seconds after the newest listing.");
        contributor.Tick(Epoch.AddSeconds(15));
        await contributor.WhenIdleAsync();
        Assert(handler.Requests.Count == 1, "A listing burst did not produce exactly one batch request.");
        var request = handler.Requests[0];
        Assert(request.Method == HttpMethod.Post && request.Uri == new Uri("http://127.0.0.1:8000/contribute/multiple"),
            "Contribution did not use the direct configured POST endpoint.");
        Assert(request.Authorization is null && request.UserAgent == "GillionsGameSync/9.9.9",
            "Contribution added credentials or lost its recognizable user agent.");
        using var payload = JsonDocument.Parse(request.Body);
        Assert(payload.RootElement.GetArrayLength() == 2, "Listing identities were not deduplicated in the batch.");
        var latest = payload.RootElement.EnumerateArray().Single(value => value.GetProperty("id").GetUInt32() == 1);
        Assert(latest.GetProperty("description").GetString() == Convert.ToBase64String(new byte[] { 0x42, 9 }),
            "Deduplication did not preserve the newest listing observation.");
        Assert(!request.Body.Contains("token", StringComparison.OrdinalIgnoreCase)
            && !request.Body.Contains("device", StringComparison.OrdinalIgnoreCase)
            && !request.Body.Contains("account", StringComparison.OrdinalIgnoreCase),
            "Contribution body contains unrelated Gillions identity data.");
    }

    private static async Task TestUploadStartupDoesNotBlockTick() {
        var source = new FakeSource();
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var returned = new ManualResetEventSlim();
        var requestThread = 0;
        var callerThread = 0;
        var handler = new RecordingHandler((_, _, _) => {
            requestThread = Environment.CurrentManagedThreadId;
            started.Set();
            release.Wait(TimeSpan.FromSeconds(5));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var contributor = Contributor(source, handler, () => true, new ManualClock(Epoch), TimeSpan.Zero);
        source.Emit(Listing(1));
        var caller = new Thread(() => {
            callerThread = Environment.CurrentManagedThreadId;
            contributor.Tick(Epoch);
            returned.Set();
        });
        caller.Start();
        bool startupObserved;
        bool returnedBeforeRelease;
        try {
            startupObserved = started.Wait(TimeSpan.FromSeconds(5));
            returnedBeforeRelease = returned.Wait(TimeSpan.FromSeconds(1));
        } finally { release.Set(); }
        caller.Join();
        await contributor.WhenIdleAsync();
        Assert(startupObserved && returnedBeforeRelease && requestThread != callerThread,
            "Synchronous HTTP startup must not block the framework Tick caller.");
        Assert(handler.Requests.Count == 1 && contributor.PendingCount == 0,
            "Off-thread upload must preserve one successful batch.");
    }

    private static async Task TestFailedRequestRetryDelay() {
        var enabled = true;
        var clock = new ManualClock(Epoch);
        var source = new FakeSource();
        var handler = new RecordingHandler((attempt, _, _) => Task.FromResult(new HttpResponseMessage(
            attempt == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)));
        using var contributor = Contributor(source, handler, () => enabled, clock);
        source.Emit(Listing(1));
        clock.Now = Epoch.AddSeconds(10);
        contributor.Tick(clock.Now);
        await contributor.WhenIdleAsync();
        Assert(handler.Requests.Count == 1 && contributor.PendingCount == 1, "Failed request did not retain one bounded retry.");
        clock.Now = Epoch.AddSeconds(19);
        contributor.Tick(clock.Now);
        Assert(handler.Requests.Count == 1, "Failed request retried before its ten-second backoff.");
        clock.Now = Epoch.AddSeconds(20);
        contributor.Tick(clock.Now);
        await contributor.WhenIdleAsync();
        Assert(handler.Requests.Count == 2 && contributor.PendingCount == 0, "Bounded retry did not complete normally.");
    }

    private static async Task TestRedirectResponseIsOneFailedAttempt() {
        var enabled = true;
        var clock = new ManualClock(Epoch);
        var source = new FakeSource();
        var handler = new RecordingHandler((_, _, _) => {
            var response = new HttpResponseMessage(HttpStatusCode.TemporaryRedirect);
            response.Headers.Location = XivpfEndpointPolicy.ProductionEndpoint;
            return Task.FromResult(response);
        });
        using var contributor = Contributor(source, handler, () => enabled, clock, TimeSpan.Zero);
        source.Emit(Listing(1));
        contributor.Tick(clock.Now);
        await contributor.WhenIdleAsync();
        Assert(handler.Requests.Count == 1 && contributor.PendingCount == 1,
            "A redirect response must be one failed, bounded attempt with no follow-up request.");
    }

    private static async Task TestRollingRequestCeiling() {
        var enabled = true;
        var clock = new ManualClock(Epoch);
        var source = new FakeSource();
        var handler = new RecordingHandler();
        using var contributor = Contributor(source, handler, () => enabled, clock, TimeSpan.Zero);
        for (uint id = 1; id <= 6; id++) {
            source.Emit(Listing(id));
            contributor.Tick(clock.Now);
            await contributor.WhenIdleAsync();
        }
        source.Emit(Listing(7));
        contributor.Tick(clock.Now);
        Assert(handler.Requests.Count == 6 && contributor.PendingCount == 1,
            "Rolling limiter allowed more than six attempts, including completed requests.");
        clock.Now = Epoch.AddSeconds(59);
        contributor.Tick(clock.Now);
        Assert(handler.Requests.Count == 6, "Rolling limiter released a request before a full minute elapsed.");
        clock.Now = Epoch.AddMinutes(1);
        contributor.Tick(clock.Now);
        await contributor.WhenIdleAsync();
        Assert(handler.Requests.Count == 7, "Rolling limiter did not release the queued request after the window expired.");
    }

    private static void TestBoundedPendingData() {
        var enabled = true;
        var clock = new ManualClock(Epoch);
        var source = new FakeSource();
        using var contributor = Contributor(source, new RecordingHandler(), () => enabled, clock);
        for (uint id = 1; id <= PartyFinderContributor.MaximumPendingListings + 5; id++) source.Emit(Listing(id));
        Assert(contributor.PendingCount == PartyFinderContributor.MaximumPendingListings,
            "Pending Party Finder data exceeded its fixed in-memory identity bound.");
    }

    private static async Task TestDisableCancelsAndClearsImmediately() {
        var enabled = true;
        var clock = new ManualClock(Epoch);
        var source = new FakeSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler(async (_, _, cancellation) => {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellation);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var contributor = Contributor(source, handler, () => enabled, clock, TimeSpan.Zero);
        source.Emit(Listing(1));
        contributor.Tick(clock.Now);
        await started.Task;
        enabled = false;
        contributor.SetEnabled(false);
        await contributor.WhenIdleAsync();
        source.Emit(Listing(2));
        contributor.Tick(clock.Now.AddHours(1));
        Assert(contributor.PendingCount == 0 && handler.Requests.Count == 1,
            "Disabling did not cancel active work, clear unsent data and stop collection immediately.");
    }

    private static async Task TestCompletionRacingDisableIsSafe() {
        var enabled = true;
        var clock = new ManualClock(Epoch);
        var source = new FakeSource();
        var complete = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler((_, _, _) => complete.Task);
        using var contributor = Contributor(source, handler, () => enabled, clock, TimeSpan.Zero);
        source.Emit(Listing(1));
        contributor.Tick(clock.Now);
        var upload = contributor.WhenIdleAsync();
        enabled = false;
        var disable = Task.Run(() => contributor.SetEnabled(false));
        complete.TrySetResult(new HttpResponseMessage(HttpStatusCode.OK));
        await Task.WhenAll(upload, disable);
        Assert(contributor.PendingCount == 0, "Completion racing opt-out threw or retained unsent data.");
    }

    private static async Task TestCompletionRacingDisposeIsSafe() {
        var clock = new ManualClock(Epoch);
        var source = new FakeSource();
        var complete = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler((_, _, _) => complete.Task);
        var contributor = Contributor(source, handler, () => true, clock, TimeSpan.Zero);
        source.Emit(Listing(1));
        contributor.Tick(clock.Now);
        var upload = contributor.WhenIdleAsync();
        var dispose = Task.Run(contributor.Dispose);
        complete.TrySetResult(new HttpResponseMessage(HttpStatusCode.OK));
        await Task.WhenAll(upload, dispose);
        Assert(source.SubscriberCount == 0 && source.Disposed, "Completion racing disposal threw or retained the event source.");
    }

    private static void TestDisposedCancellationIsSafe() {
        var cancellation = new CancellationTokenSource();
        cancellation.Dispose();
        PartyFinderContributor.CancelSafely(cancellation);
    }

    private static void TestDisposalUnsubscribes() {
        var source = new FakeSource();
        var contributor = Contributor(source, new RecordingHandler(), () => true, new ManualClock(Epoch));
        Assert(source.SubscriberCount == 1, "Contributor did not subscribe to its event source.");
        contributor.Dispose();
        Assert(source.SubscriberCount == 0 && source.Disposed, "Disposal did not unsubscribe and dispose the event source.");
    }

    private static async Task TestLocalIntegration(Uri endpoint) {
        endpoint = XivpfEndpointPolicy.RequireSafe(endpoint, true);
        var enabled = true;
        var clock = new ManualClock(Epoch);
        var source = new FakeSource();
        var log = new FakeLog();
        using var http = PartyFinderHttp.CreateClient();
        using var contributor = new PartyFinderContributor(source, http, log, () => enabled, endpoint,
            "GillionsGameSync/local-integration", () => clock.Now, TimeSpan.Zero);
        source.Emit(IntegrationListing());
        contributor.Tick(clock.Now);
        await contributor.WhenIdleAsync();
        Assert(log.UploadedCount == 1 && log.FailureCount == 0 && contributor.PendingCount == 0,
            "Official loopback Remote Party Finder server did not accept the contributor payload.");
        Console.WriteLine($"Official loopback Remote Party Finder accepted {log.UploadedCount} synthetic listing batch.");
    }

    private static PartyFinderContributor Contributor(FakeSource source, RecordingHandler handler, Func<bool> enabled,
        ManualClock clock, TimeSpan? uploadDelay = null) => new(source, new HttpClient(handler), new FakeLog(), enabled,
            new Uri("http://127.0.0.1:8000/contribute/multiple"), "GillionsGameSync/9.9.9", () => clock.Now, uploadDelay);

    private static PartyFinderContributionListing Listing(uint id, uint contentId = 0x11223344, byte marker = 1) => new(
        new PartyFinderListingSnapshot(id, contentId, [0x41, marker], [0x42, marker], 21, 22, 23, 24, 25, 26, true,
            27, 28, 2, 3, 29, 30, 31, 32, 33, 34, [3, 12], [7, 8]));

    private static PartyFinderContributionListing IntegrationListing() => new(
        new PartyFinderListingSnapshot(424242, 0x55667788, [0x41, 0x42], [0x43, 0x44], 21, 22, 23,
            16, 0, 2, true, 300, 0, 1, 1, 29, 2, 4, 1, 0, 1, [256], [0]));

    private static void AssertThrows(Action action, string message) {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Assert(bool condition, string message) {
        if (!condition) throw new InvalidOperationException(message);
    }
}

internal sealed class ManualClock(DateTime now) {
    internal DateTime Now { get; set; } = now;
}

internal sealed class FakeSource : IPartyFinderContributionSource {
    private Action<PartyFinderContributionListing>? handlers;
    internal int SubscriberCount { get; private set; }
    internal bool Disposed { get; private set; }
    public event Action<PartyFinderContributionListing>? ListingReceived {
        add { handlers += value; SubscriberCount++; }
        remove { handlers -= value; SubscriberCount--; }
    }
    internal void Emit(PartyFinderContributionListing listing) => handlers?.Invoke(listing);
    public void Dispose() => Disposed = true;
}

internal sealed class FakeLog : IPartyFinderContributionLog {
    internal int UploadedCount { get; private set; }
    internal int FailureCount { get; private set; }
    public void Uploaded(int count) => UploadedCount += count;
    public void Failed(Exception error) => FailureCount++;
}

internal sealed record RecordedRequest(HttpMethod Method, Uri? Uri, string UserAgent, AuthenticationHeaderValue? Authorization, string Body);

internal sealed class RecordingHandler : HttpMessageHandler {
    private readonly Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder;
    internal List<RecordedRequest> Requests { get; } = [];

    internal RecordingHandler(Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? responder = null) {
        this.responder = responder ?? ((_, _, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new(request.Method, request.RequestUri, request.Headers.UserAgent.ToString(), request.Headers.Authorization, body));
        return await responder(Requests.Count, request, cancellationToken);
    }
}
