using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal static class DeviceRevocationPackagedTests {
    private const string Origin = "https://test.gillions.app";
    private const string Credential = "SYNTHETIC_DEVICE_CREDENTIAL_0000000000";
    private const string DeviceId = "00000000-0000-4000-8000-000000000001";
    private const string Generation = "00000000-0000-4000-8000-000000000002";
    private const BindingFlags InternalInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    internal static async Task RunAsync(Assembly assembly) {
        var product = assembly.GetName().Name!;
        var testing = product == "GillionsGameSyncTest";
        var origin = testing ? Origin : "https://gillions.app";
        var configuration = assembly.GetType("GillionsGameSync.PluginConfiguration", true)!;
        var pairing = assembly.GetType("GillionsGameSync.PairedSession", true)!;
        var revocation = assembly.GetType("GillionsGameSync.DeviceRevocation", true)!
            .GetMethod("TryRevokeAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        var credential = configuration.GetProperty("DeviceToken")!;
        var session = configuration.GetProperty("ActiveSession")!;
        var upgrade = configuration.GetMethod("UpgradeDevicePairing", InternalInstance)!;
        var clear = configuration.GetMethod("ClearDevicePairing", InternalInstance)!;

        var legacy = JObject.FromObject(new { DeviceToken = Credential, DeviceId, ActiveSession = new {
            SchemaVersion = 1, Origin = origin, DeviceId, Generation,
            CredentialFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Credential)))
        }});
        object Load(JObject json) => JsonConvert.DeserializeObject(json.ToString(), configuration)!;
        var config = Load(legacy);
        Check((bool)upgrade.Invoke(config, [])!, "Upgrade an approved legacy pairing");
        var saved = JObject.FromObject(config);
        var protectedValue = saved.Value<string>("ProtectedDeviceToken");
        Check(saved["DeviceToken"] is null && !string.IsNullOrEmpty(protectedValue)
            && !saved.ToString().Contains(Credential, StringComparison.Ordinal), "Legacy plaintext is replaced by a protected blob");
        Check(saved["ActiveSession"]!.Value<int>("SchemaVersion") == 2
            && saved["ActiveSession"]!.Value<string>("Generation") == Generation, "Upgrade preserves local ownership");
        var reopened = Load(saved);
        Check(credential.GetValue(reopened) as string == Credential, "Same Windows user can recover the saved credential");
        Check((bool)pairing.GetMethod("IsValid")!.Invoke(session.GetValue(reopened), [DeviceId, Credential])!,
            "The migrated pairing remains usable");
        var tampered = (JObject)saved.DeepClone();
        tampered["ActiveSession"]!["Origin"] = testing ? "https://gillions.app" : Origin;
        Check(!(bool)pairing.GetMethod("IsValid")!.Invoke(session.GetValue(Load(tampered)), [DeviceId, Credential])!,
            "Changing the pairing origin invalidates the credential fingerprint");
        var foreignLegacy = (JObject)legacy.DeepClone();
        foreignLegacy["ActiveSession"]!["Origin"] = "https://example.invalid";
        Check(!(bool)upgrade.Invoke(Load(foreignLegacy), [])!, "Legacy migration cannot activate a non-Gillions pairing");
        var corrupt = (JObject)saved.DeepClone();
        corrupt["ProtectedDeviceToken"] = "not valid base64";
        corrupt["DeviceToken"] = Credential;
        var broken = Load(corrupt);
        Check(session.GetValue(broken) is null && credential.GetValue(broken) as string == ""
            && (bool)configuration.GetProperty("PairingRequired")!.GetValue(broken)!,
            "Corrupt DPAPI data fails closed even when a plaintext field is present");

        async Task<bool> RevokeAsync(HttpClient client, string destination, CancellationToken cancellation = default) =>
            await (Task<bool>)revocation.Invoke(null, [client, destination, Credential, "9.8.7.6", cancellation])!;

        foreach (var (status, reply, expected) in new[] {
            (HttpStatusCode.OK, """{"ok":true}""", true),
            (HttpStatusCode.OK, """{"ok":false}""", false),
            (HttpStatusCode.OK, "malformed", false),
            (HttpStatusCode.TemporaryRedirect, """{"ok":true}""", false),
            (HttpStatusCode.Unauthorized, """{"ok":false}""", false),
            (HttpStatusCode.ServiceUnavailable, """{"ok":false}""", false)
        }) {
            var requestCount = 0;
            using var handler = new StubHandler(async (request, cancellation) => {
                requestCount++;
                Check(request.Method == HttpMethod.Post && request.RequestUri?.AbsoluteUri == origin + "/api/game-sync/revoke-self",
                    "Disconnect uses the paired origin's self-revocation route");
                Check(request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == Credential,
                    "Self-revocation uses the captured device credential");
                Check(request.Headers.UserAgent.ToString() == product + "/9.8.7.6"
                    && await request.Content!.ReadAsStringAsync(cancellation) == "{}", "Disconnect passes only the expected Site contract");
                return new HttpResponseMessage(status) { Content = new StringContent(reply) };
            });
            using var client = new HttpClient(handler);
            var local = Load(saved);
            Check(await RevokeAsync(client, origin) == expected && requestCount == 1, "Require a confirmed Site receipt");
            Check(credential.GetValue(local) as string == Credential, "Credential remains available until the Site attempt completes");
            clear.Invoke(local, []);
            var disconnected = JObject.FromObject(local);
            Check(disconnected["DeviceToken"] is null && disconnected["ProtectedDeviceToken"] is null
                && session.GetValue(local) is null && (bool)configuration.GetProperty("PairingRequired")!.GetValue(local)!,
                "Disconnect removes both local credential and session regardless of confirmation");
        }
        using var offline = new HttpClient(new StubHandler((_, _) => throw new HttpRequestException("Offline fixture")));
        Check(!await RevokeAsync(offline, origin), "Offline Site falls back to local disconnect");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Check(!await RevokeAsync(offline, origin, cancelled.Token), "Cancellation does not claim revocation");
        using var unexpected = new HttpClient(new StubHandler((_, _) => throw new Exception("Unexpected HTTP dispatch")));
        Check(!await RevokeAsync(unexpected, "https://example.invalid"), "A foreign origin cannot receive the credential");
        if (testing)
            Check(!await RevokeAsync(unexpected, "https://gillions.app"), "Testing cannot revoke at Production");
        Console.WriteLine($"Credential migration, Windows DPAPI and simulated Site revocation PASS: {product}.");
    }

    private static void Check(bool condition, string message) {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
