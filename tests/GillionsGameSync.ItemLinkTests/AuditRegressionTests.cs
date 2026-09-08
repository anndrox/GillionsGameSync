using System.Net;
using System.Text;
using System.Text.Json;
using GillionsGameSync;

internal static class AuditRegressionTests {
    private static int checks;
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
    private static void Throws<T>(Action action, string message) where T : Exception {
        try { action(); } catch (T) { checks++; return; }
        throw new InvalidOperationException(message);
    }
    private static async Task ThrowsAsync<T>(Func<Task> action, string message) where T : Exception {
        try { await action(); } catch (T) { checks++; return; }
        throw new InvalidOperationException(message);
    }
    private static GilLedgerEvent Gil(int id, long delta = 10) => new(id.ToString("D32"), Now, delta, "unclassified", "inferred", null, null, null, null, null, [], "Fixture", "Test");
    private static PairedSession Session() => PairedSession.Create("https://example.com", "11111111-1111-1111-1111-111111111111", new string('x', 43));

    public static async Task RunAsync() {
        TestOwnershipAndLifetimes(); TestAcknowledgementsAndSales(); TestFreshnessAndTransientState(); TestBudgetAndReload();
        await TestResponsesAsync(); TestWindowStates();
        Console.WriteLine($"Audit correction regression checks passed: {checks}.");
    }

    private static void TestOwnershipAndLifetimes() {
        Check(SyncOrigin.TryNormalize("https://EXAMPLE.com:443/", out var origin) && origin == "https://example.com", "HTTPS origins must canonicalize.");
        Check(SyncOrigin.TryNormalize("https://example.com:8443", out _), "Self-hosted HTTPS ports remain supported.");
        foreach (var bad in new[] { "http://example.com", "https://u:p@example.com", "https://example.com/api", "https://example.com?q=1", "https://example.com#x", "file:///tmp" })
            Check(!SyncOrigin.TryNormalize(bad, out _), "Reject non-origin pairing addresses.");
        var session = Session(); var token = new string('x', 43);
        Check(session.IsValid(session.DeviceId, token), "A fresh bound session validates.");
        Check(!session.IsValid(session.DeviceId, "wrong") && !session.IsValid(Guid.NewGuid().ToString(), token), "Token and device changes invalidate binding.");
        Check(!(session with { Origin = "http://example.com" }).IsValid(session.DeviceId, token), "A saved insecure binding cannot resume.");
        var restored = JsonSerializer.Deserialize<PairedSession>(JsonSerializer.Serialize(session))!;
        Check(restored.IsValid(session.DeviceId, token), "A valid future session resumes after reload.");
        Check(!SyncOwnershipPolicy.IsBoundSession(null, false, session.DeviceId, token), "A legacy token with no bound issuer never gains request authority.");
        Check(!SyncOwnershipPolicy.IsBoundSession(session, true, session.DeviceId, token), "An explicitly paused pairing cannot resume.");
        var states = new Dictionary<string, OwnedCharacterState>();
        var a = SyncOwnershipPolicy.GetCharacter(states, session, 111);
        a.PendingGilLedgerEvents.Add(Gil(1)); a.RetainerGilBalances["100"] = 30;
        var b = SyncOwnershipPolicy.GetCharacter(states, session, 222);
        Check(b.PendingGilLedgerEvents.Count == 0 && b.RetainerGilBalances.Count == 0, "Character B inherits neither A's queues nor its balances.");
        var newSession = Session();
        Check(SyncOwnershipPolicy.GetCharacter(states, newSession, 111).PendingGilLedgerEvents.Count == 0 && a.PendingGilLedgerEvents.Count == 1,
            "Re-pairing creates a fresh owner partition and preserves the former generation.");
        using var lifetime = new SyncRequestLifetime();
        var manual = lifetime.Capture(SyncRequestMode.Manual, 111, session, origin, token);
        var automatic = lifetime.Capture(SyncRequestMode.Automatic, 111, session, origin, token);
        var link = lifetime.Capture(SyncRequestMode.ItemLink, 111, session, origin, token);
        var hydration = lifetime.Capture(SyncRequestMode.Hydration, 111, session, origin, token);
        Check(lifetime.Accepts(manual, 111, session, token, true, true), "Current manual permit is valid.");
        Check(!lifetime.Accepts(manual, 222, session, token, true, true), "A reply for A cannot commit to B.");
        lifetime.InvalidateAutomatic();
        Check(automatic.Cancellation.IsCancellationRequested && !lifetime.Accepts(automatic, 111, session, token, true, true), "Opt-out invalidates even a later re-enabled automatic permit.");
        Check(lifetime.Accepts(manual, 111, session, token, false, true) && lifetime.Accepts(hydration, 111, session, token, false, true), "Manual and one-time hydration have separate permits.");
        lifetime.InvalidateItemLinks();
        Check(!lifetime.Accepts(link, 111, session, token, false, true), "An old item-link permit cannot print after opt-out/re-enable.");
        lifetime.Invalidate(); // A -> B
        lifetime.Invalidate(); // B -> A
        Check(!lifetime.Accepts(manual, 111, session, token, true, true) && manual.Cancellation.IsCancellationRequested, "A-B-A cannot revive an old request.");
        var current = lifetime.Capture(SyncRequestMode.Manual, 111, session, origin, token);
        Check(!lifetime.Accepts(current, 111, newSession, token, true, true), "Re-pair rejects the old generation's reply.");
        lifetime.Dispose();
        Check(!lifetime.Accepts(current, 111, session, token, true, true) && current.Cancellation.IsCancellationRequested, "Disposal cancels transport and rejects completion.");
    }

    private static void TestAcknowledgementsAndSales() {
        var owner = new RetainerVentureLocalState { CharacterContentId = "111" };
        var read = new RetainerVentureResultRead("100", 1, 1788264000, Now, 0, [new(5, 1)]);
        var partial = RetainerVentureSnapshotPolicy.CreateResultEvent(owner, read)!;
        var complete = RetainerVentureSnapshotPolicy.CreateResultEvent(owner, read with { AwardedExperience = 200 })!;
        var other = RetainerVentureSnapshotPolicy.CreateResultEvent(owner, read with { RetainerId = "200" })!;
        owner.PendingResultEvents = [complete, other];
        RetainerVentureSnapshotPolicy.AcknowledgeResults(owner, [partial.EventId, other.EventId], new Dictionary<string, string> {
            [partial.EventId] = partial.PayloadFingerprint, [other.EventId] = other.PayloadFingerprint });
        Check(owner.PendingResultEvents.SequenceEqual([complete]), "A subset ACK retires only sent current versions and keeps an in-flight upgrade.");
        Check(!RetainerVentureSnapshotPolicy.AddPendingResult(owner, partial) && owner.PendingResultEvents.Single() == complete, "A transient partial read cannot downgrade complete pending evidence.");
        var unrelated = new string('a', 64);
        var reply = JsonSerializer.Serialize(new { ok = true, resourceType = "retainer_ventures", schemaVersion = 1, snapshotAccepted = true, serverTimeUtc = Now, acceptedEventIds = new[] { unrelated } });
        Check(!RetainerAcknowledgementPolicy.TryParseExact(reply, [partial.EventId], out _), "Foreign ACK IDs never authorize retirement.");
        var a = new PendingRetainerSale("sale-a", Now, 30, 1, 1, "100", "A");
        var b = new PendingRetainerSale("sale-b", Now, 50, 2, 1, "200", "B");
        var pending = new List<PendingRetainerSale> { a, b };
        Check(GilLedgerPolicy.Confirm(pending, 30, "100").SequenceEqual([a]) && pending.SequenceEqual([b]), "Confirming A preserves B in the original full collection.");
        pending = [a, a with { SaleId = "equal" }, b];
        Check(GilLedgerPolicy.Confirm(pending, 30, "100").Count == 0 && pending.Count == 3, "Equal-value ambiguity preserves every sale.");
        Check(GilLedgerPolicy.Confirm(pending, 50, null).Count == 0 && pending.Count == 3, "Missing retainer ownership cannot establish a sale.");
        pending = [a, a with { SaleId = "second", Amount = 20 }, b];
        Check(GilLedgerPolicy.Confirm(pending, 50, "100").Count == 2 && pending.SequenceEqual([b]), "An exact full-owner aggregate preserves other retainers.");
        var old = Gil(1); var updated = old with { Kind = "vendor_sale", ItemId = 5, ItemQuantity = 1 };
        var gilPending = new List<GilLedgerEvent> { updated, Gil(2) };
        Check(GilLedgerPolicy.Acknowledge(gilPending, [old]) == 0 && gilPending.Count == 2, "Gil retirement compares exact captured versions.");
        Check(GilLedgerPolicy.Acknowledge(gilPending, [updated with { LogIntegerParameters = [] }]) == 1 && gilPending.Single().EventId == Gil(2).EventId, "Equivalent copied arrays permit exact Gil retirement.");
        var batch = Enumerable.Range(1, 200).Select(i => Gil(i)).ToArray();
        var wire = GilLedgerPolicy.PreparePayload("Fixture", "Test", new string('s', 32), batch);
        using var json = JsonDocument.Parse(wire);
        Check(json.RootElement.GetProperty("events").GetArrayLength() == 200, "The exact server batch maximum is supported.");
        Check(!json.RootElement.GetProperty("events")[0].TryGetProperty("itemId", out _)
            && !json.RootElement.GetProperty("events")[0].TryGetProperty("itemQuantity", out _), "Absent numeric fields must be omitted so the existing server does not normalize null to rejected zero.");
        Throws<InvalidOperationException>(() => GilLedgerPolicy.PreparePayload("Fixture", "Test", new string('s', 32), batch.Append(Gil(201)).ToArray()), "Never send more than 200 events.");
        Throws<InvalidOperationException>(() => GilLedgerPolicy.PreparePayload("Fixture", "Test", new string('s', 32), [Gil(1), Gil(2) with { ItemQuantity = 10000 }]), "One invalid event rejects the complete batch without partial retirement.");
        Throws<InvalidOperationException>(() => GilLedgerPolicy.PreparePayload("Fixture", "Test", new string('s', 32), [Gil(1), Gil(1)]), "Duplicate IDs cannot mask a partially accepted batch.");
    }

    private static void TestFreshnessAndTransientState() {
        var state = new RetainerVentureLocalState { CharacterContentId = "111" };
        var roster = new RetainerVentureRosterRead(Now, true, [new("100", "Fixture", 1, 10, 0, 0, 30)]);
        Check(RetainerVentureSnapshotPolicy.MergeRoster(state, roster, Now), "Initial roster is a semantic change.");
        var save = new ObservationSavePolicy(); save.RequestDurable(); save.Saved(Now);
        var freshnessSaves = 0;
        for (var second = 1; second <= 60; second++) {
            var instant = Now.AddSeconds(second);
            Check(!RetainerVentureSnapshotPolicy.MergeRoster(state, roster with { ObservedAtUtc = instant }, instant), "Identical active roster refresh does not schedule changed-data work.");
            save.Refresh();
            if (save.ShouldSave(instant)) { freshnessSaves++; save.Saved(instant); }
        }
        Check(freshnessSaves == 2 && state.RosterObservation.LastObservedAtUtc == Now.AddSeconds(60), "Freshness persists at most once per 30 seconds while in-memory observation stays current.");
        save.RequestDurable(); save.RequestDurable();
        Check(save.ShouldSave(Now.AddSeconds(60)), "Durable changes do not wait for the freshness deadline.");
        save.Saved(Now.AddSeconds(60)); Check(!save.ShouldSave(Now.AddSeconds(60)), "Same-turn durable requests coalesce into one save.");
        Check(RetainerVentureSnapshotPolicy.MarkRosterUnavailable(state, Now.AddSeconds(61)), "A loss of availability is a semantic change.");
        Check(!RetainerVentureSnapshotPolicy.MarkRosterUnavailable(state, Now.AddSeconds(62)), "Unavailable-to-unavailable is freshness only.");
        var view = new RetainerResultViewCache();
        var read = new RetainerVentureResultRead("100", 1, 1788264000, Now, 0, [new(5, 1)]);
        Check(view.Changed("111", read), "A new native result view is captured.");
        for (var frame = 1; frame <= 120; frame++) Check(!view.Changed("111", read with { ObservedAtUtc = Now.AddMilliseconds(frame * 16) }), "Repeated view frames do not construct or hash another event.");
        Check(view.Changed("111", read with { AwardedExperience = 1 }), "A partial-to-complete result is captured immediately.");
        view.Changed("111", null); Check(view.Changed("111", read), "Closing and reopening the view resets the transient tuple.");
        Check(view.Changed("222", read), "Result-view memoization cannot cross characters.");
        var list = Enumerable.Range(0, 400).Select(i => Now.AddMilliseconds(i)).ToList();
        TransientEvidencePolicy.Prune(list, Now.AddSeconds(1), TimeSpan.FromSeconds(5), entry => entry);
        Check(list.Count == 256, "A busy matching window stays capped at 256.");
        TransientEvidencePolicy.Prune(list, Now.AddSeconds(10), TimeSpan.FromSeconds(5), entry => entry);
        Check(list.Count == 0, "Transient entries expire even without another matching input.");
        var map = Enumerable.Range(0, 400).ToDictionary(i => i, i => Now.AddMilliseconds(i));
        TransientEvidencePolicy.Prune(map, Now.AddSeconds(1), TimeSpan.FromSeconds(10), entry => entry);
        Check(map.Count == 256 && !map.ContainsKey(0), "Transient item attribution evicts the oldest opportunities.");
        TransientEvidencePolicy.Prune(map, Now.AddSeconds(11), TimeSpan.FromSeconds(10), entry => entry);
        Check(map.Count == 0, "Item attribution expires independently of lookups.");
    }

    private static void TestBudgetAndReload() {
        var session = Session(); var states = new Dictionary<string, OwnedCharacterState>();
        var a = SyncOwnershipPolicy.GetCharacter(states, session, 111); var b = SyncOwnershipPolicy.GetCharacter(states, session, 222);
        a.PendingGilLedgerEvents = Enumerable.Range(0, 5000).Select(i => Gil(i)).ToList();
        b.PendingGilLedgerEvents = Enumerable.Range(5000, 5000).Select(i => Gil(i)).ToList();
        var budget = new DurableEvidenceBudget(); var gap = new EvidenceCoverageGap();
        var usage = budget.Measure(states.Values);
        Check(usage.Records == 10000 && usage.Fits, "The shared record limit counts both characters.");
        Check(usage.Bytes == DurableEvidenceBudget.SerializeAccountingDocument(states.Values).LongLength, "Cached accounting exactly matches the owned serialized pending-data document.");
        var checkpoint = new DurableEvidenceCheckpoint(a); a.PendingGilLedgerEvents.Add(Gil(10000));
        Check(!budget.AcceptOrRestore(states.Values, checkpoint, gap, Now) && a.PendingGilLedgerEvents.Count == 5000 && gap.Paused,
            "Capacity plus one preserves every admitted record and records a coverage gap.");
        SyncOwnershipPolicy.GetCharacter(states, Session(), 111);
        Check(budget.Measure(states.Values).Records == 10000, "Re-pairing cannot reset the shared budget.");
        var reload = JsonSerializer.Deserialize<Dictionary<string, OwnedCharacterState>>(JsonSerializer.Serialize(states))!;
        var reloadedGap = JsonSerializer.Deserialize<EvidenceCoverageGap>(JsonSerializer.Serialize(gap))!;
        Check(new DurableEvidenceBudget().Measure(reload.Values) == usage && reloadedGap.Paused, "Queue accounting and the coverage gap survive reload.");
        GilLedgerPolicy.Acknowledge(a.PendingGilLedgerEvents, [a.PendingGilLedgerEvents[0]]);
        Check(budget.ResumeAfterDrain(states.Values, gap, Now.AddSeconds(1)) && !gap.Paused && gap.StartedAtUtc == Now, "Acknowledged drainage resumes admission and retains honest gap history.");
        var tiny = new OwnedCharacterState { Generation = session.Generation, CharacterContentId = "111" };
        tiny.PendingGilLedgerEvents.Add(Gil(1));
        var representative = budget.Measure([tiny]);
        var padding = checked((int)(DurableEvidenceBudget.MaximumBytes - representative.Bytes - 2));
        // null -> JSON string adds length - 2 bytes, hence the final adjustment.
        tiny.PendingGilLedgerEvents[0] = Gil(1) with { RetainerName = new string('x', padding) };
        var difference = checked((int)(DurableEvidenceBudget.MaximumBytes - budget.Measure([tiny]).Bytes));
        tiny.PendingGilLedgerEvents[0] = tiny.PendingGilLedgerEvents[0] with { RetainerName = new string('x', padding + difference) };
        Check(budget.Measure([tiny]).Bytes == DurableEvidenceBudget.MaximumBytes, "Exact serialized byte capacity is accepted.");
        var original = tiny.PendingGilLedgerEvents[0]; checkpoint = new DurableEvidenceCheckpoint(tiny);
        tiny.PendingGilLedgerEvents[0] = original with { RetainerName = original.RetainerName + "x" };
        gap = new();
        Check(!budget.AcceptOrRestore([tiny], checkpoint, gap, Now) && ReferenceEquals(tiny.PendingGilLedgerEvents[0], original),
            "A same-ID upgrade exceeding byte capacity restores the admitted version.");
        Console.WriteLine($"Synthetic pending-data sizes: one ordinary Gil row {representative.Bytes} bytes; 10,000 rows across two characters {usage.Bytes} bytes. These are fixture sizes, not player coverage estimates.");
    }

    private static async Task TestResponsesAsync() {
        var receipt = "{\"ok\":true,\"snapshotId\":\"123\",\"receivedAt\":\"2026-09-01T12:00:00Z\",\"unchanged\":false}";
        Check(SyncResponsePolicy.IsSnapshotReceipt(receipt, "gil_ledger"), "The current server's typed ordinary receipt is sufficient.");
        foreach (var bad in new[] { "{}", "{\"ok\":true}", receipt.Replace("true", "false"), receipt.Replace("\"123\"", "0"), receipt.Replace("false", "null"), "<html>proxy error</html>" })
            Check(!SyncResponsePolicy.IsSnapshotReceipt(bad, "gil_ledger"), "Malformed 2xx bodies never retire a Gil batch.");
        Check(!SyncResponsePolicy.IsSnapshotReceipt("{\"ok\":true,\"unchanged\":true}", "glamour_plates"), "A populated glamour request cannot accept an absent snapshot ID.");
        Check(SyncResponsePolicy.IsSnapshotReceipt("{\"ok\":true,\"unchanged\":true}", "glamour_plates", true), "The actual empty-glamour no-prior receipt remains compatible.");
        var huge = new CountingStream(Encoding.UTF8.GetBytes("{\"pad\":\"" + new string('x', 70000) + "\"}"));
        using (var content = new StreamContent(huge)) {
            content.Headers.ContentLength = 1; // dishonest peer header cannot bypass the streamed byte counter
            await ThrowsAsync<InvalidOperationException>(async () => await SyncResponsePolicy.ReadAsync(content), "Oversized chunked content must stop at the cap.");
            Check(huge.BytesRead <= SyncResponsePolicy.MaximumBytes + 1, "Bounded reader consumes no more than cap plus one byte.");
        }
        using (var content = new StreamContent(new CountingStream(Encoding.UTF8.GetBytes(receipt))))
            Check(await SyncResponsePolicy.ReadAsync(content) == receipt, "Missing Content-Length is supported for valid receipts.");
        using (var content = new StringContent(new string('[', 17) + "0" + new string(']', 17)))
            await ThrowsAsync<JsonException>(async () => await SyncResponsePolicy.ReadAsync(content), "Control responses deeper than 16 are rejected.");
        using (var response = new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"code\":\"SYNTHETIC_SECRET\",\"error\":\"SYNTHETIC_SECRET\"}") }) {
            try { await SyncResponsePolicy.EnsureSuccessfulAsync(response); throw new Exception("Expected rejection."); }
            catch (InvalidOperationException error) { Check(!error.Message.Contains("SYNTHETIC_SECRET") && !SyncErrorPolicy.Message(error).Contains("SYNTHETIC_SECRET"), "Remote error echoes cannot reach exception or UI text."); }
        }
        using (var response = new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("{\"code\":\"ACCOUNT_DISABLED\",\"error\":\"SYNTHETIC_SECRET\"}") }) {
            try { await SyncResponsePolicy.EnsureSuccessfulAsync(response); throw new Exception("Expected rejection."); }
            catch (GillionsSyncRejectedException error) { Check(error.Code == "ACCOUNT_DISABLED" && !error.Message.Contains("SYNTHETIC_SECRET"), "Known error codes map to local messages only."); }
        }
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        using var canceledContent = new StreamContent(new CountingStream(Encoding.UTF8.GetBytes(receipt)));
        await ThrowsAsync<OperationCanceledException>(async () => await SyncResponsePolicy.ReadAsync(canceledContent, canceled.Token), "Lifetime cancellation stops response reads.");
    }

    private static void TestWindowStates() {
        var legacy = PluginWindowModel.Create(false, true, true, true, false, false, false, "");
        Check(legacy.Warning!.Contains("preserved") && !legacy.CanSync, "Unbound upgrade explains re-pairing and inert history.");
        var paused = PluginWindowModel.Create(true, false, true, true, false, true, false, "");
        Check(paused.Warning!.Contains("missed") && paused.CanSync, "Coverage warnings remain visible while manual drainage is available.");
        var multipleWarnings = PluginWindowModel.Create(false, true, true, true, false, true, false, "ACCOUNT_DISABLED");
        Check(multipleWarnings.Warning!.Contains("disabled") && multipleWarnings.Warning.Contains("Offline storage") && multipleWarnings.Warning.Contains("Pair this device"),
            "Account, storage and re-pair warnings must remain visible together.");
        var disabled = PluginWindowModel.Create(true, false, true, false, false, false, false, "");
        Check(disabled.CanSync && disabled.Status.Contains("off"), "Automatic opt-out does not hide manual sync.");
        var logout = PluginWindowModel.Create(true, false, false, true, false, false, false, "");
        Check(!logout.CanSync && logout.Status.Contains("Log into"), "Logged-out connection status is explicit.");
    }

    private sealed class CountingStream(byte[] bytes) : Stream {
        private int position;
        public int BytesRead => position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => position; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) {
            var length = Math.Min(Math.Min(count, 997), bytes.Length - position);
            bytes.AsSpan(position, length).CopyTo(buffer.AsSpan(offset)); position += length; return length;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            var length = Math.Min(Math.Min(buffer.Length, 997), bytes.Length - position);
            bytes.AsMemory(position, length).CopyTo(buffer); position += length; return ValueTask.FromResult(length);
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
