# Testing 0.0.84 Hunt coverage refresh margin

Status: source candidate; exact-package reviews, corrected live intervals and
publication pending. Published Testing83 remains unchanged.

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

The6s target reserves9s of the15s contract budget for normal source age and
scheduling/network jitter. Actual arrival margin still requires corrected live
measurement. Network failure, unavailable character, OFF or backoff may expire
coverage honestly; Site's updating state remains the safe result.

## Validation and diagnostics

Managed fixtures exercise repeated jittered cycles, monotonic/UTC disagreement,
newer-source admission, unchanged-observation deduplication, permission OFF,
single-flight and retry exclusion, immutable nonce/receipts and inherited source/
session/final-completion behavior. They do not prove live transport latency.
Exact-DLL fixtures bind the scheduling behavior to the final package.

Existing opt-in diagnostics and40-entry capacity gain numeric local durations:
observation/preparation/worker to dispatch, dispatch to headers, headers to receipt,
receipt to framework and response disposition. No identity, nonce, credential,
payload, item, order or location is added to these timing lines. No new durable
telemetry store exists. Corrected real shared-TEST interval distributions must be
recorded before publication.

Stable/main/Site/schema/production/Market architecture/Wardrobe remain untouched.
Preserve82 command responsiveness,80 final completion, V1/V2 maps, PF/item links,
Market, Beastmaster, Submarine, Dashboard facts and ordinary sync.
