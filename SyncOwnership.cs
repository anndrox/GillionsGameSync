using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace GillionsGameSync;

public sealed record PairedSession(int SchemaVersion, string Origin, string DeviceId, string Generation, string CredentialFingerprint) {
    public static PairedSession Create(string origin, string deviceId, string token) {
        if (!SyncOrigin.TryNormalize(origin, out var normalized) || !Guid.TryParse(deviceId, out _) || !ValidToken(token))
            throw new InvalidOperationException("Gillions returned invalid pairing details.");
        return new(1, normalized, deviceId, Guid.NewGuid().ToString("N"), Fingerprint(token));
    }
    public bool IsValid(string deviceId, string token) => SchemaVersion == 1
        && SyncOrigin.TryNormalize(Origin, out var normalized) && normalized == Origin
        && Guid.TryParse(Generation, out _) && Guid.TryParse(DeviceId, out _)
        && DeviceId == deviceId && ValidToken(token) && CredentialFingerprint == Fingerprint(token);
    private static bool ValidToken(string? token) => token is { Length: >= 24 and <= 256 }
        && token.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
    private static string Fingerprint(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public static class SyncOrigin {
    public static bool TryNormalize(string? input, out string origin) {
        origin = "";
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
            || uri.AbsolutePath != "/" || string.IsNullOrEmpty(uri.Host)) return false;
        origin = uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        return true;
    }
}

public sealed class OwnedCharacterState {
    public string Generation { get; set; } = "";
    public string CharacterContentId { get; set; } = "";
    public string CharacterName { get; set; } = "";
    public string CharacterWorld { get; set; } = "";
    public string GilLedgerSessionId { get; set; } = Guid.NewGuid().ToString("N");
    public Dictionary<string, string> LastPayloadHashes { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> LastInventoryComponentHashes { get; set; } = new(StringComparer.Ordinal);
    public DateTime? LastSyncUtc { get; set; }
    public Dictionary<string, long> RetainerGilBalances { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> RetainerGilBaselinesNeedingRefresh { get; set; } = new(StringComparer.Ordinal);
    public RetainerVentureLocalState RetainerState { get; set; } = new();
    public List<GilLedgerEvent> PendingGilLedgerEvents { get; set; } = [];
    public List<GilLedgerEvent> PendingRetainerGilReceipts { get; set; } = [];
    public List<GilLedgerEvent> PendingRetainerGilDeposits { get; set; } = [];
    public List<PendingRetainerSale> PendingRetainerSales { get; set; } = [];
}

public static class SyncOwnershipPolicy {
    public static bool IsBoundSession(PairedSession? session, bool pairingRequired, string deviceId, string token) =>
        !pairingRequired && session is not null && session.IsValid(deviceId, token);
    public static OwnedCharacterState GetCharacter(IDictionary<string, OwnedCharacterState> states, PairedSession session, ulong contentId) {
        if (contentId == 0) throw new InvalidOperationException("The current character is not ready.");
        var character = contentId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var key = session.Generation + ":" + character;
        if (!states.TryGetValue(key, out var state)) {
            state = new OwnedCharacterState { Generation = session.Generation, CharacterContentId = character,
                RetainerState = new RetainerVentureLocalState { CharacterContentId = character } };
            states.Add(key, state);
        }
        if (state.Generation != session.Generation || state.CharacterContentId != character || state.RetainerState.CharacterContentId != character)
            throw new InvalidOperationException("Saved character ownership is invalid. Pair again to start a fresh connection.");
        return state;
    }
}

public enum SyncRequestMode { Manual, Automatic, Hydration, ItemLink, Pair }

// No generated record ToString: this object contains a credential, never diagnostics.
public sealed class SyncRequestPermit {
    internal SyncRequestPermit(long epoch, long modeEpoch, SyncRequestMode mode, ulong contentId, PairedSession? session,
        string origin, string token, CancellationToken cancellation) {
        Epoch = epoch; ModeEpoch = modeEpoch; Mode = mode; ContentId = contentId; Session = session;
        Origin = origin; Token = token; Cancellation = cancellation;
    }
    internal long Epoch { get; }
    internal long ModeEpoch { get; }
    public SyncRequestMode Mode { get; }
    public ulong ContentId { get; }
    public PairedSession? Session { get; }
    public string Origin { get; }
    public string Token { get; }
    public CancellationToken Cancellation { get; }
}

// This gate is exclusively owned by the framework thread. Network workers carry
// immutable permits, and return to that thread before dispatch or local effects.
public sealed class SyncRequestLifetime : IDisposable {
    private long epoch;
    private long automaticEpoch;
    private long itemEpoch;
    private CancellationTokenSource lifetime = new();
    private CancellationTokenSource automatic;
    private CancellationTokenSource item;
    private bool disposed;
    public SyncRequestLifetime() {
        automatic = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        item = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
    }
    public SyncRequestPermit Capture(SyncRequestMode mode, ulong contentId, PairedSession? session, string origin, string token) {
        ObjectDisposedException.ThrowIf(disposed, this);
        var source = mode == SyncRequestMode.Automatic ? automatic : mode == SyncRequestMode.ItemLink ? item : lifetime;
        return new(epoch, mode == SyncRequestMode.Automatic ? automaticEpoch : mode == SyncRequestMode.ItemLink ? itemEpoch : 0,
            mode, contentId, session, origin, token, source.Token);
    }
    public bool Accepts(SyncRequestPermit permit, ulong contentId, PairedSession? session, string token, bool automaticEnabled, bool itemLinksEnabled) =>
        !disposed && permit.Epoch == epoch && !permit.Cancellation.IsCancellationRequested
        && (permit.Mode == SyncRequestMode.Pair || permit.ContentId == contentId && contentId != 0
            && permit.Session == session && permit.Token == token)
        && (permit.Mode != SyncRequestMode.Automatic || automaticEnabled && permit.ModeEpoch == automaticEpoch)
        && (permit.Mode != SyncRequestMode.ItemLink || itemLinksEnabled && permit.ModeEpoch == itemEpoch);
    public void InvalidateAutomatic() {
        automatic.Cancel(); automatic.Dispose(); automaticEpoch++;
        automatic = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
    }
    public void InvalidateItemLinks() {
        item.Cancel(); item.Dispose(); itemEpoch++;
        item = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
    }
    public void Invalidate() {
        lifetime.Cancel(); automatic.Dispose(); item.Dispose(); lifetime.Dispose(); epoch++;
        lifetime = new();
        automatic = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        item = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
    }
    public void Dispose() {
        if (disposed) return;
        disposed = true; lifetime.Cancel(); automatic.Dispose(); item.Dispose(); lifetime.Dispose(); epoch++;
    }
}
