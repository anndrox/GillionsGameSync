using System;
using System.Numerics;

namespace GillionsGameSync;

internal sealed record HuntsFateRow(ushort Id, string Name, byte? Level, byte? Progress,
    string State, long? EndsAtTick, Vector3? Position) {
    internal static long? EndsAt(FateTiming? timing, DateTime now, long sampledAt) {
        if (timing is null) return null;
        long remaining = (timing.StartTimeEpoch + (long)timing.DurationSeconds) * 1000
            - new DateTimeOffset(now).ToUnixTimeMilliseconds();
        return sampledAt + Math.Max(0, remaining);
    }

    internal string TimeLeft(long tick) {
        if (EndsAtTick is not { } end) return "Time unknown";
        long seconds = Math.Max(0, end - tick + 999) / 1000;
        return $"{seconds / 60:00}:{seconds % 60:00} left";
    }

    internal static bool ValidPosition(Vector3 position) => float.IsFinite(position.Y)
        && Math.Abs(position.X) <= 4096 && Math.Abs(position.Z) <= 4096;
}

internal sealed record HuntsFates(FateContext? Context, uint Map, long ExpiresAt,
    bool Complete, HuntsFateRow[] Rows) {
    internal static readonly HuntsFates Unavailable = new(null, 0, 0, false, []);
    internal bool IsFresh(long tick) => Context is not null && tick < ExpiresAt;
}
