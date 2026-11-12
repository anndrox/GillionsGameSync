using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

internal static class AppearanceBackgroundPackagedTests {
    internal static void Run(Assembly assembly, bool baseline = false) {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
        Type TypeOf(string name) => assembly.GetType("GillionsGameSync." + name, true)!;
        var pluginType = TypeOf("Plugin");
        var configType = TypeOf("PluginConfiguration");
        var config = Activator.CreateInstance(configType)!;
        configType.GetProperty("AutomaticSync")!.SetValue(config, false);
        var warnings = new List<(string Message, string[] Values)>();
        var login = new List<Delegate>();
        Delegate? update = null;
        var loggedIn = false;
        var failPlayerRead = false;
        var failDutyRead = false;
        const ulong character = 123;

        object Mock(Type type, Func<MethodInfo, object?[]?, object?> handler) {
            var mock = DispatchProxy.Create(type, typeof(CollectibleServiceProxy));
            ((CollectibleServiceProxy)mock).Handler = handler;
            return mock;
        }
        static object? Default(MethodInfo method) => method.ReturnType == typeof(void) || !method.ReturnType.IsValueType
            ? null : Activator.CreateInstance(method.ReturnType);
        IFramework? callbackFramework = null;
        var framework = (IFramework)Mock(typeof(IFramework), (method, args) => {
            if (method.Name == "add_Update" && args![0] is Delegate callback && callback.Method.Name == "OnFrameworkUpdate") {
                update = callback;
                // Reenter on subscription, the earliest frame the framework can deliver.
                callback.DynamicInvoke(callbackFramework);
            }
            if (method.Name == "RunOnFrameworkThread" && args![0] is Action action) {
                try { action(); return Task.CompletedTask; }
                catch (Exception error) { return Task.FromException(error); }
            }
            return Default(method);
        });
        callbackFramework = framework;
        var gameData = (Lumina.GameData)RuntimeHelpers.GetUninitializedObject(typeof(Lumina.GameData));
        var repositories = typeof(Lumina.GameData).GetFields(instance).Single(field => field.Name.Contains("Repositories", StringComparison.OrdinalIgnoreCase));
        repositories.SetValue(gameData, Activator.CreateInstance(repositories.FieldType));
        var ui = Mock(typeof(IDalamudPluginInterface).GetProperty("UiBuilder")!.PropertyType, (method, _) => Default(method));
        var constructor = pluginType.GetConstructors().Single();
        object? Service(ParameterInfo parameter) {
            var type = parameter.ParameterType;
            if (type == typeof(IFramework)) return framework;
            if (type == typeof(IDataManager)) return new UnavailableCollectibleData(gameData);
            return Mock(type, (method, args) => {
                if (type == typeof(IDalamudPluginInterface)) {
                    if (method.Name == "GetPluginConfig") return config;
                    if (method.Name == "get_UiBuilder") return ui;
                }
                if (type == typeof(IClientState)) {
                    if (method.Name == "get_IsLoggedIn") return loggedIn;
                    if (method.Name == "add_Login") login.Add((Delegate)args![0]!);
                }
                if (type == typeof(IPlayerState)) {
                    if (method.Name == "get_ContentId") return character;
                    if (method.Name == "get_IsLoaded") {
                        if (failPlayerRead) {
                            failPlayerRead = false;
                            throw new NullReferenceException("Synthetic first login frame failure.");
                        }
                        return loggedIn;
                    }
                }
                if (type.Name == "ICondition" && method.Name == "get_Item" && failDutyRead) {
                    failDutyRead = false;
                    throw new NullReferenceException("Synthetic duty observation failure.");
                }
                if (type == typeof(IPluginLog) && method.Name == "Warning") {
                    warnings.Add((args!.OfType<string>().Single(),
                        args!.OfType<object[]>().SelectMany(values => values).OfType<string>().ToArray()));
                }
                return Default(method);
            });
        }
        object plugin;
        try { plugin = constructor.Invoke(constructor.GetParameters().Select(Service).ToArray()); }
        catch (TargetInvocationException error) when (baseline && error.GetBaseException() is NullReferenceException
            && warnings.Any(warning => warning.Message.Contains("background observation paused", StringComparison.Ordinal))) {
            Console.WriteLine("Released baseline reproduced: framework reentry at subscription hit the background NRE and UI cleanup threw.");
            return;
        }
        if (baseline || update is null || warnings.Count != 0)
            throw new Exception("Startup framework callback failed. " + string.Join("\n", warnings.Select(warning => string.Join(" ", warning.Values))));

        object? Get(string field) => pluginType.GetField(field, instance)!.GetValue(plugin);
        void Set(string field, object? value) => pluginType.GetField(field, instance)!.SetValue(plugin, value);
        object? Call(string method, params object?[] arguments) => pluginType.GetMethods(instance)
            .Single(candidate => candidate.Name == method && candidate.GetParameters().Length == arguments.Length).Invoke(plugin, arguments);
        var snapshotType = TypeOf("AppearanceSnapshot");
        var slots = (string[])TypeOf("AppearancePolicy").GetField("Slots", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var look = JsonSerializer.Deserialize(JsonSerializer.Serialize(new {
            CustomizationHex = new string('0', 52),
            ObservedAtUtc = "2026-11-11T18:00:00.000Z",
            Equipment = slots.ToDictionary(key => key, _ => new { ItemId = 123u, Stain0Id = 4 }),
        }), snapshotType)!;
        var samples = 0;
        Set("appearanceCollector", Mock(TypeOf("ICharacterAppearanceSource"), (method, _) => {
            if (method.Name == "Available") return true;
            samples++;
            return Activator.CreateInstance(method.ReturnType, [look, null]);
        }));
        var transport = new AppearanceTransport();
        using var http = new HttpClient(transport);
        // Every client is in-process, including unexpected requests during the fixture.
        foreach (var field in new[] { "http", "personalHttp", "partyFinderHttp", "marketHttp" }) {
            ((HttpClient)Get(field)!).Dispose();
            Set(field, http);
        }
        const string origin = "https://test.gillions.app";
        const string device = "00000000-0000-4000-8000-000000000001";
        const string fixtureToken = "fixture-appearance-no-network";
        var session = TypeOf("PairedSession").GetMethod("Create")!.Invoke(null, [origin, device, fixtureToken])!;
        configType.GetProperty("ActiveSession")!.SetValue(config, session);
        configType.GetProperty("DeviceId")!.SetValue(config, device);
        configType.GetProperty("DeviceToken")!.SetValue(config, fixtureToken);

        void LoginWithGrant() {
            loggedIn = true;
            foreach (var callback in login) callback.DynamicInvoke();
            Call("RefreshSessionContext");
            // A synthetic local character and ready authenticated grant substitute
            // for the game's actor and the site's presence response.
            var owned = TypeOf("SyncOwnershipPolicy").GetMethod("GetCharacter")!.Invoke(null,
                [configType.GetProperty("OwnedCharacters")!.GetValue(config), session, character]);
            Set("activeOwnedState", owned);
            Set("appearanceGrant", "00000000-0000-4000-8000-000000000002");
            Set("appearanceLease", DateTime.UtcNow.AddSeconds(25));
            Set("appearanceNextRead", DateTime.MinValue);
            Set("appearanceNextSend", DateTime.MinValue);
            Set("nextRetainerPresenceUtc", DateTime.MaxValue);
            Set("nextHuntFocusPresenceUtc", DateTime.MaxValue);
            Set("nextTransientMaintenanceUtc", DateTime.MaxValue);
        }
        void Frame() => update.DynamicInvoke(framework);
        void Require(bool pass, string why) {
            if (!pass) throw new Exception("Packaged background observation: " + why);
        }
        void ExpectReceipt(int uploads) {
            Require(SpinWait.SpinUntil(() => Get("appearanceFlight") is null && Get("appearanceLastSentUtc") is DateTime,
                TimeSpan.FromSeconds(2)), "appearance receipt and flight cleanup completed within the five-second sample interval");
            Require(transport.Uploads == uploads && transport.Unexpected == 0, "one real appearance send per expected fixture receipt");
            Require(((string)Call("AppearanceStatus", DateTime.UtcNow)!).StartsWith("Last sent ", StringComparison.Ordinal), "accepted receipt shows Last sent");
        }

        LoginWithGrant();
        // An earlier component fails on a frame before appearance becomes due.
        Set("appearanceNextRead", DateTime.MaxValue);
        failDutyRead = true;
        Frame();
        Require(transport.Uploads == 0, "appearance cadence held on the injected fault frame");
        Set("appearanceNextRead", DateTime.MinValue);
        Frame();
        ExpectReceipt(1);
        Require(samples == 1 && warnings.Count(w => w.Values is ["duty state", "NullReferenceException", { Length: > 0 }]) == 1,
            "the first fault logged a stack at Warning");

        LoginWithGrant();
        var firstFrame = Stopwatch.StartNew();
        failPlayerRead = true;
        Frame();
        Require(transport.Uploads == 1, "an uncertain session context withholds its frame");
        failDutyRead = true;
        Frame();
        ExpectReceipt(2);
        Require(firstFrame.Elapsed < TimeSpan.FromSeconds(5), "post-login upload within one sample interval after the first-frame exception");
        Require(warnings.Select(w => w.Values.FirstOrDefault()).SequenceEqual(["duty state", "session context"])
            && warnings.All(w => w.Values is [_, "NullReferenceException", { Length: > 0 }]),
            "repeated duty fault logs once and the session fault logs independently");
        Set("publicConnectionFailure", "CHARACTER_NOT_LINKED");
        Require((string)Call("AppearanceStatus", DateTime.UtcNow)! == "Verify this character on gillions.app.",
            "character refusal shows the plain verification reason");

        var watchdog = Get("backgroundObservation")!;
        var permit = Call("CapturePermit", Enum.Parse(TypeOf("SyncRequestMode"), "Personal"))!;
        var priorCancellation = (CancellationToken)permit.GetType().GetProperty("Cancellation")!.GetValue(permit)!;
        watchdog.GetType().GetField("lastProgress", instance)!.SetValue(watchdog, Environment.TickCount64 - 30_000);
        Frame();
        Require(priorCancellation.IsCancellationRequested && Get("appearanceGrant") is null && transport.Uploads == 2,
            "a stalled paired frame resets the observation context and revokes the old grant and request permit");
        bool Recover(long time, bool paired) => (bool)watchdog.GetType().GetMethod("RecoverIfStalled", instance)!.Invoke(watchdog, [time, paired])!;
        void Complete(long time) => watchdog.GetType().GetMethod("Completed", instance)!.Invoke(watchdog, [time]);
        Require(!Recover(0, false) && !Recover(100, true) && !Recover(30_099, true) && Recover(30_100, true), "watchdog recovers only after 30 seconds without a completed paired frame");
        Complete(30_200);
        Require(!Recover(60_199, true) && Recover(60_200, true) && !Recover(100_000, false), "completed frames restart the watchdog and unpaired frames do not recover");
        ((IDisposable)plugin).Dispose();
        Console.WriteLine("Packaged background observation PASS: constructor reentry, frame isolation, post-login upload, warning stacks, flight cleanup and watchdog.");
    }

    private sealed class AppearanceTransport : HttpMessageHandler {
        internal int Uploads, Unexpected;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var body = document.RootElement;
            if (!request.Headers.Contains("X-Gillions-Appearance-Grant")
                || body.GetProperty("resourceType").GetString() != "character_appearance") {
                Interlocked.Increment(ref Unexpected);
                return new(HttpStatusCode.BadRequest);
            }
            Interlocked.Increment(ref Uploads);
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new {
                ok = true, nonce = body.GetProperty("nonce").GetString(),
                contentHash = body.GetProperty("payload").GetProperty("contentHash").GetString(),
            })) };
        }
    }
}
