using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace GillionsGameSync;

public sealed class EvidenceCoverageGap {
    public bool Paused { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? ResumedAtUtc { get; set; }
    public int RejectedObservations { get; set; }
    public void Reject(DateTime now) {
        if (!Paused) StartedAtUtc = now;
        Paused = true;
        if (RejectedObservations < int.MaxValue) RejectedObservations++;
    }
}

public sealed record EvidenceBudgetUsage(int Records, long Bytes) {
    public bool Fits => Records <= DurableEvidenceBudget.MaximumRecords && Bytes <= DurableEvidenceBudget.MaximumBytes;
    public bool HasRoom => Records < DurableEvidenceBudget.MaximumRecords && Bytes < DurableEvidenceBudget.MaximumBytes;
}

// The budget is the exact UTF-8 size of a flat JSON array of owned pending
// records. Owner and queue labels count; live snapshots and inert legacy fields
// do not. Immutable record sizes are cached without retaining removed records.
public sealed class DurableEvidenceBudget {
    public const int MaximumRecords = 10000;
    public const long MaximumBytes = 8 * 1024 * 1024;
    private sealed record RecordSize(int Bytes);
    private readonly ConditionalWeakTable<object, RecordSize> recordSizes = new();
    public EvidenceBudgetUsage Measure(IEnumerable<OwnedCharacterState> states) {
        var count = 0;
        long bytes = 2;
        foreach (var state in states) foreach (var group in Groups(state)) {
            var overhead = JsonSerializer.SerializeToUtf8Bytes(new { generation = state.Generation, contentId = state.CharacterContentId, queue = group.Kind, data = (object?)null }).Length - 4;
            foreach (var record in group.Records) {
                bytes += overhead + recordSizes.GetValue(record, value => new(JsonSerializer.SerializeToUtf8Bytes(value, value.GetType()).Length)).Bytes;
                if (count++ > 0) bytes++;
            }
        }
        return new(count, bytes);
    }
    public static byte[] SerializeAccountingDocument(IEnumerable<OwnedCharacterState> states) =>
        JsonSerializer.SerializeToUtf8Bytes(states.SelectMany(state => Groups(state).SelectMany(group => group.Records.Select(record =>
            new { generation = state.Generation, contentId = state.CharacterContentId, queue = group.Kind, data = record }))));
    private static IEnumerable<(string Kind, IEnumerable<object> Records)> Groups(OwnedCharacterState state) {
        yield return ("gil", state.PendingGilLedgerEvents);
        yield return ("receipt", state.PendingRetainerGilReceipts);
        yield return ("deposit", state.PendingRetainerGilDeposits);
        yield return ("sale", state.PendingRetainerSales);
        yield return ("result", state.RetainerState.PendingResultEvents);
    }

    public bool AcceptOrRestore(IEnumerable<OwnedCharacterState> states, DurableEvidenceCheckpoint checkpoint, EvidenceCoverageGap gap, DateTime now) {
        if (!checkpoint.Changed) return true;
        if (Measure(states).Fits) return true;
        checkpoint.Restore();
        gap.Reject(now);
        return false;
    }
    public bool ResumeAfterDrain(IEnumerable<OwnedCharacterState> states, EvidenceCoverageGap gap, DateTime now) {
        if (!gap.Paused || !Measure(states).HasRoom) return false;
        gap.Paused = false;
        gap.ResumedAtUtc = now;
        return true;
    }
}

// Short transactions copy references only. Existing records are immutable.
// On overflow all admitted evidence is restored, including same-ID upgrades.
public sealed class DurableEvidenceCheckpoint {
    private readonly OwnedCharacterState state;
    private readonly GilLedgerEvent[] gil;
    private readonly GilLedgerEvent[] receipts;
    private readonly GilLedgerEvent[] deposits;
    private readonly PendingRetainerSale[] sales;
    private readonly RetainerVentureResultEvent[] results;
    public DurableEvidenceCheckpoint(OwnedCharacterState state) {
        this.state = state;
        gil = state.PendingGilLedgerEvents.ToArray(); receipts = state.PendingRetainerGilReceipts.ToArray();
        deposits = state.PendingRetainerGilDeposits.ToArray(); sales = state.PendingRetainerSales.ToArray(); results = state.RetainerState.PendingResultEvents.ToArray();
    }
    public bool Changed => !Same(gil, state.PendingGilLedgerEvents) || !Same(receipts, state.PendingRetainerGilReceipts)
        || !Same(deposits, state.PendingRetainerGilDeposits) || !Same(sales, state.PendingRetainerSales) || !Same(results, state.RetainerState.PendingResultEvents);
    private static bool Same<T>(T[] before, List<T> after) where T : class => before.Length == after.Count && before.Where((entry, index) => !ReferenceEquals(entry, after[index])).Any() == false;
    public void Restore() {
        state.PendingGilLedgerEvents = gil.ToList(); state.PendingRetainerGilReceipts = receipts.ToList();
        state.PendingRetainerGilDeposits = deposits.ToList(); state.PendingRetainerSales = sales.ToList(); state.RetainerState.PendingResultEvents = results.ToList();
    }
}
