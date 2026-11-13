using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;

internal static class PersonalUploadAndPairingPackagedTests {
    internal static async Task Run(Assembly assembly, bool testing) {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags methods = BindingFlags.Static | BindingFlags.NonPublic;
        Type TypeOf(string name) => assembly.GetType("GillionsGameSync." + name, true)!;
        void Check(bool condition, string failure) {
            if (!condition) throw new Exception("Packaged upload and pairing: " + failure);
        }

        var configType = TypeOf("PluginConfiguration");
        var config = Activator.CreateInstance(configType)!;
        var server = configType.GetProperty("ServerUrl")!;
        var notice = configType.GetProperty("PairingDestinationNotice")!;
        var apply = configType.GetMethod("ApplyPairingDestinationForVersion", instance)!;
        bool Install(string version, bool installedOrUpdated = false) => (bool)apply.Invoke(config, [version, testing, installedOrUpdated])!;
        server.SetValue(config, "https://test.gillions.app");
        Check(Install("1.2.3.4") != testing, "only Stable runs the install migration");
        Check((string)server.GetValue(config)! == (testing ? "https://test.gillions.app" : "https://gillions.app"),
            "Testing preserves its destination and Stable resets to the approved default");
        Check((string)notice.GetValue(config)! == (testing ? "" : "https://test.gillions.app"), "migration retains the old destination for the notice");
        notice.SetValue(config, "");
        server.SetValue(config, "https://gillions.lanlab.one");
        Check(!Install("1.2.3.4") && (string)server.GetValue(config)! == "https://gillions.lanlab.one"
            && (string)notice.GetValue(config)! == "", "same-version reload preserves a later change and produces no duplicate notice");
        Check(Install("1.2.3.5") != testing && (string)server.GetValue(config)! ==
            (testing ? "https://gillions.lanlab.one" : "https://gillions.app"), "each new Stable version applies the reset again");
        server.SetValue(config, "https://test.gillions.app");
        Check(Install("1.2.3.5", installedOrUpdated: true) != testing && (string)server.GetValue(config)! ==
            (testing ? "https://test.gillions.app" : "https://gillions.app"), "reinstalling the same Stable version applies the reset");

        var pluginType = TypeOf("Plugin");
        var plugin = RuntimeHelpers.GetUninitializedObject(pluginType);
        if (!testing) {
            var client = DispatchProxy.Create<IClientState, CollectibleServiceProxy>();
            ((CollectibleServiceProxy)client).Handler = (method, _) => method.Name == "get_IsLoggedIn";
            var chat = DispatchProxy.Create<IChatGui, CollectibleServiceProxy>();
            var notices = new List<string>();
            ((CollectibleServiceProxy)chat).Handler = (method, args) => {
                Check(method.Name == "Print", "the destination change uses a visible chat notice");
                notices.Add((string)args![0]!);
                return null;
            };
            pluginType.GetField("configuration", instance)!.SetValue(plugin, config);
            pluginType.GetField("clientState", instance)!.SetValue(plugin, client);
            pluginType.GetField("chatGui", instance)!.SetValue(plugin, chat);
            pluginType.GetField("savePolicy", instance)!.SetValue(plugin, Activator.CreateInstance(TypeOf("ObservationSavePolicy"), true));
            var show = pluginType.GetMethod("ShowPairingDestinationNotice", instance)!;
            show.Invoke(plugin, null);
            show.Invoke(plugin, null);
            Check(notices.SequenceEqual(["Pairing destination changed from https://test.gillions.app to https://gillions.app."])
                && (string)notice.GetValue(config)! == "", "the visible reset notice names both destinations and is consumed once");
        }
        var log = DispatchProxy.Create<IPluginLog, CollectibleServiceProxy>();
        var messages = new List<string>();
        ((CollectibleServiceProxy)log).Handler = (method, args) => {
            Check(method.Name == "Information" && args is ["{Code}", object[] { Length: 1 } values] && values[0] is string,
                "personal upload logs have exactly one code and use Information");
            messages.Add((string)((object[])args![1]!)[0]);
            return null;
        };
        pluginType.GetField("log", instance)!.SetValue(plugin, log);
        var read = pluginType.GetMethod("ReadPersonalUploadFailureAsync", instance)!;
        async Task<Exception?> Receive(HttpStatusCode status, string body) {
            using var response = new HttpResponseMessage(status) { Content = new StringContent(body) };
            var pending = (Task)read.Invoke(plugin, [response, CancellationToken.None])!;
            await pending;
            return (Exception?)pending.GetType().GetProperty("Result")!.GetValue(pending);
        }

        var rejected = await Receive(HttpStatusCode.BadRequest, """{"code":"INVALID_TRAVEL_CONTEXT","error":"Travel metadata is unsupported.","location":"private coordinates","token":"fixture-only secret"}""");
        Check(messages.SequenceEqual(["INVALID_TRAVEL_CONTEXT"]), "one rejection logs one server code and excludes all other fields");
        Check(await Receive(HttpStatusCode.OK, """{"ok":true}""") is null && messages.Count == 1, "successful uploads produce no Information diagnostics");
        var other = await Receive(HttpStatusCode.BadRequest, """{"code":"INVALID_TRAVEL_CONTEXT","error":"Unknown current world."}""");
        await Receive(HttpStatusCode.BadRequest, """{"code":"raw detail with spaces","error":"private"}""");
        Check(messages.SequenceEqual(["INVALID_TRAVEL_CONTEXT", "INVALID_TRAVEL_CONTEXT", "REJECTED"]),
            "each rejected response logs once and unstructured codes are withheld");

        var location = TypeOf("LocationUploadPresentation");
        var label = location.GetMethod("Label", methods)!;
        var refusal = location.GetMethod("Refusal", methods)!;
        Check((string)refusal.Invoke(null, [rejected])! == "Location: not accepted by the site (game version)"
            && (string)refusal.Invoke(null, [other])! == "", "only a confirmed build rejection reports a version problem");
        Check((string)label.Invoke(null, [false, "Location: sent"])! == "Location: sharing off"
            && (string)label.Invoke(null, [true, "Location: sent"])! == "Location: sent"
            && (string)label.Invoke(null, [null, "Location: sent"])! == "", "sharing off wins over stale receipts and unknown is not labelled off");

        var error = Activator.CreateInstance(TypeOf("GillionsHttpException"), ["REJECTED", "fixture-only secret", 401, null])!;
        var pairing = TypeOf("PublicUrlConfiguration").GetMethod("PairingFailure", methods)!;
        var failure = (string)pairing.Invoke(null, [error, "https://test.gillions.app"])!;
        Check(failure.Contains("already used on test.gillions.app", StringComparison.Ordinal)
            && !failure.Contains("fixture-only secret", StringComparison.Ordinal), "pairing failure uses the attempted host and safe copy");
        Console.WriteLine("Packaged location status, failure-only diagnostics and pairing destination migration PASS.");
    }
}
