param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$adapter = [IO.File]::ReadAllText((Join-Path $root 'PartyFinderContribution.cs'))
$core = [IO.File]::ReadAllText((Join-Path $root 'PartyFinderContributionCore.cs'))
$intake = [IO.File]::ReadAllText((Join-Path $root 'GillionsPartyFinderContributor.cs'))
$plugin = [IO.File]::ReadAllText((Join-Path $root 'Plugin.cs'))
$project = [IO.File]::ReadAllText((Join-Path $root 'GillionsGameSync.csproj'))
$behavior = [IO.File]::ReadAllText((Join-Path $root 'tests/GillionsGameSync.PartyFinderTests/Program.cs'))

function Require([bool]$Condition, [string]$Message) {
  if (-not $Condition) { throw $Message }
}

foreach ($field in @(
  'id','content_id_lower','name','description','created_world','home_world',
  'current_world','category','duty','duty_type','beginners_welcome',
  'seconds_remaining','min_item_level','num_parties','slots_available',
  'last_server_restart','objective','conditions','duty_finder_settings',
  'loot_rules','search_area','slots','jobs_present','accepting'
)) {
  Require $core.Contains("[JsonPropertyName(`"$field`")]") "Party Finder contribution is missing the official $field payload field."
}

foreach ($mapping in @(
  '(uint)listing.Id', '(uint)listing.ContentId', 'listing.Name.Encode()', 'listing.Description.Encode()',
  '(ushort)listing.World.Value.RowId', '(ushort)listing.HomeWorld.Value.RowId', '(ushort)listing.CurrentWorld.Value.RowId',
  '(uint)listing.Category', 'listing.RawDuty', '(byte)listing.DutyType', 'listing.BeginnersWelcome',
  'listing.SecondsRemaining', 'listing.MinimumItemLevel', 'listing.Parties', 'listing.SlotsAvailable',
  '(uint)listing.LastPatchHotfixTimestamp', '(uint)listing.Objective', '(uint)listing.Conditions',
  '(uint)listing.DutyFinderSettings', '(uint)listing.LootRules', '(uint)listing.SearchArea',
  'slot.Accepting.Aggregate(0u', 'listing.RawJobsPresent.ToArray()'
)) {
  Require $adapter.Contains($mapping) "Dalamud Party Finder mapping is missing: $mapping"
}

Require ($core.Contains('TimeSpan.FromSeconds(10)') -and $core.Contains('MaximumRequestsPerMinute = 6')) 'Batch/retry timing and rolling request ceiling must remain explicit.'
Require $core.Contains('MaximumPendingListings = 1000') 'Pending Party Finder data must retain a fixed bound.'
Require ($core.Contains('pending.Clear();') -and $core.Contains('CancelSafely(cancel);')) 'Opt-out must clear unsent data and cancel active work without racing request completion.'
Require ($core.Contains('AllowAutoRedirect = false') -and $plugin.Contains('PartyFinderHttp.CreateClient()')) 'Contribution HTTP must use a dedicated client that cannot follow redirects.'
Require $core.Contains('UseCookies = false') 'Contribution HTTP must not accept/replay cookies across enrollments.'
Require ($behavior.Contains('XivpfEndpointPolicy.RequireSafe(endpoint, true)') -and $behavior.Contains('using var http = PartyFinderHttp.CreateClient();')) 'The optional integration harness must require loopback and reject redirects.'
Require ($adapter.Contains('partyFinderGui.ReceiveListing += OnListing') -and $adapter.Contains('partyFinderGui.ReceiveListing -= OnListing')) 'Dalamud Party Finder event lifecycle is incomplete.'
Require $adapter.Contains('if (disposed || !enabled()) return;') 'Disabled contribution must reject the authoritative event before mapping or copying listing data.'
Require ($adapter.Contains('XivpfEndpointPolicy.RequireBuildSafe(endpoint, true)') -and $core.Contains('endpoint != ProductionEndpoint') -and $core.Contains('endpoint != GillionsPartyFinderContributor.Endpoint')) 'Built products must enforce fixed Gillions testing intake and the official stable remote endpoint.'
Require ($adapter.Contains('ordinary Gillions Game Sync remains active') -and $adapter.Contains('new DisabledPartyFinderContributor()')) 'Unsafe contribution configuration must fail closed without disabling ordinary Game Sync.'
Require $plugin.Contains('EnablePartyFinderContributions { get; set; } = false') 'Party Finder contribution must be off by default.'
Require $plugin.Contains('partyFinderContributor.SetEnabled(enablePartyFinderContributions)') 'The setting must apply opt-out clearing immediately.'
Require ($plugin.IndexOf('RequestConfigurationSave();', $plugin.IndexOf('configuration.EnablePartyFinderContributions = enablePartyFinderContributions;', [StringComparison]::Ordinal), [StringComparison]::Ordinal) -lt $plugin.IndexOf('partyFinderContributor.SetEnabled(enablePartyFinderContributions)', [StringComparison]::Ordinal)) 'Opt-out persistence must be requested before cancellation is applied.'
Require $plugin.Contains('Util.OpenLink("https://xivpf.com")') 'The settings disclosure must visibly link to xivpf.com.'
$tick = $plugin.IndexOf('partyFinderContributor.Tick(now)', [StringComparison]::Ordinal)
$pairingGate = $plugin.IndexOf('if (!HasPairedSession || activeOwnedState is null || !clientState.IsLoggedIn) return;', [StringComparison]::Ordinal)
Require ($tick -ge 0 -and $pairingGate -gt $tick) 'Party Finder contribution must remain independent from Gillions pairing and login sync gates.'
Require ($project.Contains('https://xivpf.com/contribute/multiple') -and $project.Contains('https://test.gillions.app/api/game-sync/party-finder/contribute')) 'Stable and authenticated testing endpoint defaults are incomplete.'
Require $plugin.Contains('EnableGillionsPartyFinderContributions { get; set; } = false') 'New recipient must require a separate local opt-in.'
Require ($plugin.Contains('CapturePartyFinderSession') -and $plugin.Contains('configuration.ActiveSession!.Origin != GillionsPartyFinderContributor.ApprovedTestingOrigin') -and $plugin.Contains('GillionsPartyFinderContributor.SessionEndpoint(permit.Origin)') -and $plugin.Contains('RequirePermit(permit);')) 'Authenticated intake must remain bound to the approved current TEST paired session.'
Require (-not $project.Contains('https://gillions.app/api/game-sync/party-finder/contribute') -and -not $intake.Contains('https://gillions.app/api/game-sync/party-finder/contribute')) 'Testing PF must have no production fallback.'
Require ($plugin.Contains('Data provided by xivpf.com') -and -not $plugin.Contains('Powered by xivpf.com')) 'User-facing xivpf attribution wording must match the owner direction.'
Require ($plugin.Contains('NativePartyFinderLinkFactory.Create(r)') -and $plugin.Contains('ConsumePartyFinderLinkAsync') -and $plugin.Contains('requestLifetime.InvalidateItemLinks();') -and $plugin.Contains('PartyFinderLinkPolicy.Parse')) 'PF requests must use shared item polling/consume, native factory, strict validation and lifetime invalidation.'
Require ($adapter.Contains('new GillionsPartyFinderContributor') -and $intake.Contains('MaximumBodyBytes = 262144') -and $intake.Contains('Take(batchLimit)') -and $intake.Contains('batchLimit = 100')) 'Testing transport must implement bounded contract batches.'
Require ($intake.Contains('ValidateAcknowledgement(bytes, batch)') -and $intake.Contains('blockedAuthorization = captured.AuthorizationKey') -and $intake.Contains('await captured.RecordAuthorizationDenial();') -and $intake.Contains('Requeue(row)')) 'Acknowledgement, durable authorization stop and immutable retry boundaries must remain explicit.'
Require ($plugin.Contains('GillionsPartyFinderBlockedGeneration { get; set; } = ""') -and $plugin.Contains('configuration.ActiveSession?.Generation != authorizationGeneration') -and $plugin.Contains('configuration.GillionsPartyFinderBlockedGeneration = authorizationGeneration;')) 'Denial must persist only the captured enrollment generation independently of epoch/character changes.'

Write-Output 'Party Finder contribution integration contract verification passed.'
