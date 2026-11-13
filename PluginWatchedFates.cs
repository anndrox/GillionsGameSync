#if GILLIONS_TEST_BUILD
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace GillionsGameSync;

public sealed partial class Plugin {
    private const string WatchedFatePath = "/api/game-sync/watched-fates";
    private readonly INotificationManager watchedFateNotifications;
    private readonly WatchedFatePoll watchedFatePoll = new();
    private string? watchedFatePairing;
    private (SyncRequestPermit Permit, Task<WatchedFateReply> Task)? watchedFateRequest;
    private long watchedFateViewUntil;
    private long nextWatchedFateAlert;

    private string CurrentFateWorld() => huntPlayerState.CurrentWorld.ValueNullable?.Name.ExtractText() ?? "";

    private void RequestWatchedFateMarkers() =>
        Volatile.Write(ref watchedFateViewUntil, Environment.TickCount64 + 1000);

    private bool IsWatchedFate(uint id, long now) => watchedFatePoll.IsWatched(id, now)
        || watchedFatePoll.LegacyFallback && trackedFates?.Items.ContainsKey(id) == true;

    private void TickWatchedFates(DateTime now) {
        if (HasPairedSession && configuration.ActiveSession!.Generation != watchedFatePairing) {
            watchedFatePairing = configuration.ActiveSession.Generation;
            watchedFatePoll.NewPairing();
        }
        var tick = Environment.TickCount64;
        if (watchedFateRequest is { } request) {
            if (!request.Task.IsCompleted) return;
            watchedFateRequest = null;
            var reply = request.Task.GetAwaiter().GetResult();
            if (PermitIsCurrent(request.Permit))
                watchedFatePoll.Complete(reply, tick, Random.Shared.Next(-2000, 2001));
        }
        if (!clientState.IsLoggedIn || !HasPairedSession || activeOwnedState is null
            || !NativeProduct.TransportOrigin(configuration.ActiveSession!.Origin)) return;

        if ((configuration.TrackedFateAlerts || tick < Volatile.Read(ref watchedFateViewUntil))
            && watchedFatePoll.Due(tick)) {
            var permit = CapturePermit(SyncRequestMode.Manual);
            watchedFateRequest = (permit, ReadWatchedFatesAsync(permit, watchedFatePoll.ETag(tick)));
        }

        if (!configuration.TrackedFateAlerts || tick < nextWatchedFateAlert) return;
        nextWatchedFateAlert = tick + 1000;
        if (!HuntAwarenessReady()) return;
        var world = CurrentFateWorld();
        if (world.Length == 0) return;
        foreach (var occurrence in watchedFatePoll.Active(tick)) {
            if (!fateAlerts.ObserveSite(occurrence.Id, occurrence.Fate.FateId, occurrence.World, now)) continue;
            string? elsewhere = string.Equals(world, occurrence.World, StringComparison.OrdinalIgnoreCase)
                ? null : $"{occurrence.World} ({occurrence.DataCentre})";
            PrintTrackedFateAlert(occurrence.Fate, null, elsewhere, occurrence.EndsAt);
        }
    }

    // Starts on the framework thread immediately after capturing the session
    // permit. The framework checks the same permit again before applying results.
    private async Task<WatchedFateReply> ReadWatchedFatesAsync(SyncRequestPermit permit, string? etag) {
        double? retry = null;
        try {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(permit.Cancellation);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));
            using var request = new HttpRequestMessage(HttpMethod.Get, permit.Origin + WatchedFatePath);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", permit.Token);
            request.Headers.UserAgent.ParseAdd($"{RetainerClient.ProductName}/{PluginVersion}");
            if (etag is not null) request.Headers.IfNoneMatch.ParseAdd(etag);
            var started = Stopwatch.GetTimestamp();
            using var response = await personalHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.RequestMessage?.RequestUri != request.RequestUri) return new(HttpStatusCode.BadGateway);
            retry = response.Headers.RetryAfter?.Delta?.TotalSeconds
                ?? (response.Headers.RetryAfter?.Date is { } date ? (date - DateTimeOffset.UtcNow).TotalSeconds : null);
            var tag = response.Headers.ETag is { Tag.Length: 66 } validator ? validator.ToString() : null;
            if (response.StatusCode != HttpStatusCode.OK) return new(response.StatusCode, ETag: tag, RetryAfter: retry);
            var clock = WebsiteResponseClock.Capture(response.Headers.Date, DateTime.UtcNow, Stopwatch.GetElapsedTime(started));
            var json = await SyncResponsePolicy.ReadBoundedAsync(response.Content, WatchedFateSnapshot.MaximumBytes, deadline.Token);
            return new(response.StatusCode, WatchedFateSnapshot.Parse(json), tag, clock, retry);
        } catch (Exception error) when (error is HttpRequestException or OperationCanceledException or System.IO.IOException
            or InvalidOperationException or System.Text.Json.JsonException or FormatException) {
            return new(HttpStatusCode.ServiceUnavailable, RetryAfter: retry);
        }
    }
}
#endif
