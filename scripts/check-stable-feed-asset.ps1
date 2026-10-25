# CI guard: the Stable feed in this commit must point at a published, matching
# release asset. A feed change merged before its GitHub Release exists fails here.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$entries = @([IO.File]::ReadAllText((Join-Path $root 'data/GillionsGameSync.json')) | ConvertFrom-Json -AsHashtable)
if ($entries.Count -ne 1) { throw 'The Stable feed must contain exactly one entry.' }
$version = [string]$entries[0].AssemblyVersion
$asset = "https://github.com/anndrox/GillionsGameSync/releases/download/v$version/GillionsGameSync-$version.zip"
foreach ($field in @('DownloadLink', 'DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting')) {
  if ($entries[0][$field] -cne $asset) { throw "$field does not point at the immutable v$version release asset." }
}
$recorded = ([IO.File]::ReadAllText((Join-Path $root "data/releases/v$version.sha256")).Split(' ', [StringSplitOptions]::RemoveEmptyEntries)[0]).ToLowerInvariant()
$temp = Join-Path ([IO.Path]::GetTempPath()) ("gillions-feed-asset-" + [Guid]::NewGuid().ToString('N') + '.zip')
try {
  try { $response = Invoke-WebRequest -UseBasicParsing -Uri $asset -OutFile $temp -PassThru }
  catch { throw "The Stable feed advertises $version, but its release asset is not published ($asset). Publish the asset with publish-stable-github-release.ps1 before merging the feed change." }
  if ($response.StatusCode -ne 200) { throw "The release asset returned HTTP $($response.StatusCode)." }
  $actual = (Get-FileHash -LiteralPath $temp -Algorithm SHA256).Hash.ToLowerInvariant()
  if ($actual -cne $recorded) { throw "The published v$version asset ($actual) does not match the recorded checksum ($recorded)." }
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $archive = [IO.Compression.ZipFile]::OpenRead($temp)
  try {
    $reader = [IO.StreamReader]::new($archive.GetEntry('GillionsGameSync.json').Open())
    try { $embedded = $reader.ReadToEnd() | ConvertFrom-Json -AsHashtable } finally { $reader.Dispose() }
  } finally { $archive.Dispose() }
  if ($embedded.InternalName -cne 'GillionsGameSync' -or $embedded.AssemblyVersion -cne $version) { throw 'The published asset has the wrong embedded identity or version.' }
  Write-Output "Stable feed asset check passed: v$version $actual"
}
finally { if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force } }
