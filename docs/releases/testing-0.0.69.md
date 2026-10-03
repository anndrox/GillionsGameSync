# Testing 0.0.69 — Hunt cache collection correction

Testing-only successor to 0.0.68. Stable/server unchanged. Keep the existing URL:

`https://github.com/anndrox/GillionsGameSync/releases/download/v0.0.64-testing/GillionsGameSyncTesting.json`

0.0.68 subscribed only to `MobHunt` addon events. Primary reference implementations
use `Mobhunt` plus numbered expansion windows; owner live reports stayed at
Awaiting / 0.00 ms even after opening bills. This is a collector defect, not a
pairing problem. Static diagnosis is not proof of the owner's exact event trace.

The correction removes that interface dependency. With local retention enabled,
the existing framework loop attempts a bounded cache read every five seconds.
No per-frame native scan, new subscription, game/web request or automatic UI.
No initial logout/login or opening bills required when corroborated data is loaded.

Requires the same exact game/SDK pair as 0.0.68, loaded own PlayerState, no zoning,
and loaded Key Items with a matching positive bill item plus obtained cache flag.
Absent/partial/unsupported data preserves history, never declares no bills.
Bill-item presence corroborates the category, not current cached order freshness,
acceptance or cache ownership. Comparison with real bill details remains a test,
not a mandatory collection step. No Hunt upload exists.

Additive nullable `sourceEvidence` identifies new corroboration. Old retained
observations remain valid with null evidence, not fabricated freshness. Updated
[local draft/Site handoff](../contracts/personal-observations-v1.md) and
[invented example](../examples/hunt-bills-v1.json); market contract unchanged.

Defaults/retention: Hunt OFF for new configs, saved choice preserved; 16 character
partitions, 22 latest positive bill types each, 256 KiB. Opt-out stops reads/export
and preserves history. Semantic saves remain bounded; unchanged at most once per
minute. Submarines, Party Finder, Beastmaster, market consent and ordinary sync
are unchanged. Independent privacy applicability covers broadened cache admission.

## Owner retest

1. Update normally through the existing repository entry; confirm 0.0.69.0.
2. Enable `/gillionshunts` retention. With accepted bills already held, wait ten
   seconds WITHOUT opening bill windows; copy aggregate Hunt diagnostics.
3. Compare a bill's targets/counters afterwards. During normal play, progress or
   a newly accepted bill should refresh within approximately five seconds once
   the game cache and Key Items are coherent. Report mismatches; no freshness proof.
4. Disable retention: attempt count must stop increasing and history remains.
   Re-enable: first attempt is immediate on the next framework tick.
5. Optional normal character change: no prior character private export; unmatched
   or unloaded sources remain unavailable. No logout needed just to start collection.
6. Recheck PF/Beastmaster/ordinary sync and watch frame hitches. Existing submarine
   checks continue unchanged. Site integration still requires the bounded handoff.

Diagnostics now include game/SDK versions, attempts, last attempt UTC, stage/status
and read/save milliseconds. Send aggregates first, not private JSON/configuration,
credentials or raw memory. No live successor/game validation was available locally.
