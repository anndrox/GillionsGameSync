#if GILLIONS_TEST_BUILD || GILLIONS_PUBLIC_BUILD || GILLIONS_HUNT_MAP_TESTS
using System;

namespace GillionsGameSync;

// One reservation per command lane. No game reads, persisted mode, permission,
// or replay state. Focus can shorten routine waits, never a failure backoff.
internal sealed class WebsiteCommandPollClock {
    private DateTime nextUtc;
    private bool retry;
    internal bool TryBegin(DateTime now, bool focused) {
        if (focused && !retry && nextUtc > now.AddSeconds(1)) nextUtc = now.AddSeconds(1);
        if (now < nextUtc) return false;
        retry = false;
        nextUtc = now.AddSeconds(focused ? 1 : 5);
        return true;
    }
    internal void Backoff(DateTime now, TimeSpan delay) { nextUtc = now.Add(delay); retry = true; }
    internal void Reset() { nextUtc = default; retry = false; }
}
#endif
