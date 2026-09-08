using System.Reflection;
using System.Runtime.Loader;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

if (args.Length != 3) throw new ArgumentException("Expected plugin binary, Dalamud library directory, and synthetic fixture directory.");
var assemblyPath = Path.GetFullPath(args[0]);
var libraryPath = Path.GetFullPath(args[1]);
var fixturePath = Path.GetFullPath(args[2]);
AssemblyLoadContext.Default.Resolving += (_, name) => {
    var path = Path.Combine(libraryPath, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
var pluginAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
var configurationType = pluginAssembly.GetType("GillionsGameSync.PluginConfiguration", throwOnError: true)!;
var pluginType = pluginAssembly.GetType("GillionsGameSync.Plugin", throwOnError: true)!;
Assert(pluginAssembly.GetType("GillionsGameSync.AutoRetainerVenturePlanWriter") is null
    && pluginAssembly.GetType("GillionsGameSync.AutoRetainerIpc") is null
    && pluginAssembly.GetType("GillionsGameSync.RetainerPlanDeliveryPolicy") is null
    && pluginType.GetMethod("PollRetainerPlansAsync", BindingFlags.Instance | BindingFlags.NonPublic) is null
    && pluginType.GetMethod("ApplyRetainerPlanDelivery", BindingFlags.Instance | BindingFlags.NonPublic) is null,
    "Retired executable integration must not exist in the built plugin.");
var serialize = typeof(PluginConfigurations).GetMethod("SerializeConfig", BindingFlags.Static | BindingFlags.NonPublic)
    ?? throw new MissingMethodException("The installed Dalamud config serializer changed; reconcile the test with its actual API.");
var load = typeof(PluginConfigurations).GetMethod("LoadForType")!.MakeGenericMethod(configurationType);
var configurations = new PluginConfigurations(fixturePath);
var product = pluginAssembly.GetName().Name!;
var pathForFixture = configurations.GetConfigFile(product).FullName;
var savedViaPlugin = DispatchProxy.Create<IDalamudPluginInterface, ConfigurationSaveProxy>();
var saveProxy = (ConfigurationSaveProxy)savedViaPlugin;
saveProxy.Save = config => File.WriteAllText(pathForFixture, (string)serialize.Invoke(null, [config])!);

// Type metadata deliberately names retired types. Dalamud LoadForType and the
// inert backup reader must not resolve or construct them. All data is synthetic.
var original = Parse("""
{
  "$type": "GillionsGameSync.PluginConfiguration, __PRODUCT__",
  "Version": 1,
  "DeviceToken": "SYNTHETIC-DEVICE-TOKEN",
  "DeviceId": "SYNTHETIC-DEVICE",
  "PairingCode": "SYNTHETIC-PAIRING",
  "AutomaticSync": false,
  "EnableAutoRetainerVenturePlans": true,
  "AutoRetainerVenturePlanBackups": {
    "$type": "System.Collections.Generic.Dictionary`2[[System.String],[GillionsGameSync.AutoRetainerVenturePlanBackup, __PRODUCT__]]",
    "111:100": {
      "$type": "GillionsGameSync.AutoRetainerVenturePlanBackup, __PRODUCT__",
      "Name": "Synthetic original", "Steps": [{"VentureId": 245, "Repetitions": 3}],
      "PlanCompleteBehavior": "restart_plan", "LinkedVenturePlan": "Synthetic", "VenturePlanIndex": 7,
      "EnablePlanner": true, "FutureField": {"when": "2024-01-01T12:34:56.000+02:00", "counter": 18446744073709551615}
    },
    "222:100": {"Name": "Other synthetic character", "MalformedSteps": [null, "invalid", 7]}
  },
  "AutoRetainerPlanOwnershipStates": {
    "111:100": {"OwnerDeviceId": "SYNTHETIC-DEVICE", "RetainerId": "100", "RevisionId": "synthetic-revision",
      "ProjectionGeneration": 1, "DeliveryId": "synthetic-delivery", "AppliedHash": "invalid-but-preserved",
      "PriorPlanBackupHash": "also-invalid", "RestoreApplied": true,
      "PriorPlanBackup": {"Steps": "malformed-but-preserved", "Unknown": {"$type": "Unavailable.Legacy.Type", "value": 3}}},
    "222:100": {"OwnerDeviceId": "SYNTHETIC-FOREIGN-DEVICE", "RestoreApplied": false, "RevisionNumber": "malformed"}
  }
}
""".Replace("__PRODUCT__", product));
var cases = new List<JObject> { original };
foreach (var value in new[] { "null", "[]", "17", "false", "\"malformed-container\"", "{\"future\":[\"2024-01-01T00:00:00.000Z\",null]}" }) {
    var malformed = (JObject)original.DeepClone();
    malformed["AutoRetainerVenturePlanBackups"] = ParseToken(value);
    malformed["AutoRetainerPlanOwnershipStates"] = ParseToken(value);
    cases.Add(malformed);
}
var older = (JObject)original.DeepClone();
older.Remove("AutoRetainerVenturePlanBackups");
older.Remove("AutoRetainerPlanOwnershipStates");
older.Remove("EnableAutoRetainerVenturePlans");
cases.Add(older);
foreach (var fixture in cases) {
    File.WriteAllText(pathForFixture, fixture.ToString(Formatting.None));
    var config = load.Invoke(configurations, [product]) ?? throw new InvalidOperationException("Synthetic config failed to load.");
    configurationType.GetProperty("LastReadChangelogVersion")!.SetValue(config, "synthetic-retirement");
    // Exercise the plugin's ordinary Save wrapper, intercepted only at Dalamud
    // storage to keep all files inside this synthetic fixture directory.
    configurationType.GetMethod("Save")!.Invoke(config, [savedViaPlugin]);
    var roundTrip = Parse(File.ReadAllText(pathForFixture));
    foreach (var name in new[] { "AutoRetainerVenturePlanBackups", "AutoRetainerPlanOwnershipStates" }) {
        if (fixture.TryGetValue(name, out var expected))
            Assert(JToken.DeepEquals(expected, roundTrip[name]), $"{product}: {name} lost legacy data.");
    }
    Assert(roundTrip.Value<string>("DeviceToken") == "SYNTHETIC-DEVICE-TOKEN"
        && roundTrip.Value<bool>("AutomaticSync") == false, "Unrelated config fields must survive normal save.");
    Assert(roundTrip.Value<bool>("EnableAutoRetainerVenturePlans") == fixture.Value<bool>("EnableAutoRetainerVenturePlans"),
        "Legacy opt-in must survive inertly, including the absent-field default.");
    var restarted = load.Invoke(configurations, [product])!;
    configurationType.GetMethod("Save")!.Invoke(restarted, [savedViaPlugin]);
    var second = Parse(File.ReadAllText(pathForFixture));
    Assert(JToken.DeepEquals(roundTrip, second), "Repeated ordinary saves must not normalize or lose legacy data.");
}
Console.WriteLine($"Actual Dalamud config load / plugin Save / serializer: {product}, {cases.Count} fixtures passed.");

static JObject Parse(string value) => (JObject)ParseToken(value);
static JToken ParseToken(string value) {
    using var reader = new JsonTextReader(new StringReader(value)) { DateParseHandling = DateParseHandling.None };
    return JToken.Load(reader);
}
static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

public class ConfigurationSaveProxy : DispatchProxy {
    public Action<object> Save { get; set; } = _ => throw new InvalidOperationException("Save sink is not configured.");
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
        if (targetMethod?.Name == "SavePluginConfig" && args is [object config]) { Save(config); return null; }
        throw new InvalidOperationException("Synthetic config test attempted an unexpected plugin service.");
    }
}
