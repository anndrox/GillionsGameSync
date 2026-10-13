# Unpublished live status/window correction

Preparation from published Stable 1.0.31.2's source lineage; no new publication,
Site admission, main integration, permission change or rollout is authorized here.
The original released source/artifacts remain immutable.

The owner resumed bounded release-candidate preparation on 2026-10-13. The
source correction now uses Public 1.0.31.3 for a distinguishable local package;
publication, main integration, Site mutation and player installation remain held.
See [candidate prerequisites](releases/v1.0.31.3.md). This does not retroactively
turn the earlier 0.0.0.0 offline renderer into a release or live acceptance.

Main, Settings and Pairing now use standard persistent, manually resizable Dalamud
windows instead of AlwaysAutoResize. Logical default/minimum dimensions are
420x340 / 360x220, 520x460 / 420x260, and 460x520 / 400x300 respectively.
Dalamud applies its normal scale and placement handling. Minimum constraints also
repair previously saved tiny widths. Shorter windows scroll; no frame forces the
player's geometry. Hunt keeps its existing 300x180 default / 220x100 minimum,
passive focus behavior and Lock position. Existing window IDs are unchanged.

Reconnect with a saved pairing is explicitly distinguished from first-run
onboarding. Existing pairing actions, recipients and success/failure behavior are
unchanged. A fresh Hunt read with no admitted target rows in the current area
now says there are no **observed targets in this area**, with current bill
coverage unverified. Fresh complete rows do not imply all current bills complete.
Unavailable/expired reads still suppress stale numbers and preserve uncertainty.
No collection, retention, cadence, routing, transport or consent changes.

Hunt Ready remains local-source readiness, not proof of private upload permission
or Site persistence. Ordinary connection/sync likewise does not prove optional
FATE/Market admission. Safe summary states do not contain raw response bodies,
credentials, player observations or identifiers. No new diagnostic custody added.

The maintained compiled-render harness now uses **all actual Window properties**,
not fabricated AutoResize properties for Main/Settings/Pairing. It exercises the
reported 180px width, long feature values, 100/125/150% scaling, resize and reopen.
Its synthetic ImGui pixels are CONTROLLED COMPILED RENDER, **not live FFXIV**.
Corrected in-game PNGs and independent visual judgment remain required before
claiming live visual acceptance; unit/render tests alone cannot satisfy that gate.

The earlier public-ui-polish ledger describes the historical candidate and its
unchanged deferred recommendations. This bounded owner-authorized correction
supersedes only its old content-auto-sizing description and uncertain empty-area
status wording. No unrelated recommendations are implemented.
