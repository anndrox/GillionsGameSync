# Public Game Sync release candidate

Current successor: [real unpublished Public 1.0.31 freeze](public-1.0.31-freeze.md).
The sections below record accepted Testing87 and its earlier promotion limits;
the successor reconciles Public runtime gates without changing the frozen UI.

Testing87's accepted source is `c286e5140470fd866cf43f2db92f18aa4d170f4c`.
Its immutable prerelease and unchanged rolling Testing feed are published and
anonymously verified. See [Testing87 results](releases/testing-0.0.87.md).
It is not ready for public promotion: final Stable capability wiring, product
admission and owner approval remain held. Immutable Testing86 and Stable30 remain
unchanged. Disable the dev copy before enabling installed Testing; preserve pairing
and configuration, and never load two copies.

## Implemented presentation

The main window has connection, recognized character, last sync and four health
rows, with account/storage warnings where action is needed. It has no feature
tabs or privacy switches. The dedicated welcome/pair/ready window reuses the
one-time-code protocol. An already bound connection does not repeat onboarding.
Pairing displays the normalized next destination and uses that same destination
for the website link and code submission, separate from current-account privacy.
Invalid destinations require an explicit default recovery action. Bounded action
feedback preserves pairing recovery and manual sync/disconnect results without
copying raw server messages. Direct FATE diagnostics and copied reports warn
that world, territory and observation times reveal private presence.
The approved repository/feed icon is embedded unchanged and loaded by Dalamud's
shared resource provider: larger in pairing, restrained in main and small in
Settings. No replacement art or Site asset scraping is used. Hunt Progress has
no decorative artwork. Testing live UX is accepted; separate public/Stable
exact-artifact and owner review remains outstanding.
Settings has General, Hunts, Connection and Advanced tabs. Support copying uses
fixed status labels and bounded version strings, not raw errors or payloads.

Both builds use the standard Dalamud WindowSystem. Placement follows Dalamud;
main, pairing and Settings size to content. Hunt Progress is manually resizable,
keeps the chosen size through content changes and reopening, and uses standard
Dalamud/ImGui geometry persistence rather than plugin configuration. A scaled
220-by-100 minimum preserves a usable viewport; longer contents wrap and scroll.
Lock position prevents movement, not resizing. Hunt Progress suppresses focus/navigation capture,
has no sound/animation and optionally locks movement. A manual close suppresses
auto-show in the same territory until manual reopening or a territory change.

## Local Hunt Progress

Show Hunt Progress defaults OFF. Enabling it lets the existing three-second Hunt
reader publish session-local display rows, including when retained-history consent
is OFF. It does not enable retention, personal uploads or any other permission.
This one reader is available in the proposed public build too; raw Hunt diagnostic
windows/commands and private upload transport remain Testing-only.

Fresh admitted current-area targets show remaining count primarily, with the
observed/required ratio secondarily. A supported completion transition is held for
2.5 seconds. Unavailable/expired state hides numeric rows. Labels use static native
references; an unmapped target can use an unambiguous static territory reference,
without coordinates or Site map guidance. Unknown territory association is not
guessed. Retained history never supplies a higher display count.

Current Key Item coverage proves an item domain, not exact acquisition, cache
ownership or reset generation. It cannot prove all current targets complete.
The runtime preserves the approved updating/unknown fallback. The all-complete
view exists only for a future authoritative coverage proof and controlled model
evidence; no stronger live capability is claimed.

## Automatic FATE lifecycle

The existing five-second collector starts only under fresh exact authenticated
Site policy/source admission, pairing and supported settled context. Discovery
can bootstrap before settlement; sending cannot. Missing/OFF/expired/malformed
authorization stops collection and discards unsent RAM. Reauthorization requires
fresh observations. No manual activation loop, configuration permission, history,
game request or additional location read is introduced. Site admits the exact
Testing86 and Testing87 source tuples. An ordinary authenticated presence reports
the real successor before optional FATE discovery. `/gillionsfates` only opens
Testing diagnostics.

## Site permission reconciliation

Authenticated ordinary presence now supplies `permissionAuthority` revision 1
with eight independent decisions and a 30-second lease. Native binds the response
to the paired enrollment, rejects older issuance, and uses conservative issuer
time plus a monotonic deadline. A malformed decision disables only that decision;
an invalid root/binding disables the migrated authority without breaking ordinary
sync. No generations, private data or extra credentials are persisted.

Legacy decisions preserve historical local settings, including OFF. Fresh explicit
Site ON/OFF supersedes those settings without mutating them. Item OFF does not
disable explicit PF links; automatic-map OFF does not disable explicit Hunt Show.
Site classifies and filters automatic commands on the unchanged wire contract.
Permission transitions clear obsolete prepared contributions, while retained
local history remains intact. OFF-to-ON requires fresh observations. Market's
service acknowledgment is not consent. Public31 admission remains held.

FATE schema1, PF, Market, ordinary sync, personal Hunt/submarine/travel and command
payload contracts are unchanged. Actual Testing87 acceptance passed the applicable
practical publication gates; source/fixture checks are documented separately,
not substituted for physical evidence.
Owner-approved provider attribution: a **Data provided by** label followed by a
clickable **xivpf.com** button linking to `https://xivpf.com`. Site should use the
same label plus provider-domain button, including when other providers are added.
This presentation does not imply Gillions originates the data or change contracts.

## Command and control inventory

| Surface | Classification | Candidate behavior |
| --- | --- | --- |
| `/gillionssync` or Testing `/gillionssynctest` | Public normal | Open main; no implicit sync |
| `pair` subcommand | Public connection | Open welcome/reconnection; reuse existing auth |
| `sync` subcommand and Advanced Sync now | Public advanced | Explicit existing manual sync |
| Dalamud configuration action | Public normal | Open tabbed Settings |
| Show Hunt Progress and Lock position | Public local presentation | No retention/upload/route permission |
| Privacy and data settings link | Public normal | Paired-origin `/gillions-sync` |
| Copy support summary | Public advanced | Whitelisted safe status only |
| `/gillionsfates` | Testing only | Open bounded diagnostics; cannot activate collection |
| `/gillionshunts`, `/gillionssubs`, `/gillionsfacts`, `/gillionsbst` | Testing only | Existing focused local research/validation views; private exports remain explicitly labeled |
| Old manual FATE Start/Stop session | Retired | Automatic Site-gated lifecycle only |
| Raw public diagnostic report UI | Retired | Safe summary replaces raw log copying |
| Old main privacy toggles, raw origin editor, feature panels | Retired UI | Legacy config remains preserved; fresh explicit Site decisions supersede it |

## Validation and evidence limits

Maintained tests cover prior collectors/transports, actual Dalamud serializer
upgrade/restart/OFF isolation, FATE policy-loss discard, public-state transitions,
single-loop/source boundaries and synthetic secret exclusion. Controlled images
call the actual compiled candidate Draw methods with synthetic states and installed
Dalamud font, then rasterize their ImGui triangles. They are not FFXIV captures or
proof of native focus, keyboard/controller operation, placement or live transport.
The renderer never creates a second product UI or uses Windows input.

Source, hashes, review verdicts and Site identity are bound in durable owner
evidence and the [publication record](../data/releases/testing-0.0.87.json).
Real87 automatic FATE lifecycle, consent transitions, private Hunt intake,
independent links and manual/automatic maps were checked after exact admission.
Historical86 success alone was not used to accept changed87 behavior.
The owner physically accepted c286 geometry, keyboard/chat and Lock position;
controller was not tested and live FC submarine collection is outstanding.
Security/Privacy and Compatibility acceptance for unchanged b392 surfaces remains
applicable, not a newly claimed independent c286 review. Exact tests were rebound
to c286. Ordering is NO ISSUE; no display-name deduplication, Site target lookup,
new polling or themed header was added.

## Release and recovery

Testing87 and the proposed Stable31 have separate assembly identities, config
files, conditional capabilities and manifests. They cannot be byte-identical
cross-channel artifacts. Review/freeze Testing87 separately; the eventual public
artifact needs equivalent exact-artifact acceptance after its contract is ready.
Existing package tooling stages ignored artifacts without changing published feeds.

No Stable/main/Site source/schema/production change occurred. Testing's exact
immutable assets were published first; only the established rolling JSON advanced
after the remaining practical live checks passed. No immutable bytes were replaced.
Stable publication still needs the separate owner review, compatible public admission
and normal source/manifest integration authority.

Recovery keeps immutable predecessor artifacts. A later approved Stable promotion
would advance the established Stable main manifest and immutable GitHub release;
an authorized rollback would restore the predecessor manifest pointer, not replace
an immutable ZIP. Do not exercise that mutation now. New presentation fields are
additive, but an older serializer may drop them on save; preserve a user-controlled
config backup and obtain compatibility judgment before any binary downgrade.

## Testing player release notes

Game Sync has a smaller status window and clearer connection setup. Settings are
organized into four tabs. Optional Hunt Progress shows local remaining counts in
your current area and stays closed when you dismiss it. FATE observations run
automatically only when Gillions authorizes them. Privacy controls live on Gillions;
Game Sync never moves, fights, teleports or automatically joins parties.

These notes describe the available Testing87 successor. Final public/Stable
review and product admission remain required before Stable release readiness.
