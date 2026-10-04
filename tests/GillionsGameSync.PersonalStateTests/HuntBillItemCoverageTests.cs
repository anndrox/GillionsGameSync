using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using GillionsGameSync;

internal static class HuntBillItemCoverageTests {
    internal static void Run(Action<bool,string> check) {
        uint[] ids = [2001361,2001700,2001701,2001702,2001362,2001703,2002113,2002114,2002115,2002116,2002628,2002629,2002630,2002631,2003090,2003091,2003092,2003093,2003509,2003510,2003511,2003512];
        var catalog = ids.Select((id,i) => new HuntBillItemDomain((byte)i,id,1,1,10)).ToArray();
        var key = HuntBillRetentionPolicy.CharacterKey(123); var other = HuntBillRetentionPolicy.CharacterKey(456);
        var now = new DateTime(2026,10,4,12,0,0,DateTimeKind.Utc); var mono = Stopwatch.GetTimestamp();
        long At(double seconds) => mono + (long)(Stopwatch.Frequency * seconds);
        var c = new HuntBillItemCoverage(); c.SetCatalog(catalog);
        var slots = Enumerable.Range(0,4).Select(i => new HuntKeyItemSlot(i,2004,false,0,0)).ToArray();
        void Observe(HuntKeyItemSlot[] input, double time=0, bool loaded=true, bool stable=true, int size=4, string? after=null, bool initialized=true, int container=2004, string sdk=PersonalObservationCompatibility.NativeVersion) =>
            c.Observe(key, after ?? key, initialized, loaded, container, size, input, stable, now.AddSeconds(time), At(time), PersonalObservationCompatibility.GameBuild,"0.0.83.0",sdk);
        var history = new HuntBillRetention { LocalRetentionEnabled = true };
        var hp = new HuntBillRetentionPolicy(history);
        var target = new HuntBillTarget(4,715,13000,1,1,0,2,1,1,1);
        hp.Observe(key,[new(18,"daily",1,269,2003509,[target],now,"synthetic-game","synthetic-collector")]);
        var retained = JsonSerializer.Serialize(history);
        Observe(slots);
        check(c.Current(key,now,mono)!.Domains.All(d => d.State == "absent_confirmed"),"A: complete absence failed");
        check(c.Current(other,now,mono) is null,"D: other character absence leaked");
        var absent = c.Payload(hp,key,now,mono)!;
        check(PersonalSyncPolicy.HuntCoveragePayloadValid(absent),"V2 complete domain payload rejected");
        check(!absent.Contains(key) && !absent.Contains("123456") && !absent.Contains("session",StringComparison.OrdinalIgnoreCase),"Private/session identifier exported");
        var presentSlots = slots.ToArray(); presentSlots[0] = new(0,2004,false,2003509,1);
        Observe(presentSlots,1);
        var present = c.Current(key,now.AddSeconds(1),At(1))!;
        check(present.Domains.Single(d => d.BillTypeId==18).State=="present_unresolved", "B/H: presence inferred exact order");
        check(present.Domains.Where(d => d.BillTypeId!=18).All(d => d.State=="absent_confirmed"),"G: types not independent");
        var presentPayload = c.Payload(hp,key,now.AddSeconds(1),At(1))!;
        var alternateHistory = JsonSerializer.Deserialize<HuntBillRetention>(retained)!;
        var alternatePolicy = new HuntBillRetentionPolicy(alternateHistory);
        alternatePolicy.Observe(key,[new(18,"daily",1,270,2003509,[target],now.AddSeconds(1),"synthetic-game","synthetic-collector")]);
        var differentOrderPayload = c.Payload(alternatePolicy,key,now.AddSeconds(1),At(1))!;
        check(JsonNode.DeepEquals(JsonNode.Parse(presentPayload)!["billItemCoverage"],JsonNode.Parse(differentOrderPayload)!["billItemCoverage"]),"H: different cached order changed presence authority");
        check(!JsonNode.Parse(presentPayload)!["billItemCoverage"]!.ToJsonString().Contains("269"),"H: coverage contains cached order");
        check(JsonSerializer.Serialize(history)==retained,"F: coverage mutated historical counter/provenance");
        foreach (var input in new[] { slots[..3], [slots[0] with { Symbolic=true },slots[1],slots[2],slots[3]],
            [slots[0] with { Slot=2 },slots[1],slots[2],slots[3]], [slots[0] with { Container=0 },slots[1],slots[2],slots[3]],
            [slots[0] with { ItemId=2003509 },slots[1],slots[2],slots[3]], [slots[0] with { Quantity=1 },slots[1],slots[2],slots[3]] }) {
            Observe(input,2); check(c.Current(key,now.AddSeconds(2),At(2))!.Domains.All(d=>d.State=="unavailable"),"C: partial/malformed fabricated absence");
        }
        foreach(var reason in new[]{0,1,2,3,4}) {
            Observe(slots,3,loaded:reason!=0,stable:reason!=1,initialized:reason!=2,container:reason==3?0:2004,sdk:reason==4?"unsupported":PersonalObservationCompatibility.NativeVersion);
            check(c.Current(key,now.AddSeconds(3),At(3))!.Domains.All(d=>d.State=="unavailable"),"C: unavailable source gained authority");
        }
        Observe(slots,4,after:other); check(c.Current(key,now.AddSeconds(4),At(4)) is null,"D: transition retained previous absence");
        Observe(presentSlots,5); Observe(slots,6);
        check(c.Current(key,now.AddSeconds(6),At(6))!.Domains.Single(d=>d.BillTypeId==18).State=="absent_confirmed","I: removal not observed");
        check(c.Current(key,now.AddSeconds(22),At(22)) is null,"Expired UTC coverage remained current");
        check(c.Current(key,now.AddSeconds(6),At(22)) is null,"Monotonic expiry bypassed by wall clock");
        check(c.Current(key,now.AddSeconds(5),At(6)) is null,"Future sample admitted");
        var payload = c.Payload(hp,key,now.AddSeconds(6),At(6))!;
        var owner = PersonalSyncPolicy.Owner("generation",key); var sync = new HuntBillItemSync();
        var first = sync.Prepare(owner,payload,c.Epoch,now.AddSeconds(6),At(6))!;
        check(ReferenceEquals(first,sync.Prepare(owner,payload,c.Epoch,now.AddSeconds(7),At(7))),"Retry replaced nonce/body");
        check(!PersonalSyncPolicy.CanSend(false,true,PersonalSyncPolicy.Origin,true,first),"E: permission OFF sent coverage");
        check(!PersonalSyncPolicy.CanSend(true,true,"https://gillions.app",true,first),"Production coverage admitted");
        check(sync.Current(first,owner,payload,c.Epoch,now.AddSeconds(7),At(7)),"Current retry rejected");
        check(!sync.Current(first,owner,presentPayload,c.Epoch,now.AddSeconds(7),At(7)),"Changed presence reused absence");
        c.Clear(); check(!sync.Current(first,owner,payload,c.Epoch,now.AddSeconds(7),At(7)),"Session epoch replayed absence");
        var next = sync.Prepare(owner,payload,c.Epoch,now.AddSeconds(7),At(7))!;
        check(next.Nonce!=first.Nonce,"New session reused nonce");
        sync.Block(next);
        check(ReferenceEquals(next,sync.Prepare(owner,payload,c.Epoch,now.AddSeconds(30),At(30))),"Terminal unchanged input retried after expiry");
        sync.Clear(); check(!sync.Current(next,owner,payload,c.Epoch,now.AddSeconds(8),At(8)),"OFF/reload kept pending coverage");
        Observe(slots,8); history.LocalRetentionEnabled=false;
        bool refused=false; try { c.Payload(hp,key,now.AddSeconds(8),At(8)); } catch(InvalidOperationException) { refused=true; }
        check(refused,"E: retention OFF exported coverage");
        history.LocalRetentionEnabled=true;
        var empty = new HuntBillRetentionPolicy(new(){LocalRetentionEnabled=true});
        check(c.Payload(empty,key,now.AddSeconds(8),At(8)) is not null,"No historical positives blocked absence-only payload");
        check(!PersonalSyncPolicy.PayloadValid("hunt_bills",payload),"V2 silently admitted as legacy v1");
        var ack=JsonSerializer.Serialize(new {ok=true,acceptedClientProduct="GillionsGameSyncTest", personalObservations=new {contractVersion=1,endpoint=PersonalSyncPolicy.Endpoint, resources=new[]{new {resourceType="hunt_bills",schemaVersion=2,collectorSchema="hunt-bills-v2",capability="hunt_bills_v2",maxPayloadBytes=65536}}}});
        check(PersonalSyncPolicy.HuntCoverageCompatible(ack) && !PersonalSyncPolicy.Compatible(ack,"hunt_bills"),"V2 version admission/fallback broken");
        foreach(var field in new[]{("hunt_bills_v2","hunt_bills_v1"),("hunt-bills-v2","hunt-bills-v1"),("65536","65535"),("GillionsGameSyncTest","GillionsGameSync")})
            check(!PersonalSyncPolicy.HuntCoverageCompatible(ack.Replace(field.Item1,field.Item2)),"Wrong contract accepted");
        var pnode=JsonNode.Parse(payload)!; pnode["billItemCoverage"]!["domains"]![0]!["state"]="complete";
        check(!PersonalSyncPolicy.HuntCoveragePayloadValid(pnode.ToJsonString()),"Unknown state admitted");
        c.SetCatalog(catalog[..21]); check(c.Current(key,now.AddSeconds(8),At(8)) is null,"Partial catalog authorized omissions");
        check(!HuntBillItemCoverage.CatalogValid(catalog.Select(d=>d with {KeyItemId=1}).ToArray()),"Duplicate mapping admitted");
        check(JsonSerializer.Serialize(history)==retained,"F: lifecycle tests mutated retained progress");
        Console.WriteLine("Absence-only A-I, completeness/epoch/privacy/nonce/expiry/admission fixtures PASS; synthetic, no live inventory proof.");
    }
}
