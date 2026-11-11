#if GILLIONS_TEST_BUILD
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GillionsGameSync;

public sealed partial class Plugin {
    private readonly HuntSplitUpload huntSplitUpload = new();
    private readonly ResourceRetry huntSplitRetry = new();
    private bool huntSplitInFlight;
    private long huntPermissionEpoch;

    private HuntUploadRevision HuntRevision => new(huntLocal.SemanticRevision, huntLocal.CoverageRevision,
        huntLocal.CoverageEpoch, huntPermissionEpoch);
    private ulong huntKeyContentId;
    private string huntKey = "";

    private string ActiveHuntKey() {
        if (huntKeyContentId != activeRetainerCharacterContentId) {
            huntKeyContentId = activeRetainerCharacterContentId;
            huntKey = HuntBillRetentionPolicy.CharacterKey(huntKeyContentId);
        }
        return huntKey;
    }

    private bool HuntSplitEnabled => huntCoverageAccepted && PersonalEnabled("hunt_bills") && !inDuty;

    private void ResetHuntSplit() {
        huntPermissionEpoch++;
        huntSplitUpload.Clear();
        huntSplitRetry.Clear();
    }

    private void TickHuntSplit(DateTime now) {
        if (!HuntSplitEnabled || huntSplitInFlight) return;
        var revision = HuntRevision;
        var clock = Environment.TickCount64;
        if (!huntSplitUpload.Due(revision, clock) || !huntSplitRetry.Ready(clock, revision)) return;
        var key = ActiveHuntKey();
        var floor = FreshFrom("personalHunts");
        var sample = huntLocal.CurrentCoverage(key, now);
        if (sample is null || sample.ObservedAtUtc < floor) return;
        var preparation = huntSplitUpload.Prepare(revision, sample, now, clock, () => huntLocal.PrivateBills(key, floor));
        if (preparation is null) return;
        var permit = CapturePermit(SyncRequestMode.Personal);
        if (!personalCancellation.TryGetValue("hunt_bills", out var feature)) {
            feature = new CancellationTokenSource();
            personalCancellation.Add("hunt_bills", feature);
        }
        var token = feature.Token;
        huntSplitInFlight = true;
        _ = Task.Run(() => SendHuntSplitAsync(permit, preparation, key, token));
    }

    private bool HuntSplitCurrent(HuntSplitPreparation preparation, string key, DateTime now) =>
        HuntSplitEnabled && huntSplitUpload.IsCurrent(preparation, HuntRevision, huntLocal.CurrentCoverage(key, now), now);

    private async Task SendHuntSplitAsync(SyncRequestPermit permit, HuntSplitPreparation preparation, string key, CancellationToken feature) {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(permit.Cancellation, feature);
        linked.CancelAfter(TimeSpan.FromSeconds(30));
        try {
            using var request = SnapshotRequest("/api/game-sync/sync", permit, "hunt_bills", preparation.Nonce,
                Encoding.UTF8.GetBytes(preparation.Body));
            request.Headers.Add("X-Gillions-Personal-Contract", PersonalSyncPolicy.Contract);
            request.Headers.Add("X-Gillions-Personal-Resource", "hunt_bills");
            request.Headers.Add("X-Gillions-Personal-Capability", PersonalSyncPolicy.HuntCoverageCapability);
            Task<HttpResponseMessage>? pending = null;
            long dispatched = 0;
            await framework.RunOnFrameworkThread(() => {
                RequirePermit(permit);
                linked.Token.ThrowIfCancellationRequested();
                if (!HuntSplitCurrent(preparation, key, DateTime.UtcNow)) throw new SyncNotStartedException("Hunt data changed.");
                dispatched = Environment.TickCount64;
                pending = personalHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token);
            });
            using var response = await pending!;
            await SyncResponsePolicy.EnsureSuccessfulAsync(response, linked.Token);
            if ((int)response.StatusCode is not (200 or 201)
                || !PersonalSyncPolicy.Receipt(await SyncResponsePolicy.ReadAsync(response.Content, linked.Token)))
                throw new InvalidOperationException("Invalid Hunt receipt.");
            await framework.RunOnFrameworkThread(() => {
                if (!PermitIsCurrent(permit) || feature.IsCancellationRequested || !HuntSplitCurrent(preparation, key, DateTime.UtcNow)) return;
                huntSplitUpload.Acknowledge(preparation, dispatched);
                huntSplitRetry.Clear();
                personalStatus = "Hunt update received by Gillions.";
                RecordDiagnostic($"Hunt upload accepted. {request.Content!.Headers.ContentLength:N0} bytes.");
            });
        } catch (Exception error) when (error is not SyncNotStartedException) {
            if (!disposed) await framework.RunOnFrameworkThread(() => {
                if (!PermitIsCurrent(permit) || feature.IsCancellationRequested || !HuntSplitEnabled) return;
                huntSplitRetry.Failed(Environment.TickCount64, preparation.Key.Revision, error);
                personalStatus = error is GillionsHttpException refused
                    ? $"Gillions paused Hunt uploads. HTTP {refused.Status} ({refused.Code})."
                    : "Hunt upload will retry.";
            });
        } catch (SyncNotStartedException) {
            // The next framework tick prepares the changed observation.
        } finally {
            if (!disposed) await framework.RunOnFrameworkThread(() => huntSplitInFlight = false);
        }
    }
}
#endif
