using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GillionsGameSync;

// Catalog definitions, never order/acquisition or ownership identities.
internal sealed record HuntBillItemDomain(byte BillTypeId, uint KeyItemId, byte NativeType, uint OrderStart, uint OrderAmount);
// NativeConfirmedEmpty is a same-read per-slot proof, not an aggregate count or
// an inferred absence. It is never persisted, exported or reused across samples.
internal readonly record struct HuntKeyItemSlot(int Slot, int Container, bool Symbolic, uint ItemId, int Quantity, bool NativeConfirmedEmpty = false);
// Local numeric diagnostics only, never a source-admission input or payload field.
internal readonly record struct HuntItemProbeDiagnostics(int Probed, int Unavailable, int Empty,
    int IdZero, int IdRawMatch, int QuantityZero, int QuantityRawMatch, int Changed, int MappedHunt) {
    internal string Summary => $"getter-probes={Bound(Probed)}/8, getter-unavailable={Bound(Unavailable)}, getter-empty={Bound(Empty)}, getter-id-zero={Bound(IdZero)}, getter-id-raw-match={Bound(IdRawMatch)}, getter-qty-zero={Bound(QuantityZero)}, getter-qty-raw-match={Bound(QuantityRawMatch)}, getter-changed={Bound(Changed)}, mapped-hunt={Bound(MappedHunt)}";
    private static int Bound(int value) => Math.Clamp(value, 0, 8);
}
internal sealed record HuntBillItemState(byte BillTypeId, uint KeyItemId, string State);
internal sealed record HuntBillItemSnapshot(string ObservationId, DateTime ObservedAtUtc,
    string GameVersion, string CollectorVersion, string SdkVersion, HuntBillItemState[] Domains);

// Latest-only RAM. Missing reads never mutate historical positive progress.
internal sealed class HuntBillItemCoverage {
    internal const int KeyItemsContainer = 2004;
    internal const int MaximumAgeSeconds = 15;
    internal const string DomainKind = "currently-held-item-backed-bills";
    private HuntBillItemDomain[]? catalog;
    private string character = "";
    private HuntBillItemSnapshot? latest;
    private long observedMonotonic;
    internal long Revision { get; private set; }
    internal long Epoch { get; private set; }
    internal string Status { get; private set; } = "Hunt item coverage UNAVAILABLE; no current-order/cycle proof.";
    internal static bool ItemShapeValid(HuntKeyItemSlot s) => s.NativeConfirmedEmpty
        ? s.ItemId == 0 && s.Quantity > 0
        : (s.ItemId == 0 && s.Quantity == 0) || (s.ItemId > 0 && s.Quantity > 0);
    internal static bool CatalogValid(HuntBillItemDomain[]? rows) => rows is { Length: 22 }
        && rows.All(r => r is not null && r.BillTypeId < 22 && r.KeyItemId > 0 && r.NativeType is 1 or 2
            && r.OrderStart > 0 && r.OrderAmount > 0 && (ulong)r.OrderStart + r.OrderAmount <= uint.MaxValue)
        && rows.Select(r => r.BillTypeId).Distinct().Count() == 22
        && rows.Select(r => r.KeyItemId).Distinct().Count() == 22;
    internal void SetCatalog(HuntBillItemDomain[] rows) { catalog = CatalogValid(rows) ? rows.OrderBy(r => r.BillTypeId).ToArray() : null; if (catalog is null) Clear(); }
    internal void Clear() { if (latest is not null || character.Length > 0) { Epoch++; Revision++; } latest = null; character = ""; observedMonotonic = 0; Status = "Hunt item coverage UNAVAILABLE; session/source cleared."; }
    internal void Observe(string before, string after, bool initialized, bool loaded, int container, int size,
        HuntKeyItemSlot[] slots, bool stable, DateTime now, long monotonic, string game, string collector, string sdk, HuntItemProbeDiagnostics? probe = null) {
        if (!PersonalObservationCompatibility.Key(before) || before != after
            || !PersonalObservationCompatibility.Utc(now) || !PersonalObservationCompatibility.Metadata(game)
            || !PersonalObservationCompatibility.Metadata(collector) || !PersonalObservationCompatibility.Metadata(sdk)
            || catalog is null) { Clear(); return; }
        bool complete = NativeCompatibility.Supports(NativeFamily.Hunt, game, sdk) && initialized && loaded && stable && container == KeyItemsContainer && size is >= 1 and <= 256
            && slots.Length == size && slots.Count(s => s.NativeConfirmedEmpty) <= 8 && slots.Select((s, i) => s.Slot == i && s.Container == container && !s.Symbolic
                && ItemShapeValid(s)).All(v => v);
        var present = complete ? slots.Where(s => s.ItemId > 0).Select(s => s.ItemId).ToHashSet() : [];
        var domains = catalog.Select(d => new HuntBillItemState(d.BillTypeId, d.KeyItemId,
            !complete ? "unavailable" : present.Contains(d.KeyItemId) ? "present_unresolved" : "absent_confirmed")).ToArray();
        bool changed = character != before || latest is null || !latest.Domains.SequenceEqual(domains);
        if (character != before) Epoch++;
        character = before; observedMonotonic = monotonic;
        latest = new(Guid.NewGuid().ToString("N"), now, game, collector, sdk, domains);
        if (changed) Revision++;
        Status = complete ? $"Complete Key Items snapshot: {domains.Count(d => d.State == "absent_confirmed")} ABSENT_CONFIRMED / {domains.Count(d => d.State == "present_unresolved")} PRESENT_UNRESOLVED; native-confirmed empty slots={slots.Count(s => s.NativeConfirmedEmpty)}. Exact order/cycle unsupported."
            : $"Hunt item coverage UNAVAILABLE: loaded={loaded}, stable={stable}, initialized={initialized}, slots={slots.Length}/{size}, identity-mismatch={slots.Select((s,i) => s.Slot != i || s.Container != container).Count(v => v)}, symbolic={slots.Count(s => s.Symbolic)}, invalid-item={slots.Count(s => !ItemShapeValid(s))}, zero-id-nonzero-qty={slots.Count(s => s.ItemId == 0 && s.Quantity != 0)}, positive-id-zero-qty={slots.Count(s => s.ItemId > 0 && s.Quantity == 0)}, negative-qty={slots.Count(s => s.Quantity < 0)}. {probe?.Summary ?? "Getter probes not run"}. No negative inference.";
    }
    internal HuntBillItemSnapshot? Current(string key, DateTime now, long monotonic) => character == key && latest is not null
        && now >= latest.ObservedAtUtc && now - latest.ObservedAtUtc <= TimeSpan.FromSeconds(MaximumAgeSeconds)
        && monotonic >= observedMonotonic && Stopwatch.GetElapsedTime(observedMonotonic, monotonic) <= TimeSpan.FromSeconds(MaximumAgeSeconds)
        ? latest : null;
    internal static bool SameStates(string first, string second) {
        try {
            JsonNode? Semantic(string payload) {
                var n = JsonNode.Parse(payload);
                if (n?["billItemCoverage"] is JsonObject c) { c.Remove("observationId"); c.Remove("observedAtUtc"); }
                if (n?["bills"] is JsonArray bills) foreach (var b in bills.OfType<JsonObject>()) { b.Remove("observationId"); b.Remove("observedAtUtc"); }
                return n;
            }
            return JsonNode.DeepEquals(Semantic(first), Semantic(second));
        } catch (JsonException) { return false; }
    }
    internal string? Payload(HuntBillRetentionPolicy history, string key, DateTime now, long monotonic) {
        var snapshot = Current(key, now, monotonic);
        if (snapshot is null) return null;
        var root = JsonNode.Parse(history.PreparePrivateExport(key, allowEmpty: true))!.AsObject();
        root["schemaVersion"] = 2; root["collectorSchema"] = "hunt-bills-v2";
        root["billItemCoverage"] = JsonSerializer.SerializeToNode(new {
            collectorSchema = "hunt-bill-items-v1", domainKind = DomainKind,
            snapshot.ObservationId, snapshot.ObservedAtUtc, snapshot.GameVersion, snapshot.CollectorVersion, snapshot.SdkVersion,
            source = "complete-loaded-key-items-snapshot", maximumAgeSeconds = MaximumAgeSeconds,
            orderOwnership = "unsupported", acquisitionIdentity = "unsupported", domains = snapshot.Domains
        }, PersonalObservationCompatibility.Json);
        return root.ToJsonString(PersonalObservationCompatibility.Json);
    }
}

// Uses the existing exact-body nonce/receipt type, but never writes coverage to config.
// Expired/transitioned samples are abandoned, not replayed after reload. Historical
// positives remain durably retained and are included again in the next fresh payload.
#if GILLIONS_TEST_BUILD || GILLIONS_PUBLIC_BUILD || GILLIONS_PERSONAL_STATE_TESTS
internal sealed class HuntBillItemSync {
    internal const int RefreshAfterSeconds = 6;
    // Dispatch freshness and response classification are separate: a delayed
    // failure still requires backoff; only accepting an assertion needs a live sample.
    internal static bool NeedsCurrentSample(PersonalResponseDisposition disposition) => disposition == PersonalResponseDisposition.Acknowledged;
    private PersonalPreparedSnapshot? pending;
    private long preparedMonotonic;
    private DateTime preparedUtc;
    private DateTime observedUtc;
    private long epoch;
    private JsonElement? support;
    private string? preparedSupport;
    private bool supportRecoveryUsed, supportRecoveryAllowed;
    internal void Clear() { pending = null; support = null; preparedSupport = null; supportRecoveryUsed = supportRecoveryAllowed = false; }
    // Called only from the current authenticated presence commit, after existing
    // permission/origin gates. Retain public support metadata only, never the reply.
    internal void ObserveSupport(string? json, string origin) {
        support = null;
        if (json is null || !PersonalSyncPolicy.HuntCoverageCompatible(json, origin)) return;
        try {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            var value = doc.RootElement.GetProperty("personalObservations").GetProperty("resources")
                .EnumerateArray().Single(e => e.GetProperty("resourceType").GetString() == "hunt_bills").GetProperty("support");
            bool Shape(JsonElement e, params string[] keys) => e.ValueKind == JsonValueKind.Object
                && e.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(keys.Order());
            if (!Shape(value,"revision","nativeBuilds","referenceBuilds")) return;
            var revision = value.GetProperty("revision").GetString();
            if (revision is not { Length: > 15 and <= 128 } || !revision.StartsWith("hunt-reference:",StringComparison.Ordinal)
                || !revision.All(c => char.IsAsciiLetterOrDigit(c) || c is ':' or '-' or '_')) return;
            var native = value.GetProperty("nativeBuilds").EnumerateArray().ToArray();
            var refs = value.GetProperty("referenceBuilds").EnumerateArray().Select(e => e.GetString()).ToArray();
            if (native.Length is < 1 or > 16 || refs.Length is < 1 or > 16
                || native.Any(b => !Shape(b,"gameVersion","sdkVersion")
                    || !PersonalObservationCompatibility.Metadata(b.GetProperty("gameVersion").GetString())
                    || !PersonalObservationCompatibility.Metadata(b.GetProperty("sdkVersion").GetString()))
                || refs.Any(r => !PersonalObservationCompatibility.Metadata(r)) || refs.Distinct().Count() != refs.Length
                || native.Select(b => b.GetProperty("gameVersion").GetString()+":"+b.GetProperty("sdkVersion").GetString()).Distinct().Count() != native.Length) return;
            support = value.Clone();
        } catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { }
    }
    private string? SupportedIdentity(string payload) {
        if (support is not { } value) return null;
        try {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement; var coverage = root.GetProperty("billItemCoverage");
            var game = coverage.GetProperty("gameVersion").GetString(); var sdk = coverage.GetProperty("sdkVersion").GetString();
            var native = value.GetProperty("nativeBuilds").EnumerateArray().ToArray();
            var refs = value.GetProperty("referenceBuilds").EnumerateArray().Select(e => e.GetString()).ToArray();
            if (!native.Any(b => b.GetProperty("gameVersion").GetString() == game && b.GetProperty("sdkVersion").GetString() == sdk)
                || !refs.Contains(game) || root.GetProperty("bills").EnumerateArray().Any(b => !refs.Contains(b.GetProperty("gameVersion").GetString()))) return null;
            return PersonalSyncPolicy.Hash(value.GetProperty("revision").GetString()+"\n"
                +string.Join("\n",native.Select(b => b.GetProperty("gameVersion").GetString()+":"+b.GetProperty("sdkVersion").GetString()).Order())
                +"\n"+string.Join("\n",refs.Order()));
        } catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { return null; }
    }
    // Classification of a terminal response survives expiry/delayed framework
    // commit. It stops only this exact RAM preparation, never another session.
    internal void Block(PersonalPreparedSnapshot p, int status = 400) {
        if (ReferenceEquals(pending, p)) { p.Blocked = true; supportRecoveryAllowed = status == 400; }
    }
    // This is a dispatch deadline, not a new observation cadence. Measure from
    // the actual assertion time, not preparation/ACK; retries never renew it.
    // Both clocks must agree, so a wall-clock jump cannot invent a renewal.
    internal bool RefreshDue(DateTime now, long monotonic, DateTime? newestObservationUtc) => pending is { Acknowledged: true, Blocked: false }
        && newestObservationUtc is { } newest && newest > observedUtc && newest <= now
        && now >= preparedUtc && monotonic >= preparedMonotonic
        && now - observedUtc >= TimeSpan.FromSeconds(RefreshAfterSeconds)
        && Stopwatch.GetElapsedTime(preparedMonotonic, monotonic) + (preparedUtc - observedUtc)
            >= TimeSpan.FromSeconds(RefreshAfterSeconds);
    internal PersonalPreparedSnapshot? Prepare(string owner, string payload, long currentEpoch, DateTime now, long monotonic) {
        if (!PersonalObservationCompatibility.Key(owner) || !PersonalSyncPolicy.HuntCoveragePayloadValid(payload)) return null;
        using var doc = JsonDocument.Parse(payload);
        var candidateObservedUtc = doc.RootElement.GetProperty("billItemCoverage").GetProperty("observedAtUtc").GetDateTime();
        var sameInput = pending is not null && epoch == currentEpoch && pending.OwnerKey == owner
            && HuntBillItemCoverage.SameStates(pending.Payload, payload);
        if (!sameInput) { pending = null; supportRecoveryUsed = false; }
        var supported = SupportedIdentity(payload);
        if (pending is { Blocked: true }) {
            // One recovery per unchanged semantic input/session, even if contracts
            // alternate. Missing support, clocks and renewals cannot grant a retry.
            if (!supportRecoveryAllowed || supportRecoveryUsed || supported is null || supported == preparedSupport) return pending;
            pending = null; supportRecoveryUsed = true;
        }
        if (pending is { Acknowledged: true } && epoch == currentEpoch && pending.OwnerKey == owner
            && candidateObservedUtc <= observedUtc && HuntBillItemCoverage.SameStates(pending.Payload, payload)) return pending;
        if (pending is not null && (epoch != currentEpoch || pending.OwnerKey != owner || !Fresh(now, monotonic)
            || !HuntBillItemCoverage.SameStates(pending.Payload, payload))) pending = null;
        if (RefreshDue(now, monotonic, candidateObservedUtc)) pending = null;
        if (pending is null) {
            pending = new(owner, "hunt_bills", Guid.NewGuid().ToString("N"), payload, PersonalSyncPolicy.Hash(payload));
            preparedUtc = now; preparedMonotonic = monotonic;
            observedUtc = candidateObservedUtc;
            epoch = currentEpoch; preparedSupport = supported;
        }
        return pending;
    }
    internal double PreparationAgeMilliseconds(PersonalPreparedSnapshot p, long monotonic) => ReferenceEquals(pending, p)
        && monotonic >= preparedMonotonic ? Stopwatch.GetElapsedTime(preparedMonotonic, monotonic).TotalMilliseconds : 0;
    private bool Fresh(DateTime now, long monotonic) => now >= preparedUtc && now - preparedUtc <= TimeSpan.FromSeconds(10)
        && monotonic >= preparedMonotonic && Stopwatch.GetElapsedTime(preparedMonotonic, monotonic) <= TimeSpan.FromSeconds(10);
    internal bool Current(PersonalPreparedSnapshot p, string owner, string? livePayload, long currentEpoch, DateTime now, long monotonic) => ReferenceEquals(pending, p)
        && epoch == currentEpoch && PersonalSyncPolicy.HuntCoverageFresh(p.Payload, now)
        && owner == p.OwnerKey && !p.Blocked && Fresh(now, monotonic) && livePayload is not null
        && HuntBillItemCoverage.SameStates(p.Payload, livePayload);
}
#endif
