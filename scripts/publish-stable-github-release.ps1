param(
  [Parameter(Mandatory = $true)]
  [ValidatePattern('^\d+\.\d+\.\d+$')]
  [string]$Version
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$tag = "v$Version"
$assetName = "GillionsGameSync-$Version.zip"
$assetPath = Join-Path $root "artifacts/package/stable/$Version/$assetName"
$manifestPath = Join-Path $root 'data/GillionsGameSync.json'
$checksumPath = Join-Path $root "data/releases/$tag.sha256"

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'GitHub CLI is required.' }
& gh auth status | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'GitHub CLI authentication is unavailable.' }
if ((& git -C $root status --porcelain)) { throw 'The release worktree must be clean.' }
& git -C $root fetch upstream --tags
if ($LASTEXITCODE -ne 0) { throw 'Could not refresh the canonical GitHub remote.' }
$head = (& git -C $root rev-parse HEAD).Trim()
$canonicalContains = @(& git -C $root branch -r --contains $head) -match '^\s*upstream/main$'
if (-not $canonicalContains) { throw 'The exact release source and manifest commit must be integrated into upstream/main first.' }

$manifest = @([IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -AsHashtable)[0]
if ($manifest.AssemblyVersion -cne "$Version.0") { throw 'Canonical manifest version does not match the requested release.' }
$expectedUrl = "https://github.com/anndrox/GillionsGameSync/releases/download/$tag/$assetName"
foreach ($field in @('DownloadLink', 'DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting')) {
  if ($manifest[$field] -cne $expectedUrl) { throw "$field does not match the requested immutable release asset." }
}

& (Join-Path $PSScriptRoot 'package.ps1') -Channel stable -Version $Version -PublishedAt ([long]$manifest.LastUpdate)
if ($LASTEXITCODE -ne 0) { throw 'Stable packaging failed.' }
$expectedHash = ([IO.File]::ReadAllText($checksumPath).Split(' ', [StringSplitOptions]::RemoveEmptyEntries)[0]).ToLowerInvariant()
$actualHash = (Get-FileHash -LiteralPath $assetPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -cne $expectedHash) { throw "Prepared artifact hash $actualHash does not match recorded hash $expectedHash." }

$peeledTagLine = @(& git -C $root ls-remote upstream "refs/tags/$tag^{}" 2>$null) | Select-Object -First 1
$directTagLine = @(& git -C $root ls-remote upstream "refs/tags/$tag" 2>$null) | Select-Object -First 1
$remoteTag = if ($peeledTagLine) {
  ([string]$peeledTagLine).Split("`t")[0]
} elseif ($directTagLine) {
  ([string]$directTagLine).Split("`t")[0]
} else {
  $null
}
if ($remoteTag) {
  if ($remoteTag -cne $head) { throw "Existing tag $tag does not identify the exact integrated release commit." }
}
else {
  & git -C $root tag -a $tag -m "Gillions Game Sync $Version" $head
  if ($LASTEXITCODE -ne 0) { throw "Could not create tag $tag." }
  & git -C $root push upstream $tag
  if ($LASTEXITCODE -ne 0) { throw "Could not push tag $tag." }
}

$releaseExists = $true
& gh release view $tag --repo anndrox/GillionsGameSync | Out-Null
if ($LASTEXITCODE -ne 0) { $releaseExists = $false }
if (-not $releaseExists) {
  & gh release create $tag $assetPath --repo anndrox/GillionsGameSync --verify-tag --latest --title $tag --notes "Stable Gillions Game Sync $Version.`n`nSHA-256: ``$expectedHash``"
  if ($LASTEXITCODE -ne 0) { throw "Could not create GitHub Release $tag." }
}
else {
  $release = (& gh release view $tag --repo anndrox/GillionsGameSync --json assets) | ConvertFrom-Json
  $existingAsset = @($release.assets | Where-Object { $_.name -ceq $assetName })
  if ($existingAsset.Count -eq 0) {
    & gh release upload $tag $assetPath --repo anndrox/GillionsGameSync
    if ($LASTEXITCODE -ne 0) { throw "Could not upload $assetName to existing GitHub Release $tag." }
  }
  elseif ($existingAsset.Count -ne 1) {
    throw "GitHub Release $tag has duplicate $assetName assets."
  }
}

& (Join-Path $PSScriptRoot 'verify-public-stable-release.ps1') -Version $Version
if ($LASTEXITCODE -ne 0) { throw 'Public GitHub release verification failed.' }
Write-Output "GitHub stable release $tag is published and verified."
