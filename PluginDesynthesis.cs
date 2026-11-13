#if GILLIONS_TEST_BUILD
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace GillionsGameSync;

public sealed partial class Plugin {
    private readonly DesynthesisQueue desynthesisQueue = new();
    private readonly DesynthesisResultSource desynthesisSource;
    private readonly ResourceRetry desynthesisRetry = new();
    private CancellationTokenSource? desynthesisLifetime;
    private CancellationToken? desynthesisFlight;
    private bool desynthesisSupported;
    private long nextDesynthesisSend;

    private bool DesynthesisAllowed => !disposed && clientState.IsLoggedIn && activeOwnedState is not null
        && playerState.IsLoaded && playerState.ContentId != 0 && playerState.ContentId == activeRetainerCharacterContentId
        && SitePermissionOrigin && HasPairedSession
        && permissionAuthority.Allows(DesynthesisResults.Permission, false, Environment.TickCount64);

    private void ResetDesynthesis() {
        desynthesisQueue.SetEnabled(false);
        var lifetime = desynthesisLifetime;
        desynthesisLifetime = null;
        try { lifetime?.Cancel(); } finally { lifetime?.Dispose(); }
        desynthesisRetry.Clear();
    }

    private bool AuthorizeDesynthesisCapture() {
        if (!DesynthesisAllowed) {
            ResetDesynthesis();
            return false;
        }
        desynthesisQueue.SetEnabled(true);
        return true;
    }

    private void TickDesynthesis() {
        if (!AuthorizeDesynthesisCapture() || !desynthesisSupported || desynthesisFlight is { IsCancellationRequested: false }) return;
        var clock = Environment.TickCount64;
        if (clock < nextDesynthesisSend || desynthesisQueue.Prepare() is not { } batch || !desynthesisRetry.Ready(clock, batch.Nonce)) return;
        var permit = CapturePermit(SyncRequestMode.Personal);
        if (!DesynthesisAllowed || !desynthesisSupported || !ReferenceEquals(batch, desynthesisQueue.Prepare())) return;
        desynthesisLifetime ??= new();
        var feature = desynthesisLifetime.Token;
        desynthesisFlight = feature;
        nextDesynthesisSend = clock + 5000;
        _ = Task.Run(() => SendDesynthesisAsync(permit, batch, feature));
    }

    private async Task SendDesynthesisAsync(SyncRequestPermit permit, DesynthesisBatch batch, CancellationToken feature) {
        try {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(permit.Cancellation, feature);
            deadline.CancelAfter(TimeSpan.FromSeconds(20));
            var token = deadline.Token;
            using var request = SnapshotRequest("/api/game-sync/sync", permit, DesynthesisResults.Resource, batch.Nonce, batch.Payload);
            request.Headers.Add("X-Gillions-Personal-Contract", PersonalSyncPolicy.Contract);
            request.Headers.Add("X-Gillions-Personal-Resource", DesynthesisResults.Resource);
            request.Headers.Add("X-Gillions-Personal-Capability", DesynthesisResults.Capability);
            Task<HttpResponseMessage>? pending = null;
            await framework.RunOnFrameworkThread(() => {
                RequirePermit(permit);
                token.ThrowIfCancellationRequested();
                if (!DesynthesisAllowed || !desynthesisSupported || !ReferenceEquals(batch, desynthesisQueue.Prepare()))
                    throw new OperationCanceledException();
                pending = personalHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            });
            using var response = await pending!;
            if (await ReadPersonalUploadFailureAsync(response, token) is { } refusal) throw refusal;
            if (!await PersonalSyncPolicy.ReadReceiptAsync((int)response.StatusCode, () => SyncResponsePolicy.ReadAsync(response.Content, token)))
                throw new InvalidOperationException("Invalid desynthesis receipt.");
            await framework.RunOnFrameworkThread(() => {
                if (feature.IsCancellationRequested || !PermitIsCurrent(permit) || !DesynthesisAllowed) return;
                desynthesisQueue.Acknowledge(batch);
                desynthesisRetry.Clear();
            });
        } catch (Exception error) {
            if (!disposed) await framework.RunOnFrameworkThread(() => {
                if (!feature.IsCancellationRequested && PermitIsCurrent(permit) && DesynthesisAllowed)
                    desynthesisRetry.Failed(Environment.TickCount64, batch.Nonce, error);
            });
        } finally {
            if (!disposed) await framework.RunOnFrameworkThread(() => {
                if (desynthesisFlight == feature) desynthesisFlight = null;
            });
        }
    }
}
#endif
