using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using Lumina;
using Lumina.Data;

internal static class DutyPausePackagedTests {
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;

    internal static void Run(Assembly assembly) {
        Type TypeOf(string name) => assembly.GetType("GillionsGameSync." + name, true)!;
        var pluginType = TypeOf("Plugin");
        var plugin = RuntimeHelpers.GetUninitializedObject(pluginType);
        void Set(string name, object value) => pluginType.GetField(name, Instance)!.SetValue(plugin, value);
        object? Call(object target, string name, params object?[] args) => target.GetType().GetMethod(name, Instance)!.Invoke(target, args);
        bool Gate(string name) => (bool)Call(plugin, name)!;
        bool Property(string name) => (bool)pluginType.GetProperty(name, Instance)!.GetValue(plugin)!;
        int checks = 0;
        void Check(bool pass, string reason) {
            if (!pass) throw new Exception("Packaged duty pause: " + reason);
            checks++;
        }

        var now = DateTime.UtcNow;
        const string device = "00000000-0000-4000-8000-000000000001";
        const string synthetic = "SYNTHETIC_DEVICE_CREDENTIAL_0000000000";
        const ulong contentId = 100;
        var config = Activator.CreateInstance(TypeOf("PluginConfiguration"))!;
        var session = TypeOf("PairedSession").GetMethod("Create")!.Invoke(null, ["https://test.gillions.app", device, synthetic])!;
        config.GetType().GetProperty("ActiveSession")!.SetValue(config, session);
        config.GetType().GetProperty("DeviceId")!.SetValue(config, device);
        config.GetType().GetProperty("DeviceToken")!.SetValue(config, synthetic);
        config.GetType().GetProperty("PairingRequired")!.SetValue(config, false);
        Set("configuration", config);
        Set("activeOwnedState", Activator.CreateInstance(TypeOf("OwnedCharacterState"))!);
        Set("activeRetainerCharacterContentId", contentId);

        var authority = Activator.CreateInstance(TypeOf("PermissionAuthority"), true)!;
        var keys = (string[])authority.GetType().GetField("Keys", Static)!.GetValue(null)!;
        var permissions = keys.ToDictionary(key => key, key => new {
            scope = key == "marketContribution" ? "account" : "device", state = "explicit", enabled = true,
            generation = "00000000-0000-4000-8000-000000000002", decidedAt = now.AddSeconds(-1).ToString("O")
        });
        var presence = JsonSerializer.Serialize(new {
            ok = true, permissionAuthority = new {
                revision = 1, deviceBinding = new { deviceId = device, pairedAt = now.AddDays(-1).ToString("O"), characterId = contentId },
                issuedAt = now.ToString("O"), expiresAt = now.AddSeconds(30).ToString("O"), permissions
            }
        });
        Call(authority, "Bind", "synthetic-enrollment", device);
        Check((bool)Call(authority, "Apply", presence, now, Environment.TickCount64, null)!, "synthetic permission lease");
        Set("permissionAuthority", authority);

        ConditionFlag? activeFlag = null;
        var reads = new List<ConditionFlag>();
        var conditions = Service<ICondition>((_, args) => {
            var flag = (ConditionFlag)args![0]!;
            reads.Add(flag);
            return flag == activeFlag;
        });
        var localPlayer = RuntimeHelpers.GetUninitializedObject(typeof(IPlayerCharacter).Assembly
            .GetType("Dalamud.Game.ClientState.Objects.SubKinds.PlayerCharacter", true)!);
        var client = Service<IClientState>((method, _) => method.Name == "get_IsLoggedIn");
        Set("marketConditions", conditions);
        Set("clientState", client);
        Set("objects", Service<IObjectTable>((_, _) => localPlayer));
        Set("huntFocus", Activator.CreateInstance(TypeOf("HuntFocusState"), true)!);
        Set("huntMapCancellation", new CancellationTokenSource());
        var testing = assembly.GetName().Name == "GillionsGameSyncTest";
        var huntDefer = testing ? Activator.CreateInstance(TypeOf("HuntDeferState"), true)! : null;
        if (huntDefer is not null) Set("huntDefer", huntDefer);
        var travelSync = Activator.CreateInstance(TypeOf("TravelSyncState"), true)!;
        Set("travelSync", travelSync);
        Set("travelAccepted", true);
        Set("travelBinding", "binding");
        Set("travelRetries", Activator.CreateInstance(TypeOf("ResourceRetry"), true)!);
        Set("nextRetainerPresenceUtc", DateTime.MaxValue);

        var compatibility = TypeOf("NativeCompatibility");
        var travelFamily = Enum.Parse(TypeOf("NativeFamily"), "Travel");
        var builds = (Array)compatibility.GetMethod("Qualified", Static)!.Invoke(null,[travelFamily])!;
        var loadedVersion = typeof(IFramework).Assembly.GetName().Version!.ToString();
        var runtime = builds.Cast<object>().Single(b => (string)b.GetType().GetProperty("DalamudVersion")!.GetValue(b)! == loadedVersion);
        string RuntimeValue(string field) => (string)runtime.GetType().GetProperty(field)!.GetValue(runtime)!;
        var gameBuild = RuntimeValue("GameBuild");
        var sdkVersion = RuntimeValue("ClientStructsVersion");
        // Supply a supported game version without opening local game files or reading native state.
        var gameData = (GameData)RuntimeHelpers.GetUninitializedObject(typeof(GameData));
        var repo = (Repository)RuntimeHelpers.GetUninitializedObject(typeof(Repository));
        typeof(Repository).GetField("<Version>k__BackingField", Instance)!.SetValue(repo, gameBuild);
        typeof(GameData).GetField("<Repositories>k__BackingField", Instance)!.SetValue(gameData, new Dictionary<string, Repository> { ["ffxiv"] = repo });
        var travelType = TypeOf("TravelContextLocalView");
        var travel = RuntimeHelpers.GetUninitializedObject(travelType);
        var travelState = Activator.CreateInstance(TypeOf("TravelContextState"), true)!;
        travelType.GetField("state", Instance)!.SetValue(travel, travelState);
        travelType.GetField("nativeVersion", Instance)!.SetValue(travel, sdkVersion);
        travelType.GetField("client", Instance)!.SetValue(travel, client);
        travelType.GetField("conditions", Instance)!.SetValue(travel, conditions);
        travelType.GetField("player", Instance)!.SetValue(travel, Service<IPlayerState>((method, _) =>
            method.Name == "get_ContentId" ? contentId : method.Name == "get_IsLoaded"));
        travelType.GetField("data", Instance)!.SetValue(travel, new UnavailableCollectibleData(gameData));
        // Leave the view's invalidation event unwired to exercise the duty-entry clear directly.
        Call(travel, "SetEnabled", true);
        Set("travelLocal", travel);

        var observationType = TypeOf("TravelObservation");
        var observation = Activator.CreateInstance(observationType,
            [100u, 200u, 10.1, 20.2, now, gameBuild, sdkVersion, "1.0.0", "UNAVAILABLE", Array.CreateInstance(TypeOf("TravelDestination"), 0)])!;
        if (testing) observationType.GetProperty("CurrentWorldId")!.SetValue(observation, 79u);
        var flags = new[] { ConditionFlag.BoundByDuty, ConditionFlag.BoundByDuty56, ConditionFlag.BoundByDuty95 };
        var pendingProperty = travelSync.GetType().GetProperty("Pending", Instance)!;
        var travelInFlight = pluginType.GetField("travelInFlight", Instance)!;
        Check(Gate("HuntFocusEligible") && Property("HuntReceivingEnabled") && Gate("TravelAdmitted"), "admission before duty");
        foreach (var flag in flags) {
            Check((bool)Call(travelState, "Observe", contentId, observation, now)!, "fresh location available before duty");
            var pending = Call(travelSync, "Prepare", "binding", observation, now)!;
            var pendingLifetime = (CancellationTokenSource)pending.GetType().GetProperty("Lifetime", Instance)!.GetValue(pending)!;
            var travelCancellation = pendingLifetime.Token;
            var mapCancellation = ((CancellationTokenSource)pluginType.GetField("huntMapCancellation", Instance)!.GetValue(plugin)!).Token;
            if (huntDefer is not null) Call(huntDefer, "Consumed", Activator.CreateInstance(TypeOf("HuntMapRequest"), true)!);
            reads.Clear();
            activeFlag = flag;
            Call(plugin, "UpdateDutyPause");
            Check(reads.SequenceEqual(flags.Take(Array.IndexOf(flags, flag) + 1)), $"one sampled check for {flag}");
            Check(!Gate("HuntFocusEligible") && !Property("HuntReceivingEnabled") && !Gate("TravelAdmitted"), $"admission paused for {flag}");
            Check(mapCancellation.IsCancellationRequested, "in-flight Hunt map cancelled");
            if (huntDefer is not null) Check(!(bool)huntDefer.GetType().GetProperty("CanDefer", Instance)!.GetValue(huntDefer)!,
                "duty entry retires Hunt presentation for the consumed command");
            Check(travelCancellation.IsCancellationRequested && pendingProperty.GetValue(travelSync) is null,
                "pending travel cleared and cancelled");
            Call(plugin, "TickTravelSync", now);
            Check((string)pluginType.GetField("travelStatus", Instance)!.GetValue(plugin)! == "Paused while in a duty.", "paused travel status");
            Check(Property("HasPairedSession") && Property("WebsiteLinksEnabled") && Property("PartyFinderLinksEnabled")
                && Property("PartyFinderContributionEnabled") && Property("MarketEnabled") && Property("TravelEnabled")
                && (bool)Call(plugin, "PersonalEnabled", "hunt_bills")! && (bool)Call(plugin, "PersonalEnabled", "submarine_personal")!,
                "ordinary presence, item links, Party Finder, market and personal sync permissions unchanged");
            activeFlag = null;
            Call(plugin, "UpdateDutyPause");
            Check(Gate("HuntFocusEligible") && Property("HuntReceivingEnabled") && Gate("TravelAdmitted"), $"admission resumes after {flag}");
            // The observation would still be fresh after this short duty. A resumed tick must not prepare or dispatch it.
            Call(plugin, "TickTravelSync", now.AddSeconds(1));
            Check(pendingProperty.GetValue(travelSync) is null && !(bool)travelInFlight.GetValue(plugin)!,
                "pre-duty location never dispatches on resume");
        }
        ((CancellationTokenSource)pluginType.GetField("huntMapCancellation", Instance)!.GetValue(plugin)!).Dispose();

        if (assembly.GetName().Name == "GillionsGameSync") {
            using var stream = File.OpenRead(assembly.Location);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            var literals = new List<string>();
            for (var handle = metadata.GetNextHandle(default(UserStringHandle)); !handle.IsNil; handle = metadata.GetNextHandle(handle)) {
                literals.Add(metadata.GetUserString(handle));
            }
            Check(literals.Any(text => text == "Gillions received your current location.")
                && literals.Any(text => text.Contains("HTTPS pairing and its separate travel grant", StringComparison.Ordinal))
                && literals.Any(text => text.Contains("Gillions paused location updates.", StringComparison.Ordinal)), "compiled public travel status strings present");
            Check(literals.Any(text => text.StartsWith("Awaiting compatible Gillions permission", StringComparison.Ordinal))
                && literals.Any(text => text.StartsWith("Private sync requires compatible Gillions HTTPS pairing", StringComparison.Ordinal))
                && literals.Any(text => text.StartsWith(": Receipt accepted by Gillions", StringComparison.Ordinal)), "compiled public personal status strings present");
            // The protocol refusal code and the shared window title's Testing badge are never public status text.
            var unexpected = literals.Where(text => text.Contains("TEST", StringComparison.Ordinal)
                && text is not ("TESTING_PRODUCT_REQUIRED" or " [TESTING]")).ToArray();
            Check(unexpected.Length == 0, "compiled public status strings contain no TEST. Unexpected strings: " + string.Join(" | ", unexpected));
        }
        Console.WriteLine($"Packaged duty pause PASS: {checks} checks ({assembly.GetName().Name}).");
    }

    private static T Service<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class {
        var service = DispatchProxy.Create<T, CollectibleServiceProxy>();
        ((CollectibleServiceProxy)(object)service).Handler = handler;
        return service;
    }
}
