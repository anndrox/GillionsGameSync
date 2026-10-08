# Testing 0.0.84 Hunt coverage refresh margin

Status: published and anonymously verified on October5,2026. The unchanged rolling
Testing URL resolves0.0.84.0; all32 prior releases' immutable assets, including83,
remain unchanged. The exact reviewed package was built once, tested live and
published without rebuilding.

This bounded successor corrects private Hunt coverage dispatch scheduling. The
15-second freshness contract, three-second Hunt reads, final-counter admission,
historical counters, permissions and shared private single-flight lane remain
unchanged. Site's accepted UNKNOWN/updating routing behavior is preserved.

## Measured baseline and cause

The first read-only shared-TEST trace captured39 receipt intervals: median10.157s,
worst10.468s. Six successive observed-snapshot/replacement pairs had lease margins
0.720,-0.368,-1.478,0.462,-0.594,-1.690 seconds; four arrived after the prior
15-second observation lease. These are DB transaction timestamps, not precise
HTTP arrival instants. Only receipt/observation facts are available for that
published binary; preparation and dispatch stages cannot be reconstructed exactly.

The source waits6s from preparation, tested only when the5s routine personal tick
is due. After an ACK, the immediate drain tests too early, then the5s check also
tests too early; the second tick sends at about10s. Source tests reproduce that
deadline quantization. Read phase, framework/network delay and source/server clock
difference can consume the remaining margin. A later baseline trace showed an
abrupt reduction in observation-to-receipt offset; its exact cause is unknown.
No timestamp correction or clock trust expansion is introduced here.

## Scheduling correction

Renewal becomes due6s from the acknowledged assertion's original observation,
with both UTC and monotonic elapsed-time checks. Newer coverage already available
in RAM can bypass only the routine5s tick. It cannot bypass independent Hunt
consent, enrollment/capability, session/character ownership, single-flight, retry
backoff, source completeness or dispatch freshness. An ACKed unchanged observation
does not acquire another nonce merely because preparation aged out.

Normal Hunt capture remains every3s, with the existing manual1s floor. Command
polling, travel reads, Submarine/Dashboard collection and ordinary sync are not
accelerated. This is a dispatch deadline, not a new mode or source read.

The6s observation deadline nominally leaves9s of the15s budget for scheduling and
network jitter. The real connected run below retained at least8.361s. This is a
measured healthy margin, not a guarantee during network failure, unavailable
character, OFF or backoff; those conditions may expire coverage honestly. Site's
updating state remains the safe result.

## Real TEST results

The owner loaded the reviewed84DLL. A narrow Dalamud load record confirms its
exact prepared path; TEST reported one current84receiver. Two read-only60s
windows captured34 coverage snapshots and32 within-window replacement pairs.

| Metric | Published83 baseline | Corrected84 |
| --- | --- | --- |
| Receipt gaps measured | 39 | 32 |
| Median receipt gap | 10.157s | 3.055s |
| Worst healthy receipt gap | 10.468s | 6.082s |
| Minimum watched lease margin | -1.690s | 8.361s |
| Watched lease misses | 4of6 pairs | 0of32 pairs |

The first corrected window had median3.061s/worst6.082s, minimum margin8.361s;
the second had median3.055s/worst6.029s, minimum margin8.668s. Shorter3s receipt
gaps are expected when the previous payload's original observation was already
about3s old at dispatch; renewal still targets6s from that original observation.

All48 bounded native timing records were acknowledged. Numeric stages were:

| Stage | Median | Worst |
| --- | --- | --- |
| Original observation to dispatch | 3046.63ms | 3080.34ms |
| Preparation to dispatch | 58.525ms | 70.70ms |
| Worker start to dispatch | 58.21ms | 60.18ms |
| Dispatch to HTTP headers | 24.72ms | 53.91ms |
| Headers to parsed receipt | 0.04ms | 5.55ms |
| Receipt to framework commit | 34.03ms | 37.98ms |

Receipt timestamps are database transaction times, not precise HTTP arrival.
Observation timestamps are Native UTC; elapsed Native stages use monotonic time.
Server/local clock calibration remains bounded by SSH/Docker round-trip time.
Only current live samples enter corrected metrics: historical receipt queries
join a mutable current device version and cannot identify each older sender's
version. No offset adjustment or timestamp trust change is made. The cause of
the baseline offset change remains UNKNOWN. Loaded-path/version/receipts are live
proof; in-memory assembly bytes were not separately inspected.

## Validation and diagnostics

Managed fixtures exercise repeated jittered cycles, monotonic/UTC disagreement,
newer-source admission, unchanged-observation deduplication, permission OFF,
single-flight and retry exclusion, immutable nonce/receipts and inherited source/
session/final-completion behavior. They do not prove live transport latency.
Exact-DLL fixtures bind the scheduling behavior to the final package.

Testing diagnostics were already always recorded through the existing40-entry
buffer and platform logs; Stable uses its manual diagnostic window. The added
Testing timings contain only numeric local durations:
observation/preparation/worker to dispatch, dispatch to headers, headers to receipt,
receipt to framework and response disposition. No identity, nonce, credential,
payload, item, order or location is added to these timing lines. No new durable
telemetry store exists. Security/Privacy and Compatibility accepted the unchanged
exact package and closed the runtime condition after the two real windows.

The maintained suite passed7808 counted groups:799 Hunt-map,542 Submarine,
1516 personal,1792 Dashboard,2551 travel,224 Market,116 PF and268 orchestration;
additional ordinary/Beastmaster/source/SDK/manifest/performance checks passed.
Stable/Testing validation builds and exact84packaging had zero warnings/errors;
12 actual configuration fixtures per product and exact packaged-DLL checks passed.
Post-publication verification uses separate validation outputs, not an84rebuild.
OFF/session/network/backoff behavior is fixture-validated, not live fault-injected.

## Published identities

Artifact source: `0a2c50605135812464768842690af03ae575d94b`.
Tree: `ac02c0fb3b84972d1918e678d0c686c20f0b8b27`.
Tag: `v0.0.84-testing`.

- ZIP SHA256: `1726e4ec8712dcbdda678ae085e963185a49a568f8edbcd82042d52be2eac552`
- DLL SHA256: `e5c938438bba61a8d0457995a5a227c6ff70287f7a8e929388b278deb079525d`
- Manifest SHA256: `061fb4795991fcf3cce3fe8ddffdfba8beef80c5368a22784f9f049dacbc8eb7`

See [publication record](../../data/releases/testing-0.0.84.json). Anonymous
immutable and rolling manifest/ZIP/contained-DLL bytes match these hashes, with
all4 download links pinned to84. Historical64 release-note suffix text is preserved.
Disable the diagnostic dev-plugin entry before enabling installed Testing84;
never run both. No configuration reset, re-pairing or extra gameplay is required.

Stable/main/Site/schema/production/Market architecture/Wardrobe remain untouched.
Site remains `ec70593583a88ff8d266d241c42bc7dbf08f3c6f`, schema0025.
Preserve82 command responsiveness,80 final completion, V1/V2 maps, PF/item links,
Market, Beastmaster, Submarine, Dashboard facts and ordinary sync.
