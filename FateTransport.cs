#if GILLIONS_TEST_BUILD || GILLIONS_FATE_TESTS
using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;

namespace GillionsGameSync;

// Normalized admission, populated only by exact authenticated revision3 discovery.
internal sealed record FateAdmission(string SessionGeneration, string Origin, string Product,
    string Capability, int SchemaVersion, string CollectorSchema, FateSource AdmittedSource,
    string PolicyKey, int PolicyRevision, bool Granted, bool Revoked, DateTime ExpiresAt,
    bool ActiveAccount, bool EnabledDevice);
internal enum FateSendDisposition { Acknowledged, Retry, Suspended, Invalid }
internal static class FateTransportPolicy {
    internal static bool CanCommit(bool sessionCurrent,bool contextCurrent,CancellationToken feature) =>
        sessionCurrent && contextCurrent && !feature.IsCancellationRequested;
    internal static bool CanSend(FateAdmission? grant, FateSource source, string generation,
        bool currentPairing, bool contextCurrent, FatePrepared? prepared, long epoch, DateTime now) =>
        currentPairing && contextCurrent && FatePolicy.Compatible(source) && prepared is not null
        && prepared.Epoch == epoch && prepared.Body.Length is > 0 and <= FatePolicy.MaximumBytes
        && FatePolicy.Fresh(prepared.OldestObservation,now) && grant is not null
        && generation.Length > 0 && grant.SessionGeneration == generation
        && grant.Origin == "https://test.gillions.app" && grant.Product == "GillionsGameSyncTest"
        && grant.Capability == FatePolicy.Capability && grant.SchemaVersion == 1
        && grant.CollectorSchema == FatePolicy.Capability && grant.AdmittedSource == source
        && grant.PolicyKey == FatePolicy.AccountPolicy && grant.PolicyRevision == FatePolicy.PolicyRevision
        && grant.Granted && !grant.Revoked && grant.ActiveAccount && grant.EnabledDevice
        && FatePolicy.Utc(grant.ExpiresAt) && grant.ExpiresAt > now;
    internal static HttpRequestMessage? Request(FateAdmission? grant, FateSource source, string generation,
        bool paired, bool current, FatePrepared? batch, long epoch, DateTime now, string token) {
        if (!CanSend(grant,source,generation,paired,current,batch,epoch,now) || string.IsNullOrWhiteSpace(token)) return null;
        var request = new HttpRequestMessage(HttpMethod.Post,FatePolicy.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",token);
        request.Content = new ByteArrayContent(batch!.CopyBody());
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return request; // Use established no-redirect TLS HttpClient, not a new client/auth path.
    }
    internal static bool Receipt(string json, FatePrepared batch, int rowCount, DateTime now) {
        try {
            using var d=JsonDocument.Parse(json,new JsonDocumentOptions { MaxDepth=8 });
            var r=d.RootElement;
            string[] fields=["ok","schemaVersion","batchId","receivedAt","acceptedCount","duplicateCount","rejected","retryAfterSeconds"];
            if (!FateDiscovery.Shape(r,fields) || rowCount is <1 or >64 || r.GetProperty("ok").ValueKind!=JsonValueKind.True
                || r.GetProperty("schemaVersion").GetInt32()!=1 || !FateDiscovery.Uuid(r.GetProperty("batchId").GetString(),out var id)
                || id!=batch.BatchId || r.GetProperty("retryAfterSeconds").GetInt32()!=5) return false;
            var time=r.GetProperty("receivedAt").GetString();
            if (time is null || !time.EndsWith('Z') || !DateTime.TryParse(time,CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,out var received) || !FatePolicy.Fresh(received,now)) return false;
            int accepted=r.GetProperty("acceptedCount").GetInt32(), duplicate=r.GetProperty("duplicateCount").GetInt32();
            var rejected=r.GetProperty("rejected").EnumerateArray().ToArray();
            if (accepted<0 || duplicate<0 || accepted>rowCount || duplicate>rowCount
                || accepted+duplicate+rejected.Length!=rowCount) return false;
            var indices=rejected.Select(e=>e.GetProperty("index").GetInt32()).ToArray();
            return indices.Distinct().Count()==indices.Length && indices.All(i=>i>=0 && i<rowCount)
                && rejected.All(e=>FateDiscovery.Shape(e,["index","code"]) && e.TryGetProperty("code",out var c)
                    && c.ValueKind==JsonValueKind.String && FateDiscovery.RowCodes.Contains(c.GetString()));
        } catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or System.Collections.Generic.KeyNotFoundException or OverflowException) { return false; }
    }
    internal static FateSendDisposition Classify(int status, bool receipt) => status switch {
        200 or 201 when receipt => FateSendDisposition.Acknowledged,
        401 or 403 or 404 or 422 => FateSendDisposition.Suspended,
        429 or 503 => FateSendDisposition.Retry,
        >=500 => FateSendDisposition.Retry,
        _ => FateSendDisposition.Invalid
    };
    internal static FateSendDisposition Response(int? status,bool receipt,bool transportFailed=false) =>
        status is null || status is 200 or 201 && !receipt && transportFailed
            ? FateSendDisposition.Retry : Classify(status!.Value,receipt);
    internal static int RetrySeconds(int failures, double? retryAfter = null) {
        double backoff=5*Math.Pow(2,Math.Clamp(failures,0,7));
        // Never shorten a valid server delay to the exponential backoff cap.
        // The prepared observation will expire long before a long retry delay.
        double server=retryAfter is >=0 && double.IsFinite(retryAfter.Value) ? Math.Ceiling(retryAfter.Value) : 0;
        return (int)Math.Min(Math.Max(backoff,server),int.MaxValue);
    }
}
#endif
