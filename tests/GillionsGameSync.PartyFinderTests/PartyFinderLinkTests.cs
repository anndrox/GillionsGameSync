using System.Text;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using GillionsGameSync;

internal static class PartyFinderLinkTests {
    private static int checks;
    private static readonly DateTime Now = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static PartyFinderLinkRequest Request() => new(Guid.NewGuid().ToString("D"), new string('a', 43),
        "57:1700000000:101", 101, "Fixture Recruiter", true, Now.AddSeconds(-10), Now.AddSeconds(110), Now.AddSeconds(60));
    private static object Envelope(PartyFinderLinkRequest r, string type = "party_finder", object? id = null, object? cross = null) => new {
        ok = true, nativeRequests = new { contract = "native-requests-v1", contractVersion = 1,
            acceptedClientProduct = "GillionsGameSyncTest", capabilities = new[] { "native_party_finder_link_v1" } },
        request = new { requestType = type, requestId = r.RequestId, claimToken = r.ClaimToken, listingKey = r.ListingKey,
            listingId = id ?? r.ListingId, recruiterName = r.RecruiterName, crossWorld = cross ?? r.CrossWorld,
            listingObservedAt = r.ObservedAtUtc.ToString("O"), listingExpiresAt = r.ListingExpiresAtUtc.ToString("O"), expiresAt = r.ExpiresAtUtc.ToString("O") }
    };
    private static PartyFinderLinkRequest? Parse(object o) { using var d = JsonDocument.Parse(JsonSerializer.Serialize(o)); return PartyFinderLinkPolicy.Parse(d.RootElement); }
    internal static async Task Run() {
        var r = Request();
        Check(PartyFinderLinkPolicy.Valid(r, Now), "Valid request rejected.");
        Check(Parse(Envelope(r)) == r, "Exact request roundtrip failed.");
        foreach (var type in new[] { "join", "item", "party_finder_search", "", "PARTY_FINDER" }) Check(Parse(Envelope(r, type)) is null, "Unknown action accepted.");
        foreach (object bad in new object[] { -1, 0x100000000L, "101", 1.5 }) Check(Parse(Envelope(r, id: bad)) is null, "Invalid uint JSON accepted.");
        foreach (object bad in new object[] { "true", 1, new { value = true } }) Check(Parse(Envelope(r, cross: bad)) is null, "Non-boolean crossWorld accepted.");
        Check(Parse(new { ok = true, request = Envelope(r) }) is null, "Missing capability ack accepted.");
        foreach (var name in new[] { "", " ", "x\nY", "x\0Y", "\u0002payload\u0003", "\u202ex", " x", "x ", new string('x',65), new string('界', 43), "\ud800" })
            Check(!PartyFinderLinkPolicy.RecruiterValid(name), "Unsafe recruiter accepted.");
        foreach (var name in new[] { "Fixture Recruiter", "Éclair Example", "テスト 名前", "O'Name Test" })
            Check(PartyFinderLinkPolicy.RecruiterFromBytes(Encoding.UTF8.GetBytes(name)) == name, "Valid game name rejected.");
        foreach (var bytes in new byte[][] { [0xff], [0xc0,0x80], [2,39,3], new byte[129] }) Check(PartyFinderLinkPolicy.RecruiterFromBytes(bytes) is null, "Bad game-text bytes accepted.");
        for (uint extra = 0; extra <= 36; extra += 4) {
            if ((extra & 9) != 0) continue;
            Check(PartyFinderLinkPolicy.TryCrossWorld(1|extra,out var dc) && dc, "Data-center scope incorrectly linked.");
            Check(PartyFinderLinkPolicy.TryCrossWorld(8|extra,out var world) && !world, "World scope incorrectly linked.");
        }
        foreach (uint flags in new uint[] { 0,2,4,9,255,256,uint.MaxValue }) Check(!PartyFinderLinkPolicy.TryCrossWorld(flags,out _), "Ambiguous scope accepted.");
        foreach (var invalid in new[] {
            r with { RequestId = Guid.Empty.ToString() }, r with { RequestId = "not-id" }, r with { ClaimToken = "" }, r with { ClaimToken = new string('a',501) },
            r with { ClaimToken = "xxxxxxxxxxxxxxxx/" }, r with { ListingId = 0 }, r with { ListingKey = "57:1700000000:102" },
            r with { ListingKey = "057:1700000000:101" }, r with { ListingKey = "0:0:101" }, r with { ListingKey = "65536:0:101" },
            r with { ExpiresAtUtc = Now }, r with { ExpiresAtUtc = Now.AddSeconds(61) }, r with { ListingExpiresAtUtc = Now },
            r with { ListingExpiresAtUtc = Now.AddSeconds(59) }, r with { ListingExpiresAtUtc = Now.AddHours(2) },
            r with { ObservedAtUtc = Now.AddMinutes(-6) }, r with { ObservedAtUtc = Now.AddSeconds(31) },
            r with { ObservedAtUtc = DateTime.SpecifyKind(Now,DateTimeKind.Unspecified) }, r with { RecruiterName = "" }
        }) Check(!PartyFinderLinkPolicy.Valid(invalid, Now), "Invalid identity/expiry admitted.");
        var processor = new PartyFinderLinkRequestProcessor(); int consumes = 0, prints = 0; bool enabled = true;
        Task<bool> Consume(PartyFinderLinkRequest _) { consumes++; return Task.FromResult(true); }
        Task Print(PartyFinderLinkRequest _) { Check(consumes > 0, "Presentation preceded consume."); prints++; return Task.CompletedTask; }
        var outcome = await processor.ProcessAsync(r, () => Now, () => enabled, Consume, Print);
        Check(prints == 1 && consumes == 1 && outcome.Contains("final in-game click"), "Valid delivery failed.");
        await processor.ProcessAsync(r, () => Now, () => enabled, Consume, Print);
        Check(prints == 1 && consumes == 1, "Retry replayed delivery.");
        await processor.ProcessAsync(Request(), () => Now, () => false, Consume, Print);
        Check(prints == 1 && consumes == 1, "Disabled/session-invalid action consumed.");
        await processor.ProcessAsync(Request(), () => Now, () => true, _ => Task.FromResult(false), Print);
        Check(prints == 1, "Rejected consume printed.");
        foreach (var reason in new[] { "logout", "re-pair", "device-change", "plugin-disable", "feature-disable" }) {
            enabled = true; var pending = new TaskCompletionSource<bool>(); var req = Request();
            var action = processor.ProcessAsync(req, () => Now, () => enabled, _ => pending.Task, Print);
            enabled = false; pending.SetResult(true); await action;
            Check(prints == 1, reason + " did not invalidate presentation.");
        }
        var current = Now;
        await processor.ProcessAsync(Request(), () => current, () => true, _ => { current = Now.AddSeconds(61); return Task.FromResult(true); }, Print);
        Check(prints == 1, "Expiry during consume printed.");
        var lost = Request();
        try { await processor.ProcessAsync(lost, () => Now, () => true, _ => throw new IOException(), Print); } catch (IOException) { }
        await processor.ProcessAsync(lost, () => Now, () => true, Consume, Print);
        Check(prints == 1, "Lost consume response replayed an attempt.");
        var blocked = new TaskCompletionSource<bool>(); var concurrent = Request();
        var first = processor.ProcessAsync(concurrent, () => Now, () => true, _ => blocked.Task, _ => Task.CompletedTask);
        Check(await processor.ProcessAsync(concurrent, () => Now, () => true, Consume, Print) == "Already attempted", "Concurrent request replayed.");
        blocked.SetResult(false); await first;
        var memory = (System.Collections.Generic.HashSet<string>)typeof(PartyFinderLinkRequestProcessor).GetField("attempted", System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(processor)!;
        for (int i = 0; i < 256; i++) await processor.ProcessAsync(Request(), () => Now, () => true, _ => Task.FromResult(false), Print);
        Check(memory.Count == 128 && prints == 1, "Attempt memory unbounded or rejected requests printed.");
        await RedirectsCannotAuthorizeOrTransferClaims();
        Console.WriteLine($"Party Finder native request/identity/encoding/consent/expiry/consume/lifecycle fixtures passed: {checks}; synthetic, no game UI invoked.");
    }

    private static async Task RedirectsCannotAuthorizeOrTransferClaims() {
        // Actual production handler; loopback sockets are disposable HTTP
        // fixtures, not a new accepted plugin origin or a TLS bypass.
        foreach (var status in new[] { 301, 302, 303, 307, 308 }) {
            foreach (var kind in new[] { "poll", "consume" }) {
                using var source = new TcpListener(IPAddress.Loopback, 0);
                using var foreign = new TcpListener(IPAddress.Loopback, 0);
                source.Start(); foreign.Start();
                var sourcePort = ((IPEndPoint)source.LocalEndpoint).Port;
                var foreignPort = ((IPEndPoint)foreign.LocalEndpoint).Port;
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                int foreignHits = 0;
                var foreignTask = Serve(foreign, _ => {
                    Interlocked.Increment(ref foreignHits);
                    return "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{}";
                }, cancellation.Token);
                var sourceTask = Serve(source, _ => $"HTTP/1.1 {status} Redirect\r\nLocation: http://127.0.0.1:{foreignPort}/foreign\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", cancellation.Token);
                using var client = PartyFinderHttp.CreateClient();
                using var message = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{sourcePort}/api/game-sync/item-links/{kind}") {
                    Content = new StringContent(kind == "consume" ? "{\"claimToken\":\"synthetic_private_claim\"}" : "{\"nativeRequests\":{}}", Encoding.UTF8, "application/json")
                };
                using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
                var originalBody = await sourceTask;
                Check((int)response.StatusCode == status && response.RequestMessage?.RequestUri == message.RequestUri, "PF redirect was followed or origin changed.");
                int printed = 0;
                await new PartyFinderLinkRequestProcessor().ProcessAsync(Request(), () => Now, () => true,
                    _ => Task.FromResult(response.IsSuccessStatusCode), _ => { printed++; return Task.CompletedTask; });
                cancellation.Cancel();
                string foreignBody = "";
                try { foreignBody = await foreignTask; } catch (OperationCanceledException) { }
                Check(foreignHits == 0 && foreignBody == "" && originalBody.Contains(kind == "consume" ? "claimToken" : "nativeRequests"), "PF redirect transferred request/secret claim to second origin.");
                Check(printed == 0, "Redirect authorized native presentation.");
            }
        }
    }
    private static async Task<string> Serve(TcpListener listener, Func<string,string> response, CancellationToken cancellation) {
        using var socket = await listener.AcceptTcpClientAsync(cancellation);
        await using var stream = socket.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen:true);
        int contentLength = 0, headerLength = 0;
        while (await reader.ReadLineAsync(cancellation) is { Length: >0 } line) {
            headerLength += line.Length;
            if (headerLength > 8192) throw new IOException("Oversized fixture header.");
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) contentLength = int.Parse(line[15..].Trim());
        }
        if (contentLength is <0 or >8192) throw new IOException("Oversized fixture body.");
        var body = new char[contentLength];
        if (await reader.ReadBlockAsync(body.AsMemory(), cancellation) != contentLength) throw new IOException("Incomplete fixture body.");
        var text = new string(body);
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response(text)), cancellation);
        return text;
    }
}
