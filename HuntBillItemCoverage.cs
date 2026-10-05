#if GILLIONS_TEST_BUILD || GILLIONS_PERSONAL_STATE_TESTS
using System;
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
        bool complete = PersonalObservationCompatibility.Supports(game, sdk) && initialized && loaded && stable && container == KeyItemsContainer && size is >= 1 and <= 256
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
internal sealed class HuntBillItemSync {
    // Dispatch freshness and response classification are separate: a delayed
    // failure still requires backoff; only accepting an assertion needs a live sample.
    internal static bool NeedsCurrentSample(PersonalResponseDisposition disposition) => disposition == PersonalResponseDisposition.Acknowledged;
    private PersonalPreparedSnapshot? pending;
    private long preparedMonotonic;
    private DateTime preparedUtc;
    private long epoch;
    internal void Clear() { pending = null; }
    // Classification of a terminal response survives expiry/delayed framework
    // commit. It stops only this exact RAM preparation, never another session.
    internal void Block(PersonalPreparedSnapshot p) { if (ReferenceEquals(pending, p)) p.Blocked = true; }
    internal PersonalPreparedSnapshot? Prepare(string owner, string payload, long currentEpoch, DateTime now, long monotonic) {
        if (!PersonalObservationCompatibility.Key(owner) || !PersonalSyncPolicy.HuntCoveragePayloadValid(payload)) return null;
        if (pending is { Blocked: true } && epoch == currentEpoch && pending.OwnerKey == owner
            && HuntBillItemCoverage.SameStates(pending.Payload, payload)) return pending;
        if (pending is not null && (epoch != currentEpoch || pending.OwnerKey != owner || !Fresh(now, monotonic)
            || !HuntBillItemCoverage.SameStates(pending.Payload, payload))) Clear();
        // Acknowledged states refresh before the15s server eligibility lease expires.
        if (pending is { Acknowledged: true } && now - preparedUtc >= TimeSpan.FromSeconds(6)) Clear();
        if (pending is null) {
            pending = new(owner, "hunt_bills", Guid.NewGuid().ToString("N"), payload, PersonalSyncPolicy.Hash(payload));
            preparedUtc = now; preparedMonotonic = monotonic;
            epoch = currentEpoch;
        }
        return pending;
    }
    private bool Fresh(DateTime now, long monotonic) => now >= preparedUtc && now - preparedUtc <= TimeSpan.FromSeconds(10)
        && monotonic >= preparedMonotonic && Stopwatch.GetElapsedTime(preparedMonotonic, monotonic) <= TimeSpan.FromSeconds(10);
    internal bool Current(PersonalPreparedSnapshot p, string owner, string? livePayload, long currentEpoch, DateTime now, long monotonic) => ReferenceEquals(pending, p)
        && epoch == currentEpoch && PersonalSyncPolicy.HuntCoverageFresh(p.Payload, now)
        && owner == p.OwnerKey && !p.Blocked && Fresh(now, monotonic) && livePayload is not null
        && HuntBillItemCoverage.SameStates(p.Payload, livePayload);
}
#endif
