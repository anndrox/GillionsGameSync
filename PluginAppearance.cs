#if GILLIONS_TEST_BUILD
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GillionsGameSync;

public sealed partial class Plugin {
    private readonly ICharacterAppearanceSource appearanceCollector;
    private readonly AppearanceQueue appearanceQueue=new();
    private readonly ResourceRetry appearanceRetry = new();
    private CancellationTokenSource appearanceCancellation=new();
    private ConsentStatus? consentStatus;
    private string? appearanceGrant;
    private (ulong ContentId, AppearanceGrant Grant)? appearanceReceiptScope;
    private const int AppearanceSampleSeconds = 5;
    private CancellationToken? appearanceFlight;
    private string? appearanceReadFailure;
    private string? appearanceSendFailure;
    private DateTime? appearanceLastSentUtc;
    private volatile string appearanceSharingStatus = "Waiting for Gillions to confirm sharing.";
    private DateTime appearanceLease, appearanceNextRead, appearanceNextSend;

    private void CancelAppearanceUpload() {
        appearanceCancellation.Cancel();
        appearanceQueue.DiscardPending();
    }

    private void PauseAppearance() {
        CancelAppearanceUpload();
        appearanceSendFailure = null;
        consentStatus = null;
        appearanceGrant = null;
        appearanceLease = DateTime.MinValue;
    }

    private void ResetAppearance(ulong nextContentId, bool samePairing) {
        PauseAppearance();
        appearanceReadFailure = null;
        appearanceRetry.Clear();
        // Player state can temporarily lose its content ID during a zone load.
        if (samePairing && (nextContentId == 0 || nextContentId == appearanceReceiptScope?.ContentId)) return;
        appearanceReceiptScope = null;
        appearanceQueue.Clear();
        appearanceLastSentUtc = null;
    }

    private bool AppearanceAuthorized(DateTime now) => HasPairedSession && activeOwnedState is not null
        && appearanceGrant is not null && now < appearanceLease;

    private void ApplySharingStatus(string json, SyncRequestPermit permit) {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        var character = permissionAuthority.Enrollment?.CharacterId;
        var status = ConsentStatus.Read(doc.RootElement, permit.Origin);
        consentStatus = status?.CharacterId == character ? status : null;
        var descriptor = AppearanceGrant.Read(doc.RootElement, out var disabledCharacterId);
        var state = consentStatus?.Categories.FirstOrDefault(category => category.Key == "appearance")?.State;
        var fresh = permissionAuthority.Fresh(Environment.TickCount64);
        var off = fresh && character.HasValue && (state == "off" || disabledCharacterId == character);
        if (off || descriptor?.CharacterId != character || !fresh)
            descriptor = null;
        (ulong ContentId, AppearanceGrant Grant)? nextScope = descriptor is null ? null : (permit.ContentId, descriptor);
        if (off || (nextScope.HasValue && nextScope != appearanceReceiptScope)) {
            CancelAppearanceUpload();
            appearanceQueue.Clear();
            appearanceReceiptScope = nextScope;
            appearanceLastSentUtc = null;
            appearanceSendFailure = null;
            appearanceRetry.Clear();
            appearanceNextRead = appearanceNextSend = DateTime.MinValue;
        } else if (descriptor is null && appearanceGrant is not null) {
            CancelAppearanceUpload();
        }
        appearanceGrant = descriptor?.Generation;
        appearanceLease = DateTime.UtcNow.AddSeconds(25);
    }

    private void DrawSharingStatus() {
        var status = consentStatus;
        if (status is null || DateTime.UtcNow >= appearanceLease) {
            Label("Sharing status is unavailable.");
            Label("Character appearance");
            Label(appearanceSharingStatus);
            Label(DesynthesisResults.Label);
            Label(DesynthesisResults.Description);
            return;
        }
        Dalamud.Bindings.ImGui.ImGui.PushTextWrapPos(0);
        foreach (var category in status.Categories) {
            if (category.Key == DesynthesisResults.Permission) continue;
            Dalamud.Bindings.ImGui.ImGui.TextUnformatted($"{category.Label}: {category.DisplayState}");
            if (category.Key == "appearance") Label(appearanceSharingStatus);
        }
        var desynthesis = status.Categories.FirstOrDefault(category => category.Key == DesynthesisResults.Permission);
        Label(desynthesis is null ? DesynthesisResults.Label
            : $"{DesynthesisResults.Label}: {(desynthesis.State == "not-set" && DesynthesisAllowed ? "On" : desynthesis.DisplayState)}");
        Label(DesynthesisResults.Description);
        Dalamud.Bindings.ImGui.ImGui.PopTextWrapPos();
        Link("Change on gillions.app", status.SettingsUrl);
    }

    private string AppearanceStatus(DateTime now) {
        if (!HasPairedSession) return "Connect Game Sync to Gillions.";
        if (!clientState.IsLoggedIn || activeOwnedState is null) return "Waiting for your character to load.";
        if (publicConnectionFailure is SiteRefusalPolicy.CharacterNotLinked or SiteRefusalPolicy.CharacterMismatch)
            return "Verify this character on gillions.app.";
        if (now < appearanceLease && consentStatus?.Categories.FirstOrDefault(category => category.Key == "appearance")?.State == "off")
            return "Appearance sharing is off on gillions.app.";
        if (!AppearanceAuthorized(now)) return "Waiting for Gillions to confirm sharing.";
        if (appearanceReadFailure is { } readFailure) return readFailure;
        if (appearanceQueue.Pending is not null && appearanceSendFailure is { } sendFailure) return sendFailure;
        if (appearanceLastSentUtc is { } sent) return $"Last sent {sent.ToLocalTime():T}";
        return appearanceFlight is { IsCancellationRequested: false } ? "Sending your current look." : "Waiting to send your current look.";
    }

    private void WithholdAppearance(string reason) {
        CancelAppearanceUpload();
        if (reason == appearanceReadFailure) return;
        appearanceReadFailure = reason;
        RecordDiagnostic($"Appearance paused. {reason}");
    }

    private bool AppearanceSampleDue(DateTime now) {
        if (now < appearanceNextRead) return false;
        appearanceNextRead = now.AddSeconds(AppearanceSampleSeconds);
        return true;
    }

    private AppearancePreparation? TakeAppearanceUpload(DateTime now) {
        if (appearanceFlight is { IsCancellationRequested: false } || now < appearanceNextSend
            || appearanceQueue.Pending is not { } pending || !appearanceRetry.Ready(Environment.TickCount64, pending.Hash)) return null;
        appearanceNextSend = now.AddSeconds(AppearanceSampleSeconds);
        return pending;
    }

    private void TickAppearance(DateTime now) {
        if (!AppearanceAuthorized(now)) {
            if (appearanceGrant is not null) PauseAppearance();
            return;
        }
        if (AppearanceSampleDue(now)) {
            try {
                var (snapshot, reason) = appearanceCollector.Read(activeRetainerCharacterContentId, now);
                if (snapshot is null) {
                    WithholdAppearance(reason ?? "Character appearance could not be read.");
                    return;
                }
                appearanceQueue.Observe(snapshot);
                appearanceReadFailure = null;
            } catch (Exception) {
                WithholdAppearance("Character appearance could not be read.");
                throw;
            }
        }
        if (TakeAppearanceUpload(now) is { } p) {
            var permit = CapturePermit(SyncRequestMode.Personal);
            if (!AppearanceAuthorized(now) || appearanceGrant is not { } grant || !ReferenceEquals(appearanceQueue.Pending, p)) return;
            if (appearanceCancellation.IsCancellationRequested) { appearanceCancellation.Dispose(); appearanceCancellation = new(); }
            var cancellation = appearanceCancellation.Token;
            appearanceFlight = cancellation;
            _ = Task.Run(() => SendAppearance(permit, p, grant, cancellation));
        }
    }

    private void AcceptAppearance(AppearancePreparation upload, DateTime now) {
        appearanceRetry.Clear();
        appearanceQueue.Accepted(upload);
        appearanceLastSentUtc = now;
        appearanceSendFailure = null;
    }

    private static void AppearanceHeaders(HttpRequestMessage r){r.Headers.Add("X-Gillions-Personal-Contract",AppearancePolicy.Contract);r.Headers.Add("X-Gillions-Personal-Resource",AppearancePolicy.Resource);r.Headers.Add("X-Gillions-Personal-Capability",AppearancePolicy.Capability);}
    private async Task SendAppearance(SyncRequestPermit permit,AppearancePreparation p,string grant,CancellationToken featureToken){
        try{
            using var linked=CancellationTokenSource.CreateLinkedTokenSource(permit.Cancellation,featureToken);linked.CancelAfter(TimeSpan.FromSeconds(20));var token=linked.Token;
            using var request=SnapshotRequest("/api/game-sync/sync",permit,AppearancePolicy.Resource,p.Nonce,Encoding.UTF8.GetBytes(p.Payload));AppearanceHeaders(request);request.Headers.Add("X-Gillions-Appearance-Grant",grant);
            if(request.Content!.Headers.ContentLength>20480)throw new InvalidOperationException();
            Task<HttpResponseMessage>? pending=null;
            await framework.RunOnFrameworkThread(()=>{RequirePermit(permit);token.ThrowIfCancellationRequested();if(!AppearanceAuthorized(DateTime.UtcNow)||appearanceGrant!=grant||!appearanceCollector.Available(permit.ContentId))throw new OperationCanceledException();pending=personalHttp.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);});
            using var response = await pending!;
            if (await ReadPersonalUploadFailureAsync(response, token) is { } refusal) throw refusal;
            if ((int)response.StatusCode is not (200 or 201)) throw new InvalidOperationException("Invalid appearance receipt.");
            using var d = JsonDocument.Parse(await SyncResponsePolicy.ReadAsync(response.Content, token));
            var receipt = d.RootElement;
            if (!receipt.GetProperty("ok").GetBoolean() || receipt.GetProperty("nonce").GetString() != p.Nonce
                || receipt.GetProperty("contentHash").GetString() != p.Hash) throw new InvalidOperationException("Invalid appearance receipt.");
            await framework.RunOnFrameworkThread(() => {
                if (!PermitIsCurrent(permit) || token.IsCancellationRequested || appearanceGrant != grant) return;
                AcceptAppearance(p, DateTime.UtcNow);
            });
        } catch (Exception error) {
            if (!disposed) await framework.RunOnFrameworkThread(() => {
                if (!PermitIsCurrent(permit) || featureToken.IsCancellationRequested || appearanceGrant != grant) return;
                appearanceRetry.Failed(Environment.TickCount64, p.Hash, error);
                appearanceSendFailure = error switch {
                    GillionsHttpException refusal when refusal.Code is SiteRefusalPolicy.CharacterNotLinked or SiteRefusalPolicy.CharacterMismatch
                        => "Verify this character on gillions.app.",
                    GillionsHttpException { Status: 401 or 403 } => "Gillions declined appearance sharing. Check your sharing settings on gillions.app.",
                    _ => "Gillions did not accept the look yet. Game Sync will retry.",
                };
                if (error is not (OperationCanceledException or HttpRequestException or GillionsHttpException))
                    backgroundObservation.Report("character appearance upload", error);
            });
        }
        finally {
            if (!disposed) await framework.RunOnFrameworkThread(() => {
                if (appearanceFlight == featureToken) appearanceFlight = null;
            });
        }
    }
}
#endif
