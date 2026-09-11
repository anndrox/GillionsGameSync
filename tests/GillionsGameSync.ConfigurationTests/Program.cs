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
var beastmasterView = pluginAssembly.GetType("GillionsGameSync.BeastmasterLocalView");
var beastmasterReadiness = pluginAssembly.GetType("GillionsGameSync.BeastmasterReadiness");
Assert(pluginAssembly.GetType("GillionsGameSync.BeastmasterDiagnostic") is null,
    "Standalone diagnostic entry point must not survive testing integration.");
Assert(typeof(IDalamudPlugin).IsAssignableFrom(pluginType), "Existing Plugin must retain its Dalamud entry point.");
if (product == "GillionsGameSyncTest") {
    Assert(beastmasterView is not null && beastmasterReadiness is not null,
        "Testing product must contain the local Beastmaster reader and freshness policy.");
    Assert(!typeof(IDalamudPlugin).IsAssignableFrom(beastmasterView!),
        "Local Beastmaster reader must not create a second plugin entry point.");
    Assert(pluginType.GetField("beastmasterLocal", BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType == beastmasterView,
        "Existing testing Plugin must own the local Beastmaster lifecycle.");
} else {
    Assert(beastmasterView is null && beastmasterReadiness is null,
        "Stable product must not contain experimental Beastmaster collection.");
}
Assert(!configurationType.GetProperties().Any(property => property.Name.Contains("Beastmaster", StringComparison.OrdinalIgnoreCase)),
    "Local Beastmaster test must not add persisted configuration or observations.");
Console.WriteLine($"Actual Beastmaster testing-only assembly boundary passed: {product}.");
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
  "PairingCode": "2024-01-01T12:34:56.000+02:00",
  "LastPayloadHashes": {"synthetic": "2024-01-01T12:34:56.1234567+02:00"},
  "LastSyncUtc": "2024-01-01T12:34:56.1234567+02:00",
  "UnrecognizedOrdinary": {"AutoRetainerVenturePlanBackups": "2024-01-01T12:34:56.000+02:00", "$type": "Unavailable.Ignored.Type"},
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
// The date-like scalar root is read before the property converter runs.
// Both keys must retain the literal string, offset and fractional precision.
foreach (var timestamp in new[] { "2024-01-01T12:34:56.000+02:00", "2024-01-01T12:34:56.1234567-03:30" }) {
    var scalar = (JObject)original.DeepClone();
    scalar["AutoRetainerVenturePlanBackups"] = timestamp;
    scalar["AutoRetainerPlanOwnershipStates"] = timestamp;
    cases.Add(scalar);
}
var unknownRootType = (JObject)original.DeepClone();
unknownRootType["$type"] = "Unavailable.Root.Type, NoSuchAssembly";
cases.Add(unknownRootType);
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
    Assert(roundTrip.Value<string>("PairingCode") == "2024-01-01T12:34:56.000+02:00"
        && roundTrip["LastPayloadHashes"]!.Value<string>("synthetic") == "2024-01-01T12:34:56.1234567+02:00",
        "Ordinary string fields and dictionaries must retain their usual string handling.");
    var ordinaryDate = (DateTime)configurationType.GetProperty("LastSyncUtc")!.GetValue(config)!;
    var expectedDate = JsonConvert.DeserializeObject<DateTime>("\"2024-01-01T12:34:56.1234567+02:00\"");
    Assert(ordinaryDate.Ticks == expectedDate.Ticks && ordinaryDate.Kind == expectedDate.Kind,
        "Ordinary typed dates must retain the default serializer's value and Kind.");
    Assert(roundTrip["UnrecognizedOrdinary"] is null,
        "Unknown ordinary fields must retain the default ignored-field behavior.");
    Assert(roundTrip.Value<string>("DeviceToken") == "SYNTHETIC-DEVICE-TOKEN"
        && roundTrip.Value<bool>("AutomaticSync") == false, "Unrelated config fields must survive normal save.");
    Assert(roundTrip.Value<bool>("EnableAutoRetainerVenturePlans") == fixture.Value<bool>("EnableAutoRetainerVenturePlans"),
        "Legacy opt-in must survive inertly, including the absent-field default.");
    var restarted = load.Invoke(configurations, [product])!;
    configurationType.GetMethod("Save")!.Invoke(restarted, [savedViaPlugin]);
    var second = Parse(File.ReadAllText(pathForFixture));
    Assert(JToken.DeepEquals(roundTrip, second), "Repeated ordinary saves must not normalize or lose legacy data.");
}
// Exercise the actual future configuration schema, not a parallel DTO serializer.
File.WriteAllText(pathForFixture, original.ToString(Formatting.None));
var ownedConfig = load.Invoke(configurations, [product])!;
var pairedSessionType = pluginAssembly.GetType("GillionsGameSync.PairedSession", true)!;
var ownershipPolicy = pluginAssembly.GetType("GillionsGameSync.SyncOwnershipPolicy", true)!;
var budgetType = pluginAssembly.GetType("GillionsGameSync.DurableEvidenceBudget", true)!;
var ledgerType = pluginAssembly.GetType("GillionsGameSync.GilLedgerEvent", true)!;
var syntheticToken = new string('x', 43);
var syntheticDeviceId = "11111111-1111-1111-1111-111111111111";
var pairedSession = pairedSessionType.GetMethod("Create")!.Invoke(null, ["https://example.com", syntheticDeviceId, syntheticToken])!;
configurationType.GetProperty("DeviceToken")!.SetValue(ownedConfig, syntheticToken);
configurationType.GetProperty("DeviceId")!.SetValue(ownedConfig, syntheticDeviceId);
configurationType.GetProperty("ActiveSession")!.SetValue(ownedConfig, pairedSession);
var ownedStates = configurationType.GetProperty("OwnedCharacters")!.GetValue(ownedConfig)!;
var getCharacter = ownershipPolicy.GetMethod("GetCharacter")!;
var ownedA = getCharacter.Invoke(null, [ownedStates, pairedSession, 111UL])!;
var ownedB = getCharacter.Invoke(null, [ownedStates, pairedSession, 222UL])!;
var ownedType = ownedA.GetType();
var ledgerA = (System.Collections.IList)ownedType.GetProperty("PendingGilLedgerEvents")!.GetValue(ownedA)!;
var ledgerB = (System.Collections.IList)ownedType.GetProperty("PendingGilLedgerEvents")!.GetValue(ownedB)!;
object MakeLedger(int id) => Activator.CreateInstance(ledgerType, new object?[] {
    id.ToString("D32"), new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc), 10L, "unclassified", "inferred",
    null, null, null, null, null, Array.Empty<int>(), "Fixture", "Test",
})!;
ledgerA.Add(MakeLedger(1)); ledgerB.Add(MakeLedger(2));
var legacyLedger = (System.Collections.IList)configurationType.GetProperty("PendingGilLedgerEvents")!.GetValue(ownedConfig)!;
legacyLedger.Add(MakeLedger(999));
var gap = configurationType.GetProperty("CoverageGap")!.GetValue(ownedConfig)!;
gap.GetType().GetProperty("Paused")!.SetValue(gap, true);
gap.GetType().GetProperty("StartedAtUtc")!.SetValue(gap, new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc));
var accounting = budgetType.GetMethod("SerializeAccountingDocument")!;
object Values(object dictionary) => dictionary.GetType().GetProperty("Values")!.GetValue(dictionary)!;
var beforeOwnedBytes = (byte[])accounting.Invoke(null, [Values(ownedStates)])!;
configurationType.GetMethod("Save")!.Invoke(ownedConfig, [savedViaPlugin]);
var savedOwned = Parse(File.ReadAllText(pathForFixture));
var loadedOwned = load.Invoke(configurations, [product])!;
var restoredSession = configurationType.GetProperty("ActiveSession")!.GetValue(loadedOwned)!;
Assert((bool)ownershipPolicy.GetMethod("IsBoundSession")!.Invoke(null, [restoredSession, false, syntheticDeviceId, syntheticToken])!,
    "An internally valid future binding must survive the real config load/save chain.");
var restoredStates = configurationType.GetProperty("OwnedCharacters")!.GetValue(loadedOwned)!;
var afterOwnedBytes = (byte[])accounting.Invoke(null, [Values(restoredStates)])!;
Assert(beforeOwnedBytes.SequenceEqual(afterOwnedBytes), "Actual config serialization must preserve exact managed pending-data accounting.");
Assert(((System.Collections.IList)configurationType.GetProperty("PendingGilLedgerEvents")!.GetValue(loadedOwned)!).Count == 1,
    "The unscoped legacy queue must survive separately from active owned data.");
Assert((bool)gap.GetType().GetProperty("Paused")!.GetValue(configurationType.GetProperty("CoverageGap")!.GetValue(loadedOwned))!,
    "The visible coverage gap must survive the actual serializer.");
Assert(JToken.DeepEquals(original["AutoRetainerVenturePlanBackups"], savedOwned["AutoRetainerVenturePlanBackups"])
    && JToken.DeepEquals(original["AutoRetainerPlanOwnershipStates"], savedOwned["AutoRetainerPlanOwnershipStates"]),
    "New ownership state must not change opaque legacy history.");
var secondSession = pairedSessionType.GetMethod("Create")!.Invoke(null, ["https://example.com", syntheticDeviceId, syntheticToken])!;
configurationType.GetProperty("ActiveSession")!.SetValue(loadedOwned, secondSession);
var newA = getCharacter.Invoke(null, [restoredStates, secondSession, 111UL])!;
Assert(((System.Collections.IList)ownedType.GetProperty("PendingGilLedgerEvents")!.GetValue(newA)!).Count == 0
    && beforeOwnedBytes.SequenceEqual((byte[])accounting.Invoke(null, [Values(restoredStates)])!),
    "A new pairing cannot adopt, delete or exclude the previous generation's admitted evidence from its budget.");
configurationType.GetMethod("Save")!.Invoke(loadedOwned, [savedViaPlugin]);
File.Copy(pathForFixture, Path.Combine(fixturePath, "owned-configuration-roundtrip.json"), true);
Console.WriteLine($"Actual owned configuration / re-pair / coverage-gap fixtures passed: {product}; {beforeOwnedBytes.Length} accounted bytes across two character partitions.");

GapBaselineTests.Run(pluginAssembly, config => {
    configurationType.GetMethod("Save")!.Invoke(config, [savedViaPlugin]);
    return load.Invoke(configurations, [product])!;
}, fixturePath);

// These static plugin methods require no plugin instance or native game state.
var logEvidenceType = pluginAssembly.GetType("GillionsGameSync.GilLedgerLogEvidence", true)!;
var classifyLedger = pluginType.GetMethod("ClassifyGilLedgerEvent", BindingFlags.Static | BindingFlags.NonPublic)!;
object ClassifyVendor(long delta, int item, int quantity, int amount) {
    var evidence = Activator.CreateInstance(logEvidenceType, [DateTime.UtcNow, 1688U, new[] { item, quantity, amount }])!;
    return classifyLedger.Invoke(null, [delta, evidence, null])!;
}
foreach (var classification in new[] { ClassifyVendor(10, 500, 10000, 10), ClassifyVendor(10, 1000000, 1, 10), ClassifyVendor(-10, 500, 1, -10) }) {
    Assert((string)classification.GetType().GetProperty("Confidence")!.GetValue(classification)! == "inferred",
        "Unsupported structured sale parameters must keep native balance evidence inferred and sendable.");
}
var validVendor = ClassifyVendor(10, 500, 1, 10);
Assert((string)validVendor.GetType().GetProperty("Kind")!.GetValue(validVendor)! == "vendor_sale"
    && (string)validVendor.GetType().GetProperty("Confidence")!.GetValue(validVendor)! == "confirmed",
    "Authoritative valid structured sale evidence must retain normal classification.");
var windowModel = pluginAssembly.GetType("GillionsGameSync.PluginWindowModel", true)!.GetMethod("Create")!;
var views = new JArray();
foreach (var (name, values) in new (string, object[])[] {
    ("first_pair", [false, false, true, true, false, false, false, ""]),
    ("legacy_repair", [false, true, true, true, false, false, false, ""]),
    ("connected", [true, false, true, true, false, false, false, ""]),
    ("automatic_off", [true, false, true, false, false, false, false, ""]),
    ("logged_out", [true, false, false, true, false, false, false, ""]),
    ("storage_paused", [true, false, true, true, false, true, false, ""]),
    ("gap_resumed", [true, false, true, true, false, false, true, ""]),
    ("combined_warnings", [false, true, true, true, false, true, false, "ACCOUNT_DISABLED"]),
}) views.Add(new JObject { ["scenario"] = name, ["state"] = JObject.FromObject(windowModel.Invoke(null, values)!) });
File.WriteAllText(Path.Combine(fixturePath, "ui-view-states.json"), new JObject {
    ["product"] = product, ["renderedInGame"] = false, ["states"] = views,
    ["diagnosticsDefault"] = product == "GillionsGameSyncTest" ? "automatic" : "manual",
}.ToString(Formatting.Indented));
Console.WriteLine($"Actual static ledger classification and 8 deterministic UI view states passed: {product}; no native game state or UI rendering used.");

// Exercise the adapter's exact read boundary with the installed JsonReader.
// Nested lookalike names remain ordinary date tokens; only root legacy values
// receive the scoped string policy. Settings and state must always be restored.
var adapterType = pluginAssembly.GetType("GillionsGameSync.LegacyPlanPropertyReader", throwOnError: true)!;
using (var inner = new JsonTextReader(new StringReader("""
{"ordinary":{"AutoRetainerVenturePlanBackups":"2024-01-01T12:34:56.000+02:00"},
 "unknown":"2024-01-01T12:34:56.000+02:00",
 "AutoRetainerVenturePlanBackups":"2024-01-01T12:34:56.000+02:00",
 "AutoRetainerPlanOwnershipStates":"2024-01-01T12:34:56.1234567-03:30"}
""")) { DateParseHandling = DateParseHandling.DateTime }) {
    Assert(inner.Read(), "Reader fixture root is missing.");
    using var adapter = (JsonReader)Activator.CreateInstance(adapterType, [inner])!;
    var values = new Dictionary<string, JsonToken>();
    while (adapter.Read()) {
        Assert(adapter.Depth == inner.Depth && adapter.Path == inner.Path && adapter.TokenType == inner.TokenType,
            "Adapter depth/path/token state diverged from the actual reader.");
        Assert(inner.DateParseHandling == DateParseHandling.DateTime, "Date setting leaked after a successful read.");
        if (adapter.TokenType is JsonToken.Date or JsonToken.String) values[adapter.Path] = adapter.TokenType;
    }
    Assert(values["ordinary.AutoRetainerVenturePlanBackups"] == JsonToken.Date && values["unknown"] == JsonToken.Date,
        "Nested lookalikes and unknown ordinary fields must retain ordinary date inference.");
    Assert(values["AutoRetainerVenturePlanBackups"] == JsonToken.String && values["AutoRetainerPlanOwnershipStates"] == JsonToken.String,
        "Only the top-level legacy values must bypass date inference.");
}
using (var inner = new JsonTextReader(new StringReader("{\"AutoRetainerVenturePlanBackups\":\"unfinished")) {
    DateParseHandling = DateParseHandling.DateTimeOffset,
}) {
    Assert(inner.Read(), "Failure fixture root is missing.");
    using var adapter = (JsonReader)Activator.CreateInstance(adapterType, [inner])!;
    var rejected = false;
    try { while (adapter.Read()) { } } catch (JsonReaderException) { rejected = true; }
    Assert(rejected && inner.DateParseHandling == DateParseHandling.DateTimeOffset,
        "Malformed legacy input must fail and restore the reader's original date policy.");
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
