using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GillionsGameSync;

internal static class DeviceRevocation {
    internal static async Task<bool> TryRevokeAsync(HttpClient client, string origin, string token, string version,
        CancellationToken cancellation) {
        if (!NativeProduct.RequestOrigin(origin)) return false;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        using var request = new HttpRequestMessage(HttpMethod.Post, origin + "/api/game-sync/revoke-self") {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd($"{NativeProduct.Name}/{version}");
        if (!NativeProduct.CanDispatch(origin, request.RequestUri)) return false;
        try {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode != HttpStatusCode.OK) return false;
            using var receipt = JsonDocument.Parse(await SyncResponsePolicy.ReadBoundedAsync(response.Content, 4096, deadline.Token));
            return receipt.RootElement.ValueKind == JsonValueKind.Object
                && receipt.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True;
        } catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException or JsonException or InvalidOperationException) {
            return false;
        }
    }
}
