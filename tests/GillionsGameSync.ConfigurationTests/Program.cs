using System.Reflection;
using System.Runtime.Loader;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

if (args.Length is not (3 or 4)) throw new ArgumentException("Expected plugin binary, Dalamud library directory, synthetic fixture directory, and optional running-Site proof.");
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
var partyFinderOptIn = configurationType.GetProperty("EnablePartyFinderContributions")!;
Assert(!(bool)partyFinderOptIn.GetValue(Activator.CreateInstance(configurationType))!,
    "A new configuration must keep Party Finder contribution off by default.");
var endpoint = (Uri)pluginAssembly.GetType("GillionsGameSync.XivpfEndpoints", true)!
    .GetProperty("ContributionUrl", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
var testingProduct = pluginAssembly.GetName().Name == "GillionsGameSyncTest";
FatePackagedTests.Run(pluginAssembly,testingProduct,fixturePath);
HuntV2PackagedTests.Run(pluginAssembly, testingProduct, args.Length == 4 ? args[3] : null);
HuntBillItemPackagedTests.Run(pluginAssembly, testingProduct);
var intakeOptIn = configurationType.GetProperty("EnableGillionsPartyFinderContributions")!;
var legacyOptedIn = Activator.CreateInstance(configurationType)!;
partyFinderOptIn.SetValue(legacyOptedIn, true);
Assert(!(bool)intakeOptIn.GetValue(legacyOptedIn)!, "Legacy xivpf opt-in must never grant consent to the new Gillions recipient.");
intakeOptIn.SetValue(legacyOptedIn, true);
var intakeRoundTrip = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(legacyOptedIn), configurationType)!;
Assert((bool)intakeOptIn.GetValue(intakeRoundTrip)!, "New Gillions opt-in must survive actual Newtonsoft serialization.");
var normalizeOrigin = pluginAssembly.GetType("GillionsGameSync.SyncOrigin", true)!
    .GetMethod("TryNormalize", BindingFlags.Static | BindingFlags.Public)!;
bool AcceptsOrigin(string value) => (bool)normalizeOrigin.Invoke(null, [value, ""])!;
Assert(AcceptsOrigin("https://gillions.app") && !AcceptsOrigin("http://example.com")
    && !AcceptsOrigin("http://127.0.0.1:3301") && !AcceptsOrigin("http://192.168.254.254:3301"),
    "Both products must support the main HTTPS origin and reject all plaintext pairing origins.");
Console.WriteLine($"Actual HTTPS-only pairing boundary passed: {pluginAssembly.GetName().Name}.");
Assert(testingProduct
        ? endpoint == new Uri("https://test.gillions.app/api/game-sync/party-finder/contribute")
        : endpoint == new Uri("https://xivpf.com/contribute/multiple"),
    "Built product resolved an unsafe or unexpected xivpf contribution endpoint.");
var changelog = (string[])pluginType.GetField("CurrentChangelog", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
Assert(testingProduct
    ? changelog.Any(line => line.Contains("Public listings go to Gillions HTTPS, not directly to xivpf.com or localhost."))
        && changelog.Any(line => line.Contains("runtime current-listing cache") && line.Contains("Authorization"))
        && !changelog.Any(line => line.Contains("listings directly to xivpf.com") || line.Contains("loopback-only"))
    : changelog.Any(line => line.Contains("listings directly to xivpf.com")),
    "Built-product recipient/custody changelog disclosure is incorrect.");
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
var publicPreferences = new[] { "ShowHuntProgress", "LockHuntProgressPosition", "OnboardingCompleted" };
var publicUpgrade = JsonConvert.DeserializeObject("{\"AutomaticSync\":false,\"EnableItemLinkRequests\":false,\"DeviceToken\":\"SYNTHETIC-DEVICE-TOKEN\",\"EnablePartyFinderContributions\":false,\"EnableGillionsPartyFinderContributions\":false,\"ContributeObservedMarketData\":false}", configurationType)!;
Assert(publicPreferences.All(n => !(bool)configurationType.GetProperty(n)!.GetValue(publicUpgrade)!),
    "Stable and Testing upgrades must default new presentation/onboarding fields OFF without reconsent.");
foreach (var name in publicPreferences) configurationType.GetProperty(name)!.SetValue(publicUpgrade, true);
configurationType.GetMethod("Save")!.Invoke(publicUpgrade, [savedViaPlugin]);
var publicRestart = load.Invoke(configurations, [product])!;
Assert(publicPreferences.All(n => (bool)configurationType.GetProperty(n)!.GetValue(publicRestart)!),
    "Actual Dalamud persistence lost public presentation preferences.");
configurationType.GetProperty("ShowHuntProgress")!.SetValue(publicRestart, false);
configurationType.GetMethod("Save")!.Invoke(publicRestart, [savedViaPlugin]);
publicRestart = load.Invoke(configurations, [product])!;
Assert(!(bool)configurationType.GetProperty("ShowHuntProgress")!.GetValue(publicRestart)!
    && !(bool)configurationType.GetProperty("AutomaticSync")!.GetValue(publicRestart)!
    && !(bool)configurationType.GetProperty("EnableItemLinkRequests")!.GetValue(publicRestart)!
    && !(bool)partyFinderOptIn.GetValue(publicRestart)! && !(bool)intakeOptIn.GetValue(publicRestart)!
    && (string)configurationType.GetProperty("DeviceToken")!.GetValue(publicRestart)! == "SYNTHETIC-DEVICE-TOKEN"
    && (!testingProduct || !(bool)configurationType.GetProperty("ContributeObservedMarketData")!.GetValue(publicRestart)!),
    "Hunt Progress OFF changed old credentials, ordinary sync, website commands or contribution choices.");
Console.WriteLine($"Actual public preferences default/upgrade/restart/OFF isolation PASS: {product}.");
var marketSetting = configurationType.GetProperty("ContributeObservedMarketData");
var personalHuntSetting = configurationType.GetProperty("SyncPersonalHunts");
var personalSubSetting = configurationType.GetProperty("SyncPersonalSubmarines");
var pfLinkSetting = configurationType.GetProperty("EnablePartyFinderLinkRequests");
var travelSetting = configurationType.GetProperty("ShareHuntRoutingLocation");
var huntMapSetting = configurationType.GetProperty("AutomaticallyShowHuntMap");
if (testingProduct) {
    var huntMapOlder = JsonConvert.DeserializeObject("{\"ShareHuntRoutingLocation\":true,\"SyncPersonalHunts\":true,\"SyncPersonalSubmarines\":true,\"AutomaticSync\":true,\"EnableItemLinkRequests\":true,\"EnablePartyFinderLinkRequests\":true,\"ContributeObservedMarketData\":true}",configurationType)!;
    Assert(!(bool)huntMapSetting!.GetValue(huntMapOlder)! && !(bool)huntMapSetting.GetValue(Activator.CreateInstance(configurationType))!,"Existing permissions must not grant Hunt map consent.");
    huntMapSetting.SetValue(huntMapOlder,true); configurationType.GetMethod("Save")!.Invoke(huntMapOlder,[savedViaPlugin]);
    var restored=load.Invoke(configurations,[product])!;
    Assert((bool)huntMapSetting.GetValue(restored)!,"Actual serializer lost Hunt map opt-in.");
    huntMapSetting.SetValue(restored,false); configurationType.GetMethod("Save")!.Invoke(restored,[savedViaPlugin]);
    restored=load.Invoke(configurations,[product])!;
    Assert(!(bool)huntMapSetting.GetValue(restored)! && new[] {"ShareHuntRoutingLocation","SyncPersonalHunts","SyncPersonalSubmarines","AutomaticSync","EnableItemLinkRequests","EnablePartyFinderLinkRequests","ContributeObservedMarketData"}.All(n=>(bool)configurationType.GetProperty(n)!.GetValue(restored)!),"Map OFF changed unrelated choices.");
    Assert(pluginAssembly.GetType("GillionsGameSync.HuntMapRequest",true)!.GetMethod("ToString")!.DeclaringType==typeof(object),"Hunt claim dumps token.");
    Assert(typeof(Dalamud.Plugin.Services.IGameGui).GetMethod("OpenMapWithMapLink",[typeof(Dalamud.Game.Text.SeStringHandling.Payloads.MapLinkPayload)])?.ReturnType==typeof(bool),"Public map presentation API changed.");
    Assert(typeof(Dalamud.Game.Text.SeStringHandling.Payloads.MapLinkPayload).GetConstructor([typeof(uint),typeof(uint),typeof(float),typeof(float),typeof(float)])!=null,"Human-readable map link constructor changed.");
    Console.WriteLine("Actual Hunt map default-OFF/serializer/independent consent/public SDK API PASS.");
} else Assert(huntMapSetting is null && pluginAssembly.GetType("GillionsGameSync.HuntMapRequest") is null && pluginAssembly.GetType("GillionsGameSync.HuntMapProcessor") is null,"Stable gained Hunt map actions.");
if (testingProduct) {
    var travelOlder = JsonConvert.DeserializeObject("{\"SyncPersonalHunts\":true,\"SyncPersonalSubmarines\":true,\"AutomaticSync\":true,\"EnablePartyFinderLinkRequests\":true,\"ContributeObservedMarketData\":false}",configurationType)!;
    Assert(!(bool)travelSetting!.GetValue(travelOlder)! && !(bool)travelSetting.GetValue(Activator.CreateInstance(configurationType))!,"Old/default permissions must not grant location consent.");
    travelSetting.SetValue(travelOlder,true);
    configurationType.GetMethod("Save")!.Invoke(travelOlder,[savedViaPlugin]);
    var roundTrip=load.Invoke(configurations,[product])!;
    Assert((bool)travelSetting.GetValue(roundTrip)!,"Actual serializer dropped explicit location choice.");
    travelSetting.SetValue(roundTrip,false);
    configurationType.GetMethod("Save")!.Invoke(roundTrip,[savedViaPlugin]);
    roundTrip=load.Invoke(configurations,[product])!;
    Assert(!(bool)travelSetting.GetValue(roundTrip)! && (bool)personalHuntSetting!.GetValue(roundTrip)!
        && (bool)personalSubSetting!.GetValue(roundTrip)! && (bool)pfLinkSetting!.GetValue(roundTrip)!
        && (bool)configurationType.GetProperty("AutomaticSync")!.GetValue(roundTrip)! && !(bool)marketSetting!.GetValue(roundTrip)!,"Travel OFF changed unrelated choices.");
    Assert(!configurationType.GetProperties().Any(p=>p.PropertyType.Name.StartsWith("Travel",StringComparison.Ordinal)),"Configuration must not retain travel data.");
    var native=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(libraryPath,"FFXIVClientStructs.dll"));
    Assert(native.GetName().Version?.ToString()=="7.56.2.9136","Travel SDK gate needs reassessment.");
    var info=native.GetType("FFXIVClientStructs.FFXIV.Client.Game.UI.TeleportInfo",true)!;
    foreach(var name in new[] { "AetheryteId","GilCost","TerritoryId","IsFavourite","IsFreeAetheryte" }) Assert(info.GetField(name) is not null,"Travel field changed.");
    var agent=native.GetType("FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentTeleport",true)!;
    Assert(agent.GetField("AetheryteList")?.FieldType.IsPointer==true && agent.GetField("AetheryteCount")?.FieldType==typeof(int),"Teleport cache signature changed.");
    Assert(typeof(Dalamud.Plugin.Services.IClientState).GetEvent("MapIdChanged")?.EventHandlerType==typeof(Action<uint>),"Map event changed.");
    Assert(typeof(Dalamud.Plugin.Services.IPlayerState).GetProperty("HomeAetheryte") is not null
        && typeof(Dalamud.Plugin.Services.IPlayerState).GetProperty("FreeAetheryte") is not null,"Public destination getters missing.");
    var map = Dalamud.Utility.MapUtil.WorldToMap(new System.Numerics.Vector2(0,0),0,0,100);
    Assert(Math.Abs(map.X-21.48f)<0.001f && Math.Abs(map.Y-21.48f)<0.001f,"Actual public map conversion changed.");
    Assert(pluginAssembly.GetType("GillionsGameSync.TravelContextLocalView") is not null,"Testing travel collector missing.");
    var travelPolicy=pluginAssembly.GetType("GillionsGameSync.TravelSyncPolicy",true)!;
    var admit=travelPolicy.GetMethod("Admit",BindingFlags.Static|BindingFlags.NonPublic)!;
    for(int mask=0;mask<32;mask++) Assert((bool)admit.Invoke(null,[(mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0,"https://test.gillions.app",(mask&16)!=0])! == (mask==31),"Packaged travel admission gate differs.");
    Assert(pluginAssembly.GetType("GillionsGameSync.TravelPrepared",true)!.GetMethod("ToString")!.DeclaringType==typeof(object),"Packaged preparation dumps its payload.");
    var httpHandler=(HttpClientHandler)pluginAssembly.GetType("GillionsGameSync.PartyFinderHttp",true)!.GetMethod("CreateHandler",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[])!;
    using(httpHandler) Assert(!httpHandler.AllowAutoRedirect&&!httpHandler.UseCookies,"Personal/travel client follows redirects or retains cookies.");
    Assert(((string)pluginType.GetField("PluginVersion",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!).Split('.').Length==4,"Testing request/enrollment version must be four-part.");
    Console.WriteLine("Actual travel serializer/default-OFF/unrelated consent/SDK/map conversion PASS. No live game or HTTP invocation.");
} else Assert(travelSetting is null && pluginAssembly.GetType("GillionsGameSync.TravelContextLocalView") is null
    && pluginAssembly.GetType("GillionsGameSync.TravelSyncState") is null && pluginAssembly.GetType("GillionsGameSync.TravelPrepared") is null,"Stable gained travel collection/transport.");
if (testingProduct) {
    var oldLinkConfig = JsonConvert.DeserializeObject("{\"EnableItemLinkRequests\":true}", configurationType)!;
    Assert(!(bool)pfLinkSetting!.GetValue(oldLinkConfig)! && !(bool)pfLinkSetting.GetValue(Activator.CreateInstance(configurationType))!,
        "Old/default item consent must not silently expand to PF actions.");
    pfLinkSetting.SetValue(oldLinkConfig, true);
    configurationType.GetMethod("Save")!.Invoke(oldLinkConfig, [savedViaPlugin]);
    var pfReload = load.Invoke(configurations, [product])!;
    Assert((bool)pfLinkSetting.GetValue(pfReload)! && (bool)configurationType.GetProperty("EnableItemLinkRequests")!.GetValue(pfReload)!,
        "PF action explicit consent and old item setting must survive serializer.");
    pfLinkSetting.SetValue(pfReload, false);
    Assert((bool)configurationType.GetProperty("EnableItemLinkRequests")!.GetValue(pfReload)!, "PF OFF changed item consent.");
    var sdk = typeof(Dalamud.Game.Text.SeStringHandling.SeString);
    var createPf = sdk.GetMethod("CreatePartyFinderLink", [typeof(uint),typeof(string),typeof(bool)]);
    Assert(createPf is { IsPublic: true, IsStatic: true } && createPf.ReturnType == sdk, "Installed native PF API signature changed.");
    Assert((byte)Dalamud.Game.Gui.PartyFinder.Types.SearchAreaFlags.World == 8
        && (byte)Dalamud.Game.Gui.PartyFinder.Types.SearchAreaFlags.DataCenter == 1, "Installed cross-world flag semantics changed.");
    Assert(!typeof(Dalamud.Plugin.Services.IPartyFinderGui).GetMethods().Any(m => m.Name.Contains("Open") || m.Name.Contains("Join") || m.Name.Contains("Apply")),
        "Installed supported PF surface changed: reassess true open before claiming link-only.");
    foreach (var listingId in new uint[] { 1,101,uint.MaxValue }) foreach (var cross in new[] { false,true }) {
        var linkType = cross ? Dalamud.Game.Text.SeStringHandling.Payloads.PartyFinderPayload.PartyFinderLinkType.NotSpecified
            : Dalamud.Game.Text.SeStringHandling.Payloads.PartyFinderPayload.PartyFinderLinkType.LimitedToHomeWorld;
        var nativePayload = new Dalamud.Game.Text.SeStringHandling.Payloads.PartyFinderPayload(listingId, linkType);
        var chain = new Dalamud.Game.Text.SeStringHandling.SeString(new Dalamud.Game.Text.SeStringHandling.Payload[] {
            nativePayload, new Dalamud.Game.Text.SeStringHandling.Payloads.TextPayload("Fixture Recruiter"),
            Dalamud.Game.Text.SeStringHandling.Payloads.RawPayload.LinkTerminator });
        var decoded = Dalamud.Game.Text.SeStringHandling.SeString.Parse(chain.Encode()).Payloads
            .OfType<Dalamud.Game.Text.SeStringHandling.Payloads.PartyFinderPayload>().Single();
        Assert(decoded.ListingId == listingId && decoded.LinkType == linkType, "Actual SDK PF payload encoding roundtrip failed.");
    }
    Console.WriteLine("Actual installed PF API signature/flags/chat payload encoding/default-OFF and serializer consent PASS; no live game/chat invocation.");
} else Assert(pfLinkSetting is null && pluginAssembly.GetType("GillionsGameSync.PartyFinderLinkRequestProcessor") is null,
    "Stable must not gain website PF request action classes.");
if (testingProduct) {
    var oldPersonal = JsonConvert.DeserializeObject("{\"AutomaticSync\":true,\"ContributeObservedMarketData\":false}", configurationType)!;
    Assert(!(bool)personalHuntSetting!.GetValue(oldPersonal)! && !(bool)personalSubSetting!.GetValue(oldPersonal)!,
        "Older Testing configs must not silently grant personal upload consent.");
    personalHuntSetting.SetValue(oldPersonal, true);
    configurationType.GetMethod("Save")!.Invoke(oldPersonal, [savedViaPlugin]);
    var savedPersonal = load.Invoke(configurations, [product])!;
    Assert((bool)personalHuntSetting.GetValue(savedPersonal)! && !(bool)personalSubSetting!.GetValue(savedPersonal)!,
        "Actual serializer preserves independent Hunt/Submarine permissions.");
    personalHuntSetting.SetValue(savedPersonal, false);
    configurationType.GetMethod("Save")!.Invoke(savedPersonal, [savedViaPlugin]);
    savedPersonal = load.Invoke(configurations, [product])!;
    Assert(!(bool)personalHuntSetting.GetValue(savedPersonal)! && (bool)configurationType.GetProperty("AutomaticSync")!.GetValue(savedPersonal)!
        && !(bool)marketSetting!.GetValue(savedPersonal)!, "Personal OFF preserves ordinary ON and market OFF through reload.");
    Console.WriteLine("Actual Testing personal consent defaults/independent switches/ordinary and market isolation passed.");
} else Assert(personalHuntSetting is null && personalSubSetting is null && pluginAssembly.GetType("GillionsGameSync.PersonalSyncPolicy") is null,
    "Stable must not contain personal transport or permission settings.");
if (testingProduct) {
    Assert(marketSetting is not null && (bool)marketSetting.GetValue(Activator.CreateInstance(configurationType))!,
        "New Testing configuration must default market contribution ON.");
    var olderTesting = JsonConvert.DeserializeObject("{\"AutomaticSync\":false}", configurationType)!;
    Assert((bool)marketSetting!.GetValue(olderTesting)!, "Older config missing market choice must get the owner-selected ON default.");
    marketSetting.SetValue(olderTesting, false);
    configurationType.GetMethod("Save")!.Invoke(olderTesting, [savedViaPlugin]);
    var marketReload = load.Invoke(configurations, [product])!;
    Assert(!(bool)marketSetting.GetValue(marketReload)! && !(bool)configurationType.GetProperty("AutomaticSync")!.GetValue(marketReload)!,
        "Actual Dalamud Save/load must preserve market opt-out without modifying ordinary sync.");
    configurationType.GetProperty("AutomaticSync")!.SetValue(marketReload, true);
    configurationType.GetMethod("Save")!.Invoke(marketReload, [savedViaPlugin]);
    marketReload = load.Invoke(configurations, [product])!;
    Assert(!(bool)marketSetting.GetValue(marketReload)! && (bool)configurationType.GetProperty("AutomaticSync")!.GetValue(marketReload)!,
        "Market OFF must coexist with ordinary Automatic sync ON through actual Save/load.");
    var marketStop = configurationType.GetProperty("GillionsMarketBlockedGeneration")!;
    marketStop.SetValue(marketReload, "synthetic-market-denied");
    configurationType.GetMethod("Save")!.Invoke(marketReload, [savedViaPlugin]);
    Assert((string)marketStop.GetValue(load.Invoke(configurations, [product]))! == "synthetic-market-denied",
        "Market enrollment-denial stop must survive actual configuration Save/load.");
    Assert(pluginAssembly.GetType("GillionsGameSync.MarketContributionSource") is not null, "Existing Testing Plugin must contain passive market adapter.");
    Console.WriteLine("Actual Testing market default/older-config/opt-out/ordinary isolation/enrollment-stop Save/load passed.");
} else {
    Assert(marketSetting is null && pluginAssembly.GetType("GillionsGameSync.MarketContributor") is null
        && pluginAssembly.GetType("GillionsGameSync.MarketContributionSource") is null,
        "Stable must not gain market contribution.");
}
var blockedGeneration = configurationType.GetProperty("GillionsPartyFinderBlockedGeneration")!;
var deniedConfig = Activator.CreateInstance(configurationType)!;
blockedGeneration.SetValue(deniedConfig, "synthetic-denied-enrollment");
configurationType.GetMethod("Save")!.Invoke(deniedConfig, [savedViaPlugin]);
var deniedReload = load.Invoke(configurations, [product])!;
Assert((string)blockedGeneration.GetValue(deniedReload)! == "synthetic-denied-enrollment",
    "Authorization stop must survive actual Dalamud configuration Save/load.");
blockedGeneration.SetValue(deniedReload, "");
configurationType.GetMethod("Save")!.Invoke(deniedReload, [savedViaPlugin]);
Assert((string)blockedGeneration.GetValue(load.Invoke(configurations, [product]))! == "",
    "A new/default authorization-stop marker must round-trip without listing data.");
var submarineProperty = configurationType.GetProperty("SubmarineVoyages");
var submarineViewType = pluginAssembly.GetType("GillionsGameSync.SubmarineLocalView");
if (testingProduct) {
    Assert(submarineProperty is not null && submarineViewType is not null,
        "Testing product must own the local submarine view and retained format.");
    var defaults = submarineProperty!.GetValue(Activator.CreateInstance(configurationType))!;
    Assert(!(bool)defaults.GetType().GetProperty("LocalRetentionEnabled")!.GetValue(defaults)!
        && !(bool)defaults.GetType().GetProperty("CommunityContributionEnabled")!.GetValue(defaults)!,
        "Submarine local retention and community preparation must be separate off-by-default controls.");
    var syntheticRetention = File.ReadAllText(Path.GetFullPath("artifacts/verification/submarine-policy/retained-fixture.json"));
    var syntheticConfig = Activator.CreateInstance(configurationType)!;
    submarineProperty.SetValue(syntheticConfig, JsonConvert.DeserializeObject(syntheticRetention, submarineProperty.PropertyType));
    configurationType.GetMethod("Save")!.Invoke(syntheticConfig, [savedViaPlugin]);
    var reloadedRetention = submarineProperty.GetValue(load.Invoke(configurations, [product]))!;
    Assert(JToken.DeepEquals(ParseToken(syntheticRetention), ParseToken(JsonConvert.SerializeObject(reloadedRetention))),
        "Actual Dalamud Save/reload must preserve voyage anchor, build, per-sector results, consent and public observation identity.");
    var policyType = pluginAssembly.GetType("GillionsGameSync.SubmarineVoyageRetentionPolicy", true)!;
    var retentionPolicy = Activator.CreateInstance(policyType, [reloadedRetention])!;
    Assert((bool)policyType.GetProperty("Supported", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(retentionPolicy)!,
        "Actual reloaded retained format must remain valid for policy use.");
    var sanitized = (string)policyType.GetMethod("PrepareExport", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(retentionPolicy, null)!;
    Assert(Parse(sanitized)["voyages"] is JArray { Count: 1 } && !sanitized.Contains("LOCAL-NAME-ONLY"),
        "Actual saved/reloaded retained results must produce only the consented sanitized dataset.");
    Console.WriteLine("Actual submarine configuration Save/load and sanitized export passed; no native collection performed.");
    var privateRetention = File.ReadAllText(Path.GetFullPath("artifacts/verification/submarine-policy/personal-retained-fixture.json"));
    submarineProperty.SetValue(syntheticConfig, JsonConvert.DeserializeObject(privateRetention, submarineProperty.PropertyType));
    configurationType.GetMethod("Save")!.Invoke(syntheticConfig, [savedViaPlugin]);
    var privateReload = submarineProperty.GetValue(load.Invoke(configurations, [product]))!;
    Assert(JToken.DeepEquals(ParseToken(privateRetention), ParseToken(JsonConvert.SerializeObject(privateReload))),
        "Actual Save/load must preserve additive private scope/sector/UTC fields.");
    Console.WriteLine("Actual submarine private scope/sector/provenance Save/load passed; synthetic only.");
} else {
    Assert(submarineProperty is null && submarineViewType is null
        && pluginAssembly.GetType("GillionsGameSync.SubmarineVoyageRetention") is null,
        "Stable product must not gain submarine collection or persisted format.");
}

var huntProperty = configurationType.GetProperty("HuntBills");
var huntType = pluginAssembly.GetType("GillionsGameSync.HuntBillLocalView");
var displayReader=System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(huntType!);
var displayStore=Activator.CreateInstance(pluginAssembly.GetType("GillionsGameSync.HuntBillRetention",true)!)!;
const BindingFlags localFlags=BindingFlags.NonPublic|BindingFlags.Instance;
huntType!.GetField("store",localFlags)!.SetValue(displayReader,displayStore);
Assert(!(bool)huntType.GetProperty("ReadEnabled",localFlags)!.GetValue(displayReader)!,"Both local choices OFF must stop Hunt reads.");
huntType.GetProperty("ProgressRequested",localFlags)!.SetValue(displayReader,true);
Assert((bool)huntType.GetProperty("ReadEnabled",localFlags)!.GetValue(displayReader)!
    && !(bool)displayStore.GetType().GetProperty("LocalRetentionEnabled")!.GetValue(displayStore)!,
    "Display-only Hunt reads must not grant retention or upload permission.");
huntType.GetProperty("ProgressRequested",localFlags)!.SetValue(displayReader,false);
Assert(!(bool)huntType.GetProperty("ReadEnabled",localFlags)!.GetValue(displayReader)!,"Display OFF must stop RAM-only Hunt reads.");
Console.WriteLine($"Packaged Hunt Progress RAM-only/default-OFF/read isolation PASS: {product}.");
if (testingProduct) {
    Assert(huntProperty is not null && huntType is not null, "Testing Hunt collection missing from actual binary.");
    var huntConfig = Activator.CreateInstance(configurationType)!;
    var huntStore = huntProperty!.GetValue(huntConfig)!;
    Assert(!(bool)huntStore.GetType().GetProperty("LocalRetentionEnabled")!.GetValue(huntStore)!, "Hunt retention must default OFF.");
    var fixture = File.ReadAllText(Path.GetFullPath("artifacts/verification/personal-state/hunt-bills-v1.json"));
    var billArray = Parse(fixture)["bills"]!;
    var retained = new JObject { ["SchemaVersion"] = 1, ["LocalRetentionEnabled"] = true,
        ["Characters"] = new JArray(new JObject { ["LocalCharacterKey"] = new string('a', 64), ["Bills"] = billArray }) };
    huntProperty.SetValue(huntConfig, JsonConvert.DeserializeObject(retained.ToString(), huntProperty.PropertyType));
    configurationType.GetMethod("Save")!.Invoke(huntConfig, [savedViaPlugin]);
    var huntReload = huntProperty.GetValue(load.Invoke(configurations, [product]))!;
    var huntPolicyType = pluginAssembly.GetType("GillionsGameSync.HuntBillRetentionPolicy", true)!;
    var huntPolicy = Activator.CreateInstance(huntPolicyType, [huntReload])!;
    Assert((bool)huntPolicyType.GetProperty("Supported", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(huntPolicy)!,
        "Actual Dalamud Hunt Save/load lost fields, UTC kinds or observation identity.");
    huntReload.GetType().GetProperty("LocalRetentionEnabled")!.SetValue(huntReload, false);
    huntProperty.SetValue(huntConfig, huntReload); configurationType.GetMethod("Save")!.Invoke(huntConfig, [savedViaPlugin]);
    var huntOff = huntProperty.GetValue(load.Invoke(configurations, [product]))!;
    Assert(!(bool)huntOff.GetType().GetProperty("LocalRetentionEnabled")!.GetValue(huntOff)!, "Hunt opt-out did not survive actual serializer.");
    Console.WriteLine("Actual Hunt Testing-only/default-OFF/private-state/UTC/identity/opt-out Save/load passed; no native collection.");
} else {
    Assert(huntProperty is not null && huntType is not null && pluginAssembly.GetType("GillionsGameSync.PersonalSyncPolicy") is null,
        "Public Hunt Progress must reuse the local reader without promoting personal upload transport.");
    var local = huntProperty!.GetValue(Activator.CreateInstance(configurationType))!;
    Assert(!(bool)local.GetType().GetProperty("LocalRetentionEnabled")!.GetValue(local)!,"Stable Hunt Progress must not enable retained history.");
}

// Type metadata deliberately names retired types. Dalamud LoadForType and the
var dashboardProperty = configurationType.GetProperty("DashboardFacts");
var dashboardView = pluginAssembly.GetType("GillionsGameSync.DashboardLocalView");
if (testingProduct) {
    Assert(dashboardProperty is not null && dashboardView is not null, "Testing Dashboard facts missing.");
    var config = JsonConvert.DeserializeObject("{\"AutomaticSync\":true,\"ContributeObservedMarketData\":false}", configurationType)!;
    var empty = dashboardProperty!.GetValue(config)!;
    Assert(!(bool)empty.GetType().GetProperty("LocalRetentionEnabled")!.GetValue(empty)!, "Older configs must default new facts OFF.");
    var export = Parse(File.ReadAllText("artifacts/verification/dashboard/dashboard-facts-v1.json"));
    var observationKeys = new[] { "system", "scopeId", "observedAtUtc", "nextAtUtc", "values", "gameVersion", "nativeVersion", "collectorVersion" };
    var rows = new JArray(((JArray)export["observations"]!).Cast<JObject>()
        .Select(o => new JObject(observationKeys.Select(k => new JProperty(k, o[k]!.DeepClone())))));
    var retained = new JObject { ["SchemaVersion"] = 1, ["LocalRetentionEnabled"] = true,
        ["Characters"] = new JArray(new JObject { ["LocalCharacterKey"] = new string('a', 64), ["Observations"] = rows }) };
    dashboardProperty.SetValue(config, JsonConvert.DeserializeObject(retained.ToString(), dashboardProperty.PropertyType));
    configurationType.GetMethod("Save")!.Invoke(config, [savedViaPlugin]);
    var loaded = load.Invoke(configurations, [product])!;
    var retention = dashboardProperty.GetValue(loaded)!;
    var type = pluginAssembly.GetType("GillionsGameSync.DashboardRetentionPolicy", true)!;
    var policy = Activator.CreateInstance(type, [retention])!;
    Assert((bool)type.GetProperty("Supported", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(policy)!, "Actual Dashboard serializer lost fields or UTC provenance.");
    Assert((bool)configurationType.GetProperty("AutomaticSync")!.GetValue(loaded)! && !(bool)marketSetting!.GetValue(loaded)!, "Facts retention changed ordinary sync/market opt-out.");
    retention.GetType().GetProperty("LocalRetentionEnabled")!.SetValue(retention, false);
    configurationType.GetMethod("Save")!.Invoke(loaded, [savedViaPlugin]);
    retention = dashboardProperty.GetValue(load.Invoke(configurations, [product]))!;
    Assert(!(bool)retention.GetType().GetProperty("LocalRetentionEnabled")!.GetValue(retention)!, "Facts opt-out not durable.");
    var future = Parse(File.ReadAllText(pathForFixture));
    future["DashboardFacts"]!["SchemaVersion"] = 99;
    future["DashboardFacts"]!["FutureData"] = new JObject { ["inert"] = "preserve" };
    File.WriteAllText(pathForFixture, future.ToString());
    loaded = load.Invoke(configurations, [product])!;
    configurationType.GetMethod("Save")!.Invoke(loaded, [savedViaPlugin]);
    Assert(Parse(File.ReadAllText(pathForFixture))["DashboardFacts"]!["FutureData"]!.Value<string>("inert") == "preserve", "Unknown future facts retention stripped.");
    Console.WriteLine("Actual Dashboard facts Testing-only/default-OFF/UTC/restart/opt-out/unknown-schema preservation passed; synthetic, no native collection.");
} else Assert(dashboardProperty is null && dashboardView is null && pluginAssembly.GetType("GillionsGameSync.DashboardRetention") is null,
    "Stable must not gain daily/weekly experimental retention or collectors.");

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
var partyFinderOptedIn = (JObject)original.DeepClone();
partyFinderOptedIn["EnablePartyFinderContributions"] = true;
cases.Add(partyFinderOptedIn);
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
    Assert(roundTrip.Value<bool>("EnablePartyFinderContributions") == fixture.Value<bool>("EnablePartyFinderContributions"),
        "Party Finder opt-in must persist only when explicitly enabled; absent upgrades remain off.");
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
