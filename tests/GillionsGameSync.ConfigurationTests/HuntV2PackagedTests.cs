using System.Reflection;
using System.Text.Json;

internal static class HuntV2PackagedTests {
    internal static void Run(Assembly assembly, bool testing, string? siteProof) {
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var instanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Check(bool value) { if (!value) throw new InvalidOperationException("Exact packaged Hunt V2/focus invariant failed."); }
        var focusType = assembly.GetType("GillionsGameSync.HuntFocusState");
        var negotiationType = assembly.GetType("GillionsGameSync.HuntMapNegotiation");
        var clockType = assembly.GetType("GillionsGameSync.WebsiteCommandPollClock");
        var timingType = assembly.GetType("GillionsGameSync.WebsiteCommandTrace");
        if (!testing) { Check(focusType is null && negotiationType is null && clockType is null && timingType is null); return; }
        var policy = assembly.GetType("GillionsGameSync.HuntMapPolicy", true)!;
        var poll = policy.GetMethod("TryPoll", flags)!;
        var now = new DateTime(2026, 10, 4, 18, 0, 0, DateTimeKind.Utc);
        var request = new Dictionary<string, object?> {
            ["requestType"]="hunt_map", ["requestId"]="b025d639-8779-492c-8cba-f07d79ccf860",
            ["claimToken"]="synthetic_claim_only_123456", ["huntTargetId"]=7957, ["huntTargetName"]="Synthetic B rank",
            ["availability"]=new { classification="UNKNOWN", fateId=(int?)null, fateName=(string?)null, activity="UNKNOWN" },
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
        var clock=Activator.CreateInstance(clockType!,nonPublic:true)!;
        var begin=clockType!.GetMethod("TryBegin",instanceFlags)!;
        bool Begin(DateTime time,bool focused)=>(bool)begin.Invoke(clock,[time,focused])!;
        Check(Begin(now,false)&&!Begin(now.AddSeconds(4),false)&&Begin(now.AddSeconds(5),false));
        clockType.GetMethod("Reset",instanceFlags)!.Invoke(clock,null);
        Check(Begin(now,true)&&!Begin(now.AddMilliseconds(999),true)&&Begin(now.AddSeconds(1),true));
        clockType.GetMethod("Backoff",instanceFlags)!.Invoke(clock,[now,TimeSpan.FromSeconds(30)]);
        Check(!Begin(now.AddSeconds(29),true)&&Begin(now.AddSeconds(30),true));
        Check(timingType is not null&&timingType.GetMethod("PollDispatched",instanceFlags) is not null);
        Console.WriteLine("Exact packaged command clocks: 9 background/focus/no-double-deadline/backoff/timing checks PASS; not live latency proof.");
        if (siteProof is not null) {
            using var proof=JsonDocument.Parse(File.ReadAllText(siteProof)); var root=proof.RootElement;
            var observed=DateTime.Parse(root.GetProperty("now").GetString()!).ToUniversalTime();
            foreach(var (name,wire,time) in new[]{("poll","native_hunt_map_v1",observed),("pollV2","native_hunt_map_v2",observed.AddSeconds(6)),("pollNext","native_hunt_map_v2",observed.AddSeconds(43))}) {
                object?[] values=[root.GetProperty(name).GetRawText(),time,wire,null];
                Check((bool)poll.Invoke(null,values)! && values[3] is not null);
            }
            Check((bool)apply.Invoke(focus,[root.GetProperty("focus").GetRawText(),observed.AddSeconds(6),true])!);
            Check((bool)consumed.Invoke(null,[root.GetProperty("consume").GetRawText()])!);
            Console.WriteLine("Exact packaged DLL accepts 5 running-Site V1/V2/browser-Next/focus/consume wire cross-checks; simulated SQL, not authenticated HTTP or gameplay proof.");
        }
    }
}
