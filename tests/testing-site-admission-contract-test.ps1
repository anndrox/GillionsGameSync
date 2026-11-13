$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $root 'scripts/testing-site-admission.ps1')
$version = '0.0.103.0'
$build = '2026.11.03.0000.0000'
$valid = @{
  ok = $true; admitted = $true; origin = 'https://test.gillions.app'
  product = 'GillionsGameSyncTest'; version = $version; currentGameBuild = $build
}
$admissionFixture = @{ response = $valid; status = 200; offline = $false }
function Invoke-WebRequest {
  param($Uri, $Method, $MaximumRedirection, $TimeoutSec, $ErrorAction, $Headers)
  if ($Uri -cne 'https://test.gillions.app/api/game-sync/testing-release-admission?product=GillionsGameSyncTest&version=0.0.103.0&gameBuild=2026.11.03.0000.0000' -or
      $Method -cne 'Get' -or $MaximumRedirection -ne 0 -or $TimeoutSec -ne 15 -or
      $Headers['Cache-Control'] -cne 'no-cache' -or $Headers.ContainsKey('Authorization')) {
    throw 'Unexpected request shape.'
  }
  if ($admissionFixture.offline) { throw 'Fixture transport failure.' }
  [pscustomobject]@{ StatusCode = $admissionFixture.status; Content = ($admissionFixture.response | ConvertTo-Json -Depth 5 -Compress) }
}

function Assert-Refused {
  try { Assert-TestingSiteAdmission -Version $version -GameBuild $build }
  catch {
    if ($_.Exception.Message -notmatch '^TEST has not confirmed|^Testing publication requires') { throw }
    return
  }
  throw 'An unconfirmed release passed the admission check.'
}
Assert-TestingSiteAdmission -Version $version -GameBuild $build
foreach ($change in @(
    @{ admitted = $false }, @{ admitted = 'true' }, @{ ok = $false },
    @{ version = '0.0.102.0' }, @{ currentGameBuild = '2026.11.04.0000.0000' },
    @{ product = 'GillionsGameSync' }, @{ origin = 'https://gillions.app' })) {
  $admissionFixture.response = $valid.Clone()
  foreach ($key in $change.Keys) { $admissionFixture.response[$key] = $change[$key] }
  Assert-Refused
}
$admissionFixture.response = $valid
foreach ($code in 301, 404, 503) { $admissionFixture.status = $code; Assert-Refused }
$admissionFixture.status = 200
$admissionFixture.offline = $true
Assert-Refused
$admissionFixture.offline = $false

# A denied publisher must stop before any GitHub call or packaging.
$approved = 'a' * 40
$merge = 'b' * 40
function git {
  $global:LASTEXITCODE = 0
  if ($args -contains 'rev-parse') { return $merge }
  if ($args -contains '--format=%P') { return "$('c' * 40) $approved" }
}
function dotnet { $global:LASTEXITCODE = 0; return '0.0.103.0' }
function gh { throw 'The denied publisher invoked GitHub.' }
$admissionFixture.response = $valid.Clone()
$admissionFixture.response.admitted = $false
try {
  $release = @{ Version = '0.0.103'; GameBuild = $build; ReviewedHead = $approved; NotesFile = (Join-Path $root 'docs/releasing.md') }
  & (Join-Path $root 'scripts/publish-testing-github-release.ps1') @release
  throw 'The denied publisher returned success.'
} catch {
  if ($_.Exception.Message -notmatch '^TEST has not confirmed exact admission') { throw }
}

$publisher = Get-Content -LiteralPath (Join-Path $root 'scripts/publish-testing-github-release.ps1') -Raw
foreach ($mutation in "  gh @releaseArgs", "  gh api --method PUT") {
  $start = $publisher.IndexOf($mutation, [StringComparison]::Ordinal)
  if ($start -lt 0 -or $publisher.LastIndexOf('Assert-TestingSiteAdmission', $start, [StringComparison]::Ordinal) -lt
      $publisher.IndexOf('package.ps1', [StringComparison]::Ordinal)) { throw "Missing late admission check before $mutation." }
}
Write-Output 'Testing publication admission contract passed.'
