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
    internal const string Setting="Share my character appearance with Gillions";
    internal const string Explanation="Shares this character's 26 appearance-setting bytes and the item ID, glamour item ID and both dye IDs for 12 equipped slots. Gillions stores only your latest look, privately, for your appearance page and a later 3D preview. It never changes the game. Turning this off removes the imported look when connected; use Remove imported appearance on Gillions for immediate removal while offline. Glamour Plates, visibility, visor and facewear are not included.";
    internal static readonly string[] Slots=["mainHand","offHand","head","body","hands","legs","feet","ears","neck","wrists","ring1","ring2"];
    // Amendment 1: GearsetItemIndex basis; physical check pending. No ring side claim.
    internal static readonly int[] Indices=[0,1,2,3,4,6,7,8,9,10,11,12];
    internal static readonly JsonSerializerOptions Json=new() { PropertyNamingPolicy=JsonNamingPolicy.CamelCase };
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
    internal void Clear(){Pending=null;accepted="";}
    internal void Observe(AppearanceSnapshot p){if(!AppearancePolicy.Valid(p))return;var hash=p.ContentHash;if(hash==accepted){Pending=null;return;}if(Pending?.Hash!=hash)Pending=new(p);}
    internal void Accepted(AppearancePreparation p){accepted=p.Hash;if(ReferenceEquals(Pending,p))Pending=null;}
}
