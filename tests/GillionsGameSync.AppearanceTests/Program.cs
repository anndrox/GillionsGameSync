using GillionsGameSync;
using System.Text.Json;
var checks=0;
void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
AppearanceSnapshot Fixture(uint glamour=20)=>new(){CollectorVersion="0.0.97.0",GameVersion="2026.10.16.0000.0000",ClientStructsVersion="7.57.0.9414",ObservedAtUtc="2026-11-01T12:00:00.000Z",CustomizationHex="010001"+new string('0',46),Equipment=AppearancePolicy.Slots.ToDictionary(s=>s,s=>new AppearanceSlot{ItemId=10,GlamourItemId=glamour,Stain0Id=2,Stain1Id=3})};
var p=Fixture();Check(AppearancePolicy.Valid(p),"complete fixture");
Check(!new AppearanceChoice().Enabled&&!new AppearanceChoice().Pending,"default OFF");
Check(AppearancePolicy.Slots[^2]=="ring1"&&AppearancePolicy.Slots[^1]=="ring2","neutral ring identities");
Check(p.Equipment["mainHand"].ItemId==10&&p.Equipment["mainHand"].GlamourItemId==20,"base and glamour retained");
Check(p.Equipment["ring1"].Stain0Id==2&&p.Equipment["ring1"].Stain1Id==3,"both stains");
var q=new AppearanceQueue();q.Observe(p);var first=q.Pending!;Check(first!=null,"login queues full snapshot");q.Observe(p);Check(ReferenceEquals(first,q.Pending),"unchanged retry retains nonce");
q.Observe(Fixture(21));var latest=q.Pending!;Check(latest.Hash!=first!.Hash,"latest replaces unsent");q.Accepted(first);Check(ReferenceEquals(latest,q.Pending),"old receipt cannot discard latest");q.Accepted(latest);q.Observe(Fixture(21));Check(q.Pending==null,"unchanged does not upload again");q.Clear();q.Observe(p);Check(q.Pending!=null,"new login can upload");q.Clear();Check(q.Pending==null,"opt-out clears RAM");
p.Equipment["ring1"]=new();Check(AppearancePolicy.Valid(p),"observed empty slot accepted");p.Equipment["ring1"]=new(){Stain1Id=1};Check(!AppearancePolicy.Valid(p),"unreadable cannot be empty");p.Equipment.Remove("ring2");Check(!AppearancePolicy.Valid(p),"incomplete refused");
foreach(var b in NativeCompatibility.Qualified(NativeFamily.Appearance))Check(NativeCompatibility.Supports(NativeFamily.Appearance,b.GameBuild,b.ClientStructsVersion),"qualified tuple");
Check(!NativeCompatibility.Supports(NativeFamily.Appearance,"2026.12.01.0000.0000","7.57.0.9414"),"patch fail closed");
var dyed = new HashSet<string> { "head", "body", "hands", "feet" };
foreach (var key in AppearancePolicy.Slots) {
    byte firstDye = dyed.Contains(key) ? (byte)2 : (byte)0;
    byte secondDye = dyed.Contains(key) ? (byte)3 : (byte)0;
    var slot = AppearancePolicy.FromDrawSlot(10, 20, new(100, firstDye, secondDye), !dyed.Contains(key), dyed.Contains(key));
    Check(slot?.ItemId == 10 && slot.GlamourItemId == (dyed.Contains(key) ? 20u : 0u)
        && slot.Stain0Id == firstDye && slot.Stain1Id == secondDye, "drawn model and dyes for " + key);
}
Check(AppearancePolicy.FromDrawSlot(10, 20, new(100, 1, 2), false, false) is null, "unrecognised drawn model withheld");
var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
var collector=File.ReadAllText(Path.Combine(root,"CharacterAppearanceCollector.cs"));
Check(collector.Contains("objects.LocalPlayer")&&!collector.Contains("foreach(var actor"),"local player only");
foreach(var forbidden in new[]{"SetVisor(","LoadEquipment(","SetGlasses(","MirageManager","GetGameObjectAddress"})Check(!collector.Contains(forbidden),"no forbidden surface "+forbidden);
foreach(var state in new[]{"BetweenAreas","BetweenAreas51","LoggingOut","Transformed","RolePlaying","Disguised","IsGPosing"})Check(collector.Contains(state),"withhold "+state);
var integration=File.ReadAllText(Path.Combine(root,"PluginAppearance.cs"));Check(integration.StartsWith("#if GILLIONS_TEST_BUILD"),"Testing only");Check(integration.IndexOf("!AppearanceAuthorized(now)",StringComparison.Ordinal)<integration.IndexOf("appearanceCollector.Read(",StringComparison.Ordinal),"zero appearance reads without opt-in/grant");
Check(collector.Contains("human->EquipmentModels") && collector.Contains("DrawData.DrawObject")
    && !collector.Contains("item->Stains") && !collector.Contains("draw->EquipmentModelIds"), "dyes from rendered equipment");
Check(!integration.Contains("Checkbox") && !integration.Contains("/api/game-sync/appearance-permission")
    && integration.Contains("ConsentStatus.Read") && integration.Contains("AppearanceGrant.Read"), "site owns sharing");
var keys = new[] { "marketContribution", "partyFinderContribution", "fateContribution", "personalHunts",
    "huntRoutingLocation", "automaticHuntMaps", "personalSubmarines", "itemLinks", "partyFinderLinks", "appearance" };
const string origin = "https://test.gillions.app";
const string grant = "00000000-0000-4000-8000-000000000002";
string Response(bool appearanceEnabled, string url) => JsonSerializer.Serialize(new {
    consentStatus = new {
        version = 1, characterId = 123, issuedAt = "2026-11-11T00:00:00Z", settingsUrl = url,
        categories = keys.Select((key, i) => new {
            key, label = "Exact site label " + i, changeOn = "site",
            state = key == "appearance" && appearanceEnabled ? "on" : key == "partyFinderContribution" ? "off" : "not-set",
            generation = key == "appearance" ? grant : null, decidedAt = (string?)null
        })
    },
    characterAppearance = new {
        contract = AppearancePolicy.Contract, resourceType = AppearancePolicy.Resource, capability = AppearancePolicy.Capability,
        permission = AppearancePolicy.Permission, maxPayloadBytes = 16384, maxEnvelopeBytes = 20480,
        characterId = 123, enabled = appearanceEnabled, generation = grant
    }
});
using var enabled = JsonDocument.Parse(Response(true, origin + "/gillions-sync"));
var consent = ConsentStatus.Read(enabled.RootElement, origin);
Check(consent?.SettingsUrl == origin + "/gillions-sync" && consent.Categories.Select(row => row.Key).SequenceEqual(keys)
    && consent.Categories[9].Label == "Exact site label 9" && consent.Categories[9].DisplayState == "On"
    && consent.Categories[1].DisplayState == "Off" && consent.Categories[0].DisplayState == "Not set", "read-only status uses the site rows, labels and address");
Check(AppearanceGrant.Read(enabled.RootElement) == new AppearanceGrant(123, grant), "upload grant comes from the descriptor");
using var off = JsonDocument.Parse(Response(false, origin + "/gillions-sync"));
Check(AppearanceGrant.Read(off.RootElement) is null, "off descriptor immediately drops the grant");
using var wrongUrl = JsonDocument.Parse(Response(true, "https://example.invalid/gillions-sync"));
Check(ConsentStatus.Read(wrongUrl.RootElement, origin) is null && AppearanceGrant.Read(wrongUrl.RootElement) is not null,
    "display status does not grant or revoke appearance");
if(args.Length==2&&args[0]=="--fixture"){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);File.WriteAllText(args[1],JsonSerializer.Serialize(Fixture(),AppearancePolicy.Json));}
Console.WriteLine($"Appearance fixtures: {checks} passed.");
