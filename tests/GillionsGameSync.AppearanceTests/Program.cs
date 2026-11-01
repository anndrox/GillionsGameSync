using GillionsGameSync;
using System.Text.Json;
var checks=0;
void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
AppearanceSnapshot Fixture(uint glamour=20)=>new(){CollectorVersion="0.0.97.0",GameVersion="2026.10.16.0000.0000",ClientStructsVersion="7.57.0.9414",ObservedAtUtc="2026-11-01T12:00:00.000Z",CustomizationHex="010001"+new string('0',46),Equipment=AppearancePolicy.Slots.ToDictionary(s=>s,s=>new AppearanceSlot{ItemId=10,GlamourItemId=glamour,Stain0Id=2,Stain1Id=3})};
var p=Fixture();Check(AppearancePolicy.Valid(p),"complete fixture");
Check(!new AppearanceChoice().Enabled&&!new AppearanceChoice().Pending,"default OFF");
Check(AppearancePolicy.Indices.SequenceEqual(new[]{0,1,2,3,4,6,7,8,9,10,11,12}),"Amendment 1 exact mapping");
Check(AppearancePolicy.Slots[^2]=="ring1"&&AppearancePolicy.Slots[^1]=="ring2","neutral ring identities");
Check(p.Equipment["mainHand"].ItemId==10&&p.Equipment["mainHand"].GlamourItemId==20,"base and glamour retained");
Check(p.Equipment["ring1"].Stain0Id==2&&p.Equipment["ring1"].Stain1Id==3,"both stains");
var q=new AppearanceQueue();q.Observe(p);var first=q.Pending!;Check(first!=null,"login queues full snapshot");q.Observe(p);Check(ReferenceEquals(first,q.Pending),"unchanged retry retains nonce");
q.Observe(Fixture(21));var latest=q.Pending!;Check(latest.Hash!=first!.Hash,"latest replaces unsent");q.Accepted(first);Check(ReferenceEquals(latest,q.Pending),"old receipt cannot discard latest");q.Accepted(latest);q.Observe(Fixture(21));Check(q.Pending==null,"unchanged does not upload again");q.Clear();q.Observe(p);Check(q.Pending!=null,"new login can upload");q.Clear();Check(q.Pending==null,"opt-out clears RAM");
p.Equipment["ring1"]=new();Check(AppearancePolicy.Valid(p),"observed empty slot accepted");p.Equipment["ring1"]=new(){Stain1Id=1};Check(!AppearancePolicy.Valid(p),"unreadable cannot be empty");p.Equipment.Remove("ring2");Check(!AppearancePolicy.Valid(p),"incomplete refused");
foreach(var b in NativeCompatibility.Qualified(NativeFamily.Appearance))Check(NativeCompatibility.Supports(NativeFamily.Appearance,b.GameBuild,b.ClientStructsVersion),"qualified tuple");
Check(!NativeCompatibility.Supports(NativeFamily.Appearance,"2026.12.01.0000.0000","7.57.0.9414"),"patch fail closed");
Check(!AppearancePolicy.Explanation.Contains('—'),"player copy");
var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
var collector=File.ReadAllText(Path.Combine(root,"CharacterAppearanceCollector.cs"));
Check(collector.Contains("objects.LocalPlayer")&&!collector.Contains("foreach(var actor"),"local player only");
foreach(var forbidden in new[]{"->DrawData","SetVisor(","LoadEquipment(","SetGlasses(","MirageManager","GetGameObjectAddress"})Check(!collector.Contains(forbidden),"no forbidden surface "+forbidden);
foreach(var state in new[]{"BetweenAreas","BetweenAreas51","LoggingOut","Transformed","RolePlaying","Disguised","IsGPosing"})Check(collector.Contains(state),"withhold "+state);
var integration=File.ReadAllText(Path.Combine(root,"PluginAppearance.cs"));Check(integration.StartsWith("#if GILLIONS_TEST_BUILD"),"Testing only");Check(integration.IndexOf("!AppearanceAuthorized(now)",StringComparison.Ordinal)<integration.IndexOf("appearanceCollector.Read(",StringComparison.Ordinal),"zero appearance reads without opt-in/grant");
if(args.Length==2&&args[0]=="--fixture"){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);File.WriteAllText(args[1],JsonSerializer.Serialize(Fixture(),AppearancePolicy.Json));}
Console.WriteLine($"Appearance fixtures: {checks} passed.");
