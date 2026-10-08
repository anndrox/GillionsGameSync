#if GILLIONS_TEST_BUILD || GILLIONS_PUBLIC_BUILD || GILLIONS_PF_LINK_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GillionsGameSync;

internal sealed record PartyFinderLinkRequest(string RequestId, string ClaimToken, string ListingKey,
    uint ListingId, string RecruiterName, bool CrossWorld, DateTime ObservedAtUtc,
    DateTime ListingExpiresAtUtc, DateTime ExpiresAtUtc);

internal static class PartyFinderLinkPolicy {
    internal const string Contract = "native-requests-v1";
    internal const string Capability = "native_party_finder_link_v1";
    internal const string RequestType = "party_finder";
    internal const int MaximumLifetimeSeconds = 60;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    internal static bool RecruiterValid(string? name) {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 64 || name != name.Trim()) return false;
        try {
            return StrictUtf8.GetByteCount(name) <= 128 && !name.Any(c => char.IsControl(c)
                || char.GetUnicodeCategory(c) is UnicodeCategory.Format or UnicodeCategory.PrivateUse or UnicodeCategory.Surrogate);
        } catch (EncoderFallbackException) { return false; }
    }
    // Existing contributed Name contains the host's game-text bytes. Restrict
    // actionable recruiter identity to canonical plain UTF-8, never SeString
    // control payloads. No second name/world field is added to the v1 upload.
    internal static string? RecruiterFromBytes(byte[] bytes) {
        if (bytes.Length > 128) return null;
        try { var name = StrictUtf8.GetString(bytes); return RecruiterValid(name) ? name : null; }
        catch (DecoderFallbackException) { return null; }
    }
    internal static bool TryCrossWorld(uint searchArea, out bool crossWorld) {
        crossWorld = false;
        // Installed API15 SearchAreaFlags: DataCenter=1, World=8. Never use
        // the listing indexer: its None=0 behavior matches every flag.
        var scope = searchArea & 9;
        if (searchArea > byte.MaxValue || scope is not (1 or 8)) return false;
        crossWorld = scope == 1;
        return true;
    }
    internal static bool Valid(PartyFinderLinkRequest r, DateTime now) {
        if (now.Kind != DateTimeKind.Utc || !Guid.TryParseExact(r.RequestId, "D", out var id) || id == Guid.Empty
            || string.IsNullOrEmpty(r.ClaimToken) || r.ClaimToken.Length is < 16 or > 500 || r.ClaimToken.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_'))
            || r.ListingId == 0 || !RecruiterValid(r.RecruiterName)) return false;
        if (string.IsNullOrEmpty(r.ListingKey)) return false;
        var parts = r.ListingKey.Split(':');
        if (parts.Length != 3 || !ushort.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var world) || world == 0
            || !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var restart)
            || !uint.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var listing) || listing != r.ListingId
            || r.ListingKey != FormattableString.Invariant($"{world}:{restart}:{listing}")) return false;
        if (r.ObservedAtUtc.Kind != DateTimeKind.Utc || r.ListingExpiresAtUtc.Kind != DateTimeKind.Utc || r.ExpiresAtUtc.Kind != DateTimeKind.Utc) return false;
        // Subtraction, not AddSeconds on untrusted dates, avoids overflow.
        return r.ObservedAtUtc >= now.AddMinutes(-5) && r.ObservedAtUtc <= now.AddSeconds(30)
            && r.ListingExpiresAtUtc > now && r.ListingExpiresAtUtc > r.ObservedAtUtc
            && r.ListingExpiresAtUtc - r.ObservedAtUtc <= TimeSpan.FromHours(1)
            && r.ExpiresAtUtc > now && r.ExpiresAtUtc - now <= TimeSpan.FromSeconds(MaximumLifetimeSeconds)
            && r.ExpiresAtUtc <= r.ListingExpiresAtUtc && r.ExpiresAtUtc - r.ObservedAtUtc <= TimeSpan.FromMinutes(5);
    }
    internal static PartyFinderLinkRequest? Parse(JsonElement root) {
        try {
            if (!root.GetProperty("ok").GetBoolean()) return null;
            var ack = root.GetProperty("nativeRequests");
            if (ack.GetProperty("contract").GetString() != Contract || ack.GetProperty("contractVersion").GetInt32() != 1
                || ack.GetProperty("acceptedClientProduct").GetString() != "GillionsGameSyncTest"
                || !ack.GetProperty("capabilities").EnumerateArray().Any(x => x.GetString() == Capability)) return null;
            var r = root.GetProperty("request");
            var fields = new HashSet<string>(["requestType", "requestId", "claimToken", "listingKey", "listingId",
                "recruiterName", "crossWorld", "listingObservedAt", "listingExpiresAt", "expiresAt"], StringComparer.Ordinal);
            foreach (var p in r.EnumerateObject()) if (!fields.Remove(p.Name)) return null;
            if (fields.Count != 0 || r.GetProperty("requestType").GetString() != RequestType) return null;
            return new(r.GetProperty("requestId").GetString() ?? "", r.GetProperty("claimToken").GetString() ?? "",
                r.GetProperty("listingKey").GetString() ?? "", r.GetProperty("listingId").GetUInt32(), r.GetProperty("recruiterName").GetString() ?? "",
                r.GetProperty("crossWorld").GetBoolean(), Date(r, "listingObservedAt"), Date(r, "listingExpiresAt"), Date(r, "expiresAt"));
        } catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException or FormatException or OverflowException) { return null; }
    }
    private static DateTime Date(JsonElement r, string name) {
        var value = r.GetProperty(name).GetString();
        if (value is null || !value.EndsWith('Z') || !DateTime.TryParseExact(value,
            ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"], CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date)) throw new FormatException();
        return date;
    }
}

internal sealed class PartyFinderLinkRequestProcessor {
    private readonly HashSet<string> attempted = new(StringComparer.Ordinal);
    private readonly Queue<string> order = new();
    private readonly object gate = new();
    internal async Task<string> ProcessAsync(PartyFinderLinkRequest? request, Func<DateTime> now,
        Func<bool> permitted, Func<PartyFinderLinkRequest, Task<bool>> consume,
        Func<PartyFinderLinkRequest, Task> present) {
        if (request is null || !PartyFinderLinkPolicy.Valid(request, now())) return "Invalid or expired";
        if (!permitted()) return "Disabled or stale session";
        lock (gate) {
            if (!attempted.Add(request.RequestId)) return "Already attempted";
            order.Enqueue(request.RequestId);
            while (order.Count > 128) attempted.Remove(order.Dequeue());
        }
        // Reserve before await as well as server consume-before-presentation.
        // Lost consume response sacrifices delivery, never retries a side effect.
        if (!await consume(request)) return "Consume rejected";
        if (!permitted() || !PartyFinderLinkPolicy.Valid(request, now())) return "Disabled or expired after consume";
        await present(request);
        return "Native chat link delivered; final in-game click required";
    }
}
#endif
