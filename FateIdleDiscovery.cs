#if GILLIONS_TEST_BUILD || GILLIONS_FATE_TESTS
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace GillionsGameSync;

// Testing-only FATE discovery slowdown (Site FFXIV-Gillions#745). FateDiscovery.cs
// stays the frozen Stable transport; this wraps its Parse without loosening any
// rule other than the two below, so every grant still passes FateDiscovery.Parse.
//  1. A valid denial may carry retryAfterSeconds 5 to 900 (authorized keeps 5).
//  2. A well-formed but different referenceCompatibilityEpoch is a never-sending
//     reason (FATE_REFERENCE_MISMATCH) with a slow retry, not a malformed reply.
internal readonly record struct FateDiscoveryResult(FateGrant Grant, int RetryAfterSeconds);

internal static class FateIdleDiscovery {
    internal const string ReferenceMismatch="FATE_REFERENCE_MISMATCH";
    internal const int MaximumIdleRetrySeconds=900, ReferenceMismatchRetrySeconds=600;
    private static readonly Regex Epoch=new(@"^fate-reference:\d{4}\.\d{2}\.\d{2}\.\d{4}\.\d{4}$",RegexOptions.CultureInvariant);

    internal static FateDiscoveryResult? Parse(string json, string generation, string deviceId, FateSource source, DateTime now,
        string origin = FateDiscovery.Origin) {
        JsonObject? root;
        try {
            using(var document=JsonDocument.Parse(json,new JsonDocumentOptions {MaxDepth=8}))
                if(!UniqueNames(document.RootElement)) return null; // the frozen Shape check, kept across normalization
            root=JsonNode.Parse(json,documentOptions:new JsonDocumentOptions {MaxDepth=8}) as JsonObject;
        } catch(Exception e) when(e is JsonException or ArgumentException or InvalidOperationException) { return null; }
        if(root?["fateContribution"] is not JsonObject g || g["retryAfterSeconds"] is not JsonValue retryValue
            || !retryValue.TryGetValue<int>(out var retry) || g["authorized"] is not JsonValue authorizedValue
            || !authorizedValue.TryGetValue<bool>(out var authorized)) return null;
        if(authorized ? retry!=5 : retry is <5 or >MaximumIdleRetrySeconds) return null;
        var epoch=g["referenceCompatibilityEpoch"] is JsonValue epochValue && epochValue.TryGetValue<string>(out var text) ? text : null;
        bool mismatch=epoch is not null && epoch!="fate-reference:"+source.ReferenceGameVersion && Epoch.IsMatch(epoch);
        // Nothing loosened: the frozen parser reads the original text.
        if(retry==5 && !mismatch)
            return FateDiscovery.Parse(json,generation,deviceId,source,now,origin) is { } exact ? new(exact,5) : null;
        // Normalize only the loosened fields; the frozen parser checks everything else.
        g["retryAfterSeconds"]=5;
        if(mismatch) {
            g["referenceCompatibilityEpoch"]="fate-reference:"+source.ReferenceGameVersion;
            g["authorized"]=false;
            g["reason"]="FATE_SOURCE_UNSUPPORTED";
        }
        if(FateDiscovery.Parse(root.ToJsonString(),generation,deviceId,source,now,origin) is not { } grant) return null;
        if(!mismatch) return new(grant,retry);
        return new(grant with {Reason=ReferenceMismatch},Math.Max(retry,ReferenceMismatchRetrySeconds));
    }

    // Presence carries the same policy object. It is only a hint to ask discovery
    // now; discovery stays the single authority for any grant.
    internal static bool PresenceAuthorized(string json) {
        try {
            using var d=JsonDocument.Parse(json,new JsonDocumentOptions {MaxDepth=8}); var r=d.RootElement;
            return r.ValueKind==JsonValueKind.Object && r.TryGetProperty("fateContribution",out var g)
                && g.ValueKind==JsonValueKind.Object && g.TryGetProperty("authorized",out var a) && a.ValueKind==JsonValueKind.True;
        } catch(JsonException) { return false; }
    }

    private static bool UniqueNames(JsonElement value) {
        if(value.ValueKind==JsonValueKind.Array) {
            foreach(var item in value.EnumerateArray()) if(!UniqueNames(item)) return false;
            return true;
        }
        if(value.ValueKind!=JsonValueKind.Object) return true;
        var seen=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach(var property in value.EnumerateObject())
            if(!seen.Add(property.Name) || !UniqueNames(property.Value)) return false;
        return true;
    }
}
#endif
