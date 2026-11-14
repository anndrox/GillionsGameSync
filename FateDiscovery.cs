#if GILLIONS_TEST_BUILD || GILLIONS_PUBLIC_BUILD || GILLIONS_FATE_TESTS
using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GillionsGameSync;

internal sealed record FateGrant(FateAdmission Admission, Guid? PolicyGeneration, DateTime PairedAt,
    DateTime IssuedAt, bool PolicyEnabled, string? Reason);
internal static class FateDiscovery {
    internal const string Origin="https://test.gillions.app", Path="/api/game-sync/fates/capability";
    internal static readonly string[] RowCodes=["FATE_DEFINITION_UNSUPPORTED","FATE_TERRITORY_UNSUPPORTED",
        "FATE_REFERENCE_CONTEXT_MISMATCH","FATE_WORLD_UNSUPPORTED","FATE_TERMINAL_UNSUPPORTED","AMBIGUOUS_OBSERVATION"];
    internal static readonly string[] ErrorCodes=["INVALID_JSON","INVALID_FATE_SHAPE","INVALID_FATE_CLOCK",
        "INVALID_FATE_IDENTITY","INVALID_FATE_INSTANCE","INVALID_FATE_BATCH","FATE_CLOCK_OUT_OF_RANGE",
        "DEVICE_INVALID","ACCOUNT_UNAVAILABLE","TESTING_PRODUCT_REQUIRED","FATE_POLICY_REQUIRED",
        "HTTPS_ORIGIN_REQUIRED","METHOD_NOT_ALLOWED","FATE_BATCH_COLLISION","PAYLOAD_TOO_LARGE",
        "UNSUPPORTED_MEDIA_TYPE","FATE_SCHEMA_UNSUPPORTED","FATE_SOURCE_UNSUPPORTED","FATE_STATE_UNSUPPORTED",
        "FATE_RATE_LIMITED","FATE_INTAKE_UNAVAILABLE"];
    internal static bool Shape(JsonElement value, string[] keys) => value.ValueKind==JsonValueKind.Object
        && value.EnumerateObject().Select(p=>p.Name).Order().SequenceEqual(keys.Order());
    private static bool Utc(string? text, out DateTime value) {
        value=default;
        return text is not null && Regex.IsMatch(text,@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d{1,7})?Z$",RegexOptions.CultureInvariant)
            && DateTime.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out value) && FatePolicy.Utc(value);
    }
    internal static bool Uuid(string? text,out Guid id) {
        id=default;
        return text is not null && Regex.IsMatch(text,@"^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$",RegexOptions.CultureInvariant)
            && Guid.TryParseExact(text,"D",out id);
    }
    internal static FateGrant? Parse(string json, string generation, string deviceId, FateSource source, DateTime now, string origin = Origin) {
        if (!NativeProduct.TransportOrigin(origin)) return null;
        try {
            using var d=JsonDocument.Parse(json,new JsonDocumentOptions {MaxDepth=8}); var root=d.RootElement;
            if(!Shape(root,["ok","fateContribution"]) || root.GetProperty("ok").ValueKind!=JsonValueKind.True
                || !Uuid(deviceId,out var device) || string.IsNullOrEmpty(generation)) return null;
            var g=root.GetProperty("fateContribution");
            if(!Shape(g,["capability","schemaVersion","collectorSchema","coverage","endpoint","maxPayloadBytes",
                "maxObservations","acceptedClientProduct","acceptedSource","referenceCompatibilityEpoch","authorized",
                "reason","deviceBinding","policy","issuedAt","expiresAt","retryAfterSeconds"])) return null;
            string? S(string key)=>g.GetProperty(key).GetString();
            if(S("capability")!=FatePolicy.Capability || g.GetProperty("schemaVersion").GetInt32()!=1
                || S("collectorSchema")!=FatePolicy.Capability || S("coverage")!="positive_only"
                || S("endpoint")!="/api/game-sync/fates/contribute" || g.GetProperty("maxPayloadBytes").GetInt32()!=FatePolicy.MaximumBytes
                || g.GetProperty("maxObservations").GetInt32()!=FatePolicy.MaximumRows || S("acceptedClientProduct")!=NativeProduct.Name
                || S("referenceCompatibilityEpoch")!="fate-reference:"+source.ReferenceGameVersion
                || g.GetProperty("retryAfterSeconds").GetInt32()!=5) return null;
            var s=g.GetProperty("acceptedSource");
            if(!Shape(s,["family","collectorVersion","gameVersion","dalamudApiLevel","dalamudVersion","dalamudRevision",
                "clientStructsVersion","clientStructsRevision","luminaVersion","excelVersion","referenceGameVersion"])) return null;
            var admitted=s.Deserialize<FateSource>(FatePolicy.Json);
            if(!FatePolicy.Compatible(admitted)) return null;
            var b=g.GetProperty("deviceBinding"); var p=g.GetProperty("policy");
            if(!Shape(b,["deviceId","pairedAt"]) || !Uuid(b.GetProperty("deviceId").GetString(),out var bound) || device!=bound
                || !Utc(b.GetProperty("pairedAt").GetString(),out var paired)
                || !Shape(p,["name","revision","enabled","generation"]) || p.GetProperty("name").GetString()!=FatePolicy.AccountPolicy
                || p.GetProperty("revision").GetInt32()!=FatePolicy.PolicyRevision
                || !Utc(S("issuedAt"),out var issued) || !Utc(S("expiresAt"),out var expires)
                || expires-issued!=TimeSpan.FromSeconds(30) || expires<=now || issued>now.AddSeconds(5)
                || issued<now.AddSeconds(-30) || paired>issued) return null;
            bool authorized=g.GetProperty("authorized").GetBoolean(), enabled=p.GetProperty("enabled").GetBoolean();
            Guid? policyGeneration=null; var pg=p.GetProperty("generation");
            if(pg.ValueKind!=JsonValueKind.Null) {
                if(!Uuid(pg.GetString(),out var parsed)) return null; policyGeneration=parsed;
            }
            string? reason=S("reason");
            if(authorized ? reason is not null || !enabled || policyGeneration is null || admitted!=source
                : reason is null || !ErrorCodes.Contains(reason)) return null;
            return new(new(generation,origin,NativeProduct.Name,FatePolicy.Capability,1,FatePolicy.Capability,
                admitted!,FatePolicy.AccountPolicy,1,authorized,false,expires,true,true),policyGeneration,paired,issued,enabled,reason);
        } catch(Exception e) when(e is JsonException or InvalidOperationException or FormatException
            or System.Collections.Generic.KeyNotFoundException or OverflowException or ArgumentException) { return null; }
    }
    internal static bool Error(string json,out string code,out int? retry) {
        code="Unavailable"; retry=null;
        try {
            using var d=JsonDocument.Parse(json,new JsonDocumentOptions {MaxDepth=4}); var r=d.RootElement;
            if(!(Shape(r,["ok","code"]) || Shape(r,["ok","code","retryAfterSeconds"]))
                || r.GetProperty("ok").ValueKind!=JsonValueKind.False) return false;
            var c=r.GetProperty("code").GetString(); if(c is null || !ErrorCodes.Contains(c)) return false;
            if(r.TryGetProperty("retryAfterSeconds",out var t)) {
                if(!t.TryGetInt32(out var seconds) || seconds is <1 or >640) return false; retry=seconds;
            }
            code=c; return true;
        } catch(Exception e) when(e is JsonException or InvalidOperationException or FormatException or OverflowException) { return false; }
    }
}
#endif
