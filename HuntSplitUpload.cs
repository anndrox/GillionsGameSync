#if GILLIONS_TEST_BUILD || GILLIONS_PERSONAL_STATE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;

namespace GillionsGameSync;

internal readonly record struct HuntUploadRevision(long History, long Coverage, long Epoch, long PermissionEpoch = 0);
internal readonly record struct HuntPreparationKey(HuntUploadRevision Revision, DateTime ObservedAtUtc);

internal sealed class HuntSplitPreparation(HuntPreparationKey key, string body, HuntBillObservation[] history, bool reconciles) {
    internal HuntPreparationKey Key { get; } = key;
    internal string Body { get; } = body;
    internal string Nonce { get; } = Guid.NewGuid().ToString("N");
    internal HuntBillObservation[] History { get; } = history;
    internal bool Reconciles { get; } = reconciles;
}

internal sealed class HuntSplitUpload {
    internal const long CoverageInterval = 10_000;
    internal const long HistoryInterval = 600_000;
    private HuntSplitPreparation? pending;
    private HuntBillObservation[] acknowledgedBills = [];
    private HuntUploadRevision? lastExamined;
    private long lastCoverageRevision = -1;
    private long nextCoverage;
    private long nextHistory;

    internal bool Due(HuntUploadRevision revision, long clock) =>
        pending is not null || lastExamined != revision || clock >= nextCoverage || clock >= nextHistory;

    internal HuntSplitPreparation? Prepare(HuntUploadRevision revision, HuntBillItemSnapshot sample, DateTime now, long clock,
        Func<HuntBillObservation[]> readHistory) {
        if (pending is { } previous && IsCurrent(previous, revision, sample, now)) return previous;
        if (pending is not null) lastExamined = null;
        pending = null;

        bool canRenew = sample.Domains.All(domain => domain.State != "unavailable");
        bool reconciliation = clock >= nextHistory;
        bool changed = lastExamined != revision;
        bool newCoverage = lastCoverageRevision != revision.Coverage;
        bool coverageDue = canRenew && (newCoverage || clock >= nextCoverage);
        if (!reconciliation && !changed && !coverageDue) return null;

        var history = reconciliation || changed ? readHistory() : acknowledgedBills;
        var changedBills = reconciliation ? history : history.Where(bill =>
            !acknowledgedBills.Any(prior => SameBill(prior, bill))).ToArray();
        bool removed = acknowledgedBills.Any(prior => !history.Any(bill => bill.BillTypeId == prior.BillTypeId));
        if (!canRenew && (newCoverage || changedBills.Length > 0 || reconciliation || removed)) changedBills = history;
        if (reconciliation && history.Length == 0) nextHistory = clock + HistoryInterval;
        lastExamined = revision;
        if (changedBills.Length == 0 && !coverageDue && !(removed && canRenew)) return null;

        var body = HuntBillItemCoverage.WithCoverage(HuntBillRetentionPolicy.SerializePrivateBills(changedBills), sample, compact: true);
        if (!PersonalSyncPolicy.HuntCoveragePayloadValid(body)) return null;
        return pending = new(new(revision, sample.ObservedAtUtc), body, history, reconciliation);
    }

    private static bool SameBill(HuntBillObservation a, HuntBillObservation b) =>
        a with { ObservationId = b.ObservationId, ObservedAtUtc = b.ObservedAtUtc, Targets = b.Targets } == b
        && a.Targets.SequenceEqual(b.Targets);

    internal bool IsCurrent(HuntSplitPreparation preparation, HuntUploadRevision revision, HuntBillItemSnapshot? sample, DateTime now) =>
        ReferenceEquals(pending, preparation) && preparation.Key.Revision == revision
        && sample is not null && sample.ObservedAtUtc >= preparation.Key.ObservedAtUtc
        && now >= preparation.Key.ObservedAtUtc
        && now - preparation.Key.ObservedAtUtc < TimeSpan.FromSeconds(HuntBillItemCoverage.MaximumAgeSeconds);

    internal void Acknowledge(HuntSplitPreparation preparation, long clock) {
        if (!ReferenceEquals(pending, preparation)) return;
        acknowledgedBills = preparation.History;
        lastCoverageRevision = preparation.Key.Revision.Coverage;
        nextCoverage = clock + CoverageInterval;
        if (preparation.Reconciles) nextHistory = clock + HistoryInterval;
        pending = null;
    }

    internal void Clear() {
        pending = null;
        acknowledgedBills = [];
        lastExamined = null;
        lastCoverageRevision = -1;
        nextCoverage = nextHistory = 0;
    }
}
#endif
