using System.Text.Json.Nodes;
using GillionsGameSync;

internal static class PermissionAuthorityTests {
    internal static void Run() {
        int count=0;
        void Check(bool value, string text) { count++; if(!value) throw new Exception(text); }
        var issued = new DateTime(2026,10,7,12,0,1,DateTimeKind.Utc);
        const string device = "00000000-0000-4000-8000-000000000001";
        const string generation = "00000000-0000-4000-8000-000000000002";
        JsonObject Fixture(string state="legacy", bool enabled=false) {
            var p=new JsonObject();
            foreach(var key in PermissionAuthority.Keys) p[key]=new JsonObject {
                ["scope"]=key=="marketContribution"?"account":"device", ["state"]=state,
                ["enabled"]=state=="legacy"?null:JsonValue.Create(enabled),
                ["generation"]=state=="explicit"?generation:null,
                ["decidedAt"]=state=="explicit"?issued.AddSeconds(-1).ToString("O"):null};
            return new JsonObject {["ok"]=true,["permissionAuthority"]=new JsonObject {
                ["revision"]=1,["deviceBinding"]=new JsonObject {["deviceId"]=device,
                    ["pairedAt"]=issued.AddDays(-1).ToString("O"),["characterId"]=100},
                ["issuedAt"]=issued.ToString("O"),["expiresAt"]=issued.AddSeconds(30).ToString("O"),["permissions"]=p}};
        }
        PermissionAuthority Bound() { var a=new PermissionAuthority();a.Bind("paired-session:999999999999",device);return a; }
        foreach(var key in PermissionAuthority.Keys) {
            var legacy=Bound();Check(legacy.Apply(Fixture().ToJsonString(),issued,1000),key+" legacy parse");
            Check(legacy.Allows(key,true,1000),key+" legacy ON");Check(!legacy.Allows(key,false,1000),key+" legacy OFF");
            var on=Bound();Check(on.Apply(Fixture("explicit",true).ToJsonString(),issued,1000),key+" ON parse");
            Check(on.Allows(key,false,1000),key+" explicit overrides historical OFF");
            Check(!on.Allows(key,true,31000),key+" exact lease expiry");
            on.Apply("{}",issued,1000);Check(!on.Allows(key,true,1000),key+" missing closes learned ON");
            var off=Bound();off.Apply(Fixture("explicit").ToJsonString(),issued,1000);
            Check(!off.Allows(key,true,1000),key+" explicit OFF");
            var invalid=Bound();invalid.Apply(Fixture("invalid").ToJsonString(),issued,1000);
            Check(!invalid.Allows(key,true,1000),key+" invalid");
        }
        var wrongActions=new Action<JsonObject>[] {
            r=>r["permissionAuthority"]!["revision"]=2,
            r=>r["permissionAuthority"]!["deviceBinding"]!["deviceId"]=generation,
            r=>r["permissionAuthority"]!["deviceBinding"]!["characterId"]=0,
            r=>r["permissionAuthority"]!["expiresAt"]=issued.AddSeconds(31).ToString("O"),
            r=>r["permissionAuthority"]!["issuedAt"]="malformed",
            r=>r["permissionAuthority"]!["permissions"]!["itemLinks"]!["scope"]="account",
        };
        foreach(var change in wrongActions) { var r=Fixture("explicit",true);change(r);var a=Bound();a.Apply(r.ToJsonString(),issued,1000);Check(!a.Allows("itemLinks",true,1000),"bad shape/binding/scope denied"); }
        var pinned=Bound();Check(pinned.Apply(Fixture("explicit",true).ToJsonString(),issued,1000),"binding pinned");
        var wrong=Fixture("explicit",true);wrong["permissionAuthority"]!["deviceBinding"]!["characterId"]=101;
        Check(!pinned.Apply(wrong.ToJsonString(),issued,1000)&&!pinned.Allows("itemLinks",true,1000),"wrong external Site ID denied, never compared to game Content ID");
        pinned=Bound();pinned.Apply(Fixture("explicit",true).ToJsonString(),issued,1000);
        wrong=Fixture("explicit",true);wrong["permissionAuthority"]!["deviceBinding"]!["pairedAt"]=issued.AddDays(-2).ToString("O");
        Check(!pinned.Apply(wrong.ToJsonString(),issued,1000),"wrong enrollment timestamp");
        var order=Bound();var newer=Fixture("explicit",true);
        newer["permissionAuthority"]!["issuedAt"]=issued.AddSeconds(1).ToString("O");newer["permissionAuthority"]!["expiresAt"]=issued.AddSeconds(31).ToString("O");
        order.Apply(newer.ToJsonString(),issued.AddSeconds(1),1000);
        Check(!order.Apply(Fixture("explicit",false).ToJsonString(),issued,1001)&&order.Allows("itemLinks",false,1001),"older issuedAt cannot replace newer ON");
        Check(!order.Allows("itemLinks",true,31000),"older response cannot extend lease");
        var delayed=Bound();var latest=Fixture("explicit",true);
        latest["permissionAuthority"]!["issuedAt"]=issued.AddSeconds(25).ToString("O");
        latest["permissionAuthority"]!["expiresAt"]=issued.AddSeconds(55).ToString("O");
        Check(delayed.Apply(latest.ToJsonString(),issued.AddSeconds(25),1000),"newer lease accepted");
        Check(!delayed.Apply(Fixture("explicit",false).ToJsonString(),issued.AddSeconds(31),1001)
            && delayed.Allows("itemLinks",false,1001),"expired out-of-order response discarded without revoking newer lease");
        var opaque=Fixture("explicit",false);
        opaque["permissionAuthority"]!["issuedAt"]=issued.AddSeconds(26).ToString("O");
        opaque["permissionAuthority"]!["expiresAt"]=issued.AddSeconds(56).ToString("O");
        opaque["permissionAuthority"]!["permissions"]!["itemLinks"]!["generation"]="00000000-0000-4000-8000-000000000001";
        Check(delayed.Apply(opaque.ToJsonString(),issued.AddSeconds(26),1002)
            && !delayed.Allows("itemLinks",true,1002),"opaque UUID order does not override issuedAt chronology");
        delayed.Bind("new-session:other-character",device);
        Check(!delayed.Allows("itemLinks",true,1003)&&delayed.Enrollment is null,"reconnect/character change drops lease and pinned enrollment");
        var downgrade=Bound();downgrade.Apply(Fixture("explicit",false).ToJsonString(),issued,0);
        downgrade.Apply(Fixture().ToJsonString(),issued,1);
        Check(!downgrade.Allows("itemLinks",true,1),"legacy downgrade cannot resurrect an explicit decision");
        var mixed=Fixture("explicit",true);mixed["permissionAuthority"]!["permissions"]!["itemLinks"]!["enabled"]=false;
        var independent=Bound();independent.Apply(mixed.ToJsonString(),issued,0);
        Check(!independent.Allows("itemLinks",true,0)&&independent.Allows("partyFinderLinks",false,0),"item OFF/PF ON independent");
        Check(independent.PartyFinderLinks(false,false,0),"actual shared PF path supersedes both historical OFF fields");
        var legacyCoupled=Bound();legacyCoupled.Apply(Fixture().ToJsonString(),issued,0);
        Check(!legacyCoupled.PartyFinderLinks(false,true,0)&&legacyCoupled.PartyFinderLinks(true,true,0),"actual legacy PF path retains parent behavior");
        var manual=Bound();manual.Apply(Fixture("explicit",false).ToJsonString(),issued,0);
        Check(manual.HuntReceiving(false,0),"actual manual Hunt receiver remains eligible under explicit automatic OFF");
        Check(!manual.Allows("automaticHuntMaps",true,0),"focus cannot authorize automatic OFF");
        Check(!manual.HuntReceiving(true,30000),"expired explicit cannot authorize presentation");
        var malformed=Fixture("explicit",true);malformed["permissionAuthority"]!["permissions"]!["itemLinks"]!["generation"]=new JsonObject();
        var isolated=Bound();isolated.Apply(malformed.ToJsonString(),issued,0);
        Check(!isolated.Allows("itemLinks",true,0)&&isolated.Allows("personalHunts",false,0),"malformed single decision does not cancel other resources");
        mixed["permissionAuthority"]!["permissions"]!["itemLinks"]!["enabled"]=true;
        mixed["permissionAuthority"]!["permissions"]!["partyFinderLinks"]!["enabled"]=false;
        independent.Apply(mixed.ToJsonString(),issued,0);
        Check(independent.Allows("itemLinks",false,0)&&!independent.Allows("partyFinderLinks",true,0),"reverse independence");
        mixed["permissionAuthority"]!["permissions"]!["marketContribution"]!["enabled"]=false;
        mixed["marketContribution"]=new JsonObject {["serviceAvailable"]=true,["enabled"]=false};
        independent.Apply(mixed.ToJsonString(),issued,0);Check(!independent.Allows("marketContribution",true,0),"service readiness is not consent");
        independent.Invalidate();Check(!independent.Allows("personalHunts",true,0),"session invalidation closes subordinate ON");
        Check(PermissionObservationFilter.FreshPersonal("hunt_bills","{\"bills\":[{\"observedAtUtc\":\"2026-10-07T12:00:00Z\"}]}",issued)==null,"no preconsent Hunt backlog");
        Check(PermissionObservationFilter.FreshPersonal("submarine_personal","{\"slots\":[{\"observedAtUtc\":\"2026-10-07T12:00:02Z\"}]}",issued)!=null,"fresh submarine observation");
        const string history="{\"bills\":[{\"observedAtUtc\":\"2026-10-07T12:00:00Z\"},{\"observedAtUtc\":\"2026-10-07T12:00:02Z\"}]}";
        var copy=JsonNode.Parse(PermissionObservationFilter.FreshPersonal("hunt_bills",history,issued)!);
        Check(copy!["bills"]!.AsArray().Count==1&&JsonNode.Parse(history)!["bills"]!.AsArray().Count==2,"transport copy filters backlog without changing retained history");
        Check(PermissionObservationFilter.FreshPersonal("hunt_bills","{\"bills\":[],\"billItemCoverage\":{\"observedAtUtc\":\"2026-10-07T12:00:02Z\"}}",issued)!=null,"fresh authoritative coverage remains representable without positive bills");
        Check(PermissionObservationFilter.FreshPersonal("hunt_bills","{\"bills\":[],\"billItemCoverage\":{\"observedAtUtc\":\"2026-10-07T12:00:00Z\"}}",issued)==null,"preconsent coverage is not replayed");
        Console.WriteLine($"Permission authority PASS: {count} assertions (synthetic contract/ordering/privacy, not live acceptance).");
    }
}
