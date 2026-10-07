#if GILLIONS_TEST_BUILD || GILLIONS_FATE_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GillionsGameSync;

// Revision-2 Site contract, wire schema 1. Only copied primitives enter this
// model. No player identity, native references, static text or persistent state.
internal sealed record FateSource(string Family, string CollectorVersion, string GameVersion,
    int DalamudApiLevel, string DalamudVersion, string DalamudRevision,
    string ClientStructsVersion, string ClientStructsRevision, string LuminaVersion,
    string ExcelVersion, string ReferenceGameVersion);
internal sealed record FateInstance(string Kind, uint Number);
internal sealed record FateStateValue(string Kind, byte Raw);
internal sealed record FatePosition(float X, float Y, float Z);
internal sealed record FateTiming(int StartTimeEpoch, short DurationSeconds);
internal sealed record FateObservation(ushort FateId, uint WorldId, uint TerritoryId, FateInstance Instance,
    FateStateValue State, DateTime ObservedAt, byte? ProgressPercent = null, bool? Bonus = null,
    byte? Level = null, byte? MaxLevel = null, FatePosition? PositionWorld = null,
    float? RadiusWorld = null, FateTiming? Timing = null);
internal sealed record FateBatch(int SchemaVersion, string CollectorSchema, string Coverage,
    FateSource Source, Guid BatchId, FateObservation[] Observations);
internal sealed record FateContext(ulong LocalCharacter, uint World, uint Territory, uint Instance);
internal sealed class FatePrepared {
    private readonly byte[] body;
    internal long Epoch { get; }
    internal Guid BatchId { get; }
    internal DateTime OldestObservation { get; }
    internal ReadOnlyMemory<byte> Body => body;
    internal byte[] CopyBody() => (byte[])body.Clone();
    internal FatePrepared(long epoch, Guid batchId, byte[] bytes, DateTime oldestObservation) {
        Epoch=epoch; BatchId=batchId; body=(byte[])bytes.Clone(); OldestObservation=oldestObservation;
    }
}

internal static class FatePolicy {
    internal const string Capability = "fate-live-observations-v1";
    internal const string Endpoint = "https://test.gillions.app/api/game-sync/fates/contribute";
    internal const string AccountPolicy = "fate_public_observations";
    internal const int PolicyRevision = 1, MaximumRows = 64, MaximumBytes = 128 * 1024;
    internal const int ReadSeconds = 5, RenewSeconds = 10, MaximumAgeSeconds = 30;
    internal static readonly JsonSerializerOptions Json = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };
    internal static bool Compatible(FateSource? s) => s is not null
        && s.Family == "dalamud-fate-table" && Version.TryParse(s.CollectorVersion, out _)
        && s.GameVersion == "2026.09.15.0000.0000" && s.ReferenceGameVersion == s.GameVersion
        && s.DalamudApiLevel == 15 && s.DalamudVersion == "15.0.3.6"
        && s.DalamudRevision == "b666d821a47306fb447c60155b5d99377f91a5ee"
        && s.ClientStructsVersion == "7.56.2.9136"
        && s.ClientStructsRevision == "313161e448e335adddd928f8a0212b2c328b5659"
        && s.LuminaVersion == "7.7.0" && s.ExcelVersion == "7.5.1";
    internal static FateStateValue? State(byte raw) => raw switch {
        3 => new("preparing",3), 4 => new("running",4), 5 => new("ending",5),
        7 => new("ended",7), 8 => new("failed",8), _ => null
    };
    internal static bool Utc(DateTime t) => t.Kind == DateTimeKind.Utc && t > DateTime.UnixEpoch;
    internal static bool Fresh(DateTime observed, DateTime now) => Utc(observed) && Utc(now)
        && observed <= now.AddSeconds(5) && observed >= now.AddSeconds(-MaximumAgeSeconds);
    internal static FateTiming? Timing(int start, short duration, DateTime now) {
        if (!Utc(now) || start <= 0 || duration <= 0) return null;
        long current = new DateTimeOffset(now).ToUnixTimeSeconds();
        // A preparation may have no timer. Never create one, nor interpret an
        // expired estimate as terminal state. Native Int16 bounds duration.
        return start <= current + 5 && start >= current - 32767 ? new(start,duration) : null;
    }
    internal static bool Valid(FateObservation? r, DateTime now) => r is not null
        && r.FateId != 0 && r.WorldId != 0 && r.TerritoryId != 0 && Fresh(r.ObservedAt,now)
        && r.Instance is not null && (r.Instance.Kind == "public_instance" && r.Instance.Number > 0
            || r.Instance.Kind == "noninstanced" && r.Instance.Number == 0)
        && r.State is not null && State(r.State.Raw) == r.State
        && (r.ProgressPercent is null or <= 100)
        && (r.Level is null && r.MaxLevel is null || r.Level is > 0 && r.MaxLevel >= r.Level
            && !(r.Level == 1 && r.MaxLevel == 255))
        && (r.PositionWorld is null || float.IsFinite(r.PositionWorld.X)
            && float.IsFinite(r.PositionWorld.Y) && float.IsFinite(r.PositionWorld.Z))
        && (r.RadiusWorld is null || float.IsFinite(r.RadiusWorld.Value) && r.RadiusWorld > 0)
        && (r.Timing is null || Timing(r.Timing.StartTimeEpoch,r.Timing.DurationSeconds,now) == r.Timing);
    internal static string? Occurrence(FateObservation r, string referenceEpoch) => r.Timing is null ? null
        : string.Join(":",referenceEpoch,r.WorldId,r.TerritoryId,r.Instance.Kind,r.Instance.Number,r.FateId,r.Timing.StartTimeEpoch);
    internal static bool Same(FateObservation a, FateObservation b) => a with { ObservedAt = b.ObservedAt } == b;
    internal static FatePrepared? Prepare(long epoch, FateSource source, FateObservation[] rows, DateTime now) {
        if (!Compatible(source) || rows.Length is < 1 or > MaximumRows || rows.Any(r => !Valid(r,now))) return null;
        var id = Guid.NewGuid();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new FateBatch(1,Capability,"positive_only",source,id,rows),Json);
        return bytes.Length <= MaximumBytes ? new(epoch,id,bytes,rows.Min(r => r.ObservedAt)) : null;
    }
}

// Single replaceable RAM batch, never an offline history. Context changes also
// clear occurrence support so a terminal cannot inherit an earlier session.
internal sealed class FateEpochState {
    internal long Epoch { get; private set; }
    internal FatePrepared? Prepared { get; private set; }
    internal FateObservation[] Current { get; private set; } = [];
    internal bool LastChanged { get; private set; }
    private FateContext? context;
    private DateTime lastPrepared = DateTime.MinValue;
    private readonly Dictionary<string, DateTime> timedOccurrences = [];
    internal void Invalidate() {
        Epoch++; Prepared=null; Current=[]; context=null; lastPrepared=DateTime.MinValue;
        timedOccurrences.Clear(); LastChanged=false;
    }
    internal bool Observe(FateContext before, FateContext after, FateSource source, FateObservation[] rows, DateTime now) {
        if (before != after || before.LocalCharacter == 0 || !FatePolicy.Compatible(source)) { Invalidate(); return false; }
        // The framework reader already invalidates/settles a new epoch before
        // the first observation. Binding its first context must not invalidate
        // that same epoch again (which would force alternating settle/read
        // passes and erase terminal support on every subsequent observation).
        if (context != before) { if (context is not null) Invalidate(); context=before; }
        foreach (var key in timedOccurrences.Where(p => now-p.Value > TimeSpan.FromSeconds(120)).Select(p=>p.Key).ToArray()) timedOccurrences.Remove(key);
        var accepted = new List<FateObservation>();
        foreach (var row in rows.Take(FatePolicy.MaximumRows)) {
            if (!FatePolicy.Valid(row,now) || row.WorldId != before.World || row.TerritoryId != before.Territory
                || row.Instance.Number != before.Instance) continue;
            string? key = FatePolicy.Occurrence(row,source.ReferenceGameVersion);
            if (row.State.Raw is 7 or 8) {
                if (key is null || !timedOccurrences.ContainsKey(key)) continue;
            } else if (key is not null) {
                // Only bounded support for terminal admission; not retained
                // history or a permanent per-FATE maximum/terminal accumulator.
                if (timedOccurrences.Count < 128 || timedOccurrences.ContainsKey(key)) timedOccurrences[key]=now;
            }
            if (!accepted.Any(r => r.FateId == row.FateId && r.Timing?.StartTimeEpoch == row.Timing?.StartTimeEpoch)) accepted.Add(row);
        }
        var next = accepted.OrderBy(r=>r.FateId).ThenBy(r=>r.Timing?.StartTimeEpoch).ToArray();
        LastChanged = Current.Length != next.Length || !Current.Zip(next).All(p=>FatePolicy.Same(p.First,p.Second));
        Current=next;
        // A disappearance cancels unsent evidence, never prepares a tombstone.
        if (next.Length == 0) { Prepared=null; return true; }
        if (LastChanged || now-lastPrepared >= TimeSpan.FromSeconds(FatePolicy.RenewSeconds)) {
            Prepared=FatePolicy.Prepare(Epoch,source,next,now); lastPrepared=now;
        }
        return true;
    }
    internal void Expire(DateTime now) {
        if (Prepared is not null && !FatePolicy.Fresh(Prepared.OldestObservation,now)) Prepared=null;
    }
    internal void CancelUnsent() => Prepared=null;
    // Policy loss clears observations and terminal support without changing the
    // context epoch that discovery is authorizing. No pre-consent replay.
    internal void ForgetObservations() { Prepared=null; Current=[]; timedOccurrences.Clear(); lastPrepared=DateTime.MinValue; LastChanged=false; }
}

internal sealed record FateCost(double ReadMilliseconds, long ReadBytes, double PrepareMilliseconds,
    long PrepareBytes, int TableRows, int AcceptedRows, bool Changed, string Outcome);
internal sealed class FateMeasurements {
    private readonly Queue<FateCost> costs=[];
    internal int Count => costs.Count;
    internal void Add(FateCost cost) { if (costs.Count == 240) costs.Dequeue(); costs.Enqueue(cost); }
    internal FateCost[] Snapshot() => costs.ToArray();
    internal static string Summary(FateCost[] rows) {
        if (rows.Length == 0) return "No measured samples.";
        string Stats(Func<FateCost,double> selector) {
            var values=rows.Select(selector).Order().ToArray();
            double median=values.Length%2==0 ? (values[values.Length/2-1]+values[values.Length/2])/2 : values[values.Length/2];
            return $"median {median:F3}; p95 {values[(int)Math.Ceiling(values.Length*.95)-1]:F3}; max {values[^1]:F3}";
        }
        return $"Samples {rows.Length}/240; read ms: {Stats(r=>r.ReadMilliseconds)}; prepare ms: {Stats(r=>r.PrepareMilliseconds)}; read bytes: {Stats(r=>r.ReadBytes)}; prepare bytes: {Stats(r=>r.PrepareBytes)}; table range {rows.Min(r=>r.TableRows)}..{rows.Max(r=>r.TableRows)}; changed {rows.Count(r=>r.Changed)}, unchanged {rows.Count(r=>!r.Changed)}. Read/copy and preparation stages only; current-thread allocation counters, not whole-process/frame measurements.";
    }
}
#endif
