using System;
using System.Collections.Generic;

namespace GillionsGameSync;

// The retainer whose selling list (RetainerMarket) was loaded and read in one capture: exactly one stably active retainer's
// read, else none. Independent of whether the listings changed; the listing cache and the other inventory fields are unaffected.
// Managed only (no native read here), so it sits outside every native fingerprint set.
internal static class RetainerListingReadPolicy {
    internal static string[] ReadRetainerIds(bool observed, IReadOnlyList<string> retainerIds) =>
        observed && retainerIds.Count == 1 && !string.IsNullOrWhiteSpace(retainerIds[0]) ? [retainerIds[0]] : [];
}
