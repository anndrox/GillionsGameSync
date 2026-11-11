using System;
using System.Collections.Generic;
using System.Linq;

namespace GillionsGameSync;

public sealed partial class PluginConfiguration {
    // Record bodies and opaque legacy data are replaced as values. Copy the
    // containers and mutable roots that live observation code can still edit.
    internal PluginConfiguration Checkpoint() {
        var copy = (PluginConfiguration)MemberwiseClone();
        copy.OwnedCharacters = OwnedCharacters.ToDictionary(pair => pair.Key, pair => pair.Value.Checkpoint(), StringComparer.Ordinal);
        copy.CoverageGap = new() { Paused = CoverageGap.Paused, StartedAtUtc = CoverageGap.StartedAtUtc,
            ResumedAtUtc = CoverageGap.ResumedAtUtc, RejectedObservations = CoverageGap.RejectedObservations };
        copy.HuntBills = new() { SchemaVersion = HuntBills.SchemaVersion, LocalRetentionEnabled = HuntBills.LocalRetentionEnabled,
            CapacityReached = HuntBills.CapacityReached, Characters = HuntBills.Characters };
#if GILLIONS_TEST_BUILD || GILLIONS_PUBLIC_BUILD
        copy.SubmarineVoyages = new() { SchemaVersion = SubmarineVoyages.SchemaVersion,
            LocalRetentionEnabled = SubmarineVoyages.LocalRetentionEnabled, CommunityContributionEnabled = SubmarineVoyages.CommunityContributionEnabled,
            CapacityReached = SubmarineVoyages.CapacityReached, Current = SubmarineVoyages.Current?.ToList()!,
            Voyages = SubmarineVoyages.Voyages?.ToList()! };
        copy.PersonalSync = new() { SchemaVersion = PersonalSync.SchemaVersion, Prepared = PersonalSync.Prepared?.Select(p =>
            p is null ? null! : new PersonalPreparedSnapshot(p.OwnerKey, p.Resource, p.Nonce, p.Payload, p.PayloadHash) {
                Acknowledged = p.Acknowledged, Blocked = p.Blocked
            }).ToList()! };
#if GILLIONS_TEST_BUILD
        copy.DashboardFacts = new() { SchemaVersion = DashboardFacts.SchemaVersion, LocalRetentionEnabled = DashboardFacts.LocalRetentionEnabled,
            CapacityReached = DashboardFacts.CapacityReached, Characters = DashboardFacts.Characters, UnsupportedMembers = DashboardFacts.UnsupportedMembers };
#endif
#endif
        return copy;
    }
}

public sealed partial class OwnedCharacterState {
    internal OwnedCharacterState Checkpoint() {
        var copy = (OwnedCharacterState)MemberwiseClone();
        copy.LastPayloadHashes = new(LastPayloadHashes, StringComparer.Ordinal);
        copy.LastInventoryComponentHashes = new(LastInventoryComponentHashes, StringComparer.Ordinal);
        copy.RetainerGilBalances = new(RetainerGilBalances, StringComparer.Ordinal);
        copy.RetainerGilBaselinesNeedingRefresh = new(RetainerGilBaselinesNeedingRefresh, StringComparer.Ordinal);
        copy.InventoryContainers = new(InventoryContainers ?? new(), StringComparer.Ordinal);
        copy.PendingGilLedgerEvents = PendingGilLedgerEvents.ToList();
        copy.PendingRetainerGilReceipts = PendingRetainerGilReceipts.ToList();
        copy.PendingRetainerGilDeposits = PendingRetainerGilDeposits.ToList();
        copy.PendingRetainerSales = PendingRetainerSales.ToList();
        copy.RetainerState = new() { CharacterContentId = RetainerState.CharacterContentId,
            RosterObservation = RetainerState.RosterObservation,
            Retainers = RetainerState.Retainers.Select(RetainerVentureSnapshotPolicy.CloneProfile).ToList(),
            InventorySources = RetainerState.InventorySources.ToList(),
            PendingResultEvents = RetainerState.PendingResultEvents.ToList() };
        return copy;
    }
}
