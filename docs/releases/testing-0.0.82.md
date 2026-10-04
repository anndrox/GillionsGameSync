# Testing0.0.82 command-latency correction — PUBLISHED

Owner live acceptance for published0.0.81 is FAILED: command responsiveness is
a BLOCKER. Owner-observed cases: 2nd Cohort signifer7–10s, Tryptix Stumblemox7–12s,
Upland Mylodon>30s. Functional success does not satisfy latency acceptance.

Starting branch HEAD2390aac7f3412852b8d5516d9ec2a6bb62ed3684;
published0.0.81 source49a7c7ae77b54fc753787f750a52f0cfbc576253.
Shared TEST contract checked at70bc0d7a45ba9e54d44647df72b47f735fe9042a/schema0025.
Later rediscovery found deployed36327f9cf624ca44d535a30e71fade2853762e74/schema0025:
reference coverage changed, command/focus modules/contracts did not. Running
V1/V2/Next/focus/consume fixtures were rerun read-only against that successor.

## Bounded source preparation

The old outer deadline and later inner Hunt reservation can skip a whole outer
turn. Source-bound deterministic reproduction gives6s for nominal3s and10s for
nominal5s. This is NOT real-client measurement or proof of the>30s cause.

Independent Hunt/item-PF command lanes now reserve one deadline each. A valid
permission-gated focus lease selects1s command polling, otherwise5s. No new mode,
consent, endpoint, authentication or gameplay collection cadence. Each lane has
one in-flight poll and independent unchanged failure backoff. Faster focus cannot
bypass backoff. An unrelated slow item/PF request no longer owns the Hunt flight.

Numeric local timing records poll dispatch, claim receipt, consume callback and
actual consume HTTP dispatch/headers/validated acknowledgement, and map API
invocation. Consumption is measured before map presentation, never afterwards.
Failure summaries report elapsed/HTTP-or-exception/eligibility
and backoff, not request payloads or credentials. Existing40-entry diagnostic
capacity is unchanged; no new telemetry store or automatic upload.
Existing Dalamud platform log retention is unchanged; an eight-character
request correlation prefix is disclosed in the Native contract (not a token).

The measured issuer/PC clock discrepancy exposes a second reproduced defect:
old local-time validation can reject fresh90s claims/30s focus leases, entering
the existing30s malformed-command backoff. This is demonstrated against the
current clock environment, not correlated proof of the owner's original long
tail. Request-scoped exact trusted HTTPS Date plus conservative precision/transit
and monotonic elapsed now evaluates issuer expiry without extending TTL caps.
Raw expiry/consume fields are unchanged; all parser, consume deadline and map
checks use that reference. Missing Date preserves prior strict local validation;
malformed/unbounded clock fails closed. The same exact TEST response reference
covers PF/item expiry; Stable/other origins remain unchanged. Focus failures do
not break ordinary sync and consent changes do not reset unrelated lane backoff.
Review also corrected Testing item pre-consume to use the same request clock as
admission/presentation; near-expiry issuer fixtures cover PC-ahead false rejection.
Stable behavior is unchanged.

Consumption remains BEFORE presentation. Request identity/token/revision,
consume-once, replay reservation, V1 fallback and Site-owned automatic watermark
are unchanged. The live-proven0.0.80 final-completion correction is preserved.
Hunt reads3s and travel reads/sends15s remain unchanged.

## Actual partial baseline evidence

Four agent-triggered intentional Show commands for the unchanged current target
on published0.0.81 used the authenticated existing TEST browser session. No
credentials were extracted, permissions changed, or gameplay performed.
Intentional-action dispatch to existing Native `presentation=shown` diagnostic:
3.004s,2.747s,2.601s,2.377s; median2.674s, worst3.004s (n=4; not pixel-visibility
proof, includes automation-to-click dispatch overhead).
Matching queue/revision/candidate records gave queue-to-claim
2.70525s/2.45739s/2.36723s/2.20165s (median2.41231s), and claim-to-consume
17.21ms/20.88ms/38.66ms/20.58ms (median20.73ms). These are PostgreSQL transaction-start stamps,
not exact HTTP arrival/commit timings. Browser automation return windows were
248ms/330ms/204ms/466ms, not browser network traces. No duplicate presentation
was observed. This controlled subset does not override the owner-reported
7–12s/>30s failures or prove their root cause.

A captured recent Upland row gave queue-to-claim1.77325s and claim-to-consume
63.57ms. This was not correlated by command ID with the owner's long-tail click;
it must not be substituted for the>30s observation. Its row subsequently expired.
Five clock calibrations bounded the TEST server ahead of this PC by roughly
1.77–2.17s. Cross-host subtraction without calibration is invalid.

The original owner commands' full browser/poll/claim stage timings were not
recorded. Native logs show some OperationCanceledException failures with the
existing30s retry path, but do NOT prove which caused the long-tail observation.
At preparation, controlled package timing and repeated shared-TEST measurements
remained required. The measured run below supersedes that preparation hold;
exact browser POST tracing and original long-tail attribution remain unavailable.
Do not relabel the original owner verdict or infer unmeasured stages.

## Preparation hold (historical; superseded below)

Maintained verification passes:799 Hunt-map,542 Submarine,454 personal-state,
1792 Dashboard,2551 travel,224 Market,116 PF and268 orchestration named checks
(6746 total in those counted groups), plus ordinary item-link/Beastmaster/source/
manifest/performance checks. Stable and Testing builds have zero warnings/errors;
12 actual Dalamud configuration fixtures pass for each. Packaged checks include
16 V2/focus,9 command-clock,3 issuer/focus,2 item consume clock,6 issuer-caller and
5 running-Site wire cross-checks. These are synthetic/offline checks, not after
latency measurements or owner-live acceptance.

No0.0.82 release/tag/feed publication.0.0.81 remains published unchanged.
The prepared changes do not yet resolve the latency acceptance blocker.
Required next: controlled package load; repeated shared-TEST measurements,
including focus acquisition/active/expiry, repeated Show, B-rank Next,
reconnect/backoff and the long-tail path; identify any remaining cause and correct
it; run Security/Privacy and Compatibility on the final exact package only after
latency correction; then publish the next authorized Testing version.

Do not alter Site/source/schema/Stable/main/production based on a hypothesis.

## Completed controlled validation and publication

Artifact source42cf0b11f1c499261bf56dec11d851d08dfe6d70 was loaded by the owner
with installed81 disabled. No rebuild occurred after exact-package review or
live measurement. Ten authenticated intentional Show actions against shared TEST
gave the following finite results (milliseconds):

| Stage | Median | Worst |
| --- | ---: | ---: |
| Agent action dispatch → map API invocation | 769.08 | 1081.21 |
| Agent action dispatch → Native poll | 687.50 | 959.00 |
| Poll → claim response received | 23.27 | 156.82 |
| Site queue → claim (DB transaction stamps) | 556.71 | 803.95 |
| Claim response → map API invocation | 62.95 | 136.55 |
| Consume HTTP dispatch → response headers | 16.18 | 101.40 |
| Validated consume acknowledgement → map invocation | 11.14 | 47.54 |

Zero duplicate invocations were observed. Owner feedback was "Prompt, no
unexplained repeats" for the first eight actions; later actions are instrumented
results, not separately owner-attested. Owner's natural gameplay produced
distinct automatic transitions, not repeats of consumed commands. Hunt
completion remained functional; no gameplay was requested or automated.

Leaving Active Hunt for more than30s and returning gave a real background-focus
resume response of1938.46ms, followed by reacquired active focus1120.33ms. Neither
replayed a consumed request. B-rank Next/racing/no-wrap and reconnect/backoff
recovery passed exact-package/current-Site fixtures, not live fault injection.
The owner explicitly requested a B-rank Next fixture; repeated authenticated
wire validation was real, not simulated. The earlier preparation list must not
be read as proof that every case was measured live.

Limitations: action dispatch is not an exact browser POST timestamp and includes
automation-to-click overhead; map API invocation is not rendered-pixel time.
Browser POST→queue time is UNKNOWN. DB NOW stamps are transaction starts, not
HTTP arrival/commit. Consume succeeds before presentation; presentation→consume
is N/A by design. The original>30s incident cannot be retrospectively attributed
to one failure; corrected scheduling/issuer-clock defects are independently
reproduced mechanisms, not fabricated original-request traces. The finite after
run met the target; this is not a guarantee against all future long tails.

Security/Privacy and Compatibility both ACCEPTED the final unchanged source and
exact artifact for Testing publication. Their two documentation/evidence
FOLLOW-UPs were corrected: attestation is limited to eight actions and this
record separates live, fixture, and unknown evidence. No remaining code/package
BLOCKER was found.

Published v0.0.82-testing on2026-10-04; source42cf0b11, annotated tag
e86f62830e6bfce102d7e3a0e6b416112be075a2. Anonymous immutable and rolling downloads
match accepted bytes. The unchanged rolling64 JSON now resolves82. Prior
immutable release assets and historical64 notes suffix are unchanged. See
[publication record](../../data/releases/testing-0.0.82.json) for full hashes.

Stable/main/production/Site source/schema/deployment/Market architecture/Wardrobe
were not changed. No game writes, extra sensitive reads or TLS bypass. Testing
command polling alone changed; existing backoff and permissions remain intact.

Owner procedure: keep the same repository URL and configuration. Disable the
diagnostic dev-plugin entry before enabling the installed Testing82 plugin;
never run both. No reset, re-pairing or additional delivery/Hunt action required.
