using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using Dalamud.Game.Text.SeStringHandling.Payloads;

internal static class MapFlagPackagedTests {
    internal static void Run(Assembly assembly) {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags statics = BindingFlags.Static | BindingFlags.NonPublic;
        int checks = 0;
        void Check(bool valid, string message) {
            checks++;
            if (!valid) throw new Exception("Packaged map flags: " + message);
        }
        Type TypeOf(string name) => assembly.GetType("GillionsGameSync." + name, true)!;
        var configType = TypeOf("PluginConfiguration");
        var setting = configType.GetProperty("EnableMapFlagRequests");
        var testing = assembly.GetName().Name == "GillionsGameSyncTest";
        Check((assembly.GetType("GillionsGameSync.MapFlagPolicy") is not null) == testing, "Testing-only request parser");
        Check((setting is not null) == testing, "Testing-only local setting");
        if (!testing) return;
        var config = Activator.CreateInstance(configType)!;
        Check((bool)setting!.GetValue(config)!, "new configuration defaults On");
        Check((bool)setting.GetValue(JsonConvert.DeserializeObject("{}", configType))!, "existing configuration upgrades to On");
        setting.SetValue(config, false);
        Check(!(bool)setting.GetValue(JsonConvert.DeserializeObject(JsonConvert.SerializeObject(config), configType))!,
            "saved local Off survives normal serialization");
        setting.SetValue(config, true);
        var policy = TypeOf("MapFlagPolicy");
        Check((string)policy.GetField("Permission", statics)!.GetRawConstantValue()! == "server:game-sync:receive:map-flag:v1",
            "exact device permission scope");
        Check((string)policy.GetField("Label", statics)!.GetRawConstantValue()! == "Map flags from the site", "permission label");
        var pluginType = TypeOf("Plugin");
        var plugin = RuntimeHelpers.GetUninitializedObject(pluginType);
        void Set(string name, object value) => pluginType.GetField(name, instance)!.SetValue(plugin, value);
        bool Enabled(string name) => (bool)pluginType.GetProperty(name, instance)!.GetValue(plugin)!;
        const string device = "00000000-0000-4000-8000-000000000001";
        const string token = "SYNTHETIC_DEVICE_CREDENTIAL_0000000000";
        configType.GetProperty("DeviceId")!.SetValue(config, device);
        configType.GetProperty("DeviceToken")!.SetValue(config, token);
        configType.GetProperty("EnableItemLinkRequests")!.SetValue(config, false);
        configType.GetProperty("EnablePartyFinderLinkRequests")!.SetValue(config, false);
        configType.GetProperty("AutomaticallyShowHuntMap")!.SetValue(config, false);
        configType.GetProperty("ActiveSession")!.SetValue(config, TypeOf("PairedSession").GetMethod("Create")!
            .Invoke(null, ["https://test.gillions.app", device, token]));
        Set("configuration", config);
        Set("activeOwnedState", Activator.CreateInstance(TypeOf("OwnedCharacterState"))!);
        var authorityType = TypeOf("PermissionAuthority");
        var authority = Activator.CreateInstance(authorityType, true)!;
        Set("permissionAuthority", authority);
        authorityType.GetMethod("Bind", instance)!.Invoke(authority, ["fixture:character", device]);
        var now = DateTime.UtcNow;
        JsonObject Fixture(bool? enabled, bool desynthesis) {
            var decisions = new JsonObject();
            foreach (var key in (string[])authorityType.GetField("Keys", statics)!.GetValue(null)!) {
                if (key == "mapFlags" && enabled is null || key == "desynthesisResults" && !desynthesis) continue;
                decisions[key] = new JsonObject {
                    ["scope"] = key == "marketContribution" ? "account" : "device",
                    ["state"] = key == "mapFlags" ? "explicit" : "legacy",
                    ["enabled"] = key == "mapFlags" ? JsonValue.Create(enabled) : null,
                    ["generation"] = key == "mapFlags" ? "00000000-0000-4000-8000-000000000002" : null,
                    ["decidedAt"] = key == "mapFlags" ? now.AddSeconds(-1).ToString("O") : null,
                };
            }
            return new JsonObject { ["ok"] = true, ["permissionAuthority"] = new JsonObject {
                ["revision"] = 1, ["deviceBinding"] = new JsonObject {
                    ["deviceId"] = device, ["pairedAt"] = now.AddDays(-1).ToString("O"), ["characterId"] = 100,
                },
                ["issuedAt"] = now.ToString("O"), ["expiresAt"] = now.AddSeconds(30).ToString("O"), ["permissions"] = decisions,
            } };
        }
        foreach (var desynthesis in new[] { false, true }) {
            foreach (var on in new bool?[] { null, false, true }) {
                var body = Fixture(on, desynthesis);
                Check((bool)authorityType.GetMethod("Apply", instance)!.Invoke(authority,
                    [body.ToJsonString(), now, Environment.TickCount64, null])!, "optional permissions coexist with older Sites");
                Check(Enabled("MapFlagsEnabled") == (on == true)
                    && Enabled("WebsiteLinksEnabled") == (on == true), "map permission alone enables the existing website lane");
                if (on != true) continue;
                setting.SetValue(config, false);
                Check(!Enabled("MapFlagsEnabled") && !Enabled("WebsiteLinksEnabled"), "local Off blocks explicit Site On");
                setting.SetValue(config, true);
            }
        }
        authorityType.GetMethod("Invalidate", instance)!.Invoke(authority, null);
        Check(!Enabled("MapFlagsEnabled"), "missing or expired authority prevents delivery");
        // Public SDK constructor, no game method is invoked.
        Check(typeof(MapLinkPayload).GetConstructor([typeof(uint), typeof(uint), typeof(float), typeof(float), typeof(float)]) is not null,
            "public SDK supports human-readable map coordinates");
        Console.WriteLine($"Packaged map flags PASS: {checks} permission, upgrade, channel and SDK checks. Synthetic only.");
    }
}
