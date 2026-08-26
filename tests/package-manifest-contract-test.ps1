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
& (Join-Path $root 'scripts/package.ps1') -Channel testing -Version 0.0.0 -PublicBaseUrl $testingOrigin -PublishedAt 1
if ($LASTEXITCODE -ne 0) { throw 'Testing package fixture failed.' }
$testing = @([IO.File]::ReadAllText((Join-Path $root 'artifacts/package/testing/0.0.0/GillionsGameSyncTesting.json')) | ConvertFrom-Json -AsHashtable)[0]
$testingUrl = "$testingOrigin/downloads/plugins/GillionsGameSyncTesting-0.0.0.zip"
Assert-Condition ($testing.InternalName -ceq 'GillionsGameSyncTest') 'Testing packaging changed the separate plugin identity.'
Assert-Condition ($testing.IconUrl -ceq "$testingOrigin/downloads/plugins/GillionsGameSync-icon-v4.png") 'Testing packaging changed its existing icon host.'
foreach ($field in @('DownloadLink', 'DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting')) {
  Assert-Condition ($testing[$field] -ceq $testingUrl) "Testing packaging changed the existing $field behavior."
}

$publisher = [IO.File]::ReadAllText((Join-Path $root 'scripts/publish-stable-github-release.ps1'))
Assert-Condition ($publisher.Contains('gh release create')) 'Stable publication no longer creates a GitHub Release.'
Assert-Condition ($publisher.Contains('git -C $root push upstream $tag')) 'Stable publication no longer pushes the reviewed release tag to GitHub.'
Assert-Condition ($publisher.Contains('verify-public-stable-release.ps1')) 'Stable publication no longer verifies the public GitHub distribution chain.'
Assert-Condition (-not $publisher.Contains('gillions.app') -and -not $publisher.Contains('publish-gillions-sync-static-release')) 'Stable publication must not use Gillions artifact infrastructure.'

Write-Output 'Package manifest contract verification passed.'
