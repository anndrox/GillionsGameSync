# CI guard: the Stable feed in this commit must point at a published, matching
# release asset. A feed change merged before its GitHub Release exists fails here.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$entries = @([IO.File]::ReadAllText((Join-Path $root 'data/GillionsGameSync.json')) | ConvertFrom-Json -AsHashtable)
if ($entries.Count -ne 1) { throw 'The Stable feed must contain exactly one entry.' }
$assemblyVersion = [string]$entries[0].AssemblyVersion
# The release (tag) version may be three-part (v1.0.30 ships AssemblyVersion 1.0.30.0).
$asset = [string]$entries[0].DownloadLink
$match = [regex]::Match($asset, '^https://github\.com/anndrox/GillionsGameSync/releases/download/v(\d+\.\d+\.\d+(?:\.\d+)?)/GillionsGameSync-(\d+\.\d+\.\d+(?:\.\d+)?)\.zip$')
if (-not $match.Success -or $match.Groups[1].Value -cne $match.Groups[2].Value) { throw 'DownloadLink is not an immutable GitHub release asset URL.' }
$version = $match.Groups[1].Value
$normalized = if ($version.Split('.').Count -eq 3) { "$version.0" } else { $version }
if ($normalized -cne $assemblyVersion) { throw "DownloadLink release v$version does not match AssemblyVersion $assemblyVersion." }
foreach ($field in @('DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting')) {
  if ($entries[0][$field] -cne $asset) { throw "$field does not point at the immutable v$version release asset." }
}
$checksumPath = Join-Path $root "data/releases/v$version.sha256"
if (-not (Test-Path -LiteralPath $checksumPath)) { throw "The recorded checksum data/releases/v$version.sha256 is missing." }
$recorded = ([IO.File]::ReadAllText($checksumPath).Split(' ', [StringSplitOptions]::RemoveEmptyEntries)[0]).ToLowerInvariant()
if ($recorded -notmatch '^[0-9a-f]{64}$') { throw "The recorded checksum for v$version is malformed." }
$temp = Join-Path ([IO.Path]::GetTempPath()) ("gillions-feed-asset-" + [Guid]::NewGuid().ToString('N') + '.zip')
try {
  try { Invoke-WebRequest -UseBasicParsing -Uri $asset -OutFile $temp -MaximumRetryCount 3 -RetryIntervalSec 5 }
  catch { throw "The Stable feed advertises v$version, but its release asset could not be downloaded ($asset): $($_.Exception.Message). Publish the asset with publish-stable-github-release.ps1 before merging the feed change; re-run if this was a network failure." }
  $actual = (Get-FileHash -LiteralPath $temp -Algorithm SHA256).Hash.ToLowerInvariant()
  if ($actual -cne $recorded) { throw "The published v$version asset ($actual) does not match the recorded checksum ($recorded)." }
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $archive = [IO.Compression.ZipFile]::OpenRead($temp)
  try {
    $reader = [IO.StreamReader]::new($archive.GetEntry('GillionsGameSync.json').Open())
    try { $embedded = $reader.ReadToEnd() | ConvertFrom-Json -AsHashtable } finally { $reader.Dispose() }
  } finally { $archive.Dispose() }
  if ($embedded.InternalName -cne 'GillionsGameSync' -or $embedded.AssemblyVersion -cne $assemblyVersion) { throw 'The published asset has the wrong embedded identity or version.' }
  Write-Output "Stable feed asset check passed: v$version $actual"
}
finally { if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force } }
