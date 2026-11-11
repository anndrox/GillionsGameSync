using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GillionsGameSync;

// Only consent metadata is persisted. No appearance bytes, payloads or history.
public sealed class AppearanceChoice {
    public bool Enabled { get; set; }
    public bool Pending { get; set; }
    public string Nonce { get; set; } = "";
    public string? ExpectedGeneration { get; set; }
    public long CharacterId { get; set; }
}
internal readonly record struct AppearanceDrawSlot(ulong Model, byte Stain0, byte Stain1);

internal sealed class AppearanceSlot {
    public uint ItemId { get; init; }
    public uint GlamourItemId { get; init; }
    public byte Stain0Id { get; init; }
    public byte Stain1Id { get; init; }
}
internal sealed class AppearanceSnapshot {
    public int SchemaVersion => 1;
    public string CollectorVersion { get; init; } = "";
    public string CollectorProduct => "GillionsGameSyncTest";
    public string GameVersion { get; init; } = "";
    public string ClientStructsVersion { get; init; } = "";
    public string ObservedAtUtc { get; init; } = "";
    public string State => "COMPLETE";
    public string CustomizationLayout => AppearancePolicy.Layout;
    public string CustomizationHex { get; init; } = "";
    public Dictionary<string,AppearanceSlot> Equipment { get; init; } = new();
    public string ContentHash => AppearancePolicy.Hash(this);
}
internal static class AppearancePolicy {
    internal const string Contract="character-appearance-v1", Resource="character_appearance", Capability="character_appearance_v1";
    internal const string Permission="server:game-sync:personal:character-appearance:v1", Layout="ffxiv-customize-26-v1";
    internal static readonly string[] Slots=["mainHand","offHand","head","body","hands","legs","feet","ears","neck","wrists","ring1","ring2"];
    internal static readonly JsonSerializerOptions Json=new() { PropertyNamingPolicy=JsonNamingPolicy.CamelCase };
    internal static AppearanceSlot FromDrawSlot(uint itemId, uint glamourId, AppearanceDrawSlot draw, bool matchesGlamour) =>
        new() {
            ItemId = itemId,
            GlamourItemId = matchesGlamour ? glamourId : 0,
            Stain0Id = draw.Stain0,
            Stain1Id = draw.Stain1,
        };

    internal static string Hash(AppearanceSnapshot p) {
        var lines=new List<string> { Layout,p.CustomizationHex };
        foreach(var key in Slots){var s=p.Equipment[key];lines.Add(string.Create(CultureInfo.InvariantCulture,$"{key}:{s.ItemId},{s.GlamourItemId},{s.Stain0Id},{s.Stain1Id}"));}
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n',lines))));
    }
    internal static bool Valid(AppearanceSnapshot p) => p.CustomizationHex.Length==52 && p.CustomizationHex.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f')
        && p.Equipment.Keys.Order().SequenceEqual(Slots.Order()) && p.Equipment.Values.All(s=>s.ItemId!=0 || s.GlamourItemId==0&&s.Stain0Id==0&&s.Stain1Id==0)
        && Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(p,Json))<=16384;
}
internal sealed class AppearancePreparation {
    internal AppearancePreparation(AppearanceSnapshot p) { Hash=p.ContentHash; Payload=JsonSerializer.Serialize(p,AppearancePolicy.Json); ObservedAt=DateTime.Parse(p.ObservedAtUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind); }
    internal string Nonce { get; }=Guid.NewGuid().ToString("N");
    internal string Hash { get; }
    internal string Payload { get; }
    internal DateTime ObservedAt { get; }
}
internal sealed class AppearanceQueue {
    internal AppearancePreparation? Pending { get; private set; }
    private string accepted="";
    internal void DiscardPending() => Pending = null;
    internal void Clear(){Pending=null;accepted="";}
    internal void Observe(AppearanceSnapshot p){if(!AppearancePolicy.Valid(p))return;var hash=p.ContentHash;if(hash==accepted){Pending=null;return;}if(Pending?.Hash!=hash)Pending=new(p);}
    internal void Accepted(AppearancePreparation p){accepted=p.Hash;if(ReferenceEquals(Pending,p))Pending=null;}
}

internal sealed record AppearanceGrant(long CharacterId, string Generation) {
    internal static AppearanceGrant? Read(JsonElement root, out long? disabledCharacterId) {
        disabledCharacterId = null;
        try {
            if (!root.TryGetProperty("characterAppearance", out var descriptor)
                || descriptor.GetProperty("contract").GetString() != AppearancePolicy.Contract
                || descriptor.GetProperty("resourceType").GetString() != AppearancePolicy.Resource
                || descriptor.GetProperty("capability").GetString() != AppearancePolicy.Capability
                || descriptor.GetProperty("permission").GetString() != AppearancePolicy.Permission
                || descriptor.GetProperty("maxPayloadBytes").GetInt32() != 16384
                || descriptor.GetProperty("maxEnvelopeBytes").GetInt32() != 20480) return null;
            var character = descriptor.GetProperty("characterId").GetInt64();
            if (character <= 0) return null;
            if (!descriptor.GetProperty("enabled").GetBoolean()) {
                disabledCharacterId = character;
                return null;
            }
            var generation = descriptor.GetProperty("generation").GetString();
            return Guid.TryParseExact(generation, "D", out var id) && id != Guid.Empty
                ? new(character, generation!) : null;
        } catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException) {
            return null;
        }
    }
}
