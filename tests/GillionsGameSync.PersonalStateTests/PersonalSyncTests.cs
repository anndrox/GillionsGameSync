using GillionsGameSync;
using System.Text.Json;

internal static class PersonalSyncTests {
    internal static void Run(Action<bool,string> check) {
        var payload = "{\"schemaVersion\":1,\"collectorSchema\":\"hunt-bills-v1\",\"uploadState\":\"local-only-no-server-contract\",\"bills\":[{}]}";
        var owner = PersonalSyncPolicy.Owner("generation-a", new string('a',64));
        var other = PersonalSyncPolicy.Owner("generation-b", new string('b',64));
        var state = new PersonalSyncState();
        var prepared = PersonalSyncPolicy.Prepare(state,owner,"hunt_bills",payload) ?? throw new Exception("positive preparation");
        check(true,"positive preparation");
        check(owner != other,"account generation and character partition isolation");
        var changed = payload.Replace("[{}]","[{\"changed\":true}]");
        check(ReferenceEquals(prepared,PersonalSyncPolicy.Prepare(state,owner,"hunt_bills",changed)),"unsent nonce/body immutable under successor");
        var restored = JsonSerializer.Deserialize<PersonalSyncState>(JsonSerializer.Serialize(state))!;
        check(PersonalSyncPolicy.Valid(restored) && restored.Prepared[0].Nonce == prepared.Nonce,"restart retains exact nonce/body");
        check(PersonalSyncPolicy.Prepare(state,owner,"hunt_bills",payload.Replace("[{}]","[]")) is null,"unavailable empty cannot replace");
        check(!PersonalSyncPolicy.CanSend(false,true,PersonalSyncPolicy.Origin,true,prepared),"sync OFF stops sends");
        check(!PersonalSyncPolicy.CanSend(true,false,PersonalSyncPolicy.Origin,true,prepared),"retention OFF stops sends");
        check(!PersonalSyncPolicy.CanSend(true,true,PersonalSyncPolicy.Origin,false,prepared),"capability absent stops sends");
        foreach(var origin in new[]{"https://gillions.app","http://test.gillions.app","https://10.10.2.1","https://test.gillions.app:443","https://test.gillions.app.evil"})
            check(!PersonalSyncPolicy.CanSend(true,true,origin,true,prepared),"exact secure TEST origin " + origin);
        check(PersonalSyncPolicy.CanSend(true,true,PersonalSyncPolicy.Origin,true,prepared),"explicit authorized positive send");
        prepared.Blocked = true;
        check(!PersonalSyncPolicy.CanSend(true,true,PersonalSyncPolicy.Origin,true,prepared),"terminal conflict fails closed");
        prepared.Blocked = false; prepared.Acknowledged = true;
        check(!PersonalSyncPolicy.CanSend(true,true,PersonalSyncPolicy.Origin,true,prepared),"receipt stops duplicate maintenance");
        var successor = PersonalSyncPolicy.Prepare(state,owner,"hunt_bills",changed)!;
        check(successor.Nonce != prepared.Nonce && successor.Payload == changed,"changed content receives successor nonce only after ACK");
        for (int i=1;i<PersonalSyncPolicy.MaximumPrepared;i++)
            check(PersonalSyncPolicy.Prepare(state,PersonalSyncPolicy.Hash(i.ToString()),"hunt_bills",payload) is not null,"bounded owner admission");
        var before = JsonSerializer.Serialize(state);
        check(PersonalSyncPolicy.Prepare(state,other,"hunt_bills",payload) is null && JsonSerializer.Serialize(state)==before,"capacity preserves all pending");
        var invalid = JsonSerializer.Deserialize<PersonalSyncState>(before)!;
        invalid.Prepared[0] = invalid.Prepared[0] with { Payload = changed+" " };
        check(!PersonalSyncPolicy.Valid(invalid),"modified pending hash fails closed");
        invalid.SchemaVersion = 2;
        check(!PersonalSyncPolicy.Valid(invalid),"unknown persisted schema preserved/fail closed");
        var ack = JsonSerializer.Serialize(new {ok=true, acceptedClientProduct="GillionsGameSyncTest", personalObservations=new {
            contractVersion=1, endpoint=PersonalSyncPolicy.Endpoint, resources=new[]{new {
                resourceType="hunt_bills",schemaVersion=1,collectorSchema="hunt-bills-v1",capability="hunt_bills_v1",maxPayloadBytes=65536}}
        }});
        check(PersonalSyncPolicy.Compatible(ack,"hunt_bills"),"exact contract accepted");
        check(!PersonalSyncPolicy.Compatible(ack,"submarine_personal"),"no implicit submarine permission");
        foreach(var bad in new[]{ack.Replace("https:","http:"),ack.Replace("65536","65537"),ack.Replace("hunt-bills-v1","hunt-bills-v2"),ack.Replace("GillionsGameSyncTest","GillionsGameSync"),"{}","[]"})
            check(!PersonalSyncPolicy.Compatible(bad,"hunt_bills"),"incompatible acknowledgment fail closed");
        var receipt = JsonSerializer.Serialize(new {ok=true,snapshotId=Guid.NewGuid(),receivedAt=DateTime.UtcNow,unchanged=false});
        check(PersonalSyncPolicy.Receipt(receipt),"exact receipt accepted");
        check(!PersonalSyncPolicy.Receipt("{\"ok\":true}"),"partial success not ACK");
        for(int i=0;i<20;i++)check(PersonalSyncPolicy.RetrySeconds(i) is >=60 and <=900,"bounded backoff");
        var failedStore = new PersonalSyncState();
        var failedPrepared = PersonalSyncPolicy.Prepare(failedStore,owner,"hunt_bills",payload)!;
        string? durable = null; int dispatches=0;
        for(int i=0;i<3;i++) {
            var pending = PersonalSyncPolicy.Prepare(failedStore,owner,"hunt_bills",changed)!;
            if(PersonalSyncPolicy.PersistBeforeSend(() => throw new IOException("synthetic save failure")))dispatches++;
            check(pending.Nonce==failedPrepared.Nonce && pending.Payload==payload && dispatches==0,"save failure and next tick cannot dispatch unsaved snapshot");
        }
        if(PersonalSyncPolicy.PersistBeforeSend(() => durable=JsonSerializer.Serialize(failedStore)))dispatches++;
        var afterSave = JsonSerializer.Deserialize<PersonalSyncState>(durable!)!;
        check(dispatches==1 && afterSave.Prepared[0].Nonce==failedPrepared.Nonce && afterSave.Prepared[0].Payload==payload,"successful retry saves exact nonce/body before dispatch");
        foreach(var status in new[]{400,401,403,404,409,413,415})
            foreach(var ignoredBody in new[]{"","<html>error</html>","{broken",new string('x',65537)})
                {
                    var reads=0;
                    var success=PersonalSyncPolicy.ReadReceiptAsync(status, () => { reads++; throw new JsonException(ignoredBody); }).GetAwaiter().GetResult();
                    check(!success && reads==0 && PersonalSyncPolicy.TerminalStatus(status),"terminal status independent of invalid/empty/oversize body " + ignoredBody.Length);
                }
        foreach(var status in new[]{408,429,500,502,503,504})
            check(!PersonalSyncPolicy.TerminalStatus(status)
                && !PersonalSyncPolicy.ReadReceiptAsync(status, () => throw new JsonException("HTML transient body")).GetAwaiter().GetResult(),"transient response remains bounded retry");
        check(PersonalSyncPolicy.ReadReceiptAsync(201,()=>Task.FromResult(receipt)).GetAwaiter().GetResult(),"success alone reads validated receipt body");
    }
}
