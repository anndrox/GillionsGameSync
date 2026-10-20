using System;
using System.Collections.Generic;
using System.Linq;

namespace GillionsGameSync;

// Cache definitions only after a successful, fully materialized sheet read.
// A missing source is retried on the next ordinary observation, never cached.
internal sealed class ObservedSheetRows<T> {
    private object? source;
    private T[]? rows;

    internal T[]? Get(object currentSource, Func<IEnumerable<T>?> load) {
        if (ReferenceEquals(source, currentSource)) return rows;
        var available = load();
        if (available is null) return null;
        var complete = available.ToArray();
        if (complete.Length == 0) return complete;
        source = currentSource;
        rows = complete;
        return complete;
    }
}

internal sealed class CollectibleSourceUnavailableException : Exception;

internal static class CollectibleSnapshotPolicy {
    // No partial payload is sent: older Sites cannot preserve an omitted subset.
    // Character unlock results are never cached, including across reconnects.
    internal static object? Capture(Func<ulong> readCharacter, Func<object> readPayload) {
        var character = readCharacter();
        if (character == 0) return null;
        try {
            var payload = readPayload();
            return readCharacter() == character ? payload : null;
        } catch (CollectibleSourceUnavailableException) {
            return null;
        }
    }
}
