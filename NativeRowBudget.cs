using System;
using System.Collections.Generic;

namespace GillionsGameSync;

internal sealed class NativeScanPendingException : Exception;
internal sealed class NativeScanChangedException : Exception;

// A scan retains copied values only. Native pointers and accessors stay in the
// current framework callback and are acquired again for the next slice.
internal sealed class NativeRowBudget {
    internal const int RowsPerTick = 256;
    private int remaining;
    private readonly Dictionary<object, object> scans = new();
    private readonly Dictionary<object, int> shapes = new();

    private sealed class Scan<T> {
        internal int Index;
        internal readonly List<T> Values = new();
        internal T[]? Completed;
    }

    internal void NextTick() => remaining = RowsPerTick;

    internal void RequireShape(object key, int count) {
        if (!shapes.TryAdd(key, count) && shapes[key] != count) throw new NativeScanChangedException();
    }

    internal T[] Read<T>(object key, int count, Func<int, (bool Include, T Value)> read) {
        RequireShape(key, count);
        if (!scans.TryGetValue(key, out var stored)) scans.Add(key, stored = new Scan<T>());
        var scan = (Scan<T>)stored;
        if (scan.Completed is not null) return scan.Completed;
        while (scan.Index < count && remaining > 0) {
            var row = read(scan.Index++);
            if (row.Include) scan.Values.Add(row.Value);
            remaining--;
        }
        if (scan.Index < count) throw new NativeScanPendingException();
        return scan.Completed = scan.Values.ToArray();
    }

    internal long[] Ids<T>(T[] rows, Func<T, bool> include, Func<T, long> id) =>
        Read(typeof(T), rows.Length, index => {
            var row = rows[index];
            return include(row) ? (true, id(row)) : (false, 0);
        });
}
