using System.Reflection;
using System.Text.Json;

internal static class HuntV2PackagedTests {
    internal static void Run(Assembly assembly, bool testing) {
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var instanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Check(bool value) { if (!value) throw new InvalidOperationException("Exact packaged Hunt V2/focus invariant failed."); }
        var focusType = assembly.GetType("GillionsGameSync.HuntFocusState");
        var negotiationType = assembly.GetType("GillionsGameSync.HuntMapNegotiation");
        if (!testing) { Check(focusType is null && negotiationType is null); return; }
        var policy = assembly.GetType("GillionsGameSync.HuntMapPolicy", true)!;
        var poll = policy.GetMethod("TryPoll", flags)!;
        var now = new DateTime(2026, 10, 4, 18, 0, 0, DateTimeKind.Utc);
        var request = new Dictionary<string, object?> {
            ["requestType"]="hunt_map", ["requestId"]="b025d639-8779-492c-8cba-f07d79ccf860",
            ["claimToken"]="synthetic_claim_only_123456", ["huntTargetId"]=7957, ["huntTargetName"]="Synthetic B rank",
            ["availability"]=new { classification="ALWAYS_AVAILABLE", fateId=(int?)null, fateName=(string?)null, activity="UNKNOWN" },
            ["territoryId"]=148, ["mapId"]=30, ["mapX"]=21.5, ["mapY"]=22.5,
            ["candidateId"]=new string('b',24), ["revision"]=new string('a',64), ["candidateIndex"]=1,
            ["candidateCount"]=7, ["expiresAt"]="2026-10-04T18:01:00Z", ["contractVersion"]=2,
            ["candidateKind"]="DISCRETE_SPAWN_LOCATIONS", ["walkExpiresAt"]="2026-10-03T18:00:00Z",
            ["candidateSetTruncated"]=false, ["recommendedAetheryte"]=null,
        };
        string Envelope(object? value, object? progression=null) => JsonSerializer.Serialize(new { ok=true, request=value, progression });
        bool Parse(string json, string capability, out object? result) {
            object?[] args=[json,now,capability,null]; var accepted=(bool)poll.Invoke(null,args)!; result=args[3]; return accepted;
        }
        Check(Parse(Envelope(request),"native_hunt_map_v2",out var parsed) && parsed is not null);
        Check((string)parsed!.GetType().GetProperty("Capability",instanceFlags)!.GetValue(parsed)! == "native_hunt_map_v2");
        Check(!Parse(Envelope(request),"native_hunt_map_v1",out _));
        Check(!Parse(Envelope(request,"advanced"),"native_hunt_map_v2",out _));
        Check(Parse(Envelope(null),"native_hunt_map_v2",out var empty) && empty is null);
        request["candidateIndex"]=7; Check(!Parse(Envelope(request),"native_hunt_map_v2",out _)); request["candidateIndex"]=1;
        request.Remove("contractVersion"); Check(!Parse(Envelope(request),"native_hunt_map_v2",out _));
        var consumed=policy.GetMethod("Consumed",flags)!;
        Check((bool)consumed.Invoke(null,["{\"ok\":true,\"consumed\":true}"])!);
        Check(!(bool)consumed.Invoke(null,["{\"ok\":false,\"consumed\":false}"])!);
        var focus=Activator.CreateInstance(focusType!,nonPublic:true)!;
        var apply=focusType!.GetMethod("Apply",instanceFlags)!; var active=focusType.GetMethod("Active",instanceFlags)!;
        var focusJson="{\"ok\":true,\"huntFocus\":{\"contract\":\"active_hunt_focus_v1\",\"focused\":true,\"expiresAt\":\"2026-10-04T18:00:30Z\"}}";
        Check((bool)apply.Invoke(focus,[focusJson,now,true])! && (bool)active.Invoke(focus,[now,true])!);
        Check(!(bool)active.Invoke(focus,[now,false])! && !(bool)active.Invoke(focus,[now.AddSeconds(30),true])!);
        Check(!(bool)apply.Invoke(focus,["{\"ok\":true}",now,true])! && !(bool)active.Invoke(focus,[now,true])!);
        Check(!(bool)apply.Invoke(focus,[focusJson,now,false])!);
        var negotiation=Activator.CreateInstance(negotiationType!,nonPublic:true)!;
        var capability=negotiationType!.GetMethod("Capability",instanceFlags)!;
        Check((string)capability.Invoke(negotiation,[now])! == "native_hunt_map_v2");
        negotiationType.GetMethod("Unsupported",instanceFlags)!.Invoke(negotiation,[now]);
        Check((string)capability.Invoke(negotiation,[now])! == "native_hunt_map_v1");
        Check((string)capability.Invoke(negotiation,[now.AddMinutes(5)])! == "native_hunt_map_v2");
        Console.WriteLine("Exact packaged Hunt V2/focus: 16 pure contract/lease/fallback checks PASS; no game or HTTP invocation.");
    }
}
