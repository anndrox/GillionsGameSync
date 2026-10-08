#if GILLIONS_TEST_BUILD || GILLIONS_PUBLIC_BUILD || GILLIONS_FATE_TESTS
using System;
using System.Threading;

namespace GillionsGameSync;

// Framework-owned managed scheduling. No game getters, credentials, persistence
// or contributor journal. Monotonic deadlines cannot be extended by clock rollback.
internal sealed class FateSenderState : IDisposable {
    internal FateGrant? Grant { get; private set; }
    internal string Status { get; private set; }="Site policy unavailable; no contributions.";
    internal DateTime? LastAcknowledged { get; private set; }
    internal int LastAccepted { get; private set; }
    internal double LastRequestMilliseconds { get; set; }
    internal string Binding { get; private set; }="";
    internal long Epoch { get; private set; }
    internal CancellationToken Cancellation => lifetime.Token;
    internal bool DiscoveryBusy { get; private set; }
    internal bool UploadBusy { get; private set; }
    private CancellationTokenSource lifetime=new();
    private FatePrepared? retryBatch;
    private Guid? inFlightId;
    private Guid? acknowledged;
    private long nextDiscovery, nextSend, expires;
    private DateTime consentAfter=DateTime.MaxValue;
    private int failures, discoveryFailures;
    private bool suspended, correctionRequired, disposed;

    internal void Bind(string binding,long epoch) {
        if(Binding==binding && Epoch==epoch) return;
        Cancel(); Binding=binding; Epoch=epoch; Grant=null; suspended=false; correctionRequired=false;
        consentAfter=DateTime.MaxValue; acknowledged=null; LastAcknowledged=null; LastAccepted=0;
        Status="Site policy unavailable; session/context changed.";
        // Preserve rate/discovery deadlines across context churn.
    }
    private void Cancel() {
        lifetime.Cancel(); lifetime.Dispose(); lifetime=new(); retryBatch=null; inFlightId=null;
        // Workers own the busy flags until their finally returns. Cancelling a
        // request never permits an overlapping replacement on the same lane.
    }
    internal bool Discover(long clock) {
        if(disposed || Binding.Length==0 || DiscoveryBusy || clock<nextDiscovery) return false;
        DiscoveryBusy=true; nextDiscovery=clock+10000; return true;
    }
    internal void DiscoveryFinished()=>DiscoveryBusy=false;
    internal void UploadFinished() { UploadBusy=false; inFlightId=null; }
    internal void Install(FateGrant? grant, DateTime now,long clock) {
        bool continuity=!suspended && Grant is {Admission.Granted:true} prior && grant is {Admission.Granted:true}
            && prior.PolicyGeneration==grant.PolicyGeneration && prior.PairedAt==grant.PairedAt
            && prior.Admission.AdmittedSource==grant.Admission.AdmittedSource;
        if(grant is null || !grant.Admission.Granted || !continuity) {
            Cancel(); consentAfter=grant?.Admission.Granted==true ? now : DateTime.MaxValue;
        }
        Grant=grant; suspended=false;
        if(grant is not null) discoveryFailures=0;
        expires=grant is null ? 0 : clock+(long)Math.Min(30000,Math.Max(0,(grant.Admission.ExpiresAt-now).TotalMilliseconds));
        Status=grant is null ? "Malformed/unavailable discovery; no contributions."
            : grant.Admission.Granted ? "Site policy ON; awaiting fresh positive observations."
            : $"Site policy {(grant.PolicyEnabled ? "ON" : "OFF")}; {grant.Reason}; no contributions.";
        if(correctionRequired) Status="FATE shape/receipt requires correction; no blind retry. Other sync unchanged.";
    }
    internal void DiscoveryFailed(long clock,double? retry=null) {
        Install(null,DateTime.UtcNow,clock);
        nextDiscovery=Math.Max(nextDiscovery,clock+1000L*FateTransportPolicy.RetrySeconds(discoveryFailures++,retry));
    }
    internal void Maintain(DateTime now,long clock) {
        if(Grant is not null && (clock>=expires || Grant.Admission.ExpiresAt<=now)) {
            Cancel(); Grant=null; consentAfter=DateTime.MaxValue;
            Status="Site discovery expired; unsent observations discarded.";
        }
        if(retryBatch is not null && !FatePolicy.Fresh(retryBatch.OldestObservation,now)) retryBatch=null;
    }
    internal bool Authorized(DateTime now,long clock)=>!disposed && !suspended && !correctionRequired && Binding.Length>0
        && Grant is {Admission.Granted:true} && now<Grant.Admission.ExpiresAt && clock<expires;
    internal FatePrepared? Take(FatePrepared? latest,FateSource source,string generation,DateTime now,long clock) {
        Maintain(now,clock);
        if(UploadBusy || clock<nextSend || !Authorized(now,clock)) return null;
        var batch=retryBatch??latest;
        if(batch is null || batch.BatchId==acknowledged || batch.OldestObservation<=consentAfter
            || !FateTransportPolicy.CanSend(Grant!.Admission,source,generation,true,true,batch,Epoch,now)) return null;
        retryBatch=batch; inFlightId=batch.BatchId; UploadBusy=true; nextSend=clock+5000;
        Status="Sending bounded public FATE observations."; return batch;
    }
    internal void Complete(FatePrepared batch,FateSendDisposition outcome,DateTime now,long clock,int accepted=0,double? retry=null) {
        // Payload freshness decides retry eligibility, not ownership of a
        // response already in flight. Its rate/suspension disposition survives
        // observation expiry; actual cancellation clears this identity.
        if(inFlightId!=batch.BatchId) return;
        if(outcome==FateSendDisposition.Acknowledged) {
            acknowledged=batch.BatchId; retryBatch=null; failures=0; LastAcknowledged=now; LastAccepted=accepted;
            Status=$"Acknowledged FATE batch; {accepted} accepted. Renewal uses fresh observations, not retries.";
        } else if(outcome==FateSendDisposition.Retry) {
            nextSend=Math.Max(nextSend,clock+1000L*FateTransportPolicy.RetrySeconds(failures++,retry));
            Status="FATE transport unavailable/rate-limited; bounded retry, original bytes/time retained.";
        } else {
            Cancel(); suspended=true; correctionRequired=outcome==FateSendDisposition.Invalid; consentAfter=DateTime.MaxValue;
            Status="FATE admission/receipt rejected; suspended until fresh discovery. Other sync unchanged.";
        }
    }
    public void Dispose() { if(disposed) return; disposed=true; lifetime.Cancel(); lifetime.Dispose(); retryBatch=null; inFlightId=null; Grant=null; }
}
#endif
