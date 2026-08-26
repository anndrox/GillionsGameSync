param(
  [Parameter(Mandatory = $true)]
  [ValidateSet('stable', 'testing')]
  [string]$Channel,

  [Parameter(Mandatory = $true)]
  [ValidatePattern('^\d+\.\d+\.\d+$')]
  [string]$Version,

  [string]$PublicBaseUrl = 'https://gillions.app',
  [string]$RepositoryUrl = 'https://github.com/anndrox/GillionsGameSync',
  [string]$StableReleaseBaseUrl = 'https://github.com/anndrox/GillionsGameSync/releases/download',
  [string]$StableIconUrl = 'https://raw.githubusercontent.com/anndrox/GillionsGameSync/main/assets/GillionsGameSync-icon-v4.png',
  [long]$PublishedAt = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'GillionsGameSync.csproj'
$parsedOrigin = $null
$PublicBaseUrl = $PublicBaseUrl.Trim().TrimEnd('/')
$RepositoryUrl = $RepositoryUrl.Trim().TrimEnd('/')
$StableReleaseBaseUrl = $StableReleaseBaseUrl.Trim().TrimEnd('/')
$StableIconUrl = $StableIconUrl.Trim()
$allowedSchemes = if ($Channel -eq 'testing') { @('http', 'https') } else { @('https') }
if (-not [Uri]::TryCreate($PublicBaseUrl, [UriKind]::Absolute, [ref]$parsedOrigin) -or
    $parsedOrigin.Scheme -notin $allowedSchemes -or $parsedOrigin.AbsolutePath -ne '/') {
  throw "PublicBaseUrl must be an absolute $($allowedSchemes -join ' or ') origin without a path."
}
$parsedRepository = $null
if (-not [Uri]::TryCreate($RepositoryUrl, [UriKind]::Absolute, [ref]$parsedRepository) -or
    $parsedRepository.Scheme -ne 'https' -or
    $parsedRepository.Host -ne 'github.com' -or
    $parsedRepository.AbsolutePath -ne '/anndrox/GillionsGameSync') {
  throw 'RepositoryUrl must be https://github.com/anndrox/GillionsGameSync.'
}
$parsedReleaseBase = $null
if (-not [Uri]::TryCreate($StableReleaseBaseUrl, [UriKind]::Absolute, [ref]$parsedReleaseBase) -or
    $parsedReleaseBase.Scheme -ne 'https' -or
    $parsedReleaseBase.Host -ne 'github.com' -or
    $parsedReleaseBase.AbsolutePath -ne '/anndrox/GillionsGameSync/releases/download') {
  throw 'StableReleaseBaseUrl must be the canonical GillionsGameSync GitHub Releases download path.'
}
$parsedIcon = $null
if (-not [Uri]::TryCreate($StableIconUrl, [UriKind]::Absolute, [ref]$parsedIcon) -or
    $parsedIcon.Scheme -ne 'https' -or
    $parsedIcon.Host -ne 'raw.githubusercontent.com' -or
    $parsedIcon.AbsolutePath -ne '/anndrox/GillionsGameSync/main/assets/GillionsGameSync-icon-v4.png' -or
    $parsedIcon.Query -or $parsedIcon.UserInfo) {
  throw 'StableIconUrl must be the canonical anonymous GitHub raw icon URL.'
}

$isTesting = $Channel -eq 'testing'
$internalName = if ($isTesting) { 'GillionsGameSyncTest' } else { 'GillionsGameSync' }
$displayName = if ($isTesting) { 'Gillions Game Sync Testing' } else { 'Gillions Game Sync' }
$zipBase = if ($isTesting) { 'GillionsGameSyncTesting' } else { 'GillionsGameSync' }
$output = Join-Path $root "artifacts/package/$Channel/$Version/build/"
$packageDirectory = Join-Path $root "artifacts/package/$Channel/$Version"
$zipPath = Join-Path $packageDirectory "$zipBase-$Version.zip"
$manifestPath = Join-Path $packageDirectory "$zipBase.json"

New-Item -ItemType Directory -Path $output -Force | Out-Null
$arguments = @('build', $project, '-c', 'Release', '-warnaserror', "-p:Version=$Version", "-p:GillionsPublicBaseUrl=$PublicBaseUrl", "-p:OutputPath=$output")
if ($isTesting) { $arguments += '-p:GillionsTestBuild=true' }
dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw "$displayName build failed." }

$packageFiles = @(
  (Join-Path $output "$internalName.dll"),
  (Join-Path $output "$internalName.deps.json"),
  (Join-Path $output "$internalName.json")
)
foreach ($file in $packageFiles) {
  if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing package file: $file" }
}
# Keep the immutable release artifact reproducible. Compress-Archive stamps
# entries with build time, which changes the SHA-256 even when every packaged
# file is byte-identical.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $zipPath) { [IO.File]::Delete($zipPath) }
$archive = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
try {
  foreach ($file in $packageFiles | Sort-Object { [IO.Path]::GetFileName($_) }) {
    $entry = $archive.CreateEntry([IO.Path]::GetFileName($file), [IO.Compression.CompressionLevel]::Optimal)
    $entry.LastWriteTime = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
    $source = [IO.File]::OpenRead($file)
    $destination = $entry.Open()
    try { $source.CopyTo($destination) }
    finally { $destination.Dispose(); $source.Dispose() }
  }
} finally { $archive.Dispose() }
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$downloadUrl = if ($isTesting) {
  "$PublicBaseUrl/downloads/plugins/$zipBase-$Version.zip"
} else {
  "$StableReleaseBaseUrl/v$Version/$zipBase-$Version.zip"
}
$iconUrl = if ($isTesting) {
  "$PublicBaseUrl/downloads/plugins/GillionsGameSync-icon-v4.png"
} else {
  $StableIconUrl
}

$manifest = @([ordered]@{
  Author = 'Gillions'
  Name = $displayName
  InternalName = $internalName
  AssemblyVersion = "$Version.0"
  Description = if ($isTesting) { 'Unreleased, opt-in test build for Gillions Game Sync. Install only when directed for in-game verification.' } else { 'Opt-in character synchronization for Gillions with separately gated Retainer planner integration. Never automates gameplay or sends Square Enix credentials.' }
  ApplicableVersion = 'any'
  RepoUrl = $RepositoryUrl
  Tags = if ($isTesting) { @('inventory', 'collection', 'testing') } else { @('inventory', 'collection', 'utility') }
  DalamudApiLevel = 15
  LoadRequiredState = 0
  LoadSync = $false
  CanUnloadAsync = $false
  LoadPriority = 0
  Punchline = if ($isTesting) { 'Unreleased Gillions sync test build.' } else { 'Opt-in account sync for your Gillions profile.' }
  AcceptsFeedback = $true
  IconUrl = $iconUrl
  DownloadLink = $downloadUrl
  DownloadLinkInstall = $downloadUrl
  DownloadLinkUpdate = $downloadUrl
  # This stable entry does not advertise TestingAssemblyVersion or
  # TestingDalamudApiLevel, so Dalamud cannot select its testing link. Keeping
  # it equal to the immutable stable asset is intentional; the separately
  # identified GillionsGameSyncTest product retains its own testing feed.
  DownloadLinkTesting = $downloadUrl
  DownloadCount = 0
  LastUpdate = $PublishedAt
})
[IO.File]::WriteAllText($manifestPath, ((ConvertTo-Json -InputObject $manifest -Depth 4) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))

Write-Output "Artifact=$zipPath"
Write-Output "SHA256=$hash"
Write-Output "Manifest=$manifestPath"
