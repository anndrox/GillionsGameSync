# Hunt support recovery (source change, unreleased)

Stable 1.0.31.7 stops an unchanged Hunt preparation after a terminal HTTP 400.
Refreshing its observation time does not retry it. This source change permits
one fresh preparation when an accepted authenticated presence advertises changed
support for the sample's exact native pair and all included reference builds.
Another terminal rejection stays stopped for that unchanged input/session even
if support revisions alternate. Other terminal statuses retain their stop.

The presence response must itself pass permission-authority ordering. An older
ignored response preserves both the newer lease and its support, and spends no
recovery budget. Support is compatibility information, not permission: explicit
Off, expired/revoked authority, session/character ownership, native qualification,
current observations, cancellation, receipt validation and retry gates remain.
No restart, re-pair, manual upload, receiver selection or new toggle is required.

`python tests/hunt-recovery-test.py` runs the actual managed presence, permission,
retention, preparation and sender paths with in-process I/O. The maintained
fixtures include unchanged support, bounded recovery, older issued authority,
later accepted support, second rejection, delayed responses, Off and identity
isolation. They do not establish native gameplay or an actual Site receipt.

## Later release and Site work

The prepared Stable candidate is **GillionsGameSync / 1.0.31.8**; see its
[release notes](releases/v1.0.31.8.md). The project and branch-local candidate feed
use that version. Public main/feed remain 1.0.31.7 until independent release
review, compatible Site deployment and the authorized asset-before-feed release
procedure in [releasing.md](releasing.md). Never replace 1.0.31.7's immutable
source/tag/asset. Preparation establishes no installed update.

The intended Hunt tuple remains game/reference `2026.10.16.0000.0000`, Dalamud
API 15 / `15.0.3.9`, ClientStructs `7.57.0.9414`, Lumina `7.7.0` / Excel `7.5.1`.
Fresh bill and coverage collectorVersion will be `1.0.31.8`. Native qualification
does not grant Site version admission. Before release, separately review the
exact successor in enrollment/presence, Hunt admission and matching SQL rechecks
for the intended Site profiles, preserving other family gates. Current Site
admission does not include this successor. Preserve mixed-collector rejection;
do not relabel retained old observations as newly collected ones.

No Site image/source change, deployment or release is part of this source PR.
