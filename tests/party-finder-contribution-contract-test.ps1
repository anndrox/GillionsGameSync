param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = [IO.File]::ReadAllText((Join-Path $root 'PartyFinderContribution.cs'))
$plugin = [IO.File]::ReadAllText((Join-Path $root 'Plugin.cs'))
$project = [IO.File]::ReadAllText((Join-Path $root 'GillionsGameSync.csproj'))

function Assert-Contains([string]$Needle, [string]$Message) {
  if (-not $source.Contains($Needle)) { throw $Message }
}

foreach ($field in @(
  'id','content_id_lower','name','description','created_world','home_world',
  'current_world','category','duty','duty_type','beginners_welcome',
  'seconds_remaining','min_item_level','num_parties','slots_available',
  'last_server_restart','objective','conditions','duty_finder_settings',
  'loot_rules','search_area','slots','jobs_present','accepting'
)) {
  Assert-Contains "[JsonPropertyName(`"$field`")]" "Party Finder contribution is missing the official $field payload field."
}

Assert-Contains 'TimeSpan.FromSeconds(10)' 'Party Finder contribution must retain the ten-second batching and request-rate floor.'
Assert-Contains 'if (disposed || !enabled()) return;' 'Party Finder collection must remain disabled unless the player opts in.'
Assert-Contains 'batches.Clear();' 'Disabling or disposing Party Finder contribution must clear pending listings.'
Assert-Contains 'new HttpRequestMessage(HttpMethod.Post, endpoint)' 'Party Finder listings must post directly to the configured xivpf endpoint.'
Assert-Contains 'GroupBy(listing => (listing.LastServerRestart, listing.CreatedWorld, listing.Id))' 'Party Finder batches must deduplicate official listing identities.'

if (-not $plugin.Contains('EnablePartyFinderContributions { get; set; } = false')) { throw 'Party Finder contribution must be off by default.' }
if (-not $plugin.Contains('They never pass through Gillions.')) { throw 'The settings disclosure must identify the direct contribution boundary.' }
if (-not $project.Contains('https://xivpf.com/contribute/multiple')) { throw 'Stable builds must retain the official xivpf contribution endpoint.' }
if (-not $project.Contains('http://127.0.0.1:8000/contribute/multiple')) { throw 'Testing builds must default to the loopback Remote Party Finder endpoint.' }

Write-Output 'Party Finder contribution contract verification passed.'
