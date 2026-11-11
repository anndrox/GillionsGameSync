using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Runtime.CompilerServices;

internal static class PermissionPackagedTests {
    internal static void Run(Assembly assembly) {
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        var type=assembly.GetType("GillionsGameSync.PermissionAuthority",true)!;
        var instance=Activator.CreateInstance(type,true)!;
        object? Call(string name,params object?[] values)=>type.GetMethod(name,flags)!.Invoke(instance,values);
        int count=0;
        void Check(bool value,string name) { count++;if(!value)throw new Exception("Packaged permission: "+name); }
        const string device="00000000-0000-4000-8000-000000000001";
        var issued=new DateTime(2026,10,7,12,0,1,DateTimeKind.Utc);
        var keys=(string[])type.GetField("Keys",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
        JsonObject Fixture(bool explicitDecision,bool enabled) {
            var decisions=new JsonObject();
            foreach(var key in keys) decisions[key]=new JsonObject {
                ["scope"]=key=="marketContribution"?"account":"device",["state"]=explicitDecision?"explicit":"legacy",
                ["enabled"]=explicitDecision?JsonValue.Create(enabled):null,
                ["generation"]=explicitDecision?"00000000-0000-4000-8000-000000000002":null,
                ["decidedAt"]=explicitDecision?issued.AddSeconds(-1).ToString("O"):null};
            return new JsonObject {["ok"]=true,["permissionAuthority"]=new JsonObject {
                ["revision"]=1,["deviceBinding"]=new JsonObject {["deviceId"]=device,["pairedAt"]=issued.AddDays(-1).ToString("O"),["characterId"]=100},
                ["issuedAt"]=issued.ToString("O"),["expiresAt"]=issued.AddSeconds(30).ToString("O"),["permissions"]=decisions}};
        }
        Call("Bind","synthetic-enrollment:content",device);
        Check((bool)Call("Apply",Fixture(false,false).ToJsonString(),issued,0L,null)!,"legacy authority accepted");
        foreach(var key in keys) {
            Check(!(bool)Call("Allows",key,false,0L)!,key+" historical OFF preserved");
            Check((bool)Call("Allows",key,true,0L)!,key+" historical ON preserved");
        }
        Check((bool)Call("Apply",Fixture(true,true).ToJsonString(),issued,0L,null)!,"explicit authority accepted");
        foreach(var key in keys)Check((bool)Call("Allows",key,false,0L)!,key+" fresh explicit ON supersedes local OFF");
        var mixed=Fixture(true,true);
        mixed["permissionAuthority"]!["permissions"]!["itemLinks"]!["enabled"]=false;
        mixed["permissionAuthority"]!["permissions"]!["automaticHuntMaps"]!["enabled"]=false;
        Check((bool)Call("Apply",mixed.ToJsonString(),issued,0L,null)!,"mixed authority accepted");
        Check(!(bool)Call("Allows","itemLinks",true,0L)!&&(bool)Call("PartyFinderLinks",false,false,0L)!,"PF receiving independent of item OFF");
        Check(!(bool)Call("Allows","automaticHuntMaps",true,0L)!&&(bool)Call("HuntReceiving",false,0L)!,"manual Hunt eligible while automatic OFF");
        foreach(var key in keys)Check(!(bool)Call("Allows",key,true,30000L)!,key+" exact lease expiry");
        Call("Invalidate");Check(!(bool)Call("HuntReceiving",true,0L)!,"missing/revoked lease closes presentation");
        var configuration=assembly.GetType("GillionsGameSync.PluginConfiguration",true)!;
        Check(!configuration.GetProperties().Any(p=>p.Name.Contains("PermissionAuthority")||p.Name.Contains("PermissionGeneration")),"authority/generations are not configuration");
        if(assembly.GetType("GillionsGameSync.PersonalSyncPolicy") is not null) {
            var pluginType=assembly.GetType("GillionsGameSync.Plugin",true)!;
            var plugin=RuntimeHelpers.GetUninitializedObject(pluginType);
            var config=Activator.CreateInstance(configuration)!;
            Check(!(bool)configuration.GetProperty("EnableGillionsPartyFinderContributions")!.GetValue(config)!,
                "new pairing starts with Party Finder contribution off");
            Call("Bind","new:pairing",device);
            Call("Apply",Fixture(false,false).ToJsonString(),issued,0L,null);
            Check(!(bool)Call("Allows","partyFinderContribution",false,0L)!, "legacy Party Finder default off");
            Call("Apply",Fixture(true,false).ToJsonString(),issued,0L,null);
            Check(!(bool)Call("Allows","partyFinderContribution",true,0L)!, "site off overrides a previous local on");
            var sessionType=assembly.GetType("GillionsGameSync.PairedSession",true)!;
            var session=Activator.CreateInstance(sessionType,[1,"https://test.gillions.app",device,"enrollment",new string('a',64)])!;
            configuration.GetProperty("ActiveSession")!.SetValue(config,session);
            pluginType.GetField("configuration",flags)!.SetValue(plugin,config);
            pluginType.GetField("permissionAuthority",flags)!.SetValue(plugin,instance);
            if (pluginType.GetMethod("ApplySharingStatus", flags) is { } applySharingStatus) {
                var queue = Activator.CreateInstance(assembly.GetType("GillionsGameSync.AppearanceQueue", true)!, true)!;
                var retry = Activator.CreateInstance(assembly.GetType("GillionsGameSync.ResourceRetry", true)!, true)!;
                using var cancellation = new CancellationTokenSource();
                pluginType.GetField("appearanceQueue", flags)!.SetValue(plugin, queue);
                pluginType.GetField("appearanceRetry", flags)!.SetValue(plugin, retry);
                pluginType.GetField("appearanceCancellation", flags)!.SetValue(plugin, cancellation);
                pluginType.GetField("appearanceGrant", flags)!.SetValue(plugin, "00000000-0000-4000-8000-000000000002");
                pluginType.GetField("appearanceCharacterId", flags)!.SetValue(plugin, 100L);
                var permitType = assembly.GetType("GillionsGameSync.SyncRequestPermit", true)!;
                var mode = Enum.Parse(assembly.GetType("GillionsGameSync.SyncRequestMode", true)!, "Personal");
                var permit = Activator.CreateInstance(permitType, flags, null,
                    [0L, 0L, mode, 123UL, session, "https://test.gillions.app", "", CancellationToken.None], null)!;
                var off = Fixture(true, false);
                off["characterAppearance"] = new JsonObject { ["enabled"] = false };
                applySharingStatus.Invoke(plugin, [off.ToJsonString(), permit]);
                Check(cancellation.IsCancellationRequested && pluginType.GetField("appearanceGrant", flags)!.GetValue(plugin) is null,
                    "actual presence commit cancels the in-flight appearance grant immediately on off");
                Call("Apply", Fixture(true, true).ToJsonString(), issued, Environment.TickCount64, null);
                TestAppearanceReenable(assembly, plugin, queue, applySharingStatus, permit, Check);
            }
            var record=pluginType.GetMethod("RecordContributionDenial",flags)!;
            var hash=assembly.GetType("GillionsGameSync.PersonalSyncPolicy",true)!.GetMethod("Hash",BindingFlags.NonPublic|BindingFlags.Static)!;
            foreach(var key in new[]{"marketContribution","partyFinderContribution"}) {
                Call("Bind","enrollment:character",device);
                Call("Apply",Fixture(true,true).ToJsonString(),issued,0L,null);
                var known=(string)Call("KnownIdentity",key)!;
                var fingerprint=(string)hash.Invoke(null,["enrollment:"+known])!;
                bool Record(string captured,string existing="")=>(bool)record.Invoke(plugin,[key,"enrollment",captured,existing])!;
                Check(Record(fingerprint),key+" actual Plugin callback preserves expired decision denial");
                Call("Invalidate");Check(Record(fingerprint),key+" actual Plugin callback after malformed presence");
                Call("Bind","","");Check(Record(fingerprint),key+" actual Plugin callback after logout drops RAM");
                Check(!Record(fingerprint,"newer-stop"),key+" actual Plugin callback preserves conflicting durable stop after logout");
                Call("Bind","enrollment:character",device);
                var newer=Fixture(true,true);
                newer["permissionAuthority"]!["permissions"]![key]!["generation"]="00000000-0000-4000-8000-000000000003";
                Call("Apply",newer.ToJsonString(),issued,0L,null);
                Check(!Record(fingerprint),key+" actual Plugin callback cannot apply old denial to newer decision");
            }
        }
        Console.WriteLine($"Exact packaged permission authority PASS: {count} checks; synthetic, no game/HTTP invocation.");
    }

    private static void TestAppearanceReenable(Assembly assembly, object plugin, object queue,
        MethodInfo applySharingStatus, object permit, Action<bool, string> check) {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
        const string generation = "00000000-0000-4000-8000-000000000002";
        const string newGeneration = "00000000-0000-4000-8000-000000000003";
        var pluginType = plugin.GetType();
        var queueType = queue.GetType();
        var observe = queueType.GetMethod("Observe", instance)!;
        var accepted = queueType.GetMethod("Accepted", instance)!;
        var sampleDue = pluginType.GetMethod("AppearanceSampleDue", instance)!;
        var take = pluginType.GetMethod("TakeAppearanceUpload", instance)!;
        var grantField = pluginType.GetField("appearanceGrant", instance)!;
        var cancellationField = pluginType.GetField("appearanceCancellation", instance)!;
        using var initialSource = new CancellationTokenSource();
        cancellationField.SetValue(plugin, initialSource);

        string[] categories = ["marketContribution", "partyFinderContribution", "fateContribution", "personalHunts",
            "huntRoutingLocation", "automaticHuntMaps", "personalSubmarines", "itemLinks", "partyFinderLinks", "appearance"];
        void Presence(string state, string grant) {
            var body = JsonSerializer.Serialize(new {
                consentStatus = new {
                    version = 1, characterId = 100, issuedAt = "2026-11-11T18:00:00Z",
                    settingsUrl = "https://test.gillions.app/gillions-sync",
                    categories = categories.Select(key => new {
                        key, label = key, state = key == "appearance" ? state : "not-set", changeOn = "site",
                    }),
                },
                // Keep the descriptor's generation unchanged to test the
                // explicit consent transition independently of generation rotation.
                characterAppearance = new {
                    contract = "character-appearance-v1", resourceType = "character_appearance",
                    capability = "character_appearance_v1", permission = "server:game-sync:personal:character-appearance:v1",
                    maxPayloadBytes = 16384, maxEnvelopeBytes = 20480, characterId = 100, enabled = true, generation = grant,
                },
            });
            applySharingStatus.Invoke(plugin, [body, permit]);
        }

        var snapshotType = assembly.GetType("GillionsGameSync.AppearanceSnapshot", true)!;
        var slots = (string[])assembly.GetType("GillionsGameSync.AppearancePolicy", true)!
            .GetField("Slots", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var look = JsonSerializer.Deserialize(JsonSerializer.Serialize(new {
            CustomizationHex = new string('0', 52),
            ObservedAtUtc = "2026-11-11T18:00:00.000Z",
            Equipment = slots.ToDictionary(key => key, _ => new { ItemId = 123u, Stain0Id = 4 }),
        }), snapshotType)!;
        object? Sample(DateTime at) {
            if (grantField.GetValue(plugin) is null) return null;
            if ((bool)sampleDue.Invoke(plugin, [at])!) observe.Invoke(queue, [look]);
            return take.Invoke(plugin, [at]);
        }
        string Field(object upload, string name) => (string)upload.GetType().GetProperty(name, instance)!.GetValue(upload)!;

        var now = DateTime.UtcNow;
        Presence("on", generation);
        var first = Sample(now) ?? throw new Exception("Initial look did not queue an upload");
        accepted.Invoke(queue, [first]);
        // Simulate an old upload whose cancellation continuation has not run.
        using var tokenSource = new CancellationTokenSource();
        cancellationField.SetValue(plugin, tokenSource);
        pluginType.GetField("appearanceFlight", instance)!.SetValue(plugin, tokenSource.Token);
        Presence("off", generation);
        check(tokenSource.IsCancellationRequested && Sample(now.AddSeconds(1)) is null,
            "explicit consent Off cancels uploads even if the same descriptor is present");

        var resumedAt = now.AddSeconds(2);
        Presence("on", generation);
        var resumed = Sample(resumedAt) ?? throw new Exception("Unchanged look did not queue on the first sample after On");
        check(Field(resumed, "Hash") == Field(first, "Hash") && Field(resumed, "Nonce") != Field(first, "Nonce"),
            "Off to On uploads the unchanged look with a new nonce on the next sample");
        accepted.Invoke(queue, [resumed]);
        int uploads = 1;
        for (int seconds = 1; seconds <= 5; seconds++) {
            Presence("on", generation);
            if (Sample(resumedAt.AddSeconds(seconds)) is not null) uploads++;
        }
        check(uploads == 1, "exactly one upload in the first sample interval after re-enable");

        Presence("on", newGeneration);
        var renewed = Sample(resumedAt.AddSeconds(6)) ?? throw new Exception("New grant generation did not queue an unchanged look");
        check(Field(renewed, "Hash") == Field(resumed, "Hash") && Field(renewed, "Nonce") != Field(resumed, "Nonce"),
            "grant generation change uploads the unchanged look without a reconnect");
        accepted.Invoke(queue, [renewed]);
        check(Sample(resumedAt.AddSeconds(11)) is null, "fresh grant resumes normal hash deduplication");
    }
}
