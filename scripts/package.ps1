param(
  [Parameter(Mandatory = $true)]
  [ValidateSet('stable', 'testing')]
  [string]$Channel,

  [Parameter(Mandatory = $true)]
  [ValidatePattern('^\d+\.\d+\.\d+$')]
  [string]$Version,

  [string]$PublicBaseUrl = 'https://gillions.app',
  [long]$PublishedAt = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'GillionsGameSync.csproj'
$parsedOrigin = $null
$PublicBaseUrl = $PublicBaseUrl.Trim().TrimEnd('/')
$allowedSchemes = if ($Channel -eq 'testing') { @('http', 'https') } else { @('https') }
if (-not [Uri]::TryCreate($PublicBaseUrl, [UriKind]::Absolute, [ref]$parsedOrigin) -or
    $parsedOrigin.Scheme -notin $allowedSchemes -or $parsedOrigin.AbsolutePath -ne '/') {
  throw "PublicBaseUrl must be an absolute $($allowedSchemes -join ' or ') origin without a path."
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
$downloadUrl = "$PublicBaseUrl/downloads/plugins/$zipBase-$Version.zip"

$manifest = @([ordered]@{
  Author = 'Gillions'
  Name = $displayName
  InternalName = $internalName
  AssemblyVersion = "$Version.0"
  Description = if ($isTesting) { 'Unreleased, opt-in test build for Gillions Game Sync. Install only when directed for in-game verification.' } else { 'Opt-in character synchronization for Gillions with separately gated Retainer planner integration. Never automates gameplay or sends Square Enix credentials.' }
  ApplicableVersion = 'any'
  RepoUrl = $PublicBaseUrl
  Tags = if ($isTesting) { @('inventory', 'collection', 'testing') } else { @('inventory', 'collection', 'utility') }
  DalamudApiLevel = 15
  LoadRequiredState = 0
  LoadSync = $false
  CanUnloadAsync = $false
  LoadPriority = 0
  Punchline = if ($isTesting) { 'Unreleased Gillions sync test build.' } else { 'Opt-in account sync for your Gillions profile.' }
  AcceptsFeedback = $true
  IconUrl = "$PublicBaseUrl/downloads/plugins/GillionsGameSync-icon-v4.png"
  DownloadLink = $downloadUrl
  DownloadLinkInstall = $downloadUrl
  DownloadLinkUpdate = $downloadUrl
  DownloadLinkTesting = $downloadUrl
  DownloadCount = 0
  LastUpdate = $PublishedAt
})
[IO.File]::WriteAllText($manifestPath, ((ConvertTo-Json -InputObject $manifest -Depth 4) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))

Write-Output "Artifact=$zipPath"
Write-Output "SHA256=$hash"
Write-Output "Manifest=$manifestPath"
