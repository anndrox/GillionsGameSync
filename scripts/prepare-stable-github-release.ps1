param(
  [Parameter(Mandatory = $true)]
  [ValidatePattern('^\d+\.\d+\.\d+$')]
  [string]$Version,

  [long]$PublishedAt = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = [xml][IO.File]::ReadAllText((Join-Path $root 'GillionsGameSync.csproj'))
$projectVersion = [string]$project.Project.PropertyGroup[0].Version
if ($projectVersion -cne $Version) {
  throw "Project version $projectVersion does not match requested stable version $Version."
}

& (Join-Path $PSScriptRoot 'package.ps1') -Channel stable -Version $Version -PublishedAt $PublishedAt
if ($LASTEXITCODE -ne 0) { throw 'Stable packaging failed.' }

$packageDirectory = Join-Path $root "artifacts/package/stable/$Version"
$artifact = Join-Path $packageDirectory "GillionsGameSync-$Version.zip"
$generatedManifest = Join-Path $packageDirectory 'GillionsGameSync.json'
$canonicalManifest = Join-Path $root 'data/GillionsGameSync.json'
$checksumDirectory = Join-Path $root 'data/releases'
$checksumFile = Join-Path $checksumDirectory "v$Version.sha256"
$hash = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant()

New-Item -ItemType Directory -Path $checksumDirectory -Force | Out-Null
[IO.File]::Copy($generatedManifest, $canonicalManifest, $true)
[IO.File]::WriteAllText($checksumFile, "$hash  GillionsGameSync-$Version.zip`n", [Text.UTF8Encoding]::new($false))

Write-Output "PreparedArtifact=$artifact"
Write-Output "SHA256=$hash"
Write-Output "UpdatedManifest=$canonicalManifest"
Write-Output "UpdatedChecksum=$checksumFile"
Write-Output 'Review, verify, commit, and integrate these files before publishing the tag and GitHub Release.'
