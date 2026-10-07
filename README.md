# Gillions Game Sync

## Unpublished public UX candidate

Testing87 preparation adds a compact connection/health window, dedicated pairing,
General/Hunts/Connection/Advanced Settings and a default-OFF local Hunt Progress
panel. FATE collection no longer needs a diagnostic command: fresh authenticated
Site policy and exact source admission control its existing five-second lifecycle.
No new payload contract, gameplay automation or extra Hunt/location reads are added.

This candidate is **not published or ready for Stable promotion**. Site does not
yet provide the complete Site-owned permission contract needed to replace legacy
local contribution/request choices. Testing86 remains the installed feed. Existing
Stable capabilities are not silently promoted from Testing. See the
[candidate state and required Site correction](docs/public-release-candidate.md).

Published Testing86 wires the independent default-OFF Site FATE policy and bounded
authenticated sender, with accepted85 collection unchanged. Real shared TEST
intake, renewal, same-occurrence progress, privacy, rendering and natural expiry
passed. Prompt website-map delivery was confirmed with the sender active.
Keep the rolling Testing URL. Disable the dev copy before enabling installed
Testing; never run both. No rebuild followed acceptance. See
[Testing86 results](docs/releases/testing-0.0.86.md) and
[FATE capability and transport](docs/contracts/fate-live-observations-v1.md).

Published Testing85 adds an Advanced/Testing local FATE measurement session using
maintained typed APIs. It retains only bounded RAM observations; missing FATEs
remain unknown. Remote contribution stays disabled pending exact Site admission
and independent consent. Real-client collection, bounded costs and prompt PF-link
delivery were accepted; exact reviewed bytes were published without rebuilding.
Keep the same rolling Testing URL. See [Testing85 validation and Site handoff](docs/releases/testing-0.0.85.md)
and [FATE capability and activation requirements](docs/contracts/fate-live-observations-v1.md).

Published Testing84 corrects Hunt coverage refresh scheduling without changing
the15-second contract or three-second gameplay reads. Two real TEST windows had
32refresh gaps, median3.055s/worst6.082s and minimum8.361s lease margin, with no
misses. Exact reviewed bytes were published without rebuilding; see
[Testing84 validation](docs/releases/testing-0.0.84.md).

Published Testing83 adds explicit private Hunt bill-item coverage: absent confirmed,
present unresolved or unavailable. Complete Key Items absence can exclude a
historical bill type from current routing without changing progress, but only
after Site admits the new version. No exact current order/cycle claim, new history,
faster gameplay reads or Stable change. See the [Site contract and mapping](docs/contracts/hunt-bill-items-v1.md).

Testing81 adds exact Site Hunt V2 requests and ephemeral Active Hunt focus on
secure TEST. B-rank Next remains an intentional Site action; ordinary hunts use
one reference anchor. Focus is priority, not consent, travel sharing or faster
native reads. V1 fallback and the owner-live-proven80 completion correction remain.
See [Testing81 acceptance steps](docs/releases/testing-0.0.81.md).

Testing80 adds supported numeric Hunt progress events when the native counter cache
does not advance. It binds LogMessage4411 to one recently observed exact bill,
rechecks the obtained order, and preserves positive progress against stale same-order
reads. No localized chat parsing or completion-by-disappearance. See
[correction, Site map handoff and live retest](docs/releases/testing-0.0.80.md).

Testing79 corrects Hunt final-counter retention and responsiveness. Bounded local
reads run every three seconds; semantic changes prompt enabled private TEST sync
without another periodic phase. Sync now requests an eligible fresh Hunt read and
personal send without bypassing OFF/capability/backoff gates. Explicit complete
counters can bridge bill-item removal only for the same order corroborated in
this session within six seconds; missing data alone never means completion.
Routine timestamp refreshes no longer create Hunt upload successors. See
[correction and live retest](docs/releases/testing-0.0.79.md).

Testing78 adds separate default-OFF automatic Site Hunt-map presentation using the
existing bounded request channel. Enable **Automatically show my current Hunt in
FFXIV** locally and select/grant your Testing device on the TEST Hunts page. The
Site selects the current target; accepted Hunt progress can advance the native
map without returning to Site between kills. Areas are reference locations, not
sightings. FATE requirements remain explicit, activity unknown. No teleport or
gameplay actions. V1 has no Next possible location operation; missed maps can be
retried with Site's Show in FFXIV. See [Hunt-map contract](docs/contracts/hunt-map-v1.md).

Testing77 activates the accepted shared TEST transport for the separate default-OFF
private Hunt routing-context experiment introduced in76:
rounded location and positive public teleport observations, RAM/latest-only.
Travel needs its own server grant and exact capability agreement at HTTPS
test.gillions.app; one RAM-only preparation expires45s after observation. Cached
Gil is not a proven final charge. See the [exact capability/transport contract](docs/contracts/travel-context-v1.md).

Gillions Game Sync is the open-source Dalamud plugin for [Gillions](https://gillions.app). This source targets `1.0.30`, with read-only character and Retainer synchronization, safer pairing and offline records, and a simpler settings window. The [stable manifest](data/GillionsGameSync.json) on `main` and [GitHub Releases](https://github.com/anndrox/GillionsGameSync/releases) identify the publicly available build; a task branch or local package is not a published update.

Automatic sync is one global control for the supported categories: inventory, currencies, achievements, collectibles, character progress, quest journal, reputation, Shared FATEs and glamour plates. Retainer observations and venture results use a separate server compatibility acknowledgement. There is no per-category chooser. Unavailable game data is preserved or omitted instead of being reported as an intentional deletion.

Collectibles include authoritative Master Recipe Book and Regional Folklore tome unlocks. The plugin reads tome ownership from native unlock state and sends stable tome item IDs; it does not infer ownership from inventory or unrelated collections.

Version `1.0.30` removes venture planning, AutoRetainer discovery and IPC, plan delivery, and apply/restore controls. Native Retainer observations, inventory, listings and venture results remain. Older local plans, backups, queues and maps stay preserved but inactive when their account ownership cannot be proved. This update does not modify plans already installed in AutoRetainer or add a history recovery/export feature.

## Install and update

Add the stable custom repository URL to Dalamud:

`https://raw.githubusercontent.com/anndrox/GillionsGameSync/main/data/GillionsGameSync.json`

Keep that URL when updating through Dalamud's plugin installer. Existing users must **pair once after updating to `1.0.30`** because older credentials did not record a verified issuing origin. Do not delete the configuration. Confirm the pairing destination shown in the plugin, enter a fresh one-time code from Gillions, and choose **Pair this device**. Website history is unchanged; older local records with unverified ownership will not be assigned to the newly paired account or uploaded automatically.

The testing feed is separate and should be installed only when a Gillions test is requested:

`https://github.com/anndrox/GillionsGameSync/releases/download/v0.0.64-testing/GillionsGameSyncTesting.json`

Updating this plugin does not cancel external AutoRetainer plans or guarantee cancellation of callbacks already queued by an older loaded plugin. Use AutoRetainer's own controls when you need that automation to stop. See the [release notes](docs/releases/v1.0.30.md).

## Pair and sync

- Open the plugin's Dalamud configuration window, or use `/gillionssync pair` to open its pairing controls.
- Published Stable30: `/gillionssync` opens the window and requests a manual sync.
  The unpublished public UX candidate separates these actions: `/gillionssync`
  opens the main window; `/gillionssync sync` and Advanced **Sync now** explicitly
  request sync. Testing uses `/gillionssynctest` with the same subcommands.
- **Automatic sync** checks one ordinary category every 30 seconds. Inventory changes, queued Gil records and changed Retainer observations use their own deadlines. The two-second Gil fallback and 750 ms dirty delay are unchanged.
- Pairing and startup with a valid pairing perform one character sync and presence request even when Automatic sync is off. Recurring collection remains off in that case.
- **Allow website 'Link in game' requests** is a separate control, on by default. While paired and logged in, it polls for authenticated requests and prints a requested native item link after the server consumes its claim. It does not automate gameplay.

Connection details, update history, data status and diagnostics are collapsed by default. The pairing destination and actionable account/storage warnings remain visible. Editing the HTTPS server address changes the destination for the next pairing; an existing device credential stays bound to its original origin. Disconnecting clears that credential while preserving local history.

The visible settings view refreshes at most four times per second during ordinary updates, with immediate refresh eligibility after queued settings changes or session resets. Closed settings windows skip publication. Static Armoire catalog definitions are reused while current ownership is checked on each inventory sync. Party Finder batch sorting, serialization and HTTP startup run on a worker. Diagnostic recording includes framework-thread collection time so in-game performance can be checked separately from network time; see [performance testing](docs/testing.md#performance-validation).

## Offline records

New pending evidence is owned by a pairing generation and authoritative character content ID. Acknowledgements retire only the exact versions sent. Switching characters, pairing again, opting out of a request mode, or unloading the plugin cancels the affected work and prevents stale replies from changing a new session.

Pending data across all new pairing generations and characters shares a limit of **10,000 records or 8 MiB of serialized data**, whichever is reached first. At capacity the plugin keeps admitted records, pauses new event recording and shows a coverage-gap warning. Native Gil observations keep their existing cadence. After acknowledged uploads free space, recording starts from a fresh balance baseline; missed events are not invented or recovered.

Re-pairing does not free storage or authorize uploading another generation's records. Inactive or uncertain records may continue to occupy the limit. Legacy unowned history remains outside this new budget, and the budget does not promise a maximum total configuration-file size. Synthetic fixtures measure storage accounting; they do not establish hours of player coverage. See [Privacy](docs/privacy.md).

## Optional Party Finder contribution

Both channels use separate off-by-default contribution choices and listen only to Dalamud's authoritative Party Finder event: no automatic game queries. Public-listing payload/source attribution is [Remote Party Finder / xivpf](https://xivpf.com); attribution is not the testing recipient. Uploads wait ten seconds after the newest listing and begin at most six requests in any rolling minute per load, including failed attempts.

Stable builds send the `UploadableListing` payload directly to xivpf's HTTPS receiver, independently of Gillions pairing. Gillions does not proxy or retain stable contributions, and no Gillions credential accompanies them. Stable pending and in-flight batches each retain at most 1,000 listings.

Testing `0.0.75` corrects the historical production target: it derives `/api/game-sync/party-finder/contribute` from the authenticated session restricted to `https://test.gillions.app`, not xivpf, production or localhost. It requires explicit Testing pairing, site-recorded public PF permission and separate local opt-in. Existing production pairings are not moved; ordinary sync remains independent. The token is only Gillions Authorization, never listing data. Current state is an expiring runtime cache, not durable history or proof of page integration. See [exact audit and Site handoff](docs/contracts/party-finder-native-requests-v1.md). Provider attribution: **Data provided by xivpf.com**, with its link.

Testing keeps at most 1,000 pending identities and 100 in-flight listings, with requests at most 256 KiB. Retry timestamps are immutable; stale/expired observations are discarded. Disabling contribution clears unsent memory and cancels active work where possible. A 401/403 persists only a non-secret blocked enrollment-generation marker: logout, character changes, off/on and reload do not reset it. Correct account/permission and enroll again. Redirect/404 endpoint failures instead stop the load until deployment is corrected and the plugin reloaded. No Square Enix credential, private chat, diagnostic or unrelated local data is contributed.

## Testing candidate: Beastmaster local read

The combined testing candidate includes an opt-in Beastmaster view, disabled
by default. Open **Beastmaster local test** in testing settings or use /gillionsbst.
Results stay in memory and are not uploaded. See [local test instructions](docs/beastmaster-local-test.md).

For the combined Beastmaster and Party Finder test, add this URL to Dalamud's
custom plugin repositories:

`https://github.com/anndrox/GillionsGameSync/releases/download/v0.0.64-testing/GillionsGameSyncTesting.json`

This installs the separate **Gillions Game Sync Testing** identity. It does not
replace the stable plugin or update the existing `gillions.app` testing feed.
Testing `0.0.75` preserves independently consented private Hunt/Submarine TEST sync from74 and corrects the PF origin. New website PF links require separate default-OFF consent and the compatible Site request contract; until then they are dormant. Supported native chat delivery requires a final game click. Keep this same rolling repository URL and update to `0.0.75.0`; no configuration reset or downgrade. See [release and limits](docs/releases/testing-0.0.75.md) and [personal consent/live Hunt test](docs/releases/testing-0.0.74.md). Custom Deliveries, ordinary sync, item links, Beastmaster and Market are preserved; Dashboard facts remain local. Real PF/Hunt transport and live FC validation are not established by publication. Owner FC access does not block future [Submarine production beta](docs/contracts/submarine-production-beta-readiness.md), but its server/Stable/privacy gates still apply.

Open `/gillionssubs` or **Submarine voyage retention**. Use workshop interfaces
manually; no submarine control, polling or third-party plugin is involved. Local
history retains at most 400 voyage anchors/results and 32 snapshots below 4 MiB,
with visible overflow and no silent eviction. A second community-preparation
opt-in and explicit sanitized copy exclude names/FC/account/credential data; no
public community submarine upload exists. Separately consented private TEST sync
is available in74; see [transport semantics](docs/contracts/testing-personal-transport-v1.md).
Departure and missing producing-time
build/sector evidence remain unavailable. See [schema, examples and Site requirements](docs/contracts/submarine-voyages-v1.md).

## Build and verify

### Testing 0.0.72 Timers, Custom Deliveries and Doman

Published `0.0.73` corrects the selected-client counter source after a live72
`ClientCounterMismatch`. It corroborates catalog-indexed manager usage/rank/
satisfaction with the active agent and calculates client residual capacity;
the agent's ambiguous remaining field is unsupported. Global allowances remain
unchanged. See [diagnosis and capability state](docs/releases/testing-0.0.73.md).
The immutable73 and74 packages remain preserved; the rolling Testing feed uses the current reviewed successor.

The successor separates valid global allowance observations from rejected client
details and adds a persistent, session-local **Custom Delivery read** reason to
the facts window and aggregate diagnostics. It does not relax shared native
load/reset gates or add uploads. The owner provided72 client-counter rejection
evidence;73 addresses the source assumption without claiming live73 success.
The Timers
addon lookup is corrected to `ContentsInfo`. Loaded Doman Enclave native donation
totals are retained locally, without inferring weekly completion or reset.
See the [all-timer GitHub reference audit](docs/timer-plugin-references.md) for
implemented versus unsupported categories and owner-waived live checks.

### Testing 0.0.71 daily / weekly facts

Testing 0.0.71 supplies a separate default-OFF `/gillionsfacts` local experiment
for roulette reward flags, Custom Delivery allowances/rank, Challenge Log
completion, weekly-earned tomestones, Wondrous Tails, allowances/expected timers
and weekly PvP counters. It does not store Dashboard configuration or upload new
resources. Native-cache ownership/reset freshness remains unverified; unknown
sources preserve prior observations. See the [capability audit](docs/dashboard-capability-audit.md),
[private schema/Site handoff](docs/contracts/dashboard-facts-v1.md) and
[published successor/live checklist](docs/releases/testing-0.0.71.md).
The existing rolling Testing feed supplies this update; Stable is unchanged.


### Testing 0.0.67 market predecessor

Testing 0.0.67 was published October 1, 2026 at tag `v0.0.67-testing`, source
`9821c92df8f51c9fef913a9e8294fd6ed3b4469c`. Its immutable assets are preserved;
0.0.75 uses the same separate product identity. Publication is not live-game proof.

"Contribute observed market data to Gillions" is independently **on by default**,
including older Testing configurations without that field. It has a persistent
off switch. Supported receive events supply only naturally encountered partial
listings and recent sales, never automatic searches. Uploads require an exact
authenticated server compatibility acknowledgment. Ordinary sync, Party Finder and Dalamud's own contribution
preference remain independent. Read the [contract/Site handoff](docs/contracts/market-observations-v1.md)
and [candidate checks](docs/releases/testing-0.0.67.md).

### Testing 0.0.70 personal-state experiments

Testing 0.0.69 corrects Hunt collection: the existing framework loop reads at most
every five seconds, requiring matching loaded bill Key Items. Bill windows and
logout are not prerequisites when coherent data is available. Keep the same
Testing URL. 0.0.70 adds localized target names and one wrapped row per target;
numeric IDs remain diagnostic fallbacks. Labels use a bounded static catalog
cache, not native reads or sheet lookups every UI frame. See
[0.0.70 retest instructions](docs/releases/testing-0.0.70.md).

Open `/gillionshunts` or **My Hunt Bills local test**; local retention defaults OFF.
Naturally loaded caches with matching bill Key Items supply positive targets/counters. Cache
ownership, current acceptance, complete bill set and resets remain unverified.
Open `/gillionssubs` for off-by-default local submarine retention; selected visible
planning interfaces can add positive unlocked/explored sector history, not a full
unlock-set claim. Exact game/SDK gates stop these experiments after unknown patches.
Each offers explicit **PRIVATE** export for trusted diagnostics, not community
contribution. In74, separate default-OFF private TEST sync controls can send these
observations after explicit Testing enrollment/permission. See
[current transport and bounded Site handoff](docs/contracts/testing-personal-transport-v1.md).
No radar, game requests, UI opening, voyage actions or third-party dependency.

Requirements are Windows, .NET 10 SDK and the Dalamud dependencies used by `Dalamud.NET.Sdk`.

```powershell
./scripts/verify.ps1
./scripts/package.ps1 -Channel stable -Version 1.0.30
./scripts/package.ps1 -Channel testing -Version 0.0.0
```

Verification includes synthetic policy tests, source contracts, both product builds and actual Dalamud configuration load/Save/reload fixtures. It does not run a game session. Packages stay under ignored `artifacts/`; building does not publish or install them.

## Source and releases

`main` is the public stable source line. The reviewed [`data/GillionsGameSync.json`](data/GillionsGameSync.json) is the stable Dalamud manifest. Stable ZIPs are immutable [GitHub Release assets](https://github.com/anndrox/GillionsGameSync/releases), checksums live under [`data/releases`](data/releases), and the icon lives under [`assets`](assets). Candidate preparation, independent review, publication and a user's installed version are separate states. Testing retains its own product identity and publication path.

See [Testing](docs/testing.md), [Releasing](docs/releasing.md), [Privacy](docs/privacy.md) and [Contributing](CONTRIBUTING.md).

## License

Gillions Game Sync uses the [MIT License](LICENSE).
