using System;
using System.Collections.Generic;
using System.Reflection;

namespace GillionsGameSync;

internal sealed class BackgroundObservation(Action<string, Exception> warn) {
    private readonly HashSet<(string Component, Type Error, MethodBase? Source)> reported = [];
    private long? lastProgress;

    internal bool RecoverIfStalled(long clock, bool paired) {
        if (!paired) {
            lastProgress = null;
            return false;
        }
        lastProgress ??= clock;
        if (clock - lastProgress < 30_000) return false;
        // If recovery itself fails, give it another full interval before retrying.
        lastProgress = clock;
        return true;
    }

    internal void Completed(long clock) {
        if (lastProgress.HasValue) lastProgress = clock;
    }

    internal bool Run<T>(string component, T state, DateTime now, Action<T, DateTime> tick) {
        try {
            tick(state, now);
            return true;
        } catch (Exception error) {
            Report(component, error);
            return false;
        }
    }

    internal void Report(string component, Exception error) {
        if (reported.Add((component, error.GetType(), error.TargetSite))) warn(component, error);
    }
}
