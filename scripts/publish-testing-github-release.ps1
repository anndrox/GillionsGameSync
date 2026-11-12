param(
  [Parameter(Mandatory = $true)][ValidatePattern('^\d+\.\d+\.\d+(?:\.\d+)?$')][string]$Version,
  [Parameter(Mandatory = $true)][ValidatePattern('^\d{4}\.\d{2}\.\d{2}\.\d{4}\.\d{4}$')][string]$GameBuild,
  [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-f]{40}$')][string]$ReviewedHead,
  [Parameter(Mandatory = $true)][string]$NotesFile
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'testing-site-admission.ps1')
$root = Split-Path -Parent $PSScriptRoot
$assemblyVersion = if ($Version.Split('.').Count -eq 3) { "$Version.0" } else { $Version }
$repo = 'anndrox/GillionsGameSync-Testing'
$repoUrl = "https://github.com/$repo"
$feedPath = 'data/GillionsGameSyncTesting.json'
$endpoint = "repos/$repo/contents/$feedPath"
$tag = "v$Version-testing"
$zipName = "GillionsGameSyncTesting-$Version.zip"
$manifestName = 'GillionsGameSyncTesting.json'
$assetUrl = "$repoUrl/releases/download/$tag/$zipName"
$notes = Get-Content -LiteralPath $NotesFile -Raw

function Get-Sha256([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

if (git -C $root status --porcelain) { throw 'The Testing release checkout must be clean.' }
$source = git -C $root rev-parse HEAD
$parents = (git -C $root show -s --format=%P HEAD).Split(' ')
if ($LASTEXITCODE -ne 0 -or $parents.Count -ne 2 -or $parents[1] -cne $ReviewedHead) {
  throw 'Check out the merge commit of the exact reviewed Testing head.'
}
git -C $root diff --quiet $ReviewedHead $source
if ($LASTEXITCODE -ne 0) { throw 'The merged tree differs from the reviewed Testing tree.' }
git -C $root fetch origin main
if ($LASTEXITCODE -ne 0) { throw 'Could not fetch source main.' }
git -C $root merge-base --is-ancestor $source origin/main
if ($LASTEXITCODE -ne 0) { throw 'The Testing release source must be merged to main.' }
$project = Join-Path $root 'GillionsGameSync.csproj'
$configuredVersion = dotnet msbuild $project -nologo -getProperty:Version -p:GillionsTestBuild=true
if ($LASTEXITCODE -ne 0 -or $configuredVersion -cne $assemblyVersion) { throw 'The requested release version differs from the reviewed Testing project version.' }

Assert-TestingSiteAdmission -Version $assemblyVersion -GameBuild $GameBuild

$previous = gh api "${endpoint}?ref=main" | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or -not $previous.sha) { throw 'Could not read the current Testing feed.' }
$previousEntry = @([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($previous.content)) | ConvertFrom-Json)
if ($previousEntry.Count -ne 1 -or $previousEntry[0].InternalName -cne 'GillionsGameSyncTest' -or
    [version]$previousEntry[0].AssemblyVersion -gt [version]$assemblyVersion) { throw 'Unexpected Testing feed predecessor.' }

# Bind the manifest timestamp to source so the same release can be verified after a retry.
$publishedAt = [long](git -C $root show -s --format=%ct HEAD)
$package = @{ Channel = 'testing'; Version = $Version; PublishedAt = $publishedAt
  RepositoryUrl = $repoUrl; TestingReleaseBaseUrl = "$repoUrl/releases/download" }
& (Join-Path $PSScriptRoot 'package.ps1') @package
$directory = Join-Path $root "artifacts/package/testing/$Version"
$zip = Join-Path $directory $zipName
$manifest = Join-Path $directory $manifestName
$entry = @(Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json)
if ($entry.Count -ne 1 -or $entry[0].InternalName -cne 'GillionsGameSyncTest' -or $entry[0].AssemblyVersion -cne $assemblyVersion -or
    @('DownloadLink','DownloadLinkInstall','DownloadLinkUpdate','DownloadLinkTesting').Where({ $entry[0].$_ -cne $assetUrl }).Count -ne 0) {
  throw 'The package manifest does not identify the requested Testing release.'
}
$zipHash = Get-Sha256 $zip
$manifestHash = Get-Sha256 $manifest
$releaseNotes = Join-Path $directory 'publish-notes.md'
$sourceEvidence = "Source commit $source"
$hashEvidence = "ZIP SHA-256 $zipHash"
[IO.File]::WriteAllLines($releaseNotes, @($notes, '', $sourceEvidence, '', $hashEvidence), [Text.UTF8Encoding]::new($false))

$tagRef = git ls-remote --tags "$repoUrl.git" "refs/tags/$tag"
if ($LASTEXITCODE -ne 0) { throw 'Could not verify the Testing release tag.' }
if (-not $tagRef) {
  if ([version]$previousEntry[0].AssemblyVersion -ge [version]$assemblyVersion) { throw 'The Testing feed already advertises this version.' }
  $target = gh api "repos/$repo/branches/main" --jq '.commit.sha'
  if ($LASTEXITCODE -ne 0 -or $target -cnotmatch '^[0-9a-f]{40}$') { throw 'Could not verify the Testing distribution branch.' }
  Assert-TestingSiteAdmission -Version $assemblyVersion -GameBuild $GameBuild
  $releaseArgs = @('release','create',$tag,$zip,$manifest,'--repo',$repo,'--target',$target,'--prerelease','--latest=false',
    '--title',"Gillions Game Sync $assemblyVersion (Testing)",'--notes-file',$releaseNotes)
  gh @releaseArgs
  if ($LASTEXITCODE -ne 0) { throw 'Could not publish the Testing prerelease.' }
}

$release = gh release view $tag --repo $repo --json isDraft,isPrerelease,body,url | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $release.isDraft -or -not $release.isPrerelease -or
    -not $release.body.Contains($sourceEvidence) -or -not $release.body.Contains($hashEvidence)) {
  throw 'The Testing prerelease does not match the reviewed source and ZIP.'
}
$publicZip = Join-Path $directory 'published.zip'
$publicManifest = Join-Path $directory 'published-manifest.json'
Invoke-WebRequest -Uri $assetUrl -OutFile $publicZip -TimeoutSec 30
Invoke-WebRequest -Uri "$repoUrl/releases/download/$tag/$manifestName" -OutFile $publicManifest -TimeoutSec 30
if ((Get-Sha256 $publicZip) -cne $zipHash -or (Get-Sha256 $publicManifest) -cne $manifestHash) {
  throw 'The anonymously downloaded Testing assets differ from the verified package.'
}

$update = [Convert]::ToBase64String([IO.File]::ReadAllBytes($publicManifest))
$old = [Convert]::ToBase64String([Convert]::FromBase64String($previous.content))
if ($old -cne $update) {
  if ([version]$previousEntry[0].AssemblyVersion -ge [version]$assemblyVersion) { throw 'The Testing feed already points to a different package for this version.' }
  $requestFile = Join-Path $directory 'feed-update.json'
  $request = @{ message = "Publish Gillions Game Sync Testing $assemblyVersion"; branch = 'main'; sha = $previous.sha; content = $update }
  [IO.File]::WriteAllText($requestFile, ($request | ConvertTo-Json -Compress), [Text.UTF8Encoding]::new($false))
  Assert-TestingSiteAdmission -Version $assemblyVersion -GameBuild $GameBuild
  gh api --method PUT $endpoint --input $requestFile --jq '.commit.sha'
  if ($LASTEXITCODE -ne 0) { throw 'Could not advance the Testing feed.' }
}

$feed = "https://raw.githubusercontent.com/$repo/main/$feedPath"
$live = Join-Path $directory 'live-feed.json'
Invoke-WebRequest -Uri $feed -Headers @{ 'Cache-Control' = 'no-cache' } -OutFile $live -TimeoutSec 15
if ((Get-Sha256 $live) -cne $manifestHash) { throw 'The canonical Testing feed has not converged on the published manifest.' }
Write-Output "Testing $assemblyVersion is published at $($release.url)"
Write-Output "ZIP SHA-256 $zipHash"
Write-Output "Testing feed $feed"
