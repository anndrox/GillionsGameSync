using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GillionsGameSync;

internal static class GillionsPartyFinderTests {
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class Source : IPartyFinderContributionSource {
        public event Action<PartyFinderContributionListing>? ListingReceived;
        public void Emit(uint id = 1, ushort seconds = 120, bool large = false) => ListingReceived?.Invoke(Listing(id, seconds, large));
        public void Dispose() { ListingReceived = null; }
    }
    private static PartyFinderContributionListing Listing(uint id, ushort seconds, bool large) => new(new(
        id, 99, new byte[large ? 128 : 3], new byte[large ? 1024 : 4], 57, 57, 57, 1, 100, 1, true, seconds,
        700, 1, 7, 1700000000, 1, 0, 0, 0, 1,
        large ? Enumerable.Repeat(uint.MaxValue, 48).ToArray() : [1u], large ? Enumerable.Repeat((byte)255, 48).ToArray() : [(byte)19]));
    private sealed class Harness : IDisposable {
        internal DateTime Now = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
        internal bool Enabled;
        internal string? Key = "session-a";
        internal CancellationTokenSource Lifetime = new();
        internal readonly Source Source = new();
        internal readonly List<byte[]> Requests = [];
        internal readonly List<DateTime> RequestTimes = [];
        internal readonly List<string> Reports = [];
        internal Func<byte[], CancellationToken, Task<HttpResponseMessage>>? Handler;
        internal readonly GillionsPartyFinderContributor Contributor;
        internal Harness() {
            Contributor = new(Source, () => Key is null ? null : new(Key, Lifetime.Token, async (body, token) => {
                token.ThrowIfCancellationRequested(); Requests.Add(body); RequestTimes.Add(Now);
                return Handler is null ? Ack(body) : await Handler(body, token);
            }), () => Enabled, Reports.Add, () => Now, () => 1);
        }
        internal async Task Flush(int seconds = 10) { Now = Now.AddSeconds(seconds); Contributor.Tick(Now); await Contributor.WhenIdleAsync(); }
        public void Dispose() { Contributor.Dispose(); Lifetime.Dispose(); }
    }
    private static HttpResponseMessage Ack(byte[] body, bool wrong = false) {
        using var doc = JsonDocument.Parse(body);
        var accepted = doc.RootElement.GetProperty("listings").EnumerateArray().Select(row => new {
            listingKey = wrong ? "wrong" : $"{row.GetProperty("created_world")}:{row.GetProperty("last_server_restart")}:{row.GetProperty("id")}", status = "inserted"
        }).ToArray();
        return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new {
            ok = true, schemaVersion = 1, receivedAtUtc = "2026-09-30T12:00:00.000Z", accepted, currentCount = accepted.Length
        })) };
    }
    private static HttpResponseMessage Error(HttpStatusCode status, string code, int retry = 0) {
        var response = new HttpResponseMessage(status) { Content = new StringContent(JsonSerializer.Serialize(new { ok = false, code, retryAfterSeconds = retry })) };
        if (retry > 0) response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(retry));
        return response;
    }
    internal static async Task Run() {
        Check(XivpfEndpointPolicy.RequireBuildSafe(GillionsPartyFinderContributor.Endpoint, true) == GillionsPartyFinderContributor.Endpoint, "Testing HTTPS intake rejected.");
        foreach (var url in new[] { "http://127.0.0.1:8000/contribute/multiple", "https://xivpf.com/contribute/multiple", "https://example.com/api/game-sync/party-finder/contribute", "https://gillions.app/api/game-sync/party-finder/contribute?x=1" }) {
            bool refused = false;
            try { XivpfEndpointPolicy.RequireBuildSafe(new(url), true); } catch (InvalidOperationException) { refused = true; }
            Check(refused, "Testing intake accepted another destination.");
        }
        using (var h = new Harness()) {
            h.Source.Emit(); await h.Flush(); Check(h.Requests.Count == 0, "Off-by-default captured or sent.");
            h.Enabled = true; h.Key = null; h.Source.Emit(); await h.Flush(); Check(h.Contributor.PendingCount == 0, "Unpaired data queued.");
            h.Key = "session-a"; h.Source.Emit(); h.Source.Emit(); h.Source.Emit(2); await h.Flush();
            using var doc = JsonDocument.Parse(h.Requests.Single());
            Check(doc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet().SetEquals(["schemaVersion", "observedAtUtc", "listings"]), "Wrapper has extra fields.");
            Check(doc.RootElement.GetProperty("listings").GetArrayLength() == 2 && h.Reports.Count == 1, "Deduplication or acknowledgement failed.");
            Check(!h.Reports[0].Contains("1700000000"), "Diagnostics exposed identities.");
        }
        using (var h = new Harness { Enabled = true }) {
            for (uint i = 1; i <= 1000; i++) h.Source.Emit(i, 3600, large: true);
            h.Source.Emit(1001, 3600, large: true);
            Check(h.Contributor.PendingCount == 1000, "Pending bound changed.");
            for (int i = 0; i < 7; i++) await h.Flush();
            Check(h.Requests.All(body => body.Length <= 262144 && JsonDocument.Parse(body).RootElement.GetProperty("listings").GetArrayLength() <= 100), "Body or batch ceiling exceeded.");
            Check(h.RequestTimes.All(end => h.RequestTimes.Count(time => time <= end && time > end.AddMinutes(-1)) <= 6), "Rolling request ceiling exceeded.");
        }
        using (var h = new Harness { Enabled = true }) {
            h.Handler = (body, _) => Task.FromResult(Error(HttpStatusCode.TooManyRequests, "RATE_LIMITED", 45));
            h.Source.Emit(); await h.Flush(); var original = h.Requests.Single();
            h.Source.Emit(2); await h.Flush(44); Check(h.Requests.Count == 1, "Fresh observations bypassed Retry-After.");
            await h.Flush(2); Check(h.Requests.Count == 2 && original.SequenceEqual(h.Requests[1]), "Retry regenerated timestamp or payload.");
        }
        foreach (var failure in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.NotFound }) {
            using var h = new Harness { Enabled = true };
            h.Handler = (_, _) => Task.FromResult(Error(failure, "PARTY_FINDER_PERMISSION_REQUIRED"));
            h.Source.Emit(); await h.Flush(); h.Source.Emit(2); await h.Flush(60);
            Check(h.Requests.Count == 1 && h.Contributor.PendingCount == 0, "Permanent permission/redirect failure did not stop.");
            h.Key = "new-explicit-pair"; h.Source.Emit(3); await h.Flush(); Check(h.Requests.Count == 2, "Fresh pairing did not reset stopped session.");
        }
        using (var h = new Harness { Enabled = true }) {
            h.Handler = (_, _) => throw new HttpRequestException();
            h.Source.Emit(seconds: 15); await h.Flush(); await h.Flush(20);
            Check(h.Requests.Count == 1 && h.Contributor.PendingCount == 0, "Expired observations retried.");
            h.Source.Emit(2, 3600); await h.Flush(); await h.Flush(301);
            Check(h.Contributor.PendingCount == 0, "Five-minute stale observations retained.");
        }
        using (var h = new Harness { Enabled = true }) {
            h.Handler = (body, _) => Task.FromResult(Ack(body, wrong: true));
            h.Source.Emit(); await h.Flush();
            Check(h.Contributor.PendingCount == 1 && !h.Reports.Any(s => s.Contains("accepted 1")), "Wrong acknowledgement claimed success.");
            h.Enabled = false; h.Contributor.SetEnabled(false); Check(h.Contributor.PendingCount == 0, "Opt-out retained data.");
        }
        using (var h = new Harness { Enabled = true }) {
            h.Source.Emit(); h.Key = "session-b"; await h.Flush(); Check(h.Requests.Count == 0 && h.Contributor.PendingCount == 0, "Queued data crossed paired sessions.");
            var started = new TaskCompletionSource();
            h.Handler = async (_, token) => { started.SetResult(); await Task.Delay(Timeout.Infinite, token); return Ack([]); };
            h.Source.Emit(2); h.Now = h.Now.AddSeconds(10); h.Contributor.Tick(h.Now); await started.Task;
            h.Lifetime.Cancel(); h.Key = null; h.Contributor.Tick(h.Now); await h.Contributor.WhenIdleAsync();
            Check(h.Reports.Count == 0 && h.Contributor.PendingCount == 0, "Cancelled session reported or retried success.");
        }
        foreach (var code in new[] { HttpStatusCode.BadRequest, HttpStatusCode.MethodNotAllowed, HttpStatusCode.UnsupportedMediaType }) {
            using var h = new Harness { Enabled = true };
            h.Handler = (_, _) => Task.FromResult(Error(code, "INVALID_PARTY_FINDER_REQUEST"));
            h.Source.Emit(); await h.Flush(); await h.Flush(60); Check(h.Requests.Count == 1, "Malformed data retried unchanged.");
        }
        Console.WriteLine("Authenticated Gillions Party Finder contract, batching, session, retry and acknowledgement fixtures passed.");
    }
}
