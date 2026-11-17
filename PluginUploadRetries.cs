using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace GillionsGameSync;

public sealed partial class Plugin {
    private readonly Dictionary<string, ResourceRetry> ordinaryRetries = new(StringComparer.Ordinal);

    // parks, when given, records a section that waits out an earlier refusal or that Gillions refuses now.
    private Task<bool> OrdinaryReadyAsync(SyncRequestPermit permit, string resource, string revision, Dictionary<string, SyncSectionPark>? parks = null) =>
        framework.RunOnFrameworkThread(() => {
            RequirePermit(permit);
            if (!ordinaryRetries.TryGetValue(resource, out var retry)) return true;
            var wait = retry.Remaining(Environment.TickCount64, revision);
            if (wait == TimeSpan.Zero) return true;
            ScheduleOrdinaryRetry(resource, wait);
            if (parks is not null) parks[resource] = new(null, "", Waiting: true);
            return false;
        });

    private async Task<bool> OrdinaryResponseAcceptedAsync(HttpResponseMessage response, SyncRequestPermit permit, string resource, string revision,
        Dictionary<string, SyncSectionPark>? parks = null) {
        if (response.IsSuccessStatusCode) {
            await framework.RunOnFrameworkThread(() => {
                RequirePermit(permit);
                ordinaryRetries.Remove(resource);
            });
            return true;
        }
        var error = await SyncResponsePolicy.ReadFailureAsync(response, permit.Cancellation);
        if (IsAccountAccessBlocked(error.Code) || CharacterRefusalBackoff.Counts(error.Code)) throw error;
        await framework.RunOnFrameworkThread(() => {
            RequirePermit(permit);
            if (!ordinaryRetries.TryGetValue(resource, out var retry)) ordinaryRetries.Add(resource, retry = new());
            var clock = Environment.TickCount64;
            retry.Failed(clock, revision, error);
            ScheduleOrdinaryRetry(resource, retry.Remaining(clock, revision));
            RecordDiagnostic($"Gillions paused {resource}. HTTP {error.Status} ({error.Code}).");
            if (parks is not null) parks[resource] = new(error.Status, error.Code, Waiting: false);
        });
        return false;
    }

    private void ScheduleOrdinaryRetry(string resource, TimeSpan wait) {
        var now = DateTime.UtcNow;
        if (resource == "inventory") inventoryUploads.Requeue(now, wait);
        else if (resource == RetainerClientPolicy.ResourceType) nextRetainerUploadUtc = now.Add(wait);
        else if (resource == "gil_ledger") nextGilLedgerUploadUtc = now.Add(wait);
    }
}
