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
    private CharacterAppearanceCollector appearanceCollector=null!;
    private readonly AppearanceQueue appearanceQueue=new();
    private readonly ResourceRetry appearanceRetry = new();
    private CancellationTokenSource appearanceCancellation=new();
    private ConsentStatus? consentStatus;
    private string? appearanceGrant;
    private long appearanceCharacterId;
    private const int AppearanceSampleSeconds = 5;
    private CancellationToken? appearanceFlight;
    private string? appearanceReadFailure;
    private DateTime appearanceLease, appearanceNextRead, appearanceNextSend;

    private void ClearAppearance() {
        appearanceCancellation.Cancel();
        appearanceQueue.Clear();
    }

    private void ResetAppearance() {
        ClearAppearance();
        appearanceReadFailure = null;
        consentStatus = null;
        appearanceGrant = null;
        appearanceCharacterId = 0;
        appearanceLease = DateTime.MinValue;
        appearanceRetry.Clear();
    }

    private bool AppearanceAuthorized(DateTime now) => HasPairedSession && activeOwnedState is not null
        && appearanceGrant is not null && now < appearanceLease;

    private void ApplySharingStatus(string json, SyncRequestPermit permit) {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        var character = permissionAuthority.Enrollment?.CharacterId;
        var status = ConsentStatus.Read(doc.RootElement, permit.Origin);
        consentStatus = status?.CharacterId == character ? status : null;
        var descriptor = AppearanceGrant.Read(doc.RootElement);
        var state = consentStatus?.Categories.FirstOrDefault(category => category.Key == "appearance")?.State;
        if (state == "off" || descriptor?.CharacterId != character || !permissionAuthority.Fresh(Environment.TickCount64))
            descriptor = null;
        var nextCharacterId = descriptor?.CharacterId ?? 0;
        if (descriptor?.Generation != appearanceGrant || nextCharacterId != appearanceCharacterId) {
            // Gillions deletes the old look on Off. Forget its receipt as
            // well as pending work so the next grant sends a fresh snapshot.
            ClearAppearance();
            appearanceRetry.Clear();
            appearanceNextRead = appearanceNextSend = DateTime.MinValue;
        }
        appearanceGrant = descriptor?.Generation;
        appearanceCharacterId = nextCharacterId;
        appearanceLease = DateTime.UtcNow.AddSeconds(25);
    }

    private void DrawSharingStatus() {
        var status = consentStatus;
        if (status is null || DateTime.UtcNow >= appearanceLease) {
            Label("Sharing status is unavailable.");
            return;
        }
        Dalamud.Bindings.ImGui.ImGui.PushTextWrapPos(0);
        foreach (var category in status.Categories)
            Dalamud.Bindings.ImGui.ImGui.TextUnformatted($"{category.Label}: {category.DisplayState}");
        Dalamud.Bindings.ImGui.ImGui.PopTextWrapPos();
        Link("Change on gillions.app", status.SettingsUrl);
    }

    private void WithholdAppearance(string reason) {
        ClearAppearance();
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
            if (appearanceGrant is not null) { appearanceGrant = null; ClearAppearance(); }
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
                return;
            }
        }
        if (TakeAppearanceUpload(now) is { } p) {
            if (appearanceCancellation.IsCancellationRequested) { appearanceCancellation.Dispose(); appearanceCancellation = new(); }
            var permit = CapturePermit(SyncRequestMode.Personal);
            var grant = appearanceGrant!;
            var cancellation = appearanceCancellation.Token;
            appearanceFlight = cancellation;
            _ = Task.Run(() => SendAppearance(permit, p, grant, cancellation));
        }
    }

    private static void AppearanceHeaders(HttpRequestMessage r){r.Headers.Add("X-Gillions-Personal-Contract",AppearancePolicy.Contract);r.Headers.Add("X-Gillions-Personal-Resource",AppearancePolicy.Resource);r.Headers.Add("X-Gillions-Personal-Capability",AppearancePolicy.Capability);}
    private async Task SendAppearance(SyncRequestPermit permit,AppearancePreparation p,string grant,CancellationToken featureToken){
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(permit.Cancellation,featureToken);linked.CancelAfter(TimeSpan.FromSeconds(20));var token=linked.Token;
        try{
            using var request=SnapshotRequest("/api/game-sync/sync",permit,AppearancePolicy.Resource,p.Nonce,Encoding.UTF8.GetBytes(p.Payload));AppearanceHeaders(request);request.Headers.Add("X-Gillions-Appearance-Grant",grant);
            if(request.Content!.Headers.ContentLength>20480)throw new InvalidOperationException();
            Task<HttpResponseMessage>? pending=null;
            await framework.RunOnFrameworkThread(()=>{RequirePermit(permit);token.ThrowIfCancellationRequested();if(!AppearanceAuthorized(DateTime.UtcNow)||appearanceGrant!=grant||!appearanceCollector.Available(permit.ContentId))throw new OperationCanceledException();pending=personalHttp.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);});
            using var response = await pending!;
            await SyncResponsePolicy.EnsureSuccessfulAsync(response, token);
            if ((int)response.StatusCode is not (200 or 201)) throw new InvalidOperationException("Invalid appearance receipt.");
            using var d = JsonDocument.Parse(await SyncResponsePolicy.ReadAsync(response.Content, token));
            var receipt = d.RootElement;
            if (!receipt.GetProperty("ok").GetBoolean() || receipt.GetProperty("nonce").GetString() != p.Nonce
                || receipt.GetProperty("contentHash").GetString() != p.Hash) throw new InvalidOperationException("Invalid appearance receipt.");
            await framework.RunOnFrameworkThread(() => {
                if (!PermitIsCurrent(permit) || token.IsCancellationRequested || appearanceGrant != grant) return;
                appearanceRetry.Clear();
                appearanceQueue.Accepted(p);
            });
        } catch (Exception error) {
            if (!disposed) await framework.RunOnFrameworkThread(() => {
                if (PermitIsCurrent(permit) && !featureToken.IsCancellationRequested && appearanceGrant == grant)
                    appearanceRetry.Failed(Environment.TickCount64, p.Hash, error);
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
