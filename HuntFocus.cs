#if GILLIONS_TEST_BUILD || GILLIONS_HUNT_MAP_TESTS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace GillionsGameSync;

// Ephemeral priority only. Never configuration, consent, history or a command.
internal sealed class HuntFocusState {
    internal const string Contract = "active_hunt_focus_v1";
    internal const string Header = "X-Gillions-Hunt-Focus";
    internal bool Supported { get; private set; }
    private DateTime expiresAtUtc;
    private long observed;
    private TimeSpan remaining;
    internal void Clear() { Supported = false; expiresAtUtc = default; remaining = default; }
    internal bool Active(DateTime now, bool separatelyPermitted) => separatelyPermitted && Supported
        && now.Kind == DateTimeKind.Utc && expiresAtUtc > now && Stopwatch.GetElapsedTime(observed) < remaining;
    internal bool Apply(string json, DateTime now, bool separatelyPermitted, DateTime? localNow = null) {
        Clear();
        var local = localNow ?? now;
        if (!separatelyPermitted || now.Kind != DateTimeKind.Utc || local.Kind != DateTimeKind.Utc || Encoding.UTF8.GetByteCount(json) > 131072) return false;
        try {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in root.EnumerateObject()) if (!keys.Add(field.Name)) return false;
            if (!keys.Contains("huntFocus") || !root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True) return false;
            var focus = root.GetProperty("huntFocus");
            if (!HuntMapPolicy.Exact(focus, "contract", "focused", "expiresAt") || focus.GetProperty("contract").GetString() != Contract) return false;
            var focused = focus.GetProperty("focused").GetBoolean();
            var expiry = focus.GetProperty("expiresAt");
            if (!focused) { if (expiry.ValueKind != JsonValueKind.Null) return false; Supported = true; return true; }
            if (!HuntMapPolicy.Utc(expiry.GetString(), out var time) || time <= now || time - now > TimeSpan.FromSeconds(30)) return false;
            remaining = time-now; observed = Stopwatch.GetTimestamp();
            expiresAtUtc = local.Add(remaining); Supported = true; return true;
        } catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or OverflowException) { return false; }
    }
}

// Negotiation is RAM-only and session-scoped. V2 never reinterprets V1 claims.
internal sealed class HuntMapNegotiation {
    private DateTime retryV2AtUtc;
    internal string Capability(DateTime now) => now >= retryV2AtUtc ? HuntMapPolicy.CapabilityV2 : HuntMapPolicy.Capability;
    internal void Reset() => retryV2AtUtc = default;
    internal void Unsupported(DateTime now) => retryV2AtUtc = now.AddMinutes(5);
    internal bool LegacyEmpty(string json, DateTime now) => HuntMapPolicy.TryPoll(json, now, HuntMapPolicy.Capability, out var r) && r is null;
}
#endif
