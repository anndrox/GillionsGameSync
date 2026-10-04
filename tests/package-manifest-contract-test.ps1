param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Assert-Condition([bool]$Condition, [string]$Message) {
  if (-not $Condition) { throw $Message }
}

& (Join-Path $root 'scripts/package.ps1') -Channel stable -Version 9.8.7 -PublishedAt 1
if ($LASTEXITCODE -ne 0) { throw 'Stable package fixture failed.' }
$stable = @([IO.File]::ReadAllText((Join-Path $root 'artifacts/package/stable/9.8.7/GillionsGameSync.json')) | ConvertFrom-Json -AsHashtable)[0]
$stableUrl = 'https://github.com/anndrox/GillionsGameSync/releases/download/v9.8.7/GillionsGameSync-9.8.7.zip'
Assert-Condition ($stable.IconUrl -ceq 'https://raw.githubusercontent.com/anndrox/GillionsGameSync/main/assets/GillionsGameSync-icon-v4.png') 'Stable packaging did not generate the canonical GitHub icon URL.'
foreach ($field in @('DownloadLink', 'DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting')) {
  Assert-Condition ($stable[$field] -ceq $stableUrl) "Stable packaging generated the wrong $field."
}

$testingOrigin = 'https://testing.invalid'
$testingReleaseBase = 'https://github.com/anndrox/GillionsGameSync/releases/download'
& (Join-Path $root 'scripts/package.ps1') -Channel testing -Version 0.0.0 -PublicBaseUrl $testingOrigin -TestingReleaseBaseUrl $testingReleaseBase -PublishedAt 1
if ($LASTEXITCODE -ne 0) { throw 'Testing package fixture failed.' }
$testing = @([IO.File]::ReadAllText((Join-Path $root 'artifacts/package/testing/0.0.0/GillionsGameSyncTesting.json')) | ConvertFrom-Json -AsHashtable)[0]
$embeddedTesting = [IO.File]::ReadAllText((Join-Path $root 'artifacts/package/testing/0.0.0/build/GillionsGameSyncTest.json')) | ConvertFrom-Json
Assert-Condition ($embeddedTesting.IconUrl -ceq $testing.IconUrl) 'Embedded testing manifest and feed must agree on the canonical icon.'
$testingUrl = "$testingReleaseBase/v0.0.0-testing/GillionsGameSyncTesting-0.0.0.zip"
Assert-Condition ($testing.InternalName -ceq 'GillionsGameSyncTest') 'Testing packaging changed the separate plugin identity.'
Assert-Condition ($testing.IconUrl -ceq 'https://raw.githubusercontent.com/anndrox/GillionsGameSync/main/assets/GillionsGameSync-icon-v4.png') 'Testing packaging must satisfy the hosted publisher canonical-icon contract.'
foreach ($field in @('DownloadLink', 'DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting')) {
  Assert-Condition ($testing[$field] -ceq $testingUrl) "Testing packaging did not use the requested canonical GitHub prerelease $field."
}

$manualTesting = @([IO.File]::ReadAllText((Join-Path $root 'data/GillionsGameSyncTesting.json')) | ConvertFrom-Json -AsHashtable)[0]
$manualTestingUrl = 'https://github.com/anndrox/GillionsGameSync/releases/download/v0.0.78-testing/GillionsGameSyncTesting-0.0.78.zip'
Assert-Condition ($manualTesting.InternalName -ceq 'GillionsGameSyncTest' -and $manualTesting.AssemblyVersion -ceq '0.0.78.0') 'Manual testing candidate must retain the separate testing identity on successor 0.0.78.'
foreach ($field in @('DownloadLink', 'DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting')) {
  Assert-Condition ($manualTesting[$field] -ceq $manualTestingUrl) "Manual testing candidate $field must resolve to its immutable 0.0.78 GitHub prerelease asset when published."
}

$fixedTestingFeed = 'https://github.com/anndrox/GillionsGameSync/releases/download/v0.0.64-testing/GillionsGameSyncTesting.json'
$feedEvidence = [IO.File]::ReadAllText((Join-Path $root 'data/releases/testing-update-feed.json')) | ConvertFrom-Json -AsHashtable
Assert-Condition ($feedEvidence.feed -ceq $fixedTestingFeed) 'Testing update URL must remain the owner-selected existing repository URL.'
$publishedVersion = $feedEvidence.version -replace '\.0$', ''
$publishedZip = "https://github.com/anndrox/GillionsGameSync/releases/download/v$publishedVersion-testing/GillionsGameSyncTesting-$publishedVersion.zip"
Assert-Condition ([Version]$feedEvidence.version -le [Version]$manualTesting.AssemblyVersion -and $feedEvidence.internalName -ceq $manualTesting.InternalName -and $feedEvidence.download -ceq $publishedZip) 'Published fixed-feed evidence must be a real published predecessor or the candidate, not falsely advanced by preparation.'
$readme = [IO.File]::ReadAllText((Join-Path $root 'README.md'))
$releasing = [IO.File]::ReadAllText((Join-Path $root 'docs/releasing.md'))
Assert-Condition ($readme.Contains($fixedTestingFeed) -and $releasing.Contains($fixedTestingFeed)) 'Installation and publication guidance must retain the fixed Testing update URL.'
Assert-Condition ($releasing.Contains('gh release upload v0.0.64-testing artifacts/package/testing/X.Y.Z/GillionsGameSyncTesting.json') -and $releasing.Contains('--clobber')) 'Testing publication must advance the existing manifest, not require a new repository entry.'

$publisher = [IO.File]::ReadAllText((Join-Path $root 'scripts/publish-stable-github-release.ps1'))
Assert-Condition ($publisher.Contains('gh release create')) 'Stable publication no longer creates a GitHub Release.'
Assert-Condition ($publisher.Contains('git -C $root push upstream $tag')) 'Stable publication no longer pushes the reviewed release tag to GitHub.'
Assert-Condition ($publisher.Contains('verify-public-stable-release.ps1')) 'Stable publication no longer verifies the public GitHub distribution chain.'
Assert-Condition (-not $publisher.Contains('gillions.app') -and -not $publisher.Contains('publish-gillions-sync-static-release')) 'Stable publication must not use Gillions artifact infrastructure.'

Write-Output 'Package manifest contract verification passed.'
